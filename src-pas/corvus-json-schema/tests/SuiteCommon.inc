{ What the programs that run the JSON-Schema-Test-Suite share: reading files, the suite's remotes, and the
  directory listings. Included by TestSuite, TestResults and TestDifferential. }

type
  { The suite's remotes: the documents read so far, by their path within the remotes directory. }
  TRemotes = record
    Root: UTF8String;
    Cache: TStringTable;
    Docs: array of TDocument;
  end;
  PRemotes = ^TRemotes;

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

{ RemoteResolver resolves the suite's remotes (http://localhost:1234/...) from its remotes directory. }
function RemoteResolver(Context: Pointer; const Uri: UTF8String; out Doc: TDocument): Boolean;
const
  Prefix: UTF8String = 'http://localhost:1234/';
var
  R: PRemotes;
  Rest: UTF8String;
  At, N: Int32;
  Text: TBytes;
  E: TParseError;
begin
  Doc := Default(TDocument);
  Result := False;
  R := PRemotes(Context);
  if Copy(Uri, 1, Length(Prefix)) <> Prefix then
    Exit;
  Rest := Copy(Uri, Length(Prefix) + 1, Length(Uri) - Length(Prefix));
  At := StringTableFind(R^.Cache, Rest);
  if At >= 0 then begin
    Doc := R^.Docs[R^.Cache.Values[At]];
    Result := True;
    Exit;
  end;
  if not ReadFile(R^.Root + '/' + Rest, Text) then
    Exit;
  if not ParseDocument(Text, Doc, E) then
    Exit;
  N := Length(R^.Docs);
  SetLength(R^.Docs, N + 1);
  R^.Docs[N] := Doc;
  StringTableAdd(R^.Cache, Rest, N, '');
  Result := True;
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
