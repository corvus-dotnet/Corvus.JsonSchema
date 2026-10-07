package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.BOOLEAN;
import static io.github.corvusdotnet.jsonschema.JsonDocument.OBJECT;
import static io.github.corvusdotnet.jsonschema.JsonDocument.STRING;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Loads schema documents, identifies resources and anchors, and resolves references. A port of {@code SchemaLoader}
 * (via the TypeScript and Rust ports): a schema value is identified by its document and its node in that document.
 */
final class SchemaLoader {
    static final String DEFAULT_ROOT_URI = "https://corvus-oss.org/runtime-evaluator/root.json";

    static final class SchemaDocument {
        final JsonDocument doc;
        /** The resource that owns each visited schema value, by node (-1 for none). */
        final int[] resourceOf;

        SchemaDocument(JsonDocument doc) {
            this.doc = doc;
            resourceOf = new int[doc.tape.length / 2];
            Arrays.fill(resourceOf, -1);
        }
    }

    static final class SchemaResource {
        final int document;
        final String rootPointer;
        String uri;
        final Dialect dialect;
        final int vocabularies;
        boolean recursiveAnchor;
        final Map<String, String> anchors = new HashMap<>();
        final Map<String, String> dynamicAnchors = new HashMap<>();

        SchemaResource(int document, String rootPointer, String uri, Dialect dialect, int vocabularies) {
            this.document = document;
            this.rootPointer = rootPointer;
            this.uri = uri;
            this.dialect = dialect;
            this.vocabularies = vocabularies;
        }
    }

    /** The target of a reference: a value in a document, and the resource it belongs to. */
    static final class SchemaTarget {
        final int document;
        final String pointer;
        final int node;
        final int resource;

        SchemaTarget(int document, String pointer, int node, int resource) {
            this.document = document;
            this.pointer = pointer;
            this.node = node;
            this.resource = resource;
        }
    }

    /** A dialect and its vocabularies. */
    static final class DialectInfo {
        final Dialect dialect;
        final int vocabularies;

        DialectInfo(Dialect dialect, int vocabularies) {
            this.dialect = dialect;
            this.vocabularies = vocabularies;
        }
    }

    final List<SchemaDocument> documents = new ArrayList<>();
    final List<SchemaResource> resources = new ArrayList<>();
    private final Map<String, Integer> resourcesByUri = new HashMap<>();
    private final Map<String, Integer> documentsByUri = new HashMap<>();
    private final Map<String, DialectInfo> metaschemaInfo = new HashMap<>();
    private final List<String> metaschemaLoading = new ArrayList<>();
    private final Map<String, SchemaTarget> referenceCache = new HashMap<>();
    private final CompileOptions options;

    SchemaLoader(CompileOptions options) {
        this.options = options;
    }

    JsonDocument doc(int document) {
        return documents.get(document).doc;
    }

    int loadRoot(JsonDocument schema, String baseUri) {
        String uri = baseUri != null ? Uris.normalize(baseUri) : DEFAULT_ROOT_URI;
        int d = addDocument(uri, schema);
        SchemaDocument sd = documents.get(d);
        return sd.resourceOf[sd.doc.root()];
    }

    int loadRootFromUri(String uri) {
        String normalized = Uris.normalize(uri);
        if (!tryLoadDocument(normalized)) {
            throw new SchemaCompilationException("Unable to resolve the schema document '" + uri + "'.");
        }
        SchemaDocument sd = documents.get(documentsByUri.get(normalized));
        return sd.resourceOf[sd.doc.root()];
    }

    /** The root node of a resource. */
    int resourceRoot(int resource) {
        SchemaResource r = resources.get(resource);
        JsonDocument d = doc(r.document);
        Uris.Located l = Uris.resolvePointer(d, d.root(), r.rootPointer);
        return l != null ? l.node : d.root();
    }

    SchemaTarget rootTarget(int resource) {
        SchemaResource r = resources.get(resource);
        return new SchemaTarget(r.document, r.rootPointer, resourceRoot(resource), resource);
    }

    int resourceOf(int document, int node) {
        return documents.get(document).resourceOf[node];
    }

    SchemaTarget tryResolveReference(int from, String reference) {
        String key = from + "\u0000" + reference;
        if (referenceCache.containsKey(key)) {
            return referenceCache.get(key);
        }
        SchemaTarget target = resolveReference(from, reference);
        referenceCache.put(key, target);
        return target;
    }

    private SchemaTarget resolveReference(int from, String reference) {
        String absolute = Uris.resolve(resources.get(from).uri, Uris.withoutFragment(reference));
        Integer resource = resourcesByUri.get(absolute);
        if (resource == null) {
            if (!tryLoadDocument(absolute)) {
                return null;
            }
            resource = resourcesByUri.get(absolute);
            if (resource == null) {
                return null;
            }
        }
        return tryResolveFragment(resource, Uris.decodeFragment(Uris.fragment(reference)));
    }

