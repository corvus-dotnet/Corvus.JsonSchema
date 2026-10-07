package jsonschema

import (
	"math"
	"math/bits"
)

// Fail-fast evaluation plans: each node compiled to only the keywords it has, as a flat list of operations, with
// the object keywords fused into one pass over the instance's properties (declared names resolved through a lookup
// table, required checked as a bit mask of the declared properties seen) and children that are nothing but a type
// check tested inline.
//
// A node that tracks evaluated properties or items (unevaluatedProperties, unevaluatedItems) runs through the
// general evaluator, whose children come back to their plans.

// anyType is every JSON type (a node without type).
const anyType uint8 = 0x7f

// child is a child application, with its type check hoisted so that a child that is only a type check needs no
// call.
type child struct {
	id nodeID
	// The types the child accepts (0 for false).
	types uint8
	// What the child's keywords come to, so that entering it skips the dispatch it does not need.
	shape shape
}

// noChild is a child that accepts anything and is never entered (an undeclared name without additionalProperties).
var noChild = child{id: noNode, types: anyType, shape: shapeTrivial}

// shape is the shape of a node's plan, as its callers enter it.
type shape uint8

const (
	// shapeGeneral is anything else, or on an in-place cycle (entered through the guarded path).
	shapeGeneral shape = iota
	// shapeTrivial has nothing to check beyond the types.
	shapeTrivial
	// shapeLeaf checks only its own value (const, enum, number and string keywords): no children, no calls.
	shapeLeaf
	// shapeStringEnum is only an enum of strings.
	shapeStringEnum
	// shapeStrings is only string keywords (length, pattern, format).
	shapeStrings
	// shapeObject is nothing but an object plan: an object value enters its loop directly.
	shapeObject
	// shapeArray is nothing but an array plan: an array value enters its loop directly.
	shapeArray
	// shapeApply is nothing but in-place applicators: straight to them.
	shapeApply
)

type plan struct {
	types uint8
	// Evaluated in place under the depth guard (part of an in-place cycle).
	guard bool
	// Everything beyond the type check (nil for true, false and type-only schemas).
	body *body
	// The body's shape, for entering it directly.
	shape shape
}

// body is a node's keywords, grouped so that only the ones for the instance's type are looked at.
type body struct {
	// For an object instance, the whole node in one pass (see fused.go), in place of everything below.
	fused *fusedObject
	// Instance kinds (typeObject, typeArray) whose evaluated properties or items the general evaluator must track
	// (unevaluatedProperties, unevaluatedItems), and the node it runs.
	general uint8
	node    nodeID
	// unevaluatedItems with a static coverage: the items from this index on, against the child.
	hasUnevaluatedItems bool
	unevaluatedFrom     int
	unevaluatedChild    child
	// const and enum.
	values []op
	number []numberOp
	str    []stringOp
	object *objectPlan
	array  *arrayPlan
	// In-place applicators (children resolved through pure-$ref hops), in the general evaluator's order.
	apply []op
}

type numberOpKind uint8

const (
	numberMinimum numberOpKind = iota
	numberMaximum
	numberExclusiveMinimum
	numberExclusiveMaximum
	numberMultipleOf
	numberFormat
)

type numberOp struct {
	kind numberOpKind
	// The bound's representation.
	flag    uint8
	data    uint64
	divisor *divisor
	format  formatCheck
}

type stringOpKind uint8

const (
	stringLength stringOpKind = iota
	stringPattern
	stringFormat
	stringContent
)

type stringOp struct {
	kind     stringOpKind
	min, max uint64
	pattern  *pattern
	format   formatCheck
	content  contentKind
}

type opKind uint8

const (
	opConst opKind = iota
	// opEnumStrings is an enum of strings only.
	opEnumStrings
	opEnum
	opRef
	opAllOf
	opAnyOf
	opOneOf
	opNot
	// opDynamicRef is a $dynamicRef/$recursiveRef resolved against the dynamic scope at run time.
	opDynamicRef
	opIf
)

type op struct {
	kind     opKind
	value    valueRef
	names    *names
	values   []valueRef
	node     nodeID
	children []child
	branches *branches
	dynamic  *dynamicRefTarget
	then     nodeID
	else_    nodeID
}

type formatCheck struct {
	custom FormatValidator
	kind   formatKind
	legacy bool
}

func (f *formatCheck) checkString(b []byte) bool {
	if f.custom != nil {
		return f.custom(string(b))
	}
	return f.kind.checkString(b, f.legacy)
}

func (f *formatCheck) checkNumber(d *Document, x int) bool {
	if f.custom != nil {
		return f.custom(string(d.numberText(x)))
	}
	return f.kind.checkNumber(d, x)
}

type objectPlan struct {
	min, max uint64
	// How the properties are visited (for properties, patternProperties, additionalProperties, propertyNames).
	visit visit
	// The declared names, then the names only required and dependencies mention (so that the pass sees them too):
	// the first declared have a schema.
	names    *names
	declared int
	// Per name: the declared schema, or for the others the additionalProperties child (or nothing, noChild).
	children []child
	// Bit i set: name i is required (the names checked by the mask: at most 64 names in all).
	requiredMask uint64
	// Required names checked by lookup (when the mask cannot cover them).
	required []string
	patterns []patternChild
	// For each declared name, the patterns (indexes into patterns) it matches, worked out at compile time: only
	// undeclared names are tested against the patterns at run time.
	namePatterns  [][]uint16
	hasAdditional bool
	additional    child
	propertyNames nodeID
	dependencies  []planDependency
	// No required names by lookup and no dependencies: nothing after the property loop but the required mask.
	restFree bool
	// visitNames and restFree: the strict loop (runStrictObject) decides it.
	strict bool
	// visitNames with at most lookupNames names and no additionalProperties: undeclared properties need no visit,
	// so a small object is decided by looking each name up in it (see visitLookup).
	lookup bool
}

type patternChild struct {
	pattern *pattern
	child   child
}

const (
	// Plans with at most this many names may look them up in the instance instead of visiting its properties...
	lookupNames = 4
	// ...when the instance's properties times the names are at most this (each lookup scans the properties).
	lookupBudget = 24
)

