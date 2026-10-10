package jsonschema

import (
	"encoding/json"
	"strings"
	"testing"

	"github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ecmaregex"
)

var testPatterns = []string{
	"", ".*", "^.*", ".*$", `[\s\S]*`, `^[\s\S]*`, `^[\s\S]*$`, "^.*$", "^[@$_#]", `^[a-zA-Z0-9_\.\-\|@#]*$`,
	`^[a-zA-Z0-9_\-]*$`, ".+", `^\{\{[^\W\.\-][\w\.\-]*\}\}$`, "^.{1,256}$", `^[A-Z0-9_\-\/]+$`,
	`^[a-zA-Z0-9_\.\-]+[\|]?[a-zA-Z0-9_\.\-]+$`,
	`^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$`, "^x-", "^[1-5](?:[0-9]{2}|XX)$",
	"(base64key|awskms)://(.*)", "^[A-F0-9]{1,32}$", "^[a-z][a-z0-9]{0,29}$",
	"^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])$", `^[\w\*]{0,60}$`, `^(?:@[0-9a-z-_.]+\/)?[a-z][0-9a-z-_.]*$`,
	"^[a-z][a-z0-9_]+$", `^\d+[:-]\d+$`, "^#[0-9a-fA-F]{6}$", "^[^:]+:[^:]+$", `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$`,
	`^.*\.(?:txt|trie)(?:\.gz)?$`, `^([-\w_\s]+)(,[-\w_\s]+)*$`, `^(!?[-\w_\s]+)|(\*)$`,
	"^[0-9]+(ns|ms|us|µs|s|m|h)$", `^\/[^\*\?\&\%]*(\/\*)?$`, "^[^- @#$%^&()!]+$", `^((\.(?!\.)\/)?\w+\/?)+$`,
	"^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$", `\{.*\}`, "^[a-z]{1,2}$", "^abc$", `^\/`, "^es$",
	`^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$`,
	"^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$", "^[a-z]*a$", "^a*a", "^a+b?a", "^a+b?c*a$", "^[a-z]+-?[a-z]+$", `\bfoo`,
	`^\p{L}+$`, `é`, `😀`, `[^\d]x`, `^\S+$`, "a{2}b{1,}c{0,3}", "(?<name>ab)+", `^[\b]`, `\x41`, ".",
	"^.", "^.+", "(.*)", "^(.*)", "^.+$", "^.{1,3}$", "^.{2}$", "^.{2,}$", "^(ab|cd)$", `^(?:es|ES|x-|a\.b)$`,
	"^(a|b|c|d|e|f|g|h|i|j|z)$", "^ab|cd$", "^x-|es|ms$", "a|b", `^a\$|b`, `\Bs`, `a\b`, `^\p{Lu}`, `[\p{L}\d]+$`,
	`^\P{L}+$`, `^(?=[^a-c\n]+$)(?=(.*\w)).+$`, `^\-a`, `[{}[\]]`, "(.+)", "^(.+)$", "^(.*)$", "^a(bc)?$",
	"^(a|b)c|d(e|f)$", "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$",
	`^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$`, "^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])$",
	"^([a-z]+|x)-$", "^a|[0-9]{2}$", "^(a|b)*$", "^(?=a)a|b$", "^/.*", "^a.*", `^a\\.*`,
	`(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\}$)`, "^[0-9]{1,}.[0-9]{1,}$", `^3\.1\.\d+(-.+)?$`,
	"^([A-Za-z_][-A-Za-z0-9_.:]*)$", "^a.c$", "^[a-z].$", "x.y|^z", "^es|ms|x-$", "^(ab){2}$", "^(a|b){2}c$",
	"^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$", `^([a-z][a-z0-9]{0,3})(\.[a-z][a-z0-9]{0,3})*$`,
	`^([a-z_$][a-z0-9_$]{0,3}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,3})$`, "^([a-z]+)(,[a-z]+)+$", "^(a,)*b$", "^(ab,)+a$",
	"^a(,a)*$", "^[a-z]*(-[a-z]*)*$", "^(a-)*a-b$", "^(é,)*a$", `^(?=!+[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$`,
	`^(?=!+[^a]+$)(?=(.*\w)).+$`,
	// Valid only without the u flag (identity escapes).
	`^[\&\@\_]+$`, `a\&.b`, `^[^\%]{1,3}$`, `😀|\&`,
	// Sequences decided by the length of the string.
	"^a[a-z]{2,5}z$", "^.*x$", "^-?[0-9-]{0,3}0$", `^\/[^\*\?]*\/\*$`, "^.{2,}é$", "^[^,]*,[^,]$", "^(a.*|.+b)$",
	`^(.*\.)*[a-z]$`,
	// Lists with an anchor or an assertion inside them, and with an escaped anchor.
	"^x(,a$)*$", "^a(,a$)+$", "^x$(,a)*$", "^(a$)(,a)*$", "^$(,a)*$", "^(a,$)*b$", "^([a-z]+,$)+a$", "^a(^,a)*$",
	"^(^a,)*b$", `^a(,a\b)*$`, `^a(,a\$)*$`, `^a\$(,a)*$`, `^(a\$,)*b$`,
}

