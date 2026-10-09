program TestUcd;
{$I corvus.inc}

{ The tests of the Unicode tables, ported from ucd_test.go of the Go module. The program prints each failure and a
  final count, and exits with a status other than zero when anything failed.

  TestAgreesWithTheToolchainOfTheSameVersion of the Go source compares the tables with the unicode package of the Go
  toolchain when that carries the same version of Unicode. Pascal has no such package to compare with, so that test
  is not here. BenchmarkContains is not here either. }

uses
  SysUtils,
  Corvus.JsonSchema.Ucd;

var
  Failures: Int32 = 0;

procedure Fail(const Message: UTF8String);
begin
  Inc(Failures);
  WriteLn('FAIL: ', Message);
end;

function Hex(R: Int32): UTF8String;
begin
  Result := UTF8String('U+' + IntToHex(R, 4));
end;

function Num(N: Int64): UTF8String;
begin
  Result := UTF8String(IntToStr(N));
end;

type
  TLabelled = record
    Name: UTF8String;
    Table: PUcdTable;
  end;
  TLabelledTables = array of TLabelled;

const
  CategoryNames: array[0..37] of UTF8String = (
    'Lu', 'Ll', 'Lt', 'Lm', 'Lo', 'Mn', 'Mc', 'Me', 'Nd', 'Nl', 'No', 'Pc', 'Pd', 'Ps', 'Pe', 'Pi', 'Pf', 'Po', 'Sm',
    'Sc', 'Sk', 'So', 'Zs', 'Zl', 'Zp', 'Cc', 'Cf', 'Cs', 'Co', 'Cn', 'L', 'LC', 'M', 'N', 'P', 'S', 'Z', 'C');

{ EveryTable returns every table the unit can name, by a label. }
function EveryTable: TLabelledTables;
var
  N, I, K: Int32;
  Binary: TUcdNames;
  Scripts: TUcdNameLists;

  procedure Add(const Name: UTF8String; Table: PUcdTable);
  begin
    if N = Length(Result) then
      SetLength(Result, 2 * N + 64);
    Result[N].Name := Name;
    Result[N].Table := Table;
    Inc(N);
  end;

begin
  Result := nil;
  N := 0;
  for I := 0 to High(CategoryNames) do
    Add('gc=' + CategoryNames[I], UcdCategory(CategoryNames[I]));
  Binary := UcdBinaryNames;
  for I := 0 to High(Binary) do
    Add(Binary[I], UcdBinary(Binary[I]));
  Add('Assigned', UcdBinary('Assigned'));
  Scripts := UcdScriptNames;
  for I := 0 to High(Scripts) do
    for K := 0 to High(Scripts[I]) do begin
      Add('sc=' + Scripts[I][K], UcdScript(Scripts[I][K]));
      Add('scx=' + Scripts[I][K], UcdScriptExtensions(Scripts[I][K]));
    end;
  SetLength(Result, N);
end;

{ TestTablesAreWellFormed checks the invariants Contains and AppendRanges rely on. }
procedure TestTablesAreWellFormed;
var
  All: TLabelledTables;
  T: PUcdTable;
  Pairs: TUcdRunes;
  K, I: Int32;
