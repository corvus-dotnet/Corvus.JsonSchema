package jsonschema

import "math/bits"

// The fused object plan: one pass over an object's properties for a schema whose object semantics are spread over
// in-place applicators ($ref, allOf, if/then/else, dependencies, required-only oneOf/anyOf) and possibly finished
// by unevaluatedProperties.
//
// The general path evaluates each applicator branch as a separate pass over the instance, each with its own
// property lookups, and then walks the instance once more for unevaluatedProperties. The fused plan resolves every
// property name known to any branch at compile time to the list of child schemas that apply to it (a branch's own
// property schema, its matching pattern-property schemas, or its additionalProperties when neither matched), so
// evaluation is one lookup per instance property, and it tracks which properties some branch covered so that the
// unevaluated check needs no second analysis.
//
// Branches under then/else (or a dependency's schema) apply only when their condition holds. The plan supports
// conditions the pass itself decides (required names, property values tested against constants or a pattern, and
// names no property may match), and defers those branches' applications to a second step over the properties they
// touch.
//
// Fail-fast evaluation only. The fused pass does not enter its contributors as nodes (and so would not push their
// resources on the dynamic scope): below a live dynamic reference, only contributors in the node's own resource
// fuse.

const (
	maxFusedNames        = 256
	maxFusedConditions   = 64
	maxFusedContributors = 64
	maxFusedAltGroups    = 8
)

type fusedObject struct {
	// The branches merge into one strict object loop: every one applies unconditionally with declared properties
	// and required names only (and count bounds), and each name resolves to one schema.
	flat *objectPlan
	// Every property name any branch or condition knows, to its entry.
	names   *names
	entries []fusedEntry
	// The branches, the node itself first, then the required lists of dependencies.
	contributors []fusedContributor
	conditions   []fusedCondition
	// Required-only oneOf/anyOf keywords, decided from the seen names after the pass.
	alternatives []fusedAlternative
	// oneOf/anyOf groups whose branches carry object keywords (each branch is a contributor).
	altGroups []fusedAltGroup
	// not: {required: [...]}: names that must not all be present, under a condition.
	forbidden []fusedForbidden
	// Conditions that fail when some property name matches a pattern (an if with patternProperties: {P: false}),
	// for names no entry knows. A known name that matches carries a test that never holds.
	absent []fusedAbsent
	// Some branch has pattern properties or additional properties, or some condition absent patterns, so names no
	// entry knows need resolving.
	resolvesUnknown bool
	hasCountBounds  bool
	hasUnevaluated  bool
	unevaluated     child
}

// gate is a condition and the polarity under which something applies. Without set, it always applies.
type gate struct {
	set       bool
	condition uint16
	polarity  bool
}

type fusedForbidden struct {
	gate  gate
	names []uint16
}

type fusedAbsent struct {
	condition uint16
	pattern   *pattern
}

type fusedEntry struct {
	apps []fusedApp
	// The conditions' tests on this property's value.
	tests []valueTest
	// The constant tests merged: one lookup decides them all.
	merged *mergedTests
}

type maskedValue struct {
	value valueRef
	mask  uint64
}

// mergedTests is each constant any of an entry's constant tests allows, with the mask of the tests (by index in
// tests) that allow it. Strings are looked up by name. Other constants are compared in turn.
type mergedTests struct {
	strings     *names
	stringMasks []uint64
	others      []maskedValue
	// The tests that are constant sets. The others (patterns) are tested one by one.
	keyed uint64
}

// allowed is the mask of the constant tests that allow the value.
func (m *mergedTests) allowed(d *Document, v int) uint64 {
	if d.kind(v) == kindString {
		if i := m.strings.find(d.str(v)); i >= 0 {
			return m.stringMasks[i]
		}
		return 0
	}
	for i := range m.others {
		if valuesEqual(d, v, m.others[i].value.d, m.others[i].value.n) {
			return m.others[i].mask
		}
	}
	return 0
}

// fusedApp is one child schema applying to a property on behalf of a contributor.
type fusedApp struct {
	contributor uint16
	// Not set: true, which covers without a test.
	hasChild bool
	child    child
	// Other contributors whose resolution of the property is the same test: applied once when any is active.
	others []uint16
}

type altBranch struct {
	set    bool
	group  uint16
	branch uint16
}

// optChild is a child schema, or true (not set), which covers without a test.
type optChild struct {
	set   bool
	child child
}

type fusedPattern struct {
	pattern *pattern
	child   optChild
}

type fusedContributor struct {
	// The condition and the polarity under which it applies.
	condition gate
	// The alternative group and the branch in it.
	alt      altBranch
	patterns []fusedPattern
	// additionalProperties.
	hasAdditional bool
	additional    optChild
	required      []uint16
	min, max      optCount
}

// fusedCondition is an if the pass decides (required names and value tests), or the presence of a dependency's
// property.
type fusedCondition struct {
	required []uint16
	// The enclosing condition and the polarity under which this one is reached.
	gate gate
}

