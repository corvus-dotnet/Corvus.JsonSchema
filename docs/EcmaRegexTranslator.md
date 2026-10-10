# ECMAScript Regex Translator

Translation of ECMA-262 regular expressions to .NET regular expression patterns with the same meaning, and the
check that a pattern is a valid ECMA-262 regular expression at all.

JSON Schema's `pattern` and `patternProperties` keywords are ECMA-262 regular expressions. .NET's
`System.Text.RegularExpressions` reads much of the same syntax and gives a good deal of it another meaning. The
translator is used by the runtime evaluator when it compiles a schema, by the V5 code generator for the regular
expressions of generated types, and by the generator of baked schema evaluation programs. All three therefore agree
on what a pattern means and on which patterns are valid.

The answers are tested against V8. The
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) records what V8
says about 79,221 patterns and 2,066,549 texts, and the translation gives V8's answer for every one of them in
.NET's compiled and interpreted engines.

## What It Does

- **Two grammars.** A pattern is read first with the grammar of the `u` flag, as JSON Schema specifies, and is then
  matched by code point. A pattern that is not valid with it is read with the Annex B grammar of a pattern with no
  flag, because many schemas hold such patterns (identity escapes such as `\-` or `\@`, a lone `{`), and is then
  matched by UTF-16 code unit, as ECMA-262 matches a pattern with no flag. A pattern neither grammar accepts is not
  valid.
- **Validity.** The translator refuses what ECMA-262 refuses: a quantified assertion such as `\b+` or `^*`, a
  duplicate group name, bounds out of order, a `.NET`-only construct such as `(?i)` or `(?>a)`, and so on. The error
  says what is wrong and where.
- **A tree, not a rewrite of the text.** The pattern is parsed into a tree and the tree is written for .NET, so
  nothing depends on how .NET reads a construct ECMA-262 reads differently.
- **Code points.** With the `u` flag grammar a character beyond the Basic Multilingual Plane is one character to a
  class, to the dot and to a quantifier, although .NET matches UTF-16 code units.
- **Unicode properties.** Every `\p{...}` property, value and alias ECMA-262 lists, from the Unicode data the
  translator carries (`EcmaRegexUnicodeData.cs`), so a pattern means the same on every runtime.
- **Modifier groups.** `(?i:...)`, `(?m:...)`, `(?s:...)` and their negations (ECMAScript 2025) are applied while
  the pattern is read. Case-insensitive matching follows the Canonicalize of ECMA-262 and not .NET's rules.
- **Backreferences.** A backreference to a group that has not taken part matches the empty string, and a quantified
  group forgets its captures when an iteration starts, as ECMA-262 specifies.
- **Repetitions that can match nothing.** A repetition whose body can match the empty string is written so that
  .NET's engines run it to ECMA-262's answer, and to an answer at all.

[EcmaRegexTranslations.md](./EcmaRegexTranslations.md) describes each translation.

## Quick Start

```csharp
using Corvus.Text.Json.CodeGeneration;
using System.Text.RegularExpressions;

// Either grammar: the form the evaluator and the generators use.
if (EcmaRegexTranslator.TryTranslate(@"^\d{4}-\d{2}$", out string? dotNetPattern, out string? error))
{
    Regex regex = new(dotNetPattern!, RegexOptions.CultureInvariant);
    bool isMatch = regex.IsMatch("2026-10"); // true
}
else
{
    // error is, for example, "Numbers out of order in {} quantifier at offset 6."
}
```

`EcmaRegexTranslator` is internal. Outside the library, `JsonSchemaEvaluator.ToDotNetPattern` gives the translation
the evaluator constructs for a pattern.

## API Reference

