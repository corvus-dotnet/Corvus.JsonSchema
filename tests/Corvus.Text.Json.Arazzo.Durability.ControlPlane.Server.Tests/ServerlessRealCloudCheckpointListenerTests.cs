// <copyright file="ServerlessRealCloudCheckpointListenerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.IO.Compression;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.AzureStorage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// The real-cloud proof of the authenticated checkpoint round-trip (ADR 0062): the real Azure Functions runtime advances a
/// seeded run to completion through a <b>publicly deployed, token-authenticated checkpoint listener</b> and its backing
/// Azure Storage store. It compiles the shared <c>serverless-check</c> workflow's real ReadyToRun isolated-worker app,
/// runs it under the <c>dotnet-isolated</c> Functions image, seeds a Pending run into the listener's shared store, and
/// dispatches the run with a run-scoped bearer token the runner mints. The worker loads its checkpoint from the listener
/// over public HTTPS (presenting the token), calls its <c>echo</c> source (also served by the listener), and saves the
/// advanced checkpoint back; the listener validates the token against the run in the URL and terminates the save into the
/// shared store, where the assertions read the run back as <c>Completed</c>.
/// </summary>
/// <remarks>
/// Opt-in (<c>[TestCategory("integration")][TestCategory("docker")][TestCategory("azure")]</c>). It needs a container
/// runtime, the <c>arazzo-aot-builder</c> image, the local package feed, the Azure Functions <c>dotnet-isolated</c> image,
/// and a <b>pre-deployed listener</b>: it skips unless <c>ARAZZO_AOT_LOCAL_FEED</c>, <c>ARAZZO_AOT_RUNTIME_VERSION</c>,
/// <c>ARAZZO_CHECKPOINT_LISTENER_URL</c> (the deployed listener's public base URL), <c>ARAZZO_CHECKPOINT_SECRET</c> (the
/// base64 shared checkpoint secret), and <c>ARAZZO_CHECKPOINT_STORAGE</c> (the listener's shared Azure Storage connection
/// string) are all set. No endpoint, secret, or storage identifier is baked into the source; they arrive only through
/// these environment variables. The function process runs locally so this gates the cloud checkpoint round-trip without a
/// Function App provision; <see cref="ArmFunctionAppLiveDeployTests"/> proves the same run with the function on real Flex.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
[TestCategory("azure")]
public sealed class ServerlessRealCloudCheckpointListenerTests
{
    [TestMethod]
    public async Task The_real_functions_runtime_runs_a_seeded_run_to_completion_through_the_public_token_authenticated_listener()
    {
        string? feed = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_LOCAL_FEED");
        string? runtimeVersion = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_RUNTIME_VERSION");
        string? listenerUrl = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_LISTENER_URL");
        string? secretBase64 = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_SECRET");
        string? storageConnection = System.Environment.GetEnvironmentVariable("ARAZZO_CHECKPOINT_STORAGE");
        if (string.IsNullOrEmpty(feed) || string.IsNullOrEmpty(runtimeVersion) || string.IsNullOrEmpty(listenerUrl)
            || string.IsNullOrEmpty(secretBase64) || string.IsNullOrEmpty(storageConnection))
        {
            Assert.Inconclusive("Set ARAZZO_AOT_LOCAL_FEED, ARAZZO_AOT_RUNTIME_VERSION, ARAZZO_CHECKPOINT_LISTENER_URL, ARAZZO_CHECKPOINT_SECRET, and ARAZZO_CHECKPOINT_STORAGE (a deployed listener + its shared store) to run this real-cloud proof.");
            return;
        }

        string builderImage = System.Environment.GetEnvironmentVariable("ARAZZO_AOT_IMAGE") ?? "arazzo-aot-builder:net10";
        byte[] checkpointSecret = Convert.FromBase64String(secretBase64);

        // Build the real app package, then run the real Functions runtime over it against the deployed listener + store.
        byte[] appZip = await ServerlessLiveExecutionSupport.BuildDeployArtifactAsync(ServerlessTarget.AzureFunctions, feed, runtimeVersion, builderImage);
        string appDirectory = Directory.CreateTempSubdirectory("arazzo-azure-listener-app-").FullName;

        try
        {
            using (var archive = new ZipArchive(new MemoryStream(appZip), ZipArchiveMode.Read))
            {
                archive.ExtractToDirectory(appDirectory);
            }

            // The store is shared with the deployed listener: seeding here and the listener's checkpoint saves reach the
            // same Azure Storage account, so the checkpoint the worker writes back through the listener lands where these
            // assertions read it. PrepareAsync is idempotent (the listener already provisioned the tables and containers on
            // start-up). The store holds no unmanaged resource, so it needs no disposal.
            await AzureStorageWorkflowStateStore.PrepareAsync(storageConnection);
            AzureStorageWorkflowStateStore store = await AzureStorageWorkflowStateStore.ConnectAsync(storageConnection);

            await ServerlessLiveExecutionSupport.RunSeededRunToCompletionAgainstListenerUnderLocalFunctionsHostAsync(
                appDirectory, ServerlessLiveExecutionSupport.FunctionsImage(), store, listenerUrl, checkpointSecret);
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