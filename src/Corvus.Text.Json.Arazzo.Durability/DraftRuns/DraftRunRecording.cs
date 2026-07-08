// <copyright file="DraftRunRecording.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A per-run recording of the metadata-only API exchanges a §18 debug run made, in global call order, plus the
/// per-step boundaries the runner marks at each durable checkpoint (§18 R3). Every source's
/// <see cref="RecordingApiTransport"/> for one run appends to the <em>same</em> recording, so a multi-source run's
/// exchanges keep their global call order (rather than being concatenated per source) and one boundary list
/// attributes them to steps — a step's exchanges being the range recorded since the previous boundary, faithful
/// across a step's retries. Recording holds no request or response body (the ratified §18 body posture).
/// </summary>
public sealed class DraftRunRecording
{
    private readonly List<RecordedApiExchange> exchanges = [];
    private readonly List<int> stepBoundaries = [];
    private readonly Lock gate = new();

    /// <summary>Gets a snapshot of the exchanges recorded so far, in global call order.</summary>
    public IReadOnlyList<RecordedApiExchange> Exchanges
    {
        get
        {
            lock (this.gate)
            {
                return this.exchanges.ToArray();
            }
        }
    }

    /// <summary>Gets the cumulative recorded-exchange count at each marked step boundary, in order: boundary
    /// <c>i</c> is the total exchanges recorded through the <c>i</c>-th checkpointed step, so that step's exchanges
    /// are the range <c>[boundary i-1, boundary i)</c> of <see cref="Exchanges"/> (boundary <c>-1</c> being 0).</summary>
    public IReadOnlyList<int> StepBoundaries
    {
        get
        {
            lock (this.gate)
            {
                return this.stepBoundaries.ToArray();
            }
        }
    }

    /// <summary>Records one metadata-only exchange (a <see cref="RecordingApiTransport"/> calls this once its inner
    /// transport returns a status).</summary>
    /// <param name="exchange">The recorded exchange.</param>
    public void Record(RecordedApiExchange exchange)
    {
        lock (this.gate)
        {
            this.exchanges.Add(exchange);
        }
    }

    /// <summary>Records the current recorded-exchange count as a step boundary — the runner wires this to the run's
    /// <see cref="WorkflowRun.OnCheckpointed"/> hook, so it fires at each per-step durable checkpoint. Every attempt
    /// of a retried step is recorded before that step checkpoints, so all its attempts fall inside its boundary range.</summary>
    public void MarkStepBoundary()
    {
        lock (this.gate)
        {
            this.stepBoundaries.Add(this.exchanges.Count);
        }
    }
}