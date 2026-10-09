program TestResults;

{$I corvus.inc}

{ Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
  traced through the C# collecting path (row order, locations, messages, levels). A port of results_test.go and
  annotations_test.go of the Go module.

  The annotation tests run the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through
  a verbose results collector, as the C# and Rust annotation tests do: every draft, cases filtered by
  "compatibility", and each assertion compared with the annotations grouped by instance location, keyword and
  schema location.

  With --dump <file> the program instead writes, for every case of the JSON-Schema-Test-Suite, the verdict and
  every row of a Verbose, a Detailed and a Basic evaluation, and the annotations, a line for each. A test that
  writes the same lines from the Go module gives a file to compare this one with. }

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Values,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema;

{$I SuiteCommon.inc}

var
  Checks, Failures: Int32;
  Remotes: TRemotes;

procedure Check(Ok: Boolean; const What: UTF8String);
begin
  Inc(Checks);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What);
  end;
end;

function MustCompile(const Schema: UTF8String): IJsonSchemaValidator; overload;
begin
  Result := CompileJsonSchema(Schema);
end;

function MustCompile(const Schema: UTF8String; const Options: TJsonSchemaOptions): IJsonSchemaValidator; overload;
begin
  Result := CompileJsonSchema(Schema, Options);
end;

function EntryPoint(const Reference: UTF8String): TJsonSchemaOptions;
begin
  Result := DefaultJsonSchemaOptions;
  WithEntryPoint(Result, Reference);
end;

function DumpResults(const V: IJsonSchemaValidator; const Instance: UTF8String;
  Level: TJsonSchemaResultsLevel): UTF8String;
var
  C: TJsonSchemaResults;
  Rows: TJsonSchemaResultArray;
  I: Int32;
  Outcome: UTF8String;
begin
  C := NewJsonSchemaResults(Level);
  V.Evaluate(Instance, C);
  Rows := ResultRows(C);
  Result := '';
  for I := 0 to Length(Rows) - 1 do begin
    Outcome := 'fail';
    if Rows[I].IsMatch then
      Outcome := 'match';
    if I > 0 then
      Result := Result + #10;
    Result := Result + Outcome + '|' + Rows[I].SchemaEvaluationLocation + '|' + Rows[I].EvaluationLocation + '|'
      + Rows[I].DocumentEvaluationLocation + '|' + Rows[I].Message;
  end;
end;

procedure ExpectRows(const What, Got: UTF8String; const Want: array of UTF8String);
var
  Expected: UTF8String;
  I: Int32;
begin
  Expected := '';
  for I := 0 to High(Want) do begin
    if I > 0 then
      Expected := Expected + #10;
    Expected := Expected + Want[I];
  end;
  Inc(Checks);
  if Got <> Expected then begin
    Inc(Failures);
    WriteLn('FAILED: ', What, ': rows:');
    WriteLn(Got);
    WriteLn('want:');
    WriteLn(Expected);
  end;
end;

function EndsWith(const S, Suffix: UTF8String): Boolean;
begin
  Result := (Length(S) >= Length(Suffix)) and (Copy(S, Length(S) - Length(Suffix) + 1, Length(Suffix)) = Suffix);
end;

const
  PersonSchema: UTF8String = '{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"type": "object",'
    + '"title": "Person",'
    + '"properties": {'
    + '"name": { "type": "string", "minLength": 1, "description": "The name" },'
    + '"age": { "type": "integer", "minimum": 0 }'
    + '},'
    + '"required": ["name"],'
    + '"additionalProperties": false'
    + '}';

  RefsSchema: UTF8String = '{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"$defs": {'
    + '"fooId": { "type": "integer", "minimum": 0 },'
    + '"holder": { "type": "object", "properties": { "fooId": { "$ref": "#/$defs/fooId" } } },'
    + '"viaRef": { "$ref": "#/$defs/fooId" }'
    + '}'
    + '}';

  ExampleSchema: UTF8String = '{'
    + '"type": "object",'
    + '"properties": { "a": { "type": "string" } },'
    + '"required": ["b"],'
    + '"anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]'
    + '}';

