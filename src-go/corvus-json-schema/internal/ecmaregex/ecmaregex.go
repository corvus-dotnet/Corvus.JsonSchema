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
// A pattern the u flag grammar accepts is matched by Unicode code point over the UTF-8 text. The dot, a class and a
// quantifier each see a character beyond the Basic Multilingual Plane as one character, and a surrogate pair written
// as two \u escapes names one code point.
//
// A pattern that only the Annex B grammar accepts is matched by UTF-16 code unit, as ECMA-262 matches a pattern
// with no u flag. A character beyond the Basic Multilingual Plane is then two characters, a high surrogate and a
// low one, to the dot, a class, a quantifier, a group and a backreference. A match can start or end between the
// two. Such a character in the pattern is two atoms, and a class range between two of them is an error. Case is
// folded by the Canonicalize of a pattern with no u flag. The text stays UTF-8 and no UTF-16 copy of it is made
// (see units.go). Only a text with a character beyond the Basic Multilingual Plane is read differently by the two.
//
// Malformed UTF-8 in the text is read one byte at a time as U+FFFD. The text is UTF-8, so it cannot hold half of a
// surrogate pair, and a pattern that names one, such as \ud800, matches it only as half of a character and only
// when the pattern has no u flag.
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
// above its limit, for example), runs on a backtracking matcher in this package. So does a pattern with no u flag
// on a text with a character beyond the Basic Multilingual Plane, since the regexp package matches code points.
// That matcher keeps its choices on an
// explicit pooled stack, so a long text cannot overflow the goroutine stack and a match does not allocate once the
// stack has grown. It has no step budget, so a pattern with nested quantifiers and a lookaround or a backreference
// can take exponential time, as it can in any backtracking engine. A repetition whose body cannot consume a
// character is not such a pattern. It is run once, or not at all (see simplify), as V8 runs it. And a counted loop
// gives up at a position from which the rest of the pattern needs more text than is left (see setNeeds), as V8
// does, so a loop whose iterations can be empty does not try every combination of them there.
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
	"errors"
	"regexp"
	"regexp/syntax"
	"strings"
	"sync"
	"time"
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
	// For a pattern on the regexp package: the program of the backtracking matcher, built on the first text the
	// regexp package is not given. That is a text with a character beyond the Basic Multilingual Plane when the
	// pattern was read without the u flag, because the regexp package matches by code point and such a pattern
	// matches by UTF-16 code unit. And it is a text too long for the regexp package to answer within the limit of
	// the match (see reCost), because a match there cannot be stopped.
	backOnce sync.Once
	backProg *program
	// reCost is the number of instructions of the program the regexp package runs.
	reCost int
}

const (
	// maxRE2Cost is the largest program the regexp package is asked to compile, in the instructions estimated for
	// it before it is written (see re2Cost). The package writes a counted repetition out, so a{1000} is a thousand
	// instructions, and it takes time proportional to its program to compile and, for every character of a text,
	// to match.
	maxRE2Cost = 10000
	// re2StepTime is the time the regexp package can take for one instruction of its program and one byte of the
	// text, when every instruction has a thread on it. It was measured at 5 to 13 ns (the higher figure for a
	// class of many ranges) on a machine under load, and is rounded up. A match on the regexp package cannot be
	// stopped, so a match with a limit goes there only when the size of the program times the length of the text
	// times this is within the limit. Any other goes to the backtracking matcher, which reads the clock.
	re2StepTime = 16 * time.Nanosecond
)

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
// that is not valid, and a *TooLargeError for a pattern over a limit of size (see MaxSize), whether or not it is
// valid.
func Compile(pattern string) (*Regexp, error) {
	return compile(pattern, false)
}

