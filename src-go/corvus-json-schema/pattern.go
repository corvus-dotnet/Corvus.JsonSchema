package jsonschema

import (
	"math"
	"math/bits"
	"strings"
	"sync"
	"unicode/utf8"
)

// pattern and patternProperties matching with ECMA-262 semantics (the u flag), as JSON Schema specifies.
//
// A pattern gets the cheapest matcher that decides it exactly:
//
//   - patterns every string matches ("", ".*", "[\s\S]*" and the like), ".+", and line lengths ("^.{1,256}$");
//   - anchored sequences of quantified ASCII character classes and literals ("^[a-z][a-z0-9_]{0,29}$", "^x-",
//     "^[@$_#]"), matched in one pass over the string;
//   - alternatives of such sequences once groups are multiplied out ("^([a|A]uto)|([n|N]one)$"), sets of literals,
//     and separated lists ("^([a-z]+)(\.[a-z]+)*$");
//   - anything else by the regular expression engine (see pattern_engine.go).
//
// Patterns compile once per process (a pattern is immutable, so identical patterns share one matcher).

type matcherKind uint8

const (
	// matchEverything matches every string.
	matchEverything matcherKind = iota
	// matchLiteral is "^literal" (with "$": the whole string).
	matchLiteral
	matchSequence
	matchSeparatedList
	// matchHasContent is ".+" ("^.+" with start): some (the first) character is not a line terminator.
	matchHasContent
	// matchLine is "^.{min,max}$": between min and max characters, none a line terminator.
	matchLine
	// matchLiterals is "^(a|b|...)$": one of a set of strings.
	matchLiterals
	// matchAlternatives is top-level alternatives of literals or class sequences, each optionally anchored.
	matchAlternatives
	// matchExcludedClassWithWord is "^(?=[^SET]+$)(?=(.*\w)).+$": a non-empty line without a character of the set,
	// containing a word character. With bangs ("^(?=!+[^SET]+$)..."), that line follows one or more "!".
	matchExcludedClassWithWord
	matchEngine
)

// pattern is a compiled pattern.
type pattern struct {
	source string
	kind   matcherKind
	// matchLiteral: the text, and whether it is the whole string.
	text  string
	whole bool
	// matchHasContent: anchored at the start. matchExcludedClassWithWord: bangs.
	flag bool
	// matchLine.
	min, max uint32
	seq      *sequence
	list     *separatedList
	few      []string
	many     map[string]struct{}
	alts     []alternative
	set      charSet
	engine   patternEngine
}

// match reports whether the pattern matches somewhere in s.
func (p *pattern) match(s []byte) bool {
	switch p.kind {
	case matchEverything:
		return true
	case matchLiteral:
		if p.whole {
			return string(s) == p.text
		}
		return len(s) >= len(p.text) && string(s[:len(p.text)]) == p.text
	case matchSequence:
		return p.seq.match(s)
	case matchSeparatedList:
		return p.list.match(s)
	case matchHasContent:
		if p.flag {
			if len(s) == 0 {
				return false
			}
			c, _ := utf8.DecodeRune(s)
			return !isLineTerminator(c)
		}
		for i := 0; i < len(s); {
			c, size := decodeRune(s, i)
			if !isLineTerminator(c) {
				return true
			}
			i += size
		}
		return false
	case matchLine:
		n := lineLength(s)
		return n >= 0 && uint64(n) >= uint64(p.min) && uint64(n) <= uint64(p.max)
	case matchLiterals:
		if p.many != nil {
			_, ok := p.many[string(s)]
			return ok
		}
		for _, t := range p.few {
			if string(s) == t {
				return true
			}
		}
		return false
	case matchAlternatives:
		ascii := isASCII(s)
		for i := range p.alts {
			if p.alts[i].match(s, ascii) {
				return true
			}
		}
		return false
	case matchExcludedClassWithWord:
		if p.flag {
			rest := s
			for len(rest) > 0 && rest[0] == '!' {
				rest = rest[1:]
			}
			if len(rest) == len(s) || len(rest) == 0 {
				return false
			}
			s = rest
		}
		word := false
		for i := 0; i < len(s); {
			c, size := decodeRune(s, i)
			if p.set.contains(c) || isLineTerminator(c) {
				return false
			}
			word = word || wordSet.contains(c)
			i += size
		}
		return word
	default:
		return p.engine.Match(s)
	}
}

// decodeRune reads the character at i (a byte for ASCII).
func decodeRune(s []byte, i int) (rune, int) {
	if c := s[i]; c < utf8.RuneSelf {
		return rune(c), 1
	}
	return utf8.DecodeRune(s[i:])
}

func isASCII(s []byte) bool {
	for _, c := range s {
		if c >= utf8.RuneSelf {
			return false
		}
	}
	return true
}

