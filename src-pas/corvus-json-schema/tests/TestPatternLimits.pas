program TestPatternLimits;

{$I corvus.inc}

(* The limits on a match of a pattern. A schema and an instance may both come from outside, so neither a pattern
  nor a text may make a match run without end or hold memory without bound. A port of match_limit_test.go of the
  ecmaregex package of the Go module and of pattern_match_limit_test.go of the module.

  No test here depends on a quiet machine. A match that must reach its limit runs a pattern that no backtracking
  matcher answers in the life of the machine, and the bounds on the time it is given are many times what it needs.

  What differs from the Go tests:

  A limit is a number of milliseconds, where Go has a duration, and the clock counts whole milliseconds. So where a
  Go test gives a limit of a nanosecond or a microsecond to a match of a few milliseconds, the test here gives a
  millisecond to a match of some hundred milliseconds.

  Go returns an error value. Here IsValid and Evaluate raise EJsonSchemaPatternMatchLimit, and TryIsValid and
  TryEvaluate return False with the message of that exception. No form reports a limit as an instance that is not
  valid.

  The Go package gives a pattern with no lookaround and no backreference to the regexp package, and its tests of
  when it does (TestALongTextIsNotGivenToTheRegexpPackage, TestTheRegexpPackageIsGivenNoLargeProgram) have no
  counterpart: every pattern here runs on the automaton, which reads each byte once, or on the backtracking
  matcher, which has the limit. *)

uses
  {$IFDEF FPC}{$IFDEF UNIX}cthreads,{$ENDIF}{$ENDIF}
  SysUtils,
  Corvus.JsonSchema.EcmaRegex,
  Corvus.JsonSchema.EcmaRegex.Vm,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema;

const
  { Generous is the time, in milliseconds, a test allows for something that takes milliseconds. }
  Generous = 20000;

  { NeverAnswers is a pattern, and NeverAnswered a string it never answers for: a group read back inside a
    repetition, on a text that fails at its end. The pattern is written as it stands in JSON text. }
  NeverAnswers = '^(?:(a+)\\1*)+$';
  NeverAnswersSource = '^(?:(a+)\1*)+$';

var
  Checks, Failures: Int32;
  NeverAnswered: UTF8String;

procedure Check(Ok: Boolean; const What: UTF8String);
begin
  Inc(Checks);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What);
  end;
end;

function Number(V: Int64): UTF8String;
begin
  Result := UTF8String(IntToStr(V));
end;

function Rep(const S: UTF8String; N: Int32): UTF8String;
var
  K: Int32;
begin
  Result := '';
  for K := 1 to N do
    Result := Result + S;
end;

function ToBytes(const S: UTF8String): TBytes;
begin
  Result := BytesOf(S);
end;

function Limited(Ms: Int64): TJsonSchemaOptions;
begin
  Result := DefaultJsonSchemaOptions;
  WithPatternMatchLimit(Result, Ms);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The engine }

type
  TPair = record
    Pattern, Text: UTF8String;
  end;

function Pair(const Pattern, Text: UTF8String): TPair;
begin
  Result.Pattern := Pattern;
  Result.Text := Text;
end;

function MustCompileRegex(const Pattern: UTF8String; out Re: TEcmaRegex): Boolean;
var
  Error: UTF8String;
begin
  Result := EcmaRegexCompile(Pattern, Re, Error);
  Check(Result, 'EcmaRegexCompile(' + Pattern + '): ' + Error);
end;

{ TestAMatchIsAbandonedAtItsLimit runs patterns that never answer with a limit, and expects the match to be
  abandoned for its time no sooner than the limit and not long after it. No rewrite of the engine makes these
  easy: each reads a group back inside a repetition, so the ways through the text cannot be told apart. }
procedure TestAMatchIsAbandonedAtItsLimit;
const
  Limits: array[0..1] of Int64 = (20, 300);
var
  Forever: array[0..3] of TPair;
  L, C: Int32;
  Re: TEcmaRegex;
  Text: TBytes;
  Stop: UInt8;
  Matched: Boolean;
  Start, Took: Int64;
begin
  Forever[0] := Pair('^(?:(a+)\1*)+$', Rep('a', 60) + 'b');
  Forever[1] := Pair('(a*)*\1b', Rep('a', 60));
  Forever[2] := Pair('^(?:(\w+)\s?\1?)+$', Rep('ab ', 30) + '!');
  Forever[3] := Pair('(?=(?:(a+)\1*)+b)a', Rep('a', 60));
  for L := 0 to High(Limits) do
    for C := 0 to High(Forever) do begin
      if not MustCompileRegex(Forever[C].Pattern, Re) then
        Continue;
      Text := ToBytes(Forever[C].Text);
      Start := MonotonicMilliseconds;
      Matched := EcmaRegexIsMatchLimit(Re, Text, 0, Length(Text), Limits[L], Stop);
      Took := MonotonicMilliseconds - Start;
      Check((Stop = EcmaRegexStopTime) and not Matched, Forever[C].Pattern + ' with a limit of ' + Number(Limits[L])
        + ' ms: stop ' + Number(Stop) + ', want the time');
      Check((Took >= Limits[L]) and (Took <= Limits[L] + Generous), Forever[C].Pattern + ' with a limit of '
        + Number(Limits[L]) + ' ms was abandoned after ' + Number(Took) + ' ms');
    end;
