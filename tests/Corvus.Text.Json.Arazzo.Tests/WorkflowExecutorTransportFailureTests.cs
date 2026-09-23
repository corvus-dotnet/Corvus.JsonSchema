// <copyright file="WorkflowExecutorTransportFailureTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Arazzo.Testing;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// End-to-end proof that a generated durable executor treats a failure of the exchange (ADR 0068 piece 4: a request
/// past the run's step timeout, a response over its size, a connection that failed) as the step's failure: its
/// <c>onFailure</c> actions are dispatched without a response, each retry spends fuel, and with no action that can
/// match the run faults at the step and the exception does not escape.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    private const string StatusOnlyRetryDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "statusOnly",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 3, "criteria": [ { "condition": "$statusCode == 500" } ] } ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    private const string TimedStatusOnlyRetryDocument = """
        {
          "arazzo": "1.0.1",
          "info": { "title": "t", "version": "1.0.0" },
          "sourceDescriptions": [ { "name": "petstore", "url": "./p.yaml", "type": "openapi" } ],
          "workflows": [
            {
              "workflowId": "timedStatusOnly",
              "steps": [
                {
                  "stepId": "getPet",
                  "operationId": "getPet",
                  "timeout": 50,
                  "parameters": [ { "name": "petId", "in": "path", "value": "$inputs.petId" } ],
                  "successCriteria": [ { "condition": "$statusCode == 200" } ],
                  "onFailure": [ { "name": "retry5xx", "type": "retry", "retryAfter": 0, "retryLimit": 3, "criteria": [ { "condition": "$statusCode == 500" } ] } ]
                }
              ],
              "outputs": {}
            }
          ]
        }
        """;

    [TestMethod]
    public async Task A_source_that_never_answers_spends_the_runs_fuel_and_ends_the_run_on_it()
    {
        // Every attempt runs past the step timeout. The unconditional retry takes each one, each is journaled as an
        // attempt, and the run ends on its fuel: five requests and no more, not a loop for the whole wall clock.
        var transport = new FailingApiTransport(() => new ApiTransportTimeoutException("timed out", TimeSpan.FromSeconds(1), null));
        (Durability.WorkflowCheckpointState? state, Exception? unwound) = await RunToEndAsync(RetryFloodDocument, "NeverAnswersWorkflow", "flood", transport, fuel: 5);
        using (state)
        {
            unwound!.GetType().Name.ShouldBe("WorkflowBudgetExhaustedException");
            transport.Sends.ShouldBe(5);
            state!.Status.ShouldBe(WorkflowRunStatus.Faulted);
            state.Fault!.Value.Error.ShouldBe(Durability.ExecutionBudgetFault.Fuel);
            state.StepJournal!.ShouldAllBe(e => e.StepId == "getPet" && e.Status == WorkflowStepStatus.Retrying);
        }
    }

    [TestMethod]
    [DataRow("timeout")]
    [DataRow("too-large")]
    [DataRow("connection")]
    [DataRow("body-stopped")]
    [DataRow("not-json")]
    public async Task A_failed_exchange_with_no_action_that_can_match_faults_the_run_at_the_step(string failure)
    {
        // The only onFailure action reads $statusCode, and a failed exchange has no status, so it cannot match. The
        // step has failed like any other: a durable fault at the step, one request, and nothing escapes to the host.
        var transport = new FailingApiTransport(() => failure switch
        {
            "timeout" => new ApiTransportTimeoutException("timed out", TimeSpan.FromSeconds(1), null),
            "too-large" => new ApiResponseTooLargeException("too large", 16),
            "connection" => new HttpRequestException("connection refused"),
            "body-stopped" => new IOException("the response ended prematurely"),
            _ => new JsonException("'<' is an invalid start of a value"),
        });

        (Durability.WorkflowCheckpointState? state, Exception? unwound) = await RunToEndAsync(StatusOnlyRetryDocument, "NoMatch" + failure.Replace("-", string.Empty, StringComparison.Ordinal) + "Workflow", "statusOnly", transport, fuel: 50);
        using (state)
        {
            unwound.ShouldBeNull();
            transport.Sends.ShouldBe(1);
            state!.Status.ShouldBe(WorkflowRunStatus.Faulted);
            state.Fault!.Value.StepId.ShouldBe("getPet");
            Durability.ExecutionBudgetFault.IsBudgetFault(state.Fault!.Value.Error).ShouldBeFalse();
        }
    }

    [TestMethod]
    public async Task A_failure_that_is_not_the_exchanges_is_not_the_steps_to_handle()
    {
        // A defect, or anything else the classifier does not name, propagates. The execution seam above the executor
        // is what ends the run for it.
        var transport = new FailingApiTransport(() => new InvalidOperationException("a defect"));

        (Durability.WorkflowCheckpointState? state, Exception? unwound) = await RunToEndAsync(RetryFloodDocument, "DefectWorkflow", "flood", transport, fuel: 50);
        using (state)
        {
            unwound.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("a defect");
            transport.Sends.ShouldBe(1);

            // The executor recorded nothing, so the run is still live. HostedWorkflowExecution is what faults it.
            state.ShouldBeNull();
        }
    }

    [TestMethod]
    public void A_timed_step_whose_only_failure_action_reads_the_status_compiles()
    {
        // The no-response dispatch used to emit the action's criterion against a response that does not exist
        // ("if (.StatusCode == 500)"), so a step with both a timeout and a status criterion could not be generated.
        string source = EmitGetPetExecutor(TimedStatusOnlyRetryDocument, "TimedStatusOnlyWorkflow", durable: true);

        source.ShouldNotContain("(.StatusCode");
        CompileInMemory(source).ShouldNotBeNull();
    }

    private static async Task<(Durability.WorkflowCheckpointState? State, Exception? Unwound)> RunToEndAsync(string document, string className, string workflowId, IApiTransport transport, int fuel)
    {
        string source = EmitGetPetExecutor(document, className, durable: true);
        Assembly assembly = CompileInMemory(source);
        var execute = assembly.GetType("GeneratedWorkflows." + className)!.GetMethod("ExecuteAsync")!
            .CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, IWorkflowRun?, CancellationToken, TimeProvider?, ValueTask<WorkflowRunResult<JsonElement>>>>();

        var store = new Durability.InMemoryWorkflowStateStore();
        var budget = new Durability.ExecutionBudget(fuel, TimeSpan.FromHours(1), 8, TimeSpan.Zero, Durability.ExecutionBudget.DefaultStepTimeout, Durability.ExecutionBudget.DefaultMaxResponseBytes);
        using var workspace = JsonWorkspace.CreateUnrented();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("""{"petId":"42"}"""));
        JsonElement inputs = inputsDocument.RootElement;
        string runId = className.ToLowerInvariant();
        using var run = Durability.WorkflowRun.CreateNew(store, runId, workflowId, inputs, "development", budget: budget);

        Exception? unwound = null;
        try
        {
            await execute(transport, workspace, inputs, run, default, null);
        }
        catch (Exception ex)
        {
            unwound = ex;
        }

        Durability.WorkflowCheckpoint? stored = await store.LoadAsync(TestAddresses.Dev(runId), default);
        return (stored is { } checkpoint ? Durability.WorkflowCheckpointSerializer.Deserialize(checkpoint.Row) : null, unwound);
    }

    // A transport whose every send fails the way the factory says, counting the sends made on it.
    private sealed class FailingApiTransport(Func<Exception> failure) : IApiTransport
    {
        public int Sends { get; private set; }

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Fail<TResponse>();

        public ValueTask DisposeAsync() => default;

        private ValueTask<TResponse> Fail<TResponse>()
        {
            this.Sends++;
            return ValueTask.FromException<TResponse>(failure());
        }
    }
}