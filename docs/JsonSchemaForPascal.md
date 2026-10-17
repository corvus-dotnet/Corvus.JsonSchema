# JSON Schema for Object Pascal

[corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-pas/corvus-json-schema) is the
Corvus JSON Schema evaluator for Object Pascal: draft 4, 6, 7, 2019-09 and 2020-12. It is a port of the .NET runtime
evaluator (see [Runtime Evaluator](RuntimeEvaluator.md)) by way of the [Go module](JsonSchemaForGo.md), and gives the
same results and annotations. It has no dependencies beyond the compiler's run-time library.

Free Pascal 3.2.2 or later is the compiler it is built and tested with. The source is in the Delphi dialect and is
written to be Delphi-compatible, but it has not been compiled with Delphi.

A schema is compiled once into a node graph and fail-fast plans: each plan holds only the checks its subschema needs,
and an object's keywords are fused into one pass over its properties. Any number of instances can then be validated
against it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, and all of its annotation tests.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations, the same as every other Corvus
  implementation.
- **Allocation-free validation.** In the steady state, validating a parsed document, or JSON text, allocates nothing.
  The exceptions are an asserted `regex`, `idn-hostname` or `idn-email` format, a `hostname` with an `xn--` label,
  and a `multipleOf` whose divisor has more than 18 significant digits.
- **Every array read is checked.** An index out of range raises `ERangeError` and never reads other memory. On the
  paths every validation takes, the check is one inline comparison written out in the source.

## Install

A release is a source archive on the repository's
[GitHub releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=pas-v), tagged `pas-v<version>`. The
archive unpacks to `corvus-json-schema-<version>`. Give the compiler its `src` directory for units and include files:

```sh
fpc -Fucorvus-json-schema-0.1.1/src -Ficorvus-json-schema-0.1.1/src yourprogram.pas
```

The one unit a program names in its `uses` clause is `Corvus.JsonSchema`. The samples below are written for the
Delphi mode of Free Pascal (`{$MODE DELPHI}`).

## Validate

```pascal
var
  Validator: IJsonSchemaValidator;
begin
  Validator := CompileJsonSchema('{'
    + '"type": "object",'
    + '"properties": {"id": {"type": "integer", "minimum": 1}},'
    + '"required": ["id"]'
    + '}');
  WriteLn(Validator.IsValid('{"id": 3}'));  // TRUE
  WriteLn(Validator.IsValid('{"id": 0}'));  // FALSE
end;
```

