// <copyright file="ArazzoControlPlaneWorkspaceHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Internal;
using Microsoft.Extensions.Logging;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;
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
public sealed class ArazzoControlPlaneWorkspaceHandler : IApiWorkspaceHandler, IApiDebugRunsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // The server-side bound on /count: the store stops one row past this, so a busy list renders "100+" (design §16.5).
    private const int CountCap = 100;
    private const int MaxValidationErrors = 200;

    private readonly IWorkspaceWorkflowStore store;
    private readonly ISecuredWorkflowCatalog? catalog;
    private readonly ISourceStore? sources;
    private readonly Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator? simulator;
    private readonly IEnvironmentStore? environments;
    private readonly ISourceCredentialStore? credentials;
    private readonly ControlPlaneAccess access;
    private readonly TimeProvider timeProvider;
    private readonly string actor;

    // §18 debug runs on the durable host (workflow-designer design §18 slice 3e-2c): a debug run IS the durable
    // $draft run the in-process runner executes — captured + enqueued through the DraftRunManagement, advanced by
    // driving the runner's recording+tracing resumer directly (the stepper sets a per-advance pause the dispatcher
    // cannot), its faulted-run remediation landing on the durable engine's native resume verbs. All four are needed
    // for debug runs to be offered; absent any of them the debug-run endpoints fail closed (DebugRunsNotOffered).
    private readonly IWorkflowStateStore? workflowStateStore;
    private readonly IDraftRunStore? draftRunStore;
    private readonly IDraftRunTraceStore? draftRunTraceStore;
    private readonly ISecuredWorkflowManagement? debugRunManagement;
    private readonly InProcessDraftRunner? draftRunner;
    private readonly DraftRunManagement? draftRunManagement;
    private readonly ILogger? auditLogger;

    // The audited resource kind for a debug-run lifecycle event (design §850, worklist item 8).
    private const string DebugRunTargetKind = "debug-run";

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
    /// <param name="environments">The environment store the §18 debug-run gate reads <c>allowsDraftRuns</c> from;
    /// debug runs fail closed when <see langword="null"/>.</param>
    /// <param name="credentials">The credential-binding store the §18 per-source readiness gate reads (references
    /// only — no secret material); debug runs fail closed when <see langword="null"/>.</param>
    /// <param name="workflowStateStore">The durable run store §18 debug runs are captured into and loaded from
    /// (workflow-designer design §18 slice 3e-2c); with <paramref name="draftRunStore"/> it also builds the
    /// <see cref="DraftRunManagement"/>. Debug runs fail closed when <see langword="null"/>.</param>
    /// <param name="draftRunStore">The sibling store the §18 draft capture (audit record + packed document/sources)
    /// is written to and read back from. Debug runs fail closed when <see langword="null"/>.</param>
    /// <param name="debugRunManagement">The run-management client the §18 debug-run reads (state), native
    /// faulted-run resume verbs, and cancel delegate to — constructed with the same recording+tracing resumer the
    /// <paramref name="draftRunner"/> exposes, so its native resume records+traces. Debug runs fail closed when
    /// <see langword="null"/>.</param>
    /// <param name="draftRunner">The optional in-process, environment-pinned draft-run runner the §18 stepper drives
    /// each advance through (its <see cref="InProcessDraftRunner.Resumer"/>). Interactive debug runs require it (a
    /// paused run is not dispatch-claimable); debug runs fail closed when <see langword="null"/>.</param>
    /// <param name="draftRunTraceStore">The durable trace store §18 <c>get-debug-run</c> reads each run's assembled
    /// metadata trace from (§18 R4) — written by the runner, possibly in a different process. When <see langword="null"/>
    /// the trace is omitted from the debug-run view.</param>
    internal ArazzoControlPlaneWorkspaceHandler(IWorkspaceWorkflowStore store, ControlPlaneAccess access, ISecuredWorkflowCatalog? catalog = null, ISourceStore? sources = null, TimeProvider? timeProvider = null, string actor = "control-plane", Corvus.Text.Json.Arazzo.Testing.WorkflowSimulator? simulator = null, IEnvironmentStore? environments = null, ISourceCredentialStore? credentials = null, IWorkflowStateStore? workflowStateStore = null, IDraftRunStore? draftRunStore = null, ISecuredWorkflowManagement? debugRunManagement = null, InProcessDraftRunner? draftRunner = null, IDraftRunTraceStore? draftRunTraceStore = null, ILogger? auditLogger = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.store = store;
        this.access = access;
        this.catalog = catalog;
        this.sources = sources;
        this.simulator = simulator;
        this.environments = environments;
        this.credentials = credentials;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.actor = actor;
        this.workflowStateStore = workflowStateStore;
        this.draftRunStore = draftRunStore;
        this.draftRunTraceStore = draftRunTraceStore;
        this.debugRunManagement = debugRunManagement;
        this.draftRunner = draftRunner;

        // The capture-and-enqueue front end composes from the run store + the draft store (design §18 slice 3d);
        // build it once when both are wired so a debug-run start has it ready.
        this.draftRunManagement = workflowStateStore is not null && draftRunStore is not null
            ? new DraftRunManagement(workflowStateStore, draftRunStore, this.timeProvider)
            : null;
        this.auditLogger = auditLogger;
    }

    // The §850 audit subject for a debug-run event: the authenticated developer, falling back to the configured actor.
    private string AuditActor() => PrincipalDisplayName.Resolve(this.access.CurrentPrincipal) ?? this.actor;

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
    public async ValueTask<CountWorkspaceWorkflowsResult> HandleCountWorkspaceWorkflowsAsync(CountWorkspaceWorkflowsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The same §14.2 read reach as HandleListWorkspaceWorkflowsAsync (this.access.Current()), but the store returns
        // only a bounded total, never rows (design §16.5: list footers). CountCap bounds it so a busy list renders "100+".
        (int count, bool capped) = await this.store.CountAsync(this.access.Current(), CountCap, cancellationToken).ConfigureAwait(false);
        return CountWorkspaceWorkflowsResult.Ok(Models.CountResult.Build(capped: capped, count: count), workspace);
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
        IReadOnlyList<KeyValuePair<string, byte[]>>? carriedSources = null;
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

            // §18/§9: the version's referenced sources travel forward too, so a working copy opened from a version can
            // be simulated or DEBUG-RUN immediately, with no manual re-attach. They are attached inline after the create.
            carriedSources = WorkflowPackage.Open(ownedPackage).Sources;
        }

        try
        {
            // The whole create draft — name resolution (supplied / first workflowId / base id /
            // 'untitled'), the document (request bytes-to-bytes / carry-over / blank skeleton),
            // designer state, provenance, and tags — writes in ONE pooled pass.
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceSourceJson.CreateDraft(
                (JsonElement)body.Name, (JsonElement)body.Document, catalogDocumentUtf8, (JsonElement)body.DesignerState, baseWorkflowId, basedOnVersion, managementTags, carriedScenariosUtf8);
            ParsedJsonDocument<WorkspaceWorkflow> created = await this.store.AddAsync(draft.RootElement, this.actor, cancellationToken).ConfigureAwait(false);

            // §18/§9: carry the version's sources into the new working copy (inline), so it simulates / debug-runs
            // immediately with no manual re-attach. Each attaches under the running etag; the final copy is returned.
            if (carriedSources is { Count: > 0 } toAttach)
            {
                created = await this.AttachCarriedSourcesAsync(created, toAttach, cancellationToken).ConfigureAwait(false);
            }

            // The full working copy (document included) is congruent with the API model — a free whole-document re-wrap.
            workspace.TakeOwnership(created);
            return CreateWorkspaceWorkflowResult.Created(Models.WorkingCopy.From(created.RootElement), workspace);
        }
        catch (ArgumentException ex)
        {
            return CreateWorkspaceWorkflowResult.BadRequest(Problem("invalid-working-copy", "Invalid working copy", 400, ex.Message), workspace);
        }
    }

    // Attaches a set of carried-over sources (name → document bytes) inline onto a just-created working copy, one
    // etag-guarded write per source through the same DraftReplacingAttachment path an interactive attach uses. Takes
    // ownership of <paramref name="workingCopy"/> and returns the final (fully-attached) working copy for the caller
    // to own; intermediate documents are disposed.
    private async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AttachCarriedSourcesAsync(ParsedJsonDocument<WorkspaceWorkflow> workingCopy, IReadOnlyList<KeyValuePair<string, byte[]>> sources, CancellationToken cancellationToken)
    {
        string id = (string)workingCopy.RootElement.Id;
        AccessContext context = this.access.Current();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        ParsedJsonDocument<WorkspaceWorkflow> current = workingCopy;
        foreach (KeyValuePair<string, byte[]> source in sources)
        {
            using ParsedJsonDocument<JsonElement> sourceDocument = ParsedJsonDocument<JsonElement>.Parse(source.Value);
            string? type = WorkspaceSourceJson.DetectDocumentType(sourceDocument.RootElement);
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceSourceJson.DraftReplacingAttachment(
                (JsonElement)current.RootElement.Sources, source.Key, "inline", sourceName: null, (JsonElement)sourceDocument.RootElement, type, this.actor, now);
            ParsedJsonDocument<WorkspaceWorkflow>? saved = await this.store.UpdateAsync(id, draft.RootElement, current.RootElement.EtagValue, this.actor, context, cancellationToken).ConfigureAwait(false);
            current.Dispose();
            if (saved is not { } next)
            {
                // The just-created copy vanished mid-carry (should not happen); surface the best current state.
                return await this.store.GetAsync(id, context, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException($"Working copy '{id}' vanished during source carry-over.");
            }

            current = next;
        }

        return current;
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

        // The draft carries the new document (and optionally name/designer state/Git binding, §4.7)
        // bytes-to-bytes; the immutable provenance and tags are carried forward by the store from the
        // stored working copy.
        using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft(
            (JsonElement)body.Name,
            default,
            default,
            (JsonElement)body.Document,
            (JsonElement)body.DesignerState,
            default,
            SecurityTagSet.Empty,
            gitBinding: (JsonElement)body.GitBinding);
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

    /// <inheritdoc/>
    public async ValueTask<GetWorkingCopySchemasResult> HandleGetWorkingCopySchemasAsync(GetWorkingCopySchemasParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return GetWorkingCopySchemasResult.NotFound(NotFoundProblem(id), workspace);
        }

        // Recomputed for the CURRENT document + attachments (the catalog serves the copy baked at
        // add): the same generator, so the designer's typed forms see exactly what a published
        // version's consumers will.
        byte[] documentBytes = WorkspaceSimulationJson.DocumentBytes((JsonElement)w.RootElement.Document, null)!;
        List<KeyValuePair<string, byte[]>> sourceBytes = await WorkspaceSimulationJson.SourceBytesAsync(
            (JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);
        byte[] metadata = WorkflowSchemaMetadataGenerator.Generate(documentBytes, sourceBytes);
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(metadata);
        workspace.TakeOwnership(parsed);
        return GetWorkingCopySchemasResult.Ok(parsed.RootElement, workspace);
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
            // The generated contextful Create() builds the refusal in one pooled pass (the findings list is the build
            // context, closure-free; the response pipeline consumes the parsed document for validation/serialization).
            ParsedJsonDocument<Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity> refusal =
                Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity.Create(
                    findings,
                    reason: "validation",
                    diagnostics: Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity.WorkflowDiagnosticArray.Build(
                        findings,
                        static (in List<Finding> all, ref Models.PostWorkspaceWorkflowsByIdPublishUnprocessableEntity.WorkflowDiagnosticArray.Builder b) =>
                        {
                            foreach (Finding f in all)
                            {
                                b.AddItem(Models.WorkflowDiagnostic.Build(
                                    f,
                                    static (in Finding f, ref Models.WorkflowDiagnostic.Builder ib) =>
                                        ib.Create(category: f.Category, instancePath: f.InstancePath, message: f.Message, severity: f.Severity)));
                            }
                        }));
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

    /// <summary>The four validation passes (schema conformance, document-local semantics, workspace source
    /// integrity, and embedded inputs-schema meta-validation), shared by <c>validate</c> and the publish gate.</summary>
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
        //     attachments, so "delete a declared source that steps still use" surfaces HERE. Returns the
        //     jsonschema attachment names (they are not sourceDescriptions) for pass 4's external-$ref check.
        SchemaAttachmentIndex schemaAttachments = await this.CheckSourceIntegrityAsync(document, attachments, findings, cancellationToken).ConfigureAwait(false);

        // 4 — the inputs schemas the Arazzo meta-schema treats as opaque (each workflow.inputs and every
        //     components.inputs.*) meta-validated against JSON Schema 2020-12, plus a dangling $ref
        //     check (§6): a local components.inputs reference must exist, and an external schemas/<name>
        //     reference must name an attached jsonschema document. Pass 4 validates a SUB-element, so results
        //     come back schema-relative and are rebased under the schema's document location (pass 1 gets
        //     document-rooted paths for free).
        MetaValidateInputsSchemas(in document, schemaAttachments, findings);

        return findings;
    }

    /// <summary>Meta-validates each embedded inputs schema (workflow.inputs, components.inputs.*) against JSON
    /// Schema 2020-12 and flags dangling local component references — closing a real gap: nothing else validates
    /// the CONTENT of an inputs schema (the Arazzo meta-schema models it as just object-or-boolean).</summary>
    private static void MetaValidateInputsSchemas(in JsonElement document, SchemaAttachmentIndex schemaAttachments, List<Finding> findings)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (document.TryGetProperty("workflows"u8, out JsonElement workflows) && workflows.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (JsonElement workflow in workflows.EnumerateArray())
            {
                if (workflow.ValueKind == JsonValueKind.Object && workflow.TryGetProperty("inputs"u8, out JsonElement inputs))
                {
                    MetaValidateInputsSchema(in inputs, $"/workflows/{i}/inputs", in document, schemaAttachments, findings);
                }

                i++;
            }
        }

        if (document.TryGetProperty("components"u8, out JsonElement components) && components.ValueKind == JsonValueKind.Object
            && components.TryGetProperty("inputs"u8, out JsonElement inputsLibrary) && inputsLibrary.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> entry in inputsLibrary.EnumerateObject())
            {
                JsonElement value = entry.Value;
                MetaValidateInputsSchema(in value, $"/components/inputs/{EscapeJsonPointerSegment(entry.Name)}", in document, schemaAttachments, findings);
            }
        }
    }

    /// <summary>Meta-validates one inputs schema, rebasing each finding's schema-relative path under
    /// <paramref name="basePath"/>, then flags any dangling local component reference.</summary>
    private static void MetaValidateInputsSchema(in JsonElement schema, string basePath, in JsonElement document, SchemaAttachmentIndex schemaAttachments, List<Finding> findings)
    {
        if (findings.Count >= MaxValidationErrors)
        {
            return;
        }

        using (JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed))
        {
            InputsMetaSchema.Value.Validate(in schema, collector);
            foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
            {
                if (!result.IsMatch)
                {
                    findings.Add(new("error", "schema", basePath + result.GetDocumentEvaluationLocationText(), result.GetMessageText(), result.GetSchemaEvaluationLocationText()));
                    if (findings.Count >= MaxValidationErrors)
                    {
                        return;
                    }
                }
            }
        }

        CheckDanglingInputsReferences(in schema, basePath, in document, schemaAttachments, findings);
    }

    /// <summary>Flags a <c>#/components/inputs/&lt;name&gt;</c> reference whose target is absent, and a
    /// <c>schemas/&lt;name&gt;#&lt;pointer&gt;</c> external reference whose named jsonschema document is not attached
    /// to this working copy — nothing else detects either (meta-validation cannot, the analyzer does not, and the
    /// baked path silently degrades to raw).</summary>
    private static void CheckDanglingInputsReferences(in JsonElement node, string basePath, in JsonElement document, SchemaAttachmentIndex schemaAttachments, List<Finding> findings)
    {
        if (findings.Count >= MaxValidationErrors)
        {
            return;
        }

        if (node.ValueKind == JsonValueKind.Object)
        {
            if (node.TryGetProperty("$ref"u8, out JsonElement reference) && reference.ValueKind == JsonValueKind.String
                && reference.GetString() is { } target)
            {
                if (target.StartsWith("#/components/inputs/", StringComparison.Ordinal))
                {
                    string name = target["#/components/inputs/".Length..];
                    if (!InputsLibraryHasEntry(in document, name))
                    {
                        findings.Add(new("error", "schema", basePath, $"References the library schema '{name}', which does not exist under components.inputs.", null));
                    }
                }
                else if (target.StartsWith("schemas/", StringComparison.Ordinal))
                {
                    // The FALLBACK external form (design §6): schemas/<name>#<pointer> resolves against the working
                    // copy's jsonschema attachment named <name> (packaged alongside the sources at publish, registered
                    // with the generation resolver under the same relative URI).
                    int fragment = target.IndexOf('#', StringComparison.Ordinal);
                    string name = (fragment < 0 ? target : target[..fragment])["schemas/".Length..];
                    if (!schemaAttachments.Names.Contains(name))
                    {
                        findings.Add(new("error", "schema", basePath, $"References the external schema '{name}', which is not attached — attach a jsonschema source under that name.", null));
                    }
                }
                else if (target.StartsWith("https://", StringComparison.Ordinal) || target.StartsWith("http://", StringComparison.Ordinal))
                {
                    // The PREFERRED external form: an attached schema document's absolute root $id. Any other
                    // absolute reference cannot resolve at publish (the generator resolves only registered
                    // documents, never the network), so it is flagged here rather than failing the executor build.
                    int fragment = target.IndexOf('#', StringComparison.Ordinal);
                    string identity = fragment < 0 ? target : target[..fragment];
                    if (!schemaAttachments.Ids.Contains(identity))
                    {
                        findings.Add(new("error", "schema", basePath, $"References the external schema '{identity}', which is not attached — attach the jsonschema source whose $id this is.", null));
                    }
                }
            }

            foreach (JsonProperty<JsonElement> property in node.EnumerateObject())
            {
                JsonElement child = property.Value;
                if (child.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                {
                    CheckDanglingInputsReferences(in child, basePath, in document, schemaAttachments, findings);
                }
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in node.EnumerateArray())
            {
                CheckDanglingInputsReferences(in item, basePath, in document, schemaAttachments, findings);
            }
        }
    }

    // The resolvable identities of a working copy's jsonschema attachments: the attachment NAMES (the
    // schemas/<name> fallback form) and the absolute root $id each document declares (the preferred form).
    private sealed record SchemaAttachmentIndex(HashSet<string> Names, HashSet<string> Ids);

    // Reads a schema document's absolute root $id, when it declares one (a relative $id resolves against
    // the virtual retrieval URI, which the fallback name registration already covers).
    private static bool TryGetSchemaRootId(JsonElement schemaDocument, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? id)
    {
        id = null;
        if (schemaDocument.ValueKind == JsonValueKind.Object
            && schemaDocument.TryGetProperty("$id"u8, out JsonElement idElement)
            && idElement.ValueKind == JsonValueKind.String
            && idElement.GetString() is { Length: > 0 } value
            && Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            id = value;
            return true;
        }

        return false;
    }

    private static bool InputsLibraryHasEntry(in JsonElement document, string name)
    {
        return document.TryGetProperty("components"u8, out JsonElement components) && components.ValueKind == JsonValueKind.Object
            && components.TryGetProperty("inputs"u8, out JsonElement inputs) && inputs.ValueKind == JsonValueKind.Object
            && inputs.TryGetProperty(name, out _);
    }

    private static string EscapeJsonPointerSegment(string segment)
        => segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);

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

    /// <summary>Whether this deployment offers §18 debug runs: the durable draft-run path is fully wired (the run
    /// store, the draft-run store, the run-management client, and the in-process runner) AND the two start gates'
    /// stores are present (the environment store for <c>allowsDraftRuns</c>, the credential store for readiness).
    /// Interactive debug runs REQUIRE the in-process runner — a paused run is not dispatch-claimable, so the stepper
    /// drives the runner's resumer directly (workflow-designer design §18 slice 3e-2c) — so an unwired runner fails
    /// closed exactly as the interim simulator did.</summary>
    private bool DebugRunsOffered
        => this.draftRunManagement is not null
        && this.debugRunManagement is not null
        && this.draftRunner is not null
        && this.workflowStateStore is not null
        && this.draftRunStore is not null
        && this.environments is not null
        && this.credentials is not null;

    /// <summary>The fail-closed refusal when a debug-run operation is attempted on a deployment that does not wire
    /// the durable draft-run host (workflow-designer design §18).</summary>
    /// <param name="status">The status the failing operation answers with (400 on start; 404 elsewhere).</param>
    private static Models.ProblemDetails.Source DebugRunsNotOffered(int status = 400)
        => Problem("debug-runs-not-offered", "Debug runs not offered", status, "This deployment does not offer draft debug runs (workflow-designer design §18): it wires no in-process draft-run host.");

    private static Models.ProblemDetails.Source DebugRunNotFoundProblem()
        => Problem("debug-run-not-found", "Debug run not found", 404, "No such debug run exists for this working copy.");

    /// <summary>
    /// Starts a §18 debug run of the working copy's stored document in a development-class
    /// environment, behind the three deliberate gates: the environment's <c>allowsDraftRuns</c>
    /// flag (403), the caller's reach to the working copy and the environment (404,
    /// non-disclosing), and per-source credential readiness in that environment (409 naming the
    /// gaps). The run record carries the audit tuple (who, which working copy, which document
    /// etag, which environment, when). Execution is the durable host (workflow-designer design §18
    /// slice 3e-2c): the captured draft is enqueued as a Pending <c>$draft</c> run and driven to
    /// its first pause/completion/fault through the in-process runner against real transport.
    /// </summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The created run, or the gate's problem.</returns>
    public async ValueTask<StartDebugRunResult> HandleStartDebugRunAsync(StartDebugRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return StartDebugRunResult.BadRequest(DebugRunsNotOffered(), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return StartDebugRunResult.NotFound(NotFoundProblem(id), workspace);
        }

        Models.DebugRunStart body = parameters.Body;
        string environmentName = (string)body.Environment;

        // Gate 1 (§18): the environment must be within reach AND its administrators must have
        // deliberately opened it to drafts.
        using (ParsedJsonDocument<Environment>? environment = await this.environments!.GetAsync(environmentName, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (environment is not { } env)
            {
                return StartDebugRunResult.NotFound(
                    Problem("environment-not-found", "Environment not found", 404, $"No environment '{environmentName}' is visible to you."), workspace);
            }

            if (((JsonElement)env.RootElement.AllowsDraftRuns).ValueKind != JsonValueKind.True)
            {
                return StartDebugRunResult.Forbidden(
                    Problem("draft-runs-not-allowed", "Drafts may not run here", 403, $"Environment '{environmentName}' does not allow draft debug runs (workflow-designer design §18) — its administrators must set allowsDraftRuns."), workspace);
            }
        }

        // Gate 2 (§18): per-source credential readiness in the TARGET environment. The run dialog
        // surfaces the same coverage before the attempt; starting with gaps is a 409 naming them.
        JsonElement document = (JsonElement)w.RootElement.Document;
        List<string>? missing = null;
        if (document.TryGetProperty("sourceDescriptions"u8, out JsonElement declared) && declared.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement source in declared.EnumerateArray())
            {
                if (source.TryGetProperty("name"u8, out JsonElement sourceNameElement) && sourceNameElement.GetString() is { Length: > 0 } sourceName)
                {
                    using ParsedJsonDocument<SourceCredentialBinding>? binding = await this.credentials!.GetAsync(sourceName, environmentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
                    if (binding is null)
                    {
                        (missing ??= []).Add(sourceName);
                    }
                }
            }
        }

        if (missing is not null)
        {
            return StartDebugRunResult.Conflict(
                Problem("credentials-not-ready", "Credentials not ready", 409, $"No credential bound in '{environmentName}' for: {string.Join(", ", missing)}."), workspace);
        }

        string workflowId = (string)body.WorkflowId;
        byte[]? documentBytes = WorkspaceSimulationJson.DocumentBytes(document, workflowId);
        if (documentBytes is null)
        {
            return StartDebugRunResult.BadRequest(
                Problem("unknown-workflow", "Unknown workflow", 400, $"The document declares no workflow '{workflowId}'."), workspace);
        }

        List<KeyValuePair<string, byte[]>> sourceBytes = await WorkspaceSimulationJson.SourceBytesAsync(
            (JsonElement)w.RootElement.Sources, this.sources, this.access.Current(), cancellationToken).ConfigureAwait(false);

        // Capture + enqueue the durable $draft run (design §18 slice 3d): the audited record + the packed
        // {document (chosen workflow first), sources} land in the draft store, then a Pending $draft run —
        // env-pinned and sys:workingCopy-scoped (§14.2) — lands in the run store carrying the starter's ambient tags.
        var start = new DraftRunStart(
            WorkingCopyId: id,
            WorkflowId: workflowId,
            DocumentUtf8: documentBytes,
            Sources: sourceBytes,
            Environment: environmentName,
            DocumentEtag: (string)w.RootElement.Etag,
            StartedBy: this.actor);
        JsonElement inputs = body.Inputs.IsNotUndefined() ? (JsonElement)body.Inputs : default;
        SecurityTagSet securityTags = SecurityTagSet.FromTags(this.access.InternalTags());

        // §18 R5: enqueue a Pending run carrying the requested pause; a RUNNER (this process's pump, or a separate
        // process) claims and advances it. The control plane never executes. The step ids come from the captured
        // (chosen-first) document, so they map to cursor space.
        WorkflowPauseConfig? pause;
        using (ParsedJsonDocument<JsonElement> capturedDocument = ParsedJsonDocument<JsonElement>.Parse(documentBytes))
        {
            pause = TranslatePause(body.Pause, StepIds(capturedDocument.RootElement));
        }

        WorkflowRunId runId = await this.draftRunManagement!.StartAsync(start, inputs, securityTags, pause, cancellationToken: cancellationToken).ConfigureAwait(false);

        GovernanceAudit.Mutation(this.auditLogger, "debug-run.start", this.AuditActor(), DebugRunTargetKind, runId.Value, "started");

        ParsedJsonDocument<Models.DebugRun>? view = await this.BuildDebugRunViewAsync(runId, id, cancellationToken).ConfigureAwait(false);
        if (view is not { } created)
        {
            return StartDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        workspace.TakeOwnership(created);
        return StartDebugRunResult.Created(created.RootElement, workspace);
    }

    /// <summary>
    /// Reads a §18 debug run's current durable state (workflow-designer design §18 slice 3e-2c): the run's status
    /// and cursor from its authoritative checkpoint (reach-filtered, §14.2), plus the in-process runner's cached
    /// metadata trace. The <c>wait</c> long-poll param is best-effort — the run is advanced synchronously in-process,
    /// so the current state is returned without blocking.
    /// </summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run, or the problem.</returns>
    public async ValueTask<GetDebugRunResult> HandleGetDebugRunAsync(GetDebugRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return GetDebugRunResult.NotFound(DebugRunsNotOffered(404), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is null)
        {
            return GetDebugRunResult.NotFound(NotFoundProblem(id), workspace);
        }

        ParsedJsonDocument<Models.DebugRun>? view = await this.BuildDebugRunViewAsync(new WorkflowRunId((string)parameters.DebugRunId), id, cancellationToken).ConfigureAwait(false);
        if (view is not { } v)
        {
            return GetDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        workspace.TakeOwnership(v);
        return GetDebugRunResult.Ok(v.RootElement, workspace);
    }

    /// <summary>
    /// Advances a §18 debug run on the durable host (workflow-designer design §18 slice 3e-2c). A <c>pause</c>
    /// (afterEachStep / breakpoints) reconfigures the stop points for the next advance; a bare resume clears the
    /// pause-hold and continues — both driven directly through the in-process runner. A <c>ResumeRequest</c>
    /// <c>action</c> is FAULT REMEDIATION on the durable engine's native resume verbs (retry / skip past the faulted
    /// step / rewind / state-patch), which act only on a faulted run; step-over of a non-faulted paused step is a
    /// replay-debugger operation (slice 3e-3), not a live-run one.
    /// </summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The advanced run, or the problem.</returns>
    public async ValueTask<ResumeDebugRunResult> HandleResumeDebugRunAsync(ResumeDebugRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return ResumeDebugRunResult.NotFound(DebugRunsNotOffered(404), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is null)
        {
            return ResumeDebugRunResult.NotFound(NotFoundProblem(id), workspace);
        }

        var runId = new WorkflowRunId((string)parameters.DebugRunId);
        if (!await this.RunBelongsToWorkingCopyAsync(runId, id, cancellationToken).ConfigureAwait(false))
        {
            return ResumeDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        WorkflowRunDetail? detailOpt = await this.debugRunManagement!.GetAsync(runId, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (detailOpt is not { } detail)
        {
            return ResumeDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        if (detail.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled)
        {
            return ResumeDebugRunResult.Conflict(
                Problem("not-resumable", "Not resumable", 409, $"The debug run is {MapStatus(detail.Status, detail.Wait)}."), workspace);
        }

        Models.DebugRunResume body = parameters.Body;
        if (body.Action.IsNotUndefined())
        {
            // §18 R5b: fault remediation on the durable engine's NATIVE resume verbs, MARK-CLAIMABLE — the control
            // plane applies the mutation and marks the run resume-claimable; a runner re-executes it and persists a
            // fresh trace. The control plane never executes.
            ResumeOptions options = ArazzoControlPlaneHandler.ToResumeOptions(body.Action);
            bool applied = await this.debugRunManagement.RequestFaultedResumeAsync(runId, options, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (!applied)
            {
                return ResumeDebugRunResult.Conflict(
                    Problem("resume-not-applied", "Resume not applied", 409, "The resume verb did not apply: the debug run is not faulted, the state patch did not apply to its context { \"inputs\": …, \"stepOutputs\": { … } }, or the run changed concurrently. Live-run resume verbs are fault remediation; step-over of a paused step is a replay-debugger action."), workspace);
            }
        }
        else
        {
            // §18 R5: a pause reconfiguration for the next advance, or a bare resume that continues — the control
            // plane MARKS the run resume-claimable with the requested pause (a bare resume clears the pause-hold);
            // a runner claims and advances it. The control plane never executes.
            WorkflowPauseConfig? pause = body.Pause.IsNotUndefined()
                ? TranslatePause(body.Pause, await this.CapturedStepIdsAsync(runId, cancellationToken).ConfigureAwait(false))
                : null;
            await this.MarkResumeClaimableAsync(runId, pause, cancellationToken).ConfigureAwait(false);
        }

        ParsedJsonDocument<Models.DebugRun>? view = await this.BuildDebugRunViewAsync(runId, id, cancellationToken).ConfigureAwait(false);
        if (view is not { } v)
        {
            return ResumeDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        workspace.TakeOwnership(v);
        GovernanceAudit.Mutation(this.auditLogger, "debug-run.resume", this.AuditActor(), DebugRunTargetKind, runId.Value, "resumed");
        return ResumeDebugRunResult.Ok(v.RootElement, workspace);
    }

    /// <summary>Debug-only (workflow-designer design §18 / §3.3): delivers a message to a debug run suspended on an
    /// AsyncAPI receive, standing in for the real publisher so the run advances. The payload is delivered STRAIGHT to
    /// the awaiting run through the in-process runner (matched by channel and, when set, correlationId) — nothing is
    /// published to a real broker, and there is no production analogue. 409 when no suspended run awaits that channel.</summary>
    /// <param name="parameters">The request parameters (working-copy id, debug-run id, and the {channel, payload, correlationId} body).</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The debug run after the message was delivered and it advanced, or the problem.</returns>
    public async ValueTask<InjectDebugRunMessageResult> HandleInjectDebugRunMessageAsync(InjectDebugRunMessageParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return InjectDebugRunMessageResult.NotFound(DebugRunsNotOffered(404), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is null)
        {
            return InjectDebugRunMessageResult.NotFound(NotFoundProblem(id), workspace);
        }

        var runId = new WorkflowRunId((string)parameters.DebugRunId);
        if (!await this.RunBelongsToWorkingCopyAsync(runId, id, cancellationToken).ConfigureAwait(false))
        {
            return InjectDebugRunMessageResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        WorkflowRunDetail? detailOpt = await this.debugRunManagement!.GetAsync(runId, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (detailOpt is not { } detail)
        {
            return InjectDebugRunMessageResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        if (detail.Status != WorkflowRunStatus.Suspended)
        {
            return InjectDebugRunMessageResult.Conflict(
                Problem("not-awaiting-message", "Not awaiting a message", 409, $"The debug run is {MapStatus(detail.Status, detail.Wait)}; a message can only be injected while it is suspended on an AsyncAPI receive."), workspace);
        }

        Models.DebugRunMessageInjection body = parameters.Body;
        string channel = (string)body.Channel;
        string? correlationId = body.CorrelationId.IsNotUndefined() ? (string)body.CorrelationId : null;

        // Deliver STRAIGHT to the awaiting draft run (matched by channel + correlationId), resuming it through the
        // runner's recording+tracing resumer. Nothing is published to a real broker — this is the debug stand-in for
        // the real publisher (§18: "a host calls DeliverMessageAsync directly"; message waits are not pump-driven).
        int resumed = await this.draftRunner!.DeliverMessageAsync(channel, correlationId, body.Payload, cancellationToken).ConfigureAwait(false);
        if (resumed == 0)
        {
            return InjectDebugRunMessageResult.Conflict(
                Problem("not-awaiting-message", "Not awaiting a message", 409, $"The debug run is not suspended on a receive for channel '{channel}'{(correlationId is null ? string.Empty : $" with correlation '{correlationId}'")}."), workspace);
        }

        ParsedJsonDocument<Models.DebugRun>? view = await this.BuildDebugRunViewAsync(runId, id, cancellationToken).ConfigureAwait(false);
        if (view is not { } v)
        {
            return InjectDebugRunMessageResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        workspace.TakeOwnership(v);
        GovernanceAudit.Mutation(this.auditLogger, "debug-run.inject-message", this.AuditActor(), DebugRunTargetKind, runId.Value, "injected");
        return InjectDebugRunMessageResult.Ok(v.RootElement, workspace);
    }

    /// <summary>Cancels a §18 debug run (workflow-designer design §18 slice 3e-2c): the durable run is marked
    /// Cancelled (terminal + idempotent); an already-terminal run is a no-op and its current state is returned.</summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The cancelled run, or the problem.</returns>
    public async ValueTask<CancelDebugRunResult> HandleCancelDebugRunAsync(CancelDebugRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return CancelDebugRunResult.NotFound(DebugRunsNotOffered(404), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is null)
        {
            return CancelDebugRunResult.NotFound(NotFoundProblem(id), workspace);
        }

        var runId = new WorkflowRunId((string)parameters.DebugRunId);
        if (!await this.RunBelongsToWorkingCopyAsync(runId, id, cancellationToken).ConfigureAwait(false))
        {
            return CancelDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        // Terminal + idempotent: a non-terminal run is marked Cancelled; an already-terminal (or out-of-reach) run
        // returns false and the current state below is returned unchanged.
        if (await this.debugRunManagement!.CancelAsync(runId, "debug-run cancelled by developer", this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            GovernanceAudit.Mutation(this.auditLogger, "debug-run.cancel", this.AuditActor(), DebugRunTargetKind, runId.Value, "cancelled");
        }

        ParsedJsonDocument<Models.DebugRun>? view = await this.BuildDebugRunViewAsync(runId, id, cancellationToken).ConfigureAwait(false);
        if (view is not { } v)
        {
            return CancelDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        workspace.TakeOwnership(v);
        return CancelDebugRunResult.Ok(v.RootElement, workspace);
    }

    /// <summary>Deletes a §18 debug run and purges its metadata trace and captured draft (design §18 R5c): a
    /// dual-store delete keyed by run id — the trace store, the draft-run capture, and the durable run — so a run's
    /// artifacts die with it. Reach-checked against the parent working copy (§14.2); a run not belonging to it is 404.</summary>
    /// <param name="parameters">The request parameters.</param>
    /// <param name="workspace">The response workspace.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>204 No Content when deleted, or the problem.</returns>
    public async ValueTask<DeleteDebugRunResult> HandleDeleteDebugRunAsync(DeleteDebugRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (!this.DebugRunsOffered)
        {
            return DeleteDebugRunResult.NotFound(DebugRunsNotOffered(404), workspace);
        }

        string id = (string)parameters.Id;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is null)
        {
            return DeleteDebugRunResult.NotFound(NotFoundProblem(id), workspace);
        }

        var runId = new WorkflowRunId((string)parameters.DebugRunId);
        if (!await this.RunBelongsToWorkingCopyAsync(runId, id, cancellationToken).ConfigureAwait(false))
        {
            return DeleteDebugRunResult.NotFound(DebugRunNotFoundProblem(), workspace);
        }

        // §18 R5c: purge the run's metadata trace, its captured draft, and the durable run itself. Idempotent per
        // store — an id with no entry is a no-op. The trace store may be unwired (trace was optional); guard it.
        if (this.draftRunTraceStore is { } traceStore)
        {
            await traceStore.DeleteAsync(runId, cancellationToken).ConfigureAwait(false);
        }

        await this.draftRunStore!.DeleteAsync(runId, cancellationToken).ConfigureAwait(false);
        await this.workflowStateStore!.DeleteAsync(runId, cancellationToken).ConfigureAwait(false);

        GovernanceAudit.Mutation(this.auditLogger, "debug-run.delete", this.AuditActor(), DebugRunTargetKind, runId.Value, "deleted");
        return DeleteDebugRunResult.NoContent();
    }

    /// <summary>Whether a run's captured record names this working copy (the nested route's parent) — the
    /// non-disclosing cross-working-copy isolation a debug-run sub-route enforces (§14.2). A missing capture is
    /// "not this working copy".</summary>
    private async ValueTask<bool> RunBelongsToWorkingCopyAsync(WorkflowRunId runId, string workingCopyId, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<DraftRun>? record = await this.draftRunStore!.GetAsync(runId, cancellationToken).ConfigureAwait(false);
        return record is { } r && r.RootElement.WorkingCopyIdValue == workingCopyId;
    }

    /// <summary>Marks a §18 debug run resume-claimable with the requested pause (design §18 R5): the control plane
    /// persists the pause-hold (or clears it on a bare resume) and stamps the resume-requested marker, then returns —
    /// it never executes the run. A runner surfaces the run through its dispatch index, claims it, applies the
    /// persisted pause, advances it, and persists the trace; the UI polls <c>get-debug-run</c> for the new state.</summary>
    private async ValueTask MarkResumeClaimableAsync(WorkflowRunId runId, WorkflowPauseConfig? pause, CancellationToken cancellationToken)
    {
        using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.workflowStateStore!, runId, this.timeProvider, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return;
        }

        await run.RequestResumeAsync(pause, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the <c>DebugRun</c> view from the durable run: the audit tuple + executed workflow id from the
    /// capture record, the live status/cursor from the reach-filtered checkpoint (§14.2), and the in-process runner's
    /// cached metadata trace (with the handler-computed <c>pausedBefore</c> injected for a paused run). Returns
    /// <see langword="null"/> when the run does not belong to this working copy or is outside the caller's reach.</summary>
    private async ValueTask<ParsedJsonDocument<Models.DebugRun>?> BuildDebugRunViewAsync(WorkflowRunId runId, string workingCopyId, CancellationToken cancellationToken)
    {
        string workflowId, environment, documentEtag, startedBy;
        DateTimeOffset startedAt;
        using (ParsedJsonDocument<DraftRun>? record = await this.draftRunStore!.GetAsync(runId, cancellationToken).ConfigureAwait(false))
        {
            if (record is not { } r || r.RootElement.WorkingCopyIdValue != workingCopyId)
            {
                return null;
            }

            workflowId = (string)r.RootElement.WorkflowId;
            environment = (string)r.RootElement.Environment;
            documentEtag = (string)r.RootElement.DocumentEtag;
            startedBy = (string)r.RootElement.StartedBy;
            startedAt = ((JsonElement)r.RootElement.StartedAt).GetDateTimeOffset();
        }

        WorkflowRunDetail? detailOpt = await this.debugRunManagement!.GetAsync(runId, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (detailOpt is not { } detail)
        {
            return null;
        }

        string status = MapStatus(detail.Status, detail.Wait);

        // pausedBefore = the step the run resumes at (the step at the current cursor). The persisted trace carries
        // pausedBefore:null (the runner holds no step-id↔cursor map); the handler DOES, so it injects it here.
        string? pausedBefore = null;
        if (status == "paused")
        {
            List<string> stepIds = await this.CapturedStepIdsAsync(runId, cancellationToken).ConfigureAwait(false);
            pausedBefore = detail.Cursor >= 0 && detail.Cursor < stepIds.Count ? stepIds[detail.Cursor] : null;
        }

        // §18 R4: read the run's metadata trace from the durable trace store (the runner persisted it, possibly in a
        // different process), not an in-process cache. Absent a wired store, the view simply omits the trace.
        ReadOnlyMemory<byte>? trace = this.draftRunTraceStore is { } traceStore
            ? await traceStore.GetAsync(runId, cancellationToken).ConfigureAwait(false)
            : null;

        var view = new DebugRunView(
            runId.Value, workflowId, environment, status, detail.Cursor, trace, pausedBefore,
            documentEtag, startedBy, startedAt, this.timeProvider.GetUtcNow());
        return PersistedJson.ToPooledDocument<Models.DebugRun, DebugRunView>(
            view, static (Utf8JsonWriter writer, in DebugRunView v) => WriteDebugRun(writer, v));
    }

    /// <summary>Reads the executed workflow's step ids, in declaration order, from the run's captured package (the
    /// document has the chosen workflow first, so its step order IS cursor space). Empty when no capture exists.</summary>
    private async ValueTask<List<string>> CapturedStepIdsAsync(WorkflowRunId runId, CancellationToken cancellationToken)
    {
        ReadOnlyMemory<byte>? package = await this.draftRunStore!.GetPackageAsync(runId, cancellationToken).ConfigureAwait(false);
        if (package is not { } packageBytes)
        {
            return [];
        }

        WorkflowPackageContents contents = WorkflowPackage.Open(packageBytes);
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(contents.Workflow);
        return StepIds(document.RootElement);
    }

    /// <summary>Maps the durable run status + wait-kind to the contract's debug-run status enum: a
    /// <see cref="WorkflowWaitKind.Pause"/> suspend is <c>paused</c>, a timer/message suspend is <c>suspended</c>,
    /// and Pending/Running present as <c>running</c>.</summary>
    private static string MapStatus(WorkflowRunStatus status, WorkflowWait? wait)
        => status switch
        {
            WorkflowRunStatus.Completed => "completed",
            WorkflowRunStatus.Faulted => "faulted",
            WorkflowRunStatus.Cancelled => "cancelled",
            WorkflowRunStatus.Suspended => wait is { Kind: WorkflowWaitKind.Pause } ? "paused" : "suspended",
            _ => "running",
        };

    /// <summary>Translates the request's step-id pause points to the durable engine's cursor-space
    /// <see cref="WorkflowPauseConfig"/>: <c>afterEachStep</c> carries through, and each <c>beforeSteps</c> step id
    /// maps to its declaration-order cursor (the state index the run pauses at, having completed the previous step).
    /// An absent/empty pause returns <see langword="null"/> — the advance runs to completion, a wait, or a fault.</summary>
    private static WorkflowPauseConfig? TranslatePause(in Models.DebugRunPause pause, List<string> stepIds)
    {
        if (pause.IsUndefined())
        {
            return null;
        }

        bool afterEachStep = ((JsonElement)pause.AfterEachStep).ValueKind == JsonValueKind.True;
        HashSet<int>? cursors = null;
        JsonElement beforeSteps = (JsonElement)pause.BeforeSteps;
        if (beforeSteps.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement step in beforeSteps.EnumerateArray())
            {
                if (step.GetString() is { Length: > 0 } stepId)
                {
                    int cursor = stepIds.IndexOf(stepId);
                    if (cursor >= 0)
                    {
                        (cursors ??= []).Add(cursor);
                    }
                }
            }
        }

        return !afterEachStep && cursors is null
            ? null
            : new WorkflowPauseConfig(afterEachStep, cursors ?? []);
    }

    /// <summary>Reads a document's FIRST workflow's step ids in declaration order (the run executes the chosen
    /// workflow, reordered first at capture).</summary>
    private static List<string> StepIds(in JsonElement document)
    {
        var ids = new List<string>();
        if (document.TryGetProperty("workflows"u8, out JsonElement workflows) && workflows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement workflow in workflows.EnumerateArray())
            {
                if (workflow.TryGetProperty("steps"u8, out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement step in steps.EnumerateArray())
                    {
                        if (step.TryGetProperty("stepId"u8, out JsonElement stepId) && stepId.GetString() is { Length: > 0 } stepIdValue)
                        {
                            ids.Add(stepIdValue);
                        }
                    }
                }

                break;
            }
        }

        return ids;
    }

    /// <summary>Writes the <c>DebugRun</c> contract shape (debugRunId, workflowId, environment, status, cursor,
    /// trace, and the audit tuple) into a pooled document the caller owns.</summary>
    private static void WriteDebugRun(Utf8JsonWriter writer, in DebugRunView v)
    {
        writer.WriteStartObject();
        writer.WriteString(Models.DebugRun.JsonPropertyNames.DebugRunIdUtf8, v.DebugRunId);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.WorkflowIdUtf8, v.WorkflowId);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.EnvironmentUtf8, v.Environment);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.StatusUtf8, v.Status);
        writer.WriteNumber(Models.DebugRun.JsonPropertyNames.CursorUtf8, v.Cursor);
        if (v.Trace is ReadOnlyMemory<byte> trace)
        {
            writer.WritePropertyName(Models.DebugRun.JsonPropertyNames.TraceUtf8);
            WriteTraceWithPausedBefore(writer, trace, v.PausedBefore);
        }

        writer.WriteString(Models.DebugRun.JsonPropertyNames.DocumentEtagUtf8, v.DocumentEtag);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.StartedByUtf8, v.StartedBy);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.StartedAtUtf8, v.StartedAt);
        writer.WriteString(Models.DebugRun.JsonPropertyNames.UpdatedAtUtf8, v.UpdatedAt);
        writer.WriteEndObject();
    }

    /// <summary>Writes the runner's cached metadata trace verbatim, but for a paused run substitutes the
    /// handler-computed <c>pausedBefore</c> step id (the runner assembles the trace with <c>pausedBefore:null</c>).</summary>
    private static void WriteTraceWithPausedBefore(Utf8JsonWriter writer, ReadOnlyMemory<byte> traceUtf8, string? pausedBeforeStepId)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(traceUtf8);
        JsonElement root = document.RootElement;
        if (pausedBeforeStepId is null)
        {
            root.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        bool wrotePausedBefore = false;
        foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
        {
            if (property.NameEquals("pausedBefore"u8))
            {
                writer.WriteString("pausedBefore"u8, pausedBeforeStepId);
                wrotePausedBefore = true;
                continue;
            }

            using UnescapedUtf8JsonString name = property.Utf8NameSpan;
            writer.WritePropertyName(name.Span);
            property.Value.WriteTo(writer);
        }

        if (!wrotePausedBefore)
        {
            writer.WriteString("pausedBefore"u8, pausedBeforeStepId);
        }

        writer.WriteEndObject();
    }

    /// <summary>The pooled-serialization state for one <c>DebugRun</c> view (the audit tuple + live status/cursor +
    /// the runner's trace).</summary>
    private readonly struct DebugRunView(
        string debugRunId,
        string workflowId,
        string environment,
        string status,
        int cursor,
        ReadOnlyMemory<byte>? trace,
        string? pausedBefore,
        string documentEtag,
        string startedBy,
        DateTimeOffset startedAt,
        DateTimeOffset updatedAt)
    {
        public string DebugRunId { get; } = debugRunId;

        public string WorkflowId { get; } = workflowId;

        public string Environment { get; } = environment;

        public string Status { get; } = status;

        public int Cursor { get; } = cursor;

        public ReadOnlyMemory<byte>? Trace { get; } = trace;

        public string? PausedBefore { get; } = pausedBefore;

        public string DocumentEtag { get; } = documentEtag;

        public string StartedBy { get; } = startedBy;

        public DateTimeOffset StartedAt { get; } = startedAt;

        public DateTimeOffset UpdatedAt { get; } = updatedAt;
    }

    /// <inheritdoc/>
    public async ValueTask<GetWorkingCopySourceResult> HandleGetWorkingCopySourceAsync(GetWorkingCopySourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string id = (string)parameters.Id;
        string name = (string)parameters.Name;
        using ParsedJsonDocument<WorkspaceWorkflow>? workingCopy = await this.store.GetAsync(id, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (workingCopy is not { } w)
        {
            return GetWorkingCopySourceResult.NotFound(NotFoundProblem(id), workspace);
        }

        JsonElement attachment = WorkspaceSourceJson.FindAttachment((JsonElement)w.RootElement.Sources, name);
        if (attachment.ValueKind != JsonValueKind.Object)
        {
            return GetWorkingCopySourceResult.NotFound(AttachmentNotFoundProblem(id, name), workspace);
        }

        // The stored attachment IS the restore payload: name + kind + (sourceName | document) —
        // blitted whole into a workspace-owned pooled document by the generated Create() (the store
        // copy disposes with this scope).
        ParsedJsonDocument<Models.GetWorkspaceWorkflowsByIdSourcesByNameOk> response =
            Models.GetWorkspaceWorkflowsByIdSourcesByNameOk.Create(
                Models.GetWorkspaceWorkflowsByIdSourcesByNameOk.From(attachment));
        workspace.TakeOwnership(response);
        return GetWorkingCopySourceResult.Ok(response.RootElement, workspace);
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

    /// <summary>The JSON Schema 2020-12 meta-schema, compiled once — validates the CONTENT of embedded inputs
    /// schemas (pass 4). The meta-schema ships embedded and auto-resolves through the validator's cache.</summary>
    private static readonly Lazy<ValidatorSchema> InputsMetaSchema = new(() =>
        ValidatorSchema.FromText("{\"$ref\":\"https://json-schema.org/draft/2020-12/schema\"}", "corvus:meta/inputs-2020-12"));

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

                // A step's channelPath binds by the channel ADDRESS (WorkflowOperationBinder matches
                // channel.ChannelAddress, and SourceOperationSurface writes the address into channelPath),
                // so collect the address too — otherwise an address-bound step (the common AsyncAPI 3.0
                // shape, where the key and address differ) reads as "not found in any attached source".
                if (channel.Value.ValueKind == JsonValueKind.Object
                    && channel.Value.TryGetProperty("address"u8, out JsonElement address)
                    && address.ValueKind == JsonValueKind.String
                    && address.GetString() is { Length: > 0 } channelAddress)
                {
                    channels.Add(channelAddress);
                }
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
    private async ValueTask<SchemaAttachmentIndex> CheckSourceIntegrityAsync(JsonElement document, JsonElement attachments, List<Finding> findings, CancellationToken cancellationToken)
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
        var schemaAttachments = new HashSet<string>(StringComparer.Ordinal);
        var schemaAttachmentIds = new HashSet<string>(StringComparer.Ordinal);
        var schemaIdOwners = new Dictionary<string, string>(StringComparer.Ordinal);

        // A schema document's root $id is its canonical identity: two attachments claiming the same one make
        // every reference to it ambiguous (the generation resolver's registration is last-wins), so the
        // collision is an error — validation refuses it rather than letting publish compile against an
        // arbitrary one of the two. The id still counts as attached (ambiguity is the one problem reported).
        void RecordSchemaId(JsonElement schemaDocument, string attachmentName)
        {
            if (!TryGetSchemaRootId(schemaDocument, out string? schemaId))
            {
                return;
            }

            if (schemaIdOwners.TryGetValue(schemaId, out string? owner))
            {
                findings.Add(new("error", "workspace-sources", "/sourceDescriptions", $"Schema attachments '{owner}' and '{attachmentName}' both declare the root $id '{schemaId}' — references to it are ambiguous; give each document a distinct $id.", null));
                return;
            }

            schemaIdOwners[schemaId] = attachmentName;
            schemaAttachmentIds.Add(schemaId);
        }

        var operations = new HashSet<string>(StringComparer.Ordinal);
        var channels = new HashSet<string>(StringComparer.Ordinal);
        var operationNodes = new Dictionary<string, (JsonElement Root, JsonElement Operation)>(StringComparer.Ordinal);
        var operationPathItems = new Dictionary<string, JsonElement>(StringComparer.Ordinal); // opId → its path item (path-level parameters)
        var channelPayloads = new Dictionary<string, (JsonElement Root, JsonElement Payload)>(StringComparer.Ordinal); // channel key AND address → its ONE message payload schema
        var ambiguousChannels = new HashSet<string>(StringComparer.Ordinal); // multi-message channels: typing gives the benefit of the doubt
        var rentedSourceDocs = new List<IDisposable>();
        bool anySurfaceResolved = false;
        try
        {
        if (attachments.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement attachment in attachments.EnumerateArray())
            {
                if (!attachment.TryGetProperty("name"u8, out JsonElement name) || name.GetString() is not { Length: > 0 } attachedName)
                {
                    continue;
                }

                // A jsonschema attachment is deliberately NOT a sourceDescription (the Arazzo spec pins that enum):
                // it exists to be referenced by inputs-schema external $ref, so it is exempt from the declaration
                // cross-check and carries no operation surface. Its resolvable identities are its root $id when it
                // declares an absolute one (the PREFERRED authored form) and the virtual schemas/<name> fallback.
                if (attachment.TryGetProperty("type"u8, out JsonElement attachmentType) && attachmentType.ValueEquals("jsonschema"u8))
                {
                    schemaAttachments.Add(attachedName);
                    if (attachment.TryGetProperty("document"u8, out JsonElement inlineSchema))
                    {
                        RecordSchemaId(inlineSchema, attachedName);
                    }
                    else if (this.sources is not null
                        && attachment.TryGetProperty("sourceName"u8, out JsonElement schemaSourceName)
                        && schemaSourceName.GetString() is { Length: > 0 } registeredSchemaName)
                    {
                        using ParsedJsonDocument<RegisteredSource>? registeredSchema = await this.sources.GetAsync(registeredSchemaName, this.access.Current(), cancellationToken).ConfigureAwait(false);
                        if (registeredSchema is { } rs)
                        {
                            RecordSchemaId((JsonElement)rs.RootElement.Document, attachedName);
                        }
                    }

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
                    CollectOperationNodes(inline, operationNodes, operationPathItems);
                    CollectChannelPayloads(inline, channelPayloads, ambiguousChannels);
                    anySurfaceResolved = true;
                }
                else if (this.sources is not null
                    && attachment.TryGetProperty("sourceName"u8, out JsonElement sn)
                    && sn.GetString() is { Length: > 0 } registryName)
                {
                    // NOT disposed inline: the payload-typing pass below reads elements of this
                    // document; it is returned in the finally.
                    ParsedJsonDocument<RegisteredSource>? registered = await this.sources.GetAsync(registryName, this.access.Current(), cancellationToken).ConfigureAwait(false);
                    if (registered is { } r)
                    {
                        rentedSourceDocs.Add(r);
                        CollectOperationIdentities((JsonElement)r.RootElement.Document, operations, channels);
                        CollectOperationNodes((JsonElement)r.RootElement.Document, operationNodes, operationPathItems);
                        CollectChannelPayloads((JsonElement)r.RootElement.Document, channelPayloads, ambiguousChannels);
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
            return new SchemaAttachmentIndex(schemaAttachments, schemaAttachmentIds);
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
                    return new SchemaAttachmentIndex(schemaAttachments, schemaAttachmentIds); // one document-level error says it all
                }

                if (!anySurfaceResolved)
                {
                    continue; // no surface to check against; the declared-but-unattached warnings already fired
                }

                string? op = bindsOperation ? operationId.GetString() : null;
                if (op is not null)
                {
                    if (!op.StartsWith("$sourceDescriptions.", StringComparison.Ordinal) && !operations.Contains(op))
                    {
                        findings.Add(new("warning", "workspace-sources", $"/workflows/{workflowIndex}/steps/{stepIndex}/operationId", $"Operation '{op}' is not found in any attached source — did its source description get removed?", null));
                    }
                }

                string? channel = bindsChannel ? channelPath.GetString() : null;
                if (channel is not null && !channels.Contains(channel))
                {
                    findings.Add(new("warning", "workspace-sources", $"/workflows/{workflowIndex}/steps/{stepIndex}/channelPath", $"Channel '{channel}' is not found in any attached source.", null));
                }

                // Request-shape typing: literals AND statically-typed expressions checked against the
                // bound schemas. A plain "tru" on a boolean leaf can never be valid; neither can
                // "$inputs.orderId" (a string, per the workflow's own inputs schema) — only expressions
                // whose type cannot be resolved get the benefit of the doubt. An OPERATION step checks
                // three surfaces (payload, replacement values against the schema AT their target,
                // parameters against the operation's parameter schemas); a CHANNEL step checks the same
                // body surfaces against the channel's ONE message payload schema (a multi-message
                // channel — ambiguous which message the send carries — gets the benefit of the doubt).
                if (op is not null && operationNodes.TryGetValue(op, out (JsonElement Root, JsonElement Operation) node))
                {
                    var typing = new ExpressionTypingContext(workflow, operationNodes, channelPayloads);

                    if (step.TryGetProperty("requestBody"u8, out JsonElement requestBody) && requestBody.ValueKind == JsonValueKind.Object)
                    {
                        JsonElement schema = ResolveRequestSchema(node.Operation, node.Root);
                        if (schema.ValueKind == JsonValueKind.Object)
                        {
                            CheckRequestBodySurfaces(requestBody, schema, node.Root, workflowIndex, stepIndex, findings, typing);
                        }
                    }

                    if (step.TryGetProperty("parameters"u8, out JsonElement stepParameters) && stepParameters.ValueKind == JsonValueKind.Array)
                    {
                        operationPathItems.TryGetValue(op, out JsonElement pathItem);
                        int parameterIndex = -1;
                        foreach (JsonElement parameter in stepParameters.EnumerateArray())
                        {
                            parameterIndex++;
                            if (parameter.ValueKind != JsonValueKind.Object
                                || !parameter.TryGetProperty("name"u8, out JsonElement parameterName)
                                || parameterName.ValueKind != JsonValueKind.String
                                || !parameter.TryGetProperty("value"u8, out JsonElement parameterValue))
                            {
                                continue;
                            }

                            parameter.TryGetProperty("in"u8, out JsonElement parameterLocation);
                            JsonElement parameterSchema = FindOperationParameterSchema(node.Operation, pathItem, node.Root, parameterName, parameterLocation);
                            if (parameterSchema.ValueKind == JsonValueKind.Object)
                            {
                                CheckPayloadAgainstSchema(parameterValue, parameterSchema, node.Root, $"/workflows/{workflowIndex}/steps/{stepIndex}/parameters/{parameterIndex}/value", findings, 0, typing);
                            }
                        }
                    }
                }

                if (channel is not null
                    && channelPayloads.TryGetValue(channel, out (JsonElement Root, JsonElement Payload) message)
                    && step.TryGetProperty("requestBody"u8, out JsonElement channelRequestBody)
                    && channelRequestBody.ValueKind == JsonValueKind.Object)
                {
                    CheckRequestBodySurfaces(channelRequestBody, message.Payload, message.Root, workflowIndex, stepIndex, findings, new ExpressionTypingContext(workflow, operationNodes, channelPayloads));
                }
            }
        }

        return new SchemaAttachmentIndex(schemaAttachments, schemaAttachmentIds);
        }
        finally
        {
            foreach (IDisposable rented in rentedSourceDocs)
            {
                rented.Dispose();
            }
        }
    }

    /// <summary>Maps each OpenAPI operationId to its operation node (with the doc root for $ref walks) and its
    /// path item (path-level parameters apply to every operation under the path).</summary>
    private static void CollectOperationNodes(JsonElement doc, Dictionary<string, (JsonElement Root, JsonElement Operation)> nodes, Dictionary<string, JsonElement> pathItems)
    {
        if (!doc.TryGetProperty("paths"u8, out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (JsonProperty<JsonElement> method in path.Value.EnumerateObject())
            {
                if (method.Value.ValueKind == JsonValueKind.Object
                    && method.Value.TryGetProperty("operationId"u8, out JsonElement id)
                    && id.GetString() is { Length: > 0 } operationId)
                {
                    nodes.TryAdd(operationId, (doc, method.Value));
                    pathItems.TryAdd(operationId, path.Value);
                }
            }
        }
    }

    /// <summary>Checks a step request body's two typed surfaces against a resolved schema: the payload
    /// walks whole; each replacement's value checks against the schema AT its target pointer. Shared by
    /// operation steps (the request-body schema) and channel steps (the message payload schema).</summary>
    private static void CheckRequestBodySurfaces(in JsonElement requestBody, JsonElement schema, JsonElement root, int workflowIndex, int stepIndex, List<Finding> findings, ExpressionTypingContext typing)
    {
        if (requestBody.TryGetProperty("payload"u8, out JsonElement payload))
        {
            CheckPayloadAgainstSchema(payload, schema, root, $"/workflows/{workflowIndex}/steps/{stepIndex}/requestBody/payload", findings, 0, typing);
        }

        if (requestBody.TryGetProperty("replacements"u8, out JsonElement replacements) && replacements.ValueKind == JsonValueKind.Array)
        {
            int replacementIndex = -1;
            foreach (JsonElement replacement in replacements.EnumerateArray())
            {
                replacementIndex++;
                if (replacement.ValueKind == JsonValueKind.Object
                    && replacement.TryGetProperty("target"u8, out JsonElement target)
                    && target.ValueKind == JsonValueKind.String
                    && replacement.TryGetProperty("value"u8, out JsonElement replacementValue))
                {
                    using UnescapedUtf8JsonString targetUtf8 = target.GetUtf8String();
                    JsonElement targetSchema = SchemaAtPointer(schema, root, targetUtf8.Span);
                    if (targetSchema.ValueKind == JsonValueKind.Object)
                    {
                        CheckPayloadAgainstSchema(replacementValue, targetSchema, root, $"/workflows/{workflowIndex}/steps/{stepIndex}/requestBody/replacements/{replacementIndex}/value", findings, 0, typing);
                    }
                }
            }
        }
    }

    /// <summary>Maps each AsyncAPI channel — under its key AND its address (a step's <c>channelPath</c>
    /// binds by address in 3.0; 2.x keys ARE the address) — to its ONE message payload schema, following
    /// local <c>$ref</c>s. A channel carrying several distinct message payloads (3.0 <c>messages</c> map,
    /// 2.x <c>oneOf</c>, or publish+subscribe pairs) is AMBIGUOUS: which message a given send carries is
    /// unknowable statically, so it registers in <paramref name="ambiguous"/> and is never typed.</summary>
    private static void CollectChannelPayloads(JsonElement doc, Dictionary<string, (JsonElement Root, JsonElement Payload)> payloads, HashSet<string> ambiguous)
    {
        if (!doc.TryGetProperty("channels"u8, out JsonElement channelMap) || channelMap.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> channel in channelMap.EnumerateObject())
        {
            JsonElement resolved = Deref(channel.Value, doc);
            if (resolved.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            JsonElement payload = default;
            int count = 0;
            void Consider(JsonElement messageElement)
            {
                JsonElement messageNode = Deref(messageElement, doc);
                if (messageNode.ValueKind != JsonValueKind.Object)
                {
                    return;
                }

                if (messageNode.TryGetProperty("oneOf"u8, out JsonElement oneOf) && oneOf.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement alternative in oneOf.EnumerateArray())
                    {
                        Consider(alternative);
                    }

                    return;
                }

                if (messageNode.TryGetProperty("payload"u8, out JsonElement messagePayload))
                {
                    messagePayload = Deref(messagePayload, doc);
                    if (messagePayload.ValueKind == JsonValueKind.Object)
                    {
                        count++;
                        if (count == 1)
                        {
                            payload = messagePayload;
                        }
                    }
                }
            }

            // 3.0: the channel's messages map. 2.x: the publish/subscribe operations' message.
            if (resolved.TryGetProperty("messages"u8, out JsonElement messages) && messages.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> message in messages.EnumerateObject())
                {
                    Consider(message.Value);
                }
            }

            if (resolved.TryGetProperty("publish"u8, out JsonElement publish) && publish.ValueKind == JsonValueKind.Object
                && publish.TryGetProperty("message"u8, out JsonElement publishMessage))
            {
                Consider(publishMessage);
            }

            if (resolved.TryGetProperty("subscribe"u8, out JsonElement subscribe) && subscribe.ValueKind == JsonValueKind.Object
                && subscribe.TryGetProperty("message"u8, out JsonElement subscribeMessage))
            {
                Consider(subscribeMessage);
            }

            if (count == 0)
            {
                continue;
            }

            using UnescapedUtf8JsonString keyUtf8 = channel.Utf8NameSpan;
            Register(Encoding.UTF8.GetString(keyUtf8.Span));
            if (resolved.TryGetProperty("address"u8, out JsonElement address)
                && address.ValueKind == JsonValueKind.String
                && address.GetString() is { Length: > 0 } channelAddress)
            {
                Register(channelAddress);
            }

            void Register(string identity)
            {
                if (count > 1)
                {
                    ambiguous.Add(identity);
                    payloads.Remove(identity);
                }
                else if (!ambiguous.Contains(identity))
                {
                    payloads.TryAdd(identity, (doc, payload)); // first attachment wins, like operationNodes
                }
            }
        }
    }

    /// <summary>Follows a local <c>$ref</c> ("#/…") within the source document; anything else returns as-is.</summary>
    private static JsonElement Deref(JsonElement nodeElement, JsonElement root)
    {
        for (int hops = 0; hops < 8; hops++)
        {
            if (nodeElement.ValueKind != JsonValueKind.Object
                || !nodeElement.TryGetProperty("$ref"u8, out JsonElement reference)
                || reference.GetString() is not { } pointer
                || !pointer.StartsWith("#/", StringComparison.Ordinal))
            {
                return nodeElement;
            }

            JsonElement current = root;
            foreach (string token in pointer[2..].Split('/'))
            {
                string key = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out current))
                {
                    return default;
                }
            }

            nodeElement = current;
        }

        return nodeElement;
    }

    /// <summary>The operation's JSON request schema (requestBody → content → application/json → schema), $refs followed.</summary>
    private static JsonElement ResolveRequestSchema(JsonElement operation, JsonElement root)
    {
        if (!operation.TryGetProperty("requestBody"u8, out JsonElement requestBody))
        {
            return default;
        }

        requestBody = Deref(requestBody, root);
        if (requestBody.ValueKind != JsonValueKind.Object
            || !requestBody.TryGetProperty("content"u8, out JsonElement content)
            || !content.TryGetProperty("application/json"u8, out JsonElement mediaType)
            || !mediaType.TryGetProperty("schema"u8, out JsonElement schema))
        {
            return default;
        }

        return Deref(schema, root);
    }

    private static bool IsExpressionShaped(string text)
        => text.StartsWith('$') || text.Contains("{$", StringComparison.Ordinal);

    /// <summary>The schema at a JSON Pointer WITHIN a request schema (a replacement's target): each token
    /// descends <c>properties</c> for an object schema or <c>items</c> for an array index/append token,
    /// following local <c>$ref</c>s. Default when the pointer does not resolve — no finding, the
    /// goto-target-style pointer checks own that. The pointer is walked as UTF-8 through
    /// <see cref="Utf8JsonPointer"/> (tokens decode into a stack buffer; nothing materializes).</summary>
    private static JsonElement SchemaAtPointer(JsonElement schema, JsonElement root, ReadOnlySpan<byte> pointerUtf8)
    {
        if (!Utf8JsonPointer.TryCreateJsonPointer(pointerUtf8, out Utf8JsonPointer pointer))
        {
            return default;
        }

        JsonElement current = Deref(schema, root);
        Span<byte> decoded = stackalloc byte[256];
        foreach (ReadOnlySpan<byte> encoded in pointer.EnumerateEncodedSegments())
        {
            if (current.ValueKind != JsonValueKind.Object || encoded.Length > decoded.Length)
            {
                return default; // a >256-byte property token names nothing a request schema declares
            }

            int length = Utf8JsonPointer.DecodeSegment(encoded, decoded);
            ReadOnlySpan<byte> token = decoded[..length];
            if (current.TryGetProperty("properties"u8, out JsonElement properties)
                && properties.ValueKind == JsonValueKind.Object
                && properties.TryGetProperty(token, out JsonElement propertySchema))
            {
                current = Deref(propertySchema, root);
                continue;
            }

            if (IsArrayIndexToken(token) && current.TryGetProperty("items"u8, out JsonElement items))
            {
                current = Deref(items, root);
                continue;
            }

            return default;
        }

        return current;
    }

    /// <summary>An RFC 6901 array reference token: the append marker <c>-</c> or a digit run.</summary>
    private static bool IsArrayIndexToken(ReadOnlySpan<byte> token)
    {
        if (token.Length == 1 && token[0] == (byte)'-')
        {
            return true;
        }

        if (token.IsEmpty)
        {
            return false;
        }

        foreach (byte b in token)
        {
            if (b is < (byte)'0' or > (byte)'9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds the schema of the operation parameter a step parameter binds: matched by name (and
    /// location, when the step declares <c>in</c>) over the operation's parameters first, then the path
    /// item's (path-level parameters apply to every operation under the path). Default when unmatched —
    /// an unknown parameter name is not this pass's finding. Names compare as UTF-8 spans; no strings.</summary>
    private static JsonElement FindOperationParameterSchema(JsonElement operation, JsonElement pathItem, JsonElement root, in JsonElement name, in JsonElement location)
    {
        using UnescapedUtf8JsonString nameUtf8 = name.GetUtf8String();
        bool hasLocation = location.ValueKind == JsonValueKind.String;
        using UnescapedUtf8JsonString locationUtf8 = hasLocation ? location.GetUtf8String() : default;

        Span<JsonElement> owners = [operation, pathItem];
        foreach (JsonElement owner in owners)
        {
            if (owner.ValueKind != JsonValueKind.Object
                || !owner.TryGetProperty("parameters"u8, out JsonElement parameters)
                || parameters.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (JsonElement candidate in parameters.EnumerateArray())
            {
                JsonElement resolved = Deref(candidate, root);
                if (resolved.ValueKind != JsonValueKind.Object
                    || !resolved.TryGetProperty("name"u8, out JsonElement candidateName)
                    || !candidateName.ValueEquals(nameUtf8.Span))
                {
                    continue;
                }

                if (hasLocation
                    && resolved.TryGetProperty("in"u8, out JsonElement candidateLocation)
                    && candidateLocation.ValueKind == JsonValueKind.String
                    && !candidateLocation.ValueEquals(locationUtf8.Span))
                {
                    continue;
                }

                return resolved.TryGetProperty("schema"u8, out JsonElement parameterSchema)
                    ? Deref(parameterSchema, root)
                    : default;
            }
        }

        return default;
    }

    /// <summary>
    /// Walks payload literals against the operation's schema: a non-expression string on a
    /// boolean/number leaf is an error (it can never satisfy the API); a missing required property
    /// is a warning (nothing at runtime can add it).
    /// </summary>
    /// <summary>Resolves an expression's STATIC type from the document itself: the workflow's inputs
    /// schema for <c>$inputs.…</c>, and a step's output declaration chased through its operation's
    /// response schema for <c>$steps.&lt;id&gt;.outputs.&lt;name&gt;</c>. Null = unknown (no finding).</summary>
    private sealed class ExpressionTypingContext(JsonElement workflow, Dictionary<string, (JsonElement Root, JsonElement Operation)> operationNodes, Dictionary<string, (JsonElement Root, JsonElement Payload)>? channelPayloads = null)
    {
        public string? ResolveType(string expression)
        {
            if (expression.Contains("{$", StringComparison.Ordinal))
            {
                return "string"; // interpolation always yields a string
            }

            string head = expression;
            string? pointer = null;
            int hash = expression.IndexOf('#');
            if (hash >= 0)
            {
                head = expression[..hash];
                pointer = expression[hash..];
            }

            string[] segments = head.Split('.');
            if (segments[0] == "$inputs")
            {
                return workflow.TryGetProperty("inputs"u8, out JsonElement inputs)
                    ? SchemaTypeAt(inputs, segments.AsSpan(1), pointer)
                    : null;
            }

            if (segments[0] == "$steps" && segments.Length >= 4 && segments[2] == "outputs")
            {
                JsonElement target = FindStep(segments[1]);
                if (target.ValueKind != JsonValueKind.Object
                    || !target.TryGetProperty("outputs"u8, out JsonElement outputs)
                    || !outputs.TryGetProperty(segments[3], out JsonElement declaration)
                    || declaration.GetString() is not { } outputExpression)
                {
                    return null;
                }

                if (outputExpression == "$statusCode")
                {
                    return "integer";
                }

                if (outputExpression.StartsWith("$response.body", StringComparison.Ordinal)
                    && target.TryGetProperty("operationId"u8, out JsonElement opId)
                    && opId.GetString() is { } op
                    && operationNodes.TryGetValue(op, out (JsonElement Root, JsonElement Operation) node))
                {
                    JsonElement responseSchema = ResolveSuccessResponseSchema(node.Operation, node.Root);
                    if (responseSchema.ValueKind != JsonValueKind.Object)
                    {
                        return null;
                    }

                    string? outputPointer = outputExpression.Length > "$response.body".Length ? outputExpression["$response.body".Length..] : null;
                    string? baseType = SchemaTypeAt(responseSchema, [], outputPointer);

                    // The remaining segments/pointer of the ORIGINAL expression descend further.
                    if (segments.Length > 4 || pointer is not null)
                    {
                        JsonElement descended = DescendSchema(responseSchema, outputPointer);
                        return descended.ValueKind == JsonValueKind.Object ? SchemaTypeAt(descended, segments.AsSpan(4), pointer) : null;
                    }

                    return baseType;
                }

                // A channel step's output declared from the received/sent MESSAGE ($message.payload#/…)
                // types through the channel's one message payload schema, exactly as $response.body does
                // through the operation's response schema.
                if (outputExpression.StartsWith("$message.payload", StringComparison.Ordinal)
                    && channelPayloads is not null
                    && target.TryGetProperty("channelPath"u8, out JsonElement stepChannel)
                    && stepChannel.GetString() is { } stepChannelPath
                    && channelPayloads.TryGetValue(stepChannelPath, out (JsonElement Root, JsonElement Payload) message))
                {
                    string? messagePointer = outputExpression.Length > "$message.payload".Length ? outputExpression["$message.payload".Length..] : null;
                    if (segments.Length > 4 || pointer is not null)
                    {
                        JsonElement descended = DescendSchema(message.Payload, messagePointer);
                        return descended.ValueKind == JsonValueKind.Object ? SchemaTypeAt(descended, segments.AsSpan(4), pointer) : null;
                    }

                    return SchemaTypeAt(message.Payload, [], messagePointer);
                }
            }

            return null;
        }

        private JsonElement FindStep(string stepId)
        {
            if (workflow.TryGetProperty("steps"u8, out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement candidate in steps.EnumerateArray())
                {
                    if (candidate.TryGetProperty("stepId"u8, out JsonElement id) && id.ValueEquals(stepId))
                    {
                        return candidate;
                    }
                }
            }

            return default;
        }

        private static JsonElement DescendSchema(JsonElement schema, string? pointer)
        {
            if (pointer is null or "#" or "#/")
            {
                return schema;
            }

            if (!pointer.StartsWith("#/", StringComparison.Ordinal))
            {
                return default;
            }

            JsonElement current = schema;
            foreach (string token in pointer[2..].Split('/'))
            {
                string key = token.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                if (current.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (int.TryParse(key, out _) && current.TryGetProperty("items"u8, out JsonElement items))
                {
                    current = items;
                }
                else if (current.TryGetProperty("properties"u8, out JsonElement properties) && properties.TryGetProperty(key, out JsonElement member))
                {
                    current = member;
                }
                else
                {
                    return default;
                }
            }

            return current;
        }

        private static string? SchemaTypeAt(JsonElement schema, ReadOnlySpan<string> segments, string? pointer)
        {
            JsonElement current = schema;
            foreach (string segment in segments)
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty("properties"u8, out JsonElement properties)
                    || !properties.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            current = DescendSchema(current, pointer);
            return current.ValueKind == JsonValueKind.Object
                && current.TryGetProperty("type"u8, out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                ? type.GetString()
                : null;
        }
    }

    /// <summary>The operation's first 2xx JSON response schema, $refs followed.</summary>
    private static JsonElement ResolveSuccessResponseSchema(JsonElement operation, JsonElement root)
    {
        if (!operation.TryGetProperty("responses"u8, out JsonElement responses) || responses.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        foreach (JsonProperty<JsonElement> response in responses.EnumerateObject())
        {
            string code = response.Name.ToString();
            if (code.Length == 3 && code[0] == '2')
            {
                JsonElement resolved = Deref(response.Value, root);
                if (resolved.ValueKind == JsonValueKind.Object
                    && resolved.TryGetProperty("content"u8, out JsonElement content)
                    && content.TryGetProperty("application/json"u8, out JsonElement mediaType)
                    && mediaType.TryGetProperty("schema"u8, out JsonElement schema))
                {
                    return Deref(schema, root);
                }
            }
        }

        return default;
    }

    private static bool ScalarTypesCompatible(string expressionType, string schemaType)
        => expressionType == schemaType
            || (expressionType == "integer" && schemaType == "number")
            || (expressionType == "number" && schemaType == "integer");

    private static void CheckPayloadAgainstSchema(JsonElement value, JsonElement schema, JsonElement root, string pointer, List<Finding> findings, int depth, ExpressionTypingContext? typing = null)
    {
        if (depth > 12)
        {
            return;
        }

        schema = Deref(schema, root);
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        string? type = schema.TryGetProperty("type"u8, out JsonElement typeElement) && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : null;

        // The reverse literal mismatches: a number/boolean literal on a leaf whose schema wants a
        // different scalar can never satisfy the API either.
        if (value.ValueKind == JsonValueKind.Number && type is "string" or "boolean")
        {
            findings.Add(new("error", "payload-typing", pointer, $"{value.GetRawText()} is a number — the operation's schema requires a {type} here.", null));
            return;
        }

        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False && type is "string" or "number" or "integer")
        {
            findings.Add(new("error", "payload-typing", pointer, $"{value.GetRawText()} is a boolean — the operation's schema requires a {type} here.", null));
            return;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            string text = value.GetString() ?? string.Empty;
            if (!IsExpressionShaped(text))
            {
                if (type is "boolean" or "number" or "integer")
                {
                    findings.Add(new("error", "payload-typing", pointer, $"'{text}' is neither a {type} nor a runtime expression — the operation's schema requires a {type} here.", null));
                }

                return;
            }

            // An expression whose STATIC type resolves must match the property's type; only the
            // unresolvable get the benefit of the doubt.
            if (typing is not null
                && type is "boolean" or "number" or "integer" or "string"
                && typing.ResolveType(text) is { } expressionType
                && !ScalarTypesCompatible(expressionType, type))
            {
                findings.Add(new("error", "payload-typing", pointer, $"'{text}' resolves to a {expressionType} — the operation's schema requires a {type} here.", null));
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Object && (type is null or "object"))
        {
            if (schema.TryGetProperty("required"u8, out JsonElement required) && required.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement requiredName in required.EnumerateArray())
                {
                    if (requiredName.GetString() is { Length: > 0 } memberName && !value.TryGetProperty(memberName, out _))
                    {
                        findings.Add(new("warning", "payload-typing", pointer, $"Required property '{memberName}' is missing from the payload — the operation's schema requires it.", null));
                    }
                }
            }

            if (schema.TryGetProperty("properties"u8, out JsonElement properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty<JsonElement> member in value.EnumerateObject())
                {
                    string memberName = member.Name.ToString();
                    if (properties.TryGetProperty(memberName, out JsonElement memberSchema))
                    {
                        CheckPayloadAgainstSchema(member.Value, memberSchema, root, $"{pointer}/{memberName.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)}", findings, depth + 1, typing);
                    }
                }
            }

            return;
        }

        if (value.ValueKind == JsonValueKind.Array && type == "array"
            && schema.TryGetProperty("items"u8, out JsonElement items) && items.ValueKind == JsonValueKind.Object)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
            {
                CheckPayloadAgainstSchema(item, items, root, $"{pointer}/{index}", findings, depth + 1, typing);
                index++;
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