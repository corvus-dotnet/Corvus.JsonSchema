unit Corvus.JsonSchema.EcmaRegex.Utf8;
{$I corvus.inc}

{ Reading and writing UTF-8 for the ECMA-262 regular expression engine. The Go source of the engine takes these
  functions from the unicode/utf8 package of the Go standard library. They are written out here with the same
  answers, because the answers of the engine on malformed text depend on them: a byte that does not start a
  well-formed sequence is read alone, as U+FFFD. A sequence is well formed when it is the shortest form of a code
  point that is not a surrogate and not beyond U+10FFFF. }

interface

uses
  SysUtils;

const
  { RuneError is what a malformed byte decodes to. }
  RuneError = $FFFD;
  { Code points below RuneSelf are one byte each. }
  RuneSelf = $80;

{ DecodeRune reads the code point that starts at the index At of P, of which the bytes before the index Limit are
  the text. It returns the code point and its width in bytes, which is 1 with U+FFFD for a malformed byte, and 0
  with U+FFFD when At is not before Limit. }
function DecodeRune(const P: TBytes; At, Limit: Int32; out Width: Int32): Int32;

{ DecodeLastRune reads the code point that ends just before the index Limit of P, looking no further back than the
  index Start. It returns the code point and its width in bytes, which is 1 with U+FFFD for a malformed byte, and 0
  with U+FFFD when Limit is not after Start. }
function DecodeLastRune(const P: TBytes; Start, Limit: Int32; out Width: Int32): Int32;

{ ValidRune reports whether a value is a code point that has a UTF-8 encoding: one in range that is not a
  surrogate. }
function ValidRune(R: Int32): Boolean;

{ AppendRune appends the UTF-8 encoding of a code point. A value that has none is written as U+FFFD. }
procedure AppendRune(var Dst: TBytes; R: Int32);

{ AppendRuneString is AppendRune for a string. }
procedure AppendRuneString(var Dst: UTF8String; R: Int32);

implementation

function DecodeRune(const P: TBytes; At, Limit: Int32; out Width: Int32): Int32;
var
  B0, B1, B2, B3, Lo, Hi: UInt8;
  Need: Int32;
begin
  if At >= Limit then begin
    Width := 0;
    Exit(RuneError);
  end;
  Width := 1;
  B0 := P[At];
  if B0 < RuneSelf then
    Exit(B0);
  // The second byte of a sequence has a range that depends on the first, which is what excludes the forms that
  // are longer than they need be, the surrogates, and the values beyond U+10FFFF.
  Lo := $80;
  Hi := $BF;
  if (B0 >= $C2) and (B0 <= $DF) then
    Need := 2
  else if (B0 >= $E0) and (B0 <= $EF) then begin
    Need := 3;
    if B0 = $E0 then
      Lo := $A0
    else if B0 = $ED then
      Hi := $9F;
  end else if (B0 >= $F0) and (B0 <= $F4) then begin
    Need := 4;
    if B0 = $F0 then
      Lo := $90
    else if B0 = $F4 then
      Hi := $8F;
  end else
    Exit(RuneError);
  if Limit - At < Need then
    Exit(RuneError);
  B1 := P[At + 1];
  if (B1 < Lo) or (B1 > Hi) then
    Exit(RuneError);
  if Need = 2 then begin
    Width := 2;
    Exit((Int32(B0 and $1F) shl 6) or Int32(B1 and $3F));
  end;
  B2 := P[At + 2];
  if (B2 < $80) or (B2 > $BF) then
    Exit(RuneError);
  if Need = 3 then begin
    Width := 3;
    Exit((Int32(B0 and $0F) shl 12) or (Int32(B1 and $3F) shl 6) or Int32(B2 and $3F));
  end;
  B3 := P[At + 3];
  if (B3 < $80) or (B3 > $BF) then
    Exit(RuneError);
  Width := 4;
  Result := (Int32(B0 and $07) shl 18) or (Int32(B1 and $3F) shl 12) or (Int32(B2 and $3F) shl 6) or
    Int32(B3 and $3F);
end;

function DecodeLastRune(const P: TBytes; Start, Limit: Int32; out Width: Int32): Int32;
var
  At, Lim: Int32;
begin
  if Limit <= Start then begin
    Width := 0;
    Exit(RuneError);
  end;
  At := Limit - 1;
  Result := P[At];
  if Result < RuneSelf then begin
    Width := 1;
    Exit;
  end;
  // Look back for the byte that starts the sequence, no further than the longest sequence goes.
  Lim := Limit - 4;
  if Lim < Start then
    Lim := Start;
  Dec(At);
  while At >= Lim do begin
    if (P[At] and $C0) <> $80 then
      Break;
    Dec(At);
  end;
  if At < Start then
    At := Start;
  Result := DecodeRune(P, At, Limit, Width);
  if At + Width <> Limit then begin
    Width := 1;
    Result := RuneError;
  end;
end;

function ValidRune(R: Int32): Boolean;
begin
  Result := ((R >= 0) and (R < $D800)) or ((R > $DFFF) and (R <= $10FFFF));
end;

type
  TEncoded = array[0..3] of UInt8;

{ Encode writes the encoding of a code point into B and returns its length. }
function Encode(R: Int32; out B: TEncoded): Int32;
begin
  if not ValidRune(R) then
    R := RuneError;
  if R < $80 then begin
    B[0] := UInt8(R);
    Exit(1);
  end;
  if R < $800 then begin
    B[0] := UInt8($C0 or (R shr 6));
    B[1] := UInt8($80 or (R and $3F));
    Exit(2);
  end;
  if R < $10000 then begin
    B[0] := UInt8($E0 or (R shr 12));
    B[1] := UInt8($80 or ((R shr 6) and $3F));
    B[2] := UInt8($80 or (R and $3F));
    Exit(3);
  end;
  B[0] := UInt8($F0 or (R shr 18));
  B[1] := UInt8($80 or ((R shr 12) and $3F));
  B[2] := UInt8($80 or ((R shr 6) and $3F));
  B[3] := UInt8($80 or (R and $3F));
  Result := 4;
end;

procedure AppendRune(var Dst: TBytes; R: Int32);
var
  B: TEncoded;
  N, At, I: Int32;
begin
  N := Encode(R, B);
  At := Length(Dst);
  SetLength(Dst, At + N);
  for I := 0 to N - 1 do
    Dst[At + I] := B[I];
end;

procedure AppendRuneString(var Dst: UTF8String; R: Int32);
var
  B: TEncoded;
  N, At, I: Int32;
begin
  N := Encode(R, B);
  At := Length(Dst);
  SetLength(Dst, At + N);
  for I := 0 to N - 1 do
    Dst[At + 1 + I] := AnsiChar(B[I]);
end;

end.