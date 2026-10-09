unit Corvus.JsonSchema.Formats;
{$I corvus.inc}

// Format assertions, applied only when format is asserted (the format-assertion vocabulary or the assert format
// option). They match the C# evaluator's format checks.
//
// Ported from formats.go. The checks read UTF-8 bytes in place: a text is the bytes S[From .. Stop-1] of a larger
// buffer. Where the Go module uses its linear-time regular expressions (URI, IRI, URI template, e-mail local part),
// this unit reads the same grammars directly, as the Julia port does, so no check here needs the pattern engine but
// the regex format.

interface

uses
  SysUtils;

type
  // Whether Text[Start .. Start+Len-1] (UTF-8 bytes) is valid for the format.
  TFormatCheck = function(const Text: TBytes; Start, Len: Int32): Boolean;

  // TFormatKind is the format a dialect recognises for a format value.
  TFormatKind = (
    FormatKindUnknown,
    FormatKindDate,
    FormatKindTime,
    FormatKindDateTime,
    FormatKindDuration,
    FormatKindUUID,
    FormatKindIPv4,
    FormatKindIPv6,
    FormatKindHostname,
    FormatKindIDNHostname,
    FormatKindEmail,
    FormatKindIDNEmail,
    FormatKindURI,
    FormatKindURIReference,
    FormatKindIRI,
    FormatKindIRIReference,
    FormatKindURITemplate,
    FormatKindJSONPointer,
    FormatKindRelativeJSONPointer,
    FormatKindRegex,
    // Numeric formats (a Corvus extension).
    FormatKindByte,
    FormatKindUInt16,
    FormatKindUInt32,
    FormatKindUInt64,
    FormatKindUInt128,
    FormatKindSByte,
    FormatKindInt16,
    FormatKindInt32,
    FormatKindInt64,
    FormatKindInt128,
    FormatKindHalf,
    FormatKindSingle,
    FormatKindDouble,
    FormatKindDecimal);

const
  // The dialects as FormatKindOf takes them: the ordinal of the evaluator's dialect, in specification order.
  FormatDialectDraft4 = 0;
  FormatDialectDraft6 = 1;
  FormatDialectDraft7 = 2;
  FormatDialectDraft201909 = 3;
  FormatDialectDraft202012 = 4;

  // How a document holds a number, as FormatCheckNumber takes it (the number flags of the document).
  // FormatNumInt is an integer that fits an Int64.
  FormatNumInt = 0;
  // FormatNumUint is an integer in [2^63, 2^64), held as a UInt64.
  FormatNumUint = 1;
  // FormatNumFloat is anything else, held as the bits of a Double.
  FormatNumFloat = 2;

// FormatKindOf is the format a dialect recognises for a format value. Dialect is the ordinal of the dialect.
function FormatKindOf(const Format: UTF8String; Dialect: Int32): TFormatKind;

function FormatKindIsNumeric(Kind: TFormatKind): Boolean; inline;

// FormatKindName is the canonical name, for messages.
function FormatKindName(Kind: TFormatKind): UTF8String;

// FormatKindMessage is the message for a string format failure (the C# evaluator's text), or ''.
function FormatKindMessage(Kind: TFormatKind): UTF8String;

// FormatCheckString asserts a string format. LegacyHostname selects the RFC 1123 host name rules of draft 4 and 6.
function FormatCheckString(Kind: TFormatKind; const Text: TBytes; Start, Len: Int32;
  LegacyHostname: Boolean): Boolean;

// FormatCheckNumber asserts a numeric format. Flag and Data are the number as the document holds it: an Int64 or a
// UInt64 as it is, or the bits of a Double.
function FormatCheckNumber(Kind: TFormatKind; Flag: Int32; Data: UInt64): Boolean;

// FormatKindValidator is the check FormatCheckString makes for a string format, as a function to keep in a compiled
// program. It is nil for the unknown format and for the numeric formats, which assert nothing of a string.
function FormatKindValidator(Kind: TFormatKind; LegacyHostname: Boolean): TFormatCheck;

// The built-in validator for a format name, or nil when the name is not a known format. It is the format as the
// latest dialect reads it. The numeric formats assert nothing of a string and have no validator.
function FormatValidator(const Name: UTF8String): TFormatCheck;

