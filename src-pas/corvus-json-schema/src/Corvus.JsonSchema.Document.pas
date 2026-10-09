unit Corvus.JsonSchema.Document;

{$I corvus.inc}

{ JSON text parsed for evaluation: the UTF-8 text and one flat array of values (a tape) that the evaluator reads in
  place, with no object per value. A port of document.go of the Go module. }

interface

uses
  SysUtils;

const
  { MaxDepth is the deepest nesting of arrays and objects a document may have. }
  MaxDepth = 1000;

  { The kinds of a value. They are the evaluator's type bits, so a type test is one mask operation. }
  KindNull = 1;
  KindBool = 2;
  KindObject = 4;
  KindArray = 8;
  KindNumber = 16;
  KindString = 32;

  { Number representations (the flags of a number). }
  { NumInt is an integer that fits an Int64. }
  NumInt = 0;
  { NumUint is an integer in [2^63, 2^64), held as a UInt64. }
  NumUint = 1;
  { NumFloat is anything else, held as the bits of a Double. }
  NumFloat = 2;

  { String flags. }
  { StrText says the string's bytes are in TDocument.Text (it had escapes), not in TDocument.Source. }
  StrText = 1;
  { StrWide says the string has bytes outside ASCII. }
  StrWide = 2;

type
  { Bytes are SysUtils.TBytes throughout, so that a caller's own arrays are the evaluator's without a copy. }
  TUInt64Array = array of UInt64;
  TUInt32Array = array of UInt32;

  { TDocument is JSON text parsed for evaluation. It holds the UTF-8 text and one flat array of values (a tape) that
    the evaluator reads in place, with no object per value. Strings stay in the text where they have no escapes.

    A document is not changed once it is parsed, and is safe to read from several threads. Parsing is strict RFC
    8259. Anything but whitespace after the value, invalid UTF-8, lone surrogates in \u escapes, numbers out of the
    range of a Double and nesting deeper than MaxDepth are errors. Of duplicate property names, the last value is
    kept, at the position of the first. }
  TDocument = record
    { Two words per value. The first is the header: the kind in bits 0 to 7, flags in bits 8 to 15 and a 32-bit
      field in the high half (a string's byte length, a number's offset in the source, a container's count). The
      second is the data: a string's offset, a number's bits, a container's first child, a boolean's 0 or 1. The
      children of a container are consecutive. An object's children are key and value pairs. }
    Tape: TUInt64Array;
    Source: TBytes;
    { The unescaped strings. }
    Text: TBytes;
    Root: Int32;
  end;
  PDocument = ^TDocument;

  { TParseError reports text that is not valid JSON. }
  TParseError = record
    { Message says what was wrong. }
    Message: UTF8String;
    { Offset is the byte offset at which the error was found. }
    Offset: Int32;
  end;

  { TParser builds a document. Its buffers are reused from one parse to the next. Each array is as long as it has
    grown, and the count beside it says how much of it is in use. }
  TParser = record
    B: TBytes;
    BLen: Int32;
    I: Int32;
    { Checking syntax only. No document is built and numbers are not converted. }
    Validating: Boolean;
    { Finished children of closed containers, each container's consecutive (two words per node). }
    Nodes: TUInt64Array;
    NodesLen: Int32;
    { The values of the open containers, innermost last. A container's run moves to Nodes when it closes. }
    Scratch: TUInt64Array;
    ScratchLen: Int32;
    { The scratch index (in nodes) at which each open container's children start, with the object flag in bit 31. }
    Frames: TUInt32Array;
    FramesLen: Int32;
    Text: TBytes;
    TextLen: Int32;
    { Scratch for finding duplicate keys in large objects. }
    Hashes: TUInt64Array;
    { Scratch for rebuilding an object with duplicate keys. }
    Pairs: TUInt64Array;

    ErrMessage: UTF8String;
    ErrOffset: Int32;
  end;

{ ParseDocument parses UTF-8 JSON text into D. The document keeps the array. Do not modify it afterwards. False,
  with the error, when the text is not JSON. }
function ParseDocument(const Json: TBytes; out D: TDocument; out Error: TParseError): Boolean;
{ ParseDocumentString parses JSON text. }
function ParseDocumentString(const Json: UTF8String; out D: TDocument; out Error: TParseError): Boolean;
{ ParseErrorText is the error as one line of text. }
function ParseErrorText(const Error: TParseError): UTF8String;
{ DocumentToJson returns the document as compact JSON text, with numbers as they were written. }
function DocumentToJson(const D: TDocument): UTF8String;
{ BytesOf is the bytes of a string. }
function BytesOf(const S: UTF8String): TBytes;

{ Reading values (the evaluator's side). A value is the index of its node. }

function DocKind(const D: TDocument; N: Int32): Byte; inline;
function DocFlags(const D: TDocument; N: Int32): Byte; inline;
{ DocCount is a container's item or property count, or a string's byte length. }
function DocCount(const D: TDocument; N: Int32): Int32; inline;
function DocData(const D: TDocument; N: Int32): UInt64; inline;
{ DocFirst is a container's first child. }
function DocFirst(const D: TDocument; N: Int32): Int32; inline;
function DocBoolean(const D: TDocument; N: Int32): Boolean; inline;
{ DocStrInText says a string's bytes are in D.Text (from DocData, for DocCount bytes), not in D.Source. A caller
  reads the bytes from whichever array that is: a string is never copied to be read. }
function DocStrInText(const D: TDocument; N: Int32): Boolean; inline;
{ DocStrOffset is where a string's bytes start, in D.Text or D.Source. }
function DocStrOffset(const D: TDocument; N: Int32): Int32; inline;
function DocStrASCII(const D: TDocument; N: Int32): Boolean; inline;
{ DocStrEquals says whether a string value is these bytes. }
function DocStrEquals(const D: TDocument; N: Int32; const Name: TBytes; Start, Len: Int32): Boolean;
{ DocStrCopy is a copy of a string value's bytes, for the places that keep one. }
function DocStrCopy(const D: TDocument; N: Int32): UTF8String;
{ DocProperty is the value of an object's property, or -1. }
function DocProperty(const D: TDocument; AObject: Int32; const Name: UTF8String): Int32;
{ DocFloat is a number's value as a Double. }
function DocFloat(const D: TDocument; N: Int32): Double;
{ DocNumberStart and DocNumberEnd bound a number's text as written, in D.Source. }
function DocNumberStart(const D: TDocument; N: Int32): Int32; inline;
function DocNumberEnd(const D: TDocument; N: Int32): Int32;
{ NumberEnd is the end of the number whose text starts at J (already validated). }
function NumberEnd(const B: TBytes; J: Int32): Int32;

{ The parser, for the validator's text entry points, which keep one and parse into reused arrays. }

{ ParserIsValid reports whether B[0 .. Len-1] is one valid JSON value. It allocates nothing once the buffers have
  grown. }
function ParserIsValid(var P: TParser; const B: TBytes; Len: Int32): Boolean;
{ ParserParseInto parses B[0 .. Len-1] into D, reusing its arrays and the parser's (no allocation once they have
  grown). The finished values are written straight into the document's tape. The document is valid until the next
  parse into it. }
function ParserParseInto(var P: TParser; var D: TDocument; const B: TBytes; Len: Int32): Boolean;
{ ParserError is the error of the parse that has just failed. }
function ParserError(const P: TParser): TParseError;
{ ParserRetainsTooMuch says the parser's buffers have grown beyond what is worth keeping between parses. }
function ParserRetainsTooMuch(const P: TParser): Boolean;

{ Helpers shared with the rest of the evaluator. }

{ LoadLE64 is the eight bytes at B[J .. J+7] as a little-endian word. The caller has checked that they exist: the
  read is one load, and an index beyond the array raises ERangeError like any other. }
function LoadLE64(const B: TBytes; J: Int32): UInt64; inline;
{ LoadLE32 is the four bytes at B[J .. J+3] as a little-endian word. }
function LoadLE32(const B: TBytes; J: Int32): UInt32; inline;
{ Utf8Valid says whether B[Start .. Stop-1] is valid UTF-8. }
function Utf8Valid(const B: TBytes; Start, Stop: Int32): Boolean;
{ BytesEqual says whether two runs of bytes of one length are the same. }
function BytesEqual(const A: TBytes; AStart: Int32; const B: TBytes; BStart, Len: Int32): Boolean;
{ StrHash hashes a string, eight bytes at a time. }
function StrHash(const B: TBytes; Start, Len: Int32): UInt64;
{ SortUInt64 sorts A[0 .. Count-1] in increasing order. }
procedure SortUInt64(var A: TUInt64Array; Count: Int32);

implementation

uses
  Corvus.JsonSchema.Numbers;

const
  ObjectFrame = UInt32($80000000);

  HashK = UInt64($9E3779B97F4A7C15);

  SwarOnes = UInt64($0101010101010101);
  SwarHighs = UInt64($8080808080808080);
  { SwarOnes times a space, a quote and a backslash. }
  SwarSpaces = UInt64($2020202020202020);
  SwarQuotes = UInt64($2222222222222222);
  SwarBackslashes = UInt64($5C5C5C5C5C5C5C5C);

  { The buffers a parser keeps between parses, in bytes, beyond which a pooled parser is dropped. }
  RetainedLimit = 1 shl 20;

  Hex: array[0..15] of Byte = (48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102);

  Powers: array[0..22] of Double = (
    1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
    1e20, 1e21, 1e22);

var
  { Scan is, by byte: 0 for plain ASCII, 1 for a quote, backslash or control character, 2 for a non-ASCII byte. }
  Scan: array[0..255] of Byte;

{ --------------------------------------------------------------------------------------------------------------------
  Helpers }

function BytesOf(const S: UTF8String): TBytes;
var
  N: Int32;
begin
  Result := nil;
  N := Length(S);
  SetLength(Result, N);
  if N > 0 then
    Move(S[1], Result[0], N);
end;

function LoadLE32(const B: TBytes; J: Int32): UInt32; inline;
begin
  if B[J + 3] = 0 then ;
  Move(B[J], Result, 4);
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

function LoadLE64(const B: TBytes; J: Int32): UInt64; inline;
begin
  { Indexing the last byte checks the whole run: the first is checked by the index below. }
  if B[J + 7] = 0 then ;
  Move(B[J], Result, 8);
  {$IFDEF ENDIAN_BIG}
  Result := SwapEndian(Result);
  {$ENDIF}
end;

function BytesEqual(const A: TBytes; AStart: Int32; const B: TBytes; BStart, Len: Int32): Boolean;
var
  K: Int32;
begin
  K := 0;
  while K + 8 <= Len do begin
    if LoadLE64(A, AStart + K) <> LoadLE64(B, BStart + K) then begin
      Result := False;
      Exit;
    end;
    Inc(K, 8);
  end;
  while K < Len do begin
    if A[AStart + K] <> B[BStart + K] then begin
      Result := False;
      Exit;
    end;
    Inc(K);
  end;
  Result := True;
end;

function RotateLeft5(H: UInt64): UInt64; inline;
begin
  Result := (H shl 5) or (H shr 59);
end;

function StrHash(const B: TBytes; Start, Len: Int32): UInt64;
var
  I, K: Int32;
  H, Tail: UInt64;
begin
  { The multiplications wrap, as a hash's do. }
  H := UInt64(Len) * HashK;
  I := 0;
  while I + 8 <= Len do begin
    H := (RotateLeft5(H) xor LoadLE64(B, Start + I)) * HashK;
    Inc(I, 8);
  end;
  { The tail as one word: the last eight bytes when there are that many (overlapping the words already hashed),
    else the overlapping first and last four, else the bytes themselves. }
  Tail := 0;
  if Len >= 8 then
    Tail := LoadLE64(B, Start + Len - 8)
  else if Len >= 4 then
    Tail := UInt64(LoadLE32(B, Start)) or (UInt64(LoadLE32(B, Start + Len - 4)) shl 32)
  else
    for K := Start to Start + Len - 1 do
      Tail := (Tail shl 8) or B[K];
  Result := (RotateLeft5(H) xor Tail) * HashK;
end;

procedure SortUInt64Range(var A: TUInt64Array; Lo, Hi: Int32);
var
  I, J: Int32;
  Pivot, T: UInt64;
begin
  while Hi - Lo > 16 do begin
    Pivot := A[Lo + (Hi - Lo) shr 1];
    I := Lo;
    J := Hi;
    repeat
      while A[I] < Pivot do
        Inc(I);
      while A[J] > Pivot do
        Dec(J);
      if I <= J then begin
        T := A[I];
        A[I] := A[J];
        A[J] := T;
        Inc(I);
        Dec(J);
      end;
    until I > J;
    { The smaller part by recursion, the larger by the loop: the stack stays logarithmic. }
    if J - Lo < Hi - I then begin
      SortUInt64Range(A, Lo, J);
      Lo := I;
    end else begin
      SortUInt64Range(A, I, Hi);
      Hi := J;
    end;
  end;
  for I := Lo + 1 to Hi do begin
    T := A[I];
    J := I - 1;
    while (J >= Lo) and (A[J] > T) do begin
      A[J + 1] := A[J];
      Dec(J);
    end;
    A[J + 1] := T;
  end;
end;

procedure SortUInt64(var A: TUInt64Array; Count: Int32);
begin
  if Count > 1 then
    SortUInt64Range(A, 0, Count - 1);
end;

function Utf8Valid(const B: TBytes; Start, Stop: Int32): Boolean;
var
  J: Int32;
  C, C1: Byte;
begin
  Result := False;
  J := Start;
  while J < Stop do begin
    C := B[J];
    if C < $80 then begin
      Inc(J);
      Continue;
    end;
    if (C >= $C2) and (C <= $DF) then begin
      if (J + 1 >= Stop) or (B[J + 1] and $C0 <> $80) then
        Exit;
      Inc(J, 2);
    end else if (C >= $E0) and (C <= $EF) then begin
      if J + 2 >= Stop then
        Exit;
      C1 := B[J + 1];
      if (C1 and $C0 <> $80) or (B[J + 2] and $C0 <> $80) then
        Exit;
      { No overlong forms, and no surrogates. }
      if ((C = $E0) and (C1 < $A0)) or ((C = $ED) and (C1 > $9F)) then
        Exit;
      Inc(J, 3);
    end else if (C >= $F0) and (C <= $F4) then begin
      if J + 3 >= Stop then
        Exit;
      C1 := B[J + 1];
      if (C1 and $C0 <> $80) or (B[J + 2] and $C0 <> $80) or (B[J + 3] and $C0 <> $80) then
        Exit;
      { No overlong forms, and nothing beyond U+10FFFF. }
      if ((C = $F0) and (C1 < $90)) or ((C = $F4) and (C1 > $8F)) then
        Exit;
      Inc(J, 4);
    end else
      Exit;
  end;
  Result := True;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Reading values (the evaluator's side). A value is the index of its node. }

function DocKind(const D: TDocument; N: Int32): Byte; inline;
begin
  Result := Byte(D.Tape[N shl 1] and $FF);
end;

function DocFlags(const D: TDocument; N: Int32): Byte; inline;
begin
  Result := Byte((D.Tape[N shl 1] shr 8) and $FF);
end;

function DocCount(const D: TDocument; N: Int32): Int32; inline;
begin
  Result := Int32(D.Tape[N shl 1] shr 32);
end;

function DocData(const D: TDocument; N: Int32): UInt64; inline;
begin
  Result := D.Tape[N shl 1 + 1];
end;

function DocFirst(const D: TDocument; N: Int32): Int32; inline;
begin
  Result := Int32(D.Tape[N shl 1 + 1] and $FFFFFFFF);
end;

function DocBoolean(const D: TDocument; N: Int32): Boolean; inline;
begin
  Result := D.Tape[N shl 1 + 1] <> 0;
end;

function DocStrInText(const D: TDocument; N: Int32): Boolean; inline;
begin
  Result := D.Tape[N shl 1] and (StrText shl 8) <> 0;
end;

function DocStrOffset(const D: TDocument; N: Int32): Int32; inline;
begin
  Result := Int32(D.Tape[N shl 1 + 1] and $FFFFFFFF);
end;

function DocStrASCII(const D: TDocument; N: Int32): Boolean; inline;
begin
  Result := D.Tape[N shl 1] and (StrWide shl 8) = 0;
end;

function DocStrEquals(const D: TDocument; N: Int32; const Name: TBytes; Start, Len: Int32): Boolean;
begin
  if DocCount(D, N) <> Len then
    Result := False
  else if DocStrInText(D, N) then
    Result := BytesEqual(D.Text, DocStrOffset(D, N), Name, Start, Len)
  else
    Result := BytesEqual(D.Source, DocStrOffset(D, N), Name, Start, Len);
end;

function DocStrCopy(const D: TDocument; N: Int32): UTF8String;
var
  Len: Int32;
begin
  Result := '';
  Len := DocCount(D, N);
  if Len = 0 then
    Exit;
  SetLength(Result, Len);
  if DocStrInText(D, N) then
    Move(D.Text[DocStrOffset(D, N)], Result[1], Len)
  else
    Move(D.Source[DocStrOffset(D, N)], Result[1], Len);
end;

function DocProperty(const D: TDocument; AObject: Int32; const Name: UTF8String): Int32;
var
  K, I, J, Len, Off: Int32;
  Same: Boolean;
begin
  Len := Length(Name);
  K := DocFirst(D, AObject);
  for I := DocCount(D, AObject) downto 1 do begin
    if DocCount(D, K) = Len then begin
      Off := DocStrOffset(D, K);
      Same := True;
      if DocStrInText(D, K) then begin
        for J := 0 to Len - 1 do
          if D.Text[Off + J] <> Byte(Name[J + 1]) then begin
            Same := False;
            Break;
          end;
      end else
        for J := 0 to Len - 1 do
          if D.Source[Off + J] <> Byte(Name[J + 1]) then begin
            Same := False;
            Break;
          end;
      if Same then begin
        Result := K + 1;
        Exit;
      end;
    end;
    Inc(K, 2);
  end;
  Result := -1;
end;

function DocFloat(const D: TDocument; N: Int32): Double;
var
  V: UInt64;
begin
  V := DocData(D, N);
  case DocFlags(D, N) of
    NumInt: Result := Int64(V);
    NumUint: Result := UInt64ToDouble(V);
  else
    Result := DoubleFromBits(V);
  end;
end;

function DocNumberStart(const D: TDocument; N: Int32): Int32; inline;
begin
  Result := DocCount(D, N);
end;

function DocNumberEnd(const D: TDocument; N: Int32): Int32;
begin
  Result := NumberEnd(D.Source, DocCount(D, N));
end;

function NumberEnd(const B: TBytes; J: Int32): Int32;
var
  C: Byte;
  Len: Int32;
begin
  Len := Length(B);
  while J < Len do begin
    C := B[J];
    if ((C >= Ord('0')) and (C <= Ord('9'))) or (C = Ord('-')) or (C = Ord('+')) or (C = Ord('.')) or (C = Ord('e'))
      or (C = Ord('E')) then
      Inc(J)
    else
      Break;
  end;
  Result := J;
end;

{ An output buffer that grows by doubling. }
type
  TOut = record
    Buf: TBytes;
    Len: Int32;
  end;

procedure OutByte(var O: TOut; C: Byte);
begin
  if O.Len = Length(O.Buf) then
    SetLength(O.Buf, O.Len * 2 + 64);
  O.Buf[O.Len] := C;
  Inc(O.Len);
end;

procedure OutBytes(var O: TOut; const B: TBytes; Start, Stop: Int32);
var
  N: Int32;
begin
  N := Stop - Start;
  if N <= 0 then
    Exit;
  if O.Len + N > Length(O.Buf) then
    SetLength(O.Buf, (O.Len + N) * 2 + 64);
  Move(B[Start], O.Buf[O.Len], N);
  Inc(O.Len, N);
end;

procedure OutText(var O: TOut; const S: UTF8String);
var
  K: Int32;
begin
  for K := 1 to Length(S) do
    OutByte(O, Byte(S[K]));
end;

procedure AppendQuoted(var O: TOut; const S: TBytes; Start, Len: Int32);
var
  K: Int32;
  C: Byte;
begin
  OutByte(O, Ord('"'));
  for K := Start to Start + Len - 1 do begin
    C := S[K];
    case C of
      Ord('"'): OutText(O, '\"');
      Ord('\'): OutText(O, '\\');
      10: OutText(O, '\n');
      13: OutText(O, '\r');
      9: OutText(O, '\t');
      8: OutText(O, '\b');
      12: OutText(O, '\f');
    else
      if C < $20 then begin
        OutText(O, '\u00');
        OutByte(O, Hex[C shr 4]);
        OutByte(O, Hex[C and 15]);
      end else
        OutByte(O, C);
    end;
  end;
  OutByte(O, Ord('"'));
end;

procedure AppendStr(var O: TOut; const D: TDocument; N: Int32);
begin
  if DocStrInText(D, N) then
    AppendQuoted(O, D.Text, DocStrOffset(D, N), DocCount(D, N))
  else
    AppendQuoted(O, D.Source, DocStrOffset(D, N), DocCount(D, N));
end;

{ AppendJson appends the value as compact JSON text (numbers as written, strings escaped again). }
procedure AppendJson(var O: TOut; const D: TDocument; N: Int32);
var
  C, I: Int32;
begin
  case DocKind(D, N) of
    KindNull: OutText(O, 'null');
    KindBool:
      if DocBoolean(D, N) then
        OutText(O, 'true')
      else
        OutText(O, 'false');
    KindNumber: OutBytes(O, D.Source, DocNumberStart(D, N), DocNumberEnd(D, N));
    KindString: AppendStr(O, D, N);
    KindArray: begin
      OutByte(O, Ord('['));
      C := DocFirst(D, N);
      for I := 0 to DocCount(D, N) - 1 do begin
        if I > 0 then
          OutByte(O, Ord(','));
        AppendJson(O, D, C + I);
      end;
      OutByte(O, Ord(']'));
    end;
  else
    OutByte(O, Ord('{'));
    C := DocFirst(D, N);
    for I := 0 to DocCount(D, N) - 1 do begin
      if I > 0 then
        OutByte(O, Ord(','));
      AppendStr(O, D, C + 2 * I);
      OutByte(O, Ord(':'));
      AppendJson(O, D, C + 2 * I + 1);
    end;
    OutByte(O, Ord('}'));
  end;
end;

function DocumentToJson(const D: TDocument): UTF8String;
var
  O: TOut;
begin
  O.Buf := nil;
  O.Len := 0;
  AppendJson(O, D, D.Root);
  Result := '';
  SetLength(Result, O.Len);
  if O.Len > 0 then
    Move(O.Buf[0], Result[1], O.Len);
end;

function ParseErrorText(const Error: TParseError): UTF8String;
var
  Digits: UTF8String;
begin
  Str(Error.Offset, Digits);
  Result := 'invalid JSON at offset ' + Digits + ': ' + Error.Message;
end;

{ --------------------------------------------------------------------------------------------------------------------
  The parser }

function ParserRetainsTooMuch(const P: TParser): Boolean;
begin
  Result := 8 * (Int64(Length(P.Nodes)) + Length(P.Scratch) + Length(P.Hashes) + Length(P.Pairs)) + Length(P.Text)
    > RetainedLimit;
end;

function Fail(var P: TParser; const Message: UTF8String; At: Int32): Boolean;
begin
  P.ErrMessage := Message;
  P.ErrOffset := At;
  Result := False;
end;

function ParserError(const P: TParser): TParseError;
begin
  Result.Message := P.ErrMessage;
  Result.Offset := P.ErrOffset;
end;

procedure Reset(var P: TParser; const B: TBytes; Len: Int32; Validating: Boolean);
begin
  P.B := B;
  P.BLen := Len;
  P.I := 0;
  P.Validating := Validating;
  P.NodesLen := 0;
  P.ScratchLen := 0;
  P.FramesLen := 0;
  P.TextLen := 0;
end;

procedure SkipWs(var P: TParser); inline;
var
  I: Int32;
  C: Byte;
begin
  I := P.I;
  while I < P.BLen do begin
    C := P.B[I];
    if (C <> Ord(' ')) and (C <> 10) and (C <> 13) and (C <> 9) then
      Break;
    Inc(I);
  end;
  P.I := I;
end;

function Peek(const P: TParser): Int32; inline;
begin
  if P.I < P.BLen then
    Result := P.B[P.I]
  else
    Result := -1;
end;

procedure Push(var P: TParser; Header, Data: UInt64); inline;
begin
  if P.ScratchLen + 2 > Length(P.Scratch) then
    SetLength(P.Scratch, P.ScratchLen * 2 + 64);
  P.Scratch[P.ScratchLen] := Header;
  P.Scratch[P.ScratchLen + 1] := Data;
  Inc(P.ScratchLen, 2);
end;

procedure PushFrame(var P: TParser; Frame: UInt32);
begin
  if P.FramesLen = Length(P.Frames) then
    SetLength(P.Frames, P.FramesLen * 2 + 32);
  P.Frames[P.FramesLen] := Frame;
  Inc(P.FramesLen);
end;

procedure TextReserve(var P: TParser; More: Int32); inline;
begin
  if P.TextLen + More > Length(P.Text) then
    SetLength(P.Text, (P.TextLen + More) * 2 + 64);
end;

procedure TextAppend(var P: TParser; Start, Stop: Int32);
var
  N: Int32;
begin
  N := Stop - Start;
  if N <= 0 then
    Exit;
  TextReserve(P, N);
  Move(P.B[Start], P.Text[P.TextLen], N);
  Inc(P.TextLen, N);
end;

procedure TextByte(var P: TParser; C: Byte); inline;
begin
  TextReserve(P, 1);
  P.Text[P.TextLen] := C;
  Inc(P.TextLen);
end;

{ TextRune appends a code point as UTF-8. }
procedure TextRune(var P: TParser; Cp: Int32);
begin
  if Cp < $80 then
    TextByte(P, Byte(Cp))
  else if Cp < $800 then begin
    TextByte(P, Byte($C0 or (Cp shr 6)));
    TextByte(P, Byte($80 or (Cp and $3F)));
  end else if Cp < $10000 then begin
    TextByte(P, Byte($E0 or (Cp shr 12)));
    TextByte(P, Byte($80 or ((Cp shr 6) and $3F)));
    TextByte(P, Byte($80 or (Cp and $3F)));
  end else begin
    TextByte(P, Byte($F0 or (Cp shr 18)));
    TextByte(P, Byte($80 or ((Cp shr 12) and $3F)));
    TextByte(P, Byte($80 or ((Cp shr 6) and $3F)));
    TextByte(P, Byte($80 or (Cp and $3F)));
  end;
end;

{ KeyEquals says whether the keys at scratch value indexes A and B are the same. A key's bytes are in the text
  buffer when it had escapes, and in the source otherwise. }
function KeyEqualsWords(const P: TParser; HA, OffA, HB, OffB: UInt64): Boolean;
var
  Len, A, B: Int32;
begin
  if HA shr 32 <> HB shr 32 then begin
    Result := False;
    Exit;
  end;
  Len := Int32(HA shr 32);
  A := Int32(OffA and $FFFFFFFF);
  B := Int32(OffB and $FFFFFFFF);
  if HA and (StrText shl 8) <> 0 then begin
    if HB and (StrText shl 8) <> 0 then
      Result := BytesEqual(P.Text, A, P.Text, B, Len)
    else
      Result := BytesEqual(P.Text, A, P.B, B, Len);
  end else if HB and (StrText shl 8) <> 0 then
    Result := BytesEqual(P.B, A, P.Text, B, Len)
  else
    Result := BytesEqual(P.B, A, P.B, B, Len);
end;

function KeyEquals(const P: TParser; A, B: Int32): Boolean;
begin
  Result := KeyEqualsWords(P, P.Scratch[A * 2], P.Scratch[A * 2 + 1], P.Scratch[B * 2], P.Scratch[B * 2 + 1]);
end;

function KeyHash(const P: TParser; A: Int32): UInt64;
var
  H: UInt64;
begin
  H := P.Scratch[A * 2];
  if H and (StrText shl 8) <> 0 then
    Result := StrHash(P.Text, Int32(P.Scratch[A * 2 + 1] and $FFFFFFFF), Int32(H shr 32))
  else
    Result := StrHash(P.B, Int32(P.Scratch[A * 2 + 1] and $FFFFFFFF), Int32(H shr 32));
end;

{ Dedupe keeps, of duplicate property names in the object whose pairs start at Start, the last value at the first
  position. }
procedure Dedupe(var P: TParser; Start: Int32);
var
  Count, J, K, A, E, X, Y, Q, At, Words: Int32;
  Duplicate: Boolean;
begin
  Count := (P.ScratchLen div 2 - Start) div 2;
  Duplicate := False;
  if Count <= 16 then begin
    J := 1;
    while (J < Count) and not Duplicate do begin
      for K := 0 to J - 1 do
        if KeyEquals(P, Start + 2 * K, Start + 2 * J) then begin
          Duplicate := True;
          Break;
        end;
      Inc(J);
    end;
  end else begin
    { Sorted by hash in reused scratch: only keys with equal hashes are compared. }
    if Length(P.Hashes) < Count then
      SetLength(P.Hashes, Count * 2);
    for J := 0 to Count - 1 do
      P.Hashes[J] := (KeyHash(P, Start + 2 * J) and UInt64($FFFFFFFF00000000)) or UInt64(J);
    SortUInt64(P.Hashes, Count);
    A := 0;
    while (A < Count) and not Duplicate do begin
      E := A + 1;
      while (E < Count) and (P.Hashes[E] shr 32 = P.Hashes[A] shr 32) do
        Inc(E);
      X := A + 1;
      while (X < E) and not Duplicate do begin
        for Y := A to X - 1 do
          if KeyEquals(P, Start + 2 * Int32(P.Hashes[X] and $FFFFFFFF),
            Start + 2 * Int32(P.Hashes[Y] and $FFFFFFFF)) then begin
            Duplicate := True;
            Break;
          end;
        Inc(X);
      end;
      A := E;
    end;
  end;
  if not Duplicate then
    Exit;
  Words := P.ScratchLen - Start * 2;
  if Length(P.Pairs) < Words then
    SetLength(P.Pairs, Words * 2);
  Move(P.Scratch[Start * 2], P.Pairs[0], Words * SizeOf(UInt64));
  P.ScratchLen := Start * 2;
  for Q := 0 to Count - 1 do begin
    At := -1;
    K := Start;
    while K < P.ScratchLen div 2 do begin
      if KeyEqualsWords(P, P.Scratch[K * 2], P.Scratch[K * 2 + 1], P.Pairs[Q * 4], P.Pairs[Q * 4 + 1]) then begin
        At := K;
        Break;
      end;
      Inc(K, 2);
    end;
    if At >= 0 then begin
      P.Scratch[(At + 1) * 2] := P.Pairs[Q * 4 + 2];
      P.Scratch[(At + 1) * 2 + 1] := P.Pairs[Q * 4 + 3];
    end else begin
      { The scratch held these four words before, so it has room for them. }
      Move(P.Pairs[Q * 4], P.Scratch[P.ScratchLen], 4 * SizeOf(UInt64));
      Inc(P.ScratchLen, 4);
    end;
  end;
end;

{ Close moves the closed container's children to nodes and pushes the container in their place. }
procedure CloseContainer(var P: TParser; Start: Int32; IsObject: Boolean);
var
  Children, First, Words: Int32;
begin
  Dec(P.FramesLen);
  if IsObject and (P.ScratchLen div 2 - Start > 2) and not P.Validating then
    Dedupe(P, Start);
  Children := P.ScratchLen div 2 - Start;
  First := P.NodesLen div 2;
  Words := P.ScratchLen - Start * 2;
  if P.NodesLen + Words > Length(P.Nodes) then
    SetLength(P.Nodes, (P.NodesLen + Words) * 2 + 64);
  Move(P.Scratch[Start * 2], P.Nodes[P.NodesLen], Words * SizeOf(UInt64));
  Inc(P.NodesLen, Words);
  P.ScratchLen := Start * 2;
  if IsObject then
    Push(P, UInt64(KindObject) or (UInt64(Children div 2) shl 32), UInt64(First))
  else
    Push(P, UInt64(KindArray) or (UInt64(Children) shl 32), UInt64(First));
end;

function Hex4(const P: TParser; At: Int32): Int32;
var
  K, V, D: Int32;
  C: Byte;
begin
  Result := -1;
  if At + 4 > P.BLen then
    Exit;
  V := 0;
  for K := At to At + 3 do begin
    C := P.B[K];
    if (C >= Ord('0')) and (C <= Ord('9')) then
      D := C - Ord('0')
    else if (C >= Ord('a')) and (C <= Ord('f')) then
      D := C - Ord('a') + 10
    else if (C >= Ord('A')) and (C <= Ord('F')) then
      D := C - Ord('A') + 10
    else
      Exit;
    V := V * 16 + D;
  end;
  Result := V;
end;

{ UnicodeEscape reads a \u escape at J (and its low surrogate, for a high one): the code point. }
function UnicodeEscape(var P: TParser; J: Int32; out Cp: Int32): Boolean;
var
  U, Low: Int32;
begin
  Cp := 0;
  U := Hex4(P, J + 2);
  if U < 0 then begin
    Result := Fail(P, 'invalid \u escape', J);
    Exit;
  end;
  if (U >= $D800) and (U <= $DBFF) then begin
    Low := -1;
    if (J + 7 < P.BLen) and (P.B[J + 6] = Ord('\')) and (P.B[J + 7] = Ord('u')) then
      Low := Hex4(P, J + 8);
    if (Low >= $DC00) and (Low <= $DFFF) then begin
      Cp := $10000 + ((U - $D800) shl 10) + (Low - $DC00);
      Result := True;
    end else
      Result := Fail(P, 'lone leading surrogate in hex escape', J);
    Exit;
  end;
  if (U >= $DC00) and (U <= $DFFF) then begin
    Result := Fail(P, 'lone trailing surrogate in hex escape', J);
    Exit;
  end;
  Cp := U;
  Result := True;
end;

{ Escaped reads the rest of a string with escapes, unescaped into the text buffer. J is at the first backslash. }
function Escaped(var P: TParser; Start, J: Int32; Wide: Boolean): Boolean;
var
  Offset, Run, E, Cp: Int32;
  C, OutC: Byte;
  Header: UInt64;
begin
  Offset := P.TextLen;
  Run := Start;
  while True do begin
    if J >= P.BLen then begin
      Result := Fail(P, 'unterminated string', J);
      Exit;
    end;
    C := P.B[J];
    if C = Ord('"') then begin
      if Wide and not Utf8Valid(P.B, Run, J) then begin
        Result := Fail(P, 'invalid UTF-8', Run);
        Exit;
      end;
      TextAppend(P, Run, J);
      P.I := J + 1;
      Header := UInt64(KindString) or (StrText shl 8) or (UInt64(P.TextLen - Offset) shl 32);
      if Wide then
        Header := Header or (StrWide shl 8);
      Push(P, Header, UInt64(Offset));
      Result := True;
      Exit;
    end else if C = Ord('\') then begin
      if Wide and not Utf8Valid(P.B, Run, J) then begin
        Result := Fail(P, 'invalid UTF-8', Run);
        Exit;
      end;
      TextAppend(P, Run, J);
      E := -1;
      if J + 1 < P.BLen then
        E := P.B[J + 1];
      case E of
        Ord('"'), Ord('\'), Ord('/'): OutC := Byte(E);
        Ord('b'): OutC := 8;
        Ord('f'): OutC := 12;
        Ord('n'): OutC := 10;
        Ord('r'): OutC := 13;
        Ord('t'): OutC := 9;
        Ord('u'): begin
          if not UnicodeEscape(P, J, Cp) then begin
            Result := False;
            Exit;
          end;
          if Cp >= $10000 then
            Inc(J, 12)
          else
            Inc(J, 6);
          if Cp >= $80 then
            Wide := True;
          TextRune(P, Cp);
          Run := J;
          Continue;
        end;
      else
        Result := Fail(P, 'invalid escape', J);
        Exit;
      end;
      TextByte(P, OutC);
      Inc(J, 2);
      Run := J;
    end else if C < $20 then begin
      Result := Fail(P, 'control character in a string', J);
      Exit;
    end else begin
      if C >= $80 then
        Wide := True;
      Inc(J);
    end;
  end;
end;

{ TrailingZeroBytes is how many whole low bytes of a word, which is not zero, are zero. }
function TrailingZeroBytes(W: UInt64): Int32; inline;
begin
  Result := 0;
  if W and $FFFFFFFF = 0 then begin
    Inc(Result, 4);
    W := W shr 32;
  end;
  if W and $FFFF = 0 then begin
    Inc(Result, 2);
    W := W shr 16;
  end;
  if W and $FF = 0 then
    Inc(Result);
end;

{ Str reads a string, from its opening quote. Eight bytes at a time while there are that many, then a byte at a
  time. }
function ReadStr(var P: TParser): Boolean;
var
  Start, J: Int32;
  High, W, Quote, Backslash, Stop: UInt64;
  Wide, C: Byte;
  Header: UInt64;
begin
  Start := P.I + 1;
  J := Start;
  High := 0;
  while J + 8 <= P.BLen do begin
    W := LoadLE64(P.B, J);
    { The bytes that end the run: below 0x20, a quote or a backslash. Each test marks the high bit of a byte that
      matches. A borrow can mark a byte wrongly only above one that matches, and the lowest mark is taken. The
      subtractions wrap on purpose. }
    Quote := W xor SwarQuotes;
    Backslash := W xor SwarBackslashes;
    Stop := (((W - SwarSpaces) and not W) or ((Quote - SwarOnes) and not Quote)
      or ((Backslash - SwarOnes) and not Backslash)) and SwarHighs;
    if Stop <> 0 then begin
      { Only the non-ASCII bytes before the stop count: those below its lowest set bit. }
      High := High or (W and SwarHighs and ((Stop and (not Stop + UInt64(1))) - UInt64(1)));
      Inc(J, TrailingZeroBytes(Stop));
      Break;
    end;
    High := High or (W and SwarHighs);
    Inc(J, 8);
  end;
  Wide := 0;
  if High <> 0 then
    Wide := 2;
  while J < P.BLen do begin
    C := Scan[P.B[J]];
    if C = 1 then
      Break;
    Wide := Wide or C;
    Inc(J);
  end;
  if J >= P.BLen then begin
    Result := Fail(P, 'unterminated string', J);
    Exit;
  end;
  case P.B[J] of
    Ord('"'): begin
      Header := UInt64(KindString) or (UInt64(J - Start) shl 32);
      if Wide <> 0 then begin
        if not Utf8Valid(P.B, Start, J) then begin
          Result := Fail(P, 'invalid UTF-8', Start);
          Exit;
        end;
        Header := Header or (StrWide shl 8);
      end;
      P.I := J + 1;
      Push(P, Header, UInt64(Start));
      Result := True;
    end;
    Ord('\'): Result := Escaped(P, Start, J, Wide <> 0);
  else
    Result := Fail(P, 'control character in a string', J);
  end;
end;

function Literal(var P: TParser; const Word: UTF8String; Kind, Data: UInt64): Boolean;
var
  K, Len: Int32;
begin
  Len := Length(Word);
  if P.I + Len > P.BLen then begin
    Result := Fail(P, 'expected a value', P.I);
    Exit;
  end;
  for K := 0 to Len - 1 do
    if P.B[P.I + K] <> Byte(Word[K + 1]) then begin
      Result := Fail(P, 'expected a value', P.I);
      Exit;
    end;
  Inc(P.I, Len);
  Push(P, Kind, Data);
  Result := True;
end;

function IsDigit(const P: TParser; J: Int32): Boolean; inline;
begin
  Result := (J < P.BLen) and (P.B[J] >= Ord('0')) and (P.B[J] <= Ord('9'));
end;

{ Number reads a number: integers that fit 64 bits as integers (unsigned beyond an Int64), anything else as a
  Double. The header keeps the offset of the text. }
function ReadNumber(var P: TParser): Boolean;
var
  Start, J, IntStart, Limit, Digits, Scale, FracStart, Exponent: Int32;
  Negative, Exact, Floating, ExpOverflow, ExpNegative: Boolean;
  Mantissa, Sum, Header: UInt64;
  C: Int32;
  D: Double;
begin
  Start := P.I;
  J := Start;
  Negative := P.B[J] = Ord('-');
  if Negative then
    Inc(J);
  { The digits of the integer and the fraction as one integer, while they fit: Mantissa. Exact says no digit was
    left out of it. }
  Mantissa := 0;
  Exact := True;
  IntStart := J;
  if (J < P.BLen) and (P.B[J] = Ord('0')) then
    Inc(J)
  else begin
    { Nineteen digits cannot overflow 64 bits. }
    Limit := J + 19;
    if Limit > P.BLen then
      Limit := P.BLen;
    while J < Limit do begin
      C := Int32(P.B[J]) - Ord('0');
      if (C < 0) or (C > 9) then
        Break;
      Mantissa := Mantissa * 10 + UInt64(C);
      Inc(J);
    end;
    if J = IntStart then begin
      Result := Fail(P, 'invalid number', J);
      Exit;
    end;
    while IsDigit(P, J) do begin
      if Exact then begin
        { The largest value ten times which, plus nine, still fits 64 bits. }
        if Mantissa > UInt64(1844674407370955161) then
          Exact := False
        else begin
          Sum := Mantissa * 10 + UInt64(Int32(P.B[J]) - Ord('0'));
          if Sum < Mantissa * 10 then
            Exact := False
          else
            Mantissa := Sum;
        end;
      end;
      Inc(J);
    end;
  end;
  Digits := J - IntStart;
  Floating := False;
  Scale := 0;
  if (J < P.BLen) and (P.B[J] = Ord('.')) then begin
    Inc(J);
    FracStart := J;
    while J < P.BLen do begin
      C := Int32(P.B[J]) - Ord('0');
      if (C < 0) or (C > 9) then
        Break;
      Inc(Digits);
      if Digits <= 19 then
        Mantissa := Mantissa * 10 + UInt64(C)
      else
        Exact := False;
      Inc(J);
    end;
    if J = FracStart then begin
      Result := Fail(P, 'invalid number', J);
      Exit;
    end;
    Scale := FracStart - J;
    Floating := True;
  end;
  Exponent := 0;
  ExpOverflow := False;
  if (J < P.BLen) and ((P.B[J] = Ord('e')) or (P.B[J] = Ord('E'))) then begin
    Inc(J);
    ExpNegative := False;
    if (J < P.BLen) and ((P.B[J] = Ord('+')) or (P.B[J] = Ord('-'))) then begin
      ExpNegative := P.B[J] = Ord('-');
      Inc(J);
    end;
    if not IsDigit(P, J) then begin
      Result := Fail(P, 'invalid number', J);
      Exit;
    end;
    while IsDigit(P, J) do begin
      if Exponent < 100000 then
        Exponent := Exponent * 10 + (Int32(P.B[J]) - Ord('0'))
      else
        ExpOverflow := True;
      Inc(J);
    end;
    if ExpNegative then
      Exponent := -Exponent;
    Floating := True;
  end;
  P.I := J;
  Header := UInt64(KindNumber) or (UInt64(Start) shl 32);
  if not Floating and Exact then begin
    if not Negative then begin
      if Mantissa shr 63 <> 0 then
        Push(P, Header or (NumUint shl 8), Mantissa)
      else
        Push(P, Header or (NumInt shl 8), Mantissa);
      Result := True;
      Exit;
    end;
    { -0 is the float. }
    if (Mantissa <> 0) and (Mantissa <= UInt64($8000000000000000)) then begin
      { Two's complement negation, which wraps for 2^63 to the bits of the least Int64. }
      Push(P, Header or (NumInt shl 8), not Mantissa + 1);
      Result := True;
      Exit;
    end;
  end;
  { A mantissa that a Double holds exactly and a power of ten that one does too: one exact conversion and one
    correctly rounded operation give the correctly rounded value (Clinger's fast path). }
  Inc(Scale, Exponent);
  D := 0;
  if Exact and not ExpOverflow and (Mantissa shr 53 = 0) and (Scale >= -22) and (Scale <= 22) then begin
    D := Int64(Mantissa);
    if Scale < 0 then
      D := D / Powers[-Scale]
    else
      D := D * Powers[Scale];
    if Negative then
      D := -D;
  end else if P.Validating and not ExpOverflow and (Digits + Exponent < 300) then begin
    { Checking syntax only: the number is well within the range of a Double, and its value is not needed. }
  end else begin
    { The text is a validated JSON number, which DecimalToDouble reads correctly rounded. }
    if not DecimalToDouble(P.B, Start, J, D) then begin
      Result := Fail(P, 'number out of range', Start);
      Exit;
    end;
  end;
  Push(P, Header or (NumFloat shl 8), DoubleToBits(D));
  Result := True;
end;

{ Key reads a property name and its colon, leaving the parser at the value. }
function ReadKey(var P: TParser): Boolean;
begin
  if Peek(P) <> Ord('"') then begin
    Result := Fail(P, 'expected a property name', P.I);
    Exit;
  end;
  if not ReadStr(P) then begin
    Result := False;
    Exit;
  end;
  SkipWs(P);
  if Peek(P) <> Ord(':') then begin
    Result := Fail(P, 'expected '':''', P.I);
    Exit;
  end;
  Inc(P.I);
  SkipWs(P);
  Result := True;
end;

function Parse(var P: TParser): Boolean;
var
  C: Int32;
  Frame: UInt32;
  IsObject, Next: Boolean;
begin
  SkipWs(P);
  while True do begin
    { A value. }
    C := Peek(P);
    case C of
      Ord('{'): begin
        Inc(P.I);
        SkipWs(P);
        if P.FramesLen >= MaxDepth then begin
          Result := Fail(P, 'nesting too deep', P.I);
          Exit;
        end;
        if Peek(P) = Ord('}') then begin
          Inc(P.I);
          Push(P, KindObject, 0);
        end else begin
          PushFrame(P, UInt32(P.ScratchLen div 2) or ObjectFrame);
          if not ReadKey(P) then begin
            Result := False;
            Exit;
          end;
          Continue;
        end;
      end;
      Ord('['): begin
        Inc(P.I);
        SkipWs(P);
        if P.FramesLen >= MaxDepth then begin
          Result := Fail(P, 'nesting too deep', P.I);
          Exit;
        end;
        if Peek(P) = Ord(']') then begin
          Inc(P.I);
          Push(P, KindArray, 0);
        end else begin
          PushFrame(P, UInt32(P.ScratchLen div 2));
          Continue;
        end;
      end;
      Ord('"'):
        if not ReadStr(P) then begin
          Result := False;
          Exit;
        end;
      Ord('t'):
        if not Literal(P, 'true', KindBool, 1) then begin
          Result := False;
          Exit;
        end;
      Ord('f'):
        if not Literal(P, 'false', KindBool, 0) then begin
          Result := False;
          Exit;
        end;
      Ord('n'):
        if not Literal(P, 'null', KindNull, 0) then begin
          Result := False;
          Exit;
        end;
      Ord('-'), Ord('0')..Ord('9'):
        if not ReadNumber(P) then begin
          Result := False;
          Exit;
        end;
      -1: begin
        Result := Fail(P, 'unexpected end of input', P.I);
        Exit;
      end;
    else
      Result := Fail(P, 'expected a value', P.I);
      Exit;
    end;
    { After a value: separators and closing brackets, until the next value or the end. }
    Next := False;
    while not Next do begin
      if P.FramesLen = 0 then begin
        SkipWs(P);
        if P.I <> P.BLen then
          Result := Fail(P, 'trailing characters', P.I)
        else
          Result := True;
        Exit;
      end;
      Frame := P.Frames[P.FramesLen - 1];
      IsObject := Frame and ObjectFrame <> 0;
      SkipWs(P);
      C := Peek(P);
      if C = Ord(',') then begin
        Inc(P.I);
        SkipWs(P);
        if IsObject and not ReadKey(P) then begin
          Result := False;
          Exit;
        end;
        Next := True;
      end else if (C = Ord('}')) and IsObject then begin
        Inc(P.I);
        CloseContainer(P, Int32(Frame and not ObjectFrame), True);
      end else if (C = Ord(']')) and not IsObject then begin
        Inc(P.I);
        CloseContainer(P, Int32(Frame), False);
      end else if C = -1 then begin
        Result := Fail(P, 'unexpected end of input', P.I);
        Exit;
      end else if IsObject then begin
        Result := Fail(P, 'expected '','' or ''}''', P.I);
        Exit;
      end else begin
        Result := Fail(P, 'expected '','' or '']''', P.I);
        Exit;
      end;
    end;
  end;
end;

function ParserIsValid(var P: TParser; const B: TBytes; Len: Int32): Boolean;
begin
  Reset(P, B, Len, True);
  Result := Parse(P);
  P.B := nil;
end;

{ ParseNew parses B into a new document, exactly sized. }
function ParseNew(var P: TParser; const B: TBytes; out D: TDocument): Boolean;
var
  Root: Int32;
begin
  D.Tape := nil;
  D.Source := nil;
  D.Text := nil;
  D.Root := 0;
  Reset(P, B, Length(B), False);
  Result := Parse(P);
  P.B := nil;
  if not Result then
    Exit;
  Root := P.NodesLen div 2;
  SetLength(D.Tape, P.NodesLen + 2);
  if P.NodesLen > 0 then
    Move(P.Nodes[0], D.Tape[0], P.NodesLen * SizeOf(UInt64));
  D.Tape[Root * 2] := P.Scratch[0];
  D.Tape[Root * 2 + 1] := P.Scratch[1];
  if P.TextLen > 0 then begin
    SetLength(D.Text, P.TextLen);
    Move(P.Text[0], D.Text[0], P.TextLen);
  end;
  D.Source := B;
  D.Root := Root;
end;

function ParserParseInto(var P: TParser; var D: TDocument; const B: TBytes; Len: Int32): Boolean;
var
  Nodes: TUInt64Array;
  Text: TBytes;
begin
  { The parser writes into the document's arrays for this parse, and takes its own back afterwards. }
  Nodes := P.Nodes;
  Text := P.Text;
  P.Nodes := D.Tape;
  P.Text := D.Text;
  D.Tape := nil;
  D.Text := nil;
  Reset(P, B, Len, False);
  Result := Parse(P);
  P.B := nil;
  if Result then begin
    D.Root := P.NodesLen div 2;
    if P.NodesLen + 2 > Length(P.Nodes) then
      SetLength(P.Nodes, (P.NodesLen + 2) * 2 + 64);
    P.Nodes[P.NodesLen] := P.Scratch[0];
    P.Nodes[P.NodesLen + 1] := P.Scratch[1];
    D.Source := B;
  end;
  { Keep what the buffers have grown to. }
  D.Tape := P.Nodes;
  D.Text := P.Text;
  P.Nodes := Nodes;
  P.Text := Text;
end;

function ParseDocument(const Json: TBytes; out D: TDocument; out Error: TParseError): Boolean;
var
  P: TParser;
begin
  FillChar(P, SizeOf(P), 0);
  Error.Message := '';
  Error.Offset := 0;
  Result := ParseNew(P, Json, D);
  if not Result then
    Error := ParserError(P);
end;

function ParseDocumentString(const Json: UTF8String; out D: TDocument; out Error: TParseError): Boolean;
begin
  Result := ParseDocument(BytesOf(Json), D, Error);
end;

procedure InitScan;
var
  C: Int32;
begin
  for C := 0 to 255 do
    Scan[C] := 0;
  for C := 0 to $1F do
    Scan[C] := 1;
  Scan[Ord('"')] := 1;
  Scan[Ord('\')] := 1;
  for C := $80 to $FF do
    Scan[C] := 2;
end;

initialization
  InitScan;
end.
