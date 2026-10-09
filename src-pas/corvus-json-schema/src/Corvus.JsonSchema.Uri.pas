unit Corvus.JsonSchema.Uri;
{$I corvus.inc}

// URI handling for schema identification and reference resolution (RFC 3986 section 5). Reference resolution is
// implemented directly so that opaque bases (urn:, tag:) resolve the same way in every port of the evaluator.
//
// Ported from uri.go. A URI is kept as a UTF8String, whose bytes are indexed from 1, so a position here is the Go
// position plus one and "no position" is 0 where the Go source has -1. Text is compared and built byte by byte, and
// never through a conversion between code pages.

interface

uses
  Corvus.JsonSchema.Document;

type
  TUriParts = record
    Scheme, Authority, Path, Query: UTF8String;
    HasScheme, HasAuthority, HasQuery: Boolean;
  end;

// Utf8Equal reports whether two texts are the same bytes.
function Utf8Equal(const A, B: UTF8String): Boolean;

// ParseURI splits a URI into scheme, authority, path and query (the fragment must already be removed).
function ParseURI(const Uri: UTF8String): TUriParts;

// SchemeEnd is the index of the ':' ending a URI scheme, or 0 if the text does not start with one.
function SchemeEnd(const S: UTF8String): Int32;

// AsciiLower returns S with the letters A to Z in lower case and everything else as it is. RFC 3986 and RFC 3987 make
// a scheme and a host case-insensitive for those letters only, and the mapping does not depend on the Unicode data
// of the runtime library as a general lower case function does.
function AsciiLower(const S: UTF8String): UTF8String;

function IsASCIILetter(B: Byte): Boolean; inline;

function IsASCIIDigit(B: Byte): Boolean; inline;

// HasScheme reports whether the reference starts with a URI scheme.
function HasScheme(const Reference: UTF8String): Boolean;

// SplitFragment splits a reference at its first '#'.
procedure SplitFragment(const Reference: UTF8String; out Left, Fragment: UTF8String);

// UriPartsToString puts the parts of a URI together again (the String method of uriParts in the Go source).
function UriPartsToString(const P: TUriParts): UTF8String;

function RemoveDotSegments(const Path: UTF8String): UTF8String;

function MergePaths(const Base: TUriParts; const RefPath: UTF8String): UTF8String;

function NormalizeParts(const Parts: TUriParts): UTF8String;

// NormalizeURI normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal.
function NormalizeURI(const Uri: UTF8String): UTF8String;

// ResolveURI resolves a reference (without fragment) against a base URI, returning the normalised absolute URI.
function ResolveURI(const BaseURI, Reference: UTF8String): UTF8String;

// HexValue is the value of a hexadecimal digit, or -1.
function HexValue(C: Byte): Int32;

// DecodeFragment percent-decodes a fragment (invalid escapes leave the text unchanged).
function DecodeFragment(const Fragment: UTF8String): UTF8String;

// EscapePointerToken escapes a JSON pointer token (~ as ~0, / as ~1).
function EscapePointerToken(const Token: UTF8String): UTF8String;

// UnescapePointerToken reads a token of an RFC 6901 JSON pointer as written (~1 as /, then ~0 as ~). It is the
// first step ResolvePointer takes with each token.
function UnescapePointerToken(const Raw: UTF8String): UTF8String;

// PointerArrayIndex reads a token of a JSON pointer as an index into an array: "0", or digits that do not start with
// "0". Not ok for any other token, and for an index too large to be one. It is the check ResolvePointer makes before
// it steps into an array.
function PointerArrayIndex(const Token: UTF8String; out Index: Int32): Boolean;

// ResolvePointer resolves an RFC 6901 JSON pointer against a value of a document, returning the value and its
// normalised pointer, or -1.
function ResolvePointer(const D: TDocument; Root: Int32; const Pointer: UTF8String; out Path: UTF8String): Int32;

implementation

const
  // The texts the functions below build with, typed so that a concatenation never mixes code pages.
  Slash: UTF8String = '/';
  Colon: UTF8String = ':';
  DoubleSlash: UTF8String = '//';
  QuestionMark: UTF8String = '?';
  Tilde: UTF8String = '~';
  Tilde0: UTF8String = '~0';
  Tilde1: UTF8String = '~1';

type
  TUtf8Strings = array of UTF8String;

