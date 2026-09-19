// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Corvus.Text.Json.DotNetVersions.Benchmarks;
using Perfolizer.Mathematics.OutlierDetection;

if (args.Length > 0 && args[0] == "ab")
{
    return AbHarness.Run(args);
}

// The .NET version-over-version series for the V5 engine. The same binaries (built for net10.0)
// run on each runtime, so any difference is the runtime's alone. Add a job per .NET release.
// The V4 series (from .NET 8.0) lives in src-v4/Corvus.Json.Benchmarking.
ManualConfig config = ManualConfig.CreateEmpty()
    .AddColumnProvider(DefaultColumnProviders.Instance)
    .AddLogger(ConsoleLogger.Default)
    .AddExporter(MarkdownExporter.GitHub)
    .AddExporter(JsonExporter.Full)
    .WithBuildTimeout(TimeSpan.FromMinutes(15));

config.AddJob(
    Job.Default
        .AsBaseline()
        .WithRuntime(CoreRuntime.Core10_0)
        .WithId(".NET 10.0")
        .WithOutlierMode(OutlierMode.RemoveAll)
        .WithStrategy(RunStrategy.Throughput));

// BenchmarkDotNet 0.15.8 has no .NET 11 runtime moniker (and its SDK validator
// rejects CoreRuntime.CreateForNewVersion), so we specify the toolchain directly.
config.AddJob(
    Job.Default
        .WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("net11.0", null, ".NET 11.0")))
        .WithId(".NET 11.0")
        .WithOutlierMode(OutlierMode.RemoveAll)
        .WithStrategy(RunStrategy.Throughput));

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
return 0;