begin
  All := EveryTable;
  for K := 0 to High(All) do begin
    T := All[K].Table;
    if T = nil then begin
      Fail(All[K].Name + ' has no table');
      Continue;
    end;
    if (Length(T^.R16) mod 2 <> 0) or (Length(T^.R32) mod 2 <> 0) then begin
      Fail(All[K].Name + ' has an odd number of bounds');
      Continue;
    end;
    I := 0;
    while I < Length(T^.R16) do begin
      if (T^.R16[I] > T^.R16[I + 1]) or ((I > 0) and (UInt32(T^.R16[I]) <= UInt32(T^.R16[I - 1]) + 1)) then
        Fail(All[K].Name + ': the 16-bit range at ' + Num(I) + ' is out of order or touches the one before');
      Inc(I, 2);
    end;
    I := 0;
    while I < Length(T^.R32) do begin
      if (T^.R32[I] < $10000) or (T^.R32[I] > T^.R32[I + 1]) or (T^.R32[I + 1] > UcdMaxRune) or
        ((I > 0) and (T^.R32[I] <= T^.R32[I - 1] + 1)) then
        Fail(All[K].Name + ': the 32-bit range at ' + Num(I) +
          ' is out of order, out of bounds or touches the one before');
      Inc(I, 2);
    end;
    Pairs := nil;
    T^.AppendRanges(Pairs);
    I := 0;
    while I < Length(Pairs) do begin
      if (Pairs[I] > Pairs[I + 1]) or ((I > 0) and (Pairs[I] <= Pairs[I - 1] + 1)) then
        Fail(All[K].Name + ': AppendRanges gives a range at ' + Num(I) +
          ' that is out of order or touches the one before');
      Inc(I, 2);
    end;
  end;
  WriteLn('TestTablesAreWellFormed: ', Length(All), ' names resolve');
end;

{ TestContainsAgreesWithTheRanges walks every code point for tables of each shape. }
procedure TestContainsAgreesWithTheRanges;
var
  Empty: TUcdTable;
  List: array[0..11] of PUcdTable;
  Pairs: TUcdRunes;
  K, Next, R: Int32;
  Want: Boolean;
begin
  Empty.R16 := nil;
  Empty.R32 := nil;
  List[0] := UcdCategory('Lu');
  List[1] := UcdCategory('Cn');
  List[2] := UcdCategory('Co');
  List[3] := UcdLetter;
  List[4] := UcdAssigned;
  List[5] := UcdUnknownScript;
  List[6] := UcdBinary('Alphabetic');
  List[7] := UcdBinary('Noncharacter_Code_Point');
  List[8] := UcdScript('Han');
  List[9] := UcdScriptExtensions('Latin');
  List[10] := UcdScript('Adlam');
  List[11] := @Empty;
  for K := 0 to High(List) do begin
    Pairs := nil;
    List[K]^.AppendRanges(Pairs);
    Next := 0;
    for R := 0 to UcdMaxRune do begin
      while (Next < Length(Pairs)) and (Pairs[Next + 1] < R) do
        Inc(Next, 2);
      Want := (Next < Length(Pairs)) and (Pairs[Next] <= R);
      if List[K]^.Contains(R) <> Want then begin
        Fail('table ' + Num(K) + ': Contains(' + Hex(R) + ') disagrees with the ranges');
        Break;
      end;
    end;
    if List[K]^.Contains(-1) or List[K]^.Contains(UcdMaxRune + 1) then
      Fail('table ' + Num(K) + ': Contains accepts a value that is not a code point');
  end;
  WriteLn('TestContainsAgreesWithTheRanges: ', Length(List), ' tables walked');
end;

{ TestGeneralCategoriesPartitionTheCodePoints checks that every code point has exactly one General_Category, that
  the groups hold what their members hold, and that Assigned and the Unknown script follow from the categories. }
procedure TestGeneralCategoriesPartitionTheCodePoints;
const
  GroupNames: array[0..7] of UTF8String = ('L', 'LC', 'M', 'N', 'P', 'S', 'Z', 'C');
  { The members of each group are the elements GroupFirst[G] to GroupFirst[G + 1] - 1 of Members. }
  Members: array[0..35] of UTF8String = (
    'Lu', 'Ll', 'Lt', 'Lm', 'Lo', 'Lu', 'Ll', 'Lt', 'Mn', 'Mc', 'Me', 'Nd', 'Nl', 'No',
    'Pc', 'Pd', 'Ps', 'Pe', 'Pi', 'Pf', 'Po', 'Sm', 'Sc', 'Sk', 'So', 'Zs', 'Zl', 'Zp',
    'Cc', 'Cf', 'Cs', 'Co', 'Cn', '', '', '');
  GroupFirst: array[0..8] of Int32 = (0, 5, 8, 11, 14, 21, 25, 28, 33);
