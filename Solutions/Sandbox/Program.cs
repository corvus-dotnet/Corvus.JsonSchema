using Corvus.Json.Benchmarking;

ValidateLargeDocument benchmark = new();
benchmark.GlobalSetup().Wait();


for (int i = 0; i < 50; ++i)
{
    benchmark.ValidateLargeArrayCorvusV3();
}

benchmark.GlobalCleanup().Wait();