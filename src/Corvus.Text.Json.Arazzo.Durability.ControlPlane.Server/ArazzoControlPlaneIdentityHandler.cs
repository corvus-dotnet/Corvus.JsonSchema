// <copyright file="ArazzoControlPlaneIdentityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiIdentityHandler"/> (design §16.5.4): resolve real grantees
/// (person/team/role/workflow) to their exact deployment-stamped <c>sys:</c> identity for the view/operate/administer
/// grant pickers, and report the caller's own identity. Resolution draws on the store-indexed observed-identity
/// typeahead (<see cref="IObservedIdentityStore"/>) and an optional pluggable directory (<see cref="IPrincipalDirectory"/>);
/// identities are described as <c>{dimension, value}</c> grants, never raw <c>sys:</c> tags.
/// </summary>
/// <remarks>
/// <c>whoami</c> and <c>capabilities</c> are open to any authenticated caller; the grantee search is gated by
/// <c>administrators:read</c>. All three are read-only and non-mutating. Identity features are meaningful only when a
/// row-security policy is configured (an unscoped deployment resolves to nothing).
/// </remarks>
public sealed class ArazzoControlPlaneIdentityHandler : IApiIdentityHandler
{
    private const int DefaultLimit = 20;

    private readonly IObservedIdentityStore observed;
    private readonly IPrincipalDirectory? directory;
    private readonly ControlPlaneAccess access;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneIdentityHandler"/> class.</summary>
    /// <param name="observed">The store-indexed observed-identity typeahead.</param>
    /// <param name="directory">An optional external directory for live search; <see langword="null"/> if none is configured.</param>
    /// <param name="access">Resolves the caller's identity and maps grantees to/from internal tags.</param>
    internal ArazzoControlPlaneIdentityHandler(IObservedIdentityStore observed, IPrincipalDirectory? directory, ControlPlaneAccess access)
    {
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(access);
        this.observed = observed;
        this.directory = directory;
        this.access = access;
    }

