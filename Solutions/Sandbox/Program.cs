using Corvus.Json.Benchmarking;

ValidateLargeDocument validateLargeDocument = new();
validateLargeDocument.GlobalSetup();

bool valid = validateLargeDocument.ValidateLargeArrayCorvusValidator();

Console.WriteLine(valid);
validateLargeDocument.GlobalCleanup();