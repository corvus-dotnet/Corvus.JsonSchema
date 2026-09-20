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
    private static void Main(string[] args)
    {
        // "--launches N" runs every runtime job in N separate processes. BenchmarkDotNet's own --launchCount
        // adds another job rather than changing these, and a single launch per job is not enough when
        // the difference between adjacent runtimes is a few percent.
        int launches = 1;
        int launchesIndex = Array.IndexOf(args, "--launches");
        if (launchesIndex >= 0)
        {
            launches = int.Parse(args[launchesIndex + 1]);
            args = [.. args[..launchesIndex], .. args[(launchesIndex + 2)..]];
        }

        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(
            Job.Default
                .AsBaseline()
                .WithRuntime(CoreRuntime.Core80)
                .WithId(".NET 8.0")
                .WithLaunchCount(launches)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core90)
                .WithId(".NET 9.0")
                .WithLaunchCount(launches)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core10_0)
                .WithId(".NET 10.0")
                .WithLaunchCount(launches)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        // BenchmarkDotNet 0.15.8 has no .NET 11 runtime moniker (and its SDK validator
        // rejects CoreRuntime.CreateForNewVersion), so we specify the toolchain directly.
        config.AddJob(
            Job.Default
                .WithToolchain(CsProjCoreToolchain.From(new NetCoreAppSettings("net11.0", null, ".NET 11.0")))
                .WithId(".NET 11.0")
                .WithLaunchCount(launches)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}