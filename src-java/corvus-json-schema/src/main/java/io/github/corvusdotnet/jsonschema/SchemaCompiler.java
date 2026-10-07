package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.ARRAY;
import static io.github.corvusdotnet.jsonschema.JsonDocument.BOOLEAN;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUMBER;
import static io.github.corvusdotnet.jsonschema.JsonDocument.OBJECT;
import static io.github.corvusdotnet.jsonschema.JsonDocument.STRING;

import io.github.corvusdotnet.jsonschema.SchemaLoader.SchemaResource;
import io.github.corvusdotnet.jsonschema.SchemaLoader.SchemaTarget;
import io.github.corvusdotnet.jsonschema.SchemaNode.Dependency;
import io.github.corvusdotnet.jsonschema.SchemaNode.DependencyKeyword;
import io.github.corvusdotnet.jsonschema.SchemaNode.Discriminator;
import io.github.corvusdotnet.jsonschema.SchemaNode.PatternProperty;
import io.github.corvusdotnet.jsonschema.SchemaNode.Property;
import io.github.corvusdotnet.jsonschema.SchemaNode.Value;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Deque;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

/**
 * Compiles loaded schema documents into a {@link SchemaNode} graph and runs the compile-time analyses. A port of
 * {@code Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaCompiler} (via the Rust and TypeScript ports).
 */
final class SchemaCompiler {
    /**
     * Keywords {@code SchemaCompiler.CompileNode} handles itself; anything else is an unknown keyword (an annotation
     * from 2019-09).
     */
    static final Set<String> KNOWN_KEYWORDS = Set.of(
            "if", "not", "type", "enum", "$ref", "then", "else", "const", "items", "allOf", "anyOf", "oneOf", "title",
            "$defs", "format", "pattern", "maximum", "minimum", "default", "$schema", "$anchor", "required", "contains",
            "maxItems", "minItems", "examples", "readOnly", "$comment", "maxLength", "minLength", "writeOnly",
            "properties", "multipleOf", "deprecated", "uniqueItems", "prefixItems", "minContains", "maxContains",
            "description", "$vocabulary", "definitions", "$dynamicRef", "dependencies", "propertyNames",
            "maxProperties", "minProperties", "contentSchema", "$recursiveRef", "$dynamicAnchor", "contentEncoding",
            "additionalItems", "exclusiveMaximum", "exclusiveMinimum", "unevaluatedItems", "contentMediaType",
            "$recursiveAnchor", "dependentSchemas", "patternProperties", "dependentRequired", "additionalProperties",
            "unevaluatedProperties", "id", "$id");

    /** What a node's annotations are computed from, resolved on the first evaluation with a collector. */
    static final class AnnotationSource {
        final int document;
        final int vocab;
        final boolean content;

        AnnotationSource(int document, int vocab, boolean content) {
            this.document = document;
            this.vocab = vocab;
            this.content = content;
        }
    }

    /** The compiled program: the node graph, its entry node and whether it maintains a dynamic scope. */
    static final class Compiled {
        final SchemaNode[] nodes;
        final int root;
        final boolean usesDynamicScope;
        final JsonDocument[] documents;
        final AnnotationSource[] annotationSources;

        Compiled(
                SchemaNode[] nodes,
                int root,
                boolean usesDynamicScope,
                JsonDocument[] documents,
                AnnotationSource[] annotationSources) {
            this.nodes = nodes;
            this.root = root;
            this.usesDynamicScope = usesDynamicScope;
            this.documents = documents;
            this.annotationSources = annotationSources;
        }
    }

    private static final class PendingDynamicRef {
        final int nodeId;
        final String anchor;
        final boolean isRecursive;
        final SchemaTarget initialTarget;
        boolean[] seenResources = new boolean[0];
        final List<int[]> candidates = new ArrayList<>();

        PendingDynamicRef(int nodeId, String anchor, boolean isRecursive, SchemaTarget initialTarget) {
            this.nodeId = nodeId;
            this.anchor = anchor;
            this.isRecursive = isRecursive;
            this.initialTarget = initialTarget;
        }
    }

    private final SchemaLoader loader;
    private final CompileOptions options;
    private final List<SchemaNode> nodes = new ArrayList<>();
    private final List<SchemaTarget> targets = new ArrayList<>();
    private final List<AnnotationSource> annotationSources = new ArrayList<>();
    /** The compiled node of each schema value, by document and node. */
    private final Map<Long, Integer> nodeOf = new HashMap<>();
    private int worklistHead;
    private List<PendingDynamicRef> pendingDynamicRefs = new ArrayList<>();
    private int entryNode;

    private SchemaCompiler(SchemaLoader loader, CompileOptions options) {
        this.loader = loader;
        this.options = options;
    }

    static Compiled compile(JsonDocument schema, CompileOptions options) {
        SchemaLoader loader = new SchemaLoader(options);
        int rootResource = loader.loadRoot(schema, options.baseUri);
        return compileLoaded(loader, rootResource, options);
    }

