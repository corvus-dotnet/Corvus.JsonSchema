---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Define a JSON Schema"
---
## Our example schema

This is the JSON Schema file we will work with throughout this Getting Started guide. It defines a `Person` type with nested structures, constrained values, and formatted strings — a realistic example that exercises many of the features Corvus.Text.Json provides.

Create a JSON Schema file, for example `Schemas/person.json`:

```json
{
    "$schema": "https://json-schema.org/draft/2020-12/schema",
    "title": "Person",
    "$defs": {
        "PersonName": {
            "type": "object",
            "description": "A name of a person.",
            "required": ["familyName"],
            "properties": {
                "givenName": {
                    "$ref": "#/$defs/PersonNameElement",
                    "description": "The person's given name."
                },
                "familyName": {
                    "$ref": "#/$defs/PersonNameElement",
                    "description": "The person's family name."
                },
                "otherNames": {
                    "$ref": "#/$defs/OtherNames",
                    "description": "Other (middle) names."
                }
            }
        },
        "OtherNames": {
            "oneOf": [
                { "$ref": "#/$defs/PersonNameElement" },
                { "$ref": "#/$defs/PersonNameElementArray" }
            ]
        },
        "PersonNameElementArray": {
            "type": "array",
            "items": { "$ref": "#/$defs/PersonNameElement" }
        },
        "PersonNameElement": {
            "type": "string",
            "minLength": 1,
            "maxLength": 256
        },
        "Address": {
            "allOf": [
                { "$ref": "#/$defs/Location" },
                {
                    "type": "object",
                    "properties": {
                        "street": { "type": "string" },
                        "zipCode": { "type": "string" }
                    }
                }
            ]
        },
        "Location": {
            "type": "object",
            "properties": {
                "city": { "type": "string" },
                "country": { "type": "string" }
            }
        }
    },
    "type": "object",
    "required": ["name"],
    "properties": {
        "name": { "$ref": "#/$defs/PersonName" },
        "dateOfBirth": {
            "type": "string",
            "format": "date"
        },
        "age": {
            "type": "integer",
            "format": "int32",
            "minimum": 0,
            "maximum": 150
        },
        "email": {
            "type": "string",
            "format": "email"
        },
        "address": { "$ref": "#/$defs/Address" },
        "hobbies": {
            "type": "array",
            "items": { "type": "string" }
        }
    }
}
```

Both draft 2020-12 and draft 2019-09 are supported. If your schema does not declare a `$schema` keyword, you can set the fallback vocabulary via MSBuild properties.

## Understanding the schema

The `Person` type is an object with a required `name` property and several optional properties: `dateOfBirth` (a formatted date string), `age` (an integer from 0 to 150), `email`, `address` (a composed object), and `hobbies` (an array of strings). The `name` property references a `PersonName` definition via `$ref`.

`PersonName` has a required `familyName` and optional `givenName` — both constrained to 1–256 character strings via the `PersonNameElement` definition. The `otherNames` property uses `oneOf` to accept *either* a single string *or* an array of strings. This is a common JSON Schema pattern for backwards-compatible API evolution:

```json
// A single string
{ "familyName": "Oldroyd", "givenName": "Michael", "otherNames": "Francis James" }

// Or an array of strings
{ "familyName": "Oldroyd", "givenName": "Michael", "otherNames": ["Francis", "James"] }
```

The `Address` type uses `allOf` to compose a base `Location` (with `city` and `country`) with address-specific properties (`street` and `zipCode`). The generated type merges all properties from both schemas into a single struct:

```json
{ "street": "123 Main St", "zipCode": "SP1 1AA", "city": "Springfield", "country": "UK" }
```

## How generated types work

Generated types are `readonly struct` values that act as thin wrappers over the underlying JSON data. When you parse a JSON document, the data is stored as UTF-8 bytes in pooled memory. The generated struct is essentially an index into that data — it doesn't copy or deserialize the JSON upfront.

Values are converted to .NET primitives like `string`, `int`, or `LocalDate` only at the point of use, when you access a property or perform a cast. This "just-in-time" model means:

- **No allocation on construction** — creating a `Person` from a parsed document is essentially free (a small struct on the stack).
- **No redundant copying** — the underlying UTF-8 bytes are shared, not cloned.
- **Conversion cost is deferred** — you only pay for what you access.

The code generator walks the schema tree from the root type and generates C# for every schema it encounters. Each schema element typically produces multiple partial-class files by concern (e.g., `Person.cs`, `Person.JsonSchema.cs`, `Person.Mutable.cs`). Nested entity types like `PersonName` become nested structs within the parent type (e.g., `Person.PersonNameEntity`).

