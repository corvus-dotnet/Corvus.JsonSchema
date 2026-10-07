package jsonschema

import (
	"math"
	"math/bits"
	"slices"
	"strconv"
	"sync"
	"unicode/utf8"
	"unsafe"
)

// MaxDepth is the deepest nesting of arrays and objects a document may have.
const MaxDepth = 1000

// The kinds of a value. They are the evaluator's type bits, so a type test is one mask operation.
const (
	kindNull   = 1
	kindBool   = 2
	kindObject = 4
	kindArray  = 8
	kindNumber = 16
	kindString = 32
)

// Number representations (the flags of a number).
const (
	// numInt is an integer that fits an int64.
	numInt = 0
	// numUint is an integer in [2^63, 2^64), held as a uint64.
	numUint = 1
	// numFloat is anything else, held as the bits of a float64.
	numFloat = 2
)

// String flags.
const (
	// strText says the string's bytes are in Document.text (it had escapes), not in Document.source.
	strText = 1
	// strWide says the string has bytes outside ASCII.
	strWide = 2
)

// Document is JSON text parsed for evaluation. It holds the UTF-8 text and one flat array of values (a tape) that
// the evaluator reads in place, with no object per value. Strings stay in the text where they have no escapes.
//
// A Document is immutable and safe for concurrent use. Parsing is strict RFC 8259. Anything but whitespace after
// the value, invalid UTF-8, lone surrogates in \u escapes, numbers out of the range of a float64 and nesting deeper
// than MaxDepth are errors. Of duplicate property names, the last value is kept, at the position of the first.
type Document struct {
	// Two words per value. The first is the header: the kind in bits 0 to 7, flags in bits 8 to 15 and a 32-bit
	// field in the high half (a string's byte length, a number's offset in the source, a container's count). The
	// second is the data: a string's offset, a number's bits, a container's first child, a boolean's 0 or 1. The
	// children of a container are consecutive. An object's children are key and value pairs.
	tape   []uint64
	source []byte
	// The unescaped strings.
	text []byte
	root int
}

// ParseError reports text that is not valid JSON.
type ParseError struct {
	// Message says what was wrong.
	Message string
	// Offset is the byte offset at which the error was found.
	Offset int
}

// Error implements the error interface.
func (e *ParseError) Error() string {
	return "invalid JSON at offset " + strconv.Itoa(e.Offset) + ": " + e.Message
}

var parsers = sync.Pool{New: func() any { return new(parser) }}

// ParseDocument parses UTF-8 JSON text. The document keeps the slice. Do not modify it afterwards.
func ParseDocument(json []byte) (*Document, error) {
	p := parsers.Get().(*parser)
	d, err := p.parseNew(json)
	if !p.retainsTooMuch() {
		parsers.Put(p)
	}
	return d, err
}

// ParseDocumentString parses JSON text.
func ParseDocumentString(json string) (*Document, error) {
	return ParseDocument([]byte(json))
}

// String returns the document as compact JSON text, with numbers as they were written.
func (d *Document) String() string {
	return string(d.appendJSON(nil, d.root))
}

// ---------------------------------------------------------------------------------------------------------------------
// Reading values (the evaluator's side). A value is the index of its node.

func (d *Document) kind(n int) uint8 {
	return uint8(d.tape[n<<1])
}

func (d *Document) flags(n int) uint8 {
	return uint8(d.tape[n<<1] >> 8)
}

// count is a container's item or property count, or a string's byte length.
func (d *Document) count(n int) int {
	return int(d.tape[n<<1] >> 32)
}

func (d *Document) data(n int) uint64 {
	return d.tape[n<<1+1]
}

// first is a container's first child.
func (d *Document) first(n int) int {
	return int(d.tape[n<<1+1])
}

func (d *Document) boolean(n int) bool {
	return d.tape[n<<1+1] != 0
}

// str is the bytes of a string value.
func (d *Document) str(n int) []byte {
	h := d.tape[n<<1]
	off := d.tape[n<<1+1]
	end := off + h>>32
	if h&(strText<<8) != 0 {
		return d.text[off:end]
	}
	return d.source[off:end]
}

func (d *Document) strASCII(n int) bool {
	return d.tape[n<<1]&(strWide<<8) == 0
}

