package jsonschema

import (
	"errors"
	"fmt"
	"strings"
	"testing"
	"time"
)

// The limit on the time and the memory of one match of a pattern or patternProperties regular expression. An
// instance may come from outside, and so may a schema, so no text and no pattern may make a validation run without
// end or hold memory without bound.
//
// No test here depends on a quiet machine. A match that must reach its limit runs a pattern that no backtracking
// matcher answers in the life of the machine, and the bounds on the time it is given are many times what it needs.

// generousTime is what a test allows for something that takes milliseconds.
const generousTime = 20 * time.Second

// neverAnswers is a pattern, and a string it never answers for: a group read back inside a repetition, on a text
// that fails at its end.
const neverAnswers = `^(?:(a+)\\1*)+$`

var neverAnswered = strings.Repeat("a", 60) + "b"

// limitSchemas holds the pattern in each keyword that matches a regular expression, with an instance that makes it
// match the text. The plans run pattern and patternProperties in several ways, so each is here in the shapes that
// take a different one.
func limitSchemas(pattern, text string) []struct{ name, schema, instance string } {
	return []struct{ name, schema, instance string }{
		{"pattern", `{"pattern": "` + pattern + `"}`, `"` + text + `"`},
		{"pattern in a property", `{"type": "object", "properties": {"s": {"type": "string", "pattern": "` + pattern + `"}}}`, `{"s": "` + text + `"}`},
		{"pattern in items", `{"items": {"pattern": "` + pattern + `"}}`, `["` + text + `"]`},
		{"pattern under not", `{"not": {"pattern": "` + pattern + `"}}`, `"` + text + `"`},
		{"pattern under anyOf", `{"anyOf": [{"pattern": "` + pattern + `"}, {"type": "string"}]}`, `"` + text + `"`},
		{"patternProperties", `{"patternProperties": {"` + pattern + `": {"type": "integer"}}}`, `{"` + text + `": 1}`},
		{"patternProperties with properties", `{"properties": {"id": true}, "patternProperties": {"` + pattern + `": true}, "additionalProperties": false}`, `{"id": 1, "` + text + `": 1}`},
		{"patternProperties and unevaluatedProperties", `{"patternProperties": {"` + pattern + `": true}, "unevaluatedProperties": false}`, `{"` + text + `": 1}`},
		{"patternProperties under a condition", `{"if": {"patternProperties": {"` + pattern + `": {"const": 1}}}, "then": {"minProperties": 1}}`, `{"` + text + `": 1}`},
		{"propertyNames", `{"propertyNames": {"pattern": "` + pattern + `"}}`, `{"` + text + `": 1}`},
	}
}

// expectMatchLimit checks an error is the error of a match that reached its limit, for the pattern and the limit.
func expectMatchLimit(t *testing.T, what string, ok bool, err error, limit time.Duration) {
	t.Helper()
	var limited *PatternMatchLimitError
	switch {
	case ok:
		t.Errorf("%s: the result is true with the error %v", what, err)
	case !errors.Is(err, ErrPatternMatchLimit) || !errors.As(err, &limited):
		t.Errorf("%s: the error is %v, want a *PatternMatchLimitError", what, err)
	case limited.Pattern != `^(?:(a+)\1*)+$` || limited.Limit != limit || limited.Memory:
		t.Errorf("%s: the error holds the pattern %q, the limit %v and memory %v", what, limited.Pattern, limited.Limit, limited.Memory)
	case !strings.Contains(err.Error(), limit.String()) || !strings.Contains(err.Error(), limited.Pattern):
		t.Errorf("%s: the message %q does not name the pattern and the limit", what, err)
	}
	if errors.Is(err, ErrDepthExceeded) {
		t.Errorf("%s: the error %v is also another error", what, err)
	}
}

