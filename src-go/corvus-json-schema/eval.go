package jsonschema

import (
	"strconv"
	"strings"
	"sync"
	"unicode/utf8"
)

// Evaluation. One general evaluator serves two modes. Without a collector it fails fast at the first violation and
// reports nothing. With a collector it is exhaustive and reports every keyword with its evaluation path, schema
// location and instance location, reproducing the C# collecting mode (keyword order, paths, messages, which
// subschema results are committed or discarded). Fail-fast evaluation mostly runs through plans (see plan.go) and
// comes here only for nodes that track evaluated properties or items.

// program is the compiled program an evaluator runs.
type program struct {
	nodes            []*schemaNode
	root             nodeID
	usesDynamicScope bool
	maxDepth         int
	formats          map[string]FormatValidator
	// For fail-fast evaluation: each node's target after following pure $ref hops.
	fastTarget []nodeID
	// Name lookup tables for nodes with many properties.
	propertyMaps      []map[string]nodeID
	annotationSources []annotationSource
	annotationsOnce   sync.Once
	annotations       [][]annotationEntry
	assertFormatSet   bool
	// Fail-fast plans.
	plans []plan
}

const propertyMapThreshold = 8

func newProgram(c *compiledSchema, options *compileOptions) *program {
	nodes := c.nodes
	p := &program{
		nodes: nodes, root: c.root, usesDynamicScope: c.usesDynamicScope, maxDepth: options.maxDepth,
		formats: options.formats, annotationSources: c.annotationSources,
		assertFormatSet: options.assertFormat != 0,
	}
	p.fastTarget = make([]nodeID, len(nodes))
	for id := range nodes {
		current := nodeID(id)
		for hop := 0; hop < 16; hop++ {
			n := nodes[current]
			// Pure $ref hops, and forwards: a node whose only assertion is one allOf branch, unless either end is on
			// an in-place cycle, which keeps its guard.
			next := pureRefTarget(n)
			if next < 0 {
				next = forwardTarget(n)
				if next < 0 || n.inPlaceCycle || nodes[next].inPlaceCycle || next == current {
					break
				}
			}
			if p.usesDynamicScope && nodes[next].resourceID != n.resourceID {
				break
			}
			current = next
		}
		p.fastTarget[id] = current
	}
	p.propertyMaps = make([]map[string]nodeID, len(nodes))
	for id, n := range nodes {
		if len(n.properties) > propertyMapThreshold {
			m := make(map[string]nodeID, len(n.properties))
			for _, property := range n.properties {
				m[property.name] = property.node
			}
			p.propertyMaps[id] = m
		}
	}
	p.plans = compilePlans(p)
	return p
}

// nodeAnnotations are the annotation keywords of every node (computed on the first evaluation with a collector).
func (p *program) nodeAnnotations() [][]annotationEntry {
	p.annotationsOnce.Do(func() {
		p.annotations = make([][]annotationEntry, len(p.nodes))
		for id, n := range p.nodes {
			if src := p.annotationSources[id]; src.set {
				p.annotations[id] = collectAnnotations(src.doc, src.value, n.dialect, src.vocab, src.content,
					p.assertFormatSet)
			}
		}
	})
	return p.annotations
}

func (p *program) property(id nodeID, n *schemaNode, name []byte) nodeID {
	if m := p.propertyMaps[id]; m != nil {
		if node, ok := m[string(name)]; ok {
			return node
		}
		return noNode
	}
	for i := range n.properties {
		if n.properties[i].name == string(name) {
			return n.properties[i].node
		}
	}
	return noNode
}

// hasNoAssertionsBesidesReferences reports a node with no keyword but references and allOf.
func hasNoAssertionsBesidesReferences(n *schemaNode) bool {
	return !(n.hasType || n.constValue.set() || n.hasEnum || n.hasNumberKeywords() || n.hasStringKeywords() ||
		n.hasObjectKeywords() || n.hasArrayKeywords() || n.dynamicRef != nil || n.hasAnyOf || n.hasOneOf ||
		n.not >= 0 || n.if_ >= 0)
}

// pureRefTarget is the single reference of a node that is nothing but $ref (or a static $dynamicRef), for elision.
func pureRefTarget(n *schemaNode) nodeID {
	if (n.ref >= 0) == (n.staticDynamicRef >= 0) || n.alwaysTrue || n.alwaysFalse {
		return noNode
	}
	if n.hasAllOf || !hasNoAssertionsBesidesReferences(n) {
		return noNode
	}
	if n.ref >= 0 {
		return n.ref
	}
	return n.staticDynamicRef
}

// forwardTarget is the branch of a node that is nothing but a one-branch allOf, for fail-fast forwarding.
func forwardTarget(n *schemaNode) nodeID {
	if len(n.allOf) != 1 || n.alwaysTrue || n.alwaysFalse || n.ref >= 0 || n.staticDynamicRef >= 0 ||
		!hasNoAssertionsBesidesReferences(n) {
		return noNode
	}
	return n.allOf[0]
}