end;

{ TestAMatchWithNoLimitIsNotAbandoned checks that a limit of zero is no limit: a match that takes far longer than
  a small limit allows runs to its answer, as it does through EcmaRegexIsMatch. }
procedure TestAMatchWithNoLimitIsNotAbandoned;
var
  Re: TEcmaRegex;
  Text: TBytes;
  Stop: UInt8;
  Matched: Boolean;
begin
  if not MustCompileRegex('^(?:(a+)\1*)+$', Re) then
    Exit;
  // Some hundred milliseconds of work: far beyond a limit of a millisecond.
  Text := ToBytes(Rep('a', 16) + 'b');
  Matched := EcmaRegexIsMatchLimit(Re, Text, 0, Length(Text), 1, Stop);
  Check((Stop = EcmaRegexStopTime) and not Matched, 'with a limit of 1 ms: stop ' + Number(Stop) + ', want the time');
  Matched := EcmaRegexIsMatchLimit(Re, Text, 0, Length(Text), 0, Stop);
  Check((Stop = EcmaRegexStopNone) and not Matched, 'with no limit: stop ' + Number(Stop) + ', want an answer of false');
  Check(not EcmaRegexIsMatch(Re, Text, 0, Length(Text)), 'EcmaRegexIsMatch: true, want false');
  Matched := EcmaRegexIsMatchLimit(Re, Text, 0, 16, Generous, Stop);
  Check((Stop = EcmaRegexStopNone) and Matched, 'a text that matches, with a limit: want true and no stop');
end;

(* TestAMatchIsAbandonedAtItsMemoryLimit runs patterns that push on the backtracking stack without end. With a
  limit on the match the stack is capped, and the match is abandoned for its memory long before the clock says
  anything. Without the cap the first of these took 2 GB and raised EOutOfMemory. *)
procedure TestAMatchIsAbandonedAtItsMemoryLimit;
var
  Cases: array[0..3] of TPair;
  C: Int32;
  Re: TEcmaRegex;
  Text: TBytes;
  Stop: UInt8;
  Matched: Boolean;
  Start, Took: Int64;
  {$IFDEF FPC}
  Before, Peak: PtrUInt;
  {$ENDIF}
begin
  Cases[0] := Pair('(?:a?){2147483647}', '');
  Cases[1] := Pair('(?:a?){100000000}', Rep('a', 100));
  Cases[2] := Pair('(?:(?:(?:a?){1000}){1000}){1000}', Rep('a', 20));
  Cases[3] := Pair('(?:(a)|b|){2147483647}\1', '');
  for C := 0 to High(Cases) do begin
    if not MustCompileRegex(Cases[C].Pattern, Re) then
      Continue;
    Text := ToBytes(Cases[C].Text);
    EcmaRegexReleaseThreadScratch;
    {$IFDEF FPC}
    Before := GetFPCHeapStatus.CurrHeapUsed;
    {$ENDIF}
    Start := MonotonicMilliseconds;
    Matched := EcmaRegexIsMatchLimit(Re, Text, 0, Length(Text), 3600000, Stop);
    Took := MonotonicMilliseconds - Start;
    Check((Stop = EcmaRegexStopMemory) and not Matched, Cases[C].Pattern + ': stop ' + Number(Stop)
      + ', want the memory');
    Check(Took <= Generous, Cases[C].Pattern + ' was abandoned after ' + Number(Took) + ' ms');
    {$IFDEF FPC}
    // The stack is dropped after a match that grew it past what is kept, so the peak of the heap says how far
    // it went: the cap of 1,048,576 entries of 16 bytes, a stack that doubles as it grows, and the copy that
    // growing makes. The heap of the test itself is small.
    Peak := GetFPCHeapStatus.MaxHeapUsed;
    Check(Peak - Before <= 6 * 16 * 1048576, Cases[C].Pattern + ' took the heap to ' + Number(Peak shr 20)
      + ' MB before it was abandoned');
    {$ENDIF}
  end;
end;

{ TestALimitDoesNotChangeAnAnswer runs patterns of each back end with a limit they do not reach and expects the
  answer they give without one. }
procedure TestALimitDoesNotChangeAnAnswer;
const
  Limits: array[0..1] of Int64 = (0, 60000);
type
  TCase = record
    Pattern, Text: UTF8String;
    Want: Boolean;
  end;
var
  Cases: array of TCase;
  Long, Eacute: UTF8String;
  C, L: Int32;
  Re: TEcmaRegex;
  Text: TBytes;
  Stop: UInt8;
  Got: Boolean;

  procedure Add(const Pattern, AText: UTF8String; Want: Boolean);
  var
    N: Int32;
  begin
    N := Length(Cases);
    SetLength(Cases, N + 1);
    Cases[N].Pattern := Pattern;
    Cases[N].Text := AText;
    Cases[N].Want := Want;
  end;