var
  GroupTables: array[0..7] of PUcdTable;
  MemberTables: array[0..32] of PUcdTable;
  Scripts: array of PUcdTable;
  Names: TUcdNameLists;
  G, K, R, Count, InScript, NScripts: Int32;
  Inside, Unknown: Boolean;
  Cn: PUcdTable;
begin
  for G := 0 to 7 do
    GroupTables[G] := UcdCategory(GroupNames[G]);
  for K := 0 to 32 do
    MemberTables[K] := UcdCategory(Members[K]);
  Names := UcdScriptNames;
  SetLength(Scripts, Length(Names));
  NScripts := 0;
  for K := 0 to High(Names) do
    if Names[K][0] <> 'Unknown' then begin
      Scripts[NScripts] := UcdScript(Names[K][0]);
      Inc(NScripts);
    end;
  Cn := UcdCategory('Cn');
  for R := 0 to UcdMaxRune do begin
    Count := 0;
    for G := 0 to 7 do begin
      Inside := False;
      for K := GroupFirst[G] to GroupFirst[G + 1] - 1 do
        Inside := Inside or MemberTables[K]^.Contains(R);
      if (GroupNames[G] <> 'LC') and Inside then
        Inc(Count);
      if GroupTables[G]^.Contains(R) <> Inside then begin
        Fail(Hex(R) + ': the group ' + GroupNames[G] + ' and its members disagree');
        Exit;
      end;
    end;
    if Count <> 1 then begin
      Fail(Hex(R) + ' is in ' + Num(Count) + ' General_Category groups');
      Exit;
    end;
    if UcdAssigned^.Contains(R) = Cn^.Contains(R) then begin
      Fail(Hex(R) + ': Assigned is not the complement of Cn');
      Exit;
    end;
    InScript := 0;
    for K := 0 to NScripts - 1 do
      if Scripts[K]^.Contains(R) then
        Inc(InScript);
    Unknown := UcdUnknownScript^.Contains(R);
    if (InScript > 1) or ((InScript = 0) <> Unknown) then begin
      Fail(Hex(R) + ' is in ' + Num(InScript) + ' scripts, and Unknown disagrees');
      Exit;
    end;
  end;
  WriteLn('TestGeneralCategoriesPartitionTheCodePoints: ', NScripts, ' scripts and 8 groups over every code point');
end;

procedure CheckContains(Table: PUcdTable; R: Int32; Want: Boolean; const What: UTF8String);
begin
  if Table = nil then
    Fail('no table: ' + What)
  else if Table^.Contains(R) <> Want then
    Fail('Contains(' + Hex(R) + ') is wrong: ' + What);
end;

procedure CheckFold(From, Want: Int32);
begin
  if UcdFold(From) <> Want then
    Fail('Fold(' + Hex(From) + ') = ' + Hex(UcdFold(From)) + ', want ' + Hex(Want));
end;

procedure CheckToUpper(From, Want: Int32);
begin
  if UcdToUpper(From) <> Want then
    Fail('ToUpper(' + Hex(From) + ') = ' + Hex(UcdToUpper(From)) + ', want ' + Hex(Want));
end;

{ TestTheDataIsUnicode17 checks code points whose properties older versions of Unicode do not have. }
procedure TestTheDataIsUnicode17;
const
  Pairs: array[0..3, 0..1] of Int32 = (($10D50, $10D70), ($16EA0, $16EBB), ($A7CB, $264), ($1C89, $1C8A));
  NoNames: array[0..5] of UTF8String = ('Nope', '', 'l', 'Latn=', 'Any', 'ASCII');
var
  K: Int32;
