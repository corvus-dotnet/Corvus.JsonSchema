program TestDifferential;

{$I corvus.inc}

{ Differential testing of the fail-fast plans (fused objects, type dispatch, name tables and the rest) against the
  collecting evaluator, which walks the general keyword-by-keyword path: every instance of the jsonschema-benchmark
  corpora, and mutations of it (a property removed, given another type, nulled or added, an array item changed, at
  every depth), must get the same verdict from both. A port of differential_test.go of the Go module.

  Set JSONSCHEMA_BENCHMARK to a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark. The test is
  skipped without it. DIFF_ONLY narrows the corpora, DIFF_INSTANCES caps the instances of each corpus (default 60).

  The Go test reads a schema or an instance into maps and slices, and writes each mutation as text with the names of
  an object sorted. Here a value is a tree of the same shape, with an object's names sorted in byte order, so the
  mutations are the same ones in the same order and the count of instances checked is the Go test's. A number is
  kept as it was written, where the Go test reads it as a float64 and writes that. }

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Names,
  Corvus.JsonSchema.Loader,
  Corvus.JsonSchema;

{$I SuiteCommon.inc}

type
  TIndexArray = array of Int32;

  { A JSON value: a scalar as its JSON text, or the values of an array, or the names (sorted) and values of an
    object. A value is not changed once made, so a mutation shares what it does not change. }
  TValue = record
    Kind: Byte;
    Scalar: UTF8String;
    Names: TUTF8StringArray;
    Children: TIndexArray;
  end;

var
  { The values of the instance at hand, by index. }
  Pool: array of TValue;
  PoolLen: Int32;
  { The values tried in place of a value, of every JSON type. }
  Replacements: array[0..8] of Int32;
  One: Int32;
  Failed: Boolean;

procedure Fail(const What: UTF8String);
begin
  Failed := True;
  WriteLn('FAILED: ', What);
end;

function NewValue(Kind: Byte): Int32;
begin
  if PoolLen = Length(Pool) then
    SetLength(Pool, 2 * PoolLen + 1024);
  Result := PoolLen;
  Inc(PoolLen);
  Pool[Result].Kind := Kind;
  Pool[Result].Scalar := '';
  Pool[Result].Names := nil;
  Pool[Result].Children := nil;
end;

function ScalarValue(Kind: Byte; const Text: UTF8String): Int32;
begin
  Result := NewValue(Kind);
  Pool[Result].Scalar := Text;
end;

function ArrayValue(const Children: TIndexArray): Int32;
begin
  Result := NewValue(KindArray);
  Pool[Result].Children := Children;
end;

{ Quote is a string as JSON text. }
function Quote(const S: UTF8String): UTF8String;
const
  Hex: array[0..15] of AnsiChar = '0123456789abcdef';
var
  I: Int32;
  C: AnsiChar;
