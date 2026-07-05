// <copyright file="WorkspaceSimulationJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Testing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The <c>simulateWorkingCopy</c> seam (workflow-designer design §4.3/§8): maps the generated
/// request model onto the simulator's scenario/stop/budget, extracts the document and source bytes
/// the compiler consumes (reordering the <c>workflows</c> array so the chosen workflow compiles
/// first), and writes the structured trace response in one pooled pass.
/// </summary>
internal static class WorkspaceSimulationJson
{
    private const int MaxStepsCeiling = 1024;
    private const int WallClockCeilingMs = 30_000;

    /// <summary>
    /// Extracts the document bytes the simulator compiles, with <paramref name="workflowId"/> moved
    /// to the front of <c>workflows</c> (the executor provider compiles the first workflow).
    /// Returns <see langword="null"/> when the id names no workflow in the document.
    /// </summary>
    public static byte[]? DocumentBytes(in JsonElement document, string? workflowId)
    {
        if (workflowId is not null && !HasWorkflow(document, workflowId))
        {
            return null;
        }

        return PersistedJson.ToArray(
            (Document: document, WorkflowId: workflowId),
            static (Utf8JsonWriter writer, in (JsonElement Document, string? WorkflowId) state) =>
            {
                if (state.WorkflowId is null)
                {
                    state.Document.WriteTo(writer);
                    return;
                }

                writer.WriteStartObject();
                foreach (JsonProperty<JsonElement> property in state.Document.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array && property.NameEquals("workflows"u8))
                    {
                        writer.WritePropertyName("workflows"u8);
                        writer.WriteStartArray();
                        foreach (JsonElement workflow in property.Value.EnumerateArray())
                        {
                            if (IsWorkflow(workflow, state.WorkflowId))
                            {
                                workflow.WriteTo(writer);
                            }
                        }

                        foreach (JsonElement workflow in property.Value.EnumerateArray())
                        {
                            if (!IsWorkflow(workflow, state.WorkflowId))
                            {
                                workflow.WriteTo(writer);
                            }
                        }

                        writer.WriteEndArray();
                    }
                    else
                    {
                        using UnescapedUtf8JsonString name = property.Utf8NameSpan;
                        writer.WritePropertyName(name.Span);
                        property.Value.WriteTo(writer);
                    }
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Resolves the working copy's attachments to the (name, document bytes) list the compiler
    /// consumes: inline attachments serialise their stored document; registry attachments re-resolve
    /// reach-checked. Unresolvable attachments are skipped — the compile then reports the document
    /// not executable, which is the honest outcome.
    /// </summary>
    public static async ValueTask<List<KeyValuePair<string, byte[]>>> SourceBytesAsync(
        JsonElement attachments,
        ISourceStore? registry,
        AccessContext reach,
        CancellationToken cancellationToken)
    {
        var sources = new List<KeyValuePair<string, byte[]>>();
        if (attachments.ValueKind != JsonValueKind.Array)
        {
            return sources;
        }

        foreach (JsonElement attachment in attachments.EnumerateArray())
        {
            if (!attachment.TryGetProperty("name"u8, out JsonElement nameElement) || nameElement.GetString() is not { Length: > 0 } name)
            {
                continue;
            }

            if (attachment.TryGetProperty("document"u8, out JsonElement inline) && inline.ValueKind == JsonValueKind.Object)
            {
                sources.Add(new(name, PersistedJson.ToArray(inline, static (Utf8JsonWriter w, in JsonElement d) => d.WriteTo(w))));
                continue;
            }

            if (registry is not null
                && attachment.TryGetProperty("sourceName"u8, out JsonElement sn)
                && sn.GetString() is { Length: > 0 } sourceName)
            {
                using ParsedJsonDocument<RegisteredSource>? registered = await registry.GetAsync(sourceName, reach, cancellationToken).ConfigureAwait(false);
                if (registered is { } r)
                {
                    JsonElement doc = (JsonElement)r.RootElement.Document;
                    sources.Add(new(name, PersistedJson.ToArray(doc, static (Utf8JsonWriter w, in JsonElement d) => d.WriteTo(w))));
                }
            }
        }

        return sources;
    }

    /// <summary>Maps the request's inline scenario (absent pieces default deterministically).</summary>
    public static SimulationScenario ReadScenario(in Models.SimulateRequest body)
    {
        Models.SimulateRequest.InlineScenario scenario = body.Scenario;
        if (scenario.IsUndefined())
        {
            return new SimulationScenario();
        }

        var mocks = new List<SimulationMockRoute>();
        JsonElement mocksElement = (JsonElement)scenario.Mocks;
        if (mocksElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mock in mocksElement.EnumerateArray())
            {
                if (mock.TryGetProperty("method"u8, out JsonElement method) && method.GetString() is { Length: > 0 } m
                    && mock.TryGetProperty("path"u8, out JsonElement path) && path.GetString() is { Length: > 0 } p
                    && mock.TryGetProperty("status"u8, out JsonElement status) && status.ValueKind == JsonValueKind.Number)
                {
                    mock.TryGetProperty("body"u8, out JsonElement mockBody);
                    mocks.Add(new SimulationMockRoute(m, p, status.GetInt32(), mockBody));
                }
            }
        }

        var triggers = new List<SimulationTrigger>();
        JsonElement triggersElement = (JsonElement)scenario.Triggers;
        if (triggersElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement trigger in triggersElement.EnumerateArray())
            {
                if (trigger.TryGetProperty("channel"u8, out JsonElement channel) && channel.GetString() is { Length: > 0 } c)
                {
                    trigger.TryGetProperty("payload"u8, out JsonElement payload);
                    string? correlationId = trigger.TryGetProperty("correlationId"u8, out JsonElement corr) ? corr.GetString() : null;
                    triggers.Add(new SimulationTrigger(c, payload, correlationId));
                }
            }
        }

        bool autoAdvance = true;
        if (scenario.Clock.IsNotUndefined() && ((JsonElement)scenario.Clock).TryGetProperty("autoAdvance"u8, out JsonElement auto)
            && auto.ValueKind == JsonValueKind.False)
        {
            autoAdvance = false;
        }

        return new SimulationScenario
        {
            Inputs = scenario.Inputs,
            Mocks = mocks,
            Triggers = triggers,
            AutoAdvanceClock = autoAdvance,
        };
    }

    /// <summary>Maps the request's stop condition, if any.</summary>
    public static SimulationStop? ReadStop(in Models.SimulateRequest body)
    {
        Models.SimulateRequest.StopCondition until = body.Until;
        if (until.IsUndefined())
        {
            return null;
        }

        var breakpoints = new HashSet<string>(StringComparer.Ordinal);
        JsonElement breakpointsElement = (JsonElement)until.Breakpoints;
        if (breakpointsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement breakpoint in breakpointsElement.EnumerateArray())
            {
                if (breakpoint.GetString() is { Length: > 0 } id)
                {
                    breakpoints.Add(id);
                }
            }
        }

        return new SimulationStop
        {
            BeforeStepId = until.BeforeStepId.IsNotUndefined() ? (string)until.BeforeStepId : null,
            Occurrence = until.Occurrence.IsNotUndefined() ? Math.Max(1, (int)until.Occurrence) : 1,
            Breakpoints = breakpoints,
        };
    }

