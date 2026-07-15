// <copyright file="MetadataTraceAssembler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Assembles the <c>SimulationTrace</c>-shaped metadata trace the designer's debug dock renders from a
/// completed, faulted, suspended, or paused <em>real</em> host-executed run (workflow-designer design §18
/// slice 3e-2a). It reads only the run's existing durable checkpoint — its terminal/suspended status, the
/// per-step <c>outputs</c> products, the final workflow <c>outputs</c>, the fault record, and the wait — and
/// pairs the executed steps with the metadata-only exchanges a <see cref="RecordingApiTransport"/> captured,
/// emitting the same trace shape <c>ScenarioSuite.WriteTrace</c> produces for a simulated run. No request or
/// response body is read or emitted anywhere (the ratified §18 body posture: bodies are a later
/// per-environment opt-in).
/// </summary>
/// <remarks>
/// <para>
/// The trace is emitted directly rather than by building the simulator's <c>SimulationResult</c> and calling
/// <c>ScenarioSuite.WriteTrace</c>, because <c>SimulationResult</c> and <c>WriteTrace</c> live in the
/// <c>Corvus.Text.Json.Arazzo.Testing</c> sibling assembly, which the durability layer deliberately does not
/// reference. The shape emitted here is byte-compatible with what the dock reads: <c>outcome</c>,
/// <c>pausedBefore</c>, <c>outputs</c>, <c>fault</c>, <c>wait</c>, and a <c>steps</c> array of
/// <c>{ stepId, status, attempt, outputs?, requests[] }</c> where each request is <c>{ method, path, status }</c>.
/// </para>
/// <para>
/// <b>What is derived, and what is gracefully omitted.</b> Step boundaries come from the checkpoint: each
/// per-step <c>outputs</c> entry is a completed step, taken in the checkpoint's document (insertion =
/// execution) order, plus the faulting step for a faulted run. The recorded exchanges are attributed to those
/// steps by the runner's per-step exchange boundaries (§18 R3) when supplied — a step's exchanges are those
/// recorded before its durable checkpoint, so a step's retries are grouped under it faithfully — or, without
/// boundaries, by the legacy forward-only one-call-per-step position; either way any trailing surplus attaches
/// to the last executed step so no exchange is dropped. The per-criterion
/// <c>successCriteria</c> verdicts and the routing <c>actionTaken</c> are omitted: reconstructing
/// them needs response bodies or at-source capture (a later increment), and the dock already renders a step
/// without them. A step that neither produced outputs nor faulted leaves no checkpoint evidence and is not
/// individually represented; faithful per-step boundaries across loops and branches likewise need at-source
/// capture. No criterion verdict is ever fabricated.
/// </para>
/// <para>
/// A <see cref="WorkflowWaitKind.Pause"/> suspend is rendered as <c>outcome=paused</c> plus the caller-supplied
/// paused-before step id (the step the run resumes at — known to the runner that set the breakpoint, not
/// derivable from the cursor-space checkpoint), <em>not</em> as a wait object. A timer or
/// message suspend is rendered as <c>outcome=suspended</c> with the wait object the dock renders.
/// </para>
/// </remarks>
public static class MetadataTraceAssembler
{
    /// <summary>Loads a run's checkpoint from the store and writes its metadata trace.</summary>
    /// <param name="writer">The writer to serialize the trace into.</param>
    /// <param name="store">The state store the run's checkpoint is loaded from.</param>
    /// <param name="id">The run id.</param>
    /// <param name="exchanges">The metadata-only exchanges recorded for the run, in call order (typically
    /// <see cref="RecordingApiTransport.Exchanges"/>).</param>
    /// <param name="pausedBeforeStepId">For a paused run, the id of the step the run resumes at (the breakpoint
    /// target the runner holds); ignored for any other outcome.</param>
    /// <param name="stepBoundaries">The runner's recorded per-step exchange boundaries (§18 R3,
    /// <see cref="RecordingApiTransport.StepBoundaries"/>) used to attribute exchanges to steps by range; when
    /// <see langword="null"/> or empty, exchanges are attributed by the legacy one-per-step position.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the trace has been written.</returns>
    /// <exception cref="InvalidOperationException">No run with <paramref name="id"/> exists in the store.</exception>
    public static async ValueTask WriteTraceAsync(
        Utf8JsonWriter writer,
        IWorkflowStateStore store,
        WorkflowRunId id,
        IReadOnlyList<RecordedApiExchange> exchanges,
        string? pausedBeforeStepId = null,
        IReadOnlyList<int>? stepBoundaries = null,
        IReadOnlyList<RecordedStepRecord>? capturedSteps = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        WorkflowCheckpoint checkpoint = await store.LoadAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No run '{id.Value}' exists to assemble a metadata trace for.");
        WriteTrace(writer, checkpoint.Utf8, exchanges, pausedBeforeStepId, stepBoundaries, capturedSteps);
    }

