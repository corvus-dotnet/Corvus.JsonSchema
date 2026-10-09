unit Corvus.JsonSchema.Ucd;
{$I corvus.inc}

{ Unit Ucd holds the Unicode Character Database properties the package reads, for one fixed version of Unicode.

  Nothing in the package takes Unicode properties from the run-time library of the compiler, because that library
  follows the compiler (and, for some of its functions, the operating system). With its own tables the package gives
  the same answers whichever compiler builds it and wherever it runs, and the answers change only when the tables
  are generated again. UcdVersion names the version of Unicode they hold.

  The tables are in Corvus.JsonSchema.UcdTables.inc, which tools/gen-ucd-tables.ps1 writes. Each is a sorted list of
  inclusive ranges, the code points up to U+FFFF as 16-bit bounds and the rest as 32-bit bounds. A value that follows
  from other values (a General_Category group such as L, the Assigned property, the Unknown script) has no table of
  its own and is built from them.

  This is a port of the ucd package of the Go module (src-go/corvus-json-schema/internal/ucd). Two things differ.
  The Go package builds a value that follows from others when it is first asked for, behind a sync.Once. Here every
  table is made when the unit is initialised, which takes well under a millisecond, so that reading a table needs no
  lock and no once. And Go finds a table by name with a switch over strings, which Delphi does not have, so the
  generated file holds each list of names in ordinal order and the unit searches it. }

interface

const
  { UcdMaxRune is the last Unicode code point. }
  UcdMaxRune = $10FFFF;

type
  { A list of code points. }
  TUcdRunes = array of Int32;
  { A list of names, and a list of those. }
  TUcdNames = array of UTF8String;
  TUcdNameLists = array of TUcdNames;

  { A TUcdTable is an immutable set of code points. }
  TUcdTable = record
    { R16 holds the ranges that end at or below U+FFFF and R32 the ranges that start above it, each as inclusive
      pairs in ascending order. No two ranges of one list overlap or touch. }
    R16: array of UInt16;
    R32: array of UInt32;
    { Contains reports whether the table holds the code point. }
    function Contains(R: Int32): Boolean;
    { AppendRanges appends the ranges of the table to Dst as inclusive pairs in ascending order. The pairs neither
      overlap nor touch. }
    procedure AppendRanges(var Dst: TUcdRunes);
  end;
  PUcdTable = ^TUcdTable;

{ UcdVersion returns the version of Unicode the tables hold. }
function UcdVersion: UTF8String;

{ UcdUnion builds the table of the code points that any of the tables holds. A caller that tests one code point
  against several properties builds their union once and searches one table. }
function UcdUnion(const Tables: array of PUcdTable): TUcdTable;

{ The General_Category values that are groups of other values, the Assigned property and the Unknown script.
  tools/gen-ucd-tables.ps1 checks each definition against the source of the tables. }

{ UcdLetter is the General_Category group L. }
function UcdLetter: PUcdTable;
{ UcdCasedLetter is the General_Category group LC. }
function UcdCasedLetter: PUcdTable;
{ UcdMark is the General_Category group M. }
function UcdMark: PUcdTable;
{ UcdNumber is the General_Category group N. }
function UcdNumber: PUcdTable;
{ UcdPunctuation is the General_Category group P. }
function UcdPunctuation: PUcdTable;
{ UcdSymbol is the General_Category group S. }
function UcdSymbol: PUcdTable;
{ UcdSeparator is the General_Category group Z. }
function UcdSeparator: PUcdTable;
{ UcdOther is the General_Category group C. }
function UcdOther: PUcdTable;
{ UcdAssigned is the code points that have a General_Category other than Cn. }
function UcdAssigned: PUcdTable;
{ UcdUnknownScript is the code points that belong to no script. They are the unassigned, private use and surrogate
  code points. }
function UcdUnknownScript: PUcdTable;

{ UcdCategory returns the table of a General_Category value by its short name, such as 'Lu' or 'L'. It returns nil
  for any other name. }
function UcdCategory(const Name: UTF8String): PUcdTable;

{ UcdBinary returns the table of a binary property by its canonical name, such as 'Alphabetic' or 'White_Space'. It
  returns nil for any other name. ASCII and Any, which ECMA-262 adds to the properties of Unicode, are not here. }