// IsASCIILetter and IsASCIIDigit come first, so that the functions below can take them inline.

function IsASCIILetter(B: Byte): Boolean;
begin
  Result := ((B or $20) >= Ord('a')) and ((B or $20) <= Ord('z'));
end;

function IsASCIIDigit(B: Byte): Boolean;
begin
  Result := (B >= Ord('0')) and (B <= Ord('9'));
end;

function Utf8Equal(const A, B: UTF8String): Boolean;
var
  I: Int32;
begin
  if Length(A) <> Length(B) then begin
    Result := False;
    Exit;
  end;
  for I := 1 to Length(A) do begin
    if A[I] <> B[I] then begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;

// IndexByte is the index of the first C in S at or after From, or 0.
function IndexByte(const S: UTF8String; C: AnsiChar; From: Int32): Int32;
var
  I: Int32;
begin
  for I := From to Length(S) do begin
    if S[I] = C then begin
      Result := I;
      Exit;
    end;
  end;
  Result := 0;
end;

// LastIndexByte is the index of the last C in S, or 0.
function LastIndexByte(const S: UTF8String; C: AnsiChar): Int32;
var
  I: Int32;
begin
  for I := Length(S) downto 1 do begin
    if S[I] = C then begin
      Result := I;
      Exit;
    end;
  end;
  Result := 0;
end;

// Slice is the bytes S[From .. Stop-1].
function Slice(const S: UTF8String; From, Stop: Int32): UTF8String;
var
  I: Int32;
begin
  Result := '';
  if Stop <= From then Exit;
  SetLength(Result, Stop - From);
  for I := From to Stop - 1 do Result[I - From + 1] := S[I];
end;

// HasSuffix reports whether S ends with Suffix.
function HasSuffix(const S, Suffix: UTF8String): Boolean;
var
  I, Offset: Int32;
begin
  Offset := Length(S) - Length(Suffix);
  if Offset < 0 then begin
    Result := False;
    Exit;
  end;
  for I := 1 to Length(Suffix) do begin
    if S[Offset + I] <> Suffix[I] then begin
      Result := False;
      Exit;
    end;
  end;
  Result := True;
end;

// TrimSuffix is S without the suffix, when it ends with it.
function TrimSuffix(const S, Suffix: UTF8String): UTF8String;
begin
  if HasSuffix(S, Suffix) then Result := Slice(S, 1, Length(S) - Length(Suffix) + 1)
  else Result := S;
end;

// ReplaceByte is S with every C replaced by the text.
function ReplaceByte(const S: UTF8String; C: AnsiChar; const Replacement: UTF8String): UTF8String;
var
  I, Start: Int32;
begin
  Result := '';
  Start := 1;
  for I := 1 to Length(S) do begin
    if S[I] = C then begin
      Result := Result + Slice(S, Start, I) + Replacement;
      Start := I + 1;
    end;
  end;
  Result := Result + Slice(S, Start, Length(S) + 1);
end;

// ReplacePair is S with every "~" followed by Second replaced by the text, read from the left without overlap.
function ReplacePair(const S: UTF8String; Second: AnsiChar; const Replacement: UTF8String): UTF8String;
var
  I, Start: Int32;
begin
  Result := '';
  Start := 1;
  I := 1;
  while I < Length(S) do begin
    if (S[I] = '~') and (S[I + 1] = Second) then begin
      Result := Result + Slice(S, Start, I) + Replacement;
      Inc(I, 2);
      Start := I;
    end else Inc(I);
  end;
  Result := Result + Slice(S, Start, Length(S) + 1);
end;

// Utf8Valid reports whether the text is well-formed UTF-8: no overlong forms, no surrogates and nothing above
// U+10FFFF.
function Utf8Valid(const S: UTF8String): Boolean;
var
  I, N, Need, K: Int32;
  B, Lo, Hi: Byte;
begin
  Result := False;
  N := Length(S);
  I := 1;
  while I <= N do begin
    B := Ord(S[I]);
    if B < $80 then begin
      Inc(I);
      Continue;
    end;
    if (B < $C2) or (B > $F4) then Exit;
    Lo := $80;
    Hi := $BF;
    if B < $E0 then Need := 1
    else if B < $F0 then begin
      Need := 2;
      if B = $E0 then Lo := $A0;
      if B = $ED then Hi := $9F;
    end else begin
      Need := 3;
      if B = $F0 then Lo := $90;
      if B = $F4 then Hi := $8F;
    end;
    if I + Need > N then Exit;
    B := Ord(S[I + 1]);
    if (B < Lo) or (B > Hi) then Exit;
    for K := 2 to Need do begin
      B := Ord(S[I + K]);
      if (B < $80) or (B > $BF) then Exit;
    end;
    Inc(I, Need + 1);
  end;
  Result := True;
