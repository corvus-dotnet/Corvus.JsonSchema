unit Corvus.JsonSchema.EcmaRegex.Parse;
{$I corvus.inc}

{ The parser of the ECMA-262 regular expression engine. This unit is the port of parse.go of the ecmaregex package
  of the Go module. Corvus.JsonSchema.EcmaRegex describes the engine as a whole.

  Three things differ from the Go source.

  The syntax tree is an array of nodes that name one another by index, where Go links them with pointers. Node 0 is
  an empty node that stands for "no node". The sets of the classes are in an array of the parser too, and a node
  names its set by index, with -1 for none.

  The Go parser stops at an error by panicking, and parse recovers. Exceptions are kept out of the engine here, so
  Fail records the error and moves the position to the end of the pattern, and each function returns as soon as it
  sees that the parser has failed. A function that fails returns node 0.

  A group name is kept as UTF-8, where Go keeps a string of the same content. }

interface

uses
  Corvus.JsonSchema.Ucd,
  Corvus.JsonSchema.EcmaRegex.CharSet;

type
  TNodeKind = (
    NEmpty,
    NChar,
    NSet,
    NCat,
    NAlt,
    NGroup,
    NRepeat,
    NBOL,
    NEOL,
    // NLineStart and NLineEnd are ^ and $ inside a group with the m modifier.
    NLineStart,
    NLineEnd,
    NWordBoundary,
    NNotWordBoundary,
    NLook,
    NBackref);

const
  { Unbounded is the max of a quantifier with no upper bound. }
  Unbounded = -1;

  { MaxCount caps the bounds of a counted quantifier. No text that fits in memory reaches a larger count. }
  MaxCount = High(Int32);

  { MaxNesting caps the depth of groups, which bounds the recursion of the parser and of the compilers. }
  MaxNesting = 1000;

type
  TInt32Array = array of Int32;

  { A TNode is one element of the syntax tree of a pattern. }
  TNode = record
    Kind: TNodeKind;
    { R is the code point of an NChar. }
    R: Int32;
    { SetIx is the class of an NSet, as an index into the sets of the parser. }
    SetIx: Int32;
    { Subs holds the children of NCat and NAlt, and the one child of NGroup, NRepeat and NLook. }
    Subs: TInt32Array;
    { Min, Max and Lazy describe an NRepeat. }
    Min, Max: Int32;
    Lazy: Boolean;
    { Group is the number of the capturing group of an NGroup, counted from 1. }
    Group: Int32;
    { Groups holds the groups an NBackref names. A name can belong to several groups in separate alternatives, of
      which at most one has taken part at any time. }
    Groups: TInt32Array;
    { Name is the group name of a named backreference until it is resolved. }
    Name: UTF8String;
    { Fold marks an NBackref, NWordBoundary or NNotWordBoundary inside a case-insensitive group. }
    Fold: Boolean;
    { Behind and Negative describe an NLook. }
    Behind, Negative: Boolean;
  end;
  TNodes = array of TNode;

  { A TBranch names one alternative of one disjunction. }
  TBranch = record
    Disjunction, Alternative: Int32;
  end;
  TBranches = array of TBranch;

  { A TNamedGroup is a capturing group with a name, and the alternatives that enclose it. }
  TNamedGroup = record
    Group: Int32;
    Path: TBranches;
  end;

  { The groups that share one name. }
  TGroupName = record
    Name: UTF8String;
    Groups: array of TNamedGroup;
  end;

  { A TParser reads a pattern by recursive descent. With Unicode set it applies the grammar of the u flag. Without
    it, it applies the Annex B grammar of patterns with no flag. }
  TParser = record
    P: TRunes;
    I: Int32;
    Unicode: Boolean;
    Depth: Int32;
    { GroupCount and HasNames come from a scan of the whole pattern before parsing, because the meaning of \1 and \k
      depends on groups that may follow them. }
    GroupCount: Int32;
    HasNames: Boolean;
    Groups: Int32;
    Names: array of TGroupName;
    NamedRefs: TInt32Array;
    { Path lists the alternatives that enclose the current position, innermost last. It has PathLen elements. }
    Path: TBranches;
    PathLen: Int32;
    Disjunctions: Int32;
    { ICase, Multiline and DotAll are the modifiers in force, which only a group such as (?i:...) changes. }
    ICase, Multiline, DotAll: Boolean;
    { NeedsBacktracking records that the pattern has a construct only the backtracking matcher can run. }
    NeedsBacktracking: Boolean;
    { Nodes holds the syntax tree, of which the first NodeCount elements are in use, and Sets the sets its nodes
      name, of which the first SetCount are. }
    Nodes: TNodes;
    NodeCount: Int32;
    Sets: TCharSets;
    SetCount: Int32;
    { Failed says the pattern is not valid, Msg why, and Pos where. }
    Failed: Boolean;
    Msg: UTF8String;
    Pos: Int32;

    procedure Fail(const Message: UTF8String);
    function NewNode(Kind: TNodeKind): Int32;
    function NewParent(Kind: TNodeKind; Sub: Int32): Int32;
    function AddSet(const CharSet: TCharSet): Int32;
    procedure ScanGroups;
    function More: Boolean;
    function Peek: Int32;
    function Eat(C: Int32): Boolean;
    function Disjunction: Int32;
    function Alternative: Int32;
    function CharNode(C: Int32): Int32;
    function SetNode(const CharSet: TCharSet): Int32;
    function Term: Int32;
    function LooksLikeQuantifier: Boolean;
    function Number: Int32;
    function Quantifier(out Min, Max: Int32; out Lazy: Boolean): Boolean;
    function Group(out Quantifiable: Boolean): Int32;
    function ModifierGroup: Int32;
    function Capture(const Name: UTF8String): Int32;
    procedure Close;
    function GroupName: UTF8String;
    function AtomEscape(out Quantifiable: Boolean): Int32;
    function LegacyOctal: Int32;
    function ClassEscape(C: Int32; out CharSet: TCharSet): Boolean;
    function Hex(N: Int32): Int32;
    function CharacterEscape(InClass: Boolean): Int32;
    function UnicodeEscape(Braces: Boolean): Int32;
    function CharClass: Int32;
    function ClassAtom(out C: Int32; out CharSet: TCharSet): Boolean;
  end;

