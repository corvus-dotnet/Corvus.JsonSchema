// <copyright file="ScenarioSuite.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;

namespace Corvus.Text.Json.Arazzo.Testing;

/// <summary>
/// The headless scenario engine (workflow-designer design §4.2/§4.5): resolve each mock's
/// (source, operationId) to its route from a source document, build the simulator's scenario,
/// judge the scenario's expectations against the trace, and write the run-result and trace
/// payloads. Shared verbatim by the control-plane server (run/publish endpoints) and the
/// <c>scenarios run</c> CLI, so an interactive run, a publish attestation, and a CI suite all
/// produce the same report shape.
/// </summary>
public static class ScenarioSuite
{
    /// <summary>One expectation's verdict.</summary>
    /// <param name="Kind">The expectation kind (<c>outcome</c> / <c>path</c> / <c>output</c> / <c>step</c>).</param>
    /// <param name="Passed">Whether the expectation held.</param>
    /// <param name="Detail">The judged detail (expected vs actual).</param>
    public readonly record struct Verdict(string Kind, bool Passed, string? Detail);

    /// <summary>
    /// Collects a source document's OpenAPI operations into the (source, operationId) → (method, path
    /// template) route map a scenario mock addresses operations by.
    /// </summary>
    /// <param name="source">The sourceDescriptions name the document is attached as.</param>
    /// <param name="sourceDocument">The source document value.</param>
    /// <param name="routes">The route map to add to.</param>
    public static void CollectRoutes(string source, in JsonElement sourceDocument, Dictionary<(string Source, string OperationId), (string Method, string Path)> routes)
    {
        if (!sourceDocument.TryGetProperty("paths"u8, out JsonElement paths) || paths.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty<JsonElement> path in paths.EnumerateObject())
        {
            if (path.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            using UnescapedUtf8JsonString pathName = path.Utf8NameSpan;
            string pathTemplate = System.Text.Encoding.UTF8.GetString(pathName.Span);
            foreach (JsonProperty<JsonElement> method in path.Value.EnumerateObject())
            {
                if (method.Value.ValueKind == JsonValueKind.Object
                    && method.Value.TryGetProperty("operationId"u8, out JsonElement id)
                    && id.GetString() is { Length: > 0 } operationId)
                {
                    using UnescapedUtf8JsonString methodName = method.Utf8NameSpan;
                    routes[(source, operationId)] = (System.Text.Encoding.UTF8.GetString(methodName.Span), pathTemplate);
                }
            }
        }
    }

    /// <summary>Builds the simulator's scenario from the stored scenario document.</summary>
    /// <param name="scenario">The scenario document (schema §4.2).</param>
    /// <param name="routes">The (source, operationId) → route map the mocks resolve through.</param>
    /// <returns>The simulator scenario.</returns>
    public static SimulationScenario BuildScenario(in JsonElement scenario, Dictionary<(string Source, string OperationId), (string Method, string Path)> routes)
    {
        scenario.TryGetProperty("inputs"u8, out JsonElement inputs);

        var mocks = new List<SimulationMockRoute>();
        if (scenario.TryGetProperty("mocks"u8, out JsonElement mockList) && mockList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement mock in mockList.EnumerateArray())
            {
                if (mock.TryGetProperty("source"u8, out JsonElement src) && src.GetString() is { Length: > 0 } source
                    && mock.TryGetProperty("operationId"u8, out JsonElement op) && op.GetString() is { Length: > 0 } operationId
                    && routes.TryGetValue((source, operationId), out (string Method, string Path) route)
                    && mock.TryGetProperty("responses"u8, out JsonElement responses) && responses.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement response in responses.EnumerateArray())
                    {
                        if (response.TryGetProperty("status"u8, out JsonElement status) && status.ValueKind == JsonValueKind.Number)
                        {
                            response.TryGetProperty("body"u8, out JsonElement body);
                            mocks.Add(new SimulationMockRoute(route.Method, route.Path, status.GetInt32(), body));
                        }
                    }
                }
            }
        }

