# JSON Schema Patterns in .NET - Constraining a Base Type

This recipe demonstrates how to use `$ref` to base a new schema on an existing type and add tighter constraints to its properties, showing how JSON Schema composes constraints from both base and derived schemas.

## The Pattern

Whether a type is [open or closed](../004-OpenVersusClosedTypes/), you can further constrain it in a schema.

Remember that a closed type in JSON Schema is not like a "sealed" type in an OO language like C#. You can still base another schema on that type - it's just that it will not allow extra properties that are not present on the base type.

In this case, we will use `$ref` to declare that we are basing our new schema on the `person-closed.json` schema.

Notice how we can reference schema in external documents, not just the local schema document.

:::aside
This would work equally well with the `person-open.json` we used when [extending a base type.](../005-ExtendingABaseType/)
:::

We then constrain it by defining a new version of the `height` property.

Again, we use `$ref` to base the new schema for the `height` property on the schema of the `height` property on the base schema. Then we add a new `minimum` value to constrain it to be greater than `2.0`.

Notice how we don't just have to reference schemas defined in a `$defs` section. We can reference any schema defined in the document.

I don't generally encourage this pattern; ideally if a type is *intended* to be shared and composed in this way, I like to indicate that by embedding it in the `$defs` section and documenting it properly.

## The Schemas

File: `person-tall.json`

```json
{
  "title": "A tall person",
  "$ref": "./person-closed.json",
  "properties": {
    "height": {
      "$ref": "./person-closed.json#/properties/height",
      "minimum": 2.0
    }
  }
}
```

:::aside
In draft 6 and draft 7, `$ref` cannot be used in this way. It acts as a reference to the target type and *ignores* the adjacent values. This is often a bit surprising! Instead, you can use `allOf` with a single value to get the same effect. It is a little clunkier to read, but works in the same way.
`{ "allOf": [{"$ref": "./person-closed.json"}]}`
:::

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

When we validate, the `Height` property on `PersonTall` will be constrained *both* by characteristics of the base type (such that `0 < height <= 3.0`) *and* the new `height >= 2.0`. The new constraints are composed from *both* the base *and* the derived schema.

This "composition" behaviour is often surprising to people familiar with OO languages. You can't "turn off" the base constraints in the way you can with, say, C# virtual methods that do not call the base from the derived type, because they are composed together.

You may also be surprised to learn that it is not even necessary to include this `$ref` to the base "height" schema to get the desired validation behaviour! If you just added the `minimum` constraint to the new `height` property, it would validate as desired. This is because the constraints are _composed_ and all are applied. In fact, it is slightly more efficient *not* to include the $ref as it could cause those constraints to be evaluated twice.

However, `$ref` is the way we describe to the code generator that we are basing this new `HeightEntity` type on the one from the base schema, rather than merely defining an isolated constraint. In that way, the generated type inherits all the other characteristics from the one on which it is based.

### Are your constraints compatible?

Another consequence of this composition behaviour is that any additional constraints you apply should be compatible with the base type's existing constraints (if any).

Neither JSON Schema itself, nor Corvus.Text.Json enforce this rule - but any schema that doesn't follow it will not be especially useful!

Imagine, for example, defining a schema that *required* an entity to be both an `object` *and* a `string`. That's perfectly possible in JSON Schema; and the code generator will emit "correct" code that happily compiles. But no entity will ever be valid.

(Contrast this with a constraint that an entity can be *either* an `object` *or* a `string` - that would be absolutely fine; this kind of "sum type" is common in e.g. typescript, but is unusual in .NET languages. We will see later how Corvus.Text.Json enables these kinds of structures in dotnet.)

The code generator produces both `PersonTall` and `PersonClosed`; each has its own `HeightEntity`.

## Generated Code Usage

### Creating a constrained type and validating

[Example code](./Program.cs)

