using Corvus.Json.Benchmarking;

ValidateLargeDocument validateLargeDocument = new();
validateLargeDocument.GlobalSetup();

for (int i = 0; i < 10; ++i)
{
    bool valid = validateLargeDocument.ValidateLargeArrayCorvusV4();
}

validateLargeDocument.GlobalCleanup();