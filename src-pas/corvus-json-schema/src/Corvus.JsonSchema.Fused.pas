unit Corvus.JsonSchema.Fused;

{$I corvus.inc}

{ The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
  in-place applicators ($ref, allOf, if/then/else, dependencies, required-only oneOf/anyOf) and possibly finished
  by unevaluatedProperties.

  The general path evaluates each applicator branch as a separate pass over the instance, each with its own
  property lookups, and then walks the instance once more for unevaluatedProperties. The fused plan resolves every
  property name known to any branch at compile time to the list of child schemas that apply to it (a branch's own
  property schema, its matching pattern-property schemas, or its additionalProperties when neither matched), so
  evaluation is one lookup per instance property, and it tracks which properties some branch covered so that the
  unevaluated check needs no second analysis.

  Branches under then/else (or a dependency's schema) apply only when their condition holds. The plan supports
  conditions the pass itself decides (required names, property values tested against constants or a pattern, and
  names no property may match), and defers those branches' applications to a second step over the properties they
  touch.

  Fail-fast evaluation only. The fused pass does not enter its contributors as nodes (and so would not push their
  resources on the dynamic scope): below a live dynamic reference, only contributors in the node's own resource
  fuse.

  A port of the construction half of fused.go of the Go module. The plan's types are in Corvus.JsonSchema.Plan (a
  plan's body holds one), and the pass itself is in Corvus.JsonSchema.Eval, with the rest of the evaluation. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Numbers,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Compiler,
  Corvus.JsonSchema.Plan;

{ TryFuse builds the fused plan for a node. False when it cannot be fused or fusing does not pay. Summary is the
  types and shape of every node's plan as first compiled, from which a child is made (the childOf function of the
  Go source). }
function TryFuse(const P: TProgram; Id: TNodeID; SameResource: Boolean; const Summary: TSummaryArray;
  var F: TFusedObject): Boolean;

implementation

uses
  Corvus.JsonSchema.Values;

type
  { TCollectedContributor is a contributor as collected: the branch, its condition, its alternative group and
    branch. }
  TCollectedContributor = record
    Node: TNodeID;
    Condition: TGate;
    Alt: TAltBranch;
  end;

  TPendingCondition = record
    { The if schema, or the dependency's property name. }
    IsTest: Boolean;
    Test: TNodeID;
    Dependency: UTF8String;
    Gate: TGate;
  end;

  TCollectedExtra = record
    Condition: UInt16;
    Names: TUTF8StringArray;
  end;

  TCollectedAlternative = record
    Condition: TGate;
    ExactlyOne: Boolean;
    Branches: array of TUTF8StringArray;
  end;

  TCollectedForbidden = record
    Gate: TGate;
    Names: TUTF8StringArray;
  end;

  { TFuseCollector is what a collection gathers. }
  TFuseCollector = record
    P: PProgram;
    Contributors: array of TCollectedContributor;
    Conditions: array of TPendingCondition;
    { Dependencies' required names. }
    Extras: array of TCollectedExtra;
    Alternatives: array of TCollectedAlternative;
    AltGroups: TFusedAltGroupArray;
    Forbidden: array of TCollectedForbidden;
    { Alternative groups with object keywords only where coverage is not tracked (a failed branch must not
      cover). }
    AllowAltGroups: Boolean;
    { Every contributor must belong to this resource (below a live dynamic reference, where the general path would
      push a contributor's own resource on the dynamic scope and the fused pass does not enter contributors). }
    SameResource: Boolean;
    Resource: Int32;
  end;

  { TCollectedApp is one resolution of a property by a contributor, before identical ones are merged. }
  TCollectedApp = record
    Contributor: UInt16;
    Child: TOptChild;
    Target: TNodeID;
  end;
  TCollectedAppArray = array of TCollectedApp;

function NoGate: TGate;
begin
  Result.IsSet := False;
  Result.Condition := 0;
  Result.Polarity := False;
end;

function GateOf(Condition: UInt16; Polarity: Boolean): TGate;
begin
  Result.IsSet := True;
  Result.Condition := Condition;
  Result.Polarity := Polarity;
end;

function IsRequiredListOnly(const N: TSchemaNode): Boolean;
begin
  Result := not (N.ConstValue.IsSet or N.HasEnum or NodeHasNumberKeywords(N) or NodeHasStringKeywords(N)
    or NodeHasArrayKeywords(N) or NodeHasInPlaceApplicators(N) or N.HasDynamicRef or (N.UnevaluatedProperties >= 0)
    or (N.UnevaluatedItems >= 0) or (N.HasType and (N.TypeMask and TypeObject = 0)) or N.HasPatternProperties
    or (N.AdditionalProperties >= 0) or (N.PropertyNames >= 0) or N.HasDependencies or N.MinProperties.IsSet
    or N.MaxProperties.IsSet or (Length(N.Properties) <> 0));
end;

{ ForbiddenNames are the names of a not whose schema is a non-empty required list: an object fails when all are
  present. }
function ForbiddenNames(const P: TProgram; NotNode: TNodeID; out Names: TUTF8StringArray): Boolean;
var
  N: PSchemaNode;
begin
  Names := nil;
  N := @P.Nodes[P.FastTarget[NotNode]];
  if N^.AlwaysTrue or N^.AlwaysFalse or not IsRequiredListOnly(N^) or (Length(N^.Required) = 0) then
    Exit(False);
  Names := N^.Required;
  Result := True;
end;

{ IsObjectBranch reports a branch whose only effect on an object instance is through object keywords and fusable
  in-place applicators. }
function IsObjectBranch(const P: TProgram; const N: TSchemaNode; AllowUnevaluated: Boolean): Boolean;
var
  Names: TUTF8StringArray;
begin
  if N.NotNode >= 0 then
    if not ForbiddenNames(P, N.NotNode, Names) then
      Exit(False);
  Result := not (N.ConstValue.IsSet or N.HasEnum or NodeHasNumberKeywords(N) or NodeHasStringKeywords(N)
    or (N.HasType and (N.TypeMask and TypeObject = 0)) or (N.PropertyNames >= 0) or N.HasDynamicRef
    or (not AllowUnevaluated and (N.UnevaluatedProperties >= 0)));
end;

{ IsLeaf reports only local keywords (type, const, enum, number and string keywords). }
function IsLeaf(const N: TSchemaNode): Boolean;
begin
  Result := not N.AlwaysTrue and not N.AlwaysFalse and not NodeHasObjectKeywords(N) and not NodeHasArrayKeywords(N)
    and not NodeHasInPlaceApplicators(N) and not N.HasDynamicRef;
end;

function IsTypeOnly(const N: TSchemaNode): Boolean;
begin
  Result := IsLeaf(N) and not N.ConstValue.IsSet and not N.HasEnum and not NodeHasNumberKeywords(N)
    and not NodeHasStringKeywords(N);
end;

{ ValueTestOf is the value test for a property schema inside an if: const/enum of scalars (the values the schema's
  type admits), or a pattern with at most type: string. Not ok for anything else. }
function ValueTestOf(const P: TProgram; const N: TSchemaNode; out Test: TValueTest): Boolean;
var
  Values: TValueRefArray;
  I, K: Int32;
  V: TValueRef;
begin
  Test := Default(TValueTest);
  Test.Pattern := -1;
  if N.AlwaysTrue or N.AlwaysFalse or NodeHasNumberKeywords(N) or NodeHasObjectKeywords(N)
    or NodeHasArrayKeywords(N) or NodeHasInPlaceApplicators(N) or N.HasDynamicRef then
    Exit(False);
  if N.Pattern >= 0 then begin
    Test.IsPattern := True;
    Test.Pattern := N.Pattern;
    Test.RequiresString := N.HasType;
    Result := not N.ConstValue.IsSet and not N.HasEnum and not N.MinLength.IsSet and not N.MaxLength.IsSet
      and not (N.AssertFormat and N.HasFormat) and not N.AssertContent
      and (not N.HasType or (N.TypeMask = TypeString));
    Exit;
  end;
  if NodeHasStringKeywords(N) then
    Exit(False);
  Values := nil;
  if N.ConstValue.IsSet then begin
    SetLength(Values, 1);
    Values[0] := N.ConstValue;
  end else if Length(N.EnumValues) <> 0 then
    Values := N.EnumValues
  else
    Exit(False);
  for I := 0 to Length(Values) - 1 do begin
    V := Values[I];
    case DocKind(P.Documents[V.Doc], V.N) of
      KindNumber:
        if not IsIntegerNumber(DocFlags(P.Documents[V.Doc], V.N), DocData(P.Documents[V.Doc], V.N)) then begin
          Test := Default(TValueTest);
          Test.Pattern := -1;
          Exit(False);
        end;
      KindString, KindBool, KindNull: ;
    else
      Test := Default(TValueTest);
      Test.Pattern := -1;
      Exit(False);
    end;
    { A value the schema's type rejects never passes, so it is not allowed. }
    if not N.HasType or TypeOK(N.TypeMask, P.Documents[V.Doc], V.N) then begin
      K := Length(Test.Allowed);
      SetLength(Test.Allowed, K + 1);
      Test.Allowed[K] := V;
    end;
  end;
  Result := True;
end;

function CollectorNode(const C: TFuseCollector; Id: TNodeID): PSchemaNode; inline;
begin
  Result := @C.P^.Nodes[Id];
end;

function CollectorTarget(const C: TFuseCollector; Id: TNodeID): TNodeID; inline;
begin
  Result := C.P^.FastTarget[Id];
end;

function OtherResource(const C: TFuseCollector; const N: TSchemaNode): Boolean;
begin
  Result := C.SameResource and (N.ResourceID <> C.Resource);
end;

procedure AddContributor(var C: TFuseCollector; Node: TNodeID; const Condition: TGate; const Alt: TAltBranch);
var
  K: Int32;
begin
  K := Length(C.Contributors);
  SetLength(C.Contributors, K + 1);
  C.Contributors[K].Node := Node;
  C.Contributors[K].Condition := Condition;
  C.Contributors[K].Alt := Alt;
end;

function AddCondition(var C: TFuseCollector; IsTest: Boolean; Test: TNodeID; const Dependency: UTF8String;
  const Gate: TGate): UInt16;
var
  K: Int32;
begin
  K := Length(C.Conditions);
  SetLength(C.Conditions, K + 1);
  C.Conditions[K].IsTest := IsTest;
  C.Conditions[K].Test := Test;
  C.Conditions[K].Dependency := Dependency;
  C.Conditions[K].Gate := Gate;
  Result := UInt16(K);
end;

{ CollectorTypeUnion reports that every branch only tests the type (the general path decides the keyword by one
  mask test). }
function CollectorTypeUnion(const C: TFuseCollector; const List: TNodeIDArray): Boolean;
var
  I: Int32;
  B: PSchemaNode;
begin
  for I := 0 to Length(List) - 1 do begin
    B := CollectorNode(C, CollectorTarget(C, List[I]));
    if not (B^.HasType and IsTypeOnly(B^)) then
      Exit(False);
  end;
  Result := True;
end;

{ IsSimpleArray reports an array of leaf items with at most size bounds: cheap to apply more than once. }
function IsSimpleArray(const C: TFuseCollector; const N: TSchemaNode): Boolean;
var
  Item: PSchemaNode;
begin
  if not NodeHasArrayKeywords(N) or NodeHasObjectKeywords(N) or NodeHasInPlaceApplicators(N) or N.HasDynamicRef
    or N.ConstValue.IsSet or N.HasEnum or N.HasPrefixItems or (N.Contains >= 0) or N.UniqueItems
    or (N.UnevaluatedItems >= 0) or (N.Items < 0) then
    Exit(False);
  Item := CollectorNode(C, CollectorTarget(C, N.Items));
  Result := Item^.AlwaysTrue or IsLeaf(Item^);
end;

{ ExpensiveChildrenDisjoint reports whether no property name gets an expensive child (anything but a leaf or a
  boolean schema) from more than one branch. Pattern and additional properties count as every name. }
function ExpensiveChildrenDisjoint(const C: TFuseCollector; const Branches: TNodeIDArray): Boolean;
var
  Expensive: TUTF8StringArray;
  Wildcards, I, J, K: Int32;
  N: PSchemaNode;
  Wildcard: Boolean;

  function Cheap(Id: TNodeID): Boolean;
  var
    M: PSchemaNode;
  begin
    M := CollectorNode(C, CollectorTarget(C, Id));
    Result := M^.AlwaysTrue or M^.AlwaysFalse or IsLeaf(M^) or IsSimpleArray(C, M^);
  end;

begin
  Expensive := nil;
  Wildcards := 0;
  for I := 0 to Length(Branches) - 1 do begin
    N := CollectorNode(C, Branches[I]);
    for J := 0 to Length(N^.Properties) - 1 do
      if not Cheap(N^.Properties[J].Node) then begin
        if ContainsString(Expensive, N^.Properties[J].Name) then
          Exit(False);
        K := Length(Expensive);
        SetLength(Expensive, K + 1);
        Expensive[K] := N^.Properties[J].Name;
      end;
    Wildcard := (N^.AdditionalProperties >= 0) and not Cheap(N^.AdditionalProperties);
    for J := 0 to Length(N^.PatternProperties) - 1 do
      Wildcard := Wildcard or not Cheap(N^.PatternProperties[J].Node);
    if Wildcard then begin
      Inc(Wildcards);
      if Wildcards > 1 then
        Exit(False);
    end;
  end;
  Result := (Wildcards = 0) or (Length(Expensive) = 0);
end;

{ SupportedCondition reports a condition the pass decides alone: required names, and properties whose schemas are
  value tests, optionally with type: object (which the object plan has established). At least one of the two. }
function SupportedCondition(const C: TFuseCollector; const Test: TSchemaNode): Boolean;
var
  Any: Boolean;
  I: Int32;
  VT: TValueTest;
begin
  Any := Length(Test.Required) <> 0;
  if Test.ConstValue.IsSet or Test.HasEnum or NodeHasNumberKeywords(Test) or NodeHasStringKeywords(Test)
    or NodeHasArrayKeywords(Test) or NodeHasInPlaceApplicators(Test) or Test.HasDynamicRef
    or (Test.HasType and (Test.TypeMask and TypeObject = 0)) or (Test.AdditionalProperties >= 0)
    or (Test.PropertyNames >= 0) or (Test.UnevaluatedProperties >= 0) or Test.HasDependencies
    or Test.MinProperties.IsSet or Test.MaxProperties.IsSet then
    Exit(False);
  for I := 0 to Length(Test.Properties) - 1 do begin
    if not ValueTestOf(C.P^, CollectorNode(C, CollectorTarget(C, Test.Properties[I].Node))^, VT) then
      Exit(False);
    Any := True;
  end;
  (* patternProperties: {P: false}: no property name may match P. *)
  for I := 0 to Length(Test.PatternProperties) - 1 do begin
    if not CollectorNode(C, CollectorTarget(C, Test.PatternProperties[I].Node))^.AlwaysFalse then
      Exit(False);
    Any := True;
  end;
  Result := Any;
end;

{ CollectAlternative fuses a oneOf/anyOf when every branch is a plain required list (decided from the seen names
  after the pass), or as an alternative group of object branches where coverage is not tracked. }
function CollectAlternative(var C: TFuseCollector; const List: TNodeIDArray; const Condition: TGate;
  ExactlyOne, Discriminated: Boolean): Boolean;
var
  Branches: TNodeIDArray;
  RequiredOnly: Boolean;
  Required: array of TUTF8StringArray;
  I, K: Int32;
  N: PSchemaNode;
  Group: UInt16;
  Alt: TAltBranch;
begin
  Branches := nil;
  SetLength(Branches, Length(List));
  RequiredOnly := True;
  Required := nil;
  SetLength(Required, Length(List));
  for I := 0 to Length(List) - 1 do begin
    Branches[I] := CollectorTarget(C, List[I]);
    N := CollectorNode(C, Branches[I]);
    if (Length(N^.Required) <> 0) and IsRequiredListOnly(N^) then
      Required[I] := N^.Required
    else
      RequiredOnly := False;
  end;
  if RequiredOnly then begin
    K := Length(C.Alternatives);
    SetLength(C.Alternatives, K + 1);
    C.Alternatives[K].Condition := Condition;
    C.Alternatives[K].ExactlyOne := ExactlyOne;
    SetLength(C.Alternatives[K].Branches, Length(Required));
    for I := 0 to Length(Required) - 1 do
      C.Alternatives[K].Branches[I] := Required[I];
    Exit(True);
  end;
  { Branches with object keywords: each a contributor whose failure marks the branch rather than the object. The
    pass applies every branch that knows a name to that property, where the general path stops at the first
    branch that passes, so a name with an expensive child in more than one branch is refused. A keyword the
    general path decides by discriminator or type stays with it. }
  if not C.AllowAltGroups or Discriminated or Condition.IsSet or (Length(C.AltGroups) >= MaxFusedAltGroups)
    or (Length(Branches) > 64) or not ExpensiveChildrenDisjoint(C, Branches) then
    Exit(False);
  Group := UInt16(Length(C.AltGroups));
  for I := 0 to Length(Branches) - 1 do begin
    N := CollectorNode(C, Branches[I]);
    if N^.AlwaysTrue or N^.AlwaysFalse or N^.InPlaceCycle or NodeHasInPlaceApplicators(N^) or N^.HasDependencies
      or (N^.NotNode >= 0) or not IsObjectBranch(C.P^, N^, False) or OtherResource(C, N^)
      or (Length(C.Contributors) >= MaxFusedContributors) then
      Exit(False);
    Alt.IsSet := True;
    Alt.Group := Group;
    Alt.Branch := UInt16(I);
    AddContributor(C, Branches[I], NoGate, Alt);
  end;
  K := Length(C.AltGroups);
  SetLength(C.AltGroups, K + 1);
  C.AltGroups[K].ExactlyOne := ExactlyOne;
  C.AltGroups[K].Count := UInt32(Length(Branches));
  Result := True;
end;

{ Collect walks the in-place applicators of a branch, adding every object branch with its condition. It fails when
  a branch cannot be fused. }
function Collect(var C: TFuseCollector; Id: TNodeID; const Condition: TGate): Boolean;
var
  N, Test: PSchemaNode;
  Names: TUTF8StringArray;
  I, K: Int32;
  Discriminated: Boolean;
  At: UInt16;
  TestID, ThenNode, ElseNode: TNodeID;
  NoAlt: TAltBranch;
begin
  N := CollectorNode(C, Id);
  if N^.AlwaysTrue then
    Exit(True);
  if N^.AlwaysFalse or N^.InPlaceCycle or not IsObjectBranch(C.P^, N^, Length(C.Contributors) = 0) then
    Exit(False);
  if (Length(C.Contributors) >= MaxFusedContributors) or OtherResource(C, N^) then
    Exit(False);
  NoAlt := Default(TAltBranch);
  AddContributor(C, Id, Condition, NoAlt);
  if N^.NotNode >= 0 then begin
    ForbiddenNames(C.P^, N^.NotNode, Names);
    K := Length(C.Forbidden);
    SetLength(C.Forbidden, K + 1);
    C.Forbidden[K].Gate := Condition;
    C.Forbidden[K].Names := Names;
  end;
  if (N^.Ref >= 0) and not Collect(C, CollectorTarget(C, N^.Ref), Condition) then
    Exit(False);
  if (N^.StaticDynamicRef >= 0) and not Collect(C, CollectorTarget(C, N^.StaticDynamicRef), Condition) then
    Exit(False);
  for I := 0 to Length(N^.AllOf) - 1 do
    if not Collect(C, CollectorTarget(C, N^.AllOf[I]), Condition) then
      Exit(False);
  if N^.HasOneOf then begin
    Discriminated := N^.HasOneOfDiscriminator or CollectorTypeUnion(C, N^.OneOf);
    if not CollectAlternative(C, N^.OneOf, Condition, True, Discriminated) then
      Exit(False);
  end;
  if N^.HasAnyOf then begin
    Discriminated := N^.HasAnyOfDiscriminator or CollectorTypeUnion(C, N^.AnyOf);
    if not CollectAlternative(C, N^.AnyOf, Condition, False, Discriminated) then
      Exit(False);
  end;
  for I := 0 to Length(N^.Dependencies) - 1 do begin
    if Length(C.Conditions) >= MaxFusedConditions then
      Exit(False);
    At := AddCondition(C, False, NoNode, N^.Dependencies[I].Name, Condition);
    if Length(N^.Dependencies[I].Required) <> 0 then begin
      K := Length(C.Extras);
      SetLength(C.Extras, K + 1);
      C.Extras[K].Condition := At;
      C.Extras[K].Names := N^.Dependencies[I].Required;
    end;
    if (N^.Dependencies[I].Schema >= 0)
      and not Collect(C, CollectorTarget(C, N^.Dependencies[I].Schema), GateOf(At, True)) then
      Exit(False);
  end;
  if N^.IfNode >= 0 then begin
    TestID := CollectorTarget(C, N^.IfNode);
    Test := CollectorNode(C, TestID);
    ThenNode := NoNode;
    ElseNode := NoNode;
    if N^.ThenNode >= 0 then
      ThenNode := CollectorTarget(C, N^.ThenNode);
    if N^.ElseNode >= 0 then
      ElseNode := CollectorTarget(C, N^.ElseNode);
    if Test^.AlwaysTrue then
      Exit((ThenNode < 0) or Collect(C, ThenNode, Condition));
    if Test^.AlwaysFalse then
      Exit((ElseNode < 0) or Collect(C, ElseNode, Condition));
    if not SupportedCondition(C, Test^) or (Length(C.Conditions) >= MaxFusedConditions) then
      Exit(False);
    At := AddCondition(C, True, TestID, '', Condition);
    { When the condition holds, the if schema's own properties count as evaluated, so it contributes under the
      same condition as then. }
    if Test^.HasProperties and not Collect(C, TestID, GateOf(At, True)) then
      Exit(False);
    if (ThenNode >= 0) and not Collect(C, ThenNode, GateOf(At, True)) then
      Exit(False);
    if (ElseNode >= 0) and not Collect(C, ElseNode, GateOf(At, False)) then
      Exit(False);
  end;
  Result := True;
end;

{ MergeValueTests is the merged constants of an entry's value tests, when some (of at most 64) are constant sets
  and there is more than one constant to look for. False when there is nothing to merge. }
function MergeValueTests(const P: TProgram; const Tests: TValueTestArray; var M: TMergedTests): Boolean;
var
  Constants, T, I, J, K: Int32;
  Strings: TUTF8StringArray;
  V: TValueRef;
  S: UTF8String;
  Found: Boolean;
begin
  M := Default(TMergedTests);
  Constants := 0;
  for I := 0 to Length(Tests) - 1 do
    if not Tests[I].IsPattern then
      Inc(Constants, Length(Tests[I].Allowed));
  if (Length(Tests) > 64) or (Constants < 2) then
    Exit(False);
  Strings := nil;
  for T := 0 to Length(Tests) - 1 do begin
    if Tests[T].IsPattern then
      Continue;
    M.Keyed := M.Keyed or (One64 shl T);
    for J := 0 to Length(Tests[T].Allowed) - 1 do begin
      V := Tests[T].Allowed[J];
      Found := False;
      if DocKind(P.Documents[V.Doc], V.N) = KindString then begin
        S := DocStrCopy(P.Documents[V.Doc], V.N);
        for I := 0 to Length(Strings) - 1 do
          if Strings[I] = S then begin
            M.StringMasks[I] := M.StringMasks[I] or (One64 shl T);
            Found := True;
            Break;
          end;
        if not Found then begin
          K := Length(Strings);
          SetLength(Strings, K + 1);
          Strings[K] := S;
          SetLength(M.StringMasks, K + 1);
          M.StringMasks[K] := One64 shl T;
        end;
        Continue;
      end;
      for I := 0 to Length(M.Others) - 1 do
        if ValuesEqual(P.Documents[M.Others[I].Value.Doc], M.Others[I].Value.N, P.Documents[V.Doc], V.N) then begin
          M.Others[I].Mask := M.Others[I].Mask or (One64 shl T);
          Found := True;
          Break;
        end;
      if not Found then begin
        K := Length(M.Others);
        SetLength(M.Others, K + 1);
        M.Others[K].Value := V;
        M.Others[K].Mask := One64 shl T;
      end;
    end;
  end;
  M.Strings := NewNames(Strings);
  Result := True;
end;

function SameAlt(const A, B: TAltBranch): Boolean;
begin
  Result := (A.IsSet = B.IsSet) and (A.Group = B.Group) and (A.Branch = B.Branch);
end;

{ CoalesceApps merges identical resolutions of a property from several branches (the same child, or both true)
  into one application listing every branch, applied once when any of them is active. Branches of an alternative
  group merge only within the same branch. The primary contributor is an unconditional one when there is one, so
  the pass applies it at once. }
function CoalesceApps(const Apps: TCollectedAppArray; const Contributors: TFusedContributorArray): TFusedAppArray;
var
  Used: array of Boolean;
  I, J, K, Primary: Int32;
  Others: TUInt16Array;

  function Same(const A, B: TCollectedApp): Boolean;
  begin
    if not A.Child.IsSet or not B.Child.IsSet then
      Exit(A.Child.IsSet = B.Child.IsSet);
    Result := (A.Target = B.Target) or ((A.Child.Child.Shape = ShapeTrivial) and (B.Child.Child.Shape = ShapeTrivial)
      and (A.Child.Child.Types = B.Child.Child.Types));
  end;

  procedure AddOther(Contributor: UInt16);
  var
    M: Int32;
  begin
    M := Length(Others);
    SetLength(Others, M + 1);
    Others[M] := Contributor;
  end;

begin
  Result := nil;
  Used := nil;
  SetLength(Used, Length(Apps));
  for I := 0 to Length(Apps) - 1 do begin
    if Used[I] then
      Continue;
    Primary := I;
    Others := nil;
    for J := I + 1 to Length(Apps) - 1 do begin
      if Used[J] or not Same(Apps[Primary], Apps[J])
        or not SameAlt(Contributors[Apps[Primary].Contributor].Alt, Contributors[Apps[J].Contributor].Alt) then
        Continue;
      Used[J] := True;
      if Contributors[Apps[Primary].Contributor].Condition.IsSet
        and not Contributors[Apps[J].Contributor].Condition.IsSet then begin
        AddOther(Apps[Primary].Contributor);
        Primary := J;
      end else
        AddOther(Apps[J].Contributor);
    end;
    K := Length(Result);
    SetLength(Result, K + 1);
    Result[K] := Default(TFusedApp);
    Result[K].Contributor := Apps[Primary].Contributor;
    Result[K].HasChild := Apps[Primary].Child.IsSet;
    Result[K].Child := Apps[Primary].Child.Child;
    Result[K].Others := Others;
  end;
end;

function TryFuse(const P: TProgram; Id: TNodeID; SameResource: Boolean; const Summary: TSummaryArray;
  var F: TFusedObject): Boolean;
type
  TEntryTest = record
    Entry: UInt16;
    Test: TValueTest;
  end;
  TExtraBits = record
    Condition: UInt16;
    Names: TUInt16Array;
  end;
var
  N, B, Test: PSchemaNode;
  Ctx: TFuseCollector;
  HasIf, Flat, Matched: Boolean;
  Effective, I, J, K, C, E: Int32;
  Known: TUTF8StringArray;
  TestsByEntry: array of TEntryTest;
  Required: TUInt16Array;
  Extras: array of TExtraBits;
  Tests: array of TValueTestArray;
  Apps: TCollectedAppArray;
  VT: TValueTest;
  FC: PFusedContributor;
  App: PFusedApp;
  A: TNodeID;

  function Bit(const Name: UTF8String): UInt16;
  var
    M: Int32;
  begin
    for M := 0 to Length(Known) - 1 do
      if Known[M] = Name then
        Exit(UInt16(M));
    M := Length(Known);
    SetLength(Known, M + 1);
    Known[M] := Name;
    Result := UInt16(M);
  end;

  function BitsOf(const List: TUTF8StringArray): TUInt16Array;
  var
    M: Int32;
  begin
    Result := nil;
    SetLength(Result, Length(List));
    for M := 0 to Length(List) - 1 do
      Result[M] := Bit(List[M]);
  end;

  function AppChild(Child: TNodeID): TOptChild;
  var
    T: TNodeID;
  begin
    Result := Default(TOptChild);
    T := P.FastTarget[Child];
    if P.Nodes[T].AlwaysTrue then
      Exit;
    Result.IsSet := True;
    Result.Child := ResolvedChild(Summary, T);
  end;

  procedure AddApp(Contributor: Int32; Child: TNodeID);
  var
    M: Int32;
  begin
    M := Length(Apps);
    SetLength(Apps, M + 1);
    Apps[M].Contributor := UInt16(Contributor);
    Apps[M].Child := AppChild(Child);
    Apps[M].Target := P.FastTarget[Child];
  end;

  procedure AddTest(Entry: Int32; const T: TValueTest);
  var
    M: Int32;
  begin
    M := Length(Tests[Entry]);
    SetLength(Tests[Entry], M + 1);
    Tests[Entry][M] := T;
  end;

begin
  F := Default(TFusedObject);
  N := @P.Nodes[Id];
  if not IsObjectBranch(P, N^, True) or N^.InPlaceCycle or N^.AlwaysTrue or N^.AlwaysFalse then
    Exit(False);
  Ctx := Default(TFuseCollector);
  Ctx.P := @P;
  Ctx.AllowAltGroups := N^.UnevaluatedProperties < 0;
  Ctx.SameResource := SameResource;
  Ctx.Resource := N^.ResourceID;
  if not Collect(Ctx, Id, NoGate) then
    Exit(False);
  if (Length(Ctx.Contributors) + Length(Ctx.Extras) > MaxFusedContributors)
    or (Length(Ctx.Conditions) > MaxFusedConditions) then
    Exit(False);

  { Fusing pays when a pass over every property is unavoidable (unevaluatedProperties) or when it replaces
    several passes: two or more branches with object keywords, an if the seen names decide, or alternatives. A
    node whose object keywords are all its own keeps its object plan. }
  HasIf := False;
  for I := 0 to Length(Ctx.Conditions) - 1 do
    HasIf := HasIf or Ctx.Conditions[I].IsTest;
  if (N^.UnevaluatedProperties < 0) and not HasIf and (Length(Ctx.Alternatives) = 0)
    and (Length(Ctx.AltGroups) = 0) then begin
    Effective := 0;
    for I := 0 to Length(Ctx.Contributors) - 1 do begin
      B := @P.Nodes[Ctx.Contributors[I].Node];
      if B^.HasProperties or B^.HasPatternProperties or (B^.AdditionalProperties >= 0) or (Length(B^.Required) <> 0)
        or B^.MinProperties.IsSet or B^.MaxProperties.IsSet then
        Inc(Effective);
    end;
    if Effective < 2 then
      Exit(False);
  end;

  { Every name any branch or condition knows gets an index. }
  Known := nil;
  for I := 0 to Length(Ctx.Contributors) - 1 do begin
    B := @P.Nodes[Ctx.Contributors[I].Node];
    for J := 0 to Length(B^.Properties) - 1 do
      Bit(B^.Properties[J].Name);
    for J := 0 to Length(B^.Required) - 1 do
      Bit(B^.Required[J]);
  end;
  F.AltGroups := Ctx.AltGroups;
  TestsByEntry := nil;
  SetLength(F.Conditions, Length(Ctx.Conditions));
  for I := 0 to Length(Ctx.Conditions) - 1 do begin
    Required := nil;
    if Ctx.Conditions[I].IsTest then begin
      Test := @P.Nodes[Ctx.Conditions[I].Test];
      for J := 0 to Length(Test^.PatternProperties) - 1 do begin
        K := Length(F.Absent);
        SetLength(F.Absent, K + 1);
        F.Absent[K].Condition := UInt16(I);
        F.Absent[K].Pattern := Test^.PatternProperties[J].Pattern;
      end;
      for J := 0 to Length(Test^.Properties) - 1 do begin
        ValueTestOf(P, P.Nodes[P.FastTarget[Test^.Properties[J].Node]], VT);
        VT.Condition := UInt16(I);
        K := Length(TestsByEntry);
        SetLength(TestsByEntry, K + 1);
        TestsByEntry[K].Entry := Bit(Test^.Properties[J].Name);
        TestsByEntry[K].Test := VT;
      end;
      Required := BitsOf(Test^.Required);
    end else begin
      SetLength(Required, 1);
      Required[0] := Bit(Ctx.Conditions[I].Dependency);
    end;
    F.Conditions[I].Required := Required;
    F.Conditions[I].Gate := Ctx.Conditions[I].Gate;
  end;
  SetLength(F.Forbidden, Length(Ctx.Forbidden));
  for I := 0 to Length(Ctx.Forbidden) - 1 do begin
    F.Forbidden[I].Gate := Ctx.Forbidden[I].Gate;
    F.Forbidden[I].Names := BitsOf(Ctx.Forbidden[I].Names);
  end;
  Extras := nil;
  SetLength(Extras, Length(Ctx.Extras));
  for I := 0 to Length(Ctx.Extras) - 1 do begin
    Extras[I].Condition := Ctx.Extras[I].Condition;
    Extras[I].Names := BitsOf(Ctx.Extras[I].Names);
  end;
  SetLength(F.Alternatives, Length(Ctx.Alternatives));
  for I := 0 to Length(Ctx.Alternatives) - 1 do begin
    F.Alternatives[I].Condition := Ctx.Alternatives[I].Condition;
    F.Alternatives[I].ExactlyOne := Ctx.Alternatives[I].ExactlyOne;
    SetLength(F.Alternatives[I].Branches, Length(Ctx.Alternatives[I].Branches));
    for J := 0 to Length(Ctx.Alternatives[I].Branches) - 1 do
      F.Alternatives[I].Branches[J] := BitsOf(Ctx.Alternatives[I].Branches[J]);
  end;
  if Length(Known) > MaxFusedNames then begin
    F := Default(TFusedObject);
    Exit(False);
  end;

  SetLength(F.Contributors, Length(Ctx.Contributors) + Length(Extras));
  for I := 0 to Length(Ctx.Contributors) - 1 do begin
    B := @P.Nodes[Ctx.Contributors[I].Node];
    FC := @F.Contributors[I];
    FC^ := Default(TFusedContributor);
    FC^.Condition := Ctx.Contributors[I].Condition;
    FC^.Alt := Ctx.Contributors[I].Alt;
    FC^.Required := BitsOf(B^.Required);
    FC^.Min := B^.MinProperties;
    FC^.Max := B^.MaxProperties;
    SetLength(FC^.Patterns, Length(B^.PatternProperties));
    for J := 0 to Length(B^.PatternProperties) - 1 do begin
      FC^.Patterns[J].Pattern := B^.PatternProperties[J].Pattern;
      FC^.Patterns[J].Child := AppChild(B^.PatternProperties[J].Node);
    end;
    if B^.AdditionalProperties >= 0 then begin
      FC^.HasAdditional := True;
      FC^.Additional := AppChild(B^.AdditionalProperties);
    end;
  end;
  for I := 0 to Length(Extras) - 1 do begin
    FC := @F.Contributors[Length(Ctx.Contributors) + I];
    FC^ := Default(TFusedContributor);
    FC^.Condition := GateOf(Extras[I].Condition, True);
    FC^.Required := Extras[I].Names;
  end;

  Tests := nil;
  SetLength(Tests, Length(Known));
  for I := 0 to Length(TestsByEntry) - 1 do
    AddTest(TestsByEntry[I].Entry, TestsByEntry[I].Test);
  { A known name matching an absent pattern fails its condition whatever its value: no constant is allowed. }
  for E := 0 to Length(Known) - 1 do
    for J := 0 to Length(F.Absent) - 1 do
      if PatternMatchString(P.Patterns[F.Absent[J].Pattern], Known[E]) then begin
        VT := Default(TValueTest);
        VT.Pattern := -1;
        VT.Condition := F.Absent[J].Condition;
        AddTest(E, VT);
      end;

  { Resolve every known name against every branch now. }
  SetLength(F.Entries, Length(Known));
  for I := 0 to Length(Known) - 1 do begin
    Apps := nil;
    for C := 0 to Length(Ctx.Contributors) - 1 do begin
      B := @P.Nodes[Ctx.Contributors[C].Node];
      Matched := False;
      for J := 0 to Length(B^.Properties) - 1 do
        if B^.Properties[J].Name = Known[I] then begin
          Matched := True;
          AddApp(C, B^.Properties[J].Node);
          Break;
        end;
      for J := 0 to Length(B^.PatternProperties) - 1 do
        if PatternMatchString(P.Patterns[B^.PatternProperties[J].Pattern], Known[I]) then begin
          Matched := True;
          AddApp(C, B^.PatternProperties[J].Node);
        end;
      if not Matched and (B^.AdditionalProperties >= 0) then begin
        A := B^.AdditionalProperties;
        AddApp(C, A);
      end;
    end;
    F.Entries[I] := Default(TFusedEntry);
    F.Entries[I].Apps := CoalesceApps(Apps, F.Contributors);
    F.Entries[I].Tests := Tests[I];
    F.Entries[I].HasMerged := MergeValueTests(P, Tests[I], F.Entries[I].Merged);
  end;

  Flat := (Length(F.Conditions) = 0) and (Length(F.Alternatives) = 0) and (Length(Ctx.AltGroups) = 0)
    and (Length(F.Forbidden) = 0) and (N^.UnevaluatedProperties < 0) and (Length(Known) <= 64);
  for I := 0 to Length(F.Contributors) - 1 do begin
    FC := @F.Contributors[I];
    Flat := Flat and not FC^.Condition.IsSet and (Length(FC^.Patterns) = 0) and not FC^.HasAdditional;
    F.ResolvesUnknown := F.ResolvesUnknown or (Length(FC^.Patterns) <> 0) or FC^.HasAdditional;
    F.HasCountBounds := F.HasCountBounds or FC^.Min.IsSet or FC^.Max.IsSet;
  end;
  for I := 0 to Length(F.Entries) - 1 do
    Flat := Flat and (Length(F.Entries[I].Apps) <= 1) and (Length(F.Entries[I].Tests) = 0);
  F.ResolvesUnknown := F.ResolvesUnknown or (Length(F.Absent) <> 0);
  F.Names := NewNames(Known);
  for I := 0 to Length(F.Contributors) - 1 do begin
    FC := @F.Contributors[I];
    if FC^.Condition.IsSet and FC^.Condition.Polarity then
      FC^.ThenMask := One64 shl FC^.Condition.Condition
    else if FC^.Condition.IsSet then
      FC^.ElsMask := One64 shl FC^.Condition.Condition;
    if (Length(FC^.Required) <> 0) or FC^.Min.IsSet or FC^.Max.IsSet then begin
      K := Length(F.Finals);
      SetLength(F.Finals, K + 1);
      F.Finals[K] := UInt16(I);
    end;
  end;
  for I := 0 to Length(F.Entries) - 1 do
    for J := 0 to Length(F.Entries[I].Apps) - 1 do begin
      App := @F.Entries[I].Apps[J];
      App^.ThenMask := F.Contributors[App^.Contributor].ThenMask;
      App^.ElsMask := F.Contributors[App^.Contributor].ElsMask;
      for K := 0 to Length(App^.Others) - 1 do begin
        App^.ThenMask := App^.ThenMask or F.Contributors[App^.Others[K]].ThenMask;
        App^.ElsMask := App^.ElsMask or F.Contributors[App^.Others[K]].ElsMask;
      end;
    end;
  if Flat then begin
    F.HasFlat := True;
    F.Flat := Default(TObjectPlan);
    F.Flat.Max := MaxUInt64;
    F.Flat.Visit := VisitNames;
    F.Flat.Names := F.Names;
    F.Flat.Declared := Length(Known);
    F.Flat.PropertyNames := NoNode;
    SetLength(F.Flat.Children, Length(Known));
    SetLength(F.Flat.NamePatterns, Length(Known));
    F.Flat.RestFree := True;
    F.Flat.IsStrict := True;
    { No contributor has additionalProperties (flat requires it). }
    F.Flat.Lookup := Length(Known) <= LookupNames;
    for I := 0 to Length(F.Contributors) - 1 do begin
      FC := @F.Contributors[I];
      for J := 0 to Length(FC^.Required) - 1 do
        F.Flat.RequiredMask := F.Flat.RequiredMask or (One64 shl FC^.Required[J]);
      if FC^.Min.IsSet and (FC^.Min.N > F.Flat.Min) then
        F.Flat.Min := FC^.Min.N;
      if FC^.Max.IsSet and (FC^.Max.N < F.Flat.Max) then
        F.Flat.Max := FC^.Max.N;
    end;
    for I := 0 to Length(F.Entries) - 1 do begin
      F.Flat.Children[I] := NoChild;
      if (Length(F.Entries[I].Apps) <> 0) and F.Entries[I].Apps[0].HasChild then
        F.Flat.Children[I] := F.Entries[I].Apps[0].Child;
    end;
  end;
  if N^.UnevaluatedProperties >= 0 then begin
    F.HasUnevaluated := True;
    F.Unevaluated := ResolvedChild(Summary, P.FastTarget[N^.UnevaluatedProperties]);
  end;
  Result := True;
end;

end.
