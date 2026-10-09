unit Corvus.JsonSchema.Compiler;

{$I corvus.inc}

{ Compiles loaded schema documents into a TSchemaNode graph and runs the compile-time analyses. A port of
  compiler.go of the Go module.

  The compiled schema is records in arrays, referred to by index. It holds no class instance and needs no Free.
  Besides the nodes it holds what the nodes refer to by index: the schema documents (a value of a node is a
  document's index and a value's index) and the compiled patterns.

  The Go source's maps become these:

    - the node of each (document, value) is an array for each document with an entry for every value (-1: no node);
    - the extra children of the nodes with a pending dynamic reference, for the reachability analysis, are an
      array with an entry for every node;
    - the known keywords are a sorted list, searched by halving;
    - the patterns of one schema are shared through a TStringTable from a pattern's text to its index (the Go
      source shares them through a cache for the whole process).

  A node is a record in an array that grows while a node is compiled (compiling a node creates the nodes of its
  children), where the Go source has a pointer to it. So a node is compiled in a local record and stored when it is
  done, and nothing keeps a reference into the array across a call that can add a node. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Loader;

type
  { TAnnotationSource is what a node's annotations are computed from, on the first evaluation with a collector. }
  TAnnotationSource = record
    IsSet: Boolean;
    { The index of the document in the compiled schema's Documents. }
    Doc: Int32;
    Value: Int32;
    Vocab: UInt32;
    Content: Boolean;
  end;
  TAnnotationSourceArray = array of TAnnotationSource;

  PPattern = ^TPattern;
  TPatternArray = array of TPattern;

  { TCompiledSchema is the compiled program: the node graph, its entry node and whether it keeps a dynamic scope. }
  TCompiledSchema = record
    Nodes: TSchemaNodeArray;
    Root: TNodeID;
    UsesDynamicScope: Boolean;
    AnnotationSources: TAnnotationSourceArray;
    { The schema documents, which the values of the nodes and the annotation sources refer to by index. }
    Documents: TDocumentArray;
    { The compiled patterns, which the nodes refer to by index. }
    Patterns: TPatternArray;
  end;

{ CompileDocument compiles a schema document. False, with the reason, when the schema cannot be compiled (an
  unresolvable reference, an invalid pattern). }
function CompileDocument(const Schema: TDocument; const Options: TCompileOptions; out Compiled: TCompiledSchema;
  out Error: UTF8String): Boolean;
{ CompileFromURI compiles the schema document at a URI, fetched through the document resolver of the options (or
  one of the standard metaschemas). }
function CompileFromURI(const Uri: UTF8String; const Options: TCompileOptions; out Compiled: TCompiledSchema;
  out Error: UTF8String): Boolean;

{ CollectAnnotationEntries lists the annotation keywords of the schema object E of D, in the order the C# compiler
  records them (collectAnnotations of the Go source, which the evaluator calls on the first evaluation with a
  collector). }
function CollectAnnotationEntries(const D: TDocument; E: Int32; Dialect: TDialect; Voc: UInt32;
  Content, AssertFormatSet: Boolean): TAnnotationEntryArray;

{ UIntValue is a non-negative integer keyword value. }
function UIntValue(const D: TDocument; N: Int32): TOptCount;
function TypeMaskOf(const D: TDocument; N: Int32): Byte;
{ StringItems are the strings of an array (other items are skipped). }
function StringItems(const D: TDocument; AArray: Int32): TUTF8StringArray;
function ContainsString(const List: TUTF8StringArray; const S: UTF8String): Boolean;
{ IsKnownKeyword reports a keyword the compiler handles itself. Anything else is an unknown keyword (an annotation
  from 2019-09). }
function IsKnownKeyword(const Name: UTF8String): Boolean;

{ PatternAt is a pointer to A[I]: one comparison of the index with the array's length, then the element's address
  (see Corvus.JsonSchema.Checked). }
function PatternAt(const A: TPatternArray; I: NativeInt): PPattern; inline;

implementation

uses
  Corvus.JsonSchema.Checked,
  Corvus.JsonSchema.Numbers,
  Corvus.JsonSchema.Formats,
  Corvus.JsonSchema.Uri;

{$PUSH}
{$R-}

function PatternAt(const A: TPatternArray; I: NativeInt): PPattern; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

{$POP}

const
  { KnownKeywords are the keywords CompileNode handles itself, in byte order. }
  KnownKeywords: array[0..62] of UTF8String = (
    '$anchor', '$comment', '$defs', '$dynamicAnchor', '$dynamicRef', '$id', '$recursiveAnchor', '$recursiveRef',
    '$ref', '$schema', '$vocabulary', 'additionalItems', 'additionalProperties', 'allOf', 'anyOf', 'const',
    'contains', 'contentEncoding', 'contentMediaType', 'contentSchema', 'default', 'definitions', 'dependencies',
    'dependentRequired', 'dependentSchemas', 'deprecated', 'description', 'else', 'enum', 'examples',
    'exclusiveMaximum', 'exclusiveMinimum', 'format', 'id', 'if', 'items', 'maxContains', 'maxItems', 'maxLength',
    'maxProperties', 'maximum', 'minContains', 'minItems', 'minLength', 'minProperties', 'minimum', 'multipleOf',
    'not', 'oneOf', 'pattern', 'patternProperties', 'prefixItems', 'properties', 'propertyNames', 'readOnly',
    'required', 'then', 'title', 'type', 'unevaluatedItems', 'unevaluatedProperties', 'uniqueItems', 'writeOnly');

  Two63: Double = 9223372036854775808.0;

  { The constraint a branch's properties[X] places on the value: positive (const/enum of primitives), negative (a
    string not in an enum) or wildcard (anything else). }
  ClassWildcard = 0;
  ClassPositive = 1;
  ClassNegative = 2;

type
  TBooleanArray = array of Boolean;
  TEdges = array of TNodeIDArray;

  TPendingDynamicRef = record
    Node: TNodeID;
    Anchor: UTF8String;
    IsRecursive: Boolean;
    InitialTarget: TSchemaTarget;
    SeenResources: TBooleanArray;
    Candidates: TResourceNodeArray;
  end;
  TPendingDynamicRefArray = array of TPendingDynamicRef;

  TSchemaCompiler = record
    Loader: TSchemaLoader;
    Options: TCompileOptions;
    Nodes: TSchemaNodeArray;
    NodesLen: Int32;
    { The target of each node. }
    Targets: TSchemaTargetArray;
    { The node of each value of each document, or -1. }
    NodeOf: array of TInt32Array;
    WorklistHead: Int32;
    PendingDynamicRefs: TPendingDynamicRefArray;
    AnnotationSources: TAnnotationSourceArray;
    EntryNode: TNodeID;
    { The patterns compiled so far, and the index of each by its text. }
    Patterns: TPatternArray;
    PatternsLen: Int32;
    PatternIndex: TStringTable;
    { Why the function that has just returned False failed. }
    Error: UTF8String;
  end;

  TValueClass = record
    Kind: Int32;
    Values: TDiscriminatorValueArray;
  end;

function IsKnownKeyword(const Name: UTF8String): Boolean;
var
  Lo, Hi, Mid, C: Int32;
begin
  Result := True;
  Lo := 0;
  Hi := High(KnownKeywords);
  while Lo <= Hi do begin
    Mid := Lo + (Hi - Lo) div 2;
    C := CompareUtf8(KnownKeywords[Mid], Name);
    if C = 0 then
      Exit;
    if C < 0 then
      Lo := Mid + 1
    else
      Hi := Mid - 1;
  end;
  Result := False;
end;

function UIntValue(const D: TDocument; N: Int32): TOptCount;
var
  V: Int64;
  F: Double;
begin
  Result.IsSet := False;
  Result.N := 0;
  if (N < 0) or (DocKind(D, N) <> KindNumber) then
    Exit;
  case DocFlags(D, N) of
    NumInt: begin
      V := Int64(DocData(D, N));
      if V >= 0 then begin
        Result.IsSet := True;
        Result.N := UInt64(V);
      end;
    end;
    NumUint: begin
      Result.IsSet := True;
      Result.N := DocData(D, N);
    end;
  else
    F := DoubleFromBits(DocData(D, N));
    if (F >= 0) and (F = FloorOf(F)) and (F < 1.8e19) then begin
      Result.IsSet := True;
      { Trunc gives an Int64: a value from 2^63 up is converted less 2^63, which is exact. }
      if F < Two63 then
        Result.N := UInt64(Trunc(F))
      else
        Result.N := UInt64(Trunc(F - Two63)) + UInt64($8000000000000000);
    end;
  end;
end;

function TypeMaskOf(const D: TDocument; N: Int32): Byte;
var
  S: UTF8String;
begin
  Result := 0;
  if DocKind(D, N) <> KindString then
    Exit;
  S := DocStrCopy(D, N);
  if S = 'null' then
    Result := TypeNull
  else if S = 'boolean' then
    Result := TypeBoolean
  else if S = 'object' then
    Result := TypeObject
  else if S = 'array' then
    Result := TypeArray
  else if S = 'number' then
    Result := TypeNumber
  else if S = 'string' then
    Result := TypeString
  else if S = 'integer' then
    Result := TypeInteger;
end;

function StringItems(const D: TDocument; AArray: Int32): TUTF8StringArray;
var
  C, I, N: Int32;
begin
  Result := nil;
  SetLength(Result, DocCount(D, AArray));
  C := DocFirst(D, AArray);
  N := 0;
  for I := 0 to DocCount(D, AArray) - 1 do
    if DocKind(D, C + I) = KindString then begin
      Result[N] := DocStrCopy(D, C + I);
      Inc(N);
    end;
  SetLength(Result, N);
end;

function ContainsString(const List: TUTF8StringArray; const S: UTF8String): Boolean;
var
  I: Int32;
begin
  Result := True;
  for I := 0 to Length(List) - 1 do
    if List[I] = S then
      Exit;
  Result := False;
end;

function Fail(var C: TSchemaCompiler; const Message: UTF8String): Boolean;
begin
  C.Error := Message;
  Result := False;
end;

function GetNode(var C: TSchemaCompiler; const Target: TSchemaTarget): TNodeID;
var
  Doc, I, Count: Int32;
begin
  Doc := Target.Document;
  if Doc >= Length(C.NodeOf) then
    SetLength(C.NodeOf, Length(C.Loader.Documents));
  if C.NodeOf[Doc] = nil then begin
    Count := Length(C.Loader.Documents[Doc].ResourceOf);
    SetLength(C.NodeOf[Doc], Count);
    for I := 0 to Count - 1 do
      C.NodeOf[Doc][I] := -1;
  end;
  Result := C.NodeOf[Doc][Target.Value];
  if Result >= 0 then
    Exit;
  Result := C.NodesLen;
  if C.NodesLen = Length(C.Nodes) then begin
    SetLength(C.Nodes, C.NodesLen * 2 + 16);
    SetLength(C.Targets, C.NodesLen * 2 + 16);
    SetLength(C.AnnotationSources, C.NodesLen * 2 + 16);
  end;
  C.Nodes[Result] := NewSchemaNode(Target.Resource, C.Loader.Resources[Target.Resource].Dialect, Target.Pointer);
  C.Targets[Result] := Target;
  C.AnnotationSources[Result] := Default(TAnnotationSource);
  Inc(C.NodesLen);
  C.NodeOf[Doc][Target.Value] := Result;
end;

function Child(var C: TSchemaCompiler; const Parent: TSchemaTarget; Value: Int32;
  const Relative: UTF8String): TNodeID;
var
  T: TSchemaTarget;
begin
  if not LoaderResourceOf(C.Loader, Parent.Document, Value, T.Resource) then
    T.Resource := Parent.Resource;
  T.Document := Parent.Document;
  T.Pointer := Parent.Pointer + Relative;
  T.Value := Value;
  Result := GetNode(C, T);
end;

function ChildArray(var C: TSchemaCompiler; const Parent: TSchemaTarget; const D: TDocument; Value: Int32;
  const Keyword: UTF8String; out Ids: TNodeIDArray): Boolean;
var
  First, I: Int32;
begin
  Ids := nil;
  Result := False;
  if not IsKind(D, Value, KindArray) then
    Exit;
  SetLength(Ids, DocCount(D, Value));
  First := DocFirst(D, Value);
  for I := 0 to Length(Ids) - 1 do
    Ids[I] := Child(C, Parent, First + I, '/' + Keyword + '/' + IntText(I));
  Result := True;
end;

{ CompilerPattern compiles a pattern, or fetches it when the schema has had it before: its index in the compiled
  schema's Patterns. False when it is not a valid ECMA-262 regular expression. }
function CompilerPattern(var C: TSchemaCompiler; const Source: UTF8String; out Index: Int32): Boolean;
var
  At: Int32;
  P: TPattern;
begin
  At := StringTableFind(C.PatternIndex, Source);
  if At >= 0 then begin
    Index := C.PatternIndex.Values[At];
    Result := True;
    Exit;
  end;
  Index := -1;
  Result := CompilePattern(Source, P);
  if not Result then
    Exit;
  if C.PatternsLen = Length(C.Patterns) then
    SetLength(C.Patterns, C.PatternsLen * 2 + 8);
  Index := C.PatternsLen;
  C.Patterns[Index] := P;
  Inc(C.PatternsLen);
  StringTableAdd(C.PatternIndex, Source, Index, '');
end;

function ResolveOrFail(var C: TSchemaCompiler; const Target: TSchemaTarget; const Reference: UTF8String;
  out Resolved: TSchemaTarget): Boolean;
var
  Found: Boolean;
begin
  Result := TryResolveReference(C.Loader, Target.Resource, Reference, Resolved, Found);
  if not Result then begin
    C.Error := C.Loader.Error;
    Exit;
  end;
  if not Found then
    Result := Fail(C, 'Unable to resolve reference ''' + Reference + ''' from '''
      + C.Loader.Resources[Target.Resource].Uri + '''.');
end;

{ CompileRef resolves a $ref: the node it refers to. }
function CompileRef(var C: TSchemaCompiler; const Target: TSchemaTarget; const Reference: UTF8String;
  out Node: TNodeID): Boolean;
var
  Resolved: TSchemaTarget;
begin
  Node := NoNode;
  Result := ResolveOrFail(C, Target, Reference, Resolved);
  if Result then
    Node := GetNode(C, Resolved);
end;

function DynamicKeyword(IsRecursive: Boolean): UTF8String;
begin
  if IsRecursive then
    Result := '$recursiveRef'
  else
    Result := '$dynamicRef';
end;

{ CompileDynamicRef resolves a $dynamicRef or $recursiveRef of the node Id, which is being compiled in N. }
function CompileDynamicRef(var C: TSchemaCompiler; Id: TNodeID; const Target: TSchemaTarget;
  const Reference: UTF8String; IsRecursive: Boolean; var N: TSchemaNode): Boolean;
var
  Resolved: TSchemaTarget;
  UriPart, RawFragment, Fragment: UTF8String;
  Dynamic: Boolean;
  At, Count: Int32;
  Node: TNodeID;
begin
  Result := ResolveOrFail(C, Target, Reference, Resolved);
  if not Result then
    Exit;
  SplitFragment(Reference, UriPart, RawFragment);
  Fragment := DecodeFragment(RawFragment);
  Dynamic := False;
  if IsRecursive then
    Dynamic := C.Loader.Resources[Resolved.Resource].RecursiveAnchor
      and (Resolved.Pointer = C.Loader.Resources[Resolved.Resource].RootPointer)
  else if (Fragment <> '') and (Fragment[1] <> '/') then begin
    At := StringTableFind(C.Loader.Resources[Resolved.Resource].DynamicAnchors, Fragment);
    Dynamic := (At >= 0) and (C.Loader.Resources[Resolved.Resource].DynamicAnchors.Texts[At] = Resolved.Pointer);
  end;
  if not Dynamic then begin
    { A static reference, kept apart from any sibling $ref (both apply). }
    Node := GetNode(C, Resolved);
    N.StaticDynamicRef := Node;
    N.StaticDynamicKeyword := DynamicKeyword(IsRecursive);
    Exit;
  end;
  Count := Length(C.PendingDynamicRefs);
  SetLength(C.PendingDynamicRefs, Count + 1);
  C.PendingDynamicRefs[Count].Node := Id;
  C.PendingDynamicRefs[Count].Anchor := Fragment;
  C.PendingDynamicRefs[Count].IsRecursive := IsRecursive;
  C.PendingDynamicRefs[Count].InitialTarget := Resolved;
  C.PendingDynamicRefs[Count].SeenResources := nil;
  C.PendingDynamicRefs[Count].Candidates := nil;
end;

procedure AddDependency(var List: TDependencyEntryArray; const Keyword, Name: UTF8String; HasRequired: Boolean;
  const Required: TUTF8StringArray; Schema: TNodeID);
var
  N: Int32;
begin
  N := Length(List);
  SetLength(List, N + 1);
  List[N].Keyword := Keyword;
  List[N].Name := Name;
  List[N].HasRequired := HasRequired;
  List[N].Required := Required;
  List[N].Schema := Schema;
end;

procedure CompileDependencySchemas(var C: TSchemaCompiler; const Target: TSchemaTarget; const D: TDocument;
  E: Int32; Dialect: TDialect; var List: TDependencyEntryArray);
var
  Deps, K, I, V: Int32;
  Name: UTF8String;
  Node: TNodeID;
begin
  Deps := DocProperty(D, E, 'dependencies');
  if IsKind(D, Deps, KindObject) then begin
    K := DocFirst(D, Deps);
    for I := 0 to DocCount(D, Deps) - 1 do begin
      Name := DocStrCopy(D, K + 2 * I);
      V := K + 2 * I + 1;
      if DocKind(D, V) = KindArray then
        AddDependency(List, KeywordDependencies, Name, True, StringItems(D, V), NoNode)
      else begin
        Node := Child(C, Target, V, '/dependencies/' + EscapePointerToken(Name));
        AddDependency(List, KeywordDependencies, Name, False, nil, Node);
      end;
    end;
  end;
  if Dialect >= Draft201909 then begin
    Deps := DocProperty(D, E, 'dependentSchemas');
    if IsKind(D, Deps, KindObject) then begin
      K := DocFirst(D, Deps);
      for I := 0 to DocCount(D, Deps) - 1 do begin
        Name := DocStrCopy(D, K + 2 * I);
        Node := Child(C, Target, K + 2 * I + 1, '/dependentSchemas/' + EscapePointerToken(Name));
        AddDependency(List, KeywordDependentSchemas, Name, False, nil, Node);
      end;
    end;
  end;
end;

{ KeywordOrder is the position of a keyword among the properties of the schema object E, or the largest number
  when it has no such property. }
function KeywordOrder(const D: TDocument; E: Int32; const Keyword: UTF8String): Int32;
var
  K, I: Int32;
  B: TBytes;
begin
  B := BytesOf(Keyword);
  K := DocFirst(D, E);
  for I := 0 to DocCount(D, E) - 1 do
    if DocStrEquals(D, K + 2 * I, B, 0, Length(B)) then begin
      Result := I;
      Exit;
    end;
  Result := High(Int32);
end;

{ CompileObject compiles the schema object E of D, the value of Target, into N, which is the node Id. }
function CompileObject(var C: TSchemaCompiler; Id: TNodeID; const Target: TSchemaTarget; const D: TDocument;
  E: Int32; var N: TSchemaNode): Boolean;
var
  Dialect: TDialect;
  Voc: UInt32;
  Legacy, Applicator, Validation, Unevaluated, Content, FormatAssert, HasDependencies, Base64, Json: Boolean;
  Dependencies: TDependencyEntryArray;
  Entry: TDependencyEntry;
  R, Name, Source, F, Encoding, MediaType: UTF8String;
  V, K, I, J, First, Count: Int32;
  Node: TNodeID;
  Opt: TOptCount;
  Orders: TInt32Array;
  Order: Int32;

  function Get(const Name: UTF8String): Int32;
  begin
    Result := DocProperty(D, E, Name);
  end;

  function Has(const Name: UTF8String): Boolean;
  begin
    Result := DocProperty(D, E, Name) >= 0;
  end;

  function Single(const Name: UTF8String): TNodeID;
  var
    At: Int32;
  begin
    At := Get(Name);
    if At >= 0 then
      Result := Child(C, Target, At, '/' + EscapePointerToken(Name))
    else
      Result := NoNode;
  end;

  function Num(const Name: UTF8String): TValueRef;
  var
    At: Int32;
  begin
    At := Get(Name);
    if IsKind(D, At, KindNumber) then
      Result := ValueRefOf(Target.Document, At)
    else begin
      Result.IsSet := False;
      Result.Doc := 0;
      Result.N := 0;
    end;
  end;

  function IsTrue(const Name: UTF8String): Boolean;
  var
    At: Int32;
  begin
    At := Get(Name);
    Result := IsKind(D, At, KindBool) and DocBoolean(D, At);
  end;

begin
  Result := False;
  Dialect := C.Loader.Resources[Target.Resource].Dialect;
  Voc := C.Loader.Resources[Target.Resource].Vocabularies;
  Legacy := DialectIsLegacy(Dialect);

  if Legacy then
    if StringValue(D, Get('$ref'), R) then begin
      { In draft 7 and earlier, $ref replaces every sibling keyword. }
      Result := CompileRef(C, Target, R, Node);
      N.Ref := Node;
      Exit;
    end;

  Applicator := Legacy or (Voc and VocabApplicator <> 0);
  Validation := Legacy or (Voc and VocabValidation <> 0);
  Unevaluated := Voc and VocabUnevaluated <> 0;
  if Dialect = Draft201909 then
    Unevaluated := Applicator;
  Content := Legacy or (Voc and VocabContent <> 0);
  FormatAssert := (Legacy and C.Options.AssertFormatInLegacyDrafts) or (Voc and VocabFormatAssertion <> 0);
  if C.Options.AssertFormat <> 0 then
    FormatAssert := C.Options.AssertFormat > 0;

  Dependencies := nil;
  HasDependencies := False;

  { References. }
  if StringValue(D, Get('$ref'), R) then begin
    if not CompileRef(C, Target, R, Node) then
      Exit;
    N.Ref := Node;
  end;
  if Dialect >= Draft202012 then
    if StringValue(D, Get('$dynamicRef'), R) then
      if not CompileDynamicRef(C, Id, Target, R, False, N) then
        Exit;
  if Dialect = Draft201909 then
    if StringValue(D, Get('$recursiveRef'), R) then
      if not CompileDynamicRef(C, Id, Target, R, True, N) then
        Exit;

  { Applicators. }
  if Applicator then begin
    N.HasAllOf := ChildArray(C, Target, D, Get('allOf'), 'allOf', N.AllOf);
    N.HasAnyOf := ChildArray(C, Target, D, Get('anyOf'), 'anyOf', N.AnyOf);
    N.HasOneOf := ChildArray(C, Target, D, Get('oneOf'), 'oneOf', N.OneOf);
    N.NotNode := Single('not');
    if Dialect >= Draft7 then begin
      N.IfNode := Single('if');
      if N.IfNode >= 0 then begin
        N.ThenNode := Single('then');
        N.ElseNode := Single('else');
      end;
    end;
    V := Get('properties');
    if IsKind(D, V, KindObject) then begin
      N.HasProperties := True;
      K := DocFirst(D, V);
      SetLength(N.Properties, DocCount(D, V));
      for I := 0 to DocCount(D, V) - 1 do begin
        Name := DocStrCopy(D, K + 2 * I);
        Node := Child(C, Target, K + 2 * I + 1, '/properties/' + EscapePointerToken(Name));
        N.Properties[I].Name := Name;
        N.Properties[I].Node := Node;
      end;
    end;
    V := Get('patternProperties');
    if IsKind(D, V, KindObject) then begin
      N.HasPatternProperties := True;
      K := DocFirst(D, V);
      SetLength(N.PatternProperties, DocCount(D, V));
      for I := 0 to DocCount(D, V) - 1 do begin
        Source := DocStrCopy(D, K + 2 * I);
        if not CompilerPattern(C, Source, J) then begin
          Fail(C, 'Invalid regular expression ''' + Source + ''' in patternProperties.');
          Exit;
        end;
        Node := Child(C, Target, K + 2 * I + 1, '/patternProperties/' + EscapePointerToken(Source));
        N.PatternProperties[I].Pattern := J;
        N.PatternProperties[I].Node := Node;
      end;
    end;
    N.AdditionalProperties := Single('additionalProperties');
    if Dialect >= Draft6 then begin
      N.PropertyNames := Single('propertyNames');
      N.Contains := Single('contains');
    end;

    { "dependencies" is honoured in every dialect: in 2019-09 and later it is an optional compatibility keyword. }
    if IsKind(D, Get('dependencies'), KindObject)
      or ((Dialect >= Draft201909) and IsKind(D, Get('dependentSchemas'), KindObject)) then begin
      CompileDependencySchemas(C, Target, D, E, Dialect, Dependencies);
      HasDependencies := True;
    end;

    { Array applicators. }
    if Dialect >= Draft202012 then begin
      N.HasPrefixItems := ChildArray(C, Target, D, Get('prefixItems'), 'prefixItems', N.PrefixItems);
      V := Get('items');
      if (V >= 0) and (DocKind(D, V) <> KindArray) then
        N.Items := Single('items');
    end else begin
      V := Get('items');
      if V >= 0 then begin
        if DocKind(D, V) = KindArray then begin
          N.HasPrefixItems := ChildArray(C, Target, D, V, 'items', N.PrefixItems);
          N.PrefixKeyword := 'items';
          N.Items := Single('additionalItems');
          if N.Items >= 0 then
            N.ItemsKeyword := 'additionalItems';
        end else
          N.Items := Single('items');
      end;
    end;
    N.ContainsMarksEvaluated := Dialect >= Draft202012;
  end;

  if Unevaluated and (Dialect >= Draft201909) then begin
    N.UnevaluatedProperties := Single('unevaluatedProperties');
    N.UnevaluatedItems := Single('unevaluatedItems');
  end;

  if Validation then begin
    V := Get('type');
    if V >= 0 then begin
      if DocKind(D, V) = KindArray then begin
        First := DocFirst(D, V);
        for I := 0 to DocCount(D, V) - 1 do
          N.TypeMask := N.TypeMask or TypeMaskOf(D, First + I);
      end else
        N.TypeMask := TypeMaskOf(D, V);
      N.HasType := True;
    end;
    if Dialect >= Draft6 then begin
      V := Get('const');
      if V >= 0 then
        N.ConstValue := ValueRefOf(Target.Document, V);
    end;
    V := Get('enum');
    if IsKind(D, V, KindArray) then begin
      N.HasEnum := True;
      First := DocFirst(D, V);
      SetLength(N.EnumValues, DocCount(D, V));
      for I := 0 to DocCount(D, V) - 1 do
        N.EnumValues[I] := ValueRefOf(Target.Document, First + I);
    end;
    V := Get('required');
    if IsKind(D, V, KindArray) then begin
      N.HasRequired := True;
      N.RequiredList := StringItems(D, V);
      SetLength(N.Required, Length(N.RequiredList));
      Count := 0;
      for I := 0 to Length(N.RequiredList) - 1 do begin
        J := 0;
        while (J < Count) and (N.Required[J] <> N.RequiredList[I]) do
          Inc(J);
        if J = Count then begin
          N.Required[Count] := N.RequiredList[I];
          Inc(Count);
        end;
      end;
      SetLength(N.Required, Count);
    end;
    if Dialect >= Draft201909 then begin
      V := Get('dependentRequired');
      if IsKind(D, V, KindObject) then begin
        HasDependencies := True;
        K := DocFirst(D, V);
        for I := 0 to DocCount(D, V) - 1 do
          if DocKind(D, K + 2 * I + 1) = KindArray then
            AddDependency(Dependencies, KeywordDependentRequired, DocStrCopy(D, K + 2 * I), True,
              StringItems(D, K + 2 * I + 1), NoNode);
      end;
    end;
    N.MinProperties := UIntValue(D, Get('minProperties'));
    N.MaxProperties := UIntValue(D, Get('maxProperties'));
    N.MinItems := UIntValue(D, Get('minItems'));
    N.MaxItems := UIntValue(D, Get('maxItems'));
    V := Get('uniqueItems');
    if IsKind(D, V, KindBool) then
      N.UniqueItems := DocBoolean(D, V);
    N.MinLength := UIntValue(D, Get('minLength'));
    N.MaxLength := UIntValue(D, Get('maxLength'));
    if StringValue(D, Get('pattern'), Source) then begin
      if not CompilerPattern(C, Source, J) then begin
        Fail(C, 'Invalid regular expression ''' + Source + ''' in pattern.');
        Exit;
      end;
      N.Pattern := J;
    end;
    N.MultipleOf := Num('multipleOf');
    if N.MultipleOf.IsSet then begin
      V := N.MultipleOf.N;
      N.HasDivisor := True;
      N.Divisor := NewDivisor(DocFlags(D, V), DocData(D, V), D.Source, DocNumberStart(D, V));
    end;
    if Dialect = Draft4 then begin
      { Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive. }
      if IsTrue('exclusiveMaximum') then
        N.ExclusiveMaximum := Num('maximum')
      else
        N.Maximum := Num('maximum');
      if IsTrue('exclusiveMinimum') then
        N.ExclusiveMinimum := Num('minimum')
      else
        N.Minimum := Num('minimum');
    end else begin
      N.Maximum := Num('maximum');
      N.Minimum := Num('minimum');
      N.ExclusiveMaximum := Num('exclusiveMaximum');
      N.ExclusiveMinimum := Num('exclusiveMinimum');
    end;
    if (Dialect >= Draft201909) and (N.Contains >= 0) then begin
      if Has('minContains') then begin
        N.MinContains := 1;
        Opt := UIntValue(D, Get('minContains'));
        if Opt.IsSet then
          N.MinContains := Opt.N;
      end;
      if Has('maxContains') then
        N.MaxContains := UIntValue(D, Get('maxContains'));
    end;
  end;

  if StringValue(D, Get('format'), F) then begin
    N.HasFormat := True;
    N.Format := F;
    N.FormatKind := FormatKindOf(F, Ord(Dialect));
    N.AssertFormat := FormatAssert;
  end;

  { Content keywords are asserted only in draft 7 (and annotations elsewhere). }
  if Content and (Dialect >= Draft7) and (Has('contentEncoding') or Has('contentMediaType')) then begin
    StringValue(D, Get('contentEncoding'), Encoding);
    StringValue(D, Get('contentMediaType'), MediaType);
    Base64 := Encoding = 'base64';
    Json := MediaType = 'application/json';
    if Base64 and Json then
      N.Content := ContentBase64JSON
    else if Base64 then
      N.Content := ContentBase64
    else if Json then
      N.Content := ContentJSON;
    N.AssertContent := (Dialect = Draft7) and C.Options.AssertContent and (N.Content <> ContentNone);
  end;

  if HasDependencies then begin
    { In the order the three keywords appear in the schema (a stable sort, by insertion). }
    Orders := nil;
    SetLength(Orders, Length(Dependencies));
    for I := 0 to Length(Dependencies) - 1 do
      Orders[I] := KeywordOrder(D, E, Dependencies[I].Keyword);
    for I := 1 to Length(Dependencies) - 1 do begin
      Entry := Dependencies[I];
      Order := Orders[I];
      J := I - 1;
      while (J >= 0) and (Orders[J] > Order) do begin
        Dependencies[J + 1] := Dependencies[J];
        Orders[J + 1] := Orders[J];
        Dec(J);
      end;
      Dependencies[J + 1] := Entry;
      Orders[J + 1] := Order;
    end;
  end;
  N.Dependencies := Dependencies;
  N.HasDependencies := HasDependencies;
  C.AnnotationSources[Id].IsSet := True;
  C.AnnotationSources[Id].Doc := Target.Document;
  C.AnnotationSources[Id].Value := E;
  C.AnnotationSources[Id].Vocab := Voc;
  C.AnnotationSources[Id].Content := Content;
  Result := True;
end;

function CompileNode(var C: TSchemaCompiler; Id: TNodeID; const Target: TSchemaTarget): Boolean;
var
  D: TDocument;
  E: Int32;
  N: TSchemaNode;
begin
  { Copies: the document's record, which keeps its arrays while the loader's list of documents grows, and the
    node, which is stored again when it is compiled (the list of nodes grows as the node's children are found). }
  D := C.Loader.Documents[Target.Document].Doc;
  E := Target.Value;
  N := C.Nodes[Id];
  Result := True;
  case DocKind(D, E) of
    KindBool: begin
      N.AlwaysTrue := DocBoolean(D, E);
      N.AlwaysFalse := not N.AlwaysTrue;
    end;
    KindObject: Result := CompileObject(C, Id, Target, D, E, N);
  else
    N.AlwaysTrue := True;
  end;
  C.Nodes[Id] := N;
end;

function DrainWorklist(var C: TSchemaCompiler): Boolean;
var
  Id: Int32;
  Target: TSchemaTarget;
begin
  Result := False;
  while C.WorklistHead < C.NodesLen do begin
    Id := C.WorklistHead;
    Inc(C.WorklistHead);
    { A copy: the list of targets grows while the node is compiled. }
    Target := C.Targets[Id];
    if not CompileNode(C, Id, Target) then
      Exit;
  end;
  Result := True;
end;

function TargetIn(const C: TSchemaCompiler; Resource: Int32; const Pointer: UTF8String): TSchemaTarget;
var
  Fragment, Root: UTF8String;
begin
  Root := C.Loader.Resources[Resource].RootPointer;
  Fragment := '';
  if Pointer <> Root then
    Fragment := Copy(Pointer, Length(Root) + 1, Length(Pointer) - Length(Root));
  if not TryResolveFragment(C.Loader, Resource, Fragment, Result) then
    Result := RootTarget(C.Loader, Resource);
end;

function ExpandDynamicRefs(var C: TSchemaCompiler): Boolean;
var
  P, Resource, Before, At, Count: Int32;
  Pointer: UTF8String;
  Node: TNodeID;
begin
  Result := False;
  for P := 0 to Length(C.PendingDynamicRefs) - 1 do
    for Resource := 0 to Length(C.Loader.Resources) - 1 do begin
      Count := Length(C.PendingDynamicRefs[P].SeenResources);
      if Count <= Resource then begin
        SetLength(C.PendingDynamicRefs[P].SeenResources, Resource + 1);
        for At := Count to Resource do
          C.PendingDynamicRefs[P].SeenResources[At] := False;
      end;
      if C.PendingDynamicRefs[P].SeenResources[Resource] then
        Continue;
      C.PendingDynamicRefs[P].SeenResources[Resource] := True;
      if C.PendingDynamicRefs[P].IsRecursive then begin
        if not C.Loader.Resources[Resource].RecursiveAnchor then
          Continue;
        Pointer := C.Loader.Resources[Resource].RootPointer;
      end else begin
        At := StringTableFind(C.Loader.Resources[Resource].DynamicAnchors, C.PendingDynamicRefs[P].Anchor);
        if At < 0 then
          Continue;
        Pointer := C.Loader.Resources[Resource].DynamicAnchors.Texts[At];
      end;
      Before := C.NodesLen;
      Node := GetNode(C, TargetIn(C, Resource, Pointer));
      Result := Result or (C.NodesLen <> Before);
      Count := Length(C.PendingDynamicRefs[P].Candidates);
      SetLength(C.PendingDynamicRefs[P].Candidates, Count + 1);
      C.PendingDynamicRefs[P].Candidates[Count].Resource := Resource;
      C.PendingDynamicRefs[P].Candidates[Count].Node := Node;
    end;
end;

{ ComputeReachability marks the nodes reachable from the entry, counting every candidate of a pending dynamic
  reference as a child. }
function ComputeReachability(var C: TSchemaCompiler; const Pending: TPendingDynamicRefArray): TBooleanArray;
var
  Extra: TEdges;
  Stack, Children: TNodeIDArray;
  StackLen, P, I, N, Pass: Int32;
  Id, Kid, Node: TNodeID;
begin
  Extra := nil;
  for P := 0 to Length(Pending) - 1 do begin
    Node := GetNode(C, Pending[P].InitialTarget);
    if Length(Extra) < C.NodesLen then
      SetLength(Extra, C.NodesLen);
    N := Length(Extra[Pending[P].Node]);
    SetLength(Extra[Pending[P].Node], N + 1 + Length(Pending[P].Candidates));
    Extra[Pending[P].Node][N] := Node;
    for I := 0 to Length(Pending[P].Candidates) - 1 do
      Extra[Pending[P].Node][N + 1 + I] := Pending[P].Candidates[I].Node;
  end;
  if Length(Extra) < C.NodesLen then
    SetLength(Extra, C.NodesLen);
  Result := nil;
  SetLength(Result, C.NodesLen);
  for I := 0 to C.NodesLen - 1 do
    Result[I] := False;
  Stack := nil;
  SetLength(Stack, C.NodesLen + 1);
  Stack[0] := C.EntryNode;
  StackLen := 1;
  Result[C.EntryNode] := True;
  while StackLen > 0 do begin
    Dec(StackLen);
    Id := Stack[StackLen];
    for Pass := 0 to 1 do begin
      if Pass = 0 then
        Children := NodeChildren(C.Nodes[Id])
      else
        Children := Extra[Id];
      for I := 0 to Length(Children) - 1 do begin
        Kid := Children[I];
        if (Kid < Length(Result)) and not Result[Kid] then begin
          Result[Kid] := True;
          { Each node is pushed once, so the stack has room. }
          Stack[StackLen] := Kid;
          Inc(StackLen);
        end;
      end;
    end;
  end;
end;

procedure SetStatic(var C: TSchemaCompiler; const P: TPendingDynamicRef; T: TNodeID);
begin
  C.Nodes[P.Node].StaticDynamicRef := T;
  C.Nodes[P.Node].StaticDynamicKeyword := DynamicKeyword(P.IsRecursive);
end;

function FinalizeDynamicRefs(var C: TSchemaCompiler): Boolean;
var
  Reachable: TBooleanArray;
  HasReachable: Boolean;
  Pending: TPendingDynamicRefArray;
  P, I, EntryResource: Int32;
  Fallback, Uniform: TNodeID;
begin
  Reachable := nil;
  HasReachable := False;
  Pending := C.PendingDynamicRefs;
  C.PendingDynamicRefs := nil;
  for P := 0 to Length(Pending) - 1 do begin
    Fallback := GetNode(C, Pending[P].InitialTarget);
    if Length(Pending[P].Candidates) <= 1 then begin
      { Only the initial target's resource defines the anchor: resolution is static. }
      SetStatic(C, Pending[P], Fallback);
      Continue;
    end;
    { The dynamic scope is searched outermost first and its outermost entry is always the resource evaluation
      started in. When the entry resource defines the anchor, that target is the answer on every path. }
    if not HasReachable then begin
      Reachable := ComputeReachability(C, Pending);
      HasReachable := True;
    end;
    if not Reachable[Pending[P].Node] then begin
      SetStatic(C, Pending[P], Fallback);
      Continue;
    end;
    EntryResource := C.Nodes[C.EntryNode].ResourceID;
    Uniform := NoNode;
    for I := 0 to Length(Pending[P].Candidates) - 1 do
      if Pending[P].Candidates[I].Resource = EntryResource then begin
        Uniform := Pending[P].Candidates[I].Node;
        Break;
      end;
    if Uniform >= 0 then begin
      SetStatic(C, Pending[P], Uniform);
      Continue;
    end;
    C.Nodes[Pending[P].Node].HasDynamicRef := True;
    C.Nodes[Pending[P].Node].DynamicRef.IsRecursive := Pending[P].IsRecursive;
    C.Nodes[Pending[P].Node].DynamicRef.Fallback := Fallback;
    C.Nodes[Pending[P].Node].DynamicRef.ByResource := Pending[P].Candidates;
  end;
  Result := DrainWorklist(C);
end;

function CompileAll(var C: TSchemaCompiler): Boolean;
begin
  Result := False;
  while True do begin
    if not DrainWorklist(C) then
      Exit;
    if Length(C.PendingDynamicRefs) = 0 then
      Break;
    if not ExpandDynamicRefs(C) and (C.WorklistHead = C.NodesLen) then
      Break;
  end;
  { Most schemas have no dynamic reference: skip the finalisation. }
  if Length(C.PendingDynamicRefs) <> 0 then
    Result := FinalizeDynamicRefs(C)
  else
    Result := True;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Analyses }

{ ComputeMarking works out which nodes can contribute evaluated-property/item annotations: a node marks if it has
  the keywords itself or any in-place child (not counting not) marks. HasEdges is False when no node has an
  in-place child (nil edges in the Go source). }
procedure ComputeMarking(var C: TSchemaCompiler; const Edges: TEdges; HasEdges: Boolean);
var
  Parents: TEdges;
  ParentCounts: TInt32Array;
  Children, Work: TNodeIDArray;
  I, J, Which, WorkLen, N: Int32;
  W, P, Kid: TNodeID;
  Marked: Boolean;
begin
  for I := 0 to C.NodesLen - 1 do begin
    C.Nodes[I].MarksProperties := C.Nodes[I].HasProperties or C.Nodes[I].HasPatternProperties
      or (C.Nodes[I].AdditionalProperties >= 0) or (C.Nodes[I].UnevaluatedProperties >= 0);
    C.Nodes[I].MarksItems := C.Nodes[I].HasPrefixItems or (C.Nodes[I].Items >= 0)
      or ((C.Nodes[I].Contains >= 0) and C.Nodes[I].ContainsMarksEvaluated) or (C.Nodes[I].UnevaluatedItems >= 0);
  end;
  if not HasEdges then
    Exit;
  Parents := nil;
  ParentCounts := nil;
  SetLength(Parents, C.NodesLen);
  SetLength(ParentCounts, C.NodesLen);
  for I := 0 to C.NodesLen - 1 do
    ParentCounts[I] := 0;
  for I := 0 to C.NodesLen - 1 do begin
    { not does not contribute annotations: nodes with one use the edges without it. }
    Children := Edges[I];
    if C.Nodes[I].NotNode >= 0 then
      Children := NodeInPlaceChildren(C.Nodes[I], False);
    for J := 0 to Length(Children) - 1 do begin
      Kid := Children[J];
      N := ParentCounts[Kid];
      if N = Length(Parents[Kid]) then
        SetLength(Parents[Kid], N * 2 + 4);
      Parents[Kid][N] := I;
      ParentCounts[Kid] := N + 1;
    end;
  end;
  Work := nil;
  SetLength(Work, C.NodesLen);
  for Which := 0 to 1 do begin
    WorkLen := 0;
    for I := 0 to C.NodesLen - 1 do begin
      if Which = 0 then
        Marked := C.Nodes[I].MarksProperties
      else
        Marked := C.Nodes[I].MarksItems;
      if Marked then begin
        Work[WorkLen] := I;
        Inc(WorkLen);
      end;
    end;
    while WorkLen > 0 do begin
      Dec(WorkLen);
      W := Work[WorkLen];
      for J := 0 to ParentCounts[W] - 1 do begin
        P := Parents[W][J];
        if Which = 0 then
          Marked := C.Nodes[P].MarksProperties
        else
          Marked := C.Nodes[P].MarksItems;
        if not Marked then begin
          if Which = 0 then
            C.Nodes[P].MarksProperties := True
          else
            C.Nodes[P].MarksItems := True;
          { A node is added when its flag is set, which happens once: the list has room. }
          Work[WorkLen] := P;
          Inc(WorkLen);
        end;
      end;
    end;
  end;
end;

{ ComputeInPlaceCycles marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need
  a depth guard. }
procedure ComputeInPlaceCycles(var C: TSchemaCompiler; const Edges: TEdges);
var
  Count, Start, StackLen, WorkLen, V, W, Parent, ComponentLen, I: Int32;
  Index, Low, Stack, WorkV, WorkEdge, Component: TInt32Array;
  OnStack: TBooleanArray;
  Next: Int32;
  SelfLoop: Boolean;
begin
  Count := C.NodesLen;
  Index := nil;
  Low := nil;
  Stack := nil;
  WorkV := nil;
  WorkEdge := nil;
  Component := nil;
  OnStack := nil;
  SetLength(Index, Count);
  SetLength(Low, Count);
  SetLength(OnStack, Count);
  { Each node is on each stack at most once. }
  SetLength(Stack, Count);
  SetLength(WorkV, Count);
  SetLength(WorkEdge, Count);
  SetLength(Component, Count);
  for I := 0 to Count - 1 do begin
    Index[I] := -1;
    Low[I] := 0;
    OnStack[I] := False;
  end;
  StackLen := 0;
  Next := 0;
  for Start := 0 to Count - 1 do begin
    if Index[Start] >= 0 then
      Continue;
    WorkV[0] := Start;
    WorkEdge[0] := 0;
    WorkLen := 1;
    Index[Start] := Next;
    Low[Start] := Next;
    Inc(Next);
    Stack[StackLen] := Start;
    Inc(StackLen);
    OnStack[Start] := True;
    while WorkLen > 0 do begin
      V := WorkV[WorkLen - 1];
      if WorkEdge[WorkLen - 1] < Length(Edges[V]) then begin
        W := Edges[V][WorkEdge[WorkLen - 1]];
        Inc(WorkEdge[WorkLen - 1]);
        if Index[W] < 0 then begin
          Index[W] := Next;
          Low[W] := Next;
          Inc(Next);
          Stack[StackLen] := W;
          Inc(StackLen);
          OnStack[W] := True;
          WorkV[WorkLen] := W;
          WorkEdge[WorkLen] := 0;
          Inc(WorkLen);
        end else if OnStack[W] then begin
          if Index[W] < Low[V] then
            Low[V] := Index[W];
        end;
        Continue;
      end;
      Dec(WorkLen);
      if WorkLen > 0 then begin
        Parent := WorkV[WorkLen - 1];
        if Low[V] < Low[Parent] then
          Low[Parent] := Low[V];
      end;
      if Low[V] = Index[V] then begin
        ComponentLen := 0;
        repeat
          Dec(StackLen);
          W := Stack[StackLen];
          OnStack[W] := False;
          Component[ComponentLen] := W;
          Inc(ComponentLen);
        until W = V;
        SelfLoop := False;
        for I := 0 to Length(Edges[V]) - 1 do
          SelfLoop := SelfLoop or (Edges[V][I] = V);
        if (ComponentLen > 1) or SelfLoop then
          for I := 0 to ComponentLen - 1 do
            C.Nodes[Component[I]].InPlaceCycle := True;
      end;
    end;
  end;
end;

{ EffectiveNode follows pure $ref nodes to the node that carries constraints. }
function EffectiveNode(const C: TSchemaCompiler; Id: TNodeID): TNodeID;
var
  Hops: Int32;
begin
  Result := Id;
  Hops := 0;
  while (Hops < 16) and NodeIsPureRef(C.Nodes[Result]) do begin
    Result := C.Nodes[Result].Ref;
    Inc(Hops);
  end;
end;

function Primitive(const C: TSchemaCompiler; const V: TValueRef; out P: TDiscriminatorValue): Boolean;
var
  Kind: Byte;
begin
  P := Default(TDiscriminatorValue);
  Kind := DocKind(C.Loader.Documents[V.Doc].Doc, V.N);
  case Kind of
    KindString: begin
      P.Kind := Kind;
      P.Text := DocStrCopy(C.Loader.Documents[V.Doc].Doc, V.N);
      Result := True;
    end;
    KindNumber, KindBool, KindNull: begin
      P.Kind := Kind;
      P.Value := V;
      Result := True;
    end;
  else
    Result := False;
  end;
end;

function Primitives(const C: TSchemaCompiler; const Values: TValueRefArray): TDiscriminatorValueArray;
var
  I, N: Int32;
  P: TDiscriminatorValue;
begin
  Result := nil;
  SetLength(Result, Length(Values));
  N := 0;
  for I := 0 to Length(Values) - 1 do
    if Primitive(C, Values[I], P) then begin
      Result[N] := P;
      Inc(N);
    end;
  SetLength(Result, N);
end;

{ The schema documents, for comparing the values of discriminators. }
function CompilerDocuments(const C: TSchemaCompiler): TDocumentArray;
var
  I: Int32;
begin
  Result := nil;
  SetLength(Result, Length(C.Loader.Documents));
  for I := 0 to Length(Result) - 1 do
    Result[I] := C.Loader.Documents[I].Doc;
end;

function ClassContains(const Cl: TValueClass; const V: TDiscriminatorValue; const Docs: TDocumentArray): Boolean;
var
  I: Int32;
begin
  Result := True;
  for I := 0 to Length(Cl.Values) - 1 do
    if DiscriminatorValueSame(Cl.Values[I], V, Docs) then
      Exit;
  Result := False;
end;

function Classify(const C: TSchemaCompiler; Branch: TNodeID; const Name: UTF8String): TValueClass;
var
  Kid, P, NotId: TNodeID;
  I: Int32;
  V: TDiscriminatorValue;
  Prims: TDiscriminatorValueArray;
  AllStrings: Boolean;
begin
  Result.Kind := ClassWildcard;
  Result.Values := nil;
  Kid := NoNode;
  for I := 0 to Length(C.Nodes[Branch].Properties) - 1 do
    if C.Nodes[Branch].Properties[I].Name = Name then begin
      Kid := C.Nodes[Branch].Properties[I].Node;
      Break;
    end;
  if Kid < 0 then
    Exit;
  P := EffectiveNode(C, Kid);
  if C.Nodes[P].ConstValue.IsSet then
    if Primitive(C, C.Nodes[P].ConstValue, V) then begin
      Result.Kind := ClassPositive;
      SetLength(Result.Values, 1);
      Result.Values[0] := V;
      Exit;
    end;
  if C.Nodes[P].HasEnum and (Length(C.Nodes[P].EnumValues) <> 0) then begin
    Prims := Primitives(C, C.Nodes[P].EnumValues);
    if Length(Prims) = Length(C.Nodes[P].EnumValues) then begin
      Result.Kind := ClassPositive;
      Result.Values := Prims;
      Exit;
    end;
  end;
  if (C.Nodes[P].NotNode >= 0) and C.Nodes[P].HasType and (C.Nodes[P].TypeMask = TypeString) then begin
    NotId := EffectiveNode(C, C.Nodes[P].NotNode);
    if C.Nodes[NotId].HasEnum and not C.Nodes[NotId].HasType and not C.Nodes[NotId].ConstValue.IsSet
      and not NodeHasStringKeywords(C.Nodes[NotId]) and not NodeHasInPlaceApplicators(C.Nodes[NotId]) then begin
      AllStrings := True;
      for I := 0 to Length(C.Nodes[NotId].EnumValues) - 1 do
        AllStrings := AllStrings and (DocKind(C.Loader.Documents[C.Nodes[NotId].EnumValues[I].Doc].Doc,
          C.Nodes[NotId].EnumValues[I].N) = KindString);
      if AllStrings then begin
        Result.Kind := ClassNegative;
        Result.Values := Primitives(C, C.Nodes[NotId].EnumValues);
      end;
    end;
  end;
end;

{ BuildDiscriminator classifies branches by the constraint their properties[X] places on the value, and builds the
  table from values to branches. False when the branches have no discriminator. }
function BuildDiscriminator(const C: TSchemaCompiler; const Docs: TDocumentArray; const Branches: TNodeIDArray;
  out D: TDiscriminator): Boolean;
var
  Candidates: TUTF8StringArray;
  Classes: array of TValueClass;
  Values: TDiscriminatorValueArray;
  Eff: TNodeID;
  B, I, J, K, N, Constrained, ValuesLen, Count: Int32;
  Known, Contains, Selected: Boolean;
  Name: UTF8String;
begin
  D := Default(TDiscriminator);
  Result := False;
  Candidates := nil;
  for B := 0 to Length(Branches) - 1 do begin
    Eff := EffectiveNode(C, Branches[B]);
    if not C.Nodes[Eff].HasProperties then
      Continue;
    for I := 0 to Length(C.Nodes[Eff].Properties) - 1 do
      if Classify(C, Eff, C.Nodes[Eff].Properties[I].Name).Kind <> ClassWildcard then begin
        N := Length(Candidates);
        SetLength(Candidates, N + 1);
        Candidates[N] := C.Nodes[Eff].Properties[I].Name;
      end;
    Break;
  end;
  Classes := nil;
  SetLength(Classes, Length(Branches));
  for N := 0 to Length(Candidates) - 1 do begin
    Name := Candidates[N];
    Constrained := 0;
    for I := 0 to Length(Branches) - 1 do begin
      Classes[I] := Classify(C, EffectiveNode(C, Branches[I]), Name);
      if Classes[I].Kind <> ClassWildcard then
        Inc(Constrained);
    end;
    if Constrained < 2 then
      Continue;
    Values := nil;
    ValuesLen := 0;
    for I := 0 to Length(Classes) - 1 do
      for J := 0 to Length(Classes[I].Values) - 1 do begin
        Known := False;
        for K := 0 to ValuesLen - 1 do
          Known := Known or DiscriminatorValueSame(Values[K], Classes[I].Values[J], Docs);
        if not Known then begin
          if ValuesLen = Length(Values) then
            SetLength(Values, ValuesLen * 2 + 8);
          Values[ValuesLen] := Classes[I].Values[J];
          Inc(ValuesLen);
        end;
      end;
    D.PropertyName := Name;
    D.AllRequire := True;
    SetLength(D.Known, ValuesLen);
    for I := 0 to ValuesLen - 1 do begin
      D.Known[I].Value := Values[I];
      D.Known[I].Branches := nil;
      SetLength(D.Known[I].Branches, Length(Classes));
      Count := 0;
      for B := 0 to Length(Classes) - 1 do begin
        Contains := ClassContains(Classes[B], Values[I], Docs);
        Selected := True;
        case Classes[B].Kind of
          ClassPositive: Selected := Contains;
          ClassNegative: Selected := not Contains;
        end;
        if Selected then begin
          D.Known[I].Branches[Count] := UInt32(B);
          Inc(Count);
        end;
      end;
      SetLength(D.Known[I].Branches, Count);
    end;
    SetLength(D.Unknown, Length(Classes));
    Count := 0;
    for B := 0 to Length(Classes) - 1 do
      if Classes[B].Kind <> ClassPositive then begin
        D.Unknown[Count] := UInt32(B);
        Inc(Count);
      end;
    SetLength(D.Unknown, Count);
    for B := 0 to Length(Branches) - 1 do
      D.AllRequire := D.AllRequire and ContainsString(C.Nodes[EffectiveNode(C, Branches[B])].Required, Name);
    Result := True;
    Exit;
  end;
end;

procedure ComputeDiscriminators(var C: TSchemaCompiler);
var
  Docs: TDocumentArray;
  I: Int32;
  D: TDiscriminator;
begin
  Docs := CompilerDocuments(C);
  for I := 0 to C.NodesLen - 1 do begin
    if Length(C.Nodes[I].OneOf) > 1 then
      if BuildDiscriminator(C, Docs, C.Nodes[I].OneOf, D) then begin
        C.Nodes[I].HasOneOfDiscriminator := True;
        C.Nodes[I].OneOfDiscriminator := D;
      end;
    if Length(C.Nodes[I].AnyOf) > 1 then
      if BuildDiscriminator(C, Docs, C.Nodes[I].AnyOf, D) then begin
        C.Nodes[I].HasAnyOfDiscriminator := True;
        C.Nodes[I].AnyOfDiscriminator := D;
      end;
  end;
end;

procedure Analyse(var C: TSchemaCompiler);
var
  Edges: TEdges;
  InPlace, Branches: Boolean;
  I: Int32;
begin
  Edges := nil;
  SetLength(Edges, C.NodesLen);
  InPlace := False;
  Branches := False;
  for I := 0 to C.NodesLen - 1 do begin
    Edges[I] := NodeInPlaceChildren(C.Nodes[I], True);
    InPlace := InPlace or (Length(Edges[I]) <> 0);
    Branches := Branches or C.Nodes[I].HasOneOf or C.Nodes[I].HasAnyOf;
  end;
  if InPlace then begin
    ComputeMarking(C, Edges, True);
    ComputeInPlaceCycles(C, Edges);
  end else
    ComputeMarking(C, Edges, False);
  if Branches then
    ComputeDiscriminators(C);
end;

function CompileLoaded(var C: TSchemaCompiler; RootResource: Int32; out Compiled: TCompiledSchema): Boolean;
var
  Target, Resolved: TSchemaTarget;
  Found: Boolean;
  I: Int32;
begin
  Compiled := Default(TCompiledSchema);
  Result := False;
  Target := RootTarget(C.Loader, RootResource);
  if C.Options.HasEntryPoint then begin
    if not TryResolveReference(C.Loader, RootResource, C.Options.EntryPoint, Resolved, Found) then begin
      C.Error := C.Loader.Error;
      Exit;
    end;
    if not Found then begin
      Fail(C, 'Unable to resolve the entry point ''' + C.Options.EntryPoint + '''.');
      Exit;
    end;
    Target := Resolved;
  end;
  C.EntryNode := GetNode(C, Target);
  if not CompileAll(C) then
    Exit;
  Compiled.UsesDynamicScope := False;
  for I := 0 to C.NodesLen - 1 do
    Compiled.UsesDynamicScope := Compiled.UsesDynamicScope or C.Nodes[I].HasDynamicRef;
  Analyse(C);
  SetLength(C.Nodes, C.NodesLen);
  SetLength(C.AnnotationSources, C.NodesLen);
  SetLength(C.Patterns, C.PatternsLen);
  Compiled.Nodes := C.Nodes;
  Compiled.Root := C.EntryNode;
  Compiled.AnnotationSources := C.AnnotationSources;
  Compiled.Documents := CompilerDocuments(C);
  Compiled.Patterns := C.Patterns;
  Result := True;
end;

procedure NewSchemaCompiler(out C: TSchemaCompiler; const Options: TCompileOptions);
begin
  C := Default(TSchemaCompiler);
  C.Loader := NewSchemaLoader(Options);
  C.Options := Options;
end;

function CompileDocument(const Schema: TDocument; const Options: TCompileOptions; out Compiled: TCompiledSchema;
  out Error: UTF8String): Boolean;
var
  C: TSchemaCompiler;
  Root: Int32;
begin
  Error := '';
  Compiled := Default(TCompiledSchema);
  NewSchemaCompiler(C, Options);
  if not LoadRoot(C.Loader, Schema, Root) then begin
    Error := C.Loader.Error;
    Result := False;
    Exit;
  end;
  Result := CompileLoaded(C, Root, Compiled);
  if not Result then
    Error := C.Error;
end;

function CompileFromURI(const Uri: UTF8String; const Options: TCompileOptions; out Compiled: TCompiledSchema;
  out Error: UTF8String): Boolean;
var
  C: TSchemaCompiler;
  Root: Int32;
begin
  Error := '';
  Compiled := Default(TCompiledSchema);
  NewSchemaCompiler(C, Options);
  if not LoadRootFromURI(C.Loader, Uri, Root) then begin
    Error := C.Loader.Error;
    Result := False;
    Exit;
  end;
  Result := CompileLoaded(C, Root, Compiled);
  if not Result then
    Error := C.Error;
end;

{ ValueJson is the value N of D as JSON text. }
function ValueJson(const D: TDocument; N: Int32): UTF8String;
var
  Sub: TDocument;
begin
  { A document whose root is the value: it shares the arrays of D. }
  Sub := D;
  Sub.Root := N;
  Result := DocumentToJson(Sub);
end;

function CollectAnnotationEntries(const D: TDocument; E: Int32; Dialect: TDialect; Voc: UInt32;
  Content, AssertFormatSet: Boolean): TAnnotationEntryArray;
var
  Legacy, MetaData, FormatAnnotate: Boolean;
  K, I, Count: Int32;
  Name: UTF8String;
  Res: TAnnotationEntryArray;

  procedure Add(const Keyword: UTF8String; StringsOnly: Boolean);
  begin
    if Count = Length(Res) then
      SetLength(Res, Count * 2 + 4);
    Res[Count].Keyword := Keyword;
    Res[Count].Value := ValueJson(D, DocProperty(D, E, Keyword));
    Res[Count].StringsOnly := StringsOnly;
    Inc(Count);
  end;

  function Has(const Name: UTF8String): Boolean;
  begin
    Result := DocProperty(D, E, Name) >= 0;
  end;

begin
  Legacy := DialectIsLegacy(Dialect);
  MetaData := Legacy or (Voc and VocabMetaData <> 0);
  FormatAnnotate := Legacy or (Voc and (VocabFormatAnnotation or VocabFormatAssertion) <> 0) or AssertFormatSet;
  Res := nil;
  Count := 0;
  K := DocFirst(D, E);
  for I := 0 to DocCount(D, E) - 1 do begin
    Name := DocStrCopy(D, K + 2 * I);
    if (Name = 'title') or (Name = 'description') or (Name = 'default') then begin
      if MetaData then
        Add(Name, False);
    end else if Name = 'examples' then begin
      if MetaData and (Dialect >= Draft6) then
        Add(Name, False);
    end else if (Name = 'readOnly') or (Name = 'writeOnly') then begin
      if MetaData and (Dialect >= Draft7) then
        Add(Name, False);
    end else if Name = 'deprecated' then begin
      if MetaData and (Dialect >= Draft201909) then
        Add(Name, False);
    end else if Name = 'format' then begin
      if (DocKind(D, K + 2 * I + 1) = KindString) and FormatAnnotate then
        Add(Name, False);
    end else
      { Unknown keywords are collected as annotations from 2019-09 onwards. }
      if (Dialect >= Draft201909) and not IsKnownKeyword(Name) then
        Add(Name, False);
  end;
  if Content and (Dialect >= Draft7) then begin
    if Has('contentEncoding') then
      Add('contentEncoding', True);
    if Has('contentMediaType') then begin
      Add('contentMediaType', True);
      { contentSchema is only meaningful alongside contentMediaType. }
      if Has('contentSchema') and (Dialect >= Draft201909) then
        Add('contentSchema', True);
    end;
  end;
  SetLength(Res, Count);
  Result := Res;
end;

end.
