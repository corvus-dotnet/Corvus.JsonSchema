# Version History

The version history of `io.github.corvus-dotnet:corvus-json-schema`, the Java port of the Corvus.Text.Json V5 runtime evaluator. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.1

Regular expressions that follow ECMA-262 where 0.1.0 followed `java.util.regex`.

### Bug fixes

- **Every Unicode property of ECMA-262, with the same data on every JDK.** `\p{...}` now covers all 53 binary properties and their aliases. 0.1.0 knew about 20, and a pattern with any other, such as `\p{Dash}`, was read as the literal text `p{Dash}`. `\p{Emoji}`, `\p{Emoji_Presentation}` and `\p{Extended_Pictographic}` had that fault on Java 17, whose `java.util.regex` lacks them. `\p{Script_Extensions=...}` (`scx`) is now its own property and no longer Script, and `\p{White_Space}` is the Unicode property, where 0.1.0 used the set of `\s` (which has U+FEFF and lacks U+0085). The data for General_Category, Script, Script_Extensions and the binary properties is Unicode 17, carried by the library, so a pattern no longer means something different on Java 17, 21 and 25.

- **ECMAScript 2025 pattern syntax.** Modifier groups (`(?i:...)`, `(?m:...)`, `(?s:...)`, `(?i-s:...)`), a group name shared by groups in separate alternatives, and `\u` escapes in group names are now valid, as they are in V8. Case-insensitive matching is the Canonicalize of ECMA-262 (Unicode simple case folding with the `u` flag grammar), `^` and `$` in a multiline group see ECMA-262's four line terminators, and `\w` and `\b` in a case-insensitive group count U+017F and U+212A.

- **Lookbehind of any length, counted in characters.** A lookbehind whose length `java.util.regex` cannot bound, such as `(?<=(?:ab)+)x`, was an error, and one with two unbounded repetitions, such as `(?<=(\d+)(\d+))$`, compiled and never matched. Both now match as ECMA-262 says. A lookbehind before a character beyond the Basic Multilingual Plane counted UTF-16 code units, so `(?<=^.{2})a` missed a string that starts with an emoji. It now counts characters.

- **Backreferences that mean what ECMA-262 says, or an error.** A backreference to a group with alternatives, such as `^(a|b|c)+?\1$`, tested the wrong thing and matched strings it should not. A backreference could also read what a group captured in an earlier iteration of a repetition (`^(?:(a)|b)*\1$`) or in a negative lookahead (`(?!(a))\1b`), which ECMA-262 unsets. These are fixed. A few valid patterns have a backreference that `java.util.regex` cannot give the ECMA-262 meaning (one inside a lookbehind, or to a group inside one; one to a group of a lookaround that may not have matched; one to a group that the repetition holding both can skip, or of a repetition that can match the empty string; a case-insensitive one, unless the only cased characters its group can hold are ASCII letters, without `k` and `s` under the `u` flag grammar). Compiling a schema with one now throws `SchemaCompilationException` with a message that starts "Unsupported regular expression" and names the construct. 0.1.0 ran some of them with another meaning.

- **Smaller corrections.** Leading zeros in a `\u{...}` escape (`\u{0000000061}`) are read. Group names are checked against ID_Start and ID_Continue of Unicode 17, and script names against the scripts of Unicode 17, on every JDK. Groups nested more than 200 deep make a pattern invalid, where 0.1.0 overflowed the stack, in a schema and in a string checked with the `regex` format.

## V0.1.0

The first release of the Java port of `Corvus.Text.Json.RuntimeEvaluator`.

### New features

- **A JSON Schema evaluator for Java and Kotlin.** It covers draft 4, 6, 7, 2019-09 and 2020-12 and passes all 7,966 tests of the JSON-Schema-Test-Suite, with the same single exclusion as the C# runner. It runs on Java 17 or later, is one jar with no dependencies, and can be used from Kotlin as it is.

- **Compiled to JVM bytecode.** A schema is compiled once into a hidden class, one method per schema node holding only the checks that node needs, which the JIT then optimises as it would hand-written code. Property names are dispatched by length and 64-bit words, common pattern shapes are matched without a regular expression engine, and `unevaluatedProperties` is decided from the coverage known at compile time where it can be.

- **Allocation-free validation.** Validating a parsed `JsonDocument`, or JSON text through the validator's reused per-thread buffers, allocates nothing in the steady state.

- **Results and annotations.** Evaluation with a results collector at the Basic, Detailed or Verbose level gives the same rows as the C# `JsonSchemaResultsCollector`, and annotations as `JsonSchemaAnnotationProducer` extracts them.
