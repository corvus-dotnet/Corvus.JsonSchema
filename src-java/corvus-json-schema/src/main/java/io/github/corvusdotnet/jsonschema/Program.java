package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.OBJECT;
import static io.github.corvusdotnet.jsonschema.JsonDocument.STRING;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.function.Predicate;

/** A compiled schema: the node graph and what evaluation derives from it once. */
final class Program {
    final SchemaNode[] nodes;
    final int root;
    final boolean usesDynamicScope;
    final int maxDepth;
    final Map<String, Predicate<String>> formats;
    final boolean assertFormatSet;
    /** For fail-fast evaluation: each node's target after following pure $ref hops and one-branch allOf forwards. */
    final int[] fastTarget;
    /** Name lookup tables for nodes with many properties. */
    final NameMap[] propertyMaps;
    final JsonDocument[] documents;
    private final SchemaCompiler.AnnotationSource[] annotationSources;
    private volatile AnnotationEntry[][] annotations;

    private static final int PROPERTY_MAP_THRESHOLD = 8;

    /** An annotation-producing keyword and its value (reported in verbose results). */
    static final class AnnotationEntry {
        final String keyword;
        final SchemaNode.Value value;
        /** Reported only when the instance is a string (content keywords). */
        final boolean stringsOnly;

        AnnotationEntry(String keyword, SchemaNode.Value value, boolean stringsOnly) {
            this.keyword = keyword;
            this.value = value;
            this.stringsOnly = stringsOnly;
        }
    }

    Program(SchemaCompiler.Compiled c, CompileOptions options) {
        nodes = c.nodes;
        root = c.root;
        usesDynamicScope = c.usesDynamicScope;
        maxDepth = options.maxDepth;
        formats = options.formats;
        assertFormatSet = options.assertFormat != null;
        documents = c.documents;
        annotationSources = c.annotationSources;
        fastTarget = new int[nodes.length];
        propertyMaps = new NameMap[nodes.length];
        for (int id = 0; id < nodes.length; id++) {
            fastTarget[id] = followHops(id);
            SchemaNode n = nodes[id];
            if (n.properties != null && n.properties.length > PROPERTY_MAP_THRESHOLD) {
                NameMap map = new NameMap(n.properties.length);
                for (SchemaNode.Property p : n.properties) {
                    map.putIfAbsent(p.utf8, p.node);
                }
                propertyMaps[id] = map;
            }
        }
    }

    private int followHops(int id) {
        int current = id;
        for (int i = 0; i < 16; i++) {
            SchemaNode n = nodes[current];
            // Pure-$ref hops, and forwards: a node whose only assertion is one allOf branch (C#'s NodePlan.Forward),
            // unless either end is on an in-place cycle, which keeps its guard.
            int next = pureRefTarget(n);
            if (next < 0) {
                int f = forwardTarget(n);
                if (f >= 0 && !n.inPlaceCycle && !nodes[f].inPlaceCycle && f != current) {
                    next = f;
                } else {
                    break;
                }
            }
            if (usesDynamicScope && nodes[next].resourceId != n.resourceId) {
                break;
            }
            current = next;
        }
        return current;
    }

    private static boolean hasOtherAssertions(SchemaNode n) {
        return n.hasType
                || n.constValue != null
                || n.enumValues != null
                || n.hasNumberKeywords()
                || n.hasStringKeywords()
                || n.hasObjectKeywords()
                || n.hasArrayKeywords()
                || n.dynamicRef != null
                || n.anyOf != null
                || n.oneOf != null
                || n.not >= 0
                || n.ifNode >= 0;
    }

    /** The single reference of a node that is nothing but $ref (or a static $dynamicRef), for elision; or -1. */
    static int pureRefTarget(SchemaNode n) {
        int refs = (n.ref >= 0 ? 1 : 0) + (n.staticDynamicRef >= 0 ? 1 : 0);
        if (refs != 1 || n.alwaysTrue || n.alwaysFalse || hasOtherAssertions(n) || n.allOf != null) {
            return -1;
        }
        return n.ref >= 0 ? n.ref : n.staticDynamicRef;
    }

    /** The branch of a node that is nothing but a one-branch allOf, for fail-fast forwarding; or -1. */
    static int forwardTarget(SchemaNode n) {
        if (n.allOf == null || n.allOf.length != 1) {
            return -1;
        }
        if (n.alwaysTrue || n.alwaysFalse || n.ref >= 0 || n.staticDynamicRef >= 0 || hasOtherAssertions(n)) {
            return -1;
        }
        return n.allOf[0];
    }