// Messages (the C# evaluator's text).
const (
	msgEvaluatedSubschema    = "The value was expected to match the subschema."
	msgMatchedAll            = "The value matched all subschema."
	msgDidNotMatchAll        = "The value did not match all subschema."
	msgMatchedAtLeastOne     = "The value matched at least one subschema."
	msgDidNotMatchAtLeastOne = "The value did not match at least one subschema."
	msgMatchedNoSchema       = "The instance matched no schema."
	msgMatchedExactlyOne     = "The value matched exactly one subschema."
	msgMatchedMoreThanOne    = "The instance matched more than one schema."
	msgMatchedNot            = "The value matched the subschema in a not composition, which means the evaluation was not a match."
	msgDidNotMatchNot        = "The value did not match the subschema in a not composition, which means the evaluation was a match."
	msgMatchedIfForThen      = "The value matched the subschema in a binary or ternay if, which means the evaluation will go on to match the then subschema."
	msgMatchedIfForElse      = "The value did not match the subschema in a ternary if, which means the evaluation will go on to match the else subschema."
	msgMatchedThen           = "The value matched the then subschema corresponding to a binary or ternary if."
	msgDidNotMatchThen       = "The value did not match the then subschema corresponding to a binary or ternary if."
	msgMatchedElse           = "The value matched the else subschema corresponding to a ternary if."
	msgDidNotMatchElse       = "The value did not match the else subschema corresponding to a ternary if."
	msgUniqueItems           = "The array was expected to contain unique items."
	msgPropertyNameFailed    = "The property name did not match the schema."
)

func pick(condition bool, yes, no string) string {
	if condition {
		return yes
	}
	return no
}

// quoted is " 'v'", or nothing for an empty value.
func quoted(v string) string {
	if v == "" {
		return ""
	}
	return " '" + v + "'"
}

var typeOrder = [...]struct {
	mask uint8
	name string
}{
	{typeArray, "array"}, {typeObject, "object"}, {typeNull, "null"}, {typeBoolean, "boolean"},
	{typeNumber, "number"}, {typeInteger, "integer"}, {typeString, "string"},
}

func typeMessage(mask uint8) string {
	var names []string
	for _, t := range typeOrder {
		if mask&t.mask != 0 {
			names = append(names, t.name)
		}
	}
	switch len(names) {
	case 0:
		return ""
	case 1:
		return "The value was expected to be of type '" + names[0] + "'"
	}
	return "The value was expected to be of type '[\"" + strings.Join(names, "\", \"") + "\"]'"
}

func constMessage(v valueRef) string {
	switch v.d.kind(v.n) {
	case kindString:
		return "Expected the value to be the string" + quoted(string(v.d.str(v.n)))
	case kindNumber:
		return "The value was expected to be equal to" + quoted(string(v.d.numberText(v.n)))
	case kindBool:
		return "Expected the value to be '" + strconv.FormatBool(v.d.boolean(v.n)) + "'"
	case kindNull:
		return "Expected the value to be 'null'"
	}
	return ""
}

// typeOK reports whether a value is of one of the types in a mask. A kind is its type bit.
func typeOK(mask uint8, d *Document, x int) bool {
	bit := d.kind(x)
	if mask&bit != 0 {
		return true
	}
	// A number that is not accepted as a number may still be an integer.
	return bit == kindNumber && mask&typeInteger != 0 && isIntegerNumber(d.flags(x), d.data(x))
}

// codePoints is the length of a string value in code points (what minLength and maxLength count).
func codePoints(d *Document, x int) uint64 {
	if d.strASCII(x) {
		return uint64(d.count(x))
	}
	return uint64(utf8.RuneCount(d.str(x)))
}

func base64Value(c byte) int {
	switch {
	case c >= 'A' && c <= 'Z':
		return int(c - 'A')
	case c >= 'a' && c <= 'z':
		return int(c-'a') + 26
	case c >= '0' && c <= '9':
		return int(c-'0') + 52
	case c == '+':
		return 62
	case c == '/':
		return 63
	}
	return -1
}

// base64Decode decodes padded standard base64 into out (reused). Unlike encoding/base64 it accepts no line breaks.
func base64Decode(b []byte, out []byte) ([]byte, bool) {
	out = out[:0]
	if len(b)%4 != 0 {
		return out, false
	}
	for i := 0; i < len(b); i += 4 {
		chunk := b[i : i+4]
		pad := 0
		for pad < 4 && chunk[3-pad] == '=' {
			pad++
		}
		if pad > 2 || (pad > 0 && i+4 != len(b)) {
			return out, false
		}
		acc := 0
		for _, c := range chunk[:4-pad] {
			v := base64Value(c)
			if v < 0 {
				return out, false
			}
			acc = acc<<6 | v
		}
		acc <<= 6 * pad
		bytes := [3]byte{byte(acc >> 16), byte(acc >> 8), byte(acc)}
		out = append(out, bytes[:3-pad]...)
	}
	return out, true
}

// ---------------------------------------------------------------------------------------------------------------------
// Evaluated properties and items

// bitset is a set of evaluated properties (by index in the instance object) or items, held in the evaluator's
// arena. The zero offset is the arena's start, so "no set" is a negative offset.
type bitset struct {
	off   int32
	words int32
}

var noBits = bitset{off: -1}

func (b bitset) tracked() bool {
	return b.off >= 0
}

