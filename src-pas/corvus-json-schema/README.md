# corvus-json-schema (Object Pascal)

A JSON Schema evaluator for Object Pascal (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5
runtime evaluator (`Corvus.Text.Json.RuntimeEvaluator`) by way of its Go port (`src-go/corvus-json-schema`). It is a
native port of the Go module, with its own JSON parser and its own ECMAScript regular expression engine. It has no
dependencies beyond the compiler's run-time library.

Free Pascal 3.2.2 or later is the compiler it is built and tested with. The source is in the Delphi dialect and is
written to be Delphi-compatible, but it has not been compiled with Delphi.

- **Conformant**: passes all 7,966 tests of the JSON-Schema-Test-Suite (required, optional and `optional/format`,
  every draft), with the same single exclusion as the C# runner (`draft4/optional/zeroTerminatedFloats.json`), and all
  217 assertions of the suite's annotation tests.
- **Compiled to plans**: a schema compiles once into a node graph and fail-fast plans, each holding only the checks
  its subschema needs, with an object's keywords fused into one pass over its properties.
- **No allocation**: validating a parsed document, or JSON text through the thread's reused buffers, allocates
  nothing in the steady state. [What allocates](#what-allocates) lists the exceptions.
- **Results and annotations**: evaluate with a results collector at the Basic, Detailed or Verbose level for the same
  rows (locations, messages, order) as the C# `JsonSchemaResultsCollector`, and annotations as
  `JsonSchemaAnnotationProducer` extracts them. For every case of the JSON-Schema-Test-Suite the rows are the same,
  byte for byte, as the Go module's (56,069 rows).
- **Every array read is checked**: range checking is on in every unit and is never turned off, so an index out of
  range raises `ERangeError` and never reads other memory. The Go module likewise has no unchecked reads.

## Install

A release is a source archive on the repository's
[GitHub releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=pas-v), tagged `pas-v<version>`.
The archive (a `.zip` or a `.tar.gz`) unpacks to `corvus-json-schema-<version>`. Give the compiler its `src`
directory for units and include files:

```sh
fpc -Fucorvus-json-schema-0.1.0/src -Ficorvus-json-schema-0.1.0/src yourprogram.pas
```

The one unit a program names in its `uses` clause is `Corvus.JsonSchema`. The samples below are written for the
Delphi mode of Free Pascal (`{$MODE DELPHI}`).

## Usage

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
threads at once (see [Threads](#threads)). `CompileJsonSchema` takes the schema as a `UTF8String`, as UTF-8 bytes
(`TBytes`), or as a parsed `TJsonDocument`, and `CompileJsonSchemaFromUri` fetches it through the document resolver
(or takes a standard metaschema).

Instances can be given as JSON text (a `UTF8String`, or a range of a `TBytes`), parsed into buffers the thread
reuses, or as a `TJsonDocument` parsed once and validated any number of times. A document is a value that needs no
`Free`.

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

`IsValid` raises `EJsonParseError` for text that is not JSON. `TryIsValid` raises nothing. It returns `False` with
the reason in `Error`, which is empty when the instance was evaluated and is simply not valid. The reason is text
that is not JSON, or a schema that recursed in place beyond the maximum depth.

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

A schema that is not JSON raises `EJsonParseError` from the compile functions, and one that cannot be compiled (an
unresolvable reference, an invalid pattern) raises `EJsonSchemaCompileError`. Both descend from `EJsonSchemaError`,
whose `Utf8Message` is the message as UTF-8.

```pascal
begin
  try
    CompileJsonSchema('{"pattern": "("}');
  except
    on E: EJsonSchemaCompileError do
      WriteLn('The schema cannot be compiled: ', E.Utf8Message);
  end;
  try
    CompileJsonSchema('{"type": ');
  except
    on E: EJsonParseError do
      WriteLn('The schema is not JSON: ', E.Utf8Message);
  end;
  // The schema cannot be compiled: Invalid regular expression '(' in pattern.
  // The schema is not JSON: invalid JSON at offset 9: unexpected end of input
end;
```

### Options

The compile functions take a `TJsonSchemaOptions` record (the Object Pascal counterpart of
`JsonSchemaEvaluatorOptions`). `DefaultJsonSchemaOptions` gives the defaults and the `With` procedures change them.
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

| Procedure | Meaning |
|---|---|
| `WithDefaultDialect` | Dialect for schemas without `$schema` (default `Draft202012`). The others are `Draft4`, `Draft6`, `Draft7` and `Draft201909`. |
| `WithAssertFormat` | `True` asserts `format`, `False` never does. Without it the vocabularies decide (2020-12 `format-assertion`). |
| `WithAssertFormatInLegacyDrafts` | Without `WithAssertFormat`, also assert `format` in drafts 4 to 7. |
| `WithAssertContent` | Assert `contentEncoding`/`contentMediaType` in draft 7 (default `True`). |
| `WithFormat` | A custom format assertion, which takes precedence over a built-in one of the same name. The function receives the bytes of the string, or of a number's JSON text, in place. |
| `WithDocumentResolver` | A function that resolves remote `$ref`s by absolute URI, and the context it is called with. The standard metaschemas are built in. |
| `WithBaseURI` | Base URI of the root document. |
| `WithEntryPoint` | Evaluate from a subschema, for example `#/$defs/item`. |
| `WithMaxDepth` | Depth limit for in-place recursion on a cycle (default 128). |

### Results and annotations

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

The levels and rows are those of the C# collector. `Basic` records failures without message text, `Detailed` adds
the text, and `Verbose` records every keyword, passing ones and annotations included. Each row is a
`TJsonSchemaResult` with `IsMatch`, `Message`, `EvaluationLocation`, `SchemaEvaluationLocation` and
`DocumentEvaluationLocation`. A collector is a value that needs no `Free`. It accumulates across evaluations until
`ResetResults`, and is used by one evaluation at a time. `Evaluate` raises `EJsonParseError` for text that is not
JSON and `EJsonSchemaDepthError` when the schema recursed in place beyond the maximum depth, and `TryEvaluate`
returns the reason instead.

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

`ResultAnnotations` returns the same annotations as a list. Collecting runs the general evaluator over the compiled
graph, not the fail-fast plans.

### Threads

A validator is shared between threads without a lock on the validation path. Each thread keeps the buffers its
validations and pattern matches need in thread variables, which is what makes a validation allocate nothing in the
steady state. Neither Free Pascal nor Delphi frees a thread variable of a managed type when its thread ends, so a
thread that has validated should call `JsonSchemaReleaseThreadScratch` before it ends. A thread that does not loses
that memory once, and nothing else goes wrong.

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

procedure ExampleThreads;
var
  Worker: TValidatingThread;
begin
  Worker := TValidatingThread.Create(True);
  try
    Worker.Validator := CompileJsonSchema('{"required": ["id"]}');
    Worker.Start;
    Worker.WaitFor;
    WriteLn(Worker.Valid);  // TRUE
  finally
    Worker.Free;
  end;
end;
```

With Free Pascal on Unix, a program that starts threads names `cthreads` first in its `uses` clause, as any such
program does.

Every sample above is in `tests/TestReadme.pas`, which compiles and runs each one, compares what it writes with its
comments, and fails if a block of Pascal in this file is not in that program line for line.

## How it works

The pipeline follows the C# evaluator stage for stage, as the Rust and Go ports do. The loader identifies documents,
resources, anchors, dialects and vocabularies. The compiler builds one node per schema location with its keywords
digested and `$ref`s resolved, and analyses evaluated-property marking, in-place cycles and `oneOf`/`anyOf`
discriminators. The node graph then compiles to fail-fast plans, which one evaluator interprets:

- each plan holds only the keywords its node has, grouped by the kind of value they apply to, and a child that only
  tests a type is tested where it is used and never entered;
- an object is checked in one pass over its properties, with names looked up by length and then as 64-bit words, and
  `required` as a bit mask filled in the same pass;
- `$ref`, `allOf`, `if`/`then`/`else`, dependencies and `oneOf`/`anyOf` over object schemas fuse into that one pass,
  which also decides `unevaluatedProperties` from the properties it covered;
- `oneOf`/`anyOf` narrow by a discriminator property or by type;
- instances are a flat tape of two words per value over the UTF-8 text, with strings read in place and numbers
  classified when parsed;
- common pattern shapes (literals, class sequences, separated lists, line lengths) match the UTF-8 bytes without a
  regular expression engine, and other patterns run on the package's own ECMA-262 engine (see
  [Patterns](#patterns));
- numbers compare exactly across `Int64`, `UInt64` and `Double`, and `multipleOf` is decided on the decimal digits of
  the text.

The compiled program is records in dynamic arrays that refer to each other by index, as in the Go source. Classes
are used only for the validator behind its interface and for the exceptions.

[OPTIMIZATIONS.md](OPTIMIZATIONS.md) checks each technique of the Go module against this source, and lists what is
not done yet.

## What allocates

Validating a `TJsonDocument`, a `UTF8String` or a `TBytes` with `IsValid` allocates nothing in the steady state,
which is after the thread's first validations have grown its buffers. `tests/TestAllocations.pas` installs a memory
manager that counts the requests for memory of the validating thread, and holds the keyword paths it lists to none:
no memory was requested in 16,900 steady-state validations. These allocate:

- an asserted `regex`, `idn-hostname` or `idn-email` format;
- an asserted `hostname` format, for a host name with a label that starts with `xn--`;
- a `multipleOf` whose divisor has more than 18 significant digits;
- a number of more than 19 significant digits, in the case that its nearest `Double` cannot be decided from the
  first 19;
- text that is not JSON, since `IsValid` raises an exception for it and `TryIsValid` returns the reason as a string;
- JSON text validated from inside a custom format while a validation of JSON text is running on the same thread,
  which is parsed into a document of its own.

A custom format is handed the bytes where they lie, with no copy. A range of a `TBytes` that is not the whole array
is copied into a buffer the thread reuses. Compiling a schema, parsing with `ParseJson`, and evaluating with a
results collector allocate.

## Patterns

`pattern` and `patternProperties` have ECMA-262 semantics (the `u` flag grammar, then the Annex B grammar for a
pattern only that accepts), whichever compiler built the program. A pattern outside the common shapes runs on the
package's own engine, a port of the Go module's `internal/ecmaregex`. The grammar is that of ECMAScript 2025, with
lookbehind, named groups, modifier groups and Unicode property escapes. The engine was compared with answers
recorded from V8: it agrees on 61,383 patterns and 1,057,650 match answers.

The engine has two matchers:

- **An automaton over ASCII text.** A pattern with no lookaround, backreference, multiline anchor or word boundary,
  whose automaton has at most 256 states, gets one when the schema is compiled. It decides an ASCII text with one
  table read per byte.
- **A backtracking matcher.** Every other pattern, and a text that is not ASCII for a pattern that has an automaton.
  Its stack is explicit and kept by the thread, so a long text cannot overflow the thread's stack and a match
  allocates nothing once the stack has grown.

The backtracking matcher has no step budget. A pattern with nested quantifiers can take exponential time on a text
it does not match, as it can in any backtracking engine. A schema whose patterns are not trusted should not be given
texts that are not trusted.

## Differences from the Go module

- **Patterns.** The Go module hands a pattern with no lookaround or backreference to the Go standard library's
  `regexp`, which matches in linear time. Object Pascal has no counterpart, so every pattern here runs on the
  package's own matchers (see [Patterns](#patterns)). A pattern can therefore take exponential time on a text in
  cases where the Go module's would not: a pattern that has no automaton, and a text that is not ASCII.
- **Text that is not JSON.** `IsValid` raises `EJsonParseError` for it, where the Go module's `IsValidString` and
  `IsValidBytes` report it as not valid. `TryIsValid` returns `False` with the reason.
- **Threads.** A thread keeps the buffers of its validations, where the Go module takes them from a pool that
  belongs to the validator. A thread that has validated should call `JsonSchemaReleaseThreadScratch` before it ends.
- **No process-wide pattern cache.** The Go module keeps every compiled pattern for the life of the process. Here a
  pattern is compiled with its schema, and the identical patterns of one schema share a matcher.
- The URI, IRI, URI template and e-mail formats read their grammars directly, where the Go module uses the standard
  library's `regexp`.
- The options are a record changed by `With` procedures, a custom format and a resolver are plain functions (the
  resolver with a context pointer), and the errors are exceptions with a `Try` form beside them.

## Unicode

The package takes no Unicode data from the compiler's run-time library. Every property it reads is in its own
tables, which hold Unicode 17 (`src/Corvus.JsonSchema.UcdTables.inc`, read by `Corvus.JsonSchema.Ucd`). They are the
general categories, the scripts and script extensions, the binary properties ECMA-262 lists, simple case folding and
the simple uppercase mapping. `pattern` and `patternProperties` build their `\p{...}` classes and their
case-insensitive groups from those tables, and the `hostname`, `idn-hostname` and `idn-email` formats read the same
data. A URI is normalized by lowering the letters A to Z only, as RFC 3986 and RFC 3987 specify.

`tools/gen-ucd-tables.ps1` writes the tables from the Unicode data of the `regress` crate, which the Rust port uses.
It is the Go module's generator with the part that writes Go replaced, so the two sets of tables hold the same data.

## Tests

```sh
pwsh tests/run-tests.ps1
```

The script builds every test program with the `fpc` on the `PATH` (or the one `-Fpc` names) and runs them from the
package directory. `-Only` narrows the run, and `-HeapTrace` builds with Free Pascal's heap trace unit and fails a
program that leaves memory unfreed.

- `TestSuite`: the JSON-Schema-Test-Suite (the repository's submodule, or `JSON_SCHEMA_TEST_SUITE`), every case
  fail-fast (as a document, as bytes and as a string) and through a collector at each level. `SUITE_DRAFT` and
  `SUITE_FILTER` narrow it. `TestCompileSuite` compiles every schema of the suite and the standard metaschemas.
- `TestResults`: the suite's annotation tests and the results expectations shared with the C#, Rust, Go, Java and
  TypeScript evaluators.
- `TestAllocations`: validation of a document, of bytes and of a string requests no memory in the steady state.
- `TestApi`: the public functions, the options, validators shared between threads, and the fail-fast plans against
  the general evaluator on the schemas of the Go module's tests.
- `TestDocument`, `TestUri`, `TestFormats`, `TestUcd`, `TestSchemaSide`: the parser and the number conversions, URI
  resolution, the format checks against the suite's format tests, the Unicode tables, and the name lookup, the
  regex-free matchers and the results collector. `TestSchemaSide` also checks that the embedded metaschemas match
  the Go module's (`CORVUS_GO_MODULE`, or `../../src-go/corvus-json-schema`).
- `TestEcmaRegex`: the pattern engine against answers recorded from V8 (the Go module's `v8_oracle.json`), and the
  patterns of the suite. It reads the suite from `CORVUS_JSON_SCHEMA_TEST_SUITE` when the submodule is elsewhere.
- `TestDifferential`: the plans against the general evaluator on the jsonschema-benchmark corpora and on mutations
  of their instances (200,773 instances, with no disagreement). It runs when `JSONSCHEMA_BENCHMARK` names a
  checkout, and says that it was skipped otherwise.
- `TestReadme`: the samples in this README and in `docs/JsonSchemaForPascal.md`.

`TestDoubleCases` and `TestStringCases` are built and not run, because they read files of cases written out from a
reference implementation (Go's `strconv` and `encoding/json`) that are not in the repository and cannot be written
again without Go, and they run when `-DoubleCases` and `-StringCases` name those files.

## License

Apache 2.0.
