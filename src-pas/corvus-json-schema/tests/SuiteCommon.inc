{ What the programs that run the JSON-Schema-Test-Suite share: reading files, the test cases' compatibility, tags
  and external schemas, and the directory listings. Included by TestSuite, TestResults and TestDifferential. }

type
  { A test case's externalSchemas: the object (the node Node of the suite file's document Doc, or -1 for none) whose
    members are the schemas by their retrieval URIs. }
  TCaseSchemas = record
    Doc: TDocument;
    Node: Int32;
  end;
  PCaseSchemas = ^TCaseSchemas;

function ReadFile(const Path: UTF8String; out Bytes: TBytes): Boolean;
var
  F: THandle;
  Size, Got: Int64;
begin
  Bytes := nil;
  Result := False;
  F := FileOpen(Path, fmOpenRead or fmShareDenyNone);
  if F = THandle(-1) then
    Exit;
  Size := FileSeek(F, Int64(0), 2);
  FileSeek(F, Int64(0), 0);
  SetLength(Bytes, Size);
  Got := 0;
  if Size > 0 then
    Got := FileRead(F, Bytes[0], Size);
  FileClose(F);
  Result := Got = Size;
end;

{ JsonFiles are the names of the .json files of a directory, in byte order. }
function JsonFiles(const Dir: UTF8String): TUTF8StringArray;
var
  Search: TSearchRec;
  Name, T: UTF8String;
  N, I, J: Int32;
begin
  Result := nil;
  N := 0;
  if FindFirst(Dir + '/*', faAnyFile, Search) <> 0 then
    Exit;
  repeat
    Name := UTF8String(Search.Name);
    if (Search.Attr and faDirectory = 0) and (Length(Name) > 5)
      and (Copy(Name, Length(Name) - 4, 5) = '.json') then begin
      SetLength(Result, N + 1);
      Result[N] := Name;
      Inc(N);
    end;
  until FindNext(Search) <> 0;
  FindClose(Search);
  for I := 1 to N - 1 do begin
    T := Result[I];
    J := I - 1;
    while (J >= 0) and (CompareUtf8(Result[J], T) > 0) do begin
      Result[J + 1] := Result[J];
      Dec(J);
    end;
    Result[J + 1] := T;
  end;
end;

{ ValueText is the value N of a document as JSON text of its own (numbers as they were written), as the Go tests
  take a schema or an instance out of a suite file. }
function ValueText(const D: TDocument; N: Int32): UTF8String;
var
  Sub: TDocument;
begin
  Sub := D;
  Sub.Root := N;
  Result := DocumentToJson(Sub);
end;

{ SuiteRoot is the checkout of the JSON-Schema-Test-Suite: JSON_SCHEMA_TEST_SUITE, or the repository's submodule
  (from the package directory). }
function SuiteRoot: UTF8String;
begin
  Result := UTF8String(GetEnvironmentVariable('JSON_SCHEMA_TEST_SUITE'));
  if Result = '' then
    Result := '../../JSON-Schema-Test-Suite';
end;

function LowerAscii(const S: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := S;
  for I := 1 to Length(Result) do
    if (Result[I] >= 'A') and (Result[I] <= 'Z') then
      Result[I] := AnsiChar(Ord(Result[I]) + 32);
end;

{ CaseSchemaResolver serves a test case's externalSchemas (a TCaseSchemas) at their retrieval URIs. }
function CaseSchemaResolver(Context: Pointer; const Uri: UTF8String; out Doc: TDocument): Boolean;
var
  C: PCaseSchemas;
  M: Int32;
  E: TParseError;
begin
  Doc := Default(TDocument);
  Result := False;
  C := PCaseSchemas(Context);
  M := Member(C^.Doc, C^.Node, Uri);
  if M < 0 then
    Exit;
  Result := ParseDocumentString(ValueText(C^.Doc, M), Doc, E);
end;

{ CaseSchemasOf is a test case's externalSchemas. }
function CaseSchemasOf(const D: TDocument; TestCase: Int32): TCaseSchemas;
begin
  Result.Doc := D;
  Result.Node := Member(D, TestCase, 'externalSchemas');
end;

{ SuiteCompatible reports whether a test case's compatibility (a comma-separated list of minimums, <= maximums and =
  releases, from the smallest release to the largest) admits the release, by the algorithm of the suite's
  validation/README.md. A test case without one is compatible with every release. }
function SuiteCompatible(const D: TDocument; TestCase, Release: Int32): Boolean;
var
  N, Number, Comma: Int32;
  Rest, Constraint, Op: UTF8String;
begin
  Result := True;
  N := Member(D, TestCase, 'compatibility');
  if not IsKind(D, N, KindString) then
    Exit;
  Rest := DocStrCopy(D, N);
  while Rest <> '' do begin
    Comma := Pos(',', Rest);
    if Comma = 0 then begin
      Constraint := Rest;
      Rest := '';
    end
    else begin
      Constraint := Copy(Rest, 1, Comma - 1);
      Rest := Copy(Rest, Comma + 1, Length(Rest) - Comma);
    end;
    Op := '';
    if Copy(Constraint, 1, 2) = '<=' then
      Op := '<='
    else if Copy(Constraint, 1, 1) = '=' then
      Op := '=';
    Number := StrToInt(Trim(string(Copy(Constraint, Length(Op) + 1, Length(Constraint) - Length(Op)))));
    if Op = '<=' then
      Result := Release <= Number
    else if Op = '=' then
      Result := Release = Number
    else
      Result := Release >= Number;
    if Release <= Number then
      Break;
  end;
end;

{ HasTag reports whether a test case has a tag. }
function HasTag(const D: TDocument; TestCase: Int32; const Tag: UTF8String): Boolean;
var
  Tags, I, T: Int32;
begin
  Result := False;
  Tags := Member(D, TestCase, 'tags');
  if not IsKind(D, Tags, KindArray) then
    Exit;
  for I := 0 to DocCount(D, Tags) - 1 do begin
    T := DocFirst(D, Tags) + I;
    if IsKind(D, T, KindString) and (DocStrCopy(D, T) = Tag) then
      Exit(True);
  end;
end;