function UcdBinary(const Name: UTF8String): PUcdTable;

{ UcdScript returns the table of a Script value by its long name or an alias, such as 'Greek' or 'Grek'. It returns
  nil for any other name. }
function UcdScript(const Name: UTF8String): PUcdTable;

{ UcdScriptExtensions returns the table of a Script_Extensions value by the long name of the script or an alias. It
  returns nil for any other name. }
function UcdScriptExtensions(const Name: UTF8String): PUcdTable;

{ UcdScriptNames returns the names of every script. Each element is the long name of one script followed by its
  aliases. The caller must not change it. }
function UcdScriptNames: TUcdNameLists;

{ UcdBinaryNames returns the canonical name of every binary property that has a table, which is every name
  UcdBinary knows but Assigned. The caller must not change it. }
function UcdBinaryNames: TUcdNames;

{ UcdFold returns the simple case folding of a code point (the C and S rows of CaseFolding.txt), which is the code
  point itself when it has none. Two code points are equal ignoring case when UcdFold gives the same result for
  both. }
function UcdFold(R: Int32): Int32;

{ UcdToUpper returns the simple uppercase mapping of a code point, which is the code point itself when it has
  none. }
function UcdToUpper(R: Int32): Int32;

{ UcdAppendFolding appends every code point that UcdFold changes, in ascending order. }
procedure UcdAppendFolding(var Dst: TUcdRunes);

{ UcdFoldByTable and UcdToUpperByTable are UcdFold and UcdToUpper without their path for ASCII. The tests compare
  the two paths. }
function UcdFoldByTable(R: Int32): Int32;
function UcdToUpperByTable(R: Int32): Int32;

implementation

type
  { A TUcdCaseRange maps some of the code points First, First+1, ... First+Length-1 to themselves plus Delta. The
    ones it maps are those whose offset from First has none of the bits of Mask, so a mask of 1 takes every other
    code point. }
  TUcdCaseRange = record
    First: Int32;
    Delta: Int32;
    Length: UInt16;
    Mask: UInt16;
  end;

  TInt64Array = array of Int64;

const
  { The tables that are built from others follow the generated ones in Tables. }
  DerivedLetter = 0;
  DerivedCasedLetter = 1;
  DerivedMark = 2;
  DerivedNumber = 3;
  DerivedPunctuation = 4;
  DerivedSymbol = 5;
  DerivedSeparator = 6;
  DerivedOther = 7;
  DerivedAssigned = 8;
  DerivedUnknownScript = 9;
  DerivedCount = 10;

procedure Load16(Table: Int32; const Ranges: array of UInt16); forward;
procedure Load32(Table: Int32; const Ranges: array of UInt32); forward;

{$I Corvus.JsonSchema.UcdTables.inc}

var
  { Tables holds the generated tables, in the order of their numbers, and then the ones built from them. Nothing
    writes to it once the unit is initialised. }
  Tables: array[0..TableCount + DerivedCount - 1] of TUcdTable;
  ScriptNameLists: TUcdNameLists;
  BinaryNames: TUcdNames;

procedure Load16(Table: Int32; const Ranges: array of UInt16);
var
  I: Int32;
begin
  SetLength(Tables[Table].R16, Length(Ranges));
  for I := 0 to High(Ranges) do
    Tables[Table].R16[I] := Ranges[I];
end;

procedure Load32(Table: Int32; const Ranges: array of UInt32);
var
  I: Int32;
begin
  SetLength(Tables[Table].R32, Length(Ranges));
  for I := 0 to High(Ranges) do
    Tables[Table].R32[I] := Ranges[I];
end;

function UcdVersion: UTF8String;
begin
  Result := TablesVersion;
end;

function TUcdTable.Contains(R: Int32): Boolean;
var
  C: UInt32;
  Lo, Hi, Mid: Int32;
begin
  C := UInt32(R);
  if C <= $FFFF then begin
    Lo := 0;
    Hi := Length(R16) div 2;
    while Lo < Hi do begin
      Mid := (Lo + Hi) shr 1;
      if C > R16[2 * Mid + 1] then
        Lo := Mid + 1
      else if C < R16[2 * Mid] then
        Hi := Mid
      else
        Exit(True);
    end;
    Exit(False);
  end;
  Lo := 0;
  Hi := Length(R32) div 2;
  while Lo < Hi do begin
    Mid := (Lo + Hi) shr 1;
    if C > R32[2 * Mid + 1] then
      Lo := Mid + 1
    else if C < R32[2 * Mid] then
      Hi := Mid
    else
      Exit(True);
  end;
  Result := False;
