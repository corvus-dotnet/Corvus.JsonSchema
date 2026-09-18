// <copyright file="HostedWorkflowExecution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Runs an already-resolved <see cref="IHostedWorkflow"/> for a run: binds its transports, drives it to a tri-state
/// outcome, and disposes the transports. This is the execution step shared by every backend once the executor is in
/// hand, independent of how it was obtained. The in-process resumers resolve it through the loader; an AOT serverless
/// backend resolves it as a compile-time (baked) executor. Neither the binding nor the run loop depends on that
/// choice, so this core is AOT-safe and reused unchanged (ADR 0055).
/// </summary>
public static class HostedWorkflowExecution
{
    /// <summary>
    /// Resolves the run's workflow and runs it, so that a run whose executor cannot be had is ended here and not by
    /// each host that resolves one.
    /// </summary>
    /// <param name="resolver">Resolves the run to the workflow that runs it.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports the run executes through.</param>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    /// <remarks>
    /// <para>
    /// A resolver's refusal (<see cref="WorkflowExecutorFault.IsRefusal"/>) is the same answer on every attempt. Left
    /// to propagate it reaches the host with the run still live, the lease is released, and the run is claimed and
    /// refused again on every poll, outside the run's fuel (ADR 0068). So a refusal ends the run as a durable
    /// <see cref="WorkflowExecutorFault.Unresolvable"/> fault. The refusal's message names versions and hashes, and
    /// goes to the resolving activity and not the run record.
    /// </para>
    /// <para>
    /// Anything else resolution throws is taken to be the artifact source failing, which may pass, and propagates
    /// with the run left live, as the caller's cancellation and a failure to persist do.
    /// </para>
    /// </remarks>
    public static async ValueTask<WorkflowRunResultKind> ResolveAndRunAsync(IHostedWorkflowResolver resolver, WorkflowTransportBinder transportBinder, WorkflowRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        ArgumentNullException.ThrowIfNull(transportBinder);
        ArgumentNullException.ThrowIfNull(run);

        IHostedWorkflow hosted;
        using (Activity? activity = ArazzoTelemetry.ActivitySource.StartActivity("workflow.resolve"))
        {
            try
            {
                hosted = await resolver.ResolveAsync(run, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (WorkflowExecutorFault.IsRefusal(ex) && CanFault(run, cancellationToken))
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                await run.FaultAsync(string.Empty, attempt: 1, WorkflowExecutorFault.Unresolvable, cancellationToken).ConfigureAwait(false);
                return WorkflowRunResultKind.Faulted;
            }
        }

        return await RunAsync(hosted, transportBinder, run, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Binds the resolved workflow's transports and starts or resumes the run, returning the tri-state outcome. A
    /// debugger pause unwinds to a clean <see cref="WorkflowRunResultKind.Suspended"/> (the checkpoint already
    /// persisted the run as suspended), and a run that ran out of its execution budget unwinds to a clean
    /// <see cref="WorkflowRunResultKind.Faulted"/> (ADR 0068: the run already persisted its own budget fault). The
    /// transports are disposed after the run.
    /// </summary>
    /// <remarks>
    /// The run's transport bounds (ADR 0068) are applied here, to whatever the binder returned, and not left to the
    /// binder: there are many binders, several hand out transports from factories built before any run existed, and
    /// every execution path comes through this method. A run with no budget (the scheduler) takes the default budget's
    /// bounds, so no request on the run path is unbounded.
    /// </remarks>
    /// <param name="hosted">The resolved workflow to run.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports the run executes through.</param>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    public static ValueTask<WorkflowRunResultKind> RunAsync(IHostedWorkflow hosted, WorkflowTransportBinder transportBinder, WorkflowRun run, CancellationToken cancellationToken)
        => RunAsync(hosted, transportBinder, run, discloseUnhandledError: false, cancellationToken);

    /// <summary>
    /// Binds the resolved workflow's transports and starts or resumes the run, choosing what an unhandled executor
    /// failure records on the run.
    /// </summary>
    /// <param name="hosted">The resolved workflow to run.</param>
    /// <param name="transportBinder">Binds the workflow's descriptor to the transports the run executes through.</param>
    /// <param name="run">The run to start (fresh) or resume (restored checkpoint).</param>
    /// <param name="discloseUnhandledError"><see langword="true"/> to record an unhandled failure's own message as the
    /// run's fault, which suits a debug run whose reader is the workflow's author. <see langword="false"/> records
    /// <see cref="WorkflowExecutorFault.Unhandled"/>, which is what every production run does.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run outcome.</returns>
    /// <remarks>
    /// <para>
    /// An executor failure that nothing handled ends the run as a durable fault. Left to propagate, it would reach the
    /// host with the run still live: the lease is released, the run is claimed again, and the same step fails again.
    /// The attempt that threw was announced but never journaled, so that loop is outside the run's fuel (ADR 0068) and
    /// nothing would end it.
    /// </para>
    /// <para>
    /// Binding the transports is inside the same net. A source the workflow calls with no binding in the run's
    /// environment is refused identically on every attempt, so a <see cref="WorkflowTransportBindingException"/> ends
    /// the run as <see cref="WorkflowExecutorFault.TransportUnbound"/>, for an operator to supply the binding and
    /// resume it.
    /// </para>
    /// <para>
    /// Two things still propagate, because the host and not the run must answer them. The caller's cancellation is how
    /// a host shuts down, and the run resumes elsewhere. A failure to persist (a lost lease, a refused checkpoint, a
    /// store outage) cannot be answered by persisting a fault, and the run is retried from its last durable checkpoint.
    /// </para>
    /// </remarks>
    public static async ValueTask<WorkflowRunResultKind> RunAsync(IHostedWorkflow hosted, WorkflowTransportBinder transportBinder, WorkflowRun run, bool discloseUnhandledError, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hosted);
        ArgumentNullException.ThrowIfNull(transportBinder);
        ArgumentNullException.ThrowIfNull(run);

        // Binding is inside the net too. A source with no binding is refused identically on every attempt, so left to
        // propagate it is the same endless re-claim an executor failure would be.
        WorkflowTransports transports;
        try
        {
            transports = Bound(transportBinder(hosted.Descriptor, run.SecurityTags), run.Budget ?? ExecutionBudget.Default);
        }
        catch (WorkflowTransportBindingException ex) when (CanFault(run, cancellationToken))
        {
            await run.FaultAsync(string.Empty, attempt: 1, discloseUnhandledError ? ex.Message : WorkflowExecutorFault.TransportUnbound, cancellationToken).ConfigureAwait(false);
            return WorkflowRunResultKind.Faulted;
        }

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
        catch (WorkflowBudgetExhaustedException)
        {
            // ADR 0068: the run had no fuel or time for the attempt it was about to make, or nested past the depth
            // cap. It already persisted itself Faulted with the budget fault, so this is a clean terminal fault.
            return WorkflowRunResultKind.Faulted;
        }
        catch (Exception ex) when (CanFault(run, cancellationToken))
        {
            await run.FaultAsync(string.Empty, attempt: 1, discloseUnhandledError ? ex.Message : WorkflowExecutorFault.Unhandled, cancellationToken).ConfigureAwait(false);
            return WorkflowRunResultKind.Faulted;
        }
        finally
        {
            foreach (IApiTransport apiTransport in transports.ApiTransports.Values)
            {
                await apiTransport.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    // The seam answers a failure by faulting the run unless the host must answer it instead (the caller is cancelling,
    // or persistence itself failed, so a fault could not be saved). A run that already reached a terminal state has its
    // outcome on record, and a fault must not overwrite it.
    private static bool CanFault(WorkflowRun run, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested && !run.PersistenceFailed && run.Status is not (WorkflowRunStatus.Completed or WorkflowRunStatus.Cancelled or WorkflowRunStatus.Faulted);

    // Each bounded transport replaces the one the binder returned and owns what it owned, so the bounded set is the one
    // the run executes through and the one disposed after it.
    private static WorkflowTransports Bound(in WorkflowTransports transports, in ExecutionBudget budget)
    {
        if (transports.ApiTransports.Count == 0)
        {
            return transports;
        }

        var bounded = new Dictionary<string, IApiTransport>(transports.ApiTransports.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, IApiTransport> source in transports.ApiTransports)
        {
            bounded[source.Key] = ApiTransportBounds.Apply(source.Value, budget.StepTimeout, budget.MaxResponseBytes);
        }

        return new WorkflowTransports(bounded, transports.MessageTransports);
    }
}