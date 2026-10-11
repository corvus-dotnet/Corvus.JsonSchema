package ecmaregex

import (
	"fmt"
	"runtime"
	"strings"
	"testing"
)

// The regexp package keeps the ranges of a class for every copy of it that a counted repetition writes out, and
// its one-pass matcher keeps them again with a table beside them. A class of a Unicode property has hundreds of
// ranges, so a pattern of 25 characters such as ^[\p{L}\p{N}_-]{1,450}$ held 10 megabytes. The program the package
// would hold is estimated before it is compiled (see re2Size), and a pattern over maxRE2Size is the backtracking
// matcher's, which holds a class once however often the pattern repeats it.

// retainedBy returns the heap one pattern holds, as the mean over count patterns that make gives, each compiled
// and matched against texts that build everything a pattern builds on its first matches.
func retainedBy(t *testing.T, count int, make func(n int) string) (first *Regexp, bytes int64) {
	t.Helper()
	sources := []string{}
	for n := 0; n < count; n++ {
		sources = append(sources, make(n))
	}
	held := []*Regexp{}
	var before, after runtime.MemStats
	runtime.GC()
	runtime.ReadMemStats(&before)
	for _, source := range sources {
		re, err := Compile(source)
		if err != nil {
			t.Fatalf("%.40q: %v", source, err)
		}
		for _, text := range []string{"abc", "café", "a.b.c", strings.Repeat("z", 40), "\U00010400x"} {
			if _, err := re.MatchLimit([]byte(text), 0); err != nil {
				t.Fatalf("%.40q on %q: %v", source, text, err)
			}
		}
		held = append(held, re)
	}
	runtime.GC()
	runtime.GC()
	runtime.ReadMemStats(&after)
	bytes = (int64(after.HeapAlloc) - int64(before.HeapAlloc)) / int64(count)
	runtime.KeepAlive(held)
	return held[0], bytes
}

// TestTheRegexpPackageHoldsNoLargeProgram checks which back end a pattern gets, that it answers as before, and
// that no pattern holds more than the limit.
func TestTheRegexpPackageHoldsNoLargeProgram(t *testing.T) {
	for _, c := range []struct {
		pattern string
		engine  Engine
		yes     string
	}{
		{`^\p{L}+$`, EngineRE2, "caf\u00e9"}, {`^[\p{L}\p{N}_-]{1,8}$`, EngineRE2, "caf\u00e9"},
		{`^[a-z0-9_-]{1,900}$`, EngineRE2, "cafe"}, {`^(?:[a-c]x|[^a]y){1,500}z$`, EngineRE2, "ax\u00e9yz"},
		{`^[\p{L}\p{N}_-]{1,450}$`, EngineBacktrack, "caf\u00e9"},
		{`^[\p{L}\p{N}_-]{480}$`, EngineBacktrack, strings.Repeat("caf\u00e9", 120)},
		{`^[\p{L}\p{N}\p{M}\p{P}\p{S}]{1,490}$`, EngineBacktrack, "caf\u00e9"}, {`^\P{Cn}{1,990}$`, EngineBacktrack, "caf\u00e9"},
	} {
		first, bytes := retainedBy(t, 8, func(int) string { return c.pattern })
		t.Logf("%-40s %v, %d KB a pattern, counted as %d KB", c.pattern, first.Engine(), bytes>>10, first.Retained()>>10)
		if first.Engine() != c.engine {
			t.Errorf("%s is on %v, want %v", c.pattern, first.Engine(), c.engine)
		}
		if bytes > maxRE2Size {
			t.Errorf("%s holds %d KB, which is over the limit of %d KB", c.pattern, bytes>>10, maxRE2Size>>10)
		}
		if !first.MatchString(c.yes) {
			t.Errorf("%s does not match %q", c.pattern, c.yes)
		}
		if first.MatchString(c.yes+"\U000e0080") || first.MatchString("") {
			t.Errorf("%s matches a text that ends in a character with no assignment, or the empty text", c.pattern)
		}
	}
}