        var triggers = new List<SimulationTrigger>();
        if (scenario.TryGetProperty("triggers"u8, out JsonElement triggerList) && triggerList.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement trigger in triggerList.EnumerateArray())
            {
                if (trigger.TryGetProperty("channel"u8, out JsonElement channel) && channel.GetString() is { Length: > 0 } c)
                {
                    trigger.TryGetProperty("payload"u8, out JsonElement payload);
                    triggers.Add(new SimulationTrigger(c, payload, trigger.TryGetProperty("correlationId"u8, out JsonElement corr) ? corr.GetString() : null));
                }
            }
        }

        bool autoAdvance = !(scenario.TryGetProperty("clock"u8, out JsonElement clock)
            && clock.TryGetProperty("autoAdvance"u8, out JsonElement auto)
            && auto.ValueKind == JsonValueKind.False);

        return new SimulationScenario { Inputs = inputs, Mocks = mocks, Triggers = triggers, AutoAdvanceClock = autoAdvance };
    }

    /// <summary>Judges the scenario's expectations against the trace.</summary>
    /// <param name="scenario">The scenario document (schema §4.2).</param>
    /// <param name="result">The simulation result to judge.</param>
    /// <returns>One verdict per expectation (an implicit completed-outcome expectation when the scenario declares none).</returns>
    public static List<Verdict> Evaluate(in JsonElement scenario, SimulationResult result)
    {
        var verdicts = new List<Verdict>();
        if (!scenario.TryGetProperty("expect"u8, out JsonElement expect) || expect.ValueKind != JsonValueKind.Object)
        {
            // No expectations: the scenario passes when the run completes.
            verdicts.Add(new("outcome", result.Outcome == SimulationOutcome.Completed, $"expected completed (implicit); was {OutcomeName(result.Outcome)}"));
            return verdicts;
        }

        if (expect.TryGetProperty("outcome"u8, out JsonElement outcome) && outcome.GetString() is { Length: > 0 } expectedOutcome)
        {
            string actual = OutcomeName(result.Outcome);
            verdicts.Add(new("outcome", actual == expectedOutcome, $"expected {expectedOutcome}; was {actual}"));
        }

        if (expect.TryGetProperty("path"u8, out JsonElement path) && path.ValueKind == JsonValueKind.Array)
        {
            bool exact = expect.TryGetProperty("pathMode"u8, out JsonElement mode) && mode.ValueEquals("exact");
            var visited = new List<string>();
            foreach (SimulatedStepRecord record in result.Steps)
            {
                visited.Add(record.StepId);
            }

            var expected = new List<string>();
            foreach (JsonElement step in path.EnumerateArray())
            {
                if (step.GetString() is { } s)
                {
                    expected.Add(s);
                }
            }

            bool passed;
            if (exact)
            {
                passed = expected.Count == visited.Count;
                for (int i = 0; passed && i < expected.Count; i++)
                {
                    passed = expected[i] == visited[i];
                }
            }
            else
            {
                int at = 0;
                foreach (string v in visited)
                {
                    if (at < expected.Count && v == expected[at])
                    {
                        at++;
                    }
                }

                passed = at == expected.Count;
            }

            verdicts.Add(new("path", passed, $"expected {(exact ? "exactly" : "subsequence")} [{string.Join(", ", expected)}]; visited [{string.Join(", ", visited)}]"));
        }

        if (expect.TryGetProperty("outputs"u8, out JsonElement outputs) && outputs.ValueKind == JsonValueKind.Array)
        {
            // The judged frame carries the workflow outputs AND every step's outputs (last attempt
            // wins), so conditions can assert intermediate results: $steps.<id>.outputs.<name>.
            var context = new WorkflowExecutionContext();
            if (result.Outputs.ValueKind is not JsonValueKind.Undefined)
            {
                context.SetWorkflowOutputs(result.Outputs);
            }

            foreach (SimulatedStepRecord record in result.Steps)
            {
                if (record.Outputs.ValueKind is not JsonValueKind.Undefined)
                {
                    context.SetStepOutputs(record.StepId, record.Outputs);
                }
            }

            foreach (JsonElement expectation in outputs.EnumerateArray())
            {
                if (expectation.TryGetProperty("condition"u8, out JsonElement condition) && condition.GetString() is { Length: > 0 } text)
                {
                    bool passed;
                    try
                    {
                        passed = CompiledCriterion.Compile(CriterionType.Simple, text).Evaluate(context);
                    }
                    catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException)
                    {
                        passed = false;
                    }

                    verdicts.Add(new("output", passed, text));
                }
            }
        }

        if (expect.TryGetProperty("steps"u8, out JsonElement steps) && steps.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty<JsonElement> expectation in steps.EnumerateObject())
            {
                using UnescapedUtf8JsonString stepName = expectation.Utf8NameSpan;
                string stepId = System.Text.Encoding.UTF8.GetString(stepName.Span);
                int attempts = 0;
                foreach (SimulatedStepRecord record in result.Steps)
                {
                    if (record.StepId == stepId)
                    {
                        attempts++;
                    }
                }

                if (expectation.Value.TryGetProperty("reached"u8, out JsonElement reached) && reached.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    bool expectReached = reached.ValueKind == JsonValueKind.True;
                    verdicts.Add(new("step", (attempts > 0) == expectReached, $"{stepId}: expected reached={expectReached}; executions={attempts}"));
                }

                if (expectation.Value.TryGetProperty("attempts"u8, out JsonElement expectedAttempts) && expectedAttempts.ValueKind == JsonValueKind.Number)
                {
                    int n = expectedAttempts.GetInt32();
                    verdicts.Add(new("step", attempts == n, $"{stepId}: expected {n} executions; was {attempts}"));
                }
            }
        }

        return verdicts;
    }

    /// <summary>Writes one scenario run result (a run-one response, a suite entry, and a CI report row share this shape).</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="name">The scenario name.</param>
    /// <param name="result">The simulation result.</param>
    /// <param name="verdicts">The judged expectations.</param>
    public static void WriteRunResult(Utf8JsonWriter writer, string name, SimulationResult result, List<Verdict> verdicts)
    {
        bool passed = true;
        foreach (Verdict verdict in verdicts)
        {
            passed &= verdict.Passed;
        }

        writer.WriteStartObject();
        writer.WriteString("scenario"u8, name);
        writer.WriteBoolean("passed"u8, passed);
        writer.WriteString("outcome"u8, OutcomeName(result.Outcome));
        writer.WriteStartArray("expectations"u8);
        foreach (Verdict verdict in verdicts)
        {
            writer.WriteStartObject();
            writer.WriteString("kind"u8, verdict.Kind);
            writer.WriteBoolean("passed"u8, verdict.Passed);
            if (verdict.Detail is not null)
            {
                writer.WriteString("detail"u8, verdict.Detail);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WritePropertyName("trace"u8);
        WriteTrace(writer, result);
        writer.WriteEndObject();
    }

    /// <summary>Writes one trace object (shared by the simulate response and each scenario run result).</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="r">The simulation result to project.</param>
    public static void WriteTrace(Utf8JsonWriter writer, in SimulationResult r)
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
            if (step.Skipped)
            {
                // §15 8b: a step-output override skipped the step — the record's outputs are the
                // PROVIDED value and no exchange exists (the shape the mock emits and the dock renders).
                writer.WriteBoolean("skipped"u8, true);
            }

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
                    if (!exchange.RequestBody.IsEmpty)
                    {
                        writer.WritePropertyName("requestBody"u8);
                        writer.WriteRawValue(exchange.RequestBody.Span, skipInputValidation: false);
                    }

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

    /// <summary>The contract's outcome vocabulary for a simulation outcome.</summary>
    /// <param name="outcome">The simulation outcome.</param>
    /// <returns>The contract outcome string.</returns>
    public static string OutcomeName(SimulationOutcome outcome) => outcome switch
    {
        SimulationOutcome.Completed => "completed",
        SimulationOutcome.Faulted => "faulted",
        SimulationOutcome.Suspended => "suspended",
        SimulationOutcome.Paused => "paused",
        _ => "budgetExhausted",
    };
}