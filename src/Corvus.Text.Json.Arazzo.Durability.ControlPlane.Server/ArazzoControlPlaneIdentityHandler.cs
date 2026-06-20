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
    // Ownership ledger (GC-escaping allocations = the per-grant identity leaf + DescribeUsageScope lists + tracked string
    // leaves; NO builder closure, NO per-item closure, NO re-materialisation — the ~2.0 KB closure-free floor):
    //   kindToken ............. pooled UnescapedUtf8JsonString lease, disposed at end of the parse `if`.
    //   prefix/prefixRented ... ArrayPool-rented byte[], returned in the `finally`.
    //   source/pageToken/NextPageToken  string — LEAF (request params + store NextPageToken are string-typed).
    //   found + UTF8.GetString  list/string — LEAF (IPrincipalDirectory seam is string-typed: LDAP filter / HTTP URI).
    //   page .................. ObservedIdentityPage (class, IDisposable) — `using`; identities read during the one Ok pass, before dispose.
    //   state/item ........... RefTuple<…> ref structs (stack, no heap) carrying the projection/per-grantee context to the static builders.
    //   body .................. lazy GranteeList.Source<TContext> (no closure) — materialised once by Ok<TContext>, then GC.
    //   per grantee `grants` .. IReadOnlyList<CredentialUsageGrant> — LEAF (DescribeUsageScope returns string POCO grants); ToIdentity is the identity leaf.
    //   response .............. GranteeList immutable — workspace-owned (pooled arena); the host serialises it.
    // The value/label/kind bytes path materialises NO managed string (spans / enum tokens). Benchmark: GranteeProjectionBenchmarks (2.01 KB).
    public async ValueTask<SearchGranteesResult> HandleSearchGranteesAsync(SearchGranteesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        GranteeKind? kind = null;
        if (parameters.Kind.IsNotUndefined())
        {
            using UnescapedUtf8JsonString kindToken = parameters.Kind.GetUtf8String();
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
                ? parameters.Q.GetUtf8String().TakeOwnership(out prefixRented)
                : default;

            // Project the discovered principals/identities into the GranteeList body CLOSURE-FREE with a single
            // materialisation: a context-threaded GranteeList.Source<TContext> (the generated Build<TContext> + static build
            // methods, the per-projection state carried in a RefTuple — no bespoke context struct, no per-item closure)
            // handed to the generated Ok<TContext>, which runs the one CreateBuilder<TContext> pass into the response
            // workspace (no re-materialisation). value/label/kind flow BYTES-TO-BYTES (owned UTF-8 spans / enum tokens, no
            // managed string); the {dimension, value} identity grants are the genuine string leaf (DescribeUsageScope).
            // Ownership ledger above this method; proven by GranteeProjectionBenchmarks (~2.0 KB, the closure-free floor).
            if (string.Equals(source, "directory", StringComparison.Ordinal) && this.directory is not null)
            {
                // The directory query is a string leaf (the IPrincipalDirectory seam is string-typed — an LDAP filter / an
                // HTTP URI), so transcode the prefix once for it.
                IReadOnlyList<ResolvedPrincipal> found = await this.directory.SearchAsync(kind ?? GranteeKind.Person, Encoding.UTF8.GetString(prefix.Span), limit, cancellationToken).ConfigureAwait(false);

                var state = new RefTuple<IReadOnlyList<ResolvedPrincipal>, AccessContext, ControlPlaneAccess>(found, context, this.access);
                var body = Models.GranteeList.Build(
                    in state,
                    grantees: Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildDirectoryGrantees));
                return SearchGranteesResult.Ok(body, workspace);
            }

            using ObservedIdentityPage page = await this.observed.SearchAsync(context, kind, prefix, limit, pageToken, cancellationToken).ConfigureAwait(false);

            // The opaque page token is a genuine string leaf; it threads through Build's nextPageToken only when set.
            var observedState = new RefTuple<ObservedIdentityPage, ControlPlaneAccess>(page, this.access);
            var observedBody = Models.GranteeList.Build(
                in observedState,
                grantees: Models.GranteeList.ResolvedGranteeArray.Build(in observedState, BuildObservedGrantees),
                nextPageToken: page.NextPageToken is { } token ? (Models.JsonString.Source)token : default);
            return SearchGranteesResult.Ok(observedBody, workspace);
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

    // ── grantee projection (closure-free Build<TContext>; RefTuple contexts; bytes-to-bytes value/label, §16.5.4) ──────
    //
    // The per-projection state (the page/found + AccessContext + ControlPlaneAccess) and the per-item state
    // (principal/identity + the described grants) are threaded through a RefTuple<…> rather than a bespoke context struct;
    // the static build methods deconstruct what they need. No closure is captured, and Ok<TContext> materialises the body
    // in one pass — the ~2.0 KB closure-free floor proven by GranteeProjectionBenchmarks.

    // Builds the directory grantees array: each principal admitted by the caller's read reach (the directory is external,
    // so reach-filter here) is projected through its own item RefTuple — the principal plus the {dimension, value} grants
    // its sys: identity describes back to (DescribeUsageScope, the genuine string leaf).
    private static void BuildDirectoryGrantees(in RefTuple<IReadOnlyList<ResolvedPrincipal>, AccessContext, ControlPlaneAccess> state, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        (IReadOnlyList<ResolvedPrincipal> found, AccessContext context, ControlPlaneAccess access) = state;
        foreach (ResolvedPrincipal principal in found)
        {
            if (!context.Admits(AccessVerb.Read, principal.Identity))
            {
                continue;
            }

            var item = new RefTuple<ResolvedPrincipal, IReadOnlyList<CredentialUsageGrant>>(principal, access.DescribeUsageScope(principal.Identity));
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildDirectoryGrantee));
        }
    }

    // Builds one directory grantee: the value/label flow as the adapter-owned UTF-8 the principal holds (no managed string),
    // the kind/source as UTF-8 enum tokens. `complete` is true for a directory full-resolution (§17.2).
    private static void BuildDirectoryGrantee(in RefTuple<ResolvedPrincipal, IReadOnlyList<CredentialUsageGrant>> item, ref Models.ResolvedGrantee.Builder grantee)
    {
        ResolvedPrincipal principal = item.Item1;
        if (principal.HasLabel)
        {
            grantee.Create(in item, complete: true, identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildGranteeIdentity), kind: principal.Kind.ToTokenUtf8(), source: "directory"u8, value: principal.ValueMemory.Span, label: (Models.JsonString.Source)principal.LabelMemory.Span);
        }
        else
        {
            grantee.Create(in item, complete: true, identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildGranteeIdentity), kind: principal.Kind.ToTokenUtf8(), source: "directory"u8, value: principal.ValueMemory.Span);
        }
    }

    // Builds the observed grantees array: the store already reach-filtered the page, so each identity is projected directly.
    private static void BuildObservedGrantees(in RefTuple<ObservedIdentityPage, ControlPlaneAccess> state, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        (ObservedIdentityPage page, ControlPlaneAccess access) = state;
        foreach (ObservedIdentity identity in page.Identities)
        {
            var item = new RefTuple<ObservedIdentity, IReadOnlyList<CredentialUsageGrant>>(identity, access.DescribeUsageScope(identity.IdentityTagsValue));
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildObservedGrantee));
        }
    }

    // Builds one observed grantee: subjectValue/subjectKind/label flow as UTF-8 straight off the stored CTJ model (its
    // generated JsonString/enum exposes GetUtf8String() directly — a pooled lease, no managed string), bridged to the
    // response model as spans. `complete` is the stored honesty flag (§17.2). The leases live until Create copies the bytes.
    private static void BuildObservedGrantee(in RefTuple<ObservedIdentity, IReadOnlyList<CredentialUsageGrant>> item, ref Models.ResolvedGrantee.Builder grantee)
    {
        ObservedIdentity identity = item.Item1;
        using UnescapedUtf8JsonString kind = identity.SubjectKind.GetUtf8String();
        using UnescapedUtf8JsonString value = identity.SubjectValue.GetUtf8String();
        if (identity.Label.IsNotUndefined())
        {
            using UnescapedUtf8JsonString label = identity.Label.GetUtf8String();
            grantee.Create(in item, complete: identity.CompleteValue, identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildGranteeIdentity), kind: kind.Span, source: "observed"u8, value: value.Span, label: label.Span);
        }
        else
        {
            grantee.Create(in item, complete: identity.CompleteValue, identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildGranteeIdentity), kind: kind.Span, source: "observed"u8, value: value.Span);
        }
    }

    // The grantee's identity, described as the {dimension, value} grants its sys: tags map to (never raw tags) — the genuine
    // string leaf; ToIdentity writes each grant through the WriteAction form. The item RefTuple's second element is the grant
    // list; the first (TIdentity) is whatever the grantee branch threads (a ResolvedPrincipal or an ObservedIdentity) — the
    // identity sub-array's context must match its grantee's context, so it is generic over that first element.
    private static void BuildGranteeIdentity<TIdentity>(in RefTuple<TIdentity, IReadOnlyList<CredentialUsageGrant>> item, ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder identities)
        where TIdentity : allows ref struct
    {
        foreach (CredentialUsageGrant grant in item.Item2)
        {
            identities.AddItem(ToIdentity(grant));
        }
    }
}