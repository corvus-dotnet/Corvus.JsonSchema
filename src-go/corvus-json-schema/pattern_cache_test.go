package jsonschema

import (
	"fmt"
	"runtime"
	"strings"
	"sync"
	"testing"
)

// The cache of compiled patterns is for the whole process, and a pattern comes from a schema, which may come from
// outside. It kept every pattern it was ever given, so a process that compiles schemas for its callers grew for as
// long as it ran. It is bounded now, in the number of patterns and in the bytes counted for them.

// heapNow returns the bytes of the heap that are in use, after a collection.
func heapNow() int64 {
	var stats runtime.MemStats
	runtime.GC()
	runtime.GC()
	runtime.ReadMemStats(&stats)
	return int64(stats.HeapAlloc)
}

// figures returns the number of patterns the cache holds and the bytes it counts for them, and the same two as its
// order and its patterns have them.
func (c *patternStore) figures() (patterns, bytes, queued, sum int) {
	c.mu.RLock()
	defer c.mu.RUnlock()
	for _, p := range c.patterns {
		sum += p.retained
	}
	for i := 0; i < c.count; i++ {
		if _, ok := c.patterns[c.order[(c.first+i)%len(c.order)]]; ok {
			queued++
		}
	}
	return len(c.patterns), c.bytes, queued, sum
}

// clear empties the cache.
func (c *patternStore) clear() {
	c.mu.Lock()
	defer c.mu.Unlock()
	clear(c.patterns)
	clear(c.order[:])
	c.first, c.count, c.bytes = 0, 0, 0
}

// checkCache checks what the cache says of itself against what it holds.
func checkCache(t *testing.T) {
	t.Helper()
	patterns, bytes, queued, sum := patternCache.figures()
	if patterns > maxCachedPatterns || bytes > maxCachedBytes {
		t.Fatalf("the cache holds %d patterns counted as %d bytes, over its bounds of %d and %d", patterns, bytes, maxCachedPatterns, maxCachedBytes)
	}
	if queued != patterns || sum != bytes {
		t.Fatalf("the cache holds %d patterns and has %d in its order, counts %d bytes and its patterns come to %d", patterns, queued, bytes, sum)
	}
}

// TestTheCacheOfPatternsIsBoundedInNumber compiles three times as many patterns as the cache holds, of each kind
// of matcher. The cache holds no more than its bound, the patterns compiled first have left it, and a pattern that
// has left it still answers for whoever holds it, and compiles again to a matcher of its own.
func TestTheCacheOfPatternsIsBoundedInNumber(t *testing.T) {
	budget := patternBudget{}
	first, err := compilePattern("^cache-bound-0+$")
	if err != nil {
		t.Fatal(err)
	}
	shapes := []func(n int) string{
		func(n int) string { return fmt.Sprintf("^cache-bound-%d(?=x)x$", n) },
		func(n int) string { return fmt.Sprintf("^cache-bound-%d[a-z]{1,4}$", n) },
		func(n int) string { return fmt.Sprintf("^cache-bound-%d(?:a|b)*c$", n) },
		func(n int) string { return fmt.Sprintf("^(cache-bound-%d|none|auto)$", n) },
	}
	texts := func(n int) [4]string {
		return [4]string{fmt.Sprintf("cache-bound-%dx", n), fmt.Sprintf("cache-bound-%dabc", n), fmt.Sprintf("cache-bound-%dababc", n), fmt.Sprintf("cache-bound-%d", n)}
	}
	held := []*pattern{}
	for n := 0; n < 3*maxCachedPatterns; n++ {
		p, err := compilePattern(shapes[n%4](n))
		if err != nil {
			t.Fatal(err)
		}
		held = append(held, p)
	}
	checkCache(t)
	if patterns, _, _, _ := patternCache.figures(); patterns != maxCachedPatterns {
		t.Errorf("the cache holds %d patterns after %d were compiled, want %d", patterns, len(held), maxCachedPatterns)
	}
	if patternCache.get("^cache-bound-0+$") != nil || patternCache.get(held[0].source) != nil {
		t.Error("the patterns compiled first are still in the cache")
	}
	if last := held[len(held)-1]; patternCache.get(last.source) != last {
		t.Error("the pattern compiled last is not in the cache")
	}
	// Every pattern still answers, in the cache or not.
	if !first.matchString("cache-bound-000", &budget) || first.matchString("cache-bound-1", &budget) {
		t.Error("the first pattern answers wrongly after it left the cache")
	}
	for n, p := range held {
		if yes, no := texts(n)[n%4], texts(n + 1)[n%4]; !p.matchString(yes, &budget) || p.matchString(no, &budget) {
			t.Fatalf("%s answers wrongly on %q or %q after the cache was filled", p.source, yes, no)
		}
	}
	// The same source compiles again, to a matcher of its own, which answers the same.
	again, err := compilePattern("^cache-bound-0+$")
	if err != nil || again == first || !again.matchString("cache-bound-0", &budget) || again.matchString("cache-bound-", &budget) {
		t.Errorf("the first pattern compiled again is %p (it was %p), with the error %v, or answers wrongly", again, first, err)
	}
	if same, _ := compilePattern("^cache-bound-0+$"); same != again {
		t.Error("a pattern in the cache was compiled again")
	}
}