// isLineTerminator reports an ECMA-262 LineTerminator, which "." does not match.
func isLineTerminator(c rune) bool {
	return c == '\n' || c == '\r' || c == 0x2028 || c == 0x2029
}

// lineLength is the number of characters, when none is a line terminator, else -1.
func lineLength(s []byte) int {
	n := 0
	for i := 0; i < len(s); {
		c, size := decodeRune(s, i)
		if isLineTerminator(c) {
			return -1
		}
		n++
		i += size
	}
	return n
}

// alternative is one alternative of matchAlternatives: a literal anchored at either end (or neither), a class
// sequence anchored at the start, or a class sequence of fixed width anchored at the end or not at all.
type alternative struct {
	kind uint8
	// altLiteral.
	text       string
	start, end bool
	seq        *sequence
	// altEnd, altAnywhere: the sequence's width in characters.
	width int
}

const (
	altLiteral uint8 = iota
	// altStart is "^sequence" (with "$" when the sequence runs to the end).
	altStart
	// altEnd is "sequence$" of width characters: matched over the string's last width characters.
	altEnd
	// altAnywhere is an unanchored sequence of width characters: matched at each position.
	altAnywhere
)

// match reports whether the alternative matches s, ascii saying whether s is ASCII.
func (a *alternative) match(s []byte, ascii bool) bool {
	switch a.kind {
	case altLiteral:
		switch {
		case a.start && a.end:
			return string(s) == a.text
		case a.start:
			return len(s) >= len(a.text) && string(s[:len(a.text)]) == a.text
		case a.end:
			return len(s) >= len(a.text) && string(s[len(s)-len(a.text):]) == a.text
		}
		return indexString(s, a.text) >= 0
	case altStart:
		if ascii {
			return a.seq.matchASCII(s)
		}
		return a.seq.matchChars(s)
	case altEnd:
		if ascii {
			return len(s) >= a.width && a.seq.matchASCII(s[len(s)-a.width:])
		}
		skip := utf8.RuneCount(s) - a.width
		if skip < 0 {
			return false
		}
		at := 0
		for ; skip > 0; skip-- {
			_, size := decodeRune(s, at)
			at += size
		}
		return a.seq.matchChars(s[at:])
	default:
		if ascii {
			for at := 0; at+a.width <= len(s); at++ {
				if a.seq.matchASCII(s[at:]) {
					return true
				}
			}
			return false
		}
		positions := utf8.RuneCount(s) - a.width + 1
		for at := 0; positions > 0; positions-- {
			if a.seq.matchChars(s[at:]) {
				return true
			}
			_, size := decodeRune(s, at)
			at += size
		}
		return false
	}
}

// indexString is the index of the first occurrence of text in s, or -1.
func indexString(s []byte, text string) int {
	for i := 0; i+len(text) <= len(s); i++ {
		if string(s[i:i+len(text)]) == text {
			return i
		}
	}
	return -1
}

var patternCache sync.Map

// compilePattern compiles (or fetches from the process-wide cache) a pattern. Not ok when it is not a valid
// ECMA-262 regular expression.
func compilePattern(source string) (*pattern, bool) {
	if p, ok := patternCache.Load(source); ok {
		return p.(*pattern), true
	}
	// Validity is ECMA-262's: a pattern the engine rejects is an error, whichever matcher would run it. A pattern
	// with a faster matcher that is valid with the u flag needs no engine at all. The engine reads a pattern that
	// is valid only without the u flag by code point too, so the faster matchers decide those the same way.
	p := choosePattern(source)
	if p == nil || !validRegex(source) {
		engine, ok := compileEngine(source)
		if !ok {
			return nil, false
		}
		if p == nil {
			p = &pattern{kind: matchEngine, engine: engine}
		}
	}
	p.source = source
	actual, _ := patternCache.LoadOrStore(source, p)
	return actual.(*pattern), true
}