// valueTest is a condition's test on a property's value: absent passes, present must hold.
type valueTest struct {
	condition uint16
	// One of these constants (strings, integers, booleans, null). None: the property must be absent.
	isPattern      bool
	allowed        []valueRef
	pattern        *pattern
	requiresString bool
}

func (t *valueTest) holds(d *Document, v int) bool {
	if t.isPattern {
		if d.kind(v) == kindString {
			return t.pattern.match(d.str(v), d.strASCII(v))
		}
		return !t.requiresString
	}
	for _, a := range t.allowed {
		if valuesEqual(d, v, a.d, a.n) {
			return true
		}
	}
	return false
}

type fusedAlternative struct {
	condition  gate
	exactlyOne bool
	branches   [][]uint16
}

type fusedAltGroup struct {
	exactlyOne bool
	count      uint32
}

// ---------------------------------------------------------------------------------------------------------------------
// Construction

// collectedContributor is a contributor as collected: the branch, its condition, its alternative group and branch.
type collectedContributor struct {
	node      nodeID
	condition gate
	alt       altBranch
}

type pendingCondition struct {
	// The if schema, or the dependency's property name.
	isTest     bool
	test       nodeID
	dependency string
	gate       gate
}

type collectedExtra struct {
	condition uint16
	names     []string
}

type collectedAlternative struct {
	condition  gate
	exactlyOne bool
	branches   [][]string
}

type collectedForbidden struct {
	gate  gate
	names []string
}

// fuseCollector is what a collection gathers.
type fuseCollector struct {
	p            *program
	contributors []collectedContributor
	conditions   []pendingCondition
	// Dependencies' required names.
	extras       []collectedExtra
	alternatives []collectedAlternative
	altGroups    []fusedAltGroup
	forbidden    []collectedForbidden
	// Alternative groups with object keywords only where coverage is not tracked (a failed branch must not cover).
	allowAltGroups bool
	// Every contributor must belong to this resource (below a live dynamic reference, where the general path would
	// push a contributor's own resource on the dynamic scope and the fused pass does not enter contributors).
	sameResource bool
	resource     uint32
}

func (c *fuseCollector) node(id nodeID) *schemaNode {
	return c.p.nodes[id]
}

func (c *fuseCollector) target(id nodeID) nodeID {
	return c.p.fastTarget[id]
}

func (c *fuseCollector) otherResource(n *schemaNode) bool {
	return c.sameResource && n.resourceID != c.resource
}

// collect walks the in-place applicators of a branch, adding every object branch with its condition. It fails when
// a branch cannot be fused.
func (c *fuseCollector) collect(id nodeID, condition gate) bool {
	n := c.node(id)
	if n.alwaysTrue {
		return true
	}
	if n.alwaysFalse || n.inPlaceCycle || !isObjectBranch(c.p, n, len(c.contributors) == 0) {
		return false
	}
	if len(c.contributors) >= maxFusedContributors || c.otherResource(n) {
		return false
	}
	c.contributors = append(c.contributors, collectedContributor{node: id, condition: condition})
	if n.not >= 0 {
		names, _ := forbiddenNames(c.p, n.not)
		c.forbidden = append(c.forbidden, collectedForbidden{condition, names})
	}
	for _, r := range [2]nodeID{n.ref, n.staticDynamicRef} {
		if r >= 0 && !c.collect(c.target(r), condition) {
			return false
		}
	}
	for _, child := range n.allOf {
		if !c.collect(c.target(child), condition) {
			return false
		}
	}
	if n.hasOneOf {
		discriminated := n.oneOfDiscriminator != nil || c.typeUnion(n.oneOf)
		if !c.collectAlternative(n.oneOf, condition, true, discriminated) {
			return false
		}
	}
	if n.hasAnyOf {
		discriminated := n.anyOfDiscriminator != nil || c.typeUnion(n.anyOf)
		if !c.collectAlternative(n.anyOf, condition, false, discriminated) {
			return false
		}
	}
	for i := range n.dependencies {
		dep := &n.dependencies[i]
		if len(c.conditions) >= maxFusedConditions {
			return false
		}
		at := uint16(len(c.conditions))
		c.conditions = append(c.conditions, pendingCondition{dependency: dep.name, gate: condition})
		if len(dep.required) != 0 {
			c.extras = append(c.extras, collectedExtra{at, dep.required})
		}
		if dep.schema >= 0 && !c.collect(c.target(dep.schema), gate{true, at, true}) {
			return false
		}
	}
	if n.if_ >= 0 {
		testID := c.target(n.if_)
		test := c.node(testID)
		then, else_ := noNode, noNode
		if n.then >= 0 {
			then = c.target(n.then)
		}
		if n.else_ >= 0 {
			else_ = c.target(n.else_)
		}
		if test.alwaysTrue {
			return then < 0 || c.collect(then, condition)
		}
		if test.alwaysFalse {
			return else_ < 0 || c.collect(else_, condition)
		}
		if !c.supportedCondition(test) || len(c.conditions) >= maxFusedConditions {
			return false
		}
		at := uint16(len(c.conditions))
		c.conditions = append(c.conditions, pendingCondition{isTest: true, test: testID, gate: condition})
		// When the condition holds, the if schema's own properties count as evaluated, so it contributes under the
		// same condition as then.
		if test.hasProperties && !c.collect(testID, gate{true, at, true}) {
			return false
		}
		if then >= 0 && !c.collect(then, gate{true, at, true}) {
			return false
		}
		if else_ >= 0 && !c.collect(else_, gate{true, at, false}) {
			return false
		}
	}
	return true
}

