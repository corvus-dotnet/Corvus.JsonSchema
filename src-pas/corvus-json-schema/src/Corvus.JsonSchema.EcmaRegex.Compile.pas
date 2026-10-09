unit Corvus.JsonSchema.EcmaRegex.Compile;
{$I corvus.inc}

{ The compiler of the backtracking matcher of the ECMA-262 regular expression engine. This unit is the port of
  compile.go of the ecmaregex package of the Go module. Corvus.JsonSchema.EcmaRegex describes the engine as a whole.

  What differs from the Go source: an instruction names its set by an index into the sets of the program, where Go
  holds a pointer, and the map from a group to its slot is an array. The working memory of a match is not pooled by
  the program as in Go. It belongs to the thread (see Corvus.JsonSchema.EcmaRegex.Vm). }

interface

uses
  SysUtils,
  Corvus.JsonSchema.EcmaRegex.CharSet,
  Corvus.JsonSchema.EcmaRegex.Parse;

type
  TOpcode = (
    // OpChar and OpSet consume one code point going forward. The Back forms consume the one before the position,
    // which is how the body of a lookbehind runs.
    OpChar,
    OpCharBack,
    OpSet,
    OpSetBack,
    // OpLit consumes a run of literal bytes going forward.
    OpLit,
    OpFail,
    OpBOL,
    OpEOL,
    OpWordBoundary,
    OpNotWordBoundary,
    // OpLineStart and OpLineEnd are ^ and $ with the m modifier. The Fold forms of the word boundaries and of the
    // backreferences belong to a case-insensitive group.
    OpLineStart,
    OpLineEnd,
    OpWordBoundaryFold,
    OpNotWordBoundaryFold,
    OpJmp,
    // OpSplit continues at X and leaves Y as the alternative.
    OpSplit,
    // OpSaveTmp notes where a group was entered. OpCapture records the group once it has closed.
    OpSaveTmp,
    OpCapture,
    // OpClear resets the groups A to B, as ECMA-262 does at the start of each iteration of a quantified body.
    OpClear,
    OpBackref,
    OpBackrefBack,
    OpBackrefFold,
    OpBackrefFoldBack,
    // OpLook opens a lookaround. OpLookOK ends the body of a positive one and OpLookFail the body of a negative
    // one.
    OpLook,
    OpLookOK,
    OpLookFail,
    // OpRep and OpRepLazy repeat one character or class without entering the general loop.
    OpRep,
    OpRepLazy,
    // OpLoopInit, OpLoop and OpLoopIter run a counted or possibly empty quantified body.
    OpLoopInit,
    OpLoop,
    OpLoopIter,
    OpMatch);

  { A TInst is one instruction of the backtracking matcher. }
  TInst = record
    Op: TOpcode;
    { Flag marks a lazy OpLoop, a backward OpCapture, a negative OpLook, and an OpBackrefFold that folds case as
      the u flag grammar does. }
    Flag: Boolean;
    R: Int32;
    { SetIx is the set of the instruction, as an index into the sets of the program, or -1 for none. }
    SetIx: Int32;
    Lit: TBytes;
    { X and Y are jump targets. }
    X, Y: Int32;
    { A and B are a group, a loop or a range of groups, depending on the instruction. }
    A, B: Int32;
    { Min and Max bound a repetition. A max of -1 is no bound. }
    Min, Max: Int32;
  end;
  PInst = ^TInst;
  TInsts = array of TInst;

  { A TProgram is a pattern compiled for the backtracking matcher. }
  TProgram = record
    Insts: TInsts;
    { Sets holds the sets the instructions name. }
    Sets: TCharSets;
    { Groups is the number of capturing groups some backreference names, and Loops the number of counted loops. }
    Groups, Loops: Int32;
    { Anchored says every match starts at the start of the text. }
    Anchored: Boolean;
    { Prefix is literal text every match starts with. }
    Prefix: TBytes;
    { First names the set of every code point a match can start with. It is -1 when a match can be empty or when
      the set is not known. }
    First: Int32;
  end;