begin
  if UcdVersion <> '17.0.0' then begin
    Fail('Version = ' + UcdVersion + '. Update this test for the new data.');
    Exit;
  end;
  CheckContains(UcdScript('Garay'), $10D40, True, 'Garay is a script of Unicode 16');
  CheckContains(UcdScript('Gara'), $10D40, True, 'Gara is the code of Garay');
  CheckContains(UcdScript('Sidetic'), $10940, True, 'Sidetic is a script of Unicode 17');
  CheckContains(UcdScript('Beria_Erfe'), $16EA0, True, 'Beria Erfe is a script of Unicode 17');
  CheckContains(UcdCategory('Lu'), $16EA0, True, 'U+16EA0 is an uppercase letter of Unicode 17');
  CheckContains(UcdCategory('Cn'), $16EA0, False, 'U+16EA0 is assigned in Unicode 17');
  CheckContains(UcdCategory('Cn'), $1FAE9, False, 'U+1FAE9 is assigned in Unicode 16');
  CheckContains(UcdBinary('Emoji'), $1FAE9, True, 'U+1FAE9 is an emoji of Unicode 16');
  CheckContains(UcdScript('Han'), $323B0, True, 'U+323B0 starts CJK extension J of Unicode 17');
  CheckContains(UcdUnknownScript, $10D40, False, 'U+10D40 has a script');
  CheckContains(UcdAssigned, $10FFFF, False, 'U+10FFFF is a noncharacter');
  // Case pairs of Unicode 16 and 17.
  for K := 0 to High(Pairs) do begin
    CheckFold(Pairs[K][0], Pairs[K][1]);
    CheckToUpper(Pairs[K][1], Pairs[K][0]);
  end;
  for K := 0 to High(NoNames) do
    if (UcdCategory(NoNames[K]) <> nil) or (UcdScript(NoNames[K]) <> nil) or
      (UcdScriptExtensions(NoNames[K]) <> nil) or (UcdBinary(NoNames[K]) <> nil) then
      Fail('"' + NoNames[K] + '" names a table');
  WriteLn('TestTheDataIsUnicode17: done');
end;

{ TestCaseMappings checks Fold and ToUpper over every code point. }
procedure TestCaseMappings;
const
  Folds: array[0..9, 0..1] of Int32 = ((Ord('A'), Ord('a')), (Ord('z'), Ord('z')), ($B5, $3BC), ($17F, Ord('s')),
    ($212A, Ord('k')), ($1E9E, $DF), ($3C2, $3C3), ($130, $130), ($1F88, $1F80), ($1E921, $1E943));
  Uppers: array[0..5, 0..1] of Int32 = ((Ord('a'), Ord('A')), ($DF, $DF), ($FF, $178), ($131, Ord('I')),
    ($17F, Ord('S')), ($1C5, $1C4));
var
  Folding: TUcdRunes;
  Next, R, Folded, K: Int32;
  Changes: Boolean;
begin
  Folding := nil;
  UcdAppendFolding(Folding);
  Next := 0;
  for R := 0 to UcdMaxRune do begin
    Folded := UcdFold(R);
    if UcdFold(Folded) <> Folded then begin
      Fail('Fold is not idempotent at ' + Hex(R));
      Exit;
    end;
    Changes := (Next < Length(Folding)) and (Folding[Next] = R);
    if Changes then
      Inc(Next);
    if Changes <> (Folded <> R) then begin
      Fail('AppendFolding and Fold disagree at ' + Hex(R));
      Exit;
    end;
    if R < $80 then
      if (Folded <> UcdFoldByTable(R)) or (UcdToUpper(R) <> UcdToUpperByTable(R)) then begin
        Fail('the ASCII path and the table disagree at ' + Hex(R));
        Exit;
      end;
  end;
  for K := 0 to High(Folds) do
    CheckFold(Folds[K][0], Folds[K][1]);
  for K := 0 to High(Uppers) do
    CheckToUpper(Uppers[K][0], Uppers[K][1]);
  WriteLn('TestCaseMappings: ', Length(Folding), ' code points have a folding');
end;

begin
  TestTablesAreWellFormed;
  TestContainsAgreesWithTheRanges;
  TestGeneralCategoriesPartitionTheCodePoints;
  TestTheDataIsUnicode17;
  TestCaseMappings;
  if Failures <> 0 then begin
    WriteLn('FAILED: ', Failures, ' failures');
    Halt(1);
  end;
  WriteLn('PASS: 5 tests, 0 failures');
end.