// typeUnion reports that every branch only tests the type (the general path decides the keyword by one mask test).
func (c *fuseCollector) typeUnion(list []nodeID) bool {
	for _, id := range list {
		if b := c.node(c.target(id)); !(b.hasType && isTypeOnly(b)) {
			return false
		}
	}
	return true
}

// collectAlternative fuses a oneOf/anyOf when every branch is a plain required list (decided from the seen names
// after the pass), or as an alternative group of object branches where coverage is not tracked.
func (c *fuseCollector) collectAlternative(list []nodeID, condition gate, exactlyOne, discriminated bool) bool {
	branches := make([]nodeID, len(list))
	requiredOnly := true
	required := make([][]string, len(list))
	for i, id := range list {
		branches[i] = c.target(id)
		n := c.node(branches[i])
		if len(n.required) != 0 && isRequiredListOnly(n) {
			required[i] = n.required
		} else {
			requiredOnly = false
		}
	}
	if requiredOnly {
		c.alternatives = append(c.alternatives, collectedAlternative{condition, exactlyOne, required})
		return true
	}
	// Branches with object keywords: each a contributor whose failure marks the branch rather than the object. The
	// pass applies every branch that knows a name to that property, where the general path stops at the first
	// branch that passes, so a name with an expensive child in more than one branch is refused. A keyword the
	// general path decides by discriminator or type stays with it.
	if !c.allowAltGroups || discriminated || condition.set || len(c.altGroups) >= maxFusedAltGroups ||
		len(branches) > 64 || !c.expensiveChildrenDisjoint(branches) {
		return false
	}
	group := uint16(len(c.altGroups))
	for i, b := range branches {
		n := c.node(b)
		if n.alwaysTrue || n.alwaysFalse || n.inPlaceCycle || n.hasInPlaceApplicators() || n.hasDependencies ||
			n.not >= 0 || !isObjectBranch(c.p, n, false) || c.otherResource(n) ||
			len(c.contributors) >= maxFusedContributors {
			return false
		}
		c.contributors = append(c.contributors, collectedContributor{node: b, alt: altBranch{true, group, uint16(i)}})
	}
	c.altGroups = append(c.altGroups, fusedAltGroup{exactlyOne, uint32(len(branches))})
	return true
}

// expensiveChildrenDisjoint reports whether no property name gets an expensive child (anything but a leaf or a
// boolean schema) from more than one branch. Pattern and additional properties count as every name.
func (c *fuseCollector) expensiveChildrenDisjoint(branches []nodeID) bool {
	cheap := func(id nodeID) bool {
		n := c.node(c.target(id))
		return n.alwaysTrue || n.alwaysFalse || isLeaf(n) || c.isSimpleArray(n)
	}
	var expensive []string
	wildcards := 0
	for _, b := range branches {
		n := c.node(b)
		for _, property := range n.properties {
			if !cheap(property.node) {
				if containsString(expensive, property.name) {
					return false
				}
				expensive = append(expensive, property.name)
			}
		}
		wildcard := n.additionalProperties >= 0 && !cheap(n.additionalProperties)
		for _, pp := range n.patternProperties {
			wildcard = wildcard || !cheap(pp.node)
		}
		if wildcard {
			if wildcards++; wildcards > 1 {
				return false
			}
		}
	}
	return wildcards == 0 || len(expensive) == 0
}

// isSimpleArray reports an array of leaf items with at most size bounds: cheap to apply more than once.
func (c *fuseCollector) isSimpleArray(n *schemaNode) bool {
	if !n.hasArrayKeywords() || n.hasObjectKeywords() || n.hasInPlaceApplicators() || n.dynamicRef != nil ||
		n.constValue.set() || n.hasEnum || n.hasPrefixItems || n.contains >= 0 || n.uniqueItems ||
		n.unevaluatedItems >= 0 || n.items < 0 {
		return false
	}
	item := c.node(c.target(n.items))
	return item.alwaysTrue || isLeaf(item)
}

// supportedCondition reports a condition the pass decides alone: required names, and properties whose schemas are
// value tests, optionally with type: object (which the object plan has established). At least one of the two.
func (c *fuseCollector) supportedCondition(test *schemaNode) bool {
	any := len(test.required) != 0
	if test.constValue.set() || test.hasEnum || test.hasNumberKeywords() || test.hasStringKeywords() ||
		test.hasArrayKeywords() || test.hasInPlaceApplicators() || test.dynamicRef != nil ||
		(test.hasType && test.typeMask&typeObject == 0) || test.additionalProperties >= 0 ||
		test.propertyNames >= 0 || test.unevaluatedProperties >= 0 || test.hasDependencies ||
		test.minProperties.set || test.maxProperties.set {
		return false
	}
	for _, property := range test.properties {
		if _, ok := valueTestOf(c.node(c.target(property.node))); !ok {
			return false
		}
		any = true
	}
	// patternProperties: {P: false}: no property name may match P.
	for _, pp := range test.patternProperties {
		if !c.node(c.target(pp.node)).alwaysFalse {
			return false
		}
		any = true
	}
	return any
}

