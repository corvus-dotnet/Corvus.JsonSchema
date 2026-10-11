# JSON Schema for Python

The Corvus JSON Schema evaluator for Python comes in two packages with the same API, so either can be imported in
place of the other. Both cover draft 4, 6, 7, 2019-09 and 2020-12, and give the same results and annotations as every
other Corvus implementation.

| Package | What it is | Choose it when |
|---|---|---|
| [corvus-json-schema](https://pypi.org/project/corvus-json-schema/) | Pure Python: it compiles a schema into specialised Python code. Its one dependency is [`regex`](https://pypi.org/project/regex/). | You want no native code, or a schema as a standalone Python module. |
| [corvus-json-schema-rs](https://pypi.org/project/corvus-json-schema-rs/) | Backed by the [Rust crate](JsonSchemaForRust.md), reading Python objects in place through CPython's C API. | You want the fastest validation. |

- **Conformant.** Both pass the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft),
  and all of its annotation tests. The pure package passes `draft4/optional/zeroTerminatedFloats.json` too, which the
  others exclude.
- **Fast.** The pure package validates six times faster than fastjsonschema, the other generator of Python code. The
  Rust-backed package takes about a third of the pure package's time, and 0.72 of jsonschema-rs's.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- Python 3.10 or later, typed (`py.typed`). The Rust-backed package has wheels for Linux (x86_64 and aarch64, glibc
  and musl), macOS (Intel and Apple silicon) and Windows (x64 and arm64).

## Install

```sh
pip install corvus-json-schema      # pure Python
pip install corvus-json-schema-rs   # backed by Rust
```

## Validate

```python
import corvus_json_schema as cjs  # or: import corvus_json_schema_rs as cjs

validate = cjs.compile({
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"],
})

validate({"id": 3})  # True
validate({"id": 0})  # False
```

`compile` takes the schema as a parsed value or as JSON text. The validator is a function: call it with any value
`json.loads` produces. The Rust-backed package also validates JSON text directly, without creating Python objects for
it: `validate.is_valid_json('{"id": 3}')`.

## Options

`compile` takes these options as keyword arguments, or as a `CompileOptions`:

| Option | Meaning |
|---|---|
| `default_dialect` | The dialect of a schema without `$schema`: `Dialect.DRAFT4` to `Dialect.DRAFT202012` (the default). |
| `assert_format` | `True` asserts `format`, `False` never does; `None` (the default) follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | With `assert_format` unset, assert `format` in drafts 4 to 7 too. |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `True`). |
| `formats` | Custom formats by name, such as `{"even": lambda s: len(s) % 2 == 0}`. |
| `resolve_document` | `(uri) -> schema`, JSON text or `None`, for remote references. The standard metaschemas are built in. |
| `base_uri` | The base URI of the root document. |
| `entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `max_depth` | The deepest the evaluator recurses in place (default 128); beyond it, `SchemaEvaluationDepthError`. |

A reference that cannot be resolved, or an invalid pattern, raises `SchemaCompilationError` when the schema is
compiled.

## Results and annotations

```python
collector = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
validate.evaluate({"id": 0}, collector)  # False
for r in collector.results:
    ...  # r.is_match, r.message, r.evaluation_location, r.schema_evaluation_location, r.document_evaluation_location

verbose = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
validate.evaluate({"id": 3}, verbose)
cjs.collect_annotations(verbose)  # {"": {"title": {"#": "Person"}}, ...}
```

`BASIC` records the failures without messages, `DETAILED` adds the messages, and `VERBOSE` records every keyword,
passing ones and annotations included. Validation without a collector runs the fast path, so collecting costs nothing
when it is not used.

## Standalone modules

The pure package can write a schema out as a Python module that depends only on its small runtime:

```python
from pathlib import Path

