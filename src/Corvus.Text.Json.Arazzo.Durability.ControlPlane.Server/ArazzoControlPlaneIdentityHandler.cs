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
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // The kinds a principal directory can search (people/teams/roles — the IPrincipalDirectory vocabulary); an
    // unrestricted directory or merged search queries each. Workflow is a control-plane concept, never directory-resolved.
    private static readonly GranteeKind[] DirectorySearchableKinds = [GranteeKind.Person, GranteeKind.Team, GranteeKind.Role];

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
        // Built closure-free AND allocation-free for the usage-scope projection: the caller's internal identity tag set is
        // enumerated as unescaped spans and each prefixed tag described back to a grant bytes-native (no List, no per-grant
        // strings). The body is consumed in place (WhoAmI.Build is ref-scoped to its `in` argument).
        SecurityTagSet identity = SecurityTagSet.FromTags(this.access.InternalTags());
        var ctx = new WhoAmIContext(identity, this.access);
        Models.WhoAmI.Source<WhoAmIContext> body = Models.WhoAmI.Build(
            in ctx,
            identity: Models.WhoAmI.AdministratorIdentityArray.Build(in ctx, BuildWhoAmIIdentity));
        return new ValueTask<GetWhoamiResult>(GetWhoamiResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<GetIdentityCapabilitiesResult> HandleGetIdentityCapabilitiesAsync(GetIdentityCapabilitiesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GranteeKind> kinds = this.access.SupportedGranteeKinds();
        bool hasDirectory = this.directory is not null;

        // Built closure-free (the supported-kinds list threaded as the context through the static BuildGranteeKinds) and
        // consumed in place; directorySearch is a scalar passed straight to the property-parameter Build.
        Models.IdentityCapabilities.Source<IReadOnlyList<GranteeKind>> body = Models.IdentityCapabilities.Build(
            in kinds,
            directorySearch: hasDirectory,
            granteeKinds: Models.IdentityCapabilities.GranteeKindArray.Build(in kinds, BuildGranteeKinds));
        return new ValueTask<GetIdentityCapabilitiesResult>(GetIdentityCapabilitiesResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    // Ownership ledger (GC-escaping allocations = the per-grant identity leaf + DescribeUsageScope lists + tracked string
    // leaves; NO builder closure, NO per-item closure, NO re-materialisation — the ~2.0 KB closure-free floor):
    //   kindToken ............. pooled UnescapedUtf8JsonString lease, disposed at end of the parse `if`.
    //   prefix ................ the request Q as its JSON value (From(), no reify, no rental) — passed to the store; reified only at the directory string leaf.
    //   pageToken ............. the request PageToken as its JSON value (From(), no reify, no string) — store decodes the cursor bytes-native.
    //   NextPageToken ......... the store's pooled UTF-8 in the page (no token string); written straight into the response body, freed on page dispose.
    //   source ................ string — LEAF (request param).
    //   found + (string)prefix  list/string — LEAF (IPrincipalDirectory seam is string-typed: LDAP filter / HTTP URI).
    //   page .................. ObservedIdentityPage (class, IDisposable) — `using`; identities read during the one Ok pass, before dispose.
    //   state/item ........... RefTuple<…> ref structs (stack, no heap) carrying the projection/per-grantee context to the static builders.
    //   body .................. lazy GranteeList.Source<TContext> (no closure) — materialised once by Ok<TContext>, then GC.
    //   per grantee `grants` .. IReadOnlyList<CredentialUsageGrant> — LEAF (DescribeUsageScope returns string POCO grants); ToIdentity is the identity leaf.
    //   response .............. GranteeList immutable — workspace-owned (pooled arena); the host serialises it.
    // The value/label/kind bytes path materialises NO managed string (spans / enum tokens). Benchmark: GranteeProjectionBenchmarks (2.01 KB).
    public async ValueTask<SearchGranteesResult> HandleSearchGranteesAsync(SearchGranteesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The query's grantee kind IS a JSON value; convert it to the store's kind with a straight From() (free rewrap,
        // no reify, no token re-parse) — the observed store carries it through. An absent kind is undefined (= all kinds).
        ObservedIdentity.GranteeKind kind = ObservedIdentity.GranteeKind.From(parameters.Kind);

        // An absent source defaults to `merged` when a directory is configured (a real person and a store-seen identity
        // are both findable without the caller knowing which source holds them), `observed` otherwise.
        string source = parameters.Source.IsNotUndefined()
            ? (string)parameters.Source
            : this.directory is not null ? "merged" : "observed";
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : DefaultLimit;

        // The opaque page token flows to the observed store as its JSON value (From() rewraps parameters.PageToken — free,
        // no reify, no managed string; an undefined token rewraps to an undefined JsonString the store reads as "first
        // page"); the store decodes it bytes-native from the request UTF-8.
        JsonString pageToken = JsonString.From(parameters.PageToken);

        // Reach-scope discovery to the caller's read reach (§17.1) — the same AccessContext idiom every other store uses.
        AccessContext context = this.access.Current();

        // The search prefix flows to the observed store as its JSON value (From() rewraps parameters.Q, no reify, no
        // managed string; an undefined Q rewraps to an undefined JsonString); the backend reifies it only at its index
        // leaf. Undefined Q means "match all".
        JsonString prefix = JsonString.From(parameters.Q);

        // Project the discovered principals/identities into the GranteeList body CLOSURE-FREE with a single
        // materialisation: a context-threaded GranteeList.Source<TContext> (the generated Build<TContext> + static build
        // methods, the per-projection state carried in a RefTuple — no bespoke context struct, no per-item closure)
        // handed to the generated Ok<TContext>, which runs the one CreateBuilder<TContext> pass into the response
        // workspace (no re-materialisation). value/label/kind flow BYTES-TO-BYTES (owned UTF-8 spans / enum tokens, no
        // managed string); the {dimension, value} identity grants are the genuine string leaf (DescribeUsageScope).
        // Ownership ledger above this method; proven by GranteeProjectionBenchmarks (~2.0 KB, the closure-free floor).
        if (string.Equals(source, "directory", StringComparison.Ordinal) && this.directory is not null)
        {
            // An explicit directory search surfaces a directory failure (the caller asked for THAT source): the shared
            // adapter failure type maps to a 502 problem instead of an unhandled 500.
            IReadOnlyList<ResolvedPrincipal> found;
            try
            {
                found = await this.SearchDirectoryAsync(kind, prefix, limit, cancellationToken).ConfigureAwait(false);
            }
            catch (PrincipalDirectoryException)
            {
                return SearchGranteesResult.BadGateway(
                    Problem("directory-unavailable", "Directory unavailable", 502, "The external principal directory could not be reached."), workspace);
            }

            var state = new RefTuple<IReadOnlyList<ResolvedPrincipal>, AccessContext, ControlPlaneAccess>(found, context, this.access);
            var body = Models.GranteeList.Build(
                in state,
                grantees: Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildDirectoryGrantees));
            return SearchGranteesResult.Ok(body, workspace);
        }

        if (string.Equals(source, "merged", StringComparison.Ordinal) && this.directory is not null)
        {
            // The merged view: directory results (reach-filtered) ahead of the observed identities that do not collide
            // with one on (kind, value) — directory-preferred, since a directory resolution is complete (§17.2). The
            // directory leg is best-effort enrichment here, so its failure degrades to observed results rather than
            // failing the search. One bounded result set: no page token (paging a union of a paged store and an
            // unpaged directory would re-emit the directory head every page).
            IReadOnlyList<ResolvedPrincipal> merged;
            try
            {
                merged = await this.SearchDirectoryAsync(kind, prefix, limit, cancellationToken).ConfigureAwait(false);
            }
            catch (PrincipalDirectoryException)
            {
                merged = [];
            }

            using ObservedIdentityPage observedPage = await this.observed.SearchAsync(context, kind, prefix, limit, pageToken, cancellationToken).ConfigureAwait(false);
            var mergedState = new MergedGranteesState(merged, observedPage, context, this.access);
            var mergedBody = Models.GranteeList.Build(
                in mergedState,
                grantees: Models.GranteeList.ResolvedGranteeArray.Build(in mergedState, BuildMergedGrantees));
            return SearchGranteesResult.Ok(mergedBody, workspace);
        }

        using ObservedIdentityPage page = await this.observed.SearchAsync(context, kind, prefix, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The opaque page token is the store's pooled UTF-8 (alive until the page is disposed, after the synchronous Ok
        // build); write it straight into the response body — no managed token string. Empty means "last page".
        var observedState = new RefTuple<ObservedIdentityPage, ControlPlaneAccess>(page, this.access);
        var observedBody = Models.GranteeList.Build(
            in observedState,
            grantees: Models.GranteeList.ResolvedGranteeArray.Build(in observedState, BuildObservedGrantees),
            nextPageToken: page.NextPageToken.IsEmpty ? default : (Models.JsonString.Source)page.NextPageToken.Span);
        return SearchGranteesResult.Ok(observedBody, workspace);
    }

    // Searches the directory: one call when the request pins a kind (the seam is per-kind), otherwise one call per
    // searchable kind (person/team/role), concatenated. The seam is domain-typed (the GranteeKind enum) and
    // string-typed (an LDAP filter / an HTTP URI), so the store kind and the prefix reify at this genuine leaf.
    // In the all-kinds sweep a kind whose backing resource fails (e.g. a directory service account not permitted to
    // list roles) contributes nothing rather than failing the kinds that DID resolve; the directory counts as
    // unreachable — the propagated failure — only when every kind fails.
    private async ValueTask<IReadOnlyList<ResolvedPrincipal>> SearchDirectoryAsync(ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, CancellationToken cancellationToken)
    {
        string query = prefix.IsNotUndefined() ? (string)prefix : string.Empty;
        if (kind.IsNotUndefined())
        {
            return await this.directory!.SearchAsync(kind.ToGranteeKind(), query, limit, cancellationToken).ConfigureAwait(false);
        }

        List<ResolvedPrincipal>? all = null;
        PrincipalDirectoryException? failure = null;
        bool anySucceeded = false;
        foreach (GranteeKind searchable in DirectorySearchableKinds)
        {
            IReadOnlyList<ResolvedPrincipal> found;
            try
            {
                found = await this.directory!.SearchAsync(searchable, query, limit, cancellationToken).ConfigureAwait(false);
            }
            catch (PrincipalDirectoryException exception)
            {
                failure = exception;
                continue;
            }

            anySucceeded = true;
            if (found.Count > 0)
            {
                (all ??= []).AddRange(found);
            }
        }

        if (!anySucceeded && failure is not null)
        {
            throw failure;
        }

        return (IReadOnlyList<ResolvedPrincipal>?)all ?? [];
    }

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    // A described usage grant written bytes-native from its (dimension, value) spans — shared by the whoami and grantee
    // identity sub-arrays (both hold AdministratorIdentity items). The spans are valid for the synchronous build.
    private static void BuildIdentityGrant(in UsageGrantSpans spans, ref Models.AdministratorIdentity.Builder b)
        => b.Create((Models.JsonString.Source)spans.Dimension, (Models.JsonString.Source)spans.Value);

    private readonly ref struct WhoAmIContext(SecurityTagSet identity, ControlPlaneAccess access)
    {
        public SecurityTagSet Identity { get; } = identity;

        public ControlPlaneAccess Access { get; } = access;
    }

    private static void BuildWhoAmIIdentity(in WhoAmIContext ctx, ref Models.WhoAmI.AdministratorIdentityArray.Builder array)
    {
        ControlPlaneAccess access = ctx.Access;
        SecurityTagSet.Utf8Enumerator e = ctx.Identity.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (access.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    var spans = new UsageGrantSpans(dimension, e.CurrentValue);
                    array.AddItem(Models.AdministratorIdentity.Build(in spans, BuildIdentityGrant));
                }
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildGranteeKinds(in IReadOnlyList<GranteeKind> kinds, ref Models.IdentityCapabilities.GranteeKindArray.Builder array)
    {
        foreach (GranteeKind kind in kinds)
        {
            array.AddItem(kind.ToToken());
        }
    }

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

            var item = new RefTuple<ResolvedPrincipal, SecurityTagSet, ControlPlaneAccess>(principal, principal.Identity, access);
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildDirectoryGrantee));
        }
    }

    // Builds one directory grantee: the value/label flow as the adapter-owned UTF-8 the principal holds (no managed string),
    // the kind/source as UTF-8 enum tokens. `complete` is true for a directory full-resolution (§17.2).
    private static void BuildDirectoryGrantee(in RefTuple<ResolvedPrincipal, SecurityTagSet, ControlPlaneAccess> item, ref Models.ResolvedGrantee.Builder grantee)
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

    // The merged view's projection state: the directory results, the observed page, and the caller's reach + access facade.
    private readonly ref struct MergedGranteesState(IReadOnlyList<ResolvedPrincipal> found, ObservedIdentityPage page, AccessContext context, ControlPlaneAccess access)
    {
        public IReadOnlyList<ResolvedPrincipal> Found { get; } = found;

        public ObservedIdentityPage Page { get; } = page;

        public AccessContext Context { get; } = context;

        public ControlPlaneAccess Access { get; } = access;
    }

    // Builds the merged grantees array: directory results first (reach-filtered here — the directory is external),
    // then the observed identities that do not collide with an ADMITTED directory result on (kind, value) —
    // directory-preferred, compared bytes-native off the stored CTJ strings against the principal's owned UTF-8.
    private static void BuildMergedGrantees(in MergedGranteesState state, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        foreach (ResolvedPrincipal principal in state.Found)
        {
            if (!state.Context.Admits(AccessVerb.Read, principal.Identity))
            {
                continue;
            }

            var item = new RefTuple<ResolvedPrincipal, SecurityTagSet, ControlPlaneAccess>(principal, principal.Identity, state.Access);
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildDirectoryGrantee));
        }

        foreach (ObservedIdentity identity in state.Page.Identities)
        {
            if (CollidesWithDirectory(identity, state.Found, state.Context))
            {
                continue;
            }

            var item = new RefTuple<ObservedIdentity, SecurityTagSet, ControlPlaneAccess>(identity, identity.IdentityTagsValue, state.Access);
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildObservedGrantee));
        }
    }

    // Whether an observed identity names the same (kind, value) as a directory result the caller can see. A directory
    // result the caller's reach filtered OUT must not suppress the observed entry (that would leak that a wider
    // principal exists), so only admitted principals collide.
    private static bool CollidesWithDirectory(ObservedIdentity identity, IReadOnlyList<ResolvedPrincipal> found, AccessContext context)
    {
        foreach (ResolvedPrincipal principal in found)
        {
            if (context.Admits(AccessVerb.Read, principal.Identity)
                && identity.SubjectKind.ValueEquals(principal.Kind.ToTokenUtf8())
                && identity.SubjectValue.ValueEquals(principal.ValueMemory.Span))
            {
                return true;
            }
        }

        return false;
    }

    // Builds the observed grantees array: the store already reach-filtered the page, so each identity is projected directly.
    private static void BuildObservedGrantees(in RefTuple<ObservedIdentityPage, ControlPlaneAccess> state, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        (ObservedIdentityPage page, ControlPlaneAccess access) = state;
        foreach (ObservedIdentity identity in page.Identities)
        {
            var item = new RefTuple<ObservedIdentity, SecurityTagSet, ControlPlaneAccess>(identity, identity.IdentityTagsValue, access);
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildObservedGrantee));
        }
    }

    // Builds one observed grantee: subjectValue/subjectKind/label flow as UTF-8 straight off the stored CTJ model (its
    // generated JsonString/enum exposes GetUtf8String() directly — a pooled lease, no managed string), bridged to the
    // response model as spans. `complete` is the stored honesty flag (§17.2). The leases live until Create copies the bytes.
    private static void BuildObservedGrantee(in RefTuple<ObservedIdentity, SecurityTagSet, ControlPlaneAccess> item, ref Models.ResolvedGrantee.Builder grantee)
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

    // The grantee's identity, described as the {dimension, value} grants its sys: tags map to (never raw tags), projected
    // allocation-free: the item's second element is the identity's SecurityTagSet, enumerated as unescaped spans and each
    // prefixed tag described back bytes-native via the third element (the access facade) — no List, no per-grant strings.
    // The first element (TIdentity) is whatever the grantee branch threads (a ResolvedPrincipal or an ObservedIdentity);
    // the identity sub-array's context matches its grantee's, so it is generic over that first element.
    private static void BuildGranteeIdentity<TIdentity>(in RefTuple<TIdentity, SecurityTagSet, ControlPlaneAccess> item, ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder identities)
        where TIdentity : allows ref struct
    {
        ControlPlaneAccess access = item.Item3;
        SecurityTagSet.Utf8Enumerator e = item.Item2.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (access.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    var spans = new UsageGrantSpans(dimension, e.CurrentValue);
                    identities.AddItem(Models.AdministratorIdentity.Build(in spans, BuildIdentityGrant));
                }
            }
        }
        finally
        {
            e.Dispose();
        }
    }
}