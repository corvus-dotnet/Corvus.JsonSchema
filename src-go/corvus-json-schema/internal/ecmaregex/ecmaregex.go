// Package ecmaregex matches ECMA-262 regular expressions against UTF-8 text, with the semantics JSON Schema requires
// of the pattern and patternProperties keywords and of the regex format.
//
// # Unicode mode
//
// A pattern is read first with the grammar of the ECMA-262 u flag, which is what JSON Schema specifies. A pattern
// that is not valid under that grammar is then read with the Annex B grammar of a pattern with no flag, which accepts
// what many schemas in the wild contain (identity escapes such as \&, a lone brace, legacy octal escapes). Only a
// pattern that neither grammar accepts is an error. This is the choice the Rust and Java ports make. Valid, which
// backs the regex format, accepts only the u flag grammar, as those ports do.
//
// Whichever grammar accepts the pattern, matching is by Unicode code point over the UTF-8 text. The dot, a class and a
// quantifier each see a character beyond the Basic Multilingual Plane as one character, and a surrogate pair written
// as two \u escapes names one code point. The Java port does the same. The Rust port matches UTF-16 code units for a
// pattern that only the Annex B grammar accepts, so the two can differ on such a pattern when the text has a
// character beyond the Basic Multilingual Plane. Malformed UTF-8 in the text is read one byte at a time as U+FFFD.
//
// JSON Schema gives a pattern no flags, so matching is case sensitive, ^ and $ match only at the ends of the text, and
// the dot excludes the four line terminators. A pattern can change that for part of itself with a modifier group of
// ECMAScript 2025, such as (?i:...), (?m:...) or (?s:...).
//
// # Back ends
//
// A pattern with no lookaround, no backreference, no multiline anchor and no case-insensitive word boundary is
// translated to the syntax of the standard library's regexp package and matched in linear time. Such a pattern also
// gets a deterministic automaton over ASCII text when it is small enough and has no word boundary (see dfa.go),
// which decides an ASCII text with one table read per byte. The regexp package then only sees the texts that are
// not ASCII. The translation spells out every literal and every class as code points, so
// none of the places where RE2 and ECMA-262 read the same syntax differently (\d, \w, \s, the dot, $, Unicode
// classes, case folding) can show through. Every other pattern, and any pattern whose translation regexp refuses (a repeat count
// above its limit, for example), runs on a backtracking matcher in this package. That matcher keeps its choices on an
// explicit pooled stack, so a long text cannot overflow the goroutine stack and a match does not allocate once the
// stack has grown. It has no step budget, so a pattern with nested quantifiers and a lookaround or a backreference
// can take exponential time, as it can in any backtracking engine.
//
// # Coverage and limits
//
// The grammar is that of ECMAScript 2025. It includes lookbehind, named groups, group names shared between separate
// alternatives, modifier groups, and Unicode property escapes for every property, value and alias ECMA-262 lists, with
// Unicode 17 data. The properties and the case folding come from the tables of the ucd package and never from the
// unicode package of the Go toolchain, and the regexp package is given explicit ranges, so a pattern matches the same
// texts whichever toolchain built the program. The v flag (set notation and properties of strings) does not apply, because a JSON Schema pattern
// has no flags. Groups may nest 1000 deep.
package ecmaregex

import (
	"regexp"
	"strings"
	"sync"
	"unsafe"
)

// A Regexp is a compiled ECMA-262 pattern. It is safe for concurrent use.
type Regexp struct {
	pattern string
	// Exactly one of re and prog is set.
	re   *regexp.Regexp
	prog *program
	// unicode says the pattern was read with the u flag grammar.
	unicode bool
	// For a pattern on the regexp package: its automaton over ASCII text (see dfa.go), built on the first match
	// from the syntax tree, which is kept until then. Nil when the pattern has none.
	once sync.Once
	root *node
	dfa  *dfa
}

// An Engine names the back end a pattern compiled to.
type Engine uint8

const (
	// EngineRE2 is the standard library's regexp package.
	EngineRE2 Engine = iota
	// EngineBacktrack is the backtracking matcher of this package.
	EngineBacktrack
)

// String returns the name of the engine.
func (e Engine) String() string {
	if e == EngineRE2 {
		return "re2"
	}
	return "backtrack"
}

// Compile compiles an ECMA-262 pattern with the semantics JSON Schema requires. It returns an error for a pattern
// that is not valid.
func Compile(pattern string) (*Regexp, error) {
	return compile(pattern, false)
}

// compile is Compile with a way to force the backtracking matcher, which the tests use to compare the back ends.
func compile(pattern string, forceBacktrack bool) (*Regexp, error) {
	unicode := true
	root, ps, err := parse(pattern, true)
	if err != nil {
		var legacyErr error
		unicode = false
		if root, ps, legacyErr = parse(pattern, false); legacyErr != nil {
			return nil, err
		}
	}
	r := &Regexp{pattern: pattern, unicode: unicode}
	if !forceBacktrack && !ps.needsBacktracking {
		var b strings.Builder
		writeRE2(&b, root)
		if re, err := regexp.Compile(b.String()); err == nil {
			r.re, r.root = re, root
			return r, nil
		}
	}
	r.prog = compileProgram(root, unicode)
	return r, nil
}

// Match reports whether the pattern matches anywhere in the UTF-8 text (an unanchored search). It allocates nothing
// in the steady state and is safe for concurrent use.
func (r *Regexp) Match(utf8 []byte) bool {
	if r.re != nil {
		r.once.Do(r.buildDFA)
		if r.dfa != nil {
			if matched, ok := r.dfa.match(utf8); ok {
				return matched
			}
		}
		return r.re.Match(utf8)
	}
	return r.prog.match(utf8)
}

// buildDFA builds the automaton of a pattern on the regexp package, and lets go of the syntax tree.
func (r *Regexp) buildDFA() {
	r.dfa, r.root = buildDFA(r.root), nil
}

// MatchString is Match for a string.
func (r *Regexp) MatchString(s string) bool {
	// The matchers only read the text, so they can look at the string's bytes in place.
	return r.Match(unsafe.Slice(unsafe.StringData(s), len(s)))
}

// Engine reports which back end the pattern compiled to.
func (r *Regexp) Engine() Engine {
	if r.re != nil {
		return EngineRE2
	}
	return EngineBacktrack
}

// Unicode reports whether the pattern is valid with the ECMA-262 u flag. When it is not, the pattern was read with
// the Annex B grammar.
func (r *Regexp) Unicode() bool { return r.unicode }

// String returns the source of the pattern.
func (r *Regexp) String() string { return r.pattern }

// Valid reports whether the pattern is a valid ECMA-262 pattern (for format: regex). It applies the grammar of the u
// flag.
func Valid(pattern string) bool {
	_, _, err := parse(pattern, true)
	return err == nil
}