    /// <summary>Writes the metadata trace for a run from its serialized checkpoint and recorded exchanges.</summary>
    /// <param name="writer">The writer to serialize the trace into.</param>
    /// <param name="checkpointUtf8">The run's serialized checkpoint document (UTF-8 JSON), as produced by
    /// <see cref="WorkflowCheckpointSerializer"/> — read only, never modified.</param>
    /// <param name="exchanges">The metadata-only exchanges recorded for the run, in call order.</param>
    /// <param name="pausedBeforeStepId">For a paused run, the id of the step the run resumes at; ignored otherwise.</param>
    /// <param name="stepBoundaries">The runner's recorded per-step exchange boundaries (§18 R3,
    /// <see cref="RecordingApiTransport.StepBoundaries"/>) — the cumulative exchange count at each checkpointed
    /// step — used to attribute exchanges to steps by range (faithful across retries). When <see langword="null"/>
    /// or empty, exchanges are attributed by the legacy one-per-step position.</param>
    public static void WriteTrace(
        Utf8JsonWriter writer,
        ReadOnlyMemory<byte> checkpointUtf8,
        IReadOnlyList<RecordedApiExchange> exchanges,
        string? pausedBeforeStepId = null,
        IReadOnlyList<int>? stepBoundaries = null,
        IReadOnlyList<RecordedStepRecord>? capturedSteps = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(exchanges);

        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(checkpointUtf8);
        JsonElement root = document.RootElement;

        WorkflowRunStatus status = Enum.Parse<WorkflowRunStatus>(
            root.GetProperty("status"u8).GetString() ?? nameof(WorkflowRunStatus.Pending));

        // A §18 debugger pause is a Suspended run carrying a Pause wait; the dock represents it as
        // outcome=paused + pausedBefore, NOT as a wait-kind, so the Pause wait is never emitted as a wait object.
        string? waitKind = null;
        if (root.TryGetProperty("wait"u8, out JsonElement waitElement)
            && waitElement.TryGetProperty("kind"u8, out JsonElement waitKindElement))
        {
            waitKind = waitKindElement.GetString();
        }

        bool paused = status == WorkflowRunStatus.Suspended && waitKind == nameof(WorkflowWaitKind.Pause);

        writer.WriteStartObject();

        writer.WriteString("outcome"u8, status switch
        {
            WorkflowRunStatus.Completed => "completed"u8,
            WorkflowRunStatus.Faulted => "faulted"u8,
            WorkflowRunStatus.Suspended => paused ? "paused"u8 : "suspended"u8,
            _ => "suspended"u8,
        });

        if (paused && pausedBeforeStepId is not null)
        {
            writer.WriteString("pausedBefore"u8, pausedBeforeStepId);
        }

        if (root.TryGetProperty("outputs"u8, out JsonElement outputs) && outputs.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("outputs"u8);
            outputs.WriteTo(writer);
        }

        if (status == WorkflowRunStatus.Faulted && root.TryGetProperty("fault"u8, out JsonElement faultElement))
        {
            writer.WriteStartObject("fault"u8);
            writer.WriteString("stepId"u8, faultElement.GetProperty("stepId"u8).GetString());
            writer.WriteNumber("attempt"u8, faultElement.GetProperty("attempt"u8).GetInt32());
            writer.WriteString("error"u8, faultElement.GetProperty("error"u8).GetString());
            writer.WriteEndObject();
        }

        // A timer or message suspend surfaces the wait object the dock renders; a Pause suspend does not.
        if (status == WorkflowRunStatus.Suspended && !paused && waitKind is not null)
        {
            writer.WriteStartObject("wait"u8);
            writer.WriteString("kind"u8, waitKind == nameof(WorkflowWaitKind.Timer) ? "timer"u8 : "message"u8);
            if (waitKind == nameof(WorkflowWaitKind.Timer) && waitElement.TryGetProperty("dueAt"u8, out JsonElement dueAt))
            {
                writer.WriteString("dueAt"u8, dueAt.GetDateTimeOffset());
            }

            if (waitElement.TryGetProperty("channel"u8, out JsonElement channel) && channel.ValueKind == JsonValueKind.String)
            {
                writer.WriteString("channel"u8, channel.GetString());
            }

            if (waitElement.TryGetProperty("correlationId"u8, out JsonElement correlationId) && correlationId.ValueKind == JsonValueKind.String)
            {
                writer.WriteString("correlationId"u8, correlationId.GetString());
            }

            writer.WriteEndObject();
        }

        if (capturedSteps is { Count: > 0 })
        {
            WriteCapturedSteps(writer, root, status, exchanges, capturedSteps);
        }
        else
        {
            WriteSteps(writer, root, status, exchanges, stepBoundaries);
        }

        writer.WriteEndObject();
    }

