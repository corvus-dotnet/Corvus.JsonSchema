# JSON Schema Patterns in .NET - Extending a Base Type

This recipe demonstrates how to use `$ref` to extend an open base type with additional properties, creating a derived schema that includes all base properties plus its own — similar to inheritance in object-oriented languages.

## The Pattern

When you have an open type, you can extend it with additional properties. This is rather like deriving from an (unsealed) base type in an object-oriented language, and adding additional properties in the derived type. If you are coming from an OOP background, think of `$ref` as the schema-world equivalent of inheritance: the base schema (`person-open.json`) defines a set of properties and constraints, and the extending schema adds its own on top, without modifying the original.

In this case we use `$ref` to declare that we are basing a new schema on the `person-open.json` schema.

We then extend it with an additional `wealth` property, which is a 32-bit integer number. This happens to be declared as a `required` property.

## The Schemas

File: `person-wealthy.json`

```json
{
  "title": "A wealthy person",
  "$ref": "./person-open.json",
  "required": ["wealth"],
  "properties": {
    "wealth": {
      "type": "integer",
      "format": "int32"
    }
  }
}
```

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

:::aside
In draft 6 and draft 7, `$ref` cannot be used in this way. It acts as a reference to the target type and *ignores* the adjacent values. This is often a bit surprising! Instead, you can use `allOf` with a single value to get the same effect. It is a little clunkier to read, but works in the same way.
`{ "allOf": [{"$ref": "./person-closed.json"}]}`
:::

The code generator generates types for both `PersonOpen` and `PersonWealthy`. When it encounters the `$ref` in `person-wealthy.json`, it follows the reference to `person-open.json`, resolves all the properties defined there, and composes them into the `PersonWealthy` type alongside its own declared properties.

`PersonWealthy` includes all of the properties defined both on `PersonOpen` and `PersonWealthy`:

- `BirthDate`
- `FamilyName`
- `GivenName`
- `OtherNames`
- `Height`
- `Wealth`

## Generated Code Usage

### Creating an extended type

[Example code](./Program.cs)

```csharp
using Corvus.Text.Json;
using ExtendingABaseType.Models;
using NodaTime;

// A wealthy person has an additional required property: "Wealth".
using JsonWorkspace workspace = JsonWorkspace.Create();
using var wealthyDoc = PersonWealthy.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    wealth: 1_000_000,
    height: 1.57);

int wealth = wealthyDoc.RootElement.Wealth;
Console.WriteLine($"Wealth: {wealth}");
```

### Converting to the base type

```csharp
string json = wealthyDoc.RootElement.ToString();
using var parsedWealthy = ParsedJsonDocument<PersonWealthy>.Parse(json);
PersonWealthy wealthyPerson = parsedWealthy.RootElement;

// Convert to the type on which it is based (PersonOpen).
PersonOpen basePerson = PersonOpen.From(wealthyPerson);
```

### Accessing properties on the base type

There are three ways to access properties on a JSON element:

1. **Strongly-typed property** (e.g. `wealthyPerson.Wealth`) — use this when you have an instance of the type that declares the property. This is the most natural and efficient approach.
2. **`TryGetProperty` with a UTF-8 byte literal** (e.g. `"wealth"u8`) — use this when the property is present in the underlying JSON but not declared on the .NET type you are working with, such as when you have converted to a base type like `PersonOpen`.
3. **`TryGetProperty` with a string** (e.g. `"wealth"`) — another dynamic option, convenient when you already have a string property name.

The `TryGetProperty` approach is especially useful when working with open base types. After converting a `PersonWealthy` to `PersonOpen` with `PersonOpen.From()`, the `wealth` property still exists in the underlying JSON data — it is just not surfaced as a strongly-typed .NET property on `PersonOpen`.

