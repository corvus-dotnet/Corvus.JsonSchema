// <copyright file="WorkflowExecutorLeakTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Text;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.OpenApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Tests;

/// <summary>
/// Leak tests for generated executors: run a compiled workflow tens of thousands of times against a
/// reused, reset <see cref="JsonWorkspace"/> and assert the retained managed footprint does not grow
/// with the run count. A genuine per-run leak — an unreturned pooled document (the pre-pooling
/// interpolation path), an accumulating collection, or a workspace that never releases its documents —
/// retains memory proportional to the run count and trips the bound; a clean, fully-pooled pipeline
/// settles to a near-constant footprint.
/// </summary>
public partial class WorkflowExecutorEndToEndTests
{
    [TestMethod]
    public async Task Interpolated_executor_does_not_retain_memory_across_runs()
    {
        // Exercises the inlined interpolation path: a workspace-pooled buffer and a workspace-registered
        // FixedJsonValueDocument that must be returned to its pool on each Reset.
        await AssertNoRetainedGrowthAsync(
            EmitGetPetExecutor(InterpolatedParamDocument, "AdoptInterpolatedWorkflow"),
            "GeneratedWorkflows.AdoptInterpolatedWorkflow",
            """{"id":"42"}""",
            """{"name":"Fido"}""");
    }

    [TestMethod]
    public async Task JsonPath_criterion_executor_does_not_retain_memory_across_runs()
    {
        // Exercises the inlined JSONPath path: EvaluateNodes against the run workspace plus a
        // pool-rented JsonPathResult that must be disposed each run.
        await AssertNoRetainedGrowthAsync(
            EmitGetPetExecutor(JsonPathCriteriaDocument, "AdoptJsonPathWorkflow"),
            "GeneratedWorkflows.AdoptJsonPathWorkflow",
            """{"petId":"42"}""",
            """{"name":"Fido","tags":[{"primary":false},{"primary":true}]}""");
    }

    [TestMethod]
    public async Task Body_output_executor_does_not_retain_memory_across_runs()
    {
        // The base path: response-body clone + step/workflow output documents, all workspace-owned and
        // released on Reset.
        await AssertNoRetainedGrowthAsync(
            EmitGetPetExecutor(Document, "AdoptWorkflow"),
            "GeneratedWorkflows.AdoptWorkflow",
            """{"petId":"42"}""",
            """{"name":"Fido"}""");
    }

    [TestMethod]
    public async Task Inlined_interpolation_allocates_little_more_than_the_base_path_per_run()
    {
        // The base path binds petId from $inputs.petId; the interpolation path binds "pet-{$inputs.id}".
        // Output extraction, criteria, client call, and response handling are identical, so the
        // difference in per-run allocation is the interpolation cost alone. Measuring both in the same
        // run cancels the response fake's allocation and the environment, leaving a stable delta.
        long basePerOp = await MeasurePerOpAllocationAsync(
            EmitGetPetExecutor(Document, "AdoptWorkflow"), "GeneratedWorkflows.AdoptWorkflow", """{"petId":"42"}""", """{"name":"Fido"}""");
        long interpolationPerOp = await MeasurePerOpAllocationAsync(
            EmitGetPetExecutor(InterpolatedParamDocument, "AdoptInterpolatedWorkflow"), "GeneratedWorkflows.AdoptInterpolatedWorkflow", """{"id":"42"}""", """{"name":"Fido"}""");

        long overhead = interpolationPerOp - basePerOp;

        // The inlined path pools its buffer and returns its string document to the workspace, so the
        // overhead is a small constant. The pre-inlining path (a fresh ArrayBufferWriter + an unreturned
        // FixedJsonValueDocument + the WorkflowExecutionContext) added several hundred bytes per run; a
        // 300-byte ceiling catches a regression to it while tolerating measurement noise.
        overhead.ShouldBeLessThan(
            300,
            $"interpolation added {overhead} B/run over the base path (base {basePerOp}, interpolation {interpolationPerOp})");
    }