// property is the value of an object's property, or -1.
func (d *Document) property(object int, name string) int {
	k := d.first(object)
	for i := d.count(object); i > 0; i-- {
		if d.count(k) == len(name) && string(d.str(k)) == name {
			return k + 1
		}
		k += 2
	}
	return -1
}

// float is a number's value as a float64.
func (d *Document) float(n int) float64 {
	v := d.data(n)
	switch d.flags(n) {
	case numInt:
		return float64(int64(v))
	case numUint:
		return float64(v)
	default:
		return math.Float64frombits(v)
	}
}

// numberText is a number's text as written.
func (d *Document) numberText(n int) []byte {
	start := d.count(n)
	return d.source[start:numberEnd(d.source, start)]
}

// appendJSON appends the value as compact JSON text (numbers as written, strings escaped again).
func (d *Document) appendJSON(out []byte, n int) []byte {
	switch d.kind(n) {
	case kindNull:
		return append(out, "null"...)
	case kindBool:
		if d.boolean(n) {
			return append(out, "true"...)
		}
		return append(out, "false"...)
	case kindNumber:
		return append(out, d.numberText(n)...)
	case kindString:
		return appendQuoted(out, d.str(n))
	case kindArray:
		out = append(out, '[')
		c := d.first(n)
		for i := 0; i < d.count(n); i++ {
			if i > 0 {
				out = append(out, ',')
			}
			out = d.appendJSON(out, c+i)
		}
		return append(out, ']')
	default:
		out = append(out, '{')
		c := d.first(n)
		for i := 0; i < d.count(n); i++ {
			if i > 0 {
				out = append(out, ',')
			}
			out = appendQuoted(out, d.str(c+2*i))
			out = append(out, ':')
			out = d.appendJSON(out, c+2*i+1)
		}
		return append(out, '}')
	}
}

func appendQuoted(out []byte, s []byte) []byte {
	const hex = "0123456789abcdef"
	out = append(out, '"')
	for _, c := range s {
		switch c {
		case '"':
			out = append(out, '\\', '"')
		case '\\':
			out = append(out, '\\', '\\')
		case '\n':
			out = append(out, '\\', 'n')
		case '\r':
			out = append(out, '\\', 'r')
		case '\t':
			out = append(out, '\\', 't')
		case '\b':
			out = append(out, '\\', 'b')
		case '\f':
			out = append(out, '\\', 'f')
		default:
			if c < 0x20 {
				out = append(out, '\\', 'u', '0', '0', hex[c>>4], hex[c&15])
			} else {
				out = append(out, c)
			}
		}
	}
	return append(out, '"')
}

// numberEnd is the end of the number whose text starts at j (already validated).
func numberEnd(b []byte, j int) int {
	for j < len(b) {
		c := b[j]
		if (c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E' {
			j++
		} else {
			break
		}
	}
	return j
}

// ---------------------------------------------------------------------------------------------------------------------
// The parser

// parser builds a Document. Its buffers are reused from one parse to the next.
type parser struct {
	b []byte
	i int
	// Checking syntax only. No document is built and numbers are not converted.
	validating bool
	// Finished children of closed containers, each container's consecutive (two words per node).
	nodes []uint64
	// The values of the open containers, innermost last. A container's run moves to nodes when it closes.
	scratch []uint64
	// The scratch index (in nodes) at which each open container's children start, with the object flag in bit 31.
	frames []uint32
	text   []byte
	// Scratch for finding duplicate keys in large objects.
	hashes []uint64
	// Scratch for rebuilding an object with duplicate keys.
	pairs []uint64

	errMessage string
	errOffset  int
}

// The buffers a parser keeps between parses, in bytes, beyond which a pooled parser is dropped.
const retainedLimit = 1 << 20

func (p *parser) retainsTooMuch() bool {
	return 8*(cap(p.nodes)+cap(p.scratch)+cap(p.hashes)+cap(p.pairs))+cap(p.text) > retainedLimit
}

func (p *parser) fail(message string, at int) bool {
	p.errMessage = message
	p.errOffset = at
	return false
}

func (p *parser) err() error {
	return &ParseError{Message: p.errMessage, Offset: p.errOffset}
}

func (p *parser) reset(b []byte, validating bool) {
	p.b = b
	p.i = 0
	p.validating = validating
	p.nodes = p.nodes[:0]
	p.scratch = p.scratch[:0]
	p.frames = p.frames[:0]
	p.text = p.text[:0]
}