end;

procedure TUcdTable.AppendRanges(var Dst: TUcdRunes);
var
  At, I, From: Int32;
  Joined: Boolean;
begin
  At := Length(Dst);
  // A range that crosses U+FFFF is stored in two parts.
  Joined := (Length(R16) > 0) and (Length(R32) > 0) and (R16[High(R16)] = $FFFF) and (R32[0] = $10000);
  if Joined then
    SetLength(Dst, At + Length(R16) + Length(R32) - 2)
  else
    SetLength(Dst, At + Length(R16) + Length(R32));
  for I := 0 to High(R16) do begin
    Dst[At] := R16[I];
    Inc(At);
  end;
  From := 0;
  if Joined then begin
    Dst[At - 1] := Int32(R32[1]);
    From := 2;
  end;
  for I := From to High(R32) do begin
    Dst[At] := Int32(R32[I]);
    Inc(At);
  end;
end;

{ NewTable builds a table from inclusive pairs in ascending order that neither overlap nor touch. }
function NewTable(const Pairs: TUcdRunes): TUcdTable;
var
  I, N16, N32, Lo, Hi: Int32;
begin
  SetLength(Result.R16, Length(Pairs) + 2);
  SetLength(Result.R32, Length(Pairs) + 2);
  N16 := 0;
  N32 := 0;
  I := 0;
  while I < Length(Pairs) do begin
    Lo := Pairs[I];
    Hi := Pairs[I + 1];
    if Hi <= $FFFF then begin
      Result.R16[N16] := UInt16(Lo);
      Result.R16[N16 + 1] := UInt16(Hi);
      Inc(N16, 2);
    end else if Lo > $FFFF then begin
      Result.R32[N32] := UInt32(Lo);
      Result.R32[N32 + 1] := UInt32(Hi);
      Inc(N32, 2);
    end else begin
      Result.R16[N16] := UInt16(Lo);
      Result.R16[N16 + 1] := $FFFF;
      Inc(N16, 2);
      Result.R32[N32] := $10000;
      Result.R32[N32 + 1] := UInt32(Hi);
      Inc(N32, 2);
    end;
    Inc(I, 2);
  end;
  SetLength(Result.R16, N16);
  SetLength(Result.R32, N32);
end;

{ SortInt64 sorts the elements Lo to Hi of an array into ascending order. }
procedure SortInt64(var A: TInt64Array; Lo, Hi: Int32);
var
  I, J: Int32;
  Pivot, Swap: Int64;
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
    // The smaller part is sorted by a call and the larger by the loop, so the depth of the calls stays small.
    if J - Lo < Hi - I then begin
      SortInt64(A, Lo, J);
      Lo := I;
    end else begin
      SortInt64(A, I, Hi);
      Hi := J;
    end;
  end;
end;

function UcdUnion(const Tables: array of PUcdTable): TUcdTable;
var
  All, Merged: TUcdRunes;
  Order: TInt64Array;
  I, N, M, Lo, Hi: Int32;
begin
  All := nil;
  for I := 0 to High(Tables) do
    Tables[I]^.AppendRanges(All);
  N := Length(All) div 2;
  // The ranges are sorted by their first code point. A range is one number here, its first code point above its
  // last, so that sorting the numbers sorts the ranges.
  SetLength(Order, N);
  for I := 0 to N - 1 do
    Order[I] := (Int64(All[2 * I]) shl 32) or Int64(All[2 * I + 1]);
  SortInt64(Order, 0, N - 1);
  SetLength(Merged, Length(All));
  M := 0;
  for I := 0 to N - 1 do begin
    Lo := Int32(Order[I] shr 32);
    Hi := Int32(Order[I] and $FFFFFFFF);
    if (M > 0) and (Lo <= Merged[M - 1] + 1) then begin
      if Hi > Merged[M - 1] then
        Merged[M - 1] := Hi;
      Continue;
    end;
    Merged[M] := Lo;
    Merged[M + 1] := Hi;
    Inc(M, 2);
  end;
  SetLength(Merged, M);
  Result := NewTable(Merged);