{ CompileProgram compiles the tree the parser read, which starts at the node Root. }
function CompileProgram(const Ps: TParser; Root: Int32; Unicode: Boolean): TProgram;

implementation

uses
  Corvus.JsonSchema.EcmaRegex.Utf8;

type
  TCompiler = record
    Nodes: TNodes;
    Sets: TCharSets;
    { Insts holds the instructions, of which the first InstCount are written. }
    Insts: TInsts;
    InstCount: Int32;
    { Tracked maps a capturing group's number to its slot, for the groups some backreference names. The others
      are not recorded at all, and have -1 here. TrackedCount is the number of slots. }
    Tracked: TInt32Array;
    TrackedCount: Int32;
    Loops: Int32;
    { Unicode says the pattern was read with the u flag grammar, which decides how a backreference folds case. }
    Unicode: Boolean;

    function Add(Op: TOpcode): Int32;
    function Pc: Int32;
    function TrackedSlot(Group: Int32): Int32;
    procedure WalkGroupRange(N: Int32; var Lo, Hi: Int32);
    function GroupRange(N: Int32; out Lo, Hi: Int32): Boolean;
    procedure Emit(N: Int32; Back: Boolean);
    procedure EmitBody(Body: Int32; Back, Clear: Boolean; ClearLo, ClearHi: Int32);
    procedure SetSplit(At, Enter, Leave: Int32; Lazy: Boolean);
    procedure EmitRepeat(N: Int32; Back: Boolean);
  end;

procedure CollectBackrefs(const Nodes: TNodes; N: Int32; var Seen: array of Boolean);
var
  K: Int32;
begin
  if Nodes[N].Kind = NBackref then
    for K := 0 to High(Nodes[N].Groups) do
      Seen[Nodes[N].Groups[K]] := True;
  for K := 0 to High(Nodes[N].Subs) do
    CollectBackrefs(Nodes, Nodes[N].Subs[K], Seen);
end;

{ Add writes an instruction with nothing but its opcode set and returns its index. }
function TCompiler.Add(Op: TOpcode): Int32;
begin
  if InstCount = Length(Insts) then
    SetLength(Insts, 2 * InstCount + 16);
  Result := InstCount;
  Inc(InstCount);
  Insts[Result].Op := Op;
  Insts[Result].SetIx := -1;
end;

function TCompiler.Pc: Int32;
begin
  Result := InstCount;
end;

{ TrackedSlot returns the slot of a group, or -1 when no backreference names it. }
function TCompiler.TrackedSlot(Group: Int32): Int32;
begin
  if (Group <= 0) or (Group >= Length(Tracked)) then
    Exit(-1);
  Result := Tracked[Group];
end;

procedure TCompiler.WalkGroupRange(N: Int32; var Lo, Hi: Int32);
var
  T, K: Int32;
begin
  if (Nodes[N].Kind = NGroup) and (Nodes[N].Group > 0) then begin
    T := TrackedSlot(Nodes[N].Group);
    if T >= 0 then begin
      if (Lo < 0) or (T < Lo) then
        Lo := T;
      if T > Hi then
        Hi := T;
    end;
  end;
  for K := 0 to High(Nodes[N].Subs) do
    WalkGroupRange(Nodes[N].Subs[K], Lo, Hi);
end;

{ GroupRange returns the first and last slot of the tracked groups inside N, or false when it has none. }
function TCompiler.GroupRange(N: Int32; out Lo, Hi: Int32): Boolean;
begin
  Lo := -1;
  Hi := -1;
  WalkGroupRange(N, Lo, Hi);
  Result := Lo >= 0;
end;

{ LiteralRune reports whether a code point can be matched by comparing its UTF-8 bytes. A surrogate has no encoding,
  and U+FFFD is also what a malformed byte of the text decodes to. }
