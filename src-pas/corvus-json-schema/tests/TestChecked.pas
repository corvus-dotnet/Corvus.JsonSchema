program TestChecked;
{$I corvus.inc}

// The tests of the array reads that make their own bounds check (Corvus.JsonSchema.Checked, and the functions
// written the same way next to the array types of the other units). There is no Go test to port: Go checks every
// index itself. Here the compiler's check is off inside those functions, so each one is held to what the compiler's
// check does: an index below the first element, at the length, and far beyond it raises ERangeError, on an empty
// array and on one with elements, and an index within the array reads the element.
//
// An index is a NativeInt, as wide as an array's length, so the functions are also called with indexes that do not
// fit in 32 bits, and above all with those whose low 32 bits are an index within the array: a function that compared
// only the low half would read. The same is done through the document's readers, which double a value's index, and
// with indexes worked out from 32-bit numbers whose sum does not fit in 32 bits.
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
    // A document, for its readers.
    Doc: TDocument;
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
function Call(C: TCall; const A: TArrays; I: NativeInt): UInt64;
begin
  Result := 0;
  case C of
    CallByteAt: Result := ByteAt(A.Bytes, I);
    CallWordAt: Result := WordAt(A.Words, I);
    CallWordPtrAt: Result := WordPtrAt(A.Words, I)^;
    CallLoad64: Result := Load64(A.Bytes, I);
    CallLoad32: Result := Load32(A.Bytes, I);
    CallTextByteAt: Result := TextByteAt(A.Text, I + 1);
    CallCheckRun: CheckRun(A.Bytes, I, 1);
    CallCheckWords: CheckWords(A.Words, I, 1);
    CallCheckText: CheckText(A.Text, I + 1, 1);
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
procedure Refused(C: TCall; const A: TArrays; I: NativeInt);
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
  Beyond: Int64;
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
    Refused(C, A, Low(NativeInt));
    Refused(C, A, Low(NativeInt) + 1);
    Refused(C, A, High(NativeInt) - 8);
    Refused(C, A, High(NativeInt) - 1);
    Refused(C, A, High(NativeInt));
    if SizeOf(NativeInt) = 8 then begin
      // Indexes beyond 32 bits. The low 32 bits of the first few are 0, 1, the middle and the last index of the
      // array, and those of the next few are the same with the sign bit set.
      Beyond := Int64(1) shl 32;
      Refused(C, A, NativeInt(Beyond));
      Refused(C, A, NativeInt(Beyond + 1));
      Refused(C, A, NativeInt(Beyond + A.Count div 2));
      Refused(C, A, NativeInt(Beyond + A.Count - 1));
      Refused(C, A, NativeInt(-Beyond));
      Refused(C, A, NativeInt(-Beyond + 1));
      Refused(C, A, NativeInt(-Beyond + A.Count - 1));
      Refused(C, A, NativeInt(Beyond * 1024 + 1));
      Refused(C, A, NativeInt(Int64(High(Int32)) + 1));
      Refused(C, A, NativeInt(Int64(High(Int32)) + 2));
    end;
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
procedure RunRefused(const A: TArrays; Start, Len: NativeInt);
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
        CheckText(A.Text, Start + 1, Len);
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
  Beyond: Int64;
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
  // A start and a length whose sum does not fit in a NativeInt.
  RunRefused(A, High(NativeInt), 0);
  RunRefused(A, High(NativeInt), 1);
  RunRefused(A, High(NativeInt), High(NativeInt));
  RunRefused(A, 1, High(NativeInt));
  RunRefused(A, 0, High(NativeInt));
  RunRefused(A, N, High(NativeInt));
  RunRefused(A, Low(NativeInt), 0);
  RunRefused(A, 0, Low(NativeInt));
  RunRefused(A, Low(NativeInt), Low(NativeInt));
  RunRefused(A, Low(NativeInt), High(NativeInt));
  if SizeOf(NativeInt) = 8 then begin
    // Starts and lengths beyond 32 bits whose low 32 bits are a run within the array.
    Beyond := Int64(1) shl 32;
    RunRefused(A, NativeInt(Beyond), 0);
    RunRefused(A, NativeInt(Beyond), 1);
    RunRefused(A, 0, NativeInt(Beyond));
    RunRefused(A, 0, NativeInt(Beyond + 1));
    RunRefused(A, NativeInt(Beyond), NativeInt(Beyond));
    RunRefused(A, NativeInt(-Beyond), 1);
    RunRefused(A, 0, NativeInt(-Beyond + 1));
  end;
end;

// MustRaise makes a read, of an index worked out from two 32-bit numbers, that must raise ERangeError.
type
  TRead = function(const A: TArrays; First, J: Int32): UInt64;

procedure MustRaise(const Name: UTF8String; Read: TRead; const A: TArrays; First, J: Int32);
begin
  Inc(Checks);
  try
    Sink := Sink + Read(A, First, J);
    Fail(Name + ': no exception');
  except
    on E: ERangeError do ;
    on E: Exception do
      Fail(Name + ': ' + UTF8String(E.ClassName) + ' instead of ERangeError');
  end;