// visit is the property loop, specialised by which keywords apply.
type visit uint8

const (
	// visitNone: no keyword looks at the properties.
	visitNone visit = iota
	// visitValues: only additionalProperties (a map): every value against one child.
	visitValues
	// visitNames: declared properties, and additionalProperties for the rest.
	visitNames
	// visitPattern: one patternProperties entry and nothing declared: each name against the pattern, else
	// additionalProperties.
	visitPattern
	// visitGeneral: anything else (patternProperties, propertyNames).
	visitGeneral
)

// branches are anyOf/oneOf branches, dispatched by the instance's type: only the branches whose type admits it are
// tried, and a branch that is only a type check is decided without a call.
type branches struct {
	children []child
	// For each instance kind (by the position of its bit), the branches that can accept it.
	byKind [6][]uint32
	// The discriminator, and its known values by lookup.
	discriminator *discriminator
	index         *discriminatorIndex
}

// discriminatorIndex is a discriminator's known values by lookup: string values through a name table, the few
// others (numbers, booleans, null) by scan.
type discriminatorIndex struct {
	strings *names
	// For each string in strings, its entry in discriminator.known.
	stringEntries []uint32
	others        []uint32
}

func newDiscriminatorIndex(d *discriminator) *discriminatorIndex {
	index := &discriminatorIndex{}
	var list []string
	for i := range d.known {
		if v := &d.known[i].value; v.kind == kindString {
			list = append(list, v.text)
			index.stringEntries = append(index.stringEntries, uint32(i))
		} else {
			index.others = append(index.others, uint32(i))
		}
	}
	index.strings = newNames(list)
	return index
}

// selectBranches is the branches a discriminator value selects.
func (index *discriminatorIndex) selectBranches(disc *discriminator, d *Document, v int) []uint32 {
	if d.kind(v) == kindString {
		if i := index.strings.find(d.str(v)); i >= 0 {
			return disc.known[index.stringEntries[i]].branches
		}
		return disc.unknown
	}
	for _, i := range index.others {
		if disc.known[i].value.matches(d, v) {
			return disc.known[i].branches
		}
	}
	return disc.unknown
}

func newBranches(children []child, disc *discriminator) *branches {
	b := &branches{children: children, discriminator: disc}
	if disc != nil {
		b.index = newDiscriminatorIndex(disc)
	}
	return b
}

// kindIndex is the index of a kind in branches.byKind.
func kindIndex(kind uint8) int {
	return bits.TrailingZeros8(kind)
}

// dispatch computes the dispatch table once the children's types are known.
func (b *branches) dispatch() {
	kindTypes := [6]uint8{typeNull, typeBoolean, typeObject, typeArray, typeNumber | typeInteger, typeString}
	for k, types := range kindTypes {
		b.byKind[k] = nil
		for i := range b.children {
			if b.children[i].types&types != 0 {
				b.byKind[k] = append(b.byKind[k], uint32(i))
			}
		}
	}
}

type planDependency struct {
	name     string
	required []string
	schema   nodeID
	// The seen bits of the name and of the names it requires, when every one is a known name.
	hasBits               bool
	nameBit, requiredBits uint64
}

type arrayPlan struct {
	min, max uint64
	prefix   []child
	hasItems bool
	items    child
	// contains: the node, and the bounds on the count.
	contains    nodeID
	minContains uint64
	maxContains optCount
	unique      bool
	// Bounds and type-only items, nothing else: the items' type mask (anyType: no test).
	isSimple bool
	simple   uint8
	// Items that are themselves simple arrays (GeoJSON's positions): their bounds and item types, checked inline.
	hasNested bool
	nested    simpleArray
}

type simpleArray struct {
	set      bool
	min, max uint64
	types    uint8
}

// ---------------------------------------------------------------------------------------------------------------------
// Compilation

func compilePlans(p *program) []plan {
	plans := make([]plan, len(p.nodes))
	for id, n := range p.nodes {
		plans[id] = planNode(p, nodeID(id), n)
	}
	// Hoist the children's type checks now that every plan is known.
	type summaryEntry struct {
		types uint8
		shape shape
	}
	summary := make([]summaryEntry, len(plans))
	for i := range plans {
		summary[i] = summaryEntry{plans[i].types, shapeOf(&plans[i], p.usesDynamicScope)}
	}
	resolved := func(id nodeID) child {
		s := summary[id]
		// Not shapeObject: a node may still take a fused plan below.
		if s.shape == shapeObject {
			s.shape = shapeGeneral
		}
		return child{id: id, types: s.types, shape: s.shape}
	}
	// Fused object plans, for nodes whose object semantics span in-place applicators. A fused plan applies its
	// contributors' keywords without entering them as nodes, so the dynamic scope below it would differ from the
	// general path's where a contributor is in another resource: nodes that can reach a live dynamic reference fuse
	// only contributors in their own resource (which the general path would not push again).
	reachesDynamic := reachesDynamicReference(p)
	fusedNodes := make([]bool, len(plans))
	for id := range plans {
		if f := tryFuse(p, nodeID(id), reachesDynamic[id], resolved); f != nil {
			if plans[id].body == nil {
				plans[id].body = &body{node: nodeID(id)}
			}
			plans[id].body.fused = f
			fusedNodes[id] = true
		}
	}
	fix := func(c *child) {
		if c.id < 0 {
			return
		}
		s := summary[c.id]
		c.types = s.types
		c.shape = s.shape
		if s.shape == shapeObject && fusedNodes[c.id] {
			c.shape = shapeGeneral
		}
	}
	for i := range plans {
		b := plans[i].body
		if b == nil {
			continue
		}
		if o := b.object; o != nil {
			for j := range o.children {
				fix(&o.children[j])
			}
			for j := range o.patterns {
				fix(&o.patterns[j].child)
			}
			if o.hasAdditional {
				fix(&o.additional)
			}
		}
		if a := b.array; a != nil {
			for j := range a.prefix {
				fix(&a.prefix[j])
			}
			itemTypes, typeOnly := anyType, true
			if a.hasItems {
				fix(&a.items)
				itemTypes, typeOnly = a.items.types, a.items.shape == shapeTrivial
			}
			a.isSimple = typeOnly && len(a.prefix) == 0 && a.contains < 0 && !a.unique
			a.simple = itemTypes
		}
		if b.hasUnevaluatedItems {
			fix(&b.unevaluatedChild)
		}
		for j := range b.apply {
			switch o := &b.apply[j]; o.kind {
			case opAllOf:
				for k := range o.children {
					fix(&o.children[k])
				}
			case opAnyOf, opOneOf:
				for k := range o.branches.children {
					fix(&o.branches.children[k])
				}
				o.branches.dispatch()
			}
		}
	}
	// Arrays whose items are simple arrays check them inline, without entering each one.
	simple := make([]simpleArray, len(plans))
	for i := range plans {
		b := plans[i].body
		if b == nil || b.array == nil || !b.array.isSimple || shapeOf(&plans[i], p.usesDynamicScope) != shapeArray {
			continue
		}
		simple[i] = simpleArray{set: true, min: b.array.min, max: b.array.max, types: b.array.simple}
	}
	for i := range plans {
		if b := plans[i].body; b != nil && b.array != nil {
			a := b.array
			if a.hasItems && a.items.shape == shapeArray && simple[a.items.id].set {
				a.hasNested, a.nested = true, simple[a.items.id]
			}
		}
	}
	for i := range plans {
		plans[i].shape = shapeOf(&plans[i], p.usesDynamicScope)
	}
	return plans
}

