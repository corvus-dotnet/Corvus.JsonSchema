// <copyright file="ArazzoControlPlaneEnvironmentExecutorsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Serves the executors a tenant has countersigned for an environment (ADR 0065 phase C), under the environment's own
/// administrators and the <c>environments:read</c>/<c>environments:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para>
/// The platform generates, compiles and signs a version's executor, and its signature proves only that the platform
/// produced it. A tenant operator countersigns the executor's package hash and assembly digest for one environment
/// with the tenant's own executor-signing key; a runner that pins that key's public half executes nothing in the
/// environment the tenant did not countersign. The countersignature lives on the environment record beside the key
/// generations, since it is the environment's trust configuration and needs no backend of its own.
/// </para>
/// <para>
/// The control plane holds no tenant key and verifies nothing about the signature beyond its shape. It checks that
/// the hash and digest name the version's current executor, a courtesy to an operator who signed a stale manifest
/// and not a control: the runner compares the signed values to the manifest of the executor it actually loaded, so a
/// control plane that swapped the executor after the operator reviewed it gains nothing by recording anything here.
/// </para>
/// </remarks>
internal sealed class ArazzoControlPlaneEnvironmentExecutorsHandler : IApiEnvironmentExecutorsHandler
{
    private const string TargetKind = "environment-executor";
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    // An IEEE P1363 ES256 signature is 64 bytes; the length is caller-supplied, so anything larger falls back to the
    // pool rather than the stack.
    private const int SignatureLength = 64;
    private const int StackDecodeThreshold = 256;

    private readonly IEnvironmentStore environments;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ControlPlaneAccess access;
    private readonly TimeProvider timeProvider;
    private readonly string subjectClaimType;
    private readonly GovernanceAuditor auditor;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneEnvironmentExecutorsHandler"/> class.</summary>
    /// <param name="environments">The environment store.</param>
    /// <param name="administration">The environment administration set.</param>
    /// <param name="catalog">The catalog, read for the version's executor manifest.</param>
    /// <param name="access">The caller's access context.</param>
    /// <param name="timeProvider">The time source for the recording instant.</param>
    /// <param name="subjectClaimType">The claim type identifying the deciding subject (the recorded actor); default <c>sub</c>.</param>
    /// <param name="auditor">The governance audit sink, if any.</param>
    internal ArazzoControlPlaneEnvironmentExecutorsHandler(
        IEnvironmentStore environments,
        SecuredEnvironmentAdministration administration,
        ISecuredWorkflowCatalog catalog,
        ControlPlaneAccess access,
        TimeProvider? timeProvider = null,
        string subjectClaimType = "sub",
        GovernanceAuditor? auditor = null)
    {
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        this.environments = environments;
        this.administration = administration;
        this.catalog = catalog;
        this.access = access;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.subjectClaimType = subjectClaimType;
        this.auditor = auditor ?? GovernanceAuditor.None;
    }

    private enum ShapeCheck : byte
    {
        Verified,
        SignatureUnreadable,
        SignatureWrongLength,
    }

    private enum ManifestCheck : byte
    {
        Verified,
        VersionNotFound,
        NoExecutor,
        ManifestUnreadable,
        PackageHashMismatch,
        AssemblyDigestMismatch,
    }

