// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using AsyncApiBenchmark;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.OutlierDetection;

// Always run smoke tests first to catch exceptions before a 20+ minute BDN run
await BenchmarkSmokeTests.RunAll().ConfigureAwait(false);

if (args.Contains("--smoke-only"))
{
    return;
}

ManualConfig config = ManualConfig.CreateEmpty()
    .AddColumnProvider(DefaultColumnProviders.Instance)
    .AddLogger(ConsoleLogger.Default)
    .AddExporter(MarkdownExporter.Default)
    .WithBuildTimeout(TimeSpan.FromMinutes(15));

config.AddJob(
    Job.Default
        .WithRuntime(CoreRuntime.Core10_0)
        .WithOutlierMode(OutlierMode.RemoveAll)
        .WithStrategy(RunStrategy.Throughput));

BenchmarkRunner.Run(
    [
        typeof(PublishPipelineBenchmarks),
        typeof(SubscribePipelineBenchmarks),
        typeof(RequestReplyBenchmarks),
    ],
    config);