using Corvus.Text.Json;
using ExtendingABaseType.Models;
using NodaTime;

// ------------------------------------------------------------------
// Create a wealthy person — has all the PersonOpen properties plus "wealth"
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var wealthyDoc = PersonWealthy.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    wealth: 1_000_000,
    height: 1.57);

Console.WriteLine("Created a wealthy person:");
Console.WriteLine($"  {wealthyDoc.RootElement}");

// Wealth has format: "int32", so implicit conversion is available
int wealth = wealthyDoc.RootElement.Wealth;
Console.WriteLine($"  Wealth: {wealth}");

// ------------------------------------------------------------------
// Convert to the base type using From()
// ------------------------------------------------------------------
string json = wealthyDoc.RootElement.ToString();
using var parsedWealthy = ParsedJsonDocument<PersonWealthy>.Parse(json);
PersonWealthy wealthyPerson = parsedWealthy.RootElement;

PersonOpen basePerson = PersonOpen.From(wealthyPerson);

Console.WriteLine();
Console.WriteLine("Converted to the base PersonOpen type:");
Console.WriteLine($"  {basePerson}");

// ------------------------------------------------------------------
// Access the "wealth" property via TryGetProperty on the base type
// (the property exists in the JSON but is not declared on PersonOpen)
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("Accessing 'wealth' on the base type via TryGetProperty:");

if (basePerson.TryGetProperty("wealth"u8, out JsonElement wealthElement))
{
    Console.WriteLine($"  Found wealth: {wealthElement.GetInt32()}");
}

// We can also use a string property name
if (basePerson.TryGetProperty("wealth", out JsonElement wealthElement2))
{
    Console.WriteLine($"  Found wealth (via string): {wealthElement2.GetInt32()}");
}