    SchemaTarget tryResolveFragment(int resource, String fragment) {
        if (fragment.isEmpty()) {
            return rootTarget(resource);
        }
        SchemaResource r = resources.get(resource);
        JsonDocument d = doc(r.document);
        if (fragment.startsWith("/")) {
            Uris.Located l = Uris.resolvePointer(d, resourceRoot(resource), fragment);
            if (l == null) {
                return null;
            }
            int owner = resourceOf(r.document, l.node);
            return new SchemaTarget(r.document, r.rootPointer + l.pointer, l.node, owner >= 0 ? owner : resource);
        }
        String anchor = r.anchors.get(fragment);
        if (anchor == null) {
            return null;
        }
        Uris.Located l = Uris.resolvePointer(d, d.root(), anchor);
        if (l == null) {
            return null;
        }
        return new SchemaTarget(r.document, anchor, l.node, resource);
    }

    DialectInfo getDialectInfo(String schemaUri) {
        // The standard metaschema URIs, as usually written, need no URI normalisation.
        Dialect known = Dialect.known(schemaUri.endsWith("#") ? schemaUri.substring(0, schemaUri.length() - 1) : schemaUri);
        if (known != null) {
            return new DialectInfo(known, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
        }
        String normalized = Uris.normalize(schemaUri);
        known = Dialect.known(normalized);
        if (known != null) {
            return new DialectInfo(known, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
        }
        DialectInfo cached = metaschemaInfo.get(normalized);
        if (cached != null) {
            return cached;
        }
        if (metaschemaLoading.contains(normalized)) {
            return new DialectInfo(options.defaultDialect, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
        }
        metaschemaLoading.add(normalized);
        DialectInfo info;
        try {
            info = loadMetaschemaInfo(normalized);
        } finally {
            metaschemaLoading.remove(normalized);
        }
        metaschemaInfo.put(normalized, info);
        return info;
    }

    private DialectInfo loadMetaschemaInfo(String normalized) {
        Integer meta = resourcesByUri.get(normalized);
        if (meta == null) {
            if (!tryLoadDocument(normalized)) {
                return new DialectInfo(options.defaultDialect, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
            }
            meta = resourcesByUri.get(normalized);
            if (meta == null) {
                return new DialectInfo(options.defaultDialect, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
            }
        }
        int vocabularies = Dialect.VOCAB_ALL_ANNOTATING_FORMAT;
        SchemaResource r = resources.get(meta);
        JsonDocument d = doc(r.document);
        int root = resourceRoot(meta);
        if (d.kind(root) == OBJECT) {
            int v = d.property(root, "$vocabulary");
            if (v >= 0 && d.kind(v) == OBJECT) {
                vocabularies = Dialect.VOCAB_NONE;
                int k = d.first(v);
                for (int i = 0; i < d.count(v); i++, k += 2) {
                    vocabularies |= Dialect.vocabularyFlag(d.string(k));
                }
                vocabularies |= Dialect.VOCAB_CORE;
            }
        }
        return new DialectInfo(r.dialect, vocabularies);
    }

    private boolean tryLoadDocument(String absoluteUri) {
        if (documentsByUri.containsKey(absoluteUri)) {
            return true;
        }
        if (options.documentResolver != null) {
            JsonDocument d = options.documentResolver.resolve(absoluteUri);
            if (d != null) {
                addDocument(absoluteUri, d);
                return true;
            }
        }
        byte[] text = Metaschemas.get(absoluteUri);
        if (text != null) {
            addDocument(absoluteUri, JsonDocument.parse(text));
            return true;
        }
        return false;
    }

    private int addDocument(String uri, JsonDocument root) {
        int id = documents.size();
        documents.add(new SchemaDocument(root));
        documentsByUri.put(uri, id);
        int r = root.root();
        DialectInfo info = null;
        if (root.kind(r) == OBJECT) {
            int s = root.property(r, "$schema");
            if (s >= 0 && root.kind(s) == STRING) {
                info = getDialectInfo(root.string(s));
            }
        }
        if (info == null) {
            info = new DialectInfo(options.defaultDialect, Dialect.VOCAB_ALL_ANNOTATING_FORMAT);
        }
        int resource = createResource(id, "", uri, info.dialect, info.vocabularies);
        walk(id, r, "", resource, true);
        return id;
    }

    private int createResource(int document, String pointer, String uri, Dialect dialect, int vocabularies) {
        int id = resources.size();
        resources.add(new SchemaResource(document, pointer, uri, dialect, vocabularies));
        resourcesByUri.putIfAbsent(uri, id);
        return id;
    }

    private void addAnchor(int resource, String name, String pointer) {
        resources.get(resource).anchors.putIfAbsent(name, pointer);
    }

    private static boolean isSchemaValue(JsonDocument d, int n) {
        int k = d.kind(n);
        return k == BOOLEAN || k == OBJECT;
    }

    private String stringProperty(JsonDocument d, int object, String name) {
        int v = d.property(object, name);
        return v >= 0 && d.kind(v) == STRING ? d.string(v) : null;
    }

    private void walk(int docId, int element, String pointer, int resource, boolean isResourceRoot) {
        JsonDocument d = doc(docId);
        if (d.kind(element) != OBJECT) {
            documents.get(docId).resourceOf[element] = resource;
            return;
        }
        SchemaResource current = resources.get(resource);
        Dialect dialect = current.dialect;
        int vocabularies = current.vocabularies;
        if (!isResourceRoot) {
            String s = stringProperty(d, element, "$schema");
            if (s != null) {
                DialectInfo info = getDialectInfo(s);
                dialect = info.dialect;
                vocabularies = info.vocabularies;
            }
        }

        boolean legacyRefOverridesSiblings = dialect.isLegacy() && stringProperty(d, element, "$ref") != null;
        if (!legacyRefOverridesSiblings) {
            String id = stringProperty(d, element, dialect == Dialect.DRAFT4 ? "id" : "$id");
            if (id != null) {
                String uriPart = Uris.withoutFragment(id);
                String fragment = Uris.fragment(id);
                if (uriPart.isEmpty()) {
                    if (!fragment.isEmpty() && dialect.isLegacy()) {
                        addAnchor(resource, fragment, pointer);
                    }
                } else {
                    String absolute = Uris.resolve(resources.get(resource).uri, uriPart);
                    if (!isResourceRoot || !absolute.equals(resources.get(resource).uri)) {
                        if (isResourceRoot) {
                            resourcesByUri.putIfAbsent(absolute, resource);
                            resources.get(resource).uri = absolute;
                        } else {
                            resource = createResource(docId, pointer, absolute, dialect, vocabularies);
                            isResourceRoot = true;
                        }
                    }
                    if (!fragment.isEmpty() && dialect.isLegacy()) {
                        addAnchor(resource, fragment, pointer);
                    }
                }
            }
            if (dialect.atLeast(Dialect.DRAFT201909)) {
                String a = stringProperty(d, element, "$anchor");
                if (a != null) {
                    addAnchor(resource, a, pointer);
                }
            }
            if (dialect.atLeast(Dialect.DRAFT202012)) {
                String name = stringProperty(d, element, "$dynamicAnchor");
                if (name != null) {
                    resources.get(resource).dynamicAnchors.putIfAbsent(name, pointer);
                    addAnchor(resource, name, pointer);
                }
            }
            if (dialect == Dialect.DRAFT201909 && isResourceRoot) {
                int ra = d.property(element, "$recursiveAnchor");
                if (ra >= 0 && d.kind(ra) == BOOLEAN && d.bool(ra)) {
                    resources.get(resource).recursiveAnchor = true;
                }
            }
        }

        documents.get(docId).resourceOf[element] = resource;

        int k = d.first(element);
        for (int i = 0; i < d.count(element); i++, k += 2) {
            String name = d.string(k);
            int value = k + 1;
            int kind = Dialect.subschemaKind(name, dialect, legacyRefOverridesSiblings);
            if (kind == Dialect.SUB_NONE) {
                continue;
            }
            String base = pointer + "/" + Uris.escapePointerToken(name);
            switch (kind) {
                case Dialect.SUB_SINGLE:
                    if (isSchemaValue(d, value)) {
                        walk(docId, value, base, resource, false);
                    }
                    break;
                case Dialect.SUB_SINGLE_OR_ARRAY:
                    if (d.kind(value) == JsonDocument.ARRAY) {
                        walkItems(docId, d, value, base, resource);
                    } else if (isSchemaValue(d, value)) {
                        walk(docId, value, base, resource, false);
                    }
                    break;
                case Dialect.SUB_ARRAY:
                    if (d.kind(value) == JsonDocument.ARRAY) {
                        walkItems(docId, d, value, base, resource);
                    }
                    break;
                default:
                    if (d.kind(value) == OBJECT) {
                        int e = d.first(value);
                        for (int j = 0; j < d.count(value); j++, e += 2) {
                            if (isSchemaValue(d, e + 1)) {
                                walk(docId, e + 1, base + "/" + Uris.escapePointerToken(d.string(e)), resource, false);
                            }
                        }
                    }
                    break;
            }
        }
    }

    private void walkItems(int docId, JsonDocument d, int array, String base, int resource) {
        int c = d.first(array);
        for (int j = 0; j < d.count(array); j++) {
            walk(docId, c + j, base + "/" + j, resource, false);
        }
    }
}