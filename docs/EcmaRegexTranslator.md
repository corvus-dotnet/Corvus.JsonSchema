# ECMAScript Regex Translator

> **Scope.** Since V5 and V4 match patterns with Corvus.Regex.Ecma262, the translator is used only where .NET's
> `Regex` is still wanted: V5 and V4 generated code under `--regexEngine DotNet` (MSBuild
> `CorvusTextJsonRegexEngine=DotNet`, or `CorvusJsonSchemaRegexEngine=DotNet` for V4), and
> `JsonSchemaEvaluator.ToDotNetPattern`. The runtime evaluator and the default
> generated code take their answers, validity included, from the library; see
> [RuntimeEvaluator.md](./RuntimeEvaluator.md#patterns).

Translation of ECMA-262 regular expressions to .NET regular expression patterns with the same meaning, and the
check that a pattern is a valid ECMA-262 regular expression at all.

JSON Schema's `pattern` and `patternProperties` keywords are ECMA-262 regular expressions. .NET's
`System.Text.RegularExpressions` reads much of the same syntax and gives a good deal of it another meaning. The
translator writes a .NET pattern with the ECMA-262 meaning for the code that still matches with .NET's `Regex` (see
the scope note above).

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
    // Every method throws EcmaRegexPatternTooLargeException (an InvalidOperationException) for a pattern over a
    // limit of size. It is never the answer or the exception of a pattern that is not valid.

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

    // What a translation costs .NET to construct, as the limit on it counts it.
    internal static long ConstructionCost(string dotNetPattern);

    // The steps a pattern takes to read and to write, as the limit on them counts them.
    internal static long StepsToTranslate(string ecmaPattern);
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
| V4 code generator (`--engine V4`, `Corvus.Json.SourceGenerator`, `Corvus.Json.Validator`) | The same as the V5 generators: generation stops with `InvalidOperationException`, which its source generator reports as `CRV1001`, with the library's account of what is wrong (or the translator's, under the DotNet engine). |

## Limits On The Size Of A Pattern

A schema may come from outside, and so may its patterns. No pattern is to make the translator, or .NET when it
constructs a regular expression from the translation, run for long or hold much memory. So a pattern has limits of
size (`EcmaRegexLimits`), and a pattern over one of them is refused when the schema is compiled or the code is
generated. The limits are fixed. None of them is an option.

| Limit | Value | What it bounds | The message ends |
|---|---|---|---|
| Length of the pattern | 1,048,576 characters | Reading it. | `It is longer than 1,048,576 characters.` |
| Nesting of groups | 200 deep | The recursion of the reader and of the writer, and of .NET when it reads the translation. | `Its groups nest more than 200 deep.` |
| Steps | 2,000,000 | The time and the memory of reading and writing. A step is one character read or one node of the tree visited, and the work on the ranges of a set is counted in the same measure (two ranges sorted are a step, sixteen copied are one). Steps are counted, and no clock is read, so the same pattern is refused or accepted on every machine. | `It takes more than 2,000,000 steps to translate.` |
| Length of the translation | 1,048,576 characters | What is written, and what a regular expression keeps of it. It is looked at as the translation is written, so a pattern that would be written as gigabytes is refused after the first megabyte. | `What is written for .NET from it is longer than 1,048,576 characters.` |
| Copies | 65,536 beyond four times the size of the pattern | .NET writes out a repetition with a least count of 2, 3 or 4, one inside another too, to find the text every match starts with, and keeps what it wrote. Twenty-four counts of 2 one inside another take it 220 ms and 100 MB, and thirty do not fit in memory. | `Its repetitions with a least count of 2, 3 or 4, one inside another, come to more than 65,536 copies of what they repeat, beyond four times its size.` |
| Cost of construction | 100,000,000 | .NET joins the pieces of literal text of a sequence one at a time and copies what it has joined each time, moves the terms that follow a plain group to put the group's terms in their place, and goes through the alternatives of an alternation once for each. The cost is the pieces of text of each sequence times the length of that text, its plain groups times its terms, and a quarter of the square of the alternatives of each alternation (`EcmaRegexTranslator.ConstructionCost`). | `Its sequences and alternations are longer than .NET constructs a regular expression from in bounded time: by the count of the translator, a cost of more than 100,000,000.` |

