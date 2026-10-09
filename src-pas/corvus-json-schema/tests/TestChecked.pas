program TestChecked;
{$I corvus.inc}

// The tests of the array reads that make their own bounds check (Corvus.JsonSchema.Checked, and the functions
// written the same way next to the array types of the other units). There is no Go test to port: Go checks every
// index itself. Here the compiler's check is off inside those functions, so each one is held to what the compiler's
// check does: an index below the first element, at the length, and far beyond it raises ERangeError, on an empty
// array and on one with elements, and an index within the array reads the element.
//
// The program prints each failure and a final count, and exits with a status other than zero when anything failed.

uses
  SysUtils,
  Corvus.JsonSchema.Checked,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Compiler,
  Corvus.JsonSchema.Plan;

type
  // The functions under test. A Check function is called for a run of one element at the index.
  TCall = (CallByteAt, CallWordAt, CallWordPtrAt, CallLoad64, CallLoad32, CallTextByteAt, CallCheckRun,
    CallCheckWords, CallCheckText, CallUInt32At, CallNameKeyAt, CallTextAt, CallUInt16At, CallSequenceItemAt,
    CallPatternAt, CallChildAt, CallOpAt, CallPatternChildAt, CallPlanAt, CallStringOpAt, CallEntryAt,
    CallFusedAppAt, CallFusedConditionAt, CallFusedContributorAt, CallFusedPatternAt, CallValueTestAt);

  // One array of each type the functions read. Every array of a set has the same number of elements.
  TArrays = record
    Count: Int32;
    Bytes: TBytes;
    Words: TUInt64Array;
    Text: UTF8String;
    UInt32s: TUInt32Array;
    NameKeys: TNameKeyArray;
    Texts: TUTF8StringArray;
    UInt16s: TUInt16Array;
    SequenceItems: TSequenceItemArray;
    Patterns: TPatternArray;
    Children: TChildArray;
    Ops: TOpArray;
    PatternChildren: TPatternChildArray;
    Plans: TPlanArray;
    StringOps: TStringOpArray;
    Entries: TFusedEntryArray;
    Apps: TFusedAppArray;
    Conditions: TFusedConditionArray;
    Contributors: TFusedContributorArray;
    FusedPatterns: TFusedPatternArray;
    ValueTests: TValueTestArray;
  end;

const
  CallNames: array[TCall] of UTF8String = ('ByteAt', 'WordAt', 'WordPtrAt', 'Load64', 'Load32', 'TextByteAt',
    'CheckRun', 'CheckWords', 'CheckText', 'UInt32At', 'NameKeyAt', 'TextAt', 'UInt16At', 'SequenceItemAt',
    'PatternAt', 'ChildAt', 'OpAt', 'PatternChildAt', 'PlanAt', 'StringOpAt', 'EntryAt', 'FusedAppAt',
    'FusedConditionAt', 'FusedContributorAt', 'FusedPatternAt', 'ValueTestAt');

  // The number of elements of the arrays that are not empty.
  Elements = 12;

var
  Checks: Int32 = 0;
  Failures: Int32 = 0;
  // What the reads gave, so that no call is without effect.
  Sink: UInt64 = 0;

procedure Fail(const Message: UTF8String);
begin
  Inc(Failures);
  WriteLn('FAIL: ', Message);
end;

// NewArrays is a set of arrays of Count elements each. Element I of an array of numbers is I + 1, and the Id,
// Pattern, Min, Length, Contributor or Condition of element I of an array of records is I + 1.
procedure NewArrays(out A: TArrays; Count: Int32);
var
  I: Int32;
