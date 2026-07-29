// <copyright file="ServerlessLiveExecutionAzureFunctionsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The Azure Functions counterpart to <see cref="ServerlessLiveExecutionLocalStackTests"/>: the automated proof that a
/// version's real ReadyToRun isolated-worker app <b>executes under a live Azure Functions host</b> (ADR 0055/0061). It
/// compiles the shared workflow's executor into a framework-dependent ReadyToRun isolated-worker app in the build
/// container, drops the published app into the real <c>dotnet-isolated</c> Functions runtime image (there is no Azure
/// management-plane emulator, so the gate proves <em>execution</em>, not the deploy, ADR 0061), and dispatches a seeded
/// run to it. The run advances to <c>Completed</c> by loading its checkpoint over the test's checkpoint surface, calling
/// its <c>echo</c> source, and saving the advanced checkpoint back — the same run-to-completion the Lambda gate proves,
/// on the other vendor's real runtime. The deploy path this app takes to reach a real Function App is proven by
/// <see cref="AzureFunctionsRunFromPackageDeployTests"/>.
/// </summary>
/// <remarks>
/// This is opt-in (<c>[TestCategory("integration")][TestCategory("docker")]</c>): it needs a container runtime, the
/// <c>arazzo-aot-builder</c> image, the local package feed the runtime graph restores from, and the Azure Functions
/// <c>dotnet-isolated</c> image. It skips unless <c>ARAZZO_AOT_LOCAL_FEED</c> and <c>ARAZZO_AOT_RUNTIME_VERSION</c> are
/// set. The isolated worker reaches the test's Kestrel checkpoint host (and its <c>echo</c> source) through
/// <c>host.containers.internal</c>, routed by the <c>--add-host …:host-gateway</c> the container is given.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class ServerlessLiveExecutionAzureFunctionsTests
{
    [TestMethod]
    public async Task The_published_isolated_worker_runs_a_seeded_run_to_completion_over_the_checkpoint_surface()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED (the local package feed path) and ARAZZO_AOT_RUNTIME_VERSION (its Corvus runtime version), and build the arazzo-aot-builder image, to run this proof.");
            return;
        }

        string builderImage = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";

        // Compile the shared workflow's real executor to a framework-dependent ReadyToRun isolated-worker app in the build
        // container (the same build catalog-add + the build worker drive), unpack the published app, and run it under the
        // real Functions host to completion.
        byte[] appZip = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AzureFunctions, feed, runtimeVersion, builderImage);
        string appDirectory = Directory.CreateTempSubdirectory("arazzo-azure-app-").FullName;
        try
        {
            using (var archive = new ZipArchive(new MemoryStream(appZip), ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(appDirectory);
            }

            await ServerlessLiveExecutionSupport.RunSeededRunToCompletionUnderFunctionsHostAsync(appDirectory, ServerlessLiveExecutionSupport.FunctionsImage());
        }
        finally
        {
            TryDeleteDirectory(appDirectory);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A best-effort cleanup of the unpacked app; a leftover temp directory is not worth failing the test.
        }
    }
}