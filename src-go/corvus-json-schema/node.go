package jsonschema

// The compiled schema graph: one schemaNode per distinct (document, pointer), with keyword data digested in advance.

// nodeID is a node index in the compiled graph, or noNode.
type nodeID = int32

const noNode nodeID = -1

// JSON types as bits. The first six are the kinds of a document's values.
const (
	typeNull    = kindNull
	typeBoolean = kindBool
	typeObject  = kindObject
	typeArray   = kindArray
	typeNumber  = kindNumber
	typeString  = kindString
	typeInteger = 64
)

type contentKind uint8

const (
	contentNone contentKind = iota
	contentBase64
	contentJSON
	contentBase64JSON
)

// valueRef is a value in a schema document (a constant, a bound).
type valueRef struct {
	d *Document
	n int
}

func (v valueRef) set() bool {
	return v.d != nil
}

// optCount is an optional non-negative integer keyword value.
type optCount struct {
	set bool
	n   uint64
}

// annotationEntry is an annotation-producing keyword and its value (reported in verbose results).
type annotationEntry struct {
	keyword string
	// The value as JSON text.
	value string
	// Reported only when the instance is a string (content keywords).
	stringsOnly bool
}

type namedNode struct {
	name string
	node nodeID
}

type patternProperty struct {
	pattern *pattern
	node    nodeID
}

// The keyword a dependency entry came from, which names its result rows.
const (
	keywordDependencies      = "dependencies"
	keywordDependentSchemas  = "dependentSchemas"
	keywordDependentRequired = "dependentRequired"
)

type dependencyEntry struct {
	keyword     string
	name        string
	hasRequired bool
	required    []string
	schema      nodeID
}

type resourceNode struct {
	resource uint32
	node     nodeID
}

// dynamicRefTarget is a $dynamicRef/$recursiveRef that stays dynamic after compile-time analysis.
type dynamicRefTarget struct {
	isRecursive bool
	fallback    nodeID
	// The node of the matching anchor in each resource that defines it.
	byResource []resourceNode
}

// discriminatorValue is a discriminator value: one of the JSON scalars const and enum can name.
type discriminatorValue struct {
	kind uint8
	// A string's text.
	text string
	// A number, or a boolean.
	value valueRef
}

// matches reports whether an instance value equals this one (numbers by value: 1 and 1.0 are the same key).
func (v *discriminatorValue) matches(d *Document, x int) bool {
	if d.kind(x) != v.kind {
		return false
	}
	switch v.kind {
	case kindString:
		return d.count(x) == len(v.text) && string(d.str(x)) == v.text
	case kindNull:
		return true
	}
	return valuesEqual(d, x, v.value.d, v.value.n)
}

// same is key equality, with numbers by value.
func (v *discriminatorValue) same(other *discriminatorValue) bool {
	if v.kind != other.kind {
		return false
	}
	switch v.kind {
	case kindString:
		return v.text == other.text
	case kindNull:
		return true
	}
	return valuesEqual(v.value.d, v.value.n, other.value.d, other.value.n)
}

type discriminatorEntry struct {
	value discriminatorValue
	// The branches (indexes into the keyword's list) the value can select.
	branches []uint32
}

// discriminator selects oneOf/anyOf branches by the value of one property.
type discriminator struct {
	property string
	// Known discriminator values and the branches each can select.
	known []discriminatorEntry
	// Branches that stay candidates for a value not in known (negative and wildcard branches).
	unknown []uint32
	// Every branch requires the property, so its absence fails the keyword at once.
	allRequire bool
}

type schemaNode struct {
	resourceID uint32
	dialect    Dialect
	// The JSON pointer of the schema within its document.
	pointer string

	alwaysTrue  bool
	alwaysFalse bool

	// Assertions.
	typeMask   uint8
	hasType    bool
	constValue valueRef
	hasEnum    bool
	enumValues []valueRef

	// References.
	ref nodeID
	// A $dynamicRef/$recursiveRef that compile-time analysis resolved statically, and its keyword.
	staticDynamicRef     nodeID
	staticDynamicKeyword string
	dynamicRef           *dynamicRefTarget

	// In-place applicators.
	hasAllOf, hasAnyOf, hasOneOf bool
	allOf, anyOf, oneOf          []nodeID
	not, if_, then, else_        nodeID

	// Objects.
	hasProperties         bool
	properties            []namedNode
	hasPatternProperties  bool
	patternProperties     []patternProperty
	additionalProperties  nodeID
	propertyNames         nodeID
	hasRequired           bool
	required              []string // without duplicates
	requiredList          []string // as written (duplicates kept), for results
	hasDependencies       bool
	dependencies          []dependencyEntry
	minProperties         optCount
	maxProperties         optCount
	unevaluatedProperties nodeID

	// Arrays.
	hasPrefixItems bool
	prefixItems    []nodeID
	// The keywords behind prefixItems/items: prefixItems/items (2020-12) or items/additionalItems (legacy).
	prefixKeyword          string
	itemsKeyword           string
	items                  nodeID
	contains               nodeID
	minContains            uint64
	maxContains            optCount
	containsMarksEvaluated bool
	minItems               optCount
	maxItems               optCount
	uniqueItems            bool
	unevaluatedItems       nodeID

	// Strings.
	minLength     optCount
	maxLength     optCount
	pattern       *pattern
	hasFormat     bool
	format        string
	formatKind    formatKind
	assertFormat  bool
	content       contentKind
	assertContent bool

	// Numbers.
	minimum, maximum, exclusiveMinimum, exclusiveMaximum, multipleOf valueRef
	// The multipleOf divisor, digested.
	divisor *divisor

	// Analysis.
	marksProperties    bool
	marksItems         bool
	inPlaceCycle       bool
	oneOfDiscriminator *discriminator
	anyOfDiscriminator *discriminator
}