```csharp
using Corvus.Text.Json;
using ConstrainingABaseType.Models;
using NodaTime;

// Create a tall person, although Anne is not tall enough to be a valid tall person!
using JsonWorkspace workspace = JsonWorkspace.Create();
using var tallDoc = PersonTall.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

string json = tallDoc.RootElement.ToString();
using var parsedTall = ParsedJsonDocument<PersonTall>.Parse(json);
PersonTall personTall = parsedTall.RootElement;

// personTall is not valid because of the additional constraint.
if (personTall.EvaluateSchema())
{
    Console.WriteLine("personTall is valid");
}
else
{
    Console.WriteLine("personTall is not valid");
}
```

### Converting to the less-constrained base type

```csharp
// Convert to the base type
PersonClosed personClosedFromTall = PersonClosed.From(personTall);

// But personClosedFromTall is valid because it does not have the
// additional constraint.
if (personClosedFromTall.EvaluateSchema())
{
    Console.WriteLine("personClosedFromTall is valid");
}
else
{
    Console.WriteLine("personClosedFromTall is not valid");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create directly with static Create method
PersonTall personTall = PersonTall.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

// Validate
bool isValid = personTall.IsValid();

// Convert to the base type with As<T>()
PersonClosed personClosed = personTall.As<PersonClosed>();
```

### V5 (Corvus.Text.Json)
```csharp
// Create with workspace and builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var tallDoc = PersonTall.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

// Parse for validation
string json = tallDoc.RootElement.ToString();
using var parsedTall = ParsedJsonDocument<PersonTall>.Parse(json);
PersonTall personTall = parsedTall.RootElement;

// Validate
bool isValid = personTall.EvaluateSchema();

// Convert to the base type with From()
PersonClosed personClosed = PersonClosed.From(personTall);
```

**Key differences:**
- V5 uses `CreateBuilder(workspace, prop: value)` instead of V4's `Create()` static method
- V5 requires a `JsonWorkspace` for construction and manages lifetime with `using`
- V5 uses `EvaluateSchema()` instead of `IsValid()` for schema validation
- V5 uses `From()` for type conversion instead of `As<T>()`
- V5 uses `ParsedJsonDocument<T>.Parse()` for parsing JSON strings

## Running the Example

```bash
cd docs/ExampleRecipes/006-ConstrainingABaseType
dotnet run
```

## Related Patterns

- [002-DataObjectValidation](../002-DataObjectValidation/) - Schema validation fundamentals
- [004-OpenVersusClosedTypes](../004-OpenVersusClosedTypes/) - Understanding open vs closed types
- [005-ExtendingABaseType](../005-ExtendingABaseType/) - Adding properties to a base type instead of constraints

## Frequently Asked Questions

### How does constraint composition work in JSON Schema?

When you use `$ref` to base a new schema on an existing one, all constraints from both schemas are applied together during validation. For example, if the base defines `exclusiveMinimum: 0.0` and `maximum: 3.0` for `height`, and the derived schema adds `minimum: 2.0`, then a valid `height` must satisfy *all three*: greater than 0, at most 3.0, and at least 2.0. You cannot "turn off" base constraints — they always compose additively.

### What happens if I define incompatible constraints?

JSON Schema does not prevent you from writing contradictory constraints (e.g., requiring a value to be both a `string` and an `object`). The code generator will produce valid, compilable C# code. However, no JSON document will ever satisfy both constraints simultaneously, so `EvaluateSchema()` will always return `false`. Always ensure your derived constraints are a subset of the base constraints — narrowing the valid range, not contradicting it.

### Why use `$ref` to the base property when just adding a new constraint would also validate correctly?

The `$ref` to the base property's schema is not strictly necessary for validation — the constraints compose regardless. However, `$ref` tells the code generator that the derived `HeightEntity` is based on the base `HeightEntity`, so the generated type inherits all the characteristics (format, type mappings, helper methods) from the original. Without the `$ref`, the code generator treats the new constraint as an independent definition and may not carry over the base type's code-generation behaviour.

### Can I constrain a closed type even though it has `unevaluatedProperties: false`?

Yes. A closed type prevents *new properties* from being added, but you can still narrow existing property constraints. This recipe does exactly that: `person-closed.json` has `unevaluatedProperties: false`, and `person-tall.json` references it while adding a `minimum: 2.0` constraint to the existing `height` property. No new properties are introduced, so the closed-type restriction is not violated.
