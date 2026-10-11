package ecmaregex

import (
	"errors"
	"slices"
	"strings"
	"testing"
	"time"
)

// The memory of the places where a way has failed (see memo in vm.go).

// withMemoFromTheStart runs a function with every match taking its memory before its first step. A match otherwise
// takes it only once it has made many steps, which few of the short texts of the tests reach.
func withMemoFromTheStart(run func()) {
	memoFromTheStart = true
	defer func() { memoFromTheStart = false }()
	run()
}

// TestFailedPlacesAreRemembered checks the patterns of the memo set against V8, with the memory in use from the
// first step and as it comes into use by itself. Then it checks every other set and the hand-written and generated
// patterns of the oracle the same way: remembering a failure must change no answer.
func TestFailedPlacesAreRemembered(t *testing.T) {
	c := loadCases(t)
	answers, matched, _, _ := checkCases(t, c.Memo, 10*time.Second)
	withPoints := 0
	for _, m := range c.Memo {
		root, _, err := parse(m.P, true)
		if err != nil {
			t.Fatalf("parse(%q): %v", m.P, err)
		}
		if len(compileProgram(root, true).points) > 0 {
			withPoints++
		}
	}
	t.Logf("%d patterns (%d with memo points), %d answers per back end (%d of them a match)", len(c.Memo), withPoints, answers, matched)
	if len(c.Memo) < 50 || withPoints < 45 || matched < 500 {
		t.Errorf("the cases are not the ones this test is about")
	}
	withMemoFromTheStart(func() {
		total := 0
		for _, set := range [][]v8Case{c.Memo, c.Quick, c.Loops, c.Needs, c.Units, c.Fast} {
			a, _, _, _ := checkCases(t, set, 30*time.Second)
			total += a
		}
		o := loadOracle(t)
		for _, h := range o.Hand {
			re, backtrack := checkVerdict(t, h.P, h.U, h.L)
			if re == nil || v8ModifierBugs[h.P] {
				continue
			}
			a, _ := checkAnswers(t, h.P, re, backtrack, o.Inputs, h.M)
			total += a
		}
		t.Logf("%d answers per back end with the memory in use from the first step", total)
	})
}

// TestARepetitionInARepetitionIsAnswered runs patterns that a backtracking matcher with no memory takes time that
// doubles with each character for, on texts of 40 to 100,000 characters. Each is on the backtracking matcher
// (a lookaround, a multiline anchor or a case-insensitive word boundary keeps it there) and has no backreference.
// With the memory each takes milliseconds. The limit of the match is ten seconds, so a match that goes the long
// way round is abandoned and fails the test on its error.
func TestARepetitionInARepetitionIsAnswered(t *testing.T) {
	a := strings.Repeat("a", 60)
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		{`(?=a)(a*)*b`, a, false}, {`(?=a)(a*)*b`, a + "b", true},
		{`^(?=.)(?:a+)+$`, a + "x", false}, {`^(?=.)(?:a+)+$`, a, true},
		{`^(?=.)(?:a|aa)+$`, a + "b", false}, {`^(?=.)(?:a|aa)+$`, a, true},
		{`^(?=.)(?:a?b?)*x`, strings.Repeat("ab", 30), false}, {`^(?=.)(?:a?b?)*x`, strings.Repeat("ab", 30) + "x", true},
		{`^(?=.)(?:(?:a?b?)*x?)*$`, strings.Repeat("ab", 30) + "!", false},
		{`^(?=.)(?:(?:a*)+b?)+$`, a + "!", false}, {`^(?=.)(?:(?:a*)+b?)+$`, a + "b", true},
		{`^(?=.)(?:(?:a?)+?b?)*?x`, a, false}, {`(?=.)(?:a*?b*?)*?x$`, a + "b", false},
		{`(?i:\b(?:\w+\W?)+\b!)`, strings.Repeat("ab ", 40), false}, {`(?i:\b(?:\w+\W?)+\b!)`, strings.Repeat("ab ", 40) + "ab!", true},
		{`(?m:^(?:\w+\s?)+$)`, strings.Repeat("a ", 30) + "!", false},
		{`^(?!(?:a+)+x)a*b`, a + "b", true}, {`^(?!(?:a+)+x)a*b`, a, false},
		{`(?<!(?:a|aa)+b)x$`, a + "x", true},
		{`(?=.).*.*.*x`, strings.Repeat("a", 3000), false}, {`(?=.)a*?a*?a*?b`, strings.Repeat("a", 3000), false},
		// A choice for every character of the text, from every position of it: the failures of one start serve the
		// next.
		{`(?=.)(?:a|b)*c`, strings.Repeat("ab", 50000), false},
		{`(?=.)(?:a|\u00e9)*c`, strings.Repeat("a\u00e9", 30000), false},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatalf("Compile(%q): %v", c.pattern, err)
		}
		if re.Engine() != EngineBacktrack || len(re.prog.points) == 0 {
			t.Fatalf("%q is on %v with %d memo points", c.pattern, re.Engine(), len(re.prog.points))
		}
		for _, limit := range []time.Duration{10 * time.Second, 0} {
			if got, err := re.MatchLimit([]byte(c.text), limit); err != nil || got != c.want {
				t.Errorf("%q on %d characters with a limit of %v: %v, %v, want %v", c.pattern, len(c.text), limit, got, err, c.want)
			}
		}
	}
}

