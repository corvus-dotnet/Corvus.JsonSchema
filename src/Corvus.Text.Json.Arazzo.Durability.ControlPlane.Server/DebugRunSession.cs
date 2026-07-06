// <copyright file="DebugRunSession.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Testing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// One §18 debug run under the INTERIM executor (workflow-designer design §18 staging, slice 1):
/// a forward-only run of a working-copy draft in a development-class environment. The gates, the
/// pause/resume semantics, the trace shape, and the audit tuple are the real contract; execution
/// replays the draft captured at start through the deterministic simulator against spec-derived
/// mocks. Real transport — the environment's runner resolving its credential bindings as its own
/// identity (§13.5) — arrives with the engine seam, so no secret is involved anywhere here.
/// </summary>
/// <remarks>
/// <para>The record itself carries the §18 audit tuple (who started the run, which working copy,
/// which document etag, which environment, when) and every view returns it; the durable audit
/// trail rides the engine seam.</para>
/// <para>Advances and views synchronise on an internal lock so a view never tears across a
/// concurrent advance, and a cancelled run stays cancelled — a racing advance may not resurrect
/// it. The simulator replay itself runs outside the lock; its outcome is applied atomically.</para>
/// </remarks>
internal sealed class DebugRunSession
{
    private readonly Lock syncRoot = new();

    private string status = "running";
    private int cursor;
    private byte[]? traceUtf8;
    private DateTimeOffset updatedAt;
    private bool pauseAfterEachStep;
    private string[] breakpoints = [];

    /// <summary>Gets the run id (the <c>debugRunId</c> path segment).</summary>
    public required string DebugRunId { get; init; }

    /// <summary>Gets the working copy the run belongs to (the nested route's parent).</summary>
    public required string WorkingCopyId { get; init; }

    /// <summary>Gets the workflow within the document the run executes.</summary>
    public required string WorkflowId { get; init; }

    /// <summary>Gets the target environment's name (its <c>allowsDraftRuns</c> gate passed at start).</summary>
    public required string EnvironmentName { get; init; }

    /// <summary>Gets the working-copy etag the run started from — the audited draft identity.</summary>
    public required string DocumentEtag { get; init; }

    /// <summary>Gets the audit actor who started the run.</summary>
    public required string StartedBy { get; init; }

    /// <summary>Gets when the run started.</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>Gets the Arazzo document captured at start (the chosen workflow first), so later
    /// advances replay the audited draft even if the working copy is edited meanwhile.</summary>
    public required byte[] DocumentBytes { get; init; }

    /// <summary>Gets the source documents captured at start, by <c>sourceDescriptions</c> name.</summary>
    public required IReadOnlyList<KeyValuePair<string, byte[]>> SourceBytes { get; init; }

    /// <summary>Gets the interim executor's transport: one scripted 2xx per operation the sources
    /// declare (see <see cref="SpecDerivedMocks"/>).</summary>
    public required IReadOnlyList<SimulationMockRoute> Mocks { get; init; }

    /// <summary>Gets the workflow inputs as captured UTF-8, or <see langword="null"/> for none.</summary>
    public required byte[]? InputsUtf8 { get; init; }

    /// <summary>Gets the run's current status (the contract enum:
    /// <c>running·paused·suspended·completed·faulted·cancelled</c>).</summary>
    public string StatusSnapshot
    {
        get
        {
            lock (this.syncRoot)
            {
                return this.status;
            }
        }
    }

