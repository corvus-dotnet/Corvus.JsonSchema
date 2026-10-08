package jsonschema

import (
	"math"
	"sort"
	"strconv"
)

// Compiles loaded schema documents into a schemaNode graph and runs the compile-time analyses.

// knownKeywords are the keywords compileNode handles itself. Anything else is an unknown keyword (an annotation
// from 2019-09).
var knownKeywords = map[string]bool{
	"if": true, "not": true, "type": true, "enum": true, "$ref": true, "then": true, "else": true, "const": true,
	"items": true, "allOf": true, "anyOf": true, "oneOf": true, "title": true, "$defs": true, "format": true,
	"pattern": true, "maximum": true, "minimum": true, "default": true, "$schema": true, "$anchor": true,
	"required": true, "contains": true, "maxItems": true, "minItems": true, "examples": true, "readOnly": true,
	"$comment": true, "maxLength": true, "minLength": true, "writeOnly": true, "properties": true,
	"multipleOf": true, "deprecated": true, "uniqueItems": true, "prefixItems": true, "minContains": true,
	"maxContains": true, "description": true, "$vocabulary": true, "definitions": true, "$dynamicRef": true,
	"dependencies": true, "propertyNames": true, "maxProperties": true, "minProperties": true,
	"contentSchema": true, "$recursiveRef": true, "$dynamicAnchor": true, "contentEncoding": true,
	"additionalItems": true, "exclusiveMaximum": true, "exclusiveMinimum": true, "unevaluatedItems": true,
	"contentMediaType": true, "$recursiveAnchor": true, "dependentSchemas": true, "patternProperties": true,
	"dependentRequired": true, "additionalProperties": true, "unevaluatedProperties": true, "id": true, "$id": true,
}

type pendingDynamicRef struct {
	node          nodeID
	anchor        string
	isRecursive   bool
	initialTarget schemaTarget
	seenResources []bool
	candidates    []resourceNode
}

// annotationSource is what a node's annotations are computed from, on the first evaluation with a collector.
type annotationSource struct {
	set     bool
	doc     *Document
	value   int
	vocab   uint32
	content bool
}

// compiledSchema is the compiled program: the node graph, its entry node and whether it keeps a dynamic scope.
type compiledSchema struct {
	nodes             []*schemaNode
	root              nodeID
	usesDynamicScope  bool
	annotationSources []annotationSource
}

type nodeKey struct {
	document uint32
	value    int
}

type schemaCompiler struct {
	loader             *schemaLoader
	options            *compileOptions
	nodes              []*schemaNode
	targets            []schemaTarget
	nodeOf             map[nodeKey]nodeID
	worklistHead       int
	pendingDynamicRefs []*pendingDynamicRef
	annotationSources  []annotationSource
	entryNode          nodeID
}

// uintValue is a non-negative integer keyword value.
func uintValue(d *Document, n int) optCount {
	if n < 0 || d.kind(n) != kindNumber {
		return optCount{}
	}
	switch d.flags(n) {
	case numInt:
		if v := int64(d.data(n)); v >= 0 {
			return optCount{true, uint64(v)}
		}
		return optCount{}
	case numUint:
		return optCount{true, d.data(n)}
	}
	f := math.Float64frombits(d.data(n))
	if f >= 0 && f == math.Floor(f) && f < 1.8e19 {
		return optCount{true, uint64(f)}
	}
	return optCount{}
}

func typeMaskOf(d *Document, n int) uint8 {
	if d.kind(n) != kindString {
		return 0
	}
	switch string(d.str(n)) {
	case "null":
		return typeNull
	case "boolean":
		return typeBoolean
	case "object":
		return typeObject
	case "array":
		return typeArray
	case "number":
		return typeNumber
	case "string":
		return typeString
	case "integer":
		return typeInteger
	}
	return 0
}

// stringItems are the strings of an array (other items are skipped).
func stringItems(d *Document, array int) []string {
	out := make([]string, 0, d.count(array))
	c := d.first(array)
	for i := 0; i < d.count(array); i++ {
		if d.kind(c+i) == kindString {
			out = append(out, string(d.str(c+i)))
		}
	}
	return out
}

func containsString(list []string, s string) bool {
	for _, x := range list {
		if x == s {
			return true
		}
	}
	return false
}

func compileDocument(schema *Document, options *compileOptions) (*compiledSchema, error) {
	loader := newSchemaLoader(options)
	root, err := loader.loadRoot(schema)
	if err != nil {
		return nil, err
	}
	return compileLoaded(loader, root, options)
}