procedure TestFlagAndCollectingEvaluationAgreeAtEveryLevel;
const
  Instances: array[0..5] of UTF8String = ('{ "name": "a", "age": 3 }', '{ "name": "", "age": 3 }', '{ "age": 3 }',
    '{ "name": "a", "extra": 1 }', '{ "name": "a", "age": -1 }', '[]');
  Expected: array[0..5] of Boolean = (True, False, False, False, False, False);
  Levels: array[0..2] of TJsonSchemaResultsLevel = (Basic, Detailed, Verbose);
var
  V: IJsonSchemaValidator;
  I, L: Int32;
  C: TJsonSchemaResults;
begin
  V := MustCompile(PersonSchema);
  for I := 0 to 5 do begin
    Check(V.IsValid(Instances[I]) = Expected[I], 'IsValid(' + Instances[I] + ')');
    for L := 0 to 2 do begin
      C := NewJsonSchemaResults(Levels[L]);
      Check(V.Evaluate(Instances[I], C) = Expected[I], 'Evaluate(' + Instances[I] + ')');
    end;
  end;
end;

procedure TestBasicResultsReportFailingKeywordsWithLocations;
var
  C: TJsonSchemaResults;
  Rows: TJsonSchemaResultArray;
  I: Int32;
  MinLength, Minimum, SchemaLocation: Boolean;
begin
  C := NewJsonSchemaResults(Basic);
  Check(not MustCompile(PersonSchema).Evaluate('{ "name": "", "age": -1 }', C), 'basic: the instance is valid');
  MinLength := False;
  Minimum := False;
  SchemaLocation := False;
  Rows := ResultRows(C);
  for I := 0 to Length(Rows) - 1 do begin
    Check(Rows[I].Message = '', 'a Basic row has a message: ' + Rows[I].Message);
    if Rows[I].IsMatch then
      Continue;
    MinLength := MinLength or (EndsWith(Rows[I].EvaluationLocation, '/minLength')
      and (Rows[I].DocumentEvaluationLocation = '/name'));
    Minimum := Minimum or (EndsWith(Rows[I].EvaluationLocation, '/minimum')
      and (Rows[I].DocumentEvaluationLocation = '/age'));
    SchemaLocation := SchemaLocation or (Rows[I].SchemaEvaluationLocation = '/properties/name');
  end;
  Check(MinLength and Minimum and SchemaLocation, 'basic: missing rows');
end;

procedure TestVerboseAnnotationsAreProduced;
var
  C: TJsonSchemaResults;
  Annotations: TJsonSchemaCollectedAnnotationArray;
  Value: UTF8String;
  I, Titles: Int32;
begin
  C := NewJsonSchemaResults(Verbose);
  Check(MustCompile(PersonSchema).Evaluate('{ "name": "a" }', C), 'annotations: the instance is invalid');
  Annotations := CollectedAnnotations(C);
  Titles := 0;
  for I := 0 to Length(Annotations) - 1 do
    if (Annotations[I].InstanceLocation = '') and (Annotations[I].Keyword = 'title') then
      Inc(Titles);
  Check(FindAnnotation(Annotations, '', 'title', '#', Value) and (Value = '"Person"') and (Titles = 1),
    'title: ' + Value);
  Check(FindAnnotation(Annotations, '/name', 'description', '#/properties/name', Value) and (Value = '"The name"'),
    'description: ' + Value);
end;

procedure TestVerboseOutputFollowsTheCSharpRowOrder;
begin
  ExpectRows('row order', DumpResults(MustCompile(PersonSchema), '{ "name": "a" }', Verbose), [
    'match|/properties/name|/properties/name|/name|The value was expected to match the subschema.',
    'match|/properties/name|/properties/name/description|/name|"The name"',
    'match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value to be '
      + 'greater than or equal to ''1''',
    'match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type ''string''',
    'match||||The value was expected to match the subschema.',
    'match||/title||"Person"',
    'match|/required|/required|/name|Required property present ''name''',
    'match|/type|/type||The value was expected to be of type ''object''']);
end;

function HasRow(const Rows: TJsonSchemaResultArray; IsMatch: Boolean; const Location, Message: UTF8String): Boolean;
var
  I: Int32;
begin
  for I := 0 to Length(Rows) - 1 do
    if (Rows[I].IsMatch = IsMatch) and (Rows[I].EvaluationLocation = Location) and (Rows[I].Message <> '')
      and ((Message = '') or (Rows[I].Message = Message)) then
      Exit(True);
  Result := False;