    /// <summary>Maps the request's budget, clamped to the server's ceilings.</summary>
    public static SimulationBudget ReadBudget(in Models.SimulateRequest body)
    {
        Models.SimulateRequest.SimulationBudget budget = body.Budget;
        int maxSteps = 256;
        int wallClockMs = 10_000;
        if (budget.IsNotUndefined())
        {
            JsonElement element = (JsonElement)budget;
            if (element.TryGetProperty("maxSteps"u8, out JsonElement steps) && steps.ValueKind == JsonValueKind.Number)
            {
                maxSteps = Math.Clamp(steps.GetInt32(), 1, MaxStepsCeiling);
            }

            if (element.TryGetProperty("wallClockMs"u8, out JsonElement wall) && wall.ValueKind == JsonValueKind.Number)
            {
                wallClockMs = Math.Clamp(wall.GetInt32(), 100, WallClockCeilingMs);
            }
        }

        return new SimulationBudget { MaxSteps = maxSteps, WallClock = TimeSpan.FromMilliseconds(wallClockMs) };
    }

    /// <summary>Writes the structured trace response in one pooled pass.</summary>
    public static ParsedJsonDocument<Models.SimulationTrace> TraceResponse(SimulationResult result)
    {
        return PersistedJson.ToPooledDocument<Models.SimulationTrace, SimulationResult>(
            result,
            static (Utf8JsonWriter writer, in SimulationResult r) => WriteTrace(writer, r));
    }

