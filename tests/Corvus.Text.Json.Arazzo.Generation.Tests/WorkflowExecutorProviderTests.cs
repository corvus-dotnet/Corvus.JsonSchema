// <copyright file="WorkflowExecutorProviderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Generation;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Generation.Tests;

/// <summary>
/// Proves the <see cref="WorkflowExecutorProvider"/> turns a workflow package (the Arazzo document + its
/// source documents) into a real, loadable, runnable assembly: generate → compile-to-bytes → load → run
/// against a <see cref="MockApiTransport"/>, plus the manifest binding and the not-runnable fallbacks.
/// </summary>
[TestClass]
public class WorkflowExecutorProviderTests
{
    // A workflow with no declared `inputs` schema, so the generated executor's inputs parameter is a raw
    // JsonElement — reflection-invokable without constructing a generated inputs model.
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
                "responses": {
                  "200": {
                    "description": "ok",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "name": { "type": "string" } } } } }
                  }
                }
              }
            }
          }
        }
        """;

    [TestMethod]
    public async Task Straight_line_executor_compiles_loads_and_runs_against_a_mock_transport()
    {
        WorkflowExecutorArtifact artifact = BuildOrFail(durable: false);

        string entryType = ReadManifestString(artifact, "entryType");
        entryType.ShouldBe("Corvus.Workflows.Generated.Workflows.AdoptV1Workflow");

        var loadContext = new AssemblyLoadContext("executor-test", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(artifact.Assembly.ToArray());
            Assembly assembly = loadContext.LoadFromStream(stream);

            Type workflow = assembly.GetType(entryType)
                ?? throw new InvalidOperationException($"Generated executor type '{entryType}' not found.");
            MethodInfo execute = workflow.GetMethod("ExecuteAsync")
                ?? throw new InvalidOperationException("ExecuteAsync not found.");

            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

            using JsonWorkspace workspace = JsonWorkspace.Create();
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var pending = (ValueTask<JsonElement>)execute.Invoke(
                null,
                [transport, workspace, inputs.RootElement, default(CancellationToken), null])!;
            JsonElement outputs = await pending;

            outputs.TryGetProperty("name"u8, out JsonElement name).ShouldBeTrue();
            name.GetString().ShouldBe("Fido");

            transport.Requests.Count.ShouldBe(1);
            transport.Requests[0].Path.ShouldBe("/pets/42");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [TestMethod]
    public async Task Durable_default_produces_a_loadable_host_adapter_bound_to_the_package_by_a_valid_manifest()
    {
        const string packageHash = "abc123def456";
        WorkflowExecutorArtifact artifact = BuildOrFail(packageHash);

        using ParsedJsonDocument<JsonElement> manifest = ParsedJsonDocument<JsonElement>.Parse(artifact.Manifest);
        JsonElement root = manifest.RootElement;

        root.GetProperty("formatVersion"u8).GetInt32().ShouldBe(WorkflowExecutorProvider.ManifestFormatVersion);
        root.GetProperty("targetFramework"u8).GetString().ShouldBe("net10.0");
        root.GetProperty("packageHash"u8).GetString().ShouldBe(packageHash);
        root.GetProperty("workflowId"u8).GetString().ShouldBe("adopt-v1");
        root.GetProperty("durable"u8).GetBoolean().ShouldBeTrue();
        root.GetProperty("rootNamespace"u8).GetString().ShouldBe("Corvus.Workflows.Generated");

        // The assembly digest in the manifest binds the bytes to the version.
        string expectedDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(artifact.Assembly.Span));
        root.GetProperty("assemblyDigest"u8).GetString().ShouldBe(expectedDigest);

        // The declared sources are recorded.
        JsonElement sources = root.GetProperty("sources"u8);
        sources.GetArrayLength().ShouldBe(1);
        sources[0].GetProperty("name"u8).GetString().ShouldBe("petstore");
        sources[0].GetProperty("type"u8).GetString().ShouldBe("openapi");

        // The recorded entry type resolves to the generated host adapter, which is loaded, activated through
        // the IHostedWorkflow contract, and run durably — proving the whole hosted-workflow path and guarding
        // against the provider's entry-type derivation drifting from the code generator's.
        string entryType = root.GetProperty("entryType"u8).GetString()!;
        entryType.ShouldBe("Corvus.Workflows.Generated.Workflows.AdoptV1WorkflowHost");

        var loadContext = new AssemblyLoadContext("executor-manifest-test", isCollectible: true);
        try
        {
            using var stream = new MemoryStream(artifact.Assembly.ToArray());
            Assembly assembly = loadContext.LoadFromStream(stream);
            Type? hostType = assembly.GetType(entryType);
            hostType.ShouldNotBeNull();
            typeof(IHostedWorkflow).IsAssignableFrom(hostType).ShouldBeTrue();

            var hosted = (IHostedWorkflow)Activator.CreateInstance(hostType!)!;
            hosted.Descriptor.WorkflowId.ShouldBe("adopt-v1");
            hosted.Descriptor.NeedsMessageTransport.ShouldBeFalse();
            hosted.Descriptor.Sources.ShouldContain("petstore");

            var transport = new MockApiTransport();
            transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");
            using JsonWorkspace workspace = JsonWorkspace.Create();
            using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));

            var run = new FakeWorkflowRun();
            WorkflowRunResultKind kind = await hosted.RunAsync(transport, null, workspace, inputs.RootElement, run, default);

            kind.ShouldBe(WorkflowRunResultKind.Completed);
            run.Completed.ShouldBeTrue();
            transport.Requests[0].Path.ShouldBe("/pets/42");
        }
        finally
        {
            loadContext.Unload();
        }
    }

    [TestMethod]
    public void Returns_null_when_a_declared_source_is_absent_from_the_package()
    {
        WorkflowExecutorArtifact? artifact = new WorkflowExecutorProvider().BuildExecutor(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [],
            "hash");

        artifact.ShouldBeNull();
    }

    [TestMethod]
    public void Returns_null_for_a_cross_document_arazzo_source()
    {
        const string crossDoc = """
            {
              "arazzo": "1.0.1",
              "info": { "title": "x", "version": "1.0.0" },
              "sourceDescriptions": [ { "name": "other", "url": "./other.arazzo.json", "type": "arazzo" } ],
              "workflows": [ { "workflowId": "x-v1", "steps": [] } ]
            }
            """;

        WorkflowExecutorArtifact? artifact = new WorkflowExecutorProvider().BuildExecutor(
            Encoding.UTF8.GetBytes(crossDoc),
            [],
            "hash");

        artifact.ShouldBeNull();
    }

    [TestMethod]
    public void Returns_null_when_the_workflow_has_no_workflow_id()
    {
        const string noId = """
            { "arazzo": "1.0.1", "info": { "title": "x", "version": "1.0.0" }, "workflows": [] }
            """;

        WorkflowExecutorArtifact? artifact = new WorkflowExecutorProvider().BuildExecutor(
            Encoding.UTF8.GetBytes(noId),
            [],
            "hash");

        artifact.ShouldBeNull();
    }

    private static WorkflowExecutorArtifact BuildOrFail(string packageHash = "testhash", bool durable = true)
    {
        var log = new List<string>();
        var provider = new WorkflowExecutorProvider(durable, log.Add);
        var sources = new List<KeyValuePair<string, byte[]>>
        {
            new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi)),
        };

        WorkflowExecutorArtifact? artifact = provider.BuildExecutor(
            Encoding.UTF8.GetBytes(WorkflowJson), sources, packageHash);

        artifact.ShouldNotBeNull($"build failed. Progress:\n{string.Join("\n", log)}");
        return artifact!.Value;
    }

    private static string ReadManifestString(WorkflowExecutorArtifact artifact, string property)
    {
        using ParsedJsonDocument<JsonElement> manifest = ParsedJsonDocument<JsonElement>.Parse(artifact.Manifest);
        return manifest.RootElement.GetProperty(Encoding.UTF8.GetBytes(property)).GetString()!;
    }
}
