package jsonschema

import (
	"errors"
	"time"

	"github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ecmaregex"
)

// The regular expression engine behind every pattern that has no faster matcher. This file is the only one that
// names it.

// patternEngine matches a pattern anywhere in UTF-8 text.
type patternEngine = *ecmaregex.Regexp

// compileEngine compiles an ECMA-262 pattern (with the u flag, or failing that without it, as many schemas need).
func compileEngine(source string) (patternEngine, error) {
	return ecmaregex.Compile(source)
}

// checkRegex reads a pattern without compiling it. It reports whether the pattern is valid with the u flag, and
// returns nil for a pattern that is valid with or without it, a *patternSizeError for one over a limit of size, and
// errInvalidPattern for one that is not valid.
func checkRegex(source string) (unicode bool, err error) {
	unicode, err = ecmaregex.Check(source)
	if err == nil {
		return unicode, nil
	}
	var large *ecmaregex.TooLargeError
	if errors.As(err, &large) {
		return false, &patternSizeError{limit: large.Limit, value: large.Value, reason: large.Error()}
	}
	return false, errInvalidPattern
}

// errInvalidPattern says a pattern is not a valid ECMA-262 regular expression.
var errInvalidPattern = errors.New("invalid regular expression")

// A patternSizeError says a pattern is over a limit of size, which one, and its value.
type patternSizeError struct {
	limit  string
	value  int
	reason string
}

func (e *patternSizeError) Error() string { return e.reason }

// patternError returns the error of a schema whose keyword holds a pattern that could not be compiled.
func patternError(err error, source, keyword string) error {
	var large *patternSizeError
	if errors.As(err, &large) {
		return &PatternTooLargeError{Pattern: source, Keyword: keyword, Limit: large.limit, Value: large.value, reason: large.reason}
	}
	return compileError("Invalid regular expression '" + source + "' in " + keyword + ".")
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
