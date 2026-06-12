// <copyright file="WorkflowExecutorLoaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Execution;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the <see cref="WorkflowExecutorLoader"/> verifies, loads, caches, runs, and unloads a real compiled
/// executor produced by <see cref="WorkflowExecutorProvider"/> — and refuses a tampered or mismatched one.
/// </summary>
[TestClass]
public class WorkflowExecutorLoaderTests
{
    private const string WorkflowJson = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "Adopt", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./petstore.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "adopt-v1",
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
    public async Task Loads_verifies_caches_and_runs_a_real_executor()
    {
        const string hash = "hash-abc";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        LoadedWorkflow loaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);

        loaded.Manifest.WorkflowId.ShouldBe("adopt-v1");
        loaded.Workflow.Descriptor.WorkflowId.ShouldBe("adopt-v1");

        // A second Load returns the cached instance, and TryGet sees it.
        loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash).ShouldBeSameAs(loaded);
        loader.TryGet("adopt", 1, out LoadedWorkflow? got).ShouldBeTrue();
        got.ShouldBeSameAs(loaded);

        // The loaded workflow runs durably through the contract.
        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        var run = new FakeWorkflowRun();

        WorkflowRunResultKind kind = await loaded.Workflow.RunAsync(loaded.Workflow.Descriptor.Sources.ToDictionary(s => s, _ => (IApiTransport)transport, System.StringComparer.Ordinal), null, workspace, inputs.RootElement, run, default);

        kind.ShouldBe(WorkflowRunResultKind.Completed);
        run.Completed.ShouldBeTrue();
        transport.Requests[0].Path.ShouldBe("/pets/42");
    }

    [TestMethod]
    public void Rejects_a_package_hash_mismatch()
    {
        WorkflowExecutorArtifact artifact = BuildArtifact("the-real-hash");
        using var loader = new WorkflowExecutorLoader();

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, "a-different-hash"));
        ex.Message.ShouldContain("package hash");
        loader.TryGet("adopt", 1, out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Rejects_a_tampered_assembly()
    {
        const string hash = "hash-xyz";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        byte[] tampered = artifact.Assembly.ToArray();
        tampered[^1] ^= 0xFF;

        using var loader = new WorkflowExecutorLoader();

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, tampered, artifact.Manifest, hash));
        ex.Message.ShouldContain("digest");
    }

    [TestMethod]
    public void Rejects_an_unsupported_target_framework()
    {
        const string hash = "hash-tfm";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader(supportedTargetFramework: "net9.0");

        WorkflowExecutorLoadException ex = Should.Throw<WorkflowExecutorLoadException>(
            () => loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash));
        ex.Message.ShouldContain("net9.0");
    }

    [TestMethod]
    public void Unload_evicts_the_version_and_it_can_be_reloaded()
    {
        const string hash = "hash-unload";
        WorkflowExecutorArtifact artifact = BuildArtifact(hash);
        using var loader = new WorkflowExecutorLoader();

        loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);
        loader.Unload("adopt", 1).ShouldBeTrue();
        loader.TryGet("adopt", 1, out _).ShouldBeFalse();
        loader.Unload("adopt", 1).ShouldBeFalse();

        // Reloading after unload works.
        LoadedWorkflow reloaded = loader.Load("adopt", 1, artifact.Assembly, artifact.Manifest, hash);
        reloaded.Workflow.Descriptor.WorkflowId.ShouldBe("adopt-v1");
    }

    private static WorkflowExecutorArtifact BuildArtifact(string packageHash)
    {
        var log = new List<string>();
        var provider = new WorkflowExecutorProvider(durable: true, log.Add);
        var sources = new List<KeyValuePair<string, byte[]>>
        {
            new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi)),
        };

        WorkflowExecutorArtifact? artifact = provider.BuildExecutor(Encoding.UTF8.GetBytes(WorkflowJson), sources, packageHash);
        artifact.ShouldNotBeNull($"build failed. Progress:\n{string.Join("\n", log)}");
        return artifact!.Value;
    }
}
