unit Corvus.JsonSchema;

{$I corvus.inc}

(* Corvus.JsonSchema is a JSON Schema evaluator for draft 4, 6, 7, 2019-09 and 2020-12, ported from the
  Corvus.Text.Json runtime evaluator by way of its Go port. This is the one unit a program names in its uses
  clause. A port of validator.go and version.go of the Go module.

  A schema is compiled once into a node graph with fail-fast plans, then any number of instances are validated
  against it. IsValid and its variants fail fast and report nothing. Evaluate is exhaustive and reports to a
  results collector at the Basic, Detailed or Verbose level, with the same rows (paths, messages, order) as the
  other Corvus implementations, annotations included.

    var
      Validator: IJsonSchemaValidator;
    begin
      Validator := CompileJsonSchema('{"type": "object", "properties": {"id": {"type": "integer", "minimum": 1}},'
        + ' "required": ["id"]}');
      Validator.IsValid('{"id": 3}');   // True
      Validator.IsValid('{"id": 0}');   // False
    end;

  A validator is an interface, so nothing frees it. It is immutable once compiled and safe to use from any number
  of threads at once. In the steady state, validating a parsed document, or JSON text, allocates nothing: a thread
  keeps the buffers its validations need, and a thread that is about to end calls JsonSchemaReleaseThreadScratch to
  free them.

  The types and functions of the other units that a program needs are declared again here (the options, the
  parsed document, the results collector), so that this unit is enough. *)

interface

uses
  SysUtils,
  SyncObjs,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.Results,
  Corvus.JsonSchema.Plan;

const
  { JsonSchemaVersion is the version of this package. }
  JsonSchemaVersion = '0.1.0';

