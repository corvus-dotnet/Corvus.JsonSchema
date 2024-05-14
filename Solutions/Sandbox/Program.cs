using Corvus.Json;
using Corvus.Json.Benchmarking.Models;
using JsonSchemaSample.Api;
using NodaTime;

var test = Test.Create(
    32,
    "Hello",
    new OffsetDateTime(
        new LocalDateTime(1973, 02, 14, 14, 32),
        Offset.Zero));

(Test.PositiveInt32, JsonString, JsonDateTime) foo = test;
(int, JsonString, OffsetDateTime) foo2 = test;
(int, string, OffsetDateTime) foo3 = (test.Item1, (string)test.Item2, test.Item3);

Test.PositiveInt32 item1 = test.Item1;
JsonString item2 = test.Item2;
JsonDateTime item3 = test.Item3;

Test t = foo; 
Test t2 = foo2;

PersonArray people =
    [
        Person.Create(PersonName.Create("Lovelace", "Ada")),
        Person.Create(PersonName.Create("Gates", "Bill")),
    ];

JsonArray arrayTest = [1, "hello", (JsonDateTime)OffsetDateTime.FromDateTimeOffset(DateTimeOffset.Now)];