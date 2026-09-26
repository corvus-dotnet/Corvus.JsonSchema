// <copyright file="RunnerRunAdvance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Corvus.Text.Json.Arazzo.Durability.Anchoring;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// Advancing one claimed run and giving its lease back. Dispatch and wait-resume differ in how a run is claimed and in
/// nothing after that, so what happens to a claimed run lives here rather than in each caller.
/// </summary>
internal static class RunnerRunAdvance
{
    /// <summary>Loads a claimed run, advances it, and releases its lease however that goes.</summary>
    /// <param name="client">The runner's client, which the run loads and saves through.</param>
    /// <param name="claim">The claim to advance.</param>
    /// <param name="resume">The resumer that resolves the run's executor and runs it.</param>
    /// <param name="message">The message to hand in before resuming, when <paramref name="hasMessage"/>.</param>
    /// <param name="headers">The delivered message's headers, so a resumed step can read <c>$message.header.*</c>.</param>
    /// <param name="hasMessage">Whether a message is being delivered, as distinct from a timer having fired.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="match">What the delivered message was claimed by: the channel and correlation id, and the blind indexes queried for the runner's blinded environments. A resumed run whose wait does not match is handed back without the message.</param>
    /// <returns><see langword="true"/> when the run was advanced.</returns>
    public static async ValueTask<bool> AdvanceAsync(
        ArazzoRunnerClient client,
        RunnerClaim claim,
        WorkflowResumer resume,
        JsonElement message,
        JsonElement headers,
        bool hasMessage,
        CancellationToken cancellationToken,
        MessageMatch match = default)
    {
        ulong? incarnation = null;
        try
        {
            // The allowlist (ADR 0065 decision 10): a claim for an environment this runner does not admit, or one
            // whose advertised seal key is not the one the tenant pinned, is handed back before a byte of it is
            // loaded. The control plane bound the principal; the runner decides what it serves.
            RunnerAdmission admission = await client.AdmitsAsync(claim.Environment, cancellationToken).ConfigureAwait(false);
            if (admission != RunnerAdmission.Admitted)
            {
                CountRefusal(claim, RefusalTagOf(admission));
                return false;
            }

            // The run loads and advances through the client's checkpoint store, so the executor is unaware it is not
            // talking to a database. The server re-read the run under the lease before offering it, so a null here
            // means the row went away underneath us rather than that the run was unsuitable. The run writes its grant
            // into its region (ADR 0065 decision 6): the lease epoch, and for an anchored environment the tenant's
            // attested incarnation beside it, which together are the ordering key the anchor stages every save under.
            incarnation = await client.AttestedIncarnationAsync(claim.Environment, cancellationToken).ConfigureAwait(false);
            using WorkflowRun? run = await WorkflowRun.ResumeAsync(client.Checkpoints, claim.Address, leaseEpoch: claim.LeaseEpoch, cancellationToken: cancellationToken, incarnation: incarnation, waitBlinder: client.WaitBlinderFor(claim.Environment)).ConfigureAwait(false);
            if (run is null)
            {
                return false;
            }

            // A sealed start's first claim (ADR 0065 decision 9): the control plane admitted inputs it could not read,
            // so the runner validates what it opened against the version's inputs schema before a step runs. Inputs
            // that do not validate fault the run at its start, sealed like any save, and the run is never claimed again.
            if (run.SealedStart && run.Sequence == 0 && !await client.StartInputs.ValidateAsync(run.WorkflowId, run.Inputs, cancellationToken).ConfigureAwait(false))
            {
                await run.FaultAsync(SealedStartFault.StepId, 1, SealedStartFault.InputsInvalid, cancellationToken).ConfigureAwait(false);
                CountRefusal(claim, SealedStartFault.InputsInvalid);
                return false;
            }

            if (hasMessage)
            {
                // The wait the run actually parked on, from its MAC-verified region, has to be the one the message was
                // claimed by (ADR 0065 decision 4): the index query is answered by the control plane, and a run it
                // offered under another wait is handed back rather than given a message meant for someone else.
                if (!match.Matches(run.Wait))
                {
                    return false;
                }

                run.DeliverMessage(message, headers);
            }

            await resume(run, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RunnerLeaseLostException)
        {
            // The run is someone else's now. Its checkpoint writes were refused rather than applied, so there is
            // nothing to undo, and the runner that holds the lease will carry it from the last durable checkpoint.
            return false;
        }
        catch (RunBudgetExhaustedException)
        {
            // The control plane refused the checkpoint and recorded the run as faulted on its budget (ADR 0068). The
            // run is over, not lost: nothing here is retried, and the release below hands back the lease.
            return false;
        }
        catch (SealedStartException unopenable)
        {
            // The sealed start did not open (ADR 0065 decision 9): no seal key, another generation, an initiator this
            // runner does not pin, or a seal that does not verify under the binding for this run. The row's envelope
            // is the control plane's and is readable, so the run is resumed from it alone and faulted at its start,
            // sealed like any save, which is what keeps it from being claimed again on every sweep. The refusal is
            // counted, since a stream of them is a run-injection attempt or a misconfigured initiator.
            System.Diagnostics.Activity.Current?.AddException(unopenable);
            using WorkflowRun run = WorkflowRun.Resume(client.Checkpoints, WorkflowCheckpointSerializer.Deserialize(unopenable.Row), unopenable.Etag, leaseEpoch: claim.LeaseEpoch, incarnation: incarnation, waitBlinder: client.WaitBlinderFor(claim.Environment));
            await run.FaultAsync(SealedStartFault.StepId, 1, SealedStartFault.Unopenable, cancellationToken).ConfigureAwait(false);
            CountRefusal(claim, SealedStartFault.Unopenable);
            return false;
        }
        catch (Exception fault) when (fault is CheckpointAnchorException or CryptographicException)
        {
            // The tenant anchor refused the run (a rollback, a substitution, a replay, a claim on a finished run) or
            // the row did not verify under this runner's keys (ADR 0065 decisions 4 and 6). The run cannot be
            // advanced and nothing here would make it so: it is left as it is, its lease goes back below, and the
            // sweep carries on with the other claims rather than ending on this one. It is the operator's to dispose
            // of envelope-only, or to recover once a signed re-anchor can be applied.
            System.Diagnostics.Activity.Current?.AddException(fault);
            CountRefusal(claim, fault is CheckpointAnchorException { Decision: { } decision } ? decision.Fault.ToString() : "integrity");
            return false;
        }
        finally
        {
            // Deliberately not the caller's token. Cancellation is how a runner shuts down, and that is precisely when
            // releasing matters most: a token passed through here would skip the release on every run in flight,
            // stranding each until its lease lapsed. Releasing a run this client no longer holds does nothing, so this
            // is safe on every path.
            await client.ReleaseAsync(claim.Address, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static string RefusalTagOf(RunnerAdmission admission) => admission switch
    {
        RunnerAdmission.NotAllowlisted => "allowlist",
        RunnerAdmission.SealKeyMismatch => "seal-key-mismatch",
        RunnerAdmission.GenerationNotActive => "generation-not-active",
        _ => "seal-key-unavailable",
    };

    private static void CountRefusal(in RunnerClaim claim, string refusal)
        => ArazzoTelemetry.WorkflowsRefused.Add(
            1,
            new KeyValuePair<string, object?>(ArazzoTelemetry.WorkflowIdTag, claim.WorkflowId),
            new KeyValuePair<string, object?>(ArazzoTelemetry.RefusalTag, refusal));
}

/// <summary>
/// What a delivered message was claimed by (ADR 0065 decision 4): the channel and correlation id for environments the
/// runner serves clear, and the blind indexes queried for the environments on its key ring. A resumed run is given the
/// message only when the wait it parked on is one of these.
/// </summary>
/// <param name="Channel">The channel, or <see langword="null"/> when the delivery named no channel.</param>
/// <param name="CorrelationId">The delivered correlation id, or <see langword="null"/>.</param>
/// <param name="Indexes">The blind indexes queried, or <see langword="null"/> when none were.</param>
public readonly record struct MessageMatch(string? Channel, string? CorrelationId, IReadOnlySet<string>? Indexes)
{
    /// <summary>Whether a run's wait is one the delivery was claimed by.</summary>
    /// <param name="wait">The wait the run parked on, from its own region.</param>
    /// <returns><see langword="true"/> when the message is for this run.</returns>
    public bool Matches(WorkflowWait? wait)
    {
        if (wait is not { Kind: WorkflowWaitKind.Message } w)
        {
            return false;
        }

        if (w.Index is { } index)
        {
            return this.Indexes is { } indexes && indexes.Contains(index);
        }

        // The clear rule: the channel is the channel, and a correlation absent on either side is a wildcard.
        return this.Channel is { } channel
            && string.Equals(w.Channel, channel, StringComparison.Ordinal)
            && (this.CorrelationId is null || w.CorrelationId is null || string.Equals(w.CorrelationId, this.CorrelationId, StringComparison.Ordinal));
    }
}