```csharp
internal static class EcmaRegexTranslator
{
    // Either grammar. False, with what is wrong, when the pattern is not a valid ECMA-262 pattern.
    public static bool TryTranslate(string ecmaPattern, out string? translated, out string? error);

    // The same, and whether the grammar of the u flag accepted the pattern (it is then matched by code point).
    public static bool TryTranslate(string ecmaPattern, out string? translated, out bool unicode, out string? error);

    // Either grammar. The pattern itself when it is not valid.
    internal static string TranslateOrFallback(string ecmaPattern);

    // The grammar of the u flag only. Throws ArgumentException for a pattern it does not accept.
    public static string Translate(ReadOnlySpan<char> ecmaPattern);

    // The grammar of the u flag only, into a caller's buffer.
    public static OperationStatus TryTranslate(
        ReadOnlySpan<char> ecmaPattern,
        Span<char> destination,
        out int charsWritten);

    // The length of the translation, for sizing that buffer.
    public static int GetMaxTranslatedLength(ReadOnlySpan<char> ecmaPattern);
}
```

| `OperationStatus` | Meaning |
|---|---|
| `Done` | Translation succeeded. |
| `DestinationTooSmall` | The destination buffer is too small. Use `GetMaxTranslatedLength` to size it. |
| `InvalidData` | The pattern is not valid with the `u` flag grammar. |

## Where An Invalid Pattern Is Refused

| Consumer | What happens |
|---|---|
| Runtime evaluator (`JsonSchemaEvaluator.Compile`, `CompileFromUri`, `ForEntryPoint`, `FromProgramImage`) | `JsonSchemaCompilationException` when the schema is compiled. The message names the pattern and says what is wrong and where. |
| `Corvus.Text.Json.Validator` | The same exception, from the `JsonSchema.From*` method that compiles the schema. |
| CLI code generator and Roslyn source generator | Generation stops with `InvalidOperationException` (the source generator reports it as its `CRV1001` diagnostic). No code is emitted that would fail when it first validates. |
| V4 code generator | Unchanged: it emits the pattern as it is. |

## Costs

The translation runs once for each distinct pattern, when a schema is compiled or code is generated. Nothing of it
runs when an instance is matched. The runtime evaluator keeps each pattern's translation for the life of the
process, so a pattern that many schemas share is read once.

A translation is usually a little longer than the pattern. It is much longer for a `\p{...}` property with members
beyond the Basic Multilingual Plane, which are written out as surrogate pairs (`\p{L}` is about 4,200 characters).

## Regenerating The Unicode Data

`EcmaRegexUnicodeData.cs` is written by `src/Corvus.Text.Json.CodeGeneration/gen-ecma-unicode-data.ps1` from the
Unicode tables of the `regress` crate, which the Rust, Go and Java ports of the evaluator also use:

```powershell
pwsh src/Corvus.Text.Json.CodeGeneration/gen-ecma-unicode-data.ps1 -RegressTables ~/.cargo/registry/src/index.crates.io-1949cf8c6b5b557f/regress-0.12.0/src/unicodetables.rs
```

## Known Limits

- **A case-insensitive backreference** (`\1` inside `(?i:...)`) is exact, and can be costly. .NET's own
  case-insensitive backreference does not compare by ECMA-262's Canonicalize, so the comparison is written out
  (see [the translations](./EcmaRegexTranslations.md#a-backreference-that-ignores-case)). The translation holds one
  alternative for each class of case variants the group can capture. For a group such as `([a-z]+)` that is three
  and the translation is under a thousand characters. For a group that can capture any letter (`(.)`, `(\p{L}+)`)
  it is every class there is, and the translation is about 160,000 characters. .NET's compiled engine would take
  one to three seconds to compile that at the first match, so a translation longer than 32,768 characters
  (`EcmaRegexTranslator.LongestCompiledTranslation`) is interpreted, by the evaluator whatever
  `CompileRegularExpressions` says and by generated types where they construct the expression. .NET constructs it
  for the interpreter in 10 to 50 milliseconds. Each character compared costs a walk to the end of the input and
  back.
- **Time.** ECMA-262 does not say how long a match may take, and a backtracking engine can take very long. The
  evaluator's match timeout (`JsonSchemaEvaluatorOptions.RegexMatchTimeout`) bounds it.
- **Unicode version.** The data is Unicode 17.0.0. The Basic Multilingual Plane part of a General_Category value
  uses .NET's own class, which follows the runtime's Unicode version.