// Every matcher agrees with the engine on strings over an alphabet that exercises classes, anchors and non-ASCII.
func TestMatchersAgreeWithTheEngine(t *testing.T) {
	alphabet := []string{
		"a", "b", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "$", "*", "!", "{", "}",
		"|", " ", "\n", " ", "é", "µ", "😀", " ", "x-", "es", "ES", "ms", "txt", "Au", "to", "No", "ne",
		"2015", "Co", "re", "20", "15", "22", "2", ",", "a,", "ab,", "a-", "%", "&", "?",
	}
	seed := xorshift(0x2545f4914f6cdd1d)
	engines := 0
	for _, source := range testPatterns {
		reference, err := ecmaregex.Compile(source)
		if err != nil {
			t.Errorf("%q does not compile: %v", source, err)
			continue
		}
		compiled, ok := compilePattern(source)
		if !ok {
			t.Errorf("%q is not a valid pattern", source)
			continue
		}
		if compiled.kind == matchEngine {
			engines++
		}
		for i := 0; i < 4000; i++ {
			var sb strings.Builder
			for n := seed.next(9); n > 0; n-- {
				sb.WriteString(alphabet[seed.next(len(alphabet))])
			}
			s := []byte(sb.String())
			if got, want := compiled.match(s, isASCII(s)), reference.Match(s); got != want {
				t.Errorf("%q on %q: %v, the engine says %v", source, s, got, want)
				break
			}
		}
	}
	if engines > len(testPatterns)/2 {
		t.Errorf("%d of %d patterns run on the engine", engines, len(testPatterns))
	}
}

func TestSimplePatternsTakeTheFastMatchers(t *testing.T) {
	expect := func(kind matcherKind, name string, patterns ...string) {
		t.Helper()
		for _, source := range patterns {
			p, ok := compilePattern(source)
			if !ok {
				t.Errorf("%q is not a valid pattern", source)
			} else if p.kind != kind {
				t.Errorf("%q takes matcher %d, want %s", source, p.kind, name)
			}
		}
	}
	expect(matchEverything, "everything", "", ".*", "^.*", `^[\s\S]*$`, "^(.*)")
	expect(matchSequence, "a sequence",
		"^[@$_#]", `^[a-zA-Z0-9_\-]*$`, "^#[0-9a-fA-F]{6}$", "^[a-z][a-z0-9_]+$", `^\d{4}-\d{2}-\d{2}$`,
		// Decided by the length of the string.
		"^[a-z]*a$", `^\/[^\*\?]*\/\*$`)
	expect(matchLiteral, "a literal", "^x-", `^\/`, "^abc$", `^\-a`)
	expect(matchHasContent, "has content", ".+", "^.+", "(.+)")
	expect(matchLine, "a line", "^.{1,256}$", "^.+$", "^.*$", "^(.*)$", "^(.+)$")
	expect(matchLiterals, "literals", "^(ab|cd)$", "^(?:es|ES|x-)$")
	expect(matchAlternatives, "alternatives",
		"^ab|cd$", "a|b", "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$",
		`^[Ee][Ss]2015(\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$`, "^[1-5](?:[0-9]{2}|XX)$",
		`(^([0-9]+)\.([0-9]+)$)|(^\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\}$)`,
		"^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m|M])$",
		"^([A-Za-z_][-A-Za-z0-9_.:]*)$", `^\/[^\*\?\&\%]*(\/\*)?$`, `^.*\.(?:txt|trie)(?:\.gz)?$`)
	expect(matchSeparatedList, "a separated list",
		"^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$",
		`^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$`,
		`^([a-z_$][a-z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$`,
		// An escaped "$" or "^" is a literal character of the list.
		`^x(,a\$)*$`, `^x\$(,a)*$`, `^(a\$,)*b$`, `^x(,\^a)*$`, `^(a,)*b\$$`, `^x(,[$^]a)*$`)
	expect(matchExcludedClassWithWord, "an excluded class with a word", `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$`)
	expect(matchEngine, "the engine",
		"(base64key|awskms)://(.*)", `\bfoo`, `^\p{L}+$`, `^((\.(?!\.)\/)?\w+\/?)+$`, `^\1(a)`, `a\&.b`,
		"^[a-z]*a[a-z]*$",
		// A list with an anchor or an assertion inside it.
		"^x(,a$)*$", "^x(?:,a$)+$", "^x$(,a)*$", "^(x$)(,a)*$", "^$(,a)*$", "^(a,$)*b$", "^x(^,a)*$", "^(^a,)*b$",
		"^x(,(a$))*$", `^x(,a\b)*$`, `^x\B(,a)*$`, "^x(,a(?=,|$))*$", "^x(?=,)(,a)*$")
}

