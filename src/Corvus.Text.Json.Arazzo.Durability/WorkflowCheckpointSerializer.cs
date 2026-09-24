// <copyright file="WorkflowCheckpointSerializer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Turns a run's state into the checkpoint row and back (ADR 0065 decision 4). A row is three regions under the
/// pinned framing of <see cref="CheckpointRow"/>: the <em>runner region</em> (the envelope, run-management structure
/// the control plane reads on every save), the <em>payload</em> (tenant data), and the <em>control-plane region</em>
/// (the control plane's own decisions, <see cref="ControlPlaneRecord"/>). The runner writes the first two and carries
/// the third verbatim; the control plane writes only the third.
/// </summary>
/// <remarks>
/// <para>The envelope is a closed schema in fixed property order:</para>
/// <code>
/// { "runId", "environment", "workflowId", "status", "cursor", "sequence", "epoch"?, "createdAt", "updatedAt"?,
///   "correlationId"?, "rerunOf"?, "tags"?, "securityTags"?, "retryCounters": { "&lt;stepId&gt;": n },
///   "stepJournal"?: [ { "stepId", "status", "attempt", "startedAt", "endedAt" } ], "journalTruncated"?,
///   "wait"?, "fault"? }
/// </code>
/// <para>The payload is likewise closed:</para>
/// <code>
/// { "correlationTokens": { "&lt;name&gt;": "&lt;base64&gt;" }, "inputs"?: &lt;json&gt;, "outputs"?: &lt;json&gt;, "stepOutputs": { "&lt;stepId&gt;": &lt;json&gt; } }
/// </code>
/// <para>
/// An unknown member in either is a malformed row, never data: the envelope is what the control plane reads without a
/// key, so it cannot be allowed to become a channel. The step-output and inputs elements serialize natively (they
/// already exist; the executor only ever builds the genuine products), so a checkpoint is almost free.
/// </para>
/// </remarks>
public static class WorkflowCheckpointSerializer
{
    private const int DefaultBufferSize = 1024;
    private const string RunnerRegion = "runner";
    private const string PayloadRegion = "payload";

    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Serializes a run's state to a clear checkpoint row.</summary>
    /// <param name="envelope">The runner region's scalars.</param>
    /// <param name="retryCounters">The per-step retry attempt counts.</param>
    /// <param name="correlationTokens">The correlation register (name → token bytes).</param>
    /// <param name="inputs">The workflow inputs (an undefined element omits the member).</param>
    /// <param name="stepOutputs">The per-step <c>outputs</c> products.</param>
    /// <param name="outputs">The final workflow <c>outputs</c>, if the run has completed (an undefined element omits the member).</param>
    /// <param name="controlPlaneRegion">The control-plane region, carried verbatim from the row the run was loaded from (empty for a fresh run whose control plane decided nothing yet).</param>
    /// <returns>The row.</returns>
    public static byte[] Serialize(
        in CheckpointEnvelope envelope,
        PooledUtf8Map<int> retryCounters,
        IReadOnlyDictionary<string, byte[]> correlationTokens,
        in JsonElement inputs,
        PooledUtf8Map<JsonElement> stepOutputs,
        in JsonElement outputs,
        ReadOnlySpan<byte> controlPlaneRegion)
    {
        ArgumentNullException.ThrowIfNull(envelope.WorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(envelope.Environment);
        ArgumentNullException.ThrowIfNull(retryCounters);
        ArgumentNullException.ThrowIfNull(correlationTokens);
        ArgumentNullException.ThrowIfNull(stepOutputs);

        // Both regions render through the pooled writer cache (the same primitive PersistedJson.ToArray uses) rather
        // than a fresh ArrayBufferWriter + Utf8JsonWriter each: this is the run-state checkpoint write hot path for
        // every backend. The only retained allocation is the owned row the stores' drivers demand.
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter envelopeWriter = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter envelopeBuffer);
        try
        {
            WriteEnvelope(envelopeWriter, envelope, retryCounters);
            envelopeWriter.Flush();

            Utf8JsonWriter payloadWriter = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter payloadBuffer);
            try
            {
                WritePayload(payloadWriter, correlationTokens, inputs, stepOutputs, outputs);
                payloadWriter.Flush();
                return CheckpointRow.WriteClear(envelopeBuffer.WrittenSpan, payloadBuffer.WrittenSpan, controlPlaneRegion);
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(payloadWriter, payloadBuffer);
            }
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(envelopeWriter, envelopeBuffer);
        }
    }

    /// <summary>
    /// Deserializes a checkpoint row into the run's resumable state: the join of its three regions. An encrypted row
    /// (ADR 0065 decision 5) deserializes to its envelope alone, <see cref="WorkflowCheckpointState.PayloadSealed"/>:
    /// the reader holds no key, so the inputs, outputs, step outputs and correlation tokens are absent rather than
    /// invented, and a run that needs them is opened through the runner's <see cref="SealingCheckpointStore"/>.
    /// </summary>
    /// <param name="row">The stored row.</param>
    /// <returns>
    /// The resumable state. The returned value owns the parsed payload the <see cref="WorkflowCheckpointState.Inputs"/>
    /// and <see cref="WorkflowCheckpointState.StepOutputs"/> elements point into, so the caller must dispose it.
    /// </returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row, or a region does not match its closed schema.</exception>
    public static WorkflowCheckpointState Deserialize(ReadOnlyMemory<byte> row)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row.Span);
        ControlPlaneRecord controlPlane = ControlPlaneRecord.Parse(row[layout.ControlPlaneRegion]);

        // The envelope's values are all materialized (scalars, tag copies, journal entries), so its document is
        // disposed on the way out; only the payload document lives on, since the products are views into it.
        PooledUtf8Map<int>? retryCounters = null;
        PooledUtf8Map<JsonElement>? stepOutputs = null;
        ParsedJsonDocument<JsonElement>? payload = null;
        try
        {
            CheckpointEnvelope envelope;
            using (ParsedJsonDocument<JsonElement> envelopeDocument = ParsedJsonDocument<JsonElement>.Parse(row[layout.RunnerRegion]))
            {
                envelope = ReadEnvelope(envelopeDocument.RootElement, out retryCounters);
            }

            if (layout.Algorithm != CheckpointAlgorithm.Clear)
            {
                // The payload region is ciphertext: nothing here can read it, and nothing here tries.
                stepOutputs = PooledUtf8Map<JsonElement>.Rent(0);
                return new WorkflowCheckpointState(
                    payload: null,
                    row,
                    envelope,
                    retryCounters,
                    controlPlane,
                    row[layout.ControlPlaneRegion],
                    new Dictionary<string, byte[]>(0, StringComparer.Ordinal),
                    inputs: default,
                    stepOutputs,
                    outputs: default);
            }

            payload = ParsedJsonDocument<JsonElement>.Parse(row[layout.Payload]);
            JsonElement root = payload.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                ThrowHelper.ThrowCheckpointRowMalformed();
            }

            Dictionary<string, byte[]>? correlationTokens = null;
            JsonElement inputs = default;
            JsonElement outputs = default;
            foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
            {
                if (property.NameEquals("correlationTokens"u8))
                {
                    correlationTokens = new Dictionary<string, byte[]>(property.Value.GetPropertyCount());
                    foreach (JsonProperty<JsonElement> token in property.Value.EnumerateObject())
                    {
                        correlationTokens[token.Name] = token.Value.GetBytesFromBase64();
                    }
                }
                else if (property.NameEquals("inputs"u8))
                {
                    inputs = property.Value;
                }
                else if (property.NameEquals("outputs"u8))
                {
                    outputs = property.Value;
                }
                else if (property.NameEquals("stepOutputs"u8))
                {
                    // Pre-sized to the persisted element count so a long workflow's restore does not re-allocate the
                    // backing as it grows, with keys read as borrowed UTF-8 spans copied into the map's pooled arena.
                    stepOutputs = PooledUtf8Map<JsonElement>.Rent(property.Value.GetPropertyCount());
                    foreach (JsonProperty<JsonElement> step in property.Value.EnumerateObject())
                    {
                        using UnescapedUtf8JsonString name = step.Utf8NameSpan;
                        stepOutputs.Set(name.Span, step.Value);
                    }
                }
                else
                {
                    throw ThrowHelper.GetCheckpointRegionUnknownMemberException(PayloadRegion, property.Name);
                }
            }

            return new WorkflowCheckpointState(
                payload,
                row,
                envelope,
                retryCounters,
                controlPlane,
                row[layout.ControlPlaneRegion],
                correlationTokens ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(PayloadRegion, "correlationTokens"),
                inputs,
                stepOutputs ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(PayloadRegion, "stepOutputs"),
                outputs);
        }
        catch
        {
            retryCounters?.Dispose();
            stepOutputs?.Dispose();
            payload?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Validates a runner's submission (the submitted bytes, <see cref="CheckpointRow.TryParseSubmitted"/>) and reads
    /// what the checkpoint surfaces decide on before the server joins it with the control-plane region it holds: the
    /// environment the runner region claims (ADR 0065 decision 9), and the sequence and lease epoch it carries
    /// (decision 6). The runner region is read under its closed schema, so a malformed one is refused here.
    /// </summary>
    /// <param name="submitted">The submitted bytes.</param>
    /// <param name="submission">What the runner region claims, when the submission is well formed.</param>
    /// <returns><see langword="true"/> when <paramref name="submitted"/> is a well-formed submission.</returns>
    public static bool TryReadSubmission(ReadOnlyMemory<byte> submitted, out CheckpointSubmission submission)
    {
        submission = default;
        if (!CheckpointRow.TryParseSubmitted(submitted.Span, out CheckpointRowLayout layout))
        {
            return false;
        }

        try
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(submitted[layout.RunnerRegion]);
            CheckpointEnvelope envelope = ReadEnvelope(document.RootElement, out PooledUtf8Map<int> retryCounters);
            retryCounters.Dispose();
            ReadOnlySpan<byte> keyId = submitted.Span[layout.KeyId];
            submission = new CheckpointSubmission(
                envelope.Environment,
                envelope.Sequence,
                envelope.Epoch,
                layout.Algorithm,
                keyId.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(keyId),
                !submitted.Span[layout.Mac].IsEmpty);
            return true;
        }
        catch (Exception ex) when (ex is Corvus.Text.Json.JsonException or System.Text.Json.JsonException or FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads just the per-run write sequence from a stored row (ADR 0065 decision 6), without projecting the index or
    /// materialising the run's working state: a forward-only scan of the runner region that stops at the property.
    /// </summary>
    /// <param name="row">The stored row.</param>
    /// <param name="sequence">The sequence the runner region carries, or zero when the bytes are not a row carrying one.</param>
    /// <returns><see langword="true"/> when the row carries a sequence.</returns>
    public static bool TryReadSequence(ReadOnlyMemory<byte> row, out long sequence)
    {
        sequence = 0;
        if (!CheckpointRow.TryParse(row.Span, out CheckpointRowLayout layout))
        {
            return false;
        }

        try
        {
            var reader = new Utf8JsonReader(row.Span[layout.RunnerRegion]);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isSequence = reader.ValueTextEquals(SequenceUtf8);
                if (!reader.Read())
                {
                    break;
                }

                if (isSequence)
                {
                    return reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out sequence);
                }

                reader.Skip();
            }
        }
        catch (Exception ex) when (ex is Corvus.Text.Json.JsonException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            // Bytes that are not a checkpoint carry no sequence, which is what this reports: the caller is asking a
            // question it is entitled to get "no" for.
        }

        sequence = 0;
        return false;
    }

    private static ReadOnlySpan<byte> SequenceUtf8 => "sequence"u8;

    /// <summary>
    /// Reads the facts the execution-budget predicate needs (ADR 0068) from a row: the run's frozen budget from the
    /// control-plane region, the journal's length and truncation from the runner region, and whether the run is
    /// faulted on its budget, by the control plane's decision or the runner's own. A forward-only scan of the runner
    /// region like <see cref="TryReadSequence"/>, for a caller that holds a stored row and wants these facts alone.
    /// </summary>
    /// <param name="row">The stored row.</param>
    /// <param name="facts">The facts read; <see langword="default"/> when the bytes are not a row.</param>
    /// <returns><see langword="true"/> when the facts could be read.</returns>
    public static bool TryReadBudgetFacts(ReadOnlyMemory<byte> row, out CheckpointBudgetFacts facts)
    {
        facts = default;
        if (!CheckpointRow.TryParse(row.Span, out CheckpointRowLayout layout))
        {
            return false;
        }

        long sequence = 0;
        int journalCount = 0;
        bool truncated = false;
        bool runnerBudgetFaulted = false;
        try
        {
            ControlPlaneRecord controlPlane = ControlPlaneRecord.Parse(row[layout.ControlPlaneRegion]);

            var reader = new Utf8JsonReader(row.Span[layout.RunnerRegion]);
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                int which = reader.ValueTextEquals(SequenceUtf8) ? 0
                    : reader.ValueTextEquals("stepJournal"u8) ? 1
                    : reader.ValueTextEquals("journalTruncated"u8) ? 2
                    : reader.ValueTextEquals("fault"u8) ? 3
                    : -1;
                if (!reader.Read())
                {
                    break;
                }

                switch (which)
                {
                    case 0:
                        if (reader.TokenType == JsonTokenType.Number)
                        {
                            reader.TryGetInt64(out sequence);
                        }

                        break;
                    case 1:
                        journalCount = reader.TokenType == JsonTokenType.StartArray ? CountArray(ref reader) : 0;
                        break;
                    case 2:
                        truncated = reader.TokenType == JsonTokenType.True;
                        break;
                    case 3:
                        runnerBudgetFaulted = reader.TokenType == JsonTokenType.StartObject && FaultIsBudgetFault(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            facts = new CheckpointBudgetFacts(controlPlane.Budget, journalCount, truncated, runnerBudgetFaulted || controlPlane.IsBudgetFaultedAt(sequence));
            return true;
        }
        catch (Exception ex) when (ex is Corvus.Text.Json.JsonException or System.Text.Json.JsonException or FormatException or InvalidOperationException)
        {
            facts = default;
            return false;
        }

        // Counts an array's elements, skipping each one whole. Leaves the reader on the array's end token.
        static int CountArray(ref Utf8JsonReader reader)
        {
            int count = 0;
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                reader.Skip();
                count++;
            }

            return count;
        }

        // Reads a fault object's error type and compares it against the budget faults without materializing it.
        // Leaves the reader on the object's end token.
        static bool FaultIsBudgetFault(ref Utf8JsonReader reader)
        {
            bool result = false;
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                bool isError = reader.ValueTextEquals("error"u8);
                if (!reader.Read())
                {
                    break;
                }

                if (isError && reader.TokenType == JsonTokenType.String)
                {
                    result = !reader.ValueIsEscaped && ExecutionBudgetFault.IsBudgetFault(reader.ValueSpan);
                }
                else
                {
                    reader.Skip();
                }
            }

            return result;
        }
    }

    /// <summary>
    /// Projects everything the checkpoint surfaces read from a posted row, in one parse of its envelope and its
    /// control-plane region: the effective <see cref="WorkflowRunIndexEntry"/>, the environment the runner region
    /// claims (ADR 0065 decision 9), the write sequence and lease epoch it carries (decision 6), the execution-budget
    /// facts (ADR 0068), and the control-plane region's bytes (decision 7). The payload is not parsed.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The projection.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row, or the envelope or control-plane region does not match its closed schema.</exception>
    public static CheckpointProjection Project(ReadOnlyMemory<byte> row)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row.Span);
        ReadOnlyMemory<byte> controlPlaneRegion = row[layout.ControlPlaneRegion];
        ControlPlaneRecord controlPlane = ControlPlaneRecord.Parse(controlPlaneRegion);

        CheckpointEnvelope envelope;
        int journalCount;
        using (ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(row[layout.RunnerRegion]))
        {
            envelope = ReadEnvelope(document.RootElement, out PooledUtf8Map<int> retryCounters);
            retryCounters.Dispose();
            journalCount = envelope.StepJournal.Count;
        }

        (WorkflowRunStatus status, WorkflowWait? wait, WorkflowFault? fault) = Join(envelope, controlPlane);
        bool budgetFaulted = fault is { } effective && ExecutionBudgetFault.IsBudgetFault(effective.Error);

        // The index carries a non-nullable UpdatedAt: the latest write to the row, which is the runner's stamp or a
        // later control-plane decision, so the index and the row agree whichever party wrote last.
        DateTimeOffset updatedAt = envelope.UpdatedAt ?? envelope.CreatedAt;
        foreach (DateTimeOffset? decided in (ReadOnlySpan<DateTimeOffset?>)[controlPlane.Cancellation?.At, controlPlane.BudgetFault?.At, controlPlane.ResumeRequest?.At])
        {
            if (decided is { } at && at > updatedAt)
            {
                updatedAt = at;
            }
        }

        WorkflowRunIndexEntry index = WorkflowRunIndexEntry.Project(
            envelope.WorkflowId,
            status,
            envelope.CreatedAt,
            updatedAt,
            wait,
            fault,
            envelope.CorrelationId,
            envelope.Tags,
            envelope.SecurityTags,
            controlPlane.ResumeRequestedAt(envelope.Sequence));

        return new CheckpointProjection(
            index,
            envelope.Environment,
            envelope.Sequence,
            envelope.Epoch,
            new CheckpointBudgetFacts(controlPlane.Budget, journalCount, envelope.JournalTruncated, budgetFaulted),
            controlPlaneRegion);
    }

    /// <summary>
    /// Attempts <see cref="Project"/>, returning <see langword="false"/> instead of throwing when the bytes are not a
    /// well-formed row. The checkpoint surfaces use this as the validation boundary for a posted body, so a malformed
    /// body is a clean rejection rather than an unhandled fault.
    /// </summary>
    /// <param name="row">The bytes to project.</param>
    /// <param name="projection">The projection, or <see langword="default"/> when the bytes are not a row.</param>
    /// <returns><see langword="true"/> if the bytes projected; otherwise <see langword="false"/>.</returns>
    public static bool TryProject(ReadOnlyMemory<byte> row, out CheckpointProjection projection)
    {
        try
        {
            projection = Project(row);
            return true;
        }
        catch (Exception ex) when (ex is Corvus.Text.Json.JsonException or System.Text.Json.JsonException or FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException)
        {
            // Malformed framing or JSON, a missing required member, an unknown member, or a bad scalar: the bytes are
            // not a checkpoint. Every other exception (e.g. cancellation, out-of-memory) still propagates.
            projection = default;
            return false;
        }
    }

    /// <summary>Projects a row's effective <see cref="WorkflowRunIndexEntry"/> directly from its bytes: the index part of <see cref="Project"/>.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The index entry the row projects to.</returns>
    public static WorkflowRunIndexEntry ProjectIndex(ReadOnlyMemory<byte> row)
        => Project(row).Index;

    /// <summary>As <see cref="ProjectIndex(ReadOnlyMemory{byte})"/>, additionally reporting the environment the runner region claims (ADR 0065 decision 9).</summary>
    /// <param name="row">The row.</param>
    /// <param name="environment">The environment the runner region claims.</param>
    /// <returns>The index entry the row projects to.</returns>
    public static WorkflowRunIndexEntry ProjectIndex(ReadOnlyMemory<byte> row, out string environment)
    {
        CheckpointProjection projection = Project(row);
        environment = projection.Environment;
        return projection.Index;
    }

    /// <summary>Attempts <see cref="ProjectIndex(ReadOnlyMemory{byte})"/>, returning <see langword="false"/> instead of throwing when the bytes are not a well-formed row.</summary>
    /// <param name="row">The bytes to project.</param>
    /// <param name="index">The projected index entry, or <see langword="default"/> when the bytes are not a row.</param>
    /// <returns><see langword="true"/> if the bytes projected to an index entry; otherwise <see langword="false"/>.</returns>
    public static bool TryProjectIndex(ReadOnlyMemory<byte> row, out WorkflowRunIndexEntry index)
    {
        bool projected = TryProject(row, out CheckpointProjection projection);
        index = projection.Index;
        return projected;
    }

    /// <summary>
    /// Rewrites a row for a control-plane remediation (a resume that moves the cursor, supplies a skipped step's
    /// outputs, patches the run's context, or re-budgets the run), copying every member the remediation does not name
    /// verbatim: the cursor and update time in the envelope, the context in the payload, the budget in the
    /// control-plane region. A remediation must change what it names and nothing else, so nothing here re-serializes
    /// the run from a list of its fields.
    /// </summary>
    /// <param name="row">The stored row.</param>
    /// <param name="remediation">What the remediation changes.</param>
    /// <returns>The rewritten row.</returns>
    /// <remarks>
    /// This rewrites the runner's own regions, which a clear row permits. Once the runner region carries a MAC, a
    /// payload-mutating resume is recorded as a control-plane request the runner applies inside its own boundary
    /// (ADR 0065 decision 8), and this becomes a control-plane-region write like every other.
    /// </remarks>
    internal static byte[] RewriteForRemediation(ReadOnlyMemory<byte> row, in CheckpointRemediation remediation)
    {
        CheckpointRowLayout layout = CheckpointRow.Parse(row.Span);
        ControlPlaneRecord controlPlane = ControlPlaneRecord.Parse(row[layout.ControlPlaneRegion]);
        byte[] controlPlaneRegion = remediation.Budget is { } budget
            ? (controlPlane with { Budget = budget }).ToUtf8()
            : row[layout.ControlPlaneRegion].ToArray();

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter envelopeWriter = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter envelopeBuffer);
        try
        {
            RewriteEnvelope(envelopeWriter, row.Span[layout.RunnerRegion], remediation);
            envelopeWriter.Flush();

            Utf8JsonWriter payloadWriter = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter payloadBuffer);
            try
            {
                RewritePayload(payloadWriter, row.Span[layout.Payload], remediation);
                payloadWriter.Flush();
                return CheckpointRow.WriteClear(envelopeBuffer.WrittenSpan, payloadBuffer.WrittenSpan, controlPlaneRegion);
            }
            finally
            {
                workspace.ReturnWriterAndBuffer(payloadWriter, payloadBuffer);
            }
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(envelopeWriter, envelopeBuffer);
        }

        static void RewriteEnvelope(Utf8JsonWriter writer, ReadOnlySpan<byte> source, in CheckpointRemediation remediation)
        {
            bool wroteUpdatedAt = false;
            var reader = new Utf8JsonReader(source);
            reader.Read(); // the root StartObject
            writer.WriteStartObject();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (reader.ValueTextEquals("cursor"u8))
                {
                    reader.Read();
                    writer.WriteNumber("cursor"u8, remediation.Cursor);
                }
                else if (reader.ValueTextEquals("updatedAt"u8))
                {
                    reader.Read();
                    writer.WriteString("updatedAt"u8, remediation.UpdatedAt);
                    wroteUpdatedAt = true;
                }
                else
                {
                    CopyMember(writer, ref reader, source);
                }
            }

            if (!wroteUpdatedAt)
            {
                writer.WriteString("updatedAt"u8, remediation.UpdatedAt);
            }

            writer.WriteEndObject();
        }

        static void RewritePayload(Utf8JsonWriter writer, ReadOnlySpan<byte> source, in CheckpointRemediation remediation)
        {
            bool wroteInputs = false;
            bool wroteStepOutputs = false;
            var reader = new Utf8JsonReader(source);
            reader.Read(); // the root StartObject
            writer.WriteStartObject();
            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                if (remediation.ReplacesContext && reader.ValueTextEquals("inputs"u8))
                {
                    reader.Read();
                    reader.Skip();
                    WriteInputs(writer, remediation.Inputs);
                    wroteInputs = true;
                }
                else if (remediation.ReplacesContext && reader.ValueTextEquals("stepOutputs"u8))
                {
                    reader.Read();
                    reader.Skip();
                    WriteStepOutputs(writer, remediation.StepOutputs!);
                    wroteStepOutputs = true;
                }
                else
                {
                    CopyMember(writer, ref reader, source);
                }
            }

            // A member the stored payload did not carry, and the remediation supplies, is appended.
            if (remediation.ReplacesContext)
            {
                if (!wroteInputs)
                {
                    WriteInputs(writer, remediation.Inputs);
                }

                if (!wroteStepOutputs)
                {
                    WriteStepOutputs(writer, remediation.StepOutputs!);
                }
            }

            writer.WriteEndObject();
        }

        // Member names are simple ASCII (never escaped), so the raw name span round-trips; the value (scalar or whole
        // subtree) is copied verbatim, so no working dictionary is built.
        static void CopyMember(Utf8JsonWriter writer, ref Utf8JsonReader reader, ReadOnlySpan<byte> source)
        {
            ReadOnlySpan<byte> name = reader.ValueSpan;
            reader.Read();
            int valueStart = (int)reader.TokenStartIndex;
            reader.Skip();
            writer.WritePropertyName(name);
            writer.WriteRawValue(source[valueStart..(int)reader.BytesConsumed], skipInputValidation: true);
        }
    }

    /// <summary>The join of a runner region and a control-plane record (ADR 0065 decision 7): the run's effective status, wait and fault.</summary>
    /// <param name="envelope">The runner region.</param>
    /// <param name="controlPlane">The control-plane record.</param>
    /// <returns>The effective lifecycle values.</returns>
    internal static (WorkflowRunStatus Status, WorkflowWait? Wait, WorkflowFault? Fault) Join(in CheckpointEnvelope envelope, in ControlPlaneRecord controlPlane)
    {
        if (controlPlane.Cancellation is not null)
        {
            // Cancelled is terminal and unconditional: no wait survives it, and the runner's fault, if any, is kept for
            // the record.
            return (WorkflowRunStatus.Cancelled, null, envelope.Fault);
        }

        if (controlPlane.BudgetFault is { } budgetFault && controlPlane.IsBudgetFaultedAt(envelope.Sequence))
        {
            // The fault is recorded against the last step journaled, which is where the run was when its budget ran
            // out; a run with no journal names no step.
            (string stepId, int attempt) = envelope.StepJournal.Count > 0
                ? (envelope.StepJournal[^1].StepId, envelope.StepJournal[^1].Attempt)
                : (string.Empty, 0);
            return (WorkflowRunStatus.Faulted, null, new WorkflowFault(stepId, attempt, budgetFault.Error, budgetFault.At));
        }

        return (envelope.Status, envelope.Wait, envelope.Fault);
    }

    // The envelope in its fixed property order. Optional members are omitted when absent (never written as null).
    private static void WriteEnvelope(Utf8JsonWriter writer, in CheckpointEnvelope envelope, PooledUtf8Map<int> retryCounters)
    {
        writer.WriteStartObject();
        writer.WriteString("runId"u8, envelope.RunId.Value);
        writer.WriteString("environment"u8, envelope.Environment);
        writer.WriteString("workflowId"u8, envelope.WorkflowId);
        writer.WriteString("status"u8, StatusName(envelope.Status));
        writer.WriteNumber("cursor"u8, envelope.Cursor);

        // The per-run write sequence (ADR 0065 decision 6): authored by the party that authors the checkpoint, inside
        // the region the MAC will cover, because the server validates a proposed save against the persisted value.
        writer.WriteNumber(SequenceUtf8, envelope.Sequence);
        if (envelope.Epoch is { } epoch)
        {
            writer.WriteNumber("epoch"u8, epoch);
        }

        writer.WriteString("createdAt"u8, envelope.CreatedAt);
        if (envelope.UpdatedAt is { } updatedAt)
        {
            writer.WriteString("updatedAt"u8, updatedAt);
        }

        if (envelope.CorrelationId is { } correlationId)
        {
            writer.WriteString("correlationId"u8, correlationId);
        }

        if (envelope.RerunOf is { } rerunOf)
        {
            writer.WriteString("rerunOf"u8, rerunOf);
        }

        if (!envelope.Tags.IsEmpty)
        {
            writer.WritePropertyName("tags"u8);
            envelope.Tags.WriteTo(writer);
        }

        if (!envelope.SecurityTags.IsEmpty)
        {
            writer.WritePropertyName("securityTags"u8);
            envelope.SecurityTags.WriteTo(writer);
        }

        writer.WriteStartObject("retryCounters"u8);
        PooledUtf8Map<int>.Enumerator retryEnumerator = retryCounters.GetEnumerator();
        while (retryEnumerator.MoveNext())
        {
            writer.WriteNumber(retryEnumerator.CurrentKey, retryEnumerator.CurrentValue);
        }

        writer.WriteEndObject();

        // The per-step journal (ADR 0050): payload-free metadata entries, one per step execution, in order.
        if (envelope.StepJournal is { Count: > 0 } journal)
        {
            writer.WriteStartArray("stepJournal"u8);
            for (int i = 0; i < journal.Count; i++)
            {
                WorkflowStepJournalEntry entry = journal[i];
                writer.WriteStartObject();
                writer.WriteString("stepId"u8, entry.StepId);
                writer.WriteString("status"u8, StepStatusName(entry.Status));
                writer.WriteNumber("attempt"u8, entry.Attempt);
                writer.WriteString("startedAt"u8, entry.StartedAt);
                writer.WriteString("endedAt"u8, entry.EndedAt);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (envelope.JournalTruncated)
            {
                writer.WriteBoolean("journalTruncated"u8, true);
            }
        }

        if (envelope.Wait is { } w)
        {
            writer.WriteStartObject("wait"u8);
            writer.WriteString("kind"u8, WaitKindName(w.Kind));
            if (w.Kind == WorkflowWaitKind.Timer)
            {
                writer.WriteString("dueAt"u8, w.DueAt);
            }
            else if (w.Kind == WorkflowWaitKind.Message)
            {
                writer.WriteString("channel"u8, w.Channel);
                if (w.CorrelationId is { } waitCorrelationId)
                {
                    writer.WriteString("correlationId"u8, waitCorrelationId);
                }
            }

            // A §18 Pause wait carries no wake trigger: the kind alone is the whole record.
            writer.WriteEndObject();
        }

        if (envelope.Fault is { } f)
        {
            writer.WriteStartObject("fault"u8);
            writer.WriteString("stepId"u8, f.StepId);
            writer.WriteNumber("attempt"u8, f.Attempt);
            writer.WriteString("error"u8, f.Error);
            writer.WriteString("at"u8, f.At);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    // The payload in its fixed property order.
    private static void WritePayload(Utf8JsonWriter writer, IReadOnlyDictionary<string, byte[]> correlationTokens, in JsonElement inputs, PooledUtf8Map<JsonElement> stepOutputs, in JsonElement outputs)
    {
        writer.WriteStartObject();
        writer.WriteStartObject("correlationTokens"u8);
        foreach (KeyValuePair<string, byte[]> token in correlationTokens)
        {
            writer.WriteBase64String(token.Key, token.Value);
        }

        writer.WriteEndObject();

        // Optional values are omitted when undefined (not written as null): "not present" is Undefined.
        WriteInputs(writer, inputs);
        if (outputs.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("outputs"u8);
            outputs.WriteTo(writer);
        }

        WriteStepOutputs(writer, stepOutputs);
        writer.WriteEndObject();
    }

    private static void WriteInputs(Utf8JsonWriter writer, in JsonElement inputs)
    {
        if (inputs.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName("inputs"u8);
            inputs.WriteTo(writer);
        }
    }

    private static void WriteStepOutputs(Utf8JsonWriter writer, PooledUtf8Map<JsonElement> stepOutputs)
    {
        writer.WriteStartObject("stepOutputs"u8);
        PooledUtf8Map<JsonElement>.Enumerator enumerator = stepOutputs.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.CurrentValue.ValueKind == JsonValueKind.Undefined)
            {
                continue;
            }

            writer.WritePropertyName(enumerator.CurrentKey);
            enumerator.CurrentValue.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    // Reads the envelope under its closed schema: every member is one it names, and the required ones are present.
    // The retry counters come out as a pooled map the caller owns.
    private static CheckpointEnvelope ReadEnvelope(in JsonElement root, out PooledUtf8Map<int> retryCounters)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowCheckpointRowMalformed();
        }

        string? runId = null;
        string? environment = null;
        string? workflowId = null;
        WorkflowRunStatus? status = null;
        int? cursor = null;
        long? sequence = null;
        long? epoch = null;
        DateTimeOffset? createdAt = null;
        DateTimeOffset? updatedAt = null;
        string? correlationId = null;
        string? rerunOf = null;
        TagSet tags = default;
        SecurityTagSet securityTags = default;
        PooledUtf8Map<int>? counters = null;
        List<WorkflowStepJournalEntry>? journal = null;
        bool journalTruncated = false;
        WorkflowWait? wait = null;
        WorkflowFault? fault = null;
        try
        {
            foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
            {
                JsonElement value = property.Value;
                if (property.NameEquals("runId"u8))
                {
                    runId = RequiredString(value, "runId");
                }
                else if (property.NameEquals("environment"u8))
                {
                    environment = RequiredString(value, "environment");
                }
                else if (property.NameEquals("workflowId"u8))
                {
                    workflowId = RequiredString(value, "workflowId");
                }
                else if (property.NameEquals("status"u8))
                {
                    status = Enum.TryParse(RequiredString(value, "status"), out WorkflowRunStatus parsed) ? parsed : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "status");
                }
                else if (property.NameEquals("cursor"u8))
                {
                    cursor = value.GetInt32();
                }
                else if (property.NameEquals(SequenceUtf8))
                {
                    sequence = value.GetInt64();
                }
                else if (property.NameEquals("epoch"u8))
                {
                    epoch = value.GetInt64();
                }
                else if (property.NameEquals("createdAt"u8))
                {
                    createdAt = value.GetDateTimeOffset();
                }
                else if (property.NameEquals("updatedAt"u8))
                {
                    updatedAt = value.GetDateTimeOffset();
                }
                else if (property.NameEquals("correlationId"u8))
                {
                    correlationId = RequiredString(value, "correlationId");
                }
                else if (property.NameEquals("rerunOf"u8))
                {
                    rerunOf = RequiredString(value, "rerunOf");
                }
                else if (property.NameEquals("tags"u8))
                {
                    tags = value.ValueKind == JsonValueKind.Array ? TagSet.CopyFrom(value) : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "tags");
                }
                else if (property.NameEquals("securityTags"u8))
                {
                    securityTags = value.ValueKind == JsonValueKind.Array ? SecurityTagSet.CopyFrom(value) : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "securityTags");
                }
                else if (property.NameEquals("retryCounters"u8))
                {
                    counters = PooledUtf8Map<int>.Rent(value.GetPropertyCount());
                    foreach (JsonProperty<JsonElement> counter in value.EnumerateObject())
                    {
                        using UnescapedUtf8JsonString name = counter.Utf8NameSpan;
                        counters.Set(name.Span, counter.Value.GetInt32());
                    }
                }
                else if (property.NameEquals("stepJournal"u8))
                {
                    if (value.ValueKind != JsonValueKind.Array)
                    {
                        throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "stepJournal");
                    }

                    journal = new List<WorkflowStepJournalEntry>(value.GetArrayLength());
                    foreach (JsonElement entry in value.EnumerateArray())
                    {
                        journal.Add(new WorkflowStepJournalEntry(
                            RequiredString(entry.GetProperty("stepId"u8), "stepJournal"),
                            Enum.TryParse(RequiredString(entry.GetProperty("status"u8), "stepJournal"), out WorkflowStepStatus stepStatus) ? stepStatus : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "stepJournal"),
                            entry.GetProperty("attempt"u8).GetInt32(),
                            entry.GetProperty("startedAt"u8).GetDateTimeOffset(),
                            entry.GetProperty("endedAt"u8).GetDateTimeOffset()));
                    }
                }
                else if (property.NameEquals("journalTruncated"u8))
                {
                    journalTruncated = value.GetBoolean();
                }
                else if (property.NameEquals("wait"u8))
                {
                    WorkflowWaitKind kind = Enum.TryParse(RequiredString(value.GetProperty("kind"u8), "wait"), out WorkflowWaitKind parsedKind) ? parsedKind : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, "wait");
                    wait = kind switch
                    {
                        WorkflowWaitKind.Timer => WorkflowWait.Timer(value.GetProperty("dueAt"u8).GetDateTimeOffset()),
                        WorkflowWaitKind.Pause => WorkflowWait.Pause(),
                        _ => WorkflowWait.Message(
                            RequiredString(value.GetProperty("channel"u8), "wait"),
                            value.TryGetProperty("correlationId"u8, out JsonElement waitCorrelation) ? RequiredString(waitCorrelation, "wait") : null),
                    };
                }
                else if (property.NameEquals("fault"u8))
                {
                    fault = new WorkflowFault(
                        RequiredString(value.GetProperty("stepId"u8), "fault"),
                        value.GetProperty("attempt"u8).GetInt32(),
                        RequiredString(value.GetProperty("error"u8), "fault"),
                        value.GetProperty("at"u8).GetDateTimeOffset());
                }
                else
                {
                    throw ThrowHelper.GetCheckpointRegionUnknownMemberException(RunnerRegion, property.Name);
                }
            }

            retryCounters = counters ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "retryCounters");
            return new CheckpointEnvelope(
                new WorkflowRunId(runId ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "runId")),
                environment ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "environment"),
                workflowId ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "workflowId"),
                status ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "status"),
                cursor ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "cursor"),
                sequence ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "sequence"),
                epoch,
                createdAt ?? throw ThrowHelper.GetCheckpointRegionMissingMemberException(RunnerRegion, "createdAt"),
                updatedAt,
                correlationId,
                rerunOf,
                tags,
                securityTags,
                journal ?? [],
                journalTruncated,
                wait,
                fault);
        }
        catch
        {
            counters?.Dispose();
            throw;
        }

        static string RequiredString(in JsonElement element, string member)
            => element.ValueKind == JsonValueKind.String
                ? element.GetString()!
                : throw ThrowHelper.GetCheckpointRegionMalformedMemberException(RunnerRegion, member);
    }

    // Map the enums to their names via constant strings, so serialising a checkpoint does not allocate a string per
    // call the way Enum.ToString() does. Names match the enum members so Enum.TryParse round-trips.
    private static string StepStatusName(WorkflowStepStatus status) => status switch
    {
        WorkflowStepStatus.Succeeded => nameof(WorkflowStepStatus.Succeeded),
        WorkflowStepStatus.Faulted => nameof(WorkflowStepStatus.Faulted),
        WorkflowStepStatus.Skipped => nameof(WorkflowStepStatus.Skipped),
        WorkflowStepStatus.Retrying => nameof(WorkflowStepStatus.Retrying),
        _ => nameof(WorkflowStepStatus.Succeeded),
    };

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
        WorkflowWaitKind.Pause => nameof(WorkflowWaitKind.Pause),
        _ => kind.ToString(),
    };
}

