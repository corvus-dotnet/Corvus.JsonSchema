package io.github.corvusdotnet.jsonschema;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * The compiled schema graph: one node per distinct (document, pointer), with pre-digested keyword data. A port of
 * {@code Corvus.Text.Json.RuntimeEvaluator.Compilation.SchemaNode} (via the Rust and TypeScript ports). Child nodes
 * are indexes into the graph; -1 means absent.
 */
final class SchemaNode {
    // JSON types as bits (the instance kinds, and integer).
    static final int T_NULL = JsonDocument.NULL;
    static final int T_BOOLEAN = JsonDocument.BOOLEAN;
    static final int T_OBJECT = JsonDocument.OBJECT;
    static final int T_ARRAY = JsonDocument.ARRAY;
    static final int T_NUMBER = JsonDocument.NUMBER;
    static final int T_STRING = JsonDocument.STRING;
    static final int T_INTEGER = 64;

    static final int CONTENT_NONE = 0;
    static final int CONTENT_BASE64 = 1;
    static final int CONTENT_JSON = 2;
    static final int CONTENT_BASE64_JSON = 3;

    /** A JSON value in a schema document (a {@code const}, an {@code enum} member, an annotation value). */
    static final class Value {
        final JsonDocument doc;
        final int node;

        Value(JsonDocument doc, int node) {
            this.doc = doc;
            this.node = node;
        }

        int kind() {
            return doc.kind(node);
        }

        String toJson() {
            return doc.toJson(node);
        }
    }

    /** A number keyword's value: its representation, bits and text (for messages). */
    static final class Num {
        final int flag;
        final long bits;
        final String text;

        Num(JsonDocument doc, int node) {
            flag = doc.flags(node);
            bits = doc.data(node);
            text = doc.numberText(node);
        }
    }

    /** A property name and the node its value is evaluated against. */
    static final class Property {
        final String name;
        final byte[] utf8;
        final int node;

        Property(String name, int node) {
            this.name = name;
            this.utf8 = name.getBytes(StandardCharsets.UTF_8);
            this.node = node;
        }
    }

    static final class PatternProperty {
        final SchemaPattern pattern;
        final int node;

        PatternProperty(SchemaPattern pattern, int node) {
            this.pattern = pattern;
            this.node = node;
        }
    }

    /** The keyword a dependency entry came from, which names its result rows. */
    enum DependencyKeyword {
        DEPENDENCIES("dependencies"),
        DEPENDENT_SCHEMAS("dependentSchemas"),
        DEPENDENT_REQUIRED("dependentRequired");

        final String keyword;

        DependencyKeyword(String keyword) {
            this.keyword = keyword;
        }
    }

    static final class Dependency {
        final DependencyKeyword keyword;
        final String name;
        final byte[] utf8;
        /** The required properties, or null. */
        final String[] required;
        /** The schema, or -1. */
        final int schema;

        Dependency(DependencyKeyword keyword, String name, String[] required, int schema) {
            this.keyword = keyword;
            this.name = name;
            this.utf8 = name.getBytes(StandardCharsets.UTF_8);
            this.required = required;
            this.schema = schema;
        }
    }

    /** A {@code $dynamicRef}/{@code $recursiveRef} that stays dynamic after compile-time analysis. */
    static final class DynamicRef {
        final boolean isRecursive;
        final int fallback;
        /** (resource id, node id of that resource's matching anchor), for the resources that define it. */
        final int[][] byResource;

        DynamicRef(boolean isRecursive, int fallback, int[][] byResource) {
            this.isRecursive = isRecursive;
            this.fallback = fallback;
            this.byResource = byResource;
        }
    }

    /** Selects oneOf/anyOf branches by the value of one property ({@code SchemaCompiler.BuildDiscriminator}). */
    static final class Discriminator {
        final String property;
        final byte[] utf8;
        /** Known discriminator values and the branches each can select. */
        final Value[] values;
        final int[][] branches;
        /** Branches that stay candidates for a value not among the known ones (negative and wildcard branches). */
        final int[] unknown;
        /** Every branch requires the property, so its absence fails the keyword at once. */
        final boolean allRequire;

        Discriminator(String property, Value[] values, int[][] branches, int[] unknown, boolean allRequire) {
            this.property = property;
            this.utf8 = property.getBytes(StandardCharsets.UTF_8);
            this.values = values;
            this.branches = branches;
            this.unknown = unknown;
            this.allRequire = allRequire;
        }
    }

    final int resourceId;
    final Dialect dialect;
    /** The JSON pointer of the schema within its document (C#'s SchemaLocation). */
    final String pointer;

    boolean alwaysTrue;
    boolean alwaysFalse;

    // Assertions.
    int typeMask;
    boolean hasType;
    Value constValue;
    Value[] enumValues;

    // References.
    int ref = -1;
    /** A $dynamicRef/$recursiveRef that compile-time analysis resolved statically. */
    int staticDynamicRef = -1;
    String staticDynamicKeyword = "$dynamicRef";
    DynamicRef dynamicRef;

    // In-place applicators.
    int[] allOf;
    int[] anyOf;
    int[] oneOf;
    int not = -1;
    int ifNode = -1;
    int thenNode = -1;
    int elseNode = -1;

