# JSON Schema Patterns in .NET - Flags Enums from Boolean Objects

This recipe demonstrates how an object whose properties are all boolean is detected as a flags shape, generating a native C# `[Flags]` enum with conversions in both directions.

## The Pattern

A set of on/off options is naturally modelled in JSON as an object of booleans:

- **Self-documenting** - each option is a named property
- **Extensible** - adding an option is a non-breaking schema change
- **Flags ergonomics in C#** - the generated `[Flags]` enum gives you `|`, `&`, `~` and `HasFlag`

The generator detects the shape when an object declares two to thirty-one properties, every one of them boolean (and none constant-valued), with no `patternProperties` and `additionalProperties` absent or `false`.

## The Schema

File: `feature-flags.json`

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "properties": {
        "betaFeatures": { "type": "boolean", "default": false },
        "darkMode": { "type": "boolean", "default": false },
        "telemetry": { "type": "boolean", "default": false }
    },
    "additionalProperties": false
}
```

File: `app-settings.json` references it from a `features` property.

## Generated Code Usage

### The nested `[Flags]` enum

The generated `FeatureFlags` type gains a nested enum with `None = 0` and one bit per boolean property:

```csharp
FeatureFlags.Flags enabled = FeatureFlags.Flags.DarkMode | FeatureFlags.Flags.Telemetry;
```

Bits are assigned to the properties in alphabetical order of their JSON names. Adding or renaming a property can reassign the bits, so do not persist the integer values; the JSON wire format is unaffected.

### Creating documents from flags

The `Source` conversion writes every declared property explicitly, so the output is deterministic and satisfies any `required` properties:

```csharp
using var doc = AppSettings.Create(name: "my-app", features: enabled);
Console.WriteLine(doc.RootElement.ToString());
// Output: {"features":{"betaFeatures":false,"darkMode":true,"telemetry":true},"name":"my-app"}
```

### Reading flags back

An implicit conversion reads the object. A property that is absent or `false` leaves its bit clear; extra properties are ignored:

```csharp
FeatureFlags.Flags current = doc.RootElement.Features;
bool darkMode = current.HasFlag(FeatureFlags.Flags.DarkMode);
```

The conversion throws `InvalidOperationException` if the value is not an object. Use the non-throwing form when the value may not be one:

```csharp
if (parsed.RootElement.TryGetFlags(out FeatureFlags.Flags tried))
{
    Console.WriteLine($"TryGetFlags: {tried}");
}
```

## Turning the feature off

Emission is on by default. Set the `nativeEnums` option to turn it off, either project-wide for the source generator:

```xml
<PropertyGroup>
  <CorvusTextJsonNativeEnums>None</CorvusTextJsonNativeEnums>
</PropertyGroup>
```

or with `--nativeEnums None` on the `corvusjson` CLI. `StringEnums` and `FlagsObjects` select just one of the two native enum features (see [Recipe 014](../014-StringEnumerations/) for the string enum half).

## Running the Example

```bash
cd docs/ExampleRecipes/043-FlagsEnums
dotnet run
```

## Related Patterns

- [014-StringEnumerations](../014-StringEnumerations/) - String enums, including the native `KnownValues` enum
- [002-DataObjectValidation](../002-DataObjectValidation/) - Validating parsed documents

## Frequently Asked Questions

### Q: Why is my object not getting a `Flags` enum?

**A:** Detection is deliberately conservative. The object must declare between two and thirty-one properties, every declared property must be boolean and not constant-valued, there must be no `patternProperties`, and `additionalProperties` must be absent or `false`. A single boolean property, a mixed property set, or an open object does not qualify.

### Q: What is the difference between a property being `false` and being absent?

**A:** Nothing, as far as the flags are concerned. Both leave the bit clear when reading. When writing from a `Flags` value, every declared property is emitted explicitly with `true` or `false`.

### Q: Can I persist the integer value of the flags?

**A:** No. Bits follow the alphabetical order of the JSON property names, so adding or renaming a property can renumber them. Persist the JSON instead; it is stable under those changes.