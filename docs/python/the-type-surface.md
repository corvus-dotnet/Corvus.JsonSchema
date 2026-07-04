# The type surface

How each JSON Schema construct maps to generated Python. Each row links to a runnable recipe.

| Construct | Generated Python | Recipe |
|-----------|------------------|--------|
| `type: object` + `properties` | `class T(TypedDict, total=False)` | [001](./examples/001-data-object/) |
| required / optional property | `name: Required[T]` / `name: T` on `total=False` | [001](./examples/001-data-object/) |
| `type: string` / `number` / `integer` / `boolean` / `null` | `str` / `float` / `int` / `bool` / `None` | [001](./examples/001-data-object/) |
| `format` (string) | `NewType` brand + `from_<t>` factory; the 4 temporal formats add `to_temporal_<t>` | [001](./examples/001-data-object/) |
| `format` (numeric) | `NewType` brand + range-checked `from_<t>`; `decimal` adds `to_exact_<t>` | |
| validation keywords (`minLength`, `minimum`, `pattern`, `multipleOf`, ...) | no type change; enforced by the generated validator | [002](./examples/002-validation/) |
| `$ref` / `$defs` | one shared named type | [003](./examples/003-references/) |
| `additionalProperties` / `unevaluatedProperties: false` | open (extra keys allowed) / closed (rejected) | [004](./examples/004-open-and-closed/) |
| `allOf` (base + extension) | merged `TypedDict` | [005](./examples/005-extending/) |
| `allOf` (constrain a base) | same shape, combined constraints | [006](./examples/006-constraining/) |
| `allOf` (several bases) | merged `TypedDict` | [010](./examples/010-mixins/) |
| `array` + `items` | `Sequence[T]` | [007](./examples/007-arrays/) |
| nested `items` | `Sequence[Sequence[T]]` | [008](./examples/008-nested-arrays/) |
| `prefixItems` + `items: false` + `minItems` | `tuple[A, B, C]`; variadic `tuple[A, *tuple[T, ...]]` | [009](./examples/009-tuples/) |
| `oneOf` / `anyOf` | `A \| B` alias + `is_<member>` guards + `match_<t>` | [011](./examples/011-unions/) |
| `oneOf` with `const` discriminant | union, `match_<t>` keyed off the tag | [012](./examples/012-discriminated-unions/) |
| string `enum` | `Literal["a", "b"]` | [013](./examples/013-string-enums/) |
| numeric `enum` | `Literal[200, 404]` | [014](./examples/014-numeric-enums/) |
| `additionalProperties: { ... }` (map) | `Mapping[str, V]` | [015](./examples/015-maps/) |
| `default` | property made optional; `with_defaults_<t>(value)` fills declared defaults | [016](./examples/016-mutation/) |
| `const` | the `Literal` of the value | [012](./examples/012-discriminated-unions/) |

## Shapes vs constraints

A recurring split runs through the table. A construct that changes the shape of the value appears in the type.
An object becomes a `TypedDict`, an array becomes `Sequence[T]`, a `oneOf` becomes a union. A construct that
only constrains a value that already has a shape, a length, a range, a pattern, a conditional requirement, does
not appear in the type, because Python's type system cannot express it. It is enforced by the generated
validator instead. A `format` is the one construct that appears in both, a branded `NewType` and a runtime
check.

## Objects

An object with `properties` becomes a `TypedDict` declared `total=False`, with required members wrapped in
`Required[...]`.

```python
class Person(TypedDict, total=False):
    birth_date: BirthDate
    family_name: Required[str]
    given_name: Required[str]
    height: float
    tags: Tags
```

Members are emitted in a stable order. Required members are `Required[T]` and optional members are a plain `T`,
so an optional key may be absent. `additionalProperties: false` (or `unevaluatedProperties: false`) makes the
object closed, rejecting undeclared keys, while an open object accepts them. See
[examples/004-open-and-closed](./examples/004-open-and-closed/).

## Scalars, enums and unions

A scalar maps to the Python primitive: `str`, `float`, `int`, `bool`, or `None` for the JSON literal `null`.

An `enum` becomes a `Literal` of the allowed values, a string `Literal["low", "medium", "high"]` or a numeric
`Literal[200, 404, 500]`.

A `oneOf` / `anyOf` becomes a PEP 695 `type` alias of its member types, lazy so a recursive union does not
deadlock at import. Each member gets a `typing.TypeGuard` (`is_<member>`), and the union gets a generic
`match_<t>` dispatcher. The dispatch honours JSON's type model, so a `True` value takes the `bool` branch and
not the `int` branch even though `bool` is a subtype of `int` in Python. See
[reading-and-validating](./reading-and-validating.md#narrowing-unions).

## Arrays, tuples and maps

An `array` with `items` becomes `Sequence[T]`, and nested `items` nest as `Sequence[Sequence[T]]`. A
`prefixItems` tuple becomes a fixed `tuple[A, B, C]`, and an open tuple tail becomes a variadic
`tuple[A, *tuple[T, ...]]`. A pure `additionalProperties` map becomes `Mapping[str, V]`.

## Formats and brands

A `format` keyword generates a branded `NewType` and a validating factory. `from_birth_date(value)` checks the
format and returns a `BirthDate`, raising `FormatError` on a bad value. A plain `str` is not a `BirthDate` to
the checker, so the only way to obtain one is through the factory. The four temporal formats (`date`,
`date-time`, `time`, `duration`) also emit a `to_temporal_<t>` accessor that parses the branded string into its
matching `whenever` value. A numeric `format` brands the number and range-checks `from_<t>`, and `decimal`
additionally emits `to_exact_<t>` for the exact decimal digits. See [The runtime](./runtime.md) for the values
these accessors return.

## Names

Type names come from the schema. A usable `title`, else the property or `$defs` key the type sits under, else
the document name, pascal-cased and de-duplicated. Identical subschemas collapse to a single type, so
referencing one shape many times yields one `TypedDict`. Named sub-scalars are emitted as PEP 695 aliases
(`type Tags = Sequence[str]`). Companion functions are module-level and snake_cased from the type name
(`build_person`, `patch_person`, `from_birth_date`). Property names are used verbatim as `TypedDict` keys.

By default every type goes into one `generated/__init__.py`. See [Code generation](./code-generation.md).

## What each type comes with

A generated type carries more than its `TypedDict`.

- `_eval_<t>(value, ev, r)`, the per-type validator (internal; the root's is re-exported as `evaluate`).
- `build_<t>(value)`, construct bytes; `build_canonical_<t>(value)`, deterministic RFC 8785 bytes.
- `parse_<t>(data)`, decode and cast without validating.
- `patch_<t>(source, changes, removals, arrays)`, byte-native partial update.
- `apply_merge_patch_<t>` / `create_merge_patch_<t>`, RFC 7396 merge patch over the bytes.
- `with_defaults_<t>(value)`, fill declared defaults, when the subtree has any.
- for a union, `is_<member>` guards and `match_<t>`.
- for a `format`, `from_<t>`, plus `to_temporal_<t>` (temporal) or `to_exact_<t>` (`decimal`).

The root type's `evaluate` and `parse` are re-exported at the top of the module as the document entry point.

## See also

- [reading-and-validating](./reading-and-validating.md), the read surface and the evaluators.
- [mutation](./mutation.md), producing changed documents.
- [examples](./examples/), every row above as a runnable recipe.