    // Objects.
    Property[] properties;
    PatternProperty[] patternProperties;
    int additionalProperties = -1;
    int propertyNames = -1;
    /** {@code required} without duplicates. */
    String[] required;
    /** {@code required} as written (duplicates kept), for results. */
    String[] requiredList;
    Dependency[] dependencies;
    long minProperties = -1;
    long maxProperties = -1;
    int unevaluatedProperties = -1;

    // Arrays.
    int[] prefixItems;
    /** The keywords behind prefixItems/items: prefixItems/items (2020-12) or items/additionalItems (legacy). */
    String prefixKeyword = "prefixItems";
    String itemsKeyword = "items";
    int items = -1;
    int contains = -1;
    long minContains = 1;
    long maxContains = -1;
    boolean containsMarksEvaluated;
    long minItems = -1;
    long maxItems = -1;
    boolean uniqueItems;
    int unevaluatedItems = -1;

    // Strings.
    long minLength = -1;
    long maxLength = -1;
    SchemaPattern pattern;
    String format;
    Formats.Kind formatKind = Formats.Kind.UNKNOWN;
    boolean assertFormat;
    int content = CONTENT_NONE;
    boolean assertContent;

    // Numbers.
    Num minimum;
    Num maximum;
    Num exclusiveMinimum;
    Num exclusiveMaximum;
    Num multipleOf;
    Numbers.Divisor divisor;

    // Analysis.
    boolean marksProperties;
    boolean marksItems;
    boolean inPlaceCycle;
    Discriminator oneOfDiscriminator;
    Discriminator anyOfDiscriminator;

    SchemaNode(int resourceId, Dialect dialect, String pointer) {
        this.resourceId = resourceId;
        this.dialect = dialect;
        this.pointer = pointer;
    }

    /** Keywords that apply only to objects. */
    boolean hasObjectKeywords() {
        return properties != null
                || patternProperties != null
                || additionalProperties >= 0
                || propertyNames >= 0
                || required != null
                || dependencies != null
                || minProperties >= 0
                || maxProperties >= 0
                || unevaluatedProperties >= 0;
    }

    boolean hasArrayKeywords() {
        return prefixItems != null
                || items >= 0
                || contains >= 0
                || minItems >= 0
                || maxItems >= 0
                || uniqueItems
                || unevaluatedItems >= 0;
    }

    boolean hasStringKeywords() {
        return minLength >= 0
                || maxLength >= 0
                || pattern != null
                || (assertFormat && format != null && !formatKind.isNumeric())
                || assertContent;
    }

    boolean hasNumberKeywords() {
        return minimum != null
                || maximum != null
                || exclusiveMinimum != null
                || exclusiveMaximum != null
                || multipleOf != null
                || (assertFormat && format != null && formatKind.isNumeric());
    }

    boolean hasDependencySchemas() {
        if (dependencies != null) {
            for (Dependency d : dependencies) {
                if (d.schema >= 0) {
                    return true;
                }
            }
        }
        return false;
    }

    boolean hasInPlaceApplicators() {
        return ref >= 0
                || staticDynamicRef >= 0
                || dynamicRef != null
                || allOf != null
                || anyOf != null
                || oneOf != null
                || not >= 0
                || ifNode >= 0
                || hasDependencySchemas();
    }

    /** Nothing but {@code $ref}: no other keyword that asserts. */
    boolean isPureRef() {
        return ref >= 0
                && !hasType
                && constValue == null
                && enumValues == null
                && !hasObjectKeywords()
                && !hasArrayKeywords()
                && !hasStringKeywords()
                && !hasNumberKeywords()
                && dynamicRef == null
                && staticDynamicRef < 0
                && allOf == null
                && anyOf == null
                && oneOf == null
                && not < 0
                && ifNode < 0
                && dependencies == null;
    }

    private static void add(List<Integer> out, int id) {
        if (id >= 0) {
            out.add(id);
        }
    }

    private static void addAll(List<Integer> out, int[] ids) {
        if (ids != null) {
            for (int id : ids) {
                out.add(id);
            }
        }
    }

    /** In-place children (the instance is evaluated at the same location). */
    List<Integer> inPlaceChildren(boolean includeNot) {
        List<Integer> out = new ArrayList<>();
        add(out, ref);
        add(out, staticDynamicRef);
        if (dynamicRef != null) {
            out.add(dynamicRef.fallback);
            for (int[] r : dynamicRef.byResource) {
                out.add(r[1]);
            }
        }
        addAll(out, allOf);
        addAll(out, anyOf);
        addAll(out, oneOf);
        if (includeNot) {
            add(out, not);
        }
        add(out, ifNode);
        add(out, thenNode);
        add(out, elseNode);
        if (dependencies != null) {
            for (Dependency d : dependencies) {
                add(out, d.schema);
            }
        }
        return out;
    }

    /** Every child node. */
    List<Integer> children() {
        List<Integer> out = inPlaceChildren(true);
        if (properties != null) {
            for (Property p : properties) {
                out.add(p.node);
            }
        }
        if (patternProperties != null) {
            for (PatternProperty p : patternProperties) {
                out.add(p.node);
            }
        }
        add(out, additionalProperties);
        add(out, propertyNames);
        add(out, unevaluatedProperties);
        add(out, items);
        add(out, contains);
        add(out, unevaluatedItems);
        addAll(out, prefixItems);
        return out;
    }
}