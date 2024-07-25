using Corvus.Json.Benchmarking;

ValidateSmallDocument benchmark = new();
benchmark.GlobalSetup();


for(int i = 0; i < 5; ++i)
{
    benchmark.ValidateSmallDocumentCorvusV3();
}

Thread.Sleep(500);

for (int i = 0; i < 50; ++i)
{
    benchmark.ValidateSmallDocumentCorvusV3();
}

Thread.Sleep(500);

benchmark.GlobalCleanup();
