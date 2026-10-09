program TestUri;
{$I corvus.inc}

// The tests of the URI and JSON pointer functions. The Go module has no test file for uri.go: its functions are
// exercised through the loader, and asciiLower by TestUnicodeFormatsUseUnicode17 of unicode_test.go, which is here.
// The other cases are the examples of RFC 3986 section 5.4 and a set of harder inputs, and the result each one
// expects is the result the Go function gives for it (written out by running the Go source), so the two ports are
// held to the same answers.
//
// The program prints each failure and a final count, and exits with a status other than zero when anything failed.

uses
  SysUtils,
  Corvus.JsonSchema.Document,
  Corvus.JsonSchema.Uri;

var
  Checks: Int32 = 0;
  Failures: Int32 = 0;

// H is the text whose bytes are written in hexadecimal, for text that is not plain ASCII.
function H(const Hex: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := '';
  SetLength(Result, Length(Hex) div 2);
  for I := 1 to Length(Result) do begin
    Result[I] := AnsiChar(HexValue(Ord(Hex[2 * I - 1])) * 16 + HexValue(Ord(Hex[2 * I])));
  end;
end;

// Show is the text with every byte that is not printable ASCII written as \xNN.
function Show(const S: UTF8String): UTF8String;
var
  I: Int32;
begin
  Result := '';
  for I := 1 to Length(S) do begin
    if (Ord(S[I]) >= $20) and (Ord(S[I]) < $7F) then Result := Result + UTF8String(S[I])
    else Result := Result + UTF8String('\x' + IntToHex(Ord(S[I]), 2));
  end;
end;

procedure CheckText(const Name, Got, Want: UTF8String);
begin
  Inc(Checks);
  if not Utf8Equal(Got, Want) then begin
    Inc(Failures);
    WriteLn('FAIL: ', Name, ': got "', Show(Got), '", want "', Show(Want), '"');
  end;
end;

procedure CheckInt(const Name: UTF8String; Got, Want: Int32);
begin
  Inc(Checks);
  if Got <> Want then begin
    Inc(Failures);
    WriteLn('FAIL: ', Name, ': got ', Got, ', want ', Want);
  end;
end;

procedure CheckBool(const Name: UTF8String; Got, Want: Boolean);
begin
  Inc(Checks);
  if Got <> Want then begin
    Inc(Failures);
    WriteLn('FAIL: ', Name, ': got ', Got, ', want ', Want);
  end;
end;

// CheckParts checks what ParseURI makes of a URI, and that the parts go together again as the same text.
procedure CheckParts(const Uri, Scheme, Authority, Path, Query: UTF8String; HasScheme, HasAuthority,
  HasQuery: Boolean);
var
  P: TUriParts;
begin
  P := ParseURI(Uri);
  CheckText('parseURI scheme of ' + Uri, P.Scheme, Scheme);
  CheckText('parseURI authority of ' + Uri, P.Authority, Authority);
  CheckText('parseURI path of ' + Uri, P.Path, Path);
  CheckText('parseURI query of ' + Uri, P.Query, Query);
  CheckBool('parseURI hasScheme of ' + Uri, P.HasScheme, HasScheme);
  CheckBool('parseURI hasAuthority of ' + Uri, P.HasAuthority, HasAuthority);
  CheckBool('parseURI hasQuery of ' + Uri, P.HasQuery, HasQuery);
  CheckText('uriParts.String of ' + Uri, UriPartsToString(P), Uri);
  CheckBool('hasScheme of ' + Uri, Corvus.JsonSchema.Uri.HasScheme(Uri), HasScheme);
end;

procedure TestResolveURI1;
begin
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g'), 'http://a/b/c/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', './g'), 'http://a/b/c/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g/'), 'http://a/b/c/g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '/g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '?y'), 'http://a/b/c/d;p?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g?y'), 'http://a/b/c/g?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', ';x'), 'http://a/b/c/;x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g;x'), 'http://a/b/c/g;x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', ''), 'http://a/b/c/d;p?q');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '.'), 'http://a/b/c/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', './'), 'http://a/b/c/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '..'), 'http://a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../'), 'http://a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../g'), 'http://a/b/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../..'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../../'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '../../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '/./g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '/../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g.'), 'http://a/b/c/g.');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '.g'), 'http://a/b/c/.g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g..'), 'http://a/b/c/g..');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '..g'), 'http://a/b/c/..g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', './../g'), 'http://a/b/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', './g/.'), 'http://a/b/c/g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g/./h'), 'http://a/b/c/g/h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g/../h'), 'http://a/b/c/h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g;x=1/./y'), 'http://a/b/c/g;x=1/y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g;x=1/../y'), 'http://a/b/c/y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g?y/./x'), 'http://a/b/c/g?y/./x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'g?y/../x'), 'http://a/b/c/g?y/../x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '//G:80/a/../b'), 'http://g/b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', H('782fc3a92f2e2e2fc3af')),
    H('687474703a2f2f612f622f632f782fc3af'));
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'a//b'), 'http://a/b/c/a//b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '//'), 'http:///');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '?'), 'http://a/b/c/d;p?');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '/'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'a/./b/.'), 'http://a/b/c/a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'a/b/..'), 'http://a/b/c/a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '..a/b'), 'http://a/b/c/..a/b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', 'a?b?c'), 'http://a/b/c/a?b?c');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q', '//g/a/../../..'), 'http://g/');
  CheckText('resolveURI', ResolveURI('', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('', 'g'), 'g');
  CheckText('resolveURI', ResolveURI('', './g'), './g');
  CheckText('resolveURI', ResolveURI('', 'g/'), 'g/');
  CheckText('resolveURI', ResolveURI('', '/g'), '/g');
  CheckText('resolveURI', ResolveURI('', '//g'), '//g');
  CheckText('resolveURI', ResolveURI('', '?y'), '?y');
  CheckText('resolveURI', ResolveURI('', 'g?y'), 'g?y');
  CheckText('resolveURI', ResolveURI('', ';x'), ';x');
  CheckText('resolveURI', ResolveURI('', 'g;x'), 'g;x');
  CheckText('resolveURI', ResolveURI('', ''), '');
  CheckText('resolveURI', ResolveURI('', '.'), '.');
  CheckText('resolveURI', ResolveURI('', './'), './');
  CheckText('resolveURI', ResolveURI('', '..'), '..');
  CheckText('resolveURI', ResolveURI('', '../'), '../');
  CheckText('resolveURI', ResolveURI('', '../g'), '../g');
  CheckText('resolveURI', ResolveURI('', '../..'), '../..');
  CheckText('resolveURI', ResolveURI('', '../../'), '../../');
  CheckText('resolveURI', ResolveURI('', '../../g'), '../../g');
  CheckText('resolveURI', ResolveURI('', '../../../g'), '../../../g');
  CheckText('resolveURI', ResolveURI('', '../../../../g'), '../../../../g');
  CheckText('resolveURI', ResolveURI('', '/./g'), '/./g');
  CheckText('resolveURI', ResolveURI('', '/../g'), '/../g');
  CheckText('resolveURI', ResolveURI('', 'g.'), 'g.');
  CheckText('resolveURI', ResolveURI('', '.g'), '.g');
  CheckText('resolveURI', ResolveURI('', 'g..'), 'g..');
  CheckText('resolveURI', ResolveURI('', '..g'), '..g');
  CheckText('resolveURI', ResolveURI('', './../g'), './../g');
  CheckText('resolveURI', ResolveURI('', './g/.'), './g/.');
  CheckText('resolveURI', ResolveURI('', 'g/./h'), 'g/./h');
  CheckText('resolveURI', ResolveURI('', 'g/../h'), 'g/../h');
  CheckText('resolveURI', ResolveURI('', 'g;x=1/./y'), 'g;x=1/./y');
  CheckText('resolveURI', ResolveURI('', 'g;x=1/../y'), 'g;x=1/../y');
  CheckText('resolveURI', ResolveURI('', 'g?y/./x'), 'g?y/./x');
  CheckText('resolveURI', ResolveURI('', 'g?y/../x'), 'g?y/../x');
  CheckText('resolveURI', ResolveURI('', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('', '//G:80/a/../b'), '//G:80/a/../b');
  CheckText('resolveURI', ResolveURI('', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('', H('782fc3a92f2e2e2fc3af')), H('782fc3a92f2e2e2fc3af'));
  CheckText('resolveURI', ResolveURI('', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('', 'a//b'), 'a//b');
  CheckText('resolveURI', ResolveURI('', '//'), '//');
  CheckText('resolveURI', ResolveURI('', '?'), '?');
  CheckText('resolveURI', ResolveURI('', '/'), '/');
  CheckText('resolveURI', ResolveURI('', 'a/./b/.'), 'a/./b/.');
  CheckText('resolveURI', ResolveURI('', 'a/b/..'), 'a/b/..');
  CheckText('resolveURI', ResolveURI('', '..a/b'), '..a/b');
  CheckText('resolveURI', ResolveURI('', 'a?b?c'), 'a?b?c');
  CheckText('resolveURI', ResolveURI('', '//g?y'), '//g?y');
  CheckText('resolveURI', ResolveURI('', '//g/a/../../..'), '//g/a/../../..');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', './g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g/'), 'urn:g/');
  CheckText('resolveURI', ResolveURI('urn:example:a', '/g'), 'urn:/g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '//g'), 'urn://g/');
  CheckText('resolveURI', ResolveURI('urn:example:a', '?y'), 'urn:example:a?y');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g?y'), 'urn:g?y');
  CheckText('resolveURI', ResolveURI('urn:example:a', ';x'), 'urn:;x');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g;x'), 'urn:g;x');
  CheckText('resolveURI', ResolveURI('urn:example:a', ''), 'urn:example:a');
  CheckText('resolveURI', ResolveURI('urn:example:a', '.'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', './'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', '..'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../..'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../../'), 'urn:');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../../g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../../../g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '../../../../g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '/./g'), 'urn:/g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '/../g'), 'urn:/g');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g.'), 'urn:g.');
  CheckText('resolveURI', ResolveURI('urn:example:a', '.g'), 'urn:.g');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g..'), 'urn:g..');
  CheckText('resolveURI', ResolveURI('urn:example:a', '..g'), 'urn:..g');
  CheckText('resolveURI', ResolveURI('urn:example:a', './../g'), 'urn:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', './g/.'), 'urn:g/');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g/./h'), 'urn:g/h');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g/../h'), 'urn:h');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g;x=1/./y'), 'urn:g;x=1/y');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g;x=1/../y'), 'urn:y');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g?y/./x'), 'urn:g?y/./x');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'g?y/../x'), 'urn:g?y/../x');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('urn:example:a', '//G:80/a/../b'), 'urn://g:80/b');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('urn:example:a', H('782fc3a92f2e2e2fc3af')), H('75726e3a782fc3af'));
  CheckText('resolveURI', ResolveURI('urn:example:a', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'a//b'), 'urn:a//b');
  CheckText('resolveURI', ResolveURI('urn:example:a', '//'), 'urn:///');
  CheckText('resolveURI', ResolveURI('urn:example:a', '?'), 'urn:example:a?');
  CheckText('resolveURI', ResolveURI('urn:example:a', '/'), 'urn:/');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'a/./b/.'), 'urn:a/b/');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'a/b/..'), 'urn:a/');
  CheckText('resolveURI', ResolveURI('urn:example:a', '..a/b'), 'urn:..a/b');
  CheckText('resolveURI', ResolveURI('urn:example:a', 'a?b?c'), 'urn:a?b?c');
  CheckText('resolveURI', ResolveURI('urn:example:a', '//g?y'), 'urn://g/?y');
  CheckText('resolveURI', ResolveURI('urn:example:a', '//g/a/../../..'), 'urn://g/');
