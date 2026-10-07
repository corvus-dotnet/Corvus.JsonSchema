package ecmaregex

import (
	"fmt"
	"math"
)

type nodeKind uint8

const (
	nEmpty nodeKind = iota
	nChar
	nSet
	nCat
	nAlt
	nGroup
	nRepeat
	nBOL
	nEOL
	// nLineStart and nLineEnd are ^ and $ inside a group with the m modifier.
	nLineStart
	nLineEnd
	nWordBoundary
	nNotWordBoundary
	nLook
	nBackref
)

// unbounded is the max of a quantifier with no upper bound.
const unbounded = -1

// maxCount caps the bounds of a counted quantifier. No text that fits in memory reaches a larger count.
const maxCount = math.MaxInt32

// maxNesting caps the depth of groups, which bounds the recursion of the parser and of the compilers.
const maxNesting = 1000

// A node is one element of the syntax tree of a pattern.
type node struct {
	kind nodeKind
	// r is the code point of an nChar.
	r rune
	// set is the class of an nSet.
	set *charSet
	// subs holds the children of nCat and nAlt, and the one child of nGroup, nRepeat and nLook.
	subs []*node
	// min, max and lazy describe an nRepeat.
	min, max int
	lazy     bool
	// group is the number of the capturing group of an nGroup, counted from 1.
	group int
	// groups holds the groups an nBackref names. A name can belong to several groups in separate alternatives, of
	// which at most one has taken part at any time.
	groups []int
	// name is the group name of a named backreference until it is resolved.
	name string
	// fold marks an nBackref, nWordBoundary or nNotWordBoundary inside a case-insensitive group.
	fold bool
	// behind and negative describe an nLook.
	behind, negative bool
}

// A parser reads a pattern by recursive descent. With unicode set it applies the grammar of the u flag. Without it,
// it applies the Annex B grammar of patterns with no flag.
type parser struct {
	p       []rune
	i       int
	unicode bool
	depth   int
	// groupCount and hasNames come from a scan of the whole pattern before parsing, because the meaning of \1 and \k
	// depends on groups that may follow them.
	groupCount int
	hasNames   bool
	groups     int
	names      map[string][]namedGroup
	namedRefs  []*node
	// path lists the alternatives that enclose the current position, innermost last.
	path         []branch
	disjunctions int
	// icase, multiline and dotAll are the modifiers in force, which only a group such as (?i:...) changes.
	icase, multiline, dotAll bool
	// needsBacktracking records that the pattern has a construct only the backtracking matcher can run.
	needsBacktracking bool
}

// A branch names one alternative of one disjunction.
type branch struct {
	disjunction, alternative int
}

// A namedGroup is a capturing group with a name, and the alternatives that enclose it.
type namedGroup struct {
	group int
	path  []branch
}

// A parseError carries the reason a pattern is not valid.
type parseError struct {
	msg string
	pos int
}

func (p *parser) fail(msg string) {
	panic(parseError{msg, p.i})
}

// parse reads the pattern. It returns an error for a pattern that is not valid under the chosen grammar.
func parse(pattern string, unicode bool) (root *node, ps *parser, err error) {
	ps = &parser{p: []rune(pattern), unicode: unicode}
	defer func() {
		if r := recover(); r != nil {
			pe, ok := r.(parseError)
			if !ok {
				panic(r)
			}
			root, err = nil, fmt.Errorf("ecmaregex: %s at offset %d in %q", pe.msg, pe.pos, pattern)
		}
	}()
	ps.scanGroups()
	root = ps.disjunction()
	if ps.i < len(ps.p) {
		if ps.p[ps.i] == ')' {
			ps.fail("unmatched ')'")
		}
		ps.fail("unexpected character")
	}
	for _, ref := range ps.namedRefs {
		named, ok := ps.names[ref.name]
		if !ok {
			ps.fail("reference to an unknown group name")
		}
		for _, g := range named {
			ref.groups = append(ref.groups, g.group)
		}
	}
	return root, ps, nil
}