func choosePattern(p string) *pattern {
	// Unanchored (or start-anchored) ".*" finds an empty match in any string. "^.*$" does not ("." stops at a line
	// terminator), so it is not listed.
	switch p {
	case "", ".*", "^.*", ".*$", "(.*)", "^(.*)", `[\s\S]*`, `^[\s\S]*`, `^[\s\S]*$`:
		return &pattern{kind: matchEverything}
	}
	switch p {
	case ".+", ".", "(.+)":
		return &pattern{kind: matchHasContent}
	case "^.+", "^.":
		return &pattern{kind: matchHasContent, flag: true}
	}
	// "^X.*" (no "$") matches exactly where "^X" does: ".*" can match nothing.
	if rest, ok := strings.CutSuffix(p, ".*"); ok && strings.HasPrefix(rest, "^") && len(rest) > 1 &&
		!endsWithEscape(rest) && !strings.ContainsRune("*+?}|(^", rune(rest[len(rest)-1])) {
		if m := choosePattern(rest); m != nil {
			return m
		}
	}
	if min, max, ok := lineRange(p); ok {
		return &pattern{kind: matchLine, min: min, max: max}
	}
	if texts, ok := wholeAlternatives(p); ok {
		m := &pattern{kind: matchLiterals}
		if len(texts) <= 8 {
			m.few = texts
		} else {
			m.many = make(map[string]struct{}, len(texts))
			for _, t := range texts {
				m.many[t] = struct{}{}
			}
		}
		return m
	}
	if alts := parseAlternatives(p); alts != nil {
		return &pattern{kind: matchAlternatives, alts: alts}
	}
	if list := parseSeparatedList(p); list != nil {
		return &pattern{kind: matchSeparatedList, list: list}
	}
	if set, bangs, ok := excludedClassWithWord(p); ok {
		return &pattern{kind: matchExcludedClassWithWord, set: set, flag: bangs}
	}
	if seq := parseSequence(p); seq != nil {
		if text, ok := seq.literal(); ok {
			return &pattern{kind: matchLiteral, text: text, whole: seq.toEnd}
		}
		return &pattern{kind: matchSequence, seq: seq}
	}
	return nil
}

// lineRange reads "^.{m,n}$" (and "^.*$", "^.+$", "^.{m}$", "^.{m,}$", each also as "^(....)$"): the bounds on the
// length of a line.
func lineRange(p string) (uint32, uint32, bool) {
	q, ok := strings.CutPrefix(p, "^.")
	grouped := false
	if !ok {
		if q, ok = strings.CutPrefix(p, "^(."); !ok {
			return 0, 0, false
		}
		grouped = true
	}
	if q, ok = strings.CutSuffix(q, "$"); !ok {
		return 0, 0, false
	}
	if grouped {
		if q, ok = strings.CutSuffix(q, ")"); !ok {
			return 0, 0, false
		}
	}
	min, max, next, ok := parseQuantifier(q, 0)
	if !ok || next != len(q) || next == 0 || strings.HasSuffix(q, "?") {
		return 0, 0, false
	}
	return min, max, true
}

// literalText reads literal text: characters other than syntax characters, and identity or control escapes.
func literalText(p string) (string, bool) {
	var out strings.Builder
	for i := 0; i < len(p); i++ {
		c := p[i]
		switch c {
		case '\\':
			i++
			if i >= len(p) {
				return "", false
			}
			switch e := p[i]; e {
			case 'n':
				out.WriteByte('\n')
			case 'r':
				out.WriteByte('\r')
			case 't':
				out.WriteByte('\t')
			case 'f':
				out.WriteByte('\f')
			case 'v':
				out.WriteByte('\v')
			case '^', '$', '\\', '.', '*', '+', '?', '(', ')', '[', ']', '{', '}', '|', '/', '-':
				out.WriteByte(e)
			default:
				return "", false
			}
		case '^', '$', '.', '*', '+', '?', '(', ')', '[', ']', '{', '}', '|':
			return "", false
		default:
			out.WriteByte(c)
		}
	}
	return out.String(), true
}

// splitAlternatives splits at top-level "|"s (none inside a group, a class or after "\").
func splitAlternatives(p string) ([]string, bool) {
	depth, inClass, start := 0, false, 0
	var parts []string
	for i := 0; i < len(p); i++ {
		switch c := p[i]; {
		case c == '\\':
			i++
		case c == '[':
			inClass = true
		case c == ']':
			inClass = false
		case c == '(' && !inClass:
			depth++
		case c == ')' && !inClass:
			if depth == 0 {
				return nil, false
			}
			depth--
		case c == '|' && !inClass && depth == 0:
			parts = append(parts, p[start:i])
			start = i + 1
		}
	}
	return append(parts, p[min(start, len(p)):]), true
}

// wholeAlternatives reads "^(a|b|...)$" or "^(?:a|b|...)$" over literal alternatives.
func wholeAlternatives(p string) ([]string, bool) {
	inner, ok := strings.CutPrefix(p, "^(")
	if !ok {
		return nil, false
	}
	if inner, ok = strings.CutSuffix(inner, ")$"); !ok {
		return nil, false
	}
	inner = strings.TrimPrefix(inner, "?:")
	if strings.HasPrefix(inner, "?") {
		return nil, false
	}
	parts, ok := splitAlternatives(inner)
	if !ok || len(parts) < 2 {
		return nil, false
	}
	texts := make([]string, len(parts))
	for i, part := range parts {
		if texts[i], ok = literalText(part); !ok {
			return nil, false
		}
	}
	return texts, true
}

