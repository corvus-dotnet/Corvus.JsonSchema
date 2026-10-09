unit Corvus.JsonSchema.Loader;

{$I corvus.inc}

{ Loads schema documents, identifies resources and anchors, and resolves references. Elements are identified by
  (document, value index). A port of loader.go of the Go module.

  The Go source's maps become these:

    - the maps with string keys (resources and documents by URI, the dialect of each metaschema, a resource's
      anchors and dynamic anchors, and the cache of resolved references) are each a TStringTable, a hash table with
      open addressing over UTF8String keys, declared here;
    - the cache of resolved references, whose key in Go is the resource and the reference, is one table for each
      resource, keyed by the reference;
    - the resource that owns each visited value of a document, a map by value index in Go, is an array with an
      entry for every value of the document (-1 for a value that was not visited).

  A document or a resource is referred to by its index (an Int32), and the loader's functions take the loader. A
  function that fails returns False and leaves the reason in the loader's Error, where the Go source returns an
  error. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Dialect,
  Corvus.JsonSchema.Options;

const
  DefaultRootURI = 'https://corvus-oss.org/runtime-evaluator/root.json';

type
  { TStringTable is a map from a string to a number and a text: a hash table with open addressing. The entries are
    kept in the order they were added. }
  TStringTable = record
    Keys: TUTF8StringArray;
    Values: TInt32Array;
    Texts: TUTF8StringArray;
    Count: Int32;
    { The index + 1 of the entry in each slot (0: empty). The length is a power of two. }
    Slots: TInt32Array;
  end;

  TSchemaDocument = record
    Doc: TDocument;
    { The resource that owns each visited schema value, or -1. }
    ResourceOf: TInt32Array;
  end;

  TSchemaResource = record
    Document: Int32;
    RootPointer: UTF8String;
    Uri: UTF8String;
    Dialect: TDialect;
    Vocabularies: UInt32;
    RecursiveAnchor: Boolean;
    { The pointer of each anchor (the text of an entry), by its name. }
    Anchors: TStringTable;
    DynamicAnchors: TStringTable;
    { The references resolved from this resource: the index of the target in the loader's Targets, or -1 for a
      reference that does not resolve. }
    ReferenceCache: TStringTable;
  end;

  { TSchemaTarget is the target of a reference: a value in a document, and the resource it belongs to. }
  TSchemaTarget = record
    Document: Int32;
    Pointer: UTF8String;
    Value: Int32;
    Resource: Int32;
  end;
  TSchemaTargetArray = array of TSchemaTarget;

  TDialectInfo = record
    Dialect: TDialect;
    Vocabularies: UInt32;
  end;

  TSchemaLoader = record
    Documents: array of TSchemaDocument;
    Resources: array of TSchemaResource;
    { The index of a resource, by its URI. }
    ResourcesByURI: TStringTable;
    { The index of a document, by its URI. }
    DocumentsByURI: TStringTable;
    { The index in MetaschemaInfos of a metaschema's dialect and vocabularies, by its URI. }
    MetaschemaInfo: TStringTable;
    MetaschemaInfos: array of TDialectInfo;
    MetaschemaLoading: TUTF8StringArray;
    MetaschemaLoadingLen: Int32;
    { The targets of the resolved references (see TSchemaResource.ReferenceCache). }
    Targets: TSchemaTargetArray;
    TargetsLen: Int32;
    Options: TCompileOptions;
    { Why the function that has just returned False failed. }
    Error: UTF8String;
  end;

{ The string table. }

{ StringTableFind is the index of the entry with a key, or -1. }
function StringTableFind(const T: TStringTable; const Key: UTF8String): Int32;
{ StringTableAdd adds an entry for a key that has none, and returns its index. }
function StringTableAdd(var T: TStringTable; const Key: UTF8String; Value: Int32; const Text: UTF8String): Int32;

{ Reading schema values. }

{ StringValue is the text of a string value, if N is one. }
function StringValue(const D: TDocument; N: Int32; out S: UTF8String): Boolean;
{ Member is the value of a property of N, or -1 when N is not an object or has no such property. }
function Member(const D: TDocument; N: Int32; const Name: UTF8String): Int32;
function IsKind(const D: TDocument; N: Int32; Kind: Byte): Boolean;
function IsSchemaValue(const D: TDocument; N: Int32): Boolean;
{ IntText is a number as decimal text. }
function IntText(V: Int64): UTF8String;

{ The loader. }

function NewSchemaLoader(const Options: TCompileOptions): TSchemaLoader;
function LoaderRootResource(const L: TSchemaLoader; Doc: Int32): Int32;
function LoadRoot(var L: TSchemaLoader; const Schema: TDocument; out Resource: Int32): Boolean;
function LoadRootFromURI(var L: TSchemaLoader; const Uri: UTF8String; out Resource: Int32): Boolean;
{ ResourceRoot is the root value of a resource. }
function ResourceRoot(const L: TSchemaLoader; Resource: Int32): Int32;
function RootTarget(const L: TSchemaLoader; Resource: Int32): TSchemaTarget;
{ LoaderResourceOf is the resource that owns a value of a document. False when the value was not visited. }
function LoaderResourceOf(const L: TSchemaLoader; Document, Value: Int32; out Resource: Int32): Boolean;
{ TryResolveReference resolves a reference from a resource. Found is False when it does not resolve. The result is
  False when a document the reference needs could not be loaded. }
function TryResolveReference(var L: TSchemaLoader; From: Int32; const Reference: UTF8String;
  out Target: TSchemaTarget; out Found: Boolean): Boolean;
{ TryResolveFragment is the target of a fragment (already percent-decoded) within a resource. False when there is
  none. }
function TryResolveFragment(const L: TSchemaLoader; Resource: Int32; const Fragment: UTF8String;
  out Target: TSchemaTarget): Boolean;

implementation

uses
  Corvus.JsonSchema.Uri,
  Corvus.JsonSchema.Metaschemas;

{ --------------------------------------------------------------------------------------------------------------------
  The string table }

{ KeyHash hashes a key (FNV-1a). The product wraps, as a hash's does. }
function KeyHash(const Key: UTF8String): UInt64;
const
  Prime = UInt64($100000001B3);
var
  K: Int32;
begin
  Result := UInt64($CBF29CE484222325);
  for K := 1 to Length(Key) do
    Result := (Result xor UInt64(Byte(Key[K]))) * Prime;
end;

function HomeSlot(const Key: UTF8String; Size: Int32): Int32; inline;
begin
  Result := Int32((KeyHash(Key) shr 32) and UInt64(Size - 1));
end;

function StringTableFind(const T: TStringTable; const Key: UTF8String): Int32;
var
  Size, Slot, At: Int32;
begin
  Result := -1;
  Size := Length(T.Slots);
  if Size = 0 then
    Exit;
  Slot := HomeSlot(Key, Size);
  while True do begin
    At := T.Slots[Slot];
    if At = 0 then
      Exit;
    if T.Keys[At - 1] = Key then begin
      Result := At - 1;
      Exit;
    end;
    Slot := (Slot + 1) and (Size - 1);
  end;
end;

{ Place puts entry I in the first free slot from its own. }
procedure Place(var T: TStringTable; I: Int32);
var
  Size, Slot: Int32;
begin
  Size := Length(T.Slots);
  Slot := HomeSlot(T.Keys[I], Size);
  while T.Slots[Slot] <> 0 do
    Slot := (Slot + 1) and (Size - 1);
  T.Slots[Slot] := I + 1;
end;

function StringTableAdd(var T: TStringTable; const Key: UTF8String; Value: Int32; const Text: UTF8String): Int32;
var
  I, Size: Int32;
begin
  if T.Count = Length(T.Keys) then begin
    SetLength(T.Keys, T.Count * 2 + 4);
    SetLength(T.Values, T.Count * 2 + 4);
    SetLength(T.Texts, T.Count * 2 + 4);
  end;
  Result := T.Count;
  T.Keys[Result] := Key;
  T.Values[Result] := Value;
  T.Texts[Result] := Text;
  Inc(T.Count);
  { At least two slots for each entry. }
  if T.Count * 2 > Length(T.Slots) then begin
    Size := 8;
    while Size < T.Count * 4 do
      Size := Size shl 1;
    T.Slots := nil;
    SetLength(T.Slots, Size);
    for I := 0 to Size - 1 do
      T.Slots[I] := 0;
    for I := 0 to T.Count - 1 do
      Place(T, I);
  end else
    Place(T, Result);
end;

{ --------------------------------------------------------------------------------------------------------------------
  Reading schema values }

function StringValue(const D: TDocument; N: Int32; out S: UTF8String): Boolean;
begin
  if (N < 0) or (DocKind(D, N) <> KindString) then begin
    S := '';
    Result := False;
  end else begin
    S := DocStrCopy(D, N);
    Result := True;
  end;
end;

function Member(const D: TDocument; N: Int32; const Name: UTF8String): Int32;
begin
  if (N < 0) or (DocKind(D, N) <> KindObject) then
    Result := -1
  else
    Result := DocProperty(D, N, Name);
end;

function IsKind(const D: TDocument; N: Int32; Kind: Byte): Boolean;
begin
  Result := (N >= 0) and (DocKind(D, N) = Kind);
end;

function IsSchemaValue(const D: TDocument; N: Int32): Boolean;
var
  K: Byte;
begin
  K := DocKind(D, N);
  Result := (K = KindBool) or (K = KindObject);
end;

function IntText(V: Int64): UTF8String;
begin
  Result := '';
  Str(V, Result);
end;

{ --------------------------------------------------------------------------------------------------------------------
  The loader }

function AddDocument(var L: TSchemaLoader; const Uri: UTF8String; const Doc: TDocument; out Id: Int32): Boolean;
  forward;
function TryLoadDocument(var L: TSchemaLoader; const AbsoluteURI: UTF8String; out Loaded: Boolean): Boolean; forward;

function Fail(var L: TSchemaLoader; const Message: UTF8String): Boolean;
begin
  L.Error := Message;
  Result := False;
end;

function NewSchemaLoader(const Options: TCompileOptions): TSchemaLoader;
begin
  Result := Default(TSchemaLoader);
  Result.Options := Options;
end;

function LoaderRootResource(const L: TSchemaLoader; Doc: Int32): Int32;
begin
  Result := L.Documents[Doc].ResourceOf[L.Documents[Doc].Doc.Root];
end;

function LoadRoot(var L: TSchemaLoader; const Schema: TDocument; out Resource: Int32): Boolean;
var
  Uri: UTF8String;
  Doc: Int32;
begin
  Resource := 0;
  Uri := DefaultRootURI;
  if L.Options.BaseURI <> '' then
    Uri := NormalizeURI(L.Options.BaseURI);
  Result := AddDocument(L, Uri, Schema, Doc);
  if Result then
    Resource := LoaderRootResource(L, Doc);
end;

function LoadRootFromURI(var L: TSchemaLoader; const Uri: UTF8String; out Resource: Int32): Boolean;
var
  Normalized: UTF8String;
  Ok: Boolean;
begin
  Resource := 0;
  Normalized := NormalizeURI(Uri);
  Result := TryLoadDocument(L, Normalized, Ok);
  if not Result then
    Exit;
  if not Ok then begin
    Result := Fail(L, 'Unable to resolve the schema document ''' + Uri + '''.');
    Exit;
  end;
  Resource := LoaderRootResource(L, L.DocumentsByURI.Values[StringTableFind(L.DocumentsByURI, Normalized)]);
end;

function ResourceRoot(const L: TSchemaLoader; Resource: Int32): Int32;
var
  Doc: Int32;
  Path: UTF8String;
begin
  Doc := L.Resources[Resource].Document;
  Result := ResolvePointer(L.Documents[Doc].Doc, L.Documents[Doc].Doc.Root, L.Resources[Resource].RootPointer,
    Path);
  if Result < 0 then
    Result := L.Documents[Doc].Doc.Root;
end;

function RootTarget(const L: TSchemaLoader; Resource: Int32): TSchemaTarget;
begin
  Result.Document := L.Resources[Resource].Document;
  Result.Pointer := L.Resources[Resource].RootPointer;
  Result.Value := ResourceRoot(L, Resource);
  Result.Resource := Resource;
end;

function LoaderResourceOf(const L: TSchemaLoader; Document, Value: Int32; out Resource: Int32): Boolean;
begin
  Resource := L.Documents[Document].ResourceOf[Value];
  Result := Resource >= 0;
end;

function ResolveReference(var L: TSchemaLoader; From: Int32; const Reference: UTF8String; out Target: TSchemaTarget;
  out Found: Boolean): Boolean;
var
  UriPart, Fragment, Absolute: UTF8String;
  At: Int32;
  Loaded: Boolean;
begin
  Target := Default(TSchemaTarget);
  Found := False;
  Result := True;
  SplitFragment(Reference, UriPart, Fragment);
  Absolute := ResolveURI(L.Resources[From].Uri, UriPart);
  At := StringTableFind(L.ResourcesByURI, Absolute);
  if At < 0 then begin
    Result := TryLoadDocument(L, Absolute, Loaded);
    if not Result or not Loaded then
      Exit;
    At := StringTableFind(L.ResourcesByURI, Absolute);
    if At < 0 then
      Exit;
  end;
  Found := TryResolveFragment(L, L.ResourcesByURI.Values[At], DecodeFragment(Fragment), Target);
end;

function TryResolveReference(var L: TSchemaLoader; From: Int32; const Reference: UTF8String;
  out Target: TSchemaTarget; out Found: Boolean): Boolean;
var
  At, Index: Int32;
begin
  At := StringTableFind(L.Resources[From].ReferenceCache, Reference);
  if At >= 0 then begin
    Index := L.Resources[From].ReferenceCache.Values[At];
    Found := Index >= 0;
    if Found then
      Target := L.Targets[Index]
    else
      Target := Default(TSchemaTarget);
    Result := True;
    Exit;
  end;
  Result := ResolveReference(L, From, Reference, Target, Found);
  if not Result then
    Exit;
  Index := -1;
  if Found then begin
    if L.TargetsLen = Length(L.Targets) then
      SetLength(L.Targets, L.TargetsLen * 2 + 16);
    Index := L.TargetsLen;
    L.Targets[Index] := Target;
    Inc(L.TargetsLen);
  end;
  StringTableAdd(L.Resources[From].ReferenceCache, Reference, Index, '');
end;

function TryResolveFragment(const L: TSchemaLoader; Resource: Int32; const Fragment: UTF8String;
  out Target: TSchemaTarget): Boolean;
var
  Doc, Value, Owner, At: Int32;
  Path, Anchor: UTF8String;
begin
  if Fragment = '' then begin
    Target := RootTarget(L, Resource);
    Result := True;
    Exit;
  end;
  Target := Default(TSchemaTarget);
  Result := False;
  Doc := L.Resources[Resource].Document;
  if Fragment[1] = '/' then begin
    Value := ResolvePointer(L.Documents[Doc].Doc, ResourceRoot(L, Resource), Fragment, Path);
    if Value < 0 then
      Exit;
    if not LoaderResourceOf(L, Doc, Value, Owner) then
      Owner := Resource;
    Target.Document := Doc;
    Target.Pointer := L.Resources[Resource].RootPointer + Path;
    Target.Value := Value;
    Target.Resource := Owner;
    Result := True;
    Exit;
  end;
  At := StringTableFind(L.Resources[Resource].Anchors, Fragment);
  if At < 0 then
    Exit;
  Anchor := L.Resources[Resource].Anchors.Texts[At];
  Value := ResolvePointer(L.Documents[Doc].Doc, L.Documents[Doc].Doc.Root, Anchor, Path);
  if Value < 0 then
    Exit;
  Target.Document := Doc;
  Target.Pointer := Anchor;
  Target.Value := Value;
  Target.Resource := Resource;
  Result := True;
end;

function DefaultDialectInfo(const L: TSchemaLoader): TDialectInfo;
begin
  Result.Dialect := L.Options.DefaultDialect;
  Result.Vocabularies := VocabAllAnnotating;
end;

function LoadMetaschemaInfo(var L: TSchemaLoader; const Normalized: UTF8String; out Info: TDialectInfo): Boolean;
var
  At, MetaResource, V, K, I: Int32;
  Loaded: Boolean;
  Vocabularies: UInt32;
  D: TDocument;
begin
  Info := DefaultDialectInfo(L);
  Result := True;
  At := StringTableFind(L.ResourcesByURI, Normalized);
  if At < 0 then begin
    Result := TryLoadDocument(L, Normalized, Loaded);
    if not Result or not Loaded then
      Exit;
    At := StringTableFind(L.ResourcesByURI, Normalized);
    if At < 0 then
      Exit;
  end;
  MetaResource := L.ResourcesByURI.Values[At];
  Vocabularies := VocabAllAnnotating;
  D := L.Documents[L.Resources[MetaResource].Document].Doc;
  V := Member(D, ResourceRoot(L, MetaResource), '$vocabulary');
  if IsKind(D, V, KindObject) then begin
    Vocabularies := VocabCore;
    K := DocFirst(D, V);
    for I := 0 to DocCount(D, V) - 1 do
      Vocabularies := Vocabularies or VocabularyFlag(DocStrCopy(D, K + 2 * I));
  end;
  Info.Dialect := L.Resources[MetaResource].Dialect;
  Info.Vocabularies := Vocabularies;
end;

function GetDialectInfo(var L: TSchemaLoader; const SchemaURI: UTF8String; out Info: TDialectInfo): Boolean;
var
  Plain, Normalized: UTF8String;
  N, At, I: Int32;
  Known: TDialect;
begin
  Result := True;
  Info.Vocabularies := VocabAllAnnotating;
  Info.Dialect := L.Options.DefaultDialect;
  { The standard metaschema URIs, as usually written, need no URI normalisation. }
  Plain := SchemaURI;
  N := Length(Plain);
  if (N > 0) and (Plain[N] = '#') then
    Plain := Copy(Plain, 1, N - 1);
  if KnownDialect(Plain, Known) then begin
    Info.Dialect := Known;
    Exit;
  end;
  Normalized := NormalizeURI(SchemaURI);
  if KnownDialect(Normalized, Known) then begin
    Info.Dialect := Known;
    Exit;
  end;
  At := StringTableFind(L.MetaschemaInfo, Normalized);
  if At >= 0 then begin
    Info := L.MetaschemaInfos[L.MetaschemaInfo.Values[At]];
    Exit;
  end;
  for I := 0 to L.MetaschemaLoadingLen - 1 do
    if L.MetaschemaLoading[I] = Normalized then begin
      Info := DefaultDialectInfo(L);
      Exit;
    end;
  if L.MetaschemaLoadingLen = Length(L.MetaschemaLoading) then
    SetLength(L.MetaschemaLoading, L.MetaschemaLoadingLen * 2 + 4);
  L.MetaschemaLoading[L.MetaschemaLoadingLen] := Normalized;
  Inc(L.MetaschemaLoadingLen);
  Result := LoadMetaschemaInfo(L, Normalized, Info);
  Dec(L.MetaschemaLoadingLen);
  if not Result then
    Exit;
  N := Length(L.MetaschemaInfos);
  SetLength(L.MetaschemaInfos, N + 1);
  L.MetaschemaInfos[N] := Info;
  StringTableAdd(L.MetaschemaInfo, Normalized, N, '');
end;

function TryLoadDocument(var L: TSchemaLoader; const AbsoluteURI: UTF8String; out Loaded: Boolean): Boolean;
var
  Doc: TDocument;
  Text: TBytes;
  Error: TParseError;
  Id: Int32;
begin
  Result := True;
  Loaded := True;
  if StringTableFind(L.DocumentsByURI, AbsoluteURI) >= 0 then
    Exit;
  if Assigned(L.Options.Resolver) then
    if L.Options.Resolver(L.Options.ResolverContext, AbsoluteURI, Doc) then begin
      Result := AddDocument(L, AbsoluteURI, Doc, Id);
      Loaded := Result;
      Exit;
    end;
  if Metaschema(AbsoluteURI, Text) then begin
    if not ParseDocument(Text, Doc, Error) then begin
      Loaded := False;
      Result := Fail(L, 'The embedded metaschema ''' + AbsoluteURI + ''' is not valid JSON.');
      Exit;
    end;
    Result := AddDocument(L, AbsoluteURI, Doc, Id);
    Loaded := Result;
    Exit;
  end;
  Loaded := False;
end;

function CreateResource(var L: TSchemaLoader; Document: Int32; const Pointer, Uri: UTF8String;
  const Info: TDialectInfo): Int32;
begin
  Result := Length(L.Resources);
  SetLength(L.Resources, Result + 1);
  L.Resources[Result] := Default(TSchemaResource);
  L.Resources[Result].Document := Document;
  L.Resources[Result].RootPointer := Pointer;
  L.Resources[Result].Uri := Uri;
  L.Resources[Result].Dialect := Info.Dialect;
  L.Resources[Result].Vocabularies := Info.Vocabularies;
  if StringTableFind(L.ResourcesByURI, Uri) < 0 then
    StringTableAdd(L.ResourcesByURI, Uri, Result, '');
end;

procedure AddAnchor(var L: TSchemaLoader; Resource: Int32; const Name, Pointer: UTF8String);
begin
  if StringTableFind(L.Resources[Resource].Anchors, Name) < 0 then
    StringTableAdd(L.Resources[Resource].Anchors, Name, 0, Pointer);
end;

function Walk(var L: TSchemaLoader; Doc, Element: Int32; const Pointer: UTF8String; Resource: Int32;
  IsResourceRoot: Boolean): Boolean;
var
  D: TDocument;
  Info: TDialectInfo;
  Dialect: TDialect;
  LegacyRefOverridesSiblings: Boolean;
  S, IdName, Id, UriPart, Fragment, Absolute, Name, Base: UTF8String;
  V, K, I, C, J, Value, Entry: Int32;
  Kind: TSubschemaKind;
begin
  Result := True;
  { A copy of the document's record, which keeps its arrays while the loader's list of documents grows. }
  D := L.Documents[Doc].Doc;
  if DocKind(D, Element) <> KindObject then begin
    L.Documents[Doc].ResourceOf[Element] := Resource;
    Exit;
  end;
  Info.Dialect := L.Resources[Resource].Dialect;
  Info.Vocabularies := L.Resources[Resource].Vocabularies;
  if not IsResourceRoot then
    if StringValue(D, DocProperty(D, Element, '$schema'), S) then
      if not GetDialectInfo(L, S, Info) then begin
        Result := False;
        Exit;
      end;
  Dialect := Info.Dialect;

  LegacyRefOverridesSiblings := DialectIsLegacy(Dialect) and IsKind(D, DocProperty(D, Element, '$ref'), KindString);
  if not LegacyRefOverridesSiblings then begin
    IdName := '$id';
    if Dialect = Draft4 then
      IdName := 'id';
    if StringValue(D, DocProperty(D, Element, IdName), Id) then begin
      SplitFragment(Id, UriPart, Fragment);
      if UriPart = '' then begin
        if (Fragment <> '') and DialectIsLegacy(Dialect) then
          AddAnchor(L, Resource, Fragment, Pointer);
      end else begin
        Absolute := ResolveURI(L.Resources[Resource].Uri, UriPart);
        if not IsResourceRoot or (Absolute <> L.Resources[Resource].Uri) then begin
          if IsResourceRoot then begin
            if StringTableFind(L.ResourcesByURI, Absolute) < 0 then
              StringTableAdd(L.ResourcesByURI, Absolute, Resource, '');
            L.Resources[Resource].Uri := Absolute;
          end else begin
            Resource := CreateResource(L, Doc, Pointer, Absolute, Info);
            IsResourceRoot := True;
          end;
        end;
        if (Fragment <> '') and DialectIsLegacy(Dialect) then
          AddAnchor(L, Resource, Fragment, Pointer);
      end;
    end;
    if Dialect >= Draft201909 then
      if StringValue(D, DocProperty(D, Element, '$anchor'), S) then
        AddAnchor(L, Resource, S, Pointer);
    if Dialect >= Draft202012 then
      if StringValue(D, DocProperty(D, Element, '$dynamicAnchor'), S) then begin
        if StringTableFind(L.Resources[Resource].DynamicAnchors, S) < 0 then
          StringTableAdd(L.Resources[Resource].DynamicAnchors, S, 0, Pointer);
        AddAnchor(L, Resource, S, Pointer);
      end;
    if (Dialect = Draft201909) and IsResourceRoot then begin
      V := DocProperty(D, Element, '$recursiveAnchor');
      if IsKind(D, V, KindBool) and DocBoolean(D, V) then
        L.Resources[Resource].RecursiveAnchor := True;
    end;
  end;

  L.Documents[Doc].ResourceOf[Element] := Resource;

  Result := False;
  K := DocFirst(D, Element);
  for I := 0 to DocCount(D, Element) - 1 do begin
    Name := DocStrCopy(D, K + 2 * I);
    Value := K + 2 * I + 1;
    Kind := SubschemaKindOf(Name, Dialect, LegacyRefOverridesSiblings);
    if Kind = SubschemaNone then
      Continue;
    Base := Pointer + '/' + EscapePointerToken(Name);
    case Kind of
      SubschemaSingle:
        if IsSchemaValue(D, Value) then
          if not Walk(L, Doc, Value, Base, Resource, False) then
            Exit;
      SubschemaSingleOrArray, SubschemaArray:
        if DocKind(D, Value) = KindArray then begin
          C := DocFirst(D, Value);
          for J := 0 to DocCount(D, Value) - 1 do
            if not Walk(L, Doc, C + J, Base + '/' + IntText(J), Resource, False) then
              Exit;
        end else if (Kind = SubschemaSingleOrArray) and IsSchemaValue(D, Value) then
          if not Walk(L, Doc, Value, Base, Resource, False) then
            Exit;
      SubschemaMap:
        if DocKind(D, Value) = KindObject then begin
          C := DocFirst(D, Value);
          for J := 0 to DocCount(D, Value) - 1 do begin
            Entry := C + 2 * J;
            if IsSchemaValue(D, Entry + 1) then
              if not Walk(L, Doc, Entry + 1, Base + '/' + EscapePointerToken(DocStrCopy(D, Entry)), Resource,
                False) then
                Exit;
          end;
        end;
    end;
  end;
  Result := True;
end;

function AddDocument(var L: TSchemaLoader; const Uri: UTF8String; const Doc: TDocument; out Id: Int32): Boolean;
var
  Info: TDialectInfo;
  S: UTF8String;
  Resource, I, At: Int32;
begin
  Id := Length(L.Documents);
  SetLength(L.Documents, Id + 1);
  L.Documents[Id].Doc := Doc;
  L.Documents[Id].ResourceOf := nil;
  SetLength(L.Documents[Id].ResourceOf, Length(Doc.Tape) div 2);
  for I := 0 to Length(L.Documents[Id].ResourceOf) - 1 do
    L.Documents[Id].ResourceOf[I] := -1;
  { The latest document of a URI is the one the URI names, as an assignment to a map makes it. }
  At := StringTableFind(L.DocumentsByURI, Uri);
  if At >= 0 then
    L.DocumentsByURI.Values[At] := Id
  else
    StringTableAdd(L.DocumentsByURI, Uri, Id, '');
  Info := DefaultDialectInfo(L);
  if StringValue(Doc, Member(Doc, Doc.Root, '$schema'), S) then
    if not GetDialectInfo(L, S, Info) then begin
      Result := False;
      Exit;
    end;
  Resource := CreateResource(L, Id, '', Uri, Info);
  Result := Walk(L, Id, Doc.Root, '', Resource, True);
end;

end.
