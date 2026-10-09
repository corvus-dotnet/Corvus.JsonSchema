unit Corvus.JsonSchema.Checked;

{$I corvus.inc}

{ Array reads for the paths every validation takes, each with its bounds check written out.

  Every index into an array is checked throughout the evaluator (corvus.inc turns range checking on, and nothing
  turns it off but this unit). Free Pascal makes that check with a call to a run-time routine for each read of a
  dynamic array, which on the paths every validation takes costs more than the read it guards: measured, the
  evaluator took about two and a half times as long with those calls as without any check.

  The functions here read an element after one comparison of the index with the array's length, written inline,
  and raise ERangeError exactly as the compiler's check does when the index is out of range. The guarantee is the
  same: no read outside an array. Only the reads inside these functions are compiled without the compiler's own
  check, and each of them follows the comparison that makes it safe. }

interface

uses
  SysUtils;

type
  TUInt64Array = array of UInt64;

{ ByteAt is B[I]. }
function ByteAt(const B: TBytes; I: Int32): Byte; inline;
{ WordAt is A[I]. }
function WordAt(const A: TUInt64Array; I: Int32): UInt64; inline;
{ Load64 is the eight bytes B[I .. I+7] as a little-endian word, and Load32 the four bytes B[I .. I+3]. }
function Load64(const B: TBytes; I: Int32): UInt64; inline;
function Load32(const B: TBytes; I: Int32): UInt32; inline;
{ CheckRun raises ERangeError unless B[Start .. Start+Len-1] lies within B (Len may be 0). After it, the bytes of
  the run may be read with RunByteAt, which makes no further check. }
procedure CheckRun(const B: TBytes; Start, Len: Int32); inline;
{ RunByteAt is B[I], for an index within a run that CheckRun has checked. }
function RunByteAt(const B: TBytes; I: Int32): Byte; inline;

{ RangeFail raises ERangeError. }
procedure RangeFail;

implementation

procedure RangeFail;
begin
  raise ERangeError.Create('Range check error');
end;

{$PUSH}
{$R-}

function ByteAt(const B: TBytes; I: Int32): Byte; inline;
begin
  { One unsigned comparison covers both ends: a negative index is a large unsigned number. }
  if UInt32(I) >= UInt32(Length(B)) then
    RangeFail;
  Result := B[I];
end;

function WordAt(const A: TUInt64Array; I: Int32): UInt64; inline;
begin
  if UInt32(I) >= UInt32(Length(A)) then
    RangeFail;
  Result := A[I];
end;

function Load64(const B: TBytes; I: Int32): UInt64; inline;
begin
  if (I < 0) or (Int64(I) + 8 > Length(B)) then
    RangeFail;
  Result := PUInt64(@B[I])^;
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

function Load32(const B: TBytes; I: Int32): UInt32; inline;
begin
  if (I < 0) or (Int64(I) + 4 > Length(B)) then
    RangeFail;
  Result := PUInt32(@B[I])^;
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

procedure CheckRun(const B: TBytes; Start, Len: Int32); inline;
begin
  if (Start < 0) or (Len < 0) or (Int64(Start) + Len > Length(B)) then
    RangeFail;
end;

function RunByteAt(const B: TBytes; I: Int32): Byte; inline;
begin
  Result := B[I];
end;

{$POP}

end.
