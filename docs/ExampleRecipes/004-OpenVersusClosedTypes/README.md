# JSON Schema Patterns in .NET - Open Versus Closed Types

This recipe demonstrates how to use `unevaluatedProperties` to control whether a JSON Schema object type is open (extensible) or closed (strict), and how that affects validation and mutation in generated .NET code.

## The Pattern

**Open Types**

In this context, an open type is an `object` type that is still valid when it contains properties other than those explicitly defined in its own schema (such as those under the `properties` keyword).

By default, JSON Schema assumes an open content model, meaning that unless specified otherwise, objects can have additional properties not declared in the schema.

This is useful when you want your objects to be extensible or when you don't want to strictly validate every property.

Allowing this extensibility is a common approach to "versioning by evolution" or "backwards compatibility" where it is intended that new versions of a schema are still accepted by old consumers that don't know about (or use) the new extensions.

**Closed Types**

If you want to create a *closed* type in JSON Schema, you would set `unevaluatedProperties` to `false`.

This means that the object can only contain properties that are explicitly defined in the schema, and no others.

This is commonly used in "parallel versioning" where old consumers do *not* support new versions of schema, and typically new versions have new names to avoid confusion.

> **Note:** Prior to draft 2020-12, `unevaluatedProperties` was not available and you would use `additionalProperties`. The semantics are slightly different (and a little complex!), and you would generally prefer `unevaluatedProperties` today.

## The Schemas

File: `person-open.json`

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

This generates:

- `PersonOpen` — the root object type
- `PersonOpen.ConstrainedString` — shared string type with length constraints
- `PersonOpen.HeightEntity` — constrained height type

File: `person-closed.json`

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

  "unevaluatedProperties": false,

  "$defs": {
    "constrainedString": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    }
  }
}
```

This generates:

- `PersonClosed` — the root object type with `unevaluatedProperties: false`
- `PersonClosed.ConstrainedString` — shared string type
- `PersonClosed.HeightEntity` — constrained height type

## Generated Code Usage

### Validating open vs closed types

[Example code](./Program.cs)

```csharp
using Corvus.Text.Json;
using OpenVersusClosedTypes.Models;

// A person with an additional property: "jobRole".
string extendedPersonJsonString =
    """
    {
        "familyName": "Brontë",
        "givenName": "Anne",
        "birthDate": "1820-01-17",
        "height": 1.57,
        "jobRole": "Author"
    }
    """;

using var parsedPersonOpen = ParsedJsonDocument<PersonOpen>.Parse(extendedPersonJsonString);
PersonOpen personOpen = parsedPersonOpen.RootElement;

// An object is, by default, open - allowing undeclared properties.
if (personOpen.EvaluateSchema())
{
    Console.WriteLine("personOpen is valid");
}
else
{
    Console.WriteLine("personOpen is not valid");
}

using var parsedPersonClosed = ParsedJsonDocument<PersonClosed>.Parse(extendedPersonJsonString);
PersonClosed personClosed = parsedPersonClosed.RootElement;

// An object with unevaluatedProperties: false does not allow undeclared properties.
if (personClosed.EvaluateSchema())
{
    Console.WriteLine("personClosed is valid");
}
else
{
    Console.WriteLine("personClosed is not valid");
}
```

### Fixing a closed type by removing undeclared properties

```csharp
// We can still use an invalid entity - and fix it; for instance, by removing
// the invalid property via the open type's generic RemoveProperty method.
using JsonWorkspace workspace = JsonWorkspace.Create();
using var fixedDoc = personOpen.CreateBuilder(workspace);
PersonOpen.Mutable fixedRoot = fixedDoc.RootElement;
fixedRoot.RemoveProperty("jobRole"u8);

PersonClosed personFixed = PersonClosed.From(fixedDoc.RootElement);
if (personFixed.EvaluateSchema())
{
    Console.WriteLine("personFixed is valid");
}
else
{
    Console.WriteLine("personFixed is not valid");
}
```

### Breaking a closed type by adding undeclared properties

```csharp
// Equally we can make a valid entity invalid by setting an unknown property.
using var brokenDoc = personOpen.CreateBuilder(workspace);
PersonOpen.Mutable brokenRoot = brokenDoc.RootElement;
brokenRoot.SetProperty("age"u8, 23);

