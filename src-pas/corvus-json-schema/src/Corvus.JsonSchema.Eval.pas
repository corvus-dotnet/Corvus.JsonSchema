unit Corvus.JsonSchema.Eval;

{$I corvus.inc}

{ Evaluation. One general evaluator serves two modes. Without a collector it fails fast at the first violation and
  reports nothing. With a collector it is exhaustive and reports every keyword with its evaluation path, schema
  location and instance location, reproducing the C# collecting mode (keyword order, paths, messages, which
  subschema results are committed or discarded). Fail-fast evaluation mostly runs through plans (see
  Corvus.JsonSchema.Plan) and comes to the general evaluator only for nodes that track evaluated properties or
  items.

  A port of eval.go of the Go module, with the evaluation halves of plan.go and fused.go: the functions of the
  three files call each other (a plan enters the fused pass and the general evaluator, and both come back to the
  plans), which in Pascal puts them in one unit. They are in the order of the Go files: eval.go's helpers, the plans
  (plan.go), the fused pass (fused.go), then the general evaluator (eval.go).

  The methods of the Go evaluator are functions that take the evaluator. A string of the instance is read where it
  lies: the array that holds its bytes (the document's source, or its text for a string that had escapes), an offset
  and a length, never a copy.

  Go keeps the buffers of an evaluation in a pool that belongs to the validator. Here each thread keeps one set, in
  a thread variable, so the validation path takes no lock. An evaluation that starts while another is running on
  the same thread (a custom format validator that itself validates) uses the same buffers above what the outer one
  has in use, and parses JSON text into a document of its own. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Checked,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Numbers,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Formats,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Compiler,
  Corvus.JsonSchema.Results,
  Corvus.JsonSchema.Plan;

const
  PropertyMapThreshold = 8;

  { The buffers a scratch keeps between validations, in bytes, beyond which it is dropped instead of kept. }
  ScratchRetainedLimit = 4 shl 20;

type
  PResultsCollector = ^TResultsCollector;

  { TScratch is the buffers of an evaluation, reused from one evaluation to the next. Each array is as long as it
    has grown, and the count beside it says how much of it is in use. }
  TScratch = record
    { The dynamic scope: the resources entered, outermost first. }
    Scope: TInt32Array;
    ScopeLen: Int32;
    { The evaluated-property and evaluated-item sets in use. }
    Arena: TUInt64Array;
    ArenaLen: Int32;
    { Scratch for uniqueItems. }
    Unique: TUInt64Array;
    { For validating JSON text: the document it is parsed into, and the parser. TextBusy says a validation of JSON
      text is using them. TextBuf holds text that is not the whole of an array of bytes (a string, or part of an
      array), which the parser reads from the start of an array. }
    Text: TDocument;
    Parser: TParser;
    TextBuf: TBytes;
    TextBusy: Boolean;
    { For content assertions: the decoded bytes, and the parser that checks them. }
    Content: TBytes;
    ContentParser: TParser;
  end;
  PScratch = ^TScratch;

  { TEvaluator is the state of one evaluation: small enough to live on the stack of the call that validates. The
    buffers an evaluation may need are in a scratch, the calling thread's, taken only when first needed. }
  TEvaluator = record
    P: PProgram;
    { The instance document. }
    D: PDocument;
    { The collector, or nil to fail fast. }
    C: PResultsCollector;
    Depth: Int32;
    { Evaluation recursed in place beyond the maximum depth. }
    DepthExceeded: Boolean;
    { The buffers, once taken, and what was in use in them when they were (by an evaluation that this one runs
      inside). }
    S: PScratch;
    ScopeBase: Int32;
    ArenaBase: Int32;
  end;

{ NewProgram is the program of a compiled schema, with its fail-fast plans. }
procedure NewProgram(const C: TCompiledSchema; const Options: TCompileOptions; var P: TProgram);
{ ComputeNodeAnnotations digests the annotation keywords of every node. The caller sees that it runs once, before
  the first evaluation with a collector (the nodeAnnotations function of the Go source, which runs it under a
  sync.Once). }
procedure ComputeNodeAnnotations(var P: TProgram);

{ AcquireScratch is the calling thread's buffers. }
function AcquireScratch: PScratch;
{ ReleaseScratch drops the buffers when they have grown beyond what is worth keeping, unless an evaluation is
  still using them. }
procedure ReleaseScratch(S: PScratch);
{ ReleaseText lets go of the instance a validation of JSON text parsed, which the caller owns, and then releases
  the scratch. }
procedure ReleaseText(S: PScratch);
{ ReleaseThreadScratch frees the calling thread's buffers. }
procedure ReleaseThreadScratch;

{ NewEvaluator is an evaluator for one evaluation of the instance D. C is the collector, or nil to fail fast. S is
  the scratch when the caller has taken it already, or nil. }
procedure NewEvaluator(out E: TEvaluator; P: PProgram; D: PDocument; C: PResultsCollector; S: PScratch); inline;
{ FinishEvaluator gives back what the evaluation took of the scratch. }
procedure FinishEvaluator(var E: TEvaluator); inline;
{ Validate evaluates the program's entry, failing fast. }
function Validate(var E: TEvaluator): Boolean;
{ Evaluate evaluates the program's entry, reporting to the collector. }
function Evaluate(var E: TEvaluator): Boolean;

{ PureRefTarget is the single reference of a node that is nothing but $ref (or a static $dynamicRef), for
  elision. }
function PureRefTarget(const N: TSchemaNode): TNodeID;
{ ForwardTarget is the branch of a node that is nothing but a one-branch allOf, for fail-fast forwarding. }
function ForwardTarget(const N: TSchemaNode): TNodeID;

implementation

uses
  Corvus.JsonSchema.Uri,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema.Values;

type
  PDocBytes = ^TBytes;
  PUInt32List = ^TUInt32Array;

  { TBitset is a set of evaluated properties (by index in the instance object) or items, held in the evaluator's
    arena. The zero offset is the arena's start, so "no set" is a negative offset. }
  TBitset = record
    Off: Int32;
    Words: Int32;
  end;

  { TFusedPass is the state of one pass. It lives on the stack of RunFused. }
  TFusedPass = record
    { The entries seen. }
    Seen: array[0..MaxFusedNames div 64 - 1] of UInt64;
    { Bit i: condition i's value test failed. }
    Failed: UInt64;
    AltFailed: array[0..MaxFusedAltGroups - 1] of UInt64;
    Holds: UInt64;
    GateOK: UInt64;
    { Once the conditions are decided: those that apply and hold, and those that apply and do not. }
    ThenMask, ElsMask: UInt64;
  end;

const
  NoBits: TBitset = (Off: -1; Words: 0);

  { The outcome of a property in the first step of a fused pass. }
  { The object fails. }
  FusedFailed = 1;
  { Some branch covered the property. }
  FusedCover = 2;
  { Conditional applications are pending. }
  FusedDefer = 4;

  { Messages (the C# evaluator's text). }
  MsgEvaluatedSubschema = 'The value was expected to match the subschema.';
  MsgMatchedAll = 'The value matched all subschema.';
  MsgDidNotMatchAll = 'The value did not match all subschema.';
  MsgMatchedAtLeastOne = 'The value matched at least one subschema.';
  MsgDidNotMatchAtLeastOne = 'The value did not match at least one subschema.';
  MsgMatchedNoSchema = 'The instance matched no schema.';
  MsgMatchedExactlyOne = 'The value matched exactly one subschema.';
  MsgMatchedMoreThanOne = 'The instance matched more than one schema.';
  MsgMatchedNot = 'The value matched the subschema in a not composition, which means the evaluation was not a '
    + 'match.';
  MsgDidNotMatchNot = 'The value did not match the subschema in a not composition, which means the evaluation was '
    + 'a match.';
  MsgMatchedIfForThen = 'The value matched the subschema in a binary or ternay if, which means the evaluation will '
    + 'go on to match the then subschema.';
  MsgMatchedIfForElse = 'The value did not match the subschema in a ternary if, which means the evaluation will go '
    + 'on to match the else subschema.';
  MsgMatchedThen = 'The value matched the then subschema corresponding to a binary or ternary if.';
  MsgDidNotMatchThen = 'The value did not match the then subschema corresponding to a binary or ternary if.';
  MsgMatchedElse = 'The value matched the else subschema corresponding to a ternary if.';
  MsgDidNotMatchElse = 'The value did not match the else subschema corresponding to a ternary if.';
  MsgUniqueItems = 'The array was expected to contain unique items.';
  MsgPropertyNameFailed = 'The property name did not match the schema.';

threadvar
  TheScratch: TScratch;

var
  { No branches (a discriminator that every branch requires, on an object without the property). }
  NoBranches: TUInt32Array;

{ ---------------------------------------------------------------------------------------------------------------------
  The program }

function HasNoAssertionsBesidesReferences(const N: TSchemaNode): Boolean;
begin
  Result := not (N.HasType or N.ConstValue.IsSet or N.HasEnum or NodeHasNumberKeywords(N)
    or NodeHasStringKeywords(N) or NodeHasObjectKeywords(N) or NodeHasArrayKeywords(N) or N.HasDynamicRef
    or N.HasAnyOf or N.HasOneOf or (N.NotNode >= 0) or (N.IfNode >= 0));
end;

function PureRefTarget(const N: TSchemaNode): TNodeID;
begin
  if ((N.Ref >= 0) = (N.StaticDynamicRef >= 0)) or N.AlwaysTrue or N.AlwaysFalse then
    Exit(NoNode);
  if N.HasAllOf or not HasNoAssertionsBesidesReferences(N) then
    Exit(NoNode);
  if N.Ref >= 0 then
    Exit(N.Ref);
  Result := N.StaticDynamicRef;
end;

function ForwardTarget(const N: TSchemaNode): TNodeID;
begin
  if (Length(N.AllOf) <> 1) or N.AlwaysTrue or N.AlwaysFalse or (N.Ref >= 0) or (N.StaticDynamicRef >= 0)
    or not HasNoAssertionsBesidesReferences(N) then
    Exit(NoNode);
  Result := N.AllOf[0];
end;

procedure NewProgram(const C: TCompiledSchema; const Options: TCompileOptions; var P: TProgram);
var
  Id, Hop, I: Int32;
  Current, Next: TNodeID;
  List: TUTF8StringArray;
begin
  P := Default(TProgram);
  P.Nodes := C.Nodes;
  P.Root := C.Root;
  P.UsesDynamicScope := C.UsesDynamicScope;
  P.MaxDepth := Options.MaxDepth;
  P.Options := Options;
  P.AnnotationSources := C.AnnotationSources;
  P.AssertFormatSet := Options.AssertFormat <> 0;
  P.Documents := C.Documents;
  P.Patterns := C.Patterns;
  SetLength(P.FastTarget, Length(P.Nodes));
  for Id := 0 to Length(P.Nodes) - 1 do begin
    Current := Id;
    for Hop := 0 to 15 do begin
      { Pure $ref hops, and forwards: a node whose only assertion is one allOf branch, unless either end is on
        an in-place cycle, which keeps its guard. }
      Next := PureRefTarget(P.Nodes[Current]);
      if Next < 0 then begin
        Next := ForwardTarget(P.Nodes[Current]);
        if (Next < 0) or P.Nodes[Current].InPlaceCycle or P.Nodes[Next].InPlaceCycle or (Next = Current) then
          Break;
      end;
      if P.UsesDynamicScope and (P.Nodes[Next].ResourceID <> P.Nodes[Current].ResourceID) then
        Break;
      Current := Next;
    end;
    P.FastTarget[Id] := Current;
  end;
  SetLength(P.HasPropertyMap, Length(P.Nodes));
  SetLength(P.PropertyMaps, Length(P.Nodes));
  for Id := 0 to Length(P.Nodes) - 1 do
    if Length(P.Nodes[Id].Properties) > PropertyMapThreshold then begin
      List := nil;
      SetLength(List, Length(P.Nodes[Id].Properties));
      for I := 0 to Length(List) - 1 do
        List[I] := P.Nodes[Id].Properties[I].Name;
      P.HasPropertyMap[Id] := True;
      P.PropertyMaps[Id] := NewNames(List);
    end;
  P.Entry := P.FastTarget[P.Root];
  CompilePlans(P);
end;

procedure ComputeNodeAnnotations(var P: TProgram);
var
  Id: Int32;
begin
  if P.AnnotationsReady then
    Exit;
  SetLength(P.Annotations, Length(P.Nodes));
  for Id := 0 to Length(P.Nodes) - 1 do
    if P.AnnotationSources[Id].IsSet then
      P.Annotations[Id] := CollectAnnotationEntries(P.Documents[P.AnnotationSources[Id].Doc],
        P.AnnotationSources[Id].Value, P.Nodes[Id].Dialect, P.AnnotationSources[Id].Vocab,
        P.AnnotationSources[Id].Content, P.AssertFormatSet);
  P.AnnotationsReady := True;
end;

{ PropertyOf is the node of a declared property of a node, or NoNode. }
function PropertyOf(const P: TProgram; Id: TNodeID; const N: TSchemaNode; const Name: TBytes;
  Start, Len: Int32): TNodeID;
var
  I: Int32;
begin
  if P.HasPropertyMap[Id] then begin
    I := NamesFind(P.PropertyMaps[Id], Name, Start, Len);
    if I >= 0 then
      Exit(N.Properties[I].Node);
    Exit(NoNode);
  end;
  for I := 0 to Length(N.Properties) - 1 do
    if TextEqualsBytes(N.Properties[I].Name, Name, Start, Len) then
      Exit(N.Properties[I].Node);
  Result := NoNode;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Messages }

function Pick(Condition: Boolean; const Yes, No: UTF8String): UTF8String;
begin
  if Condition then
    Result := Yes
  else
    Result := No;
end;

{ Quoted is " 'v'", or nothing for an empty value. }
function Quoted(const V: UTF8String): UTF8String;
begin
  if V = '' then
    Result := ''
  else
    Result := ' ''' + V + '''';
end;

function UIntText(V: UInt64): UTF8String;
begin
  Result := '';
  Str(V, Result);
end;

function DynamicKeyword(IsRecursive: Boolean): UTF8String;
begin
  if IsRecursive then
    Result := '$recursiveRef'
  else
    Result := '$dynamicRef';
end;

function TypeMessage(Mask: Byte): UTF8String;
const
  TypeOrderMask: array[0..6] of Byte = (TypeArray, TypeObject, TypeNull, TypeBoolean, TypeNumber, TypeInteger,
    TypeString);
  TypeOrderName: array[0..6] of UTF8String = ('array', 'object', 'null', 'boolean', 'number', 'integer', 'string');
var
  I, Count: Int32;
  Joined, First: UTF8String;
begin
  Count := 0;
  Joined := '';
  First := '';
  for I := 0 to 6 do
    if Mask and TypeOrderMask[I] <> 0 then begin
      if Count = 0 then
        First := TypeOrderName[I]
      else
        Joined := Joined + '", "';
      Joined := Joined + TypeOrderName[I];
      Inc(Count);
    end;
  case Count of
    0: Result := '';
    1: Result := 'The value was expected to be of type ''' + First + '''';
  else
    Result := 'The value was expected to be of type ''["' + Joined + '"]''';
  end;
end;

{ NumberText is a number's text as written. }
function NumberText(const D: TDocument; N: Int32): UTF8String;
var
  Start, Stop: Int32;
begin
  Result := '';
  Start := DocNumberStart(D, N);
  Stop := DocNumberEnd(D, N);
  if Stop > Start then begin
    SetLength(Result, Stop - Start);
    Move(D.Source[Start], Result[1], Stop - Start);
  end;
end;

function ConstMessage(const D: TDocument; N: Int32): UTF8String;
begin
  case DocKind(D, N) of
    KindString: Result := 'Expected the value to be the string' + Quoted(DocStrCopy(D, N));
    KindNumber: Result := 'The value was expected to be equal to' + Quoted(NumberText(D, N));
    KindBool:
      if DocBoolean(D, N) then
        Result := 'Expected the value to be ''true'''
      else
        Result := 'Expected the value to be ''false''';
    KindNull: Result := 'Expected the value to be ''null''';
  else
    Result := '';
  end;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Reading the instance }

{ StrBytes is the array that holds the bytes of a string value: the document's text for a string that had escapes,
  or its source. The bytes are the DocCount bytes from DocStrOffset. }
function StrBytes(D: PDocument; N: NativeInt): PDocBytes; inline;
begin
  if WordAt(D^.Tape, N shl 1) and (StrText shl 8) <> 0 then
    Result := @D^.Text
  else
    Result := @D^.Source;
end;

{ HeaderBytes is StrBytes for a string value whose header word has been read already (see DocHeader). }
function HeaderBytes(D: PDocument; H: UInt64): PDocBytes; inline;
begin
  if HeaderStrInText(H) then
    Result := @D^.Text
  else
    Result := @D^.Source;
end;

{ CodePoints is the length of a string value in code points (what minLength and maxLength count). }
function CodePoints(D: PDocument; X: NativeInt): UInt64;
var
  Src: PDocBytes;
  I, Off, Len: NativeInt;
begin
  Len := DocCount(D^, X);
  if DocStrASCII(D^, X) then
    Exit(UInt64(Len));
  Src := StrBytes(D, X);
  Off := DocStrOffset(D^, X);
  Result := 0;
  { The text is valid UTF-8, so a code point is a byte that does not continue one. }
  for I := Off to Off + Len - 1 do
    if Src^[I] and $C0 <> $80 then
      Inc(Result);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The scratch }

function AcquireScratch: PScratch;
begin
  Result := @TheScratch;
end;

procedure ReleaseScratch(S: PScratch);
begin
  if S^.TextBusy or (S^.ScopeLen <> 0) or (S^.ArenaLen <> 0) then
    Exit;
  if ParserRetainsTooMuch(S^.Parser)
    or (8 * (Int64(Length(S^.Text.Tape)) + Length(S^.Arena) + Length(S^.Unique)) + Length(S^.Text.Text)
      + Length(S^.TextBuf) > ScratchRetainedLimit) then
    S^ := Default(TScratch);
end;

procedure ReleaseText(S: PScratch);
begin
  { Let go of the instance, which the caller owns. }
  S^.Text.Source := nil;
  S^.TextBusy := False;
  ReleaseScratch(S);
end;

procedure ReleaseThreadScratch;
var
  S: PScratch;
begin
  S := @TheScratch;
  S^ := Default(TScratch);
end;

procedure NewEvaluator(out E: TEvaluator; P: PProgram; D: PDocument; C: PResultsCollector; S: PScratch); inline;
begin
  E.P := P;
  E.D := D;
  E.C := C;
  E.Depth := 0;
  E.DepthExceeded := False;
  E.S := S;
  E.ScopeBase := 0;
  E.ArenaBase := 0;
  if S <> nil then begin
    E.ScopeBase := S^.ScopeLen;
    E.ArenaBase := S^.ArenaLen;
  end;
end;

procedure FinishEvaluator(var E: TEvaluator); inline;
begin
  if E.S <> nil then begin
    E.S^.ScopeLen := E.ScopeBase;
    E.S^.ArenaLen := E.ArenaBase;
  end;
end;

procedure TakeScratch(var E: TEvaluator);
begin
  E.S := @TheScratch;
  E.ScopeBase := E.S^.ScopeLen;
  E.ArenaBase := E.S^.ArenaLen;
end;

{ State is the evaluation's buffers, taken on first use. }
function State(var E: TEvaluator): PScratch; inline;
begin
  if E.S = nil then
    TakeScratch(E);
  Result := E.S;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Evaluated properties and items }

function Tracked(const B: TBitset): Boolean; inline;
begin
  Result := B.Off >= 0;
end;

{ NewBits takes a cleared set for Length members from the arena. Sets are released in the reverse order. }
function NewBits(var E: TEvaluator; Length: NativeInt): TBitset;
var
  S: PScratch;
  Words, Off, Stop, I: NativeInt;
begin
  S := State(E);
  Words := (Length + 63) shr 6;
  if Words = 0 then
    Words := 1;
  Off := S^.ArenaLen;
  Stop := Off + Words;
  if Stop > System.Length(S^.Arena) then
    SetLength(S^.Arena, 2 * Stop + 16);
  S^.ArenaLen := Stop;
  for I := Off to Stop - 1 do
    WordPtrAt(S^.Arena, I)^ := 0;
  Result.Off := Off;
  Result.Words := Words;
end;

{ The functions below are only given sets that NewBits returned, so the buffers are there. }

procedure FreeBits(var E: TEvaluator; const B: TBitset); inline;
begin
  E.S^.ArenaLen := B.Off;
end;

procedure SetBit(var E: TEvaluator; const B: TBitset; I: NativeInt); inline;
var
  W: PUInt64;
begin
  W := WordPtrAt(E.S^.Arena, B.Off + I shr 6);
  W^ := W^ or (One64 shl (I and 63));
end;

function GetBit(var E: TEvaluator; const B: TBitset; I: NativeInt): Boolean; inline;
begin
  Result := WordAt(E.S^.Arena, B.Off + I shr 6) and (One64 shl (I and 63)) <> 0;
end;

procedure MergeBits(var E: TEvaluator; const Into, From: TBitset);
var
  I: NativeInt;
begin
  for I := 0 to From.Words - 1 do
    E.S^.Arena[Into.Off + I] := E.S^.Arena[Into.Off + I] or E.S^.Arena[From.Off + I];
end;

procedure ClearBits(var E: TEvaluator; const B: TBitset);
var
  I: NativeInt;
begin
  for I := B.Off to B.Off + B.Words - 1 do
    E.S^.Arena[I] := 0;
end;

procedure CopyBits(var E: TEvaluator; const Into, From: TBitset);
var
  I: NativeInt;
begin
  for I := 0 to From.Words - 1 do
    E.S^.Arena[Into.Off + I] := E.S^.Arena[From.Off + I];
end;

{ PushScope enters a resource in the dynamic scope, unless it is the innermost one already. It reports whether it
  did. }
function PushScope(var E: TEvaluator; Resource: NativeInt): Boolean;
var
  S: PScratch;
begin
  S := State(E);
  if (S^.ScopeLen > E.ScopeBase) and (S^.Scope[S^.ScopeLen - 1] = Resource) then
    Exit(False);
  if S^.ScopeLen = Length(S^.Scope) then
    SetLength(S^.Scope, 2 * S^.ScopeLen + 16);
  S^.Scope[S^.ScopeLen] := Resource;
  Inc(S^.ScopeLen);
  Result := True;
end;

procedure PopScope(var E: TEvaluator); inline;
begin
  Dec(E.S^.ScopeLen);
end;

function ResolveDynamic(var E: TEvaluator; const D: TDynamicRefTarget): TNodeID;
var
  S: PScratch;
  K, I: NativeInt;
begin
  S := State(E);
  for K := E.ScopeBase to S^.ScopeLen - 1 do
    for I := 0 to Length(D.ByResource) - 1 do
      if D.ByResource[I].Resource = S^.Scope[K] then
        Exit(D.ByResource[I].Node);
  Result := D.Fallback;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The plans (the evaluation half of plan.go) }

function EnterChild(var E: TEvaluator; const C: TChild; X: NativeInt): Boolean; forward;
function RunBody(var E: TEvaluator; B: PBody; X: NativeInt): Boolean; forward;
function RunKeywords(var E: TEvaluator; B: PBody; X: NativeInt): Boolean; forward;
function RunApply(var E: TEvaluator; const Ops: TOpArray; X: NativeInt): Boolean; forward;
function RunObject(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean; forward;
function RunStrictObject(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean; forward;
function RunArray(var E: TEvaluator; Pl: PArrayPlan; X: NativeInt): Boolean; forward;
function RunLeaf(var E: TEvaluator; B: PBody; X: NativeInt): Boolean; forward;
function RunNumber(var E: TEvaluator; const Ops: TNumberOpArray; X: NativeInt): Boolean; forward;
function RunString(var E: TEvaluator; const Ops: TStringOpArray; X: NativeInt): Boolean; forward;
function RunFused(var E: TEvaluator; F: PFusedObject; X: NativeInt): Boolean; forward;
function ObjectVisitLookup(var E: TEvaluator; Pl: PObjectPlan; First, Count: NativeInt; out Seen: UInt64): Boolean;
  forward;
function ObjectVisitNames(var E: TEvaluator; Pl: PObjectPlan; First, Count: NativeInt; out Seen: UInt64): Boolean;
  forward;
function EvalNode(var E: TEvaluator; Id: TNodeID; X: Int32; const Bits: TBitset): Boolean; forward;
function ContentOK(var E: TEvaluator; Src: PDocBytes; Off, Len: Int32; Kind: TContentKind): Boolean; forward;

{ Abandon gives back what an evaluation took of the thread's buffers when an exception ends it. The Go source
  needs no such thing: a scratch that is not put back in the pool is dropped. Here the buffers are the thread's, so
  an evaluation that ends without giving them back would leave every later one on the thread above what it left
  in use, and every later validation of JSON text parsing into a document of its own. }
procedure Abandon(var E: TEvaluator);
begin
  if E.S = nil then
    Exit;
  E.S^.ScopeLen := E.ScopeBase;
  E.S^.ArenaLen := E.ArenaBase;
  { The evaluation of JSON text, whose instance is the thread's document. }
  if E.D = PDocument(@E.S^.Text) then begin
    E.S^.Text.Source := nil;
    E.S^.TextBusy := False;
  end;
end;

{ CallCustom calls a custom format validator, which is the caller's code. It must not raise an exception. If it
  does, the evaluation is abandoned and the exception goes on to the caller of the validator. }
function CallCustom(var E: TEvaluator; Custom: TFormatValidator; const B: TBytes; Start, Len: NativeInt): Boolean;
begin
  try
    Result := Custom(B, Start, Len);
  except
    Abandon(E);
    raise;
  end;
end;

function CheckString(var E: TEvaluator; const F: TFormatCheckOp; const B: TBytes; Start, Len: NativeInt): Boolean;
begin
  if Assigned(F.Custom) then
    Result := CallCustom(E, F.Custom, B, Start, Len)
  else
    Result := FormatCheckString(F.Kind, B, Start, Len, F.Legacy);
end;

function CheckNumber(var E: TEvaluator; const F: TFormatCheckOp; const D: TDocument; X: NativeInt): Boolean;
var
  Start: NativeInt;
begin
  if Assigned(F.Custom) then begin
    Start := DocNumberStart(D, X);
    Result := CallCustom(E, F.Custom, D.Source, Start, DocNumberEnd(D, X) - Start);
  end else
    Result := FormatCheckNumber(F.Kind, DocFlags(D, X), DocData(D, X));
end;

{ SelectIndexed is the branches a discriminator value selects. }
function SelectIndexed(const P: TProgram; const B: TBranches; D: PDocument; V: NativeInt): PUInt32List;
var
  I, K: NativeInt;
begin
  if DocKind(D^, V) = KindString then begin
    I := NamesFind(B.Index.Strings, StrBytes(D, V)^, DocStrOffset(D^, V), DocCount(D^, V));
    if I >= 0 then
      Exit(@B.Discriminator.Known[B.Index.StringEntries[I]].Branches);
    Exit(@B.Discriminator.Unknown);
  end;
  for K := 0 to Length(B.Index.Others) - 1 do begin
    I := B.Index.Others[K];
    if DiscriminatorValueMatches(B.Discriminator.Known[I].Value, P.Documents, D^, V) then
      Exit(@B.Discriminator.Known[I].Branches);
  end;
  Result := @B.Discriminator.Unknown;
end;

{ Run evaluates a node's plan (at a new instance location, or where no depth guard applies). }
function Run(var E: TEvaluator; Id: TNodeID; X: NativeInt): Boolean; inline;
begin
  Result := EnterChild(E, PlanAt(E.P^.Plans, Id)^.Self, X);
end;

{ RunChild evaluates a child at a new instance location. A child that is only a type test the value passes is
  decided here, in the caller once this is inlined, and anything else is one call. }
function RunChild(var E: TEvaluator; const C: TChild; X: NativeInt): Boolean; inline;
begin
  Result := (C.Pass and Byte(WordAt(E.D^.Tape, X shl 1) and $FF) <> 0) or EnterChild(E, C, X);
end;

{ EnterChild is RunChild past its inlined test: the type test in full, then the child's keywords by its shape. An
  object for a strict object plan has its loop called from here, with no function in between, since that is what
  most values with keywords are. }
function EnterChild(var E: TEvaluator; const C: TChild; X: NativeInt): Boolean;
var
  D: PDocument;
  Header, Seen: UInt64;
  Kind: Byte;
  Pl: PPlan;
  B: PBody;
  Op: PObjectPlan;
  Count: NativeInt;
  Ok: Boolean;
begin
  D := E.D;
  { The value's header is read once here, and what the loops need of it is handed to them. }
  Header := WordAt(D^.Tape, X shl 1);
  Kind := Byte(Header and $FF);
  if (C.Types and Kind = 0) and not IntegerOK(C.Types, D^, X) then
    Exit(False);
  if C.Shape = ShapeTrivial then
    Exit(True);
  Pl := PlanAt(E.P^.Plans, C.Id);
  if not Pl^.HasBody then
    Exit(True);
  B := @Pl^.Body;
  case C.Shape of
    ShapeLeaf:
      Exit(RunLeaf(E, B, X));
    ShapeStringEnum:
      Exit((Kind = KindString)
        and (NamesFind(B^.Values[0].Names, HeaderBytes(D, Header)^, DocStrOffset(D^, X), HeaderCount(Header)) >= 0));
    ShapeStrings:
      Exit((Kind <> KindString) or RunString(E, B^.Str, X));
    ShapeObject: begin
      if Kind <> KindObject then
        Exit(True);
      Op := @B^.ObjectPlan;
      if not Op^.IsStrict then
        Exit(RunObject(E, Op, X));
    end;
    ShapeFused: begin
      if Kind <> KindObject then
        Exit(RunKeywords(E, B, X));
      if not B^.Fused.HasFlat then
        Exit(RunFused(E, @B^.Fused, X));
      Op := @B^.Fused.Flat;
    end;
    ShapeArray:
      Exit((Kind <> KindArray) or RunArray(E, @B^.ArrayPlan, X));
    ShapeApply:
      Exit(RunApply(E, B^.Apply, X));
  else
    Exit(RunBody(E, B, X));
  end;
  { The strict loop (RunStrictObject). }
  Count := Int32(Header shr 32);
  if (UInt64(Count) < Op^.Min) or (UInt64(Count) > Op^.Max) then
    Exit(False);
  if Op^.Lookup and (Count * NamesLen(Op^.Names) <= LookupBudget) then
    Ok := ObjectVisitLookup(E, Op, DocFirst(D^, X), Count, Seen)
  else
    Ok := ObjectVisitNames(E, Op, DocFirst(D^, X), Count, Seen);
  Result := Ok and (Seen and Op^.RequiredMask = Op^.RequiredMask);
end;

{ Enter evaluates a body by its shape (its types already tested, and not on an in-place cycle unless general). }
function Enter(var E: TEvaluator; S: TShape; B: PBody; X: NativeInt): Boolean;
var
  D: PDocument;
begin
  D := E.D;
  case S of
    ShapeLeaf:
      Exit(RunLeaf(E, B, X));
    ShapeStringEnum:
      Exit((DocKind(D^, X) = KindString)
        and (NamesFind(B^.Values[0].Names, StrBytes(D, X)^, DocStrOffset(D^, X), DocCount(D^, X)) >= 0));
    ShapeStrings:
      Exit((DocKind(D^, X) <> KindString) or RunString(E, B^.Str, X));
    ShapeObject: begin
      if DocKind(D^, X) <> KindObject then
        Exit(True);
      if B^.ObjectPlan.IsStrict then
        Exit(RunStrictObject(E, @B^.ObjectPlan, X));
      Exit(RunObject(E, @B^.ObjectPlan, X));
    end;
    ShapeFused: begin
      if DocKind(D^, X) <> KindObject then
        Exit(RunKeywords(E, B, X));
      Exit(RunFused(E, @B^.Fused, X));
    end;
    ShapeArray:
      Exit((DocKind(D^, X) <> KindArray) or RunArray(E, @B^.ArrayPlan, X));
    ShapeApply:
      Exit(RunApply(E, B^.Apply, X));
  else
  end;
  Result := RunBody(E, B, X);
end;

{ RunInPlace evaluates an in-place child under the depth guard. }
function RunInPlace(var E: TEvaluator; Id: TNodeID; X: NativeInt): Boolean;
begin
  if not PlanAt(E.P^.Plans, Id)^.Guard then
    Exit(Run(E, Id, X));
  Inc(E.Depth);
  if E.Depth > E.P^.MaxDepth then begin
    E.DepthExceeded := True;
    Dec(E.Depth);
    Exit(False);
  end;
  Result := Run(E, Id, X);
  Dec(E.Depth);
end;

{ RunBranch evaluates an in-place child with its type check inline, then its keywords under the depth guard
  (without testing the type again). }
function RunBranch(var E: TEvaluator; const C: TChild; X: NativeInt): Boolean;
var
  Pl: PPlan;
begin
  if (C.Types <> AnyType) and not TypeOK(C.Types, E.D^, X) then
    Exit(False);
  if C.Shape = ShapeTrivial then
    Exit(True);
  Pl := PlanAt(E.P^.Plans, C.Id);
  if not Pl^.HasBody or Pl^.Guard then
    Exit(RunInPlace(E, C.Id, X));
  Result := Enter(E, C.Shape, @Pl^.Body, X);
end;

{ Candidates are the anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the
  instance type. }
function Candidates(var E: TEvaluator; const B: TBranches; X: NativeInt): PUInt32List;
var
  D: PDocument;
  Kind: Byte;
  V: NativeInt;
begin
  D := E.D;
  Kind := DocKind(D^, X);
  if not B.HasDiscriminator or (Kind <> KindObject) then
    Exit(@B.ByKind[KindIndex(Kind)]);
  V := DocProperty(D^, X, B.Discriminator.PropertyName);
  if V >= 0 then
    Exit(SelectIndexed(E.P^, B, D, V));
  if B.Discriminator.AllRequire then
    Exit(@NoBranches);
  Result := @B.ByKind[KindIndex(Kind)];
end;

{ RunBody evaluates a node's keywords. Where the program keeps a dynamic scope, entering a node of another resource
  pushes that resource (as the general evaluator does), for the dynamic references below it. }
function RunBody(var E: TEvaluator; B: PBody; X: NativeInt): Boolean;
var
  Pushed: Boolean;
begin
  if not E.P^.UsesDynamicScope then
    Exit(RunKeywords(E, B, X));
  Pushed := PushScope(E, E.P^.Nodes[B^.Node].ResourceID);
  Result := RunKeywords(E, B, X);
  if Pushed then
    PopScope(E);
end;

function EnumContains(var E: TEvaluator; const Values: TValueRefArray; X: NativeInt): Boolean;
var
  I: NativeInt;
begin
  for I := 0 to Length(Values) - 1 do
    if ValuesEqual(E.D^, X, E.P^.Documents[Values[I].Doc], Values[I].N) then
      Exit(True);
  Result := False;
end;

function RunOp(var E: TEvaluator; O: POp; X: NativeInt): Boolean;
var
  D: PDocument;
  List: PUInt32List;
  I, Matched: NativeInt;
  Next: TNodeID;
begin
  D := E.D;
  case O^.Kind of
    OpConst:
      Result := ValuesEqual(D^, X, E.P^.Documents[O^.Value.Doc], O^.Value.N);
    OpEnumStrings:
      Result := (DocKind(D^, X) = KindString)
        and (NamesFind(O^.Names, StrBytes(D, X)^, DocStrOffset(D^, X), DocCount(D^, X)) >= 0);
    OpEnum:
      Result := EnumContains(E, O^.Values, X);
    OpRef:
      Result := RunInPlace(E, O^.Node, X);
    OpAllOf: begin
      for I := 0 to Length(O^.Children) - 1 do
        if not RunBranch(E, ChildAt(O^.Children, I)^, X) then
          Exit(False);
      Result := True;
    end;
    OpAnyOf: begin
      List := Candidates(E, O^.Branches, X);
      for I := 0 to Length(List^) - 1 do
        if RunBranch(E, ChildAt(O^.Branches.Children, Int32(UInt32At(List^, I)))^, X) then
          Exit(True);
      Result := False;
    end;
    OpOneOf: begin
      List := Candidates(E, O^.Branches, X);
      { The instance's type (or the discriminator) leaves one branch: that branch decides. }
      if Length(List^) = 1 then
        Exit(RunBranch(E, ChildAt(O^.Branches.Children, Int32(UInt32At(List^, 0)))^, X));
      Matched := 0;
      for I := 0 to Length(List^) - 1 do
        if RunBranch(E, ChildAt(O^.Branches.Children, Int32(UInt32At(List^, I)))^, X) then begin
          Inc(Matched);
          if Matched > 1 then
            Break;
        end;
      Result := Matched = 1;
    end;
    OpNot:
      { Under the depth guard, like every other in-place applicator: a not can be part of a cycle too. }
      Result := not RunInPlace(E, O^.Node, X);
    OpDynamicRef:
      Result := RunInPlace(E, E.P^.FastTarget[ResolveDynamic(E, O^.Dynamic)], X);
  else
    Next := O^.ElseNode;
    if RunInPlace(E, O^.Node, X) then
      Next := O^.ThenNode;
    Result := (Next < 0) or RunInPlace(E, Next, X);
  end;
end;

function RunKeywords(var E: TEvaluator; B: PBody; X: NativeInt): Boolean;
var
  D: PDocument;
  Kind: Byte;
  I, First: NativeInt;
begin
  D := E.D;
  Kind := DocKind(D^, X);
  case Kind of
    KindObject: begin
      if B^.HasFused then
        Exit(RunFused(E, @B^.Fused, X));
      if B^.General and TypeObject <> 0 then
        Exit(EvalNode(E, B^.Node, X, NoBits));
    end;
    KindArray:
      if B^.General and TypeArray <> 0 then
        Exit(EvalNode(E, B^.Node, X, NoBits));
  else
  end;
  for I := 0 to Length(B^.Values) - 1 do
    if not RunOp(E, OpAt(B^.Values, I), X) then
      Exit(False);
  case Kind of
    KindNumber:
      if (Length(B^.Number) <> 0) and not RunNumber(E, B^.Number, X) then
        Exit(False);
    KindString:
      if (Length(B^.Str) <> 0) and not RunString(E, B^.Str, X) then
        Exit(False);
    KindObject:
      if B^.HasObject and not RunObject(E, @B^.ObjectPlan, X) then
        Exit(False);
    KindArray: begin
      if B^.HasArray and not RunArray(E, @B^.ArrayPlan, X) then
        Exit(False);
      if B^.HasUnevaluatedItems then begin
        First := DocFirst(D^, X);
        for I := B^.UnevaluatedFrom to DocCount(D^, X) - 1 do
          if not RunChild(E, B^.UnevaluatedChild, First + I) then
            Exit(False);
      end;
    end;
  else
  end;
  Result := (Length(B^.Apply) = 0) or RunApply(E, B^.Apply, X);
end;

function RunApply(var E: TEvaluator; const Ops: TOpArray; X: NativeInt): Boolean;
var
  I: NativeInt;
begin
  for I := 0 to Length(Ops) - 1 do
    if not RunOp(E, OpAt(Ops, I), X) then
      Exit(False);
  Result := True;
end;

{ ObjectRest checks the required names checked by lookup, and the dependencies. }
function ObjectRest(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt; Seen: UInt64): Boolean;
var
  D: PDocument;
  I, J: NativeInt;
begin
  D := E.D;
  for I := 0 to Length(Pl^.Required) - 1 do
    if DocProperty(D^, X, Pl^.Required[I]) < 0 then
      Exit(False);
  for I := 0 to Length(Pl^.Dependencies) - 1 do begin
    if Pl^.Dependencies[I].HasBits then begin
      if Seen and Pl^.Dependencies[I].NameBit = 0 then
        Continue;
      if Seen and Pl^.Dependencies[I].RequiredBits <> Pl^.Dependencies[I].RequiredBits then
        Exit(False);
    end else begin
      if DocProperty(D^, X, Pl^.Dependencies[I].Name) < 0 then
        Continue;
      for J := 0 to Length(Pl^.Dependencies[I].Required) - 1 do
        if DocProperty(D^, X, Pl^.Dependencies[I].Required[J]) < 0 then
          Exit(False);
    end;
    if (Pl^.Dependencies[I].Schema >= 0) and not RunInPlace(E, Pl^.Dependencies[I].Schema, X) then
      Exit(False);
  end;
  Result := True;
end;

{ ObjectVisitValues is for only additionalProperties: every value against one child. }
function ObjectVisitValues(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean;
var
  D: PDocument;
  C: TChild;
  First, Count, I, V: NativeInt;
begin
  D := E.D;
  C := Pl^.Additional;
  First := DocFirst(D^, X);
  Count := DocCount(D^, X);
  if C.Shape = ShapeTrivial then begin
    if C.Types = AnyType then
      Exit(True);
    { The values' headers are every fourth word of the properties' run of the tape. }
    for I := 0 to Count - 1 do begin
      V := First + 2 * I + 1;
      if (C.Types and Byte(WordAt(D^.Tape, V shl 1) and $FF) = 0) and not IntegerOK(C.Types, D^, V) then
        Exit(False);
    end;
    Exit(True);
  end;
  for I := 0 to Count - 1 do
    if not RunChild(E, C, First + 2 * I + 1) then
      Exit(False);
  Result := True;
end;

{ ObjectVisitGeneral is the general property loop: declared names, patterns, additionalProperties and propertyNames. }
function ObjectVisitGeneral(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt; out Seen: UInt64): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  Pc: PPatternChild;
  Bits, W, H: UInt64;
  Hint, K, Stop, I, J, Off, Len: NativeInt;
  Matched: Boolean;
begin
  D := E.D;
  Seen := 0;
  Bits := 0;
  Hint := 0;
  K := DocFirst(D^, X);
  Stop := K + 2 * DocCount(D^, X);
  while K < Stop do begin
    { The name's header is read once, as the Go source's str reads it. }
    H := DocHeader(D^, K);
    Src := HeaderBytes(D, H);
    Off := DocStrOffset(D^, K);
    Len := HeaderCount(H);
    Matched := False;
    W := NameWord(Src^, Off, Len);
    if NamesAt(Pl^.Names, Hint, Len, W) and ((Len <= 8) or NameMapRest(Pl^.Names.M, Hint, Src^, Off, Len)) then begin
      I := Hint;
      Inc(Hint);
    end else
      I := NamesFindAfter(Pl^.Names, Src^, Off, Len, W, Hint);
    if I >= 0 then begin
      Bits := Bits or (One64 shl (I and 63));
      { A name only required (or a dependency) mentions is undeclared: patterns, else additionalProperties. }
      if I < Pl^.Declared then begin
        Matched := True;
        if not RunChild(E, ChildAt(Pl^.Children, I)^, K + 1) then
          Exit(False);
      end;
      for J := 0 to Length(Pl^.NamePatterns[I]) - 1 do begin
        Matched := True;
        if not RunChild(E, PatternChildAt(Pl^.Patterns, UInt16At(Pl^.NamePatterns[I], J))^.Child, K + 1) then
          Exit(False);
      end;
    end else
      for J := 0 to Length(Pl^.Patterns) - 1 do begin
        Pc := PatternChildAt(Pl^.Patterns, J);
        if PatternMatch(PatternAt(E.P^.Patterns, Pc^.Pattern)^, Src^, Off, Len, HeaderStrASCII(H)) then begin
          Matched := True;
          if not RunChild(E, Pc^.Child, K + 1) then
            Exit(False);
        end;
      end;
    if not Matched and Pl^.HasAdditional and not RunChild(E, Pl^.Additional, K + 1) then
      Exit(False);
    { The name is a string value of the document: it is evaluated where it is. }
    if (Pl^.PropertyNames >= 0) and not Run(E, Pl^.PropertyNames, K) then
      Exit(False);
    Inc(K, 2);
  end;
  Seen := Bits;
  Result := True;
end;

function ObjectVisitPattern(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean;
var
  D: PDocument;
  Pc: PPatternChild;
  H: UInt64;
  K, Stop: NativeInt;
begin
  D := E.D;
  K := DocFirst(D^, X);
  Stop := K + 2 * DocCount(D^, X);
  while K < Stop do begin
    Pc := PatternChildAt(Pl^.Patterns, 0);
    H := DocHeader(D^, K);
    if PatternMatch(PatternAt(E.P^.Patterns, Pc^.Pattern)^, HeaderBytes(D, H)^, DocStrOffset(D^, K), HeaderCount(H),
      HeaderStrASCII(H)) then begin
      if not RunChild(E, Pc^.Child, K + 1) then
        Exit(False);
    end else if Pl^.HasAdditional and not RunChild(E, Pl^.Additional, K + 1) then
      Exit(False);
    Inc(K, 2);
  end;
  Result := True;
end;

{ RunObject evaluates an object plan: the size bounds, the property loop for its shape, then the required names and
  dependencies. }
function RunObject(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean;
var
  Count: NativeInt;
  Seen: UInt64;
  Ok: Boolean;
begin
  Count := DocCount(E.D^, X);
  if (UInt64(Count) < Pl^.Min) or (UInt64(Count) > Pl^.Max) then
    Exit(False);
  Seen := 0;
  Ok := True;
  case Pl^.Visit of
    VisitValues:
      Ok := ObjectVisitValues(E, Pl, X);
    VisitNames:
      if Pl^.Lookup and (Count * NamesLen(Pl^.Names) <= LookupBudget) then
        Ok := ObjectVisitLookup(E, Pl, DocFirst(E.D^, X), Count, Seen)
      else
        Ok := ObjectVisitNames(E, Pl, DocFirst(E.D^, X), Count, Seen);
    VisitPattern:
      Ok := ObjectVisitPattern(E, Pl, X);
    VisitGeneral:
      Ok := ObjectVisitGeneral(E, Pl, X, Seen);
  else
  end;
  if not Ok or (Seen and Pl^.RequiredMask <> Pl^.RequiredMask) then
    Exit(False);
  Result := Pl^.RestFree or ObjectRest(E, Pl, X, Seen);
end;

{ RunStrictObject is the strict loop: bounds, declared names (additionalProperties for the rest), and the required
  mask. A small function of its own, since nested objects enter it directly. }
function RunStrictObject(var E: TEvaluator; Pl: PObjectPlan; X: NativeInt): Boolean;
var
  D: PDocument;
  Count: NativeInt;
  Seen: UInt64;
  Ok: Boolean;
begin
  D := E.D;
  Count := DocCount(D^, X);
  if (UInt64(Count) < Pl^.Min) or (UInt64(Count) > Pl^.Max) then
    Exit(False);
  if Pl^.Lookup and (Count * NamesLen(Pl^.Names) <= LookupBudget) then
    Ok := ObjectVisitLookup(E, Pl, DocFirst(D^, X), Count, Seen)
  else
    Ok := ObjectVisitNames(E, Pl, DocFirst(D^, X), Count, Seen);
  Result := Ok and (Seen and Pl^.RequiredMask = Pl^.RequiredMask);
end;

{ ObjectVisitLookup looks each name up in the object, whose Count properties start at First (a plan with Lookup: the
  other properties need no visit). It gives the names seen. The search for a name reads the lengths of the property
  names first, which are in the headers of the tape. }
function ObjectVisitLookup(var E: TEvaluator; Pl: PObjectPlan; First, Count: NativeInt; out Seen: UInt64): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  C: PChild;
  Bits, H: UInt64;
  I, J, K, Len, Off: NativeInt;
begin
  D := E.D;
  Seen := 0;
  Bits := 0;
  for I := 0 to Length(Pl^.Names.M.Keys) - 1 do begin
    Len := NameKeyAt(Pl^.Names.M.Keys, I)^.Length;
    for J := 0 to Count - 1 do begin
      K := First + 2 * J;
      H := DocHeader(D^, K);
      if HeaderCount(H) <> Len then
        Continue;
      Src := HeaderBytes(D, H);
      Off := DocStrOffset(D^, K);
      if (NameWord(Src^, Off, Len) <> WordAt(Pl^.Names.M.Words, I))
        or ((Len > 8) and not TextEqualsBytes(TextAt(Pl^.Names.M.Names, I)^, Src^, Off, Len)) then
        Continue;
      Bits := Bits or (One64 shl I);
      C := ChildAt(Pl^.Children, I);
      if (C^.Pass and Byte(WordAt(D^.Tape, (K + 1) shl 1) and $FF) = 0) and not EnterChild(E, C^, K + 1) then
        Exit(False);
      Break;
    end;
  end;
  Seen := Bits;
  Result := True;
end;

{ ObjectVisitNames is for declared properties, and additionalProperties for the rest, over the Count properties that
  start at First. It gives the declared names seen. }
function ObjectVisitNames(var E: TEvaluator; Pl: PObjectPlan; First, Count: NativeInt; out Seen: UInt64): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  Bits, W, H: UInt64;
  Hint, K, Stop, I, Off, Len: NativeInt;
begin
  D := E.D;
  Seen := 0;
  Bits := 0;
  Hint := 0;
  K := First;
  Stop := K + 2 * Count;
  while K < Stop do begin
    H := DocHeader(D^, K);
    Src := HeaderBytes(D, H);
    Off := DocStrOffset(D^, K);
    Len := HeaderCount(H);
    W := NameWord(Src^, Off, Len);
    if NamesAt(Pl^.Names, Hint, Len, W) and ((Len <= 8) or NameMapRest(Pl^.Names.M, Hint, Src^, Off, Len)) then begin
      I := Hint;
      Inc(Hint);
    end else
      I := NamesFindAfter(Pl^.Names, Src^, Off, Len, W, Hint);
    if I >= 0 then begin
      Bits := Bits or (One64 shl (I and 63));
      if not RunChild(E, ChildAt(Pl^.Children, I)^, K + 1) then
        Exit(False);
    end else if Pl^.HasAdditional and not RunChild(E, Pl^.Additional, K + 1) then
      Exit(False);
    Inc(K, 2);
  end;
  Seen := Bits;
  Result := True;
end;

{ AllOfType reports whether each of Count consecutive values is of the types in a mask. }
function AllOfType(D: PDocument; First, Count: NativeInt; Types: Byte): Boolean;
var
  I: NativeInt;
  Bit: Byte;
begin
  if (Types = AnyType) or (Count <= 0) then
    Exit(True);
  { The values' words (two for each) are checked once: the reads below are all within them. }
  CheckWords(D^.Tape, First shl 1, Count shl 1);
  if (Types and TypeInteger <> 0) and (Types and TypeNumber = 0) then begin
    { Integers that are not numbers in general: the number's value decides. }
    for I := First to First + Count - 1 do begin
      Bit := Byte(RunWordAt(D^.Tape, I shl 1) and $FF);
      if (Types and Bit = 0) and not ((Bit = KindNumber)
        and IsIntegerNumber(Byte((RunWordAt(D^.Tape, I shl 1) shr 8) and $FF), RunWordAt(D^.Tape, I shl 1 + 1))) then
        Exit(False);
    end;
    Exit(True);
  end;
  { A kind is its type bit. }
  for I := First to First + Count - 1 do
    if Byte(RunWordAt(D^.Tape, I shl 1) and $FF) and Types = 0 then
      Exit(False);
  Result := True;
end;

function RunArray(var E: TEvaluator; Pl: PArrayPlan; X: NativeInt): Boolean;
var
  D: PDocument;
  Count, First, Prefix, I, Item: NativeInt;
  Length64, Matches: UInt64;
begin
  D := E.D;
  Count := DocCount(D^, X);
  if (UInt64(Count) < Pl^.Min) or (UInt64(Count) > Pl^.Max) then
    Exit(False);
  First := DocFirst(D^, X);
  if Pl^.IsSimple then
    Exit(AllOfType(D, First, Count, Pl^.Simple));
  Prefix := Length(Pl^.Prefix);
  if Count < Prefix then
    Prefix := Count;
  for I := 0 to Prefix - 1 do
    if not RunChild(E, ChildAt(Pl^.Prefix, I)^, First + I) then
      Exit(False);
  if Pl^.HasItems then begin
    if Pl^.HasNested then begin
      for Item := First + Prefix to First + Count - 1 do begin
        if DocKind(D^, Item) <> KindArray then begin
          { Not an array: only the items' type test applies. }
          if not TypeOK(Pl^.Items.Types, D^, Item) then
            Exit(False);
          Continue;
        end;
        Length64 := UInt64(DocCount(D^, Item));
        if (Pl^.Items.Types and TypeArray = 0) or (Length64 < Pl^.Nested.Min) or (Length64 > Pl^.Nested.Max)
          or not AllOfType(D, DocFirst(D^, Item), Int32(Length64), Pl^.Nested.Types) then
          Exit(False);
      end;
    end else if Pl^.Items.Shape = ShapeTrivial then begin
      { A type-only items schema: one tight loop, or none for true. }
      if not AllOfType(D, First + Prefix, Count - Prefix, Pl^.Items.Types) then
        Exit(False);
    end else
      for Item := First + Prefix to First + Count - 1 do
        if not RunChild(E, Pl^.Items, Item) then
          Exit(False);
  end;
  if Pl^.Contains >= 0 then begin
    Matches := 0;
    for I := 0 to Count - 1 do
      if Run(E, Pl^.Contains, First + I) then begin
        Inc(Matches);
        if not Pl^.MaxContains.IsSet and (Matches >= Pl^.MinContains) then
          Break;
      end;
    if (Matches < Pl^.MinContains) or (Pl^.MaxContains.IsSet and (Matches > Pl^.MaxContains.N)) then
      Exit(False);
  end;
  Result := not Pl^.Unique or AllUnique(D^, X, State(E)^.Unique);
end;

{ RunLeaf evaluates a leaf's keywords: its value constraints, then those for the instance's type. }
function RunLeaf(var E: TEvaluator; B: PBody; X: NativeInt): Boolean;
var
  D: PDocument;
  I: NativeInt;
  O: POp;
  Ok: Boolean;
begin
  D := E.D;
  for I := 0 to Length(B^.Values) - 1 do begin
    O := OpAt(B^.Values, I);
    case O^.Kind of
      OpConst:
        Ok := ValuesEqual(D^, X, E.P^.Documents[O^.Value.Doc], O^.Value.N);
      OpEnumStrings:
        Ok := (DocKind(D^, X) = KindString)
          and (NamesFind(O^.Names, StrBytes(D, X)^, DocStrOffset(D^, X), DocCount(D^, X)) >= 0);
    else
      Ok := EnumContains(E, O^.Values, X);
    end;
    if not Ok then
      Exit(False);
  end;
  case DocKind(D^, X) of
    KindNumber:
      Result := (Length(B^.Number) = 0) or RunNumber(E, B^.Number, X);
    KindString:
      Result := (Length(B^.Str) = 0) or RunString(E, B^.Str, X);
  else
    Result := True;
  end;
end;

function RunNumber(var E: TEvaluator; const Ops: TNumberOpArray; X: NativeInt): Boolean;
var
  D: PDocument;
  Flag: Byte;
  Data: UInt64;
  I: NativeInt;
  Ok: Boolean;
begin
  D := E.D;
  Flag := DocFlags(D^, X);
  Data := DocData(D^, X);
  for I := 0 to Length(Ops) - 1 do begin
    case Ops[I].Kind of
      NumberMinimum:
        Ok := CompareNumbers(Flag, Data, Ops[I].Flag, Ops[I].Data) >= 0;
      NumberMaximum:
        Ok := CompareNumbers(Flag, Data, Ops[I].Flag, Ops[I].Data) <= 0;
      NumberExclusiveMinimum:
        Ok := CompareNumbers(Flag, Data, Ops[I].Flag, Ops[I].Data) > 0;
      NumberExclusiveMaximum:
        Ok := CompareNumbers(Flag, Data, Ops[I].Flag, Ops[I].Data) < 0;
      NumberMultipleOf:
        Ok := DivisorDivides(Ops[I].Divisor, Flag, Data, D^.Source, DocNumberStart(D^, X));
    else
      Ok := CheckNumber(E, Ops[I].Format, D^, X);
    end;
    if not Ok then
      Exit(False);
  end;
  Result := True;
end;

{ LengthOK decides minLength/maxLength, counting code points only when the byte length cannot decide (a code point
  is one to four bytes). }
function LengthOK(D: PDocument; X: NativeInt; Min, Max: UInt64): Boolean;
var
  Length64, Quarter, Chars: UInt64;
begin
  Length64 := UInt64(DocCount(D^, X));
  if Length64 < Min then
    Exit(False);
  if DocStrASCII(D^, X) then
    Exit(Length64 <= Max);
  Quarter := (Length64 + 3) div 4;
  if (Length64 <= Max) and (Quarter >= Min) then
    Exit(True);
  { Every code point is at most four bytes: more than max of them for sure. }
  if Quarter > Max then
    Exit(False);
  Chars := CodePoints(D, X);
  Result := (Chars >= Min) and (Chars <= Max);
end;

function RunString(var E: TEvaluator; const Ops: TStringOpArray; X: NativeInt): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  Op: PStringOp;
  H: UInt64;
  I, Off, Len: NativeInt;
  Ok: Boolean;
begin
  D := E.D;
  H := DocHeader(D^, X);
  Src := HeaderBytes(D, H);
  Off := DocStrOffset(D^, X);
  Len := HeaderCount(H);
  for I := 0 to Length(Ops) - 1 do begin
    Op := StringOpAt(Ops, I);
    case Op^.Kind of
      StringLength:
        Ok := LengthOK(D, X, Op^.Min, Op^.Max);
      StringPattern:
        Ok := PatternMatch(PatternAt(E.P^.Patterns, Op^.Pattern)^, Src^, Off, Len, HeaderStrASCII(H));
      StringFormat:
        Ok := CheckString(E, Op^.Format, Src^, Off, Len);
    else
      Ok := ContentOK(E, Src, Off, Len, Op^.Content);
    end;
    if not Ok then
      Exit(False);
  end;
  Result := True;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The fused pass (the evaluation half of fused.go) }

{ MergedAllowed is the mask of the constant tests that allow the value. }
function MergedAllowed(const P: TProgram; const M: TMergedTests; D: PDocument; V: NativeInt): UInt64;
var
  I: NativeInt;
begin
  if DocKind(D^, V) = KindString then begin
    I := NamesFind(M.Strings, StrBytes(D, V)^, DocStrOffset(D^, V), DocCount(D^, V));
    if I >= 0 then
      Exit(WordAt(M.StringMasks, I));
    Exit(0);
  end;
  for I := 0 to Length(M.Others) - 1 do
    if ValuesEqual(D^, V, P.Documents[M.Others[I].Value.Doc], M.Others[I].Value.N) then
      Exit(M.Others[I].Mask);
  Result := 0;
end;

function TestHolds(const P: TProgram; const T: TValueTest; D: PDocument; V: NativeInt): Boolean;
var
  I: NativeInt;
begin
  if T.IsPattern then begin
    if DocKind(D^, V) = KindString then
      Exit(PatternMatch(PatternAt(P.Patterns, T.Pattern)^, StrBytes(D, V)^, DocStrOffset(D^, V), DocCount(D^, V),
        DocStrASCII(D^, V)));
    Exit(not T.RequiresString);
  end;
  for I := 0 to Length(T.Allowed) - 1 do
    if ValuesEqual(D^, V, P.Documents[T.Allowed[I].Doc], T.Allowed[I].N) then
      Exit(True);
  Result := False;
end;

{ FusedApplies reports whether one of the conditions in the masks applies what they guard: one of ThenMask that
  holds, or one of ElsMask that does not. }
function FusedApplies(const S: TFusedPass; ThenMask, ElsMask: UInt64): Boolean; inline;
begin
  Result := (S.ThenMask and ThenMask) or (S.ElsMask and ElsMask) <> 0;
end;

function AllSeen(const S: TFusedPass; const Names: TUInt16Array): Boolean;
var
  K: NativeInt;
  Name: UInt16;
begin
  for K := 0 to Length(Names) - 1 do begin
    Name := UInt16At(Names, K);
    if S.Seen[Name shr 6] and (One64 shl (Name and 63)) = 0 then
      Exit(False);
  end;
  Result := True;
end;

function GateActive(const S: TFusedPass; const G: TGate): Boolean;
begin
  if not G.IsSet then
    Exit(True);
  Result := (S.GateOK and (One64 shl G.Condition) <> 0)
    and ((S.Holds and (One64 shl G.Condition) <> 0) = G.Polarity);
end;

procedure FailAlt(var S: TFusedPass; const Alt: TAltBranch); inline;
begin
  S.AltFailed[Alt.Group] := S.AltFailed[Alt.Group] or (One64 shl Alt.Branch);
end;

function ApplyOpt(var E: TEvaluator; const C: TOptChild; V: NativeInt): Boolean; inline;
begin
  Result := not C.IsSet or RunChild(E, C.Child, V);
end;

{ ResolveUnknown resolves a name no entry knows against one branch's pattern and additional properties. Matched
  says whether it matched (the property is covered). The result is False when the application failed. }
function ResolveUnknown(var E: TEvaluator; C: PFusedContributor; const Name: TBytes; Off, Len: NativeInt;
  Ascii: Boolean; V: NativeInt; out Matched: Boolean): Boolean;
var
  I: NativeInt;
  Fp: PFusedPattern;
begin
  Matched := False;
  for I := 0 to Length(C^.Patterns) - 1 do begin
    Fp := FusedPatternAt(C^.Patterns, I);
    if PatternMatch(PatternAt(E.P^.Patterns, Fp^.Pattern)^, Name, Off, Len, Ascii) then begin
      Matched := True;
      if not ApplyOpt(E, Fp^.Child, V) then begin
        Matched := False;
        Exit(False);
      end;
    end;
  end;
  if not Matched and C^.HasAdditional then begin
    Matched := True;
    if not ApplyOpt(E, C^.Additional, V) then begin
      Matched := False;
      Exit(False);
    end;
  end;
  Result := True;
end;

function FusedEntryAt(var E: TEvaluator; F: PFusedObject; Index, V: NativeInt; var Pass: TFusedPass): Byte;
var
  D: PDocument;
  Entry: PFusedEntry;
  Allowed, Keyed: UInt64;
  T, I: NativeInt;
  Holds: Boolean;
  App: PFusedApp;
  C: PFusedContributor;
begin
  D := E.D;
  Entry := EntryAt(F^.Entries, Index);
  Pass.Seen[Index shr 6] := Pass.Seen[Index shr 6] or (One64 shl (Index and 63));
  if Length(Entry^.Tests) <> 0 then begin
    Allowed := 0;
    Keyed := 0;
    if Entry^.HasMerged then begin
      Allowed := MergedAllowed(E.P^, Entry^.Merged, D, V);
      Keyed := Entry^.Merged.Keyed;
    end;
    for T := 0 to Length(Entry^.Tests) - 1 do begin
      if (T < 64) and (Keyed and (One64 shl T) <> 0) then
        Holds := Allowed and (One64 shl T) <> 0
      else
        Holds := TestHolds(E.P^, ValueTestAt(Entry^.Tests, T)^, D, V);
      if not Holds then
        Pass.Failed := Pass.Failed or (One64 shl ValueTestAt(Entry^.Tests, T)^.Condition);
    end;
  end;
  Result := 0;
  for I := 0 to Length(Entry^.Apps) - 1 do begin
    App := FusedAppAt(Entry^.Apps, I);
    C := FusedContributorAt(F^.Contributors, App^.Contributor);
    if C^.Condition.IsSet then begin
      Result := Result or FusedDefer;
      Continue;
    end;
    if App^.HasChild and not RunChild(E, App^.Child, V) then begin
      if not C^.Alt.IsSet then
        Exit(FusedFailed);
      FailAlt(Pass, C^.Alt);
      Continue;
    end;
    Result := Result or FusedCover;
  end;
end;

function FusedUnknown(var E: TEvaluator; F: PFusedObject; const Name: TBytes; Off, Len: NativeInt; Ascii: Boolean;
  V: NativeInt; var Pass: TFusedPass): Byte;
var
  I: NativeInt;
  C: PFusedContributor;
  Matched, Ok: Boolean;
begin
  if not F^.ResolvesUnknown then
    Exit(0);
  for I := 0 to Length(F^.Absent) - 1 do
    if (Pass.Failed and (One64 shl F^.Absent[I].Condition) = 0)
      and PatternMatch(PatternAt(E.P^.Patterns, F^.Absent[I].Pattern)^, Name, Off, Len, Ascii) then
      Pass.Failed := Pass.Failed or (One64 shl F^.Absent[I].Condition);
  Result := 0;
  for I := 0 to Length(F^.Contributors) - 1 do begin
    C := FusedContributorAt(F^.Contributors, I);
    if C^.Condition.IsSet then begin
      if (Length(C^.Patterns) <> 0) or C^.HasAdditional then
        Result := Result or FusedDefer;
      Continue;
    end;
    Ok := ResolveUnknown(E, C, Name, Off, Len, Ascii, V, Matched);
    if not Ok and not C^.Alt.IsSet then
      Exit(FusedFailed)
    else if not Ok then
      FailAlt(Pass, C^.Alt)
    else if Matched then
      Result := Result or FusedCover;
  end;
end;

function CountOK(C: PFusedContributor; Count: UInt64): Boolean; inline;
begin
  Result := (not C^.Min.IsSet or (Count >= C^.Min.N)) and (not C^.Max.IsSet or (Count <= C^.Max.N));
end;

function OnesCount64(V: UInt64): NativeInt;
begin
  Result := 0;
  while V <> 0 do begin
    V := V and (V - 1);
    Inc(Result);
  end;
end;

function RunFusedPass(var E: TEvaluator; F: PFusedObject; X: NativeInt; var Pass: TFusedPass): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  Count, Pending, Hint, First, Ordinal, K, Index, Off, Len, I, J, V, Matches: NativeInt;
  Covered, DeferredBeyond: TBitset;
  Deferred, W, H, All, Survivors: UInt64;
  Outcome: Byte;
  Cover, Matched: Boolean;
  App: PFusedApp;
  C: PFusedContributor;
begin
  D := E.D;
  Count := DocCount(D^, X);
  if F^.HasCountBounds then
    for I := 0 to Length(F^.Contributors) - 1 do begin
      C := FusedContributorAt(F^.Contributors, I);
      if not C^.Condition.IsSet and not C^.Alt.IsSet and not CountOK(C, UInt64(Count)) then
        Exit(False);
    end;
  { The covered properties (for unevaluatedProperties) and, beyond the first 64 (which a mask holds), the
    properties with conditional applications pending, by ordinal. }
  Covered := NoBits;
  DeferredBeyond := NoBits;
  if F^.HasUnevaluated then
    Covered := NewBits(E, Count);
  if Count > 64 then
    DeferredBeyond := NewBits(E, Count);
  Deferred := 0;
  Pending := 0;
  Hint := 0;
  First := DocFirst(D^, X);
  for Ordinal := 0 to Count - 1 do begin
    K := First + 2 * Ordinal;
    H := DocHeader(D^, K);
    Src := HeaderBytes(D, H);
    Off := DocStrOffset(D^, K);
    Len := HeaderCount(H);
    W := NameWord(Src^, Off, Len);
    if NamesAt(F^.Names, Hint, Len, W) and ((Len <= 8) or NameMapRest(F^.Names.M, Hint, Src^, Off, Len)) then begin
      Index := Hint;
      Inc(Hint);
    end else
      Index := NamesFindAfter(F^.Names, Src^, Off, Len, W, Hint);
    if Index >= 0 then
      Outcome := FusedEntryAt(E, F, Index, K + 1, Pass)
    else
      Outcome := FusedUnknown(E, F, Src^, Off, Len, HeaderStrASCII(H), K + 1, Pass);
    if Outcome and FusedFailed <> 0 then
      Exit(False);
    if (Outcome and FusedCover <> 0) and Tracked(Covered) then
      SetBit(E, Covered, Ordinal);
    if Outcome and FusedDefer <> 0 then begin
      Inc(Pending);
      if Ordinal < 64 then
        Deferred := Deferred or (One64 shl Ordinal)
      else
        SetBit(E, DeferredBeyond, Ordinal);
    end;
  end;

  { Decide the conditions, then which apply along their gates (a gate precedes the conditions under it). }
  for I := 0 to Length(F^.Conditions) - 1 do
    if (Pass.Failed and (One64 shl I) = 0) and AllSeen(Pass, FusedConditionAt(F^.Conditions, I)^.Required) then
      Pass.Holds := Pass.Holds or (One64 shl I);
  for I := 0 to Length(F^.Conditions) - 1 do
    if GateActive(Pass, FusedConditionAt(F^.Conditions, I)^.Gate) then
      Pass.GateOK := Pass.GateOK or (One64 shl I);
  Pass.ThenMask := Pass.GateOK and Pass.Holds;
  Pass.ElsMask := Pass.GateOK and not Pass.Holds;

  Ordinal := 0;
  while (Ordinal < Count) and (Pending > 0) do begin
    if Ordinal < 64 then begin
      if Deferred and (One64 shl Ordinal) = 0 then begin
        Inc(Ordinal);
        Continue;
      end;
    end else if not GetBit(E, DeferredBeyond, Ordinal) then begin
      Inc(Ordinal);
      Continue;
    end;
    Dec(Pending);
    K := First + 2 * Ordinal;
    H := DocHeader(D^, K);
    Src := HeaderBytes(D, H);
    Off := DocStrOffset(D^, K);
    Len := HeaderCount(H);
    V := K + 1;
    Cover := False;
    Index := NamesFind(F^.Names, Src^, Off, Len);
    if Index >= 0 then begin
      for I := 0 to Length(EntryAt(F^.Entries, Index)^.Apps) - 1 do begin
        { An application whose first contributor has no condition was made in the first step. }
        App := FusedAppAt(EntryAt(F^.Entries, Index)^.Apps, I);
        if not FusedContributorAt(F^.Contributors, App^.Contributor)^.Condition.IsSet then
          Continue;
        if FusedApplies(Pass, App^.ThenMask, App^.ElsMask) then begin
          if App^.HasChild and not RunChild(E, App^.Child, V) then
            Exit(False);
          Cover := True;
        end;
      end;
    end else
      for I := 0 to Length(F^.Contributors) - 1 do begin
        C := FusedContributorAt(F^.Contributors, I);
        if C^.Condition.IsSet and FusedApplies(Pass, C^.ThenMask, C^.ElsMask) then begin
          if not ResolveUnknown(E, C, Src^, Off, Len, HeaderStrASCII(H), V, Matched) then
            Exit(False);
          Cover := Cover or Matched;
        end;
      end;
    if Cover and Tracked(Covered) then
      SetBit(E, Covered, Ordinal);
    Inc(Ordinal);
  end;

  for J := 0 to Length(F^.Finals) - 1 do begin
    C := FusedContributorAt(F^.Contributors, UInt16At(F^.Finals, J));
    if C^.Condition.IsSet then begin
      if not FusedApplies(Pass, C^.ThenMask, C^.ElsMask) then
        Continue;
      if not CountOK(C, UInt64(Count)) then
        Exit(False);
    end;
    if (C^.Alt.IsSet and not CountOK(C, UInt64(Count))) or not AllSeen(Pass, C^.Required) then begin
      if not C^.Alt.IsSet then
        Exit(False);
      FailAlt(Pass, C^.Alt);
    end;
  end;

  for I := 0 to Length(F^.AltGroups) - 1 do begin
    All := MaxUInt64;
    if F^.AltGroups[I].Count < 64 then
      All := (One64 shl F^.AltGroups[I].Count) - 1;
    Survivors := not Pass.AltFailed[I] and All;
    if (Survivors = 0) or (F^.AltGroups[I].ExactlyOne and (OnesCount64(Survivors) <> 1)) then
      Exit(False);
  end;

  for I := 0 to Length(F^.Forbidden) - 1 do
    if GateActive(Pass, F^.Forbidden[I].Gate) and AllSeen(Pass, F^.Forbidden[I].Names) then
      Exit(False);

  for I := 0 to Length(F^.Alternatives) - 1 do begin
    if not GateActive(Pass, F^.Alternatives[I].Condition) then
      Continue;
    Matches := 0;
    for J := 0 to Length(F^.Alternatives[I].Branches) - 1 do
      if AllSeen(Pass, F^.Alternatives[I].Branches[J]) then
        Inc(Matches);
    if (Matches = 0) or (F^.Alternatives[I].ExactlyOne and (Matches <> 1)) then
      Exit(False);
  end;

  if F^.HasUnevaluated then
    for Ordinal := 0 to Count - 1 do
      if not GetBit(E, Covered, Ordinal) and not RunChild(E, F^.Unevaluated, First + 2 * Ordinal + 1) then
        Exit(False);
  Result := True;
end;

function RunFused(var E: TEvaluator; F: PFusedObject; X: NativeInt): Boolean;
var
  Pass: TFusedPass;
  Mark: NativeInt;
begin
  if F^.HasFlat then
    Exit(RunStrictObject(E, @F^.Flat, X));
  FillChar(Pass, SizeOf(Pass), 0);
  { Only a pass that tracks the covered properties, or an object of more than 64 properties, takes sets from the
    arena, and so the evaluation's buffers. }
  if not F^.HasUnevaluated and (DocCount(E.D^, X) <= 64) then
    Exit(RunFusedPass(E, F, X, Pass));
  Mark := State(E)^.ArenaLen;
  Result := RunFusedPass(E, F, X, Pass);
  E.S^.ArenaLen := Mark;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  The evaluator (eval.go) }

function Validate(var E: TEvaluator): Boolean;
begin
  Result := Run(E, E.P^.Entry, E.D^.Root);
end;

{ Wants reports whether a keyword result with the given outcome carries message text. }
function Wants(var E: TEvaluator; IsMatch: Boolean): Boolean; inline;
begin
  Result := (E.C <> nil) and CollectorWithText(E.C^, IsMatch);
end;

{ Keyword records a keyword's result when collecting. It reports whether evaluation stops here (failing fast). }
function Keyword(var E: TEvaluator; IsMatch: Boolean; const Message, Name: UTF8String): Boolean;
begin
  if E.C <> nil then begin
    EvaluatedKeyword(E.C^, IsMatch, Message, Name);
    Exit(False);
  end;
  Result := not IsMatch;
end;

{ CountKeyword records a keyword whose message ends in a quoted count. }
function CountKeyword(var E: TEvaluator; IsMatch: Boolean; const Prefix: UTF8String; N: UInt64;
  const Name: UTF8String): Boolean;
var
  Message: UTF8String;
begin
  if E.C = nil then
    Exit(not IsMatch);
  Message := '';
  if CollectorWithText(E.C^, IsMatch) then
    Message := Prefix + ' ''' + UIntText(N) + '''';
  EvaluatedKeyword(E.C^, IsMatch, Message, Name);
  Result := False;
end;

{ CollectPureRef is the single reference of a pure-$ref node, for collecting (a node with annotations is not
  elided). }
function CollectPureRef(var E: TEvaluator; Id: TNodeID): TNodeID;
begin
  if (Id < Length(E.P^.Annotations)) and (Length(E.P^.Annotations[Id]) <> 0) then
    Exit(NoNode);
  Result := PureRefTarget(E.P^.Nodes[Id]);
end;

{ Resolve follows pure-reference hops (at most 16, and not across resources when a dynamic scope is kept), giving
  the target and the evaluation-path suffix of the hops (/$ref, /$dynamicRef, /$recursiveRef). }
function Resolve(var E: TEvaluator; Id: TNodeID; out Suffix: UTF8String): TNodeID;
var
  Current, Next: TNodeID;
  Hop: Int32;
begin
  Current := Id;
  Suffix := '';
  for Hop := 0 to 15 do begin
    Next := CollectPureRef(E, Current);
    if Next < 0 then
      Break;
    if E.P^.UsesDynamicScope and (E.P^.Nodes[Next].ResourceID <> E.P^.Nodes[Current].ResourceID) then
      Break;
    if E.P^.Nodes[Current].Ref >= 0 then
      Suffix := Suffix + '/$ref'
    else
      Suffix := Suffix + '/' + E.P^.Nodes[Current].StaticDynamicKeyword;
    Current := Next;
  end;
  Result := Current;
end;

function EvalCore(var E: TEvaluator; Id: TNodeID; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean; forward;

function Evaluate(var E: TEvaluator): Boolean;
var
  Root: TNodeID;
  Suffix: UTF8String;
begin
  { A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path. }
  Root := Resolve(E, E.P^.Root, Suffix);
  BeginChildContext(E.C^, False, '', E.P^.Nodes[Root].Pointer, False, '');
  Result := EvalNode(E, Root, E.D^.Root, NoBits);
  CommitChildContext(E.C^, False, Result, MsgEvaluatedSubschema);
end;

function EvalNode(var E: TEvaluator; Id: TNodeID; X: Int32; const Bits: TBitset): Boolean;
var
  N: PSchemaNode;
  Pushed: Boolean;
  Kind: Byte;
  Own: TBitset;
begin
  N := @E.P^.Nodes[Id];
  if N^.AlwaysTrue or N^.AlwaysFalse then begin
    if E.C <> nil then
      EvaluatedBooleanSchema(E.C^, N^.AlwaysTrue);
    Exit(N^.AlwaysTrue);
  end;
  Pushed := E.P^.UsesDynamicScope and PushScope(E, N^.ResourceID);
  Kind := DocKind(E.D^, X);
  if not Tracked(Bits) and (((N^.UnevaluatedProperties >= 0) and (Kind = KindObject))
    or ((N^.UnevaluatedItems >= 0) and (Kind = KindArray))) then begin
    Own := NewBits(E, DocCount(E.D^, X));
    Result := EvalCore(E, Id, N, X, Own);
    FreeBits(E, Own);
  end else
    Result := EvalCore(E, Id, N, X, Bits);
  if Pushed then
    PopScope(E);
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Numbers and strings }

function EvalNumber(var E: TEvaluator; N: PSchemaNode; X: Int32): Boolean;
var
  D: PDocument;
  M: Boolean;
  Message: UTF8String;
  Custom: TFormatValidator;
  Start: Int32;

  function Bound(const B: TValueRef; IsMatch: Boolean; const Text, Name: UTF8String): Boolean;
  var
    BoundMessage: UTF8String;
  begin
    BoundMessage := '';
    if Wants(E, IsMatch) then
      BoundMessage := Text + Quoted(NumberText(E.P^.Documents[B.Doc], B.N));
    Result := Keyword(E, IsMatch, BoundMessage, Name);
  end;

  function Compare(const B: TValueRef): Int32;
  begin
    Result := CompareNumbers(DocFlags(D^, X), DocData(D^, X), DocFlags(E.P^.Documents[B.Doc], B.N),
      DocData(E.P^.Documents[B.Doc], B.N));
  end;

begin
  D := E.D;
  Result := True;
  if N^.AssertFormat and FormatKindIsNumeric(N^.FormatKind) and N^.HasFormat then begin
    Custom := CustomFormat(E.P^.Options, N^.Format);
    if Assigned(Custom) then begin
      Start := DocNumberStart(D^, X);
      M := CallCustom(E, Custom, D^.Source, Start, DocNumberEnd(D^, X) - Start);
    end else
      M := FormatCheckNumber(N^.FormatKind, DocFlags(D^, X), DocData(D^, X));
    Message := '';
    if Wants(E, M) then
      Message := 'The value was expected to be in a supported format, and within bounds for '''
        + FormatKindName(N^.FormatKind) + '''';
    if Keyword(E, M, Message, 'format') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.Minimum.IsSet then begin
    M := Compare(N^.Minimum) >= 0;
    if Bound(N^.Minimum, M, 'The value was expected to be greater than or equal to', 'minimum') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.Maximum.IsSet then begin
    M := Compare(N^.Maximum) <= 0;
    if Bound(N^.Maximum, M, 'The value was expected to be less than or equal to', 'maximum') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.ExclusiveMinimum.IsSet then begin
    M := Compare(N^.ExclusiveMinimum) > 0;
    if Bound(N^.ExclusiveMinimum, M, 'The value was expected to be greater than', 'exclusiveMinimum') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.ExclusiveMaximum.IsSet then begin
    M := Compare(N^.ExclusiveMaximum) < 0;
    if Bound(N^.ExclusiveMaximum, M, 'The value was expected to be less than', 'exclusiveMaximum') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.MultipleOf.IsSet then begin
    M := DivisorDivides(N^.Divisor, DocFlags(D^, X), DocData(D^, X), D^.Source, DocNumberStart(D^, X));
    if Bound(N^.MultipleOf, M, 'The value was expected to be a multiple of', 'multipleOf') then
      Exit(False);
    Result := Result and M;
  end;
end;

{ ContentOK is the draft 7 content assertion. }
function ContentOK(var E: TEvaluator; Src: PDocBytes; Off, Len: Int32; Kind: TContentKind): Boolean;
var
  St: PScratch;
  N: Int32;
begin
  if Kind = ContentNone then
    Exit(True);
  St := State(E);
  if Kind <> ContentJSON then begin
    Result := Base64Decode(Src^, Off, Len, St^.Content, N);
    if not Result or (Kind = ContentBase64) then
      Exit;
  end else begin
    { The parser reads from the start of an array, so the string is copied to the content buffer. }
    N := Len;
    if N > Length(St^.Content) then
      SetLength(St^.Content, 2 * N + 64);
    if N > 0 then
      Move(Src^[Off], St^.Content[0], N);
  end;
  Result := ParserIsValid(St^.ContentParser, St^.Content, N);
end;

function EvalString(var E: TEvaluator; N: PSchemaNode; X: Int32): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  Off, Len: Int32;
  Length64: UInt64;
  M: Boolean;
  Message, Name: UTF8String;
  Custom: TFormatValidator;
begin
  D := E.D;
  Src := StrBytes(D, X);
  Off := DocStrOffset(D^, X);
  Len := DocCount(D^, X);
  Result := True;
  if N^.MinLength.IsSet or N^.MaxLength.IsSet then begin
    Length64 := CodePoints(D, X);
    if N^.MinLength.IsSet then begin
      M := Length64 >= N^.MinLength.N;
      if CountKeyword(E, M, 'Expected the length of the value to be greater than or equal to', N^.MinLength.N,
        'minLength') then
        Exit(False);
      Result := Result and M;
    end;
    if N^.MaxLength.IsSet then begin
      M := Length64 <= N^.MaxLength.N;
      if CountKeyword(E, M, 'Expected the length of the value to be less than or equal to', N^.MaxLength.N,
        'maxLength') then
        Exit(False);
      Result := Result and M;
    end;
  end;
  if N^.Pattern >= 0 then begin
    M := PatternMatch(E.P^.Patterns[N^.Pattern], Src^, Off, Len, DocStrASCII(D^, X));
    Message := '';
    if Wants(E, M) then
      Message := 'Expected the value to match the regular expression' + Quoted(E.P^.Patterns[N^.Pattern].Source);
    if Keyword(E, M, Message, 'pattern') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.AssertFormat and not FormatKindIsNumeric(N^.FormatKind) and N^.HasFormat then begin
    M := True;
    Message := '';
    Custom := CustomFormat(E.P^.Options, N^.Format);
    if Assigned(Custom) then begin
      M := CallCustom(E, Custom, Src^, Off, Len);
      if Wants(E, M) then
        Message := 'Expected a string in the ''' + N^.Format + ''' format.';
    end else if N^.FormatKind <> FormatKindUnknown then begin
      M := FormatCheckString(N^.FormatKind, Src^, Off, Len, N^.Dialect <= Draft6);
      if E.C <> nil then
        Message := FormatKindMessage(N^.FormatKind);
    end;
    if Keyword(E, M, Message, 'format') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.AssertContent then begin
    M := ContentOK(E, Src, Off, Len, N^.Content);
    if E.C = nil then begin
      if not M then
        Exit(False);
    end else begin
      Message := 'Expected valid Base64-encoded JSON content.';
      Name := 'contentMediaType';
      case N^.Content of
        ContentBase64: begin
          Message := 'Expected a valid Base64-encoded string.';
          Name := 'contentEncoding';
        end;
        ContentJSON:
          Message := 'Expected valid JSON content.';
      else
      end;
      EvaluatedKeyword(E.C^, M, Message, Name);
    end;
    Result := Result and M;
  end;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Objects }

{ EvalAt applies a child at a new instance location (a property value or an array item), when collecting. }
function EvalAt(var E: TEvaluator; Child: TNodeID; const Path: UTF8String; Value: Int32;
  const DocSegment: UTF8String): Boolean;
var
  Target: TNodeID;
  Suffix: UTF8String;
begin
  Target := Resolve(E, Child, Suffix);
  BeginChildContext(E.C^, True, Path + Suffix, E.P^.Nodes[Target].Pointer, True, DocSegment);
  Result := EvalNode(E, Target, Value, NoBits);
  CommitChildContext(E.C^, Result, Result, MsgEvaluatedSubschema);
end;

{ RequiredProperty records whether a required property is present. It reports whether evaluation stops here. }
function RequiredProperty(var E: TEvaluator; Present: Boolean; const PropertyName, Name: UTF8String): Boolean;
var
  Message: UTF8String;
begin
  if E.C = nil then
    Exit(not Present);
  Message := '';
  if CollectorWithText(E.C^, Present) then
    Message := 'Required property ' + Pick(Present, '', 'not ') + 'present ''' + PropertyName + '''';
  EvaluatedKeywordForProperty(E.C^, Present, Message, PropertyName, Name);
  Result := False;
end;

function EvalInPlaceChild(var E: TEvaluator; Child: TNodeID; const Path: UTF8String; X: Int32; const Bits: TBitset;
  CommitOnFailure, Elide: Boolean): Boolean; forward;

{ EvalProperty evaluates the keywords that apply to one property of an object (properties, patternProperties,
  additionalProperties and propertyNames): the body of the property loop of evalObject in the Go source, which has
  the property's name and its pointer segment as locals. Stop says evaluation stops here (failing fast). }
function EvalProperty(var E: TEvaluator; Id: TNodeID; N: PSchemaNode; First, I: Int32; const Bits: TBitset;
  var Ok: Boolean): Boolean;
var
  D: PDocument;
  Src: PDocBytes;
  K, V, Off, Len, J: Int32;
  Matched, Collect, M: Boolean;
  Segment, Entry: UTF8String;
  P: TNodeID;

  { At applies a child to the property's value. }
  function At(Child: TNodeID; const Name, EntryName: UTF8String): Boolean;
  var
    Path: UTF8String;
  begin
    if Tracked(Bits) then
      SetBit(E, Bits, I);
    if not Collect then
      Exit(Run(E, E.P^.FastTarget[Child], V));
    Path := Name;
    if (EntryName <> '') or (Name <> 'additionalProperties') then
      Path := Name + '/' + EntryName;
    Result := EvalAt(E, Child, Path, V, Segment);
  end;

begin
  D := E.D;
  Collect := E.C <> nil;
  K := First + 2 * I;
  V := K + 1;
  Src := StrBytes(D, K);
  Off := DocStrOffset(D^, K);
  Len := DocCount(D^, K);
  Matched := False;
  Segment := '';
  if Collect then
    Segment := EscapePointerToken(DocStrCopy(D^, K));
  P := PropertyOf(E.P^, Id, N^, Src^, Off, Len);
  if P >= 0 then begin
    Matched := True;
    if not At(P, 'properties', Segment) then begin
      if not Collect then
        Exit(True);
      Ok := False;
    end;
  end;
  for J := 0 to Length(N^.PatternProperties) - 1 do begin
    if not PatternMatch(E.P^.Patterns[N^.PatternProperties[J].Pattern], Src^, Off, Len, DocStrASCII(D^, K)) then
      Continue;
    Matched := True;
    Entry := '';
    if Collect then
      Entry := EscapePointerToken(E.P^.Patterns[N^.PatternProperties[J].Pattern].Source);
    if not At(N^.PatternProperties[J].Node, 'patternProperties', Entry) then begin
      if not Collect then
        Exit(True);
      Ok := False;
    end;
  end;
  if (N^.AdditionalProperties >= 0) and not Matched then
    if not At(N^.AdditionalProperties, 'additionalProperties', '') then begin
      if not Collect then
        Exit(True);
      Ok := False;
    end;
  if N^.PropertyNames >= 0 then begin
    { The name is a string value of the document: it is evaluated where it is. }
    if Collect then begin
      { Not elided. The document path stays the object's. }
      BeginChildContext(E.C^, True, 'propertyNames', E.P^.Nodes[N^.PropertyNames].Pointer, False, '');
      M := EvalNode(E, N^.PropertyNames, K, NoBits);
      CommitChildContext(E.C^, M, M, MsgEvaluatedSubschema);
      if not M then begin
        EvaluatedKeyword(E.C^, False, MsgPropertyNameFailed, 'propertyNames');
        Ok := False;
      end;
    end else if not Run(E, E.P^.FastTarget[N^.PropertyNames], K) then
      Exit(True);
  end;
  Result := False;
end;

{ EvalDependencySchema evaluates the schema of a dependency whose property is present. Stop says evaluation stops
  here (failing fast). }
function EvalDependencySchema(var E: TEvaluator; const Dep: TDependencyEntry; X: Int32; const Bits: TBitset;
  var Ok: Boolean): Boolean;
var
  Path, Message: UTF8String;
  M: Boolean;
begin
  if E.C = nil then
    Exit(not EvalInPlaceChild(E, Dep.Schema, '', X, Bits, True, True));
  Path := Dep.Keyword + '/' + EscapePointerToken(Dep.Name);
  M := EvalInPlaceChild(E, Dep.Schema, Path, X, Bits, True, True);
  Message := '';
  if CollectorWithText(E.C^, M) then
    Message := 'The value did match the schema applied because it contained the property ''' + Dep.Name + '''';
  EvaluatedKeywordForProperty(E.C^, M, Message, Dep.Name, Dep.Keyword);
  Ok := Ok and M;
  Result := False;
end;

function EvalObject(var E: TEvaluator; Id: TNodeID; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  D: PDocument;
  Count, First, I, J: Int32;
  M, Present: Boolean;
begin
  D := E.D;
  Result := True;
  Count := DocCount(D^, X);
  if N^.MinProperties.IsSet then begin
    M := UInt64(Count) >= N^.MinProperties.N;
    if CountKeyword(E, M, 'Expected the property count to be greater than or equal to', N^.MinProperties.N,
      'minProperties') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.MaxProperties.IsSet then begin
    M := UInt64(Count) <= N^.MaxProperties.N;
    if CountKeyword(E, M, 'Expected the property count to be less than or equal to', N^.MaxProperties.N,
      'maxProperties') then
      Exit(False);
    Result := Result and M;
  end;
  First := DocFirst(D^, X);
  if N^.HasProperties or N^.HasPatternProperties or (N^.AdditionalProperties >= 0) or (N^.PropertyNames >= 0) then
    for I := 0 to Count - 1 do
      if EvalProperty(E, Id, N, First, I, Bits, Result) then
        Exit(False);
  for I := 0 to Length(N^.RequiredList) - 1 do begin
    Present := DocProperty(D^, X, N^.RequiredList[I]) >= 0;
    if RequiredProperty(E, Present, N^.RequiredList[I], 'required') then
      Exit(False);
    Result := Result and Present;
  end;
  { Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas). }
  for I := 0 to Length(N^.Dependencies) - 1 do begin
    if DocProperty(D^, X, N^.Dependencies[I].Name) < 0 then
      Continue;
    for J := 0 to Length(N^.Dependencies[I].Required) - 1 do begin
      Present := DocProperty(D^, X, N^.Dependencies[I].Required[J]) >= 0;
      if RequiredProperty(E, Present, N^.Dependencies[I].Required[J], N^.Dependencies[I].Keyword) then
        Exit(False);
      Result := Result and Present;
    end;
    if N^.Dependencies[I].Schema >= 0 then
      if EvalDependencySchema(E, N^.Dependencies[I], X, Bits, Result) then
        Exit(False);
  end;
end;

function EvalUnevaluatedProperties(var E: TEvaluator; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  D: PDocument;
  Child: TNodeID;
  First, I, V: Int32;
begin
  D := E.D;
  Child := N^.UnevaluatedProperties;
  Result := True;
  First := DocFirst(D^, X);
  for I := 0 to DocCount(D^, X) - 1 do begin
    if GetBit(E, Bits, I) then
      Continue;
    SetBit(E, Bits, I);
    V := First + 2 * I + 1;
    if E.C = nil then begin
      if not Run(E, E.P^.FastTarget[Child], V) then
        Exit(False);
    end else if not EvalAt(E, Child, 'unevaluatedProperties', V,
      EscapePointerToken(DocStrCopy(D^, First + 2 * I))) then
      Result := False;
  end;
  if E.C <> nil then
    EvaluatedKeyword(E.C^, Result, '', 'unevaluatedProperties');
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Arrays }

{ EvalItem evaluates the keywords that apply to one item of an array when collecting (prefixItems, items and
  contains). It reports whether the item matched contains. }
function EvalItemCollecting(var E: TEvaluator; N: PSchemaNode; First, I: Int32; const Bits: TBitset;
  var Ok: Boolean): Boolean;
var
  Child, Target: TNodeID;
  Path, Suffix, Index: UTF8String;
  Item: Int32;
begin
  Item := First + I;
  Index := IntText(I);
  Child := NoNode;
  Path := '';
  if I < Length(N^.PrefixItems) then begin
    Child := N^.PrefixItems[I];
    Path := N^.PrefixKeyword + '/' + Index;
  end else if N^.Items >= 0 then begin
    Child := N^.Items;
    Path := N^.ItemsKeyword;
  end;
  if Child >= 0 then begin
    if Tracked(Bits) then
      SetBit(E, Bits, I);
    if not EvalAt(E, Child, Path, Item, Index) then
      Ok := False;
  end;
  Result := False;
  if N^.Contains >= 0 then begin
    Target := Resolve(E, N^.Contains, Suffix);
    BeginChildContext(E.C^, True, 'contains' + Suffix, E.P^.Nodes[Target].Pointer, True, Index);
    Result := EvalNode(E, Target, Item, NoBits);
    if Result then
      CommitChildContext(E.C^, True, True, MsgEvaluatedSubschema)
    else
      PopChildContext(E.C^);
  end;
end;

function EvalArray(var E: TEvaluator; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  D: PDocument;
  Collect, M, Matched, Over, Stop: Boolean;
  Count, First, I, Item: Int32;
  Matches: UInt64;
  Child: TNodeID;
begin
  D := E.D;
  Collect := E.C <> nil;
  Result := True;
  Count := DocCount(D^, X);
  if N^.MinItems.IsSet then begin
    M := UInt64(Count) >= N^.MinItems.N;
    if CountKeyword(E, M, 'Expected the item count to be greater than or equal to', N^.MinItems.N, 'minItems') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.MaxItems.IsSet then begin
    M := UInt64(Count) <= N^.MaxItems.N;
    if CountKeyword(E, M, 'Expected the item count to be less than or equal to', N^.MaxItems.N, 'maxItems') then
      Exit(False);
    Result := Result and M;
  end;
  if not N^.HasPrefixItems and (N^.Items < 0) and (N^.Contains < 0) and not N^.UniqueItems then
    Exit;
  Matches := 0;
  First := DocFirst(D^, X);
  for I := 0 to Count - 1 do begin
    Item := First + I;
    if Collect then
      Matched := EvalItemCollecting(E, N, First, I, Bits, Result)
    else begin
      Child := NoNode;
      if I < Length(N^.PrefixItems) then
        Child := N^.PrefixItems[I]
      else if N^.Items >= 0 then
        Child := N^.Items;
      if Child >= 0 then begin
        if Tracked(Bits) then
          SetBit(E, Bits, I);
        if not Run(E, E.P^.FastTarget[Child], Item) then
          Exit(False);
      end;
      Matched := (N^.Contains >= 0) and Run(E, E.P^.FastTarget[N^.Contains], Item);
    end;
    if Matched then begin
      Inc(Matches);
      if N^.ContainsMarksEvaluated and Tracked(Bits) then
        SetBit(E, Bits, I);
    end;
  end;
  if N^.UniqueItems then begin
    M := AllUnique(D^, X, State(E)^.Unique);
    if Keyword(E, M, MsgUniqueItems, 'uniqueItems') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.Contains >= 0 then begin
    Over := N^.MaxContains.IsSet and (Matches > N^.MaxContains.N);
    M := (Matches >= N^.MinContains) and not Over;
    if Over then
      Stop := CountKeyword(E, M, 'Expected the contains count to be less than or equal to', N^.MaxContains.N,
        'contains')
    else
      Stop := CountKeyword(E, M, 'Expected the contains count to be greater than or equal to', N^.MinContains,
        'contains');
    if Stop then
      Exit(False);
    Result := Result and M;
  end;
end;

function EvalUnevaluatedItems(var E: TEvaluator; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  D: PDocument;
  Child: TNodeID;
  First, I: Int32;
begin
  D := E.D;
  Child := N^.UnevaluatedItems;
  Result := True;
  First := DocFirst(D^, X);
  for I := 0 to DocCount(D^, X) - 1 do begin
    if GetBit(E, Bits, I) then
      Continue;
    SetBit(E, Bits, I);
    if E.C = nil then begin
      if not Run(E, E.P^.FastTarget[Child], First + I) then
        Exit(False);
    end else if not EvalAt(E, Child, 'unevaluatedItems', First + I, IntText(I)) then
      Result := False;
  end;
  if E.C <> nil then
    EvaluatedKeyword(E.C^, Result, '', 'unevaluatedItems');
end;

{ ---------------------------------------------------------------------------------------------------------------------
  In-place applicators }

function CanMark(var E: TEvaluator; Id: TNodeID; X: Int32): Boolean;
begin
  if DocKind(E.D^, X) = KindObject then
    Result := E.P^.Nodes[Id].MarksProperties
  else
    Result := E.P^.Nodes[Id].MarksItems;
end;

{ EvalInPlaceCollecting is the collecting case of EvalInPlaceChild: the child in a context of its own. }
function EvalInPlaceCollecting(var E: TEvaluator; Child: TNodeID; const Path: UTF8String; X: Int32;
  const Scratch: TBitset; CommitOnFailure, Elide: Boolean; out Guarded: Boolean; out Exceeded: Boolean): Boolean;
var
  Target: TNodeID;
  Suffix: UTF8String;
begin
  Target := Child;
  Suffix := '';
  if Elide then
    Target := Resolve(E, Child, Suffix);
  Exceeded := False;
  Guarded := E.P^.Nodes[Target].InPlaceCycle;
  if Guarded then begin
    Inc(E.Depth);
    if E.Depth > E.P^.MaxDepth then begin
      E.DepthExceeded := True;
      Dec(E.Depth);
      Exceeded := True;
      Exit(False);
    end;
  end;
  BeginChildContext(E.C^, True, Path + Suffix, E.P^.Nodes[Target].Pointer, False, '');
  Result := EvalNode(E, Target, X, Scratch);
  if Result or CommitOnFailure then
    CommitChildContext(E.C^, Result, Result, MsgEvaluatedSubschema)
  else
    PopChildContext(E.C^);
  if Guarded then
    Dec(E.Depth);
end;

{ EvalInPlaceChild evaluates an in-place child: a new context at the same instance location, on a fresh scratch set
  of evaluated properties or items merged into the parent's on success. A failing child is committed or popped. }
function EvalInPlaceChild(var E: TEvaluator; Child: TNodeID; const Path: UTF8String; X: Int32; const Bits: TBitset;
  CommitOnFailure, Elide: Boolean): Boolean;
var
  Target: TNodeID;
  Scratch: TBitset;
  Guarded, Exceeded: Boolean;
begin
  Scratch := NoBits;
  if Tracked(Bits) and CanMark(E, Child, X) then
    Scratch := NewBits(E, DocCount(E.D^, X));
  if E.C <> nil then begin
    Result := EvalInPlaceCollecting(E, Child, Path, X, Scratch, CommitOnFailure, Elide, Guarded, Exceeded);
    if Exceeded then begin
      if Tracked(Scratch) then
        FreeBits(E, Scratch);
      Exit(False);
    end;
  end else begin
    Target := Child;
    if Elide then
      Target := E.P^.FastTarget[Child];
    Guarded := E.P^.Nodes[Target].InPlaceCycle;
    if Guarded then begin
      Inc(E.Depth);
      if E.Depth > E.P^.MaxDepth then begin
        E.DepthExceeded := True;
        Dec(E.Depth);
        if Tracked(Scratch) then
          FreeBits(E, Scratch);
        Exit(False);
      end;
    end;
    if Tracked(Scratch) then
      Result := EvalNode(E, Target, X, Scratch)
    else
      Result := Run(E, Target, X);
    if Guarded then
      Dec(E.Depth);
  end;
  if Tracked(Scratch) then begin
    if Result then
      MergeBits(E, Bits, Scratch);
    FreeBits(E, Scratch);
  end;
end;

{ SelectBranches is the oneOf/anyOf branches a discriminator leaves as candidates for an instance: all of them
  (the result), or the listed ones. }
function SelectBranches(var E: TEvaluator; HasDisc: Boolean; const Disc: TDiscriminator; X: Int32;
  out Subset: PUInt32List): Boolean;
var
  D: PDocument;
  Value, I: Int32;
begin
  D := E.D;
  Subset := @NoBranches;
  if not HasDisc or (DocKind(D^, X) <> KindObject) then
    Exit(True);
  Value := DocProperty(D^, X, Disc.PropertyName);
  if Value < 0 then
    Exit(not Disc.AllRequire);
  for I := 0 to Length(Disc.Known) - 1 do
    if DiscriminatorValueMatches(Disc.Known[I].Value, E.P^.Documents, D^, Value) then begin
      Subset := @Disc.Known[I].Branches;
      Exit(False);
    end;
  Subset := @Disc.Unknown;
  Result := False;
end;

{ BranchPath is the evaluation path of a branch of a keyword when collecting, and nothing otherwise. }
function BranchPath(Collect: Boolean; const Name: UTF8String; I: Int32): UTF8String;
begin
  if Collect then
    Result := Name + '/' + IntText(I)
  else
    Result := '';
end;

function EvalInPlace(var E: TEvaluator; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  Collect, M, All, Any, Exhaustive, Track, Inner, Cond, NotGuarded: Boolean;
  Target: TNodeID;
  Suffix: UTF8String;
  Subset: PUInt32List;
  Count, I, J, Matched: Int32;
  Aside, Only: TBitset;
begin
  Collect := E.C <> nil;
  Result := True;
  if N^.Ref >= 0 then begin
    M := EvalInPlaceChild(E, N^.Ref, '$ref', X, Bits, True, True);
    if Keyword(E, M, Pick(M, MsgMatchedAll, MsgDidNotMatchAll), '$ref') then
      Exit(False);
    Result := Result and M;
  end;
  if N^.StaticDynamicRef >= 0 then begin
    M := EvalInPlaceChild(E, N^.StaticDynamicRef, N^.StaticDynamicKeyword, X, Bits, True, True);
    if Keyword(E, M, Pick(M, MsgMatchedAll, MsgDidNotMatchAll), N^.StaticDynamicKeyword) then
      Exit(False);
    Result := Result and M;
  end;
  if N^.HasDynamicRef then begin
    { The resolved target is elided, with no hops in the path. }
    Target := ResolveDynamic(E, N^.DynamicRef);
    if Collect then
      Target := Resolve(E, Target, Suffix)
    else
      Target := E.P^.FastTarget[Target];
    M := EvalInPlaceChild(E, Target, DynamicKeyword(N^.DynamicRef.IsRecursive), X, Bits, True, False);
    if Keyword(E, M, Pick(M, MsgMatchedAll, MsgDidNotMatchAll), DynamicKeyword(N^.DynamicRef.IsRecursive)) then
      Exit(False);
    Result := Result and M;
  end;
  if N^.HasAllOf then begin
    All := True;
    for I := 0 to Length(N^.AllOf) - 1 do
      if not EvalInPlaceChild(E, N^.AllOf[I], BranchPath(Collect, 'allOf', I), X, Bits, True, True) then begin
        if not Collect then
          Exit(False);
        All := False;
      end;
    if Keyword(E, All, Pick(All, MsgMatchedAll, MsgDidNotMatchAll), 'allOf') then
      Exit(False);
    Result := Result and All;
  end;
  if N^.HasAnyOf then begin
    Any := False;
    { Every branch runs when results are collected or evaluated properties or items are tracked. }
    Exhaustive := Collect or Tracked(Bits);
    { Fail-fast evaluation only tries the branches a discriminator property can select. }
    All := True;
    Subset := @NoBranches;
    if not Exhaustive then
      All := SelectBranches(E, N^.HasAnyOfDiscriminator, N^.AnyOfDiscriminator, X, Subset);
    Count := Length(Subset^);
    if All then
      Count := Length(N^.AnyOf);
    for J := 0 to Count - 1 do begin
      I := J;
      if not All then
        I := Int32(Subset^[J]);
      if EvalInPlaceChild(E, N^.AnyOf[I], BranchPath(Collect, 'anyOf', I), X, Bits, False, True) then begin
        Any := True;
        if not Exhaustive then
          Break;
      end;
    end;
    if Keyword(E, Any, Pick(Any, MsgMatchedAtLeastOne, MsgDidNotMatchAtLeastOne), 'anyOf') then
      Exit(False);
    Result := Result and Any;
  end;
  if N^.HasOneOf then begin
    Matched := 0;
    Track := Tracked(Bits);
    { Evaluated properties or items are merged only when exactly one branch matched, so they are collected
      aside, and the matching branch's kept. }
    Aside := NoBits;
    Only := NoBits;
    if Track then begin
      Only := NewBits(E, DocCount(E.D^, X));
      Aside := NewBits(E, DocCount(E.D^, X));
    end;
    { Branches a discriminator rules out cannot match, so fail-fast evaluation skips them. }
    All := True;
    Subset := @NoBranches;
    if not Collect and not Track then
      All := SelectBranches(E, N^.HasOneOfDiscriminator, N^.OneOfDiscriminator, X, Subset);
    Count := Length(Subset^);
    if All then
      Count := Length(N^.OneOf);
    for J := 0 to Count - 1 do begin
      I := J;
      if not All then
        I := Int32(Subset^[J]);
      if Track then
        ClearBits(E, Aside);
      if EvalInPlaceChild(E, N^.OneOf[I], BranchPath(Collect, 'oneOf', I), X, Aside, False, True) then begin
        Inc(Matched);
        if Track then
          CopyBits(E, Only, Aside);
        if not Collect and (Matched > 1) then
          Break;
      end;
    end;
    if Track then begin
      if Matched = 1 then
        MergeBits(E, Bits, Only);
      FreeBits(E, Only);
    end;
    case Matched of
      0: M := Keyword(E, False, MsgMatchedNoSchema, 'oneOf');
      1: M := Keyword(E, True, MsgMatchedExactlyOne, 'oneOf');
    else
      M := Keyword(E, False, MsgMatchedMoreThanOne, 'oneOf');
    end;
    if M then
      Exit(False);
    Result := Result and (Matched = 1);
  end;
  if N^.NotNode >= 0 then begin
    { Not elided, never contributes results or evaluated properties or items. A not on an in-place cycle is under
      the depth guard, like every other in-place applicator. }
    if Collect then begin
      NotGuarded := E.P^.Nodes[N^.NotNode].InPlaceCycle;
      if NotGuarded then
        Inc(E.Depth);
      if NotGuarded and (E.Depth > E.P^.MaxDepth) then begin
        E.DepthExceeded := True;
        Inner := False;
      end else begin
        BeginChildContext(E.C^, True, 'not', E.P^.Nodes[N^.NotNode].Pointer, False, '');
        Inner := EvalNode(E, N^.NotNode, X, NoBits);
        PopChildContext(E.C^);
      end;
      if NotGuarded then
        Dec(E.Depth);
    end else
      Inner := RunInPlace(E, E.P^.FastTarget[N^.NotNode], X);
    if Keyword(E, not Inner, Pick(Inner, MsgMatchedNot, MsgDidNotMatchNot), 'not') then
      Exit(False);
    Result := Result and not Inner;
  end;
  if N^.IfNode >= 0 then begin
    Cond := EvalInPlaceChild(E, N^.IfNode, 'if', X, Bits, False, True);
    if Collect then
      EvaluatedKeyword(E.C^, True, Pick(Cond, MsgMatchedIfForThen, MsgMatchedIfForElse), 'if');
    if Cond then begin
      if N^.ThenNode >= 0 then begin
        M := EvalInPlaceChild(E, N^.ThenNode, 'then', X, Bits, True, True);
        if Keyword(E, M, Pick(M, MsgMatchedThen, MsgDidNotMatchThen), 'then') then
          Exit(False);
        Result := Result and M;
      end;
    end else if N^.ElseNode >= 0 then begin
      M := EvalInPlaceChild(E, N^.ElseNode, 'else', X, Bits, True, True);
      if Keyword(E, M, Pick(M, MsgMatchedElse, MsgDidNotMatchElse), 'else') then
        Exit(False);
      Result := Result and M;
    end;
  end;
end;

{ EvalEnum decides enum by JSON equality with each value. }
function EvalEnum(var E: TEvaluator; N: PSchemaNode; X: Int32): Boolean;
var
  I: Int32;
begin
  for I := 0 to Length(N^.EnumValues) - 1 do
    if ValuesEqual(E.D^, X, E.P^.Documents[N^.EnumValues[I].Doc], N^.EnumValues[I].N) then
      Exit(True);
  Result := False;
end;

{ TypeKeyword and ConstKeyword record the type and const keywords with their messages when collecting. They are
  apart from EvalCore so that it holds no string of its own. }
procedure TypeKeyword(var E: TEvaluator; N: PSchemaNode; M: Boolean);
var
  Message: UTF8String;
begin
  Message := '';
  if CollectorWithText(E.C^, M) then
    Message := TypeMessage(N^.TypeMask);
  EvaluatedKeyword(E.C^, M, Message, 'type');
end;

procedure ConstKeyword(var E: TEvaluator; N: PSchemaNode; M: Boolean);
var
  Message: UTF8String;
begin
  Message := '';
  if CollectorWithText(E.C^, M) then
    Message := ConstMessage(E.P^.Documents[N^.ConstValue.Doc], N^.ConstValue.N);
  EvaluatedKeyword(E.C^, M, Message, 'const');
end;

procedure EnumKeyword(var E: TEvaluator; M: Boolean);
begin
  if M then
    EvaluatedKeyword(E.C^, M, MsgMatchedAtLeastOne, 'enum')
  else
    EvaluatedKeyword(E.C^, M, MsgDidNotMatchAtLeastOne, 'enum');
end;

procedure AnnotationKeywords(var E: TEvaluator; Id: TNodeID; Kind: Byte);
var
  I: Int32;
begin
  for I := 0 to Length(E.P^.Annotations[Id]) - 1 do begin
    if E.P^.Annotations[Id][I].StringsOnly and (Kind <> KindString) then
      Continue;
    IgnoredKeyword(E.C^, E.P^.Annotations[Id][I].Value, E.P^.Annotations[Id][I].Keyword);
  end;
end;

function EvalCore(var E: TEvaluator; Id: TNodeID; N: PSchemaNode; X: Int32; const Bits: TBitset): Boolean;
var
  D: PDocument;
  M: Boolean;
  Kind: Byte;
begin
  D := E.D;
  Result := True;
  if N^.HasType then begin
    M := TypeOK(N^.TypeMask, D^, X);
    if E.C <> nil then
      TypeKeyword(E, N, M)
    else if not M then
      Exit(False);
    Result := Result and M;
  end;
  if N^.ConstValue.IsSet then begin
    M := ValuesEqual(D^, X, E.P^.Documents[N^.ConstValue.Doc], N^.ConstValue.N);
    if E.C <> nil then
      ConstKeyword(E, N, M)
    else if not M then
      Exit(False);
    Result := Result and M;
  end;
  if N^.HasEnum then begin
    M := EvalEnum(E, N, X);
    if E.C <> nil then
      EnumKeyword(E, M)
    else if not M then
      Exit(False);
    Result := Result and M;
  end;
  Kind := DocKind(D^, X);
  M := True;
  if (Kind = KindNumber) and NodeHasNumberKeywords(N^) then
    M := EvalNumber(E, N, X)
  else if (Kind = KindString) and NodeHasStringKeywords(N^) then
    M := EvalString(E, N, X)
  else if (Kind = KindObject) and NodeHasObjectKeywords(N^) then
    M := EvalObject(E, Id, N, X, Bits)
  else if (Kind = KindArray) and NodeHasArrayKeywords(N^) then
    M := EvalArray(E, N, X, Bits);
  if not M and (E.C = nil) then
    Exit(False);
  Result := Result and M;
  M := EvalInPlace(E, N, X, Bits);
  if not M and (E.C = nil) then
    Exit(False);
  Result := Result and M;
  if (Kind = KindObject) and (N^.UnevaluatedProperties >= 0) then
    M := EvalUnevaluatedProperties(E, N, X, Bits)
  else if (Kind = KindArray) and (N^.UnevaluatedItems >= 0) then
    M := EvalUnevaluatedItems(E, N, X, Bits);
  if not M and (E.C = nil) then
    Exit(False);
  Result := Result and M;
  if E.C <> nil then
    AnnotationKeywords(E, Id, Kind);
end;

initialization
  NoBranches := nil;
end.