    // §15-8a/§3.4: the at-source captured records are the step sequence when they exist — faithful per-step
    // boundaries across loops and retries, plus sub-workflow nesting. The checkpoint still contributes per step:
    // outputs enrich the LAST captured record per stepId; steps in the checkpoint the recording never saw (earlier
    // segments on another runner instance) are emitted FIRST, checkpoint-derived, without requests (their
    // exchanges predate this recording); and a step whose last captured record FAULTED but which the checkpoint
    // shows with outputs was durably Skip-resumed — a synthetic skipped record (attempt 0, the provided outputs,
    // no exchanges) follows the faulted one, the derivation design §10 F3 prescribes.
    private static void WriteCapturedSteps(
        Utf8JsonWriter writer,
        in JsonElement root,
        WorkflowRunStatus status,
        IReadOnlyList<RecordedApiExchange> exchanges,
        IReadOnlyList<RecordedStepRecord> captured)
    {
        _ = status;
        var outputsByStep = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var attemptsByStep = new Dictionary<string, int>(StringComparer.Ordinal);
        root.TryGetProperty("retryCounters"u8, out JsonElement retryCounters);
        var spineOrder = new List<string>();
        if (root.TryGetProperty("stepOutputs"u8, out JsonElement stepOutputs) && stepOutputs.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> step in stepOutputs.EnumerateObject())
            {
                string name = step.Name;
                outputsByStep[name] = step.Value;
                spineOrder.Add(name);
                if (retryCounters.ValueKind == JsonValueKind.Object && retryCounters.TryGetProperty(name, out JsonElement count))
                {
                    attemptsByStep[name] = count.GetInt32();
                }
            }
        }

