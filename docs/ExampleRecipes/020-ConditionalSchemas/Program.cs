using Corvus.Text.Json;
using ConditionalSchemas.Models;

// ------------------------------------------------------------------
// Valid card payment
// ------------------------------------------------------------------
string validCardJson = """
    {
        "amount": 49.99,
        "method": "card",
        "cardNumber": "4111111111111111",
        "expiry": "12/25"
    }
    """;

using var validCardDoc = ParsedJsonDocument<Payment>.Parse(validCardJson);
Payment validCard = validCardDoc.RootElement;

Console.WriteLine("=== Valid Card Payment ===");
Console.WriteLine(validCard);

if (validCard.EvaluateSchema())
{
    Console.WriteLine("Validation: PASSED");
}
else
{
    Console.WriteLine("Validation: FAILED");
}

Console.WriteLine();

// ------------------------------------------------------------------
// Valid bank payment
// ------------------------------------------------------------------
string validBankJson = """
    {
        "amount": 250.00,
        "method": "bank",
        "accountNumber": "12345678",
        "sortCode": "112233"
    }
    """;

using var validBankDoc = ParsedJsonDocument<Payment>.Parse(validBankJson);
Payment validBank = validBankDoc.RootElement;

Console.WriteLine("=== Valid Bank Payment ===");
Console.WriteLine(validBank);

if (validBank.EvaluateSchema())
{
    Console.WriteLine("Validation: PASSED");
}
else
{
    Console.WriteLine("Validation: FAILED");
}

Console.WriteLine();

// ------------------------------------------------------------------
// Invalid card payment — missing cardNumber
// ------------------------------------------------------------------
string invalidCardJson = """
    {
        "amount": 19.99,
        "method": "card",
        "expiry": "06/26"
    }
    """;

using var invalidCardDoc = ParsedJsonDocument<Payment>.Parse(invalidCardJson);
Payment invalidCard = invalidCardDoc.RootElement;

Console.WriteLine("=== Invalid Card Payment (missing cardNumber) ===");
Console.WriteLine(invalidCard);

if (!invalidCard.EvaluateSchema())
{
    Console.WriteLine("Validation: FAILED (as expected)");
    Console.WriteLine();

    // ------------------------------------------------------------------
    // Show validation results — detailed diagnostics
    // ------------------------------------------------------------------
    using JsonSchemaResultsCollector collector =
        JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

    invalidCard.EvaluateSchema(collector);

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
// Invalid bank payment — missing sortCode
// ------------------------------------------------------------------
string invalidBankJson = """
    {
        "amount": 500.00,
        "method": "bank",
        "accountNumber": "87654321"
    }
    """;

using var invalidBankDoc = ParsedJsonDocument<Payment>.Parse(invalidBankJson);
Payment invalidBank = invalidBankDoc.RootElement;

Console.WriteLine("=== Invalid Bank Payment (missing sortCode) ===");
Console.WriteLine(invalidBank);

if (!invalidBank.EvaluateSchema())
{
    Console.WriteLine("Validation: FAILED (as expected)");
    Console.WriteLine();

    // ------------------------------------------------------------------
    // Show validation results — detailed diagnostics
    // ------------------------------------------------------------------
    using JsonSchemaResultsCollector collector =
        JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

    invalidBank.EvaluateSchema(collector);

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