func newSchemaNode(resource uint32, dialect Dialect, pointer string) *schemaNode {
	return &schemaNode{
		resourceID: resource, dialect: dialect, pointer: pointer,
		ref: noNode, staticDynamicRef: noNode, staticDynamicKeyword: "$dynamicRef",
		not: noNode, if_: noNode, then: noNode, else_: noNode,
		additionalProperties: noNode, propertyNames: noNode, unevaluatedProperties: noNode,
		prefixKeyword: "prefixItems", itemsKeyword: "items",
		items: noNode, contains: noNode, minContains: 1, unevaluatedItems: noNode,
	}
}

// hasObjectKeywords reports keywords that apply only to objects.
func (n *schemaNode) hasObjectKeywords() bool {
	return n.hasProperties || n.hasPatternProperties || n.additionalProperties >= 0 || n.propertyNames >= 0 ||
		n.hasRequired || n.hasDependencies || n.minProperties.set || n.maxProperties.set ||
		n.unevaluatedProperties >= 0
}

func (n *schemaNode) hasArrayKeywords() bool {
	return n.hasPrefixItems || n.items >= 0 || n.contains >= 0 || n.minItems.set || n.maxItems.set ||
		n.uniqueItems || n.unevaluatedItems >= 0
}

func (n *schemaNode) hasStringKeywords() bool {
	return n.minLength.set || n.maxLength.set || n.pattern != nil ||
		(n.assertFormat && n.hasFormat && !n.formatKind.isNumeric()) || n.assertContent
}

func (n *schemaNode) hasNumberKeywords() bool {
	return n.minimum.set() || n.maximum.set() || n.exclusiveMinimum.set() || n.exclusiveMaximum.set() ||
		n.multipleOf.set() || (n.assertFormat && n.hasFormat && n.formatKind.isNumeric())
}

func (n *schemaNode) hasDependencySchema() bool {
	for i := range n.dependencies {
		if n.dependencies[i].schema >= 0 {
			return true
		}
	}
	return false
}

func (n *schemaNode) hasInPlaceApplicators() bool {
	return n.ref >= 0 || n.staticDynamicRef >= 0 || n.dynamicRef != nil || n.hasAllOf || n.hasAnyOf || n.hasOneOf ||
		n.not >= 0 || n.if_ >= 0 || n.hasDependencySchema()
}

// isPureRef reports a node that is nothing but $ref: no other keyword that asserts.
func (n *schemaNode) isPureRef() bool {
	return n.ref >= 0 && !n.hasType && !n.constValue.set() && !n.hasEnum && !n.hasObjectKeywords() &&
		!n.hasArrayKeywords() && !n.hasStringKeywords() && !n.hasNumberKeywords() && n.dynamicRef == nil &&
		n.staticDynamicRef < 0 && !n.hasAllOf && !n.hasAnyOf && !n.hasOneOf && n.not < 0 && n.if_ < 0 &&
		!n.hasDependencies
}

func appendNode(out []nodeID, ids ...nodeID) []nodeID {
	for _, id := range ids {
		if id >= 0 {
			out = append(out, id)
		}
	}
	return out
}

// inPlaceChildren are the in-place children (the instance is evaluated at the same location).
func (n *schemaNode) inPlaceChildren(includeNot bool) []nodeID {
	out := appendNode(nil, n.ref, n.staticDynamicRef)
	if d := n.dynamicRef; d != nil {
		out = append(out, d.fallback)
		for _, r := range d.byResource {
			out = append(out, r.node)
		}
	}
	out = append(out, n.allOf...)
	out = append(out, n.anyOf...)
	out = append(out, n.oneOf...)
	if includeNot {
		out = appendNode(out, n.not)
	}
	out = appendNode(out, n.if_, n.then, n.else_)
	for i := range n.dependencies {
		out = appendNode(out, n.dependencies[i].schema)
	}
	return out
}

// children are every child node.
func (n *schemaNode) children() []nodeID {
	out := n.inPlaceChildren(true)
	for _, p := range n.properties {
		out = append(out, p.node)
	}
	for _, p := range n.patternProperties {
		out = append(out, p.node)
	}
	out = appendNode(out, n.additionalProperties, n.propertyNames, n.unevaluatedProperties, n.items, n.contains,
		n.unevaluatedItems)
	return append(out, n.prefixItems...)
}
