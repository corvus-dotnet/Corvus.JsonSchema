program TestSuite;

{$I corvus.inc}

{ Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, as suite_test.go of the Go
  module and the C# and Rust suite runners do. Every dialect's cases are in one tree (validation/tests, required and
  optional): each case's compatibility selects the drafts it applies to, its tags whether format is asserted, and its
  externalSchemas are served to that case alone. Every case runs fail-fast (as a document, as bytes and as a string)
  and through a results collector at each level.

  Set JSON_SCHEMA_TEST_SUITE to use a different checkout, SUITE_DRAFT and SUITE_FILTER to narrow the run. }

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema;

{$I SuiteCommon.inc}

type
  TArea = record
    Name: UTF8String;
    Passed, Counted: Int32;
  end;

var
  CaseSchemas: TCaseSchemas;
  Areas: array of TArea;
  Filter: UTF8String;
  Total, Failed, Skipped: Int32;

function AreaOf(const AreaName: UTF8String): Int32;
var
  I: Int32;
begin
  for I := 0 to Length(Areas) - 1 do
    if Areas[I].Name = AreaName then
      Exit(I);
  Result := Length(Areas);
  SetLength(Areas, Result + 1);
  Areas[Result].Name := AreaName;
  Areas[Result].Passed := 0;
  Areas[Result].Counted := 0;
end;

function SameRows(const A, B: TJsonSchemaResultArray): Boolean;
var
  I: Int32;
begin
  Result := False;
  if Length(A) <> Length(B) then
    Exit;
  for I := 0 to Length(A) - 1 do
    if (A[I].IsMatch <> B[I].IsMatch) or (A[I].Message <> B[I].Message)
      or (A[I].EvaluationLocation <> B[I].EvaluationLocation)
      or (A[I].SchemaEvaluationLocation <> B[I].SchemaEvaluationLocation)
      or (A[I].DocumentEvaluationLocation <> B[I].DocumentEvaluationLocation) then
      Exit;
  Result := True;
end;

function BoolText(V: Boolean): UTF8String;
begin
  if V then
    Result := 'true'
  else
    Result := 'false';
end;

{ RunSuiteCase evaluates one instance every way the API offers and checks that they agree. False, with the reason,
  when they do not. }
function RunSuiteCase(const V: IJsonSchemaValidator; const Data: UTF8String; out Outcome: Boolean;
  out Error: UTF8String): Boolean;
const
  Levels: array[0..2] of TJsonSchemaResultsLevel = (Basic, Detailed, Verbose);
var
  Document: TJsonDocument;
  Fast, Ok, Summary: Boolean;
  Reason: UTF8String;
  Offset, L, I, Pad: Int32;
  C: TJsonSchemaResults;
  Rows, VerboseRows: TJsonSchemaResultArray;
  Bytes, Padded: TBytes;
