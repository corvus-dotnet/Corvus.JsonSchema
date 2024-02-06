// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using Perfolizer.Mathematics.OutlierDetection;

/// <summary>
/// Benchmark runner.
/// </summary>
internal class Program
{
    private static void Main(string[] args)
    {
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(
            args,
            GetGlobalConfig());
    }

    private static IConfig? GetGlobalConfig()
    {
        return DefaultConfig.Instance
        .AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithOutlierMode(OutlierMode.RemoveAll)
            .WithStrategy(RunStrategy.Throughput).AsDefault())
        .AddValidator(ExecutionValidator.DontFailOnError);
    }
}