    /** The node for a property name of an object instance (by its UTF-8 bytes), or -1. */
    int property(int id, JsonDocument doc, int key) {
        NameMap map = propertyMaps[id];
        byte[] b = doc.strBytes(key);
        int off = doc.strOffset(key);
        int len = doc.count(key);
        if (map != null) {
            return map.get(b, off, len);
        }
        SchemaNode.Property[] props = nodes[id].properties;
        if (props != null) {
            for (SchemaNode.Property p : props) {
                if (p.utf8.length == len && java.util.Arrays.equals(p.utf8, 0, len, b, off, off + len)) {
                    return p.node;
                }
            }
        }
        return -1;
    }

    /** The annotation keywords of every node (computed on the first evaluation with a collector). */
    AnnotationEntry[][] annotations() {
        AnnotationEntry[][] a = annotations;
        if (a == null) {
            synchronized (this) {
                a = annotations;
                if (a == null) {
                    a = new AnnotationEntry[nodes.length][];
                    for (int i = 0; i < nodes.length; i++) {
                        SchemaCompiler.AnnotationSource src = annotationSources[i];
                        if (src == null) {
                            continue;
                        }
                        JsonDocument d = documents[src.document];
                        Uris.Located l = Uris.resolvePointer(d, d.root(), nodes[i].pointer);
                        if (l == null || d.kind(l.node) != OBJECT) {
                            continue;
                        }
                        a[i] = collectAnnotations(
                                d, l.node, nodes[i].dialect, src.vocab, src.content, assertFormatSet);
                    }
                    annotations = a;
                }
            }
        }
        return a;
    }

    /** The annotation keywords of a schema object, in the order {@code SchemaCompiler.CompileNode} records them. */
    static AnnotationEntry[] collectAnnotations(
            JsonDocument d, int e, Dialect dialect, int voc, boolean content, boolean assertFormatSet) {
        boolean legacy = dialect.isLegacy();
        boolean metaData = legacy || (voc & Dialect.VOCAB_META_DATA) != 0;
        boolean formatAnnotate = legacy
                || (voc & (Dialect.VOCAB_FORMAT_ANNOTATION | Dialect.VOCAB_FORMAT_ASSERTION)) != 0
                || assertFormatSet;
        List<AnnotationEntry> out = new ArrayList<>();
        int k = d.first(e);
        for (int i = 0; i < d.count(e); i++, k += 2) {
            String name = d.string(k);
            boolean add;
            switch (name) {
                case "title":
                case "description":
                case "default":
                    add = metaData;
                    break;
                case "examples":
                    add = metaData && dialect.atLeast(Dialect.DRAFT6);
                    break;
                case "readOnly":
                case "writeOnly":
                    add = metaData && dialect.atLeast(Dialect.DRAFT7);
                    break;
                case "deprecated":
                    add = metaData && dialect.atLeast(Dialect.DRAFT201909);
                    break;
                case "format":
                    add = d.kind(k + 1) == STRING && formatAnnotate;
                    break;
                default:
                    // Unknown keywords are collected as annotations from 2019-09 onwards.
                    add = dialect.atLeast(Dialect.DRAFT201909) && !SchemaCompiler.KNOWN_KEYWORDS.contains(name);
                    break;
            }
            if (add) {
                out.add(new AnnotationEntry(name, new SchemaNode.Value(d, k + 1), false));
            }
        }
        int ce = d.property(e, "contentEncoding");
        int cm = d.property(e, "contentMediaType");
        int cs = d.property(e, "contentSchema");
        if (content && dialect.atLeast(Dialect.DRAFT7) && (ce >= 0 || cm >= 0 || cs >= 0)) {
            if (ce >= 0) {
                out.add(new AnnotationEntry("contentEncoding", new SchemaNode.Value(d, ce), true));
            }
            if (cm >= 0) {
                out.add(new AnnotationEntry("contentMediaType", new SchemaNode.Value(d, cm), true));
                // contentSchema is only meaningful alongside contentMediaType.
                if (cs >= 0 && dialect.atLeast(Dialect.DRAFT201909)) {
                    out.add(new AnnotationEntry("contentSchema", new SchemaNode.Value(d, cs), true));
                }
            }
        }
        return out.isEmpty() ? null : out.toArray(new AnnotationEntry[0]);
    }
}