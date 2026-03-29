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
        ManualConfig config = ManualConfig.CreateEmpty()
            .AddColumnProvider(DefaultColumnProviders.Instance) // empty config does not define any columns for the output table, we need to define it manually
            .AddLogger(ConsoleLogger.Default)
            .AddExporter(MarkdownExporter.Default);

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core10_0)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        ////config.AddJob(
        ////    Job.Default
        ////        .AsBaseline()
        ////        .WithRuntime(CoreRuntime.Core90)
        ////        .WithOutlierMode(OutlierMode.RemoveAll)
        ////        .WithStrategy(RunStrategy.Throughput));

        ////config.AddJob(
        ////    Job.Default
        ////        .WithRuntime(CoreRuntime.Core80)
        ////        .WithOutlierMode(OutlierMode.RemoveAll)
        ////        .WithStrategy(RunStrategy.Throughput));

        ////config.AddJob(
        ////    Job.Default
        ////        .WithRuntime(ClrRuntime.Net481)
        ////        .WithOutlierMode(OutlierMode.RemoveAll)
        ////        .WithStrategy(RunStrategy.Throughput));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config: config);
    }
}