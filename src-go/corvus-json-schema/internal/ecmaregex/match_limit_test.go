package ecmaregex

import (
	"errors"
	"regexp/syntax"
	"runtime"
	"strings"
	"testing"
	"time"
)

// The limit on a match. A schema and an instance may both come from outside, so neither a pattern nor a text may
// make a match run without end or hold memory without bound.
//
// No test here depends on a quiet machine. A match that must reach its limit runs a pattern that would take longer
// than the age of the universe, and the bounds on the time it is given are many times what it needs.

// generous is the time a test allows for something that takes milliseconds.
const generous = 20 * time.Second

// forever lists patterns with a text that no backtracking matcher answers, and that no rewrite of this package
// makes easy: each reads a group back inside a repetition, so the ways through the text cannot be told apart.
var forever = []struct{ pattern, text string }{
	{`^(?:(a+)\1*)+$`, strings.Repeat("a", 60) + "b"},
	{`(a*)*\1b`, strings.Repeat("a", 60)},
	{`^(?:(\w+)\s?\1?)+$`, strings.Repeat("ab ", 30) + "!"},
	{`(?=(?:(a+)\1*)+b)a`, strings.Repeat("a", 60)},
}

// TestAMatchIsAbandonedAtItsLimit runs each pattern that never answers with a limit, and expects ErrMatchTime no
// sooner than the limit and not long after it.
func TestAMatchIsAbandonedAtItsLimit(t *testing.T) {
	for _, limit := range []time.Duration{20 * time.Millisecond, 300 * time.Millisecond} {
		for _, c := range forever {
			re, err := Compile(c.pattern)
			if err != nil {
				t.Fatalf("Compile(%q): %v", c.pattern, err)
			}
			start := time.Now()
			matched, err := re.MatchLimit([]byte(c.text), limit)
			took := time.Since(start)
			if !errors.Is(err, ErrMatchTime) || matched {
				t.Errorf("%q with a limit of %v: %v, %v, want false and ErrMatchTime", c.pattern, limit, matched, err)
			}
			if took < limit || took > limit+generous {
				t.Errorf("%q with a limit of %v was abandoned after %v", c.pattern, limit, took)
			}
		}
	}
}

// TestAMatchWithNoLimitIsNotAbandoned checks that a limit of zero is no limit: a match that takes far more steps
// than a small limit allows runs to its answer, as it does through Match.
func TestAMatchWithNoLimitIsNotAbandoned(t *testing.T) {
	re, err := Compile(`^(?:(a+)\1*)+$`)
	if err != nil {
		t.Fatal(err)
	}
	// Some hundred thousand steps: far beyond a limit of a nanosecond, and a few milliseconds of work.
	text := []byte(strings.Repeat("a", 14) + "b")
	if matched, err := re.MatchLimit(text, time.Nanosecond); !errors.Is(err, ErrMatchTime) || matched {
		t.Errorf("with a limit of 1ns: %v, %v, want false and ErrMatchTime", matched, err)
	}
	if matched, err := re.MatchLimit(text, 0); err != nil || matched {
		t.Errorf("with no limit: %v, %v, want false and no error", matched, err)
	}
	if re.Match(text) {
		t.Errorf("Match: true, want false")
	}
	if matched, err := re.MatchLimit(text[:14], generous); err != nil || !matched {
		t.Errorf("a text that matches, with a limit: %v, %v, want true and no error", matched, err)
	}
}

// TestAMatchIsAbandonedAtItsMemoryLimit runs patterns that push on the backtracking stack without end. With a limit
// on the match the stack is capped, and the error is ErrMatchMemory long before the clock says anything. Without
// the cap the first of these took 5.7 GB and the Go runtime ended the process, which nothing can recover from.
func TestAMatchIsAbandonedAtItsMemoryLimit(t *testing.T) {
	for _, c := range []struct{ pattern, text string }{
		{`(?:a?){2147483647}`, ""},
		{`(?:a?){100000000}`, strings.Repeat("a", 100)},
		{`(?:(?:(?:a?){1000}){1000}){1000}`, strings.Repeat("a", 20)},
		{`(?:(a)|b|){2147483647}\1`, ""},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatalf("Compile(%q): %v", c.pattern, err)
		}
		var before, after runtime.MemStats
		runtime.GC()
		runtime.ReadMemStats(&before)
		start := time.Now()
		matched, err := re.MatchLimit([]byte(c.text), time.Hour)
		took := time.Since(start)
		runtime.ReadMemStats(&after)
		if !errors.Is(err, ErrMatchMemory) || matched {
			t.Errorf("%q: %v, %v, want false and ErrMatchMemory", c.pattern, matched, err)
		}
		if took > generous {
			t.Errorf("%q was abandoned after %v", c.pattern, took)
		}
		// Every stack the match grew through, which is a few times the largest: 24 bytes an entry.
		if grew := after.TotalAlloc - before.TotalAlloc; grew > 16*24*maxStack {
			t.Errorf("%q allocated %d MB before it was abandoned", c.pattern, grew>>20)
		}
	}
}

