# Version History

The version history of `corvus-json-schema`, the Python port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.1

V0.1.1 makes `pattern`, `patternProperties` and `format: regex` answer as V8 does where version 0.1.0 gave another answer, raised an error or refused the schema. There are no API changes.

### Bug fixes

- **`\B` in an empty string.** In ECMA-262, `\B` holds wherever `\b` does not, so it holds in an empty string, which has no word character and so no word boundary. The standard library's `re` module had `\B` never match in an empty string before CPython 3.14, and the package handed `\B` to it unchanged. `cjs.compile({"pattern": "\\B"})("")` returned `False`, where the instance is valid, and so did any pattern that reaches a `\B` with an empty string, such as `^\B$` or `^(\d|\B){2}`. A pattern used for `patternProperties` or `propertyNames` was affected for the property name `""`. `\B` is now translated as "not at a word boundary", `(?!\b)`, which is right on every version. `\b` was already right, and a string that is not empty was never affected. A test checks both at every position of every string of up to three characters from a small alphabet, through the standard library's engine and through the `regex` package. Version 0.1.0 is affected on CPython 3.10 to 3.13, and is not on CPython 3.14.
- **Backreferences.** In ECMA-262 a backreference to a group that has not matched matches the empty string, and the groups inside a repetition lose their values at the start of each iteration. The two engines the package uses fail such a reference and keep a value from one iteration to the next, and the standard library's refuses a reference that comes before its group or is inside it. So `cjs.compile({"pattern": "^(?:(a)|b)\\1$"})("b")` and `cjs.compile({"pattern": "^(?:(a)|b)*\\1$"})("ab")` returned `False`, where the instances are valid, and `^\1(a)$` and `^(a\1)$` raised `SchemaCompilationError`, where the patterns are valid. Each reference is now written from where its group is in the pattern. One that can never be read after its group has matched (it is inside the group, before it, or in another alternative) is written as nothing. One whose group has always matched when it is read stays a reference. For any other, the pattern is run by the `regex` package, where an empty group of the same name resets the group at the start of the pattern and of each iteration. On CPython 3.10 a pattern with a reference to a group inside a repetition is also run by `regex`, because the standard library's engine did not put back what such a group held when it backtracked: `^([^a]?a+){2,3}?\1a$` did not match `aaaa` there. A pattern with no backreference is translated as before.

## V0.1.0

The first release of the Python port of `Corvus.Text.Json.RuntimeEvaluator`, by way of its TypeScript port.

### New features

- **A JSON Schema evaluator for Python.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,967 tests of the JSON-Schema-Test-Suite, including `draft4/optional/zeroTerminatedFloats.json`, which the C# and TypeScript runners exclude. A schema is compiled once into specialised Python, one function per schema node with leaf schemas tested inline, that validates the values `json.loads` produces. It is pure Python, with the `regex` package for the Unicode properties of ECMA-262 patterns.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.

- **Standalone modules.** A schema can be emitted as a Python module that depends only on the package's small runtime. The `corvus-json-schema` command line generates modules and validates instances.