end;

{ Complement builds the table of the code points that the table does not hold. }
function Complement(const T: TUcdTable): TUcdTable;
var
  Pairs, Inverse: TUcdRunes;
  I, N, Next: Int32;
begin
  Pairs := nil;
  T.AppendRanges(Pairs);
  SetLength(Inverse, Length(Pairs) + 2);
  N := 0;
  Next := 0;
  I := 0;
  while I < Length(Pairs) do begin
    if Pairs[I] > Next then begin
      Inverse[N] := Next;
      Inverse[N + 1] := Pairs[I] - 1;
      Inc(N, 2);
    end;
    Next := Pairs[I + 1] + 1;
    Inc(I, 2);
  end;
  if Next <= UcdMaxRune then begin
    Inverse[N] := Next;
    Inverse[N + 1] := UcdMaxRune;
    Inc(N, 2);
  end;
  SetLength(Inverse, N);
  Result := NewTable(Inverse);
end;

function UcdLetter: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedLetter];
end;

function UcdCasedLetter: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedCasedLetter];
end;

function UcdMark: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedMark];
end;

function UcdNumber: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedNumber];
end;

function UcdPunctuation: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedPunctuation];
end;

function UcdSymbol: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedSymbol];
end;

function UcdSeparator: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedSeparator];
end;

function UcdOther: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedOther];
end;

function UcdAssigned: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedAssigned];
end;

function UcdUnknownScript: PUcdTable;
begin
  Result := @Tables[TableCount + DerivedUnknownScript];
end;

{ FindTable searches names in ordinal order for a name, and returns the table of the same position, or nil when the
  name is not there. This is what a switch over the names is in the Go source. }
function FindTable(const Names: array of UTF8String; const TableOf: array of Int32;
  const Name: UTF8String): PUcdTable;
var
  Lo, Hi, Mid: Int32;
begin
  Lo := 0;
  Hi := Length(Names);
  while Lo < Hi do begin
    Mid := (Lo + Hi) shr 1;
    if Names[Mid] < Name then
      Lo := Mid + 1
    else if Names[Mid] > Name then
      Hi := Mid
    else
      Exit(@Tables[TableOf[Mid]]);
  end;
  Result := nil;
end;

function UcdCategory(const Name: UTF8String): PUcdTable;
begin
  if Name = 'L' then
    Exit(UcdLetter);
  if Name = 'LC' then
    Exit(UcdCasedLetter);
  if Name = 'M' then
    Exit(UcdMark);
  if Name = 'N' then
    Exit(UcdNumber);
  if Name = 'P' then
    Exit(UcdPunctuation);
  if Name = 'S' then
    Exit(UcdSymbol);
  if Name = 'Z' then
    Exit(UcdSeparator);
  if Name = 'C' then
    Exit(UcdOther);
  Result := FindTable(CategoryTableNames, CategoryTableTables, Name);
end;

function UcdBinary(const Name: UTF8String): PUcdTable;
begin
  if Name = 'Assigned' then
    Exit(UcdAssigned);
  Result := FindTable(BinaryTableNames, BinaryTableTables, Name);
end;

function UcdScript(const Name: UTF8String): PUcdTable;
begin
  if (Name = 'Unknown') or (Name = 'Zzzz') then
    Exit(UcdUnknownScript);
  Result := FindTable(ScriptTableNames, ScriptTableTables, Name);
end;

function UcdScriptExtensions(const Name: UTF8String): PUcdTable;
begin
  if (Name = 'Unknown') or (Name = 'Zzzz') then
    Exit(UcdUnknownScript);
  Result := FindTable(ScriptExtensionsTableNames, ScriptExtensionsTableTables, Name);
end;

function UcdScriptNames: TUcdNameLists;
begin
  Result := ScriptNameLists;
end;

function UcdBinaryNames: TUcdNames;
begin
  Result := BinaryNames;
end;

function MapCase(const Table: array of TUcdCaseRange; R: Int32): Int32;
var
  Lo, Hi, Mid, Offset: Int32;
