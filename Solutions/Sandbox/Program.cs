using Corvus.Json.Benchmarking;

ValidateLargeDocument doc = new();
await doc.GlobalSetup();

for(int i = 0; i < 20; ++i)
{
    doc.ValidateLargeArrayCorvus();
}

await Task.Delay(2000);

for (int i = 0; i < 20; ++i)
{
    doc.ValidateLargeArrayCorvus();
}

await Task.Delay(2000);