    /// <inheritdoc/>
    public async ValueTask<ListExecutorCountersignaturesResult> HandleListExecutorCountersignaturesAsync(ListExecutorCountersignaturesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;

        ParsedJsonDocument<Environment>? stored = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (stored is null)
        {
            return ListExecutorCountersignaturesResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        // The views are built lazily over these bytes, so the workspace owns the document until the response is
        // written. The set is bounded by the versions promoted into one environment and is served whole: the page
        // parameters are accepted for the contract's uniformity and the record is small enough to answer in one.
        workspace.TakeOwnership(stored);
        Environment.EnvironmentExecutorCountersignatureArray recorded = stored.RootElement.ExecutorCountersignatures;
        Models.ExecutorCountersignatureList.Source<Environment.EnvironmentExecutorCountersignatureArray> body = Models.ExecutorCountersignatureList.Build(
            in recorded,
            countersignatures: Models.ExecutorCountersignatureList.ExecutorCountersignatureViewArray.Build(in recorded, BuildViews));

        return ListExecutorCountersignaturesResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<CountersignExecutorResult> HandleCountersignExecutorAsync(CountersignExecutorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        (GovernanceGate gate, ParsedJsonDocument<Environment>? authorized) = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return CountersignExecutorResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            await this.auditor.MutationAsync("environment.executor.countersign", this.AuditActor(), TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), "refused-not-administrator").ConfigureAwait(false);
            return CountersignExecutorResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        // The gate read this row to decide, and hands it on; it is non-null exactly on the Authorized path.
        ParsedJsonDocument<Environment> stored = authorized!;

        // The shape: a signature that decodes to the 64 bytes of an ES256 signature. Nothing more is checked here,
        // since the control plane holds no key it could verify under.
        ShapeCheck shape = CheckShape(parameters.Body);
        if (shape != ShapeCheck.Verified)
        {
            stored.Dispose();
            await this.auditor.MutationAsync("environment.executor.countersign", this.AuditActor(), TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), $"refused-{shape}").ConfigureAwait(false);
            return CountersignExecutorResult.BadRequest(ShapeProblem(environment, baseWorkflowId, versionNumber, shape), workspace);
        }

        // The signed hash and digest have to name the version's current executor. An operator who signed the manifest
        // of a version since repacked or recompiled is told so now rather than by a runner refusing every run.
        ManifestCheck manifest = await this.CheckManifestAsync(baseWorkflowId, versionNumber, parameters.Body, cancellationToken).ConfigureAwait(false);
        if (manifest == ManifestCheck.VersionNotFound)
        {
            stored.Dispose();
            return CountersignExecutorResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
        }

        if (manifest != ManifestCheck.Verified)
        {
            stored.Dispose();
            await this.auditor.MutationAsync("environment.executor.countersign", this.AuditActor(), TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), $"refused-{manifest}").ConfigureAwait(false);
            return CountersignExecutorResult.Conflict(ManifestProblem(environment, baseWorkflowId, versionNumber, manifest), workspace);
        }

        string actor = this.CallerActor();

        // The hash, digest and signature are carried into the document as the request's own JSON values, copied
        // verbatim: a runner reads back exactly the bytes the operator sent.
        using ParsedJsonDocument<Environment> draft = Environment.DraftWithExecutorCountersigned(
            stored.RootElement,
            baseWorkflowId,
            versionNumber,
            (JsonElement)parameters.Body.PackageHash,
            (JsonElement)parameters.Body.AssemblyDigest,
            (JsonElement)parameters.Body.Signature,
            actor,
            this.timeProvider.GetUtcNow());

