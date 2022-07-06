using Benchmarks;

try
{
    var bench = new GeneratedBenchmark64();
    await bench.GlobalSetup().ConfigureAwait(false);

    // Warmup
    bench.PatchCorvus();


    for (int i = 0; i < 32768; ++i)
    {
        bench.PatchCorvus();
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}