begin
  A := Default(TArrays);
  A.Count := Count;
  SetLength(A.Bytes, Count);
  SetLength(A.Words, Count);
  SetLength(A.Text, Count);
  SetLength(A.UInt32s, Count);
  SetLength(A.NameKeys, Count);
  SetLength(A.Texts, Count);
  SetLength(A.UInt16s, Count);
  SetLength(A.SequenceItems, Count);
  SetLength(A.Patterns, Count);
  SetLength(A.Children, Count);
  SetLength(A.Ops, Count);
  SetLength(A.PatternChildren, Count);
  SetLength(A.Plans, Count);
  SetLength(A.StringOps, Count);
  SetLength(A.Entries, Count);
  SetLength(A.Apps, Count);
  SetLength(A.Conditions, Count);
  SetLength(A.Contributors, Count);
  SetLength(A.FusedPatterns, Count);
  SetLength(A.ValueTests, Count);
  for I := 0 to Count - 1 do begin
    A.Bytes[I] := Byte(I + 1);
    A.Words[I] := UInt64(I + 1);
    A.Text[I + 1] := AnsiChar(I + 1);
    A.UInt32s[I] := UInt32(I + 1);
    A.NameKeys[I].Length := I + 1;
    A.Texts[I] := UTF8String(IntToStr(I + 1));
    A.UInt16s[I] := UInt16(I + 1);
    A.SequenceItems[I].Min := UInt32(I + 1);
    A.Patterns[I].Min := UInt32(I + 1);
    A.Children[I].Id := I + 1;
    A.Ops[I].Node := I + 1;
    A.PatternChildren[I].Pattern := I + 1;
    A.Plans[I].Self.Id := I + 1;
    A.StringOps[I].Pattern := I + 1;
    A.Entries[I].Merged.Keyed := UInt64(I + 1);
    A.Apps[I].Contributor := UInt16(I + 1);
    A.Conditions[I].Gate.Condition := UInt16(I + 1);
    A.Contributors[I].ThenMask := UInt64(I + 1);
    A.FusedPatterns[I].Pattern := I + 1;
    A.ValueTests[I].Pattern := I + 1;
  end;
end;

// Call makes one call with an index and gives what the element holds (see NewArrays), or 0 for a function that
// reads nothing. A string's first byte has the index 1, so the functions for a string are given the index + 1: every
// function is then called with indexes from 0.
function Call(C: TCall; const A: TArrays; I: Int32): UInt64;
begin
  Result := 0;
  case C of
    CallByteAt: Result := ByteAt(A.Bytes, I);
    CallWordAt: Result := WordAt(A.Words, I);
    CallWordPtrAt: Result := WordPtrAt(A.Words, I)^;
    CallLoad64: Result := Load64(A.Bytes, I);
    CallLoad32: Result := Load32(A.Bytes, I);
    CallTextByteAt: Result := TextByteAt(A.Text, Int32(Int64(I) + 1));
    CallCheckRun: CheckRun(A.Bytes, I, 1);
    CallCheckWords: CheckWords(A.Words, I, 1);
    CallCheckText: CheckText(A.Text, Int32(Int64(I) + 1), 1);
    CallUInt32At: Result := UInt32At(A.UInt32s, I);
    CallNameKeyAt: Result := UInt64(NameKeyAt(A.NameKeys, I)^.Length);
    CallTextAt: Result := UInt64(StrToInt(String(TextAt(A.Texts, I)^)));
    CallUInt16At: Result := UInt16At(A.UInt16s, I);
    CallSequenceItemAt: Result := SequenceItemAt(A.SequenceItems, I)^.Min;
    CallPatternAt: Result := PatternAt(A.Patterns, I)^.Min;
    CallChildAt: Result := UInt64(ChildAt(A.Children, I)^.Id);
    CallOpAt: Result := UInt64(OpAt(A.Ops, I)^.Node);
    CallPatternChildAt: Result := UInt64(PatternChildAt(A.PatternChildren, I)^.Pattern);
    CallPlanAt: Result := UInt64(PlanAt(A.Plans, I)^.Self.Id);
    CallStringOpAt: Result := UInt64(StringOpAt(A.StringOps, I)^.Pattern);
    CallEntryAt: Result := EntryAt(A.Entries, I)^.Merged.Keyed;
    CallFusedAppAt: Result := FusedAppAt(A.Apps, I)^.Contributor;
    CallFusedConditionAt: Result := FusedConditionAt(A.Conditions, I)^.Gate.Condition;
    CallFusedContributorAt: Result := FusedContributorAt(A.Contributors, I)^.ThenMask;
    CallFusedPatternAt: Result := UInt64(FusedPatternAt(A.FusedPatterns, I)^.Pattern);
    CallValueTestAt: Result := UInt64(ValueTestAt(A.ValueTests, I)^.Pattern);
  end;
end;

// Width is the number of elements a call reads from its index (or checks).
function Width(C: TCall): Int32;
begin
  case C of
    CallLoad64: Result := 8;
    CallLoad32: Result := 4;
  else
    Result := 1;
  end;
