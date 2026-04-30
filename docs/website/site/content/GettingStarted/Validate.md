---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Schema Validation"
---
## Schema validation and error reporting

JSON Schema uses a duck-typing model rather than a rigid type system. It describes the *shape* of valid data with constraints like "it must have these properties", "it must match one of these shapes", or "this value must be between 0 and 150". When you construct a generated type from JSON data, you can safely use it through that type **if and only if** the data is valid according to the schema.

Constructing a generated type from invalid JSON does **not** throw — the type is permissive. You can still access the parts of the data that *are* present. This is valuable for error reporting, diagnostics, and self-healing systems where you need to inspect malformed data.

This permissive model is also what makes composition types work. As we saw in the [Composition types and pattern matching](#composition-types-and-pattern-matching) section, a `oneOf` type like `OtherNames` can hold data matching *any* of its variants. The `Match()` method uses schema validation internally to determine which variant the data conforms to, and dispatches accordingly. Without permissive parsing, you wouldn't be able to hold the data in the first place before determining its shape.

## Basic validation

```csharp
bool isValid = person.EvaluateSchema();
```

## Detailed results

Pass a `JsonSchemaResultsCollector` to collect error messages and locations:

```csharp
using JsonSchemaResultsCollector collector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

bool isValid = person.EvaluateSchema(collector);

if (!isValid)
{
    foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
    {
        if (!r.IsMatch)
        {
            Console.WriteLine(
                $"Error at {r.GetDocumentEvaluationLocationText()}: {r.GetMessageText()}");
        }
    }
}
```

## Validation levels

| Level | Description |
|---|---|
| *(no collector)* | Fastest — returns only `bool` |
| `JsonSchemaResultsLevel.Basic` | Records failures with location information, but without error messages |
| `JsonSchemaResultsLevel.Detailed` | Records failures with location information and error messages |
| `JsonSchemaResultsLevel.Verbose` | Records all events — successes, failures, and ignored keywords — with full messages |

## Example: validating input

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse("""{"age":200}""");
Person person = doc.RootElement;

using JsonSchemaResultsCollector collector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

bool isValid = person.EvaluateSchema(collector);
// isValid is false — "name" is required and age exceeds maximum of 150

foreach (JsonSchemaResultsCollector.Result r in collector.EnumerateResults())
{
    if (!r.IsMatch)
    {
        Console.WriteLine(
            $"{r.GetDocumentEvaluationLocationText()}: {r.GetMessageText()}");
    }
}
```

The collector provides:

- **`r.IsMatch`** — whether this individual constraint passed
- **`r.GetMessageText()`** — the error or success message
- **`r.GetDocumentEvaluationLocationText()`** — JSON pointer to the failing location in the document
- **`r.GetSchemaEvaluationLocationText()`** — JSON pointer to the constraint within the schema
