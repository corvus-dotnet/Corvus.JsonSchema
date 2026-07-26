// <copyright file="WorkflowAotBuildIntegrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Aot.Tests;

/// <summary>
/// The opt-in container integration proof (ADR 0055): it builds a real durable executor (a workflow with an OpenAPI
/// call), assembles the host-app around its signed IL, and native-AOT compiles it in the build container, proving the
/// whole builder path end to end and that a real generated executor is AOT-clean under whole-program ILC. It is
/// <c>[TestCategory("integration")]</c> and skips unless the feed and runtime version are configured, since it needs
/// podman and the <c>arazzo-aot-builder</c> image. It produces no committed artifact; the native binary is built and
/// discarded in the test.
/// </summary>
[TestClass]
public sealed class WorkflowAotBuildIntegrationTests
{
    // A real durable workflow: one step calling an OpenAPI operation, so the generated executor carries genuine
    // workflow, transport-binding, and JSON logic rather than a trivial stub.
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
    [TestCategory("integration")]
    public async Task Builds_a_real_workflow_executor_to_a_native_binary_in_the_container()
    {
        string? feed = Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED (the local package feed path) and ARAZZO_AOT_RUNTIME_VERSION (its Corvus runtime version), and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string image = Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";

        // 1. Build a real durable executor IL (as catalog-add would). Capture the provider's progress so a build failure
        // surfaces its reason rather than a bare null.
        var buildLog = new List<string>();
        WorkflowExecutorArtifact? executor = new WorkflowExecutorProvider(durable: true, progress: buildLog.Add).BuildExecutor(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("petstore", Encoding.UTF8.GetBytes(PetstoreOpenApi))],
            "integration-hash");
        executor.ShouldNotBeNull($"the executor did not build. Provider progress:\n{string.Join("\n", buildLog)}");

        // 2. Assemble the host-app around the signed executor IL for the Linux target.
        AssembledHostApp hostApp = new AotHostAppAssembler().Assemble(
            executor!.Value.Assembly,
            executor.Value.Manifest,
            "linux-x64",
            new AotHostAppOptions
            {
                RuntimePackageVersion = runtimeVersion,
                FeedSources =
                [
                    ("local", "/work/local-packages"),
                    ("nuget.org", "https://api.nuget.org/v3/index.json"),
                    ("dotnet-eng", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                    ("dotnet-libraries", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"),
                ],
            });

        // 3. Native-AOT compile it in the container, mounting the host feed at the path the feed config names.
        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
        {
            ContainerImage = image,
            ReadOnlyMounts = [(feed, "/work/local-packages")],
        });

        AotBuildResult result = await builder.BuildAsync(hostApp, CancellationToken.None);

        // 4. The output is a real, non-trivial ELF executable.
        result.Succeeded.ShouldBeTrue($"AOT build failed. Log:\n{result.Log}");
        result.NativeBinary.Length.ShouldBeGreaterThan(1_000_000);
        result.NativeBinary.Span[..4].ToArray().ShouldBe([0x7F, (byte)'E', (byte)'L', (byte)'F']);
    }
}