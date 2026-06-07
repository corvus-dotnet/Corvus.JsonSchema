// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Running;
using Corvus.Text.Json.Arazzo.Benchmarks;

if (args.Length > 0 && args[0] == "bdn")
{
    BenchmarkRunner.Run<ArazzoRuntimeBenchmarks>();
    return;
}

// Fast allocation probe: measures bytes allocated per call on the per-evaluation hot path.
var benchmarks = new ArazzoRuntimeBenchmarks();
benchmarks.Setup();

Console.WriteLine("Arazzo runtime per-call allocation (bytes/op):");
Measure("ResolveValue", benchmarks.ResolveValue);
Measure("SimpleNumeric", benchmarks.SimpleNumeric);
Measure("SimpleString", benchmarks.SimpleString);
Measure("JsonPath", benchmarks.JsonPath);
Measure("Regex", benchmarks.Regex);
Measure("Interpolate", benchmarks.Interpolate);

benchmarks.Cleanup();

static void Measure(string name, Func<bool> op)
{
    const int iterations = 200_000;

    for (int i = 0; i < 2_000; i++)
    {
        _ = op();
    }

    GC.Collect();
    GC.WaitForPendingFinalizers();
    GC.Collect();

    long before = GC.GetAllocatedBytesForCurrentThread();
    bool sink = false;
    for (int i = 0; i < iterations; i++)
    {
        sink ^= op();
    }

    long after = GC.GetAllocatedBytesForCurrentThread();
    double perOp = (after - before) / (double)iterations;
    Console.WriteLine($"  {name,-14} {perOp,8:0.00} B/op   (sink={sink})");
}
