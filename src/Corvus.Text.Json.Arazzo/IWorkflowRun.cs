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
}