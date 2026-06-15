// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Corvus.Text.Json.Arazzo.Durability.Benchmarks;

ManualConfig config = ManualConfig.CreateMinimumViable()
    .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
    .AddDiagnoser(MemoryDiagnoser.Default);

BenchmarkRunner.Run([typeof(DocumentMaterializationBenchmarks), typeof(WriteToStreamBenchmarks), typeof(RunWriteBenchmarks), typeof(EnvelopeWriteBenchmarks), typeof(CheckpointSerializeBenchmarks), typeof(TagSetBenchmarks), typeof(SourceSetBenchmarks), typeof(CosmosReadBenchmark), typeof(CheckpointDeserializeBenchmarks), typeof(PooledDocumentListBenchmark), typeof(CancelRewriteBenchmark)], config);