// newBits takes a cleared set for length members from the arena. Sets are released in the reverse order.
func (e *evaluator) newBits(length int) bitset {
	words := (length + 63) >> 6
	if words == 0 {
		words = 1
	}
	off := len(e.arena)
	end := off + words
	if end > cap(e.arena) {
		grown := make([]uint64, end, 2*end+16)
		copy(grown, e.arena)
		e.arena = grown
	}
	e.arena = e.arena[:end]
	clear(e.arena[off:end])
	return bitset{int32(off), int32(words)}
}

func (e *evaluator) freeBits(b bitset) {
	e.arena = e.arena[:b.off]
}

func (e *evaluator) setBit(b bitset, i int) {
	e.arena[int(b.off)+i>>6] |= 1 << (i & 63)
}

func (e *evaluator) getBit(b bitset, i int) bool {
	return e.arena[int(b.off)+i>>6]&(1<<(i&63)) != 0
}

func (e *evaluator) mergeBits(into, from bitset) {
	a := e.arena[into.off : into.off+into.words]
	for i, w := range e.arena[from.off : from.off+from.words] {
		a[i] |= w
	}
}

func (e *evaluator) clearBits(b bitset) {
	clear(e.arena[b.off : b.off+b.words])
}

// ---------------------------------------------------------------------------------------------------------------------
// The evaluator

// evaluator holds the state of one evaluation. Its buffers are reused from one evaluation to the next.
type evaluator struct {
	p *program
	// The instance document.
	d *Document
	// The collector, or nil to fail fast.
	c           *ResultsCollector
	annotations [][]annotationEntry
	// The dynamic scope: the resources entered, outermost first.
	scope         []uint32
	depth         int
	depthExceeded bool
	// The evaluated-property and evaluated-item sets in use.
	arena []uint64
	// Scratch for uniqueItems.
	unique []uint64
	// Fused passes not in use.
	passes []*fusedPass
	// For validating JSON text: the document it is parsed into, and the parser.
	text   Document
	parser parser
	// For content assertions: the decoded bytes, and the parser that checks them.
	content       []byte
	contentParser *parser
}

func newEvaluator(p *program, c *ResultsCollector) *evaluator {
	e := &evaluator{p: p, c: c}
	if c != nil {
		e.annotations = p.nodeAnnotations()
	}
	return e
}

// begin readies the evaluator for an instance document.
func (e *evaluator) begin(d *Document) {
	e.d = d
	e.scope = e.scope[:0]
	e.arena = e.arena[:0]
	e.depth = 0
	e.depthExceeded = false
}

// validate evaluates the program's entry, failing fast.
func (e *evaluator) validate(d *Document) bool {
	e.begin(d)
	return e.run(e.p.fastTarget[e.p.root], d.root)
}

// evaluate evaluates the program's entry, reporting to the collector.
func (e *evaluator) evaluate(d *Document) bool {
	e.begin(d)
	// A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
	root, _ := e.resolve(e.p.root)
	e.c.beginChildContext(false, "", e.p.nodes[root].pointer, false, "")
	ok := e.evalNode(root, d.root, noBits)
	e.c.commitChildContext(false, ok, msgEvaluatedSubschema)
	return ok
}

// wants reports whether a keyword result with the given outcome carries message text.
func (e *evaluator) wants(isMatch bool) bool {
	return e.c != nil && e.c.withText(isMatch)
}

// keyword records a keyword's result when collecting. It reports whether evaluation stops here (failing fast).
func (e *evaluator) keyword(isMatch bool, message, keyword string) bool {
	if e.c != nil {
		e.c.evaluatedKeyword(isMatch, message, keyword)
		return false
	}
	return !isMatch
}

// countKeyword records a keyword whose message ends in a quoted count.
func (e *evaluator) countKeyword(isMatch bool, prefix string, n uint64, keyword string) bool {
	if e.c == nil {
		return !isMatch
	}
	message := ""
	if e.c.withText(isMatch) {
		message = prefix + " '" + strconv.FormatUint(n, 10) + "'"
	}
	e.c.evaluatedKeyword(isMatch, message, keyword)
	return false
}

// collectPureRef is the single reference of a pure-$ref node, for collecting (a node with annotations is not
// elided).
func (e *evaluator) collectPureRef(id nodeID) nodeID {
	if int(id) < len(e.annotations) && len(e.annotations[id]) != 0 {
		return noNode
	}
	return pureRefTarget(e.p.nodes[id])
}

// resolve follows pure-reference hops (at most 16, and not across resources when a dynamic scope is kept),
// returning the target and the evaluation-path suffix of the hops (/$ref, /$dynamicRef, /$recursiveRef).
func (e *evaluator) resolve(id nodeID) (nodeID, string) {
	current := id
	suffix := ""
	for hop := 0; hop < 16; hop++ {
		n := e.p.nodes[current]
		next := e.collectPureRef(current)
		if next < 0 {
			break
		}
		if e.p.usesDynamicScope && e.p.nodes[next].resourceID != n.resourceID {
			break
		}
		if n.ref >= 0 {
			suffix += "/$ref"
		} else {
			suffix += "/" + n.staticDynamicKeyword
		}
		current = next
	}
	return current, suffix
}