end;

procedure TestMatchingKeywordsCarryTheirMessageInVerboseOutput;
var
  V: IJsonSchemaValidator;
  C: TJsonSchemaResults;
  Valid: Boolean;
begin
  V := MustCompile('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"type": ["integer", "array"],'
    + '"uniqueItems": true,'
    + '"properties": { "n": { "type": "integer" } }'
    + '}');
  C := NewJsonSchemaResults(Verbose);
  Valid := V.Evaluate('[1, 2]', C);
  Check(Valid and HasRow(ResultRows(C), True, '/type', 'The value was expected to be of type ''["array", "integer"]''')
    and HasRow(ResultRows(C), True, '/uniqueItems', ''), '[1, 2]');
  C := NewJsonSchemaResults(Verbose);
  Valid := V.Evaluate('[1, 1]', C);
  Check(not Valid and HasRow(ResultRows(C), False, '/uniqueItems', ''), '[1, 1]');
  C := NewJsonSchemaResults(Verbose);
  Valid := MustCompile('{ "type": "integer" }').Evaluate('3', C);
  Check(Valid and HasRow(ResultRows(C), True, '/type', 'The value was expected to be of type ''integer'''),
    'integer');
end;

procedure TestAnEntryPointReportsItsOwnSchemaLocation;
begin
  ExpectRows('entry point', DumpResults(MustCompile(RefsSchema, EntryPoint('#/$defs/fooId')), '"notAnInteger"',
    Detailed), [
    'fail|/$defs/fooId|||The value was expected to match the subschema.',
    'fail|/$defs/fooId/type|/type||The value was expected to be of type ''integer''']);
end;

procedure TestAPureRefPropertyIsElidedWithRefInTheEvaluationPath;
begin
  ExpectRows('pure $ref property', DumpResults(MustCompile(RefsSchema, EntryPoint('#/$defs/holder')),
    '{ "fooId": "notAnInteger" }', Detailed), [
    'fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the subschema.',
    'fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of type ''integer''',
    'fail|/$defs/holder|||The value was expected to match the subschema.']);
end;

procedure TestAPureRefRootReportsAgainstItsTarget;
begin
  ExpectRows('pure $ref root', DumpResults(MustCompile(RefsSchema, EntryPoint('#/$defs/viaRef')), '"notAnInteger"',
    Detailed), [
    'fail|/$defs/fooId|||The value was expected to match the subschema.',
    'fail|/$defs/fooId/type|/type||The value was expected to be of type ''integer''']);
end;

procedure TestARequiredFailureCarriesThePropertyName;
begin
  ExpectRows('required', DumpResults(MustCompile('{ "type": "object", "required": ["name"] }'), '{}', Detailed), [
    'fail||||The value was expected to match the subschema.',
    'fail|/required|/required|/name|Required property not present ''name''']);
end;

procedure TestDetailedOutputKeepsFailuresOnlyWithMessages;
const
  Expected: array[0..3] of UTF8String = (
    'fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.',
    'fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type ''string''',
    'fail||||The value was expected to match the subschema.',
    'fail|/required|/required|/b|Required property not present ''b''');
var
  V: IJsonSchemaValidator;
  BasicRows: array[0..3] of UTF8String;
  I, K, Bar: Int32;
begin
  V := MustCompile(ExampleSchema);
  ExpectRows('detailed', DumpResults(V, '{ "a": 1 }', Detailed), Expected);
  for I := 0 to 3 do begin
    Bar := 0;
    for K := 1 to Length(Expected[I]) do
      if Expected[I][K] = '|' then
        Bar := K;
    BasicRows[I] := Copy(Expected[I], 1, Bar);
  end;
  ExpectRows('basic', DumpResults(V, '{ "a": 1 }', Basic), BasicRows);
end;

procedure TestVerboseOutputReversesAContextsOwnRowsAfterItsSummary;
begin
  ExpectRows('reversed rows', DumpResults(MustCompile(ExampleSchema), '{ "a": 1 }', Verbose), [
    'fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.',
    'fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type ''string''',
    'match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.',
    'match|/anyOf/0/required|/anyOf/0/required|/a|Required property present ''a''',
    'fail||||The value was expected to match the subschema.',
    'match|/anyOf|/anyOf||The value matched at least one subschema.',
    'fail|/required|/required|/b|Required property not present ''b''',
    'match|/type|/type||The value was expected to be of type ''object''']);
