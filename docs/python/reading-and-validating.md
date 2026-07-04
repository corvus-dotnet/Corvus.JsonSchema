# Reading and evaluating

This guide covers the read side of a generated module. The `TypedDict` surface you read values through, and
the boolean `evaluate` you validate them with.

## Reading the data

The generated `TypedDict` describes the parsed value directly. A value returned by `parse`, once it passes
evaluation, is an instance of the type. You read it with ordinary subscripting and iteration. There is no
wrapper object and no accessor layer. The parsed value is the value.

```python
from generated import parse

person = parse(data)
person["family_name"]          # str
"birth_date" in person         # optional, present?
```

Optionality is explicit. A required property is `Required[T]`, an optional one is a plain `T` on a
`total=False` class, so the checker knows an optional key may be absent. Read a required member by subscript
(`person["given_name"]`) and an optional one with `.get` or an `in` guard.

```python
birth_date = person.get("birth_date")
if birth_date is not None:
    ...                        # birth_date is present
```

An absent optional reads as a missing key. A property that may be the JSON literal `null` is typed `T | None`
and reads as `None`. The two are distinct in JSON Schema and in the generated type.

## From bytes or a parsed value

`build_<t>` / `patch_<t>` return UTF-8 JSON bytes, so reading a result back could mean decoding and parsing by
hand. The module gives you two shorter forms.

- `parse(data)` and the per-type `parse_<t>(data)` take `bytes | str` and return the value typed as the
  schema type. They do not validate. They are the convenience form of `decode_and_parse(data)` with a cast, so
  use them for trusted input, or call `evaluate` first.
- `evaluate(value)` accepts a parsed value or the JSON bytes directly (it decodes them), so `evaluate(data)`
  replaces `evaluate(parse(data))`.

```python
from generated import Person, build_person, evaluate, parse, from_birth_date

data = build_person({"family_name": "Bronte", "given_name": "Anne"})
evaluate(data)                 # validate the bytes directly
person = parse(data)           # -> Person (typed, unvalidated)
```

Numbers come back exact. `parse` decodes with `parse_float=Decimal`, so a JSON number arrives as an `int` or a
`decimal.Decimal` and keeps every digit, even past float precision. The declared model type of a `number`
field is `float`, but the runtime value is a `Decimal`. See [The runtime](./runtime.md#numbers-as-decimal).

For the untyped case, `decode_and_parse(data)` is re-exported from `corvus_json_runtime` (decode plus parse,
returning `object`).

## Evaluating: a boolean by default

The module's `evaluate(value, results=None)` validates a value against the root schema and returns a `bool`,
`True` when every constraint holds. It mirrors the .NET `EvaluateSchema()` and the TypeScript `evaluate`.
`evaluate` is the document entry point, re-exported at the top of the module.

```python
from generated import evaluate

evaluate(value)                # True | False
```

A rejection is just `False`. There is no thrown exception and no error object allocated on the "is this
acceptable?" path, which keeps validation inexpensive. To find out why a value failed, pass an optional
results collector as the second argument (see [Detailed results](#detailed-results)).

### Why it is `evaluate`, not `validate`

It is an evaluator. It walks the schema collecting evaluation results, which keywords applied to which
locations. That is what `unevaluatedProperties` / `unevaluatedItems` need, and what a results collector
reports. "Validate" would describe only the boolean outcome. "Evaluate" describes what it actually does.

The public boolean entry point is the root type's `evaluate`. Every type in the schema has an internal
`_eval_<t>` validator, and a parent threads its evaluation tracker into its children so that in-place
applicators (`allOf`, `if`, `$ref`) and `unevaluated*` keywords see a consistent picture. You do not see the
tracker. `evaluate` seeds a fresh one for the whole-value check.

## Detailed results

`evaluate(value, results)` takes an optional `Results` collector from the runtime. Omit it (the default) for
the zero-allocation boolean fast path. Pass one to gather why a value failed. Each `Failure` carries a
`keyword_location`, an `instance_location`, and an `absolute_keyword_location`, in the JSON Schema spec output
shape. Construct it with `verbose=True` to also collect annotations from subschemas that passed.

```python
from corvus_json_runtime import Results
from generated import evaluate

r = Results()
if not evaluate(value, r):
    for failure in r.failures:
        print(failure.instance_location, failure.keyword_location)
```

`r.valid` is `True` when no failure was recorded. On the fast path (`results` omitted) the validator computes
no locations at all.

## Narrowing unions

A `oneOf` / `anyOf` generates a union alias with a per-member `typing.TypeGuard` and a generic dispatcher. The
guards narrow a value on their own.

```python
from generated import Value, is_one_of, match_value

if is_one_of(maybe):
    len(maybe)                 # maybe narrowed to str here
```

`match_<t>` tries each member guard in order and calls the matching keyword-only handler. It is generic in the
return type, so every branch must produce the same type.

```python
described = match_value(
    v,
    one_of=lambda s: f"string {s!r}",
    one_of2=lambda n: f"number {n}",
    one_of3=lambda b: f"boolean {b}",
)
```

The dispatch respects JSON's type model, so a `True` value takes the boolean branch rather than the number
branch even though `bool` is an `int` in Python. See [examples/011-unions](./examples/011-unions/).

## Trusting evaluated data

After `evaluate(value)` returns `True`, the value matched the schema, so treating it as the typed value is
safe. A common pattern at a trust boundary, an HTTP body or a queue message, is to parse and gate the trust on
`evaluate`.

```python
from generated import Order, evaluate, parse

order = parse(body)            # bytes | str -> typed, but NOT yet validated
if not evaluate(order):
    return bad_request()       # evaluate is the trust gate
# from here, order is known to match the schema
```

## See also

- [the-type-surface](./the-type-surface.md), what each construct reads as.
- [mutation](./mutation.md), producing changed documents.
- [examples/001-data-object](./examples/001-data-object/), a runnable walkthrough.