// At most this many alternatives after expanding groups.
const maxAlternatives = 64

// At most this many alternatives when any is a class sequence: beyond it, trying each in turn is slower than one
// pass of the engine.
const maxSequenceAlternatives = 4

// parseAlternatives reads two or more alternatives once groups of alternatives (and optional groups) are expanded
// into whole alternatives ("^a|b|c$", "^([a|A]uto)|([n|N]one)$", "^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$"), each a
// literal or a class sequence anchored where its own "^" and "$" say.
func parseAlternatives(p string) []alternative {
	c := []rune(p)
	parts, end, ok := expandAlternatives(c, 0)
	// A single alternative is only new here when a group was expanded (parseSequence takes the rest).
	if !ok || end != len(c) || len(parts) == 0 || (len(parts) == 1 && parts[0] == p) {
		return nil
	}
	alts := make([]alternative, 0, len(parts))
	sequences := false
	for _, part := range parts {
		body, start := strings.CutPrefix(part, "^")
		end := false
		if r, ok := strings.CutSuffix(body, "$"); ok && !endsWithEscape(r) {
			body, end = r, true
		}
		if text, ok := literalText(body); ok {
			alts = append(alts, alternative{kind: altLiteral, text: text, start: start, end: end})
			continue
		}
		source := "^" + body
		if end {
			source += "$"
		}
		seq := parseSequence(source)
		if seq == nil {
			return nil
		}
		sequences = true
		if start {
			alts = append(alts, alternative{kind: altStart, seq: seq})
			continue
		}
		width := 0
		for _, item := range seq.items {
			if item.min != item.max {
				return nil
			}
			width += int(item.min)
		}
		kind := altAnywhere
		if end {
			kind = altEnd
		}
		alts = append(alts, alternative{kind: kind, seq: seq, width: width})
	}
	if sequences && len(alts) > maxSequenceAlternatives {
		return nil
	}
	return alts
}

// endsWithEscape reports whether the text ends in an unpaired "\" (so a "$" after it would be escaped).
func endsWithEscape(t string) bool {
	n := 0
	for i := len(t) - 1; i >= 0 && t[i] == '\\'; i-- {
		n++
	}
	return n%2 == 1
}

func runeAt(c []rune, i int) rune {
	if i < len(c) {
		return c[i]
	}
	return -1
}

// product is every concatenation of one of a and one of b, or not ok when there are too many.
func product(a, b []string) ([]string, bool) {
	if len(a)*len(b) > maxAlternatives {
		return nil, false
	}
	out := make([]string, 0, len(a)*len(b))
	for _, x := range a {
		for _, y := range b {
			out = append(out, x+y)
		}
	}
	return out, true
}

// expandAlternatives expands the alternatives from i up to an unmatched ")" or the end: groups of alternatives
// multiply out, a group quantified by "?" also contributes the empty alternative, and everything else is copied.
// Not ok for lookarounds, other quantified groups, or too many alternatives.
func expandAlternatives(c []rune, i int) ([]string, int, bool) {
	var all []string
	branch := []string{""}
	appendAll := func(text string) {
		for b := range branch {
			branch[b] += text
		}
	}
loop:
	for i < len(c) {
		switch c[i] {
		case '|':
			all = append(all, branch...)
			branch = []string{""}
			i++
		case ')':
			break loop
		case '(':
			i++
			if runeAt(c, i) == '?' {
				if runeAt(c, i+1) != ':' {
					return nil, 0, false
				}
				i += 2
			}
			inner, next, ok := expandAlternatives(c, i)
			if !ok || runeAt(c, next) != ')' {
				return nil, 0, false
			}
			i = next + 1
			switch runeAt(c, i) {
			case '?':
				inner = append(inner, "")
				i++
				if runeAt(c, i) == '?' {
					i++
				}
			case '{':
				// An exact count repeats the group. Any other bound needs a real regular expression.
				close := i
				for close < len(c) && c[close] != '}' {
					close++
				}
				if close == len(c) {
					return nil, 0, false
				}
				n, ok := parseCount(string(c[i+1 : close]))
				if !ok || n > 16 {
					return nil, 0, false
				}
				repeated := []string{""}
				for ; n > 0; n-- {
					if repeated, ok = product(repeated, inner); !ok {
						return nil, 0, false
					}
				}
				inner = repeated
				i = close + 1
				if runeAt(c, i) == '?' {
					i++
				}
			case '*', '+':
				return nil, 0, false
			}
			if branch, ok = product(branch, inner); !ok {
				return nil, 0, false
			}
		case '[':
			start := i
			i++
			if runeAt(c, i) == '^' {
				i++
			}
			if runeAt(c, i) == ']' {
				i++
			}
			for {
				if i >= len(c) {
					return nil, 0, false
				}
				if c[i] == ']' {
					break
				}
				if c[i] == '\\' {
					i++
				}
				i++
			}
			i++
			appendAll(string(c[start:i]))
		case '\\':
			if i+2 > len(c) {
				return nil, 0, false
			}
			appendAll(string(c[i : i+2]))
			i += 2
		default:
			appendAll(string(c[i]))
			i++
		}
		if len(all)+len(branch) > maxAlternatives {
			return nil, 0, false
		}
	}
	return append(all, branch...), i, true
}