The number of groups has no limit of its own. The steps bound it: every group is a node, and what is worked out
for each group against each other group is counted.

**The error is not the error of a pattern that is not valid.** The message starts with the pattern, or with its
first 120 characters and its length, and says one of two things. When the whole pattern was read before the limit
was met, it is `The pattern '...' is a valid ECMA-262 regular expression, and is too large.` When the limit was met
while reading (the length, the nesting, the steps of reading), the rest of the pattern has not been read, so the
message is `The pattern '...' is too large to read as an ECMA-262 regular expression.` and says nothing of whether
it is valid. A pattern that nests too deep and is also not valid is refused as too large.

| Consumer | What happens |
|---|---|
| Runtime evaluator (`JsonSchemaEvaluator.Compile`, `CompileFromUri`, `ForEntryPoint`, `FromProgramImage`, `ToDotNetPattern`) | `JsonSchemaCompilationException` when the schema is compiled, with that message. |
| `Corvus.Text.Json.Validator` | The same exception, from the `JsonSchema.From*` method that compiles the schema. |
| CLI code generator | Generation stops with an exception whose message names the keyword too: `The pattern '...' of the keyword 'pattern' is a valid ECMA-262 regular expression, and is too large. ...`. Its type derives from `InvalidOperationException`, the type of the error of a pattern that is not valid. |
| Roslyn source generator | The diagnostic `CRV1003`, with that message. A pattern that is not valid is `CRV1001`. |
| V4 code generator (`--engine V4`, `Corvus.Json.SourceGenerator`) | The same as the V5 generators: the exception, and `CRV1003` from its source generator. |

### What an ordinary pattern can meet

A pattern of a few hundred characters meets a limit only where what is written for it, or what .NET does with it,
grows much faster than the pattern.

