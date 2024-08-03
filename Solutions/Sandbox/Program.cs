Benchmarks.LargeFileBenchmark benchmark = new();

await benchmark.GlobalSetup();

for (int i = 0; i < 5; ++i)
{
    benchmark.PatchCorvus();
}

Microsoft.DiagnosticsHub.UserMarkRange r2 = new("V4", "Corvus V4");

for (int i = 0; i < 10; ++i)
{
    benchmark.PatchCorvus();
}

r2.Dispose();