    /// <summary>
    /// Derives the interim executor's transport from the captured source documents: every declared
    /// operation answers with its first <c>2xx</c> response code and an empty body — enough for
    /// status-code success criteria to hold while the trace records real request shapes. The engine
    /// seam replaces this with the environment's real transport.
    /// </summary>
    /// <param name="sources">The captured source documents, by attachment name.</param>
    /// <returns>The scripted routes.</returns>
    public static List<SimulationMockRoute> SpecDerivedMocks(IReadOnlyList<KeyValuePair<string, byte[]>> sources)
    {
        var mocks = new List<SimulationMockRoute>();
        foreach (KeyValuePair<string, byte[]> source in sources)
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(source.Value);
            if (!document.RootElement.TryGetProperty("paths"u8, out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (JsonProperty<JsonElement> path in paths.EnumerateObject())
            {
                if (path.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (JsonProperty<JsonElement> operation in path.Value.EnumerateObject())
                {
                    if (operation.Value.ValueKind == JsonValueKind.Object && IsHttpMethod(operation))
                    {
                        mocks.Add(new SimulationMockRoute(operation.Name, path.Name, FirstSuccessStatus(operation.Value), default));
                    }
                }
            }
        }

        return mocks;
    }

    /// <summary>Replaces the run's pause points (single-step and/or breakpoints).</summary>
    /// <param name="afterEachStep">Whether the run pauses after every executed step.</param>
    /// <param name="breakpoints">The steps the run pauses before.</param>
    public void SetPause(bool afterEachStep, string[] breakpoints)
    {
        lock (this.syncRoot)
        {
            this.pauseAfterEachStep = afterEachStep;
            this.breakpoints = breakpoints;
        }
    }

    /// <summary>Snapshots what the next advance replays to: the position reached so far and the
    /// pause points that bound it.</summary>
    /// <returns>The consistent (cursor, pause) triple.</returns>
    public (int Cursor, bool AfterEachStep, string[] Breakpoints) Plan()
    {
        lock (this.syncRoot)
        {
            return (this.cursor, this.pauseAfterEachStep, this.breakpoints);
        }
    }

    /// <summary>Applies one advance's outcome atomically. A cancelled run is terminal: a replay
    /// that was in flight when the cancel landed is discarded rather than resurrecting the run.</summary>
    /// <param name="status">The mapped status.</param>
    /// <param name="cursor">The steps executed.</param>
    /// <param name="traceUtf8">The trace (the shape simulation emits).</param>
    /// <param name="updatedAt">The advance time.</param>
    public void Apply(string status, int cursor, byte[] traceUtf8, DateTimeOffset updatedAt)
    {
        lock (this.syncRoot)
        {
            if (this.status == "cancelled")
            {
                return;
            }

            this.status = status;
            this.cursor = cursor;
            this.traceUtf8 = traceUtf8;
            this.updatedAt = updatedAt;
        }
    }

    /// <summary>Cancels the run (terminal).</summary>
    /// <param name="updatedAt">The cancel time.</param>
    public void Cancel(DateTimeOffset updatedAt)
    {
        lock (this.syncRoot)
        {
            this.status = "cancelled";
            this.updatedAt = updatedAt;
        }
    }

    /// <summary>Writes the run as the contract's <c>DebugRun</c> shape, into a pooled document the
    /// caller owns (hand it to the response workspace).</summary>
    /// <returns>The pooled view.</returns>
    public ParsedJsonDocument<Models.DebugRun> View()
        => PersistedJson.ToPooledDocument<Models.DebugRun, DebugRunSession>(
            this,
            static (Utf8JsonWriter writer, in DebugRunSession run) => run.Write(writer));

    private static bool IsHttpMethod(in JsonProperty<JsonElement> operation)
        => operation.NameEquals("get"u8) || operation.NameEquals("put"u8) || operation.NameEquals("post"u8)
        || operation.NameEquals("delete"u8) || operation.NameEquals("options"u8) || operation.NameEquals("head"u8)
        || operation.NameEquals("patch"u8) || operation.NameEquals("trace"u8);

    /// <summary>Finds the operation's first <c>2xx</c> response code, defaulting to <c>200</c>.</summary>
    private static int FirstSuccessStatus(in JsonElement operation)
    {
        if (operation.TryGetProperty("responses"u8, out JsonElement responses) && responses.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> response in responses.EnumerateObject())
            {
                using UnescapedUtf8JsonString code = response.Utf8NameSpan;
                ReadOnlySpan<byte> span = code.Span;
                if (span.Length == 3 && span[0] == (byte)'2' && char.IsAsciiDigit((char)span[1]) && char.IsAsciiDigit((char)span[2]))
                {
                    return 200 + ((span[1] - (byte)'0') * 10) + (span[2] - (byte)'0');
                }
            }
        }

        return 200;
    }

    private void Write(Utf8JsonWriter writer)
    {
        lock (this.syncRoot)
        {
            writer.WriteStartObject();
            writer.WriteString(Models.DebugRun.JsonPropertyNames.DebugRunIdUtf8, this.DebugRunId);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.WorkflowIdUtf8, this.WorkflowId);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.EnvironmentUtf8, this.EnvironmentName);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.StatusUtf8, this.status);
            writer.WriteNumber(Models.DebugRun.JsonPropertyNames.CursorUtf8, this.cursor);
            if (this.traceUtf8 is { } trace)
            {
                writer.WritePropertyName(Models.DebugRun.JsonPropertyNames.TraceUtf8);
                writer.WriteRawValue(trace);
            }

            writer.WriteString(Models.DebugRun.JsonPropertyNames.DocumentEtagUtf8, this.DocumentEtag);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.StartedByUtf8, this.StartedBy);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.StartedAtUtf8, this.StartedAt);
            writer.WriteString(Models.DebugRun.JsonPropertyNames.UpdatedAtUtf8, this.updatedAt);
            writer.WriteEndObject();
        }
    }
}