    /// <inheritdoc/>
    public ValueTask<GetWhoamiResult> HandleGetWhoamiAsync(GetWhoamiParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CredentialUsageGrant> identity = this.access.CallerIdentityGrants();
        Models.WhoAmI.Source body = new((ref Models.WhoAmI.Builder b) => b.Create(
            identity: new Models.WhoAmI.AdministratorIdentityArray.Source((ref Models.WhoAmI.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (CredentialUsageGrant grant in identity)
                {
                    ab.AddItem(ToIdentity(grant));
                }
            })));
        return new ValueTask<GetWhoamiResult>(GetWhoamiResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<GetIdentityCapabilitiesResult> HandleGetIdentityCapabilitiesAsync(GetIdentityCapabilitiesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GranteeKind> kinds = this.access.SupportedGranteeKinds();
        bool hasDirectory = this.directory is not null;
        Models.IdentityCapabilities.Source body = new((ref Models.IdentityCapabilities.Builder b) => b.Create(
            directorySearch: hasDirectory,
            granteeKinds: new Models.IdentityCapabilities.GranteeKindArray.Source((ref Models.IdentityCapabilities.GranteeKindArray.Builder ab) =>
            {
                foreach (GranteeKind kind in kinds)
                {
                    ab.AddItem(kind.ToToken());
                }
            })));
        return new ValueTask<GetIdentityCapabilitiesResult>(GetIdentityCapabilitiesResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    public async ValueTask<SearchGranteesResult> HandleSearchGranteesAsync(SearchGranteesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GranteeKind? kind = null;
        if (parameters.Kind.IsNotUndefined())
        {
            using UnescapedUtf8JsonString kindToken = ((JsonElement)parameters.Kind).GetUtf8String();
            if (GranteeKinds.TryParse(kindToken.Span, out GranteeKind k))
            {
                kind = k;
            }
        }

        string source = parameters.Source.IsNotUndefined() ? (string)parameters.Source : "observed";
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : DefaultLimit;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        // Reach-scope discovery to the caller's read reach (§17.1) — the same AccessContext idiom every other store uses.
        AccessContext context = this.access.Current();

        // The search prefix flows to the (UTF-8) observed store as owned, pooled memory that survives the await — read
        // once via GetUtf8String().TakeOwnership and returned to the pool in the finally; no managed string here.
        byte[]? prefixRented = null;
        try
        {
            ReadOnlyMemory<byte> prefix = parameters.Q.IsNotUndefined()
                ? ((JsonElement)parameters.Q).GetUtf8String().TakeOwnership(out prefixRented)
                : default;

            // The projected grantee (ResolvedGrantee.Source) is a ref struct, so it cannot be collected into a list; the
            // array is built inline from the live results, which stay in scope through Ok (it materialises the body).
            if (string.Equals(source, "directory", StringComparison.Ordinal) && this.directory is not null)
            {
                // The directory query is a string leaf (the IPrincipalDirectory seam is string-typed — an LDAP filter / an
                // HTTP URI), so transcode the prefix once for it.
                IReadOnlyList<ResolvedPrincipal> found = await this.directory.SearchAsync(kind ?? GranteeKind.Person, Encoding.UTF8.GetString(prefix.Span), limit, cancellationToken).ConfigureAwait(false);

                // Project the resolved principals BYTES-TO-BYTES with the generated builder's context-threading form (static
                // lambdas + a ref-struct state): each grantee's value/label flow as the owned UTF-8 the adapter resolved,
                // written straight into the model — no managed-string round-trip, and no closure allocated per grantee.
                var directoryState = new DirectoryGranteesState(found, context, this.access);
                Models.GranteeList grantees = Models.GranteeList.CreateBuilder(
                    workspace,
                    in directoryState,
                    Models.GranteeList.ResolvedGranteeArray.Build(in directoryState, BuildDirectoryGrantees)).RootElement;
                return SearchGranteesResult.Ok(grantees, workspace);
            }

            using ObservedIdentityPage page = await this.observed.SearchAsync(context, kind, prefix, limit, pageToken, cancellationToken).ConfigureAwait(false);
            string? nextToken = page.NextPageToken;
            Models.GranteeList.Source body = new((ref Models.GranteeList.Builder b) =>
            {
                var array = new Models.GranteeList.ResolvedGranteeArray.Source((ref Models.GranteeList.ResolvedGranteeArray.Builder ab) =>
                {
                    foreach (ObservedIdentity identity in page.Identities)
                    {
                        ab.AddItem(this.ToGrantee(identity.SubjectKindValue, identity.SubjectValueValue, identity.LabelOrNull, identity.IdentityTagsValue, identity.CompleteValue, "observed"));
                    }
                });

                if (nextToken is null)
                {
                    b.Create(grantees: array);
                }
                else
                {
                    b.Create(grantees: array, nextPageToken: nextToken);
                }
            });

            return SearchGranteesResult.Ok(body, workspace);
        }
        finally
        {
            if (prefixRented is not null)
            {
                ArrayPool<byte>.Shared.Return(prefixRented);
            }
        }
    }

    private static Models.AdministratorIdentity.Source ToIdentity(CredentialUsageGrant grant)
        => new((ref Models.AdministratorIdentity.Builder b) => b.Create(grant.Dimension, grant.Value));

    // Projects a resolved grantee — its identity described as {dimension, value} grants (never raw sys: tags). `complete`
    // is reported honestly (§17.2): the stored completeness for an observed identity, true for a directory full-resolution.
    private Models.ResolvedGrantee.Source ToGrantee(string kindToken, string value, string? label, SecurityTagSet identity, bool complete, string source)
    {
        IReadOnlyList<CredentialUsageGrant> grants = this.access.DescribeUsageScope(identity);
        return new((ref Models.ResolvedGrantee.Builder b) =>
        {
            var identityArray = new Models.ResolvedGrantee.AdministratorIdentityArray.Source((ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (CredentialUsageGrant grant in grants)
                {
                    ab.AddItem(ToIdentity(grant));
                }
            });

            if (label is null)
            {
                b.Create(complete: complete, identity: identityArray, kind: kindToken, source: source, value: value);
            }
            else
            {
                b.Create(complete: complete, identity: identityArray, kind: kindToken, source: source, value: value, label: label);
            }
        });
    }

    // ── directory grantee projection (bytes-to-bytes, context-threaded — no closures, design §16.5.4) ────────────────

    // Builds the directory grantees array: each principal admitted by the caller's read reach is projected through its own
    // item state, so the per-grantee build threads the principal's owned value/label UTF-8 (no managed string, no closure).
    private static void BuildDirectoryGrantees(in DirectoryGranteesState state, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        foreach (ResolvedPrincipal principal in state.Found)
        {
            // The directory is external (no AccessContext on its rows), so reach-filter its results here, consistently.
            if (!state.Context.Admits(AccessVerb.Read, principal.Identity))
            {
                continue;
            }

            var item = new DirectoryGranteeItemState(principal, state.Access.DescribeUsageScope(principal.Identity));
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildDirectoryGrantee));
        }
    }

    // Builds one resolved grantee: the value/label flow as the adapter-owned UTF-8 the principal holds (no string), the
    // kind/source as UTF-8 enum tokens, and the identity is described as {dimension, value} grants. `complete` is true for
    // a directory full-resolution (§17.2).
    private static void BuildDirectoryGrantee(in DirectoryGranteeItemState item, ref Models.ResolvedGrantee.Builder grantee)
    {
        grantee.Create(
            in item,
            complete: true,
            identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildDirectoryGranteeIdentity),
            kind: item.Principal.Kind.ToTokenUtf8(),
            source: "directory"u8,
            value: item.Principal.ValueMemory.Span,
            label: item.Principal.HasLabel ? (Models.JsonString.Source)item.Principal.LabelMemory.Span : default);
    }

    // Describes a grantee's identity as the {dimension, value} grants its sys: tags map to (never raw tags). The grant
    // dimension/value are the genuine response leaf (DescribeUsageScope materializes them as strings); ToIdentity writes
    // them through the WriteAction form, so the lazy item never holds a stack ref to a string→Source temporary.
    private static void BuildDirectoryGranteeIdentity(in DirectoryGranteeItemState item, ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder identities)
    {
        foreach (CredentialUsageGrant grant in item.Grants)
        {
            identities.AddItem(ToIdentity(grant));
        }
    }

    private readonly ref struct DirectoryGranteesState(IReadOnlyList<ResolvedPrincipal> found, AccessContext context, ControlPlaneAccess access)
    {
        public IReadOnlyList<ResolvedPrincipal> Found { get; } = found;

        public AccessContext Context { get; } = context;

        public ControlPlaneAccess Access { get; } = access;
    }

    private readonly ref struct DirectoryGranteeItemState(ResolvedPrincipal principal, IReadOnlyList<CredentialUsageGrant> grants)
    {
        public ResolvedPrincipal Principal { get; } = principal;

        public IReadOnlyList<CredentialUsageGrant> Grants { get; } = grants;
    }
}