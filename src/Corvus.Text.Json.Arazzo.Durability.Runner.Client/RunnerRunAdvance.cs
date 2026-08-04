// <copyright file="RunnerRunAdvance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
    /// <returns><see langword="true"/> when the run was advanced.</returns>
    public static async ValueTask<bool> AdvanceAsync(
        ArazzoRunnerClient client,
        RunnerClaim claim,
        WorkflowResumer resume,
        JsonElement message,
        JsonElement headers,
        bool hasMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            // The run loads and advances through the client's checkpoint store, so the executor is unaware it is not
            // talking to a database. The server re-read the run under the lease before offering it, so a null here
            // means the row went away underneath us rather than that the run was unsuitable.
            using WorkflowRun? run = await WorkflowRun.ResumeAsync(client.Checkpoints, claim.RunId, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (run is null)
            {
                return false;
            }

            if (hasMessage)
            {
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
        finally
        {
            // Deliberately not the caller's token. Cancellation is how a runner shuts down, and that is precisely when
            // releasing matters most: a token passed through here would skip the release on every run in flight,
            // stranding each until its lease lapsed. Releasing a run this client no longer holds does nothing, so this
            // is safe on every path.
            await client.ReleaseAsync(claim.RunId, CancellationToken.None).ConfigureAwait(false);
        }
    }
}