    /// <summary>Writes one trace object (shared by the simulate response and each scenario run result).</summary>
    public static void WriteTrace(Utf8JsonWriter writer, in SimulationResult r)
    {
        {
            {
                writer.WriteStartObject();
                writer.WriteString("outcome"u8, r.Outcome switch
                {
                    SimulationOutcome.Completed => "completed"u8,
                    SimulationOutcome.Faulted => "faulted"u8,
                    SimulationOutcome.Paused => "paused"u8,
                    SimulationOutcome.Suspended => "suspended"u8,
                    _ => "budgetExhausted"u8,
                });

                if (r.PausedBefore is not null)
                {
                    writer.WriteString("pausedBefore"u8, r.PausedBefore);
                }

                if (r.Outputs.ValueKind is not JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("outputs"u8);
                    r.Outputs.WriteTo(writer);
                }

                if (r.Fault is { } fault)
                {
                    writer.WriteStartObject("fault"u8);
                    writer.WriteString("stepId"u8, fault.StepId);
                    writer.WriteNumber("attempt"u8, fault.Attempt);
                    writer.WriteString("error"u8, fault.Error);
                    writer.WriteEndObject();
                }

                if (r.Wait is { } wait)
                {
                    writer.WriteStartObject("wait"u8);
                    writer.WriteString("kind"u8, wait.Kind == WorkflowWaitKind.Timer ? "timer"u8 : "message"u8);
                    if (wait.Kind == WorkflowWaitKind.Timer)
                    {
                        writer.WriteString("dueAt"u8, wait.DueAt);
                    }

                    if (wait.Channel is not null)
                    {
                        writer.WriteString("channel"u8, wait.Channel);
                    }

                    if (wait.CorrelationId is not null)
                    {
                        writer.WriteString("correlationId"u8, wait.CorrelationId);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteStartArray("steps"u8);
                foreach (SimulatedStepRecord step in r.Steps)
                {
                    writer.WriteStartObject();
                    writer.WriteString("stepId"u8, step.StepId);
                    writer.WriteString("status"u8, step.Faulted ? "faulted"u8 : "completed"u8);
                    writer.WriteNumber("attempt"u8, step.Attempt);
                    if (step.Outputs.ValueKind is not JsonValueKind.Undefined)
                    {
                        writer.WritePropertyName("outputs"u8);
                        step.Outputs.WriteTo(writer);
                    }

                    if (step.ExchangeCount > 0)
                    {
                        writer.WriteStartArray("requests"u8);
                        for (int i = 0; i < step.ExchangeCount; i++)
                        {
                            MockApiExchange exchange = r.Exchanges[step.FirstExchange + i];
                            writer.WriteStartObject();
                            writer.WriteString("method"u8, exchange.Method.ToString().ToLowerInvariant());
                            writer.WriteString("path"u8, exchange.Path);
                            writer.WriteNumber("status"u8, exchange.StatusCode);
                            if (!exchange.ResponseBody.IsEmpty)
                            {
                                writer.WritePropertyName("responseBody"u8);
                                writer.WriteRawValue(exchange.ResponseBody.Span, skipInputValidation: false);
                            }

                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }

                    if (step.SuccessCriteria.Count > 0)
                    {
                        writer.WriteStartArray("successCriteria"u8);
                        foreach (SimulatedCriterionVerdict verdict in step.SuccessCriteria)
                        {
                            writer.WriteStartObject();
                            writer.WriteString("condition"u8, verdict.Condition);
                            writer.WriteBoolean("satisfied"u8, verdict.Satisfied);
                            writer.WriteEndObject();
                        }

                        writer.WriteEndArray();
                    }

                    if (step.ActionTaken is { } action)
                    {
                        writer.WriteStartObject("actionTaken"u8);
                        writer.WriteString("type"u8, action.Type);
                        if (action.Name is not null)
                        {
                            writer.WriteString("name"u8, action.Name);
                        }

                        if (action.Target is not null)
                        {
                            writer.WriteString("target"u8, action.Target);
                        }

                        writer.WriteEndObject();
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();

                if (r.ClockAdvances.Count > 0)
                {
                    writer.WriteStartArray("clockAdvances"u8);
                    foreach (SimulationClockAdvance advance in r.ClockAdvances)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("to"u8, advance.To);
                        writer.WriteString("reason"u8, advance.Reason);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                }

                writer.WriteNumber("stepsExecuted"u8, r.StepsExecuted);
                writer.WriteEndObject();
            }
        }
    }

    private static bool HasWorkflow(in JsonElement document, string workflowId)
    {
        if (!document.TryGetProperty("workflows"u8, out JsonElement workflows) || workflows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement workflow in workflows.EnumerateArray())
        {
            if (IsWorkflow(workflow, workflowId))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWorkflow(in JsonElement workflow, string workflowId)
        => workflow.TryGetProperty("workflowId"u8, out JsonElement id) && id.ValueEquals(workflowId);
}