package ecmaregex

import (
	"encoding/json"
	"os"
	"slices"
	"strings"
	"testing"
	"time"
)

// A v8Case is one pattern of testdata/v8_cases.json with what V8 says of it: whether it is valid with the u flag and
// with no flag, and whether it matches each text. testdata/gen_cases.js writes the file. Where V8 departs from
// ECMA-262 the answers are ECMA-262's, and Ecma262 names the departure.
type v8Case struct {
	P       string `json:"p"`
	U       bool   `json:"u"`
	L       bool   `json:"l"`
	Ecma262 string `json:"ecma262"`
	Tests   []struct {
		T    string `json:"t"`
		Want bool   `json:"want"`
	} `json:"tests"`
}

type v8Cases struct {
	Node  string   `json:"node"`
	Units []v8Case `json:"units"`
	Loops []v8Case `json:"loops"`
	Fast  []v8Case `json:"fast"`
	Needs []v8Case `json:"needs"`
	Quick []v8Case `json:"quick"`
	Memo  []v8Case `json:"memo"`
	// The four sets of what is a pattern.
	ClassK     []v8Case `json:"classK"`
	Counts     []v8Case `json:"counts"`
	Properties []v8Case `json:"properties"`
	Names      []v8Case `json:"names"`
}

func loadCases(t *testing.T) *v8Cases {
	t.Helper()
	data, err := os.ReadFile("testdata/v8_cases.json")
	if err != nil {
		t.Fatal(err)
	}
	var c v8Cases
	if err := json.Unmarshal(data, &c); err != nil {
		t.Fatal(err)
	}
	return &c
}

// checkCases compares the validity of each pattern and the answers of both back ends with V8's. A pattern has
// limit to answer all its texts, and ten times that under the race detector, which slows the matcher about eight
// times: the slowest pattern of the sets takes over a second for its texts on both back ends, which V8 takes a
// tenth of a second for, and a pattern that the limit is there to catch does not finish at all.
func checkCases(t *testing.T, cases []v8Case, limit time.Duration) (answers, matched, legacy, invalid int) {
	t.Helper()
	if raceEnabled {
		limit *= 10
	}
	for _, c := range cases {
		re, backtrack := checkVerdict(t, c.P, c.U, c.L)
		if re == nil {
			invalid++
			continue
		}
		if !c.U {
			legacy++
		}
		done := make(chan struct{})
		go func() {
			defer close(done)
			for _, test := range c.Tests {
				if got := re.MatchString(test.T); got != test.Want {
					t.Errorf("%q on %q (%v): got %v, V8 says %v", c.P, test.T, re.Engine(), got, test.Want)
				}
				if got := backtrack.Match([]byte(test.T)); got != test.Want {
					t.Errorf("%q on %q (forced backtrack): got %v, V8 says %v", c.P, test.T, got, test.Want)
				}
				if got, err := re.MatchLimit([]byte(test.T), time.Nanosecond); err == nil && got != test.Want {
					t.Errorf("%q on %q (%v, with a limit): got %v, V8 says %v", c.P, test.T, re.Engine(), got, test.Want)
				}
			}
		}()
		select {
		case <-done:
		case <-time.After(limit):
			t.Fatalf("%q did not answer its %d texts in %v", c.P, len(c.Tests), limit)
		}
		for _, test := range c.Tests {
			answers++
			if test.Want {
				matched++
			}
		}
	}
	return
}