// scanGroups counts the capturing groups of the pattern and notes whether any is named.
func (p *parser) scanGroups() {
	inClass := false
	s := p.p
	for k := 0; k < len(s); k++ {
		switch c := s[k]; {
		case c == '\\':
			k++
		case c == '[':
			inClass = true
		case c == ']':
			inClass = false
		case c == '(' && !inClass:
			if k+1 >= len(s) || s[k+1] != '?' {
				p.groupCount++
			} else if k+3 < len(s) && s[k+2] == '<' && s[k+3] != '=' && s[k+3] != '!' {
				p.groupCount++
				p.hasNames = true
			}
		}
	}
}

func (p *parser) more() bool { return p.i < len(p.p) }

func (p *parser) peek() rune {
	if p.i < len(p.p) {
		return p.p[p.i]
	}
	return -1
}

func (p *parser) eat(c rune) bool {
	if p.i < len(p.p) && p.p[p.i] == c {
		p.i++
		return true
	}
	return false
}

func (p *parser) disjunction() *node {
	p.depth++
	if p.depth > maxNesting {
		p.fail("groups nested too deeply")
	}
	p.disjunctions++
	p.path = append(p.path, branch{disjunction: p.disjunctions})
	last := len(p.path) - 1
	first := p.alternative()
	if p.peek() != '|' {
		p.path = p.path[:last]
		p.depth--
		return first
	}
	alt := &node{kind: nAlt, subs: []*node{first}}
	for p.eat('|') {
		p.path[last].alternative++
		alt.subs = append(alt.subs, p.alternative())
	}
	p.path = p.path[:last]
	p.depth--
	return alt
}

func (p *parser) alternative() *node {
	var terms []*node
	for p.more() && p.peek() != '|' && p.peek() != ')' {
		terms = append(terms, p.term())
	}
	switch len(terms) {
	case 0:
		return &node{kind: nEmpty}
	case 1:
		return terms[0]
	}
	return &node{kind: nCat, subs: terms}
}

// charNode returns the node for one literal character. Inside a case-insensitive group that is the class of the
// characters equivalent to it.
func (p *parser) charNode(c rune) *node {
	if p.icase {
		if set := foldClosure(newCharSet([]rune{c, c}, false), p.unicode); len(set.ranges) != 2 || set.ranges[0] != set.ranges[1] {
			return &node{kind: nSet, set: set}
		}
	}
	return &node{kind: nChar, r: c}
}

// setNode returns the node for a class. Inside a case-insensitive group the class also takes every character
// equivalent to one of its members.
func (p *parser) setNode(set *charSet) *node {
	if p.icase {
		set = foldClosure(set, p.unicode)
	}
	return &node{kind: nSet, set: set}
}

var anySet = newCharSet([]rune{0, maxRune}, false)

func (p *parser) term() *node {
	c := p.peek()
	quantifiable := true
	var atom *node
	switch c {
	case '^':
		p.i++
		atom, quantifiable = &node{kind: nBOL}, false
		if p.multiline {
			atom.kind, p.needsBacktracking = nLineStart, true
		}
	case '$':
		p.i++
		atom, quantifiable = &node{kind: nEOL}, false
		if p.multiline {
			atom.kind, p.needsBacktracking = nLineEnd, true
		}
	case '(':
		atom, quantifiable = p.group()
	case '.':
		p.i++
		atom = &node{kind: nSet, set: dotSet}
		if p.dotAll {
			atom.set = anySet
		}
	case '[':
		atom = p.class()
	case '\\':
		atom, quantifiable = p.atomEscape()
	case '*', '+', '?':
		p.fail("nothing to repeat")
	case '{':
		if p.unicode || p.looksLikeQuantifier() {
			p.fail("nothing to repeat")
		}
		p.i++
		atom = p.charNode('{')
	case ']', '}':
		if p.unicode {
			p.fail("lone bracket")
		}
		p.i++
		atom = p.charNode(c)
	default:
		p.i++
		atom = p.charNode(c)
	}
	min, max, lazy, ok := p.quantifier()
	if !ok {
		return atom
	}
	if !quantifiable {
		p.fail("nothing to repeat")
	}
	return &node{kind: nRepeat, subs: []*node{atom}, min: min, max: max, lazy: lazy}
}