func (e *evaluator) evalNode(id nodeID, x int, bits bitset) bool {
	n := e.p.nodes[id]
	if n.alwaysTrue || n.alwaysFalse {
		if e.c != nil {
			e.c.evaluatedBooleanSchema(n.alwaysTrue)
		}
		return n.alwaysTrue
	}
	pushed := e.p.usesDynamicScope && (len(e.scope) == 0 || e.scope[len(e.scope)-1] != n.resourceID)
	if pushed {
		e.scope = append(e.scope, n.resourceID)
	}
	kind := e.d.kind(x)
	var ok bool
	if !bits.tracked() && ((n.unevaluatedProperties >= 0 && kind == kindObject) ||
		(n.unevaluatedItems >= 0 && kind == kindArray)) {
		own := e.newBits(e.d.count(x))
		ok = e.evalCore(id, n, x, own)
		e.freeBits(own)
	} else {
		ok = e.evalCore(id, n, x, bits)
	}
	if pushed {
		e.scope = e.scope[:len(e.scope)-1]
	}
	return ok
}

func (e *evaluator) evalCore(id nodeID, n *schemaNode, x int, bits bitset) bool {
	d := e.d
	ok := true
	if n.hasType {
		m := typeOK(n.typeMask, d, x)
		message := ""
		if e.wants(m) {
			message = typeMessage(n.typeMask)
		}
		if e.keyword(m, message, "type") {
			return false
		}
		ok = ok && m
	}
	if n.constValue.set() {
		m := valuesEqual(d, x, n.constValue.d, n.constValue.n)
		message := ""
		if e.wants(m) {
			message = constMessage(n.constValue)
		}
		if e.keyword(m, message, "const") {
			return false
		}
		ok = ok && m
	}
	if n.hasEnum {
		m := false
		for _, v := range n.enumValues {
			if valuesEqual(d, x, v.d, v.n) {
				m = true
				break
			}
		}
		if e.keyword(m, pick(m, msgMatchedAtLeastOne, msgDidNotMatchAtLeastOne), "enum") {
			return false
		}
		ok = ok && m
	}
	kind := d.kind(x)
	m := true
	switch {
	case kind == kindNumber && n.hasNumberKeywords():
		m = e.evalNumber(n, x)
	case kind == kindString && n.hasStringKeywords():
		m = e.evalString(n, x)
	case kind == kindObject && n.hasObjectKeywords():
		m = e.evalObject(id, n, x, bits)
	case kind == kindArray && n.hasArrayKeywords():
		m = e.evalArray(n, x, bits)
	}
	if !m && e.c == nil {
		return false
	}
	ok = ok && m
	m = e.evalInPlace(n, x, bits)
	if !m && e.c == nil {
		return false
	}
	ok = ok && m
	switch {
	case kind == kindObject && n.unevaluatedProperties >= 0:
		m = e.evalUnevaluatedProperties(n, x, bits)
	case kind == kindArray && n.unevaluatedItems >= 0:
		m = e.evalUnevaluatedItems(n, x, bits)
	}
	if !m && e.c == nil {
		return false
	}
	ok = ok && m
	if e.c != nil {
		for _, a := range e.annotations[id] {
			if a.stringsOnly && kind != kindString {
				continue
			}
			e.c.ignoredKeyword(a.value, a.keyword)
		}
	}
	return ok
}

// ---------------------------------------------------------------------------------------------------------------------
// Numbers and strings

func (e *evaluator) evalNumber(n *schemaNode, x int) bool {
	d := e.d
	ok := true
	if n.assertFormat && n.formatKind.isNumeric() && n.hasFormat {
		var m bool
		if custom, found := e.p.formats[n.format]; found {
			m = custom(string(d.numberText(x)))
		} else {
			m = n.formatKind.checkNumber(d, x)
		}
		message := ""
		if e.wants(m) {
			message = "The value was expected to be in a supported format, and within bounds for '" +
				n.formatKind.name() + "'"
		}
		if e.keyword(m, message, "format") {
			return false
		}
		ok = ok && m
	}
	bound := func(b valueRef, m bool, text, keyword string) bool {
		message := ""
		if e.wants(m) {
			message = text + quoted(string(b.d.numberText(b.n)))
		}
		return e.keyword(m, message, keyword)
	}
	compare := func(b valueRef) int {
		return compareNumbers(d.flags(x), d.data(x), b.d.flags(b.n), b.d.data(b.n))
	}
	if b := n.minimum; b.set() {
		m := compare(b) >= 0
		if bound(b, m, "The value was expected to be greater than or equal to", "minimum") {
			return false
		}
		ok = ok && m
	}
	if b := n.maximum; b.set() {
		m := compare(b) <= 0
		if bound(b, m, "The value was expected to be less than or equal to", "maximum") {
			return false
		}
		ok = ok && m
	}
	if b := n.exclusiveMinimum; b.set() {
		m := compare(b) > 0
		if bound(b, m, "The value was expected to be greater than", "exclusiveMinimum") {
			return false
		}
		ok = ok && m
	}
	if b := n.exclusiveMaximum; b.set() {
		m := compare(b) < 0
		if bound(b, m, "The value was expected to be less than", "exclusiveMaximum") {
			return false
		}
		ok = ok && m
	}
	if b := n.multipleOf; b.set() {
		m := n.divisor.divides(d, x)
		if bound(b, m, "The value was expected to be a multiple of", "multipleOf") {
			return false
		}
		ok = ok && m
	}
	return ok
}

