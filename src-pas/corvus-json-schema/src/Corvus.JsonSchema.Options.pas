unit Corvus.JsonSchema.Options;

{$I corvus.inc}

{ The options for compiling a schema. A port of options.go of the Go module.

  Go configures a compilation with functional options (WithDefaultDialect and the rest, each a function that changes
  the options). Here the options are one record, TCompileOptions, which DefaultOptions fills with the defaults, and
  each With function of the Go source is a procedure that changes the record it is given.

  Go's resolver and format validators are closures. Pascal as Delphi and Free Pascal share it has none, so a
  resolver is a plain function that is handed a context pointer the caller chose, and a format validator is a plain
  function over the bytes of the value. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Dialect;

type
  { TDocumentResolver resolves a schema document by absolute URI. It returns False if the document is unknown.
    Context is the ResolverContext of the options. }
  TDocumentResolver = function(Context: Pointer; const Uri: UTF8String; out Doc: TDocument): Boolean;

  { TFormatValidator is a custom format assertion over the string value (or the number's JSON text, for numbers):
    the bytes Text[Start .. Start+Len-1]. It has the shape of the built-in checks of the Formats unit. }
  TFormatValidator = function(const Text: TBytes; Start, Len: Int32): Boolean;

  { A custom format: its name and its assertion. }
  TNamedFormat = record
    Name: UTF8String;
    Validator: TFormatValidator;
  end;

  { TCompileOptions are the options for compiling a schema. }
  TCompileOptions = record
    DefaultDialect: TDialect;
    { Whether format is asserted: 1 always, -1 never, 0 as the vocabularies say. }
    AssertFormat: Int8;
    AssertFormatInLegacyDrafts: Boolean;
    AssertContent: Boolean;
    { The custom formats (a map by name in the Go source). There are few, so a name is found by reading the list. A
      name is in it once. }
    Formats: array of TNamedFormat;
    Resolver: TDocumentResolver;
    ResolverContext: Pointer;
    BaseURI: UTF8String;
    EntryPoint: UTF8String;
    HasEntryPoint: Boolean;
    MaxDepth: Int32;
  end;

const
  { ErrDepthExceeded reports that evaluation recursed in place beyond the maximum depth (a schema that loops
    without consuming the instance). }
  ErrDepthExceeded: UTF8String = 'the schema recursed in place beyond the maximum depth';

{ DefaultOptions are the options of a compilation that is given none. }
function DefaultOptions: TCompileOptions;

{ WithDefaultDialect sets the dialect of documents without $schema. The default is Draft202012. }
procedure WithDefaultDialect(var O: TCompileOptions; Dialect: TDialect);
{ WithAssertFormat says whether format is asserted: True always, False never. Without this option the vocabularies
  decide (the 2020-12 format-assertion vocabulary asserts, anything else annotates). }
procedure WithAssertFormat(var O: TCompileOptions; Assert: Boolean);
{ WithAssertFormatInLegacyDrafts also asserts format in drafts 4 to 7, when WithAssertFormat is not given. }
procedure WithAssertFormatInLegacyDrafts(var O: TCompileOptions; Assert: Boolean);
{ WithAssertContent says whether contentEncoding and contentMediaType are asserted in draft 7, the only draft that
  asserts them. The default is True. }
procedure WithAssertContent(var O: TCompileOptions; Assert: Boolean);
{ WithFormat adds a custom format assertion, which takes precedence over a built-in one of the same name. It
  receives the bytes of the string value, or of a number's JSON text. }
procedure WithFormat(var O: TCompileOptions; const Name: UTF8String; Validator: TFormatValidator);
{ WithDocumentResolver sets the function that resolves remote documents, and the context it is called with. The
  standard metaschemas are always available. }
procedure WithDocumentResolver(var O: TCompileOptions; Resolver: TDocumentResolver; Context: Pointer);
{ WithBaseURI sets the base URI of the root document. }
procedure WithBaseURI(var O: TCompileOptions; const Uri: UTF8String);
{ WithEntryPoint sets a reference, relative to the root, to evaluate from (for example #/$defs/item). The default
  is the root. }
procedure WithEntryPoint(var O: TCompileOptions; const Reference: UTF8String);
{ WithMaxDepth sets the maximum depth of in-place recursion on a cycle before evaluation is abandoned. The default
  is 128. Values below 1 are ignored. }
procedure WithMaxDepth(var O: TCompileOptions; Depth: Int32);

{ CustomFormat is the custom format assertion of a name, or nil when the options have none. }
function CustomFormat(const O: TCompileOptions; const Name: UTF8String): TFormatValidator;

implementation

function DefaultOptions: TCompileOptions;
begin
  Result.DefaultDialect := Draft202012;
  Result.AssertFormat := 0;
  Result.AssertFormatInLegacyDrafts := False;
  Result.AssertContent := True;
  Result.Formats := nil;
  Result.Resolver := nil;
  Result.ResolverContext := nil;
  Result.BaseURI := '';
  Result.EntryPoint := '';
  Result.HasEntryPoint := False;
  Result.MaxDepth := 128;
end;

procedure WithDefaultDialect(var O: TCompileOptions; Dialect: TDialect);
begin
  O.DefaultDialect := Dialect;
end;

procedure WithAssertFormat(var O: TCompileOptions; Assert: Boolean);
begin
  if Assert then
    O.AssertFormat := 1
  else
    O.AssertFormat := -1;
end;

procedure WithAssertFormatInLegacyDrafts(var O: TCompileOptions; Assert: Boolean);
begin
  O.AssertFormatInLegacyDrafts := Assert;
end;

procedure WithAssertContent(var O: TCompileOptions; Assert: Boolean);
begin
  O.AssertContent := Assert;
end;

procedure WithFormat(var O: TCompileOptions; const Name: UTF8String; Validator: TFormatValidator);
var
  I, N: Int32;
begin
  N := Length(O.Formats);
  for I := 0 to N - 1 do
    if O.Formats[I].Name = Name then begin
      O.Formats[I].Validator := Validator;
      Exit;
    end;
  { The options may be a copy of another record: the list is copied before it grows, so the other keeps its own. }
  O.Formats := Copy(O.Formats, 0, N);
  SetLength(O.Formats, N + 1);
  O.Formats[N].Name := Name;
  O.Formats[N].Validator := Validator;
end;

procedure WithDocumentResolver(var O: TCompileOptions; Resolver: TDocumentResolver; Context: Pointer);
begin
  O.Resolver := Resolver;
  O.ResolverContext := Context;
end;

procedure WithBaseURI(var O: TCompileOptions; const Uri: UTF8String);
begin
  O.BaseURI := Uri;
end;

procedure WithEntryPoint(var O: TCompileOptions; const Reference: UTF8String);
begin
  O.EntryPoint := Reference;
  O.HasEntryPoint := True;
end;

procedure WithMaxDepth(var O: TCompileOptions; Depth: Int32);
begin
  if Depth >= 1 then
    O.MaxDepth := Depth;
end;

function CustomFormat(const O: TCompileOptions; const Name: UTF8String): TFormatValidator;
var
  I: Int32;
begin
  Result := nil;
  for I := 0 to Length(O.Formats) - 1 do
    if O.Formats[I].Name = Name then begin
      Result := O.Formats[I].Validator;
      Exit;
    end;
end;

end.
