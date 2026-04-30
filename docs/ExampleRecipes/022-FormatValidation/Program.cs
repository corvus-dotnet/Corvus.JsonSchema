using Corvus.Text.Json;
using System.Text;
using FormatValidation.Models;
using NodaTime;

// ------------------------------------------------------------------
// 1. Parse a valid event — all format properties populated correctly
// ------------------------------------------------------------------
string validJson = """
    {
        "name": "Tech Conference 2025",
        "startTime": "2025-07-15T09:00:00+01:00",
        "date": "2025-07-15",
        "duration": "P3DT2H",
        "website": "https://example.com:8080/conference?year=2025#schedule",
        "relatedLink": "/sessions/keynote",
        "intlHomepage": "https://例え.jp/カンファレンス",
        "intlRelatedLink": "/セッション/基調講演",
        "eventId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
        "hostEmail": "host@example.com"
    }
    """;

using ParsedJsonDocument<Event> validDoc = ParsedJsonDocument<Event>.Parse(validJson);
Event validEvent = validDoc.RootElement;
Console.WriteLine("Parsed valid event:");
Console.WriteLine(validEvent);
Console.WriteLine();

// ------------------------------------------------------------------
// 2. Access strongly-typed values — implicit conversions
// ------------------------------------------------------------------

// startTime (format: date-time) → NodaTime.OffsetDateTime
OffsetDateTime startTime = validEvent.StartTime;
Console.WriteLine($"Start time (OffsetDateTime): {startTime}");

// date (format: date) → NodaTime.LocalDate
LocalDate date = validEvent.Date;
Console.WriteLine($"Date (LocalDate): {date}");

// duration (format: duration) → Corvus.Text.Json.Period
Corvus.Text.Json.Period duration = validEvent.Duration;
Console.WriteLine($"Duration (Period): {duration}");

// eventId (format: uuid) → System.Guid
Guid eventId = validEvent.EventId;
Console.WriteLine($"Event ID (Guid): {eventId}");

// hostEmail (format: email) → access the email string value
Console.WriteLine($"Host email: {(string)validEvent.HostEmail}");
Console.WriteLine();

// ------------------------------------------------------------------
// 3. URI/IRI component access via Utf8UriValue / Utf8IriValue
// ------------------------------------------------------------------

// format: "uri" — explicit cast to Utf8UriValue, then access Utf8Uri components
// Utf8UriValue is IDisposable — always use a using declaration
using (Utf8UriValue uriValue = (Utf8UriValue)validEvent.Website)
{
    Utf8Uri uri = uriValue.Uri;
    Console.WriteLine("URI components (website):");
    Console.WriteLine($"  Scheme:    {Encoding.UTF8.GetString(uri.Scheme)}");
    Console.WriteLine($"  Authority: {Encoding.UTF8.GetString(uri.Authority)}");
    Console.WriteLine($"  Host:      {Encoding.UTF8.GetString(uri.Host)}");
    Console.WriteLine($"  Port:      {Encoding.UTF8.GetString(uri.Port)} (value: {uri.PortValue})");
    Console.WriteLine($"  Path:      {Encoding.UTF8.GetString(uri.Path)}");
    Console.WriteLine($"  Query:     {Encoding.UTF8.GetString(uri.Query)}");
    Console.WriteLine($"  Fragment:  {Encoding.UTF8.GetString(uri.Fragment)}");
    Console.WriteLine($"  GetUri():  {uri.GetUri()}");
}

Console.WriteLine();

// format: "uri-reference" — can be relative (no scheme)
using (Utf8UriReferenceValue uriRefValue = (Utf8UriReferenceValue)validEvent.RelatedLink)
{
    Utf8UriReference uriRef = uriRefValue.UriReference;
    Console.WriteLine("URI-reference components (relatedLink):");
    Console.WriteLine($"  IsRelative: {uriRef.IsRelative}");
    Console.WriteLine($"  Path:       {Encoding.UTF8.GetString(uriRef.Path)}");
}

Console.WriteLine();

