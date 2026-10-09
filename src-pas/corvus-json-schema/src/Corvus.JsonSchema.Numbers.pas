unit Corvus.JsonSchema.Numbers;

{$I corvus.inc}

{ Numbers: the correctly rounded conversion of a JSON number's text to a Double, and exact numeric comparisons over
  the parser's number representations (an Int64, a UInt64 beyond an Int64, or a Double). Integers stay exact and
  floats compare by value, so 1 = 1.0 and 9007199254740993 > 9007199254740992.0. multipleOf is decided exactly on
  decimal forms, so 0.0075 is a multiple of 0.0001.

  A port of numbers.go of the Go module. Go's standard library converts decimal text to a float64 and has big
  numbers. The Pascal run-time libraries have neither in a form that is the same in Free Pascal and Delphi, so this
  unit has its own: the Eisel-Lemire algorithm as the Java port wrote it (FastDouble.java), and a small unsigned big
  integer for the cases those algorithms leave. }

interface

uses
  SysUtils;

type
  { An unsigned big integer: 32-bit limbs, least significant first. Only what this unit needs of one. }
  TBig = record
    Limbs: array of UInt32;
    Len: Int32;
  end;

  { TDivisor is a multipleOf divisor, as the decimal its JSON text writes: a significand without trailing zeros and
    an exponent. x is a multiple when x / divisor is an integer, decided exactly on the decimal digits of x's own
    text, with integer arithmetic only: the C# evaluator's decimal semantics. }
  TDivisor = record
    IsInt: Boolean;
    Value: Int64;
    { The significand (at most 18 digits), or -1 when the divisor's digits do not fit. }
    Significand: Int64;
    { The exponent of the significand, whichever of the two holds it. }
    Exponent: Int64;
    { The significand of a divisor of more than 18 significant digits. }
    Big: TBig;
  end;

const
  { The number representations, as Corvus.JsonSchema.Document declares them. }
  NumFlagInt = 0;
  NumFlagUint = 1;
  NumFlagFloat = 2;

function DoubleToBits(D: Double): UInt64; inline;
function DoubleFromBits(V: UInt64): Double; inline;
{ UInt64ToDouble is the Double nearest an unsigned 64-bit integer. }
function UInt64ToDouble(V: UInt64): Double;
{ FloorOf is the largest whole Double not above D. }
function FloorOf(D: Double): Double;
function IsInfinite(D: Double): Boolean; inline;

{ DecimalToDouble is the Double nearest the JSON number text in B[Start .. Stop-1] (already validated), correctly
  rounded. False when the number is beyond the range of a Double. }
function DecimalToDouble(const B: TBytes; Start, Stop: Int32; out D: Double): Boolean;

{ CompareNumbers compares two numbers exactly: negative, zero or positive. }
function CompareNumbers(FA: Byte; A: UInt64; FB: Byte; B: UInt64): Int32;
{ IsIntegerNumber reports whether a number is an integer (what the integer type accepts). }
function IsIntegerNumber(Flag: Byte; V: UInt64): Boolean;

{ NewDivisor is the divisor a number with these flags and data is, its text starting at B[Start]. }
function NewDivisor(Flags: Byte; Data: UInt64; const B: TBytes; Start: Int32): TDivisor;
{ DivisorDivides is the exact multipleOf of a number with these flags and data, its text starting at B[Start]. }
function DivisorDivides(const V: TDivisor; Flags: Byte; Data: UInt64; const B: TBytes; Start: Int32): Boolean;

implementation

{$I Corvus.JsonSchema.PowersOfFive.inc}

const
  Two63: Double = 9223372036854775808.0;
  Two64: Double = 18446744073709551616.0;

  SmallestPowerOfTen = -342;
  LargestPowerOfTen = 308;
  MantissaExplicitBits = 52;
  MinimumExponent = -1023;
  InfinitePower = $7FF;
  MinExponentRoundToEven = -4;
  MaxExponentRoundToEven = 23;

  InfinityBits = UInt64($7FF0000000000000);

{ --------------------------------------------------------------------------------------------------------------------
  Bits }

function DoubleToBits(D: Double): UInt64; inline;
begin
  Move(D, Result, 8);
end;

function DoubleFromBits(V: UInt64): Double; inline;
begin
  Move(V, Result, 8);
end;

function IsInfinite(D: Double): Boolean; inline;
begin
  Result := DoubleToBits(D) and UInt64($7FFFFFFFFFFFFFFF) = InfinityBits;
end;

function UInt64ToDouble(V: UInt64): Double;
begin
  if V shr 63 = 0 then
    Result := Int64(V)
  else begin
    { Halved, with the bit that falls off kept as a sticky bit, so that doubling gives the correctly rounded value. }
    Result := Int64((V shr 1) or (V and 1));
    Result := Result * 2.0;
  end;
end;

function FloorOf(D: Double): Double;
begin
  { Beyond 2^52 every Double is whole, and Int would be asked for more than it need be. }
  if (D >= 4503599627370496.0) or (D <= -4503599627370496.0) then begin
    Result := D;
    Exit;
  end;
  Result := Int(D);
  if Result > D then
    Result := Result - 1.0;
end;

{ MulHigh is the high 64 bits of the unsigned 128-bit product of A and B. }
function MulHigh(A, B: UInt64): UInt64;
var
  A0, A1, B0, B1, P00, P01, P10, P11, Mid: UInt64;
begin
  A0 := A and $FFFFFFFF;
  A1 := A shr 32;
  B0 := B and $FFFFFFFF;
  B1 := B shr 32;
  P00 := A0 * B0;
  P01 := A0 * B1;
  P10 := A1 * B0;
  P11 := A1 * B1;
  Mid := (P00 shr 32) + (P01 and $FFFFFFFF) + (P10 and $FFFFFFFF);
  Result := P11 + (P01 shr 32) + (P10 shr 32) + (Mid shr 32);
end;

function LeadingZeros(W: UInt64): Int32;
begin
  Result := 0;
  if W = 0 then begin
    Result := 64;
    Exit;
  end;
  if W shr 32 = 0 then begin
    Inc(Result, 32);
    W := W shl 32;
  end;
  if W shr 48 = 0 then begin
    Inc(Result, 16);
    W := W shl 16;
  end;
  if W shr 56 = 0 then begin
    Inc(Result, 8);
    W := W shl 8;
  end;
  if W shr 60 = 0 then begin
    Inc(Result, 4);
    W := W shl 4;
  end;
  if W shr 62 = 0 then begin
    Inc(Result, 2);
    W := W shl 2;
  end;
  if W shr 63 = 0 then
    Inc(Result);
end;

{ --------------------------------------------------------------------------------------------------------------------
  The unsigned big integer }

procedure BigSetSmall(var A: TBig; V: UInt32);
begin
  if Length(A.Limbs) < 4 then
    SetLength(A.Limbs, 4);
  A.Limbs[0] := V;
  if V = 0 then
    A.Len := 0
  else
    A.Len := 1;
end;

procedure BigPushLimb(var A: TBig; V: UInt32);
begin
  if A.Len = Length(A.Limbs) then
    SetLength(A.Limbs, A.Len * 2 + 4);
  A.Limbs[A.Len] := V;
  Inc(A.Len);
end;

{ A := A * M + Add. }
procedure BigMulAdd(var A: TBig; M, Add: UInt32);
var
  K: Int32;
  Carry, T: UInt64;
begin
  Carry := Add;
  for K := 0 to A.Len - 1 do begin
    T := UInt64(A.Limbs[K]) * M + Carry;
    A.Limbs[K] := UInt32(T and $FFFFFFFF);
    Carry := T shr 32;
  end;
  if Carry <> 0 then
    BigPushLimb(A, UInt32(Carry));
end;

{ A := A * 10^N. }
procedure BigMulPow10(var A: TBig; N: Int64);
begin
  while N >= 9 do begin
    BigMulAdd(A, 1000000000, 0);
    Dec(N, 9);
  end;
  while N > 0 do begin
    BigMulAdd(A, 10, 0);
    Dec(N);
  end;
end;

{ A := A * 2^N. }
procedure BigShiftLeft(var A: TBig; N: Int64);
begin
  while N >= 31 do begin
    BigMulAdd(A, UInt32($80000000), 0);
    Dec(N, 31);
  end;
  if N > 0 then
    BigMulAdd(A, UInt32(1) shl N, 0);
end;

{ A := A div M, returning the remainder. }
function BigDivSmall(var A: TBig; M: UInt32): UInt32;
var
  K: Int32;
  R, T: UInt64;
begin
  R := 0;
  for K := A.Len - 1 downto 0 do begin
    T := (R shl 32) or A.Limbs[K];
    A.Limbs[K] := UInt32(T div M);
    R := T mod M;
  end;
  while (A.Len > 0) and (A.Limbs[A.Len - 1] = 0) do
    Dec(A.Len);
  Result := UInt32(R);
end;

{ The remainder of A by M, leaving A as it is. }
function BigModSmall(const A: TBig; M: UInt32): UInt32;
var
  K: Int32;
  R: UInt64;
begin
  R := 0;
  for K := A.Len - 1 downto 0 do
    R := ((R shl 32) or A.Limbs[K]) mod M;
  Result := UInt32(R);
end;

function BigCompare(const A, B: TBig): Int32;
var
  K: Int32;
begin
  if A.Len <> B.Len then begin
    if A.Len < B.Len then
      Result := -1
    else
      Result := 1;
    Exit;
  end;
  for K := A.Len - 1 downto 0 do
    if A.Limbs[K] <> B.Limbs[K] then begin
      if A.Limbs[K] < B.Limbs[K] then
        Result := -1
      else
        Result := 1;
      Exit;
    end;
  Result := 0;
end;

{ A := A - B, for A at least B. }
procedure BigSub(var A: TBig; const B: TBig);
var
  K: Int32;
  Borrow, T, Other: Int64;
begin
  Borrow := 0;
  for K := 0 to A.Len - 1 do begin
    Other := 0;
    if K < B.Len then
      Other := B.Limbs[K];
    T := Int64(A.Limbs[K]) - Other - Borrow;
    if T < 0 then begin
      T := T + Int64($100000000);
      Borrow := 1;
    end else
      Borrow := 0;
    A.Limbs[K] := UInt32(T);
  end;
  while (A.Len > 0) and (A.Limbs[A.Len - 1] = 0) do
    Dec(A.Len);
end;

procedure BigCopy(const A: TBig; var B: TBig);
begin
  B.Limbs := Copy(A.Limbs, 0, Length(A.Limbs));
  B.Len := A.Len;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Decimal to Double: the Eisel-Lemire algorithm (Daniel Lemire, "Number Parsing at a Gigabyte per Second", and the
  fast_float library), which is exact for a decimal significand of up to 19 digits (Noble Mushtak and Daniel Lemire,
  "Fast Number Parsing Without Fallback"). A longer significand is truncated and the result accepted when the
  truncated and the next value round to the same Double. Otherwise the digits are compared, as big integers, with
  the point halfway between the two. }

{ FloorShift16 is X divided by 65536, rounded down (an arithmetic shift, which Pascal's shr is not for a negative
  number). }
function FloorShift16(X: Int64): Int64; inline;
begin
  if X >= 0 then
    Result := X shr 16
  else
    Result := -((-X + 65535) shr 16);
end;

{ EiselLemire is the bits of the Double nearest W * 10^Q (W an unsigned significand). }
function EiselLemire(W: UInt64; Q: Int32): UInt64;
var
  Lz, Index, UpperBit, Shift, Power2: Int32;
  High, Low, SecondHigh, PrecisionMask, Mantissa: UInt64;
begin
  if (W = 0) or (Q < SmallestPowerOfTen) then begin
    Result := 0;
    Exit;
  end;
  if Q > LargestPowerOfTen then begin
    Result := InfinityBits;
    Exit;
  end;
  Lz := LeadingZeros(W);
  W := W shl Lz;
  Index := 2 * (Q - SmallestPowerOfTen);
  High := MulHigh(W, PowersOfFive[Index]);
  { The low half of the product: the multiplication wraps on purpose. }
  Low := W * PowersOfFive[Index];
  PrecisionMask := UInt64($FFFFFFFFFFFFFFFF) shr (MantissaExplicitBits + 3);
  if High and PrecisionMask = PrecisionMask then begin
    SecondHigh := MulHigh(W, PowersOfFive[Index + 1]);
    Low := Low + SecondHigh;
    if SecondHigh > Low then
      Inc(High);
  end;
  UpperBit := Int32(High shr 63);
  Shift := UpperBit + 64 - MantissaExplicitBits - 3;
  Mantissa := High shr Shift;
  Power2 := Int32(FloorShift16((152170 + 65536) * Int64(Q)) + 63) + UpperBit - Lz - MinimumExponent;
  if Power2 <= 0 then begin
    { Subnormal. }
    if -Power2 + 1 >= 64 then begin
      Result := 0;
      Exit;
    end;
    Mantissa := Mantissa shr (-Power2 + 1);
    Mantissa := Mantissa + (Mantissa and 1);
    Mantissa := Mantissa shr 1;
    if Mantissa < UInt64(1) shl MantissaExplicitBits then
      Power2 := 0
    else
      Power2 := 1;
    Result := (UInt64(Power2) shl MantissaExplicitBits) or (Mantissa and ((UInt64(1) shl MantissaExplicitBits) - 1));
    Exit;
  end;
  { A product exactly halfway between two doubles rounds to even. }
  if (Low <= 1) and (Q >= MinExponentRoundToEven) and (Q <= MaxExponentRoundToEven) and (Mantissa and 3 = 1) then
    if Mantissa shl Shift = High then
      Mantissa := Mantissa and not UInt64(1);
  Mantissa := Mantissa + (Mantissa and 1);
  Mantissa := Mantissa shr 1;
  if Mantissa >= UInt64(2) shl MantissaExplicitBits then begin
    Mantissa := UInt64(1) shl MantissaExplicitBits;
    Inc(Power2);
  end;
  Mantissa := Mantissa and not (UInt64(1) shl MantissaExplicitBits);
  if Power2 >= InfinitePower then begin
    Result := InfinityBits;
    Exit;
  end;
  Result := (UInt64(Power2) shl MantissaExplicitBits) or Mantissa;
end;

{ ReadExponent reads the exponent part of a number's text at J, if there is one, as far as Stop. }
function ReadExponent(const B: TBytes; J, Stop: Int32): Int64;
var
  Negative: Boolean;
  E: Int64;
begin
  Result := 0;
  if (J >= Stop) or ((B[J] <> Ord('e')) and (B[J] <> Ord('E'))) then
    Exit;
  Inc(J);
  Negative := False;
  if (J < Stop) and ((B[J] = Ord('+')) or (B[J] = Ord('-'))) then begin
    Negative := B[J] = Ord('-');
    Inc(J);
  end;
  E := 0;
  while (J < Stop) and (B[J] >= Ord('0')) and (B[J] <= Ord('9')) do begin
    if E < 1000000000 then
      E := E * 10 + (B[J] - Ord('0'));
    Inc(J);
  end;
  if Negative then
    E := -E;
  Result := E;
end;

{ Above says whether the positive decimal of the digits of B[DigitsStart .. Stop-1] is above (1), at (0) or below
  (-1) the point halfway between the Double with the bits Low and the next Double up. }
function CompareWithHalfway(const B: TBytes; DigitsStart, Stop: Int32; Low: UInt64): Int32;
var
  Digits, Half: TBig;
  J: Int32;
  C: Byte;
  Exponent, BinaryExponent: Int64;
  Fraction: Boolean;
  Mantissa: UInt64;
begin
  { The decimal: its digits as an integer, and its power of ten. }
  Digits.Limbs := nil;
  Digits.Len := 0;
  BigSetSmall(Digits, 0);
  Exponent := 0;
  Fraction := False;
  J := DigitsStart;
  while J < Stop do begin
    C := B[J];
    if C = Ord('.') then
      Fraction := True
    else if (C >= Ord('0')) and (C <= Ord('9')) then begin
      BigMulAdd(Digits, 10, C - Ord('0'));
      if Fraction then
        Dec(Exponent);
    end else
      Break;
    Inc(J);
  end;
  Inc(Exponent, ReadExponent(B, J, Stop));
  { The halfway point: (2 * mantissa + 1) * 2^(exponent - 1), where the Double is mantissa * 2^exponent. }
  Mantissa := Low and ((UInt64(1) shl MantissaExplicitBits) - 1);
  BinaryExponent := Int64((Low shr MantissaExplicitBits) and $7FF);
  if BinaryExponent = 0 then
    BinaryExponent := 1
  else
    Mantissa := Mantissa or (UInt64(1) shl MantissaExplicitBits);
  BinaryExponent := BinaryExponent - 1075 - 1;
  Mantissa := Mantissa * 2 + 1;
  Half.Limbs := nil;
  Half.Len := 0;
  BigSetSmall(Half, UInt32(Mantissa and $FFFFFFFF));
  if Mantissa shr 32 <> 0 then begin
    if Half.Len = 0 then
      BigPushLimb(Half, 0);
    BigPushLimb(Half, UInt32(Mantissa shr 32));
  end;
  { Both sides as integers: the powers with negative exponents move to the other side. }
  if Exponent >= 0 then
    BigMulPow10(Digits, Exponent)
  else
    BigMulPow10(Half, -Exponent);
  if BinaryExponent >= 0 then
    BigShiftLeft(Half, BinaryExponent)
  else
    BigShiftLeft(Digits, -BinaryExponent);
  Result := BigCompare(Digits, Half);
end;

function DecimalToDouble(const B: TBytes; Start, Stop: Int32; out D: Double): Boolean;
var
  J, DigitsStart, Digits, Q, Order: Int32;
  Negative, Truncated, Fraction: Boolean;
  W, Bits, Next: UInt64;
  Exponent: Int64;
  C: Byte;
begin
  J := Start;
  Negative := B[J] = Ord('-');
  if Negative then
    Inc(J);
  DigitsStart := J;
  W := 0;
  Digits := 0;
  Exponent := 0;
  Truncated := False;
  Fraction := False;
  while J < Stop do begin
    C := B[J];
    if C = Ord('.') then begin
      Fraction := True;
      Inc(J);
      Continue;
    end;
    if (C < Ord('0')) or (C > Ord('9')) then
      Break;
    if (Digits = 0) and (C = Ord('0')) then begin
      { Leading zeros count only as a fraction's places. }
      if Fraction then
        Dec(Exponent);
    end else if Digits < 19 then begin
      W := W * 10 + UInt64(C - Ord('0'));
      Inc(Digits);
      if Fraction then
        Dec(Exponent);
    end else begin
      { Beyond 19 digits: dropped, but an integer part's dropped digits still scale the value. }
      if C <> Ord('0') then
        Truncated := True;
      if not Fraction then
        Inc(Exponent);
    end;
    Inc(J);
  end;
  Inc(Exponent, ReadExponent(B, J, Stop));
  if Exponent > 100000 then
    Q := 100000
  else if Exponent < -100000 then
    Q := -100000
  else
    Q := Int32(Exponent);
  Bits := EiselLemire(W, Q);
  if Truncated then begin
    Next := EiselLemire(W + 1, Q);
    if Next <> Bits then begin
      { The digits that were dropped decide between the two: the value is above the truncated one, so it rounds to
        the lower Double, to the next one up, or (exactly halfway) to whichever of them is even. }
      if Bits = InfinityBits then
        Order := -1
      else
        Order := CompareWithHalfway(B, DigitsStart, Stop, Bits);
      if (Order > 0) or ((Order = 0) and (Bits and 1 = 1)) then
        Inc(Bits);
    end;
  end;
  if Bits and UInt64($7FFFFFFFFFFFFFFF) >= InfinityBits then begin
    D := 0;
    Result := False;
    Exit;
  end;
  D := DoubleFromBits(Bits);
  if Negative then
    D := -D;
  Result := True;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Comparisons }

function CompareInt(A, B: Int64): Int32; inline;
begin
  if A < B then
    Result := -1
  else if A > B then
    Result := 1
  else
    Result := 0;
end;

{ CompareIntFloat compares an Int64 with a (finite) Double exactly. }
function CompareIntFloat(A: Int64; D: Double): Int32;
var
  Floor: Double;
begin
  if D >= Two63 then begin
    Result := -1;
    Exit;
  end;
  if D < -Two63 then begin
    Result := 1;
    Exit;
  end;
  Floor := FloorOf(D);
  Result := CompareInt(A, Trunc(Floor));
  if Result <> 0 then
    Exit;
  if D > Floor then
    Result := -1;
end;

{ CompareUintFloat compares a UInt64 at or above 2^63 with a (finite) Double exactly. }
function CompareUintFloat(U: UInt64; D: Double): Int32;
var
  V: UInt64;
begin
  if D >= Two64 then begin
    Result := -1;
    Exit;
  end;
  if D < Two63 then begin
    Result := 1;
    Exit;
  end;
  { Floats in [2^63, 2^64) are integers. }
  V := UInt64(Trunc(D - Two63)) or (UInt64(1) shl 63);
  if U < V then
    Result := -1
  else if U > V then
    Result := 1
  else
    Result := 0;
end;

function CompareNumbers(FA: Byte; A: UInt64; FB: Byte; B: UInt64): Int32;
var
  X, Y: Double;
begin
  case FA of
    NumFlagInt: begin
      case FB of
        NumFlagInt: Result := CompareInt(Int64(A), Int64(B));
        NumFlagFloat: Result := CompareIntFloat(Int64(A), DoubleFromBits(B));
      else
        Result := -1;
      end;
      Exit;
    end;
    NumFlagFloat: begin
      X := DoubleFromBits(A);
      case FB of
        NumFlagFloat: begin
          Y := DoubleFromBits(B);
          if X < Y then
            Result := -1
          else if X > Y then
            Result := 1
          else
            Result := 0;
        end;
        NumFlagInt: Result := -CompareIntFloat(Int64(B), X);
      else
        Result := -CompareUintFloat(B, X);
      end;
      Exit;
    end;
  end;
  { A UInt64 beyond an Int64. }
  case FB of
    NumFlagUint:
      if A < B then
        Result := -1
      else if A > B then
        Result := 1
      else
        Result := 0;
    NumFlagInt: Result := 1;
  else
    Result := CompareUintFloat(A, DoubleFromBits(B));
  end;
end;

function IsIntegerNumber(Flag: Byte; V: UInt64): Boolean;
var
  D: Double;
begin
  if Flag <> NumFlagFloat then begin
    Result := True;
    Exit;
  end;
  D := DoubleFromBits(V);
  Result := not IsInfinite(D) and (D = FloorOf(D));
end;

{ --------------------------------------------------------------------------------------------------------------------
  multipleOf }

{ TextExponent reads the exponent part of a number's text at J, if there is one. }
function TextExponent(const B: TBytes; J: Int32): Int64;
begin
  Result := ReadExponent(B, J, Length(B));
end;

{ DecimalOf reads the decimal of the number text at Start: the significand without trailing zeros and the exponent.
  The significand is M when it has at most 18 digits (Fits), and Big whenever it is asked for. }
procedure DecimalOf(const B: TBytes; Start: Int32; WantBig: Boolean; out M: Int64; out Exponent: Int64;
  out Fits: Boolean; var Big: TBig);
var
  J, Len, Digits, PendingZeros: Int32;
  C: Byte;
  Fraction: Boolean;
begin
  M := 0;
  Fits := True;
  if WantBig then
    BigSetSmall(Big, 0);
  Len := Length(B);
  J := Start;
  if B[J] = Ord('-') then
    Inc(J);
  Digits := 0;
  Exponent := 0;
  PendingZeros := 0;
  Fraction := False;
  while J < Len do begin
    C := B[J];
    if C = Ord('.') then begin
      Fraction := True;
      Inc(J);
      Continue;
    end;
    if (C < Ord('0')) or (C > Ord('9')) then
      Break;
    if Fraction then
      Dec(Exponent);
    if C = Ord('0') then begin
      { Trailing zeros are held back, so that they move into the exponent if no other digit follows. }
      if Digits > 0 then
        Inc(PendingZeros);
      Inc(J);
      Continue;
    end;
    while PendingZeros > 0 do begin
      if Digits >= 18 then
        Fits := False
      else
        M := M * 10;
      if WantBig then
        BigMulAdd(Big, 10, 0);
      Inc(Digits);
      Dec(PendingZeros);
    end;
    if Digits >= 18 then
      Fits := False
    else
      M := M * 10 + (C - Ord('0'));
    if WantBig then begin
      if Big.Len = 0 then
        BigSetSmall(Big, C - Ord('0'))
      else
        BigMulAdd(Big, 10, C - Ord('0'));
    end;
    Inc(Digits);
    Inc(J);
  end;
  Inc(Exponent, PendingZeros);
  Inc(Exponent, TextExponent(B, J));
end;

function NewDivisor(Flags: Byte; Data: UInt64; const B: TBytes; Start: Int32): TDivisor;
var
  M, Exponent: Int64;
  Fits: Boolean;
begin
  Result.IsInt := Flags = NumFlagInt;
  Result.Value := Int64(Data);
  Result.Significand := -1;
  Result.Exponent := 0;
  Result.Big.Limbs := nil;
  Result.Big.Len := 0;
  DecimalOf(B, Start, False, M, Exponent, Fits, Result.Big);
  Result.Exponent := Exponent;
  if Fits then
    Result.Significand := M
  else
    DecimalOf(B, Start, True, M, Exponent, Fits, Result.Big);
end;

{ The digits of the number text at Start: where they start, the last one that is not zero (or -1 for zero), and the
  power of ten of the digit string up to it. }
procedure DigitsOf(const B: TBytes; Start: Int32; out DigitsStart, LastNonZero: Int32; out Exponent: Int64);
var
  J, Stop, T, Len: Int32;
  C: Byte;
  Fraction: Boolean;
begin
  Len := Length(B);
  J := Start;
  if B[J] = Ord('-') then
    Inc(J);
  DigitsStart := J;
  Exponent := 0;
  LastNonZero := -1;
  Fraction := False;
  Stop := J;
  while Stop < Len do begin
    C := B[Stop];
    if C = Ord('.') then begin
      Fraction := True;
      Inc(Stop);
      Continue;
    end;
    if (C < Ord('0')) or (C > Ord('9')) then
      Break;
    if Fraction then
      Dec(Exponent);
    if C <> Ord('0') then
      LastNonZero := Stop;
    Inc(Stop);
  end;
  if LastNonZero < 0 then
    Exit;
  Inc(Exponent, TextExponent(B, Stop));
  { Digits after the last non-zero one are trailing zeros: they move into the exponent. }
  for T := LastNonZero + 1 to Stop - 1 do
    if B[T] <> Ord('.') then
      Inc(Exponent);
end;

{ DividesText reports whether the number text at Start is a multiple of Dm * 10^De (Dm positive, no trailing
  zeros). The text's digits are streamed modulo what remains of the divisor, so the text may have any number of
  digits. }
function DividesText(const B: TBytes; Start: Int32; Dm: UInt64; De: Int64): Boolean;
var
  DigitsStart, LastNonZero, T: Int32;
  Exponent, Shift, Twos, Fives: Int64;
  Rest, R: UInt64;
  C: Byte;
begin
  { x = xm * 10^xe with xm the digit string (trailing zeros moved into xe). x / d is an integer exactly when dm
    divides xm * 10^(xe - de). }
  DigitsOf(B, Start, DigitsStart, LastNonZero, Exponent);
  if LastNonZero < 0 then begin
    { Zero is a multiple of everything. }
    Result := True;
    Exit;
  end;
  Shift := Exponent - De;
  if Shift < 0 then begin
    { dm * 10^-shift must divide xm, which has no trailing zero, so is not a multiple of 10. }
    Result := False;
    Exit;
  end;
  { Remove from dm the factors of 2 and 5 that 10^shift supplies. What remains must divide xm. }
  Rest := Dm;
  Twos := 0;
  while (Twos < Shift) and (Rest and 1 = 0) do begin
    Rest := Rest shr 1;
    Inc(Twos);
  end;
  Fives := 0;
  while (Fives < Shift) and (Rest mod 5 = 0) do begin
    Rest := Rest div 5;
    Inc(Fives);
  end;
  if Rest = 1 then begin
    Result := True;
    Exit;
  end;
  R := 0;
  for T := DigitsStart to LastNonZero do begin
    C := B[T];
    if C = Ord('.') then
      Continue;
    { r < rest <= 10^18, so r * 10 + 9 < 2^64. }
    R := (R * 10 + UInt64(C - Ord('0'))) mod Rest;
  end;
  Result := R = 0;
end;

{ DividesTextBig is DividesText for a divisor whose significand has more than 18 digits. The remainder is kept
  below the divisor by subtraction, at most nine times for each digit. }
function DividesTextBig(const B: TBytes; Start: Int32; const Dm: TBig; De: Int64): Boolean;
var
  DigitsStart, LastNonZero, T: Int32;
  Exponent, Shift, Twos, Fives: Int64;
  Rest, R: TBig;
  C: Byte;
begin
  DigitsOf(B, Start, DigitsStart, LastNonZero, Exponent);
  if LastNonZero < 0 then begin
    Result := True;
    Exit;
  end;
  Shift := Exponent - De;
  if Shift < 0 then begin
    Result := False;
    Exit;
  end;
  Rest.Limbs := nil;
  Rest.Len := 0;
  BigCopy(Dm, Rest);
  Twos := 0;
  while (Twos < Shift) and (Rest.Len > 0) and (Rest.Limbs[0] and 1 = 0) do begin
    BigDivSmall(Rest, 2);
    Inc(Twos);
  end;
  Fives := 0;
  while (Fives < Shift) and (Rest.Len > 0) and (BigModSmall(Rest, 5) = 0) do begin
    BigDivSmall(Rest, 5);
    Inc(Fives);
  end;
  if (Rest.Len = 1) and (Rest.Limbs[0] = 1) then begin
    Result := True;
    Exit;
  end;
  R.Limbs := nil;
  R.Len := 0;
  BigSetSmall(R, 0);
  for T := DigitsStart to LastNonZero do begin
    C := B[T];
    if C = Ord('.') then
      Continue;
    if R.Len = 0 then
      BigSetSmall(R, C - Ord('0'))
    else
      BigMulAdd(R, 10, C - Ord('0'));
    while BigCompare(R, Rest) >= 0 do
      BigSub(R, Rest);
  end;
  Result := R.Len = 0;
end;

function DivisorDivides(const V: TDivisor; Flags: Byte; Data: UInt64; const B: TBytes; Start: Int32): Boolean;
begin
  if V.IsInt and (Flags = NumFlagInt) then begin
    { The least Int64 divided by -1 overflows, and is a multiple of it. }
    if V.Value = -1 then
      Result := True
    else
      Result := (V.Value <> 0) and (Int64(Data) mod V.Value = 0);
    Exit;
  end;
  if V.Significand = 0 then begin
    Result := False;
    Exit;
  end;
  if V.Significand > 0 then
    Result := DividesText(B, Start, UInt64(V.Significand), V.Exponent)
  else if V.Big.Len = 0 then
    Result := False
  else
    { A divisor of more than 18 significant digits. }
    Result := DividesTextBig(B, Start, V.Big, V.Exponent);
end;

end.