// isValid reports whether b is one valid JSON value. It allocates nothing once the buffers have grown.
func (p *parser) isValid(b []byte) bool {
	p.reset(b, true)
	ok := p.parse()
	p.b = nil
	return ok
}

// parseNew parses b into a new document, exactly sized.
func (p *parser) parseNew(b []byte) (*Document, error) {
	p.reset(b, false)
	ok := p.parse()
	p.b = nil
	if !ok {
		return nil, p.err()
	}
	root := len(p.nodes) / 2
	tape := make([]uint64, len(p.nodes)+2)
	copy(tape, p.nodes)
	tape[root*2] = p.scratch[0]
	tape[root*2+1] = p.scratch[1]
	var text []byte
	if len(p.text) > 0 {
		text = append([]byte(nil), p.text...)
	}
	return &Document{tape: tape, source: b, text: text, root: root}, nil
}

// parseInto parses b into d, reusing its arrays and the parser's (no allocation once they have grown). The document
// is valid until the next parse into it.
func (p *parser) parseInto(d *Document, b []byte) bool {
	p.reset(b, false)
	ok := p.parse()
	p.b = nil
	if !ok {
		return false
	}
	d.tape = append(append(d.tape[:0], p.nodes...), p.scratch[0], p.scratch[1])
	d.text = append(d.text[:0], p.text...)
	d.source = b
	d.root = len(p.nodes) / 2
	return true
}

func (p *parser) skipWs() {
	b := p.b
	i := p.i
	for i < len(b) {
		c := b[i]
		if c != ' ' && c != '\n' && c != '\r' && c != '\t' {
			break
		}
		i++
	}
	p.i = i
}

func (p *parser) peek() int {
	if p.i < len(p.b) {
		return int(p.b[p.i])
	}
	return -1
}

func (p *parser) push(header, data uint64) {
	p.scratch = append(p.scratch, header, data)
}

const objectFrame = 1 << 31

func (p *parser) parse() bool {
	p.skipWs()
	for {
		// A value.
		switch c := p.peek(); c {
		case '{':
			p.i++
			p.skipWs()
			if len(p.frames) >= MaxDepth {
				return p.fail("nesting too deep", p.i)
			}
			if p.peek() == '}' {
				p.i++
				p.push(kindObject, 0)
			} else {
				p.frames = append(p.frames, uint32(len(p.scratch)/2)|objectFrame)
				if !p.key() {
					return false
				}
				continue
			}
		case '[':
			p.i++
			p.skipWs()
			if len(p.frames) >= MaxDepth {
				return p.fail("nesting too deep", p.i)
			}
			if p.peek() == ']' {
				p.i++
				p.push(kindArray, 0)
			} else {
				p.frames = append(p.frames, uint32(len(p.scratch)/2))
				continue
			}
		case '"':
			if !p.str() {
				return false
			}
		case 't':
			if !p.literal("true", kindBool, 1) {
				return false
			}
		case 'f':
			if !p.literal("false", kindBool, 0) {
				return false
			}
		case 'n':
			if !p.literal("null", kindNull, 0) {
				return false
			}
		case '-', '0', '1', '2', '3', '4', '5', '6', '7', '8', '9':
			if !p.number() {
				return false
			}
		case -1:
			return p.fail("unexpected end of input", p.i)
		default:
			return p.fail("expected a value", p.i)
		}
		// After a value: separators and closing brackets, until the next value or the end.
		for next := false; !next; {
			if len(p.frames) == 0 {
				p.skipWs()
				if p.i != len(p.b) {
					return p.fail("trailing characters", p.i)
				}
				return true
			}
			frame := p.frames[len(p.frames)-1]
			object := frame&objectFrame != 0
			p.skipWs()
			c := p.peek()
			switch {
			case c == ',':
				p.i++
				p.skipWs()
				if object && !p.key() {
					return false
				}
				next = true
			case c == '}' && object:
				p.i++
				p.close(int(frame&^objectFrame), true)
			case c == ']' && !object:
				p.i++
				p.close(int(frame), false)
			case c == -1:
				return p.fail("unexpected end of input", p.i)
			case object:
				return p.fail("expected ',' or '}'", p.i)
			default:
				return p.fail("expected ',' or ']'", p.i)
			}
		}
	}
}

