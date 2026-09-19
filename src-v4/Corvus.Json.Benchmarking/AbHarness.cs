// <copyright file="AbHarness.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Corvus.Json.Benchmarking;

/// <summary>
/// A stopwatch harness for alternating A/B comparisons of <see cref="ValidateLargeDocumentCorvusOnly.ValidateLargeArrayCorvusV4"/> between runtimes.
/// </summary>
/// <remarks>
/// <para>
/// BenchmarkDotNet runs its jobs in order, directly after it builds them, so the first runtime is measured on a
/// machine that is still hot from the build. When the difference between adjacent runtimes is a few percent, that
/// ordering decides the result. This harness measures in one process, so that a script can alternate
/// processes between runtimes (<c>dotnet exec --fx-version &lt;version&gt;</c> runs the same binaries on each).
/// </para>
/// <para>
/// Usage: <c>ab [measureSeconds]</c>.
/// </para>
/// </remarks>
internal static class AbHarness
{
    /// <summary>
    /// Runs the harness.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int Run(string[] args)
    {
        double measureSeconds = args.Length > 1 ? double.Parse(args[1]) : 5;

        var benchmark = new ValidateLargeDocumentCorvusOnly();
        benchmark.GlobalSetup();

        // Two bursts, with a pause longer than the tiering delay between them, so that the
        // measured code is at its final tier (with dynamic PGO applied) before we measure.
        Burst(benchmark, 2);
        Thread.Sleep(400);
        Burst(benchmark, 2);

        List<double> samples = [];
        long end = Stopwatch.GetTimestamp() + (long)(measureSeconds * Stopwatch.Frequency);
        bool result = true;
        while (Stopwatch.GetTimestamp() < end)
        {
            long start = Stopwatch.GetTimestamp();
            result &= benchmark.ValidateLargeArrayCorvusV4();
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
            $"{RuntimeInformation.FrameworkDescription} V4 n={samples.Count} min={samples[0]:F4} p25={samples[samples.Count / 4]:F4} median={samples[samples.Count / 2]:F4} mean={samples.Average():F4} ms");
        return 0;
    }

    private static void Burst(ValidateLargeDocumentCorvusOnly benchmark, double seconds)
    {
        long end = Stopwatch.GetTimestamp() + (long)(seconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < end)
        {
            benchmark.ValidateLargeArrayCorvusV4();
        }
    }
}