// parseCount reads a decimal count that fits 32 bits.
func parseCount(s string) (uint32, bool) {
	if s == "" || len(s) > 10 {
		return 0, false
	}
	n := uint64(0)
	for i := 0; i < len(s); i++ {
		if !isASCIIDigit(s[i]) {
			return 0, false
		}
		n = n*10 + uint64(s[i]-'0')
	}
	return uint32(n), n <= math.MaxUint32
}

// excludedClassWithWord reads "^(?=[^SET]+$)(?=(.*\w)).+$" (with "(?:" or "(" around ".*\w"): the excluded set.
func excludedClassWithWord(p string) (charSet, bool, bool) {
	// parseClass reads a class body from after "[", here "^SET]".
	body, bangs := strings.CutPrefix(p, "^(?=!+[")
	if !bangs {
		var ok bool
		if body, ok = strings.CutPrefix(p, "^(?=["); !ok {
			return charSet{}, false, false
		}
	}
	negated, next, ok := parseClass(body, 0)
	if !ok || !strings.HasPrefix(body, "^") {
		return charSet{}, false, false
	}
	excluded := charSet{ascii: [2]uint64{^negated.ascii[0], ^negated.ascii[1]}}
	// The run of "!" ends exactly where the class starts only when the class excludes "!".
	if bangs && !excluded.contains('!') {
		return charSet{}, false, false
	}
	switch body[next:] {
	case `+$)(?=(.*\w)).+$`, `+$)(?=(?:.*\w)).+$`, `+$)(?=.*\w).+$`:
		return excluded, bangs, true
	}
	return charSet{}, false, false
}

// ---------------------------------------------------------------------------------------------------------------------
// Class sequences

// charSet is a set of characters: ASCII by bit mask, then either all or none of the line separators U+2028 and
// U+2029, and either all or none of the other non-ASCII characters.
type charSet struct {
	ascii      [2]uint64
	nonASCII   bool
	separators bool
}

// charRange is ASCII lo to hi inclusive (hi below 128).
func charRange(lo, hi byte) charSet {
	var s charSet
	for c := int(lo); c <= int(hi); c++ {
		s.ascii[c>>6] |= 1 << (c & 63)
	}
	return s
}

// dotSet is ECMA-262's ".": everything but the line terminators.
func dotSet() charSet {
	s := charSet{ascii: [2]uint64{math.MaxUint64, math.MaxUint64}, nonASCII: true}
	s.ascii[0] &^= 1<<'\n' | 1<<'\r'
	return s
}

func (s charSet) union(other charSet) charSet {
	return charSet{
		ascii:      [2]uint64{s.ascii[0] | other.ascii[0], s.ascii[1] | other.ascii[1]},
		nonASCII:   s.nonASCII || other.nonASCII,
		separators: s.separators || other.separators,
	}
}

func (s charSet) negate() charSet {
	return charSet{ascii: [2]uint64{^s.ascii[0], ^s.ascii[1]}, nonASCII: !s.nonASCII, separators: !s.separators}
}

func (s charSet) disjoint(other charSet) bool {
	return s.ascii[0]&other.ascii[0] == 0 && s.ascii[1]&other.ascii[1] == 0 &&
		!(s.nonASCII && other.nonASCII) && !(s.separators && other.separators)
}

// hasASCII reports whether the set holds an ASCII character.
func (s *charSet) hasASCII(c byte) bool {
	return s.ascii[(c>>6)&1]>>(c&63)&1 != 0
}

func (s *charSet) contains(c rune) bool {
	switch {
	case c < 128:
		return s.hasASCII(byte(c))
	case c == 0x2028 || c == 0x2029:
		return s.separators
	}
	return s.nonASCII
}

// single is the set's character when it holds exactly one ASCII character and nothing else.
func (s charSet) single() (byte, bool) {
	if s.nonASCII || s.separators || bits.OnesCount64(s.ascii[0])+bits.OnesCount64(s.ascii[1]) != 1 {
		return 0, false
	}
	if s.ascii[0] != 0 {
		return byte(bits.TrailingZeros64(s.ascii[0])), true
	}
	return byte(64 + bits.TrailingZeros64(s.ascii[1])), true
}

