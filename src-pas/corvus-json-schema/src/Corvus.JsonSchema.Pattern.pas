unit Corvus.JsonSchema.Pattern;

{$I corvus.inc}

(* pattern and patternProperties matching with ECMA-262 semantics (the u flag), as JSON Schema specifies. A port of
  pattern.go and pattern_engine.go of the Go module.

  A pattern gets the cheapest matcher that decides it exactly:

    - patterns every string matches ("", ".*", "[\s\S]*" and the like), ".+", and line lengths ("^.{1,256}$");
    - anchored sequences of quantified ASCII character classes and literals ("^[a-z][a-z0-9_]{0,29}$", "^x-",
      "^[@$_#]"), matched in one pass over the string;
    - alternatives of such sequences once groups are multiplied out ("^([a|A]uto)|([n|N]one)$"), sets of literals,
      and separated lists ("^([a-z]+)(\.[a-z]+)*$");
    - anything else by the regular expression engine (Corvus.JsonSchema.EcmaRegex).

  A compiled pattern is a value that nothing writes to: it needs no Free, and threads may match with one at the
  same time. The text matched is the bytes S[Start .. Start+Len-1], never a copy.

  The Go source keeps every compiled pattern in a cache for the whole process. This unit has no such cache:
  CompilePattern compiles the pattern it is given each time, and the schema compiler shares one matcher between the
  identical patterns of a schema (see Corvus.JsonSchema.Compiler).

  The pattern's own text is read here with Go's positions (from 0): ByteAt and Slice read a string as the Go source
  reads its strings. Where the Go source reads a pattern as code points (to multiply groups out), this unit reads
  bytes. Every character that is syntax is ASCII and no byte of a longer character is, so the texts built are the
  same. *)

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.EcmaRegex;