func compileFromURI(uri string, options *compileOptions) (*compiledSchema, error) {
	loader := newSchemaLoader(options)
	root, err := loader.loadRootFromURI(uri)
	if err != nil {
		return nil, err
	}
	return compileLoaded(loader, root, options)
}

func compileLoaded(loader *schemaLoader, rootResource uint32, options *compileOptions) (*compiledSchema, error) {
	target := loader.rootTarget(rootResource)
	if options.hasEntryPoint {
		t, err := loader.tryResolveReference(rootResource, options.entryPoint)
		if err != nil {
			return nil, err
		}
		if t == nil {
			return nil, compileError("Unable to resolve the entry point '" + options.entryPoint + "'.")
		}
		target = *t
	}
	c := &schemaCompiler{loader: loader, options: options, nodeOf: make(map[nodeKey]nodeID)}
	c.entryNode = c.getNode(target)
	if err := c.compileAll(); err != nil {
		return nil, err
	}
	usesDynamicScope := false
	for _, n := range c.nodes {
		usesDynamicScope = usesDynamicScope || n.dynamicRef != nil
	}
	c.analyse()
	return &compiledSchema{
		nodes: c.nodes, root: c.entryNode, usesDynamicScope: usesDynamicScope, annotationSources: c.annotationSources,
	}, nil
}

func (c *schemaCompiler) getNode(target schemaTarget) nodeID {
	key := nodeKey{target.document, target.value}
	if id, ok := c.nodeOf[key]; ok {
		return id
	}
	id := nodeID(len(c.nodes))
	resource := c.loader.resources[target.resource]
	c.nodes = append(c.nodes, newSchemaNode(target.resource, resource.dialect, target.pointer))
	c.targets = append(c.targets, target)
	c.annotationSources = append(c.annotationSources, annotationSource{})
	c.nodeOf[key] = id
	return id
}

func (c *schemaCompiler) drainWorklist() error {
	for c.worklistHead < len(c.nodes) {
		id := c.worklistHead
		c.worklistHead++
		if err := c.compileNode(nodeID(id), c.targets[id]); err != nil {
			return err
		}
	}
	return nil
}

func (c *schemaCompiler) compileAll() error {
	for {
		if err := c.drainWorklist(); err != nil {
			return err
		}
		if len(c.pendingDynamicRefs) == 0 || (!c.expandDynamicRefs() && c.worklistHead == len(c.nodes)) {
			break
		}
	}
	// Most schemas have no dynamic reference: skip the finalisation.
	if len(c.pendingDynamicRefs) != 0 {
		return c.finalizeDynamicRefs()
	}
	return nil
}

func (c *schemaCompiler) child(parent schemaTarget, value int, relative string) nodeID {
	resource, ok := c.loader.resourceOf(parent.document, value)
	if !ok {
		resource = parent.resource
	}
	return c.getNode(schemaTarget{
		document: parent.document, pointer: parent.pointer + relative, value: value, resource: resource,
	})
}

func (c *schemaCompiler) childArray(parent schemaTarget, d *Document, value int, keyword string) ([]nodeID, bool) {
	if !isKind(d, value, kindArray) {
		return nil, false
	}
	out := make([]nodeID, d.count(value))
	first := d.first(value)
	for i := range out {
		out[i] = c.child(parent, first+i, "/"+keyword+"/"+strconv.Itoa(i))
	}
	return out, true
}

