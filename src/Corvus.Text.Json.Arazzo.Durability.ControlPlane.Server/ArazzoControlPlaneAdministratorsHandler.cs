// <copyright file="ArazzoControlPlaneAdministratorsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// Implements the generated <see cref="IApiAdministratorsHandler"/> over an <see cref="IWorkflowCatalogClient"/> — the
/// control-plane surface that manages a base workflow id's administrator set (design §15). The endpoints are gated by
/// the <c>administrators:read</c>/<c>administrators:write</c> capability scopes.
/// </summary>
/// <remarks>
/// <para><strong>Identity model (§15).</strong> An administrator is a deployment-stamped <c>sys:</c> identity (the same
/// unforgeable internal tags a catalogued version is stamped with), and the set is governed by current-administrator
/// membership — never reach. The operator-facing API never exposes raw internal tags: each administrator is named by
/// the grant <c>{dimension, value}</c> the deployment maps it to (the same mapping the §13 usage grants use, via
/// <see cref="ControlPlaneRowSecurityPolicy.ResolveUsageGrants"/> / <see cref="ControlPlaneRowSecurityPolicy.DescribeUsageScope"/>).
/// The caller's own identity is read from the deployment's row-security policy, so administration is meaningful only
/// when a policy is configured.</para>
/// <para>An unknown base id and a caller who is not a current administrator are refused identically (403,
/// non-disclosing). A concurrent change that loses the optimistic-concurrency race conflicts (409). Removing the last
/// administrator is refused (409) — a workflow always has at least one. When the catalog client has no administrator
/// store, mutation is unavailable (409); listing still works (the version-1-derived sole administrator).</para>
/// </remarks>
public sealed class ArazzoControlPlaneAdministratorsHandler : IApiAdministratorsHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly IWorkflowCatalogClient catalog;
    private readonly ControlPlaneAccess access;

    /// <summary>Initializes a new, unscoped instance (the caller resolves to no identity — administration management
    /// requires a configured row-security policy).</summary>
    /// <param name="catalog">The catalog client that owns the administrator store and the administration operations.</param>
    public ArazzoControlPlaneAdministratorsHandler(IWorkflowCatalogClient catalog)
        : this(catalog, new ControlPlaneAccess())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneAdministratorsHandler"/> class.</summary>
    /// <param name="catalog">The catalog client that owns the administrator store and the administration operations.</param>
    /// <param name="access">Resolves the caller's deployment identity per request and maps administrator grants to and
    /// from internal tags. Unscoped (no identity) when no row security is configured.</param>
    internal ArazzoControlPlaneAdministratorsHandler(IWorkflowCatalogClient catalog, ControlPlaneAccess access)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(access);
        this.catalog = catalog;
        this.access = access;
    }

    /// <inheritdoc/>
    public async ValueTask<ListAdministratorsResult> HandleListAdministratorsAsync(ListAdministratorsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SecurityTagSet> administrators = await this.catalog.GetAdministratorsAsync((string)parameters.BaseWorkflowId, cancellationToken).ConfigureAwait(false);
        return ListAdministratorsResult.Ok(this.ToList(administrators), workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<AddAdministratorResult> HandleAddAdministratorAsync(AddAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        SecurityTagSet newAdministrator;
        try
        {
            newAdministrator = this.IdentityFromGrant((string)parameters.Body.DimensionValue, (string)parameters.Body.Value);
        }
        catch (ArgumentException ex)
        {
            return AddAdministratorResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.AddAdministratorAsync(baseWorkflowId, newAdministrator, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            return AddAdministratorResult.Ok(this.ToList(administrators), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return AddAdministratorResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return AddAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return AddAdministratorResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TransferAdministrationResult> HandleTransferAdministrationAsync(TransferAdministrationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;
        List<SecurityTagSet> newAdministrators;
        try
        {
            newAdministrators = [];
            foreach (Models.AdministratorIdentity identity in parameters.Body.Administrators.EnumerateArray())
            {
                newAdministrators.Add(this.IdentityFromGrant((string)identity.DimensionValue, (string)identity.Value));
            }
        }
        catch (ArgumentException ex)
        {
            return TransferAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator identity", 400, ex.Message), workspace);
        }

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.TransferAdministrationAsync(baseWorkflowId, newAdministrators, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            return TransferAdministrationResult.Ok(this.ToList(administrators), workspace);
        }
        catch (ArgumentException ex)
        {
            return TransferAdministrationResult.BadRequest(Problem("invalid-administrator", "Invalid administrator set", 400, ex.Message), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return TransferAdministrationResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return TransferAdministrationResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return TransferAdministrationResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RemoveAdministratorResult> HandleRemoveAdministratorAsync(RemoveAdministratorParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string baseWorkflowId = (string)parameters.BaseWorkflowId;

        // A grant that does not map to a deployment identity (or the unscoped default) resolves to the empty set, which
        // is never a member — so removal becomes an idempotent no-op. We still route through the catalog so the
        // current-administrator gate (403) runs first and membership stays non-disclosing.
        SecurityTagSet administrator = SecurityTagSet.FromTags(this.access.ResolveUsageGrants([new CredentialUsageGrant((string)parameters.Dimension, (string)parameters.Value)]));

        try
        {
            IReadOnlyList<SecurityTagSet> administrators = await this.catalog.RemoveAdministratorAsync(baseWorkflowId, administrator, this.CallerIdentity(), cancellationToken).ConfigureAwait(false);
            return RemoveAdministratorResult.Ok(this.ToList(administrators), workspace);
        }
        catch (ArgumentException ex)
        {
            // Removing the last administrator would orphan the workflow — refused (a workflow always has one).
            return RemoveAdministratorResult.Conflict(Problem("last-administrator", "Cannot remove the last administrator", 409, ex.Message), workspace);
        }
        catch (WorkflowAdministrationException)
        {
            return RemoveAdministratorResult.Forbidden(NotAdministratorProblem(baseWorkflowId), workspace);
        }
        catch (WorkflowAdministrationConflictException ex)
        {
            return RemoveAdministratorResult.Conflict(ConflictProblem(ex), workspace);
        }
        catch (NotSupportedException ex)
        {
            return RemoveAdministratorResult.Conflict(UnavailableProblem(ex), workspace);
        }
    }

    // The caller's deployment identity (the internal tags the row-security policy stamps for the current principal).
    // Empty when unscoped, so the current-administrator membership check refuses every mutation (403) — administration
    // management requires a configured policy.
    private SecurityTagSet CallerIdentity() => SecurityTagSet.FromTags(this.access.InternalTags());

    // Maps an operator-facing administrator grant {dimension, value} to its unforgeable internal identity tags (the
    // inverse of the list response's DescribeUsageScope). A grant the deployment policy declines to map (or the
    // unscoped default) resolves to nothing — rejected, so an unmappable identity can never be named.
    private SecurityTagSet IdentityFromGrant(string dimension, string value)
    {
        IReadOnlyList<SecurityTag> tags = this.access.ResolveUsageGrants([new CredentialUsageGrant(dimension, value)]);
        if (tags.Count == 0)
        {
            throw new ArgumentException($"The administrator grant '{dimension}={value}' does not resolve to a deployment identity.");
        }

        return SecurityTagSet.FromTags(tags);
    }

    private static Models.AdministratorIdentity.Source ToIdentity(CredentialUsageGrant grant)
        => new((ref Models.AdministratorIdentity.Builder b) => b.Create(grant.Dimension, grant.Value));

    private static Models.ProblemDetails.Source NotAdministratorProblem(string baseWorkflowId)
        => Problem("not-administrator", "Not an administrator", 403, $"You are not a current administrator of '{baseWorkflowId}', or it has no established administration.");

    private static Models.ProblemDetails.Source ConflictProblem(WorkflowAdministrationConflictException ex)
        => Problem("administration-conflict", "Administration changed concurrently", 409, ex.Message);

    private static Models.ProblemDetails.Source UnavailableProblem(NotSupportedException ex)
        => Problem("administration-unavailable", "Administration management unavailable", 409, ex.Message);

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    // Describes each administrator (a set of internal identity tags) back as the operator-facing grants it maps from —
    // the inverse of IdentityFromGrant. Internal tags are never exposed raw; unscoped deployments describe nothing.
    private Models.AdministratorList.Source ToList(IReadOnlyList<SecurityTagSet> administrators)
        => new((ref Models.AdministratorList.Builder b) => b.Create(
            administrators: new Models.AdministratorList.AdministratorIdentityArray.Source((ref Models.AdministratorList.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (SecurityTagSet administrator in administrators)
                {
                    foreach (CredentialUsageGrant grant in this.access.DescribeUsageScope(administrator.ToList()))
                    {
                        ab.AddItem(ToIdentity(grant));
                    }
                }
            })));
}