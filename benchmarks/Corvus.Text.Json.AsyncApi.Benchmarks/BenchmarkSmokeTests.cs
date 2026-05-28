// <copyright file="BenchmarkSmokeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace AsyncApiBenchmark;

/// <summary>
/// Smoke tests that exercise every benchmark method once before BDN starts.
/// Catches exceptions immediately rather than waiting for a 20+ minute run.
/// </summary>
internal static class BenchmarkSmokeTests
{
    /// <summary>
    /// Runs every benchmark method once and throws if any fail.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunAll()
    {
        Console.WriteLine("Running benchmark smoke tests...");

        await VerifyPublishPipeline().ConfigureAwait(false);
        await VerifySubscribePipeline().ConfigureAwait(false);
        await VerifyRequestReply().ConfigureAwait(false);

        Console.WriteLine("All smoke tests passed.");
        Console.WriteLine();
    }

    private static async Task VerifyPublishPipeline()
    {
        PublishPipelineBenchmarks bench = new();
        await bench.Setup().ConfigureAwait(false);

        await VerifyAsync("Publish.Wolverine_Publish", () => bench.Wolverine_Publish()).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_NoValidation", () => bench.Corvus_NoValidation()).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_WithBasicValidation", () => bench.Corvus_WithBasicValidation()).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_WithDetailedValidation", () => bench.Corvus_WithDetailedValidation()).ConfigureAwait(false);

        await bench.Cleanup().ConfigureAwait(false);
    }

    private static async Task VerifySubscribePipeline()
    {
        SubscribePipelineBenchmarks bench = new();
        await bench.Setup().ConfigureAwait(false);

        await VerifyAsync("Subscribe.Wolverine_DeserializeAndDispatch", () => bench.Wolverine_DeserializeAndDispatch()).ConfigureAwait(false);
        await VerifyAsync("Subscribe.Corvus_NoValidation", () => bench.Corvus_NoValidation()).ConfigureAwait(false);
        await VerifyAsync("Subscribe.Corvus_WithBasicValidation", () => bench.Corvus_WithBasicValidation()).ConfigureAwait(false);
        await VerifyAsync("Subscribe.Corvus_WithDetailedValidation", () => bench.Corvus_WithDetailedValidation()).ConfigureAwait(false);
        await VerifyAsync("Subscribe.Corvus_FullPipelineWithHeaders", () => bench.Corvus_FullPipelineWithHeaders()).ConfigureAwait(false);

        await bench.Cleanup().ConfigureAwait(false);
    }

    private static async Task VerifyRequestReply()
    {
        RequestReplyBenchmarks bench = new();
        await bench.Setup().ConfigureAwait(false);

        await VerifyAsync("RequestReply.Wolverine_RequestReply", () => bench.Wolverine_RequestReply()).ConfigureAwait(false);
        await VerifyAsync("RequestReply.Corvus_NoValidation", () => bench.Corvus_RequestReply_NoValidation()).ConfigureAwait(false);
        await VerifyAsync("RequestReply.Corvus_WithValidation", () => bench.Corvus_RequestReply_WithValidation()).ConfigureAwait(false);

        await bench.Cleanup().ConfigureAwait(false);
    }

    private static async Task VerifyAsync(string name, Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL: {name} — {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException($"Smoke test failed: {name}", ex);
        }
    }

    private static async Task VerifyAsync<T>(string name, Func<Task<T>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL: {name} — {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException($"Smoke test failed: {name}", ex);
        }
    }

    private static async Task VerifyAsync(string name, Func<ValueTask> action)
    {
        try
        {
            await action().ConfigureAwait(false);
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL: {name} — {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException($"Smoke test failed: {name}", ex);
        }
    }

    private static async Task VerifyAsync<T>(string name, Func<ValueTask<T>> action)
    {
        try
        {
            _ = await action().ConfigureAwait(false);
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL: {name} — {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException($"Smoke test failed: {name}", ex);
        }
    }
}