// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json;
using JsonParsingBenchmarks;
using Microsoft.DiagnosticsHub;

var benchmark = new BenchmarkParseObjectWithoutPropertyMapBacking();

for (int i = 0; i < 100000; i++)
{
    benchmark.ParseObjectToCorvusJsonElement();
}


Thread.Sleep(1000);

JsonValueKind kind = default;

var range = new UserMarkRange("System.Text.Json Benchmark");

for (int i = 0; i < 100000; i++)
{
    kind = benchmark.ParseObjectToCorvusJsonElement();
}

range.Dispose();

Thread.Sleep(1000);

if (kind == JsonValueKind.Object)
{
    Console.WriteLine("OK");
}
else
{
    Console.WriteLine("Not OK");
}