func planNode(p *program, id nodeID, n *schemaNode) plan {
	guard := n.inPlaceCycle
	if n.alwaysTrue {
		return plan{types: anyType, guard: guard}
	}
	if n.alwaysFalse {
		return plan{types: 0, guard: guard}
	}
	target := func(id nodeID) nodeID { return p.fastTarget[id] }
	childOf := func(id nodeID) child { return child{id: target(id), types: anyType, shape: shapeGeneral} }
	b := &body{node: id}

	if n.constValue.set() {
		b.values = append(b.values, op{kind: opConst, value: n.constValue})
	}
	if n.hasEnum {
		allStrings := true
		for _, v := range n.enumValues {
			allStrings = allStrings && v.d.kind(v.n) == kindString
		}
		if allStrings {
			list := make([]string, len(n.enumValues))
			for i, v := range n.enumValues {
				list[i] = string(v.d.str(v.n))
			}
			b.values = append(b.values, op{kind: opEnumStrings, names: newNames(list)})
		} else {
			b.values = append(b.values, op{kind: opEnum, values: n.enumValues})
		}
	}

	formatCheckFor := func(numeric bool) (formatCheck, bool) {
		if !n.assertFormat || !n.hasFormat || n.formatKind.isNumeric() != numeric {
			return formatCheck{}, false
		}
		if custom, ok := p.formats[n.format]; ok {
			return formatCheck{custom: custom}, true
		}
		if n.formatKind == formatUnknown {
			return formatCheck{}, false
		}
		return formatCheck{kind: n.formatKind, legacy: n.dialect <= Draft6}, true
	}

	// Numbers.
	if f, ok := formatCheckFor(true); ok {
		b.number = append(b.number, numberOp{kind: numberFormat, format: f})
	}
	bound := func(kind numberOpKind, v valueRef) {
		if v.set() {
			b.number = append(b.number, numberOp{kind: kind, flag: v.d.flags(v.n), data: v.d.data(v.n)})
		}
	}
	bound(numberMinimum, n.minimum)
	bound(numberMaximum, n.maximum)
	bound(numberExclusiveMinimum, n.exclusiveMinimum)
	bound(numberExclusiveMaximum, n.exclusiveMaximum)
	if n.multipleOf.set() {
		b.number = append(b.number, numberOp{kind: numberMultipleOf, divisor: n.divisor})
	}

	// Strings.
	if n.minLength.set || n.maxLength.set {
		o := stringOp{kind: stringLength, max: math.MaxUint64}
		if n.minLength.set {
			o.min = n.minLength.n
		}
		if n.maxLength.set {
			o.max = n.maxLength.n
		}
		b.str = append(b.str, o)
	}
	if n.pattern != nil {
		b.str = append(b.str, stringOp{kind: stringPattern, pattern: n.pattern})
	}
	if f, ok := formatCheckFor(false); ok {
		b.str = append(b.str, stringOp{kind: stringFormat, format: f})
	}
	if n.assertContent {
		b.str = append(b.str, stringOp{kind: stringContent, content: n.content})
	}

	// Objects (unless the type excludes objects: then the keywords apply to nothing and must not cost the node its
	// shape).
	ownTypes := anyType
	if n.hasType {
		ownTypes = n.typeMask
	}
	if n.hasObjectKeywords() && ownTypes&typeObject != 0 {
		b.object = planObject(p, n, childOf)
	}

	// Arrays.
	if n.hasArrayKeywords() && ownTypes&typeArray != 0 {
		a := &arrayPlan{max: math.MaxUint64, contains: noNode, unique: n.uniqueItems}
		if n.minItems.set {
			a.min = n.minItems.n
		}
		if n.maxItems.set {
			a.max = n.maxItems.n
		}
		for _, c := range n.prefixItems {
			a.prefix = append(a.prefix, childOf(c))
		}
		if n.items >= 0 {
			a.hasItems, a.items = true, childOf(n.items)
		}
		if n.contains >= 0 {
			a.contains, a.minContains, a.maxContains = target(n.contains), n.minContains, n.maxContains
		}
		b.array = a
	}

	// In-place applicators, in the general evaluator's order.
	if n.ref >= 0 {
		b.apply = append(b.apply, op{kind: opRef, node: target(n.ref)})
	}
	if n.staticDynamicRef >= 0 {
		b.apply = append(b.apply, op{kind: opRef, node: target(n.staticDynamicRef)})
	}
	if d := n.dynamicRef; d != nil {
		if p.usesDynamicScope {
			b.apply = append(b.apply, op{kind: opDynamicRef, dynamic: d})
		} else {
			// Without a dynamic scope the reference always takes its fallback.
			b.apply = append(b.apply, op{kind: opRef, node: target(d.fallback)})
		}
	}
	children := func(list []nodeID) []child {
		out := make([]child, len(list))
		for i, c := range list {
			out[i] = childOf(c)
		}
		return out
	}
	if n.hasAllOf {
		b.apply = append(b.apply, op{kind: opAllOf, children: children(n.allOf)})
	}
	// An anyOf of type-only branches, or a oneOf of type-only branches with no type in common, is one type test:
	// it narrows the node's own types instead of adding a keyword.
	union := anyType
	if n.hasAnyOf {
		if mask, ok := typeUnion(p, n.anyOf, false); ok {
			union = meetTypes(union, mask)
		} else {
			b.apply = append(b.apply, op{kind: opAnyOf, branches: newBranches(children(n.anyOf), n.anyOfDiscriminator)})
		}
	}
	if n.hasOneOf {
		if mask, ok := typeUnion(p, n.oneOf, true); ok {
			union = meetTypes(union, mask)
		} else {
			b.apply = append(b.apply, op{kind: opOneOf, branches: newBranches(children(n.oneOf), n.oneOfDiscriminator)})
		}
	}
	if n.not >= 0 {
		b.apply = append(b.apply, op{kind: opNot, node: target(n.not)})
	}
	if n.if_ >= 0 && (n.then >= 0 || n.else_ >= 0) {
		o := op{kind: opIf, node: target(n.if_), then: noNode, else_: noNode}
		if n.then >= 0 {
			o.then = target(n.then)
		}
		if n.else_ >= 0 {
			o.else_ = target(n.else_)
		}
		b.apply = append(b.apply, o)
	}

	// unevaluatedProperties is left to the general evaluator (or a fused object plan). unevaluatedItems takes the
	// items after a static prefix when every contribution to the evaluated items is unconditional.
	if n.unevaluatedProperties >= 0 {
		b.general |= typeObject
	}
	if n.unevaluatedItems >= 0 {
		switch all, from, ok := staticItemCoverage(p, id); {
		case !ok:
			b.general |= typeArray
		case !all:
			b.hasUnevaluatedItems, b.unevaluatedFrom, b.unevaluatedChild = true, from, childOf(n.unevaluatedItems)
		}
	}

	types := meetTypes(ownTypes, union)
	if len(b.values) == 0 && len(b.number) == 0 && len(b.str) == 0 && b.object == nil && b.array == nil &&
		len(b.apply) == 0 && b.general == 0 && !b.hasUnevaluatedItems {
		return plan{types: types, guard: guard}
	}
	return plan{types: types, guard: guard, body: b}
}

