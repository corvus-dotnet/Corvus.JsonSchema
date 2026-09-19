// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Perfolizer.Mathematics.OutlierDetection;

namespace Corvus.Json.Benchmarking;

internal class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "ab")
        {
            return AbHarness.Run(args);
        }

        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(
            Job.Default
                .AsBaseline()
                .WithRuntime(CoreRuntime.Core80)
                .WithId(".NET 8.0")
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core90)
                .WithId(".NET 9.0")
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default
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
    }
}