```csharp
// Although the property is not present on the PersonOpen .NET type, it is still
// available through the generic TryGetProperty() API.
if (basePerson.TryGetProperty("wealth"u8, out JsonElement wealthElement))
{
    // We will only get here if wealthElement is not undefined.
    // We know our PersonOpen was derived from a valid PersonWealthy so we do not
    // need to re-validate; if it was present, then it is known to be an int32.
    Console.WriteLine($"Wealth: {wealthElement.GetInt32()}");
}

// Or we could just use a literal string as the property name.
if (basePerson.TryGetProperty("wealth", out JsonElement wealthElement2))
{
    Console.WriteLine($"Wealth: {wealthElement2.GetInt32()}");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create directly with static Create method
PersonWealthy wealthy = PersonWealthy.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    wealth: 1_000_000,
    height: 1.57);

// Access properties directly
int wealth = wealthy.Wealth;

// Convert to base type with As<T>()
PersonOpen basePerson = wealthy.As<PersonOpen>();

// Access undeclared property via TryGetProperty
if (basePerson.TryGetProperty("wealth"u8, out var wealthElement))
{
    int w = wealthElement.GetInt32();
}
```

### V5 (Corvus.Text.Json)
```csharp
// Create with workspace and builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var wealthyDoc = PersonWealthy.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    wealth: 1_000_000,
    height: 1.57);

// Access properties directly (same as V4)
int wealth = wealthyDoc.RootElement.Wealth;

// Convert to base type with From()
PersonOpen basePerson = PersonOpen.From(wealthyDoc.RootElement);

// Access undeclared property via TryGetProperty (same as V4)
if (basePerson.TryGetProperty("wealth"u8, out JsonElement wealthElement))
{
    int w = wealthElement.GetInt32();
}
```

**Key differences:**
- V5 uses `CreateBuilder(workspace, prop: value)` instead of V4's `Create()` static method
- V5 requires a `JsonWorkspace` for construction and manages lifetime with `using`
- V5 uses `From()` for type conversion instead of `As<T>()`
- Property access via strongly-typed properties and `TryGetProperty` works the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/005-ExtendingABaseType
dotnet run
```

## Related Patterns

- [003-ReusingCommonTypes](../003-ReusingCommonTypes/) - Using `$ref` and `$defs` for shared type definitions
- [004-OpenVersusClosedTypes](../004-OpenVersusClosedTypes/) - Understanding open vs closed types
- [006-ConstrainingABaseType](../006-ConstrainingABaseType/) - Narrowing constraints instead of adding properties
- [011-InterfacesAndMixInTypes](../011-InterfacesAndMixInTypes/) - Composing multiple schemas with `allOf`

## Frequently Asked Questions

### How is `$ref` like inheritance in OOP?

Think of `$ref` as single inheritance: the base schema defines a set of properties and constraints, and the extending schema adds its own on top. The key difference from OOP is that JSON Schema composes *constraints* — you can't "override" a base property to weaken its constraints, only add more. The extending schema's properties are merged with those from the referenced schema, and all constraints from both apply during validation.

### Can I access base-type properties on the extended type?

Yes. When the code generator encounters `$ref`, it follows the reference and composes all properties from the base schema into the generated type. `PersonWealthy` has strongly-typed properties for both its own `Wealth` and all of `PersonOpen`'s properties (`FamilyName`, `GivenName`, `BirthDate`, etc.). You access them the same way — `wealthyPerson.FamilyName`.

### When should I use `TryGetProperty` instead of a strongly-typed property?

Use `TryGetProperty` when you have converted to a base type (e.g., `PersonOpen.From(wealthyPerson)`) and need to access a property that exists in the JSON but is not declared on the base .NET type. The property is still present in the underlying JSON data; `TryGetProperty` with a UTF-8 byte literal (`"wealth"u8`) gives you zero-allocation access to it. Use the strongly-typed property whenever it is available on the type you are working with.

### Does the base type need to be open for extension to work?

Yes. If the base type has `unevaluatedProperties: false` (closed), then any document with properties not declared in that schema will fail validation against the base. You can still *compose* schemas using `$ref` on a closed type (see [006-ConstrainingABaseType](../006-ConstrainingABaseType/)), but you cannot add new properties — only narrow existing constraints.

### What about draft 6/7 where `$ref` ignores adjacent keywords?

In draft 6 and 7, `$ref` replaces the entire schema object — sibling keywords like `properties` and `required` are silently ignored. The workaround is to wrap the reference in an `allOf`: `{ "allOf": [{"$ref": "./person-open.json"}], "properties": { ... } }`. This achieves the same composition but is less readable. Draft 2019-09 and later allow `$ref` alongside other keywords, which is what this recipe uses.
