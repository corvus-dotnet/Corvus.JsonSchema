// <copyright file="WorkflowExecutorBudgetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Arazzo.Tests.Fakes;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof that a generated durable executor spends its run's execution budget (ADR 0068) per attempt: a
/// retry loop is bounded by the fuel the way a goto loop is, a sub-workflow's attempts spend the root's fuel, and
/// mutual recursion between workflows stops at the depth cap.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string RetryFloodDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "flood",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "again", "type": "retry", "retryAfter": 0, "retryLimit": 2000000000 } ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    private const string MutualRecursionDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "parent",
              "steps": [
                { "stepId": "callChild", "workflowId": "child", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ],
              "outputs": {}
            },
            {
              "workflowId": "child",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ]
                },
                { "stepId": "callParent", "workflowId": "parent", "parameters": [ { "name": "petId", "value": "$inputs.petId" } ] }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task A_retry_loop_spends_the_runs_fuel_one_unit_per_attempt_and_stops_at_it()
    {
        // The hazard ADR 0068 exists for. retryLimit is the workflow author's and has no cap, and retryAfter 0 makes a
        // tight loop, so before attempts were journaled one step could send requests for the whole wall clock on a
        // single unit of fuel. With five units the source sees five requests and no more.
        string source = EmitGetPetExecutor(RetryFloodDocument, "RetryFloodWorkflow", durable: true);
        Assembly assembly = CompileInMemory(source);
        var execute = assembly.GetType("GeneratedWorkflows.RetryFloodWorkflow")!.GetMethod("ExecuteAsync")!
            .CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, IWorkflowRun?, CancellationToken, TimeProvider?, ValueTask<WorkflowRunResult<JsonElement>>>>();

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");

        var store = new Durability.InMemoryWorkflowStateStore();
        var budget = new Durability.ExecutionBudget(5, TimeSpan.FromHours(1), 8, TimeSpan.Zero, Durability.ExecutionBudget.DefaultStepTimeout, Durability.ExecutionBudget.DefaultMaxResponseBytes);
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        JsonElement inputs = inputsDocument.RootElement;
        using var run = Durability.WorkflowRun.CreateNew(store, "flood-1", "flood", inputs, "development", budget: budget);

        Exception unwound = await Should.ThrowAsync<Exception>(async () => await execute(transport, workspace, inputs, run, default, null));
        unwound.GetType().Name.ShouldBe("WorkflowBudgetExhaustedException");

        transport.Requests.Count.ShouldBe(5);

        Durability.WorkflowCheckpoint stored = (await store.LoadAsync(TestAddresses.Dev("flood-1"), default))!.Value;
        using Durability.WorkflowCheckpointState state = Durability.WorkflowCheckpointSerializer.Deserialize(stored.Row);
        state.Status.ShouldBe(WorkflowRunStatus.Faulted);
        state.Fault!.Value.Error.ShouldBe(Durability.ExecutionBudgetFault.Fuel);
        state.StepJournal!.Count.ShouldBe(5);
        state.StepJournal.ShouldAllBe(e => e.StepId == "getPet" && e.Status == WorkflowStepStatus.Retrying);
        state.StepJournal.Select(e => e.Attempt).ShouldBe([1, 2, 3, 4, 5]);

        // The run faulted itself inside its fuel, so the control plane's predicate has nothing to refuse.
        Durability.WorkflowCheckpointSerializer.TryReadBudgetFacts(stored.Row, out Durability.CheckpointBudgetFacts facts).ShouldBeTrue();
        Durability.ExecutionBudgetFault.Find(budget, facts, state.CreatedAt, state.CreatedAt).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_run_completes_on_exactly_its_fuel()
    {
        // Fuel N admits N attempts: the goto document executes two steps, and two units are enough.
        string source = EmitGetPetExecutor(GotoDocument, "ExactFuelWorkflow", durable: true);
        Assembly assembly = CompileInMemory(source);
        var execute = assembly.GetType("GeneratedWorkflows.ExactFuelWorkflow")!.GetMethod("ExecuteAsync")!
            .CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, IWorkflowRun?, CancellationToken, TimeProvider?, ValueTask<WorkflowRunResult<JsonElement>>>>();

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        var store = new Durability.InMemoryWorkflowStateStore();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        JsonElement inputs = inputsDocument.RootElement;
        using var run = Durability.WorkflowRun.CreateNew(
            store, "exact-1", "gotoAdopt", inputs, "development", budget: new Durability.ExecutionBudget(2, TimeSpan.FromHours(1), 8, TimeSpan.Zero, Durability.ExecutionBudget.DefaultStepTimeout, Durability.ExecutionBudget.DefaultMaxResponseBytes));

        WorkflowRunResult<JsonElement> result = await execute(transport, workspace, inputs, run, default, null);

        result.IsCompleted.ShouldBeTrue();
        transport.Requests.Count.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_sub_workflows_attempts_spend_the_roots_fuel_and_are_journaled_under_the_invoking_step()
    {
        Assembly assembly = await GenerateAndCompileDurableWorkflows(RetrySubWorkflowDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        // The child fails once (500) and the parent's retry re-invokes it (200).
        var transport = new MockApiTransport();
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 500, "{}");
        transport.EnqueueResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        var store = new Durability.InMemoryWorkflowStateStore();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using var run = Durability.WorkflowRun.CreateNew(
            store, "sub-1", "parent", inputsDocument.RootElement, "development", budget: new Durability.ExecutionBudget(10, TimeSpan.FromHours(1), 8, TimeSpan.Zero, Durability.ExecutionBudget.DefaultStepTimeout, Durability.ExecutionBudget.DefaultMaxResponseBytes));

        var pending = (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!;
        WorkflowRunResult<JsonElement> result = await pending;

        result.IsCompleted.ShouldBeTrue();
        transport.Requests.Count.ShouldBe(2);

        using Durability.WorkflowCheckpointState state = Durability.WorkflowCheckpointSerializer.Deserialize((await store.LoadAsync(TestAddresses.Dev("sub-1"), default))!.Value.Row);
        state.StepJournal!.Select(e => (e.StepId, e.Status, e.Attempt)).ShouldBe(
        [
            ("callChild/getPet", WorkflowStepStatus.Faulted, 1),
            ("callChild", WorkflowStepStatus.Retrying, 1),
            ("callChild/getPet", WorkflowStepStatus.Succeeded, 1),
            ("callChild", WorkflowStepStatus.Succeeded, 2),
        ]);
    }

    [TestMethod]
    public async Task Mutual_recursion_between_workflows_stops_at_the_depth_cap()
    {
        Assembly assembly = await GenerateAndCompileDurableWorkflows(MutualRecursionDocument);
        MethodInfo execute = assembly.GetType("GeneratedWorkflows.Workflows.ParentWorkflow")!.GetMethod("ExecuteAsync")!;

        var transport = new MockApiTransport();
        transport.SetResponse(OperationMethod.Get, "/pets/{petId}", 200, """{"name":"Fido"}""");

        var store = new Durability.InMemoryWorkflowStateStore();
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        using var run = Durability.WorkflowRun.CreateNew(
            store, "depth-1", "parent", inputsDocument.RootElement, "development", budget: new Durability.ExecutionBudget(100, TimeSpan.FromHours(1), 2, TimeSpan.Zero, Durability.ExecutionBudget.DefaultStepTimeout, Durability.ExecutionBudget.DefaultMaxResponseBytes));

        Exception unwound = await Should.ThrowAsync<Exception>(async () =>
            await (ValueTask<WorkflowRunResult<JsonElement>>)execute.Invoke(null, [transport, workspace, inputsDocument.RootElement, run, default(CancellationToken), null])!);
        unwound.GetType().Name.ShouldBe("WorkflowBudgetExhaustedException");

        // Depth one is the child and depth two the parent it re-enters; the child that would nest at depth three is
        // refused before it reaches the source, so the source saw the one request the first child made.
        transport.Requests.Count.ShouldBe(1);
        using Durability.WorkflowCheckpointState state = Durability.WorkflowCheckpointSerializer.Deserialize((await store.LoadAsync(TestAddresses.Dev("depth-1"), default))!.Value.Row);
        state.Status.ShouldBe(WorkflowRunStatus.Faulted);
        state.Fault!.Value.Error.ShouldBe(Durability.ExecutionBudgetFault.Depth);
        state.Fault!.Value.StepId.ShouldBe("callChild/callParent/callChild");
    }

    private static async Task<Assembly> GenerateAndCompileDurableWorkflows(string document)
    {
        OperationDescriptor[] operations =
        [
            new(
                "/pets/{petId}",
                OperationMethod.Get,
                "getPet",
                "GetPet",
                typeof(PetByIdRequest).FullName!,
                typeof(PetByIdResponse).FullName!,
                [new RequestParameterInfo("petId", ParameterLocation.Path, "PetId", "Corvus.Text.Json.JsonElement", true, "petId")],
                false,
                [new ResponseDescriptor("200", "Corvus.Text.Json.JsonElement", "OkBody")],
                typeof(PetByIdClient).FullName!,
                "GetPetAsync",
                null,
                null),
        ];

        var binder = new WorkflowOperationBinder([new SourceDescriptionClient("petstore", OperationResolver.Create("petstore", operations))]);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration.GenerateAsync(
            Encoding.UTF8.GetBytes(document), binder, new ArazzoGenerationOptions("GeneratedWorkflows", Durable: true));

        string[] executors = [.. files.Where(f => f.FileName.StartsWith("Workflows/", StringComparison.Ordinal)).Select(f => f.Content)];
        return CompileInMemory(executors);
    }
}