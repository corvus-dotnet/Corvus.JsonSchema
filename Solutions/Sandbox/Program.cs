using Benchmarks;

try
{
    var bench = new GeneratedBenchmark2();
    await bench.GlobalSetup().ConfigureAwait(false);

    // Warmup
    bench.PatchCorvus();

    // Give us a nice big gap
    Task.Delay(1000).Wait();

    for (int i = 0; i < 32768; ++i)
    {
        bench.PatchCorvus();
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}