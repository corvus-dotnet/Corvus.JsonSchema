program TestDocument;

{$I corvus.inc}

{ The JSON parser and the number conversions: round trips, errors, and Doubles checked bit for bit. }

uses
  SysUtils, Corvus.JsonSchema.Document, Corvus.JsonSchema.Numbers;

var
  Failures, Checks: Int32;

procedure Check(Ok: Boolean; const What: UTF8String);
begin
  Inc(Checks);
  if not Ok then begin
    Inc(Failures);
    WriteLn('FAILED: ', What);
  end;
end;

procedure RoundTrip(const Json, Expected: UTF8String);
var
  D: TDocument;
  E: TParseError;
  Text: UTF8String;
begin
  if not ParseDocumentString(Json, D, E) then begin
    Check(False, Json + ' -> ' + ParseErrorText(E));
    Exit;
  end;
  Text := DocumentToJson(D);
  Check(Text = Expected, Json + ' -> ' + Text + ', expected ' + Expected);
end;

procedure Refused(const Json, Message: UTF8String; Offset: Int32);
var
  D: TDocument;
  E: TParseError;
  P: TParser;
  B: TBytes;
begin
  if ParseDocumentString(Json, D, E) then begin
    Check(False, Json + ' was accepted');
    Exit;
  end;
  Check((E.Message = Message) and (E.Offset = Offset), Json + ' -> ' + ParseErrorText(E));
  { The validating parser agrees. }
  FillChar(P, SizeOf(P), 0);
  B := BytesOf(Json);
  Check(not ParserIsValid(P, B, Length(B)), Json + ' was valid to the validating parser');
end;

procedure Bits(const Json: UTF8String; Flag: Byte; Expected: UInt64);
var
  D: TDocument;
  E: TParseError;
begin
  if not ParseDocumentString(Json, D, E) then begin
    Check(False, Json + ' -> ' + ParseErrorText(E));
    Exit;
  end;
  Check((DocKind(D, D.Root) = KindNumber) and (DocFlags(D, D.Root) = Flag) and (DocData(D, D.Root) = Expected),
    Json + ' -> flag ' + IntToStr(DocFlags(D, D.Root)) + ' data $' + IntToHex(Int64(DocData(D, D.Root)), 16));
end;

procedure Multiple(const X, Divisor: UTF8String; Expected: Boolean);
var
  DX, DV: TDocument;
  E: TParseError;
  V: TDivisor;
  Actual: Boolean;
begin
  if not ParseDocumentString(X, DX, E) or not ParseDocumentString(Divisor, DV, E) then begin
    Check(False, X + ' or ' + Divisor + ' did not parse');
    Exit;
  end;
  V := NewDivisor(DocFlags(DV, DV.Root), DocData(DV, DV.Root), DV.Source, DocNumberStart(DV, DV.Root));
  Actual := DivisorDivides(V, DocFlags(DX, DX.Root), DocData(DX, DX.Root), DX.Source, DocNumberStart(DX, DX.Root));
  Check(Actual = Expected, X + ' multipleOf ' + Divisor + ' -> ' + BoolToStr(Actual, True));
end;

procedure Compare(const A, B: UTF8String; Expected: Int32);
var
  DA, DB: TDocument;
  E: TParseError;
  Actual: Int32;
begin
  if not ParseDocumentString(A, DA, E) or not ParseDocumentString(B, DB, E) then begin
    Check(False, A + ' or ' + B + ' did not parse');
    Exit;
  end;
  Actual := CompareNumbers(DocFlags(DA, DA.Root), DocData(DA, DA.Root), DocFlags(DB, DB.Root), DocData(DB, DB.Root));
  if Actual < 0 then
    Actual := -1
  else if Actual > 0 then
    Actual := 1;
  Check(Actual = Expected, A + ' compared with ' + B + ' -> ' + IntToStr(Actual));
end;

var
  D: TDocument;
  E: TParseError;
  P: TParser;
  B: TBytes;
  I: Int32;
  Big: UTF8String;