function LiteralRune(R: Int32): Boolean;
begin
  Result := ValidRune(R) and (R <> RuneError);
end;

{ Nullable reports whether N can match without consuming anything. It errs towards true. }
function Nullable(const Nodes: TNodes; N: Int32): Boolean;
var
  K: Int32;
begin
  case Nodes[N].Kind of
    NChar, NSet:
      Result := False;
    NCat: begin
      for K := 0 to High(Nodes[N].Subs) do
        if not Nullable(Nodes, Nodes[N].Subs[K]) then
          Exit(False);
      Result := True;
    end;
    NAlt: begin
      for K := 0 to High(Nodes[N].Subs) do
        if Nullable(Nodes, Nodes[N].Subs[K]) then
          Exit(True);
      Result := False;
    end;
    NGroup:
      Result := Nullable(Nodes, Nodes[N].Subs[0]);
    NRepeat:
      Result := (Nodes[N].Min = 0) or Nullable(Nodes, Nodes[N].Subs[0]);
  else
    Result := True;
  end;
end;

{ Emit writes the instructions for N. With Back set the instructions match right to left, as the body of a
  lookbehind must. }
procedure TCompiler.Emit(N: Int32; Back: Boolean);
var
  I, J, K, T, Split, Look, Count: Int32;
  Lit: TBytes;
  Jumps: TInt32Array;
  Op: TOpcode;
begin
  case Nodes[N].Kind of
    NEmpty:
      ;
    NChar: begin
      if Back then
        K := Add(OpCharBack)
      else
        K := Add(OpChar);
      Insts[K].R := Nodes[N].R;
    end;
    NSet: begin
      if Sets[Nodes[N].SetIx].Empty then
        Add(OpFail)
      else begin
        if Back then
          K := Add(OpSetBack)
        else
          K := Add(OpSet);
        Insts[K].SetIx := Nodes[N].SetIx;
      end;
    end;
    NCat: begin
      Count := Length(Nodes[N].Subs);
      if Back then begin
        for I := Count - 1 downto 0 do
          Emit(Nodes[N].Subs[I], True);
        Exit;
      end;
      I := 0;
      while I < Count do begin
        // A run of literal characters becomes one comparison of bytes.
        J := I;
        Lit := nil;
        while (J < Count) and (Nodes[Nodes[N].Subs[J]].Kind = NChar) and
          LiteralRune(Nodes[Nodes[N].Subs[J]].R) do begin
          AppendRune(Lit, Nodes[Nodes[N].Subs[J]].R);
          Inc(J);
        end;
        if J - I >= 2 then begin
          K := Add(OpLit);
          Insts[K].Lit := Lit;
          I := J;
          Continue;
        end;
        Emit(Nodes[N].Subs[I], False);
        Inc(I);
      end;
    end;
    NAlt: begin
      Count := Length(Nodes[N].Subs);
      SetLength(Jumps, Count);
      for I := 0 to Count - 1 do begin
        if I = Count - 1 then begin
          Emit(Nodes[N].Subs[I], Back);
          Break;
        end;
        Split := Add(OpSplit);
        Insts[Split].X := Pc;
        Emit(Nodes[N].Subs[I], Back);
        Jumps[I] := Add(OpJmp);
        Insts[Split].Y := Pc;
      end;
      for I := 0 to Count - 2 do
        Insts[Jumps[I]].X := Pc;
    end;
    NGroup: begin
      T := TrackedSlot(Nodes[N].Group);
      if T < 0 then begin
        Emit(Nodes[N].Subs[0], Back);
        Exit;
      end;
      K := Add(OpSaveTmp);
      Insts[K].A := T;
      Emit(Nodes[N].Subs[0], Back);
      K := Add(OpCapture);
      Insts[K].A := T;
      Insts[K].Flag := Back;
    end;
    NBOL:
      Add(OpBOL);
    NEOL:
      Add(OpEOL);
    NLineStart:
      Add(OpLineStart);
    NLineEnd:
      Add(OpLineEnd);
    NWordBoundary: begin
      if Nodes[N].Fold then
        Add(OpWordBoundaryFold)
      else
        Add(OpWordBoundary);
    end;
    NNotWordBoundary: begin
      if Nodes[N].Fold then
        Add(OpNotWordBoundaryFold)
      else
        Add(OpNotWordBoundary);
    end;
    NBackref: begin
      Op := OpBackref;
      if Nodes[N].Fold and Back then
        Op := OpBackrefFoldBack
      else if Nodes[N].Fold then
        Op := OpBackrefFold
      else if Back then
        Op := OpBackrefBack;
      // When several groups share the name, at most one has taken part, and the others match the empty string.
      // So matching each in turn matches the one that took part.
      for I := 0 to High(Nodes[N].Groups) do begin
        K := Add(Op);
        Insts[K].A := TrackedSlot(Nodes[N].Groups[I]);
        Insts[K].Flag := Unicode;
      end;
    end;
    NLook: begin
      Look := Add(OpLook);
      Insts[Look].Flag := Nodes[N].Negative;
      Emit(Nodes[N].Subs[0], Nodes[N].Behind);
      if Nodes[N].Negative then
        Add(OpLookFail)
      else
        Add(OpLookOK);
      Insts[Look].X := Pc;
    end;
    NRepeat:
      EmitRepeat(N, Back);
  end;
