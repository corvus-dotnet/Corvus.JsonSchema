unit Corvus.JsonSchema.EcmaRegex.Vm;
{$I corvus.inc}

{ The backtracking matcher of the ECMA-262 regular expression engine. This unit is the port of vm.go of the
  ecmaregex package of the Go module. Corvus.JsonSchema.EcmaRegex describes the engine as a whole.

  Two things differ from the Go source.

  The text is the bytes Lo to Hi - 1 of an array, where Go is handed a slice that starts at 0. So every position
  here is an index into the array, the start of the text is Lo and its end is Hi.

  The working memory of a match (the backtracking stack, the groups and the loop counters) is pooled by each
  program in Go, in a sync.Pool. Here it belongs to the thread: it is one thread variable, shared by every program
  the thread runs, and it grows to the largest size any of them has needed. A match never runs inside another on
  one thread, so one is enough. That keeps a match free of allocation in the steady state with no lock and no
  atomic operation, and it keeps a compiled pattern a plain value that any number of threads can read. The public
  surface gives a match no parameter through which a caller could supply the memory, which is the other way to do
  it. The cost is that neither Free Pascal nor Delphi frees a thread variable of a managed type when its thread
  ends, so a thread that has matched and is about to end should call ReleaseThreadState. A thread that does not
  loses that memory once, and nothing else goes wrong.

  The limit of a match is a number of milliseconds, where Go has a duration. The clock is the tick count of the
  system (GetTickCount64 of Free Pascal, which reads the monotonic clock, and the time stamp of TStopwatch in
  Delphi), which never goes back and counts whole milliseconds. }

interface

uses
  SysUtils,
  {$IFNDEF FPC}
  System.Diagnostics,
  {$ENDIF}
  Corvus.JsonSchema.EcmaRegex.Compile;

const
  { Why a match was abandoned. StopTime says it ran for longer than its limit, and StopMemory that its
    backtracking stack reached MaxStack entries. Neither is an answer. }
  StopNone = 0;
  StopTime = 1;
  StopMemory = 2;

  { CheckEvery is the number of steps the matcher takes between two looks at the clock and at the depth of its
    stack. A step is one instruction, and eight characters of a single character loop, of a backreference or of a
    literal. A step takes some tens of nanoseconds, so the clock is read some tens of microseconds apart (44 to
    128 were measured), a match that ends before its first check never reads it, and a match goes past its limit
    by that much at most, and by the millisecond the clock counts in. }
  CheckEvery = 1 shl 12;

  (* MaxStack is the number of entries the backtracking stack of a match with a limit may hold (16 bytes each). A
     match that needs more is abandoned. A pattern that keeps a choice for every character of its text needs one
     entry or a few for each, so this is room for a text of some hundred thousand characters, and a pattern that
     pushes without consuming, such as (?:a?){2147483647}, reaches it in a few milliseconds. It is looked at with
     the clock, so the stack can go past it by CheckEvery entries. *)
  MaxStack = 1 shl 20;

  { MaxMemoWords is the largest memory of failed places, in words of 64 bits: 8 MB. A match that would need more
    goes without it. }
  MaxMemoWords = 1 shl 20;
  { MemoStepsPerPosition is the number of steps for each position of the text that a match makes before it takes
    the memory into use. }
  MemoStepsPerPosition = 64;
  { MaxRetainedMemo is the largest memory kept between matches. }
  MaxRetainedMemo = 1 shl 16;
  { MaxMarkDepth is the depth of the backtracking stack beyond which a memo point is not marked. A mark is an entry
    of the stack, and a pattern that keeps no choice for a character still marks a place for each, so on a long
    text the marks alone would reach MaxStack. A place that is not marked is not remembered, which loses time and
    nothing else, and the places below this depth are remembered when the stack unwinds, so a later start has the
    room to mark the next ones. }
  MaxMarkDepth = MaxStack div 2;

var
  { MemoFromTheStart makes every match take its memory of failed places before its first step. The tests set it,
    to run every pattern they have with the memory in use. A program built with CORVUS_MEMO_FROM_THE_START defined
    starts with it set: no program is built that way but the harnesses that run the pattern datasets through the
    port, which compare the answers of such a build with those of an ordinary one. Remembering a failure must
    change no answer. }
  MemoFromTheStart: Boolean = {$IFDEF CORVUS_MEMO_FROM_THE_START}True{$ELSE}False{$ENDIF};

{ MemoWouldBeTaken reports, for a program with Points memo points and a text of Width positions, whether a match
  has the memory of failed places in use after Checks checks, and in Never whether it never will. It is what the
  matcher decides at each check, given apart for the tests. }
function MemoWouldBeTaken(Points, Width, Checks: Int32; out Never: Boolean; out Words: Int32): Boolean;

{ ProgramMatch reports whether the program matches anywhere in the bytes Lo to Hi - 1 of the text, with no limit on
  the time or the memory the match takes. }
function ProgramMatch(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32): Boolean;

{ ProgramMatchLimit is ProgramMatch with a limit on the time the match may take, in milliseconds, when LimitMs is
  above zero. A match that reaches the limit is abandoned and Stop is StopTime. So is a match whose backtracking
  stack reaches MaxStack entries, and Stop is StopMemory. The result is then False and is not an answer. Stop is
  StopNone for a match that ran to its answer. }