func (c *schemaCompiler) compileNode(id nodeID, target schemaTarget) error {
	d := c.loader.documents[target.document].doc
	e := target.value
	n := c.nodes[id]
	switch d.kind(e) {
	case kindBool:
		n.alwaysTrue = d.boolean(e)
		n.alwaysFalse = !n.alwaysTrue
		return nil
	case kindObject:
	default:
		n.alwaysTrue = true
		return nil
	}
	get := func(name string) int { return d.property(e, name) }
	has := func(name string) bool { return d.property(e, name) >= 0 }

	resource := c.loader.resources[target.resource]
	dialect, voc := resource.dialect, resource.vocabularies
	legacy := dialect.isLegacy()

	if legacy {
		if r, ok := stringValue(d, get("$ref")); ok {
			// In draft 7 and earlier, $ref replaces every sibling keyword.
			return c.compileRef(id, target, r)
		}
	}

	applicator := legacy || voc&vocabApplicator != 0
	validation := legacy || voc&vocabValidation != 0
	unevaluated := voc&vocabUnevaluated != 0
	if dialect == Draft201909 {
		unevaluated = applicator
	}
	content := legacy || voc&vocabContent != 0
	formatAssert := (legacy && c.options.assertFormatInLegacyDrafts) || voc&vocabFormatAssertion != 0
	if c.options.assertFormat != 0 {
		formatAssert = c.options.assertFormat > 0
	}
	single := func(name string) nodeID {
		if v := get(name); v >= 0 {
			return c.child(target, v, "/"+escapePointerToken(name))
		}
		return noNode
	}

	var dependencies []dependencyEntry
	hasDependencies := false

	// References.
	if r, ok := stringValue(d, get("$ref")); ok {
		if err := c.compileRef(id, target, r); err != nil {
			return err
		}
	}
	if dialect >= Draft202012 {
		if r, ok := stringValue(d, get("$dynamicRef")); ok {
			if err := c.compileDynamicRef(id, target, r, false); err != nil {
				return err
			}
		}
	}
	if dialect == Draft201909 {
		if r, ok := stringValue(d, get("$recursiveRef")); ok {
			if err := c.compileDynamicRef(id, target, r, true); err != nil {
				return err
			}
		}
	}

	// Applicators.
	if applicator {
		n.allOf, n.hasAllOf = c.childArray(target, d, get("allOf"), "allOf")
		n.anyOf, n.hasAnyOf = c.childArray(target, d, get("anyOf"), "anyOf")
		n.oneOf, n.hasOneOf = c.childArray(target, d, get("oneOf"), "oneOf")
		n.not = single("not")
		if dialect >= Draft7 {
			if n.if_ = single("if"); n.if_ >= 0 {
				n.then = single("then")
				n.else_ = single("else")
			}
		}
		if props := get("properties"); isKind(d, props, kindObject) {
			n.hasProperties = true
			k := d.first(props)
			for i := 0; i < d.count(props); i++ {
				name := string(d.str(k + 2*i))
				node := c.child(target, k+2*i+1, "/properties/"+escapePointerToken(name))
				n.properties = append(n.properties, namedNode{name, node})
			}
		}
		if pp := get("patternProperties"); isKind(d, pp, kindObject) {
			n.hasPatternProperties = true
			k := d.first(pp)
			for i := 0; i < d.count(pp); i++ {
				source := string(d.str(k + 2*i))
				compiled, ok := compilePattern(source)
				if !ok {
					return compileError("Invalid regular expression '" + source + "' in patternProperties.")
				}
				node := c.child(target, k+2*i+1, "/patternProperties/"+escapePointerToken(source))
				n.patternProperties = append(n.patternProperties, patternProperty{compiled, node})
			}
		}
		n.additionalProperties = single("additionalProperties")
		if dialect >= Draft6 {
			n.propertyNames = single("propertyNames")
			n.contains = single("contains")
		}

		// "dependencies" is honoured in every dialect: in 2019-09 and later it is an optional compatibility keyword.
		if isKind(d, get("dependencies"), kindObject) ||
			(dialect >= Draft201909 && isKind(d, get("dependentSchemas"), kindObject)) {
			dependencies = c.compileDependencySchemas(target, d, e, dialect)
			hasDependencies = true
		}

		// Array applicators.
		if dialect >= Draft202012 {
			n.prefixItems, n.hasPrefixItems = c.childArray(target, d, get("prefixItems"), "prefixItems")
			if v := get("items"); v >= 0 && d.kind(v) != kindArray {
				n.items = single("items")
			}
		} else if v := get("items"); v >= 0 {
			if d.kind(v) == kindArray {
				n.prefixItems, n.hasPrefixItems = c.childArray(target, d, v, "items")
				n.prefixKeyword = "items"
				if n.items = single("additionalItems"); n.items >= 0 {
					n.itemsKeyword = "additionalItems"
				}
			} else {
				n.items = single("items")
			}
		}
		n.containsMarksEvaluated = dialect >= Draft202012
	}

	if unevaluated && dialect >= Draft201909 {
		n.unevaluatedProperties = single("unevaluatedProperties")
		n.unevaluatedItems = single("unevaluatedItems")
	}

	if validation {
		if t := get("type"); t >= 0 {
			if d.kind(t) == kindArray {
				first := d.first(t)
				for i := 0; i < d.count(t); i++ {
					n.typeMask |= typeMaskOf(d, first+i)
				}
			} else {
				n.typeMask = typeMaskOf(d, t)
			}
			n.hasType = true
		}
		if dialect >= Draft6 {
			if v := get("const"); v >= 0 {
				n.constValue = valueRef{d, v}
			}
		}
		if values := get("enum"); isKind(d, values, kindArray) {
			n.hasEnum = true
			first := d.first(values)
			for i := 0; i < d.count(values); i++ {
				n.enumValues = append(n.enumValues, valueRef{d, first + i})
			}
		}
		if req := get("required"); isKind(d, req, kindArray) {
			n.hasRequired = true
			n.requiredList = stringItems(d, req)
			n.required = make([]string, 0, len(n.requiredList))
			for _, r := range n.requiredList {
				if !containsString(n.required, r) {
					n.required = append(n.required, r)
				}
			}
		}
		if dialect >= Draft201909 {
			if dr := get("dependentRequired"); isKind(d, dr, kindObject) {
				hasDependencies = true
				k := d.first(dr)
				for i := 0; i < d.count(dr); i++ {
					if v := k + 2*i + 1; d.kind(v) == kindArray {
						dependencies = append(dependencies, dependencyEntry{
							keyword: keywordDependentRequired, name: string(d.str(k + 2*i)),
							hasRequired: true, required: stringItems(d, v), schema: noNode,
						})
					}
				}
			}
		}
		n.minProperties = uintValue(d, get("minProperties"))
		n.maxProperties = uintValue(d, get("maxProperties"))
		n.minItems = uintValue(d, get("minItems"))
		n.maxItems = uintValue(d, get("maxItems"))
		if v := get("uniqueItems"); isKind(d, v, kindBool) {
			n.uniqueItems = d.boolean(v)
		}
		n.minLength = uintValue(d, get("minLength"))
		n.maxLength = uintValue(d, get("maxLength"))
		if p, ok := stringValue(d, get("pattern")); ok {
			compiled, ok := compilePattern(p)
			if !ok {
				return compileError("Invalid regular expression '" + p + "' in pattern.")
			}
			n.pattern = compiled
		}
		num := func(name string) valueRef {
			if v := get(name); isKind(d, v, kindNumber) {
				return valueRef{d, v}
			}
			return valueRef{}
		}
		if n.multipleOf = num("multipleOf"); n.multipleOf.set() {
			n.divisor = newDivisor(d, n.multipleOf.n)
		}
		isTrue := func(name string) bool {
			v := get(name)
			return isKind(d, v, kindBool) && d.boolean(v)
		}
		if dialect == Draft4 {
			// Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive.
			if isTrue("exclusiveMaximum") {
				n.exclusiveMaximum = num("maximum")
			} else {
				n.maximum = num("maximum")
			}
			if isTrue("exclusiveMinimum") {
				n.exclusiveMinimum = num("minimum")
			} else {
				n.minimum = num("minimum")
			}
		} else {
			n.maximum = num("maximum")
			n.minimum = num("minimum")
			n.exclusiveMaximum = num("exclusiveMaximum")
			n.exclusiveMinimum = num("exclusiveMinimum")
		}
		if dialect >= Draft201909 && n.contains >= 0 {
			if has("minContains") {
				n.minContains = 1
				if v := uintValue(d, get("minContains")); v.set {
					n.minContains = v.n
				}
			}
			if has("maxContains") {
				n.maxContains = uintValue(d, get("maxContains"))
			}
		}
	}

	if f, ok := stringValue(d, get("format")); ok {
		n.hasFormat = true
		n.format = f
		n.formatKind = formatKindOf(f, dialect)
		n.assertFormat = formatAssert
	}

	// Content keywords are asserted only in draft 7 (and annotations elsewhere).
	if content && dialect >= Draft7 && (has("contentEncoding") || has("contentMediaType")) {
		encoding, _ := stringValue(d, get("contentEncoding"))
		mediaType, _ := stringValue(d, get("contentMediaType"))
		switch base64, json := encoding == "base64", mediaType == "application/json"; {
		case base64 && json:
			n.content = contentBase64JSON
		case base64:
			n.content = contentBase64
		case json:
			n.content = contentJSON
		}
		n.assertContent = dialect == Draft7 && c.options.assertContent && n.content != contentNone
	}

	if hasDependencies {
		// In the order the three keywords appear in the schema.
		order := func(keyword string) int {
			k := d.first(e)
			for i := 0; i < d.count(e); i++ {
				if string(d.str(k+2*i)) == keyword {
					return i
				}
			}
			return math.MaxInt
		}
		sort.SliceStable(dependencies, func(i, j int) bool {
			return order(dependencies[i].keyword) < order(dependencies[j].keyword)
		})
	}
	n.dependencies, n.hasDependencies = dependencies, hasDependencies
	c.annotationSources[id] = annotationSource{set: true, doc: d, value: e, vocab: voc, content: content}
	return nil
}

