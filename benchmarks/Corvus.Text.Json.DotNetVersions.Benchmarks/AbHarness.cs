// <copyright file="AbHarness.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.DotNetVersions.Benchmarks;

/// <summary>
/// A stopwatch harness for alternating A/B comparisons of one benchmark method between runtimes.
/// </summary>
/// <remarks>
/// <para>
/// BenchmarkDotNet runs its jobs in order, directly after it builds them, so the first runtime is measured on a
/// machine that is still hot from the build. When the difference between adjacent runtimes is a few percent, that
/// ordering decides the result. This harness measures one method in one process, so that a script can alternate
/// processes between runtimes (<c>dotnet exec --fx-version &lt;version&gt;</c> runs the same binaries on each).
/// </para>
/// <para>
/// Usage: <c>ab &lt;GeneratedTypes|StandaloneEvaluator|DynamicValidator&gt; [measureSeconds]</c>.
/// </para>
/// </remarks>
internal static class AbHarness
{
    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ab <GeneratedTypes|StandaloneEvaluator|DynamicValidator> [measureSeconds]");
            return 1;
        }

        double measureSeconds = args.Length > 2 ? double.Parse(args[2]) : 5;

        var benchmark = new ValidateLargeDocument();
        benchmark.GlobalSetup();

        Func<bool> method = args[1] switch
        {
            "GeneratedTypes" => benchmark.ValidateLargeArrayCorvusV5GeneratedTypes,
            "StandaloneEvaluator" => benchmark.ValidateLargeArrayCorvusV5StandaloneEvaluator,
            "DynamicValidator" => benchmark.ValidateLargeArrayCorvusV5DynamicValidator,
            _ => throw new ArgumentException($"Unknown method '{args[1]}'."),
        };

        // Two bursts, with a pause longer than the tiering delay between them, so that the
        // measured code is at its final tier (with dynamic PGO applied) before we measure.
        Burst(method, 2);
        Thread.Sleep(400);
        Burst(method, 2);

        List<double> samples = [];
        long end = Stopwatch.GetTimestamp() + (long)(measureSeconds * Stopwatch.Frequency);
        bool result = true;
        while (Stopwatch.GetTimestamp() < end)
        {
            long start = Stopwatch.GetTimestamp();
            result &= method();
            samples.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        benchmark.GlobalCleanup();

        if (!result)
        {
            Console.Error.WriteLine("The document did not validate.");
            return 1;
        }

        samples.Sort();
        Console.WriteLine(
            $"{RuntimeInformation.FrameworkDescription} {args[1]} n={samples.Count} min={samples[0]:F4} p25={samples[samples.Count / 4]:F4} median={samples[samples.Count / 2]:F4} mean={samples.Average():F4} ms");
        return 0;
    }

    private static void Burst(Func<bool> method, double seconds)
    {
        long end = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < end)
        {
            method();
        }
    }
}