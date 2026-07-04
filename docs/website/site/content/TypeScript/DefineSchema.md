---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "Define a schema"
---
## Define a schema

Create a file called `person.json`:

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "Person",
  "type": "object",
  "required": [ "familyName" ],
  "properties": {
    "familyName": { "type": "string" },
    "givenName": { "type": "string" },
    "birthDate": { "type": "string", "format": "date" },
    "height": { "type": "number" }
  }
}
```

The `title` (`Person`) becomes the generated type name. `familyName` is required; the other properties are optional. `birthDate` has a `format`, which becomes a branded type with a validating factory.
