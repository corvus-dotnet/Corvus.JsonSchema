# Python patterns, conditional schemas

This recipe shows `if` / `then` / `else`, applying a constraint conditionally on a value's contents, and how
the Corvus.Text.Json **Python** generator keeps that rule in the evaluator rather than the type.

## The pattern

`if` / `then` / `else` makes a constraint depend on the data. When the `if` subschema matches, `then` must also
hold (otherwise `else`). Here, when `method` is `"card"`, `cardNumber` becomes required. This is a *constraint*,
not a shape change. The generated `TypedDict` keeps `cardNumber` optional, and `evaluate` enforces the
conditional requirement.

TypedDict cannot express "if method is card then cardNumber is required" as a single object type without
splitting it into a union. The generator keeps the base shape (`cardNumber` optional) and enforces the rule in
`evaluate`. If you want the *type* to capture the two cases, model them as a `oneOf` (see recipe 011) instead.

- `evaluate(value)`. The AOT-compiled boolean evaluator. It applies the `if` / `then` branch as part of
  whole-document evaluation, accepting the wire bytes directly (it decodes them) or an already-parsed value.
- `build_payment(props)` / `build_canonical_payment(props)`. Plain values to compact / RFC 8785 bytes.
- `parse(data)`. Decode `bytes | str` into a typed `Payment`.

## The schema

```json
{ "title": "Payment", "type": "object", "required": ["method"],
  "properties": { "method": { "enum": ["card", "cash"] }, "cardNumber": { "type": "string" } },
  "if": { "properties": { "method": { "const": "card" } } },
  "then": { "required": ["cardNumber"] } }
```

## The generated surface

```python
class Payment(TypedDict, total=False):
    cardNumber: str
    method: Required[Method2]


Method2 = Literal["card", "cash"]
```

The `if` / `then` subschemas are compiled into `evaluate`; they add no members to the `Payment` type.

## The demo

See [`demo.py`](./demo.py). It builds a valid cash payment, evaluates a card payment with and without its
number, and parses the bytes back to a typed value.

```
1. built:         {"method":"cash"}
2. cash valid:    True
3. card + number: True
4. card, no num:  False
5. method:        cash
```

Line 4 is the conditional at work. `{"method": "card"}` satisfies the base shape (both `cardNumber` optional),
but the `if` matches so the `then` clause requires `cardNumber`, and `evaluate` returns `False`.

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
