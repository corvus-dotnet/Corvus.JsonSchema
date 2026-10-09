unit Corvus.JsonSchema.Results;

{$I corvus.inc}

{ Results collection. The evaluator opens a context per subschema application and writes keyword rows into the open
  context. Closing a context either commits it (a summary row, then its own rows newest first, after its committed
  descendants) or pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist
  and which carry message text.

  A port of results.go of the Go module. A collector is a record, and the methods of the Go collector are functions
  that take it. Its lists are arrays kept at what they have grown to, each with a count beside it. }

interface

uses
  SysUtils;

type
  { TResultsLevel says how much a TResultsCollector records. }
  TResultsLevel = (
    { Basic records failures only, without message text (the lowest overhead). }
    Basic,
    { Detailed records failures only, with message text. }
    Detailed,
    { Verbose records every evaluation, passing and failing, with message text, including annotations. }
    Verbose);

  { TSchemaResult is one result row. }
  TSchemaResult = record
    { IsMatch says whether the keyword or subschema matched. }
    IsMatch: Boolean;
    { Message is the message, or '' when the level records none or the keyword has none. Annotation rows carry raw
      JSON. }
    Message: UTF8String;
    { EvaluationLocation is the path of keywords from the root schema (for example /properties/name/type). }
    EvaluationLocation: UTF8String;
    { SchemaEvaluationLocation is the JSON pointer of the evaluated schema (or keyword) within its document. }
    SchemaEvaluationLocation: UTF8String;
    { DocumentEvaluationLocation is the JSON pointer of the instance location (for example /name). }
    DocumentEvaluationLocation: UTF8String;
  end;
  TSchemaResultArray = array of TSchemaResult;

  TResultsFrame = record
    EvalLength: Int32;
    SchemaPath: UTF8String;
    DocLength: Int32;
    CommitIndex: Int32;
    RowsStart: Int32;
  end;

  { TResultsCollector collects the results of an evaluation. A collector is used by one evaluation at a time. }
  TResultsCollector = record
    Level: TResultsLevel;
    Committed: TSchemaResultArray;
    CommittedLen: Int32;
    Frames: array of TResultsFrame;
    FramesLen: Int32;
    { The rows written into open frames (each frame owns the tail from its RowsStart). }
    Pending: TSchemaResultArray;
    PendingLen: Int32;
    EvalPath: TBytes;
    EvalPathLen: Int32;
    SchemaPath: UTF8String;
    DocPath: TBytes;
    DocPathLen: Int32;
  end;

  { TAnnotation is an annotation extracted from verbose results. }
  TAnnotation = record
    { InstanceLocation is the instance location (a JSON pointer). }
    InstanceLocation: UTF8String;
    { Keyword is the annotating keyword. }
    Keyword: UTF8String;
    { SchemaLocation is the JSON pointer of the schema object that holds the keyword. }
    SchemaLocation: UTF8String;
    { Value is the annotation value as JSON text. }
    Value: UTF8String;
  end;
  TAnnotationArray = array of TAnnotation;

  { TCollectedAnnotation is one entry of CollectAnnotations: the value of a keyword at an instance location, from
    the schema at a schema location fragment. }
  TCollectedAnnotation = record
    InstanceLocation: UTF8String;
    Keyword: UTF8String;
    { "#" followed by the schema location, percent-encoded as a URI fragment. }
    SchemaLocationFragment: UTF8String;
    Value: UTF8String;
  end;
  TCollectedAnnotationArray = array of TCollectedAnnotation;