    static Compiled compileFromUri(String uri, CompileOptions options) {
        SchemaLoader loader = new SchemaLoader(options);
        int rootResource = loader.loadRootFromUri(uri);
        return compileLoaded(loader, rootResource, options);
    }

    private static Compiled compileLoaded(SchemaLoader loader, int rootResource, CompileOptions options) {
        SchemaTarget target = loader.rootTarget(rootResource);
        if (options.entryPoint != null) {
            target = loader.tryResolveReference(rootResource, options.entryPoint);
            if (target == null) {
                throw new SchemaCompilationException(
                        "Unable to resolve the entry point '" + options.entryPoint + "'.");
            }
        }
        SchemaCompiler c = new SchemaCompiler(loader, options);
        c.entryNode = c.getNode(target);
        c.compileAll();
        boolean usesDynamicScope = false;
        for (SchemaNode n : c.nodes) {
            usesDynamicScope |= n.dynamicRef != null;
        }
        c.analyse();
        JsonDocument[] documents = new JsonDocument[loader.documents.size()];
        for (int i = 0; i < documents.length; i++) {
            documents[i] = loader.documents.get(i).doc;
        }
        return new Compiled(
                c.nodes.toArray(new SchemaNode[0]),
                c.entryNode,
                usesDynamicScope,
                documents,
                c.annotationSources.toArray(new AnnotationSource[0]));
    }

    private int getNode(SchemaTarget target) {
        long key = ((long) target.document << 32) | target.node;
        Integer existing = nodeOf.get(key);
        if (existing != null) {
            return existing;
        }
        int id = nodes.size();
        SchemaResource resource = loader.resources.get(target.resource);
        nodes.add(new SchemaNode(target.resource, resource.dialect, target.pointer));
        targets.add(target);
        annotationSources.add(null);
        nodeOf.put(key, id);
        return id;
    }

    private void compileAll() {
        while (true) {
            drainWorklist();
            if (pendingDynamicRefs.isEmpty() || (!expandDynamicRefs() && worklistHead == nodes.size())) {
                break;
            }
        }
        // Most schemas have no dynamic reference: skip (and so never compile) the finalisation.
        if (!pendingDynamicRefs.isEmpty()) {
            finalizeDynamicRefs();
        }
    }

    private void drainWorklist() {
        while (worklistHead < nodes.size()) {
            int id = worklistHead++;
            compileNode(id, targets.get(id));
        }
    }

    private int child(SchemaTarget parent, int value, String relative) {
        int resource = loader.resourceOf(parent.document, value);
        return getNode(new SchemaTarget(
                parent.document, parent.pointer + relative, value, resource >= 0 ? resource : parent.resource));
    }

    private int[] childArray(SchemaTarget parent, JsonDocument d, int value, String keyword) {
        if (value < 0 || d.kind(value) != ARRAY) {
            return null;
        }
        int[] out = new int[d.count(value)];
        int c = d.first(value);
        for (int i = 0; i < out.length; i++) {
            out[i] = child(parent, c + i, "/" + keyword + "/" + i);
        }
        return out;
    }

    private static String kw(String name) {
        return "/" + Uris.escapePointerToken(name);
    }

    private static String stringOf(JsonDocument d, int value) {
        return value >= 0 && d.kind(value) == STRING ? d.string(value) : null;
    }

    private static boolean isTrue(JsonDocument d, int value) {
        return value >= 0 && d.kind(value) == BOOLEAN && d.bool(value);
    }

    /** A non-negative integer (or integral double) keyword value, or -1. */
    private static long getUint(JsonDocument d, int value) {
        if (value < 0 || d.kind(value) != NUMBER) {
            return -1;
        }
        int flag = d.flags(value);
        long bits = d.data(value);
        if (flag == JsonDocument.NUM_LONG) {
            return bits >= 0 ? bits : -1;
        }
        if (flag == JsonDocument.NUM_U64) {
            return Long.MAX_VALUE;
        }
        double f = Double.longBitsToDouble(bits);
        if (f >= 0 && f == Math.floor(f)) {
            return f >= 0x1p63 ? Long.MAX_VALUE : (long) f;
        }
        return -1;
    }

    private static int typeMaskOf(JsonDocument d, int value) {
        String name = stringOf(d, value);
        if (name == null) {
            return 0;
        }
        switch (name) {
            case "null":
                return SchemaNode.T_NULL;
            case "boolean":
                return SchemaNode.T_BOOLEAN;
            case "object":
                return SchemaNode.T_OBJECT;
            case "array":
                return SchemaNode.T_ARRAY;
            case "number":
                return SchemaNode.T_NUMBER;
            case "string":
                return SchemaNode.T_STRING;
            case "integer":
                return SchemaNode.T_INTEGER;
            default:
                return 0;
        }
    }

    private static String[] strings(JsonDocument d, int array) {
        List<String> out = new ArrayList<>();
        int c = d.first(array);
        for (int i = 0; i < d.count(array); i++) {
            String s = stringOf(d, c + i);
            if (s != null) {
                out.add(s);
            }
        }
        return out.toArray(new String[0]);
    }

