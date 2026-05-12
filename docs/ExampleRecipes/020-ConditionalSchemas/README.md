# JSON Schema Patterns in .NET - Conditional Schemas

This recipe demonstrates how to use JSON Schema's `if`/`then`/`else` keywords for conditional validation, where the required properties change based on the value of another property.

## The Pattern

Many real-world data models have fields that are only required — or only valid — depending on some other field's value. For example, a payment object might require `cardNumber` and `expiry` when the payment method is `"card"`, but `accountNumber` and `sortCode` when the method is `"bank"`.

JSON Schema's `if`/`then`/`else` keywords let you express this conditional logic declaratively:

- **`if`** — a sub-schema that is tested against the instance. If it validates, the `then` schema is applied; otherwise, the `else` schema is applied.
- **`then`** — additional constraints applied when the `if` condition matches.
- **`else`** — additional constraints applied when the `if` condition does not match.

The `if` schema does not contribute validation errors itself — it only determines which branch (`then` or `else`) applies. This means a document that fails the `if` condition is not invalid for that reason alone; it simply takes the `else` path.

## The Schema

File: `payment.json`

```json
{
    "title": "Payment",
    "type": "object",
    "required": ["amount", "method"],
    "properties": {
        "amount": { "type": "number" },
        "method": { "type": "string", "enum": ["card", "bank"] }
    },
    "if": {
        "properties": {
            "method": { "const": "card" }
        },
        "required": ["method"]
    },
    "then": {
        "required": ["cardNumber", "expiry"],
        "properties": {
            "cardNumber": { "type": "string", "pattern": "^[0-9]{16}$" },
            "expiry": { "type": "string", "pattern": "^(0[1-9]|1[0-2])/[0-9]{2}$" }
        }
    },
    "else": {
        "required": ["accountNumber", "sortCode"],
        "properties": {
            "accountNumber": { "type": "string", "pattern": "^[0-9]{8}$" },
            "sortCode": { "type": "string", "pattern": "^[0-9]{6}$" }
        }
    }
}
```

When `method` is `"card"`, the `if` condition matches and the `then` branch requires `cardNumber` and `expiry`. When `method` is `"bank"` (or any other value), the `else` branch requires `accountNumber` and `sortCode`.

## Generated Code Usage

[Example code](./Program.cs)

### Parsing and validating a card payment

```csharp
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

if (validCard.EvaluateSchema())
{
    Console.WriteLine("Validation: PASSED");
}
```

### Parsing and validating a bank payment

```csharp
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

if (validBank.EvaluateSchema())
{
    Console.WriteLine("Validation: PASSED");
}
```

### Detecting validation failures with detailed diagnostics

```csharp
string invalidCardJson = """
    {
        "amount": 19.99,
        "method": "card",
        "expiry": "06/26"
    }
    """;

using var invalidCardDoc = ParsedJsonDocument<Payment>.Parse(invalidCardJson);
Payment invalidCard = invalidCardDoc.RootElement;

if (!invalidCard.EvaluateSchema())
{
    Console.WriteLine("Validation: FAILED");

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
        }
    }
}
```

## Running the Example

```bash
cd docs/ExampleRecipes/020-ConditionalSchemas
dotnet run
```

## Related Patterns

- [002-DataObjectValidation](../002-DataObjectValidation/) - Basic schema validation with constraints and detailed diagnostics
- [012-PatternMatching](../012-PatternMatching/) - Discriminated unions and pattern matching with `oneOf`