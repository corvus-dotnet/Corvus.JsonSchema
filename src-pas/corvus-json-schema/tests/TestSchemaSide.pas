program TestSchemaSide;

{$I corvus.inc}

{ The schema side of the evaluator, without an evaluation: JSON equality and hashing, property name lookup, the
  dialects, the embedded metaschemas, the pattern matchers against the regular expression engine, the results
  collector, and schemas compiled to the node graph. Ported from the tests of the Go module that need no evaluator
  (plan_test.go for the names, metaschemas_test.go, pattern_test.go, unicode_test.go, and the parts of
  results_test.go and api_test.go that only compile a schema or drive a collector). }

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Values,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.Uri,
  Corvus.JsonSchema.Formats,
  Corvus.JsonSchema.EcmaRegex,
  Corvus.JsonSchema.EcmaRegex.Utf8,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Metaschemas,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Results,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema.Compiler;

var
  Failures, Checks: Int32;

procedure Check(Ok: Boolean; const What: UTF8String);
begin
  Inc(Checks);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What);
  end;
end;

function Rune(Cp: Int32): UTF8String;
begin
  Result := '';
  AppendRuneString(Result, Cp);
end;

function Parse(const Json: UTF8String): TDocument;
var
  E: TParseError;
begin
  if not ParseDocumentString(Json, Result, E) then begin
    Check(False, Json + ' -> ' + ParseErrorText(E));
    ParseDocumentString('null', Result, E);
  end;
end;

function ReadFile(const Path: UTF8String; out Bytes: TBytes): Boolean;
var
  F: THandle;
  Size, Got: Int64;
begin
  Bytes := nil;
  Result := False;
  F := FileOpen(Path, fmOpenRead or fmShareDenyNone);
  if F = THandle(-1) then
    Exit;
  Size := FileSeek(F, Int64(0), 2);
  FileSeek(F, Int64(0), 0);
  SetLength(Bytes, Size);
  Got := 0;
  if Size > 0 then
    Got := FileRead(F, Bytes[0], Size);
  FileClose(F);
  Result := Got = Size;
end;

function Repeated(const S: UTF8String; N: Int32): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 1 to N do
    Result := Result + S;
end;

{ --------------------------------------------------------------------------------------------------------------------
  Values }

procedure TestValues;
var
  A, B: TDocument;
  Scratch: TUInt64Array;
  Items: UTF8String;
  I: Int32;

  procedure Equal(const X, Y: UTF8String; Expected: Boolean);
  begin
    A := Parse(X);
    B := Parse(Y);
    Check(ValuesEqual(A, A.Root, B, B.Root) = Expected, X + ' = ' + Y);
    Check(ValuesEqual(B, B.Root, A, A.Root) = Expected, Y + ' = ' + X);
    if Expected then
      Check(ValueHash(A, A.Root) = ValueHash(B, B.Root), 'hashes of ' + X + ' and ' + Y);
  end;

  procedure Unique(const X: UTF8String; Expected: Boolean);
  begin
    A := Parse(X);
    Check(AllUnique(A, A.Root, Scratch) = Expected, 'uniqueItems of ' + Copy(X, 1, 60));
  end;

begin
  Scratch := nil;
  Equal('null', 'null', True);
  Equal('true', 'true', True);
  Equal('true', 'false', False);
  Equal('1', '1.0', True);
  Equal('1', '1.5', False);
  Equal('-0', '0', True);
  Equal('1e2', '100', True);
  Equal('9223372036854775808', '9223372036854775808.0', True);
  Equal('18446744073709551615', '18446744073709551616', False);
  Equal('9007199254740993', '9007199254740992.0', False);
  Equal('"a"', '"a"', True);
  Equal('"aé"', '"aé"', True);
  Equal('"a"', '"b"', False);
  Equal('"a"', '1', False);
  Equal('[1, 2, [3]]', '[1.0, 2, [3e0]]', True);
  Equal('[1, 2]', '[2, 1]', False);
  Equal('[1, 2]', '[1, 2, 3]', False);
  Equal('{"a": 1, "b": [1, 2.0]}', '{"b": [1.0, 2], "a": 1.0}', True);
  Equal('{"a": 1, "b": 2, "c": 3}', '{"a": 1, "c": 3, "b": 2}', True);
  Equal('{"a": 1, "b": 2}', '{"a": 1, "c": 2}', False);
  Equal('{"a": 1, "b": 2}', '{"a": 1, "b": 3}', False);
  Equal('{"a": 1}', '{"a": 1, "b": 2}', False);
  Equal('{"a\n": {"b": null}}', '{"a\u000a": {"b": null}}', True);
  Equal('{}', '[]', False);

  A := Parse('{"a": 1, "bb": 2}');
  B := Parse('["bb", "c"]');
  Check(FindProperty(A, A.Root, B, DocFirst(B, B.Root)) = DocProperty(A, A.Root, 'bb'), 'FindProperty finds bb');
  Check(FindProperty(A, A.Root, B, DocFirst(B, B.Root) + 1) = -1, 'FindProperty does not find c');

  Unique('[]', True);
  Unique('[1]', True);
  Unique('["a", "b", "c"]', True);
  Unique('["a", "b", "a"]', False);
  Unique('[1, 1.0]', False);
  Unique('[1, "1", [1], {"1": 1}, true, null]', True);
  Unique('[{"a": 1, "b": 2}, {"b": 2, "a": 1}]', False);
  Unique('[[1, 2], [2, 1]]', True);
  { Unique items over large arrays (TestUniqueItemsOverLargeArrays of the Go module). }
  Items := '';
  for I := 0 to 199 do begin
    if I > 0 then
      Items := Items + ',';
    Items := Items + '{"id": ' + IntText(I) + ', "tags": ["a", ' + IntText(I mod 3) + ']}';
  end;
  Unique('[' + Items + ']', True);
  Unique('[' + Items + ',{"tags": ["a", 1.0], "id": 4e0}]', False);
  { More than thirty-two strings take the hashed path. }
  Items := '';
  for I := 0 to 99 do
    Items := Items + '"s' + IntText(I) + '",';
  Unique('[' + Items + '"s100"]', True);
  Unique('[' + Items + '"s57"]', False);
end;

{ --------------------------------------------------------------------------------------------------------------------
  Names (plan_test.go) }

function IndexOf(const List: TUTF8StringArray; const S: UTF8String): Int32;
var
  I: Int32;
begin
  for I := 0 to Length(List) - 1 do
    if List[I] = S then begin
      Result := I;
      Exit;
    end;
  Result := -1;
end;

function MapFind(const M: TNameMap; const Name: UTF8String): Int32;
var
  B: TBytes;
begin
  B := BytesOf(Name);
  Result := NameMapFind(M, B, 0, Length(B), NameWord(B, 0, Length(B)));
end;

function FindFrom(const Ns: TNames; const Name: UTF8String; var Hint: Int32): Int32;
var
  B: TBytes;
  H: NativeInt;
begin
  B := BytesOf(Name);
  { The hint of the names is as wide as an index (see Corvus.JsonSchema.Checked). }
  H := Hint;
  Result := NamesFindFrom(Ns, B, 0, Length(B), H);
  Hint := H;
end;

procedure AddName(var List: TUTF8StringArray; const S: UTF8String);
var
  N: Int32;
begin
  N := Length(List);
  SetLength(List, N + 1);
  List[N] := S;
end;

function ListOf(const Items: array of UTF8String): TUTF8StringArray;
var
  I: Int32;
begin
  Result := nil;
  SetLength(Result, Length(Items));
  for I := 0 to Length(Items) - 1 do
    Result[I] := Items[I];
end;

procedure TestNameMapFindsEveryName;
var
  List, Misses, Few: TUTF8StringArray;
  M: TNameMap;
  I, K: Int32;
begin
  List := nil;
  for I := 0 to 199 do
    AddName(List, 'p' + IntText(I) + Repeated('x', I mod 13));
  AddName(List, '');
  AddName(List, 'a');
  AddName(List, 'ab');
  AddName(List, 'abc');
  AddName(List, 'abcd');
  AddName(List, 'abcdefgh');
  AddName(List, 'abcdefghi');
  AddName(List, 'é');
  AddName(List, '日本');
  AddName(List, Repeated('y', 300));
  AddName(List, 'abcdefghijkl');
  AddName(List, 'abcdefghijkm');
  AddName(List, 'abcdefghXjkl');
  { Longer than sixteen bytes with the same first and last eight: only the text tells them apart. }
  AddName(List, 'aaaaaaaa-1-bbbbbbbb');
  AddName(List, 'aaaaaaaa-2-bbbbbbbb');
  AddName(List, 'aaaaaaaa-3-bbbbbbbb');
  M := NewNameMap(List);
  for I := 0 to Length(List) - 1 do
    Check(MapFind(M, List[I]) = I, 'find(' + List[I] + ')');
  Misses := ListOf(['p', 'q1', 'p1000', 'p0x', 'b', 'abce', 'abcdefgj', 'abcdefghj', 'è', 'y', Repeated('y', 299),
    'abcdefghijkn', 'abcdefghXjkm', 'aaaaaaaa-4-bbbbbbbb', Repeated('y', 150) + 'z' + Repeated('y', 149)]);
  for I := 0 to Length(Misses) - 1 do
    Check(MapFind(M, Misses[I]) = -1, 'find(' + Misses[I] + ') finds nothing');
  for K := 0 to 5 do begin
    case K of
      0: Few := nil;
      1: Few := ListOf(['a']);
      2: Few := ListOf(['ab', 'ba']);
      3: Few := ListOf(['alpha', 'gamma', 'delta', 'omega']);
      4: Few := ListOf(['abcdefghij', 'abcdefghik']);
    else
      Few := ListOf(['twice', 'twice', 'once']);
    end;
    M := NewNameMap(Few);
    for I := 0 to Length(Few) - 1 do
      Check(MapFind(M, Few[I]) = IndexOf(Few, Few[I]), 'few ' + IntText(K) + ': find(' + Few[I] + ')');
    Check(MapFind(M, 'zeta!') = -1, 'few ' + IntText(K) + ': find(zeta!)');
  end;
end;

procedure TestNamesFollowDeclaredOrSortedOrder;
var
  Declared, Order, Many: TUTF8StringArray;
  Ns, Large: TNames;
  K, Start, Hint, I, Got: Int32;
  Two: UTF8String;
begin
  Declared := ListOf(['name', 'version', 'repository', 'alias']);
  Ns := NewNames(Declared);
  Check(NamesLen(Ns) = 4, 'four names');
  { Every order of the names, from every starting hint, finds each one. }
  for K := 0 to 3 do begin
    case K of
      0: Order := ListOf(['name', 'version', 'repository', 'alias']);
      1: Order := ListOf(['alias', 'name', 'repository', 'version']);
      2: Order := ListOf(['version', 'alias', 'name', 'repository']);
    else
      Order := ListOf(['repository', 'repository', 'name']);
    end;
    for Start := 0 to Length(Declared) do begin
      Hint := Start;
      for I := 0 to Length(Order) - 1 do begin
        Got := FindFrom(Ns, Order[I], Hint);
        Check(Got = IndexOf(Declared, Order[I]), Order[I] + ' in order ' + IntText(K) + ' from ' + IntText(Start));
      end;
    end;
  end;
  Hint := 0;
  Check(FindFrom(Ns, 'other', Hint) = -1, 'other is not a name');
  Hint := 2;
  Check(FindFrom(Ns, 'names', Hint) = -1, 'names is not a name');
  Check(NamesFindString(Ns, 'repository') = 2, 'NamesFindString finds repository');
  Check(NamesFindString(Ns, 'repositorx') = -1, 'NamesFindString does not find repositorx');
  { Sorted successors: after "name" (index 0) comes "repository" (2), after it "version" (1), then none. }
  Check((Length(Ns.SortedNext) = 5) and (Ns.SortedNext[0] = 3) and (Ns.SortedNext[1] = 2)
    and (Ns.SortedNext[2] = NoName) and (Ns.SortedNext[3] = 1) and (Ns.SortedNext[4] = 0), 'sortedNext');
  Many := nil;
  SetLength(Many, 40);
  for I := 0 to 39 do begin
    Two := IntText((I * 7) mod 40);
    if Length(Two) < 2 then
      Two := '0' + Two;
    Many[I] := 'property-number-' + Two;
  end;
  Large := NewNames(Many);
  Start := 0;
  while Start <= Length(Many) do begin
    for I := 0 to Length(Many) - 1 do begin
      Hint := Start;
      Check(FindFrom(Large, Many[I], Hint) = I, Many[I] + ' from ' + IntText(Start));
    end;
    Hint := Start;
    Check(FindFrom(Large, 'property-number-40', Hint) = -1, 'property-number-40 from ' + IntText(Start));
    Inc(Start, 13);
  end;
end;

procedure TestLinearNamesFindFromAnyHint;
var
  Ns: TNames;
  List: TUTF8StringArray;
  Start, I, Hint: Int32;
begin
  List := ListOf(['a', 'b', 'c']);
  Ns := NewNames(List);
  for Start := 0 to 2 do begin
    for I := 0 to 2 do begin
      Hint := Start;
      Check(FindFrom(Ns, List[I], Hint) = I, List[I] + ' from ' + IntText(Start));
    end;
    Hint := Start;
    Check(FindFrom(Ns, 'd', Hint) = -1, 'd from ' + IntText(Start));
  end;