{ Parse reads the pattern. It returns false, with the reason, for a pattern that is not valid under the chosen
  grammar. Root is the node the tree starts at. }
function Parse(const Pattern: UTF8String; Unicode: Boolean; out Ps: TParser; out Root: Int32;
  out Error: UTF8String): Boolean;

implementation

uses
  SysUtils,
  Corvus.JsonSchema.EcmaRegex.Utf8;

var
  { These are set when the unit is initialised and never written again. }
  AnySet: TCharSet;
  DigitSet, NotDigitSet, WordSet, NotWordSet, SpaceSet, NotSpaceSet: TCharSet;
  IdStart, IdContinue: PUcdTable;

procedure TParser.Fail(const Message: UTF8String);
begin
  if not Failed then begin
    Failed := True;
    Msg := Message;
    Pos := I;
  end;
  // Nothing is left to read, so every loop of the parser ends.
  I := Length(P);
end;

{ NewNode adds a node of a kind and returns its index. }
function TParser.NewNode(Kind: TNodeKind): Int32;
begin
  if NodeCount = Length(Nodes) then
    SetLength(Nodes, 2 * NodeCount + 16);
  Result := NodeCount;
  Inc(NodeCount);
  Nodes[Result].Kind := Kind;
  Nodes[Result].SetIx := -1;
end;

{ NewParent adds a node of a kind with one child. }
function TParser.NewParent(Kind: TNodeKind; Sub: Int32): Int32;
begin
  Result := NewNode(Kind);
  SetLength(Nodes[Result].Subs, 1);
  Nodes[Result].Subs[0] := Sub;
end;

{ AddSet adds a set and returns its index. }
function TParser.AddSet(const CharSet: TCharSet): Int32;
begin
  if SetCount = Length(Sets) then
    SetLength(Sets, 2 * SetCount + 8);
  Result := SetCount;
  Inc(SetCount);
  Sets[Result] := CharSet;
end;

function IntToUtf8(Value: Int32): UTF8String;
var
  Digits: array[0..11] of AnsiChar;
  N, K: Int32;
begin
  N := 0;
  repeat
    Digits[N] := AnsiChar(Ord('0') + Value mod 10);
    Value := Value div 10;
    Inc(N);
  until Value = 0;
  SetLength(Result, N);
  for K := 1 to N do
    Result[K] := Digits[N - K];
end;

function Parse(const Pattern: UTF8String; Unicode: Boolean; out Ps: TParser; out Root: Int32;
  out Error: UTF8String): Boolean;
var
  Bytes: TBytes;
  K, N, At, Width, Ref, Found, G, Count: Int32;
begin
  Ps := Default(TParser);
  Ps.Unicode := Unicode;
  Error := '';
  // The pattern as code points. A malformed byte is U+FFFD.
  SetLength(Bytes, Length(Pattern));
  for K := 1 to Length(Pattern) do
    Bytes[K - 1] := UInt8(Pattern[K]);
  SetLength(Ps.P, Length(Bytes));
  N := 0;
  At := 0;
  while At < Length(Bytes) do begin
    Ps.P[N] := DecodeRune(Bytes, At, Length(Bytes), Width);
    Inc(At, Width);
    Inc(N);
  end;
  SetLength(Ps.P, N);
  // Node 0 stands for no node.
  Ps.NewNode(NEmpty);
  Ps.ScanGroups;
  Root := Ps.Disjunction;
  if (not Ps.Failed) and (Ps.I < Length(Ps.P)) then begin
    if Ps.P[Ps.I] = Ord(')') then
      Ps.Fail('unmatched '')''')
    else
      Ps.Fail('unexpected character');
  end;
  if not Ps.Failed then
    for K := 0 to High(Ps.NamedRefs) do begin
      Ref := Ps.NamedRefs[K];
      Found := -1;
      for N := 0 to High(Ps.Names) do
        if Ps.Names[N].Name = Ps.Nodes[Ref].Name then
          Found := N;
      if Found < 0 then begin
        Ps.Fail('reference to an unknown group name');
        Break;
      end;
      Count := Length(Ps.Names[Found].Groups);
      SetLength(Ps.Nodes[Ref].Groups, Count);
      for G := 0 to Count - 1 do
        Ps.Nodes[Ref].Groups[G] := Ps.Names[Found].Groups[G].Group;
    end;
  if Ps.Failed then begin
    Root := 0;
    Error := 'ecmaregex: ' + Ps.Msg + ' at offset ' + IntToUtf8(Ps.Pos) + ' in "' + Pattern + '"';
    Exit(False);
  end;
  Result := True;
