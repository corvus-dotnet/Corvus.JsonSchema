# Version History

The version history of `corvus-json-schema-rs`, the Python package backed by the `corvus-json-schema` Rust crate. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.2.0

V0.2.0 takes version 0.2.0 of the `corvus-json-schema` crate, which matches patterns with the [`corvus-regex-ecma262`](https://crates.io/crates/corvus-regex-ecma262) crate and reads them in the grammar of the dialect. It is a minor version because under 2019-09 and 2020-12 a pattern that uses what ECMAScript added after 2020 is no longer valid. Everything else that was there is unchanged, and there are two new options.

- **The grammar of the dialect.** JSON Schema 2019-09 and 2020-12 name the grammar of ECMAScript 2020, and patterns under them are now read with it: a modifier group such as `(?i:...)`, or a group name used by two groups, is not valid, and `compile` raises `SchemaCompilationError` for a schema with one in a `pattern`. A property escape that the tables of 2020 do not have (`\p{Script=Yezidi}`) is not valid with the `u` flag, so the pattern is read without it, where `\p` is the letter `p`, as a pattern with `\&` is. The `regex` format reads only the grammar of the flag. Drafts 4 to 7 read today's grammar, as before.
- **`regex_edition`** (`RegexEdition.DIALECT`, the default, `CURRENT` or `ES2020`, or the strings `"dialect"`, `"current"` and `"es2020"`) chooses the edition for every dialect, and **`regex_unicode_version`** (`RegexUnicodeVersion.DEFAULT`, the default, `LATEST`, `V13`, `V17` or `V18`, or `"default"`, `"latest"`, `"13"`, `"17"` and `"18"`) the version of Unicode. Both are fields of `CompileOptions` and keywords of `compile`. Another value raises `ValueError`.
- **The engine.** Every pattern that no dedicated matcher of the crate takes is matched by `corvus-regex-ecma262`, whose answers are V8's for the whole of the ECMA-262 regular expression test data. The limits are those of 0.1.4. Over the jsonschema-benchmark corpora the crate validates in 0.98 of the time of 0.1.7 once warm, and takes 1.06 of it for the first validation and 1.05 to compile, since the engine builds a pattern's automaton on its first match.
- **Notices.** `THIRD-PARTY-NOTICES.txt` names the tables of `corvus-regex-ecma262`, which the wheels now hold in place of the crate's own.

## V0.1.4

V0.1.4 takes version 0.1.7 of the `corvus-json-schema` crate. That fixes wrong results for several forms of pattern and a validation that did not end, replaces the crate's regular expression engine, and gives every match of a pattern a limit of time. The functions and types that were there are unchanged, and there are additions for the limit. Versions 0.1.0 to 0.1.3 are affected. The pure-Python `corvus-json-schema` package is not.

### The regular expression engine

The crate no longer uses the `regress` crate for the patterns with a lookaround, a backreference or a modifier group. It matches them with an engine of its own, a port of the ECMA-262 engine of the Corvus Go module, and matches every pattern as it is written. The crate's [version history](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) lists the patterns that were answered wrongly before (a repetition inside a repetition that lost its count or did not end, a backreference from inside its group, two groups with one name, case-insensitive matching without the `u` flag, a pattern that is valid only without the `u` flag and a character beyond the Basic Multilingual Plane, `\B` beside a character of several bytes, and others), and what was measured: V8's answer for all 4,268,313 texts of the ECMA-262 regular expression test data that can be asked.

### Limits on regular expressions

A schema and an instance may both come from outside, so a pattern is now bounded in what it costs. Nothing that was there has changed. There is one new option and there are two new exceptions.

- **A match has a limit of one second.** `pattern_match_limit`, a keyword of `compile` and a field of `CompileOptions`, is the seconds one match of a `pattern` or `patternProperties` regular expression against one string may take. The default is `1.0`. `None` or `0` turns the limit off for a caller who trusts both the schema and the instances. The limit is on each match, not on the validation.
- **`PatternMatchLimitError`** is raised by a validation (calling the validator, `is_valid`, `is_valid_json` and `evaluate`) when a match reached the limit, or when its backtracking held 1,048,576 entries. The instance is then neither valid nor not valid. The message names the pattern and the limit. `compile` can raise it too, because a pattern of `patternProperties` is matched against the property names the schema declares.
- **`PatternTooLargeError`** is raised by `compile` for a schema with a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of groups, or 1,048,576 counted for it as it is read (a pattern of plain characters can be 16,384 characters long). It is a `SchemaCompilationError`, so code that catches those still catches it. A regular expression that is not valid raises `SchemaCompilationError` itself, as before.

### Bug fixes

- **A separated list with a `$` inside it.** The crate matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without a regular expression engine. It dropped a `$` at the end of the first item or of the repeated group, so `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most. `cjs.compile({"pattern": "^x(,a$)*$"})("x,a,a")` returned `True`, where the instance is not valid. `^x$(,a)*$` took `"x,a"`, and `^(a,$)*b$` took `"a,b"`. Calling the validator, `is_valid`, `is_valid_json` and `evaluate` were all affected, and so was a pattern used for `patternProperties` or `propertyNames`. Such a pattern is now matched by a regular expression engine. An escaped `\$` is a literal character and is matched as before. No other form of pattern was affected.

### Notices

- **The notice of the Unicode data is in the package.** The Unicode tables of the `corvus-json-schema` crate are derived from the Unicode Character Database, which is under the Unicode License V3. That license asks that its notice goes with every copy of the data. `THIRD-PARTY-NOTICES.txt` has it, with the notice of the `regress` crate that the tables were first read from. It is a license file of the project, so it is now in the source distribution and in every wheel (`dist-info/licenses`). Versions 0.1.0 to 0.1.3 carried no such notice.

## V0.1.3

V0.1.3 fixes a crash of the Python process for a schema whose `not` leads back to the schema it is in, by taking version 0.1.6 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 to 0.1.2 are affected. The pure-Python `corvus-json-schema` package is not.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The package abandons such an evaluation at `max_depth` (128 by default) and raises `SchemaEvaluationDepthError`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the stack overflowed. That ends the Python process with a segmentation fault. No exception is raised, so nothing can catch it. Calling the validator, `is_valid`, `is_valid_json` and `evaluate` were all affected. A `not` is now under the guard, so they raise `SchemaEvaluationDepthError`. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write. A schema that loops elsewhere under a `not` was not affected in this package: it raised `SchemaEvaluationDepthError`, and still does.

## V0.1.2

V0.1.2 fixes wrong validation results for one form of pattern, by taking version 0.1.4 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 and 0.1.1 are affected. The pure-Python `corvus-json-schema` package is not.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The crate matches this form without a regular expression engine and keeps the excluded set as one bit for each ASCII character. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time, and set the bits of unrelated ASCII characters (`C` and `)` for `é`). A validator compiled from such a schema rejected valid strings (`"C1"`) and accepted invalid ones (`"é1"`), with no error. Such a pattern is now matched by the regular expression engine. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.1

V0.1.1 takes version 0.1.2 of the `corvus-json-schema` crate, which validates faster, and parses JSON text faster. There are no API changes, and every result is unchanged.

### Other changes

- **Faster validation.** The crate's 0.1.2 release makes several patterns and small objects cheaper to evaluate. A lookahead pattern such as `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$` no longer rebuilds a character set for every character it tests, a pattern valid only without the `u` flag (such as `^\/[^\*\?\&\%]*(\/\*)?$`) no longer always runs on the backtracking engine, and objects with few declared properties cost less. On the sourcemeta jsonschema-benchmark corpora, cspell validates more than five times faster and krakend about 30% faster.

- **`is_valid_json` parses into a `JsonDocument`.** JSON text is parsed into the crate's `JsonDocument`, read in place by the evaluator, instead of a `serde_json::Value`: about a quarter of the parsing time, and one allocation per document. Doubles are now correctly rounded (serde_json's parser could be one unit in the last place off). Invalid UTF-8 in `bytes` raises `ValueError` as other invalid JSON does.

## V0.1.0

The first release.

### New features

- **The Rust evaluator for Python.** It has the API of the pure-Python `corvus-json-schema` package and passes the JSON-Schema-Test-Suite (every draft, with the C# runner's single exclusion) and its annotation tests.

- **Instances are read in place.** Python objects are read through CPython's C API, without converting them to `serde_json` values first, so validating an object costs the evaluation alone. JSON text can also be validated directly, parsed in Rust.

- **Wheels for every major platform.** The extension uses the stable ABI, so one wheel per platform serves CPython 3.10 and later, on Linux (x86_64 and aarch64, glibc and musl), macOS (universal2) and Windows (x64 and arm64).