// isObjectBranch reports a branch whose only effect on an object instance is through object keywords and fusable
// in-place applicators.
func isObjectBranch(p *program, n *schemaNode, allowUnevaluated bool) bool {
	if n.not >= 0 {
		if _, ok := forbiddenNames(p, n.not); !ok {
			return false
		}
	}
	return !(n.constValue.set() || n.hasEnum || n.hasNumberKeywords() || n.hasStringKeywords() ||
		(n.hasType && n.typeMask&typeObject == 0) || n.propertyNames >= 0 || n.dynamicRef != nil ||
		(!allowUnevaluated && n.unevaluatedProperties >= 0))
}

func isTypeOnly(n *schemaNode) bool {
	return isLeaf(n) && !n.constValue.set() && !n.hasEnum && !n.hasNumberKeywords() && !n.hasStringKeywords()
}

// isLeaf reports only local keywords (type, const, enum, number and string keywords).
func isLeaf(n *schemaNode) bool {
	return !n.alwaysTrue && !n.alwaysFalse && !n.hasObjectKeywords() && !n.hasArrayKeywords() &&
		!n.hasInPlaceApplicators() && n.dynamicRef == nil
}

// forbiddenNames are the names of a not whose schema is a non-empty required list: an object fails when all are
// present.
func forbiddenNames(p *program, not nodeID) ([]string, bool) {
	n := p.nodes[p.fastTarget[not]]
	if n.alwaysTrue || n.alwaysFalse || !isRequiredListOnly(n) || len(n.required) == 0 {
		return nil, false
	}
	return n.required, true
}

func isRequiredListOnly(n *schemaNode) bool {
	return !(n.constValue.set() || n.hasEnum || n.hasNumberKeywords() || n.hasStringKeywords() ||
		n.hasArrayKeywords() || n.hasInPlaceApplicators() || n.dynamicRef != nil || n.unevaluatedProperties >= 0 ||
		n.unevaluatedItems >= 0 || (n.hasType && n.typeMask&typeObject == 0) || n.hasPatternProperties ||
		n.additionalProperties >= 0 || n.propertyNames >= 0 || n.hasDependencies || n.minProperties.set ||
		n.maxProperties.set || len(n.properties) != 0)
}

// valueTestOf is the value test for a property schema inside an if: const/enum of scalars (the values the schema's
// type admits), or a pattern with at most type: string. Not ok for anything else.
func valueTestOf(n *schemaNode) (valueTest, bool) {
	if n.alwaysTrue || n.alwaysFalse || n.hasNumberKeywords() || n.hasObjectKeywords() || n.hasArrayKeywords() ||
		n.hasInPlaceApplicators() || n.dynamicRef != nil {
		return valueTest{}, false
	}
	if n.pattern != nil {
		onlyPattern := !n.constValue.set() && !n.hasEnum && !n.minLength.set && !n.maxLength.set &&
			!(n.assertFormat && n.hasFormat) && !n.assertContent && (!n.hasType || n.typeMask == typeString)
		return valueTest{isPattern: true, pattern: n.pattern, requiresString: n.hasType}, onlyPattern
	}
	if n.hasStringKeywords() {
		return valueTest{}, false
	}
	var values []valueRef
	switch {
	case n.constValue.set():
		values = []valueRef{n.constValue}
	case len(n.enumValues) != 0:
		values = n.enumValues
	default:
		return valueTest{}, false
	}
	test := valueTest{}
	for _, v := range values {
		switch v.d.kind(v.n) {
		case kindNumber:
			if !isIntegerNumber(v.d.flags(v.n), v.d.data(v.n)) {
				return valueTest{}, false
			}
		case kindString, kindBool, kindNull:
		default:
			return valueTest{}, false
		}
		// A value the schema's type rejects never passes, so it is not allowed.
		if !n.hasType || typeOK(n.typeMask, v.d, v.n) {
			test.allowed = append(test.allowed, v)
		}
	}
	return test, true
}

// collectedApp is one resolution of a property by a contributor, before identical ones are merged.
type collectedApp struct {
	contributor uint16
	child       optChild
	target      nodeID
}

