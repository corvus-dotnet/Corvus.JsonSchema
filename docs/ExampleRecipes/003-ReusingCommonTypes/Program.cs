using Corvus.Text.Json;
using ReusingCommonTypes.Models;
using NodaTime;

// ------------------------------------------------------------------
// Create a PersonCommonSchema instance
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = PersonCommonSchema.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    otherNames: string.Empty,
    height: 1.57);

// Parse to get an immutable instance for property access
string json = personDoc.RootElement.ToString();
Console.WriteLine(json);

using var parsedDoc = ParsedJsonDocument<PersonCommonSchema>.Parse(json);
PersonCommonSchema personCommonSchema = parsedDoc.RootElement;

// ------------------------------------------------------------------
// Shared types via $ref — familyName, givenName, and otherNames all
// share the same ConstrainedString type defined in $defs
// ------------------------------------------------------------------
PersonCommonSchema.ConstrainedString constrainedGivenName = personCommonSchema.GivenName;
PersonCommonSchema.ConstrainedString constrainedFamilyName = personCommonSchema.FamilyName;

// Explicit cast to string
string cgn = (string)constrainedGivenName;
string cfn = (string)constrainedFamilyName;
Console.WriteLine($"Given name: {cgn}");
Console.WriteLine($"Family name: {cfn}");

// ------------------------------------------------------------------
// Low-allocation comparisons — available on the shared type
// ------------------------------------------------------------------
bool namesEqual = constrainedGivenName.Equals(constrainedFamilyName);
Console.WriteLine($"Given name equals family name: {namesEqual}");

Console.WriteLine($"Given name ValueEquals \"Hello\": {constrainedGivenName.ValueEquals("Hello")}");
Console.WriteLine($"Given name ValueEquals \"Anne\"u8: {constrainedGivenName.ValueEquals("Anne"u8)}");