// key reads a property name and its colon, leaving the parser at the value.
func (p *parser) key() bool {
	if p.peek() != '"' {
		return p.fail("expected a property name", p.i)
	}
	if !p.str() {
		return false
	}
	p.skipWs()
	if p.peek() != ':' {
		return p.fail("expected ':'", p.i)
	}
	p.i++
	p.skipWs()
	return true
}

// close moves the closed container's children to nodes and pushes the container in their place.
func (p *parser) close(start int, object bool) {
	p.frames = p.frames[:len(p.frames)-1]
	if object && len(p.scratch)/2-start > 2 && !p.validating {
		p.dedupe(start)
	}
	children := len(p.scratch)/2 - start
	first := len(p.nodes) / 2
	p.nodes = append(p.nodes, p.scratch[start*2:]...)
	p.scratch = p.scratch[:start*2]
	if object {
		p.push(kindObject|uint64(children/2)<<32, uint64(first))
	} else {
		p.push(kindArray|uint64(children)<<32, uint64(first))
	}
}

// scratchKey is the bytes of the key at scratch value index a.
func (p *parser) scratchKey(a int) []byte {
	return p.keyBytes(p.scratch[a*2], p.scratch[a*2+1])
}

func (p *parser) keyBytes(h, off uint64) []byte {
	if h&(strText<<8) != 0 {
		return p.text[off : off+h>>32]
	}
	return p.b[off : off+h>>32]
}

func (p *parser) keyEquals(a, b int) bool {
	if p.scratch[a*2]>>32 != p.scratch[b*2]>>32 {
		return false
	}
	return string(p.scratchKey(a)) == string(p.scratchKey(b))
}

// dedupe keeps, of duplicate property names in the object whose pairs start at start, the last value at the first
// position.
func (p *parser) dedupe(start int) {
	count := (len(p.scratch)/2 - start) / 2
	duplicate := false
	if count <= 16 {
	small:
		for j := 1; j < count; j++ {
			for k := 0; k < j; k++ {
				if p.keyEquals(start+2*k, start+2*j) {
					duplicate = true
					break small
				}
			}
		}
	} else {
		// Sorted by hash in reused scratch: only keys with equal hashes are compared.
		p.hashes = p.hashes[:0]
		for j := 0; j < count; j++ {
			p.hashes = append(p.hashes, strHash(p.scratchKey(start+2*j))&^0xffffffff|uint64(j))
		}
		h := p.hashes
		slices.Sort(h)
	large:
		for a := 0; a < count; {
			e := a + 1
			for e < count && h[e]>>32 == h[a]>>32 {
				e++
			}
			for x := a + 1; x < e; x++ {
				for y := a; y < x; y++ {
					if p.keyEquals(start+2*int(uint32(h[x])), start+2*int(uint32(h[y]))) {
						duplicate = true
						break large
					}
				}
			}
			a = e
		}
	}
	if !duplicate {
		return
	}
	p.pairs = append(p.pairs[:0], p.scratch[start*2:]...)
	pairs := p.pairs
	p.scratch = p.scratch[:start*2]
	for q := 0; q < count; q++ {
		name := p.keyBytes(pairs[q*4], pairs[q*4+1])
		at := -1
		for k := start; k < len(p.scratch)/2; k += 2 {
			if string(p.scratchKey(k)) == string(name) {
				at = k
				break
			}
		}
		if at >= 0 {
			p.scratch[(at+1)*2] = pairs[q*4+2]
			p.scratch[(at+1)*2+1] = pairs[q*4+3]
		} else {
			p.scratch = append(p.scratch, pairs[q*4:q*4+4]...)
		}
	}
}

func (p *parser) literal(word string, kind, data uint64) bool {
	if p.i+len(word) > len(p.b) || string(p.b[p.i:p.i+len(word)]) != word {
		return p.fail("expected a value", p.i)
	}
	p.i += len(word)
	p.push(kind, data)
	return true
}

// scan is, by byte: 0 for plain ASCII, 1 for a quote, backslash or control character, 2 for a non-ASCII byte.
var scan = func() (t [256]uint8) {
	for c := 0; c < 0x20; c++ {
		t[c] = 1
	}
	t['"'] = 1
	t['\\'] = 1
	for c := 0x80; c < 0x100; c++ {
		t[c] = 2
	}
	return
}()