begin
  Lo := 0;
  Hi := Length(Table);
  while Lo < Hi do begin
    Mid := (Lo + Hi) shr 1;
    if R < Table[Mid].First then
      Hi := Mid
    else begin
      Offset := R - Table[Mid].First;
      if Offset >= Table[Mid].Length then
        Lo := Mid + 1
      else if (Offset and Table[Mid].Mask) = 0 then
        Exit(R + Table[Mid].Delta)
      else
        Exit(R);
    end;
  end;
  Result := R;
end;

function UcdFold(R: Int32): Int32;
begin
  if R < $80 then begin
    if (R >= Ord('A')) and (R <= Ord('Z')) then
      Exit(R + (Ord('a') - Ord('A')));
    Exit(R);
  end;
  Result := MapCase(CaseFolds, R);
end;

function UcdToUpper(R: Int32): Int32;
begin
  if R < $80 then begin
    if (R >= Ord('a')) and (R <= Ord('z')) then
      Exit(R - (Ord('a') - Ord('A')));
    Exit(R);
  end;
  Result := MapCase(UpperCases, R);
end;

function UcdFoldByTable(R: Int32): Int32;
begin
  Result := MapCase(CaseFolds, R);
end;

function UcdToUpperByTable(R: Int32): Int32;
begin
  Result := MapCase(UpperCases, R);
end;

procedure UcdAppendFolding(var Dst: TUcdRunes);
var
  I, N, Offset: Int32;
begin
  N := Length(Dst);
  for I := 0 to High(CaseFolds) do
    if CaseFolds[I].Delta <> 0 then
      for Offset := 0 to Int32(CaseFolds[I].Length) - 1 do
        if (Offset and CaseFolds[I].Mask) = 0 then begin
          if N = Length(Dst) then
            SetLength(Dst, 2 * N + 64);
          Dst[N] := CaseFolds[I].First + Offset;
          Inc(N);
        end;
  SetLength(Dst, N);
end;

{ BuildTables makes every table: the generated ones from their constants and the others from those. }
procedure BuildTables;
var
  I, K: Int32;
begin
  LoadTables;
  Tables[TableCount + DerivedLetter] :=
    UcdUnion([@Tables[GcLu], @Tables[GcLl], @Tables[GcLt], @Tables[GcLm], @Tables[GcLo]]);
  Tables[TableCount + DerivedCasedLetter] := UcdUnion([@Tables[GcLu], @Tables[GcLl], @Tables[GcLt]]);
  Tables[TableCount + DerivedMark] := UcdUnion([@Tables[GcMn], @Tables[GcMc], @Tables[GcMe]]);
  Tables[TableCount + DerivedNumber] := UcdUnion([@Tables[GcNd], @Tables[GcNl], @Tables[GcNo]]);
  Tables[TableCount + DerivedPunctuation] := UcdUnion([@Tables[GcPc], @Tables[GcPd], @Tables[GcPs], @Tables[GcPe],
    @Tables[GcPi], @Tables[GcPf], @Tables[GcPo]]);
  Tables[TableCount + DerivedSymbol] := UcdUnion([@Tables[GcSm], @Tables[GcSc], @Tables[GcSk], @Tables[GcSo]]);
  Tables[TableCount + DerivedSeparator] := UcdUnion([@Tables[GcZs], @Tables[GcZl], @Tables[GcZp]]);
  Tables[TableCount + DerivedOther] :=
    UcdUnion([@Tables[GcCc], @Tables[GcCf], @Tables[GcCs], @Tables[GcCo], @Tables[GcCn]]);
  Tables[TableCount + DerivedAssigned] := Complement(Tables[GcCn]);
  Tables[TableCount + DerivedUnknownScript] := UcdUnion([@Tables[GcCn], @Tables[GcCo], @Tables[GcCs]]);

  SetLength(ScriptNameLists, ScriptCount);
  for I := 0 to ScriptCount - 1 do begin
    SetLength(ScriptNameLists[I], ScriptNameFirst[I + 1] - ScriptNameFirst[I]);
    for K := ScriptNameFirst[I] to ScriptNameFirst[I + 1] - 1 do
      ScriptNameLists[I][K - ScriptNameFirst[I]] := ScriptNameList[K];
  end;
  SetLength(BinaryNames, Length(BinaryNameList));
  for I := 0 to High(BinaryNameList) do
    BinaryNames[I] := BinaryNameList[I];
end;

initialization
  BuildTables;
end.