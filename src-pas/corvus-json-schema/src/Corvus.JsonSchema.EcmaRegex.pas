unit Corvus.JsonSchema.EcmaRegex;
{$I corvus.inc}

{ Unit EcmaRegex matches ECMA-262 regular expressions against UTF-8 text, with the semantics JSON Schema requires
  of the pattern and patternProperties keywords and of the regex format. It is a port of the ecmaregex package of
  the Go module (src-go/corvus-json-schema/internal/ecmaregex).

  Unicode mode

  A pattern is read first with the grammar of the ECMA-262 u flag, which is what JSON Schema specifies. A pattern
  that is not valid under that grammar is then read with the Annex B grammar of a pattern with no flag, which
  accepts what many schemas in the wild contain (identity escapes such as \&, a lone brace, legacy octal escapes).
  Only a pattern that neither grammar accepts is an error. This is the choice the Rust, Java and Go ports make.
  EcmaRegexValid, which backs the regex format, accepts only the u flag grammar, as those ports do.

  A pattern the u flag grammar accepts is matched by Unicode code point over the UTF-8 text. The dot, a class and a
  quantifier each see a character beyond the Basic Multilingual Plane as one character, and a surrogate pair
  written as two \u escapes names one code point.

  A pattern that only the Annex B grammar accepts is matched by UTF-16 code unit, as ECMA-262 matches a pattern
  with no u flag, and as the Go package does. A character beyond the Basic Multilingual Plane is then two
  characters, a high surrogate and a low one, to the dot, a class, a quantifier, a group and a backreference. A
  match can start or end between the two. Such a character in the pattern is two atoms, and a class range between
  two of them is an error. Case is folded by the Canonicalize of a pattern with no u flag. The text stays UTF-8 and
  no UTF-16 copy of it is made (see Corvus.JsonSchema.EcmaRegex.Utf8). Only a text with a character beyond the
  Basic Multilingual Plane is read differently by the two.

  Malformed UTF-8 in the text is read one byte at a time as U+FFFD. The text is UTF-8, so it cannot hold half of a
  surrogate pair, and a pattern that names one, such as \ud800, matches it only as half of a character and only
  when the pattern has no u flag.

  JSON Schema gives a pattern no flags, so matching is case sensitive, ^ and $ match only at the ends of the text,
  and the dot excludes the four line terminators. A pattern can change that for part of itself with a modifier
  group of ECMAScript 2025, such as (?i:...), (?m:...) or (?s:...).

  Back ends, and the one difference from the Go package

  The Go package hands a pattern with no lookaround, no backreference, no multiline anchor and no case-insensitive
  word boundary to the regexp package of the Go standard library, in a translation that spells out every literal
  and class as code points. Pascal has no such library and this unit uses none, so the translation (re2.go) is not
  ported. Every pattern runs on the two matchers of the package itself.

  A pattern of that kind gets a deterministic automaton over ASCII text when it is small enough and has no word
  boundary (see Corvus.JsonSchema.EcmaRegex.Dfa), which decides an ASCII text with one table read per byte. Every
  other case runs on the backtracking matcher (see Corvus.JsonSchema.EcmaRegex.Vm): a pattern with no automaton,
  and a text that is not ASCII for a pattern that has one. That matcher keeps its choices on an explicit stack that
  the thread keeps between matches, so a long text cannot overflow the stack of the thread and a match does not
  allocate once the stack has grown. A pattern with nested quantifiers can take exponential time on a text it does
  not match, as it can in any backtracking engine, so a match can be given a limit (EcmaRegexIsMatchLimit): the
  matcher counts its steps, reads the clock every 4,096 of them, and abandons a match that has run for longer than
  the limit or whose stack has reached 1,048,576 entries. In Go only a pattern with a lookaround or a
  backreference can take that long, because the regexp package matches the others in linear time. Here a pattern
  that has no automaton can, and so can any pattern on a text that is not ASCII. A repetition whose body
  cannot consume a character is not such a pattern. It is run once, or not at all (see Simplify of the Compile
  unit), as V8 runs it. And a counted loop gives up at a position from which the rest of the pattern needs more
  text than is left (see SetNeeds of the Compile unit), as V8 does, so a loop whose iterations can be empty does
  not try every combination of them there.

  The automaton is built when the pattern is compiled, where Go builds it on the first match. A compiled pattern is
  then a value that nothing writes to, which is what lets threads share it without a lock.

  Coverage and limits

  The grammar is that of ECMAScript 2025. It includes lookbehind, named groups, group names shared between separate
  alternatives, modifier groups, and Unicode property escapes for every property, value and alias ECMA-262 lists,
  with Unicode 17 data. The properties and the case folding come from the tables of the Ucd unit and never from the
  run-time library of the compiler, so a pattern matches the same texts whichever compiler built the program. The v
  flag (set notation and properties of strings) does not apply, because a JSON Schema pattern has no flags.

  A pattern has three limits of size, which are those of the Go package: 1,048,576 bytes of length, groups 1,000
  deep, and a size of 1,048,576 bytes as the parser counts it (see MaxSize of the Parse unit). A pattern over one
  is refused as too large, which is not the error of a pattern that is not valid.

  The units

  Corvus.JsonSchema.EcmaRegex.CharSet is charset.go and fold.go, .Parse is parse.go, .Compile is compile.go, .Vm is
  vm.go and .Dfa is dfa.go. .Utf8 holds what the Go source takes from the unicode/utf8 package. This unit is
  ecmaregex.go, and the one other code uses. }

