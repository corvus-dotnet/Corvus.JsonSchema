// <copyright file="ServerlessLiveExecutionSupport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Generation;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The shared fixture for the serverless live-execution gates (ADR 0055/0060/0061): the one source-calling workflow both
/// the AWS Lambda and Azure Functions proofs compile, the deploy-artifact build that assembles and container-compiles it
/// for a target, and a free-port helper for the checkpoint host. Holding the workflow as a single constant is what keeps
/// the two gates proving the <em>identical</em> workflow, so they cannot silently drift apart.
/// </summary>
internal static class ServerlessLiveExecutionSupport
{
    // A minimal source-calling workflow: one GET on the "echo" source. The same shape the demo deploys, so the compiled
    // artifact carries genuine workflow, transport-binding, and JSON logic rather than a trivial stub.
    internal const string WorkflowJson = """
        {
          "arazzo": "1.1.0",
          "info": { "title": "Serverless Check", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "echo", "url": "./echo.openapi.json", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "serverless-check",
              "steps": [
                {
                  "stepId": "callEcho",
                  "operationId": "echo",
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "outputs": { "status": "$response.body#/status" }
                }
              ],
              "outputs": { "status": "$steps.callEcho.outputs.status" }
            }
          ]
        }
        """;

    internal const string EchoOpenApi = """
        {
          "openapi": "3.1.0",
          "info": { "title": "Echo API", "version": "1.0.0" },
          "paths": {
            "/demo/echo": {
              "get": {
                "operationId": "echo",
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "status": { "type": "string" } } } } }
                  }
                }
              }
            }
          }
        }
        """;

    // A free ephemeral TCP port for the Kestrel checkpoint host. Bind-to-0 then release; the small reuse window is
    // acceptable for an opt-in integration test that immediately binds the port on 0.0.0.0.
    internal static int FreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>
    /// Compiles the shared workflow's real executor and assembles then container-builds the deploy artifact for a target:
    /// the self-contained native <c>bootstrap</c> for AWS Lambda, or the zipped framework-dependent ReadyToRun
    /// isolated-worker app for Azure Functions. This is the exact build path catalog-add and the build worker drive.
    /// </summary>
    /// <param name="target">The serverless platform to assemble and compile for.</param>
    /// <param name="feed">The host path of the local package feed the runtime graph restores from.</param>
    /// <param name="runtimeVersion">The Corvus runtime package version that matches the executor's engine version.</param>
    /// <param name="image">The build container image tag.</param>
    /// <returns>The deploy artifact bytes: a native ELF for Lambda, or the zipped published app for Azure.</returns>
    internal static async Task<byte[]> BuildDeployArtifactAsync(ServerlessTarget target, string feed, string runtimeVersion, string image)
    {
        var buildLog = new List<string>();
        WorkflowExecutorArtifact? executor = new WorkflowExecutorProvider(durable: true, progress: buildLog.Add).BuildExecutor(
            Encoding.UTF8.GetBytes(WorkflowJson),
            [new("echo", Encoding.UTF8.GetBytes(EchoOpenApi))],
            "live-execution-hash");
        executor.ShouldNotBeNull($"the executor did not build. Provider progress:\n{string.Join("\n", buildLog)}");

        AssembledHostApp hostApp = new AotHostAppAssembler().Assemble(
            executor!.Value.Assembly,
            executor.Value.Manifest,
            "linux-x64",
            new AotHostAppOptions
            {
                Target = target,
                RuntimePackageVersion = runtimeVersion,
                FeedSources =
                [
                    ("local", "/work/local-packages"),
                    ("nuget.org", "https://api.nuget.org/v3/index.json"),
                    ("dotnet-eng", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-eng/nuget/v3/index.json"),
                    ("dotnet-libraries", "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-libraries/nuget/v3/index.json"),
                ],
            });

        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
        {
            ContainerImage = image,
            ReadOnlyMounts = [(feed, "/work/local-packages")],
        });

        AotBuildResult result = await builder.BuildAsync(hostApp, default);
        result.Succeeded.ShouldBeTrue($"the {target} deploy-artifact build failed. Log:\n{result.Log}");
        return result.NativeBinary.ToArray();
    }
}