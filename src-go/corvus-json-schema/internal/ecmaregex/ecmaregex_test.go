package ecmaregex

import (
	"strings"
	"sync"
	"testing"
)

// javaHand is the list of hand-written patterns of the Java port's EcmaRegexValidatorTest, each with whether it is a
// valid ECMA-262 pattern with the u flag and whether it is valid with either grammar. The Java test compares its
// validator with its translator. Here the expectations are written out, and TestOracleHandWritten checks the same
// list against V8.
var javaHand = []struct {
	pattern        string
	unicode, valid bool
}{
	{"", true, true}, {"a", true, true}, {"^a$", true, true}, {"a|b", true, true}, {"(a)", true, true},
	{"(?:a)", true, true}, {"(?=a)", true, true}, {"(?!a)", true, true}, {"(?<=a)", true, true},
	{"(?<!a)", true, true}, {`(?<n>a)\k<n>`, true, true}, {`\k<n>`, false, true}, {"(?<n>a)(?<n>b)", false, false},
	{"(?<$x_1>a)", true, true}, {"(?<1a>a)", false, false}, {"a{2}", true, true}, {"a{2,}", true, true},
	{"a{2,3}", true, true}, {"a{3,2}", false, false}, {"a{", false, true}, {"a{,2}", false, true},
	{"{", false, true}, {"}", false, true}, {"]", false, true}, {"[", false, false}, {"[]", true, true},
	{"[^]", true, true}, {"[a-z]", true, true}, {"[z-a]", false, false}, {`[\d-z]`, false, true},
	{`[a-\d]`, false, true}, {`[\b]`, true, true}, {`[\-]`, true, true}, {`\-`, false, true}, {`\a`, false, true},
	{`\c`, false, true}, {`\cA`, true, true}, {`\c1`, false, true}, {`\0`, true, true}, {`\01`, false, true},
	{`\1`, false, true}, {`(a)\1`, true, true}, {`(a)\2`, false, true}, {`\x4`, false, true}, {`\x41`, true, true},
	{`\u004`, false, true}, {`A`, true, true}, {`\u{1F600}`, true, true}, {`\u{110000}`, false, true},
	{`😀`, true, true}, {`[😀-🙏]`, true, true}, {`\p{L}`, true, true},
	{`\p{Letter}`, true, true}, {`\p{digit}`, true, true}, {`\p{Nope}`, false, true}, {`\p{gc=Lu}`, true, true},
	{`\p{Script=Greek}`, true, true}, {`\p{sc=Grek}`, true, true}, {`\p{Script=Nope}`, false, true},
	{`\P{ASCII}`, true, true}, {`\p`, false, true}, {`\p{`, false, true}, {"a**", false, false}, {"a+?", true, true},
	{"*a", false, false}, {"(?=a)*", false, true}, {"^*", false, false}, {"$+", false, false}, {`\b+`, false, false},
	{"a)", false, false}, {"(a", false, false}, {"(?a)", false, false}, {`\/`, true, true}, {`\.`, true, true},
	{`a\`, false, false}, {"[a", false, false}, {"x{1}{2}", false, false}, {`\w+@\w+\.\w+`, true, true},
	{"^[a-z][a-z0-9_]*$", true, true}, {`(?<a>.)\k<a>`, true, true}, {`[\p{L}\d]`, true, true},
	{`\s\S\w\W\d\D`, true, true},
}

func TestJavaHandWrittenPatterns(t *testing.T) {
	for _, c := range javaHand {
		if got := Valid(c.pattern); got != c.unicode {
			t.Errorf("Valid(%q) = %v, want %v", c.pattern, got, c.unicode)
		}
		re, err := Compile(c.pattern)
		if (err == nil) != c.valid {
			t.Errorf("Compile(%q) error %v, want valid %v", c.pattern, err, c.valid)
		}
		if err == nil && re.Unicode() != c.unicode {
			t.Errorf("Compile(%q).Unicode() = %v, want %v", c.pattern, re.Unicode(), c.unicode)
		}
	}
	t.Logf("%d patterns", len(javaHand))
}

// TestSuiteCorpus ports the Java port's test over the suite's patterns. Every pattern, patternProperties name and
// string instance in the suite is read as a pattern. None may make the parser or a compiler misbehave, a pattern
// Valid accepts must compile with the u flag grammar, and a pattern or patternProperties name must compile.
func TestSuiteCorpus(t *testing.T) {
	corpus := suiteCorpus(t)
	valid, compiled := 0, 0
	for _, p := range corpus {
		ok := Valid(p)
		re, err := Compile(p)
		if ok {
			valid++
			if err != nil || !re.Unicode() {
				t.Errorf("Valid(%q) but Compile gives %v", p, err)
			}
		}
		if err != nil {
			continue
		}
		compiled++
		if !ok && re.Unicode() {
			t.Errorf("Compile(%q) used the u flag grammar but Valid rejects it", p)
		}
	}
	t.Logf("%d strings, %d valid with the u flag, %d compiled", len(corpus), valid, compiled)
}

// differentialInputs builds texts over an alphabet that exercises classes, anchors, line terminators, characters
// beyond the Basic Multilingual Plane and malformed UTF-8.
func differentialInputs(n int) []string {
	alphabet := []string{
		"a", "b", "c", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "$", "*", "!", "{",
		"}", "|", " ", "\n", "\r", "\t", " ", "é", "µ", "😀", "🐲", " ", "�", "x-", "es", "ES", "ms",
		"txt", "Au", "to", "No", "ne", "2015", "Co", "re", "20", "15", "22", "2", ",", "a,", "ab,", "a-", "foo", "bar",
		"α", "Ω", "٣", "\\", "&", "(", ")", "[", "]", "^", "+", "?", "<", ">", "=", "%", "\xff", "\xc3", "\xe2\x82",
		"\xf0\x9f", "\xed\xa0\x80",
	}
	seed := xorshift(0x2545f491)
	inputs := append([]string{""}, alphabet...)
	for len(inputs) < n {
		var b strings.Builder
		for k := 1 + seed.next()%8; k > 0; k-- {
			b.WriteString(alphabet[seed.next()%uint32(len(alphabet))])
		}
		inputs = append(inputs, b.String())
	}
	return inputs
}

// TestDifferential proves the translation to the regexp package exact on the patterns at hand. Every pattern that
// takes that back end is also run on the backtracking matcher, and the two must agree on every text.
func TestDifferential(t *testing.T) {
	patterns := map[string]bool{}
	o := loadOracle(t)
	for _, h := range o.Hand {
		patterns[h.P] = true
	}
	for _, f := range o.Flagged {
		patterns["(?"+f.F+":"+f.P+")"] = true
	}
	for _, p := range []string{
		`�`, `^[^�]+$`, `^�+$`, `^.$`, `^..$`, `^[^a]$`, `\P{Any}`, `^\p{Any}*$`, `^[\s\S]{2,4}$`,
		`^(?:\p{L}|\p{N})+$`, `\b\w+\b`, `\B`, `^$`, `$`, `^`, `(?:)`, `^(?:a{2}){2,3}$`, `^(?:a|b|ab){1,4}?c`,
		`^.{0,300}$`, `^(?:[a-z]{1,3}\d?){2,5}$`, `^[\u0000-￿]+$`, `^[\u{10000}-\u{10ffff}]+$`,
		`(?:a{50}){30}`, `^a{1001}$`, `^(?:a?){1001}$`, `\x{41}`, `^\u{2}$`,
	} {
		patterns[p] = true
	}
	if corpus := optionalSuiteCorpus(t); corpus != nil {
		for _, p := range corpus {
			patterns[p] = true
		}
	}
	inputs := differentialInputs(1500)
	var engines [2]int
	answers := 0
	for p := range patterns {
		re, err := Compile(p)
		if err != nil {
			continue
		}
		engines[re.Engine()]++
		if re.Engine() != EngineRE2 {
			continue
		}
		backtrack, err := compile(p, true)
		if err != nil {
			t.Fatalf("compile(%q) for the backtracking matcher: %v", p, err)
		}
		for _, s := range inputs {
			answers++
			if a, b := re.MatchString(s), backtrack.MatchString(s); a != b {
				t.Errorf("%q on %q: %v says %v, %v says %v", p, s, EngineRE2, a, EngineBacktrack, b)
			}
		}
	}
	t.Logf("%d patterns on %v compared on %d texts each (%d answers), %d patterns on %v only",
		engines[EngineRE2], EngineRE2, len(inputs), answers, engines[EngineBacktrack], EngineBacktrack)
}

// TestEngineChoice checks which back end a pattern takes.
func TestEngineChoice(t *testing.T) {
	for p, want := range map[string]Engine{
		`^[a-z][a-z0-9_]*$`: EngineRE2, `\bfoo`: EngineRE2, `^\p{L}+$`: EngineRE2, `(?<name>ab)+`: EngineRE2,
		`^\/[^\*\?\&\%]*(\/\*)?$`: EngineRE2, `^.{1,256}$`: EngineRE2, `^a*?b`: EngineRE2,
		`(a)\1`: EngineBacktrack, `^(?!foo)`: EngineBacktrack, `(?=a)`: EngineBacktrack, `(?<=a)b`: EngineBacktrack,
		`(?<!a)b`: EngineBacktrack, `(?<n>a)\k<n>`: EngineBacktrack,
		// The regexp package refuses a repeat count above 1000, so the backtracking matcher takes these.
		`^a{1001}$`: EngineBacktrack, `(?:a{50}){30}`: EngineBacktrack,
	} {
		re, err := Compile(p)
		if err != nil {
			t.Errorf("Compile(%q): %v", p, err)
		} else if re.Engine() != want {
			t.Errorf("Compile(%q) is on %v, want %v", p, re.Engine(), want)
		}
	}
}

// TestBacktrackingSemantics pins the ECMA-262 behaviours only the backtracking matcher has. The oracle checks most
// of them against V8 too. These are the ones worth reading.
func TestBacktrackingSemantics(t *testing.T) {
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		// A backreference to a group that has not taken part matches the empty string.
		{`^\1(a)$`, "a", true}, {`^(a)?\1b$`, "b", true}, {`^(?:(a)|b)\1$`, "b", true},
		// A group is reset at the start of each iteration of the loop around it.
		{`^(?:(a)|b)*\1$`, "ab", true}, {`^(?:(a)|b)*\1$`, "aba", false}, {`^(?:(a)|b)*\1$`, "baa", true},
		// An iteration that consumes nothing ends the loop once the minimum is met.
		{`^(?:a*)*$(?<=a)`, "aaa", true}, {`^(?:a*)*b(?<=b)`, "aaa", false}, {`^(?:(?=(a)))?\1b`, "b", true},
		{`^(?:(?=(a)))?\1a`, "a", true}, {`^(?:a?){3,5}b(?<=b)`, "aab", true}, {`^(?:|a)+b(?<=b)`, "aab", true},
		// A lookaround is atomic and a negative one leaves no group set.
		{`^(?=(a+))a*b\1$`, "aaaba", false}, {`^(?=(a+))a*b\1$`, "aaabaaa", true}, {`(?!(a))\1b`, "b", true},
		// A lookbehind matches right to left, so the rightmost group takes the most.
		{`(?<=(\d+)(\d+))$`, "1053", true}, {`(?<=\1(a))b`, "aab", true}, {`(?<=\1(a))b`, "ab", false},
		{`(?<=^a*)b`, "aab", true}, {`(?<!^a*)b`, "aab", false}, {`(?<=a|bc)x`, "bcx", true},
		// Matching is by code point.
		{`^(?<n>.)\k<n>$`, "🐲🐲", true}, {`^(?<n>.)\k<n>$`, "🐲🐉", false}, {`(?<=^.)$`, "🐲", true},
		{`(?<=[😀-😎])x`, "😃x", true}, {`(?<!\u{1F600})x`, "😀x", false},
		// Lazy and counted loops.
		{`^(?:a|ab)+?b$(?<=b)`, "aabb", true}, {`^(?:(a)|b){2,}?\1$`, "abbaa", true},
		{`^(?:(a)|b){2,}?\1$`, "abba", false}, {`^(.+?)\1+$`, "abcabcabc", true},
		{`^(.+?)\1+$`, "abcabcab", false}, {`^(?:(\w)(?!\1))+$`, "abab", true}, {`^(?:(\w)(?!\1))+$`, "abba", false},
		{`^(?!.*(.).*\1)[a-c]+$`, "abc", true}, {`^(?!.*(.).*\1)[a-c]+$`, "abca", false},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Errorf("Compile(%q): %v", c.pattern, err)
			continue
		}
		if re.Engine() != EngineBacktrack {
			t.Errorf("Compile(%q) is on %v", c.pattern, re.Engine())
		}
		if got := re.MatchString(c.text); got != c.want {
			t.Errorf("%q on %q: got %v, want %v", c.pattern, c.text, got, c.want)
		}
	}
}

// TestLongInput runs the backtracking matcher on texts long enough that a matcher recursing once per character or
// per choice would exhaust its stack.
func TestLongInput(t *testing.T) {
	long := strings.Repeat("ab", 1<<20)
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		{`^(?:a|b)*$(?<=b)`, long, true}, {`^(?:a|b)*c(?<=c)`, long, false}, {`^(?:ab)+(?!.)`, long, true},
		{`^(?=a)(?:\w\w)*?$`, long, true}, {`^(a|b)*\1$`, long + "b", true}, {`(?<=^(?:ab)*)c`, long + "c", true},
		{`^(?!b).*b$`, long, true}, {`^[ab]{2097152}(?<=b)$`, long, true}, {`^(?:[ab]c?){2097152}(?<=b)$`, long, true},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatalf("Compile(%q): %v", c.pattern, err)
		}
		if re.Engine() != EngineBacktrack {
			t.Errorf("Compile(%q) is on %v", c.pattern, re.Engine())
		}
		if got := re.MatchString(c.text); got != c.want {
			t.Errorf("%q on %d bytes: got %v, want %v", c.pattern, len(c.text), got, c.want)
		}
	}
}

// TestAllocations checks that neither back end allocates in the steady state.
func TestAllocations(t *testing.T) {
	texts := [][]byte{
		[]byte("the quick brown fox jumps over the lazy dog 12345"), []byte("abcabcabc"), []byte("a-b_c.d@example.com"),
		[]byte("🐲 naïve café 🐲"), []byte(""),
	}
	for p, want := range map[string]Engine{
		`^[a-z][a-z0-9_]*$`: EngineRE2, `\b\d{5}\b`: EngineRE2, `^\p{L}+$`: EngineRE2,
		`(?:fox|dog) \w+`: EngineRE2, `^(.+?)\1+$`: EngineBacktrack, `^(?!.*\d{6})(?=.*\bfox\b).+$`: EngineBacktrack,
		`(?<=@)\w+(?:\.\w+)+$`: EngineBacktrack, `^(?:(\w)(?!\1)|\W){3,}?$`: EngineBacktrack,
		`(?<n>[a-z])\k<n>`: EngineBacktrack, `café(?= )`: EngineBacktrack, `(?i:QUICK|ÉCOLE)`: EngineRE2,
		`(\w)(?i:\1)`: EngineBacktrack, `(?m:^)\w+(?m:$)`: EngineBacktrack, `(?i:\bCAF\b)`: EngineBacktrack,
	} {
		re, err := Compile(p)
		if err != nil {
			t.Fatalf("Compile(%q): %v", p, err)
		}
		if re.Engine() != want {
			t.Fatalf("Compile(%q) is on %v, want %v", p, re.Engine(), want)
		}
		// The first matches grow the pooled state to the size the texts need.
		for _, text := range texts {
			re.Match(text)
		}
		allocs := testing.AllocsPerRun(200, func() {
			for _, text := range texts {
				re.Match(text)
			}
		})
		if allocs != 0 {
			t.Errorf("%q on %v: %v allocations per run", p, re.Engine(), allocs)
		}
		text := "the quick brown fox 12345"
		if allocs := testing.AllocsPerRun(200, func() { re.MatchString(text) }); allocs != 0 {
			t.Errorf("%q on %v: MatchString makes %v allocations per run", p, re.Engine(), allocs)
		}
	}
}

// TestConcurrentUse matches one pattern of each back end from many goroutines at once.
func TestConcurrentUse(t *testing.T) {
	for _, p := range []string{`^(?:[a-z]+\d)+$`, `^(?:([a-z])\1?\d)+(?<=\d)$`} {
		re, err := Compile(p)
		if err != nil {
			t.Fatal(err)
		}
		var wg sync.WaitGroup
		for g := 0; g < 16; g++ {
			wg.Add(1)
			go func(g int) {
				defer wg.Done()
				yes := []byte(strings.Repeat("aa1b2", 50+g))
				no := append(append([]byte{}, yes...), '!')
				for i := 0; i < 500; i++ {
					if !re.Match(yes) || re.Match(no) {
						t.Errorf("%q gave a wrong answer under concurrent use", p)
						return
					}
				}
			}(g)
		}
		wg.Wait()
	}
}

func BenchmarkMatch(b *testing.B) {
	text := []byte("the quick brown fox jumps over the lazy dog 12345")
	for _, p := range []string{
		`^[a-z][a-z0-9_ ]*$`, `\b\d{5}\b`, `^(?!.*\d{6})(?=.*\bfox\b).+$`, `(?<=dog )\d+$`, `(\w)\1`,
	} {
		re, err := Compile(p)
		if err != nil {
			b.Fatal(err)
		}
		b.Run(re.Engine().String()+"/"+p, func(b *testing.B) {
			b.ReportAllocs()
			for i := 0; i < b.N; i++ {
				re.Match(text)
			}
		})
	}
}

// TestFoldTables checks the tables behind case-insensitive matching. The table for the u flag grammar must hold every
// character that has a simple case folding, and each table must agree with canonicalize.
func TestFoldTables(t *testing.T) {
	for _, unicodeMode := range []bool{true, false} {
		table := foldClasses(unicodeMode)
		class := map[rune][]rune{}
		for i, point := range table.points {
			class[point] = table.classes[i]
			for _, member := range table.classes[i] {
				if canonicalize(member, unicodeMode) != canonicalize(point, unicodeMode) {
					t.Errorf("u flag %v: U+%04X and U+%04X share a class but not a canonical form", unicodeMode, point, member)
				}
			}
		}
		seen := map[rune]rune{}
		for r := rune(0); r <= maxRune; r++ {
			key := canonicalize(r, unicodeMode)
			if other, ok := seen[key]; ok || key != r {
				if !ok {
					other = key
				}
				if len(class[r]) == 0 || len(class[other]) == 0 {
					t.Errorf("u flag %v: U+%04X and U+%04X are equivalent but not both in the table", unicodeMode, r, other)
				}
			}
			seen[key] = r
		}
		t.Logf("u flag %v: %d characters in classes", unicodeMode, len(table.points))
	}
}

// TestModifiers checks the modifier groups of ECMAScript 2025 on cases worth reading. TestOracleFlags checks them
// against V8 at large.
func TestModifiers(t *testing.T) {
	for _, c := range []struct {
		pattern, text string
		want          bool
		engine        Engine
	}{
		{`^(?i:abc)$`, "aBc", true, EngineRE2}, {`^a(?i:b)c$`, "aBc", true, EngineRE2}, {`^a(?i:b)c$`, "ABc", false, EngineRE2},
		{`^(?i:a(?-i:b)c)$`, "AbC", true, EngineRE2}, {`^(?i:a(?-i:b)c)$`, "ABC", false, EngineRE2},
		// With the u flag grammar the Kelvin sign and the long s fold to k and s. With the Annex B grammar they do
		// not, and the trailing \& is what makes the second pattern of each pair an Annex B one.
		{`^(?i:k)$`, "\u212a", true, EngineRE2}, {`^(?i:k)\&?$`, "\u212a", false, EngineRE2},
		{`^(?i:s)$`, "\u017f", true, EngineRE2}, {`^(?i:s)\&?$`, "\u017f", false, EngineRE2},
		{`^(?i:\w)$`, "\u017f", true, EngineRE2}, {`^(?i:\W)$`, "\u017f", false, EngineRE2},
		{`(?i:\b)\u212a`, "\u212a", true, EngineBacktrack}, {`\b\u212a`, "\u212a", false, EngineRE2},
		{`^(?i:[^a-z])$`, "K", false, EngineRE2}, {`^(?i:[^a-z])$`, "1", true, EngineRE2},
		{`^(?i:ß)$`, "\u1e9e", true, EngineRE2}, {`^(?i:ß)$`, "SS", false, EngineRE2},
		{`^(.)(?i:\1)$`, "aA", true, EngineBacktrack}, {`^(.)\1$`, "aA", false, EngineBacktrack},
		{`^(.)(?i:\1)$`, "k\u212a", true, EngineBacktrack}, {`(?<=(?i:\1)(.))x`, "Aax", true, EngineBacktrack},
		{`^(?s:.)$`, "\n", true, EngineRE2}, {`^.$`, "\n", false, EngineRE2}, {`^(?s:a.)b.$`, "a\nb\n", false, EngineRE2},
		{`(?m:^)b`, "a\nb", true, EngineBacktrack}, {`^b`, "a\nb", false, EngineRE2},
		{`a(?m:$)`, "a\u2028b", true, EngineBacktrack}, {`a(?m:$)`, "ab", false, EngineBacktrack},
		{`(?m:^)b`, "a\rb", true, EngineBacktrack}, {`(?m:^)b`, "a\u2029b", true, EngineBacktrack},
		// A name may be shared by groups in separate alternatives, and a reference means the one that took part.
		{`^(?:(?<a>x)|(?<a>y))\k<a>$`, "yy", true, EngineBacktrack}, {`^(?:(?<a>x)|(?<a>y))\k<a>$`, "xy", false, EngineBacktrack},
		{`^(?:(?<a>x)|(?<a>y))+\k<a>$`, "xyy", true, EngineBacktrack}, {`^(?:(?<a>x)|(?<a>y))+\k<a>$`, "xyx", false, EngineBacktrack},
		{`^(?<\u0061b>.)\k<ab>$`, "zz", true, EngineBacktrack},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Errorf("Compile(%q): %v", c.pattern, err)
			continue
		}
		if re.Engine() != c.engine {
			t.Errorf("Compile(%q) is on %v, want %v", c.pattern, re.Engine(), c.engine)
		}
		if got := re.MatchString(c.text); got != c.want {
			t.Errorf("%q on %q: got %v, want %v", c.pattern, c.text, got, c.want)
		}
	}
	for _, p := range []string{
		`(?i)a`, `(?-:a)`, `(?ii:a)`, `(?i-i:a)`, `(?x:a)`, `(?i-m-s:a)`, `(?<a>x)(?<a>y)`, `(?<a>x|(?<a>y))`,
		`(?:(?<a>x)|b)(?:(?<a>y)|c)`, `(?<a\u0020>x)`, `(?<\u0030>x)`,
	} {
		if Valid(p) {
			t.Errorf("Valid(%q) = true", p)
		}
		if _, err := Compile(p); err == nil {
			t.Errorf("Compile(%q) gives no error", p)
		}
	}
}

// TestProperties checks that every property name, value and alias resolves, and a few memberships. The whole of each
// set was compared with V8 once, code point by code point, when the tables were generated.
func TestProperties(t *testing.T) {
	expressions := 0
	check := func(expr string) {
		expressions++
		if _, ok := propertySet(expr); !ok {
			t.Errorf("\\p{%s} does not resolve", expr)
		}
	}
	for name := range binaryNames {
		check(name)
	}
	for name := range categoryNames {
		check(name)
		check("gc=" + name)
		check("General_Category=" + name)
	}
	for alias, name := range scriptAliases {
		for _, value := range []string{alias, name} {
			check("sc=" + value)
			check("Script=" + value)
			check("scx=" + value)
			check("Script_Extensions=" + value)
		}
	}
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		{`^\p{L}$`, "é", true}, {`^\p{L}$`, "1", false}, {`^\p{Lu}$`, "É", true}, {`^\P{Lu}$`, "É", false},
		{`^\p{Nd}$`, "৪", true}, {`^\p{digit}$`, "৪", true}, {`^\p{sc=Grek}$`, "α", true}, {`^\p{Script=Greek}$`, "a", false},
		// U+0342 is Inherited by Script and Greek by Script_Extensions.
		{`^\p{sc=Grek}$`, "\u0342", false}, {`^\p{scx=Grek}$`, "\u0342", true}, {`^\p{sc=Zinh}$`, "\u0342", true},
		{`^\p{Emoji}$`, "🐲", true}, {`^\p{Emoji_Presentation}$`, "#", false}, {`^\p{Emoji}$`, "#", true},
		{`^\p{Alphabetic}$`, "ⅷ", true}, {`^\p{Uppercase}$`, "Ⓐ", true}, {`^\p{ID_Start}$`, "_", false},
		{`^\p{ID_Continue}$`, "_", true}, {`^\p{White_Space}$`, "\u0085", true}, {`^\s$`, "\u0085", false},
		{`^\p{White_Space}$`, "\ufeff", false}, {`^\s$`, "\ufeff", true}, {`^\p{Any}$`, "\U0010ffff", true},
		{`^\p{Assigned}$`, "\U0010ffff", false}, {`^\p{Script=Unknown}$`, "\U000e0080", true}, {`^\p{ASCII}$`, "é", false},
		{`^[\p{Lu}\p{Nd}]+$`, "A1É৪", true}, {`^[^\p{L}]+$`, "1 -", true}, {`^[^\p{L}]+$`, "1a", false},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Errorf("Compile(%q): %v", c.pattern, err)
			continue
		}
		if got := re.MatchString(c.text); got != c.want {
			t.Errorf("%q on %q: got %v, want %v", c.pattern, c.text, got, c.want)
		}
	}
	for _, p := range []string{`\p{Nope}`, `\p{L=x}`, `\p{gc=Greek}`, `\p{sc=L}`, `\p{}`, `\p{Lu`, `\pL`, `\p{sc=}`, `\p{ascii}`} {
		if Valid(p) {
			t.Errorf("Valid(%q) = true", p)
		}
	}
	t.Logf("%d property expressions resolve", expressions)
}
