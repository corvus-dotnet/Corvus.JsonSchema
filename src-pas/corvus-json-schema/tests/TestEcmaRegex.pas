program TestEcmaRegex;
{$I corvus.inc}

{ The tests of the ECMA-262 regular expression engine, ported from ecmaregex_test.go, oracle_test.go and
  suite_test.go of the ecmaregex package of the Go module. The program prints each failure and a final count, and
  exits with a status other than zero when anything failed.

  Usage: TestEcmaRegex [oracle [suite]]

  The first argument is the path of testdata/v8_oracle.json of the Go package, which holds what V8 answers for more
  than 60,000 patterns. Without it the program reads the file by its path from the directory of the Pascal package
  (src-pas/corvus-json-schema), so run it from there. The second argument is the root of the JSON Schema Test Suite.
  Without it the program reads the environment variable CORVUS_JSON_SCHEMA_TEST_SUITE, and then the submodule at the
  root of the repository. The tests that read the suite are skipped when it is not there, as in Go.

  What differs from the Go tests:

  The Go package has two back ends, the regexp package of the standard library and its own backtracking matcher, and
  the tests compare them. Here the two are the automaton over ASCII text (where the pattern has one) and the
  backtracking matcher, so "both back ends" means a pattern as compiled and the same pattern with its automaton
  taken away. Where a Go test names the back end a pattern takes, the test here checks the Regular field, which
  says the Go package would run the pattern on the regexp package.

  TestTranslationCarriesNoUnicodeClass is not here, because there is no translation to the regexp package. The
  benchmarks are not here.

  The patterns and texts of the tables below are the ones of the Go source, converted by a script, so a character
  that is hard to see is written as a character code. }

uses
  {$IFDEF UNIX}
  cthreads,
  {$ENDIF}
  SysUtils,
  Classes,
  Corvus.JsonSchema.Ucd,
  Corvus.JsonSchema.EcmaRegex.Utf8,
  Corvus.JsonSchema.EcmaRegex.CharSet,
  Corvus.JsonSchema.EcmaRegex.Dfa,
  Corvus.JsonSchema.EcmaRegex;

type
  TTexts = array of UTF8String;

var
  Failures: Int32 = 0;
  { What the comparison with V8 came to: the verdicts on whether a pattern is valid, and the answers on whether a
    pattern matches a text, counted once for each of the two back ends. }
  OracleVerdicts: Int64 = 0;
  OracleVerdictsAgreed: Int64 = 0;
  OracleAnswers: Int64 = 0;
  OracleAnswersAgreed: Int64 = 0;
  OraclePath: UTF8String = '../../src-go/corvus-json-schema/internal/ecmaregex/testdata/v8_oracle.json';
  SuitePath: UTF8String = '';

procedure Fail(const Message: UTF8String);
begin
  Inc(Failures);
  // A broken engine fails tens of thousands of cases. The first few hundred say what is wrong.
  if Failures <= 300 then
    WriteLn('FAIL: ', Message);
end;

function Num(N: Int64): UTF8String;
begin
  Result := UTF8String(IntToStr(N));
end;

function Bool(B: Boolean): UTF8String;
begin
  if B then
    Result := 'true'
  else
    Result := 'false';
end;

function Quote(const S: UTF8String): UTF8String;
begin
  Result := '"' + S + '"';
end;

function ToBytes(const S: UTF8String): TBytes;
var
  K: Int32;
begin
  SetLength(Result, Length(S));
  for K := 1 to Length(S) do
    Result[K - 1] := UInt8(S[K]);
end;

function HexDigit(C: AnsiChar): Int32;
begin
  if C <= '9' then
    Result := Ord(C) - Ord('0')
  else if C <= 'F' then
    Result := Ord(C) - Ord('A') + 10
  else
    Result := Ord(C) - Ord('a') + 10;
end;

{ HexBytes returns the bytes written as hexadecimal digits. It is how a text that is not UTF-8 is written here. }
function HexBytes(const Hex: UTF8String): UTF8String;
var
  K: Int32;
begin
  SetLength(Result, Length(Hex) div 2);
  for K := 1 to Length(Result) do
    Result[K] := AnsiChar(16 * HexDigit(Hex[2 * K - 1]) + HexDigit(Hex[2 * K]));
end;

{ U returns a code point as UTF-8. It is how a code point that has no character yet is written here. }
function U(R: Int32): UTF8String;
begin
  Result := '';
  AppendRuneString(Result, R);
end;

function MatchString(const Re: TEcmaRegex; const S: UTF8String): Boolean;
var
  Text: TBytes;
begin
  Text := ToBytes(S);
  Result := EcmaRegexIsMatch(Re, Text, 0, Length(Text));
end;

{ Backtracking returns the pattern without its automaton, so that every text runs on the backtracking matcher. It
  is what compile with forceBacktrack is in the Go tests. }
function Backtracking(const Re: TEcmaRegex): TEcmaRegex;
begin
  Result := Re;
  Result.HasDfa := False;
end;

{ Compiled compiles a pattern that must be valid, and reports a failure when it is not. }
function Compiled(const Pattern: UTF8String; out Re: TEcmaRegex): Boolean;
var
  Error: UTF8String;
begin
  Result := EcmaRegexCompile(Pattern, Re, Error);
  if not Result then
    Fail('Compile(' + Quote(Pattern) + '): ' + Error);
end;

{ SortTexts sorts the elements Lo to Hi by their bytes. }
procedure SortTexts(var A: TTexts; Lo, Hi: Int32);
var
  I, J: Int32;
  Pivot, Swap: UTF8String;
begin
  while Lo < Hi do begin
    I := Lo;
    J := Hi;
    Pivot := A[Lo + (Hi - Lo) div 2];
    repeat
      while A[I] < Pivot do
        Inc(I);
      while A[J] > Pivot do
        Dec(J);
      if I <= J then begin
        Swap := A[I];
        A[I] := A[J];
        A[J] := Swap;
        Inc(I);
        Dec(J);
      end;
    until I > J;
    if J - Lo < Hi - I then begin
      SortTexts(A, Lo, J);
      Lo := I;
    end else begin
      SortTexts(A, I, Hi);
      Hi := J;
    end;
  end;
end;

{ SortedSet sorts the first Count elements and drops the ones that repeat. It is what a Go map with the texts as
  its keys comes to. }
procedure SortedSet(var A: TTexts; Count: Int32);
var
  I, N: Int32;
begin
  SortTexts(A, 0, Count - 1);
  N := 0;
  for I := 0 to Count - 1 do
    if (N = 0) or (A[I] <> A[N - 1]) then begin
      A[N] := A[I];
      Inc(N);
    end;
  SetLength(A, N);
end;

procedure AppendText(var A: TTexts; var Count: Int32; const S: UTF8String);
begin
  if Count = Length(A) then
    SetLength(A, 2 * Count + 64);
  A[Count] := S;
  Inc(Count);
end;

{ The JSON the tests read: the oracle and the files of the suite. This is the least that reads them, and it trusts
  them to be well formed. }

type
  TJsonKind = (JNull, JFalse, JTrue, JNumber, JString, JArray, JObject);

  { A value. The members of an object and the items of an array are a chain: First is the first and Next the one
    after. A member has its name in Key. }
  TJsonNode = record
    Kind: TJsonKind;
    Text: UTF8String;
    Number: Double;
    Key: UTF8String;
    First, Next: Int32;
  end;

  TJson = record
    Nodes: array of TJsonNode;
    Count: Int32;
    Data: TBytes;
    At: Int32;
    Ok: Boolean;
    function NewNode(Kind: TJsonKind): Int32;
    procedure Space;
    function ReadString: UTF8String;
    function ReadValue: Int32;
    { Member returns the member of an object with a name, or -1. }
    function Member(Obj: Int32; const Name: UTF8String): Int32;
    { Text returns the string a member holds and Flag whether a member is true. A member that is not there is
      the empty string, or false. }
    function Text(Obj: Int32; const Name: UTF8String): UTF8String;
    function Flag(Obj: Int32; const Name: UTF8String): Boolean;
  end;

function TJson.NewNode(Kind: TJsonKind): Int32;
begin
  if Count = Length(Nodes) then
    SetLength(Nodes, 2 * Count + 256);
  Result := Count;
  Inc(Count);
  Nodes[Result].Kind := Kind;
  Nodes[Result].First := -1;
  Nodes[Result].Next := -1;
end;

procedure TJson.Space;
begin
  while (At < Length(Data)) and (Data[At] in [9, 10, 13, 32]) do
    Inc(At);
end;

function TJson.ReadString: UTF8String;
var
  C: UInt8;
  R, Low, K, N: Int32;
  Buf: TBytes;

  procedure Put(B: UInt8);
  begin
    if N = Length(Buf) then
      SetLength(Buf, 2 * N + 32);
    Buf[N] := B;
    Inc(N);
  end;

  procedure PutRune(Rune: Int32);
  var
    Encoded: TBytes;
    I: Int32;
  begin
    Encoded := nil;
    AppendRune(Encoded, Rune);
    for I := 0 to High(Encoded) do
      Put(Encoded[I]);
  end;

  function Hex4(From: Int32): Int32;
  var
    I: Int32;
  begin
    Result := 0;
    for I := From to From + 3 do
      Result := Result * 16 + HexDigit(AnsiChar(Data[I]));
  end;

