# Parsing & Reading JSON

## Overview

`ParsedJsonDocument<T>` is a lightweight, read-only representation of a JSON value that utilizes pooled memory to minimize garbage collector impact. It provides an efficient way to parse and navigate JSON documents with minimal allocations.

Built on top of `Corvus.Text.Json`, which is derived from `System.Text.Json`, `ParsedJsonDocument<T>` extends the functionality while maintaining a high degree of source compatibility with the familiar `System.Text.Json.JsonElement` API.

## Key Features

- **Memory Efficient**: Uses pooled memory from `ArrayPool<byte>` to reduce GC pressure
- **Read-Only**: Provides immutable access to parsed JSON data
- **Multiple Input Sources**: Supports parsing from strings, byte arrays, streams, and sequences
- **IDisposable**: Must be disposed to return pooled memory
- **System.Text.Json Compatible**: Works with familiar `System.Text.Json` patterns

## Comparison with System.Text.Json

`ParsedJsonDocument<T>` is based on `System.Text.Json.JsonDocument` but provides several enhancements:

| Feature | System.Text.Json (`JsonDocument`) | Corvus.Text.Json (`ParsedJsonDocument<T>`) |
|---------|-----------------------------------|-------------------------------------------|
| **Generic Support** | Fixed to `JsonElement` | Generic over `IJsonElement<T>` for extensibility |
| **Property Access** | Sequential search-based property lookup | Optionally uses a property map with O(1) performance for repeated access |
| **Additional Types** | Standard .NET types | Includes `BigNumber`, `BigInteger`, NodaTime types (`LocalDate`, `OffsetTime`, `OffsetDate`, `OffsetDateTime`, `Period`) |
| **Mutability** | Read-only | Read-only by default, with mutable variant (`JsonElement.Mutable`) available via `JsonDocumentBuilder` |
| **UTF-8 Support** | Good | Enhanced with direct UTF-8 property access methods |

### When to Use ParsedJsonDocument

- ✅ When you need JSON schema support
- ✅ When you need mutability support
- ✅ When you want to create custom JSON element types
- ✅ When you need extended type support (BigInteger, BigNumber, NodaTime types)

## Basic Usage

### Parsing JSON from a String

```csharp
using Corvus.Text.Json;

string json = """
    {
        "name": "John",
        "age": 30
    }
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

JsonElement root = doc.RootElement;
string name = root.GetProperty("name"u8).GetString();
int age = root.GetProperty("age"u8).GetInt32();

Console.WriteLine($"Name: {name}, Age: {age}");
```

### Parsing JSON from UTF-8 Bytes

```csharp
ReadOnlySpan<byte> utf8Json = """
    {
        "message": "Hello, World!"
    }
    """u8;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(utf8Json.ToArray().AsMemory());

JsonElement root = doc.RootElement;
string message = root.GetProperty("message"u8).GetString();

Console.WriteLine(message); // Output: Hello, World!
```

### Parsing JSON from a Stream

```csharp
using FileStream fileStream = File.OpenRead("data.json");
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(fileStream);

JsonElement root = doc.RootElement;
// Process the JSON data...
```

### Async Stream Parsing

Asynchronous parsing is ideal for large files or network streams:

```csharp
using FileStream fileStream = File.OpenRead("large-data.json");
using ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(fileStream);

JsonElement root = doc.RootElement;
// Process the JSON data...
```

### Parsing from Memory Stream

```csharp
ReadOnlySpan<byte> utf8Json = """
    {
        "message": "Hello from stream"
    }
    """u8;
using var stream = new MemoryStream(utf8Json.ToArray());
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(stream);

JsonElement root = doc.RootElement;
string message = root.GetProperty("message"u8).GetString();
```

## Working with JSON Arrays