// The checks themselves. Each reports whether Text[Start .. Start+Len-1] is valid for its format.
function IsDate(const Text: TBytes; Start, Len: Int32): Boolean;
function IsTime(const Text: TBytes; Start, Len: Int32): Boolean;
function IsDateTime(const Text: TBytes; Start, Len: Int32): Boolean;
function IsDuration(const Text: TBytes; Start, Len: Int32): Boolean;
function IsUUID(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIPv4(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIPv6(const Text: TBytes; Start, Len: Int32): Boolean;
function IsLegacyHostname(const Text: TBytes; Start, Len: Int32): Boolean;
function IsHostname(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIDNHostname(const Text: TBytes; Start, Len: Int32): Boolean;
function IsEmail(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIDNEmail(const Text: TBytes; Start, Len: Int32): Boolean;
function IsURI(const Text: TBytes; Start, Len: Int32): Boolean;
function IsURIReference(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIRI(const Text: TBytes; Start, Len: Int32): Boolean;
function IsIRIReference(const Text: TBytes; Start, Len: Int32): Boolean;
function IsURITemplate(const Text: TBytes; Start, Len: Int32): Boolean;
function IsJSONPointer(const Text: TBytes; Start, Len: Int32): Boolean;
function IsRelativeJSONPointer(const Text: TBytes; Start, Len: Int32): Boolean;
function IsRegex(const Text: TBytes; Start, Len: Int32): Boolean;

// Base64Decode decodes padded standard base64 into Output (reused, and grown when it is too short), giving the
// number of bytes decoded. It accepts no line breaks. It is the contentEncoding check of draft 7, which eval.go
// holds in the Go source. The contentMediaType check reads the decoded bytes with the document parser.
function Base64Decode(const Text: TBytes; Start, Len: Int32; var Output: TBytes; out OutputLen: Int32): Boolean;

implementation

uses
  Corvus.JsonSchema.Uri, Corvus.JsonSchema.Ucd, Corvus.JsonSchema.EcmaRegex;

type
  // The code points of a text.
  TRunes = array of Int32;

  TByteSet = set of Byte;

const
  // RuneSelf is the first code point that UTF-8 writes in more than one byte.
  RuneSelf = $80;
  // RuneError is what a byte that is not well-formed UTF-8 reads as.
  RuneError = $FFFD;
  MaxRune = $10FFFF;
  MaxUInt32 = Int64($FFFFFFFF);

function FormatKindOf(const Format: UTF8String; Dialect: Int32): TFormatKind;

  function AtLeast(D: Int32; K: TFormatKind): TFormatKind;
  begin
    if Dialect >= D then Result := K
    else Result := FormatKindUnknown;
  end;

  function Named(const Name: UTF8String): Boolean;
  begin
    Result := Utf8Equal(Format, Name);
  end;

begin
  if Named('float') or Named('single') then Result := FormatKindSingle
  else if Named('byte') then Result := FormatKindByte
  else if Named('uint16') then Result := FormatKindUInt16
  else if Named('uint32') then Result := FormatKindUInt32
  else if Named('uint64') then Result := FormatKindUInt64
  else if Named('uint128') then Result := FormatKindUInt128
  else if Named('sbyte') then Result := FormatKindSByte
  else if Named('int16') then Result := FormatKindInt16
  else if Named('int32') then Result := FormatKindInt32
  else if Named('int64') then Result := FormatKindInt64
  else if Named('int128') then Result := FormatKindInt128
  else if Named('half') then Result := FormatKindHalf
  else if Named('double') then Result := FormatKindDouble
  else if Named('decimal') then Result := FormatKindDecimal
  else if Named('date-time') then Result := FormatKindDateTime
  else if Named('email') then Result := FormatKindEmail
  else if Named('hostname') then Result := FormatKindHostname
  else if Named('ipv4') then Result := FormatKindIPv4
  else if Named('ipv6') then Result := FormatKindIPv6
  else if Named('uri') then Result := FormatKindURI
  else if Named('uri-reference') then Result := AtLeast(FormatDialectDraft6, FormatKindURIReference)
  else if Named('uri-template') then Result := AtLeast(FormatDialectDraft6, FormatKindURITemplate)
  else if Named('json-pointer') then Result := AtLeast(FormatDialectDraft6, FormatKindJSONPointer)
  else if Named('date') then Result := AtLeast(FormatDialectDraft7, FormatKindDate)
  else if Named('time') then Result := AtLeast(FormatDialectDraft7, FormatKindTime)
  else if Named('regex') then Result := AtLeast(FormatDialectDraft7, FormatKindRegex)
  else if Named('relative-json-pointer') then Result := AtLeast(FormatDialectDraft7, FormatKindRelativeJSONPointer)
  else if Named('idn-email') then Result := AtLeast(FormatDialectDraft7, FormatKindIDNEmail)
  else if Named('idn-hostname') then Result := AtLeast(FormatDialectDraft7, FormatKindIDNHostname)
  else if Named('iri') then Result := AtLeast(FormatDialectDraft7, FormatKindIRI)
  else if Named('iri-reference') then Result := AtLeast(FormatDialectDraft7, FormatKindIRIReference)
  else if Named('duration') then Result := AtLeast(FormatDialectDraft201909, FormatKindDuration)
  else if Named('uuid') then Result := AtLeast(FormatDialectDraft201909, FormatKindUUID)
  else Result := FormatKindUnknown;
end;

function FormatKindIsNumeric(Kind: TFormatKind): Boolean;
begin
  Result := Kind >= FormatKindByte;
end;

const
  FormatNames: array[TFormatKind] of UTF8String = (
    'unknown', 'date', 'time', 'date-time', 'duration', 'uuid', 'ipv4', 'ipv6', 'hostname', 'idn-hostname', 'email',
    'idn-email', 'uri', 'uri-reference', 'iri', 'iri-reference', 'uri-template', 'json-pointer',
    'relative-json-pointer', 'regex', 'byte', 'uint16', 'uint32', 'uint64', 'uint128', 'sbyte', 'int16', 'int32',
    'int64', 'int128', 'half', 'single', 'double', 'decimal');

function FormatKindName(Kind: TFormatKind): UTF8String;
begin
  Result := FormatNames[Kind];
end;

function FormatKindMessage(Kind: TFormatKind): UTF8String;
begin
  case Kind of
    FormatKindDate: Result := 'Expected an ISO8601 Date string.';
    FormatKindDateTime: Result := 'Expected an ISO8601 Offset DateTime string.';
    FormatKindTime: Result := 'Expected an ISO8601 Offset Time string.';
    FormatKindDuration: Result := 'Expected an ISO8601 Duration string.';
    FormatKindEmail: Result := 'Expected an RFC5321 Section-4.1.2 Email string.';
    FormatKindIDNEmail: Result := 'Expected an RFC6531 IDN Email string.';
    FormatKindHostname: Result := 'Expected an RFC1035 hostname.';
    FormatKindIDNHostname: Result := 'Expected an RFC5890 Section-2.3.2.3 IDN hostname.';
    FormatKindIPv4: Result := 'Expected an RFC2673 IP V4 address.';
    FormatKindIPv6: Result := 'Expected an RFC2373 IP V6 address.';
    FormatKindURI: Result := 'Expected an absolute URI.';
    FormatKindURIReference: Result := 'Expected a URI reference.';
    FormatKindIRI: Result := 'Expected an absolute IRI.';
    FormatKindIRIReference: Result := 'Expected an IRI reference.';
    FormatKindUUID: Result := 'Expected an RFC4122 UUID.';
    FormatKindURITemplate: Result := 'Expected an RFC6570 URI Template.';
    FormatKindJSONPointer: Result := 'Expected an RFC6901 JSON Pointer.';
    FormatKindRelativeJSONPointer:
      Result := 'Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer).';
    FormatKindRegex: Result := 'Expected a regular expression specification.';
  else
    Result := '';
  end;
end;

function FormatCheckNumber(Kind: TFormatKind; Flag: Int32; Data: UInt64): Boolean;
const
  // The limits, as the bits of the Double nearest to each, so that they do not depend on how a compiler reads a
  // decimal number.
  // 3.402823669209385e38, which is 2^128.
  UInt128Limit = UInt64($47F0000000000000);
  // 1.7014118346046923e38, which is 2^127.
  Int128Limit = UInt64($47E0000000000000);
  // 65504, the largest half precision number.
  HalfLimit = UInt64($40EFFC0000000000);
  // 3.4028234663852886e38, the largest single precision number.
  SingleLimit = UInt64($47EFFFFFE0000000);
  // 1.7976931348623157e308, the largest double precision number.
  DoubleLimit = UInt64($7FEFFFFFFFFFFFFF);
  // 7.922816251426434e28, which is 2^96.
  DecimalLimit = UInt64($45F0000000000000);
  // 2^63 and 2^64, the Doubles nearest to the largest Int64 and the largest UInt64.
  Int64Limit = UInt64($43E0000000000000);
  UInt64Limit = UInt64($43F0000000000000);
  ExponentMask = UInt64($7FF0000000000000);
var
  F: Double;
  Infinite: Boolean;

  function FromBits(Bits: UInt64): Double;
  begin
    Move(Bits, Result, SizeOf(Result));
  end;

  function Integral(Lo, Hi: Double): Boolean;
  begin
    Result := (not Infinite) and (F = Int(F)) and (F >= Lo) and (F <= Hi);
  end;

  // IntRange reports an integer from Lo to Hi. A limit that is beyond Int64 is given as its Double.
  function IntRange(Lo: Int64; Hi: UInt64; LoFloat, HiFloat: Double): Boolean;
  var
    I: Int64;
  begin
    if Flag = FormatNumInt then begin
      I := Int64(Data);
      Result := (I >= Lo) and ((I < 0) or (Data <= Hi));
    end else if Flag = FormatNumUint then Result := Data <= Hi
    else Result := Integral(LoFloat, HiFloat);
  end;

  function SmallRange(Lo, Hi: Int64): Boolean;
  begin
    Result := IntRange(Lo, UInt64(Hi), Lo, Hi);
  end;

  function Magnitude(Limit: Double): Boolean;
  begin
    Result := (not Infinite) and (Abs(F) <= Limit);
  end;

begin
  Infinite := False;
  if Flag = FormatNumInt then F := Int64(Data)
  else if Flag = FormatNumUint then F := Data
  else begin
    F := FromBits(Data);
    // An exponent of all ones is an infinity or not a number, and neither is within any bound.
    Infinite := (Data and ExponentMask) = ExponentMask;
  end;
  case Kind of
    FormatKindByte: Result := SmallRange(0, 255);
    FormatKindUInt16: Result := SmallRange(0, 65535);
    FormatKindUInt32: Result := SmallRange(0, 4294967295);
    FormatKindUInt64: Result := IntRange(0, High(UInt64), 0, FromBits(UInt64Limit));
    FormatKindUInt128:
      if Flag = FormatNumInt then Result := Int64(Data) >= 0
      else if Flag = FormatNumUint then Result := True
      else Result := Integral(0, FromBits(UInt128Limit));
    FormatKindSByte: Result := SmallRange(-128, 127);
    FormatKindInt16: Result := SmallRange(-32768, 32767);
    FormatKindInt32: Result := SmallRange(-2147483648, 2147483647);
    FormatKindInt64:
      Result := IntRange(Low(Int64), UInt64(High(Int64)), -FromBits(Int64Limit), FromBits(Int64Limit));
    FormatKindInt128:
      Result := (Flag <> FormatNumFloat) or Integral(-FromBits(Int128Limit), FromBits(Int128Limit));
    FormatKindHalf: Result := Magnitude(FromBits(HalfLimit));
    FormatKindSingle: Result := Magnitude(FromBits(SingleLimit));
    FormatKindDouble: Result := Magnitude(FromBits(DoubleLimit));
    FormatKindDecimal: Result := Magnitude(FromBits(DecimalLimit));
  else
    Result := True;
  end;
end;

// ---------------------------------------------------------------------------------------------------------------------
// Reading bytes

// IndexByte is the index of the first C in S[From .. Stop-1], or -1.
function IndexByte(const S: TBytes; From, Stop: Int32; C: Byte): Int32;
var
  I: Int32;
begin
  for I := From to Stop - 1 do begin
    if S[I] = C then begin
      Result := I;
      Exit;
    end;
  end;
  Result := -1;
end;

// LastIndexByte is the index of the last C in S[From .. Stop-1], or -1.
function LastIndexByte(const S: TBytes; From, Stop: Int32; C: Byte): Int32;
var
  I: Int32;
begin
  for I := Stop - 1 downto From do begin
    if S[I] = C then begin
      Result := I;
      Exit;
    end;
  end;
  Result := -1;
end;

// DecodeRune reads the code point at S[I], which is before Stop, and gives its size in bytes. A byte that does not
// start well-formed UTF-8 (an overlong form, a surrogate, a value above U+10FFFF, a sequence cut short) reads as
// RuneError with a size of one, as it does in the Go source.
function DecodeRune(const S: TBytes; I, Stop: NativeInt; out Size: Int32): Int32;
var
  B0, B1, B2, B3, Lo, Hi: Byte;
begin
  Size := 1;
  Result := RuneError;
  B0 := S[I];
  if B0 < RuneSelf then begin
    Result := B0;
    Exit;
  end;
  if (B0 < $C2) or (B0 > $F4) or (I + 1 >= Stop) then Exit;
  B1 := S[I + 1];
  Lo := $80;
  Hi := $BF;
  if B0 < $E0 then begin
    if (B1 < Lo) or (B1 > Hi) then Exit;
    Result := (Int32(B0 and $1F) shl 6) or Int32(B1 and $3F);
    Size := 2;
    Exit;
  end;
  if B0 < $F0 then begin
    if B0 = $E0 then Lo := $A0;
    if B0 = $ED then Hi := $9F;
    if (B1 < Lo) or (B1 > Hi) or (I + 2 >= Stop) then Exit;
    B2 := S[I + 2];
    if (B2 < $80) or (B2 > $BF) then Exit;
    Result := (Int32(B0 and $0F) shl 12) or (Int32(B1 and $3F) shl 6) or Int32(B2 and $3F);
    Size := 3;
    Exit;
  end;
  if B0 = $F0 then Lo := $90;
  if B0 = $F4 then Hi := $8F;
  if (B1 < Lo) or (B1 > Hi) or (I + 3 >= Stop) then Exit;
  B2 := S[I + 2];
  B3 := S[I + 3];
  if (B2 < $80) or (B2 > $BF) or (B3 < $80) or (B3 > $BF) then Exit;
  Result := (Int32(B0 and $07) shl 18) or (Int32(B1 and $3F) shl 12) or (Int32(B2 and $3F) shl 6) or
    Int32(B3 and $3F);
  Size := 4;
end;

// CodePoints is the code points of UTF-8 text.
function CodePoints(const S: TBytes; From, Stop: Int32): TRunes;
var
  I, Count, Size: Int32;
begin
  Result := nil;
  SetLength(Result, Stop - From);
  Count := 0;
  I := From;
  while I < Stop do begin
    Result[Count] := DecodeRune(S, I, Stop, Size);
    Inc(Count);
    Inc(I, Size);
  end;
  SetLength(Result, Count);
end;

function IsASCIIRange(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  for I := From to Stop - 1 do begin
    if S[I] >= RuneSelf then begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;

// ---------------------------------------------------------------------------------------------------------------------
// Dates, times and durations

function IsLeapYear(Y: Int32): Boolean;
begin
  Result := (Y mod 4 = 0) and ((Y mod 100 <> 0) or (Y mod 400 = 0));
end;

// DigitsValue is the value of a run of ASCII digits, or -1.
function DigitsValue(const S: TBytes; From, Stop: Int32): Int32;
var
  I, V: Int32;
begin
  Result := -1;
  if From >= Stop then Exit;
  V := 0;
  for I := From to Stop - 1 do begin
    if not IsASCIIDigit(S[I]) then Exit;
    V := V * 10 + (S[I] - Ord('0'));
  end;
  Result := V;
end;

const
  MonthDays: array[0..12] of Int32 = (0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31);

function DateOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  Y, M, D: Int32;
begin
  Result := False;
  if (Stop - From <> 10) or (S[From + 4] <> Ord('-')) or (S[From + 7] <> Ord('-')) then Exit;
  Y := DigitsValue(S, From, From + 4);
  M := DigitsValue(S, From + 5, From + 7);
  D := DigitsValue(S, From + 8, From + 10);
  if (Y < 0) or (M < 1) or (M > 12) or (D < 1) then Exit;
  if (M = 2) and IsLeapYear(Y) then Result := D <= 29
  else Result := D <= MonthDays[M];
end;

function TimeOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  H, Mi, Sec, I, Start, OH, OM, Sign, Utc: Int32;
begin
  Result := False;
  if (Stop - From < 9) or (S[From + 2] <> Ord(':')) or (S[From + 5] <> Ord(':')) then Exit;
  H := DigitsValue(S, From, From + 2);
  Mi := DigitsValue(S, From + 3, From + 5);
  Sec := DigitsValue(S, From + 6, From + 8);
  if (H < 0) or (Mi < 0) or (Sec < 0) then Exit;
  I := From + 8;
  if S[I] = Ord('.') then begin
    Inc(I);
    Start := I;
    while (I < Stop) and IsASCIIDigit(S[I]) do Inc(I);
    if I = Start then Exit;
  end;
  if I >= Stop then Exit;
  OH := 0;
  OM := 0;
  Sign := 0;
  if (S[I] = Ord('z')) or (S[I] = Ord('Z')) then begin
    if I + 1 <> Stop then Exit;
  end else if (S[I] = Ord('+')) or (S[I] = Ord('-')) then begin
    if (Stop - I <> 6) or (S[I + 3] <> Ord(':')) then Exit;
    Sign := -1;
    if S[I] = Ord('-') then Sign := 1;
    OH := DigitsValue(S, I + 1, I + 3);
    OM := DigitsValue(S, I + 4, I + 6);
    if (OH < 0) or (OM < 0) or (OH > 23) or (OM > 59) then Exit;
  end else Exit;
  if (H > 23) or (Mi > 59) or (Sec > 60) then Exit;
  if Sec = 60 then begin
    // A leap second is only valid at 23:59:60 UTC.
    Utc := (H * 60 + Mi + Sign * (OH * 60 + OM)) mod 1440;
    if Utc < 0 then Inc(Utc, 1440);
    Result := Utc = 23 * 60 + 59;
    Exit;
  end;
  Result := True;
end;

function DateTimeOK(const S: TBytes; From, Stop: Int32): Boolean;
begin
  Result := (Stop - From > 11) and ((S[From + 10] = Ord('T')) or (S[From + 10] = Ord('t'))) and
    DateOK(S, From, From + 10) and TimeOK(S, From + 11, Stop);
end;

const
  DurationTime: array[0..2] of Byte = (Ord('H'), Ord('M'), Ord('S'));
  DurationDate: array[0..2] of Byte = (Ord('Y'), Ord('M'), Ord('D'));
  DurationWeek: array[0..0] of Byte = (Ord('W'));

// DurationOK reads an ISO 8601 duration: P then weeks, or date parts in order then an optional time, or a time.
function DurationOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  I, Save: Int32;

  // Part reads digits and one of the designators from position First in order, returning the designator's index.
  function Part(const Designators: array of Byte; First: Int32): Int32;
  var
    Start, At: Int32;
  begin
    Result := -1;
    Start := I;
    while (I < Stop) and IsASCIIDigit(S[I]) do Inc(I);
    if (I = Start) or (I >= Stop) then Exit;
    for At := First to High(Designators) do begin
      if Designators[At] = S[I] then begin
        Inc(I);
        Result := At;
        Exit;
      end;
    end;
  end;

  // Run reads one or more consecutive parts in designator order.
  function Run(const Designators: array of Byte): Boolean;
  var
    At, Next: Int32;
  begin
    Result := False;
    At := Part(Designators, 0);
    if At < 0 then Exit;
    while (At + 1 < Length(Designators)) and (I < Stop) and IsASCIIDigit(S[I]) do begin
      Next := Part(Designators, At + 1);
      if Next <> At + 1 then Exit;
      At := Next;
    end;
    Result := True;
  end;

begin
  Result := False;
  if (Stop - From < 2) or (S[From] <> Ord('P')) then Exit;
  I := From + 1;
  if S[I] = Ord('T') then begin
    Inc(I);
    Result := Run(DurationTime) and (I = Stop);
    Exit;
  end;
  Save := I;
  if Part(DurationWeek, 0) = 0 then begin
    Result := I = Stop;
    Exit;
  end;
  I := Save;
  if not Run(DurationDate) then Exit;
  if (I < Stop) and (S[I] = Ord('T')) then begin
    Inc(I);
    Result := Run(DurationTime) and (I = Stop);
    Exit;
  end;
  Result := I = Stop;
end;

// ---------------------------------------------------------------------------------------------------------------------
// UUIDs and IP addresses

function IsHexDigit(C: Byte): Boolean;
begin
  Result := HexValue(C) >= 0;
end;

function UUIDOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  Result := False;
  if Stop - From <> 36 then Exit;
  for I := 0 to 35 do begin
    if (I = 8) or (I = 13) or (I = 18) or (I = 23) then begin
      if S[From + I] <> Ord('-') then Exit;
    end else if not IsHexDigit(S[From + I]) then Exit;
  end;
  Result := True;
end;

function IPv4OK(const S: TBytes; From, Stop: Int32): Boolean;
var
  Parts, P, Dot, PartStop, L, V: Int32;
begin
  Result := False;
  Parts := 0;
  P := From;
  while True do begin
    Dot := IndexByte(S, P, Stop, Ord('.'));
    if Dot >= 0 then PartStop := Dot
    else PartStop := Stop;
    Inc(Parts);
    L := PartStop - P;
    if (Parts > 4) or (L = 0) or (L > 3) or ((L > 1) and (S[P] = Ord('0'))) then Exit;
    V := DigitsValue(S, P, PartStop);
    if (V < 0) or (V > 255) then Exit;
    if Dot < 0 then begin
      Result := Parts = 4;
      Exit;
    end;
    P := Dot + 1;
  end;
end;

type
  // An address with "0:0" in the place of an IPv4 tail. A valid address is at most 64 bytes here.
  TIPv6Buffer = array[0..66] of Byte;

// HexGroups is the number of colon-separated groups of one to four hex digits, or -1 when one is not valid.
function HexGroups(const B: TIPv6Buffer; From, Stop: Int32): Int32;
var
  N, P, I, Colon, PartStop: Int32;
begin
  Result := 0;
  if From >= Stop then Exit;
  Result := -1;
  N := 0;
  P := From;
  while True do begin
    Colon := -1;
    for I := P to Stop - 1 do begin
      if B[I] = Ord(':') then begin
        Colon := I;
        Break;
      end;
    end;
    if Colon >= 0 then PartStop := Colon
    else PartStop := Stop;
    if (PartStop = P) or (PartStop - P > 4) then Exit;
    for I := P to PartStop - 1 do begin
      if not IsHexDigit(B[I]) then Exit;
    end;
    Inc(N);
    if Colon < 0 then begin
      Result := N;
      Exit;
    end;
    P := Colon + 1;
  end;
end;

// IndexDoubleColon is the index of the first "::" in B[From .. Stop-1], or -1.
function IndexDoubleColon(const B: TIPv6Buffer; From, Stop: Int32): Int32;
var
  I: Int32;
begin
  for I := From to Stop - 2 do begin
    if (B[I] = Ord(':')) and (B[I + 1] = Ord(':')) then begin
      Result := I;
      Exit;
    end;
  end;
  Result := -1;
end;

function IPv6OK(const S: TBytes; From, Stop: Int32): Boolean;
var
  Buf: TIPv6Buffer;
  N, I, LastColon, Dbl, L, R: Int32;
  C: Byte;
  HasDot: Boolean;
begin
  Result := False;
  // A valid address is at most 51 characters (an IPv4 tail after six groups), so longer text fails without a copy.
  N := Stop - From;
  if (N < 2) or (N > 64) then Exit;
  FillChar(Buf, SizeOf(Buf), 0);
  HasDot := False;
  for I := 0 to N - 1 do begin
    C := S[From + I];
    if not (IsHexDigit(C) or (C = Ord(':')) or (C = Ord('.'))) then Exit;
    HasDot := HasDot or (C = Ord('.'));
    Buf[I] := C;
  end;
  // An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place.
  if HasDot then begin
    LastColon := LastIndexByte(S, From, Stop, Ord(':'));
    if (LastColon < 0) or not IPv4OK(S, LastColon + 1, Stop) then Exit;
    N := LastColon + 1 - From;
    Buf[N] := Ord('0');
    Buf[N + 1] := Ord(':');
    Buf[N + 2] := Ord('0');
    Inc(N, 3);
  end;
  Dbl := IndexDoubleColon(Buf, 0, N);
  if Dbl >= 0 then begin
    if IndexDoubleColon(Buf, Dbl + 1, N) >= 0 then Exit;
    L := HexGroups(Buf, 0, Dbl);
    R := HexGroups(Buf, Dbl + 2, N);
    Result := (L >= 0) and (R >= 0) and (L + R < 8);
    Exit;
  end;
  Result := HexGroups(Buf, 0, N) = 8;
end;

// ---------------------------------------------------------------------------------------------------------------------
// Host names

type
  TLabelCheck = function(const S: TBytes; From, Stop: Int32): Boolean;

function PunycodeLabelOK(const S: TBytes; From, Stop: Int32): Boolean; forward;

function IsASCIIAlphanumeric(C: Byte): Boolean;
begin
  Result := IsASCIILetter(C) or IsASCIIDigit(C);
end;

function IsLDHLabel(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  Result := False;
  if (Stop - From = 0) or (Stop - From > 63) or not IsASCIIAlphanumeric(S[From]) or
    not IsASCIIAlphanumeric(S[Stop - 1]) then Exit;
  for I := From to Stop - 1 do begin
    if not IsASCIIAlphanumeric(S[I]) and (S[I] <> Ord('-')) then Exit;
  end;
  Result := True;
end;

function StartsWithXN(const S: TBytes; From, Stop: Int32): Boolean;
begin
  Result := (Stop - From >= 4) and ((S[From] or $20) = Ord('x')) and ((S[From + 1] or $20) = Ord('n')) and
    (S[From + 2] = Ord('-')) and (S[From + 3] = Ord('-'));
end;

function HostnameLabelOK(const S: TBytes; From, Stop: Int32): Boolean;
begin
  Result := False;
  if not IsLDHLabel(S, From, Stop) then Exit;
  // "--" in the third and fourth positions is reserved for A-labels (xn--).
  Result := not ((Stop - From >= 4) and (S[From + 2] = Ord('-')) and (S[From + 3] = Ord('-')) and
    not StartsWithXN(S, From, Stop));
end;

// EachLabel calls OK for each dot-separated label, stopping at the first it refuses.
function EachLabel(const S: TBytes; From, Stop: Int32; OK: TLabelCheck): Boolean;
var
  P, Dot: Int32;
begin
  P := From;
  while True do begin
    Dot := IndexByte(S, P, Stop, Ord('.'));
    if Dot < 0 then begin
      Result := OK(S, P, Stop);
      Exit;
    end;
    if not OK(S, P, Dot) then begin
      Result := False;
      Exit;
    end;
    P := Dot + 1;
  end;
end;

// LegacyHostnameOK checks RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels.
function LegacyHostnameOK(const S: TBytes; From, Stop: Int32): Boolean;
begin
  Result := (Stop - From <> 0) and (Stop - From <= 253) and EachLabel(S, From, Stop, IsLDHLabel);
end;

function HostnameLabel(const S: TBytes; From, Stop: Int32): Boolean;
begin
  Result := HostnameLabelOK(S, From, Stop) and
    (not StartsWithXN(S, From, Stop) or PunycodeLabelOK(S, From + 4, Stop));
end;

function HostnameOK(const S: TBytes; From, Stop: Int32): Boolean;
begin
  if (Stop - From = 0) or (Stop - From > 253) then Result := False
  else Result := EachLabel(S, From, Stop, HostnameLabel);
end;

type
  // TIdnTables holds the Unicode properties the host name checks read. They are built from the tables of
  // Corvus.JsonSchema.Ucd, which hold one fixed version of Unicode, and never from the Unicode data of the runtime
  // library, which follows the compiler. A format therefore accepts the same strings whichever compiler built the
  // program.
  //
  // RFC 5892 defines the IDNA2008 code point classes by rules over Unicode properties and not by a list for one
  // version of Unicode, so that they extend to each new version. The checks here apply those rules to the properties
  // of the tables' version.
  TIdnTables = record
    // Disallowed is the general categories that IDNA2008 never makes PVALID: controls, private use, unassigned,
    // spaces, uppercase and titlecase letters (mapped away), mathematical and other symbols, and punctuation.
    Disallowed: TUcdTable;
    // Invisible is the controls, the format characters, the spaces and the unassigned code points.
    Invisible: TUcdTable;
    // Mark is the group M, Nonspacing is Mn, and NonspacingOrEnclosing is Mn and Me, the stand-in for Bidi class
    // NSM.
    Mark, Nonspacing, NonspacingOrEnclosing: TUcdTable;
    // LetterOrSpacingMark is the group L and Mc, the stand-in for Bidi class L.
    LetterOrSpacingMark: TUcdTable;
    // The scripts the contextual rules of RFC 5892 and the Bidi rule of RFC 5893 name.
    Hebrew, Greek, ArabicLike, KanaOrHan: TUcdTable;
    // LetterMarkNumber is the groups L, M and N together, for the local part of an internationalized e-mail
    // address.
    LetterMarkNumber: TUcdTable;
  end;

var
  // Idn is the tables. The Go source builds them when a host name check first needs them. Here the unit builds them
  // as it starts, before any thread can read them, so no check takes a lock. A union of properties is one table, so
  // a code point is tested against it with one search.
  Idn: TIdnTables;

procedure BuildIdnTables;
begin
  Idn.Disallowed := UcdUnion([UcdCategory('Cc'), UcdCategory('Co'), UcdCategory('Zs'), UcdCategory('Zl'),
    UcdCategory('Zp'), UcdCategory('Lu'), UcdCategory('Lt'), UcdCategory('Sm'), UcdCategory('So'), UcdCategory('P'),
    UcdCategory('Cn')]);
  Idn.Invisible := UcdUnion([UcdCategory('Cc'), UcdCategory('Cf'), UcdCategory('Zs'), UcdCategory('Cn')]);
  Idn.Mark := UcdCategory('M')^;
  Idn.Nonspacing := UcdCategory('Mn')^;
  Idn.NonspacingOrEnclosing := UcdUnion([UcdCategory('Mn'), UcdCategory('Me')]);
  Idn.LetterOrSpacingMark := UcdUnion([UcdCategory('L'), UcdCategory('Mc')]);
  Idn.Hebrew := UcdScript('Hebrew')^;
  Idn.Greek := UcdScript('Greek')^;
  Idn.ArabicLike := UcdUnion([UcdScript('Arabic'), UcdScript('Syriac'), UcdScript('Thaana'), UcdScript('Nko')]);
  Idn.KanaOrHan := UcdUnion([UcdScript('Hiragana'), UcdScript('Katakana'), UcdScript('Han')]);
  Idn.LetterMarkNumber := UcdUnion([UcdCategory('L'), UcdCategory('M'), UcdCategory('N')]);
end;

function IsDisallowedException(R: Int32): Boolean;
begin
  case R of
    $06FD, $06FE, $0F0B, $00B7, $05F3, $05F4, $30FB: Result := True;
  else
    Result := False;
  end;
end;

// Disallowed reports code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned,
// spaces, uppercase and titlecase letters (mapped away, never PVALID), symbols and punctuation.
function Disallowed(const Cps: TRunes): Boolean;
var
  I, R: Int32;
begin
  for I := 0 to High(Cps) do begin
    R := Cps[I];
    if (R = Ord('-')) or IsDisallowedException(R) then Continue;
    if Idn.Disallowed.Contains(R) then begin
      Result := True;
      Exit;
    end;
  end;
  Result := False;
end;

const
  PunyBase = 36;
  PunyTMin = 1;
  PunyTMax = 26;
  PunySkew = 38;
  PunyDamp = 700;

// The Punycode arithmetic is that of 32-bit unsigned integers in the Go source. Here each value is held in an Int64
// and kept within 32 bits where the Go source would wrap or saturate.

function PunyAdapt(Delta, NumPoints: Int64; FirstTime: Boolean): Int64;
var
  K: Int64;
begin
  if FirstTime then Delta := Delta div PunyDamp
  else Delta := Delta shr 1;
  Delta := Delta + Delta div NumPoints;
  K := 0;
  while Delta > ((PunyBase - PunyTMin) * PunyTMax) shr 1 do begin
    Delta := Delta div (PunyBase - PunyTMin);
    Inc(K, PunyBase);
  end;
  Result := K + ((PunyBase - PunyTMin + 1) * Delta) div (Delta + PunySkew);
end;

function PunyThreshold(K, Bias: Int64): Int64;
begin
  if K <= Bias then Result := PunyTMin
  else if K >= Bias + PunyTMax then Result := PunyTMax
  else Result := K - Bias;
end;

function SaturatingAdd(A, B: Int64): Int64;
begin
  if A > MaxUInt32 - B then Result := MaxUInt32
  else Result := A + B;
end;

function PunyDigit(D: Int64): Byte;
begin
  if D < 26 then Result := Byte(D + 97)
  else Result := Byte(D + 22);
end;

// PunycodeEncode is the RFC 3492 encoding, for the canonical round trip and A-label length checks. It gives the
// number of bytes it wrote to Output.
function PunycodeEncode(const Cps: TRunes; var Output: TBytes): Int32;
var
  Count, I: Int32;
  BasicLength, H, N, Delta, Bias, M, Step, Q, K, T, C: Int64;

  procedure Append(B: Byte);
  begin
    if Count = Length(Output) then SetLength(Output, 2 * Count + 16);
    Output[Count] := B;
    Inc(Count);
  end;

begin
  Count := 0;
  for I := 0 to High(Cps) do begin
    if Cps[I] < RuneSelf then Append(Byte(Cps[I]));
  end;
  BasicLength := Count;
  H := BasicLength;
  if BasicLength > 0 then Append(Ord('-'));
  N := 128;
  Delta := 0;
  Bias := 72;
  while H < Length(Cps) do begin
    M := MaxUInt32;
    for I := 0 to High(Cps) do begin
      C := Cps[I];
      if (C >= N) and (C < M) then M := C;
    end;
    // No code point is at or above N only when the text is not code points at all, which no caller passes.
    Step := (M - N) * (H + 1);
    if Step > MaxUInt32 then Step := MaxUInt32;
    Delta := SaturatingAdd(Delta, Step);
    N := M;
    for I := 0 to High(Cps) do begin
      C := Cps[I];
      if C < N then Delta := SaturatingAdd(Delta, 1);
      if C = N then begin
        Q := Delta;
        K := PunyBase;
        while True do begin
          T := PunyThreshold(K, Bias);
          if Q < T then Break;
          Append(PunyDigit(T + (Q - T) mod (PunyBase - T)));
          Q := (Q - T) div (PunyBase - T);
          Inc(K, PunyBase);
        end;
        Append(PunyDigit(Q));
        Bias := PunyAdapt(Delta, H + 1, H = BasicLength);
        Delta := 0;
        Inc(H);
      end;
    end;
    Delta := (Delta + 1) and MaxUInt32;
    N := (N + 1) and MaxUInt32;
  end;
  Result := Count;
end;

// PunycodeDecode is the RFC 3492 decoding, enough to validate A-labels.
function PunycodeDecode(const S: TBytes; From, Stop: Int32; out Output: TRunes): Boolean;
var
  Basic, J, Index, Count, At: Int32;
  N, I, Bias, OldI, W, K, Digit, T, Next, Len: Int64;
  C: Byte;
begin
  Result := False;
  Output := nil;
  N := 128;
  I := 0;
  Bias := 72;
  Count := 0;
  SetLength(Output, Stop - From);
  // Basic is the number of bytes before the last "-", and not its index.
  Basic := LastIndexByte(S, From, Stop, Ord('-')) - From;
  if Basic < 0 then Basic := 0;
  for J := 0 to Basic - 1 do begin
    if S[From + J] >= $80 then begin
      Output := nil;
      Exit;
    end;
    Output[Count] := S[From + J];
    Inc(Count);
  end;
  Index := From;
  if Basic > 0 then Index := From + Basic + 1;
  while Index < Stop do begin
    OldI := I;
    W := 1;
    K := PunyBase;
    while True do begin
      if Index >= Stop then begin
        Output := nil;
        Exit;
      end;
      C := S[Index];
      Inc(Index);
      if (C >= Ord('0')) and (C <= Ord('9')) then Digit := Int64(C) - 22
      else if (C >= Ord('A')) and (C <= Ord('Z')) then Digit := Int64(C) - 65
      else if (C >= Ord('a')) and (C <= Ord('z')) then Digit := Int64(C) - 97
      else begin
        Output := nil;
        Exit;
      end;
      Next := I + Digit * W;
      if Next > MaxUInt32 then begin
        Output := nil;
        Exit;
      end;
      I := Next;
      T := PunyThreshold(K, Bias);
      if Digit < T then Break;
      W := W * (PunyBase - T);
      if W > MaxUInt32 then begin
        Output := nil;
        Exit;
      end;
      Inc(K, PunyBase);
    end;
    Len := Int64(Count) + 1;
    Bias := PunyAdapt(I - OldI, Len, OldI = 0);
    if N + I div Len > MaxUInt32 then begin
      Output := nil;
      Exit;
    end;
    N := N + I div Len;
    I := I mod Len;
    if (N > MaxRune) or ((N >= $D800) and (N <= $DFFF)) then begin
      Output := nil;
      Exit;
    end;
    // Each code point takes at least one byte of the text, so the output never outgrows the text.
    At := Int32(I);
    for J := Count downto At + 1 do Output[J] := Output[J - 1];
    Output[At] := Int32(N);
    Inc(Count);
    Inc(I);
  end;
  SetLength(Output, Count);
  Result := True;
end;

function IsArabicLike(R: Int32): Boolean;
begin
  Result := Idn.ArabicLike.Contains(R);
end;

type
  TBidi = (BidiL, BidiR, BidiAL, BidiAN, BidiEN, BidiNSM, BidiON);

// BidiClass approximates the Bidi classes of the RFC 5893 Bidi rule by script and general category.
function BidiClass(C: Int32): TBidi;
begin
  if Idn.NonspacingOrEnclosing.Contains(C) then Result := BidiNSM
  else if ((C >= $660) and (C <= $669)) or (C = $66B) or (C = $66C) then Result := BidiAN
  else if ((C >= $30) and (C <= $39)) or ((C >= $6F0) and (C <= $6F9)) then Result := BidiEN
  else if Idn.Hebrew.Contains(C) then Result := BidiR
  else if IsArabicLike(C) then Result := BidiAL
  else if Idn.LetterOrSpacingMark.Contains(C) then Result := BidiL
  else Result := BidiON;
end;

function BidiDomain(const Cps: TRunes): Boolean;
var
  I, C: Int32;
  Rtl, Strong: Boolean;
  Cls: TBidi;
begin
  Rtl := False;
  Strong := False;
  for I := 0 to High(Cps) do begin
    C := Cps[I];
    Rtl := Rtl or Idn.Hebrew.Contains(C) or IsArabicLike(C) or ((C >= $660) and (C <= $669)) or (C = $66B) or
      (C = $66C);
    Cls := BidiClass(C);
    Strong := Strong or (Cls = BidiR) or (Cls = BidiAL) or (Cls = BidiAN);
  end;
  Result := Rtl and Strong;
end;

function BidiLabelOK(const Cps: TRunes; IsBidiDomain: Boolean): Boolean;
var
  Classes: array of TBidi;
  Has: array[TBidi] of Boolean;
  Cls, Last: TBidi;
  I, LastIndex: Int32;
begin
  Result := True;
  if not IsBidiDomain then Exit;
  Result := False;
  for Cls := Low(TBidi) to High(TBidi) do Has[Cls] := False;
  Classes := nil;
  SetLength(Classes, Length(Cps));
  for I := 0 to High(Cps) do begin
    Cls := BidiClass(Cps[I]);
    Classes[I] := Cls;
    Has[Cls] := True;
  end;
  if Length(Classes) = 0 then Exit;
  LastIndex := Length(Classes) - 1;
  while (LastIndex > 0) and (Classes[LastIndex] = BidiNSM) do Dec(LastIndex);
  Last := Classes[LastIndex];
  case Classes[0] of
    BidiR, BidiAL:
      Result := (not Has[BidiL]) and ((Last = BidiR) or (Last = BidiAL) or (Last = BidiEN) or (Last = BidiAN)) and
        not (Has[BidiEN] and Has[BidiAN]);
    BidiL:
      Result := (not Has[BidiR]) and (not Has[BidiAL]) and (not Has[BidiAN]) and
        ((Last = BidiL) or (Last = BidiEN));
  end;
end;

function IsVirama(R: Int32): Boolean;
begin
  case R of
    $094D, $09CD, $0A4D, $0ACD, $0B4D, $0BCD, $0C4D, $0CCD, $0D3B, $0D3C, $0D4D, $0DCA, $0E3A,
    $0EBA, $0F84, $1039, $103A, $1714, $1734, $17D2, $1A60, $1B44, $1BAA, $1BAB, $1BF2, $1BF3,
    $2D7F, $A806, $A8C4, $A953, $A9C0, $AAF6, $ABED: Result := True;
  else
    Result := False;
  end;
end;

// ZwnjJoiningContext approximates (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D})
// with Arabic letters.
function ZwnjJoiningContext(const Cps: TRunes; I: Int32): Boolean;
var
  L, R: Int32;

  function IsJoiner(C: Int32): Boolean;
  begin
    Result := ((C >= $0620) and (C <= $064A)) or ((C >= $066E) and (C <= $06D3));
  end;

begin
  L := I - 1;
  while (L >= 0) and Idn.Nonspacing.Contains(Cps[L]) do Dec(L);
  R := I + 1;
  while (R < Length(Cps)) and Idn.Nonspacing.Contains(Cps[R]) do Inc(R);
  Result := (L >= 0) and (R < Length(Cps)) and IsJoiner(Cps[L]) and IsJoiner(Cps[R]);
end;

// IdnLabelOK checks the contextual and disallowed code points from RFC 5892 that the test suite exercises.
function IdnLabelOK(const Cps: TRunes): Boolean;
var
  I, J, C, N: Int32;
  Found: Boolean;

  function HasRange(Lo, Hi: Int32): Boolean;
  var
    K: Int32;
  begin
    for K := 0 to High(Cps) do begin
      if (Cps[K] >= Lo) and (Cps[K] <= Hi) then begin
        Result := True;
        Exit;
      end;
    end;
    Result := False;
  end;

begin
  Result := False;
  N := Length(Cps);
  if (N = 0) or (Cps[0] = Ord('-')) or (Cps[N - 1] = Ord('-')) then Exit;
  if (N >= 4) and (Cps[2] = Ord('-')) and (Cps[3] = Ord('-')) then Exit;
  if Idn.Mark.Contains(Cps[0]) then Exit;
  if HasRange($660, $669) and HasRange($6F0, $6F9) then Exit;
  for I := 0 to N - 1 do begin
    C := Cps[I];
    if (C = $302E) or (C = $302F) or (C = $0640) or (C = $07FA) or ((C >= $3031) and (C <= $3035)) or
      (C = $303B) then Exit
    else if C = $00B7 then begin
      if not ((I > 0) and (I < N - 1) and (Cps[I - 1] = Ord('l')) and (Cps[I + 1] = Ord('l'))) then Exit;
    end else if C = $0375 then begin
      if not ((I < N - 1) and Idn.Greek.Contains(Cps[I + 1])) then Exit;
    end else if (C = $05F3) or (C = $05F4) then begin
      if not ((I > 0) and Idn.Hebrew.Contains(Cps[I - 1])) then Exit;
    end else if C = $30FB then begin
      Found := False;
      for J := 0 to N - 1 do Found := Found or ((Cps[J] <> $30FB) and Idn.KanaOrHan.Contains(Cps[J]));
      if not Found then Exit;
    end else if C = $200D then begin
      if (I = 0) or not IsVirama(Cps[I - 1]) then Exit;
    end else if C = $200C then begin
      if ((I = 0) or not IsVirama(Cps[I - 1])) and not ZwnjJoiningContext(Cps, I) then Exit;
    end;
  end;
  Result := True;
end;

function PunycodeLabelOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  Decoded: TRunes;
  Encoded: TBytes;
  I, Count: Int32;
  Ascii: Boolean;
  C: Byte;
begin
  Result := False;
  Decoded := nil;
  if not PunycodeDecode(S, From, Stop, Decoded) or (Length(Decoded) = 0) then Exit;
  Ascii := True;
  for I := 0 to High(Decoded) do Ascii := Ascii and (Decoded[I] < RuneSelf);
  if Ascii then Exit;
  // The encoding must be canonical: encoding the U-label again gives the same A-label.
  Encoded := nil;
  Count := PunycodeEncode(Decoded, Encoded);
  if Count <> Stop - From then Exit;
  for I := 0 to Count - 1 do begin
    C := S[From + I];
    if (C >= Ord('A')) and (C <= Ord('Z')) then C := C + (Ord('a') - Ord('A'));
    if Encoded[I] <> C then Exit;
  end;
  Result := IdnLabelOK(Decoded) and not Disallowed(Decoded) and BidiLabelOK(Decoded, BidiDomain(Decoded));
end;

function IsLabelSeparator(R: Int32): Boolean;
begin
  // Full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
  Result := (R = Ord('.')) or (R = $3002) or (R = $FF0E) or (R = $FF61);
end;

function IDNHostnameOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  LabelFrom, LabelStop: array of Int32;
  UnicodeLabels: array of TRunes;
  Decoded, WithoutJoiners: TRunes;
  Encoded: TBytes;
  Count, I, J, K, R, Size, Start, LFrom, LStop, AsciiLength, ALabelLength: Int32;
  IsBidi: Boolean;

  // AddLabel keeps a label, the bytes S[A .. B-1].
  procedure AddLabel(A, B: Int32);
  begin
    if Count = Length(LabelFrom) then begin
      SetLength(LabelFrom, 2 * Count + 8);
      SetLength(LabelStop, 2 * Count + 8);
    end;
    LabelFrom[Count] := A;
    LabelStop[Count] := B;
    Inc(Count);
  end;

begin
  Result := False;
  if From >= Stop then Exit;
  // The labels: the host name split at every label separator, keeping empty labels.
  LabelFrom := nil;
  LabelStop := nil;
  Count := 0;
  Start := From;
  I := From;
  while I < Stop do begin
    R := DecodeRune(S, I, Stop, Size);
    if IsLabelSeparator(R) then begin
      AddLabel(Start, I);
      Start := I + Size;
    end;
    Inc(I, Size);
  end;
  AddLabel(Start, Stop);
  UnicodeLabels := nil;
  SetLength(UnicodeLabels, Count);
  IsBidi := False;
  for I := 0 to Count - 1 do begin
    LFrom := LabelFrom[I];
    LStop := LabelStop[I];
    if StartsWithXN(S, LFrom, LStop) and PunycodeDecode(S, LFrom + 4, LStop, Decoded) then UnicodeLabels[I] := Decoded
    else UnicodeLabels[I] := CodePoints(S, LFrom, LStop);
    IsBidi := IsBidi or BidiDomain(UnicodeLabels[I]);
  end;
  AsciiLength := 0;
  Encoded := nil;
  for I := 0 to Count - 1 do begin
    LFrom := LabelFrom[I];
    LStop := LabelStop[I];
    if LFrom = LStop then Exit;
    if IsASCIIRange(S, LFrom, LStop) then begin
      if not HostnameLabelOK(S, LFrom, LStop) then Exit;
      if StartsWithXN(S, LFrom, LStop) and not PunycodeLabelOK(S, LFrom + 4, LStop) then Exit;
      if not BidiLabelOK(UnicodeLabels[I], IsBidi) then Exit;
      Inc(AsciiLength, LStop - LFrom + 1);
    end else begin
      // A label that is not ASCII is never an A-label that decodes, so its code points are those of its text.
      if not IdnLabelOK(UnicodeLabels[I]) then Exit;
      WithoutJoiners := nil;
      SetLength(WithoutJoiners, Length(UnicodeLabels[I]));
      K := 0;
      for J := 0 to High(UnicodeLabels[I]) do begin
        R := UnicodeLabels[I][J];
        if (R <> $200C) and (R <> $200D) then begin
          WithoutJoiners[K] := R;
          Inc(K);
        end;
      end;
      SetLength(WithoutJoiners, K);
      for J := 0 to K - 1 do begin
        if Idn.Invisible.Contains(WithoutJoiners[J]) then Exit;
      end;
      if Disallowed(WithoutJoiners) then Exit;
      if not BidiLabelOK(UnicodeLabels[I], IsBidi) then Exit;
      ALabelLength := 4 + PunycodeEncode(UnicodeLabels[I], Encoded);
      if ALabelLength > 63 then Exit;
      Inc(AsciiLength, ALabelLength + 1);
    end;
  end;
  Result := AsciiLength - 1 <= 253;
end;

// ---------------------------------------------------------------------------------------------------------------------
// E-mail addresses

const
  // The ASCII characters of an e-mail atom: letters, digits and !#$%&'*+/=?^_`{|}~-
  EmailAtomSet = [Ord('0')..Ord('9'), Ord('A')..Ord('Z'), Ord('a')..Ord('z'), Ord('!'), Ord('#'), Ord('$'),
    Ord('%'), Ord('&'), Ord(''''), Ord('*'), Ord('+'), Ord('/'), Ord('='), Ord('?'), Ord('^'), Ord('_'), Ord('`'),
    Ord('{'), Ord('|'), Ord('}'), Ord('~'), Ord('-')];

// EmailLocalOK reads the local part of an e-mail address: atoms separated by single dots, or a quoted string. An atom
// of an internationalized local part (RFC 6531 extends atext of RFC 5322) is taken here to be a letter, a mark or a
// number of any script, or one of the ASCII atom characters, as in the other ports of the evaluator. The Go source
// reads the same grammar with two regular expressions.
function EmailLocalOK(const S: TBytes; From, Stop: Int32; IdnLocal: Boolean): Boolean;
var
  I, AtomLength, R, Size: Int32;
  C: Byte;
begin
  Result := False;
  if From >= Stop then Exit;
  if S[From] = Ord('"') then begin
    // A quote, then characters that are not a quote, a backslash or a line break, or a backslash and any character
    // but a line feed, then a quote that ends the text.
    I := From + 1;
    while I < Stop do begin
      C := S[I];
      if C = Ord('"') then begin
        Result := I = Stop - 1;
        Exit;
      end else if C = Ord('\') then begin
        if (I + 1 >= Stop) or (S[I + 1] = 10) then Exit;
        DecodeRune(S, I + 1, Stop, Size);
        Inc(I, 1 + Size);
      end else if (C = 13) or (C = 10) then Exit
      else Inc(I);
    end;
    Exit;
  end;
  AtomLength := 0;
  I := From;
  while I < Stop do begin
    C := S[I];
    if C = Ord('.') then begin
      if AtomLength = 0 then Exit;
      AtomLength := 0;
      Inc(I);
    end else if C < RuneSelf then begin
      if not (C in EmailAtomSet) then Exit;
      Inc(AtomLength);
      Inc(I);
    end else begin
      if not IdnLocal then Exit;
      R := DecodeRune(S, I, Stop, Size);
      if not Idn.LetterMarkNumber.Contains(R) then Exit;
      Inc(AtomLength);
      Inc(I, Size);
    end;
  end;
  Result := AtomLength <> 0;
end;

function EmailOK(const S: TBytes; From, Stop: Int32; IdnAddress: Boolean): Boolean;
var
  At, DomainFrom, InnerFrom, InnerStop: Int32;
begin
  Result := False;
  At := LastIndexByte(S, From, Stop, Ord('@'));
  if At <= From then Exit;
  DomainFrom := At + 1;
  if not EmailLocalOK(S, From, At, IdnAddress) then Exit;
  if (Stop - DomainFrom >= 2) and (S[DomainFrom] = Ord('[')) and (S[Stop - 1] = Ord(']')) then begin
    InnerFrom := DomainFrom + 1;
    InnerStop := Stop - 1;
    if (InnerStop - InnerFrom >= 5) and ((S[InnerFrom] or $20) = Ord('i')) and
      ((S[InnerFrom + 1] or $20) = Ord('p')) and ((S[InnerFrom + 2] or $20) = Ord('v')) and
      (S[InnerFrom + 3] = Ord('6')) and (S[InnerFrom + 4] = Ord(':')) then begin
      Result := IPv6OK(S, InnerFrom + 5, InnerStop);
      Exit;
    end;
    Result := IPv4OK(S, InnerFrom, InnerStop);
    Exit;
  end;
  if IdnAddress then Result := IDNHostnameOK(S, DomainFrom, Stop)
  else Result := HostnameOK(S, DomainFrom, Stop);
end;

// ---------------------------------------------------------------------------------------------------------------------
// The RFC 3986 (URI) and RFC 3987 (IRI) grammars, read directly. The Go source builds them as regular expressions
// (uriRegexpSource). The grammar is deterministic once the fragment and the query are cut off at the first "#" and
// the first "?", so each part is one pass.

const
  UriSubDelims = [Ord('!'), Ord('$'), Ord('&'), Ord(''''), Ord('('), Ord(')'), Ord('*'), Ord('+'), Ord(','),
    Ord(';'), Ord('=')];
  UriUnreserved = [Ord('0')..Ord('9'), Ord('A')..Ord('Z'), Ord('a')..Ord('z'), Ord('-'), Ord('.'), Ord('_'),
    Ord('~')];
  UriRegName = UriUnreserved + UriSubDelims;
  UriUserinfo = UriRegName + [Ord(':')];
  UriPChar = UriUserinfo + [Ord('@')];
  UriSegmentNC = UriRegName + [Ord('@')];
  // The characters of path segments and the "/" between them.
  UriQueryPath = UriPChar + [Ord('/')];
  UriQuery = UriQueryPath + [Ord('?')];

function IsUcsChar(C: Int32): Boolean;
begin
  Result := ((C >= $A0) and (C <= $D7FF)) or ((C >= $F900) and (C <= $FDCF)) or ((C >= $FDF0) and (C <= $FFEF)) or
    ((C >= $10000) and (C <= $EFFFD));
end;

function IsIPrivate(C: Int32): Boolean;
begin
  Result := ((C >= $E000) and (C <= $F8FF)) or ((C >= $F0000) and (C <= $FFFFD)) or
    ((C >= $100000) and (C <= $10FFFD));
end;

// UriCharsOK reports whether S[From .. Stop-1] is characters of the set, percent-encoded triplets and, for an IRI,
// the characters beyond ASCII that RFC 3987 allows (with the private ranges too, for a query).
function UriCharsOK(const S: TBytes; From, Stop: Int32; const Allowed: TByteSet; Iri, PrivateUse: Boolean): Boolean;
var
  I, R, Size: Int32;
  C: Byte;
begin
  Result := False;
  I := From;
  while I < Stop do begin
    C := S[I];
    if C = Ord('%') then begin
      if not ((I + 2 < Stop) and IsHexDigit(S[I + 1]) and IsHexDigit(S[I + 2])) then Exit;
      Inc(I, 3);
    end else if C < RuneSelf then begin
      if not (C in Allowed) then Exit;
      Inc(I);
    end else begin
      if not Iri then Exit;
      R := DecodeRune(S, I, Stop, Size);
      if not (IsUcsChar(R) or (PrivateUse and IsIPrivate(R))) then Exit;
      Inc(I, Size);
    end;
  end;
  Result := True;
end;

// UriAuthorityOK reads an authority in S[From .. Stop-1]: optional user information, a host (an IP literal, or a
// registered name, of which an IPv4 address is one) and an optional port.
function UriAuthorityOK(const S: TBytes; From, Stop: Int32; Iri: Boolean): Boolean;
var
  Host, Port, I, Close, Dot: Int32;
  C: Byte;
begin
  Result := False;
  Host := From;
  for I := From to Stop - 1 do begin
    if S[I] = Ord('@') then begin
      // The host and the port have no "@", so the user information ends at the only one.
      if Host > From then Exit;
      if not UriCharsOK(S, From, I, UriUserinfo, Iri, False) then Exit;
      Host := I + 1;
    end;
  end;
  Port := Stop;
  if (Host < Stop) and (S[Host] = Ord('[')) then begin
    Close := IndexByte(S, Host + 1, Stop, Ord(']'));
    if Close < 0 then Exit;
    if (Close > Host + 1) and (S[Host + 1] = Ord('v')) then begin
      // "v" hex digits "." then unreserved characters, sub-delimiters or ":".
      Dot := IndexByte(S, Host + 1, Close, Ord('.'));
      if Dot < Host + 3 then Exit;
      for I := Host + 2 to Dot - 1 do begin
        if not IsHexDigit(S[I]) then Exit;
      end;
      if Dot + 1 >= Close then Exit;
      for I := Dot + 1 to Close - 1 do begin
        C := S[I];
        if not ((C < RuneSelf) and (C in UriUserinfo)) then Exit;
      end;
    end else if not IPv6OK(S, Host + 1, Close) then Exit;
    Port := Close + 1;
  end else begin
    I := IndexByte(S, Host, Stop, Ord(':'));
    if I >= 0 then Port := I;
    if not UriCharsOK(S, Host, Port, UriRegName, Iri, False) then Exit;
  end;
  if Port < Stop then begin
    if S[Port] <> Ord(':') then Exit;
    for I := Port + 1 to Stop - 1 do begin
      if not IsASCIIDigit(S[I]) then Exit;
    end;
  end;
  Result := True;
end;

// UriHierOK reads the part of a URI between the scheme and the query in S[From .. Stop-1]: "//" authority and a
// path, or a path. A relative reference's first segment has no ":" (NoScheme).
function UriHierOK(const S: TBytes; From, Stop: Int32; Iri, NoScheme: Boolean): Boolean;
var
  SegmentStop: Int32;
begin
  Result := True;
  if From = Stop then Exit;
  Result := False;
  if (Stop - From >= 2) and (S[From] = Ord('/')) and (S[From + 1] = Ord('/')) then begin
    SegmentStop := IndexByte(S, From + 2, Stop, Ord('/'));
    if SegmentStop < 0 then SegmentStop := Stop;
    if not UriAuthorityOK(S, From + 2, SegmentStop, Iri) then Exit;
    Result := UriCharsOK(S, SegmentStop, Stop, UriQueryPath, Iri, False);
    Exit;
  end;
  if S[From] = Ord('/') then begin
    // An absolute path: "/" alone, or a first segment that is not empty.
    if From + 1 = Stop then begin
      Result := True;
      Exit;
    end;
    if S[From + 1] = Ord('/') then Exit;
    Result := UriCharsOK(S, From + 1, Stop, UriQueryPath, Iri, False);
    Exit;
  end;
  // A path with no root: a first segment that is not empty.
  SegmentStop := IndexByte(S, From, Stop, Ord('/'));
  if SegmentStop < 0 then SegmentStop := Stop;
  if NoScheme then begin
    if not UriCharsOK(S, From, SegmentStop, UriSegmentNC, Iri, False) then Exit;
  end else if not UriCharsOK(S, From, SegmentStop, UriPChar, Iri, False) then Exit;
  Result := UriCharsOK(S, SegmentStop, Stop, UriQueryPath, Iri, False);
end;

// UriOK reads a URI or an IRI, and with Reference a relative reference too.
function UriOK(const S: TBytes; From, Stop: Int32; Iri, Reference: Boolean): Boolean;
var
  Limit, Hash, PathStop, Question, Colon, I: Int32;
  C: Byte;
begin
  Result := False;
  Limit := Stop;
  Hash := IndexByte(S, From, Stop, Ord('#'));
  if Hash >= 0 then begin
    if not UriCharsOK(S, Hash + 1, Limit, UriQuery, Iri, False) then Exit;
    Limit := Hash;
  end;
  PathStop := Limit;
  Question := IndexByte(S, From, Limit, Ord('?'));
  if Question >= 0 then begin
    if not UriCharsOK(S, Question + 1, Limit, UriQuery, Iri, Iri) then Exit;
    PathStop := Question;
  end;
  // The scheme: a letter, then letters, digits, "+", "-" and ".", then ":".
  Colon := -1;
  if (PathStop > From) and IsASCIILetter(S[From]) then begin
    for I := From + 1 to PathStop - 1 do begin
      C := S[I];
      if C = Ord(':') then begin
        Colon := I;
        Break;
      end;
      if not (IsASCIILetter(C) or IsASCIIDigit(C) or (C = Ord('+')) or (C = Ord('-')) or (C = Ord('.'))) then Break;
    end;
  end;
  if (Colon >= 0) and UriHierOK(S, Colon + 1, PathStop, Iri, False) then begin
    Result := True;
    Exit;
  end;
  Result := Reference and UriHierOK(S, From, PathStop, Iri, True);
end;

// UriTemplateVar reads one variable of a URI template expression at I: name characters (letters, digits, "_" and
// percent-encoded triplets) with single dots between them, then an optional prefix length or "*". The next offset,
// or -1.
function UriTemplateVar(const S: TBytes; I, Stop: Int32): Int32;
var
  Need: Boolean;
  Digits: Int32;
begin
  Result := -1;
  Need := True;
  while True do begin
    if (I < Stop) and (S[I] = Ord('%')) then begin
      if not ((I + 2 < Stop) and IsHexDigit(S[I + 1]) and IsHexDigit(S[I + 2])) then Exit;
      Inc(I, 3);
    end else if (I < Stop) and (IsASCIIAlphanumeric(S[I]) or (S[I] = Ord('_'))) then Inc(I)
    else if Need then Exit
    else Break;
    Need := False;
    if (I < Stop) and (S[I] = Ord('.')) then begin
      Inc(I);
      Need := True;
    end;
  end;
  if (I < Stop) and (S[I] = Ord('*')) then begin
    Result := I + 1;
    Exit;
  end;
  if (I < Stop) and (S[I] = Ord(':')) then begin
    if not ((I + 1 < Stop) and (S[I + 1] >= Ord('1')) and (S[I + 1] <= Ord('9'))) then Exit;
    Inc(I, 2);
    Digits := 0;
    while (I < Stop) and IsASCIIDigit(S[I]) and (Digits < 3) do begin
      Inc(I);
      Inc(Digits);
    end;
  end;
  Result := I;
end;

const
  UriTemplateOperators = [Ord('+'), Ord('#'), Ord('.'), Ord('/'), Ord(';'), Ord('?'), Ord('&'), Ord('='), Ord(','),
    Ord('!'), Ord('@'), Ord('|')];
  // The characters a URI template has no literal for. "{" opens an expression.
  UriTemplateNotLiteral = [0..$20, Ord('"'), Ord(''''), Ord('<'), Ord('>'), Ord('\'), Ord('^'), Ord('`'), Ord('|'),
    Ord('}')];

// UriTemplateOK reads an RFC 6570 URI template: literal characters and expressions in braces.
function UriTemplateOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
  C: Byte;
begin
  Result := False;
  I := From;
  while I < Stop do begin
    C := S[I];
    if C = Ord('{') then begin
      Inc(I);
      if (I < Stop) and (S[I] in UriTemplateOperators) then Inc(I);
      while True do begin
        I := UriTemplateVar(S, I, Stop);
        if (I < 0) or (I >= Stop) then Exit;
        if S[I] = Ord(',') then Inc(I)
        else if S[I] = Ord('}') then begin
          Inc(I);
          Break;
        end else Exit;
      end;
    end else if C in UriTemplateNotLiteral then Exit
    else Inc(I);
  end;
  Result := True;
end;

// ---------------------------------------------------------------------------------------------------------------------
// JSON pointers

// JSONPointerOK reads an RFC 6901 JSON pointer: segments that each start with "/", where "~" is followed by 0 or 1.
function JSONPointerOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  Result := False;
  if (From < Stop) and (S[From] <> Ord('/')) then Exit;
  for I := From to Stop - 1 do begin
    if S[I] = Ord('~') then begin
      if (I + 1 >= Stop) or ((S[I + 1] <> Ord('0')) and (S[I + 1] <> Ord('1'))) then Exit;
    end;
  end;
  Result := True;
end;

// RelativeJSONPointerOK reads a non-negative integer without leading zeros, then "#" or a JSON pointer.
function RelativeJSONPointerOK(const S: TBytes; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  Result := False;
  I := From;
  while (I < Stop) and IsASCIIDigit(S[I]) do Inc(I);
  if (I = From) or ((I > From + 1) and (S[From] = Ord('0'))) then Exit;
  Result := ((Stop - I = 1) and (S[I] = Ord('#'))) or JSONPointerOK(S, I, Stop);
end;

// ---------------------------------------------------------------------------------------------------------------------
// The checks by format

function IsDate(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := DateOK(Text, Start, Start + Len);
end;

function IsTime(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := TimeOK(Text, Start, Start + Len);
end;

function IsDateTime(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := DateTimeOK(Text, Start, Start + Len);
end;

function IsDuration(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := DurationOK(Text, Start, Start + Len);
end;

function IsUUID(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UUIDOK(Text, Start, Start + Len);
end;

function IsIPv4(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := IPv4OK(Text, Start, Start + Len);
end;

function IsIPv6(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := IPv6OK(Text, Start, Start + Len);
end;

function IsLegacyHostname(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := LegacyHostnameOK(Text, Start, Start + Len);
end;

function IsHostname(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := HostnameOK(Text, Start, Start + Len);
end;

function IsIDNHostname(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := IDNHostnameOK(Text, Start, Start + Len);
end;

function IsEmail(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := EmailOK(Text, Start, Start + Len, False);
end;

function IsIDNEmail(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := EmailOK(Text, Start, Start + Len, True);
end;

function IsURI(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UriOK(Text, Start, Start + Len, False, False);
end;

function IsURIReference(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UriOK(Text, Start, Start + Len, False, True);
end;

function IsIRI(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UriOK(Text, Start, Start + Len, True, False);
end;

function IsIRIReference(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UriOK(Text, Start, Start + Len, True, True);
end;

function IsURITemplate(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := UriTemplateOK(Text, Start, Start + Len);
end;

function IsJSONPointer(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := JSONPointerOK(Text, Start, Start + Len);
end;

function IsRelativeJSONPointer(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := RelativeJSONPointerOK(Text, Start, Start + Len);
end;

function IsRegex(const Text: TBytes; Start, Len: Int32): Boolean;
var
  Pattern: UTF8String;
  I: Int32;
begin
  // The pattern is kept as text of its own for the parser to read.
  Pattern := '';
  SetLength(Pattern, Len);
  for I := 0 to Len - 1 do Pattern[I + 1] := AnsiChar(Text[Start + I]);
  Result := EcmaRegexValid(Pattern);
end;

function FormatKindValidator(Kind: TFormatKind; LegacyHostname: Boolean): TFormatCheck;
begin
  case Kind of
    FormatKindDate: Result := IsDate;
    FormatKindTime: Result := IsTime;
    FormatKindDateTime: Result := IsDateTime;
    FormatKindDuration: Result := IsDuration;
    FormatKindUUID: Result := IsUUID;
    FormatKindIPv4: Result := IsIPv4;
    FormatKindIPv6: Result := IsIPv6;
    FormatKindHostname:
      if LegacyHostname then Result := IsLegacyHostname
      else Result := IsHostname;
    FormatKindIDNHostname: Result := IsIDNHostname;
    FormatKindEmail: Result := IsEmail;
    FormatKindIDNEmail: Result := IsIDNEmail;
    FormatKindURI: Result := IsURI;
    FormatKindURIReference: Result := IsURIReference;
    FormatKindIRI: Result := IsIRI;
    FormatKindIRIReference: Result := IsIRIReference;
    FormatKindURITemplate: Result := IsURITemplate;
    FormatKindJSONPointer: Result := IsJSONPointer;
    FormatKindRelativeJSONPointer: Result := IsRelativeJSONPointer;
    FormatKindRegex: Result := IsRegex;
  else
    Result := nil;
  end;
end;

function FormatCheckString(Kind: TFormatKind; const Text: TBytes; Start, Len: Int32;
  LegacyHostname: Boolean): Boolean;
var
  Check: TFormatCheck;
begin
  Check := FormatKindValidator(Kind, LegacyHostname);
  if Assigned(Check) then Result := Check(Text, Start, Len)
  else Result := True;
end;

function FormatValidator(const Name: UTF8String): TFormatCheck;
begin
  Result := FormatKindValidator(FormatKindOf(Name, FormatDialectDraft202012), False);
end;

// ---------------------------------------------------------------------------------------------------------------------
// Content

function Base64Value(C: Byte): Int32;
begin
  if (C >= Ord('A')) and (C <= Ord('Z')) then Result := C - Ord('A')
  else if (C >= Ord('a')) and (C <= Ord('z')) then Result := C - Ord('a') + 26
  else if (C >= Ord('0')) and (C <= Ord('9')) then Result := C - Ord('0') + 52
  else if C = Ord('+') then Result := 62
  else if C = Ord('/') then Result := 63
  else Result := -1;
end;

function Base64Decode(const Text: TBytes; Start, Len: Int32; var Output: TBytes; out OutputLen: Int32): Boolean;
var
  I, J, Pad, Acc, V: Int32;
begin
  Result := False;
  OutputLen := 0;
  if Len mod 4 <> 0 then Exit;
  if Length(Output) < (Len div 4) * 3 then SetLength(Output, (Len div 4) * 3);
  I := Start;
  while I < Start + Len do begin
    Pad := 0;
    while (Pad < 4) and (Text[I + 3 - Pad] = Ord('=')) do Inc(Pad);
    if (Pad > 2) or ((Pad > 0) and (I + 4 <> Start + Len)) then Exit;
    Acc := 0;
    for J := 0 to 3 - Pad do begin
      V := Base64Value(Text[I + J]);
      if V < 0 then Exit;
      Acc := (Acc shl 6) or V;
    end;
    Acc := Acc shl (6 * Pad);
    Output[OutputLen] := Byte((Acc shr 16) and $FF);
    if Pad < 2 then Output[OutputLen + 1] := Byte((Acc shr 8) and $FF);
    if Pad < 1 then Output[OutputLen + 2] := Byte(Acc and $FF);
    Inc(OutputLen, 3 - Pad);
    Inc(I, 4);
  end;
  Result := True;
end;

initialization
  BuildIdnTables;

end.