func isDigit(c rune) bool { return c >= '0' && c <= '9' }

// looksLikeQuantifier reports whether the text at the current '{' reads as {n}, {n,} or {n,m}.
func (p *parser) looksLikeQuantifier() bool {
	s := p.p
	k := p.i + 1
	start := k
	for k < len(s) && isDigit(s[k]) {
		k++
	}
	if k == start || k >= len(s) {
		return false
	}
	if s[k] == '}' {
		return true
	}
	if s[k] != ',' {
		return false
	}
	k++
	for k < len(s) && isDigit(s[k]) {
		k++
	}
	return k < len(s) && s[k] == '}'
}

// number reads a run of decimal digits, saturating at maxCount.
func (p *parser) number() int {
	start := p.i
	v := 0
	for p.i < len(p.p) && isDigit(p.p[p.i]) {
		if v < maxCount {
			v = v*10 + int(p.p[p.i]-'0')
			if v > maxCount {
				v = maxCount
			}
		}
		p.i++
	}
	if start == p.i {
		p.fail("expected a number")
	}
	return v
}

func (p *parser) quantifier() (min, max int, lazy, ok bool) {
	switch c := p.peek(); {
	case c == '*':
		p.i++
		min, max = 0, unbounded
	case c == '+':
		p.i++
		min, max = 1, unbounded
	case c == '?':
		p.i++
		min, max = 0, 1
	case c == '{' && p.looksLikeQuantifier():
		p.i++
		min = p.number()
		max = min
		if p.eat(',') {
			if p.peek() == '}' {
				max = unbounded
			} else {
				max = p.number()
			}
		}
		if !p.eat('}') {
			p.fail("malformed quantifier")
		}
		if max != unbounded && max < min {
			p.fail("numbers out of order in quantifier")
		}
	default:
		return 0, 0, false, false
	}
	return min, max, p.eat('?'), true
}

// group reads a group or a lookaround. It also reports whether a quantifier may follow.
func (p *parser) group() (*node, bool) {
	p.i++
	if !p.eat('?') {
		return p.capture(""), true
	}
	if p.eat(':') {
		body := p.disjunction()
		p.close()
		return &node{kind: nGroup, subs: []*node{body}}, true
	}
	if c := p.peek(); c == 'i' || c == 'm' || c == 's' || c == '-' {
		return p.modifierGroup(), true
	}
	if p.eat('=') || p.eat('!') {
		negative := p.p[p.i-1] == '!'
		body := p.disjunction()
		p.close()
		p.needsBacktracking = true
		// Annex B lets a quantifier follow a lookahead. The u flag does not.
		return &node{kind: nLook, subs: []*node{body}, negative: negative}, !p.unicode
	}
	if p.eat('<') {
		if p.eat('=') || p.eat('!') {
			negative := p.p[p.i-1] == '!'
			body := p.disjunction()
			p.close()
			p.needsBacktracking = true
			return &node{kind: nLook, subs: []*node{body}, negative: negative, behind: true}, false
		}
		name := p.groupName()
		// Two groups may share a name only when they are in separate alternatives of one disjunction, so that no
		// match can go through both.
		for _, other := range p.names[name] {
			if !separateAlternatives(other.path, p.path) {
				p.fail("duplicate group name")
			}
		}
		return p.capture(name), true
	}
	p.fail("invalid group")
	return nil, false
}

// separateAlternatives reports whether two positions lie in different alternatives of some disjunction.
func separateAlternatives(a, b []branch) bool {
	for i := 0; i < len(a) && i < len(b) && a[i].disjunction == b[i].disjunction; i++ {
		if a[i].alternative != b[i].alternative {
			return true
		}
	}
	return false
}