begin
  Failures := 0;
  Checks := 0;

  { Values, whitespace, strings and escapes. }
  RoundTrip(' null ', 'null');
  RoundTrip('true', 'true');
  RoundTrip('false', 'false');
  RoundTrip('[]', '[]');
  RoundTrip('{}', '{}');
  RoundTrip(' [ 1 , 2.50 , -3e2 , "a" , null , [ ] , { } ] ', '[1,2.50,-3e2,"a",null,[],{}]');
  RoundTrip('{"a": {"b": [1, {"c": true}]}, "d": "e"}', '{"a":{"b":[1,{"c":true}]},"d":"e"}');
  RoundTrip('"a\"b\\c\/d\b\f\n\r\t"', '"a\"b\\c/d\b\f\n\r\t"');
  RoundTrip('"Aé€😀"', '"A' + #$C3#$A9 + #$E2#$82#$AC + #$F0#$9F#$98#$80 + '"');
  RoundTrip('"\u0000\u001f"', '"\u0000\u001f"');
  RoundTrip('"' + #$C3#$A9 + 'caf' + #$C3#$A9 + ' plain text longer than eight bytes"',
    '"' + #$C3#$A9 + 'caf' + #$C3#$A9 + ' plain text longer than eight bytes"');
  RoundTrip('"exactly8"', '"exactly8"');
  RoundTrip('"1234567\""', '"1234567\""');

  { Of duplicate names the last value is kept, at the position of the first. }
  RoundTrip('{"a": 1, "b": 2, "a": 3}', '{"a":3,"b":2}');
  RoundTrip('{"a": 1, "b": 2, "c": 3, "a": 4, "b": 5}', '{"a":4,"b":5,"c":3}');
  Big := '{';
  for I := 0 to 39 do
    Big := Big + '"k' + IntToStr(I) + '": ' + IntToStr(I) + ', ';
  Big := Big + '"k7": 700, "k39": 3900}';
  if ParseDocumentString(Big, D, E) then begin
    Check(DocCount(D, D.Root) = 40, 'forty names after duplicates in a large object');
    Check(DocData(D, DocProperty(D, D.Root, 'k7')) = 700, 'the last value of k7');
    Check(DocData(D, DocProperty(D, D.Root, 'k39')) = 3900, 'the last value of k39');
    Check(DocData(D, DocProperty(D, D.Root, 'k8')) = 8, 'the value of k8');
    Check(DocProperty(D, D.Root, 'k40') = -1, 'no k40');
  end else
    Check(False, 'a large object with duplicates: ' + ParseErrorText(E));

  { Errors, with the offsets the Go parser reports. }
  Refused('', 'unexpected end of input', 0);
  Refused('nul', 'expected a value', 0);
  Refused('[1,]', 'expected a value', 3);
  Refused('[1 2]', 'expected '','' or '']''', 3);
  Refused('{"a" 1}', 'expected '':''', 5);
  Refused('{"a": 1,}', 'expected a property name', 8);
  Refused('{"a": 1 "b": 2}', 'expected '','' or ''}''', 8);
  Refused('{1: 2}', 'expected a property name', 1);
  Refused('1 2', 'trailing characters', 2);
  Refused('"abc', 'unterminated string', 4);
  Refused('"a' + #10 + 'b"', 'control character in a string', 2);
  Refused('"\x"', 'invalid escape', 1);
  Refused('"\u12g4"', 'invalid \u escape', 1);
  Refused('"\ud800"', 'lone leading surrogate in hex escape', 1);
  Refused('"\udc00"', 'lone trailing surrogate in hex escape', 1);
  Refused('"' + #$C3 + '"', 'invalid UTF-8', 1);
  Refused('"' + #$ED#$A0#$80 + '"', 'invalid UTF-8', 1);
  Refused('"' + #$C0#$80 + '"', 'invalid UTF-8', 1);
  Refused('01', 'trailing characters', 1);
  Refused('-', 'invalid number', 1);
  Refused('1.', 'invalid number', 2);
  Refused('1e', 'invalid number', 2);
  Refused('1e400', 'number out of range', 0);
  Refused('-1e400', 'number out of range', 0);
  Big := '';
  for I := 1 to MaxDepth + 1 do
    Big := Big + '[';
  Refused(Big, 'nesting too deep', MaxDepth + 1);
  Big := '';
  for I := 1 to MaxDepth do
    Big := Big + '[';
  for I := 1 to MaxDepth do
    Big := Big + ']';
  Check(ParseDocumentString(Big, D, E), 'nesting of MaxDepth is accepted');

  { Numbers: integers stay integers, and a Double is the correctly rounded one. }
  Bits('0', NumInt, 0);
  Bits('-0', NumFloat, UInt64($8000000000000000));
  Bits('42', NumInt, 42);
  Bits('-42', NumInt, UInt64(-42));
  Bits('9223372036854775807', NumInt, UInt64($7FFFFFFFFFFFFFFF));
  Bits('-9223372036854775808', NumInt, UInt64($8000000000000000));
  Bits('9223372036854775808', NumUint, UInt64($8000000000000000));
  Bits('18446744073709551615', NumUint, UInt64($FFFFFFFFFFFFFFFF));
  Bits('18446744073709551616', NumFloat, UInt64($43F0000000000000));
  Bits('-9223372036854775809', NumFloat, UInt64($C3E0000000000000));
  Bits('1.0', NumFloat, UInt64($3FF0000000000000));
  Bits('0.1', NumFloat, UInt64($3FB999999999999A));
  Bits('1e22', NumFloat, UInt64($4480F0CF064DD592));
  Bits('1e23', NumFloat, UInt64($44B52D02C7E14AF6));
  Bits('123456789012345678901234567890', NumFloat, UInt64($45F8EE90FF6C373E));
  Bits('2.2250738585072014e-308', NumFloat, UInt64($0010000000000000));
  Bits('2.2250738585072011e-308', NumFloat, UInt64($000FFFFFFFFFFFFF));
  Bits('5e-324', NumFloat, 1);
  Bits('4.9406564584124654e-324', NumFloat, 1);
  Bits('2.4703282292062328e-324', NumFloat, 1);
  Bits('2.4703282292062327e-324', NumFloat, 0);
  Bits('1e-400', NumFloat, 0);
  Bits('1.7976931348623157e308', NumFloat, UInt64($7FEFFFFFFFFFFFFF));
  Bits('1.7976931348623158e308', NumFloat, UInt64($7FEFFFFFFFFFFFFF));
  Bits('9007199254740993', NumInt, UInt64(9007199254740993));
  Bits('9007199254740993.0', NumFloat, UInt64($4340000000000000));
  Bits('9007199254740993.00000000000000000001', NumFloat, UInt64($4340000000000001));
  Bits('9007199254740995.0', NumFloat, UInt64($4340000000000002));
  { The suite's bignum.json: the long text and the shortest text of its Double read as the same Double. }
  Bits('972783798187987123879878123.18878137', NumFloat, UInt64($45892557DAF10FBE));
  Bits('9.727837981879871e+26', NumFloat, UInt64($45892557DAF10FBE));
  { More than nineteen digits, where the digits beyond them decide the rounding. }
  Bits('1.00000000000000011102230246251565404236316680908203125', NumFloat, UInt64($3FF0000000000000));
  Bits('1.00000000000000011102230246251565404236316680908203126', NumFloat, UInt64($3FF0000000000001));
  Bits('1.00000000000000033306690738754696212708950042724609375', NumFloat, UInt64($3FF0000000000002));
  Bits('1.00000000000000033306690738754696212708950042724609374', NumFloat, UInt64($3FF0000000000001));

  { Comparisons are exact across the representations. }
  Compare('1', '1.0', 0);
  Compare('1', '1.5', -1);
  Compare('-1', '-1.5', 1);
  Compare('9007199254740993', '9007199254740992.0', 1);
  Compare('9223372036854775807', '9223372036854775808', -1);
  Compare('9223372036854775808', '9223372036854775808.0', 0);
  Compare('18446744073709551615', '18446744073709551616', -1);
  Compare('18446744073709551615', '1.8446744073709552e19', -1);
  Compare('9223372036854775807', '9223372036854775808.0', -1);
  Compare('-9223372036854775808', '-9223372036854775808.0', 0);
  Compare('-9223372036854775808', '-9223372036854775809', 0);
  Compare('1e300', '18446744073709551615', 1);
  Compare('-1e300', '-9223372036854775808', -1);
  Compare('0.5', '0', 1);
  Compare('-0.5', '0', -1);
  Compare('-0', '0', 0);

  { multipleOf is decided on the decimal digits. }
  Multiple('10', '2', True);
  Multiple('7', '2', False);
  Multiple('0', '3', True);
  Multiple('0.0', '0.1', True);
  Multiple('0.0075', '0.0001', True);
  Multiple('0.00751', '0.0001', False);
  Multiple('4.5', '1.5', True);
  Multiple('35', '1.5', False);
  Multiple('1e308', '1e-8', True);
  Multiple('12391239123', '0.01', True);
  Multiple('1e2', '100', True);
  Multiple('100', '1e2', True);
  Multiple('150', '1e2', False);
  Multiple('1.0', '1', True);
  Multiple('3', '0.5', True);
  Multiple('3', '0', False);
  Multiple('-9223372036854775808', '-1', True);
  Multiple('9223372036854775808', '2', True);
  Multiple('18446744073709551615', '5', True);
  Multiple('18446744073709551615', '2', False);
  Multiple('246913578246913578246913578', '123456789123456789123456789', True);
  Multiple('246913578246913578246913579', '123456789123456789123456789', False);
  Multiple('1234567891234567891.23456789', '0.000000000123456789123456789123456789', True);
  Multiple('1234567891234567891234567890', '12345678912345678912345678900', False);
  Multiple('123456789123456789123456789000', '12345678912345678912345678900', True);
  Multiple('1e300', '1e299', True);
  Multiple('1e-300', '1e-299', False);
  Multiple('1e-299', '1e-300', True);

  { Parsing into reused buffers gives the same document, and allocates no new arrays once they have grown. }
  P := Default(TParser);
  D := Default(TDocument);
  B := BytesOf('{"a": [1, 2, {"b": "c\n"}], "d": 1.5}');
  Check(ParserParseInto(P, D, B, Length(B)), 'parse into');
  Check(DocumentToJson(D) = '{"a":[1,2,{"b":"c\n"}],"d":1.5}', 'parse into: ' + DocumentToJson(D));
  B := BytesOf('[true');
  Check(not ParserParseInto(P, D, B, Length(B)), 'parse into refuses bad text');
  Check(ParserError(P).Message = 'unexpected end of input', 'parse into reports the error');
  B := BytesOf('[false, "x"]');
  Check(ParserParseInto(P, D, B, Length(B)) and (DocumentToJson(D) = '[false,"x"]'), 'parse into after a failure');
  Check(ParserIsValid(P, B, Length(B)), 'the validating parser accepts valid text');

  WriteLn(Checks, ' checks, ', Failures, ' failed');
  if Failures > 0 then
    Halt(1);
end.
