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
  loses that memory once, and nothing else goes wrong. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.EcmaRegex.Compile;

{ ProgramMatch reports whether the program matches anywhere in the bytes Lo to Hi - 1 of the text. }
function ProgramMatch(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32): Boolean;

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

{ IndexOf returns the first position at or after At where the bytes of Lit are in the text, or -1. }
function IndexOf(const Text: TBytes; At, Hi: Int32; const Lit: TBytes): Int32;
var
  Last: Int32;
  First: UInt8;
begin
  Last := Hi - Length(Lit);
  First := Lit[0];
  while At <= Last do begin
    if (Text[At] = First) and EqualLit(Text, At, Lit) then
      Exit(At);
    Inc(At);
  end;
  Result := -1;
end;

{ One returns the width of the character at the position At when the instruction's character or class matches it,
  and 0 otherwise. }
function One(const Prog: TProgram; const Inst: TInst; const Text: TBytes; At, Hi: Int32): Int32;
var
  R, W: Int32;
begin
  if At >= Hi then
    Exit(0);
  R := Text[At];
  W := 1;
  if R >= RuneSelf then
    R := DecodeRune(Text, At, Hi, W);
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
  counter is back to the value it had on entry. }
function Run(const Prog: TProgram; var St: TState; const Text: TBytes; Lo, Hi, Start: Int32): Boolean;
var
  Pc, Pos, R, W, At, N, Floor, From, Till, G, F, Keep, K, Want, Got: Int32;
  C: UInt8;
  I, Rep: PInst;
  CharSet: PCharSet;
  E: PEntry;
  Undone: TEntry;
  Before, After, Ok: Boolean;
  Kind: UInt8;
begin
  St.Sp := 0;
  Pc := 0;
  Pos := Start;
  while True do begin
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
          end else if DecodeRune(Text, Pos, Hi, W) = I^.R then begin
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
          end else if CharSet^.Contains(DecodeRune(Text, Pos, Hi, W)) then begin
            Inc(Pos, W);
            Inc(Pc);
            Continue;
          end;
        end;
      OpLit:
        if (Hi - Pos >= Length(I^.Lit)) and EqualLit(Text, Pos, I^.Lit) then begin
          Inc(Pos, Length(I^.Lit));
          Inc(Pc);
          Continue;
        end;
      OpCharBack:
        if Pos > Lo then begin
          R := Text[Pos - 1];
          W := 1;
          if R >= RuneSelf then
            R := DecodeLastRune(Text, Lo, Pos, W);
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
            R := DecodeLastRune(Text, Lo, Pos, W);
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
          Dec(Pos, N);
          Inc(Pc);
          Continue;
        end;
      end;
      OpBackrefFold: begin
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        At := Pos;
        Ok := True;
        while (From < Till) and Ok do begin
          Want := DecodeRune(Text, From, Till, W);
          Inc(From, W);
          if At >= Hi then begin
            Ok := False;
            Break;
          end;
          Got := DecodeRune(Text, At, Hi, W);
          Inc(At, W);
          Ok := (Got = Want) or (Canonicalize(Got, I^.Flag) = Canonicalize(Want, I^.Flag));
        end;
        if Ok then begin
          Pos := At;
          Inc(Pc);
          Continue;
        end;
      end;
      OpBackrefFoldBack: begin
        From := St.Caps[2 * I^.A];
        Till := St.Caps[2 * I^.A + 1];
        if From < 0 then begin
          Inc(Pc);
          Continue;
        end;
        At := Pos;
        Ok := True;
        while (From < Till) and Ok do begin
          Want := DecodeLastRune(Text, From, Till, W);
          Dec(Till, W);
          if At <= Lo then begin
            Ok := False;
            Break;
          end;
          Got := DecodeLastRune(Text, Lo, At, W);
          Dec(At, W);
          Ok := (Got = Want) or (Canonicalize(Got, I^.Flag) = Canonicalize(Want, I^.Flag));
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
              if not CharSet^.Contains(DecodeRune(Text, At, Hi, W)) then
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
              R := DecodeRune(Text, At, Hi, W);
            if R <> I^.R then
              Break;
            Inc(At, W);
            Inc(N);
            if N = I^.Min then
              Floor := At;
          end;
        end;
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
          W := One(Prog, I^, Text, At, Hi);
          if W = 0 then
            Break;
          Inc(At, W);
          Inc(N);
        end;
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
        // loop over a body that can be empty.
        if not ((N > I^.Min) and (Pos = St.Counts[2 * I^.A + 1])) then begin
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
      OpMatch:
        Exit(True);
    end;
    // The instruction failed. Take up the most recent choice, undoing what was recorded since.
    while True do begin
      if St.Sp = 0 then
        Exit(False);
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
          if Text[E^.Pos - 1] >= RuneSelf then
            DecodeLastRune(Text, E^.A, E^.Pos, W);
          Dec(E^.Pos, W);
          Pc := E^.Pc;
          Pos := E^.Pos;
          if E^.Pos <= E^.A then
            Dec(St.Sp);
          Break;
        end;
        ERepLazy: begin
          Rep := @Prog.Insts[E^.Pc];
          W := One(Prog, Rep^, Text, E^.Pos, Hi);
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
  if Prog.Anchored then
    Exit(Run(Prog, St, Text, Lo, Hi, Lo));
  if Length(Prog.Prefix) > 0 then begin
    At := Lo;
    while True do begin
      At := IndexOf(Text, At, Hi, Prog.Prefix);
      if At < 0 then
        Exit(False);
      if Run(Prog, St, Text, Lo, Hi, At) then
        Exit(True);
      Inc(At);
    end;
  end;
  At := Lo;
  while True do begin
    if At = Hi then
      Exit((Prog.First < 0) and Run(Prog, St, Text, Lo, Hi, At));
    R := Text[At];
    W := 1;
    if R >= RuneSelf then
      R := DecodeRune(Text, At, Hi, W);
    if ((Prog.First < 0) or Prog.Sets[Prog.First].Contains(R)) and Run(Prog, St, Text, Lo, Hi, At) then
      Exit(True);
    Inc(At, W);
  end;
end;

function ProgramMatch(const Prog: TProgram; const Text: TBytes; Lo, Hi: Int32): Boolean;
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
  Result := Search(Prog, St^, Text, Lo, Hi);
  if Length(St^.Stack) > MaxRetainedStack then
    St^.Stack := nil;
end;

initialization

finalization
  ReleaseThreadState;
end.