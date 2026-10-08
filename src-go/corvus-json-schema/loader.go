package jsonschema

import "strconv"

// Loads schema documents, identifies resources and anchors, and resolves references. Elements are identified by
// (document, value index).

const defaultRootURI = "https://corvus-oss.org/runtime-evaluator/root.json"

type schemaDocument struct {
	doc *Document
	// The resource that owns each visited schema value.
	resourceOf map[int]uint32
}

type schemaResource struct {
	document        uint32
	rootPointer     string
	uri             string
	dialect         Dialect
	vocabularies    uint32
	recursiveAnchor bool
	anchors         map[string]string
	dynamicAnchors  map[string]string
}

// schemaTarget is the target of a reference: a value in a document, and the resource it belongs to.
type schemaTarget struct {
	document uint32
	pointer  string
	value    int
	resource uint32
}

type dialectInfo struct {
	dialect      Dialect
	vocabularies uint32
}

type referenceKey struct {
	from      uint32
	reference string
}

type schemaLoader struct {
	documents         []schemaDocument
	resources         []*schemaResource
	resourcesByURI    map[string]uint32
	documentsByURI    map[string]uint32
	metaschemaInfo    map[string]dialectInfo
	metaschemaLoading []string
	referenceCache    map[referenceKey]*schemaTarget
	options           *compileOptions
}

func newSchemaLoader(options *compileOptions) *schemaLoader {
	return &schemaLoader{
		resourcesByURI: make(map[string]uint32),
		documentsByURI: make(map[string]uint32),
		metaschemaInfo: make(map[string]dialectInfo),
		referenceCache: make(map[referenceKey]*schemaTarget),
		options:        options,
	}
}

// stringValue is the text of a string value, if n is one.
func stringValue(d *Document, n int) (string, bool) {
	if n < 0 || d.kind(n) != kindString {
		return "", false
	}
	return string(d.str(n)), true
}

// member is the value of a property of n, or -1 when n is not an object or has no such property.
func member(d *Document, n int, name string) int {
	if n < 0 || d.kind(n) != kindObject {
		return -1
	}
	return d.property(n, name)
}

func isKind(d *Document, n int, kind uint8) bool {
	return n >= 0 && d.kind(n) == kind
}

func isSchemaValue(d *Document, n int) bool {
	k := d.kind(n)
	return k == kindBool || k == kindObject
}

func (l *schemaLoader) rootResource(doc uint32) uint32 {
	d := &l.documents[doc]
	return d.resourceOf[d.doc.root]
}

func (l *schemaLoader) loadRoot(schema *Document) (uint32, error) {
	uri := defaultRootURI
	if l.options.baseURI != "" {
		uri = normalizeURI(l.options.baseURI)
	}
	doc, err := l.addDocument(uri, schema)
	if err != nil {
		return 0, err
	}
	return l.rootResource(doc), nil
}

func (l *schemaLoader) loadRootFromURI(uri string) (uint32, error) {
	normalized := normalizeURI(uri)
	ok, err := l.tryLoadDocument(normalized)
	if err != nil {
		return 0, err
	}
	if !ok {
		return 0, compileError("Unable to resolve the schema document '" + uri + "'.")
	}
	return l.rootResource(l.documentsByURI[normalized]), nil
}

// resourceRoot is the root value of a resource.
func (l *schemaLoader) resourceRoot(resource uint32) int {
	r := l.resources[resource]
	d := l.documents[r.document].doc
	if v, _ := resolvePointer(d, d.root, r.rootPointer); v >= 0 {
		return v
	}
	return d.root
}

func (l *schemaLoader) rootTarget(resource uint32) schemaTarget {
	r := l.resources[resource]
	return schemaTarget{document: r.document, pointer: r.rootPointer, value: l.resourceRoot(resource), resource: resource}
}

func (l *schemaLoader) resourceOf(document uint32, value int) (uint32, bool) {
	r, ok := l.documents[document].resourceOf[value]
	return r, ok
}

// tryResolveReference resolves a reference from a resource. The target is nil when it does not resolve.
func (l *schemaLoader) tryResolveReference(from uint32, reference string) (*schemaTarget, error) {
	key := referenceKey{from, reference}
	if t, ok := l.referenceCache[key]; ok {
		return t, nil
	}
	target, err := l.resolveReference(from, reference)
	if err != nil {
		return nil, err
	}
	l.referenceCache[key] = target
	return target, nil
}

func (l *schemaLoader) resolveReference(from uint32, reference string) (*schemaTarget, error) {
	uriPart, fragment := splitFragment(reference)
	absolute := resolveURI(l.resources[from].uri, uriPart)
	resource, ok := l.resourcesByURI[absolute]
	if !ok {
		loaded, err := l.tryLoadDocument(absolute)
		if err != nil || !loaded {
			return nil, err
		}
		if resource, ok = l.resourcesByURI[absolute]; !ok {
			return nil, nil
		}
	}
	return l.tryResolveFragment(resource, decodeFragment(fragment)), nil
}