type
  { EJsonSchemaError is the ancestor of the exceptions this unit raises. Utf8Message is the message as UTF-8. }
  EJsonSchemaError = class(Exception)
  private
    FUtf8Message: UTF8String;
  public
    constructor CreateUtf8(const AMessage: UTF8String);
    property Utf8Message: UTF8String read FUtf8Message;
  end;

  { EJsonParseError reports text that is not valid JSON. Offset is the byte offset at which the error was found. }
  EJsonParseError = class(EJsonSchemaError)
  private
    FOffset: Int32;
  public
    property Offset: Int32 read FOffset;
  end;

  { EJsonSchemaCompileError reports a schema that cannot be compiled (an unresolvable reference, an invalid
    pattern). }
  EJsonSchemaCompileError = class(EJsonSchemaError);

  { EJsonSchemaDepthError reports that evaluation recursed in place beyond the maximum depth (a schema that loops
    without consuming the instance). }
  EJsonSchemaDepthError = class(EJsonSchemaError);

  { TJsonDocument is JSON text parsed for evaluation: parse once, validate any number of times. It is a value that
    needs no Free, is not changed once parsed, and is safe to read from several threads. }
  TJsonDocument = Corvus.JsonSchema.Document.TDocument;

  { TJsonSchemaDialect is a JSON Schema dialect the evaluator understands. }
  TJsonSchemaDialect = Corvus.JsonSchema.Dialect.TDialect;

  { TJsonSchemaOptions are the options for compiling a schema. DefaultJsonSchemaOptions gives the defaults, and
    the With procedures change them. }
  TJsonSchemaOptions = Corvus.JsonSchema.Options.TCompileOptions;
  { TJsonSchemaResolver resolves a schema document by absolute URI. It returns False if the document is unknown.
    Context is the pointer given to WithDocumentResolver. }
  TJsonSchemaResolver = Corvus.JsonSchema.Options.TDocumentResolver;
  { TJsonSchemaFormat is a custom format assertion over the string value (or the number's JSON text, for numbers):
    the bytes Text[Start .. Start+Len-1]. It must not raise an exception. }
  TJsonSchemaFormat = Corvus.JsonSchema.Options.TFormatValidator;

  { TJsonSchemaResultsLevel says how much a results collector records. }
  TJsonSchemaResultsLevel = Corvus.JsonSchema.Results.TResultsLevel;
  { TJsonSchemaResult is one result row. }
  TJsonSchemaResult = Corvus.JsonSchema.Results.TSchemaResult;
  TJsonSchemaResultArray = Corvus.JsonSchema.Results.TSchemaResultArray;
  { TJsonSchemaResults collects the results of an evaluation. It is a value that needs no Free. A collector is used
    by one evaluation at a time. }
  TJsonSchemaResults = Corvus.JsonSchema.Results.TResultsCollector;
  { TJsonSchemaAnnotation is an annotation extracted from verbose results. }
  TJsonSchemaAnnotation = Corvus.JsonSchema.Results.TAnnotation;
  TJsonSchemaAnnotationArray = Corvus.JsonSchema.Results.TAnnotationArray;
  { TJsonSchemaCollectedAnnotation is the value of a keyword at an instance location, from the schema at a schema
    location fragment. }
  TJsonSchemaCollectedAnnotation = Corvus.JsonSchema.Results.TCollectedAnnotation;
  TJsonSchemaCollectedAnnotationArray = Corvus.JsonSchema.Results.TCollectedAnnotationArray;

const
  { The dialects, in specification order. }
  Draft4 = Corvus.JsonSchema.Dialect.Draft4;
  Draft6 = Corvus.JsonSchema.Dialect.Draft6;
  Draft7 = Corvus.JsonSchema.Dialect.Draft7;
  Draft201909 = Corvus.JsonSchema.Dialect.Draft201909;
  Draft202012 = Corvus.JsonSchema.Dialect.Draft202012;

  { Basic records failures only, without message text (the lowest overhead). }
  Basic = Corvus.JsonSchema.Results.Basic;
  { Detailed records failures only, with message text. }
  Detailed = Corvus.JsonSchema.Results.Detailed;
  { Verbose records every evaluation, passing and failing, with message text, including annotations. }
  Verbose = Corvus.JsonSchema.Results.Verbose;

type
  { IJsonSchemaValidator is a compiled schema. It is immutable and safe for concurrent use.

    The Go module has two forms of each fail-fast call: IsValid, which reports text that is not JSON as invalid,
    and Validate, which returns the reason as an error. Here IsValid raises EJsonParseError for text that is not
    JSON, and TryIsValid raises nothing: it returns False with the reason in Error, which is empty when the
    instance was evaluated and is simply not valid. }
  IJsonSchemaValidator = interface
    ['{6E1B6C0B-52D5-4C59-9E0C-0F3B4D0A7C31}']
    { IsValid reports whether an instance is valid. When the schema recursed in place beyond the maximum depth the
      result is what the abandoned evaluation came to, as in the Go module: not valid, unless the branch that was
      abandoned is under a "not". Use TryIsValid to be told. }
    function IsValid(const Instance: TJsonDocument): Boolean; overload;
    { IsValid reports whether JSON text is a valid instance. The text is parsed into buffers the calling thread
      reuses, so a validation allocates nothing in the steady state. It raises EJsonParseError when the text is not
      JSON. }
    function IsValid(const Json: UTF8String): Boolean; overload;
    { IsValid for the UTF-8 JSON text Json[Start .. Start+Len-1]. }
    function IsValid(const Json: TBytes; Start, Len: Int32): Boolean; overload;

    { TryIsValid reports whether an instance is valid, and raises nothing. Error is the reason when the instance
      could not be evaluated: text that is not JSON, or a schema that recursed in place beyond the maximum depth.
      It is empty otherwise. }
    function TryIsValid(const Instance: TJsonDocument; out Error: UTF8String): Boolean; overload;
    function TryIsValid(const Json: UTF8String; out Error: UTF8String): Boolean; overload;
    function TryIsValid(const Json: TBytes; Start, Len: Int32; out Error: UTF8String): Boolean; overload;

    { Evaluate evaluates an instance exhaustively, reporting to the collector, and reports whether the instance is
      valid. It raises EJsonSchemaDepthError when evaluation recursed in place beyond the maximum depth, and
      EJsonParseError when the text is not JSON. }
    function Evaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults): Boolean; overload;
    function Evaluate(const Json: UTF8String; var Results: TJsonSchemaResults): Boolean; overload;
    function Evaluate(const Json: TBytes; Start, Len: Int32; var Results: TJsonSchemaResults): Boolean; overload;

    { TryEvaluate is Evaluate that raises nothing: Error is the reason when the instance could not be evaluated. }
    function TryEvaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;
    function TryEvaluate(const Json: UTF8String; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;
    function TryEvaluate(const Json: TBytes; Start, Len: Int32; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;

    { CompiledProgram is the compiled program, for the tests and tools that look at the plans. }
    function CompiledProgram: PProgram;
  end;

{ Compiling. Each function raises EJsonParseError when schema text is not JSON, and EJsonSchemaCompileError when
  the schema cannot be compiled. }

{ CompileJsonSchema compiles a schema given as JSON text. }
function CompileJsonSchema(const Schema: UTF8String): IJsonSchemaValidator; overload;
function CompileJsonSchema(const Schema: UTF8String; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
  overload;
{ CompileJsonSchema compiles a schema given as UTF-8 JSON text. The validator keeps the array. Do not modify it
  afterwards. }
function CompileJsonSchema(const Schema: TBytes): IJsonSchemaValidator; overload;
function CompileJsonSchema(const Schema: TBytes; const Options: TJsonSchemaOptions): IJsonSchemaValidator; overload;
{ CompileJsonSchema compiles a schema document. }
function CompileJsonSchema(const Schema: TJsonDocument): IJsonSchemaValidator; overload;
function CompileJsonSchema(const Schema: TJsonDocument; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
  overload;
{ CompileJsonSchemaFromUri compiles the schema document at a URI, fetched through the document resolver given in
  the options (or one of the standard metaschemas). }
function CompileJsonSchemaFromUri(const Uri: UTF8String): IJsonSchemaValidator; overload;
function CompileJsonSchemaFromUri(const Uri: UTF8String; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
  overload;

{ Documents. }

{ ParseJson parses JSON text into a document. It raises EJsonParseError when the text is not JSON. }
function ParseJson(const Json: UTF8String): TJsonDocument; overload;
{ ParseJson parses UTF-8 JSON text. The document keeps the array. Do not modify it afterwards. }
function ParseJson(const Json: TBytes): TJsonDocument; overload;
{ TryParseJson parses JSON text and raises nothing. False, with the reason and the byte offset at which the error
  was found, when the text is not JSON. }
function TryParseJson(const Json: UTF8String; out Doc: TJsonDocument; out Error: UTF8String;
  out ErrorOffset: Int32): Boolean; overload;
function TryParseJson(const Json: TBytes; out Doc: TJsonDocument; out Error: UTF8String;
  out ErrorOffset: Int32): Boolean; overload;
{ JsonText returns a document as compact JSON text, with numbers as they were written. }
function JsonText(const Doc: TJsonDocument): UTF8String;

{ Options. }

{ DefaultJsonSchemaOptions are the options of a compilation that is given none. }
function DefaultJsonSchemaOptions: TJsonSchemaOptions;
{ WithDefaultDialect sets the dialect of documents without $schema. The default is Draft202012. }
procedure WithDefaultDialect(var O: TJsonSchemaOptions; Dialect: TJsonSchemaDialect);
{ WithAssertFormat says whether format is asserted: True always, False never. Without this option the vocabularies
  decide (the 2020-12 format-assertion vocabulary asserts, anything else annotates). }
procedure WithAssertFormat(var O: TJsonSchemaOptions; Assert: Boolean);
{ WithAssertFormatInLegacyDrafts also asserts format in drafts 4 to 7, when WithAssertFormat is not given. }
procedure WithAssertFormatInLegacyDrafts(var O: TJsonSchemaOptions; Assert: Boolean);
{ WithAssertContent says whether contentEncoding and contentMediaType are asserted in draft 7, the only draft that
  asserts them. The default is True. }
procedure WithAssertContent(var O: TJsonSchemaOptions; Assert: Boolean);
{ WithFormat adds a custom format assertion, which takes precedence over a built-in one of the same name. It
  receives the bytes of the string value, or of a number's JSON text. }
procedure WithFormat(var O: TJsonSchemaOptions; const Name: UTF8String; Validator: TJsonSchemaFormat);
{ WithDocumentResolver sets the function that resolves remote documents, and the context it is called with. The
  standard metaschemas are always available. }
procedure WithDocumentResolver(var O: TJsonSchemaOptions; Resolver: TJsonSchemaResolver; Context: Pointer);
{ WithBaseURI sets the base URI of the root document. }
procedure WithBaseURI(var O: TJsonSchemaOptions; const Uri: UTF8String);
{ WithEntryPoint sets a reference, relative to the root, to evaluate from (for example #/$defs/item). The default
  is the root. }
procedure WithEntryPoint(var O: TJsonSchemaOptions; const Reference: UTF8String);
{ WithMaxDepth sets the maximum depth of in-place recursion on a cycle before evaluation is abandoned. The default
  is 128. Values below 1 are ignored. }
procedure WithMaxDepth(var O: TJsonSchemaOptions; Depth: Int32);

{ Results. }

{ NewJsonSchemaResults creates a results collector at the given level. }
function NewJsonSchemaResults(Level: TJsonSchemaResultsLevel): TJsonSchemaResults;
{ ResultCount is the number of results. The results are Results.Committed[0 .. ResultCount-1], in commit order. }
function ResultCount(const Results: TJsonSchemaResults): Int32;
{ ResultRows returns a copy of the results, in commit order. }
function ResultRows(const Results: TJsonSchemaResults): TJsonSchemaResultArray;
{ ResetResults discards the results, so that the collector can be used for another evaluation. }
procedure ResetResults(var Results: TJsonSchemaResults);
{ ResultAnnotations returns the annotations in a verbose collector's results. }
function ResultAnnotations(const Results: TJsonSchemaResults): TJsonSchemaAnnotationArray;
{ CollectedAnnotations returns the annotations by instance location, keyword and schema location fragment ("#"
  followed by the schema location), with the values as JSON text: one entry for each of the three, in the order
  each was first seen. }
function CollectedAnnotations(const Results: TJsonSchemaResults): TJsonSchemaCollectedAnnotationArray;
{ FindAnnotation is the value, as JSON text, of a keyword at an instance location from the schema at a schema
  location fragment. False when there is no such annotation. }
function FindAnnotation(const Annotations: TJsonSchemaCollectedAnnotationArray; const InstanceLocation, Keyword,
  SchemaLocationFragment: UTF8String; out Value: UTF8String): Boolean;

{ JsonSchemaReleaseThreadScratch frees the buffers the calling thread's validations and pattern matches have kept.
  A thread that has validated should call it before it ends, because a thread variable of a managed type is not
  freed with its thread. Calling it at any other time costs only the allocations of the next validation. Do not
  call it from a format validator. }
procedure JsonSchemaReleaseThreadScratch;

implementation

uses
  Corvus.JsonSchema.EcmaRegex,
  Corvus.JsonSchema.Compiler,
  Corvus.JsonSchema.Eval;

type
  PParseError = ^TParseError;

  { How a validation of JSON text ended. }
  TTextStatus = (TextEvaluated, TextNotJson, TextDepthExceeded);

  TJsonSchemaValidator = class(TInterfacedObject, IJsonSchemaValidator)
  private
    FProgram: TProgram;
    { Held while the annotation keywords are digested, on the first evaluation with a collector. }
    FLock: TCriticalSection;
    function Run(Instance: PDocument; S: PScratch; out DepthExceeded: Boolean): Boolean;
    function RunParsed(S: PScratch; const B: TBytes; Len: Int32; out Status: TTextStatus;
      Error: PParseError): Boolean;
    function RunNested(const B: TBytes; out Status: TTextStatus; Error: PParseError): Boolean;
    function RunNestedBytes(const Json: TBytes; Start, Len: Int32; out Status: TTextStatus;
      Error: PParseError): Boolean;
    function RunNestedString(const Json: UTF8String; out Status: TTextStatus; Error: PParseError): Boolean;
    function RunBytes(const Json: TBytes; Start, Len: Int32; out Status: TTextStatus; Error: PParseError): Boolean;
    function RunString(const Json: UTF8String; out Status: TTextStatus; Error: PParseError): Boolean;
    procedure RaiseStringNotJson(const Json: UTF8String);
    procedure RaiseBytesNotJson(const Json: TBytes; Start, Len: Int32);
    function Collect(const Instance: TJsonDocument; var Results: TJsonSchemaResults;
      out DepthExceeded: Boolean): Boolean;
  public
    constructor Create(const Compiled: TCompiledSchema; const Options: TCompileOptions);
    destructor Destroy; override;
    function IsValid(const Instance: TJsonDocument): Boolean; overload;
    function IsValid(const Json: UTF8String): Boolean; overload;
    function IsValid(const Json: TBytes; Start, Len: Int32): Boolean; overload;
    function TryIsValid(const Instance: TJsonDocument; out Error: UTF8String): Boolean; overload;
    function TryIsValid(const Json: UTF8String; out Error: UTF8String): Boolean; overload;
    function TryIsValid(const Json: TBytes; Start, Len: Int32; out Error: UTF8String): Boolean; overload;
    function Evaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults): Boolean; overload;
    function Evaluate(const Json: UTF8String; var Results: TJsonSchemaResults): Boolean; overload;
    function Evaluate(const Json: TBytes; Start, Len: Int32; var Results: TJsonSchemaResults): Boolean; overload;
    function TryEvaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;
    function TryEvaluate(const Json: UTF8String; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;
    function TryEvaluate(const Json: TBytes; Start, Len: Int32; var Results: TJsonSchemaResults;
      out Error: UTF8String): Boolean; overload;
    function CompiledProgram: PProgram;
  end;

{ ---------------------------------------------------------------------------------------------------------------------
  Exceptions }

constructor EJsonSchemaError.CreateUtf8(const AMessage: UTF8String);
begin
  inherited Create(string(AMessage));
  FUtf8Message := AMessage;
end;

procedure RaiseParseError(const Error: TParseError);
var
  E: EJsonParseError;
begin
  E := EJsonParseError.CreateUtf8(ParseErrorText(Error));
  E.FOffset := Error.Offset;
  raise E;
end;

procedure RaiseDepthError;
begin
  raise EJsonSchemaDepthError.CreateUtf8(ErrDepthExceeded);
end;

{ Slice is a copy of the bytes Json[Start .. Start+Len-1]. A range that is not within the array is cut to it, so
  that it reads no other memory: the text that is left is then not JSON, or is what the caller meant. }
function Slice(const Json: TBytes; Start, Len: Int32): TBytes;
begin
  Result := nil;
  if (Start < 0) or (Len <= 0) or (Start >= Length(Json)) then
    Exit;
  if Len > Length(Json) - Start then
    Len := Length(Json) - Start;
  SetLength(Result, Len);
  Move(Json[Start], Result[0], Len);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The validator }

constructor TJsonSchemaValidator.Create(const Compiled: TCompiledSchema; const Options: TCompileOptions);
begin
  inherited Create;
  FLock := TCriticalSection.Create;
  NewProgram(Compiled, Options, FProgram);
end;

destructor TJsonSchemaValidator.Destroy;
begin
  FLock.Free;
  inherited Destroy;
end;

function TJsonSchemaValidator.CompiledProgram: PProgram;
begin
  Result := @FProgram;
end;

{ Run validates a document, failing fast. It reports the result and whether evaluation recursed in place beyond the
  maximum depth. }
function TJsonSchemaValidator.Run(Instance: PDocument; S: PScratch; out DepthExceeded: Boolean): Boolean;
var
  E: TEvaluator;
begin
  NewEvaluator(E, @FProgram, Instance, nil, S);
  Result := Corvus.JsonSchema.Eval.Validate(E);
  FinishEvaluator(E);
  if (S = nil) and (E.S <> nil) then
    ReleaseScratch(E.S);
  DepthExceeded := E.DepthExceeded;
end;

{ RunParsed parses B[0 .. Len-1] into the thread's reused document and validates it. The result is the
  evaluation's, whatever the status (as the run of the Go source, which returns the result beside the flag). }
function TJsonSchemaValidator.RunParsed(S: PScratch; const B: TBytes; Len: Int32; out Status: TTextStatus;
  Error: PParseError): Boolean;
var
  Exceeded: Boolean;
begin
  Result := False;
  S^.TextBusy := True;
  if not ParserParseInto(S^.Parser, S^.Text, B, Len) then begin
    Status := TextNotJson;
    if Error <> nil then
      Error^ := ParserError(S^.Parser);
  end else begin
    Result := Run(@S^.Text, S, Exceeded);
    Status := TextEvaluated;
    if Exceeded then
      Status := TextDepthExceeded;
  end;
  ReleaseText(S);
end;

{ RunNested validates JSON text while a validation of JSON text is running on this thread (from a format
  validator): the text is parsed into a document of its own. }
function TJsonSchemaValidator.RunNested(const B: TBytes; out Status: TTextStatus; Error: PParseError): Boolean;
var
  Doc: TDocument;
  ParseError: TParseError;
  Exceeded: Boolean;
begin
  Result := False;
  if not ParseDocument(B, Doc, ParseError) then begin
    Status := TextNotJson;
    if Error <> nil then
      Error^ := ParseError;
    Exit;
  end;
  Result := Run(@Doc, nil, Exceeded);
  Status := TextEvaluated;
  if Exceeded then
    Status := TextDepthExceeded;
end;

{ RunNestedBytes and RunNestedString are apart from their callers so that the usual path holds no copy of the
  text. }
function TJsonSchemaValidator.RunNestedBytes(const Json: TBytes; Start, Len: Int32; out Status: TTextStatus;
  Error: PParseError): Boolean;
begin
  Result := RunNested(Slice(Json, Start, Len), Status, Error);
end;

function TJsonSchemaValidator.RunNestedString(const Json: UTF8String; out Status: TTextStatus;
  Error: PParseError): Boolean;
begin
  Result := RunNested(BytesOf(Json), Status, Error);
end;

function TJsonSchemaValidator.RunBytes(const Json: TBytes; Start, Len: Int32; out Status: TTextStatus;
  Error: PParseError): Boolean;
var
  S: PScratch;
begin
  S := AcquireScratch;
  if S^.TextBusy then
    Exit(RunNestedBytes(Json, Start, Len, Status, Error));
  if (Start = 0) and (Len = Length(Json)) then
    Exit(RunParsed(S, Json, Len, Status, Error));
  { A document reads its text from the start of an array to a byte that ends the last value: text that is part of
    an array is copied to the thread's buffer, with a byte after it that is no part of a value. }
  if (Start < 0) or (Len < 0) or (Start >= Length(Json)) then
    Len := 0
  else if Len > Length(Json) - Start then
    Len := Length(Json) - Start;
  if Len >= Length(S^.TextBuf) then
    SetLength(S^.TextBuf, 2 * Len + 64);
  if Len > 0 then
    Move(Json[Start], S^.TextBuf[0], Len);
  S^.TextBuf[Len] := 0;
  Result := RunParsed(S, S^.TextBuf, Len, Status, Error);
end;

function TJsonSchemaValidator.RunString(const Json: UTF8String; out Status: TTextStatus;
  Error: PParseError): Boolean;
var
  S: PScratch;
  Len: Int32;
begin
  S := AcquireScratch;
  if S^.TextBusy then
    Exit(RunNestedString(Json, Status, Error));
  { A string is not an array of bytes: its text is copied to the thread's buffer, which the parser reads, with a
    byte after it that is no part of a value. }
  Len := Length(Json);
  if Len >= Length(S^.TextBuf) then
    SetLength(S^.TextBuf, 2 * Len + 64);
  if Len > 0 then
    Move(Json[1], S^.TextBuf[0], Len);
  S^.TextBuf[Len] := 0;
  Result := RunParsed(S, S^.TextBuf, Len, Status, Error);
end;

function TJsonSchemaValidator.IsValid(const Instance: TJsonDocument): Boolean;
var
  Exceeded: Boolean;
begin
  Result := Run(@Instance, nil, Exceeded);
end;

{ RaiseStringNotJson and RaiseBytesNotJson raise the EJsonParseError of text that IsValid found is not JSON. The
  reason is asked for only here, in a function of its own, so that IsValid holds no string (a function that holds
  one sets up a frame to free it, on every call). }
procedure TJsonSchemaValidator.RaiseStringNotJson(const Json: UTF8String);
var
  Status: TTextStatus;
  Error: TParseError;
begin
  RunString(Json, Status, @Error);
  RaiseParseError(Error);
end;

procedure TJsonSchemaValidator.RaiseBytesNotJson(const Json: TBytes; Start, Len: Int32);
var
  Status: TTextStatus;
  Error: TParseError;
begin
  RunBytes(Json, Start, Len, Status, @Error);
  RaiseParseError(Error);
end;

function TJsonSchemaValidator.IsValid(const Json: UTF8String): Boolean;
var
  Status: TTextStatus;
begin
  Result := RunString(Json, Status, nil);
  if Status = TextNotJson then
    RaiseStringNotJson(Json);
end;

function TJsonSchemaValidator.IsValid(const Json: TBytes; Start, Len: Int32): Boolean;
var
  Status: TTextStatus;
begin
  Result := RunBytes(Json, Start, Len, Status, nil);
  if Status = TextNotJson then
    RaiseBytesNotJson(Json, Start, Len);
end;

function TJsonSchemaValidator.TryIsValid(const Instance: TJsonDocument; out Error: UTF8String): Boolean;
var
  Exceeded: Boolean;
begin
  Error := '';
  Result := Run(@Instance, nil, Exceeded);
  if Exceeded then begin
    Error := ErrDepthExceeded;
    Result := False;
  end;
end;

function TJsonSchemaValidator.TryIsValid(const Json: UTF8String; out Error: UTF8String): Boolean;
var
  Status: TTextStatus;
  ParseError: TParseError;
begin
  Error := '';
  Result := RunString(Json, Status, @ParseError);
  case Status of
    TextNotJson: Error := ParseErrorText(ParseError);
    TextDepthExceeded: begin
      Error := ErrDepthExceeded;
      Result := False;
    end;
  else
  end;
end;

function TJsonSchemaValidator.TryIsValid(const Json: TBytes; Start, Len: Int32; out Error: UTF8String): Boolean;
var
  Status: TTextStatus;
  ParseError: TParseError;
begin
  Error := '';
  Result := RunBytes(Json, Start, Len, Status, @ParseError);
  case Status of
    TextNotJson: Error := ParseErrorText(ParseError);
    TextDepthExceeded: begin
      Error := ErrDepthExceeded;
      Result := False;
    end;
  else
  end;
end;

{ Collect evaluates an instance exhaustively, reporting to the collector. }
function TJsonSchemaValidator.Collect(const Instance: TJsonDocument; var Results: TJsonSchemaResults;
  out DepthExceeded: Boolean): Boolean;
var
  E: TEvaluator;
begin
  { The annotation keywords of every node are digested on the first evaluation with a collector. }
  FLock.Enter;
  try
    ComputeNodeAnnotations(FProgram);
  finally
    FLock.Leave;
  end;
  NewEvaluator(E, @FProgram, @Instance, @Results, nil);
  Result := Corvus.JsonSchema.Eval.Evaluate(E);
  FinishEvaluator(E);
  if E.S <> nil then
    ReleaseScratch(E.S);
  DepthExceeded := E.DepthExceeded;
  if DepthExceeded then
    Result := False;
end;

function TJsonSchemaValidator.Evaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults): Boolean;
var
  Exceeded: Boolean;
begin
  Result := Collect(Instance, Results, Exceeded);
  if Exceeded then
    RaiseDepthError;
end;

function TJsonSchemaValidator.Evaluate(const Json: UTF8String; var Results: TJsonSchemaResults): Boolean;
begin
  Result := Evaluate(ParseJson(Json), Results);
end;

function TJsonSchemaValidator.Evaluate(const Json: TBytes; Start, Len: Int32;
  var Results: TJsonSchemaResults): Boolean;
begin
  if (Start = 0) and (Len = Length(Json)) then
    Result := Evaluate(ParseJson(Json), Results)
  else
    Result := Evaluate(ParseJson(Slice(Json, Start, Len)), Results);
end;

function TJsonSchemaValidator.TryEvaluate(const Instance: TJsonDocument; var Results: TJsonSchemaResults;
  out Error: UTF8String): Boolean;
var
  Exceeded: Boolean;
begin
  Error := '';
  Result := Collect(Instance, Results, Exceeded);
  if Exceeded then
    Error := ErrDepthExceeded;
end;

function TJsonSchemaValidator.TryEvaluate(const Json: UTF8String; var Results: TJsonSchemaResults;
  out Error: UTF8String): Boolean;
var
  Doc: TJsonDocument;
  Offset: Int32;
begin
  Result := False;
  if TryParseJson(Json, Doc, Error, Offset) then
    Result := TryEvaluate(Doc, Results, Error);
end;

function TJsonSchemaValidator.TryEvaluate(const Json: TBytes; Start, Len: Int32; var Results: TJsonSchemaResults;
  out Error: UTF8String): Boolean;
var
  Doc: TJsonDocument;
  Offset: Int32;
  Parsed: Boolean;
begin
  Result := False;
  if (Start = 0) and (Len = Length(Json)) then
    Parsed := TryParseJson(Json, Doc, Error, Offset)
  else
    Parsed := TryParseJson(Slice(Json, Start, Len), Doc, Error, Offset);
  if Parsed then
    Result := TryEvaluate(Doc, Results, Error);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Compiling }

function NewValidator(const Compiled: TCompiledSchema; const Options: TCompileOptions): IJsonSchemaValidator;
begin
  Result := TJsonSchemaValidator.Create(Compiled, Options);
end;

function CompileJsonSchema(const Schema: TJsonDocument; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
var
  Compiled: TCompiledSchema;
  Error: UTF8String;
begin
  if not CompileDocument(Schema, Options, Compiled, Error) then
    raise EJsonSchemaCompileError.CreateUtf8(Error);
  Result := NewValidator(Compiled, Options);
end;

function CompileJsonSchema(const Schema: TJsonDocument): IJsonSchemaValidator;
begin
  Result := CompileJsonSchema(Schema, DefaultOptions);
end;

function CompileJsonSchema(const Schema: TBytes; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
begin
  Result := CompileJsonSchema(ParseJson(Schema), Options);
end;

function CompileJsonSchema(const Schema: TBytes): IJsonSchemaValidator;
begin
  Result := CompileJsonSchema(ParseJson(Schema), DefaultOptions);
end;

function CompileJsonSchema(const Schema: UTF8String; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
begin
  Result := CompileJsonSchema(ParseJson(Schema), Options);
end;

function CompileJsonSchema(const Schema: UTF8String): IJsonSchemaValidator;
begin
  Result := CompileJsonSchema(ParseJson(Schema), DefaultOptions);
end;

function CompileJsonSchemaFromUri(const Uri: UTF8String; const Options: TJsonSchemaOptions): IJsonSchemaValidator;
var
  Compiled: TCompiledSchema;
  Error: UTF8String;
begin
  if not CompileFromURI(Uri, Options, Compiled, Error) then
    raise EJsonSchemaCompileError.CreateUtf8(Error);
  Result := NewValidator(Compiled, Options);
end;

function CompileJsonSchemaFromUri(const Uri: UTF8String): IJsonSchemaValidator;
begin
  Result := CompileJsonSchemaFromUri(Uri, DefaultOptions);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Documents }

function ParseJson(const Json: TBytes): TJsonDocument;
var
  Error: TParseError;
begin
  if not ParseDocument(Json, Result, Error) then
    RaiseParseError(Error);
end;

function ParseJson(const Json: UTF8String): TJsonDocument;
begin
  Result := ParseJson(BytesOf(Json));
end;

function TryParseJson(const Json: TBytes; out Doc: TJsonDocument; out Error: UTF8String;
  out ErrorOffset: Int32): Boolean;
var
  ParseError: TParseError;
begin
  Error := '';
  ErrorOffset := 0;
  Result := ParseDocument(Json, Doc, ParseError);
  if not Result then begin
    Error := ParseErrorText(ParseError);
    ErrorOffset := ParseError.Offset;
  end;
end;

function TryParseJson(const Json: UTF8String; out Doc: TJsonDocument; out Error: UTF8String;
  out ErrorOffset: Int32): Boolean;
begin
  Result := TryParseJson(BytesOf(Json), Doc, Error, ErrorOffset);
end;

function JsonText(const Doc: TJsonDocument): UTF8String;
begin
  Result := DocumentToJson(Doc);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Options }

function DefaultJsonSchemaOptions: TJsonSchemaOptions;
begin
  Result := DefaultOptions;
end;

procedure WithDefaultDialect(var O: TJsonSchemaOptions; Dialect: TJsonSchemaDialect);
begin
  Corvus.JsonSchema.Options.WithDefaultDialect(O, Dialect);
end;

procedure WithAssertFormat(var O: TJsonSchemaOptions; Assert: Boolean);
begin
  Corvus.JsonSchema.Options.WithAssertFormat(O, Assert);
end;

procedure WithAssertFormatInLegacyDrafts(var O: TJsonSchemaOptions; Assert: Boolean);
begin
  Corvus.JsonSchema.Options.WithAssertFormatInLegacyDrafts(O, Assert);
end;

procedure WithAssertContent(var O: TJsonSchemaOptions; Assert: Boolean);
begin
  Corvus.JsonSchema.Options.WithAssertContent(O, Assert);
end;

procedure WithFormat(var O: TJsonSchemaOptions; const Name: UTF8String; Validator: TJsonSchemaFormat);
begin
  Corvus.JsonSchema.Options.WithFormat(O, Name, Validator);
end;

procedure WithDocumentResolver(var O: TJsonSchemaOptions; Resolver: TJsonSchemaResolver; Context: Pointer);
begin
  Corvus.JsonSchema.Options.WithDocumentResolver(O, Resolver, Context);
end;

procedure WithBaseURI(var O: TJsonSchemaOptions; const Uri: UTF8String);
begin
  Corvus.JsonSchema.Options.WithBaseURI(O, Uri);
end;

procedure WithEntryPoint(var O: TJsonSchemaOptions; const Reference: UTF8String);
begin
  Corvus.JsonSchema.Options.WithEntryPoint(O, Reference);
end;

procedure WithMaxDepth(var O: TJsonSchemaOptions; Depth: Int32);
begin
  Corvus.JsonSchema.Options.WithMaxDepth(O, Depth);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Results }

function NewJsonSchemaResults(Level: TJsonSchemaResultsLevel): TJsonSchemaResults;
begin
  Result := NewResultsCollector(Level);
end;

function ResultCount(const Results: TJsonSchemaResults): Int32;
begin
  Result := CollectorResultCount(Results);
end;

function ResultRows(const Results: TJsonSchemaResults): TJsonSchemaResultArray;
begin
  Result := CollectorResults(Results);
end;

procedure ResetResults(var Results: TJsonSchemaResults);
begin
  CollectorReset(Results);
end;

function ResultAnnotations(const Results: TJsonSchemaResults): TJsonSchemaAnnotationArray;
begin
  Result := CollectorAnnotations(Results);
end;

function CollectedAnnotations(const Results: TJsonSchemaResults): TJsonSchemaCollectedAnnotationArray;
begin
  Result := CollectAnnotations(Results);
end;

function FindAnnotation(const Annotations: TJsonSchemaCollectedAnnotationArray; const InstanceLocation, Keyword,
  SchemaLocationFragment: UTF8String; out Value: UTF8String): Boolean;
var
  I: Int32;
begin
  Value := '';
  for I := 0 to Length(Annotations) - 1 do
    if (Annotations[I].InstanceLocation = InstanceLocation) and (Annotations[I].Keyword = Keyword)
      and (Annotations[I].SchemaLocationFragment = SchemaLocationFragment) then begin
      Value := Annotations[I].Value;
      Exit(True);
    end;
  Result := False;
end;

procedure JsonSchemaReleaseThreadScratch;
begin
  ReleaseThreadScratch;
  EcmaRegexReleaseThreadScratch;
end;

end.
