using Corvus.Json;
using Corvus.Json.Benchmarking.Models;

////using var parsedPerson = ParsedValue<Person>.Parse(
////    """
////    {
////        "name": {
////            "familyName": "Oldroyd",
////            "givenName": "Michael",
////            "otherNames": [],
////            "email": "michael.oldryoyd@contoso.com"
////        },
////        "dateOfBirth": "Henry 1944-07-14",
////        "netWorth": 1234567890.1234567891,
////        "height": 1.8
////    }
////    """);


////Person person = parsedPerson.Instance;

Benchmarks.GeneratedBenchmark0 benchmark0 = new();
await benchmark0.GlobalSetup();

for (int i = 0; i < 100; ++i)
{
    benchmark0.PatchCorvus();
}

Thread.Sleep(2000);

for (int i = 0; i < 10000; ++i)
{
    benchmark0.PatchCorvus();
}

////ValidationContext result = person.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);

////if (!result.IsValid)
////{
////    foreach (ValidationResult error in result.Results)
////    {
////        Console.WriteLine(error);
////    }
////}