end;

{ EmitBody writes the body of a quantifier, after the instruction that resets its groups when it has any. }
procedure TCompiler.EmitBody(Body: Int32; Back, Clear: Boolean; ClearLo, ClearHi: Int32);
var
  K: Int32;
begin
  if Clear then begin
    K := Add(OpClear);
    Insts[K].A := ClearLo;
    Insts[K].B := ClearHi;
  end;
  Emit(Body, Back);
end;

{ SetSplit makes the OpSplit at At a choice between entering the body and leaving the loop, in the order the
  quantifier prefers. }
procedure TCompiler.SetSplit(At, Enter, Leave: Int32; Lazy: Boolean);
begin
  if Lazy then begin
    Insts[At].X := Leave;
    Insts[At].Y := Enter;
  end else begin
    Insts[At].X := Enter;
    Insts[At].Y := Leave;
  end;
end;

procedure TCompiler.EmitRepeat(N: Int32; Back: Boolean);
var
  Body, Min, Max, K, At, Enter, Loop, ClearLo, ClearHi: Int32;
  Clear, Lazy: Boolean;
begin
  Body := Nodes[N].Subs[0];
  if Nodes[N].Max = 0 then
    Exit;
  Min := Nodes[N].Min;
  Max := Nodes[N].Max;
  Lazy := Nodes[N].Lazy;
  if (not Back) and ((Nodes[Body].Kind = NChar) or
    ((Nodes[Body].Kind = NSet) and not Sets[Nodes[Body].SetIx].Empty)) then begin
    if Lazy then
      K := Add(OpRepLazy)
    else
      K := Add(OpRep);
    Insts[K].R := Nodes[Body].R;
    Insts[K].SetIx := Nodes[Body].SetIx;
    Insts[K].Min := Min;
    Insts[K].Max := Max;
    Exit;
  end;
  Clear := GroupRange(Body, ClearLo, ClearHi);
  // A body that always consumes something needs no check for empty iterations, so the unbounded forms and the
  // optional form are plain choices.
  if not Nullable(Nodes, Body) then begin
    if (Min = 0) and (Max = Unbounded) then begin
      At := Add(OpSplit);
      Enter := Pc;
      EmitBody(Body, Back, Clear, ClearLo, ClearHi);
      K := Add(OpJmp);
      Insts[K].X := At;
      SetSplit(At, Enter, Pc, Lazy);
      Exit;
    end;
    if (Min = 1) and (Max = Unbounded) then begin
      Enter := Pc;
      EmitBody(Body, Back, Clear, ClearLo, ClearHi);
      At := Add(OpSplit);
      SetSplit(At, Enter, Pc, Lazy);
      Exit;
    end;
    if (Min = 0) and (Max = 1) then begin
      At := Add(OpSplit);
      Enter := Pc;
      EmitBody(Body, Back, Clear, ClearLo, ClearHi);
      SetSplit(At, Enter, Pc, Lazy);
      Exit;
    end;
  end;
  K := Loops;
  Inc(Loops);
  At := Add(OpLoopInit);
  Insts[At].A := K;
  Loop := Add(OpLoop);
  Insts[Loop].A := K;
  Insts[Loop].Min := Min;
  Insts[Loop].Max := Max;
  Insts[Loop].Flag := Lazy;
  At := Add(OpLoopIter);
  Insts[At].A := K;
  EmitBody(Body, Back, Clear, ClearLo, ClearHi);
  At := Add(OpJmp);
  Insts[At].X := Loop;
  Insts[Loop].X := Pc;