```csharp
string json = """
    [
        1,
        2,
        3,
        4,
        5
    ]
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

JsonElement root = doc.RootElement;

// Using foreach with EnumerateArray()
foreach (JsonElement element in root.EnumerateArray())
{
    int value = element.GetInt32();
    Console.WriteLine(value);
}

// Or using indexed access
// Note that indexed access is *not* as efficient as enumeration.
int length = root.GetArrayLength();
for (int i = 0; i < length; i++)
{
    int value = root[i].GetInt32();
    Console.WriteLine(value);
}
```

## Working with Nested Objects

```csharp
string json = """
    {
        "person": {
            "name": "Alice",
            "address": {
                "city": "Seattle",
                "zip": "98101"
            }
        }
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

JsonElement root = doc.RootElement;
JsonElement person = root.GetProperty("person"u8);
JsonElement address = person.GetProperty("address"u8);

string name = person.GetProperty("name"u8).GetString();
string city = address.GetProperty("city"u8).GetString();
string zip = address.GetProperty("zip"u8).GetString();

Console.WriteLine($"{name} lives in {city}, {zip}");
```

## Getting Strongly-Typed Values

`JsonElement` provides methods to extract strongly-typed values from JSON. This is similar to System.Text.Json, but with additional types supported.

### Numeric Types

```csharp
string json = """
    {
        "byteValue": 255,
        "sbyteValue": -128,
        "shortValue": -32768,
        "ushortValue": 65535,
        "intValue": -2147483648,
        "uintValue": 4294967295,
        "longValue": -9223372036854775808,
        "ulongValue": 18446744073709551615,
        "floatValue": 3.14159,
        "doubleValue": 2.718281828459045,
        "decimalValue": 79228162514264337593543950335
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

// Integer types
byte byteVal = root.GetProperty("byteValue"u8).GetByte();
sbyte sbyteVal = root.GetProperty("sbyteValue"u8).GetSByte();
short shortVal = root.GetProperty("shortValue"u8).GetInt16();
ushort ushortVal = root.GetProperty("ushortValue"u8).GetUInt16();
int intVal = root.GetProperty("intValue"u8).GetInt32();
uint uintVal = root.GetProperty("uintValue"u8).GetUInt32();
long longVal = root.GetProperty("longValue"u8).GetInt64();
ulong ulongVal = root.GetProperty("ulongValue"u8).GetUInt64();

// Floating-point types
float floatVal = root.GetProperty("floatValue"u8).GetSingle();
double doubleVal = root.GetProperty("doubleValue"u8).GetDouble();
decimal decimalVal = root.GetProperty("decimalValue"u8).GetDecimal();
```

### Boolean Values

```csharp
string json = """
    {
        "isActive": true,
        "isDeleted": false
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

bool isActive = root.GetProperty("isActive"u8).GetBoolean();
bool isDeleted = root.GetProperty("isDeleted"u8).GetBoolean();
```

### String Values

```csharp
string json = """
    {
        "name": "John Doe",
        "email": "john.doe@example.com",
        "description": null
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

string? name = root.GetProperty("name"u8).GetString();
string? email = root.GetProperty("email"u8).GetString();
string? description = root.GetProperty("description"u8).GetString(); // Returns null
```

### Date and Time Values

```csharp
string json = """
    {
        "createdAt": "2024-01-15T10:30:00Z",
        "updatedAt": "2024-02-28T14:45:30.123+05:00",
        "timestamp": "2024-03-01T00:00:00-08:00"
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

// DateTime values
DateTime createdAt = root.GetProperty("createdAt"u8).GetDateTime();

// DateTimeOffset values (preserves timezone information)
DateTimeOffset updatedAt = root.GetProperty("updatedAt"u8).GetDateTimeOffset();
DateTimeOffset timestamp = root.GetProperty("timestamp"u8).GetDateTimeOffset();
```

### Guid Values

```csharp
string json = """
    {
        "userId": "550e8400-e29b-41d4-a716-446655440000",
        "sessionId": "12345678-1234-1234-1234-123456789012"
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

Guid userId = root.GetProperty("userId"u8).GetGuid();
Guid sessionId = root.GetProperty("sessionId"u8).GetGuid();
```

### Binary Data (Base64)