// tryFuse builds the fused plan for a node, or nil when it cannot be fused or fusing does not pay.
func tryFuse(p *program, id nodeID, sameResource bool, childOf func(nodeID) child) *fusedObject {
	n := p.nodes[id]
	if !isObjectBranch(p, n, true) || n.inPlaceCycle || n.alwaysTrue || n.alwaysFalse {
		return nil
	}
	ctx := &fuseCollector{
		p: p, allowAltGroups: n.unevaluatedProperties < 0, sameResource: sameResource, resource: n.resourceID,
	}
	if !ctx.collect(id, gate{}) {
		return nil
	}
	if len(ctx.contributors)+len(ctx.extras) > maxFusedContributors || len(ctx.conditions) > maxFusedConditions {
		return nil
	}

	// Fusing pays when a pass over every property is unavoidable (unevaluatedProperties) or when it replaces
	// several passes: two or more branches with object keywords, an if the seen names decide, or alternatives. A
	// node whose object keywords are all its own keeps its object plan.
	hasIf := false
	for i := range ctx.conditions {
		hasIf = hasIf || ctx.conditions[i].isTest
	}
	if n.unevaluatedProperties < 0 && !hasIf && len(ctx.alternatives) == 0 && len(ctx.altGroups) == 0 {
		effective := 0
		for _, contributor := range ctx.contributors {
			b := p.nodes[contributor.node]
			if b.hasProperties || b.hasPatternProperties || b.additionalProperties >= 0 || len(b.required) != 0 ||
				b.minProperties.set || b.maxProperties.set {
				effective++
			}
		}
		if effective < 2 {
			return nil
		}
	}

	// Every name any branch or condition knows gets an index.
	var known []string
	bit := func(name string) uint16 {
		for i, k := range known {
			if k == name {
				return uint16(i)
			}
		}
		known = append(known, name)
		return uint16(len(known) - 1)
	}
	bitsOf := func(list []string) []uint16 {
		out := make([]uint16, len(list))
		for i, name := range list {
			out[i] = bit(name)
		}
		return out
	}
	for _, contributor := range ctx.contributors {
		b := p.nodes[contributor.node]
		for _, property := range b.properties {
			bit(property.name)
		}
		for _, name := range b.required {
			bit(name)
		}
	}
	f := &fusedObject{altGroups: ctx.altGroups}
	type entryTest struct {
		entry uint16
		test  valueTest
	}
	var testsByEntry []entryTest
	for i := range ctx.conditions {
		pending := &ctx.conditions[i]
		var required []uint16
		if pending.isTest {
			test := p.nodes[pending.test]
			for _, pp := range test.patternProperties {
				f.absent = append(f.absent, fusedAbsent{uint16(i), pp.pattern})
			}
			for _, property := range test.properties {
				vt, _ := valueTestOf(p.nodes[p.fastTarget[property.node]])
				vt.condition = uint16(i)
				testsByEntry = append(testsByEntry, entryTest{bit(property.name), vt})
			}
			required = bitsOf(test.required)
		} else {
			required = []uint16{bit(pending.dependency)}
		}
		f.conditions = append(f.conditions, fusedCondition{required: required, gate: pending.gate})
	}
	for _, forbidden := range ctx.forbidden {
		f.forbidden = append(f.forbidden, fusedForbidden{forbidden.gate, bitsOf(forbidden.names)})
	}
	type extraBits struct {
		condition uint16
		names     []uint16
	}
	extras := make([]extraBits, len(ctx.extras))
	for i, extra := range ctx.extras {
		extras[i] = extraBits{extra.condition, bitsOf(extra.names)}
	}
	for _, alternative := range ctx.alternatives {
		branches := make([][]uint16, len(alternative.branches))
		for i, branch := range alternative.branches {
			branches[i] = bitsOf(branch)
		}
		f.alternatives = append(f.alternatives, fusedAlternative{alternative.condition, alternative.exactlyOne, branches})
	}
	if len(known) > maxFusedNames {
		return nil
	}

	appChild := func(c nodeID) optChild {
		t := p.fastTarget[c]
		if p.nodes[t].alwaysTrue {
			return optChild{}
		}
		return optChild{true, childOf(t)}
	}
	for _, contributor := range ctx.contributors {
		b := p.nodes[contributor.node]
		fc := fusedContributor{
			condition: contributor.condition, alt: contributor.alt, required: bitsOf(b.required),
			min: b.minProperties, max: b.maxProperties,
		}
		for _, pp := range b.patternProperties {
			fc.patterns = append(fc.patterns, fusedPattern{pp.pattern, appChild(pp.node)})
		}
		if b.additionalProperties >= 0 {
			fc.hasAdditional, fc.additional = true, appChild(b.additionalProperties)
		}
		f.contributors = append(f.contributors, fc)
	}
	for _, extra := range extras {
		f.contributors = append(f.contributors, fusedContributor{
			condition: gate{true, extra.condition, true}, required: extra.names,
		})
	}

	tests := make([][]valueTest, len(known))
	for _, t := range testsByEntry {
		tests[t.entry] = append(tests[t.entry], t.test)
	}
	// A known name matching an absent pattern fails its condition whatever its value: no constant is allowed.
	for e, name := range known {
		for _, absent := range f.absent {
			if absent.pattern.matchString(name) {
				tests[e] = append(tests[e], valueTest{condition: absent.condition})
			}
		}
	}

	// Resolve every known name against every branch now.
	f.entries = make([]fusedEntry, len(known))
	for i, name := range known {
		var apps []collectedApp
		for c, contributor := range ctx.contributors {
			b := p.nodes[contributor.node]
			matched := false
			for _, property := range b.properties {
				if property.name == name {
					matched = true
					apps = append(apps, collectedApp{uint16(c), appChild(property.node), p.fastTarget[property.node]})
					break
				}
			}
			for _, pp := range b.patternProperties {
				if pp.pattern.matchString(name) {
					matched = true
					apps = append(apps, collectedApp{uint16(c), appChild(pp.node), p.fastTarget[pp.node]})
				}
			}
			if !matched && b.additionalProperties >= 0 {
				a := b.additionalProperties
				apps = append(apps, collectedApp{uint16(c), appChild(a), p.fastTarget[a]})
			}
		}
		f.entries[i] = fusedEntry{
			apps: coalesceApps(apps, f.contributors), tests: tests[i], merged: mergeValueTests(tests[i]),
		}
	}

	flat := len(f.conditions) == 0 && len(f.alternatives) == 0 && len(ctx.altGroups) == 0 &&
		len(f.forbidden) == 0 && n.unevaluatedProperties < 0 && len(known) <= 64
	for i := range f.contributors {
		c := &f.contributors[i]
		flat = flat && !c.condition.set && len(c.patterns) == 0 && !c.hasAdditional
		f.resolvesUnknown = f.resolvesUnknown || len(c.patterns) != 0 || c.hasAdditional
		f.hasCountBounds = f.hasCountBounds || c.min.set || c.max.set
	}
	for i := range f.entries {
		flat = flat && len(f.entries[i].apps) <= 1 && len(f.entries[i].tests) == 0
	}
	f.resolvesUnknown = f.resolvesUnknown || len(f.absent) != 0
	f.names = newNames(known)
	if flat {
		o := &objectPlan{
			max: ^uint64(0), visit: visitNames, names: f.names, declared: len(known), propertyNames: noNode,
			children: make([]child, len(known)), namePatterns: make([][]uint16, len(known)),
			restFree: true, strict: true,
			// No contributor has additionalProperties (flat requires it).
			lookup: len(known) <= lookupNames,
		}
		for i := range f.contributors {
			c := &f.contributors[i]
			for _, r := range c.required {
				o.requiredMask |= 1 << r
			}
			if c.min.set && c.min.n > o.min {
				o.min = c.min.n
			}
			if c.max.set && c.max.n < o.max {
				o.max = c.max.n
			}
		}
		for i := range f.entries {
			o.children[i] = noChild
			if apps := f.entries[i].apps; len(apps) != 0 && apps[0].hasChild {
				o.children[i] = apps[0].child
			}
		}
		f.flat = o
	}
	if n.unevaluatedProperties >= 0 {
		f.hasUnevaluated, f.unevaluated = true, childOf(p.fastTarget[n.unevaluatedProperties])
	}
	return f
}