func planObject(p *program, n *schemaNode, childOf func(nodeID) child) *objectPlan {
	// Names only required or dependencies mention join the declared ones, up to 64 names in all (one mask word).
	known := make([]string, 0, len(n.properties))
	for _, property := range n.properties {
		known = append(known, property.name)
	}
	var extras []string
	extra := func(name string) {
		if !containsString(known, name) && !containsString(extras, name) {
			extras = append(extras, name)
		}
	}
	for _, r := range n.required {
		extra(r)
	}
	for i := range n.dependencies {
		extra(n.dependencies[i].name)
		for _, r := range n.dependencies[i].required {
			extra(r)
		}
	}
	if len(known)+len(extras) <= 64 {
		known = append(known, extras...)
	}
	o := &objectPlan{
		max: math.MaxUint64, names: newNames(known), declared: len(n.properties), propertyNames: noNode,
	}
	if n.minProperties.set {
		o.min = n.minProperties.n
	}
	if n.maxProperties.set {
		o.max = n.maxProperties.n
	}
	undeclared := noChild
	if n.additionalProperties >= 0 {
		undeclared = childOf(n.additionalProperties)
		o.hasAdditional, o.additional = true, undeclared
	}
	o.children = make([]child, len(known))
	for i := range known {
		if i < len(n.properties) {
			o.children[i] = childOf(n.properties[i].node)
		} else {
			o.children[i] = undeclared
		}
	}
	hasNames := len(known) != 0
	switch {
	case n.propertyNames < 0 && !hasNames && len(n.patternProperties) == 1:
		o.visit = visitPattern
	case n.hasPatternProperties || n.propertyNames >= 0:
		o.visit = visitGeneral
	case hasNames:
		o.visit = visitNames
	case n.additionalProperties >= 0:
		o.visit = visitValues
	}
	visited := (o.visit == visitNames || o.visit == visitGeneral) && len(known) <= 64
	bit := func(name string) (uint64, bool) {
		if !visited {
			return 0, false
		}
		if i := o.names.findString(name); i >= 0 {
			return 1 << i, true
		}
		return 0, false
	}
	for _, r := range n.required {
		if b, ok := bit(r); ok {
			o.requiredMask |= b
		} else {
			o.required = append(o.required, r)
		}
	}
	for i := range n.dependencies {
		dep := &n.dependencies[i]
		pd := planDependency{name: dep.name, required: dep.required, schema: noNode}
		if dep.schema >= 0 {
			pd.schema = p.fastTarget[dep.schema]
		}
		nameBit, ok := bit(dep.name)
		for _, r := range dep.required {
			b, found := bit(r)
			ok = ok && found
			pd.requiredBits |= b
		}
		pd.hasBits, pd.nameBit = ok, nameBit
		o.dependencies = append(o.dependencies, pd)
	}
	for _, pp := range n.patternProperties {
		o.patterns = append(o.patterns, patternChild{pp.pattern, childOf(pp.node)})
	}
	o.namePatterns = make([][]uint16, len(known))
	for i, name := range known {
		for j, pp := range n.patternProperties {
			if pp.pattern.matchString(name) {
				o.namePatterns[i] = append(o.namePatterns[i], uint16(j))
			}
		}
	}
	if n.propertyNames >= 0 {
		o.propertyNames = p.fastTarget[n.propertyNames]
	}
	o.restFree = len(o.required) == 0 && !n.hasDependencies
	o.strict = o.visit == visitNames && o.restFree
	o.lookup = o.visit == visitNames && n.additionalProperties < 0 && len(known) <= lookupNames
	return o
}

