// <copyright file="ArazzoControlPlaneProvidersHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The connected-providers API (ADR 0052): the provider registry with the caller's per-provider
/// connection state, begin/complete a provider sign-in (the brokered popup — authorize, callback,
/// server-side code exchange), and disconnect. Token custody is the shared
/// <see cref="ProviderBroker"/>'s, keyed by <c>(principal, provider)</c>; the callback
/// authenticates by its single-use state (a top-level navigation carries no bearer token). A
/// deployment that registers no providers lists an empty registry and refuses the auth
/// operations.
/// </summary>
public sealed class ArazzoControlPlaneProvidersHandler : IApiProvidersHandler
{
    private const string ProblemBase = "https://corvus-oss.org/arazzo/control-plane/problems/";

    private readonly ProviderBroker? broker;
    private readonly ControlPlaneAccess access;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext;
    private readonly string subjectClaimType;

    /// <summary>Initializes a new instance of the <see cref="ArazzoControlPlaneProvidersHandler"/> class.</summary>
    /// <param name="broker">The deployment's provider broker; <see langword="null"/> lists an empty registry and refuses the auth operations.</param>
    /// <param name="access">Resolves the caller's identity (the token-custody key).</param>
    /// <param name="httpContext">Reads the authenticated principal in the modes whose access binding carries none (ScopesOnly).</param>
    /// <param name="subjectClaimType">The claim naming the authenticated subject (the custody key's fallback dimension).</param>
    internal ArazzoControlPlaneProvidersHandler(
        ProviderBroker? broker,
        ControlPlaneAccess access,
        Microsoft.AspNetCore.Http.IHttpContextAccessor? httpContext = null,
        string subjectClaimType = "sub")
    {
        ArgumentNullException.ThrowIfNull(access);
        ArgumentNullException.ThrowIfNull(subjectClaimType);
        this.broker = broker;
        this.access = access;
        this.httpContext = httpContext;
        this.subjectClaimType = subjectClaimType;
    }

    /// <inheritdoc/>
    public ValueTask<ListProvidersResult> HandleListProvidersAsync(ListProvidersParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        // No registry is not an error for a read: an empty listing means the pane simply offers no
        // provider affordance. Connection state is per caller; an unresolvable principal reads as
        // disconnected everywhere (it could hold no custody row).
        var rows = new List<ProviderRow>();
        if (this.broker is { } providers)
        {
            string? principal = this.PrincipalKey();
            foreach (ConnectedProviderOptions entry in providers.Providers)
            {
                rows.Add(new ProviderRow(entry, principal is not null && providers.IsConnected(principal, entry.Name!)));
            }
        }

        // The registry is deployment configuration (a handful of entries), projected closure-free
        // through the context-threaded Build over the row list.
        Models.ProviderList.Source<List<ProviderRow>> body = Models.ProviderList.Build(
            in rows,
            providers: Models.ProviderList.ProviderSummaryArray.Build(in rows, BuildProviders));
        return ValueTask.FromResult(ListProvidersResult.Ok(body, workspace));
    }