```csharp
string json = """
    {
        "data": "SGVsbG8gV29ybGQh",
        "signature": "AQIDBA=="
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

// Decode base64 strings to byte arrays
byte[] data = root.GetProperty("data"u8).GetBytesFromBase64();
byte[] signature = root.GetProperty("signature"u8).GetBytesFromBase64();

// data contains: "Hello World!" as bytes
// signature contains: [1, 2, 3, 4]
```

### Extended Numeric Types (Beyond System.Text.Json)

Corvus.Text.Json provides additional numeric types not available in System.Text.Json:

```csharp
        Console.WriteLine("--- Example 12: Extended Numeric Types (Beyond System.Text.Json) ---");

        string json = """
            {
                "largeInteger": 12345678901234567890,
                "preciseNumber": 1234567890123456789.1234567890123456789,
                "bigNumberWithScale": 123456789012345678901234567890123456789E-10
            }
            """;

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        JsonElement root = doc.RootElement;

        // BigInteger for large integers beyond long range
        BigInteger largeNum = root.GetProperty("largeInteger"u8).GetBigInteger();
        // BigNumber for high-precision arithmetic
        BigNumber preciseNum = root.GetProperty("preciseNumber"u8).GetBigNumber();
        BigNumber bigNumWithScale = root.GetProperty("bigNumberWithScale"u8).GetBigNumber();
```

### NodaTime Types

For applications using NodaTime, Corvus.Text.Json provides direct support:

```csharp
using NodaTime;

string json = """
    {
        "localDate": "2024-02-28",
        "offsetTime": "14:30:00+05:00",
        "offsetDate": "2024-02-28+00:00",
        "offsetDateTime": "2024-02-28T14:30:00+05:00",
        "period": "P1Y2M3DT4H5M6S"
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

LocalDate localDate = root.GetProperty("localDate"u8).GetLocalDate();
OffsetTime offsetTime = root.GetProperty("offsetTime"u8).GetOffsetTime();
OffsetDate offsetDate = root.GetProperty("offsetDate"u8).GetOffsetDate();
OffsetDateTime offsetDateTime = root.GetProperty("offsetDateTime"u8).GetOffsetDateTime();
Period period = root.GetProperty("period"u8).GetPeriod();
```

### Type Checking and Safe Conversion

Always check the `ValueKind` before calling Get methods to avoid exceptions:

```csharp
string json = """
    {
        "maybeNumber": "not a number",
        "actualNumber": 42
    }
    """;

using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonElement root = doc.RootElement;

JsonElement maybeNumber = root.GetProperty("maybeNumber"u8);
if (maybeNumber.ValueKind == JsonValueKind.Number)
{
    int value = maybeNumber.GetInt32(); // Safe
}
else
{
    Console.WriteLine("Not a number!"); // This will execute
}

// Use TryGet methods for safer conversion
if (root.GetProperty("actualNumber"u8).TryGetInt32(out int actualValue))
{
    Console.WriteLine($"Value: {actualValue}"); // This will execute
}
```

## Enumerating Object Properties

```csharp
string json = """
    {
        "first": "John",
        "last": "Smith",
        "age": 30
    }
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

JsonElement root = doc.RootElement;
foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
{
    Console.WriteLine($"{property.Name}: {property.Value}");
}
```

## Parsing with Options

```csharp
var options = new JsonDocumentOptions
{
    AllowTrailingCommas = true,
    CommentHandling = JsonCommentHandling.Skip,
    MaxDepth = 64
};

string json = """
    {
        "name": "John", /* comment */
    }
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json, options);

JsonElement root = doc.RootElement;
// Process the JSON data...
```

## Writing JSON from ParsedJsonDocument

After parsing a JSON document, you often need to serialize it for storage, transmission, or API responses. The `WriteTo` method provides efficient UTF-8 serialization.

### Basic Serialization to Stream

