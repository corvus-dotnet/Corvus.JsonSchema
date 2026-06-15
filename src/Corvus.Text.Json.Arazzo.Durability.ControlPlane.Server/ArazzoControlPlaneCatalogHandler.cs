// <copyright file="ArazzoControlPlaneCatalogHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Durability;
using ValidatorSchema = Corvus.Text.Json.Validator.JsonSchema;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiCatalogHandler"/> over an <see cref="IWorkflowCatalogClient"/>,
/// mapping each catalog REST operation onto the corresponding catalog-client call and projecting the .NET
/// metadata records into the generated response models. The package and its individual documents are returned
/// as their stored JSON; the version-metadata responses carry the HATEOAS links the contract declares.
/// </summary>
public sealed class ArazzoControlPlaneCatalogHandler : IApiCatalogHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private const int MaxValidationErrors = 200;
    private const int MaxCachedSchemas = 512;

    // Compiling a JSON Schema is an expensive one-time job, so cache the compiled validator. The key is
    // (base/version/target), which is content-stable because catalog versions are immutable — so the cache is
    // bounded by distinct catalogued schemas, never by request volume. The coarse cap sheds reuse (not
    // correctness) if a single process accumulates more than MaxCachedSchemas distinct target schemas.
    private static readonly ConcurrentDictionary<string, ValidatorSchema> SchemaCache = new(StringComparer.Ordinal);

    private readonly IWorkflowCatalogClient catalog;
    private readonly IWorkflowManagementClient management;
    private readonly IRunnerRegistry runners;
    private readonly ControlPlaneAccess access;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneCatalogHandler"/> class (unscoped: full access).</summary>
    /// <param name="catalog">The catalog client the endpoints delegate to.</param>
    /// <param name="management">The management client used to create runs when a workflow version is triggered.</param>
    /// <param name="runners">The runner registry consulted to gate a trigger on a runner that hosts the version.</param>
    public ArazzoControlPlaneCatalogHandler(IWorkflowCatalogClient catalog, IWorkflowManagementClient management, IRunnerRegistry runners)
        : this(catalog, management, runners, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneCatalogHandler"/> class.</summary>
    /// <param name="catalog">The catalog client the endpoints delegate to.</param>
    /// <param name="management">The management client used to create runs when a workflow version is triggered.</param>
    /// <param name="runners">The runner registry consulted to gate a trigger on a runner that hosts the version.</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> per request (§14.2).</param>
    internal ArazzoControlPlaneCatalogHandler(IWorkflowCatalogClient catalog, IWorkflowManagementClient management, IRunnerRegistry runners, ControlPlaneAccess access)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(management);
        ArgumentNullException.ThrowIfNull(runners);
        ArgumentNullException.ThrowIfNull(access);
        this.catalog = catalog;
        this.management = management;
        this.runners = runners;
        this.access = access;
    }

    /// <inheritdoc/>
    public async ValueTask<AddCatalogVersionResult> HandleAddCatalogVersionAsync(AddCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        Models.PostCatalogBody body = parameters.Body;
        if (parameters.Package.IsEmpty || body.Owner.IsUndefined())
        {
            return AddCatalogVersionResult.BadRequest(
                Problem("invalid-package", "Invalid submission", 400, "A 'package' file part and an 'owner' part are both required."), workspace);
        }

        CatalogOwner owner = ToOwner(body.Owner);
        TagSet tags = ToTags(body.Tags);

        // Stamp the deployment's internal tags (e.g. the principal's tenant, §14.3) onto the new version so runs
        // triggered from it inherit them. User-supplied security tags would be validated here once the contract
        // carries them.
        SecurityTagSet securityTags = SecurityTagSet.FromTags(this.access.InternalTags());

        try
        {
            CatalogVersion version = await this.catalog.AddAsync(parameters.Package, owner, tags, securityTags, cancellationToken).ConfigureAwait(false);
            return AddCatalogVersionResult.Created(Models.CatalogVersionSummary.From(version), workspace);
        }
        catch (WorkflowAdministrationException ex)
        {
            // The base workflow id is administered by a different identity — only an administrator may publish further versions (§13).
            return AddCatalogVersionResult.Conflict(
                Problem("workflow-not-administered", "Workflow id not administered", 409, ex.Message), workspace);
        }
        catch (Security.SourceCredentialAccessDeniedException ex)
        {
            // The workflow declares a credential-protected source the submitter is not entitled to use (§13) — refuse
            // the submission rather than catalogue a version whose runs could never authenticate the source.
            return AddCatalogVersionResult.BadRequest(
                Problem("source-access-denied", "Source credential access denied", 400, ex.Message), workspace);
        }
        catch (ArgumentException ex)
        {
            return AddCatalogVersionResult.BadRequest(
                Problem("invalid-package", "Invalid package", 400, ex.Message), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SearchCatalogResult> HandleSearchCatalogAsync(SearchCatalogParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string? text = parameters.Q.IsNotUndefined() ? (string)parameters.Q : null;
        string? baseWorkflowId = parameters.BaseWorkflowId.IsNotUndefined() ? (string)parameters.BaseWorkflowId : null;
        string? workflowIdPrefix = parameters.WorkflowIdPrefix.IsNotUndefined() ? (string)parameters.WorkflowIdPrefix : null;
        TagSet tags = ToTags(parameters.Tag);
        CatalogStatus? status = parameters.Status.IsNotUndefined() ? Enum.Parse<CatalogStatus>((string)parameters.Status) : null;
        string? owner = parameters.Owner.IsNotUndefined() ? (string)parameters.Owner : null;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        CatalogPage page = await this.catalog.SearchAsync(
            new CatalogQuery(text, baseWorkflowId, workflowIdPrefix, tags, status, owner, limit, pageToken), this.access.Current(), cancellationToken).ConfigureAwait(false);
        return SearchCatalogResult.Ok(BuildPage(page), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListCatalogVersionsResult> HandleListCatalogVersionsAsync(ListCatalogVersionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        string? pageToken = parameters.PageToken.IsNotUndefined() ? (string)parameters.PageToken : null;

        CatalogPage page = await this.catalog.SearchAsync(
            new CatalogQuery(BaseWorkflowId: baseWorkflowId, Limit: limit, ContinuationToken: pageToken), this.access.Current(), cancellationToken).ConfigureAwait(false);
        return ListCatalogVersionsResult.Ok(BuildPage(page), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogVersionResult> HandleGetCatalogVersionAsync(GetCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Gated by read reach (§14.2): a version outside it comes back null → 404 (non-disclosing).
        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false);
        return version is { } v
            ? GetCatalogVersionResult.Ok(Models.CatalogVersionSummary.From(v), workspace)
            : GetCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<UpdateCatalogVersionResult> HandleUpdateCatalogVersionAsync(UpdateCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        Models.CatalogMetadataPatch patch = parameters.Body;

        CatalogOwner? owner = patch.Owner.IsNotUndefined() ? ToOwner(patch.Owner) : null;
        TagSet? tags = ToTags(patch.Tags);
        CatalogStatus? status = patch.Status.IsNotUndefined() ? Enum.Parse<CatalogStatus>((string)patch.Status) : null;
        AccessContext ctx = this.access.Current();

        // Gate (§14.2): a version outside read reach → 404 (non-disclosing); readable but outside write reach → 403.
        CatalogVersion? existing = await this.catalog.GetAsync(baseWorkflowId, versionNumber, ctx, cancellationToken).ConfigureAwait(false);
        if (existing is not { } ev)
        {
            return UpdateCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        if (!ctx.Admits(AccessVerb.Write, ev.SecurityTagsValue))
        {
            return UpdateCatalogVersionResult.Forbidden(ForbiddenProblem(baseWorkflowId, versionNumber), workspace);
        }

        CatalogVersion? updated = await this.catalog.UpdateAsync(baseWorkflowId, versionNumber, owner, tags, status, ctx, cancellationToken).ConfigureAwait(false);
        return updated is { } v
            ? UpdateCatalogVersionResult.Ok(Models.CatalogVersionSummary.From(v), workspace)
            : UpdateCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<DeleteCatalogVersionResult> HandleDeleteCatalogVersionAsync(DeleteCatalogVersionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        AccessContext ctx = this.access.Current();

        // Gate (§14.2): a version outside read reach → 404 (non-disclosing); readable but outside write reach → 403.
        CatalogVersion? existing = await this.catalog.GetAsync(baseWorkflowId, versionNumber, ctx, cancellationToken).ConfigureAwait(false);
        if (existing is not { } ev)
        {
            return DeleteCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        if (!ctx.Admits(AccessVerb.Write, ev.SecurityTagsValue))
        {
            return DeleteCatalogVersionResult.Forbidden(ForbiddenProblem(baseWorkflowId, versionNumber), workspace);
        }

        CatalogDeleteOutcome outcome = await this.catalog.DeleteAsync(baseWorkflowId, versionNumber, ctx, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            CatalogDeleteOutcome.Deleted => DeleteCatalogVersionResult.NoContent(),
            CatalogDeleteOutcome.Referenced => DeleteCatalogVersionResult.Conflict(
                Problem("version-referenced", "Version is referenced", 409, $"Version {versionNumber} of '{baseWorkflowId}' cannot be deleted while workflow runs reference it; mark it obsolete instead."), workspace),
            _ => DeleteCatalogVersionResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<PurgeCatalogResult> HandlePurgeCatalogAsync(PurgeCatalogParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // The scoped catalog client row-scopes the purge (§14.2): reaps only obsolete versions the principal may
        // see (see run purge — the capability is orthogonal to reach).
        int purged = await this.catalog.PurgeAsync(this.access.Current(), cancellationToken).ConfigureAwait(false);
        return PurgeCatalogResult.Ok(
            new Models.PurgeResult.Source((ref Models.PurgeResult.Builder b) => b.Create(purgedCount: purged)), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogPackageResult> HandleGetCatalogPackageAsync(GetCatalogPackageParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Visibility is enforced inside the read: an invisible version yields null → 404 (no separate pre-check).
        ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (package is not { } bytes)
        {
            return GetCatalogPackageResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // The stored package is an opaque binary artifact streamed back as-is (octet-stream).
        return GetCatalogPackageResult.Ok(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogWorkflowResult> HandleGetCatalogWorkflowAsync(GetCatalogWorkflowParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, CatalogPackage.WorkflowDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogWorkflowResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // Hand the parsed document to the response workspace so it lives until the response is written
        // (the result Body references it); the workspace disposes it — do not dispose it here.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogWorkflowResult.Ok(parsed.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogWorkflowSchemasResult> HandleGetCatalogWorkflowSchemasAsync(GetCatalogWorkflowSchemasParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.SchemasDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogWorkflowSchemasResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // Hand the parsed document to the response workspace so it lives until the response is written.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogWorkflowSchemasResult.Ok(parsed.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogExecutorResult> HandleGetCatalogExecutorAsync(GetCatalogExecutorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        ReadOnlyMemory<byte>? executor = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (executor is not { } bytes)
        {
            return GetCatalogExecutorResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // The compiled executor assembly is an opaque binary artifact streamed back as-is (octet-stream).
        return GetCatalogExecutorResult.Ok(bytes);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogExecutorManifestResult> HandleGetCatalogExecutorManifestAsync(GetCatalogExecutorManifestParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogExecutorManifestResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        // Hand the parsed document to the response workspace so it lives until the response is written.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogExecutorManifestResult.Ok(parsed.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetCatalogSourceResult> HandleGetCatalogSourceAsync(GetCatalogSourceParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string sourceName = (string)parameters.SourceName;

        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, sourceName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (document is not { } bytes)
        {
            return GetCatalogSourceResult.NotFound(
                Problem("source-not-found", "Source not found", 404, $"Version {versionNumber} of '{baseWorkflowId}' has no source document named '{sourceName}'."), workspace);
        }

        // Hand the parsed document to the response workspace (see HandleGetCatalogWorkflowAsync) so it lives
        // until the response is written; the workspace disposes it.
        ParsedJsonDocument<Models.JsonObject> parsed = ParsedJsonDocument<Models.JsonObject>.Parse(bytes);
        workspace.TakeOwnership(parsed);
        return GetCatalogSourceResult.Ok(parsed.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ValidateCatalogValueResult> HandleValidateCatalogValueAsync(ValidateCatalogValueParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        AccessContext ctx = this.access.Current();

        // Gate on read reach (the schema resolution below reads the package): a version outside it reads back null.
        if (await this.catalog.GetAsync(baseWorkflowId, versionNumber, ctx, cancellationToken).ConfigureAwait(false) is null)
        {
            return ValidateCatalogValueResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        if (!TryMapTarget(parameters.Body.Target, out WorkflowSchemaTarget target, out string targetKey))
        {
            return ValidateCatalogValueResult.NotFound(
                Problem("validation-target", "Unknown validation target", 404, "The validation target kind is not recognised."), workspace);
        }

        string cacheKey = $"{baseWorkflowId}/{versionNumber}/{targetKey}";
        (SchemaResolution resolution, ValidatorSchema schema) = await this.ResolveSchemaAsync(baseWorkflowId, versionNumber, target, cacheKey, ctx, cancellationToken).ConfigureAwait(false);
        switch (resolution)
        {
            case SchemaResolution.VersionMissing:
                return ValidateCatalogValueResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
            case SchemaResolution.SchemaMissing:
                return ValidateCatalogValueResult.NotFound(
                    Problem("validation-target", "Validation target not found", 404, "The requested schema could not be resolved from this version's package."), workspace);
        }

        Corvus.Text.Json.JsonElement value = parameters.Body.Value;
        (bool valid, IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)> errors) = Validate(schema, in value);
        return ValidateCatalogValueResult.Ok(BuildValidationResult(valid, errors), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<StartCatalogWorkflowRunResult> HandleStartCatalogWorkflowRunAsync(StartCatalogWorkflowRunParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        AccessContext ctx = this.access.Current();

        // Gated by read reach (§14.2): a version outside it reads back null → 404 (triggering is gated by it).
        CatalogVersion? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, ctx, cancellationToken).ConfigureAwait(false);
        if (version is not { } catalogVersion)
        {
            return StartCatalogWorkflowRunResult.NotFound(NotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        if (!(bool)catalogVersion.Runnable)
        {
            return StartCatalogWorkflowRunResult.Conflict(
                Problem("not-runnable", "Version not runnable", 409, $"Version {versionNumber} of '{baseWorkflowId}' carries no compiled executor; it cannot be run."), workspace);
        }

        // Validate the inputs against the version's baked inputs schema (when it declares one).
        string workflowId = (string)catalogVersion.WorkflowId;
        var target = new WorkflowSchemaTarget(WorkflowSchemaTargetKind.Inputs, workflowId, null, null);
        string cacheKey = $"{baseWorkflowId}/{versionNumber}/inputs/{workflowId}//";
        (SchemaResolution resolution, ValidatorSchema schema) = await this.ResolveSchemaAsync(baseWorkflowId, versionNumber, target, cacheKey, ctx, cancellationToken).ConfigureAwait(false);

        Corvus.Text.Json.JsonElement inputs = parameters.Body;
        if (resolution == SchemaResolution.Resolved)
        {
            (bool valid, IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)> errors) = Validate(schema, in inputs);
            if (!valid)
            {
                return StartCatalogWorkflowRunResult.UnprocessableEntity(BuildValidationResult(valid, errors), workspace);
            }
        }

        // Gate on a registered runner that hosts this version: a run accepted with no runner to execute it would
        // sit Pending indefinitely, so refuse it up front with a 409 the caller can act on.
        if (!await this.runners.IsVersionHostedAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false))
        {
            return StartCatalogWorkflowRunResult.Conflict(
                Problem("no-runner", "No hosting runner", 409, $"No registered runner currently hosts version {versionNumber} of '{baseWorkflowId}'; start a runner that hosts it and retry."), workspace);
        }

        // A version with no inputs schema (SchemaMissing) accepts any inputs. The run inherits the version's
        // security tags (KVP labels) so row authorization (§14.2) sees the same labels the workflow carries.
        WorkflowRunId runId = await this.management.StartAsync(workflowId, inputs, correlationId: null, tags: default, securityTags: catalogVersion.SecurityTagsValue, cancellationToken).ConfigureAwait(false);

        return StartCatalogWorkflowRunResult.Accepted(
            new Models.WorkflowRunAccepted.Source((ref Models.WorkflowRunAccepted.Builder b) => b.Create(
                runId: runId.Value,
                status: WorkflowRunStatus.Pending.ToString(),
                workflowId: workflowId)),
            workspace);
    }

    /// <summary>How a target schema resolved from a version's package.</summary>
    private enum SchemaResolution
    {
        /// <summary>The catalog version does not exist.</summary>
        VersionMissing,

        /// <summary>The version exists but declares no schema for the target (e.g. a workflow with no inputs schema).</summary>
        SchemaMissing,

        /// <summary>The schema was resolved (and compiled/cached).</summary>
        Resolved,
    }

    private static (bool Valid, IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)> Errors) Validate(ValidatorSchema schema, in Corvus.Text.Json.JsonElement value)
    {
        using JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        bool valid = schema.Validate(in value, collector);

        var errors = new List<(string InstancePath, string Message, string SchemaLocation)>();
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            if (!result.IsMatch)
            {
                errors.Add((result.GetDocumentEvaluationLocationText(), result.GetMessageText(), result.GetSchemaEvaluationLocationText()));
                if (errors.Count >= MaxValidationErrors)
                {
                    break;
                }
            }
        }

        return (valid, errors);
    }

    private async ValueTask<(SchemaResolution Resolution, ValidatorSchema Schema)> ResolveSchemaAsync(string baseWorkflowId, int versionNumber, WorkflowSchemaTarget target, string cacheKey, AccessContext context, CancellationToken cancellationToken)
    {
        if (SchemaCache.TryGetValue(cacheKey, out ValidatorSchema cached))
        {
            return (SchemaResolution.Resolved, cached);
        }

        // Read the package through the caller's read reach (§14.2): an out-of-reach version resolves as missing.
        ReadOnlyMemory<byte>? package = await this.catalog.GetPackageAsync(baseWorkflowId, versionNumber, context, cancellationToken).ConfigureAwait(false);
        if (package is not { } packageBytes)
        {
            return (SchemaResolution.VersionMissing, default!);
        }

        (byte[] workflow, IReadOnlyList<KeyValuePair<string, byte[]>> sources) = CatalogPackage.Unpack(packageBytes);
        if (!WorkflowSchemaMetadataGenerator.TryBuildValidationSchema(workflow, sources, target, out byte[] schemaDocument))
        {
            return (SchemaResolution.SchemaMissing, default!);
        }

        ValidatorSchema schema = ValidatorSchema.FromText(Encoding.UTF8.GetString(schemaDocument), "corvus:catalog/" + cacheKey);
        if (SchemaCache.Count >= MaxCachedSchemas)
        {
            SchemaCache.Clear();
        }

        SchemaCache[cacheKey] = schema;
        return (SchemaResolution.Resolved, schema);
    }

    private static bool TryMapTarget(Models.ValidationTarget target, out WorkflowSchemaTarget schemaTarget, out string targetKey)
    {
        schemaTarget = default;
        targetKey = string.Empty;

        string kind = target.Kind.IsNotUndefined() ? (string)target.Kind : string.Empty;
        WorkflowSchemaTargetKind? kindEnum = kind switch
        {
            "inputs" => WorkflowSchemaTargetKind.Inputs,
            "requestBody" => WorkflowSchemaTargetKind.RequestBody,
            "responseBody" => WorkflowSchemaTargetKind.ResponseBody,
            "stepOutputs" => WorkflowSchemaTargetKind.StepOutputs,
            _ => null,
        };
        if (kindEnum is not { } resolvedKind)
        {
            return false;
        }

        string? workflowId = target.WorkflowId.IsNotUndefined() ? (string)target.WorkflowId : null;
        string? stepId = target.StepId.IsNotUndefined() ? (string)target.StepId : null;
        string? status = target.Status.IsNotUndefined() ? (string)target.Status : null;
        schemaTarget = new WorkflowSchemaTarget(resolvedKind, workflowId, stepId, status);
        targetKey = $"{kind}/{workflowId}/{stepId}/{status}";
        return true;
    }

    private static Models.ValidationResult.Source BuildValidationResult(bool valid, IReadOnlyList<(string InstancePath, string Message, string SchemaLocation)> errors)
        => new((ref Models.ValidationResult.Builder b) => b.Create(
            valid: valid,
            errors: new Models.ValidationResult.ValidationErrorArray.Source((ref Models.ValidationResult.ValidationErrorArray.Builder ab) =>
            {
                foreach ((string instancePath, string message, string schemaLocation) in errors)
                {
                    ab.AddItem(new Models.ValidationError.Source((ref Models.ValidationError.Builder eb) =>
                    {
                        Models.JsonString.Source instancePathSource = string.IsNullOrEmpty(instancePath) ? default : (Models.JsonString.Source)instancePath;
                        Models.JsonString.Source schemaLocationSource = string.IsNullOrEmpty(schemaLocation) ? default : (Models.JsonString.Source)schemaLocation;
                        eb.Create(message: message, instancePath: instancePathSource, schemaLocation: schemaLocationSource);
                    }));
                }
            })));

    private static CatalogOwner ToOwner(Models.CatalogOwner owner)
        => new(
            Name: owner.Name.IsNotUndefined() ? (string)owner.Name : string.Empty,
            Email: owner.Email.IsNotUndefined() ? (string)owner.Email : string.Empty,
            Team: owner.Team.IsNotUndefined() ? (string)owner.Team : null,
            Url: owner.Url.IsNotUndefined() ? (string)owner.Url : null);

    // Copy the parsed tag-list parameter's canonical bytes into the holder (per request, not per row). An add or a
    // search needle yields an empty holder when absent; a patch yields null (= "leave tags unchanged").
    private static TagSet ToTags(Models.PostCatalogBody.JsonStringArray tags) => TagSet.CopyFrom(tags);

    private static TagSet? ToTags(Models.CatalogMetadataPatch.JsonStringArray tags) => tags.IsUndefined() ? null : TagSet.CopyFrom(tags);

    private static TagSet ToTags(Models.TagList tags) => TagSet.CopyFrom(tags);

    private static Models.CatalogPage.Source BuildPage(CatalogPage page)
        => new((ref Models.CatalogPage.Builder b) =>
        {
            Models.JsonString.Source nextPageToken = page.ContinuationToken is { } token ? (Models.JsonString.Source)token : default;
            b.Create(
                versions: new Models.CatalogPage.CatalogVersionSummaryArray.Source(
                    (ref Models.CatalogPage.CatalogVersionSummaryArray.Builder ab) =>
                    {
                        foreach (CatalogVersion version in page.Versions)
                        {
                            ab.AddItem(Models.CatalogVersionSummary.From(version));
                        }
                    }),
                nextPageToken: nextPageToken);
        });

    private static Models.ProblemDetails.Source NotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Version not found", 404, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists.");

    private static Models.ProblemDetails.Source ForbiddenProblem(string baseWorkflowId, int versionNumber)
        => Problem("forbidden", "Action not permitted", 403, $"You do not have permission to modify version {versionNumber} of '{baseWorkflowId}'.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));
}