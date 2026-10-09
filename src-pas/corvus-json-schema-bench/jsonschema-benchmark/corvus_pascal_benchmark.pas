program corvus_pascal_benchmark;

{$MODE DELPHI}
{$H+}

{ corvus-json-schema's implementation of the jsonschema-benchmark protocol
  (https://github.com/sourcemeta-research/jsonschema-benchmark):

    corvus_pascal_benchmark <schema.json> <instances.jsonl>

  It parses every instance, compiles the schema, validates every instance once cold, warms up, and validates once
  warm. It prints one line, cold,warm,compile,parse in nanoseconds, and exits 1 if an instance is invalid.

  The benchmark defines warm as steady state. The warm-up follows a rule that is the same for every engine whatever
  its runtime: validation passes for a fixed time (WarmupTime), and at least MinWarmupPasses passes. The warm figure
  is the last of those passes. }

uses
  SysUtils, Classes, BaseUnix, Linux, Corvus.JsonSchema;

const
  WarmupTime = Int64(2000000000);
  MinWarmupPasses = 100;

type
  TDocuments = array of TJsonDocument;

function Now64: Int64;
var
  T: TTimeSpec;
begin
  clock_gettime(CLOCK_MONOTONIC, @T);
  Result := Int64(T.tv_sec) * 1000000000 + T.tv_nsec;
end;

function ReadFile(const Name: AnsiString): TBytes;
var
  S: TFileStream;
begin
  Result := nil;
  S := TFileStream.Create(Name, fmOpenRead or fmShareDenyWrite);
  try
    SetLength(Result, S.Size);
    if S.Size > 0 then
      S.ReadBuffer(Result[0], S.Size);
  finally
    S.Free;
  end;
end;

function ValidateAll(const V: IJsonSchemaValidator; const Documents: TDocuments): Boolean;
var
  I: Int32;
begin
  Result := True;
  for I := 0 to High(Documents) do
    if not V.IsValid(Documents[I]) then
      Result := False;
end;

var
  Schema, Contents: TBytes;
  Texts: array of TBytes;
  Documents: TDocuments;
  V: IJsonSchemaValidator;
  I, Count, LineStart, K, Pass: Int32;
  Blank, Valid: Boolean;
  ParseStart, Parse, CompileStart, Compile, ColdStart, Cold, Deadline, Start, Warm: Int64;
begin
  if ParamCount <> 2 then begin
    WriteLn(StdErr, 'Usage: corvus_pascal_benchmark <schema> <instances>');
    Halt(2);
  end;
  try
    Schema := ReadFile(ParamStr(1));
    Contents := ReadFile(ParamStr(2));
    Count := 0;
    LineStart := 0;
    for I := 0 to Length(Contents) do
      if (I = Length(Contents)) or (Contents[I] = 10) then begin
        Blank := True;
        for K := LineStart to I - 1 do
          if not (Contents[K] in [9, 10, 13, 32]) then begin
            Blank := False;
            Break;
          end;
        if not Blank then begin
          if Count = Length(Texts) then
            SetLength(Texts, Count * 2 + 64);
          Texts[Count] := Copy(Contents, LineStart, I - LineStart);
          Inc(Count);
        end;
        LineStart := I + 1;
      end;
    SetLength(Texts, Count);

    ParseStart := Now64;
    SetLength(Documents, Count);
    for I := 0 to Count - 1 do
      Documents[I] := ParseJson(Texts[I]);
    Parse := Now64 - ParseStart;

    { The benchmark's schema-noformat.json has no format keywords, and the defaults leave format as an annotation. }
    CompileStart := Now64;
    V := CompileJsonSchema(Schema);
    Compile := Now64 - CompileStart;

    ColdStart := Now64;
    Valid := ValidateAll(V, Documents);
    Cold := Now64 - ColdStart;
    if not Valid then
      Halt(1);

    { The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it, as
      in the harnesses of the runtimes that compile while they run. }
    Deadline := Now64 + WarmupTime;
    Warm := 0;
    Pass := 0;
    while (Pass < MinWarmupPasses) or (Now64 < Deadline) do begin
      Start := Now64;
      Valid := ValidateAll(V, Documents) and Valid;
      Warm := Now64 - Start;
      Inc(Pass);
    end;
    if not Valid then
      Halt(1);

    WriteLn(Cold, ',', Warm, ',', Compile, ',', Parse);
  except
    on E: Exception do begin
      WriteLn(StdErr, E.Message);
      Halt(2);
    end;
  end;
end.
