---
name: corvus-ecma-regex
description: >
  Translate ECMA-262 regular expressions to .NET regex patterns with the same meaning, and decide
  whether a pattern is valid ECMA-262. Covers the two grammars (u flag, then no flag), code point
  matching through surrogate pairs, Unicode property escapes, modifier groups and case folding,
  backreferences and capture reset, repetitions whose body can match nothing, the .NET engine
  faults the translation avoids, and how the answers are tested against V8.
  USE FOR: pattern/patternProperties answers that differ from ECMA-262, a schema refused for its
  pattern, changing or extending the translator, regenerating its Unicode data.
  DO NOT USE FOR: general .NET regex usage, writing custom regex patterns.
---

# ECMAScript Regex Translation

JSON Schema's `pattern` and `patternProperties` are ECMA-262 regular expressions. The translator turns one into a
.NET pattern that gives the same answer, or says why the pattern is not valid. Full reference:
`docs/EcmaRegexTranslator.md` (overview) and `docs/EcmaRegexTranslations.md` (every translation).

## Files

| File (under `src/Corvus.Text.Json.CodeGeneration/`) | Role |
|---|---|
| `EcmaRegexTranslator.cs` | Entry points, and the `Emitter` that writes the tree for .NET |
| `EcmaRegexParser.cs` | The tree (`EcmaRegexNode`) and the parser for both grammars |
| `EcmaRegexUnicode.cs` | Sets of code points, `\p{...}` properties, case folding |
| `EcmaRegexUnicodeData.cs` | Generated Unicode tables. Do not edit. Regenerate with `gen-ecma-unicode-data.ps1` |

The four files are linked as source into `Corvus.Text.Json` (the runtime evaluator), the V5 source generator and
the V4 generator projects. **A new file must be added to all seven project files that link `EcmaRegexTranslator.cs`.**
They compile for netstandard2.0, so use no API that it lacks.

## Entry Point API

```csharp
// Either grammar. This is what the evaluator and the generators call.
bool valid = EcmaRegexTranslator.TryTranslate(ecmaPattern, out string? dotNetPattern, out string? error);

// The same, and whether the u flag grammar accepted the pattern (matched by code point) or only the
// grammar with no flag did (matched by UTF-16 code unit).
bool valid2 = EcmaRegexTranslator.TryTranslate(ecmaPattern, out string? translated, out bool unicode, out string? error2);

// Either grammar. Returns the pattern itself when it is not valid.
string pattern = EcmaRegexTranslator.TranslateOrFallback(ecmaPattern);

// The u flag grammar only. Throws ArgumentException for a pattern it does not accept.
string strict = EcmaRegexTranslator.Translate(@"\d+\.\d+");
```

## Who Calls It, and What an Invalid Pattern Does

| Caller | Invalid pattern |
|---|---|
| `PatternMatcher.Create` in `RuntimeEvaluator/Compilation/SchemaNode.cs` | `JsonSchemaCompilationException` when the schema is compiled. It runs before the regex-free matchers. |
| `CodeGeneratorExtensions.JsonSchema.cs` (`ThrowIfAPatternIsNotValid`) | `InvalidOperationException` stops the generation of the type |
| `RuntimeProgramCompiler` (baked programs) | Compiles the schema with the runtime evaluator, so the same exception |

## Translation Examples

| ECMAScript | .NET | Reason |
|---|---|---|
| `\d` | `[0-9]` | ASCII digits only |
| `$` | `\z` | .NET's `$` also holds before a final `\n` |
| `^.*$` | `^[^\n\r\u2028\u2029]*\z` | The dot leaves out four line terminators |
| `a.b` | `a(?>[^\n\r\u2028\u2029\uD800-\uDFFF]\|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]\|(?![\uDC00-\uDFFF]))\|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])b` | One character is one code point |
| `\u{1F600}` | `(?:\uD83D\uDE00)` | A surrogate pair |
| `(a)\1` | `(?<1>a)(?(1)\1)` | An unset group matches empty |
| `^(?:(a)\|b)*\1` | `^(?:(?(1)(?<-1>))(?:(?<1>a)\|b))*(?(1)\1)` | Captures are reset at each iteration |
| `^(a\|b)$` | `^(?:a\|b)\z` | No capture unless a backreference reads it |
| `(?i:k)` (u flag) | `(?:[Kk\u212A])` | Case folding is applied by the translator |
| `\-` (no u flag) | `-` | Annex B identity escape |

