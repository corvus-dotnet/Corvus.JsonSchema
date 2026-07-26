# Corvus.Json.CodeGeneration.OpenApi20

Defines the vocabulary for the OpenAPI 2.0 (Swagger) schema-object dialect.

The dialect is JSON Schema draft-04 based, taking the draft-04 validation keywords
with boolean `exclusiveMinimum`/`exclusiveMaximum` modifiers, plus the OpenAPI 2.0
fixed fields (`discriminator` in its string form, `readOnly`, `xml`, `externalDocs`,
`example`) and the widely-adopted `x-nullable` vendor convention.

The vocabulary is deliberately a practical superset of the specification: `oneOf`,
`anyOf`, `not`, `nullable`, and nested `definitions` are accepted even though the
OpenAPI 2.0 specification omits them, because real-world Swagger documents use them
routinely and silently ignoring them would both change validation semantics and drop
`$ref` targets from the generated type graph. `required` is active only in its
draft-04 array form; the boolean form found on Parameter Objects is inert.
