Corvus.Json.Benchmarking.ValidateLargeDocument benchmark = new();

benchmark.GlobalSetup();

for (int i = 0; i < 10; ++i)
{
    benchmark.ValidateLargeArrayCorvusV4();
}

Microsoft.DiagnosticsHub.UserMarkRange r2 = new("V4", "Corvus V4");

for (int i = 0; i < 10; ++i)
{
    benchmark.ValidateLargeArrayCorvusV4();
}

r2.Dispose();