// TestAMatchThatReachesItsLimitIsAnError checks every entry point, for pattern and patternProperties, failing fast
// and collecting: a match that reaches its limit ends the validation with a *PatternMatchLimitError, no sooner than
// the limit and not long after it. It is never an answer. IsValid and its variants return a bool alone, and return
// false.
func TestAMatchThatReachesItsLimitIsAnError(t *testing.T) {
	const limit = 50 * time.Millisecond
	for _, c := range limitSchemas(neverAnswers, neverAnswered) {
		v := mustCompile(t, c.schema, WithPatternMatchLimit(limit))
		doc := mustParse(t, c.instance)
		timed := func(what string, run func() (bool, error)) {
			t.Helper()
			start := time.Now()
			ok, err := run()
			took := time.Since(start)
			expectMatchLimit(t, c.name+", "+what, ok, err, limit)
			if took < limit || took > limit+generousTime {
				t.Errorf("%s, %s: abandoned after %v with a limit of %v", c.name, what, took, limit)
			}
		}
		timed("Validate", func() (bool, error) { return v.Validate(doc) })
		timed("ValidateBytes", func() (bool, error) { return v.ValidateBytes([]byte(c.instance)) })
		timed("ValidateString", func() (bool, error) { return v.ValidateString(c.instance) })
		timed("Evaluate with no collector", func() (bool, error) { return v.Evaluate(doc, nil) })
		for _, level := range []ResultsLevel{Basic, Detailed, Verbose} {
			timed(fmt.Sprintf("Evaluate at level %d", level), func() (bool, error) { return v.Evaluate(doc, NewResultsCollector(level)) })
		}
		timed("EvaluateBytes", func() (bool, error) { return v.EvaluateBytes([]byte(c.instance), NewResultsCollector(Basic)) })
		timed("EvaluateString", func() (bool, error) { return v.EvaluateString(c.instance, NewResultsCollector(Basic)) })
		if v.IsValid(doc) || v.IsValidBytes([]byte(c.instance)) || v.IsValidString(c.instance) {
			t.Errorf("%s: IsValid is true for a match that reached its limit", c.name)
		}
	}
}

// TestTheDefaultMatchLimitIsOneSecond checks the limit is on when no option names it, and is a second.
func TestTheDefaultMatchLimitIsOneSecond(t *testing.T) {
	if DefaultPatternMatchLimit != time.Second {
		t.Fatalf("DefaultPatternMatchLimit is %v", DefaultPatternMatchLimit)
	}
	v := mustCompile(t, `{"pattern": "`+neverAnswers+`"}`)
	start := time.Now()
	ok, err := v.ValidateString(`"` + neverAnswered + `"`)
	took := time.Since(start)
	expectMatchLimit(t, "the default limit", ok, err, time.Second)
	if took < time.Second || took > time.Second+generousTime {
		t.Errorf("abandoned after %v with the default limit", took)
	}
}

// TestTheMatchLimitIsForEachMatch checks the limit is on one match and not on the validation: an array of strings
// that each take a part of the limit is validated, however long the whole takes.
func TestTheMatchLimitIsForEachMatch(t *testing.T) {
	// Some hundred thousand steps for each string, which is a few milliseconds.
	item := `"` + strings.Repeat("a", 14) + `b"`
	instance := "[" + strings.TrimSuffix(strings.Repeat(item+",", 40), ",") + "]"
	v := mustCompile(t, `{"items": {"not": {"pattern": "`+neverAnswers+`"}}}`, WithPatternMatchLimit(generousTime))
	if ok, err := v.ValidateString(instance); err != nil || !ok {
		t.Errorf("40 strings that each take a part of the limit: %v, %v, want true and no error", ok, err)
	}
}

