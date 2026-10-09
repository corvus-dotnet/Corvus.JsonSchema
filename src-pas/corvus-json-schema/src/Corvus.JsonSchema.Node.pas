unit Corvus.JsonSchema.Node;

{$I corvus.inc}

{ The compiled schema graph: one TSchemaNode per distinct (document, pointer), with keyword data digested in
  advance. A port of node.go of the Go module.

  Where the Go source holds a pointer, a node here holds an index or a value:

    - a value of a schema document (a constant, a bound) is the index of the document in the compiled schema's
      Documents and the index of the value in it;
    - a pattern is its index in the compiled schema's Patterns, or -1;
    - the multipleOf divisor, a dynamic reference and a discriminator are records in the node, each with a Boolean
      that says whether the node has one.

  Go names that are reserved words in Pascal are spelt out: not, if, then and else are NotNode, IfNode, ThenNode
  and ElseNode, and a discriminator's property is PropertyName. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Numbers,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Formats;

type
  { TNodeID is a node index in the compiled graph, or NoNode. }
  TNodeID = Int32;
  TNodeIDArray = array of TNodeID;

  { The schema documents of a compiled schema, which the values of its nodes refer to by index. }
  TDocumentArray = array of TDocument;

const
  NoNode = TNodeID(-1);

  { JSON types as bits. The first six are the kinds of a document's values. }
  TypeNull = KindNull;
  TypeBoolean = KindBool;
  TypeObject = KindObject;
  TypeArray = KindArray;
  TypeNumber = KindNumber;
  TypeString = KindString;
  TypeInteger = 64;

  { The keyword a dependency entry came from, which names its result rows. }
  KeywordDependencies = 'dependencies';
  KeywordDependentSchemas = 'dependentSchemas';
  KeywordDependentRequired = 'dependentRequired';

type
  TContentKind = (ContentNone, ContentBase64, ContentJSON, ContentBase64JSON);

  { TValueRef is a value in a schema document (a constant, a bound): the value N of the document Doc of the
    compiled schema. }
  TValueRef = record
    IsSet: Boolean;
    Doc: Int32;
    N: Int32;
  end;
  TValueRefArray = array of TValueRef;

  { TOptCount is an optional non-negative integer keyword value. }
  TOptCount = record
    IsSet: Boolean;
    N: UInt64;
  end;

  { TAnnotationEntry is an annotation-producing keyword and its value (reported in verbose results). }
  TAnnotationEntry = record
    Keyword: UTF8String;
    { The value as JSON text. }
    Value: UTF8String;
    { Reported only when the instance is a string (content keywords). }
    StringsOnly: Boolean;
  end;
  TAnnotationEntryArray = array of TAnnotationEntry;

  TNamedNode = record
    Name: UTF8String;
    Node: TNodeID;
  end;
  TNamedNodeArray = array of TNamedNode;

  TPatternProperty = record
    { The index of the pattern in the compiled schema's Patterns. }
    Pattern: Int32;
    Node: TNodeID;
  end;
  TPatternPropertyArray = array of TPatternProperty;

  TDependencyEntry = record
    Keyword: UTF8String;
    Name: UTF8String;
    HasRequired: Boolean;
    Required: TUTF8StringArray;
    Schema: TNodeID;
  end;
  TDependencyEntryArray = array of TDependencyEntry;

  TResourceNode = record
    Resource: Int32;
    Node: TNodeID;
  end;
  TResourceNodeArray = array of TResourceNode;

  { TDynamicRefTarget is a $dynamicRef/$recursiveRef that stays dynamic after compile-time analysis. }
  TDynamicRefTarget = record
    IsRecursive: Boolean;
    Fallback: TNodeID;
    { The node of the matching anchor in each resource that defines it. }
    ByResource: TResourceNodeArray;
  end;

  { TDiscriminatorValue is a discriminator value: one of the JSON scalars const and enum can name. }
  TDiscriminatorValue = record
    Kind: Byte;
    { A string's text. }
    Text: UTF8String;
    { A number, or a boolean. }
    Value: TValueRef;
  end;
  TDiscriminatorValueArray = array of TDiscriminatorValue;

  TDiscriminatorEntry = record
    Value: TDiscriminatorValue;
    { The branches (indexes into the keyword's list) the value can select. }
    Branches: TUInt32Array;
  end;
  TDiscriminatorEntryArray = array of TDiscriminatorEntry;

  { TDiscriminator selects oneOf/anyOf branches by the value of one property. }
  TDiscriminator = record
    PropertyName: UTF8String;
    { Known discriminator values and the branches each can select. }
    Known: TDiscriminatorEntryArray;
    { Branches that stay candidates for a value not in known (negative and wildcard branches). }
    Unknown: TUInt32Array;
    { Every branch requires the property, so its absence fails the keyword at once. }
    AllRequire: Boolean;
  end;

  TSchemaNode = record
    ResourceID: Int32;
    Dialect: TDialect;
    { The JSON pointer of the schema within its document. }
    Pointer: UTF8String;

    AlwaysTrue: Boolean;
    AlwaysFalse: Boolean;

    { Assertions. }
    TypeMask: Byte;
    HasType: Boolean;
    ConstValue: TValueRef;
    HasEnum: Boolean;
    EnumValues: TValueRefArray;

    { References. }
    Ref: TNodeID;
    { A $dynamicRef/$recursiveRef that compile-time analysis resolved statically, and its keyword. }
    StaticDynamicRef: TNodeID;
    StaticDynamicKeyword: UTF8String;
    { Whether the node has a dynamic reference (a nil dynamicRef in the Go source says it has not). }
    HasDynamicRef: Boolean;
    DynamicRef: TDynamicRefTarget;

    { In-place applicators. }
    HasAllOf, HasAnyOf, HasOneOf: Boolean;
    AllOf, AnyOf, OneOf: TNodeIDArray;
    NotNode, IfNode, ThenNode, ElseNode: TNodeID;

    { Objects. }
    HasProperties: Boolean;
    Properties: TNamedNodeArray;
    HasPatternProperties: Boolean;
    PatternProperties: TPatternPropertyArray;
    AdditionalProperties: TNodeID;
    PropertyNames: TNodeID;
    HasRequired: Boolean;
    { Without duplicates. }
    Required: TUTF8StringArray;
    { As written (duplicates kept), for results. }
    RequiredList: TUTF8StringArray;
    HasDependencies: Boolean;
    Dependencies: TDependencyEntryArray;
    MinProperties: TOptCount;
    MaxProperties: TOptCount;
    UnevaluatedProperties: TNodeID;

    { Arrays. }
    HasPrefixItems: Boolean;
    PrefixItems: TNodeIDArray;
    { The keywords behind prefixItems/items: prefixItems/items (2020-12) or items/additionalItems (legacy). }
    PrefixKeyword: UTF8String;
    ItemsKeyword: UTF8String;
    Items: TNodeID;
    Contains: TNodeID;
    MinContains: UInt64;
    MaxContains: TOptCount;
    ContainsMarksEvaluated: Boolean;
    MinItems: TOptCount;
    MaxItems: TOptCount;
    UniqueItems: Boolean;
    UnevaluatedItems: TNodeID;

    { Strings. }
    MinLength: TOptCount;
    MaxLength: TOptCount;
    { The index of the pattern in the compiled schema's Patterns, or -1 (a nil pattern in the Go source). }
    Pattern: Int32;
    HasFormat: Boolean;
    Format: UTF8String;
    FormatKind: TFormatKind;
    AssertFormat: Boolean;
    Content: TContentKind;
    AssertContent: Boolean;

    { Numbers. }
    Minimum, Maximum, ExclusiveMinimum, ExclusiveMaximum, MultipleOf: TValueRef;
    { The multipleOf divisor, digested, when HasDivisor (a nil divisor in the Go source says there is none). }
    HasDivisor: Boolean;
    Divisor: TDivisor;

    { Analysis. }
    MarksProperties: Boolean;
    MarksItems: Boolean;
    InPlaceCycle: Boolean;
    HasOneOfDiscriminator: Boolean;
    OneOfDiscriminator: TDiscriminator;
    HasAnyOfDiscriminator: Boolean;
    AnyOfDiscriminator: TDiscriminator;
  end;
  TSchemaNodeArray = array of TSchemaNode;

{ ValueRefOf is the value N of the document Doc. }
function ValueRefOf(Doc, N: Int32): TValueRef; inline;

{ DiscriminatorValueMatches reports whether the instance value X of D equals this one (numbers by value: 1 and 1.0
  are the same key). Docs are the schema documents of the compiled schema. }
function DiscriminatorValueMatches(const V: TDiscriminatorValue; const Docs: TDocumentArray; const D: TDocument;
  X: Int32): Boolean;
{ DiscriminatorValueSame is key equality, with numbers by value. }
function DiscriminatorValueSame(const V, Other: TDiscriminatorValue; const Docs: TDocumentArray): Boolean;

function NewSchemaNode(Resource: Int32; Dialect: TDialect; const Pointer: UTF8String): TSchemaNode;

{ NodeHasObjectKeywords reports keywords that apply only to objects. }
function NodeHasObjectKeywords(const N: TSchemaNode): Boolean;
function NodeHasArrayKeywords(const N: TSchemaNode): Boolean;
function NodeHasStringKeywords(const N: TSchemaNode): Boolean;
function NodeHasNumberKeywords(const N: TSchemaNode): Boolean;
function NodeHasDependencySchema(const N: TSchemaNode): Boolean;
function NodeHasInPlaceApplicators(const N: TSchemaNode): Boolean;
{ NodeIsPureRef reports a node that is nothing but $ref: no other keyword that asserts. }
function NodeIsPureRef(const N: TSchemaNode): Boolean;
{ NodeInPlaceChildren are the in-place children (the instance is evaluated at the same location). }
function NodeInPlaceChildren(const N: TSchemaNode; IncludeNot: Boolean): TNodeIDArray;
{ NodeChildren are every child node. }
function NodeChildren(const N: TSchemaNode): TNodeIDArray;

implementation

uses
  Corvus.JsonSchema.Values;

function ValueRefOf(Doc, N: Int32): TValueRef; inline;
begin
  Result.IsSet := True;
  Result.Doc := Doc;
  Result.N := N;
end;

function DiscriminatorValueMatches(const V: TDiscriminatorValue; const Docs: TDocumentArray; const D: TDocument;
  X: Int32): Boolean;
begin
  if DocKind(D, X) <> V.Kind then
    Result := False
  else if V.Kind = KindString then begin
    if DocStrInText(D, X) then
      Result := TextEqualsBytes(V.Text, D.Text, DocStrOffset(D, X), DocCount(D, X))
    else
      Result := TextEqualsBytes(V.Text, D.Source, DocStrOffset(D, X), DocCount(D, X));
  end else if V.Kind = KindNull then
    Result := True
  else
    Result := ValuesEqual(D, X, Docs[V.Value.Doc], V.Value.N);
end;

function DiscriminatorValueSame(const V, Other: TDiscriminatorValue; const Docs: TDocumentArray): Boolean;
begin
  if V.Kind <> Other.Kind then
    Result := False
  else if V.Kind = KindString then
    Result := V.Text = Other.Text
  else if V.Kind = KindNull then
    Result := True
  else
    Result := ValuesEqual(Docs[V.Value.Doc], V.Value.N, Docs[Other.Value.Doc], Other.Value.N);
end;

function NewSchemaNode(Resource: Int32; Dialect: TDialect; const Pointer: UTF8String): TSchemaNode;
begin
  Result := Default(TSchemaNode);
  Result.ResourceID := Resource;
  Result.Dialect := Dialect;
  Result.Pointer := Pointer;
  Result.Ref := NoNode;
  Result.StaticDynamicRef := NoNode;
  Result.StaticDynamicKeyword := '$dynamicRef';
  Result.DynamicRef.Fallback := NoNode;
  Result.NotNode := NoNode;
  Result.IfNode := NoNode;
  Result.ThenNode := NoNode;
  Result.ElseNode := NoNode;
  Result.AdditionalProperties := NoNode;
  Result.PropertyNames := NoNode;
  Result.UnevaluatedProperties := NoNode;
  Result.PrefixKeyword := 'prefixItems';
  Result.ItemsKeyword := 'items';
  Result.Items := NoNode;
  Result.Contains := NoNode;
  Result.MinContains := 1;
  Result.UnevaluatedItems := NoNode;
  Result.Pattern := -1;
end;

function NodeHasObjectKeywords(const N: TSchemaNode): Boolean;
begin
  Result := N.HasProperties or N.HasPatternProperties or (N.AdditionalProperties >= 0) or (N.PropertyNames >= 0)
    or N.HasRequired or N.HasDependencies or N.MinProperties.IsSet or N.MaxProperties.IsSet
    or (N.UnevaluatedProperties >= 0);
end;

function NodeHasArrayKeywords(const N: TSchemaNode): Boolean;
begin
  Result := N.HasPrefixItems or (N.Items >= 0) or (N.Contains >= 0) or N.MinItems.IsSet or N.MaxItems.IsSet
    or N.UniqueItems or (N.UnevaluatedItems >= 0);
end;

function NodeHasStringKeywords(const N: TSchemaNode): Boolean;
begin
  Result := N.MinLength.IsSet or N.MaxLength.IsSet or (N.Pattern >= 0)
    or (N.AssertFormat and N.HasFormat and not FormatKindIsNumeric(N.FormatKind)) or N.AssertContent;
end;

function NodeHasNumberKeywords(const N: TSchemaNode): Boolean;
begin
  Result := N.Minimum.IsSet or N.Maximum.IsSet or N.ExclusiveMinimum.IsSet or N.ExclusiveMaximum.IsSet
    or N.MultipleOf.IsSet or (N.AssertFormat and N.HasFormat and FormatKindIsNumeric(N.FormatKind));
end;

function NodeHasDependencySchema(const N: TSchemaNode): Boolean;
var
  I: Int32;
begin
  Result := True;
  for I := 0 to Length(N.Dependencies) - 1 do
    if N.Dependencies[I].Schema >= 0 then
      Exit;
  Result := False;
end;

function NodeHasInPlaceApplicators(const N: TSchemaNode): Boolean;
begin
  Result := (N.Ref >= 0) or (N.StaticDynamicRef >= 0) or N.HasDynamicRef or N.HasAllOf or N.HasAnyOf or N.HasOneOf
    or (N.NotNode >= 0) or (N.IfNode >= 0) or NodeHasDependencySchema(N);
end;

function NodeIsPureRef(const N: TSchemaNode): Boolean;
begin
  Result := (N.Ref >= 0) and not N.HasType and not N.ConstValue.IsSet and not N.HasEnum
    and not NodeHasObjectKeywords(N) and not NodeHasArrayKeywords(N) and not NodeHasStringKeywords(N)
    and not NodeHasNumberKeywords(N) and not N.HasDynamicRef and (N.StaticDynamicRef < 0) and not N.HasAllOf
    and not N.HasAnyOf and not N.HasOneOf and (N.NotNode < 0) and (N.IfNode < 0) and not N.HasDependencies;
end;

{ An output list that grows by doubling. }
type
  TNodeList = record
    Ids: TNodeIDArray;
    Len: Int32;
  end;

procedure Add(var L: TNodeList; Id: TNodeID);
begin
  if L.Len = Length(L.Ids) then
    SetLength(L.Ids, L.Len * 2 + 8);
  L.Ids[L.Len] := Id;
  Inc(L.Len);
end;

{ AppendNode adds the node when there is one. }
procedure AppendNode(var L: TNodeList; Id: TNodeID);
begin
  if Id >= 0 then
    Add(L, Id);
end;

procedure AddAll(var L: TNodeList; const Ids: TNodeIDArray);
var
  I: Int32;
begin
  for I := 0 to Length(Ids) - 1 do
    Add(L, Ids[I]);
end;

procedure InPlace(var L: TNodeList; const N: TSchemaNode; IncludeNot: Boolean);
var
  I: Int32;
begin
  AppendNode(L, N.Ref);
  AppendNode(L, N.StaticDynamicRef);
  if N.HasDynamicRef then begin
    Add(L, N.DynamicRef.Fallback);
    for I := 0 to Length(N.DynamicRef.ByResource) - 1 do
      Add(L, N.DynamicRef.ByResource[I].Node);
  end;
  AddAll(L, N.AllOf);
  AddAll(L, N.AnyOf);
  AddAll(L, N.OneOf);
  if IncludeNot then
    AppendNode(L, N.NotNode);
  AppendNode(L, N.IfNode);
  AppendNode(L, N.ThenNode);
  AppendNode(L, N.ElseNode);
  for I := 0 to Length(N.Dependencies) - 1 do
    AppendNode(L, N.Dependencies[I].Schema);
end;

function NodeInPlaceChildren(const N: TSchemaNode; IncludeNot: Boolean): TNodeIDArray;
var
  L: TNodeList;
begin
  L.Ids := nil;
  L.Len := 0;
  InPlace(L, N, IncludeNot);
  SetLength(L.Ids, L.Len);
  Result := L.Ids;
end;

function NodeChildren(const N: TSchemaNode): TNodeIDArray;
var
  L: TNodeList;
  I: Int32;
begin
  L.Ids := nil;
  L.Len := 0;
  InPlace(L, N, True);
  for I := 0 to Length(N.Properties) - 1 do
    Add(L, N.Properties[I].Node);
  for I := 0 to Length(N.PatternProperties) - 1 do
    Add(L, N.PatternProperties[I].Node);
  AppendNode(L, N.AdditionalProperties);
  AppendNode(L, N.PropertyNames);
  AppendNode(L, N.UnevaluatedProperties);
  AppendNode(L, N.Items);
  AppendNode(L, N.Contains);
  AppendNode(L, N.UnevaluatedItems);
  AddAll(L, N.PrefixItems);
  SetLength(L.Ids, L.Len);
  Result := L.Ids;
end;

end.
