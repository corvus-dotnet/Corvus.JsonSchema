# Version History

The version history of the `corvus-json-schema` Rust crate. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.7

V0.1.7 fixes wrong results for several forms of pattern, a validation that did not end, and a form of pattern that is not valid and was accepted. There are no API changes. Versions 0.1.0 to 0.1.6 are affected.

### Bug fixes

- **A separated list with a `$` inside it.** The crate matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without a regular expression engine (and likewise the forms with `+`, and `^(item separator)*item$`). It read the first item and the repeated group as sequences of characters and classes, and a `$` at the end of either was read and then dropped. So `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most, and only as the last thing in the string. `{"pattern": "^x(,a$)*$"}` was valid for `"x,a,a"`, where it is not. `^x$(,a)*$` was valid for `"x,a"`, and `^(a,$)*b$` for `"a,b"`. A pattern used for `patternProperties` or `propertyNames` was affected in the same way. A list with a `$` at the end of its first item or of its group is now matched by a regular expression engine. An escaped `\$` is a literal character and such a list is matched as before. No other form of pattern was affected. A table of 101 patterns with an anchor, a word boundary or a lookaround inside one of the forms the crate matches by itself, with answers from V8, is now part of the tests (`tests/pattern_anchors.rs`).

The fixes below are for the patterns that no matcher of the crate's own and not the `regex` crate takes: those with a lookaround, a backreference or a modifier group. They are matched by the [`regress`](https://crates.io/crates/regress) crate, which answers some of them differently from ECMA-262. It is now given another pattern with the same meaning for those, and every other pattern as it is written (`src/pattern/rewrite.rs`). Nothing is added to the matching of a string. Compiling a schema reads each such pattern once more, which takes about a microsecond.

- **A repetition inside a repetition that did not end, or lost its count.** `regress` 0.12 sets the iteration count of a repetition to zero each time the repetition starts, and going back by backtracking does not put the old count back. So a repetition that is inside another one has a count of zero when the match comes back into an earlier run of it. With a body that can match the empty string the match then went round for ever. `{"pattern": "^(?!-)(?:(?:a*)*)*x"}` on `"a"` took memory until the process was stopped, and no instance was needed beyond that one string. With other bodies the count was wrong. `{"pattern": "^(?!-)(?:(?:(?:a){1,}){1}){3}$"}` was not valid for `"aaa"`, where it is. A repetition is affected when it is inside another one, its body has an alternation or a repetition of its own, and it is not `X*` over a body that cannot be empty. It is now given to `regress` without a count. `X{m,n}` is `m` copies of `X` and `n - m` optional copies, each an alternation, and `X{m,}` is `m` copies and `(?:X)*`. ECMA-262 refuses an iteration that matches nothing once the minimum is met, and each copy after the minimum is written to say so. A backreference reads the group of its own copy, and from outside the repetition, the last copy that ran. The copies multiply with the repetitions around them. A pattern that would come to more than 65,536 characters, or to groups nested deeper than the 256 `regress` reads, does not compile, and the error says that the pattern is valid and too large (`^(?!-)(?:(?:a|x){1,300}-)+$` is one). A repetition whose body `regress` cannot come back into, such as `[a-z]{1,300}` or `(?:ab){300}`, is not written out and has no such limit.

- **A repetition of a body that can only match the empty string.** A body of assertions and lookarounds, as in `(?:^|\b|(?=a)){4,}`, matches at one position however often it is repeated. `regress` tried every way through every iteration, which for a few such repetitions inside each other took minutes. The body is now matched once, or not at all for a minimum of zero, which is what ECMA-262's rule for empty iterations comes to.

- **A backreference to a group from inside the group.** A group has no value until it closes, so `\1` inside group 1 matches the empty string. `regress` read the value the group had in an alternative it had backtracked out of. `{"pattern": "(x|\\1)x"}` was not valid for `"x"`, where it is.

- **Two groups with one name.** ECMAScript 2025 allows it for groups in separate alternatives. A reference to the name matched the empty string whichever group had matched, so `{"pattern": "^(?:(?<n>a)|(?<n>x))\\k<n>$"}` was valid for `"a"`, where only `"aa"` and `"xx"` are.

- **A quantifier on a word boundary.** `\b+`, `\B*` and `\b{2}` are not valid in ECMA-262, with or without the `u` flag. They were accepted. A schema with such a pattern now fails to compile, and the `regex` format refuses it.

- **A case-insensitive group.** In a pattern that is valid only without the `u` flag, such as one with `\&` or `\-`, two characters are equal in `(?i:...)` when the uppercase forms of their UTF-16 code units are. `regress` compared by Unicode case folding as it does with the flag, so `{"pattern": "^(?i:s)\\&?$"}` was valid for `"\u017f"` (the long s), and `^(?i:k)\&?$` for the Kelvin sign. The literals and classes of such a group are now written out as the classes they stand for. With the `u` flag, `\W` in a class of such a group, as in `(?i:[\W])`, held the two characters that fold to a word character and so matched `s` and `k`. One difference remains. A backreference inside a case-insensitive group, in a pattern valid only without the `u` flag, is still compared by case folding, so `{"pattern": "^(?i:(\u017f)\\1)\\&?$"}` is valid for the long s followed by `S`, where ECMA-262 says it is not.

The crate now gives V8's answer for every pattern and text of the [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data): 79,221 patterns, of which 24,843 are not valid, and 2,054,925 texts. (A text with a character beyond the Basic Multilingual Plane is left out for a pattern that is valid only without the `u` flag, as V8 matches code units there.) `tests/pattern_engine.rs` has 83 such patterns with V8's answers, on every text of up to three characters over `a`, `x` and `-` or, for the case-insensitive groups, on 19 texts.

## V0.1.6

V0.1.6 fixes a stack overflow, and a wrong `is_valid` result, for a schema whose `not` is part of a loop. There are no API changes. Versions 0.1.0 to 0.1.5 are affected.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The crate abandons such an evaluation at `CompileOptions::max_depth` (128 by default) and reports `SchemaEvaluationDepthError`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the stack overflowed, and a stack overflow aborts the process. Every function was affected (`is_valid`, `validate`, `validate_json`, `validate_instance` and the three `evaluate` functions). A `not` is now under the guard, so these functions return the depth error and `is_valid` returns false. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write.

- **`is_valid` for a schema that loops under a `not`.** With the loop elsewhere, as in `{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}`, the guard stopped the loop and counted the abandoned branch as false, and the `not` turned that into true. `is_valid` returned true for an evaluation that had been abandoned, where its documentation says false. It now returns false whenever the evaluation went beyond the maximum depth, whatever the rest of the schema came to. The same rule changes `is_valid` for a schema whose `anyOf` recovers after the depth is exceeded, such as `{"$defs": {"a": {"anyOf": [{"$ref": "#/$defs/a"}, true]}}, "$ref": "#/$defs/a"}`. Its first branch loops and is abandoned, and its second is true. `is_valid` returned true for it and now returns false, as its documentation says. The functions that return a `Result` reported the depth error for both schemas, and still do.

## V0.1.5

V0.1.5 corrects the minimum Rust version the crate declares. There are no code or API changes.

### Bug fixes

- **The crate declares Rust 1.89 as its minimum (`rust-version`), not 1.85.** Versions 0.1.0 to 0.1.4 declared 1.85 but do not build with it: the `regress` dependency (0.12) uses `let` chains, which need Rust 1.88, and the crate's own source needs 1.89. With the declared version, Cargo chose these versions for a 1.85 toolchain and the build then failed in `regress` with "`let` expressions in this position are unstable". The crate's CI now builds it with the version it declares.

## V0.1.4

V0.1.4 fixes wrong validation results for one form of pattern. There are no API changes. Versions 0.1.0 to 0.1.3 are affected.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The crate matches this form without the regular expression engine and keeps the excluded set as one bit for each ASCII character. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time as a bit number of 128 or more. In a release build the shift wrapped, so the set held unrelated ASCII characters (`C` and `)` for `é`) and not the member. The pattern then rejected valid strings (`"C1"`) and accepted invalid ones (`"é1"`). In a debug build, compiling the schema panicked with a shift overflow. Such a pattern is now matched by the regular expression engine, with the results ECMA-262 gives. A pattern of this form whose excluded set is all ASCII was never affected, and is still matched without the engine.

## V0.1.3

V0.1.3 validates JSON text without allocating, and reads a number in a schema and the same number in an instance as the same double. There are no breaking changes.

### New features

- **`Validator::validate_json` and `Validator::evaluate_json`.** They take JSON text, parse it into the `JsonDocument` parser's per-thread buffers and evaluate it in place: in the steady state a validation of JSON text allocates nothing (a test counts allocations). `JsonValidationError` tells invalid JSON (with its byte offset) from a schema that recurses in place beyond the maximum depth. A format validator that itself validates JSON text, during a validation of JSON text, gets buffers of its own. Use `JsonDocument` to validate the same text more than once.

### Bug fixes

- **Numbers in schemas and instances are read alike.** The crate turns on serde_json's `float_roundtrip` feature. Without it, serde_json read some numbers one unit in the last place off the correctly rounded double, including its own output (`9.727837981879871e+26`), while `JsonDocument` rounds correctly. A schema passed as JSON text and an instance validated as JSON text could then hold two different doubles for one literal, so an `exclusiveMaximum` equal to the instance passed. The JSON-Schema-Test-Suite's `bignum.json` found it, run through the C library.

## V0.1.2

V0.1.2 adds `JsonDocument`, a parsed form of JSON text that the evaluator reads in place, and makes validation faster on schemas where the Blaze C++ validator was faster. There are no breaking changes, and every result is unchanged.

### New features

- **`JsonDocument` parses JSON text for evaluation.** `JsonDocument::parse` reads JSON text into one flat array of values, with strings borrowed from the text where they have no escapes, and `JsonDocument::root` is an `Instance` for `Validator::validate_instance` and `Validator::evaluate_instance`. Parsing takes about a quarter of the time of parsing into a `serde_json::Value`, and a document costs one allocation (two when strings need unescaping), against one or more per string, array and object for a `Value`. Validation reads it at least as fast as a `Value`. It accepts what serde_json accepts: nesting up to 127 levels, no lone surrogates in `\u` escapes, numbers within the range of a double; of duplicate property names, the last value is kept at the position of the first. Doubles are correctly rounded, where serde_json's parser (without its `float_roundtrip` feature) can be one unit in the last place off. `JsonDocument::to_value` converts a document to a `serde_json::Value`.

### Other changes

- **Faster validation.** On the sourcemeta jsonschema-benchmark corpora, measured with the benchmark's own harness against Blaze:
  - The `\w` test of the matcher for patterns such as `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$` no longer rebuilds its character set for every character: cspell validates more than five times faster.
  - A pattern that is valid only without the `u` flag (an identity escape such as `\&`) runs on the `regex` crate for strings within the Basic Multilingual Plane, instead of always on the backtracking engine: krakend validates about 30% faster.
  - Small objects cost less to evaluate: a plan's shape is entered directly, a few declared names are looked up in a small object instead of matching each of its properties, a property name that is not the next declared one is tried as the next in sorted order, and fused object plans no longer allocate per evaluation.

  With a `JsonDocument`, the crate now parses and validates faster than Blaze on all 37 corpora (geometric mean about 0.37 of Blaze's time), and validates already-parsed instances faster on 34 of them (about 0.54).

## V0.1.1

V0.1.1 makes the evaluator generic over the instance it reads.

### New features

- **The `Instance` trait.** `Validator::validate_instance` and `Validator::evaluate_instance` take any `Instance`: a value shown as one of the six JSON kinds, with arrays and objects read in place through `ArrayView` and `ObjectView`. `&serde_json::Value` implements it, and so can a host language's own values, which can then be validated without converting them to `serde_json::Value` first. The `corvus-json-schema-rs` Python package reads Python objects this way.

## V0.1.0

The first release: a port of the Corvus.Text.Json V5 runtime evaluator (`Corvus.Text.Json.RuntimeEvaluator`) to Rust, for drafts 4, 6, 7, 2019-09 and 2020-12. It passes the whole JSON-Schema-Test-Suite and produces the same results and annotations as the C# evaluator.
