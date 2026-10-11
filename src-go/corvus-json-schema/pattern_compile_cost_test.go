package jsonschema

import (
	"fmt"
	"runtime"
	"strings"
	"testing"
)

// TestALongPatternCompilesInMemoryProportionalToItsLength compiles literal patterns of growing length through
// every matcher a literal can take, and counts the bytes allocated, which no load on the machine changes. A pattern
// of 100,000 characters took a second when the alternatives of a pattern were built a character at a time, each
// step copying what was built so far: 16,000 characters allocated 128 MB that way. A long pattern must allocate
// no more than a few times its share of what a short one does.
func TestALongPatternCompilesInMemoryProportionalToItsLength(t *testing.T) {
	round := 0
	compile := func(n int, shape func(string) string) uint64 {
		// A pattern is compiled once for the process, so each must be a new one.
		round++
		pattern := shape(strings.Repeat("a", n-8) + fmt.Sprintf("%08d", round))
		var before, after runtime.MemStats
		runtime.ReadMemStats(&before)
		if _, err := CompileString(`{"pattern": "` + pattern + `"}`); err != nil {
			t.Fatalf("a pattern of %d characters: %v", n, err)
		}
		runtime.ReadMemStats(&after)
		return after.TotalAlloc - before.TotalAlloc
	}
	for name, shape := range map[string]func(string) string{
		"a literal":      func(s string) string { return s },
		"anchored":       func(s string) string { return "^" + s + "$" },
		"alternatives":   func(s string) string { return "^(?:" + s[:len(s)-200] + "|x" + s[len(s)-100:] + ")$" },
		"with an escape": func(s string) string { return s[:len(s)/2] + `\\.` + s[len(s)/2+2:] },
		"in a group":     func(s string) string { return "(" + s[:len(s)-4] + ")" },
	} {
		short, long := compile(1000, shape), compile(16000, shape)
		t.Logf("%s: 1,000 characters allocate %d KB, 16,000 allocate %d KB", name, short>>10, long>>10)
		// Sixteen times the length. Quadratic growth is 256 times.
		if long > 64*short {
			t.Errorf("%s: 16,000 characters allocate %d KB and 1,000 allocate %d KB", name, long>>10, short>>10)
		}
	}
}
