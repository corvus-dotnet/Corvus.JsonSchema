// <copyright file="HostedWorkflowExecution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Runs an already-resolved <see cref="IHostedWorkflow"/> for a run: binds its transports, drives it to a tri-state
/// outcome, and disposes the transports. This is the execution step shared by every backend once the executor is in
/// hand, independent of how it was obtained. The in-process resumers resolve it through the loader; an AOT serverless
/// backend resolves it as a compile-time (baked) executor. Neither the binding nor the run loop depends on that
/// choice, so this core is AOT-safe and reused unchanged (ADR 0028).
/// </summary>
public static class HostedWorkflowExecution
{
    /// <summary>
    /// Binds the resolved workflow's transports and starts or resumes the run, returning the tri-state outcome. A
    /// debugger pause unwinds to a clean <see cref="WorkflowRunResultKind.Suspended"/> (the checkpoint already
    /// persisted the run as suspended). The transports are disposed after the run.
    /// </summary>
    /// <param name="hosted">The resolved workflow to run.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports the run executes through.</param>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    public static async ValueTask<WorkflowRunResultKind> RunAsync(IHostedWorkflow hosted, WorkflowTransportBinder transportBinder, WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hosted);
        ArgumentNullException.ThrowIfNull(transportBinder);
        ArgumentNullException.ThrowIfNull(run);

        WorkflowTransports transports = transportBinder(hosted.Descriptor, run.SecurityTags);

        // Unrented (no thread affinity): RunAsync is awaited and a run's async continuation (e.g. an outbound HTTP call
        // completing on a thread-pool thread) can dispose this workspace on a different thread than the one that created
        // it, so a thread-local rented workspace would fail its return-to-cache invariant. Same posture as the generated
        // OpenAPI response handlers.
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        try
        {
            return await hosted.RunAsync(transports.ApiTransports, transports.MessageTransports, workspace, run.Inputs, run, cancellationToken).ConfigureAwait(false);
        }
        catch (WorkflowPauseException)
        {
            // §18: the executor unwound at a debugger pause point. CheckpointAsync already persisted the run as
            // Suspended with a Pause wait (no wake trigger), so this is a clean suspend — report the same tri-state
            // Suspended a timer or message suspend returns; the finally still disposes the transports.
            return WorkflowRunResultKind.Suspended;
        }
        finally
        {
            foreach (IApiTransport apiTransport in transports.ApiTransports.Values)
            {
                await apiTransport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}