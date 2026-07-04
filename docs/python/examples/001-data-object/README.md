# Python patterns, simple data objects

This recipe defines a simple data object of primitive values in JSON Schema and shows how the Corvus.Text.Json
**Python** generator produces an idiomatic `TypedDict` plus a small, allocation-lean API to evaluate, read,
build and mutate it.

## The pattern

It is common to define a data object of primitive values for exchange through an API.

The generator emits, for the root schema, a `TypedDict` whose name is derived from the schema (here `Person`).
Required properties are typed with `Required[T]`, optional properties are plain `T` on a `total=False` class.
JSON Schema scalar types map to Python primitives (`str`, `float`, `bool`), and a `format` keyword becomes a
**branded** `NewType` with a validating factory (here `birth_date` is `NewType("BirthDate", str)` reached
through `from_birth_date(...)`).

There is nothing to wrap. A value parsed from JSON *is* a `Person` once `evaluate` accepts it, and you read it
with ordinary subscripting. The generator adds only what the language does not give you for free.

- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions, no error-object graph). It accepts the
  wire bytes directly (it decodes them) or an already-parsed value.
- `parse(data)`. Decode `bytes | str` into a typed `Person`.
- `build_person(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `build_canonical_person(props)`. The same, with RFC 8785 sorted keys.
- `patch_person(source, changes, removals)`. Change only the named fields, spliced at the byte level (unchanged
  bytes copied verbatim).
- `from_birth_date(value)`. A validating factory for the `date`-formatted branded type.
- `to_temporal_birth_date(value)`. Read the brand as a `whenever.Date`.

## The schema

```json
{
  "title": "Person",
  "type": "object",
  "required": ["family_name", "given_name"],
  "properties": {
    "family_name": { "type": "string" },
    "given_name": { "type": "string" },
    "birth_date": { "type": "string", "format": "date" },
    "height": { "type": "number" },
    "tags": { "type": "array", "items": { "type": "string" } }
  }
}
```

## The generated surface

```python
class Person(TypedDict, total=False):
    birth_date: BirthDate
    family_name: Required[str]
    given_name: Required[str]
    height: float
    tags: Tags


BirthDate = NewType("BirthDate", str)
```

## The demo

See [`demo.py`](./demo.py). It builds a `Person`, validates trusted and untrusted input, parses bytes back to
a typed value, reads the `date` brand as a temporal value, and applies a byte-native partial update.

```
1. built:         {"family_name":"Bronte","given_name":"Anne","birth_date":"1820-01-17","height":1.52,"tags":["author"]}
2. valid:         True
   missing reqd:  False
3. given_name:    Anne
4. birth date:    1820-01-17
5. patched:       {"family_name":"Bronte","given_name":"Anne","birth_date":"1820-01-17","height":1.53}
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