// str reads a string, from its opening quote.
func (p *parser) str() bool {
	b := p.b
	start := p.i + 1
	j := start
	wide := uint8(0)
	for j < len(b) {
		c := scan[b[j]]
		if c == 1 {
			break
		}
		wide |= c
		j++
	}
	if j >= len(b) {
		return p.fail("unterminated string", j)
	}
	switch b[j] {
	case '"':
		header := uint64(kindString) | uint64(j-start)<<32
		if wide != 0 {
			if !utf8.Valid(b[start:j]) {
				return p.fail("invalid UTF-8", start)
			}
			header |= strWide << 8
		}
		p.i = j + 1
		p.push(header, uint64(start))
		return true
	case '\\':
		return p.escaped(start, j, wide != 0)
	default:
		return p.fail("control character in a string", j)
	}
}

// escaped reads the rest of a string with escapes, unescaped into the text buffer. j is at the first backslash.
func (p *parser) escaped(start, j int, wide bool) bool {
	offset := len(p.text)
	b := p.b
	run := start
	for {
		if j >= len(b) {
			return p.fail("unterminated string", j)
		}
		c := b[j]
		switch {
		case c == '"':
			if wide && !utf8.Valid(b[run:j]) {
				return p.fail("invalid UTF-8", run)
			}
			p.text = append(p.text, b[run:j]...)
			p.i = j + 1
			header := uint64(kindString) | strText<<8 | uint64(len(p.text)-offset)<<32
			if wide {
				header |= strWide << 8
			}
			p.push(header, uint64(offset))
			return true
		case c == '\\':
			if wide && !utf8.Valid(b[run:j]) {
				return p.fail("invalid UTF-8", run)
			}
			p.text = append(p.text, b[run:j]...)
			e := -1
			if j+1 < len(b) {
				e = int(b[j+1])
			}
			var out byte
			switch e {
			case '"', '\\', '/':
				out = byte(e)
			case 'b':
				out = '\b'
			case 'f':
				out = '\f'
			case 'n':
				out = '\n'
			case 'r':
				out = '\r'
			case 't':
				out = '\t'
			case 'u':
				cp, ok := p.unicodeEscape(j)
				if !ok {
					return false
				}
				if cp >= 0x10000 {
					j += 12
				} else {
					j += 6
				}
				if cp >= 0x80 {
					wide = true
				}
				p.text = utf8.AppendRune(p.text, rune(cp))
				run = j
				continue
			default:
				return p.fail("invalid escape", j)
			}
			p.text = append(p.text, out)
			j += 2
			run = j
		case c < 0x20:
			return p.fail("control character in a string", j)
		default:
			if c >= 0x80 {
				wide = true
			}
			j++
		}
	}
}

func (p *parser) hex4(at int) int {
	if at+4 > len(p.b) {
		return -1
	}
	v := 0
	for _, c := range p.b[at : at+4] {
		var d int
		switch {
		case c >= '0' && c <= '9':
			d = int(c - '0')
		case c >= 'a' && c <= 'f':
			d = int(c-'a') + 10
		case c >= 'A' && c <= 'F':
			d = int(c-'A') + 10
		default:
			return -1
		}
		v = v*16 + d
	}
	return v
}

// unicodeEscape reads a \u escape at j (and its low surrogate, for a high one): the code point.
func (p *parser) unicodeEscape(j int) (int, bool) {
	u := p.hex4(j + 2)
	if u < 0 {
		return 0, p.fail("invalid \\u escape", j)
	}
	if u >= 0xd800 && u <= 0xdbff {
		low := -1
		if j+7 < len(p.b) && p.b[j+6] == '\\' && p.b[j+7] == 'u' {
			low = p.hex4(j + 8)
		}
		if low >= 0xdc00 && low <= 0xdfff {
			return 0x10000 + (u-0xd800)<<10 + (low - 0xdc00), true
		}
		return 0, p.fail("lone leading surrogate in hex escape", j)
	}
	if u >= 0xdc00 && u <= 0xdfff {
		return 0, p.fail("lone trailing surrogate in hex escape", j)
	}
	return u, true
}

func isDigit(b []byte, j int) bool {
	return j < len(b) && b[j] >= '0' && b[j] <= '9'
}

