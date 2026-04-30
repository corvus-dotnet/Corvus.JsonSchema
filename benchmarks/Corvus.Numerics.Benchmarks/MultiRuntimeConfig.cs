// <copyright file="MultiRuntimeConfig.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace Corvus.Numerics.Benchmarks;

/// <summary>
/// Configuration for running benchmarks across multiple .NET runtimes.
/// Enables performance comparison between .NET Framework 4.8.1, .NET 8.0, .NET 9.0, and .NET 10.0.
/// </summary>
public class MultiRuntimeConfig : ManualConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MultiRuntimeConfig"/> class.
    /// </summary>
    public MultiRuntimeConfig()
    {
        AddJob(Job.Default
            .WithRuntime(ClrRuntime.Net481)
            .WithId(".NET Framework 4.8.1"));

        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core80)
            .WithId(".NET 8.0"));

        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core90)
            .WithId(".NET 9.0"));

        AddJob(Job.Default
            .WithRuntime(CoreRuntime.Core10_0)
            .WithId(".NET 10.0"));

        AddDiagnoser(MemoryDiagnoser.Default);
    }
}