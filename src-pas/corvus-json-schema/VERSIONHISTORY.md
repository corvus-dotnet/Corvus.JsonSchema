# Version History

The version history of `corvus-json-schema` for Object Pascal (`src-pas/corvus-json-schema`), the Object Pascal port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.1

V0.1.1 fixes wrong results for two kinds of pattern. There are no API changes. Version 0.1.0 is affected.

### Bug fixes

- **A pattern that is valid only without the `u` flag, on a string with a character beyond the Basic Multilingual Plane.** The package reads a pattern with the grammar of the ECMA-262 `u` flag, and with the grammar of a pattern with no flag when the first refuses it, as `\-` outside a class or `\_` make it do. ECMA-262 matches a pattern with no `u` flag by UTF-16 code unit, and the package matched it by code point. A character beyond the Basic Multilingual Plane is two code units, so `{"pattern": "^\\-?.$"}` was valid for `"\ud801\udc00"` (the one character U+10400), where it is not, and `{"pattern": "^\\-?[^a]{2}$"}` was not valid for it, where it is. The same went for a class, a quantifier, a group, a backreference, a lookbehind and a case-insensitive group, and for the patterns of these shapes that the package matches without the pattern engine. Such a character written in the pattern is two atoms, and was read as one: `^\-?\ud801\udc00?x$` requires the high surrogate and makes the low one optional, and was taken to match `"x"`. A class range between two such characters, as in `^\-?[\ud801\udc00-\ud801\udc28]$`, runs from a low surrogate down to a high one. It is an error in both grammars and was accepted, and `^\-?[\ud801\udc00-\udfff]$`, which is valid, was refused. Such a pattern is now matched by code unit, and the engine reads the code units from the UTF-8 text without making a copy of it. A pattern that is valid with the `u` flag was never affected, nor was a string within the Basic Multilingual Plane.

- **A separated list with a `$` inside it.** The package matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without the pattern engine (and likewise the forms with `+`, and `^(item separator)*item$`). It read the first item and the repeated group as sequences of characters and classes, and a `$` at the end of either was read and then dropped. So `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most, and only as the last thing in the string. `{"pattern": "^x(,a$)*$"}` was valid for `"x,a,a"`, where it is not. `^x$(,a)*$` was valid for `"x,a"`, and `^(a,$)*b$` for `"a,b"`. A pattern used for `patternProperties` or `propertyNames` was affected in the same way. A list with a `$` at the end of its first item or of its group is now matched by the pattern engine. An escaped `\$` is a literal character and such a list is matched as before. No other form of pattern was affected. A table of 101 patterns with an anchor, a word boundary or a lookaround inside one of the forms the package matches by itself, with answers from V8, is now part of the tests.

## V0.1.0

The first release of the Object Pascal port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Object Pascal.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It is a native port of the Go module and has no dependencies beyond the compiler's run-time library. Free Pascal 3.2.2 or later is the compiler it is built and tested with. The source is in the Delphi dialect and is written to be Delphi-compatible, but it has not been compiled with Delphi.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the package that agrees with V8 on 61,383 patterns and 1,057,650 match answers. A pattern runs on an automaton over ASCII text where it allows one, and on a backtracking matcher otherwise. The backtracking matcher has no step budget, so a pattern with nested quantifiers can take exponential time on a text it does not match.

- **The same results whichever compiler builds it.** The package reads Unicode properties and case mappings from its own Unicode 17 tables and never from the compiler's run-time library.

- **Every array read is range checked.** Range checking is on in every unit. On the paths every validation takes the check is one inline comparison, written out in accessor functions that raise `ERangeError` as the compiler's check does.

- **Allocation-free validation.** Validating a parsed `TJsonDocument`, or JSON text through the thread's reused buffers, allocates nothing in the steady state. A thread that has validated should call `JsonSchemaReleaseThreadScratch` before it ends.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them. The rows are the same, byte for byte, as the Go module's for every case of the JSON-Schema-Test-Suite.