end;

function ParseURI(const Uri: UTF8String): TUriParts;
var
  Colon, Start, Stop, Q, I: Int32;
begin
  Result.Scheme := '';
  Result.Authority := '';
  Result.Path := '';
  Result.Query := '';
  Result.HasScheme := False;
  Result.HasAuthority := False;
  Result.HasQuery := False;
  // Start is where the rest of the text begins.
  Start := 1;
  Colon := SchemeEnd(Uri);
  if Colon > 0 then begin
    Result.Scheme := Slice(Uri, 1, Colon);
    Result.HasScheme := True;
    Start := Colon + 1;
  end;
  if (Start + 1 <= Length(Uri)) and (Uri[Start] = '/') and (Uri[Start + 1] = '/') then begin
    Inc(Start, 2);
    Stop := Length(Uri) + 1;
    for I := Start to Length(Uri) do begin
      if (Uri[I] = '/') or (Uri[I] = '?') then begin
        Stop := I;
        Break;
      end;
    end;
    Result.Authority := Slice(Uri, Start, Stop);
    Result.HasAuthority := True;
    Start := Stop;
  end;
  Q := IndexByte(Uri, '?', Start);
  if Q > 0 then begin
    Result.Path := Slice(Uri, Start, Q);
    Result.Query := Slice(Uri, Q + 1, Length(Uri) + 1);
    Result.HasQuery := True;
  end else Result.Path := Slice(Uri, Start, Length(Uri) + 1);
end;

function SchemeEnd(const S: UTF8String): Int32;
var
  I: Int32;
  B: Byte;
begin
  Result := 0;
  if (Length(S) = 0) or not IsASCIILetter(Ord(S[1])) then Exit;
  for I := 2 to Length(S) do begin
    B := Ord(S[I]);
    if B = Ord(':') then begin
      Result := I;
      Exit;
    end;
    if not (IsASCIILetter(B) or IsASCIIDigit(B) or (B = Ord('+')) or (B = Ord('-')) or (B = Ord('.'))) then Exit;
  end;
end;

function AsciiLower(const S: UTF8String): UTF8String;
var
  I, J: Int32;
begin
  for I := 1 to Length(S) do begin
    if (S[I] >= 'A') and (S[I] <= 'Z') then begin
      Result := Slice(S, 1, Length(S) + 1);
      for J := I to Length(Result) do begin
        if (Result[J] >= 'A') and (Result[J] <= 'Z') then Result[J] := AnsiChar(Ord(Result[J]) + (Ord('a') - Ord('A')));
      end;
      Exit;
    end;
  end;
  Result := S;
end;

function HasScheme(const Reference: UTF8String): Boolean;
begin
  Result := SchemeEnd(Reference) > 0;
end;

procedure SplitFragment(const Reference: UTF8String; out Left, Fragment: UTF8String);
var
  I: Int32;
begin
  I := IndexByte(Reference, '#', 1);
  if I > 0 then begin
    Left := Slice(Reference, 1, I);
    Fragment := Slice(Reference, I + 1, Length(Reference) + 1);
  end else begin
    Left := Reference;
    Fragment := '';
  end;
end;

function UriPartsToString(const P: TUriParts): UTF8String;
begin
  Result := '';
  if P.HasScheme then Result := Result + P.Scheme + Colon;
  if P.HasAuthority then Result := Result + DoubleSlash + P.Authority;
  Result := Result + P.Path;
  if P.HasQuery then Result := Result + QuestionMark + P.Query;
end;

function RemoveDotSegments(const Path: UTF8String): UTF8String;
var
  Input, Output: TUtf8Strings;
  Count, OutCount, I, Start, Last: Int32;
  Seg: UTF8String;