begin
  Buf := nil;
  N := 0;
  // The position is at the opening quote.
  Inc(At);
  while Data[At] <> Ord('"') do begin
    C := Data[At];
    Inc(At);
    if C <> Ord('\') then begin
      Put(C);
      Continue;
    end;
    C := Data[At];
    Inc(At);
    case Char(C) of
      'n': Put(10);
      'r': Put(13);
      't': Put(9);
      'b': Put(8);
      'f': Put(12);
      'u': begin
        R := Hex4(At);
        Inc(At, 4);
        // A surrogate pair is one code point. A surrogate alone is U+FFFD, as Go's encoding/json reads it.
        if (R >= $D800) and (R <= $DBFF) and (Data[At] = Ord('\')) and (Data[At + 1] = Ord('u')) then begin
          Low := Hex4(At + 2);
          if (Low >= $DC00) and (Low <= $DFFF) then begin
            R := $10000 + ((R - $D800) shl 10) + (Low - $DC00);
            Inc(At, 6);
          end;
        end;
        PutRune(R);
      end;
    else
      Put(C);
    end;
  end;
  Inc(At);
  SetLength(Result, N);
  for K := 1 to N do
    Result[K] := AnsiChar(Buf[K - 1]);
end;

function TJson.ReadValue: Int32;
var
  Last, Item, Start, Code: Int32;
  Name: UTF8String;
begin
  Space;
  case Char(Data[At]) of
    '{': begin
      Result := NewNode(JObject);
      Inc(At);
      Last := -1;
      Space;
      while Data[At] <> Ord('}') do begin
        Space;
        Name := ReadString;
        Space;
        Inc(At);
        Item := ReadValue;
        Nodes[Item].Key := Name;
        if Last < 0 then
          Nodes[Result].First := Item
        else
          Nodes[Last].Next := Item;
        Last := Item;
        Space;
        if Data[At] = Ord(',') then
          Inc(At);
        Space;
      end;
      Inc(At);
    end;
    '[': begin
      Result := NewNode(JArray);
      Inc(At);
      Last := -1;
      Space;
      while Data[At] <> Ord(']') do begin
        Item := ReadValue;
        if Last < 0 then
          Nodes[Result].First := Item
        else
          Nodes[Last].Next := Item;
        Last := Item;
        Space;
        if Data[At] = Ord(',') then
          Inc(At);
        Space;
      end;
      Inc(At);
    end;
    '"': begin
      Name := ReadString;
      Result := NewNode(JString);
      Nodes[Result].Text := Name;
    end;
    't': begin
      Result := NewNode(JTrue);
      Inc(At, 4);
    end;
    'f': begin
      Result := NewNode(JFalse);
      Inc(At, 5);
    end;
    'n': begin
      Result := NewNode(JNull);
      Inc(At, 4);
    end;
  else
    Result := NewNode(JNumber);
    Start := At;
    while (At < Length(Data)) and (Char(Data[At]) in ['0'..'9', '-', '+', '.', 'e', 'E']) do
      Inc(At);
    SetLength(Name, At - Start);
    for Item := 1 to At - Start do
      Name[Item] := AnsiChar(Data[Start + Item - 1]);
    Val(Name, Nodes[Result].Number, Code);
    if (Code <> 0) or (At = Start) then
      Ok := False;
  end;
end;

function TJson.Member(Obj: Int32; const Name: UTF8String): Int32;
begin
  Result := Nodes[Obj].First;
  while (Result >= 0) and (Nodes[Result].Key <> Name) do
    Result := Nodes[Result].Next;
end;

function TJson.Text(Obj: Int32; const Name: UTF8String): UTF8String;
var
  V: Int32;
begin
  V := Member(Obj, Name);
  if V < 0 then
    Exit('');
  Result := Nodes[V].Text;
end;

function TJson.Flag(Obj: Int32; const Name: UTF8String): Boolean;
var
  V: Int32;
begin
  V := Member(Obj, Name);
  Result := (V >= 0) and (Nodes[V].Kind = JTrue);
end;

function ReadFile(const Path: UTF8String; out Data: TBytes): Boolean;
var
  Stream: TFileStream;
begin
  Data := nil;
  if not FileExists(Path) then
    Exit(False);
  Stream := TFileStream.Create(Path, fmOpenRead or fmShareDenyWrite);
  try
    SetLength(Data, Stream.Size);
    if Length(Data) > 0 then
      Stream.ReadBuffer(Data[0], Length(Data));
  finally
    Stream.Free;
  end;
  Result := True;
end;

{ LoadJson reads a file of JSON. Root is its value. }
function LoadJson(const Path: UTF8String; out Json: TJson; out Root: Int32): Boolean;
begin
  Json := Default(TJson);
  Root := -1;
  if not ReadFile(Path, Json.Data) then
    Exit(False);
  Json.Ok := True;
  Root := Json.ReadValue;
  Result := Json.Ok;
end;

{ The JSON Schema Test Suite. }

{ SuiteDir returns the tests directory of the JSON Schema Test Suite, and whether it is there. The suite is a
  submodule at the root of the repository, and CORVUS_JSON_SCHEMA_TEST_SUITE names another copy. }
function SuiteDir(out Tests: UTF8String): Boolean;
var
  Root: UTF8String;
begin
  Root := SuitePath;
  if Root = '' then
    Root := UTF8String(GetEnvironmentVariable('CORVUS_JSON_SCHEMA_TEST_SUITE'));
  if Root = '' then
    Root := '../../JSON-Schema-Test-Suite';
  Tests := Root + '/tests';
  Result := DirectoryExists(Tests);
end;

{ ListDir lists the names in a directory: the directories in it, or the files. }
function ListDir(const Dir: UTF8String; Directories: Boolean): TTexts;
var
  Found: TSearchRec;
  Count: Int32;
begin
  Result := nil;
  Count := 0;
  if FindFirst(Dir + '/*', faAnyFile, Found) = 0 then begin
    repeat
      if (Found.Name <> '.') and (Found.Name <> '..') and (((Found.Attr and faDirectory) <> 0) = Directories) then
        AppendText(Result, Count, UTF8String(Found.Name));
    until FindNext(Found) <> 0;
    FindClose(Found);
  end;
  SetLength(Result, Count);
  SortTexts(Result, 0, Count - 1);
end;

{ SuiteFiles lists the files of the suite that exercise regular expressions, for every draft that has them. }
function SuiteFiles(const Tests: UTF8String): TTexts;
const
  Names: array[0..4] of UTF8String = ('optional/ecmascript-regex.json', 'optional/format/regex.json',
    'pattern.json', 'patternProperties.json', 'optional/non-bmp-regex.json');
var
  Drafts: TTexts;
  Count, N, D: Int32;
  Path: UTF8String;
begin
  Result := nil;
  Count := 0;
  Drafts := ListDir(Tests, True);
  for N := 0 to High(Names) do
    for D := 0 to High(Drafts) do begin
      Path := Tests + '/' + Drafts[D] + '/' + Names[N];
      if FileExists(Path) then
        AppendText(Result, Count, Path);
    end;
  SetLength(Result, Count);
  SortTexts(Result, 0, Count - 1);
  if Count = 0 then
    Fail('no regular expression files under ' + Tests);
end;

type
  { A TSuiteEvaluator evaluates the few keywords the regular expression files of the suite use, taking every
    pattern through EcmaRegexCompile and EcmaRegexIsMatch and the regex format through EcmaRegexValid. }
  TSuiteEvaluator = record
    Json: TJson;
    Patterns: TTexts;
    Regexes: array of TEcmaRegex;
    Count: Int32;
    { Broken says a file used something this evaluator does not know, which fails the test. }
    Broken: Boolean;
    function Pattern(const P: UTF8String): Int32;
    function HasType(const Name: UTF8String; Data: Int32): Boolean;
    function Valid(Schema, Data: Int32): Boolean;
  end;

function TSuiteEvaluator.Pattern(const P: UTF8String): Int32;
var
  K: Int32;
  Error: UTF8String;
begin
  for K := 0 to Count - 1 do
    if Patterns[K] = P then
      Exit(K);
  if Count = Length(Patterns) then begin
    SetLength(Patterns, 2 * Count + 64);
    SetLength(Regexes, 2 * Count + 64);
  end;
  Result := Count;
  Inc(Count);
  Patterns[Result] := P;
  if not EcmaRegexCompile(P, Regexes[Result], Error) then begin
    Fail('Compile(' + Quote(P) + '): ' + Error);
    Broken := True;
    // A pattern that matches nothing stands in, so that the run can go on.
    EcmaRegexCompile('[]', Regexes[Result], Error);
  end;
end;

function TSuiteEvaluator.HasType(const Name: UTF8String; Data: Int32): Boolean;
var
  Kind: TJsonKind;
  F: Double;
begin
  Kind := Json.Nodes[Data].Kind;
  if Name = 'string' then
    Exit(Kind = JString);
  if Name = 'object' then
    Exit(Kind = JObject);
  if Name = 'array' then
    Exit(Kind = JArray);
  if Name = 'boolean' then
    Exit(Kind in [JTrue, JFalse]);
  if Name = 'null' then
    Exit(Kind = JNull);
  if Name = 'number' then
    Exit(Kind = JNumber);
  if Name = 'integer' then begin
    F := Json.Nodes[Data].Number;
    Exit((Kind = JNumber) and (Abs(F) < 9.0e18) and (F = Trunc(F)));
  end;
  Result := Name = 'any';
end;

function TSuiteEvaluator.Valid(Schema, Data: Int32): Boolean;
var
  Keyword, Value, Name, Sub, Prop, Others, Other, Ix: Int32;
  Key: UTF8String;
  Matched, Additional: Boolean;
begin
  if Json.Nodes[Schema].Kind in [JTrue, JFalse] then
    Exit(Json.Nodes[Schema].Kind = JTrue);
  if Json.Nodes[Schema].Kind <> JObject then begin
    Fail('unexpected schema');
    Broken := True;
    Exit(False);
  end;
  Keyword := Json.Nodes[Schema].First;
  while Keyword >= 0 do begin
    Value := Keyword;
    Key := Json.Nodes[Keyword].Key;
    if Key = '$schema' then begin
    end else if Key = 'type' then begin
      Matched := False;
      if Json.Nodes[Value].Kind = JString then
        Matched := HasType(Json.Nodes[Value].Text, Data)
      else begin
        Name := Json.Nodes[Value].First;
        while Name >= 0 do begin
          Matched := Matched or HasType(Json.Nodes[Name].Text, Data);
          Name := Json.Nodes[Name].Next;
        end;
      end;
      if not Matched then
        Exit(False);
    end else if Key = 'maximum' then begin
      if (Json.Nodes[Data].Kind = JNumber) and (Json.Nodes[Data].Number > Json.Nodes[Value].Number) then
        Exit(False);
    end else if Key = 'pattern' then begin
      if Json.Nodes[Data].Kind = JString then begin
        // The pattern is looked up before its place is read, because looking it up can move the patterns.
        Ix := Pattern(Json.Nodes[Value].Text);
        if not MatchString(Regexes[Ix], Json.Nodes[Data].Text) then
          Exit(False);
      end;
    end else if Key = 'format' then begin
      if Json.Nodes[Value].Text <> 'regex' then begin
        Fail('unexpected format ' + Json.Nodes[Value].Text);
        Broken := True;
      end;
      if (Json.Nodes[Data].Kind = JString) and not EcmaRegexValid(Json.Nodes[Data].Text) then
        Exit(False);
    end else if Key = 'patternProperties' then begin
      Sub := Json.Nodes[Value].First;
      while Sub >= 0 do begin
        if Json.Nodes[Data].Kind = JObject then begin
          Prop := Json.Nodes[Data].First;
          while Prop >= 0 do begin
            Ix := Pattern(Json.Nodes[Sub].Key);
            if MatchString(Regexes[Ix], Json.Nodes[Prop].Key) and not Valid(Sub, Prop) then
              Exit(False);
            Prop := Json.Nodes[Prop].Next;
          end;
        end;
        Sub := Json.Nodes[Sub].Next;
      end;
    end else if Key = 'additionalProperties' then begin
      Others := Json.Member(Schema, 'patternProperties');
      if Json.Nodes[Data].Kind = JObject then begin
        Prop := Json.Nodes[Data].First;
        while Prop >= 0 do begin
          Additional := True;
          if Others >= 0 then begin
            Other := Json.Nodes[Others].First;
            while Other >= 0 do begin
              Ix := Pattern(Json.Nodes[Other].Key);
              Additional := Additional and not MatchString(Regexes[Ix], Json.Nodes[Prop].Key);
              Other := Json.Nodes[Other].Next;
            end;
          end;
          if Additional and not Valid(Value, Prop) then
            Exit(False);
          Prop := Json.Nodes[Prop].Next;
        end;
      end;
    end else begin
      Fail('unexpected keyword ' + Quote(Key));
      Broken := True;
    end;
    Keyword := Json.Nodes[Keyword].Next;
  end;
  Result := True;
end;

{ TestSuite runs every case of the suite's regular expression files through EcmaRegexCompile, EcmaRegexIsMatch and
  EcmaRegexValid. }
procedure TestSuite;
var
  Tests: UTF8String;
  Files: TTexts;
  E: TSuiteEvaluator;
  F, Root, Group, Test, Cases, K, Regular, Automata: Int32;
  Got: Boolean;
begin
  if not SuiteDir(Tests) then begin
    WriteLn('TestSuite: SKIPPED, the JSON Schema Test Suite is not at ', Tests);
    Exit;
  end;
  Files := SuiteFiles(Tests);
  E := Default(TSuiteEvaluator);
  Cases := 0;
  for F := 0 to High(Files) do begin
    // The patterns are kept from file to file, and the values of the last file are dropped.
    E.Json := Default(TJson);
    if not LoadJson(Files[F], E.Json, Root) then begin
      Fail(Files[F] + ': cannot be read');
      Continue;
    end;
    Group := E.Json.Nodes[Root].First;
    while Group >= 0 do begin
      Test := E.Json.Nodes[E.Json.Member(Group, 'tests')].First;
      while Test >= 0 do begin
        Inc(Cases);
        Got := E.Valid(E.Json.Member(Group, 'schema'), E.Json.Member(Test, 'data'));
        if Got <> E.Json.Flag(Test, 'valid') then
          Fail(Files[F] + ': ' + E.Json.Text(Group, 'description') + ': ' + E.Json.Text(Test, 'description') +
            ': got ' + Bool(Got) + ', want ' + Bool(not Got));
        Test := E.Json.Nodes[Test].Next;
      end;
      Group := E.Json.Nodes[Group].Next;
    end;
  end;
  Regular := 0;
  Automata := 0;
  for K := 0 to E.Count - 1 do begin
    if E.Regexes[K].Regular then
      Inc(Regular);
    if E.Regexes[K].HasDfa then
      Inc(Automata);
  end;
  WriteLn('TestSuite: ', Cases, ' cases in ', Length(Files), ' files, ', E.Count, ' distinct patterns: ', Regular,
    ' regular (', Automata, ' with an automaton), ', E.Count - Regular, ' on the backtracking matcher only');
end;

{ CorpusWalk collects what SuiteCorpus wants from one value. }
procedure CorpusWalk(const Json: TJson; V: Int32; var Seen: TTexts; var Count: Int32);
var
  Item, P: Int32;
  Name: UTF8String;
begin
  if Json.Nodes[V].Kind = JArray then begin
    Item := Json.Nodes[V].First;
    while Item >= 0 do begin
      CorpusWalk(Json, Item, Seen, Count);
      Item := Json.Nodes[Item].Next;
    end;
  end else if Json.Nodes[V].Kind = JObject then begin
    Item := Json.Nodes[V].First;
    while Item >= 0 do begin
      Name := Json.Nodes[Item].Key;
      if (Json.Nodes[Item].Kind = JString) and ((Name = 'pattern') or (Name = 'data')) then
        AppendText(Seen, Count, Json.Nodes[Item].Text);
      if (Json.Nodes[Item].Kind = JObject) and (Name = 'patternProperties') then begin
        P := Json.Nodes[Item].First;
        while P >= 0 do begin
          AppendText(Seen, Count, Json.Nodes[P].Key);
          P := Json.Nodes[P].Next;
        end;
      end;
      CorpusWalk(Json, Item, Seen, Count);
      Item := Json.Nodes[Item].Next;
    end;
  end;
end;

procedure CorpusDir(const Dir: UTF8String; var Seen: TTexts; var Count: Int32);
var
  Names: TTexts;
  K, Root: Int32;
  Json: TJson;
begin
  Names := ListDir(Dir, False);
  for K := 0 to High(Names) do
    if (Length(Names[K]) > 5) and (Copy(Names[K], Length(Names[K]) - 4, 5) = '.json') then begin
      if LoadJson(Dir + '/' + Names[K], Json, Root) then
        CorpusWalk(Json, Root, Seen, Count)
      else
        Fail(Dir + '/' + Names[K] + ': cannot be read');
    end;
  Names := ListDir(Dir, True);
  for K := 0 to High(Names) do
    CorpusDir(Dir + '/' + Names[K], Seen, Count);
end;

{ SuiteCorpus collects, from every file of the suite, each pattern, each patternProperties name and each string
  instance (the instances of the regex format tests are patterns too, and the rest are arbitrary text to parse). It
  returns false when the suite is not there. }
function SuiteCorpus(out Corpus: TTexts): Boolean;
var
  Tests: UTF8String;
  Count: Int32;
begin
  Corpus := nil;
  if not SuiteDir(Tests) then
    Exit(False);
  Count := 0;
  CorpusDir(Tests, Corpus, Count);
  SortedSet(Corpus, Count);
  Result := True;
end;

{ The hand-written patterns of the Java port's EcmaRegexValidatorTest, each with whether it is a valid ECMA-262
  pattern with the u flag and whether it is valid with either grammar. The Java test compares its validator with its
  translator. Here the expectations are written out, and TestOracleHandWritten checks the same list against V8. }
procedure TestJavaHandWrittenPatterns;
var
  Patterns: Int32;

  procedure Hand(const Pattern: UTF8String; Unicode, Valid: Boolean);
  var
    Re: TEcmaRegex;
    Error: UTF8String;
    Ok: Boolean;
  begin
    Inc(Patterns);
    if EcmaRegexValid(Pattern) <> Unicode then
      Fail('Valid(' + Quote(Pattern) + ') = ' + Bool(not Unicode) + ', want ' + Bool(Unicode));
    Ok := EcmaRegexCompile(Pattern, Re, Error);
    if Ok <> Valid then
      Fail('Compile(' + Quote(Pattern) + ') error ' + Quote(Error) + ', want valid ' + Bool(Valid));
    if Ok and (Re.Unicode <> Unicode) then
      Fail('Compile(' + Quote(Pattern) + ').Unicode = ' + Bool(Re.Unicode) + ', want ' + Bool(Unicode));
  end;

begin
  Patterns := 0;
  Hand('', True, True);
  Hand('a', True, True);
  Hand('^a$', True, True);
  Hand('a|b', True, True);
  Hand('(a)', True, True);
  Hand('(?:a)', True, True);
  Hand('(?=a)', True, True);
  Hand('(?!a)', True, True);
  Hand('(?<=a)', True, True);
  Hand('(?<!a)', True, True);
  Hand('(?<n>a)\k<n>', True, True);
  Hand('\k<n>', False, True);
  Hand('(?<n>a)(?<n>b)', False, False);
  Hand('(?<$x_1>a)', True, True);
  Hand('(?<1a>a)', False, False);
  Hand('a{2}', True, True);
  Hand('a{2,}', True, True);
  Hand('a{2,3}', True, True);
  Hand('a{3,2}', False, False);
  Hand('a{', False, True);
  Hand('a{,2}', False, True);
  Hand('{', False, True);
  Hand('}', False, True);
  Hand(']', False, True);
  Hand('[', False, False);
  Hand('[]', True, True);
  Hand('[^]', True, True);
  Hand('[a-z]', True, True);
  Hand('[z-a]', False, False);
  Hand('[\d-z]', False, True);
  Hand('[a-\d]', False, True);
  Hand('[\b]', True, True);
  Hand('[\-]', True, True);
  Hand('\-', False, True);
  Hand('\a', False, True);
  Hand('\c', False, True);
  Hand('\cA', True, True);
  Hand('\c1', False, True);
  Hand('\0', True, True);
  Hand('\01', False, True);
  Hand('\1', False, True);
  Hand('(a)\1', True, True);
  Hand('(a)\2', False, True);
  Hand('\x4', False, True);
  Hand('\x41', True, True);
  Hand('\u004', False, True);
  Hand('A', True, True);
  Hand('\u{1F600}', True, True);
  Hand('\u{110000}', False, True);
  Hand('😀', True, True);
  Hand('[😀-🙏]', True, True);
  Hand('\p{L}', True, True);
  Hand('\p{Letter}', True, True);
  Hand('\p{digit}', True, True);
  Hand('\p{Nope}', False, True);
  Hand('\p{gc=Lu}', True, True);
  Hand('\p{Script=Greek}', True, True);
  Hand('\p{sc=Grek}', True, True);
  Hand('\p{Script=Nope}', False, True);
  Hand('\P{ASCII}', True, True);
  Hand('\p', False, True);
  Hand('\p{', False, True);
  Hand('a**', False, False);
  Hand('a+?', True, True);
  Hand('*a', False, False);
  Hand('(?=a)*', False, True);
  Hand('^*', False, False);
  Hand('$+', False, False);
  Hand('\b+', False, False);
  Hand('a)', False, False);
  Hand('(a', False, False);
  Hand('(?a)', False, False);
  Hand('\/', True, True);
  Hand('\.', True, True);
  Hand('a\', False, False);
  Hand('[a', False, False);
  Hand('x{1}{2}', False, False);
  Hand('\w+@\w+\.\w+', True, True);
  Hand('^[a-z][a-z0-9_]*$', True, True);
  Hand('(?<a>.)\k<a>', True, True);
  Hand('[\p{L}\d]', True, True);
  Hand('\s\S\w\W\d\D', True, True);
  WriteLn('TestJavaHandWrittenPatterns: ', Patterns, ' patterns');
end;

{ TestSuiteCorpus ports the Java port's test over the suite's patterns. Every pattern, patternProperties name and
  string instance in the suite is read as a pattern. None may make the parser or a compiler misbehave, a pattern
  Valid accepts must compile with the u flag grammar, and a pattern or patternProperties name must compile. }
procedure TestSuiteCorpus;
var
  Corpus: TTexts;
  Tests, Error: UTF8String;
  K, Valid, CompiledCount: Int32;
  Ok, Built: Boolean;
  Re: TEcmaRegex;
begin
  if not SuiteCorpus(Corpus) then begin
    SuiteDir(Tests);
    WriteLn('TestSuiteCorpus: SKIPPED, the JSON Schema Test Suite is not at ', Tests);
    Exit;
  end;
  Valid := 0;
  CompiledCount := 0;
  for K := 0 to High(Corpus) do begin
    Ok := EcmaRegexValid(Corpus[K]);
    Built := EcmaRegexCompile(Corpus[K], Re, Error);
    if Ok then begin
      Inc(Valid);
      if (not Built) or not Re.Unicode then
        Fail('Valid(' + Quote(Corpus[K]) + ') but Compile gives ' + Quote(Error));
    end;
    if not Built then
      Continue;
    Inc(CompiledCount);
    if (not Ok) and Re.Unicode then
      Fail('Compile(' + Quote(Corpus[K]) + ') used the u flag grammar but Valid rejects it');
  end;
  WriteLn('TestSuiteCorpus: ', Length(Corpus), ' strings, ', Valid, ' valid with the u flag, ', CompiledCount,
    ' compiled');
end;

{ Next is the generator gen_oracle.js uses, so the test builds the patterns the oracle's verdicts are about. }
function Next(var Seed: UInt32): UInt32;
var
  S: UInt64;
begin
  S := Seed;
  S := (S xor (S shl 13)) and $FFFFFFFF;
  S := S xor (S shr 17);
  S := (S xor (S shl 5)) and $FFFFFFFF;
  Seed := UInt32(S);
  Result := Seed;
end;

{ DifferentialInputs builds texts over an alphabet that exercises classes, anchors, line terminators, characters
  beyond the Basic Multilingual Plane and malformed UTF-8. }
function DifferentialInputs(N: Int32): TTexts;
var
  Alphabet: TTexts;
  Letters, Count, K: Int32;
  Seed: UInt32;
  Text: UTF8String;

  procedure Add(const S: UTF8String);
  begin
    AppendText(Alphabet, Letters, S);
  end;

begin
  Alphabet := nil;
  Letters := 0;
  Add('a');
  Add('b');
  Add('c');
  Add('z');
  Add('A');
  Add('X');
  Add('Z');
  Add('0');
  Add('1');
  Add('5');
  Add('9');
  Add('_');
  Add('-');
  Add('.');
  Add(':');
  Add('/');
  Add('@');
  Add('#');
  Add('$');
  Add('*');
  Add('!');
  Add('{');
  Add('}');
  Add('|');
  Add(' ');
  Add(#$000A);
  Add(#$000D);
  Add(#$0009);
  Add(#$00A0);
  Add('é');
  Add('µ');
  Add('😀');
  Add('🐲');
  Add(#$2028);
  Add(#$FFFD);
  Add('x-');
  Add('es');
  Add('ES');
  Add('ms');
  Add('txt');
  Add('Au');
  Add('to');
  Add('No');
  Add('ne');
  Add('2015');
  Add('Co');
  Add('re');
  Add('20');
  Add('15');
  Add('22');
  Add('2');
  Add(',');
  Add('a,');
  Add('ab,');
  Add('a-');
  Add('foo');
  Add('bar');
  Add('α');
  Add('Ω');
  Add('٣');
  Add('\');
  Add('&');
  Add('(');
  Add(')');
  Add('[');
  Add(']');
  Add('^');
  Add('+');
  Add('?');
  Add('<');
  Add('>');
  Add('=');
  Add('%');
  Add(HexBytes('ff'));
  Add(HexBytes('c3'));
  Add(HexBytes('e282'));
  Add(HexBytes('f09f'));
  Add(HexBytes('eda080'));
  Seed := $2545F491;
  Result := nil;
  Count := 0;
  AppendText(Result, Count, '');
  for K := 0 to Letters - 1 do
    AppendText(Result, Count, Alphabet[K]);
  while Count < N do begin
    Text := '';
    K := 1 + Int32(Next(Seed) mod 8);
    while K > 0 do begin
      Text := Text + Alphabet[Next(Seed) mod UInt32(Letters)];
      Dec(K);
    end;
    AppendText(Result, Count, Text);
  end;
  SetLength(Result, Count);
end;

{ The oracle holds what V8 answers for a set of patterns and texts. testdata/gen_oracle.js of the Go package writes
  it. }
type
  TOracleHand = record
    P: UTF8String;
    // U and L say whether the pattern is valid with the u flag and with no flag.
    U, L: Boolean;
    // M has one bit per input, set for a match, written as hexadecimal digits of four bits each.
    M: UTF8String;
  end;

  TOracleFlagged = record
    P: UTF8String;
    // F holds the flags the pattern was matched with, out of i, m and s.
    F: UTF8String;
    U: Boolean;
    M: UTF8String;
  end;

  TOracle = record
    Node: UTF8String;
    Inputs: TTexts;
    ShortInputs: TTexts;
    Verdicts: UTF8String;
    RandomMatches: TTexts;
    Hand: array of TOracleHand;
    Flagged: array of TOracleFlagged;
  end;

var
  Oracle: TOracle;
  OracleLoaded: Boolean = False;

function LoadOracle: Boolean;
var
  Json: TJson;
  Root, Item, N: Int32;

  function Texts(const Name: UTF8String): TTexts;
  var
    V, Count: Int32;
  begin
    Result := nil;
    Count := 0;
    V := Json.Nodes[Json.Member(Root, Name)].First;
    while V >= 0 do begin
      AppendText(Result, Count, Json.Nodes[V].Text);
      V := Json.Nodes[V].Next;
    end;
    SetLength(Result, Count);
  end;

begin
  if OracleLoaded then
    Exit(True);
  if not LoadJson(OraclePath, Json, Root) then begin
    Fail('the oracle cannot be read at ' + OraclePath);
    Exit(False);
  end;
  Oracle.Node := Json.Text(Root, 'node');
  Oracle.Inputs := Texts('inputs');
  Oracle.ShortInputs := Texts('shortInputs');
  Oracle.Verdicts := Json.Text(Root, 'verdicts');
  Oracle.RandomMatches := Texts('randomMatches');
  N := 0;
  Item := Json.Nodes[Json.Member(Root, 'hand')].First;
  while Item >= 0 do begin
    SetLength(Oracle.Hand, N + 1);
    Oracle.Hand[N].P := Json.Text(Item, 'p');
    Oracle.Hand[N].U := Json.Flag(Item, 'u');
    Oracle.Hand[N].L := Json.Flag(Item, 'l');
    Oracle.Hand[N].M := Json.Text(Item, 'm');
    Inc(N);
    Item := Json.Nodes[Item].Next;
  end;
  N := 0;
  Item := Json.Nodes[Json.Member(Root, 'flagged')].First;
  while Item >= 0 do begin
    SetLength(Oracle.Flagged, N + 1);
    Oracle.Flagged[N].P := Json.Text(Item, 'p');
    Oracle.Flagged[N].F := Json.Text(Item, 'f');
    Oracle.Flagged[N].U := Json.Flag(Item, 'u');
    Oracle.Flagged[N].M := Json.Text(Item, 'm');
    Inc(N);
    Item := Json.Nodes[Item].Next;
  end;
  OracleLoaded := True;
  Result := True;
end;

{ Bit returns bit K of a string written by the oracle's hexBits. }
function Bit(const Hex: UTF8String; K: Int32): Boolean;
var
  Digit: Int32;
begin
  Digit := Ord(Hex[K div 4 + 1]);
  if Digit >= Ord('a') then
    Digit := Digit - Ord('a') + 10
  else
    Digit := Digit - Ord('0');
  Result := ((Digit shr (3 - K mod 4)) and 1) = 1;
end;

{ BeyondBMP reports whether the text has a character beyond the Basic Multilingual Plane. V8 matches a pattern with
  no flag by UTF-16 code unit and this package matches by code point, so the oracle is not asked about such a text
  for a pattern that is valid only with no flag. }
function BeyondBMP(const S: UTF8String): Boolean;
var
  Text: TBytes;
  At, Width: Int32;
begin
  Text := ToBytes(S);
  At := 0;
  while At < Length(Text) do begin
    if DecodeRune(Text, At, Length(Text), Width) > $FFFF then
      Exit(True);
    Inc(At, Width);
  end;
  Result := False;
end;

{ V8ModifierBug reports whether a pattern is one for which V8 13.6 answers a case-insensitive modifier group
  differently from the same pattern with the i flag. ECMA-262 gives the two the same meaning. Only the validity of
  these patterns is taken from the oracle. TestOracleFlags checks the behaviour of each through the i flag, where V8
  agrees with this package. }
function V8ModifierBug(const Pattern: UTF8String): Boolean;
var
  Found: Boolean;

  procedure Bug(const P: UTF8String);
  begin
    Found := Found or (P = Pattern);
  end;

begin
  Found := False;
  Bug('^(?i:\u212a)$');
  Bug('^(?i:\u017f)$');
  Bug('^(?i:\u1e9e)$');
  Bug('^(?i:\u03bc)$');
  Bug('^(?i:\u03bc)\&?$');
  Bug('(?i:\Bs)');
  Result := Found;
end;

{ CheckVerdict compares the validity of a pattern with V8's. It gives the pattern compiled for each back end, and
  returns false when the pattern is not valid. }
function CheckVerdict(const P: UTF8String; ValidUnicode, ValidLegacy: Boolean;
  out Re, Backtrack: TEcmaRegex): Boolean;
var
  Error: UTF8String;
  Ok, Agreed: Boolean;
begin
  Inc(OracleVerdicts);
  Agreed := True;
  Re := Default(TEcmaRegex);
  Backtrack := Default(TEcmaRegex);
  if EcmaRegexValid(P) <> ValidUnicode then begin
    Fail('Valid(' + Quote(P) + ') = ' + Bool(not ValidUnicode) + ', V8 says ' + Bool(ValidUnicode));
    Agreed := False;
  end;
  Ok := EcmaRegexCompile(P, Re, Error);
  if Ok <> (ValidUnicode or ValidLegacy) then begin
    Fail('Compile(' + Quote(P) + ') error ' + Quote(Error) + ', V8 valid with u ' + Bool(ValidUnicode) +
      ', with no flag ' + Bool(ValidLegacy));
    Exit(False);
  end;
  if not Ok then begin
    if Agreed then
      Inc(OracleVerdictsAgreed);
    Exit(False);
  end;
  if Re.Unicode <> ValidUnicode then begin
    Fail('Compile(' + Quote(P) + ').Unicode = ' + Bool(Re.Unicode) + ', V8 says ' + Bool(ValidUnicode));
    Agreed := False;
  end;
  if Agreed then
    Inc(OracleVerdictsAgreed);
  Backtrack := Backtracking(Re);
  Result := True;
end;

{ CheckAnswers compares the answers of both back ends with the oracle's on every text. It adds to Answers the number
  of answers compared and to Matched the number of those that are a match. }
procedure CheckAnswers(const P: UTF8String; const Re, Backtrack: TEcmaRegex; const Inputs: TTexts;
  const Bits: UTF8String; var Answers, Matched: Int64);
var
  I: Int32;
  Want, Got: Boolean;
  Text: TBytes;
begin
  for I := 0 to High(Inputs) do begin
    if (not Re.Unicode) and BeyondBMP(Inputs[I]) then
      Continue;
    Want := Bit(Bits, I);
    Inc(Answers);
    if Want then
      Inc(Matched);
    Text := ToBytes(Inputs[I]);
    Inc(OracleAnswers, 2);
    Got := EcmaRegexIsMatch(Re, Text, 0, Length(Text));
    if Got <> Want then
      Fail(Quote(P) + ' on ' + Quote(Inputs[I]) + ' (as compiled, automaton ' + Bool(Re.HasDfa) + '): got ' +
        Bool(Got) + ', V8 says ' + Bool(Want))
    else
      Inc(OracleAnswersAgreed);
    Got := EcmaRegexIsMatch(Backtrack, Text, 0, Length(Text));
    if Got <> Want then
      Fail(Quote(P) + ' on ' + Quote(Inputs[I]) + ' (forced backtrack): got ' + Bool(Got) + ', V8 says ' +
        Bool(Want))
    else
      Inc(OracleAnswersAgreed);
  end;
end;

{ TestOracleHandWritten checks the hand-written patterns (the Java port's validator cases, the Rust port's pattern
  cases and the Go package's own) against V8, for validity and for the answer on every text, on both back ends. }
procedure TestOracleHandWritten;
var
  K: Int32;
  Answers, Matched: Int64;
  Re, Backtrack: TEcmaRegex;
begin
  if not LoadOracle then
    Exit;
  Answers := 0;
  Matched := 0;
  for K := 0 to High(Oracle.Hand) do begin
    if not CheckVerdict(Oracle.Hand[K].P, Oracle.Hand[K].U, Oracle.Hand[K].L, Re, Backtrack) then
      Continue;
    if V8ModifierBug(Oracle.Hand[K].P) then
      Continue;
    CheckAnswers(Oracle.Hand[K].P, Re, Backtrack, Oracle.Inputs, Oracle.Hand[K].M, Answers, Matched);
  end;
  WriteLn('TestOracleHandWritten: ', Length(Oracle.Hand), ' patterns, ', Answers, ' answers per back end (', Matched,
    ' of them a match), from Node ', Oracle.Node);
end;

{ TestOracleFlags checks case-insensitive, multiline and dot-all matching. V8 matched each pattern with flags set
  for the whole pattern, and here the pattern is wrapped in the modifier group that means the same. }
procedure TestOracleFlags;
var
  K: Int32;
  Answers, Matched: Int64;
  P: UTF8String;
  Re, Backtrack: TEcmaRegex;
begin
  if not LoadOracle then
    Exit;
  Answers := 0;
  Matched := 0;
  for K := 0 to High(Oracle.Flagged) do begin
    P := '(?' + Oracle.Flagged[K].F + ':' + Oracle.Flagged[K].P + ')';
    if not CheckVerdict(P, Oracle.Flagged[K].U, not Oracle.Flagged[K].U, Re, Backtrack) then
      Continue;
    CheckAnswers(P, Re, Backtrack, Oracle.Inputs, Oracle.Flagged[K].M, Answers, Matched);
  end;
  WriteLn('TestOracleFlags: ', Length(Oracle.Flagged), ' patterns with flags, ', Answers,
    ' answers per back end (', Matched, ' of them a match)');
end;

{ TestOracleGenerated ports the Java port's generated test, which reads random patterns over an alphabet of syntax
  characters. Each one's validity is checked against V8, and the first few thousand are also matched on both back
  ends. }
procedure TestOracleGenerated;
const
  Alphabet: UTF8String = 'ab0\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L';
var
  Seed: UInt32;
  Counts: array[0..2] of Int32;
  Answers, Matched: Int64;
  I, N, K, Before: Int32;
  P: UTF8String;
  Verdict: AnsiChar;
  Re, Backtrack: TEcmaRegex;
begin
  if not LoadOracle then
    Exit;
  Seed := $9E3779B9;
  Counts[0] := 0;
  Counts[1] := 0;
  Counts[2] := 0;
  Answers := 0;
  Matched := 0;
  Before := Failures;
  for I := 0 to Length(Oracle.Verdicts) - 1 do begin
    N := 1 + Int32(Next(Seed) mod 8);
    SetLength(P, N);
    for K := 1 to N do
      P[K] := Alphabet[1 + Int32(Next(Seed) mod UInt32(Length(Alphabet)))];
    Verdict := Oracle.Verdicts[I + 1];
    Inc(Counts[Ord(Verdict) - Ord('0')]);
    if (not CheckVerdict(P, Verdict = '1', Verdict = '2', Re, Backtrack)) or (I >= Length(Oracle.RandomMatches)) then
      Continue;
    CheckAnswers(P, Re, Backtrack, Oracle.ShortInputs, Oracle.RandomMatches[I], Answers, Matched);
    if (Failures > Before) and (I > 200) then begin
      Fail('TestOracleGenerated stopped at pattern ' + Num(I) + ' after its first failures');
      Break;
    end;
  end;
  WriteLn('TestOracleGenerated: ', Length(Oracle.Verdicts), ' patterns (', Counts[0], ' not valid, ', Counts[1],
    ' valid with u, ', Counts[2], ' valid only with no flag), ', Answers, ' answers per back end (', Matched,
    ' of them a match)');
end;

{ TestDifferential runs every pattern that has an automaton, or that the Go package would hand to the regexp
  package, on the backtracking matcher alone too, and the two must agree on every text. In Go this test proves the
  translation to the regexp package exact. Here it proves the automaton over ASCII text exact, and that a text the
  automaton hands back is answered the same. }
procedure TestDifferential;
var
  Patterns, Corpus, Inputs: TTexts;
  Count, K, S, RegularCount, Others, Automata: Int32;
  Answers, Decided: Int64;
  Re, Backtrack: TEcmaRegex;
  Error: UTF8String;
  Text: TBytes;
  A, B, Matched: Boolean;

  procedure Add(const P: UTF8String);
  begin
    AppendText(Patterns, Count, P);
  end;

begin
  if not LoadOracle then
    Exit;
  Patterns := nil;
  Count := 0;
  for K := 0 to High(Oracle.Hand) do
    Add(Oracle.Hand[K].P);
  for K := 0 to High(Oracle.Flagged) do
    Add('(?' + Oracle.Flagged[K].F + ':' + Oracle.Flagged[K].P + ')');
  Add(#$FFFD);
  Add('^[^'#$FFFD']+$');
  Add('^'#$FFFD'+$');
  Add('^.$');
  Add('^..$');
  Add('^[^a]$');
  Add('\P{Any}');
  Add('^\p{Any}*$');
  Add('^[\s\S]{2,4}$');
  Add('^(?:\p{L}|\p{N})+$');
  Add('\b\w+\b');
  Add('\B');
  Add('^$');
  Add('$');
  Add('^');
  Add('(?:)');
  Add('^(?:a{2}){2,3}$');
  Add('^(?:a|b|ab){1,4}?c');
  Add('^.{0,300}$');
  Add('^(?:[a-z]{1,3}\d?){2,5}$');
  Add('^[\u0000-'#$FFFF']+$');
  Add('^[\u{10000}-\u{10ffff}]+$');
  Add('(?:a{50}){30}');
  Add('^a{1001}$');
  Add('^(?:a?){1001}$');
  Add('\x{41}');
  Add('^\u{2}$');
  if SuiteCorpus(Corpus) then
    for K := 0 to High(Corpus) do
      Add(Corpus[K]);
  SortedSet(Patterns, Count);
  Inputs := DifferentialInputs(1500);
  RegularCount := 0;
  Others := 0;
  Automata := 0;
  Answers := 0;
  Decided := 0;
  for K := 0 to High(Patterns) do begin
    if not EcmaRegexCompile(Patterns[K], Re, Error) then
      Continue;
    if not Re.Regular then begin
      Inc(Others);
      if Re.HasDfa then
        Fail(Quote(Patterns[K]) + ' has an automaton and is not regular');
      Continue;
    end;
    Inc(RegularCount);
    Backtrack := Backtracking(Re);
    for S := 0 to High(Inputs) do begin
      Inc(Answers);
      Text := ToBytes(Inputs[S]);
      A := EcmaRegexIsMatch(Re, Text, 0, Length(Text));
      B := EcmaRegexIsMatch(Backtrack, Text, 0, Length(Text));
      if A <> B then
        Fail(Quote(Patterns[K]) + ' on ' + Quote(Inputs[S]) + ': as compiled (automaton ' + Bool(Re.HasDfa) +
          ') says ' + Bool(A) + ', the backtracking matcher says ' + Bool(B));
      if Re.HasDfa and DfaMatch(Re.Dfa, Text, 0, Length(Text), Matched) then begin
        Inc(Decided);
        if Matched <> B then
          Fail(Quote(Patterns[K]) + ' on ' + Quote(Inputs[S]) + ': the automaton says ' + Bool(Matched) +
            ', the backtracking matcher says ' + Bool(B));
      end;
    end;
    if Re.HasDfa then
      Inc(Automata);
  end;
  WriteLn('TestDifferential: ', RegularCount, ' regular patterns compared on ', Length(Inputs), ' texts each (',
    Answers, ' answers), ', Others, ' patterns on the backtracking matcher only');
  WriteLn('TestDifferential: ', Automata, ' of the regular patterns have an automaton, which decided ', Decided,
    ' of the answers');
  if (Automata = 0) or (Decided = 0) then
    Fail('no automaton was compared');
end;

{ TestEngineChoice checks which back end a pattern takes in Go, which here is whether it is regular. }
procedure TestEngineChoice;

  procedure Choice(const Pattern: UTF8String; Regular: Boolean);
  var
    Re: TEcmaRegex;
  begin
    if not Compiled(Pattern, Re) then
      Exit;
    if Re.Regular <> Regular then
      Fail('Compile(' + Quote(Pattern) + ').Regular = ' + Bool(Re.Regular) + ', want ' + Bool(Regular));
  end;

begin
  Choice('^[a-z][a-z0-9_]*$', True);
  Choice('\bfoo', True);
  Choice('^\p{L}+$', True);
  Choice('(?<name>ab)+', True);
  Choice('^\/[^\*\?\&\%]*(\/\*)?$', True);
  Choice('^.{1,256}$', True);
  Choice('^a*?b', True);
  Choice('(a)\1', False);
  Choice('^(?!foo)', False);
  Choice('(?=a)', False);
  Choice('(?<=a)b', False);
  Choice('(?<!a)b', False);
  Choice('(?<n>a)\k<n>', False);
  Choice('^a{1001}$', False);
  Choice('(?:a{50}){30}', False);
  WriteLn('TestEngineChoice: done');
end;

{ TestBacktrackingSemantics pins the ECMA-262 behaviours only the backtracking matcher has. The oracle checks most
  of them against V8 too. These are the ones worth reading. }
procedure TestBacktrackingSemantics;
var
  Cases: Int32;

  procedure Semantic(const Pattern, Text: UTF8String; Want: Boolean);
  var
    Re: TEcmaRegex;
  begin
    Inc(Cases);
    if not Compiled(Pattern, Re) then
      Exit;
    if Re.Regular then
      Fail('Compile(' + Quote(Pattern) + ') is regular');
    if MatchString(Re, Text) <> Want then
      Fail(Quote(Pattern) + ' on ' + Quote(Text) + ': got ' + Bool(not Want) + ', want ' + Bool(Want));
  end;

begin
  Cases := 0;
  // The Go source says of these, in order: a backreference to a group that has not taken part matches the empty
  // string. A group is reset at the start of each iteration of the loop around it. An iteration that consumes
  // nothing ends the loop once the minimum is met. A lookaround is atomic and a negative one leaves no group set.
  // A lookbehind matches right to left, so the rightmost group takes the most. Matching is by code point. Lazy and
  // counted loops.
  Semantic('^\1(a)$', 'a', True);
  Semantic('^(a)?\1b$', 'b', True);
  Semantic('^(?:(a)|b)\1$', 'b', True);
  Semantic('^(?:(a)|b)*\1$', 'ab', True);
  Semantic('^(?:(a)|b)*\1$', 'aba', False);
  Semantic('^(?:(a)|b)*\1$', 'baa', True);
  Semantic('^(?:a*)*$(?<=a)', 'aaa', True);
  Semantic('^(?:a*)*b(?<=b)', 'aaa', False);
  Semantic('^(?:(?=(a)))?\1b', 'b', True);
  Semantic('^(?:(?=(a)))?\1a', 'a', True);
  Semantic('^(?:a?){3,5}b(?<=b)', 'aab', True);
  Semantic('^(?:|a)+b(?<=b)', 'aab', True);
  Semantic('^(?=(a+))a*b\1$', 'aaaba', False);
  Semantic('^(?=(a+))a*b\1$', 'aaabaaa', True);
  Semantic('(?!(a))\1b', 'b', True);
  Semantic('(?<=(\d+)(\d+))$', '1053', True);
  Semantic('(?<=\1(a))b', 'aab', True);
  Semantic('(?<=\1(a))b', 'ab', False);
  Semantic('(?<=^a*)b', 'aab', True);
  Semantic('(?<!^a*)b', 'aab', False);
  Semantic('(?<=a|bc)x', 'bcx', True);
  Semantic('^(?<n>.)\k<n>$', '🐲🐲', True);
  Semantic('^(?<n>.)\k<n>$', '🐲🐉', False);
  Semantic('(?<=^.)$', '🐲', True);
  Semantic('(?<=[😀-😎])x', '😃x', True);
  Semantic('(?<!\u{1F600})x', '😀x', False);
  Semantic('^(?:a|ab)+?b$(?<=b)', 'aabb', True);
  Semantic('^(?:(a)|b){2,}?\1$', 'abbaa', True);
  Semantic('^(?:(a)|b){2,}?\1$', 'abba', False);
  Semantic('^(.+?)\1+$', 'abcabcabc', True);
  Semantic('^(.+?)\1+$', 'abcabcab', False);
  Semantic('^(?:(\w)(?!\1))+$', 'abab', True);
  Semantic('^(?:(\w)(?!\1))+$', 'abba', False);
  Semantic('^(?!.*(.).*\1)[a-c]+$', 'abc', True);
  Semantic('^(?!.*(.).*\1)[a-c]+$', 'abca', False);
  WriteLn('TestBacktrackingSemantics: ', Cases, ' cases');
end;

{ TestLongInput runs the backtracking matcher on texts long enough that a matcher recursing once per character or
  per choice would exhaust its stack. }
procedure TestLongInput;
var
  Long, LongB, LongC: TBytes;
  K: Int32;

  procedure Check(const Pattern: UTF8String; const Text: TBytes; Want: Boolean);
  var
    Re: TEcmaRegex;
  begin
    if not Compiled(Pattern, Re) then
      Exit;
    if Re.Regular then
      Fail('Compile(' + Quote(Pattern) + ') is regular');
    if EcmaRegexIsMatch(Re, Text, 0, Length(Text)) <> Want then
      Fail(Quote(Pattern) + ' on ' + Num(Length(Text)) + ' bytes: got ' + Bool(not Want) + ', want ' + Bool(Want));
  end;

begin
  SetLength(Long, 2 shl 20);
  for K := 0 to (1 shl 20) - 1 do begin
    Long[2 * K] := Ord('a');
    Long[2 * K + 1] := Ord('b');
  end;
  LongB := Copy(Long, 0, Length(Long));
  SetLength(LongB, Length(Long) + 1);
  LongB[Length(Long)] := Ord('b');
  LongC := Copy(LongB, 0, Length(LongB));
  LongC[Length(Long)] := Ord('c');
  Check('^(?:a|b)*$(?<=b)', Long, True);
  Check('^(?:a|b)*c(?<=c)', Long, False);
  Check('^(?:ab)+(?!.)', Long, True);
  Check('^(?=a)(?:\w\w)*?$', Long, True);
  Check('^(a|b)*\1$', LongB, True);
  Check('(?<=^(?:ab)*)c', LongC, True);
  Check('^(?!b).*b$', Long, True);
  Check('^[ab]{2097152}(?<=b)$', Long, True);
  Check('^(?:[ab]c?){2097152}(?<=b)$', Long, True);
  // The stack of the last matches is tens of megabytes, and is not kept.
  EcmaRegexReleaseThreadScratch;
  WriteLn('TestLongInput: 9 patterns on ', Length(Long), ' bytes');
end;

{$IFDEF FPC}
{ The memory manager is wrapped to count the allocations of TestAllocations. }
var
  SystemManager: TMemoryManager;
  Allocations: Int64 = 0;

function CountingGetMem(Size: PtrUInt): Pointer;
begin
  Inc(Allocations);
  Result := SystemManager.GetMem(Size);
end;

function CountingAllocMem(Size: PtrUInt): Pointer;
begin
  Inc(Allocations);
  Result := SystemManager.AllocMem(Size);
end;

function CountingReAllocMem(var P: Pointer; Size: PtrUInt): Pointer;
begin
  Inc(Allocations);
  Result := SystemManager.ReAllocMem(P, Size);
end;
{$ENDIF}

{ TestAllocations checks that neither back end allocates in the steady state. }
procedure TestAllocations;
{$IFDEF FPC}
var
  Texts: array[0..4] of TBytes;
  Counting: TMemoryManager;
  Patterns: Int32;

  procedure Alloc(const Pattern: UTF8String; Regular: Boolean);
  var
    Re: TEcmaRegex;
    Run, T: Int32;
    Before: Int64;
  begin
    Inc(Patterns);
    if not Compiled(Pattern, Re) then
      Exit;
    if Re.Regular <> Regular then begin
      Fail('Compile(' + Quote(Pattern) + ').Regular = ' + Bool(Re.Regular) + ', want ' + Bool(Regular));
      Exit;
    end;
    // The first matches grow the memory of the thread to the size the texts need.
    for T := 0 to High(Texts) do
      EcmaRegexIsMatch(Re, Texts[T], 0, Length(Texts[T]));
    SetMemoryManager(Counting);
    Before := Allocations;
    for Run := 1 to 200 do
      for T := 0 to High(Texts) do
        EcmaRegexIsMatch(Re, Texts[T], 0, Length(Texts[T]));
    Before := Allocations - Before;
    SetMemoryManager(SystemManager);
    if Before <> 0 then
      Fail(Quote(Pattern) + ' (automaton ' + Bool(Re.HasDfa) + '): ' + Num(Before) + ' allocations in 200 runs');
  end;

begin
  Texts[0] := ToBytes('the quick brown fox jumps over the lazy dog 12345');
  Texts[1] := ToBytes('abcabcabc');
  Texts[2] := ToBytes('a-b_c.d@example.com');
  Texts[3] := ToBytes('🐲 naïve café 🐲');
  Texts[4] := nil;
  GetMemoryManager(SystemManager);
  Counting := SystemManager;
  Counting.GetMem := @CountingGetMem;
  Counting.AllocMem := @CountingAllocMem;
  Counting.ReAllocMem := @CountingReAllocMem;
  Patterns := 0;
  Alloc('^[a-z][a-z0-9_]*$', True);
  Alloc('\b\d{5}\b', True);
  Alloc('^\p{L}+$', True);
  Alloc('(?:fox|dog) \w+', True);
  Alloc('^(.+?)\1+$', False);
  Alloc('^(?!.*\d{6})(?=.*\bfox\b).+$', False);
  Alloc('(?<=@)\w+(?:\.\w+)+$', False);
  Alloc('^(?:(\w)(?!\1)|\W){3,}?$', False);
  Alloc('(?<n>[a-z])\k<n>', False);
  Alloc('café(?= )', False);
  Alloc('(?i:QUICK|ÉCOLE)', True);
  Alloc('(\w)(?i:\1)', False);
  Alloc('(?m:^)\w+(?m:$)', False);
  Alloc('(?i:\bCAF\b)', False);
  WriteLn('TestAllocations: ', Patterns, ' patterns, each matched 200 times on ', Length(Texts), ' texts');
end;
{$ELSE}
begin
  WriteLn('TestAllocations: SKIPPED, the count of allocations is taken from the memory manager of Free Pascal');
end;
{$ENDIF}

type
  { A thread of TestConcurrentUse. }
  TMatchThread = class(TThread)
  public
    Regex: TEcmaRegex;
    G: Int32;
    Wrong: Boolean;
    procedure Execute; override;
  end;

procedure TMatchThread.Execute;
var
  Yes, No: TBytes;
  I, K: Int32;
begin
  SetLength(Yes, 5 * (50 + G));
  for K := 0 to 50 + G - 1 do begin
    Yes[5 * K] := Ord('a');
    Yes[5 * K + 1] := Ord('a');
    Yes[5 * K + 2] := Ord('1');
    Yes[5 * K + 3] := Ord('b');
    Yes[5 * K + 4] := Ord('2');
  end;
  No := Copy(Yes, 0, Length(Yes));
  SetLength(No, Length(Yes) + 1);
  No[Length(Yes)] := Ord('!');
  for I := 1 to 500 do
    if (not EcmaRegexIsMatch(Regex, Yes, 0, Length(Yes))) or EcmaRegexIsMatch(Regex, No, 0, Length(No)) then begin
      Wrong := True;
      Break;
    end;
  EcmaRegexReleaseThreadScratch;
end;

{ TestConcurrentUse matches one pattern of each back end from many threads at once. }
procedure TestConcurrentUse;
const
  Patterns: array[0..1] of UTF8String = ('^(?:[a-z]+\d)+$', '^(?:([a-z])\1?\d)+(?<=\d)$');
var
  P, G: Int32;
  Re: TEcmaRegex;
  Threads: array[0..15] of TMatchThread;
begin
  for P := 0 to High(Patterns) do begin
    if not Compiled(Patterns[P], Re) then
      Continue;
    for G := 0 to High(Threads) do begin
      Threads[G] := TMatchThread.Create(True);
      Threads[G].Regex := Re;
      Threads[G].G := G;
      Threads[G].Start;
    end;
    for G := 0 to High(Threads) do begin
      Threads[G].WaitFor;
      if Threads[G].Wrong then
        Fail(Quote(Patterns[P]) + ' gave a wrong answer under concurrent use');
      Threads[G].Free;
    end;
  end;
  WriteLn('TestConcurrentUse: 2 patterns, ', Length(Threads), ' threads each');
end;

{ TestFoldTables checks the tables behind case-insensitive matching. The table for the u flag grammar must hold
  every character that has a simple case folding, and each table must agree with Canonicalize. }
procedure TestFoldTables;
var
  Mode: Boolean;
  Pass, I, M, R, Key, Other, Reported: Int32;
  Table: PFoldTable;
  InClass: array of Boolean;
  Seen: array of Int32;
begin
  for Pass := 0 to 1 do begin
    Mode := Pass = 0;
    Reported := 0;
    Table := FoldClasses(Mode);
    SetLength(InClass, 0);
    SetLength(InClass, MaxRune + 1);
    for I := 0 to High(Table^.Points) do begin
      InClass[Table^.Points[I]] := Length(Table^.Classes[I]) > 0;
      for M := 0 to High(Table^.Classes[I]) do
        if Canonicalize(Table^.Classes[I][M], Mode) <> Canonicalize(Table^.Points[I], Mode) then
          Fail('u flag ' + Bool(Mode) + ': U+' + UTF8String(IntToHex(Table^.Points[I], 4)) + ' and U+' +
            UTF8String(IntToHex(Table^.Classes[I][M], 4)) + ' share a class but not a canonical form');
    end;
    SetLength(Seen, MaxRune + 1);
    for R := 0 to MaxRune do
      Seen[R] := -1;
    for R := 0 to MaxRune do begin
      Key := Canonicalize(R, Mode);
      if (Seen[Key] >= 0) or (Key <> R) then begin
        Other := Seen[Key];
        if Other < 0 then
          Other := Key;
        if ((not InClass[R]) or not InClass[Other]) and (Reported < 10) then begin
          Inc(Reported);
          Fail('u flag ' + Bool(Mode) + ': U+' + UTF8String(IntToHex(R, 4)) + ' and U+' +
            UTF8String(IntToHex(Other, 4)) + ' are equivalent but not both in the table');
        end;
      end;
      Seen[Key] := R;
    end;
    WriteLn('TestFoldTables: u flag ', Bool(Mode), ': ', Length(Table^.Points), ' characters in classes');
  end;
end;

{ TestModifiers checks the modifier groups of ECMAScript 2025 on cases worth reading. TestOracleFlags checks them
  against V8 at large. }
procedure TestModifiers;
var
  Cases: Int32;

  procedure Modifier(const Pattern, Text: UTF8String; Want, Regular: Boolean);
  var
    Re: TEcmaRegex;
  begin
    Inc(Cases);
    if not Compiled(Pattern, Re) then
      Exit;
    if Re.Regular <> Regular then
      Fail('Compile(' + Quote(Pattern) + ').Regular = ' + Bool(Re.Regular) + ', want ' + Bool(Regular));
    if MatchString(Re, Text) <> Want then
      Fail(Quote(Pattern) + ' on ' + Quote(Text) + ': got ' + Bool(not Want) + ', want ' + Bool(Want));
  end;

  procedure Invalid(const Pattern: UTF8String);
  var
    Re: TEcmaRegex;
    Error: UTF8String;
  begin
    Inc(Cases);
    if EcmaRegexValid(Pattern) then
      Fail('Valid(' + Quote(Pattern) + ') = true');
    if EcmaRegexCompile(Pattern, Re, Error) then
      Fail('Compile(' + Quote(Pattern) + ') gives no error');
  end;

begin
  Cases := 0;
  // With the u flag grammar the Kelvin sign and the long s fold to k and s. With the Annex B grammar they do not,
  // and the trailing \& is what makes the second pattern of each such pair an Annex B one. A name may be shared by
  // groups in separate alternatives, and a reference means the one that took part.
  Modifier('^(?i:abc)$', 'aBc', True, True);
  Modifier('^a(?i:b)c$', 'aBc', True, True);
  Modifier('^a(?i:b)c$', 'ABc', False, True);
  Modifier('^(?i:a(?-i:b)c)$', 'AbC', True, True);
  Modifier('^(?i:a(?-i:b)c)$', 'ABC', False, True);
  Modifier('^(?i:k)$', 'K', True, True);
  Modifier('^(?i:k)\&?$', 'K', False, True);
  Modifier('^(?i:s)$', 'ſ', True, True);
  Modifier('^(?i:s)\&?$', 'ſ', False, True);
  Modifier('^(?i:\w)$', 'ſ', True, True);
  Modifier('^(?i:\W)$', 'ſ', False, True);
  Modifier('(?i:\b)\u212a', 'K', True, False);
  Modifier('\b\u212a', 'K', False, True);
  Modifier('^(?i:[^a-z])$', 'K', False, True);
  Modifier('^(?i:[^a-z])$', '1', True, True);
  Modifier('^(?i:ß)$', 'ẞ', True, True);
  Modifier('^(?i:ß)$', 'SS', False, True);
  Modifier('^(.)(?i:\1)$', 'aA', True, False);
  Modifier('^(.)\1$', 'aA', False, False);
  Modifier('^(.)(?i:\1)$', 'kK', True, False);
  Modifier('(?<=(?i:\1)(.))x', 'Aax', True, False);
  Modifier('^(?s:.)$', #$000A, True, True);
  Modifier('^.$', #$000A, False, True);
  Modifier('^(?s:a.)b.$', 'a'#$000A'b'#$000A, False, True);
  Modifier('(?m:^)b', 'a'#$000A'b', True, False);
  Modifier('^b', 'a'#$000A'b', False, True);
  Modifier('a(?m:$)', 'a'#$2028'b', True, False);
  Modifier('a(?m:$)', 'ab', False, False);
  Modifier('(?m:^)b', 'a'#$000D'b', True, False);
  Modifier('(?m:^)b', 'a'#$2029'b', True, False);
  Modifier('^(?:(?<a>x)|(?<a>y))\k<a>$', 'yy', True, False);
  Modifier('^(?:(?<a>x)|(?<a>y))\k<a>$', 'xy', False, False);
  Modifier('^(?:(?<a>x)|(?<a>y))+\k<a>$', 'xyy', True, False);
  Modifier('^(?:(?<a>x)|(?<a>y))+\k<a>$', 'xyx', False, False);
  Modifier('^(?<\u0061b>.)\k<ab>$', 'zz', True, False);
  Invalid('(?i)a');
  Invalid('(?-:a)');
  Invalid('(?ii:a)');
  Invalid('(?i-i:a)');
  Invalid('(?x:a)');
  Invalid('(?i-m-s:a)');
  Invalid('(?<a>x)(?<a>y)');
  Invalid('(?<a>x|(?<a>y))');
  Invalid('(?:(?<a>x)|b)(?:(?<a>y)|c)');
  Invalid('(?<a\u0020>x)');
  Invalid('(?<\u0030>x)');
  WriteLn('TestModifiers: ', Cases, ' cases');
end;

{ TestProperties checks that every property name, value and alias resolves, and a few memberships. The whole of each
  set was compared with V8 once, code point by code point, when the tables of the Go module were generated. }
procedure TestProperties;
var
  Expressions, K, V: Int32;
  Scripts: TUcdNameLists;

  procedure Check(const Expr: UTF8String);
  var
    CharSet: TCharSet;
  begin
    Inc(Expressions);
    if not PropertySet(Expr, CharSet) then
      Fail('\p(' + Expr + ') does not resolve');
  end;

  procedure Member(const Pattern, Text: UTF8String; Want: Boolean);
  var
    Re: TEcmaRegex;
  begin
    if not Compiled(Pattern, Re) then
      Exit;
    if MatchString(Re, Text) <> Want then
      Fail(Quote(Pattern) + ' on ' + Quote(Text) + ': got ' + Bool(not Want) + ', want ' + Bool(Want));
  end;

  procedure NotValid(const Pattern: UTF8String);
  begin
    if EcmaRegexValid(Pattern) then
      Fail('Valid(' + Quote(Pattern) + ') = true');
  end;

begin
  Expressions := 0;
  for K := 0 to BinaryNameCount - 1 do
    Check(BinaryNameAt(K));
  for K := 0 to CategoryNameCount - 1 do begin
    Check(CategoryNameAt(K));
    Check('gc=' + CategoryNameAt(K));
    Check('General_Category=' + CategoryNameAt(K));
  end;
  Scripts := UcdScriptNames;
  for K := 0 to High(Scripts) do
    for V := 0 to High(Scripts[K]) do begin
      Check('sc=' + Scripts[K][V]);
      Check('Script=' + Scripts[K][V]);
      Check('scx=' + Scripts[K][V]);
      Check('Script_Extensions=' + Scripts[K][V]);
    end;
  // U+0342 is Inherited by Script and Greek by Script_Extensions.
  Member('^\p{L}$', 'é', True);
  Member('^\p{L}$', '1', False);
  Member('^\p{Lu}$', 'É', True);
  Member('^\P{Lu}$', 'É', False);
  Member('^\p{Nd}$', '৪', True);
  Member('^\p{digit}$', '৪', True);
  Member('^\p{sc=Grek}$', 'α', True);
  Member('^\p{Script=Greek}$', 'a', False);
  Member('^\p{sc=Grek}$', #$0342, False);
  Member('^\p{scx=Grek}$', #$0342, True);
  Member('^\p{sc=Zinh}$', #$0342, True);
  Member('^\p{Emoji}$', '🐲', True);
  Member('^\p{Emoji_Presentation}$', '#', False);
  Member('^\p{Emoji}$', '#', True);
  Member('^\p{Alphabetic}$', 'ⅷ', True);
  Member('^\p{Uppercase}$', 'Ⓐ', True);
  Member('^\p{ID_Start}$', '_', False);
  Member('^\p{ID_Continue}$', '_', True);
  Member('^\p{White_Space}$', #$0085, True);
  Member('^\s$', #$0085, False);
  Member('^\p{White_Space}$', #$FEFF, False);
  Member('^\s$', #$FEFF, True);
  Member('^\p{Any}$', U($10FFFF), True);
  Member('^\p{Assigned}$', U($10FFFF), False);
  Member('^\p{Script=Unknown}$', U($E0080), True);
  Member('^\p{ASCII}$', 'é', False);
  Member('^[\p{Lu}\p{Nd}]+$', 'A1É৪', True);
  Member('^[^\p{L}]+$', '1 -', True);
  Member('^[^\p{L}]+$', '1a', False);
  NotValid('\p{Nope}');
  NotValid('\p{L=x}');
  NotValid('\p{gc=Greek}');
  NotValid('\p{sc=L}');
  NotValid('\p{}');
  NotValid('\p{Lu');
  NotValid('\pL');
  NotValid('\p{sc=}');
  NotValid('\p{ascii}');
  WriteLn('TestProperties: ', Expressions, ' property expressions resolve');
end;

{ TestAutomaton checks which patterns get an automaton over ASCII text, and its answers at the edges: the empty
  text, anchors, an unanchored search, and a text that is not ASCII (which it must hand back). }
procedure TestAutomaton;
var
  Re: TEcmaRegex;
  Text: TBytes;
  Matched: Boolean;

  // A word boundary, and more states than an automaton may have, are the ones with none.
  procedure HasAutomaton(const Pattern: UTF8String; Want: Boolean);
  var
    R: TEcmaRegex;
  begin
    if not Compiled(Pattern, R) then
      Exit;
    if not R.Regular then
      Fail(Quote(Pattern) + ' is not regular');
    if R.HasDfa <> Want then
      Fail(Quote(Pattern) + ': automaton ' + Bool(R.HasDfa) + ', want ' + Bool(Want));
  end;

  procedure Automaton(const Pattern, S: UTF8String; Want: Boolean);
  var
    R: TEcmaRegex;
    T: TBytes;
    M: Boolean;
  begin
    if not Compiled(Pattern, R) then
      Exit;
    T := ToBytes(S);
    if (EcmaRegexIsMatch(R, T, 0, Length(T)) <> Want) or
      (EcmaRegexIsMatch(Backtracking(R), T, 0, Length(T)) <> Want) then
      Fail(Quote(Pattern) + ' on ' + Quote(S) + ': as compiled ' + Bool(EcmaRegexIsMatch(R, T, 0, Length(T))) +
        ', the backtracking matcher ' + Bool(EcmaRegexIsMatch(Backtracking(R), T, 0, Length(T))) + ', want ' +
        Bool(Want));
    if not R.HasDfa then
      Fail(Quote(Pattern) + ': no automaton')
    else if not DfaMatch(R.Dfa, T, 0, Length(T), M) then
      Fail(Quote(Pattern) + ' on ' + Quote(S) + ': the automaton did not decide an ASCII text');
  end;

begin
  HasAutomaton('^[a-z][a-z0-9_]*$', True);
  HasAutomaton('(base64key|awskms)://(.*)', True);
  HasAutomaton('^[Ee][Ss]20(1[5-9]|2[0-2])(\.[a-z]+)?$', True);
  HasAutomaton('^$', True);
  HasAutomaton('$', True);
  HasAutomaton('a*', True);
  HasAutomaton('^(?:a?){40}$', True);
  HasAutomaton('é+', True);
  HasAutomaton('\bfoo', False);
  HasAutomaton('^a{600}$', False);
  HasAutomaton('(?:a{50}){12}', False);
  HasAutomaton('^(?:[a-z]{1,3}\d?){2,90}$', False);
  Automaton('^$', '', True);
  Automaton('^$', 'a', False);
  Automaton('$', 'abc', True);
  Automaton('^', 'abc', True);
  Automaton('$^', '', True);
  Automaton('$^', 'a', False);
  Automaton('a$', 'ba', True);
  Automaton('a$', 'ab', False);
  Automaton('^a', 'ab', True);
  Automaton('^a', 'ba', False);
  Automaton('b', 'aaab', True);
  Automaton('^(a|ab)(c|bcd)$', 'abcd', True);
  Automaton('^(a|ab)(c|bcd)$', 'abc', True);
  Automaton('^(a|ab)(c|bcd)$', 'abcc', False);
  Automaton('x{2,3}', 'axxb', True);
  Automaton('^x{2,3}$', 'xxxx', False);
  Automaton('^.$', #$000A, False);
  Automaton('^[^a]$', #$000A, True);
  Automaton('é', 'caf', False);
  Automaton('^a*$', '', True);
  Automaton('^(a$|b)c?$', 'a', True);
  Automaton('^(a$|b)c?$', 'ac', False);
  if Compiled('^[a-z]+$', Re) then begin
    if (not MatchString(Re, 'abc')) or MatchString(Re, 'café') then
      Fail('^[a-z]+$ on abc and on a text that is not ASCII');
    Text := ToBytes('café');
    if (not Re.HasDfa) or DfaMatch(Re.Dfa, Text, 0, Length(Text), Matched) then
      Fail('the automaton decided a text that is not ASCII');
  end;
  if Compiled('^\p{L}+$', Re) then
    if (not MatchString(Re, 'café')) or MatchString(Re, 'café 1') or not MatchString(Re, 'cafe') then
      Fail('a text that is not ASCII was not handed to the backtracking matcher');
  WriteLn('TestAutomaton: done');
end;

{ TestSlices checks that a match reads only the bytes it is given, which the Go package has no test for because a Go
  slice cannot be read beyond its end. }
procedure TestSlices;
var
  Text: TBytes;
  Re: TEcmaRegex;
begin
  Text := ToBytes('xxabcyy');
  if Compiled('^abc$', Re) then begin
    if not EcmaRegexIsMatch(Re, Text, 2, 3) then
      Fail('^abc$ does not match the middle of xxabcyy');
    if EcmaRegexIsMatch(Re, Text, 1, 4) or EcmaRegexIsMatch(Re, Text, 2, 4) then
      Fail('^abc$ matches more than the bytes it was given');
    if EcmaRegexIsMatch(Backtracking(Re), Text, 1, 4) or not EcmaRegexIsMatch(Backtracking(Re), Text, 2, 3) then
      Fail('^abc$ on the backtracking matcher reads outside the bytes it was given');
  end;
  if Compiled('(?<=x)a', Re) then
    if EcmaRegexIsMatch(Re, Text, 2, 3) or not EcmaRegexIsMatch(Re, Text, 1, 3) then
      Fail('a lookbehind reads before the bytes it was given');
  if Compiled('c(?=y)', Re) then
    if EcmaRegexIsMatch(Re, Text, 2, 3) or not EcmaRegexIsMatch(Re, Text, 2, 4) then
      Fail('a lookahead reads after the bytes it was given');
  if Compiled('^$', Re) then
    if (not EcmaRegexIsMatch(Re, Text, 3, 0)) or (not EcmaRegexIsMatch(Re, nil, 0, 0)) then
      Fail('^$ does not match an empty slice');
  WriteLn('TestSlices: done');
end;

begin
  if ParamCount >= 1 then
    OraclePath := UTF8String(ParamStr(1));
  if ParamCount >= 2 then
    SuitePath := UTF8String(ParamStr(2));
  TestJavaHandWrittenPatterns;
  TestSuiteCorpus;
  TestDifferential;
  TestEngineChoice;
  TestBacktrackingSemantics;
  TestLongInput;
  TestAllocations;
  TestConcurrentUse;
  TestFoldTables;
  TestModifiers;
  TestProperties;
  TestAutomaton;
  TestSlices;
  TestSuite;
  TestOracleHandWritten;
  TestOracleFlags;
  TestOracleGenerated;
  WriteLn('V8 oracle: ', OracleVerdicts, ' verdicts on validity compared, ', OracleVerdictsAgreed, ' agreed. ',
    OracleAnswers, ' answers on a match compared (each text on both back ends), ', OracleAnswersAgreed, ' agreed.');
  if Failures <> 0 then begin
    WriteLn('FAILED: ', Failures, ' failures');
    Halt(1);
  end;
  WriteLn('PASS: 17 tests, 0 failures');
end.