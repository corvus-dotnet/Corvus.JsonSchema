---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-29T00:00:00.0+00:00
Title: "Setup and Schema Design"
---
## Getting started

First, install the code generator tool globally:

```
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool --prerelease
```

Create a console app and add the extended types package:

```
dotnet new console -o JsonSchemaSample -f net8.0
cd JsonSchemaSample
dotnet add package Corvus.Json.ExtendedTypes
```

## Designing with JSON Schema

We will work with a JSON Schema document describing a `Person`. Here is the complete schema — save it as `api/person-from-api.json` in your project:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "JSON Schema for a Person entity coming back from a 3rd party API",
  "$defs": {
    "Person": {
      "type": "object",
      "required":  ["name"],
      "properties": {
        "name": { "$ref": "#/$defs/PersonName" },
        "dateOfBirth": {
          "type": "string",
          "format": "date"
        }
      }
    },
    "PersonName": {
      "type": "object",
      "description": "A name of a person.",
      "required": [ "familyName" ],
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
          "description": "Other (middle) names for the person"
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
      "items": {
        "$ref": "#/$defs/PersonNameElement"
      }
    },
    "PersonNameElement": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    },
    "Link":
    {
      "required": [ "href" ],
      "type": "object",
      "properties": {
        "href": {
          "title": "URI of the target resource",
          "type": "string",
          "description": "Either a URI [RFC3986] or URI Template [RFC6570] of the target resource."
        },
        "templated": {
          "title": "URI Template",
          "type": "boolean",
          "description": "Is true when the link object's href property is a URI Template. Defaults to false.",
          "default": false
        },
        "type": {
          "title": "Media type indication of the target resource",
          "pattern": "^(application|audio|example|image|message|model|multipart|text|video)\\\\/[a-zA-Z0-9!#\\\\$&\\\\.\\\\+-\\\\^_]{1,127}$",
          "type": "string"
        },
        "name": {
          "title": "Secondary key",
          "type": "string"
        },
        "profile": {
          "title": "Additional semantics of the target resource",
          "type": "string",
          "format": "uri"
        },
        "description": {
          "title": "Human-readable identifier",
          "type": "string"
        },
        "hreflang": {
          "title": "Language indication of the target resource [RFC5988]",
          "type": "string"
        }
      }
    }
  }
}
```

### Key schema features

- **`Person`** — an object with a required `name` property and an optional `dateOfBirth` (a `date`-formatted string)
- **`PersonName`** — an object with a required `familyName` and optional `givenName` and `otherNames`
- **`PersonNameElement`** — a string with length constraints (1–256 characters)
- **`OtherNames`** — a `oneOf` union: either a single `PersonNameElement` string or a `PersonNameElementArray`
- **`Link`** — a HAL-style web link (not referenced by `Person`, included for later use)

The `OtherNames` union type allows backwards-compatible API evolution — a previous version might support only a single string, while a newer version adds the array form.

## Generating C# code

Run the code generator, specifying the root namespace and the path to the `Person` definition:

```
corvusjson jsonschema --rootNamespace JsonSchemaSample.Api --rootPath #/$defs/Person person-from-api.json --engine V4
```

This produces files for each schema element reachable from `Person`:

| Schema location | Files |
| --- | --- |
| `#/$defs/Person` | `Person.cs`, `Person.Object.cs`, `Person.Validate.cs` |
| `#/$defs/PersonName` | `PersonName.cs`, `PersonName.Object.cs`, `PersonName.Validate.cs` |
| `#/$defs/PersonNameElement` | `PersonNameElement.cs`, `PersonNameElement.String.cs`, `PersonNameElement.Validate.cs` |
| `#/$defs/OtherNames` | `OtherNames.cs`, `OtherNames.Array.cs`, `OtherNames.String.cs`, `OtherNames.Validate.cs` |
| `#/$defs/PersonNameElementArray` | `PersonNameElementArray.cs`, `PersonNameElementArray.Validate.cs` |

Note that the `Link` schema was **not** generated — the tool only generates types reachable from the root path.

Build the project to verify everything compiles:

```
cd ..
dotnet build
```