// TestTheMatchLimitCanBeTurnedOff checks that NoPatternMatchLimit, zero, or less restores a match with no limit: a
// match that takes far more steps than a small limit allows runs to its answer.
func TestTheMatchLimitCanBeTurnedOff(t *testing.T) {
	schema := `{"pattern": "` + neverAnswers + `"}`
	// Some hundred thousand steps: far beyond a limit of a microsecond, and a few milliseconds of work.
	instance := `"` + strings.Repeat("a", 14) + `b"`
	ok, err := mustCompile(t, schema, WithPatternMatchLimit(time.Microsecond)).ValidateString(instance)
	expectMatchLimit(t, "a limit of a microsecond", ok, err, time.Microsecond)
	for _, off := range []time.Duration{NoPatternMatchLimit, 0, -time.Second} {
		v := mustCompile(t, schema, WithPatternMatchLimit(off))
		if ok, err := v.ValidateString(instance); err != nil || ok {
			t.Errorf("with the limit %v: %v, %v, want false and no error", off, ok, err)
		}
		if ok, err := v.ValidateString(`"aaaa"`); err != nil || !ok {
			t.Errorf("with the limit %v, a string that matches: %v, %v, want true and no error", off, ok, err)
		}
	}
	// The last option given is the one that counts.
	v := mustCompile(t, schema, WithPatternMatchLimit(NoPatternMatchLimit), WithPatternMatchLimit(time.Microsecond))
	ok, err = v.ValidateString(instance)
	expectMatchLimit(t, "a limit given after none", ok, err, time.Microsecond)
}

// TestAMatchThatReachesItsMemoryLimitIsAnError runs a pattern that pushes on the backtracking stack without end.
// With a limit on the match the stack is capped, and the error says the memory was the limit. The time limit here
// is long, so only the memory can end the match.
func TestAMatchThatReachesItsMemoryLimitIsAnError(t *testing.T) {
	v := mustCompile(t, `{"pattern": "(?:a?){2147483647}"}`, WithPatternMatchLimit(time.Hour))
	start := time.Now()
	ok, err := v.ValidateString(`""`)
	took := time.Since(start)
	var limited *PatternMatchLimitError
	if ok || !errors.Is(err, ErrPatternMatchLimit) || !errors.As(err, &limited) || !limited.Memory {
		t.Fatalf("%v, %v, want false and a *PatternMatchLimitError for memory", ok, err)
	}
	if !strings.Contains(err.Error(), "1,048,576 backtracking entries") {
		t.Errorf("the message %q does not name the limit", err)
	}
	if took > generousTime {
		t.Errorf("abandoned after %v", took)
	}
}

// TestAMatchWhileCompilingHasTheLimit checks the matches made when a schema is compiled. The plans match each
// pattern of patternProperties against the property names the schema declares, and that match has the limit too:
// Compile returns the error.
func TestAMatchWhileCompilingHasTheLimit(t *testing.T) {
	const limit = 50 * time.Millisecond
	for _, schema := range []string{
		`{"properties": {"` + neverAnswered + `": true}, "patternProperties": {"` + neverAnswers + `": {"type": "integer"}}}`,
		`{"properties": {"` + neverAnswered + `": true}, "patternProperties": {"` + neverAnswers + `": true}, "additionalProperties": false}`,
		`{"properties": {"` + neverAnswered + `": true}, "allOf": [{"patternProperties": {"` + neverAnswers + `": true}}], "unevaluatedProperties": false}`,
	} {
		start := time.Now()
		v, err := CompileString(schema, WithPatternMatchLimit(limit))
		took := time.Since(start)
		if v != nil {
			// The schema compiled with no match made, so the match is made when an instance has the property.
			ok, err := v.ValidateString(`{"` + neverAnswered + `": 1}`)
			expectMatchLimit(t, "validating "+schema, ok, err, limit)
			continue
		}
		expectMatchLimit(t, "compiling "+schema, false, err, limit)
		if took > limit+generousTime {
			t.Errorf("compiling was abandoned after %v", took)
		}
	}
}

