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

internal class Program
{
    private static void Main(string[] args)
    {
        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddJob(
            Job.Default
                .AsBaseline()
                .WithRuntime(CoreRuntime.Core80)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default.WithToolchain(
                CsProjCoreToolchain.From(
                    new NetCoreAppSettings(
                        targetFrameworkMoniker: "net9.0",
                        runtimeFrameworkVersion: "9.0.0-rc.2.24461.16",
                        name: ".NET 9.0 RC2 Nightly")))
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(config: config);
    }
}