// TestCodeUnits checks matching by UTF-16 code unit against V8. A pattern that is valid only with no flag is matched
// a code unit at a time. A character beyond the Basic Multilingual Plane is two units to the dot, to a class, to a
// quantifier, to a group and to a backreference, a match can start and end between them, and such a character in
// the pattern is two atoms. A class range between two of them is an error in both grammars.
func TestCodeUnits(t *testing.T) {
	c := loadCases(t)
	answers, matched, legacy, invalid := checkCases(t, c.Units, time.Minute)
	t.Logf("%d patterns (%d valid only with no flag, %d not valid), %d answers per back end (%d of them a match), from Node %s",
		len(c.Units), legacy, invalid, answers, matched, c.Node)
	if legacy < 90 || invalid < 3 || matched < 500 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// checkSyntax checks one of the sets of what is a pattern: which patterns are valid, in each grammar, and what the
// valid ones match. The expectations are V8's, and ECMA-262's where V8 13.6 departs from it, which gen_cases.js
// works out and marks. It returns the number of patterns that are not valid, and the number marked for each
// departure.
func checkSyntax(t *testing.T, cases []v8Case) (invalid int, departures map[string]int) {
	t.Helper()
	answers, matched, legacy, invalid := checkCases(t, cases, 10*time.Second)
	departures = map[string]int{}
	for _, c := range cases {
		if c.Ecma262 != "" {
			departures[c.Ecma262]++
		}
	}
	t.Logf("%d patterns (%d valid only with no flag, %d not valid), %d answers per back end (%d of them a match), %v where V8 departs from ECMA-262",
		len(cases), legacy, invalid, answers, matched, departures)
	return invalid, departures
}

// TestEscapedKInAClass checks \k in a class. In a pattern with no named group and no u flag it is the letter k. In a
// pattern with a named group it is an error, in a class as outside one: the identity escapes of Annex B leave k out
// when the pattern has a group name, and \k is a reference only outside a class.
func TestEscapedKInAClass(t *testing.T) {
	c := loadCases(t)
	if invalid, _ := checkSyntax(t, c.ClassK); len(c.ClassK) < 30 || invalid < 9 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestCountsAreComparedAsWritten checks the two counts of a quantifier. A first count greater than the second is an
// error whatever the size of the numbers. V8 13.6 keeps a count as a number that stops at 2^31 - 1 and so takes
// a{2147483648,2147483647}. Those cases have ECMA-262's answer.
func TestCountsAreComparedAsWritten(t *testing.T) {
	c := loadCases(t)
	if invalid, departures := checkSyntax(t, c.Counts); invalid < 20 || departures["count-order"] < 10 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestPropertyNamesAndValues checks the names and values of \p{...} where V8 13.6 departs from ECMA-262. WSpace is
// not a name ECMA-262 lists, so \p{WSpace} is an error with the u flag and is read with no flag, as the characters
// p{WSpace}. Katakana_Or_Hiragana (Hrkt) is a Script value, which no character has, so \p{sc=Hrkt} matches nothing
// and \P{sc=Hrkt} matches any character.
func TestPropertyNamesAndValues(t *testing.T) {
	c := loadCases(t)
	if _, departures := checkSyntax(t, c.Properties); departures["property-name"] < 3 || departures["script-value"] < 20 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestAGroupNameUsedTwice checks a name on two groups. It is valid only when the groups are in separate alternatives
// of one disjunction, so that no match goes through both. V8 13.6 takes some patterns where both can take part,
// such as (?<m>a)(?:x|(?<m>b)). Those cases have ECMA-262's answer.
func TestAGroupNameUsedTwice(t *testing.T) {
	c := loadCases(t)
	if invalid, departures := checkSyntax(t, c.Names); invalid < 15 || departures["duplicate-name"] < 4 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestRepetitionsThatCannotConsume checks, against V8, repetitions whose body can match only the empty string and
// backreferences inside the group they name. The matcher runs such a body once, or not at all when the minimum is
// zero, and reads such a backreference as the empty string (see simplify). Run as written, the first patterns of the
// set take time exponential in their counts, so each pattern has ten seconds for all its texts. The others pin
// what the groups of such a body hold afterwards.
func TestRepetitionsThatCannotConsume(t *testing.T) {
	c := loadCases(t)
	start := time.Now()
	answers, matched, _, _ := checkCases(t, c.Loops, 10*time.Second)
	t.Logf("%d patterns, %d answers per back end (%d of them a match) in %v", len(c.Loops), answers, matched,
		time.Since(start).Round(time.Millisecond))
}

// TestSimplify checks the two rewrites of simplify on the syntax tree, and that it leaves the tree it was given
// alone.
func TestSimplify(t *testing.T) {
	for _, c := range []struct {
		pattern string
		// loops is the number of counted loops of the program, and backrefs the number of its backreferences.
		loops, backrefs int
	}{
		{`(?:\b|(?=a)|){4,5}`, 0, 0}, {`(?:(?=(a))|){2}\1`, 0, 1}, {`(?:(?=a))*b`, 0, 0}, {`(?:a|(?=b)){2,3}`, 1, 0},
		{`(a\1)+`, 0, 0}, {`(?:(a)|b\1){2}`, 1, 1}, {`((\1|\2|)){3}`, 0, 0}, {`(?<n>a\k<n>)`, 0, 0},
		{`(?:(?<n>a)|(?<n>b\k<n>)){2}`, 1, 2}, {`(?:(\1){3}|(?!a)){2,}(?<=b)`, 0, 0}, {`(?:\1(a)){2}`, 1, 1},
	} {
		root, _, err := parse(c.pattern, true)
		if err != nil {
			t.Fatalf("parse(%q): %v", c.pattern, err)
		}
		before := dump(root)
		p := compileProgram(root, true)
		if after := dump(root); after != before {
			t.Errorf("%q: compiling changed the tree from %s to %s", c.pattern, before, after)
		}
		backrefs := 0
		for _, i := range p.insts {
			if i.op == opBackref {
				backrefs++
			}
		}
		if p.loops != c.loops || backrefs != c.backrefs {
			t.Errorf("%q: %d counted loops and %d backreferences, want %d and %d", c.pattern, p.loops, backrefs, c.loops,
				c.backrefs)
		}
	}
}

// dump writes the shape of a tree, for comparing two trees.
func dump(n *node) string {
	s := string(rune('A'+int(n.kind))) + "("
	if n.kind == nRepeat {
		s += string(rune('0'+n.min%10)) + string(rune('0'+(n.max+1)%10))
	}
	for _, sub := range n.subs {
		s += dump(sub)
	}
	return s + ")"
}

// TestLoopsGiveUpWhenTheRestCannotFit checks, against V8, counted loops whose iterations can be empty, before a rest
// of the pattern that needs more text than is left. The matcher gives up at such a loop (see setNeeds). Without
// that, the first three patterns of the set do not finish: at the end of the text their loops try every combination
// of the ways through every iteration. Each pattern has ten seconds for all its texts. The other patterns have such
// loops inside and around lookaheads and lookbehinds, where a wrong count of what is left would give a wrong answer.
func TestLoopsGiveUpWhenTheRestCannotFit(t *testing.T) {
	c := loadCases(t)
	start := time.Now()
	answers, matched, _, _ := checkCases(t, c.Needs, 10*time.Second)
	t.Logf("%d patterns, %d answers per back end (%d of them a match) in %v", len(c.Needs), answers, matched,
		time.Since(start).Round(time.Millisecond))
	if len(c.Needs) < 20 || matched < 100 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestNeeds checks the least number of bytes a match consumes, which setNeeds works out for a program and for each
// of its counted loops, and the least one iteration of each loop consumes. The figures are lower bounds. The need of
// a loop counts what follows it and none of its iterations, which the matcher adds from the count it has reached.
// The need of the program counts the least number of iterations of every loop it must go through.
func TestNeeds(t *testing.T) {
	for _, c := range []struct {
		pattern string
		// need is the need of the program, loops the needs of its counted loops in order, and iters the least bytes
		// of one iteration of each.
		need         int
		loops, iters []int32
	}{
		{`abc`, 3, nil, nil}, {`a+b{2,}c?`, 3, nil, nil}, {`(?:a|bc)d`, 2, nil, nil}, {`(?=abc)a`, 1, nil, nil},
		{`(?<=abc)d`, 1, nil, nil}, {`\u00e9{2}\u{10400}`, 3, nil, nil},
		{`(?:a|){3}xy$`, 2, []int32{2}, []int32{0}}, {`(?:a|){3}`, 0, []int32{0}, []int32{0}},
		{`(?:(?:a|){2}b){2}c`, 3, []int32{1, 2}, []int32{1, 0}},
		{`(?=(?:a|){2}xyz)(?:b|){2}c`, 1, []int32{3, 1}, []int32{0, 0}},
		{`(?<=(?:ab|){2}c)(?:d|){2}ef`, 2, []int32{0, 2}, []int32{0, 0}},
		{`(?!(?:a|){2}xx)(?:(x)|){2}\1y`, 1, []int32{2, 1}, []int32{0, 0}},
		{`(?:a|){2}[]`, noMatch, []int32{noMatch}, []int32{0}}, {`(?:(?:a|){2}|bcd)e`, 1, []int32{1}, []int32{0}},
		// The least count of a loop times the least of its body.
		{`(?:a|b){5}c`, 6, []int32{1}, []int32{1}}, {`(?:ab|cde){3,}?x`, 7, []int32{1}, []int32{2}},
		{`(?:a{1000}){1000}`, 1000000, []int32{0}, []int32{1000}}, {`(?:\u00e9|xyz){2}`, 4, []int32{0}, []int32{2}},
		{`(?:a|b){2147483647}c`, noMatch, []int32{1}, []int32{1}}, {`(?:(?:a|bc){2}d){3}`, 9, []int32{0, 1}, []int32{3, 1}},
		// The body of a lookbehind is matched backwards and counts nothing.
		{`(?<=(?:ab|c){2}d)e`, 1, []int32{0}, []int32{0}},
		{`(?:a|(?=(?:b|c){3}))x{2}`, 2, []int32{0}, []int32{1}},
	} {
		root, _, err := parse(c.pattern, true)
		if err != nil {
			t.Fatalf("parse(%q): %v", c.pattern, err)
		}
		p := compileProgram(root, true)
		var loops, iters []int32
		for _, i := range p.insts {
			if i.op == opLoop {
				loops, iters = append(loops, i.need), append(iters, i.iter)
			}
		}
		if p.need != c.need || !slices.Equal(loops, c.loops) || !slices.Equal(iters, c.iters) {
			t.Errorf("%q: need %d, loops %v, iterations %v, want %d, %v and %v", c.pattern, p.need, loops, iters, c.need, c.loops, c.iters)
		}
	}
}

// TestALoopGivesUpWhenItsIterationsCannotFit checks the matcher uses the least count of a counted loop: a pattern
// that needs a million characters, or two thousand million, answers a text of a hundred thousand at once. Without
// the count in the need, the matcher runs each loop as far as the text goes from every position of the text, which
// took 15 seconds for the first of these and no time that was waited for the second. The limit of the match here
// is a small part of that, so a match that does the work is abandoned and the test fails on its error.
func TestALoopGivesUpWhenItsIterationsCannotFit(t *testing.T) {
	text := []byte(strings.Repeat("a", 100000))
	for _, pattern := range []string{`(?:a{1000}){1000}`, `(?:a|b){2147483647}c`, `(?=.)(?:(?:ab|a){500}){500}`, `x?(?:[ab]c?){100001}$`} {
		re, err := compile(pattern, true)
		if err != nil {
			t.Fatalf("compile(%q): %v", pattern, err)
		}
		if matched, err := re.MatchLimit(text, 200*time.Millisecond); err != nil || matched {
			t.Errorf("%q on 100,000 characters: %v, %v, want false and no error", pattern, matched, err)
		}
	}
	// A text that is long enough still matches.
	re, err := compile(`^(?:a{10}){100}$`, true)
	if err != nil {
		t.Fatal(err)
	}
	if matched, err := re.MatchLimit(text[:1000], time.Minute); err != nil || !matched {
		t.Errorf("a thousand characters: %v, %v, want true and no error", matched, err)
	}
	if matched, err := re.MatchLimit(text[:999], time.Minute); err != nil || matched {
		t.Errorf("999 characters: %v, %v, want false and no error", matched, err)
	}
}

// TestCodeUnitPositions checks the two readers of a code unit on every position of a text, forwards and backwards.
func TestCodeUnitPositions(t *testing.T) {
	text := []byte("a\u00e9\u20ac\U00010400\U0010FFFFz")
	want := []rune{'a', 0xE9, 0x20AC, 0xD801, 0xDC00, 0xDBFF, 0xDFFF, 'z'}
	var ends []int
	pos := 0
	for _, unit := range want {
		got, w := unitAt(text, pos)
		if got != unit {
			t.Errorf("unitAt(%d) = %#x, want %#x", pos, got, unit)
		}
		pos += w
		ends = append(ends, pos)
	}
	if pos != len(text) {
		t.Errorf("the units end at %d of %d bytes", pos, len(text))
	}
	for k := len(want) - 1; k >= 0; k-- {
		got, w := unitBefore(text, ends[k])
		if got != want[k] {
			t.Errorf("unitBefore(%d) = %#x, want %#x", ends[k], got, want[k])
		}
		if start := 0; k > 0 {
			start = ends[k-1]
			if ends[k]-w != start {
				t.Errorf("unitBefore(%d) spans %d bytes, want %d", ends[k], w, ends[k]-start)
			}
		}
	}
	if !hasSupplementary(text) || hasSupplementary([]byte("a\u00e9\u20ac\uffff")) {
		t.Errorf("hasSupplementary is wrong")
	}
	// Text that is not UTF-8 is read a byte at a time, as it is when matching by code point.
	for _, bad := range []string{"\xf0\x9f", "\xf0\x9f\x98", "\x80\x80\x80", "\xf0a\x80\x80", "\xf8\x88\x80\x80\x80"} {
		for pos := 0; pos < len(bad); {
			r, w := unitAt([]byte(bad), pos)
			if w != 1 || (r != 0xFFFD && r != rune(bad[pos])) {
				t.Errorf("unitAt(%q, %d) = %#x, %d", bad, pos, r, w)
			}
			pos += w
		}
		for pos := len(bad); pos > 0; {
			r, w := unitBefore([]byte(bad), pos)
			if w != 1 || (r != 0xFFFD && r != rune(bad[pos-1])) {
				t.Errorf("unitBefore(%q, %d) = %#x, %d", bad, pos, r, w)
			}
			pos -= w
		}
	}
}
