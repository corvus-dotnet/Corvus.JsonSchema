// <copyright file="WorkspaceScenarioJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Testing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The scenarios seam (workflow-designer design §4.2): the working copy's scenario set edits as an
/// etag-guarded read-modify-write (like attachments), and a run resolves each mock's
/// (source, operationId) to its route through the attached surfaces, executes the deterministic
/// simulator, and judges the scenario's expectations against the trace.
/// </summary>
internal static class WorkspaceScenarioJson
{
    /// <summary>One expectation's verdict.</summary>
    public readonly record struct Verdict(string Kind, bool Passed, string? Detail);

    /// <summary>Writes the list response.</summary>
    public static ParsedJsonDocument<Models.GetWorkspaceWorkflowsByIdScenariosOk> ListResponse(in JsonElement scenarios)
    {
        return PersistedJson.ToPooledDocument<Models.GetWorkspaceWorkflowsByIdScenariosOk, JsonElement>(
            scenarios,
            static (Utf8JsonWriter writer, in JsonElement s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("scenarios"u8);
                if (s.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement scenario in s.EnumerateArray())
                    {
                        scenario.WriteTo(writer);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    /// <summary>Finds a scenario by name (an undefined element when absent).</summary>
    public static JsonElement FindScenario(in JsonElement scenarios, string name)
    {
        if (scenarios.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement scenario in scenarios.EnumerateArray())
            {
                if (scenario.TryGetProperty("name"u8, out JsonElement n) && n.ValueEquals(name))
                {
                    return scenario;
                }
            }
        }

        return default;
    }

    /// <summary>A draft replacing (or appending) the named scenario — the whole-set RMW write.</summary>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> DraftUpserting(in JsonElement currentScenarios, in JsonElement scenario, string name)
    {
        return PersistedJson.ToPooledDocument<WorkspaceWorkflows.WorkspaceWorkflow, (JsonElement Current, JsonElement Scenario, string Name)>(
            (currentScenarios, scenario, name),
            static (Utf8JsonWriter writer, in (JsonElement Current, JsonElement Scenario, string Name) s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("scenarios"u8);
                if (s.Current.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in s.Current.EnumerateArray())
                    {
                        if (!(entry.TryGetProperty("name"u8, out JsonElement n) && n.ValueEquals(s.Name)))
                        {
                            entry.WriteTo(writer);
                        }
                    }
                }

                s.Scenario.WriteTo(writer);
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    /// <summary>A draft with the named scenario removed (the caller checks presence first).</summary>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> DraftRemoving(in JsonElement currentScenarios, string name)
    {
        return PersistedJson.ToPooledDocument<WorkspaceWorkflows.WorkspaceWorkflow, (JsonElement Current, string Name)>(
            (currentScenarios, name),
            static (Utf8JsonWriter writer, in (JsonElement Current, string Name) s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("scenarios"u8);
                if (s.Current.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in s.Current.EnumerateArray())
                    {
                        if (!(entry.TryGetProperty("name"u8, out JsonElement n) && n.ValueEquals(s.Name)))
                        {
                            entry.WriteTo(writer);
                        }
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    /// <summary>Writes the put response: the stored scenario + the working copy's fresh etag.</summary>
    public static ParsedJsonDocument<Models.PutWorkspaceWorkflowsByIdScenariosByScenarioNameOk> PutResponse(in JsonElement scenario, string etag)
    {
        return PersistedJson.ToPooledDocument<Models.PutWorkspaceWorkflowsByIdScenariosByScenarioNameOk, (JsonElement Scenario, string Etag)>(
            (scenario, etag),
            static (Utf8JsonWriter writer, in (JsonElement Scenario, string Etag) s) =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("scenario"u8);
                s.Scenario.WriteTo(writer);
                writer.WriteString("etag"u8, s.Etag);
                writer.WriteEndObject();
            });
    }

    /// <summary>
    /// Resolves every attached source's OpenAPI operations to (source, operationId) → (method, path
    /// template) so a scenario mock addresses operations the way the document does.
    /// </summary>
    public static async ValueTask<Dictionary<(string Source, string OperationId), (string Method, string Path)>> ResolveRoutesAsync(
        JsonElement attachments, ISourceStore? registry, AccessContext reach, CancellationToken cancellationToken)
    {
        var routes = new Dictionary<(string, string), (string, string)>();
        if (attachments.ValueKind != JsonValueKind.Array)
        {
            return routes;
        }

        foreach (JsonElement attachment in attachments.EnumerateArray())
        {
            if (!attachment.TryGetProperty("name"u8, out JsonElement nameElement) || nameElement.GetString() is not { Length: > 0 } name)
            {
                continue;
            }

            if (attachment.TryGetProperty("document"u8, out JsonElement inline) && inline.ValueKind == JsonValueKind.Object)
            {
                CollectRoutes(name, inline, routes);
            }
            else if (registry is not null
                && attachment.TryGetProperty("sourceName"u8, out JsonElement sn)
                && sn.GetString() is { Length: > 0 } registryName)
            {
                using ParsedJsonDocument<RegisteredSource>? registered = await registry.GetAsync(registryName, reach, cancellationToken).ConfigureAwait(false);
                if (registered is { } r)
                {
                    CollectRoutes(name, (JsonElement)r.RootElement.Document, routes);
                }
            }
        }

        return routes;
    }

    /// <summary>Builds the simulator's scenario from the stored scenario document.</summary>
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
            var context = new WorkflowExecutionContext();
            if (result.Outputs.ValueKind is not JsonValueKind.Undefined)
            {
                context.SetWorkflowOutputs(result.Outputs);
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

    /// <summary>Writes one scenario run result (used for run-one and each suite entry).</summary>
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
        WorkspaceSimulationJson.WriteTrace(writer, result);
        writer.WriteEndObject();
    }

    /// <summary>The contract's outcome vocabulary for a simulation outcome.</summary>
    public static string OutcomeName(SimulationOutcome outcome) => outcome switch
    {
        SimulationOutcome.Completed => "completed",
        SimulationOutcome.Faulted => "faulted",
        SimulationOutcome.Suspended => "suspended",
        SimulationOutcome.Paused => "paused",
        _ => "budgetExhausted",
    };

    private static void CollectRoutes(string source, in JsonElement sourceDocument, Dictionary<(string, string), (string, string)> routes)
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
}