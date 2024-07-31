using Benchmarks;

LargeFileBenchmark benchmark = new();
benchmark.GlobalSetup().Wait();

for(int i = 0; i < 50; ++i)
{
    benchmark.PatchCorvus();
}