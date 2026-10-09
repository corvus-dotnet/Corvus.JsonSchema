unit Corvus.JsonSchema.Plan;

{$I corvus.inc}

{ Fail-fast evaluation plans: each node compiled to only the keywords it has, as a flat list of operations, with
  the object keywords fused into one pass over the instance's properties (declared names resolved through a lookup
  table, required checked as a bit mask of the declared properties seen) and children that are nothing but a type
  check tested inline.

  A node that tracks evaluated properties or items (unevaluatedProperties, unevaluatedItems) runs through the
  general evaluator, whose children come back to their plans.

  A port of the compilation half of plan.go of the Go module, with the data of fused.go (a body holds its fused
  object plan, and a fused object plan may hold an object plan, so the types of the two files are declared
  together). The functions that build a fused plan are in Corvus.JsonSchema.Fused, and everything that evaluates
  (the second half of plan.go and of fused.go, and eval.go) is in Corvus.JsonSchema.Eval, because those functions
  call each other.

  Where the Go source holds a pointer that may be nil (a plan's body, a body's object plan, array plan and fused
  plan, a fused plan's flat object plan), the record is held in place with a Boolean that says whether it is
  there. A pattern is its index in the program's Patterns, and a value of a schema document is a TValueRef into the
  program's Documents, as in the compiled schema. The program itself (the program type of eval.go) is declared
  here, since the plans are built from it and it holds them. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Numbers,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Formats,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Compiler;

const
  { AnyType is every JSON type (a node without type). }
  AnyType = Byte($7F);

  MaxUInt64 = UInt64($FFFFFFFFFFFFFFFF);
  One64 = UInt64(1);

  { Plans with at most this many names may look them up in the instance instead of visiting its properties... }
  LookupNames = 4;
  { ...when the instance's properties times the names are at most this (each lookup scans the properties). }
  LookupBudget = 24;

  MaxFusedNames = 256;
  MaxFusedConditions = 64;
  MaxFusedContributors = 64;
  MaxFusedAltGroups = 8;

type
  PSchemaNode = ^TSchemaNode;
  TBooleanArray = array of Boolean;

  { TShape is the shape of a node's plan, as its callers enter it. }
  TShape = (
    { ShapeGeneral is anything else, or on an in-place cycle (entered through the guarded path). }
    ShapeGeneral,
    { ShapeTrivial has nothing to check beyond the types. }
    ShapeTrivial,
    { ShapeLeaf checks only its own value (const, enum, number and string keywords): no children, no calls. }
    ShapeLeaf,
    { ShapeStringEnum is only an enum of strings. }
    ShapeStringEnum,
    { ShapeStrings is only string keywords (length, pattern, format). }
    ShapeStrings,
    { ShapeObject is nothing but an object plan: an object value enters its loop directly. }
    ShapeObject,
    { ShapeArray is nothing but an array plan: an array value enters its loop directly. }
    ShapeArray,
    { ShapeApply is nothing but in-place applicators: straight to them. }
    ShapeApply,
    { ShapeFused has a fused object plan (see Corvus.JsonSchema.Fused): an object value goes to it directly, and a
      value of any other kind to the node's keywords. }
    ShapeFused);

  { TChild is a child application, with its type check hoisted so that a child that is only a type check needs no
    call. }
  TChild = record
    Id: TNodeID;
    { The types the child accepts (0 for false). }
    Types: Byte;
    { What the child's keywords come to, so that entering it skips the dispatch it does not need. }
    Shape: TShape;
    { For a child that is only a type test (ShapeTrivial), the types again, and 0 for any other child: a value whose
      kind is in Pass needs nothing more, which is one test where the child is applied. }
    Pass: Byte;
  end;
  PChild = ^TChild;
  TChildArray = array of TChild;

  TNumberOpKind = (NumberMinimum, NumberMaximum, NumberExclusiveMinimum, NumberExclusiveMaximum, NumberMultipleOf,
    NumberFormat);

  TFormatCheckOp = record
    Custom: TFormatValidator;
    Kind: TFormatKind;
    Legacy: Boolean;
  end;

  TNumberOp = record
    Kind: TNumberOpKind;
    { The bound's representation. }
    Flag: Byte;
    Data: UInt64;
    Divisor: TDivisor;
    Format: TFormatCheckOp;
  end;
  TNumberOpArray = array of TNumberOp;

  TStringOpKind = (StringLength, StringPattern, StringFormat, StringContent);

  TStringOp = record
    Kind: TStringOpKind;
    Min, Max: UInt64;
    { The index of the pattern in the program's Patterns. }
    Pattern: Int32;
    Format: TFormatCheckOp;
    Content: TContentKind;
  end;
  PStringOp = ^TStringOp;
  TStringOpArray = array of TStringOp;

  TOpKind = (
    OpConst,
    { OpEnumStrings is an enum of strings only. }
    OpEnumStrings,
    OpEnum,
    OpRef,
    OpAllOf,
    OpAnyOf,
    OpOneOf,
    OpNot,
    { OpDynamicRef is a $dynamicRef/$recursiveRef resolved against the dynamic scope at run time. }
    OpDynamicRef,
    OpIf);

  { TDiscriminatorIndex is a discriminator's known values by lookup: string values through a name table, the few
    others (numbers, booleans, null) by scan. }
  TDiscriminatorIndex = record
    Strings: TNames;
    { For each string in Strings, its entry in the discriminator's Known. }
    StringEntries: TUInt32Array;
    Others: TUInt32Array;
  end;

  { TBranches are anyOf/oneOf branches, dispatched by the instance's type: only the branches whose type admits it
    are tried, and a branch that is only a type check is decided without a call. }
  TBranches = record
    Children: TChildArray;
    { For each instance kind (by the position of its bit), the branches that can accept it. }
    ByKind: array[0..5] of TUInt32Array;
    { The discriminator, and its known values by lookup. }
    HasDiscriminator: Boolean;
    Discriminator: TDiscriminator;
    Index: TDiscriminatorIndex;
  end;

  TOp = record
    Kind: TOpKind;
    Value: TValueRef;
    Names: TNames;
    Values: TValueRefArray;
    Node: TNodeID;
    Children: TChildArray;
    Branches: TBranches;
    Dynamic: TDynamicRefTarget;
    ThenNode: TNodeID;
    ElseNode: TNodeID;
  end;
  POp = ^TOp;
  TOpArray = array of TOp;

  TPatternChild = record
    { The index of the pattern in the program's Patterns. }
    Pattern: Int32;
    Child: TChild;
  end;
  PPatternChild = ^TPatternChild;
  TPatternChildArray = array of TPatternChild;

  { TVisit is the property loop, specialised by which keywords apply. }
  TVisit = (
    { VisitNone: no keyword looks at the properties. }
    VisitNone,
    { VisitValues: only additionalProperties (a map): every value against one child. }
    VisitValues,
    { VisitNames: declared properties, and additionalProperties for the rest. }
    VisitNames,
    { VisitPattern: one patternProperties entry and nothing declared: each name against the pattern, else
      additionalProperties. }
    VisitPattern,
    { VisitGeneral: anything else (patternProperties, propertyNames). }
    VisitGeneral);

  TPlanDependency = record
    Name: UTF8String;
    Required: TUTF8StringArray;
    Schema: TNodeID;
    { The seen bits of the name and of the names it requires, when every one is a known name. }
    HasBits: Boolean;
    NameBit, RequiredBits: UInt64;
  end;

  TObjectPlan = record
    Min, Max: UInt64;
    { How the properties are visited (for properties, patternProperties, additionalProperties, propertyNames). }
    Visit: TVisit;
    { The declared names, then the names only required and dependencies mention (so that the pass sees them too):
      the first Declared have a schema. }
    Names: TNames;
    Declared: Int32;
    { Per name: the declared schema, or for the others the additionalProperties child (or nothing, NoChild). }
    Children: TChildArray;
    { Bit i set: name i is required (the names checked by the mask: at most 64 names in all). }
    RequiredMask: UInt64;
    { Required names checked by lookup (when the mask cannot cover them). }
    Required: TUTF8StringArray;
    Patterns: TPatternChildArray;
    { For each declared name, the patterns (indexes into Patterns) it matches, worked out at compile time: only
      undeclared names are tested against the patterns at run time. }
    NamePatterns: array of TUInt16Array;
    HasAdditional: Boolean;
    Additional: TChild;
    PropertyNames: TNodeID;
    Dependencies: array of TPlanDependency;
    { No required names by lookup and no dependencies: nothing after the property loop but the required mask. }
    RestFree: Boolean;
    { IsStrict is VisitNames and RestFree: the strict loop (RunStrictObject) decides it. }
    IsStrict: Boolean;
    { VisitNames with at most LookupNames names and no additionalProperties: undeclared properties need no visit,
      so a small object is decided by looking each name up in it (see VisitLookup). }
    Lookup: Boolean;
  end;
  PObjectPlan = ^TObjectPlan;

  TSimpleArray = record
    IsSet: Boolean;
    Min, Max: UInt64;
    Types: Byte;
  end;

  TArrayPlan = record
    Min, Max: UInt64;
    Prefix: TChildArray;
    HasItems: Boolean;
    Items: TChild;
    { contains: the node, and the bounds on the count. }
    Contains: TNodeID;
    MinContains: UInt64;
    MaxContains: TOptCount;
    Unique: Boolean;
    { Bounds and type-only items, nothing else: the items' type mask (AnyType: no test). }
    IsSimple: Boolean;
    Simple: Byte;
    { Items that are themselves simple arrays (GeoJSON's positions): their bounds and item types, checked inline. }
    HasNested: Boolean;
    Nested: TSimpleArray;
  end;
  PArrayPlan = ^TArrayPlan;

  { ------------------------------------------------------------------------------------------------------------------
    The fused object plan (the types of fused.go). }

  { TGate is a condition and the polarity under which something applies. Without IsSet, it always applies. }
  TGate = record
    IsSet: Boolean;
    Condition: UInt16;
    Polarity: Boolean;
  end;

  TFusedForbidden = record
    Gate: TGate;
    Names: TUInt16Array;
  end;

  TFusedAbsent = record
    Condition: UInt16;
    Pattern: Int32;
  end;

  TMaskedValue = record
    Value: TValueRef;
    Mask: UInt64;
  end;

  { TMergedTests is each constant any of an entry's constant tests allows, with the mask of the tests (by index in
    Tests) that allow it. Strings are looked up by name. Other constants are compared in turn. }
  TMergedTests = record
    Strings: TNames;
    StringMasks: TUInt64Array;
    Others: array of TMaskedValue;
    { The tests that are constant sets. The others (patterns) are tested one by one. }
    Keyed: UInt64;
  end;

  { TFusedApp is one child schema applying to a property on behalf of a contributor. }
  TFusedApp = record
    Contributor: UInt16;
    { Not set: true, which covers without a test. }
    HasChild: Boolean;
    Child: TChild;
    { Other contributors whose resolution of the property is the same test: applied once when any is active. }
    Others: TUInt16Array;
    { For an application under conditions: the conditions that apply it when they hold, and when they do not, over
      its contributors (see FusedApplies). }
    ThenMask, ElsMask: UInt64;
  end;
  PFusedApp = ^TFusedApp;
  TFusedAppArray = array of TFusedApp;

  TAltBranch = record
    IsSet: Boolean;
    Group: UInt16;
    Branch: UInt16;
  end;

  { TOptChild is a child schema, or true (not set), which covers without a test. }
  TOptChild = record
    IsSet: Boolean;
    Child: TChild;
  end;

  TFusedPattern = record
    Pattern: Int32;
    Child: TOptChild;
  end;
  PFusedPattern = ^TFusedPattern;
  TFusedPatternArray = array of TFusedPattern;

  TFusedContributor = record
    { The condition and the polarity under which it applies. }
    Condition: TGate;
    { The alternative group and the branch in it. }
    Alt: TAltBranch;
    Patterns: TFusedPatternArray;
    { additionalProperties. }
    HasAdditional: Boolean;
    Additional: TOptChild;
    Required: TUInt16Array;
    Min, Max: TOptCount;
    { The condition as a mask: the bit of the condition in ThenMask when it applies the contributor by holding, in
      ElsMask when by not holding. }
    ThenMask, ElsMask: UInt64;
  end;
  PFusedContributor = ^TFusedContributor;
  TFusedContributorArray = array of TFusedContributor;

  { TFusedCondition is an if the pass decides (required names and value tests), or the presence of a dependency's
    property. }
  TFusedCondition = record
    Required: TUInt16Array;
    { The enclosing condition and the polarity under which this one is reached. }
    Gate: TGate;
  end;
  PFusedCondition = ^TFusedCondition;
  TFusedConditionArray = array of TFusedCondition;

  { TValueTest is a condition's test on a property's value: absent passes, present must hold. }
  TValueTest = record
    Condition: UInt16;
    { One of these constants (strings, integers, booleans, null). None: the property must be absent. }
    IsPattern: Boolean;
    Allowed: TValueRefArray;
    Pattern: Int32;
    RequiresString: Boolean;
  end;
  PValueTest = ^TValueTest;
  TValueTestArray = array of TValueTest;

  TFusedEntry = record
    Apps: TFusedAppArray;
    { The conditions' tests on this property's value. }
    Tests: TValueTestArray;
    { The constant tests merged: one lookup decides them all. }
    HasMerged: Boolean;
    Merged: TMergedTests;
  end;
  PFusedEntry = ^TFusedEntry;
  TFusedEntryArray = array of TFusedEntry;

  TFusedAlternative = record
    Condition: TGate;
    ExactlyOne: Boolean;
    Branches: array of TUInt16Array;
  end;

  TFusedAltGroup = record
    ExactlyOne: Boolean;
    Count: UInt32;
  end;
  TFusedAltGroupArray = array of TFusedAltGroup;

  TFusedObject = record
    { The branches merge into one strict object loop: every one applies unconditionally with declared properties
      and required names only (and count bounds), and each name resolves to one schema. }
    HasFlat: Boolean;
    Flat: TObjectPlan;
    { Every property name any branch or condition knows, to its entry. }
    Names: TNames;
    Entries: TFusedEntryArray;
    { The branches, the node itself first, then the required lists of dependencies. }
    Contributors: TFusedContributorArray;
    Conditions: TFusedConditionArray;
    { Required-only oneOf/anyOf keywords, decided from the seen names after the pass. }
    Alternatives: array of TFusedAlternative;
    { oneOf/anyOf groups whose branches carry object keywords (each branch is a contributor). }
    AltGroups: TFusedAltGroupArray;
    (* not: {required: [...]}: names that must not all be present, under a condition. *)
    Forbidden: array of TFusedForbidden;
    (* Conditions that fail when some property name matches a pattern (an if with patternProperties: {P: false}),
      for names no entry knows. A known name that matches carries a test that never holds. *)
    Absent: array of TFusedAbsent;
    { The contributors with something to check after the pass (required names or count bounds). }
    Finals: TUInt16Array;
    { Some branch has pattern properties or additional properties, or some condition absent patterns, so names no
      entry knows need resolving. }
    ResolvesUnknown: Boolean;
    HasCountBounds: Boolean;
    HasUnevaluated: Boolean;
    Unevaluated: TChild;
  end;
  PFusedObject = ^TFusedObject;

  { ------------------------------------------------------------------------------------------------------------------
    Plans }

  { TBody is a node's keywords, grouped so that only the ones for the instance's type are looked at. }
  TBody = record
    { For an object instance, the whole node in one pass (see Corvus.JsonSchema.Fused), in place of everything
      below. }
    HasFused: Boolean;
    Fused: TFusedObject;
    { Instance kinds (TypeObject, TypeArray) whose evaluated properties or items the general evaluator must track
      (unevaluatedProperties, unevaluatedItems), and the node it runs. }
    General: Byte;
    Node: TNodeID;
    { unevaluatedItems with a static coverage: the items from this index on, against the child. }
    HasUnevaluatedItems: Boolean;
    UnevaluatedFrom: Int32;
    UnevaluatedChild: TChild;
    { const and enum. }
    Values: TOpArray;
    Number: TNumberOpArray;
    Str: TStringOpArray;
    HasObject: Boolean;
    ObjectPlan: TObjectPlan;
    HasArray: Boolean;
    ArrayPlan: TArrayPlan;
    { In-place applicators (children resolved through pure-$ref hops), in the general evaluator's order. }
    Apply: TOpArray;
  end;
  PBody = ^TBody;

  TPlan = record
    Types: Byte;
    { Evaluated in place under the depth guard (part of an in-place cycle). }
    Guard: Boolean;
    { Everything beyond the type check (not there for true, false and type-only schemas). }
    HasBody: Boolean;
    Body: TBody;
    { The body's shape, for entering it directly. }
    Shape: TShape;
    { The node as a child, for entering it by its id (see Run). }
    Self: TChild;
  end;
  PPlan = ^TPlan;
  TPlanArray = array of TPlan;

  { A node's types and shape, while the plans are compiled. }
  TSummaryEntry = record
    Types: Byte;
    Shape: TShape;
  end;
  TSummaryArray = array of TSummaryEntry;

  { TProgram is the compiled program an evaluator runs (the program type of eval.go). }
  TProgram = record
    Nodes: TSchemaNodeArray;
    Root: TNodeID;
    { The entry for fail-fast evaluation: the root's target. }
    Entry: TNodeID;
    UsesDynamicScope: Boolean;
    MaxDepth: Int32;
    { The options the schema was compiled with, for their custom formats. }
    Options: TCompileOptions;
    { For fail-fast evaluation: each node's target after following pure $ref hops. }
    FastTarget: TNodeIDArray;
    { Name lookup tables for nodes with many properties (a map from the name in the Go source). }
    HasPropertyMap: array of Boolean;
    PropertyMaps: array of TNames;
    AnnotationSources: TAnnotationSourceArray;
    { The annotation keywords of every node, once AnnotationsReady (computed on the first evaluation with a
      collector, under the validator's lock). }
    AnnotationsReady: Boolean;
    Annotations: array of TAnnotationEntryArray;
    AssertFormatSet: Boolean;
    { Fail-fast plans. }
    Plans: TPlanArray;
    { The schema documents and the compiled patterns, which the nodes and the plans refer to by index. }
    Documents: TDocumentArray;
    Patterns: TPatternArray;
  end;
  PProgram = ^TProgram;

{ ChildAt, OpAt, PatternChildAt, PlanAt, StringOpAt and the functions for the arrays of a fused object plan
  (EntryAt, FusedAppAt, FusedConditionAt, FusedContributorAt, FusedPatternAt, ValueTestAt) are pointers to A[I]: one
  comparison of the index with the array's length, then the element's address (see Corvus.JsonSchema.Checked). }
function ChildAt(const A: TChildArray; I: NativeInt): PChild; inline;
function OpAt(const A: TOpArray; I: NativeInt): POp; inline;
function PatternChildAt(const A: TPatternChildArray; I: NativeInt): PPatternChild; inline;
function PlanAt(const A: TPlanArray; I: NativeInt): PPlan; inline;
function StringOpAt(const A: TStringOpArray; I: NativeInt): PStringOp; inline;
function EntryAt(const A: TFusedEntryArray; I: NativeInt): PFusedEntry; inline;
function FusedAppAt(const A: TFusedAppArray; I: NativeInt): PFusedApp; inline;
function FusedConditionAt(const A: TFusedConditionArray; I: NativeInt): PFusedCondition; inline;
function FusedContributorAt(const A: TFusedContributorArray; I: NativeInt): PFusedContributor; inline;
function FusedPatternAt(const A: TFusedPatternArray; I: NativeInt): PFusedPattern; inline;
function ValueTestAt(const A: TValueTestArray; I: NativeInt): PValueTest; inline;

{ NoChild is a child that accepts anything and is never entered (an undeclared name without
  additionalProperties). }
function NoChild: TChild; inline;
{ SetShape sets a child's types and shape together, with what follows from them. }
procedure SetShape(var C: TChild; Types: Byte; S: TShape); inline;
{ ResolvedChild is a node as a child, from the summary of the plans as first compiled. }
function ResolvedChild(const Summary: TSummaryArray; Id: TNodeID): TChild;

{ KindIndex is the index of a kind in TBranches.ByKind. }
function KindIndex(Kind: Byte): Int32; inline;

{ CompilePlans compiles the plan of every node of the program. }
procedure CompilePlans(var P: TProgram);

{ ExpandTypes is a type mask with integer made explicit wherever number is (every integer is a number). }
function ExpandTypes(Mask: Byte): Byte;
{ MeetTypes is the types both masks admit. }
function MeetTypes(A, B: Byte): Byte;
{ TypeOnlyMask is the mask of a schema that tests only the type (true admits everything, false nothing). }
function TypeOnlyMask(const N: TSchemaNode; out Mask: Byte): Boolean;

{ TypeOK reports whether a value is of one of the types in a mask. A kind is its type bit. The mask test is small
  enough to inline at every call site, and the integer test, which few values reach, is a call. }
function TypeOK(Mask: Byte; const D: TDocument; X: NativeInt): Boolean; inline;
{ IntegerOK reports a number that the mask does not accept as a number but accepts as an integer. }
function IntegerOK(Mask: Byte; const D: TDocument; X: NativeInt): Boolean;

implementation

uses
  Corvus.JsonSchema.Checked,
  Corvus.JsonSchema.Fused;

{$PUSH}
{$R-}

function ChildAt(const A: TChildArray; I: NativeInt): PChild; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function OpAt(const A: TOpArray; I: NativeInt): POp; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function PatternChildAt(const A: TPatternChildArray; I: NativeInt): PPatternChild; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function StringOpAt(const A: TStringOpArray; I: NativeInt): PStringOp; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function PlanAt(const A: TPlanArray; I: NativeInt): PPlan; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function EntryAt(const A: TFusedEntryArray; I: NativeInt): PFusedEntry; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function FusedAppAt(const A: TFusedAppArray; I: NativeInt): PFusedApp; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function FusedConditionAt(const A: TFusedConditionArray; I: NativeInt): PFusedCondition; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function FusedContributorAt(const A: TFusedContributorArray; I: NativeInt): PFusedContributor; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function FusedPatternAt(const A: TFusedPatternArray; I: NativeInt): PFusedPattern; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

function ValueTestAt(const A: TValueTestArray; I: NativeInt): PValueTest; inline;
begin
  if NativeUInt(I) >= NativeUInt(Length(A)) then
    RangeFail;
  Result := @A[I];
end;

{$POP}

function NoChild: TChild; inline;
begin
  Result.Id := NoNode;
  Result.Types := AnyType;
  Result.Shape := ShapeTrivial;
  Result.Pass := AnyType;
end;

procedure SetShape(var C: TChild; Types: Byte; S: TShape); inline;
begin
  C.Types := Types;
  C.Shape := S;
  C.Pass := 0;
  if S = ShapeTrivial then
    C.Pass := Types;
end;

function ResolvedChild(const Summary: TSummaryArray; Id: TNodeID): TChild;
var
  S: TSummaryEntry;
begin
  S := Summary[Id];
  { Not ShapeObject: a node may still take a fused plan below. }
  if S.Shape = ShapeObject then
    S.Shape := ShapeGeneral;
  Result.Id := Id;
  SetShape(Result, S.Types, S.Shape);
end;

function TypeOK(Mask: Byte; const D: TDocument; X: NativeInt): Boolean; inline;
begin
  Result := (Mask and DocKind(D, X) <> 0) or IntegerOK(Mask, D, X);
end;

function IntegerOK(Mask: Byte; const D: TDocument; X: NativeInt): Boolean;
begin
  Result := (DocKind(D, X) = KindNumber) and (Mask and TypeInteger <> 0)
    and IsIntegerNumber(DocFlags(D, X), DocData(D, X));
end;

function NewDiscriminatorIndex(const D: TDiscriminator): TDiscriminatorIndex;
var
  List: TUTF8StringArray;
  I, N: Int32;
begin
  Result := Default(TDiscriminatorIndex);
  List := nil;
  for I := 0 to Length(D.Known) - 1 do
    if D.Known[I].Value.Kind = KindString then begin
      N := Length(List);
      SetLength(List, N + 1);
      List[N] := D.Known[I].Value.Text;
      SetLength(Result.StringEntries, N + 1);
      Result.StringEntries[N] := UInt32(I);
    end else begin
      N := Length(Result.Others);
      SetLength(Result.Others, N + 1);
      Result.Others[N] := UInt32(I);
    end;
  Result.Strings := NewNames(List);
end;

procedure NewBranches(var B: TBranches; const Children: TChildArray; HasDisc: Boolean; const Disc: TDiscriminator);
begin
  B := Default(TBranches);
  B.Children := Children;
  B.HasDiscriminator := HasDisc;
  if HasDisc then begin
    B.Discriminator := Disc;
    B.Index := NewDiscriminatorIndex(Disc);
  end;
end;

function KindIndex(Kind: Byte): Int32; inline;
begin
  { The trailing zeros of the kind, which is one bit. }
  case Kind of
    KindNull: Result := 0;
    KindBool: Result := 1;
    KindObject: Result := 2;
    KindArray: Result := 3;
    KindNumber: Result := 4;
  else
    Result := 5;
  end;
end;

{ Dispatch computes the dispatch table once the children's types are known. }
procedure Dispatch(var B: TBranches);
const
  KindTypes: array[0..5] of Byte = (TypeNull, TypeBoolean, TypeObject, TypeArray, TypeNumber or TypeInteger,
    TypeString);
var
  K, I, N: Int32;
begin
  for K := 0 to 5 do begin
    B.ByKind[K] := nil;
    N := 0;
    SetLength(B.ByKind[K], Length(B.Children));
    for I := 0 to Length(B.Children) - 1 do
      if B.Children[I].Types and KindTypes[K] <> 0 then begin
        B.ByKind[K][N] := UInt32(I);
        Inc(N);
      end;
    SetLength(B.ByKind[K], N);
  end;
end;

{ ---------------------------------------------------------------------------------------------------------------------
  Compilation }

function ExpandTypes(Mask: Byte): Byte;
begin
  if Mask and TypeNumber <> 0 then
    Result := Mask or TypeInteger
  else
    Result := Mask;
end;

function MeetTypes(A, B: Byte): Byte;
var
  M: Byte;
begin
  M := ExpandTypes(A) and ExpandTypes(B);
  { number in both keeps number. integer alone stays integer. }
  if (A and B and TypeNumber = 0) and (M and TypeNumber <> 0) then
    Result := M and Byte($FF xor TypeNumber)
  else
    Result := M;
end;

function TypeOnlyMask(const N: TSchemaNode; out Mask: Byte): Boolean;
begin
  if N.AlwaysTrue then begin
    Mask := AnyType;
    Exit(True);
  end;
  if N.AlwaysFalse then begin
    Mask := 0;
    Exit(True);
  end;
  Mask := N.TypeMask;
  Result := N.HasType and not N.InPlaceCycle and not N.ConstValue.IsSet and not N.HasEnum
    and not NodeHasNumberKeywords(N) and not NodeHasStringKeywords(N) and not NodeHasObjectKeywords(N)
    and not NodeHasArrayKeywords(N) and not NodeHasInPlaceApplicators(N) and not N.HasDynamicRef;
end;

{ TypeUnion is the union of anyOf/oneOf branches that all test only the type. For oneOf, only when no two branches
  admit a common value (so that "exactly one" is "any"). }
function TypeUnion(const P: TProgram; const List: TNodeIDArray; ExactlyOne: Boolean; out Union: Byte): Boolean;
var
  Masks: array of Byte;
  I, J: Int32;
begin
  Union := 0;
  Masks := nil;
  SetLength(Masks, Length(List));
  for I := 0 to Length(List) - 1 do
    if not TypeOnlyMask(P.Nodes[P.FastTarget[List[I]]], Masks[I]) then
      Exit(False);
  for I := 0 to Length(Masks) - 1 do begin
    if ExactlyOne then
      for J := I + 1 to Length(Masks) - 1 do
        if ExpandTypes(Masks[I]) and ExpandTypes(Masks[J]) <> 0 then begin
          Union := 0;
          Exit(False);
        end;
    Union := Union or Masks[I];
  end;
  Result := True;
end;

procedure AppendNode(var List: TNodeIDArray; Id: TNodeID);
var
  N: Int32;
begin
  if Id >= 0 then begin
    N := Length(List);
    SetLength(List, N + 1);
    List[N] := Id;
  end;
end;

{ StaticItemCoverage is the items a node's evaluation always marks evaluated, when that is static: the longest
  prefixItems of the node and the contributors it always applies ($ref and allOf chains) as From, or all of them
  when one has items (or, below the node, unevaluatedItems). Not ok when an in-place child that applies
  conditionally (anyOf, oneOf, if/then/else, a dependent schema, a dynamic reference) can mark items, or contains
  marks them. }
function StaticItemCoverage(const P: TProgram; Id: TNodeID; out All: Boolean; out From: Int32): Boolean;
type
  TEntry = record
    Id: TNodeID;
    Root: Boolean;
  end;
var
  Stack: array of TEntry;
  StackLen: Int32;
  Visited, Conditional, Always: TNodeIDArray;
  At: TEntry;
  N: PSchemaNode;
  I, J, K: Int32;
  Seen: Boolean;
begin
  All := False;
  From := 0;
  Stack := nil;
  SetLength(Stack, 8);
  Stack[0].Id := Id;
  Stack[0].Root := True;
  StackLen := 1;
  Visited := nil;
  AppendNode(Visited, Id);
  while StackLen > 0 do begin
    At := Stack[StackLen - 1];
    Dec(StackLen);
    N := @P.Nodes[At.Id];
    if N^.AlwaysTrue or N^.AlwaysFalse then
      Continue;
    if N^.InPlaceCycle or N^.HasDynamicRef or ((N^.Contains >= 0) and N^.ContainsMarksEvaluated) then begin
      All := False;
      From := 0;
      Exit(False);
    end;
    if Length(N^.PrefixItems) > From then
      From := Length(N^.PrefixItems);
    All := All or (N^.Items >= 0) or (not At.Root and (N^.UnevaluatedItems >= 0));
    Conditional := nil;
    for I := 0 to Length(N^.AnyOf) - 1 do
      AppendNode(Conditional, N^.AnyOf[I]);
    for I := 0 to Length(N^.OneOf) - 1 do
      AppendNode(Conditional, N^.OneOf[I]);
    AppendNode(Conditional, N^.IfNode);
    AppendNode(Conditional, N^.ThenNode);
    AppendNode(Conditional, N^.ElseNode);
    for I := 0 to Length(N^.Dependencies) - 1 do
      AppendNode(Conditional, N^.Dependencies[I].Schema);
    for I := 0 to Length(Conditional) - 1 do
      if P.Nodes[Conditional[I]].MarksItems then begin
        All := False;
        From := 0;
        Exit(False);
      end;
    Always := nil;
    AppendNode(Always, N^.Ref);
    AppendNode(Always, N^.StaticDynamicRef);
    for I := 0 to Length(N^.AllOf) - 1 do
      AppendNode(Always, N^.AllOf[I]);
    for I := 0 to Length(Always) - 1 do begin
      Seen := False;
      for J := 0 to Length(Visited) - 1 do
        Seen := Seen or (Visited[J] = Always[I]);
      if not Seen then begin
        AppendNode(Visited, Always[I]);
        if StackLen = Length(Stack) then
          SetLength(Stack, 2 * StackLen + 8);
        K := StackLen;
        Stack[K].Id := Always[I];
        Stack[K].Root := False;
        Inc(StackLen);
      end;
    end;
  end;
  Result := True;
end;

function ChildOf(const P: TProgram; Id: TNodeID): TChild;
begin
  Result.Id := P.FastTarget[Id];
  Result.Types := AnyType;
  Result.Shape := ShapeGeneral;
  Result.Pass := 0;
end;

function ChildrenOf(const P: TProgram; const List: TNodeIDArray): TChildArray;
var
  I: Int32;
begin
  Result := nil;
  SetLength(Result, Length(List));
  for I := 0 to Length(List) - 1 do
    Result[I] := ChildOf(P, List[I]);
end;

procedure PlanObject(const P: TProgram; const N: TSchemaNode; var O: TObjectPlan);
var
  Known, Extras: TUTF8StringArray;
  I, J, K, Count: Int32;
  Undeclared: TChild;
  HasNames, Visited, Ok, Found: Boolean;
  B, NameBit: UInt64;

  procedure Extra(const Name: UTF8String);
  var
    M: Int32;
  begin
    if not ContainsString(Known, Name) and not ContainsString(Extras, Name) then begin
      M := Length(Extras);
      SetLength(Extras, M + 1);
      Extras[M] := Name;
    end;
  end;

  function Bit(const Name: UTF8String; out Mask: UInt64): Boolean;
  var
    At: Int32;
  begin
    Mask := 0;
    if not Visited then
      Exit(False);
    At := NamesFindString(O.Names, Name);
    if At >= 0 then begin
      Mask := One64 shl At;
      Exit(True);
    end;
    Result := False;
  end;

begin
  O := Default(TObjectPlan);
  { Names only required or dependencies mention join the declared ones, up to 64 names in all (one mask word). }
  Known := nil;
  SetLength(Known, Length(N.Properties));
  for I := 0 to Length(N.Properties) - 1 do
    Known[I] := N.Properties[I].Name;
  Extras := nil;
  for I := 0 to Length(N.Required) - 1 do
    Extra(N.Required[I]);
  for I := 0 to Length(N.Dependencies) - 1 do begin
    Extra(N.Dependencies[I].Name);
    for J := 0 to Length(N.Dependencies[I].Required) - 1 do
      Extra(N.Dependencies[I].Required[J]);
  end;
  if Length(Known) + Length(Extras) <= 64 then begin
    Count := Length(Known);
    SetLength(Known, Count + Length(Extras));
    for I := 0 to Length(Extras) - 1 do
      Known[Count + I] := Extras[I];
  end;
  O.Max := MaxUInt64;
  O.Names := NewNames(Known);
  O.Declared := Length(N.Properties);
  O.PropertyNames := NoNode;
  if N.MinProperties.IsSet then
    O.Min := N.MinProperties.N;
  if N.MaxProperties.IsSet then
    O.Max := N.MaxProperties.N;
  Undeclared := NoChild;
  if N.AdditionalProperties >= 0 then begin
    Undeclared := ChildOf(P, N.AdditionalProperties);
    O.HasAdditional := True;
    O.Additional := Undeclared;
  end;
  SetLength(O.Children, Length(Known));
  for I := 0 to Length(Known) - 1 do
    if I < Length(N.Properties) then
      O.Children[I] := ChildOf(P, N.Properties[I].Node)
    else
      O.Children[I] := Undeclared;
  HasNames := Length(Known) <> 0;
  if (N.PropertyNames < 0) and not HasNames and (Length(N.PatternProperties) = 1) then
    O.Visit := VisitPattern
  else if N.HasPatternProperties or (N.PropertyNames >= 0) then
    O.Visit := VisitGeneral
  else if HasNames then
    O.Visit := VisitNames
  else if N.AdditionalProperties >= 0 then
    O.Visit := VisitValues;
  Visited := ((O.Visit = VisitNames) or (O.Visit = VisitGeneral)) and (Length(Known) <= 64);
  for I := 0 to Length(N.Required) - 1 do
    if Bit(N.Required[I], B) then
      O.RequiredMask := O.RequiredMask or B
    else begin
      K := Length(O.Required);
      SetLength(O.Required, K + 1);
      O.Required[K] := N.Required[I];
    end;
  SetLength(O.Dependencies, Length(N.Dependencies));
  for I := 0 to Length(N.Dependencies) - 1 do begin
    O.Dependencies[I] := Default(TPlanDependency);
    O.Dependencies[I].Name := N.Dependencies[I].Name;
    O.Dependencies[I].Required := N.Dependencies[I].Required;
    O.Dependencies[I].Schema := NoNode;
    if N.Dependencies[I].Schema >= 0 then
      O.Dependencies[I].Schema := P.FastTarget[N.Dependencies[I].Schema];
    Ok := Bit(N.Dependencies[I].Name, NameBit);
    for J := 0 to Length(N.Dependencies[I].Required) - 1 do begin
      Found := Bit(N.Dependencies[I].Required[J], B);
      Ok := Ok and Found;
      O.Dependencies[I].RequiredBits := O.Dependencies[I].RequiredBits or B;
    end;
    O.Dependencies[I].HasBits := Ok;
    O.Dependencies[I].NameBit := NameBit;
  end;
  SetLength(O.Patterns, Length(N.PatternProperties));
  for I := 0 to Length(N.PatternProperties) - 1 do begin
    O.Patterns[I].Pattern := N.PatternProperties[I].Pattern;
    O.Patterns[I].Child := ChildOf(P, N.PatternProperties[I].Node);
  end;
  SetLength(O.NamePatterns, Length(Known));
  for I := 0 to Length(Known) - 1 do
    for J := 0 to Length(N.PatternProperties) - 1 do
      if PatternMatchString(P.Patterns[N.PatternProperties[J].Pattern], Known[I]) then begin
        K := Length(O.NamePatterns[I]);
        SetLength(O.NamePatterns[I], K + 1);
        O.NamePatterns[I][K] := UInt16(J);
      end;
  if N.PropertyNames >= 0 then
    O.PropertyNames := P.FastTarget[N.PropertyNames];
  O.RestFree := (Length(O.Required) = 0) and not N.HasDependencies;
  O.IsStrict := (O.Visit = VisitNames) and O.RestFree;
  O.Lookup := (O.Visit = VisitNames) and (N.AdditionalProperties < 0) and (Length(Known) <= LookupNames);
end;

procedure PlanNode(const P: TProgram; Id: TNodeID; var Pl: TPlan);
var
  N: PSchemaNode;
  B: PBody;
  I, K, From: Int32;
  AllStrings, All: Boolean;
  List: TUTF8StringArray;
  F: TFormatCheckOp;
  OwnTypes, Union, Mask: Byte;

  function AddOp(var Ops: TOpArray; Kind: TOpKind): POp;
  var
    M: Int32;
  begin
    M := Length(Ops);
    SetLength(Ops, M + 1);
    Ops[M] := Default(TOp);
    Ops[M].Kind := Kind;
    Ops[M].Node := NoNode;
    Ops[M].ThenNode := NoNode;
    Ops[M].ElseNode := NoNode;
    Result := @Ops[M];
  end;

  function FormatCheckFor(Numeric: Boolean; out Check: TFormatCheckOp): Boolean;
  var
    Custom: TFormatValidator;
  begin
    Check := Default(TFormatCheckOp);
    if not N^.AssertFormat or not N^.HasFormat or (FormatKindIsNumeric(N^.FormatKind) <> Numeric) then
      Exit(False);
    Custom := CustomFormat(P.Options, N^.Format);
    if Assigned(Custom) then begin
      Check.Custom := Custom;
      Exit(True);
    end;
    if N^.FormatKind = FormatKindUnknown then
      Exit(False);
    Check.Kind := N^.FormatKind;
    Check.Legacy := N^.Dialect <= Draft6;
    Result := True;
  end;

  procedure Bound(Kind: TNumberOpKind; const V: TValueRef);
  var
    M: Int32;
  begin
    if V.IsSet then begin
      M := Length(B^.Number);
      SetLength(B^.Number, M + 1);
      B^.Number[M] := Default(TNumberOp);
      B^.Number[M].Kind := Kind;
      B^.Number[M].Flag := DocFlags(P.Documents[V.Doc], V.N);
      B^.Number[M].Data := DocData(P.Documents[V.Doc], V.N);
    end;
  end;

  function AddString(Kind: TStringOpKind): Int32;
  begin
    Result := Length(B^.Str);
    SetLength(B^.Str, Result + 1);
    B^.Str[Result] := Default(TStringOp);
    B^.Str[Result].Kind := Kind;
    B^.Str[Result].Pattern := -1;
  end;

begin
  N := @P.Nodes[Id];
  Pl := Default(TPlan);
  Pl.Guard := N^.InPlaceCycle;
  if N^.AlwaysTrue then begin
    Pl.Types := AnyType;
    Exit;
  end;
  if N^.AlwaysFalse then begin
    Pl.Types := 0;
    Exit;
  end;
  B := @Pl.Body;
  B^.Node := Id;

  if N^.ConstValue.IsSet then
    AddOp(B^.Values, OpConst)^.Value := N^.ConstValue;
  if N^.HasEnum then begin
    AllStrings := True;
    for I := 0 to Length(N^.EnumValues) - 1 do
      AllStrings := AllStrings and (DocKind(P.Documents[N^.EnumValues[I].Doc], N^.EnumValues[I].N) = KindString);
    if AllStrings then begin
      List := nil;
      SetLength(List, Length(N^.EnumValues));
      for I := 0 to Length(N^.EnumValues) - 1 do
        List[I] := DocStrCopy(P.Documents[N^.EnumValues[I].Doc], N^.EnumValues[I].N);
      AddOp(B^.Values, OpEnumStrings)^.Names := NewNames(List);
    end else
      AddOp(B^.Values, OpEnum)^.Values := N^.EnumValues;
  end;

  { Numbers. }
  if FormatCheckFor(True, F) then begin
    K := Length(B^.Number);
    SetLength(B^.Number, K + 1);
    B^.Number[K] := Default(TNumberOp);
    B^.Number[K].Kind := NumberFormat;
    B^.Number[K].Format := F;
  end;
  Bound(NumberMinimum, N^.Minimum);
  Bound(NumberMaximum, N^.Maximum);
  Bound(NumberExclusiveMinimum, N^.ExclusiveMinimum);
  Bound(NumberExclusiveMaximum, N^.ExclusiveMaximum);
  if N^.MultipleOf.IsSet then begin
    K := Length(B^.Number);
    SetLength(B^.Number, K + 1);
    B^.Number[K] := Default(TNumberOp);
    B^.Number[K].Kind := NumberMultipleOf;
    B^.Number[K].Divisor := N^.Divisor;
  end;

  { Strings. }
  if N^.MinLength.IsSet or N^.MaxLength.IsSet then begin
    K := AddString(StringLength);
    B^.Str[K].Max := MaxUInt64;
    if N^.MinLength.IsSet then
      B^.Str[K].Min := N^.MinLength.N;
    if N^.MaxLength.IsSet then
      B^.Str[K].Max := N^.MaxLength.N;
  end;
  if N^.Pattern >= 0 then begin
    K := AddString(StringPattern);
    B^.Str[K].Pattern := N^.Pattern;
  end;
  if FormatCheckFor(False, F) then begin
    K := AddString(StringFormat);
    B^.Str[K].Format := F;
  end;
  if N^.AssertContent then begin
    K := AddString(StringContent);
    B^.Str[K].Content := N^.Content;
  end;

  { Objects (unless the type excludes objects: then the keywords apply to nothing and must not cost the node its
    shape). }
  OwnTypes := AnyType;
  if N^.HasType then
    OwnTypes := N^.TypeMask;
  if NodeHasObjectKeywords(N^) and (OwnTypes and TypeObject <> 0) then begin
    B^.HasObject := True;
    PlanObject(P, N^, B^.ObjectPlan);
  end;

  { Arrays. }
  if NodeHasArrayKeywords(N^) and (OwnTypes and TypeArray <> 0) then begin
    B^.HasArray := True;
    B^.ArrayPlan.Max := MaxUInt64;
    B^.ArrayPlan.Contains := NoNode;
    B^.ArrayPlan.Unique := N^.UniqueItems;
    if N^.MinItems.IsSet then
      B^.ArrayPlan.Min := N^.MinItems.N;
    if N^.MaxItems.IsSet then
      B^.ArrayPlan.Max := N^.MaxItems.N;
    B^.ArrayPlan.Prefix := ChildrenOf(P, N^.PrefixItems);
    if N^.Items >= 0 then begin
      B^.ArrayPlan.HasItems := True;
      B^.ArrayPlan.Items := ChildOf(P, N^.Items);
    end;
    if N^.Contains >= 0 then begin
      B^.ArrayPlan.Contains := P.FastTarget[N^.Contains];
      B^.ArrayPlan.MinContains := N^.MinContains;
      B^.ArrayPlan.MaxContains := N^.MaxContains;
    end;
  end;

  { In-place applicators, in the general evaluator's order. }
  if N^.Ref >= 0 then
    AddOp(B^.Apply, OpRef)^.Node := P.FastTarget[N^.Ref];
  if N^.StaticDynamicRef >= 0 then
    AddOp(B^.Apply, OpRef)^.Node := P.FastTarget[N^.StaticDynamicRef];
  if N^.HasDynamicRef then begin
    if P.UsesDynamicScope then
      AddOp(B^.Apply, OpDynamicRef)^.Dynamic := N^.DynamicRef
    else
      { Without a dynamic scope the reference always takes its fallback. }
      AddOp(B^.Apply, OpRef)^.Node := P.FastTarget[N^.DynamicRef.Fallback];
  end;
  if N^.HasAllOf then
    AddOp(B^.Apply, OpAllOf)^.Children := ChildrenOf(P, N^.AllOf);
  { An anyOf of type-only branches, or a oneOf of type-only branches with no type in common, is one type test:
    it narrows the node's own types instead of adding a keyword. }
  Union := AnyType;
  if N^.HasAnyOf then begin
    if TypeUnion(P, N^.AnyOf, False, Mask) then
      Union := MeetTypes(Union, Mask)
    else
      NewBranches(AddOp(B^.Apply, OpAnyOf)^.Branches, ChildrenOf(P, N^.AnyOf), N^.HasAnyOfDiscriminator,
        N^.AnyOfDiscriminator);
  end;
  if N^.HasOneOf then begin
    if TypeUnion(P, N^.OneOf, True, Mask) then
      Union := MeetTypes(Union, Mask)
    else
      NewBranches(AddOp(B^.Apply, OpOneOf)^.Branches, ChildrenOf(P, N^.OneOf), N^.HasOneOfDiscriminator,
        N^.OneOfDiscriminator);
  end;
  if N^.NotNode >= 0 then
    AddOp(B^.Apply, OpNot)^.Node := P.FastTarget[N^.NotNode];
  if (N^.IfNode >= 0) and ((N^.ThenNode >= 0) or (N^.ElseNode >= 0)) then begin
    AddOp(B^.Apply, OpIf)^.Node := P.FastTarget[N^.IfNode];
    K := Length(B^.Apply) - 1;
    if N^.ThenNode >= 0 then
      B^.Apply[K].ThenNode := P.FastTarget[N^.ThenNode];
    if N^.ElseNode >= 0 then
      B^.Apply[K].ElseNode := P.FastTarget[N^.ElseNode];
  end;

  { unevaluatedProperties is left to the general evaluator (or a fused object plan). unevaluatedItems takes the
    items after a static prefix when every contribution to the evaluated items is unconditional. }
  if N^.UnevaluatedProperties >= 0 then
    B^.General := B^.General or TypeObject;
  if N^.UnevaluatedItems >= 0 then begin
    if not StaticItemCoverage(P, Id, All, From) then
      B^.General := B^.General or TypeArray
    else if not All then begin
      B^.HasUnevaluatedItems := True;
      B^.UnevaluatedFrom := From;
      B^.UnevaluatedChild := ChildOf(P, N^.UnevaluatedItems);
    end;
  end;

  Pl.Types := MeetTypes(OwnTypes, Union);
  if (Length(B^.Values) = 0) and (Length(B^.Number) = 0) and (Length(B^.Str) = 0) and not B^.HasObject
    and not B^.HasArray and (Length(B^.Apply) = 0) and (B^.General = 0) and not B^.HasUnevaluatedItems then begin
    Pl.Body := Default(TBody);
    Exit;
  end;
  Pl.HasBody := True;
end;

{ ShapeOf says how callers can enter a plan (see TShape). The shortcuts skip the scope push, so a program that
  keeps a dynamic scope takes them only for leaves. A node on an in-place cycle is entered through its guard. }
function ShapeOf(const Pl: TPlan; DynamicScope: Boolean): TShape;
var
  Values, HasObject, HasArray, HasApply: Boolean;
begin
  if Pl.Guard then
    Exit(ShapeGeneral);
  if not Pl.HasBody then
    Exit(ShapeTrivial);
  if (Pl.Body.General <> 0) or Pl.Body.HasUnevaluatedItems then
    Exit(ShapeGeneral);
  if Pl.Body.HasFused then begin
    { Without the scope push of the general entry, so not in a program that keeps a dynamic scope. }
    if DynamicScope then
      Exit(ShapeGeneral);
    Exit(ShapeFused);
  end;
  Values := (Length(Pl.Body.Values) <> 0) or (Length(Pl.Body.Number) <> 0) or (Length(Pl.Body.Str) <> 0);
  HasObject := Pl.Body.HasObject;
  HasArray := Pl.Body.HasArray;
  HasApply := Length(Pl.Body.Apply) <> 0;
  if not HasObject and not HasArray and not HasApply then begin
    if Values and (Length(Pl.Body.Number) = 0) and (Length(Pl.Body.Str) = 0) and (Length(Pl.Body.Values) = 1)
      and (Pl.Body.Values[0].Kind = OpEnumStrings) then
      Exit(ShapeStringEnum);
    if Values and (Length(Pl.Body.Number) = 0) and (Length(Pl.Body.Values) = 0) then
      Exit(ShapeStrings);
    Exit(ShapeLeaf);
  end;
  if Values or DynamicScope then
    Exit(ShapeGeneral);
  if HasObject and not HasArray and not HasApply then
    Exit(ShapeObject);
  if not HasObject and HasArray and not HasApply then
    Exit(ShapeArray);
  if not HasObject and not HasArray and HasApply then
    Exit(ShapeApply);
  Result := ShapeGeneral;
end;

{ ReachesDynamicReference marks every node from which a live dynamic reference is reachable through any child. }
function ReachesDynamicReference(const P: TProgram): TBooleanArray;
var
  Children: array of TNodeIDArray;
  I, J: Int32;
  Changed: Boolean;
begin
  Result := nil;
  SetLength(Result, Length(P.Nodes));
  if not P.UsesDynamicScope then
    Exit;
  Children := nil;
  SetLength(Children, Length(P.Nodes));
  for I := 0 to Length(P.Nodes) - 1 do begin
    Result[I] := P.Nodes[I].HasDynamicRef;
    Children[I] := NodeChildren(P.Nodes[I]);
  end;
  Changed := True;
  while Changed do begin
    Changed := False;
    for I := 0 to Length(Children) - 1 do begin
      if Result[I] then
        Continue;
      for J := 0 to Length(Children[I]) - 1 do
        if Result[Children[I][J]] then begin
          Result[I] := True;
          Changed := True;
          Break;
        end;
    end;
  end;
end;

procedure CompilePlans(var P: TProgram);
var
  Summary: TSummaryArray;
  Reaches, FusedNodes: TBooleanArray;
  Simple: array of TSimpleArray;
  I, J, K: Int32;
  B: PBody;
  ItemTypes: Byte;
  TypeOnly: Boolean;

  procedure Fix(var C: TChild);
  begin
    if C.Id < 0 then
      Exit;
    SetShape(C, Summary[C.Id].Types, Summary[C.Id].Shape);
    if (Summary[C.Id].Shape = ShapeObject) and FusedNodes[C.Id] then
      SetShape(C, Summary[C.Id].Types, ShapeGeneral);
  end;

  { Every child now takes the final shape of its node, which a fused plan may have changed since the child was
    made: a child that kept the shape its node had before it was fused would enter the node's applicators one by
    one where the fused plan decides them in one pass. }
  procedure Final(var C: TChild);
  begin
    if C.Id >= 0 then
      SetShape(C, P.Plans[C.Id].Types, P.Plans[C.Id].Shape);
  end;

  procedure FinalObject(var O: TObjectPlan);
  var
    M: Int32;
  begin
    for M := 0 to Length(O.Children) - 1 do
      Final(O.Children[M]);
    for M := 0 to Length(O.Patterns) - 1 do
      Final(O.Patterns[M].Child);
    if O.HasAdditional then
      Final(O.Additional);
  end;

begin
  P.Plans := nil;
  SetLength(P.Plans, Length(P.Nodes));
  for I := 0 to Length(P.Nodes) - 1 do
    PlanNode(P, I, P.Plans[I]);
  { Hoist the children's type checks now that every plan is known. }
  Summary := nil;
  SetLength(Summary, Length(P.Plans));
  for I := 0 to Length(P.Plans) - 1 do begin
    Summary[I].Types := P.Plans[I].Types;
    Summary[I].Shape := ShapeOf(P.Plans[I], P.UsesDynamicScope);
  end;
  { Fused object plans, for nodes whose object semantics span in-place applicators. A fused plan applies its
    contributors' keywords without entering them as nodes, so the dynamic scope below it would differ from the
    general path's where a contributor is in another resource: nodes that can reach a live dynamic reference fuse
    only contributors in their own resource (which the general path would not push again). }
  Reaches := ReachesDynamicReference(P);
  FusedNodes := nil;
  SetLength(FusedNodes, Length(P.Plans));
  for I := 0 to Length(P.Plans) - 1 do begin
    B := @P.Plans[I].Body;
    if TryFuse(P, I, Reaches[I], Summary, B^.Fused) then begin
      if not P.Plans[I].HasBody then begin
        P.Plans[I].HasBody := True;
        B^.Node := I;
      end;
      B^.HasFused := True;
      FusedNodes[I] := True;
    end else
      B^.Fused := Default(TFusedObject);
  end;
  for I := 0 to Length(P.Plans) - 1 do begin
    if not P.Plans[I].HasBody then
      Continue;
    B := @P.Plans[I].Body;
    if B^.HasObject then begin
      for J := 0 to Length(B^.ObjectPlan.Children) - 1 do
        Fix(B^.ObjectPlan.Children[J]);
      for J := 0 to Length(B^.ObjectPlan.Patterns) - 1 do
        Fix(B^.ObjectPlan.Patterns[J].Child);
      if B^.ObjectPlan.HasAdditional then
        Fix(B^.ObjectPlan.Additional);
    end;
    if B^.HasArray then begin
      for J := 0 to Length(B^.ArrayPlan.Prefix) - 1 do
        Fix(B^.ArrayPlan.Prefix[J]);
      ItemTypes := AnyType;
      TypeOnly := True;
      if B^.ArrayPlan.HasItems then begin
        Fix(B^.ArrayPlan.Items);
        ItemTypes := B^.ArrayPlan.Items.Types;
        TypeOnly := B^.ArrayPlan.Items.Shape = ShapeTrivial;
      end;
      B^.ArrayPlan.IsSimple := TypeOnly and (Length(B^.ArrayPlan.Prefix) = 0) and (B^.ArrayPlan.Contains < 0)
        and not B^.ArrayPlan.Unique;
      B^.ArrayPlan.Simple := ItemTypes;
    end;
    if B^.HasUnevaluatedItems then
      Fix(B^.UnevaluatedChild);
    for J := 0 to Length(B^.Apply) - 1 do
      case B^.Apply[J].Kind of
        OpAllOf:
          for K := 0 to Length(B^.Apply[J].Children) - 1 do
            Fix(B^.Apply[J].Children[K]);
        OpAnyOf, OpOneOf: begin
          for K := 0 to Length(B^.Apply[J].Branches.Children) - 1 do
            Fix(B^.Apply[J].Branches.Children[K]);
          Dispatch(B^.Apply[J].Branches);
        end;
      else
      end;
  end;
  { Arrays whose items are simple arrays check them inline, without entering each one. }
  Simple := nil;
  SetLength(Simple, Length(P.Plans));
  for I := 0 to Length(P.Plans) - 1 do begin
    Simple[I] := Default(TSimpleArray);
    B := @P.Plans[I].Body;
    if not P.Plans[I].HasBody or not B^.HasArray or not B^.ArrayPlan.IsSimple
      or (ShapeOf(P.Plans[I], P.UsesDynamicScope) <> ShapeArray) then
      Continue;
    Simple[I].IsSet := True;
    Simple[I].Min := B^.ArrayPlan.Min;
    Simple[I].Max := B^.ArrayPlan.Max;
    Simple[I].Types := B^.ArrayPlan.Simple;
  end;
  for I := 0 to Length(P.Plans) - 1 do begin
    B := @P.Plans[I].Body;
    if P.Plans[I].HasBody and B^.HasArray then
      if B^.ArrayPlan.HasItems and (B^.ArrayPlan.Items.Shape = ShapeArray)
        and Simple[B^.ArrayPlan.Items.Id].IsSet then begin
        B^.ArrayPlan.HasNested := True;
        B^.ArrayPlan.Nested := Simple[B^.ArrayPlan.Items.Id];
      end;
  end;
  for I := 0 to Length(P.Plans) - 1 do begin
    P.Plans[I].Shape := ShapeOf(P.Plans[I], P.UsesDynamicScope);
    P.Plans[I].Self.Id := I;
    SetShape(P.Plans[I].Self, P.Plans[I].Types, P.Plans[I].Shape);
  end;
  for I := 0 to Length(P.Plans) - 1 do begin
    if not P.Plans[I].HasBody then
      Continue;
    B := @P.Plans[I].Body;
    if B^.HasObject then
      FinalObject(B^.ObjectPlan);
    if B^.HasArray then begin
      for J := 0 to Length(B^.ArrayPlan.Prefix) - 1 do
        Final(B^.ArrayPlan.Prefix[J]);
      if B^.ArrayPlan.HasItems then
        Final(B^.ArrayPlan.Items);
    end;
    if B^.HasUnevaluatedItems then
      Final(B^.UnevaluatedChild);
    for J := 0 to Length(B^.Apply) - 1 do
      case B^.Apply[J].Kind of
        OpAllOf:
          for K := 0 to Length(B^.Apply[J].Children) - 1 do
            Final(B^.Apply[J].Children[K]);
        OpAnyOf, OpOneOf:
          for K := 0 to Length(B^.Apply[J].Branches.Children) - 1 do
            Final(B^.Apply[J].Branches.Children[K]);
      else
      end;
    if B^.HasFused then begin
      if B^.Fused.HasFlat then
        FinalObject(B^.Fused.Flat);
      for J := 0 to Length(B^.Fused.Entries) - 1 do
        for K := 0 to Length(B^.Fused.Entries[J].Apps) - 1 do
          if B^.Fused.Entries[J].Apps[K].HasChild then
            Final(B^.Fused.Entries[J].Apps[K].Child);
      for J := 0 to Length(B^.Fused.Contributors) - 1 do begin
        for K := 0 to Length(B^.Fused.Contributors[J].Patterns) - 1 do
          if B^.Fused.Contributors[J].Patterns[K].Child.IsSet then
            Final(B^.Fused.Contributors[J].Patterns[K].Child.Child);
        if B^.Fused.Contributors[J].HasAdditional and B^.Fused.Contributors[J].Additional.IsSet then
          Final(B^.Fused.Contributors[J].Additional.Child);
      end;
      if B^.Fused.HasUnevaluated then
        Final(B^.Fused.Unevaluated);
    end;
  end;
end;

end.
