package ecmaregex

import (
	"testing"
	"time"
)

// TestTheNextCharacterRulesOutAWay checks the patterns of the quick set against V8 on both back ends. They have a
// choice, a loop or a lookaround where the next character of the text rules a way out, with everything a way can
// end with or go through without a character, which is where ruling a way out wrongly would show. The first three
// are from a fuzz set and took the matcher 12 and 16 seconds for their texts before it looked at the next
// character. A pattern has ten seconds here.
func TestTheNextCharacterRulesOutAWay(t *testing.T) {
	c := loadCases(t)
	start := time.Now()
	answers, matched, legacy, _ := checkCases(t, c.Quick, 10*time.Second)
	t.Logf("%d patterns (%d valid only with no flag), %d answers per back end (%d of them a match) in %v", len(c.Quick), legacy, answers, matched,
		time.Since(start).Round(time.Millisecond))
	if len(c.Quick) < 75 || matched < 2000 {
		t.Errorf("the cases are not the ones this test is about")
	}
}

// TestQuick checks what setQuick works out for the first instruction of a program: the ASCII characters a match can
// start with, whether it can start with one that is not ASCII, and whether it can end without one while text is
// left and at the end of the text.
func TestQuick(t *testing.T) {
	for _, c := range []struct {
		pattern  string
		ascii    string
		other    bool
		mid, end bool
	}{
		{`abc`, "a", false, false, false}, {`[a-c]x`, "abc", false, false, false}, {`a|b|\u00e9`, "ab", true, false, false},
		{`a?b`, "ab", false, false, false}, {`a*$`, "a", false, false, true}, {`(?:a|)b`, "ab", false, false, false},
		{`$`, "", false, false, true}, {`(?:a|$)b`, "a", false, false, false},
		{`(?=a)[a-c]`, "a", false, false, false}, {`(?=[ab])(?:b|x)`, "b", false, false, false}, {`(?!a)[a-c]`, "abc", false, false, false},
		{`(?=a|$)(?:a|)$`, "a", false, false, true}, {`(?<=a)b`, "b", false, false, false}, {`(?<=a|b)$`, "", false, false, true},
		{`\bx`, "x", false, false, false}, {`^a`, "a", false, false, false}, {`(?:a|b)*c`, "abc", false, false, false},
		{`(?:a?b?)*c`, "abc", false, false, false}, {`(?:a|){2}x`, "ax", false, false, false}, {`[^a]`, "bcd", true, false, false},
		{`(?=)a`, "a", false, false, false}, {`[]|a`, "a", false, false, false}, {`(?!)[]`, "", false, false, false},
	} {
		root, _, err := parse(c.pattern, true)
		if err != nil {
			t.Fatalf("parse(%q): %v", c.pattern, err)
		}
		q := compileProgram(root, true).quick[0]
		ok := q.other == c.other && q.mid == c.mid && q.end == c.end
		for _, b := range []byte(c.ascii) {
			ok = ok && q.ascii[b>>6]>>(b&63)&1 != 0
		}
		// A character the pattern cannot start with: none of the cases starts with a digit.
		ok = ok && (q.ascii['0'>>6]>>('0'&63)&1 != 0) == (c.pattern == `[^a]`)
		if !ok {
			t.Errorf("%q: quick %x, other %v, mid %v, end %v, want %q, %v, %v, %v", c.pattern, q.ascii, q.other, q.mid, q.end, c.ascii, c.other, c.mid, c.end)
		}
	}
	// A backreference and the body of a lookbehind allow anything.
	for _, pattern := range []string{`(a)\1x`, `(?<=ab)`} {
		root, _, err := parse(pattern, true)
		if err != nil {
			t.Fatal(err)
		}
		p := compileProgram(root, true)
		found := false
		for pc, i := range p.insts {
			if i.op == opBackref || i.op == opCharBack {
				found = true
				if q := p.quick[pc]; q.ascii != [2]uint64{^uint64(0), ^uint64(0)} || !q.other || !q.mid || !q.end {
					t.Errorf("%q: instruction %d does not allow everything", pattern, pc)
				}
			}
		}
		if !found {
			t.Errorf("%q has no instruction this is about", pattern)
		}
	}
}
