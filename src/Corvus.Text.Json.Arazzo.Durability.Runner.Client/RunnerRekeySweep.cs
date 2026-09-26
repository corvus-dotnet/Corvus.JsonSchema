// <copyright file="RunnerRekeySweep.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// The re-key sweep (ADR 0065 decision 12): for every environment on the runner's key ring that holds more than one
/// accepted generation, claims the resting runs still sealed under an older generation, opens each under it, saves it
/// under the generation the runner now writes under, re-deriving a blinded message wait's index from the clear wait
/// the payload carries, and releases it. The lease is taken only when free and a held run is left for a later pass, so
/// the sweep never preempts a live holder; the save is the normal save, so the tenant anchor advances under the run's
/// lease exactly as for any other write. A generation with no resting rows left under it is one the operator can
/// retire and, once retired, drop from the ring.
/// </summary>
/// <param name="client">The runner's client.</param>
public sealed class RunnerRekeySweep(ArazzoRunnerClient client)
{
    private readonly ArazzoRunnerClient client = client ?? throw new ArgumentNullException(nameof(client));

    /// <summary>Gets or sets the lease duration to request per claim; the server bounds it.</summary>
    public TimeSpan? LeaseDuration { get; set; }

    /// <summary>Gets or sets the most runs to re-seal per claim; the server bounds it.</summary>
    public int? MaximumRunsPerPass { get; set; }

    /// <summary>
    /// Runs one pass: for each older generation held for each environment, claims a page of resting runs sealed under
    /// it and re-seals them under the write generation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of runs re-sealed.</returns>
    public async ValueTask<int> SweepAsync(CancellationToken cancellationToken)
    {
        int resealed = 0;
        foreach (string environment in this.client.BlindedEnvironments)
        {
            if (!this.client.Allowlist.TryGet(environment, out RunnerEnvironmentKeys keys) || keys.GenerationCount < 2)
            {
                continue;
            }

            // The environment has to be admitted, and admitted is what selects the write generation (decision 12): a
            // sweep under a generation the control plane does not hold active would seal rows the runner API refuses.
            if (await this.client.AdmitsAsync(environment, cancellationToken).ConfigureAwait(false) != RunnerAdmission.Admitted)
            {
                continue;
            }

            string write = this.client.Allowlist.WriteGenerationOf(environment)!;
            foreach (RunnerGenerationKeys generation in keys.AcceptedGenerations())
            {
                if (string.Equals(generation.KeyId, write, StringComparison.Ordinal))
                {
                    continue;
                }

                resealed += await this.ResealAsync(environment, generation.KeyId, cancellationToken).ConfigureAwait(false);
            }
        }

        return resealed;
    }

    private async ValueTask<int> ResealAsync(string environment, string generation, CancellationToken cancellationToken)
    {
        int resealed = 0;
        string? pageToken = this.client.RekeyPageTokenOf(environment, generation);
        RunnerRekeyClaims page = await this.client.ClaimForRekeyAsync(generation, pageToken, this.MaximumRunsPerPass, this.LeaseDuration, cancellationToken).ConfigureAwait(false);
        this.client.RememberRekeyPageToken(environment, generation, page.NextPageToken);
        foreach (RunnerClaim claim in page.Claims)
        {
            if (!string.Equals(claim.Environment, environment, StringComparison.Ordinal))
            {
                // Offered for another environment the principal is bound to: this pass is per environment, and the
                // claim is handed back rather than re-sealed under a generation selected for a different one.
                await this.client.ReleaseAsync(claim.Address, CancellationToken.None).ConfigureAwait(false);
                continue;
            }

            try
            {
                ulong? incarnation = await this.client.AttestedIncarnationAsync(claim.Environment, cancellationToken).ConfigureAwait(false);
                using WorkflowRun? run = await WorkflowRun.ResumeAsync(this.client.Checkpoints, claim.Address, leaseEpoch: claim.LeaseEpoch, cancellationToken: cancellationToken, incarnation: incarnation, waitBlinder: this.client.WaitBlinderFor(claim.Environment)).ConfigureAwait(false);
                if (run is null || run.Status is WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled)
                {
                    continue;
                }

                await run.ResealAsync(cancellationToken).ConfigureAwait(false);
                resealed++;
                ArazzoTelemetry.WorkflowsResealed.Add(1, new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, claim.WorkflowId));
            }
            catch (Exception fault) when (fault is RunnerLeaseLostException or CheckpointAnchorException or CryptographicException or SealedStartException or RunBudgetExhaustedException)
            {
                // The run is someone else's now, or is one this runner cannot vouch for; it is left as it is for the
                // operator or a later pass, as the advance path does.
                System.Diagnostics.Activity.Current?.AddException(fault);
            }
            finally
            {
                await this.client.ReleaseAsync(claim.Address, CancellationToken.None).ConfigureAwait(false);
            }
        }

        return resealed;
    }
}