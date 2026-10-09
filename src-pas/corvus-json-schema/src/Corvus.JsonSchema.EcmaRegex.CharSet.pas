unit Corvus.JsonSchema.EcmaRegex.CharSet;
{$I corvus.inc}

{ The sets of code points of the ECMA-262 regular expression engine, and case-insensitive matching. This unit is the
  port of charset.go and fold.go of the ecmaregex package of the Go module. Corvus.JsonSchema.EcmaRegex describes
  the engine as a whole.

  Three things differ from the Go source.

  A set is a record that is copied, where Go shares a pointer to it. The copy shares the list of ranges, so it costs
  a few words.

  Go keeps the sets already built for property expressions in a sync.Map, so that a property many patterns name is
  built once. A map shared between threads needs a lock, which this package does without, so here each pattern
  builds the property sets it names. That costs a pattern some microseconds when it is compiled and nothing when it
  matches.

  Go builds each of the two tables of case-insensitive matching on first use behind a sync.Once. Here a thread that
  finds no table builds one and publishes it with one atomic exchange of a pointer. Two threads that race both build
  the table, one of them wins, and the other frees its copy and uses the winner's. }

interface

uses
  Corvus.JsonSchema.Ucd;

const
  { MaxRune is the last Unicode code point. }
  MaxRune = UcdMaxRune;

type
  TRunes = TUcdRunes;

  { A TCharSet is an immutable set of code points. The ranges are sorted, inclusive, and neither overlap nor touch.
    The two words of Ascii repeat the membership of the code points below 128 so that the common case is one bit
    test. }
  TCharSet = record
    Ranges: TRunes;
    Ascii: array[0..1] of UInt64;
    { Contains reports whether the set holds the code point. }
    function Contains(R: Int32): Boolean;
    { Empty reports whether the set holds no code point. }
    function Empty: Boolean;
  end;
  TCharSets = array of TCharSet;

  { A TFoldTable lists the characters that are equivalent to some other character, each with all the members of its
    equivalence class. }
  TFoldTable = record
    Points: TRunes;
    Classes: array of TRunes;
  end;
  PFoldTable = ^TFoldTable;

const
  { The classes ECMA-262 defines. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and . is everything
    but the four line terminators. }
  DigitPairs: array[0..1] of Int32 = (Ord('0'), Ord('9'));
  WordPairs: array[0..7] of Int32 = (Ord('0'), Ord('9'), Ord('A'), Ord('Z'), Ord('_'), Ord('_'), Ord('a'), Ord('z'));
  SpacePairs: array[0..19] of Int32 = (
    $9, $D, $20, $20, $A0, $A0, $1680, $1680, $2000, $200A, $2028, $2029, $202F, $202F, $205F, $205F,
    $3000, $3000, $FEFF, $FEFF);
  LineTerminatorPairs: array[0..5] of Int32 = (10, 10, 13, 13, $2028, $2029);

  { With the u flag and case-insensitive matching, \w and \b also count the two characters that fold to an ASCII
    letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters. }
  FoldWordPairs: array[0..11] of Int32 = (
    Ord('0'), Ord('9'), Ord('A'), Ord('Z'), Ord('_'), Ord('_'), Ord('a'), Ord('z'), $17F, $17F, $212A, $212A);

var
  { These are set when the unit is initialised and never written again. }
  DotSet: TCharSet;
  FoldWordSet: TCharSet;
  NotFoldWordSet: TCharSet;
  { MultiUpper holds the characters of the Basic Multilingual Plane whose uppercase form is more than one character,
    as inclusive pairs. Canonicalize leaves them alone. }
  MultiUpper: TCharSet;

{ NewCharSet builds a set from inclusive pairs in any order, which may overlap. With Negate it builds the
  complement. }
function NewCharSet(const Pairs: array of Int32; Negate: Boolean): TCharSet;

{ IsWordByte reports whether a byte is an ECMA-262 word character. Every word character is ASCII, so a word boundary
  can be decided from the bytes on either side of a position. }
function IsWordByte(B: UInt8): Boolean; inline;

(* PropertySet resolves the expression between the braces of \p{...} to its set. It reports false for a name or value
  that ECMA-262 does not define. *)
function PropertySet(const Expr: UTF8String; out CharSet: TCharSet): Boolean;

{ PropertyPairs returns the ranges of a property expression. Every range comes from the tables of the Ucd unit,
  which hold one fixed version of Unicode, so the answer does not depend on the compiler. }
function PropertyPairs(const Expr: UTF8String; out Pairs: TRunes): Boolean;

{ CategoryNameCount and BinaryNameCount are the sizes of the two lists of names a property expression can use, and
  CategoryNameAt and BinaryNameAt return a name of each. The tests walk them. }
function CategoryNameCount: Int32;
function CategoryNameAt(Index: Int32): UTF8String;
function BinaryNameCount: Int32;
function BinaryNameAt(Index: Int32): UTF8String;

{ Canonicalize is the Canonicalize function of ECMA-262 for case-insensitive matching. }
function Canonicalize(R: Int32; UnicodeMode: Boolean): Int32;

{ FoldClasses returns the table of the grammar in use. It is built on first use, which only a pattern with a
  case-insensitive group reaches. }
function FoldClasses(UnicodeMode: Boolean): PFoldTable;

{ FoldClosure returns the set of every character equivalent to some member of the set. }
function FoldClosure(const CharSet: TCharSet; UnicodeMode: Boolean): TCharSet;

function IsFoldWordRune(R: Int32): Boolean;

implementation

type
  TInt64Array = array of Int64;

const
  { CategoryNames maps the long names and the aliases of the General_Category values to their short names. Each name
    is followed by the short name it stands for. }
  CategoryNames: array[0..159] of UTF8String = (
    'Letter', 'L', 'Lowercase_Letter', 'Ll', 'Uppercase_Letter', 'Lu', 'Titlecase_Letter', 'Lt', 'Cased_Letter', 'LC',
    'Modifier_Letter', 'Lm', 'Other_Letter', 'Lo', 'Mark', 'M', 'Combining_Mark', 'M', 'Nonspacing_Mark', 'Mn',
    'Spacing_Mark', 'Mc', 'Enclosing_Mark', 'Me', 'Number', 'N', 'Decimal_Number', 'Nd', 'digit', 'Nd',
    'Letter_Number', 'Nl', 'Other_Number', 'No', 'Punctuation', 'P', 'punct', 'P', 'Connector_Punctuation', 'Pc',
    'Dash_Punctuation', 'Pd', 'Open_Punctuation', 'Ps', 'Close_Punctuation', 'Pe', 'Initial_Punctuation', 'Pi',
    'Final_Punctuation', 'Pf', 'Other_Punctuation', 'Po', 'Symbol', 'S', 'Math_Symbol', 'Sm', 'Currency_Symbol', 'Sc',
    'Modifier_Symbol', 'Sk', 'Other_Symbol', 'So', 'Separator', 'Z', 'Space_Separator', 'Zs', 'Line_Separator', 'Zl',
    'Paragraph_Separator', 'Zp', 'Other', 'C', 'Control', 'Cc', 'cntrl', 'Cc', 'Format', 'Cf', 'Surrogate', 'Cs',
    'Private_Use', 'Co', 'Unassigned', 'Cn', 'L', 'L', 'Ll', 'Ll', 'Lu', 'Lu', 'Lt', 'Lt', 'LC', 'LC', 'Lm', 'Lm',
    'Lo', 'Lo', 'M', 'M', 'Mn', 'Mn', 'Mc', 'Mc', 'Me', 'Me', 'N', 'N', 'Nd', 'Nd', 'Nl', 'Nl', 'No', 'No', 'P', 'P',
    'Pc', 'Pc', 'Pd', 'Pd', 'Ps', 'Ps', 'Pe', 'Pe', 'Pi', 'Pi', 'Pf', 'Pf', 'Po', 'Po', 'S', 'S', 'Sm', 'Sm',
    'Sc', 'Sc', 'Sk', 'Sk', 'So', 'So', 'Z', 'Z', 'Zs', 'Zs', 'Zl', 'Zl', 'Zp', 'Zp', 'C', 'C', 'Cc', 'Cc',
    'Cf', 'Cf', 'Cs', 'Cs', 'Co', 'Co', 'Cn', 'Cn'
  );

  { BinaryNames maps the names and the aliases of the binary properties ECMA-262 lists to their canonical names.
    Each name is followed by the canonical name it stands for. }
  BinaryNames: array[0..195] of UTF8String = (
    'ASCII', 'ASCII', 'ASCII_Hex_Digit', 'ASCII_Hex_Digit', 'AHex', 'ASCII_Hex_Digit', 'Alphabetic', 'Alphabetic',
    'Alpha', 'Alphabetic', 'Any', 'Any', 'Assigned', 'Assigned', 'Bidi_Control', 'Bidi_Control',
    'Bidi_C', 'Bidi_Control', 'Bidi_Mirrored', 'Bidi_Mirrored', 'Bidi_M', 'Bidi_Mirrored',
    'Case_Ignorable', 'Case_Ignorable', 'CI', 'Case_Ignorable', 'Cased', 'Cased',
    'Changes_When_Casefolded', 'Changes_When_Casefolded', 'CWCF', 'Changes_When_Casefolded',
    'Changes_When_Casemapped', 'Changes_When_Casemapped', 'CWCM', 'Changes_When_Casemapped',
    'Changes_When_Lowercased', 'Changes_When_Lowercased', 'CWL', 'Changes_When_Lowercased',
    'Changes_When_NFKC_Casefolded', 'Changes_When_NFKC_Casefolded', 'CWKCF', 'Changes_When_NFKC_Casefolded',
    'Changes_When_Titlecased', 'Changes_When_Titlecased', 'CWT', 'Changes_When_Titlecased',
    'Changes_When_Uppercased', 'Changes_When_Uppercased', 'CWU', 'Changes_When_Uppercased', 'Dash', 'Dash',
    'Default_Ignorable_Code_Point', 'Default_Ignorable_Code_Point', 'DI', 'Default_Ignorable_Code_Point',
    'Deprecated', 'Deprecated', 'Dep', 'Deprecated', 'Diacritic', 'Diacritic', 'Dia', 'Diacritic', 'Emoji', 'Emoji',
    'Emoji_Component', 'Emoji_Component', 'EComp', 'Emoji_Component', 'Emoji_Modifier', 'Emoji_Modifier',
    'EMod', 'Emoji_Modifier', 'Emoji_Modifier_Base', 'Emoji_Modifier_Base', 'EBase', 'Emoji_Modifier_Base',
    'Emoji_Presentation', 'Emoji_Presentation', 'EPres', 'Emoji_Presentation',
    'Extended_Pictographic', 'Extended_Pictographic', 'ExtPict', 'Extended_Pictographic', 'Extender', 'Extender',
    'Ext', 'Extender', 'Grapheme_Base', 'Grapheme_Base', 'Gr_Base', 'Grapheme_Base',
    'Grapheme_Extend', 'Grapheme_Extend', 'Gr_Ext', 'Grapheme_Extend', 'Hex_Digit', 'Hex_Digit', 'Hex', 'Hex_Digit',
    'IDS_Binary_Operator', 'IDS_Binary_Operator', 'IDSB', 'IDS_Binary_Operator',
    'IDS_Trinary_Operator', 'IDS_Trinary_Operator', 'IDST', 'IDS_Trinary_Operator', 'ID_Continue', 'ID_Continue',
    'IDC', 'ID_Continue', 'ID_Start', 'ID_Start', 'IDS', 'ID_Start', 'Ideographic', 'Ideographic',
    'Ideo', 'Ideographic', 'Join_Control', 'Join_Control', 'Join_C', 'Join_Control',
    'Logical_Order_Exception', 'Logical_Order_Exception', 'LOE', 'Logical_Order_Exception', 'Lowercase', 'Lowercase',
    'Lower', 'Lowercase', 'Math', 'Math', 'Noncharacter_Code_Point', 'Noncharacter_Code_Point',
    'NChar', 'Noncharacter_Code_Point', 'Pattern_Syntax', 'Pattern_Syntax', 'Pat_Syn', 'Pattern_Syntax',
    'Pattern_White_Space', 'Pattern_White_Space', 'Pat_WS', 'Pattern_White_Space', 'Quotation_Mark', 'Quotation_Mark',
    'QMark', 'Quotation_Mark', 'Radical', 'Radical', 'Regional_Indicator', 'Regional_Indicator',
    'RI', 'Regional_Indicator', 'Sentence_Terminal', 'Sentence_Terminal', 'STerm', 'Sentence_Terminal',
    'Soft_Dotted', 'Soft_Dotted', 'SD', 'Soft_Dotted', 'Terminal_Punctuation', 'Terminal_Punctuation',
    'Term', 'Terminal_Punctuation', 'Unified_Ideograph', 'Unified_Ideograph', 'UIdeo', 'Unified_Ideograph',
    'Uppercase', 'Uppercase', 'Upper', 'Uppercase', 'Variation_Selector', 'Variation_Selector',
    'VS', 'Variation_Selector', 'White_Space', 'White_Space', 'space', 'White_Space', 'XID_Continue', 'XID_Continue',
    'XIDC', 'XID_Continue', 'XID_Start', 'XID_Start', 'XIDS', 'XID_Start'
  );

{ SortInt64 sorts the elements Lo to Hi of an array into ascending order. }
procedure SortInt64(var A: TInt64Array; Lo, Hi: Int32);
var
  I, J: Int32;
  Pivot, Swap: Int64;
begin
  while Lo < Hi do begin
    I := Lo;
    J := Hi;
    Pivot := A[Lo + (Hi - Lo) div 2];
    repeat
      while A[I] < Pivot do
        Inc(I);
      while A[J] > Pivot do
        Dec(J);
      if I <= J then begin
        Swap := A[I];
        A[I] := A[J];
        A[J] := Swap;
        Inc(I);
        Dec(J);
      end;
    until I > J;
    // The smaller part is sorted by a call and the larger by the loop, so the depth of the calls stays small.
    if J - Lo < Hi - I then begin
      SortInt64(A, Lo, J);
      Lo := I;
    end else begin
      SortInt64(A, I, Hi);
      Hi := J;
    end;
  end;
end;

function NewCharSet(const Pairs: array of Int32; Negate: Boolean): TCharSet;
var
  N, I, M, K, Lo, Hi, Next, C: Int32;
  Idx: TInt64Array;
  Merged, Inverse: TRunes;
begin
  N := Length(Pairs) div 2;
  // The pairs are sorted by their first code point. A pair is one number here, its first code point above its last,
  // so that sorting the numbers sorts the pairs.
  SetLength(Idx, N);
  for I := 0 to N - 1 do
    Idx[I] := (Int64(Pairs[2 * I]) shl 32) or Int64(Pairs[2 * I + 1]);
  SortInt64(Idx, 0, N - 1);
  SetLength(Merged, 2 * N);
  M := 0;
  for I := 0 to N - 1 do begin
    Lo := Int32(Idx[I] shr 32);
    Hi := Int32(Idx[I] and $FFFFFFFF);
    if (M > 0) and (Lo <= Merged[M - 1] + 1) then begin
      if Hi > Merged[M - 1] then
        Merged[M - 1] := Hi;
      Continue;
    end;
    Merged[M] := Lo;
    Merged[M + 1] := Hi;
    Inc(M, 2);
  end;
  SetLength(Merged, M);
  if Negate then begin
    SetLength(Inverse, M + 2);
    K := 0;
    Next := 0;
    I := 0;
    while I < M do begin
      if Merged[I] > Next then begin
        Inverse[K] := Next;
        Inverse[K + 1] := Merged[I] - 1;
        Inc(K, 2);
      end;
      Next := Merged[I + 1] + 1;
      Inc(I, 2);
    end;
    if Next <= MaxRune then begin
      Inverse[K] := Next;
      Inverse[K + 1] := MaxRune;
      Inc(K, 2);
    end;
    SetLength(Inverse, K);
    Merged := Inverse;
    M := K;
  end;
  Result.Ranges := Merged;
  Result.Ascii[0] := 0;
  Result.Ascii[1] := 0;
  I := 0;
  while (I < M) and (Merged[I] < 128) do begin
    C := Merged[I];
    while (C <= Merged[I + 1]) and (C < 128) do begin
      Result.Ascii[C shr 6] := Result.Ascii[C shr 6] or (UInt64(1) shl (C and 63));
      Inc(C);
    end;
    Inc(I, 2);
  end;
end;

function TCharSet.Contains(R: Int32): Boolean;
var
  Lo, Hi, Mid: Int32;
begin
  // A value that is not a code point is in no set.
  if UInt32(R) < 128 then
    Exit(((Ascii[R shr 6] shr (R and 63)) and 1) <> 0);
  Lo := 0;
  Hi := Length(Ranges) div 2;
  while Lo < Hi do begin
    Mid := (Lo + Hi) shr 1;
    if R > Ranges[2 * Mid + 1] then
      Lo := Mid + 1
    else if R < Ranges[2 * Mid] then
      Hi := Mid
    else
      Exit(True);
  end;
  Result := False;
end;

function TCharSet.Empty: Boolean;
begin
  Result := Length(Ranges) = 0;
end;

function IsWordByte(B: UInt8): Boolean;
begin
  Result := ((B >= Ord('a')) and (B <= Ord('z'))) or ((B >= Ord('A')) and (B <= Ord('Z'))) or
    ((B >= Ord('0')) and (B <= Ord('9'))) or (B = Ord('_'));
end;

{ LookUp finds a name in a list of names each followed by what it stands for. }
function LookUp(const Names: array of UTF8String; const Name: UTF8String; out Value: UTF8String): Boolean;
var
  I: Int32;
begin
  I := 0;
  while I < Length(Names) do begin
    if Names[I] = Name then begin
      Value := Names[I + 1];
      Exit(True);
    end;
    Inc(I, 2);
  end;
  Value := '';
  Result := False;
end;

function CategoryNameCount: Int32;
begin
  Result := Length(CategoryNames) div 2;
end;

function CategoryNameAt(Index: Int32): UTF8String;
begin
  Result := CategoryNames[2 * Index];
end;

function BinaryNameCount: Int32;
begin
  Result := Length(BinaryNames) div 2;
end;

function BinaryNameAt(Index: Int32): UTF8String;
begin
  Result := BinaryNames[2 * Index];
end;

{ TablePairs returns the ranges of a table as inclusive pairs. It reports false for no table. }
function TablePairs(T: PUcdTable; out Pairs: TRunes): Boolean;
begin
  Pairs := nil;
  if T = nil then
    Exit(False);
  T^.AppendRanges(Pairs);
  Result := True;
end;

function CategoryPairs(const Value: UTF8String; out Pairs: TRunes): Boolean;
var
  Short: UTF8String;
begin
  Pairs := nil;
  if not LookUp(CategoryNames, Value, Short) then
    Exit(False);
  Result := TablePairs(UcdCategory(Short), Pairs);
end;

function PropertySet(const Expr: UTF8String; out CharSet: TCharSet): Boolean;
var
  Pairs: TRunes;
begin
  if not PropertyPairs(Expr, Pairs) then begin
    CharSet.Ranges := nil;
    CharSet.Ascii[0] := 0;
    CharSet.Ascii[1] := 0;
    Exit(False);
  end;
  CharSet := NewCharSet(Pairs, False);
  Result := True;
end;

function PropertyPairs(const Expr: UTF8String; out Pairs: TRunes): Boolean;
var
  Name, Value, Canonical: UTF8String;
  HasValue: Boolean;
  I: Int32;
begin
  Pairs := nil;
  Name := Expr;
  Value := '';
  HasValue := False;
  for I := 1 to Length(Expr) do
    if Expr[I] = '=' then begin
      Name := Copy(Expr, 1, I - 1);
      Value := Copy(Expr, I + 1, Length(Expr) - I);
      HasValue := True;
      Break;
    end;
  if HasValue then begin
    if (Name = 'General_Category') or (Name = 'gc') then
      Exit(CategoryPairs(Value, Pairs));
    if (Name = 'Script') or (Name = 'sc') then
      Exit(TablePairs(UcdScript(Value), Pairs));
    if (Name = 'Script_Extensions') or (Name = 'scx') then
      Exit(TablePairs(UcdScriptExtensions(Value), Pairs));
    Exit(False);
  end;
  if CategoryPairs(Name, Pairs) then
    Exit(True);
  if not LookUp(BinaryNames, Name, Canonical) then
    Exit(False);
  if Canonical = 'ASCII' then begin
    SetLength(Pairs, 2);
    Pairs[0] := 0;
    Pairs[1] := $7F;
    Exit(True);
  end;
  if Canonical = 'Any' then begin
    SetLength(Pairs, 2);
    Pairs[0] := 0;
    Pairs[1] := MaxRune;
    Exit(True);
  end;
  Result := TablePairs(UcdBinary(Canonical), Pairs);
end;

{ Case-insensitive matching exists only inside a modifier group such as (?i:...). ECMA-262 defines it through a
  Canonicalize function. Two characters match when they canonicalize to the same character. The function differs
  between the two grammars. With the u flag it is Unicode simple case folding. With no flag it is the single
  character uppercase mapping of a UTF-16 code unit, which never maps a character outside ASCII into ASCII.

  The parser applies the equivalence to every literal and class as it reads them, so a case-insensitive group is
  matched with plain classes. Only a backreference needs the function when matching.

  The folding and the uppercase mapping come from the tables of the Ucd unit, never from the run-time library of the
  compiler, so a pattern matches the same texts whichever compiler built the program. }

const
  MultiUpperPairs: array[0..53] of Int32 = (
    $DF, $DF, $149, $149, $1F0, $1F0, $390, $390, $3B0, $3B0, $587, $587, $1E96, $1E9A, $1F50, $1F50,
    $1F52, $1F52, $1F54, $1F54, $1F56, $1F56, $1F80, $1FAF, $1FB2, $1FB4, $1FB6, $1FB7, $1FBC, $1FBC,
    $1FC2, $1FC4, $1FC6, $1FC7, $1FCC, $1FCC, $1FD2, $1FD3, $1FD6, $1FD7, $1FE2, $1FE4, $1FE6, $1FE7,
    $1FF2, $1FF4, $1FF6, $1FF7, $1FFC, $1FFC, $FB00, $FB06, $FB13, $FB17);

var
  { The two tables, each nil until a thread has built it. They hold a PFoldTable. }
  UnicodeFoldTable: Pointer = nil;
  LegacyFoldTable: Pointer = nil;

function Canonicalize(R: Int32; UnicodeMode: Boolean): Int32;
var
  Upper: Int32;
begin
  if UnicodeMode then
    // Every character of a simple case folding class folds to the same character.
    Exit(UcdFold(R));
  // A character beyond the Basic Multilingual Plane is two code units, and neither has an uppercase form.
  if (R > $FFFF) or MultiUpper.Contains(R) then
    Exit(R);
  Upper := UcdToUpper(R);
  if ((R >= 128) and (Upper < 128)) or (Upper > $FFFF) then
    Exit(R);
  Result := Upper;
end;

{ NewFoldTable builds a table from the classes. A class is given as its members, each a number with the key of the
  class (the character the members canonicalize to) above the member, in the first N elements of Pairs. A pair may
  be there more than once. }
function NewFoldTable(var Pairs: TInt64Array; N: Int32): PFoldTable;
var
  Classes: array of TRunes;
  ByMember: TInt64Array;
  I, J, K, NClasses, NMembers, Size: Int32;
begin
  SortInt64(Pairs, 0, N - 1);
  // Drop the pairs that repeat the one before.
  K := 0;
  for I := 0 to N - 1 do
    if (K = 0) or (Pairs[I] <> Pairs[K - 1]) then begin
      Pairs[K] := Pairs[I];
      Inc(K);
    end;
  N := K;
  // The pairs of one class are now together. Only a class of two or more members goes in the table.
  SetLength(Classes, N);
  SetLength(ByMember, N);
  NClasses := 0;
  NMembers := 0;
  I := 0;
  while I < N do begin
    J := I;
    while (J < N) and ((Pairs[J] shr 32) = (Pairs[I] shr 32)) do
      Inc(J);
    Size := J - I;
    if Size >= 2 then begin
      SetLength(Classes[NClasses], Size);
      for K := 0 to Size - 1 do begin
        Classes[NClasses][K] := Int32(Pairs[I + K] and $FFFFFFFF);
        // The member above the number of its class, so that sorting these sorts the members.
        ByMember[NMembers] := ((Pairs[I + K] and $FFFFFFFF) shl 32) or Int64(NClasses);
        Inc(NMembers);
      end;
      Inc(NClasses);
    end;
    I := J;
  end;
  SortInt64(ByMember, 0, NMembers - 1);
  New(Result);
  SetLength(Result^.Points, NMembers);
  SetLength(Result^.Classes, NMembers);
  for I := 0 to NMembers - 1 do begin
    Result^.Points[I] := Int32(ByMember[I] shr 32);
    Result^.Classes[I] := Classes[Int32(ByMember[I] and $FFFFFFFF)];
  end;
end;

{ Publish makes a table the one every thread uses, unless another thread has published one first, and returns the
  table in use. }
function Publish(var Slot: Pointer; Table: PFoldTable): PFoldTable;
var
  Before: Pointer;
begin
  Before := InterlockedCompareExchangePointer(Slot, Table, nil);
  if Before = nil then
    Exit(Table);
  Dispose(Table);
  Result := PFoldTable(Before);
end;

function FoldClasses(UnicodeMode: Boolean): PFoldTable;
var
  Pairs: TInt64Array;
  Folding: TUcdRunes;
  I, N, R: Int32;
  Key: Int64;
begin
  if UnicodeMode then begin
    Result := PFoldTable(UnicodeFoldTable);
    if Result <> nil then
      Exit;
    // A class is a character that others fold to, with those others.
    Folding := nil;
    UcdAppendFolding(Folding);
    SetLength(Pairs, 2 * Length(Folding));
    N := 0;
    for I := 0 to High(Folding) do begin
      Key := UcdFold(Folding[I]);
      Pairs[N] := (Key shl 32) or Key;
      Pairs[N + 1] := (Key shl 32) or Int64(Folding[I]);
      Inc(N, 2);
    end;
    Exit(Publish(UnicodeFoldTable, NewFoldTable(Pairs, N)));
  end;
  Result := PFoldTable(LegacyFoldTable);
  if Result <> nil then
    Exit;
  SetLength(Pairs, $10000);
  for R := 0 to $FFFF do begin
    Key := Canonicalize(R, False);
    Pairs[R] := (Key shl 32) or Int64(R);
  end;
  Result := Publish(LegacyFoldTable, NewFoldTable(Pairs, $10000));
end;

function FoldClosure(const CharSet: TCharSet; UnicodeMode: Boolean): TCharSet;
var
  Table: PFoldTable;
  Extra: TRunes;
  I, K, M, N, Lo, Hi, First, Limit, Mid, Member: Int32;
begin
  Table := FoldClasses(UnicodeMode);
  Extra := nil;
  N := 0;
  I := 0;
  while I < Length(CharSet.Ranges) do begin
    Lo := CharSet.Ranges[I];
    Hi := CharSet.Ranges[I + 1];
    // The first point at or after the start of the range.
    First := 0;
    Limit := Length(Table^.Points);
    while First < Limit do begin
      Mid := (First + Limit) shr 1;
      if Table^.Points[Mid] >= Lo then
        Limit := Mid
      else
        First := Mid + 1;
    end;
    K := First;
    while (K < Length(Table^.Points)) and (Table^.Points[K] <= Hi) do begin
      for M := 0 to High(Table^.Classes[K]) do begin
        Member := Table^.Classes[K][M];
        if not CharSet.Contains(Member) then begin
          if N + 2 > Length(Extra) then
            SetLength(Extra, 2 * N + 16);
          Extra[N] := Member;
          Extra[N + 1] := Member;
          Inc(N, 2);
        end;
      end;
      Inc(K);
    end;
    Inc(I, 2);
  end;
  if N = 0 then
    Exit(CharSet);
  SetLength(Extra, N + Length(CharSet.Ranges));
  for I := 0 to High(CharSet.Ranges) do
    Extra[N + I] := CharSet.Ranges[I];
  Result := NewCharSet(Extra, False);
end;

function IsFoldWordRune(R: Int32): Boolean;
begin
  Result := ((R >= 0) and (R < 128) and IsWordByte(UInt8(R))) or (R = $17F) or (R = $212A);
end;

initialization
  DotSet := NewCharSet(LineTerminatorPairs, True);
  FoldWordSet := NewCharSet(FoldWordPairs, False);
  NotFoldWordSet := NewCharSet(FoldWordPairs, True);
  MultiUpper := NewCharSet(MultiUpperPairs, False);

finalization
  if UnicodeFoldTable <> nil then
    Dispose(PFoldTable(UnicodeFoldTable));
  if LegacyFoldTable <> nil then
    Dispose(PFoldTable(LegacyFoldTable));
end.