{ ResultsLevelName returns the level's name (the String method of the Go source). }
function ResultsLevelName(L: TResultsLevel): UTF8String;

{ NewResultsCollector creates a collector at the given level. }
function NewResultsCollector(Level: TResultsLevel): TResultsCollector;
{ CollectorResultCount is the number of results. The results are Committed[0 .. CollectorResultCount-1], in commit
  order, and are valid until the collector is used again. }
function CollectorResultCount(const C: TResultsCollector): Int32; inline;
{ CollectorResults returns a copy of the results, in commit order. }
function CollectorResults(const C: TResultsCollector): TSchemaResultArray;
{ CollectorReset discards the results, so that the collector can be used for another evaluation. }
procedure CollectorReset(var C: TResultsCollector);

{ The evaluator's side. }

{ CollectorVerbose reports whether passing rows are recorded. }
function CollectorVerbose(const C: TResultsCollector): Boolean; inline;
{ CollectorWithText reports whether a row with the given result carries its message. }
function CollectorWithText(const C: TResultsCollector; IsMatch: Boolean): Boolean; inline;
{ CollectorRecords reports whether a keyword row with the given result is recorded at all. }
function CollectorRecords(const C: TResultsCollector; IsMatch: Boolean): Boolean; inline;
{ BeginChildContext opens a child context. The evaluation path is extended by EvalSegment (verbatim), the schema
  path is replaced by SchemaLocation, and the document path is extended by DocSegment (already pointer-encoded).
  HasEval and HasDoc say whether there is a segment at all. }
procedure BeginChildContext(var C: TResultsCollector; HasEval: Boolean; const EvalSegment, SchemaLocation: UTF8String;
  HasDoc: Boolean; const DocSegment: UTF8String);
{ CommitChildContext closes a child context. When the parent does not need the child's results (ParentIsMatch)
  they are discarded below Verbose. Otherwise the context's summary row is written and its rows are committed. }
procedure CommitChildContext(var C: TResultsCollector; ParentIsMatch, ChildIsMatch: Boolean;
  const Message: UTF8String);
{ PopChildContext closes a child context and discards everything it and its descendants wrote. }
procedure PopChildContext(var C: TResultsCollector);
{ EvaluatedKeyword records a keyword's result. The message is only looked at when CollectorWithText says so. }
procedure EvaluatedKeyword(var C: TResultsCollector; IsMatch: Boolean; const Message, Keyword: UTF8String);
procedure EvaluatedKeywordForProperty(var C: TResultsCollector; IsMatch: Boolean;
  const Message, PropertyName, Keyword: UTF8String);
{ IgnoredKeyword records an annotation: Verbose only. The keyword extends the evaluation path but not the schema
  path. }
procedure IgnoredKeyword(var C: TResultsCollector; const Message, Keyword: UTF8String);
procedure EvaluatedBooleanSchema(var C: TResultsCollector; IsMatch: Boolean);

{ CollectorAnnotations returns the annotations in a verbose collector's results. }
function CollectorAnnotations(const C: TResultsCollector): TAnnotationArray;
{ SchemaLocationFragment returns "#" followed by the schema location, percent-encoded as a URI fragment (upper-case
  hex, UTF-8). }
function SchemaLocationFragment(const SchemaLocation: UTF8String): UTF8String;
(* CollectAnnotations returns the annotations by instance location, keyword and schema location fragment, with the
  values as JSON text. The Go source returns a map of maps of maps ({"/name": {"title": {"#/properties/name":
  "\"Name\""}}}). Here it is the entries of those maps as a list: one entry for each instance location, keyword and
  fragment, in the order each was first seen, with the value of the last annotation that has all three. *)
function CollectAnnotations(const C: TResultsCollector): TCollectedAnnotationArray;

implementation

uses
  Corvus.JsonSchema.Uri;

function ResultsLevelName(L: TResultsLevel): UTF8String;
begin
  case L of
    Basic: Result := 'Basic';
    Detailed: Result := 'Detailed';
    Verbose: Result := 'Verbose';
  else
    Result := 'unknown';
  end;
end;

function NewResultsCollector(Level: TResultsLevel): TResultsCollector;
begin
  Result := Default(TResultsCollector);
  Result.Level := Level;
end;

function CollectorResultCount(const C: TResultsCollector): Int32; inline;
begin
  Result := C.CommittedLen;
end;

function CollectorResults(const C: TResultsCollector): TSchemaResultArray;
begin
  Result := Copy(C.Committed, 0, C.CommittedLen);
end;

procedure CollectorReset(var C: TResultsCollector);
begin
  C.Committed := nil;
  C.CommittedLen := 0;
  C.FramesLen := 0;
  C.PendingLen := 0;
  C.EvalPathLen := 0;
  C.SchemaPath := '';
  C.DocPathLen := 0;
end;

function CollectorVerbose(const C: TResultsCollector): Boolean; inline;
begin
  Result := C.Level = Verbose;
end;

function CollectorWithText(const C: TResultsCollector; IsMatch: Boolean): Boolean; inline;
begin
  Result := (C.Level = Verbose) or (not IsMatch and (C.Level >= Detailed));
end;

function CollectorRecords(const C: TResultsCollector; IsMatch: Boolean): Boolean; inline;
begin
  Result := not IsMatch or (C.Level = Verbose);
end;

{ PathText is the first Len bytes of a path as a string. }
function PathText(const Path: TBytes; Len: Int32): UTF8String;
begin
  Result := '';
  if Len = 0 then
    Exit;
  SetLength(Result, Len);
  Move(Path[0], Result[1], Len);
end;

{ PathAppend extends a path by "/" and a segment. }
procedure PathAppend(var Path: TBytes; var Len: Int32; const Segment: UTF8String);
var
  N: Int32;
begin
  N := Length(Segment);
  if Len + 1 + N > Length(Path) then
    SetLength(Path, (Len + 1 + N) * 2 + 64);
  Path[Len] := Ord('/');
  if N > 0 then
    Move(Segment[1], Path[Len + 1], N);
  Inc(Len, 1 + N);
end;

{ PushPending adds a row to the rows of the open frames. }
procedure PushPending(var C: TResultsCollector; IsMatch: Boolean; const Message, EvaluationLocation,
  SchemaEvaluationLocation, DocumentEvaluationLocation: UTF8String);
begin
  if C.PendingLen = Length(C.Pending) then
    SetLength(C.Pending, C.PendingLen * 2 + 16);
  C.Pending[C.PendingLen].IsMatch := IsMatch;
  C.Pending[C.PendingLen].Message := Message;
  C.Pending[C.PendingLen].EvaluationLocation := EvaluationLocation;
  C.Pending[C.PendingLen].SchemaEvaluationLocation := SchemaEvaluationLocation;
  C.Pending[C.PendingLen].DocumentEvaluationLocation := DocumentEvaluationLocation;
  Inc(C.PendingLen);
end;

function Text(const C: TResultsCollector; IsMatch: Boolean; const Message: UTF8String): UTF8String;
begin
  if CollectorWithText(C, IsMatch) then
    Result := Message
  else
    Result := '';
end;

procedure Restore(var C: TResultsCollector; const Frame: TResultsFrame);
begin
  C.EvalPathLen := Frame.EvalLength;
  C.SchemaPath := Frame.SchemaPath;
  C.DocPathLen := Frame.DocLength;
end;

procedure BeginChildContext(var C: TResultsCollector; HasEval: Boolean; const EvalSegment, SchemaLocation: UTF8String;
  HasDoc: Boolean; const DocSegment: UTF8String);
begin
  if C.FramesLen = Length(C.Frames) then
    SetLength(C.Frames, C.FramesLen * 2 + 16);
  C.Frames[C.FramesLen].EvalLength := C.EvalPathLen;
  C.Frames[C.FramesLen].SchemaPath := C.SchemaPath;
  C.Frames[C.FramesLen].DocLength := C.DocPathLen;
  C.Frames[C.FramesLen].CommitIndex := C.CommittedLen;
  C.Frames[C.FramesLen].RowsStart := C.PendingLen;
  Inc(C.FramesLen);
  if HasEval then
    PathAppend(C.EvalPath, C.EvalPathLen, EvalSegment);
  C.SchemaPath := SchemaLocation;
  if HasDoc then
    PathAppend(C.DocPath, C.DocPathLen, DocSegment);
end;

procedure CommitChildContext(var C: TResultsCollector; ParentIsMatch, ChildIsMatch: Boolean;
  const Message: UTF8String);
var
  Frame: TResultsFrame;
  I: Int32;
begin
  if ParentIsMatch and (C.Level <> Verbose) then begin
    PopChildContext(C);
    Exit;
  end;
  PushPending(C, ChildIsMatch, Text(C, ChildIsMatch, Message), PathText(C.EvalPath, C.EvalPathLen), C.SchemaPath,
    PathText(C.DocPath, C.DocPathLen));
  Frame := C.Frames[C.FramesLen - 1];
  Dec(C.FramesLen);
  if C.CommittedLen + (C.PendingLen - Frame.RowsStart) > Length(C.Committed) then
    SetLength(C.Committed, (C.CommittedLen + (C.PendingLen - Frame.RowsStart)) * 2 + 16);
  for I := C.PendingLen - 1 downto Frame.RowsStart do begin
    C.Committed[C.CommittedLen] := C.Pending[I];
    Inc(C.CommittedLen);
  end;
  C.PendingLen := Frame.RowsStart;
  Restore(C, Frame);
end;

procedure PopChildContext(var C: TResultsCollector);
var
  Frame: TResultsFrame;
begin
  Frame := C.Frames[C.FramesLen - 1];
  Dec(C.FramesLen);
  C.CommittedLen := Frame.CommitIndex;
  C.PendingLen := Frame.RowsStart;
  Restore(C, Frame);
end;

procedure EvaluatedKeyword(var C: TResultsCollector; IsMatch: Boolean; const Message, Keyword: UTF8String);
var
  K: UTF8String;
begin
  if CollectorRecords(C, IsMatch) then begin
    K := '/' + EscapePointerToken(Keyword);
    PushPending(C, IsMatch, Text(C, IsMatch, Message), PathText(C.EvalPath, C.EvalPathLen) + K, C.SchemaPath + K,
      PathText(C.DocPath, C.DocPathLen));
  end;
end;

procedure EvaluatedKeywordForProperty(var C: TResultsCollector; IsMatch: Boolean;
  const Message, PropertyName, Keyword: UTF8String);
var
  K: UTF8String;
begin
  if CollectorRecords(C, IsMatch) then begin
    K := '/' + EscapePointerToken(Keyword);
    PushPending(C, IsMatch, Text(C, IsMatch, Message), PathText(C.EvalPath, C.EvalPathLen) + K, C.SchemaPath + K,
      PathText(C.DocPath, C.DocPathLen) + '/' + EscapePointerToken(PropertyName));
  end;
end;

procedure IgnoredKeyword(var C: TResultsCollector; const Message, Keyword: UTF8String);
begin
  if C.Level = Verbose then
    PushPending(C, True, Message, PathText(C.EvalPath, C.EvalPathLen) + '/' + EscapePointerToken(Keyword),
      C.SchemaPath, PathText(C.DocPath, C.DocPathLen));
end;

procedure EvaluatedBooleanSchema(var C: TResultsCollector; IsMatch: Boolean);
begin
  if CollectorRecords(C, IsMatch) then
    PushPending(C, IsMatch, '', PathText(C.EvalPath, C.EvalPathLen), C.SchemaPath,
      PathText(C.DocPath, C.DocPathLen));
end;

function IsOneOf(C: Byte; const Chars: UTF8String): Boolean;
var
  K: Int32;
begin
  Result := True;
  for K := 1 to Length(Chars) do
    if Byte(Chars[K]) = C then
      Exit;
  Result := False;
end;

function CollectorAnnotations(const C: TResultsCollector): TAnnotationArray;
var
  I, K, Slash, Count: Int32;
  Keyword: UTF8String;
  First: Byte;
begin
  Result := nil;
  Count := 0;
  for I := 0 to C.CommittedLen - 1 do begin
    if not C.Committed[I].IsMatch or (C.Committed[I].Message = '') then
      Continue;
    Slash := 0;
    for K := Length(C.Committed[I].EvaluationLocation) downto 1 do
      if C.Committed[I].EvaluationLocation[K] = '/' then begin
        Slash := K;
        Break;
      end;
    if (Slash = 0) or (C.Committed[I].EvaluationLocation = C.Committed[I].SchemaEvaluationLocation) then
      Continue;
    Keyword := Copy(C.Committed[I].EvaluationLocation, Slash + 1,
      Length(C.Committed[I].EvaluationLocation) - Slash);
    First := Byte(C.Committed[I].Message[1]);
    if (Keyword = '') or not (IsOneOf(First, '"{[tfn-') or IsASCIIDigit(First)) then
      Continue;
    if Count = Length(Result) then
      SetLength(Result, Count * 2 + 8);
    Result[Count].InstanceLocation := C.Committed[I].DocumentEvaluationLocation;
    Result[Count].Keyword := Keyword;
    Result[Count].SchemaLocation := C.Committed[I].SchemaEvaluationLocation;
    Result[Count].Value := C.Committed[I].Message;
    Inc(Count);
  end;
  SetLength(Result, Count);
end;

function SchemaLocationFragment(const SchemaLocation: UTF8String): UTF8String;
const
  Hex: array[0..15] of AnsiChar = '0123456789ABCDEF';
var
  I, N: Int32;
  C: Byte;
begin
  Result := '';
  { Every byte is at most three bytes of the fragment. }
  SetLength(Result, 1 + 3 * Length(SchemaLocation));
  Result[1] := '#';
  N := 1;
  for I := 1 to Length(SchemaLocation) do begin
    C := Byte(SchemaLocation[I]);
    if IsASCIILetter(C) or IsASCIIDigit(C) or IsOneOf(C, '-._~!$&''()*+,;=:@/?') then begin
      Inc(N);
      Result[N] := AnsiChar(C);
    end else begin
      Result[N + 1] := '%';
      Result[N + 2] := Hex[C shr 4];
      Result[N + 3] := Hex[C and 15];
      Inc(N, 3);
    end;
  end;
  SetLength(Result, N);
end;

{ HashText continues a hash (FNV-1a) over the bytes of a text and a byte that ends it. The product wraps, as a
  hash's does. }
function HashText(H: UInt64; const S: UTF8String): UInt64;
const
  Prime = UInt64($100000001B3);
var
  K: Int32;
begin
  for K := 1 to Length(S) do
    H := (H xor UInt64(Byte(S[K]))) * Prime;
  Result := (H xor UInt64($FF)) * Prime;
end;

function CollectAnnotations(const C: TResultsCollector): TCollectedAnnotationArray;
var
  Annotations: TAnnotationArray;
  { The entries by the hash of their three keys, with open addressing: each slot the index + 1 of an entry, or 0.
    This is the Go source's three maps as one table. }
  Slots: array of Int32;
  I, Count, At, Size, Slot: Int32;
  Fragment: UTF8String;
begin
  Result := nil;
  Slots := nil;
  Count := 0;
  Annotations := CollectorAnnotations(C);
  Size := 16;
  while Size < 2 * Length(Annotations) do
    Size := Size shl 1;
  SetLength(Slots, Size);
  for I := 0 to Size - 1 do
    Slots[I] := 0;
  SetLength(Result, Length(Annotations));
  for I := 0 to Length(Annotations) - 1 do begin
    Fragment := SchemaLocationFragment(Annotations[I].SchemaLocation);
    Slot := Int32((HashText(HashText(HashText(UInt64($CBF29CE484222325), Annotations[I].InstanceLocation),
      Annotations[I].Keyword), Fragment) shr 32) and UInt64(Size - 1));
    At := -1;
    while Slots[Slot] <> 0 do begin
      At := Slots[Slot] - 1;
      if (Result[At].InstanceLocation = Annotations[I].InstanceLocation)
        and (Result[At].Keyword = Annotations[I].Keyword) and (Result[At].SchemaLocationFragment = Fragment) then
        Break;
      At := -1;
      Slot := (Slot + 1) and (Size - 1);
    end;
    if At < 0 then begin
      At := Count;
      Inc(Count);
      Slots[Slot] := Count;
      Result[At].InstanceLocation := Annotations[I].InstanceLocation;
      Result[At].Keyword := Annotations[I].Keyword;
      Result[At].SchemaLocationFragment := Fragment;
    end;
    Result[At].Value := Annotations[I].Value;
  end;
  SetLength(Result, Count);
end;

end.
