// <copyright file="ArazzoControlPlaneWorkspaceHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Internal;
using ValidatorSchema = Corvus.Text.Json.Validator.JsonSchema;

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
    private const int MaxValidationErrors = 200;

    private readonly IWorkspaceWorkflowStore store;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ISourceStore? sources;
    private readonly Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator? simulator;
    private readonly ControlPlaneAccess access;
    private readonly TimeProvider timeProvider;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/> — no
    /// row security).</summary>
    /// <param name="store">The persistent working-copy store the endpoints delegate to.</param>
    /// <param name="catalog">The secured catalog used to open a working copy from a published version (the carry-over);
    /// creating from a version is unavailable when <see langword="null"/>.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    /// <param name="simulator">The deterministic simulator (design §8); simulation fails closed when <see langword="null"/>.</param>
    public ArazzoControlPlaneWorkspaceHandler(IWorkspaceWorkflowStore store, ISecuredWorkflowCatalog? catalog = null, string actor = "control-plane", WorkflowSimulator? simulator = null)
        : this(store, new ControlPlaneAccess(), catalog, null, null, actor, simulator)
    {
    }

    /// <summary>
    /// Simulates the working copy deterministically (design §4.3/§8): compile through the catalog's
    /// own executor path (content-hash cached), replay against the scripted mock transport and
    /// virtual clock to the stop condition, and return the structured trace. Stateless: every debug
    /// command replays from the start. Fails closed when this deployment wires no simulator.
    /// </summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The trace, or the problem.</returns>
    public async ValueTask<SimulateWorkingCopyResult> HandleSimulateWorkingCopyAsync(SimulateWorkingCopyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.simulator is null)
        {
            return SimulateWorkingCopyResult.BadRequest(
                Problem("simulation-not-offered", "Simulation not offered", 400, "This deployment does not offer workflow simulation."),
                workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return SimulateWorkingCopyResult.NotFound(NotFoundProblem(id), workspace);
        }

        Models.SimulateRequest body = parameters.Body;
        string? workflowId = body.WorkflowId.IsNotUndefined() ? (string)body.WorkflowId : null;
        byte[]? documentBytes = WorkspaceSimulationJson.DocumentBytes((JsonElement)w.RootElement.Document, workflowId);
        if (documentBytes is null)
        {
            return SimulateWorkingCopyResult.BadRequest(
                Problem("unknown-workflow", "Unknown workflow", 400, $"The document declares no workflow '{workflowId}'."),
                workspace);
        }

        List<KeyValuePair<string, byte[]>> sourceBytes = await WorkspaceSimulationJson.SourceBytesAsync(
            (JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);

        using SimulationResult result = await this.simulator.SimulateAsync(
            documentBytes,
            sourceBytes,
            WorkspaceSimulationJson.ReadScenario(body),
            WorkspaceSimulationJson.ReadStop(body),
            WorkspaceSimulationJson.ReadBudget(body),
            cancellationToken).ConfigureAwait(false);

        if (result.Outcome == SimulationOutcome.NotExecutable)
        {
            return SimulateWorkingCopyResult.UnprocessableEntity(
                Problem("not-executable", "Not executable", 422, "The document does not compile to an executable workflow (e.g. it references another Arazzo document as a source, or has no workflows)."),
                workspace);
        }

        ParsedJsonDocument<Models.SimulationTrace> trace = WorkspaceSimulationJson.TraceResponse(result);
        workspace.TakeOwnership(trace);
        return SimulateWorkingCopyResult.Ok(trace.RootElement, workspace);
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneWorkspaceHandler"/> class.</summary>
    /// <param name="store">The persistent working-copy store the endpoints delegate to.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request and the internal tenant tags
    /// stamped onto working copies (§14.2). Unscoped (<see cref="AccessContext.System"/>) when no row security is configured.</param>
    /// <param name="catalog">The secured catalog used to open a working copy from a published version (the carry-over);
    /// creating from a version is unavailable when <see langword="null"/>.</param>
    /// <param name="sources">The source registry used to resolve registry-reference attachments at read time
    /// (reach-checked); registry attachments are unavailable when <see langword="null"/>.</param>
    /// <param name="timeProvider">The time source stamped onto attachments; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="actor">The audit actor recorded on writes (a deployment may resolve this from the principal).</param>
    internal ArazzoControlPlaneWorkspaceHandler(IWorkspaceWorkflowStore store, ControlPlaneAccess access, ISecuredWorkflowCatalog? catalog = null, ISourceStore? sources = null, TimeProvider? timeProvider = null, string actor = "control-plane", Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator? simulator = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.catalog = catalog;
        this.sources = sources;
        this.simulator = simulator;
        this.timeProvider = timeProvider ?? TimeProvider.System;
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

        // Resolve the carry-over document when requested; the draft below writes the request document
        // bytes-to-bytes, the carry-over bytes, or an inline blank skeleton — no interim buffers.
        ReadOnlyMemory<byte> catalogDocumentUtf8 = default;
        ReadOnlyMemory<byte> carriedScenariosUtf8 = default;
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

            // Load the whole package once (owned copy), then slice the workflow document AND the
            // scenario set from it: both carry forward (§9), so the new working copy inherits the
            // version's tests. The owned array outlives the draft write, keeping the slices valid.
            ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(
                baseWorkflowId, basedOnVersion.Value, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (package is not { } packageBytes)
            {
                return CreateWorkspaceWorkflowResult.NotFound(
                    Problem("version-not-found", "Catalog version not found", 404, $"No catalog version '{baseWorkflowId}' v{basedOnVersion} exists, or it is outside your reach."), workspace);
            }

            var ownedPackage = new ReadOnlyMemory<byte>(packageBytes.ToArray());
            if (!WorkflowPackage.TryReadEntry(ownedPackage, "workflow.json"u8, out catalogDocumentUtf8))
            {
                return CreateWorkspaceWorkflowResult.NotFound(
                    Problem("version-not-found", "Catalog version not found", 404, $"No catalog version '{baseWorkflowId}' v{basedOnVersion} exists, or it is outside your reach."), workspace);
            }

            if (WorkflowPackage.TryReadEntry(ownedPackage, "metadata/scenarios.json"u8, out ReadOnlyMemory<byte> scenarios))
            {
                carriedScenariosUtf8 = scenarios;
            }
        }

        try
        {
            // The whole create draft — name resolution (supplied / first workflowId / base id /
            // 'untitled'), the document (request bytes-to-bytes / carry-over / blank skeleton),
            // designer state, provenance, and tags — writes in ONE pooled pass.
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceSourceJson.CreateDraft(
                (JsonElement)body.Name, (JsonElement)body.Document, catalogDocumentUtf8, (JsonElement)body.DesignerState, baseWorkflowId, basedOnVersion, managementTags, carriedScenariosUtf8);
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
            default,
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

    /// <inheritdoc/>
    public async ValueTask<ValidateWorkspaceWorkflowResult> HandleValidateWorkspaceWorkflowAsync(ValidateWorkspaceWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return ValidateWorkspaceWorkflowResult.NotFound(NotFoundProblem(id), workspace);
        }

        // All three passes run over the stored document and their findings merge (shared with the
        // publish gate). An invalid document is a SUCCESSFUL validation run (200 + findings).
        var document = (Corvus.Text.Json.JsonElement)w.RootElement.Document;
        List<Finding> findings = await this.CollectDiagnosticsAsync(document, (JsonElement)w.RootElement.Sources, cancellationToken).ConfigureAwait(false);

        bool valid = true;
        foreach (Finding finding in findings)
        {
            if (finding.Severity == "error")
            {
                valid = false;
                break;
            }
        }

        Models.WorkingCopyDiagnostics.Source<List<Finding>> body = Models.WorkingCopyDiagnostics.Build(
            in findings,
            valid: valid,
            diagnostics: Models.WorkingCopyDiagnostics.WorkflowDiagnosticArray.Build(in findings, BuildFindings));
        return ValidateWorkspaceWorkflowResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListWorkingCopySourcesResult> HandleListWorkingCopySourcesAsync(ListWorkingCopySourcesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return ListWorkingCopySourcesResult.NotFound(NotFoundProblem(id), workspace);
        }

        // The list is the stored attachments minus their inline documents — one pooled write+parse pass;
        // the pooled body document is handed to the workspace (it disposes it after serialization).
        ParsedJsonDocument<Models.AttachedSourceList> body = WorkspaceSourceJson.AttachmentListResponse((JsonElement)w.RootElement.Sources);
        workspace.TakeOwnership(body);
        return ListWorkingCopySourcesResult.Ok(body.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AttachWorkingCopySourceResult> HandleAttachWorkingCopySourceAsync(AttachWorkingCopySourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.Name;
        Models.AttachSourceRequest body = parameters.Body;
        bool isRegistry = body.SourceName.IsNotUndefined();
        bool isInline = body.Document.IsNotUndefined();
        if (isRegistry == isInline)
        {
            return AttachWorkingCopySourceResult.BadRequest(
                Problem("invalid-attachment", "Invalid attachment", 400, "Supply EXACTLY ONE of sourceName (a registry reference) or document (an inline upload)."), workspace);
        }

        string? sourceName = isRegistry ? (string)body.SourceName : null;
        string? type = body.Type.IsNotUndefined() ? (string)body.Type : null;
        if (isInline && type is null)
        {
            type = WorkspaceSourceJson.DetectDocumentType((JsonElement)body.Document);
            if (type is null)
            {
                return AttachWorkingCopySourceResult.BadRequest(
                    Problem("invalid-attachment", "Invalid attachment", 400, "The inline document declares neither openapi, asyncapi, nor arazzo; supply an explicit type."), workspace);
            }
        }

        if (isRegistry)
        {
            if (this.sources is null)
            {
                return AttachWorkingCopySourceResult.BadRequest(
                    Problem("registry-unavailable", "Source registry unavailable", 400, "This deployment does not offer registry attachments."), workspace);
            }

            // The reference must resolve within the caller's reach NOW (attach-time honesty); it re-resolves on every read.
            using ParsedJsonDocument<RegisteredSource>? registered = await this.sources.GetAsync(sourceName!, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (registered is not { } r)
            {
                return AttachWorkingCopySourceResult.NotFound(
                    Problem("source-not-found", "Source not found", 404, $"No registered source named '{sourceName}' exists, or it is outside your reach."), workspace);
            }

            type ??= r.RootElement.Type.IsNotUndefined() ? (string)r.RootElement.Type : null;
        }

        // Read-modify-write under the etag we read: the replacement attachment set rides a draft that
        // carries ONLY sources (everything else carries forward bytes-to-bytes in the store).
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return AttachWorkingCopySourceResult.NotFound(NotFoundProblem(id), workspace);
        }

        try
        {
            // The replacement attachment set composes INSIDE the draft's single pooled write pass (stored
            // entries bytes-to-bytes, the new entry from the request); the store carries everything else
            // forward, and the response body is a second pooled pass over the saved entry.
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceSourceJson.DraftReplacingAttachment(
                (JsonElement)w.RootElement.Sources, name, isRegistry ? "registry" : "inline", sourceName, (JsonElement)body.Document, type, this.actor, this.timeProvider.GetUtcNow());
            using ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, w.RootElement.EtagValue, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (saved is not { } s)
            {
                return AttachWorkingCopySourceResult.NotFound(NotFoundProblem(id), workspace);
            }

            ParsedJsonDocument<Models.AttachedSource> response = WorkspaceSourceJson.AttachmentResponse((JsonElement)s.RootElement.Sources, name, s.RootElement.EtagValue.Value);
            workspace.TakeOwnership(response);
            return AttachWorkingCopySourceResult.Ok(response.RootElement, workspace);
        }
        catch (WorkspaceWorkflowConflictException ex)
        {
            return AttachWorkingCopySourceResult.Conflict(
                Problem("save-conflict", "Save conflict", 409, ex.Message + " Retry the attach against the fresh state."), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ListScenariosResult> HandleListScenariosAsync(ListScenariosParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return ListScenariosResult.NotFound(NotFoundProblem(id), workspace);
        }

        ParsedJsonDocument<Models.GetWorkspaceWorkflowsByIdScenariosOk> body = WorkspaceScenarioJson.ListResponse((JsonElement)w.RootElement.Scenarios);
        workspace.TakeOwnership(body);
        return ListScenariosResult.Ok(body.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PutScenarioResult> HandlePutScenarioAsync(PutScenarioParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.ScenarioName;
        JsonElement scenario = (JsonElement)parameters.Body;
        if (!(scenario.TryGetProperty("name"u8, out JsonElement bodyName) && bodyName.ValueEquals(name)))
        {
            return PutScenarioResult.BadRequest(
                Problem("scenario-name-mismatch", "Scenario name mismatch", 400, $"The body's name must be '{name}' (the path segment)."), workspace);
        }

        // Read-modify-write under the etag we read. The contract exposes no 409 here (a scenario
        // upsert is whole-entry replacement), so a concurrent save retries against fresh state.
        for (int attempt = 0; ; attempt++)
        {
            using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (workingCopy is not { } w)
            {
                return PutScenarioResult.NotFound(NotFoundProblem(id), workspace);
            }

            try
            {
                using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceScenarioJson.DraftUpserting((JsonElement)w.RootElement.Scenarios, scenario, name);
                using ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, w.RootElement.EtagValue, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
                if (saved is not { } s)
                {
                    return PutScenarioResult.NotFound(NotFoundProblem(id), workspace);
                }

                ParsedJsonDocument<Models.PutWorkspaceWorkflowsByIdScenariosByScenarioNameOk> body = WorkspaceScenarioJson.PutResponse(
                    WorkspaceScenarioJson.FindScenario((JsonElement)s.RootElement.Scenarios, name), s.RootElement.EtagValue.Value ?? string.Empty);
                workspace.TakeOwnership(body);
                return PutScenarioResult.Ok(body.RootElement, workspace);
            }
            catch (WorkspaceWorkflowConflictException) when (attempt < 3)
            {
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteScenarioResult> HandleDeleteScenarioAsync(DeleteScenarioParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.ScenarioName;
        for (int attempt = 0; ; attempt++)
        {
            using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (workingCopy is not { } w)
            {
                return DeleteScenarioResult.NotFound(NotFoundProblem(id), workspace);
            }

            if (WorkspaceScenarioJson.FindScenario((JsonElement)w.RootElement.Scenarios, name).ValueKind != JsonValueKind.Object)
            {
                return DeleteScenarioResult.NotFound(
                    Problem("scenario-not-found", "Scenario not found", 404, $"Working copy '{id}' has no scenario named '{name}'."), workspace);
            }

            try
            {
                using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceScenarioJson.DraftRemoving((JsonElement)w.RootElement.Scenarios, name);
                using ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, w.RootElement.EtagValue, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
                return saved is { } ? DeleteScenarioResult.NoContent() : DeleteScenarioResult.NotFound(NotFoundProblem(id), workspace);
            }
            catch (WorkspaceWorkflowConflictException) when (attempt < 3)
            {
            }
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RunScenarioResult> HandleRunScenarioAsync(RunScenarioParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.ScenarioName;
        if (this.simulator is null)
        {
            return RunScenarioResult.BadRequest(
                Problem("simulation-not-offered", "Simulation not offered", 400, "This deployment does not offer workflow simulation."), workspace);
        }

        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return RunScenarioResult.NotFound(NotFoundProblem(id), workspace);
        }

        JsonElement scenario = WorkspaceScenarioJson.FindScenario((JsonElement)w.RootElement.Scenarios, name);
        if (scenario.ValueKind != JsonValueKind.Object)
        {
            return RunScenarioResult.NotFound(
                Problem("scenario-not-found", "Scenario not found", 404, $"Working copy '{id}' has no scenario named '{name}'."), workspace);
        }

        (SimulationResult result, List<ScenarioSuite.Verdict> verdicts) = await this.RunOneScenarioAsync(w, scenario, cancellationToken).ConfigureAwait(false);
        using (result)
        {
            ParsedJsonDocument<Models.ScenarioRunResult> body = PersistedJson.ToPooledDocument<Models.ScenarioRunResult, (string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>(
                (name, result, verdicts),
                static (Utf8JsonWriter writer, in (string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts) s) =>
                    ScenarioSuite.WriteRunResult(writer, s.Name, s.Result, s.Verdicts));
            workspace.TakeOwnership(body);
            return RunScenarioResult.Ok(body.RootElement, workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RunAllScenariosResult> HandleRunAllScenariosAsync(RunAllScenariosParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        if (this.simulator is null)
        {
            return RunAllScenariosResult.BadRequest(
                Problem("simulation-not-offered", "Simulation not offered", 400, "This deployment does not offer workflow simulation."), workspace);
        }

        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return RunAllScenariosResult.NotFound(NotFoundProblem(id), workspace);
        }

        var runs = new List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>();
        try
        {
            JsonElement scenarios = (JsonElement)w.RootElement.Scenarios;
            if (scenarios.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement scenario in scenarios.EnumerateArray())
                {
                    if (scenario.TryGetProperty("name"u8, out JsonElement n) && n.GetString() is { Length: > 0 } scenarioName)
                    {
                        (SimulationResult result, List<ScenarioSuite.Verdict> verdicts) = await this.RunOneScenarioAsync(w, scenario, cancellationToken).ConfigureAwait(false);
                        runs.Add((scenarioName, result, verdicts));
                    }
                }
            }

            ParsedJsonDocument<Models.ScenarioSuiteReport> body = PersistedJson.ToPooledDocument<Models.ScenarioSuiteReport, List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>>(
                runs,
                static (Utf8JsonWriter writer, in List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)> all) =>
                {
                    int passed = 0;
                    foreach ((string _, SimulationResult _, List<ScenarioSuite.Verdict> verdicts) in all)
                    {
                        bool ok = true;
                        foreach (ScenarioSuite.Verdict v in verdicts)
                        {
                            ok &= v.Passed;
                        }

                        if (ok)
                        {
                            passed++;
                        }
                    }

                    writer.WriteStartObject();
                    writer.WriteNumber("total"u8, all.Count);
                    writer.WriteNumber("passed"u8, passed);
                    writer.WriteNumber("failed"u8, all.Count - passed);
                    writer.WriteStartArray("results"u8);
                    foreach ((string name, SimulationResult result, List<ScenarioSuite.Verdict> verdicts) in all)
                    {
                        ScenarioSuite.WriteRunResult(writer, name, result, verdicts);
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                });
            workspace.TakeOwnership(body);
            return RunAllScenariosResult.Ok(body.RootElement, workspace);
        }
        finally
        {
            foreach ((string _, SimulationResult result, List<ScenarioSuite.Verdict> _) in runs)
            {
                result.Dispose();
            }
        }
    }

    /// <summary>
    /// The deliberate publish act (design §4.6): the validation gate, the server-attested scenario
    /// suite, the evidence record, the package embedding metadata/scenarios.json +
    /// metadata/evidence.json, and the catalog add. The new version starts as a draft.
    /// </summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The new version's summary, or the refusal.</returns>
    public async ValueTask<PublishWorkingCopyResult> HandlePublishWorkingCopyAsync(PublishWorkingCopyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        if (this.catalog is null)
        {
            return PublishWorkingCopyResult.BadRequest(
                Problem("catalog-unavailable", "Catalog unavailable", 400, "This deployment does not offer publishing."), workspace);
        }

        Models.PostWorkspaceWorkflowsByIdPublishBody body = parameters.Body;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return PublishWorkingCopyResult.NotFound(NotFoundProblem(id), workspace);
        }

        // 1 — the validation gate: any error refuses the publish with the diagnostics.
        var document = (JsonElement)w.RootElement.Document;
        List<Finding> findings = await this.CollectDiagnosticsAsync(document, (JsonElement)w.RootElement.Sources, cancellationToken).ConfigureAwait(false);
        bool invalid = false;
        foreach (Finding finding in findings)
        {
            invalid |= finding.Severity == "error";
        }

        if (invalid)
        {
            ParsedJsonDocument<Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity> refusal =
                PersistedJson.ToPooledDocument<Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity, List<Finding>>(
                    findings,
                    static (Utf8JsonWriter writer, in List<Finding> all) =>
                    {
                        writer.WriteStartObject();
                        writer.WriteString("reason"u8, "validation"u8);
                        writer.WriteStartArray("diagnostics"u8);
                        foreach (Finding f in all)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("severity"u8, f.Severity);
                            writer.WriteString("category"u8, f.Category);
                            writer.WriteString("instancePath"u8, f.InstancePath);
                            writer.WriteString("message"u8, f.Message);
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                        writer.WriteEndObject();
                    });
            workspace.TakeOwnership(refusal);
            return PublishWorkingCopyResult.UnprocessableEntity(refusal.RootElement, workspace);
        }

        // 2 — the server-attested suite: every stored scenario replays here; a client cannot submit evidence.
        JsonElement scenarios = (JsonElement)w.RootElement.Scenarios;
        var runs = new List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>();
        try
        {
            if (scenarios.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement scenario in scenarios.EnumerateArray())
                {
                    if (scenario.TryGetProperty("name"u8, out JsonElement n) && n.GetString() is { Length: > 0 } scenarioName)
                    {
                        if (this.simulator is null)
                        {
                            return PublishWorkingCopyResult.BadRequest(
                                Problem("simulation-not-offered", "Simulation not offered", 400, "The working copy carries scenarios but this deployment cannot attest them (no simulator wired)."), workspace);
                        }

                        (SimulationResult result, List<ScenarioSuite.Verdict> verdicts) = await this.RunOneScenarioAsync(w, scenario, cancellationToken).ConfigureAwait(false);
                        runs.Add((scenarioName, result, verdicts));
                    }
                }
            }

            bool anyFailed = false;
            foreach ((string _, SimulationResult _, List<ScenarioSuite.Verdict> verdicts) in runs)
            {
                foreach (ScenarioSuite.Verdict v in verdicts)
                {
                    anyFailed |= !v.Passed;
                }
            }

            bool requireScenarios = !(body.RequireScenarios.IsNotUndefined() && !(bool)body.RequireScenarios);
            if (anyFailed && requireScenarios)
            {
                ParsedJsonDocument<Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity> refusal =
                    PersistedJson.ToPooledDocument<Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity, List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)>>(
                        runs,
                        static (Utf8JsonWriter writer, in List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)> all) =>
                        {
                            int passed = 0;
                            foreach ((string _, SimulationResult _, List<ScenarioSuite.Verdict> verdicts) in all)
                            {
                                bool ok = true;
                                foreach (ScenarioSuite.Verdict v in verdicts)
                                {
                                    ok &= v.Passed;
                                }

                                if (ok)
                                {
                                    passed++;
                                }
                            }

                            writer.WriteStartObject();
                            writer.WriteString("reason"u8, "scenarios"u8);
                            writer.WriteStartObject("suite"u8);
                            writer.WriteNumber("total"u8, all.Count);
                            writer.WriteNumber("passed"u8, passed);
                            writer.WriteNumber("failed"u8, all.Count - passed);
                            writer.WriteStartArray("results"u8);
                            foreach ((string name, SimulationResult result, List<ScenarioSuite.Verdict> verdicts) in all)
                            {
                                ScenarioSuite.WriteRunResult(writer, name, result, verdicts);
                            }

                            writer.WriteEndArray();
                            writer.WriteEndObject();
                            writer.WriteEndObject();
                        });
                workspace.TakeOwnership(refusal);
                return PublishWorkingCopyResult.UnprocessableEntity(refusal.RootElement, workspace);
            }

            // 3 — the package: content first (the hash canonicalises only {workflow, sources}), then the
            //     evidence carrying that hash, then the final package embedding both metadata entries.
            byte[] documentBytes = WorkspaceSimulationJson.DocumentBytes(document, null)!;
            List<KeyValuePair<string, byte[]>> sourceBytes = await WorkspaceSimulationJson.SourceBytesAsync(
                (JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);
            string hash = CatalogPackage.Validate(CatalogPackage.Build(documentBytes, sourceBytes)).Hash ?? string.Empty;
            byte[] scenariosBytes = scenarios.ValueKind == JsonValueKind.Array
                ? PersistedJson.ToArray(scenarios, static (Utf8JsonWriter writer, in JsonElement sc) => sc.WriteTo(writer))
                : [];
            byte[] evidenceBytes = PersistedJson.ToArray(
                (Hash: hash, At: this.timeProvider.GetUtcNow(), Runs: runs),
                static (Utf8JsonWriter writer, in (string Hash, DateTimeOffset At, List<(string Name, SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)> Runs) s) =>
                {
                    int passed = 0;
                    foreach ((string _, SimulationResult _, List<ScenarioSuite.Verdict> verdicts) in s.Runs)
                    {
                        bool ok = true;
                        foreach (ScenarioSuite.Verdict v in verdicts)
                        {
                            ok &= v.Passed;
                        }

                        if (ok)
                        {
                            passed++;
                        }
                    }

                    writer.WriteStartObject();
                    writer.WriteString("packageHash"u8, s.Hash);
                    writer.WriteString("engineVersion"u8, typeof(IWorkflowRun).Assembly.GetName().Version?.ToString() ?? "unknown");
                    writer.WriteString("at"u8, s.At);
                    writer.WriteStartObject("suite"u8);
                    writer.WriteNumber("total"u8, s.Runs.Count);
                    writer.WriteNumber("passed"u8, passed);
                    writer.WriteNumber("failed"u8, s.Runs.Count - passed);
                    writer.WriteEndObject();
                    writer.WriteStartArray("scenarios"u8);
                    foreach ((string name, SimulationResult result, List<ScenarioSuite.Verdict> verdicts) in s.Runs)
                    {
                        bool ok = true;
                        foreach (ScenarioSuite.Verdict v in verdicts)
                        {
                            ok &= v.Passed;
                        }

                        writer.WriteStartObject();
                        writer.WriteString("name"u8, name);
                        writer.WriteBoolean("passed"u8, ok);
                        writer.WriteString("outcome"u8, ScenarioSuite.OutcomeName(result.Outcome));
                        var visited = new List<string>();
                        foreach (SimulatedStepRecord record in result.Steps)
                        {
                            visited.Add(record.StepId);
                        }

                        writer.WriteString("pathSummary"u8, string.Join(" → ", visited));
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                });
            byte[] package = CatalogPackage.Build(documentBytes, sourceBytes, scenariosBytes, evidenceBytes);

            JsonElement ownerElement = (JsonElement)body.Owner;
            var owner = new CatalogOwner(
                ownerElement.TryGetProperty("name"u8, out JsonElement on) ? on.GetString() ?? string.Empty : string.Empty,
                ownerElement.TryGetProperty("email"u8, out JsonElement oe) ? oe.GetString() ?? string.Empty : string.Empty,
                ownerElement.TryGetProperty("team"u8, out JsonElement ot) ? ot.GetString() : null,
                ownerElement.TryGetProperty("url"u8, out JsonElement ou) ? ou.GetString() : null);
            TagSet tags = TagSet.CopyFrom(body.Tags);
            SecurityTagSet securityTags = ArazzoControlPlaneCatalogHandler.CombineSecurityTags(this.access.InternalTags(), default);

            try
            {
                ParsedJsonDocument<CatalogVersion> version = await this.catalog.AddAsync(package, owner, tags, securityTags, cancellationToken).ConfigureAwait(false);
                workspace.TakeOwnership(version);
                return PublishWorkingCopyResult.Created(Models.CatalogVersionSummary.From(this.access.PublicView(version.RootElement, workspace)), workspace);
            }
            catch (ArgumentException ex)
            {
                return PublishWorkingCopyResult.BadRequest(
                    Problem("not-publishable", "Not publishable", 400, ex.Message), workspace);
            }
        }
        finally
        {
            foreach ((string _, SimulationResult result, List<ScenarioSuite.Verdict> _) in runs)
            {
                result.Dispose();
            }
        }
    }

    /// <summary>The three validation passes (schema conformance, document-local semantics, workspace source
    /// integrity), shared by <c>validate</c> and the publish gate.</summary>
    private async ValueTask<List<Finding>> CollectDiagnosticsAsync(JsonElement document, JsonElement attachments, CancellationToken cancellationToken)
    {
        var findings = new List<Finding>();

        // 1 — JSON-Schema conformance against the Arazzo meta-schema the document declares.
        using (JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed))
        {
            ArazzoMetaSchema.For(document).Validate(in document, collector);
            foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
            {
                if (!result.IsMatch)
                {
                    findings.Add(new("error", "schema", result.GetDocumentEvaluationLocationText(), result.GetMessageText(), result.GetSchemaEvaluationLocationText()));
                    if (findings.Count >= MaxValidationErrors)
                    {
                        break;
                    }
                }
            }
        }

        // 2 — document-local semantic checks (the compiler's rules, collected instead of thrown).
        foreach (WorkflowDocumentDiagnostic diagnostic in WorkflowDocumentAnalyzer.Analyze(in document))
        {
            findings.Add(new(
                diagnostic.Severity switch
                {
                    WorkflowDocumentDiagnosticSeverity.Error => "error",
                    WorkflowDocumentDiagnosticSeverity.Warning => "warning",
                    _ => "info",
                },
                diagnostic.Category,
                diagnostic.InstancePath,
                diagnostic.Message,
                SchemaLocation: null));
        }

        // 3 — workspace-level source integrity: the document's operation bindings cross-checked
        //     against the working copy's ATTACHED sources. Document-local passes cannot see the
        //     attachments, so "delete a declared source that steps still use" surfaces HERE.
        await this.CheckSourceIntegrityAsync(document, attachments, findings, cancellationToken).ConfigureAwait(false);

        return findings;
    }

    /// <summary>Runs one stored scenario against the simulator and judges its expectations.</summary>
    private async ValueTask<(SimulationResult Result, List<ScenarioSuite.Verdict> Verdicts)> RunOneScenarioAsync(ParsedJsonDocument<WorkspaceWorkflow> w, JsonElement scenario, CancellationToken cancellationToken)
    {
        Dictionary<(string Source, string OperationId), (string Method, string Path)> routes =
            await WorkspaceScenarioJson.ResolveRoutesAsync((JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);
        byte[] documentBytes = WorkspaceSimulationJson.DocumentBytes((JsonElement)w.RootElement.Document, null)!;
        List<KeyValuePair<string, byte[]>> sourceBytes = await WorkspaceSimulationJson.SourceBytesAsync(
            (JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);
        SimulationResult result = await this.simulator!.SimulateAsync(
            documentBytes, sourceBytes, ScenarioSuite.BuildScenario(scenario, routes), cancellationToken: cancellationToken).ConfigureAwait(false);
        return (result, ScenarioSuite.Evaluate(scenario, result));
    }

    /// <inheritdoc/>
    public async ValueTask<DetachWorkingCopySourceResult> HandleDetachWorkingCopySourceAsync(DetachWorkingCopySourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.Name;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return DetachWorkingCopySourceResult.NotFound(NotFoundProblem(id), workspace);
        }

        if (WorkspaceSourceJson.FindAttachment((JsonElement)w.RootElement.Sources, name).ValueKind != JsonValueKind.Object)
        {
            return DetachWorkingCopySourceResult.NotFound(AttachmentNotFoundProblem(id, name), workspace);
        }

        try
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceSourceJson.DraftRemovingAttachment((JsonElement)w.RootElement.Sources, name);
            using ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, w.RootElement.EtagValue, this.actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
            return saved is not null
                ? DetachWorkingCopySourceResult.NoContent()
                : DetachWorkingCopySourceResult.NotFound(NotFoundProblem(id), workspace);
        }
        catch (WorkspaceWorkflowConflictException ex)
        {
            return DetachWorkingCopySourceResult.Conflict(
                Problem("save-conflict", "Save conflict", 409, ex.Message + " Retry the detach against the fresh state."), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ListWorkingCopySourceOperationsResult> HandleListWorkingCopySourceOperationsAsync(ListWorkingCopySourceOperationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.Name;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return ListWorkingCopySourceOperationsResult.NotFound(NotFoundProblem(id), workspace);
        }

        JsonElement attachment = WorkspaceSourceJson.FindAttachment((JsonElement)w.RootElement.Sources, name);
        if (attachment.ValueKind != JsonValueKind.Object)
        {
            return ListWorkingCopySourceOperationsResult.NotFound(AttachmentNotFoundProblem(id, name), workspace);
        }

        // An inline attachment projects its stored document; a registry attachment re-resolves the
        // registered source at read time (reach-checked, non-disclosing). The projection writes straight
        // through into the pooled response body while the source is alive; the pooled source may then
        // dispose (the response owns its own pooled buffer).
        ParsedJsonDocument<Models.OperationSurface> body;
        if (attachment.TryGetProperty("document"u8, out JsonElement inlineDocument))
        {
            body = WorkspaceSourceJson.OperationSurfaceResponse(inlineDocument);
        }
        else
        {
            string? sourceName = attachment.TryGetProperty("sourceName"u8, out JsonElement sn) && sn.ValueKind == JsonValueKind.String ? sn.GetString() : null;
            if (sourceName is null || this.sources is null)
            {
                return ListWorkingCopySourceOperationsResult.NotFound(AttachmentNotFoundProblem(id, name), workspace);
            }

            using ParsedJsonDocument<RegisteredSource>? registered = await this.sources.GetAsync(sourceName, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (registered is not { } r)
            {
                return ListWorkingCopySourceOperationsResult.NotFound(
                    Problem("source-not-found", "Source not found", 404, $"The attachment references registered source '{sourceName}', which no longer exists or is outside your reach."), workspace);
            }

            body = WorkspaceSourceJson.OperationSurfaceResponse((JsonElement)r.RootElement.Document);
        }

        workspace.TakeOwnership(body);
        return ListWorkingCopySourceOperationsResult.Ok(body.RootElement, workspace);
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

    // ── diagnostics projection (schema + semantic findings → the wire model) ──────────────────────
    private static void BuildFindings(in List<Finding> findings, ref Models.WorkingCopyDiagnostics.WorkflowDiagnosticArray.Builder array)
    {
        foreach (Finding finding in findings)
        {
            array.AddItem(Models.WorkflowDiagnostic.Build(in finding, BuildFinding));
        }
    }

    private static void BuildFinding(in Finding finding, ref Models.WorkflowDiagnostic.Builder b)
        => b.Create(
            severity: finding.Severity,
            category: finding.Category,
            instancePath: finding.InstancePath,
            message: finding.Message,
            schemaLocation: finding.SchemaLocation is { } schemaLocation ? schemaLocation : default(Models.JsonString.Source));

    // One positioned finding, from either pass (SchemaLocation only on schema-conformance findings).
    private readonly record struct Finding(string Severity, string Category, string InstancePath, string Message, string? SchemaLocation);

    /// <summary>The embedded Arazzo meta-schemas, compiled once and picked by the document's declared <c>arazzo</c> version.</summary>
    private static class ArazzoMetaSchema
    {
        private static readonly Lazy<ValidatorSchema> Arazzo10 = new(() => Load("Arazzo10", "https://spec.openapis.org/arazzo/1.0/schema/embedded"));
        private static readonly Lazy<ValidatorSchema> Arazzo11 = new(() => Load("Arazzo11", "https://spec.openapis.org/arazzo/1.1/schema/embedded"));

        public static ValidatorSchema For(in Corvus.Text.Json.JsonElement document)
        {
            // A 1.0.x declaration validates against the 1.0 schema; everything else (1.1.x, absent,
            // malformed) uses 1.1 — the schema pass itself reports a bad/missing arazzo field.
            bool isV10 = document.ValueKind == JsonValueKind.Object
                && document.TryGetProperty("arazzo"u8, out Corvus.Text.Json.JsonElement version)
                && version.ValueKind == JsonValueKind.String
                && version.GetString() is { } v
                && v.StartsWith("1.0", StringComparison.Ordinal);
            return isV10 ? Arazzo10.Value : Arazzo11.Value;
        }

        private static ValidatorSchema Load(string name, string canonicalUri)
        {
            using Stream stream = typeof(ArazzoMetaSchema).Assembly.GetManifestResourceStream($"Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Schemas.{name}.json")
                ?? throw new InvalidOperationException($"The embedded Arazzo meta-schema '{name}' is missing.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return ValidatorSchema.FromText(reader.ReadToEnd(), canonicalUri);
        }
    }

    /// <summary>Collects an attached source document's operation identities (OpenAPI operationIds, AsyncAPI operation names and channel keys).</summary>
    private static void CollectOperationIdentities(in JsonElement sourceDocument, HashSet<string> operations, HashSet<string> channels)
    {
        if (sourceDocument.TryGetProperty("paths"u8, out JsonElement paths) && paths.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> path in paths.EnumerateObject())
            {
                if (path.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty<JsonElement> operation in path.Value.EnumerateObject())
                {
                    if (operation.Value.ValueKind == JsonValueKind.Object
                        && operation.Value.TryGetProperty("operationId"u8, out JsonElement id)
                        && id.GetString() is { Length: > 0 } value)
                    {
                        operations.Add(value);
                    }
                }
            }
        }

        if (sourceDocument.TryGetProperty("channels"u8, out JsonElement channelMap) && channelMap.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> channel in channelMap.EnumerateObject())
            {
                using UnescapedUtf8JsonString name = channel.Utf8NameSpan;
                channels.Add(Encoding.UTF8.GetString(name.Span));
            }
        }

        if (sourceDocument.TryGetProperty("operations"u8, out JsonElement asyncOps) && asyncOps.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> operation in asyncOps.EnumerateObject())
            {
                using UnescapedUtf8JsonString name = operation.Utf8NameSpan;
                operations.Add(Encoding.UTF8.GetString(name.Span));
            }
        }
    }

    /// <summary>
    /// The workspace-level pass over declared sourceDescriptions vs the working copy's attachments:
    /// declared-but-unattached (warning — operations cannot resolve), attached-but-undeclared
    /// (info), operation bindings absent from every resolvable surface (warning), and steps binding
    /// operations with no sourceDescriptions at all (error).
    /// </summary>
    private async ValueTask CheckSourceIntegrityAsync(JsonElement document, JsonElement attachments, List<Finding> findings, CancellationToken cancellationToken)
    {
        var declared = new List<string>();
        if (document.TryGetProperty("sourceDescriptions"u8, out JsonElement sourceDescriptions) && sourceDescriptions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement source in sourceDescriptions.EnumerateArray())
            {
                if (source.TryGetProperty("name"u8, out JsonElement name) && name.GetString() is { Length: > 0 } value)
                {
                    declared.Add(value);
                }
            }
        }

        var attachedNames = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        var channels = new HashSet<string>(StringComparer.Ordinal);
        bool anySurfaceResolved = false;
        if (attachments.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement attachment in attachments.EnumerateArray())
            {
                if (!attachment.TryGetProperty("name"u8, out JsonElement name) || name.GetString() is not { Length: > 0 } attachedName)
                {
                    continue;
                }

                attachedNames.Add(attachedName);
                if (!declared.Contains(attachedName))
                {
                    findings.Add(new("info", "workspace-sources", "/sourceDescriptions", $"Attached source '{attachedName}' is not declared in sourceDescriptions — the document cannot reference its operations.", null));
                }

                if (attachment.TryGetProperty("document"u8, out JsonElement inline) && inline.ValueKind == JsonValueKind.Object)
                {
                    CollectOperationIdentities(inline, operations, channels);
                    anySurfaceResolved = true;
                }
                else if (this.sources is not null
                    && attachment.TryGetProperty("sourceName"u8, out JsonElement sn)
                    && sn.GetString() is { Length: > 0 } registryName)
                {
                    using ParsedJsonDocument<RegisteredSource>? registered = await this.sources.GetAsync(registryName, this.access.Current(), cancellationToken).ConfigureAwait(false);
                    if (registered is { } r)
                    {
                        CollectOperationIdentities((JsonElement)r.RootElement.Document, operations, channels);
                        anySurfaceResolved = true;
                    }
                }
            }
        }

        for (int i = 0; i < declared.Count; i++)
        {
            if (!attachedNames.Contains(declared[i]))
            {
                findings.Add(new("warning", "workspace-sources", $"/sourceDescriptions/{i}", $"Declared source '{declared[i]}' has no attachment in this working copy — its operations cannot resolve here.", null));
            }
        }

        if (!document.TryGetProperty("workflows"u8, out JsonElement workflows) || workflows.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        int workflowIndex = -1;
        foreach (JsonElement workflow in workflows.EnumerateArray())
        {
            workflowIndex++;
            if (!workflow.TryGetProperty("steps"u8, out JsonElement steps) || steps.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            int stepIndex = -1;
            foreach (JsonElement step in steps.EnumerateArray())
            {
                stepIndex++;
                bool bindsOperation = step.TryGetProperty("operationId"u8, out JsonElement operationId)
                    && operationId.GetString() is { Length: > 0 };
                bool bindsChannel = step.TryGetProperty("channelPath"u8, out JsonElement channelPath)
                    && channelPath.GetString() is { Length: > 0 };
                if (!bindsOperation && !bindsChannel)
                {
                    continue;
                }

                if (declared.Count == 0)
                {
                    findings.Add(new("error", "workspace-sources", "/sourceDescriptions", "Steps bind operations but the document declares no sourceDescriptions — nothing can resolve them.", null));
                    return; // one document-level error says it all
                }

                if (!anySurfaceResolved)
                {
                    continue; // no surface to check against; the declared-but-unattached warnings already fired
                }

                if (bindsOperation)
                {
                    string op = operationId.GetString()!;
                    if (!op.StartsWith("$sourceDescriptions.", StringComparison.Ordinal) && !operations.Contains(op))
                    {
                        findings.Add(new("warning", "workspace-sources", $"/workflows/{workflowIndex}/steps/{stepIndex}/operationId", $"Operation '{op}' is not found in any attached source — did its source description get removed?", null));
                    }
                }

                if (bindsChannel)
                {
                    string channel = channelPath.GetString()!;
                    if (!channels.Contains(channel))
                    {
                        findings.Add(new("warning", "workspace-sources", $"/workflows/{workflowIndex}/steps/{stepIndex}/channelPath", $"Channel '{channel}' is not found in any attached source.", null));
                    }
                }
            }
        }
    }

    // ── problem documents ──────────────────────────────────────────────────────────────────────────────────────────
    private static Models.ProblemDetails.Source AttachmentNotFoundProblem(string id, string name)
        => Problem("attachment-not-found", "Attachment not found", 404, $"Working copy '{id}' has no source attached as '{name}'.");

    private static Models.ProblemDetails.Source NotFoundProblem(string id)
        => Problem("working-copy-not-found", "Working copy not found", 404, $"No working copy '{id}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}