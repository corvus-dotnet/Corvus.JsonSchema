// <copyright file="BakedHostedWorkflowResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo;
using Corvus.Text.Json.AsyncApi;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the <see cref="BakedHostedWorkflowResolver"/> behaves as the AOT counterpart to the loader resolver: a
/// host baked for one version resolves every run of that version to its single compiled-in
/// <see cref="IHostedWorkflow"/>, warms nothing, and fails fast on a run misrouted from a different version.
/// </summary>
[TestClass]
public class BakedHostedWorkflowResolverTests
{
    [TestMethod]
    public async Task Resolves_every_matching_run_to_the_single_baked_workflow()
    {
        var baked = new FakeHostedWorkflow("adopt-v1");
        var resolver = new BakedHostedWorkflowResolver(baked);

        using WorkflowRun first = NewRun("run-1", "adopt-v1");
        using WorkflowRun second = NewRun("run-2", "adopt-v1");

        // Two distinct runs of the baked version both resolve to the same, already-in-hand executor — no loader.
        (await resolver.ResolveAsync(first, default)).ShouldBeSameAs(baked);
        (await resolver.ResolveAsync(second, default)).ShouldBeSameAs(baked);
    }

    [TestMethod]
    public void PrepareAsync_is_a_no_op_that_completes_synchronously()
    {
        var resolver = new BakedHostedWorkflowResolver(new FakeHostedWorkflow("adopt-v1"));

        // The executor is baked in, so there is nothing to warm — PrepareAsync completes without touching anything.
        ValueTask warm = resolver.PrepareAsync("adopt", 1, default);
        warm.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Throws_when_a_run_targets_a_different_version()
    {
        var resolver = new BakedHostedWorkflowResolver(new FakeHostedWorkflow("adopt-v1"));
        using WorkflowRun otherVersion = NewRun("run-1", "adopt-v2");

        // A run for a different version reached this baked host by a routing fault; returning the baked executor
        // would silently run the wrong workflow, so it must throw rather than resolve.
        await Should.ThrowAsync<InvalidOperationException>(async () => await resolver.ResolveAsync(otherVersion, default));
    }

    [TestMethod]
    public void Rejects_a_null_baked_workflow()
    {
        Should.Throw<ArgumentNullException>(() => new BakedHostedWorkflowResolver(null!));
    }

    private static WorkflowRun NewRun(string runId, string workflowId)
    {
        var store = new InMemoryWorkflowStateStore();
        using ParsedJsonDocument<JsonElement> inputs = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"1"}"""));
        return WorkflowRun.CreateNew(store, runId, workflowId, inputs.RootElement, "development");
    }

    // A minimal in-hand executor: it only ever advertises its versioned id, which is all the resolver reads. Its
    // RunAsync is never reached by a resolver test (the resolver hands the instance back; the host runs it).
    private sealed class FakeHostedWorkflow(string workflowId) : IHostedWorkflow
    {
        public WorkflowDescriptor Descriptor { get; } = new(workflowId, [], []);

        public ValueTask<WorkflowRunResultKind> RunAsync(
            IReadOnlyDictionary<string, IApiTransport> apiTransports,
            IReadOnlyDictionary<string, IMessageTransport> messageTransports,
            JsonWorkspace workspace,
            JsonElement inputs,
            IWorkflowRun run,
            CancellationToken cancellationToken)
            => new(WorkflowRunResultKind.Completed);
    }
}