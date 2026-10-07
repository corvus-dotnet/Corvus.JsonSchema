package jsonschema

import (
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
		`^([a-z_$][a-z0-9_$]{0,39}\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$`)
	expect(matchExcludedClassWithWord, "an excluded class with a word", `^(?=[^!*,;{}[\]~\n]+$)(?=(.*\w)).+$`)
	expect(matchEngine, "the engine",
		"(base64key|awskms)://(.*)", `\bfoo`, `^\p{L}+$`, `^((\.(?!\.)\/)?\w+\/?)+$`, `^\1(a)`, `a\&.b`,
		"^[a-z]*a[a-z]*$")
}

func TestInvalidPatternsAreRejected(t *testing.T) {
	for _, source := range []string{"a(", "[a", "a{2,1}", "(?<n>a)(?<n>b)", "*a", `^(?=a`} {
		if _, ok := compilePattern(source); ok {
			t.Errorf("%q compiled", source)
		}
	}
}
