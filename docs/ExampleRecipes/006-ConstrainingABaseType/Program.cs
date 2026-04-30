using Corvus.Text.Json;
using ConstrainingABaseType.Models;
using NodaTime;

// ------------------------------------------------------------------
// Create a tall person — although Anne is not tall enough
// to satisfy the tighter height constraint (minimum: 2.0)
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var tallDoc = PersonTall.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

string json = tallDoc.RootElement.ToString();
Console.WriteLine("Created a person with height 1.57:");
Console.WriteLine($"  {json}");

using var parsedTall = ParsedJsonDocument<PersonTall>.Parse(json);
PersonTall personTall = parsedTall.RootElement;

// ------------------------------------------------------------------
// Validate as the constrained type (PersonTall requires height >= 2.0)
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("Validating as PersonTall (minimum height: 2.0):");
if (personTall.EvaluateSchema())
{
    Console.WriteLine("  personTall is valid");
}
else
{
    Console.WriteLine("  personTall is not valid");
}

// ------------------------------------------------------------------
// Convert to the base type — the less restrictive constraint still passes
// ------------------------------------------------------------------
PersonClosed personClosedFromTall = PersonClosed.From(personTall);

Console.WriteLine();
Console.WriteLine("Converted to the base PersonClosed type:");
Console.WriteLine("Validating as PersonClosed (no minimum height, only maximum: 3.0):");
if (personClosedFromTall.EvaluateSchema())
{
    Console.WriteLine("  personClosedFromTall is valid");
}
else
{
    Console.WriteLine("  personClosedFromTall is not valid");
}