## Rules That Are Easy To Break

- **Sets with characters beyond the Basic Multilingual Plane** are an atomic alternation of a .NET class and
  surrogate pairs. Every alternative that takes a lone surrogate is guarded so a pair is never split. A repetition
  with no upper bound of a set that holds every such character is written as a plain class repetition, followed by
  `(?!(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF])` where what follows could match inside a pair.
- **A repetition of a set must cost what one class costs on text of the Basic Multilingual Plane.** With no upper
  bound a set with some pairs is runs, `C*(?:PC*)*`. With an upper bound the dot or a negated class is a conditional
  that looks ahead for a surrogate, `(?(?=[^\uD800-\uDFFF]{m}|[^\uD800-\uDFFF]*\z)[C]{n,m}|(?>...){n,m})`. More than
  two pair alternatives stand behind `(?=[\uD800-\uDFFF])`.
- **Never use .NET's `(?i)`** for anything but a backreference. Its tables and its culture are not ECMA-262's.
- **Never write `(?!)`**. .NET folds it away with what depends on it, and the compiled engine can then throw. The
  translator writes `(?!\z|[\s\S])`.
- **An empty alternative beside one other** is written `(?:X)?`. .NET runs `(?:a+|)+` as `a+`. It does the same
  for an alternative that is nothing in disguise (`()`, `(?=)`, a backreference to a group that never takes part),
  so `Prune` makes each of those an empty node before anything is written.
- **Lazy repetitions of a group** are written greedy (same answer, since only whether a match exists is asked).
  .NET's lazy group repetition can hang or throw. Inside a lookaround whose captures a backreference reads, order
  matters, so the lazy form is kept and guarded. A lazy repetition of a group with a highest count is never written
  as .NET's own: it is optional groups one in another, or a repetition whose iterations a balancing group counts.
- **In a lookbehind** .NET matches right to left, as ECMA-262 does. Whatever must run first is written last.

## Regex-Free Matchers

`PatternMatcher` (runtime) and `ClassifyRegexPattern` (generated types) match common shapes without a `Regex`:

| Classification | Example | Matched as |
|----------------|---------|-------------|
| `Noop` | `.*`, `[\s\S]*` | Always true |
| `NonEmpty` | `.+`, `.` | Some character that is not a line terminator |
| `Prefix` | `^foo` | `StartsWith("foo")` |
| `Range` | `^.{1,256}$`, `^.*$`, `^.+$` | No line terminator, and a length in range |
| `FullRegex` | Everything else | The translated regex |

The runtime matcher has more shapes (class sequences, separated lists, literal alternations). They read the raw
pattern with their own small parsers, so each must decline what it does not fully understand. A pattern valid only
without the u flag that could take a character beyond the Basic Multilingual Plane (a dot, a negated class, `\D`,
`\W`, `\S`) skips them, because they count code points.

## Testing Against V8

- Dataset: `~/src/ecma262-regex-test-data` (79,221 patterns with V8's answers). The translator must give V8's
  answer for all of it in both .NET engines.
- `tests/Corvus.Text.Json.RuntimeEvaluator.Tests/Unit/EcmaPatternCases.cs` holds a sample of it by class of fault,
  run through the translator (`EcmaRegexTranslatorDataTests`) and through the evaluator
  (`EcmaPatternSemanticsTests`).
- V8 itself answers some `(?i:...)` modifier groups wrongly (`K`, long s). Give V8 `i` as a flag when comparing.
- When a new fault turns up, reduce it, find out what .NET does, and change what the translator writes. Add the
  pattern with V8's answers to the tests first.

## Cross-References
- For the evaluator that uses translated patterns, see `corvus-standalone-evaluator`
- For the keyword that drives pattern validation, see `corvus-keywords-and-validation`