interface

uses
  SysUtils,
  Corvus.JsonSchema.EcmaRegex.Compile,
  Corvus.JsonSchema.EcmaRegex.Dfa;

type
  { A TEcmaRegex is a compiled ECMA-262 pattern. It is a value: nothing writes to it once it is compiled, so it
    needs no Free and any number of threads may match with one at the same time. Its fields are for the engine and
    its tests. }
  TEcmaRegex = record
    { Unicode says the pattern was read with the u flag grammar. When it is false, the pattern is valid only with
      the Annex B grammar. }
    Unicode: Boolean;
    { Regular says the pattern has no construct that only the backtracking matcher can run, and no counted
      quantifier beyond what the regexp package of Go accepts. It is the patterns the Go package runs on the regexp
      package, and the ones tried for an automaton here. }
    Regular: Boolean;
    { HasDfa says the pattern has an automaton over ASCII text, which is then in Dfa. }
    HasDfa: Boolean;
    Dfa: TDfa;
    { Prog is the pattern compiled for the backtracking matcher. }
    Prog: TProgram;
  end;

  { A TEcmaRegexLimit names the limit of size a pattern is over, and gives its value. Name is empty for a pattern
    that is over none. The names are those of the Parse unit: length (the bytes of the pattern, 1,048,576),
    nesting (the depth of its groups, 1,000) and size (the bytes counted for it as it is read, 1,048,576). }
  TEcmaRegexLimit = record
    Name: UTF8String;
    Value: Int32;
  end;

{ EcmaRegexCompile compiles an ECMA-262 pattern with the semantics JSON Schema requires: the u flag grammar first,
  then Annex B. It returns false, with a message, for a pattern that is not valid, and for a pattern over a limit
  of size, whether or not it is valid: Limit then names the limit. A pattern over a limit is not read to its end,
  so nothing is known of whether it is valid. }
function EcmaRegexCompile(const Pattern: UTF8String; out Regex: TEcmaRegex; out Error: UTF8String;
  out Limit: TEcmaRegexLimit): Boolean; overload;
{ EcmaRegexCompile without the limit: false for a pattern that is not valid and for one that is too large. }
function EcmaRegexCompile(const Pattern: UTF8String; out Regex: TEcmaRegex; out Error: UTF8String): Boolean;
  overload;

{ EcmaRegexCheck reads the pattern without compiling it. It returns what EcmaRegexCompile would: true for a pattern
  either grammar accepts, with Unicode saying whether the u flag grammar does, and false with the message for a
  pattern that is not valid or is over a limit of size, which Limit then names. }
function EcmaRegexCheck(const Pattern: UTF8String; out Unicode: Boolean; out Error: UTF8String;
  out Limit: TEcmaRegexLimit): Boolean;