    private void compileNode(int id, SchemaTarget target) {
        SchemaNode n = nodes.get(id);
        JsonDocument d = loader.doc(target.document);
        int e = target.node;
        int kind = d.kind(e);
        if (kind == BOOLEAN) {
            if (d.bool(e)) {
                n.alwaysTrue = true;
            } else {
                n.alwaysFalse = true;
            }
            return;
        }
        if (kind != OBJECT) {
            n.alwaysTrue = true;
            return;
        }

        SchemaResource r = loader.resources.get(target.resource);
        Dialect dialect = r.dialect;
        int voc = r.vocabularies;
        boolean legacy = dialect.isLegacy();

        String refText = stringOf(d, d.property(e, "$ref"));
        if (legacy && refText != null) {
            // In draft 7 and earlier, $ref replaces every sibling keyword.
            compileRef(id, target, refText);
            return;
        }

        boolean applicator = legacy || (voc & Dialect.VOCAB_APPLICATOR) != 0;
        boolean validation = legacy || (voc & Dialect.VOCAB_VALIDATION) != 0;
        boolean unevaluated = dialect == Dialect.DRAFT201909 ? applicator : (voc & Dialect.VOCAB_UNEVALUATED) != 0;
        boolean content = legacy || (voc & Dialect.VOCAB_CONTENT) != 0;
        boolean formatAssert = options.assertFormat != null
                ? options.assertFormat
                : (legacy && options.assertFormatInLegacyDrafts) || (voc & Dialect.VOCAB_FORMAT_ASSERTION) != 0;

        List<Dependency> dependencies = null;

        // References.
        if (refText != null) {
            compileRef(id, target, refText);
        }
        if (dialect.atLeast(Dialect.DRAFT202012)) {
            String dr = stringOf(d, d.property(e, "$dynamicRef"));
            if (dr != null) {
                compileDynamicRef(id, target, dr, false);
            }
        }
        if (dialect == Dialect.DRAFT201909) {
            String rr = stringOf(d, d.property(e, "$recursiveRef"));
            if (rr != null) {
                compileDynamicRef(id, target, rr, true);
            }
        }

        // Applicators.
        if (applicator) {
            int v = d.property(e, "allOf");
            if (v >= 0) {
                n.allOf = childArray(target, d, v, "allOf");
            }
            v = d.property(e, "anyOf");
            if (v >= 0) {
                n.anyOf = childArray(target, d, v, "anyOf");
            }
            v = d.property(e, "oneOf");
            if (v >= 0) {
                n.oneOf = childArray(target, d, v, "oneOf");
            }
            v = d.property(e, "not");
            if (v >= 0) {
                n.not = child(target, v, "/not");
            }
            if (dialect.atLeast(Dialect.DRAFT7)) {
                v = d.property(e, "if");
                if (v >= 0) {
                    n.ifNode = child(target, v, "/if");
                    int t = d.property(e, "then");
                    if (t >= 0) {
                        n.thenNode = child(target, t, "/then");
                    }
                    int el = d.property(e, "else");
                    if (el >= 0) {
                        n.elseNode = child(target, el, "/else");
                    }
                }
            }
            v = d.property(e, "properties");
            if (v >= 0 && d.kind(v) == OBJECT) {
                Property[] list = new Property[d.count(v)];
                int k = d.first(v);
                for (int i = 0; i < list.length; i++, k += 2) {
                    String name = d.string(k);
                    list[i] = new Property(name, child(target, k + 1, "/properties/" + Uris.escapePointerToken(name)));
                }
                n.properties = list;
            }
            v = d.property(e, "patternProperties");
            if (v >= 0 && d.kind(v) == OBJECT) {
                PatternProperty[] list = new PatternProperty[d.count(v)];
                int k = d.first(v);
                for (int i = 0; i < list.length; i++, k += 2) {
                    String pattern = d.string(k);
                    SchemaPattern compiled = SchemaPattern.compile(pattern);
                    if (compiled == null) {
                        throw new SchemaCompilationException(SchemaPattern.failure(pattern, "patternProperties"));
                    }
                    list[i] = new PatternProperty(
                            compiled, child(target, k + 1, "/patternProperties/" + Uris.escapePointerToken(pattern)));
                }
                n.patternProperties = list;
            }
            v = d.property(e, "additionalProperties");
            if (v >= 0) {
                n.additionalProperties = child(target, v, kw("additionalProperties"));
            }
            if (dialect.atLeast(Dialect.DRAFT6)) {
                v = d.property(e, "propertyNames");
                if (v >= 0) {
                    n.propertyNames = child(target, v, kw("propertyNames"));
                }
                v = d.property(e, "contains");
                if (v >= 0) {
                    n.contains = child(target, v, kw("contains"));
                }
            }

            // "dependencies" is honoured in every dialect: in 2019-09+ it is an optional compatibility keyword.
            int deps = d.property(e, "dependencies");
            int ds = d.property(e, "dependentSchemas");
            if ((deps >= 0 && d.kind(deps) == OBJECT)
                    || (dialect.atLeast(Dialect.DRAFT201909) && ds >= 0 && d.kind(ds) == OBJECT)) {
                dependencies = compileDependencySchemas(target, d, e, dialect);
            }

            // Array applicators.
            int itemsValue = d.property(e, "items");
            if (dialect.atLeast(Dialect.DRAFT202012)) {
                v = d.property(e, "prefixItems");
                if (v >= 0 && d.kind(v) == ARRAY) {
                    n.prefixItems = childArray(target, d, v, "prefixItems");
                }
                if (itemsValue >= 0 && d.kind(itemsValue) != ARRAY) {
                    n.items = child(target, itemsValue, kw("items"));
                }
            } else if (itemsValue >= 0) {
                if (d.kind(itemsValue) == ARRAY) {
                    n.prefixItems = childArray(target, d, itemsValue, "items");
                    n.prefixKeyword = "items";
                    int a = d.property(e, "additionalItems");
                    if (a >= 0) {
                        n.items = child(target, a, "/additionalItems");
                        n.itemsKeyword = "additionalItems";
                    }
                } else {
                    n.items = child(target, itemsValue, kw("items"));
                }
            }
            n.containsMarksEvaluated = dialect.atLeast(Dialect.DRAFT202012);
        }

        if (unevaluated && dialect.atLeast(Dialect.DRAFT201909)) {
            int v = d.property(e, "unevaluatedProperties");
            if (v >= 0) {
                n.unevaluatedProperties = child(target, v, kw("unevaluatedProperties"));
            }
            v = d.property(e, "unevaluatedItems");
            if (v >= 0) {
                n.unevaluatedItems = child(target, v, kw("unevaluatedItems"));
            }
        }

        if (validation) {
            int t = d.property(e, "type");
            if (t >= 0) {
                int mask = 0;
                if (d.kind(t) == ARRAY) {
                    int c = d.first(t);
                    for (int i = 0; i < d.count(t); i++) {
                        mask |= typeMaskOf(d, c + i);
                    }
                } else {
                    mask = typeMaskOf(d, t);
                }
                n.typeMask = mask;
                n.hasType = true;
            }
            if (dialect.atLeast(Dialect.DRAFT6)) {
                int c = d.property(e, "const");
                if (c >= 0) {
                    n.constValue = new Value(d, c);
                }
            }
            int en = d.property(e, "enum");
            if (en >= 0 && d.kind(en) == ARRAY) {
                Value[] values = new Value[d.count(en)];
                int c = d.first(en);
                for (int i = 0; i < values.length; i++) {
                    values[i] = new Value(d, c + i);
                }
                n.enumValues = values;
            }
            int req = d.property(e, "required");
            if (req >= 0 && d.kind(req) == ARRAY) {
                String[] list = strings(d, req);
                n.requiredList = list;
                n.required = Arrays.stream(list).distinct().toArray(String[]::new);
            }
            if (dialect.atLeast(Dialect.DRAFT201909)) {
                int dr = d.property(e, "dependentRequired");
                if (dr >= 0 && d.kind(dr) == OBJECT) {
                    if (dependencies == null) {
                        dependencies = new ArrayList<>();
                    }
                    int k = d.first(dr);
                    for (int i = 0; i < d.count(dr); i++, k += 2) {
                        if (d.kind(k + 1) == ARRAY) {
                            dependencies.add(new Dependency(
                                    DependencyKeyword.DEPENDENT_REQUIRED, d.string(k), strings(d, k + 1), -1));
                        }
                    }
                }
            }
            n.minProperties = getUint(d, d.property(e, "minProperties"));
            n.maxProperties = getUint(d, d.property(e, "maxProperties"));
            n.minItems = getUint(d, d.property(e, "minItems"));
            n.maxItems = getUint(d, d.property(e, "maxItems"));
            n.uniqueItems = isTrue(d, d.property(e, "uniqueItems"));
            n.minLength = getUint(d, d.property(e, "minLength"));
            n.maxLength = getUint(d, d.property(e, "maxLength"));
            String p = stringOf(d, d.property(e, "pattern"));
            if (p != null) {
                n.pattern = SchemaPattern.compile(p);
                if (n.pattern == null) {
                    throw new SchemaCompilationException(SchemaPattern.failure(p, "pattern"));
                }
            }
            SchemaNode.Num m = num(d, d.property(e, "multipleOf"));
            if (m != null) {
                n.multipleOf = m;
                n.divisor = new Numbers.Divisor(d, d.property(e, "multipleOf"));
            }
            if (dialect == Dialect.DRAFT4) {
                // Draft 4: exclusiveMaximum/exclusiveMinimum are booleans that make maximum/minimum exclusive.
                SchemaNode.Num max = num(d, d.property(e, "maximum"));
                if (max != null) {
                    if (isTrue(d, d.property(e, "exclusiveMaximum"))) {
                        n.exclusiveMaximum = max;
                    } else {
                        n.maximum = max;
                    }
                }
                SchemaNode.Num min = num(d, d.property(e, "minimum"));
                if (min != null) {
                    if (isTrue(d, d.property(e, "exclusiveMinimum"))) {
                        n.exclusiveMinimum = min;
                    } else {
                        n.minimum = min;
                    }
                }
            } else {
                n.maximum = num(d, d.property(e, "maximum"));
                n.minimum = num(d, d.property(e, "minimum"));
                n.exclusiveMaximum = num(d, d.property(e, "exclusiveMaximum"));
                n.exclusiveMinimum = num(d, d.property(e, "exclusiveMinimum"));
            }
            if (dialect.atLeast(Dialect.DRAFT201909) && n.contains >= 0) {
                int mc = d.property(e, "minContains");
                if (mc >= 0) {
                    long v = getUint(d, mc);
                    n.minContains = v >= 0 ? v : 1;
                }
                int xc = d.property(e, "maxContains");
                if (xc >= 0) {
                    n.maxContains = getUint(d, xc);
                }
            }
        }

        String f = stringOf(d, d.property(e, "format"));
        if (f != null) {
            n.format = f;
            n.formatKind = Formats.Kind.of(f, dialect);
            n.assertFormat = formatAssert;
        }

        // Content keywords are asserted only in draft 7 (and annotations elsewhere).
        int ce = d.property(e, "contentEncoding");
        int cm = d.property(e, "contentMediaType");
        if (content && dialect.atLeast(Dialect.DRAFT7) && (ce >= 0 || cm >= 0)) {
            boolean base64 = "base64".equals(stringOf(d, ce));
            boolean json = "application/json".equals(stringOf(d, cm));
            n.content = base64 && json ? SchemaNode.CONTENT_BASE64_JSON
                    : base64 ? SchemaNode.CONTENT_BASE64
                    : json ? SchemaNode.CONTENT_JSON
                    : SchemaNode.CONTENT_NONE;
            n.assertContent = dialect == Dialect.DRAFT7 && options.assertContent
                    && n.content != SchemaNode.CONTENT_NONE;
        }

        if (dependencies != null) {
            // In the order the three keywords appear in the schema, as SchemaCompiler.CompileNode meets them.
            List<String> keys = new ArrayList<>();
            int k = d.first(e);
            for (int i = 0; i < d.count(e); i++, k += 2) {
                keys.add(d.string(k));
            }
            dependencies.sort((a, b) -> Integer.compare(order(keys, a.keyword), order(keys, b.keyword)));
            n.dependencies = dependencies.toArray(new Dependency[0]);
        }
        annotationSources.set(id, new AnnotationSource(target.document, voc, content));
    }

