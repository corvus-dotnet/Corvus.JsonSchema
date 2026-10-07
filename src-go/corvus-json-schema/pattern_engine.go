package jsonschema

import "regexp"

// The regular expression engine behind every pattern that has no faster matcher. This file is the only one that
// names it.
//
// TEMPORARY: until internal/ecmaregex is ready, this is a shim over the standard regexp package, which has no
// lookaround or backreferences and differs from ECMA-262 in details. A pattern it cannot compile is taken as valid
// and never matches.

// patternEngine matches a pattern anywhere in UTF-8 text.
type patternEngine interface {
	Match(utf8 []byte) bool
}

type unsupportedPattern struct{}

func (unsupportedPattern) Match([]byte) bool { return false }

// compileEngine compiles an ECMA-262 pattern. Not ok when the pattern is invalid.
func compileEngine(source string) (patternEngine, bool) {
	re, err := regexp.Compile(source)
	if err != nil {
		return unsupportedPattern{}, true
	}
	return re, true
}

// validRegex reports whether a string is a valid ECMA-262 regular expression (the regex format).
func validRegex(source string) bool {
	_, err := regexp.Compile(source)
	return err == nil
}
