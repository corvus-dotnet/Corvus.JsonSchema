package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.ARRAY;
import static io.github.corvusdotnet.jsonschema.JsonDocument.BOOLEAN;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NULL;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUMBER;
import static io.github.corvusdotnet.jsonschema.JsonDocument.OBJECT;
import static io.github.corvusdotnet.jsonschema.JsonDocument.STRING;

import io.github.corvusdotnet.jsonschema.SchemaNode.Dependency;
import io.github.corvusdotnet.jsonschema.SchemaNode.Discriminator;
import io.github.corvusdotnet.jsonschema.SchemaNode.PatternProperty;
import io.github.corvusdotnet.jsonschema.SchemaNode.Value;
import java.util.function.Predicate;
import java.util.function.Supplier;
import java.util.regex.Matcher;

/**
 * Evaluation by interpreting the node graph, like the C# {@code Evaluator.Eval<TMode>}: fail-fast without a collector
 * (returning at the first violation), or exhaustive with one, reporting every keyword with its evaluation path, schema
 * location and instance location in the C# collecting mode's order, paths and messages.
 *
 * <p>An evaluator evaluates one instance document at a time and is not thread-safe.
 */
final class Evaluator {
    // Messages (Corvus.Text.Json Strings.resx).
    static final String EVALUATED_SUBSCHEMA = "The value was expected to match the subschema.";
    static final String MATCHED_ALL = "The value matched all subschema.";
    static final String DID_NOT_MATCH_ALL = "The value did not match all subschema.";
    static final String MATCHED_AT_LEAST_ONE = "The value matched at least one subschema.";
    static final String DID_NOT_MATCH_AT_LEAST_ONE = "The value did not match at least one subschema.";
    static final String MATCHED_NO_SCHEMA = "The instance matched no schema.";
    static final String MATCHED_EXACTLY_ONE = "The value matched exactly one subschema.";
    static final String MATCHED_MORE_THAN_ONE = "The instance matched more than one schema.";
    static final String MATCHED_NOT =
            "The value matched the subschema in a not composition, which means the evaluation was not a match.";
    static final String DID_NOT_MATCH_NOT =
            "The value did not match the subschema in a not composition, which means the evaluation was a match.";
    static final String MATCHED_IF_FOR_THEN = "The value matched the subschema in a binary or ternay if, which means "
            + "the evaluation will go on to match the then subschema.";
    static final String MATCHED_IF_FOR_ELSE = "The value did not match the subschema in a ternary if, which means the "
            + "evaluation will go on to match the else subschema.";
    static final String MATCHED_THEN = "The value matched the then subschema corresponding to a binary or ternary if.";
    static final String DID_NOT_MATCH_THEN =
            "The value did not match the then subschema corresponding to a binary or ternary if.";
    static final String MATCHED_ELSE = "The value matched the else subschema corresponding to a ternary if.";
    static final String DID_NOT_MATCH_ELSE = "The value did not match the else subschema corresponding to a ternary if.";
    static final String UNIQUE_ITEMS = "The array was expected to contain unique items.";
    static final String PROPERTY_NAME_FAILED = "The property name did not match the schema.";

    private final Program p;
    private final SchemaNode[] nodes;
    private final JsonSchemaResultsCollector c;
    private final boolean collect;
    private final Program.AnnotationEntry[][] annotations;
    private JsonDocument doc;
    private int[] scope = new int[8];
    private int scopeLength;
    private int depth;
    boolean depthExceeded;
    private final Utf8Chars chars = new Utf8Chars();
    private Matcher[] matchers = new Matcher[0];
    /** The scratch bits of the last in-place child (for oneOf, which merges only a single match). */
    private long[] lastScratch;

    Evaluator(Program p, JsonSchemaResultsCollector c) {
        this.p = p;
        this.nodes = p.nodes;
        this.c = c;
        this.collect = c != null;
        this.annotations = c != null ? p.annotations() : null;
    }

    /** Evaluates the program's entry, fail-fast. */
    boolean validate(JsonDocument d, int x) {
        doc = d;
        depth = 0;
        depthExceeded = false;
        scopeLength = 0;
        return evalNode(p.fastTarget[p.root], x, null);
    }