func (c *schemaCompiler) compileDependencySchemas(target schemaTarget, d *Document, e int, dialect Dialect) []dependencyEntry {
	var out []dependencyEntry
	if deps := d.property(e, "dependencies"); isKind(d, deps, kindObject) {
		k := d.first(deps)
		for i := 0; i < d.count(deps); i++ {
			name := string(d.str(k + 2*i))
			v := k + 2*i + 1
			if d.kind(v) == kindArray {
				out = append(out, dependencyEntry{
					keyword: keywordDependencies, name: name, hasRequired: true, required: stringItems(d, v),
					schema: noNode,
				})
			} else {
				node := c.child(target, v, "/dependencies/"+escapePointerToken(name))
				out = append(out, dependencyEntry{keyword: keywordDependencies, name: name, schema: node})
			}
		}
	}
	if dialect >= Draft201909 {
		if ds := d.property(e, "dependentSchemas"); isKind(d, ds, kindObject) {
			k := d.first(ds)
			for i := 0; i < d.count(ds); i++ {
				name := string(d.str(k + 2*i))
				node := c.child(target, k+2*i+1, "/dependentSchemas/"+escapePointerToken(name))
				out = append(out, dependencyEntry{keyword: keywordDependentSchemas, name: name, schema: node})
			}
		}
	}
	return out
}

