// <copyright file="ArazzoControlPlaneSourcesHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiSourcesHandler"/> over an <see cref="ISourceStore"/> — the control-plane
/// surface that registers and manages first-class, reach-scoped sources (design §7.6): the OpenAPI/AsyncAPI documents a
/// workflow references by name in its <c>sourceDescriptions</c>. The endpoints are gated by the
/// <c>sources:read</c>/<c>sources:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Reach (§14.2).</strong> Visibility and the data plane (list/get/register/update/delete) are
/// reach-filtered by the caller's <see cref="AccessContext"/> over each source's <c>managementTags</c>; a source outside
/// reach is reported as not found. Sources are NOT governed (there is no administrator set, unlike environments §7.7) —
/// reach membership is the management gate, so an out-of-reach update/delete is a 404, and the only escalation guard is
/// at registration (a principal may not register a source it could not itself manage).</para>
/// <para><strong>The document split.</strong> The registered <c>document</c> can be large, so it is returned only on a
/// single read/register/update — where the persisted <see cref="RegisteredSource"/> is congruent with the API
/// <see cref="Models.SourceEntity"/> and reads project as a free whole-document re-wrap
/// (<see cref="Models.SourceEntity"/><c>.From</c>). The list returns a document-less <see cref="Models.SourceSummary"/>,
/// so each list row is field-selected (the document is dropped) rather than re-wrapped.</para>
/// </remarks>
public sealed class ArazzoControlPlaneSourcesHandler : IApiSourcesHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly ISourceStore store;
    private readonly ControlPlaneAccess access;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/> — no
    /// row security).</summary>
    /// <param name="store">The persistent source store the endpoints delegate to.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneSourcesHandler(ISourceStore store, string actor = "control-plane")
        : this(store, new ControlPlaneAccess(), actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneSourcesHandler"/> class.</summary>
    /// <param name="store">The persistent source store the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request and the internal tenant tags
    /// stamped onto registered sources (§14.2). Unscoped (<see cref="AccessContext.System"/>) when no row security is configured.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    internal ArazzoControlPlaneSourcesHandler(ISourceStore store, ControlPlaneAccess access, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListSourcesResult> HandleListSourcesAsync(ListSourcesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // managed string; an undefined token rewraps to an undefined JsonString); the store decodes it bytes-native.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using SourcePage page = await this.store.ListAsync(this.access.Current(), limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The list summary is NOT congruent with the stored source (it drops the document), so each row is field-selected
        // through the closure-free Build<TContext> over the page; the management tags are projected from a non-owning view
        // over each source's array. The summaries reference the pooled source documents, and the body is
        // validated/serialized after this returns, so hand the documents to the workspace (it disposes them at request
        // end); `using page` then only returns the batch's backing array.
        page.Sources.TransferOwnershipTo(workspace);
        IReadOnlyList<RegisteredSource> sources = page.Sources;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.SourceList.Source<IReadOnlyList<RegisteredSource>> body = Models.SourceList.Build(
            in sources,
            sources: Models.SourceList.SourceSummaryArray.Build(in sources, BuildSummaries),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListSourcesResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateSourceResult> HandleCreateSourceAsync(CreateSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.SourceCreate body = parameters.Body;
        SecurityTagSet managementTags;
        try
        {
            if (!body.Name.IsNotUndefined())
            {
                throw new ArgumentException("A 'name' is required.");
            }

            if (!body.Type.IsNotUndefined())
            {
                throw new ArgumentException("A 'type' is required.");
            }

            if (!body.Document.IsNotUndefined())
            {
                throw new ArgumentException("A 'document' is required.");
            }

            // managementTags = the principal's deployment-internal tenant tag (always stamped, so the registrant keeps
            // management) PLUS any operator-supplied management labels (validated against the reserved internal prefix as a
            // non-owning view over the request body's array). Built once into a SecurityTagSet — the draft and the
            // privilege-escalation guard both read it.
            SecurityTagSet userManagement = body.ManagementTags.IsNotUndefined()
                ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(body.ManagementTags).Memory)
                : SecurityTagSet.Empty;
            this.access.ValidateUserTags(userManagement);
            var tagsState = new ManagementTagsState(this.access.InternalTags(), userManagement);
            managementTags = SecurityTagSet.Build(in tagsState, WriteManagementTags);
        }
        catch (ArgumentException ex)
        {
            return CreateSourceResult.BadRequest(Problem("invalid-source", "Invalid source", 400, ex.Message), workspace);
        }

        // Guard against privilege escalation: a principal may not register a source it could not itself manage.
        if (!managementTags.IsEmpty && !this.access.Current().Admits(AccessVerb.Write, managementTags))
        {
            return CreateSourceResult.BadRequest(
                Problem("management-out-of-reach", "Management scope out of reach", 400, "The source's management tags are outside your own management reach."), workspace);
        }

        try
        {
            // The persisted source carries the request body's name/type/document/metadata JSON values bytes-to-bytes (the
            // potentially large document included) plus the resolved management tags; the store stamps createdBy/createdAt/etag.
            using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(
                (JsonElement)body.Name,
                (JsonElement)body.Type,
                (JsonElement)body.Document,
                (JsonElement)body.DisplayName,
                (JsonElement)body.Description,
                managementTags);
            ParsedJsonDocument<RegisteredSource> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);

            // The full source (document included) is congruent with the API model — a free whole-document re-wrap. Hand the
            // pooled document to the workspace (it disposes it after the response is written); the draft is input only.
            workspace.TakeOwnership(created);
            return CreateSourceResult.Created(Models.SourceEntity.From(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateSourceResult.BadRequest(Problem("invalid-source", "Invalid source", 400, ex.Message), workspace);
        }
        catch (InvalidOperationException ex)
        {
            return CreateSourceResult.Conflict(Problem("source-exists", "Source already exists", 409, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetSourceResult> HandleGetSourceAsync(GetSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        ParsedJsonDocument<RegisteredSource>? source = await this.store.GetAsync(name, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (source is not { } s)
        {
            return GetSourceResult.NotFound(NotFoundProblem(name), workspace);
        }

        // Congruent whole-document re-wrap (Models.SourceEntity.From, the document included) — hand the pooled document to
        // the workspace so the deferred body validation/serialization is safe (it disposes the document afterwards).
        workspace.TakeOwnership(s);
        return GetSourceResult.Ok(Models.SourceEntity.From(s.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateSourceResult> HandleUpdateSourceAsync(UpdateSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        Models.SourceUpdate body = parameters.Body;

        // A present managementTags re-tags the reach scope (§14.2): the caller's non-internal labels replace the old ones
        // while the deployment-internal tenant tag is (re-)stamped so management is preserved (validated against the
        // reserved prefix). Absent leaves the tags unchanged — the draft omits them and the store carries them forward. The
        // immutable name + type + created-* audit are carried forward; an undefined document keeps the stored one.
        SecurityTagSet managementTags = SecurityTagSet.Empty;
        if (body.ManagementTags.IsNotUndefined())
        {
            try
            {
                SecurityTagSet userManagement = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(body.ManagementTags).Memory);
                this.access.ValidateUserTags(userManagement);
                var tagsState = new ManagementTagsState(this.access.InternalTags(), userManagement);
                managementTags = SecurityTagSet.Build(in tagsState, WriteManagementTags);
            }
            catch (ArgumentException ex)
            {
                return UpdateSourceResult.BadRequest(Problem("invalid-source", "Invalid source", 400, ex.Message), workspace);
            }

            // A principal may not re-tag a source to a management scope outside its own reach.
            if (!managementTags.IsEmpty && !this.access.Current().Admits(AccessVerb.Write, managementTags))
            {
                return UpdateSourceResult.BadRequest(
                    Problem("management-out-of-reach", "Management scope out of reach", 400, "The source's management tags are outside your own management reach."), workspace);
            }
        }

        using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(
            default,
            default,
            (JsonElement)body.Document,
            (JsonElement)body.DisplayName,
            (JsonElement)body.Description,
            managementTags);
        ParsedJsonDocument<RegisteredSource>? updated = await this.store.UpdateAsync(name, draft.RootElement, WorkflowEtag.None, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (updated is not { } s)
        {
            return UpdateSourceResult.NotFound(NotFoundProblem(name), workspace);
        }

        workspace.TakeOwnership(s);
        return UpdateSourceResult.Ok(Models.SourceEntity.From(s.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteSourceResult> HandleDeleteSourceAsync(DeleteSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string name = (string)parameters.Name;
        bool deleted = await this.store.DeleteAsync(name, WorkflowEtag.None, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return deleted
            ? DeleteSourceResult.NoContent()
            : DeleteSourceResult.NotFound(NotFoundProblem(name), workspace);
    }

    // ── source-summary list projection (field-select, closure-free Build<TContext>; the document is dropped) ──────────
    private static void BuildSummaries(in IReadOnlyList<RegisteredSource> sources, ref Models.SourceList.SourceSummaryArray.Builder array)
    {
        foreach (RegisteredSource source in sources)
        {
            array.AddItem(Models.SourceSummary.Build(in source, BuildSummary));
        }
    }

    private static void BuildSummary(in RegisteredSource source, ref Models.SourceSummary.Builder b)
    {
        // A cheap array-length check — testing emptiness via ManagementTagsValue would allocate a CopyFrom byte[] per row.
        bool hasManagementTags = source.ManagementTags.IsNotUndefined() && source.ManagementTags.GetArrayLength() > 0;

        // The document is deliberately NOT projected (the list is document-less); every other field is carried straight
        // through From()/the value bridges — which propagate Undefined, so an absent optional field is simply omitted.
        b.Create(
            in source,
            createdAt: Models.JsonDateTime.From(source.CreatedAt),
            createdBy: Models.JsonString.From(source.CreatedBy),
            etag: Models.JsonString.From(source.Etag),
            name: Models.JsonString.From(source.Name),
            type: Models.SourceType.From(source.Type),
            description: Models.JsonString.From(source.Description),
            displayName: Models.JsonString.From(source.DisplayName),
            lastUpdatedAt: Models.JsonDateTime.From(source.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(source.LastUpdatedBy),
            managementTags: hasManagementTags ? Models.SourceSummary.SourceSecurityTagArray.Build(in source, BuildManagementTags) : default);
    }

    // Management tags are projected verbatim ({key, value}): a non-owning SecurityTagSet view over the source's
    // ManagementTags array (no CopyFrom byte[]), enumerated as unescaped spans, each written bytes-native. The source
    // document outlives the synchronous build (handed to the workspace), so the view is safe.
    private static void BuildManagementTags(in RegisteredSource source, ref Models.SourceSummary.SourceSecurityTagArray.Builder array)
    {
        SecurityTagSet managementTags = SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(source.ManagementTags).Memory);
        SecurityTagSet.Utf8Enumerator e = managementTags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                var spans = new TagSpans(e.CurrentKey, e.CurrentValue);
                array.AddItem(Models.SourceSecurityTag.Build(in spans, BuildSecurityTag));
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildSecurityTag(in TagSpans spans, ref Models.SourceSecurityTag.Builder b)
        => b.Create(key: (Models.JsonString.Source)spans.Key, value: (Models.JsonString.Source)spans.Value);

    // A two-UTF-8-span carrier for a single {key, value} tag, threaded as the context into the leaf SourceSecurityTag build.
    private readonly ref struct TagSpans(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        public ReadOnlySpan<byte> Key { get; } = key;

        public ReadOnlySpan<byte> Value { get; } = value;
    }

    // ── management-tag build (internal + operator tags → one SecurityTagSet), mirroring the environments handler ──────
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

    private readonly struct ManagementTagsState(IReadOnlyList<SecurityTag> internalTags, SecurityTagSet userTags)
    {
        public IReadOnlyList<SecurityTag> InternalTags { get; } = internalTags;

        public SecurityTagSet UserTags { get; } = userTags;
    }

    // ── problem documents ──────────────────────────────────────────────────────────────────────────────────────────
    private static Models.ProblemDetails.Source NotFoundProblem(string name)
        => Problem("source-not-found", "Source not found", 404, $"No source named '{name}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}