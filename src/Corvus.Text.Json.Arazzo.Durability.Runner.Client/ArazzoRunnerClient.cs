// <copyright file="ArazzoRunnerClient.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;
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
    private static readonly TimeSpan SealKeyCheckInterval = TimeSpan.FromMinutes(1);

    private readonly IApiClaimsClient claims;
    private readonly IApiLeasesClient leases;
    private readonly IApiCheckpointsClient checkpoints;
    private readonly IApiCatalogClient catalog;
    private readonly IApiEnvironmentsClient environments;
    private readonly ConcurrentDictionary<string, SealKeyCheck> sealKeyChecks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<WorkflowRunAddress, HeldLease> heldLeases = new();
    private readonly ConcurrentDictionary<WorkflowRunAddress, RunnerQuotaHold> quotaHolds = new();
    private readonly RunnerQuotaHoldOptions holdOptions;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClients;
    private readonly SealingCheckpointStore? sealing;
    private readonly ITenantAnchorStore? anchors;
    private readonly RunnerKeyRing keyRing;
    private RunStartInputValidator? startInputs;
    private readonly ConcurrentDictionary<string, WaitIndexBlinder> blinders = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerClient"/> class over an API transport.</summary>
    /// <param name="transport">The transport to the runner API host.</param>
    /// <param name="holdOptions">How long this runner will wait out a quota refusal; defaults are used when omitted.</param>
    /// <param name="timeProvider">The time source for quota holds; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="keyRing">The runner's keys (ADR 0065 decision 10): with one, rows for the environments it holds are sealed on save and verified on load through a <see cref="SealingCheckpointStore"/>.</param>
    /// <param name="anchors">The tenant anchor store (ADR 0065 decision 6): with one, every environment on the ring is anchored, so a rollback, substitution or replay by the control plane faults the run at its next open. Required when the ring marks any environment sealed.</param>
    public ArazzoRunnerClient(IApiTransport transport, RunnerQuotaHoldOptions? holdOptions = null, TimeProvider? timeProvider = null, RunnerKeyRing? keyRing = null, ITenantAnchorStore? anchors = null)
        : this(new ApiClaimsClient(transport), new ApiLeasesClient(transport), new ApiCheckpointsClient(transport), new ApiCatalogClient(transport), ownsClients: true, holdOptions, timeProvider, keyRing, anchors, new ApiEnvironmentsClient(transport))
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
    /// <param name="keyRing">The runner's keys (ADR 0065 decision 10): with one, rows for the environments it holds are sealed on save and verified on load through a <see cref="SealingCheckpointStore"/>.</param>
    /// <param name="anchors">The tenant anchor store (ADR 0065 decision 6): with one, every environment on the ring is anchored, so a rollback, substitution or replay by the control plane faults the run at its next open. Required when the ring marks any environment sealed.</param>
    /// <param name="environments">The environments client, which serves the seal keys the control plane advertises for the runner's allowlist check (decision 10).</param>
    public ArazzoRunnerClient(IApiClaimsClient claims, IApiLeasesClient leases, IApiCheckpointsClient checkpoints, IApiCatalogClient catalog, bool ownsClients = false, RunnerQuotaHoldOptions? holdOptions = null, TimeProvider? timeProvider = null, RunnerKeyRing? keyRing = null, ITenantAnchorStore? anchors = null, IApiEnvironmentsClient? environments = null)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(leases);
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(catalog);

        this.environments = environments ?? throw new ArgumentNullException(nameof(environments), "The environments client serves the seal keys the control plane advertises, which the runner checks against its allowlist (ADR 0065 decision 10).");
        this.claims = claims;
        this.leases = leases;
        this.checkpoints = checkpoints;
        this.catalog = catalog;
        this.ownsClients = ownsClients;
        this.holdOptions = holdOptions ?? new RunnerQuotaHoldOptions();
        this.timeProvider = timeProvider ?? TimeProvider.System;

        // ADR 0065 decisions 4 and 10: with a key ring, every row this runner saves for an environment the ring
        // holds is MAC'd before it leaves the process, and every row it loads is verified before the run trusts it.
        // Decision 6: this client is the lease holder, and the lease holder is a run's sole anchor writer, so a ring
        // that marks an environment sealed needs the tenant anchor store to write. Serving a sealed environment with
        // no anchor would leave its freshness to the control plane, which is what the anchor exists to take away.
        // Decision 10: the ring is the runner's allowlist, and it is default deny. A client built without one admits
        // no environment and serves nothing, which is the fail-closed reading of a runner nobody configured.
        RunnerKeyRing ring = keyRing ?? RunnerKeyRing.Empty;
        if (anchors is null)
        {
            foreach (string environment in ring.SealedEnvironments)
            {
                throw new InvalidOperationException($"Environment '{environment}' is on the key ring as sealed, and no tenant anchor store was given. A sealed environment's freshness is the anchor's, so serving it without one is the fail-open posture ADR 0065 decision 10 forbids.");
            }
        }

        IWorkflowCheckpointStore checkpointStore = new RunnerApiCheckpointStore(this);
        this.anchors = anchors;
        this.keyRing = ring;
        this.sealing = new SealingCheckpointStore(checkpointStore, ring, anchors);
        this.Checkpoints = this.sealing;
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
            MessageClaimRequest.HostedVersionsEntityArray.Build(
                in hostedVersions,
                static (in IReadOnlyCollection<string> versions, ref MessageClaimRequest.HostedVersionsEntityArray.Builder builder) =>
                {
                    foreach (string version in versions)
                    {
                        builder.AddItem(version);
                    }
                }),
            channel: channel,
            correlationId: correlation,
            leaseSeconds: leaseSeconds,
            limit: wanted);

        return this.ReadClaimsAsync(this.claims.ClaimAwaitingMessageAsync(request, cancellationToken));
    }

    /// <summary>
    /// Claims every run awaiting the message whose blind wait index is <paramref name="index"/> (ADR 0065 decision 4),
    /// and takes a lease on each. The index is computed by this runner's own <see cref="WaitBlinderFor"/> for the
    /// environment, so the control plane matches it by equality and learns neither the channel nor the business key.
    /// </summary>
    /// <param name="index">The blind wait index.</param>
    /// <param name="hostedVersions">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="limit">The most runs to claim; the server bounds it.</param>
    /// <param name="lease">The lease duration to request; the server bounds it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The claimed runs, empty when nothing awaits that index.</returns>
    public ValueTask<IReadOnlyList<RunnerClaim>> ClaimAwaitingIndexAsync(string index, IReadOnlyCollection<string> hostedVersions, int? limit = null, TimeSpan? lease = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(index);
        ArgumentNullException.ThrowIfNull(hostedVersions);

        MessageClaimRequest.LeaseSecondsEntity.Source leaseSeconds = lease is { } requested
            ? (MessageClaimRequest.LeaseSecondsEntity.Source)(long)requested.TotalSeconds
            : default;
        MessageClaimRequest.LimitEntity.Source wanted = limit is { } count
            ? (MessageClaimRequest.LimitEntity.Source)(long)count
            : default;

        MessageClaimRequest.Source<IReadOnlyCollection<string>> request = MessageClaimRequest.Build(
            in hostedVersions,
            MessageClaimRequest.HostedVersionsEntityArray.Build(
                in hostedVersions,
                static (in IReadOnlyCollection<string> versions, ref MessageClaimRequest.HostedVersionsEntityArray.Builder builder) =>
                {
                    foreach (string version in versions)
                    {
                        builder.AddItem(version);
                    }
                }),
            index: (MessageClaimRequest.IndexEntity.Source)index,
            leaseSeconds: leaseSeconds,
            limit: wanted);

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

    /// <summary>
    /// The tenant-attested store incarnation a run in <paramref name="environment"/> is held under (ADR 0065
    /// decision 6), which the run writes into its region beside the lease epoch; <see langword="null"/> for an
    /// environment this runner does not anchor.
    /// </summary>
    /// <param name="environment">The run's environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The attested incarnation, or <see langword="null"/> when the environment is not anchored here.</returns>
    /// <exception cref="Anchoring.CheckpointAnchorException">The environment is anchored and the tenant has attested no incarnation for it.</exception>
    public async ValueTask<ulong?> AttestedIncarnationAsync(string environment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (this.sealing is not { } sealing || this.anchors is not { } anchors || !sealing.IsAnchored(environment))
        {
            return null;
        }

        return await anchors.ReadAttestedIncarnationAsync(environment, cancellationToken).ConfigureAwait(false)
            ?? throw new CheckpointAnchorException(
                new WorkflowRunAddress(environment, default),
                null,
                $"Environment '{environment}' has no tenant-attested store incarnation, so no run in it can be claimed: the anchor's first attestation is made when the environment is created (ADR 0065 decision 6).");
    }

    /// <summary>
    /// The wait-index blinder for an environment on this runner's key ring (ADR 0065 decision 4), which a run in that
    /// environment parks its message waits under and a delivery names them by; <see langword="null"/> for an
    /// environment the runner serves clear.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The blinder, or <see langword="null"/>.</returns>
    public WaitIndexBlinder? WaitBlinderFor(string environment)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (!this.keyRing.TryGet(environment, out RunnerEnvironmentKeys keys))
        {
            return null;
        }

        return this.blinders.GetOrAdd(environment, static (env, k) => new WaitIndexBlinder(env, k.KeyId, k.PayloadKey), keys);
    }

    /// <summary>Gets the environments on this runner's key ring, whose waits are blinded.</summary>
    public IEnumerable<string> BlindedEnvironments => this.keyRing.Environments;

    /// <summary>Gets the runner's allowlist (ADR 0065 decision 10): the environments it serves, clear or sealed.</summary>
    public RunnerKeyRing Allowlist => this.keyRing;

    /// <summary>
    /// Whether this runner admits a claim for an environment (ADR 0065 decision 10): the environment is on its
    /// allowlist and, for a keyed entry with a pinned fingerprint, the seal key the control plane advertises for the
    /// generation held is the one the tenant pinned. The advertised key is fetched at most once a minute per environment; a fetch that fails, a
    /// generation the environment does not hold active, or any other fingerprint than the pinned one suspends the
    /// environment until a later check passes. A binding the control plane wrote for an environment the tenant did
    /// not name, and an environment the control plane re-keyed under a key the tenant did not register, both get this
    /// runner nothing.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Why the environment is admitted or not.</returns>
    public async ValueTask<RunnerAdmission> AdmitsAsync(string environment, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (!this.keyRing.Admits(environment))
        {
            return RunnerAdmission.NotAllowlisted;
        }

        if (!this.keyRing.TryGet(environment, out RunnerEnvironmentKeys keys) || keys.SealKeyFingerprint is null)
        {
            // A clear entry, or keys handed to the ring by a host that holds them itself with no pin to check: a
            // configured ring (RunnerKeyRing.BuildAsync) never builds a keyed entry without one.
            return RunnerAdmission.Admitted;
        }

        long now = this.timeProvider.GetTimestamp();
        if (this.sealKeyChecks.TryGetValue(environment, out SealKeyCheck cached) && this.timeProvider.GetElapsedTime(cached.CheckedAt, now) < SealKeyCheckInterval)
        {
            return cached.Admission;
        }

        RunnerAdmission admission = await this.CheckSealKeyAsync(environment, keys, cancellationToken).ConfigureAwait(false);
        this.sealKeyChecks[environment] = new SealKeyCheck(now, admission);
        return admission;
    }

    // The advertised seal keys, compared to the pin: the generation this runner holds has to be registered, active,
    // and under exactly the public key whose fingerprint the tenant pinned. The control plane's answer is a claim the
    // pin checks, never the other way round.
    private async ValueTask<RunnerAdmission> CheckSealKeyAsync(string environment, RunnerEnvironmentKeys keys, CancellationToken cancellationToken)
    {
        try
        {
            await using GetEnvironmentSealKeyResponse response = await this.environments.GetEnvironmentSealKeyAsync(environment, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode != 200)
            {
                return RunnerAdmission.SealKeyUnavailable;
            }

            foreach (EnvironmentSealKeyGeneration generation in response.OkBody.Generations.EnumerateArray())
            {
                if (!string.Equals((string)generation.KeyId, keys.KeyId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!generation.State.ValueEquals("Active"u8))
                {
                    return RunnerAdmission.GenerationNotActive;
                }

                return keys.Pins(((JsonElement)generation.SealPublicKey).GetBytesFromBase64()) ? RunnerAdmission.Admitted : RunnerAdmission.SealKeyMismatch;
            }

            return RunnerAdmission.GenerationNotActive;
        }
        catch (Exception ex) when (ex is HttpRequestException or RunnerApiException or FormatException)
        {
            return RunnerAdmission.SealKeyUnavailable;
        }
    }

    /// <summary>
    /// Gets the validator a sealed start's inputs are checked against at first claim (ADR 0065 decision 9), over the
    /// version documents this runner reads through the runner API.
    /// </summary>
    public RunStartInputValidator StartInputs => this.startInputs ??= new RunStartInputValidator(new RunnerApiArtifactSource(this.catalog));

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

    private readonly record struct SealKeyCheck(long CheckedAt, RunnerAdmission Admission);
}