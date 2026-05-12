using Corvus.Text.Json;
using DataObjectValidation.Models;
using NodaTime;

// ------------------------------------------------------------------
// Creating a PersonConstraints with an invalid otherNames (empty string
// violates minLength: 1)
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = PersonConstraints.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52,
    otherNames: string.Empty);
PersonConstraints personConstraints = personDoc.RootElement;

// ------------------------------------------------------------------
// Fast, zero-allocation boolean-only validation
// ------------------------------------------------------------------
if (!personConstraints.EvaluateSchema())
{
    Console.WriteLine("Schema validation failed. Getting detailed results...");
    Console.WriteLine();

    // ------------------------------------------------------------------
    // Detailed validation — use a results collector for diagnostics
    // (best used only when fast validation indicates an invalid condition)
    // ------------------------------------------------------------------
    using JsonSchemaResultsCollector collector =
        JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

    personConstraints.EvaluateSchema(collector);

    foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
    {
        if (!result.IsMatch)
        {
            Console.WriteLine($"  Message:  {result.GetMessageText()}");
            Console.WriteLine($"  Path:     {result.GetDocumentEvaluationLocationText()}");
            Console.WriteLine($"  Schema:   {result.GetSchemaEvaluationLocationText()}");
            Console.WriteLine();
        }
    }
}

// ------------------------------------------------------------------
// Property access — constrained types allow implicit conversions
// ------------------------------------------------------------------

// Height has format: "double", so implicit conversion to double is available
double heightValue = personConstraints.Height;
Console.WriteLine($"Height: {heightValue}");

// Required properties are always present (they are in the "required" array)
string familyName = (string)personConstraints.FamilyName;
Console.WriteLine($"Family name: {familyName}");