    /** Evaluates the program's entry, reporting to the collector. */
    boolean evaluate(JsonDocument d, int x) {
        doc = d;
        depth = 0;
        depthExceeded = false;
        scopeLength = 0;
        // A root that is nothing but a $ref reports against its target, with no $ref in the evaluation path.
        int root = resolve(p.root);
        c.beginChildContext(null, nodes[root].pointer, null);
        boolean ok = evalNode(root, x, null);
        c.commitChildContext(false, ok, EVALUATED_SUBSCHEMA);
        return ok;
    }

    private String resolvedSuffix;

    /** The single reference of a pure-$ref node, for collecting mode (a node with annotations is not elided). */
    private int collectPureRef(int id) {
        if (annotations[id] != null) {
            return -1;
        }
        return Program.pureRefTarget(nodes[id]);
    }

    /**
     * Follows pure-reference hops (at most 16; not across resources when a dynamic scope is kept), returning the
     * target and leaving the evaluation-path suffix of the hops in {@link #resolvedSuffix}.
     */
    private int resolve(int id) {
        int current = id;
        StringBuilder suffix = null;
        for (int i = 0; i < 16; i++) {
            SchemaNode n = nodes[current];
            int next = collectPureRef(current);
            if (next < 0) {
                break;
            }
            if (p.usesDynamicScope && nodes[next].resourceId != n.resourceId) {
                break;
            }
            if (suffix == null) {
                suffix = new StringBuilder();
            }
            suffix.append('/').append(n.ref >= 0 ? "$ref" : n.staticDynamicKeyword);
            current = next;
        }
        resolvedSuffix = suffix == null ? "" : suffix.toString();
        return current;
    }

    private static long[] newBits(int len) {
        return new long[(len + 63) >>> 6];
    }

    private static void set(long[] bits, int i) {
        bits[i >>> 6] |= 1L << i;
    }

    private static boolean get(long[] bits, int i) {
        return (bits[i >>> 6] & (1L << i)) != 0;
    }

    private static void merge(long[] into, long[] from) {
        for (int i = 0; i < into.length; i++) {
            into[i] |= from[i];
        }
    }

    private int containerLength(int x) {
        int k = doc.kind(x);
        return k == OBJECT || k == ARRAY ? doc.count(x) : 0;
    }

    boolean evalNode(int id, int x, long[] bits) {
        SchemaNode n = nodes[id];
        if (n.alwaysTrue || n.alwaysFalse) {
            if (collect) {
                c.evaluatedBooleanSchema(n.alwaysTrue);
            }
            return n.alwaysTrue;
        }
        boolean pushed = p.usesDynamicScope && (scopeLength == 0 || scope[scopeLength - 1] != n.resourceId);
        if (pushed) {
            if (scopeLength == scope.length) {
                scope = java.util.Arrays.copyOf(scope, scopeLength * 2);
            }
            scope[scopeLength++] = n.resourceId;
        }
        int kind = doc.kind(x);
        boolean needsOwn = bits == null
                && ((n.unevaluatedProperties >= 0 && kind == OBJECT) || (n.unevaluatedItems >= 0 && kind == ARRAY));
        boolean ok = evalCore(id, n, x, needsOwn ? newBits(containerLength(x)) : bits);
        if (pushed) {
            scopeLength--;
        }
        return ok;
    }

