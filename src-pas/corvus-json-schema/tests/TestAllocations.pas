program TestAllocations;

{$I corvus.inc}

{ Validation allocates nothing in the steady state: the evaluator's buffers are kept by the thread that validates,
  JSON text is parsed into reused buffers, and the document is evaluated where it was parsed. A port of
  allocations_test.go of the Go module.

  Go counts with testing.AllocsPerRun. Here the program installs a memory manager that counts the requests for
  memory (GetMem, AllocMem and ReallocMem) of the thread that validates, between the moment that thread starts
  counting and the moment it stops: the flag that says so is a thread variable, so no other thread is counted.

  Four asserted formats are outside this, as in the Go module: regex (the pattern is parsed), idn-hostname and
  idn-email (the labels are decoded), and hostname for a label that starts with "xn--".

  The Go module's second test (text that is not JSON allocates nothing for IsValidBytes) has no counterpart: here
  IsValid raises an exception for text that is not JSON, and TryIsValid returns the reason as a string, each of
  which is an allocation. }

uses
  SysUtils,
  Corvus.JsonSchema;

var
  OldManager, CountingManager: TMemoryManager;
  Requests: Int64;

threadvar
  { The calling thread is counting. }
  Counting: Boolean;

{$IFDEF FPC}
function CountedGetMem(Size: PtrUInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.GetMem(Size);
end;

function CountedAllocMem(Size: PtrUInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.AllocMem(Size);
end;

function CountedReallocMem(var P: Pointer; Size: PtrUInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.ReallocMem(P, Size);
end;
{$ELSE}
function CountedGetMem(Size: NativeInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.GetMem(Size);
end;

function CountedAllocMem(Size: NativeInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.AllocMem(Size);
end;

function CountedReallocMem(P: Pointer; Size: NativeInt): Pointer;
begin
  if Counting then
    Inc(Requests);
  Result := OldManager.ReallocMem(P, Size);
end;
{$ENDIF}

procedure InstallCountingManager;
begin
  GetMemoryManager(OldManager);
  CountingManager := OldManager;
  CountingManager.GetMem := CountedGetMem;
  CountingManager.AllocMem := CountedAllocMem;
  CountingManager.ReallocMem := CountedReallocMem;
  SetMemoryManager(CountingManager);
end;

const
  Runs = 100;

type
  TAllocationCase = record
    Name: UTF8String;
    Schema: UTF8String;
    Options: TJsonSchemaOptions;
    Instances: array of UTF8String;
  end;

var
  Cases: array of TAllocationCase;
  Checks, Failures: Int32;
  TotalRequests, TotalValidations: Int64;
  Sink: Boolean;

function Number(V: Int32): UTF8String;
begin
  Result := UTF8String(IntToStr(V));
end;

procedure AddCase(const Name, Schema: UTF8String; const Options: TJsonSchemaOptions;
  const Instances: array of UTF8String);
var
  N, I: Int32;
begin
  N := Length(Cases);
  SetLength(Cases, N + 1);
  Cases[N].Name := Name;
  Cases[N].Schema := Schema;
  Cases[N].Options := Options;
  SetLength(Cases[N].Instances, Length(Instances));
  for I := 0 to High(Instances) do
    Cases[N].Instances[I] := Instances[I];
end;

procedure AllocationCases;
var
  Large, Unique: UTF8String;
  I: Int32;
  Formats: TJsonSchemaOptions;
  Eacute, Uuml: UTF8String;
begin
  Large := '';
  for I := 0 to 299 do begin
    if I > 0 then
      Large := Large + ',';
    Large := Large + '"p' + Number(I) + '": ' + Number(I);
  end;
  Unique := '';
  for I := 0 to 99 do begin
    if I > 0 then
      Unique := Unique + ',';
    Unique := Unique + '{"id": ' + Number(I) + ', "name": "n' + Number(I) + '"}';
  end;
  Eacute := #$C3#$A9;
  Uuml := #$C3#$BC;
  AddCase('objects and arrays', '{'
    + '"type": "object",'
    + '"properties": { "name": { "type": "string", "minLength": 1 }, "tags": { "type": "array", "items": '
    + '{ "type": "string" } } },'
    + '"required": ["name"]'
    + '}', DefaultJsonSchemaOptions, [
    '{"name": "a", "tags": ["x", "y\nz"]}',
    '{"name": "", "tags": []}',
    '{"name": "caf' + Eacute + '", "tags": ["1", "2", "3", "4", "5", "6", "7", "8"], "other": {"deep": [1, [2, '
      + '[3]]]}}']);
  AddCase('keywords of every kind', '{'
    + '"type": "object",'
    + '"properties": {'
    + '"n": { "type": "number", "minimum": 0, "exclusiveMaximum": 100, "multipleOf": 0.25 },'
    + '"s": { "type": "string", "maxLength": 5, "pattern": "^[a-z]+$" },'
    + '"e": { "enum": ["a", "b", 1, null] },'
    + '"c": { "const": { "k": [1, 2] } },'
    + '"u": { "type": "array", "uniqueItems": true, "contains": { "type": "integer" }, "minContains": 1 },'
    + '"o": { "oneOf": [{ "type": "string" }, { "type": "integer" }, { "required": ["x"] }] },'
    + '"a": { "anyOf": [{ "minimum": 5 }, { "maxLength": 2 }] },'
    + '"i": { "if": { "type": "integer" }, "then": { "minimum": 1 }, "else": { "type": "string" } },'
    + '"x": { "not": { "type": "null" } }'
    + '},'
    + '"patternProperties": { "^x-": { "type": "boolean" } },'
    + '"additionalProperties": { "type": "integer" },'
    + '"propertyNames": { "maxLength": 10 },'
    + '"dependentRequired": { "n": ["s"] },'
    + '"minProperties": 1'
    + '}', DefaultJsonSchemaOptions, [
    '{"n": 1.5, "s": "abc", "e": "b", "c": {"k": [1, 2.0]}, "u": [1, "a", [2], {"b": 1}], "o": 3, "a": "ab", '
      + '"i": 2, "x": 0, "x-flag": true, "extra": 7}',
    '{"n": 1.3, "s": "abc"}',
    '{"u": [' + Unique + ', 5]}',
    '{"u": [' + Unique + ', {"name": "n3", "id": 3}]}',
    '{"o": {"x": 1}, "a": 3, "i": "s", "x": null}',
    '{"a-name-that-is-too-long": 1}']);
  AddCase('unevaluated properties and items', '{'
    + '"$defs": { "base": { "properties": { "a": { "type": "integer" } }, "patternProperties": { "^x-": true } } },'
    + '"allOf": [{ "$ref": "#/$defs/base" }],'
    + '"anyOf": [{ "properties": { "b": true }, "required": ["b"] }, { "properties": { "c": true } }],'
    + '"oneOf": [{ "properties": { "d": { "type": "string" } } }, { "properties": { "d": { "type": "integer" } } }],'
    + '"properties": { "list": { "prefixItems": [true], "contains": { "type": "string" }, "unevaluatedItems": '
    + 'false } },'
    + '"unevaluatedProperties": false'
    + '}', DefaultJsonSchemaOptions, [
    '{"a": 1, "b": 2, "d": "s", "x-y": null, "list": [1, "a", "b"]}',
    '{"a": 1, "c": 2, "d": 3, "list": [1, "a", 2]}',
    '{"a": 1, "e": 2}',
    '{' + Large + '}']);
  AddCase('fused objects with conditions', '{'
    + '"type": "object",'
    + '"properties": { "kind": { "enum": ["a", "b"] }, "value": true, "other": { "type": "string" } },'
    + '"allOf": [{ "$ref": "#/$defs/ext" }, { "properties": { "c": { "type": "integer" } } }],'
    + '"if": { "properties": { "kind": { "const": "a" } }, "required": ["kind"] },'
    + '"then": { "required": ["value"], "properties": { "extra": { "type": "integer" } } },'
    + '"else": { "not": { "required": ["value", "other"] } },'
    + '"dependentSchemas": { "c": { "properties": { "d": true } } },'
    + '"unevaluatedProperties": false,'
    + '"$defs": { "ext": { "patternProperties": { "^x-": true } } }'
    + '}', DefaultJsonSchemaOptions, [
    '{"kind": "a", "value": 1, "extra": 2, "x-a": 1}',
    '{"kind": "b", "value": 1, "c": 3, "d": 4}',
    '{"kind": "b", "value": 1, "other": "x"}',
    '{"kind": "a"}',
    '{"kind": "a", "value": 1, ' + Large + '}']);
  AddCase('a dynamic scope', '{'
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
    + '"properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } }'
    + '}'
    + '}'
    + '}', DefaultJsonSchemaOptions, [
    '{"children": [{"data": 1, "children": [{"data": [1, 2, 3]}]}]}',
    '{"children": [{"daat": 1}]}']);
  Formats := DefaultJsonSchemaOptions;
  WithDefaultDialect(Formats, Draft7);
  WithAssertFormat(Formats, True);
  AddCase('asserted formats and content', '{'
    + '"properties": {'
    + '"date": { "format": "date-time" }, "ip": { "format": "ipv6" }, "host": { "format": "hostname" },'
    + '"id": { "format": "uuid" }, "n": { "format": "int32" }, "pointer": { "format": "json-pointer" },'
    + '"uri": { "format": "uri" }, "ref": { "format": "uri-reference" }, "iri": { "format": "iri" },'
    + '"template": { "format": "uri-template" }, "email": { "format": "email" },'
    + '"duration": { "format": "duration" }, "time": { "format": "time" }, "v4": { "format": "ipv4" },'
    + '"json": { "contentMediaType": "application/json", "contentEncoding": "base64" }'
    + '}'
    + '}', Formats, [
    '{"date": "2020-01-02T03:04:05.678Z", "ip": "::ffff:192.168.0.1", "host": "example.com", "id": '
      + '"2eb8aa08-aa98-11ea-b4aa-73b441d16380", "n": 12, "pointer": "/a/~0b", "json": "eyJhIjogWzEsIDIsIDNdfQ=="}',
    '{"uri": "http://example.com/a/b?c=d#e", "ref": "../a/b?c#d", "iri": "http://' + Eacute + 'xample.com/' + Uuml
      + '", "template": "http://example.com/{id}/x{?q,r}", "email": "joe.bloggs@example.com", "duration": '
      + '"P4DT12H30M5S", "time": "08:30:06.283185+01:00", "v4": "1.2.3.4"}',
    '{"n": 1e30}',
    '{"json": "bm90IGpzb24="}']);
end;

{ Report records the requests for memory of one form of validation: Runs passes over the instances of a case. }
procedure Report(const CaseName, What: UTF8String; Count: Int64; Instances: Int32);
begin
  Inc(Checks);
  Inc(TotalRequests, Count);
  Inc(TotalValidations, Int64(Runs) * Instances);
  if Count <> 0 then begin
    Inc(Failures);
    WriteLn('FAILED: ', CaseName, ': ', Count, ' requests for memory in ', Runs, ' passes over ', Instances, ' ',
      What);
  end;
end;

procedure RunCase(const C: TAllocationCase);
var
  V: IJsonSchemaValidator;
  Documents: array of TJsonDocument;
  Texts, Padded: array of TBytes;
  Strings: array of UTF8String;
  Error: UTF8String;
  N, I, R, J: Int32;
  Before: Int64;
begin
  V := CompileJsonSchema(C.Schema, C.Options);
  N := Length(C.Instances);
  Documents := nil;
  Texts := nil;
  Padded := nil;
  Strings := nil;
  SetLength(Documents, N);
  SetLength(Texts, N);
  SetLength(Padded, N);
  SetLength(Strings, N);
  for I := 0 to N - 1 do begin
    Strings[I] := C.Instances[I];
    Documents[I] := ParseJson(Strings[I]);
    Texts[I] := BytesOf(Strings[I]);
    { The text in the middle of an array of bytes. }
    SetLength(Padded[I], Length(Texts[I]) + 6);
    for J := 0 to Length(Padded[I]) - 1 do
      Padded[I][J] := Ord('x');
    Move(Texts[I][0], Padded[I][3], Length(Texts[I]));
  end;
  { The first validations size the buffers. }
  Error := '';
  for I := 0 to N - 1 do begin
    V.IsValid(Documents[I]);
    V.IsValid(Texts[I], 0, Length(Texts[I]));
    V.IsValid(Padded[I], 3, Length(Texts[I]));
    V.IsValid(Strings[I]);
    V.TryIsValid(Documents[I], Error);
    V.TryIsValid(Texts[I], 0, Length(Texts[I]), Error);
    V.TryIsValid(Strings[I], Error);
  end;

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.IsValid(Documents[I]) <> Sink;
  Counting := False;
  Report(C.Name, 'documents', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.TryIsValid(Documents[I], Error) <> Sink;
  Counting := False;
  Report(C.Name, 'documents (TryIsValid)', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.IsValid(Texts[I], 0, Length(Texts[I])) <> Sink;
  Counting := False;
  Report(C.Name, 'texts as bytes', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.TryIsValid(Texts[I], 0, Length(Texts[I]), Error) <> Sink;
  Counting := False;
  Report(C.Name, 'texts as bytes (TryIsValid)', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.IsValid(Padded[I], 3, Length(Texts[I])) <> Sink;
  Counting := False;
  Report(C.Name, 'texts as part of an array of bytes', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.IsValid(Strings[I]) <> Sink;
  Counting := False;
  Report(C.Name, 'texts as strings', Requests - Before, N);

  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    for I := 0 to N - 1 do
      Sink := V.TryIsValid(Strings[I], Error) <> Sink;
  Counting := False;
  Report(C.Name, 'texts as strings (TryIsValid)', Requests - Before, N);
end;

{ The counting itself is checked: an allocation by this thread while it counts is seen. }
procedure TestTheCounterCounts;
var
  B: TBytes;
  Before: Int64;
begin
  B := nil;
  Before := Requests;
  Counting := True;
  SetLength(B, 100);
  Counting := False;
  Inc(Checks);
  if (Requests - Before < 1) or (Length(B) <> 100) then begin
    Inc(Failures);
    WriteLn('FAILED: the memory manager did not count an allocation');
  end;
  Before := Requests;
  SetLength(B, 100000);
  Inc(Checks);
  if (Requests <> Before) or (Length(B) <> 100000) then begin
    Inc(Failures);
    WriteLn('FAILED: the memory manager counted while the thread was not counting');
  end;
end;

{ A format validator must not raise an exception. One that does (here by validating text that is not JSON with the
  IsValid that raises) abandons the evaluation it was called from, and the thread's buffers are fit for the next:
  a later validation of JSON text still parses into them, and allocates nothing. }
var
  Raising: IJsonSchemaValidator;

function Raises(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := Raising.IsValid(Text, Start, Len);
end;

procedure TestAFormatValidatorThatRaisesLeavesTheBuffersFit;
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
    + '"properties": { "data": { "format": "raises" }, "children": { "type": "array", "items": { "$dynamicRef": '
    + '"#node" } } }'
    + '}'
    + '}'
    + '}';
  { The innermost data is not JSON, two evaluations down. }
  Instance: UTF8String = '{"children": [{"data": "{\"children\": [{\"data\": \"{not json\"}]}"}]}';
  Fine: UTF8String = '{"children": [{"data": "{\"children\": [{\"data\": \"{}\"}]}"}]}';
var
  O: TJsonSchemaOptions;
  Raised: Int32;
  R: Int32;
  Before: Int64;
  Document: TJsonDocument;
  C: TJsonSchemaResults;
  Plain: IJsonSchemaValidator;
begin
  Plain := CompileJsonSchema('{ "type": "array", "items": { "type": "integer" } }');
  Plain.IsValid('[1, 2, 3]');
  O := DefaultJsonSchemaOptions;
  WithAssertFormat(O, True);
  WithFormat(O, 'raises', Raises);
  Raising := CompileJsonSchema(Tree, O);
  Document := ParseJson(Instance);
  Raised := 0;
  try
    Raising.IsValid(Instance);
  except
    on EJsonParseError do
      Inc(Raised);
  end;
  try
    Raising.IsValid(Document);
  except
    on EJsonParseError do
      Inc(Raised);
  end;
  try
    C := NewJsonSchemaResults(Basic);
    Raising.Evaluate(Document, C);
  except
    on EJsonParseError do
      Inc(Raised);
  end;
  Inc(Checks);
  if Raised <> 3 then begin
    Inc(Failures);
    WriteLn('FAILED: the exception of a format validator reached the caller ', Raised, ' times of 3');
  end;
  Inc(Checks);
  if not Raising.IsValid(Fine) then begin
    Inc(Failures);
    WriteLn('FAILED: a validation after an abandoned one gave the wrong answer');
  end;
  { A validation of JSON text that no other is running around parses into the thread's buffers again. }
  Before := Requests;
  Counting := True;
  for R := 1 to Runs do
    Sink := Plain.IsValid('[1, 2, 3]') <> Sink;
  Counting := False;
  Report('after a format validator raised', 'texts as strings', Requests - Before, 1);
  Raising := nil;
end;

var
  I: Int32;
begin
  InstallCountingManager;
  Checks := 0;
  Failures := 0;
  TotalRequests := 0;
  TotalValidations := 0;
  Sink := False;
  Cases := nil;
  TestTheCounterCounts;
  AllocationCases;
  for I := 0 to Length(Cases) - 1 do
    RunCase(Cases[I]);
  TestAFormatValidatorThatRaisesLeavesTheBuffersFit;
  JsonSchemaReleaseThreadScratch;
  WriteLn(TotalRequests, ' requests for memory in ', TotalValidations, ' steady-state validations');
  WriteLn(Checks - Failures, '/', Checks, ' checks passed');
  if Failures <> 0 then
    Halt(1);
end.