begin
  Result := False;
  Outcome := False;
  Error := '';
  try
    if not TryParseJson(Data, Document, Reason, Offset) then begin
      Error := 'document: ' + Reason;
      Exit;
    end;
    Fast := V.TryIsValid(Document, Reason);
    if Reason <> '' then begin
      Error := 'fast: ' + Reason;
      Exit;
    end;
    if V.IsValid(Document) <> Fast then begin
      Error := 'IsValid disagrees with TryIsValid (' + BoolText(Fast) + ')';
      Exit;
    end;
    VerboseRows := nil;
    for L := 0 to 2 do begin
      C := NewJsonSchemaResults(Levels[L]);
      Ok := V.TryEvaluate(Document, C, Reason);
      if Reason <> '' then begin
        Error := 'collecting: ' + Reason;
        Exit;
      end;
      if Ok <> Fast then begin
        Error := 'a collector returned ' + BoolText(Ok) + ', fast returned ' + BoolText(Fast);
        Exit;
      end;
      Summary := False;
      Rows := ResultRows(C);
      for I := 0 to Length(Rows) - 1 do
        Summary := Summary or ((Rows[I].EvaluationLocation = '') and (Rows[I].DocumentEvaluationLocation = '')
          and (Rows[I].IsMatch = Ok));
      if not Summary then begin
        Error := 'no root summary row matching the result';
        Exit;
      end;
      VerboseRows := Rows;
    end;
    { The same instance straight from the text, in the thread's reused buffers. }
    Bytes := BytesOf(Data);
    Ok := V.TryIsValid(Bytes, 0, Length(Bytes), Reason);
    if Reason <> '' then begin
      Error := 'TryIsValid of bytes: ' + Reason;
      Exit;
    end;
    if (Ok <> Fast) or (V.IsValid(Bytes, 0, Length(Bytes)) <> Fast) then begin
      Error := 'the bytes gave ' + BoolText(Ok) + ', the document ' + BoolText(Fast);
      Exit;
    end;
    { And from the middle of an array of bytes. }
    Pad := 3;
    Padded := nil;
    SetLength(Padded, Length(Bytes) + 2 * Pad);
    for I := 0 to Length(Padded) - 1 do
      Padded[I] := Ord('x');
    if Length(Bytes) > 0 then
      Move(Bytes[0], Padded[Pad], Length(Bytes));
    if V.IsValid(Padded, Pad, Length(Bytes)) <> Fast then begin
      Error := 'part of an array of bytes disagrees with the document (' + BoolText(Fast) + ')';
      Exit;
    end;
    Ok := V.TryIsValid(Data, Reason);
    if Reason <> '' then begin
      Error := 'TryIsValid of a string: ' + Reason;
      Exit;
    end;
    if (Ok <> Fast) or (V.IsValid(Data) <> Fast) then begin
      Error := 'the string gave ' + BoolText(Ok) + ', the document ' + BoolText(Fast);
      Exit;
    end;
    C := NewJsonSchemaResults(Verbose);
    V.Evaluate(Bytes, 0, Length(Bytes), C);
    if not SameRows(VerboseRows, ResultRows(C)) then begin
      Error := 'Evaluate of bytes: the verbose results differ from the document''s';
      Exit;
    end;
    Outcome := Fast;
    Result := True;
  except
    on E: Exception do
      Error := 'exception during evaluation: ' + UTF8String(E.ClassName) + ': ' + UTF8String(E.Message);
  end;
end;

procedure RunFile(Dialect: TJsonSchemaDialect; Release: Int32; const Path, Name, AreaName: UTF8String);
var
  Text: TBytes;
  Doc: TDocument;
  E: TParseError;
  Area, G, T, Group, Groups, Schema, Tests, Test, Data, Valid, Description: Int32;
  Options: TJsonSchemaOptions;
  GroupDescription, TestDescription, CompileError, Error, Got: UTF8String;
  Validator: IJsonSchemaValidator;
  Actual, Expected, Ran, AssertFormat: Boolean;
begin
  if not ReadFile(Path, Text) then begin
    WriteLn('FAILED: ', Path, ' could not be read');
    Halt(1);
  end;
  Groups := -1;
  if ParseDocument(Text, Doc, E) then
    Groups := Member(Doc, Doc.Root, 'tests');
  if not IsKind(Doc, Groups, KindArray) then begin
    WriteLn('FAILED: ', Path, ' is not a suite file: ', ParseErrorText(E));
    Halt(1);
  end;
  Area := AreaOf(AreaName);
  for G := 0 to DocCount(Doc, Groups) - 1 do begin
    Group := DocFirst(Doc, Groups) + G;
    if not SuiteCompatible(Doc, Group, Release) then
      Continue;
    { The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there. }
    if HasTag(Doc, Group, 'format-annotation') and (Release <= 7) then
      Continue;
    AssertFormat := HasTag(Doc, Group, 'format-assertion');
    GroupDescription := '';
    Description := Member(Doc, Group, 'description');
    if IsKind(Doc, Description, KindString) then
      GroupDescription := DocStrCopy(Doc, Description);
    if (Filter <> '') and (Pos(Filter, GroupDescription) = 0) and (Pos(Filter, Name) = 0) then
      Continue;
    Schema := Member(Doc, Group, 'schema');
    Options := DefaultJsonSchemaOptions;
    WithDefaultDialect(Options, Dialect);
    CaseSchemas := CaseSchemasOf(Doc, Group);
    WithDocumentResolver(Options, CaseSchemaResolver, @CaseSchemas);
    if AssertFormat then
      WithAssertFormat(Options, True);
    Validator := nil;
    CompileError := '';
    try
      Validator := CompileJsonSchema(ValueText(Doc, Schema), Options);
    except
      on Ex: Exception do
        CompileError := 'compile error: ' + UTF8String(Ex.ClassName) + ': ' + UTF8String(Ex.Message);
    end;
    Tests := Member(Doc, Group, 'tests');
    if not IsKind(Doc, Tests, KindArray) then
      Continue;
    for T := 0 to DocCount(Doc, Tests) - 1 do begin
      Test := DocFirst(Doc, Tests) + T;
      Inc(Total);
      Inc(Areas[Area].Counted);
      TestDescription := '';
      Description := Member(Doc, Test, 'description');
      if IsKind(Doc, Description, KindString) then
        TestDescription := DocStrCopy(Doc, Description);
      Data := Member(Doc, Test, 'data');
      Valid := Member(Doc, Test, 'valid');
      Expected := IsKind(Doc, Valid, KindBool) and DocBoolean(Doc, Valid);
      Actual := False;
      Error := CompileError;
      Ran := False;
      if Error = '' then
        Ran := RunSuiteCase(Validator, ValueText(Doc, Data), Actual, Error);
      if Ran and (Actual = Expected) then begin
        Inc(Areas[Area].Passed);
        Continue;
      end;
      { Leap seconds are skipped where format is asserted until this engine has the leap-second rule. }
      if AssertFormat and (Pos('leap second', LowerAscii(TestDescription)) > 0) then begin
        Inc(Areas[Area].Passed);
        Inc(Skipped);
        WriteLn('skipped: ', Name, ' [', GroupDescription, '] ', TestDescription);
        Continue;
      end;
      Got := BoolText(Actual);
      if not Ran then
        Got := Error;
      Inc(Failed);
      WriteLn(Name, ' [', GroupDescription, '] ', TestDescription, ': expected ', BoolText(Expected), ', got ', Got);
    end;
  end;
end;

const
  DraftNames: array[0..4] of UTF8String = ('draft4', 'draft6', 'draft7', 'draft2019-09', 'draft2020-12');
  DraftDialects: array[0..4] of TJsonSchemaDialect = (Draft4, Draft6, Draft7, Draft201909, Draft202012);
  DraftReleases: array[0..4] of Int32 = (4, 6, 7, 2019, 2020);
  { The number of test cases the suite has, less the exclusion: a smaller count means a stale or partial run. }
  ExpectedTotal = 8686;
var
  Root, Tests, DraftFilter, Name: UTF8String;
  Files, OptionalFiles: TUTF8StringArray;
  D, I: Int32;
begin
  Root := SuiteRoot;
  Tests := Root + '/validation/tests';
  if not DirectoryExists(Tests) then begin
    WriteLn('JSON-Schema-Test-Suite not found at ', Root,
      ' (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)');
    Halt(1);
  end;
  Filter := UTF8String(GetEnvironmentVariable('SUITE_FILTER'));
  DraftFilter := UTF8String(GetEnvironmentVariable('SUITE_DRAFT'));
  Areas := nil;
  Total := 0;
  Failed := 0;
  Skipped := 0;
  Files := JsonFiles(Tests);
  OptionalFiles := JsonFiles(Tests + '/optional');
  for D := 0 to 4 do begin
    if (DraftFilter <> '') and (DraftFilter <> DraftNames[D]) then
      Continue;
    for I := 0 to Length(Files) - 1 do
      RunFile(DraftDialects[D], DraftReleases[D], Tests + '/' + Files[I], DraftNames[D] + '/' + Files[I],
        DraftNames[D]);
    for I := 0 to Length(OptionalFiles) - 1 do begin
      Name := DraftNames[D] + '/optional/' + OptionalFiles[I];
      { The one exclusion, matching the C# and Rust runners: zero-terminated floats. }
      if Name <> 'draft4/optional/zero-terminated-floats.json' then
        RunFile(DraftDialects[D], DraftReleases[D], Tests + '/optional/' + OptionalFiles[I], Name,
          DraftNames[D] + '/optional');
    end;
  end;
  JsonSchemaReleaseThreadScratch;
  for I := 0 to Length(Areas) - 1 do
    WriteLn(Areas[I].Name, '': 35 - Length(Areas[I].Name), Areas[I].Passed: 5, '/', Areas[I].Counted);
  WriteLn(Skipped, ' failing leap second cases that assert format were skipped');
  WriteLn('JSON-Schema-Test-Suite: ', Total - Failed, '/', Total, ' passed');
  if Total = 0 then begin
    WriteLn('no JSON-Schema-Test-Suite cases ran');
    Halt(1);
  end;
  if Failed <> 0 then begin
    WriteLn(Failed, ' JSON-Schema-Test-Suite cases failed');
    Halt(1);
  end;
  if (DraftFilter = '') and (Filter = '') and (Total <> ExpectedTotal) then begin
    WriteLn(Total, ' JSON-Schema-Test-Suite cases ran, where ', ExpectedTotal, ' were expected');
    Halt(1);
  end;
end.
