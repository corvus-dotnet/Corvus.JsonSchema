# The runtime

The generated Python imports a small runtime package, `corvus_json_runtime`, for the parts the language cannot
express on its own: exact numeric comparison, RFC-accurate format checks, the byte-level edit engine, the
evaluation tracker, and the temporal conversions. It is the Python peer of the TypeScript
`@endjin/corvus-json-runtime`. This guide covers what it provides, how to supply it, and its dependencies.

## Overview

The runtime is a single import package. The generated code imports the primitives it needs from it. Validators
call the format, equality, and exact-number helpers, builders call the byte engine, and brand factories call
the format checks. It is the same for every generated module, so when you generate many schemas they all share
one installed copy.

## Supplying the runtime

```bash
pip install corvus-json-runtime
```

The generated module imports it as `corvus_json_runtime`. To import it under a different name, set
`CORVUS_PY_RUNTIME_MODULE` at generation time (see [Code generation](./code-generation.md#the-runtime-dependency)).

## Dependencies

The runtime relies on three well-known packages for the parts the standard library does not cover exactly.

| Package | Used for |
|---|---|
| [`whenever`](https://pypi.org/project/whenever/) | The `date` / `time` / `date-time` / `duration` temporal values the `to_temporal_<t>` accessors return. |
| [`regex`](https://pypi.org/project/regex/) | ECMA-262 `pattern` and `format: regex` matching, including `\p{...}` Unicode property escapes that stdlib `re` rejects. |
| [`idna`](https://pypi.org/project/idna/) | IDNA / UTS-46 processing for the `idn-email`, `idn-hostname`, `iri`, and `iri-reference` formats. |

## Numbers as Decimal

`decode_and_parse` decodes JSON with `parse_float=Decimal`, so a JSON number arrives as an `int` or a
`decimal.Decimal` and keeps every digit. Numeric validation runs on that exact value, never a lossy binary
float. `minimum`, `maximum`, `exclusiveMinimum` / `exclusiveMaximum`, and `multipleOf` compare mathematically,
so the cases where floating point lies just work. `multipleOf` uses exact rationals (`Fraction`), so a large or
high-precision divisor is not limited by the decimal context.

```python
from corvus_json_runtime import exact_number, decode_and_parse

doc = decode_and_parse(b'{"amount": 123456789012345678901234567890.5}')
exact_number(doc["amount"])   # "123456789012345678901234567890.5", every digit kept
```

The declared model type of a `number` field is `float`, but the runtime value read back from `parse` is a
`Decimal`. A `decimal`-format field additionally gets a generated `to_exact_<t>` accessor over `exact_number`.

## What the runtime provides

The generated code imports these from `corvus_json_runtime`.

- Core JSON helpers. `decode_and_parse`, `build`, `canonicalize`, `eq` (JSON deep equality, numbers by value),
  `is_obj`, `is_arr`, `is_num`, `is_int`, `ptr` (RFC 6901 JSON Pointer), and `re_test`.
- Exact numeric primitives. `cmp`, `num_eq`, `multiple_of`, and `exact_number`, all over a number's exact
  decimal value.
- Format checks. `fmt` and `fmt_content`, the RFC-accurate validators for `date`, `time`, `date-time`,
  `duration`, `email` / `idn-email`, `hostname` / `idn-hostname`, `ipv4` / `ipv6`, the `uri` / `iri` family,
  `uuid`, `uri-template`, `json-pointer` / `relative-json-pointer`, and `regex`, shared by the validators and
  the brand factories. `FormatError` is raised by a `from_<t>` factory on a bad value.
- The byte-level edit engine. `rmw_upsert`, `rmw_produce_full`, and the `RmwTarget`, `RmwArrayEdit`,
  `RmwArrayOps`, and `ListOps` types that back `patch_<t>`, plus `produce`, the recipe-driven draft that backs
  `produce_<t>` (see [Mutation](./mutation.md)).
- JSON Patch. `apply_patch` and `create_patch` (RFC 6902 operation patch), `apply_merge_patch` and
  `create_merge_patch` (RFC 7396 merge patch), and `JsonPatchError` (see [JSON Patch](./json-patch.md)).
- The evaluation tracker and results. `Ev`, `NOEV`, `fresh` (the tracker for `unevaluated*`), and `Results`,
  `Failure`, and `Annotation` (the spec-output failure collector, see
  [Reading and validating](./reading-and-validating.md#detailed-results)).
- Temporal conversions. `to_instant`, `to_plain_date`, `to_plain_time`, and `to_duration`, plus the `whenever`
  types they return, re-exported as `Instant`, `Date`, `Time`, and `TimeDelta`.

## Temporal values

The four temporal formats each get a generated `to_temporal_<t>` accessor that parses the branded string into
its matching `whenever` value: a `date` into a `Date`, a `date-time` into an `Instant`, a `time` into a `Time`,
and a `duration` into a `TimeDelta`. Validation is separate from conversion. `evaluate` checks the format to
RFC 3339 rules, and the accessor parses a value already known to be well formed.

```python
from generated import parse, to_temporal_birth_date

person = parse(data)
birth_date = person.get("birth_date")
if birth_date is not None:
    to_temporal_birth_date(birth_date)   # whenever.Date("1820-01-17")
```

## Python version

The runtime and the generated code target Python 3.12. Both type-check clean under `mypy --strict` and pyright,
so they fit a strict project without loosening your configuration.

## See also

- [Code generation](./code-generation.md), the `--engine Python` option and the runtime dependency.
- [The type surface](./the-type-surface.md), the branded formats and temporal accessors the runtime backs.
- [Mutation](./mutation.md), the byte-level engine the runtime provides.