var (
	digitSet = charRange('0', '9')
	// wordSet is "\w".
	wordSet = charRange('0', '9').union(charRange('A', 'Z')).union(charRange('a', 'z')).union(charRange('_', '_'))
)

type sequenceItem struct {
	set charSet
	min uint32
	// math.MaxUint32: unbounded.
	max uint32
}

// sequence is "^" then quantified character sets, optionally "$". Matched greedily, which is exact because every
// variable item's set is disjoint from the next item's (a character the item leaves cannot be taken by the next one
// either).
type sequence struct {
	items []sequenceItem
	toEnd bool
}

func parseSequence(p string) *sequence {
	if len(p) == 0 || p[0] != '^' || !isASCII([]byte(p)) {
		return nil
	}
	i := 1
	var items []sequenceItem
	toEnd := false
	for i < len(p) {
		var set charSet
		switch c := p[i]; c {
		case '$':
			if i != len(p)-1 {
				return nil
			}
			toEnd = true
			i++
			continue
		case '[':
			s, next, ok := parseClass(p, i+1)
			if !ok {
				return nil
			}
			set, i = s, next
		case '\\':
			if i+1 >= len(p) {
				return nil
			}
			s, ok := classEscape(p[i+1])
			if !ok {
				return nil
			}
			set = s
			i += 2
		case '.':
			i++
			set = dotSet()
		case '(', ')', '|', '^', '*', '+', '?', '{', '}', ']':
			return nil
		default:
			i++
			set = charRange(c, c)
		}
		min, max, next, ok := parseQuantifier(p, i)
		if !ok {
			return nil
		}
		i = next
		items = append(items, sequenceItem{set, min, max})
	}
	// Greedy matching is exact only when a variable item cannot give up characters an item after it needs: its set
	// must be disjoint from every item that can directly follow it (up to the first that cannot match nothing).
	for i, item := range items {
		if item.min == item.max {
			continue
		}
		for _, next := range items[i+1:] {
			if !item.set.disjoint(next.set) {
				return nil
			}
			if next.min > 0 {
				break
			}
		}
	}
	return &sequence{items: items, toEnd: toEnd}
}

// literal is the text, when every item is one fixed character.
func (q *sequence) literal() (string, bool) {
	text := make([]byte, 0, len(q.items))
	for _, item := range q.items {
		c, ok := item.set.single()
		if !ok || item.min != 1 || item.max != 1 {
			return "", false
		}
		text = append(text, c)
	}
	return string(text), true
}

func (q *sequence) match(s []byte) bool {
	if isASCII(s) {
		return q.matchASCII(s)
	}
	return q.matchChars(s)
}

// matchChars matches over any text, a character at a time.
func (q *sequence) matchChars(s []byte) bool {
	at := 0
	for i := range q.items {
		item := &q.items[i]
		n := uint32(0)
		for n < item.max && at < len(s) {
			c, size := decodeRune(s, at)
			if !item.set.contains(c) {
				break
			}
			at += size
			n++
		}
		if n < item.min {
			return false
		}
	}
	return !q.toEnd || at == len(s)
}

// matchASCII matches over ASCII text, a byte per character.
func (q *sequence) matchASCII(b []byte) bool {
	at := 0
	for i := range q.items {
		item := &q.items[i]
		start := at
		end := len(b)
		if uint64(item.max) < uint64(len(b)-start) {
			end = start + int(item.max)
		}
		for at < end && item.set.hasASCII(b[at]) {
			at++
		}
		if at-start < int(item.min) {
			return false
		}
	}
	return !q.toEnd || at == len(b)
}

// consume greedily matches the items at the start of s (ignoring toEnd): the bytes taken, or -1.
func (q *sequence) consume(s []byte) int {
	at := 0
	for i := range q.items {
		item := &q.items[i]
		n := uint32(0)
		for n < item.max && at < len(s) {
			c, size := decodeRune(s, at)
			if !item.set.contains(c) {
				break
			}
			at += size
			n++
		}
		if n < item.min {
			return -1
		}
	}
	return at
}

// greedyBefore reports whether greedy matching is exact when next can follow the items: every variable item is
// disjoint from the items that can directly follow it (up to the first that cannot match nothing), next included.
func greedyBefore(items []sequenceItem, next charSet) bool {
outer:
	for i, item := range items {
		if item.min == item.max {
			continue
		}
		for _, following := range items[i+1:] {
			if !item.set.disjoint(following.set) {
				return false
			}
			if following.min > 0 {
				continue outer
			}
		}
		if !item.set.disjoint(next) {
			return false
		}
	}
	return true
}

