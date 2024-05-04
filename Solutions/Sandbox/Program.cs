using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

var test = Test.Create(
    32,
    "Hello",
    new OffsetDateTime(
        new LocalDateTime(1973, 02, 14, 14, 32),
        Offset.Zero));

(JsonInt32, JsonString, JsonDateTime) foo = test;
(int, JsonString, OffsetDateTime) foo2 = test;
(int, string, OffsetDateTime) foo3 = (test.Item1, (string)test.Item2, test.Item3);

JsonInt32 item1 = test.Item1;
JsonString item2 = test.Item2;
JsonDateTime item3 = test.Item3;

Test t = foo; 
Test t2 = foo2;