        // The last captured record per stepId (it gets the checkpoint outputs), and the captured id set
        // (spine steps outside it were executed before this recording existed).
        var lastRecordByStep = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < captured.Count; i++)
        {
            lastRecordByStep[captured[i].StepId] = i;
        }

        int emitted = 0;
        writer.WriteStartArray("steps"u8);
        foreach (string stepId in spineOrder)
        {
            if (lastRecordByStep.ContainsKey(stepId))
            {
                continue;
            }

            writer.WriteStartObject();
            writer.WriteString("stepId"u8, stepId);
            writer.WriteString("status"u8, "completed"u8);
            writer.WriteNumber("attempt"u8, attemptsByStep.GetValueOrDefault(stepId));
            if (outputsByStep.TryGetValue(stepId, out JsonElement priorOutputs) && priorOutputs.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("outputs"u8);
                priorOutputs.WriteTo(writer);
            }

            writer.WriteEndObject();
            emitted++;
        }

        for (int i = 0; i < captured.Count; i++)
        {
            RecordedStepRecord record = captured[i];
            bool isLastForStep = lastRecordByStep[record.StepId] == i;
            bool skipResumed = record.Faulted && isLastForStep && outputsByStep.ContainsKey(record.StepId);
            JsonElement recordOutputs = !record.Faulted && isLastForStep && outputsByStep.TryGetValue(record.StepId, out JsonElement o) ? o : default;
            WriteCapturedRecord(writer, record, recordOutputs, exchanges);
            emitted++;

            if (skipResumed)
            {
                // The durable Skip protocol really did both: the attempt faulted (above), then the resume
                // seeded the provided outputs and moved past the step.
                writer.WriteStartObject();
                writer.WriteString("stepId"u8, record.StepId);
                writer.WriteString("status"u8, "completed"u8);
                writer.WriteNumber("attempt"u8, 0);
                writer.WriteBoolean("skipped"u8, true);
                if (outputsByStep[record.StepId].ValueKind != JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("outputs"u8);
                    outputsByStep[record.StepId].WriteTo(writer);
                }

                writer.WriteEndObject();
                emitted++;
            }
        }

        writer.WriteEndArray();
        writer.WriteNumber("stepsExecuted"u8, emitted);
    }

    private static void WriteCapturedRecord(Utf8JsonWriter writer, RecordedStepRecord record, in JsonElement outputs, IReadOnlyList<RecordedApiExchange> exchanges)
    {
        writer.WriteStartObject();
        writer.WriteString("stepId"u8, record.StepId);
        writer.WriteString("status"u8, record.Faulted ? "faulted"u8 : record.SubTrace?.Outcome == "suspended" ? "suspended"u8 : "completed"u8);
        writer.WriteNumber("attempt"u8, record.Attempt);
        if (outputs.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("outputs"u8);
            outputs.WriteTo(writer);
        }

        if (record.ExchangeCount > 0 && record.FirstExchange + record.ExchangeCount <= exchanges.Count)
        {
            writer.WriteStartArray("requests"u8);
            for (int e = record.FirstExchange; e < record.FirstExchange + record.ExchangeCount; e++)
            {
                RecordedApiExchange exchange = exchanges[e];
                writer.WriteStartObject();
                writer.WriteString("method"u8, exchange.Method.ToString().ToLowerInvariant());
                writer.WriteString("path"u8, exchange.Path);
                writer.WriteNumber("status"u8, exchange.StatusCode);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        if (record.SubTrace is { } sub)
        {
            // The nested trace, metadata-only (no outputs — the checkpoint holds none for child steps by
            // design; no bodies, no verdicts), in the kit's pinned {workflowId, ...trace} contract shape.
            writer.WriteStartObject("subTrace"u8);
            writer.WriteString("workflowId"u8, sub.WorkflowId);
            writer.WriteString("outcome"u8, sub.Outcome);
            writer.WriteStartArray("steps"u8);
            foreach (RecordedStepRecord nested in sub.Steps)
            {
                WriteCapturedRecord(writer, nested, default, exchanges);
            }

            writer.WriteEndArray();
            writer.WriteNumber("stepsExecuted"u8, sub.Steps.Count);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static void WriteSteps(Utf8JsonWriter writer, in JsonElement root, WorkflowRunStatus status, IReadOnlyList<RecordedApiExchange> exchanges, IReadOnlyList<int>? stepBoundaries)
    {
        var steps = new List<ExecutedStep>();
        root.TryGetProperty("retryCounters"u8, out JsonElement retryCounters);
        if (root.TryGetProperty("stepOutputs"u8, out JsonElement stepOutputs) && stepOutputs.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> step in stepOutputs.EnumerateObject())
            {
                using UnescapedUtf8JsonString name = step.Utf8NameSpan;
                int attempt = retryCounters.ValueKind == JsonValueKind.Object && retryCounters.TryGetProperty(name.Span, out JsonElement count)
                    ? count.GetInt32()
                    : 0;
                steps.Add(new ExecutedStep(Encoding.UTF8.GetString(name.Span), Faulted: false, attempt, step.Value));
            }
        }

        if (status == WorkflowRunStatus.Faulted && root.TryGetProperty("fault"u8, out JsonElement fault))
        {
            string faultStep = fault.GetProperty("stepId"u8).GetString() ?? string.Empty;
            int faultAttempt = fault.GetProperty("attempt"u8).GetInt32();
            if (steps.Count > 0 && steps[^1].StepId == faultStep)
            {
                // The faulting step also staged outputs (an earlier successful attempt); mark its record faulted.
                steps[^1] = steps[^1] with { Faulted = true };
            }
            else
            {
                // The usual case: the step faulted before extracting outputs, so it is not in stepOutputs.
                steps.Add(new ExecutedStep(faultStep, Faulted: true, faultAttempt, default));
            }
        }

        // §18 R3: attribute exchanges to steps by the runner's recorded per-step boundaries when they are present
        // and aligned (one boundary per checkpointed step; a step that faulted before it checkpointed has none) —
        // step i's exchanges are the range [boundary[i-1], boundary[i]), faithful across a step's retries. Without
        // usable boundaries, fall back to the legacy positional one-per-step with trailing surplus on the last step.
        bool useBoundaries = stepBoundaries is { Count: > 0 } && stepBoundaries.Count >= steps.Count - 1;

        writer.WriteStartArray("steps"u8);
        for (int i = 0; i < steps.Count; i++)
        {
            ExecutedStep step = steps[i];
            writer.WriteStartObject();
            writer.WriteString("stepId"u8, step.StepId);
            writer.WriteString("status"u8, step.Faulted ? "faulted"u8 : "completed"u8);
            writer.WriteNumber("attempt"u8, step.Attempt);

            if (step.Outputs.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("outputs"u8);
                step.Outputs.WriteTo(writer);
            }

            int start;
            int end;
            if (useBoundaries)
            {
                // The last step absorbs any tail past the last boundary (a step that faulted before checkpointing,
                // or surplus), so no exchange is dropped; an interior step ends at its own boundary.
                start = i == 0 ? 0 : stepBoundaries![i - 1];
                end = i == steps.Count - 1 ? exchanges.Count : stepBoundaries![i];
            }
            else
            {
                start = Math.Min(i, exchanges.Count);
                end = i == steps.Count - 1 ? exchanges.Count : Math.Min(i + 1, exchanges.Count);
            }

            if (end > start)
            {
                writer.WriteStartArray("requests"u8);
                for (int e = start; e < end; e++)
                {
                    RecordedApiExchange exchange = exchanges[e];
                    writer.WriteStartObject();
                    writer.WriteString("method"u8, exchange.Method.ToString().ToLowerInvariant());
                    writer.WriteString("path"u8, exchange.Path);
                    writer.WriteNumber("status"u8, exchange.StatusCode);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteNumber("stepsExecuted"u8, steps.Count);
    }

    private readonly record struct ExecutedStep(string StepId, bool Faulted, int Attempt, JsonElement Outputs);
}