end;

// Refused makes a call that must raise ERangeError.
procedure Refused(C: TCall; const A: TArrays; I: Int32);
var
  Name: UTF8String;
begin
  Inc(Checks);
  Name := CallNames[C] + '(' + UTF8String(IntToStr(A.Count)) + ' elements, ' + UTF8String(IntToStr(I)) + ')';
  try
    Sink := Sink + Call(C, A, I);
    Fail(Name + ': no exception');
  except
    on E: ERangeError do ;
    on E: Exception do
      Fail(Name + ': ' + UTF8String(E.ClassName) + ' instead of ERangeError');
  end;
end;

// Allowed makes a call that must read the element (or check the run) without an exception.
procedure Allowed(C: TCall; const A: TArrays; I: Int32);
var
  Name: UTF8String;
  Got, Want: UInt64;
  K: Int32;
begin
  Inc(Checks);
  Name := CallNames[C] + '(' + UTF8String(IntToStr(A.Count)) + ' elements, ' + UTF8String(IntToStr(I)) + ')';
  Want := 0;
  case C of
    CallCheckRun, CallCheckWords, CallCheckText: ;
    CallLoad64, CallLoad32:
      // The bytes from the index, the first the lowest.
      for K := Width(C) - 1 downto 0 do
        Want := (Want shl 8) or UInt64(I + K + 1);
  else
    Want := UInt64(I + 1);
  end;
  try
    Got := Call(C, A, I);
    Sink := Sink + Got;
    if Got <> Want then
      Fail(Name + ': got ' + UTF8String(IntToStr(Got)) + ', want ' + UTF8String(IntToStr(Want)));
  except
    on E: Exception do
      Fail(Name + ': ' + UTF8String(E.ClassName));
  end;
end;

// TestIndexes calls every function with the indexes that are outside an array, and with those at its two ends.
procedure TestIndexes(const A: TArrays);
var
  C: TCall;
  Last, I: Int32;
begin
  for C := Low(TCall) to High(TCall) do begin
    // The last index a call may be given: it reads Width elements from it.
    Last := A.Count - Width(C);
    // Below the first element.
    Refused(C, A, -1);
    Refused(C, A, -2);
    Refused(C, A, -1000000);
    Refused(C, A, Low(Int32));
    Refused(C, A, Low(Int32) + 1);
    // At the length and beyond it (for a read of several bytes, from the first index whose read would pass the end).
    for I := Last + 1 to A.Count + 1 do
      if I >= 0 then
        Refused(C, A, I);
    Refused(C, A, A.Count + 1000000);
    Refused(C, A, High(Int32) - 8);
    Refused(C, A, High(Int32) - 1);
    Refused(C, A, High(Int32));
    // Within the array.
    if Last >= 0 then begin
      Allowed(C, A, 0);
      Allowed(C, A, Last div 2);
      Allowed(C, A, Last);
    end;
  end;
end;

// RunRefused and RunAllowed call the three Check functions with a run, of bytes, of words and of a string's bytes
// (whose first has the index Start + 1).
procedure RunRefused(const A: TArrays; Start, Len: Int32);
var
  Name: UTF8String;
  Which: Int32;
begin
  for Which := 0 to 2 do begin
    Inc(Checks);
    Name := 'run ' + UTF8String(IntToStr(Which)) + ' (' + UTF8String(IntToStr(A.Count)) + ' elements, '
      + UTF8String(IntToStr(Start)) + ', ' + UTF8String(IntToStr(Len)) + ')';
    try
      case Which of
        0: CheckRun(A.Bytes, Start, Len);
        1: CheckWords(A.Words, Start, Len);
      else
        CheckText(A.Text, Int32(Int64(Start) + 1), Len);
      end;
      Fail(Name + ': no exception');
    except
      on E: ERangeError do ;
      on E: Exception do
        Fail(Name + ': ' + UTF8String(E.ClassName) + ' instead of ERangeError');
    end;
  end;
end;

procedure RunAllowed(const A: TArrays; Start, Len: Int32);
var
  Name: UTF8String;
  Which, K: Int32;
