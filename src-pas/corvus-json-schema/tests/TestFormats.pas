program TestFormats;
{$I corvus.inc}

// The tests of the format checks.
//
// The first part is the format tests of the JSON-Schema-Test-Suite (tests/<draft>/optional/format/*.json), run as
// suite_test.go of the Go module runs them: for draft 4, 6, 7, 2019-09 and 2020-12, each with the formats and the
// host name rules of its dialect. A value that is not a string is valid for every format, and so is every value for
// a format the dialect does not know. A case that fails and whose description contains "leap second" is counted as
// skipped and not as a failure, exactly as in the Go runner. No case is skipped before it has run.
//
// The second part is the cases of the Go tests that exercise formats.go directly: TestUnicodeFormatsUseUnicode17 and
// BenchmarkUnicodeFormats of unicode_test.go, and the format and content cases of api_test.go.
//
// The third part, which runs only when it is asked for, compares the checks with answers written out from the Go
// source for many more strings and numbers than the suite holds.
//
// Usage: TestFormats [suite-root] [--vectors file] [--numbers file]
//
// The suite is at suite-root, or at the path in JSON_SCHEMA_TEST_SUITE, or at ../../JSON-Schema-Test-Suite. Each
// line of a vectors file is a format kind, 1 for the host name rules of draft 4 and 6, the bytes of a string in
// hexadecimal and the expected answer (1 or 0), separated by tabs. Each line of a numbers file is a format kind, the
// number's flag, its data in hexadecimal, the expected answer and the number as it was written.
//
// When the program is built with CORVUS_STUBS defined, the regular expression unit is the stand-in of tests/stubs,
// which reads no pattern. The cases of the regex format then need the real unit: they are not run, and they are
// counted apart.
//
// The program prints each failure and the final counts, and exits with a status other than zero when anything
// failed.

uses
  SysUtils,
  Classes,
  Corvus.JsonSchema.Uri,
  Corvus.JsonSchema.Formats;

// ---------------------------------------------------------------------------------------------------------------------
// A reader of JSON text, enough for the files of the test suite

type
  TJsonKind = (JsonNull, JsonFalse, JsonTrue, JsonNumber, JsonString, JsonArray, JsonObject);

  // A value of a document. The members of an array or an object are a chain of values.
  TJsonNode = record
    Kind: TJsonKind;
    // The name of the member, when the value is a member of an object.
    Key: UTF8String;
    // The text of a string, with its escapes read, or a number as it was written.
    Text: UTF8String;
    FirstChild, NextSibling: Int32;
  end;

  TJsonReader = record
    Source: TBytes;
    Pos: Int32;
    Nodes: array of TJsonNode;
    Count: Int32;
    Failed: Boolean;
  end;

function NewNode(var R: TJsonReader; Kind: TJsonKind): Int32;
begin
  if R.Count = Length(R.Nodes) then SetLength(R.Nodes, 2 * R.Count + 64);
  Result := R.Count;
  Inc(R.Count);
  R.Nodes[Result].Kind := Kind;
  R.Nodes[Result].Key := '';
  R.Nodes[Result].Text := '';
  R.Nodes[Result].FirstChild := -1;
  R.Nodes[Result].NextSibling := -1;
end;

procedure SkipSpace(var R: TJsonReader);
begin
  while (R.Pos < Length(R.Source)) and (R.Source[R.Pos] in [9, 10, 13, 32]) do Inc(R.Pos);
end;

function Peek(const R: TJsonReader): Byte;
begin
  if R.Pos < Length(R.Source) then Result := R.Source[R.Pos]
  else Result := 0;
end;

// AppendRune appends a code point to the text as UTF-8.
procedure AppendRune(var S: UTF8String; C: Int32);
begin
  if C < $80 then S := S + UTF8String(AnsiChar(C))
  else if C < $800 then begin
    SetLength(S, Length(S) + 2);
    S[Length(S) - 1] := AnsiChar($C0 or (C shr 6));
    S[Length(S)] := AnsiChar($80 or (C and $3F));
  end else if C < $10000 then begin
    SetLength(S, Length(S) + 3);
    S[Length(S) - 2] := AnsiChar($E0 or (C shr 12));
    S[Length(S) - 1] := AnsiChar($80 or ((C shr 6) and $3F));
    S[Length(S)] := AnsiChar($80 or (C and $3F));
  end else begin
    SetLength(S, Length(S) + 4);
    S[Length(S) - 3] := AnsiChar($F0 or (C shr 18));
    S[Length(S) - 2] := AnsiChar($80 or ((C shr 12) and $3F));
    S[Length(S) - 1] := AnsiChar($80 or ((C shr 6) and $3F));
    S[Length(S)] := AnsiChar($80 or (C and $3F));
  end;
end;

function ReadHex4(var R: TJsonReader): Int32;
var
  I, V: Int32;
begin
  Result := 0;
  for I := 0 to 3 do begin
    V := -1;
    if R.Pos < Length(R.Source) then V := HexValue(R.Source[R.Pos]);
    if V < 0 then begin
      R.Failed := True;
      Exit;
    end;
    Result := Result * 16 + V;
    Inc(R.Pos);
  end;
