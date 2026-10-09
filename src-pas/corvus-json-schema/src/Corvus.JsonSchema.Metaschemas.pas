unit Corvus.JsonSchema.Metaschemas;

{$I corvus.inc}

{ The standard metaschemas, copied from src/Corvus.Text.Json/metaschema by way of the Go module (a test keeps the
  copy current). A port of metaschemas.go of the Go module.

  Go embeds the files of its metaschemas directory. Here they are constants in an include file that
  tools/gen-metaschemas.ps1 writes from that directory. }

interface

uses
  SysUtils;

{ Metaschema is the text of a standard metaschema, by its canonical URI (no trailing empty fragment). }
function Metaschema(const Uri: UTF8String; out Text: TBytes): Boolean;
{ MetaschemaFile is the file of a standard metaschema within the metaschemas directory, by its canonical URI. It
  says nothing of whether there is such a file. }
function MetaschemaFile(const Uri: UTF8String; out FileName: UTF8String): Boolean;

{ The embedded files, for the tests: how many there are, the path of each within the metaschemas directory, and a
  copy of its bytes. }
function EmbeddedMetaschemaCount: Int32;
function EmbeddedMetaschemaName(Index: Int32): UTF8String;
function EmbeddedMetaschemaText(Index: Int32): TBytes;

implementation

{$I Corvus.JsonSchema.MetaschemaFiles.inc}

function EmbeddedMetaschemaCount: Int32;
begin
  Result := MetaschemaFileCount;
end;

function EmbeddedMetaschemaName(Index: Int32): UTF8String;
begin
  Result := MetaschemaFileNames[Index];
end;

function EmbeddedMetaschemaText(Index: Int32): TBytes;
begin
  Result := MetaschemaFileText(Index);
end;

function Metaschema(const Uri: UTF8String; out Text: TBytes): Boolean;
var
  FileName: UTF8String;
  Lo, Hi, Mid: Int32;
begin
  Text := nil;
  Result := False;
  if not MetaschemaFile(Uri, FileName) then
    Exit;
  { The names are in order: the file is found by halving. }
  Lo := 0;
  Hi := MetaschemaFileCount - 1;
  while Lo <= Hi do begin
    Mid := Lo + (Hi - Lo) div 2;
    if MetaschemaFileNames[Mid] = FileName then begin
      Text := MetaschemaFileText(Mid);
      Result := True;
      Exit;
    end;
    if MetaschemaFileNames[Mid] < FileName then
      Lo := Mid + 1
    else
      Hi := Mid - 1;
  end;
end;

function MetaschemaFile(const Uri: UTF8String; out FileName: UTF8String): Boolean;
const
  Prefix: UTF8String = 'https://json-schema.org/draft/';
var
  Rest, Draft, Name, Vocabulary: UTF8String;
  Slash, K: Int32;
begin
  Result := True;
  if Uri = 'http://json-schema.org/draft-04/schema' then
    FileName := 'draft4/schema.json'
  else if Uri = 'http://json-schema.org/draft-06/schema' then
    FileName := 'draft6/schema.json'
  else if Uri = 'http://json-schema.org/draft-07/schema' then
    FileName := 'draft7/schema.json'
  else begin
    FileName := '';
    Result := False;
    if (Length(Uri) < Length(Prefix)) or (Copy(Uri, 1, Length(Prefix)) <> Prefix) then
      Exit;
    Rest := Copy(Uri, Length(Prefix) + 1, Length(Uri) - Length(Prefix));
    Slash := 0;
    for K := 1 to Length(Rest) do
      if Rest[K] = '/' then begin
        Slash := K;
        Break;
      end;
    if Slash = 0 then begin
      Draft := Rest;
      Name := '';
    end else begin
      Draft := Copy(Rest, 1, Slash - 1);
      Name := Copy(Rest, Slash + 1, Length(Rest) - Slash);
    end;
    if (Draft <> '2019-09') and (Draft <> '2020-12') then
      Exit;
    if Name = 'schema' then begin
      FileName := 'draft' + Draft + '/schema.json';
      Result := True;
      Exit;
    end;
    if (Length(Name) > 5) and (Copy(Name, 1, 5) = 'meta/') then begin
      Vocabulary := Copy(Name, 6, Length(Name) - 5);
      for K := 1 to Length(Vocabulary) do
        if (Vocabulary[K] = '/') or (Vocabulary[K] = '.') then
          Exit;
      FileName := 'draft' + Draft + '/meta/' + Vocabulary + '.json';
      Result := True;
    end;
  end;
end;

end.
