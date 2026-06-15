// <copyright file="WorkflowCheckpointSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Turns a run's products and scalars into the single JSON checkpoint document, and back. The step-output
/// and inputs <see cref="JsonElement"/>s serialize natively (they already exist — the executor only ever
/// builds the genuine products), so a checkpoint is almost free; everything else is a handful of scalars.
/// </summary>
/// <remarks>
/// The document shape (see <c>docs/ArazzoWorkflowEnginePlan.md</c> §9.2):
/// <code>
/// { "runId", "workflowId", "status", "cursor",
///   "retryCounters": { "&lt;stepId&gt;": n },
///   "correlationTokens": { "&lt;name&gt;": "&lt;base64&gt;" },
///   "inputs": &lt;json&gt;,
///   "stepOutputs": { "&lt;stepId&gt;": &lt;json&gt; },
///   "outputs": &lt;json&gt; }   // present once the run has completed
/// </code>
/// </remarks>
public static class WorkflowCheckpointSerializer
{
    private const int DefaultBufferSize = 1024;

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Serializes a run's state to the checkpoint document.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="workflowId">The id of the workflow the run executes.</param>
    /// <param name="status">The run's lifecycle status.</param>
    /// <param name="cursor">The cursor (state-machine index of the next step to run).</param>
    /// <param name="createdAt">When the run was first created.</param>
    /// <param name="retryCounters">The per-step retry attempt counts.</param>
    /// <param name="correlationTokens">The correlation register (name → token bytes).</param>
    /// <param name="inputs">The workflow inputs (an undefined element writes <c>null</c>).</param>
    /// <param name="stepOutputs">The per-step <c>outputs</c> products.</param>
    /// <param name="outputs">The final workflow <c>outputs</c>, if the run has completed (an undefined element omits the field).</param>
    /// <param name="wait">The wait describing why the run is suspended, if it is (Tier 2).</param>
    /// <param name="fault">The fault record if the run is faulted (Tier 2).</param>
    /// <returns>The serialized checkpoint document (UTF-8 JSON).</returns>
    public static byte[] Serialize(
        WorkflowRunId runId,
        string workflowId,
        WorkflowRunStatus status,
        int cursor,
        DateTimeOffset createdAt,
        IReadOnlyDictionary<string, int> retryCounters,
        IReadOnlyDictionary<string, byte[]> correlationTokens,
        in JsonElement inputs,
        IReadOnlyDictionary<string, JsonElement> stepOutputs,
        in JsonElement outputs,
        WorkflowWait? wait = null,
        WorkflowFault? fault = null,
        string? correlationId = null,
        TagSet tags = default,
        SecurityTagSet securityTags = default)
    {
        ArgumentNullException.ThrowIfNull(workflowId);
        ArgumentNullException.ThrowIfNull(retryCounters);
        ArgumentNullException.ThrowIfNull(correlationTokens);
        ArgumentNullException.ThrowIfNull(stepOutputs);

        // Serialize through the pooled writer cache (the same primitive PersistedJson.ToArray uses) rather than a fresh
        // ArrayBufferWriter + Utf8JsonWriter — this is the run-state checkpoint write hotpath for every backend. Inlined
        // (not via PersistedJson.ToArray's callback) because the parameter set is too large for a context tuple. The only
        // retained allocation is the owned byte[] the stores' drivers demand.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartObject();
            writer.WriteString("runId"u8, runId.Value);
            writer.WriteString("workflowId"u8, workflowId);
            writer.WriteString("status"u8, StatusName(status));
            writer.WriteNumber("cursor"u8, cursor);
            writer.WriteString("createdAt"u8, createdAt);

            // Run-creation metadata (immutable): the telemetry correlation id and free-form tags.
            if (correlationId is { } cid)
            {
                writer.WriteString("correlationId"u8, cid);
            }

            if (!tags.IsEmpty)
            {
                writer.WritePropertyName("tags"u8);
                tags.WriteTo(writer);
            }

            if (!securityTags.IsEmpty)
            {
                writer.WritePropertyName("securityTags"u8);
                securityTags.WriteTo(writer);
            }

            writer.WriteStartObject("retryCounters"u8);
            foreach (KeyValuePair<string, int> counter in retryCounters)
            {
                writer.WriteNumber(counter.Key, counter.Value);
            }

            writer.WriteEndObject();

            writer.WriteStartObject("correlationTokens"u8);
            foreach (KeyValuePair<string, byte[]> token in correlationTokens)
            {
                writer.WriteBase64String(token.Key, token.Value);
            }

            writer.WriteEndObject();

            // Optional values are omitted when undefined (not written as null): "not present" is Undefined.
            if (inputs.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("inputs"u8);
                inputs.WriteTo(writer);
            }

            writer.WriteStartObject("stepOutputs"u8);
            foreach (KeyValuePair<string, JsonElement> step in stepOutputs)
            {
                if (step.Value.ValueKind == JsonValueKind.Undefined)
                {
                    continue;
                }

                writer.WritePropertyName(step.Key);
                step.Value.WriteTo(writer);
            }

            writer.WriteEndObject();

            if (outputs.ValueKind != JsonValueKind.Undefined)
            {
                writer.WritePropertyName("outputs"u8);
                outputs.WriteTo(writer);
            }

            if (wait is { } w)
            {
                writer.WriteStartObject("wait"u8);
                writer.WriteString("kind"u8, WaitKindName(w.Kind));
                if (w.Kind == WorkflowWaitKind.Timer)
                {
                    writer.WriteString("dueAt"u8, w.DueAt);
                }
                else
                {
                    writer.WriteString("channel"u8, w.Channel);
                    if (w.CorrelationId is { } waitCorrelationId)
                    {
                        writer.WriteString("correlationId"u8, waitCorrelationId);
                    }
                }

                writer.WriteEndObject();
            }

            if (fault is { } f)
            {
                writer.WriteStartObject("fault"u8);
                writer.WriteString("stepId"u8, f.StepId);
                writer.WriteNumber("attempt"u8, f.Attempt);
                writer.WriteString("error"u8, f.Error);
                writer.WriteString("at"u8, f.At);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Deserializes a checkpoint document into the run's resumable state.</summary>
    /// <param name="checkpointUtf8">The serialized checkpoint document (UTF-8 JSON).</param>
    /// <returns>
    /// The resumable state. The returned value owns the parsed document the <see cref="WorkflowCheckpointState.Inputs"/>
    /// and <see cref="WorkflowCheckpointState.StepOutputs"/> elements point into, so the caller must dispose it.
    /// </returns>
    public static WorkflowCheckpointState Deserialize(ReadOnlyMemory<byte> checkpointUtf8)
    {
        ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(checkpointUtf8);
        try
        {
            JsonElement root = document.RootElement;

            string runId = root.GetProperty("runId"u8).GetString() ?? string.Empty;
            string workflowId = root.GetProperty("workflowId"u8).GetString() ?? string.Empty;
            WorkflowRunStatus status = Enum.Parse<WorkflowRunStatus>(root.GetProperty("status"u8).GetString() ?? nameof(WorkflowRunStatus.Pending));
            int cursor = root.GetProperty("cursor"u8).GetInt32();
            DateTimeOffset createdAt = root.TryGetProperty("createdAt"u8, out JsonElement createdAtElement)
                ? createdAtElement.GetDateTimeOffset()
                : default;

            string? correlationId = root.TryGetProperty("correlationId"u8, out JsonElement correlationIdMeta) ? correlationIdMeta.GetString() : null;

            TagSet tags = default;
            if (root.TryGetProperty("tags"u8, out JsonElement tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                tags = TagSet.CopyFrom(tagsElement);
            }

            SecurityTagSet securityTags = default;
            if (root.TryGetProperty("securityTags"u8, out JsonElement securityTagsElement) && securityTagsElement.ValueKind == JsonValueKind.Array)
            {
                securityTags = SecurityTagSet.CopyFrom(securityTagsElement);
            }

            // Pre-size each working dictionary to its persisted element count so a long workflow's restore does not
            // re-allocate the backing array as the dictionary grows (GetPropertyCount is a no-alloc scan).
            Dictionary<string, int> retryCounters;
            if (root.TryGetProperty("retryCounters"u8, out JsonElement retryCountersElement))
            {
                retryCounters = new Dictionary<string, int>(retryCountersElement.GetPropertyCount());
                foreach (JsonProperty<JsonElement> counter in retryCountersElement.EnumerateObject())
                {
                    retryCounters[counter.Name] = counter.Value.GetInt32();
                }
            }
            else
            {
                retryCounters = [];
            }

            Dictionary<string, byte[]> correlationTokens;
            if (root.TryGetProperty("correlationTokens"u8, out JsonElement correlationTokensElement))
            {
                correlationTokens = new Dictionary<string, byte[]>(correlationTokensElement.GetPropertyCount());
                foreach (JsonProperty<JsonElement> token in correlationTokensElement.EnumerateObject())
                {
                    correlationTokens[token.Name] = token.Value.GetBytesFromBase64();
                }
            }
            else
            {
                correlationTokens = [];
            }

            JsonElement inputs = root.TryGetProperty("inputs"u8, out JsonElement inputsElement) ? inputsElement : default;

            Dictionary<string, JsonElement> stepOutputs;
            if (root.TryGetProperty("stepOutputs"u8, out JsonElement stepOutputsElement))
            {
                stepOutputs = new Dictionary<string, JsonElement>(stepOutputsElement.GetPropertyCount());
                foreach (JsonProperty<JsonElement> step in stepOutputsElement.EnumerateObject())
                {
                    stepOutputs[step.Name] = step.Value;
                }
            }
            else
            {
                stepOutputs = [];
            }

            JsonElement outputs = root.TryGetProperty("outputs"u8, out JsonElement outputsElement) ? outputsElement : default;

            WorkflowWait? wait = null;
            if (root.TryGetProperty("wait"u8, out JsonElement waitElement))
            {
                WorkflowWaitKind kind = Enum.Parse<WorkflowWaitKind>(waitElement.GetProperty("kind"u8).GetString() ?? nameof(WorkflowWaitKind.Timer));
                wait = kind == WorkflowWaitKind.Timer
                    ? WorkflowWait.Timer(waitElement.GetProperty("dueAt"u8).GetDateTimeOffset())
                    : WorkflowWait.Message(
                        waitElement.GetProperty("channel"u8).GetString() ?? string.Empty,
                        waitElement.TryGetProperty("correlationId"u8, out JsonElement correlationIdElement) ? correlationIdElement.GetString() : null);
            }

            WorkflowFault? fault = null;
            if (root.TryGetProperty("fault"u8, out JsonElement faultElement))
            {
                fault = new WorkflowFault(
                    faultElement.GetProperty("stepId"u8).GetString() ?? string.Empty,
                    faultElement.GetProperty("attempt"u8).GetInt32(),
                    faultElement.GetProperty("error"u8).GetString() ?? string.Empty,
                    faultElement.GetProperty("at"u8).GetDateTimeOffset());
            }

            return new WorkflowCheckpointState(document, runId, workflowId, status, cursor, createdAt, retryCounters, correlationTokens, inputs, stepOutputs, outputs, wait, fault, correlationId, tags, securityTags);
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Rewrites a checkpoint's <c>status</c> (and optionally drops its <c>wait</c>) by copying every other property's
    /// raw bytes verbatim — the working state (retry counters, correlation tokens, step outputs, inputs, outputs) is
    /// passed straight through with no dictionary materialized. For status-only transitions such as cancel, this
    /// replaces a full <see cref="Deserialize"/> + <see cref="Serialize"/> round-trip.
    /// </summary>
    /// <param name="source">The current checkpoint document (UTF-8 JSON).</param>
    /// <param name="newStatus">The status to write.</param>
    /// <param name="dropWait">Whether to omit the <c>wait</c> property (clearing a suspended run's wait).</param>
    /// <returns>The rewritten checkpoint document.</returns>
    public static byte[] RewriteStatus(ReadOnlySpan<byte> source, WorkflowRunStatus newStatus, bool dropWait)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            var reader = new Utf8JsonReader(source);
            reader.Read(); // the root StartObject
            writer.WriteStartObject();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("status"u8))
                {
                    reader.Read();
                    writer.WriteString("status"u8, StatusName(newStatus));
                }
                else if (dropWait && reader.ValueTextEquals("wait"u8))
                {
                    reader.Read();
                    reader.Skip();
                }
                else
                {
                    // Checkpoint property names are simple ASCII (never escaped), so the raw name span round-trips;
                    // the value (scalar or whole subtree) is copied verbatim, so no working dictionary is built.
                    ReadOnlySpan<byte> name = reader.ValueSpan;
                    reader.Read();
                    int valueStart = (int)reader.TokenStartIndex;
                    reader.Skip();
                    writer.WritePropertyName(name);
                    writer.WriteRawValue(source[valueStart..(int)reader.BytesConsumed], skipInputValidation: true);
                }
            }

            writer.WriteEndObject();
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Reads just the security tags from a parsed checkpoint (for the index projection), without materializing the working dictionaries.</summary>
    /// <param name="root">The parsed checkpoint root.</param>
    /// <returns>The security tags as a deferred holder over the persisted bytes (empty if absent).</returns>
    public static SecurityTagSet ReadSecurityTags(in JsonElement root)
    {
        if (!root.TryGetProperty("securityTags"u8, out JsonElement element) || element.ValueKind != JsonValueKind.Array)
        {
            return default;
        }

        return SecurityTagSet.CopyFrom(element);
    }

    // Map the enums to their names via constant strings, so serialising a checkpoint does not allocate a
    // string per call the way Enum.ToString() does. Names match the enum members so Enum.Parse round-trips.
    private static string StatusName(WorkflowRunStatus status) => status switch
    {
        WorkflowRunStatus.Pending => nameof(WorkflowRunStatus.Pending),
        WorkflowRunStatus.Running => nameof(WorkflowRunStatus.Running),
        WorkflowRunStatus.Suspended => nameof(WorkflowRunStatus.Suspended),
        WorkflowRunStatus.Completed => nameof(WorkflowRunStatus.Completed),
        WorkflowRunStatus.Cancelled => nameof(WorkflowRunStatus.Cancelled),
        WorkflowRunStatus.Faulted => nameof(WorkflowRunStatus.Faulted),
        _ => status.ToString(),
    };

    private static string WaitKindName(WorkflowWaitKind kind) => kind switch
    {
        WorkflowWaitKind.Timer => nameof(WorkflowWaitKind.Timer),
        WorkflowWaitKind.Message => nameof(WorkflowWaitKind.Message),
        _ => kind.ToString(),
    };
}