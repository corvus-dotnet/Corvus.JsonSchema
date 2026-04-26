// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.OutlierDetection;

namespace Corvus.Text.Json.Benchmarks;

internal class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "profile")
        {
            RunProfileHarness();
            return;
        }

        ManualConfig config = ManualConfig.CreateEmpty()
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(MarkdownExporter.Default)
            .AddExporter(BenchmarkDotNet.Exporters.Json.JsonExporter.Full)
            .WithBuildTimeout(TimeSpan.FromMinutes(15));

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core10_0)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config: config);
    }

    private static void RunProfileHarness()
    {
        Console.WriteLine($"PID: {Environment.ProcessId}");
        Console.WriteLine("Warming up...");

        for (int i = 0; i < 1000; i++)
        {
            BuildOnly();
        }

        Console.WriteLine("Warmup complete. Press Enter to start profiling run...");
        Console.ReadLine();

        Console.WriteLine("Running 10M iterations...");
        long start = System.Diagnostics.Stopwatch.GetTimestamp();

        for (int i = 0; i < 10_000_000; i++)
        {
            BuildOnly();
        }

        double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalSeconds;
        Console.WriteLine($"Done. {elapsed:F2}s ({10_000_000 / elapsed:F0} ops/sec, {elapsed / 10_000_000 * 1e9:F1} ns/op)");
    }

    private static void BuildOnly()
    {
        using var workspace = JsonWorkspace.Create();
        using var person = Benchmark.CorvusTextJson.Person.CreateBuilder(
            workspace,
            (ref b) => b.Create(
            age: 51,
            name: Benchmark.CorvusTextJson.PersonName.Build(static (ref personName) =>
            {
                personName.Create(
                    firstName: "Michael"u8,
                    lastName: "Adams"u8,
                    otherNames: Benchmark.CorvusTextJson.OtherNames.Build(static (ref otherNames) =>
                    {
                        otherNames.AddItem("Francis"u8);
                        otherNames.AddItem("James"u8);
                    }));
            }),
            competedInYears: Benchmark.CorvusTextJson.CompetedInYears.Build([2012, 2016, 2024])));
    }
}