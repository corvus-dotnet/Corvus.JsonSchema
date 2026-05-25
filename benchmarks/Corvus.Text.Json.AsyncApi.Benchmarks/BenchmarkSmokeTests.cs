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
        VerifyHeaderEncoding();
        await VerifyRequestReply().ConfigureAwait(false);

        Console.WriteLine("All smoke tests passed.");
        Console.WriteLine();
    }

    private static async Task VerifyPublishPipeline()
    {
        PublishPipelineBenchmarks bench = new();
        bench.Setup();

        Verify("Publish.RawNats_NoValidation", () => bench.RawNats_NoValidation());
        await VerifyAsync("Publish.Corvus_NoValidation", bench.Corvus_NoValidation).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_WithBasicValidation", bench.Corvus_WithBasicValidation).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_WithDetailedValidation", bench.Corvus_WithDetailedValidation).ConfigureAwait(false);
        await VerifyAsync("Publish.Corvus_WithHeaders", bench.Corvus_WithHeaders).ConfigureAwait(false);
    }

    private static async Task VerifySubscribePipeline()
    {
        SubscribePipelineBenchmarks bench = new();
        await bench.Setup().ConfigureAwait(false);

        Verify("Subscribe.RawNats_DeserializeAndHandle", () => bench.RawNats_DeserializeAndHandle());
        Verify("Subscribe.Corvus_NoValidation", () => bench.Corvus_NoValidation());
        Verify("Subscribe.Corvus_WithBasicValidation", () => bench.Corvus_WithBasicValidation());
        Verify("Subscribe.Corvus_WithDetailedValidation", () => bench.Corvus_WithDetailedValidation());
        await VerifyAsync("Subscribe.Corvus_FullPipeline", () => bench.Corvus_FullPipeline()).ConfigureAwait(false);
        await VerifyAsync("Subscribe.Corvus_FullPipelineWithHeaders", () => bench.Corvus_FullPipelineWithHeaders()).ConfigureAwait(false);
    }

    private static void VerifyHeaderEncoding()
    {
        foreach (int count in new[] { 1, 5, 10 })
        {
            HeaderEncodingBenchmarks bench = new() { HeaderCount = count };
            bench.Setup();

            Verify($"Headers[{count}].EncodeHeaders_Base64", () => bench.EncodeHeaders_Base64());
            Verify($"Headers[{count}].DecodeHeaders_Base64", () => bench.DecodeHeaders_Base64());
            Verify($"Headers[{count}].EncodeHeaders_Corvus", () => bench.EncodeHeaders_Corvus());
            Verify($"Headers[{count}].DecodeHeaders_Corvus", () => bench.DecodeHeaders_Corvus());
        }
    }

    private static async Task VerifyRequestReply()
    {
        RequestReplyBenchmarks bench = new();
        bench.Setup();

        Verify("RequestReply.RawNats_RequestReply", () => bench.RawNats_RequestReply());
        await VerifyAsync("RequestReply.Corvus_NoValidation", () => bench.Corvus_RequestReply_NoValidation()).ConfigureAwait(false);
        await VerifyAsync("RequestReply.Corvus_WithValidation", () => bench.Corvus_RequestReply_WithValidation()).ConfigureAwait(false);
    }

    private static void Verify<T>(string name, Func<T> action)
    {
        try
        {
            _ = action();
            Console.WriteLine($"  PASS: {name}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  FAIL: {name} — {ex.GetType().Name}: {ex.Message}");
            throw new InvalidOperationException($"Smoke test failed: {name}", ex);
        }
    }

    private static async Task VerifyAsync(string name, Func<ValueTask<int>> action)
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

    private static async Task VerifyAsync(string name, Func<ValueTask<double>> action)
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
}