// modifierGroup reads a group that turns modifiers on or off for its body, such as (?i:...) or (?s-i:...). The
// position is after the "(?".
func (p *parser) modifierGroup() *node {
	icase, multiline, dotAll := p.icase, p.multiline, p.dotAll
	var seen [3]bool
	removing, count := false, 0
	for !p.eat(':') {
		c := p.peek()
		p.i++
		var flag int
		switch c {
		case 'i':
			flag, p.icase = 0, !removing
		case 'm':
			flag, p.multiline = 1, !removing
		case 's':
			flag, p.dotAll = 2, !removing
		case '-':
			if removing {
				p.fail("invalid group modifier")
			}
			removing = true
			continue
		default:
			p.fail("invalid group modifier")
		}
		if seen[flag] {
			p.fail("repeated group modifier")
		}
		seen[flag] = true
		count++
	}
	if count == 0 {
		p.fail("invalid group modifier")
	}
	body := p.disjunction()
	p.close()
	p.icase, p.multiline, p.dotAll = icase, multiline, dotAll
	return &node{kind: nGroup, subs: []*node{body}}
}

func (p *parser) capture(name string) *node {
	p.groups++
	g := &node{kind: nGroup, group: p.groups}
	if name != "" {
		if p.names == nil {
			p.names = map[string][]namedGroup{}
		}
		p.names[name] = append(p.names[name], namedGroup{group: g.group, path: append([]branch(nil), p.path...)})
	}
	g.subs = []*node{p.disjunction()}
	p.close()
	return g
}

func (p *parser) close() {
	if !p.eat(')') {
		p.fail("unterminated group")
	}
}

// groupName reads a group name up to and including its '>'. A character of the name may be written as a \u escape.
func (p *parser) groupName() string {
	var name []rune
	for !p.eat('>') {
		if !p.more() {
			p.fail("invalid group name")
		}
		c := p.peek()
		p.i++
		if c == '\\' {
			if !p.eat('u') {
				p.fail("invalid group name")
			}
			c = p.unicodeEscape(true)
			if c < 0 {
				p.fail("invalid group name")
			}
		}
		if len(name) == 0 {
			if !(c == '$' || c == '_' || idStart.contains(c)) {
				p.fail("invalid group name")
			}
		} else if !(c == '$' || c == 0x200C || c == 0x200D || idContinue.contains(c)) {
			p.fail("invalid group name")
		}
		name = append(name, c)
	}
	if len(name) == 0 {
		p.fail("invalid group name")
	}
	return string(name)
}

var (
	idStart    = newCharSet(tabIdStart, false)
	idContinue = newCharSet(tabIdContinue, false)
)

// atomEscape reads an escape outside a class. It also reports whether a quantifier may follow.
func (p *parser) atomEscape() (*node, bool) {
	p.i++
	if !p.more() {
		p.fail("\\ at end of pattern")
	}
	c := p.peek()
	switch c {
	case 'b', 'B':
		p.i++
		boundary := &node{kind: nWordBoundary}
		if c == 'B' {
			boundary.kind = nNotWordBoundary
		}
		// With the u flag, a case-insensitive word boundary counts two more characters as word characters, which
		// the regexp package's \b does not.
		if p.unicode && p.icase {
			boundary.fold, p.needsBacktracking = true, true
		}
		return boundary, false
	case 'k':
		// Annex B reads \k as the letter k in a pattern with no named group.
		if p.unicode || p.hasNames {
			p.i++
			if !p.eat('<') {
				p.fail("invalid named reference")
			}
			ref := &node{kind: nBackref, name: p.groupName(), fold: p.icase}
			p.namedRefs = append(p.namedRefs, ref)
			p.needsBacktracking = true
			return ref, true
		}
		p.i++
		return p.charNode('k'), true
	}
	if c >= '1' && c <= '9' {
		start := p.i
		n := p.number()
		if n <= p.groupCount {
			p.needsBacktracking = true
			return &node{kind: nBackref, groups: []int{n}, fold: p.icase}, true
		}
		if p.unicode {
			p.fail("reference to a group that does not exist")
		}
		// Annex B reads it as an octal escape, or as the digit itself.
		p.i = start
		return p.charNode(p.legacyOctal()), true
	}
	if set := p.classEscape(c); set != nil {
		return p.setNode(set), true
	}
	return p.charNode(p.characterEscape(false)), true
}

