// <copyright file="RunnerApiWorker.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Resumes waiting runs through the runner API, the counterpart to <see cref="WorkflowWorker"/> for a runner that holds
/// no store credential (ADR 0065). A suspended run resumes when its durable timer fires or when a message it is
/// awaiting arrives, and neither now requires the runner to query the store's wait index.
/// </summary>
/// <remarks>
/// <para>
/// As with dispatch, what is missing is the point. There is no environment parameter, because the candidate set is
/// intersected with the principal's bindings server-side. There is no due-time cutoff, because the server resumes
/// against its own clock rather than one a runner asserts. And a sweep now intersects with the runner's hosted
/// versions, which the store-backed worker never did: it would hand a runner a due run for a version it had not baked,
/// which the runner could only fault.
/// </para>
/// <para>
/// A delivered message's payload never leaves the runner. The API answers which runs a message can resume, and the
/// runner hands its own copy to each of them, so the control plane learns that a message arrived on a channel and not
/// what it said.
/// </para>
/// </remarks>
public sealed class RunnerApiWorker
{
    private readonly ArazzoRunnerClient client;

    /// <summary>Initializes a new instance of the <see cref="RunnerApiWorker"/> class.</summary>
    /// <param name="client">The runner's client for the runner API.</param>
    public RunnerApiWorker(ArazzoRunnerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    /// <summary>
    /// Gets or sets the lease duration to request per claim. Leave <see langword="null"/> for the deployment's
    /// default; the server bounds whatever is asked for.
    /// </summary>
    public TimeSpan? LeaseDuration { get; set; }

    /// <summary>
    /// Gets or sets the most runs to resume in one sweep. Every run a sweep claims is leased from that moment while
    /// the worker advances them one at a time, so the bound is what stops a large fan-out being held far longer than
    /// it is being executed. Leave <see langword="null"/> for the deployment's default.
    /// </summary>
    public int? MaximumRunsPerSweep { get; set; }

    /// <summary>
    /// Resumes every suspended run this runner can execute whose durable timer is now due.
    /// </summary>
    /// <param name="hostedWorkflowIds">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs resumed.</returns>
    public async ValueTask<int> ResumeDueTimersAsync(IReadOnlyCollection<string> hostedWorkflowIds, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        ArgumentNullException.ThrowIfNull(resume);

        IReadOnlyList<RunnerClaim> claims = await this.client.ClaimDueTimersAsync(hostedWorkflowIds, this.MaximumRunsPerSweep, this.LeaseDuration, cancellationToken).ConfigureAwait(false);
        return await this.AdvanceAllAsync(claims, resume, default, default, hasMessage: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Delivers a message to every suspended run this runner can execute that is awaiting it, and resumes them.
    /// </summary>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The delivered message's correlation token, or <see langword="null"/> to match every run awaiting the channel, whatever correlation each awaits.</param>
    /// <param name="payload">The message payload. It stays on this runner and is handed to each resumed run directly.</param>
    /// <param name="hostedWorkflowIds">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs resumed with the message.</returns>
    public ValueTask<int> DeliverMessageAsync(string channel, string? correlationId, JsonElement payload, IReadOnlyCollection<string> hostedWorkflowIds, WorkflowResumer resume, CancellationToken cancellationToken)
        => this.DeliverMessageAsync(channel, correlationId, payload, default, hostedWorkflowIds, resume, cancellationToken);

    /// <summary>
    /// Delivers a message and its headers to every suspended run this runner can execute that is awaiting it, so a
    /// resumed step can evaluate <c>$message.header.*</c> on the delivered message.
    /// </summary>
    /// <param name="channel">The channel the message arrived on.</param>
    /// <param name="correlationId">The delivered message's correlation token, or <see langword="null"/> to match every run awaiting the channel, whatever correlation each awaits.</param>
    /// <param name="payload">The message payload. It stays on this runner and is handed to each resumed run directly.</param>
    /// <param name="headers">The message headers, default when the transport carried none.</param>
    /// <param name="hostedWorkflowIds">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs resumed with the message.</returns>
    public async ValueTask<int> DeliverMessageAsync(string channel, string? correlationId, JsonElement payload, JsonElement headers, IReadOnlyCollection<string> hostedWorkflowIds, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        ArgumentNullException.ThrowIfNull(resume);

        IReadOnlyList<RunnerClaim> claims = await this.client.ClaimAwaitingMessageAsync(channel, correlationId, hostedWorkflowIds, this.MaximumRunsPerSweep, this.LeaseDuration, cancellationToken).ConfigureAwait(false);
        return await this.AdvanceAllAsync(claims, resume, payload, headers, hasMessage: true, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<int> AdvanceAllAsync(IReadOnlyList<RunnerClaim> claims, WorkflowResumer resume, JsonElement payload, JsonElement headers, bool hasMessage, CancellationToken cancellationToken)
    {
        int resumed = 0;
        foreach (RunnerClaim claim in claims)
        {
            if (await RunnerRunAdvance.AdvanceAsync(this.client, claim, resume, payload, headers, hasMessage, cancellationToken).ConfigureAwait(false))
            {
                resumed++;
            }
        }

        return resumed;
    }
}