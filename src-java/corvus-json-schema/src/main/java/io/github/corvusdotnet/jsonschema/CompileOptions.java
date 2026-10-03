package io.github.corvusdotnet.jsonschema;

import java.util.Collections;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.function.Predicate;

/**
 * Options for compiling a schema (the counterpart of the C# {@code JsonSchemaEvaluatorOptions}). Immutable; create
 * one with {@link #builder()}.
 */
public final class CompileOptions {
    /** The default options: 2020-12 for documents without {@code $schema}, {@code format} as the vocabularies say. */
    public static final CompileOptions DEFAULT = builder().build();

    final Dialect defaultDialect;
    final Boolean assertFormat;
    final boolean assertFormatInLegacyDrafts;
    final boolean assertContent;
    final Map<String, Predicate<String>> formats;
    final DocumentResolver documentResolver;
    final String baseUri;
    final String entryPoint;
    final int maxDepth;

    private CompileOptions(Builder b) {
        defaultDialect = b.defaultDialect;
        assertFormat = b.assertFormat;
        assertFormatInLegacyDrafts = b.assertFormatInLegacyDrafts;
        assertContent = b.assertContent;
        formats = Collections.unmodifiableMap(new HashMap<>(b.formats));
        documentResolver = b.documentResolver;
        baseUri = b.baseUri;
        entryPoint = b.entryPoint;
        maxDepth = b.maxDepth;
    }

    /**
     * A builder of options, starting from the defaults.
     *
     * @return the builder
     */
    public static Builder builder() {
        return new Builder();
    }

    /** Builds {@link CompileOptions}. */
    public static final class Builder {
        private Dialect defaultDialect = Dialect.DRAFT202012;
        private Boolean assertFormat;
        private boolean assertFormatInLegacyDrafts;
        private boolean assertContent = true;
        private final Map<String, Predicate<String>> formats = new HashMap<>();
        private DocumentResolver documentResolver;
        private String baseUri;
        private String entryPoint;
        private int maxDepth = 128;

        private Builder() {
        }

        /**
         * The dialect of documents without {@code $schema}. Defaults to 2020-12.
         *
         * @param dialect the dialect
         * @return this builder
         */
        public Builder defaultDialect(Dialect dialect) {
            defaultDialect = Objects.requireNonNull(dialect);
            return this;
        }

        /**
         * Whether {@code format} is asserted: {@code true} always, {@code false} never, {@code null} (the default) as
         * the vocabularies say (the 2020-12 {@code format-assertion} vocabulary asserts, anything else annotates).
         *
         * @param assertFormat whether to assert format, or null
         * @return this builder
         */
        public Builder assertFormat(Boolean assertFormat) {
            this.assertFormat = assertFormat;
            return this;
        }

        /**
         * With {@link #assertFormat(Boolean)} unset, also assert {@code format} in drafts 4 to 7.
         *
         * @param value whether to assert format in drafts 4 to 7
         * @return this builder
         */
        public Builder assertFormatInLegacyDrafts(boolean value) {
            assertFormatInLegacyDrafts = value;
            return this;
        }

        /**
         * Assert {@code contentEncoding} and {@code contentMediaType} in draft 7, the only draft that asserts them.
         * Defaults to true.
         *
         * @param value whether to assert content
         * @return this builder
         */
        public Builder assertContent(boolean value) {
            assertContent = value;
            return this;
        }

        /**
         * Adds a custom format assertion, which takes precedence over a built-in one of the same name. It receives
         * the string value, or a number's JSON text.
         *
         * @param name the format name
         * @param validator the assertion
         * @return this builder
         */
        public Builder format(String name, Predicate<String> validator) {
            formats.put(Objects.requireNonNull(name), Objects.requireNonNull(validator));
            return this;
        }

        /**
         * Resolves remote documents. The standard metaschemas are always available.
         *
         * @param resolver the resolver
         * @return this builder
         */
        public Builder documentResolver(DocumentResolver resolver) {
            documentResolver = resolver;
            return this;
        }

        /**
         * The base URI of the root document.
         *
         * @param uri the base URI
         * @return this builder
         */
        public Builder baseUri(String uri) {
            baseUri = uri;
            return this;
        }

        /**
         * A reference, relative to the root, to evaluate from (for example {@code #/$defs/item}). Defaults to the
         * root.
         *
         * @param reference the entry point
         * @return this builder
         */
        public Builder entryPoint(String reference) {
            entryPoint = reference;
            return this;
        }

        /**
         * The maximum depth of in-place recursion on a cycle before evaluation is abandoned. Defaults to 128.
         *
         * @param depth the depth
         * @return this builder
         */
        public Builder maxDepth(int depth) {
            if (depth < 1) {
                throw new IllegalArgumentException("maxDepth must be at least 1");
            }
            maxDepth = depth;
            return this;
        }

        /**
         * Builds the options.
         *
         * @return the options
         */
        public CompileOptions build() {
            return new CompileOptions(this);
        }
    }
}
