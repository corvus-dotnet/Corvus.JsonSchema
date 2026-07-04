// <copyright file="ArazzoControlPlaneWorkspaceHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiWorkspaceHandler"/> over an <see cref="IWorkspaceWorkflowStore"/> — the
/// designer's working copies (workflow-designer design §4.1): mutable Arazzo documents saved as many times as needed
/// during development without ever minting a catalog version. The endpoints are gated by the
/// <c>workspace:read</c>/<c>workspace:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Reach (§14.2).</strong> Visibility and the data plane (list/get/create/save/delete) are reach-filtered
/// by the caller's <see cref="AccessContext"/> over each working copy's <c>managementTags</c>; a working copy outside
/// reach is reported as not found. Working copies are not governed (no administrator set) — reach membership is the
/// gate, and the only escalation guard is at create (a principal may not create a working copy it could not itself
/// manage).</para>
/// <para><strong>Concurrency.</strong> A save presents the etag it read (<c>expectedEtag</c>); a stale save returns
/// <c>409</c> rather than clobbering a collaborator's work — the client re-fetches and reconciles.</para>
/// <para><strong>The document split.</strong> The Arazzo document (and designer state) can be large, so they are
/// returned only on a single read/create/save — where the persisted <see cref="WorkspaceWorkflow"/> is congruent with
/// the API <see cref="Models.WorkingCopy"/> and reads project as a free whole-document re-wrap
/// (<see cref="Models.WorkingCopy"/><c>.From</c>). The list returns a document-less
/// <see cref="Models.WorkingCopySummary"/>, so each list row is field-selected.</para>
/// </remarks>
public sealed class ArazzoControlPlaneWorkspaceHandler : IApiWorkspaceHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IWorkspaceWorkflowStore store;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ControlPlaneAccess access;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/> — no
    /// row security).</summary>
    /// <param name="store">The persistent working-copy store the endpoints delegate to.</param>
    /// <param name="catalog">The secured catalog used to open a working copy from a published version (the carry-over);
    /// creating from a version is unavailable when <see langword="null"/>.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    public ArazzoControlPlaneWorkspaceHandler(IWorkspaceWorkflowStore store, ISecuredWorkflowCatalog? catalog = null, string actor = "control-plane")
        : this(store, new ControlPlaneAccess(), catalog, actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneWorkspaceHandler"/> class.</summary>
    /// <param name="store">The persistent working-copy store the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request and the internal tenant tags
    /// stamped onto working copies (§14.2). Unscoped (<see cref="AccessContext.System"/>) when no row security is configured.</param>
    /// <param name="catalog">The secured catalog used to open a working copy from a published version (the carry-over);
    /// creating from a version is unavailable when <see langword="null"/>.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    internal ArazzoControlPlaneWorkspaceHandler(IWorkspaceWorkflowStore store, ControlPlaneAccess access, ISecuredWorkflowCatalog? catalog = null, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.catalog = catalog;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListWorkspaceWorkflowsResult> HandleListWorkspaceWorkflowsAsync(ListWorkspaceWorkflowsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;

        // The opaque page token flows to the store as its JSON value (From() rewraps parameters.PageToken — free, no
        // managed string; an undefined token rewraps to an undefined JsonString); the store decodes it bytes-native.
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using WorkspaceWorkflowPage page = await this.store.ListAsync(this.access.Current(), limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The list summary is NOT congruent with the stored working copy (it drops the document/designer state/tags), so
        // each row is field-selected through the closure-free Build<TContext> over the page. The summaries reference the
        // pooled documents, and the body is validated/serialized after this returns, so hand the documents to the
        // workspace (it disposes them at request end); `using page` then only returns the batch's backing array.
        page.WorkingCopies.TransferOwnershipTo(workspace);
        IReadOnlyList<WorkspaceWorkflow> workingCopies = page.WorkingCopies;
        ReadOnlyMemory<byte> nextPageToken = page.NextPageToken;
        Models.WorkingCopyList.Source<IReadOnlyList<WorkspaceWorkflow>> body = Models.WorkingCopyList.Build(
            in workingCopies,
            workingCopies: Models.WorkingCopyList.WorkingCopySummaryArray.Build(in workingCopies, BuildSummaries),
            nextPageToken: nextPageToken.IsEmpty ? default : (Models.JsonString.Source)nextPageToken.Span);
        return ListWorkspaceWorkflowsResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CreateWorkspaceWorkflowResult> HandleCreateWorkspaceWorkflowAsync(CreateWorkspaceWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.WorkingCopyCreate body = parameters.Body;
        bool hasDocument = body.Document.IsNotUndefined();
        bool fromVersion = body.FromBaseWorkflowId.IsNotUndefined();
        if (hasDocument && fromVersion)
        {
            return CreateWorkspaceWorkflowResult.BadRequest(
                Problem("invalid-working-copy", "Invalid working copy", 400, "Supply a document OR a from-version, not both."), workspace);
        }

        // managementTags = the principal's deployment-internal tenant tag (always stamped, so the creator keeps
        // management) PLUS any operator-supplied labels (validated against the reserved internal prefix).
        SecurityTagSet managementTags;
        try
        {
            SecurityTagSet userManagement = body.ManagementTags.IsNotUndefined()
                ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(body.ManagementTags).Memory)
                : SecurityTagSet.Empty;
            this.access.ValidateUserTags(userManagement);
            var tagsState = new ManagementTagsState(this.access.InternalTags(), userManagement);
            managementTags = SecurityTagSet.Build(in tagsState, WriteManagementTags);
        }
        catch (ArgumentException ex)
        {
            return CreateWorkspaceWorkflowResult.BadRequest(Problem("invalid-working-copy", "Invalid working copy", 400, ex.Message), workspace);
        }

        // Guard against privilege escalation: a principal may not create a working copy it could not itself manage.
        if (!managementTags.IsEmpty && !this.access.Current().Admits(AccessVerb.Write, managementTags))
        {
            return CreateWorkspaceWorkflowResult.BadRequest(
                Problem("management-out-of-reach", "Management scope out of reach", 400, "The working copy's management tags are outside your own management reach."), workspace);
        }

        // Resolve the document: supplied verbatim, copied from a catalog version (the carry-over), or a blank skeleton.
        ReadOnlyMemory<byte> documentUtf8;
        string? baseWorkflowId = null;
        int? basedOnVersion = null;
        if (fromVersion)
        {
            if (this.catalog is null)
            {
                return CreateWorkspaceWorkflowResult.BadRequest(
                    Problem("carry-over-unavailable", "Carry-over unavailable", 400, "This deployment does not offer catalog carry-over."), workspace);
            }

            if (!body.FromVersionNumber.IsNotUndefined())
            {
                return CreateWorkspaceWorkflowResult.BadRequest(
                    Problem("invalid-working-copy", "Invalid working copy", 400, "fromVersionNumber is required with fromBaseWorkflowId."), workspace);
            }

            baseWorkflowId = (string)body.FromBaseWorkflowId;
            basedOnVersion = (int)body.FromVersionNumber;
            ReadOnlyMemory<byte>? copied = await this.catalog.GetDocumentAsync(
                baseWorkflowId, basedOnVersion.Value, CatalogPackage.WorkflowDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (copied is not { } workflowDocument)
            {
                return CreateWorkspaceWorkflowResult.NotFound(
                    Problem("version-not-found", "Catalog version not found", 404, $"No catalog version '{baseWorkflowId}' v{basedOnVersion} exists, or it is outside your reach."), workspace);
            }

            documentUtf8 = workflowDocument;
        }
        else if (hasDocument)
        {
            documentUtf8 = JsonMarshal.GetRawUtf8Value((JsonElement)body.Document).Memory;
        }
        else
        {
            documentUtf8 = ReadOnlyMemory<byte>.Empty; // the skeleton is written below, once the name is known
        }

        // The display name: supplied, or derived from the document's first workflowId, or 'untitled'.
        string name = body.Name.IsNotUndefined() ? (string)body.Name : DeriveName(documentUtf8.Span);
        if (documentUtf8.IsEmpty)
        {
            documentUtf8 = BlankDocument(name);
        }

        ReadOnlyMemory<byte> designerStateUtf8 = body.DesignerState.IsNotUndefined()
            ? JsonMarshal.GetRawUtf8Value((JsonElement)body.DesignerState).Memory
            : ReadOnlyMemory<byte>.Empty;

        try
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft(name, documentUtf8, designerStateUtf8, baseWorkflowId, basedOnVersion, managementTags);
            ParsedJsonDocument<WorkspaceWorkflow> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);

            // The full working copy (document included) is congruent with the API model — a free whole-document re-wrap.
            workspace.TakeOwnership(created);
            return CreateWorkspaceWorkflowResult.Created(Models.WorkingCopy.From(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateWorkspaceWorkflowResult.BadRequest(Problem("invalid-working-copy", "Invalid working copy", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GetWorkspaceWorkflowResult> HandleGetWorkspaceWorkflowAsync(GetWorkspaceWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return GetWorkspaceWorkflowResult.NotFound(NotFoundProblem(id), workspace);
        }

        workspace.TakeOwnership(w);
        return GetWorkspaceWorkflowResult.Ok(Models.WorkingCopy.From(w.RootElement), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateWorkspaceWorkflowResult> HandleUpdateWorkspaceWorkflowAsync(UpdateWorkspaceWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        Models.WorkingCopyUpdate body = parameters.Body;
        if (!body.Document.IsNotUndefined())
        {
            return UpdateWorkspaceWorkflowResult.BadRequest(
                Problem("invalid-working-copy", "Invalid working copy", 400, "A save requires the 'document'."), workspace);
        }

        if (!body.ExpectedEtag.IsNotUndefined())
        {
            return UpdateWorkspaceWorkflowResult.BadRequest(
                Problem("invalid-working-copy", "Invalid working copy", 400, "A save requires the 'expectedEtag' it read (optimistic concurrency)."), workspace);
        }

        var expectedEtag = new WorkflowEtag((string)body.ExpectedEtag);

        // The draft carries the new document (and optionally name/designer state) bytes-to-bytes; the immutable
        // provenance and tags are carried forward by the store from the stored working copy.
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft(
            (JsonElement)body.Name,
            default,
            default,
            (JsonElement)body.Document,
            (JsonElement)body.DesignerState,
            SecurityTagSet.Empty);
        try
        {
            ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, expectedEtag, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (saved is not { } w)
            {
                return UpdateWorkspaceWorkflowResult.NotFound(NotFoundProblem(id), workspace);
            }

            workspace.TakeOwnership(w);
            return UpdateWorkspaceWorkflowResult.Ok(Models.WorkingCopy.From(w.RootElement), workspace);
        }
        catch (WorkspaceWorkflowConflictException ex)
        {
            return UpdateWorkspaceWorkflowResult.Conflict(
                Problem("save-conflict", "Save conflict", 409, ex.Message + " Re-fetch the working copy and reconcile."), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteWorkspaceWorkflowResult> HandleDeleteWorkspaceWorkflowAsync(DeleteWorkspaceWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        bool deleted = await this.store.DeleteAsync(id, WorkflowEtag.None, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return deleted
            ? DeleteWorkspaceWorkflowResult.NoContent()
            : DeleteWorkspaceWorkflowResult.NotFound(NotFoundProblem(id), workspace);
    }

    // ── working-copy summary list projection (field-select, closure-free Build<TContext>) ─────────────────────────────
    private static void BuildSummaries(in IReadOnlyList<WorkspaceWorkflow> workingCopies, ref Models.WorkingCopyList.WorkingCopySummaryArray.Builder array)
    {
        foreach (WorkspaceWorkflow workingCopy in workingCopies)
        {
            array.AddItem(Models.WorkingCopySummary.Build(in workingCopy, BuildSummary));
        }
    }

    private static void BuildSummary(in WorkspaceWorkflow workingCopy, ref Models.WorkingCopySummary.Builder b)
    {
        // The document, designer state, and tags are deliberately NOT projected (the list is light); every other field is
        // carried straight through From()/the value bridges — which propagate Undefined, so an absent optional field is
        // simply omitted.
        b.Create(
            createdAt: Models.JsonDateTime.From(workingCopy.CreatedAt),
            createdBy: Models.JsonString.From(workingCopy.CreatedBy),
            etag: Models.JsonString.From(workingCopy.Etag),
            id: Models.JsonString.From(workingCopy.Id),
            name: Models.JsonString.From(workingCopy.Name),
            baseWorkflowId: Models.JsonString.From(workingCopy.BaseWorkflowId),
            basedOnVersion: Models.JsonInteger.From(workingCopy.BasedOnVersion),
            lastUpdatedAt: Models.JsonDateTime.From(workingCopy.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(workingCopy.LastUpdatedBy));
    }

    // ── document helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    // Derives a display name from the document's first workflowId ('untitled' when there is none) — a linear
    // Utf8JsonReader scan of the raw bytes; the document may be interim-invalid, so this never throws.
    private static string DeriveName(ReadOnlySpan<byte> documentUtf8)
    {
        if (documentUtf8.IsEmpty)
        {
            return "untitled";
        }

        try
        {
            var reader = new Utf8JsonReader(documentUtf8);
            int depth = -1;
            bool inWorkflows = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == 1 && reader.ValueTextEquals("workflows"u8))
                {
                    inWorkflows = true;
                    depth = reader.CurrentDepth;
                    continue;
                }

                if (inWorkflows && reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals("workflowId"u8))
                {
                    reader.Read();
                    return reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "untitled" : "untitled";
                }

                if (inWorkflows && reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == depth)
                {
                    break; // workflows was empty
                }
            }
        }
        catch (JsonException)
        {
            // An interim-invalid document is fine — the name just defaults.
        }

        return "untitled";
    }

    // A blank Arazzo skeleton: enough structure for the designer to open, deliberately not yet valid (working copies
    // hold work in progress; validation is on demand).
    private static ReadOnlyMemory<byte> BlankDocument(string name)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("arazzo"u8, "1.1.0"u8);
            writer.WriteStartObject("info"u8);
            writer.WriteString("title"u8, name);
            writer.WriteString("version"u8, "0.1.0"u8);
            writer.WriteEndObject();
            writer.WriteStartArray("sourceDescriptions"u8);
            writer.WriteEndArray();
            writer.WriteStartArray("workflows"u8);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    // ── management-tag build (internal + operator tags → one SecurityTagSet), mirroring the sources handler ───────────
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
    private static Models.ProblemDetails.Source NotFoundProblem(string id)
        => Problem("working-copy-not-found", "Working copy not found", 404, $"No working copy '{id}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}