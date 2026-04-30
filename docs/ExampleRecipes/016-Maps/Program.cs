using Corvus.Text.Json;
using Maps.Models;

// Parse a map (object with additionalProperties)
string json = """
    {
      "foo": 1,
      "bar": 2,
      "baz": 3
    }
    """;

using var parsed = ParsedJsonDocument<StringToIntMap>.Parse(json);
StringToIntMap map = parsed.RootElement;
Console.WriteLine("Parsed map:");
Console.WriteLine(map);
Console.WriteLine();

// Access map values using TryGetProperty
Console.WriteLine("Accessing individual values:");
if (map.TryGetProperty("foo"u8, out var fooValue))
{
    Console.WriteLine($"  foo = {fooValue}");
}

if (map.TryGetProperty("bar"u8, out var barValue))
{
    Console.WriteLine($"  bar = {barValue}");
}
Console.WriteLine();

// Enumerate all entries
Console.WriteLine("All entries:");
foreach (var property in map.EnumerateObject())
{
    Console.WriteLine($"  {property.Name} = {property.Value}");
}
Console.WriteLine();

// Build a new map using the mutable builder
Console.WriteLine("Building a new map:");
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = StringToIntMap.CreateBuilder(workspace);
StringToIntMap.Mutable newMap = builder.RootElement;

// Add properties to the map
newMap.SetProperty("alpha"u8, 10);
newMap.SetProperty("beta"u8, 20);
newMap.SetProperty("gamma"u8, 30);

Console.WriteLine(newMap);
Console.WriteLine();

// Mutate an existing map by creating a builder from it
Console.WriteLine("Mutating the original map:");
using var mutatedBuilder = map.CreateBuilder(workspace);
StringToIntMap.Mutable mutableMap = mutatedBuilder.RootElement;

// Update existing property
mutableMap.SetProperty("foo"u8, 100);

// Add new property
mutableMap.SetProperty("qux"u8, 4);

// Remove a property
mutableMap.RemoveProperty("bar"u8);

Console.WriteLine(mutableMap);