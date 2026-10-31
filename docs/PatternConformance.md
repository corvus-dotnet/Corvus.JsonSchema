# Patterns Across The Ports

`pattern` and `patternProperties` are ECMA-262 regular expressions, and so is the `regex` format. Every Corvus
evaluator answers them as an ECMAScript engine does. This page says how each port matches a pattern, what each
refuses, how each limits a match, and where a port still differs from ECMA-262. Each port's own page has the detail.

## What every port does

- **Two grammars.** A pattern is read with the grammar of the `u` flag and matched by code point. A pattern that
  grammar does not accept is read with the Annex B grammar of a pattern with no flag (which accepts `\-`, `\&`, a
  lone `{` and other things many schemas hold) and matched by UTF-16 code unit, as ECMA-262 matches such a pattern.
  A pattern neither grammar accepts is refused when the schema is compiled.
- **One oracle.** Every port is tested against the
  [ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data): V8's answers for
  98,839 patterns and 4,322,391 texts. Where V8 departs from ECMA-262 the ports follow ECMA-262. A quantifier whose
  first count is greater than its second is an error whatever the size of the numbers. A group name used twice is an
  error unless the two groups are in separate alternatives. `\p{WSpace}` is not a property name ECMA-262 lists.
  `Katakana_Or_Hiragana` is a Script value that no character has. The properties are those of Unicode 17.
- **Limits.** A schema and an instance can both come from outside, so a pattern has limits of size, refused as too
  large when the schema is compiled and never as not valid, and a match has a limit of time, 1 second by default.

## The ports

