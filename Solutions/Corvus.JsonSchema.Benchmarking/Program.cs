// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.Benchmarking
{
    using BenchmarkDotNet.Configs;
    using BenchmarkDotNet.Engines;
    using BenchmarkDotNet.Environments;
    using BenchmarkDotNet.Jobs;
    using BenchmarkDotNet.Running;
    using Perfolizer.Mathematics.OutlierDetection;

    /// <summary>
    /// Main program.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Arguments.</param>
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAllJoined(
                ManualConfig.Create(DefaultConfig.Instance)
                .AddJob(Job.Dry
                    .WithRuntime(CoreRuntime.Core50)
                    .WithOutlierMode(OutlierMode.RemoveAll)
                    .WithStrategy(RunStrategy.Throughput)));
        }
    }
}
