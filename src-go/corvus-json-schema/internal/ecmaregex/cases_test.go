package ecmaregex

import (
	"encoding/json"
	"os"
	"testing"
	"time"
)

// A v8Case is one pattern of testdata/v8_cases.json with what V8 says of it: whether it is valid with the u flag and
// with no flag, and whether it matches each text. testdata/gen_cases.js writes the file.
type v8Case struct {
	P     string `json:"p"`
	U     bool   `json:"u"`
	L     bool   `json:"l"`
	Tests []struct {
		T    string `json:"t"`
		Want bool   `json:"want"`
	} `json:"tests"`
}

type v8Cases struct {
	Node  string   `json:"node"`
	Units []v8Case `json:"units"`
	Loops []v8Case `json:"loops"`
	Fast  []v8Case `json:"fast"`
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
// limit to answer all its texts.
func checkCases(t *testing.T, cases []v8Case, limit time.Duration) (answers, matched, legacy, invalid int) {
	t.Helper()
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