end;

procedure TestNotSubtreesAndBooleanSchemas;
begin
  ExpectRows('not', DumpResults(MustCompile('{ "not": { "type": "string" } }'), '"x"', Detailed), [
    'fail||||The value was expected to match the subschema.',
    'fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was not a '
      + 'match.']);
  ExpectRows('false', DumpResults(MustCompile('false'), '1', Detailed), [
    'fail||||The value was expected to match the subschema.', 'fail||||']);
end;

procedure TestAValidInstanceAtDetailedLevelYieldsOnlyThePassingRootRow;
begin
  ExpectRows('valid at Detailed', DumpResults(MustCompile(PersonSchema), '{ "name": "a" }', Detailed), ['match||||']);
end;

procedure TestPropertyNamesKeepsTheObjectLocationAndAddsAFailureRowPerName;
begin
  ExpectRows('propertyNames', DumpResults(MustCompile('{ "propertyNames": { "maxLength": 2 } }'), '{ "abc": 1 }',
    Detailed), [
    'fail|/propertyNames|/propertyNames||The value was expected to match the subschema.',
    'fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less than or '
      + 'equal to ''2''',
    'fail||||The value was expected to match the subschema.',
    'fail|/propertyNames|/propertyNames||The property name did not match the schema.']);
end;

procedure TestDraft4ExclusiveBoundsReportUnderExclusiveMaximumWithTheMaximum;
begin
  ExpectRows('draft 4 exclusiveMaximum', DumpResults(MustCompile(
    '{ "$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, "exclusiveMaximum": true }'), '3',
    Detailed), [
    'fail||||The value was expected to match the subschema.',
    'fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than ''3''']);
end;

procedure TestFailingAnyOfBranchesAreDiscardedAndUnevaluatedPropertiesHasNoMessage;
begin
  ExpectRows('unevaluatedProperties', DumpResults(MustCompile(
    '{ "anyOf": [{ "properties": { "a": true } }, { "required": ["zz"] }], "unevaluatedProperties": false }'),
    '{ "a": 1, "b": 2 }', Detailed), [
    'fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the subschema.',
    'fail|/unevaluatedProperties|/unevaluatedProperties|/b|',
    'fail||||The value was expected to match the subschema.',
    'fail|/unevaluatedProperties|/unevaluatedProperties||']);
end;

procedure TestACollectorAccumulatesAcrossEvaluations;
var
  V: IJsonSchemaValidator;
  C: TJsonSchemaResults;
  First: Int32;
begin
  V := MustCompile(PersonSchema);
  C := NewJsonSchemaResults(Detailed);
  V.Evaluate('{ "age": "x" }', C);
  First := ResultCount(C);
  V.Evaluate('{ "age": "x" }', C);
  Check((First <> 0) and (ResultCount(C) = 2 * First), 'a collector accumulates');
  ResetResults(C);
  Check(ResultCount(C) = 0, 'rows after Reset');
end;

procedure TestDependenciesReportsUnderItsOwnNameInEveryDialect;
var
  Rows: UTF8String;
begin
  ExpectRows('dependencies', DumpResults(MustCompile('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"dependencies": { "a": ["b"], "c": { "required": ["d"] } }'
    + '}'), '{ "a": 1, "c": 1 }', Detailed), [
    'fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.',
    'fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present ''d''',
    'fail||||The value was expected to match the subschema.',
    'fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the property '
      + '''c''',
    'fail|/dependencies|/dependencies|/b|Required property not present ''b''']);
  Rows := DumpResults(MustCompile(
    '{ "dependentRequired": { "a": ["b"] }, "dependentSchemas": { "c": { "required": ["d"] } } }'),
    '{ "a": 1, "c": 1 }', Detailed);
  Check((Pos('fail|/dependentRequired|/dependentRequired|/b|Required property not present ''b''', Rows) > 0)
    and (Pos('fail|/dependentSchemas/c|/dependentSchemas/c||', Rows) > 0), 'dependentRequired rows: ' + Rows);
end;