// number reads a number: integers that fit 64 bits as integers (unsigned beyond an int64), anything else as a
// float64. The header keeps the offset of the text.
func (p *parser) number() bool {
	b := p.b
	start := p.i
	j := start
	negative := b[j] == '-'
	if negative {
		j++
	}
	var value uint64
	overflow := false
	intDigits := 0
	if j < len(b) && b[j] == '0' {
		j++
		intDigits = 1
	} else if isDigit(b, j) {
		for isDigit(b, j) {
			if !overflow {
				hi, lo := bits.Mul64(value, 10)
				sum := lo + uint64(b[j]-'0')
				if hi != 0 || sum < lo {
					overflow = true
				} else {
					value = sum
				}
			}
			intDigits++
			j++
		}
	} else {
		return p.fail("invalid number", j)
	}
	floating := false
	fracStart, fracEnd := -1, -1
	if j < len(b) && b[j] == '.' {
		j++
		if !isDigit(b, j) {
			return p.fail("invalid number", j)
		}
		fracStart = j
		for isDigit(b, j) {
			j++
		}
		fracEnd = j
		floating = true
	}
	exponent := 0
	expOverflow := false
	if j < len(b) && (b[j] == 'e' || b[j] == 'E') {
		j++
		expNegative := false
		if j < len(b) && (b[j] == '+' || b[j] == '-') {
			expNegative = b[j] == '-'
			j++
		}
		if !isDigit(b, j) {
			return p.fail("invalid number", j)
		}
		for isDigit(b, j) {
			if exponent < 100000 {
				exponent = exponent*10 + int(b[j]-'0')
			} else {
				expOverflow = true
			}
			j++
		}
		if expNegative {
			exponent = -exponent
		}
		floating = true
	}
	p.i = j
	header := uint64(kindNumber) | uint64(start)<<32
	if !floating && !overflow {
		if !negative {
			if value>>63 != 0 {
				p.push(header|numUint<<8, value)
			} else {
				p.push(header|numInt<<8, value)
			}
			return true
		}
		// -0 is the float.
		if value != 0 && value <= 1<<63 {
			p.push(header|numInt<<8, -value)
			return true
		}
	}
	d, exact := math.NaN(), false
	if !expOverflow {
		d, exact = fastFloat(b, negative, start, intDigits, fracStart, fracEnd, exponent)
	}
	if !exact {
		if p.validating && !expOverflow && intDigits+exponent < 300 {
			// Checking syntax only: the number is well within the range of a float64, and its value is not needed.
			p.push(header|numFloat<<8, 0)
			return true
		}
		// The text is a validated JSON number, which strconv reads the same way.
		var err error
		d, err = strconv.ParseFloat(unsafe.String(&b[start], j-start), 64)
		if err != nil || math.IsInf(d, 0) {
			return p.fail("number out of range", start)
		}
	}
	p.push(header|numFloat<<8, math.Float64bits(d))
	return true
}

var powers = [...]float64{
	1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19,
	1e20, 1e21, 1e22,
}

// fastFloat is the correctly rounded float64 for a number whose significant digits fit 2^53 and whose decimal
// exponent is within 10^22 (Clinger's fast path: one exact conversion and one correctly rounded operation).
func fastFloat(b []byte, negative bool, start, intDigits, fracStart, fracEnd, exponent int) (float64, bool) {
	intStart := start
	if negative {
		intStart++
	}
	var mantissa uint64
	digits := 0
	for _, c := range b[intStart : intStart+intDigits] {
		if digits > 0 || c != '0' {
			digits++
		}
		mantissa = mantissa*10 + uint64(c-'0')
		if digits > 15 {
			return 0, false
		}
	}
	scale := exponent
	if fracStart >= 0 {
		for _, c := range b[fracStart:fracEnd] {
			if digits > 0 || c != '0' {
				digits++
			}
			mantissa = mantissa*10 + uint64(c-'0')
			if digits > 15 {
				return 0, false
			}
		}
		scale -= fracEnd - fracStart
	}
	d := float64(mantissa)
	switch {
	case scale == 0:
	case scale > 0 && scale <= 22:
		d *= powers[scale]
	case scale < 0 && scale >= -22:
		d /= powers[-scale]
	default:
		return 0, false
	}
	if negative {
		d = -d
	}
	return d, true
}
