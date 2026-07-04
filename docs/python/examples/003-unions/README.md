# Python patterns, untagged unions

This recipe shows how a `oneOf` (or `anyOf`) union is projected: a `type` alias of the member types, a
`TypeGuard` per member, and a generic `match` dispatcher.

## The pattern

The generator emits, for a union schema (here `Value`):

- `type Value = str | float | bool`. A PEP 695 type alias (lazy, so a recursive union does not deadlock at
  import).
- `is_one_of(value)`, `is_one_of2(value)`, `is_one_of3(value)`. One `typing.TypeGuard` per member, backed by
  that member's validator. Each narrows a value on its own.
- `match_value[R](value, *, one_of, one_of2, one_of3) -> R`. A generic dispatcher that tries each member guard
  in order and calls the matching keyword-only handler, raising if nothing matched. Because it is generic in the
  return type, every branch produces the same type.

Member handler names are the member module names (`one_of`, `one_of2`, ...). The dispatch respects JSON's type
model, so a `True` value takes the boolean branch rather than the number branch even though `bool` is an `int`
in Python.

## The schema

```json
{
  "title": "Value",
  "oneOf": [
    { "type": "string" },
    { "type": "number" },
    { "type": "boolean" }
  ]
}
```

## The demo

See [`demo.py`](./demo.py).

```
string 'hello'
number 42
boolean True
length: 5
```

## Running it

From `docs/python/examples`:

```bash
pwsh ./regenerate.ps1 -Check
```
