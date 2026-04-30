# JSON Schema Patterns in .NET - Default Values

This recipe demonstrates how JSON Schema `default` annotations work with generated types in Corvus.Text.Json, and how the generated property getters automatically apply schema-declared defaults when properties are missing from the JSON document.

## The Pattern

In JSON Schema, the `default` keyword is an **annotation** — it declares what value a property should logically assume when absent, but it does not affect validation. A document missing a property with a default is still valid (provided the property is not also in `required`).

Corvus.Text.Json V5 goes a step further: the generated **immutable** property getters automatically return the schema-declared default value when a property is not present in the JSON. This means you can access properties naturally without manual fallback logic. The default values are exposed as `DefaultInstance` static properties on each property's entity type, so you can also access them directly when needed.

Key points:
- **`default` is not a validation keyword** — missing properties with defaults still pass validation
- **Immutable property getters apply defaults automatically** — no need to check `IsUndefined()` and fall back manually
- **`DefaultInstance`** is available on each property entity type for direct access to the schema-declared default
- **The underlying JSON is unchanged** — defaults are applied by the generated getter, not written into the document

## The Schema

File: `config.json`

```json
{
    "title": "Configuration",
    "type": "object",
    "properties": {
        "host": { "type": "string" },
        "port": { "type": "integer", "format": "int32", "default": 8080 },
        "timeout": { "type": "integer", "format": "int32", "default": 30 },
        "retries": { "type": "integer", "format": "int32", "default": 3 },
        "logLevel": { "type": "string", "enum": ["debug", "info", "warn", "error"], "default": "info" },
        "enableSsl": { "type": "boolean", "default": true }
    },
    "required": ["host"]
}
```

Only `host` is required. All other properties have `default` values that the generated code will return when those properties are absent.

The generated .NET properties are:

- `Host` — of type `Config.HostEntity` (a string type, required)
- `Port` — of type `Config.PortEntity` (an int32 type, default: `8080`)
- `Timeout` — of type `Config.TimeoutEntity` (an int32 type, default: `30`)
- `Retries` — of type `Config.RetriesEntity` (an int32 type, default: `3`)
- `LogLevel` — of type `Config.LogLevelEntity` (a string enum type, default: `"info"`)
- `EnableSsl` — of type `Config.EnableSslEntity` (a boolean type, default: `true`)

## Generated Code Usage

[Example code](./Program.cs)

### Parsing minimal JSON with automatic defaults

```csharp
using var minimalDoc = ParsedJsonDocument<Config>.Parse(
    """{"host":"localhost"}""");
Config minimal = minimalDoc.RootElement;

// Property getters automatically return schema defaults for missing properties
string host = (string)minimal.Host;         // "localhost" (from JSON)
int port = (int)minimal.Port;               // 8080 (from schema default)
int timeout = (int)minimal.Timeout;         // 30 (from schema default)
int retries = (int)minimal.Retries;         // 3 (from schema default)
string logLevel = (string)minimal.LogLevel; // "info" (from schema default)
bool enableSsl = (bool)minimal.EnableSsl;   // true (from schema default)
```

The underlying JSON still only contains `{"host":"localhost"}` — the defaults are applied by the generated property getters, not written into the document.

### Accessing DefaultInstance directly

Each property entity type with a `default` in the schema exposes a static `DefaultInstance` property:

```csharp
int defaultPort = (int)Config.PortEntity.DefaultInstance;         // 8080
int defaultTimeout = (int)Config.TimeoutEntity.DefaultInstance;   // 30
int defaultRetries = (int)Config.RetriesEntity.DefaultInstance;   // 3
string defaultLogLevel = (string)Config.LogLevelEntity.DefaultInstance; // "info"
bool defaultSsl = (bool)Config.EnableSslEntity.DefaultInstance;   // true
```

This is useful when you need to display or log the schema-declared defaults independently of any parsed document.

### Explicit values override defaults

