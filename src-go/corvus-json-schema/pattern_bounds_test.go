package jsonschema

import (
	"strings"
	"testing"
	"time"
)

// The matchers that are not the regular expression engine have no limit, so each must read a text a bounded number
// of times. Two did not: an unanchored class sequence or literal among alternatives, tried at every position of the
// text, and the final sequence of a list, tried after every item against all that is left.

// TestAWideSearchIsLeftToTheEngine checks which matcher a pattern gets. A sequence or a literal that would be
// searched for at every position of the text keeps its matcher up to 64 characters, and is the engine's beyond.
func TestAWideSearchIsLeftToTheEngine(t *testing.T) {
	for _, c := range []struct {
		pattern string
		kind    matcherKind
	}{
		{`[a-z]{64}[0-9]{0}|x`, matchAlternatives}, {`[a-z]{63}[0-9]|x`, matchAlternatives}, {`[a-z]{64}[0-9]|x`, matchEngine},
		{`[a-z]{50000}[0-9]|x`, matchEngine}, {`^[a-z]{50000}[0-9]|x`, matchAlternatives}, {`[a-z]{50000}[0-9]$|^x`, matchAlternatives},
		{strings.Repeat("a", 64) + `|^x`, matchAlternatives}, {strings.Repeat("a", 65) + `|^x`, matchEngine},
		{`^` + strings.Repeat("a", 65) + `|^x`, matchAlternatives}, {strings.Repeat("a", 65) + `$|^x`, matchAlternatives},
		// The final sequence of a list must stop at the separator, and must not be matched by the length of the text.
		{`^(?:[a-z]+,)*[a-z]{2}[0-9]$`, matchSeparatedList}, {`^(?:[a-z]+,)*[a-z,]+[0-9]$`, matchEngine},
		{`^(?:[a-z]+,)*[a-z]*[a-z0-9]{2}$`, matchEngine}, {`^(?:[a-z]+,)*[0-9]{1,3}$`, matchSeparatedList},
	} {
		p, err := compilePattern(c.pattern)
		if err != nil {
			t.Fatalf("%.30q: %v", c.pattern, err)
		}
		if p.kind != c.kind {
			t.Errorf("%.30q has the matcher %d, want %d", c.pattern, p.kind, c.kind)
		}
	}
}

// TestNoMatcherReadsTheTextFromEveryPosition runs the patterns on long texts. The first took 1.8 seconds and the
// second 3.0 with the limit of a match on, which could not stop them. Each now answers, or ends at the limit of
// the engine. The list that is still a list worked out whether the rest of the text is ASCII after every item,
// which made 33,333 items take up to 1.6 seconds.
func TestNoMatcherReadsTheTextFromEveryPosition(t *testing.T) {
	items := strings.Repeat("ab,", 33333)
	for _, c := range []struct {
		pattern, text string
	}{
		{`[a-z]{50000}[0-9]|x`, strings.Repeat("a", 100000)},
		{`^(?:[a-z]+,)*[a-z,]+[0-9]$`, items},
		{`^(?:[a-z]+,)*[a-z]{2}[0-9]$`, items},
		{`^(?:[a-zé]+,)*[a-z]{2}[0-9]$`, "é," + items},
	} {
		const limit = 200 * time.Millisecond
		v := mustCompile(t, `{"pattern": "`+strings.ReplaceAll(c.pattern, `\`, `\\`)+`"}`, WithPatternMatchLimit(limit))
		start := time.Now()
		ok, err := v.ValidateString(`"` + c.text + `"`)
		took := time.Since(start)
		if ok || took > limit+generousTime {
			t.Errorf("%.30q: %v, %v after %v", c.pattern, ok, err, took)
		}
		t.Logf("%.30q on %d characters: %v, %v after %v", c.pattern, len(c.text), ok, err, took.Round(time.Millisecond))
	}
}
