package jsonschema

import "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ecmaregex"

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

// validRegex reports whether a string is a valid ECMA-262 regular expression with the u flag (the regex format,
// and the patterns the engine reads without falling back).
func validRegex(source string) bool {
	return ecmaregex.Valid(source)
}
