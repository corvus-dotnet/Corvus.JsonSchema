// <copyright file="ControlPlaneRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Frozen;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The control-plane-authored region of a checkpoint row (ADR 0065 decision 7): the fields the control plane writes
/// about a run and the runner never authors. It is the last region of the row, joined at read time and outside the
/// runner's submitted bytes, so a control-plane write never touches the runner's octets and a runner save never
/// carries a control-plane decision the runner made up. Every reader joins it with the runner region to get the run's
/// effective state (<see cref="WorkflowCheckpointSerializer.Deserialize"/>).
/// </summary>
/// <remarks>
/// <para>
/// Two of the fields are <em>requests against a sequence</em>: a resume request and a budget fault each record the
/// runner sequence they were written against, and take effect only while the runner region is still at that sequence
/// or below. The runner consumes them by advancing, which is the only thing a runner can do to a control-plane field:
/// a resume request is honoured by the runner's next save, and a budget fault is superseded by a save the coordinator
/// accepted under a re-resolved budget. Cancellation is unconditional: a cancelled run stays cancelled whatever the
/// runner writes afterwards.
/// </para>
/// <para>
/// The region is a closed schema in fixed property order, written and read here only, so its bytes are deterministic
/// for a given record and an unknown member is a malformed row rather than data.
/// </para>
/// </remarks>
/// <param name="Budget">The execution budget the control plane resolved for the run (ADR 0068), frozen with its identity.</param>
/// <param name="BudgetFault">The budget fault the control plane recorded against the run, if any.</param>
/// <param name="Cancellation">The run's cancellation, if the control plane cancelled it.</param>
/// <param name="Pause">The debugger pause configuration the control plane set (design §18), if any.</param>
/// <param name="ResumeRequest">The outstanding resume request, if the control plane marked the run resume-claimable.</param>
public readonly record struct ControlPlaneRecord(
    ExecutionBudget? Budget = null,
    ControlPlaneBudgetFault? BudgetFault = null,
    ControlPlaneCancellation? Cancellation = null,
    WorkflowPauseConfig? Pause = null,
    ControlPlaneResumeRequest? ResumeRequest = null)
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Whether the budget fault is in effect against a runner region at <paramref name="runnerSequence"/>.</summary>
    /// <param name="runnerSequence">The sequence the runner region carries.</param>
    /// <returns><see langword="true"/> when the run is faulted on its budget by the control plane's decision.</returns>
    public bool IsBudgetFaultedAt(long runnerSequence)
        => this.BudgetFault is { } fault && fault.AgainstSequence >= runnerSequence;

    /// <summary>Whether a resume request is outstanding against a runner region at <paramref name="runnerSequence"/>.</summary>
    /// <param name="runnerSequence">The sequence the runner region carries.</param>
    /// <returns>When the request was made, or <see langword="null"/> when none is outstanding.</returns>
    public DateTimeOffset? ResumeRequestedAt(long runnerSequence)
        => this.ResumeRequest is { } request && request.AgainstSequence >= runnerSequence ? request.At : null;

    /// <summary>Parses the region. An empty region is the default record: nothing decided.</summary>
    /// <param name="regionUtf8">The region's bytes.</param>
    /// <returns>The record.</returns>
    /// <exception cref="FormatException">The region is not a control-plane record.</exception>
    public static ControlPlaneRecord Parse(ReadOnlyMemory<byte> regionUtf8)
    {
        if (regionUtf8.IsEmpty)
        {
            return default;
        }

        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(regionUtf8);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            ThrowHelper.ThrowCheckpointRowMalformed();
        }

        ExecutionBudget? budget = null;
        ControlPlaneBudgetFault? budgetFault = null;
        ControlPlaneCancellation? cancellation = null;
        WorkflowPauseConfig? pause = null;
        ControlPlaneResumeRequest? resumeRequest = null;
        foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
        {
            if (property.NameEquals(ExecutionBudget.JsonPropertyNames.BudgetUtf8))
            {
                budget = ExecutionBudget.TryRead(property.Value, out ExecutionBudget read) ? read : throw ThrowHelper.GetCheckpointRegionMalformedMemberException("control-plane", "budget");
            }
            else if (property.NameEquals("budgetFault"u8))
            {
                JsonElement fault = property.Value;
                budgetFault = new ControlPlaneBudgetFault(
                    fault.GetProperty("error"u8).GetString() ?? throw ThrowHelper.GetCheckpointRegionMalformedMemberException("control-plane", "budgetFault"),
                    fault.GetProperty("at"u8).GetDateTimeOffset(),
                    fault.GetProperty("sequence"u8).GetInt64());
            }
            else if (property.NameEquals("cancelled"u8))
            {
                cancellation = new ControlPlaneCancellation(property.Value.GetProperty("at"u8).GetDateTimeOffset());
            }
            else if (property.NameEquals("pause"u8))
            {
                pause = ReadPause(property.Value);
            }
            else if (property.NameEquals("resumeRequested"u8))
            {
                JsonElement request = property.Value;
                resumeRequest = new ControlPlaneResumeRequest(request.GetProperty("at"u8).GetDateTimeOffset(), request.GetProperty("sequence"u8).GetInt64());
            }
            else
            {
                throw ThrowHelper.GetCheckpointRegionUnknownMemberException("control-plane", property.Name);
            }
        }

        return new ControlPlaneRecord(budget, budgetFault, cancellation, pause, resumeRequest);
    }

    /// <summary>Serializes the record to the region's bytes: an empty region for the default record, otherwise a fixed-order object.</summary>
    /// <returns>The region's bytes.</returns>
    public byte[] ToUtf8()
    {
        if (this == default)
        {
            return [];
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, 256, out IByteBufferWriter buffer);
        try
        {
            this.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Writes the record as a JSON object in its fixed property order.</summary>
    /// <param name="writer">The writer.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        if (this.Budget is { } budget)
        {
            budget.WriteTo(writer);
        }

        if (this.BudgetFault is { } fault)
        {
            writer.WriteStartObject("budgetFault"u8);
            writer.WriteString("at"u8, fault.At);
            writer.WriteString("error"u8, fault.Error);
            writer.WriteNumber("sequence"u8, fault.AgainstSequence);
            writer.WriteEndObject();
        }

        if (this.Cancellation is { } cancellation)
        {
            writer.WriteStartObject("cancelled"u8);
            writer.WriteString("at"u8, cancellation.At);
            writer.WriteEndObject();
        }

        if (this.Pause is { } pause)
        {
            writer.WriteStartObject("pause"u8);
            writer.WriteBoolean("afterEachStep"u8, pause.AfterEachStep);
            writer.WriteStartArray("breakpoints"u8);
            if (pause.BreakpointCursors is { } breakpoints)
            {
                foreach (int breakpoint in breakpoints)
                {
                    writer.WriteNumberValue(breakpoint);
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (this.ResumeRequest is { } request)
        {
            writer.WriteStartObject("resumeRequested"u8);
            writer.WriteString("at"u8, request.At);
            writer.WriteNumber("sequence"u8, request.AgainstSequence);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static WorkflowPauseConfig ReadPause(in JsonElement element)
    {
        bool afterEachStep = element.TryGetProperty("afterEachStep"u8, out JsonElement afterEachStepElement) && afterEachStepElement.GetBoolean();

        // The breakpoint set is allocated only when there are breakpoints; a single-step pause shares the empty set.
        IReadOnlySet<int> breakpoints = FrozenSet<int>.Empty;
        if (element.TryGetProperty("breakpoints"u8, out JsonElement breakpointsElement)
            && breakpointsElement.ValueKind == JsonValueKind.Array
            && breakpointsElement.GetArrayLength() > 0)
        {
            var cursors = new HashSet<int>();
            foreach (JsonElement breakpoint in breakpointsElement.EnumerateArray())
            {
                cursors.Add(breakpoint.GetInt32());
            }

            breakpoints = cursors;
        }

        return new WorkflowPauseConfig(afterEachStep, breakpoints);
    }
}

/// <summary>A budget fault the control plane recorded against a run (ADR 0068), in effect while the runner region is at or below <paramref name="AgainstSequence"/>.</summary>
/// <param name="Error">The budget fault's error type, one of <see cref="ExecutionBudgetFault"/>.</param>
/// <param name="At">When the fault was decided.</param>
/// <param name="AgainstSequence">The runner sequence the fault was recorded against.</param>
public readonly record struct ControlPlaneBudgetFault(string Error, DateTimeOffset At, long AgainstSequence);

/// <summary>A run's cancellation by the control plane. Unconditional: no later runner save undoes it.</summary>
/// <param name="At">When the run was cancelled.</param>
public readonly record struct ControlPlaneCancellation(DateTimeOffset At);

/// <summary>A resume request the control plane recorded (design §18), outstanding while the runner region is at or below <paramref name="AgainstSequence"/>.</summary>
/// <param name="At">When the resume was requested.</param>
/// <param name="AgainstSequence">The runner sequence the request was made against; the runner's next save consumes it.</param>
public readonly record struct ControlPlaneResumeRequest(DateTimeOffset At, long AgainstSequence);