// TestALimitDoesNotChangeAnAnswer runs patterns of every back end with a limit they do not reach and expects the
// answer they give without one.
func TestALimitDoesNotChangeAnAnswer(t *testing.T) {
	long := strings.Repeat("ab", 50000)
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		{`^[a-z]+$`, "abc", true}, {`^[a-z]+$`, "abc1", false},
		{`^\p{L}+$`, "café", true}, {`^\p{L}+$`, "café ", false},
		{`^(?=.*\d)\w+$`, "abc1", true}, {`^(?=.*\d)\w+$`, "abcd", false},
		{`^(a+)\1$`, "aaaa", true}, {`^(a+)\1$`, "aaa", false},
		{`(?<=\$)\d+`, "cost $42", true},
		{`^(?=.)(?:a|b)*$`, long, true}, {`^(?=.)(?:a|b)*c$`, long, false},
		{`^[ab]*$`, long, true}, {`^[ab]*c$`, long, false},
		{`^[abé]*$`, long + "é", true}, {`^[abé]*c$`, long + "é", false},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatalf("Compile(%q): %v", c.pattern, err)
		}
		for _, limit := range []time.Duration{0, time.Minute} {
			if got, err := re.MatchLimit([]byte(c.text), limit); err != nil || got != c.want {
				t.Errorf("%q on %.20q with a limit of %v: %v, %v, want %v", c.pattern, c.text, limit, got, err, c.want)
			}
		}
	}
}

// TestALongTextIsNotGivenToTheRegexpPackage checks where a pattern on the regexp package is matched. A match there
// cannot be stopped and takes time proportional to the program times the text, so with a limit the text goes there
// only when that product, at re2StepTime each, is within the limit. Any other text goes to the backtracking
// matcher, which reads the clock. The answers are the same.
func TestALongTextIsNotGivenToTheRegexpPackage(t *testing.T) {
	re, err := Compile(`^(?:[a-cé]{0,500}x)*$`)
	if err != nil {
		t.Fatal(err)
	}
	if re.Engine() != EngineRE2 || re.reCost < 500 {
		t.Fatalf("engine %v with a program of %d instructions, want the regexp package and more than 500", re.Engine(), re.reCost)
	}
	short := []byte("éabcx")
	long := []byte(strings.Repeat("éabcx", 100000))
	fits := func(text []byte, limit time.Duration) bool {
		return time.Duration(re.reCost)*time.Duration(len(text)) <= limit/re2StepTime
	}
	if !fits(short, time.Second) || fits(long, time.Second) {
		t.Fatalf("the texts are not on each side of the limit for a program of %d instructions", re.reCost)
	}
	if matched, err := re.MatchLimit(short, time.Second); err != nil || !matched || re.backProg != nil {
		t.Errorf("a short text: %v, %v, backtracking program built %v, want true, no error and not built", matched, err, re.backProg != nil)
	}
	if matched, err := re.MatchLimit(long, time.Second); err != nil || !matched || re.backProg == nil {
		t.Errorf("a long text: %v, %v, backtracking program built %v, want true, no error and built", matched, err, re.backProg != nil)
	}
	if matched, err := re.MatchLimit(append(long, 'y'), 0); err != nil || matched {
		t.Errorf("a long text with no limit: %v, %v, want false and no error", matched, err)
	}
}

