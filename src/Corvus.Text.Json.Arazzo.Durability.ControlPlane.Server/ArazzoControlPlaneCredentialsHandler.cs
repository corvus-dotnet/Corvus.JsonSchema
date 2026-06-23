// <copyright file="ArazzoControlPlaneCredentialsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiCredentialsHandler"/> over an <see cref="ISourceCredentialStore"/> — the
/// control-plane surface that manages source credential bindings (design §13). The endpoints are gated by the
/// <c>credentials:read</c>/<c>credentials:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Trust boundary (§13).</strong> This handler manages <em>references</em> only. It holds no
/// <see cref="ISecretResolver"/> and never resolves, returns, or logs secret material; request and response bodies
/// carry a <see cref="SecretRef"/> plus non-secret metadata. A value that is not a well-formed <see cref="SecretRef"/>
/// is rejected at the boundary (400) by the same <see cref="SourceCredentialBinding.ValidateDraft"/> the store
/// uses, so secret material cannot be smuggled inline. Rotation is by changing the reference.</para>
/// <para>The (sourceName, environment) pair is the binding key; create conflicts (409) if one already exists, and
/// update/delete on a missing binding is a 404. Identity and created-* audit fields are immutable across updates.</para>
/// </remarks>
public sealed class ArazzoControlPlaneCredentialsHandler : IApiCredentialsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private static readonly TimeSpan DefaultExpiringWindow = TimeSpan.FromDays(7);

    private readonly ISourceCredentialStore store;
    private readonly ControlPlaneAccess access;
    private readonly string actor;
    private readonly TimeProvider timeProvider;
    private readonly TimeSpan expiringWindow;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/> — no
    /// row security).</summary>
    /// <param name="store">The persistent source credential store the endpoints delegate to.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneCredentialsHandler(ISourceCredentialStore store, string actor = "control-plane")
        : this(store, new ControlPlaneAccess(), actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneCredentialsHandler"/> class.</summary>
    /// <param name="store">The persistent source credential store the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request and the internal tenant tags
    /// stamped onto created bindings (§14.2). Unscoped (<see cref="AccessContext.System"/>) when no row security is configured.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    /// <param name="timeProvider">The clock used to derive each binding's <see cref="CredentialStatus"/> on read
    /// (defaults to <see cref="TimeProvider.System"/>).</param>
    /// <param name="expiringWindow">How far ahead of expiry a still-valid credential is reported as
    /// <see cref="CredentialStatus.ExpiringSoon"/> (defaults to 7 days).</param>
    internal ArazzoControlPlaneCredentialsHandler(ISourceCredentialStore store, ControlPlaneAccess access, string actor = "control-plane", TimeProvider? timeProvider = null, TimeSpan? expiringWindow = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.actor = actor;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.expiringWindow = expiringWindow ?? DefaultExpiringWindow;
    }

    /// <inheritdoc/>
    public async ValueTask<ListCredentialsResult> HandleListCredentialsAsync(ListCredentialsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // reify, no managed string; an undefined token rewraps to an undefined JsonString); the store decodes it
        // bytes-native from the request UTF-8.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using SourceCredentialPage page = await this.store.ListAsync(this.access.Current(), limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Each summary references its pooled binding document (the per-field From() projection is a zero-copy element
        // wrap), and the body is validated/serialized after this handler returns — so hand the documents to the
        // workspace (it disposes them at request end); `using page` then only returns the batch's backing array.
        page.Bindings.TransferOwnershipTo(workspace);

        // The list body is built closure-free and consumed in place (CredentialBindingList.Build scopes its result to
        // the `in credentials` argument, so it cannot be returned from a helper — see the BuildSummaries notes).
        var listContext = new ListContext(page.Bindings, this.access, this.timeProvider, this.expiringWindow);
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.CredentialBindingList.Source<ListContext> body = Models.CredentialBindingList.Build(
            in listContext,
            credentials: Models.CredentialBindingList.CredentialBindingSummaryArray.Build(in listContext, BuildSummaries),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListCredentialsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateCredentialResult> HandleCreateCredentialAsync(CreateCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.CredentialBindingWrite body = parameters.Body;
        ManagementTagsState managementState;
        bool hasManagementTags;
        UsageTagsState usageState;
        bool hasUsageTags;
        try
        {
            // managementTags = the principal's deployment-internal tenant tag (always stamped, so the owner keeps
            // management) PLUS any operator-supplied management labels. The user tags are validated against the reserved
            // prefix string-free (a non-owning SecurityTagSet view over the request body's array — no ReadTags list). The
            // tags are NOT materialized here: they are written straight into the draft below (the internal tags
            // string-sourced from the policy + the user tags' UTF-8 spans), and the Admits write-reach check reads them
            // back from the draft as a non-owning view — so the draft document is the only materialization. InternalTags()
            // is resolved once and shared with usage below.
            IReadOnlyList<SecurityTag> internalTags = this.access.InternalTags();
            SecurityTagSet userManagement = body.ManagementTags.IsNotUndefined()
                ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(body.ManagementTags).Memory)
                : SecurityTagSet.Empty;
            this.access.ValidateUserTags(userManagement);
            managementState = new ManagementTagsState(internalTags, userManagement);
            hasManagementTags = internalTags.Count > 0 || !userManagement.IsEmpty;

            // usageTags are written straight into the draft (no intermediate SecurityTagSet — the draft document is the
            // leaf): the operator's usage GRANTS resolved BYTES-TO-BYTES to UNFORGEABLE internal tags (e.g.
            // sys:workflow=nightly-reconcile; never free-form, so usage cannot be self-granted by a workflow author), or —
            // with no grants — the creating principal's own identity (its internal tags). The two scopes are independent
            // (§13/§14.2). Empty only when unscoped (no grants and no internal tags), in which case the property is omitted.
            bool useGrants = body.UsageGrants.IsNotUndefined() && body.UsageGrants.GetArrayLength() > 0;
            usageState = new UsageTagsState(this.access, body.UsageGrants, internalTags, useGrants);
            hasUsageTags = useGrants || internalTags.Count > 0;

            // The required request fields are validated up front; their JSON values are then carried into the draft
            // bytes-to-bytes (no per-field strings, no list) — see the draft build below.
            if (!body.SourceName.IsNotUndefined())
            {
                throw new ArgumentException("A 'sourceName' is required.");
            }

            if (!body.Environment.IsNotUndefined())
            {
                throw new ArgumentException("An 'environment' is required.");
            }

            _ = ReadAuthKind(body.AuthKind);
        }
        catch (ArgumentException ex)
        {
            return CreateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }

        try
        {
            // The persisted binding carries the request body's reference/metadata JSON values bytes-to-bytes (no
            // per-field strings, no list) plus the resolved management/usage tags written straight into the draft (no tag
            // SecurityTagSet materialization); the store stamps id/createdBy/createdAt/etag.
            using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(
                sourceName: (JsonElement)body.SourceName,
                environment: (JsonElement)body.Environment,
                authKind: (JsonElement)body.AuthKind,
                secretRefs: (JsonElement)body.SecretRefs,
                config: (JsonElement)body.Config,
                description: (JsonElement)body.Description,
                expiresAt: (JsonElement)body.ExpiresAt,
                rotatedAt: (JsonElement)body.RotatedAt,
                managementState: managementState,
                hasManagementTags: hasManagementTags,
                writeManagementTags: WriteManagementTags,
                usageState: usageState,
                hasUsageTags: hasUsageTags,
                writeUsageTags: WriteUsageTags);

            // Guard against privilege escalation: a principal may not create a binding it could not itself manage. The
            // binding's management tags are read back from the draft as a non-owning view (the draft is the only
            // materialization — no separate SecurityTagSet) and checked against the caller's write reach.
            SecurityTagSet managementTags = hasManagementTags
                ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(draft.RootElement.ManagementTags).Memory)
                : SecurityTagSet.Empty;
            if (!this.access.Current().Admits(AccessVerb.Write, managementTags))
            {
                return CreateCredentialResult.BadRequest(
                    Problem("management-out-of-reach", "Management scope out of reach", 400, "The binding's management tags are outside your own management reach."), workspace);
            }

            // The summary references the pooled binding document (per-field From() zero-copy wrap), so hand it to the
            // workspace — it disposes it after the response is written — rather than `using`-disposing at method exit
            // (the body is validated/serialized after this returns). The draft is the input only, so it stays scoped.
            ParsedJsonDocument<SourceCredentialBinding> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);
            workspace.TakeOwnership(created);
            return CreateCredentialResult.Created(ToSummary(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }
        catch (InvalidOperationException ex)
        {
            return CreateCredentialResult.Conflict(Problem("credential-exists", "Credential already exists", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetCredentialResult> HandleGetCredentialAsync(GetCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        ParsedJsonDocument<SourceCredentialBinding>? binding = await this.store.GetAsync(sourceName, environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (binding is not { } b)
        {
            return GetCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
        }

        // The summary references the pooled binding document (per-field From() zero-copy wrap) — hand it to the
        // workspace so the deferred body validation/serialization is safe (it disposes the document afterwards).
        workspace.TakeOwnership(b);
        return GetCredentialResult.Ok(ToSummary(b.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateCredentialResult> HandleUpdateCredentialAsync(UpdateCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        Models.CredentialBindingUpdate body = parameters.Body;
        try
        {
            _ = ReadAuthKind(body.AuthKind);
        }
        catch (ArgumentException ex)
        {
            return UpdateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }

        try
        {
            // Only the mutable content is carried bytes-to-bytes; the immutable identity (sourceName, environment) and the
            // security tags are carried forward from the stored binding by the store, so the draft omits them.
            using ParsedJsonDocument<SourceCredentialBinding> draft = SourceCredentialBinding.Draft(
                sourceName: default,
                environment: default,
                authKind: (JsonElement)body.AuthKind,
                secretRefs: (JsonElement)body.SecretRefs,
                config: (JsonElement)body.Config,
                description: (JsonElement)body.Description,
                expiresAt: (JsonElement)body.ExpiresAt,
                rotatedAt: (JsonElement)body.RotatedAt,
                managementTags: default,
                usageTags: default);
            ParsedJsonDocument<SourceCredentialBinding>? updated = await this.store.UpdateAsync(sourceName, environment, draft.RootElement, WorkflowEtag.None, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (updated is not { } b)
            {
                return UpdateCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
            }

            // The summary references the pooled binding document (per-field From() zero-copy wrap) — hand it to the
            // workspace so the deferred body validation/serialization is safe (it disposes the document afterwards).
            workspace.TakeOwnership(b);
            return UpdateCredentialResult.Ok(ToSummary(b.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return UpdateCredentialResult.BadRequest(Problem("invalid-credential", "Invalid credential binding", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteCredentialResult> HandleDeleteCredentialAsync(DeleteCredentialParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string sourceName = (string)parameters.SourceName;
        string environment = (string)parameters.Environment;
        bool deleted = await this.store.DeleteAsync(sourceName, environment, WorkflowEtag.None, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return deleted
            ? DeleteCredentialResult.NoContent()
            : DeleteCredentialResult.NotFound(NotFoundProblem(sourceName, environment), workspace);
    }

    private static SourceCredentialKind ReadAuthKind(Models.SourceCredentialKind authKind)
        => authKind.IsNotUndefined()
            ? SourceCredentialKindExtensions.Parse((string)authKind)
            : throw new ArgumentException("An 'authKind' is required.");

    // Writes the binding's management tags straight into the pooled buffer (the bytes-to-bytes write leaf, mirroring the
    // usage-grant BuildUsageGrants below): the deployment-internal tags first (string-sourced from the policy — the short
    // key is encoded to a stack buffer, the value written via the string overload), then the operator's user tags as
    // unescaped UTF-8 spans straight off the request body. No managed-string tag list, no merged List.
    private static void WriteManagementTags(ref IdentityBuilder builder, in ManagementTagsState state)
    {
        WriteInternalTags(ref builder, state.InternalTags);

        SecurityTagSet.Utf8Enumerator e = state.UserTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                builder.Add(e.CurrentKey, e.CurrentValue);
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private readonly struct ManagementTagsState(IReadOnlyList<SecurityTag> internalTags, SecurityTagSet userTags)
    {
        public IReadOnlyList<SecurityTag> InternalTags { get; } = internalTags;

        public SecurityTagSet UserTags { get; } = userTags;
    }

    // Writes the binding's usage-identity tags straight into the draft's usage array (no intermediate SecurityTagSet — the
    // draft document is the leaf): the operator's usage grants resolved bytes-to-bytes to the deployment's UNFORGEABLE
    // internal tags (ResolveUsageGrantInto, the span counterpart of ResolveUsageGrants — a deployment that remaps grants
    // overrides it), or — with no grants — the creating principal's own internal tags.
    private static void WriteUsageTags(ref IdentityBuilder builder, in UsageTagsState state)
    {
        if (state.UseGrants)
        {
            foreach (Models.CredentialUsageGrant grant in state.Grants.EnumerateArray())
            {
                using UnescapedUtf8JsonString dimension = grant.DimensionValue.GetUtf8String();
                using UnescapedUtf8JsonString value = grant.Value.GetUtf8String();
                state.Access.ResolveUsageGrantInto(dimension.Span, value.Span, ref builder);
            }
        }
        else
        {
            WriteInternalTags(ref builder, state.InternalTags);
        }
    }

    private readonly struct UsageTagsState(ControlPlaneAccess access, Models.CredentialBindingWrite.CredentialUsageGrantArray grants, IReadOnlyList<SecurityTag> internalTags, bool useGrants)
    {
        public ControlPlaneAccess Access { get; } = access;

        public Models.CredentialBindingWrite.CredentialUsageGrantArray Grants { get; } = grants;

        public IReadOnlyList<SecurityTag> InternalTags { get; } = internalTags;

        public bool UseGrants { get; } = useGrants;
    }

    // Writes deployment-internal tags (string-sourced from the policy) verbatim into the buffer: the short key is encoded
    // to a stack buffer and the value written via the string overload — no per-tag managed string beyond the policy's own.
    // Shared by the management write (internal + user tags) and the no-grants usage write (the principal's own identity).
    private static void WriteInternalTags(ref IdentityBuilder builder, IReadOnlyList<SecurityTag> internalTags)
    {
        Span<byte> keyBuffer = stackalloc byte[256];
        foreach (SecurityTag tag in internalTags)
        {
            if (Encoding.UTF8.GetMaxByteCount(tag.Key.Length) <= keyBuffer.Length)
            {
                int written = Encoding.UTF8.GetBytes(tag.Key, keyBuffer);
                builder.Add(keyBuffer[..written], tag.Value);
            }
            else
            {
                builder.Add(Encoding.UTF8.GetBytes(tag.Key), tag.Value);
            }
        }
    }

    // ── credential-summary projection (closure-free Build<TContext>; RefTuple contexts; §13) ────────────────────────────
    //
    // The summary is NOT congruent with the stored binding — it requires a derived credentialStatus (computed from
    // expiresAt vs now, §13.2; never persisted) and exposes operator-facing usageGrants in place of the internal
    // usageTags (the raw tags are deliberately hidden), so From() cannot be used and the fields are projected. The two
    // non-congruent transforms (status + describedGrants) need `this` (the clock / the row-security policy), so they are
    // computed up front in the instance helpers below and threaded — together with the binding — through a RefTuple to
    // the static build methods. No closure is captured (the outer summary closure + the four nested-array closures +
    // the per-item closures the old shape allocated are all gone); the per-field From() value bridges (FIX #1) are
    // preserved unchanged — only the closures are removed. Proven by CredentialSummaryProjectionBenchmarks.

    // The per-binding summary context: the stored binding, its derived status, and the row-security access (the usage
    // scope is described lazily and allocation-free in BuildUsageGrants, straight off the binding's UsageTags).
    private readonly ref struct SummaryContext(SourceCredentialBinding binding, CredentialStatus status, ControlPlaneAccess access)
    {
        public SourceCredentialBinding Binding { get; } = binding;

        public CredentialStatus Status { get; } = status;

        public ControlPlaneAccess Access { get; } = access;
    }

    // Computes the derived credentialStatus (from the clock, §13.2) and threads the binding + the row-security access
    // through a SummaryContext to the closure-free BuildSummary. The usage scope (the inverse of the create mapping —
    // internal tags are never exposed raw) is projected lazily and allocation-free inside BuildUsageGrants. The returned
    // Source<SummaryContext> holds a copy of the context (the builder ctor takes `scoped in`, so it does not escape by ref).
    private Models.CredentialBindingSummary.Source<SummaryContext> ToSummary(SourceCredentialBinding binding)
    {
        CredentialStatus status = binding.DeriveStatus(this.timeProvider.GetUtcNow(), this.expiringWindow);
        var ctx = new SummaryContext(binding, status, this.access);
        return Models.CredentialBindingSummary.Build(in ctx, BuildSummary);
    }

    private static void BuildSummary(in SummaryContext ctx, ref Models.CredentialBindingSummary.Builder b)
    {
        SourceCredentialBinding binding = ctx.Binding;

        bool hasConfig = binding.Config.IsNotUndefined() && binding.Config.GetArrayLength() > 0;

        // A cheap array-length check — the old !binding.ManagementTagsValue.IsEmpty allocated a CopyFrom byte[] per row
        // just to test emptiness.
        bool hasManagementTags = binding.ManagementTags.IsNotUndefined() && binding.ManagementTags.GetArrayLength() > 0;

        // Stored usage tags always carry the internal prefix (they are written via ResolveUsageGrants), so a non-empty
        // usageTags array always describes back to at least one grant — the cheap length check matches the old
        // DescribedGrants.Count > 0 without materializing the grants.
        bool hasUsageGrants = binding.UsageTags.IsNotUndefined() && binding.UsageTags.GetArrayLength() > 0;

        // The optional scalars carry the binding's raw CTJ value straight through From() — which propagates Undefined, so an
        // absent field is omitted with no IsNotUndefined/XxxOrNull ternary (the "Undefined not null" convention).
        b.Create(
            in ctx,
            authKind: binding.AuthKindValue.ToJsonToken(),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            credentialStatus: ToStatusToken(ctx.Status),
            environment: Models.JsonString.From(binding.Environment),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            secretRefs: Models.CredentialBindingSummary.SecretReferenceArray.Build(in ctx, BuildSecretRefs),
            sourceName: Models.JsonString.From(binding.SourceName),
            config: hasConfig ? Models.CredentialBindingSummary.CredentialConfigEntryArray.Build(in ctx, BuildConfig) : default,
            description: Models.JsonString.From(binding.Description),
            expiresAt: Models.JsonDateTime.From(binding.ExpiresAt),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy),
            managementTags: hasManagementTags ? Models.CredentialBindingSummary.CredentialSecurityTagArray.Build(in ctx, BuildManagementTags) : default,
            rotatedAt: Models.JsonDateTime.From(binding.RotatedAt),
            usageGrants: hasUsageGrants ? Models.CredentialBindingSummary.CredentialUsageGrantArray.Build(in ctx, BuildUsageGrants) : default);
    }

    // Each leaf item is built through its own context-threaded Build (the leaf value IS the context — a readonly struct
    // or record, no closure): the per-field From()/string value bridges thread straight into the item Builder.Create,
    // which writes into the array's builder rather than returning a span-bound Source (the property-parameter
    // Build(name:, refValue:) form would return a Source the compiler scopes to the From() temporaries — CS8347/CS8156 —
    // so the item Build<TContext> form is used instead, the same shape the grantee identity sub-array uses).
    private static void BuildSecretRefs(in SummaryContext ctx, ref Models.CredentialBindingSummary.SecretReferenceArray.Builder array)
    {
        foreach (SourceCredentialBinding.SecretReference reference in ctx.Binding.SecretRefs.EnumerateArray())
        {
            array.AddItem(Models.SecretReference.Build(in reference, BuildSecretRef));
        }
    }

    private static void BuildSecretRef(in SourceCredentialBinding.SecretReference reference, ref Models.SecretReference.Builder b)
        => b.Create(name: Models.JsonString.From(reference.Name), refValue: Models.JsonString.From(reference.Ref));

    private static void BuildConfig(in SummaryContext ctx, ref Models.CredentialBindingSummary.CredentialConfigEntryArray.Builder array)
    {
        foreach (SourceCredentialBinding.CredentialConfigEntry entry in ctx.Binding.Config.EnumerateArray())
        {
            array.AddItem(Models.CredentialConfigEntry.Build(in entry, BuildConfigEntry));
        }
    }

    private static void BuildConfigEntry(in SourceCredentialBinding.CredentialConfigEntry entry, ref Models.CredentialConfigEntry.Builder b)
        => b.Create(key: Models.JsonString.From(entry.Key), value: Models.JsonString.From(entry.Value));

    // Management tags are projected verbatim ({key, value}, no prefix strip — unlike usageGrants): a non-owning
    // SecurityTagSet view over the binding's ManagementTags array (no CopyFrom byte[]), enumerated as unescaped spans,
    // each written bytes-native. The binding document outlives the synchronous build, so the view is safe.
    private static void BuildManagementTags(in SummaryContext ctx, ref Models.CredentialBindingSummary.CredentialSecurityTagArray.Builder array)
    {
        SecurityTagSet managementTags = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(ctx.Binding.ManagementTags).Memory);
        SecurityTagSet.Utf8Enumerator e = managementTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                // UsageGrantSpans is the shared two-UTF-8-span carrier; here its first span is the verbatim tag key.
                var spans = new UsageGrantSpans(e.CurrentKey, e.CurrentValue);
                array.AddItem(Models.CredentialSecurityTag.Build(in spans, BuildSecurityTag));
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildSecurityTag(in UsageGrantSpans spans, ref Models.CredentialSecurityTag.Builder b)
        => b.Create(key: (Models.JsonString.Source)spans.Dimension, value: (Models.JsonString.Source)spans.Value);

    // The stored usage scope is described back as operator-facing identity grants (the inverse of the create mapping) —
    // internal tags are not exposed raw. Projected allocation-free straight off the binding's UsageTags: a non-owning
    // SecurityTagSet view over the array's raw UTF-8 (no CopyFrom byte[]), enumerated as unescaped spans, each prefixed
    // tag's (dimension, value) written bytes-native via the policy's span-based TryDescribeUsageGrant — no List, no
    // managed strings. The binding document outlives the synchronous build (handed to the workspace), so the view is safe.
    private static void BuildUsageGrants(in SummaryContext ctx, ref Models.CredentialBindingSummary.CredentialUsageGrantArray.Builder array)
    {
        SecurityTagSet usageTags = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(ctx.Binding.UsageTags).Memory);
        ControlPlaneAccess access = ctx.Access;
        SecurityTagSet.Utf8Enumerator e = usageTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                if (access.TryDescribeUsageGrant(e.CurrentKey, out ReadOnlySpan<byte> dimension))
                {
                    var spans = new UsageGrantSpans(dimension, e.CurrentValue);
                    array.AddItem(Models.CredentialUsageGrant.Build(in spans, BuildUsageGrant));
                }
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildUsageGrant(in UsageGrantSpans spans, ref Models.CredentialUsageGrant.Builder b)
        => b.Create(dimension: (Models.JsonString.Source)spans.Dimension, value: (Models.JsonString.Source)spans.Value);

    private static string ToStatusToken(CredentialStatus status) => status switch
    {
        CredentialStatus.Valid => "valid",
        CredentialStatus.ExpiringSoon => "expiringSoon",
        CredentialStatus.Expired => "expired",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown credential status."),
    };

    // ── credential-list projection (closure-free Build<TContext> over the page; §13) ────────────────────────────────
    //
    // The list body is built closure-free too: the page of bindings + the clock/policy needed for the per-binding
    // non-congruent transforms (status + describedGrants) are threaded through a ListContext to the static array
    // builder, which computes each binding's transforms inside the loop and projects it through BuildSummary. The whole
    // body is a CredentialBindingList.Source<ListContext> handed straight to ListCredentialsResult.Ok<TContext> (one
    // materialisation, no re-materialisation). The body is built inline in HandleListCredentialsAsync (not in a helper):
    // CredentialBindingList.Build scopes its result to the `in credentials` argument, so the body cannot escape a helper
    // (CS8347) and must be consumed in place — the same shape ArazzoControlPlaneIdentityHandler uses for GranteeList.
    // The summaries reference their pooled binding documents, which the caller has handed to the workspace
    // (TransferOwnershipTo) so the deferred body validation/serialization is safe.
    private readonly ref struct ListContext(IReadOnlyList<SourceCredentialBinding> bindings, ControlPlaneAccess access, TimeProvider timeProvider, TimeSpan expiringWindow)
    {
        public IReadOnlyList<SourceCredentialBinding> Bindings { get; } = bindings;

        public ControlPlaneAccess Access { get; } = access;

        public TimeProvider TimeProvider { get; } = timeProvider;

        public TimeSpan ExpiringWindow { get; } = expiringWindow;
    }

    private static void BuildSummaries(in ListContext ctx, ref Models.CredentialBindingList.CredentialBindingSummaryArray.Builder array)
    {
        DateTimeOffset now = ctx.TimeProvider.GetUtcNow();
        foreach (SourceCredentialBinding binding in ctx.Bindings)
        {
            CredentialStatus status = binding.DeriveStatus(now, ctx.ExpiringWindow);
            var summaryCtx = new SummaryContext(binding, status, ctx.Access);
            array.AddItem(Models.CredentialBindingSummary.Build(in summaryCtx, BuildSummary));
        }
    }

    private static Models.ProblemDetails.Source NotFoundProblem(string sourceName, string environment)
        => Problem("credential-not-found", "Credential not found", 404, $"No source credential binding for '{sourceName}@{environment}' exists.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}