begin
  if IndexByte(Path, '.', 1) = 0 then begin
    Result := Path;
    Exit;
  end;
  // The segments between the slashes: one more than there are slashes.
  Count := 1;
  for I := 1 to Length(Path) do begin
    if Path[I] = '/' then Inc(Count);
  end;
  SetLength(Input, Count);
  Count := 0;
  Start := 1;
  for I := 1 to Length(Path) do begin
    if Path[I] = '/' then begin
      Input[Count] := Slice(Path, Start, I);
      Inc(Count);
      Start := I + 1;
    end;
  end;
  Input[Count] := Slice(Path, Start, Length(Path) + 1);
  Inc(Count);
  SetLength(Output, Count);
  OutCount := 0;
  Last := Count - 1;
  for I := 0 to Last do begin
    Seg := Input[I];
    if (Length(Seg) = 1) and (Seg[1] = '.') then begin
      if I = Last then begin
        Output[OutCount] := '';
        Inc(OutCount);
      end;
    end else if (Length(Seg) = 2) and (Seg[1] = '.') and (Seg[2] = '.') then begin
      if (OutCount > 1) or ((OutCount = 1) and (Length(Output[0]) <> 0)) then Dec(OutCount);
      if I = Last then begin
        Output[OutCount] := '';
        Inc(OutCount);
      end;
    end else begin
      Output[OutCount] := Seg;
      Inc(OutCount);
    end;
  end;
  Result := '';
  for I := 0 to OutCount - 1 do begin
    if I > 0 then Result := Result + Slash;
    Result := Result + Output[I];
  end;
end;

function MergePaths(const Base: TUriParts; const RefPath: UTF8String): UTF8String;
var
  I: Int32;
begin
  if Base.HasAuthority and (Length(Base.Path) = 0) then begin
    Result := Slash + RefPath;
    Exit;
  end;
  I := LastIndexByte(Base.Path, '/');
  if I > 0 then Result := Slice(Base.Path, 1, I + 1) + RefPath
  else Result := RefPath;
end;

function NormalizeParts(const Parts: TUriParts): UTF8String;
var
  P: TUriParts;
  A: UTF8String;
begin
  P := Parts;
  if P.HasScheme then P.Scheme := AsciiLower(P.Scheme);
  P.Path := RemoveDotSegments(P.Path);
  if P.HasAuthority then begin
    A := AsciiLower(P.Authority);
    if Utf8Equal(P.Scheme, 'http') then A := TrimSuffix(A, ':80')
    else if Utf8Equal(P.Scheme, 'https') then A := TrimSuffix(A, ':443');
    P.Authority := A;
    if Length(P.Path) = 0 then P.Path := Slash;
  end;
  Result := UriPartsToString(P);
end;

function NormalizeURI(const Uri: UTF8String): UTF8String;
var
  U, Fragment: UTF8String;
begin
  SplitFragment(Uri, U, Fragment);
  if not HasScheme(U) then begin
    Result := U;
    Exit;
  end;
  Result := NormalizeParts(ParseURI(U));
end;

function ResolveURI(const BaseURI, Reference: UTF8String): UTF8String;
var
  R, B, T: TUriParts;
begin
  if Length(Reference) = 0 then begin
    Result := BaseURI;
    Exit;
  end;
  R := ParseURI(Reference);
  if R.HasScheme then begin
    Result := NormalizeParts(R);
    Exit;
  end;
  if Length(BaseURI) = 0 then begin
    Result := Reference;
    Exit;
  end;
  B := ParseURI(BaseURI);
  T.Scheme := B.Scheme;
  T.HasScheme := B.HasScheme;
  T.Authority := '';
  T.HasAuthority := False;
  T.Path := '';
  T.Query := '';
  T.HasQuery := False;
  if R.HasAuthority then begin
    T.Authority := R.Authority;
    T.HasAuthority := True;
    T.Path := RemoveDotSegments(R.Path);
    T.Query := R.Query;
    T.HasQuery := R.HasQuery;
  end else begin
    if Length(R.Path) = 0 then begin
      T.Path := B.Path;
      if R.HasQuery then begin
        T.Query := R.Query;
        T.HasQuery := True;
      end else begin
        T.Query := B.Query;
        T.HasQuery := B.HasQuery;
      end;
    end else begin
      if R.Path[1] = '/' then T.Path := RemoveDotSegments(R.Path)
      else T.Path := RemoveDotSegments(MergePaths(B, R.Path));
      T.Query := R.Query;
      T.HasQuery := R.HasQuery;
    end;
    T.Authority := B.Authority;
    T.HasAuthority := B.HasAuthority;
  end;
  if B.HasScheme then Result := NormalizeParts(T)
  else Result := UriPartsToString(T);