```csharp
string json = """
    {
        "message": "Hello",
        "status": 200
    }
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

// Write to any stream - file, network, memory
using var stream = new MemoryStream();
using (var writer = new Utf8JsonWriter(
    stream,
    new JsonWriterOptions { Indented = true }))
{
    doc.WriteTo(writer);
}

string outputJson = Encoding.UTF8.GetString(stream.ToArray());
Console.WriteLine(outputJson);
```

### Serialization Options

Control output formatting with `JsonWriterOptions`:

```csharp
using System.Text.Encodings.Web;

string json = """{"name":"<script>alert('xss')</script>"}""";
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

var options = new JsonWriterOptions
{
    Indented = true,           // Pretty-print with indentation
    SkipValidation = false,     // Validate during write
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // Less aggressive escaping
};

using var stream = new MemoryStream();
using (var writer = new Utf8JsonWriter(stream, options))
{
    doc.WriteTo(writer);
}

string output = Encoding.UTF8.GetString(stream.ToArray());
Console.WriteLine(output);
```

### Writing to File

```csharp
string json = """
    {
        "config": "data",
        "version": "1.0"
    }
    """;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

using FileStream fileStream = File.Create("output.json");
using var writer = new Utf8JsonWriter(
    fileStream,
    new JsonWriterOptions { Indented = true });
    
doc.WriteTo(writer);
// Flush happens automatically on dispose
```

### Writing to HTTP Response (ASP.NET Core)

For web APIs, write directly to the response stream for optimal performance:

```csharp
public async Task WriteJsonResponse(HttpContext context)
{
    // Parse or build your JSON document
    string json = GetDataAsJson();
    using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
    
    context.Response.ContentType = "application/json";
    
    // Write directly to response body
    await using var writer = new Utf8JsonWriter(
        context.Response.Body,
        new JsonWriterOptions { Indented = false });
        
    doc.WriteTo(writer);
    await writer.FlushAsync();
}
```

### Getting JSON as String

For simpler scenarios, use `ToString()` or `GetRawText()`:

```csharp
string json = """{"message":"Hello","value":42}""";
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

// Get formatted string representation
string formattedJson = doc.RootElement.ToString();
Console.WriteLine(formattedJson);

// Get original raw JSON text (preserves formatting)
string rawJson = doc.RootElement.GetRawText();
Console.WriteLine(rawJson);
```

**Note**: `ToString()` creates a new string allocation. For high-performance scenarios, prefer `WriteTo()` with a writer.

### Performance Considerations

1. **Use UTF-8 directly**: `WriteTo()` writes UTF-8 bytes without encoding conversion
2. **Stream to destination**: Write directly to your target stream instead of intermediate buffers
3. **Disable validation**: Set `SkipValidation = true` if you trust the source data
4. **Avoid ToString()**: In hot paths, use `WriteTo()` instead of string conversion
5. **Reuse writers**: For repeated operations, consider writer pooling (see JsonDocumentBuilder docs)

### Performance Comparison

```csharp
// ❌ Slower: Multiple allocations
string jsonString = doc.RootElement.ToString();
byte[] bytes = Encoding.UTF8.GetBytes(jsonString);
await stream.WriteAsync(bytes);

// ✅ Faster: Direct UTF-8 serialization
using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false });
doc.WriteTo(writer);
await writer.FlushAsync();
```

## Common Serialization Patterns

### API Gateway: Parse and Forward

A common pattern in API gateways and middleware - parse incoming JSON, optionally validate or transform it, then forward to an upstream service:

```csharp
public async Task ForwardJsonAsync(HttpContext context, string upstreamUrl)
{
    // Parse incoming request
    using var requestDoc = await ParsedJsonDocument<JsonElement>.ParseAsync(
        context.Request.Body);
    
    // Optional: validate or transform here
    // if (!IsValid(requestDoc.RootElement)) { return BadRequest(); }
    
    // Forward to upstream service
    using var httpClient = new HttpClient();
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
    {
        requestDoc.WriteTo(writer);
    }
    
    stream.Position = 0;
    var content = new StreamContent(stream);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    
    var response = await httpClient.PostAsync(upstreamUrl, content);
    
    // Forward response back to client
    context.Response.StatusCode = (int)response.StatusCode;
    await response.Content.CopyToAsync(context.Response.Body);
}
```