end;

// The reads below work an index out from 32-bit numbers, as the evaluator's loops do, and hand it to a function (see
// TestComputed).
function ReadPair(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := WordAt(A.Words, First + 2 * J);
end;

function ReadByteSum(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := ByteAt(A.Bytes, First + J);
end;

function ReadLoad(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := Load64(A.Bytes, First + J - 8);
end;

function ReadRun(const A: TArrays; First, J: Int32): UInt64;
begin
  CheckWords(A.Words, First shl 1, J shl 1);
  Result := 0;
end;

function ReadHeader(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := DocHeader(A.Doc, NativeInt(First) + J);
end;

function ReadData(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := DocData(A.Doc, NativeInt(First) + J);
end;

function ReadKind(const A: TArrays; First, J: Int32): UInt64;
begin
  Result := DocKind(A.Doc, NativeInt(First) + J);
end;

// TestComputed hands the functions indexes worked out from 32-bit numbers. The true value of each is outside the
// array, and where a NativeInt is 64 bits wide that value reaches the function, which refuses it. Most of the pairs
// are chosen so that the low 32 bits of the true value are an index within the array: arithmetic that was cut to
// 32 bits and then read would read the wrong element.
//
// Where a NativeInt is 32 bits wide the same arithmetic wraps. A sum of two numbers below 2^31 then wraps to a
// negative number, which is refused, and those sums are tested everywhere. A true value of 2^32 or more would wrap
// back into the array, and so would the doubling of a value's index of 2^30 or more. Neither can come from a
// document, whose text and tape are below 2^31 (MaxDocumentSize, which tests/TestDocument.pas holds the parser to),
// so those cases are only made where they can be told apart, with a 64-bit NativeInt.
procedure TestComputed(const A: TArrays);
begin
  // 2^31 - 1 + 2^31 - 1 is 2^32 - 2, which as a 32-bit number is -2.
  MustRaise('ByteAt(First + J) beyond 31 bits', ReadByteSum, A, High(Int32), High(Int32));
  MustRaise('Load64(First + J - 8) beyond 31 bits', ReadLoad, A, High(Int32), High(Int32));
  MustRaise('WordAt(First + 2 * J) beyond 31 bits', ReadPair, A, High(Int32), 1);
  // The run from 2^32 - 2 words, of 2^32 - 2 words.
  MustRaise('CheckWords(First shl 1, J shl 1) beyond 31 bits', ReadRun, A, High(Int32), High(Int32));
  MustRaise('DocHeader of the value -1', ReadHeader, A, -1, 0);
  MustRaise('DocData of the value -1', ReadData, A, -1, 0);
  MustRaise('DocKind of the value -2', ReadKind, A, -1, -1);
  // The value after the last (the document is one value).
  MustRaise('DocHeader of the value 1', ReadHeader, A, 0, 1);
  MustRaise('DocHeader of the value 2^31 - 1', ReadHeader, A, High(Int32), 0);
  MustRaise('DocData of the value 2^30', ReadData, A, 1 shl 30, 0);
  if SizeOf(NativeInt) = 8 then begin
    // 2^31 - 1 + 2 * (2^30 + 1) is 2^32 + 1, whose low 32 bits are 1.
    MustRaise('WordAt(First + 2 * J) beyond 32 bits', ReadPair, A, High(Int32), (1 shl 30) + 1);
    // A value's index is doubled to give its header's: the value 2^31 has the header 2^32, whose low 32 bits are
    // 0, the header of the document's first value. The value -2^31 has the header -2^32, likewise.
    MustRaise('DocHeader of the value 2^31', ReadHeader, A, High(Int32), 1);
    MustRaise('DocData of the value 2^31', ReadData, A, High(Int32), 1);
    MustRaise('DocKind of the value 2^31', ReadKind, A, High(Int32), 1);
    MustRaise('DocHeader of the value -2^31', ReadHeader, A, Low(Int32), 0);
    MustRaise('DocData of the value -2^31', ReadData, A, Low(Int32), 0);
  end;
  // The document's one value is read.
  Inc(Checks);
  if (DocKind(A.Doc, 0) <> KindNumber) or (HeaderCount(DocHeader(A.Doc, 0)) <> DocCount(A.Doc, 0)) then
    Fail('the readers of the document do not read its value');
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
  ParseError: TParseError;
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
  if not ParseDocumentString('7', Full.Doc, ParseError) then
    Fail('the document was not parsed');
  TestComputed(Full);
  // An array that was never given a length is empty as well.
  Empty := Default(TArrays);
  TestIndexes(Empty);
  TestRuns(Empty);
  WriteLn('TestChecked: ', Checks, ' checks, ', Checks - Failures, ' passed, ', Failures, ' failed');
  if (Failures <> 0) or (Sink = 0) then Halt(1);
end.