/// <summary>
/// The runner region's scalars (ADR 0065 decision 4): the run-management structure the runner authors, which the
/// control plane reads on every save and, once the region carries a MAC, cannot rewrite.
/// </summary>
/// <param name="RunId">The run id.</param>
/// <param name="Environment">The environment the run is pinned to (decision 9); checked against the row's address on every save.</param>
/// <param name="WorkflowId">The id of the workflow the run executes.</param>
/// <param name="Status">The run's lifecycle status as the runner left it.</param>
/// <param name="Cursor">The cursor (state-machine index of the next step to run).</param>
/// <param name="Sequence">The per-run write sequence this checkpoint is persisted at (decision 6).</param>
/// <param name="Epoch">The lease epoch the writer holds the run under (decision 6), or <see langword="null"/> for a writer with no lease grant.</param>
/// <param name="CreatedAt">When the run was first created.</param>
/// <param name="UpdatedAt">When this checkpoint is written.</param>
/// <param name="CorrelationId">The run-wide telemetry correlation id (the W3C trace id) set at creation, if any.</param>
/// <param name="RerunOf">The id of the run this run re-runs, if any.</param>
/// <param name="Tags">The free-form tags applied to the run at creation, if any.</param>
/// <param name="SecurityTags">The security tags applied to the run at creation, if any (design §14.2).</param>
/// <param name="StepJournal">The per-step journal (ADR 0050).</param>
/// <param name="JournalTruncated">Whether the journal was capped and its oldest entries dropped.</param>
/// <param name="Wait">The wait the run is suspended on, if it is.</param>
/// <param name="Fault">The fault the runner recorded, if the run is faulted.</param>
public readonly record struct CheckpointEnvelope(
    WorkflowRunId RunId,
    string Environment,
    string WorkflowId,
    WorkflowRunStatus Status,
    int Cursor,
    long Sequence,
    long? Epoch,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? CorrelationId,
    string? RerunOf,
    TagSet Tags,
    SecurityTagSet SecurityTags,
    IReadOnlyList<WorkflowStepJournalEntry> StepJournal,
    bool JournalTruncated,
    WorkflowWait? Wait,
    WorkflowFault? Fault);