Path("person_validator.py").write_text(cjs.generate_module(schema))
```

## Regular expressions

`pattern`, `patternProperties` and the `regex` format follow ECMA-262, as JSON Schema specifies, with the grammar of
ECMAScript 2025. This section is about the pure package, which translates a pattern for Python's engines: the
standard library's `re`, and the `regex` package for what `re` cannot run. A pattern is read with the `u` flag, and a
pattern that is not valid with it is read as a pattern with no flag, which accepts what many schemas hold (`\&`, a
lone brace). The `regex` format accepts only the `u` flag grammar. Every answer for the 98,839 patterns of
the [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) is V8's, on
CPython 3.10, 3.12 and 3.14, and one of those patterns passes the size limit below.

- `\d`, `\w` and `\b` are ASCII, the dot excludes the four line terminators, and `$` holds only at the end.
- Modifier groups (`(?i:...)`, `(?m:...)`, `(?s:...)`, `(?i-s:...)`) ignore case, match at line terminators and let
  the dot match everything as ECMA-262 defines them. Case is ignored by the tables of Unicode 16 that the package
  carries, so the answer does not depend on the version of Python or of `regex`.
- A backreference reads what ECMA-262 says its group holds. A group that did not take part reads as the empty string.
  The groups of a repeated body are unset when an iteration starts, so `^(?:(a)|x)*\1$` takes "ax" and not "axa",
  and an iteration that matched nothing leaves no group set.
- A lookbehind can be of any length and is matched backwards, so `(?<=\1(a))b` takes "aab" and `(?<=(a+))b\1`
  takes "aabaa". A group that a lookahead sets on only some of the ways it can match, as in `(?=(a)?b)\1`, and a
  group of a lookaround that is read in the same repetition after a choice, as in
  `^(?:(?=([ab]))[ab](?:c|ca)\1)*$`, hold what they hold in ECMA-262.
- Inside a case-insensitive group a backreference compares by ECMA-262's Canonicalize, character by character:
  `^(?i:(.)\1)$` takes "sſ" and "kK" (U+017F, U+212A) and not "iİ" (U+0130). The comparison is written out
  in the translation, as neither engine compares that way. It costs a few microseconds for each character
  compared, and a group that can hold any character has a translation of about 54,000 characters, which takes
  about 0.2 seconds to compile and holds about 3 MB.
- A pattern that is valid only without the `u` flag is read and matched by UTF-16 code unit, as ECMA-262 says: in
  `^\-?.$` the dot takes half of a character beyond the Basic Multilingual Plane.
- A `\p{...}` takes the names that ECMA-262 has and no others: a value of General_Category (`\p{Lu}`,
  `\p{gc=Letter}`), a value of Script after `Script=`, `sc=`, `Script_Extensions=` or `scx=` (`\p{sc=Greek}`), or a
  binary property that ECMA-262 lists (`\p{Alphabetic}`), in the spelling of the Unicode Character Database. The
  names are those of Unicode 16. A name that only the `regex` package has, such as `\p{Greek}`, `\p{IsLatin}` or
  `\p{alpha}`, makes a pattern that is not valid with the `u` flag, and so one where `\p` is the letter `p`. The
  characters of a property are the `regex` package's, and follow its version of Unicode.

No valid pattern is refused for what it means, and none is run with another meaning. Two limits remain.

- A pattern whose translation would pass 65,536 characters raises `SchemaCompilationError` with a message that says
  the pattern is valid and too large. That takes a count inside a count around a group that a backreference reads,
  as in `^(?:(?:(a)|b){300}){300}\1$`, a count of more than 175 around a case-insensitive backreference inside a
  lookbehind (more than 1,284 around one outside a lookbehind), or a minimum count of 2,147,483,647 or more over
  something that can match nothing. The `regex` format still accepts such a pattern.
- Both engines backtrack, and neither has V8's short cuts. A repetition whose body can match nothing, inside
  another repetition or around one, is written with a body that cannot wherever no reference reads a group of it,
  so that a text that does not match is answered as quickly as in V8. Where a reference reads a group inside such
  repetitions, the pattern is written as ECMA-262 runs it, and may take far longer than it does in V8.

Where V8 itself departs from ECMA-262, the package follows ECMA-262. With the `u` flag V8 13.6 also starts a match
between the two surrogates of a character, and with the `i` and `u` flags it compares a backreference to a group of
several characters by full case folding. It takes `\p{WSpace}`, which ECMA-262 does not list, and refuses the
Script value `Katakana_Or_Hiragana`, which ECMA-262 has. The
[package's README](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-py/corvus-json-schema#limitations-and-differences-from-the-c-evaluator)
has an example of each.

## Performance

Measured with jsonschema-benchmark's corpora and protocol on CPython 3.12 (warm validation, geometric means):

| Comparison | Time ratio | Faster on |
|---|---:|---:|
| corvus-json-schema / fastjsonschema | 0.17 | 34 of 35 corpora |
| corvus-json-schema-rs / corvus-json-schema | 0.35 | |
| corvus-json-schema-rs / jsonschema-rs | 0.72 | 34 of 35 corpora |

fastjsonschema supports drafts 4 to 7 only, and jsonschema-rs cannot run two of the 37 corpora. The
[pure package's README](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-py/corvus-json-schema#performance-results)
has the figures for each corpus.

## Links

- Packages: [corvus-json-schema](https://pypi.org/project/corvus-json-schema/) and
  [corvus-json-schema-rs](https://pypi.org/project/corvus-json-schema-rs/) on PyPI
- Source and READMEs: [src-py](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-py)
- The other languages: see [Other languages](OtherLanguages.md)
