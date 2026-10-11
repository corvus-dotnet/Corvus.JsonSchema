package jsonschema

import (
	"errors"
	"strings"
	"testing"
	"time"
)

// The limits on the size of one pattern. A schema may come from outside, so no pattern may take long or much memory
// to read and to compile. A pattern over a limit is refused when the schema is compiled, with an error that is not
// the error of a pattern that is not valid.

// TestAPatternOverALimitOfSizeIsRefused checks each limit in both keywords: the error is a *PatternTooLargeError
// that names the keyword, the limit and its value, and is neither a *CompileError nor the error of a match.
func TestAPatternOverALimitOfSizeIsRefused(t *testing.T) {
	nest := func(depth int) string { return strings.Repeat("(?:", depth) + "a" + strings.Repeat(")", depth) }
	for _, c := range []struct {
		name, at, over, limit string
		value                 int
		message               string
	}{
		{"length", `\\u{` + strings.Repeat("0", 1<<20-6) + `41}`, `\\u{` + strings.Repeat("0", 1<<20-5) + `41}`, "length", 1 << 20, "the pattern is longer than 1048576 bytes"},
		{"nesting", nest(999), nest(1000), "nesting", 1000, "the groups of the pattern nest more than 1000 deep"},
		{"size", strings.Repeat("a", 16384), strings.Repeat("a", 16385), "size", 1 << 20, "the pattern has a size of more than 1048576 bytes when it is read"},
		{"size of a class", "[" + strings.Repeat(`\\u0100\\u0102`, 60000) + "]", "[" + strings.Repeat(`\\u0100\\u0102`, 70000) + "]", "size", 1 << 20, "the pattern has a size of more than 1048576 bytes when it is read"},
		{"nesting, and not valid", nest(999), nest(1000) + "*+)", "nesting", 1000, "the groups of the pattern nest more than 1000 deep"},
	} {
		for _, keyword := range []string{"pattern", "patternProperties"} {
			schema := func(pattern string) string {
				if keyword == "pattern" {
					return `{"pattern": "` + pattern + `"}`
				}
				return `{"patternProperties": {"` + pattern + `": true}}`
			}
			start := time.Now()
			if _, err := CompileString(schema(c.at)); err != nil {
				t.Errorf("%s in %s: the pattern at the limit: %v", c.name, keyword, err)
			}
			_, err := CompileString(schema(c.over))
			if took := time.Since(start); took > generousTime {
				t.Errorf("%s in %s: compiling took %v", c.name, keyword, took)
			}
			var large *PatternTooLargeError
			var compile *CompileError
			switch {
			case !errors.Is(err, ErrPatternTooLarge) || !errors.As(err, &large):
				t.Errorf("%s in %s: the error is %.200v, want a *PatternTooLargeError", c.name, keyword, err)
				continue
			case large.Keyword != keyword || large.Limit != c.limit || large.Value != c.value:
				t.Errorf("%s in %s: the error holds the keyword %q, the limit %q and the value %d", c.name, keyword, large.Keyword, large.Limit, large.Value)
			case errors.As(err, &compile) || errors.Is(err, ErrPatternMatchLimit):
				t.Errorf("%s in %s: the error is also another error", c.name, keyword)
			}
			want := "' (" + itoa(len(large.Pattern)) + " bytes) in " + keyword + " is too large to read as an ECMA-262 regular expression: " + c.message + "."
			if message := err.Error(); !strings.HasPrefix(message, "The regular expression '") || !strings.HasSuffix(message, want) || len(message) > 400 {
				t.Errorf("%s in %s: the message is %.300q", c.name, keyword, message)
			}
			if strings.Contains(strings.ToLower(err.Error()), "invalid") {
				t.Errorf("%s in %s: the message says the pattern is not valid: %.300q", c.name, keyword, err)
			}
		}
	}
}

func itoa(n int) string {
	if n == 0 {
		return "0"
	}
	var digits []byte
	for ; n > 0; n /= 10 {
		digits = append([]byte{byte('0' + n%10)}, digits...)
	}
	return string(digits)
}

// TestAPatternThatIsNotValidIsNotTooLarge checks the two errors stay apart: a pattern under every limit that is not
// valid is a *CompileError.
func TestAPatternThatIsNotValidIsNotTooLarge(t *testing.T) {
	for _, pattern := range []string{"(", "a**", "[b-a]", `(?<n>a)\\k<m>`, strings.Repeat("(?:", 999) + "a" + strings.Repeat(")", 1000)} {
		_, err := CompileString(`{"pattern": "` + pattern + `"}`)
		var compile *CompileError
		if !errors.As(err, &compile) || errors.Is(err, ErrPatternTooLarge) || !strings.HasPrefix(err.Error(), "Invalid regular expression '") {
			t.Errorf("%.20q: the error is %.100v, want a *CompileError for a pattern that is not valid", pattern, err)
		}
	}
}

// TestTheRegexFormatRefusesAPatternOverALimit checks the regex format, which reads a string of the instance as a
// pattern: a string over a limit of size is not read to its end and is not a valid regular expression.
func TestTheRegexFormatRefusesAPatternOverALimit(t *testing.T) {
	v := mustCompile(t, `{"format": "regex"}`, WithAssertFormat(true))
	for text, want := range map[string]bool{
		"^[a-z]+$": true, "(": false,
		strings.Repeat("a", 16384): true, strings.Repeat("a", 16385): false,
		strings.Repeat("(?:", 999) + "a" + strings.Repeat(")", 999):   true,
		strings.Repeat("(?:", 1000) + "a" + strings.Repeat(")", 1000): false,
	} {
		start := time.Now()
		ok, err := v.ValidateString(`"` + text + `"`)
		if err != nil || ok != want {
			t.Errorf("a string of %d bytes: %v, %v, want %v", len(text), ok, err, want)
		}
		if took := time.Since(start); took > generousTime {
			t.Errorf("a string of %d bytes took %v", len(text), took)
		}
	}
}