| Port | How a pattern is matched | What is refused that is valid ECMA-262 | The limit of a match | Match-limit hits on the dataset |
|---|---|---|---|---|
| .NET (V5) | [Corvus.Regex.Ecma262](https://corvus-oss.org/Corvus.Regex.Ecma262/), its own ECMA-262 engine over UTF-8, and the evaluator's own matchers for a few shapes where they are faster. Generated code carries the library's compiled matchers. With `--regexEngine DotNet`, generated code holds .NET `Regex` through a translation from the parse tree | Nothing, apart from the library's limits of size | `RegexMatchTimeout`, 1 second, which throws .NET's `RegexMatchTimeoutException`, and the library's cap on backtracking memory | 0 |
| .NET (V4 generated code) | Corvus.Regex.Ecma262's compiled matchers, as V5 generated code. With `--regexEngine DotNet`, .NET `Regex` through the same translation | Nothing, apart from the limits of size | A match timeout of 1 second in the generated code, which throws .NET's `RegexMatchTimeoutException` | 0 |
| Rust, and the C library, C++, Python (Rust-backed), Ruby, PHP, R, Swift, Fortran and Perl (XS) built on it | Its own ECMA-262 engine, and the `regex` crate for patterns it proves linear | Nothing, apart from the limits of size | 1 second, as an error | 0 |
| Go | Its own ECMA-262 engine, and the standard library's `regexp` for patterns it proves linear | Nothing, apart from the limits of size | 1 second, as an error | 0 |
| Object Pascal | Its own ECMA-262 engine | Nothing, apart from the limits of size | 1 second, as an exception | 0 |
| Java and Kotlin | `java.util.regex`, through a translation | A backreference to a group of a lookbehind that only matching backwards can tell (689 patterns of the dataset), and 4 over the limits of size | 1 second, and 8 MiB of what `java.util.regex` holds for one pattern | 6 to 7 |
| Julia | PCRE2, through a translation | A backreference into a lookbehind that PCRE2 cannot write with the same meaning (467 patterns of the dataset), and 139 corner cases of the grammar with no `u` flag | 1 second, and PCRE2's count of steps | 2 |
| Python (pure) | `re` for patterns it proves linear, the `regex` package for the rest | 1 pattern of the dataset | 1 second by the clock | 9 to 10 |
| Perl (pure) | Its own ECMA-262 engine, a port of the Go module's, and Perl's own engine for patterns it proves linear | Nothing, apart from the limits of size | 1 second by the clock, as an exception | 34 |
| TypeScript and JavaScript | The host's own `RegExp` | Nothing | None | Not applicable |

The counts of refusals and of match-limit hits are from runs over the 13 files of the dataset: .NET on 2026-10-16,
pure Perl on 2026-10-20, the others on 2026-10-12. A match-limit hit is a match that reached the limit and so has no answer. It is never a
wrong answer. No port gives a wrong answer on the dataset.

## Where the ports differ

These are the known differences from ECMA-262, or between the ports, that the dataset does not count as wrong
answers. Each is a decision recorded here, with an issue where there is work to follow.

### .NET

- **Only a backreference can make a match backtrack without end.** Corvus.Regex.Ecma262 remembers where a way has
  failed, as the other ports' engines do, so `^(a|aa)+$` on a long run of `a` answers at once. A backreference keeps
  a pattern from that memo, and `^(a|aa)+\1$` on the same text runs until the timeout. See
  [the timeout](RuntimeEvaluator.md#regular-expression-match-timeout).
- **Draft 2019-09 and draft 2020-12 read the grammar of ECMAScript 2020,** the edition they cite, so a modifier group
  such as `(?i:a)` or a duplicate group name is refused under them, as today's grammar accepts it under the other
  dialects. `RegexEdition` (and the generators' `--regexEdition`) chooses another.
- **A JSON string with a lone surrogate throws.** The parser accepts an instance string that holds the escape of half
  of a surrogate pair, such as `"\ud800"`, and `pattern`, `minLength` and `maxLength` then throw
  `InvalidOperationException` (in V4 generated code too). Every other port refuses such a document when it parses
  it, or reports it as not valid. The 46,438 texts of the dataset's `lone-surrogates.jsonl` all throw
  ([#1021](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1021)).
- **`idn-email` has a limit of 320 bytes on the whole address.** The Rust crate has no limit on the address as a
  whole. The host name of the address has the limit of 253 characters as A-labels in both ([#1029](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1029)).
- **.NET Framework and a large count, with the DotNet engine.** .NET Framework's `Regex` makes a string of the count
  of a repetition of one character when it is constructed, so `^a{100000000}$` costs some 200 MB there, and a count
  no string can reach is written as a part that never matches. .NET does neither, and nor does Corvus.Regex.Ecma262.

### Java and Julia

- **Refused lookbehinds.** `java.util.regex` and PCRE2 match a lookbehind forwards, and ECMA-262 matches it
  backwards. Where a backreference reads a group whose text depends on that, the pattern is refused when the schema
  is compiled, with a message that says so. See [Java](JsonSchemaForJava.md) and [Julia](JsonSchemaForJulia.md).
- **Julia 1.10** bundles a PCRE2 whose case folding is that of Unicode 14, so thirty case pairs are not equivalent in
  a case-insensitive backreference there. Julia 1.13 answers exactly.
- **Julia: nested repetitions are left to PCRE2.** A pattern such as `^(?:(?:(?:a*)*)*)*b$` is written as
  `(?:(?:(?:a+)+)+)*`, which means the same, and PCRE2 backtracks through every way to divide a run among its
  levels: such a match on sixteen characters reaches the limit of a match. The ports with their own engine finish it.
- **Julia: a pattern too large to be timed.** A pattern that PCRE2 holds only without the code that reads the clock,
  such as an alternation of 3,000 words, is run without it, and its match ends in its first stage.

### Python

- The limit is of time on the clock. The `regex` package's timeout counts the time of the process on the processor,
  so with three or more other threads busy a match can run for longer than the limit before it is stopped.
- A text of more than 1,048,576 characters is not matched by a pattern that is not proved linear, once the first try
  at a match has been abandoned.
- `re` has no limit on the memory of a match. It runs only patterns proved linear.

### Perl

- The engine of its own is written in Perl, so it takes many times longer than the Go module's for a pattern that
  backtracks: the 34 texts of the dataset that reach the limit of one second all get V8's answer with a limit of 30
  seconds. Perl's own engine runs the patterns proved linear, and they need no limit.
- Perl 5.40 compiles a class whose members are one case folding set, such as `[\x{1f80}\x{1f88}]`, to a node that
  compares with full case folding, which matches neither member of that class. The package never writes such a
  class for Perl's engine, and its own engine tests a small class without one.

### TypeScript and JavaScript

- **No limit of a match.** A `RegExp` match cannot be interrupted, so a pattern that backtracks without end, on a
  text made for it, does not return. The answers are those of the host engine, which are V8's on Node and in Chrome.
  A port of the ECMA-262 engine, which would give a limit and the same answers on every host, is to follow
  ([#1026](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/1026)).