end;

{ A set too large for the table's indexes is searched in sorted order. }
procedure TestLargeNameSets;
var
  List: TUTF8StringArray;
  Ns: TNames;
  I, Hint: Int32;
begin
  List := nil;
  SetLength(List, 40000);
  for I := 0 to Length(List) - 1 do
    List[I] := 'name-' + IntText((Int64(I) * 7919) mod 40000) + Repeated('-', I mod 20);
  { A name twice: the first is the one found. }
  List[39999] := List[5];
  Ns := NewNames(List);
  Check(Ns.M.IsLarge, 'forty thousand names are a large set');
  I := 0;
  while I < 39999 do begin
    Check(NamesFindString(Ns, List[I]) = I, 'large: ' + List[I]);
    Hint := I;
    Check(FindFrom(Ns, List[I], Hint) = I, 'large from its own index: ' + List[I]);
    Hint := 0;
    Check(FindFrom(Ns, List[I], Hint) = I, 'large from the start: ' + List[I]);
    Inc(I, 997);
  end;
  Check(NamesFindString(Ns, List[39999]) = 5, 'large: the first of two equal names');
  Check(NamesFindString(Ns, 'name-40000') = -1, 'large: a name that is not there');
  Check(NamesFindString(Ns, 'nam') = -1, 'large: a shorter name that is not there');
end;

{ --------------------------------------------------------------------------------------------------------------------
  Dialects }

procedure TestDialects;
var
  D: TDialect;
begin
  Check(DialectName(Draft4) = 'draft4', 'the name of draft 4');
  Check(DialectName(Draft201909) = 'draft2019-09', 'the name of 2019-09');
  Check(DialectName(Draft202012) = 'draft2020-12', 'the name of 2020-12');
  Check(DialectIsLegacy(Draft7) and not DialectIsLegacy(Draft201909), 'draft 7 is the last legacy dialect');
  Check(KnownDialect('http://json-schema.org/draft-06/schema', D) and (D = Draft6), 'draft 6 is known');
  Check(KnownDialect('https://json-schema.org/draft/2020-12/schema', D) and (D = Draft202012), '2020-12 is known');
  Check(not KnownDialect('https://json-schema.org/draft/2020-12/schema#', D), 'a fragment is not normalised');
  Check(VocabularyFlag('https://json-schema.org/draft/2019-09/vocab/format') = VocabFormatAnnotation,
    'the 2019-09 format vocabulary annotates');
  Check(VocabularyFlag('https://json-schema.org/draft/2020-12/vocab/format-assertion') = VocabFormatAssertion,
    'the format-assertion vocabulary');
  Check(VocabularyFlag('https://example.com/vocab') = VocabNone, 'an unknown vocabulary');
  Check(VocabAllAnnotating = 223, 'every vocabulary but format-assertion');
  Check(SubschemaKindOf('items', Draft7, False) = SubschemaSingleOrArray, 'items before 2020-12');
  Check(SubschemaKindOf('items', Draft202012, False) = SubschemaSingle, 'items in 2020-12');
  Check(SubschemaKindOf('prefixItems', Draft201909, False) = SubschemaNone, 'prefixItems before 2020-12');
  Check(SubschemaKindOf('additionalItems', Draft202012, False) = SubschemaNone, 'additionalItems in 2020-12');
  Check(SubschemaKindOf('if', Draft6, False) = SubschemaNone, 'if before draft 7');
  Check(SubschemaKindOf('properties', Draft4, True) = SubschemaNone, 'siblings of a legacy $ref');
  Check(SubschemaKindOf('definitions', Draft4, True) = SubschemaMap, 'definitions beside a legacy $ref');
  Check(SubschemaKindOf('$defs', Draft4, False) = SubschemaMap, '$defs in every dialect');
  Check(SubschemaKindOf('dependentSchemas', Draft7, False) = SubschemaNone, 'dependentSchemas before 2019-09');
  { The Formats unit takes a dialect by its ordinal. }
  Check((Ord(Draft4) = FormatDialectDraft4) and (Ord(Draft6) = FormatDialectDraft6)
    and (Ord(Draft7) = FormatDialectDraft7) and (Ord(Draft201909) = FormatDialectDraft201909)
    and (Ord(Draft202012) = FormatDialectDraft202012), 'the ordinals of the dialects are those of the Formats unit');
end;

{ --------------------------------------------------------------------------------------------------------------------
  Metaschemas (metaschemas_test.go) }

function GoModule: UTF8String;
begin
  Result := UTF8String(GetEnvironmentVariable('CORVUS_GO_MODULE'));
  if Result = '' then
    Result := '../../src-go/corvus-json-schema';
end;

{ Keeps the embedded metaschemas in step with those of the Go module. Run tools/gen-metaschemas.ps1 to write them
  again. }
procedure TestEmbeddedMetaschemasAreCurrent;
var
  Source: UTF8String;
  I: Int32;
  Want, Got: TBytes;
begin
  Source := GoModule + '/metaschemas';
  if not DirectoryExists(Source) then begin
    WriteLn('skipped: no Go module at ', GoModule, ' to compare the metaschemas with (set CORVUS_GO_MODULE)');
    Exit;
  end;
  for I := 0 to EmbeddedMetaschemaCount - 1 do begin
    Got := EmbeddedMetaschemaText(I);
    if not ReadFile(Source + '/' + EmbeddedMetaschemaName(I), Want) then
      Check(False, EmbeddedMetaschemaName(I) + ' has no source in ' + Source)
    else
      Check((Length(Got) = Length(Want)) and BytesEqual(Got, 0, Want, 0, Length(Got)),
        EmbeddedMetaschemaName(I) + ' is stale: run pwsh tools/gen-metaschemas.ps1');
  end;
end;

function EndsWith(const S, Suffix: UTF8String): Boolean;
begin
  Result := (Length(S) >= Length(Suffix)) and (Copy(S, Length(S) - Length(Suffix) + 1, Length(Suffix)) = Suffix);
end;

procedure TestEveryEmbeddedMetaschemaResolvesByItsURI;
var
  Uris: TUTF8StringArray;
  I, K, TypeNode: Int32;
  Name, Uri, Error: UTF8String;
  Text: TBytes;
  Doc: TDocument;
  E: TParseError;
  Compiled: TCompiledSchema;
begin
  Uris := ListOf(['http://json-schema.org/draft-04/schema', 'http://json-schema.org/draft-06/schema',
    'http://json-schema.org/draft-07/schema', 'https://json-schema.org/draft/2019-09/schema',
    'https://json-schema.org/draft/2020-12/schema']);
  for I := 0 to EmbeddedMetaschemaCount - 1 do begin
    Name := EmbeddedMetaschemaName(I);
    for K := 0 to 1 do begin
      if K = 0 then
        Uri := '2019-09'
      else
        Uri := '2020-12';
      if Copy(Name, 1, Length('draft' + Uri + '/meta/')) = 'draft' + Uri + '/meta/' then
        AddName(Uris, 'https://json-schema.org/draft/' + Uri + '/meta/'
          + Copy(Name, Length('draft' + Uri + '/meta/') + 1, Length(Name) - Length('draft' + Uri + '/meta/') - 5));
    end;
  end;
  Check(Length(Uris) = 21, IntText(Length(Uris)) + ' metaschemas, want 21');
  Check(EmbeddedMetaschemaCount = 21, IntText(EmbeddedMetaschemaCount) + ' embedded files, want 21');
  for I := 0 to Length(Uris) - 1 do begin
    Uri := Uris[I];
    if not Metaschema(Uri, Text) then begin
      Check(False, Uri + ' is not embedded');
      Continue;
    end;
    Check(ParseDocument(Text, Doc, E), Uri + ' parses');
    if Pos('hyper-schema', Uri) > 0 then
      { The hyper-schema vocabulary refers to the links schema, which is not embedded. }
      Continue;
    Error := '';
    Compiled := Default(TCompiledSchema);
    if not CompileFromURI(Uri, DefaultOptions, Compiled, Error) then begin
      Check(False, Uri + ': ' + Error);
      Continue;
    end;
    Check(Length(Compiled.Nodes) > 1, Uri + ' compiles to a graph');
    { The root metaschemas (and the validation vocabularies) know that type is a name or a list of names. The Go
      test validates a schema with a numeric type. Here the node of the type property is looked at. }
    if (Pos('/meta/', Uri) = 0) or EndsWith(Uri, '/meta/validation') then begin
      TypeNode := NoNode;
      for K := 0 to Length(Compiled.Nodes) - 1 do
        if EndsWith(Compiled.Nodes[K].Pointer, '/properties/type') then
          TypeNode := K;
      Check((TypeNode >= 0) and (Compiled.Nodes[TypeNode].HasAnyOf), Uri + ' constrains type');
    end;
  end;
  Check(not Metaschema('https://json-schema.org/draft/2020-12/meta/../schema', Text), 'a dotted path resolved');
  Check(not Metaschema('https://example.com/schema', Text), 'another host resolved');
  Check(not Metaschema('', Text), 'the empty URI resolved');
  Check(not Metaschema('https://json-schema.org/draft/2020-12/meta/nothing', Text), 'an unknown vocabulary resolved');
end;

{ --------------------------------------------------------------------------------------------------------------------
  Patterns (pattern_test.go) }

var
  TestPatterns: TUTF8StringArray;
  Alphabet: TUTF8StringArray;

procedure Add(const Source: UTF8String);
begin
  AddName(TestPatterns, Source);
end;

procedure AddLetter(const S: UTF8String);
begin
  AddName(Alphabet, S);
end;

procedure FillTestPatterns;
begin
  TestPatterns := nil;
  Add('');
  Add('.*');
  Add('^.*');
  Add('.*$');
  Add('[\s\S]*');
  Add('^[\s\S]*');
  Add('^[\s\S]*$');
  Add('^.*$');
  Add('^[@$_#]');
  Add('^[a-zA-Z0-9_\.\-\|@#]*$');
  Add('^[a-zA-Z0-9_\-]*$');
  Add('.+');
  Add('^\{\{[^\W\.\-][\w\.\-]*\}\}$');
  Add('^.{1,256}$');
  Add('^[A-Z0-9_\-\/]+$');
  Add('^[a-zA-Z0-9_\.\-]+[\|]?[a-zA-Z0-9_\.\-]+$');
  Add('^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$');
  Add('^x-');
  Add('^[1-5](?:[0-9]{2}|XX)$');
  Add('(base64key|awskms)://(.*)');
  Add('^[A-F0-9]{1,32}$');
  Add('^[a-z][a-z0-9]{0,29}$');
  Add('^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])$');
  Add('^[\w\*]{0,60}$');
  Add('^(?:@[0-9a-z-_.]+\/)?[a-z][0-9a-z-_.]*$');
  Add('^[a-z][a-z0-9_]+$');
  Add('^\d+[:-]\d+$');
  Add('^#[0-9a-fA-F]{6}$');
  Add('^[^:]+:[^:]+$');
  Add('^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$');
  Add('^.*\.(?:txt|trie)(?:\.gz)?$');
  Add('^([-\w_\s]+)(,[-\w_\s]+)*$');
  Add('^(!?[-\w_\s]+)|(\*)$');
  Add('^[0-9]+(ns|ms|us|µs|s|m|h)$');
  Add('^\/[^\*\?\&\%]*(\/\*)?$');
  Add('^[^- @#$%^&()!]+$');
  Add('^((\.(?!\.)\/)?\w+\/?)+$');
  Add('^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$');
  Add('\{.*\}');
  Add('^[a-z]{1,2}$');
  Add('^abc$');
  Add('^\/');
  Add('^es$');
  Add('^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA'
    + '-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.'
    + '[0-9a-zA-Z-]+)*))?$');
  Add('^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$');
  Add('^[a-z]*a$');
  Add('^a*a');
  Add('^a+b?a');
  Add('^a+b?c*a$');
  Add('^[a-z]+-?[a-z]+$');
  Add('\bfoo');
  Add('^\p{L}+$');
  Add('é');
  Add('😀');
  Add('[^\d]x');
  Add('^\S+$');
  Add('a{2}b{1,}c{0,3}');
  Add('(?<name>ab)+');
  Add('^[\b]');
  Add('\x41');
  Add('.');
  Add('^.');
  Add('^.+');
  Add('(.*)');
  Add('^(.*)');
  Add('^.+$');
  Add('^.{1,3}$');
  Add('^.{2}$');
  Add('^.{2,}$');
  Add('^(ab|cd)$');
  Add('^(?:es|ES|x-|a\.b)$');
  Add('^(a|b|c|d|e|f|g|h|i|j|z)$');
  Add('^ab|cd$');
  Add('^x-|es|ms$');
  Add('a|b');
  Add('^a\$|b');
  Add('\Bs');
  Add('a\b');
  Add('^\p{Lu}');
  Add('[\p{L}\d]+$');
  Add('^\P{L}+$');
  Add('^(?=[^a-c\n]+$)(?=(.*\w)).+$');
  Add('^\-a');
  Add('[{}[\]]');
  Add('(.+)');
  Add('^(.+)$');
  Add('^(.*)$');
  Add('^a(bc)?$');
  Add('^(a|b)c|d(e|f)$');
  Add('^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$');
  Add('^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$');
  Add('^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])$');
  Add('^([a-z]+|x)-$');
  Add('^a|[0-9]{2}$');
  Add('^(a|b)*$');
  Add('^(?=a)a|b$');
  Add('^/.*');
  Add('^a.*');
  Add('^a\\.*');
  Add('(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\}$)');
  Add('^[0-9]{1,}.[0-9]{1,}$');
  Add('^3\.1\.\d+(-.+)?$');
  Add('^([A-Za-z_][-A-Za-z0-9_.:]*)$');
  Add('^a.c$');
  Add('^[a-z].$');
  Add('x.y|^z');
  Add('^es|ms|x-$');
  Add('^(ab){2}$');
  Add('^(a|b){2}c$');
  Add('^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$');
  Add('^([a-z][a-z0-9]{0,3})(\.[a-z][a-z0-9]{0,3})*$');
  Add('^([a-z_$][a-z0-9_$]{0,3}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,3})$');
  Add('^([a-z]+)(,[a-z]+)+$');
  Add('^(a,)*b$');
  Add('^(ab,)+a$');
  Add('^a(,a)*$');
  Add('^[a-z]*(-[a-z]*)*$');
  Add('^(a-)*a-b$');
  Add('^(é,)*a$');
  Add('^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$');
  Add('^(?=!+[^a]+$)(?=(.*\w)).+$');
  Add('^[\&\@\_]+$');
  Add('a\&.b');
  Add('^[^\%]{1,3}$');
  Add('😀|\&');
  Add('^a[a-z]{2,5}z$');
  Add('^.*x$');
  Add('^-?[0-9-]{0,3}0$');
  Add('^\/[^\*\?]*\/\*$');
  Add('^.{2,}é$');
  Add('^[^,]*,[^,]$');
  Add('^(a.*|.+b)$');
  Add('^(.*\.)*[a-z]$');
end;

procedure FillAlphabet;
begin
  Alphabet := nil;
  AddLetter('a');
  AddLetter('b');
  AddLetter('z');
  AddLetter('A');
  AddLetter('X');
  AddLetter('Z');
  AddLetter('0');
  AddLetter('1');
  AddLetter('5');
  AddLetter('9');
  AddLetter('_');
  AddLetter('-');
  AddLetter('.');
  AddLetter(':');
  AddLetter('/');
  AddLetter('@');
  AddLetter('#');
  AddLetter('$');
  AddLetter('*');
  AddLetter('!');
  AddLetter('{');
  AddLetter('}');
  AddLetter('|');
  AddLetter(' ');
  AddLetter(#10);
  AddLetter(Rune($A0));
  AddLetter('é');
  AddLetter('µ');
  AddLetter('😀');
  AddLetter(Rune($2028));
  AddLetter('x-');
  AddLetter('es');
  AddLetter('ES');
  AddLetter('ms');
  AddLetter('txt');
  AddLetter('Au');
  AddLetter('to');
  AddLetter('No');
  AddLetter('ne');
  AddLetter('2015');
  AddLetter('Co');
  AddLetter('re');
  AddLetter('20');
  AddLetter('15');
  AddLetter('22');
  AddLetter('2');
  AddLetter(',');
  AddLetter('a,');
  AddLetter('ab,');
  AddLetter('a-');
  AddLetter('%');
  AddLetter('&');
  AddLetter('?');
end;

var
  Seed: UInt64;

{ A small deterministic generator for the tests that compare against a reference (xorshift of the Go tests). }
function NextRandom(N: Int32): Int32;
begin
  Seed := Seed xor (Seed shl 13);
  Seed := Seed xor (Seed shr 7);
  Seed := Seed xor (Seed shl 17);
  Result := Int32(Seed mod UInt64(N));
end;

{ Every matcher agrees with the engine on strings over an alphabet that exercises classes, anchors and non-ASCII. }
procedure TestMatchersAgreeWithTheEngine;
var
  P, I, N, Engines: Int32;
  Reference: TEcmaRegex;
  Compiled: TPattern;
  Error, Text: UTF8String;
  S: TBytes;
  Got, Want: Boolean;
begin
  FillTestPatterns;
  FillAlphabet;
  Check(Length(TestPatterns) = 133, IntText(Length(TestPatterns)) + ' test patterns, want 133');
  Check(Length(Alphabet) = 53, IntText(Length(Alphabet)) + ' letters, want 53');
  Seed := UInt64($2545F4914F6CDD1D);
  Engines := 0;
  for P := 0 to Length(TestPatterns) - 1 do begin
    if not EcmaRegexCompile(TestPatterns[P], Reference, Error) then begin
      Check(False, TestPatterns[P] + ' does not compile: ' + Error);
      Continue;
    end;
    if not CompilePattern(TestPatterns[P], Compiled) then begin
      Check(False, TestPatterns[P] + ' is not a valid pattern');
      Continue;
    end;
    Check(Compiled.Source = TestPatterns[P], 'the source of ' + TestPatterns[P]);
    if Compiled.Kind = MatchEngine then
      Inc(Engines);
    for I := 1 to 4000 do begin
      Text := '';
      N := NextRandom(9);
      while N > 0 do begin
        Text := Text + Alphabet[NextRandom(Length(Alphabet))];
        Dec(N);
      end;
      S := BytesOf(Text);
      Got := PatternMatch(Compiled, S, 0, Length(S), IsASCII(S, 0, Length(S)));
      Want := EcmaRegexIsMatch(Reference, S, 0, Length(S));
      Inc(Checks);
      if Got <> Want then begin
        Inc(Failures);
        WriteLn('FAILED: ', TestPatterns[P], ' on ', Text, ': ', Got, ', the engine says ', Want);
        Break;
      end;
      { The same text within a longer buffer: a match reads no byte outside its text. }
      if I mod 16 = 0 then begin
        S := BytesOf('a' + #10 + Text + 'z!');
        Got := PatternMatch(Compiled, S, 2, Length(S) - 4, IsASCII(S, 2, Length(S) - 2));
        Check(Got = Want, TestPatterns[P] + ' within a longer buffer on ' + Text);
        if Got <> Want then
          Break;
      end;
    end;
  end;
  Check(Engines <= Length(TestPatterns) div 2, IntText(Engines) + ' of ' + IntText(Length(TestPatterns))
    + ' patterns run on the engine');
  WriteLn(Engines, ' of ', Length(TestPatterns), ' test patterns run on the engine');
end;

var
  MorePatterns: TUTF8StringArray;

procedure AddMore(const Source: UTF8String);
begin
  AddName(MorePatterns, Source);
end;

{ More shapes than the Go test has, each chosen to reach a branch of a matcher (a sequence decided by the string's
  length, an alternative of fixed width at the end or anywhere, a list with a final item, counts of nothing), against
  the engine in the same way. }
procedure TestMoreShapesAgreeWithTheEngine;
var
  P, I, N: Int32;
  Reference: TEcmaRegex;
  Compiled: TPattern;
  Error, Text: UTF8String;
  S: TBytes;
  Got, Want: Boolean;
  Kinds: array[TMatcherKind] of Int32;
  K: TMatcherKind;
begin
  MorePatterns := nil;
  AddMore('^');
  AddMore('^$');
  AddMore('$');
  AddMore('^a{0}$');
  AddMore('^(a){0}b$');
  AddMore('^(ab){3}$');
  AddMore('^.{0,}$');
  AddMore('^.{3}?$');
  AddMore('^(.{3})$');
  AddMore('^(.)$');
  AddMore('^(?:a|b)$');
  AddMore('^(a|\n|\v|\f)$');
  AddMore('[a-z]{2}$');
  AddMore('[a-z]{2}');
  AddMore('^[\d-]$');
  AddMore('^a\\$');
  AddMore('^a\$');
  AddMore('(a|b)?c');
  AddMore('^((a|b)(a|z)){3}$');
  AddMore('^(a|b)(-|:)(e|s)(2|0)(1|5)(a|b)$');
  AddMore('^x(?:,x)*$');
  AddMore('^(x,)+x$');
  AddMore('^(?:x-)*[a-z]+$');
  AddMore('^([a-z],[0-9])*$');
  AddMore('^[a-z]+(\.[a-z]+)+$');
  AddMore('^(?=[^a]+$)(?=.*\w).+$');
  AddMore('^(?=!+[^!]+$)(?=(?:.*\w)).+$');
  AddMore('^é.*');
  AddMore('^a.*b.*');
  AddMore('^\..*');
  AddMore('^a|b.*');
  AddMore('^[a-z]{4294967295}$');
  AddMore('^[^\d\n]{2}[\W][\w]?$');
  AddMore('^a?a?a$');
  AddMore('^[ab]*[bc]$');
  AddMore('^[ab]+[bc]+$');
  AddMore('x-$');
  AddMore('^-$|^x-|es$');
  AddMore('[0-9]{2}|^a.$');
  AddMore('^(es|ms){2}$');
  AddMore('^[a-z]*(-[a-z]+)*$');
  AddMore('^(a,)+b$');
  AddMore('^(,a)*$');
  AddMore('^\t|\n$');
  AddMore('^[\t\n ]+$');
  AddMore('^\D\W\d\w$');
  for K := Low(TMatcherKind) to High(TMatcherKind) do
    Kinds[K] := 0;
  Seed := UInt64($9E3779B97F4A7C15);
  for P := 0 to Length(MorePatterns) - 1 do begin
    if not EcmaRegexCompile(MorePatterns[P], Reference, Error) then begin
      Check(False, MorePatterns[P] + ' does not compile: ' + Error);
      Continue;
    end;
    if not CompilePattern(MorePatterns[P], Compiled) then begin
      Check(False, MorePatterns[P] + ' is not a valid pattern');
      Continue;
    end;
    Inc(Kinds[Compiled.Kind]);
    for I := 1 to 4000 do begin
      Text := '';
      N := NextRandom(7);
      while N > 0 do begin
        Text := Text + Alphabet[NextRandom(Length(Alphabet))];
        Dec(N);
      end;
      S := BytesOf(Text);
      Got := PatternMatch(Compiled, S, 0, Length(S), IsASCII(S, 0, Length(S)));
      Want := EcmaRegexIsMatch(Reference, S, 0, Length(S));
      Inc(Checks);
      if Got <> Want then begin
        Inc(Failures);
        WriteLn('FAILED: ', MorePatterns[P], ' on ', Text, ': ', Got, ', the engine says ', Want);
        Break;
      end;
    end;
  end;
  { Every fast matcher that has shapes is among them (the two that have none are a few patterns by their text). }
  for K := MatchLiteral to MatchExcludedClassWithWord do
    Check((Kinds[K] > 0) or (K = MatchHasContent), 'no pattern of these takes matcher ' + IntText(Ord(K)));
end;

procedure ExpectKind(Kind: TMatcherKind; const Source: UTF8String);
var
  P: TPattern;
begin
  if not CompilePattern(Source, P) then
    Check(False, Source + ' is not a valid pattern')
  else
    Check(P.Kind = Kind, Source + ' takes matcher ' + IntText(Ord(P.Kind)) + ', want ' + IntText(Ord(Kind)));
end;

procedure TestSimplePatternsTakeTheFastMatchers;
begin
  ExpectKind(MatchEverything, '');
  ExpectKind(MatchEverything, '.*');
  ExpectKind(MatchEverything, '^.*');
  ExpectKind(MatchEverything, '^[\s\S]*$');
  ExpectKind(MatchEverything, '^(.*)');
  ExpectKind(MatchSequence, '^[@$_#]');
  ExpectKind(MatchSequence, '^[a-zA-Z0-9_\-]*$');
  ExpectKind(MatchSequence, '^#[0-9a-fA-F]{6}$');
  ExpectKind(MatchSequence, '^[a-z][a-z0-9_]+$');
  ExpectKind(MatchSequence, '^\d{4}-\d{2}-\d{2}$');
  ExpectKind(MatchSequence, '^[a-z]*a$');
  ExpectKind(MatchSequence, '^\/[^\*\?]*\/\*$');
  ExpectKind(MatchLiteral, '^x-');
  ExpectKind(MatchLiteral, '^\/');
  ExpectKind(MatchLiteral, '^abc$');
  ExpectKind(MatchLiteral, '^\-a');
  ExpectKind(MatchHasContent, '.+');
  ExpectKind(MatchHasContent, '^.+');
  ExpectKind(MatchHasContent, '(.+)');
  ExpectKind(MatchLine, '^.{1,256}$');
  ExpectKind(MatchLine, '^.+$');
  ExpectKind(MatchLine, '^.*$');
  ExpectKind(MatchLine, '^(.*)$');
  ExpectKind(MatchLine, '^(.+)$');
  ExpectKind(MatchLiterals, '^(ab|cd)$');
  ExpectKind(MatchLiterals, '^(?:es|ES|x-)$');
  ExpectKind(MatchAlternatives, '^ab|cd$');
  ExpectKind(MatchAlternatives, 'a|b');
  ExpectKind(MatchAlternatives, '^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$');
  ExpectKind(MatchAlternatives, '^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$');
  ExpectKind(MatchAlternatives, '^[1-5](?:[0-9]{2}|XX)$');
  ExpectKind(MatchAlternatives, '(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\}$)');
  ExpectKind(MatchAlternatives, '^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m'
    + '|M])$');
  ExpectKind(MatchAlternatives, '^([A-Za-z_][-A-Za-z0-9_.:]*)$');
  ExpectKind(MatchAlternatives, '^\/[^\*\?\&\%]*(\/\*)?$');
  ExpectKind(MatchAlternatives, '^.*\.(?:txt|trie)(?:\.gz)?$');
  ExpectKind(MatchSeparatedList, '^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$');
  ExpectKind(MatchSeparatedList, '^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$');
  ExpectKind(MatchSeparatedList, '^([a-z_$][a-z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$');
  ExpectKind(MatchExcludedClassWithWord, '^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$');
  ExpectKind(MatchEngine, '(base64key|awskms)://(.*)');
  ExpectKind(MatchEngine, '\bfoo');
  ExpectKind(MatchEngine, '^\p{L}+$');
  ExpectKind(MatchEngine, '^((\.(?!\.)\/)?\w+\/?)+$');
  ExpectKind(MatchEngine, '^\1(a)');
  ExpectKind(MatchEngine, 'a\&.b');
  ExpectKind(MatchEngine, '^[a-z]*a[a-z]*$');
end;

procedure TestInvalidPatternsAreRejected;
var
  P: TPattern;
  Sources: TUTF8StringArray;
  I: Int32;
begin
  Sources := ListOf(['a(', '[a', 'a{2,1}', '(?<n>a)(?<n>b)', '*a', '^(?=a']);
  for I := 0 to Length(Sources) - 1 do
    Check(not CompilePattern(Sources[I], P), Sources[I] + ' compiled');
  Check(ValidRegex('^a+$') and not ValidRegex('a\&.b') and not ValidRegex('a('), 'ValidRegex is the u flag grammar');
end;

{ A class with a member outside ASCII, in the shape "^(?=[^SET]+$)(?=(.*\w)).+$". The shape's set holds ASCII
  characters only. Reading such a member as a bit of the set raised an error when the schema was compiled (v0.1.0
  of the Go module). The pattern has no shape and is matched by the engine. }
procedure TestExcludedClassWithAMemberOutsideASCII;
var
  Patterns, Texts: TUTF8StringArray;
  C, T, At, Size, Cp: Int32;
  P: TPattern;
  S: TBytes;
  HasWord, HasExcluded, Excluded: Boolean;
  Compiled: TCompiledSchema;
  Schema, Error: UTF8String;
begin
  Patterns := ListOf(['^(?=[^é]+$)(?=(.*\w)).+$', '^(?=[^xé]+$)(?=(?:.*\w)).+$', '^(?=[^中/]+$)(?=.*\w).+$',
    '^(?=[^😀]+$)(?=(.*\w)).+$',
    { A range that starts in ASCII and ends outside it. }
    '^(?=[^m-é]+$)(?=(.*\w)).+$']);
  Texts := ListOf(['C1', ')a', 'abc', 'd-8p', 'p.q', 'x1', 'a/b', '---', 'é1', 'aé', '中a', 'a😀', 'zè', '0', 'ABC',
    'a{', 'al']);
  for C := 0 to Length(Patterns) - 1 do begin
    if not CompilePattern(Patterns[C], P) then begin
      Check(False, Patterns[C] + ' is not a valid pattern');
      Continue;
    end;
    Check(P.Kind = MatchEngine, Patterns[C] + ' is matched by the engine');
    { As the pattern of a schema. }
    Schema := '{"pattern": "' + Patterns[C] + '"}';
    Schema := StringReplace(Schema, '\', '\\', [rfReplaceAll]);
    Error := '';
    Compiled := Default(TCompiledSchema);
    Check(CompileDocument(Parse(Schema), DefaultOptions, Compiled, Error) and (Length(Compiled.Patterns) = 1)
      and (Compiled.Patterns[0].Kind = MatchEngine) and (Compiled.Nodes[Compiled.Root].Pattern = 0),
      Patterns[C] + ' compiles as the pattern of a schema: ' + Error);
    for T := 0 to Length(Texts) - 1 do begin
      S := BytesOf(Texts[T]);
      HasWord := False;
      HasExcluded := False;
      At := 0;
      while At < Length(S) do begin
        Cp := DecodeRune(S, At, Length(S), Size);
        Inc(At, Size);
        HasWord := HasWord or (Cp = Ord('_')) or ((Cp >= Ord('0')) and (Cp <= Ord('9')))
          or ((Cp >= Ord('a')) and (Cp <= Ord('z'))) or ((Cp >= Ord('A')) and (Cp <= Ord('Z')));
        case C of
          0: Excluded := Cp = $E9;
          1: Excluded := (Cp = Ord('x')) or (Cp = $E9);
          2: Excluded := (Cp = $4E2D) or (Cp = Ord('/'));
          3: Excluded := Cp = $1F600;
        else
          Excluded := (Cp >= Ord('m')) and (Cp <= $E9);
        end;
        HasExcluded := HasExcluded or Excluded;
      end;
      Check(PatternMatchString(P, Texts[T]) = (HasWord and not HasExcluded), Patterns[C] + ' on ' + Texts[T]);
    end;
  end;
end;

{ The matchers one by one, on texts chosen for each. }
procedure TestMatchersByHand;

  procedure M(const Source, Text: UTF8String; Expected: Boolean);
  var
    P: TPattern;
  begin
    if not CompilePattern(Source, P) then
      Check(False, Source + ' is not a valid pattern')
    else
      Check(PatternMatchString(P, Text) = Expected, Source + ' on ' + Text);
  end;

begin
  M('', 'anything', True);
  M('^.+', #10 + 'a', False);
  M('^.+', 'a' + #10, True);
  M('.+', #10 + #13 + Rune($2028), False);
  M('.+', #10 + 'é', True);
  M('^.{1,3}$', 'aé😀', True);
  M('^.{1,3}$', 'aé😀b', False);
  M('^.{1,3}$', '', False);
  M('^.{2}$', 'a' + #10, False);
  M('^x-', 'x-a', True);
  M('^x-', 'ax-', False);
  M('^abc$', 'abc', True);
  M('^abc$', 'abcd', False);
  M('^(a|b|c|d|e|f|g|h|i|j|z)$', 'z', True);
  M('^(a|b|c|d|e|f|g|h|i|j|z)$', 'y', False);
  M('^(a|b|c|d|e|f|g|h|i|j|z)$', 'zz', False);
  M('^(?:es|ES|x-|a\.b)$', 'a.b', True);
  M('^(?:es|ES|x-|a\.b)$', 'aXb', False);
  M('^[a-z][a-z0-9_]{0,29}$', 'a_29', True);
  M('^[a-z][a-z0-9_]{0,29}$', 'a' + Repeated('b', 30), False);
  M('^[a-z]*a$', 'bcda', True);
  M('^[a-z]*a$', 'bcdb', False);
  M('^.{2,}é$', 'abé', True);
  M('^.{2,}é$', 'aé', False);
  M('^ab|cd$', 'xcd', True);
  M('^ab|cd$', 'xab', False);
  M('^a|[0-9]{2}$', 'x12', True);
  M('^a|[0-9]{2}$', 'x1é', False);
  M('x.y|^z', 'aax😀yb', True);
  M('x.y|^z', 'aaxy', False);
  M('^([a-z]+)(,[a-z]+)+$', 'a,b,c', True);
  M('^([a-z]+)(,[a-z]+)+$', 'a', False);
  M('^(a,)*b$', 'a,a,b', True);
  M('^(a,)*b$', 'a,a,', False);
  M('^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$', '!!ab', True);
  M('^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$', 'ab', False);
  M('^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$', '!!', False);
  M('^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$', 'a b', True);
  M('^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$', '- -', False);
  M('^\p{L}+$', 'aé中', True);
  M('^\p{L}+$', 'a1', False);
end;

{ --------------------------------------------------------------------------------------------------------------------
  Unicode (unicode_test.go) }

function Bytes8(const S: UTF8String): TBytes;
begin
  Result := BytesOf(S);
end;

{ Format answers that differ between Unicode 15 and the Unicode 17 of the Ucd unit. }
procedure TestUnicodeFormatsUseUnicode17;

  procedure Host(const Name: UTF8String; Want: Boolean);
  var
    B: TBytes;
  begin
    B := Bytes8(Name);
    Check(IsIDNHostname(B, 0, Length(B)) = Want, 'IsIDNHostname(' + Name + ')');
  end;

  procedure Address(const Name: UTF8String; Want: Boolean);
  var
    B: TBytes;
  begin
    B := Bytes8(Name);
    Check(IsIDNEmail(B, 0, Length(B)) = Want, 'IsIDNEmail(' + Name + ')');
  end;

var
  Lowered: UTF8String;
begin
  { U+10D4A and U+10D4B are Garay letters, assigned in Unicode 16. U+1C89 is an uppercase letter of Unicode 16,
    and IDNA2008 disallows uppercase letters. U+2FFFF is a noncharacter in every version. }
  Host(Rune($10D4A) + Rune($10D4B) + '.example', True);
  Host(Rune($1C8A) + '.example', True);
  Host(Rune($1C89) + '.example', False);
  Host(Rune($2FFFF) + '.example', False);
  Address(Rune($10D4A) + Rune($10D4B) + '@example.com', True);
  Address(Rune($16EA0) + Rune($16EBB) + '@example.com', True);
  Address(Rune($2FFFF) + '@example.com', False);
  Address('δοκιμή@example.com', True);
  Address('a b@example.com', False);
  { Only the letters A to Z are lowered when a URI is normalized. (The text wanted is a variable: a comparison
    with a literal that has characters outside ASCII would be made in UTF-16.) }
  Lowered := 'http://Éxample.com/İ';
  Check(AsciiLower('HTTP://ÉXAMPLE.Com/İ') = Lowered, 'AsciiLower gives ' + AsciiLower('HTTP://ÉXAMPLE.Com/İ'));
end;

{ Fails if a unit of the package takes Unicode data, or case mapping, from the run-time library of the compiler.
  That data differs between compilers and their versions, so a result that rests on it changes with the toolchain.
  The package reads the Ucd unit instead. The test reads the source of every unit, leaves out comments and string
  literals, and refuses the identifiers of the run-time library that map or compare case or classify characters,
  and the units that hold such data. (TestNoToolchainUnicodeData of the Go module, for Pascal.) }
procedure TestNoToolchainUnicodeData;
const
  Banned: array[0..27] of UTF8String = (
    'character', 'cwstring', 'unicodedata', 'lazutf8', 'strutils', 'widestrutils', 'ansistrings',
    'ansiuppercase', 'ansilowercase', 'wideuppercase', 'widelowercase', 'unicodeuppercase', 'unicodelowercase',
    'uppercase', 'lowercase', 'ansicomparetext', 'ansisametext', 'widecomparetext', 'widesametext',
    'unicodecomparetext', 'unicodesametext', 'comparetext', 'sametext', 'utf8uppercase', 'utf8lowercase',
    'tcharacter', 'toupper', 'tolower');
var
  Search: TSearchRec;
  Files, I, B, At, Start: Int32;
  Text: TBytes;
  Name, Word: UTF8String;
  C: Byte;
begin
  Files := 0;
  if FindFirst('src/*', faAnyFile, Search) <> 0 then begin
    WriteLn('skipped: no src directory here to read (run the test from the package directory)');
    Exit;
  end;
  repeat
    Name := UTF8String(Search.Name);
    if not (EndsWith(Name, '.pas') or EndsWith(Name, '.inc')) then
      Continue;
    if not ReadFile('src/' + Name, Text) then begin
      Check(False, 'src/' + Name + ' could not be read');
      Continue;
    end;
    Inc(Files);
    At := 0;
    while At < Length(Text) do begin
      C := Text[At];
      if C = Ord('{') then begin
        while (At < Length(Text)) and (Text[At] <> Ord('}')) do
          Inc(At);
        Inc(At);
      end else if (C = Ord('(')) and (At + 1 < Length(Text)) and (Text[At + 1] = Ord('*')) then begin
        Inc(At, 2);
        while (At + 1 < Length(Text)) and not ((Text[At] = Ord('*')) and (Text[At + 1] = Ord(')'))) do
          Inc(At);
        Inc(At, 2);
      end else if (C = Ord('/')) and (At + 1 < Length(Text)) and (Text[At + 1] = Ord('/')) then begin
        while (At < Length(Text)) and (Text[At] <> 10) do
          Inc(At);
      end else if C = Ord('''') then begin
        Inc(At);
        while (At < Length(Text)) and (Text[At] <> Ord('''')) and (Text[At] <> 10) do
          Inc(At);
        Inc(At);
      end else if IsASCIILetter(C) or (C = Ord('_')) then begin
        Start := At;
        Word := '';
        while (At < Length(Text))
          and (IsASCIILetter(Text[At]) or IsASCIIDigit(Text[At]) or (Text[At] = Ord('_'))) do begin
          C := Text[At];
          if (C >= Ord('A')) and (C <= Ord('Z')) then
            C := C + 32;
          SetLength(Word, Length(Word) + 1);
          Word[Length(Word)] := AnsiChar(C);
          Inc(At);
        end;
        { A name after a dot is a part of a unit's name or a field, and is the caller's own. }
        if (Start = 0) or (Text[Start - 1] <> Ord('.')) then
          for B := 0 to High(Banned) do
            if Word = Banned[B] then
              Check(False, 'src/' + Name + ' uses ' + Word + ', which reads the data of the run-time library');
      end else
        Inc(At);
    end;
  until FindNext(Search) <> 0;
  FindClose(Search);
  Check(Files >= 20, 'only ' + IntText(Files) + ' files were read');
  for I := 0 to 0 do
    Inc(Checks);
end;

{ --------------------------------------------------------------------------------------------------------------------
  The results collector (results.go, driven as the evaluator drives it) }

function Row(const R: TSchemaResult): UTF8String;
begin
  if R.IsMatch then
    Result := 'match'
  else
    Result := 'fail';
  Result := Result + '|' + R.SchemaEvaluationLocation + '|' + R.EvaluationLocation + '|'
    + R.DocumentEvaluationLocation + '|' + R.Message;
end;

function Rows(const C: TResultsCollector): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 0 to CollectorResultCount(C) - 1 do begin
    if I > 0 then
      Result := Result + #10;
    Result := Result + Row(C.Committed[I]);
  end;
end;

{ Drive writes what an evaluation of an object with a name too short, against a schema with a title, would: a root
  context with a passing type and a title annotation, and a failing child context for the name. }
procedure Drive(var C: TResultsCollector; Valid: Boolean);
begin
  BeginChildContext(C, False, '', '', False, '');
  EvaluatedKeyword(C, True, 'the type is object', 'type');
  BeginChildContext(C, True, 'properties/na~1me', '/properties/na~1me', True, 'na~1me');
  EvaluatedKeyword(C, True, 'the type is string', 'type');
  EvaluatedKeyword(C, Valid, 'minLength 1', 'minLength');
  CommitChildContext(C, Valid, Valid, 'the name');
  IgnoredKeyword(C, '"Person"', 'title');
  EvaluatedKeywordForProperty(C, Valid, 'required a/b', 'a/b', 'required');
  CommitChildContext(C, False, Valid, 'the root');
end;

procedure TestResultsCollector;
var
  C: TResultsCollector;
  A: TAnnotationArray;
  G: TCollectedAnnotationArray;
  R: TSchemaResultArray;
begin
  Check((ResultsLevelName(Basic) = 'Basic') and (ResultsLevelName(Detailed) = 'Detailed')
    and (ResultsLevelName(Verbose) = 'Verbose'), 'the names of the levels');

  { Verbose records every row, with text. A context's summary comes first, then its own rows newest first, after
    its committed descendants. }
  C := NewResultsCollector(Verbose);
  Check(C.Level = Verbose, 'the level of a collector');
  Check(CollectorVerbose(C) and CollectorWithText(C, True) and CollectorRecords(C, True), 'verbose records all');
  Drive(C, False);
  Check(Rows(C) =
    'fail|/properties/na~1me|/properties/na~1me|/na~1me|the name' + #10
    + 'fail|/properties/na~1me/minLength|/properties/na~1me/minLength|/na~1me|minLength 1' + #10
    + 'match|/properties/na~1me/type|/properties/na~1me/type|/na~1me|the type is string' + #10
    + 'fail||||the root' + #10
    + 'fail|/required|/required|/a~1b|required a/b' + #10
    + 'match||/title||"Person"' + #10
    + 'match|/type|/type||the type is object', 'verbose rows of a failure:' + #10 + Rows(C));
  Check((C.FramesLen = 0) and (C.PendingLen = 0) and (C.EvalPathLen = 0) and (C.DocPathLen = 0)
    and (C.SchemaPath = ''), 'the collector is back at the root');
  R := CollectorResults(C);
  Check((Length(R) = 7) and (R[3].Message = 'the root') and not R[3].IsMatch, 'CollectorResults copies the rows');

  { Annotations are the passing rows whose message is JSON and whose keyword is not in the schema path. }
  A := CollectorAnnotations(C);
  Check((Length(A) = 1) and (A[0].Keyword = 'title') and (A[0].Value = '"Person"') and (A[0].InstanceLocation = '')
    and (A[0].SchemaLocation = ''), 'the title annotation');

  { A collector accumulates across evaluations until it is reset. }
  Drive(C, True);
  Check(CollectorResultCount(C) = 14, 'a collector accumulates: ' + IntText(CollectorResultCount(C)));
  Check(Row(C.Committed[7]) = 'match|/properties/na~1me|/properties/na~1me|/na~1me|the name',
    'the first row of the second evaluation: ' + Row(C.Committed[7]));
  CollectorReset(C);
  Check((CollectorResultCount(C) = 0) and (Length(CollectorResults(C)) = 0), 'a reset collector is empty');

  { Detailed keeps failures only, with messages. }
  C := NewResultsCollector(Detailed);
  Check(not CollectorVerbose(C) and CollectorWithText(C, False) and not CollectorWithText(C, True)
    and CollectorRecords(C, False) and not CollectorRecords(C, True), 'detailed records failures with text');
  Drive(C, False);
  Check(Rows(C) =
    'fail|/properties/na~1me|/properties/na~1me|/na~1me|the name' + #10
    + 'fail|/properties/na~1me/minLength|/properties/na~1me/minLength|/na~1me|minLength 1' + #10
    + 'fail||||the root' + #10
    + 'fail|/required|/required|/a~1b|required a/b', 'detailed rows of a failure:' + #10 + Rows(C));
  { A valid instance at the detailed level yields only the passing root row, without text: the child that the
    parent does not need is discarded. }
  CollectorReset(C);
  Drive(C, True);
  Check(Rows(C) = 'match||||', 'detailed rows of a valid instance:' + #10 + Rows(C));
  Check(Length(CollectorAnnotations(C)) = 0, 'no annotations below verbose');

  { Basic keeps failures only, without messages. }
  C := NewResultsCollector(Basic);
  Check(not CollectorWithText(C, False) and CollectorRecords(C, False) and not CollectorRecords(C, True),
    'basic records failures without text');
  Drive(C, False);
  Check(Rows(C) =
    'fail|/properties/na~1me|/properties/na~1me|/na~1me|' + #10
    + 'fail|/properties/na~1me/minLength|/properties/na~1me/minLength|/na~1me|' + #10
    + 'fail||||' + #10
    + 'fail|/required|/required|/a~1b|', 'basic rows of a failure:' + #10 + Rows(C));

  { A popped context leaves nothing, whatever it and its descendants wrote and committed. }
  C := NewResultsCollector(Verbose);
  BeginChildContext(C, False, '', '', False, '');
  BeginChildContext(C, True, 'anyOf/0', '/anyOf/0', False, '');
  EvaluatedKeyword(C, False, 'not a string', 'type');
  BeginChildContext(C, True, 'items', '/anyOf/0/items', True, '0');
  EvaluatedBooleanSchema(C, False);
  CommitChildContext(C, False, False, 'the item');
  Check(CollectorResultCount(C) = 2, 'the item context is committed');
  PopChildContext(C);
  Check((CollectorResultCount(C) = 0) and (C.PendingLen = 0), 'a popped context leaves nothing');
  BeginChildContext(C, True, 'anyOf/1', '/anyOf/1', False, '');
  EvaluatedBooleanSchema(C, True);
  CommitChildContext(C, True, True, '');
  CommitChildContext(C, False, True, '');
  Check(Rows(C) = 'match|/anyOf/1|/anyOf/1||' + #10 + 'match|/anyOf/1|/anyOf/1||' + #10 + 'match||||',
    'a boolean schema and its context:' + #10 + Rows(C));

  { Annotations by location, keyword and schema location fragment. Of two with all three the same, the last is
    kept. }
  C := NewResultsCollector(Verbose);
  BeginChildContext(C, False, '', '', False, '');
  IgnoredKeyword(C, '"Root"', 'title');
  IgnoredKeyword(C, 'some text', 'x-note');
  IgnoredKeyword(C, '12', 'x-number');
  IgnoredKeyword(C, '-1', 'x-number');
  BeginChildContext(C, True, 'properties/a b', '/properties/a b', True, 'a b');
  IgnoredKeyword(C, '"A"', 'title');
  IgnoredKeyword(C, '{"a": 1}', 'default');
  CommitChildContext(C, False, True, '');
  BeginChildContext(C, True, '$ref', '/$defs/é', True, 'c');
  IgnoredKeyword(C, 'true', 'readOnly');
  CommitChildContext(C, False, True, '');
  CommitChildContext(C, False, True, '');
  A := CollectorAnnotations(C);
  Check(Length(A) = 6, IntText(Length(A)) + ' annotations, want 6');
  G := CollectAnnotations(C);
  Check(Length(G) = 5, IntText(Length(G)) + ' collected annotations, want 5');
  if Length(G) = 5 then begin
    Check((G[0].InstanceLocation = '/a b') and (G[0].Keyword = 'default')
      and (G[0].SchemaLocationFragment = '#/properties/a%20b') and (G[0].Value = '{"a": 1}'), 'the default of a b');
    Check((G[1].InstanceLocation = '/a b') and (G[1].Keyword = 'title') and (G[1].Value = '"A"'), 'the title of a b');
    Check((G[2].InstanceLocation = '/c') and (G[2].Keyword = 'readOnly')
      and (G[2].SchemaLocationFragment = '#/$defs/%C3%A9') and (G[2].Value = 'true'), 'readOnly of c');
    Check((G[3].InstanceLocation = '') and (G[3].Keyword = 'x-number') and (G[3].SchemaLocationFragment = '#')
      and (G[3].Value = '12'), 'the last x-number: ' + G[3].Value);
    Check((G[4].Keyword = 'title') and (G[4].Value = '"Root"'), 'the title of the root');
  end;

  Check(SchemaLocationFragment('') = '#', 'the fragment of the root');
  Check(SchemaLocationFragment('/properties/name') = '#/properties/name', 'the fragment of a pointer');
  Check(SchemaLocationFragment('/$defs/a b/~0~1/%/"é"/[x]') = '#/$defs/a%20b/~0~1/%25/%22%C3%A9%22/%5Bx%5D',
    'the fragment of a pointer with characters to encode: ' + SchemaLocationFragment('/$defs/a b/~0~1/%/"é"/[x]'));
  Check(SchemaLocationFragment('-._~!$&''()*+,;=:@/?') = '#-._~!$&''()*+,;=:@/?', 'the characters a fragment keeps');
end;

{ --------------------------------------------------------------------------------------------------------------------
  Compiling (api_test.go, and the graph the compiler builds) }

var
  ItemDoc, TreeDoc: TDocument;

function TestResolver(Context: Pointer; const Uri: UTF8String; out Doc: TDocument): Boolean;
begin
  Result := True;
  if Uri = 'https://example.com/schemas/item.json' then
    Doc := ItemDoc
  else if Uri = 'https://example.com/tree' then
    Doc := TreeDoc
  else begin
    Doc := Default(TDocument);
    Result := False;
  end;
  if Context <> @ItemDoc then
    Result := False;
end;

function MustCompile(const Schema: UTF8String; const O: TCompileOptions; out S: TCompiledSchema): Boolean;
var
  Error: UTF8String;
begin
  Result := CompileDocument(Parse(Schema), O, S, Error);
  Check(Result, 'compile ' + Schema + ': ' + Error);
end;

function CompileFails(const Schema: UTF8String; const O: TCompileOptions): UTF8String;
var
  S: TCompiledSchema;
begin
  Result := '';
  if CompileDocument(Parse(Schema), O, S, Result) then begin
    Check(False, Schema + ' compiled');
    Result := '';
  end;
end;

function PropertyNode(const S: TCompiledSchema; Node: TNodeID; const Name: UTF8String): TNodeID;
var
  I: Int32;
begin
  Result := NoNode;
  for I := 0 to Length(S.Nodes[Node].Properties) - 1 do
    if S.Nodes[Node].Properties[I].Name = Name then
      Result := S.Nodes[Node].Properties[I].Node;
end;

function BranchList(const B: TUInt32Array): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 0 to Length(B) - 1 do
    Result := Result + IntText(B[I]) + ' ';
end;

procedure TestEntryPointEvaluatesFromASubschema;
var
  O: TCompileOptions;
  S: TCompiledSchema;
const
  Schema = '{ "$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" }';
begin
  O := DefaultOptions;
  WithEntryPoint(O, '#/$defs/positive');
  if MustCompile(Schema, O, S) then begin
    Check(S.Nodes[S.Root].Pointer = '/$defs/positive', 'the entry node is the subschema');
    Check(S.Nodes[S.Root].HasType and (S.Nodes[S.Root].TypeMask = TypeNumber), 'the entry node is a number');
    Check(S.Nodes[S.Root].ExclusiveMinimum.IsSet and (S.Nodes[S.Root].ExclusiveMinimum.Doc = 0)
      and (DocumentToJson(S.Documents[0]) <> '') and (DocKind(S.Documents[0], S.Nodes[S.Root].ExclusiveMinimum.N)
      = KindNumber), 'the entry node has its bound');
    Check(Length(S.Nodes) = 1, 'only the entry node is compiled');
  end;
  O := DefaultOptions;
  WithEntryPoint(O, '#/$defs/missing');
  Check(CompileFails(Schema, O) = 'Unable to resolve the entry point ''#/$defs/missing''.', 'a missing entry point');
end;

procedure TestBaseURIResolvesRelativeReferences;
var
  O: TCompileOptions;
  S: TCompiledSchema;
  Error: UTF8String;
  Items, Target: TNodeID;
begin
  ItemDoc := Parse('{ "type": "integer" }');
  Error := '';
  O := DefaultOptions;
  WithBaseURI(O, 'https://example.com/schemas/root.json');
  WithDocumentResolver(O, TestResolver, @ItemDoc);
  if MustCompile('{ "items": { "$ref": "item.json" } }', O, S) then begin
    Items := S.Nodes[S.Root].Items;
    Check(Items >= 0, 'the root has items');
    if Items >= 0 then begin
      Target := S.Nodes[Items].Ref;
      Check((Target >= 0) and S.Nodes[Target].HasType and (S.Nodes[Target].TypeMask = TypeInteger)
        and (S.Nodes[Target].ResourceID <> S.Nodes[S.Root].ResourceID), 'items refers to the resolved document');
      Check(Length(S.Documents) = 2, 'two documents');
    end;
  end;
  Check(CompileFromURI('https://example.com/schemas/item.json', O, S, Error) and S.Nodes[S.Root].HasType
    and (S.Nodes[S.Root].TypeMask = TypeInteger), 'a schema compiled from its URI: ' + Error);
  Check(not CompileFromURI('https://example.com/schemas/other.json', O, S, Error), 'an unknown URI compiled');
  Check(Error = 'Unable to resolve the schema document ''https://example.com/schemas/other.json''.',
    'the error of an unknown URI: ' + Error);
  { Without a resolver the same reference does not resolve. }
  Check(CompileFails('{ "items": { "$ref": "item.json" } }', DefaultOptions)
    = 'Unable to resolve reference ''item.json'' from ''https://corvus-oss.org/runtime-evaluator/root.json''.',
    'a relative reference with no resolver');
end;

procedure TestDefaultDialectAppliesToSchemasWithoutSchema;
var
  O: TCompileOptions;
  S: TCompiledSchema;
const
  Schema = '{ "items": [{ "type": "string" }], "additionalItems": false }';
begin
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile(Schema, O, S) then begin
    Check(S.Nodes[S.Root].Dialect = Draft7, 'the default dialect is the root''s');
    Check(S.Nodes[S.Root].HasPrefixItems and (Length(S.Nodes[S.Root].PrefixItems) = 1)
      and (S.Nodes[S.Root].PrefixKeyword = 'items') and (S.Nodes[S.Root].Items >= 0)
      and (S.Nodes[S.Root].ItemsKeyword = 'additionalItems'), 'the tuple form of draft 7');
    Check(S.Nodes[S.Nodes[S.Root].Items].AlwaysFalse, 'additionalItems is false');
  end;
  { In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies. }
  if MustCompile(Schema, DefaultOptions, S) then
    Check((S.Nodes[S.Root].Dialect = Draft202012) and not S.Nodes[S.Root].HasPrefixItems
      and (S.Nodes[S.Root].Items < 0) and (S.Nodes[S.Root].PrefixKeyword = 'prefixItems')
      and (S.Nodes[S.Root].ItemsKeyword = 'items'), 'neither keyword applies in 2020-12');
  { $schema decides, whatever the default. }
  if MustCompile('{ "$schema": "http://json-schema.org/draft-04/schema#", "id": "http://example.com/a",'
    + ' "exclusiveMinimum": true, "minimum": 1, "maximum": 5 }', DefaultOptions, S) then
    Check((S.Nodes[S.Root].Dialect = Draft4) and S.Nodes[S.Root].ExclusiveMinimum.IsSet
      and not S.Nodes[S.Root].Minimum.IsSet and S.Nodes[S.Root].Maximum.IsSet
      and not S.Nodes[S.Root].ExclusiveMaximum.IsSet, 'the exclusive bounds of draft 4');
end;

procedure TestCompilationErrors;
var
  D: TDocument;
  E: TParseError;
begin
  Check(CompileFails('{ "$ref": "https://example.com/missing.json" }', DefaultOptions)
    = 'Unable to resolve reference ''https://example.com/missing.json'' from '
    + '''https://corvus-oss.org/runtime-evaluator/root.json''.', 'an unresolvable reference');
  Check(CompileFails('{ "pattern": "a(" }', DefaultOptions) = 'Invalid regular expression ''a('' in pattern.',
    'an invalid pattern');
  Check(CompileFails('{ "patternProperties": { "[a": true } }', DefaultOptions)
    = 'Invalid regular expression ''[a'' in patternProperties.', 'an invalid pattern property');
  Check(CompileFails('{ "$ref": "#/$defs/nothing" }', DefaultOptions)
    = 'Unable to resolve reference ''#/$defs/nothing'' from ''https://corvus-oss.org/runtime-evaluator/root.json''.',
    'a pointer to nothing');
  Check(CompileFails('{ "$dynamicRef": "#nothing" }', DefaultOptions)
    = 'Unable to resolve reference ''#nothing'' from ''https://corvus-oss.org/runtime-evaluator/root.json''.',
    'an anchor that is not there');
  Check(not ParseDocumentString('{ "type": ', D, E), 'schema text that is not JSON');
end;

procedure TestKeepsTheDynamicScope;
var
  O: TCompileOptions;
  S: TCompiledSchema;
  Tree, Children, Items: TNodeID;
begin
  ItemDoc := Parse('true');
  TreeDoc := Parse('{ "$schema": "https://json-schema.org/draft/2020-12/schema",'
    + ' "$id": "https://example.com/tree", "$dynamicAnchor": "node", "type": "object",'
    + ' "properties": { "data": true, "children": { "type": "array", "items": { "$dynamicRef": "#node" } } } }');
  O := DefaultOptions;
  WithDocumentResolver(O, TestResolver, @ItemDoc);
  { The entry resource defines the anchor, so every path through the reference ends at the entry: it is resolved
    when the schema is compiled. }
  if MustCompile('{ "$schema": "https://json-schema.org/draft/2020-12/schema",'
    + ' "$id": "https://example.com/strict-tree", "$dynamicAnchor": "node", "$ref": "tree",'
    + ' "unevaluatedProperties": false }', O, S) then begin
    Tree := S.Nodes[S.Root].Ref;
    Check((Tree >= 0) and (S.Nodes[S.Root].UnevaluatedProperties >= 0), 'the strict tree refers to the tree');
    Children := PropertyNode(S, Tree, 'children');
    Check(Children >= 0, 'the tree has children');
    if Children >= 0 then begin
      Items := S.Nodes[Children].Items;
      Check((Items >= 0) and (S.Nodes[Items].StaticDynamicRef = S.Root)
        and (S.Nodes[Items].StaticDynamicKeyword = '$dynamicRef') and not S.Nodes[Items].HasDynamicRef,
        'the dynamic reference resolves to the entry');
    end;
    Check(not S.UsesDynamicScope, 'no dynamic scope is kept');
    Check(S.Nodes[S.Root].MarksProperties and S.Nodes[Tree].MarksProperties, 'the tree marks properties');
  end;
  { Entered from a schema that does not define the anchor, the reference stays dynamic: the tree and the strict
    tree both define it. }
  ItemDoc := Parse('{ "$schema": "https://json-schema.org/draft/2020-12/schema",'
    + ' "$id": "https://example.com/schemas/item.json", "$dynamicAnchor": "node", "$ref": "../tree",'
    + ' "unevaluatedProperties": false }');
  if MustCompile('{ "$schema": "https://json-schema.org/draft/2020-12/schema", "$id": "https://example.com/start",'
    + ' "anyOf": [{ "$ref": "tree" }, { "$ref": "schemas/item.json" }] }', O, S) then begin
    Check(S.UsesDynamicScope, 'the dynamic scope is kept');
    Tree := S.Nodes[S.Nodes[S.Root].AnyOf[0]].Ref;
    Items := S.Nodes[PropertyNode(S, Tree, 'children')].Items;
    Check(S.Nodes[Items].HasDynamicRef and not S.Nodes[Items].DynamicRef.IsRecursive
      and (S.Nodes[Items].DynamicRef.Fallback = Tree) and (Length(S.Nodes[Items].DynamicRef.ByResource) = 2)
      and (S.Nodes[Items].StaticDynamicRef = NoNode), 'the reference has a target in each resource');
    if Length(S.Nodes[Items].DynamicRef.ByResource) = 2 then
      Check((S.Nodes[Items].DynamicRef.ByResource[0].Node = Tree)
        and (S.Nodes[Items].DynamicRef.ByResource[1].Node = S.Nodes[S.Nodes[S.Root].AnyOf[1]].Ref)
        and (S.Nodes[Items].DynamicRef.ByResource[0].Resource = S.Nodes[Tree].ResourceID), 'the targets by resource');
  end;
end;

procedure TestFormatAndContentOptions;
var
  O: TCompileOptions;
  S: TCompiledSchema;
const
  Ipv4 = '{ "format": "ipv4" }';
  Draft7Ipv4 = '{ "$schema": "http://json-schema.org/draft-07/schema#", "format": "ipv4" }';
  Content = '{ "contentEncoding": "base64", "contentMediaType": "application/json" }';
begin
  { Format is an annotation by default and asserted on request. }
  if MustCompile(Ipv4, DefaultOptions, S) then
    Check(S.Nodes[S.Root].HasFormat and (S.Nodes[S.Root].Format = 'ipv4')
      and (S.Nodes[S.Root].FormatKind = FormatKindIPv4) and not S.Nodes[S.Root].AssertFormat
      and not NodeHasStringKeywords(S.Nodes[S.Root]), 'format annotates by default');
  O := DefaultOptions;
  WithAssertFormat(O, True);
  if MustCompile(Ipv4, O, S) then
    Check(S.Nodes[S.Root].AssertFormat and NodeHasStringKeywords(S.Nodes[S.Root])
      and not NodeHasNumberKeywords(S.Nodes[S.Root]), 'format asserted on request');
  if MustCompile('{ "format": "int32" }', O, S) then
    Check(S.Nodes[S.Root].AssertFormat and (S.Nodes[S.Root].FormatKind = FormatKindInt32)
      and NodeHasNumberKeywords(S.Nodes[S.Root]) and not NodeHasStringKeywords(S.Nodes[S.Root]),
      'a numeric format is a number keyword');
  if MustCompile('{ "format": "even-length" }', O, S) then
    Check(S.Nodes[S.Root].AssertFormat and (S.Nodes[S.Root].FormatKind = FormatKindUnknown)
      and (S.Nodes[S.Root].Format = 'even-length'), 'an unknown format keeps its name');
  if MustCompile(Draft7Ipv4, DefaultOptions, S) then
    Check(not S.Nodes[S.Root].AssertFormat, 'format annotates in draft 7');
  O := DefaultOptions;
  WithAssertFormatInLegacyDrafts(O, True);
  if MustCompile(Draft7Ipv4, O, S) then
    Check(S.Nodes[S.Root].AssertFormat, 'format asserted in legacy drafts on request');
  if MustCompile(Ipv4, O, S) then
    Check(not S.Nodes[S.Root].AssertFormat, 'the legacy option leaves 2020-12 alone');
  WithAssertFormat(O, False);
  if MustCompile(Draft7Ipv4, O, S) then
    Check(not S.Nodes[S.Root].AssertFormat, 'never asserting wins over the legacy option');
  { The format-assertion vocabulary asserts. }
  ItemDoc := Parse('{ "$schema": "https://json-schema.org/draft/2020-12/schema",'
    + ' "$id": "https://example.com/schemas/item.json", "$vocabulary": {'
    + ' "https://json-schema.org/draft/2020-12/vocab/core": true,'
    + ' "https://json-schema.org/draft/2020-12/vocab/format-assertion": true } }');
  O := DefaultOptions;
  WithDocumentResolver(O, TestResolver, @ItemDoc);
  if MustCompile('{ "$schema": "https://example.com/schemas/item.json", "format": "ipv4", "type": "string",'
    + ' "properties": {} }', O, S) then
    Check(S.Nodes[S.Root].AssertFormat and not S.Nodes[S.Root].HasType and not S.Nodes[S.Root].HasProperties,
      'the vocabularies of a metaschema decide which keywords apply');

  { Content is asserted in draft 7 only. }
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile(Content, O, S) then
    Check((S.Nodes[S.Root].Content = ContentBase64JSON) and S.Nodes[S.Root].AssertContent
      and NodeHasStringKeywords(S.Nodes[S.Root]), 'content asserted in draft 7');
  WithAssertContent(O, False);
  if MustCompile(Content, O, S) then
    Check((S.Nodes[S.Root].Content = ContentBase64JSON) and not S.Nodes[S.Root].AssertContent,
      'content not asserted on request');
  if MustCompile(Content, DefaultOptions, S) then
    Check((S.Nodes[S.Root].Content = ContentBase64JSON) and not S.Nodes[S.Root].AssertContent,
      'content annotates in 2020-12');
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile('{ "contentMediaType": "application/json" }', O, S) then
    Check((S.Nodes[S.Root].Content = ContentJSON) and S.Nodes[S.Root].AssertContent, 'a media type alone');
  if MustCompile('{ "contentEncoding": "base64" }', O, S) then
    Check((S.Nodes[S.Root].Content = ContentBase64) and S.Nodes[S.Root].AssertContent, 'an encoding alone');
  if MustCompile('{ "contentEncoding": "base32" }', O, S) then
    Check((S.Nodes[S.Root].Content = ContentNone) and not S.Nodes[S.Root].AssertContent, 'an unknown encoding');
  WithDefaultDialect(O, Draft6);
  if MustCompile(Content, O, S) then
    Check(S.Nodes[S.Root].Content = ContentNone, 'no content keywords before draft 7');
end;

function EvenLength(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (Len mod 2 = 0) and (Start >= 0) and (Start + Len <= Length(Text));
end;

function OddLength(const Text: TBytes; Start, Len: Int32): Boolean;
begin
  Result := (Len mod 2 = 1) and (Start >= 0) and (Start + Len <= Length(Text));
end;

procedure TestOptions;
var
  O, Other: TCompileOptions;
  B: TBytes;
begin
  O := DefaultOptions;
  Check((O.DefaultDialect = Draft202012) and (O.AssertFormat = 0) and not O.AssertFormatInLegacyDrafts
    and O.AssertContent and (O.MaxDepth = 128) and not O.HasEntryPoint and not Assigned(O.Resolver)
    and (O.BaseURI = '') and (Length(O.Formats) = 0), 'the defaults');
  WithMaxDepth(O, 0);
  Check(O.MaxDepth = 128, 'a depth below one is ignored');
  WithMaxDepth(O, 7);
  Check(O.MaxDepth = 7, 'a depth');
  WithAssertFormat(O, False);
  Check(O.AssertFormat = -1, 'never assert');
  WithAssertFormat(O, True);
  Check(O.AssertFormat = 1, 'always assert');
  WithEntryPoint(O, '');
  Check(O.HasEntryPoint and (O.EntryPoint = ''), 'an empty entry point is still one');
  Check(not Assigned(CustomFormat(O, 'even-length')), 'no custom format');
  WithFormat(O, 'even-length', EvenLength);
  Other := O;
  WithFormat(Other, 'odd-length', OddLength);
  WithFormat(Other, 'even-length', OddLength);
  B := BytesOf('abc');
  Check(Assigned(CustomFormat(O, 'even-length')) and not CustomFormat(O, 'even-length')(B, 0, 3)
    and CustomFormat(O, 'even-length')(B, 1, 2), 'a custom format');
  Check(not Assigned(CustomFormat(O, 'odd-length')), 'options copied before a format was added keep their own list');
  Check((Length(Other.Formats) = 2) and CustomFormat(Other, 'odd-length')(B, 0, 3)
    and CustomFormat(Other, 'even-length')(B, 0, 3), 'a format given twice is the last');
  Check(ErrDepthExceeded = 'the schema recursed in place beyond the maximum depth', 'the depth error');
end;

procedure TestDiscriminators;
var
  S: TCompiledSchema;
  K, I: Int32;
  Keyword, Branches, Schema: UTF8String;
  D: TDiscriminator;
  Has: Boolean;
  Instance: TDocument;

  function Shape(const Kind, Extra: UTF8String): UTF8String;
  begin
    Result := '{ "type": "object", "properties": { "kind": { "const": ' + Kind + ' }, "' + Extra
      + '": { "type": "number" } }, "required": ["kind", "' + Extra + '"] }';
  end;

  function Selects(const Value: UTF8String): UTF8String;
  var
    N: Int32;
  begin
    Instance := Parse(Value);
    Result := 'unknown: ' + BranchList(D.Unknown);
    for N := 0 to Length(D.Known) - 1 do
      if DiscriminatorValueMatches(D.Known[N].Value, S.Documents, Instance, Instance.Root) then
        Result := BranchList(D.Known[N].Branches);
  end;

begin
  { A discriminated oneOf and anyOf (TestDiscriminatedOneOfAndAnyOfAgreeWithExhaustiveEvaluation). }
  Branches := '[' + Shape('"circle"', 'r') + ',' + Shape('"square"', 'side')
    + ', { "properties": { "kind": { "enum": [1, true] } } }]';
  for K := 0 to 1 do begin
    if K = 0 then
      Keyword := 'oneOf'
    else
      Keyword := 'anyOf';
    if not MustCompile('{ "' + Keyword + '": ' + Branches + ' }', DefaultOptions, S) then
      Continue;
    if K = 0 then begin
      Has := S.Nodes[S.Root].HasOneOfDiscriminator and not S.Nodes[S.Root].HasAnyOfDiscriminator;
      D := S.Nodes[S.Root].OneOfDiscriminator;
    end else begin
      Has := S.Nodes[S.Root].HasAnyOfDiscriminator and not S.Nodes[S.Root].HasOneOfDiscriminator;
      D := S.Nodes[S.Root].AnyOfDiscriminator;
    end;
    Check(Has and (D.PropertyName = 'kind') and (Length(D.Known) = 4) and (Length(D.Unknown) = 0)
      and not D.AllRequire, Keyword + ' has a discriminator on kind');
    if Length(D.Known) = 4 then begin
      Check(Selects('"circle"') = '0 ', Keyword + ': circle selects ' + Selects('"circle"'));
      Check(Selects('"square"') = '1 ', Keyword + ': square selects ' + Selects('"square"'));
      Check(Selects('1.0') = '2 ', Keyword + ': 1.0 selects ' + Selects('1.0'));
      Check(Selects('true') = '2 ', Keyword + ': true selects ' + Selects('true'));
      Check(Selects('false') = 'unknown: ', Keyword + ': false selects ' + Selects('false'));
      Check(Selects('"triangle"') = 'unknown: ', Keyword + ': triangle selects ' + Selects('"triangle"'));
    end;
  end;
  { Discriminators key null and numbers by value. }
  Schema := '{ "oneOf": [' + Shape('null', 'a') + ',' + Shape('1', 'b') + ',' + Shape('1.0', 'c') + ','
    + Shape('"x"', 'd') + '] }';
  if MustCompile(Schema, DefaultOptions, S) then begin
    D := S.Nodes[S.Root].OneOfDiscriminator;
    Check(S.Nodes[S.Root].HasOneOfDiscriminator and (Length(D.Known) = 3) and D.AllRequire
      and (Length(D.Unknown) = 0), 'null, 1 and x are the keys');
    if Length(D.Known) = 3 then begin
      Check(Selects('null') = '0 ', 'null selects ' + Selects('null'));
      Check(Selects('1') = '1 2 ', '1 selects ' + Selects('1'));
      Check(Selects('1e0') = '1 2 ', '1e0 selects ' + Selects('1e0'));
      Check(Selects('"x"') = '3 ', 'x selects ' + Selects('"x"'));
      Check(Selects('"y"') = 'unknown: ', 'y selects ' + Selects('"y"'));
      Check(DiscriminatorValueSame(D.Known[1].Value, D.Known[1].Value, S.Documents)
        and not DiscriminatorValueSame(D.Known[0].Value, D.Known[1].Value, S.Documents)
        and not DiscriminatorValueSame(D.Known[1].Value, D.Known[2].Value, S.Documents), 'key equality');
    end;
  end;
  { A negative branch (a string not in an enum) and a branch behind a reference. }
  if MustCompile('{ "$defs": { "a": { "properties": { "t": { "const": "a" } }, "required": ["t"] } },'
    + ' "oneOf": [{ "$ref": "#/$defs/a" },'
    + ' { "properties": { "t": { "type": "string", "not": { "enum": ["a", "b"] } } }, "required": ["t"] },'
    + ' { "properties": { "t": { "enum": ["b", "c"] } }, "required": ["t"] }] }',
    DefaultOptions, S) then begin
    D := S.Nodes[S.Root].OneOfDiscriminator;
    Check(S.Nodes[S.Root].HasOneOfDiscriminator and (D.PropertyName = 't') and D.AllRequire
      and (BranchList(D.Unknown) = '1 '), 'a negative branch stays a candidate for an unknown value');
    if Length(D.Known) = 3 then begin
      Check(Selects('"a"') = '0 ', 'a selects ' + Selects('"a"'));
      Check(Selects('"b"') = '2 ', 'b selects ' + Selects('"b"'));
      Check(Selects('"c"') = '1 2 ', 'c selects ' + Selects('"c"'));
    end else
      Check(False, IntText(Length(D.Known)) + ' known values, want 3');
  end;
  { One constrained branch is no discriminator. }
  if MustCompile('{ "oneOf": [{ "properties": { "t": { "const": 1 } } }, { "type": "string" }] }', DefaultOptions,
    S) then
    Check(not S.Nodes[S.Root].HasOneOfDiscriminator, 'one constrained branch is no discriminator');
  for I := 0 to 0 do
    Inc(Checks);
end;

procedure TestGraph;
var
  S: TCompiledSchema;
  O: TCompileOptions;
  A, B: TNodeID;
  N: TSchemaNode;
  Deps: UTF8String;
  I: Int32;
  Entries: TAnnotationEntryArray;

  function Count(const Json: UTF8String): TOptCount;
  var
    D: TDocument;
  begin
    D := Parse(Json);
    Result := UIntValue(D, D.Root);
  end;

begin
  { Counts. }
  Check(Count('2').IsSet and (Count('2').N = 2), 'a count');
  Check(Count('2.0').IsSet and (Count('2.0').N = 2), 'a count written as a float');
  Check(Count('0').IsSet and (Count('0').N = 0), 'zero is a count');
  Check(not Count('-1').IsSet and not Count('1.5').IsSet and not Count('"1"').IsSet and not Count('-0.5').IsSet,
    'what is not a count');
  Check(Count('18446744073709551615').IsSet and (Count('18446744073709551615').N = UInt64($FFFFFFFFFFFFFFFF)),
    'the largest count');
  Check(Count('1e19').IsSet and (Count('1e19').N = UInt64(10000000000000000000)), 'a count beyond an Int64');
  Check(not Count('1e20').IsSet, 'too large for a count');

  { Assertions of every kind, digested. }
  if MustCompile('{ "type": ["integer", "string", "nonsense"], "const": 1, "enum": [1, "a", null],'
    + ' "required": ["a", "b", "a", 3], "minProperties": 1, "maxProperties": 2.0, "minItems": 3, "maxItems": 4,'
    + ' "uniqueItems": true, "minLength": 5, "maxLength": 6, "pattern": "^a", "multipleOf": 0.5, "minimum": 1,'
    + ' "maximum": 2, "exclusiveMinimum": 0, "exclusiveMaximum": 3, "contains": true, "minContains": 2,'
    + ' "maxContains": 3, "propertyNames": { "pattern": "^a" }, "patternProperties": { "^a": true, "b$": false },'
    + ' "additionalProperties": false }', DefaultOptions, S) then begin
    N := S.Nodes[S.Root];
    Check(N.HasType and (N.TypeMask = TypeInteger or TypeString), 'the type mask');
    Check(N.ConstValue.IsSet and N.HasEnum and (Length(N.EnumValues) = 3), 'const and enum');
    Check(N.HasRequired and (Length(N.Required) = 2) and (N.Required[0] = 'a') and (N.Required[1] = 'b')
      and (Length(N.RequiredList) = 3) and (N.RequiredList[2] = 'a'), 'required, with and without duplicates');
    Check(N.MinProperties.IsSet and (N.MinProperties.N = 1) and N.MaxProperties.IsSet and (N.MaxProperties.N = 2)
      and (N.MinItems.N = 3) and (N.MaxItems.N = 4) and N.UniqueItems and (N.MinLength.N = 5)
      and (N.MaxLength.N = 6), 'the counts');
    Check(N.MultipleOf.IsSet and N.HasDivisor and N.Minimum.IsSet and N.Maximum.IsSet and N.ExclusiveMinimum.IsSet
      and N.ExclusiveMaximum.IsSet, 'the bounds');
    Check((N.Contains >= 0) and (N.MinContains = 2) and N.MaxContains.IsSet and (N.MaxContains.N = 3)
      and N.ContainsMarksEvaluated, 'contains');
    Check((N.Pattern >= 0) and (S.Patterns[N.Pattern].Kind = MatchLiteral) and (S.Patterns[N.Pattern].Source = '^a'),
      'the pattern');
    Check(N.HasPatternProperties and (Length(N.PatternProperties) = 2)
      and (N.PatternProperties[0].Pattern = N.Pattern) and (N.PatternProperties[1].Pattern <> N.Pattern)
      and (S.Nodes[N.PropertyNames].Pattern = N.Pattern) and (Length(S.Patterns) = 2),
      'identical patterns of a schema share one matcher');
    Check(S.Nodes[N.PatternProperties[1].Node].AlwaysFalse and S.Nodes[N.PatternProperties[0].Node].AlwaysTrue
      and (S.Nodes[N.PatternProperties[1].Node].Pointer = '/patternProperties/b$'), 'the pattern properties');
    Check(NodeHasObjectKeywords(N) and NodeHasArrayKeywords(N) and NodeHasStringKeywords(N)
      and NodeHasNumberKeywords(N) and not NodeHasInPlaceApplicators(N) and not NodeIsPureRef(N),
      'the keyword groups');
    Check(N.MarksProperties and N.MarksItems and not N.InPlaceCycle, 'what the node marks');
    Check(S.AnnotationSources[S.Root].IsSet and (S.AnnotationSources[S.Root].Doc = 0)
      and (S.AnnotationSources[S.Root].Value = S.Documents[0].Root)
      and (S.AnnotationSources[S.Root].Vocab = VocabAllAnnotating) and S.AnnotationSources[S.Root].Content,
      'the annotation source');
    Check(not S.AnnotationSources[N.Contains].IsSet, 'a boolean schema has no annotation source');
  end;
  { minContains without contains is nothing, and its default is one. }
  if MustCompile('{ "minContains": 0, "contains": {} }', DefaultOptions, S) then
    Check(S.Nodes[S.Root].MinContains = 0, 'minContains of zero');
  if MustCompile('{ "contains": {}, "minContains": "x" }', DefaultOptions, S) then
    Check(S.Nodes[S.Root].MinContains = 1, 'minContains that is not a count');
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile('{ "contains": {}, "minContains": 0 }', O, S) then
    Check((S.Nodes[S.Root].MinContains = 1) and not S.Nodes[S.Root].ContainsMarksEvaluated,
      'no minContains before 2019-09');

  { References, pure and not, and the cycles of in-place applicators. }
  if MustCompile('{ "$ref": "#" }', DefaultOptions, S) then
    Check((S.Nodes[S.Root].Ref = S.Root) and S.Nodes[S.Root].InPlaceCycle and NodeIsPureRef(S.Nodes[S.Root]),
      'a schema that refers to itself in place');
  if MustCompile('{ "properties": { "a": { "$ref": "#" } } }', DefaultOptions, S) then
    Check(not S.Nodes[S.Root].InPlaceCycle and (S.Nodes[PropertyNode(S, S.Root, 'a')].Ref = S.Root)
      and not S.Nodes[PropertyNode(S, S.Root, 'a')].InPlaceCycle, 'recursion through a property is no in-place cycle');
  if MustCompile('{ "$defs": { "a": { "allOf": [{ "$ref": "#/$defs/b" }] }, "b": { "not": { "$ref": "#/$defs/a" } },'
    + ' "c": { "type": "string" } }, "anyOf": [{ "$ref": "#/$defs/a" }, { "$ref": "#/$defs/c" }] }', DefaultOptions,
    S) then begin
    A := S.Nodes[S.Nodes[S.Root].AnyOf[0]].Ref;
    B := S.Nodes[S.Nodes[A].AllOf[0]].Ref;
    Check(S.Nodes[A].InPlaceCycle and S.Nodes[B].InPlaceCycle and S.Nodes[S.Nodes[A].AllOf[0]].InPlaceCycle
      and S.Nodes[S.Nodes[B].NotNode].InPlaceCycle and not S.Nodes[S.Root].InPlaceCycle
      and not S.Nodes[S.Nodes[S.Root].AnyOf[1]].InPlaceCycle, 'a cycle through allOf, $ref and not');
    Check((S.Nodes[A].Pointer = '/$defs/a') and (S.Nodes[B].Pointer = '/$defs/b'), 'the pointers of the nodes');
  end;
  { In draft 7 and earlier, $ref replaces every sibling keyword. }
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile('{ "definitions": { "a": { "type": "string" } }, "$ref": "#/definitions/a", "type": "number",'
    + ' "properties": { "x": false } }', O, S) then
    Check((S.Nodes[S.Root].Ref >= 0) and not S.Nodes[S.Root].HasType and not S.Nodes[S.Root].HasProperties
      and NodeIsPureRef(S.Nodes[S.Root]) and not S.AnnotationSources[S.Root].IsSet and (Length(S.Nodes) = 2),
      'a legacy $ref replaces its siblings');
  if MustCompile('{ "$defs": { "a": { "type": "string" } }, "$ref": "#/$defs/a", "type": "number" }',
    DefaultOptions, S) then
    Check((S.Nodes[S.Root].Ref >= 0) and S.Nodes[S.Root].HasType and not NodeIsPureRef(S.Nodes[S.Root]),
      'a $ref of 2020-12 applies beside its siblings');
  { Anchors, and resources within a document. }
  if MustCompile('{ "$id": "https://example.com/root", "$defs": { "a": { "$anchor": "here", "type": "string" },'
    + ' "b": { "$id": "nested", "$defs": { "c": { "$anchor": "here", "type": "number" } } } },'
    + ' "allOf": [{ "$ref": "#here" }, { "$ref": "nested#here" }, { "$ref": "https://example.com/nested#/$defs/c" },'
    + ' { "$ref": "#/$defs/b/$defs/c" }] }', DefaultOptions, S) then begin
    A := S.Nodes[S.Nodes[S.Root].AllOf[0]].Ref;
    B := S.Nodes[S.Nodes[S.Root].AllOf[1]].Ref;
    Check((S.Nodes[A].TypeMask = TypeString) and (S.Nodes[B].TypeMask = TypeNumber), 'an anchor in each resource');
    Check((S.Nodes[S.Nodes[S.Root].AllOf[2]].Ref = B) and (S.Nodes[S.Nodes[S.Root].AllOf[3]].Ref = B),
      'one node however it is referred to');
    Check((S.Nodes[B].ResourceID <> S.Nodes[S.Root].ResourceID) and (S.Nodes[A].ResourceID = S.Nodes[S.Root].ResourceID)
      and (S.Nodes[B].Pointer = '/$defs/b/$defs/c'), 'the resource and pointer of a nested node');
  end;
  { A reference into a metaschema loads it. }
  if MustCompile('{ "$ref": "https://json-schema.org/draft/2020-12/schema" }', DefaultOptions, S) then
    Check((Length(S.Documents) > 1) and S.UsesDynamicScope, 'a reference to the 2020-12 metaschema');
  if MustCompile('{ "$ref": "http://json-schema.org/draft-07/schema#/definitions/nonNegativeInteger" }',
    DefaultOptions, S) then
    Check((Length(S.Documents) = 2) and (S.Nodes[S.Nodes[S.Root].Ref].Dialect = Draft7)
      and (S.Nodes[S.Nodes[S.Root].Ref].TypeMask = TypeInteger), 'a reference into the draft 7 metaschema');

  { Marking: not does not contribute annotations. }
  if MustCompile('{ "allOf": [{ "properties": {} }], "not": { "items": {} } }', DefaultOptions, S) then
    Check(S.Nodes[S.Root].MarksProperties and not S.Nodes[S.Root].MarksItems
      and S.Nodes[S.Nodes[S.Root].NotNode].MarksItems, 'not does not mark');
  if MustCompile('{ "if": { "prefixItems": [true] }, "dependentSchemas": { "a": { "properties": {} } } }',
    DefaultOptions, S) then
    Check(S.Nodes[S.Root].MarksProperties and S.Nodes[S.Root].MarksItems and NodeHasDependencySchema(S.Nodes[S.Root])
      and NodeHasInPlaceApplicators(S.Nodes[S.Root]), 'if and dependent schemas mark');

  { Dependencies, in the order the three keywords appear in the schema. }
  O := DefaultOptions;
  WithDefaultDialect(O, Draft201909);
  if MustCompile('{ "dependentRequired": { "a": ["b", 1, "c"], "skipped": 3 }, "dependencies": { "c": ["d"], "e": {} },'
    + ' "dependentSchemas": { "f": true } }', O, S) then begin
    Deps := '';
    for I := 0 to Length(S.Nodes[S.Root].Dependencies) - 1 do
      Deps := Deps + S.Nodes[S.Root].Dependencies[I].Keyword + ':' + S.Nodes[S.Root].Dependencies[I].Name + ' ';
    Check(S.Nodes[S.Root].HasDependencies
      and (Deps = 'dependentRequired:a dependencies:c dependencies:e dependentSchemas:f '), 'dependencies: ' + Deps);
    if Length(S.Nodes[S.Root].Dependencies) = 4 then
      Check(S.Nodes[S.Root].Dependencies[0].HasRequired and (Length(S.Nodes[S.Root].Dependencies[0].Required) = 2)
        and (S.Nodes[S.Root].Dependencies[0].Schema = NoNode) and not S.Nodes[S.Root].Dependencies[2].HasRequired
        and (S.Nodes[S.Root].Dependencies[2].Schema >= 0)
        and (S.Nodes[S.Nodes[S.Root].Dependencies[3].Schema].Pointer = '/dependentSchemas/f'), 'the entries');
  end;
  O := DefaultOptions;
  WithDefaultDialect(O, Draft7);
  if MustCompile('{ "dependentSchemas": { "f": true }, "dependentRequired": { "a": ["b"] } }', O, S) then
    Check(not S.Nodes[S.Root].HasDependencies, 'no dependent keywords in draft 7');

  { The annotations of a schema object, in the order the keywords are written, the content keywords last. }
  if MustCompile('{ "title": "Person", "x-unknown": [1, {"a": null}], "format": "ipv4", "type": "object",'
    + ' "contentMediaType": "text/plain", "contentSchema": {}, "contentEncoding": "base64", "deprecated": true,'
    + ' "description": "a\nb", "default": 1.50, "examples": [], "readOnly": false }', DefaultOptions, S) then begin
    Entries := CollectAnnotationEntries(S.Documents[S.AnnotationSources[S.Root].Doc],
      S.AnnotationSources[S.Root].Value, S.Nodes[S.Root].Dialect, S.AnnotationSources[S.Root].Vocab,
      S.AnnotationSources[S.Root].Content, False);
    Deps := '';
    for I := 0 to Length(Entries) - 1 do begin
      Deps := Deps + Entries[I].Keyword + '=' + Entries[I].Value;
      if Entries[I].StringsOnly then
        Deps := Deps + '(strings)';
      Deps := Deps + ' ';
    end;
    Check(Deps = 'title="Person" x-unknown=[1,{"a":null}] format="ipv4" deprecated=true description="a\nb" '
      + 'default=1.50 examples=[] readOnly=false contentEncoding="base64"(strings) '
      + 'contentMediaType="text/plain"(strings) contentSchema={}(strings) ', 'annotations: ' + Deps);
    Entries := CollectAnnotationEntries(S.Documents[0], S.Documents[0].Root, Draft4, 0, True, False);
    Deps := '';
    for I := 0 to Length(Entries) - 1 do
      Deps := Deps + Entries[I].Keyword + ' ';
    Check(Deps = 'title format description default ', 'annotations of draft 4: ' + Deps);
    Entries := CollectAnnotationEntries(S.Documents[0], S.Documents[0].Root, Draft202012, VocabCore, False, False);
    Check((Length(Entries) = 1) and (Entries[0].Keyword = 'x-unknown'), 'annotations with the core vocabulary only');
    Entries := CollectAnnotationEntries(S.Documents[0], S.Documents[0].Root, Draft202012, VocabCore, False, True);
    Check((Length(Entries) = 2) and (Entries[1].Keyword = 'format'), 'format annotates when its assertion is set');
  end;
  Check(IsKnownKeyword('$anchor') and IsKnownKeyword('writeOnly') and IsKnownKeyword('id') and IsKnownKeyword('if')
    and not IsKnownKeyword('x-unknown') and not IsKnownKeyword('') and not IsKnownKeyword('Type')
    and not IsKnownKeyword('zzz'), 'the known keywords');

  { Numbers of a schema keep their text (TestSchemaAndInstanceNumbersParseAlike of the Go module compares an
    instance with them). }
  if MustCompile('{"exclusiveMaximum": 972783798187987123879878123.18878137}', DefaultOptions, S) then begin
    A := S.Nodes[S.Root].ExclusiveMaximum.N;
    Check((DocFlags(S.Documents[0], A) = NumFloat) and (DocData(S.Documents[0], A) = UInt64($45892557DAF10FBE)),
      'a bound beyond an integer');
  end;
end;

begin
  Failures := 0;
  Checks := 0;
  TestValues;
  TestNameMapFindsEveryName;
  TestNamesFollowDeclaredOrSortedOrder;
  TestLinearNamesFindFromAnyHint;
  TestLargeNameSets;
  TestDialects;
  TestEmbeddedMetaschemasAreCurrent;
  TestEveryEmbeddedMetaschemaResolvesByItsURI;
  TestMatchersAgreeWithTheEngine;
  TestMoreShapesAgreeWithTheEngine;
  TestSimplePatternsTakeTheFastMatchers;
  TestInvalidPatternsAreRejected;
  TestExcludedClassWithAMemberOutsideASCII;
  TestMatchersByHand;
  TestUnicodeFormatsUseUnicode17;
  TestNoToolchainUnicodeData;
  TestResultsCollector;
  TestOptions;
  TestEntryPointEvaluatesFromASubschema;
  TestBaseURIResolvesRelativeReferences;
  TestDefaultDialectAppliesToSchemasWithoutSchema;
  TestCompilationErrors;
  TestKeepsTheDynamicScope;
  TestFormatAndContentOptions;
  TestDiscriminators;
  TestGraph;
  EcmaRegexReleaseThreadScratch;

  WriteLn(Checks, ' checks, ', Failures, ' failed');
  if Failures > 0 then
    Halt(1);
end.
