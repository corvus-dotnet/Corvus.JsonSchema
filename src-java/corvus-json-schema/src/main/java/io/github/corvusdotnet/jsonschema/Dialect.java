package io.github.corvusdotnet.jsonschema;

/** The JSON Schema dialects the evaluator understands, in specification order. */
public enum Dialect {
    /** Draft 4. */
    DRAFT4,
    /** Draft 6. */
    DRAFT6,
    /** Draft 7. */
    DRAFT7,
    /** Draft 2019-09. */
    DRAFT201909,
    /** Draft 2020-12. */
    DRAFT202012;

    /** Draft 7 and earlier, where {@code $ref} replaces its siblings and there are no vocabularies. */
    boolean isLegacy() {
        return this.compareTo(DRAFT7) <= 0;
    }

    boolean atLeast(Dialect other) {
        return this.compareTo(other) >= 0;
    }

    // The vocabularies in effect for a schema resource (2019-09 and later), as bits.
    static final int VOCAB_NONE = 0;
    static final int VOCAB_CORE = 1;
    static final int VOCAB_APPLICATOR = 1 << 1;
    static final int VOCAB_VALIDATION = 1 << 2;
    static final int VOCAB_META_DATA = 1 << 3;
    static final int VOCAB_FORMAT_ANNOTATION = 1 << 4;
    static final int VOCAB_FORMAT_ASSERTION = 1 << 5;
    static final int VOCAB_CONTENT = 1 << 6;
    static final int VOCAB_UNEVALUATED = 1 << 7;
    static final int VOCAB_ALL_ANNOTATING_FORMAT = VOCAB_CORE | VOCAB_APPLICATOR | VOCAB_VALIDATION | VOCAB_META_DATA
            | VOCAB_FORMAT_ANNOTATION | VOCAB_CONTENT | VOCAB_UNEVALUATED;

    /** The dialect of a well-known metaschema URI (normalised, no fragment), or null. */
    static Dialect known(String uri) {
        switch (uri) {
            case "http://json-schema.org/draft-04/schema":
                return DRAFT4;
            case "http://json-schema.org/draft-06/schema":
                return DRAFT6;
            case "http://json-schema.org/draft-07/schema":
                return DRAFT7;
            case "https://json-schema.org/draft/2019-09/schema":
                return DRAFT201909;
            case "https://json-schema.org/draft/2020-12/schema":
                return DRAFT202012;
            default:
                return null;
        }
    }

    /** The bit of a vocabulary URI. */
    static int vocabularyFlag(String uri) {
        switch (uri) {
            case "https://json-schema.org/draft/2020-12/vocab/core":
            case "https://json-schema.org/draft/2019-09/vocab/core":
                return VOCAB_CORE;
            case "https://json-schema.org/draft/2020-12/vocab/applicator":
            case "https://json-schema.org/draft/2019-09/vocab/applicator":
                return VOCAB_APPLICATOR;
            case "https://json-schema.org/draft/2020-12/vocab/validation":
            case "https://json-schema.org/draft/2019-09/vocab/validation":
                return VOCAB_VALIDATION;
            case "https://json-schema.org/draft/2020-12/vocab/meta-data":
            case "https://json-schema.org/draft/2019-09/vocab/meta-data":
                return VOCAB_META_DATA;
            case "https://json-schema.org/draft/2020-12/vocab/format-annotation":
            case "https://json-schema.org/draft/2019-09/vocab/format":
                return VOCAB_FORMAT_ANNOTATION;
            case "https://json-schema.org/draft/2020-12/vocab/format-assertion":
                return VOCAB_FORMAT_ASSERTION;
            case "https://json-schema.org/draft/2020-12/vocab/content":
            case "https://json-schema.org/draft/2019-09/vocab/content":
                return VOCAB_CONTENT;
            case "https://json-schema.org/draft/2020-12/vocab/unevaluated":
                return VOCAB_UNEVALUATED;
            default:
                return VOCAB_NONE;
        }
    }

    // How a keyword holds subschemas.
    static final int SUB_NONE = 0;
    static final int SUB_SINGLE = 1;
    static final int SUB_SINGLE_OR_ARRAY = 2;
    static final int SUB_ARRAY = 3;
    static final int SUB_MAP = 4;

    /** Which keywords hold subschemas, by dialect ({@code SchemaKeywords.GetSubschemaKind}). */
    static int subschemaKind(String keyword, Dialect dialect, boolean legacyRefOverridesSiblings) {
        if (keyword.equals("definitions") || keyword.equals("$defs")) {
            return SUB_MAP;
        }
        if (legacyRefOverridesSiblings) {
            return SUB_NONE;
        }
        switch (keyword) {
            case "properties":
            case "patternProperties":
            case "dependencies":
                return SUB_MAP;
            case "additionalProperties":
            case "not":
                return SUB_SINGLE;
            case "allOf":
            case "anyOf":
            case "oneOf":
                return SUB_ARRAY;
            case "items":
                return dialect.atLeast(DRAFT202012) ? SUB_SINGLE : SUB_SINGLE_OR_ARRAY;
            case "additionalItems":
                return dialect.compareTo(DRAFT201909) <= 0 ? SUB_SINGLE : SUB_NONE;
            case "contains":
            case "propertyNames":
                return dialect.atLeast(DRAFT6) ? SUB_SINGLE : SUB_NONE;
            case "if":
            case "then":
            case "else":
                return dialect.atLeast(DRAFT7) ? SUB_SINGLE : SUB_NONE;
            case "unevaluatedProperties":
            case "unevaluatedItems":
            case "contentSchema":
                return dialect.atLeast(DRAFT201909) ? SUB_SINGLE : SUB_NONE;
            case "dependentSchemas":
                return dialect.atLeast(DRAFT201909) ? SUB_MAP : SUB_NONE;
            case "prefixItems":
                return dialect.atLeast(DRAFT202012) ? SUB_ARRAY : SUB_NONE;
            default:
                return SUB_NONE;
        }
    }
}