func TestInvalidPatternsAreRejected(t *testing.T) {
	for _, source := range []string{"a(", "[a", "a{2,1}", "(?<n>a)(?<n>b)", "*a", `^(?=a`} {
		if _, ok := compilePattern(source); ok {
			t.Errorf("%q compiled", source)
		}
	}
}

// A class with a member outside ASCII, in the shape "^(?=[^SET]+$)(?=(.*\w)).+$". The shape's set holds ASCII
// characters only. Reading such a member as a bit of the set panicked when the schema was compiled (v0.1.0). The
// pattern has no shape and is matched by the engine.
func TestExcludedClassWithAMemberOutsideASCII(t *testing.T) {
	word := func(c rune) bool {
		return c == '_' || c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'
	}
	cases := []struct {
		pattern  string
		excluded func(rune) bool
	}{
		{"^(?=[^é]+$)(?=(.*\\w)).+$", func(c rune) bool { return c == 0xe9 }},
		{"^(?=[^xé]+$)(?=(?:.*\\w)).+$", func(c rune) bool { return c == 'x' || c == 0xe9 }},
		{"^(?=[^中/]+$)(?=.*\\w).+$", func(c rune) bool { return c == 0x4e2d || c == '/' }},
		{"^(?=[^\U0001f600]+$)(?=(.*\\w)).+$", func(c rune) bool { return c == 0x1f600 }},
		// A range that starts in ASCII and ends outside it.
		{"^(?=[^m-é]+$)(?=(.*\\w)).+$", func(c rune) bool { return c >= 'm' && c <= 0xe9 }},
	}
	texts := []string{
		"C1", ")a", "abc", "d-8p", "p.q", "x1", "a/b", "---", "é1", "aé", "中a", "a\U0001f600", "zè",
		"0", "ABC", "a{", "al",
	}
	for _, c := range cases {
		schema, err := json.Marshal(map[string]string{"pattern": c.pattern})
		if err != nil {
			t.Fatal(err)
		}
		v, err := Compile(schema)
		if err != nil {
			t.Fatalf("%s: %v", c.pattern, err)
		}
		for _, text := range texts {
			instance, err := json.Marshal(text)
			if err != nil {
				t.Fatal(err)
			}
			want := strings.IndexFunc(text, word) >= 0 && strings.IndexFunc(text, c.excluded) < 0
			if got := v.IsValidBytes(instance); got != want {
				t.Errorf("%s on %q: got %v, want %v", c.pattern, text, got, want)
			}
		}
	}
}