// shapeOf says how callers can enter a plan (see shape). The shortcuts skip the scope push, so a program that keeps
// a dynamic scope takes them only for leaves. A node on an in-place cycle is entered through its guard.
func shapeOf(pl *plan, dynamicScope bool) shape {
	if pl.guard {
		return shapeGeneral
	}
	b := pl.body
	if b == nil {
		return shapeTrivial
	}
	if b.fused != nil || b.general != 0 || b.hasUnevaluatedItems {
		return shapeGeneral
	}
	values := len(b.values) != 0 || len(b.number) != 0 || len(b.str) != 0
	object, array, apply := b.object != nil, b.array != nil, len(b.apply) != 0
	switch {
	case !object && !array && !apply:
		if values && len(b.number) == 0 && len(b.str) == 0 && len(b.values) == 1 && b.values[0].kind == opEnumStrings {
			return shapeStringEnum
		}
		if values && len(b.number) == 0 && len(b.values) == 0 {
			return shapeStrings
		}
		return shapeLeaf
	case values || dynamicScope:
		return shapeGeneral
	case object && !array && !apply:
		return shapeObject
	case !object && array && !apply:
		return shapeArray
	case !object && !array && apply:
		return shapeApply
	}
	return shapeGeneral
}

// reachesDynamicReference marks every node from which a live dynamic reference is reachable through any child.
func reachesDynamicReference(p *program) []bool {
	reaches := make([]bool, len(p.nodes))
	if !p.usesDynamicScope {
		return reaches
	}
	children := make([][]nodeID, len(p.nodes))
	for i, n := range p.nodes {
		reaches[i] = n.dynamicRef != nil
		children[i] = n.children()
	}
	for changed := true; changed; {
		changed = false
		for i, list := range children {
			if reaches[i] {
				continue
			}
			for _, c := range list {
				if reaches[c] {
					reaches[i], changed = true, true
					break
				}
			}
		}
	}
	return reaches
}

// expandTypes is a type mask with integer made explicit wherever number is (every integer is a number).
func expandTypes(mask uint8) uint8 {
	if mask&typeNumber != 0 {
		return mask | typeInteger
	}
	return mask
}

// meetTypes is the types both masks admit.
func meetTypes(a, b uint8) uint8 {
	m := expandTypes(a) & expandTypes(b)
	// number in both keeps number. integer alone stays integer.
	if a&b&typeNumber == 0 && m&typeNumber != 0 {
		return m &^ typeNumber
	}
	return m
}

// typeOnlyMask is the mask of a schema that tests only the type (true admits everything, false nothing).
func typeOnlyMask(n *schemaNode) (uint8, bool) {
	if n.alwaysTrue {
		return anyType, true
	}
	if n.alwaysFalse {
		return 0, true
	}
	onlyType := n.hasType && !n.inPlaceCycle && !n.constValue.set() && !n.hasEnum && !n.hasNumberKeywords() &&
		!n.hasStringKeywords() && !n.hasObjectKeywords() && !n.hasArrayKeywords() && !n.hasInPlaceApplicators() &&
		n.dynamicRef == nil
	return n.typeMask, onlyType
}

// typeUnion is the union of anyOf/oneOf branches that all test only the type. For oneOf, only when no two branches
// admit a common value (so that "exactly one" is "any").
func typeUnion(p *program, list []nodeID, exactlyOne bool) (uint8, bool) {
	masks := make([]uint8, len(list))
	for i, c := range list {
		mask, ok := typeOnlyMask(p.nodes[p.fastTarget[c]])
		if !ok {
			return 0, false
		}
		masks[i] = mask
	}
	union := uint8(0)
	for i, a := range masks {
		if exactlyOne {
			for _, b := range masks[i+1:] {
				if expandTypes(a)&expandTypes(b) != 0 {
					return 0, false
				}
			}
		}
		union |= a
	}
	return union, true
}

// staticItemCoverage is the items a node's evaluation always marks evaluated, when that is static: the longest
// prefixItems of the node and the contributors it always applies ($ref and allOf chains) as from, or all of them
// when one has items (or, below the node, unevaluatedItems). Not ok when an in-place child that applies
// conditionally (anyOf, oneOf, if/then/else, a dependent schema, a dynamic reference) can mark items, or contains
// marks them.
func staticItemCoverage(p *program, id nodeID) (all bool, from int, ok bool) {
	type entry struct {
		id   nodeID
		root bool
	}
	stack := []entry{{id, true}}
	visited := []nodeID{id}
	for len(stack) > 0 {
		at := stack[len(stack)-1]
		stack = stack[:len(stack)-1]
		n := p.nodes[at.id]
		if n.alwaysTrue || n.alwaysFalse {
			continue
		}
		if n.inPlaceCycle || n.dynamicRef != nil || (n.contains >= 0 && n.containsMarksEvaluated) {
			return false, 0, false
		}
		from = max(from, len(n.prefixItems))
		all = all || n.items >= 0 || (!at.root && n.unevaluatedItems >= 0)
		conditional := append(append([]nodeID(nil), n.anyOf...), n.oneOf...)
		conditional = appendNode(conditional, n.if_, n.then, n.else_)
		for i := range n.dependencies {
			conditional = appendNode(conditional, n.dependencies[i].schema)
		}
		for _, c := range conditional {
			if p.nodes[c].marksItems {
				return false, 0, false
			}
		}
		for _, c := range append(appendNode(nil, n.ref, n.staticDynamicRef), n.allOf...) {
			seen := false
			for _, v := range visited {
				seen = seen || v == c
			}
			if !seen {
				visited = append(visited, c)
				stack = append(stack, entry{c, false})
			}
		}
	}
	return all, from, true
}

// ---------------------------------------------------------------------------------------------------------------------
// Evaluation

