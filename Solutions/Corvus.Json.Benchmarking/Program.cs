// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
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
            Job.Default
                .WithRuntime(CoreRuntime.Core90)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        config.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core10_0)
                .WithOutlierMode(OutlierMode.RemoveAll)
                .WithStrategy(RunStrategy.Throughput));

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(config: config);
    }
}