// <copyright file="IWorkflowRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The per-run durability seam a code-generated <em>durable</em> workflow executor touches. It carries the
/// run's resumable state — the cursor, each step's <c>outputs</c> product, retry attempt counts, and the
/// AsyncAPI correlation register — and persists a checkpoint after each step (Tier 1 crash recovery).
/// </summary>
/// <remarks>
/// <para>
/// The reification-free executor only ever constructs the genuine products, so a checkpoint is almost free:
/// the run serialises what already exists (see the durability execution model in the plan). On a fresh run
/// every accessor returns its empty default and <see cref="Cursor"/> is <c>0</c>, so the executor behaves
/// exactly as the non-durable form; on a resumed run the accessors return the persisted state and the
/// generated <c>while/switch</c> loop jumps straight to <see cref="Cursor"/>.
/// </para>
/// <para>
/// This interface lives in the runtime (referenced by generated executors); concrete implementations and
/// the pluggable state store live in <c>Corvus.Text.Json.Arazzo.Durability</c>. It is <em>not</em> on the
/// non-durable hot path — durability is an opt-in generation mode.
/// </para>
/// </remarks>
public interface IWorkflowRun
{
    /// <summary>Gets the cursor to resume at — the state-machine state index of the next step to run (<c>0</c> for a fresh run).</summary>
    int Cursor { get; }

    /// <summary>
    /// Gets the run-wide telemetry correlation id (the W3C trace id captured at creation), if any, so the
    /// generated executor can re-establish the run's trace context on resume — pinning resumed steps'
    /// outbound OpenAPI/AsyncAPI calls to the original trace.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the correlation register (correlation-id name → token bytes) restored from the checkpoint, owned
    /// by the run so the generated executor can read and mutate it in place. Empty for a fresh run.
    /// </summary>
    System.Collections.Generic.Dictionary<string, byte[]> CorrelationTokens { get; }

    /// <summary>Gets a step's restored <c>outputs</c> product, if the checkpoint holds one.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="outputs">The restored outputs element when present.</param>
    /// <returns><see langword="true"/> if the checkpoint holds outputs for the step.</returns>
    bool TryGetStepOutputs(string stepId, out JsonElement outputs);

    /// <summary>Gets a step's restored retry attempt count (<c>0</c> if none).</summary>
    /// <param name="stepId">The step id.</param>
    /// <returns>The persisted attempt count.</returns>
    int GetRetryCount(string stepId);

    /// <summary>Stages a step's <c>outputs</c> product for the next checkpoint.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="outputs">The built outputs element.</param>
    void SetStepOutputs(string stepId, in JsonElement outputs);

    /// <summary>Stages a step's retry attempt count for the next checkpoint.</summary>
    /// <param name="stepId">The step id.</param>
    /// <param name="count">The current attempt count.</param>
    void SetRetryCount(string stepId, int count);

    /// <summary>Persists the run's current state at <paramref name="cursor"/> (called after a step completes).</summary>
    /// <param name="cursor">The state index of the next step to run on resume.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the checkpoint is durable.</returns>
    ValueTask CheckpointAsync(int cursor, CancellationToken cancellationToken);

    /// <summary>Records the run as completed with its final workflow <c>outputs</c> (the terminal checkpoint).</summary>
    /// <param name="outputs">The workflow outputs element.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the terminal checkpoint is durable.</returns>
    ValueTask CompleteAsync(JsonElement outputs, CancellationToken cancellationToken);

    /// <summary>
    /// Suspends the run on a durable timer (Tier 2): persists <see cref="WorkflowRunStatus.Suspended"/> with a
    /// timer wait at <paramref name="cursor"/>, due <paramref name="delay"/> from now (the run owns the clock).
    /// </summary>
    /// <param name="cursor">The state index to resume at when the timer is due.</param>
    /// <param name="delay">How far in the future the run becomes due to resume.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted wait (its <see cref="WorkflowWait.DueAt"/> is the computed due instant).</returns>
    ValueTask<WorkflowWait> SuspendForTimerAsync(int cursor, TimeSpan delay, CancellationToken cancellationToken);