{ EcmaRegexSize is the size the parser counts for a pattern (see MaxSize of the Parse unit) under the u flag
  grammar, or under the Annex B grammar when Unicode is false. It is -1 for a pattern that grammar does not accept,
  and for one that is too large. It is for the tests that pin the size against the Go package's. }
function EcmaRegexSize(const Pattern: UTF8String; Unicode: Boolean): Int32;

{ EcmaRegexValid reports whether the pattern is a valid ECMA-262 pattern (for format: regex). It applies the grammar
  of the u flag alone. A pattern over a limit of size is not read to its end and is reported as not valid. }
function EcmaRegexValid(const Pattern: UTF8String): Boolean;

{ EcmaRegexIsMatch reports whether the pattern matches anywhere in the bytes Text[Start .. Start+Len-1], which are
  UTF-8 (an unanchored search). It allocates nothing in the steady state and is safe for one TEcmaRegex used from
  several threads. }
function EcmaRegexIsMatch(const Regex: TEcmaRegex; const Text: TBytes; Start, Len: Int32): Boolean;

const
  { Why a match with a limit was abandoned (the StopNone, StopTime and StopMemory of the Vm unit). }
  EcmaRegexStopNone = 0;
  EcmaRegexStopTime = 1;
  EcmaRegexStopMemory = 2;

{ EcmaRegexIsMatchLimit is EcmaRegexIsMatch with a limit on the time the match may take, in milliseconds, when
  LimitMs is above zero. A match that reaches the limit is abandoned and Stop is EcmaRegexStopTime. So is a match
  whose backtracking stack reaches 1,048,576 entries, and Stop is EcmaRegexStopMemory. The result is then False
  and is not an answer. The clock is read every 4,096 steps of the backtracking matcher, so a match can go past the
  limit by the time those take and by the millisecond the clock counts in. The automaton takes time proportional
  to the text and is not stopped. }
function EcmaRegexIsMatchLimit(const Regex: TEcmaRegex; const Text: TBytes; Start, Len: Int32; LimitMs: Int64;
  out Stop: UInt8): Boolean;

{ EcmaRegexReleaseThreadScratch frees the working memory the calling thread's matches have kept. A thread that has
  matched should call it before it ends, because a thread variable of a managed type is not freed with its thread.
  Calling it at any other time costs only the allocations of the next match. }
procedure EcmaRegexReleaseThreadScratch;

implementation

uses
  Corvus.JsonSchema.EcmaRegex.Parse,
  Corvus.JsonSchema.EcmaRegex.Vm;

const
  { The largest count the regexp package of Go accepts in a counted quantifier, and the largest product of the
    counts of nested ones. }
  MaxRegexpRepeat = 1000;

{ IsCounted reports whether a quantifier is one the Go package writes with braces for the regexp package: every one
  but *, + and ?. }
function IsCounted(const Node: TNode): Boolean;
begin
  Result := not (((Node.Min = 0) and (Node.Max = Unbounded)) or ((Node.Min = 1) and (Node.Max = Unbounded)) or
    ((Node.Min = 0) and (Node.Max = 1)));
end;

{ RepeatIsValid reports whether the counted quantifiers at and below N nest to a product of at most Limit. It is
  the rule of the same name in the regexp/syntax package of Go. }
function RepeatIsValid(const Nodes: TNodes; N, Limit: Int32): Boolean;
var
  M, K: Int32;
begin
  if (Nodes[N].Kind = NRepeat) and IsCounted(Nodes[N]) then begin
    M := Nodes[N].Max;
    if M = 0 then
      Exit(True);
    if M < 0 then
      M := Nodes[N].Min;
    if M > Limit then
      Exit(False);
    if M > 0 then
      Limit := Limit div M;
  end;
  for K := 0 to High(Nodes[N].Subs) do
    if not RepeatIsValid(Nodes, Nodes[N].Subs[K], Limit) then
      Exit(False);
  Result := True;
end;

{ WithinRegexpLimits reports whether every counted quantifier at and below N is one the regexp package of Go
  accepts. The Go package runs a pattern on the backtracking matcher alone when the regexp package refuses its
  translation, which it does for these quantifiers. The rule is kept here so that the same patterns are tried for
  an automaton, and because it bounds the work of building one (see BuildDFA). The other limits of the regexp
  package, on the size and the depth of a pattern, are not kept: a pattern beyond them is too large for an
  automaton in any case. }
function WithinRegexpLimits(const Nodes: TNodes; N: Int32): Boolean;
var
  K: Int32;
begin
  for K := 0 to High(Nodes[N].Subs) do
    if not WithinRegexpLimits(Nodes, Nodes[N].Subs[K]) then
      Exit(False);
  if (Nodes[N].Kind = NRepeat) and IsCounted(Nodes[N]) then begin
    if (Nodes[N].Min > MaxRegexpRepeat) or (Nodes[N].Max > MaxRegexpRepeat) then
      Exit(False);
    if ((Nodes[N].Min >= 2) or (Nodes[N].Max >= 2)) and not RepeatIsValid(Nodes, N, MaxRegexpRepeat) then
      Exit(False);
  end;
  Result := True;
end;

{ LimitOf is the limit of size a parser stopped at. }
function LimitOf(const Ps: TParser): TEcmaRegexLimit;
begin
  Result.Name := Ps.LimitName;
  Result.Value := Ps.LimitValue;
end;

function EcmaRegexCompile(const Pattern: UTF8String; out Regex: TEcmaRegex; out Error: UTF8String): Boolean;
var
  Limit: TEcmaRegexLimit;
begin
  Result := EcmaRegexCompile(Pattern, Regex, Error, Limit);
end;

function EcmaRegexCheck(const Pattern: UTF8String; out Unicode: Boolean; out Error: UTF8String;
  out Limit: TEcmaRegexLimit): Boolean;
var
  Ps: TParser;
  Root: Int32;
  LegacyError: UTF8String;
begin
  Limit := Default(TEcmaRegexLimit);
  Unicode := Parse(Pattern, True, Ps, Root, Error);
  if Unicode then
    Exit(True);
  if Ps.TooLarge then begin
    Limit := LimitOf(Ps);
    Exit(False);
  end;
  if not Parse(Pattern, False, Ps, Root, LegacyError) then begin
    if Ps.TooLarge then begin
      Limit := LimitOf(Ps);
      Error := LegacyError;
    end;
    Exit(False);
  end;
  Error := '';
  Result := True;
end;

function EcmaRegexSize(const Pattern: UTF8String; Unicode: Boolean): Int32;
var
  Ps: TParser;
  Root: Int32;
  Error: UTF8String;
begin
  if Parse(Pattern, Unicode, Ps, Root, Error) then
    Result := Ps.Size
  else
    Result := -1;
end;

function EcmaRegexCompile(const Pattern: UTF8String; out Regex: TEcmaRegex; out Error: UTF8String;
  out Limit: TEcmaRegexLimit): Boolean;
var
  Ps: TParser;
  Root: Int32;
  Unicode: Boolean;
  LegacyError: UTF8String;
begin
  Regex := Default(TEcmaRegex);
  Limit := Default(TEcmaRegexLimit);
  Unicode := True;
  if not Parse(Pattern, True, Ps, Root, Error) then begin
    if Ps.TooLarge then begin
      Limit := LimitOf(Ps);
      Exit(False);
    end;
    Unicode := False;
    if not Parse(Pattern, False, Ps, Root, LegacyError) then begin
      if Ps.TooLarge then begin
        Limit := LimitOf(Ps);
        Error := LegacyError;
      end;
      Exit(False);
    end;
    Error := '';
  end;
  Regex.Unicode := Unicode;
  Regex.Regular := (not Ps.NeedsBacktracking) and WithinRegexpLimits(Ps.Nodes, Root);
  if Regex.Regular then begin
    Regex.HasDfa := BuildDFA(Ps, Root, Regex.Dfa);
    if not Regex.HasDfa then
      Regex.Dfa := Default(TDfa);
  end;
  Regex.Prog := CompileProgram(Ps, Root, Unicode);
  Result := True;
end;

function EcmaRegexIsMatch(const Regex: TEcmaRegex; const Text: TBytes; Start, Len: Int32): Boolean;
var
  Stop: UInt8;
begin
  Result := EcmaRegexIsMatchLimit(Regex, Text, Start, Len, 0, Stop);
end;

function EcmaRegexIsMatchLimit(const Regex: TEcmaRegex; const Text: TBytes; Start, Len: Int32; LimitMs: Int64;
  out Stop: UInt8): Boolean;
var
  Matched: Boolean;
begin
  Stop := EcmaRegexStopNone;
  if Regex.HasDfa and DfaMatch(Regex.Dfa, Text, Start, Start + Len, Matched) then
    Exit(Matched);
  Result := ProgramMatchLimit(Regex.Prog, Text, Start, Start + Len, LimitMs, Stop);
end;

function EcmaRegexValid(const Pattern: UTF8String): Boolean;
var
  Ps: TParser;
  Root: Int32;
  Error: UTF8String;
begin
  Result := Parse(Pattern, True, Ps, Root, Error);
end;

procedure EcmaRegexReleaseThreadScratch;
begin
  ReleaseThreadState;
end;

end.