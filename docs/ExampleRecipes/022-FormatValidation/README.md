# JSON Schema Patterns in .NET - Format Validation

This recipe demonstrates how JSON Schema `format` keywords map to strongly-typed .NET values, and how format-specific validation catches semantic errors in string properties.

## The Pattern

The JSON Schema `format` keyword annotates string properties with a specific semantic meaning — such as `date-time`, `date`, `duration`, `uri`, `uuid`, or `email`. The Corvus.Text.Json code generator maps these formats to strongly-typed .NET accessors:

- Date/time formats produce implicit conversions to [NodaTime](https://nodatime.org/) types, giving you type-safe, zero-allocation access to temporal values.
- URI and IRI formats generate types with explicit conversions to `Utf8UriValue`, `Utf8UriReferenceValue`, `Utf8IriValue`, and `Utf8IriReferenceValue` — giving you zero-allocation access to parsed URI/IRI components.
- UUID and email formats generate validated string types with implicit conversion to `System.Guid` and explicit conversion to `string`, respectively.

When you call `EvaluateSchema()`, the generated code validates not just the JSON structure but also the *content* of each formatted string — rejecting values like `"not-a-date"` for a `date` field or `"://missing-scheme"` for a `uri` field.

### Format assertion vs annotation

In JSON Schema Draft 2020-12, the `format` keyword is an *annotation* by default — it describes the expected format but does not cause validation to fail if the value doesn't match. Corvus.Text.Json overrides this default and **enables format assertion** for all schema drafts, so `format` is validated by default.

If you need the standard annotation-only behaviour, you can disable format assertion via code generation options:

- **Source Generator:** set `<CorvusTextJsonAlwaysAssertFormat>false</CorvusTextJsonAlwaysAssertFormat>` in the `.csproj` file.
- **CLI tool (`generatejsonschematypes`):** pass the `--assertFormat false` flag

## Format Mapping

| JSON Schema Format | .NET Type | Notes |
|---|---|---|
| `date-time` | `NodaTime.OffsetDateTime` | Full date-time with timezone offset (RFC 3339) |
| `date` | `NodaTime.LocalDate` | Calendar date without time |
| `duration` | `Corvus.Text.Json.Period` | ISO 8601 duration (implicit conversion from `NodaTime.Period`) |
| `uri` | `Utf8UriValue` → `Utf8Uri` | Absolute URI (RFC 3986); zero-allocation component access |
| `uri-reference` | `Utf8UriReferenceValue` → `Utf8UriReference` | Absolute or relative URI (RFC 3986) |
| `iri` | `Utf8IriValue` → `Utf8Iri` | Absolute IRI with Unicode support (RFC 3987) |
| `iri-reference` | `Utf8IriReferenceValue` → `Utf8IriReference` | Absolute or relative IRI (RFC 3987) |
| `uuid` | `System.Guid` | Implicit conversion to `Guid` |
| `email` | String with email validation | Access via explicit string conversion |

## URI and IRI Types

For `uri`, `uri-reference`, `iri`, and `iri-reference` formats, the generated type exposes two access patterns:

### Explicit cast to `Utf8*Value`

The `Utf8UriValue`, `Utf8UriReferenceValue`, `Utf8IriValue`, and `Utf8IriReferenceValue` types are `IDisposable` wrappers that may rent memory from the `ArrayPool`. Always use them in a `using` declaration:

```csharp
using (Utf8UriValue uriValue = (Utf8UriValue)validEvent.Website)
{
    Utf8Uri uri = uriValue.Uri;
    Console.WriteLine(Encoding.UTF8.GetString(uri.Scheme));    // "https"
    Console.WriteLine(Encoding.UTF8.GetString(uri.Host));      // "example.com"
    Console.WriteLine(Encoding.UTF8.GetString(uri.Path));      // "/conference"
    Console.WriteLine(Encoding.UTF8.GetString(uri.Query));     // "year=2025"
    Console.WriteLine(Encoding.UTF8.GetString(uri.Fragment));  // "schedule"
}
```

The inner `Utf8Uri` (and its variants) is a `readonly ref struct` that provides zero-allocation access to all URI components via `ReadOnlySpan<byte>` properties:

- `Scheme`, `Authority`, `User`, `Host`, `Port`, `Path`, `Query`, `Fragment`
- `HasScheme`, `HasAuthority`, `HasHost`, `HasPort`, `HasPath`, `HasQuery`, `HasFragment`
- `IsRelative`, `IsValid`, `IsDefaultPort`, `PortValue`
- `GetUri()` — converts to `System.Uri` (allocating)
- `TryFormatDisplay()` / `TryFormatCanonical()` — write to a `Span<byte>` buffer
- `TryApply()` — resolves a URI reference against this base URI (RFC 3986 §5.2)
- `TryMakeRelative()` — creates a relative reference from two absolute URIs

### TryGetValue — non-throwing alternative

If the JSON value might not be a valid URI, use the non-throwing `TryGetValue` pattern:

```csharp
if (validEvent.Website.TryGetValue(out Utf8UriValue uriValue))
{
    // uriValue is valid — use it
    uriValue.Dispose();
}
```

### ValueEquals — zero-allocation comparison

For simple equality checks you don't need to parse into `Utf8UriValue` at all:

```csharp
bool match = validEvent.Website.ValueEquals("https://example.com/conference");
```

## The Schema

File: `event.json`

```json
{
    "title": "Event",
    "type": "object",
    "required": ["name", "startTime"],
    "properties": {
        "name": { "type": "string" },
        "startTime": { "type": "string", "format": "date-time" },
        "date": { "type": "string", "format": "date" },
        "duration": { "type": "string", "format": "duration" },
        "website": { "type": "string", "format": "uri" },
        "relatedLink": { "type": "string", "format": "uri-reference" },
        "intlHomepage": { "type": "string", "format": "iri" },
        "intlRelatedLink": { "type": "string", "format": "iri-reference" },
        "eventId": { "type": "string", "format": "uuid" },
        "hostEmail": { "type": "string", "format": "email" }
    }
}
```

The generated .NET properties are:

- `Name` — of type `Event.NameEntity` (a string type)
- `StartTime` — of type `Event.StartTimeEntity` (a date-time-formatted string type, with implicit conversion to `NodaTime.OffsetDateTime`)
- `Date` — of type `Event.DateEntity` (a date-formatted string type, with implicit conversion to `NodaTime.LocalDate`)
- `Duration` — of type `Event.DurationEntity` (a duration-formatted string type, with implicit conversion to `Corvus.Text.Json.Period`)
- `Website` — of type `Event.WebsiteEntity` (a URI-formatted string type, with explicit conversion to `Utf8UriValue`)
- `RelatedLink` — of type `Event.RelatedLinkEntity` (a URI-reference-formatted string type, with explicit conversion to `Utf8UriReferenceValue`)
- `IntlHomepage` — of type `Event.IntlHomepageEntity` (an IRI-formatted string type, with explicit conversion to `Utf8IriValue`)
- `IntlRelatedLink` — of type `Event.IntlRelatedLinkEntity` (an IRI-reference-formatted string type, with explicit conversion to `Utf8IriReferenceValue`)
- `EventId` — of type `Event.EventIdEntity` (a UUID-formatted string type, with implicit conversion to `System.Guid`)
- `HostEmail` — of type `Event.HostEmailEntity` (an email-formatted string type)

## Generated Code Usage

[Example code](./Program.cs)

### Parsing and accessing strongly-typed values

```csharp
using ParsedJsonDocument<Event> validDoc = ParsedJsonDocument<Event>.Parse(validJson);
Event validEvent = validDoc.RootElement;

// Implicit conversion to NodaTime types (zero-allocation)
OffsetDateTime startTime = validEvent.StartTime;
LocalDate date = validEvent.Date;
Corvus.Text.Json.Period duration = validEvent.Duration;

// Implicit conversion to Guid for UUID
Guid eventId = validEvent.EventId;
string email = (string)validEvent.HostEmail;
```

### Accessing URI/IRI components

```csharp
// Explicit cast to Utf8UriValue — always use a using declaration
using (Utf8UriValue uriValue = (Utf8UriValue)validEvent.Website)
{
    Utf8Uri uri = uriValue.Uri;
    Console.WriteLine($"Scheme: {Encoding.UTF8.GetString(uri.Scheme)}");  // "https"
    Console.WriteLine($"Host:   {Encoding.UTF8.GetString(uri.Host)}");    // "example.com"
    Console.WriteLine($"Path:   {Encoding.UTF8.GetString(uri.Path)}");    // "/conference"
    Console.WriteLine($"Full:   {uri.GetUri()}");                         // System.Uri
}

// URI-references can be relative (no scheme)
using (Utf8UriReferenceValue uriRefValue = (Utf8UriReferenceValue)validEvent.RelatedLink)
{
    Console.WriteLine($"IsRelative: {uriRefValue.UriReference.IsRelative}");
}

// IRIs allow Unicode characters (RFC 3987)
using (Utf8IriValue iriValue = (Utf8IriValue)validEvent.IntlHomepage)
{
    Console.WriteLine($"Host: {Encoding.UTF8.GetString(iriValue.Iri.Host)}");  // "例え.jp"
}
```

### Schema validation with format checking

```csharp
// Fast boolean-only validation
bool isValid = validEvent.EvaluateSchema();

// Detailed validation with results collector
using JsonSchemaResultsCollector collector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

malformedEvent.EvaluateSchema(collector);

foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
{
    if (!result.IsMatch)
    {
        Console.WriteLine($"  Message:  {result.GetMessageText()}");
        Console.WriteLine($"  Path:     {result.GetDocumentEvaluationLocationText()}");
        Console.WriteLine($"  Schema:   {result.GetSchemaEvaluationLocationText()}");
    }
}
```

Format validation catches semantic errors — for example, `"not-a-date-time"` for a `date-time` field, `"://missing-scheme"` for a `uri` field, or `"not-a-uuid"` for a `uuid` field.

## Running the Example

```bash
cd docs/ExampleRecipes/022-FormatValidation
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) - Defining simple data objects with primitive and formatted properties
- [002-DataObjectValidation](../002-DataObjectValidation/) - Adding validation constraints to data objects