begin
  Cases := nil;
  Long := Rep('ab', 50000);
  Eacute := #$C3#$A9;
  Add('^[a-z]+$', 'abc', True);
  Add('^[a-z]+$', 'abc1', False);
  Add('^\p{L}+$', 'caf' + Eacute, True);
  Add('^\p{L}+$', 'caf' + Eacute + ' ', False);
  Add('^(?=.*\d)\w+$', 'abc1', True);
  Add('^(?=.*\d)\w+$', 'abcd', False);
  Add('^(a+)\1$', 'aaaa', True);
  Add('^(a+)\1$', 'aaa', False);
  Add('(?<=\$)\d+', 'cost $42', True);
  Add('^(?=.)(?:a|b)*$', Long, True);
  Add('^(?=.)(?:a|b)*c$', Long, False);
  Add('^[ab]*$', Long, True);
  Add('^[ab]*c$', Long, False);
  Add('^[abé]*$', Long + Eacute, True);
  Add('^[abé]*c$', Long + Eacute, False);
  for C := 0 to High(Cases) do begin
    if not MustCompileRegex(Cases[C].Pattern, Re) then
      Continue;
    Text := ToBytes(Cases[C].Text);
    for L := 0 to High(Limits) do begin
      Got := EcmaRegexIsMatchLimit(Re, Text, 0, Length(Text), Limits[L], Stop);
      Check((Stop = EcmaRegexStopNone) and (Got = Cases[C].Want), Cases[C].Pattern + ' on '
        + Copy(Cases[C].Text, 1, 20) + ' with a limit of ' + Number(Limits[L]) + ' ms: stop ' + Number(Stop));
    end;
  end;
end;

{ TestTheClockDoesNotGoBack reads the clock of the limit many times: it never goes back, and it moves. }
procedure TestTheClockDoesNotGoBack;
var
  First, Last, Now: Int64;
  K: Int32;
  Back: Boolean;
begin
  First := MonotonicMilliseconds;
  Last := First;
  Back := False;
  K := 0;
  repeat
    Now := MonotonicMilliseconds;
    Back := Back or (Now < Last);
    Last := Now;
    Inc(K);
  until (Now - First >= 20) or (K > 2000000000);
  Check(not Back, 'the clock went back');
  Check(Now - First >= 20, 'the clock did not move');
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The validator }

type
  TLimitSchema = record
    Name, Schema, Instance: UTF8String;
  end;
  TLimitSchemas = array of TLimitSchema;

{ LimitSchemas holds the pattern in each keyword that matches a regular expression, with an instance that makes it
  match the text. The plans run pattern and patternProperties in several ways, so each is here in the shapes that
  take a different one. }
function LimitSchemas(const Pattern, Text: UTF8String): TLimitSchemas;

  procedure Add(const Name, Schema, Instance: UTF8String);
  var
    N: Int32;
  begin
    N := Length(Result);
    SetLength(Result, N + 1);
    Result[N].Name := Name;
    Result[N].Schema := Schema;
    Result[N].Instance := Instance;
  end;

begin
  Result := nil;
  Add('pattern', '{"pattern": "' + Pattern + '"}', '"' + Text + '"');
  Add('pattern in a property', '{"type": "object", "properties": {"s": {"type": "string", "pattern": "' + Pattern
    + '"}}}', '{"s": "' + Text + '"}');
  Add('pattern in items', '{"items": {"pattern": "' + Pattern + '"}}', '["' + Text + '"]');
  Add('pattern under not', '{"not": {"pattern": "' + Pattern + '"}}', '"' + Text + '"');
  Add('pattern under anyOf', '{"anyOf": [{"pattern": "' + Pattern + '"}, {"type": "string"}]}', '"' + Text + '"');
  Add('patternProperties', '{"patternProperties": {"' + Pattern + '": {"type": "integer"}}}', '{"' + Text + '": 1}');
  Add('patternProperties with properties', '{"properties": {"id": true}, "patternProperties": {"' + Pattern
    + '": true}, "additionalProperties": false}', '{"id": 1, "' + Text + '": 1}');
  Add('patternProperties and unevaluatedProperties', '{"patternProperties": {"' + Pattern
    + '": true}, "unevaluatedProperties": false}', '{"' + Text + '": 1}');
  Add('patternProperties under a condition', '{"if": {"patternProperties": {"' + Pattern
    + '": {"const": 1}}}, "then": {"minProperties": 1}}', '{"' + Text + '": 1}');
  Add('propertyNames', '{"propertyNames": {"pattern": "' + Pattern + '"}}', '{"' + Text + '": 1}');
end;

