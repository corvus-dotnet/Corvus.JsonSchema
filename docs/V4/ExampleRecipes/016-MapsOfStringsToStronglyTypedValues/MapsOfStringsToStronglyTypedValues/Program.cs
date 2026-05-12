using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;
using System.Collections.Frozen;

string jsonMapString =
    """
    {
      "beans": {
        "familyName": "Smith",
        "givenName": "John",
        "otherNames": "Edward,Michael",
        "birthDate": "2004-01-01",
        "height": 1.8
      },
      "bangers": {
        "familyName": "Johnson",
        "givenName": "Alice",
        "birthDate": "2000-02-02",
        "height": 1.6
      },
      "bacon": {
        "familyName": "Williams",
        "givenName": "Robert",
        "otherNames": "James,Thomas",
        "birthDate": "1995-03-03",
        "height": 1.7
      },
      "burgers": {
        "familyName": "Brown",
        "givenName": "Jessica",
        "birthDate": "1990-04-04",
        "height": 1.9
      }
    }
    """;

// Parse a map from JSON
using ParsedValue<MapOfStringToType> parsedMap = ParsedValue<MapOfStringToType>.Parse(jsonMapString);
MapOfStringToType map = parsedMap.Instance;

// Enumerate the strongly typed items
foreach (JsonObjectProperty<PersonClosed> item in map.EnumerateObject())
{
    PersonClosed val = item.Value;
    Console.WriteLine($"{item.Name}: {item.Value}");
}

// Get a known value from the map (throws if not present)
PersonClosed bangers = map["bangers"];

// Try to get a known value from the map
if (map.TryGetProperty("burgers", out PersonClosed burgersPerson))
{
    Console.WriteLine(burgersPerson);
}

// Update the map with a strongly typed value
map = map.SetProperty(
    "fish",
    PersonClosed.Create(new LocalDate(1991, 3, 7), "Irving", "Henry"));

// Construct a map from name/value tuples
var newMap = MapOfStringToType.FromProperties(
    ("one", map["bacon"]),
    ("two", map["fish"]));

// Manipulate a map with LINQ operators
FrozenDictionary<string, string> dictionaryOfStringToString =
    map
        .Where(kvp => kvp.Value.BirthDate > new LocalDate(1995, 1, 1))
        .ToFrozenDictionary(
            kvp => (string)kvp.Key,
            kvp => $"{kvp.Value.FamilyName}, {kvp.Value.GivenName}");