/// <summary>What a runner's submission claims, read by the checkpoint surfaces before the server joins it with the control-plane region (<see cref="WorkflowCheckpointSerializer.TryReadSubmission"/>).</summary>
/// <param name="Environment">The environment the runner region claims (ADR 0065 decision 9).</param>
/// <param name="Sequence">The write sequence the runner region carries (decision 6).</param>
/// <param name="Epoch">The lease epoch the runner region carries (decision 6), or <see langword="null"/> when the writer holds no grant.</param>
/// <param name="Algorithm">The payload algorithm the header names (decision 5): <see cref="CheckpointAlgorithm.Clear"/> for a clear payload.</param>
/// <param name="KeyId">The key generation the submission is sealed under (decision 4), or <see langword="null"/> for a clear submission.</param>
/// <param name="HasMac">Whether the submission carries a MAC. The server cannot verify it; it can require it (decision 10).</param>
public readonly record struct CheckpointSubmission(string Environment, long Sequence, long? Epoch, CheckpointAlgorithm Algorithm, string? KeyId, bool HasMac);

/// <summary>
/// Everything a checkpoint surface reads from a stored row, from one parse of its envelope and control-plane region
/// (<see cref="WorkflowCheckpointSerializer.Project"/>).
/// </summary>
/// <param name="Index">The effective index entry the row projects to.</param>
/// <param name="Environment">The environment the runner region claims (ADR 0065 decision 9).</param>
/// <param name="Sequence">The write sequence the runner region carries (decision 6).</param>
/// <param name="Epoch">The lease epoch the runner region carries (decision 6), or <see langword="null"/> when the writer holds no grant.</param>
/// <param name="Facts">The execution-budget facts (ADR 0068).</param>
/// <param name="ControlPlaneRegion">The control-plane region's bytes as the row carries them (decision 7): the server's, which a runner never submits.</param>
public readonly record struct CheckpointProjection(WorkflowRunIndexEntry Index, string Environment, long Sequence, long? Epoch, CheckpointBudgetFacts Facts, ReadOnlyMemory<byte> ControlPlaneRegion);