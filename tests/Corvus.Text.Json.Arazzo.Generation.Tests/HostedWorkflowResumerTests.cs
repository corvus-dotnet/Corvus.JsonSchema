// <copyright file="HostedWorkflowResumerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the <see cref="HostedWorkflowResumer"/> ties the pieces together: it resolves a run's workflow id to
/// a runnable catalog version, loads + verifies its compiled executor through the loader, and drives the run to
/// completion as the <see cref="WorkflowResumer"/> the durable worker expects — all against a real
/// provider-built, catalogued executor.
/// </summary>
[TestClass]
public class HostedWorkflowResumerTests
{
    // Base workflow id "adopt" — the catalog rewrites it to "adopt-v1" and bakes the executor.
    private const string WorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "petName": "$response.body#/name" }
                }
              ],
              "outputs": { "name": "$steps.getPet.outputs.petName" }
            }
          ]
        }
        """;

    private const string PetstoreOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Pets", "version": "1.0.0" },
          "paths": {
            "/pets/{petId}": {
              "get": {
                "operationId": "getPet",
                "parameters": [ { "name": "petId", "in": "path", "required": true, "schema": { "type": "string" } } ],
                "responses": { "200": { "description": "ok", "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } } } }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Resolves_a_runnable_catalog_version_loads_it_and_runs_the_run_to_completion()
    {
        var catalog = new InMemoryWorkflowCatalogStore(executorProvider: new WorkflowExecutorProvider());
        CatalogVersion version = await catalog.AddAsync("adopt", Package(), Meta(), default);
        version.Ref.WorkflowId.ShouldBe("adopt-v1");
        ((bool)version.Runnable).ShouldBeTrue();

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", version.Ref.WorkflowId, inputs.RootElement);

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null));

        // Drive it through the WorkflowResumer delegate the durable worker would call.
        WorkflowResumer resume = resumer.AsResumer();
        WorkflowRunResultKind kind = await resume(run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);
        transport.Requests[0].Path.ShouldBe("/pets/42");

        // The version is now cached in the loader for subsequent runs.
        loader.TryGet("adopt", 1, out _).ShouldBeTrue();
    }

    [TestMethod]
    public async Task Throws_when_the_version_is_not_runnable()
    {
        // No executor provider → the catalogued version carries no executor.
        var catalog = new InMemoryWorkflowCatalogStore();
        await catalog.AddAsync("adopt", Package(), Meta(), default);

        var runStore = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using WorkflowRun run = WorkflowRun.CreateNew(runStore, "run-1", "adopt-v1", inputs.RootElement);

        using var loader = new WorkflowExecutorLoader();
        var resumer = new HostedWorkflowResumer(catalog, loader, (d, _tags) => new WorkflowTransports(d.Sources.ToDictionary(s => s, _ => (IApiTransport)new MockApiTransport(), System.StringComparer.Ordinal), null));

        await Should.ThrowAsync<InvalidOperationException>(async () => await resumer.ResumeAsync(run, default));
    }

    private static CatalogMetadata Meta() => new(new CatalogOwner("Team", "team@example.com"), "alice");

    private static byte[] Package()
        => WorkflowPackage.Pack(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))]);
}