type
  TMatcherKind = (
    { MatchEverything matches every string. }
    MatchEverything,
    { MatchLiteral is "^literal" (with "$": the whole string). }
    MatchLiteral,
    MatchSequence,
    MatchSeparatedList,
    { MatchHasContent is ".+" ("^.+" with start): some (the first) character is not a line terminator. }
    MatchHasContent,
    (* MatchLine is "^.{min,max}$": between min and max characters, none a line terminator. *)
    MatchLine,
    { MatchLiterals is "^(a|b|...)$": one of a set of strings. }
    MatchLiterals,
    { MatchAlternatives is top-level alternatives of literals or class sequences, each optionally anchored. }
    MatchAlternatives,
    { MatchExcludedClassWithWord is "^(?=[^SET]+$)(?=(.*\w)).+$": a non-empty line without a character of the
      set, containing a word character. With bangs ("^(?=!+[^SET]+$)..."), that line follows one or more "!". }
    MatchExcludedClassWithWord,
    MatchEngine);

  { TCharSet is a set of characters: ASCII by bit mask, then either all or none of the line separators U+2028 and
    U+2029, and either all or none of the other non-ASCII characters. }
  TCharSet = record
    Ascii: array[0..1] of UInt64;
    NonASCII: Boolean;
    Separators: Boolean;
  end;

  TSequenceItem = record
    Chars: TCharSet;
    Min: UInt32;
    { MaxCount: unbounded. }
    Max: UInt32;
  end;
  TSequenceItemArray = array of TSequenceItem;

  { TSequence is "^" then quantified character sets, optionally "$". Matched greedily, which is exact because
    every variable item's set is disjoint from the next item's (a character the item leaves cannot be taken by the
    next one either). Where it is not, a sequence anchored at both ends with a single variable item is still
    decided in one pass: the string's length fixes how many characters that item takes (pinned). }
  TSequence = record
    Items: TSequenceItemArray;
    ToEnd: Boolean;
    { The index of the one variable item of a sequence matched by length, or -1 for greedy matching. }
    Pinned: Int32;
    { For a pinned sequence, the characters the other items take. }
    FixedWidth: Int32;
  end;

  { TSeparatedList is a list of items between separators: "^I(SR)*$" or "^I(SR)+$" (no final), or "^(RS)*F$" and
    "^(RS)+F$". The separator is a fixed sequence and no variable class can run into what follows it, so greedy
    matching splits the string exactly where the pattern does. }
  TSeparatedList = record
    { Whether the list has the first form (a nil first in the Go source says it has not). }
    HasFirst: Boolean;
    { I of the first form. }
    First: TSequence;
    Repeated: TSequence;
    Separator: TSequence;
    { F of the second form, matched against the whole remainder. }
    Final: TSequence;
    MinRepeats: UInt32;
  end;

  { TAlternative is one alternative of MatchAlternatives: a literal anchored at either end (or neither), a class
    sequence anchored at the start, or a class sequence of fixed width anchored at the end or not at all. }
  TAlternative = record
    Kind: Byte;
    { AltLiteral. }
    Text: TBytes;
    AtStart, AtEnd: Boolean;
    Seq: TSequence;
    { AltEnd, AltAnywhere: the sequence's width in characters. }
    Width: Int32;
  end;
  TAlternativeArray = array of TAlternative;

  TBytesArray = array of TBytes;

  { TPattern is a compiled pattern. }
  TPattern = record
    Source: UTF8String;
    Kind: TMatcherKind;
    { MatchLiteral: the text, and whether it is the whole string. }
    Text: TBytes;
    Whole: Boolean;
    { MatchHasContent: anchored at the start. MatchExcludedClassWithWord: bangs. }
    Flag: Boolean;
    { MatchLine. }
    Min, Max: UInt32;
    Seq: TSequence;
    List: TSeparatedList;
    { MatchLiterals: the strings. More than eight are found through Many (a map in the Go source): a hash table
      with open addressing, each slot the index + 1 of a string of Few, or 0. Its length is a power of two. }
    Few: TBytesArray;
    Many: TInt32Array;
    Alts: TAlternativeArray;
    Chars: TCharSet;
    Engine: TEcmaRegex;
  end;

const
  { The count of an item that is not bounded. }
  MaxCount = UInt32($FFFFFFFF);

  AltLiteral = 0;
  { AltStart is "^sequence" (with "$" when the sequence runs to the end). }
  AltStart = 1;
  { AltEnd is "sequence$" of width characters: matched over the string's last width characters. }
  AltEnd = 2;
  { AltAnywhere is an unanchored sequence of width characters: matched at each position. }
  AltAnywhere = 3;

{ CompilePattern compiles a pattern. False when it is not a valid ECMA-262 regular expression. }
function CompilePattern(const Source: UTF8String; out P: TPattern): Boolean;
{ ValidRegex reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format, and
  the patterns the engine reads without falling back). }
function ValidRegex(const Source: UTF8String): Boolean;
{ PatternMatch reports whether the pattern matches somewhere in S[Start .. Start+Len-1], Ascii saying whether those
  bytes are known to be ASCII (a document knows that of its strings). }
function PatternMatch(const P: TPattern; const S: TBytes; Start, Len: Int32; Ascii: Boolean): Boolean;
{ PatternMatchString is PatternMatch for a string that is not known to be ASCII. }
function PatternMatchString(const P: TPattern; const S: UTF8String): Boolean;
{ IsASCII says whether every byte of S[Start .. Stop-1] is ASCII. }
function IsASCII(const S: TBytes; Start, Stop: Int32): Boolean;

implementation

uses
  Corvus.JsonSchema.EcmaRegex.Utf8;

const
  { At most this many alternatives after expanding groups. }
  MaxAlternatives = 64;
  { At most this many alternatives when any is a class sequence: beyond it, trying each in turn is slower than one
    pass of the engine. }
  MaxSequenceAlternatives = 4;

var
  DigitSet: TCharSet;
  { WordSet is "\w". }
  WordSet: TCharSet;

type
  TTextArray = array of UTF8String;

{ --------------------------------------------------------------------------------------------------------------------
  Reading a pattern's text as the Go source does }

{ ByteAt is the byte of S at the Go position I (from 0). }
function ByteAt(const S: UTF8String; I: Int32): Byte; inline;
begin
  Result := Byte(S[I + 1]);
end;

{ ByteOr is the byte of S at the Go position I, or -1 beyond its end (runeAt of the Go source). }
function ByteOr(const S: UTF8String; I: Int32): Int32; inline;
begin
  if I < Length(S) then
    Result := Byte(S[I + 1])
  else
    Result := -1;
end;

{ Slice is S[From:Stop] of Go. }
function Slice(const S: UTF8String; From, Stop: Int32): UTF8String; inline;
begin
  Result := Copy(S, From + 1, Stop - From);
end;

function HasPrefix(const S, Prefix: UTF8String): Boolean;
var
  K: Int32;
begin
  Result := False;
  if Length(Prefix) > Length(S) then
    Exit;
  for K := 1 to Length(Prefix) do
    if S[K] <> Prefix[K] then
      Exit;
  Result := True;
end;

function HasSuffix(const S, Suffix: UTF8String): Boolean;
var
  K, D: Int32;
begin
  Result := False;
  D := Length(S) - Length(Suffix);
  if D < 0 then
    Exit;
  for K := 1 to Length(Suffix) do
    if S[D + K] <> Suffix[K] then
      Exit;
  Result := True;
end;

{ CutPrefix is S without the prefix, when it has it. Otherwise S and False. }
function CutPrefix(const S, Prefix: UTF8String; out Rest: UTF8String): Boolean;
begin
  Result := HasPrefix(S, Prefix);
  if Result then
    Rest := Copy(S, Length(Prefix) + 1, Length(S) - Length(Prefix))
  else
    Rest := S;
end;

function CutSuffix(const S, Suffix: UTF8String; out Rest: UTF8String): Boolean;
begin
  Result := HasSuffix(S, Suffix);
  if Result then
    Rest := Copy(S, 1, Length(S) - Length(Suffix))
  else
    Rest := S;
end;

{ IsOneOf says whether C is one of the characters of Chars. }
function IsOneOf(C: Byte; const Chars: UTF8String): Boolean;
var
  K: Int32;
begin
  Result := True;
  for K := 1 to Length(Chars) do
    if Byte(Chars[K]) = C then
      Exit;
  Result := False;
end;

{ AppendByte appends one byte to a text, as it is. }
procedure AppendByte(var S: UTF8String; C: Byte);
var
  N: Int32;
begin
  N := Length(S);
  SetLength(S, N + 1);
  S[N + 1] := AnsiChar(C);
end;

function IsDigitByte(C: Byte): Boolean; inline;
begin
  Result := (C >= Ord('0')) and (C <= Ord('9'));
end;

{ --------------------------------------------------------------------------------------------------------------------
  Characters }

{ DecodeRuneAt reads the character at I (a byte for ASCII). }
function DecodeRuneAt(const S: TBytes; I, Stop: Int32; out Size: Int32): Int32; inline;
begin
  Result := S[I];
  if Result < RuneSelf then
    Size := 1
  else
    Result := DecodeRune(S, I, Stop, Size);
end;

function IsASCII(const S: TBytes; Start, Stop: Int32): Boolean;
var
  K: Int32;
begin
  Result := False;
  for K := Start to Stop - 1 do
    if S[K] >= RuneSelf then
      Exit;
  Result := True;
end;

function TextIsASCII(const S: UTF8String): Boolean;
var
  K: Int32;
begin
  Result := False;
  for K := 1 to Length(S) do
    if Byte(S[K]) >= RuneSelf then
      Exit;
  Result := True;
end;

{ RuneCount is the number of characters of S[Start .. Stop-1] (a byte that starts no character counts as one). }
function RuneCount(const S: TBytes; Start, Stop: Int32): Int32;
var
  Size: Int32;
begin
  Result := 0;
  while Start < Stop do begin
    DecodeRuneAt(S, Start, Stop, Size);
    Inc(Start, Size);
    Inc(Result);
  end;
end;

{ IsLineTerminator reports an ECMA-262 LineTerminator, which "." does not match. }
function IsLineTerminator(C: Int32): Boolean; inline;
begin
  Result := (C = 10) or (C = 13) or (C = $2028) or (C = $2029);
end;

{ LineLength is the number of characters, when none is a line terminator, else -1. }
function LineLength(const S: TBytes; Start, Stop: Int32): Int32;
var
  C, Size: Int32;
begin
  Result := 0;
  while Start < Stop do begin
    C := DecodeRuneAt(S, Start, Stop, Size);
    if IsLineTerminator(C) then begin
      Result := -1;
      Exit;
    end;
    Inc(Result);
    Inc(Start, Size);
  end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Character sets }

function EmptySet: TCharSet;
begin
  Result.Ascii[0] := 0;
  Result.Ascii[1] := 0;
  Result.NonASCII := False;
  Result.Separators := False;
end;

{ CharRange is ASCII Lo to Hi inclusive (Hi below 128). }
function CharRange(Lo, Hi: Byte): TCharSet;
var
  C: Int32;
begin
  Result := EmptySet;
  for C := Lo to Hi do
    Result.Ascii[C shr 6] := Result.Ascii[C shr 6] or (UInt64(1) shl (C and 63));
end;

{ DotSet is ECMA-262's ".": everything but the line terminators. }
function DotSet: TCharSet;
begin
  Result.Ascii[0] := UInt64($FFFFFFFFFFFFFFFF) and not ((UInt64(1) shl 10) or (UInt64(1) shl 13));
  Result.Ascii[1] := UInt64($FFFFFFFFFFFFFFFF);
  Result.NonASCII := True;
  Result.Separators := False;
end;

function SetUnion(const S, Other: TCharSet): TCharSet;
begin
  Result.Ascii[0] := S.Ascii[0] or Other.Ascii[0];
  Result.Ascii[1] := S.Ascii[1] or Other.Ascii[1];
  Result.NonASCII := S.NonASCII or Other.NonASCII;
  Result.Separators := S.Separators or Other.Separators;
end;

function SetNegate(const S: TCharSet): TCharSet;
begin
  Result.Ascii[0] := not S.Ascii[0];
  Result.Ascii[1] := not S.Ascii[1];
  Result.NonASCII := not S.NonASCII;
  Result.Separators := not S.Separators;
end;

function SetDisjoint(const S, Other: TCharSet): Boolean;
begin
  Result := (S.Ascii[0] and Other.Ascii[0] = 0) and (S.Ascii[1] and Other.Ascii[1] = 0)
    and not (S.NonASCII and Other.NonASCII) and not (S.Separators and Other.Separators);
end;

{ SetHasASCII reports whether the set holds an ASCII character. }
function SetHasASCII(const S: TCharSet; C: Byte): Boolean; inline;
begin
  Result := (S.Ascii[(C shr 6) and 1] shr (C and 63)) and 1 <> 0;
end;

function SetContains(const S: TCharSet; C: Int32): Boolean; inline;
begin
  if C < 128 then
    Result := SetHasASCII(S, Byte(C))
  else if (C = $2028) or (C = $2029) then
    Result := S.Separators
  else
    Result := S.NonASCII;
end;

function OnesCount(V: UInt64): Int32;
begin
  Result := 0;
  while V <> 0 do begin
    V := V and (V - UInt64(1));
    Inc(Result);
  end;
end;

function TrailingZeros(V: UInt64): Int32;
begin
  Result := 0;
  while (Result < 64) and (V and 1 = 0) do begin
    V := V shr 1;
    Inc(Result);
  end;
end;

{ SetSingle is the set's character when it holds exactly one ASCII character and nothing else. }
function SetSingle(const S: TCharSet; out C: Byte): Boolean;
begin
  C := 0;
  Result := False;
  if S.NonASCII or S.Separators or (OnesCount(S.Ascii[0]) + OnesCount(S.Ascii[1]) <> 1) then
    Exit;
  if S.Ascii[0] <> 0 then
    C := Byte(TrailingZeros(S.Ascii[0]))
  else
    C := Byte(64 + TrailingZeros(S.Ascii[1]));
  Result := True;
end;

function IsASCIIPunctuation(C: Byte): Boolean;
begin
  Result := ((C >= Ord('!')) and (C <= Ord('/'))) or ((C >= Ord(':')) and (C <= Ord('@')))
    or ((C >= Ord('[')) and (C <= Ord('`'))) or ((C >= Ord('{')) and (C <= Ord('~')));
end;

{ ClassEscape is the set of a class escape ("\d", "\w", their negations, or an escaped punctuation character). Not
  ok for "\s" (not ASCII-only) and anything else. }
function ClassEscape(C: Byte; out S: TCharSet): Boolean;
begin
  Result := True;
  case C of
    Ord('d'): S := DigitSet;
    Ord('D'): S := SetNegate(DigitSet);
    Ord('w'): S := WordSet;
    Ord('W'): S := SetNegate(WordSet);
    Ord('n'): S := CharRange(10, 10);
    Ord('r'): S := CharRange(13, 13);
    Ord('t'): S := CharRange(9, 9);
  else
    if IsASCIIPunctuation(C) then
      S := CharRange(C, C)
    else begin
      S := EmptySet;
      Result := False;
    end;
  end;
end;

{ ParseClass reads a class body from after "[" to after "]", with ASCII members (a negated class also takes every
  non-ASCII character). I is the position after "[", and Next the position after "]". }
function ParseClass(const B: UTF8String; I: Int32; out Chars: TCharSet; out Next: Int32): Boolean;
var
  Negated, First, Single: Boolean;
  C, E, Lo, Hi: Byte;
  Atom: TCharSet;
begin
  Result := False;
  Next := 0;
  Chars := EmptySet;
  Negated := (I < Length(B)) and (ByteAt(B, I) = Ord('^'));
  if Negated then
    Inc(I);
  First := True;
  while True do begin
    if I >= Length(B) then
      Exit;
    C := ByteAt(B, I);
    if C = Ord(']') then begin
      if First then
        Exit; { "[]" and "[^]" }
      Inc(I);
      Break;
    end;
    First := False;
    { The set holds ASCII members only. A member outside ASCII leaves the pattern to the engine. (This is tested
      before the set is built: a byte of 128 or more is not a bit of it.) }
    if C >= RuneSelf then
      Exit;
    { One atom: a single character (for ranges) or a set escape. }
    Atom := CharRange(C, C);
    Lo := C;
    Single := True;
    if C = Ord('\') then begin
      if I + 1 >= Length(B) then
        Exit;
      E := ByteAt(B, I + 1);
      if not ClassEscape(E, Atom) then
        Exit;
      Single := SetSingle(Atom, Lo);
      Single := Single and ((E = Ord('n')) or (E = Ord('r')) or (E = Ord('t')) or IsASCIIPunctuation(E));
      Inc(I, 2);
    end else
      Inc(I);
    if (I + 1 < Length(B)) and (ByteAt(B, I) = Ord('-')) and (ByteAt(B, I + 1) <> Ord(']')) then begin
      if not Single then
        Exit;
      Hi := ByteAt(B, I + 1);
      if Hi = Ord('\') then begin
        if (I + 2 >= Length(B)) or not IsASCIIPunctuation(ByteAt(B, I + 2)) then
          Exit;
        Hi := ByteAt(B, I + 2);
        Inc(I, 3);
      end else
        Inc(I, 2);
      if (Lo > Hi) or (Hi >= RuneSelf) then
        Exit;
      Chars := SetUnion(Chars, CharRange(Lo, Hi));
    end else
      Chars := SetUnion(Chars, Atom);
  end;
  if Negated then
    Chars := SetNegate(Chars);
  Next := I;
  Result := True;
end;

{ ParseCount reads a decimal count that fits 32 bits. }
function ParseCount(const S: UTF8String; out Count: UInt32): Boolean;
var
  N: UInt64;
  I: Int32;
begin
  Count := 0;
  Result := False;
  if (S = '') or (Length(S) > 10) then
    Exit;
  N := 0;
  for I := 0 to Length(S) - 1 do begin
    if not IsDigitByte(ByteAt(S, I)) then
      Exit;
    N := N * UInt64(10) + UInt64(ByteAt(S, I) - Ord('0'));
  end;
  if N > UInt64(MaxCount) then
    Exit;
  Count := UInt32(N);
  Result := True;
end;

(* ParseQuantifier reads an optional quantifier ("*", "+", "?", "{n}", "{n,}", "{n,m}", each optionally lazy) at
  I. *)
function ParseQuantifier(const B: UTF8String; I: Int32; out Min, Max: UInt32; out Next: Int32): Boolean;
var
  Stop, Comma, K: Int32;
  Body, Lo, Hi: UTF8String;
begin
  Result := False;
  Min := 1;
  Max := 1;
  Next := I;
  if I >= Length(B) then begin
    Result := True;
    Exit;
  end;
  case ByteAt(B, I) of
    Ord('*'): begin
      Inc(I);
      Min := 0;
      Max := MaxCount;
    end;
    Ord('+'): begin
      Inc(I);
      Min := 1;
      Max := MaxCount;
    end;
    Ord('?'): begin
      Inc(I);
      Min := 0;
      Max := 1;
    end;
    Ord('{'): begin
      Stop := -1;
      for K := I to Length(B) - 1 do
        if ByteAt(B, K) = Ord('}') then begin
          Stop := K;
          Break;
        end;
      if Stop < 0 then
        Exit;
      Body := Slice(B, I + 1, Stop);
      I := Stop + 1;
      Comma := -1;
      Hi := '';
      for K := 0 to Length(Body) - 1 do
        if ByteAt(Body, K) = Ord(',') then begin
          Comma := K;
          Break;
        end;
      if Comma < 0 then
        Lo := Body
      else begin
        Lo := Slice(Body, 0, Comma);
        Hi := Slice(Body, Comma + 1, Length(Body));
      end;
      if not ParseCount(Lo, Min) then
        Exit;
      if Comma < 0 then
        Max := Min
      else if Hi = '' then
        Max := MaxCount
      else if not ParseCount(Hi, Max) then
        Exit;
    end;
  else
    Result := True;
    Exit;
  end;
  if Min > Max then
    Exit;
  if (I < Length(B)) and (ByteAt(B, I) = Ord('?')) then
    Inc(I); { Laziness does not change whether the whole pattern matches. }
  Next := I;
  Result := True;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Class sequences }

{ GreedyBefore reports whether greedy matching is exact when Next can follow the items Items[From .. Stop-1]:
  every variable item is disjoint from the items that can directly follow it (up to the first that cannot match
  nothing), Next included. }
function GreedyBefore(const Items: TSequenceItemArray; From, Stop: Int32; const Next: TCharSet): Boolean;
var
  I, J: Int32;
  Settled: Boolean;
begin
  Result := False;
  for I := From to Stop - 1 do begin
    if Items[I].Min = Items[I].Max then
      Continue;
    Settled := False;
    for J := I + 1 to Stop - 1 do begin
      if not SetDisjoint(Items[I].Chars, Items[J].Chars) then
        Exit;
      if Items[J].Min > 0 then begin
        Settled := True;
        Break;
      end;
    end;
    if Settled then
      Continue;
    if not SetDisjoint(Items[I].Chars, Next) then
      Exit;
  end;
  Result := True;
end;

procedure ClearSequence(out Seq: TSequence);
begin
  Seq.Items := nil;
  Seq.ToEnd := False;
  Seq.Pinned := -1;
  Seq.FixedWidth := 0;
end;

function ParseSequence(const P: UTF8String; out Seq: TSequence): Boolean;
var
  I, Next, Count, Pinned, K: Int32;
  Items: TSequenceItemArray;
  ToEnd: Boolean;
  Chars: TCharSet;
  C: Byte;
  Min, Max: UInt32;
  FixedWidth: UInt64;
begin
  Result := False;
  ClearSequence(Seq);
  if (Length(P) = 0) or (ByteAt(P, 0) <> Ord('^')) or not TextIsASCII(P) then
    Exit;
  I := 1;
  Items := nil;
  Count := 0;
  ToEnd := False;
  while I < Length(P) do begin
    C := ByteAt(P, I);
    case C of
      Ord('$'): begin
        if I <> Length(P) - 1 then
          Exit;
        ToEnd := True;
        Inc(I);
        Continue;
      end;
      Ord('['): begin
        if not ParseClass(P, I + 1, Chars, Next) then
          Exit;
        I := Next;
      end;
      Ord('\'): begin
        if I + 1 >= Length(P) then
          Exit;
        if not ClassEscape(ByteAt(P, I + 1), Chars) then
          Exit;
        Inc(I, 2);
      end;
      Ord('.'): begin
        Inc(I);
        Chars := DotSet;
      end;
      Ord('('), Ord(')'), Ord('|'), Ord('^'), Ord('*'), Ord('+'), Ord('?'), Ord('{'), Ord('}'), Ord(']'):
        Exit;
    else
      Inc(I);
      Chars := CharRange(C, C);
    end;
    if not ParseQuantifier(P, I, Min, Max, Next) then
      Exit;
    I := Next;
    if Count = Length(Items) then
      SetLength(Items, Count * 2 + 8);
    Items[Count].Chars := Chars;
    Items[Count].Min := Min;
    Items[Count].Max := Max;
    Inc(Count);
  end;
  SetLength(Items, Count);
  if GreedyBefore(Items, 0, Count, EmptySet) then begin
    Seq.Items := Items;
    Seq.ToEnd := ToEnd;
    Seq.Pinned := -1;
    Result := True;
    Exit;
  end;
  { Greedy matching is not exact. With both ends anchored and one variable item, the length decides. }
  if not ToEnd then
    Exit;
  Pinned := -1;
  FixedWidth := 0;
  for K := 0 to Count - 1 do
    if Items[K].Min = Items[K].Max then
      FixedWidth := FixedWidth + UInt64(Items[K].Min)
    else if Pinned >= 0 then
      Exit
    else
      Pinned := K;
  if FixedWidth > UInt64(High(Int32)) then
    Exit;
  Seq.Items := Items;
  Seq.ToEnd := True;
  Seq.Pinned := Pinned;
  Seq.FixedWidth := Int32(FixedWidth);
  Result := True;
end;

{ SequenceLiteral is the text, when every item is one fixed character. }
function SequenceLiteral(const Q: TSequence; out Text: TBytes): Boolean;
var
  I: Int32;
  C: Byte;
begin
  Result := False;
  Text := nil;
  SetLength(Text, Length(Q.Items));
  for I := 0 to Length(Q.Items) - 1 do begin
    if not SetSingle(Q.Items[I].Chars, C) or (Q.Items[I].Min <> 1) or (Q.Items[I].Max <> 1) then begin
      Text := nil;
      Exit;
    end;
    Text[I] := C;
  end;
  Result := True;
end;

{ MatchPinned matches a pinned sequence: every item but one takes a fixed number of characters, so the string's
  length says how many the variable one takes. }
function MatchPinned(const Q: TSequence; const S: TBytes; At, Stop: Int32; Ascii: Boolean): Boolean;
var
  Len, Variable, I, C, Size: Int32;
  Count: Int64;
begin
  Result := False;
  if Ascii then
    Len := Stop - At
  else
    Len := RuneCount(S, At, Stop);
  Variable := Len - Q.FixedWidth;
  if (Variable < 0) or (Int64(Variable) < Int64(Q.Items[Q.Pinned].Min))
    or (Int64(Variable) > Int64(Q.Items[Q.Pinned].Max)) then
    Exit;
  for I := 0 to Length(Q.Items) - 1 do begin
    Count := Q.Items[I].Min;
    if I = Q.Pinned then
      Count := Variable;
    while Count > 0 do begin
      C := DecodeRuneAt(S, At, Stop, Size);
      if not SetContains(Q.Items[I].Chars, C) then
        Exit;
      Inc(At, Size);
      Dec(Count);
    end;
  end;
  Result := True;
end;

{ MatchChars matches over any text, a character at a time. }
function MatchChars(const Q: TSequence; const S: TBytes; At, Stop: Int32): Boolean;
var
  I, C, Size: Int32;
  N: UInt32;
begin
  if Q.Pinned >= 0 then begin
    Result := MatchPinned(Q, S, At, Stop, False);
    Exit;
  end;
  Result := False;
  for I := 0 to Length(Q.Items) - 1 do begin
    N := 0;
    while (N < Q.Items[I].Max) and (At < Stop) do begin
      C := DecodeRuneAt(S, At, Stop, Size);
      if not SetContains(Q.Items[I].Chars, C) then
        Break;
      Inc(At, Size);
      Inc(N);
    end;
    if N < Q.Items[I].Min then
      Exit;
  end;
  Result := not Q.ToEnd or (At = Stop);
end;

{ MatchASCII matches over ASCII text, a byte per character. }
function MatchASCII(const Q: TSequence; const B: TBytes; At, Stop: Int32): Boolean;
var
  I, Start, Limit: Int32;
begin
  if Q.Pinned >= 0 then begin
    Result := MatchPinned(Q, B, At, Stop, True);
    Exit;
  end;
  Result := False;
  for I := 0 to Length(Q.Items) - 1 do begin
    Start := At;
    Limit := Stop;
    if Int64(Q.Items[I].Max) < Int64(Stop - Start) then
      Limit := Start + Int32(Q.Items[I].Max);
    while (At < Limit) and SetHasASCII(Q.Items[I].Chars, B[At]) do
      Inc(At);
    if Int64(At - Start) < Int64(Q.Items[I].Min) then
      Exit;
  end;
  Result := not Q.ToEnd or (At = Stop);
end;

function MatchSeq(const Q: TSequence; const S: TBytes; At, Stop: Int32): Boolean;
begin
  if IsASCII(S, At, Stop) then
    Result := MatchASCII(Q, S, At, Stop)
  else
    Result := MatchChars(Q, S, At, Stop);
end;

{ Consume greedily matches the items at the start of S[At .. Stop-1] (ignoring ToEnd): the bytes taken, or -1. }
function Consume(const Q: TSequence; const S: TBytes; At, Stop: Int32): Int32;
var
  I, C, Size, From: Int32;
  N: UInt32;
begin
  From := At;
  for I := 0 to Length(Q.Items) - 1 do begin
    N := 0;
    while (N < Q.Items[I].Max) and (At < Stop) do begin
      C := DecodeRuneAt(S, At, Stop, Size);
      if not SetContains(Q.Items[I].Chars, C) then
        Break;
      Inc(At, Size);
      Inc(N);
    end;
    if N < Q.Items[I].Min then begin
      Result := -1;
      Exit;
    end;
  end;
  Result := At - From;
end;

function ListMatch(const L: TSeparatedList; const S: TBytes; At, Stop: Int32): Boolean;
var
  Repeats: UInt32;
  N, M: Int32;
begin
  Result := False;
  Repeats := 0;
  if L.HasFirst then begin
    N := Consume(L.First, S, At, Stop);
    if N < 0 then
      Exit;
    Inc(At, N);
    while True do begin
      if At = Stop then begin
        Result := Repeats >= L.MinRepeats;
        Exit;
      end;
      N := Consume(L.Separator, S, At, Stop);
      if N < 0 then
        Exit;
      Inc(At, N);
      N := Consume(L.Repeated, S, At, Stop);
      if N < 0 then
        Exit;
      Inc(At, N);
      Inc(Repeats);
    end;
  end;
  while True do begin
    if (Repeats >= L.MinRepeats) and MatchSeq(L.Final, S, At, Stop) then begin
      Result := True;
      Exit;
    end;
    N := Consume(L.Repeated, S, At, Stop);
    if N < 0 then
      Exit;
    M := Consume(L.Separator, S, At + N, Stop);
    if M < 0 then
      Exit;
    Inc(At, N + M);
    Inc(Repeats);
  end;
end;

{ StripGroup is the inside of "(...)" or "(?:...)". Not ok for other groups. }
function StripGroup(const G: UTF8String; out Inner: UTF8String): Boolean;
var
  Rest: UTF8String;
begin
  Result := False;
  if not CutPrefix(G, '(', Inner) then begin
    Inner := '';
    Exit;
  end;
  Rest := Inner;
  if not CutSuffix(Rest, ')', Inner) then begin
    Inner := '';
    Exit;
  end;
  Rest := Inner;
  if CutPrefix(Rest, '?:', Inner) then begin
    Result := True;
    Exit;
  end;
  Result := not HasPrefix(Inner, '?');
end;

{ UnwrapGroup is a text that is one group wrapping everything, unwrapped. Otherwise the text itself. }
function UnwrapGroup(const T: UTF8String; out Inner: UTF8String): Boolean;
var
  Depth, I: Int32;
  Escaped: Boolean;
  C: Byte;
begin
  if not HasPrefix(T, '(') or not HasSuffix(T, ')') then begin
    Inner := T;
    Result := True;
    Exit;
  end;
  Result := False;
  if not StripGroup(T, Inner) then begin
    Inner := '';
    Exit;
  end;
  { Only when the parentheses enclose the whole text ("(a)(b)" is two groups). }
  Depth := 0;
  Escaped := False;
  for I := 0 to Length(Inner) - 1 do begin
    C := ByteAt(Inner, I);
    if Escaped then
      Escaped := False
    else if C = Ord('\') then
      Escaped := True
    else if C = Ord('(') then
      Inc(Depth)
    else if C = Ord(')') then begin
      Dec(Depth);
      if Depth < 0 then begin
        Inner := '';
        Exit;
      end;
    end;
  end;
  Result := True;
end;

{ EndsWithEscape reports whether the text ends in an unpaired "\" (so a "$" after it would be escaped). }
function EndsWithEscape(const T: UTF8String): Boolean;
var
  N, I: Int32;
begin
  N := 0;
  I := Length(T) - 1;
  while (I >= 0) and (ByteAt(T, I) = Ord('\')) do begin
    Inc(N);
    Dec(I);
  end;
  Result := N mod 2 = 1;
end;

function GreedySequence(const Items: TSequenceItemArray; From, Stop: Int32): TSequence;
begin
  Result.Items := Copy(Items, From, Stop - From);
  Result.ToEnd := False;
  Result.Pinned := -1;
  Result.FixedWidth := 0;
end;

function FixedItems(const Items: TSequenceItemArray; From, Stop: Int32): Boolean;
var
  I: Int32;
begin
  Result := False;
  for I := From to Stop - 1 do
    if (Items[I].Min <> Items[I].Max) or (Items[I].Min = 0) then
      Exit;
  Result := Stop > From;
end;

function ParseSeparatedList(const P: UTF8String; out L: TSeparatedList): Boolean;
var
  Body, Rest, Inner, Before, After, Unwrapped: UTF8String;
  Opens, Closes: TInt32Array;
  Groups, Depth, Open, I, K, Quantified, G, GOpen, GClose, Count: Int32;
  InClass: Boolean;
  C: Byte;
  MinRepeats: UInt32;
  Seq, First, Final: TSequence;
  GroupItems: TSequenceItemArray;
begin
  Result := False;
  L.HasFirst := False;
  ClearSequence(L.First);
  ClearSequence(L.Repeated);
  ClearSequence(L.Separator);
  ClearSequence(L.Final);
  L.MinRepeats := 0;
  if not CutPrefix(P, '^', Rest) then
    Exit;
  Body := '';
  if not CutSuffix(Rest, '$', Body) or EndsWithEscape(Body) then
    Exit;
  { Top-level groups: the index of each one's "(" and ")". }
  Opens := nil;
  Closes := nil;
  Groups := 0;
  Depth := 0;
  InClass := False;
  Open := 0;
  I := 0;
  while I < Length(Body) do begin
    C := ByteAt(Body, I);
    if C = Ord('\') then
      Inc(I)
    else if (C = Ord('[')) and not InClass then
      InClass := True
    else if (C = Ord(']')) and InClass then
      InClass := False
    else if (C = Ord('(')) and not InClass then begin
      if Depth = 0 then
        Open := I;
      Inc(Depth);
    end else if (C = Ord(')')) and not InClass then begin
      if Depth = 0 then
        Exit;
      Dec(Depth);
      if Depth = 0 then begin
        SetLength(Opens, Groups + 1);
        SetLength(Closes, Groups + 1);
        Opens[Groups] := Open;
        Closes[Groups] := I;
        Inc(Groups);
      end;
    end;
    Inc(I);
  end;
  Quantified := 0;
  G := -1;
  for I := 0 to Groups - 1 do
    if (Closes[I] + 1 < Length(Body))
      and ((ByteAt(Body, Closes[I] + 1) = Ord('*')) or (ByteAt(Body, Closes[I] + 1) = Ord('+'))) then begin
      Inc(Quantified);
      G := I;
    end;
  if Quantified <> 1 then
    Exit;
  GOpen := Opens[G];
  GClose := Closes[G];
  MinRepeats := 0;
  if ByteAt(Body, GClose + 1) = Ord('+') then
    MinRepeats := 1;
  if not StripGroup(Slice(Body, GOpen, GClose + 1), Inner) then
    Exit;
  Before := Slice(Body, 0, GOpen);
  After := Slice(Body, GClose + 2, Length(Body));
  if not ParseSequence('^' + Inner, Seq) then
    Exit;
  GroupItems := Seq.Items;
  Count := Length(GroupItems);
  if Count = 0 then
    Exit;
  if (Before <> '') and (After = '') then begin
    { ^I(SR)*$ }
    if not UnwrapGroup(Before, Unwrapped) then
      Exit;
    if not ParseSequence('^' + Unwrapped, First) then
      Exit;
    for K := 1 to Count - 1 do
      { The separator is the first K items and what is repeated the rest. }
      if FixedItems(GroupItems, 0, K) and GreedyBefore(First.Items, 0, Length(First.Items), GroupItems[0].Chars)
        and GreedyBefore(GroupItems, K, Count, GroupItems[0].Chars) then begin
        L.HasFirst := True;
        L.First := GreedySequence(First.Items, 0, Length(First.Items));
        L.Repeated := GreedySequence(GroupItems, K, Count);
        L.Separator := GreedySequence(GroupItems, 0, K);
        L.MinRepeats := MinRepeats;
        Result := True;
        Exit;
      end;
  end else if (Before = '') and (After <> '') then begin
    { ^(RS)*F$ }
    if not UnwrapGroup(After, Unwrapped) then
      Exit;
    if not ParseSequence('^' + Unwrapped + '$', Final) then
      Exit;
    for K := 1 to Count - 1 do
      { What is repeated is the first K items and the separator the rest. }
      if FixedItems(GroupItems, K, Count) and GreedyBefore(GroupItems, 0, K, GroupItems[K].Chars) then begin
        L.Repeated := GreedySequence(GroupItems, 0, K);
        L.Separator := GreedySequence(GroupItems, K, Count);
        L.Final := Final;
        L.MinRepeats := MinRepeats;
        Result := True;
        Exit;
      end;
  end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Choosing a matcher }

(* LineRange reads "^.{m,n}$" (and "^.*$", "^.+$", "^.{m}$", "^.{m,}$", each also as "^(....)$"): the bounds on the
  length of a line. *)
function LineRange(const P: UTF8String; out Min, Max: UInt32): Boolean;
var
  Q, R: UTF8String;
  Grouped: Boolean;
  Next: Int32;
begin
  Result := False;
  Min := 0;
  Max := 0;
  Grouped := False;
  if not CutPrefix(P, '^.', Q) then begin
    if not CutPrefix(P, '^(.', Q) then
      Exit;
    Grouped := True;
  end;
  R := Q;
  if not CutSuffix(R, '$', Q) then
    Exit;
  if Grouped then begin
    R := Q;
    if not CutSuffix(R, ')', Q) then
      Exit;
  end;
  if not ParseQuantifier(Q, 0, Min, Max, Next) or (Next <> Length(Q)) or (Next = 0) or HasSuffix(Q, '?') then begin
    Min := 0;
    Max := 0;
    Exit;
  end;
  Result := True;
end;

{ LiteralText reads literal text: characters other than syntax characters, and identity or control escapes. }
function LiteralText(const P: UTF8String; out Text: UTF8String): Boolean;
var
  I: Int32;
  C, E: Byte;
begin
  Result := False;
  Text := '';
  I := 0;
  while I < Length(P) do begin
    C := ByteAt(P, I);
    if C = Ord('\') then begin
      Inc(I);
      if I >= Length(P) then begin
        Text := '';
        Exit;
      end;
      E := ByteAt(P, I);
      case E of
        Ord('n'): AppendByte(Text, 10);
        Ord('r'): AppendByte(Text, 13);
        Ord('t'): AppendByte(Text, 9);
        Ord('f'): AppendByte(Text, 12);
        Ord('v'): AppendByte(Text, 11);
      else
        if IsOneOf(E, '^$\.*+?()[]{}|/-') then
          AppendByte(Text, E)
        else begin
          Text := '';
          Exit;
        end;
      end;
    end else if IsOneOf(C, '^$.*+?()[]{}|') then begin
      Text := '';
      Exit;
    end else
      AppendByte(Text, C);
    Inc(I);
  end;
  Result := True;
end;

procedure AppendText(var List: TTextArray; const S: UTF8String);
var
  N: Int32;
begin
  N := Length(List);
  SetLength(List, N + 1);
  List[N] := S;
end;

{ SplitAlternatives splits at top-level "|"s (none inside a group, a class or after "\"). }
function SplitAlternatives(const P: UTF8String; out Parts: TTextArray): Boolean;
var
  Depth, Start, I: Int32;
  InClass: Boolean;
  C: Byte;
begin
  Result := False;
  Parts := nil;
  Depth := 0;
  InClass := False;
  Start := 0;
  I := 0;
  while I < Length(P) do begin
    C := ByteAt(P, I);
    if C = Ord('\') then
      Inc(I)
    else if C = Ord('[') then
      InClass := True
    else if C = Ord(']') then
      InClass := False
    else if (C = Ord('(')) and not InClass then
      Inc(Depth)
    else if (C = Ord(')')) and not InClass then begin
      if Depth = 0 then begin
        Parts := nil;
        Exit;
      end;
      Dec(Depth);
    end else if (C = Ord('|')) and not InClass and (Depth = 0) then begin
      AppendText(Parts, Slice(P, Start, I));
      Start := I + 1;
    end;
    Inc(I);
  end;
  if Start > Length(P) then
    Start := Length(P);
  AppendText(Parts, Slice(P, Start, Length(P)));
  Result := True;
end;

{ WholeAlternatives reads "^(a|b|...)$" or "^(?:a|b|...)$" over literal alternatives. }
function WholeAlternatives(const P: UTF8String; out Texts: TTextArray): Boolean;
var
  Inner, Rest: UTF8String;
  Parts: TTextArray;
  I: Int32;
begin
  Result := False;
  Texts := nil;
  if not CutPrefix(P, '^(', Rest) then
    Exit;
  if not CutSuffix(Rest, ')$', Inner) then
    Exit;
  Rest := Inner;
  CutPrefix(Rest, '?:', Inner);
  if HasPrefix(Inner, '?') then
    Exit;
  Parts := nil;
  if not SplitAlternatives(Inner, Parts) or (Length(Parts) < 2) then
    Exit;
  SetLength(Texts, Length(Parts));
  for I := 0 to Length(Parts) - 1 do
    if not LiteralText(Parts[I], Texts[I]) then begin
      Texts := nil;
      Exit;
    end;
  Result := True;
end;

{ Product is every concatenation of one of A and one of B, or not ok when there are too many. }
function Product(const A, B: TTextArray; out Res: TTextArray): Boolean;
var
  X, Y, N: Int32;
begin
  Res := nil;
  Result := False;
  if Length(A) * Length(B) > MaxAlternatives then
    Exit;
  SetLength(Res, Length(A) * Length(B));
  N := 0;
  for X := 0 to Length(A) - 1 do
    for Y := 0 to Length(B) - 1 do begin
      Res[N] := A[X] + B[Y];
      Inc(N);
    end;
  Result := True;
end;

procedure AppendAll(var Branch: TTextArray; const Text: UTF8String);
var
  B: Int32;
begin
  for B := 0 to Length(Branch) - 1 do
    Branch[B] := Branch[B] + Text;
end;

procedure AppendList(var List: TTextArray; const More: TTextArray);
var
  N, K: Int32;
begin
  N := Length(List);
  SetLength(List, N + Length(More));
  for K := 0 to Length(More) - 1 do
    List[N + K] := More[K];
end;

function OneEmpty: TTextArray;
begin
  Result := nil;
  SetLength(Result, 1);
  Result[0] := '';
end;

{ ExpandAlternatives expands the alternatives from I up to an unmatched ")" or the end: groups of alternatives
  multiply out, a group quantified by "?" also contributes the empty alternative, and everything else is copied.
  Not ok for lookarounds, other quantified groups, or too many alternatives. Next is where it stopped. }
function ExpandAlternatives(const C: UTF8String; I: Int32; out Parts: TTextArray; out Next: Int32): Boolean;
var
  All, Branch, Inner, Repeated, Multiplied: TTextArray;
  InnerNext, Close, Start: Int32;
  N: UInt32;
begin
  Result := False;
  Parts := nil;
  Next := 0;
  All := nil;
  Branch := OneEmpty;
  while I < Length(C) do begin
    case ByteAt(C, I) of
      Ord('|'): begin
        AppendList(All, Branch);
        Branch := OneEmpty;
        Inc(I);
      end;
      Ord(')'): Break;
      Ord('('): begin
        Inc(I);
        if ByteOr(C, I) = Ord('?') then begin
          if ByteOr(C, I + 1) <> Ord(':') then
            Exit;
          Inc(I, 2);
        end;
        if not ExpandAlternatives(C, I, Inner, InnerNext) or (ByteOr(C, InnerNext) <> Ord(')')) then
          Exit;
        I := InnerNext + 1;
        case ByteOr(C, I) of
          Ord('?'): begin
            AppendText(Inner, '');
            Inc(I);
            if ByteOr(C, I) = Ord('?') then
              Inc(I);
          end;
          Ord('{'): begin
            { An exact count repeats the group. Any other bound needs a real regular expression. }
            Close := I;
            while (Close < Length(C)) and (ByteAt(C, Close) <> Ord('}')) do
              Inc(Close);
            if Close = Length(C) then
              Exit;
            if not ParseCount(Slice(C, I + 1, Close), N) or (N > 16) then
              Exit;
            Repeated := OneEmpty;
            while N > 0 do begin
              if not Product(Repeated, Inner, Multiplied) then
                Exit;
              Repeated := Multiplied;
              Dec(N);
            end;
            Inner := Repeated;
            I := Close + 1;
            if ByteOr(C, I) = Ord('?') then
              Inc(I);
          end;
          Ord('*'), Ord('+'): Exit;
        end;
        if not Product(Branch, Inner, Multiplied) then
          Exit;
        Branch := Multiplied;
      end;
      Ord('['): begin
        Start := I;
        Inc(I);
        if ByteOr(C, I) = Ord('^') then
          Inc(I);
        if ByteOr(C, I) = Ord(']') then
          Inc(I);
        while True do begin
          if I >= Length(C) then
            Exit;
          if ByteAt(C, I) = Ord(']') then
            Break;
          if ByteAt(C, I) = Ord('\') then
            Inc(I);
          Inc(I);
        end;
        Inc(I);
        AppendAll(Branch, Slice(C, Start, I));
      end;
      Ord('\'): begin
        if I + 2 > Length(C) then
          Exit;
        AppendAll(Branch, Slice(C, I, I + 2));
        Inc(I, 2);
      end;
    else
      AppendAll(Branch, Slice(C, I, I + 1));
      Inc(I);
    end;
    if Length(All) + Length(Branch) > MaxAlternatives then
      Exit;
  end;
  AppendList(All, Branch);
  Parts := All;
  Next := I;
  Result := True;
end;

{ ParseAlternatives reads two or more alternatives once groups of alternatives (and optional groups) are expanded
  into whole alternatives ("^a|b|c$", "^([a|A]uto)|([n|N]one)$", "^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$"), each a
  literal or a class sequence anchored where its own "^" and "$" say. }
function ParseAlternatives(const P: UTF8String; out Alts: TAlternativeArray): Boolean;
var
  Parts: TTextArray;
  Stop, I, K, Count: Int32;
  Width: Int64;
  Sequences, AtStart, AtEnd: Boolean;
  Body, R, Text, Source: UTF8String;
  Seq: TSequence;
begin
  Result := False;
  Alts := nil;
  { A single alternative is only new here when a group was expanded (ParseSequence takes the rest). }
  Parts := nil;
  if not ExpandAlternatives(P, 0, Parts, Stop) or (Stop <> Length(P)) or (Length(Parts) = 0)
    or ((Length(Parts) = 1) and (Parts[0] = P)) then
    Exit;
  SetLength(Alts, Length(Parts));
  Count := 0;
  Sequences := False;
  for I := 0 to Length(Parts) - 1 do begin
    Alts[Count].Kind := AltLiteral;
    Alts[Count].Text := nil;
    Alts[Count].AtStart := False;
    Alts[Count].AtEnd := False;
    ClearSequence(Alts[Count].Seq);
    Alts[Count].Width := 0;
    AtStart := CutPrefix(Parts[I], '^', Body);
    AtEnd := False;
    R := '';
    if CutSuffix(Body, '$', R) and not EndsWithEscape(R) then begin
      Body := R;
      AtEnd := True;
    end;
    if LiteralText(Body, Text) then begin
      Alts[Count].Text := BytesOf(Text);
      Alts[Count].AtStart := AtStart;
      Alts[Count].AtEnd := AtEnd;
      Inc(Count);
      Continue;
    end;
    Source := '^' + Body;
    if AtEnd then
      Source := Source + '$';
    if not ParseSequence(Source, Seq) then begin
      Alts := nil;
      Exit;
    end;
    Sequences := True;
    Alts[Count].Seq := Seq;
    if AtStart then begin
      Alts[Count].Kind := AltStart;
      Inc(Count);
      Continue;
    end;
    Width := 0;
    for K := 0 to Length(Seq.Items) - 1 do begin
      if Seq.Items[K].Min <> Seq.Items[K].Max then begin
        Alts := nil;
        Exit;
      end;
      Width := Width + Int64(Seq.Items[K].Min);
    end;
    { No text is longer than an Int32 counts, so a width beyond one is as good as the largest: it never matches. }
    if Width > High(Int32) then
      Width := High(Int32);
    if AtEnd then
      Alts[Count].Kind := AltEnd
    else
      Alts[Count].Kind := AltAnywhere;
    Alts[Count].Width := Int32(Width);
    Inc(Count);
  end;
  if Sequences and (Count > MaxSequenceAlternatives) then begin
    Alts := nil;
    Exit;
  end;
  Result := True;
end;

{ ExcludedClassWithWord reads "^(?=[^SET]+$)(?=(.*\w)).+$" (with "(?:" or "(" around ".*\w"): the excluded set. }
function ExcludedClassWithWord(const P: UTF8String; out Excluded: TCharSet; out Bangs: Boolean): Boolean;
var
  Body, Rest: UTF8String;
  Negated: TCharSet;
  Next: Int32;
begin
  Result := False;
  Excluded := EmptySet;
  { ParseClass reads a class body from after "[", here "^SET]". }
  Bangs := CutPrefix(P, '^(?=!+[', Body);
  if not Bangs then
    if not CutPrefix(P, '^(?=[', Body) then
      Exit;
  if not ParseClass(Body, 0, Negated, Next) or not HasPrefix(Body, '^') then begin
    Bangs := False;
    Exit;
  end;
  Excluded.Ascii[0] := not Negated.Ascii[0];
  Excluded.Ascii[1] := not Negated.Ascii[1];
  { The run of "!" ends exactly where the class starts only when the class excludes "!". }
  if Bangs and not SetContains(Excluded, Ord('!')) then begin
    Excluded := EmptySet;
    Bangs := False;
    Exit;
  end;
  Rest := Slice(Body, Next, Length(Body));
  if (Rest = '+$)(?=(.*\w)).+$') or (Rest = '+$)(?=(?:.*\w)).+$') or (Rest = '+$)(?=.*\w).+$') then
    Result := True
  else begin
    Excluded := EmptySet;
    Bangs := False;
  end;
end;

procedure ClearPattern(out P: TPattern);
begin
  P := Default(TPattern);
  P.Kind := MatchEverything;
  ClearSequence(P.Seq);
  ClearSequence(P.List.First);
  ClearSequence(P.List.Repeated);
  ClearSequence(P.List.Separator);
  ClearSequence(P.List.Final);
end;

{ LiteralsHash is the slot a string of a set of literals starts its search at. }
function LiteralsSlot(const S: TBytes; Start, Len, Slots: Int32): Int32; inline;
begin
  Result := Int32((StrHash(S, Start, Len) shr 32) and UInt64(Slots - 1));
end;

procedure BuildMany(var M: TPattern);
var
  Size, I, Slot: Int32;
  Same: Boolean;
begin
  Size := 16;
  while Size < 2 * Length(M.Few) do
    Size := Size shl 1;
  SetLength(M.Many, Size);
  for I := 0 to Size - 1 do
    M.Many[I] := 0;
  for I := 0 to Length(M.Few) - 1 do begin
    Slot := LiteralsSlot(M.Few[I], 0, Length(M.Few[I]), Size);
    Same := False;
    while M.Many[Slot] <> 0 do begin
      if (Length(M.Few[M.Many[Slot] - 1]) = Length(M.Few[I]))
        and BytesEqual(M.Few[M.Many[Slot] - 1], 0, M.Few[I], 0, Length(M.Few[I])) then begin
        Same := True;
        Break;
      end;
      Slot := (Slot + 1) and (Size - 1);
    end;
    if not Same then
      M.Many[Slot] := I + 1;
  end;
end;

function ChoosePattern(const P: UTF8String; out M: TPattern): Boolean;
var
  Rest: UTF8String;
  Min, Max: UInt32;
  Texts: TTextArray;
  I: Int32;
  Bangs: Boolean;
  Chars: TCharSet;
  Text: TBytes;
begin
  ClearPattern(M);
  Result := True;
  { Unanchored (or start-anchored) ".*" finds an empty match in any string. "^.*$" does not ("." stops at a line
    terminator), so it is not listed. }
  if (P = '') or (P = '.*') or (P = '^.*') or (P = '.*$') or (P = '(.*)') or (P = '^(.*)') or (P = '[\s\S]*')
    or (P = '^[\s\S]*') or (P = '^[\s\S]*$') then begin
    M.Kind := MatchEverything;
    Exit;
  end;
  if (P = '.+') or (P = '.') or (P = '(.+)') then begin
    M.Kind := MatchHasContent;
    Exit;
  end;
  if (P = '^.+') or (P = '^.') then begin
    M.Kind := MatchHasContent;
    M.Flag := True;
    Exit;
  end;
  { "^X.*" (no "$") matches exactly where "^X" does: ".*" can match nothing. }
  Rest := '';
  if CutSuffix(P, '.*', Rest) and HasPrefix(Rest, '^') and (Length(Rest) > 1) and not EndsWithEscape(Rest)
    and not IsOneOf(ByteAt(Rest, Length(Rest) - 1), '*+?}|(^') then
    if ChoosePattern(Rest, M) then
      Exit;
  ClearPattern(M);
  if LineRange(P, Min, Max) then begin
    M.Kind := MatchLine;
    M.Min := Min;
    M.Max := Max;
    Exit;
  end;
  if WholeAlternatives(P, Texts) then begin
    M.Kind := MatchLiterals;
    SetLength(M.Few, Length(Texts));
    for I := 0 to Length(Texts) - 1 do
      M.Few[I] := BytesOf(Texts[I]);
    if Length(Texts) > 8 then
      BuildMany(M);
    Exit;
  end;
  if ParseAlternatives(P, M.Alts) then begin
    M.Kind := MatchAlternatives;
    Exit;
  end;
  if ParseSeparatedList(P, M.List) then begin
    M.Kind := MatchSeparatedList;
    Exit;
  end;
  if ExcludedClassWithWord(P, Chars, Bangs) then begin
    M.Kind := MatchExcludedClassWithWord;
    M.Chars := Chars;
    M.Flag := Bangs;
    Exit;
  end;
  if ParseSequence(P, M.Seq) then begin
    if SequenceLiteral(M.Seq, Text) then begin
      M.Kind := MatchLiteral;
      M.Text := Text;
      M.Whole := M.Seq.ToEnd;
      ClearSequence(M.Seq);
    end else
      M.Kind := MatchSequence;
    Exit;
  end;
  ClearPattern(M);
  Result := False;
end;

function ValidRegex(const Source: UTF8String): Boolean;
begin
  Result := EcmaRegexValid(Source);
end;

function CompilePattern(const Source: UTF8String; out P: TPattern): Boolean;
var
  Chosen: Boolean;
  Engine: TEcmaRegex;
  Error: UTF8String;
begin
  { Validity is ECMA-262's: a pattern the engine rejects is an error, whichever matcher would run it. A pattern
    with a faster matcher that is valid with the u flag needs no engine at all. The engine reads a pattern that is
    valid only without the u flag by code point too, so the faster matchers decide those the same way. }
  Chosen := ChoosePattern(Source, P);
  if not Chosen or not ValidRegex(Source) then begin
    { The engine compiles the pattern with the u flag, or failing that without it, as many schemas need. }
    if not EcmaRegexCompile(Source, Engine, Error) then begin
      ClearPattern(P);
      Result := False;
      Exit;
    end;
    if not Chosen then begin
      P.Kind := MatchEngine;
      P.Engine := Engine;
    end;
  end;
  P.Source := Source;
  Result := True;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Matching }

{ IndexBytes is the index of the first occurrence of Text in S[At .. Stop-1], or -1. }
function IndexBytes(const S: TBytes; At, Stop: Int32; const Text: TBytes): Int32;
var
  I, N: Int32;
begin
  N := Length(Text);
  I := At;
  while I + N <= Stop do begin
    if BytesEqual(S, I, Text, 0, N) then begin
      Result := I;
      Exit;
    end;
    Inc(I);
  end;
  Result := -1;
end;

{ AlternativeMatch reports whether the alternative matches S[At .. Stop-1], Ascii saying whether that is ASCII. }
function AlternativeMatch(const A: TAlternative; const S: TBytes; At, Stop: Int32; Ascii: Boolean): Boolean;
var
  N, Skip, Positions, Size: Int32;
begin
  case A.Kind of
    AltLiteral: begin
      N := Length(A.Text);
      if A.AtStart and A.AtEnd then
        Result := (Stop - At = N) and BytesEqual(S, At, A.Text, 0, N)
      else if A.AtStart then
        Result := (Stop - At >= N) and BytesEqual(S, At, A.Text, 0, N)
      else if A.AtEnd then
        Result := (Stop - At >= N) and BytesEqual(S, Stop - N, A.Text, 0, N)
      else
        Result := IndexBytes(S, At, Stop, A.Text) >= 0;
    end;
    AltStart:
      if Ascii then
        Result := MatchASCII(A.Seq, S, At, Stop)
      else
        Result := MatchChars(A.Seq, S, At, Stop);
    AltEnd: begin
      if Ascii then begin
        Result := (Stop - At >= A.Width) and MatchASCII(A.Seq, S, Stop - A.Width, Stop);
        Exit;
      end;
      Skip := RuneCount(S, At, Stop) - A.Width;
      if Skip < 0 then begin
        Result := False;
        Exit;
      end;
      while Skip > 0 do begin
        DecodeRuneAt(S, At, Stop, Size);
        Inc(At, Size);
        Dec(Skip);
      end;
      Result := MatchChars(A.Seq, S, At, Stop);
    end;
  else
    Result := False;
    if Ascii then begin
      while Int64(At) + A.Width <= Stop do begin
        if MatchASCII(A.Seq, S, At, Stop) then begin
          Result := True;
          Exit;
        end;
        Inc(At);
      end;
      Exit;
    end;
    Positions := Int32(Int64(RuneCount(S, At, Stop)) - A.Width + 1);
    while Positions > 0 do begin
      if MatchChars(A.Seq, S, At, Stop) then begin
        Result := True;
        Exit;
      end;
      { The last position can be the end of the text, where there is no character to step over. }
      if At < Stop then begin
        DecodeRuneAt(S, At, Stop, Size);
        Inc(At, Size);
      end;
      Dec(Positions);
    end;
  end;
end;

function LiteralsMatch(const P: TPattern; const S: TBytes; Start, Len: Int32): Boolean;
var
  I, Slot, Size, At: Int32;
begin
  Result := True;
  Size := Length(P.Many);
  if Size <> 0 then begin
    Slot := LiteralsSlot(S, Start, Len, Size);
    while True do begin
      At := P.Many[Slot];
      if At = 0 then
        Break;
      if (Length(P.Few[At - 1]) = Len) and BytesEqual(S, Start, P.Few[At - 1], 0, Len) then
        Exit;
      Slot := (Slot + 1) and (Size - 1);
    end;
    Result := False;
    Exit;
  end;
  for I := 0 to Length(P.Few) - 1 do
    if (Length(P.Few[I]) = Len) and BytesEqual(S, Start, P.Few[I], 0, Len) then
      Exit;
  Result := False;
end;

function PatternMatch(const P: TPattern; const S: TBytes; Start, Len: Int32; Ascii: Boolean): Boolean;
var
  Stop, I, C, Size, N: Int32;
  Word: Boolean;
begin
  Stop := Start + Len;
  case P.Kind of
    MatchEverything: Result := True;
    MatchLiteral:
      if P.Whole then
        Result := (Len = Length(P.Text)) and BytesEqual(S, Start, P.Text, 0, Len)
      else
        Result := (Len >= Length(P.Text)) and BytesEqual(S, Start, P.Text, 0, Length(P.Text));
    MatchSequence:
      if Ascii then
        Result := MatchASCII(P.Seq, S, Start, Stop)
      else
        Result := MatchChars(P.Seq, S, Start, Stop);
    MatchSeparatedList: Result := ListMatch(P.List, S, Start, Stop);
    MatchHasContent: begin
      Result := False;
      if P.Flag then begin
        if Len = 0 then
          Exit;
        C := DecodeRune(S, Start, Stop, Size);
        Result := not IsLineTerminator(C);
        Exit;
      end;
      I := Start;
      while I < Stop do begin
        C := DecodeRuneAt(S, I, Stop, Size);
        if not IsLineTerminator(C) then begin
          Result := True;
          Exit;
        end;
        Inc(I, Size);
      end;
    end;
    MatchLine: begin
      if Ascii and (Int64(Len) < Int64(P.Min)) then begin
        Result := False;
        Exit;
      end;
      N := LineLength(S, Start, Stop);
      Result := (N >= 0) and (Int64(N) >= Int64(P.Min)) and (Int64(N) <= Int64(P.Max));
    end;
    MatchLiterals: Result := LiteralsMatch(P, S, Start, Len);
    MatchAlternatives: begin
      Result := True;
      for I := 0 to Length(P.Alts) - 1 do
        if AlternativeMatch(P.Alts[I], S, Start, Stop, Ascii) then
          Exit;
      Result := False;
    end;
    MatchExcludedClassWithWord: begin
      Result := False;
      I := Start;
      if P.Flag then begin
        while (I < Stop) and (S[I] = Ord('!')) do
          Inc(I);
        if (I = Start) or (I = Stop) then
          Exit;
      end;
      Word := False;
      while I < Stop do begin
        C := DecodeRuneAt(S, I, Stop, Size);
        if SetContains(P.Chars, C) or IsLineTerminator(C) then
          Exit;
        Word := Word or SetContains(WordSet, C);
        Inc(I, Size);
      end;
      Result := Word;
    end;
  else
    Result := EcmaRegexIsMatch(P.Engine, S, Start, Len);
  end;
end;

function PatternMatchString(const P: TPattern; const S: UTF8String): Boolean;
var
  B: TBytes;
begin
  B := BytesOf(S);
  Result := PatternMatch(P, B, 0, Length(B), IsASCII(B, 0, Length(B)));
end;

initialization
  DigitSet := CharRange(Ord('0'), Ord('9'));
  WordSet := SetUnion(SetUnion(SetUnion(CharRange(Ord('0'), Ord('9')), CharRange(Ord('A'), Ord('Z'))),
    CharRange(Ord('a'), Ord('z'))), CharRange(Ord('_'), Ord('_')));
end.
