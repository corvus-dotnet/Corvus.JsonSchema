// <copyright file="DeployedFunctionUrlResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Proves <see cref="DeployedFunctionUrlResolver"/> maps a run to its deployed serverless function's invoke URL from the
/// WorkflowDeployment store (ADR 0059): it parses the run's versioned workflow id, reads the environment's current runtime
/// target, and looks up the Deployed deployment for the full tuple (base, version, environment, rid) — so it selects the
/// environment's current function and not a stale one, with no list-order choice — throwing when there is no deployed
/// function so the run stays claimable for the dispatcher's retry.
/// </summary>
[TestClass]
public sealed class DeployedFunctionUrlResolverTests
{
    [TestMethod]
    public async Task Resolves_the_deployed_functions_invoke_url()
    {
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore();
        await SeedEnvironmentAsync(environments, "production", "linux-x64");
        await SeedDeployedAsync(deployments, "flow", 1, "production", "linux-x64", "https://fn.example/invoke");
        Func<WorkflowRun, CancellationToken, ValueTask<Uri>> resolve = DeployedFunctionUrlResolver.ForStore(deployments, environments);
        using WorkflowRun run = NewRun("run-1", "flow-v1", "production");

        Uri url = await resolve(run, default);

        url.ShouldBe(new Uri("https://fn.example/invoke"));
    }

    [TestMethod]
    public async Task Throws_when_the_run_has_no_deployed_function()
    {
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore();
        await SeedEnvironmentAsync(environments, "production", "linux-x64");
        Func<WorkflowRun, CancellationToken, ValueTask<Uri>> resolve = DeployedFunctionUrlResolver.ForStore(deployments, environments);
        using WorkflowRun run = NewRun("run-1", "flow-v1", "production");

        // The environment resolves, but nothing is deployed for its target → throw so the run stays claimable for retry.
        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () => await resolve(run, default));
        ex.Message.ShouldContain("not deployed");
    }

    [TestMethod]
    public async Task Throws_when_only_a_different_rid_is_deployed()
    {
        // The environment now targets linux-arm64, but only the superseded linux-x64 function is Deployed: the full-tuple
        // lookup on the environment's current rid excludes it, so the resolver throws rather than route to the stale
        // wrong-architecture function.
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore();
        await SeedEnvironmentAsync(environments, "production", "linux-arm64");
        await SeedDeployedAsync(deployments, "flow", 1, "production", "linux-x64", "https://stale.example/invoke");
        Func<WorkflowRun, CancellationToken, ValueTask<Uri>> resolve = DeployedFunctionUrlResolver.ForStore(deployments, environments);
        using WorkflowRun run = NewRun("run-1", "flow-v1", "production");

        await Should.ThrowAsync<InvalidOperationException>(async () => await resolve(run, default));
    }

    [TestMethod]
    public async Task Throws_when_the_environment_is_unknown()
    {
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore(); // no environments registered
        await SeedDeployedAsync(deployments, "flow", 1, "production", "linux-x64", "https://fn.example/invoke");
        Func<WorkflowRun, CancellationToken, ValueTask<Uri>> resolve = DeployedFunctionUrlResolver.ForStore(deployments, environments);
        using WorkflowRun run = NewRun("run-1", "flow-v1", "production");

        await Should.ThrowAsync<InvalidOperationException>(async () => await resolve(run, default));
    }

    [TestMethod]
    public async Task Throws_when_the_workflow_id_is_not_versioned()
    {
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore();
        Func<WorkflowRun, CancellationToken, ValueTask<Uri>> resolve = DeployedFunctionUrlResolver.ForStore(deployments, environments);
        using WorkflowRun run = NewRun("run-1", "not-versioned", "production");

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(async () => await resolve(run, default));
        ex.Message.ShouldContain("versioned");
    }

    [TestMethod]
    public void Rejects_null_constructor_arguments()
    {
        IWorkflowDeploymentStore deployments = new InMemoryWorkflowDeploymentStore();
        IEnvironmentStore environments = new InMemoryEnvironmentStore();

        Should.Throw<ArgumentNullException>(() => DeployedFunctionUrlResolver.ForStore(null!, environments));
        Should.Throw<ArgumentNullException>(() => DeployedFunctionUrlResolver.ForStore(deployments, null!));
    }

    private static WorkflowRun NewRun(string runId, string workflowId, string environment)
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        return WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, environment);
    }

    private static async Task SeedEnvironmentAsync(IEnvironmentStore environments, string name, string runtimeIdentifier)
    {
        using ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment> draft =
            ParsedJsonDocument<Corvus.Text.Json.Arazzo.Durability.Environments.Environment>.Parse(
                Encoding.UTF8.GetBytes($$"""{"name":"{{name}}","requiredIsolation":"Isolated","runtimeIdentifier":"{{runtimeIdentifier}}"}"""));
        (await environments.AddAsync(draft.RootElement, "ops", default)).Dispose();
    }

    private static async Task SeedDeployedAsync(IWorkflowDeploymentStore deployments, string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, string functionUrl)
    {
        using (ParsedJsonDocument<WorkflowDeployment> draft = WorkflowDeployment.Draft(baseWorkflowId, versionNumber, environment, runtimeIdentifier))
        {
            (await deployments.EnqueueAsync(draft.RootElement, "test", default)).Dispose();
        }

        using ParsedJsonDocument<WorkflowDeployment>? claimed = await deployments.ClaimNextQueuedAsync("test-worker", TimeSpan.FromMinutes(5), default);
        WorkflowDeployment deploying = claimed!.RootElement;
        using (await deployments.CompleteAsync(deploying.IdValue, new WorkflowDeploymentCompletion(WorkflowDeploymentStatus.Deployed, functionUrl), deploying.EtagValue, default))
        {
        }
    }
}