// anchorCases are patterns with an anchor, a word boundary or a lookaround in a place where a matcher for a shape could
// lose it, and patterns with an escaped "$" or "^", which is a literal character. Each has the texts it matches and the
// texts it does not. The answers are V8's: new RegExp(pattern, "u").test(text).
var anchorCases = []struct {
	pattern string
	matches []string
	fails   []string
}{
	{`^x(,a$)*$`, []string{"x", "x,a"}, []string{"x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(,a$)+$`, []string{"x,a"}, []string{"x", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(?:,a$)*$`, []string{"x", "x,a"}, []string{"x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x$(,a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^(x$)(,a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^(?:x$)(,a)+$`, nil, []string{"x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^$(,a)*$`, []string{""}, []string{",a", ",a,a", "a"}},
	{`^(a,$)*b$`, []string{"b"}, []string{"a,b", "a,a,b", "a,", "", "a,a"}},
	{`^(a,$)+b$`, nil, []string{"b", "a,b", "a,a,b", "a,", "", "a,a"}},
	{`^(?:a,$)*b$`, []string{"b"}, []string{"a,b", "a,a,b", "a,", "", "a,a"}},
	{`^([a-z]+)(\.[a-z]+$)*$`, []string{"a", "a.b"}, []string{"a.b.c", "a.", ""}},
	{`^([a-z]+\.$)*[a-z]+$`, []string{"a"}, []string{"a.b", "a.b.c", ".a", ""}},
	{`^x(,a$$)*$`, []string{"x", "x,a"}, []string{"x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(^,a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(,^a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^^x(,a)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^(^x)(,a)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^(^a,)*b$`, []string{"b", "a,b"}, []string{"a,a,b", "a,", "", "a,a"}},
	{`^(a,)*^b$`, []string{"b"}, []string{"a,b", "a,a,b", "a,", "", "a,a"}},
	{`^x(,(a$))*$`, []string{"x", "x,a"}, []string{"x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(,(?:a$))*$`, []string{"x", "x,a"}, []string{"x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(,a|$)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^x(,(a|$))*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a", "x,"}, []string{"", "x,b"}},
	{`^x((^|,)a)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^(a,)*(b$)$`, []string{"b", "a,b", "a,a,b"}, []string{"a,", "", "a,a"}},
	{`^(a,)*b$$`, []string{"b", "a,b", "a,a,b"}, []string{"a,", "", "a,a"}},
	{`^x(,a\b)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b", "x,ab"}},
	{`^x(\b,a)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^x(,\Ba)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(\B,a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x\b(,a)*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^x\B(,a)*$`, nil, []string{"x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^(a\b,)*b$`, []string{"b", "a,b", "a,a,b"}, []string{"a,", "", "a,a"}},
	{`^(a,\B)*b$`, []string{"b"}, []string{"a,b", "a,a,b", "a,", "", "a,a"}},
	{`^x(,a(?=,|$))*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^x(,(?!a)a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(?=,)(,a)*$`, []string{"x,a", "x,a,a", "x,a,a,a"}, []string{"x", "", "x,", "x,b"}},
	{`^x(,a(?<=a))*$`, []string{"x", "x,a", "x,a,a", "x,a,a,a"}, []string{"", "x,", "x,b"}},
	{`^x(,(?<!,)a)*$`, []string{"x"}, []string{"x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^x(?=,a$)(,a)*$`, []string{"x,a"}, []string{"x", "x,a,a", "x,a,a,a", "", "x,", "x,b"}},
	{`^(a,(?=b))*b$`, []string{"b", "a,b"}, []string{"a,a,b", "a,", "", "a,a"}},
	{`^(?!a,a)(a,)*b$`, []string{"b", "a,b"}, []string{"a,a,b", "a,", "", "a,a"}},
	{`^x(,a\$)*$`, []string{"x", "x,a$", "x,a$,a$"}, []string{"x,a", "x,a,a", "x,a$,a"}},
	{`^x\$(,a)*$`, []string{"x$", "x$,a", "x$,a,a"}, []string{"x", "x,a"}},
	{`^(a\$,)*b$`, []string{"b", "a$,b", "a$,a$,b"}, []string{"a,b", "a$,a,b"}},
	{`^x(,\^a)*$`, []string{"x", "x,^a", "x,^a,^a"}, []string{"x,a", "x,a,a"}},
	{`^(a,)*b\$$`, []string{"b$", "a,b$", "a,a,b$"}, []string{"b", "a,b"}},
	{`^x(,[$^]a)*$`, []string{"x", "x,$a", "x,^a,$a"}, []string{"x,a", "x,a,a"}},
	{`^a$b`, nil, []string{"a", "ab", "a$b", ""}},
	{`^a^b`, nil, []string{"a", "ab", "b"}},
	{`^a$$`, []string{"a"}, []string{"ab", ""}},
	{`^^a`, []string{"a", "ab"}, []string{"ba"}},
	{`^a\$b$`, []string{"a$b"}, []string{"ab", "a"}},
	{`^a\b`, []string{"a", "a b", "a,"}, []string{"ab"}},
	{`^a\Bb`, []string{"ab"}, []string{"a b", "a"}},
	{`^\ba`, []string{"a"}, []string{"ba", ""}},
	{`^a(?=b)`, []string{"ab"}, []string{"a", "ac"}},
	{`^a(?!b)`, []string{"a", "ac"}, []string{"ab"}},
	{`^[a-c]+\b$`, []string{"a", "abc"}, []string{""}},
	{`^[a-c]+$\b`, []string{"a", "abc"}, []string{""}},
	{`^$`, []string{""}, []string{"a"}},
	{`^a$.*`, []string{"a"}, []string{"ab", ""}},
	{`^a\b.*`, []string{"a", "a b"}, []string{"ab"}},
	{`^a^.*`, nil, []string{"a", "ab"}},
	{`^\$.*`, []string{"$", "$a"}, []string{"a"}},
	{`^a$|b`, []string{"a", "ab", "b", "cb"}, []string{"c"}},
	{`a$|^b`, []string{"a", "ba", "bc"}, []string{"ab", "cb"}},
	{`^(a$|b)$`, []string{"a", "b"}, []string{"ab", ""}},
	{`^(a|^b)$`, []string{"a", "b"}, []string{"ab"}},
	{`^(a|b\b)$`, []string{"a", "b"}, []string{"ab"}},
	{`^(?:a\$|b)$`, []string{"a$", "b"}, []string{"a"}},
	{`^(a|$)$`, []string{"a", ""}, []string{"b"}},
	{`^(^|b)$`, []string{"", "b"}, []string{"a"}},
	{`^(a$|b)c`, []string{"bc", "bcd"}, []string{"a", "ac"}},
	{`(^a|b)c$`, []string{"ac", "bc", "xbc"}, []string{"xac", "acx"}},
	{`a(b|^c)`, []string{"ab", "xab"}, []string{"ac", "c"}},
	{`(a$){2}`, nil, []string{"a", "aa", ""}},
	{`(^a){2}`, nil, []string{"a", "aa"}},
	{`x(a$)?b`, []string{"xb"}, []string{"xab", "xa"}},
	{`^x(a$)?`, []string{"x", "xa", "xab", "xb"}, nil},
	{`(a|b$)c`, []string{"ac"}, []string{"bc", "b"}},
	{`a|\bb`, []string{"a", "b", "c b"}, []string{"cb"}},
	{`a\b|c`, []string{"a", "c", "b a"}, []string{"ab"}},
	{`(?=a)a|b`, []string{"a", "b"}, []string{"c"}},
	{`(?<=a)b|c`, []string{"ab", "c"}, []string{"b"}},
	{`a^|b`, []string{"b"}, []string{"a", ""}},
	{`$a|b`, []string{"b"}, []string{"a", ""}},
	{`^|a`, []string{"", "b"}, nil},
	{`a|$`, []string{"", "b"}, nil},
	{`(a|b)$c`, nil, []string{"ac", "a", "bc"}},
	{`^(a|b)^c`, nil, []string{"ac", "c"}},
	{`[a-c]{2}$|^x`, []string{"ab", "x", "yab"}, []string{"abx", "yx"}},
	{`[a-c]\b|x`, []string{"a", "x", "d a"}, []string{"ad"}},
	{`^[a-c]+$|^\d$`, []string{"abc", "1"}, []string{"a1", ""}},
	{`^(a|b){2}$`, []string{"ab"}, []string{"a", "abc"}},
	{`^.{1,3}$$`, []string{"a"}, []string{"abcd", ""}},
	{`^.$^`, nil, []string{"a", ""}},
	{`^(.*)$`, []string{"", "a"}, nil},
	{`^es$|ms`, []string{"es", "xms"}, []string{"esx", "x"}},
	{`^x-$`, []string{"x-"}, []string{"x-a"}},
	{`^x-\b`, []string{"x-a"}, []string{"x-", "x--"}},
}

// A shape that cannot express an anchor or an assertion leaves the pattern to the engine. The separated list dropped a
// "$" at the end of its repeated group and after its first item (v0.1.0 to v0.1.2), so {"pattern": "^x(,a$)*$"} took
// "x,a,a", which has a "$" before its second ",a".
func TestAnAnchorOrAnAssertionInsideAShapeIsNotDropped(t *testing.T) {
	for _, c := range anchorCases {
		schema, err := json.Marshal(map[string]string{"pattern": c.pattern})
		if err != nil {
			t.Fatal(err)
		}
		v, err := Compile(schema)
		if err != nil {
			t.Errorf("%s: %v", c.pattern, err)
			continue
		}
		check := func(texts []string, want bool) {
			for _, text := range texts {
				instance, err := json.Marshal(text)
				if err != nil {
					t.Fatal(err)
				}
				if got := v.IsValidBytes(instance); got != want {
					t.Errorf("%s on %q: got %v, want %v", c.pattern, text, got, want)
				}
			}
		}
		check(c.matches, true)
		check(c.fails, false)
	}
}
