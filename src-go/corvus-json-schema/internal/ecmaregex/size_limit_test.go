package ecmaregex

import (
	"errors"
	"fmt"
	"runtime"
	"strings"
	"testing"
	"time"
)

// The limits on the size of one pattern. A schema may come from outside, so no pattern may take long or much memory
// to read and to compile.

// atTheLimit lists patterns of each kind that costs something to read or to compile, each a little under MaxSize.
// count is the number of copies of the piece that fit.
func atTheLimit() map[string]string {
	fit := func(each int) int { return (MaxSize - 4096) / each }
	lRanges := len(mustProperty("L").ranges) / 2
	nest := func(open, body, close string, depth int) string {
		return strings.Repeat(open, depth) + body + strings.Repeat(close, depth)
	}
	// Groups of one name: each is charged for the groups before it, so 400 of them cost most of the limit.
	named := make([]string, 400)
	for k := range named {
		named[k] = fmt.Sprintf("(?<n>%d)", k)
	}
	return map[string]string{
		"literal":                strings.Repeat("a", fit(2*nodeSize)),
		"alternatives":           strings.TrimSuffix(strings.Repeat("a|", fit(3*nodeSize)), "|"),
		"groups":                 strings.Repeat("(a)", fit(4*nodeSize)),
		"groups read back":       strings.Repeat("(a)", 4000) + strings.Repeat(`\1`, 4000) + strings.Repeat(`\4000`, 300),
		"nested groups":          nest("(", strings.Repeat("a", fit(2*nodeSize)-2*MaxNesting), ")", MaxNesting-1),
		"nested counted loops":   nest("(?:", strings.Repeat("a?b", 2000), "){2,3}", MaxNesting-1),
		"nested lookbehinds":     nest("(?<=", strings.Repeat("a", 2000), ")", MaxNesting-1),
		"counted loops":          strings.Repeat("(?:a|){2}", fit(7*nodeSize)),
		"lazy loops":             strings.Repeat("(?:a|b)*?", fit(7*nodeSize)),
		"properties":             strings.Repeat(`\p{L}`, fit(2*nodeSize)),
		"negated properties":     strings.Repeat(`\P{L}`, fit(2*nodeSize+rangeSize*(lRanges+1))),
		"classes of properties":  strings.Repeat(`[\p{L}\p{N}]`, fit(2*nodeSize+rangeSize*(lRanges+200))),
		"folded negated classes": "(?i:" + strings.Repeat(`[^a]`, fit(2*nodeSize+3000)) + ")",
		"folded dots":            "(?i:" + strings.Repeat(`.`, fit(2*nodeSize+3000)) + ")",
		"folded properties":      "(?i:" + strings.Repeat(`\p{L}`, fit(2*nodeSize+3000+rangeSize*(lRanges+50))) + ")",
		"folded literal":         "(?i:" + strings.Repeat("k", fit(2*nodeSize)) + ")",
		"one large class":        "[" + strings.Repeat(`ĀĂ`, fit(2*rangeSize)) + "]",
		"groups of one name":     strings.Join(named, "|"),
		"named groups, nested":   nest("(?:x|", strings.Repeat("(?<a>b)|(?<c>d)", 40), ")", 600),
		"counted, for regexp":    strings.Repeat("(?:a?){1000}", fit(4*nodeSize)),
		"quantified everything":  strings.Repeat("a{2,5}?", fit(2*nodeSize)),
		"long escapes":           strings.Repeat(`\u{0000000000000000000000000000000000000000000041}`, fit(2*nodeSize)),
	}
}

func mustProperty(name string) *charSet {
	set, ok := propertySet(name)
	if !ok {
		panic(name)
	}
	return set
}