    /// <inheritdoc/>
    public async ValueTask<BeginProviderAuthResult> HandleBeginProviderAuthAsync(BeginProviderAuthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } providers)
        {
            return BeginProviderAuthResult.BadRequest(NotConfiguredProblem(), workspace);
        }

        if (this.PrincipalKey() is not { } principal)
        {
            return BeginProviderAuthResult.BadRequest(
                Problem("provider-identity-unresolvable", "Identity unresolvable", 400, "A provider connection binds to the calling principal, but no stable principal identity resolves for this caller."), workspace);
        }

        string providerName = (string)parameters.Provider;
        (ProviderBroker.BeginOutcome outcome, string? authorizeUrl, string? state) = await providers.BeginAuthAsync(providerName, principal, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            ProviderBroker.BeginOutcome.Success => BeginProviderAuthResult.Ok(
                new((ref Models.ProviderAuthStart.Builder b) => b.Create(authorizeUrl: authorizeUrl!, state: state!)), workspace),
            ProviderBroker.BeginOutcome.UnknownProvider => BeginProviderAuthResult.BadRequest(
                Problem("provider-unknown", "Unknown provider", 400, $"No connected provider named '{providerName}' is registered."), workspace),
            _ => BeginProviderAuthResult.BadRequest(
                Problem("provider-discovery-failed", "Discovery failed", 400, $"Provider '{providerName}' names an OIDC issuer whose discovery document could not be resolved; try again, or check the control plane's outbound connectivity."), workspace),
        };
    }

    /// <inheritdoc/>
    public async ValueTask<CompleteProviderAuthResult> HandleCompleteProviderAuthAsync(CompleteProviderAuthParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } providers)
        {
            return CompleteProviderAuthResult.BadRequest(NotConfiguredProblem(), workspace);
        }

        ProviderBroker.CompleteOutcome outcome = await providers.CompleteAuthAsync((string)parameters.Provider, (string)parameters.State, (string)parameters.Code, cancellationToken).ConfigureAwait(false);
        return outcome switch
        {
            ProviderBroker.CompleteOutcome.Success => CompleteProviderAuthResult.Ok(),
            ProviderBroker.CompleteOutcome.InvalidState => CompleteProviderAuthResult.BadRequest(
                Problem("provider-invalid-state", "Invalid state", 400, "The state is unknown, expired, already used, or bound to a different provider; begin the sign-in again."), workspace),
            _ => CompleteProviderAuthResult.BadRequest(
                Problem("provider-exchange-failed", "Exchange failed", 400, "The provider refused the code exchange, or could not be reached from the control plane (check outbound TLS/proxy); begin the sign-in again."), workspace),
        };
    }

    /// <inheritdoc/>
    public ValueTask<DeleteProviderSessionResult> HandleDeleteProviderSessionAsync(DeleteProviderSessionParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.broker is not { } providers)
        {
            return ValueTask.FromResult(DeleteProviderSessionResult.BadRequest(NotConfiguredProblem(), workspace));
        }

        // Idempotent by custody key: an unknown provider name (or an already-absent session)
        // removes nothing and still reports 204.
        if (this.PrincipalKey() is { } principal)
        {
            providers.Disconnect(principal, (string)parameters.Provider);
        }

        return ValueTask.FromResult(DeleteProviderSessionResult.NoContent());
    }

    // ── registry projection (closure-free Build<TContext> over the configuration rows) ────────────
    private static void BuildProviders(in List<ProviderRow> rows, ref Models.ProviderList.ProviderSummaryArray.Builder array)
    {
        foreach (ProviderRow row in rows)
        {
            array.AddItem(Models.ProviderSummary.Build(in row, BuildProvider));
        }
    }

    private static void BuildProvider(in ProviderRow row, ref Models.ProviderSummary.Builder b)
    {
        // The entry is validated configuration (Name/Hosts present); the secret REFERENCE and the
        // client registration deliberately never ride this API.
        b.Create(
            in row,
            connected: row.Connected,
            hosts: Models.ProviderSummary.JsonStringArray.Build(in row, BuildHosts),
            name: row.Entry.Name!,
            displayName: row.Entry.DisplayName is { } displayName ? (Models.JsonString.Source)displayName : default);
    }

    private static void BuildHosts(in ProviderRow row, ref Models.ProviderSummary.JsonStringArray.Builder array)
    {
        foreach (string host in row.Entry.Hosts)
        {
            array.AddItem(host);
        }
    }

    private static Models.ProblemDetails.Source NotConfiguredProblem()
        => Problem("providers-not-configured", "No connected providers", 400, "This deployment registers no connected providers.");

    private static Models.ProblemDetails.Source Problem(string type, string title, int status, string detail)
        => new((ref Models.ProblemDetails.Builder b) => b.Create(
            detail: detail,
            status: status,
            title: title,
            type: ProblemBase + type));

    // The token-custody key (§4.7): shared with the GitHub surface, so one signed-in principal
    // keys one custody row per provider whichever surface began the sign-in.
    private string? PrincipalKey() => PrincipalCustodyKey.Resolve(this.access, this.httpContext, this.subjectClaimType);

    // One registry row with the caller's connection state, threaded as the build context.
    private readonly record struct ProviderRow(ConnectedProviderOptions Entry, bool Connected);
}