function ProgramMatchLimit(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32; LimitMs: Int64;
  out Stop: UInt8): Boolean;

{ MonotonicMilliseconds reads the clock the limit of a match is measured with. }
function MonotonicMilliseconds: Int64;

{ ReleaseThreadState frees the working memory the calling thread's matches have used. }
procedure ReleaseThreadState;

implementation

uses
  Corvus.JsonSchema.EcmaRegex.Utf8,
  Corvus.JsonSchema.EcmaRegex.CharSet,
  Corvus.JsonSchema.EcmaRegex.Parse;

{ The kinds of entry on the backtracking stack. A choice entry says where to resume. An undo entry restores a value
  that was overwritten after the entries below it were pushed. }
const
  // EChoice resumes at Pc with the position Pos.
  EChoice = 0;
  // ERep belongs to a greedy single character loop that stopped at Pos. Resuming gives back one character, down
  // to the position A.
  ERep = 1;
  // ERepLazy belongs to a lazy single character loop that has taken A characters and stands at Pos. Resuming
  // takes one more.
  ERepLazy = 2;
  // ELook marks the start of a positive lookaround and ELookNeg of a negative one. Both hold the position to
  // restore, and ELookNeg the instruction to continue at when its body has failed.
  ELook = 3;
  ELookNeg = 4;
  // EMark lies under the choices of a memo point. When it is reached, every way from the point has failed, and
  // the bit Pos of the memory is set (see Memo of TState).
  EMark = 5;
  // ETmp restores the entry position of group Pc to Pos.
  ETmp = 6;
  // ECap restores the start and end of group Pc to Pos and A.
  ECap = 7;
  // ECounter restores the count and the iteration start of loop Pc to Pos and A.
  ECounter = 8;

  { MaxRetainedStack is the largest stack kept between matches. }
  MaxRetainedStack = 1 shl 16;

type
  TEntry = record
    Kind: UInt8;
    Pc: Int32;
    Pos: Int32;
    A: Int32;
  end;
  PEntry = ^TEntry;

  { A TState is the working memory of one match. It is kept by the thread, so a match allocates only while the
    stack is still growing to the depth the pattern and the text need. }
  TState = record
    { Stack holds the entries, of which the first Sp are in use. }
    Stack: array of TEntry;
    Sp: Int32;
    { Caps holds the start and end of each tracked group, or -1, and Tmp the position at which each was entered. }
    Caps: TInt32Array;
    Tmp: TInt32Array;
    { Counts holds, for each loop, the number of iterations started and the position at which the last one
      started. }
    Counts: TInt32Array;
    { LimitMs is the time the match may take, or zero for no limit. The clock starts at the first check, so
      Deadline is set when Timed is. }
    LimitMs: Int64;
    Deadline: Int64;
    Timed: Boolean;
    { Steps is the number of steps left before the next check, and Stop why the match was abandoned. }
    Steps: Int32;
    Stop: UInt8;
    { Memo is the memory of the places where a way has failed: one bit for each memo point of the program, each
      position of the text and each state of the loops around the point (see TMemoPoint). A bit is set when every
      way from the place has failed, and a way that comes to a place whose bit is set fails at once. So no place is
      gone through twice, and a pattern of repetitions inside repetitions takes time proportional to its size
      times the text and not time that doubles with each character.

      The memory costs its size to clear, and a mark on the stack for each place gone through, so a match starts
      without it. It takes it at the check by which it has made as many steps as the memory has words and
      MemoStepsPerPosition more for each position of the text, which a match that goes through its text once or a
      few times never makes. A place whose way failed before then is not remembered, which loses time and nothing
      else. MemoOn says the memory is in use, MemoOff that it never will be (the program has no memo points, or
      the memory would be larger than MaxMemoWords), Width is the number of positions of the text, MemoLo its
      first position and MemoWords the words in use (the array keeps the length it has grown to). }
    Memo: array of UInt64;
    MemoOn, MemoOff: Boolean;
    Width, Checks, Points, MemoLo, MemoWords: Int32;
  end;
  PState = ^TState;

  PCharSet = ^TCharSet;

threadvar
  ThreadState: TState;

procedure ReleaseThreadState;
var
  St: PState;
begin
  St := @ThreadState;
  St^.Stack := nil;
  St^.Caps := nil;
  St^.Tmp := nil;
  St^.Counts := nil;
  St^.Memo := nil;
  St^.Sp := 0;
end;

function MemoWouldBeTaken(Points, Width, Checks: Int32; out Never: Boolean; out Words: Int32): Boolean;
var
  Bits: Int64;
begin
  Never := False;
  Words := 0;
  if Points = 0 then begin
    Never := True;
    Exit(False);
  end;
  Bits := (Int64(Points) * Width) shl MaxMemoLoops;
  if (Bits + 63) shr 6 > MaxMemoWords then begin
    Never := True;
    Exit(False);
  end;
  Words := Int32((Bits + 63) shr 6);
  Result := MemoFromTheStart or (Int64(Checks) * CheckEvery >= Words + Int64(MemoStepsPerPosition) * Width);
end;

{ Remember takes the memory of failed places into use when the match has made enough steps to pay for it. }
procedure Remember(var St: TState);
var
  Never: Boolean;
  Words, K: Int32;
begin
  if not MemoWouldBeTaken(St.Points, St.Width, St.Checks, Never, Words) then begin
    St.MemoOff := Never;
    Exit;
  end;
  if Length(St.Memo) < Words then begin
    St.Memo := nil;
    SetLength(St.Memo, Words);
  end else
    for K := 0 to Words - 1 do
      St.Memo[K] := 0;
  St.MemoWords := Words;
  St.MemoOn := True;
end;

{ MemoIndex returns the bit of the memory for the memo point M at a position, with the state of the loops around
  the point: for each, whether it would refuse an iteration that ended here as empty. }
function MemoIndex(const Prog: TProgram; var St: TState; M, Pos: Int32): Int32;
var
  K, L: Int32;
begin
  Result := ((M - 1) * St.Width + (Pos - St.MemoLo)) shl MaxMemoLoops;
  for K := 0 to MaxMemoLoops - 1 do begin
    L := Prog.Points[M - 1].Loops[K];
    if (L >= 0) and (St.Counts[2 * L] > Prog.Points[M - 1].Mins[K]) and (St.Counts[2 * L + 1] = Pos) then
      Result := Result or (1 shl K);
  end;
end;

function MemoFailed(var St: TState; Idx: Int32): Boolean; inline;
begin
  Result := ((St.Memo[Idx shr 6] shr (Idx and 63)) and 1) <> 0;
end;

procedure MemoFail(var St: TState; Idx: Int32); inline;
begin
  St.Memo[Idx shr 6] := St.Memo[Idx shr 6] or (UInt64(1) shl (Idx and 63));
end;

{ QuickAllows reports whether a way can go on from the position. }
function QuickAllows(const Q: TQuick; const Text: TBytes; Pos, Hi: Int32): Boolean; inline;
var
  C: UInt8;
begin
  if Pos >= Hi then
    Exit(Q.AtEnd);
  if Q.Mid then
    Exit(True);
  C := Text[Pos];
  if C < $80 then
    Result := ((Q.Ascii[C shr 6] shr (C and 63)) and 1) <> 0
  else
    Result := Q.Other;
end;

function MonotonicMilliseconds: Int64;
begin
  {$IFDEF FPC}
  Result := Int64(GetTickCount64);
  {$ELSE}
  Result := TStopwatch.GetTimeStamp div 10000;
  {$ENDIF}
end;

{ Check is called every CheckEvery steps. It reports whether the match may go on. }
function Check(var St: TState): Boolean;
var
  Now: Int64;
begin
  if (not St.MemoOn) and (not St.MemoOff) then begin
    Inc(St.Checks);
    Remember(St);
  end;
  if St.LimitMs <= 0 then
    Exit(True);
  if St.Sp > MaxStack then begin
    St.Stop := StopMemory;
    Exit(False);
  end;
  Now := MonotonicMilliseconds;
  if not St.Timed then begin
    St.Timed := True;
    St.Deadline := Now + St.LimitMs;
    Exit(True);
  end;
  if Now > St.Deadline then begin
    St.Stop := StopTime;
    Exit(False);
  end;
  Result := True;
end;

procedure GrowStack(var St: TState);
begin
  SetLength(St.Stack, 2 * Length(St.Stack) + 64);
end;

procedure Push(var St: TState; Kind: UInt8; Pc, Pos, A: Int32); inline;
begin
  if St.Sp = Length(St.Stack) then
    GrowStack(St);
  St.Stack[St.Sp].Kind := Kind;
  St.Stack[St.Sp].Pc := Pc;
  St.Stack[St.Sp].Pos := Pos;
  St.Stack[St.Sp].A := A;
  Inc(St.Sp);
end;

{ Undo applies one undo entry. }
procedure Undo(var St: TState; const E: TEntry);
begin
  case E.Kind of
    ETmp:
      St.Tmp[E.Pc] := E.Pos;
    ECap: begin
      St.Caps[2 * E.Pc] := E.Pos;
      St.Caps[2 * E.Pc + 1] := E.A;
    end;
    ECounter: begin
      St.Counts[2 * E.Pc] := E.Pos;
      St.Counts[2 * E.Pc + 1] := E.A;
    end;
  end;
end;

{ LineTerminatorBefore reports whether the character before the position is an ECMA-262 line terminator, and
  LineTerminatorAt whether the character at the position is one. U+2028 and U+2029 are E2 80 A8 and E2 80 A9. }
function LineTerminatorBefore(const Text: TBytes; Lo, Pos: Int32): Boolean;
begin
  case Text[Pos - 1] of
    10, 13:
      Result := True;
    $A8, $A9:
      Result := (Pos - Lo >= 3) and (Text[Pos - 3] = $E2) and (Text[Pos - 2] = $80);
  else
    Result := False;
  end;
end;

function LineTerminatorAt(const Text: TBytes; Pos, Hi: Int32): Boolean;
begin
  case Text[Pos] of
    10, 13:
      Result := True;
    $E2:
      Result := (Pos + 2 < Hi) and (Text[Pos + 1] = $80) and ((Text[Pos + 2] = $A8) or (Text[Pos + 2] = $A9));
  else
    Result := False;
  end;
end;

{ EqualLit reports whether the bytes of the text from At are the bytes of Lit. The caller has checked that the text
  has that many. }
function EqualLit(const Text: TBytes; At: Int32; const Lit: TBytes): Boolean;
var
  K: Int32;
begin
  for K := 0 to High(Lit) do
    if Text[At + K] <> Lit[K] then
      Exit(False);
  Result := True;
end;

{ EqualSpans reports whether the N bytes of the text from A are the N bytes from B. }
function EqualSpans(const Text: TBytes; A, B, N: Int32): Boolean;
var
  K: Int32;
begin
  for K := 0 to N - 1 do
    if Text[A + K] <> Text[B + K] then
      Exit(False);
  Result := True;
end;

{ IndexOf returns the first position at or after At where the first bytes of Lit are in the text and the rest of
  Lit fits, or -1. It compares PrefixProbe bytes of Lit at most, so the search takes the text times that and not
  the text times the length of Lit. The matcher is run at the position found, and compares every byte there. (Go
  searches with bytes.Index, which takes time proportional to the text.) }
function IndexOf(const Text: TBytes; At, Hi: Int32; const Lit: TBytes): Int32;
const
  PrefixProbe = 16;
var
  Last, N, K: Int32;
  First: UInt8;
  Same: Boolean;
begin
  Last := Hi - Length(Lit);
  N := Length(Lit);
  if N > PrefixProbe then
    N := PrefixProbe;
  First := Lit[0];
  while At <= Last do begin
    if Text[At] = First then begin
      Same := True;
      for K := 1 to N - 1 do
        if Text[At + K] <> Lit[K] then begin
          Same := False;
          Break;
        end;
      if Same then
        Exit(At);
    end;
    Inc(At);
  end;
  Result := -1;
end;

{ Decode returns the character at the position At and its width in bytes: a code unit with Units set, and a code
  point otherwise. DecodeLast is Decode for the character that ends at the position. }
function Decode(const Text: TBytes; Lo, At, Hi: Int32; Units: Boolean; out W: Int32): Int32; inline;
begin
  if Units then
    Result := UnitAt(Text, Lo, At, Hi, W)
  else
    Result := DecodeRune(Text, At, Hi, W);
end;

function DecodeLast(const Text: TBytes; Lo, At, Hi: Int32; Units: Boolean; out W: Int32): Int32; inline;
begin
  if Units then
    Result := UnitBefore(Text, Lo, At, Hi, W)
  else
    Result := DecodeLastRune(Text, Lo, At, W);
end;

{ One returns the width of the character at the position At when the instruction's character or class matches it,
  and 0 otherwise. The character is a code unit when the program matches by code unit. }
function One(const Prog: TProgram; const Inst: TInst; const Text: TBytes; Lo, At, Hi: Int32): Int32;
var
  R, W: Int32;
begin
  if At >= Hi then
    Exit(0);
  R := Text[At];
  W := 1;
  if R >= RuneSelf then
    R := Decode(Text, Lo, At, Hi, Prog.Units, W);
  if Inst.SetIx >= 0 then begin
    if not Prog.Sets[Inst.SetIx].Contains(R) then
      Exit(0);
    Exit(W);
  end;
  if R <> Inst.R then
    Exit(0);
  Result := W;
end;

{ Run reports whether the program matches at the position Start. It keeps its choices on an explicit stack, so the
  depth of the search is bounded by memory and not by the stack of the thread. When it fails, every group and
  counter is back to the value it had on entry. It counts its steps, and when a check says the match has reached a
  limit it returns False with the reason in St.Stop. }
function Run(const Prog: TProgram; var St: TState; const Text: TBytes; Lo, Hi, Start: Int32): Boolean;
var
  Pc, Pos, R, W, At, N, Floor, From, Till, G, F, Keep, K, Want, Got, Steps: Int32;
  C: UInt8;
  I, Rep: PInst;
  CharSet: PCharSet;
  E: PEntry;
  Undone: TEntry;
  Before, After, Ok, Units, Memo, Marked, Last, Pass, Failed: Boolean;
  Kind: UInt8;
  Idx: Int32;
  Quick: PQuick;
begin
  // Units says the text is matched by UTF-16 code unit (see Corvus.JsonSchema.EcmaRegex.Utf8).
  Units := Prog.Units;
  St.Sp := 0;
  Pc := 0;
  Pos := Start;
  Steps := St.Steps;
  // Memo says the match has taken the memory of failed places into use.
  Memo := St.MemoOn;
  while True do begin
    Dec(Steps);
    if Steps < 0 then begin
      if not Check(St) then begin
        St.Steps := 0;
        Exit(False);
      end;
      Steps := CheckEvery;
      Memo := St.MemoOn;
    end;
    I := @Prog.Insts[Pc];
    case I^.Op of
      OpChar:
        if Pos < Hi then begin
          C := Text[Pos];
          if C < RuneSelf then begin
            if C = I^.R then begin
              Inc(Pos);
              Inc(Pc);
              Continue;
            end;
          end else if Decode(Text, Lo, Pos, Hi, Units, W) = I^.R then begin
            Inc(Pos, W);
            Inc(Pc);
            Continue;
          end;
        end;
      OpSet:
        if Pos < Hi then begin
          C := Text[Pos];
          CharSet := @Prog.Sets[I^.SetIx];
          if C < RuneSelf then begin
            if ((CharSet^.Ascii[C shr 6] shr (C and 63)) and 1) <> 0 then begin
              Inc(Pos);
              Inc(Pc);
              Continue;
            end;
          end else if CharSet^.Contains(Decode(Text, Lo, Pos, Hi, Units, W)) then begin
            Inc(Pos, W);
            Inc(Pc);
            Continue;
          end;
        end;
      OpLit:
        if Hi - Pos >= Length(I^.Lit) then begin
          // The bytes are compared one at a time here, so a long literal is charged for. (Go compares with
          // bytes.Equal, which is much faster for each byte, and does not charge.)
          Dec(Steps, Length(I^.Lit) shr 3);
          if EqualLit(Text, Pos, I^.Lit) then begin
            Inc(Pos, Length(I^.Lit));
            Inc(Pc);
            Continue;
          end;
        end;
      OpCharBack:
        if Pos > Lo then begin
          R := Text[Pos - 1];
          W := 1;
          if R >= RuneSelf then
            R := DecodeLast(Text, Lo, Pos, Hi, Units, W);
          if R = I^.R then begin
            Dec(Pos, W);
            Inc(Pc);
            Continue;
          end;
        end;
      OpSetBack:
        if Pos > Lo then begin
          R := Text[Pos - 1];
          W := 1;
          if R >= RuneSelf then
            R := DecodeLast(Text, Lo, Pos, Hi, Units, W);
          if Prog.Sets[I^.SetIx].Contains(R) then begin
            Dec(Pos, W);
            Inc(Pc);
            Continue;
          end;
        end;
      OpFail:
        ;
      OpBOL:
        if Pos = Lo then begin
          Inc(Pc);
          Continue;
        end;
      OpEOL:
        if Pos = Hi then begin
          Inc(Pc);
          Continue;
        end;
      OpWordBoundary, OpNotWordBoundary: begin
        Before := (Pos > Lo) and IsWordByte(Text[Pos - 1]);
        After := (Pos < Hi) and IsWordByte(Text[Pos]);
        if (Before <> After) = (I^.Op = OpWordBoundary) then begin
          Inc(Pc);
          Continue;
        end;
      end;
      OpLineStart:
        if (Pos = Lo) or LineTerminatorBefore(Text, Lo, Pos) then begin
          Inc(Pc);
          Continue;
        end;
      OpLineEnd:
        if (Pos = Hi) or LineTerminatorAt(Text, Pos, Hi) then begin
          Inc(Pc);
          Continue;
        end;
      OpWordBoundaryFold, OpNotWordBoundaryFold: begin
        Before := False;
        After := False;
        if Pos > Lo then
          Before := IsFoldWordRune(DecodeLastRune(Text, Lo, Pos, W));
        if Pos < Hi then
          After := IsFoldWordRune(DecodeRune(Text, Pos, Hi, W));
        if (Before <> After) = (I^.Op = OpWordBoundaryFold) then begin
          Inc(Pc);
          Continue;
        end;
      end;
      OpJmp: begin
        Pc := I^.X;
        Continue;
      end;
      OpSplit: begin
        // A place from which every way has failed before fails at once. The mark is under whatever comes of this
        // place, a choice or not, so that the place is remembered when it has all failed.
        Failed := False;
        if Memo and (I^.M <> 0) then begin
          Idx := MemoIndex(Prog, St, I^.M, Pos);
          if MemoFailed(St, Idx) then
            Failed := True
          else if St.Sp < MaxMarkDepth then
            Push(St, EMark, 0, Idx, 0);
        end;
        if not Failed then begin
          // A way the next character rules out is not taken, and no choice is kept for it.
          if not QuickAllows(Prog.Quick[I^.Y], Text, Pos, Hi) then begin
            if QuickAllows(Prog.Quick[I^.X], Text, Pos, Hi) then begin
              Pc := I^.X;
              Continue;
            end;
          end else if not QuickAllows(Prog.Quick[I^.X], Text, Pos, Hi) then begin
            Pc := I^.Y;
            Continue;
          end else begin
            Push(St, EChoice, I^.Y, Pos, 0);
            Pc := I^.X;
            Continue;
          end;
        end;
      end;
      OpSaveTmp: begin
        Push(St, ETmp, I^.A, St.Tmp[I^.A], 0);
        St.Tmp[I^.A] := Pos;
        Inc(Pc);
        Continue;
      end;
      OpCapture: begin
        Push(St, ECap, I^.A, St.Caps[2 * I^.A], St.Caps[2 * I^.A + 1]);
        if I^.Flag then begin
          St.Caps[2 * I^.A] := Pos;
          St.Caps[2 * I^.A + 1] := St.Tmp[I^.A];
        end else begin
          St.Caps[2 * I^.A] := St.Tmp[I^.A];
          St.Caps[2 * I^.A + 1] := Pos;
        end;
        Inc(Pc);
        Continue;
      end;
      OpClear: begin
        Dec(Steps, I^.B - I^.A);
        for G := I^.A to I^.B do
          if St.Caps[2 * G] >= 0 then begin
            Push(St, ECap, G, St.Caps[2 * G], St.Caps[2 * G + 1]);
            St.Caps[2 * G] := -1;
            St.Caps[2 * G + 1] := -1;
          end;
        Inc(Pc);
        Continue;
      end;
      OpBackref: begin
        // A group that has not taken part matches the empty string.
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        N := Till - From;
        if (Hi - Pos >= N) and EqualSpans(Text, Pos, From, N) then begin
          Dec(Steps, N shr 3);
          Inc(Pos, N);
          Inc(Pc);
          Continue;
        end;
      end;
      OpBackrefBack: begin
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        N := Till - From;
        if (Pos - Lo >= N) and EqualSpans(Text, Pos - N, From, N) then begin
          Dec(Steps, N shr 3);
          Dec(Pos, N);
          Inc(Pc);
          Continue;
        end;
      end;
      OpBackrefFold, OpBackrefUnits: begin
        // A character at a time: to fold case, or because a group matched by code unit can begin or end between
        // the halves of a character, where its bytes do not say what it holds. (The second half of a character
        // lies beyond the end of a group that ends between the halves, so a code unit is read from the text.)
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        Dec(Steps, (Till - From) shr 3);
        At := Pos;
        Ok := True;
        while (From < Till) and Ok do begin
          if Units then
            Want := UnitAt(Text, Lo, From, Hi, W)
          else
            Want := DecodeRune(Text, From, Till, W);
          Inc(From, W);
          if At >= Hi then begin
            Ok := False;
            Break;
          end;
          Got := Decode(Text, Lo, At, Hi, Units, W);
          Inc(At, W);
          Ok := (Got = Want) or ((I^.Op = OpBackrefFold) and
            (Canonicalize(Got, I^.Flag) = Canonicalize(Want, I^.Flag)));
        end;
        if Ok then begin
          Pos := At;
          Inc(Pc);
          Continue;
        end;
      end;
      OpBackrefFoldBack, OpBackrefUnitsBack: begin
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        Dec(Steps, (Till - From) shr 3);
        At := Pos;
        Ok := True;
        while (From < Till) and Ok do begin
          if Units then
            Want := UnitBefore(Text, Lo, Till, Hi, W)
          else
            Want := DecodeLastRune(Text, From, Till, W);
          Dec(Till, W);
          if At <= Lo then begin
            Ok := False;
            Break;
          end;
          Got := DecodeLast(Text, Lo, At, Hi, Units, W);
          Dec(At, W);
          Ok := (Got = Want) or ((I^.Op = OpBackrefFoldBack) and
            (Canonicalize(Got, I^.Flag) = Canonicalize(Want, I^.Flag)));
        end;
        if Ok then begin
          Pos := At;
          Inc(Pc);
          Continue;
        end;
      end;
      OpLook:
        // What follows a lookaround starts where the lookaround does, and so does the body of a lookahead.
        if QuickAllows(Prog.Quick[Pc], Text, Pos, Hi) then begin
          Kind := ELook;
          if I^.Flag then
            Kind := ELookNeg;
          Push(St, Kind, I^.X, Pos, 0);
          Inc(Pc);
          Continue;
        end;
      OpLookOK: begin
        // The body matched. A lookaround is atomic, so the choices made inside it are dropped. The undo entries
        // stay, because the groups it set remain set until something below this point is retried.
        F := St.Sp - 1;
        while St.Stack[F].Kind <> ELook do
          Dec(F);
        Pos := St.Stack[F].Pos;
        Keep := F;
        for K := F + 1 to St.Sp - 1 do
          if St.Stack[K].Kind >= ETmp then begin
            St.Stack[Keep] := St.Stack[K];
            Inc(Keep);
          end;
        St.Sp := Keep;
        Inc(Pc);
        Continue;
      end;
      OpLookFail:
        // The body of a negative lookaround matched, so the lookaround fails and everything it did is undone.
        while True do begin
          Dec(St.Sp);
          Undone := St.Stack[St.Sp];
          if Undone.Kind = ELookNeg then
            Break;
          Undo(St, Undone);
        end;
      OpRep: begin
        At := Pos;
        N := 0;
        Floor := Pos;
        if I^.SetIx >= 0 then begin
          CharSet := @Prog.Sets[I^.SetIx];
          while (N <> I^.Max) and (At < Hi) do begin
            C := Text[At];
            if C < RuneSelf then begin
              if ((CharSet^.Ascii[C shr 6] shr (C and 63)) and 1) = 0 then
                Break;
              Inc(At);
            end else begin
              if not CharSet^.Contains(Decode(Text, Lo, At, Hi, Units, W)) then
                Break;
              Inc(At, W);
            end;
            Inc(N);
            if N = I^.Min then
              Floor := At;
          end;
        end else begin
          while (N <> I^.Max) and (At < Hi) do begin
            R := Text[At];
            W := 1;
            if R >= RuneSelf then
              R := Decode(Text, Lo, At, Hi, Units, W);
            if R <> I^.R then
              Break;
            Inc(At, W);
            Inc(N);
            if N = I^.Min then
              Floor := At;
          end;
        end;
        Dec(Steps, (At - Pos) shr 3);
        if N >= I^.Min then begin
          Failed := False;
          if Memo and (I^.M <> 0) then begin
            Idx := MemoIndex(Prog, St, I^.M, At);
            if MemoFailed(St, Idx) then
              // What follows the loop has failed from here before. The loop gives a character back.
              Failed := True
            else if (At = Floor) and (St.Sp < MaxMarkDepth) then
              Push(St, EMark, 0, Idx, 0);
          end;
          if At > Floor then
            Push(St, ERep, Pc + 1, At, Floor);
          if not Failed then begin
            Pos := At;
            Inc(Pc);
            Continue;
          end;
        end;
      end;
      OpRepLazy: begin
        At := Pos;
        N := 0;
        while N < I^.Min do begin
          W := One(Prog, I^, Text, Lo, At, Hi);
          if W = 0 then
            Break;
          Inc(At, W);
          Inc(N);
        end;
        Dec(Steps, N shr 3);
        if N = I^.Min then begin
          Failed := False;
          if Memo and (I^.M <> 0) then begin
            Idx := MemoIndex(Prog, St, I^.M, At);
            if MemoFailed(St, Idx) then
              // What follows the loop has failed from here before. The loop takes one more character.
              Failed := True
            else if (N = I^.Max) and (St.Sp < MaxMarkDepth) then
              Push(St, EMark, 0, Idx, 0);
          end;
          if N <> I^.Max then
            Push(St, ERepLazy, Pc, At, N);
          if not Failed then begin
            Pos := At;
            Inc(Pc);
            Continue;
          end;
        end;
      end;
      OpLoopInit: begin
        Push(St, ECounter, I^.A, St.Counts[2 * I^.A], St.Counts[2 * I^.A + 1]);
        St.Counts[2 * I^.A] := 0;
        St.Counts[2 * I^.A + 1] := -1;
        Inc(Pc);
        Continue;
      end;
      OpLoop: begin
        N := St.Counts[2 * I^.A];
        // Once the minimum is met, ECMA-262 rejects an iteration that consumed nothing. That is what stops a
        // loop over a body that can be empty. The loop also gives up when fewer bytes are left than any way from
        // here to a match consumes: what follows the loop, and the iterations it has still to make.
        if (Hi - Pos >= I^.Need) and not ((N < I^.Min) and (I^.Iter <> 0)
          and (Int64(Hi - Pos) - I^.Need < Int64(I^.Min - N) * I^.Iter))
          and not ((N > I^.Min) and (Pos = St.Counts[2 * I^.A + 1])) then begin
          if N < I^.Min then begin
            Inc(Pc);
            Continue;
          end;
          if (I^.Max >= 0) and (N >= I^.Max) then begin
            Pc := I^.X;
            Continue;
          end;
          // The loop may be left or entered from here. A place from which both have failed before fails at once.
          Failed := False;
          if Memo and (I^.M <> 0) then begin
            Idx := MemoIndex(Prog, St, I^.M, Pos);
            if MemoFailed(St, Idx) then
              Failed := True
            else if St.Sp < MaxMarkDepth then
              Push(St, EMark, 0, Idx, 0);
          end;
          if not Failed then begin
            if not QuickAllows(Prog.Quick[I^.X], Text, Pos, Hi) then
              // The next character rules out leaving the loop, so there is no choice.
              Inc(Pc)
            else if not QuickAllows(Prog.Quick[Pc + 1], Text, Pos, Hi) then
              Pc := I^.X
            else if I^.Flag then begin
              Push(St, EChoice, Pc + 1, Pos, 0);
              Pc := I^.X;
            end else begin
              Push(St, EChoice, I^.X, Pos, 0);
              Inc(Pc);
            end;
            Continue;
          end;
        end;
      end;
      OpLoopIter: begin
        Push(St, ECounter, I^.A, St.Counts[2 * I^.A], St.Counts[2 * I^.A + 1]);
        Inc(St.Counts[2 * I^.A]);
        St.Counts[2 * I^.A + 1] := Pos;
        Inc(Pc);
        Continue;
      end;
      OpMatch: begin
        St.Steps := Steps;
        Exit(True);
      end;
    end;
    // The instruction failed. Take up the most recent choice, undoing what was recorded since.
    while True do begin
      if St.Sp = 0 then begin
        St.Steps := Steps;
        Exit(False);
      end;
      E := @St.Stack[St.Sp - 1];
      case E^.Kind of
        EChoice: begin
          Pc := E^.Pc;
          Pos := E^.Pos;
          Dec(St.Sp);
          Break;
        end;
        ERep: begin
          // What followed the loop from the position it stood at has failed.
          Rep := @Prog.Insts[E^.Pc - 1];
          Marked := Memo and (Rep^.M <> 0);
          if Marked then
            MemoFail(St, MemoIndex(Prog, St, Rep^.M, E^.Pos));
          // Give back the last character taken. The characters between A and Pos were decoded going forward,
          // and decoding the last of them going backward finds the same boundary.
          W := 1;
          if Text[E^.Pos - 1] >= RuneSelf then begin
            if Units then
              UnitBefore(Text, Lo, E^.Pos, Hi, W)
            else
              DecodeLastRune(Text, E^.A, E^.Pos, W);
          end;
          Dec(E^.Pos, W);
          Pc := E^.Pc;
          Pos := E^.Pos;
          Last := E^.Pos <= E^.A;
          // A position from which what follows the loop cannot go on is passed over, and so is one from which it
          // has failed before.
          Pass := not QuickAllows(Prog.Quick[Pc], Text, Pos, Hi);
          if Marked and not Pass then begin
            Idx := MemoIndex(Prog, St, Rep^.M, Pos);
            if MemoFailed(St, Idx) then
              Pass := True
            else if Last then begin
              // The loop has nothing more to give back. The entry stays to note this last way.
              E^.Kind := EMark;
              E^.Pc := 0;
              E^.Pos := Idx;
              E^.A := 0;
              Break;
            end;
          end;
          if Last then
            Dec(St.Sp);
          if Pass then begin
            Dec(Steps);
            Continue;
          end;
          Break;
        end;
        ERepLazy: begin
          // What followed the loop from the position it stood at has failed.
          Rep := @Prog.Insts[E^.Pc];
          Marked := Memo and (Rep^.M <> 0);
          if Marked then
            MemoFail(St, MemoIndex(Prog, St, Rep^.M, E^.Pos));
          W := One(Prog, Rep^, Text, Lo, E^.Pos, Hi);
          if W = 0 then begin
            Dec(St.Sp);
            Continue;
          end;
          Inc(E^.Pos, W);
          Inc(E^.A);
          Pc := E^.Pc + 1;
          Pos := E^.Pos;
          Last := E^.A = Rep^.Max;
          if Marked then begin
            Idx := MemoIndex(Prog, St, Rep^.M, Pos);
            if MemoFailed(St, Idx) then begin
              if Last then
                Dec(St.Sp);
              Dec(Steps);
              Continue;
            end;
            if Last then begin
              E^.Kind := EMark;
              E^.Pc := 0;
              E^.Pos := Idx;
              E^.A := 0;
              Break;
            end;
          end;
          if Last then
            Dec(St.Sp);
          Break;
        end;
        EMark: begin
          // Every way from a memo point has failed.
          MemoFail(St, E^.Pos);
          Dec(St.Sp);
        end;
        ELook:
          // The body of a positive lookaround has no way left to match.
          Dec(St.Sp);
        ELookNeg: begin
          // The body of a negative lookaround has no way to match, so the lookaround holds.
          Pc := E^.Pc;
          Pos := E^.Pos;
          Dec(St.Sp);
          Break;
        end;
      else
        Undo(St, E^);
        Dec(St.Sp);
      end;
    end;
  end;
end;

function Search(const Prog: TProgram; var St: TState; const Text: TBytes; Lo, Hi: Int32): Boolean;
var
  At, R, W: Int32;
begin
  if Hi - Lo < Prog.Need then
    Exit(False);
  if Prog.Anchored then
    Exit(Run(Prog, St, Text, Lo, Hi, Lo));
  if Length(Prog.Prefix) > 0 then begin
    At := Lo;
    while True do begin
      At := IndexOf(Text, At, Hi, Prog.Prefix);
      if (At < 0) or (Hi - At < Prog.Need) then
        Exit(False);
      if Run(Prog, St, Text, Lo, Hi, At) then
        Exit(True);
      if St.Stop <> StopNone then
        Exit(False);
      Inc(At);
    end;
  end;
  At := Lo;
  while True do begin
    if Hi - At < Prog.Need then
      Exit(False);
    if At = Hi then
      Exit((Prog.First < 0) and Run(Prog, St, Text, Lo, Hi, At));
    R := Text[At];
    W := 1;
    if R >= RuneSelf then
      R := Decode(Text, Lo, At, Hi, Prog.Units, W);
    if ((Prog.First < 0) or Prog.Sets[Prog.First].Contains(R)) and Run(Prog, St, Text, Lo, Hi, At) then
      Exit(True);
    if St.Stop <> StopNone then
      Exit(False);
    Inc(At, W);
  end;
end;

function ProgramMatch(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32): Boolean;
var
  Stop: UInt8;
begin
  Result := ProgramMatchLimit(Prog, Text, Lo, Hi, 0, Stop);
end;

function ProgramMatchLimit(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32; LimitMs: Int64;
  out Stop: UInt8): Boolean;
var
  St: PState;
  K: Int32;
begin
  St := @ThreadState;
  // The memory of the thread grows to what this program needs. The groups start out not having taken part. The
  // entry positions and the counters need no start value: a match writes each before it uses it, and until then
  // only saves and restores what it finds there.
  if Length(St^.Caps) < 2 * Prog.Groups then begin
    SetLength(St^.Caps, 2 * Prog.Groups);
    SetLength(St^.Tmp, Prog.Groups);
  end;
  if Length(St^.Counts) < 2 * Prog.Loops then
    SetLength(St^.Counts, 2 * Prog.Loops);
  for K := 0 to 2 * Prog.Groups - 1 do
    St^.Caps[K] := -1;
  St^.LimitMs := LimitMs;
  St^.Timed := False;
  St^.Steps := CheckEvery;
  St^.Stop := StopNone;
  St^.MemoOn := False;
  St^.MemoOff := Length(Prog.Points) = 0;
  St^.Checks := 0;
  St^.Width := Hi - Lo + 1;
  St^.MemoLo := Lo;
  St^.Points := Length(Prog.Points);
  if MemoFromTheStart and not St^.MemoOff then
    Remember(St^);
  Result := Search(Prog, St^, Text, Lo, Hi);
  Stop := St^.Stop;
  if Stop <> StopNone then
    Result := False;
  if Length(St^.Stack) > MaxRetainedStack then
    St^.Stack := nil;
  if Length(St^.Memo) > MaxRetainedMemo then
    St^.Memo := nil;
end;

initialization

finalization
  ReleaseThreadState;
end.