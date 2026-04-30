using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

// Required fields are not-nullable in the create method
var personConstraints = PersonConstraints.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52,
    // Invalid string length
    otherNames: string.Empty);

// Fast, zero-allocation boolean-only validation
if (!personConstraints.IsValid())
{
    // Detailed validation (best used only when fast validation indicates an invalid condition)
    var validationResults = personConstraints.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);
    foreach (var result in validationResults.Results)
    {
        Console.WriteLine(result);
    }
}

// Conversion to double is available implicitly as it is now constrained by format, and does not allocate.
double heightValue = personConstraints.Height;