// run evaluates a node's plan (at a new instance location, or where no depth guard applies).
func (e *evaluator) run(id nodeID, x int) bool {
	pl := &e.p.plans[id]
	if pl.types != anyType && !typeOK(pl.types, e.d, x) {
		return false
	}
	if pl.body == nil {
		return true
	}
	if pl.shape == shapeGeneral {
		return e.runBody(pl.body, x)
	}
	return e.enter(pl.shape, pl.body, x)
}

// runChild evaluates a child at a new instance location: its type check inline, its other keywords (if any) by
// call.
func (e *evaluator) runChild(c child, x int) bool {
	if c.types != anyType && !typeOK(c.types, e.d, x) {
		return false
	}
	if c.shape == shapeTrivial {
		return true
	}
	b := e.p.plans[c.id].body
	if b == nil {
		return true
	}
	return e.enter(c.shape, b, x)
}

// enter evaluates a body by its shape (its types already tested, and not on an in-place cycle unless general).
func (e *evaluator) enter(s shape, b *body, x int) bool {
	d := e.d
	switch s {
	case shapeLeaf:
		return e.runLeaf(b, x)
	case shapeStringEnum:
		return d.kind(x) == kindString && b.values[0].names.find(d.str(x)) >= 0
	case shapeStrings:
		return d.kind(x) != kindString || e.runString(b.str, x)
	case shapeObject:
		if d.kind(x) != kindObject {
			return true
		}
		if b.object.strict {
			return e.runStrictObject(b.object, x)
		}
		return e.runObject(b.object, x)
	case shapeArray:
		return d.kind(x) != kindArray || e.runArray(b.array, x)
	case shapeApply:
		return e.runApply(b.apply, x)
	}
	return e.runBody(b, x)
}

// runInPlace evaluates an in-place child under the depth guard.
func (e *evaluator) runInPlace(id nodeID, x int) bool {
	if !e.p.plans[id].guard {
		return e.run(id, x)
	}
	e.depth++
	if e.depth > e.p.maxDepth {
		e.depthExceeded = true
		e.depth--
		return false
	}
	ok := e.run(id, x)
	e.depth--
	return ok
}

// runBranch evaluates an in-place child with its type check inline, then its keywords under the depth guard
// (without testing the type again).
func (e *evaluator) runBranch(c child, x int) bool {
	if c.types != anyType && !typeOK(c.types, e.d, x) {
		return false
	}
	if c.shape == shapeTrivial {
		return true
	}
	pl := &e.p.plans[c.id]
	if pl.body == nil || pl.guard {
		return e.runInPlace(c.id, x)
	}
	return e.enter(c.shape, pl.body, x)
}

// candidates are the anyOf/oneOf branches that can match: those a discriminator selects, or those admitting the
// instance type.
func (e *evaluator) candidates(b *branches, x int) []uint32 {
	d := e.d
	kind := d.kind(x)
	if b.discriminator == nil || kind != kindObject {
		return b.byKind[kindIndex(kind)]
	}
	v := d.property(x, b.discriminator.property)
	switch {
	case v >= 0:
		return b.index.selectBranches(b.discriminator, d, v)
	case b.discriminator.allRequire:
		return nil
	}
	return b.byKind[kindIndex(kind)]
}

// runBody evaluates a node's keywords. Where the program keeps a dynamic scope, entering a node of another resource
// pushes that resource (as the general evaluator does), for the dynamic references below it.
func (e *evaluator) runBody(b *body, x int) bool {
	if !e.p.usesDynamicScope {
		return e.runKeywords(b, x)
	}
	pushed := e.pushScope(e.p.nodes[b.node].resourceID)
	ok := e.runKeywords(b, x)
	if pushed {
		e.popScope()
	}
	return ok
}

func (e *evaluator) runKeywords(b *body, x int) bool {
	d := e.d
	kind := d.kind(x)
	switch kind {
	case kindObject:
		if b.fused != nil {
			return e.runFused(b.fused, x)
		}
		if b.general&typeObject != 0 {
			return e.evalNode(b.node, x, noBits)
		}
	case kindArray:
		if b.general&typeArray != 0 {
			return e.evalNode(b.node, x, noBits)
		}
	}
	for i := range b.values {
		if !e.runOp(&b.values[i], x) {
			return false
		}
	}
	switch kind {
	case kindNumber:
		if len(b.number) != 0 && !e.runNumber(b.number, x) {
			return false
		}
	case kindString:
		if len(b.str) != 0 && !e.runString(b.str, x) {
			return false
		}
	case kindObject:
		if b.object != nil && !e.runObject(b.object, x) {
			return false
		}
	case kindArray:
		if b.array != nil && !e.runArray(b.array, x) {
			return false
		}
		if b.hasUnevaluatedItems {
			first := d.first(x)
			for i := b.unevaluatedFrom; i < d.count(x); i++ {
				if !e.runChild(b.unevaluatedChild, first+i) {
					return false
				}
			}
		}
	}
	return len(b.apply) == 0 || e.runApply(b.apply, x)
}

func (e *evaluator) runApply(ops []op, x int) bool {
	for i := range ops {
		if !e.runOp(&ops[i], x) {
			return false
		}
	}
	return true
}

func (e *evaluator) enumContains(values []valueRef, x int) bool {
	for _, v := range values {
		if valuesEqual(e.d, x, v.d, v.n) {
			return true
		}
	}
	return false
}

