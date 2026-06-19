// <copyright file="ArazzoControlPlaneIdentityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
        GranteeKind? kind = parameters.Kind.IsNotUndefined() && GranteeKinds.TryParse((string)parameters.Kind, out GranteeKind k) ? k : null;
        string prefix = parameters.Q.IsNotUndefined() ? (string)parameters.Q : string.Empty;
        string source = parameters.Source.IsNotUndefined() ? (string)parameters.Source : "observed";
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : DefaultLimit;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        // Reach-scope discovery to the caller's read reach (§17.1) — the same AccessContext idiom every other store uses.
        AccessContext context = this.access.Current();

        // The projected grantee (ResolvedGrantee.Source) is a ref struct, so it cannot be collected into a list; the
        // array is built inline from the live results, which stay in scope through Ok (it materialises the body).
        if (string.Equals(source, "directory", StringComparison.Ordinal) && this.directory is not null)
        {
            IReadOnlyList<ResolvedPrincipal> found = await this.directory.SearchAsync(kind ?? GranteeKind.Person, prefix, limit, cancellationToken).ConfigureAwait(false);
            Models.GranteeList.Source directoryBody = new((ref Models.GranteeList.Builder b) => b.Create(
                grantees: new Models.GranteeList.ResolvedGranteeArray.Source((ref Models.GranteeList.ResolvedGranteeArray.Builder ab) =>
                {
                    foreach (ResolvedPrincipal principal in found)
                    {
                        // The directory is external (no AccessContext), so reach-filter its results here, consistently.
                        if (context.Admits(AccessVerb.Read, principal.Identity))
                        {
                            ab.AddItem(this.ToGrantee(principal.Kind.ToToken(), principal.Value, principal.Label, principal.Identity, complete: true, "directory"));
                        }
                    }
                })));
            return SearchGranteesResult.Ok(directoryBody, workspace);
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
}