// TestTheCacheOfPatternsIsBoundedInSize compiles large patterns, which leave the cache by what they are counted as
// well before their number is the bound, and a pattern too large to be kept at all. The heap the process holds for
// them all stays under the bound of the cache. Each of the 600 patterns holds 280 KB or so, which came to 170 MB
// when the cache kept them all.
func TestTheCacheOfPatternsIsBoundedInSize(t *testing.T) {
	class := strings.Repeat(`[ĀĂĄĆ]`, 1500)
	sources := []string{}
	for n := 0; n < 600; n++ {
		sources = append(sources, fmt.Sprintf("^(?=large-%d)%s$", n, class))
	}
	budget := patternBudget{}
	before := heapNow()
	for n, source := range sources {
		p, err := compilePattern(source)
		if err != nil {
			t.Fatal(err)
		}
		if n == 0 && p.retained <= 256<<10 {
			t.Errorf("a large pattern is counted as %d bytes", p.retained)
		}
		if p.matchString("large", &budget) {
			t.Fatalf("pattern %d matches", n)
		}
	}
	checkCache(t)
	held := heapNow() - before
	patterns, bytes, _, _ := patternCache.figures()
	t.Logf("600 large patterns compiled: the heap grew by %d MB, and the cache holds %d patterns counted as %d MB", held>>20, patterns, bytes>>20)
	if held > maxCachedBytes {
		t.Errorf("the heap grew by %d MB, which is over the bound of the cache, %d MB", held>>20, maxCachedBytes>>20)
	}
	if patterns >= maxCachedPatterns/2 || patterns < 16 {
		t.Errorf("the cache holds %d large patterns, want them bounded by size and not by number", patterns)
	}
	// A pattern too large for the cache is compiled for whoever asked and not kept.
	huge, err := compilePattern("^(?=huge)" + strings.Repeat("a", 15000) + "$")
	if err != nil {
		t.Fatal(err)
	}
	if huge.retained <= maxCachedPatternBytes || patternCache.get(huge.source) != nil {
		t.Errorf("a pattern counted as %d bytes (the most a kept pattern has is %d) is in the cache", huge.retained, maxCachedPatternBytes)
	}
	if !huge.matchString("huge"+strings.Repeat("a", 14996), &budget) && huge.matchString("huge", &budget) {
		t.Error("the pattern that was not kept answers wrongly")
	}
	checkCache(t)
}

// TestAProcessThatCompilesManySchemasDoesNotGrow compiles validators for schemas with patterns of their own and
// lets go of each, as a service that validates against the schemas of its callers does. The heap ends where it
// would with the cache full. A validator made before still answers, and so does one made after for the same schema.
func TestAProcessThatCompilesManySchemasDoesNotGrow(t *testing.T) {
	schema := func(n int) string {
		return fmt.Sprintf(`{"type": "object", "properties": {"id": {"type": "string", "pattern": "^(?=id)id-%d-[0-9]{1,%d}(?:%s)?$"}},
			"patternProperties": {"^x-%d-(?=[a-z])[a-z]+$": {"type": "integer"}}}`, n, 1+n%7, strings.Repeat("[a-f][0-9]", 400), n)
	}
	check := func(v *Validator, n int) {
		t.Helper()
		for instance, want := range map[string]bool{
			fmt.Sprintf(`{"id": "id-%d-7", "x-%d-abc": 1}`, n, n):   true,
			fmt.Sprintf(`{"id": "id-%d-7", "x-%d-abc": "1"}`, n, n): false,
			fmt.Sprintf(`{"id": "id-%d-", "x-%d-abc": 1}`, n, n):    false,
			fmt.Sprintf(`{"id": "id-%d-7", "x-%d-abc": 1}`, n+1, n): false,
		} {
			if got, err := v.ValidateString(instance); err != nil || got != want {
				t.Fatalf("schema %d on %s: %v, %v, want %v", n, instance, got, err, want)
			}
		}
	}
	early, err := CompileString(schema(0))
	if err != nil {
		t.Fatal(err)
	}
	const rounds = 6000
	var after1024 int64
	before := heapNow()
	for n := 1; n <= rounds; n++ {
		v, err := CompileString(schema(n))
		if err != nil {
			t.Fatal(err)
		}
		if n%500 == 0 {
			check(v, n)
		}
		if n == maxCachedPatterns {
			after1024 = heapNow() - before
		}
	}
	end := heapNow() - before
	checkCache(t)
	t.Logf("the heap grew by %d KB over the first %d schemas and by %d KB over all %d", after1024>>10, maxCachedPatterns, end>>10, rounds)
	// Six times the schemas must not hold six times the memory. With no bound the heap grew in proportion.
	if end > 2*after1024+(4<<20) {
		t.Errorf("the heap grew by %d KB over %d schemas, and by %d KB over the first %d", end>>10, rounds, after1024>>10, maxCachedPatterns)
	}
	check(early, 0)
	again, err := CompileString(schema(0))
	if err != nil {
		t.Fatal(err)
	}
	check(again, 0)
}