// TestTheRegexpPackageIsGivenNoLargeProgram checks the estimate of the program the regexp package would compile.
// It is what keeps a pattern such as \p{L}{1000} written 90 times, which is 90,000 instructions and took the
// package more than 20 seconds on a text of 100,000 characters, on the backtracking matcher. The estimate must not
// be far below the true figure for any pattern the package is given.
func TestTheRegexpPackageIsGivenNoLargeProgram(t *testing.T) {
	for _, c := range []struct {
		pattern string
		engine  Engine
	}{
		{`^[a-z]{1,64}$`, EngineRE2}, {`a{1000}`, EngineRE2}, {`(?:a|b|ab)*c`, EngineRE2},
		{strings.Repeat(`\p{L}{1000}`, 90), EngineBacktrack}, {`(?:[ab]{1000}){50}c`, EngineBacktrack},
		{`(?:a{100}){101}`, EngineBacktrack}, {`(?:(?:a?){100}){100}b`, EngineBacktrack},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatalf("Compile(%.40q): %v", c.pattern, err)
		}
		if re.Engine() != c.engine {
			t.Errorf("%.40q is on %v, want %v", c.pattern, re.Engine(), c.engine)
		}
	}
	o := loadOracle(t)
	checked, largest := 0, 0
	for _, h := range o.Hand {
		root, ps, err := parse(h.P, true)
		if err != nil {
			if root, ps, err = parse(h.P, false); err != nil {
				continue
			}
		}
		if ps.needsBacktracking {
			continue
		}
		var b strings.Builder
		writeRE2(&b, root)
		tree, err := syntax.Parse(b.String(), syntax.Perl)
		if err != nil {
			continue
		}
		prog, err := syntax.Compile(tree.Simplify())
		if err != nil {
			continue
		}
		checked++
		estimate := re2Cost(root)
		if len(prog.Inst) > largest {
			largest = len(prog.Inst)
		}
		// The package adds a few instructions of its own to every program, and merges what the estimate counts
		// apart, so the estimate may be above. It must not be much below.
		if 2*estimate+8 < len(prog.Inst) {
			t.Errorf("%q: estimated %d instructions, the regexp package compiles %d", h.P, estimate, len(prog.Inst))
		}
	}
	t.Logf("%d patterns checked, the largest program %d instructions", checked, largest)
	if checked < 100 {
		t.Errorf("only %d patterns were checked", checked)
	}
}

// TestALimitedMatchAllocatesNothing checks the steady state with a limit on the match, on patterns that reach the
// first check of the clock and on one that is abandoned there. The clock is read into the pooled state of the
// match, and the errors are values that exist already.
func TestALimitedMatchAllocatesNothing(t *testing.T) {
	if raceEnabled {
		t.Skip("the race detector makes sync.Pool drop values, so pooled state is allocated again")
	}
	long := []byte(strings.Repeat("ab", 20000))
	for _, c := range []struct {
		pattern string
		text    []byte
		limit   time.Duration
		err     error
	}{
		{`^(?=.)(?:a|b)*c$`, long, time.Minute, nil},
		{`^(?:(a|b)\1?)+$`, long[:4000], time.Minute, nil},
		{`^[abé]{0,60000}c$`, append([]byte("é"), long...), time.Minute, nil},
		{`^(?:(a+)\1*)+$`, []byte(strings.Repeat("a", 60) + "b"), time.Millisecond, ErrMatchTime},
		// A match that takes the memory of failed places into use, which is pooled with the rest of its state.
		{`^(?=.)(?:a|aa)+$`, []byte(strings.Repeat("a", 1000) + "b"), time.Minute, nil},
		{`(?=.)(?:a|b)*c`, long, 0, nil},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			t.Fatal(err)
		}
		// Grow the pooled state to what the text needs.
		for i := 0; i < 3; i++ {
			if _, err := re.MatchLimit(c.text, c.limit); err != c.err {
				t.Fatalf("%q: %v, want %v", c.pattern, err, c.err)
			}
		}
		if allocs := testing.AllocsPerRun(20, func() { re.MatchLimit(c.text, c.limit) }); allocs != 0 {
			t.Errorf("%q with a limit of %v: %v allocations per match, want 0", c.pattern, c.limit, allocs)
		}
	}
}

// BenchmarkMatchLimit matches patterns that run on the backtracking matcher with no limit and with a limit they do
// not reach, to show what counting the steps and reading the clock cost. The first group ends before its first
// check. The second makes tens of thousands of steps for each match, so the clock is read several times. The third
// takes the memory of failed places into use.
func BenchmarkMatchLimit(b *testing.B) {
	short := []byte("the quick brown fox jumps over the lazy dog 12345")
	long := []byte(strings.Repeat("ab", 5000) + "!")
	for _, c := range []struct {
		pattern string
		text    []byte
	}{
		{`^(?!.*\d{6})(?=.*\bfox\b).+$`, short}, {`(?<=dog )\d+$`, short}, {`(\w)\1`, short}, {`^(?=.)(?:\w+\s?)+$`, short},
		{`^(?=.)(?:a|b)*c$`, long}, {`^(?:(a|b)\1?)+$`, long}, {`(?=.)(?:a|b)*c`, long[:2000]},
		{`^(?=.)(?:a|aa)+$`, []byte(strings.Repeat("a", 500) + "b")}, {`^(?=.)(?:a?b?)*x`, long[:1000]},
	} {
		re, err := Compile(c.pattern)
		if err != nil {
			b.Fatal(err)
		}
		for _, limit := range []time.Duration{0, time.Minute} {
			name := "no limit"
			if limit > 0 {
				name = "limit"
			}
			b.Run(c.pattern+"/"+name, func(b *testing.B) {
				b.ReportAllocs()
				for i := 0; i < b.N; i++ {
					if _, err := re.MatchLimit(c.text, limit); err != nil {
						b.Fatal(err)
					}
				}
			})
		}
	}
}
