program TestApi;

{$I corvus.inc}

{ Public API behaviour: a port of api_test.go and example_test.go of the Go module, of the tests of plan_test.go that
  need an evaluation (the others are in TestSchemaSide), and of what document_test.go has that TestDocument lacks.

  Go's functional options, closures and error values are, here, an options record, plain functions that are handed
  a context, and exceptions with a Try form beside them: see the header of Corvus.JsonSchema.pas. }

uses
  {$IFDEF FPC}{$IFDEF UNIX}cthreads,{$ENDIF}{$ENDIF}
  SysUtils,
  Classes,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Plan,
  Corvus.JsonSchema;

var
  Checks, Failures: Int32;

procedure Check(Ok: Boolean; const What: UTF8String);
begin
  Inc(Checks);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What);
  end;
end;

function Number(V: Int64): UTF8String;
begin
  Result := UTF8String(IntToStr(V));
end;

function BoolText(V: Boolean): UTF8String;
begin
  if V then
    Result := 'true'
  else
    Result := 'false';
end;

function Options: TJsonSchemaOptions;
begin
  Result := DefaultJsonSchemaOptions;
end;

function MustCompile(const Schema: UTF8String): IJsonSchemaValidator; overload;
begin
  Result := CompileJsonSchema(Schema);
end;

function MustCompile(const Schema: UTF8String; const O: TJsonSchemaOptions): IJsonSchemaValidator; overload;
begin
  Result := CompileJsonSchema(Schema, O);
end;

{ ExpectAgreement checks that fail-fast evaluation (through the plans) and collecting evaluation (through the
  general evaluator) give the same answer for each instance. }
procedure ExpectAgreement(const V: IJsonSchemaValidator; const Name: UTF8String;
  const Instances: array of UTF8String);
var
  I: Int32;
  C: TJsonSchemaResults;
  Collected, Fast: Boolean;
  Error: UTF8String;
begin
  for I := 0 to High(Instances) do begin
    C := NewJsonSchemaResults(Basic);
    Collected := V.TryEvaluate(Instances[I], C, Error);
    if Error <> '' then begin
      Check(False, Name + ' on ' + Instances[I] + ': ' + Error);
      Continue;
    end;
    Fast := V.IsValid(Instances[I]);
    Check(Fast = Collected, Name + ' on ' + Instances[I] + ': fail-fast ' + BoolText(Fast) + ', collecting '
      + BoolText(Collected));
  end;
end;

procedure ExpectValid(const V: IJsonSchemaValidator; const Instance: UTF8String; Want: Boolean);
var
  Bytes: TBytes;
begin
  Check(V.IsValid(Instance) = Want, 'IsValid(' + Instance + '), want ' + BoolText(Want));
  Check(V.IsValid(ParseJson(Instance)) = Want, 'IsValid(document ' + Instance + '), want ' + BoolText(Want));
  Bytes := BytesOf(Instance);
  Check(V.IsValid(Bytes, 0, Length(Bytes)) = Want, 'IsValid(bytes ' + Instance + '), want ' + BoolText(Want));
end;

{ A resolver that knows one document. }
type
  TOneDocument = record
    Uri: UTF8String;
    Doc: TJsonDocument;
  end;
  POneDocument = ^TOneDocument;

function OneDocumentResolver(Context: Pointer; const Uri: UTF8String; out Doc: TJsonDocument): Boolean;
begin
  Doc := Default(TJsonDocument);
  Result := Uri = POneDocument(Context)^.Uri;
  if Result then
    Doc := POneDocument(Context)^.Doc;
end;

{ --------------------------------------------------------------------------------------------------------------------
  api_test.go }

procedure TestKeepsTheDynamicScope;
var
  Tree: TOneDocument;
  O: TJsonSchemaOptions;
  V: IJsonSchemaValidator;
begin
  Tree.Uri := 'https://example.com/tree';
  Tree.Doc := ParseJson('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"$id": "https://example.com/tree",'
    + '"$dynamicAnchor": "node",'
    + '"type": "object",'
    + '"properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }'
    + '}');
  O := Options;
  WithDocumentResolver(O, OneDocumentResolver, @Tree);
  V := MustCompile('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"$id": "https://example.com/strict-tree",'
    + '"$dynamicAnchor": "node",'
    + '"$ref": "tree",'
    + '"unevaluatedProperties": false'
    + '}', O);
  ExpectValid(V, '{ "children": [{ "data": 1, "children": [] }] }', True);
  ExpectValid(V, '{ "children": [{ "daat": 1 }] }', False);
end;

function EvenLength(const Text: TBytes; Start, Len: Int32): Boolean;
var
  I, Count: Int32;
begin
  Count := 0;
  for I := Start to Start + Len - 1 do
    if Text[I] and $C0 <> $80 then
      Inc(Count);
  Result := Count mod 2 = 0;
end;