procedure TestAStaticallyResolvedDynamicRefHopIsNamedDynamicRefInTheEvaluationPath;
begin
  ExpectRows('static $dynamicRef', DumpResults(MustCompile('{'
    + '"$schema": "https://json-schema.org/draft/2020-12/schema",'
    + '"properties": { "p": { "$dynamicRef": "#/$defs/n" } },'
    + '"$defs": { "n": { "type": "integer" } }'
    + '}'), '{ "p": "x" }', Detailed), [
    'fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.',
    'fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type ''integer''',
    'fail||||The value was expected to match the subschema.']);
end;

{ --------------------------------------------------------------------------------------------------------------------
  The annotation suite }

function CompatibilityIndex(const Level: UTF8String): Int32;
const
  Order: array[0..5] of UTF8String = ('3', '4', '6', '7', '2019', '2020');
var
  I: Int32;
begin
  for I := 0 to 5 do
    if Order[I] = Level then
      Exit(I);
  Result := -1;
end;

function Compatible(const Level, Compatibility: UTF8String): Boolean;
var
  At, Limit, Least: Int32;
begin
  At := CompatibilityIndex(Level);
  if (Length(Compatibility) > 2) and (Copy(Compatibility, 1, 2) = '<=') then begin
    Limit := CompatibilityIndex(Copy(Compatibility, 3, Length(Compatibility) - 2));
    Exit((Limit >= 0) and (At <= Limit));
  end;
  Least := CompatibilityIndex(Compatibility);
  Result := (Least >= 0) and (At >= Least);
end;