func (e *evaluator) evalString(n *schemaNode, x int) bool {
	d := e.d
	ok := true
	if n.minLength.set || n.maxLength.set {
		length := codePoints(d, x)
		if n.minLength.set {
			m := length >= n.minLength.n
			if e.countKeyword(m, "Expected the length of the value to be greater than or equal to", n.minLength.n,
				"minLength") {
				return false
			}
			ok = ok && m
		}
		if n.maxLength.set {
			m := length <= n.maxLength.n
			if e.countKeyword(m, "Expected the length of the value to be less than or equal to", n.maxLength.n,
				"maxLength") {
				return false
			}
			ok = ok && m
		}
	}
	if p := n.pattern; p != nil {
		m := p.match(d.str(x))
		message := ""
		if e.wants(m) {
			message = "Expected the value to match the regular expression" + quoted(p.source)
		}
		if e.keyword(m, message, "pattern") {
			return false
		}
		ok = ok && m
	}
	if n.assertFormat && !n.formatKind.isNumeric() && n.hasFormat {
		m, message := true, ""
		if custom, found := e.p.formats[n.format]; found {
			m = custom(string(d.str(x)))
			if e.wants(m) {
				message = "Expected a string in the '" + n.format + "' format."
			}
		} else if n.formatKind != formatUnknown {
			m = n.formatKind.checkString(d.str(x), n.dialect <= Draft6)
			message = n.formatKind.message()
		}
		if e.keyword(m, message, "format") {
			return false
		}
		ok = ok && m
	}
	if n.assertContent {
		m := e.contentOK(d.str(x), n.content)
		message, keyword := "Expected valid Base64-encoded JSON content.", "contentMediaType"
		switch n.content {
		case contentBase64:
			message, keyword = "Expected a valid Base64-encoded string.", "contentEncoding"
		case contentJSON:
			message = "Expected valid JSON content."
		}
		if e.keyword(m, message, keyword) {
			return false
		}
		ok = ok && m
	}
	return ok
}

// contentOK is the draft 7 content assertion.
func (e *evaluator) contentOK(s []byte, kind contentKind) bool {
	if kind == contentNone {
		return true
	}
	if kind != contentJSON {
		decoded, ok := base64Decode(s, e.content)
		e.content = decoded
		if !ok || kind == contentBase64 {
			return ok
		}
		s = decoded
	}
	if e.contentParser == nil {
		e.contentParser = new(parser)
	}
	return e.contentParser.isValid(s)
}

// ---------------------------------------------------------------------------------------------------------------------
// Objects

// evalAt applies a child at a new instance location (a property value or an array item), when collecting.
func (e *evaluator) evalAt(child nodeID, path string, value int, docSegment string) bool {
	target, suffix := e.resolve(child)
	e.c.beginChildContext(true, path+suffix, e.p.nodes[target].pointer, true, docSegment)
	ok := e.evalNode(target, value, noBits)
	e.c.commitChildContext(ok, ok, msgEvaluatedSubschema)
	return ok
}

// requiredProperty records whether a required property is present. It reports whether evaluation stops here.
func (e *evaluator) requiredProperty(present bool, name, keyword string) bool {
	if e.c == nil {
		return !present
	}
	message := ""
	if e.c.withText(present) {
		message = "Required property " + pick(present, "", "not ") + "present '" + name + "'"
	}
	e.c.evaluatedKeywordForProperty(present, message, name, keyword)
	return false
}

