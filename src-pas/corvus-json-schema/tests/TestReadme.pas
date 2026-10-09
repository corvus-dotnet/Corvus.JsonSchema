program TestReadme;

{ The samples of the package's README.md and of docs/JsonSchemaForPascal.md, compiled and run.

  Each sample is a procedure of this program (with the declarations it needs before it). The program runs each one
  with its standard output going to a file, and compares what it wrote with the output the sample's comments give.
  It then reads the two documents and checks that every block of Pascal in them is in this file, line for line
  (indentation and blank lines aside). So a sample that is changed in a document and not here, or here and not in a
  document, fails the run.

  The program is run from the package directory, as tests/run-tests.ps1 runs it: the README is README.md, this file
  is tests/TestReadme.pas, and the documentation page is ../../docs/JsonSchemaForPascal.md. The page is not part of
  a source archive of the package, so it is checked when it is there and reported as skipped when it is not.

  Unlike the other test programs this one does not include corvus.inc: it is compiled as a program that uses the
  package is, with nothing but the Delphi mode asked of Free Pascal. }

{$IFDEF FPC}
  {$MODE DELPHI}
{$ENDIF}

uses
  {$IFDEF FPC}{$IFDEF UNIX}cthreads,{$ENDIF}{$ENDIF}
  SysUtils,
  Classes,
  Corvus.JsonSchema;

{ --------------------------------------------------------------------------------------------------------------------
  The samples }

procedure ExampleUsage;
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

procedure ExampleDocuments;
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

procedure ExampleNotJson;
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

procedure ExampleErrors;
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

procedure ExampleResults;
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

procedure ExampleAnnotations;
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

{ --------------------------------------------------------------------------------------------------------------------
  The checks }

type
  TExample = procedure;

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

{ ReadText reads a file as UTF-8 text, without the carriage returns a checkout on Windows gives its lines. }
function ReadText(const Path: UTF8String): UTF8String;
var
  Stream: TFileStream;
  Bytes: TBytes;
  I, N: Int32;
begin
  Bytes := nil;
  Stream := TFileStream.Create(string(Path), fmOpenRead or fmShareDenyWrite);
  try
    SetLength(Bytes, Stream.Size);
    if Length(Bytes) > 0 then
      Stream.ReadBuffer(Bytes[0], Length(Bytes));
  finally
    Stream.Free;
  end;
  Result := '';
  SetLength(Result, Length(Bytes));
  N := 0;
  for I := 0 to Length(Bytes) - 1 do
    if Bytes[I] <> 13 then begin
      Inc(N);
      Result[N] := AnsiChar(Bytes[I]);
    end;
  SetLength(Result, N);
end;

{ Expect runs a sample with its standard output in a file, and checks what it wrote. }
procedure Expect(const Name: UTF8String; Example: TExample; const Want: UTF8String);
var
  Path, Got: UTF8String;
begin
  Path := UTF8String(ParamStr(0)) + '.out';
  AssignFile(Output, string(Path));
  Rewrite(Output);
  try
    Example;
  finally
    CloseFile(Output);
    AssignFile(Output, '');
    Rewrite(Output);
  end;
  Got := ReadText(Path);
  DeleteFile(string(Path));
  Check(Got = Want, Name + ' wrote:' + #10 + Got + 'and its comments say:' + #10 + Want);
end;

{ NextLine is the line of Text that starts at At, without the spaces around it, and moves At to the next line. }
function NextLine(const Text: UTF8String; var At: Int32): UTF8String;
var
  Start, Stop: Int32;
begin
  Start := At;
  while (At <= Length(Text)) and (Text[At] <> #10) do
    Inc(At);
  Stop := At;
  Inc(At);
  while (Start < Stop) and (Text[Start] in [' ', #9]) do
    Inc(Start);
  while (Stop > Start) and (Text[Stop - 1] in [' ', #9]) do
    Dec(Stop);
  Result := Copy(Text, Start, Stop - Start);
end;

{ Flattened is a text's lines without the spaces around them and without the empty ones, each ended by a line feed,
  after one line feed: so a run of whole lines of one text is found in another by searching for it. }
function Flattened(const Text: UTF8String): UTF8String;
var
  At: Int32;
  Line: UTF8String;
begin
  Result := #10;
  At := 1;
  while At <= Length(Text) do begin
    Line := NextLine(Text, At);
    if Line <> '' then
      Result := Result + Line + #10;
  end;
end;

{ CheckDocument checks that every block of Pascal in a Markdown document is in this program's source, and returns
  the number of blocks. }
function CheckDocument(const Path, Source: UTF8String): Int32;
var
  Text, Line, Block: UTF8String;
  At, Opened: Int32;
  InBlock: Boolean;
begin
  Result := 0;
  Text := ReadText(Path);
  At := 1;
  InBlock := False;
  Block := '';
  Opened := 0;
  while At <= Length(Text) do begin
    Line := NextLine(Text, At);
    if not InBlock then begin
      if Line = '```pascal' then begin
        InBlock := True;
        Block := '';
        Opened := Result + 1;
      end;
    end else if Line = '```' then begin
      InBlock := False;
      Inc(Result);
      Check(Pos(Flattened(Block), Source) > 0, Path + ': Pascal block ' + UTF8String(IntToStr(Opened))
        + ' is not in tests/TestReadme.pas:' + #10 + Block);
    end else
      Block := Block + Line + #10;
  end;
  Check(not InBlock, Path + ': a Pascal block is not closed');
  Check(Result > 0, Path + ': no Pascal blocks');
end;

const
  Page = '../../docs/JsonSchemaForPascal.md';

var
  Source: UTF8String;
  Blocks: Int32;
begin
  Checks := 0;
  Failures := 0;
  Expect('usage', ExampleUsage, 'TRUE' + #10 + 'FALSE' + #10);
  Expect('documents', ExampleDocuments, 'TRUE' + #10 + 'FALSE' + #10 + 'TRUE' + #10);
  Expect('text that is not JSON', ExampleNotJson, 'FALSE' + #10
    + 'invalid JSON at offset 8: unexpected end of input' + #10
    + '8: invalid JSON at offset 8: unexpected end of input' + #10);
  Expect('errors', ExampleErrors,
    'The schema cannot be compiled: Invalid regular expression ''('' in pattern.' + #10
    + 'The schema is not JSON: invalid JSON at offset 9: unexpected end of input' + #10);
  Expect('options', ExampleOptions, 'TRUE' + #10 + 'FALSE' + #10);
  Expect('results', ExampleResults, 'FALSE' + #10
    + '/properties/id at "/id": The value was expected to match the subschema.' + #10
    + '/properties/id/type at "/id": The value was expected to be of type ''integer''' + #10
    + '/required at "/name": Required property not present ''name''' + #10);
  Expect('annotations', ExampleAnnotations, 'TRUE' + #10 + '"Person"' + #10 + '"Name"' + #10);
  Expect('threads', ExampleThreads, 'TRUE' + #10);
  JsonSchemaReleaseThreadScratch;

  Source := Flattened(ReadText('tests/TestReadme.pas'));
  Blocks := CheckDocument('README.md', Source);
  WriteLn('README.md: ', Blocks, ' Pascal blocks');
  if FileExists(Page) then begin
    Blocks := CheckDocument(Page, Source);
    WriteLn(Page, ': ', Blocks, ' Pascal blocks');
  end else
    WriteLn(Page, ': skipped (the file is not there)');
  WriteLn(Checks - Failures, '/', Checks, ' checks passed');
  if Failures <> 0 then
    Halt(1);
end.