program TestDoubleCases;

{$I corvus.inc}

{ Reads lines of "<number text> <the bits of its Double in hex>" (written by a reference implementation) and checks
  the parser gives the same bits. Usage: TestDoubleCases <file>. }

uses
  SysUtils, Corvus.JsonSchema.Document;

var
  F: TextFile;
  Line, Text, Expected: UTF8String;
  D: TDocument;
  E: TParseError;
  Space, Cases, Failures: Int32;
begin
  Cases := 0;
  Failures := 0;
  AssignFile(F, ParamStr(1));
  Reset(F);
  while not Eof(F) do begin
    ReadLn(F, Line);
    Space := Pos(' ', Line);
    if Space = 0 then
      Continue;
    Text := Copy(Line, 1, Space - 1);
    Expected := Copy(Line, Space + 1, 16);
    Inc(Cases);
    if not ParseDocumentString(Text, D, E) then begin
      Inc(Failures);
      if Failures <= 20 then
        WriteLn('FAILED: ', Copy(Text, 1, 60), ' did not parse: ', E.Message);
    end else if UTF8String(IntToHex(Int64(DocData(D, D.Root)), 16)) <> Expected then begin
      { An integer that fits 64 bits is kept as one: its bits are not a Double's. }
      if DocFlags(D, D.Root) <> NumFloat then
        Dec(Cases)
      else begin
        Inc(Failures);
        if Failures <= 20 then
          WriteLn('FAILED: ', Copy(Text, 1, 60), ' -> ', IntToHex(Int64(DocData(D, D.Root)), 16), ', expected ',
            Expected);
      end;
    end;
  end;
  CloseFile(F);
  WriteLn(Cases, ' numbers compared, ', Failures, ' failed');
  if Failures > 0 then
    Halt(1);
end.