end;

procedure TestResolveURI2;
begin
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', './g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g/'), 'tag:g/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '/g'), 'tag:/g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '//g'), 'tag://g/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '?y'), 'tag:example.com,2024:x?y');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g?y'), 'tag:g?y');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', ';x'), 'tag:;x');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g;x'), 'tag:g;x');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', ''), 'tag:example.com,2024:x');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '.'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', './'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '..'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../..'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../../'), 'tag:');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../../g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../../../g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '../../../../g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '/./g'), 'tag:/g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '/../g'), 'tag:/g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g.'), 'tag:g.');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '.g'), 'tag:.g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g..'), 'tag:g..');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '..g'), 'tag:..g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', './../g'), 'tag:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', './g/.'), 'tag:g/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g/./h'), 'tag:g/h');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g/../h'), 'tag:h');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g;x=1/./y'), 'tag:g;x=1/y');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g;x=1/../y'), 'tag:y');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g?y/./x'), 'tag:g?y/./x');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'g?y/../x'), 'tag:g?y/../x');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '//G:80/a/../b'), 'tag://g:80/b');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', H('782fc3a92f2e2e2fc3af')), H('7461673a782fc3af'));
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'a//b'), 'tag:a//b');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '//'), 'tag:///');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '?'), 'tag:example.com,2024:x?');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '/'), 'tag:/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'a/./b/.'), 'tag:a/b/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'a/b/..'), 'tag:a/');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '..a/b'), 'tag:..a/b');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', 'a?b?c'), 'tag:a?b?c');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '//g?y'), 'tag://g/?y');
  CheckText('resolveURI', ResolveURI('tag:example.com,2024:x', '//g/a/../../..'), 'tag://g/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g'), 'http://example.com/a/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', './g'), 'http://example.com/a/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g/'), 'http://example.com/a/g/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '/g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '?y'), 'http://example.com/a/c?y');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g?y'), 'http://example.com/a/g?y');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', ';x'), 'http://example.com/a/;x');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g;x'), 'http://example.com/a/g;x');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', ''), 'HTTP://Example.COM:80/a/./b/../c');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '.'), 'http://example.com/a/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', './'), 'http://example.com/a/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '..'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../..'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../../'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '../../../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '/./g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '/../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g.'), 'http://example.com/a/g.');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '.g'), 'http://example.com/a/.g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g..'), 'http://example.com/a/g..');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '..g'), 'http://example.com/a/..g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', './../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', './g/.'), 'http://example.com/a/g/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g/./h'), 'http://example.com/a/g/h');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g/../h'), 'http://example.com/a/h');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g;x=1/./y'),
    'http://example.com/a/g;x=1/y');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g;x=1/../y'), 'http://example.com/a/y');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g?y/./x'), 'http://example.com/a/g?y/./x');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'g?y/../x'),
    'http://example.com/a/g?y/../x');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '//G:80/a/../b'), 'http://g/b');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', H('782fc3a92f2e2e2fc3af')),
    H('687474703a2f2f6578616d706c652e636f6d2f612f782fc3af'));
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'a//b'), 'http://example.com/a/a//b');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '//'), 'http:///');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '?'), 'http://example.com/a/c?');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '/'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'a/./b/.'), 'http://example.com/a/a/b/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'a/b/..'), 'http://example.com/a/a/');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '..a/b'), 'http://example.com/a/..a/b');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', 'a?b?c'), 'http://example.com/a/a?b?c');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI('HTTP://Example.COM:80/a/./b/../c', '//g/a/../../..'), 'http://g/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', './g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g/'), 'https://example.com/g/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '/g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '//g'), 'https://g/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '?y'), 'https://example.com/?y');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g?y'), 'https://example.com/g?y');
  CheckText('resolveURI', ResolveURI('https://example.com:443', ';x'), 'https://example.com/;x');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g;x'), 'https://example.com/g;x');
  CheckText('resolveURI', ResolveURI('https://example.com:443', ''), 'https://example.com:443');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '.'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', './'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '..'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../..'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../../'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../../../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '../../../../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '/./g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '/../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g.'), 'https://example.com/g.');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '.g'), 'https://example.com/.g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g..'), 'https://example.com/g..');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '..g'), 'https://example.com/..g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', './../g'), 'https://example.com/g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', './g/.'), 'https://example.com/g/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g/./h'), 'https://example.com/g/h');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g/../h'), 'https://example.com/h');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g;x=1/./y'), 'https://example.com/g;x=1/y');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g;x=1/../y'), 'https://example.com/y');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g?y/./x'), 'https://example.com/g?y/./x');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'g?y/../x'), 'https://example.com/g?y/../x');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '//G:80/a/../b'), 'https://g:80/b');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', H('782fc3a92f2e2e2fc3af')),
    H('68747470733a2f2f6578616d706c652e636f6d2f782fc3af'));
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'a//b'), 'https://example.com/a//b');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '//'), 'https:///');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '?'), 'https://example.com/?');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '/'), 'https://example.com/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'a/./b/.'), 'https://example.com/a/b/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'a/b/..'), 'https://example.com/a/');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '..a/b'), 'https://example.com/..a/b');
  CheckText('resolveURI', ResolveURI('https://example.com:443', 'a?b?c'), 'https://example.com/a?b?c');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '//g?y'), 'https://g/?y');
  CheckText('resolveURI', ResolveURI('https://example.com:443', '//g/a/../../..'), 'https://g/');
end;

