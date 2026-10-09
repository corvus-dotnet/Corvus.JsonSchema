unit Corvus.JsonSchema.Dialect;

{$I corvus.inc}

{ The JSON Schema dialects the evaluator understands, their vocabularies, and which keywords hold subschemas in
  each. A port of dialect.go of the Go module. }

interface

type
  { TDialect is a JSON Schema dialect the evaluator understands. The values are in specification order. }
  TDialect = (Draft4, Draft6, Draft7, Draft201909, Draft202012);

  { How a keyword holds subschemas. }
  TSubschemaKind = (SubschemaNone, SubschemaSingle, SubschemaSingleOrArray, SubschemaArray, SubschemaMap);

const
  { The vocabularies in effect for a schema resource (2019-09 and later), as bits. }
  VocabNone = UInt32(0);
  VocabCore = UInt32(1 shl 0);
  VocabApplicator = UInt32(1 shl 1);
  VocabValidation = UInt32(1 shl 2);
  VocabMetaData = UInt32(1 shl 3);
  VocabFormatAnnotation = UInt32(1 shl 4);
  VocabFormatAssertion = UInt32(1 shl 5);
  VocabContent = UInt32(1 shl 6);
  VocabUnevaluated = UInt32(1 shl 7);
  VocabAllAnnotating = UInt32(VocabCore or VocabApplicator or VocabValidation or VocabMetaData
    or VocabFormatAnnotation or VocabContent or VocabUnevaluated);

{ DialectName returns the dialect's usual name (the String method of the Go source). }
function DialectName(D: TDialect): UTF8String;
{ DialectIsLegacy reports draft 7 and earlier, where $ref replaces its siblings and there are no vocabularies. }
function DialectIsLegacy(D: TDialect): Boolean; inline;
{ KnownDialect maps a well-known metaschema URI (normalised, no fragment) to its dialect. }
function KnownDialect(const Uri: UTF8String; out Dialect: TDialect): Boolean;
{ VocabularyFlag maps a vocabulary URI to its bit. }
function VocabularyFlag(const Uri: UTF8String): UInt32;
{ SubschemaKindOf says which keywords hold subschemas, by dialect. }
function SubschemaKindOf(const Keyword: UTF8String; Dialect: TDialect;
  LegacyRefOverridesSiblings: Boolean): TSubschemaKind;

implementation

function DialectName(D: TDialect): UTF8String;
begin
  case D of
    Draft4: Result := 'draft4';
    Draft6: Result := 'draft6';
    Draft7: Result := 'draft7';
    Draft201909: Result := 'draft2019-09';
    Draft202012: Result := 'draft2020-12';
  else
    Result := 'unknown';
  end;
end;

function DialectIsLegacy(D: TDialect): Boolean; inline;
begin
  Result := D <= Draft7;
end;

function KnownDialect(const Uri: UTF8String; out Dialect: TDialect): Boolean;
begin
  Result := True;
  if Uri = 'http://json-schema.org/draft-04/schema' then
    Dialect := Draft4
  else if Uri = 'http://json-schema.org/draft-06/schema' then
    Dialect := Draft6
  else if Uri = 'http://json-schema.org/draft-07/schema' then
    Dialect := Draft7
  else if Uri = 'https://json-schema.org/draft/2019-09/schema' then
    Dialect := Draft201909
  else if Uri = 'https://json-schema.org/draft/2020-12/schema' then
    Dialect := Draft202012
  else begin
    Dialect := Draft4;
    Result := False;
  end;
end;

function VocabularyFlag(const Uri: UTF8String): UInt32;
begin
  if (Uri = 'https://json-schema.org/draft/2020-12/vocab/core')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/core') then
    Result := VocabCore
  else if (Uri = 'https://json-schema.org/draft/2020-12/vocab/applicator')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/applicator') then
    Result := VocabApplicator
  else if (Uri = 'https://json-schema.org/draft/2020-12/vocab/validation')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/validation') then
    Result := VocabValidation
  else if (Uri = 'https://json-schema.org/draft/2020-12/vocab/meta-data')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/meta-data') then
    Result := VocabMetaData
  else if (Uri = 'https://json-schema.org/draft/2020-12/vocab/format-annotation')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/format') then
    Result := VocabFormatAnnotation
  else if Uri = 'https://json-schema.org/draft/2020-12/vocab/format-assertion' then
    Result := VocabFormatAssertion
  else if (Uri = 'https://json-schema.org/draft/2020-12/vocab/content')
    or (Uri = 'https://json-schema.org/draft/2019-09/vocab/content') then
    Result := VocabContent
  else if Uri = 'https://json-schema.org/draft/2020-12/vocab/unevaluated' then
    Result := VocabUnevaluated
  else
    Result := VocabNone;
end;

function SubschemaKindOf(const Keyword: UTF8String; Dialect: TDialect;
  LegacyRefOverridesSiblings: Boolean): TSubschemaKind;
begin
  Result := SubschemaNone;
  if (Keyword = 'definitions') or (Keyword = '$defs') then begin
    Result := SubschemaMap;
    Exit;
  end;
  if LegacyRefOverridesSiblings then
    Exit;
  if (Keyword = 'properties') or (Keyword = 'patternProperties') or (Keyword = 'dependencies') then
    Result := SubschemaMap
  else if (Keyword = 'additionalProperties') or (Keyword = 'not') then
    Result := SubschemaSingle
  else if (Keyword = 'allOf') or (Keyword = 'anyOf') or (Keyword = 'oneOf') then
    Result := SubschemaArray
  else if Keyword = 'items' then begin
    if Dialect >= Draft202012 then
      Result := SubschemaSingle
    else
      Result := SubschemaSingleOrArray;
  end else if Keyword = 'additionalItems' then begin
    if Dialect <= Draft201909 then
      Result := SubschemaSingle;
  end else if (Keyword = 'contains') or (Keyword = 'propertyNames') then begin
    if Dialect >= Draft6 then
      Result := SubschemaSingle;
  end else if (Keyword = 'if') or (Keyword = 'then') or (Keyword = 'else') then begin
    if Dialect >= Draft7 then
      Result := SubschemaSingle;
  end else if (Keyword = 'unevaluatedProperties') or (Keyword = 'unevaluatedItems')
    or (Keyword = 'contentSchema') then begin
    if Dialect >= Draft201909 then
      Result := SubschemaSingle;
  end else if Keyword = 'dependentSchemas' then begin
    if Dialect >= Draft201909 then
      Result := SubschemaMap;
  end else if Keyword = 'prefixItems' then begin
    if Dialect >= Draft202012 then
      Result := SubschemaArray;
  end;
end;

end.