func (e *evaluator) evalObject(id nodeID, n *schemaNode, x int, bits bitset) bool {
	d := e.d
	collect := e.c != nil
	ok := true
	count := d.count(x)
	if n.minProperties.set {
		m := uint64(count) >= n.minProperties.n
		if e.countKeyword(m, "Expected the property count to be greater than or equal to", n.minProperties.n,
			"minProperties") {
			return false
		}
		ok = ok && m
	}
	if n.maxProperties.set {
		m := uint64(count) <= n.maxProperties.n
		if e.countKeyword(m, "Expected the property count to be less than or equal to", n.maxProperties.n,
			"maxProperties") {
			return false
		}
		ok = ok && m
	}
	first := d.first(x)
	if n.hasProperties || n.hasPatternProperties || n.additionalProperties >= 0 || n.propertyNames >= 0 {
		for i := 0; i < count; i++ {
			k, v := first+2*i, first+2*i+1
			name := d.str(k)
			matched := false
			segment := ""
			if collect {
				segment = escapePointerToken(string(name))
			}
			// at applies a child to the property's value.
			at := func(child nodeID, keyword, entry string) bool {
				if bits.tracked() {
					e.setBit(bits, i)
				}
				if !collect {
					return e.run(e.p.fastTarget[child], v)
				}
				path := keyword
				if entry != "" || keyword != "additionalProperties" {
					path = keyword + "/" + entry
				}
				return e.evalAt(child, path, v, segment)
			}
			if p := e.p.property(id, n, name); p >= 0 {
				matched = true
				if !at(p, "properties", segment) {
					if !collect {
						return false
					}
					ok = false
				}
			}
			for j := range n.patternProperties {
				pp := &n.patternProperties[j]
				if !pp.pattern.match(name) {
					continue
				}
				matched = true
				entry := ""
				if collect {
					entry = escapePointerToken(pp.pattern.source)
				}
				if !at(pp.node, "patternProperties", entry) {
					if !collect {
						return false
					}
					ok = false
				}
			}
			if n.additionalProperties >= 0 && !matched {
				if !at(n.additionalProperties, "additionalProperties", "") {
					if !collect {
						return false
					}
					ok = false
				}
			}
			if pn := n.propertyNames; pn >= 0 {
				// The name is a string value of the document: it is evaluated where it is.
				if collect {
					// Not elided. The document path stays the object's.
					e.c.beginChildContext(true, "propertyNames", e.p.nodes[pn].pointer, false, "")
					m := e.evalNode(pn, k, noBits)
					e.c.commitChildContext(m, m, msgEvaluatedSubschema)
					if !m {
						e.c.evaluatedKeyword(false, msgPropertyNameFailed, "propertyNames")
						ok = false
					}
				} else if !e.run(e.p.fastTarget[pn], k) {
					return false
				}
			}
		}
	}
	for _, r := range n.requiredList {
		present := d.property(x, r) >= 0
		if e.requiredProperty(present, r, "required") {
			return false
		}
		ok = ok && present
	}
	// Rows are reported under the keyword the schema used (dependencies, dependentRequired, dependentSchemas).
	for i := range n.dependencies {
		dep := &n.dependencies[i]
		if d.property(x, dep.name) < 0 {
			continue
		}
		for _, r := range dep.required {
			present := d.property(x, r) >= 0
			if e.requiredProperty(present, r, dep.keyword) {
				return false
			}
			ok = ok && present
		}
		if dep.schema >= 0 {
			path := ""
			if collect {
				path = dep.keyword + "/" + escapePointerToken(dep.name)
			}
			m := e.evalInPlaceChild(dep.schema, path, x, bits, true, true)
			if !collect {
				if !m {
					return false
				}
				continue
			}
			message := ""
			if e.c.withText(m) {
				message = "The value did match the schema applied because it contained the property '" + dep.name + "'"
			}
			e.c.evaluatedKeywordForProperty(m, message, dep.name, dep.keyword)
			ok = ok && m
		}
	}
	return ok
}

func (e *evaluator) evalUnevaluatedProperties(n *schemaNode, x int, bits bitset) bool {
	d := e.d
	child := n.unevaluatedProperties
	ok := true
	first := d.first(x)
	for i := 0; i < d.count(x); i++ {
		if e.getBit(bits, i) {
			continue
		}
		e.setBit(bits, i)
		v := first + 2*i + 1
		if e.c == nil {
			if !e.run(e.p.fastTarget[child], v) {
				return false
			}
		} else if !e.evalAt(child, "unevaluatedProperties", v, escapePointerToken(string(d.str(first+2*i)))) {
			ok = false
		}
	}
	if e.c != nil {
		e.c.evaluatedKeyword(ok, "", "unevaluatedProperties")
	}
	return ok
}

// ---------------------------------------------------------------------------------------------------------------------
// Arrays

func (e *evaluator) evalArray(n *schemaNode, x int, bits bitset) bool {
	d := e.d
	collect := e.c != nil
	ok := true
	count := d.count(x)
	if n.minItems.set {
		m := uint64(count) >= n.minItems.n
		if e.countKeyword(m, "Expected the item count to be greater than or equal to", n.minItems.n, "minItems") {
			return false
		}
		ok = ok && m
	}
	if n.maxItems.set {
		m := uint64(count) <= n.maxItems.n
		if e.countKeyword(m, "Expected the item count to be less than or equal to", n.maxItems.n, "maxItems") {
			return false
		}
		ok = ok && m
	}
	if !n.hasPrefixItems && n.items < 0 && n.contains < 0 && !n.uniqueItems {
		return ok
	}
	matches := uint64(0)
	first := d.first(x)
	for i := 0; i < count; i++ {
		item := first + i
		child, path := noNode, ""
		if i < len(n.prefixItems) {
			child = n.prefixItems[i]
			if collect {
				path = n.prefixKeyword + "/" + strconv.Itoa(i)
			}
		} else if n.items >= 0 {
			child, path = n.items, n.itemsKeyword
		}
		if child >= 0 {
			if bits.tracked() {
				e.setBit(bits, i)
			}
			if !collect {
				if !e.run(e.p.fastTarget[child], item) {
					return false
				}
			} else if !e.evalAt(child, path, item, strconv.Itoa(i)) {
				ok = false
			}
		}
		if n.contains >= 0 {
			var matched bool
			if collect {
				target, suffix := e.resolve(n.contains)
				e.c.beginChildContext(true, "contains"+suffix, e.p.nodes[target].pointer, true, strconv.Itoa(i))
				if matched = e.evalNode(target, item, noBits); matched {
					e.c.commitChildContext(true, true, msgEvaluatedSubschema)
				} else {
					e.c.popChildContext()
				}
			} else {
				matched = e.run(e.p.fastTarget[n.contains], item)
			}
			if matched {
				matches++
				if n.containsMarksEvaluated && bits.tracked() {
					e.setBit(bits, i)
				}
			}
		}
	}
	if n.uniqueItems {
		m := allUnique(d, x, &e.unique)
		if e.keyword(m, msgUniqueItems, "uniqueItems") {
			return false
		}
		ok = ok && m
	}
	if n.contains >= 0 {
		over := n.maxContains.set && matches > n.maxContains.n
		m := matches >= n.minContains && !over
		var stop bool
		if over {
			stop = e.countKeyword(m, "Expected the contains count to be less than or equal to", n.maxContains.n,
				"contains")
		} else {
			stop = e.countKeyword(m, "Expected the contains count to be greater than or equal to", n.minContains,
				"contains")
		}
		if stop {
			return false
		}
		ok = ok && m
	}
	return ok
}

