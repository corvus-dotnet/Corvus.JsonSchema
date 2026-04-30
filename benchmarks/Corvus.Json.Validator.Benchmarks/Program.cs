// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Corvus.Json.Validator.Benchmarks;
using Perfolizer.Mathematics.OutlierDetection;

ManualConfig config = ManualConfig.CreateEmpty()
    .AddColumnProvider(DefaultColumnProviders.Instance)
    .AddLogger(ConsoleLogger.Default)
    .AddExporter(MarkdownExporter.Default);

config.AddJob(
    Job.Default
        .WithRuntime(CoreRuntime.Core10_0)
        .WithOutlierMode(OutlierMode.RemoveAll)
        .WithStrategy(RunStrategy.Throughput));

BenchmarkRunner.Run([typeof(ValidDocumentBenchmarks), typeof(InvalidDocumentBenchmarks)], config);