PersonClosed personBroken = PersonClosed.From(brokenDoc.RootElement);
if (personBroken.EvaluateSchema())
{
    Console.WriteLine("personBroken is valid");
}
else
{
    Console.WriteLine("personBroken is not valid");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Parse directly (no document wrapper)
PersonOpen personOpen = PersonOpen.Parse(extendedPersonJsonString);

// Validate
bool isValid = personOpen.IsValid();

// Create a new instance with a property removed (immutable transform)
PersonOpen fixedPerson = personOpen.RemoveProperty("jobRole");

// Implicit conversion between types
PersonClosed personClosed = fixedPerson.As<PersonClosed>();
```

### V5 (Corvus.Text.Json)
```csharp
// Parse with document wrapper for resource management
using var parsedPersonOpen = ParsedJsonDocument<PersonOpen>.Parse(extendedPersonJsonString);
PersonOpen personOpen = parsedPersonOpen.RootElement;

// Validate
bool isValid = personOpen.EvaluateSchema();

// Mutate via workspace and builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var fixedDoc = personOpen.CreateBuilder(workspace);
fixedDoc.RootElement.RemoveProperty("jobRole"u8);

// Convert between types with From()
PersonClosed personClosed = PersonClosed.From(fixedDoc.RootElement);
```

**Key differences:**
- V5 uses `ParsedJsonDocument<T>` for parsing with proper resource management (`using`)
- V5 uses `EvaluateSchema()` instead of `IsValid()` for schema validation
- V5 mutations use a `JsonWorkspace` and mutable builder pattern instead of immutable transforms
- V5 uses `From()` for explicit type conversion instead of `As<T>()`
- V5 uses UTF-8 byte literals (`"jobRole"u8`) for zero-allocation property access

## Running the Example

```bash
cd docs/ExampleRecipes/004-OpenVersusClosedTypes
dotnet run
```

## Related Patterns

- [005-ExtendingABaseType](../005-ExtendingABaseType/) - Adding properties to an open base type
- [006-ConstrainingABaseType](../006-ConstrainingABaseType/) - Adding constraints to a base type
- [016-Maps](../016-Maps/) - Using `additionalProperties` for map/dictionary structures

## Frequently Asked Questions

### When should I use an open type vs a closed type?

Use **open types** when you want forwards compatibility — consumers that understand an older version of the schema will still accept documents that include properties added in newer versions. This is the default in JSON Schema and is ideal for evolving APIs. Use **closed types** (`unevaluatedProperties: false`) when you need strict validation — for example, when unknown properties indicate a client error, or when you use parallel versioning with distinct schema names for each version.

### What is the difference between `unevaluatedProperties` and `additionalProperties`?

Both can close a type, but they differ in scope. `additionalProperties` only considers properties declared directly in the same schema's `properties` and `patternProperties` keywords. `unevaluatedProperties` (introduced in draft 2020-12) considers properties evaluated by *any* applicator keyword in the schema — including `allOf`, `oneOf`, `anyOf`, `if/then/else`, and `$ref`. This means `unevaluatedProperties: false` works correctly with composed schemas, whereas `additionalProperties: false` can accidentally reject properties declared in referenced schemas. Prefer `unevaluatedProperties` in draft 2020-12 and later.

### Can I still access undeclared properties on a closed type?

Yes. The generated .NET type for a closed schema still has the generic `TryGetProperty()` method, so you can read any property present in the underlying JSON. However, the document will fail `EvaluateSchema()` if it contains properties not declared in the schema. Closing a type is a *validation* concern, not a data-access concern.

### How does open/closed affect API versioning?

With **open types**, a v2 document that adds a new `email` property is still valid against the v1 schema — older consumers simply ignore the extra field. With **closed types**, the v1 schema rejects the v2 document because `email` is not declared. This forces you to publish a new schema (e.g., `person-v2-closed.json`) with its own name and version, making the version boundary explicit.