function IsTwelve(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (Len = 2) and (Text[Start] = Ord('1')) and (Text[Start + 1] = Ord('2'));
end;

procedure TestCustomFormatsAreAssertedWhenFormatAssertionIsOn;
var
  O: TJsonSchemaOptions;
  V, Digits: IJsonSchemaValidator;
begin
  O := Options;
  WithAssertFormat(O, True);
  WithFormat(O, 'even-length', EvenLength);
  V := MustCompile('{ "type": "string", "format": "even-length" }', O);
  ExpectValid(V, '"ab"', True);
  ExpectValid(V, '"abc"', False);
  { Code points are counted, not bytes: two characters of two and of four bytes, then three. }
  ExpectValid(V, '"' + #$C3#$A9 + #$F0#$9F#$98#$80 + '"', True);
  ExpectValid(V, '"aé😀"', False);
  ExpectAgreement(V, 'even-length', ['"ab"', '"abc"', '1']);
  { A custom format on a number receives its JSON text. }
  O := Options;
  WithAssertFormat(O, True);
  WithFormat(O, 'int32', IsTwelve);
  Digits := MustCompile('{ "format": "int32" }', O);
  ExpectValid(Digits, '12', True);
  ExpectValid(Digits, '12.0', False);
  ExpectAgreement(Digits, 'custom int32', ['12', '13', '"12"']);
end;

procedure TestFormatIsAnAnnotationByDefaultAndAssertedOnRequest;
const
  Schema: UTF8String = '{ "format": "ipv4" }';
  Draft7Schema: UTF8String = '{ "$schema": "http://json-schema.org/draft-07/schema#", "format": "ipv4" }';
var
  O: TJsonSchemaOptions;
begin
  ExpectValid(MustCompile(Schema), '"not an address"', True);
  O := Options;
  WithAssertFormat(O, True);
  ExpectValid(MustCompile(Schema, O), '"not an address"', False);
  ExpectValid(MustCompile(Schema, O), '"10.0.0.1"', True);
  ExpectValid(MustCompile(Draft7Schema), '"not an address"', True);
  O := Options;
  WithAssertFormatInLegacyDrafts(O, True);
  ExpectValid(MustCompile(Draft7Schema, O), '"not an address"', False);
  WithAssertFormat(O, False);
  ExpectValid(MustCompile(Draft7Schema, O), '"x"', True);
end;

procedure TestContentIsAssertedInDraft7Only;
const
  Schema: UTF8String = '{ "contentEncoding": "base64", "contentMediaType": "application/json" }';
var
  O: TJsonSchemaOptions;
  Draft7Validator, Json: IJsonSchemaValidator;
begin
  O := Options;
  WithDefaultDialect(O, Draft7);
  Draft7Validator := MustCompile(Schema, O);
  ExpectValid(Draft7Validator, '"eyJhIjogMX0="', True);
  ExpectValid(Draft7Validator, '"bm90IGpzb24="', False);
  ExpectValid(Draft7Validator, '"not base64"', False);
  ExpectValid(Draft7Validator, '12', True);
  ExpectAgreement(Draft7Validator, 'content', ['"eyJhIjogMX0="', '"bm90IGpzb24="', '"not base64"', '12']);
  Json := MustCompile('{ "contentMediaType": "application/json" }', O);
  WithAssertContent(O, False);
  ExpectValid(MustCompile(Schema, O), '"not base64"', True);
  ExpectValid(MustCompile(Schema), '"not base64"', True);
  ExpectValid(Json, '"[1, 2]"', True);
  ExpectValid(Json, '"[1, 2"', False);
end;

procedure TestEntryPointEvaluatesFromASubschema;
const
  Schema: UTF8String = '{ "$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" }';
var
  O: TJsonSchemaOptions;
  V: IJsonSchemaValidator;
  Raised: Boolean;
begin
  O := Options;
  WithEntryPoint(O, '#/$defs/positive');
  V := MustCompile(Schema, O);
  ExpectValid(V, '3', True);
  ExpectValid(V, '-3', False);
  ExpectValid(V, '"s"', False);
  O := Options;
  WithEntryPoint(O, '#/$defs/missing');
  Raised := False;
  try
    CompileJsonSchema(Schema, O);
  except
    on EJsonSchemaCompileError do
      Raised := True;
  end;
  Check(Raised, 'a missing entry point compiled');
end;

procedure TestBaseURIResolvesRelativeReferences;
var
  Item: TOneDocument;
  O: TJsonSchemaOptions;
  V, FromUri: IJsonSchemaValidator;
  Raised: Boolean;
begin
  Item.Uri := 'https://example.com/schemas/item.json';
  Item.Doc := ParseJson('{ "type": "integer" }');
  O := Options;
  WithBaseURI(O, 'https://example.com/schemas/root.json');
  WithDocumentResolver(O, OneDocumentResolver, @Item);
  V := MustCompile('{ "items": { "$ref": "item.json" } }', O);
  ExpectValid(V, '[1, 2]', True);
  ExpectValid(V, '[1, "2"]', False);
  O := Options;
  WithDocumentResolver(O, OneDocumentResolver, @Item);
  FromUri := CompileJsonSchemaFromUri('https://example.com/schemas/item.json', O);
  ExpectValid(FromUri, '1', True);
  ExpectValid(FromUri, '"1"', False);
  Raised := False;
  try
    CompileJsonSchemaFromUri('https://example.com/schemas/other.json', O);
  except
    on EJsonSchemaCompileError do
      Raised := True;
  end;
  Check(Raised, 'an unknown URI compiled');
end;

procedure TestDefaultDialectAppliesToSchemasWithoutSchema;
const
  Schema: UTF8String = '{ "items": [{ "type": "string" }], "additionalItems": false }';
var
  O: TJsonSchemaOptions;
begin
  O := Options;
  WithDefaultDialect(O, Draft7);
  ExpectValid(MustCompile(Schema, O), '["a", "b"]', False);
  { In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies. }
  ExpectValid(MustCompile(Schema), '["a", "b"]', True);
end;

{ 1: EJsonSchemaCompileError, 2: EJsonParseError, 3: another EJsonSchemaError, 0: no exception. }
function CompileOutcome(const Schema: UTF8String): Int32;
begin
  Result := 0;
  try
    CompileJsonSchema(Schema);
  except
    on EJsonSchemaCompileError do
      Result := 1;
    on EJsonParseError do
      Result := 2;
    on EJsonSchemaError do
      Result := 3;
  end;
end;

procedure TestCompilationErrors;
var
  Bytes: TBytes;
  Outcome: Int32;
begin
  Check(CompileOutcome('{ "$ref": "https://example.com/missing.json" }') = 1, 'an unresolvable reference');
  Check(CompileOutcome('{ "pattern": "a(" }') = 1, 'an invalid pattern');
  Check(CompileOutcome('{ "type": ') = 2, 'schema text that is not JSON');
  Bytes := BytesOf('{ "type": ');
  Outcome := 0;
  try
    CompileJsonSchema(Bytes);
  except
    on E: EJsonParseError do
      if (E.Offset = 10) and (E.Utf8Message = 'invalid JSON at offset 10: unexpected end of input') then
        Outcome := 2;
  end;
  Check(Outcome = 2, 'schema bytes that are not JSON');
end;

procedure TestInPlaceRecursionBeyondMaxDepthIsAnError;
const
  Schema: UTF8String = '{ "$defs": { "loop": { "allOf": [{ "$ref": "#/$defs/loop" }] } }, "$ref": "#/$defs/loop" }';
var
  O: TJsonSchemaOptions;
  V: IJsonSchemaValidator;
  Error: UTF8String;
  C: TJsonSchemaResults;
  Raised: Boolean;
  Bytes: TBytes;
begin
  Error := '';
  O := Options;
  WithMaxDepth(O, 16);
  V := MustCompile(Schema, O);
  Check(not V.TryIsValid('1', Error) and (Error = 'the schema recursed in place beyond the maximum depth'),
    'TryIsValid of a string: ' + Error);
  Check(not V.TryIsValid(ParseJson('1'), Error) and (Error <> ''), 'TryIsValid of a document: ' + Error);
  Bytes := BytesOf('1');
  Check(not V.TryIsValid(Bytes, 0, 1, Error) and (Error <> ''), 'TryIsValid of bytes: ' + Error);
  Check(not V.IsValid('1'), 'IsValid reports a runaway schema as valid');
  C := NewJsonSchemaResults(Detailed);
  Raised := False;
  try
    V.Evaluate('1', C);
  except
    on EJsonSchemaDepthError do
      Raised := True;
  end;
  Check(Raised, 'Evaluate of a runaway schema raised no EJsonSchemaDepthError');
  C := NewJsonSchemaResults(Detailed);
  Check(not V.TryEvaluate('1', C, Error) and (Error <> ''), 'TryEvaluate: ' + Error);
  { The evaluator that gave up is fit for the next validation. }
  Check(not V.TryIsValid('2', Error) and (Error <> ''), 'TryIsValid again: ' + Error);
end;

{ An evaluation that recursed beyond the maximum depth is not valid, whatever it came to, the same for a document
  and for text. Under a "not" the branch that was abandoned counts as false, which the not would turn into true. }
procedure TestIsValidAgreesForDocumentsAndTextBeyondMaxDepth;
var
  V: IJsonSchemaValidator;
  Error: UTF8String;
  Bytes: TBytes;
  Document, Text, FromBytes: Boolean;
begin
  Error := '';
  V := MustCompile('{ "$defs": { "loop": { "allOf": [{ "$ref": "#/$defs/loop" }] } }, "not": { "$ref": '
    + '"#/$defs/loop" } }');
  Bytes := BytesOf('1');
  Document := V.IsValid(ParseJson('1'));
  Text := V.IsValid('1');
  FromBytes := V.IsValid(Bytes, 0, 1);
  Check(not Document and not Text and not FromBytes, 'IsValid beyond the maximum depth: document '
    + BoolText(Document) + ', string ' + BoolText(Text) + ', bytes ' + BoolText(FromBytes));
  Check(not V.TryIsValid(ParseJson('1'), Error) and (Error <> ''), 'TryIsValid of a document under not');
  Check(not V.TryIsValid('1', Error) and (Error <> ''), 'TryIsValid of a string under not');
  Check(not V.TryIsValid(Bytes, 0, 1, Error) and (Error <> ''), 'TryIsValid of bytes under not');
end;

{ A not whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops at
  the maximum depth: the validation is an error, and IsValid reports the instance as invalid. (Evaluating not went
  around the depth guard, so the first of these schemas overflowed the stack, and for the others the not turned the
  abandoned evaluation's false into true.) }
procedure TestNotOnAnInPlaceCycleStopsAtMaxDepth;
const
  Looping: UTF8String = '{ "allOf": [{ "$ref": "#/$defs/loop" }] }';
  Instances: array[0..3] of UTF8String = ('1', '"a"', '{"a": 1}', '[1]');
var
  Schemas: array[0..8] of UTF8String;
  O: TJsonSchemaOptions;
  V: IJsonSchemaValidator;
  S, I, Level: Int32;
  Error, What: UTF8String;
  C: TJsonSchemaResults;
  Raised: Boolean;
begin
  Schemas[0] := '{ "not": { "$ref": "#" } }';
  Schemas[1] := '{ "not": { "not": { "$ref": "#" } } }';
  Schemas[2] := '{ "type": "integer", "not": { "$ref": "#" } }';
  Schemas[3] := '{ "allOf": [{ "not": { "$ref": "#" } }] }';
  Schemas[4] := '{ "$defs": { "a": { "not": { "$ref": "#/$defs/b" } }, "b": { "not": { "$ref": "#/$defs/a" } } }, '
    + '"$ref": "#/$defs/a" }';
  Schemas[5] := '{ "$defs": { "loop": ' + Looping + ' }, "not": { "$ref": "#/$defs/loop" } }';
  Schemas[6] := '{ "$defs": { "loop": ' + Looping + ' }, "not": { "not": { "$ref": "#/$defs/loop" } } }';
  Schemas[7] := '{ "$defs": { "loop": ' + Looping
    + ' }, "properties": { "a": { "not": { "$ref": "#/$defs/loop" } } } }';
  Schemas[8] := '{ "unevaluatedProperties": false, "not": { "$ref": "#" } }';
  O := Options;
  WithMaxDepth(O, 16);
  for S := 0 to High(Schemas) do begin
    V := MustCompile(Schemas[S], O);
    for I := 0 to High(Instances) do begin
      { Only an object with the property reaches the loop of the eighth schema, and anything but an integer fails
        the type of the third before its not is reached, when failing fast. }
      if ((S = 7) and (I <> 2)) or ((S = 2) and (I <> 0)) then
        Continue;
      What := Schemas[S] + ' with ' + Instances[I];
      Error := '';
      Check(not V.IsValid(Instances[I]), 'IsValid of a string: ' + What);
      Check(not V.IsValid(ParseJson(Instances[I])), 'IsValid of a document: ' + What);
      Check(not V.TryIsValid(Instances[I], Error) and (Error = 'the schema recursed in place beyond the maximum depth'),
        'TryIsValid of a string: ' + What + ': ' + Error);
      Check(not V.TryIsValid(ParseJson(Instances[I]), Error) and (Error <> ''), 'TryIsValid of a document: ' + What);
      for Level := 0 to 2 do begin
        case Level of
          0: C := NewJsonSchemaResults(Basic);
          1: C := NewJsonSchemaResults(Detailed);
        else
          C := NewJsonSchemaResults(Verbose);
        end;
        Raised := False;
        try
          V.Evaluate(ParseJson(Instances[I]), C);
        except
          on EJsonSchemaDepthError do
            Raised := True;
        end;
        Check(Raised, 'Evaluate raised no EJsonSchemaDepthError: ' + What);
      end;
    end;
  end;
end;

procedure TestNumbersAreComparedExactlyForMultipleOf;
var
  V: IJsonSchemaValidator;
begin
  V := MustCompile('{ "multipleOf": 0.01 }');
  ExpectValid(V, '0.07', True);
  ExpectValid(V, '19.99', True);
  ExpectValid(V, '0.075', False);
  ExpectValid(MustCompile('{ "multipleOf": 0.0001 }'), '0.0075', True);
end;

{ Threads. }
type
  TValidatingThread = class(TThread)
  private
    FValidator, FObjects: IJsonSchemaValidator;
    FIndex: Int32;
  protected
    procedure Execute; override;
  public
    Wrong: Boolean;
    constructor Create(const Validator, Objects: IJsonSchemaValidator; Index: Int32);
  end;

constructor TValidatingThread.Create(const Validator, Objects: IJsonSchemaValidator; Index: Int32);
begin
  FValidator := Validator;
  FObjects := Objects;
  FIndex := Index;
  Wrong := False;
  inherited Create(False);
end;

procedure TValidatingThread.Execute;
var
  Items, Valid, Invalid, GoodObject, BadObject: UTF8String;
  I: Int32;
  C: TJsonSchemaResults;
  Document: TJsonDocument;
begin
  try
    Items := '';
    for I := 0 to 99 do begin
      if I > 0 then
        Items := Items + ',';
      Items := Items + Number(FIndex * 1000 + I);
    end;
    Valid := '[' + Items + ']';
    Invalid := '[' + Items + ',-1]';
    GoodObject := '{"name": "thread' + Number(FIndex) + '", "tags": ["a", "b"], "x-' + Number(FIndex) + '": 1}';
    BadObject := '{"name": "Thread", "tags": ["a", "a"], "other": 1}';
    Document := ParseJson(GoodObject);
    for I := 0 to 199 do begin
      if not FValidator.IsValid(Valid) or FValidator.IsValid(Invalid) then
        Wrong := True;
      { Patterns, a dynamic scope's buffers, and the collector, whose first use digests the annotations. }
      if not FObjects.IsValid(GoodObject) or FObjects.IsValid(BadObject) or not FObjects.IsValid(Document) then
        Wrong := True;
      C := NewJsonSchemaResults(Verbose);
      if not FObjects.Evaluate(Document, C) or (Length(ResultAnnotations(C)) <> 1) then
        Wrong := True;
      C := NewJsonSchemaResults(Basic);
      if FObjects.Evaluate(BadObject, C) then
        Wrong := True;
    end;
  except
    Wrong := True;
  end;
  { A thread that has validated frees its buffers before it ends. }
  JsonSchemaReleaseThreadScratch;
end;

procedure TestValidatorsAreSafeForConcurrentUse;
var
  V, Objects: IJsonSchemaValidator;
  Threads: array[0..7] of TValidatingThread;
  G: Int32;
  Wrong: Boolean;
begin
  V := MustCompile('{ "type": "array", "items": { "type": "integer", "minimum": 0 }, "uniqueItems": true }');
  Objects := MustCompile('{'
    + '"title": "An object",'
    + '"properties": { "name": { "pattern": "^[a-z]+[0-9]*$" }, "tags": { "uniqueItems": true } },'
    + '"patternProperties": { "^x-[0-9]+$": { "type": "integer" } },'
    + '"unevaluatedProperties": false'
    + '}');
  for G := 0 to 7 do
    Threads[G] := TValidatingThread.Create(V, Objects, G);
  Wrong := False;
  for G := 0 to 7 do begin
    Threads[G].WaitFor;
    Wrong := Wrong or Threads[G].Wrong;
    Threads[G].Free;
  end;
  Check(not Wrong, 'a concurrent validation gave the wrong answer');
end;

function Shape(const Kind, Extra: UTF8String): UTF8String;
begin
  Result := '{ "type": "object", "properties": { "kind": { "const": "' + Kind + '" }, "' + Extra
    + '": { "type": "number" } }, "required": ["kind", "' + Extra + '"] }';
end;

procedure TestDiscriminatedOneOfAndAnyOfAgreeWithExhaustiveEvaluation;
const
  Keywords: array[0..1] of UTF8String = ('oneOf', 'anyOf');
var
  Branches: UTF8String;
  K: Int32;
begin
  Branches := '[' + Shape('circle', 'r') + ',' + Shape('square', 'side')
    + ', { "properties": { "kind": { "enum": [1, true] } } }]';
  for K := 0 to 1 do
    ExpectAgreement(MustCompile('{ "' + Keywords[K] + '": ' + Branches + ' }'), Keywords[K], [
      '{ "kind": "circle", "r": 1 }', '{ "kind": "circle", "side": 1 }', '{ "kind": "square", "side": 1 }',
      '{ "kind": "triangle" }', '{ "kind": 1.0 }', '{ "kind": true }', '{ "kind": false }', '{}', '"circle"']);
end;

function Branch(const Kind, Extra: UTF8String): UTF8String;
begin
  Result := '{ "type": "object", "properties": { "kind": { "const": ' + Kind + ' }, "' + Extra
    + '": { "type": "string" } }, "required": ["kind", "' + Extra + '"] }';
end;

procedure TestDiscriminatorsKeyNullAndNumbersByValue;
var
  V: IJsonSchemaValidator;
begin
  V := MustCompile('{ "oneOf": [' + Branch('null', 'a') + ',' + Branch('1', 'b') + ',' + Branch('1.0', 'c') + ','
    + Branch('"x"', 'd') + '] }');
  ExpectAgreement(V, 'discriminator', [
    '{ "kind": null, "a": "s" }', '{ "kind": null, "b": "s" }', '{ "kind": 1, "b": "s" }',
    '{ "kind": 1, "b": "s", "c": "t" }', '{ "kind": 1.0, "c": "t" }', '{ "kind": "x", "d": "s" }',
    '{ "kind": "y", "d": "s" }', '{ "d": "s" }']);
  { Both numeric branches match 1 when it has both properties: oneOf fails. }
  ExpectValid(V, '{ "kind": 1, "b": "s", "c": "t" }', False);
end;

procedure TestArraysOfSimpleArraysMatchTheGeneralPath;
const
  Position: UTF8String = '{ "type": "array", "minItems": 2, "maxItems": 3, "items": { "type": "number" } }';
var
  Schemas: array[0..3] of UTF8String;
  I: Int32;
begin
  Schemas[0] := '{ "type": "array", "items": ' + Position + ' }';
  Schemas[1] := '{ "type": "array", "items": { "type": ["array", "string"], "minItems": 2, "items": { "type": '
    + '"integer" } } }';
  Schemas[2] := '{ "type": "array", "items": { "type": "object", "minItems": 2 } }';
  Schemas[3] := '{ "type": "array", "items": { "type": "array", "items": { "type": "array", "items": { "type": '
    + '"number" } } } }';
  for I := 0 to 3 do
    ExpectAgreement(MustCompile(Schemas[I]), Schemas[I], [
      '[]', '[[1, 2]]', '[[1, 2], [3, 4, 5]]', '[[1]]', '[[1, 2, 3, 4]]', '[[1, "a"]]', '[[1.5, 2]]',
      '[["x", "y"]]', '["s", [1, 2]]', '[{}, [1, 2]]', '[[[1, 2]], [[3]]]', '[[[1, "a"]]]']);
end;

{ RootBody is the body of the plan of a validator's root, or nil. }
function RootBody(const V: IJsonSchemaValidator): PBody;
var
  P: PProgram;
begin
  P := V.CompiledProgram;
  Result := nil;
  if P^.Plans[P^.Root].HasBody then
    Result := @P^.Plans[P^.Root].Body;
end;

procedure TestFusedNotRequiredAndAbsentPatternConditionsMatchTheGeneralPath;
const
  Extensions: UTF8String = '{ "patternProperties": { "^x-": true } }';
var
  Schemas: array[0..2] of UTF8String;
  I: Int32;
  V: IJsonSchemaValidator;
  B: PBody;
begin
  (* not: {required} alongside unevaluatedProperties (the OpenAPI example object). *)
  Schemas[0] := '{'
    + '"type": "object",'
    + '"properties": { "value": true, "externalValue": { "type": "string" }, "summary": { "type": "string" } },'
    + '"not": { "required": ["value", "externalValue"] },'
    + '"$ref": "#/$defs/ext",'
    + '"unevaluatedProperties": false,'
    + '"$defs": { "ext": ' + Extensions + ' }'
    + '}';
  { An if deciding on no name matching a pattern (the OpenAPI responses object). }
  Schemas[1] := '{'
    + '"type": "object",'
    + '"properties": { "default": { "type": "integer" } },'
    + '"patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": { "type": "integer" } },'
    + '"$ref": "#/$defs/ext",'
    + '"unevaluatedProperties": false,'
    + '"if": { "patternProperties": { "^[1-5](?:[0-9]{2}|XX)$": false } },'
    + '"then": { "required": ["default"] },'
    + '"$defs": { "ext": ' + Extensions + ' }'
    + '}';
  { Both, gated by another condition, with a name the pattern also matches. }
  Schemas[2] := '{'
    + '"type": "object",'
    + '"properties": { "kind": true, "a": true, "b": true, "x-a": true },'
    + '"allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": true } }],'
    + '"if": { "properties": { "kind": { "const": "k" } }, "required": ["kind"] },'
    + '"then": {'
    + '"not": { "required": ["a", "b"] },'
    + '"if": { "patternProperties": { "^x-": false } },'
    + '"then": { "required": ["c"] },'
    + '"else": { "properties": { "d": true } }'
    + '},'
    + '"unevaluatedProperties": false,'
    + '"$defs": { "ext": ' + Extensions + ' }'
    + '}';
  for I := 0 to 2 do begin
    V := MustCompile(Schemas[I]);
    B := RootBody(V);
    Check((B <> nil) and B^.HasFused, 'the schema did not take a fused plan: ' + Schemas[I]);
    ExpectAgreement(V, Schemas[I], [
      '{}', '{ "value": 1 }', '{ "value": 1, "externalValue": "u" }', '{ "externalValue": 2 }',
      '{ "x-y": 1, "summary": "s" }', '{ "other": 1 }', '{ "default": 1 }', '{ "200": 1 }',
      '{ "2XX": 1, "x-a": 1 }', '{ "600": 1 }', '{ "default": "a", "404": 1 }', '{ "kind": "k" }',
      '{ "kind": "k", "c": 1 }', '{ "kind": "k", "a": 1, "b": 1, "c": 1 }', '{ "kind": "k", "a": 1, "c": 1 }',
      '{ "kind": "k", "x-a": 1, "d": 1 }', '{ "kind": "k", "x-z": 1, "d": 1 }', '{ "kind": "k", "d": 1, "c": 1 }',
      '{ "kind": "j", "a": 1, "b": 1 }']);
  end;
end;

procedure TestFlatFusedObjectsMatchTheGeneralPath;
const
  Shared: UTF8String = '{ "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], '
    + '"maxProperties": 3 }';
var
  Schemas: array[0..2] of UTF8String;
  I: Int32;
  V: IJsonSchemaValidator;
  B: PBody;
begin
  Schemas[0] := '{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "c": { "type": "integer" } }, "$defs": '
    + '{ "s": ' + Shared + ' } }';
  Schemas[1] := '{'
    + '"allOf": [{ "$ref": "#/$defs/s" }, { "properties": { "a": { "type": "string" } }, "required": ["c"] }],'
    + '"properties": { "c": { "type": "integer" } },'
    + '"minProperties": 2,'
    + '"$defs": { "s": ' + Shared + ' }'
    + '}';
  { The same name with different schemas stays a fused plan. }
  Schemas[2] := '{ "allOf": [{ "$ref": "#/$defs/s" }], "properties": { "a": { "minLength": 2 } }, "$defs": { "s": '
    + Shared + ' } }';
  for I := 0 to 2 do begin
    V := MustCompile(Schemas[I]);
    B := RootBody(V);
    Check((B <> nil) and B^.HasFused and (B^.Fused.HasFlat = (I < 2)), 'schema ' + Number(I) + ': unexpected plan');
    ExpectAgreement(V, Schemas[I], [
      '{}', '{ "a": "x" }', '{ "a": "xy", "c": 1 }', '{ "a": 1, "c": 1 }', '{ "a": "x", "c": "1" }',
      '{ "a": "x", "b": null, "c": 1 }', '{ "a": "x", "b": 1, "c": 1, "d": 1 }', '{ "c": 1 }', '[]']);
  end;
end;

procedure TestFusedObjectsBelowADynamicReferenceMatchTheGeneralPath;
var
  V: IJsonSchemaValidator;
begin
  { The items' $dynamicRef resolves (through the scope) to "strict", whose allOf contributor is in its own
    resource. }
  V := MustCompile('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"$id": "https://example.com/root",'
    + '"$ref": "strict",'
    + '"$defs": {'
    + '"strict": {'
    + '"$id": "https://example.com/strict",'
    + '"$dynamicAnchor": "node",'
    + '"type": "object",'
    + '"properties": { "data": true, "y": true, "children": { "type": "array", "items": { "$ref": '
    + '"tree#/$defs/kids" } } },'
    + '"allOf": [{ "$ref": "#/$defs/extra" }],'
    + '"unevaluatedProperties": false,'
    + '"$defs": { "extra": { "properties": { "x": { "type": "integer" } } } }'
    + '},'
    + '"tree": {'
    + '"$id": "https://example.com/tree",'
    + '"$dynamicAnchor": "node",'
    + '"type": "object",'
    + '"$defs": { "kids": { "$dynamicRef": "#node" } }'
    + '}'
    + '}'
    + '}');
  ExpectAgreement(V, 'dynamic', [
    '{}', '{ "data": 1, "x": 2 }', '{ "x": "a" }', '{ "y": 1, "children": [{ "y": 1 }] }',
    '{ "children": [{ "z": 1 }] }', '{ "children": [{ "x": 1, "children": [{ "y": 2, "data": 3 }] }] }',
    '{ "children": [{ "children": [{ "x": "no" }] }] }', '{ "z": 1 }']);
  ExpectValid(V, '{ "children": [{ "x": 1, "children": [{ "y": 2 }] }] }', True);
  ExpectValid(V, '{ "children": [{ "z": 1 }] }', False);
end;

{ Small objects are decided by looking the few declared and required names up in them, large ones by visiting their
  properties. Both agree, top-level and nested. }
procedure TestFewNamesAreLookedUpInSmallAndLargeObjects;
const
  Pads: array[0..3] of Int32 = (0, 3, 40, 300);
var
  V: IJsonSchemaValidator;
  K, I: Int32;
  P: UTF8String;
begin
  V := MustCompile('{'
    + '"properties": { "a": { "type": "string" }, "n": { "properties": { "x": { "type": "integer" } } } },'
    + '"required": ["b"]'
    + '}');
  for K := 0 to 3 do begin
    P := '';
    for I := 0 to Pads[K] - 1 do
      P := P + ',"p' + Number(I) + '": ' + Number(I);
    ExpectValid(V, '{"b": 1, "a": "x"' + P + '}', True);
    ExpectValid(V, '{"a": "x"' + P + '}', False);
    ExpectValid(V, '{"a": 1, "b": 1' + P + '}', False);
    ExpectValid(V, '{"b": null' + P + ', "n": {"x": 2' + P + '}}', True);
    ExpectValid(V, '{"b": null' + P + ', "n": {"x": "2"' + P + '}}', False);
  end;
end;

{ JSON text is validated in place. Invalid JSON and runaway recursion are told apart. }
procedure TestValidatesJSONText;
var
  V: IJsonSchemaValidator;
  Error: UTF8String;
  Bytes, Padded: TBytes;
  Offset, I: Int32;
  Raised, Found: Boolean;
  C: TJsonSchemaResults;
  Rows: TJsonSchemaResultArray;
begin
  V := MustCompile('{ "type": "array", "items": { "type": "integer" } }');
  Check(V.TryIsValid('[1, 2, 3]', Error) and (Error = ''), '[1, 2, 3]: ' + Error);
  Bytes := BytesOf('[1, "2"]');
  Check(not V.TryIsValid(Bytes, 0, Length(Bytes), Error) and (Error = ''), '[1, "2"]: ' + Error);
  Check(not V.TryIsValid('[1, 2', Error) and (Error = 'invalid JSON at offset 5: unexpected end of input'),
    '[1, 2: ' + Error);
  Offset := -1;
  try
    V.IsValid('[1, 2');
  except
    on E: EJsonParseError do
      Offset := E.Offset;
  end;
  Check(Offset = 5, 'IsValid of [1, 2 raised no EJsonParseError at offset 5');
  Raised := False;
  try
    V.IsValid(nil, 0, 0);
  except
    on EJsonParseError do
      Raised := True;
  end;
  Check(Raised and not V.TryIsValid(nil, 0, 0, Error) and (Error <> ''), 'no text is valid');
  { Part of an array of bytes, and a range that is not within the array, which reads no other memory. }
  Padded := BytesOf('xx[1, 2]]]');
  Check(V.IsValid(Padded, 2, 6), 'part of an array of bytes');
  Check(not V.TryIsValid(Padded, 2, 5, Error) and (Error <> ''), 'a part that ends early: ' + Error);
  Check(not V.TryIsValid(Padded, 2, 7, Error) and (Error <> ''), 'a part that ends late: ' + Error);
  Check(not V.TryIsValid(Padded, 2, 1000, Error) and (Error <> ''), 'a length beyond the array: ' + Error);
  Check(not V.TryIsValid(Padded, 50, 6, Error) and (Error <> ''), 'a start beyond the array: ' + Error);
  Check(not V.TryIsValid(Padded, -1, 6, Error) and (Error <> ''), 'a negative start: ' + Error);
  C := NewJsonSchemaResults(Detailed);
  Check(not V.TryEvaluate('["x"]', C, Error) and (Error = ''), '["x"]: ' + Error);
  Found := False;
  Rows := ResultRows(C);
  for I := 0 to Length(Rows) - 1 do
    Found := Found or (not Rows[I].IsMatch and (Rows[I].DocumentEvaluationLocation = '/0'));
  Check(Found, 'no failure at /0');
  Raised := False;
  try
    V.Evaluate('[', C);
  except
    on EJsonParseError do
      Raised := True;
  end;
  Check(Raised and not V.TryEvaluate('[', C, Error) and (Error <> ''), 'Evaluate of text that is not JSON');
  C := NewJsonSchemaResults(Basic);
  Check(V.Evaluate(Padded, 2, 6, C) and (ResultCount(C) = 1), 'Evaluate of part of an array of bytes');
end;

{ A format callback that validates JSON text itself, during a validation of JSON text on the same thread, gets
  buffers of its own. }
var
  InnerValidator, OuterValidator: IJsonSchemaValidator;

function EmbeddedJson(const Text: TBytes; Start, Len: Int32): Boolean;
var
  Error: UTF8String;
begin
  { The same validator too: an evaluation nested in its own callback. }
  Result := InnerValidator.TryIsValid(Text, Start, Len, Error) and OuterValidator.IsValid('[]');
end;

procedure TestValidatingJSONFromAFormatCallbackWorks;
var
  O: TJsonSchemaOptions;
begin
  InnerValidator := MustCompile('{ "type": "object", "required": ["a"] }');
  O := Options;
  WithAssertFormat(O, True);
  WithFormat(O, 'embedded-json', EmbeddedJson);
  OuterValidator := MustCompile('{ "type": "array", "items": { "format": "embedded-json" } }', O);
  ExpectValid(OuterValidator, '["{\"a\": 1}", "{\"a\": 2}"]', True);
  ExpectValid(OuterValidator, '["{\"a\": 1}", "{\"b\": 2}"]', False);
  ExpectValid(OuterValidator, '["{\"a\": 1}", "not json"]', False);
  ExpectAgreement(OuterValidator, 'embedded-json', ['["{\"a\": 1}"]', '["{\"b\": 1}"]', '["x"]', '[]']);
  InnerValidator := nil;
  OuterValidator := nil;
end;

{ The same, where the schema that calls back keeps a dynamic scope and tracks evaluated properties, so the nested
  evaluation shares the thread's buffers with the one it runs inside. }
function EmbeddedTree(const Text: TBytes; Start, Len: Int32): Boolean;
var
  Error: UTF8String;
begin
  Result := InnerValidator.TryIsValid(Text, Start, Len, Error);
end;

procedure TestNestedEvaluationsShareTheThreadsBuffers;
const
  Tree: UTF8String = '{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"$id": "https://example.com/strict-tree",'
    + '"$dynamicAnchor": "node",'
    + '"$ref": "tree",'
    + '"unevaluatedProperties": false,'
    + '"$defs": {'
    + '"tree": {'
    + '"$id": "https://example.com/tree",'
    + '"$dynamicAnchor": "node",'
    + '"type": "object",'
    + '"properties": { "data": { "format": "tree" }, "children": { "type": "array", "items": { "$dynamicRef": '
    + '"#node" } } }'
    + '}'
    + '}'
    + '}';
  Good: UTF8String = '{"children": [{"data": "{\"children\": [{\"data\": \"{}\"}, {}]}", "children": [{"data": '
    + '"{\"data\": \"{\\\"children\\\": []}\"}"}]}, {"data": "{}"}]}';
  Bad: UTF8String = '{"children": [{"data": "{\"children\": [{\"data\": \"{}\"}, {}]}", "children": [{"data": '
    + '"{\"data\": \"{\\\"childre\\\": []}\"}"}]}, {"data": "{}"}]}';
  BadOutside: UTF8String = '{"children": [{"data": "{\"children\": [{\"data\": \"{}\"}, {}]}", "children": '
    + '[{"data": "{}"}]}, {"daat": "{}"}]}';
var
  O: TJsonSchemaOptions;
begin
  O := Options;
  WithAssertFormat(O, True);
  WithFormat(O, 'tree', EmbeddedTree);
  InnerValidator := MustCompile(Tree, O);
  ExpectValid(InnerValidator, Good, True);
  ExpectValid(InnerValidator, Bad, False);
  ExpectValid(InnerValidator, BadOutside, False);
  ExpectAgreement(InnerValidator, 'nested trees', [Good, Bad, BadOutside]);
  InnerValidator := nil;
end;

{ A number in a schema and the same number in an instance are the same float64, however it is written. }
procedure TestSchemaAndInstanceNumbersParseAlike;
const
  Keywords: array[0..1] of UTF8String = ('exclusiveMaximum', 'exclusiveMinimum');
  Literals: array[0..1] of UTF8String = ('972783798187987123879878123.18878137',
    '-972783798187987123879878123.18878137');
  Shortest: array[0..1] of UTF8String = ('9.727837981879871e+26', '-9.727837981879871e+26');
var
  K, T: Int32;
  Text: UTF8String;
  V: IJsonSchemaValidator;
begin
  for K := 0 to 1 do
    for T := 0 to 1 do begin
      Text := Literals[K];
      if T = 1 then
        Text := Shortest[K];
      V := MustCompile('{"' + Keywords[K] + '": ' + Text + '}');
      ExpectValid(V, Text, False);
      ExpectValid(V, Literals[K], False);
    end;
end;

procedure TestUnevaluatedItemsWithAStaticPrefixMatchTheGeneralPath;
const
  Schemas: array[0..5] of UTF8String = (
    '{ "prefixItems": [{ "type": "integer" }], "unevaluatedItems": { "type": "string" } }',
    '{ "allOf": [{ "prefixItems": [true, { "type": "integer" }] }], "prefixItems": [{ "type": "integer" }], '
      + '"unevaluatedItems": false }',
    '{ "allOf": [{ "items": { "type": "integer" } }], "unevaluatedItems": false }',
    '{ "anyOf": [{ "prefixItems": [true, true] }, { "prefixItems": [{ "type": "integer" }] }], '
      + '"unevaluatedItems": false }',
    '{ "contains": { "type": "integer" }, "unevaluatedItems": { "type": "string" } }',
    '{ "if": { "prefixItems": [{ "const": 1 }] }, "then": { "prefixItems": [true, true] }, "unevaluatedItems": '
      + 'false }');
var
  I: Int32;
begin
  for I := 0 to 5 do
    ExpectAgreement(MustCompile(Schemas[I]), Schemas[I], [
      '[]', '[1]', '[1, 2]', '[1, "a"]', '["a"]', '[1, 2, 3]', '[1, "a", "b"]', '["a", 1, "b"]', '{}', '[2, 2]']);
end;

procedure TestUniqueItemsOverLargeArrays;
var
  V: IJsonSchemaValidator;
  Items: UTF8String;
  I: Int32;
begin
  V := MustCompile('{ "uniqueItems": true }');
  Items := '';
  for I := 0 to 199 do begin
    if I > 0 then
      Items := Items + ',';
    Items := Items + '{"id": ' + Number(I) + ', "tags": ["a", ' + Number(I mod 3) + ']}';
  end;
  ExpectValid(V, '[' + Items + ']', True);
  ExpectValid(V, '[' + Items + ',{"tags": ["a", 1.0], "id": 4e0}]', False);
end;

{ --------------------------------------------------------------------------------------------------------------------
  plan_test.go }

procedure TestMeetTreatsIntegersAsNumbers;
const
  A: array[0..3] of Byte = (TypeInteger, TypeNumber, AnyType, TypeString);
  B: array[0..3] of Byte = (TypeNumber, TypeNumber or TypeString, TypeString or TypeArray, TypeInteger);
  Want: array[0..3] of Byte = (TypeInteger, TypeNumber or TypeInteger, TypeString or TypeArray, 0);
  Instances: array[0..2] of UTF8String = ('1', '1.5', '"a"');
  Left: array[0..3] of Byte = (TypeInteger, TypeNumber, TypeNumber or TypeString, AnyType);
  Right: array[0..3] of Byte = (TypeNumber, TypeInteger, TypeInteger or TypeString, TypeNumber);
var
  I, J: Int32;
  D: TJsonDocument;
begin
  for I := 0 to 3 do
    Check(MeetTypes(A[I], B[I]) = Want[I], 'meet(' + Number(A[I]) + ', ' + Number(B[I]) + ') = '
      + Number(MeetTypes(A[I], B[I])) + ', want ' + Number(Want[I]));
  for I := 0 to 2 do begin
    D := ParseJson(Instances[I]);
    for J := 0 to 3 do
      Check(TypeOK(MeetTypes(Left[J], Right[J]), D, D.Root) = (TypeOK(Left[J], D, D.Root)
        and TypeOK(Right[J], D, D.Root)), Number(Left[J]) + ' and ' + Number(Right[J]) + ' on ' + Instances[I]);
  end;
end;

{ A plan of a few names looks each one up in a small object (the lookup of an object plan): every name is found by
  its length and word, the text deciding beyond eight bytes, and a name that is not there is not. }
procedure TestPropertyLookupByName;
const
  Instance: UTF8String = '{"a": 1, "ab": 2, "abcdefgh": 3, "abcdefghi": 4, "abcdefghj": 5, "' + #$C3#$A9
    + '": 6, "": 7, "a\nb": 8}';
  { The names as JSON text, and as they are. }
  Quoted: array[0..7] of UTF8String = ('"a"', '"ab"', '"abcdefgh"', '"abcdefghi"', '"abcdefghj"',
    '"' + #$C3#$A9 + '"', '""', '"a\nb"');
  Names: array[0..7] of UTF8String = ('a', 'ab', 'abcdefgh', 'abcdefghi', 'abcdefghj', #$C3#$A9, '', 'a' + #10 + 'b');
  Misses: array[0..5] of UTF8String = ('b', 'ba', 'abcdefgi', 'abcdefghk', 'abcdefgh ', #$C3#$A8);
var
  D: TJsonDocument;
  I, K, Value: Int32;
  Schema, Constant: UTF8String;
  V: IJsonSchemaValidator;
  P: PProgram;
begin
  D := ParseJson(Instance);
  for I := 0 to 7 do begin
    Value := DocProperty(D, D.Root, Names[I]);
    Check((Value >= 0) and (DocData(D, Value) = UInt64(I + 1)), 'property ' + Quoted[I]);
    for K := 0 to 1 do begin
      Constant := Number(I + 1);
      if K = 1 then
        Constant := '0';
      Schema := '{"properties": {' + Quoted[I] + ': {"const": ' + Constant + '}}, "required": [' + Quoted[I] + ']}';
      V := MustCompile(Schema);
      P := V.CompiledProgram;
      Check(P^.Plans[P^.Entry].HasBody and P^.Plans[P^.Entry].Body.HasObject
        and P^.Plans[P^.Entry].Body.ObjectPlan.Lookup, Schema + ': not a lookup plan');
      Check(V.IsValid(D) = (K = 0), Schema);
    end;
  end;
  for I := 0 to 5 do begin
    Check(DocProperty(D, D.Root, Misses[I]) = -1, 'property "' + Misses[I] + '" found');
    { Found, the property would fail its schema. Required, it is missed. }
    Schema := '{"properties": {"' + Misses[I] + '": false}}';
    Check(MustCompile(Schema).IsValid(D), Schema);
    Schema := '{"properties": {"' + Misses[I] + '": true}, "required": ["' + Misses[I] + '"]}';
    Check(not MustCompile(Schema).IsValid(D), Schema);
  end;
end;

{ The Go test calls lengthOK, which here is not part of a unit's interface: the length keywords are evaluated
  through a validator, fail-fast (the plans' LengthOK) and collecting (the general evaluator's count). }
procedure TestLengthOKAgreesWithCounting;
const
  Strings: array[0..6] of UTF8String = ('', 'a', 'abcd', #$C3#$A9, #$C3#$A9#$C3#$A9,
    #$F0#$9F#$98#$80#$F0#$9F#$98#$80, 'a' + #$F0#$9F#$98#$80 + 'b');
  Chars: array[0..6] of Int32 = (0, 1, 4, 1, 2, 2, 3);
var
  S, Min, Max: Int32;
  V: IJsonSchemaValidator;
  D: TJsonDocument;
  C: TJsonSchemaResults;
  Want: Boolean;
begin
  for Min := 0 to 5 do
    for Max := 0 to 5 do begin
      V := MustCompile('{"minLength": ' + Number(Min) + ', "maxLength": ' + Number(Max) + '}');
      for S := 0 to 6 do begin
        D := ParseJson('"' + Strings[S] + '"');
        Want := (Chars[S] >= Min) and (Chars[S] <= Max);
        C := NewJsonSchemaResults(Basic);
        Check((V.IsValid(D) = Want) and (V.Evaluate(D, C) = Want), '"' + Strings[S] + '" in [' + Number(Min) + ', '
          + Number(Max) + ']');
      end;
    end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  document_test.go: what TestDocument does not have }

procedure TestParseRoundTrips;
const
  Texts: array[0..15] of UTF8String = ('null', 'true', 'false', '0', '-0', '1.5e3', '""', '"a"', '[]', '{}',
    '[1,[2,[3]],{"a":null}]', '{"a":1,"b":[true,false],"c":{"d":"e"}}',
    '"caf' + #$C3#$A9 + ' \n \"q\" \\ ' + #$F0#$9F#$98#$80 + '"', '18446744073709551615', '-9223372036854775808',
    '123456789012345678901234567890');
var
  I: Int32;
  D, Again: TJsonDocument;
  Same: IJsonSchemaValidator;
begin
  for I := 0 to High(Texts) do begin
    D := ParseJson(Texts[I]);
    Again := ParseJson(JsonText(D));
    { The value read back equals the value: it is the const of a schema the first is valid against. }
    Same := MustCompile('{"const": ' + JsonText(Again) + '}');
    Check(Same.IsValid(D), Texts[I] + ' did not round trip: ' + JsonText(D));
  end;
  Check(JsonText(ParseJson(' { "a" : [ 1 , 2 ] } ')) = '{"a":[1,2]}', 'compact text');
  Check(JsonText(ParseJson('"\u0001\t\/"')) = '"\u0001\t/"', 'escapes: ' + JsonText(ParseJson('"\u0001\t\/"')));
end;

procedure TestParseRejectsInvalidJSON;
const
  Texts: array[0..31] of UTF8String = ('', ' ', '{', '[', '[1,]', '{"a":1,}', '{"a"}', '{a:1}', '01', '1.', '.5',
    '-', '1e', '+1', 'tru', 'nul', '"abc', '"\x"', '"\u12"', '"\ud800"', '"\udc00"', '"\ud800A"',
    '"a' + #10 + 'b"', '1 2', '[1] x', '1e999', '"' + #$FF + '"', '"' + #$C0#$80 + '"', '"' + #$ED#$A0#$80 + '"',
    '{"a":1 "b":2}', '[1 2]', ']');
var
  I, Offset: Int32;
  D: TJsonDocument;
  Error: UTF8String;
  Parser: TParser;
  Bytes: TBytes;
  Raised: Boolean;
begin
  Parser := Default(TParser);
  for I := 0 to High(Texts) do begin
    Check(not TryParseJson(Texts[I], D, Error, Offset) and (Error <> ''), Texts[I] + ' parsed');
    Bytes := BytesOf(Texts[I]);
    Check(not ParserIsValid(Parser, Bytes, Length(Bytes)), Texts[I] + ' is valid to the syntax check');
    Raised := False;
    try
      ParseJson(Texts[I]);
    except
      on EJsonParseError do
        Raised := True;
    end;
    Check(Raised, Texts[I] + ' raised no EJsonParseError');
  end;
  Check(not TryParseJson('[1, x]', D, Error, Offset) and (Offset = 4)
    and (Error = 'invalid JSON at offset 4: expected a value'), '[1, x]: ' + Error);
end;

procedure TestDuplicateKeysKeepTheLastValueAtTheFirstPosition;
var
  Text: UTF8String;
  I: Int32;
  D: TJsonDocument;
begin
  Check(JsonText(ParseJson('{"a":1,"b":2,"a":3,"c":4,"b":5}')) = '{"a":3,"b":5,"c":4}', 'small object');
  Text := '{';
  for I := 0 to 39 do
    Text := Text + '"k' + Number(I) + '":' + Number(I) + ',';
  Text := Text + '"k7":"seven","k3":"three"}';
  D := ParseJson(Text);
  Check(DocCount(D, D.Root) = 40, 'count ' + Number(DocCount(D, D.Root)));
  Check(DocStrCopy(D, DocProperty(D, D.Root, 'k7')) = 'seven', 'k7');
  Check(DocStrCopy(D, DocProperty(D, D.Root, 'k3')) = 'three', 'k3');
  Check(DocStrCopy(D, DocFirst(D, D.Root) + 2 * 7) = 'k7', 'position 7 holds '
    + DocStrCopy(D, DocFirst(D, D.Root) + 2 * 7));
end;

procedure TestStrHashReadsEveryByte;
var
  Base, Changed: TBytes;
  N, I: Int32;
  H: UInt64;
begin
  Base := BytesOf('abcdefghijklmnopqrstuvwxyz');
  for N := 0 to Length(Base) do begin
    H := StrHash(Base, 0, N);
    for I := 0 to N - 1 do begin
      Changed := Copy(Base, 0, N);
      Changed[I] := Changed[I] xor 1;
      Check(StrHash(Changed, 0, N) <> H, 'length ' + Number(N) + ': byte ' + Number(I)
        + ' does not affect the hash');
    end;
  end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  example_test.go: each example, with the output the Go example gives }

var
  Output: UTF8String;

procedure Print(const Line: UTF8String);
begin
  Output := Output + Line + #10;
end;

procedure ExpectOutput(const Name, Want: UTF8String);
begin
  Check(Output = Want, Name + ' printed:' + #10 + Output + 'want:' + #10 + Want);
  Output := '';
end;

procedure ExampleCompileJsonSchema;
var
  Validator: IJsonSchemaValidator;
begin
  Validator := CompileJsonSchema('{'
    + '"type": "object",'
    + '"properties": { "id": { "type": "integer", "minimum": 1 } },'
    + '"required": ["id"]'
    + '}');
  Print(BoolText(Validator.IsValid('{"id": 3}')));
  Print(BoolText(Validator.IsValid('{"id": 0}')));
  ExpectOutput('CompileJsonSchema', 'true' + #10 + 'false' + #10);
end;

procedure ExampleEvaluate;
var
  Validator: IJsonSchemaValidator;
  Instance: TJsonDocument;
  Collector: TJsonSchemaResults;
  Rows: TJsonSchemaResultArray;
  I: Int32;
begin
  Validator := CompileJsonSchema('{"properties": {"id": {"type": "integer"}}, "required": ["name"]}');
  Instance := ParseJson('{"id": "seven"}');
  Collector := NewJsonSchemaResults(Detailed);
  Print(BoolText(Validator.Evaluate(Instance, Collector)));
  Rows := ResultRows(Collector);
  for I := 0 to Length(Rows) - 1 do
    if (Rows[I].EvaluationLocation <> '') and (Rows[I].Message <> '') then
      Print(Rows[I].EvaluationLocation + ' at "' + Rows[I].DocumentEvaluationLocation + '": ' + Rows[I].Message);
  ExpectOutput('Evaluate', 'false' + #10
    + '/properties/id at "/id": The value was expected to match the subschema.' + #10
    + '/properties/id/type at "/id": The value was expected to be of type ''integer''' + #10
    + '/required at "/name": Required property not present ''name''' + #10);
end;

procedure ExampleParseJson;
var
  Validator: IJsonSchemaValidator;
  Document: TJsonDocument;
  Text: TBytes;
begin
  Validator := CompileJsonSchema('{"type": "array", "items": {"type": "integer"}}');
  { Parse once, validate any number of times. }
  Document := ParseJson(BytesOf('[1, 2, 3]'));
  Print(BoolText(Validator.IsValid(Document)));
  { JSON text is parsed into buffers the thread reuses. }
  Text := BytesOf('[1, "two"]');
  Print(BoolText(Validator.IsValid(Text, 0, Length(Text))));
  ExpectOutput('ParseJson', 'true' + #10 + 'false' + #10);
end;

procedure ExampleTryIsValid;
var
  Validator: IJsonSchemaValidator;
  Valid: Boolean;
  Error: UTF8String;
begin
  Validator := CompileJsonSchema('{"type": "object"}');
  Valid := Validator.TryIsValid('{"id": 3', Error);
  Print(BoolText(Valid) + ' ' + Error);
  { IsValid raises EJsonParseError for text that is not JSON. }
  try
    Validator.IsValid('{"id": 3');
  except
    on E: EJsonParseError do
      Print(E.Utf8Message);
  end;
  ExpectOutput('TryIsValid', 'false invalid JSON at offset 8: unexpected end of input' + #10
    + 'invalid JSON at offset 8: unexpected end of input' + #10);
end;

function Even(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (Start >= 0) and (Start + Len <= Length(Text)) and (Len mod 2 = 0);
end;

procedure ExampleOptions;
var
  Item: TOneDocument;
  O: TJsonSchemaOptions;
  Validator: IJsonSchemaValidator;
begin
  Item.Uri := 'https://example.com/item.json';
  Item.Doc := ParseJson('{"type": "string", "format": "even"}');
  O := DefaultJsonSchemaOptions;
  { For schemas without $schema (default 2020-12). }
  WithDefaultDialect(O, Draft201909);
  { True or False. Without it the vocabularies decide. }
  WithAssertFormat(O, True);
  WithFormat(O, 'even', Even);
  WithDocumentResolver(O, OneDocumentResolver, @Item);
  WithBaseURI(O, 'https://example.com/root.json');
  WithEntryPoint(O, '#/$defs/item');
  WithMaxDepth(O, 128);
  Validator := CompileJsonSchema('{"$defs": {"item": {"$ref": "item.json"}}}', O);
  Print(BoolText(Validator.IsValid('"four"')));
  Print(BoolText(Validator.IsValid('"three"')));
  ExpectOutput('options', 'true' + #10 + 'false' + #10);
end;

procedure ExampleCollectedAnnotations;
var
  Validator: IJsonSchemaValidator;
  Collector: TJsonSchemaResults;
  Annotations: TJsonSchemaCollectedAnnotationArray;
  Value, Error: UTF8String;
begin
  Error := '';
  Validator := CompileJsonSchema('{'
    + '"title": "Person",'
    + '"properties": { "name": { "title": "Name", "type": "string" } }'
    + '}');
  Collector := NewJsonSchemaResults(Verbose);
  Print(BoolText(Validator.TryEvaluate('{"name": "Ada"}', Collector, Error)) + ' ' + Error);
  { Instance location, then keyword, then schema location, then the value as JSON text. }
  Annotations := CollectedAnnotations(Collector);
  if FindAnnotation(Annotations, '', 'title', '#', Value) then
    Print(Value);
  if FindAnnotation(Annotations, '/name', 'title', '#/properties/name', Value) then
    Print(Value);
  ExpectOutput('CollectedAnnotations', 'true ' + #10 + '"Person"' + #10 + '"Name"' + #10);
end;

procedure ExampleErrors;
begin
  try
    CompileJsonSchema('{"$ref": "#/$defs/missing"}');
    Print('compiled');
  except
    on E: EJsonSchemaCompileError do
      Print('EJsonSchemaCompileError');
  end;
  try
    CompileJsonSchema('{"type": ');
    Print('compiled');
  except
    on E: EJsonParseError do
      Print('EJsonParseError');
  end;
  ExpectOutput('errors', 'EJsonSchemaCompileError' + #10 + 'EJsonParseError' + #10);
end;

procedure TestVersion;
var
  I, Dots: Int32;
  Ok: Boolean;
begin
  { As version_test.go: three numbers with dots between them. }
  Dots := 0;
  Ok := JsonSchemaVersion <> '';
  for I := 1 to Length(JsonSchemaVersion) do
    if JsonSchemaVersion[I] = '.' then
      Inc(Dots)
    else if not (JsonSchemaVersion[I] in ['0'..'9']) then
      Ok := False;
  Check(Ok and (Dots = 2), 'the version is not three numbers: ' + JsonSchemaVersion);
end;

begin
  Checks := 0;
  Failures := 0;
  Output := '';
  TestKeepsTheDynamicScope;
  TestCustomFormatsAreAssertedWhenFormatAssertionIsOn;
  TestFormatIsAnAnnotationByDefaultAndAssertedOnRequest;
  TestContentIsAssertedInDraft7Only;
  TestEntryPointEvaluatesFromASubschema;
  TestBaseURIResolvesRelativeReferences;
  TestDefaultDialectAppliesToSchemasWithoutSchema;
  TestCompilationErrors;
  TestInPlaceRecursionBeyondMaxDepthIsAnError;
  TestIsValidAgreesForDocumentsAndTextBeyondMaxDepth;
  TestNotOnAnInPlaceCycleStopsAtMaxDepth;
  TestNumbersAreComparedExactlyForMultipleOf;
  TestValidatorsAreSafeForConcurrentUse;
  TestDiscriminatedOneOfAndAnyOfAgreeWithExhaustiveEvaluation;
  TestDiscriminatorsKeyNullAndNumbersByValue;
  TestArraysOfSimpleArraysMatchTheGeneralPath;
  TestFusedNotRequiredAndAbsentPatternConditionsMatchTheGeneralPath;
  TestFlatFusedObjectsMatchTheGeneralPath;
  TestFusedObjectsBelowADynamicReferenceMatchTheGeneralPath;
  TestFewNamesAreLookedUpInSmallAndLargeObjects;
  TestValidatesJSONText;
  TestValidatingJSONFromAFormatCallbackWorks;
  TestNestedEvaluationsShareTheThreadsBuffers;
  TestSchemaAndInstanceNumbersParseAlike;
  TestUnevaluatedItemsWithAStaticPrefixMatchTheGeneralPath;
  TestUniqueItemsOverLargeArrays;
  TestMeetTreatsIntegersAsNumbers;
  TestPropertyLookupByName;
  TestLengthOKAgreesWithCounting;
  TestParseRoundTrips;
  TestParseRejectsInvalidJSON;
  TestDuplicateKeysKeepTheLastValueAtTheFirstPosition;
  TestStrHashReadsEveryByte;
  ExampleCompileJsonSchema;
  ExampleEvaluate;
  ExampleParseJson;
  ExampleTryIsValid;
  ExampleOptions;
  ExampleCollectedAnnotations;
  ExampleErrors;
  TestVersion;
  JsonSchemaReleaseThreadScratch;
  WriteLn(Checks - Failures, '/', Checks, ' checks passed');
  if Failures <> 0 then
    Halt(1);
end.