func (c *schemaCompiler) resolveOrFail(target schemaTarget, reference string) (schemaTarget, error) {
	t, err := c.loader.tryResolveReference(target.resource, reference)
	if err != nil {
		return schemaTarget{}, err
	}
	if t == nil {
		return schemaTarget{}, compileError("Unable to resolve reference '" + reference + "' from '" +
			c.loader.resources[target.resource].uri + "'.")
	}
	return *t, nil
}

func (c *schemaCompiler) compileRef(id nodeID, target schemaTarget, reference string) error {
	resolved, err := c.resolveOrFail(target, reference)
	if err != nil {
		return err
	}
	c.nodes[id].ref = c.getNode(resolved)
	return nil
}

func dynamicKeyword(isRecursive bool) string {
	if isRecursive {
		return "$recursiveRef"
	}
	return "$dynamicRef"
}

func (c *schemaCompiler) compileDynamicRef(id nodeID, target schemaTarget, reference string, isRecursive bool) error {
	resolved, err := c.resolveOrFail(target, reference)
	if err != nil {
		return err
	}
	_, rawFragment := splitFragment(reference)
	fragment := decodeFragment(rawFragment)
	res := c.loader.resources[resolved.resource]
	var dynamic bool
	if isRecursive {
		dynamic = res.recursiveAnchor && resolved.pointer == res.rootPointer
	} else if fragment != "" && fragment[0] != '/' {
		pointer, ok := res.dynamicAnchors[fragment]
		dynamic = ok && pointer == resolved.pointer
	}
	if !dynamic {
		// A static reference, kept apart from any sibling $ref (both apply).
		n := c.nodes[id]
		n.staticDynamicRef = c.getNode(resolved)
		n.staticDynamicKeyword = dynamicKeyword(isRecursive)
		return nil
	}
	c.pendingDynamicRefs = append(c.pendingDynamicRefs, &pendingDynamicRef{
		node: id, anchor: fragment, isRecursive: isRecursive, initialTarget: resolved,
	})
	return nil
}

func (c *schemaCompiler) expandDynamicRefs() bool {
	added := false
	for _, pending := range c.pendingDynamicRefs {
		for resource := 0; resource < len(c.loader.resources); resource++ {
			for len(pending.seenResources) <= resource {
				pending.seenResources = append(pending.seenResources, false)
			}
			if pending.seenResources[resource] {
				continue
			}
			pending.seenResources[resource] = true
			r := c.loader.resources[resource]
			var pointer string
			if pending.isRecursive {
				if !r.recursiveAnchor {
					continue
				}
				pointer = r.rootPointer
			} else {
				p, ok := r.dynamicAnchors[pending.anchor]
				if !ok {
					continue
				}
				pointer = p
			}
			before := len(c.nodes)
			node := c.getNode(c.targetIn(uint32(resource), pointer))
			added = added || len(c.nodes) != before
			pending.candidates = append(pending.candidates, resourceNode{uint32(resource), node})
		}
	}
	return added
}