// separatedList is a list of items between separators: "^I(SR)*$" or "^I(SR)+$" (no final), or "^(RS)*F$" and
// "^(RS)+F$". The separator is a fixed sequence and no variable class can run into what follows it, so greedy
// matching splits the string exactly where the pattern does.
type separatedList struct {
	// I of the first form.
	first     *sequence
	repeated  *sequence
	separator *sequence
	// F of the second form, matched against the whole remainder.
	final      *sequence
	minRepeats uint32
}

func (l *separatedList) match(s []byte) bool {
	rest := s
	repeats := uint32(0)
	if l.first != nil {
		n := l.first.consume(rest)
		if n < 0 {
			return false
		}
		rest = rest[n:]
		for {
			if len(rest) == 0 {
				return repeats >= l.minRepeats
			}
			if n = l.separator.consume(rest); n < 0 {
				return false
			}
			rest = rest[n:]
			if n = l.repeated.consume(rest); n < 0 {
				return false
			}
			rest = rest[n:]
			repeats++
		}
	}
	for {
		if repeats >= l.minRepeats && l.final.match(rest) {
			return true
		}
		n := l.repeated.consume(rest)
		if n < 0 {
			return false
		}
		m := l.separator.consume(rest[n:])
		if m < 0 {
			return false
		}
		rest = rest[n+m:]
		repeats++
	}
}

func parseSeparatedList(p string) *separatedList {
	body, ok := strings.CutPrefix(p, "^")
	if !ok {
		return nil
	}
	if body, ok = strings.CutSuffix(body, "$"); !ok || endsWithEscape(body) {
		return nil
	}
	// Top-level groups: the index of each one's "(" and ")".
	type group struct{ open, close int }
	var groups []group
	depth, inClass, open := 0, false, 0
	for i := 0; i < len(body); i++ {
		switch c := body[i]; {
		case c == '\\':
			i++
		case c == '[' && !inClass:
			inClass = true
		case c == ']' && inClass:
			inClass = false
		case c == '(' && !inClass:
			if depth == 0 {
				open = i
			}
			depth++
		case c == ')' && !inClass:
			if depth == 0 {
				return nil
			}
			if depth--; depth == 0 {
				groups = append(groups, group{open, i})
			}
		}
	}
	var quantified []group
	for _, g := range groups {
		if g.close+1 < len(body) && (body[g.close+1] == '*' || body[g.close+1] == '+') {
			quantified = append(quantified, g)
		}
	}
	if len(quantified) != 1 {
		return nil
	}
	g := quantified[0]
	minRepeats := uint32(0)
	if body[g.close+1] == '+' {
		minRepeats = 1
	}
	inner, ok := stripGroup(body[g.open : g.close+1])
	if !ok {
		return nil
	}
	before, after := body[:g.open], body[g.close+2:]
	items := func(t string) []sequenceItem {
		if seq := parseSequence("^" + t); seq != nil {
			return seq.items
		}
		return nil
	}
	fixed := func(items []sequenceItem) bool {
		for _, item := range items {
			if item.min != item.max || item.min == 0 {
				return false
			}
		}
		return len(items) != 0
	}
	groupItems := items(inner)
	if groupItems == nil {
		return nil
	}
	switch {
	case before != "" && after == "":
		// ^I(SR)*$
		unwrapped, ok := unwrapGroup(before)
		if !ok {
			return nil
		}
		first := items(unwrapped)
		if first == nil && parseSequence("^"+unwrapped) == nil {
			return nil
		}
		for k := 1; k < len(groupItems); k++ {
			separator, repeated := groupItems[:k], groupItems[k:]
			next := separator[0].set
			if fixed(separator) && greedyBefore(first, next) && greedyBefore(repeated, next) {
				return &separatedList{
					first: &sequence{items: first}, repeated: &sequence{items: repeated},
					separator: &sequence{items: separator}, minRepeats: minRepeats,
				}
			}
		}
	case before == "" && after != "":
		// ^(RS)*F$
		unwrapped, ok := unwrapGroup(after)
		if !ok {
			return nil
		}
		final := parseSequence("^" + unwrapped + "$")
		if final == nil {
			return nil
		}
		for k := 1; k < len(groupItems); k++ {
			repeated, separator := groupItems[:k], groupItems[k:]
			if fixed(separator) && greedyBefore(repeated, separator[0].set) {
				return &separatedList{
					repeated: &sequence{items: repeated}, separator: &sequence{items: separator},
					final: &sequence{items: final.items, toEnd: true}, minRepeats: minRepeats,
				}
			}
		}
	}
	return nil
}

// stripGroup is the inside of "(...)" or "(?:...)". Not ok for other groups.
func stripGroup(g string) (string, bool) {
	inner, ok := strings.CutPrefix(g, "(")
	if !ok {
		return "", false
	}
	if inner, ok = strings.CutSuffix(inner, ")"); !ok {
		return "", false
	}
	if rest, ok := strings.CutPrefix(inner, "?:"); ok {
		return rest, true
	}
	return inner, !strings.HasPrefix(inner, "?")
}

