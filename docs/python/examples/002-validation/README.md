# Python patterns, validation

This recipe adds **constraints** to a data object (length, range, pattern, numeric step, format) and shows that
the generated `evaluate` enforces them while the `TypedDict` stays the plain shape.

## The pattern

A JSON Schema validation keyword (`minLength`, `pattern`, `minimum`/`maximum`, `multipleOf`, `format`, ...)
constrains a value beyond its type. The generator splits the two concerns. The **`TypedDict` is the shape**, and
**`evaluate` is the constraint authority**. Evaluation returns a plain `bool`. A rejection is `False`, with no
raised exception and no allocated error graph. (Collecting *why* a value failed, the per-keyword results, is a
separate opt-in through a `Results` collector.)

A `format` keyword additionally produces a **branded** `NewType` with an eager factory (`from_email`) that
raises `FormatError` on a malformed value at construction. This is useful when you want to fail fast on one field
rather than evaluate a whole document.

- `evaluate(value)`. An AOT-compiled boolean evaluator. It accepts the wire bytes directly (it decodes them) or
  an already-parsed value.
- `build_registration(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `from_email(value)`. A validating factory for the `email`-formatted branded type. It raises `FormatError` on a
  bad value.

Numeric constraints are exact. `minimum`/`maximum`/`multipleOf` are checked against the number's source text, not
a lossy IEEE-754 double, so `multipleOf: 0.5` behaves mathematically.

## The schema

```json
{
  "title": "Registration",
  "type": "object",
  "required": ["username", "age", "email"],
  "properties": {
    "username": { "type": "string", "minLength": 3, "maxLength": 20, "pattern": "^[a-z][a-z0-9_]*$" },
    "age": { "type": "integer", "minimum": 18, "maximum": 120 },
    "email": { "type": "string", "format": "email" },
    "score": { "type": "number", "exclusiveMinimum": 0, "multipleOf": 0.5 }
  }
}
```

## The generated surface

```python
class Registration(TypedDict, total=False):
    age: Required[int]
    email: Required[Email]
    score: float
    username: Required[str]


Email = NewType("Email", str)
```

## The demo

See [`demo.py`](./demo.py). It builds a valid `Registration`, evaluates trusted bytes, rejects four different
constraint violations (each just `False`), then shows the `format` brand raising eagerly at construction.

```
1. built:          {"username":"ada_lovelace","age":36,"email":"ada@example.com","score":4.5}
2. valid:          True
3. short username: False
   bad pattern:    False
   under age:      False
   score step:     False
4. from_email raised: value does not match format 'email'
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