// compile is Compile with a way to force the backtracking matcher, which the tests use to compare the back ends.
func compile(pattern string, forceBacktrack bool) (*Regexp, error) {
	unicode := true
	root, ps, err := parse(pattern, true)
	if err != nil {
		if tooLarge(err) {
			return nil, err
		}
		var legacyErr error
		unicode = false
		if root, ps, legacyErr = parse(pattern, false); legacyErr != nil {
			if tooLarge(legacyErr) {
				return nil, legacyErr
			}
			return nil, err
		}
	}
	r := &Regexp{pattern: pattern, unicode: unicode}
	if !forceBacktrack && !ps.needsBacktracking {
		if re2Cost(root) <= maxRE2Cost {
			var b strings.Builder
			writeRE2(&b, root)
			if re, cost := compileRE2(b.String()); re != nil {
				r.re, r.root, r.reCost = re, root, cost
				return r, nil
			}
		}
	}
	r.prog = compileProgram(root, unicode)
	return r, nil
}

// compileRE2 compiles a translation with the regexp package. It also returns the number of instructions of the
// program the package runs, which the package does not tell, so the translation is compiled a second time by the
// syntax package to count them. It returns nil when the package refuses the translation.
func compileRE2(translation string) (*regexp.Regexp, int) {
	re, err := regexp.Compile(translation)
	if err != nil {
		return nil, 0
	}
	tree, err := syntax.Parse(translation, syntax.Perl)
	if err != nil {
		return nil, 0
	}
	prog, err := syntax.Compile(tree.Simplify())
	if err != nil {
		return nil, 0
	}
	return re, len(prog.Inst)
}

func tooLarge(err error) bool {
	var large *TooLargeError
	return errors.As(err, &large)
}

// Match reports whether the pattern matches anywhere in the UTF-8 text (an unanchored search), with no limit on
// the time or the memory the match takes. It allocates nothing in the steady state and is safe for concurrent use.
func (r *Regexp) Match(utf8 []byte) bool {
	matched, _ := r.MatchLimit(utf8, 0)
	return matched
}

// MatchLimit is Match with a limit on the time the match may take, when limit is above zero. A match that reaches
// the limit is abandoned and the error is ErrMatchTime. So is a match whose backtracking stack reaches maxStack
// entries, and the error is ErrMatchMemory. The result is then not an answer. The clock is read every checkEvery
// steps of the backtracking matcher, so a match can go past the limit by the time those take, which is some tens of
// microseconds. The automaton and the regexp package take time proportional to the text and are not stopped: the
// regexp package is given a text only when it can answer within the limit (see re2StepTime).
func (r *Regexp) MatchLimit(utf8 []byte, limit time.Duration) (bool, error) {
	if r.re != nil {
		r.once.Do(r.buildDFA)
		if r.dfa != nil {
			if matched, ok := r.dfa.match(utf8); ok {
				return matched, nil
			}
		}
		if (r.unicode || !hasSupplementary(utf8)) && (limit <= 0 || time.Duration(r.reCost)*time.Duration(len(utf8)) <= limit/re2StepTime) {
			return r.re.Match(utf8), nil
		}
		r.backOnce.Do(r.buildBacktracking)
		return r.backProg.match(utf8, limit)
	}
	return r.prog.match(utf8, limit)
}

// buildBacktracking compiles the program of the backtracking matcher for a pattern on the regexp package. The
// syntax tree was let go of when the pattern was compiled, so the pattern is read again, which cannot fail.
func (r *Regexp) buildBacktracking() {
	root, _, _ := parse(r.pattern, r.unicode)
	r.backProg = compileProgram(root, r.unicode)
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
// flag. A pattern over a limit of size is not read to its end and is reported as not valid.
func Valid(pattern string) bool {
	_, _, err := parse(pattern, true)
	return err == nil
}

// Check reads the pattern without compiling it. It reports whether the pattern is valid with the u flag grammar,
// and returns the error Compile would: nil for a pattern either grammar accepts, a *TooLargeError for a pattern over
// a limit of size, and any other error for a pattern that is not valid.
func Check(pattern string) (unicode bool, err error) {
	if _, _, err = parse(pattern, true); err == nil {
		return true, nil
	}
	if tooLarge(err) {
		return false, err
	}
	if _, _, legacyErr := parse(pattern, false); legacyErr != nil {
		if tooLarge(legacyErr) {
			return false, legacyErr
		}
		return false, err
	}
	return false, nil
}