// mergeValueTests is the merged constants of an entry's value tests, when some (of at most 64) are constant sets
// and there is more than one constant to look for.
func mergeValueTests(tests []valueTest) *mergedTests {
	constants := 0
	for i := range tests {
		if !tests[i].isPattern {
			constants += len(tests[i].allowed)
		}
	}
	if len(tests) > 64 || constants < 2 {
		return nil
	}
	m := &mergedTests{}
	var strings []string
	for t := range tests {
		if tests[t].isPattern {
			continue
		}
		m.keyed |= 1 << t
	values:
		for _, v := range tests[t].allowed {
			if v.d.kind(v.n) == kindString {
				s := string(v.d.str(v.n))
				for i := range strings {
					if strings[i] == s {
						m.stringMasks[i] |= 1 << t
						continue values
					}
				}
				strings = append(strings, s)
				m.stringMasks = append(m.stringMasks, 1<<t)
				continue
			}
			for i := range m.others {
				if valuesEqual(m.others[i].value.d, m.others[i].value.n, v.d, v.n) {
					m.others[i].mask |= 1 << t
					continue values
				}
			}
			m.others = append(m.others, maskedValue{v, 1 << t})
		}
	}
	m.strings = newNames(strings)
	return m
}

// coalesceApps merges identical resolutions of a property from several branches (the same child, or both true)
// into one application listing every branch, applied once when any of them is active. Branches of an alternative
// group merge only within the same branch. The primary contributor is an unconditional one when there is one, so
// the pass applies it at once.
func coalesceApps(apps []collectedApp, contributors []fusedContributor) []fusedApp {
	same := func(a, b *collectedApp) bool {
		if !a.child.set || !b.child.set {
			return a.child.set == b.child.set
		}
		x, y := a.child.child, b.child.child
		return a.target == b.target || (x.shape == shapeTrivial && y.shape == shapeTrivial && x.types == y.types)
	}
	used := make([]bool, len(apps))
	var out []fusedApp
	for i := range apps {
		if used[i] {
			continue
		}
		primary := i
		var others []uint16
		for j := i + 1; j < len(apps); j++ {
			a, b := &contributors[apps[primary].contributor], &contributors[apps[j].contributor]
			if used[j] || !same(&apps[primary], &apps[j]) || a.alt != b.alt {
				continue
			}
			used[j] = true
			if a.condition.set && !b.condition.set {
				others = append(others, apps[primary].contributor)
				primary = j
			} else {
				others = append(others, apps[j].contributor)
			}
		}
		out = append(out, fusedApp{
			contributor: apps[primary].contributor, hasChild: apps[primary].child.set, child: apps[primary].child.child,
			others: others,
		})
	}
	return out
}