// TestNoCountOfAWideClassHoldsALargeProgram goes through the counts of a class of many ranges, on both sides of
// the count at which the pattern leaves the regexp package. Whichever back end has it, the pattern holds less than
// the limit.
func TestNoCountOfAWideClassHoldsALargeProgram(t *testing.T) {
	engines := map[Engine]int{}
	for _, class := range []string{`[\p{L}\p{N}_-]`, `\P{Cn}`, `[\p{L}\p{N}\p{M}\p{P}\p{S}]`, `[^\p{Lo}]`} {
		for _, count := range []string{"{1,16}", "{1,32}", "{1,40}", "{1,48}", "{1,56}", "{1,64}", "{1,72}", "{1,80}", "{1,96}", "{1,128}", "{60}", "{80}", "{120}", "{1,999}"} {
			pattern := "^" + class + count + "$"
			first, bytes := retainedBy(t, 4, func(int) string { return pattern })
			engines[first.Engine()]++
			if bytes > maxRE2Size || bytes > int64(first.Retained()) {
				t.Errorf("%s on %v holds %d KB, and is counted as %d KB", pattern, first.Engine(), bytes>>10, first.Retained()>>10)
			}
		}
	}
	if engines[EngineRE2] < 8 || engines[EngineBacktrack] < 8 {
		t.Errorf("the patterns are on %v, want some on each back end", engines)
	}
}

// TestAPatternIsCountedAsNoLessThanItHolds compares what Retained says of a pattern with the heap it holds, for
// patterns of each back end and shape. The count is what the cache of compiled patterns adds up, so it must not be
// below the truth. It may be well above it.
func TestAPatternIsCountedAsNoLessThanItHolds(t *testing.T) {
	long := strings.Repeat(`[ĀĂĄĆ]`, 1500)
	var alternatives strings.Builder
	for i := 0; i < 300; i++ {
		fmt.Fprintf(&alternatives, "[a-f]*w%d+|", i)
	}
	for name, make := range map[string]func(n int) string{
		"small":                    func(n int) string { return fmt.Sprintf("^(a|b%d)*c+(d|e)?f$", n) },
		"groups":                   func(n int) string { return fmt.Sprintf("^(a%d|b|c|[d-f]x|gg)(h|i|j)(k|l)$", n) },
		"property":                 func(n int) string { return fmt.Sprintf(`^\p{L}+%d$`, n) },
		"three properties":         func(n int) string { return fmt.Sprintf(`^\p{L}+%d\p{L}*x\p{L}?$`, n) },
		"counted property":         func(n int) string { return fmt.Sprintf(`^%d[\p{L}\p{N}_-]{1,48}$`, n) },
		"counted":                  func(n int) string { return fmt.Sprintf(`^(?:[a-c]x|%d[^a]y){1,500}z$`, n) },
		"nested counts":            func(n int) string { return fmt.Sprintf(`^%d(?:\p{Lu}{1,8}){1,8}$`, n) },
		"alternatives":             func(n int) string { return fmt.Sprintf("x%d(?:%sz)*y", n, alternatives.String()) },
		"many states":              func(n int) string { return fmt.Sprintf("%d(a|b)*a(a|b)(a|b)(a|b)(a|b)(a|b)(a|b)$", n) },
		"backreference":            func(n int) string { return fmt.Sprintf(`^(?=a)a%d(b)\1$`, n) },
		"counted, backtracking":    func(n int) string { return fmt.Sprintf(`^(?=a)(?:\p{L}%d|[ĀĂ]){1,5000}$`, n) },
		"long, backtracking":       func(n int) string { return fmt.Sprintf("^(?=a)%d%s", n, long) },
		"no u flag":                func(n int) string { return fmt.Sprintf(`^\-?%d[a-z]{1,40}$`, n) },
		"no u flag, long, counted": func(n int) string { return fmt.Sprintf(`^\-?%d(?:[a-z]{1,30}x){1,30}$`, n) },
	} {
		first, bytes := retainedBy(t, 64, make)
		t.Logf("%-26s %v, %d bytes a pattern, counted as %d", name, first.Engine(), bytes, first.Retained())
		if counted := int64(first.Retained()); bytes > counted {
			t.Errorf("%s: a pattern holds %d bytes and is counted as %d", name, bytes, counted)
		}
	}
}
