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
    private readonly IApiCatalogClient catalog;
    private readonly ConcurrentDictionary<WorkflowRunAddress, HeldLease> heldLeases = new();
    private readonly ConcurrentDictionary<WorkflowRunAddress, RunnerQuotaHold> quotaHolds = new();
    private readonly RunnerQuotaHoldOptions holdOptions;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClients;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClient"/> class over an API transport.</summary>
    /// <param name="transport">The transport to the runner API host.</param>
    /// <param name="holdOptions">How long this runner will wait out a quota refusal; defaults are used when omitted.</param>
    /// <param name="timeProvider">The time source for quota holds; defaults to <see cref="TimeProvider.System"/>.</param>
    public ArazzoRunnerClient(IApiTransport transport, RunnerQuotaHoldOptions? holdOptions = null, TimeProvider? timeProvider = null)
        : this(new ApiClaimsClient(transport), new ApiLeasesClient(transport), new ApiCheckpointsClient(transport), new ApiCatalogClient(transport), ownsClients: true, holdOptions, timeProvider)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClient"/> class over prepared clients.</summary>
    /// <param name="claims">The claims client.</param>
    /// <param name="leases">The leases client.</param>
    /// <param name="checkpoints">The checkpoints client.</param>
    /// <param name="catalog">The catalog client, which serves what this runner may execute and the artifacts for it.</param>
    /// <param name="ownsClients">Whether disposing this disposes the clients.</param>
    /// <param name="holdOptions">How long this runner will wait out a quota refusal; defaults are used when omitted.</param>
    /// <param name="timeProvider">The time source for quota holds; defaults to <see cref="TimeProvider.System"/>.</param>
    public ArazzoRunnerClient(IApiClaimsClient claims, IApiLeasesClient leases, IApiCheckpointsClient checkpoints, IApiCatalogClient catalog, bool ownsClients = false, RunnerQuotaHoldOptions? holdOptions = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(catalog);

        this.claims = claims;
        this.leases = leases;
        this.checkpoints = checkpoints;
        this.catalog = catalog;
        this.ownsClients = ownsClients;
        this.holdOptions = holdOptions ?? new RunnerQuotaHoldOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;
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
    // Not async, and it must stay that way: Source<TContext> holds its context by reference, so it cannot live across
    // an await. Building the request and starting the send happen here, synchronously; the response is read in the
    // async continuation below. The generated client's own methods are shaped the same way and for the same reason.
    public ValueTask<RunnerClaim?> TryClaimAsync(IReadOnlyCollection<string> hostedVersions, TimeSpan? lease = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostedVersions);

        // Typed explicitly rather than inferred: a conditional whose branches are `long` and `default` takes `long` as
        // its natural type, so an absent lease would become leaseSeconds 0 — which the schema's minimum of 1 refuses.
        ClaimRequest.LeaseSecondsEntity.Source leaseSeconds = lease is { } requested
            ? (ClaimRequest.LeaseSecondsEntity.Source)(long)requested.TotalSeconds
            : default;

        // The versions are threaded through as context rather than captured, so the request materialises in one pass
        // with no closure and no interim collection.
        ClaimRequest.Source<IReadOnlyCollection<string>> request = ClaimRequest.Build(
            in hostedVersions,
            ClaimRequest.HostedVersionsEntityArray.Build(
                in hostedVersions,
                static (in IReadOnlyCollection<string> versions, ref ClaimRequest.HostedVersionsEntityArray.Builder builder) =>
                {
                    foreach (string version in versions)
                    {
                        builder.AddItem(version);
                    }
                }),
            leaseSeconds);

        return this.ReadClaimAsync(this.claims.ClaimRunAsync(request, cancellationToken));
    }

    /// <summary>
    /// Lists every version this runner may execute, following the listing to its end.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The versions, resolved from this runner's environment bindings rather than from the catalog at large.</returns>
    /// <exception cref="RunnerApiException">The API refused the listing.</exception>
    public async ValueTask<IReadOnlyList<RunnerHostedVersion>> ListHostedVersionsAsync(CancellationToken cancellationToken = default)
    {
        var hosted = new List<RunnerHostedVersion>();
        byte[]? pageToken = null;

        while (true)
        {
            await using ListHostedVersionsResponse response = await this.FetchHostedPageAsync(pageToken, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != 200)
            {
                throw Refused("list the versions it may execute", response.StatusCode);
            }

            HostedVersions body = response.OkBody;
            foreach (HostedVersion version in body.Versions.EnumerateArray())
            {
                hosted.Add(new RunnerHostedVersion((string)version.BaseWorkflowId, (int)version.VersionNumber, (string)version.Hash));
            }

            if (body.NextPageToken.IsUndefined())
            {
                return hosted;
            }

            // Copied because the response owns the bytes and is about to be disposed. The token stays UTF-8 either
            // way: it is opaque to the runner and never becomes a string on the way through.
            using UnescapedUtf8JsonString tokenUtf8 = body.NextPageToken.GetUtf8String();
            pageToken = tokenUtf8.Span.ToArray();
        }
    }

    // Not async, for the same reason as TryClaimAsync: the request Source is a ref struct and cannot live across the
    // await, so it is built and handed to the send here, and the response is read by the caller's continuation.
    private ValueTask<ListHostedVersionsResponse> FetchHostedPageAsync(byte[]? pageToken, CancellationToken cancellationToken)
        => this.catalog.ListHostedVersionsAsync(
            pageToken is null ? default : (Models.GetHostedVersionsPageToken.Source)pageToken.AsSpan(),
            default,
            cancellationToken);

    /// <summary>
    /// Takes the runs whose durable timer is now due, and their leases.
    /// </summary>
    /// <param name="hostedVersions">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="limit">The most runs to claim; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="lease">The lease duration to request; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed runs, empty when no timer is due.</returns>
    /// <exception cref="RunnerApiException">The API refused the sweep.</exception>
    /// <remarks>
    /// There is no cutoff to pass. The server resumes against its own clock, so a runner cannot ask for timers that
    /// have not fired, whether deliberately or through a skewed clock of its own.
    /// </remarks>
    // Not async, for the same reason as TryClaimAsync: Source<TContext> holds its context by reference.
    public ValueTask<IReadOnlyList<RunnerClaim>> ClaimDueTimersAsync(IReadOnlyCollection<string> hostedVersions, int? limit = null, TimeSpan? lease = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hostedVersions);

        TimerClaimRequest.LeaseSecondsEntity.Source leaseSeconds = lease is { } requested
            ? (TimerClaimRequest.LeaseSecondsEntity.Source)(long)requested.TotalSeconds
            : default;
        TimerClaimRequest.LimitEntity.Source wanted = limit is { } count
            ? (TimerClaimRequest.LimitEntity.Source)(long)count
            : default;

        TimerClaimRequest.Source<IReadOnlyCollection<string>> request = TimerClaimRequest.Build(
            in hostedVersions,
            TimerClaimRequest.HostedVersionsEntityArray.Build(
                in hostedVersions,
                static (in IReadOnlyCollection<string> versions, ref TimerClaimRequest.HostedVersionsEntityArray.Builder builder) =>
                {
                    foreach (string version in versions)
                    {
                        builder.AddItem(version);
                    }
                }),
            leaseSeconds,
            wanted);

        return this.ReadClaimsAsync(this.claims.ClaimDueTimersAsync(request, cancellationToken));
    }

    /// <summary>
    /// Takes the runs awaiting a message on a channel, and their leases, so this runner can hand each of them the
    /// message it is holding.
    /// </summary>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The delivered message's correlation token, or <see langword="null"/> to match every run awaiting the channel, whatever correlation each awaits.</param>
    /// <param name="hostedVersions">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="limit">The most runs to claim; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="lease">The lease duration to request; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed runs, empty when nothing awaits that message.</returns>
    /// <exception cref="RunnerApiException">The API refused the sweep.</exception>
    /// <remarks>
    /// The payload is not sent. This asks which runs a message can resume, not what it said, so the runner keeps the
    /// only copy of the message and delivers it to each run itself.
    /// </remarks>
    // Not async, for the same reason as TryClaimAsync: Source<TContext> holds its context by reference.
    public ValueTask<IReadOnlyList<RunnerClaim>> ClaimAwaitingMessageAsync(string channel, string? correlationId, IReadOnlyCollection<string> hostedVersions, int? limit = null, TimeSpan? lease = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(hostedVersions);

        MessageClaimRequest.LeaseSecondsEntity.Source leaseSeconds = lease is { } requested
            ? (MessageClaimRequest.LeaseSecondsEntity.Source)(long)requested.TotalSeconds
            : default;
        MessageClaimRequest.LimitEntity.Source wanted = limit is { } count
            ? (MessageClaimRequest.LimitEntity.Source)(long)count
            : default;
        MessageClaimRequest.CorrelationIdEntity.Source correlation = correlationId is { } token
            ? (MessageClaimRequest.CorrelationIdEntity.Source)token
            : default;

        MessageClaimRequest.Source<IReadOnlyCollection<string>> request = MessageClaimRequest.Build(
            in hostedVersions,
            channel,
            MessageClaimRequest.HostedVersionsEntityArray.Build(
                in hostedVersions,
                static (in IReadOnlyCollection<string> versions, ref MessageClaimRequest.HostedVersionsEntityArray.Builder builder) =>
                {
                    foreach (string version in versions)
                    {
                        builder.AddItem(version);
                    }
                }),
            correlation,
            leaseSeconds,
            wanted);

        return this.ReadClaimsAsync(this.claims.ClaimAwaitingMessageAsync(request, cancellationToken));
    }

    private async ValueTask<IReadOnlyList<RunnerClaim>> ReadClaimsAsync(ValueTask<ClaimDueTimersResponse> pending)
    {
        await using ClaimDueTimersResponse response = await pending.ConfigureAwait(false);
        if (response.StatusCode != 200)
        {
            throw Refused("claim due timers", response.StatusCode);
        }

        return this.Retain(response.OkBody.Claims);
    }

    private async ValueTask<IReadOnlyList<RunnerClaim>> ReadClaimsAsync(ValueTask<ClaimAwaitingMessageResponse> pending)
    {
        await using ClaimAwaitingMessageResponse response = await pending.ConfigureAwait(false);
        if (response.StatusCode != 200)
        {
            throw Refused("claim runs awaiting a message", response.StatusCode);
        }

        return this.Retain(response.OkBody.Claims);
    }

    // Every claim's token is retained here, exactly as a single claim's is, so a runner working through a sweep never
    // handles a lease token itself.
    private List<RunnerClaim> Retain(ClaimedRuns.ClaimedRunArray claims)
    {
        var result = new List<RunnerClaim>(claims.GetArrayLength());
        foreach (ClaimedRun claimed in claims.EnumerateArray())
        {
            var runId = new WorkflowRunId((string)claimed.RunId);

            // The environment is half of the run's address (ADR 0065 §9): every later operation for this run names it
            // in the route, so the retention is keyed by the full address — the same id claimed in two environments is
            // two runs with two leases. One materialisation serves both the claim and the entry.
            string environment = (string)claimed.Environment;
            this.heldLeases[new WorkflowRunAddress(environment, runId)] = new HeldLease(environment, (string)claimed.Lease.Token);
            result.Add(new RunnerClaim(
                runId,
                (string)claimed.WorkflowId,
                environment,
                ((NodaTime.OffsetDateTime)claimed.Lease.ExpiresAt).ToDateTimeOffset(),
                (long)claimed.Lease.Epoch));
        }

        return result;
    }

    private async ValueTask<RunnerClaim?> ReadClaimAsync(ValueTask<ClaimRunResponse> pending)
    {
        await using ClaimRunResponse response = await pending.ConfigureAwait(false);
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

        // The token is retained rather than returned: every later operation for this run presents it from here, keyed
        // by the run's full address (ADR 0065 §9) — the environment is half the key and names the route.
        string environment = (string)claimed.Environment;
        this.heldLeases[new WorkflowRunAddress(environment, runId)] = new HeldLease(environment, (string)claimed.Lease.Token);
        return new RunnerClaim(
            runId,
            (string)claimed.WorkflowId,
            environment,
            ((NodaTime.OffsetDateTime)claimed.Lease.ExpiresAt).ToDateTimeOffset(),
            (long)claimed.Lease.Epoch);
    }

    /// <summary>
    /// Extends the lease on a run this runner holds, so a long advance does not have it reclaimed as an orphan.
    /// </summary>
    /// <param name="address">The run's <c>(environment, runId)</c> address (ADR 0065 decision 9).</param>
    /// <param name="extension">The extension to request; the server bounds it. Omit for the deployment's default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>When the extended lease now lapses.</returns>
    /// <exception cref="RunnerLeaseLostException">The lease is no longer current, so the run may already be held by another runner.</exception>
    public async ValueTask<DateTimeOffset> RenewAsync(WorkflowRunAddress address, TimeSpan? extension = null, CancellationToken cancellationToken = default)
    {
        WorkflowRunId runId = address.RunId;
        HeldLease held = this.RequireLease(address);

        RunnerQuotaHold hold = this.HoldFor(address);
        RenewLeaseResponse response = await this.SendRenewalAsync(runId, held, extension, cancellationToken).ConfigureAwait(false);

        // The renewal shares the advance's hold allowance rather than having its own. A renewal refused while a save is
        // also being refused is one overload, not two, and giving each operation a private budget is how a bounded hold
        // becomes an unbounded one by arithmetic.
        while (response.StatusCode == 429)
        {
            long? retryAfter = response.RetryAfterHeader.IsNotUndefined() ? (long)response.RetryAfterHeader : null;
            if (!await hold.TryWaitAsync(retryAfter, cancellationToken).ConfigureAwait(false))
            {
                QuotaProblem refusal = response.TooManyRequestsBody;
                var exhausted = new RunnerQuotaExhaustedException(
                    $"renew the lease for run '{runId.Value}'",
                    refusal.IsNotUndefined() && refusal.Quota.IsNotUndefined() ? (string)refusal.Quota : "unknown",
                    refusal.IsNotUndefined() && refusal.Counter.IsNotUndefined() ? (string)refusal.Counter : "unknown");
                await response.DisposeAsync().ConfigureAwait(false);
                throw exhausted;
            }

            await response.DisposeAsync().ConfigureAwait(false);
            response = await this.SendRenewalAsync(runId, held, extension, cancellationToken).ConfigureAwait(false);
        }

        await using RenewLeaseResponse owned = response;
        if (response.StatusCode == 409)
        {
            this.heldLeases.TryRemove(address, out _);
            this.quotaHolds.TryRemove(address, out _);
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
    /// <param name="address">The run's <c>(environment, runId)</c> address (ADR 0065 decision 9).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when this runner no longer holds the run.</returns>
    /// <remarks>Releasing a run this client does not hold does nothing and is not an error, so a runner can release in a
    /// <c>finally</c> without first working out whether it still holds the lease.</remarks>
    public async ValueTask ReleaseAsync(WorkflowRunAddress address, CancellationToken cancellationToken = default)
    {
        this.quotaHolds.TryRemove(address, out _);
        if (!this.heldLeases.TryRemove(address, out HeldLease held))
        {
            return;
        }

        await using ReleaseLeaseResponse response = await this.leases.ReleaseLeaseAsync(held.Environment, address.RunId.Value, held.Token, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode != 204)
        {
            throw Refused($"release the lease for run '{address.RunId.Value}'", response.StatusCode);
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

    // Not async, deliberately, and rebuilt per attempt: LeaseRenewal.Source holds its context by reference, so it
    // cannot live across an await. Building it and starting the send happen here, synchronously; the retry loop awaits
    // only the response. The claim path is shaped the same way and for the same reason.
    private ValueTask<RenewLeaseResponse> SendRenewalAsync(WorkflowRunId runId, HeldLease held, TimeSpan? extension, CancellationToken cancellationToken)
    {
        LeaseRenewal.Source body = extension is { } requested
            ? LeaseRenewal.Build(leaseSeconds: (long)requested.TotalSeconds)
            : default;

        return this.leases.RenewLeaseAsync(held.Environment, runId.Value, held.Token, body, cancellationToken);
    }

    internal static RunnerApiException Refused(string what, int status)
        => new((System.Net.HttpStatusCode)status, $"The runner API refused to {what} ({status}).");

    internal IApiCheckpointsClient CheckpointsClient => this.checkpoints;

    internal HeldLease RequireLease(in WorkflowRunAddress address)
        => this.heldLeases.TryGetValue(address, out HeldLease held)
            ? held
            : throw new RunnerLeaseLostException(address.RunId);

    internal void Forget(in WorkflowRunAddress address)
    {
        this.heldLeases.TryRemove(address, out _);
        this.quotaHolds.TryRemove(address, out _);
    }

    // One advance's allowance for waiting out quota refusals. Keyed by the run's address and dropped with the lease,
    // because the lease is held for exactly the advance: taken at the claim, given up when the advance ends.
    internal RunnerQuotaHold HoldFor(in WorkflowRunAddress address)
        => this.quotaHolds.GetOrAdd(address, static (_, s) => new RunnerQuotaHold(s.Options, s.Clock), (Options: this.holdOptions, Clock: this.timeProvider));

    /// <summary>
    /// What claiming a run retains for it: the lease token, and the environment half of the run's address (ADR 0065
    /// §9), which every later route for the run names.
    /// </summary>
    /// <param name="Environment">The run's home environment, echoed by the claim.</param>
    /// <param name="Token">The lease token, presented on every operation over the run.</param>
    internal readonly record struct HeldLease(string Environment, string Token);
}