// format: "iri" — allows Unicode characters (RFC 3987)
using (Utf8IriValue iriValue = (Utf8IriValue)validEvent.IntlHomepage)
{
    Utf8Iri iri = iriValue.Iri;
    Console.WriteLine("IRI components (intlHomepage):");
    Console.WriteLine($"  Scheme:    {Encoding.UTF8.GetString(iri.Scheme)}");
    Console.WriteLine($"  Host:      {Encoding.UTF8.GetString(iri.Host)}");
    Console.WriteLine($"  Path:      {Encoding.UTF8.GetString(iri.Path)}");
}

Console.WriteLine();

// format: "iri-reference" — relative IRI with Unicode
using (Utf8IriReferenceValue iriRefValue = (Utf8IriReferenceValue)validEvent.IntlRelatedLink)
{
    Utf8IriReference iriRef = iriRefValue.IriReference;
    Console.WriteLine("IRI-reference components (intlRelatedLink):");
    Console.WriteLine($"  IsRelative: {iriRef.IsRelative}");
    Console.WriteLine($"  Path:       {Encoding.UTF8.GetString(iriRef.Path)}");
}

Console.WriteLine();

// TryGetValue — non-throwing alternative to explicit cast
if (validEvent.Website.TryGetValue(out Utf8UriValue tryUri))
{
    Console.WriteLine($"TryGetValue succeeded: {tryUri.Uri.GetUri()}");
    tryUri.Dispose();
}

// Zero-allocation comparison — no need to parse into Utf8UriValue
Console.WriteLine($"Website ValueEquals: {validEvent.Website.ValueEquals("https://example.com:8080/conference?year=2025#schedule")}");
Console.WriteLine();

// ------------------------------------------------------------------
// 4. Validate a well-formed event — passes validation
// ------------------------------------------------------------------
bool isValid = validEvent.EvaluateSchema();
Console.WriteLine($"Valid event passes schema validation: {isValid}");
Console.WriteLine();

// ------------------------------------------------------------------
// 5. Validate a malformed event — bad date-time and bad URI format
// ------------------------------------------------------------------
string malformedJson = """
    {
        "name": "Bad Event",
        "startTime": "not-a-date-time",
        "website": "not a valid uri",
        "relatedLink": 42,
        "intlHomepage": "://missing-scheme"
    }
    """;

using ParsedJsonDocument<Event> malformedDoc = ParsedJsonDocument<Event>.Parse(malformedJson);
Event malformedEvent = malformedDoc.RootElement;

bool isMalformedValid = malformedEvent.EvaluateSchema();
Console.WriteLine($"Malformed event passes schema validation: {isMalformedValid}");

if (!isMalformedValid)
{
    Console.WriteLine("Detailed validation results:");
    Console.WriteLine();

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
            Console.WriteLine();
        }
    }
}

// ------------------------------------------------------------------
// 6. Format-specific validation — semantic format errors
// ------------------------------------------------------------------
string badFormatJson = """
    {
        "name": "Format Errors",
        "startTime": "2025-13-45T99:00:00+01:00",
        "date": "not-a-date",
        "duration": "not-a-duration",
        "website": "://missing-scheme",
        "eventId": "not-a-uuid",
        "hostEmail": "not-an-email"
    }
    """;

using ParsedJsonDocument<Event> badFormatDoc = ParsedJsonDocument<Event>.Parse(badFormatJson);
Event badFormatEvent = badFormatDoc.RootElement;

Console.WriteLine($"Bad format event passes schema validation: {badFormatEvent.EvaluateSchema()}");
Console.WriteLine();

using JsonSchemaResultsCollector formatCollector =
    JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);

badFormatEvent.EvaluateSchema(formatCollector);

Console.WriteLine("Format validation failures:");
foreach (JsonSchemaResultsCollector.Result result in formatCollector.EnumerateResults())
{
    if (!result.IsMatch)
    {
        Console.WriteLine($"  Message:  {result.GetMessageText()}");
        Console.WriteLine($"  Path:     {result.GetDocumentEvaluationLocationText()}");
        Console.WriteLine();
    }
}