// ---------------------------------------------------------------------------------------------------------------------
// Evaluation

// fusedPass is the state of one pass. It lives on the stack of runFused.
type fusedPass struct {
	// The entries seen.
	seen [maxFusedNames / 64]uint64
	// Bit i: condition i's value test failed.
	failed    uint64
	altFailed [maxFusedAltGroups]uint64
	holds     uint64
	gateOK    uint64
}

func (s *fusedPass) allSeen(names []uint16) bool {
	for _, i := range names {
		if s.seen[i>>6]&(1<<(i&63)) == 0 {
			return false
		}
	}
	return true
}

func (s *fusedPass) active(g gate) bool {
	if !g.set {
		return true
	}
	return s.gateOK&(1<<g.condition) != 0 && (s.holds&(1<<g.condition) != 0) == g.polarity
}

func (s *fusedPass) failAlt(alt altBranch) {
	s.altFailed[alt.group] |= 1 << alt.branch
}

// The outcome of a property in the first step.
const (
	// The object fails.
	fusedFailed uint8 = 1 << iota
	// Some branch covered the property.
	fusedCover
	// Conditional applications are pending.
	fusedDefer
)

func (e *evaluator) applyOpt(c optChild, v int) bool {
	return !c.set || e.runChild(c.child, v)
}

// resolveUnknown resolves a name no entry knows against one branch's pattern and additional properties. It returns
// whether it matched (the property is covered), and false when the application failed.
func (e *evaluator) resolveUnknown(c *fusedContributor, name []byte, ascii bool, v int) (matched, ok bool) {
	for i := range c.patterns {
		if c.patterns[i].pattern.match(name, ascii) {
			matched = true
			if !e.applyOpt(c.patterns[i].child, v) {
				return false, false
			}
		}
	}
	if !matched && c.hasAdditional {
		matched = true
		if !e.applyOpt(c.additional, v) {
			return false, false
		}
	}
	return matched, true
}

func (e *evaluator) fusedEntry(f *fusedObject, index int, v int, pass *fusedPass) uint8 {
	d := e.d
	entry := &f.entries[index]
	pass.seen[index>>6] |= 1 << (index & 63)
	if len(entry.tests) != 0 {
		allowed, keyed := uint64(0), uint64(0)
		if entry.merged != nil {
			allowed, keyed = entry.merged.allowed(d, v), entry.merged.keyed
		}
		for t := range entry.tests {
			test := &entry.tests[t]
			var holds bool
			if keyed&(1<<t) != 0 {
				holds = allowed&(1<<t) != 0
			} else {
				holds = test.holds(d, v)
			}
			if !holds {
				pass.failed |= 1 << test.condition
			}
		}
	}
	outcome := uint8(0)
	for i := range entry.apps {
		app := &entry.apps[i]
		c := &f.contributors[app.contributor]
		if c.condition.set {
			outcome |= fusedDefer
			continue
		}
		if app.hasChild && !e.runChild(app.child, v) {
			if !c.alt.set {
				return fusedFailed
			}
			pass.failAlt(c.alt)
			continue
		}
		outcome |= fusedCover
	}
	return outcome
}

func (e *evaluator) fusedUnknown(f *fusedObject, name []byte, ascii bool, v int, pass *fusedPass) uint8 {
	if !f.resolvesUnknown {
		return 0
	}
	for i := range f.absent {
		a := &f.absent[i]
		if pass.failed&(1<<a.condition) == 0 && a.pattern.match(name, ascii) {
			pass.failed |= 1 << a.condition
		}
	}
	outcome := uint8(0)
	for i := range f.contributors {
		c := &f.contributors[i]
		if c.condition.set {
			if len(c.patterns) != 0 || c.hasAdditional {
				outcome |= fusedDefer
			}
			continue
		}
		matched, ok := e.resolveUnknown(c, name, ascii, v)
		switch {
		case !ok && !c.alt.set:
			return fusedFailed
		case !ok:
			pass.failAlt(c.alt)
		case matched:
			outcome |= fusedCover
		}
	}
	return outcome
}

func (e *evaluator) runFused(f *fusedObject, x int) bool {
	if f.flat != nil {
		return e.runStrictObject(f.flat, x)
	}
	var pass fusedPass
	// Only a pass that tracks the covered properties, or an object of more than 64 properties, takes sets from the
	// arena, and so the evaluation's buffers from the pool.
	if !f.hasUnevaluated && e.d.count(x) <= 64 {
		return e.runFusedPass(f, x, &pass)
	}
	mark := len(e.state().arena)
	ok := e.runFusedPass(f, x, &pass)
	e.s.arena = e.s.arena[:mark]
	return ok
}