    private static int order(List<String> keys, DependencyKeyword k) {
        int i = keys.indexOf(k.keyword);
        return i >= 0 ? i : Integer.MAX_VALUE;
    }

    private static SchemaNode.Num num(JsonDocument d, int value) {
        return value >= 0 && d.kind(value) == NUMBER ? new SchemaNode.Num(d, value) : null;
    }

    private List<Dependency> compileDependencySchemas(SchemaTarget target, JsonDocument d, int e, Dialect dialect) {
        List<Dependency> out = new ArrayList<>();
        int deps = d.property(e, "dependencies");
        if (deps >= 0 && d.kind(deps) == OBJECT) {
            int k = d.first(deps);
            for (int i = 0; i < d.count(deps); i++, k += 2) {
                String name = d.string(k);
                if (d.kind(k + 1) == ARRAY) {
                    out.add(new Dependency(DependencyKeyword.DEPENDENCIES, name, strings(d, k + 1), -1));
                } else {
                    int c = child(target, k + 1, "/dependencies/" + Uris.escapePointerToken(name));
                    out.add(new Dependency(DependencyKeyword.DEPENDENCIES, name, null, c));
                }
            }
        }
        if (dialect.atLeast(Dialect.DRAFT201909)) {
            int ds = d.property(e, "dependentSchemas");
            if (ds >= 0 && d.kind(ds) == OBJECT) {
                int k = d.first(ds);
                for (int i = 0; i < d.count(ds); i++, k += 2) {
                    String name = d.string(k);
                    int c = child(target, k + 1, "/dependentSchemas/" + Uris.escapePointerToken(name));
                    out.add(new Dependency(DependencyKeyword.DEPENDENT_SCHEMAS, name, null, c));
                }
            }
        }
        return out;
    }