func (e *evaluator) runOp(o *op, x int) bool {
	d := e.d
	switch o.kind {
	case opConst:
		return valuesEqual(d, x, o.value.d, o.value.n)
	case opEnumStrings:
		return d.kind(x) == kindString && o.names.find(d.str(x)) >= 0
	case opEnum:
		return e.enumContains(o.values, x)
	case opRef:
		return e.runInPlace(o.node, x)
	case opAllOf:
		for _, c := range o.children {
			if !e.runBranch(c, x) {
				return false
			}
		}
		return true
	case opAnyOf:
		for _, i := range e.candidates(o.branches, x) {
			if e.runBranch(o.branches.children[i], x) {
				return true
			}
		}
		return false
	case opOneOf:
		candidates := e.candidates(o.branches, x)
		// The instance's type (or the discriminator) leaves one branch: that branch decides.
		if len(candidates) == 1 {
			return e.runBranch(o.branches.children[candidates[0]], x)
		}
		matched := 0
		for _, i := range candidates {
			if e.runBranch(o.branches.children[i], x) {
				if matched++; matched > 1 {
					break
				}
			}
		}
		return matched == 1
	case opNot:
		return !e.run(o.node, x)
	case opDynamicRef:
		return e.runInPlace(e.p.fastTarget[e.resolveDynamic(o.dynamic)], x)
	default:
		next := o.else_
		if e.runInPlace(o.node, x) {
			next = o.then
		}
		return next < 0 || e.runInPlace(next, x)
	}
}

// runObject evaluates an object plan: the size bounds, the property loop for its shape, then the required names and
// dependencies.
func (e *evaluator) runObject(pl *objectPlan, x int) bool {
	count := e.d.count(x)
	if uint64(count) < pl.min || uint64(count) > pl.max {
		return false
	}
	seen, ok := uint64(0), true
	switch pl.visit {
	case visitValues:
		ok = e.visitValues(pl, x)
	case visitNames:
		if pl.lookup && count*pl.names.len() <= lookupBudget {
			seen, ok = e.visitLookup(pl, x)
		} else {
			seen, ok = e.visitNames(pl, x)
		}
	case visitPattern:
		ok = e.visitPattern(pl, x)
	case visitGeneral:
		seen, ok = e.visitGeneral(pl, x)
	}
	if !ok || seen&pl.requiredMask != pl.requiredMask {
		return false
	}
	return pl.restFree || e.objectRest(pl, x, seen)
}

// runStrictObject is the strict loop: bounds, declared names (additionalProperties for the rest), and the required
// mask. A small function of its own, since nested objects enter it directly.
func (e *evaluator) runStrictObject(pl *objectPlan, x int) bool {
	d := e.d
	count := d.count(x)
	if uint64(count) < pl.min || uint64(count) > pl.max {
		return false
	}
	if pl.lookup && count*pl.names.len() <= lookupBudget {
		seen, ok := e.visitLookup(pl, x)
		return ok && seen&pl.requiredMask == pl.requiredMask
	}
	seen, ok := e.visitNames(pl, x)
	return ok && seen&pl.requiredMask == pl.requiredMask
}

// visitLookup looks each name up in the object (a plan with lookup: the other properties need no visit). It returns
// the names seen.
func (e *evaluator) visitLookup(pl *objectPlan, x int) (uint64, bool) {
	seen := uint64(0)
	for i, name := range pl.names.m.names {
		if v := e.d.property(x, name); v >= 0 {
			seen |= 1 << i
			if !e.runChild(pl.children[i], v) {
				return 0, false
			}
		}
	}
	return seen, true
}

// objectRest checks the required names checked by lookup, and the dependencies.
func (e *evaluator) objectRest(pl *objectPlan, x int, seen uint64) bool {
	d := e.d
	for _, r := range pl.required {
		if d.property(x, r) < 0 {
			return false
		}
	}
	for i := range pl.dependencies {
		dep := &pl.dependencies[i]
		if dep.hasBits {
			if seen&dep.nameBit == 0 {
				continue
			}
			if seen&dep.requiredBits != dep.requiredBits {
				return false
			}
		} else {
			if d.property(x, dep.name) < 0 {
				continue
			}
			for _, r := range dep.required {
				if d.property(x, r) < 0 {
					return false
				}
			}
		}
		if dep.schema >= 0 && !e.runInPlace(dep.schema, x) {
			return false
		}
	}
	return true
}

// visitValues is for only additionalProperties: every value against one child.
func (e *evaluator) visitValues(pl *objectPlan, x int) bool {
	d := e.d
	c := pl.additional
	first, count := d.first(x), d.count(x)
	if c.shape == shapeTrivial {
		if c.types == anyType {
			return true
		}
		for i := 0; i < count; i++ {
			if !typeOK(c.types, d, first+2*i+1) {
				return false
			}
		}
		return true
	}
	for i := 0; i < count; i++ {
		if !e.runChild(c, first+2*i+1) {
			return false
		}
	}
	return true
}

// visitNames is for declared properties, and additionalProperties for the rest. It returns the declared names seen.
func (e *evaluator) visitNames(pl *objectPlan, x int) (uint64, bool) {
	d := e.d
	seen := uint64(0)
	hint := 0
	k := d.first(x)
	for end := k + 2*d.count(x); k < end; k += 2 {
		var i int
		if i, hint = pl.names.findFrom(d.str(k), hint); i >= 0 {
			seen |= 1 << (i & 63)
			if !e.runChild(pl.children[i], k+1) {
				return 0, false
			}
		} else if pl.hasAdditional && !e.runChild(pl.additional, k+1) {
			return 0, false
		}
	}
	return seen, true
}

func (e *evaluator) visitPattern(pl *objectPlan, x int) bool {
	d := e.d
	p := &pl.patterns[0]
	k := d.first(x)
	for end := k + 2*d.count(x); k < end; k += 2 {
		if p.pattern.match(d.str(k), d.strASCII(k)) {
			if !e.runChild(p.child, k+1) {
				return false
			}
		} else if pl.hasAdditional && !e.runChild(pl.additional, k+1) {
			return false
		}
	}
	return true
}

