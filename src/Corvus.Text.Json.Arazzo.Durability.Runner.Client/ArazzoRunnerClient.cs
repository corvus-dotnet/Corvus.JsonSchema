// <copyright file="ArazzoRunnerClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client.Models;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// A runner's whole reach into durable run state (ADR 0065): claim a run, hold its lease, and load and save its
/// checkpoint, all over the runner API. A runner using this binds no store SDK and holds no store credential — it
/// authenticates as its own machine principal and the control plane, which owns the store, performs every read and
/// write on its behalf.
/// </summary>
/// <remarks>
/// <para>
/// The lease token for each claimed run is held here and presented automatically. That is not just convenience: a
/// runner that never handles the token cannot log it, persist it, or send it for the wrong run, and the token is the
/// only thing besides the authenticated principal that authorises an operation on a run.
/// </para>
/// <para>
/// <see cref="Checkpoints"/> is an ordinary <see cref="IWorkflowCheckpointStore"/>, so a run resumes and advances
/// through it exactly as it would over a database-backed store. The difference is invisible to the run and total from
/// the deployment's point of view.
/// </para>
/// </remarks>
public sealed class ArazzoRunnerClient : IAsyncDisposable
{
    private readonly IApiClaimsClient claims;
    private readonly IApiLeasesClient leases;
    private readonly IApiCheckpointsClient checkpoints;
    private readonly ConcurrentDictionary<string, string> heldLeases = new(StringComparer.Ordinal);
    private readonly bool ownsClients;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClient"/> class over an API transport.</summary>
    /// <param name="transport">The transport to the runner API host.</param>
    public ArazzoRunnerClient(IApiTransport transport)
        : this(new ApiClaimsClient(transport), new ApiLeasesClient(transport), new ApiCheckpointsClient(transport), ownsClients: true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClient"/> class over prepared clients.</summary>
    /// <param name="claims">The claims client.</param>
    /// <param name="leases">The leases client.</param>
    /// <param name="checkpoints">The checkpoints client.</param>
    /// <param name="ownsClients">Whether disposing this disposes the clients.</param>
    public ArazzoRunnerClient(IApiClaimsClient claims, IApiLeasesClient leases, IApiCheckpointsClient checkpoints, bool ownsClients = false)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(checkpoints);

        this.claims = claims;
        this.leases = leases;
        this.checkpoints = checkpoints;
        this.ownsClients = ownsClients;
        this.Checkpoints = new RunnerApiCheckpointStore(this);
    }

    /// <summary>
    /// Gets the checkpoint store a claimed run loads and saves through. Operations on a run this client does not hold a
    /// lease for throw <see cref="RunnerLeaseLostException"/> without a round trip, because there is nothing to present.
    /// </summary>
    public IWorkflowCheckpointStore Checkpoints { get; }

    /// <summary>
    /// Takes the first claimable run this runner can execute, and its lease.
    /// </summary>
    /// <param name="hostedVersions">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="lease">The lease duration to request; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed run, or <see langword="null"/> when nothing is claimable — the common case for an idle
    /// runner, and not an error.</returns>
    /// <exception cref="RunnerApiException">The API refused the claim.</exception>
    public async ValueTask<RunnerClaim?> TryClaimAsync(IReadOnlyCollection<string> hostedVersions, TimeSpan? lease = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostedVersions);

        // A generated client takes only the non-generic source for a request body: there is no Source<TContext>
        // overload to thread the versions through, though the server side emits exactly that for response bodies. So
        // the array is built through a callback that closes over them, and the closure is the generator's shape rather
        // than a choice made here — see #227, which is where that asymmetry gets fixed.
        ClaimRequest.HostedVersionsEntityArray.Source versions = ClaimRequest.HostedVersionsEntityArray.Build(
            (ref ClaimRequest.HostedVersionsEntityArray.Builder builder) =>
            {
                foreach (string version in hostedVersions)
                {
                    builder.AddItem(version);
                }
            });

        ClaimRequest.Source request = lease is { } requested
            ? ClaimRequest.Build(versions, (long)requested.TotalSeconds)
            : ClaimRequest.Build(versions);

        await using ClaimRunResponse response = await this.claims.ClaimRunAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == 204)
        {
            return null;
        }

        if (response.StatusCode != 200)
        {
            throw Refused("claim a run", response.StatusCode);
        }

        ClaimedRun claimed = response.OkBody;
        var runId = new WorkflowRunId((string)claimed.RunId);

        // The token is retained rather than returned: every later operation for this run presents it from here.
        this.heldLeases[runId.Value] = (string)claimed.Lease.Token;
        return new RunnerClaim(
            runId,
            (string)claimed.WorkflowId,
            (string)claimed.Environment,
            ((NodaTime.OffsetDateTime)claimed.Lease.ExpiresAt).ToDateTimeOffset(),
            (long)claimed.Lease.Epoch);
    }

    /// <summary>
    /// Extends the lease on a run this runner holds, so a long advance does not have it reclaimed as an orphan.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="extension">The extension to request; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>When the extended lease now lapses.</returns>
    /// <exception cref="RunnerLeaseLostException">The lease is no longer current, so the run may already be held by another runner.</exception>
    public async ValueTask<DateTimeOffset> RenewAsync(WorkflowRunId runId, TimeSpan? extension = null, CancellationToken cancellationToken = default)
    {
        string token = this.RequireLease(runId);
        LeaseRenewal.Source body = extension is { } requested
            ? LeaseRenewal.Build(leaseSeconds: (long)requested.TotalSeconds)
            : default;

        await using RenewLeaseResponse response = await this.leases.RenewLeaseAsync(runId.Value, token, body, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == 409)
        {
            this.heldLeases.TryRemove(runId.Value, out _);
            throw new RunnerLeaseLostException(runId);
        }

        if (response.StatusCode != 200)
        {
            throw Refused($"renew the lease for run '{runId.Value}'", response.StatusCode);
        }

        // The token does not change across an extension, so the held one stays valid.
        return ((NodaTime.OffsetDateTime)response.OkBody.ExpiresAt).ToDateTimeOffset();
    }

    /// <summary>
    /// Hands a run back so another runner may claim it without waiting for the lease to expire.
    /// </summary>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when this runner no longer holds the run.</returns>
    /// <remarks>Releasing a run this client does not hold does nothing and is not an error, so a runner can release in a
    /// <c>finally</c> without first working out whether it still holds the lease.</remarks>
    public async ValueTask ReleaseAsync(WorkflowRunId runId, CancellationToken cancellationToken = default)
    {
        if (!this.heldLeases.TryRemove(runId.Value, out string? token))
        {
            return;
        }

        await using ReleaseLeaseResponse response = await this.leases.ReleaseLeaseAsync(runId.Value, token, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != 204)
        {
            throw Refused($"release the lease for run '{runId.Value}'", response.StatusCode);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (!this.ownsClients)
        {
            return;
        }

        await this.claims.DisposeAsync().ConfigureAwait(false);
        await this.leases.DisposeAsync().ConfigureAwait(false);
        await this.checkpoints.DisposeAsync().ConfigureAwait(false);
    }

    internal static RunnerApiException Refused(string what, int status)
        => new((System.Net.HttpStatusCode)status, $"The runner API refused to {what} ({status}).");

    internal IApiCheckpointsClient CheckpointsClient => this.checkpoints;

    internal string RequireLease(WorkflowRunId runId)
        => this.heldLeases.TryGetValue(runId.Value, out string? token)
            ? token
            : throw new RunnerLeaseLostException(runId);

    internal void Forget(WorkflowRunId runId) => this.heldLeases.TryRemove(runId.Value, out _);
}