func (c *schemaCompiler) targetIn(resource uint32, pointer string) schemaTarget {
	r := c.loader.resources[resource]
	fragment := ""
	if pointer != r.rootPointer {
		fragment = pointer[len(r.rootPointer):]
	}
	if t := c.loader.tryResolveFragment(resource, fragment); t != nil {
		return *t
	}
	return c.loader.rootTarget(resource)
}

func (c *schemaCompiler) finalizeDynamicRefs() error {
	var reachable []bool
	pending := c.pendingDynamicRefs
	c.pendingDynamicRefs = nil
	for _, p := range pending {
		fallback := c.getNode(p.initialTarget)
		setStatic := func(t nodeID) {
			n := c.nodes[p.node]
			n.staticDynamicRef = t
			n.staticDynamicKeyword = dynamicKeyword(p.isRecursive)
		}
		if len(p.candidates) <= 1 {
			// Only the initial target's resource defines the anchor: resolution is static.
			setStatic(fallback)
			continue
		}
		// The dynamic scope is searched outermost first and its outermost entry is always the resource evaluation
		// started in. When the entry resource defines the anchor, that target is the answer on every path.
		if reachable == nil {
			reachable = c.computeReachability(pending)
		}
		if !reachable[p.node] {
			setStatic(fallback)
			continue
		}
		entryResource := c.nodes[c.entryNode].resourceID
		uniform := noNode
		for _, candidate := range p.candidates {
			if candidate.resource == entryResource {
				uniform = candidate.node
				break
			}
		}
		if uniform >= 0 {
			setStatic(uniform)
			continue
		}
		c.nodes[p.node].dynamicRef = &dynamicRefTarget{
			isRecursive: p.isRecursive, fallback: fallback, byResource: p.candidates,
		}
	}
	return c.drainWorklist()
}

// computeReachability marks the nodes reachable from the entry, counting every candidate of a pending dynamic
// reference as a child.
func (c *schemaCompiler) computeReachability(pending []*pendingDynamicRef) []bool {
	extra := make(map[nodeID][]nodeID)
	for _, p := range pending {
		list := append(extra[p.node], c.getNode(p.initialTarget))
		for _, candidate := range p.candidates {
			list = append(list, candidate.node)
		}
		extra[p.node] = list
	}
	reached := make([]bool, len(c.nodes))
	stack := []nodeID{c.entryNode}
	reached[c.entryNode] = true
	for len(stack) > 0 {
		id := stack[len(stack)-1]
		stack = stack[:len(stack)-1]
		for _, child := range append(c.nodes[id].children(), extra[id]...) {
			if int(child) < len(reached) && !reached[child] {
				reached[child] = true
				stack = append(stack, child)
			}
		}
	}
	return reached
}

// ---------------------------------------------------------------------------------------------------------------------
// Analyses

func (c *schemaCompiler) analyse() {
	edges := make([][]nodeID, len(c.nodes))
	inPlace, branches := false, false
	for i, n := range c.nodes {
		edges[i] = n.inPlaceChildren(true)
		inPlace = inPlace || len(edges[i]) != 0
		branches = branches || n.hasOneOf || n.hasAnyOf
	}
	if inPlace {
		c.computeMarking(edges)
		c.computeInPlaceCycles(edges)
	} else {
		c.computeMarking(nil)
	}
	if branches {
		c.computeDiscriminators()
	}
}

// computeMarking works out which nodes can contribute evaluated-property/item annotations: a node marks if it has
// the keywords itself or any in-place child (not counting not) marks.
func (c *schemaCompiler) computeMarking(edges [][]nodeID) {
	for _, n := range c.nodes {
		n.marksProperties = n.hasProperties || n.hasPatternProperties || n.additionalProperties >= 0 ||
			n.unevaluatedProperties >= 0
		n.marksItems = n.hasPrefixItems || n.items >= 0 || (n.contains >= 0 && n.containsMarksEvaluated) ||
			n.unevaluatedItems >= 0
	}
	if edges == nil {
		return
	}
	parents := make([][]nodeID, len(c.nodes))
	for i, edge := range edges {
		// not does not contribute annotations: nodes with one use the edges without it.
		children := edge
		if c.nodes[i].not >= 0 {
			children = c.nodes[i].inPlaceChildren(false)
		}
		for _, child := range children {
			parents[child] = append(parents[child], nodeID(i))
		}
	}
	for which := 0; which < 2; which++ {
		flag := func(n *schemaNode) *bool {
			if which == 0 {
				return &n.marksProperties
			}
			return &n.marksItems
		}
		var work []nodeID
		for i, n := range c.nodes {
			if *flag(n) {
				work = append(work, nodeID(i))
			}
		}
		for len(work) > 0 {
			w := work[len(work)-1]
			work = work[:len(work)-1]
			for _, p := range parents[w] {
				if f := flag(c.nodes[p]); !*f {
					*f = true
					work = append(work, p)
				}
			}
		}
	}
}