- **Seven or more case-insensitive backreferences to a group that can hold any letter** (`(?i:(.)\1\1\1\1\1\1\1)`).
  Each is written as a comparison of about 157,000 characters with the `u` flag grammar and about 117,000 without
  (see [Known Limits](#known-limits)), so six are under the limit on the translation and seven are over it. A
  backreference to a group of ASCII letters is a few hundred characters, and hundreds of those are under it.
- **Repetitions written more than once, one inside another.** A repetition whose iterations that need not run are
  made to fail by the check that reads the rest of the input is written as its body twice (see
  [the translations](./EcmaRegexTranslations.md#repetitions) for where the check is kept). So is a lazy repetition
  with a highest count inside a lookaround whose captures are read, up to eight times. Fourteen of the first one
  inside another, in such a lookaround, or six of the second, are over the limit on the translation. Outside such
  a lookaround a repetition of a body that holds a group a backreference reads is written without the check, and
  sixty one inside another are under the limit.
- **Counts of 2, 3 or 4 one inside another**, from about twelve counts of 2 or eight counts of 4
  (`(?:(?:(?:a{4}){4}){4}...`). A count of 5 or more is not written out by .NET, and neither is a count of 1 or a
  range from 0, so `(?:(?:a{1000}){1000}){1000}` is under the limit.
- **A dot written eight thousand times.** With the `u` flag grammar one dot is 126 characters (see
  [the translations](./EcmaRegexTranslations.md)). `.{8000}` is one dot and a count.
- **One sequence of more than about ten thousand characters that are written as escapes**: characters outside
  ASCII, which are written `\uXXXX`, and escaped punctuation such as `\.`. For characters beyond the Basic
  Multilingual Plane it is about six thousand. Text of letters and digits is one piece however long it is, and the
  alternatives of an alternation are sequences of their own, so a list of ten thousand words is under the limit.
- **An alternation of more than twenty thousand alternatives**, or a pattern of more than about 285,000 letters,
  which is 2,000,000 steps.
- **More than about ten thousand groups that no backreference reads, one after another in one sequence**
  (`(a)(b)(c)...`), which are written as plain groups.

### What the limits were measured against

A probe gives the translator and both of .NET's engines 119 shapes built to cost as much as they can: 111 patterns
of at most 1,000 characters against texts of at most 100,000, and 8 patterns of up to 700,000 characters. They are
repetitions of what can match nothing, one inside another, backreferences in repetitions, counts large and one
inside another, 10,000 open parentheses, long alternations, lookbehinds over a long text, case-insensitive
backreferences to groups that hold any character, and classes of large properties. Before the limits, and with the
match limit at 10 seconds, ten of its runs were stopped when the process passed 3 GB, five of them in the
translator and five in .NET's `Regex` constructor, and one pattern of 610 characters was translated to 49 million
characters and kept 453 MB.

With the limits, on a machine with a load of 7 to 8 from other work, every run ends in an answer, in the error of
a pattern that is too large, or in `RegexMatchTimeoutException`:

| For a pattern of at most 1,000 characters | The most measured |
|---|---|
| Time to translate it or to refuse it | 110 ms |
| Time for .NET to construct the regular expression | 43 ms |
| Memory kept for it, the translation and the regular expression | 3.5 MB |
| Time of one match, which is the match limit | 1.01 s |
| Memory of the process while it matched | 195 MB |

The eight larger patterns, and forty more of up to a megabyte, are translated or refused in at most 0.31 seconds,
and what is accepted of the eight is constructed in at most 45 ms and keeps at most 6.8 MB. At the limits themselves, .NET keeps about 11 bytes for each character
of a translation it interprets, so a translation of 1,048,576 characters keeps about 11 MB for as long as an
evaluator holds it.

A pattern that is over no limit is translated in the steps it takes. The most that a pattern of the
[ECMA-262 regular expression test data](https://github.com/corvus-dotnet/ecma262-regex-test-data) takes is 180,606
steps and a translation of 946,581 characters, for a pattern with six case-insensitive backreferences. None of its
98,839 patterns is over a limit.

## Costs

The translation runs once for each distinct pattern, when a schema is compiled or code is generated. Nothing of it
runs when an instance is matched. The runtime evaluator keeps each pattern's translation and regular expression for
the life of the process, so a pattern that many schemas share is read once. What it keeps is bounded, as a process
may compile schemas from outside for as long as it runs: at most 4,096 patterns and 1,048,576 characters of patterns
and translations, with no pattern or translation longer than 32,768 characters among them. Past either bound
everything kept is let go, and a pattern met again is read again. An evaluator keeps its own regular expressions
for as long as it lives, whatever is let go.

A translation is usually a little longer than the pattern. It is much longer for a `\p{...}` property with members
beyond the Basic Multilingual Plane, which are written out as surrogate pairs (`\p{L}` is about 4,200 characters).

## Regenerating The Unicode Data

`EcmaRegexUnicodeData.cs` is written by `src/Corvus.Text.Json.CodeGeneration/gen-ecma-unicode-data.ps1` from
`src/unicodetables.rs` of the `regress` crate, which that crate generates from the Unicode Character Database. The
crate is only where the data is read from. No port of the evaluator depends on it, and the Rust crate carries a port
of the project's own ECMA-262 engine. The tables of the Go and Java ports were read from the same file:

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
- **The engine.** Two kinds of translation are run by .NET's interpreted engine whatever
  `CompileRegularExpressions` says, and have no compiled form in generated code: one longer than 32,768 characters
  (above), and one that keeps a lazy repetition of a group, which starts with `(?#interpreted)`. The compiled engine
  does not always return from a match of the second kind
  (see [the translations](./EcmaRegexTranslations.md#repetitions)). Only a pattern with a lazy repetition of a group
  inside a lookaround whose captures a backreference reads has such a translation.
- **Memory while a match runs.** The match limit bounds the time of a match and not its memory. A backtracking
  engine keeps what it may have to come back to, and a match that runs to the limit can keep more for as long as
  it runs. The most the probe above provoked in the second a match is given is 195 MB for the whole process, 160 MB
  of it the match: a repetition over 110 groups that a backreference reads, on 80,000 characters, in the
  interpreted engine. .NET has no limit on the memory of a match. Where neither the schema nor the instance is
  trusted, set a shorter `RegexMatchTimeout`, bound the length of the instance, or give the process a memory limit.
- **Code that was generated before the match limit has none.** The limit is written into generated code when it
  is generated, so code generated by an earlier version has none until it is generated again.
- **Time.** ECMA-262 does not say how long a match may take, and a backtracking engine can take very long. The
  evaluator's match limit (`JsonSchemaEvaluatorOptions.RegexMatchTimeout`, 1 second unless it is set) bounds it,
  and a match that reaches it throws `RegexMatchTimeoutException` and gives no answer. V8 answers every text of
  the ECMA-262 regular expression test data in under 250,000 backtracks. One of its 98,839 patterns is at the
  limit here: `^a{2}?(((?!)|((|a){2,}(|a)\B|){2,3}?){2,3}.{3}a{2})\4`, whose hardest text takes V8 238,314
  backtracks. It repeats, several deep, a body that matches the empty string in more than one way, and .NET goes
  through every way where V8 does too, at many times the cost of a step. The compiled engine takes about 0.7
  seconds for that text and the interpreted one about 1.8 (with a load of 2 to 4 on the machine), most of it the
  unsetting of a group when each iteration starts and the written-out `\B`. So the interpreted engine meets the
  limit, and the compiled one meets it on a busy machine: in two runs of the test data with a load of 5 to 7 the
  pattern met the limit in seven of the eight configurations. A second pattern, of 190 characters with `(a|)(a|)`
  in a group that is read, three repetitions deep, whose hardest text takes V8 113,530 backtracks, is answered by
  the compiled engine in about 0.05 seconds and by the interpreted one in about a third of a second (load 2). In
  those two runs it was answered in every configuration but the interpreted one, where four matches at a time on
  the busy machine took it past the limit. A translation gives V8's answer and not V8's time, in four ways.
  - A repetition whose body can match the empty string, with no backreference in it and no group that one reads
    (`(?:a?b?)*`), is written as it is. .NET takes an iteration that matched nothing and, when the rest fails, tries
    again without it, where ECMA-262 refuses the iteration. A search that passes twenty such repetitions is made
    two to the power of twenty times: `^(?:(?:a?b?)*c){1,22}d$` against twenty `c` and an `e` is 42 backtracks in
    V8 and about a million paths in .NET. The same pattern costs the same with no translation at all.
  - A lazy repetition of anything but one code unit is written greedy, and an empty alternative before another
    (`(|a)`) as an optional group. The answer does not depend on the order of the paths, and the time to find a
    match does: .NET can search long before it reaches the path V8 takes first.
  - Unsetting groups when an iteration starts and refusing an iteration that matched nothing are written with
    conditionals, balancing groups and lookarounds, so a step of such a search costs more in .NET than in V8.
  - Where an iteration that need not run is made to fail by the check that reads the rest of the input
    (see [the translations](./EcmaRegexTranslations.md#repetitions) for the five places), .NET's interpreted engine
    takes time in proportion to the square of the length of the text. The compiled engine does not.
- **Where V8 is not ECMA-262.** The answers are tested against V8 13.6, and the translator follows ECMA-262 where
  that V8 departs from it. The Script value `Katakana_Or_Hiragana` (`Hrkt`) is accepted, and matches no character,
  since none has it: V8 refuses it. `\p{WSpace}` is refused, since ECMA-262 lists `White_Space` and `space` only:
  V8 takes it. `a{2147483648,2147483647}` is refused, since the first count is the greater: V8 keeps both as one
  number. A backreference that ignores case compares character by character through Canonicalize, where V8 with
  the `i` and `u` flags compares the whole text by full case folding. The scripts Unicode 17 added (`Sidetic`,
  `Tolong_Siki`, `Beria_Erfe`, `Tai_Yo`) are accepted, and V8 13.6 has Unicode 16.
- **Unicode version.** The data is Unicode 17.0.0. The Basic Multilingual Plane part of a General_Category value
  uses .NET's own class, which follows the runtime's Unicode version.