// unwrapGroup is a text that is one group wrapping everything, unwrapped. Otherwise the text itself.
func unwrapGroup(t string) (string, bool) {
	if !strings.HasPrefix(t, "(") || !strings.HasSuffix(t, ")") {
		return t, true
	}
	inner, ok := stripGroup(t)
	if !ok {
		return "", false
	}
	// Only when the parentheses enclose the whole text ("(a)(b)" is two groups).
	depth, escaped := 0, false
	for i := 0; i < len(inner); i++ {
		switch c := inner[i]; {
		case escaped:
			escaped = false
		case c == '\\':
			escaped = true
		case c == '(':
			depth++
		case c == ')':
			if depth--; depth < 0 {
				return "", false
			}
		}
	}
	return inner, true
}

func isASCIIPunctuation(c byte) bool {
	return (c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~')
}

// classEscape is the set of a class escape ("\d", "\w", their negations, or an escaped punctuation character). Not
// ok for "\s" (not ASCII-only) and anything else.
func classEscape(c byte) (charSet, bool) {
	switch c {
	case 'd':
		return digitSet, true
	case 'D':
		return digitSet.negate(), true
	case 'w':
		return wordSet, true
	case 'W':
		return wordSet.negate(), true
	case 'n':
		return charRange('\n', '\n'), true
	case 'r':
		return charRange('\r', '\r'), true
	case 't':
		return charRange('\t', '\t'), true
	}
	if isASCIIPunctuation(c) {
		return charRange(c, c), true
	}
	return charSet{}, false
}

// parseClass reads a class body from after "[" to after "]", with ASCII members (a negated class also takes every
// non-ASCII character).
func parseClass(b string, i int) (charSet, int, bool) {
	fail := func() (charSet, int, bool) { return charSet{}, 0, false }
	negated := i < len(b) && b[i] == '^'
	if negated {
		i++
	}
	var set charSet
	first := true
	for {
		if i >= len(b) {
			return fail()
		}
		c := b[i]
		if c == ']' {
			if first {
				return fail() // "[]" and "[^]"
			}
			i++
			break
		}
		first = false
		// One atom: a single character (for ranges) or a set escape.
		atom := charRange(c, c)
		lo, single := c, true
		if c == '\\' {
			if i+1 >= len(b) {
				return fail()
			}
			e := b[i+1]
			s, ok := classEscape(e)
			if !ok {
				return fail()
			}
			atom = s
			lo, single = atom.single()
			single = single && (e == 'n' || e == 'r' || e == 't' || isASCIIPunctuation(e))
			i += 2
		} else {
			if c >= utf8.RuneSelf {
				return fail()
			}
			i++
		}
		if i+1 < len(b) && b[i] == '-' && b[i+1] != ']' {
			if !single {
				return fail()
			}
			hi := b[i+1]
			if hi == '\\' {
				if i+2 >= len(b) || !isASCIIPunctuation(b[i+2]) {
					return fail()
				}
				hi = b[i+2]
				i += 3
			} else {
				i += 2
			}
			if lo > hi || hi >= utf8.RuneSelf {
				return fail()
			}
			set = set.union(charRange(lo, hi))
		} else {
			set = set.union(atom)
		}
	}
	if negated {
		set = set.negate()
	}
	return set, i, true
}

// parseQuantifier reads an optional quantifier ("*", "+", "?", "{n}", "{n,}", "{n,m}", each optionally lazy) at i.
func parseQuantifier(b string, i int) (min, max uint32, next int, ok bool) {
	if i >= len(b) {
		return 1, 1, i, true
	}
	switch b[i] {
	case '*':
		i++
		min, max = 0, math.MaxUint32
	case '+':
		i++
		min, max = 1, math.MaxUint32
	case '?':
		i++
		min, max = 0, 1
	case '{':
		end := strings.IndexByte(b[i:], '}')
		if end < 0 {
			return 0, 0, 0, false
		}
		end += i
		body := b[i+1 : end]
		i = end + 1
		lo, hi, ranged := strings.Cut(body, ",")
		if min, ok = parseCount(lo); !ok {
			return 0, 0, 0, false
		}
		switch {
		case !ranged:
			max = min
		case hi == "":
			max = math.MaxUint32
		default:
			if max, ok = parseCount(hi); !ok {
				return 0, 0, 0, false
			}
		}
	default:
		return 1, 1, i, true
	}
	if min > max {
		return 0, 0, 0, false
	}
	if i < len(b) && b[i] == '?' {
		i++ // Laziness does not change whether the whole pattern matches.
	}
	return min, max, i, true
}