end;

function HexValue(C: Byte): Int32;
begin
  if (C >= Ord('0')) and (C <= Ord('9')) then Result := C - Ord('0')
  else if (C >= Ord('a')) and (C <= Ord('f')) then Result := C - Ord('a') + 10
  else if (C >= Ord('A')) and (C <= Ord('F')) then Result := C - Ord('A') + 10
  else Result := -1;
end;

function DecodeFragment(const Fragment: UTF8String): UTF8String;
var
  Decoded: UTF8String;
  I, Count, H, L: Int32;
begin
  Result := Fragment;
  if IndexByte(Fragment, '%', 1) = 0 then Exit;
  Decoded := '';
  SetLength(Decoded, Length(Fragment));
  Count := 0;
  I := 1;
  while I <= Length(Fragment) do begin
    if Fragment[I] = '%' then begin
      if I + 2 > Length(Fragment) then Exit;
      H := HexValue(Ord(Fragment[I + 1]));
      L := HexValue(Ord(Fragment[I + 2]));
      if (H < 0) or (L < 0) then Exit;
      Inc(Count);
      Decoded[Count] := AnsiChar(H * 16 + L);
      Inc(I, 3);
      Continue;
    end;
    Inc(Count);
    Decoded[Count] := Fragment[I];
    Inc(I);
  end;
  SetLength(Decoded, Count);
  if not Utf8Valid(Decoded) then Exit;
  Result := Decoded;
end;

function EscapePointerToken(const Token: UTF8String): UTF8String;
begin
  if (IndexByte(Token, '~', 1) = 0) and (IndexByte(Token, '/', 1) = 0) then begin
    Result := Token;
    Exit;
  end;
  Result := ReplaceByte(ReplaceByte(Token, '~', Tilde0), '/', Tilde1);
end;

function UnescapePointerToken(const Raw: UTF8String): UTF8String;
begin
  if IndexByte(Raw, '~', 1) = 0 then begin
    Result := Raw;
    Exit;
  end;
  Result := ReplacePair(ReplacePair(Raw, '1', Slash), '0', Tilde);
end;

function PointerArrayIndex(const Token: UTF8String; out Index: Int32): Boolean;
var
  I: Int32;
  Value: Int64;
begin
  Result := False;
  Index := -1;
  if Length(Token) = 0 then Exit;
  if (Token[1] = '0') and (Length(Token) > 1) then Exit;
  Value := 0;
  for I := 1 to Length(Token) do begin
    if not IsASCIIDigit(Ord(Token[I])) then Exit;
    Value := Value * 10 + (Ord(Token[I]) - Ord('0'));
    // No array has an index beyond the range of Int32, so a larger one is refused before it can overflow.
    if Value > High(Int32) then Exit;
  end;
  Index := Int32(Value);
  Result := True;
end;

function ResolvePointer(const D: TDocument; Root: Int32; const Pointer: UTF8String; out Path: UTF8String): Int32;
var
  Current, Start, Stop, Index: Int32;
  Token, Built: UTF8String;
begin
  Path := '';
  if Length(Pointer) = 0 then begin
    Result := Root;
    Exit;
  end;
  Result := -1;
  if Pointer[1] <> '/' then Exit;
  Current := Root;
  Built := '';
  // The tokens are the text after the first "/", split at every other one.
  Start := 2;
  while True do begin
    Stop := IndexByte(Pointer, '/', Start);
    if Stop = 0 then Stop := Length(Pointer) + 1;
    Token := UnescapePointerToken(Slice(Pointer, Start, Stop));
    case DocKind(D, Current) of
      KindArray:
        begin
          if not PointerArrayIndex(Token, Index) or (Index >= DocCount(D, Current)) then Exit;
          Current := DocFirst(D, Current) + Index;
          Built := Built + Slash + Token;
        end;
      KindObject:
        begin
          Current := DocProperty(D, Current, Token);
          if Current < 0 then Exit;
          Built := Built + Slash + EscapePointerToken(Token);
        end;
    else
      Exit;
    end;
    if Stop > Length(Pointer) then Break;
    Start := Stop + 1;
  end;
  Path := Built;
  Result := Current;
end;

end.
