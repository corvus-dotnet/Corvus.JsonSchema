program TestStringCases;

{$I corvus.inc}

{ Reads lines of "<JSON text in hexadecimal> <B, or A or N and the string in hexadecimal>" (written by a reference
  implementation: the cases of TestStringsAgreeWithEncodingJSON of the Go module's document_test.go, with what Go's
  encoding/json reads) and checks the parser against them: a text marked B must not parse, nor pass the syntax
  check, and any other must parse to the string given, with the all-ASCII flag A says. Usage: TestStringCases
  <file>. }

uses
  SysUtils, Corvus.JsonSchema.Document;

function HexValue(C: AnsiChar): Int32;
begin
  case C of
    '0'..'9': Result := Ord(C) - Ord('0');
    'a'..'f': Result := Ord(C) - Ord('a') + 10;
  else
    Result := -1;
  end;
end;

function FromHex(const S: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := '';
  SetLength(Result, Length(S) div 2);
  for I := 1 to Length(Result) do
    Result[I] := AnsiChar(HexValue(S[2 * I - 1]) * 16 + HexValue(S[2 * I]));
end;

var
  F: TextFile;
  Line, Text, Want: UTF8String;
  D: TDocument;
  E: TParseError;
  P: TParser;
  B: TBytes;
  Space, Cases, Failures: Int32;
  Flag: AnsiChar;
begin
  Cases := 0;
  Failures := 0;
  P := Default(TParser);
  AssignFile(F, ParamStr(1));
  Reset(F);
  while not Eof(F) do begin
    ReadLn(F, Line);
    Space := Pos(' ', Line);
    if (Space = 0) or (Space = Length(Line)) then
      Continue;
    Text := FromHex(Copy(Line, 1, Space - 1));
    Flag := Line[Space + 1];
    Want := FromHex(Copy(Line, Space + 2, Length(Line) - Space - 1));
    Inc(Cases);
    B := BytesOf(Text);
    if Flag = 'B' then begin
      if ParseDocument(B, D, E) or ParserIsValid(P, B, Length(B)) then begin
        Inc(Failures);
        WriteLn('FAILED: ', Line, ' parsed');
      end;
      Continue;
    end;
    if not ParseDocument(B, D, E) or not ParserIsValid(P, B, Length(B)) then begin
      Inc(Failures);
      WriteLn('FAILED: ', Line, ' did not parse: ', ParseErrorText(E));
    end else if (DocStrCopy(D, D.Root) <> Want) or (DocStrASCII(D, D.Root) <> (Flag = 'A')) then begin
      Inc(Failures);
      WriteLn('FAILED: ', Line, ' read differently');
    end;
  end;
  CloseFile(F);
  WriteLn(Cases, ' cases, ', Failures, ' failed');
  if (Failures > 0) or (Cases = 0) then
    Halt(1);
end.