{ LimitMessage is the message of a match of NeverAnswers that was abandoned for its time. }
function LimitMessage(const Shown: UTF8String): UTF8String;
begin
  Result := 'A match of the regular expression ''' + NeverAnswersSource
    + ''' was abandoned: it took longer than the limit of ' + Shown + '.';
end;

type
  { The entry points of a validator, each as a number. }
  TEntry = 0..17;

const
  EntryNames: array[TEntry] of UTF8String = ('IsValid of a document', 'IsValid of a string', 'IsValid of bytes',
    'TryIsValid of a document', 'TryIsValid of a string', 'TryIsValid of bytes', 'Evaluate of a document at Basic',
    'Evaluate of a document at Detailed', 'Evaluate of a document at Verbose', 'Evaluate of a string',
    'Evaluate of bytes', 'TryEvaluate of a document at Basic', 'TryEvaluate of a document at Detailed',
    'TryEvaluate of a document at Verbose', 'TryEvaluate of a string', 'TryEvaluate of bytes',
    'IsValid of a part of bytes', 'TryIsValid of a part of bytes');

{ RunEntry runs one entry point. Raised says it raised EJsonSchemaPatternMatchLimit, with the exception's fields in
  Pattern, Limit and Memory, and Message is the message of the exception or the reason a Try form gave. Any other
  exception goes to the caller. }
function RunEntry(const V: IJsonSchemaValidator; Entry: TEntry; const Instance: UTF8String; out Raised: Boolean;
  out Message, Pattern: UTF8String; out Limit: Int64; out Memory: Boolean): Boolean;
var
  Doc: TJsonDocument;
  Bytes, Padded: TBytes;
  Results: TJsonSchemaResults;
begin
  Result := False;
  Raised := False;
  Message := '';
  Pattern := '';
  Limit := -1;
  Memory := False;
  Doc := ParseJson(Instance);
  Bytes := BytesOf(Instance);
  Padded := BytesOf('[[' + Instance + ']]');
  Results := NewJsonSchemaResults(Basic);
  case Entry of
    7, 12: Results := NewJsonSchemaResults(Detailed);
    8, 13: Results := NewJsonSchemaResults(Verbose);
  end;
  try
    case Entry of
      0: Result := V.IsValid(Doc);
      1: Result := V.IsValid(Instance);
      2: Result := V.IsValid(Bytes, 0, Length(Bytes));
      3: Result := V.TryIsValid(Doc, Message);
      4: Result := V.TryIsValid(Instance, Message);
      5: Result := V.TryIsValid(Bytes, 0, Length(Bytes), Message);
      6, 7, 8: Result := V.Evaluate(Doc, Results);
      9: Result := V.Evaluate(Instance, Results);
      10: Result := V.Evaluate(Bytes, 0, Length(Bytes), Results);
      11, 12, 13: Result := V.TryEvaluate(Doc, Results, Message);
      14: Result := V.TryEvaluate(Instance, Results, Message);
      15: Result := V.TryEvaluate(Bytes, 0, Length(Bytes), Results, Message);
      16: Result := V.IsValid(Padded, 2, Length(Bytes));
      17: Result := V.TryIsValid(Padded, 2, Length(Bytes), Message);
    end;
  except
    on E: EJsonSchemaPatternMatchLimit do begin
      Raised := True;
      Message := E.Utf8Message;
      Pattern := E.Pattern;
      Limit := E.Limit;
      Memory := E.Memory;
    end;
  end;
end;

function IsTryEntry(Entry: TEntry): Boolean;
begin
  Result := Entry in [3, 4, 5, 11, 12, 13, 14, 15, 17];
end;

{ ExpectMatchLimit runs one entry point on an instance whose match never answers, and checks that it reports the
  limit: the forms that raise raise EJsonSchemaPatternMatchLimit with the pattern and the limit, and the Try forms
  return False with its message. It returns the time the entry point took, in milliseconds. }
function ExpectMatchLimit(const V: IJsonSchemaValidator; Entry: TEntry; const What, Instance: UTF8String;
  LimitMs: Int64; const Shown: UTF8String): Int64;
var
  Start, Limit: Int64;
  Ok, Raised, Memory: Boolean;
  Message, Pattern: UTF8String;
begin
  Start := MonotonicMilliseconds;
  Ok := RunEntry(V, Entry, Instance, Raised, Message, Pattern, Limit, Memory);
  Result := MonotonicMilliseconds - Start;
  Check(not Ok, What + ', ' + EntryNames[Entry] + ': the result is true');
  Check(Message = LimitMessage(Shown), What + ', ' + EntryNames[Entry] + ': the message is ' + Message);
  if IsTryEntry(Entry) then
    Check(not Raised, What + ', ' + EntryNames[Entry] + ': a Try form raised')
  else
    Check(Raised and (Pattern = NeverAnswersSource) and (Limit = LimitMs) and not Memory, What + ', '
      + EntryNames[Entry] + ': raised ' + UTF8String(BoolToStr(Raised, True)) + ' with the pattern ' + Pattern
      + ' and the limit ' + Number(Limit));
end;

{ TestAMatchThatReachesItsLimitIsAnError checks every entry point, for pattern and patternProperties, failing fast
  and collecting: a match that reaches its limit ends the validation with the error of the limit, no sooner than
  the limit and not long after it. It is never an answer. }
procedure TestAMatchThatReachesItsLimitIsAnError;
const
  Limit = 50;
var
  Schemas: TLimitSchemas;
  S: Int32;
  Entry: TEntry;
  V: IJsonSchemaValidator;
  Took: Int64;
begin
  Schemas := LimitSchemas(NeverAnswers, NeverAnswered);
  for S := 0 to High(Schemas) do begin
    V := CompileJsonSchema(Schemas[S].Schema, Limited(Limit));
    for Entry := Low(TEntry) to High(TEntry) do begin
      Took := ExpectMatchLimit(V, Entry, Schemas[S].Name, Schemas[S].Instance, Limit, '50ms');
      Check((Took >= Limit) and (Took <= Limit + Generous), Schemas[S].Name + ', ' + EntryNames[Entry]
        + ': abandoned after ' + Number(Took) + ' ms with a limit of 50 ms');
    end;
  end;
end;

{ TestTheDefaultMatchLimitIsOneSecond checks the limit is on when no option names it, and is a second. }
procedure TestTheDefaultMatchLimitIsOneSecond;
var
  V: IJsonSchemaValidator;
  Took: Int64;
begin
  Check(DefaultPatternMatchLimit = 1000, 'DefaultPatternMatchLimit is ' + Number(DefaultPatternMatchLimit));
  Check(DefaultJsonSchemaOptions.PatternMatchLimit = 1000, 'the options start with a limit of '
    + Number(DefaultJsonSchemaOptions.PatternMatchLimit));
  V := CompileJsonSchema('{"pattern": "' + NeverAnswers + '"}');
  Took := ExpectMatchLimit(V, 1, 'the default limit', '"' + NeverAnswered + '"', 1000, '1s');
  Check((Took >= 1000) and (Took <= 1000 + Generous), 'abandoned after ' + Number(Took) + ' ms with the default limit');
  Took := ExpectMatchLimit(V, 4, 'the default limit', '"' + NeverAnswered + '"', 1000, '1s');
  Check((Took >= 1000) and (Took <= 1000 + Generous), 'abandoned after ' + Number(Took) + ' ms with the default limit');
end;

{ TestTheMatchLimitIsForEachMatch checks the limit is on one match and not on the validation: an array of strings
  that each take a part of the limit is validated, however long the whole takes. }
procedure TestTheMatchLimitIsForEachMatch;
var
  Item, Instance, Error: UTF8String;
  V: IJsonSchemaValidator;
  K: Int32;
  Ok: Boolean;
begin
  // Some tens of milliseconds for each string.
  Item := '"' + Rep('a', 13) + 'b"';
  Instance := '[' + Item;
  for K := 2 to 40 do
    Instance := Instance + ',' + Item;
  Instance := Instance + ']';
  V := CompileJsonSchema('{"items": {"not": {"pattern": "' + NeverAnswers + '"}}}', Limited(Generous));
  Ok := V.TryIsValid(Instance, Error);
  Check(Ok and (Error = ''), '40 strings that each take a part of the limit: ' + Error);
  Check(V.IsValid(Instance), '40 strings that each take a part of the limit, through IsValid');
end;

{ TestTheMatchLimitCanBeTurnedOff checks that NoPatternMatchLimit, zero, or less restores a match with no limit: a
  match that takes far longer than a small limit allows runs to its answer. }
procedure TestTheMatchLimitCanBeTurnedOff;
const
  Off: array[0..2] of Int64 = (NoPatternMatchLimit, 0, -1000);
var
  Schema, Instance, Error: UTF8String;
  V: IJsonSchemaValidator;
  K: Int32;
  O: TJsonSchemaOptions;
  Ok: Boolean;
begin
  Schema := '{"pattern": "' + NeverAnswers + '"}';
  // Some hundred milliseconds of work: far beyond a limit of a millisecond.
  Instance := '"' + Rep('a', 16) + 'b"';
  ExpectMatchLimit(CompileJsonSchema(Schema, Limited(1)), 1, 'a limit of a millisecond', Instance, 1, '1ms');
  for K := 0 to High(Off) do begin
    V := CompileJsonSchema(Schema, Limited(Off[K]));
    Ok := V.TryIsValid(Instance, Error);
    Check((not Ok) and (Error = ''), 'with the limit ' + Number(Off[K]) + ': want false and no error, got ' + Error);
    Check(not V.IsValid(Instance), 'with the limit ' + Number(Off[K]) + ', through IsValid: want false');
    Ok := V.TryIsValid('"aaaa"', Error);
    Check(Ok and (Error = ''), 'with the limit ' + Number(Off[K]) + ', a string that matches: ' + Error);
  end;
  Check(Limited(-5).PatternMatchLimit = 0, 'a limit below zero is kept as ' + Number(Limited(-5).PatternMatchLimit));
  // The last option given is the one that counts.
  O := DefaultJsonSchemaOptions;
  WithPatternMatchLimit(O, NoPatternMatchLimit);
  WithPatternMatchLimit(O, 1);
  ExpectMatchLimit(CompileJsonSchema(Schema, O), 4, 'a limit given after none', Instance, 1, '1ms');
end;

(* TestAMatchThatReachesItsMemoryLimitIsAnError runs a pattern that pushes on the backtracking stack without end.
  With a limit on the match the stack is capped, and the error says the memory was the limit. The time limit here
  is long, so only the memory can end the match. *)
procedure TestAMatchThatReachesItsMemoryLimitIsAnError;
const
  Want = 'A match of the regular expression ''(?:a?){2147483647}'' was abandoned: it needed more than the limit of '
    + '1,048,576 backtracking entries.';
var
  V: IJsonSchemaValidator;
  Start, Took: Int64;
  Error: UTF8String;
  Raised: Boolean;
begin
  V := CompileJsonSchema('{"pattern": "(?:a?){2147483647}"}', Limited(3600000));
  Start := MonotonicMilliseconds;
  Check(not V.TryIsValid('""', Error), 'TryIsValid is true');
  Took := MonotonicMilliseconds - Start;
  Check(Error = Want, 'the reason is ' + Error);
  Check(Took <= Generous, 'abandoned after ' + Number(Took) + ' ms');
  Raised := False;
  try
    V.IsValid('""');
  except
    on E: EJsonSchemaPatternMatchLimit do begin
      Raised := True;
      Check(E.Memory and (E.Limit = 3600000) and (E.Pattern = '(?:a?){2147483647}') and (E.Utf8Message = Want),
        'the exception holds the pattern ' + E.Pattern + ' and the message ' + E.Utf8Message);
    end;
  end;
  Check(Raised, 'IsValid did not raise');
end;

{ TestAMatchWhileCompilingHasTheLimit checks the matches made when a schema is compiled. The plans match each
  pattern of patternProperties against the property names the schema declares, and that match has the limit too:
  compiling raises the error. }
procedure TestAMatchWhileCompilingHasTheLimit;
const
  Limit = 50;
var
  Schemas: array[0..2] of UTF8String;
  K: Int32;
  V: IJsonSchemaValidator;
  Start, Took: Int64;
  Raised: Boolean;
begin
  Schemas[0] := '{"properties": {"' + NeverAnswered + '": true}, "patternProperties": {"' + NeverAnswers
    + '": {"type": "integer"}}}';
  Schemas[1] := '{"properties": {"' + NeverAnswered + '": true}, "patternProperties": {"' + NeverAnswers
    + '": true}, "additionalProperties": false}';
  Schemas[2] := '{"properties": {"' + NeverAnswered + '": true}, "allOf": [{"patternProperties": {"' + NeverAnswers
    + '": true}}], "unevaluatedProperties": false}';
  for K := 0 to High(Schemas) do begin
    V := nil;
    Raised := False;
    Start := MonotonicMilliseconds;
    try
      V := CompileJsonSchema(Schemas[K], Limited(Limit));
    except
      on E: EJsonSchemaPatternMatchLimit do begin
        Raised := True;
        Check((E.Pattern = NeverAnswersSource) and (E.Limit = Limit) and (not E.Memory)
          and (E.Utf8Message = LimitMessage('50ms')), 'compiling schema ' + Number(K) + ': the exception says '
          + E.Utf8Message);
      end;
    end;
    Took := MonotonicMilliseconds - Start;
    Check(Took <= Limit + Generous, 'compiling schema ' + Number(K) + ' took ' + Number(Took) + ' ms');
    // A schema that compiled made no match, so the match is made when an instance has the property.
    if not Raised then
      ExpectMatchLimit(V, 1, 'validating schema ' + Number(K), '{"' + NeverAnswered + '": 1}', Limit, '50ms');
  end;
end;

{ TestAnOrdinaryPatternIsNotAffectedByTheLimit checks patterns of every matcher with the default limit, a limit of
  their own and none: the answers are the same. }
procedure TestAnOrdinaryPatternIsNotAffectedByTheLimit;
const
  Limits: array[0..2] of Int64 = (DefaultPatternMatchLimit, 10000, NoPatternMatchLimit);
var
  Eacute, Error: UTF8String;
  L: Int32;

  procedure Ordinary(const Pattern, Text: UTF8String; Want: Boolean);
  var
    V: IJsonSchemaValidator;
    Results: TJsonSchemaResults;
    Schemas: TLimitSchemas;
    Ok: Boolean;
  begin
    Schemas := LimitSchemas(Pattern, Text);
    V := CompileJsonSchema(Schemas[0].Schema, Limited(Limits[L]));
    Ok := V.TryIsValid(Schemas[0].Instance, Error);
    Check((Ok = Want) and (Error = ''), Pattern + ' on ' + Copy(Text, 1, 20) + ' with a limit of '
      + Number(Limits[L]) + ' ms: ' + Error);
    Check(V.IsValid(Schemas[0].Instance) = Want, Pattern + ' on ' + Copy(Text, 1, 20) + ', through IsValid');
    Results := NewJsonSchemaResults(Basic);
    Ok := V.TryEvaluate(Schemas[0].Instance, Results, Error);
    Check((Ok = Want) and (Error = ''), Pattern + ' on ' + Copy(Text, 1, 20) + ', collecting: ' + Error);
  end;

begin
  Eacute := #$C3#$A9;
  for L := 0 to High(Limits) do begin
    Ordinary('^[a-z]+$', 'abc', True);
    Ordinary('^[a-z]+$', 'abc1', False);
    Ordinary('^x-', 'x-a', True);
    Ordinary('^\\p{L}+$', 'caf' + Eacute, True);
    Ordinary('^\\p{L}+$', 'caf' + Eacute + ' ', False);
    Ordinary('^(?=.*\\d)\\w+$', 'abc1', True);
    Ordinary('^(?=.*\\d)\\w+$', 'abcd', False);
    Ordinary('^(a+)\\1$', 'aaaa', True);
    Ordinary('^(a+)\\1$', 'aaa', False);
    Ordinary('(?<=\\$)\\d+', 'cost $42', True);
    Ordinary('^(?:[a-z]+\\.)+[a-z]+$', 'a.b.c', True);
    Ordinary('^[ab' + Eacute + ']*c$', Rep('ab', 20000) + Eacute, False);
  end;
end;

(* TestTheMatchersWithNoLimitReadTheTextABoundedNumberOfTimes checks the matchers that are not the engine, which
  have no limit and so must take time proportional to the text. Three shapes did not:

    - a class sequence that is not anchored is tried at every position, and one 50,000 characters wide took the
      width times the text: "[a-z]{50000}[0-9]|x" took 5 seconds on 100,000 characters;
    - a literal that is not anchored is searched for at every position, the same way;
    - the final sequence of a list is tried after every item, and one that reads through the separators, or that
      is matched by the length of what is left, read to the end of the text each time:
      "^(?:[a-z]+,)*[a-z,]+[0-9]$" took 8 seconds on 33,333 items.

  Each of those is now left to the engine, which has the limit. The shapes beside them that read a bounded part of
  the text at each position keep their matcher. On a text of a million characters the old matchers took minutes,
  and every pattern here must end, in an answer or in the error of the limit, in the time a test allows. *)
procedure TestTheMatchersWithNoLimitReadTheTextABoundedNumberOfTimes;
var
  Long, Items, Error: UTF8String;
  K: Int32;

  procedure Shape(const Pattern, Text: UTF8String; Kind: TMatcherKind);
  var
    P: TPattern;
    V: IJsonSchemaValidator;
    Start, Took: Int64;
  begin
    if not CompilePattern(Pattern, P) then begin
      Check(False, Copy(Pattern, 1, 40) + ' does not compile');
      Exit;
    end;
    Check(P.Kind = Kind, Copy(Pattern, 1, 40) + ' has the matcher ' + Number(Ord(P.Kind)) + ', want '
      + Number(Ord(Kind)));
    V := CompileJsonSchema('{"pattern": "' + Pattern + '"}');
    Start := MonotonicMilliseconds;
    Check(not V.TryIsValid('"' + Text + '"', Error), Copy(Pattern, 1, 40) + ' matches');
    Took := MonotonicMilliseconds - Start;
    Check(Took <= Generous, Copy(Pattern, 1, 40) + ' took ' + Number(Took) + ' ms on ' + Number(Length(Text))
      + ' characters');
    Check((Error = '') or (Pos('A match of the regular expression', Error) = 1), Copy(Pattern, 1, 40) + ': '
      + Error);
  end;

begin
  SetLength(Long, 1000000);
  FillChar(Long[1], Length(Long), Ord('a'));
  SetLength(Items, 999999);
  for K := 0 to 333332 do begin
    Items[3 * K + 1] := 'a';
    Items[3 * K + 2] := 'b';
    Items[3 * K + 3] := ',';
  end;
  // Tried at every position, and wide.
  Shape('[a-z]{50000}[0-9]|x', Long, MatchEngine);
  Shape('[a-z]{65}[0-9]|x', Long, MatchEngine);
  Shape(Rep('a', 65) + 'b|x', Long, MatchEngine);
  // Tried at every position, and narrow.
  Shape('[a-z]{63}[0-9]|x', Long, MatchAlternatives);
  Shape(Rep('a', 63) + 'b|x', Long, MatchAlternatives);
  // Anchored, so tried once.
  Shape('^[a-z]{50000}[0-9]|^x', Long, MatchAlternatives);
  Shape('[a-z]{50000}[0-9]$|^x', Long, MatchAlternatives);
  // A list whose final sequence reads through the separators, or is matched by length.
  Shape('^(?:[a-z]+,)*[a-z,]+[0-9]$', Items, MatchEngine);
  Shape('^(?:[a-z]+,)*[a-z,]*[0-9]$', Items, MatchEngine);
  Shape('^(?:[a-z]+,)*[a-z]*[a-z]{2}$', Items, MatchEngine);
  // A list whose final sequence stops at a separator.
  Shape('^(?:[a-z]+,)*[a-z]{2}[0-9]$', Items, MatchSeparatedList);
  Shape('^(?:[a-z]+,)*[a-z]+[0-9]$', Items, MatchSeparatedList);
  Shape('^(?:[a-z]+,)*[a-z]*[0-9]$', Items, MatchSeparatedList);
  Shape('^[a-z]+(?:,[a-z]+)*$', Items, MatchSeparatedList);
  // A long literal the engine searches for.
  Shape(Rep('a', 15000) + 'b(?=c)', Long, MatchEngine);
end;

{$IFDEF FPC}
var
  { The memory manager is wrapped to count the requests for memory of TestALongPatternIsCopiedARunAtATime. }
  SystemManager: TMemoryManager;
  Requests: Int64 = 0;

function CountingGetMem(Size: PtrUInt): Pointer;
begin
  Inc(Requests);
  Result := SystemManager.GetMem(Size);
end;

function CountingAllocMem(Size: PtrUInt): Pointer;
begin
  Inc(Requests);
  Result := SystemManager.AllocMem(Size);
end;

function CountingReAllocMem(var P: Pointer; Size: PtrUInt): Pointer;
begin
  Inc(Requests);
  Result := SystemManager.ReAllocMem(P, Size);
end;
{$ENDIF}

{ TestALongPatternIsCopiedARunAtATime compiles patterns that are a long literal for the matchers that multiply out
  the alternatives of a pattern, and counts the requests for memory, which no load on the machine changes. The
  characters a pattern copies were added to every branch one at a time, each a request to make the branch a
  character longer, and each such request can copy the branch: a literal of 128,000 characters took 6.6 seconds
  to compile. They are now added a run at a time, so a pattern sixteen times as long asks for memory about as
  often. (Go counts the bytes allocated. Here the memory manager grows a block where it stands when it can, so the
  bytes asked for say little, and the number of requests says how the pattern was built.) }
procedure TestALongPatternIsCopiedARunAtATime;
{$IFDEF FPC}
var
  Counting: TMemoryManager;
  Round: Int32;

  function Compile(const Pattern: UTF8String): Int64;
  var
    P: TPattern;
    Ok: Boolean;
  begin
    SetMemoryManager(Counting);
    Result := Requests;
    Ok := CompilePattern(Pattern, P);
    Result := Requests - Result;
    SetMemoryManager(SystemManager);
    Check(Ok, 'a pattern of ' + Number(Length(Pattern)) + ' characters does not compile');
  end;

  procedure Shape(const Name, Before, After: UTF8String);
  var
    Short, Long: Int64;
  begin
    Inc(Round);
    Short := Compile(Before + Rep('a', 1000) + Number(Round) + After);
    Long := Compile(Before + Rep('a', 16000) + Number(Round) + After);
    WriteLn('TestALongPatternIsCopiedARunAtATime: ', Name, ': ', Short, ' requests for 1,000 characters, ', Long,
      ' for 16,000');
    // Built a character at a time, the long pattern asks sixteen times as often, and some 16,000 times more.
    Check(Long <= 4 * Short + 256, Name + ': ' + Number(Long) + ' requests for memory for 16,000 characters and '
      + Number(Short) + ' for 1,000');
  end;

begin
  GetMemoryManager(SystemManager);
  Counting := SystemManager;
  Counting.GetMem := @CountingGetMem;
  Counting.AllocMem := @CountingAllocMem;
  Counting.ReAllocMem := @CountingReAllocMem;
  Round := 0;
  Shape('anchored', '^', '$');
  Shape('anchored at the start', '^', '');
  Shape('alternatives', '^(?:', '|xyz)$');
  Shape('with an escape', '^\.', '$');
  Shape('with a class', '^[.]', '$');
end;
{$ELSE}
begin
  WriteLn('TestALongPatternIsCopiedARunAtATime: SKIPPED, the count of requests is taken from the memory manager of '
    + 'Free Pascal');
end;
{$ENDIF}

begin
  Checks := 0;
  Failures := 0;
  NeverAnswered := Rep('a', 60) + 'b';
  TestTheClockDoesNotGoBack;
  TestAMatchIsAbandonedAtItsLimit;
  TestAMatchWithNoLimitIsNotAbandoned;
  TestAMatchIsAbandonedAtItsMemoryLimit;
  TestALimitDoesNotChangeAnAnswer;
  TestAMatchThatReachesItsLimitIsAnError;
  TestTheDefaultMatchLimitIsOneSecond;
  TestTheMatchLimitIsForEachMatch;
  TestTheMatchLimitCanBeTurnedOff;
  TestAMatchThatReachesItsMemoryLimitIsAnError;
  TestAMatchWhileCompilingHasTheLimit;
  TestAnOrdinaryPatternIsNotAffectedByTheLimit;
  TestTheMatchersWithNoLimitReadTheTextABoundedNumberOfTimes;
  TestALongPatternIsCopiedARunAtATime;
  JsonSchemaReleaseThreadScratch;
  WriteLn(Checks - Failures, '/', Checks, ' checks passed');
  if Failures <> 0 then
    Halt(1);
end.