func (e *evaluator) evalUnevaluatedItems(n *schemaNode, x int, bits bitset) bool {
	d := e.d
	child := n.unevaluatedItems
	ok := true
	first := d.first(x)
	for i := 0; i < d.count(x); i++ {
		if e.getBit(bits, i) {
			continue
		}
		e.setBit(bits, i)
		if e.c == nil {
			if !e.run(e.p.fastTarget[child], first+i) {
				return false
			}
		} else if !e.evalAt(child, "unevaluatedItems", first+i, strconv.Itoa(i)) {
			ok = false
		}
	}
	if e.c != nil {
		e.c.evaluatedKeyword(ok, "", "unevaluatedItems")
	}
	return ok
}

// ---------------------------------------------------------------------------------------------------------------------
// In-place applicators

func (e *evaluator) canMark(id nodeID, x int) bool {
	n := e.p.nodes[id]
	if e.d.kind(x) == kindObject {
		return n.marksProperties
	}
	return n.marksItems
}

// evalInPlaceChild evaluates an in-place child: a new context at the same instance location, on a fresh scratch set
// of evaluated properties or items merged into the parent's on success. A failing child is committed or popped.
func (e *evaluator) evalInPlaceChild(child nodeID, path string, x int, bits bitset, commitOnFailure, elide bool) bool {
	target, suffix := child, ""
	if elide {
		if e.c != nil {
			target, suffix = e.resolve(child)
		} else {
			target = e.p.fastTarget[child]
		}
	}
	scratch := noBits
	if bits.tracked() && e.canMark(child, x) {
		scratch = e.newBits(e.d.count(x))
	}
	guarded := e.p.nodes[target].inPlaceCycle
	if guarded {
		e.depth++
		if e.depth > e.p.maxDepth {
			e.depthExceeded = true
			e.depth--
			if scratch.tracked() {
				e.freeBits(scratch)
			}
			return false
		}
	}
	var ok bool
	switch {
	case e.c != nil:
		e.c.beginChildContext(true, path+suffix, e.p.nodes[target].pointer, false, "")
		ok = e.evalNode(target, x, scratch)
		if ok || commitOnFailure {
			e.c.commitChildContext(ok, ok, msgEvaluatedSubschema)
		} else {
			e.c.popChildContext()
		}
	case scratch.tracked():
		ok = e.evalNode(target, x, scratch)
	default:
		ok = e.run(target, x)
	}
	if guarded {
		e.depth--
	}
	if scratch.tracked() {
		if ok {
			e.mergeBits(bits, scratch)
		}
		e.freeBits(scratch)
	}
	return ok
}

func (e *evaluator) resolveDynamic(d *dynamicRefTarget) nodeID {
	for _, resource := range e.scope {
		for i := range d.byResource {
			if d.byResource[i].resource == resource {
				return d.byResource[i].node
			}
		}
	}
	return d.fallback
}

// selectBranches is the oneOf/anyOf branches a discriminator leaves as candidates for an instance: all of them
// (all), or the listed ones.
func (e *evaluator) selectBranches(disc *discriminator, x int) (all bool, subset []uint32) {
	d := e.d
	if disc == nil || d.kind(x) != kindObject {
		return true, nil
	}
	value := d.property(x, disc.property)
	if value < 0 {
		return !disc.allRequire, nil
	}
	for i := range disc.known {
		if disc.known[i].value.matches(d, value) {
			return false, disc.known[i].branches
		}
	}
	return false, disc.unknown
}

