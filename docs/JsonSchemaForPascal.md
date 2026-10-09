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
fpc -Fucorvus-json-schema-0.1.0/src -Ficorvus-json-schema-0.1.0/src yourprogram.pas
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
reference, an invalid pattern) raises `EJsonSchemaCompileError`.

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
with answers recorded from V8 on 61,383 patterns and 1,057,650 match answers.

A pattern with no lookaround, backreference, multiline anchor or word boundary gets an automaton that decides an
ASCII text with one table read per byte, when the automaton is small enough. Every other pattern, and a text that
is not ASCII, runs on a backtracking matcher. The Go module hands most patterns to Go's linear-time `regexp`
package, which Object Pascal has no counterpart of. The backtracking matcher has no step budget, so a pattern with
nested quantifiers can take exponential time on a text it does not match, in cases where the Go module's would not.

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
