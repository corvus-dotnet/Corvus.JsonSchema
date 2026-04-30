# JSON Schema Patterns in .NET - Reusing Common Types

This recipe demonstrates how to use JSON Schema's `$ref` and `$defs` keywords to share type definitions across properties, reducing generated code size and enabling zero-allocation conversions between compatible property types.

## The Pattern

In our [previous example](../002-DataObjectValidation/), we constrained all of our string properties to a string of length 1–256. Each property had its own inline definition, which created separate types: `GivenNameEntity`, `FamilyNameEntity`, and `OtherNamesEntity`.

We can simplify this by re-using a reference to a common schema with the `$ref` keyword. Instead of three separate types for the string properties, the code generator produces a single type: `PersonCommonSchema.ConstrainedString`.

Using `$ref` to share schema definitions provides two key advantages:

### Code Size Reduction

Instead of generating three separate entity types (`GivenNameEntity`, `FamilyNameEntity`, `OtherNamesEntity`), the generator produces a single `ConstrainedString` type. This reduces the overall size of the generated assembly and simplifies the API surface.

### Type Compatibility and Interoperability

**Shared schemas mean the generated value types are compatible.** Because `GivenName`, `FamilyName`, and `OtherNames` all use the same `$ref`, they return the same `ConstrainedString` type. This enables:

- **Zero-allocation comparisons**: `givenName.Equals(familyName)` compares the underlying JSON data directly
- **Zero-allocation conversions**: If you have multiple schemas using the same `$ref`, their property entity types are compatible and can be converted using `.From()` without extracting to primitives
- **Shared operations**: All the same methods, formatting, and validation logic apply uniformly

This compatibility is essential for mapping between different schemas that share common sub-schemas, as demonstrated in [Recipe 017 - Mapping Input and Output Values](../017-MappingInputAndOutputValues/).

## The Schema

File: `person-common-schema.json`

```json
{
  "title": "The person schema https://schema.org/Person",
  "type": "object",
  "required": [ "familyName", "givenName", "birthDate" ],
  "properties": {
    "familyName": { "$ref": "#/$defs/constrainedString" },
    "givenName": { "$ref": "#/$defs/constrainedString" },
    "otherNames": { "$ref": "#/$defs/constrainedString" },
    "birthDate": {
      "type": "string",
      "format": "date"
    },
    "height": {
      "type": "number",
      "format": "double",
      "exclusiveMinimum": 0.0,
      "maximum": 3.0
    }
  },

  "$defs": {
    "constrainedString": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    }
  }
}
```

The generated types are:

- `PersonCommonSchema` — the root object type
- `PersonCommonSchema.ConstrainedString` — the shared string type with length constraints (1–256)
- `PersonCommonSchema.HeightEntity` — the constrained height type (`double`, `exclusiveMinimum: 0`, `maximum: 3.0`)

The `GivenName`, `FamilyName`, and `OtherNames` properties all return the *same* `ConstrainedString` type.

## Generated Code Usage

[Example code](./Program.cs)

### Creating an instance with shared types

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = PersonCommonSchema.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    otherNames: string.Empty,
    height: 1.57);

string json = personDoc.RootElement.ToString();
using var parsedDoc = ParsedJsonDocument<PersonCommonSchema>.Parse(json);
PersonCommonSchema personCommonSchema = parsedDoc.RootElement;
```

### Working with the shared type

```csharp
// Custom types can be converted in the same way as the primitive types on which they are based.
PersonCommonSchema.ConstrainedString constrainedGivenName = personCommonSchema.GivenName;
PersonCommonSchema.ConstrainedString constrainedFamilyName = personCommonSchema.FamilyName;
string cgn = (string)constrainedGivenName;
string cfn = (string)constrainedFamilyName;
```

### Zero-allocation comparisons

```csharp
// Low-allocation comparisons are available in the usual way (as are all
// the other features of JsonString).
constrainedGivenName.Equals(constrainedFamilyName);
constrainedGivenName.ValueEquals("Hello");
constrainedGivenName.ValueEquals("Anne"u8);
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create directly with static factory method
PersonCommonSchema person = PersonCommonSchema.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    otherNames: string.Empty,
    height: 1.57);

// Access shared types
PersonCommonSchema.ConstrainedString givenName = person.GivenName;
PersonCommonSchema.ConstrainedString familyName = person.FamilyName;

// Zero-allocation comparison (same as V5)
givenName.Equals(familyName);
givenName.ValueEquals("Anne"u8);
```

### V5 (Corvus.Text.Json)
```csharp
// Create using workspace and builder pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = PersonCommonSchema.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    otherNames: string.Empty,
    height: 1.57);
PersonCommonSchema person = personDoc.RootElement;

// Access shared types (same as V4)
PersonCommonSchema.ConstrainedString givenName = person.GivenName;
PersonCommonSchema.ConstrainedString familyName = person.FamilyName;

// Zero-allocation comparison (same as V4)
givenName.Equals(familyName);
givenName.ValueEquals("Anne"u8);
```

**Key differences:**
- V5 uses `CreateBuilder(workspace, prop: value, ...)` instead of V4's `Create(prop: value, ...)`
- V5 requires a `JsonWorkspace` for creation — this manages pooled memory for high-performance scenarios
- Shared type behaviour is identical: `$ref` produces the same `ConstrainedString` type in both versions
- `From()` conversions and zero-allocation comparisons work the same way in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/003-ReusingCommonTypes
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) - Simple data objects without shared types
- [005-ExtendingABaseType](../005-ExtendingABaseType/) - Inheritance via `allOf` with a single base
- [017-MappingInputAndOutputValues](../017-MappingInputAndOutputValues/) - Using `From()` for zero-allocation mapping between schemas

## Frequently Asked Questions

### What is the difference between using `$ref` and defining properties inline?

Inline definitions create a separate generated type for each property — so three string properties with identical constraints produce three distinct entity types (`GivenNameEntity`, `FamilyNameEntity`, `OtherNamesEntity`). Using `$ref` to point all three properties at the same `$defs` entry produces a single shared type (`ConstrainedString`). The shared type reduces code size and, crucially, makes the property values type-compatible so you can compare or convert between them without extracting to .NET primitives.

### Can I share types across multiple schema files?

Yes. `$ref` supports relative file paths (e.g., `{ "$ref": "./common/constrained-string.json" }`), so you can define a shared type in one file and reference it from any number of other schema files. The code generator resolves these references at generation time. This is the recommended approach for large projects with many schemas that share common definitions.

### Are properties with the same `$ref` target truly type-compatible?

Yes. When two or more properties reference the same `$ref` target (whether via `$defs` in the same file or a shared external file), the code generator produces a single .NET type. All properties using that `$ref` return the same type, so you can compare them directly with `Equals()`, pass them to the same methods, and convert between entity types from different schemas using `From()` — all without allocating .NET strings or other primitives.

### How does `From()` work for converting between compatible entity types?

`From()` is a static method on generated entity types that creates a zero-allocation view over the underlying JSON data of a compatible source entity. For example, if schema A has `NameEntity` and schema B has `FullNameEntity`, and both are `$ref`-compatible strings, you can write `B.FullNameEntity.From(sourceA.Name)` to convert without extracting to a .NET `string`. This is the foundation of the mapping pattern shown in [Recipe 017](../017-MappingInputAndOutputValues/).
