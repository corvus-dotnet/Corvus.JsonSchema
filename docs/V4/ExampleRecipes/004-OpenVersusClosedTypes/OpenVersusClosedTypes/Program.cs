using Corvus.Json;
using JsonSchemaSample.Api;

// A person with an additional property: "JobRole".
var extendedPersonJsonString =
    """
    {
        "familyName": "Brontë",
        "givenName": "Anne",
        "birthDate": "1820-01-17",
        "height": 1.57,
        "jobRole": "Author"
    }
    """;

using var parsedPersonOpen = ParsedValue<PersonOpen>.Parse(extendedPersonJsonString);
PersonOpen personOpen = parsedPersonOpen.Instance;

// An object is, by default, open - allowing undeclared properties.
if (personOpen.IsValid())
{
    Console.WriteLine("personOpen is valid");
}
else
{
    Console.WriteLine("personOpen is not valid");
}

using var parsedPersonClosed = ParsedValue<PersonClosed>.Parse(extendedPersonJsonString);
PersonClosed personClosed = parsedPersonClosed.Instance;

// An object with unevaluatedProperties: false does not allow undeclared properties.
if (personClosed.IsValid())
{
    Console.WriteLine("personClosed is valid");
}
else
{
    Console.WriteLine("personClosed is not valid");
}

// We can still use an invalid entity - and fix it; for instance, by removing the invalid property
PersonClosed personFixed = personClosed.RemoveProperty("jobRole"u8);
if (personFixed.IsValid())
{
    Console.WriteLine("personFixed is valid");
}
else
{
    Console.WriteLine("personFixed is not valid");
}

// Equally we can make a valid entity invalid
// Setting an unknown property to a particular value.
PersonClosed personBroken = personFixed.SetProperty<JsonInteger>("age", 23);
if (personBroken.IsValid())
{
    Console.WriteLine("personBroken is valid");
}
else
{
    Console.WriteLine("personBroken is not valid");
}