### Caching: Serialize for Storage

Serialize JSON documents to byte arrays for caching in Redis, Memcached, or in-memory caches:

```csharp
public async Task CacheJsonAsync(string key, ParsedJsonDocument<JsonElement> doc)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
    {
        doc.WriteTo(writer);
    }
    
    byte[] jsonBytes = stream.ToArray();
    
    // Store in cache with expiration
    await _cache.SetAsync(
        key, 
        jsonBytes, 
        new DistributedCacheEntryOptions 
        { 
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) 
        });
}

public async Task<ParsedJsonDocument<JsonElement>?> GetCachedJsonAsync(string key)
{
    byte[]? jsonBytes = await _cache.GetAsync(key);
    if (jsonBytes == null)
    {
        return null;
    }
    
    return ParsedJsonDocument<JsonElement>.Parse(jsonBytes.AsMemory());
}
```

### Logging: Structured JSON

Log JSON data efficiently:

```csharp
public void LogStructuredData(ParsedJsonDocument<JsonElement> data, string operation)
{
    if (!_logger.IsEnabled(LogLevel.Information))
    {
        return;
    }
    
    // For logging, ToString() is fine - it's already a single allocation
    _logger.LogInformation(
        "Operation {Operation} completed with data: {Json}", 
        operation,
        data.RootElement.ToString());
}

// For high-volume logging where you want to avoid ToString(),
// log the document directly and let the logger handle serialization:
public void LogStructuredDataHighVolume(ParsedJsonDocument<JsonElement> data, string operation)
{
    if (!_logger.IsEnabled(LogLevel.Information))
    {
        return;
    }
    
    // Pass the JsonElement directly - many structured logging systems
    // can serialize it without intermediate string conversion
    _logger.LogInformation(
        "Operation {Operation} completed with data: {@Data}", 
        operation,
        data.RootElement);
}
```

### File Operations: Batch Processing

Process multiple JSON files efficiently:

```csharp
public async Task ProcessJsonFilesAsync(string inputDirectory, string outputDirectory)
{
    foreach (string inputFile in Directory.GetFiles(inputDirectory, "*.json"))
    {
        // Parse with pooled memory
        using FileStream inputStream = File.OpenRead(inputFile);
        using ParsedJsonDocument<JsonElement> doc = 
            await ParsedJsonDocument<JsonElement>.ParseAsync(inputStream);
        
        // Validate or transform
        if (!ValidateDocument(doc.RootElement))
        {
            _logger.LogWarning("Skipping invalid file: {File}", inputFile);
            continue;
        }
        
        // Write to output with formatting
        string outputFile = Path.Combine(
            outputDirectory, 
            Path.GetFileName(inputFile));
        
        using FileStream outputStream = File.Create(outputFile);
        using var writer = new Utf8JsonWriter(
            outputStream,
            new JsonWriterOptions { Indented = true });
        
        doc.WriteTo(writer);
        await writer.FlushAsync();
    }
}
```

## Comparison: ParsedJsonDocument vs System.Text.Json.JsonDocument

### Writing/Serialization Comparison

Both support similar serialization patterns with identical APIs:

**System.Text.Json.JsonDocument**:
```csharp
using System.Text.Json;

string json = """{"message":"Hello"}""";
using var doc = JsonDocument.Parse(json);

using var stream = new MemoryStream();
using var writer = new Utf8JsonWriter(stream);
doc.WriteTo(writer);

string output = Encoding.UTF8.GetString(stream.ToArray());
```

**Corvus.Text.Json.ParsedJsonDocument**:
```csharp
using Corvus.Text.Json;

string json = """{"message":"Hello"}""";
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);

using var stream = new MemoryStream();
using var writer = new Utf8JsonWriter(stream);
doc.WriteTo(writer);

string output = Encoding.UTF8.GetString(stream.ToArray());
```

**Key Differences**:

| Aspect | System.Text.Json.JsonDocument | Corvus.Text.Json.ParsedJsonDocument |
|--------|-------------------------------|-------------------------------------|
| **API Compatibility** | Standard .NET API | Compatible API with extensions |
| **Type System** | Fixed to `JsonElement` | Generic over `IJsonElement<T>` |

### When to Choose Which

**Use System.Text.Json.JsonDocument when:**
- Standard .NET library is sufficient

**Use ParsedJsonDocument when:**
- Need extended type support (BigNumber, NodaTime)
- Need JSON Schema Serialization support (`IJsonElement<T>`)

## Error Handling

### Handling Write Errors

```csharp
public async Task WriteJsonSafelyAsync(ParsedJsonDocument<JsonElement> doc, string filePath)
{
    try
    {
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true });
        
        doc.WriteTo(writer);
        await writer.FlushAsync();
        
        _logger.LogInformation("Successfully wrote JSON to {FilePath}", filePath);
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "Failed to write JSON to file {FilePath}", filePath);
        throw new ApplicationException($"Could not write to {filePath}", ex);
    }
    catch (UnauthorizedAccessException ex)
    {
        _logger.LogError(ex, "Access denied writing to {FilePath}", filePath);
        throw;
    }
}
```

### Handling Parse and Write Pipeline

```csharp
public async Task<bool> TryProcessJsonAsync(Stream input, Stream output)
{
    ParsedJsonDocument<JsonElement>? doc = null;
    try
    {
        // Parse
        doc = await ParsedJsonDocument<JsonElement>.ParseAsync(input);
        
        // Validate
        if (!IsValidDocument(doc.RootElement))
        {
            _logger.LogWarning("Document failed validation");
            return false;
        }
        
        // Write
        using var writer = new Utf8JsonWriter(
            output,
            new JsonWriterOptions { Indented = false });
        doc.WriteTo(writer);
        await writer.FlushAsync();
        
        return true;
    }
    catch (JsonException ex)
    {
        _logger.LogError(ex, "Invalid JSON format");
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error processing JSON");
        return false;
    }
    finally
    {
        // Critical: Always dispose to return pooled memory
        doc?.Dispose();
    }
}
```

## Important Notes

### Memory Management

`ParsedJsonDocument<T>` uses pooled memory and **must be disposed** to return memory to the pool. Failure to dispose will result in increased GC pressure.

```csharp
// ❌ Bad - memory leak
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
// Missing dispose!

// ✅ Good - proper disposal
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
// Automatically disposed at end of scope

// ✅ Also good - explicit disposal
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
try
{
    // Use doc
}
finally
{
    doc.Dispose();
}
```

### Data Lifetime

When parsing from `ReadOnlyMemory<byte>` or `ReadOnlySequence<byte>`, the memory must remain valid for the entire lifetime of the document. The parser does not make a copy of the input data.

### Thread Safety

While `ParsedJsonDocument<T>` itself is thread-safe for read operations after parsing, the underlying JSON data should not be modified during the document's lifetime.

## Static Constants

For common literal values, use the static properties to avoid allocations:

```csharp
JsonElement nullValue = ParsedJsonDocument<JsonElement>.Null;
JsonElement trueValue = ParsedJsonDocument<JsonElement>.True;
JsonElement falseValue = ParsedJsonDocument<JsonElement>.False;
```

## Error Handling

```csharp
try
{
    string invalidJson = """
        {
            invalid json
        }
        """;
    using var doc = ParsedJsonDocument<JsonElement>.Parse(invalidJson);
}
catch (JsonException ex)
{
    Console.WriteLine($"Invalid JSON: {ex.Message}");
}
```

## Performance Tips

1. **Use async for large streams**: When parsing large files or network streams, use `ParseAsync` to avoid blocking
2. **Reuse streams**: When parsing multiple documents, reuse stream objects when possible
3. **Use UTF-8 directly**: Parsing from `ReadOnlyMemory<byte>` is more efficient than parsing from strings
4. **Dispose properly**: Always dispose documents to return memory to the pool
