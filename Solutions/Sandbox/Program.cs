using Benchmarks;

GeneratedBenchmark47 b = new();
await b.GlobalSetup();

await Task.Delay(2000);

b.PatchCorvus();