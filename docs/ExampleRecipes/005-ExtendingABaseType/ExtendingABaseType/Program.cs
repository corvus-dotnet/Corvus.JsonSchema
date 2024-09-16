using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

// A wealthy person has an additional required property: "Wealth".
var wealthyPerson = PersonWealthy.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    wealth: 1_000_000,
    height: 1.57);

Console.WriteLine($"Wealth: {wealthyPerson.Wealth}");

// Implicit conversion to the type on which it is based (PersonOpen) is available.
PersonOpen basePerson = wealthyPerson;

// Although the property is not present on the PersonOpen dotnet type, it is still available
// through the generic TryGetProperty() API. Note that we can use the JsonPropertyNames declared
// on PersonWealthy to get the appropriate name. We know this is backed by a dotnet type, so it is
// marginally more efficient to use the string version of the property name. If we were backed by a
// JsonElement, it would be marginally more efficient to use the UTF8 property name.
if (basePerson.TryGetProperty(PersonWealthy.JsonPropertyNames.Wealth, out JsonInt32 baseWealth))
{
    // We will only get here if baseWealth is not undefined
    // We know our PersonOpen was derived from a valid
    // PersonWealthy so we do not need to re-validate; if it was
    // present, then it is known to be an int32.
    Console.WriteLine($"Wealth: {(int)baseWealth}");
}

// Or we could just use a literal string as the property name.
if (basePerson.TryGetProperty("wealth", out JsonInt32 baseWealth2))
{
    Console.WriteLine($"Wealth: {(int)baseWealth2}");
}

// Or a literal utf8 string as the property name.
if (basePerson.TryGetProperty("wealth"u8, out JsonInt32 baseWealth3))
{
    Console.WriteLine($"Wealth: {(int)baseWealth3}");
}