// TestTheCacheOfPatternsIsSafeForConcurrentUse compiles overlapping sets of patterns from many goroutines, more
// than the cache holds, and matches with each. The race detector watches it.
func TestTheCacheOfPatternsIsSafeForConcurrentUse(t *testing.T) {
	var wg sync.WaitGroup
	failures := make(chan string, 64)
	for g := 0; g < 8; g++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			budget := patternBudget{}
			for n := 0; n < 2*maxCachedPatterns; n++ {
				k := (n*7 + g*131) % (3 * maxCachedPatterns)
				p, err := compilePattern(fmt.Sprintf("^shared-%d(?:-[a-z]+)*$", k))
				if err != nil {
					failures <- err.Error()
					return
				}
				if !p.matchString(fmt.Sprintf("shared-%d-a-b", k), &budget) || p.matchString(fmt.Sprintf("shared-%d-", k), &budget) {
					failures <- p.source + " answers wrongly"
					return
				}
			}
		}()
	}
	wg.Wait()
	close(failures)
	for failure := range failures {
		t.Error(failure)
	}
	checkCache(t)
}

// TestAPatternIsCountedAsNoLessThanItHolds compares what the cache counts for a pattern with the heap the pattern
// holds, for the matchers that are not the engine's. (The engine's own test has its patterns.)
func TestAPatternIsCountedAsNoLessThanItHolds(t *testing.T) {
	literal := strings.Repeat("x", 15000)
	var set strings.Builder
	for i := 0; i < 40; i++ {
		fmt.Fprintf(&set, "word%d|", i)
	}
	var large strings.Builder
	for i := 0; i < 2500; i++ {
		fmt.Fprintf(&large, "%d|", i)
	}
	for name, source := range map[string]func(n int) string{
		"literal":              func(n int) string { return fmt.Sprintf("^lit-%d$", n) },
		"sequence":             func(n int) string { return fmt.Sprintf("^%d[a-z][a-z0-9_]{0,29}$", n) },
		"long sequence":        func(n int) string { return fmt.Sprintf("^%d%s$", n, strings.Repeat("[a-z]", 5000)) },
		"list":                 func(n int) string { return fmt.Sprintf(`^([a-z]+)(\.%d[a-z]{1,9})*$`, n) },
		"long list":            func(n int) string { return fmt.Sprintf(`^(?:%d%s,)*x$`, n, strings.Repeat("[a-z]", 5000)) },
		"set of literals":      func(n int) string { return fmt.Sprintf("^(%sz%d)$", set.String(), n) },
		"one text many times":  func(n int) string { return fmt.Sprintf("^(%sc%d)$", strings.Repeat("ab|", 5000), n) },
		"large set":            func(n int) string { return fmt.Sprintf("^(%sz%d)$", large.String(), n) },
		"alternatives":         func(n int) string { return fmt.Sprintf("^([a|A]uto%d)|([n|N]one)$", n) },
		"groups, long literal": func(n int) string { return fmt.Sprintf("^(a|b)(c|d)(e|f)(g|h)(i|j)(k|l)%d%s$", n, literal) },
		"line":                 func(n int) string { return fmt.Sprintf("^.{1,%d}$", n+1) },
	} {
		const count = 32
		sources := []string{}
		for n := 0; n < count; n++ {
			sources = append(sources, source(n))
		}
		held := []*pattern{}
		before := heapNow()
		for _, s := range sources {
			p, err := compilePattern(s)
			if err != nil {
				t.Fatalf("%s: %v", name, err)
			}
			held = append(held, p)
		}
		patternCache.clear()
		bytes := (heapNow() - before) / count
		t.Logf("%-22s matcher %d, %d bytes a pattern, counted as %d", name, held[0].kind, bytes, held[0].retained)
		if bytes > int64(held[0].retained) {
			t.Errorf("%s: a pattern holds %d bytes and is counted as %d", name, bytes, held[0].retained)
		}
		runtime.KeepAlive(held)
	}
}