```csharp
using var fullDoc = ParsedJsonDocument<Config>.Parse(
    """
    {
        "host": "api.example.com",
        "port": 443,
        "timeout": 60,
        "retries": 5,
        "logLevel": "debug",
        "enableSsl": false
    }
    """);
Config full = fullDoc.RootElement;

int port = (int)full.Port;               // 443 (from JSON, not default)
bool enableSsl = (bool)full.EnableSsl;   // false (from JSON, not default)
```

### Validation

A config with just the required `host` property passes validation — `default` does not affect validation rules:

```csharp
Console.WriteLine(minimal.EvaluateSchema()); // True
Console.WriteLine(full.EvaluateSchema());    // True
```

### Creating with CreateBuilder

Use `CreateBuilder` to set only the properties you need:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builtDoc = Config.CreateBuilder(workspace, host: "built.example.com");
Config built = builtDoc.RootElement;

// Generated JSON only contains the explicitly set property
Console.WriteLine(built); // {"host":"built.example.com"}

// But property getters still return schema defaults
int port = (int)built.Port; // 8080 (from schema default)
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Parse — direct static method
Config config = Config.Parse("""{"host":"localhost"}""");

// Defaults applied the same way via property getters
int port = (int)config.Port; // 8080

// Access default directly
int defaultPort = (int)Config.PortEntity.DefaultInstance; // 8080
```

### V5 (Corvus.Text.Json)
```csharp
// Parse — via ParsedJsonDocument wrapper
using var doc = ParsedJsonDocument<Config>.Parse("""{"host":"localhost"}""");
Config config = doc.RootElement;

// Defaults applied the same way via property getters
int port = (int)config.Port; // 8080

// Access default directly
int defaultPort = (int)Config.PortEntity.DefaultInstance; // 8080
```

**Key differences:**
- V5 wraps parsed results in `ParsedJsonDocument<T>` with `using` for deterministic memory management
- V5 uses `CreateBuilder(workspace, ...)` instead of V4's `Create(...)` for constructing new instances
- The `DefaultInstance` pattern and automatic property getter defaults work identically in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/021-DefaultValues
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) - Basic data objects and property access patterns
- [002-DataObjectValidation](../002-DataObjectValidation/) - Schema validation with detailed error reporting
- [014-StringEnumerations](../014-StringEnumerations/) - String enum values with pattern matching (used by `logLevel` in this recipe)

## Frequently Asked Questions

### Why aren't defaults automatically applied to the JSON document?

Because JSON Schema treats `default` as an **annotation**, not a validation keyword. The specification (drafts 2019-09 and 2020-12) explicitly states that `default` has no effect on validation — it exists purely as metadata for documentation and tooling. Corvus.Text.Json respects this by leaving the underlying JSON unchanged. The generated property getters return the default value when a property is missing, giving you the convenience of automatic defaults while keeping the document faithful to the original JSON.

### How do I distinguish between "explicitly set to the default value" and "missing, defaulted by the getter"?

Check whether the property exists in the raw JSON by examining the element directly. If you parse `{"host":"localhost","port":8080}`, the `port` property is present and set to `8080`. If you parse `{"host":"localhost"}`, the `port` property is absent but the getter still returns `8080` via `DefaultInstance`. To distinguish these cases, you can check `minimal.AsJsonElement.TryGetProperty("port"u8, out _)` on the underlying JSON, but in most applications this distinction is unnecessary.

### Can I use `default` with required properties?

Yes, but it's unusual. If a property is in the `required` array, it must always be present in valid JSON, so the default would never be applied during normal operation. Schema validators will reject documents missing required properties regardless of whether a default is declared. The `default` annotation on a required property serves only as documentation of the expected typical value.

### Does `default` work with complex types (objects, arrays)?

Yes. The `default` keyword accepts any valid JSON value, including objects and arrays. The generated `DefaultInstance` will contain the full default structure. For example, a property with `"default": {"key": "value"}` would produce a `DefaultInstance` containing that object.