end;

{ StartsAtBOL reports whether every way through N begins with ^. }
function StartsAtBOL(const Nodes: TNodes; N: Int32): Boolean;
var
  K: Int32;
begin
  case Nodes[N].Kind of
    NBOL:
      Result := True;
    NCat:
      Result := StartsAtBOL(Nodes, Nodes[N].Subs[0]);
    NAlt: begin
      for K := 0 to High(Nodes[N].Subs) do
        if not StartsAtBOL(Nodes, Nodes[N].Subs[K]) then
          Exit(False);
      Result := True;
    end;
    NGroup:
      Result := StartsAtBOL(Nodes, Nodes[N].Subs[0]);
    NRepeat:
      Result := (Nodes[N].Min >= 1) and StartsAtBOL(Nodes, Nodes[N].Subs[0]);
  else
    Result := False;
  end;
end;

{ LiteralPrefix appends the literal text every match of N starts with. }
procedure LiteralPrefix(const Nodes: TNodes; N: Int32; var Prefix: TBytes);
var
  K, Sub: Int32;
begin
  case Nodes[N].Kind of
    NChar:
      if LiteralRune(Nodes[N].R) then
        AppendRune(Prefix, Nodes[N].R);
    NCat:
      for K := 0 to High(Nodes[N].Subs) do begin
        Sub := Nodes[N].Subs[K];
        if (Nodes[Sub].Kind <> NChar) or not LiteralRune(Nodes[Sub].R) then begin
          if (Length(Prefix) = 0) and (Nodes[Sub].Kind = NGroup) then
            LiteralPrefix(Nodes, Sub, Prefix);
          Break;
        end;
        AppendRune(Prefix, Nodes[Sub].R);
      end;
    NGroup:
      LiteralPrefix(Nodes, Nodes[N].Subs[0], Prefix);
  end;
end;

procedure AppendPair(var Pairs: TRunes; var Count: Int32; Lo, Hi: Int32);
begin
  if Count + 2 > Length(Pairs) then
    SetLength(Pairs, 2 * Count + 16);
  Pairs[Count] := Lo;
  Pairs[Count + 1] := Hi;
  Inc(Count, 2);
end;

{ FirstChars appends, as inclusive pairs, the code points a match of N can start with. It also reports in Empty
  whether N can match the empty string, and returns false when the set is not known. Assertions are treated as
  matching the empty string, which can only make the set larger than it need be. }
function FirstChars(const Nodes: TNodes; const Sets: TCharSets; N: Int32; var Pairs: TRunes; var Count: Int32;
  out Empty: Boolean): Boolean;
var
  K, SetIx: Int32;
  SubEmpty: Boolean;
