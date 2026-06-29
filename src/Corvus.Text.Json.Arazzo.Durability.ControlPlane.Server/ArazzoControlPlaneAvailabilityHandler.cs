// <copyright file="ArazzoControlPlaneAvailabilityHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAvailabilityHandler"/> over an <see cref="IAvailabilityStore"/> — the
/// control-plane surface that makes workflow versions available in deployment environments ("promotion", design §7.8).
/// The endpoints are gated by the <c>availability:read</c>/<c>availability:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Governance + readiness.</strong> Availability is additive and many-to-many. Making a version available
/// (or withdrawing it) is governed by the <em>target environment's</em> administrators — a two-layer check mirroring
/// §7.7: the environment must be in the caller's reach (404 otherwise) and the caller must be a current administrator of
/// it (403 otherwise). Make-available is additionally readiness-gated (§7.7): the version is allowed into the environment
/// only where every source it references resolves a credential there, else 409 listing the missing sources.</para>
/// <para><strong>Reads.</strong> The per-version listing (the environments a version is available in) is visible to a
/// caller who can read the workflow version; the per-environment listing (the versions available in an environment) is
/// visible to a caller whose reach admits the environment. The persisted <see cref="AvailabilityEntry"/> is congruent
/// with the API <see cref="Models.AvailabilityEntry"/>, so reads project as a free whole-document re-wrap.</para>
/// </remarks>
public sealed class ArazzoControlPlaneAvailabilityHandler : IApiAvailabilityHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IAvailabilityStore availability;
    private readonly IEnvironmentStore environments;
    private readonly SecuredEnvironmentAdministration administration;
    private readonly ISecuredWorkflowCatalog catalog;
    private readonly ISourceCredentialStore credentials;
    private readonly ControlPlaneAccess access;
    private readonly string actor;

    /// <summary>Initializes a new, unscoped instance (every request runs with <see cref="AccessContext.System"/>).</summary>
    /// <param name="availability">The availability matrix store.</param>
    /// <param name="environments">The environment store (target-environment visibility).</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating).</param>
    /// <param name="catalog">The workflow catalog (version existence + its sources, for readiness).</param>
    /// <param name="credentials">The source-credential store (readiness: a credential per source × environment).</param>
    /// <param name="actor">The audit actor recorded on writes.</param>
    public ArazzoControlPlaneAvailabilityHandler(IAvailabilityStore availability, IEnvironmentStore environments, SecuredEnvironmentAdministration administration, ISecuredWorkflowCatalog catalog, ISourceCredentialStore credentials, string actor = "control-plane")
        : this(availability, environments, administration, catalog, credentials, new ControlPlaneAccess(), actor)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAvailabilityHandler"/> class.</summary>
    /// <param name="availability">The availability matrix store.</param>
    /// <param name="environments">The environment store (target-environment visibility).</param>
    /// <param name="administration">The environment-administration governance service (current-administrator gating).</param>
    /// <param name="catalog">The workflow catalog (version existence + its sources, for readiness).</param>
    /// <param name="credentials">The source-credential store (readiness: a credential per source × environment).</param>
    /// <param name="access">Resolves the caller's <see cref="AccessContext"/> and deployment identity per request.</param>
    /// <param name="actor">The audit actor recorded on writes.</param>
    internal ArazzoControlPlaneAvailabilityHandler(IAvailabilityStore availability, IEnvironmentStore environments, SecuredEnvironmentAdministration administration, ISecuredWorkflowCatalog catalog, ISourceCredentialStore credentials, ControlPlaneAccess access, string actor = "control-plane")
    {
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(administration);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(actor);
        this.availability = availability;
        this.environments = environments;
        this.administration = administration;
        this.catalog = catalog;
        this.credentials = credentials;
        this.access = access;
        this.actor = actor;
    }

    /// <inheritdoc/>
    public async ValueTask<ListVersionAvailabilityResult> HandleListVersionAvailabilityAsync(ListVersionAvailabilityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;

        // Visibility: the availability of a version is readable by anyone who can read the version itself.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return ListVersionAvailabilityResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using AvailabilityPage page = await this.availability.ListByVersionAsync(baseWorkflowId, versionNumber, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // The list body is built inline and consumed in place: AvailabilityList.Build scopes its result to the
        // `in entries` argument (and the span-bound token), so it cannot be returned from a helper (CS8347).
        page.Entries.TransferOwnershipTo(workspace);
        IReadOnlyList<AvailabilityEntry> versionEntries = page.Entries;
        ReadOnlyMemory<byte> versionNextToken = page.NextPageToken;
        Models.AvailabilityList.Source<IReadOnlyList<AvailabilityEntry>> versionBody = Models.AvailabilityList.Build(
            in versionEntries,
            availability: Models.AvailabilityList.AvailabilityEntryArray.Build(in versionEntries, BuildEntries),
            nextPageToken: versionNextToken.IsEmpty ? default : (Models.JsonString.Source)versionNextToken.Span);
        return ListVersionAvailabilityResult.Ok(versionBody, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListEnvironmentAvailabilityResult> HandleListEnvironmentAvailabilityAsync(ListEnvironmentAvailabilityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string environment = (string)parameters.Name;

        // Visibility: the availability in an environment is readable by anyone whose reach admits the environment.
        using (ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (environmentDoc is null)
            {
                return ListEnvironmentAvailabilityResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
            }
        }

        int limit = parameters.Limit.IsNotUndefined() ? (int)parameters.Limit : 100;
        JsonString pageToken = JsonString.From(parameters.PageToken);
        using AvailabilityPage page = await this.availability.ListByEnvironmentAsync(environment, limit, pageToken, cancellationToken).ConfigureAwait(false);

        // Built inline and consumed in place (see HandleListVersionAvailabilityAsync) — the body cannot escape a helper.
        page.Entries.TransferOwnershipTo(workspace);
        IReadOnlyList<AvailabilityEntry> envEntries = page.Entries;
        ReadOnlyMemory<byte> envNextToken = page.NextPageToken;
        Models.AvailabilityList.Source<IReadOnlyList<AvailabilityEntry>> envBody = Models.AvailabilityList.Build(
            in envEntries,
            availability: Models.AvailabilityList.AvailabilityEntryArray.Build(in envEntries, BuildEntries),
            nextPageToken: envNextToken.IsEmpty ? default : (Models.JsonString.Source)envNextToken.Span);
        return ListEnvironmentAvailabilityResult.Ok(envBody, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<MakeVersionAvailableResult> HandleMakeVersionAvailableAsync(MakeVersionAvailableParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string environment = (string)parameters.Environment;

        // Governance: the target environment must be in reach (404) and the caller a current administrator of it (403).
        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return MakeVersionAvailableResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate == GovernanceGate.Forbidden)
        {
            return MakeVersionAvailableResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        // The version must exist and be readable; its sources drive the readiness gate.
        using (ParsedJsonDocument<CatalogVersion>? version = await this.catalog.GetAsync(baseWorkflowId, versionNumber, this.access.Current(), cancellationToken).ConfigureAwait(false))
        {
            if (version is null)
            {
                return MakeVersionAvailableResult.NotFound(VersionNotFoundProblem(baseWorkflowId, versionNumber), workspace);
            }

            // Readiness (§7.7): the version may be made available only where every source it references resolves a
            // credential in the target environment. A missing credential for even one source blocks promotion (409).
            List<string> missing = await this.MissingSourcesAsync(version.RootElement, environment, cancellationToken).ConfigureAwait(false);
            if (missing.Count > 0)
            {
                return MakeVersionAvailableResult.Conflict(NotReadyProblem(baseWorkflowId, versionNumber, environment, missing), workspace);
            }
        }

        (ParsedJsonDocument<AvailabilityEntry> entry, bool created) = await this.availability.MakeAvailableAsync(baseWorkflowId, versionNumber, environment, this.actor, cancellationToken).ConfigureAwait(false);
        workspace.TakeOwnership(entry);
        Models.AvailabilityEntry.Source body = Models.AvailabilityEntry.From(entry.RootElement);
        return created
            ? MakeVersionAvailableResult.Created(body, workspace)
            : MakeVersionAvailableResult.Ok(body, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<WithdrawVersionAvailabilityResult> HandleWithdrawVersionAvailabilityAsync(WithdrawVersionAvailabilityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        int versionNumber = (int)parameters.VersionNumber;
        string environment = (string)parameters.Environment;

        GovernanceGate gate = await this.AuthorizeEnvironmentAdminAsync(environment, cancellationToken).ConfigureAwait(false);
        if (gate == GovernanceGate.NotFound)
        {
            return WithdrawVersionAvailabilityResult.NotFound(EnvironmentNotFoundProblem(environment), workspace);
        }

        if (gate == GovernanceGate.Forbidden)
        {
            return WithdrawVersionAvailabilityResult.Forbidden(NotAdministratorProblem(environment), workspace);
        }

        bool withdrawn = await this.availability.WithdrawAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
        return withdrawn
            ? WithdrawVersionAvailabilityResult.NoContent()
            : WithdrawVersionAvailabilityResult.NotFound(AvailabilityNotFoundProblem(baseWorkflowId, versionNumber, environment), workspace);
    }

    // Each availability row is congruent with the persisted entry — a free whole-document re-wrap
    // (Models.AvailabilityEntry.From). The entries reference the pooled documents handed to the workspace by the caller.
    private static void BuildEntries(in IReadOnlyList<AvailabilityEntry> entries, ref Models.AvailabilityList.AvailabilityEntryArray.Builder array)
    {
        foreach (AvailabilityEntry entry in entries)
        {
            array.AddItem(Models.AvailabilityEntry.From(entry));
        }
    }

    // Visibility-then-membership gate on the target environment (mirrors the environments governance gate): an
    // environment outside reach is not found; a non-administrator is refused.
    private async ValueTask<GovernanceGate> AuthorizeEnvironmentAdminAsync(string environment, CancellationToken cancellationToken)
    {
        using ParsedJsonDocument<Environment>? environmentDoc = await this.environments.GetAsync(environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
        if (environmentDoc is null)
        {
            return GovernanceGate.NotFound;
        }

        using ParsedJsonDocument<EnvironmentAdministrators>? record = await this.administration.GetAdministratorsAsync(environment, cancellationToken).ConfigureAwait(false);
        return record?.RootElement.IsAdministeredBy(this.CallerIdentity()) == true
            ? GovernanceGate.Authorized
            : GovernanceGate.Forbidden;
    }

    // The sources the version references that have no usable credential in the target environment (readiness, §7.7).
    private async ValueTask<List<string>> MissingSourcesAsync(CatalogVersion version, string environment, CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (CatalogSourceRef source in version.SourcesValue.ToList())
        {
            using ParsedJsonDocument<SourceCredentialBinding>? binding = await this.credentials.GetAsync(source.Name, environment, this.access.Current(), cancellationToken).ConfigureAwait(false);
            if (binding is null)
            {
                missing.Add(source.Name);
            }
        }

        return missing;
    }

    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    private static Models.ProblemDetails.Source EnvironmentNotFoundProblem(string environment)
        => Problem("environment-not-found", "Environment not found", 404, $"No environment named '{environment}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source VersionNotFoundProblem(string baseWorkflowId, int versionNumber)
        => Problem("version-not-found", "Workflow version not found", 404, $"No version {versionNumber} of workflow '{baseWorkflowId}' exists, or it is outside your reach.");

    private static Models.ProblemDetails.Source AvailabilityNotFoundProblem(string baseWorkflowId, int versionNumber, string environment)
        => Problem("availability-not-found", "Not available", 404, $"Version {versionNumber} of workflow '{baseWorkflowId}' is not available in environment '{environment}'.");

    private static Models.ProblemDetails.Source NotAdministratorProblem(string environment)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of environment '{environment}'.");

    private static Models.ProblemDetails.Source NotReadyProblem(string baseWorkflowId, int versionNumber, string environment, IReadOnlyList<string> missing)
        => Problem("environment-not-ready", "Environment not ready", 409, $"Version {versionNumber} of workflow '{baseWorkflowId}' cannot be made available in '{environment}': no credential for {string.Join(", ", missing)}.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    private enum GovernanceGate
    {
        NotFound,
        Forbidden,
        Authorized,
    }
}