end;

function ReadString(var R: TJsonReader): UTF8String;
var
  C: Byte;
  U, LowUnit: Int32;
begin
  Result := '';
  // The opening quote.
  Inc(R.Pos);
  while R.Pos < Length(R.Source) do begin
    C := R.Source[R.Pos];
    Inc(R.Pos);
    if C = Ord('"') then Exit;
    if C <> Ord('\') then begin
      SetLength(Result, Length(Result) + 1);
      Result[Length(Result)] := AnsiChar(C);
      Continue;
    end;
    C := Peek(R);
    Inc(R.Pos);
    case C of
      Ord('b'): AppendRune(Result, 8);
      Ord('f'): AppendRune(Result, 12);
      Ord('n'): AppendRune(Result, 10);
      Ord('r'): AppendRune(Result, 13);
      Ord('t'): AppendRune(Result, 9);
      Ord('u'):
        begin
          U := ReadHex4(R);
          if (U >= $D800) and (U <= $DBFF) and (R.Pos + 1 < Length(R.Source)) and (R.Source[R.Pos] = Ord('\')) and
            (R.Source[R.Pos + 1] = Ord('u')) then begin
            Inc(R.Pos, 2);
            LowUnit := ReadHex4(R);
            if (LowUnit >= $DC00) and (LowUnit <= $DFFF) then U := $10000 + ((U - $D800) shl 10) + (LowUnit - $DC00)
            else R.Failed := True;
          end else if (U >= $D800) and (U <= $DFFF) then R.Failed := True;
          AppendRune(Result, U);
        end;
    else
      // A quote, a backslash or a slash.
      AppendRune(Result, C);
    end;
  end;
  R.Failed := True;
end;

function ReadValue(var R: TJsonReader): Int32;
var
  Last, Child, Start, I: Int32;
  Key: UTF8String;
  C: Byte;
begin
  SkipSpace(R);
  C := Peek(R);
  if C = Ord('"') then begin
    Result := NewNode(R, JsonString);
    Key := ReadString(R);
    R.Nodes[Result].Text := Key;
  end else if (C = Ord('{')) or (C = Ord('[')) then begin
    if C = Ord('{') then Result := NewNode(R, JsonObject)
    else Result := NewNode(R, JsonArray);
    Inc(R.Pos);
    Last := -1;
    while not R.Failed do begin
      SkipSpace(R);
      if (Peek(R) = Ord('}')) or (Peek(R) = Ord(']')) then begin
        Inc(R.Pos);
        Exit;
      end;
      if Peek(R) = Ord(',') then begin
        Inc(R.Pos);
        Continue;
      end;
      Key := '';
      if C = Ord('{') then begin
        if Peek(R) <> Ord('"') then begin
          R.Failed := True;
          Exit;
        end;
        Key := ReadString(R);
        SkipSpace(R);
        if Peek(R) <> Ord(':') then begin
          R.Failed := True;
          Exit;
        end;
        Inc(R.Pos);
      end;
      Child := ReadValue(R);
      R.Nodes[Child].Key := Key;
      if Last < 0 then R.Nodes[Result].FirstChild := Child
      else R.Nodes[Last].NextSibling := Child;
      Last := Child;
    end;
  end else if C = Ord('t') then begin
    Result := NewNode(R, JsonTrue);
    Inc(R.Pos, 4);
  end else if C = Ord('f') then begin
    Result := NewNode(R, JsonFalse);
    Inc(R.Pos, 5);
  end else if C = Ord('n') then begin
    Result := NewNode(R, JsonNull);
    Inc(R.Pos, 4);
  end else if (C = Ord('-')) or IsASCIIDigit(C) then begin
    Result := NewNode(R, JsonNumber);
    Start := R.Pos;
    while (R.Pos < Length(R.Source)) and
      (R.Source[R.Pos] in [Ord('0')..Ord('9'), Ord('-'), Ord('+'), Ord('.'), Ord('e'), Ord('E')]) do Inc(R.Pos);
    Key := '';
    SetLength(Key, R.Pos - Start);
    for I := Start to R.Pos - 1 do Key[I - Start + 1] := AnsiChar(R.Source[I]);
    R.Nodes[Result].Text := Key;
  end else begin
    Result := NewNode(R, JsonNull);
    R.Failed := True;
  end;
end;

// Member is the value of a member of an object, or -1.
function Member(const R: TJsonReader; Node: Int32; const Name: UTF8String): Int32;
begin
  Result := R.Nodes[Node].FirstChild;
  while (Result >= 0) and not Utf8Equal(R.Nodes[Result].Key, Name) do Result := R.Nodes[Result].NextSibling;
end;

// ---------------------------------------------------------------------------------------------------------------------
// Files and text

function ReadFileBytes(const Path: UTF8String; out Bytes: TBytes): Boolean;
var
  Stream: TFileStream;
  Size, Got: Int32;
begin
  Result := False;
  Bytes := nil;
  if not FileExists(Path) then Exit;
  Stream := TFileStream.Create(Path, fmOpenRead or fmShareDenyNone);
  try
    Size := Int32(Stream.Size);
    SetLength(Bytes, Size);
    Got := 0;
    if Size > 0 then Got := Stream.Read(Bytes[0], Size);
    Result := Got = Size;
  finally
    Stream.Free;
  end;
end;

function BytesOf(const S: UTF8String): TBytes;
var
  I: Int32;
begin
  Result := nil;
  SetLength(Result, Length(S));
  for I := 1 to Length(S) do Result[I - 1] := Ord(S[I]);
end;

// Show is the text with every byte that is not printable ASCII written as \xNN.
function Show(const S: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 1 to Length(S) do begin
    if (Ord(S[I]) >= $20) and (Ord(S[I]) < $7F) then Result := Result + UTF8String(S[I])
    else Result := Result + UTF8String('\x' + IntToHex(Ord(S[I]), 2));
  end;
end;

// FromHex is the text whose bytes are written in hexadecimal.
function FromHex(const Hex: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := '';
  SetLength(Result, Length(Hex) div 2);
  for I := 1 to Length(Result) do begin
    Result[I] := AnsiChar(HexValue(Ord(Hex[2 * I - 1])) * 16 + HexValue(Ord(Hex[2 * I])));
  end;
end;

// U is the UTF-8 text of code points.
function U(const CodePoints: array of Int32): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 0 to High(CodePoints) do AppendRune(Result, CodePoints[I]);
end;

// ContainsLower reports whether the text, with its letters A to Z in lower case, contains the other text.
function ContainsLower(const S, Sub: UTF8String): Boolean;
var
  Lower: UTF8String;
  I, J: Int32;
begin
  Lower := AsciiLower(S);
  for I := 1 to Length(Lower) - Length(Sub) + 1 do begin
    J := 1;
    while (J <= Length(Sub)) and (Lower[I + J - 1] = Sub[J]) do Inc(J);
    if J > Length(Sub) then begin
      Result := True;
      Exit;
    end;
  end;
  Result := False;
end;

// ---------------------------------------------------------------------------------------------------------------------
// Counts

type
  TTally = record
    Name: UTF8String;
    Run, Passed, Skipped, Failed, NeedReal: Int32;
  end;

var
  Tallies: array of TTally;
  // The cases of the suite: all of them, those that ran, and how they ended.
  SuiteTotal: Int32 = 0;
  SuiteRun: Int32 = 0;
  SuitePassed: Int32 = 0;
  SuiteSkipped: Int32 = 0;
  SuiteFailed: Int32 = 0;
  SuiteNeedReal: Int32 = 0;
  SuiteNotFormat: Int32 = 0;
  SuiteFiles: Int32 = 0;
  // The other checks.
  Checks: Int32 = 0;
  Failures: Int32 = 0;
  VectorsRun: Int32 = 0;
  VectorsFailed: Int32 = 0;
  VectorsNeedReal: Int32 = 0;

function TallyOf(const Name: UTF8String): Int32;
var
  I: Int32;
begin
  for I := 0 to High(Tallies) do begin
    if Utf8Equal(Tallies[I].Name, Name) then begin
      Result := I;
      Exit;
    end;
  end;
  SetLength(Tallies, Length(Tallies) + 1);
  Result := High(Tallies);
  Tallies[Result].Name := Name;
  Tallies[Result].Run := 0;
  Tallies[Result].Passed := 0;
  Tallies[Result].Skipped := 0;
  Tallies[Result].Failed := 0;
  Tallies[Result].NeedReal := 0;
end;

procedure Check(const Name: UTF8String; Got, Want: Boolean);
begin
  Inc(Checks);
  if Got <> Want then begin
    Inc(Failures);
    WriteLn('FAIL: ', Name, ': got ', Got, ', want ', Want);
  end;
end;

// Padded is the text inside a larger buffer, so that a check that read outside its bounds would read other bytes.
// The text starts at the offset it gives.
function Padded(const S: UTF8String; out Start: Int32): TBytes;
const
  Before: UTF8String = 'a.b:/';
  After: UTF8String = '0Z@x:';
begin
  Result := BytesOf(Before + S + After);
  Start := Length(Before);
end;

// CheckKind runs the check of a format kind over the text.
function CheckKind(Kind: TFormatKind; const S: UTF8String; Legacy: Boolean): Boolean;
var
  Buffer: TBytes;
  Start: Int32;
begin
  Buffer := Padded(S, Start);
  Result := FormatCheckString(Kind, Buffer, Start, Length(S), Legacy);
end;

// ---------------------------------------------------------------------------------------------------------------------
// The JSON-Schema-Test-Suite

type
  TDraft = record
    Name: UTF8String;
    Dialect: Int32;
  end;

const
  SuiteDrafts: array[0..4] of TDraft = (
    (Name: 'draft4'; Dialect: FormatDialectDraft4), (Name: 'draft6'; Dialect: FormatDialectDraft6),
    (Name: 'draft7'; Dialect: FormatDialectDraft7), (Name: 'draft2019-09'; Dialect: FormatDialectDraft201909),
    (Name: 'draft2020-12'; Dialect: FormatDialectDraft202012));

procedure RunSuiteFile(const Path, FileLabel: UTF8String; Dialect: Int32);
var
  R: TJsonReader;
  Bytes: TBytes;
  Root, Group, Schema, Format, Test, Data, Valid, Description, M, T: Int32;
  Kind: TFormatKind;
  JustFormat, Actual, Expected, Legacy: Boolean;
  FormatName, GroupName, TestName: UTF8String;
  Named: TFormatCheck;
begin
  if not ReadFileBytes(Path, Bytes) then begin
    Inc(Failures);
    WriteLn('FAIL: cannot read ', Path);
    Exit;
  end;
  R.Source := Bytes;
  R.Pos := 0;
  R.Nodes := nil;
  R.Count := 0;
  R.Failed := False;
  Root := ReadValue(R);
  if R.Failed or (R.Nodes[Root].Kind <> JsonArray) then begin
    Inc(Failures);
    WriteLn('FAIL: cannot parse ', Path);
    Exit;
  end;
  Inc(SuiteFiles);
  Legacy := Dialect <= FormatDialectDraft6;
  Group := R.Nodes[Root].FirstChild;
  while Group >= 0 do begin
    GroupName := '';
    Description := Member(R, Group, 'description');
    if Description >= 0 then GroupName := R.Nodes[Description].Text;
    Schema := Member(R, Group, 'schema');
    // A schema that is just a format: an object with a format, and nothing else but the dialect it names.
    Format := -1;
    JustFormat := (Schema >= 0) and (R.Nodes[Schema].Kind = JsonObject);
    if JustFormat then begin
      Format := Member(R, Schema, 'format');
      JustFormat := (Format >= 0) and (R.Nodes[Format].Kind = JsonString);
      M := R.Nodes[Schema].FirstChild;
      while M >= 0 do begin
        if not (Utf8Equal(R.Nodes[M].Key, 'format') or Utf8Equal(R.Nodes[M].Key, '$schema')) then JustFormat := False;
        M := R.Nodes[M].NextSibling;
      end;
    end;
    Test := -1;
    M := Member(R, Group, 'tests');
    if M >= 0 then Test := R.Nodes[M].FirstChild;
    while Test >= 0 do begin
      Inc(SuiteTotal);
      Data := Member(R, Test, 'data');
      Valid := Member(R, Test, 'valid');
      Description := Member(R, Test, 'description');
      TestName := '';
      if Description >= 0 then TestName := R.Nodes[Description].Text;
      if (not JustFormat) or (Data < 0) or (Valid < 0) then begin
        Inc(SuiteNotFormat);
        WriteLn('not a format case: ', FileLabel, ' [', GroupName, '] ', TestName);
        Test := R.Nodes[Test].NextSibling;
        Continue;
      end;
      FormatName := R.Nodes[Format].Text;
      Kind := FormatKindOf(FormatName, Dialect);
      T := TallyOf(FormatName);
      Expected := R.Nodes[Valid].Kind = JsonTrue;
      {$IFDEF CORVUS_STUBS}
      if (Kind = FormatKindRegex) and (R.Nodes[Data].Kind = JsonString) then begin
        Inc(SuiteNeedReal);
        Inc(Tallies[T].NeedReal);
        Test := R.Nodes[Test].NextSibling;
        Continue;
      end;
      {$ENDIF}
      // Data that is not a string is valid for every format, and FormatCheckString accepts every string for a format
      // the dialect does not know and for a numeric format.
      if R.Nodes[Data].Kind = JsonString then Actual := CheckKind(Kind, R.Nodes[Data].Text, Legacy)
      else Actual := True;
      // The validator by name is the same check, for the formats the latest dialect knows.
      if (R.Nodes[Data].Kind = JsonString) and (Dialect = FormatDialectDraft202012) then begin
        Named := FormatValidator(FormatName);
        if Assigned(Named) then begin
          Bytes := BytesOf(R.Nodes[Data].Text);
          Check('FormatValidator(' + FormatName + ') agrees for "' + Show(R.Nodes[Data].Text) + '"',
            Named(Bytes, 0, Length(Bytes)), Actual);
        end else Check('FormatValidator(' + FormatName + ') is nil', Kind = FormatKindUnknown, True);
      end;
      Inc(SuiteRun);
      Inc(Tallies[T].Run);
      if Actual = Expected then begin
        Inc(SuitePassed);
        Inc(Tallies[T].Passed);
      end else if ContainsLower(TestName, 'leap second') then begin
        // Leap seconds are skipped in the format run, as in the Go and C# runners.
        Inc(SuiteSkipped);
        Inc(Tallies[T].Skipped);
        WriteLn('skipped: ', FileLabel, ' [', GroupName, '] ', TestName);
      end else begin
        Inc(SuiteFailed);
        Inc(Tallies[T].Failed);
        WriteLn('FAIL: ', FileLabel, ' [', GroupName, '] ', TestName, ': expected ', Expected, ', got ', Actual,
          ' for "', Show(R.Nodes[Data].Text), '"');
      end;
      Test := R.Nodes[Test].NextSibling;
    end;
    Group := R.Nodes[Group].NextSibling;
  end;
end;

procedure RunSuite(const Root: UTF8String);
var
  D, I, J, Count: Int32;
  Dir, Name: UTF8String;
  Names: array of UTF8String;
  Search: TSearchRec;
begin
  for D := 0 to High(SuiteDrafts) do begin
    Dir := Root + '/tests/' + SuiteDrafts[D].Name + '/optional/format/';
    Names := nil;
    Count := 0;
    if FindFirst(Dir + '*.json', faAnyFile, Search) = 0 then begin
      repeat
        SetLength(Names, Count + 1);
        Names[Count] := UTF8String(Search.Name);
        Inc(Count);
      until FindNext(Search) <> 0;
      FindClose(Search);
    end;
    // The files in the order of their names.
    for I := 1 to Count - 1 do begin
      Name := Names[I];
      J := I;
      while (J > 0) and (CompareStr(Names[J - 1], Name) > 0) do begin
        Names[J] := Names[J - 1];
        Dec(J);
      end;
      Names[J] := Name;
    end;
    for I := 0 to Count - 1 do begin
      RunSuiteFile(Dir + Names[I], SuiteDrafts[D].Name + '/optional/format/' + Names[I], SuiteDrafts[D].Dialect);
    end;
  end;
end;

// ---------------------------------------------------------------------------------------------------------------------
// The cases of the Go tests

// TestUnicodeFormatsUseUnicode17 checks format answers that differ between Unicode 15 and the Unicode 17 of the
// tables.
procedure TestUnicodeFormatsUseUnicode17;

  procedure Host(const Name: UTF8String; Want: Boolean);
  begin
    Check('isIDNHostname("' + Show(Name) + '")', CheckKind(FormatKindIDNHostname, Name, False), Want);
  end;

  procedure Address(const Name: UTF8String; Want: Boolean);
  begin
    Check('isEmail("' + Show(Name) + '", idn)', CheckKind(FormatKindIDNEmail, Name, False), Want);
  end;

begin
  // U+10D4A and U+10D4B are Garay letters, assigned in Unicode 16. U+1C89 is an uppercase letter of Unicode 16, and
  // IDNA2008 disallows uppercase letters. U+2FFFF is a noncharacter in every version.
  Host(U([$10D4A, $10D4B]) + '.example', True);
  Host(U([$1C8A]) + '.example', True);
  Host(U([$1C89]) + '.example', False);
  Host(U([$2FFFF]) + '.example', False);
  Address(U([$10D4A, $10D4B]) + '@example.com', True);
  Address(U([$16EA0, $16EBB]) + '@example.com', True);
  Address(U([$2FFFF]) + '@example.com', False);
  // "dokimi" in Greek letters.
  Address(U([$3B4, $3BF, $3BA, $3B9, $3BC, $3AE]) + '@example.com', True);
  Address('a b@example.com', False);
end;

// The strings BenchmarkUnicodeFormats of the Go source requires to be valid.
procedure TestUnicodeFormatStrings;
var
  Greek, Japanese, Arabic, Sample: UTF8String;
begin
  Greek := U([$3B5, $3BB, $3BB, $3B7, $3BD, $3B9, $3BA, $3AC]);
  Japanese := U([$4F8B, $3048]);
  Arabic := U([$628, $64A, $631, $648, $62A]);
  Check('isIDNHostname of a host name in Greek, Japanese and Arabic',
    CheckKind(FormatKindIDNHostname, Greek + '.' + Japanese + '.' + Arabic + '.example', False), True);
  Sample := U([$3C0, $3B1, $3C1, $3AC, $3B4, $3B5, $3B9, $3B3, $3BC, $3B1]);
  Check('isEmail of an address in Greek and Japanese', CheckKind(FormatKindIDNEmail,
    U([$3B4, $3BF, $3BA, $3B9, $3BC, $3AE]) + '.' + Japanese + '@' + Sample + '.example', False), True);
end;

// TestFormatIsAnAnnotationByDefaultAndAssertedOnRequest of api_test.go, as far as the checks go.
procedure TestIPv4Cases;
begin
  Check('ipv4 "not an address"', CheckKind(FormatKindIPv4, 'not an address', False), False);
  Check('ipv4 "10.0.0.1"', CheckKind(FormatKindIPv4, '10.0.0.1', False), True);
  Check('unknown format accepts anything', CheckKind(FormatKindUnknown, 'x', False), True);
  Check('a numeric format accepts every string', CheckKind(FormatKindInt32, 'x', False), True);
end;

// The content cases of api_test.go, as far as the base64 check goes.
procedure TestContent;
var
  Output: TBytes;
  Start, Len: Int32;

  procedure Decode(const Text: UTF8String; WantOK: Boolean; const Want: UTF8String);
  var
    Buffer: TBytes;
    Got: UTF8String;
    I: Int32;
    OK: Boolean;
  begin
    Buffer := Padded(Text, Start);
    OK := Base64Decode(Buffer, Start, Length(Text), Output, Len);
    Check('base64 "' + Show(Text) + '"', OK, WantOK);
    if OK and WantOK then begin
      Got := '';
      SetLength(Got, Len);
      for I := 0 to Len - 1 do Got[I + 1] := AnsiChar(Output[I]);
      Check('base64 "' + Show(Text) + '" decodes to "' + Show(Want) + '", got "' + Show(Got) + '"',
        Utf8Equal(Got, Want), True);
    end;
  end;

begin
  Output := nil;
  Decode('eyJhIjogMX0=', True, '{"a": 1}');
  Decode('bm90IGpzb24=', True, 'not json');
  Decode('not base64', False, '');
  Decode('', True, '');
  Decode('QQ==', True, 'A');
  Decode('QUI=', True, 'AB');
  Decode('QUJD', True, 'ABC');
  Decode('QUJDRA==', True, 'ABCD');
  Decode('+/+/', True, FromHex('fbffbf'));
  Decode('QQ', False, '');
  Decode('QQ=', False, '');
  Decode('Q===', False, '');
  Decode('====', False, '');
  Decode('QQ==QUJD', False, '');
  Decode('QUJD'#10'QUJD', False, '');
  Decode('QU-D', False, '');
  Decode('QU_D', False, '');
  Decode('Q=JD', False, '');
end;

// The formats each dialect knows, with their names and messages.
procedure TestFormatKinds;
const
  // The first dialect that knows each string format.
  Since: array[FormatKindDate..FormatKindRegex] of Int32 = (
    FormatDialectDraft7, FormatDialectDraft7, FormatDialectDraft4, FormatDialectDraft201909,
    FormatDialectDraft201909, FormatDialectDraft4, FormatDialectDraft4, FormatDialectDraft4, FormatDialectDraft7,
    FormatDialectDraft4, FormatDialectDraft7, FormatDialectDraft4, FormatDialectDraft6, FormatDialectDraft7,
    FormatDialectDraft7, FormatDialectDraft6, FormatDialectDraft6, FormatDialectDraft7, FormatDialectDraft7);
var
  Kind, Got: TFormatKind;
  Dialect: Int32;
  Name: UTF8String;
begin
  for Kind := Low(TFormatKind) to High(TFormatKind) do begin
    Name := FormatKindName(Kind);
    Check('isNumeric of ' + Name, FormatKindIsNumeric(Kind), Kind >= FormatKindByte);
    for Dialect := FormatDialectDraft4 to FormatDialectDraft202012 do begin
      Got := FormatKindOf(Name, Dialect);
      if (Kind >= FormatKindDate) and (Kind <= FormatKindRegex) then begin
        if Dialect >= Since[Kind] then Check('formatKindOf ' + Name, Got = Kind, True)
        else Check('formatKindOf ' + Name + ' before its dialect', Got = FormatKindUnknown, True);
      end else Check('formatKindOf ' + Name, Got = Kind, True);
    end;
    Check('message of ' + Name, Length(FormatKindMessage(Kind)) <> 0,
      (Kind >= FormatKindDate) and (Kind <= FormatKindRegex));
    Check('validator of ' + Name, Assigned(FormatKindValidator(Kind, False)),
      (Kind >= FormatKindDate) and (Kind <= FormatKindRegex));
  end;
  Check('formatKindOf float', FormatKindOf('float', FormatDialectDraft4) = FormatKindSingle, True);
  Check('formatKindOf of no format', FormatKindOf('no-such-format', FormatDialectDraft202012) = FormatKindUnknown,
    True);
  Check('formatKindOf of the empty name', FormatKindOf('', FormatDialectDraft202012) = FormatKindUnknown, True);
  Check('formatKindOf is case sensitive', FormatKindOf('Date', FormatDialectDraft202012) = FormatKindUnknown, True);
  Check('FormatValidator of no format', Assigned(FormatValidator('no-such-format')), False);
  Check('FormatValidator of a numeric format', Assigned(FormatValidator('int32')), False);
  Check('FormatValidator of ipv4', Assigned(FormatValidator('ipv4')), True);
  Check('message of date-time',
    Utf8Equal(FormatKindMessage(FormatKindDateTime), 'Expected an ISO8601 Offset DateTime string.'), True);
  Check('message of a numeric format', Length(FormatKindMessage(FormatKindInt32)) = 0, True);
end;

// ---------------------------------------------------------------------------------------------------------------------
// Answers written out from the Go source

// Field gives the next field of a line of tab-separated text, moving the position past it.
function Field(const Bytes: TBytes; var Pos: Int32): UTF8String;
var
  Start, I: Int32;
begin
  Start := Pos;
  while (Pos < Length(Bytes)) and (Bytes[Pos] <> 9) and (Bytes[Pos] <> 10) do Inc(Pos);
  Result := '';
  SetLength(Result, Pos - Start);
  for I := Start to Pos - 1 do Result[I - Start + 1] := AnsiChar(Bytes[I]);
  if (Pos < Length(Bytes)) and (Bytes[Pos] = 9) then Inc(Pos);
end;

function ToInt(const S: UTF8String): Int32;
var
  I: Int32;
begin
  Result := 0;
  for I := 1 to Length(S) do Result := Result * 10 + (Ord(S[I]) - Ord('0'));
end;

procedure RunVectors(const Path: UTF8String);
var
  Bytes: TBytes;
  Pos, Shown: Int32;
  Kind: TFormatKind;
  Legacy, Want, Got: Boolean;
  Text: UTF8String;
begin
  if not ReadFileBytes(Path, Bytes) then begin
    Inc(Failures);
    WriteLn('FAIL: cannot read ', Path);
    Exit;
  end;
  Pos := 0;
  Shown := 0;
  while Pos < Length(Bytes) do begin
    Kind := TFormatKind(ToInt(Field(Bytes, Pos)));
    Legacy := ToInt(Field(Bytes, Pos)) = 1;
    Text := FromHex(Field(Bytes, Pos));
    Want := ToInt(Field(Bytes, Pos)) = 1;
    while (Pos < Length(Bytes)) and (Bytes[Pos] <> 10) do Inc(Pos);
    Inc(Pos);
    {$IFDEF CORVUS_STUBS}
    if Kind = FormatKindRegex then begin
      Inc(VectorsNeedReal);
      Continue;
    end;
    {$ENDIF}
    Got := CheckKind(Kind, Text, Legacy);
    Inc(VectorsRun);
    if Got <> Want then begin
      Inc(VectorsFailed);
      Inc(Shown);
      if Shown <= 200 then begin
        WriteLn('FAIL: vector ', FormatKindName(Kind), ' legacy=', Legacy, ' "', Show(Text), '": got ', Got, ', want ',
          Want);
      end;
    end;
  end;
end;

procedure RunNumbers(const Path: UTF8String);
var
  Bytes: TBytes;
  Pos, Flag, I: Int32;
  Kind: TFormatKind;
  Data: UInt64;
  Want, Got: Boolean;
  Hex, Text: UTF8String;
begin
  if not ReadFileBytes(Path, Bytes) then begin
    Inc(Failures);
    WriteLn('FAIL: cannot read ', Path);
    Exit;
  end;
  Pos := 0;
  while Pos < Length(Bytes) do begin
    Kind := TFormatKind(ToInt(Field(Bytes, Pos)));
    Flag := ToInt(Field(Bytes, Pos));
    Hex := Field(Bytes, Pos);
    Data := 0;
    for I := 1 to Length(Hex) do Data := (Data shl 4) or UInt64(HexValue(Ord(Hex[I])));
    Want := ToInt(Field(Bytes, Pos)) = 1;
    Text := Field(Bytes, Pos);
    while (Pos < Length(Bytes)) and (Bytes[Pos] <> 10) do Inc(Pos);
    Inc(Pos);
    Got := FormatCheckNumber(Kind, Flag, Data);
    Inc(VectorsRun);
    if Got <> Want then begin
      Inc(VectorsFailed);
      WriteLn('FAIL: number ', FormatKindName(Kind), ' ', Text, ' (flag ', Flag, ', data ', Hex, '): got ', Got,
        ', want ', Want);
    end;
  end;
end;

// The numeric formats, for numbers as a document holds them.
procedure TestNumbers;

  procedure Whole(Kind: TFormatKind; Value: Int64; Want: Boolean);
  begin
    Check(FormatKindName(Kind) + ' of ' + UTF8String(IntToStr(Value)),
      FormatCheckNumber(Kind, FormatNumInt, UInt64(Value)), Want);
  end;

  procedure Real(Kind: TFormatKind; Bits: UInt64; Want: Boolean);
  begin
    Check(FormatKindName(Kind) + ' of the double with bits ' + UTF8String(IntToHex(Int64(Bits), 16)),
      FormatCheckNumber(Kind, FormatNumFloat, Bits), Want);
  end;

begin
  Whole(FormatKindByte, 0, True);
  Whole(FormatKindByte, 255, True);
  Whole(FormatKindByte, 256, False);
  Whole(FormatKindByte, -1, False);
  Whole(FormatKindSByte, -128, True);
  Whole(FormatKindSByte, -129, False);
  Whole(FormatKindSByte, 127, True);
  Whole(FormatKindSByte, 128, False);
  Whole(FormatKindInt16, -32768, True);
  Whole(FormatKindInt16, 32768, False);
  Whole(FormatKindUInt16, 65535, True);
  Whole(FormatKindUInt16, 65536, False);
  Whole(FormatKindInt32, -2147483648, True);
  Whole(FormatKindInt32, 2147483648, False);
  Whole(FormatKindUInt32, 4294967295, True);
  Whole(FormatKindUInt32, 4294967296, False);
  Whole(FormatKindInt64, Low(Int64), True);
  Whole(FormatKindInt64, High(Int64), True);
  Whole(FormatKindUInt64, High(Int64), True);
  Whole(FormatKindUInt64, -1, False);
  Whole(FormatKindUInt128, -1, False);
  Whole(FormatKindInt128, Low(Int64), True);
  // An integer of 2^63 or more is held as a UInt64.
  Check('uint64 of 2^64-1', FormatCheckNumber(FormatKindUInt64, FormatNumUint, High(UInt64)), True);
  Check('int64 of 2^63', FormatCheckNumber(FormatKindInt64, FormatNumUint, UInt64($8000000000000000)), False);
  Check('uint32 of 2^63', FormatCheckNumber(FormatKindUInt32, FormatNumUint, UInt64($8000000000000000)), False);
  Check('uint128 of 2^64-1', FormatCheckNumber(FormatKindUInt128, FormatNumUint, High(UInt64)), True);
  // 1.5, 2^63, 2^64, 65504, 65505, and the infinities.
  Real(FormatKindInt32, UInt64($3FF8000000000000), False);
  Real(FormatKindDouble, UInt64($3FF8000000000000), True);
  Real(FormatKindInt64, UInt64($43E0000000000000), True);
  Real(FormatKindUInt64, UInt64($43F0000000000000), True);
  Real(FormatKindUInt32, UInt64($43F0000000000000), False);
  Real(FormatKindHalf, UInt64($40EFFC0000000000), True);
  Real(FormatKindHalf, UInt64($40EFFC2000000000), False);
  Real(FormatKindDouble, UInt64($7FF0000000000000), False);
  Real(FormatKindDouble, UInt64($FFF0000000000000), False);
  Real(FormatKindInt128, UInt64($7FF0000000000000), False);
end;

// ---------------------------------------------------------------------------------------------------------------------

var
  SuiteRoot, VectorsPath, NumbersPath, Arg: UTF8String;
  I: Int32;
begin
  SuiteRoot := UTF8String(GetEnvironmentVariable('JSON_SCHEMA_TEST_SUITE'));
  if Length(SuiteRoot) = 0 then SuiteRoot := '../../JSON-Schema-Test-Suite';
  VectorsPath := '';
  NumbersPath := '';
  I := 1;
  while I <= ParamCount do begin
    Arg := UTF8String(ParamStr(I));
    if Utf8Equal(Arg, '--vectors') and (I < ParamCount) then begin
      Inc(I);
      VectorsPath := UTF8String(ParamStr(I));
    end else if Utf8Equal(Arg, '--numbers') and (I < ParamCount) then begin
      Inc(I);
      NumbersPath := UTF8String(ParamStr(I));
    end else SuiteRoot := Arg;
    Inc(I);
  end;

  Tallies := nil;
  RunSuite(SuiteRoot);
  TestFormatKinds;
  TestIPv4Cases;
  TestContent;
  TestNumbers;
  TestUnicodeFormatsUseUnicode17;
  TestUnicodeFormatStrings;
  if Length(VectorsPath) <> 0 then RunVectors(VectorsPath);
  if Length(NumbersPath) <> 0 then RunNumbers(NumbersPath);

  WriteLn;
  WriteLn('JSON-Schema-Test-Suite format cases, by format (run, passed, skipped, failed, needing the real unit):');
  for I := 0 to High(Tallies) do begin
    WriteLn('  ', Tallies[I].Name, ': ', Tallies[I].Run, ' run, ', Tallies[I].Passed, ' passed, ',
      Tallies[I].Skipped, ' skipped, ', Tallies[I].Failed, ' failed, ', Tallies[I].NeedReal, ' need the real unit');
  end;
  WriteLn('JSON-Schema-Test-Suite: ', SuiteFiles, ' files, ', SuiteTotal, ' cases, ', SuiteRun, ' run, ',
    SuitePassed, ' passed, ', SuiteSkipped, ' skipped (leap second), ', SuiteFailed, ' failed, ', SuiteNeedReal,
    ' need the real regular expression unit, ', SuiteNotFormat, ' not format cases');
  WriteLn('Other checks: ', Checks, ' run, ', Checks - Failures, ' passed, ', Failures, ' failed');
  if (Length(VectorsPath) <> 0) or (Length(NumbersPath) <> 0) then begin
    WriteLn('Answers of the Go source: ', VectorsRun, ' run, ', VectorsRun - VectorsFailed, ' passed, ',
      VectorsFailed, ' failed, ', VectorsNeedReal, ' need the real regular expression unit');
  end;
  if SuiteTotal = 0 then begin
    WriteLn('FAIL: no JSON-Schema-Test-Suite cases ran (is the suite at ', SuiteRoot, '?)');
    Halt(1);
  end;
  if (SuiteFailed <> 0) or (Failures <> 0) or (VectorsFailed <> 0) then Halt(1);
end.