// computeInPlaceCycles marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a
// depth guard.
func (c *schemaCompiler) computeInPlaceCycles(edges [][]nodeID) {
	count := len(c.nodes)
	index := make([]int32, count)
	for i := range index {
		index[i] = -1
	}
	low := make([]int32, count)
	onStack := make([]bool, count)
	var stack []int
	next := int32(0)
	type frame struct{ v, edge int }
	for start := 0; start < count; start++ {
		if index[start] >= 0 {
			continue
		}
		work := []frame{{start, 0}}
		index[start], low[start] = next, next
		next++
		stack = append(stack, start)
		onStack[start] = true
		for len(work) > 0 {
			top := &work[len(work)-1]
			v := top.v
			if top.edge < len(edges[v]) {
				w := int(edges[v][top.edge])
				top.edge++
				if index[w] < 0 {
					index[w], low[w] = next, next
					next++
					stack = append(stack, w)
					onStack[w] = true
					work = append(work, frame{w, 0})
				} else if onStack[w] {
					low[v] = min(low[v], index[w])
				}
				continue
			}
			work = work[:len(work)-1]
			if len(work) > 0 {
				parent := work[len(work)-1].v
				low[parent] = min(low[parent], low[v])
			}
			if low[v] == index[v] {
				var component []int
				for {
					w := stack[len(stack)-1]
					stack = stack[:len(stack)-1]
					onStack[w] = false
					component = append(component, w)
					if w == v {
						break
					}
				}
				selfLoop := false
				for _, w := range edges[v] {
					selfLoop = selfLoop || int(w) == v
				}
				if len(component) > 1 || selfLoop {
					for _, w := range component {
						c.nodes[w].inPlaceCycle = true
					}
				}
			}
		}
	}
}

// effectiveNode follows pure $ref nodes to the node that carries constraints.
func (c *schemaCompiler) effectiveNode(id nodeID) *schemaNode {
	node := c.nodes[id]
	for hops := 0; hops < 16 && node.isPureRef(); hops++ {
		node = c.nodes[node.ref]
	}
	return node
}

func (c *schemaCompiler) computeDiscriminators() {
	for _, n := range c.nodes {
		if len(n.oneOf) > 1 {
			n.oneOfDiscriminator = c.buildDiscriminator(n.oneOf)
		}
		if len(n.anyOf) > 1 {
			n.anyOfDiscriminator = c.buildDiscriminator(n.anyOf)
		}
	}
}

// The constraint a branch's properties[X] places on the value: positive (const/enum of primitives), negative (a
// string not in an enum) or wildcard (anything else).
const (
	classWildcard = iota
	classPositive
	classNegative
)

type valueClass struct {
	kind int
	set  []discriminatorValue
}

func (c *valueClass) contains(v *discriminatorValue) bool {
	for i := range c.set {
		if c.set[i].same(v) {
			return true
		}
	}
	return false
}

// buildDiscriminator classifies branches by the constraint their properties[X] places on the value, and builds the
// table from values to branches.
func (c *schemaCompiler) buildDiscriminator(branches []nodeID) *discriminator {
	var candidates []string
	for _, b := range branches {
		eff := c.effectiveNode(b)
		if !eff.hasProperties {
			continue
		}
		for _, p := range eff.properties {
			if class := c.classify(eff, p.name); class.kind != classWildcard {
				candidates = append(candidates, p.name)
			}
		}
		break
	}
	for _, name := range candidates {
		classes := make([]valueClass, len(branches))
		constrained := 0
		for i, b := range branches {
			classes[i] = c.classify(c.effectiveNode(b), name)
			if classes[i].kind != classWildcard {
				constrained++
			}
		}
		if constrained < 2 {
			continue
		}
		var values []discriminatorValue
		for i := range classes {
			for j := range classes[i].set {
				v := &classes[i].set[j]
				known := false
				for k := range values {
					known = known || values[k].same(v)
				}
				if !known {
					values = append(values, *v)
				}
			}
		}
		d := &discriminator{property: name, allRequire: true}
		for i := range values {
			entry := discriminatorEntry{value: values[i]}
			for b := range classes {
				contains := classes[b].contains(&values[i])
				selected := true
				switch classes[b].kind {
				case classPositive:
					selected = contains
				case classNegative:
					selected = !contains
				}
				if selected {
					entry.branches = append(entry.branches, uint32(b))
				}
			}
			d.known = append(d.known, entry)
		}
		for b := range classes {
			if classes[b].kind != classPositive {
				d.unknown = append(d.unknown, uint32(b))
			}
		}
		for _, b := range branches {
			d.allRequire = d.allRequire && containsString(c.effectiveNode(b).required, name)
		}
		return d
	}
	return nil
}

