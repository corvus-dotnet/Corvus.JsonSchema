using Corvus.Text.Json;
using OpenVersusClosedTypes.Models;

// A person with an additional property: "jobRole".
string extendedPersonJsonString =
    """
    {
        "familyName": "Brontë",
        "givenName": "Anne",
        "birthDate": "1820-01-17",
        "height": 1.57,
        "jobRole": "Author"
    }
    """;

Console.WriteLine("We have a person with an extra 'jobRole' property:");
Console.WriteLine(extendedPersonJsonString);

// ------------------------------------------------------------------
// Open type — allows additional properties by default
// ------------------------------------------------------------------
using var parsedPersonOpen = ParsedJsonDocument<PersonOpen>.Parse(extendedPersonJsonString);
PersonOpen personOpen = parsedPersonOpen.RootElement;

Console.WriteLine("Validating as an open type (additional properties allowed):");
if (personOpen.EvaluateSchema())
{
    Console.WriteLine("  personOpen is valid");
}
else
{
    Console.WriteLine("  personOpen is not valid");
}

// ------------------------------------------------------------------
// Closed type — unevaluatedProperties: false forbids extra properties
// ------------------------------------------------------------------
using var parsedPersonClosed = ParsedJsonDocument<PersonClosed>.Parse(extendedPersonJsonString);
PersonClosed personClosed = parsedPersonClosed.RootElement;

Console.WriteLine();
Console.WriteLine("Validating as a closed type (unevaluatedProperties: false):");
if (personClosed.EvaluateSchema())
{
    Console.WriteLine("  personClosed is valid");
}
else
{
    Console.WriteLine("  personClosed is not valid");
}

// ------------------------------------------------------------------
// Fix the entity by removing the undeclared property via the open type
// (the open type supports generic RemoveProperty for arbitrary names)
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var fixedDoc = personOpen.CreateBuilder(workspace);
PersonOpen.Mutable fixedRoot = fixedDoc.RootElement;
fixedRoot.RemoveProperty("jobRole"u8);

Console.WriteLine();
Console.WriteLine("After removing the 'jobRole' property:");
Console.WriteLine($"  {fixedDoc.RootElement}");

// Validate the result as a closed type
PersonClosed personFixed = PersonClosed.From(fixedDoc.RootElement);
Console.WriteLine("Validating the fixed entity as a closed type:");
if (personFixed.EvaluateSchema())
{
    Console.WriteLine("  personFixed is valid");
}
else
{
    Console.WriteLine("  personFixed is not valid");
}

// ------------------------------------------------------------------
// Break the entity by setting an undeclared property
// ------------------------------------------------------------------
using var brokenDoc = personOpen.CreateBuilder(workspace);
PersonOpen.Mutable brokenRoot = brokenDoc.RootElement;
brokenRoot.SetProperty("age"u8, 23);

Console.WriteLine();
Console.WriteLine("After adding an undeclared 'age' property:");
Console.WriteLine($"  {brokenDoc.RootElement}");

PersonClosed personBroken = PersonClosed.From(brokenDoc.RootElement);
Console.WriteLine("Validating as a closed type:");
if (personBroken.EvaluateSchema())
{
    Console.WriteLine("  personBroken is valid");
}
else
{
    Console.WriteLine("  personBroken is not valid");
}