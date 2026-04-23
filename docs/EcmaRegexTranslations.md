# Translation Reference

This document describes every translation performed by `EcmaRegexTranslator` when converting ECMAScript 262 `/u` mode regular expressions to .NET regular expressions.

Where an ECMAScript construct has identical semantics in .NET, it is passed through unchanged. Where semantics differ, the translator emits an equivalent .NET pattern.

## Table of Contents

- [Character Shorthands](#character-shorthands)
- [Dot (`.`)](#dot-)
- [Word Boundaries (`\b`, `\B`)](#word-boundaries-b-b)
- [Unicode Escapes (`\u`, `\u{...}`)](#unicode-escapes-u-u)
- [Literal Non-BMP Characters](#literal-non-bmp-characters)
- [Unicode Property Escapes (`\p{...}`, `\P{...}`)](#unicode-property-escapes-p-p)
- [Backreferences](#backreferences)
- [Character Classes](#character-classes)
- [Identity Escapes](#identity-escapes)
- [Strict `/u` Mode Validation](#strict-u-mode-validation)
- [Pass-Through Constructs](#pass-through-constructs)

---

## Character Shorthands

ECMAScript and .NET define different character sets for `\d`, `\w`, and `\s`. The translator expands these to explicit character classes matching the ECMAScript definitions.

### `\d` and `\D` — Digit Classes

ECMAScript `\d` matches `[0-9]` only. .NET `\d` matches all Unicode digits (e.g. `٣`, `੫`).

| ECMAScript | .NET Translation | Context |
|---|---|---|
| `\d` | `[0-9]` | Standalone |
| `\D` | `[^0-9]` | Standalone |
| `\d` inside `[...]` | `0-9` | Inline (without brackets) |

**Example:**

```
ECMAScript:  \d+
.NET:        [0-9]+
```

### `\w` and `\W` — Word Classes

ECMAScript `\w` matches `[a-zA-Z0-9_]` only. .NET `\w` matches all Unicode word characters.

| ECMAScript | .NET Translation | Context |
|---|---|---|
| `\w` | `[a-zA-Z0-9_]` | Standalone |
| `\W` | `[^a-zA-Z0-9_]` | Standalone |
| `\w` inside `[...]` | `a-zA-Z0-9_` | Inline |

### `\s` and `\S` — Whitespace Classes

ECMAScript and .NET disagree on two specific characters:

| Character | ECMAScript `\s` | .NET `\s` |
|---|---|---|
| `\uFEFF` (BOM) | ✅ Included | ❌ Excluded |
| `\x85` (NEL) | ❌ Excluded | ✅ Included |

The translator emits the full explicit ECMAScript whitespace character class:

| ECMAScript | .NET Translation |
|---|---|
| `\s` | `[\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]` |
| `\S` | `[^\t\n\v\f\r \u00A0\uFEFF\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]` |

The same characters are used inline when `\s` appears inside a character class.

---

## Dot (`.`)

ECMAScript `.` excludes four line terminators. .NET `.` only excludes `\n`.

| ECMAScript | .NET Translation |
|---|---|
| `.` | `[^\n\r\u2028\u2029]` |

This ensures `\r`, `\u2028` (Line Separator), and `\u2029` (Paragraph Separator) are excluded, matching ECMAScript semantics.

---

## Word Boundaries (`\b`, `\B`)

ECMAScript word boundaries are defined in terms of the ASCII `\w` character set `[a-zA-Z0-9_]`. .NET `\b` uses the Unicode-aware `\w`, which would produce different results.

The translator expands word boundaries to explicit lookaround assertions:

| ECMAScript | .NET Translation |
|---|---|
| `\b` | `(?:(?<=[a-zA-Z0-9_])(?![a-zA-Z0-9_])\|(?<![a-zA-Z0-9_])(?=[a-zA-Z0-9_]))` |
| `\B` | `(?:(?<=[a-zA-Z0-9_])(?=[a-zA-Z0-9_])\|(?<![a-zA-Z0-9_])(?![a-zA-Z0-9_]))` |

---

## Unicode Escapes (`\u`, `\u{...}`)

### BMP Escapes

`\uHHHH` is passed through unchanged — both engines support this syntax.

### Extended Unicode Escapes

ECMAScript `/u` mode supports `\u{XXXXX}` for code points above U+FFFF. .NET does not support this syntax.

| Code Point Range | ECMAScript | .NET Translation |
|---|---|---|
| BMP (U+0000–U+FFFF) | `\u{41}` | `\u0041` |
| Supplementary (U+10000+) | `\u{1F600}` | `(?:\uD83D\uDE00)` (surrogate pair in non-capturing group) |

**Example:**

```
ECMAScript:  \u{48}\u{65}\u{6C}\u{6C}\u{6F}
.NET:        \u0048\u0065\u006C\u006C\u006F

ECMAScript:  \u{1F600}
.NET:        (?:\uD83D\uDE00)
```

Supplementary code points are wrapped in a non-capturing group `(?:...)` outside character classes, ensuring any following quantifier applies to the whole code point.

### Supplementary Characters in Character Classes

When character class ranges span supplementary code points, the translator emits surrogate-pair-aware alternation patterns.

**Same high surrogate:**

```
ECMAScript:  [\u{1F600}-\u{1F64F}]
.NET:        (?:\uD83D[\uDE00-\uDE4F])
```

**Different high surrogates (multi-part):**

```
ECMAScript:  [\u{1F600}-\u{1F9FF}]
.NET:        (?:\uD83D[\uDE00-\uDFFF]|[\uD83E-\uD83E][\uDC00-\uDFFF]|\uD83E[\uDC00-\uDDFF])
```

**Mixed BMP and supplementary:**

```
ECMAScript:  [a-z\u{1F600}-\u{1F64F}]
.NET:        (?:[a-z]|\uD83D[\uDE00-\uDE4F])
```

**Negated supplementary classes** use a negative lookahead:

```
ECMAScript:  [^\u{1F600}-\u{1F64F}]
.NET:        (?!\uD83D[\uDE00-\uDE4F])(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])
```

---

## Literal Non-BMP Characters

ECMAScript patterns can contain literal non-BMP characters (e.g. `🐲`, U+1F432). In UTF-16, these are stored as surrogate pairs — two `char` values. Without special handling, a quantifier like `*` after a literal emoji would apply only to the low surrogate, producing incorrect results.

The translator detects literal surrogate pairs in the pattern and wraps them in a non-capturing group with explicit escape sequences, ensuring quantifiers apply to the whole code point.

### Outside Character Classes

| ECMAScript | .NET Translation | Notes |
|---|---|---|
| `🐲` | `(?:\uD83D\uDC32)` | Surrogate pair grouped |
| `🐲*` | `(?:\uD83D\uDC32)*` | Quantifier applies to whole code point |
| `🐲+` | `(?:\uD83D\uDC32)+` | Same for `+` |
| `🐲{2}` | `(?:\uD83D\uDC32){2}` | Same for `{n}` |

**Example:**

```
ECMAScript:  ^🐲*$
.NET:        ^(?:\uD83D\uDC32)*$

ECMAScript:  🐲🐉
.NET:        (?:\uD83D\uDC32)(?:\uD83D\uDC09)
```

### Inside Character Classes

Literal surrogate pairs inside character classes are handled using the same surrogate-pair-aware alternation approach used for `\u{XXXXX}` escapes:

```
ECMAScript:  [🐲]
.NET:        (?:\uD83D\uDC32)

ECMAScript:  [a🐲z]
.NET:        (?:[az]|\uD83D\uDC32)
```

---

## Unicode Property Escapes (`\p{...}`, `\P{...}`)

### General Category — Long Names

Long Unicode General Category names are mapped to their short equivalents, which .NET supports natively.

| ECMAScript | .NET Translation |
|---|---|
| `\p{Letter}` | `\p{L}` |
| `\p{Uppercase_Letter}` | `\p{Lu}` |
| `\p{Lowercase_Letter}` | `\p{Ll}` |
| `\p{Titlecase_Letter}` | `\p{Lt}` |
| `\p{Cased_Letter}` | `\p{LC}` |
| `\p{Modifier_Letter}` | `\p{Lm}` |
| `\p{Other_Letter}` | `\p{Lo}` |
| `\p{Mark}` | `\p{M}` |
| `\p{Nonspacing_Mark}` | `\p{Mn}` |
| `\p{Spacing_Mark}` | `\p{Mc}` |
| `\p{Enclosing_Mark}` | `\p{Me}` |
| `\p{Number}` | `\p{N}` |
| `\p{Decimal_Number}` | `\p{Nd}` |
| `\p{Letter_Number}` | `\p{Nl}` |
| `\p{Other_Number}` | `\p{No}` |
| `\p{Punctuation}` | `\p{P}` |
| `\p{Connector_Punctuation}` | `\p{Pc}` |
| `\p{Dash_Punctuation}` | `\p{Pd}` |
| `\p{Open_Punctuation}` | `\p{Ps}` |
| `\p{Close_Punctuation}` | `\p{Pe}` |
| `\p{Initial_Punctuation}` | `\p{Pi}` |
| `\p{Final_Punctuation}` | `\p{Pf}` |
| `\p{Other_Punctuation}` | `\p{Po}` |
| `\p{Symbol}` | `\p{S}` |
| `\p{Math_Symbol}` | `\p{Sm}` |
| `\p{Currency_Symbol}` | `\p{Sc}` |
| `\p{Modifier_Symbol}` | `\p{Sk}` |
| `\p{Other_Symbol}` | `\p{So}` |
| `\p{Separator}` | `\p{Z}` |
| `\p{Space_Separator}` | `\p{Zs}` |
| `\p{Line_Separator}` | `\p{Zl}` |
| `\p{Paragraph_Separator}` | `\p{Zp}` |
| `\p{Other}` | `\p{C}` |
| `\p{Control}` | `\p{Cc}` |
| `\p{Format}` | `\p{Cf}` |
| `\p{Surrogate}` | `\p{Cs}` |
| `\p{Private_Use}` | `\p{Co}` |
| `\p{Unassigned}` | `\p{Cn}` |

### General Category — Unicode Aliases

The Unicode `PropertyValueAliases.txt` file defines additional aliases for some General Category values beyond the short and long names. These are valid in ECMAScript `\p{...}` escapes and are mapped to their .NET short equivalents.

| ECMAScript | .NET Translation | Alias for |
|---|---|---|
| `\p{digit}` | `\p{Nd}` | `Decimal_Number` |
| `\p{cntrl}` | `\p{Cc}` | `Control` |
| `\p{punct}` | `\p{P}` | `Punctuation` |
| `\p{Combining_Mark}` | `\p{M}` | `Mark` |

**Example:**

```
ECMAScript:  ^\p{digit}+$
.NET:        ^\p{Nd}+$
```

Short category names (`L`, `Lu`, `Nd`, etc.) and the `General_Category=`/`gc=` prefix are passed through after stripping the prefix:

```
ECMAScript:  \p{gc=Lu}       →  .NET: \p{Lu}
ECMAScript:  \p{General_Category=Letter}  →  .NET: \p{L}
```

### General Category — Negated

`\P{...}` is translated to `\P{...}` in .NET (both engines support negation natively for categories).

### Script Properties

Script properties are mapped to .NET Unicode block names where they align. The `Script=`, `sc=`, `Script_Extensions=`, and `scx=` prefixes are all supported.

| ECMAScript | .NET Translation |
|---|---|
| `\p{Script=Greek}` / `\p{sc=Grek}` | `\p{IsGreek}` |
| `\p{Script=Cyrillic}` / `\p{sc=Cyrl}` | `\p{IsCyrillic}` |
| `\p{Script=Armenian}` / `\p{sc=Armn}` | `\p{IsArmenian}` |
| `\p{Script=Hebrew}` / `\p{sc=Hebr}` | `\p{IsHebrew}` |
| `\p{Script=Arabic}` / `\p{sc=Arab}` | `\p{IsArabic}` |
| `\p{Script=Thai}` | `\p{IsThai}` |
| `\p{Script=Georgian}` / `\p{sc=Geor}` | `\p{IsGeorgian}` |
| `\p{Script=Devanagari}` / `\p{sc=Deva}` | `\p{IsDevanagari}` |
| `\p{Script=Bengali}` / `\p{sc=Beng}` | `\p{IsBengali}` |
| `\p{Script=Gujarati}` / `\p{sc=Gujr}` | `\p{IsGujarati}` |
| `\p{Script=Tamil}` / `\p{sc=Taml}` | `\p{IsTamil}` |
| `\p{Script=Telugu}` / `\p{sc=Telu}` | `\p{IsTelugu}` |
| `\p{Script=Kannada}` / `\p{sc=Knda}` | `\p{IsKannada}` |
| `\p{Script=Malayalam}` / `\p{sc=Mlym}` | `\p{IsMalayalam}` |
| `\p{Script=Sinhala}` / `\p{sc=Sinh}` | `\p{IsSinhala}` |
| `\p{Script=Myanmar}` / `\p{sc=Mymr}` | `\p{IsMyanmar}` |
| `\p{Script=Ethiopic}` / `\p{sc=Ethi}` | `\p{IsEthiopic}` |
| `\p{Script=Khmer}` / `\p{sc=Khmr}` | `\p{IsKhmer}` |
| `\p{Script=Mongolian}` / `\p{sc=Mong}` | `\p{IsMongolian}` |
| `\p{Script=Lao}` / `\p{sc=Laoo}` | `\p{IsLao}` |
| `\p{Script=Hangul}` / `\p{sc=Hang}` | `\p{IsHangulJamo}` |
| `\p{Script=Hiragana}` / `\p{sc=Hira}` | `\p{IsHiragana}` |
| `\p{Script=Katakana}` / `\p{sc=Kana}` | `\p{IsKatakana}` |
| `\p{Script=Tibetan}` / `\p{sc=Tibt}` | `\p{IsTibetan}` |

**Script=Latin** is a special case. .NET does not have a `\p{IsLatin}` property — the Latin script spans multiple Unicode blocks. The translator emits an explicit alternation of the exact Unicode 16.0 Latin script code point ranges (34 BMP ranges + 5 supplementary ranges via surrogate pairs):

```
ECMAScript:  \p{Script=Latin}
.NET:        (?:[\u0041-\u005A\u0061-\u007A\u00AA\u00BA...\uFF41-\uFF5A]
              |\uD801[\uDF80-\uDF85]|\uD801[\uDF87-\uDFB0]
              |\uD801[\uDFB2-\uDFBA]|\uD837[\uDF00-\uDF1E]
              |\uD837[\uDF25-\uDF2A])
```

Negated script properties (`\P{Script=Latin}`) use a negative lookahead followed by a match for any Unicode code point.

### Binary Properties

ECMAScript `/u` mode supports binary Unicode properties. These are expanded to equivalent .NET character classes or alternation patterns.

| ECMAScript | .NET Translation | Notes |
|---|---|---|
| `\p{ASCII}` | `[\u0000-\u007F]` | U+0000–U+007F |
| `\p{Alphabetic}` | `[\p{L}\p{M}]` | Close approximation (Letter + Mark) |
| `\p{White_Space}` | `[<whitespace chars>]` | Full ECMAScript whitespace set |
| `\p{Emoji}` | `(?:[<BMP emoji>]\|<surrogate ranges>)` | Alternation of BMP class + supplementary surrogate pairs |
| `\p{Any}` | `[\s\S]` | Matches any character |
| `\p{Assigned}` | `[\P{Cn}]` | Everything except Unassigned |
| `\p{Hex_Digit}` | `[0-9A-Fa-f\uFF10-\uFF19\uFF21-\uFF26\uFF41-\uFF46]` | ASCII + fullwidth hex digits |
| `\p{Lowercase}` | `(?:[\p{Ll}<Other_Lowercase>]\|<supp>)` | `\p{Ll}` + 28 BMP + 5 supplementary Other_Lowercase ranges |
| `\p{Uppercase}` | `(?:[\p{Lu}<Other_Uppercase>]\|<supp>)` | `\p{Lu}` + 2 BMP + 3 supplementary Other_Uppercase ranges |
| `\p{ID_Start}` | `[\p{L}\p{Nl}\u1885-\u1886\u2118\u212E\u309B-\u309C]` | `[\p{L}\p{Nl}]` + 4 extra code points |
| `\p{Emoji_Presentation}` | `(?:[<BMP>]\|<supp>)` | 33 BMP + 47 supplementary ranges |
| `\p{Extended_Pictographic}` | `(?:[<BMP>]\|<supp>)` | 50 BMP + 28 supplementary ranges |

Negated binary properties (`\P{ASCII}`, `\P{Emoji}`, etc.) use the appropriate negation mechanism — either `[^...]` for class-based properties or `(?!...)` negative lookahead for alternation-pattern properties.

**Binary properties inside character classes:**

BMP-only binary properties (ASCII, Alphabetic, White_Space, Any, Assigned, Hex_Digit, ID_Start) are inlined directly. Alternation-pattern properties (Emoji, Lowercase, Uppercase, Emoji_Presentation, Extended_Pictographic) cause the character class to be emitted as an alternation group:

```
ECMAScript:  [\p{Emoji}a-z]
.NET:        (?:[a-z]|(?:[\u0023\u002A...BMP emoji...]|\uD83C[\uDC00-\uDFFF]|...))
```

---

## Backreferences

### Numbered Backreferences

ECMAScript treats backreferences to non-participating or forward-declared capture groups as matching the empty string. .NET fails the match in this case.

The translator wraps every backreference in a .NET conditional:

| ECMAScript | .NET Translation | Semantics |
|---|---|---|
| `\1` | `(?(1)\1)` | If group 1 captured, match its value; else match empty |
| `\12` | `(?(12)\12)` | Same for multi-digit refs |

### Named Backreferences

| ECMAScript | .NET Translation |
|---|---|
| `\k<name>` | `(?(name)\k<name>)` |

This `(?(N)\N)` conditional is a non-capturing construct — it does not affect group numbering.

**Example:**

```
ECMAScript:  (a)?\1
.NET:        (a)?(?(1)\1)

Matching "" against (a)?\1:
  ECMAScript: ✅ matches (group 1 non-participating → \1 matches empty)
  .NET:       ✅ matches (conditional: group 1 not captured → match empty)
```

### Backreference vs. Octal Ambiguity

In ECMAScript, `\1`–`\9` are always backreferences if there is a corresponding capture group. The translator counts capture groups and uses `(?(N)\N)` for all backreferences.

`\0` (null character) is supported and translated as-is, but `\0` followed by a digit (e.g. `\00`) is rejected as `InvalidData` in `/u` mode.

---

## Character Classes

### Simple Classes

Simple character classes without negated shorthands or supplementary code points are passed through with content translation:

```
ECMAScript:  [a-z\d]
.NET:        [a-z0-9]
```

### Negated Shorthands in Classes

ECMAScript allows negated shorthands like `\D` inside character classes: `[\d\D]`. This cannot be represented directly in a .NET `[...]` class.

**Non-negated class with negated shorthands** — Uses alternation:

```
ECMAScript:  [a-z\D]
.NET:        (?:[a-z]|[^0-9])
```

**Negated class with negated shorthands** — Uses .NET character class subtraction:

```
ECMAScript:  [^a-z\D]
.NET:        [^0-9-[a-z]]
```

### Empty and Match-Any Classes

| ECMAScript | .NET Translation | Meaning |
|---|---|---|
| `[]` | `[^\s\S]` | Matches nothing (empty class) |
| `[^]` | `[\s\S]` | Matches any character |

---

## Identity Escapes

In ECMAScript `/u` mode, only specific characters may be escaped with a backslash. The translator enforces these restrictions and passes valid identity escapes through.

### Outside Character Classes

Valid identity escapes: `^`, `$`, `.`, `*`, `+`, `?`, `(`, `)`, `[`, `]`, `{`, `}`, `|`, `\`, `/`

These are the ECMAScript *SyntaxCharacter* set plus `/`.

### Inside Character Classes

Valid identity escapes: all of the above plus `-`.

### Control Escapes

`\t`, `\n`, `\r`, `\f`, `\v` are passed through in all contexts.

### Control Letter Escapes

`\cA`–`\cZ` and `\ca`–`\cz` are passed through. An invalid letter after `\c` returns `InvalidData`.

### Hex Escapes

`\xHH` is passed through unchanged.

---

## Strict `/u` Mode Validation

The following patterns are **rejected as `InvalidData`** in `/u` mode, matching ECMAScript strict behaviour:

| Pattern | Reason |
|---|---|
| `\a` | Not a valid escape in `/u` mode (would be BEL in .NET — silent mismatch) |
| `\e` | Not a valid escape in `/u` mode (would be ESC in .NET) |
| `\B` inside `[...]` | Word boundary assertion not valid inside character class |
| `\1`–`\9` inside `[...]` | Backreferences not valid inside character class |
| `\0` followed by a digit | Octal escapes not permitted in `/u` mode |
| `\p{letter}` | Wrong case — property names are case-sensitive |
| `\p{}`, `\p{=}` | Malformed property name |
| `\p{Script=Klingon}` | Unknown script name |
| `\-` outside `[...]` | Dash is only a valid identity escape inside character classes |

---

## Pass-Through Constructs

The following ECMAScript constructs have identical semantics in .NET and are passed through without modification:

| Construct | Description |
|---|---|
| Literal characters | `a`, `Z`, `0`, etc. |
| `^`, `$` | Anchors |
| `*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}` | Quantifiers |
| `*?`, `+?`, `??` | Non-greedy quantifiers |
| `(...)` | Capturing groups |
| `(?:...)` | Non-capturing groups |
| `(?=...)`, `(?!...)` | Lookahead assertions |
| `(?<=...)`, `(?<!...)` | Lookbehind assertions |
| `(?<name>...)` | Named capturing groups |
| `\|` | Alternation |
| `\uHHHH` | BMP Unicode escape |
| `\xHH` | Hex escape |
| `\t`, `\n`, `\r`, `\f`, `\v` | Control escapes |
| `\cX` | Control letter escapes |
| `\p{L}`, `\p{Lu}`, etc. | Short category names |
