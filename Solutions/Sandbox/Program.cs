////using Corvus.Json;
////using JsonSchemaSample.Api;

////Console.WriteLine("Hello world.");

////var matthew =
////    Person.Create(
////        name: PersonName.Create(
////            givenName: "Matthew",
////            familyName: "Adams",
////            otherNames: "William"),
////        dateOfBirth: "1973-02-14");

////var michael =
////    Person.Create(
////        name: PersonName.Create(
////            givenName: "Michael",
////            familyName: "Adams",
////            otherNames: OtherNames.FromItems("Francis", "James")),
////        dateOfBirth: "not valid");

////Console.WriteLine(matthew);
////Console.WriteLine($"matthew.IsValid(): {matthew.IsValid()}");
////Console.WriteLine(michael);
////Console.WriteLine($"michael.IsValid: {michael.IsValid()}");


////if (michael.Name.OtherNames.TryGetAsPersonNameElement(out PersonNameElement result))
////{
////    Console.WriteLine($"It was an item: {result}");
////}

////if (michael.Name.OtherNames.TryGetAsPersonNameElementArray(out PersonNameElementArray arrayResult))
////{
////    Console.WriteLine($"It was an array: {arrayResult}");
////}

var bench = new Benchmarks.GeneratedBenchmark5();
await bench.GlobalSetup();

for (int i = 0; i < 10000; ++i)
{
    bench.PatchCorvus();
}