    private boolean evalCore(int id, SchemaNode n, int x, long[] bits) {
        boolean ok = true;
        int kind = doc.kind(x);
        if (n.hasType) {
            boolean m = matchesType(n.typeMask, x);
            if (collect) {
                c.evaluatedKeyword(m, null, () -> typeMessage(n.typeMask), "type");
                ok &= m;
            } else if (!m) {
                return false;
            }
        }
        if (n.constValue != null) {
            boolean m = Values.equal(doc, x, n.constValue.doc, n.constValue.node);
            if (collect) {
                c.evaluatedKeyword(m, null, () -> constMessage(n.constValue), "const");
                ok &= m;
            } else if (!m) {
                return false;
            }
        }
        if (n.enumValues != null) {
            boolean m = false;
            for (Value v : n.enumValues) {
                if (Values.equal(doc, x, v.doc, v.node)) {
                    m = true;
                    break;
                }
            }
            if (collect) {
                c.evaluatedKeyword(m, m ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, null, "enum");
                ok &= m;
            } else if (!m) {
                return false;
            }
        }
        if (kind == NUMBER) {
            if (n.hasNumberKeywords() && !evalNumber(n, x)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        } else if (kind == STRING) {
            if (n.hasStringKeywords() && !evalString(n, x)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        } else if (kind == OBJECT) {
            if (n.hasObjectKeywords() && !evalObject(id, n, x, bits)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        } else if (kind == ARRAY) {
            if (n.hasArrayKeywords() && !evalArray(n, x, bits)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        }
        if (!evalInPlace(n, x, bits)) {
            if (!collect) {
                return false;
            }
            ok = false;
        }
        if (kind == OBJECT && n.unevaluatedProperties >= 0) {
            if (!evalUnevaluated(n.unevaluatedProperties, x, bits, true)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        } else if (kind == ARRAY && n.unevaluatedItems >= 0) {
            if (!evalUnevaluated(n.unevaluatedItems, x, bits, false)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        }
        if (collect && annotations[id] != null) {
            for (Program.AnnotationEntry a : annotations[id]) {
                if (a.stringsOnly && kind != STRING) {
                    continue;
                }
                c.ignoredKeyword(a.value::toJson, a.keyword);
            }
        }
        return ok;
    }

    boolean matchesType(int mask, int x) {
        int kind = doc.kind(x);
        if ((mask & kind) != 0) {
            return true;
        }
        return kind == NUMBER
                && (mask & SchemaNode.T_INTEGER) != 0
                && Numbers.isInteger(doc.flags(x), doc.data(x));
    }

    private static final int[][] TYPE_ORDER = {
        {SchemaNode.T_ARRAY}, {SchemaNode.T_OBJECT}, {SchemaNode.T_NULL}, {SchemaNode.T_BOOLEAN},
        {SchemaNode.T_NUMBER}, {SchemaNode.T_INTEGER}, {SchemaNode.T_STRING},
    };
    private static final String[] TYPE_NAMES = {"array", "object", "null", "boolean", "number", "integer", "string"};

    static String typeMessage(int mask) {
        java.util.List<String> names = new java.util.ArrayList<>();
        for (int i = 0; i < TYPE_ORDER.length; i++) {
            if ((mask & TYPE_ORDER[i][0]) != 0) {
                names.add(TYPE_NAMES[i]);
            }
        }
        if (names.isEmpty()) {
            return "";
        }
        if (names.size() == 1) {
            return "The value was expected to be of type '" + names.get(0) + "'";
        }
        StringBuilder sb = new StringBuilder("The value was expected to be of type '[");
        for (int i = 0; i < names.size(); i++) {
            if (i > 0) {
                sb.append(", ");
            }
            sb.append('"').append(names.get(i)).append('"');
        }
        return sb.append("]'").toString();
    }

    /** {@code " 'v'"}, or nothing for an empty value ({@code JsonSchemaEvaluation.AppendSingleQuotedValue}). */
    static String q(String v) {
        return v.isEmpty() ? "" : " '" + v + "'";
    }

    static String constMessage(Value value) {
        switch (value.kind()) {
            case STRING:
                return "Expected the value to be the string" + q(value.doc.string(value.node));
            case NUMBER:
                return "The value was expected to be equal to" + q(value.doc.numberText(value.node));
            case BOOLEAN:
                return "Expected the value to be '" + value.doc.bool(value.node) + "'";
            case NULL:
                return "Expected the value to be 'null'";
            default:
                return "";
        }
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Numbers and strings

    private boolean check(boolean m, String message, Supplier<String> lazy, String keyword) {
        if (collect) {
            c.evaluatedKeyword(m, message, lazy, keyword);
        }
        return m;
    }

    private boolean evalNumber(SchemaNode n, int x) {
        boolean ok = true;
        int flag = doc.flags(x);
        long bits = doc.data(x);
        if (n.assertFormat && n.formatKind.isNumeric() && n.format != null) {
            Predicate<String> custom = p.formats.get(n.format);
            boolean m = custom != null ? custom.test(doc.numberText(x)) : n.formatKind.checkNumber(flag, bits);
            String kind = n.formatKind.formatName;
            ok &= check(m, null, () -> "The value was expected to be in a supported format, and within bounds for '"
                    + kind + "'", "format");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.minimum != null) {
            boolean m = Numbers.compare(flag, bits, n.minimum.flag, n.minimum.bits) >= 0;
            ok &= check(m, null, () -> "The value was expected to be greater than or equal to" + q(n.minimum.text),
                    "minimum");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.maximum != null) {
            boolean m = Numbers.compare(flag, bits, n.maximum.flag, n.maximum.bits) <= 0;
            ok &= check(m, null, () -> "The value was expected to be less than or equal to" + q(n.maximum.text),
                    "maximum");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.exclusiveMinimum != null) {
            boolean m = Numbers.compare(flag, bits, n.exclusiveMinimum.flag, n.exclusiveMinimum.bits) > 0;
            ok &= check(m, null, () -> "The value was expected to be greater than" + q(n.exclusiveMinimum.text),
                    "exclusiveMinimum");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.exclusiveMaximum != null) {
            boolean m = Numbers.compare(flag, bits, n.exclusiveMaximum.flag, n.exclusiveMaximum.bits) < 0;
            ok &= check(m, null, () -> "The value was expected to be less than" + q(n.exclusiveMaximum.text),
                    "exclusiveMaximum");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.multipleOf != null) {
            boolean m = n.divisor.divides(flag, bits);
            ok &= check(m, null, () -> "The value was expected to be a multiple of" + q(n.multipleOf.text),
                    "multipleOf");
        }
        return ok;
    }

    private Matcher matcher(SchemaPattern pattern) {
        int id = pattern.id;
        if (id >= matchers.length) {
            matchers = java.util.Arrays.copyOf(matchers, Math.max(id + 1, matchers.length * 2));
        }
        Matcher m = matchers[id];
        if (m == null) {
            m = pattern.regex.matcher("");
            matchers[id] = m;
        }
        return m;
    }

    /** Whether a pattern matches the string value {@code s}. */
    boolean matches(SchemaPattern pattern, int s) {
        if (pattern.matchesAll()) {
            return true;
        }
        return pattern.find(matcher(pattern), chars.of(doc, s));
    }

    private boolean evalString(SchemaNode n, int x) {
        boolean ok = true;
        if (n.minLength >= 0 || n.maxLength >= 0) {
            long len = doc.strAscii(x)
                    ? doc.count(x)
                    : Utf8.codePoints(doc.strBytes(x), doc.strOffset(x), doc.strOffset(x) + doc.count(x));
            if (n.minLength >= 0) {
                ok &= check(len >= n.minLength, null,
                        () -> "Expected the length of the value to be greater than or equal to '" + n.minLength + "'",
                        "minLength");
                if (!ok && !collect) {
                    return false;
                }
            }
            if (n.maxLength >= 0) {
                ok &= check(len <= n.maxLength, null,
                        () -> "Expected the length of the value to be less than or equal to '" + n.maxLength + "'",
                        "maxLength");
                if (!ok && !collect) {
                    return false;
                }
            }
        }
        if (n.pattern != null) {
            ok &= check(matches(n.pattern, x), null,
                    () -> "Expected the value to match the regular expression" + q(n.pattern.source), "pattern");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.assertFormat && !n.formatKind.isNumeric() && n.format != null) {
            Predicate<String> custom = p.formats.get(n.format);
            if (custom != null) {
                ok &= check(custom.test(doc.string(x)), null,
                        () -> "Expected a string in the '" + n.format + "' format.", "format");
            } else if (n.formatKind == Formats.Kind.UNKNOWN) {
                ok &= check(true, null, null, "format");
            } else {
                boolean m = n.formatKind.checkString(doc.string(x), n.dialect.compareTo(Dialect.DRAFT6) <= 0);
                ok &= check(m, n.formatKind.message, null, "format");
            }
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.assertContent) {
            boolean m = contentOk(doc.string(x), n.content);
            String message;
            String keyword;
            if (n.content == SchemaNode.CONTENT_BASE64) {
                message = "Expected a valid Base64-encoded string.";
                keyword = "contentEncoding";
            } else if (n.content == SchemaNode.CONTENT_JSON) {
                message = "Expected valid JSON content.";
                keyword = "contentMediaType";
            } else {
                message = "Expected valid Base64-encoded JSON content.";
                keyword = "contentMediaType";
            }
            ok &= check(m, message, null, keyword);
        }
        return ok;
    }

    static byte[] base64Decode(String s) {
        int n = s.length();
        if (n % 4 != 0) {
            return null;
        }
        byte[] out = new byte[n / 4 * 3];
        int o = 0;
        for (int ci = 0; ci < n; ci += 4) {
            boolean last = ci == n - 4;
            int pad = 0;
            for (int k = 3; k >= 0 && s.charAt(ci + k) == '='; k--) {
                pad++;
            }
            if (pad > 2 || (pad > 0 && !last)) {
                return null;
            }
            int acc = 0;
            for (int k = 0; k < 4 - pad; k++) {
                int v = base64Value(s.charAt(ci + k));
                if (v < 0) {
                    return null;
                }
                acc = (acc << 6) | v;
            }
            acc <<= 6 * pad;
            out[o++] = (byte) (acc >> 16);
            if (pad < 2) {
                out[o++] = (byte) (acc >> 8);
            }
            if (pad < 1) {
                out[o++] = (byte) acc;
            }
        }
        return java.util.Arrays.copyOf(out, o);
    }

    private static int base64Value(char c) {
        if (c >= 'A' && c <= 'Z') {
            return c - 'A';
        }
        if (c >= 'a' && c <= 'z') {
            return c - 'a' + 26;
        }
        if (c >= '0' && c <= '9') {
            return c - '0' + 52;
        }
        if (c == '+') {
            return 62;
        }
        if (c == '/') {
            return 63;
        }
        return -1;
    }

    private static boolean isJson(byte[] utf8) {
        try {
            JsonDocument.parse(utf8);
            return true;
        } catch (JsonParseException e) {
            return false;
        }
    }

    /** Draft 7 content assertion. */
    static boolean contentOk(String s, int kind) {
        switch (kind) {
            case SchemaNode.CONTENT_BASE64:
                return base64Decode(s) != null;
            case SchemaNode.CONTENT_JSON:
                return isJson(s.getBytes(java.nio.charset.StandardCharsets.UTF_8));
            case SchemaNode.CONTENT_BASE64_JSON: {
                byte[] d = base64Decode(s);
                return d != null && isJson(d);
            }
            default:
                return true;
        }
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Objects

    /** A child application at a new instance location (a property value or an array item). */
    private boolean evalAt(int child, String path, int value, String docSegment) {
        if (!collect) {
            return evalNode(p.fastTarget[child], value, null);
        }
        int target = resolve(child);
        c.beginChildContext(path + resolvedSuffix, nodes[target].pointer, docSegment);
        boolean ok = evalNode(target, value, null);
        c.commitChildContext(ok, ok, EVALUATED_SUBSCHEMA);
        return ok;
    }

    private String keyText(int key) {
        return Uris.escapePointerToken(doc.string(key));
    }

    private boolean evalObject(int id, SchemaNode n, int x, long[] bits) {
        boolean ok = true;
        long len = doc.count(x);
        if (n.minProperties >= 0) {
            ok &= check(len >= n.minProperties, null,
                    () -> "Expected the property count to be greater than or equal to '" + n.minProperties + "'",
                    "minProperties");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.maxProperties >= 0) {
            ok &= check(len <= n.maxProperties, null,
                    () -> "Expected the property count to be less than or equal to '" + n.maxProperties + "'",
                    "maxProperties");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.properties != null || n.patternProperties != null || n.additionalProperties >= 0
                || n.propertyNames >= 0) {
            int k = doc.first(x);
            for (int i = 0; i < len; i++, k += 2) {
                int v = k + 1;
                boolean matched = false;
                int pn = n.properties != null ? p.property(id, doc, k) : -1;
                if (pn >= 0) {
                    matched = true;
                    if (bits != null) {
                        set(bits, i);
                    }
                    String seg = collect ? keyText(k) : null;
                    if (!evalAt(pn, collect ? "properties/" + seg : null, v, seg)) {
                        if (!collect) {
                            return false;
                        }
                        ok = false;
                    }
                }
                if (n.patternProperties != null) {
                    for (PatternProperty pp : n.patternProperties) {
                        if (!matches(pp.pattern, k)) {
                            continue;
                        }
                        matched = true;
                        if (bits != null) {
                            set(bits, i);
                        }
                        String path = collect ? "patternProperties/" + Uris.escapePointerToken(pp.pattern.source) : null;
                        if (!evalAt(pp.node, path, v, collect ? keyText(k) : null)) {
                            if (!collect) {
                                return false;
                            }
                            ok = false;
                        }
                    }
                }
                if (n.additionalProperties >= 0 && !matched) {
                    if (bits != null) {
                        set(bits, i);
                    }
                    if (!evalAt(n.additionalProperties, "additionalProperties", v, collect ? keyText(k) : null)) {
                        if (!collect) {
                            return false;
                        }
                        ok = false;
                    }
                }
                if (n.propertyNames >= 0) {
                    // The key is a string value of the document: evaluate the name in place.
                    if (collect) {
                        // Not elided; the document path stays the object's.
                        c.beginChildContext("propertyNames", nodes[n.propertyNames].pointer, null);
                        boolean m = evalNode(n.propertyNames, k, null);
                        c.commitChildContext(m, m, EVALUATED_SUBSCHEMA);
                        if (!m) {
                            c.evaluatedKeyword(false, PROPERTY_NAME_FAILED, null, "propertyNames");
                            ok = false;
                        }
                    } else if (!evalNode(p.fastTarget[n.propertyNames], k, null)) {
                        return false;
                    }
                }
            }
        }
        if (n.requiredList != null) {
            for (String r : n.requiredList) {
                boolean present = doc.property(x, r) >= 0;
                if (collect) {
                    c.evaluatedKeywordForProperty(present, null,
                            () -> "Required property " + (present ? "" : "not ") + "present '" + r + "'", r,
                            "required");
                    ok &= present;
                } else if (!present) {
                    return false;
                }
            }
        }
        if (n.dependencies != null) {
            // Rows are reported under the keyword the schema used.
            for (Dependency d : n.dependencies) {
                if (doc.property(x, d.utf8) < 0) {
                    continue;
                }
                String keyword = d.keyword.keyword;
                if (d.required != null) {
                    for (String r : d.required) {
                        boolean present = doc.property(x, r) >= 0;
                        if (collect) {
                            c.evaluatedKeywordForProperty(present, null,
                                    () -> "Required property " + (present ? "" : "not ") + "present '" + r + "'", r,
                                    keyword);
                            ok &= present;
                        } else if (!present) {
                            return false;
                        }
                    }
                }
                if (d.schema >= 0) {
                    boolean m = evalInPlaceChild(d.schema,
                            collect ? keyword + "/" + Uris.escapePointerToken(d.name) : null, x, bits, true, true);
                    if (collect) {
                        c.evaluatedKeywordForProperty(m, null,
                                () -> "The value did match the schema applied because it contained the property '"
                                        + d.name + "'",
                                d.name, keyword);
                        ok &= m;
                    } else if (!m) {
                        return false;
                    }
                }
            }
        }
        return ok;
    }

    private boolean evalUnevaluated(int child, int x, long[] bits, boolean object) {
        boolean ok = true;
        int len = doc.count(x);
        int first = doc.first(x);
        for (int i = 0; i < len; i++) {
            if (get(bits, i)) {
                continue;
            }
            set(bits, i);
            int value = object ? first + 2 * i + 1 : first + i;
            String seg = collect ? (object ? keyText(first + 2 * i) : Integer.toString(i)) : null;
            if (!evalAt(child, object ? "unevaluatedProperties" : "unevaluatedItems", value, seg)) {
                if (!collect) {
                    return false;
                }
                ok = false;
            }
        }
        if (collect) {
            c.evaluatedKeyword(ok, null, null, object ? "unevaluatedProperties" : "unevaluatedItems");
        }
        return ok;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Arrays

    private boolean evalArray(SchemaNode n, int x, long[] bits) {
        boolean ok = true;
        int len = doc.count(x);
        if (n.minItems >= 0) {
            ok &= check(len >= n.minItems, null,
                    () -> "Expected the item count to be greater than or equal to '" + n.minItems + "'", "minItems");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.maxItems >= 0) {
            ok &= check(len <= n.maxItems, null,
                    () -> "Expected the item count to be less than or equal to '" + n.maxItems + "'", "maxItems");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.prefixItems == null && n.items < 0 && n.contains < 0 && !n.uniqueItems) {
            return ok;
        }
        long count = 0;
        int prefixLength = n.prefixItems != null ? n.prefixItems.length : 0;
        int first = doc.first(x);
        for (int i = 0; i < len; i++) {
            int item = first + i;
            if (i < prefixLength) {
                if (bits != null) {
                    set(bits, i);
                }
                String seg = collect ? Integer.toString(i) : null;
                if (!evalAt(n.prefixItems[i], collect ? n.prefixKeyword + "/" + i : null, item, seg)) {
                    if (!collect) {
                        return false;
                    }
                    ok = false;
                }
            } else if (n.items >= 0) {
                if (bits != null) {
                    set(bits, i);
                }
                if (!evalAt(n.items, n.itemsKeyword, item, collect ? Integer.toString(i) : null)) {
                    if (!collect) {
                        return false;
                    }
                    ok = false;
                }
            }
            if (n.contains >= 0) {
                boolean matched;
                if (collect) {
                    int target = resolve(n.contains);
                    c.beginChildContext("contains" + resolvedSuffix, nodes[target].pointer, Integer.toString(i));
                    if (evalNode(target, item, null)) {
                        c.commitChildContext(true, true, EVALUATED_SUBSCHEMA);
                        matched = true;
                    } else {
                        c.popChildContext();
                        matched = false;
                    }
                } else {
                    matched = evalNode(p.fastTarget[n.contains], item, null);
                }
                if (matched) {
                    count++;
                    if (n.containsMarksEvaluated && bits != null) {
                        set(bits, i);
                    }
                }
            }
        }
        if (n.uniqueItems) {
            ok &= check(Values.allUnique(doc, x), UNIQUE_ITEMS, null, "uniqueItems");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.contains >= 0) {
            long max = n.maxContains;
            long min = n.minContains;
            boolean m = count >= min && (max < 0 || count <= max);
            boolean over = max >= 0 && count > max;
            ok &= check(m, null, () -> over
                    ? "Expected the contains count to be less than or equal to '" + max + "'"
                    : "Expected the contains count to be greater than or equal to '" + min + "'", "contains");
        }
        return ok;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // In-place applicators

    private boolean canMark(int id, int x) {
        SchemaNode n = nodes[id];
        return doc.kind(x) == OBJECT ? n.marksProperties : n.marksItems;
    }

    /**
     * Evaluates an in-place child: a new context at the same instance location, on a fresh scratch set of evaluated
     * properties/items merged into the parent's on success. A failing child is committed or popped. The scratch bits
     * are left in {@link #lastScratch}.
     */
    private boolean evalInPlaceChild(
            int child, String path, int x, long[] bits, boolean commitOnFailure, boolean elide) {
        int target;
        String suffix = "";
        if (!elide) {
            target = child;
        } else if (collect) {
            target = resolve(child);
            suffix = resolvedSuffix;
        } else {
            target = p.fastTarget[child];
        }
        long[] scratch = bits != null && canMark(child, x) ? newBits(containerLength(x)) : null;
        boolean guarded = nodes[target].inPlaceCycle;
        if (guarded) {
            depth++;
            if (depth > p.maxDepth) {
                depthExceeded = true;
                depth--;
                lastScratch = null;
                return false;
            }
        }
        boolean ok;
        if (collect) {
            c.beginChildContext(path + suffix, nodes[target].pointer, null);
            ok = evalNode(target, x, scratch);
            if (ok || commitOnFailure) {
                c.commitChildContext(ok, ok, EVALUATED_SUBSCHEMA);
            } else {
                c.popChildContext();
            }
        } else {
            ok = evalNode(target, x, scratch);
        }
        if (guarded) {
            depth--;
        }
        if (ok && scratch != null && bits != null) {
            merge(bits, scratch);
        }
        lastScratch = scratch;
        return ok;
    }

    private int resolveDynamic(SchemaNode.DynamicRef d) {
        for (int s = 0; s < scopeLength; s++) {
            int resource = scope[s];
            for (int[] r : d.byResource) {
                if (r[0] == resource) {
                    return r[1];
                }
            }
        }
        return d.fallback;
    }

    /** The oneOf/anyOf branches a discriminator leaves as candidates for an instance; null for all of them. */
    private int[] select(Discriminator d, int x) {
        if (d == null || doc.kind(x) != OBJECT) {
            return null;
        }
        int value = doc.property(x, d.utf8);
        if (value < 0) {
            return d.allRequire ? NONE : null;
        }
        for (int i = 0; i < d.values.length; i++) {
            Value v = d.values[i];
            if (Values.equal(doc, value, v.doc, v.node)) {
                return d.branches[i];
            }
        }
        return d.unknown;
    }

    private static final int[] NONE = new int[0];

    private boolean evalInPlace(SchemaNode n, int x, long[] bits) {
        boolean ok = true;
        if (n.ref >= 0) {
            boolean m = evalInPlaceChild(n.ref, "$ref", x, bits, true, true);
            ok &= check(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, null, "$ref");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.staticDynamicRef >= 0) {
            String keyword = n.staticDynamicKeyword;
            boolean m = evalInPlaceChild(n.staticDynamicRef, keyword, x, bits, true, true);
            ok &= check(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, null, keyword);
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.dynamicRef != null) {
            String keyword = n.dynamicRef.isRecursive ? "$recursiveRef" : "$dynamicRef";
            // The resolved target is elided, with no hops in the path.
            int dynamicTarget = resolveDynamic(n.dynamicRef);
            int target = collect ? resolve(dynamicTarget) : p.fastTarget[dynamicTarget];
            boolean m = evalInPlaceChild(target, keyword, x, bits, true, false);
            ok &= check(m, m ? MATCHED_ALL : DID_NOT_MATCH_ALL, null, keyword);
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.allOf != null) {
            boolean all = true;
            for (int i = 0; i < n.allOf.length; i++) {
                if (!evalInPlaceChild(n.allOf[i], collect ? "allOf/" + i : null, x, bits, true, true)) {
                    if (!collect) {
                        return false;
                    }
                    all = false;
                }
            }
            ok &= check(all, all ? MATCHED_ALL : DID_NOT_MATCH_ALL, null, "allOf");
        }
        if (n.anyOf != null) {
            boolean any = false;
            // Every branch runs when results are collected or evaluated properties/items are tracked.
            boolean exhaustive = collect || bits != null;
            // Fail-fast evaluation only tries the branches a discriminator property can select.
            int[] selection = exhaustive ? null : select(n.anyOfDiscriminator, x);
            int count = selection != null ? selection.length : n.anyOf.length;
            for (int s = 0; s < count; s++) {
                int i = selection != null ? selection[s] : s;
                if (evalInPlaceChild(n.anyOf[i], collect ? "anyOf/" + i : null, x, bits, false, true)) {
                    any = true;
                    if (!exhaustive) {
                        break;
                    }
                }
            }
            ok &= check(any, any ? MATCHED_AT_LEAST_ONE : DID_NOT_MATCH_AT_LEAST_ONE, null, "anyOf");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.oneOf != null) {
            int matched = 0;
            long[] only = null;
            boolean track = bits != null;
            // Branches a discriminator rules out cannot match, so fail-fast evaluation skips them.
            int[] selection = collect || track ? null : select(n.oneOfDiscriminator, x);
            int count = selection != null ? selection.length : n.oneOf.length;
            for (int s = 0; s < count; s++) {
                int i = selection != null ? selection[s] : s;
                // Evaluated properties/items are merged only when exactly one branch matched, so collect them aside.
                long[] aside = track ? newBits(containerLength(x)) : null;
                boolean m = evalInPlaceChild(n.oneOf[i], collect ? "oneOf/" + i : null, x, aside, false, true);
                if (m) {
                    matched++;
                    only = track ? (aside != null ? aside : lastScratch) : null;
                    if (!collect && matched > 1) {
                        return false;
                    }
                }
            }
            if (matched == 1 && bits != null && only != null) {
                merge(bits, only);
            }
            String message = matched == 0 ? MATCHED_NO_SCHEMA : matched == 1 ? MATCHED_EXACTLY_ONE
                    : MATCHED_MORE_THAN_ONE;
            ok &= check(matched == 1, message, null, "oneOf");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.not >= 0) {
            // Not elided, never contributes results or evaluated properties/items.
            boolean inner;
            if (collect) {
                c.beginChildContext("not", nodes[n.not].pointer, null);
                inner = evalNode(n.not, x, null);
                c.popChildContext();
            } else {
                inner = evalNode(p.fastTarget[n.not], x, null);
            }
            ok &= check(!inner, inner ? MATCHED_NOT : DID_NOT_MATCH_NOT, null, "not");
            if (!ok && !collect) {
                return false;
            }
        }
        if (n.ifNode >= 0) {
            boolean cond = evalInPlaceChild(n.ifNode, "if", x, bits, false, true);
            if (collect) {
                c.evaluatedKeyword(true, cond ? MATCHED_IF_FOR_THEN : MATCHED_IF_FOR_ELSE, null, "if");
            }
            if (cond) {
                if (n.thenNode >= 0) {
                    boolean m = evalInPlaceChild(n.thenNode, "then", x, bits, true, true);
                    ok &= check(m, m ? MATCHED_THEN : DID_NOT_MATCH_THEN, null, "then");
                }
            } else if (n.elseNode >= 0) {
                boolean m = evalInPlaceChild(n.elseNode, "else", x, bits, true, true);
                ok &= check(m, m ? MATCHED_ELSE : DID_NOT_MATCH_ELSE, null, "else");
            }
        }
        return ok;
    }
}