func countOK(c *fusedContributor, count uint64) bool {
	return (!c.min.set || count >= c.min.n) && (!c.max.set || count <= c.max.n)
}

func (e *evaluator) runFusedPass(f *fusedObject, x int, pass *fusedPass) bool {
	d := e.d
	count := d.count(x)
	if f.hasCountBounds {
		for i := range f.contributors {
			c := &f.contributors[i]
			if !c.condition.set && !c.alt.set && !countOK(c, uint64(count)) {
				return false
			}
		}
	}
	// The covered properties (for unevaluatedProperties) and, beyond the first 64 (which a mask holds), the
	// properties with conditional applications pending, by ordinal.
	covered, deferredBeyond := noBits, noBits
	if f.hasUnevaluated {
		covered = e.newBits(count)
	}
	if count > 64 {
		deferredBeyond = e.newBits(count)
	}
	deferred := uint64(0)
	pending := 0
	hint := 0
	ns := f.names
	first := d.first(x)
	for ordinal := 0; ordinal < count; ordinal++ {
		k := first + 2*ordinal
		name := d.str(k)
		w := nameWord(name)
		var index int
		if ns.at(hint, len(name), w) && (len(name) <= 8 || ns.m.rest(hint, name)) {
			index, hint = hint, hint+1
		} else {
			index, hint = ns.findAfter(name, w, hint)
		}
		var outcome uint8
		if index >= 0 {
			outcome = e.fusedEntry(f, index, k+1, pass)
		} else {
			outcome = e.fusedUnknown(f, name, d.strASCII(k), k+1, pass)
		}
		if outcome&fusedFailed != 0 {
			return false
		}
		if outcome&fusedCover != 0 && covered.tracked() {
			e.setBit(covered, ordinal)
		}
		if outcome&fusedDefer != 0 {
			pending++
			if ordinal < 64 {
				deferred |= 1 << ordinal
			} else {
				e.setBit(deferredBeyond, ordinal)
			}
		}
	}

	// Decide the conditions, then which apply along their gates (a gate precedes the conditions under it).
	for i := range f.conditions {
		if pass.failed&(1<<i) == 0 && pass.allSeen(f.conditions[i].required) {
			pass.holds |= 1 << i
		}
	}
	for i := range f.conditions {
		if pass.active(f.conditions[i].gate) {
			pass.gateOK |= 1 << i
		}
	}

	for ordinal := 0; ordinal < count && pending > 0; ordinal++ {
		if ordinal < 64 {
			if deferred&(1<<ordinal) == 0 {
				continue
			}
		} else if !e.getBit(deferredBeyond, ordinal) {
			continue
		}
		pending--
		k := first + 2*ordinal
		name, v := d.str(k), k+1
		cover := false
		if index := f.names.find(name); index >= 0 {
			apps := f.entries[index].apps
			for i := range apps {
				app := &apps[i]
				c := &f.contributors[app.contributor]
				if !c.condition.set {
					continue
				}
				active := pass.active(c.condition)
				for _, other := range app.others {
					active = active || pass.active(f.contributors[other].condition)
				}
				if active {
					if app.hasChild && !e.runChild(app.child, v) {
						return false
					}
					cover = true
				}
			}
		} else {
			for i := range f.contributors {
				c := &f.contributors[i]
				if c.condition.set && pass.active(c.condition) {
					matched, ok := e.resolveUnknown(c, name, d.strASCII(k), v)
					if !ok {
						return false
					}
					cover = cover || matched
				}
			}
		}
		if cover && covered.tracked() {
			e.setBit(covered, ordinal)
		}
	}

	for i := range f.contributors {
		c := &f.contributors[i]
		if c.condition.set {
			if !pass.active(c.condition) {
				continue
			}
			if !countOK(c, uint64(count)) {
				return false
			}
		}
		if (c.alt.set && !countOK(c, uint64(count))) || !pass.allSeen(c.required) {
			if !c.alt.set {
				return false
			}
			pass.failAlt(c.alt)
		}
	}

	for g := range f.altGroups {
		group := &f.altGroups[g]
		all := ^uint64(0)
		if group.count < 64 {
			all = 1<<group.count - 1
		}
		survivors := ^pass.altFailed[g] & all
		if survivors == 0 || (group.exactlyOne && bits.OnesCount64(survivors) != 1) {
			return false
		}
	}

	for i := range f.forbidden {
		if pass.active(f.forbidden[i].gate) && pass.allSeen(f.forbidden[i].names) {
			return false
		}
	}

	for i := range f.alternatives {
		a := &f.alternatives[i]
		if !pass.active(a.condition) {
			continue
		}
		matches := 0
		for _, branch := range a.branches {
			if pass.allSeen(branch) {
				matches++
			}
		}
		if matches == 0 || (a.exactlyOne && matches != 1) {
			return false
		}
	}

	if f.hasUnevaluated {
		for ordinal := 0; ordinal < count; ordinal++ {
			if !e.getBit(covered, ordinal) && !e.runChild(f.unevaluated, first+2*ordinal+1) {
				return false
			}
		}
	}
	return true
}
