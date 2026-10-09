unit Corvus.JsonSchema.Names;

{$I corvus.inc}

{ Property name lookup for the fail-fast plans. A port of names.go of the Go module.

  A name to look up is the bytes Name[Start .. Start+Len-1] (a Go slice of bytes). A set too large for the table's
  indexes is a map from the name in the Go source. Here it is the names' indexes sorted by name, searched by
  halving, since the set is only read once it is built. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Checked,
  Corvus.JsonSchema.Document;

const
  NoName = UInt32($FFFFFFFF);

type
  TUTF8StringArray = array of UTF8String;
  TUInt16Array = array of UInt16;
  TInt32Array = array of Int32;

  { TNameKey is what decides whether a name is a given one: its length, its word, and for a name longer than eight
    bytes its last eight bytes. That is the whole name up to sixteen bytes, so only a longer name has its text
    compared. }
  TNameKey = record
    Word: UInt64;
    Tail: UInt64;
    Length: Int32;
  end;
  PNameKey = ^TNameKey;
  TNameKeyArray = array of TNameKey;

  { TNameMap finds a property name among a set of names, through a hash table from a name's key to its index, with
    open addressing. The table has at least four slots for each name and its hash is the one of a few that leaves
    the fewest names away from their first slot (for most sets, none), so a search is one multiplication and one
    read to the index, then the comparison of the key. }
  TNameMap = record
    Names: TUTF8StringArray;
    { The word of each name (see NameWord). }
    Words: TUInt64Array;
    Keys: TNameKeyArray;
    { The index + 1 of the name in each slot (0: empty). The length is a power of two. }
    Table: TUInt16Array;
    { The length of the table less one. }
    Mask: UInt64;
    { The multiplier of the hash. A slot is the high bits of the product, cut to the table's length. }
    Mul: UInt64;
    { A set too large for the table's indexes (which no schema in practice has) is searched in sorted order
      instead: the indexes of the names, sorted by name and then by index. }
    IsLarge: Boolean;
    Large: TUInt32Array;
  end;

  { TNames maps declared property names to their index. }
  TNames = record
    { Bit n set: some name has length n (lengths of 63 and more share bit 63). A name whose length is not in the
      set is not declared, which settles most misses without a search. }
    Lengths: UInt64;
    M: TNameMap;
    { For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
      first name in sorted order). NoName after the last. }
    SortedNext: TUInt32Array;
  end;

{ NameKeyAt and TextAt are pointers to A[I], and UInt16At is A[I]: one comparison of the index with the array's
  length, then the read (see Corvus.JsonSchema.Checked). }
function NameKeyAt(const A: TNameKeyArray; I: Int32): PNameKey; inline;
function TextAt(const A: TUTF8StringArray; I: Int32): PUTF8String; inline;
function UInt16At(const A: TUInt16Array; I: Int32): UInt16; inline;

{ NameWord is a word that tells names of one length apart cheaply. For a name of at most eight bytes it is unique
  among names of the same length: the first and last four bytes (overlapping, so every byte is in one of them), or
  for shorter names the first, middle and last byte. For a longer name it is the first eight bytes, so names with
  different words differ, and names with the same word are compared in full. }
function NameWord(const B: TBytes; Start, Len: Int32): UInt64;
{ TailWord is the last eight bytes of a name longer than eight bytes. }
function TailWord(const B: TBytes; Start, Len: Int32): UInt64; inline;

function NewNameMap(const Names: TUTF8StringArray): TNameMap;
{ NameMapHash is the hash of a key. }
function NameMapHash(const M: TNameMap; Word, Tail: UInt64; Length: Int32): UInt64; inline;
{ NameMapRest reports whether name I is the given name, which is longer than eight bytes, when their lengths and
  words are equal. }
function NameMapRest(const M: TNameMap; I: Int32; const Name: TBytes; Start, Len: Int32): Boolean;
{ NameMapEqual reports whether name I is the given name, whose word is W. }
function NameMapEqual(const M: TNameMap; I: Int32; const Name: TBytes; Start, Len: Int32; W: UInt64): Boolean;
{ NameMapFind is the index of a name (whose word is W), or -1. }
function NameMapFind(const M: TNameMap; const Name: TBytes; Start, Len: Int32; W: UInt64): Int32;
{ NameMapFindKey is the index of the name with a key, or -1, for a name of at most sixteen bytes (which its key
  decides). }
function NameMapFindKey(const M: TNameMap; W, Tail: UInt64; Length: Int32): Int32;
{ NameMapFindLong is NameMapFind for a name longer than sixteen bytes, whose text is compared as well, and for a
  large set. }
function NameMapFindLong(const M: TNameMap; const Name: TBytes; Start, Len: Int32; W: UInt64): Int32;

function LengthBit(Length: Int32): UInt64; inline;
function NewNames(const List: TUTF8StringArray): TNames;
function NamesLen(const Ns: TNames): Int32; inline;
{ NamesFind is the index of a name, without the ordering hint, or -1. }
function NamesFind(const Ns: TNames; const Name: TBytes; Start, Len: Int32): Int32;
{ NamesFindString is NamesFind for a name held as a string. }
function NamesFindString(const Ns: TNames; const Name: UTF8String): Int32;
{ NamesAt reports whether the name at a hint (the index after the previous match) has the given length and word.
  For a name of at most eight bytes that is the name. }
function NamesAt(const Ns: TNames; Hint, Length: Int32; W: UInt64): Boolean; inline;
{ NamesFindFrom finds a name, trying the one after the previous match first: instances tend to list their
  properties in the schema's order, so the next name is usually the next one declared. It returns the index (or -1)
  and sets Hint for the next call. }
function NamesFindFrom(const Ns: TNames; const Name: TBytes; Start, Len: Int32; var Hint: Int32): Int32;
{ NamesFindAfter is NamesFindFrom for a name (whose word is W) that is not the one at the hint. It tries the name
  after the previous match in sorted order (instances written by tools that sort their keys), then searches the
  table. }
function NamesFindAfter(const Ns: TNames; const Name: TBytes; Start, Len: Int32; W: UInt64; var Hint: Int32): Int32;
{ NamesFindAfterLong is NamesFindAfter for a name longer than sixteen bytes, whose text is compared, and for a
  large set. }
function NamesFindAfterLong(const Ns: TNames; const Name: TBytes; Start, Len: Int32; W: UInt64;
  var Hint: Int32): Int32;

{ TextEqualsBytes says whether a string is the bytes B[Start .. Start+Len-1]. }
function TextEqualsBytes(const S: UTF8String; const B: TBytes; Start, Len: Int32): Boolean;
{ CompareUtf8 compares two strings byte by byte: negative, zero or positive. }
function CompareUtf8(const A, B: UTF8String): Int32;

implementation

const
  { The first multiplier tried for a map's hash, and the number tried. }
  NameHashMultiplier = UInt64($9E3779B97F4A7C15);
  NameHashMultipliers = 24;
  { The high bits of the hash that give a slot. }
  NameHashShift = 64 - 18;
  { More names than this are searched in sorted order (the table has four to eight slots for each name). }
  MaxTableNames = 1 shl 15;

{$PUSH}
{$R-}

function NameKeyAt(const A: TNameKeyArray; I: Int32): PNameKey; inline;
begin
  if UInt32(I) >= UInt32(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function TextAt(const A: TUTF8StringArray; I: Int32): PUTF8String; inline;
begin
  if UInt32(I) >= UInt32(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function UInt16At(const A: TUInt16Array; I: Int32): UInt16; inline;
begin
  if UInt32(I) >= UInt32(Length(A)) then
    RangeFail;
  Result := A[I];
end;

{$POP}

function TextEqualsBytes(const S: UTF8String; const B: TBytes; Start, Len: Int32): Boolean;
var
  K: Int32;
begin
  Result := False;
  if Length(S) <> Len then
    Exit;
  { Both runs are checked once: the reads below are all within them. }
  CheckText(S, 1, Len);
  CheckRun(B, Start, Len);
  for K := 0 to Len - 1 do
    if RunTextByteAt(S, K + 1) <> RunByteAt(B, Start + K) then
      Exit;
  Result := True;
end;

function CompareUtf8(const A, B: UTF8String): Int32;
var
  K, N: Int32;
begin
  N := Length(A);
  if Length(B) < N then
    N := Length(B);
  for K := 1 to N do
    if A[K] <> B[K] then begin
      Result := Int32(Byte(A[K])) - Int32(Byte(B[K]));
      Exit;
    end;
  Result := Length(A) - Length(B);
end;

{ CompareBytesText compares the bytes B[Start .. Start+Len-1] with a string: negative, zero or positive. }
function CompareBytesText(const B: TBytes; Start, Len: Int32; const S: UTF8String): Int32;
var
  K, N: Int32;
begin
  N := Len;
  if Length(S) < N then
    N := Length(S);
  for K := 0 to N - 1 do
    if B[Start + K] <> Byte(S[K + 1]) then begin
      Result := Int32(B[Start + K]) - Int32(Byte(S[K + 1]));
      Exit;
    end;
  Result := Len - Length(S);
end;

function NameWord(const B: TBytes; Start, Len: Int32): UInt64;
begin
  if Len > 8 then
    Result := LoadLE64(B, Start)
  else if Len >= 4 then
    Result := UInt64(LoadLE32(B, Start)) or (UInt64(LoadLE32(B, Start + Len - 4)) shl 32)
  else if Len > 0 then
    Result := UInt64(ByteAt(B, Start)) or (UInt64(ByteAt(B, Start + Len div 2)) shl 8)
      or (UInt64(ByteAt(B, Start + Len - 1)) shl 16)
  else
    Result := 0;
end;

function TailWord(const B: TBytes; Start, Len: Int32): UInt64; inline;
begin
  Result := LoadLE64(B, Start + Len - 8);
end;

function NameMapHash(const M: TNameMap; Word, Tail: UInt64; Length: Int32): UInt64; inline;
begin
  { The sum and the product wrap, as a hash's do. }
  Result := ((Word xor ((Tail shl 29) or (Tail shr 35))) + UInt64(Length)) * M.Mul;
end;

{ Fill puts the names in a table under the map's hash, each in the first free slot from its own. It returns how
  many are not in their own slot. }
function Fill(const M: TNameMap; var Table: TUInt16Array): Int32;
var
  Mask, Home, Slot: UInt64;
  I: Int32;
  Same: Boolean;
begin
  Mask := UInt64(Length(Table) - 1);
  Result := 0;
  for I := 0 to Length(M.Keys) - 1 do begin
    Home := NameMapHash(M, M.Keys[I].Word, M.Keys[I].Tail, M.Keys[I].Length) shr NameHashShift;
    Slot := Home;
    Same := False;
    while Table[Slot and Mask] <> 0 do begin
      { The first of equal names keeps the slot (a set built from a schema has no equal names). }
      if M.Names[Table[Slot and Mask] - 1] = M.Names[I] then begin
        Same := True;
        Break;
      end;
      Inc(Slot);
    end;
    if Same then
      Continue;
    Table[Slot and Mask] := UInt16(I + 1);
    if Slot <> Home then
      Inc(Result);
  end;
end;

procedure ClearTable(var Table: TUInt16Array);
var
  I: Int32;
begin
  for I := 0 to Length(Table) - 1 do
    Table[I] := 0;
end;

{ SortIndexes sorts the indexes Order[Lo .. Hi] by the names they refer to, and names that are the same by index
  (a merge sort, so that it is stable and takes no more than n log n steps whatever the names). }
procedure SortIndexes(const List: TUTF8StringArray; var Order, Scratch: TUInt32Array; Lo, Hi: Int32);
var
  Mid, I, J, K: Int32;
begin
  if Hi - Lo < 1 then
    Exit;
  Mid := Lo + (Hi - Lo) div 2;
  SortIndexes(List, Order, Scratch, Lo, Mid);
  SortIndexes(List, Order, Scratch, Mid + 1, Hi);
  I := Lo;
  J := Mid + 1;
  K := Lo;
  while (I <= Mid) and (J <= Hi) do begin
    if CompareUtf8(List[Order[J]], List[Order[I]]) < 0 then begin
      Scratch[K] := Order[J];
      Inc(J);
    end else begin
      Scratch[K] := Order[I];
      Inc(I);
    end;
    Inc(K);
  end;
  while I <= Mid do begin
    Scratch[K] := Order[I];
    Inc(I);
    Inc(K);
  end;
  while J <= Hi do begin
    Scratch[K] := Order[J];
    Inc(J);
    Inc(K);
  end;
  for K := Lo to Hi do
    Order[K] := Scratch[K];
end;

function SortedOrder(const List: TUTF8StringArray): TUInt32Array;
var
  Scratch: TUInt32Array;
  I: Int32;
begin
  Result := nil;
  Scratch := nil;
  SetLength(Result, Length(List));
  SetLength(Scratch, Length(List));
  for I := 0 to Length(List) - 1 do
    Result[I] := UInt32(I);
  SortIndexes(List, Result, Scratch, 0, Length(List) - 1);
end;

function NewNameMap(const Names: TUTF8StringArray): TNameMap;
var
  I, N, Size, Attempt, Moved, BestMoved: Int32;
  B: TBytes;
  Best, Mul: UInt64;
begin
  Result.Names := Names;
  Result.Words := nil;
  Result.Keys := nil;
  Result.Table := nil;
  Result.Mask := 0;
  Result.Mul := 0;
  Result.IsLarge := False;
  Result.Large := nil;
  N := Length(Names);
  SetLength(Result.Words, N);
  SetLength(Result.Keys, N);
  for I := 0 to N - 1 do begin
    B := BytesOf(Names[I]);
    Result.Keys[I].Word := NameWord(B, 0, Length(B));
    Result.Keys[I].Length := Length(B);
    Result.Keys[I].Tail := 0;
    if Length(B) > 8 then
      Result.Keys[I].Tail := TailWord(B, 0, Length(B));
    Result.Words[I] := Result.Keys[I].Word;
  end;
  if N > MaxTableNames then begin
    Result.IsLarge := True;
    Result.Large := SortedOrder(Names);
    Exit;
  end;
  Size := 4;
  while Size < 4 * N do
    Size := Size shl 1;
  { The multiplier that leaves the fewest names away from their first slot. }
  Best := NameHashMultiplier;
  BestMoved := -1;
  SetLength(Result.Table, Size);
  Mul := NameHashMultiplier;
  for Attempt := 1 to NameHashMultipliers do begin
    ClearTable(Result.Table);
    Result.Mul := Mul;
    Moved := Fill(Result, Result.Table);
    if (BestMoved < 0) or (Moved < BestMoved) then begin
      Best := Mul;
      BestMoved := Moved;
      if Moved = 0 then
        Break;
    end;
    { The next odd multiplier (a step of an xorshift generator). }
    Mul := Mul xor (Mul shl 13);
    Mul := Mul xor (Mul shr 7);
    Mul := Mul xor (Mul shl 17);
    Mul := Mul or 1;
  end;
  if Result.Mul <> Best then begin
    ClearTable(Result.Table);
    Result.Mul := Best;
    Fill(Result, Result.Table);
  end;
  Result.Mask := UInt64(Size - 1);
end;

function NameMapRest(const M: TNameMap; I: Int32; const Name: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (TailWord(Name, Start, Len) = NameKeyAt(M.Keys, I)^.Tail)
    and ((Len <= 16) or TextEqualsBytes(TextAt(M.Names, I)^, Name, Start, Len));
end;

function NameMapEqual(const M: TNameMap; I: Int32; const Name: TBytes; Start, Len: Int32; W: UInt64): Boolean;
var
  Key: PNameKey;
begin
  Key := NameKeyAt(M.Keys, I);
  Result := (Key^.Length = Len) and (Key^.Word = W) and ((Len <= 8) or NameMapRest(M, I, Name, Start, Len));
end;

function NameMapFind(const M: TNameMap; const Name: TBytes; Start, Len: Int32; W: UInt64): Int32;
var
  Tail: UInt64;
begin
  if (Len > 16) or M.IsLarge then begin
    Result := NameMapFindLong(M, Name, Start, Len, W);
    Exit;
  end;
  Tail := 0;
  if Len > 8 then
    Tail := TailWord(Name, Start, Len);
  Result := NameMapFindKey(M, W, Tail, Len);
end;

function NameMapFindKey(const M: TNameMap; W, Tail: UInt64; Length: Int32): Int32;
var
  Slot: UInt64;
  At: Int32;
  Key: PNameKey;
begin
  Slot := NameMapHash(M, W, Tail, Length) shr NameHashShift;
  while True do begin
    At := UInt16At(M.Table, Int32(Slot and M.Mask));
    if At = 0 then begin
      Result := -1;
      Exit;
    end;
    Key := NameKeyAt(M.Keys, At - 1);
    if (Key^.Word = W) and (Key^.Tail = Tail) and (Key^.Length = Length) then begin
      Result := At - 1;
      Exit;
    end;
    Inc(Slot);
  end;
end;

function NameMapFindLong(const M: TNameMap; const Name: TBytes; Start, Len: Int32; W: UInt64): Int32;
var
  Tail, Slot: UInt64;
  At, Lo, Hi, Mid: Int32;
  Key: PNameKey;
begin
  if M.IsLarge then begin
    { The first of the names that are not below the given one, which is the one of the lowest index among equal
      names. }
    Lo := 0;
    Hi := Length(M.Large);
    while Lo < Hi do begin
      Mid := Lo + (Hi - Lo) div 2;
      if CompareBytesText(Name, Start, Len, M.Names[M.Large[Mid]]) > 0 then
        Lo := Mid + 1
      else
        Hi := Mid;
    end;
    if (Lo < Length(M.Large)) and TextEqualsBytes(M.Names[M.Large[Lo]], Name, Start, Len) then
      Result := Int32(M.Large[Lo])
    else
      Result := -1;
    Exit;
  end;
  Tail := 0;
  if Len > 8 then
    Tail := TailWord(Name, Start, Len);
  Slot := NameMapHash(M, W, Tail, Len) shr NameHashShift;
  while True do begin
    At := UInt16At(M.Table, Int32(Slot and M.Mask));
    if At = 0 then begin
      Result := -1;
      Exit;
    end;
    Key := NameKeyAt(M.Keys, At - 1);
    if (Key^.Word = W) and (Key^.Tail = Tail) and (Key^.Length = Len)
      and TextEqualsBytes(TextAt(M.Names, At - 1)^, Name, Start, Len) then begin
      Result := At - 1;
      Exit;
    end;
    Inc(Slot);
  end;
end;

function LengthBit(Length: Int32): UInt64; inline;
begin
  if Length > 63 then
    Length := 63;
  Result := UInt64(1) shl Length;
end;

function NewNames(const List: TUTF8StringArray): TNames;
var
  Order: TUInt32Array;
  I: Int32;
begin
  Result.Lengths := 0;
  Result.M := NewNameMap(List);
  for I := 0 to Length(List) - 1 do
    Result.Lengths := Result.Lengths or LengthBit(Length(List[I]));
  Order := SortedOrder(List);
  Result.SortedNext := nil;
  SetLength(Result.SortedNext, Length(List) + 1);
  for I := 0 to Length(List) do
    Result.SortedNext[I] := NoName;
  for I := 0 to Length(Order) - 1 do
    if I = 0 then
      Result.SortedNext[0] := Order[I]
    else
      Result.SortedNext[Order[I - 1] + 1] := Order[I];
end;

function NamesLen(const Ns: TNames): Int32; inline;
begin
  Result := Length(Ns.M.Names);
end;

function NamesFind(const Ns: TNames; const Name: TBytes; Start, Len: Int32): Int32;
var
  W, Tail: UInt64;
begin
  if Ns.Lengths and LengthBit(Len) = 0 then begin
    Result := -1;
    Exit;
  end;
  W := NameWord(Name, Start, Len);
  if (Len > 16) or Ns.M.IsLarge then begin
    Result := NameMapFindLong(Ns.M, Name, Start, Len, W);
    Exit;
  end;
  Tail := 0;
  if Len > 8 then
    Tail := TailWord(Name, Start, Len);
  { The Go source writes the search of the table out here, to save a call. Free Pascal does not inline a function
    with a loop, and the search is the same. }
  Result := NameMapFindKey(Ns.M, W, Tail, Len);
end;

function NamesFindString(const Ns: TNames; const Name: UTF8String): Int32;
var
  B: TBytes;
begin
  B := BytesOf(Name);
  Result := NamesFind(Ns, B, 0, Length(B));
end;

function NamesAt(const Ns: TNames; Hint, Length: Int32; W: UInt64): Boolean; inline;
var
  Key: PNameKey;
begin
  Result := False;
  if (Hint >= 0) and (Hint < System.Length(Ns.M.Keys)) then begin
    Key := NameKeyAt(Ns.M.Keys, Hint);
    Result := (Key^.Length = Length) and (Key^.Word = W);
  end;
end;

function NamesFindFrom(const Ns: TNames; const Name: TBytes; Start, Len: Int32; var Hint: Int32): Int32;
var
  W: UInt64;
begin
  W := NameWord(Name, Start, Len);
  if NamesAt(Ns, Hint, Len, W) and ((Len <= 8) or NameMapRest(Ns.M, Hint, Name, Start, Len)) then begin
    Result := Hint;
    Inc(Hint);
    Exit;
  end;
  Result := NamesFindAfter(Ns, Name, Start, Len, W, Hint);
end;

function NamesFindAfter(const Ns: TNames; const Name: TBytes; Start, Len: Int32; W: UInt64; var Hint: Int32): Int32;
var
  Tail, Slot: UInt64;
  Next: UInt32;
  At: Int32;
  Key: PNameKey;
begin
  if Ns.Lengths and LengthBit(Len) = 0 then begin
    Result := -1;
    Exit;
  end;
  if (Len > 16) or Ns.M.IsLarge then begin
    Result := NamesFindAfterLong(Ns, Name, Start, Len, W, Hint);
    Exit;
  end;
  Tail := 0;
  if Len > 8 then
    Tail := TailWord(Name, Start, Len);
  if (Hint >= 0) and (Hint < Length(Ns.SortedNext)) then begin
    Next := UInt32At(Ns.SortedNext, Hint);
    if Next <> NoName then begin
      Key := NameKeyAt(Ns.M.Keys, Int32(Next));
      if (Key^.Word = W) and (Key^.Tail = Tail) and (Key^.Length = Len) then begin
        Result := Int32(Next);
        Hint := Result + 1;
        Exit;
      end;
    end;
  end;
  Slot := NameMapHash(Ns.M, W, Tail, Len) shr NameHashShift;
  while True do begin
    At := UInt16At(Ns.M.Table, Int32(Slot and Ns.M.Mask));
    if At = 0 then begin
      Result := -1;
      Exit;
    end;
    Key := NameKeyAt(Ns.M.Keys, At - 1);
    if (Key^.Word = W) and (Key^.Tail = Tail) and (Key^.Length = Len) then begin
      Result := At - 1;
      Hint := At;
      Exit;
    end;
    Inc(Slot);
  end;
end;

function NamesFindAfterLong(const Ns: TNames; const Name: TBytes; Start, Len: Int32; W: UInt64;
  var Hint: Int32): Int32;
var
  Next: UInt32;
begin
  if (Hint >= 0) and (Hint < Length(Ns.SortedNext)) then begin
    Next := UInt32At(Ns.SortedNext, Hint);
    if (Next <> NoName) and NameMapEqual(Ns.M, Int32(Next), Name, Start, Len, W) then begin
      Result := Int32(Next);
      Hint := Result + 1;
      Exit;
    end;
  end;
  Result := NameMapFindLong(Ns.M, Name, Start, Len, W);
  if Result >= 0 then
    Hint := Result + 1;
end;

end.
