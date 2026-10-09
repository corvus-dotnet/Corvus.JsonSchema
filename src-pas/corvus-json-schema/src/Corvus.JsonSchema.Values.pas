unit Corvus.JsonSchema.Values;

{$I corvus.inc}

{ JSON equality, hashing and uniqueness over values in documents (an instance against a schema's constant). A port
  of values.go of the Go module. StrHash, which values.go also holds, is in Corvus.JsonSchema.Document. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document;

{ ValuesEqual is JSON equality: numbers by value, objects by their property sets, arrays element by element. }
function ValuesEqual(const A: TDocument; X: Int32; const B: TDocument; Y: Int32): Boolean;
{ StringsEqual says whether two string values (or property names) are the same bytes. }
function StringsEqual(const A: TDocument; X: Int32; const B: TDocument; Y: Int32): Boolean;
{ FindProperty is the value of the property of AObject in D that has the name of the string value Key of KeyDoc, or
  -1. }
function FindProperty(const D: TDocument; AObject: Int32; const KeyDoc: TDocument; Key: Int32): Int32;
{ ValueHash is a hash that agrees with JSON equality (object hashing is independent of the member order). }
function ValueHash(const D: TDocument; V: Int32): UInt64;
{ AllUnique decides uniqueItems: pairwise for short arrays, otherwise sorted by hash in Scratch, each entry the
  hash's high half and the item's index, so that only items with equal hashes are compared. It allocates nothing
  once Scratch has grown. }
function AllUnique(const D: TDocument; AArray: Int32; var Scratch: TUInt64Array): Boolean;
function AllStrings(const D: TDocument; First, N: Int32): Boolean;

implementation

uses
  Corvus.JsonSchema.Numbers;

const
  HashK = UInt64($9E3779B97F4A7C15);
  Two63: Double = 9223372036854775808.0;

function StringsEqual(const A: TDocument; X: Int32; const B: TDocument; Y: Int32): Boolean;
var
  Len: Int32;
begin
  Len := DocCount(A, X);
  if Len <> DocCount(B, Y) then
    Result := False
  else if DocStrInText(B, Y) then
    Result := DocStrEquals(A, X, B.Text, DocStrOffset(B, Y), Len)
  else
    Result := DocStrEquals(A, X, B.Source, DocStrOffset(B, Y), Len);
end;

function ValuesEqual(const A: TDocument; X: Int32; const B: TDocument; Y: Int32): Boolean;
var
  Kind: Byte;
  N, P, Q, I, J, K, L, Key, W: Int32;
begin
  Kind := DocKind(A, X);
  if Kind <> DocKind(B, Y) then begin
    Result := False;
    Exit;
  end;
  case Kind of
    KindNull: Result := True;
    KindBool: Result := DocBoolean(A, X) = DocBoolean(B, Y);
    KindNumber: Result := CompareNumbers(DocFlags(A, X), DocData(A, X), DocFlags(B, Y), DocData(B, Y)) = 0;
    KindString: Result := StringsEqual(A, X, B, Y);
    KindArray: begin
      Result := False;
      N := DocCount(A, X);
      if N <> DocCount(B, Y) then
        Exit;
      P := DocFirst(A, X);
      Q := DocFirst(B, Y);
      for I := 0 to N - 1 do
        if not ValuesEqual(A, P + I, B, Q + I) then
          Exit;
      Result := True;
    end;
  else
    Result := False;
    N := DocCount(A, X);
    if N <> DocCount(B, Y) then
      Exit;
    { Objects usually list their members in the same order: compare position by position, and look names up only
      from the first position where the names differ. }
    P := DocFirst(A, X);
    Q := DocFirst(B, Y);
    for I := 0 to N - 1 do begin
      K := P + 2 * I;
      L := Q + 2 * I;
      if not StringsEqual(A, K, B, L) then begin
        for J := I to N - 1 do begin
          Key := P + 2 * J;
          W := FindProperty(B, Y, A, Key);
          if (W < 0) or not ValuesEqual(A, Key + 1, B, W) then
            Exit;
        end;
        Result := True;
        Exit;
      end;
      if not ValuesEqual(A, K + 1, B, L + 1) then
        Exit;
    end;
    Result := True;
  end;
end;

function FindProperty(const D: TDocument; AObject: Int32; const KeyDoc: TDocument; Key: Int32): Int32;
var
  K, I: Int32;
begin
  K := DocFirst(D, AObject);
  for I := DocCount(D, AObject) downto 1 do begin
    if StringsEqual(D, K, KeyDoc, Key) then begin
      Result := K + 1;
      Exit;
    end;
    Inc(K, 2);
  end;
  Result := -1;
end;

function ValueHash(const D: TDocument; V: Int32): UInt64;
var
  Flag: Byte;
  Data, H: UInt64;
  F: Double;
  N, C, I: Int32;
begin
  case DocKind(D, V) of
    KindNull: Result := $53;
    KindBool:
      if DocBoolean(D, V) then
        Result := $52
      else
        Result := $51;
    KindNumber: begin
      Flag := DocFlags(D, V);
      Data := DocData(D, V);
      if Flag = NumFloat then begin
        F := DoubleFromBits(Data);
        { Integral floats hash as the integer they equal. }
        if (F = FloorOf(F)) and (Abs(F) < Two63) then
          Result := (UInt64(Trunc(F)) * HashK) xor UInt64($1234)
        else
          Result := (DoubleToBits(F) * HashK) xor UInt64($4321);
      end else if Flag = NumUint then
        { Floats at or above 2^63 are integers, and never equal a UInt64 exactly unless the UInt64 is a float: hash
          both by the float. }
        Result := (DoubleToBits(UInt64ToDouble(Data)) * HashK) xor UInt64($4321)
      else
        Result := (Data * HashK) xor UInt64($1234);
    end;
    KindString:
      if DocStrInText(D, V) then
        Result := StrHash(D.Text, DocStrOffset(D, V), DocCount(D, V))
      else
        Result := StrHash(D.Source, DocStrOffset(D, V), DocCount(D, V));
    KindArray: begin
      N := DocCount(D, V);
      H := UInt64($54) + UInt64(N);
      C := DocFirst(D, V);
      for I := 0 to N - 1 do
        H := H * UInt64(31) + ValueHash(D, C + I);
      Result := H;
    end;
  else
    { Equal objects have the same member values, so a sum of the values' hashes (whatever the order) agrees with
      equality. }
    N := DocCount(D, V);
    H := UInt64($55) + UInt64(N);
    C := DocFirst(D, V);
    for I := 0 to N - 1 do
      H := H + ValueHash(D, C + 2 * I + 1) * UInt64($2C1B3C6D);
    Result := H;
  end;
end;

function AllUnique(const D: TDocument; AArray: Int32; var Scratch: TUInt64Array): Boolean;
var
  N, C, I, J, Start, Stop: Int32;
begin
  Result := True;
  N := DocCount(D, AArray);
  if N < 2 then
    Exit;
  Result := False;
  C := DocFirst(D, AArray);
  if (N <= 32) and AllStrings(D, C, N) then begin
    { Lists of names, the common case: pairwise, comparing lengths first. }
    for I := 1 to N - 1 do
      for J := 0 to I - 1 do
        if StringsEqual(D, C + I, D, C + J) then
          Exit;
    Result := True;
    Exit;
  end;
  if N <= 16 then begin
    for I := 1 to N - 1 do
      for J := 0 to I - 1 do
        if ValuesEqual(D, C + I, D, C + J) then
          Exit;
    Result := True;
    Exit;
  end;
  if Length(Scratch) < N then
    SetLength(Scratch, N * 2);
  for I := 0 to N - 1 do
    Scratch[I] := (ValueHash(D, C + I) and UInt64($FFFFFFFF00000000)) or UInt64(I);
  SortUInt64(Scratch, N);
  Start := 0;
  for Stop := 1 to N do
    if (Stop = N) or (Scratch[Stop] shr 32 <> Scratch[Start] shr 32) then begin
      for I := Start + 1 to Stop - 1 do
        for J := Start to I - 1 do
          if ValuesEqual(D, C + Int32(Scratch[I] and $FFFFFFFF), D, C + Int32(Scratch[J] and $FFFFFFFF)) then
            Exit;
      Start := Stop;
    end;
  Result := True;
end;

function AllStrings(const D: TDocument; First, N: Int32): Boolean;
var
  I: Int32;
begin
  for I := 0 to N - 1 do
    if DocKind(D, First + I) <> KindString then begin
      Result := False;
      Exit;
    end;
  Result := True;
end;

end.