    /// <summary>
    /// Suspends the run on a correlated message (Tier 2): persists <see cref="WorkflowRunStatus.Suspended"/>
    /// with a message wait at <paramref name="cursor"/>, so a worker can resume when a matching message
    /// arrives and hand the payload back via <see cref="TryTakeDeliveredMessage(out JsonElement)"/>.
    /// </summary>
    /// <param name="cursor">The state index to resume at when a matching message is delivered.</param>
    /// <param name="channel">The channel the run is awaiting a message on.</param>
    /// <param name="correlationId">The correlation id the run is awaiting, if any.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted wait.</returns>
    ValueTask<WorkflowWait> SuspendForMessageAsync(int cursor, string channel, string? correlationId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the run as faulted (Tier 2 terminal-but-recoverable checkpoint). The run stamps the fault time
    /// (it owns the clock) and returns the constructed record for the executor's <c>Faulted</c> result.
    /// </summary>
    /// <param name="stepId">The id of the step that failed.</param>
    /// <param name="attempt">The attempt number on which it failed (1-based).</param>
    /// <param name="error">A description of the failure.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The persisted fault record.</returns>
    ValueTask<WorkflowFault> FaultAsync(string stepId, int attempt, string error, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the message a worker delivered for a resumed correlated-receive step, if any. The first call
    /// after a message-wait resume returns the payload (so the receive completes immediately without
    /// blocking); subsequent calls and fresh runs return <see langword="false"/>.
    /// </summary>
    /// <param name="payload">The delivered message payload when present.</param>
    /// <returns><see langword="true"/> if a delivered message was handed in.</returns>
    bool TryTakeDeliveredMessage(out JsonElement payload);

    /// <summary>
    /// Takes the message a worker delivered for a resumed correlated-receive step, along with its headers, if
    /// any. Like <see cref="TryTakeDeliveredMessage(out JsonElement)"/> but also hands back the delivered
    /// message headers so a resumed step can evaluate <c>$message.header.*</c> criteria/outputs. The first
    /// call after a message-wait resume returns the message; subsequent calls and fresh runs return
    /// <see langword="false"/>.
    /// </summary>
    /// <param name="payload">The delivered message payload when present.</param>
    /// <param name="headers">The delivered message headers when present (default when the transport carried none).</param>
    /// <returns><see langword="true"/> if a delivered message was handed in.</returns>
    bool TryTakeDeliveredMessage(out JsonElement payload, out JsonElement headers)
    {
        // Default for runs that do not carry headers through delivery: return the payload with no headers.
        headers = default;
        return this.TryTakeDeliveredMessage(out payload);
    }

    /// <summary>
    /// Begins a child scope for a sub-workflow invocation (a <c>workflowId</c>-bound step or a
    /// <c>goto</c>-workflow transfer), returning the run the durable executor threads into the sub-workflow's
    /// <c>ExecuteAsync</c>. The default returns <see langword="null"/> — the sub-workflow runs untracked, exactly
    /// as an unthreaded call would (no checkpoints, no nested recording), which is the correct behaviour for a
    /// production durable run whose child never checkpoints. A tracing or recording run overrides this to return a
    /// child recorder that shares the parent's step budget, virtual clock, stop conditions, and exchange cursor
    /// and collects the sub-workflow's step records as the parent step's nested trace.
    /// </summary>
    /// <param name="stepId">The parent step invoking the sub-workflow (the goto action's target for a transfer).</param>
    /// <param name="subWorkflowId">The invoked workflow's id.</param>
    /// <returns>The child scope to thread into the sub-workflow, or <see langword="null"/> to run it untracked.</returns>
    IWorkflowRun? BeginSubWorkflow(string stepId, string subWorkflowId) => null;

    /// <summary>
    /// The sub-workflow nesting depth cap every tracking run surface honours (§15-8a decision §8.2):
    /// a deliberate constant well past any legitimate composition depth, so runaway mutual recursion
    /// between workflows exhausts predictably instead of overflowing the stack. The demo mock aligns
    /// to this value.
    /// </summary>
    public const int MaxSubWorkflowDepth = 8;
}