func (l *schemaLoader) tryResolveFragment(resource uint32, fragment string) *schemaTarget {
	if fragment == "" {
		t := l.rootTarget(resource)
		return &t
	}
	r := l.resources[resource]
	d := l.documents[r.document].doc
	if fragment[0] == '/' {
		value, path := resolvePointer(d, l.resourceRoot(resource), fragment)
		if value < 0 {
			return nil
		}
		owner, ok := l.resourceOf(r.document, value)
		if !ok {
			owner = resource
		}
		return &schemaTarget{document: r.document, pointer: r.rootPointer + path, value: value, resource: owner}
	}
	anchor, ok := r.anchors[fragment]
	if !ok {
		return nil
	}
	value, _ := resolvePointer(d, d.root, anchor)
	if value < 0 {
		return nil
	}
	return &schemaTarget{document: r.document, pointer: anchor, value: value, resource: resource}
}

func (l *schemaLoader) defaultDialectInfo() dialectInfo {
	return dialectInfo{l.options.defaultDialect, vocabAllAnnotating}
}

func (l *schemaLoader) getDialectInfo(schemaURI string) (dialectInfo, error) {
	// The standard metaschema URIs, as usually written, need no URI normalisation.
	plain := schemaURI
	if n := len(plain); n > 0 && plain[n-1] == '#' {
		plain = plain[:n-1]
	}
	if d, ok := knownDialect(plain); ok {
		return dialectInfo{d, vocabAllAnnotating}, nil
	}
	normalized := normalizeURI(schemaURI)
	if d, ok := knownDialect(normalized); ok {
		return dialectInfo{d, vocabAllAnnotating}, nil
	}
	if info, ok := l.metaschemaInfo[normalized]; ok {
		return info, nil
	}
	for _, u := range l.metaschemaLoading {
		if u == normalized {
			return l.defaultDialectInfo(), nil
		}
	}
	l.metaschemaLoading = append(l.metaschemaLoading, normalized)
	info, err := l.loadMetaschemaInfo(normalized)
	l.metaschemaLoading = l.metaschemaLoading[:len(l.metaschemaLoading)-1]
	if err != nil {
		return dialectInfo{}, err
	}
	l.metaschemaInfo[normalized] = info
	return info, nil
}

func (l *schemaLoader) loadMetaschemaInfo(normalized string) (dialectInfo, error) {
	metaResource, ok := l.resourcesByURI[normalized]
	if !ok {
		loaded, err := l.tryLoadDocument(normalized)
		if err != nil {
			return dialectInfo{}, err
		}
		if !loaded {
			return l.defaultDialectInfo(), nil
		}
		if metaResource, ok = l.resourcesByURI[normalized]; !ok {
			return l.defaultDialectInfo(), nil
		}
	}
	vocabularies := vocabAllAnnotating
	r := l.resources[metaResource]
	d := l.documents[r.document].doc
	if v := member(d, l.resourceRoot(metaResource), "$vocabulary"); isKind(d, v, kindObject) {
		vocabularies = vocabCore
		k := d.first(v)
		for i := 0; i < d.count(v); i++ {
			vocabularies |= vocabularyFlag(string(d.str(k + 2*i)))
		}
	}
	return dialectInfo{l.resources[metaResource].dialect, vocabularies}, nil
}

func (l *schemaLoader) tryLoadDocument(absoluteURI string) (bool, error) {
	if _, ok := l.documentsByURI[absoluteURI]; ok {
		return true, nil
	}
	if l.options.resolver != nil {
		if doc := l.options.resolver(absoluteURI); doc != nil {
			_, err := l.addDocument(absoluteURI, doc)
			return err == nil, err
		}
	}
	if text, ok := metaschema(absoluteURI); ok {
		doc, err := ParseDocument(text)
		if err != nil {
			return false, compileError("The embedded metaschema '" + absoluteURI + "' is not valid JSON.")
		}
		_, err = l.addDocument(absoluteURI, doc)
		return err == nil, err
	}
	return false, nil
}

func (l *schemaLoader) addDocument(uri string, doc *Document) (uint32, error) {
	id := uint32(len(l.documents))
	l.documents = append(l.documents, schemaDocument{doc: doc, resourceOf: make(map[int]uint32)})
	l.documentsByURI[uri] = id
	info := l.defaultDialectInfo()
	if s, ok := stringValue(doc, member(doc, doc.root, "$schema")); ok {
		var err error
		if info, err = l.getDialectInfo(s); err != nil {
			return 0, err
		}
	}
	resource := l.createResource(id, "", uri, info)
	if err := l.walk(id, doc.root, "", resource, true); err != nil {
		return 0, err
	}
	return id, nil
}