procedure TestResolveURI3;
begin
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g'), 'https://example.com:80/x/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', './g'), 'https://example.com:80/x/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g/'), 'https://example.com:80/x/g/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '/g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '//g'), 'https://g/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '?y'), 'https://example.com:80/x/?y');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g?y'), 'https://example.com:80/x/g?y');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', ';x'), 'https://example.com:80/x/;x');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g;x'), 'https://example.com:80/x/g;x');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', ''), 'https://example.com:80/x/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '.'), 'https://example.com:80/x/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', './'), 'https://example.com:80/x/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '..'), 'https://example.com:80/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../'), 'https://example.com:80/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../..'), 'https://example.com:80/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../../'), 'https://example.com:80/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../../../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '../../../../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '/./g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '/../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g.'), 'https://example.com:80/x/g.');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '.g'), 'https://example.com:80/x/.g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g..'), 'https://example.com:80/x/g..');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '..g'), 'https://example.com:80/x/..g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', './../g'), 'https://example.com:80/g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', './g/.'), 'https://example.com:80/x/g/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g/./h'), 'https://example.com:80/x/g/h');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g/../h'), 'https://example.com:80/x/h');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g;x=1/./y'), 'https://example.com:80/x/g;x=1/y');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g;x=1/../y'), 'https://example.com:80/x/y');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g?y/./x'), 'https://example.com:80/x/g?y/./x');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'g?y/../x'), 'https://example.com:80/x/g?y/../x');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '//G:80/a/../b'), 'https://g:80/b');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', H('782fc3a92f2e2e2fc3af')),
    H('68747470733a2f2f6578616d706c652e636f6d3a38302f782f782fc3af'));
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'a//b'), 'https://example.com:80/x/a//b');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '//'), 'https:///');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '?'), 'https://example.com:80/x/?');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '/'), 'https://example.com:80/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'a/./b/.'), 'https://example.com:80/x/a/b/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'a/b/..'), 'https://example.com:80/x/a/');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '..a/b'), 'https://example.com:80/x/..a/b');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', 'a?b?c'), 'https://example.com:80/x/a?b?c');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '//g?y'), 'https://g/?y');
  CheckText('resolveURI', ResolveURI('https://example.com:80/x/', '//g/a/../../..'), 'https://g/');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', './g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g/'), 'http://example.com/g/');
  CheckText('resolveURI', ResolveURI('http://example.com', '/g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI('http://example.com', '?y'), 'http://example.com/?y');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g?y'), 'http://example.com/g?y');
  CheckText('resolveURI', ResolveURI('http://example.com', ';x'), 'http://example.com/;x');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g;x'), 'http://example.com/g;x');
  CheckText('resolveURI', ResolveURI('http://example.com', ''), 'http://example.com');
  CheckText('resolveURI', ResolveURI('http://example.com', '.'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', './'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', '..'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', '../'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', '../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '../..'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', '../../'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', '../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '../../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '../../../../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '/./g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', '/../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g.'), 'http://example.com/g.');
  CheckText('resolveURI', ResolveURI('http://example.com', '.g'), 'http://example.com/.g');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g..'), 'http://example.com/g..');
  CheckText('resolveURI', ResolveURI('http://example.com', '..g'), 'http://example.com/..g');
  CheckText('resolveURI', ResolveURI('http://example.com', './../g'), 'http://example.com/g');
  CheckText('resolveURI', ResolveURI('http://example.com', './g/.'), 'http://example.com/g/');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g/./h'), 'http://example.com/g/h');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g/../h'), 'http://example.com/h');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g;x=1/./y'), 'http://example.com/g;x=1/y');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g;x=1/../y'), 'http://example.com/y');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g?y/./x'), 'http://example.com/g?y/./x');
  CheckText('resolveURI', ResolveURI('http://example.com', 'g?y/../x'), 'http://example.com/g?y/../x');
  CheckText('resolveURI', ResolveURI('http://example.com', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('http://example.com', '//G:80/a/../b'), 'http://g/b');
  CheckText('resolveURI', ResolveURI('http://example.com', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('http://example.com', H('782fc3a92f2e2e2fc3af')),
    H('687474703a2f2f6578616d706c652e636f6d2f782fc3af'));
  CheckText('resolveURI', ResolveURI('http://example.com', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('http://example.com', 'a//b'), 'http://example.com/a//b');
  CheckText('resolveURI', ResolveURI('http://example.com', '//'), 'http:///');
  CheckText('resolveURI', ResolveURI('http://example.com', '?'), 'http://example.com/?');
  CheckText('resolveURI', ResolveURI('http://example.com', '/'), 'http://example.com/');
  CheckText('resolveURI', ResolveURI('http://example.com', 'a/./b/.'), 'http://example.com/a/b/');
  CheckText('resolveURI', ResolveURI('http://example.com', 'a/b/..'), 'http://example.com/a/');
  CheckText('resolveURI', ResolveURI('http://example.com', '..a/b'), 'http://example.com/..a/b');
  CheckText('resolveURI', ResolveURI('http://example.com', 'a?b?c'), 'http://example.com/a?b?c');
  CheckText('resolveURI', ResolveURI('http://example.com', '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI('http://example.com', '//g/a/../../..'), 'http://g/');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g'), 'file:///a/g');
  CheckText('resolveURI', ResolveURI('file:///a/b', './g'), 'file:///a/g');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g/'), 'file:///a/g/');
  CheckText('resolveURI', ResolveURI('file:///a/b', '/g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '//g'), 'file://g/');
  CheckText('resolveURI', ResolveURI('file:///a/b', '?y'), 'file:///a/b?y');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g?y'), 'file:///a/g?y');
  CheckText('resolveURI', ResolveURI('file:///a/b', ';x'), 'file:///a/;x');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g;x'), 'file:///a/g;x');
  CheckText('resolveURI', ResolveURI('file:///a/b', ''), 'file:///a/b');
  CheckText('resolveURI', ResolveURI('file:///a/b', '.'), 'file:///a/');
  CheckText('resolveURI', ResolveURI('file:///a/b', './'), 'file:///a/');
  CheckText('resolveURI', ResolveURI('file:///a/b', '..'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../..'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../../'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../../../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '../../../../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '/./g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '/../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g.'), 'file:///a/g.');
  CheckText('resolveURI', ResolveURI('file:///a/b', '.g'), 'file:///a/.g');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g..'), 'file:///a/g..');
  CheckText('resolveURI', ResolveURI('file:///a/b', '..g'), 'file:///a/..g');
  CheckText('resolveURI', ResolveURI('file:///a/b', './../g'), 'file:///g');
  CheckText('resolveURI', ResolveURI('file:///a/b', './g/.'), 'file:///a/g/');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g/./h'), 'file:///a/g/h');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g/../h'), 'file:///a/h');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g;x=1/./y'), 'file:///a/g;x=1/y');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g;x=1/../y'), 'file:///a/y');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g?y/./x'), 'file:///a/g?y/./x');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'g?y/../x'), 'file:///a/g?y/../x');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('file:///a/b', '//G:80/a/../b'), 'file://g:80/b');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('file:///a/b', H('782fc3a92f2e2e2fc3af')), H('66696c653a2f2f2f612f782fc3af'));
  CheckText('resolveURI', ResolveURI('file:///a/b', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'a//b'), 'file:///a/a//b');
  CheckText('resolveURI', ResolveURI('file:///a/b', '//'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', '?'), 'file:///a/b?');
  CheckText('resolveURI', ResolveURI('file:///a/b', '/'), 'file:///');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'a/./b/.'), 'file:///a/a/b/');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'a/b/..'), 'file:///a/a/');
  CheckText('resolveURI', ResolveURI('file:///a/b', '..a/b'), 'file:///a/..a/b');
  CheckText('resolveURI', ResolveURI('file:///a/b', 'a?b?c'), 'file:///a/a?b?c');
  CheckText('resolveURI', ResolveURI('file:///a/b', '//g?y'), 'file://g/?y');
  CheckText('resolveURI', ResolveURI('file:///a/b', '//g/a/../../..'), 'file://g/');
end;

procedure TestResolveURI4;
begin
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g'), '/a/b/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', './g'), '/a/b/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g/'), '/a/b/g/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '/g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '//g'), '//g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '?y'), '/a/b/c?y');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g?y'), '/a/b/g?y');
  CheckText('resolveURI', ResolveURI('/a/b/c', ';x'), '/a/b/;x');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g;x'), '/a/b/g;x');
  CheckText('resolveURI', ResolveURI('/a/b/c', ''), '/a/b/c');
  CheckText('resolveURI', ResolveURI('/a/b/c', '.'), '/a/b/');
  CheckText('resolveURI', ResolveURI('/a/b/c', './'), '/a/b/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '..'), '/a/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../'), '/a/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../g'), '/a/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../..'), '/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../../'), '/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../../g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../../../g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '../../../../g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '/./g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '/../g'), '/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g.'), '/a/b/g.');
  CheckText('resolveURI', ResolveURI('/a/b/c', '.g'), '/a/b/.g');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g..'), '/a/b/g..');
  CheckText('resolveURI', ResolveURI('/a/b/c', '..g'), '/a/b/..g');
  CheckText('resolveURI', ResolveURI('/a/b/c', './../g'), '/a/g');
  CheckText('resolveURI', ResolveURI('/a/b/c', './g/.'), '/a/b/g/');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g/./h'), '/a/b/g/h');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g/../h'), '/a/b/h');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g;x=1/./y'), '/a/b/g;x=1/y');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g;x=1/../y'), '/a/b/y');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g?y/./x'), '/a/b/g?y/./x');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'g?y/../x'), '/a/b/g?y/../x');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('/a/b/c', '//G:80/a/../b'), '//G:80/b');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('/a/b/c', H('782fc3a92f2e2e2fc3af')), H('2f612f622f782fc3af'));
  CheckText('resolveURI', ResolveURI('/a/b/c', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'a//b'), '/a/b/a//b');
  CheckText('resolveURI', ResolveURI('/a/b/c', '//'), '//');
  CheckText('resolveURI', ResolveURI('/a/b/c', '?'), '/a/b/c?');
  CheckText('resolveURI', ResolveURI('/a/b/c', '/'), '/');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'a/./b/.'), '/a/b/a/b/');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'a/b/..'), '/a/b/a/');
  CheckText('resolveURI', ResolveURI('/a/b/c', '..a/b'), '/a/b/..a/b');
  CheckText('resolveURI', ResolveURI('/a/b/c', 'a?b?c'), '/a/b/a?b?c');
  CheckText('resolveURI', ResolveURI('/a/b/c', '//g?y'), '//g?y');
  CheckText('resolveURI', ResolveURI('/a/b/c', '//g/a/../../..'), '//g/');
  CheckText('resolveURI', ResolveURI('a/b', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('a/b', 'g'), 'a/g');
  CheckText('resolveURI', ResolveURI('a/b', './g'), 'a/g');
  CheckText('resolveURI', ResolveURI('a/b', 'g/'), 'a/g/');
  CheckText('resolveURI', ResolveURI('a/b', '/g'), '/g');
  CheckText('resolveURI', ResolveURI('a/b', '//g'), '//g');
  CheckText('resolveURI', ResolveURI('a/b', '?y'), 'a/b?y');
  CheckText('resolveURI', ResolveURI('a/b', 'g?y'), 'a/g?y');
  CheckText('resolveURI', ResolveURI('a/b', ';x'), 'a/;x');
  CheckText('resolveURI', ResolveURI('a/b', 'g;x'), 'a/g;x');
  CheckText('resolveURI', ResolveURI('a/b', ''), 'a/b');
  CheckText('resolveURI', ResolveURI('a/b', '.'), 'a/');
  CheckText('resolveURI', ResolveURI('a/b', './'), 'a/');
  CheckText('resolveURI', ResolveURI('a/b', '..'), '');
  CheckText('resolveURI', ResolveURI('a/b', '../'), '');
  CheckText('resolveURI', ResolveURI('a/b', '../g'), 'g');
  CheckText('resolveURI', ResolveURI('a/b', '../..'), '');
  CheckText('resolveURI', ResolveURI('a/b', '../../'), '');
  CheckText('resolveURI', ResolveURI('a/b', '../../g'), 'g');
  CheckText('resolveURI', ResolveURI('a/b', '../../../g'), 'g');
  CheckText('resolveURI', ResolveURI('a/b', '../../../../g'), 'g');
  CheckText('resolveURI', ResolveURI('a/b', '/./g'), '/g');
  CheckText('resolveURI', ResolveURI('a/b', '/../g'), '/g');
  CheckText('resolveURI', ResolveURI('a/b', 'g.'), 'a/g.');
  CheckText('resolveURI', ResolveURI('a/b', '.g'), 'a/.g');
  CheckText('resolveURI', ResolveURI('a/b', 'g..'), 'a/g..');
  CheckText('resolveURI', ResolveURI('a/b', '..g'), 'a/..g');
  CheckText('resolveURI', ResolveURI('a/b', './../g'), 'g');
  CheckText('resolveURI', ResolveURI('a/b', './g/.'), 'a/g/');
  CheckText('resolveURI', ResolveURI('a/b', 'g/./h'), 'a/g/h');
  CheckText('resolveURI', ResolveURI('a/b', 'g/../h'), 'a/h');
  CheckText('resolveURI', ResolveURI('a/b', 'g;x=1/./y'), 'a/g;x=1/y');
  CheckText('resolveURI', ResolveURI('a/b', 'g;x=1/../y'), 'a/y');
  CheckText('resolveURI', ResolveURI('a/b', 'g?y/./x'), 'a/g?y/./x');
  CheckText('resolveURI', ResolveURI('a/b', 'g?y/../x'), 'a/g?y/../x');
  CheckText('resolveURI', ResolveURI('a/b', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('a/b', '//G:80/a/../b'), '//G:80/b');
  CheckText('resolveURI', ResolveURI('a/b', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('a/b', H('782fc3a92f2e2e2fc3af')), H('612f782fc3af'));
  CheckText('resolveURI', ResolveURI('a/b', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('a/b', 'a//b'), 'a/a//b');
  CheckText('resolveURI', ResolveURI('a/b', '//'), '//');
  CheckText('resolveURI', ResolveURI('a/b', '?'), 'a/b?');
  CheckText('resolveURI', ResolveURI('a/b', '/'), '/');
  CheckText('resolveURI', ResolveURI('a/b', 'a/./b/.'), 'a/a/b/');
  CheckText('resolveURI', ResolveURI('a/b', 'a/b/..'), 'a/a/');
  CheckText('resolveURI', ResolveURI('a/b', '..a/b'), 'a/..a/b');
  CheckText('resolveURI', ResolveURI('a/b', 'a?b?c'), 'a/a?b?c');
  CheckText('resolveURI', ResolveURI('a/b', '//g?y'), '//g?y');
  CheckText('resolveURI', ResolveURI('a/b', '//g/a/../../..'), '//g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g'), 'http://a/b/c/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', './g'), 'http://a/b/c/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g/'), 'http://a/b/c/g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '/g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '?y'), 'http://a/b/c/d;p?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g?y'), 'http://a/b/c/g?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', ';x'), 'http://a/b/c/;x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g;x'), 'http://a/b/c/g;x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', ''), 'http://a/b/c/d;p?q#frag');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '.'), 'http://a/b/c/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', './'), 'http://a/b/c/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '..'), 'http://a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../'), 'http://a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../g'), 'http://a/b/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../..'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../../'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '../../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '/./g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '/../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g.'), 'http://a/b/c/g.');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '.g'), 'http://a/b/c/.g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g..'), 'http://a/b/c/g..');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '..g'), 'http://a/b/c/..g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', './../g'), 'http://a/b/g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', './g/.'), 'http://a/b/c/g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g/./h'), 'http://a/b/c/g/h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g/../h'), 'http://a/b/c/h');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g;x=1/./y'), 'http://a/b/c/g;x=1/y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g;x=1/../y'), 'http://a/b/c/y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g?y/./x'), 'http://a/b/c/g?y/./x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'g?y/../x'), 'http://a/b/c/g?y/../x');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '//G:80/a/../b'), 'http://g/b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', H('782fc3a92f2e2e2fc3af')),
    H('687474703a2f2f612f622f632f782fc3af'));
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'a//b'), 'http://a/b/c/a//b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '//'), 'http:///');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '?'), 'http://a/b/c/d;p?');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '/'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'a/./b/.'), 'http://a/b/c/a/b/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'a/b/..'), 'http://a/b/c/a/');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '..a/b'), 'http://a/b/c/..a/b');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', 'a?b?c'), 'http://a/b/c/a?b?c');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI('http://a/b/c/d;p?q#frag', '//g/a/../../..'), 'http://g/');
end;

