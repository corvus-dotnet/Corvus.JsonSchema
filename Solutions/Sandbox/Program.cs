using System;
using System.Diagnostics;
using Benchmarks;
using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;

try
{
    var bench = new GeneratedBenchmark0();
    await bench.GlobalSetup().ConfigureAwait(false);
    bench.PatchCorvus();
    bench.PatchJsonEverything();
}
catch(Exception ex)
{
    Console.WriteLine(ex.ToString());
    throw;
}