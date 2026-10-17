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
    { The time one match of a pattern or patternProperties regular expression against one string may take, in
      milliseconds. Zero is no limit. }
    PatternMatchLimit: Int64;
  end;

const
  { DefaultPatternMatchLimit is the time one match of a pattern or patternProperties regular expression against
    one string may take, in milliseconds, unless WithPatternMatchLimit says otherwise: one second. }
  DefaultPatternMatchLimit = 1000;
  { NoPatternMatchLimit, given to WithPatternMatchLimit, turns the limit on a match off. }
  NoPatternMatchLimit = 0;
  { PatternMatchStackLimit is the number of entries the backtracking of one match may hold when the match has a
    limit (16 bytes each). }
  PatternMatchStackLimit = 1048576;

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

{ WithPatternMatchLimit sets the time one match of a pattern or patternProperties regular expression against one
  string may take, in milliseconds. The default is DefaultPatternMatchLimit, one second. A match that takes longer
  is abandoned, and the validation it belongs to raises EJsonSchemaPatternMatchLimit (the Try forms return the
  reason). Zero (NoPatternMatchLimit) or less turns the limit off, for a caller who trusts both the schema and the
  instances. Without the limit a match also has no limit on its memory.

  The limit is on each match, not on the validation. Only the backtracking matcher can take long, and it reads the
  clock every 4,096 steps, so a match is abandoned within a millisecond or two of the limit. The matchers that take
  time proportional to the text are not stopped. }
procedure WithPatternMatchLimit(var O: TCompileOptions; Milliseconds: Int64);

{ PatternMatchLimitMessage is the message of a match that was abandoned: for its time when Memory is false, with
  the limit it had, and for the memory of its backtracking when Memory is true. }
function PatternMatchLimitMessage(const Pattern: UTF8String; LimitMs: Int64; Memory: Boolean): UTF8String;
{ ShownPattern quotes a pattern for a message, or its first 120 bytes and its length when it is longer. }
function ShownPattern(const Pattern: UTF8String): UTF8String;

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
  Result.PatternMatchLimit := DefaultPatternMatchLimit;
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

procedure WithPatternMatchLimit(var O: TCompileOptions; Milliseconds: Int64);
begin
  if Milliseconds < 0 then
    Milliseconds := 0;
  O.PatternMatchLimit := Milliseconds;
end;

function ShownPattern(const Pattern: UTF8String): UTF8String;
const
  Shown = 120;
var
  Cut: Int32;
begin
  if Length(Pattern) <= Shown then
    Exit('''' + Pattern + '''');
  { Not in the middle of a character: the byte after the cut is not a continuation byte. }
  Cut := Shown;
  while (Cut > 0) and ((Ord(Pattern[Cut + 1]) and $C0) = $80) do
    Dec(Cut);
  Result := '''' + Copy(Pattern, 1, Cut) + '...'' (' + UTF8String(IntToStr(Length(Pattern))) + ' bytes)';
end;

{ DurationText writes a number of milliseconds as Go writes a duration: 1s, 50ms, 1m0s, 1h0m0s, 1.5s. }
function DurationText(Ms: Int64): UTF8String;
var
  Seconds: UTF8String;
  Frac: Int64;
begin
  if Ms < 1000 then
    Exit(UTF8String(IntToStr(Ms)) + 'ms');
  Seconds := UTF8String(IntToStr((Ms div 1000) mod 60));
  Frac := Ms mod 1000;
  if Frac <> 0 then begin
    Seconds := Seconds + '.' + UTF8String(Format('%.3d', [Frac]));
    while Seconds[Length(Seconds)] = '0' do
      SetLength(Seconds, Length(Seconds) - 1);
  end;
  Result := Seconds + 's';
  if Ms >= 60000 then
    Result := UTF8String(IntToStr((Ms div 60000) mod 60)) + 'm' + Result;
  if Ms >= 3600000 then
    Result := UTF8String(IntToStr(Ms div 3600000)) + 'h' + Result;
end;

function PatternMatchLimitMessage(const Pattern: UTF8String; LimitMs: Int64; Memory: Boolean): UTF8String;
begin
  if Memory then
    Result := 'A match of the regular expression ' + ShownPattern(Pattern)
      + ' was abandoned: it needed more than the limit of 1,048,576 backtracking entries.'
  else
    Result := 'A match of the regular expression ' + ShownPattern(Pattern)
      + ' was abandoned: it took longer than the limit of ' + DurationText(LimitMs) + '.';
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
