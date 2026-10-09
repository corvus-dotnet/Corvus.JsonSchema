unit Corvus.JsonSchema.Checked;

{$I corvus.inc}

{ Array reads for the paths every validation takes, each with its bounds check written out.

  Every index into an array is checked throughout the evaluator (corvus.inc turns range checking on, and nothing
  turns it off but the functions of this unit and the functions written the same way next to an array type of
  another unit: NameKeyAt, ChildAt, PlanAt and the like). Free Pascal makes that check with a call to a run-time
  routine for each read of a dynamic array, which on the paths every validation takes costs more than the read it
  guards: measured, the evaluator took about two and a half times as long with those calls as without any check.

  The functions here read an element after one comparison of the index with the array's length, written inline,
  and raise ERangeError exactly as the compiler's check does when the index is out of range. The guarantee is the
  same: no read outside an array. Only the reads inside these functions are compiled without the compiler's own
  check, and each of them follows the comparison that makes it safe.

  An index is a NativeInt, as wide as the length it is compared with, so the comparison is right for every value an
  index can have: a negative one, and one that does not fit in 32 bits, are both outside the array. The width is
  also what keeps the compiler's other check away from the reads. Under range checking Free Pascal checks every
  integer expression that is assigned to a narrower variable or handed to a narrower parameter, with a call for the
  failure, and a 32-bit index worked out as First + 2 * J is such an expression on a 64-bit processor. The indexes
  of the paths every validation takes (the parameters and variables of the evaluator's loops, the name tables, the
  pattern matchers and the document's readers) are therefore NativeInt as well, like the int of the Go source.
  Nothing is narrowed on the way to a read, so nothing is checked on the way, and the comparison in the function
  that reads decides. Where a NativeInt is 64 bits wide, an index worked out from a document's 32-bit numbers cannot
  wrap. Where it is 32 bits wide it could wrap only if a document's text or tape reached 2^31, which the parser
  refuses (MaxDocumentSize in Corvus.JsonSchema.Document). Range checking itself stays on everywhere outside the
  functions of this kind, for arithmetic as for reads.

  There are two kinds. An At function (ByteAt, WordAt, Load64) compares its index with the array's length and then
  reads. A Check function (CheckRun, CheckWords, CheckText) compares a whole run with the array's length once, and
  the Run function that goes with it (RunByteAt, RunWordAt, RunTextByteAt) reads within that run with no comparison
  of its own: it is only for a loop that follows the check in the same function. tests/TestChecked.pas calls every
  one of them with indexes outside an array, with indexes beyond 32 bits, and with indexes worked out from numbers
  whose sum is beyond 32 bits. }

interface

uses
  SysUtils;

type
  TUInt64Array = array of UInt64;

{ ByteAt is B[I]. }
function ByteAt(const B: TBytes; I: NativeInt): Byte; inline;
{ WordAt is A[I], and WordPtrAt a pointer to A[I], for writing the one word. }
function WordAt(const A: TUInt64Array; I: NativeInt): UInt64; inline;
function WordPtrAt(const A: TUInt64Array; I: NativeInt): PUInt64; inline;
{ Load64 is the eight bytes B[I .. I+7] as a little-endian word, and Load32 the four bytes B[I .. I+3]. }
function Load64(const B: TBytes; I: NativeInt): UInt64; inline;
function Load32(const B: TBytes; I: NativeInt): UInt32; inline;
{ TextByteAt is the byte S[I] of a string (whose first byte is S[1]). }
function TextByteAt(const S: UTF8String; I: NativeInt): Byte; inline;
{ CheckRun raises ERangeError unless B[Start .. Start+Len-1] lies within B (Len may be 0). After it, the bytes of
  the run may be read with RunByteAt, which makes no further check. }
procedure CheckRun(const B: TBytes; Start, Len: NativeInt); inline;
{ RunByteAt is B[I], for an index within a run that CheckRun has checked. }
function RunByteAt(const B: TBytes; I: NativeInt): Byte; inline;
{ CheckWords and RunWordAt are CheckRun and RunByteAt for the words A[Start .. Start+Len-1]. }
procedure CheckWords(const A: TUInt64Array; Start, Len: NativeInt); inline;
function RunWordAt(const A: TUInt64Array; I: NativeInt): UInt64; inline;
{ CheckText and RunTextByteAt are CheckRun and RunByteAt for the bytes S[Start .. Start+Len-1] of a string (whose
  first byte is S[1]). }
procedure CheckText(const S: UTF8String; Start, Len: NativeInt); inline;
function RunTextByteAt(const S: UTF8String; I: NativeInt): Byte; inline;

{ RangeFail raises ERangeError. }
procedure RangeFail;

implementation

procedure RangeFail;
begin
  raise ERangeError.Create('Range check error');
end;

{$PUSH}
{$R-}

function ByteAt(const B: TBytes; I: NativeInt): Byte; inline;
begin
  { One unsigned comparison covers both ends: a negative index is a large unsigned number. The index is a NativeInt,
    as wide as the length it is compared with, so no index of any size passes that is not within the array, and an
    expression given as the index is not narrowed on the way in. }
  if NativeUInt(I) >= NativeUInt(Length(B)) then
    RangeFail;
  Result := B[I];
end;

function WordAt(const A: TUInt64Array; I: NativeInt): UInt64; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := A[I];
end;

function WordPtrAt(const A: TUInt64Array; I: NativeInt): PUInt64; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function Load64(const B: TBytes; I: NativeInt): UInt64; inline;
begin
  { Written with a subtraction from the length, which cannot overflow, where a sum with the index could. }
  if (I < 0) or (I > Length(B) - 8) then
    RangeFail;
  Result := PUInt64(@B[I])^;
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

function Load32(const B: TBytes; I: NativeInt): UInt32; inline;
begin
  if (I < 0) or (I > Length(B) - 4) then
    RangeFail;
  Result := PUInt32(@B[I])^;
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

function TextByteAt(const S: UTF8String; I: NativeInt): Byte; inline;
begin
  { The index before the first byte is 0, which the subtraction makes a large unsigned number (and the lowest index
    there is wraps to the highest, which is beyond any string). }
  if NativeUInt(I - 1) >= NativeUInt(Length(S)) then
    RangeFail;
  Result := Byte(S[I]);
end;

procedure CheckRun(const B: TBytes; Start, Len: NativeInt); inline;
begin
  { Len is known to be within the length before it is subtracted from it, so nothing here can overflow. }
  if (Start < 0) or (Len < 0) or (Len > Length(B)) or (Start > Length(B) - Len) then
    RangeFail;
end;

function RunByteAt(const B: TBytes; I: NativeInt): Byte; inline;
begin
  Result := B[I];
end;

procedure CheckWords(const A: TUInt64Array; Start, Len: NativeInt); inline;
begin
  if (Start < 0) or (Len < 0) or (Len > Length(A)) or (Start > Length(A) - Len) then
    RangeFail;
end;

function RunWordAt(const A: TUInt64Array; I: NativeInt): UInt64; inline;
begin
  Result := A[I];
end;

procedure CheckText(const S: UTF8String; Start, Len: NativeInt); inline;
begin
  if (Start < 1) or (Len < 0) or (Len > Length(S)) or (Start - 1 > Length(S) - Len) then
    RangeFail;
end;

function RunTextByteAt(const S: UTF8String; I: NativeInt): Byte; inline;
begin
  Result := Byte(S[I]);
end;

{$POP}

end.