// legacyOctal reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start
// one (8 or 9) stands for itself.
func (p *parser) legacyOctal() rune {
	v := rune(0)
	k := 0
	for k < 3 && p.more() && p.peek() >= '0' && p.peek() <= '7' && v*8+(p.peek()-'0') <= 0377 {
		v = v*8 + (p.peek() - '0')
		p.i++
		k++
	}
	if k == 0 {
		d := p.peek()
		p.i++
		return d
	}
	return v
}

var (
	digitSet    = newCharSet(digitPairs, false)
	notDigitSet = newCharSet(digitPairs, true)
	wordSet     = newCharSet(wordPairs, false)
	notWordSet  = newCharSet(wordPairs, true)
	spaceSet    = newCharSet(spacePairs, false)
	notSpaceSet = newCharSet(spacePairs, true)
)

// classEscape reads a class escape such as \d or \p{...} whose letter is c. It returns nil, consuming nothing, when
// c does not start one.
func (p *parser) classEscape(c rune) *charSet {
	switch c {
	case 'd':
		p.i++
		return digitSet
	case 'D':
		p.i++
		return notDigitSet
	case 'w':
		p.i++
		if p.unicode && p.icase {
			return foldWordSet
		}
		return wordSet
	case 'W':
		p.i++
		if p.unicode && p.icase {
			return notFoldWordSet
		}
		return notWordSet
	case 's':
		p.i++
		return spaceSet
	case 'S':
		p.i++
		return notSpaceSet
	case 'p', 'P':
		// Without the u flag \p is the letter p.
		if !p.unicode {
			return nil
		}
		p.i++
		if !p.eat('{') {
			p.fail("invalid property name")
		}
		start := p.i
		for p.more() && p.peek() != '}' {
			p.i++
		}
		expr := string(p.p[start:p.i])
		if !p.eat('}') {
			p.fail("invalid property name")
		}
		set, ok := propertySet(expr)
		if !ok {
			p.fail("invalid property name")
		}
		if c == 'P' {
			return newCharSet(set.ranges, true)
		}
		return set
	}
	return nil
}

func hexValue(c rune) int {
	switch {
	case c >= '0' && c <= '9':
		return int(c - '0')
	case c >= 'a' && c <= 'f':
		return int(c-'a') + 10
	case c >= 'A' && c <= 'F':
		return int(c-'A') + 10
	}
	return -1
}

// hex reads exactly n hexadecimal digits. It returns -1, consuming nothing, when they are not there.
func (p *parser) hex(n int) rune {
	if p.i+n > len(p.p) {
		return -1
	}
	v := rune(0)
	for k := 0; k < n; k++ {
		d := hexValue(p.p[p.i+k])
		if d < 0 {
			return -1
		}
		v = v*16 + rune(d)
	}
	p.i += n
	return v
}