begin
  for Which := 0 to 2 do begin
    Inc(Checks);
    Name := 'run ' + UTF8String(IntToStr(Which)) + ' (' + UTF8String(IntToStr(A.Count)) + ' elements, '
      + UTF8String(IntToStr(Start)) + ', ' + UTF8String(IntToStr(Len)) + ')';
    try
      // The reads that the check allows give the elements.
      for K := Start to Start + Len - 1 do
        case Which of
          0: begin
            CheckRun(A.Bytes, Start, Len);
            if RunByteAt(A.Bytes, K) <> Byte(K + 1) then
              Fail(Name + ': RunByteAt read another byte');
          end;
          1: begin
            CheckWords(A.Words, Start, Len);
            if RunWordAt(A.Words, K) <> UInt64(K + 1) then
              Fail(Name + ': RunWordAt read another word');
          end;
        else
          CheckText(A.Text, Start + 1, Len);
          if RunTextByteAt(A.Text, K + 1) <> Byte(K + 1) then
            Fail(Name + ': RunTextByteAt read another byte');
        end;
      // A run of no elements is checked as well.
      case Which of
        0: CheckRun(A.Bytes, Start, Len);
        1: CheckWords(A.Words, Start, Len);
      else
        CheckText(A.Text, Start + 1, Len);
      end;
    except
      on E: Exception do
        Fail(Name + ': ' + UTF8String(E.ClassName));
    end;
  end;
end;

// TestRuns checks runs of more than one element, of none, and of lengths that are negative or pass the end.
procedure TestRuns(const A: TArrays);
var
  N: Int32;
begin
  N := A.Count;
  RunAllowed(A, 0, 0);
  RunAllowed(A, 0, N);
  RunAllowed(A, N, 0);
  if N > 1 then begin
    RunAllowed(A, 1, N - 1);
    RunAllowed(A, N - 1, 1);
    RunAllowed(A, N div 2, 0);
  end;
  RunRefused(A, 0, N + 1);
  RunRefused(A, 1, N);
  RunRefused(A, N, 1);
  RunRefused(A, N + 1, 0);
  RunRefused(A, -1, 0);
  RunRefused(A, -1, 1);
  RunRefused(A, -1, N + 1);
  RunRefused(A, 0, -1);
  RunRefused(A, N, -1);
  RunRefused(A, 0, Low(Int32));
  RunRefused(A, Low(Int32), 0);
  RunRefused(A, Low(Int32), Low(Int32));
  RunRefused(A, 0, High(Int32));
  RunRefused(A, High(Int32) - 1, 0);
  RunRefused(A, High(Int32) - 1, 1);
  // A start and a length whose sum does not fit in 32 bits.
  RunRefused(A, High(Int32) - 1, High(Int32));
  RunRefused(A, 1, High(Int32));
  RunRefused(A, N, High(Int32));
end;

// TestWrite writes through the pointer WordPtrAt gives, which is to the element and to nothing else.
procedure TestWrite(var A: TArrays);
var
  I: Int32;
begin
  for I := 0 to A.Count - 1 do
    WordPtrAt(A.Words, I)^ := UInt64(1000 + I);
  for I := 0 to A.Count - 1 do begin
    Inc(Checks);
    if A.Words[I] <> UInt64(1000 + I) then
      Fail('WordPtrAt: element ' + UTF8String(IntToStr(I)) + ' was not written');
  end;
  for I := 0 to A.Count - 1 do
    A.Words[I] := UInt64(I + 1);
end;

// TestRangeFail calls the function every check ends in.
procedure TestRangeFail;
begin
  Inc(Checks);
  try
    RangeFail;
    Fail('RangeFail: no exception');
  except
    on E: ERangeError do ;
    on E: Exception do
      Fail('RangeFail: ' + UTF8String(E.ClassName) + ' instead of ERangeError');
  end;
end;

var
  Empty, One, Full: TArrays;
begin
  NewArrays(Empty, 0);
  NewArrays(One, 1);
  NewArrays(Full, Elements);
  TestRangeFail;
  TestIndexes(Empty);
  TestIndexes(One);
  TestIndexes(Full);
  TestRuns(Empty);
  TestRuns(One);
  TestRuns(Full);
  TestWrite(Full);
  // An array that was never given a length is empty as well.
  Empty := Default(TArrays);
  TestIndexes(Empty);
  TestRuns(Empty);
  WriteLn('TestChecked: ', Checks, ' checks, ', Checks - Failures, ' passed, ', Failures, ' failed');
  if (Failures <> 0) or (Sink = 0) then Halt(1);
end.
