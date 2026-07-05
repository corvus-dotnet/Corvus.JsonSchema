// <copyright file="WorkspaceScenarioJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Testing;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// The scenarios seam (workflow-designer design §4.2): the working copy's scenario set edits as an
/// etag-guarded read-modify-write (like attachments), and the routes a run resolves mocks through
/// come from the attached surfaces (inline documents, or the registry re-resolved at read time).
/// The headless engine itself — build/judge/report — is <see cref="ScenarioSuite"/> in the Testing
/// assembly, shared with the <c>scenarios run</c> CLI.
/// </summary>
internal static class WorkspaceScenarioJson
{
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
                ScenarioSuite.CollectRoutes(name, inline, routes);
            }
            else if (registry is not null
                && attachment.TryGetProperty("sourceName"u8, out JsonElement sn)
                && sn.GetString() is { Length: > 0 } registryName)
            {
                using ParsedJsonDocument<RegisteredSource>? registered = await registry.GetAsync(registryName, reach, cancellationToken).ConfigureAwait(false);
                if (registered is { } r)
                {
                    ScenarioSuite.CollectRoutes(name, (JsonElement)r.RootElement.Document, routes);
                }
            }
        }

        return routes;
    }
}