func (l *schemaLoader) createResource(document uint32, pointer, uri string, info dialectInfo) uint32 {
	id := uint32(len(l.resources))
	l.resources = append(l.resources, &schemaResource{
		document: document, rootPointer: pointer, uri: uri, dialect: info.dialect, vocabularies: info.vocabularies,
		anchors: make(map[string]string), dynamicAnchors: make(map[string]string),
	})
	if _, ok := l.resourcesByURI[uri]; !ok {
		l.resourcesByURI[uri] = id
	}
	return id
}

func (l *schemaLoader) addAnchor(resource uint32, name, pointer string) {
	anchors := l.resources[resource].anchors
	if _, ok := anchors[name]; !ok {
		anchors[name] = pointer
	}
}

func (l *schemaLoader) walk(doc uint32, element int, pointer string, resource uint32, isResourceRoot bool) error {
	d := l.documents[doc].doc
	if d.kind(element) != kindObject {
		l.documents[doc].resourceOf[element] = resource
		return nil
	}
	info := dialectInfo{l.resources[resource].dialect, l.resources[resource].vocabularies}
	if !isResourceRoot {
		if s, ok := stringValue(d, d.property(element, "$schema")); ok {
			var err error
			if info, err = l.getDialectInfo(s); err != nil {
				return err
			}
		}
	}
	dialect := info.dialect

	legacyRefOverridesSiblings := dialect.isLegacy() && isKind(d, d.property(element, "$ref"), kindString)
	if !legacyRefOverridesSiblings {
		idName := "$id"
		if dialect == Draft4 {
			idName = "id"
		}
		if id, ok := stringValue(d, d.property(element, idName)); ok {
			uriPart, fragment := splitFragment(id)
			if uriPart == "" {
				if fragment != "" && dialect.isLegacy() {
					l.addAnchor(resource, fragment, pointer)
				}
			} else {
				absolute := resolveURI(l.resources[resource].uri, uriPart)
				if !isResourceRoot || absolute != l.resources[resource].uri {
					if isResourceRoot {
						if _, ok := l.resourcesByURI[absolute]; !ok {
							l.resourcesByURI[absolute] = resource
						}
						l.resources[resource].uri = absolute
					} else {
						resource = l.createResource(doc, pointer, absolute, info)
						isResourceRoot = true
					}
				}
				if fragment != "" && dialect.isLegacy() {
					l.addAnchor(resource, fragment, pointer)
				}
			}
		}
		if dialect >= Draft201909 {
			if a, ok := stringValue(d, d.property(element, "$anchor")); ok {
				l.addAnchor(resource, a, pointer)
			}
		}
		if dialect >= Draft202012 {
			if name, ok := stringValue(d, d.property(element, "$dynamicAnchor")); ok {
				dynamic := l.resources[resource].dynamicAnchors
				if _, ok := dynamic[name]; !ok {
					dynamic[name] = pointer
				}
				l.addAnchor(resource, name, pointer)
			}
		}
		if dialect == Draft201909 && isResourceRoot {
			if v := d.property(element, "$recursiveAnchor"); isKind(d, v, kindBool) && d.boolean(v) {
				l.resources[resource].recursiveAnchor = true
			}
		}
	}

	l.documents[doc].resourceOf[element] = resource

	k := d.first(element)
	for i := 0; i < d.count(element); i++ {
		name := string(d.str(k + 2*i))
		value := k + 2*i + 1
		kind := subschemaKindOf(name, dialect, legacyRefOverridesSiblings)
		if kind == subschemaNone {
			continue
		}
		base := pointer + "/" + escapePointerToken(name)
		switch kind {
		case subschemaSingle:
			if isSchemaValue(d, value) {
				if err := l.walk(doc, value, base, resource, false); err != nil {
					return err
				}
			}
		case subschemaSingleOrArray, subschemaArray:
			if d.kind(value) == kindArray {
				c := d.first(value)
				for j := 0; j < d.count(value); j++ {
					if err := l.walk(doc, c+j, base+"/"+strconv.Itoa(j), resource, false); err != nil {
						return err
					}
				}
			} else if kind == subschemaSingleOrArray && isSchemaValue(d, value) {
				if err := l.walk(doc, value, base, resource, false); err != nil {
					return err
				}
			}
		case subschemaMap:
			if d.kind(value) == kindObject {
				c := d.first(value)
				for j := 0; j < d.count(value); j++ {
					entry := c + 2*j
					if isSchemaValue(d, entry+1) {
						p := base + "/" + escapePointerToken(string(d.str(entry)))
						if err := l.walk(doc, entry+1, p, resource, false); err != nil {
							return err
						}
					}
				}
			}
		}
	}
	return nil
}