func (e *evaluator) evalInPlace(n *schemaNode, x int, bits bitset) bool {
	collect := e.c != nil
	ok := true
	if n.ref >= 0 {
		m := e.evalInPlaceChild(n.ref, "$ref", x, bits, true, true)
		if e.keyword(m, pick(m, msgMatchedAll, msgDidNotMatchAll), "$ref") {
			return false
		}
		ok = ok && m
	}
	if n.staticDynamicRef >= 0 {
		keyword := n.staticDynamicKeyword
		m := e.evalInPlaceChild(n.staticDynamicRef, keyword, x, bits, true, true)
		if e.keyword(m, pick(m, msgMatchedAll, msgDidNotMatchAll), keyword) {
			return false
		}
		ok = ok && m
	}
	if dr := n.dynamicRef; dr != nil {
		keyword := dynamicKeyword(dr.isRecursive)
		// The resolved target is elided, with no hops in the path.
		target := e.resolveDynamic(dr)
		if collect {
			target, _ = e.resolve(target)
		} else {
			target = e.p.fastTarget[target]
		}
		m := e.evalInPlaceChild(target, keyword, x, bits, true, false)
		if e.keyword(m, pick(m, msgMatchedAll, msgDidNotMatchAll), keyword) {
			return false
		}
		ok = ok && m
	}
	if n.hasAllOf {
		all := true
		for i, b := range n.allOf {
			path := ""
			if collect {
				path = "allOf/" + strconv.Itoa(i)
			}
			if !e.evalInPlaceChild(b, path, x, bits, true, true) {
				if !collect {
					return false
				}
				all = false
			}
		}
		if e.keyword(all, pick(all, msgMatchedAll, msgDidNotMatchAll), "allOf") {
			return false
		}
		ok = ok && all
	}
	if n.hasAnyOf {
		any := false
		// Every branch runs when results are collected or evaluated properties or items are tracked.
		exhaustive := collect || bits.tracked()
		// Fail-fast evaluation only tries the branches a discriminator property can select.
		all, subset := true, []uint32(nil)
		if !exhaustive {
			all, subset = e.selectBranches(n.anyOfDiscriminator, x)
		}
		count := len(subset)
		if all {
			count = len(n.anyOf)
		}
		for j := 0; j < count; j++ {
			i := j
			if !all {
				i = int(subset[j])
			}
			path := ""
			if collect {
				path = "anyOf/" + strconv.Itoa(i)
			}
			if e.evalInPlaceChild(n.anyOf[i], path, x, bits, false, true) {
				any = true
				if !exhaustive {
					break
				}
			}
		}
		if e.keyword(any, pick(any, msgMatchedAtLeastOne, msgDidNotMatchAtLeastOne), "anyOf") {
			return false
		}
		ok = ok && any
	}
	if n.hasOneOf {
		matched := 0
		track := bits.tracked()
		// Evaluated properties or items are merged only when exactly one branch matched, so they are collected
		// aside, and the matching branch's kept.
		aside, only := noBits, noBits
		if track {
			only = e.newBits(e.d.count(x))
			aside = e.newBits(e.d.count(x))
		}
		// Branches a discriminator rules out cannot match, so fail-fast evaluation skips them.
		all, subset := true, []uint32(nil)
		if !collect && !track {
			all, subset = e.selectBranches(n.oneOfDiscriminator, x)
		}
		count := len(subset)
		if all {
			count = len(n.oneOf)
		}
		for j := 0; j < count; j++ {
			i := j
			if !all {
				i = int(subset[j])
			}
			path := ""
			if collect {
				path = "oneOf/" + strconv.Itoa(i)
			}
			if track {
				e.clearBits(aside)
			}
			if e.evalInPlaceChild(n.oneOf[i], path, x, aside, false, true) {
				matched++
				if track {
					copy(e.arena[only.off:only.off+only.words], e.arena[aside.off:aside.off+aside.words])
				}
				if !collect && matched > 1 {
					break
				}
			}
		}
		if track {
			if matched == 1 {
				e.mergeBits(bits, only)
			}
			e.freeBits(only)
		}
		message := msgMatchedMoreThanOne
		switch matched {
		case 0:
			message = msgMatchedNoSchema
		case 1:
			message = msgMatchedExactlyOne
		}
		if e.keyword(matched == 1, message, "oneOf") {
			return false
		}
		ok = ok && matched == 1
	}
	if n.not >= 0 {
		// Not elided, never contributes results or evaluated properties or items.
		var inner bool
		if collect {
			e.c.beginChildContext(true, "not", e.p.nodes[n.not].pointer, false, "")
			inner = e.evalNode(n.not, x, noBits)
			e.c.popChildContext()
		} else {
			inner = e.run(e.p.fastTarget[n.not], x)
		}
		if e.keyword(!inner, pick(inner, msgMatchedNot, msgDidNotMatchNot), "not") {
			return false
		}
		ok = ok && !inner
	}
	if n.if_ >= 0 {
		cond := e.evalInPlaceChild(n.if_, "if", x, bits, false, true)
		if collect {
			e.c.evaluatedKeyword(true, pick(cond, msgMatchedIfForThen, msgMatchedIfForElse), "if")
		}
		if cond {
			if n.then >= 0 {
				m := e.evalInPlaceChild(n.then, "then", x, bits, true, true)
				if e.keyword(m, pick(m, msgMatchedThen, msgDidNotMatchThen), "then") {
					return false
				}
				ok = ok && m
			}
		} else if n.else_ >= 0 {
			m := e.evalInPlaceChild(n.else_, "else", x, bits, true, true)
			if e.keyword(m, pick(m, msgMatchedElse, msgDidNotMatchElse), "else") {
				return false
			}
			ok = ok && m
		}
	}
	return ok
}
