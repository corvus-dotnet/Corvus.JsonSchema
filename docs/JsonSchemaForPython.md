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
| `pattern_match_limit` | The seconds one match of a regular expression against one string may take, by the clock (default `1.0`). Beyond it, `PatternMatchLimitError`. `None` or `0` turns the limits of a match off. |
| `regex_edition` | `corvus-json-schema-rs` only. The edition of ECMA-262 patterns are read with: `RegexEdition.DIALECT` (the default: ECMAScript 2020 for 2019-09 and 2020-12, which name it, today's for the drafts before them), `CURRENT` or `ES2020`. Under ECMAScript 2020 a modifier group such as `(?i:...)` is not valid. |
| `regex_unicode_version` | `corvus-json-schema-rs` only. The version of Unicode patterns are read with: `RegexUnicodeVersion.DEFAULT` (the version of the V8 the answers are held to, 17 today), `LATEST`, `V13`, `V17` or `V18`. |

A reference that cannot be resolved, or an invalid pattern, raises `SchemaCompilationError` when the schema is
compiled.

In both packages a regular expression is bounded in what it costs, since a schema and an instance may both come
from outside. A validation whose match reaches `pattern_match_limit`, or a limit of memory, raises
`PatternMatchLimitError`, from the validator and from `evaluate`, and the instance is then neither valid nor not
valid. A schema with a regular expression over a limit of size raises `PatternTooLargeError`, which is a
`SchemaCompilationError` and is never raised for a pattern that is not valid. The limits are those of the engine
underneath:

| | `corvus-json-schema` | `corvus-json-schema-rs` |
|---|---|---|
| Length of a pattern | 32,768 characters | 1,048,576 bytes |
| Groups inside each other | 100 | 1,000 |
| What the pattern comes to | 32,768 characters as it is written for Python's engines, and 4,096 copies of what a count of more than one term repeats in a pattern that the `regex` package runs | about 16,000 terms |
| How the time of a match is counted | By the clock, for a pattern that the `regex` package runs: the error comes between nine tenths of the limit and about twice the limit. None for a pattern that is proved to match in time proportional to the text, which `re` runs. | By the clock, by the engine, for every pattern it is given |
| The memory of a match | A text of more than 1,048,576 characters is not matched once the first try at a match has been abandoned, and a pattern with groups that the engine keeps for every repetition takes fewer. A pattern that `re` runs has no such limit. | A match is abandoned when its backtracking stack reaches 1,048,576 entries (24 MB) |
| The limit error | Has `pattern`, `limit` and `most_characters` | Has its message |
| A limit error from `compile` | Never: compiling runs no match | When a pattern of `patternProperties` reaches the limit on a property name the schema declares |
| `format: regex` | A text over a limit of reading (32,768 characters, 100 levels) is not valid. Any other is valid or not by the grammar. | The same rule, with its limits of reading (1,048,576 bytes, 1,000 levels, about 16,000 terms) |

```python
import corvus_json_schema as cjs  # or corvus_json_schema_rs

validate = cjs.compile({"pattern": r"^(?:(a+)\1*)+$"}, pattern_match_limit=0.25)
try:
    validate("a" * 60 + "b")
except cjs.PatternMatchLimitError:
    ...  # no answer within a quarter of a second
```

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
standard library's `re`, and the `regex` package for what `re` cannot run or is not proved to run in time
proportional to the text. A pattern is read with the `u` flag, and a
pattern that is not valid with it is read as a pattern with no flag, which accepts what many schemas hold (`\&`, a
lone brace). The `regex` format accepts only the `u` flag grammar. Every answer the package gives for the 98,839
patterns of the [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data)
is V8's, on CPython 3.10, 3.12 and 3.14. One of those patterns is over a limit of size, and nine or ten, by the
version of Python and by what else the machine is doing, have a text whose match reaches the limit of one second.
Both are set out below.

- `\d`, `\w` and `\b` are ASCII, the dot excludes the four line terminators, and `$` holds only at the end.
- Modifier groups (`(?i:...)`, `(?m:...)`, `(?s:...)`, `(?i-s:...)`) ignore case, match at line terminators and let
  the dot match everything as ECMA-262 defines them. Case is ignored by the tables of Unicode 17 that the package
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
  names and the characters are those of Unicode 17, from tables that the package carries and that every port of
  the evaluator shares, whatever version of Unicode the `regex` package that is installed has (release 2026.9.29
  has Unicode 18) and whatever version CPython has. For the `regex` package a property is written as its own with
  the tens of ranges that are different, which is checked against the tables for every character, and for `re` as
  ranges. So `\p{sc=Sidetic}`, `\p{sc=Tolong_Siki}`, `\p{sc=Beria_Erfe}` and `\p{sc=Tai_Yo}` are names
  here, where V8 13.6 has Unicode 16 and reads such a pattern without the `u` flag. A name that only the `regex`
  package has, such as `\p{Greek}`, `\p{IsLatin}` or `\p{alpha}`, makes a pattern that is not valid with the `u`
  flag, and so one where `\p` is the letter `p`.

No valid pattern is refused for what it means, and none is run with another meaning. What remains is the limits
on what a regular expression may cost.

### The limits, and which engine runs a pattern

The standard library's `re` cannot be stopped once a match has started. It has no limit of its own and looks at
signals in the main thread only. The `regex` package takes a timeout. So with `pattern_match_limit` in force,
which is the default, a pattern goes to `re` only when the package proves that `re` matches it in time
proportional to the text, whatever the text is. Such a pattern has no limit and needs none, and a match costs what
it did before. Every other pattern goes to the `regex` package with the limit. With the limit
turned off, `re` runs every pattern it can.

The proof is that at every choice in the pattern (the alternatives of an alternation, one more iteration of a
repetition or none) the next character of the text says which way to go, and that the body of a repetition takes a
character. Beyond that it allows, each for a bounded cost, a pattern that takes a bounded number of characters and
has few ways through it, a repetition that is left with a bounded rest of the pattern to try, an alternation of
bounded alternatives that start alike, a bounded lookahead (and one with no bound at the start of a pattern that
starts with `^`), and a pattern without `^` whose tries fail within a few characters.

| Proved, and run by `re` | Not proved, and run by `regex` with the limit |
|---|---|
| `^[a-z0-9]+(?:-[a-z0-9]+)*$` | `^(a+)+$`, where the time doubles with each character |
| `^[a-z]+@[a-z]+\.[a-z]{2,}$` | `^([a-z0-9]+[._-]?)*$`, the same |
| `^(?=.*[a-z])(?=.*\d).{8,}$` | `a*b` and `\{.*\}`, where it grows as the square |
| `^.*\.json$`, `^(http\|https)://`, `[a-z][a-z0-9]*` | `^(?:[a-z]+,)*[a-z]+$`, which is linear and not proved |
| `^a{100000}$` | a pattern with a lookbehind or a backreference that reads a group |

Of the 124 patterns of the jsonschema-benchmark corpora that need a regular expression, 117 are proved.

The limit is of time on the clock. The `regex` package's timeout counts the time of the whole process on the
processor, and the package runs a match without the interpreter's lock, so with three other threads busy a timeout
of 1 second came after 0.24 seconds. The package uses the timeout as an allowance of processor time and lets the
clock decide. The first try at a match is allowed a thirty-second of the limit, with nothing measured around it, so
a match that answers costs what it did. A match that does not answer in that is tried again from its start, with
what should come to the limit on the clock at the rate the processor's time is passing. The error is not raised
before nine tenths of the limit have passed, and no try is allowed more than is left of twice the limit, so a match
is bounded at about twice the limit whatever the other threads do. With three or more other threads at work
throughout, a try gets less than the limit on the clock (half of it with three). With a limit of 1 second the
error came after 0.95 to 1.12 seconds with up to fifteen other threads at work.

One step of a match can run over the whole text, and the engine looks at its clock every 256 steps, so its
timeout can come late by a time that grows with the text. A repetition of one class is such a step. A class of
more than 24 ranges under a repetition is written so that each character is a step, and a text of more than 32,768
characters is matched by the pattern written so for every such repetition. In the adversarial probe (210
patterns of at most 1,000 characters against texts of at most 100,000) the latest that an answer or the error
came was 1.3 seconds after the match started.

The memory of a match is bounded too. Both engines keep what a group held each time it closes, until the match
ends, so a group that no backreference reads is written so that it does not capture inside a repetition. For the
groups that are read, and for what the `regex` package keeps for each iteration of a repetition, a text has a limit
of length in a pattern that the `regex` package runs, once the first try has been abandoned. It is 1,048,576
characters, and fewer for a pattern with groups that are kept for every repetition (262,144 for `^(?:(a)|b)*\1$`).
A longer text raises `PatternMatchLimitError`, whose `most_characters` is the number. The most that one match took
at the longest text its pattern lets through is 90 MB, and that is also the most that a process took for a pattern
of the probe. A pattern that `re` runs has no limit of time or of memory. Its time and its memory are in
proportion to the text.

The limits of size, each of which raises `PatternTooLargeError` when the schema is compiled:

- A pattern of more than 32,768 characters, or one whose groups nest more than 100 deep. Both engines and the
  translator read a pattern with code written in Python, by recursion.
- A translation of more than 32,768 characters. A plain term is as long translated as it was written, and a long
  class that the pattern has many times, such as a property, counts once. What makes a translation longer is a
  character in a case-insensitive group, which is the class of its forms, and a repetition around a group that a
  backreference reads, which is written out copy by copy, as in `^(?:(?:(a)|b){300}){300}\1$`.
- Counts that come to more than 4,096 copies in a pattern that the `regex` package runs. That package compiles
  `(?:ab){1000}` as a thousand and one copies of `ab`, so `(?:(?:ab){1000}){1000}` would take 125 MB, and
  `(?<=b)(?:ab){4097}` is refused. A count of one character or class is written without copies where they would be
  over the limit, so `(?<=b)a{1000000}` is compiled, in kilobytes. `re` keeps a count as a number, so
  `^a{100000}$`, which is proved linear, has no such limit.
- A minimum count of 2,147,483,647 or more over something that can match nothing.

Within them, the longest compilation measured was 0.47 seconds (a literal of 32,760 characters beyond ASCII for
the `regex` package, on CPython 3.14 with the machine under load), and the most kept for a compiled pattern 4.7 MB
(10,920 groups, for the `regex` package). The package's cache holds the last 256 compiled patterns of
1,024 characters at most, `re` is given translations of 1,024 characters at most to keep in its own cache of 512,
and a longer one is compiled without being kept. The `regex` format reads a pattern and does not compile it. One
that is too long or too deep to read is not valid, and any other is valid or not by the grammar, whatever it would
cost to compile.

Both engines backtrack, and neither has V8's short cuts. A repetition whose body can match nothing, inside another
repetition or around one, is written with a body that cannot wherever no reference reads a group of it, and a
repetition with no maximum of nothing but another one is written as the inner one, so that a text that does not
match is answered as quickly as in V8. Where a reference reads a group inside such repetitions, the pattern is
written as ECMA-262 runs it, and a match can reach the limit where V8 answers in milliseconds. Nine or ten patterns of
the test data have such a text, nine of them in its `backreferences` file, and V8 answers each of those texts
within 10,000 backtracks. One pattern of the test data is refused as too large: its translation is too long.

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
