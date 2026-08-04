// <copyright file="RunnerApiDispatcher.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Claims and runs work through the runner API, the counterpart to <see cref="WorkflowDispatcher"/> for a runner that
/// holds no store credential (ADR 0065). A runner polls it exactly as it polled the store-backed dispatcher; what
/// changes is that every step of the cycle is now a request the control plane authorises rather than a query the
/// runner makes for itself.
/// </summary>
/// <remarks>
/// <para>
/// It is markedly smaller than the store-backed dispatcher, and each thing missing is something the server now
/// enforces rather than something dropped:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <strong>No environment parameter.</strong> The store-backed dispatcher required one and refused to run without it,
/// because nothing else stopped a runner claiming another environment's work. Here the candidate set is intersected
/// with the principal's bindings server-side, so there is no environment for a runner to state, and none for it to
/// state wrongly.
/// </description></item>
/// <item><description>
/// <strong>No dispatch gate.</strong> The store-backed dispatcher took a callback so a runner could check its own
/// authorization before claiming — a check that was only ever as good as the runner's willingness to make it. A
/// revoked runner is now resolved to no bindings and offered nothing.
/// </description></item>
/// <item><description>
/// <strong>No claimability re-check.</strong> Querying an index and then leasing was two steps that could disagree;
/// claiming is one operation, and the server re-reads under the lease before offering the run.
/// </description></item>
/// </list>
/// <para>
/// The lease is granted for the deployment's default duration unless <see cref="LeaseDuration"/> asks otherwise, and
/// is not renewed automatically: an advance longer than the lease loses it, exactly as with the store-backed
/// dispatcher. A runner that expects long advances renews through <see cref="ArazzoRunnerClient.RenewAsync"/>.
/// </para>
/// </remarks>
public sealed class RunnerApiDispatcher
{
    private readonly ArazzoRunnerClient client;

    /// <summary>Initializes a new instance of the <see cref="RunnerApiDispatcher"/> class.</summary>
    /// <param name="client">The runner's client for the runner API.</param>
    public RunnerApiDispatcher(ArazzoRunnerClient client)
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
    /// Gets or sets the most runs to claim and advance in one pass. A poll that kept claiming until the queue emptied
    /// would starve the runner's other work and hold its concurrency budget against one burst. Defaults to 16.
    /// </summary>
    public int MaximumRunsPerPass { get; set; } = 16;

    /// <summary>
    /// Claims and runs whatever this runner can execute, up to <see cref="MaximumRunsPerPass"/>, returning how many
    /// ran. Nothing claimable returns zero, which is the common case for an idle runner and is not an error.
    /// </summary>
    /// <param name="hostedWorkflowIds">The versioned workflow ids this runner has baked and can execute.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs dispatched.</returns>
    public async ValueTask<int> DispatchClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, WorkflowResumer resume, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        ArgumentNullException.ThrowIfNull(resume);

        int dispatched = 0;
        int attempted = 0;
        WorkflowRunId first = default;
        HashSet<WorkflowRunId>? seen = null;
        while (attempted < this.MaximumRunsPerPass && !cancellationToken.IsCancellationRequested)
        {
            RunnerClaim? claimed = await this.client.TryClaimAsync(hostedWorkflowIds, this.LeaseDuration, cancellationToken).ConfigureAwait(false);
            if (claimed is not { } claim)
            {
                break;
            }

            // An advance normally moves the run out of the claimable set (Suspended at a wait, Completed, or Faulted),
            // so seeing a run twice in one pass means it came back without advancing. Re-taking it would spin on a run
            // that is not progressing, so the pass ends instead. Ending rather than skipping-and-continuing is
            // deliberate: the claimable query has no defined order, so a skip would keep drawing the same run and burn
            // the whole pass on claim-release churn. The store-backed dispatcher never faces this because it iterates
            // one snapshot of claimable ids; claiming one at a time is what makes the check necessary here.
            if (!TryRecord(claim.RunId, ref first, ref seen, attempted))
            {
                await this.client.ReleaseAsync(claim.RunId, CancellationToken.None).ConfigureAwait(false);
                break;
            }

            attempted++;
            if (await RunnerRunAdvance.AdvanceAsync(this.client, claim, resume, default, default, hasMessage: false, cancellationToken).ConfigureAwait(false))
            {
                dispatched++;
            }
        }

        return dispatched;
    }

    // The set is built only once a pass has actually taken a second run. An idle runner claims nothing and a busy one
    // usually takes one run per pass, and those paths are the ones that run constantly, so neither allocates; a pass
    // that genuinely dispatches several amortises one small set against several workflow advances.
    private static bool TryRecord(WorkflowRunId id, ref WorkflowRunId first, ref HashSet<WorkflowRunId>? seen, int attempted)
    {
        if (attempted == 0)
        {
            first = id;
            return true;
        }

        seen ??= [first];
        return seen.Add(id);
    }
}