{ SameAnnotations compares the produced annotations of a keyword at a location with the expected ones: the same
  schema locations, each with an equal JSON value (numbers by value, objects unordered). Expected is an object of
  the suite's document, from a schema location fragment to a value. }
function SameAnnotations(const Produced: TJsonSchemaCollectedAnnotationArray; const Location, Keyword: UTF8String;
  const Suite: TDocument; Expected: Int32): Boolean;
var
  Actual, I, K: Int32;
  Value, Error: UTF8String;
  A: TJsonDocument;
  Offset: Int32;
begin
  Result := False;
  Actual := 0;
  for I := 0 to Length(Produced) - 1 do
    if (Produced[I].InstanceLocation = Location) and (Produced[I].Keyword = Keyword) then
      Inc(Actual);
  if Actual <> DocCount(Suite, Expected) then
    Exit;
  for I := 0 to DocCount(Suite, Expected) - 1 do begin
    K := DocFirst(Suite, Expected) + 2 * I;
    if not FindAnnotation(Produced, Location, Keyword, DocStrCopy(Suite, K), Value) then
      Exit;
    if not TryParseJson(Value, A, Error, Offset) or not ValuesEqual(A, A.Root, Suite, K + 1) then
      Exit;
  end;
  Result := True;
end;

procedure TestAnnotationSuite;
const
  DraftNames: array[0..4] of UTF8String = ('draft4', 'draft6', 'draft7', 'draft2019-09', 'draft2020-12');
  DraftDialects: array[0..4] of TJsonSchemaDialect = (Draft4, Draft6, Draft7, Draft201909, Draft202012);
  DraftLevels: array[0..4] of UTF8String = ('4', '6', '7', '2019', '2020');
var
  Root, Dir, Name, Compatibility, Description, Location, Keyword: UTF8String;
  Files: TUTF8StringArray;
  D, F, G, T, A, Total, Failed: Int32;
  Text: TBytes;
  Doc: TDocument;
  E: TParseError;
  Suite, Group, Schema, Tests, Test, Assertions, Assertion, N: Int32;
  Options: TJsonSchemaOptions;
  Validator: IJsonSchemaValidator;
  C: TJsonSchemaResults;
  Produced: TJsonSchemaCollectedAnnotationArray;
begin
  Root := SuiteRoot;
  Dir := Root + '/annotations/tests';
  if not DirectoryExists(Dir) then begin
    Check(False, 'annotation tests not found at ' + Dir
      + ' (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)');
    Exit;
  end;
  Total := 0;
  Failed := 0;
  Files := JsonFiles(Dir);
  for D := 0 to 4 do
    for F := 0 to Length(Files) - 1 do begin
      Name := DraftNames[D] + '/' + Files[F];
      if not ReadFile(Dir + '/' + Files[F], Text) or not ParseDocument(Text, Doc, E) then begin
        Check(False, Name + ' could not be read as JSON');
        Continue;
      end;
      Suite := Member(Doc, Doc.Root, 'suite');
      if not IsKind(Doc, Suite, KindArray) then
        Continue;
      for G := 0 to DocCount(Doc, Suite) - 1 do begin
        Group := DocFirst(Doc, Suite) + G;
        Compatibility := '';
        N := Member(Doc, Group, 'compatibility');
        if IsKind(Doc, N, KindString) then
          Compatibility := DocStrCopy(Doc, N);
        if (Compatibility <> '') and not Compatible(DraftLevels[D], Compatibility) then
          Continue;
        Description := '';
        N := Member(Doc, Group, 'description');
        if IsKind(Doc, N, KindString) then
          Description := DocStrCopy(Doc, N);
        Schema := Member(Doc, Group, 'schema');
        Options := DefaultJsonSchemaOptions;
        WithDefaultDialect(Options, DraftDialects[D]);
        WithDocumentResolver(Options, RemoteResolver, @Remotes);
        try
          Validator := CompileJsonSchema(ValueText(Doc, Schema), Options);
        except
          on Ex: EJsonSchemaError do begin
            Inc(Failed);
            Check(False, Name + ' [' + Description + ']: compile error: ' + Ex.Utf8Message);
            Continue;
          end;
        end;
        Tests := Member(Doc, Group, 'tests');
        if not IsKind(Doc, Tests, KindArray) then
          Continue;
        for T := 0 to DocCount(Doc, Tests) - 1 do begin
          Test := DocFirst(Doc, Tests) + T;
          C := NewJsonSchemaResults(Verbose);
          Validator.Evaluate(ValueText(Doc, Member(Doc, Test, 'instance')), C);
          Produced := CollectedAnnotations(C);
          Assertions := Member(Doc, Test, 'assertions');
          if not IsKind(Doc, Assertions, KindArray) then
            Continue;
          for A := 0 to DocCount(Doc, Assertions) - 1 do begin
            Assertion := DocFirst(Doc, Assertions) + A;
            Inc(Total);
            Location := DocStrCopy(Doc, Member(Doc, Assertion, 'location'));
            Keyword := DocStrCopy(Doc, Member(Doc, Assertion, 'keyword'));
            Inc(Checks);
            if not SameAnnotations(Produced, Location, Keyword, Doc, Member(Doc, Assertion, 'expected')) then begin
              Inc(Failed);
              Inc(Failures);
              WriteLn('FAILED: ', Name, ' [', Description, '] instance ', ValueText(Doc, Member(Doc, Test, 'instance')),
                ' ''', Location, ''' ', Keyword, ': expected ', ValueText(Doc, Member(Doc, Assertion, 'expected')));
            end;
          end;
        end;
      end;
    end;
  WriteLn(Total - Failed, '/', Total, ' annotation assertions passed');
  Check(Total <> 0, 'no annotation assertions ran');
end;

{ --------------------------------------------------------------------------------------------------------------------
  The dump }

var
  Dump: Text;
  DumpCases, DumpRows: Int64;

{ Q is a text with every byte that is not a visible ASCII character, and the characters the dump itself uses,
  written as % and two hexadecimal digits. }
function Q(const S: UTF8String): UTF8String;
const
  Hex: array[0..15] of AnsiChar = '0123456789ABCDEF';
var
  I, N: Int32;
  C: Byte;
begin
  Result := '';
  SetLength(Result, 3 * Length(S));
  N := 0;
  for I := 1 to Length(S) do begin
    C := Byte(S[I]);
    if (C < $20) or (C > $7E) or (C = Ord('%')) or (C = Ord('|')) then begin
      Result[N + 1] := '%';
      Result[N + 2] := Hex[C shr 4];
      Result[N + 3] := Hex[C and 15];
      Inc(N, 3);
    end else begin
      Inc(N);
      Result[N] := AnsiChar(C);
    end;
  end;
  SetLength(Result, N);
end;

function Bit(V: Boolean): AnsiChar;
begin
  if V then
    Result := '1'
  else
    Result := '0';
end;

function CompareEntries(const A, B: TJsonSchemaCollectedAnnotation): Int32;
begin
  Result := CompareUtf8(A.InstanceLocation, B.InstanceLocation);
  if Result = 0 then
    Result := CompareUtf8(A.Keyword, B.Keyword);
  if Result = 0 then
    Result := CompareUtf8(A.SchemaLocationFragment, B.SchemaLocationFragment);
end;

procedure DumpCase(const V: IJsonSchemaValidator; Index: Int32; const Data: UTF8String);
const
  Levels: array[0..2] of TJsonSchemaResultsLevel = (Verbose, Detailed, Basic);
  LevelNames: array[0..2] of UTF8String = ('Verbose', 'Detailed', 'Basic');
var
  Document: TJsonDocument;
  C: TJsonSchemaResults;
  L, I, J: Int32;
  Ok: Boolean;
  Annotations: TJsonSchemaAnnotationArray;
  Collected: TJsonSchemaCollectedAnnotationArray;
  Entry: TJsonSchemaCollectedAnnotation;
begin
  Document := ParseJson(Data);
  Inc(DumpCases);
  WriteLn(Dump, 'T ', Index, ' ', Bit(V.IsValid(Document)));
  for L := 0 to 2 do begin
    C := NewJsonSchemaResults(Levels[L]);
    Ok := V.Evaluate(Document, C);
    WriteLn(Dump, 'L ', LevelNames[L], ' ', Bit(Ok), ' ', ResultCount(C));
    for I := 0 to ResultCount(C) - 1 do begin
      Inc(DumpRows);
      WriteLn(Dump, 'R ', Bit(C.Committed[I].IsMatch), '|', Q(C.Committed[I].Message), '|',
        Q(C.Committed[I].EvaluationLocation), '|', Q(C.Committed[I].SchemaEvaluationLocation), '|',
        Q(C.Committed[I].DocumentEvaluationLocation));
    end;
    if L = 0 then begin
      Annotations := ResultAnnotations(C);
      WriteLn(Dump, 'A ', Length(Annotations));
      for I := 0 to Length(Annotations) - 1 do
        WriteLn(Dump, 'a ', Q(Annotations[I].InstanceLocation), '|', Q(Annotations[I].Keyword), '|',
          Q(Annotations[I].SchemaLocation), '|', Q(Annotations[I].Value));
      { The annotations by location, keyword and fragment, which are a map in the Go module, in sorted order. }
      Collected := CollectedAnnotations(C);
      for I := 1 to Length(Collected) - 1 do begin
        Entry := Collected[I];
        J := I - 1;
        while (J >= 0) and (CompareEntries(Collected[J], Entry) > 0) do begin
          Collected[J + 1] := Collected[J];
          Dec(J);
        end;
        Collected[J + 1] := Entry;
      end;
      WriteLn(Dump, 'M ', Length(Collected));
      for I := 0 to Length(Collected) - 1 do
        WriteLn(Dump, 'm ', Q(Collected[I].InstanceLocation), '|', Q(Collected[I].Keyword), '|',
          Q(Collected[I].SchemaLocationFragment), '|', Q(Collected[I].Value));
    end;
  end;
end;

procedure DumpFile(Dialect: TJsonSchemaDialect; const Path, Name: UTF8String; AssertFormat: Boolean);
var
  Text: TBytes;
  Doc: TDocument;
  E: TParseError;
  G, T, Group, Tests: Int32;
  Options: TJsonSchemaOptions;
  Validator: IJsonSchemaValidator;
begin
  if not ReadFile(Path, Text) or not ParseDocument(Text, Doc, E) then begin
    WriteLn('FAILED: ', Path, ' could not be read as JSON');
    Halt(1);
  end;
  for G := 0 to DocCount(Doc, Doc.Root) - 1 do begin
    Group := DocFirst(Doc, Doc.Root) + G;
    WriteLn(Dump, 'S ', Q(Name), ' ', G);
    Options := DefaultJsonSchemaOptions;
    WithDefaultDialect(Options, Dialect);
    WithDocumentResolver(Options, RemoteResolver, @Remotes);
    if AssertFormat then
      WithAssertFormat(Options, True);
    try
      Validator := CompileJsonSchema(ValueText(Doc, Member(Doc, Group, 'schema')), Options);
    except
      on EJsonSchemaError do begin
        WriteLn(Dump, 'E');
        Continue;
      end;
    end;
    Tests := Member(Doc, Group, 'tests');
    for T := 0 to DocCount(Doc, Tests) - 1 do
      DumpCase(Validator, T, ValueText(Doc, Member(Doc, DocFirst(Doc, Tests) + T, 'data')));
  end;
end;

procedure RunDump(const FileName: UTF8String);
const
  DraftNames: array[0..4] of UTF8String = ('draft4', 'draft6', 'draft7', 'draft2019-09', 'draft2020-12');
  DraftDialects: array[0..4] of TJsonSchemaDialect = (Draft4, Draft6, Draft7, Draft201909, Draft202012);
var
  Tests, Dir, Name: UTF8String;
  Files: TUTF8StringArray;
  D, I: Int32;
begin
  Tests := SuiteRoot + '/tests';
  if not DirectoryExists(Tests) then begin
    WriteLn('JSON-Schema-Test-Suite not found at ', SuiteRoot);
    Halt(1);
  end;
  Assign(Dump, FileName);
  Rewrite(Dump);
  DumpCases := 0;
  DumpRows := 0;
  for D := 0 to 4 do begin
    Dir := Tests + '/' + DraftNames[D];
    Files := JsonFiles(Dir);
    for I := 0 to Length(Files) - 1 do
      DumpFile(DraftDialects[D], Dir + '/' + Files[I], DraftNames[D] + '/' + Files[I], False);
    Files := JsonFiles(Dir + '/optional');
    for I := 0 to Length(Files) - 1 do begin
      Name := DraftNames[D] + '/optional/' + Files[I];
      if Name <> 'draft4/optional/zeroTerminatedFloats.json' then
        DumpFile(DraftDialects[D], Dir + '/optional/' + Files[I], Name, False);
    end;
    Files := JsonFiles(Dir + '/optional/format');
    for I := 0 to Length(Files) - 1 do
      DumpFile(DraftDialects[D], Dir + '/optional/format/' + Files[I], DraftNames[D] + '/optional/format/' + Files[I],
        True);
  end;
  Close(Dump);
  WriteLn(DumpCases, ' cases and ', DumpRows, ' result rows written to ', FileName);
end;

begin
  Checks := 0;
  Failures := 0;
  Remotes := Default(TRemotes);
  Remotes.Root := SuiteRoot + '/remotes';
  if (ParamCount = 2) and (ParamStr(1) = '--dump') then begin
    RunDump(UTF8String(ParamStr(2)));
    JsonSchemaReleaseThreadScratch;
    Exit;
  end;
  TestFlagAndCollectingEvaluationAgreeAtEveryLevel;
  TestBasicResultsReportFailingKeywordsWithLocations;
  TestVerboseAnnotationsAreProduced;
  TestVerboseOutputFollowsTheCSharpRowOrder;
  TestMatchingKeywordsCarryTheirMessageInVerboseOutput;
  TestAnEntryPointReportsItsOwnSchemaLocation;
  TestAPureRefPropertyIsElidedWithRefInTheEvaluationPath;
  TestAPureRefRootReportsAgainstItsTarget;
  TestARequiredFailureCarriesThePropertyName;
  TestDetailedOutputKeepsFailuresOnlyWithMessages;
  TestVerboseOutputReversesAContextsOwnRowsAfterItsSummary;
  TestNotSubtreesAndBooleanSchemas;
  TestAValidInstanceAtDetailedLevelYieldsOnlyThePassingRootRow;
  TestPropertyNamesKeepsTheObjectLocationAndAddsAFailureRowPerName;
  TestDraft4ExclusiveBoundsReportUnderExclusiveMaximumWithTheMaximum;
  TestFailingAnyOfBranchesAreDiscardedAndUnevaluatedPropertiesHasNoMessage;
  TestACollectorAccumulatesAcrossEvaluations;
  TestDependenciesReportsUnderItsOwnNameInEveryDialect;
  TestAStaticallyResolvedDynamicRefHopIsNamedDynamicRefInTheEvaluationPath;
  TestAnnotationSuite;
  JsonSchemaReleaseThreadScratch;
  WriteLn(Checks - Failures, '/', Checks, ' checks passed');
  if Failures <> 0 then
    Halt(1);
end.