// TestAPatternAtTheLimitOfSizeIsCheap compiles the patterns of atTheLimit and matches each once. The limit of size
// is what bounds the time and the memory of compiling, so the largest pattern of every kind must take little of
// both. The bounds asserted are generous. The figures measured are logged.
func TestAPatternAtTheLimitOfSizeIsCheap(t *testing.T) {
	for name, pattern := range atTheLimit() {
		var before, after runtime.MemStats
		runtime.GC()
		runtime.ReadMemStats(&before)
		start := time.Now()
		re, err := Compile(pattern)
		compiled := time.Since(start)
		if err != nil {
			t.Errorf("%s (%d bytes): %v", name, len(pattern), err)
			continue
		}
		// The match may reach a limit, which is an outcome. What matters here is the first match, which builds the
		// automaton or the backtracking state.
		start = time.Now()
		_, matchErr := re.MatchLimit([]byte("abc"), time.Minute)
		matched := time.Since(start)
		runtime.ReadMemStats(&after)
		allocated := after.TotalAlloc - before.TotalAlloc
		runtime.GC()
		runtime.ReadMemStats(&after)
		retained := int64(after.HeapAlloc) - int64(before.HeapAlloc)
		t.Logf("%-24s %7d bytes, %v: compile %v, first match %v (%v), %d MB allocated by both, %d KB retained", name, len(pattern), re.Engine(),
			compiled.Round(100*time.Microsecond), matched.Round(100*time.Microsecond), matchErr, allocated>>20, retained>>10)
		if compiled > generous || matched > generous {
			t.Errorf("%s took %v to compile and %v for its first match", name, compiled, matched)
		}
		if allocated > 512<<20 || retained > 64<<20 {
			t.Errorf("%s allocated %d MB and retained %d MB", name, allocated>>20, retained>>20)
		}
		runtime.KeepAlive(re)
	}
}

// TestAPatternOverALimitOfSizeIsTooLarge checks each limit: the pattern at the limit compiles and the pattern
// over it is refused with a *TooLargeError that names the limit, in both grammars and by Check, and a pattern that
// is over a limit and also not valid is refused for its size.
func TestAPatternOverALimitOfSizeIsTooLarge(t *testing.T) {
	nest := func(depth int) string { return strings.Repeat("(?:", depth) + "a" + strings.Repeat(")", depth) }
	terms := MaxSize / (2 * nodeSize)
	for _, c := range []struct {
		name, at, over, limit string
		value                 int
	}{
		{"length", `\u{` + strings.Repeat("0", MaxPatternLength-6) + "41}", `\u{` + strings.Repeat("0", MaxPatternLength-5) + "41}", LimitLength, MaxPatternLength},
		{"nesting", nest(MaxNesting - 1), nest(MaxNesting), LimitNesting, MaxNesting},
		{"size of terms", strings.Repeat("a", terms), strings.Repeat("a", terms+1), LimitSize, MaxSize},
		{"size of classes", "[" + strings.Repeat(`ĀĂ`, 60000) + "]", "[" + strings.Repeat(`ĀĂ`, 70000) + "]", LimitSize, MaxSize},
		{"size without the u flag", strings.Repeat(`\-`, terms), strings.Repeat(`\-`, terms+1), LimitSize, MaxSize},
		{"nesting without the u flag", `\-` + nest(MaxNesting-1), `\-` + nest(MaxNesting), LimitNesting, MaxNesting},
		{"nesting, and not valid", nest(MaxNesting-1) + "x", nest(MaxNesting) + "*+)", LimitNesting, MaxNesting},
		{"size, and not valid", strings.Repeat("a", terms), strings.Repeat("a", terms+1) + "(", LimitSize, MaxSize},
	} {
		if _, err := Compile(c.at); err != nil {
			t.Errorf("%s: the pattern at the limit (%d bytes): %v", c.name, len(c.at), err)
		}
		if _, err := Check(c.at); err != nil {
			t.Errorf("%s: Check of the pattern at the limit: %v", c.name, err)
		}
		_, err := Compile(c.over)
		_, checkErr := Check(c.over)
		for _, err := range []error{err, checkErr} {
			var large *TooLargeError
			if !errors.As(err, &large) || large.Limit != c.limit || large.Value != c.value {
				t.Errorf("%s: the pattern over the limit (%d bytes): %v, want a TooLargeError for %s of %d", c.name, len(c.over), err, c.limit, c.value)
			}
		}
		if Valid(c.over) {
			t.Errorf("%s: Valid of the pattern over the limit: true", c.name)
		}
	}
	// A pattern that is not valid and under every limit is not too large.
	for _, pattern := range []string{"(", "a**", `(?<n>a)\k<m>`, "[b-a]", nest(MaxNesting-1) + ")"} {
		_, err := Compile(pattern)
		var large *TooLargeError
		if err == nil || errors.As(err, &large) {
			t.Errorf("Compile(%.20q): %v, want the error of a pattern that is not valid", pattern, err)
		}
	}
}
