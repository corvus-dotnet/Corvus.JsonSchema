# Version History

The version history of `github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema`, the Go port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.3

V0.1.3 fixes a wrong result for one form of pattern, which was taken to match strings it does not match. There are no API changes. Versions 0.1.0 to 0.1.2 are affected.

### Bug fixes

- **A separated list with a `$` inside it.** The module matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without the pattern engine (and likewise the forms with `+`, and `^(item separator)*item$`). It read the first item and the repeated group as sequences of characters and classes, and a `$` at the end of either was read and then dropped. So `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most, and only as the last thing in the string. `{"pattern": "^x(,a$)*$"}` was valid for `"x,a,a"`, where it is not. `^x$(,a)*$` was valid for `"x,a"`, and `^(a,$)*b$` for `"a,b"`. A pattern used for `patternProperties` or `propertyNames` was affected in the same way. A list with a `$` at the end of its first item or of its group is now matched by the pattern engine. An escaped `\$` is a literal character and such a list is matched as before. No other form of pattern was affected. A table of 101 patterns with an anchor, a word boundary or a lookaround inside one of the forms the module matches by itself, with answers from V8, is now part of the tests.

## V0.1.2

V0.1.2 fixes a stack overflow, and a wrong `IsValid` result, for a schema whose `not` is part of a loop. There are no API changes. Versions 0.1.0 and 0.1.1 are affected.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The module abandons such an evaluation at the maximum depth (`WithMaxDepth`, 128 by default) and returns `ErrDepthExceeded`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the goroutine's stack reached its limit. That is a fatal error in Go ("stack overflow"), which ends the process and cannot be recovered. `IsValid`, `Validate`, `Evaluate` and their forms for strings and bytes were all affected. A `not` is now under the guard, so `Validate` and `Evaluate` return `ErrDepthExceeded` and `IsValid` returns false. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write.

- **`IsValid` for a schema that loops under a `not`.** With the loop elsewhere, as in `{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}`, the guard stopped the loop and counted the abandoned branch as false, and the `not` turned that into true. `IsValid` returned true for an evaluation that had been abandoned, where its documentation says false. It now returns false whenever the evaluation went beyond the maximum depth, whatever the rest of the schema came to. The same rule changes `IsValid` for a schema whose `anyOf` recovers after the depth is exceeded, such as `{"$defs": {"a": {"anyOf": [{"$ref": "#/$defs/a"}, true]}}, "$ref": "#/$defs/a"}`. Its first branch loops and is abandoned, and its second is true. `IsValid` returned true for it and now returns false, as its documentation says. `Validate` and `Evaluate` returned `ErrDepthExceeded` for both schemas, and still do.

## V0.1.1

V0.1.1 fixes a panic when compiling a schema with one form of pattern. There are no API changes. Version 0.1.0 is affected.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The module matches this form without the pattern engine and keeps the excluded set as one bit for each ASCII character, in two 64-bit words. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time as a bit number of 128 or more, which indexed past the two words. `Compile` panicked with an index out of range and returned no error, for a schema that is valid. Such a pattern is now matched by the pattern engine, and `Compile` succeeds. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.0

The first release of the Go port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Go.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It needs Go 1.25 or later and has no dependencies outside the standard library.

- **Compiled to fail-fast plans.** A schema is compiled once into a node graph and plans that hold only the checks each subschema needs. An object is checked in one pass over its properties, with the keywords of `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` branches fused into it. Property names are looked up by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **ECMA-262 patterns.** `pattern` and `patternProperties` have ECMA-262 semantics, through an engine in the module that translates to the standard library's `regexp` where the two agree and backtracks otherwise.

- **The same results with every Go release.** The module reads Unicode properties and case mappings from its own Unicode 17 tables and never from the toolchain, whose Unicode version differs between Go releases. Patterns and the `hostname`, `idn-hostname` and `idn-email` formats give the same answers with Go 1.25, 1.26 and 1.27.

- **Allocation-free validation.** Validating a parsed `Document`, or JSON text through the validator's reused buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