    private SchemaTarget resolveOrFail(SchemaTarget target, String reference) {
        SchemaTarget t = loader.tryResolveReference(target.resource, reference);
        if (t == null) {
            throw new SchemaCompilationException("Unable to resolve reference '" + reference + "' from '"
                    + loader.resources.get(target.resource).uri + "'.");
        }
        return t;
    }

    private void compileRef(int id, SchemaTarget target, String reference) {
        SchemaTarget resolved = resolveOrFail(target, reference);
        nodes.get(id).ref = getNode(resolved);
    }

    private void compileDynamicRef(int id, SchemaTarget target, String reference, boolean isRecursive) {
        SchemaTarget resolved = resolveOrFail(target, reference);
        String fragment = Uris.decodeFragment(Uris.fragment(reference));
        SchemaResource res = loader.resources.get(resolved.resource);
        boolean dynamic = isRecursive
                ? res.recursiveAnchor && resolved.pointer.equals(res.rootPointer)
                : !fragment.isEmpty()
                        && !fragment.startsWith("/")
                        && resolved.pointer.equals(res.dynamicAnchors.get(fragment));
        String keyword = isRecursive ? "$recursiveRef" : "$dynamicRef";
        if (!dynamic) {
            // A static reference, kept apart from any sibling $ref (both apply).
            int t = getNode(resolved);
            SchemaNode n = nodes.get(id);
            n.staticDynamicRef = t;
            n.staticDynamicKeyword = keyword;
            return;
        }
        pendingDynamicRefs.add(new PendingDynamicRef(id, fragment, isRecursive, resolved));
    }

