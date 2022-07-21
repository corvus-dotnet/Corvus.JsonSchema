// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Mathematics.OutlierDetection;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAllJoined(
        ManualConfig.Create(DefaultConfig.Instance)
        .AddJob(Job.Dry
            .WithRuntime(CoreRuntime.Core60)
            .WithOutlierMode(OutlierMode.RemoveAll)
            .WithStrategy(RunStrategy.Throughput)
            .WithIterationCount(1)));