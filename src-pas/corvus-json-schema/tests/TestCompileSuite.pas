program TestCompileSuite;

{$I corvus.inc}

{ Compiles every schema of the JSON-Schema-Test-Suite, as suite_test.go of the Go module does before it evaluates:
  the required and optional tests with format as an annotation, optional/format with format asserted, and the
  suite's remotes (http://localhost:1234/...) resolved from its remotes directory. Every schema must compile.

  The suite is looked for at ../../JSON-Schema-Test-Suite (the repository's submodule, from the package directory).
  Set JSON_SCHEMA_TEST_SUITE to use a different checkout, and SUITE_DRAFT to narrow the run to one draft.

  After the suite it compiles the standard metaschemas by their URIs (all but the hyper-schema vocabularies, which
  refer to a links schema that is not embedded). When CORVUS_EXTRA_SCHEMAS names a directory, it also compiles the
  schema.json of each of its subdirectories (the schemas of the jsonschema-benchmark repository are laid out so).

  With --dump <file> the program also writes every compiled graph as text, a line for each node. A program that
  writes the same lines from the Go module's compiler gives a file to compare this one with. }

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Options,
  Corvus.JsonSchema.EcmaRegex,
  Corvus.JsonSchema.Pattern,
  Corvus.JsonSchema.Metaschemas,
  Corvus.JsonSchema.Node,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema.Compiler;

type
  { The suite's remotes: the documents read so far, by their path within the remotes directory. }
  TRemotes = record
    Root: UTF8String;
    Cache: TStringTable;
    Docs: array of TDocument;
  end;
  PRemotes = ^TRemotes;

var
  Failures: Int32;
  Dumping: Boolean;
  Dump: Text;

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

{ RemoteResolver resolves the suite's remotes (http://localhost:1234/...) from its remotes directory. }
function RemoteResolver(Context: Pointer; const Uri: UTF8String; out Doc: TDocument): Boolean;
const
  Prefix: UTF8String = 'http://localhost:1234/';
var
  R: PRemotes;
  Rest: UTF8String;
  At, N: Int32;
  Text: TBytes;
  E: TParseError;
begin
  Doc := Default(TDocument);
  Result := False;
  R := PRemotes(Context);
  if Copy(Uri, 1, Length(Prefix)) <> Prefix then
    Exit;
  Rest := Copy(Uri, Length(Prefix) + 1, Length(Uri) - Length(Prefix));
  At := StringTableFind(R^.Cache, Rest);
  if At >= 0 then begin
    Doc := R^.Docs[R^.Cache.Values[At]];
    Result := True;
    Exit;
  end;
  if not ReadFile(R^.Root + '/' + Rest, Text) then
    Exit;
  if not ParseDocument(Text, Doc, E) then
    Exit;
  N := Length(R^.Docs);
  SetLength(R^.Docs, N + 1);
  R^.Docs[N] := Doc;
  StringTableAdd(R^.Cache, Rest, N, '');
  Result := True;
end;

{ JsonFiles are the names of the .json files of a directory, in byte order. }
function JsonFiles(const Dir: UTF8String): TUTF8StringArray;
var
  Search: TSearchRec;
  Name, T: UTF8String;
  N, I, J: Int32;
begin
  Result := nil;
  N := 0;
  if FindFirst(Dir + '/*', faAnyFile, Search) <> 0 then
    Exit;
  repeat
    Name := UTF8String(Search.Name);
    if (Search.Attr and faDirectory = 0) and (Length(Name) > 5)
      and (Copy(Name, Length(Name) - 4, 5) = '.json') then begin
      SetLength(Result, N + 1);
      Result[N] := Name;
      Inc(N);
    end;
  until FindNext(Search) <> 0;
  FindClose(Search);
  for I := 1 to N - 1 do begin
    T := Result[I];
    J := I - 1;
    while (J >= 0) and (CompareUtf8(Result[J], T) > 0) do begin
      Result[J + 1] := Result[J];
      Dec(J);
    end;
    Result[J + 1] := T;
  end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  The dump }

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
    if (C < $21) or (C > $7E) or (C = Ord('%')) or (C = Ord(',')) or (C = Ord('/')) or (C = Ord('[')) or (C = Ord(']'))
      or (C = Ord('=')) or (C = Ord(';')) or (C = Ord(':')) then begin
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

function B(V: Boolean): UTF8String;
begin
  if V then
    Result := '1'
  else
    Result := '0';
end;

function U(V: UInt64): UTF8String;
begin
  Result := '';
  Str(V, Result);
end;

function Ids(const A: TNodeIDArray): UTF8String;
var
  I: Int32;
begin
  Result := '[';
  for I := 0 to Length(A) - 1 do begin
    if I > 0 then
      Result := Result + ',';
    Result := Result + IntText(A[I]);
  end;
  Result := Result + ']';
end;

function Branches(const A: TUInt32Array): UTF8String;
var
  I: Int32;
begin
  Result := '[';
  for I := 0 to Length(A) - 1 do begin
    if I > 0 then
      Result := Result + ',';
    Result := Result + IntText(A[I]);
  end;
  Result := Result + ']';
end;

function Texts(const A: TUTF8StringArray): UTF8String;
var
  I: Int32;
begin
  Result := '[';
  for I := 0 to Length(A) - 1 do begin
    if I > 0 then
      Result := Result + ',';
    Result := Result + Q(A[I]);
  end;
  Result := Result + ']';
end;

function V(const R: TValueRef): UTF8String;
begin
  if R.IsSet then
    Result := IntText(R.Doc) + ':' + IntText(R.N)
  else
    Result := '-';
end;

function O(const C: TOptCount): UTF8String;
begin
  if C.IsSet then
    Result := U(C.N)
  else
    Result := '-';
end;

function Disc(Has: Boolean; const D: TDiscriminator): UTF8String;
var
  I: Int32;
begin
  if not Has then begin
    Result := '-';
    Exit;
  end;
  Result := Q(D.PropertyName) + '/' + B(D.AllRequire) + '/[';
  for I := 0 to Length(D.Known) - 1 do begin
    if I > 0 then
      Result := Result + ';';
    Result := Result + IntText(D.Known[I].Value.Kind) + ':';
    if D.Known[I].Value.Kind = KindString then
      Result := Result + Q(D.Known[I].Value.Text)
    else
      Result := Result + V(D.Known[I].Value.Value);
    Result := Result + ':' + Branches(D.Known[I].Branches);
  end;
  Result := Result + ']/' + Branches(D.Unknown);
end;

function NodeLine(const S: TCompiledSchema; Id: Int32): UTF8String;
var
  N: TSchemaNode;
  I: Int32;
  L: UTF8String;
begin
  N := S.Nodes[Id];
  L := 'N' + IntText(Id) + ' res=' + IntText(N.ResourceID) + ' dia=' + IntText(Ord(N.Dialect)) + ' ptr=' + Q(N.Pointer)
    + ' t=' + B(N.AlwaysTrue) + ' f=' + B(N.AlwaysFalse) + ' tm=' + IntText(N.TypeMask) + ' ht=' + B(N.HasType)
    + ' c=' + V(N.ConstValue) + ' he=' + B(N.HasEnum) + ' e=[';
  for I := 0 to Length(N.EnumValues) - 1 do begin
    if I > 0 then
      L := L + ',';
    L := L + V(N.EnumValues[I]);
  end;
  L := L + '] ref=' + IntText(N.Ref) + ' sdr=' + IntText(N.StaticDynamicRef) + ' sdk=' + Q(N.StaticDynamicKeyword)
    + ' dr=';
  if N.HasDynamicRef then begin
    L := L + B(N.DynamicRef.IsRecursive) + ':' + IntText(N.DynamicRef.Fallback) + ':[';
    for I := 0 to Length(N.DynamicRef.ByResource) - 1 do begin
      if I > 0 then
        L := L + ',';
      L := L + IntText(N.DynamicRef.ByResource[I].Resource) + '/' + IntText(N.DynamicRef.ByResource[I].Node);
    end;
    L := L + ']';
  end else
    L := L + '-';
  L := L + ' hall=' + B(N.HasAllOf) + ' all=' + Ids(N.AllOf) + ' hany=' + B(N.HasAnyOf) + ' any=' + Ids(N.AnyOf)
    + ' hone=' + B(N.HasOneOf) + ' one=' + Ids(N.OneOf) + ' not=' + IntText(N.NotNode) + ' if=' + IntText(N.IfNode)
    + ' then=' + IntText(N.ThenNode) + ' else=' + IntText(N.ElseNode) + ' hp=' + B(N.HasProperties) + ' p=[';
  for I := 0 to Length(N.Properties) - 1 do begin
    if I > 0 then
      L := L + ',';
    L := L + Q(N.Properties[I].Name) + '/' + IntText(N.Properties[I].Node);
  end;
  L := L + '] hpp=' + B(N.HasPatternProperties) + ' pp=[';
  for I := 0 to Length(N.PatternProperties) - 1 do begin
    if I > 0 then
      L := L + ',';
    L := L + Q(S.Patterns[N.PatternProperties[I].Pattern].Source) + '/'
      + IntText(Ord(S.Patterns[N.PatternProperties[I].Pattern].Kind)) + '/' + IntText(N.PatternProperties[I].Node);
  end;
  L := L + '] ap=' + IntText(N.AdditionalProperties) + ' pn=' + IntText(N.PropertyNames) + ' hr=' + B(N.HasRequired)
    + ' r=' + Texts(N.Required) + ' rl=' + Texts(N.RequiredList) + ' hd=' + B(N.HasDependencies) + ' d=[';
  for I := 0 to Length(N.Dependencies) - 1 do begin
    if I > 0 then
      L := L + ',';
    L := L + Q(N.Dependencies[I].Keyword) + '/' + Q(N.Dependencies[I].Name) + '/' + B(N.Dependencies[I].HasRequired)
      + '/' + Texts(N.Dependencies[I].Required) + '/' + IntText(N.Dependencies[I].Schema);
  end;
  L := L + '] minp=' + O(N.MinProperties) + ' maxp=' + O(N.MaxProperties) + ' up=' + IntText(N.UnevaluatedProperties)
    + ' hpi=' + B(N.HasPrefixItems) + ' pi=' + Ids(N.PrefixItems) + ' pk=' + Q(N.PrefixKeyword) + ' ik='
    + Q(N.ItemsKeyword) + ' it=' + IntText(N.Items) + ' co=' + IntText(N.Contains) + ' minc=' + U(N.MinContains)
    + ' maxc=' + O(N.MaxContains) + ' cme=' + B(N.ContainsMarksEvaluated) + ' mini=' + O(N.MinItems) + ' maxi='
    + O(N.MaxItems) + ' uniq=' + B(N.UniqueItems) + ' ui=' + IntText(N.UnevaluatedItems) + ' minl=' + O(N.MinLength)
    + ' maxl=' + O(N.MaxLength) + ' pat=';
  if N.Pattern >= 0 then
    L := L + Q(S.Patterns[N.Pattern].Source) + '/' + IntText(Ord(S.Patterns[N.Pattern].Kind))
  else
    L := L + '-';
  L := L + ' hf=' + B(N.HasFormat) + ' fmt=' + Q(N.Format) + ' fk=' + IntText(Ord(N.FormatKind)) + ' af='
    + B(N.AssertFormat) + ' ct=' + IntText(Ord(N.Content)) + ' ac=' + B(N.AssertContent) + ' min=' + V(N.Minimum)
    + ' max=' + V(N.Maximum) + ' xmin=' + V(N.ExclusiveMinimum) + ' xmax=' + V(N.ExclusiveMaximum) + ' mo='
    + V(N.MultipleOf) + ' div=' + B(N.HasDivisor) + ' mp=' + B(N.MarksProperties) + ' mi=' + B(N.MarksItems)
    + ' cyc=' + B(N.InPlaceCycle) + ' od=' + Disc(N.HasOneOfDiscriminator, N.OneOfDiscriminator) + ' ad='
    + Disc(N.HasAnyOfDiscriminator, N.AnyOfDiscriminator);
  Result := L;
end;

function SourceLine(const S: TCompiledSchema; Id: Int32): UTF8String;
var
  A: TAnnotationSource;
  Entries: TAnnotationEntryArray;
  I: Int32;
begin
  A := S.AnnotationSources[Id];
  Result := 'A' + IntText(Id) + ' set=' + B(A.IsSet);
  if not A.IsSet then
    Exit;
  Result := Result + ' doc=' + IntText(A.Doc) + ' value=' + IntText(A.Value) + ' vocab=' + IntText(A.Vocab)
    + ' content=' + B(A.Content) + ' ann=[';
  Entries := CollectAnnotationEntries(S.Documents[A.Doc], A.Value, S.Nodes[Id].Dialect, A.Vocab, A.Content, False);
  for I := 0 to Length(Entries) - 1 do begin
    if I > 0 then
      Result := Result + ',';
    Result := Result + Q(Entries[I].Keyword) + '=' + Q(Entries[I].Value) + '/' + B(Entries[I].StringsOnly);
  end;
  Result := Result + ']';
end;

procedure DumpSchema(const S: TCompiledSchema);
var
  I: Int32;
begin
  WriteLn(Dump, 'C root=', S.Root, ' dyn=', B(S.UsesDynamicScope), ' nodes=', Length(S.Nodes), ' docs=',
    Length(S.Documents));
  for I := 0 to Length(S.Nodes) - 1 do begin
    WriteLn(Dump, NodeLine(S, I));
    WriteLn(Dump, SourceLine(S, I));
  end;
end;

{ --------------------------------------------------------------------------------------------------------------------
  The run }

type
  TArea = record
    Name: UTF8String;
    Schemas, Compiled, Nodes, Dynamic, Patterns, Engines: Int32;
  end;

var
  Remotes: TRemotes;
  Areas: array of TArea;

function AreaOf(const AreaName: UTF8String): Int32;
var
  I: Int32;
begin
  Result := -1;
  for I := 0 to Length(Areas) - 1 do
    if Areas[I].Name = AreaName then
      Result := I;
  if Result < 0 then begin
    Result := Length(Areas);
    SetLength(Areas, Result + 1);
    Areas[Result] := Default(TArea);
    Areas[Result].Name := AreaName;
  end;
end;

{ CompileOne compiles one schema, a document or (when Uri is not empty) the document at a URI, and counts it. }
procedure CompileOne(Area: Int32; const Name: UTF8String; Index: Int32; const What: UTF8String;
  const Schema: TDocument; const Uri: UTF8String; const Options: TCompileOptions);
var
  Compiled: TCompiledSchema;
  Error: UTF8String;
  Ok: Boolean;
  I: Int32;
begin
  Inc(Areas[Area].Schemas);
  if Dumping then
    WriteLn(Dump, 'S ', Q(Name), ' ', Index);
  Error := '';
  Compiled := Default(TCompiledSchema);
  if Uri <> '' then
    Ok := CompileFromURI(Uri, Options, Compiled, Error)
  else
    Ok := CompileDocument(Schema, Options, Compiled, Error);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What, ' did not compile: ', Error);
    if Dumping then
      WriteLn(Dump, 'E ', Q(Error));
    Exit;
  end;
  Inc(Areas[Area].Compiled);
  Inc(Areas[Area].Nodes, Length(Compiled.Nodes));
  if Compiled.UsesDynamicScope then
    Inc(Areas[Area].Dynamic);
  Inc(Areas[Area].Patterns, Length(Compiled.Patterns));
  for I := 0 to Length(Compiled.Patterns) - 1 do
    if Compiled.Patterns[I].Kind = MatchEngine then
      Inc(Areas[Area].Engines);
  if Dumping then
    DumpSchema(Compiled);
end;

procedure RunFile(Dialect: TDialect; const Path, Name, AreaName: UTF8String; AssertFormat: Boolean);
var
  Text: TBytes;
  Doc, Sub: TDocument;
  E: TParseError;
  Area, G, Schema, Description: Int32;
  Options: TCompileOptions;
  What, SchemaText: UTF8String;
begin
  Doc := Default(TDocument);
  Area := AreaOf(AreaName);
  if not ReadFile(Path, Text) then begin
    Inc(Failures);
    WriteLn('FAILED: ', Path, ' could not be read');
    Exit;
  end;
  if not ParseDocument(Text, Doc, E) or (DocKind(Doc, Doc.Root) <> KindArray) then begin
    Inc(Failures);
    WriteLn('FAILED: ', Path, ' is not a JSON array: ', ParseErrorText(E));
    Exit;
  end;
  for G := 0 to DocCount(Doc, Doc.Root) - 1 do begin
    Schema := Member(Doc, DocFirst(Doc, Doc.Root) + G, 'schema');
    if Schema < 0 then begin
      Inc(Failures);
      WriteLn('FAILED: ', Name, ' group ', G, ' has no schema');
      Continue;
    end;
    Description := Member(Doc, DocFirst(Doc, Doc.Root) + G, 'description');
    What := Name + ' [';
    if IsKind(Doc, Description, KindString) then
      What := What + DocStrCopy(Doc, Description);
    What := What + ']';
    { The schema as a document of its own, as the Go test compiles the schema's text: its text is written out and
      parsed again, so that the values of the schema are numbered as they are there. }
    Sub := Doc;
    Sub.Root := Schema;
    SchemaText := DocumentToJson(Sub);
    if not ParseDocumentString(SchemaText, Sub, E) then begin
      Inc(Failures);
      WriteLn('FAILED: ', What, ' was not written as JSON: ', ParseErrorText(E));
      Continue;
    end;
    Options := DefaultOptions;
    WithDefaultDialect(Options, Dialect);
    WithDocumentResolver(Options, RemoteResolver, @Remotes);
    if AssertFormat then
      WithAssertFormat(Options, True);
    CompileOne(Area, Name, G, What, Sub, '', Options);
  end;
end;

{ MetaschemaURI is the URI of an embedded metaschema, from its path within the metaschemas directory. }
function MetaschemaURI(const Name: UTF8String): UTF8String;
var
  Slash: Int32;
  Draft, Rest: UTF8String;
begin
  if Name = 'draft4/schema.json' then
    Result := 'http://json-schema.org/draft-04/schema'
  else if Name = 'draft6/schema.json' then
    Result := 'http://json-schema.org/draft-06/schema'
  else if Name = 'draft7/schema.json' then
    Result := 'http://json-schema.org/draft-07/schema'
  else begin
    Slash := Pos('/', Name);
    { "draft" and the draft's name, then the path less ".json". }
    Draft := Copy(Name, 6, Slash - 6);
    Rest := Copy(Name, Slash + 1, Length(Name) - Slash - 5);
    Result := 'https://json-schema.org/draft/' + Draft + '/' + Rest;
  end;
end;

procedure RunMetaschemas;
var
  I, Area: Int32;
  Uri: UTF8String;
begin
  Area := AreaOf('metaschemas');
  for I := 0 to EmbeddedMetaschemaCount - 1 do begin
    Uri := MetaschemaURI(EmbeddedMetaschemaName(I));
    { The hyper-schema vocabulary refers to the links schema, which is not embedded. }
    if Pos('hyper-schema', Uri) > 0 then
      Continue;
    CompileOne(Area, Uri, 0, Uri, Default(TDocument), Uri, DefaultOptions);
  end;
end;

{ Directories are the names of the subdirectories of a directory, in byte order. }
function Directories(const Dir: UTF8String): TUTF8StringArray;
var
  Search: TSearchRec;
  Name, T: UTF8String;
  N, I, J: Int32;
begin
  Result := nil;
  N := 0;
  if FindFirst(Dir + '/*', faAnyFile, Search) <> 0 then
    Exit;
  repeat
    Name := UTF8String(Search.Name);
    if (Search.Attr and faDirectory <> 0) and (Name <> '.') and (Name <> '..') then begin
      SetLength(Result, N + 1);
      Result[N] := Name;
      Inc(N);
    end;
  until FindNext(Search) <> 0;
  FindClose(Search);
  for I := 1 to N - 1 do begin
    T := Result[I];
    J := I - 1;
    while (J >= 0) and (CompareUtf8(Result[J], T) > 0) do begin
      Result[J + 1] := Result[J];
      Dec(J);
    end;
    Result[J + 1] := T;
  end;
end;

procedure RunExtra(const Dir: UTF8String);
var
  Names: TUTF8StringArray;
  I, Area: Int32;
  Text: TBytes;
  Doc: TDocument;
  E: TParseError;
begin
  Area := AreaOf('extra');
  Names := Directories(Dir);
  for I := 0 to Length(Names) - 1 do begin
    if not ReadFile(Dir + '/' + Names[I] + '/schema.json', Text) then
      Continue;
    if not ParseDocument(Text, Doc, E) then begin
      Inc(Failures);
      WriteLn('FAILED: ', Names[I], '/schema.json is not JSON: ', ParseErrorText(E));
      Continue;
    end;
    CompileOne(Area, 'extra/' + Names[I], 0, 'extra/' + Names[I], Doc, '', DefaultOptions);
  end;
end;

const
  DraftNames: array[TDialect] of UTF8String = ('draft4', 'draft6', 'draft7', 'draft2019-09', 'draft2020-12');
var
  Root, Tests, Dir, DraftFilter, Extra: UTF8String;
  D: TDialect;
  Files: TUTF8StringArray;
  I, Schemas, Compiled: Int32;
begin
  Failures := 0;
  Dumping := (ParamCount = 2) and (ParamStr(1) = '--dump');
  if Dumping then begin
    Assign(Dump, ParamStr(2));
    Rewrite(Dump);
  end;
  Root := UTF8String(GetEnvironmentVariable('JSON_SCHEMA_TEST_SUITE'));
  if Root = '' then
    Root := '../../JSON-Schema-Test-Suite';
  Tests := Root + '/tests';
  if not DirectoryExists(Tests) then begin
    WriteLn('JSON-Schema-Test-Suite not found at ', Root,
      ' (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)');
    Halt(1);
  end;
  Remotes := Default(TRemotes);
  Remotes.Root := Root + '/remotes';
  DraftFilter := UTF8String(GetEnvironmentVariable('SUITE_DRAFT'));
  Areas := nil;
  for D := Low(TDialect) to High(TDialect) do begin
    if (DraftFilter <> '') and (DraftFilter <> DraftNames[D]) then
      Continue;
    Dir := Tests + '/' + DraftNames[D];
    Files := JsonFiles(Dir);
    for I := 0 to Length(Files) - 1 do
      RunFile(D, Dir + '/' + Files[I], DraftNames[D] + '/' + Files[I], DraftNames[D], False);
    { The Go test does not run draft4/optional/zeroTerminatedFloats.json, whose cases differ by design. Its
      schemas are compiled here like any other. }
    Files := JsonFiles(Dir + '/optional');
    for I := 0 to Length(Files) - 1 do
      RunFile(D, Dir + '/optional/' + Files[I], DraftNames[D] + '/optional/' + Files[I],
        DraftNames[D] + '/optional', False);
    Files := JsonFiles(Dir + '/optional/format');
    for I := 0 to Length(Files) - 1 do
      RunFile(D, Dir + '/optional/format/' + Files[I], DraftNames[D] + '/optional/format/' + Files[I],
        DraftNames[D] + '/optional/format', True);
  end;
  if DraftFilter = '' then begin
    RunMetaschemas;
    Extra := UTF8String(GetEnvironmentVariable('CORVUS_EXTRA_SCHEMAS'));
    if Extra <> '' then
      RunExtra(Extra);
  end;
  if Dumping then
    Close(Dump);
  EcmaRegexReleaseThreadScratch;
  Schemas := 0;
  Compiled := 0;
  WriteLn('area                               compiled/schemas   nodes  dynamic  patterns (on the engine)');
  for I := 0 to Length(Areas) - 1 do begin
    WriteLn(Areas[I].Name, '': 34 - Length(Areas[I].Name), Areas[I].Compiled: 9, '/', Areas[I].Schemas: 4,
      Areas[I].Nodes: 11, Areas[I].Dynamic: 9, Areas[I].Patterns: 10, ' (', Areas[I].Engines, ')');
    Inc(Schemas, Areas[I].Schemas);
    Inc(Compiled, Areas[I].Compiled);
  end;
  WriteLn(Compiled, '/', Schemas, ' schemas compiled, ', Length(Remotes.Docs), ' remote documents read');
  if Schemas = 0 then begin
    WriteLn('no JSON-Schema-Test-Suite schemas were compiled');
    Halt(1);
  end;
  if Failures > 0 then
    Halt(1);
end.
