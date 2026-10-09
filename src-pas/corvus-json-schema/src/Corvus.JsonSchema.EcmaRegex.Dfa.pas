unit Corvus.JsonSchema.EcmaRegex.Dfa;
{$I corvus.inc}

{ A deterministic automaton for the patterns that need no backtracking, over ASCII text. This unit is the port of
  dfa.go of the ecmaregex package of the Go module. Corvus.JsonSchema.EcmaRegex describes the engine as a whole.

  Matching a pattern by walking its instructions costs tens of nanoseconds per byte of text. The strings a schema
  tests are short and nearly always ASCII, and most patterns in schemas are small. For such a pattern every state
  set the text can lead to is worked out once, and a match is then one table read per byte.

  The automaton reads bytes below 0x80 only. A text with any other byte is handed to the backtracking matcher from
  the start, so nothing here needs to know UTF-8 or the Unicode classes: a class is its ASCII members. A pattern
  with a word boundary, or whose automaton has more states than a byte can number, has no automaton and keeps to
  the backtracking matcher.

  What differs from the Go source: Go hands the texts the automaton does not decide to the regexp package of its
  standard library, and builds the automaton on the first match, behind a sync.Once. Here those texts go to the
  backtracking matcher, and the automaton is built when the pattern is compiled, because a compiled pattern is a
  plain value that threads share without a lock. A set of the pattern's states is found again through a small hash
  table, where Go uses a map with the set as its key. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.EcmaRegex.Parse;

const
  // The states every automaton has. DfaDead: no match can follow. DfaAccept: a match was found. DfaBail: the text
  // is not ASCII.
  DfaDead = 0;
  DfaAccept = 1;
  DfaBail = 2;
  DfaFirst = 3;

  // An automaton has at most this many states (they are numbered in a byte), built from at most this many states
  // of the pattern, in at most this many steps (a step is one state of the pattern looked at while a set is made).
  // The steps bound what a pattern too large for an automaton costs before that is known, to about a millisecond.
  MaxDFAStates = 256;
  MaxNFAStates = 512;
  MaxDFASteps = 400000;

type
  { TDfa is the automaton. A state is a byte, a byte of the text is one of a few classes (bytes the pattern does not
    tell apart), and Trans[State shl Shift or Class] is the state after the byte. }
  TDfa = record
    { ByteClass holds the class of each of the 256 bytes. }
    ByteClass: TBytes;
    Trans: TBytes;
    Shift: Int32;
    Start: UInt8;
    { For each state: the pattern matches if the text ends there. }
    AtEnd: array of Boolean;
    { The pattern matches the empty text. }
    Empty: Boolean;
  end;

{ DfaMatch reports in Matched whether the pattern matches the bytes Lo to Hi - 1 of the text. It returns false when
  the text is not ASCII, which this automaton does not decide. }
function DfaMatch(const D: TDfa; const Text: TBytes; Lo, Hi: Int32; out Matched: Boolean): Boolean;

{ BuildDFA builds the automaton of a pattern for an unanchored search, from the tree the parser read, which starts
  at the node Root. It returns false when the pattern has none. The caller must not ask for the automaton of a
  pattern with a counted quantifier above 1000 or with such quantifiers nested to a product above 1000: the number
  of states bounds the work here only when each repetition adds a state. }
function BuildDFA(const Ps: TParser; Root: Int32; out D: TDfa): Boolean;

implementation

uses
  Corvus.JsonSchema.EcmaRegex.CharSet;

type
  TNfaKind = (
    // NfaSet reads one byte of its set and goes to OutState.
    NfaSet,
    // NfaSplit goes to OutState and to AltState, reading nothing.
    NfaSplit,
    // NfaBOL and NfaEOL go to OutState at the start and at the end of the text.
    NfaBOL,
    NfaEOL,
    NfaMatch);

  TNfaState = record
    Kind: TNfaKind;
    Bits: array[0..1] of UInt64;
    OutState, AltState: Int32;
  end;
  TNfaStates = array of TNfaState;

  TNfaBuilder = record
    Nodes: TNodes;
    Sets: TCharSets;
    { States holds the states, of which the first Count are in use. }
    States: TNfaStates;
    Count: Int32;
    // The pattern has something an automaton over bytes cannot decide, or is too large.
    Failed: Boolean;
    function Add(Kind: TNfaKind; OutState, AltState: Int32): Int32;
    function Compile(N, Next: Int32): Int32;
  end;

  { A list of states of the pattern, of which the first Count elements of Items are in use. }
  TStateList = record
    Items: TInt32Array;
    Count: Int32;
  end;

  { TDfaBuilder makes the automaton's states from sets of the pattern's states. }
  TDfaBuilder = record
    Nfa: TNfaStates;
    NfaCount: Int32;
    // Scratch for closure: the states visited, by generation, and the stack.
    Mark: array of UInt32;
    Gen: UInt32;
    Stack: TStateList;
    // The steps left (see MaxDFASteps).
    Steps: Int32;
    procedure Closure(var CharSet: TStateList; const From: TStateList; AtStart, AtEnd: Boolean);
    function HasMatch(const CharSet: TStateList): Boolean;
  end;

procedure Append(var List: TStateList; Value: Int32);
begin
  if List.Count = Length(List.Items) then
    SetLength(List.Items, 2 * List.Count + 16);
  List.Items[List.Count] := Value;
  Inc(List.Count);
end;

function TNfaBuilder.Add(Kind: TNfaKind; OutState, AltState: Int32): Int32;
begin
  if Count >= MaxNFAStates then begin
    Failed := True;
    Exit(0);
  end;
  if Count = Length(States) then
    SetLength(States, 2 * Count + 16);
  States[Count].Kind := Kind;
  States[Count].Bits[0] := 0;
  States[Count].Bits[1] := 0;
  States[Count].OutState := OutState;
  States[Count].AltState := AltState;
  Result := Count;
  Inc(Count);
end;

{ Compile adds the states of a node that go on to Next, and returns the state they start at. }
function TNfaBuilder.Compile(N, Next: Int32): Int32;
var
  I, Start, Loop, Sub, First, R: Int32;
begin
  if Failed then
    Exit(0);
  case Nodes[N].Kind of
    NEmpty:
      Result := Next;
    NChar: begin
      Result := Add(NfaSet, Next, 0);
      R := Nodes[N].R;
      if (not Failed) and (R >= 0) and (R < 128) then
        States[Result].Bits[R shr 6] := UInt64(1) shl (R and 63);
    end;
    NSet: begin
      Result := Add(NfaSet, Next, 0);
      if not Failed then begin
        States[Result].Bits[0] := Sets[Nodes[N].SetIx].Ascii[0];
        States[Result].Bits[1] := Sets[Nodes[N].SetIx].Ascii[1];
      end;
    end;
    NCat: begin
      for I := High(Nodes[N].Subs) downto 0 do
        Next := Compile(Nodes[N].Subs[I], Next);
      Result := Next;
    end;
    NAlt: begin
      Start := Compile(Nodes[N].Subs[High(Nodes[N].Subs)], Next);
      for I := High(Nodes[N].Subs) - 1 downto 0 do begin
        First := Compile(Nodes[N].Subs[I], Next);
        Start := Add(NfaSplit, First, Start);
      end;
      Result := Start;
    end;
    NGroup:
      Result := Compile(Nodes[N].Subs[0], Next);
    NRepeat: begin
      Sub := Nodes[N].Subs[0];
      if Nodes[N].Max = Unbounded then begin
        // The loop, then the copies that must match before it.
        Loop := Add(NfaSplit, 0, Next);
        if Failed then
          Exit(0);
        First := Compile(Sub, Loop);
        States[Loop].OutState := First;
        Next := Loop;
      end else begin
        // The copies that may match, innermost last.
        I := Nodes[N].Min;
        while (I < Nodes[N].Max) and not Failed do begin
          First := Compile(Sub, Next);
          Next := Add(NfaSplit, First, Next);
          Inc(I);
        end;
      end;
      I := 0;
      while (I < Nodes[N].Min) and not Failed do begin
        Next := Compile(Sub, Next);
        Inc(I);
      end;
      Result := Next;
    end;
    NBOL:
      Result := Add(NfaBOL, Next, 0);
    NEOL:
      Result := Add(NfaEOL, Next, 0);
  else
    // A word boundary, or anything only the backtracking matcher can run.
    Failed := True;
    Result := 0;
  end;
end;

function DfaMatch(const D: TDfa; const Text: TBytes; Lo, Hi: Int32; out Matched: Boolean): Boolean;
var
  S: UInt8;
  K: Int32;
begin
  if Hi <= Lo then begin
    Matched := D.Empty;
    Exit(True);
  end;
  S := D.Start;
  if S = DfaAccept then begin
    Matched := True;
    Exit(True);
  end;
  for K := Lo to Hi - 1 do begin
    S := D.Trans[(Int32(S) shl D.Shift) or D.ByteClass[Text[K]]];
    if S < DfaFirst then begin
      Matched := S = DfaAccept;
      Exit(S <> DfaBail);
    end;
  end;
  Matched := D.AtEnd[S];
  Result := True;
end;

{ Closure adds to CharSet every state that reading nothing reaches from the states in From, keeping the states that
  read a byte, the end anchors still to pass, and the match. A start anchor is passed only AtStart, and an end
  anchor only AtEnd. }
procedure TDfaBuilder.Closure(var CharSet: TStateList; const From: TStateList; AtStart, AtEnd: Boolean);
var
  I, K: Int32;
begin
  Inc(Gen);
  Stack.Count := 0;
  for K := 0 to From.Count - 1 do
    Append(Stack, From.Items[K]);
  while Stack.Count > 0 do begin
    Dec(Stack.Count);
    I := Stack.Items[Stack.Count];
    Dec(Steps);
    if Mark[I] = Gen then
      Continue;
    Mark[I] := Gen;
    case Nfa[I].Kind of
      NfaSplit: begin
        Append(Stack, Nfa[I].OutState);
        Append(Stack, Nfa[I].AltState);
      end;
      NfaBOL:
        if AtStart then
          Append(Stack, Nfa[I].OutState);
      NfaEOL:
        if AtEnd then
          Append(Stack, Nfa[I].OutState)
        else
          Append(CharSet, I);
    else
      Append(CharSet, I);
    end;
  end;
end;

function TDfaBuilder.HasMatch(const CharSet: TStateList): Boolean;
var
  K: Int32;
begin
  for K := 0 to CharSet.Count - 1 do
    if Nfa[CharSet.Items[K]].Kind = NfaMatch then
      Exit(True);
  Result := False;
end;

{ SortSet puts the states of a set in increasing order, which is the form two equal sets are compared in. }
procedure SortSet(var CharSet: TStateList);
var
  I, J, Swap: Int32;
begin
  // Insertion sort: the sets are small.
  for I := 1 to CharSet.Count - 1 do begin
    J := I;
    while (J > 0) and (CharSet.Items[J] < CharSet.Items[J - 1]) do begin
      Swap := CharSet.Items[J];
      CharSet.Items[J] := CharSet.Items[J - 1];
      CharSet.Items[J - 1] := Swap;
      Dec(J);
    end;
  end;
end;

const
  { The size of the table that finds a set again. It is a power of two well above MaxDFAStates, so the table is
    never full. }
  IdSlots = 1024;

type
  { What BuildDFA keeps while it works. }
  TBuild = record
    B: TDfaBuilder;
    { Sets holds the set of each state of the automaton, and SetCount their number. }
    Sets: array of TInt32Array;
    SetCount: Int32;
    { Ids finds the state of a set: a slot holds a state, or -1. }
    Ids: array[0..IdSlots - 1] of Int32;
    Stride: Int32;
  end;

{ Intern gives a set its state. It returns false when there are too many. }
function Intern(var W: TBuild; var D: TDfa; var CharSet: TStateList; out Id: UInt8): Boolean;
var
  Hash: UInt32;
  Slot, K, State: Int32;
  Same: Boolean;
  Ends, Closed: TStateList;
begin
  Id := DfaDead;
  if W.B.HasMatch(CharSet) then begin
    Id := DfaAccept;
    Exit(True);
  end;
  if CharSet.Count = 0 then
    Exit(True);
  SortSet(CharSet);
  // The hash wraps on purpose.
  Hash := 2166136261;
  for K := 0 to CharSet.Count - 1 do
    Hash := UInt32((UInt64(Hash xor UInt32(CharSet.Items[K])) * 16777619) and $FFFFFFFF);
  Slot := Int32(Hash and (IdSlots - 1));
  while W.Ids[Slot] >= 0 do begin
    State := W.Ids[Slot];
    Same := Length(W.Sets[State]) = CharSet.Count;
    K := 0;
    while Same and (K < CharSet.Count) do begin
      Same := W.Sets[State][K] = CharSet.Items[K];
      Inc(K);
    end;
    if Same then begin
      Id := UInt8(State);
      Exit(True);
    end;
    Slot := (Slot + 1) and (IdSlots - 1);
  end;
  if W.SetCount >= MaxDFAStates then
    Exit(False);
  Id := UInt8(W.SetCount);
  W.Ids[Slot] := W.SetCount;
  if W.SetCount = Length(W.Sets) then
    SetLength(W.Sets, 2 * W.SetCount + 16);
  W.Sets[W.SetCount] := Copy(CharSet.Items, 0, CharSet.Count);
  Inc(W.SetCount);
  SetLength(D.Trans, W.SetCount * W.Stride);
  // At the end of the text the end anchors are passed.
  Ends := Default(TStateList);
  for K := 0 to CharSet.Count - 1 do
    if W.B.Nfa[CharSet.Items[K]].Kind = NfaEOL then
      Append(Ends, W.B.Nfa[CharSet.Items[K]].OutState);
  SetLength(D.AtEnd, W.SetCount);
  if Ends.Count <> 0 then begin
    Closed := Default(TStateList);
    W.B.Closure(Closed, Ends, False, True);
    D.AtEnd[W.SetCount - 1] := W.B.HasMatch(Closed);
  end;
  Result := True;
end;

function BuildDFA(const Ps: TParser; Root: Int32; out D: TDfa): Boolean;
var
  Nb: TNfaBuilder;
  W: TBuild;
  Match, Start, Classes, C, I, K, N, Id, ByteClass, Found: Int32;
  Signatures: array of TBytes;
  Signature: TBytes;
  Acc, Bits, Wanted, State: UInt8;
  Sample: array[0..128] of Int32;
  One, Restart, Closed, Moved, Next: TStateList;
  Same: Boolean;
begin
  D := Default(TDfa);
  Nb := Default(TNfaBuilder);
  Nb.Nodes := Ps.Nodes;
  Nb.Sets := Ps.Sets;
  Match := Nb.Add(NfaMatch, 0, 0);
  Start := Nb.Compile(Root, Match);
  if Nb.Failed then
    Exit(False);
  W := Default(TBuild);
  W.B.Nfa := Nb.States;
  W.B.NfaCount := Nb.Count;
  SetLength(W.B.Mark, Nb.Count);
  W.B.Steps := MaxDFASteps;
  for K := 0 to IdSlots - 1 do
    W.Ids[K] := -1;

  // The classes of bytes: two bytes are in one class when every set of the pattern has both or neither. Class 0 is
  // the bytes that are not ASCII.
  SetLength(D.ByteClass, 256);
  Classes := 1;
  SetLength(Signatures, 129);
  SetLength(Signature, Nb.Count div 8 + 1);
  for C := 0 to 127 do begin
    N := 0;
    Acc := 0;
    Bits := 0;
    for I := 0 to Nb.Count - 1 do begin
      if Nb.States[I].Kind <> NfaSet then
        Continue;
      Acc := UInt8(((Acc shl 1) or ((Nb.States[I].Bits[C shr 6] shr (C and 63)) and 1)) and $FF);
      Inc(Bits);
      if Bits = 8 then begin
        Signature[N] := Acc;
        Inc(N);
        Acc := 0;
        Bits := 0;
      end;
    end;
    Signature[N] := Acc;
    Inc(N);
    Found := -1;
    for K := 1 to Classes - 1 do begin
      Same := True;
      for I := 0 to N - 1 do
        if Signatures[K][I] <> Signature[I] then begin
          Same := False;
          Break;
        end;
      if Same then begin
        Found := K;
        Break;
      end;
    end;
    if Found < 0 then begin
      Found := Classes;
      Inc(Classes);
      Signatures[Found] := Copy(Signature, 0, N);
    end;
    D.ByteClass[C] := UInt8(Found);
  end;
  while (1 shl D.Shift) < Classes do
    Inc(D.Shift);
  // One byte of each class, to move a set by.
  for C := 0 to 128 do
    Sample[C] := 0;
  for C := 127 downto 0 do
    Sample[D.ByteClass[C]] := C;

  // The search is unanchored: a match may start at any position, so after every byte the states the pattern
  // starts in (not at the start of the text) join the set.
  One := Default(TStateList);
  Append(One, Start);
  Restart := Default(TStateList);
  W.B.Closure(Restart, One, False, False);
  Closed := Default(TStateList);
  W.B.Closure(Closed, One, True, True);
  D.Empty := W.B.HasMatch(Closed);

  W.Stride := 1 shl D.Shift;
  SetLength(D.Trans, DfaFirst * W.Stride);
  SetLength(D.AtEnd, DfaFirst);
  for ByteClass := 0 to W.Stride - 1 do begin
    D.Trans[DfaAccept * W.Stride + ByteClass] := DfaAccept;
    D.Trans[DfaBail * W.Stride + ByteClass] := DfaBail;
  end;
  SetLength(W.Sets, 16);
  W.SetCount := DfaFirst;
  Closed.Count := 0;
  W.B.Closure(Closed, One, True, False);
  if not Intern(W, D, Closed, State) then
    Exit(False);
  D.Start := State;
  Moved := Default(TStateList);
  Next := Default(TStateList);
  Id := DfaFirst;
  while Id < W.SetCount do begin
    for ByteClass := 0 to W.Stride - 1 do begin
      if (ByteClass = 0) or (ByteClass >= Classes) then begin
        // Not ASCII (or no such class).
        D.Trans[Id * W.Stride + ByteClass] := DfaBail;
        Continue;
      end;
      C := Sample[ByteClass];
      Moved.Count := 0;
      for K := 0 to High(W.Sets[Id]) do begin
        I := W.Sets[Id][K];
        if (W.B.Nfa[I].Kind = NfaSet) and (((W.B.Nfa[I].Bits[C shr 6] shr (C and 63)) and 1) <> 0) then
          Append(Moved, W.B.Nfa[I].OutState);
      end;
      for K := 0 to Restart.Count - 1 do
        Append(Moved, Restart.Items[K]);
      Next.Count := 0;
      W.B.Closure(Next, Moved, False, False);
      if (not Intern(W, D, Next, Wanted)) or (W.B.Steps < 0) then
        Exit(False);
      D.Trans[Id * W.Stride + ByteClass] := Wanted;
    end;
    Inc(Id);
  end;
  Result := True;
end;

end.