func isASCIILetter(c rune) bool { return c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' }

// syntaxCharacter reports whether c is one of the characters the u flag lets an identity escape name.
func syntaxCharacter(c rune) bool {
	switch c {
	case '^', '$', '\\', '.', '*', '+', '?', '(', ')', '[', ']', '{', '}', '|', '/':
		return true
	}
	return false
}

// characterEscape reads the escape after a backslash, in or out of a class, and returns the code point it names.
func (p *parser) characterEscape(inClass bool) rune {
	c := p.peek()
	p.i++
	switch c {
	case 'f':
		return '\f'
	case 'n':
		return '\n'
	case 'r':
		return '\r'
	case 't':
		return '\t'
	case 'v':
		return '\v'
	case 'c':
		l := p.peek()
		if isASCIILetter(l) {
			p.i++
			return l % 32
		}
		if !p.unicode && inClass && (isDigit(l) || l == '_') {
			p.i++
			return l % 32
		}
		if p.unicode {
			p.fail("invalid control escape")
		}
		// Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
		p.i--
		return '\\'
	case '0':
		if p.more() && isDigit(p.peek()) {
			if p.unicode {
				p.fail("invalid decimal escape")
			}
			p.i--
			return p.legacyOctal()
		}
		return 0
	case 'x':
		h := p.hex(2)
		if h < 0 {
			if p.unicode {
				p.fail("invalid \\x escape")
			}
			return 'x'
		}
		return h
	case 'u':
		u := p.unicodeEscape(p.unicode)
		if u < 0 {
			if p.unicode {
				p.fail("invalid \\u escape")
			}
			return 'u'
		}
		return u
	}
	if c >= '1' && c <= '9' && !p.unicode && inClass {
		p.i--
		return p.legacyOctal()
	}
	// Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows
	// any character.
	if p.unicode && !syntaxCharacter(c) && !(inClass && c == '-') {
		p.fail("invalid escape")
	}
	return c
}

// unicodeEscape reads what follows a \u and returns the code point, or -1, consuming nothing, when it is not a
// well-formed escape. With braces set it accepts the \u{...} form. A surrogate pair written as two escapes is one code
// point.
func (p *parser) unicodeEscape(braces bool) rune {
	start := p.i
	if braces && p.eat('{') {
		digits := p.i
		v := rune(0)
		for p.more() && hexValue(p.peek()) >= 0 {
			if v <= maxRune {
				v = v*16 + rune(hexValue(p.peek()))
			}
			p.i++
		}
		if digits == p.i || !p.eat('}') || v > maxRune {
			p.i = start
			return -1
		}
		return v
	}
	u := p.hex(4)
	if u < 0 {
		return -1
	}
	if u >= 0xD800 && u <= 0xDBFF && p.i+6 <= len(p.p) && p.p[p.i] == '\\' && p.p[p.i+1] == 'u' {
		save := p.i
		p.i += 2
		if low := p.hex(4); low >= 0xDC00 && low <= 0xDFFF {
			return 0x10000 + (u-0xD800)<<10 + (low - 0xDC00)
		}
		p.i = save
	}
	return u
}

// class reads a character class from its '[' to its ']'.
func (p *parser) class() *node {
	p.i++
	negated := p.eat('^')
	var pairs []rune
	for {
		if !p.more() {
			p.fail("unterminated character class")
		}
		if p.eat(']') {
			break
		}
		first, firstSet := p.classAtom()
		if p.peek() == '-' && p.i+1 < len(p.p) && p.p[p.i+1] != ']' {
			p.i++
			second, secondSet := p.classAtom()
			if firstSet != nil || secondSet != nil {
				if p.unicode {
					p.fail("invalid character class range")
				}
				// Annex B reads the '-' as itself when a class escape is at either end.
				pairs = appendClassAtom(pairs, first, firstSet)
				pairs = append(pairs, '-', '-')
				pairs = appendClassAtom(pairs, second, secondSet)
				continue
			}
			if second < first {
				p.fail("range out of order in character class")
			}
			pairs = append(pairs, first, second)
			continue
		}
		pairs = appendClassAtom(pairs, first, firstSet)
	}
	// Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a
	// negated class excludes exactly those characters.
	set := newCharSet(pairs, false)
	if p.icase {
		set = foldClosure(set, p.unicode)
	}
	if negated {
		set = newCharSet(set.ranges, true)
	}
	return &node{kind: nSet, set: set}
}

func appendClassAtom(pairs []rune, c rune, set *charSet) []rune {
	if set != nil {
		return append(pairs, set.ranges...)
	}
	return append(pairs, c, c)
}

// classAtom reads one member of a class. It returns a code point, or a set for a class escape.
func (p *parser) classAtom() (rune, *charSet) {
	c := p.peek()
	p.i++
	if c != '\\' {
		return c, nil
	}
	if !p.more() {
		p.fail("\\ at end of pattern")
	}
	e := p.peek()
	if e == 'b' {
		p.i++
		return '\b', nil
	}
	if set := p.classEscape(e); set != nil {
		return 0, set
	}
	return p.characterEscape(true), nil
}