// TestAnOrdinaryPatternIsNotAffectedByTheLimit checks patterns of every matcher with the default limit, a small
// one and none: the answers are the same.
func TestAnOrdinaryPatternIsNotAffectedByTheLimit(t *testing.T) {
	for _, c := range []struct {
		pattern, text string
		want          bool
	}{
		{`^[a-z]+$`, "abc", true}, {`^[a-z]+$`, "abc1", false}, {`^x-`, "x-a", true},
		{`^\\p{L}+$`, "café", true}, {`^\\p{L}+$`, "café ", false},
		{`^(?=.*\\d)\\w+$`, "abc1", true}, {`^(?=.*\\d)\\w+$`, "abcd", false},
		{`^(a+)\\1$`, "aaaa", true}, {`^(a+)\\1$`, "aaa", false},
		{`(?<=\\$)\\d+`, "cost $42", true}, {`^(?:[a-z]+\\.)+[a-z]+$`, "a.b.c", true},
		{`^[abé]*c$`, strings.Repeat("ab", 20000) + "é", false},
	} {
		for _, options := range [][]Option{nil, {WithPatternMatchLimit(10 * time.Second)}, {WithPatternMatchLimit(NoPatternMatchLimit)}} {
			for _, s := range limitSchemas(c.pattern, c.text)[:1] {
				v := mustCompile(t, s.schema, options...)
				if ok, err := v.ValidateString(s.instance); err != nil || ok != c.want {
					t.Errorf("%s on %.20q: %v, %v, want %v", c.pattern, c.text, ok, err, c.want)
				}
				if ok, err := v.EvaluateString(s.instance, NewResultsCollector(Basic)); err != nil || ok != c.want {
					t.Errorf("%s on %.20q, collecting: %v, %v, want %v", c.pattern, c.text, ok, err, c.want)
				}
			}
		}
	}
}

// TestValidationWithAMatchLimitAllocatesNothing checks the steady state for patterns that run on the backtracking
// matcher, with the default limit, a limit of their own and none, on strings long enough for the matcher to read
// the clock. The matcher's state is pooled with the limit in it, and the clock is read into that state.
func TestValidationWithAMatchLimitAllocatesNothing(t *testing.T) {
	if raceEnabled {
		t.Skip("the race detector makes sync.Pool drop values")
	}
	long := strings.Repeat("ab", 2000)
	schema := `{
		"type": "object",
		"properties": {
			"look": { "pattern": "^(?=.)(?:a|b)*c?$" },
			"back": { "pattern": "^(?:(a|b)\\1?)+$" },
			"wide": { "pattern": "^[abé]{0,60000}c?$" }
		},
		"patternProperties": { "^(?=x)(?:x|-)+$": { "type": "integer" } }
	}`
	instances := []string{
		`{"look": "` + long + `", "back": "` + long + `", "wide": "é` + long + `", "x-x--x": 1}`,
		`{"look": "` + long + `d", "back": "` + long + `c", "wide": "é` + long + `d", "x-x--x": "no"}`,
	}
	for _, options := range [][]Option{nil, {WithPatternMatchLimit(time.Minute)}, {WithPatternMatchLimit(NoPatternMatchLimit)}} {
		v := mustCompile(t, schema, options...)
		texts := make([][]byte, len(instances))
		for i, instance := range instances {
			texts[i] = []byte(instance)
			for k := 0; k < 3; k++ {
				if _, err := v.ValidateBytes(texts[i]); err != nil {
					t.Fatal(err)
				}
			}
		}
		if ok, _ := v.ValidateBytes(texts[0]); !ok {
			t.Errorf("the first instance is not valid")
		}
		if ok, _ := v.ValidateBytes(texts[1]); ok {
			t.Errorf("the second instance is valid")
		}
		sink := false
		allocations := testing.AllocsPerRun(50, func() {
			for _, text := range texts {
				ok, _ := v.ValidateBytes(text)
				sink = ok != sink
				sink = v.IsValidBytes(text) != sink
			}
		})
		if allocations != 0 {
			t.Errorf("with the options %d: %v allocations for each pass", len(options), allocations)
		}
	}
}
