# Python patterns, numeric enumerations

This recipe defines an `enum` of numbers in JSON Schema and shows how the Corvus.Text.Json **Python** generator
projects it as a numeric `Literal` union.

## The pattern

A numeric `enum` becomes a `Literal` type alias of the member numbers (here `Status = Literal[200, 404, 500]`),
statically checkable and completable. `evaluate` rejects any other number. Membership is compared by
mathematical value, so there is no floating-point surprise even for large or fractional members.

The generator emits, for the root schema, a `TypedDict` (`Response`) that types the enum property with its
`Literal` alias, plus the module-level API.

- `Status`. A `Literal[200, 404, 500]` alias. A parsed value narrows to this union, so an equality test against
  a member (`status == 200`) is statically an overlap and checks cleanly.
- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions). It accepts the wire bytes directly (it
  decodes them) or an already-parsed value, and fails any number outside the set.
- `parse(data)`. Decode `bytes | str` into a typed `Response`; the enum property narrows to its `Literal` alias.
- `build_response(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `build_canonical_response(props)`. The same, with RFC 8785 sorted keys.
- `patch_response(source, changes, removals)`. Change only the named fields, spliced at the byte level.

## The schema

```json
{ "title": "Response", "type": "object", "required": ["status"],
  "properties": { "status": { "enum": [200, 404, 500] } } }
```

## The generated surface

```python
class Response(TypedDict, total=False):
    status: Required[Status]


type Status = Literal[200, 404, 500]
```

## The demo

See [`demo.py`](./demo.py). It builds a `Response`, validates trusted and untrusted input, parses bytes back to
a typed value, and narrows the numeric `Literal` in an equality test.

```
1. built:      {"status":404}
2. valid:      True
   bad code:   False
3. status:     404
   ok?:        False
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