func (c *schemaCompiler) classify(branch *schemaNode, name string) valueClass {
	child := noNode
	for _, p := range branch.properties {
		if p.name == name {
			child = p.node
			break
		}
	}
	if child < 0 {
		return valueClass{}
	}
	p := c.effectiveNode(child)
	if p.constValue.set() {
		if v, ok := primitive(p.constValue); ok {
			return valueClass{classPositive, []discriminatorValue{v}}
		}
	}
	if p.hasEnum && len(p.enumValues) != 0 {
		if prims := primitives(p.enumValues); len(prims) == len(p.enumValues) {
			return valueClass{classPositive, prims}
		}
	}
	if p.not >= 0 && p.hasType && p.typeMask == typeString {
		not := c.effectiveNode(p.not)
		if not.hasEnum && !not.hasType && !not.constValue.set() && !not.hasStringKeywords() &&
			!not.hasInPlaceApplicators() {
			allStrings := true
			for _, v := range not.enumValues {
				allStrings = allStrings && v.d.kind(v.n) == kindString
			}
			if allStrings {
				return valueClass{classNegative, primitives(not.enumValues)}
			}
		}
	}
	return valueClass{}
}

func primitives(values []valueRef) []discriminatorValue {
	var out []discriminatorValue
	for _, v := range values {
		if p, ok := primitive(v); ok {
			out = append(out, p)
		}
	}
	return out
}

func primitive(v valueRef) (discriminatorValue, bool) {
	switch kind := v.d.kind(v.n); kind {
	case kindString:
		return discriminatorValue{kind: kind, text: string(v.d.str(v.n))}, true
	case kindNumber, kindBool, kindNull:
		return discriminatorValue{kind: kind, value: v}, true
	}
	return discriminatorValue{}, false
}

// collectAnnotations lists the annotation keywords of a schema object, in the order the C# compiler records them.
func collectAnnotations(d *Document, e int, dialect Dialect, voc uint32, content, assertFormatSet bool) []annotationEntry {
	legacy := dialect.isLegacy()
	metaData := legacy || voc&vocabMetaData != 0
	formatAnnotate := legacy || voc&(vocabFormatAnnotation|vocabFormatAssertion) != 0 || assertFormatSet
	var out []annotationEntry
	add := func(keyword string, stringsOnly bool) {
		value := string(d.appendJSON(nil, d.property(e, keyword)))
		out = append(out, annotationEntry{keyword: keyword, value: value, stringsOnly: stringsOnly})
	}
	has := func(name string) bool { return d.property(e, name) >= 0 }
	k := d.first(e)
	for i := 0; i < d.count(e); i++ {
		name := string(d.str(k + 2*i))
		switch name {
		case "title", "description", "default":
			if metaData {
				add(name, false)
			}
		case "examples":
			if metaData && dialect >= Draft6 {
				add(name, false)
			}
		case "readOnly", "writeOnly":
			if metaData && dialect >= Draft7 {
				add(name, false)
			}
		case "deprecated":
			if metaData && dialect >= Draft201909 {
				add(name, false)
			}
		case "format":
			if d.kind(k+2*i+1) == kindString && formatAnnotate {
				add(name, false)
			}
		default:
			// Unknown keywords are collected as annotations from 2019-09 onwards.
			if dialect >= Draft201909 && !knownKeywords[name] {
				add(name, false)
			}
		}
	}
	if content && dialect >= Draft7 {
		if has("contentEncoding") {
			add("contentEncoding", true)
		}
		if has("contentMediaType") {
			add("contentMediaType", true)
			// contentSchema is only meaningful alongside contentMediaType.
			if has("contentSchema") && dialect >= Draft201909 {
				add("contentSchema", true)
			}
		}
	}
	return out
}
