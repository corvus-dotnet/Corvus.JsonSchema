using Corvus.Json;
using Corvus.Json.Benchmarking.Models;
using JsonSchemaSample.Api;
using NodaTime;

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

var test = Test.Create(32, "Hello", new OffsetDateTime(new LocalDateTime(1973, 02, 14, 14, 32), Offset.Zero));
(JsonInt32, JsonString, JsonDateTime) foo = test;
(int, JsonString, OffsetDateTime) foo2 = test;

JsonInt32 item1 = test.Item1;
JsonString item2 = test.Item2;
JsonDateTime item3 = test.Item3;

Test t = foo; 
Test t2 = foo2;