        ParsedJsonDocument<Environment>? updated = await this.environments.UpdateAsync(
            environment, draft.RootElement, stored.RootElement.EtagValue, actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        stored.Dispose();
        if (updated is null)
        {
            return CountersignExecutorResult.Conflict(ConcurrentWriteProblem(environment, baseWorkflowId, versionNumber), workspace);
        }

        await this.auditor.MutationAsync("environment.executor.countersign", actor, TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), "recorded").ConfigureAwait(false);
        workspace.TakeOwnership(updated);
        return CountersignExecutorResult.Ok(Models.ExecutorCountersignatureView.From(Environment.FindExecutorCountersignature(updated.RootElement, baseWorkflowId, versionNumber)!.Value), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<WithdrawExecutorCountersignatureResult> HandleWithdrawExecutorCountersignatureAsync(WithdrawExecutorCountersignatureParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        (GovernanceGate gate, ParsedJsonDocument<Environment>? authorized) = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return WithdrawExecutorCountersignatureResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate != GovernanceGate.Authorized)
        {
            await this.auditor.MutationAsync("environment.executor.withdraw", this.AuditActor(), TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), "refused-not-administrator").ConfigureAwait(false);
            return WithdrawExecutorCountersignatureResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        using ParsedJsonDocument<Environment> stored = authorized!;

        // Idempotent: withdrawing what is not recorded changes nothing and says so.
        if (Environment.FindExecutorCountersignature(stored.RootElement, baseWorkflowId, versionNumber) is null)
        {
            return WithdrawExecutorCountersignatureResult.NoContent();
        }

        string actor = this.CallerActor();
        using ParsedJsonDocument<Environment> draft = Environment.DraftWithExecutorCountersignatureWithdrawn(stored.RootElement, baseWorkflowId, versionNumber);
        using ParsedJsonDocument<Environment>? updated = await this.environments.UpdateAsync(
            environment, draft.RootElement, stored.RootElement.EtagValue, actor, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (updated is null)
        {
            return WithdrawExecutorCountersignatureResult.Conflict(ConcurrentWriteProblem(environment, baseWorkflowId, versionNumber), workspace);
        }

        await this.auditor.MutationAsync("environment.executor.withdraw", actor, TargetKind, ExecutorKey(environment, baseWorkflowId, versionNumber), "withdrawn").ConfigureAwait(false);
        return WithdrawExecutorCountersignatureResult.NoContent();
    }

    // Writes the recorded countersignatures into the response array. Each view is a whole-document re-wrap of the
    // stored record, so nothing is projected field by field.
    private static void BuildViews(in Environment.EnvironmentExecutorCountersignatureArray recorded, ref Models.ExecutorCountersignatureList.ExecutorCountersignatureViewArray.Builder array)
    {
        foreach (Environment.EnvironmentExecutorCountersignature countersignature in Environment.Enumerate(recorded))
        {
            array.AddItem(Models.ExecutorCountersignatureView.From(countersignature));
        }
    }

    // The signature decodes straight out of the request's UTF-8 into a stack buffer; it never becomes a managed string
    // or array here, since nothing verifies it.
    private static ShapeCheck CheckShape(in Models.ExecutorCountersignature body)
    {
        ReadOnlySpan<byte> signatureBase64 = ((JsonElement)body.Signature).GetUtf8String().Span;
        int max = Base64.GetMaxDecodedFromUtf8Length(signatureBase64.Length);
        byte[]? rented = max > StackDecodeThreshold ? ArrayPool<byte>.Shared.Rent(max) : null;
        try
        {
            Span<byte> buffer = rented ?? stackalloc byte[StackDecodeThreshold];
            if (Base64.DecodeFromUtf8(signatureBase64, buffer, out _, out int written) != OperationStatus.Done)
            {
                return ShapeCheck.SignatureUnreadable;
            }

            return written == SignatureLength ? ShapeCheck.Verified : ShapeCheck.SignatureWrongLength;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    // The version's stored executor manifest, compared field by field to what was signed. The catalog read is under the
    // caller's reach, so a version the caller cannot see is not found rather than described.
    private async ValueTask<ManifestCheck> CheckManifestAsync(string baseWorkflowId, int versionNumber, Models.ExecutorCountersignature body, CancellationToken cancellationToken)
    {
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return ManifestCheck.VersionNotFound;
            }
        }

        ReadOnlyMemory<byte>? document = await this.catalog.GetDocumentAsync(baseWorkflowId, versionNumber, WorkflowPackage.ExecutorManifestDocumentName, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (document is not { } manifestUtf8)
        {
            return ManifestCheck.NoExecutor;
        }

        WorkflowExecutorManifest manifest;
        try
        {
            manifest = WorkflowExecutorManifest.Parse(manifestUtf8);
        }
        catch (FormatException)
        {
            return ManifestCheck.ManifestUnreadable;
        }

        if (!body.PackageHash.ValueEquals(manifest.PackageHash))
        {
            return ManifestCheck.PackageHashMismatch;
        }

        return body.AssemblyDigest.ValueEquals(manifest.AssemblyDigest) ? ManifestCheck.Verified : ManifestCheck.AssemblyDigestMismatch;
    }

    // The gate must read the environment to decide visibility, and every authorized caller then works on that same
    // row, so it hands the document on rather than disposing it. The caller owns the returned document on the
    // Authorized path only.
    private async ValueTask<(GovernanceGate Gate, ParsedJsonDocument<Environment>? Environment)> AuthorizeEnvironmentAdminAsync(
        string environment, CancellationToken cancellationToken)
    {
        ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environmentDoc is null)
        {
            return (GovernanceGate.NotFound, null);
        }

        try
        {
            using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(environment, cancellationToken).ConfigureAwait(false);
            if (record?.RootElement.IsAdministeredBy(this.CallerIdentity()) != true)
            {
                environmentDoc.Dispose();
                return (GovernanceGate.Forbidden, null);
            }
        }
        catch
        {
            environmentDoc.Dispose();
            throw;
        }

        return (GovernanceGate.Authorized, environmentDoc);
    }

    private static string ExecutorKey(string environment, string baseWorkflowId, int versionNumber) => $"{environment}/{baseWorkflowId}/{versionNumber}";

    private static Models.ProblemDetails.Source EnvironmentNotFoundProblem(string environment)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source VersionNotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Version not found", 404, $"No version {versionNumber} of '{baseWorkflowId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source ConcurrentWriteProblem(string environment, string baseWorkflowId, int versionNumber)
        => Problem("environment-executor-conflict", "Concurrent executor change", 409, $"Environment '{environment}' changed while countersigning or withdrawing version {versionNumber} of '{baseWorkflowId}'. Re-read and retry.");

    private static Models.ProblemDetails.Source ShapeProblem(string environment, string baseWorkflowId, int versionNumber, ShapeCheck shape)
        => Problem("executor-countersignature-shape", "Countersignature not accepted", 400, shape switch
        {
            ShapeCheck.SignatureUnreadable => $"The countersignature for version {versionNumber} of '{baseWorkflowId}' in environment '{environment}' did not decode as base64.",
            _ => $"The countersignature for version {versionNumber} of '{baseWorkflowId}' in environment '{environment}' is not 64 bytes; an ES256 signature in IEEE P1363 form is.",
        });

    private static Models.ProblemDetails.Source ManifestProblem(string environment, string baseWorkflowId, int versionNumber, ManifestCheck check)
        => Problem("executor-digest-mismatch", "Countersignature names another executor", 409, check switch
        {
            ManifestCheck.NoExecutor => $"Version {versionNumber} of '{baseWorkflowId}' carries no executor, so there is nothing to countersign for environment '{environment}'.",
            ManifestCheck.ManifestUnreadable => $"The executor manifest of version {versionNumber} of '{baseWorkflowId}' does not parse, so nothing can be countersigned for environment '{environment}'.",
            ManifestCheck.PackageHashMismatch => $"The package hash countersigned for version {versionNumber} of '{baseWorkflowId}' is not the one its executor manifest records. Read the current manifest and sign again.",
            _ => $"The assembly digest countersigned for version {versionNumber} of '{baseWorkflowId}' is not the one its executor manifest records. Read the current manifest, review the executor it names, and sign again.",
        });

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => Models.ProblemDetails.Build(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type);

    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    // Countersigning is a governance decision, so the actor recorded is the deciding subject, as the key endpoints
    // record it.
    private string CallerActor() => AuditSubject.ResolveSubject(this.access.CurrentPrincipal, this.subjectClaimType);

    private AuditSubject AuditActor() => this.access.AuditSubject();

    private enum GovernanceGate
    {
        NotFound,
        Forbidden,
        Authorized,
    }
}