func (e *evaluator) visitGeneral(pl *objectPlan, x int) (uint64, bool) {
	d := e.d
	seen := uint64(0)
	hint := 0
	k := d.first(x)
	for end := k + 2*d.count(x); k < end; k += 2 {
		name := d.str(k)
		matched := false
		var i int
		if i, hint = pl.names.findFrom(name, hint); i >= 0 {
			seen |= 1 << (i & 63)
			// A name only required (or a dependency) mentions is undeclared: patterns, else additionalProperties.
			if i < pl.declared {
				matched = true
				if !e.runChild(pl.children[i], k+1) {
					return 0, false
				}
			}
			for _, j := range pl.namePatterns[i] {
				matched = true
				if !e.runChild(pl.patterns[j].child, k+1) {
					return 0, false
				}
			}
		} else {
			for j := range pl.patterns {
				if pl.patterns[j].pattern.match(name, d.strASCII(k)) {
					matched = true
					if !e.runChild(pl.patterns[j].child, k+1) {
						return 0, false
					}
				}
			}
		}
		if !matched && pl.hasAdditional && !e.runChild(pl.additional, k+1) {
			return 0, false
		}
		// The name is a string value of the document: it is evaluated where it is.
		if pl.propertyNames >= 0 && !e.run(pl.propertyNames, k) {
			return 0, false
		}
	}
	return seen, true
}

func (e *evaluator) runArray(pl *arrayPlan, x int) bool {
	d := e.d
	count := d.count(x)
	if uint64(count) < pl.min || uint64(count) > pl.max {
		return false
	}
	first := d.first(x)
	if pl.isSimple {
		return allOfType(d, first, count, pl.simple)
	}
	prefix := min(len(pl.prefix), count)
	for i := 0; i < prefix; i++ {
		if !e.runChild(pl.prefix[i], first+i) {
			return false
		}
	}
	if pl.hasItems {
		items := pl.items
		switch {
		case pl.hasNested:
			nested := pl.nested
			for item := first + prefix; item < first+count; item++ {
				if d.kind(item) != kindArray {
					// Not an array: only the items' type test applies.
					if !typeOK(items.types, d, item) {
						return false
					}
					continue
				}
				length := uint64(d.count(item))
				if items.types&typeArray == 0 || length < nested.min || length > nested.max ||
					!allOfType(d, d.first(item), int(length), nested.types) {
					return false
				}
			}
		case items.shape == shapeTrivial:
			// A type-only items schema: one tight loop, or none for true.
			if !allOfType(d, first+prefix, count-prefix, items.types) {
				return false
			}
		default:
			for item := first + prefix; item < first+count; item++ {
				if !e.runChild(items, item) {
					return false
				}
			}
		}
	}
	if pl.contains >= 0 {
		matches := uint64(0)
		for i := 0; i < count; i++ {
			if e.run(pl.contains, first+i) {
				matches++
				if !pl.maxContains.set && matches >= pl.minContains {
					break
				}
			}
		}
		if matches < pl.minContains || (pl.maxContains.set && matches > pl.maxContains.n) {
			return false
		}
	}
	return !pl.unique || allUnique(d, x, &e.state().unique)
}

// allOfType reports whether each of count consecutive values is of the types in a mask.
func allOfType(d *Document, first, count int, types uint8) bool {
	if types == anyType {
		return true
	}
	tape := d.tape[first<<1 : (first+count)<<1]
	if types&typeInteger != 0 && types&typeNumber == 0 {
		// Integers that are not numbers in general: the number's value decides.
		for i := 0; i < len(tape); i += 2 {
			bit := uint8(tape[i])
			if types&bit == 0 && !(bit == kindNumber && isIntegerNumber(uint8(tape[i]>>8), tape[i+1])) {
				return false
			}
		}
		return true
	}
	// A kind is its type bit.
	for i := 0; i < len(tape); i += 2 {
		if uint8(tape[i])&types == 0 {
			return false
		}
	}
	return true
}

// runLeaf evaluates a leaf's keywords: its value constraints, then those for the instance's type.
func (e *evaluator) runLeaf(b *body, x int) bool {
	d := e.d
	for i := range b.values {
		o := &b.values[i]
		var ok bool
		switch o.kind {
		case opConst:
			ok = valuesEqual(d, x, o.value.d, o.value.n)
		case opEnumStrings:
			ok = d.kind(x) == kindString && o.names.find(d.str(x)) >= 0
		default:
			ok = e.enumContains(o.values, x)
		}
		if !ok {
			return false
		}
	}
	switch d.kind(x) {
	case kindNumber:
		return len(b.number) == 0 || e.runNumber(b.number, x)
	case kindString:
		return len(b.str) == 0 || e.runString(b.str, x)
	}
	return true
}

func (e *evaluator) runNumber(ops []numberOp, x int) bool {
	d := e.d
	flag, data := d.flags(x), d.data(x)
	for i := range ops {
		o := &ops[i]
		var ok bool
		switch o.kind {
		case numberMinimum:
			ok = compareNumbers(flag, data, o.flag, o.data) >= 0
		case numberMaximum:
			ok = compareNumbers(flag, data, o.flag, o.data) <= 0
		case numberExclusiveMinimum:
			ok = compareNumbers(flag, data, o.flag, o.data) > 0
		case numberExclusiveMaximum:
			ok = compareNumbers(flag, data, o.flag, o.data) < 0
		case numberMultipleOf:
			ok = o.divisor.divides(d, x)
		default:
			ok = o.format.checkNumber(d, x)
		}
		if !ok {
			return false
		}
	}
	return true
}

func (e *evaluator) runString(ops []stringOp, x int) bool {
	d := e.d
	for i := range ops {
		o := &ops[i]
		var ok bool
		switch o.kind {
		case stringLength:
			ok = lengthOK(d, x, o.min, o.max)
		case stringPattern:
			ok = o.pattern.match(d.str(x), d.strASCII(x))
		case stringFormat:
			ok = o.format.checkString(d.str(x))
		default:
			ok = e.contentOK(d.str(x), o.content)
		}
		if !ok {
			return false
		}
	}
	return true
}

// lengthOK decides minLength/maxLength, counting code points only when the byte length cannot decide (a code point
// is one to four bytes).
func lengthOK(d *Document, x int, min, max uint64) bool {
	length := uint64(d.count(x))
	if length < min {
		return false
	}
	if d.strASCII(x) {
		return length <= max
	}
	quarter := (length + 3) / 4
	if length <= max && quarter >= min {
		return true
	}
	// Every code point is at most four bytes: more than max of them for sure.
	if quarter > max {
		return false
	}
	chars := codePoints(d, x)
	return chars >= min && chars <= max
}