    private boolean expandDynamicRefs() {
        boolean added = false;
        for (PendingDynamicRef pending : pendingDynamicRefs) {
            int resourceCount = loader.resources.size();
            for (int resource = 0; resource < resourceCount; resource++) {
                if (pending.seenResources.length <= resource) {
                    pending.seenResources = Arrays.copyOf(pending.seenResources, resource + 1);
                }
                if (pending.seenResources[resource]) {
                    continue;
                }
                pending.seenResources[resource] = true;
                SchemaResource r = loader.resources.get(resource);
                String pointer;
                if (pending.isRecursive) {
                    if (!r.recursiveAnchor) {
                        continue;
                    }
                    pointer = r.rootPointer;
                } else {
                    pointer = r.dynamicAnchors.get(pending.anchor);
                    if (pointer == null) {
                        continue;
                    }
                }
                SchemaTarget t = targetIn(resource, pointer);
                int before = nodes.size();
                int nodeId = getNode(t);
                added |= nodes.size() != before;
                pending.candidates.add(new int[] {resource, nodeId});
            }
        }
        return added;
    }

    private SchemaTarget targetIn(int resource, String pointer) {
        SchemaResource r = loader.resources.get(resource);
        String fragment = pointer.equals(r.rootPointer) ? "" : pointer.substring(r.rootPointer.length());
        SchemaTarget t = loader.tryResolveFragment(resource, fragment);
        return t != null ? t : loader.rootTarget(resource);
    }

    private void finalizeDynamicRefs() {
        boolean[] reachable = null;
        List<PendingDynamicRef> pending = pendingDynamicRefs;
        pendingDynamicRefs = new ArrayList<>();
        for (PendingDynamicRef p : pending) {
            int fallback = getNode(p.initialTarget);
            String keyword = p.isRecursive ? "$recursiveRef" : "$dynamicRef";
            SchemaNode n = nodes.get(p.nodeId);
            if (p.candidates.size() <= 1) {
                // Only the initial target's resource defines the anchor: resolution is static.
                n.staticDynamicRef = fallback;
                n.staticDynamicKeyword = keyword;
                continue;
            }
            // The dynamic scope is searched outermost-first and its outermost entry is always the resource evaluation
            // started in. When the entry resource defines the anchor, that target is the answer on every path.
            if (reachable == null) {
                reachable = computeReachability(pending);
            }
            if (!reachable[p.nodeId]) {
                n.staticDynamicRef = fallback;
                n.staticDynamicKeyword = keyword;
                continue;
            }
            int entryResource = nodes.get(entryNode).resourceId;
            int[] uniform = null;
            for (int[] c : p.candidates) {
                if (c[0] == entryResource) {
                    uniform = c;
                    break;
                }
            }
            if (uniform != null) {
                n.staticDynamicRef = uniform[1];
                n.staticDynamicKeyword = keyword;
                continue;
            }
            n.dynamicRef = new SchemaNode.DynamicRef(p.isRecursive, fallback, p.candidates.toArray(new int[0][]));
        }
        drainWorklist();
    }

