# Translation Reference

> **Scope.** These translations apply to the DotNet regular expression engine (`--regexEngine DotNet`, in V5 and V4),
> and `JsonSchemaEvaluator.ToDotNetPattern`. See the scope note in
> [EcmaRegexTranslator.md](./EcmaRegexTranslator.md).

What `EcmaRegexTranslator` writes for each construct of an ECMA-262 regular expression, and why.
[EcmaRegexTranslator.md](./EcmaRegexTranslator.md) is the overview.

The translator parses a pattern into a tree and writes the tree. A construct that means the same in .NET is written
in its plain form. The examples below are the exact output for a pattern that is valid with the `u` flag, unless
they say otherwise.

## Table of Contents

- [The Two Grammars](#the-two-grammars)
- [Validity](#validity)
- [Literal Characters and Escapes](#literal-characters-and-escapes)
- [Character Shorthands](#character-shorthands)
- [Dot](#dot)
- [Anchors](#anchors)
- [Word Boundaries](#word-boundaries)
- [Sets and Code Points](#sets-and-code-points)
- [Character Classes](#character-classes)
- [Unicode Property Escapes](#unicode-property-escapes)
- [Modifier Groups](#modifier-groups)
- [Groups](#groups)
- [Backreferences](#backreferences)
- [Repetitions](#repetitions)
- [Lookarounds](#lookarounds)
- [Alternatives](#alternatives)

---

## The Two Grammars

| Grammar | When | A character is | Example of a pattern only it accepts |
|---|---|---|---|
| `u` flag | Tried first. JSON Schema's `pattern` is specified with it. | A code point | `\u{1F600}`, `\p{L}` |
| No flag (Annex B) | When the `u` flag grammar refuses the pattern. | A UTF-16 code unit | `\-`, `\@`, `a{`, `\1` with no group |

Without the flag an identity escape may escape any character, a lone `{`, `}` or `]` is itself, `\1` with no such
group is an octal escape, `\p` is the letter `p`, and a lookahead may be quantified. A character beyond the Basic
Multilingual Plane is then two characters to the dot and to a negated class.

```
ECMAScript:  ^a\-b{$          (valid only without the u flag)
.NET:        ^a-b\{\z
```

## Validity

A pattern neither grammar accepts is refused. `TryTranslate` returns `false` and an error that says what is wrong
and the offset in the pattern. When the two grammars object to different things the error gives both.

| Pattern | Error |
|---|---|
| `a{2,1}` | `Numbers out of order in {} quantifier at offset 6.` |
| `x\b+` | `Nothing to repeat at offset 1.` |
| `(?<n>a)(?<n>b)` | `Duplicate group name 'n' at offset 12.` |
| `[z-a]` | `Range out of order in character class at offset 2.` |
| `(` | `Unterminated group at offset 1.` |
| `\p{Nope}(` | `Invalid property name at offset 3 (read without the u flag: Unterminated group at offset 9).` |

Constructs that only .NET has are not ECMA-262 and are refused: `(?i)`, `(?#comment)`, `(?'name'a)`, `(?>a)`.
`\A`, `\Z` and `\z` are valid only without the `u` flag, where they are the letters `A`, `Z` and `z`.

## Literal Characters and Escapes

A literal is written as the character it names, whatever way the pattern named it.

| ECMAScript | .NET | |
|---|---|---|
| `a`, `\x61`, `\u0061`, `\u{61}` | `a` | A printable ASCII character is itself. |
| `\.`, `\*`, `\(`, `\{`, `\/` | `\.`, `\*`, `\(`, `\{`, `/` | Escaped where .NET needs it. |
| `\t`, `\n`, `\v`, `\f`, `\r` | the same | |
| `\0`, `\cA`, `\cJ` | `\u0000`, `\u0001`, `\n` | A control escape is the character it names. |
| `é`, `\u00E9` | `\u00E9` | A character outside printable ASCII is written by its number. |
| `\u{1F600}`, a literal U+1F600 | `(?:\uD83D\uDE00)` | Both halves in a group, so that a quantifier applies to the character. |
| `\uD83D` (a lone surrogate) | `\uD83D(?![\uDC00-\uDFFF])` | It does not match half of a pair. |

## Character Shorthands

ECMA-262's `\d`, `\w` and `\s` are narrower than .NET's.

| ECMAScript | .NET | Reason |
|---|---|---|
| `\d` | `[0-9]` | .NET's `\d` is every Unicode decimal digit. |
| `\w` | `[a-zA-Z0-9_]` | .NET's `\w` is every Unicode word character. |
| `\s` | `[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]` | ECMA-262 has U+FEFF and not U+0085. |
| `\D`, `\W`, `\S` | The complement, as a [set that holds characters beyond the Basic Multilingual Plane](#sets-and-code-points). | |

Inside a case-insensitive group read with the `u` flag grammar, `\w` also holds U+017F and U+212A, which fold to
`s` and `k`.

## Dot

The dot is every character but the four line terminators `\n`, `\r`, U+2028 and U+2029. .NET's dot leaves out only
`\n`. With the `s` modifier the dot is every character.

```
ECMAScript:  ^.*$
.NET:        ^[^\n\r\u2028\u2029]*\z

ECMAScript:  a.b
.NET:        a(?>[^\n\r\u2028\u2029\uD800-\uDFFF]|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]|(?![\uDC00-\uDFFF]))|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])b
```

The second form is explained under [Sets and Code Points](#sets-and-code-points).

## Anchors

| ECMAScript | .NET | Reason |
|---|---|---|
| `^` | `^` | .NET is not given its multiline option, so `^` is the start of the input. |
| `$` | `\z` | .NET's `$` also holds before a final `\n`. |
| `^` in `(?m:...)` | `(?:^\|(?<=[\n\r\u2028\u2029]))` | A line starts after any of the four line terminators. .NET's multiline `^` knows only `\n`. |
| `$` in `(?m:...)` | `(?:\z\|(?=[\n\r\u2028\u2029]))` | |

## Word Boundaries

A word character is one of the 63 ASCII ones. The positions before the start and after the end of the input count
as not a word character.

```
ECMAScript:  \b
.NET:        (?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))

ECMAScript:  x\B
.NET:        x(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))
```

## Sets and Code Points

With the `u` flag grammar a character is a code point, and .NET matches UTF-16 code units. A set (a class, a class
escape, the dot, a property) is therefore written by what it holds:

| The set holds | Written as |
|---|---|
| Only characters of the Basic Multilingual Plane, no surrogate | One .NET class: `[a-z]` |
| Characters beyond it as well | An atomic alternation of the class and the surrogate pairs: `[a-z\u{1F600}-\u{1F64F}]` is `(?>[a-z]\|\uD83D[\uDE00-\uDE4F])` |
| Every character beyond it and every surrogate (the dot, a negated class, `\D`, `\W`, `\S`) | `(?>[...\uD800-\uDFFF excluded]\|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]\|(?![\uDC00-\uDFFF]))\|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])` |

The last form reads: a code unit of the set that is not a surrogate, or a first half with its second half when
there is one, or a second half with no first half before it. So a surrogate pair is always one character and is
never split. The alternatives start with different code units, which is why the group can be atomic.

A **repetition** of such a set is written so that a text with no character beyond the Basic Multilingual Plane is
matched by a repetition of one class, at the cost it always had.

With **no upper bound**, a set that holds every such character (the dot, a negated class) does not need the long
form, because any run of the set's code units that does not start or end inside a surrogate pair is a run of its
characters:

```
ECMAScript:  ^[^/]+/.*$
.NET:        ^[^/]+/[^\n\r\u2028\u2029]*\z
```

Where what follows the repetition could match inside a pair, the repetition is followed by
`(?!(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF])`, which holds everywhere but between the two halves of a pair. A whole
translation starts with the same assertion when the pattern could otherwise match from inside a pair, because .NET
tries a match at every code unit: `\B` becomes `(?!(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF])(?:...)`.

With no upper bound, a set that holds some surrogate pairs is runs of the class with a pair between one run and the
next:

```
ECMAScript:  ^[a-z\u{1F600}-\u{1F64F}]+$
.NET:        ^(?>[a-z]|\uD83D[\uDE00-\uDE4F])[a-z]*(?:\uD83D[\uDE00-\uDE4F][a-z]*)*\z
```

With **an upper bound**, the dot or a negated class counts characters, and a run of code units is a run of as many
characters only when none is a surrogate. So the translation looks ahead as far as the highest count. When no
surrogate is there it is the class repeated, and otherwise the long form:

```
ECMAScript:  ^.{2,5}$
.NET:        ^(?(?=[^\uD800-\uDFFF]{5}|[^\uD800-\uDFFF]*\z)[^\n\r\u2028\u2029\uD800-\uDFFF]{2,5}|(?>[^\n\r\u2028\u2029\uD800-\uDFFF]|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]|(?![\uDC00-\uDFFF]))|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]){2,5})\z
```

A set with more than two alternatives of surrogate pairs (`\p{Lu}`, `\p{L}`) has them behind a look at the next
code unit, so that a character of the Basic Multilingual Plane that is not in the class fails without trying each:
`(?>\p{Lu}|(?=[\uD800-\uDFFF])(?:\uD801[...]|\uD803[...]|...))`. In a lookbehind, which .NET matches from right to
left, the look is `(?<=[\uD800-\uDFFF])` and stands after the alternatives.

Without the `u` flag none of this applies. Every set is one .NET class of code units.

## Character Classes

A class is the set of its members. Members in the Basic Multilingual Plane are written in the order the pattern
gave them. A class that needs another form is written from its set, in code point order.

| ECMAScript | .NET | |
|---|---|---|
| `[a-zA-Z0-9_]` | `[a-zA-Z0-9_]` | |
| `[\w.+-]` | `[a-zA-Z0-9_.+\-]` | A class escape is its members. |
| `[\b]` | `[\u0008]` | In a class `\b` is backspace. |
| `[--a]` | `[\u002D-a]` | .NET does not take an escaped hyphen as the start of a range. |
| `[^abc]` | `(?>[^abc\uD800-\uDFFF]\|...)` | Every other character, by code point. |
| `[\W]`, `[^\w]` | The same as `\W` | |
| `[^\W]` | `[a-zA-Z0-9_]` | |
| `[^a\D]` | `[0-9]` | The digits but `a`: the class is written from its set. |
| `[\d\D]`, `[^]` | `(?>[^\uD800-\uDFFF]\|...)` | Every character. |
| `[]`, `[^\d\D]` | `(?!\z\|[\s\S])` | No character: an assertion that never holds. |

`[]` is a class with no member and `[^]` is every character. A `]` closes a class wherever it stands, unlike in
.NET, where a `]` first in a class is a member.

`(?!)` is not used for "never", because .NET removes it and what depends on it from the pattern before it runs it,
and its compiled engine then goes wrong on some patterns.

## Unicode Property Escapes

`\p{...}` and `\P{...}` take every property, value and alias ECMA-262 lists: the General_Category values
(`\p{L}`, `\p{Letter}`, `\p{gc=L}`, `\p{General_Category=Letter}`), `Script` and `Script_Extensions` for every
script, and the 53 binary properties. A name ECMA-262 does not define is an error with the `u` flag.

| ECMAScript | .NET | |
|---|---|---|
| `\p{Zs}` | `\p{Zs}` | A General_Category value with no member beyond the Basic Multilingual Plane is .NET's class. |
| `\p{L}` | `(?>\p{L}\|(?=[\uD800-\uDFFF])(?:\uD800[\uDC00-\uDC0B...]\|\uD801[...]\|...))` | .NET's class, then the surrogate pairs of the letters beyond the Basic Multilingual Plane. |
| `\P{Zs}` | `(?>[^\p{Zs}\uD800-\uDFFF]\|...)` | |
| `\p{LC}` | `(?>[\p{Lu}\p{Ll}\p{Lt}]\|...)` | .NET has no `LC`. |
| `\p{Script=Greek}` | `(?>[\u0370-\u0373\u0375-\u0377...]\|\uD800[\uDD40-\uDD8E\uDDA0]\|\uD834[\uDE00-\uDE45])` | The ranges of the script. .NET's `IsGreek` is a block, which is not the script. |
| `\p{ASCII}` | `[\u0000-\u007F]` | |
| `\p{White_Space}` | `[\t-\r \u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]` | The Unicode property, which is not `\s`. |
| `\p{Any}` | Every character | |
| `\p{Cs}` | `(?>[\uD800-\uDBFF](?![\uDC00-\uDFFF])\|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF])` | A surrogate that is not half of a pair. |

.NET's `\p{L}` tests one code unit, so it never matches a letter beyond the Basic Multilingual Plane. That is why
the pairs are written out, and why `\p{L}` is about 4,200 characters long.

## Modifier Groups

`(?i:...)`, `(?m:...)`, `(?s:...)`, combinations such as `(?im:...)`, and negations such as `(?-i:...)` or
`(?s-i:...)` change how their body is read. No .NET option is used for them.

| Modifier | Effect on the translation |
|---|---|
| `s` | The dot is every character. |
| `m` | `^` and `$` are the line assertions shown under [Anchors](#anchors). |
| `i` | A literal or a class becomes the set of the characters ECMA-262's Canonicalize makes equal to it. |

```
ECMAScript:  ^(?i:straße)$
.NET:        ^(?:[Ss\u017F][Tt][Rr][Aa][\u00DF\u1E9E][Ee])\z
```

Canonicalize is Unicode simple case folding with the `u` flag grammar, and the uppercase mapping of one code unit
without it, which never maps a character outside ASCII into ASCII. So `(?i:k)` matches the Kelvin sign U+212A only
with the `u` flag grammar. .NET's own case-insensitive matching follows other tables and the current culture.

## Groups

A group is written without capture, `(?:...)`, unless a backreference reads it. A group that is read is written
with its ECMA-262 number as its name, `(?<1>...)`. A named group is treated the same way, so a name may be any
ECMA-262 identifier and two groups in separate alternatives may share one.

```
ECMAScript:  ^(a|b)(c)$
.NET:        ^(?:a|b)(?:c)\z

ECMAScript:  (?<year>\d{4})-\k<year>
.NET:        (?<1>[0-9]{4})-(?(1)\1)
```

A group that is read and can capture the empty string is written apart: its body where that matches something, in
the group, or else its body where that matches nothing, in no group.

```
ECMAScript:  ^(a|)\1?b$
.NET:        ^(?:(?<1>a))?(?:\1)?b\z

ECMAScript:  ^(^|a)\1?b$
.NET:        ^(?:(?<1>a)|^)(?:\1)?b\z

ECMAScript:  ^(a?b?)\1?c$
.NET:        ^(?:(?<1>ab?|b))?(?:\1)?c\z

ECMAScript:  ^((a|)(a|))\1$
.NET:        ^(?:(?<1>a(?:(?:a)?)))?(?(1)\1)\z
```

The body of the third is one alternative for each term that can be the first to match something: `a` and then
`b?`, or `b`. In the fourth the two terms are the same, and the alternative of the second, `a`, would match one `a`
that the alternative of the first has matched already. It is left out (see [Repetitions](#repetitions)).

ECMA-262 cannot tell a group that captured the empty string from a group that is unset: a backreference to either
matches the empty string, and nothing else reads a group. .NET can tell them apart, and it is the only thing a
translation can ask .NET about a group. So the group is left unset where it would capture nothing, and is then
set exactly when it captured something. A backreference that has to match something, or fail, can then be .NET's
own with no test around it (see [Repetitions](#repetitions)). The group stays whole in three places: in a
lookaround whose captures a backreference reads, where the order of the paths counts and this changes it, where it
holds another group that is read, and where it can only match the empty string (`()`), since nothing would define
it.

## Backreferences

In ECMA-262 a backreference matches what its group captured when the group last took part, and the empty string
when it has not taken part. A quantified term forgets the captures of the groups inside it when each iteration
starts. .NET fails a backreference to a group that has not taken part, and keeps a capture from an earlier
iteration.

| ECMAScript | .NET | |
|---|---|---|
| `(a)\1` | `(?<1>a)(?(1)\1)` | The conditional matches nothing when the group is unset. |
| `^\1(a)` | `^(?(1)\1)(?<1>a)` | A reference before its group matches the empty string. |
| `^(?:(a)\|b)*\1` | `^(?:(?(1)(?<-1>))(?:(?<1>a)\|b))*(?(1)\1)` | Each iteration starts by unsetting group 1. |
| `^(a){0}\1` | `^` | The group never takes part, so the reference is the empty string. |
| `^(a\1)+$` | `^(?:a)+\z` | A reference inside the group it reads is the empty string. The group is not set until it closes, and the repetition unsets it when each iteration starts. |
| `^(?:(a)\|\1b)+$` | `^(?:(?:a)\|b)+\z` | So is a reference to a group in another alternative of the alternation the group is in. One alternative runs at a time. |

`(?(1)(?<-1>))` is a balancing group: it takes the capture of group 1 away when there is one, and .NET gives it
back if the match backtracks out of the iteration.

The last two rows are more than tidiness. With such a reference taken out, a body such as `(|\2|\1\2)` is seen
to match only the empty string, and its repetition is written as the body once (see
[Repetitions](#repetitions)). Run as written, `((|\2|\1\2){3,}|[ax]){4}x$` tries every combination of the three
ways through each iteration, 27 to the power of 4 of them at every place in the text. V8 takes a reference inside
its own group for the empty string too, and answers such a pattern in a few hundred steps.

### A Backreference That Ignores Case

Inside `(?i:...)` a backreference matches a text that is the text of its group character by character, where two
characters are the same when Canonicalize gives one character for both: simple case folding with the `u` flag
grammar, and the uppercase mapping of one code unit without it.

.NET's own case-insensitive backreference is not that comparison. Measured on .NET 10 over every pair of
characters, with the `u` flag grammar it fails 106 pairs of the Basic Multilingual Plane that ECMA-262 matches
(among them `\u03C2` and `\u03C3`, the two small sigmas, `s` and `\u017F`, `\u00B5` and `\u03BC`) and all 614 pairs
beyond it, because it compares code units. Without the `u` flag it differs on 166 pairs, in both directions
(it matches `k` with `\u212A` and `\u00DF` with `\u1E9E`, which ECMA-262 does not). Its table also changes from
one runtime to the next. So it is used only for a text that is ASCII with no `i`, `k` or `s`, the letters that have
a case variant outside ASCII:

| ECMAScript | .NET | |
|---|---|---|
| `(?i:(a)\1)` | `(?:(?<1>[Aa])(?(1)(?i:\1)))` | The group can only capture `a` or `A`. |
| `(?i:(\d+x)\1)` | `(?:(?<1>[0-9]+[Xx])(?(1)(?i:\1)))` | |

For any other group the comparison is written out. For `(?i:(k)\1)`, whose group can capture `k`, `K` or the
Kelvin sign:

```
(?:(?<1>[Kk\u212A])(?(1)
  (?<=\A(?=[\s\S]*?(?=B\1B(?<z1>[\s\S]*))(?<q1>[\s\S]*))[\s\S]*)
  (?>(?:
      (?=(?<u1>[\uD800-\uDBFF][\uDC00-\uDFFF]|[\s\S]))(?=[\s\S]*\z(?<=(?=(?!\k<z1>\z)\k<u1>B(?<q1>[\s\S]*))\k<q1>))\k<u1>
    | (?=[Kk\u212A])(?=[\s\S]*\z(?<=(?=(?!\k<z1>\z)[Kk\u212A](?<q1>[\s\S]*))\k<q1>))[Kk\u212A]
  )*)
  (?=[\s\S]*\z(?<=(?=\k<z1>\z)\k<q1>))
))
```

It is shown on several lines, and `B` stands for `(?!(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF])`, which holds everywhere
but between the two halves of a surrogate pair. It reads:

1. The text of the group cannot be read where it stands in the pattern, but a copy of it stands somewhere in the
   input. The first line finds one from the start of the input, and captures the input from the copy to the end
   as `q1` and the input after the copy as `z1`. A group that holds the input from some place to the end names
   that place.
2. Each turn of the repetition compares one character. It looks at the next character of the input, goes to the
   place `q1` names (to the end of the input, and back over the text of `q1`), tests the character there, moves
   `q1` past it, and takes the character of the input. The test is one of the alternatives: the same character
   (`\k<u1>`), or two members of one class of case variants (`[Kk\u212A]` at both places). A group that can
   capture one of the other ASCII letters gets one more alternative, which uses .NET's own comparison for those
   letters alone.
3. The repetition is atomic, since each turn has one outcome. The last line holds when `q1` has reached `z1`, so
   that every character of the copy has been compared.

In a lookbehind, which is matched from right to left, the same comparison runs forward from a place before the
current one and has to end at the current one.

There is one alternative for each class of case variants that has a member the group can capture, 32 at a time
behind a look at the next character. See [Known Limits](./EcmaRegexTranslator.md#known-limits) for what that costs
when the group can capture any letter.

A group can hold the text of another group through a backreference inside it, and that group can read the first.
So what each group can capture is worked out for all of them together: every set starts empty, and each is worked
out from the others until none grows. `(?i:(?:(\2a)(\1b))+)` has two groups that can each capture `a` and `b` and
nothing else, and both of its backreferences are left to .NET's comparison.

## Repetitions

A quantifier is written as it is, except in the cases below. They concern a body that can match the empty string,
where ECMA-262 is exact and .NET's engines are not: ECMA-262 lets the iterations that must run match nothing, and
refuses an iteration that matched nothing once they are done.

| Case | What is written |
|---|---|
| `X{0}` | Nothing. |
| A body that can only match the empty string (`(?:^)*`, `(?=a){3}`, `\b+` is not valid) | Nothing when no iteration must run, and the body once when one must. |
| A lazy repetition of anything but one code unit (`(?:a\|b)*?`, `(a\|){2,}?`) | The greedy form. Whether a pattern matches does not depend on which path is tried first. .NET's lazy repetition of a group can fail to end, or throw. |
| A body that matches the empty string wherever it stands, with a least count (`(a\|){2,3}`) | The least count becomes 0: `(?:(?:a)?){0,3}`. |
| A body with a group a backreference reads, which can match the empty string, with iterations that need not run (`(?:(a)\|b\|)*\1`) | Those iterations are written apart as a body that cannot match the empty string, or made to fail when they matched nothing where there is no such body (below). |
| A body with a backreference, which can match the empty string, with iterations that need not run (`(a)\1?`, `(?:\1\|b)*`) | The same. |

The case of a body with a group that is read is about the answer. .NET would take an iteration that matched
nothing and leave the repetition with the group unset, where ECMA-262 refuses the iteration and leaves the group as
the last iteration that matched something set it. So the iterations that need not run are given a body that
cannot match the empty string:

```
ECMAScript:  ^(?:(a)|)*\1             .NET:  ^(?:(?(1)(?<-1>))(?<1>a))*(?(1)\1)
ECMAScript:  ^(?:(a)|b|)*\1$          .NET:  ^(?:(?(1)(?<-1>))(?:(?<1>a)|b))*(?(1)\1)\z
ECMAScript:  ^(?:(a)|b|){2,4}\1$      .NET:  ^(?:(?(1)(?<-1>))(?:(?<1>a)|b|)){2}(?:(?(1)(?<-1>))(?:(?<1>a)|b)){0,2}(?(1)\1)\z
ECMAScript:  ^((a|)\2)*$              .NET:  ^(?:(?(2)(?<-2>))(?:(?<2>a)(?(2)\2)|(?:\2)))*\z
```

In the third line the two iterations that must run have the body as it is, empty alternative and all, and the two
that need not have it without. In the fourth the body is `(a|)` and then `\2`: either the group matches `a` and the
backreference is as it was, or the group matches nothing, is left unset, and the backreference has to match
something, which with the group unset it cannot. A group inside the body is still a group in the body that cannot
match the empty string, around what it matches that is not the empty string.

Where there is no such body the iteration is made to fail when it has matched nothing, for `^(?:(?=(a))|x)*\1`:

```
^(?:(?(1)(?<-1>))(?=(?<e1>[\s\S]*))(?:(?=(?<1>a))|x)(?!\k<e1>\z))*(?(1)\1)
```

`(?=(?<e1>[\s\S]*))` captures the rest of the input when an iteration starts, and `(?!\k<e1>\z)` fails the
iteration when the rest of the input is still that text. .NET's interpreted engine reads the rest of the input one
character at a time to capture it, so a repetition with the check takes that engine time in proportion to the
square of the length of the text: a pattern as plain as `^(a|)(?:\1|b)*$` took it 24 seconds on 100,000 characters
when it had the check. The check is written only where there is no body that cannot match the empty string:

- inside a lookaround whose captures a backreference reads, where the order of the paths counts,
- where the body holds a repetition, with two or more iterations that must run, of a group that is read
  (`(?:(?:(a)|){2}c)*\1`): the last of those iterations may match nothing after one that matched something, and
  it leaves the group unset,
- where a group that is read would be left with nothing to define it, because it only ever matches the empty
  string and no iteration must run (`(?:(^)x?)*\1`),
- where the body holds a backreference that ignores case, which may be written as 160,000 characters (see
  [A Backreference That Ignores Case](#a-backreference-that-ignores-case)) and would be written twice,
- and where the body is too large to write this way (256 nodes).

The case of a body with a backreference is about time and not about the answer. A backreference matches nothing
whenever its group is unset, so `\1?` and `(?:\1|b)*` have iterations that match nothing. .NET takes such an
iteration and leaves the repetition, and when what follows fails it tries again without the iteration, from the
same place and with the same groups. ECMA-262 refuses the iteration, and V8 searches once. Each such repetition a
search passes doubles it, and one text of a pattern of the ECMA-262 regular expression test data that V8 answers in
64,000 backtracks took .NET more than eight seconds. So the iterations that need not run are given a body that cannot
match the empty string:

```
ECMAScript:  ^(a)\1?$             .NET:  ^(?<1>a)(?:\1)?\z
ECMAScript:  ^(a)(?:\1|b)*$       .NET:  ^(?<1>a)(?:(?:\1)|b)*\z
ECMAScript:  ^(a)(?:\1?x?)*$      .NET:  ^(?<1>a)(?:(?:\1)x?|x)*\z
ECMAScript:  ^(a)\1{2,5}$         .NET:  ^(?<1>a)(?:(?(1)\1)){2,5}\z
```

- A backreference to a group that cannot capture the empty string matches something exactly when the group is set.
  It is written with no test of the group around it, `(?:\1)`, which is .NET's own backreference: it fails when the
  group is unset. Where the body has to match nothing, a test that the group is unset is written,
  `(?(1)(?!\z|[\s\S]))`.
- Terms one after another match something when one of them is the first to. There is an alternative for each term
  that can be: the terms before it match nothing, it matches something, and the rest are as they were. In the third
  line `\1?x?` is `\1` and then `x?`, or `x`.
- A term has no alternative when it is the same as an earlier term that has one, every term after that one up to
  it matches the empty string wherever it stands, and no backreference reads a group of any of them. Its
  alternative would only match again what the earlier one matches, by a second path: `(a|)(a|)` is `a(?:a)?` and
  not `a(?:a)?|a`, and `(a|)(b|)(a|)` is `a(?:b)?(?:a)?|b(?:a)?`. A path that ends where an earlier one ended, with
  the same captures, fails as that one did, so no answer changes. In a repetition the second path is the base of a
  power: (4/3) to the number of iterations for `(a|)(a|)`. .NET makes the same saving by itself for
  `(?:a)?(?:a)?`, which it counts as `a{0,2}`, and cannot make it for an alternation.
- A repetition matches something when its iterations do.
- A lone backreference with an iteration that must run is left as it was (the fourth line). When that iteration
  matches nothing .NET runs no other, and when it matches something so does every other.

A backreference to a group that can capture the empty string is written the same way where the group is written
apart (see [Groups](#groups)): `^(a|)\1?b$` is `^(?:(?<1>a))?(?:\1)?b\z`, and `^(a|)(?:\1|b)*$` is
`^(?:(?<1>a))?(?:(?:\1)|b)*\z`. Where it is not, or the body is too large to write this way, the iteration is made
to fail when it has matched nothing, with the check above. A repetition whose body
can match the empty string and holds no backreference and no group that one reads (`(?:a?b?)*`) is written as it
is. .NET searches it in the same doubled way (see [Known Limits](./EcmaRegexTranslator.md#known-limits)).

Inside a lookaround whose captures a backreference reads, the order in which paths are tried decides what is
captured. There a lazy repetition stays lazy, every iteration that need not run has the check above, and a lazy
repetition of a group with a highest count is never left to .NET, whose compiled engine can throw on one that
stands inside another repetition. With up to eight iterations left to choice it is written as optional groups one
in another (`(?:ab){0,2}?` is `(?:(?:ab)(?:(?:ab))??)??`). With more, a group counts the iterations:

```
ECMAScript:  (?=((?:ab){0,12}?)c)\1
.NET:        (?#interpreted)(?=(?<1>(?:(?<c1>)){12}(?:(?<-c1>)(?:ab))*?(?:(?(c1)(?<-c1>))){12})c)(?(1)\1)
```

`(?:(?<c1>)){12}` gives the group `c1` twelve captures. Each iteration takes one away with `(?<-c1>)` and cannot
start when none is left, and `(?:(?(c1)(?<-c1>))){12}` takes away what is left after the last.

A translation that keeps a lazy repetition of a group in any of these forms starts with `(?#interpreted)`, a
comment .NET skips: `(?=((?:ab){0,2}?)c)\1` is `(?#interpreted)(?=(?<1>(?:(?:ab)(?:(?:ab))??)??)c)(?(1)\1)`. It
marks the translation for .NET's interpreted engine. The compiled engine does not always return from a match of
such an expression. After a repetition whose iterations hold a lookbehind and can match nothing, it loops on a
lazy repetition of a group in a lookbehind: `(?:x*\B){2}(?<=((?:ab)*?)\1)$` never answers for `-a`, and neither
does a pattern of the ECMA-262 regular expression test data. The interpreted engine answers both. No rewriting
takes a lazy repetition with no highest count out of a pattern, so the engine is chosen instead
(`EcmaRegexTranslator.IsForCompiledEngine`): the evaluator constructs a marked translation without
`RegexOptions.Compiled` whatever its options say, and generated code gives it no `[GeneratedRegex]` form, because
the regular expression generator writes the algorithm of the compiled engine.

## Lookarounds

`(?=...)`, `(?!...)`, `(?<=...)` and `(?<!...)` are written as they are. Both engines match a lookbehind from
right to left, so captures and backreferences inside one behave alike. Inside a lookbehind the translator writes
what must be matched first on the right.

A negative lookaround whose body matches the empty string wherever it stands, such as `(?!)` or `(?<!a*)`, never
holds and is written `(?!\z|[\s\S])`.

What never matches takes what depends on it out of the pattern, before anything is written. Terms one after
another with such a part never match, so `a(?!)b` is `(?!\z|[\s\S])`. An alternative that never matches is no
alternative, so `a(?!)b|c` is `c` and `^(?:x+|[]|){2}$` is `^(?:(?:x+)?){0,2}\z`. A repetition that need not run
of what never matches is nothing, and so is a negative lookaround of it. This is for .NET. An alternative that
never matches beside an empty one would be an optional group around `(?!\z|[\s\S])`, which .NET sees through, and
it then runs `(?:x+|(?:(?!\z|[\s\S]))?){2}` as it runs `(?:x+|){2}`: as `(?:x+){2}` (see
[Alternatives](#alternatives)).

Without the `u` flag a lookahead may be quantified. It is a body that can only match the empty string, so it is
written once or not at all.

## Alternatives

Alternatives are written in order. An empty alternative beside one other is written as that other made optional:
`(?:a+|)` is `(?:(?:a+)?)`, which is what .NET makes of it itself. It is written so because .NET's own rewrite goes
wrong when the other alternative is a repetition with no highest count and a repetition encloses both. It runs
`(?:a+|)+` as `a+`, which does not match the empty string.

.NET also takes for empty an alternative that is written but matches nothing wherever it stands: a group no
backreference reads around nothing (`()`), a lookaround of nothing (`(?=)`), a backreference to a group that never
takes part. The translator reads each of those as the empty alternative it is, so `(?:x+|()){3}` is
`(?:(?:x+)?){0,3}`, and writes none of them anywhere else: `a(?=)b` is `ab`.