end;

{ ScanGroups counts the capturing groups of the pattern and notes whether any is named. }
procedure TParser.ScanGroups;
var
  InClass: Boolean;
  K, C: Int32;
begin
  InClass := False;
  K := 0;
  while K < Length(P) do begin
    C := P[K];
    if C = Ord('\') then
      Inc(K)
    else if C = Ord('[') then
      InClass := True
    else if C = Ord(']') then
      InClass := False
    else if (C = Ord('(')) and not InClass then begin
      if (K + 1 >= Length(P)) or (P[K + 1] <> Ord('?')) then
        Inc(GroupCount)
      else if (K + 3 < Length(P)) and (P[K + 2] = Ord('<')) and (P[K + 3] <> Ord('=')) and
        (P[K + 3] <> Ord('!')) then begin
        Inc(GroupCount);
        HasNames := True;
      end;
    end;
    Inc(K);
  end;
end;

function TParser.More: Boolean;
begin
  Result := I < Length(P);
end;

function TParser.Peek: Int32;
begin
  if I < Length(P) then
    Exit(P[I]);
  Result := -1;
end;

function TParser.Eat(C: Int32): Boolean;
begin
  if (I < Length(P)) and (P[I] = C) then begin
    Inc(I);
    Exit(True);
  end;
  Result := False;
end;

function TParser.Disjunction: Int32;
var
  Last, First, Alt, Sub, N: Int32;
  Subs: TInt32Array;
begin
  Inc(Depth);
  if Depth > MaxNesting then begin
    Fail('groups nested too deeply');
    Exit(0);
  end;
  Inc(Disjunctions);
  if PathLen = Length(Path) then
    SetLength(Path, 2 * PathLen + 8);
  Path[PathLen].Disjunction := Disjunctions;
  Path[PathLen].Alternative := 0;
  Inc(PathLen);
  Last := PathLen - 1;
  First := Alternative;
  if Failed then
    Exit(0);
  if Peek <> Ord('|') then begin
    PathLen := Last;
    Dec(Depth);
    Exit(First);
  end;
  SetLength(Subs, 4);
  Subs[0] := First;
  N := 1;
  while Eat(Ord('|')) do begin
    Inc(Path[Last].Alternative);
    Sub := Alternative;
    if Failed then
      Exit(0);
    if N = Length(Subs) then
      SetLength(Subs, 2 * N);
    Subs[N] := Sub;
    Inc(N);
  end;
  SetLength(Subs, N);
  Alt := NewNode(NAlt);
  Nodes[Alt].Subs := Subs;
  PathLen := Last;
  Dec(Depth);
  Result := Alt;
end;

function TParser.Alternative: Int32;
var
  Terms: TInt32Array;
  N, Sub: Int32;
begin
  Terms := nil;
  N := 0;
  while More and (Peek <> Ord('|')) and (Peek <> Ord(')')) do begin
    Sub := Term;
    if Failed then
      Exit(0);
    if N = Length(Terms) then
      SetLength(Terms, 2 * N + 4);
    Terms[N] := Sub;
    Inc(N);
  end;
  if N = 0 then
    Exit(NewNode(NEmpty));
  if N = 1 then
    Exit(Terms[0]);
  SetLength(Terms, N);
  Result := NewNode(NCat);
  Nodes[Result].Subs := Terms;
end;

{ CharNode returns the node for one literal character. Inside a case-insensitive group that is the class of the
  characters equivalent to it. }
function TParser.CharNode(C: Int32): Int32;
var
  CharSet: TCharSet;
  SetIx: Int32;
begin
  if ICase then begin
    CharSet := FoldClosure(NewCharSet([C, C], False), Unicode);
    if (Length(CharSet.Ranges) <> 2) or (CharSet.Ranges[0] <> CharSet.Ranges[1]) then begin
      SetIx := AddSet(CharSet);
      Result := NewNode(NSet);
      Nodes[Result].SetIx := SetIx;
      Exit;
    end;
  end;
  Result := NewNode(NChar);
  Nodes[Result].R := C;
end;

{ SetNode returns the node for a class. Inside a case-insensitive group the class also takes every character
  equivalent to one of its members. }
function TParser.SetNode(const CharSet: TCharSet): Int32;
var
  SetIx: Int32;
begin
  if ICase then
    SetIx := AddSet(FoldClosure(CharSet, Unicode))
  else
    SetIx := AddSet(CharSet);
  Result := NewNode(NSet);
  Nodes[Result].SetIx := SetIx;
end;

function TParser.Term: Int32;
var
  C, Atom, Min, Max, SetIx: Int32;
  Quantifiable, Lazy: Boolean;
begin
  C := Peek;
  Quantifiable := True;
  Atom := 0;
  case C of
    Ord('^'): begin
      Inc(I);
      Atom := NewNode(NBOL);
      Quantifiable := False;
      if Multiline then begin
        Nodes[Atom].Kind := NLineStart;
        NeedsBacktracking := True;
      end;
    end;
    Ord('$'): begin
      Inc(I);
      Atom := NewNode(NEOL);
      Quantifiable := False;
      if Multiline then begin
        Nodes[Atom].Kind := NLineEnd;
        NeedsBacktracking := True;
      end;
    end;
    Ord('('):
      Atom := Group(Quantifiable);
    Ord('.'): begin
      Inc(I);
      if DotAll then
        SetIx := AddSet(AnySet)
      else
        SetIx := AddSet(DotSet);
      Atom := NewNode(NSet);
      Nodes[Atom].SetIx := SetIx;
    end;
    Ord('['):
      Atom := CharClass;
    Ord('\'):
      Atom := AtomEscape(Quantifiable);
    Ord('*'), Ord('+'), Ord('?'):
      Fail('nothing to repeat');
    Ord('{'): begin
      if Unicode or LooksLikeQuantifier then
        Fail('nothing to repeat')
      else begin
        Inc(I);
        Atom := CharNode(Ord('{'));
      end;
    end;
    Ord(']'), Ord('}'): begin
      if Unicode then
        Fail('lone bracket')
      else begin
        Inc(I);
        Atom := CharNode(C);
      end;
    end;
  else
    Inc(I);
    Atom := CharNode(C);
  end;
  if Failed then
    Exit(0);
  if not Quantifier(Min, Max, Lazy) then begin
    if Failed then
      Exit(0);
    Exit(Atom);
  end;
  if not Quantifiable then begin
    Fail('nothing to repeat');
    Exit(0);
  end;
  Result := NewParent(NRepeat, Atom);
  Nodes[Result].Min := Min;
  Nodes[Result].Max := Max;
  Nodes[Result].Lazy := Lazy;
end;

function IsDigit(C: Int32): Boolean;
begin
  Result := (C >= Ord('0')) and (C <= Ord('9'));
end;

(* LooksLikeQuantifier reports whether the text at the current '{' reads as {n}, {n,} or {n,m}. *)
function TParser.LooksLikeQuantifier: Boolean;
var
  K, Start: Int32;
begin
  K := I + 1;
  Start := K;
  while (K < Length(P)) and IsDigit(P[K]) do
    Inc(K);
  if (K = Start) or (K >= Length(P)) then
    Exit(False);
  if P[K] = Ord('}') then
    Exit(True);
  if P[K] <> Ord(',') then
    Exit(False);
  Inc(K);
  while (K < Length(P)) and IsDigit(P[K]) do
    Inc(K);
  Result := (K < Length(P)) and (P[K] = Ord('}'));
end;

{ Number reads a run of decimal digits, saturating at MaxCount. }
function TParser.Number: Int32;
var
  Start: Int32;
  V: Int64;
begin
  Start := I;
  V := 0;
  while (I < Length(P)) and IsDigit(P[I]) do begin
    if V < MaxCount then begin
      V := V * 10 + (P[I] - Ord('0'));
      if V > MaxCount then
        V := MaxCount;
    end;
    Inc(I);
  end;
  if Start = I then begin
    Fail('expected a number');
    Exit(0);
  end;
  Result := Int32(V);
end;

function TParser.Quantifier(out Min, Max: Int32; out Lazy: Boolean): Boolean;
var
  C: Int32;
begin
  Min := 0;
  Max := 0;
  Lazy := False;
  C := Peek;
  if C = Ord('*') then begin
    Inc(I);
    Min := 0;
    Max := Unbounded;
  end else if C = Ord('+') then begin
    Inc(I);
    Min := 1;
    Max := Unbounded;
  end else if C = Ord('?') then begin
    Inc(I);
    Min := 0;
    Max := 1;
  end else if (C = Ord('{')) and LooksLikeQuantifier then begin
    Inc(I);
    Min := Number;
    Max := Min;
    if Eat(Ord(',')) then begin
      if Peek = Ord('}') then
        Max := Unbounded
      else
        Max := Number;
    end;
    if Failed then
      Exit(False);
    if not Eat(Ord('}')) then begin
      Fail('malformed quantifier');
      Exit(False);
    end;
    if (Max <> Unbounded) and (Max < Min) then begin
      Fail('numbers out of order in quantifier');
      Exit(False);
    end;
  end else
    Exit(False);
  Lazy := Eat(Ord('?'));
  Result := True;
end;

{ SeparateAlternatives reports whether two positions lie in different alternatives of some disjunction. The
  positions are the first ALen elements of A and the first BLen elements of B. }
function SeparateAlternatives(const A: TBranches; ALen: Int32; const B: TBranches; BLen: Int32): Boolean;
var
  K: Int32;
begin
  K := 0;
  while (K < ALen) and (K < BLen) and (A[K].Disjunction = B[K].Disjunction) do begin
    if A[K].Alternative <> B[K].Alternative then
      Exit(True);
    Inc(K);
  end;
  Result := False;
end;

{ Group reads a group or a lookaround. It also reports whether a quantifier may follow. }
function TParser.Group(out Quantifiable: Boolean): Int32;
var
  C, Body, N, K: Int32;
  Negative: Boolean;
  Name: UTF8String;
begin
  Quantifiable := True;
  Inc(I);
  if not Eat(Ord('?')) then
    Exit(Capture(''));
  if Eat(Ord(':')) then begin
    Body := Disjunction;
    Close;
    if Failed then
      Exit(0);
    Exit(NewParent(NGroup, Body));
  end;
  C := Peek;
  if (C = Ord('i')) or (C = Ord('m')) or (C = Ord('s')) or (C = Ord('-')) then
    Exit(ModifierGroup);
  if Eat(Ord('=')) or Eat(Ord('!')) then begin
    Negative := P[I - 1] = Ord('!');
    Body := Disjunction;
    Close;
    if Failed then
      Exit(0);
    NeedsBacktracking := True;
    // Annex B lets a quantifier follow a lookahead. The u flag does not.
    Quantifiable := not Unicode;
    Result := NewParent(NLook, Body);
    Nodes[Result].Negative := Negative;
    Exit;
  end;
  if Eat(Ord('<')) then begin
    if Eat(Ord('=')) or Eat(Ord('!')) then begin
      Negative := P[I - 1] = Ord('!');
      Body := Disjunction;
      Close;
      if Failed then
        Exit(0);
      NeedsBacktracking := True;
      Quantifiable := False;
      Result := NewParent(NLook, Body);
      Nodes[Result].Negative := Negative;
      Nodes[Result].Behind := True;
      Exit;
    end;
    Name := GroupName;
    if Failed then
      Exit(0);
    // Two groups may share a name only when they are in separate alternatives of one disjunction, so that no
    // match can go through both.
    for N := 0 to High(Names) do
      if Names[N].Name = Name then
        for K := 0 to High(Names[N].Groups) do
          if not SeparateAlternatives(Names[N].Groups[K].Path, Length(Names[N].Groups[K].Path), Path,
            PathLen) then begin
            Fail('duplicate group name');
            Exit(0);
          end;
    Exit(Capture(Name));
  end;
  Fail('invalid group');
  Result := 0;
end;

{ ModifierGroup reads a group that turns modifiers on or off for its body, such as (?i:...) or (?s-i:...). The
  position is after the "(?". }
function TParser.ModifierGroup: Int32;
var
  SavedICase, SavedMultiline, SavedDotAll, Removing: Boolean;
  Seen: array[0..2] of Boolean;
  Count, C, Flag, Body: Int32;
begin
  SavedICase := ICase;
  SavedMultiline := Multiline;
  SavedDotAll := DotAll;
  Seen[0] := False;
  Seen[1] := False;
  Seen[2] := False;
  Removing := False;
  Count := 0;
  while not Eat(Ord(':')) do begin
    C := Peek;
    Inc(I);
    if C = Ord('i') then begin
      Flag := 0;
      ICase := not Removing;
    end else if C = Ord('m') then begin
      Flag := 1;
      Multiline := not Removing;
    end else if C = Ord('s') then begin
      Flag := 2;
      DotAll := not Removing;
    end else if C = Ord('-') then begin
      if Removing then begin
        Fail('invalid group modifier');
        Exit(0);
      end;
      Removing := True;
      Continue;
    end else begin
      Fail('invalid group modifier');
      Exit(0);
    end;
    if Seen[Flag] then begin
      Fail('repeated group modifier');
      Exit(0);
    end;
    Seen[Flag] := True;
    Inc(Count);
  end;
  if Count = 0 then begin
    Fail('invalid group modifier');
    Exit(0);
  end;
  Body := Disjunction;
  Close;
  if Failed then
    Exit(0);
  ICase := SavedICase;
  Multiline := SavedMultiline;
  DotAll := SavedDotAll;
  Result := NewParent(NGroup, Body);
end;

function TParser.Capture(const Name: UTF8String): Int32;
var
  G, N, Found, K, Body: Int32;
begin
  Inc(Groups);
  G := NewNode(NGroup);
  Nodes[G].Group := Groups;
  if Name <> '' then begin
    Found := -1;
    for N := 0 to High(Names) do
      if Names[N].Name = Name then
        Found := N;
    if Found < 0 then begin
      Found := Length(Names);
      SetLength(Names, Found + 1);
      Names[Found].Name := Name;
    end;
    K := Length(Names[Found].Groups);
    SetLength(Names[Found].Groups, K + 1);
    Names[Found].Groups[K].Group := Groups;
    Names[Found].Groups[K].Path := Copy(Path, 0, PathLen);
  end;
  Body := Disjunction;
  Close;
  if Failed then
    Exit(0);
  SetLength(Nodes[G].Subs, 1);
  Nodes[G].Subs[0] := Body;
  Result := G;
end;

procedure TParser.Close;
begin
  if not Eat(Ord(')')) then
    Fail('unterminated group');
end;

{ GroupName reads a group name up to and including its '>'. A character of the name may be written as a \u
  escape. }
function TParser.GroupName: UTF8String;
var
  C, N: Int32;
begin
  Result := '';
  N := 0;
  while not Eat(Ord('>')) do begin
    if not More then begin
      Fail('invalid group name');
      Exit('');
    end;
    C := Peek;
    Inc(I);
    if C = Ord('\') then begin
      if not Eat(Ord('u')) then begin
        Fail('invalid group name');
        Exit('');
      end;
      C := UnicodeEscape(True);
      if C < 0 then begin
        Fail('invalid group name');
        Exit('');
      end;
    end;
    if N = 0 then begin
      if not ((C = Ord('$')) or (C = Ord('_')) or IdStart^.Contains(C)) then begin
        Fail('invalid group name');
        Exit('');
      end;
    end else if not ((C = Ord('$')) or (C = $200C) or (C = $200D) or IdContinue^.Contains(C)) then begin
      Fail('invalid group name');
      Exit('');
    end;
    AppendRuneString(Result, C);
    Inc(N);
  end;
  if N = 0 then begin
    Fail('invalid group name');
    Exit('');
  end;
end;

{ AtomEscape reads an escape outside a class. It also reports whether a quantifier may follow. }
function TParser.AtomEscape(out Quantifiable: Boolean): Int32;
var
  C, Start, N, R: Int32;
  Name: UTF8String;
  CharSet: TCharSet;
begin
  Quantifiable := True;
  Inc(I);
  if not More then begin
    Fail('\ at end of pattern');
    Exit(0);
  end;
  C := Peek;
  if (C = Ord('b')) or (C = Ord('B')) then begin
    Inc(I);
    if C = Ord('B') then
      Result := NewNode(NNotWordBoundary)
    else
      Result := NewNode(NWordBoundary);
    // With the u flag, a case-insensitive word boundary counts two more characters as word characters, which
    // an automaton over bytes does not.
    if Unicode and ICase then begin
      Nodes[Result].Fold := True;
      NeedsBacktracking := True;
    end;
    Quantifiable := False;
    Exit;
  end;
  if C = Ord('k') then begin
    // Annex B reads \k as the letter k in a pattern with no named group.
    if Unicode or HasNames then begin
      Inc(I);
      if not Eat(Ord('<')) then begin
        Fail('invalid named reference');
        Exit(0);
      end;
      Name := GroupName;
      if Failed then
        Exit(0);
      Result := NewNode(NBackref);
      Nodes[Result].Name := Name;
      Nodes[Result].Fold := ICase;
      N := Length(NamedRefs);
      SetLength(NamedRefs, N + 1);
      NamedRefs[N] := Result;
      NeedsBacktracking := True;
      Exit;
    end;
    Inc(I);
    Exit(CharNode(Ord('k')));
  end;
  if (C >= Ord('1')) and (C <= Ord('9')) then begin
    Start := I;
    N := Number;
    if N <= GroupCount then begin
      NeedsBacktracking := True;
      Result := NewNode(NBackref);
      SetLength(Nodes[Result].Groups, 1);
      Nodes[Result].Groups[0] := N;
      Nodes[Result].Fold := ICase;
      Exit;
    end;
    if Unicode then begin
      Fail('reference to a group that does not exist');
      Exit(0);
    end;
    // Annex B reads it as an octal escape, or as the digit itself.
    I := Start;
    Exit(CharNode(LegacyOctal));
  end;
  if ClassEscape(C, CharSet) then
    Exit(SetNode(CharSet));
  if Failed then
    Exit(0);
  R := CharacterEscape(False);
  if Failed then
    Exit(0);
  Result := CharNode(R);
end;

{ LegacyOctal reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start
  one (8 or 9) stands for itself. }
function TParser.LegacyOctal: Int32;
var
  V, K: Int32;
begin
  V := 0;
  K := 0;
  while (K < 3) and More and (Peek >= Ord('0')) and (Peek <= Ord('7')) and (V * 8 + (Peek - Ord('0')) <= 255) do begin
    V := V * 8 + (Peek - Ord('0'));
    Inc(I);
    Inc(K);
  end;
  if K = 0 then begin
    V := Peek;
    Inc(I);
  end;
  Result := V;
end;

(* ClassEscape reads a class escape such as \d or \p{...} whose letter is C. It returns false, consuming nothing,
  when C does not start one. It also returns false when the escape is not valid, and then the parser has failed. *)
function TParser.ClassEscape(C: Int32; out CharSet: TCharSet): Boolean;
var
  Start, K: Int32;
  Expr: UTF8String;
begin
  CharSet := Default(TCharSet);
  case C of
    Ord('d'): begin
      Inc(I);
      CharSet := DigitSet;
    end;
    Ord('D'): begin
      Inc(I);
      CharSet := NotDigitSet;
    end;
    Ord('w'): begin
      Inc(I);
      if Unicode and ICase then
        CharSet := FoldWordSet
      else
        CharSet := WordSet;
    end;
    Ord('W'): begin
      Inc(I);
      if Unicode and ICase then
        CharSet := NotFoldWordSet
      else
        CharSet := NotWordSet;
    end;
    Ord('s'): begin
      Inc(I);
      CharSet := SpaceSet;
    end;
    Ord('S'): begin
      Inc(I);
      CharSet := NotSpaceSet;
    end;
    Ord('p'), Ord('P'): begin
      // Without the u flag \p is the letter p.
      if not Unicode then
        Exit(False);
      Inc(I);
      if not Eat(Ord('{')) then begin
        Fail('invalid property name');
        Exit(False);
      end;
      Start := I;
      while More and (Peek <> Ord('}')) do
        Inc(I);
      Expr := '';
      for K := Start to I - 1 do
        AppendRuneString(Expr, P[K]);
      if not Eat(Ord('}')) then begin
        Fail('invalid property name');
        Exit(False);
      end;
      if not PropertySet(Expr, CharSet) then begin
        Fail('invalid property name');
        Exit(False);
      end;
      if C = Ord('P') then
        CharSet := NewCharSet(CharSet.Ranges, True);
    end;
  else
    Exit(False);
  end;
  Result := True;
end;

function HexValue(C: Int32): Int32;
begin
  if (C >= Ord('0')) and (C <= Ord('9')) then
    Exit(C - Ord('0'));
  if (C >= Ord('a')) and (C <= Ord('f')) then
    Exit(C - Ord('a') + 10);
  if (C >= Ord('A')) and (C <= Ord('F')) then
    Exit(C - Ord('A') + 10);
  Result := -1;
end;

{ Hex reads exactly N hexadecimal digits. It returns -1, consuming nothing, when they are not there. }
function TParser.Hex(N: Int32): Int32;
var
  V, K, D: Int32;
begin
  if I + N > Length(P) then
    Exit(-1);
  V := 0;
  for K := 0 to N - 1 do begin
    D := HexValue(P[I + K]);
    if D < 0 then
      Exit(-1);
    V := V * 16 + D;
  end;
  Inc(I, N);
  Result := V;
end;

function IsASCIILetter(C: Int32): Boolean;
begin
  Result := ((C >= Ord('a')) and (C <= Ord('z'))) or ((C >= Ord('A')) and (C <= Ord('Z')));
end;

{ SyntaxCharacter reports whether C is one of the characters the u flag lets an identity escape name. }
function SyntaxCharacter(C: Int32): Boolean;
begin
  case C of
    Ord('^'), Ord('$'), Ord('\'), Ord('.'), Ord('*'), Ord('+'), Ord('?'), Ord('('), Ord(')'), Ord('['), Ord(']'),
    Ord('{'), Ord('}'), Ord('|'), Ord('/'):
      Result := True;
  else
    Result := False;
  end;
end;

{ CharacterEscape reads the escape after a backslash, in or out of a class, and returns the code point it names. }
function TParser.CharacterEscape(InClass: Boolean): Int32;
var
  C, L, H, U: Int32;
begin
  C := Peek;
  Inc(I);
  case C of
    Ord('f'):
      Exit(12);
    Ord('n'):
      Exit(10);
    Ord('r'):
      Exit(13);
    Ord('t'):
      Exit(9);
    Ord('v'):
      Exit(11);
    Ord('c'): begin
      L := Peek;
      if IsASCIILetter(L) then begin
        Inc(I);
        Exit(L mod 32);
      end;
      if (not Unicode) and InClass and (IsDigit(L) or (L = Ord('_'))) then begin
        Inc(I);
        Exit(L mod 32);
      end;
      if Unicode then begin
        Fail('invalid control escape');
        Exit(0);
      end;
      // Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
      Dec(I);
      Exit(Ord('\'));
    end;
    Ord('0'): begin
      if More and IsDigit(Peek) then begin
        if Unicode then begin
          Fail('invalid decimal escape');
          Exit(0);
        end;
        Dec(I);
        Exit(LegacyOctal);
      end;
      Exit(0);
    end;
    Ord('x'): begin
      H := Hex(2);
      if H < 0 then begin
        if Unicode then begin
          Fail('invalid \x escape');
          Exit(0);
        end;
        Exit(Ord('x'));
      end;
      Exit(H);
    end;
    Ord('u'): begin
      U := UnicodeEscape(Unicode);
      if U < 0 then begin
        if Unicode then begin
          Fail('invalid \u escape');
          Exit(0);
        end;
        Exit(Ord('u'));
      end;
      Exit(U);
    end;
  end;
  if (C >= Ord('1')) and (C <= Ord('9')) and (not Unicode) and InClass then begin
    Dec(I);
    Exit(LegacyOctal);
  end;
  // Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows
  // any character.
  if Unicode and (not SyntaxCharacter(C)) and not (InClass and (C = Ord('-'))) then begin
    Fail('invalid escape');
    Exit(0);
  end;
  Result := C;
end;

(* UnicodeEscape reads what follows a \u and returns the code point, or -1, consuming nothing, when it is not a
  well-formed escape. With Braces set it accepts the \u{...} form. A surrogate pair written as two escapes is one
  code point. *)
function TParser.UnicodeEscape(Braces: Boolean): Int32;
var
  Start, Digits, V, U, Save, Low: Int32;
begin
  Start := I;
  if Braces and Eat(Ord('{')) then begin
    Digits := I;
    V := 0;
    while More and (HexValue(Peek) >= 0) do begin
      if V <= MaxRune then
        V := V * 16 + HexValue(Peek);
      Inc(I);
    end;
    if (Digits = I) or (not Eat(Ord('}'))) or (V > MaxRune) then begin
      I := Start;
      Exit(-1);
    end;
    Exit(V);
  end;
  U := Hex(4);
  if U < 0 then
    Exit(-1);
  if (U >= $D800) and (U <= $DBFF) and (I + 6 <= Length(P)) and (P[I] = Ord('\')) and (P[I + 1] = Ord('u')) then begin
    Save := I;
    Inc(I, 2);
    Low := Hex(4);
    if (Low >= $DC00) and (Low <= $DFFF) then
      Exit($10000 + ((U - $D800) shl 10) + (Low - $DC00));
    I := Save;
  end;
  Result := U;
end;

procedure AppendPair(var Pairs: TRunes; var N: Int32; Lo, Hi: Int32);
begin
  if N + 2 > Length(Pairs) then
    SetLength(Pairs, 2 * N + 16);
  Pairs[N] := Lo;
  Pairs[N + 1] := Hi;
  Inc(N, 2);
end;

procedure AppendClassAtom(var Pairs: TRunes; var N: Int32; C: Int32; IsSet: Boolean; const CharSet: TCharSet);
var
  K: Int32;
begin
  if IsSet then begin
    K := 0;
    while K < Length(CharSet.Ranges) do begin
      AppendPair(Pairs, N, CharSet.Ranges[K], CharSet.Ranges[K + 1]);
      Inc(K, 2);
    end;
    Exit;
  end;
  AppendPair(Pairs, N, C, C);
end;

{ CharClass reads a character class from its '[' to its ']'. }
function TParser.CharClass: Int32;
var
  Negated, FirstIsSet, SecondIsSet: Boolean;
  Pairs: TRunes;
  N, First, Second, SetIx: Int32;
  FirstSet, SecondSet, CharSet: TCharSet;
begin
  Inc(I);
  Negated := Eat(Ord('^'));
  Pairs := nil;
  N := 0;
  while True do begin
    if not More then begin
      Fail('unterminated character class');
      Exit(0);
    end;
    if Eat(Ord(']')) then
      Break;
    FirstIsSet := ClassAtom(First, FirstSet);
    if Failed then
      Exit(0);
    if (Peek = Ord('-')) and (I + 1 < Length(P)) and (P[I + 1] <> Ord(']')) then begin
      Inc(I);
      SecondIsSet := ClassAtom(Second, SecondSet);
      if Failed then
        Exit(0);
      if FirstIsSet or SecondIsSet then begin
        if Unicode then begin
          Fail('invalid character class range');
          Exit(0);
        end;
        // Annex B reads the '-' as itself when a class escape is at either end.
        AppendClassAtom(Pairs, N, First, FirstIsSet, FirstSet);
        AppendPair(Pairs, N, Ord('-'), Ord('-'));
        AppendClassAtom(Pairs, N, Second, SecondIsSet, SecondSet);
        Continue;
      end;
      if Second < First then begin
        Fail('range out of order in character class');
        Exit(0);
      end;
      AppendPair(Pairs, N, First, Second);
      Continue;
    end;
    AppendClassAtom(Pairs, N, First, FirstIsSet, FirstSet);
  end;
  // Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a
  // negated class excludes exactly those characters.
  SetLength(Pairs, N);
  CharSet := NewCharSet(Pairs, False);
  if ICase then
    CharSet := FoldClosure(CharSet, Unicode);
  if Negated then
    CharSet := NewCharSet(CharSet.Ranges, True);
  SetIx := AddSet(CharSet);
  Result := NewNode(NSet);
  Nodes[Result].SetIx := SetIx;
end;

{ ClassAtom reads one member of a class. It gives a code point, or returns true and gives a set for a class
  escape. }
function TParser.ClassAtom(out C: Int32; out CharSet: TCharSet): Boolean;
var
  E: Int32;
begin
  CharSet := Default(TCharSet);
  C := Peek;
  Inc(I);
  if C <> Ord('\') then
    Exit(False);
  if not More then begin
    Fail('\ at end of pattern');
    Exit(False);
  end;
  E := Peek;
  if E = Ord('b') then begin
    Inc(I);
    C := 8;
    Exit(False);
  end;
  C := 0;
  if ClassEscape(E, CharSet) then
    Exit(True);
  if Failed then
    Exit(False);
  C := CharacterEscape(True);
  Result := False;
end;

initialization
  AnySet := NewCharSet([0, MaxRune], False);
  DigitSet := NewCharSet(DigitPairs, False);
  NotDigitSet := NewCharSet(DigitPairs, True);
  WordSet := NewCharSet(WordPairs, False);
  NotWordSet := NewCharSet(WordPairs, True);
  SpaceSet := NewCharSet(SpacePairs, False);
  NotSpaceSet := NewCharSet(SpacePairs, True);
  IdStart := UcdBinary('ID_Start');
  IdContinue := UcdBinary('ID_Continue');
end.