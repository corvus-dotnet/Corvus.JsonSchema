package jsonschema

import (
	"time"

	"github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ecmaregex"
)

// The regular expression engine behind every pattern that has no faster matcher. This file is the only one that
// names it.

// patternEngine matches a pattern anywhere in UTF-8 text.
type patternEngine = *ecmaregex.Regexp

// compileEngine compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need).
// Not ok when the pattern is invalid.
func compileEngine(source string) (patternEngine, bool) {
	re, err := ecmaregex.Compile(source)
	return re, err == nil
}

// A patternBudget is what one evaluation, or the compilation of one schema, gives every match of a pattern: the
// time each may take. It notes the first match that reached a limit, after which every match is abandoned at once.
type patternBudget struct {
	// limit is the time one match may take, or zero for no limit.
	limit time.Duration
	// err is ecmaregex.ErrMatchTime or ecmaregex.ErrMatchMemory once a match has reached a limit, and source is the
	// pattern of that match.
	err    error
	source string
}

// engineMatch matches the text on the engine within the budget. When a limit is reached the answer is false, and
// it is not an answer: the caller of the evaluation gets the error (see failure).
func (b *patternBudget) engineMatch(engine patternEngine, source string, s []byte) bool {
	if b.err != nil {
		return false
	}
	matched, err := engine.MatchLimit(s, b.limit)
	if err != nil {
		b.err, b.source = err, source
		return false
	}
	return matched
}

// failure returns the error of a match that reached a limit, or nil.
func (b *patternBudget) failure() error {
	if b.err == nil {
		return nil
	}
	return &PatternMatchLimitError{Pattern: b.source, Limit: b.limit, Memory: b.err == ecmaregex.ErrMatchMemory}
}

// validRegex reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format,
// and the patterns the engine reads without falling back).
func validRegex(source string) bool {
	return ecmaregex.Valid(source)
}
