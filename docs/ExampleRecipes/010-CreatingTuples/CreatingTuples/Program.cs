using Corvus.Json;
using JsonSchemaSample.Api;

// Create a tuple from a dotnet tuple
(int, string, bool) dotnetTuple = (3, "Hello", false);
ThreeTuple threeTuple = dotnetTuple;

// Create a tuple directly
ThreeTuple threeTuple2 = ThreeTuple.Create(3, "Hello", false);

// Parse a JSON array
string threeTupleJson =
    """
    [3, "Hello", false]
    """;

using ParsedValue<ThreeTuple> threeTuple3 = ParsedValue<ThreeTuple>.Parse(threeTupleJson);

if (threeTuple3.Instance == threeTuple)
{
    // The tuples are equal
    Console.WriteLine("The tuples are equal");
}
else
{
    Console.WriteLine("The tuples are not equal");
}

// Access the item values
Console.WriteLine($"{threeTuple.Item1}, {threeTuple.Item2}, {threeTuple.Item3}");

// Convert to a dotnet tuple
(JsonInt32, JsonString, JsonBoolean) tupleFromThreeTuple = threeTuple;

// Automatically convert to a tuple where implicit conversions are available
(int, JsonString, bool) dotnetTupleFromThreeTuple = threeTuple;