A validator is an interface, so nothing frees it. It is not modified by validation and is safe to use from several
threads at once. A schema that is not JSON raises `EJsonParseError`, and one that cannot be compiled (an unresolvable
reference, an invalid pattern) raises `EJsonSchemaCompileError`. A schema with a pattern over a limit of size raises
`EJsonSchemaPatternTooLarge`, and a validation in which a match of a pattern reached its limit raises
`EJsonSchemaPatternMatchLimit` (see [Limits](#limits)).

## JSON text

`IsValid` takes JSON text as a `UTF8String` or as a range of a `TBytes`. It parses the text into buffers the thread
reuses and validates it in place, so in the steady state it allocates nothing. To validate the same text more than
once, parse it into a `TJsonDocument`: the UTF-8 text and one flat array of values, with strings read in place where
they have no escapes. A document is a value that needs no `Free`.

```pascal
var
  Validator: IJsonSchemaValidator;
  Document: TJsonDocument;
  Text: TBytes;
begin
  Validator := CompileJsonSchema('{"type": "array", "items": {"type": "integer"}}');

  // Parse once, validate any number of times.
  Document := ParseJson('[1, 2, 3]');
  WriteLn(Validator.IsValid(Document));  // TRUE

  // JSON text is parsed into buffers the thread reuses.
  WriteLn(Validator.IsValid('[1, "two"]'));  // FALSE
  Text := BytesOf('[4, 5]');
  WriteLn(Validator.IsValid(Text, 0, Length(Text)));  // TRUE
end;
```

`IsValid` raises `EJsonParseError` for text that is not JSON. `TryIsValid` raises nothing. It returns `False` with the
reason, which is empty when the instance was evaluated and is simply not valid.

```pascal
var
  Validator: IJsonSchemaValidator;
  Error: UTF8String;
begin
  Validator := CompileJsonSchema('{"type": "object"}');

  // TryIsValid raises nothing. Error is the reason the text could not be evaluated.
  WriteLn(Validator.TryIsValid('{"id": 3', Error));  // FALSE
  WriteLn(Error);  // invalid JSON at offset 8: unexpected end of input

  // IsValid raises EJsonParseError for text that is not JSON.
  try
    Validator.IsValid('{"id": 3');
  except
    on E: EJsonParseError do
      WriteLn(E.Offset, ': ', E.Utf8Message);  // 8: invalid JSON at offset 8: unexpected end of input
  end;
end;
```

## Options

The compile functions take a `TJsonSchemaOptions` record after the schema. `DefaultJsonSchemaOptions` gives the
defaults, and these procedures change them:

| Procedure | Meaning |
|---|---|
| `WithDefaultDialect` | The dialect of a schema without `$schema` (default `Draft202012`). |
| `WithAssertFormat` | `True` asserts `format`, `False` never does. Without it the schema's vocabularies decide. |
| `WithAssertFormatInLegacyDrafts` | Without `WithAssertFormat`, assert `format` in drafts 4 to 7 too. |
| `WithAssertContent` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default `True`). |
| `WithFormat` | A custom format: a function from the bytes of the string (or of a number's JSON text) to whether it is valid. |
| `WithDocumentResolver` | A function from an absolute URI to the document, for remote references, and the context it is called with. The standard metaschemas are built in. |
| `WithBaseURI` | The base URI of the root document. |
| `WithEntryPoint` | A subschema to validate against, such as `#/$defs/item`. |
| `WithMaxDepth` | The deepest the evaluator recurses in place (default 128). |
| `WithPatternMatchLimit` | The milliseconds one match of a pattern against one string may take (default 1000). `NoPatternMatchLimit` turns the limit off. See [Limits](#limits). |

A custom format and a document resolver are plain functions.

```pascal
// A custom format. The value is the bytes Text[Start .. Start+Len-1]. It must not raise an exception.
function EvenLength(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (Start + Len <= Length(Text)) and (Len mod 2 = 0);
end;

// A document resolver. Context is the pointer given to WithDocumentResolver.
function ResolveItem(Context: Pointer; const Uri: UTF8String; out Doc: TJsonDocument): Boolean;
begin
  Result := Uri = 'https://example.com/item.json';
  if Result then
    Doc := TJsonDocument(Context^)
  else
    Doc := Default(TJsonDocument);
end;

procedure ExampleOptions;
var
  Item: TJsonDocument;
  Options: TJsonSchemaOptions;
  Validator: IJsonSchemaValidator;
begin
  Item := ParseJson('{"type": "string", "format": "even-length"}');

  Options := DefaultJsonSchemaOptions;
  WithDefaultDialect(Options, Draft201909);  // for schemas without $schema (default Draft202012)
  WithAssertFormat(Options, True);           // True or False. Without it the vocabularies decide
  WithFormat(Options, 'even-length', EvenLength);
  WithDocumentResolver(Options, ResolveItem, @Item);
  WithBaseURI(Options, 'https://example.com/root.json');
  WithEntryPoint(Options, '#/$defs/item');
  WithMaxDepth(Options, 128);

  Validator := CompileJsonSchema('{"$defs": {"item": {"$ref": "item.json"}}}', Options);
  WriteLn(Validator.IsValid('"four"'));   // TRUE
  WriteLn(Validator.IsValid('"three"'));  // FALSE
end;
```

## Results and annotations

```pascal
var
  Validator: IJsonSchemaValidator;
  Instance: TJsonDocument;
  Results: TJsonSchemaResults;
  Rows: TJsonSchemaResultArray;
  I: Int32;
begin
  Validator := CompileJsonSchema('{"properties": {"id": {"type": "integer"}}, "required": ["name"]}');
  Instance := ParseJson('{"id": "seven"}');
  Results := NewJsonSchemaResults(Detailed);
  WriteLn(Validator.Evaluate(Instance, Results));  // FALSE

  Rows := ResultRows(Results);
  for I := 0 to Length(Rows) - 1 do
    if (Rows[I].EvaluationLocation <> '') and (Rows[I].Message <> '') then
      WriteLn(Rows[I].EvaluationLocation, ' at "', Rows[I].DocumentEvaluationLocation, '": ', Rows[I].Message);
  // /properties/id at "/id": The value was expected to match the subschema.
  // /properties/id/type at "/id": The value was expected to be of type 'integer'
  // /required at "/name": Required property not present 'name'
end;
```

Each row is a `TJsonSchemaResult` with `IsMatch`, `Message`, `EvaluationLocation`, `SchemaEvaluationLocation` and
`DocumentEvaluationLocation`. `Basic` records the failures without messages, `Detailed` adds the messages, and
`Verbose` records every keyword, passing ones and annotations included.

```pascal
var
  Validator: IJsonSchemaValidator;
  Results: TJsonSchemaResults;
  Found: TJsonSchemaCollectedAnnotationArray;
  Value: UTF8String;
begin
  Validator := CompileJsonSchema('{'
    + '"title": "Person",'
    + '"properties": {"name": {"title": "Name", "type": "string"}}'
    + '}');
  Results := NewJsonSchemaResults(Verbose);
  WriteLn(Validator.Evaluate('{"name": "Ada"}', Results));  // TRUE

  // Instance location, then keyword, then schema location, then the value as JSON text.
  Found := CollectedAnnotations(Results);
  if FindAnnotation(Found, '', 'title', '#', Value) then
    WriteLn(Value);  // "Person"
  if FindAnnotation(Found, '/name', 'title', '#/properties/name', Value) then
    WriteLn(Value);  // "Name"
end;
```

## Threads

A validator is shared between threads without a lock on the validation path. Each thread keeps the buffers its
validations need, and neither Free Pascal nor Delphi frees a thread variable of a managed type when its thread ends.
A thread that has validated should therefore call `JsonSchemaReleaseThreadScratch` before it ends. A thread that
does not loses that memory once, and nothing else goes wrong.

```pascal
type
  TValidatingThread = class(TThread)
  public
    Validator: IJsonSchemaValidator;
    Valid: Boolean;
  protected
    procedure Execute; override;
  end;

procedure TValidatingThread.Execute;
begin
  Valid := Validator.IsValid('{"id": 3}');
  // The thread keeps the buffers its validations used. Free them before it ends.
  JsonSchemaReleaseThreadScratch;
end;
```

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics. The common shapes of pattern are matched with no regular
expression engine. Any other pattern runs on the package's own engine, which reads every character class from the
package's own Unicode 17 data, so a pattern means the same whichever compiler built the program. The engine agrees
with answers recorded from V8 on 61,694 patterns and 1,091,280 match answers.

A pattern is read with the grammar of the `u` flag and matched by Unicode code point. A pattern that grammar refuses
is read with the Annex B grammar of a pattern with no flag, which many schemas need (`\-` outside a class, `\_`, a
lone `{`). Such a pattern is matched by UTF-16 code unit, as ECMA-262 matches a pattern with no `u` flag. The two
differ only for a string with a character beyond the Basic Multilingual Plane, which is two code units. `^.$`
matches `"\ud801\udc00"` (the one character U+10400), and `^\-?.$` does not, because the dot takes one unit of the
two. A pattern that neither grammar accepts is an `EJsonSchemaCompileError`.

The answers are V8's except where V8 itself departs from ECMA-262, and there the package follows ECMA-262. A
quantifier whose first count is greater than its second is an error whatever the size of the numbers, so
`a{2147483648,2147483647}` is refused, which V8 takes. A group name used twice is an error unless the two groups
are in separate alternatives of one disjunction, so `(?<m>a)(?:x|(?<m>b))` is refused, which V8 takes.
`\p{WSpace}` is not a property name ECMA-262 lists, so the pattern is read with no flag, as the characters
`p{WSpace}`, where V8 reads it as `\p{White_Space}`. `Katakana_Or_Hiragana` (`Hrkt`) is a Script value that no
character has, so `\p{sc=Hrkt}` is valid and matches nothing, where V8 refuses it. And the properties are those of
Unicode 17, so the scripts Sidetic, Tolong_Siki, Beria_Erfe and Tai_Yo are known, which V8 13.6 does not know.

A JSON string can name half of a surrogate pair with an escape, as in `"\ud800"`. That is not Unicode text, and a
`TJsonDocument` is UTF-8 and cannot hold it. Parsing such text raises `EJsonParseError`, and `TryIsValid` returns
`False` with that reason, whatever the schema. So a pattern is never matched against such a string.

A pattern with no lookaround, backreference, multiline anchor or word boundary gets an automaton that decides an
ASCII text with one table read per byte, when the automaton is small enough. Every other pattern, and a text that
is not ASCII, runs on a backtracking matcher. The Go module hands most patterns to Go's linear-time `regexp`
package, which Object Pascal has no counterpart of. A pattern with nested quantifiers can take exponential time on
a text it does not match, in cases where the Go module's would not, so a match has a limit on its time and on its
memory (see [Limits](#limits)). A repetition whose body cannot consume a character, such as `(?:\b|(?=a)|){4,5}`,
is not such a case. Every iteration of it is empty, so the matcher runs the body once when the minimum is one or
more and not at all when it is zero, which is what ECMA-262 says such a repetition matches. And a counted
repetition is not tried at all at a place where the rest of the pattern needs more of the string than is left,
counting the iterations the repetition has still to make.

## Limits

A schema may come from outside, and so may an instance. Neither is to make a validation run without end, hold
memory out of proportion to what it was given, or take long to compile.

**One match of a pattern against one string may take one second.** `WithPatternMatchLimit` sets another limit, in
milliseconds, and `NoPatternMatchLimit` turns it off, for a program that trusts both its schemas and its instances.
A match that reaches the limit is abandoned and the validation ends with an error. It is never reported as an
instance that is valid, or as one that is not.

| Entry point | A match that reached its limit |
|---|---|
| `IsValid` | Raises `EJsonSchemaPatternMatchLimit`, with the `Pattern`, the `Limit` in milliseconds, and `Memory`. |
| `TryIsValid` | Returns `False`, with the message of that exception in `Error`. It starts with `A match of the regular expression`. |
| `Evaluate` | Raises `EJsonSchemaPatternMatchLimit`. What the collector holds is then not a report of the instance. |
| `TryEvaluate` | Returns `False`, with the message in `Error`. |
| `CompileJsonSchema`, `CompileJsonSchemaFromUri` | Raise it when the match was of a `patternProperties` pattern against a property name the schema declares. |

The backtracking matcher counts its steps and reads a clock that never goes back every 4,096 of them, which is some
tens of microseconds, so a match is abandoned within a millisecond or two of its limit. With a limit, the
backtracking of one match may also hold 1,048,576 entries (16 MB) and no more, and a match that needs more is
abandoned with the same exception, with `Memory` set. The limit is for each match, so a validation that makes many
matches takes the sum of them. The matchers that are not the backtracking one read the string a bounded number of
times and have no limit. With the limit off, a match has no bound on its time or on its memory.

**A pattern has three limits of size**, and compiling a schema with a pattern over one raises
`EJsonSchemaPatternTooLarge`, with the `Pattern`, the `Keyword`, the name of the `Limit` and its `Value`.

| Limit | Value |
|---|---|
| `length` | 1,048,576 bytes of UTF-8. |
| `nesting` | Groups 1,000 deep. |
| `size` | 1,048,576 bytes as the parser counts them: 64 for each term, 32 for each further alternative, 8 for each range of a class and of the set that case folding or `\P{...}` builds, 8 for each alternative around a named group and each group of the same name, and 1 for each character with a case variant in a folded class. |

A pattern of 16,384 literal characters is at the limit of size. The largest pattern of each of 25 kinds compiled in
1 to 96 milliseconds and kept 3.5 MB at the most, on a machine under load. `EJsonSchemaPatternTooLarge` is not
`EJsonSchemaCompileError`, the error of a pattern that is not valid: a pattern is not read past a limit, so nothing
is known of whether it is valid. The `regex` format reads a string of the instance with the same limits, and a
string over one is not a valid regular expression to it. The limits and the size are those of the Go module.

The other checks of the `format` keyword are not under the limit of a match, and each takes time proportional to
the string. `idn-hostname`, and the domain of an `idn-email`, read nothing of a string over 1,012 bytes, since an
internationalized host name has 253 characters at most and its check takes time that grows with the square of a
label.

```pascal
procedure ExampleLimits;
var
  Options: TJsonSchemaOptions;
  Validator: IJsonSchemaValidator;
  Instance, Error: UTF8String;
begin
  // One match of a pattern against one string may take 100 milliseconds. The default is 1000.
  Options := DefaultJsonSchemaOptions;
  WithPatternMatchLimit(Options, 100);
  Validator := CompileJsonSchema('{"pattern": "^(?:(a+)\\1*)+$"}', Options);

  // A pattern that reads a group back inside a repetition, and a string it never answers for.
  Instance := '"' + StringOfChar('a', 60) + 'b"';
  try
    Validator.IsValid(Instance);
  except
    on E: EJsonSchemaPatternMatchLimit do
      WriteLn(E.Limit, ' ms, ', E.Memory, ': ', E.Pattern);  // 100 ms, FALSE: ^(?:(a+)\1*)+$
  end;

  // The Try form raises nothing. It returns False with the reason, which is empty for an instance that was
  // evaluated and is not valid.
  WriteLn(Validator.TryIsValid(Instance, Error));  // FALSE
  WriteLn(Error);
  // A match of the regular expression '^(?:(a+)\1*)+$' was abandoned: it took longer than the limit of 100ms.

  // A pattern over a limit of size is refused when the schema is compiled.
  try
    CompileJsonSchema('{"pattern": "' + StringOfChar('a', 20000) + '"}');
  except
    on E: EJsonSchemaPatternTooLarge do
      WriteLn(E.Keyword, ': ', E.Limit, ' of ', E.Value);  // pattern: size of 1048576
  end;
end;
```

Most patterns that a backtracking matcher takes long for do not reach the limit. The matcher looks at the next
character of the string before it takes a way, as V8 does, and for a pattern with no backreference it remembers
the places where every way has failed, so `(a+)+$`, `(a|aa)+$` and `(\w+\s?)+$` answer at once on any text. A
pattern of a few characters can still need more than a second: a group read back inside a repetition
(`^(?:(a+)\1*)+$`), a repetition that counts to a bound around something that matches in more than one way
(`^(?:a{0,1000}){0,1000}$`), a lookaround that reads to the end of a long text from every position of it. The
answer is then `EJsonSchemaPatternMatchLimit`, where it was no answer at all. The package's README lists what
reached a limit in the tests.

## Performance

Measured with [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark)'s 37 corpora, each
implementation in its own container pinned to the same 8 CPUs, the median of 3 runs. The Object Pascal program was
built with Free Pascal 3.2.2 at `-O3` for x86-64 Linux. The Object Pascal, Go and Julia harnesses warm up for 2
seconds (at least 100 passes) and report the last warm-up pass. The others are the benchmark's own harnesses. Each
figure is the geometric mean of Object Pascal's time over the other's (below 1 means Object Pascal is faster), with
how many corpora Object Pascal is faster on.

| Object Pascal over | Warm validation | Cold validation | Compile | Parse |
|---|---|---|---|---|
| [Blaze](https://github.com/sourcemeta/blaze) | 1.25 (12 of 37) | 0.84 (28 of 37) | 0.30 (37 of 37) | 0.68 (35 of 37) |
| Corvus Go | 1.82 (0 of 37) | 1.39 (1 of 37) | 2.18 (0 of 37) | 1.96 (1 of 37) |
| Corvus Julia | 1.57 (0 of 37) | 1.01 (16 of 37) | 1.16 (11 of 37) | 1.54 (1 of 37) |
| Corvus Rust | 2.37 (0 of 37) | 1.53 (2 of 37) | 1.34 (3 of 37) | 1.85 (0 of 37) |

Warm validation is slower than Blaze's and than the other Corvus ports', and validation from a cold start, compiling
a schema and parsing are faster than Blaze's. Two things account for the warm figure. Every array read is bounds
checked, as in the Go module and the Julia package. And Free Pascal 3.2.2 generates slower code for these loops than
the Go, Julia and Rust compilers do. The checks are the smaller part: a build with no checks at all, which is not how
the package is built, took 0.93 of the time on these corpora (one pinned core).

## Links

- Releases: [GitHub releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=pas-v)
- Source and README: [src-pas/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-pas/corvus-json-schema)
- The other languages: see [Other languages](OtherLanguages.md)