begin
  Empty := False;
  case Nodes[N].Kind of
    NChar: begin
      AppendPair(Pairs, Count, Nodes[N].R, Nodes[N].R);
      Result := True;
    end;
    NSet: begin
      SetIx := Nodes[N].SetIx;
      K := 0;
      while K < Length(Sets[SetIx].Ranges) do begin
        AppendPair(Pairs, Count, Sets[SetIx].Ranges[K], Sets[SetIx].Ranges[K + 1]);
        Inc(K, 2);
      end;
      Result := True;
    end;
    NCat: begin
      for K := 0 to High(Nodes[N].Subs) do begin
        if not FirstChars(Nodes, Sets, Nodes[N].Subs[K], Pairs, Count, SubEmpty) then
          Exit(False);
        if not SubEmpty then
          Exit(True);
      end;
      Empty := True;
      Result := True;
    end;
    NAlt: begin
      for K := 0 to High(Nodes[N].Subs) do begin
        if not FirstChars(Nodes, Sets, Nodes[N].Subs[K], Pairs, Count, SubEmpty) then
          Exit(False);
        Empty := Empty or SubEmpty;
      end;
      Result := True;
    end;
    NGroup:
      Result := FirstChars(Nodes, Sets, Nodes[N].Subs[0], Pairs, Count, Empty);
    NRepeat: begin
      if Nodes[N].Max = 0 then begin
        Empty := True;
        Exit(True);
      end;
      Result := FirstChars(Nodes, Sets, Nodes[N].Subs[0], Pairs, Count, SubEmpty);
      Empty := SubEmpty or (Nodes[N].Min = 0);
    end;
    NBackref:
      Result := False;
  else
    Empty := True;
    Result := True;
  end;
end;

function CompileProgram(const Ps: TParser; Root: Int32; Unicode: Boolean): TProgram;
var
  C: TCompiler;
  Seen: array of Boolean;
  G, MaxGroup, Count: Int32;
  Pairs: TRunes;
  Empty: Boolean;
begin
  C := Default(TCompiler);
  C.Nodes := Ps.Nodes;
  C.Sets := Ps.Sets;
  C.Unicode := Unicode;
  // A backreference names a group the scan of the pattern counted, which the parser need not have read.
  MaxGroup := Ps.GroupCount;
  if Ps.Groups > MaxGroup then
    MaxGroup := Ps.Groups;
  SetLength(Seen, MaxGroup + 1);
  CollectBackrefs(C.Nodes, Root, Seen);
  // Slots follow group order, so the groups inside any one subtree take a contiguous range of slots.
  SetLength(C.Tracked, MaxGroup + 1);
  C.Tracked[0] := -1;
  for G := 1 to MaxGroup do
    if Seen[G] then begin
      C.Tracked[G] := C.TrackedCount;
      Inc(C.TrackedCount);
    end else
      C.Tracked[G] := -1;
  C.Emit(Root, False);
  C.Add(OpMatch);
  SetLength(C.Insts, C.InstCount);
  Result := Default(TProgram);
  Result.Insts := C.Insts;
  Result.Sets := Copy(Ps.Sets, 0, Ps.SetCount);
  Result.Groups := C.TrackedCount;
  Result.Loops := C.Loops;
  Result.First := -1;
  Result.Anchored := StartsAtBOL(C.Nodes, Root);
  if not Result.Anchored then begin
    LiteralPrefix(C.Nodes, Root, Result.Prefix);
    if Length(Result.Prefix) = 0 then begin
      Pairs := nil;
      Count := 0;
      if FirstChars(C.Nodes, C.Sets, Root, Pairs, Count, Empty) and not Empty then begin
        SetLength(Pairs, Count);
        Result.First := Length(Result.Sets);
        SetLength(Result.Sets, Result.First + 1);
        Result.Sets[Result.First] := NewCharSet(Pairs, False);
      end;
    end;
  end;
end;

end.