    /** Nodes reachable from the entry, counting every candidate of a pending dynamic reference as a child. */
    private boolean[] computeReachability(List<PendingDynamicRef> pending) {
        Map<Integer, List<Integer>> extra = new HashMap<>();
        for (PendingDynamicRef p : pending) {
            int init = getNode(p.initialTarget);
            List<Integer> list = extra.computeIfAbsent(p.nodeId, k -> new ArrayList<>());
            list.add(init);
            for (int[] c : p.candidates) {
                list.add(c[1]);
            }
        }
        boolean[] reached = new boolean[nodes.size()];
        Deque<Integer> stack = new ArrayDeque<>();
        stack.push(entryNode);
        reached[entryNode] = true;
        while (!stack.isEmpty()) {
            int id = stack.pop();
            List<Integer> children = nodes.get(id).children();
            List<Integer> e = extra.get(id);
            if (e != null) {
                children.addAll(e);
            }
            for (int c : children) {
                if (c < reached.length && !reached[c]) {
                    reached[c] = true;
                    stack.push(c);
                }
            }
        }
        return reached;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Analyses

    private void analyse() {
        int count = nodes.size();
        List<List<Integer>> edges = new ArrayList<>(count);
        boolean inPlace = false;
        boolean branches = false;
        for (SchemaNode n : nodes) {
            List<Integer> e = n.inPlaceChildren(true);
            inPlace |= !e.isEmpty();
            branches |= n.oneOf != null || n.anyOf != null;
            edges.add(e);
        }
        computeMarking(inPlace ? edges : null);
        if (inPlace) {
            computeInPlaceCycles(edges);
        }
        if (branches) {
            computeDiscriminators();
        }
    }

    /**
     * Which nodes can contribute evaluated-property/item annotations: a node marks if it has the keywords itself or
     * any in-place child (not counting {@code not}) marks.
     */
    private void computeMarking(List<List<Integer>> edges) {
        for (SchemaNode n : nodes) {
            n.marksProperties = n.properties != null
                    || n.patternProperties != null
                    || n.additionalProperties >= 0
                    || n.unevaluatedProperties >= 0;
            n.marksItems = n.prefixItems != null
                    || n.items >= 0
                    || (n.contains >= 0 && n.containsMarksEvaluated)
                    || n.unevaluatedItems >= 0;
        }
        if (edges == null) {
            return;
        }
        int count = nodes.size();
        List<List<Integer>> parents = new ArrayList<>(count);
        for (int i = 0; i < count; i++) {
            parents.add(new ArrayList<>());
        }
        for (int i = 0; i < count; i++) {
            // `not` does not contribute annotations: nodes with one use the edges without it.
            List<Integer> children = nodes.get(i).not >= 0 ? nodes.get(i).inPlaceChildren(false) : edges.get(i);
            for (int c : children) {
                parents.get(c).add(i);
            }
        }
        for (int which = 0; which < 2; which++) {
            Deque<Integer> work = new ArrayDeque<>();
            for (int i = 0; i < count; i++) {
                SchemaNode n = nodes.get(i);
                if (which == 0 ? n.marksProperties : n.marksItems) {
                    work.push(i);
                }
            }
            while (!work.isEmpty()) {
                int w = work.pop();
                for (int p : parents.get(w)) {
                    SchemaNode n = nodes.get(p);
                    if (which == 0 ? !n.marksProperties : !n.marksItems) {
                        if (which == 0) {
                            n.marksProperties = true;
                        } else {
                            n.marksItems = true;
                        }
                        work.push(p);
                    }
                }
            }
        }
    }

    /** Marks nodes on a cycle of in-place applicators (iterative Tarjan), the only ones that need a depth guard. */
    private void computeInPlaceCycles(List<List<Integer>> edges) {
        int count = nodes.size();
        int[] index = new int[count];
        Arrays.fill(index, -1);
        int[] low = new int[count];
        boolean[] onStack = new boolean[count];
        Deque<Integer> stack = new ArrayDeque<>();
        int next = 0;
        for (int start = 0; start < count; start++) {
            if (index[start] >= 0) {
                continue;
            }
            Deque<int[]> work = new ArrayDeque<>();
            work.push(new int[] {start, 0});
            index[start] = next;
            low[start] = next;
            next++;
            stack.push(start);
            onStack[start] = true;
            while (!work.isEmpty()) {
                int[] top = work.peek();
                int v = top[0];
                List<Integer> ev = edges.get(v);
                if (top[1] < ev.size()) {
                    int w = ev.get(top[1]++);
                    if (index[w] < 0) {
                        index[w] = next;
                        low[w] = next;
                        next++;
                        stack.push(w);
                        onStack[w] = true;
                        work.push(new int[] {w, 0});
                    } else if (onStack[w]) {
                        low[v] = Math.min(low[v], index[w]);
                    }
                } else {
                    work.pop();
                    if (!work.isEmpty()) {
                        int parent = work.peek()[0];
                        low[parent] = Math.min(low[parent], low[v]);
                    }
                    if (low[v] == index[v]) {
                        List<Integer> component = new ArrayList<>();
                        while (true) {
                            int w = stack.pop();
                            onStack[w] = false;
                            component.add(w);
                            if (w == v) {
                                break;
                            }
                        }
                        if (component.size() > 1 || ev.contains(v)) {
                            for (int c : component) {
                                nodes.get(c).inPlaceCycle = true;
                            }
                        }
                    }
                }
            }
        }
    }

    /** Follows pure {@code $ref} nodes to the node that carries constraints. */
    private SchemaNode effectiveNode(int id) {
        SchemaNode node = nodes.get(id);
        for (int hops = 0; hops < 16 && node.isPureRef(); hops++) {
            node = nodes.get(node.ref);
        }
        return node;
    }

    private void computeDiscriminators() {
        for (SchemaNode n : nodes) {
            if (n.oneOf != null && n.oneOf.length > 1) {
                n.oneOfDiscriminator = buildDiscriminator(n.oneOf);
            }
            if (n.anyOf != null && n.anyOf.length > 1) {
                n.anyOfDiscriminator = buildDiscriminator(n.anyOf);
            }
        }
    }

    private static final int POSITIVE = 0;
    private static final int NEGATIVE = 1;
    private static final int WILDCARD = 2;

    /** A branch's constraint on a property's value: positive (const/enum), negative (a string not in an enum), any. */
    private static final class Class {
        final int kind;
        final List<Value> set;

        Class(int kind, List<Value> set) {
            this.kind = kind;
            this.set = set;
        }

        boolean contains(Value v) {
            for (Value x : set) {
                if (Values.equal(x.doc, x.node, v.doc, v.node)) {
                    return true;
                }
            }
            return false;
        }
    }

    private static final Class ANY = new Class(WILDCARD, List.of());

    /**
     * Classifies branches by the constraint their {@code properties[X]} places on the value, and builds the value to
     * branches table.
     */
    private Discriminator buildDiscriminator(int[] branches) {
        List<String> candidates = new ArrayList<>();
        for (int b : branches) {
            SchemaNode eff = effectiveNode(b);
            if (eff.properties == null) {
                continue;
            }
            for (Property p : eff.properties) {
                if (classify(eff, p.name).kind != WILDCARD) {
                    candidates.add(p.name);
                }
            }
            break;
        }
        for (String name : candidates) {
            List<Class> classes = new ArrayList<>();
            int nonWildcard = 0;
            for (int b : branches) {
                Class c = classify(effectiveNode(b), name);
                classes.add(c);
                if (c.kind != WILDCARD) {
                    nonWildcard++;
                }
            }
            if (nonWildcard < 2) {
                continue;
            }
            List<Value> values = new ArrayList<>();
            for (Class c : classes) {
                for (Value v : c.set) {
                    boolean seen = false;
                    for (Value x : values) {
                        if (Values.equal(x.doc, x.node, v.doc, v.node)) {
                            seen = true;
                            break;
                        }
                    }
                    if (!seen) {
                        values.add(v);
                    }
                }
            }
            int[][] selected = new int[values.size()][];
            for (int k = 0; k < values.size(); k++) {
                Value value = values.get(k);
                List<Integer> s = new ArrayList<>();
                for (int i = 0; i < classes.size(); i++) {
                    Class c = classes.get(i);
                    boolean contains = c.contains(value);
                    if (c.kind == POSITIVE ? contains : c.kind == NEGATIVE ? !contains : true) {
                        s.add(i);
                    }
                }
                selected[k] = s.stream().mapToInt(Integer::intValue).toArray();
            }
            List<Integer> unknown = new ArrayList<>();
            for (int i = 0; i < classes.size(); i++) {
                if (classes.get(i).kind != POSITIVE) {
                    unknown.add(i);
                }
            }
            boolean allRequire = true;
            for (int b : branches) {
                String[] r = effectiveNode(b).required;
                allRequire &= r != null && Arrays.asList(r).contains(name);
            }
            return new Discriminator(
                    name,
                    values.toArray(new Value[0]),
                    selected,
                    unknown.stream().mapToInt(Integer::intValue).toArray(),
                    allRequire);
        }
        return null;
    }

    private static boolean isPrimitive(Value v) {
        int k = v.kind();
        return k != OBJECT && k != ARRAY;
    }

    private Class classify(SchemaNode branch, String name) {
        int child = -1;
        if (branch.properties != null) {
            for (Property p : branch.properties) {
                if (p.name.equals(name)) {
                    child = p.node;
                    break;
                }
            }
        }
        if (child < 0) {
            return ANY;
        }
        SchemaNode p = effectiveNode(child);
        if (p.constValue != null && isPrimitive(p.constValue)) {
            return new Class(POSITIVE, List.of(p.constValue));
        }
        if (p.enumValues != null && p.enumValues.length > 0) {
            boolean all = true;
            for (Value v : p.enumValues) {
                all &= isPrimitive(v);
            }
            if (all) {
                return new Class(POSITIVE, Arrays.asList(p.enumValues));
            }
        }
        if (p.not >= 0 && p.hasType && p.typeMask == SchemaNode.T_STRING) {
            SchemaNode not = effectiveNode(p.not);
            if (not.enumValues != null) {
                boolean allStrings = true;
                for (Value v : not.enumValues) {
                    allStrings &= v.kind() == STRING;
                }
                if (allStrings
                        && !not.hasType
                        && not.constValue == null
                        && !not.hasStringKeywords()
                        && !not.hasInPlaceApplicators()) {
                    return new Class(NEGATIVE, Arrays.asList(not.enumValues));
                }
            }
        }
        return ANY;
    }
}