// TestABackreferenceTurnsTheMemoryOff checks a pattern that reads a group back has no memo points: what a group
// holds changes what follows a place, so a failure there says nothing of the next time. Such a pattern is still
// abandoned at its limit.
func TestABackreferenceTurnsTheMemoryOff(t *testing.T) {
	for _, pattern := range []string{`(a*)*\1b`, `^(?:(a+)\1*)+$`, `(a)(?:b|c)*\1`, `(?<n>a)(?:b+)+\k<n>`} {
		re, err := compile(pattern, true)
		if err != nil {
			t.Fatal(err)
		}
		if len(re.prog.points) != 0 {
			t.Errorf("%q has %d memo points", pattern, len(re.prog.points))
		}
	}
	re, _ := Compile(`(a*)*\1b`)
	if _, err := re.MatchLimit([]byte(strings.Repeat("a", 60)), 50*time.Millisecond); !errors.Is(err, ErrMatchTime) {
		t.Errorf("a nested repetition before a backreference: %v, want ErrMatchTime", err)
	}
}

// TestMemoPoints checks where a program has memo points and which loops each tells apart. A split, a loop with no
// upper bound and a least count of zero or one, and a single character loop have one, unless they are inside a loop
// that counts to a bound or inside more than two loops. A lookaround starts afresh: what its body does depends on
// nothing outside it. Each point is given as the instruction's operation and its loops.
func TestMemoPoints(t *testing.T) {
	for _, c := range []struct {
		pattern string
		points  []string
	}{
		{`(?:a|b)*c`, []string{"split", "split"}},
		{`(?:a?b?)*c`, []string{"loop", "rep 0", "rep 0"}},
		{`(?:(?:a?)*b)*`, []string{"split", "loop", "rep 0"}},
		{`(?:(?:a?)+b?)+x`, []string{"loop", "loop 0", "rep 0 1", "rep 0"}},
		// The innermost a? is inside three loops and has no point.
		{`(?:(?:(?:a?)*b?)*x?)*`, []string{"loop", "loop 0", "loop 0 1", "rep 0 1", "rep 0"}},
		// A loop that counts to a bound has no point, and neither has anything in it.
		{`(?:a|){2,3}(?:b|c)`, []string{"split"}},
		{`(?:(?:a|b){2}x?)*`, []string{"split", "rep"}},
		{`(?:(?:a?b?)*?(?:c|d)){3}`, nil},
		// The body of a lookaround starts afresh.
		{`(?:a?(?=(?:b|c)*x))*`, []string{"loop", "rep 0", "split", "split"}},
		{`(?:(?:a|){2}(?=b|c))*`, []string{"loop", "split"}},
		{`(?<=(?:a?b?)*)x`, []string{"loop", "split 0", "split 0"}},
		{`a*b*?c{2,3}`, []string{"rep", "lazy", "rep"}},
		{`(?:a+)+$`, []string{"rep", "split"}},
		{`(a)\1(?:b|c)`, nil},
	} {
		root, _, err := parse(c.pattern, true)
		if err != nil {
			t.Fatalf("parse(%q): %v", c.pattern, err)
		}
		p := compileProgram(root, true)
		var points []string
		for _, i := range p.insts {
			if i.m == 0 {
				continue
			}
			s := map[opcode]string{opSplit: "split", opLoop: "loop", opRep: "rep", opRepLazy: "lazy"}[i.op]
			for _, l := range p.points[i.m-1].loops {
				if l >= 0 {
					s += " " + string(rune('0'+l))
				}
			}
			points = append(points, s)
		}
		if !slices.Equal(points, c.points) || len(p.points) != len(c.points) {
			t.Errorf("%q: memo points %q, want %q", c.pattern, points, c.points)
		}
	}
}

// TestTheMemoryIsBounded checks when a match takes the memory into use: not before it has made as many steps as
// the memory has words and 64 more for each position of the text, and never when the memory would be more than
// maxMemoWords.
func TestTheMemoryIsBounded(t *testing.T) {
	for _, c := range []struct {
		points, width, checks int
		on, off               bool
	}{
		{0, 100, 1000, false, true},
		// 19 words and 6,400 steps for the positions: the second check.
		{3, 100, 1, false, false}, {3, 100, 2, true, false},
		// 18,751 words and 6,400,064 steps: the check after 6,418,815 steps.
		{3, 100001, 1567, false, false}, {3, 100001, 1568, true, false},
		{1000, 100001, 1 << 20, false, true}, {167, 100001, 1 << 20, true, false}, {168, 100001, 1 << 20, false, true},
	} {
		st := &state{points: c.points, width: c.width, memoOff: c.points == 0}
		for k := 0; k < c.checks && !st.memoOn && !st.memoOff; k++ {
			st.checks++
			st.remember()
		}
		if st.memoOn != c.on || st.memoOff != c.off {
			t.Errorf("%d points, %d positions, %d checks: in use %v, never %v, want %v and %v", c.points, c.width, c.checks, st.memoOn, st.memoOff, c.on, c.off)
		}
		if words := (c.points*c.width<<maxMemoLoops + 63) >> 6; st.memoOn && (len(st.memo) != words || words > maxMemoWords) {
			t.Errorf("%d points, %d positions: a memory of %d words, want %d", c.points, c.width, len(st.memo), words)
		}
	}
	// A match too large for the memory still has its limit.
	alternatives := make([]string, 400)
	for k := range alternatives {
		alternatives[k] = "a" + strings.Repeat("b", k%7)
	}
	re, err := compile(`^(?=.)(?:`+strings.Join(alternatives, "|")+`)+$`, true)
	if err != nil {
		t.Fatal(err)
	}
	if _, err := re.MatchLimit([]byte(strings.Repeat("ab", 50000)+"x"), 100*time.Millisecond); err != nil && !errors.Is(err, ErrMatchTime) {
		t.Errorf("a match too large for the memory: %v", err)
	}
}