procedure TestResolveURI5;
begin
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), './g'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g/'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f672f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '/g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '?y'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f783f79'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g?y'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f673f79'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), ';x'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f3b78'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g;x'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f673b78'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), ''),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '.'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), './'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '..'),
    H('687474703a2f2fc38978616d706c652e636f6d2f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../'),
    H('687474703a2f2fc38978616d706c652e636f6d2f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../..'),
    H('687474703a2f2fc38978616d706c652e636f6d2f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../../'),
    H('687474703a2f2fc38978616d706c652e636f6d2f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../../../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '../../../../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '/./g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '/../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g.'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f672e'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '.g'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f2e67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g..'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f672e2e'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '..g'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f2e2e67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), './../g'),
    H('687474703a2f2fc38978616d706c652e636f6d2f67'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), './g/.'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f672f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g/./h'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f672f68'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g/../h'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f68'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g;x=1/./y'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f673b783d312f79'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g;x=1/../y'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f79'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g?y/./x'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f673f792f2e2f78'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'g?y/../x'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f673f792f2e2e2f78'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '//G:80/a/../b'),
    'http://g/b');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'HTTPS://G:443'),
    'https://g/');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'),
    H('782fc3a92f2e2e2fc3af')), H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f782fc3af'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'urn:Other:B'),
    'urn:Other:B');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'a//b'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f612f2f62'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '//'), 'http:///');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '?'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f783f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '/'),
    H('687474703a2f2fc38978616d706c652e636f6d2f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'a/./b/.'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f612f622f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'a/b/..'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f612f'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '..a/b'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f2e2e612f62'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'a?b?c'),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f613f623f63'));
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), '//g/a/../../..'),
    'http://g/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', './g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g/'), 'mailto:g/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '/g'), 'mailto:/g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '//g'), 'mailto://g/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '?y'), 'mailto:x@y?y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g?y'), 'mailto:g?y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', ';x'), 'mailto:;x');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g;x'), 'mailto:g;x');
  CheckText('resolveURI', ResolveURI('mailto:x@y', ''), 'mailto:x@y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '.'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', './'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '..'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../..'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../../'), 'mailto:');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../../g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../../../g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '../../../../g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '/./g'), 'mailto:/g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '/../g'), 'mailto:/g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g.'), 'mailto:g.');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '.g'), 'mailto:.g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g..'), 'mailto:g..');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '..g'), 'mailto:..g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', './../g'), 'mailto:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', './g/.'), 'mailto:g/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g/./h'), 'mailto:g/h');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g/../h'), 'mailto:h');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g;x=1/./y'), 'mailto:g;x=1/y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g;x=1/../y'), 'mailto:y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g?y/./x'), 'mailto:g?y/./x');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'g?y/../x'), 'mailto:g?y/../x');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '//G:80/a/../b'), 'mailto://g:80/b');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', H('782fc3a92f2e2e2fc3af')), H('6d61696c746f3a782fc3af'));
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'a//b'), 'mailto:a//b');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '//'), 'mailto:///');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '?'), 'mailto:x@y?');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '/'), 'mailto:/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'a/./b/.'), 'mailto:a/b/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'a/b/..'), 'mailto:a/');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '..a/b'), 'mailto:..a/b');
  CheckText('resolveURI', ResolveURI('mailto:x@y', 'a?b?c'), 'mailto:a?b?c');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '//g?y'), 'mailto://g/?y');
  CheckText('resolveURI', ResolveURI('mailto:x@y', '//g/a/../../..'), 'mailto://g/');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g:h'), 'g:h');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', './g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g/'), 'http://a/g/');
  CheckText('resolveURI', ResolveURI('http://a?q', '/g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '//g'), 'http://g/');
  CheckText('resolveURI', ResolveURI('http://a?q', '?y'), 'http://a/?y');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g?y'), 'http://a/g?y');
  CheckText('resolveURI', ResolveURI('http://a?q', ';x'), 'http://a/;x');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g;x'), 'http://a/g;x');
  CheckText('resolveURI', ResolveURI('http://a?q', ''), 'http://a?q');
  CheckText('resolveURI', ResolveURI('http://a?q', '.'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', './'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '..'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '../'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '../..'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '../../'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '../../../../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '/./g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', '/../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g.'), 'http://a/g.');
  CheckText('resolveURI', ResolveURI('http://a?q', '.g'), 'http://a/.g');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g..'), 'http://a/g..');
  CheckText('resolveURI', ResolveURI('http://a?q', '..g'), 'http://a/..g');
  CheckText('resolveURI', ResolveURI('http://a?q', './../g'), 'http://a/g');
  CheckText('resolveURI', ResolveURI('http://a?q', './g/.'), 'http://a/g/');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g/./h'), 'http://a/g/h');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g/../h'), 'http://a/h');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g;x=1/./y'), 'http://a/g;x=1/y');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g;x=1/../y'), 'http://a/y');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g?y/./x'), 'http://a/g?y/./x');
  CheckText('resolveURI', ResolveURI('http://a?q', 'g?y/../x'), 'http://a/g?y/../x');
  CheckText('resolveURI', ResolveURI('http://a?q', 'http:g'), 'http:g');
  CheckText('resolveURI', ResolveURI('http://a?q', '//G:80/a/../b'), 'http://g/b');
  CheckText('resolveURI', ResolveURI('http://a?q', 'HTTPS://G:443'), 'https://g/');
  CheckText('resolveURI', ResolveURI('http://a?q', H('782fc3a92f2e2e2fc3af')), H('687474703a2f2f612f782fc3af'));
  CheckText('resolveURI', ResolveURI('http://a?q', 'urn:Other:B'), 'urn:Other:B');
  CheckText('resolveURI', ResolveURI('http://a?q', 'a//b'), 'http://a/a//b');
  CheckText('resolveURI', ResolveURI('http://a?q', '//'), 'http:///');
  CheckText('resolveURI', ResolveURI('http://a?q', '?'), 'http://a/?');
  CheckText('resolveURI', ResolveURI('http://a?q', '/'), 'http://a/');
  CheckText('resolveURI', ResolveURI('http://a?q', 'a/./b/.'), 'http://a/a/b/');
  CheckText('resolveURI', ResolveURI('http://a?q', 'a/b/..'), 'http://a/a/');
  CheckText('resolveURI', ResolveURI('http://a?q', '..a/b'), 'http://a/..a/b');
  CheckText('resolveURI', ResolveURI('http://a?q', 'a?b?c'), 'http://a/a?b?c');
  CheckText('resolveURI', ResolveURI('http://a?q', '//g?y'), 'http://g/?y');
  CheckText('resolveURI', ResolveURI('http://a?q', '//g/a/../../..'), 'http://g/');
end;

procedure TestNormalizeURI1;
var
  Left, Right: UTF8String;
begin
  CheckText('normalizeURI', NormalizeURI('http://a/b/c/d;p?q'), 'http://a/b/c/d;p?q');
  SplitFragment('http://a/b/c/d;p?q', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/b/c/d;p?q');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/b/c/d;p?q', 'http', 'a', '/b/c/d;p', 'q', True, True, True);
  CheckInt('schemeEnd', SchemeEnd('http://a/b/c/d;p?q'), 5);
  CheckText('normalizeURI', NormalizeURI(''), '');
  SplitFragment('', Left, Right);
  CheckText('splitFragment left', Left, '');
  CheckText('splitFragment right', Right, '');
  CheckParts('', '', '', '', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd(''), 0);
  CheckText('normalizeURI', NormalizeURI('urn:example:a'), 'urn:example:a');
  SplitFragment('urn:example:a', Left, Right);
  CheckText('splitFragment left', Left, 'urn:example:a');
  CheckText('splitFragment right', Right, '');
  CheckParts('urn:example:a', 'urn', '', 'example:a', '', True, False, False);
  CheckInt('schemeEnd', SchemeEnd('urn:example:a'), 4);
  CheckText('normalizeURI', NormalizeURI('tag:example.com,2024:x'), 'tag:example.com,2024:x');
  SplitFragment('tag:example.com,2024:x', Left, Right);
  CheckText('splitFragment left', Left, 'tag:example.com,2024:x');
  CheckText('splitFragment right', Right, '');
  CheckParts('tag:example.com,2024:x', 'tag', '', 'example.com,2024:x', '', True, False, False);
  CheckInt('schemeEnd', SchemeEnd('tag:example.com,2024:x'), 4);
  CheckText('normalizeURI', NormalizeURI('HTTP://Example.COM:80/a/./b/../c'), 'http://example.com/a/c');
  SplitFragment('HTTP://Example.COM:80/a/./b/../c', Left, Right);
  CheckText('splitFragment left', Left, 'HTTP://Example.COM:80/a/./b/../c');
  CheckText('splitFragment right', Right, '');
  CheckParts('HTTP://Example.COM:80/a/./b/../c', 'HTTP', 'Example.COM:80', '/a/./b/../c', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('HTTP://Example.COM:80/a/./b/../c'), 5);
  CheckText('normalizeURI', NormalizeURI('https://example.com:443'), 'https://example.com/');
  SplitFragment('https://example.com:443', Left, Right);
  CheckText('splitFragment left', Left, 'https://example.com:443');
  CheckText('splitFragment right', Right, '');
  CheckParts('https://example.com:443', 'https', 'example.com:443', '', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('https://example.com:443'), 6);
  CheckText('normalizeURI', NormalizeURI('https://example.com:80/x/'), 'https://example.com:80/x/');
  SplitFragment('https://example.com:80/x/', Left, Right);
  CheckText('splitFragment left', Left, 'https://example.com:80/x/');
  CheckText('splitFragment right', Right, '');
  CheckParts('https://example.com:80/x/', 'https', 'example.com:80', '/x/', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('https://example.com:80/x/'), 6);
  CheckText('normalizeURI', NormalizeURI('http://example.com'), 'http://example.com/');
  SplitFragment('http://example.com', Left, Right);
  CheckText('splitFragment left', Left, 'http://example.com');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://example.com', 'http', 'example.com', '', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://example.com'), 5);
  CheckText('normalizeURI', NormalizeURI('file:///a/b'), 'file:///a/b');
  SplitFragment('file:///a/b', Left, Right);
  CheckText('splitFragment left', Left, 'file:///a/b');
  CheckText('splitFragment right', Right, '');
  CheckParts('file:///a/b', 'file', '', '/a/b', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('file:///a/b'), 5);
  CheckText('normalizeURI', NormalizeURI('/a/b/c'), '/a/b/c');
  SplitFragment('/a/b/c', Left, Right);
  CheckText('splitFragment left', Left, '/a/b/c');
  CheckText('splitFragment right', Right, '');
  CheckParts('/a/b/c', '', '', '/a/b/c', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('/a/b/c'), 0);
  CheckText('normalizeURI', NormalizeURI('a/b'), 'a/b');
  SplitFragment('a/b', Left, Right);
  CheckText('splitFragment left', Left, 'a/b');
  CheckText('splitFragment right', Right, '');
  CheckParts('a/b', '', '', 'a/b', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('a/b'), 0);
  CheckText('normalizeURI', NormalizeURI('http://a/b/c/d;p?q#frag'), 'http://a/b/c/d;p?q');
  SplitFragment('http://a/b/c/d;p?q#frag', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/b/c/d;p?q');
  CheckText('splitFragment right', Right, 'frag');
  CheckParts('http://a/b/c/d;p?q', 'http', 'a', '/b/c/d;p', 'q', True, True, True);
  CheckInt('schemeEnd', SchemeEnd('http://a/b/c/d;p?q#frag'), 5);
  CheckText('normalizeURI', NormalizeURI(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78')),
    H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'));
  SplitFragment(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), Left, Right);
  CheckText('splitFragment left', Left, H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'));
  CheckText('splitFragment right', Right, '');
  CheckParts(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78'), 'http', H('c38978616d706c652e636f6d'),
    H('2fc3a92f78'), '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd(H('687474703a2f2fc38978616d706c652e636f6d2fc3a92f78')), 5);
  CheckText('normalizeURI', NormalizeURI('mailto:x@y'), 'mailto:x@y');
  SplitFragment('mailto:x@y', Left, Right);
  CheckText('splitFragment left', Left, 'mailto:x@y');
  CheckText('splitFragment right', Right, '');
  CheckParts('mailto:x@y', 'mailto', '', 'x@y', '', True, False, False);
  CheckInt('schemeEnd', SchemeEnd('mailto:x@y'), 7);
  CheckText('normalizeURI', NormalizeURI('http://a?q'), 'http://a/?q');
  SplitFragment('http://a?q', Left, Right);
  CheckText('splitFragment left', Left, 'http://a?q');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a?q', 'http', 'a', '', 'q', True, True, True);
  CheckInt('schemeEnd', SchemeEnd('http://a?q'), 5);
  CheckText('normalizeURI', NormalizeURI('HTTP://A.B/%7e/../x#frag'), 'http://a.b/x');
  SplitFragment('HTTP://A.B/%7e/../x#frag', Left, Right);
  CheckText('splitFragment left', Left, 'HTTP://A.B/%7e/../x');
  CheckText('splitFragment right', Right, 'frag');
  CheckParts('HTTP://A.B/%7e/../x', 'HTTP', 'A.B', '/%7e/../x', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('HTTP://A.B/%7e/../x#frag'), 5);
  CheckText('normalizeURI', NormalizeURI('a/b/../c#f'), 'a/b/../c');
  SplitFragment('a/b/../c#f', Left, Right);
  CheckText('splitFragment left', Left, 'a/b/../c');
  CheckText('splitFragment right', Right, 'f');
  CheckParts('a/b/../c', '', '', 'a/b/../c', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('a/b/../c#f'), 0);
  CheckText('normalizeURI', NormalizeURI('Http://X:8080/'), 'http://x:8080/');
  SplitFragment('Http://X:8080/', Left, Right);
  CheckText('splitFragment left', Left, 'Http://X:8080/');
  CheckText('splitFragment right', Right, '');
  CheckParts('Http://X:8080/', 'Http', 'X:8080', '/', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('Http://X:8080/'), 5);
  CheckText('normalizeURI', NormalizeURI('x:'), 'x:');
  SplitFragment('x:', Left, Right);
  CheckText('splitFragment left', Left, 'x:');
  CheckText('splitFragment right', Right, '');
  CheckParts('x:', 'x', '', '', '', True, False, False);
  CheckInt('schemeEnd', SchemeEnd('x:'), 2);
  CheckText('normalizeURI', NormalizeURI('HtTp://UsEr@HoSt:80'), 'http://user@host/');
  SplitFragment('HtTp://UsEr@HoSt:80', Left, Right);
  CheckText('splitFragment left', Left, 'HtTp://UsEr@HoSt:80');
  CheckText('splitFragment right', Right, '');
  CheckParts('HtTp://UsEr@HoSt:80', 'HtTp', 'UsEr@HoSt:80', '', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('HtTp://UsEr@HoSt:80'), 5);
  CheckText('normalizeURI', NormalizeURI('https://h:443/'), 'https://h/');
  SplitFragment('https://h:443/', Left, Right);
  CheckText('splitFragment left', Left, 'https://h:443/');
  CheckText('splitFragment right', Right, '');
  CheckParts('https://h:443/', 'https', 'h:443', '/', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('https://h:443/'), 6);
  CheckText('normalizeURI', NormalizeURI('https://h:80'), 'https://h:80/');
  SplitFragment('https://h:80', Left, Right);
  CheckText('splitFragment left', Left, 'https://h:80');
  CheckText('splitFragment right', Right, '');
  CheckParts('https://h:80', 'https', 'h:80', '', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('https://h:80'), 6);
  CheckText('normalizeURI', NormalizeURI('http://h:443/a?B=C'), 'http://h:443/a?B=C');
  SplitFragment('http://h:443/a?B=C', Left, Right);
  CheckText('splitFragment left', Left, 'http://h:443/a?B=C');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://h:443/a?B=C', 'http', 'h:443', '/a', 'B=C', True, True, True);
  CheckInt('schemeEnd', SchemeEnd('http://h:443/a?B=C'), 5);
  CheckText('normalizeURI', NormalizeURI('URN:ISBN:1'), 'urn:ISBN:1');
  SplitFragment('URN:ISBN:1', Left, Right);
  CheckText('splitFragment left', Left, 'URN:ISBN:1');
  CheckText('splitFragment right', Right, '');
  CheckParts('URN:ISBN:1', 'URN', '', 'ISBN:1', '', True, False, False);
  CheckInt('schemeEnd', SchemeEnd('URN:ISBN:1'), 4);
  CheckText('normalizeURI', NormalizeURI('#f'), '');
  SplitFragment('#f', Left, Right);
  CheckText('splitFragment left', Left, '');
  CheckText('splitFragment right', Right, 'f');
  CheckParts('', '', '', '', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('#f'), 0);
end;

procedure TestNormalizeURI2;
var
  Left, Right: UTF8String;
begin
  CheckText('normalizeURI', NormalizeURI('a#'), 'a');
  SplitFragment('a#', Left, Right);
  CheckText('splitFragment left', Left, 'a');
  CheckText('splitFragment right', Right, '');
  CheckParts('a', '', '', 'a', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('a#'), 0);
  CheckText('normalizeURI', NormalizeURI('http://a/b/c/.'), 'http://a/b/c/');
  SplitFragment('http://a/b/c/.', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/b/c/.');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/b/c/.', 'http', 'a', '/b/c/.', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/b/c/.'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/b/c/..'), 'http://a/b/');
  SplitFragment('http://a/b/c/..', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/b/c/..');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/b/c/..', 'http', 'a', '/b/c/..', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/b/c/..'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/..'), 'http://a/');
  SplitFragment('http://a/..', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/..');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/..', 'http', 'a', '/..', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/..'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/../..'), 'http://a/');
  SplitFragment('http://a/../..', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/../..');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/../..', 'http', 'a', '/../..', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/../..'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/./'), 'http://a/');
  SplitFragment('http://a/./', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/./');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/./', 'http', 'a', '/./', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/./'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/b//c/../d'), 'http://a/b//d');
  SplitFragment('http://a/b//c/../d', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/b//c/../d');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/b//c/../d', 'http', 'a', '/b//c/../d', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/b//c/../d'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/...'), 'http://a/...');
  SplitFragment('http://a/...', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/...');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/...', 'http', 'a', '/...', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/...'), 5);
  CheckText('normalizeURI', NormalizeURI('http://a/.x/..y'), 'http://a/.x/..y');
  SplitFragment('http://a/.x/..y', Left, Right);
  CheckText('splitFragment left', Left, 'http://a/.x/..y');
  CheckText('splitFragment right', Right, '');
  CheckParts('http://a/.x/..y', 'http', 'a', '/.x/..y', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('http://a/.x/..y'), 5);
  CheckText('normalizeURI', NormalizeURI('1http://a'), '1http://a');
  SplitFragment('1http://a', Left, Right);
  CheckText('splitFragment left', Left, '1http://a');
  CheckText('splitFragment right', Right, '');
  CheckParts('1http://a', '', '', '1http://a', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('1http://a'), 0);
  CheckText('normalizeURI', NormalizeURI('a+b-c.d://X/'), 'a+b-c.d://x/');
  SplitFragment('a+b-c.d://X/', Left, Right);
  CheckText('splitFragment left', Left, 'a+b-c.d://X/');
  CheckText('splitFragment right', Right, '');
  CheckParts('a+b-c.d://X/', 'a+b-c.d', 'X', '/', '', True, True, False);
  CheckInt('schemeEnd', SchemeEnd('a+b-c.d://X/'), 8);
  CheckText('normalizeURI', NormalizeURI('a b://X/'), 'a b://X/');
  SplitFragment('a b://X/', Left, Right);
  CheckText('splitFragment left', Left, 'a b://X/');
  CheckText('splitFragment right', Right, '');
  CheckParts('a b://X/', '', '', 'a b://X/', '', False, False, False);
  CheckInt('schemeEnd', SchemeEnd('a b://X/'), 0);
end;

procedure TestRemoveDotSegments;
begin
  CheckText('removeDotSegments', RemoveDotSegments(''), '');
  CheckText('removeDotSegments', RemoveDotSegments('/'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('a'), 'a');
  CheckText('removeDotSegments', RemoveDotSegments('.'), '');
  CheckText('removeDotSegments', RemoveDotSegments('..'), '');
  CheckText('removeDotSegments', RemoveDotSegments('/.'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('/..'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('a/.'), 'a/');
  CheckText('removeDotSegments', RemoveDotSegments('a/..'), '');
  CheckText('removeDotSegments', RemoveDotSegments('/a/b/../../..'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('a/b/../../..'), '');
  CheckText('removeDotSegments', RemoveDotSegments('../a'), 'a');
  CheckText('removeDotSegments', RemoveDotSegments('../../a'), 'a');
  CheckText('removeDotSegments', RemoveDotSegments('/../a'), '/a');
  CheckText('removeDotSegments', RemoveDotSegments('./a'), 'a');
  CheckText('removeDotSegments', RemoveDotSegments('a/./b'), 'a/b');
  CheckText('removeDotSegments', RemoveDotSegments('a//b/..'), 'a//');
  CheckText('removeDotSegments', RemoveDotSegments('a/b/../'), 'a/');
  CheckText('removeDotSegments', RemoveDotSegments('/a/b/c/./../../g'), '/a/g');
  CheckText('removeDotSegments', RemoveDotSegments('mid/content=5/../6'), 'mid/6');
  CheckText('removeDotSegments', RemoveDotSegments('a.b/c'), 'a.b/c');
  CheckText('removeDotSegments', RemoveDotSegments('...'), '...');
  CheckText('removeDotSegments', RemoveDotSegments('a/.../b'), 'a/.../b');
  CheckText('removeDotSegments', RemoveDotSegments('//'), '//');
  CheckText('removeDotSegments', RemoveDotSegments('/./'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('/../'), '/');
  CheckText('removeDotSegments', RemoveDotSegments('./'), '');
  CheckText('removeDotSegments', RemoveDotSegments('../'), '');
  CheckText('removeDotSegments', RemoveDotSegments('a/../..'), '');
  CheckText('removeDotSegments', RemoveDotSegments('a/../../b'), 'b');
  CheckText('removeDotSegments', RemoveDotSegments('.a'), '.a');
  CheckText('removeDotSegments', RemoveDotSegments('a.'), 'a.');
  CheckText('removeDotSegments', RemoveDotSegments('/.a/..b/'), '/.a/..b/');
end;

procedure TestDecodeFragment;
begin
  CheckText('decodeFragment', DecodeFragment(''), '');
  CheckText('decodeFragment', DecodeFragment('a'), 'a');
  CheckText('decodeFragment', DecodeFragment('%41'), 'A');
  CheckText('decodeFragment', DecodeFragment('%4'), '%4');
  CheckText('decodeFragment', DecodeFragment('%'), '%');
  CheckText('decodeFragment', DecodeFragment('a%'), 'a%');
  CheckText('decodeFragment', DecodeFragment('a%4'), 'a%4');
  CheckText('decodeFragment', DecodeFragment('a%41'), 'aA');
  CheckText('decodeFragment', DecodeFragment('%41%42c'), 'ABc');
  CheckText('decodeFragment', DecodeFragment('%zz'), '%zz');
  CheckText('decodeFragment', DecodeFragment('%4g'), '%4g');
  CheckText('decodeFragment', DecodeFragment('%g4'), '%g4');
  CheckText('decodeFragment', DecodeFragment('%C3%A9'), H('c3a9'));
  CheckText('decodeFragment', DecodeFragment('%c3%a9'), H('c3a9'));
  CheckText('decodeFragment', DecodeFragment('%C3'), '%C3');
  CheckText('decodeFragment', DecodeFragment('%FF'), '%FF');
  CheckText('decodeFragment', DecodeFragment('%E2%82%AC'), H('e282ac'));
  CheckText('decodeFragment', DecodeFragment('%ED%A0%80'), '%ED%A0%80');
  CheckText('decodeFragment', DecodeFragment('%C0%80'), '%C0%80');
  CheckText('decodeFragment', DecodeFragment('%F4%90%80%80'), '%F4%90%80%80');
  CheckText('decodeFragment', DecodeFragment('%F0%9F%98%80'), H('f09f9880'));
  CheckText('decodeFragment', DecodeFragment('/a%20b/c%2Fd'), '/a b/c/d');
  CheckText('decodeFragment', DecodeFragment('%25'), '%');
  CheckText('decodeFragment', DecodeFragment('%2541'), '%41');
  CheckText('decodeFragment', DecodeFragment(H('c3a9253431')), H('c3a941'));
  CheckText('decodeFragment', DecodeFragment('%00'), H('00'));
  CheckText('decodeFragment', DecodeFragment('a%41%'), 'a%41%');
end;

procedure TestPointerTokens;
begin
  CheckText('escapePointerToken', EscapePointerToken(''), '');
  CheckText('unescapePointerToken', UnescapePointerToken(''), '');
  CheckText('escapePointerToken', EscapePointerToken('a'), 'a');
  CheckText('unescapePointerToken', UnescapePointerToken('a'), 'a');
  CheckText('escapePointerToken', EscapePointerToken('~'), '~0');
  CheckText('unescapePointerToken', UnescapePointerToken('~'), '~');
  CheckText('escapePointerToken', EscapePointerToken('/'), '~1');
  CheckText('unescapePointerToken', UnescapePointerToken('/'), '/');
  CheckText('escapePointerToken', EscapePointerToken('~/'), '~0~1');
  CheckText('unescapePointerToken', UnescapePointerToken('~/'), '~/');
  CheckText('escapePointerToken', EscapePointerToken('/~'), '~1~0');
  CheckText('unescapePointerToken', UnescapePointerToken('/~'), '/~');
  CheckText('escapePointerToken', EscapePointerToken('a~b/c'), 'a~0b~1c');
  CheckText('unescapePointerToken', UnescapePointerToken('a~b/c'), 'a~b/c');
  CheckText('escapePointerToken', EscapePointerToken('~0'), '~00');
  CheckText('unescapePointerToken', UnescapePointerToken('~0'), '~');
  CheckText('escapePointerToken', EscapePointerToken('~1'), '~01');
  CheckText('unescapePointerToken', UnescapePointerToken('~1'), '/');
  CheckText('escapePointerToken', EscapePointerToken('~~//'), '~0~0~1~1');
  CheckText('unescapePointerToken', UnescapePointerToken('~~//'), '~~//');
  CheckText('escapePointerToken', EscapePointerToken(H('c3a92f7e')), H('c3a97e317e30'));
  CheckText('unescapePointerToken', UnescapePointerToken(H('c3a92f7e')), H('c3a92f7e'));
end;

procedure TestAsciiLower;
begin
  CheckText('asciiLower', AsciiLower(H('485454503a2f2fc38958414d504c452e436f6d2fc4b0')),
    H('687474703a2f2fc38978616d706c652e636f6d2fc4b0'));
  CheckText('asciiLower', AsciiLower(''), '');
  CheckText('asciiLower', AsciiLower('abc'), 'abc');
  CheckText('asciiLower', AsciiLower('ABC'), 'abc');
  CheckText('asciiLower', AsciiLower('aBc[]@`{'), 'abc[]@`{');
  CheckText('asciiLower', AsciiLower(H('ffc341')), H('ffc361'));
end;

procedure CheckIndex(const Token: UTF8String; WantOK: Boolean; WantIndex: Int32);
var
  Index: Int32;
  OK: Boolean;
begin
  OK := PointerArrayIndex(Token, Index);
  CheckBool('pointerArrayIndex of ' + Token, OK, WantOK);
  if OK and WantOK then CheckInt('pointerArrayIndex of ' + Token, Index, WantIndex);
end;

var
  PointerDocument: TDocument;

// CheckPointer resolves a pointer against the document, and checks the kind of the value it finds (-1 for none),
// its normalised pointer and the value as JSON text.
procedure CheckPointer(const Pointer: UTF8String; WantKind: Int32; const WantPath, WantJson: UTF8String);
var
  Value, Kind: Int32;
  Path, Json: UTF8String;
  Found: TDocument;
begin
  Value := ResolvePointer(PointerDocument, PointerDocument.Root, Pointer, Path);
  Kind := -1;
  Json := '';
  if Value >= 0 then begin
    Kind := DocKind(PointerDocument, Value);
    // The value as text: the document, read from the value.
    Found := PointerDocument;
    Found.Root := Value;
    Json := DocumentToJson(Found);
  end;
  CheckInt('resolvePointer kind of ' + Pointer, Kind, WantKind);
  CheckText('resolvePointer path of ' + Pointer, Path, WantPath);
  CheckText('resolvePointer value of ' + Pointer, Json, WantJson);
end;

procedure TestResolvePointer;
var
  Error: TParseError;
begin
  if not ParseDocumentString('{"a":[10,{"b~/":"x"},[]],"":{"":1},"0":true,"m~n":null,' + H('22c3a922') +
    ':{"1":"s"}}', PointerDocument, Error) then begin
    Inc(Failures);
    WriteLn('FAIL: resolvePointer: the document does not parse: ', ParseErrorText(Error));
    Exit;
  end;
  CheckPointer('', 4, '',
    H('7b2261223a5b31302c7b22627e2f223a2278227d2c5b5d5d2c22223a7b22223a317d2c2230223a747275652c226d7e6e223a' +
    '6e756c6c2c22c3a9223a7b2231223a2273227d7d'));
  CheckPointer('/a', 8, '/a', '[10,{"b~/":"x"},[]]');
  CheckPointer('/a/0', 16, '/a/0', '10');
  CheckPointer('/a/1/b~0~1', 32, '/a/1/b~0~1', '"x"');
  CheckPointer('/a/01', -1, '', '');
  CheckPointer('/a/3', -1, '', '');
  CheckPointer('/a/-', -1, '', '');
  CheckPointer('/a/2', 8, '/a/2', '[]');
  CheckPointer('/a/2/0', -1, '', '');
  CheckPointer('/', 4, '/', '{"":1}');
  CheckPointer('//', 16, '//', '1');
  CheckPointer('/0', 2, '/0', 'true');
  CheckPointer('/0/x', -1, '', '');
  CheckPointer('a', -1, '', '');
  CheckPointer('/m~0n', 1, '/m~0n', 'null');
  CheckPointer('/m~n', 1, '/m~0n', 'null');
  CheckPointer('/~01', -1, '', '');
  CheckPointer('/a/99999999999999999999', -1, '', '');
  CheckPointer('/a/1', 4, '/a/1', '{"b~/":"x"}');
  CheckPointer('/a/1/', -1, '', '');
  CheckPointer('/a/', -1, '', '');
  CheckPointer('/a/00', -1, '', '');
  CheckPointer('/a/1x', -1, '', '');
  CheckPointer(H('2fc3a92f31'), 32, H('2fc3a92f31'), '"s"');
  CheckPointer(H('2fc3a92f3031'), -1, '', '');
  CheckPointer('/b', -1, '', '');
  CheckPointer('/a/0/0', -1, '', '');
  CheckPointer('/a/+1', -1, '', '');
  CheckPointer('/a/2147483648', -1, '', '');
end;

// The tokens ResolvePointer takes as an index into an array: "0", or digits with no leading zero.
procedure TestPointerArrayIndex;
begin
  CheckIndex('0', True, 0);
  CheckIndex('1', True, 1);
  CheckIndex('10', True, 10);
  CheckIndex('2147483647', True, 2147483647);
  CheckIndex('2147483648', False, 0);
  CheckIndex('99999999999999999999999', False, 0);
  CheckIndex('', False, 0);
  CheckIndex('00', False, 0);
  CheckIndex('01', False, 0);
  CheckIndex('-1', False, 0);
  CheckIndex('+1', False, 0);
  CheckIndex('1a', False, 0);
  CheckIndex('a', False, 0);
  CheckIndex(' 1', False, 0);
  CheckIndex('-', False, 0);
end;

procedure TestHelpers;
var
  B: Int32;
begin
  for B := 0 to 255 do begin
    CheckBool('isASCIILetter', IsASCIILetter(Byte(B)),
      ((B >= Ord('a')) and (B <= Ord('z'))) or ((B >= Ord('A')) and (B <= Ord('Z'))));
    CheckBool('isASCIIDigit', IsASCIIDigit(Byte(B)), (B >= Ord('0')) and (B <= Ord('9')));
  end;
  CheckInt('hexValue', HexValue(Ord('0')), 0);
  CheckInt('hexValue', HexValue(Ord('9')), 9);
  CheckInt('hexValue', HexValue(Ord('a')), 10);
  CheckInt('hexValue', HexValue(Ord('f')), 15);
  CheckInt('hexValue', HexValue(Ord('A')), 10);
  CheckInt('hexValue', HexValue(Ord('F')), 15);
  CheckInt('hexValue', HexValue(Ord('g')), -1);
  CheckInt('hexValue', HexValue(Ord('G')), -1);
  CheckInt('hexValue', HexValue(Ord('/')), -1);
  CheckInt('hexValue', HexValue(Ord(':')), -1);
  CheckInt('hexValue', HexValue(Ord('@')), -1);
  CheckInt('hexValue', HexValue(Ord('`')), -1);
  CheckInt('hexValue', HexValue(0), -1);
  CheckInt('hexValue', HexValue(255), -1);
  CheckBool('utf8Equal', Utf8Equal('', ''), True);
  CheckBool('utf8Equal', Utf8Equal('a', 'a'), True);
  CheckBool('utf8Equal', Utf8Equal('a', 'b'), False);
  CheckBool('utf8Equal', Utf8Equal('a', 'ab'), False);
  CheckBool('utf8Equal', Utf8Equal(H('c389'), H('c389')), True);
  CheckBool('utf8Equal', Utf8Equal(H('c389'), H('c388')), False);
end;

begin
  TestResolveURI1;
  TestResolveURI2;
  TestResolveURI3;
  TestResolveURI4;
  TestResolveURI5;
  TestNormalizeURI1;
  TestNormalizeURI2;
  TestRemoveDotSegments;
  TestDecodeFragment;
  TestPointerTokens;
  TestAsciiLower;
  TestResolvePointer;
  TestPointerArrayIndex;
  TestHelpers;
  WriteLn('TestUri: ', Checks, ' checks, ', Checks - Failures, ' passed, ', Failures, ' failed');
  if Failures <> 0 then Halt(1);
end.