    private static async Task<long> MeasurePerOpAllocationAsync(string source, string typeName, string inputsJson, string responseJson)
    {
        Assembly assembly = CompileInMemory(source);
        var execute = assembly.GetType(typeName)!.GetMethod("ExecuteAsync")!
            .CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();

        var transport = new FixedResponseTransport(200, responseJson);
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputsJson));
        JsonElement inputs = inputsDocument.RootElement;

        // The fake transport completes synchronously, so every await continues inline on this thread and
        // GetAllocatedBytesForCurrentThread captures the whole run.
        async Task RunBatchAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                workspace.Reset();
                _ = await execute(transport, workspace, inputs, default).ConfigureAwait(false);
            }
        }

        await RunBatchAsync(5_000).ConfigureAwait(false);

        const int measured = 50_000;
        long before = GC.GetAllocatedBytesForCurrentThread();
        await RunBatchAsync(measured).ConfigureAwait(false);
        long after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / measured;
    }

    private static async Task AssertNoRetainedGrowthAsync(string source, string typeName, string inputsJson, string responseJson)
    {
        Assembly assembly = CompileInMemory(source);
        var execute = assembly.GetType(typeName)!.GetMethod("ExecuteAsync")!
            .CreateDelegate<Func<IApiTransport, JsonWorkspace, JsonElement, CancellationToken, ValueTask<JsonElement>>>();

        var transport = new FixedResponseTransport(200, responseJson);
        using var workspace = JsonWorkspace.Create();
        using var inputsDocument = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(inputsJson));
        JsonElement inputs = inputsDocument.RootElement;

        async Task RunBatchAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                workspace.Reset();
                _ = await execute(transport, workspace, inputs, default).ConfigureAwait(false);
            }
        }

        // Warm up: JIT the executor, prime the array/document pools, and settle the workspace's backing
        // arrays so the steady-state footprint is established before measuring.
        await RunBatchAsync(2_000).ConfigureAwait(false);

        long before = StableManagedMemory();
        await RunBatchAsync(50_000).ConfigureAwait(false);
        long after = StableManagedMemory();

        long growth = after - before;

        // 50k runs: an unreturned per-run document (hundreds of bytes) would retain multiple megabytes.
        // The 1 MB bound (~20 B/run) absorbs runtime/pool noise while still catching a real leak by an
        // order of magnitude.
        growth.ShouldBeLessThan(
            1024 * 1024,
            $"retained {growth} bytes across 50k runs (~{growth / 50_000.0:0.0} B/run) — expected a flat footprint");
    }

    private static long StableManagedMemory()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    /// <summary>
    /// A minimal non-recording <see cref="IApiTransport"/> that replays one fixed response. Unlike
    /// <see cref="Testing.MockApiTransport"/> it keeps no per-call request log, so its own bookkeeping
    /// cannot masquerade as an executor leak.
    /// </summary>
    private sealed class FixedResponseTransport(int statusCode, string jsonBody) : IApiTransport
    {
        private readonly byte[] body = Encoding.UTF8.GetBytes(jsonBody);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Respond<TResponse>(cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TBody, TResponse>(in TRequest request, in TBody body, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TBody : struct, IJsonElement<TBody>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Respond<TResponse>(cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Stream body, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Respond<TResponse>(cancellationToken);

        public ValueTask<TResponse> SendAsync<TRequest, TResponse>(in TRequest request, Func<Stream, CancellationToken, ValueTask> bodyWriter, string contentType, CancellationToken cancellationToken = default)
            where TRequest : struct, IApiRequest<TRequest>
            where TResponse : struct, IApiResponse<TResponse>
            => this.Respond<TResponse>(cancellationToken);

        public ValueTask DisposeAsync() => default;

        private ValueTask<TResponse> Respond<TResponse>(CancellationToken cancellationToken)
            where TResponse : struct, IApiResponse<TResponse>
        {
            var stream = new MemoryStream(this.body, writable: false);
            return TResponse.CreateAsync(statusCode, stream, "application/json", cancellationToken: cancellationToken);
        }
    }
}