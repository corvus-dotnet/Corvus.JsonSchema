# OpenAPI client fixtures

Language-neutral conformance cases for the generated OpenAPI clients. Every language's client is driven through the
same cases, the way every language's evaluator is driven through the JSON Schema Test Suite, so "the same results
everywhere" is measured, not assumed. The .NET client runs them first and proves them.

## Layout

```
tests/OpenApi-Client-Fixtures/
  fixture.schema.json                      the schema of a fixture file
  <document>/<operationId>.json            cases for an operation of that document
```

`<document>` is the base name of the OpenAPI document a client runtime test project generates its client from:
`canonicalization-spec` (3.1), `canonicalization-spec-3.0`, `canonicalization-spec-3.2` and `covspec-2.0`, found
in those projects' `TestData` folders. A fixture file names its document and operation; its cases are independent
of each other. Each runtime test project copies its document's fixture files beside the test assembly and runs them
through its generated adapter.

## How a case runs

1. The runner builds the operation's typed inputs from `input` through the generated fixture adapter (a test-only
   file the C# emitter writes when the CLI is given the hidden `--emit-fixture-adapter` option; every language's
   emitter writes its own), calls the generated client method over a mock transport that captures the request and
   answers with `response`, and receives the typed outcome projected back to JSON.
2. It compares the captured request with `expectRequest` and the projected outcome with `expect`.

The adapter is the only per-language code a case touches, and it is generated, not written.

## A case

```json
{
  "spec": "covspec-3.1.json",
  "operationId": "getItem",
  "cases": [
    {
      "name": "200 parses the Ok body",
      "rows": [3, 4, 33, 38],
      "input": {
        "path": { "itemId": "item-1" },
        "query": { "limit": 5 },
        "header": { "X-Request-Id": "req-42" }
      },
      "response": {
        "status": 200,
        "headers": { "Content-Type": "application/json" },
        "body": { "id": "item-1", "name": "Widget", "price": 9.99 }
      },
      "expectRequest": {
        "method": "GET",
        "path": "/items/item-1?limit=5",
        "headers": { "accept": "application/json", "x-request-id": "req-42" }
      },
      "expect": {
        "kind": "ok",
        "status": 200,
        "case": "Ok",
        "body": { "id": "item-1", "name": "Widget", "price": 9.99 }
      }
    }
  ]
}
```

`rows` are the rows of the .NET parity checklist
(`docs/proposals/OpenApiClientsForOtherLanguages-DotNetParity.md`) the case exercises.

### `input`

Values are JSON, keyed by the parameter name as written in the document, under `path`, `query`, `header` and
`cookie`. A JSON request body is `body`; a text body is `bodyText`; a binary body is `bodyBase64`. `validation`
(`none`, `basic`, `detailed`; default `basic`) and `responseValidation` (default `none`) select the validation modes
the client method is called with. An absent optional input is not supplied to the client.

### `response`

What the mock transport answers: `status`, `headers` (as sent on the wire; the mock sets `Content-Type` from the
body kind when the case gives none) and a `body`, `bodyText` or `bodyBase64`.

### `expectRequest`

What the client must have sent. `method` is upper-case. `path` is the request path and query, percent-encoded,
exactly as sent, relative to the mock's base URL (`http://localhost`). `headers` lists the headers to assert, with
lower-case names and exact values; headers not listed are not checked, and `absentHeaders` lists names that must not
be present. `contentType` is the media type of the body without parameters. A `body` is compared as JSON (RFC 8785
canonical form), so serialisers may differ in whitespace and property order; `bodyText` and `bodyForm` are compared
exactly; `bodyBase64` is compared byte for byte. A multipart body is `bodyMultipart`: a list of parts, each with
`headers` and a `body`, `bodyText` or `bodyBase64`, compared after the boundary is stripped.

### `expect`

`kind` is `ok` when the client method returned, or `error` when it threw before or after sending (a request
validation failure, a response validation failure, an unexpected status). For `ok`: `status`, `case` (the
generated name of the matched response: `Ok`, `Created`, `NotFound`, `Default`, `Status2xx`, ...), `body` (JSON the
typed body projects to), `bodyText`, and `headers` (each typed response header projected to JSON, `null` when
absent). An `ok` expectation that omits `case` asserts that no response case matched. For `error`: `error.contains`
is a substring the message must contain, and it is required, so an error case never passes on an unrelated
exception.

### `skip`

A map of language identifier to reason. A language's runner reports a skipped case as skipped, never as passed.

## Normalisation rules

- Header names are compared case-insensitively; the file writes them lower-case.
- The request path and query are compared as the client wrote them, before any URI normalisation by the
  language's HTTP stack, so an over- or under-encoding client fails even where the wire form would coincide.
- Query strings are compared as sent, in order; a case that admits more than one order lists `pathAnyOf`.
- JSON bodies are compared canonically. Numbers compare by value in canonical form, so `1.0` and `1` differ, as
  the wire bytes do.
- Multipart boundaries are stripped and parts are compared in order.
- Streamed response bodies (SSE, NDJSON) are given as `chunks`, delivered to the client in that order; the projected
  outcome lists the items the client yielded.

## Authoring

One or more cases per parity-checklist row, over the covspec operations that exercise it. A case asserts one
thing in its name. Expected wire bytes come from the OpenAPI specification's rules, not from what a generator
happens to emit; where the .NET client disagrees with the specification, the case records the specification and
the .NET client is fixed.
