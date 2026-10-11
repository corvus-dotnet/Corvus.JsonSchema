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
  // ETmp restores the entry position of group Pc to Pos.
  ETmp = 5;
  // ECap restores the start and end of group Pc to Pos and A.
  ECap = 6;
  // ECounter restores the count and the iteration start of loop Pc to Pos and A.
  ECounter = 7;

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
  St^.Sp := 0;
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
  Before, After, Ok, Units: Boolean;
  Kind: UInt8;
begin
  // Units says the text is matched by UTF-16 code unit (see Corvus.JsonSchema.EcmaRegex.Utf8).
  Units := Prog.Units;
  St.Sp := 0;
  Pc := 0;
  Pos := Start;
  Steps := St.Steps;
  while True do begin
    Dec(Steps);
    if Steps < 0 then begin
      if not Check(St) then begin
        St.Steps := 0;
        Exit(False);
      end;
      Steps := CheckEvery;
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
        Push(St, EChoice, I^.Y, Pos, 0);
        Pc := I^.X;
        Continue;
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
      OpLook: begin
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
          if At > Floor then
            Push(St, ERep, Pc + 1, At, Floor);
          Pos := At;
          Inc(Pc);
          Continue;
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
          if N <> I^.Max then
            Push(St, ERepLazy, Pc, At, N);
          Pos := At;
          Inc(Pc);
          Continue;
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
          if N < I^.Min then
            Inc(Pc)
          else if (I^.Max >= 0) and (N >= I^.Max) then
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
          if E^.Pos <= E^.A then
            Dec(St.Sp);
          Break;
        end;
        ERepLazy: begin
          Rep := @Prog.Insts[E^.Pc];
          W := One(Prog, Rep^, Text, Lo, E^.Pos, Hi);
          if W = 0 then begin
            Dec(St.Sp);
            Continue;
          end;
          Inc(E^.Pos, W);
          Inc(E^.A);
          Pc := E^.Pc + 1;
          Pos := E^.Pos;
          if E^.A = Rep^.Max then
            Dec(St.Sp);
          Break;
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
  Result := Search(Prog, St^, Text, Lo, Hi);
  Stop := St^.Stop;
  if Stop <> StopNone then
    Result := False;
  if Length(St^.Stack) > MaxRetainedStack then
    St^.Stack := nil;
end;

initialization

finalization
  ReleaseThreadState;
end.