begin
  I := 1;
  while (I <= Length(S)) and (S[I] <> '"') and (S[I] <> '\') and (S[I] >= ' ') do
    Inc(I);
  if I > Length(S) then
    Exit('"' + S + '"');
  Result := '"';
  for I := 1 to Length(S) do begin
    C := S[I];
    if (C = '"') or (C = '\') then
      Result := Result + '\' + C
    else if C < ' ' then
      Result := Result + '\u00' + Hex[Ord(C) shr 4] + Hex[Ord(C) and 15]
    else
      Result := Result + C;
  end;
  Result := Result + '"';
end;

{ FromDocument is the value N of a document as a tree. StripFormat leaves out a "format" whose value is a string,
  at every depth (the stripFormat of the Go test). }
function FromDocument(const D: TDocument; N: Int32; StripFormat: Boolean): Int32;
var
  Kind: Byte;
  I, J, Count, Kept, First, Child: Int32;
  Names: TUTF8StringArray;
  Children: TIndexArray;
  Name: UTF8String;
begin
  Kind := DocKind(D, N);
  Count := DocCount(D, N);
  First := 0;
  if (Kind = KindArray) or (Kind = KindObject) then
    First := DocFirst(D, N);
  Names := nil;
  Children := nil;
  case Kind of
    KindArray: begin
      SetLength(Children, Count);
      for I := 0 to Count - 1 do begin
        Child := FromDocument(D, First + I, StripFormat);
        Children[I] := Child;
      end;
      Result := ArrayValue(Children);
    end;
    KindObject: begin
      SetLength(Names, Count);
      SetLength(Children, Count);
      Kept := 0;
      for I := 0 to Count - 1 do begin
        Name := DocStrCopy(D, First + 2 * I);
        if StripFormat and (Name = 'format') and (DocKind(D, First + 2 * I + 1) = KindString) then
          Continue;
        Child := FromDocument(D, First + 2 * I + 1, StripFormat);
        { The names in byte order, as the Go test's sorted keys. }
        J := Kept - 1;
        while (J >= 0) and (CompareUtf8(Names[J], Name) > 0) do begin
          Names[J + 1] := Names[J];
          Children[J + 1] := Children[J];
          Dec(J);
        end;
        Names[J + 1] := Name;
        Children[J + 1] := Child;
        Inc(Kept);
      end;
      SetLength(Names, Kept);
      SetLength(Children, Kept);
      Result := NewValue(KindObject);
      Pool[Result].Names := Names;
      Pool[Result].Children := Children;
    end;
  else
    Result := ScalarValue(Kind, ValueText(D, N));
  end;
end;

{ A buffer the text of a value is written to. }
var
  Buffer: TBytes;
  BufferLen: Int32;

procedure Append(const S: UTF8String);
begin
  if Length(S) = 0 then
    Exit;
  if BufferLen + Length(S) > Length(Buffer) then
    SetLength(Buffer, 2 * (BufferLen + Length(S)) + 256);
  Move(S[1], Buffer[BufferLen], Length(S));
  Inc(BufferLen, Length(S));
end;

procedure AppendValue(V: Int32);
var
  I: Int32;
begin
  case Pool[V].Kind of
    KindArray: begin
      Append('[');
      for I := 0 to Length(Pool[V].Children) - 1 do begin
        if I > 0 then
          Append(',');
        AppendValue(Pool[V].Children[I]);
      end;
      Append(']');
    end;
    KindObject: begin
      Append('{');
      for I := 0 to Length(Pool[V].Children) - 1 do begin
        if I > 0 then
          Append(',');
        Append(Quote(Pool[V].Names[I]));
        Append(':');
        AppendValue(Pool[V].Children[I]);
      end;
      Append('}');
    end;
  else
    Append(Pool[V].Scalar);
  end;
end;

{ TextOf is a value as JSON text. }
function TextOf(V: Int32): TBytes;
begin
  BufferLen := 0;
  AppendValue(V);
  Result := Copy(Buffer, 0, BufferLen);
end;

{ WithProperty is an object with a property set to a value (added in its sorted place when the object has no such
  name), or removed. }
function WithProperty(O: Int32; const Name: UTF8String; Value: Int32; Remove: Boolean): Int32;
var
  Names: TUTF8StringArray;
  Children: TIndexArray;
  I, N, At: Int32;
  Found: Boolean;
begin
  N := Length(Pool[O].Names);
  At := 0;
  while (At < N) and (CompareUtf8(Pool[O].Names[At], Name) < 0) do
    Inc(At);
  Found := (At < N) and (Pool[O].Names[At] = Name);
  Names := nil;
  Children := nil;
  if Remove then begin
    if Found then begin
      SetLength(Names, N - 1);
      SetLength(Children, N - 1);
      for I := 0 to N - 2 do begin
        Names[I] := Pool[O].Names[I + Ord(I >= At)];
        Children[I] := Pool[O].Children[I + Ord(I >= At)];
      end;
    end else begin
      Names := Copy(Pool[O].Names, 0, N);
      Children := Copy(Pool[O].Children, 0, N);
    end;
  end else if Found then begin
    Names := Copy(Pool[O].Names, 0, N);
    Children := Copy(Pool[O].Children, 0, N);
    Children[At] := Value;
  end else begin
    SetLength(Names, N + 1);
    SetLength(Children, N + 1);
    for I := 0 to N do
      if I < At then begin
        Names[I] := Pool[O].Names[I];
        Children[I] := Pool[O].Children[I];
      end else if I = At then begin
        Names[I] := Name;
        Children[I] := Value;
      end else begin
        Names[I] := Pool[O].Names[I - 1];
        Children[I] := Pool[O].Children[I - 1];
      end;
  end;
  Result := NewValue(KindObject);
  Pool[Result].Names := Names;
  Pool[Result].Children := Children;
end;

function WithItem(A, I, Value: Int32): Int32;
var
  Children: TIndexArray;
begin
  Children := Copy(Pool[A].Children, 0, Length(Pool[A].Children));
  Children[I] := Value;
  Result := ArrayValue(Children);
end;

procedure Add(var Out: TIndexArray; var Count: Int32; V: Int32);
begin
  if Count = Length(Out) then
    SetLength(Out, 2 * Count + 16);
  Out[Count] := V;
  Inc(Count);
end;

{ Mutations are mutations of V: at the root and, recursively, inside it (bounded at each level to keep the count
  sane). }
function Mutations(V, Depth: Int32): TIndexArray;
var
  Count, I, J, N, Child: Int32;
  Inner, Children: TIndexArray;
  Name: UTF8String;
begin
  Result := nil;
  Count := 0;
  if Depth > 6 then
    Exit;
  case Pool[V].Kind of
    KindObject: begin
      N := Length(Pool[V].Names);
      for I := 0 to N - 1 do begin
        if I >= 12 then
          Break;
        Name := Pool[V].Names[I];
        Child := Pool[V].Children[I];
        Add(Result, Count, WithProperty(V, Name, 0, True));
        J := I mod 3;
        while J <= High(Replacements) do begin
          Add(Result, Count, WithProperty(V, Name, Replacements[J], False));
          Inc(J, 3);
        end;
        Inner := Mutations(Child, Depth + 1);
        J := 0;
        while (J < Length(Inner)) and (J < 40) do begin
          Add(Result, Count, WithProperty(V, Name, Inner[J], False));
          Inc(J);
        end;
      end;
      Add(Result, Count, WithProperty(V, 'zzUnknownProperty', One, False));
    end;
    KindArray: begin
      N := Length(Pool[V].Children);
      I := 0;
      while (I < N) and (I < 4) do begin
        J := I mod 2;
        while J <= High(Replacements) do begin
          Add(Result, Count, WithItem(V, I, Replacements[J]));
          Inc(J, 2);
        end;
        Inner := Mutations(Pool[V].Children[I], Depth + 1);
        J := 0;
        while (J < Length(Inner)) and (J < 40) do begin
          Add(Result, Count, WithItem(V, I, Inner[J]));
          Inc(J);
        end;
        Inc(I);
      end;
      if N <> 0 then begin
        Children := Copy(Pool[V].Children, 0, N);
        SetLength(Children, N + 1);
        Children[N] := Children[0];
        Add(Result, Count, ArrayValue(Children));
      end;
    end;
  else
    for I := 0 to High(Replacements) do
      Add(Result, Count, Replacements[I]);
  end;
  SetLength(Result, Count);
end;

{ NewPool empties the pool and makes the values every instance's mutations use. }
procedure NewPool;
var
  Children: TIndexArray;
begin
  PoolLen := 0;
  Replacements[0] := ScalarValue(KindNull, 'null');
  Replacements[1] := ScalarValue(KindBool, 'true');
  Replacements[2] := ScalarValue(KindNumber, '0');
  Replacements[3] := ScalarValue(KindNumber, '-1.5');
  Replacements[4] := ScalarValue(KindString, '""');
  Replacements[5] := ScalarValue(KindString, '"x"');
  Replacements[6] := ArrayValue(nil);
  Replacements[7] := NewValue(KindObject);
  One := ScalarValue(KindNumber, '1');
  Children := nil;
  SetLength(Children, 2);
  Children[0] := One;
  Children[1] := ScalarValue(KindString, '"a"');
  Replacements[8] := ArrayValue(Children);
end;

{ Directories are the names of the directories of a directory, in byte order. }
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

function IsBlank(const B: TBytes; Start, Stop: Int32): Boolean;
var
  I: Int32;
begin
  for I := Start to Stop - 1 do
    if not (B[I] in [9, 10, 11, 12, 13, 32]) then
      Exit(False);
  Result := True;
end;

function Shown(const Text: TBytes): UTF8String;
var
  N: Int32;
begin
  N := Length(Text);
  if N > 400 then
    N := 400;
  Result := '';
  SetLength(Result, N);
  if N > 0 then
    Move(Text[0], Result[1], N);
end;

function BoolText(V: Boolean): UTF8String;
begin
  if V then
    Result := 'true'
  else
    Result := 'false';
end;

var
  Root, Only, Name, Error: UTF8String;
  Dirs: TUTF8StringArray;
  Limit, D, Lines, Failures, Start, Stop, I, X: Int32;
  Checked: Int64;
  SchemaText, Instances, Line, Text: TBytes;
  Doc: TDocument;
  E: TParseError;
  V: IJsonSchemaValidator;
  C: TJsonSchemaResults;
  Candidates: TIndexArray;
  Fast, Collected: Boolean;
begin
  Root := UTF8String(GetEnvironmentVariable('JSONSCHEMA_BENCHMARK'));
  if Root = '' then begin
    WriteLn('skipped: set JSONSCHEMA_BENCHMARK to a checkout of jsonschema-benchmark to run the differential test');
    Exit;
  end;
  if not DirectoryExists(Root + '/schemas') then begin
    WriteLn('FAILED: jsonschema-benchmark not found at ', Root);
    Halt(1);
  end;
  Dirs := Directories(Root + '/schemas');
  Only := UTF8String(GetEnvironmentVariable('DIFF_ONLY'));
  Limit := 60;
  if GetEnvironmentVariable('DIFF_INSTANCES') <> '' then
    Limit := StrToInt(GetEnvironmentVariable('DIFF_INSTANCES'));
  Failed := False;
  Checked := 0;
  Pool := nil;
  Buffer := nil;
  for D := 0 to Length(Dirs) - 1 do begin
    Name := Dirs[D];
    if (Only <> '') and (Pos(',' + Name + ',', ',' + Only + ',') = 0) then
      Continue;
    if not ReadFile(Root + '/schemas/' + Name + '/schema.json', SchemaText) then
      Continue;
    if not ParseDocument(SchemaText, Doc, E) then begin
      Fail(Name + ': ' + ParseErrorText(E));
      Break;
    end;
    NewPool;
    V := nil;
    try
      V := CompileJsonSchema(TextOf(FromDocument(Doc, Doc.Root, True)));
    except
      on Ex: EJsonSchemaError do begin
        Fail(Name + ': compile error ' + Ex.Utf8Message);
        Continue;
      end;
    end;
    if not ReadFile(Root + '/schemas/' + Name + '/instances.jsonl', Instances) then begin
      Fail(Name + ': instances.jsonl could not be read');
      Break;
    end;
    Failures := 0;
    Lines := 0;
    Start := 0;
    while Start <= Length(Instances) do begin
      Stop := Start;
      while (Stop < Length(Instances)) and (Instances[Stop] <> 10) do
        Inc(Stop);
      Line := Copy(Instances, Start, Stop - Start);
      Start := Stop + 1;
      if IsBlank(Line, 0, Length(Line)) then
        Continue;
      Inc(Lines);
      if Lines > Limit then
        Break;
      if not ParseDocument(Line, Doc, E) then begin
        Fail(Name + ': ' + ParseErrorText(E));
        Halt(1);
      end;
      if not V.TryIsValid(Line, 0, Length(Line), Error) then
        Fail(Name + ': instance ' + UTF8String(IntToStr(Lines)) + ' is not valid ' + Error);
      NewPool;
      X := FromDocument(Doc, Doc.Root, False);
      Candidates := Mutations(X, 0);
      for I := -1 to Length(Candidates) - 1 do begin
        Inc(Checked);
        if I < 0 then
          Text := TextOf(X)
        else
          Text := TextOf(Candidates[I]);
        Fast := V.TryIsValid(Text, 0, Length(Text), Error);
        if Error = '' then begin
          C := NewJsonSchemaResults(Basic);
          Collected := V.TryEvaluate(Text, 0, Length(Text), C, Error);
          if (Error = '') and (Fast = Collected) then
            Continue;
        end;
        Inc(Failures);
        if Failures <= 3 then
          Fail(Name + ': fail-fast ' + BoolText(Fast) + ', collecting ' + BoolText(Collected) + ' (' + Error
            + ') on ' + Shown(Text));
      end;
    end;
    if Failures > 3 then
      Fail(Name + ': ' + UTF8String(IntToStr(Failures)) + ' disagreements in all');
  end;
  JsonSchemaReleaseThreadScratch;
  WriteLn(Checked, ' instances checked');
  if Failed then
    Halt(1);
  if Checked = 0 then begin
    WriteLn('FAILED: no instances were checked');
    Halt(1);
  end;
end.
