package io.github.corvusdotnet.jsonschema;

import java.time.Duration;
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

    /**
     * The limit on the time of one match of a {@code pattern} or {@code patternProperties} regular expression,
     * unless {@link Builder#patternMatchLimit} sets another: 1 second.
     */
    public static final Duration DEFAULT_PATTERN_MATCH_LIMIT = Duration.ofSeconds(1);

    final Dialect defaultDialect;
    final Boolean assertFormat;
    final boolean assertFormatInLegacyDrafts;
    final boolean assertContent;
    final Map<String, Predicate<String>> formats;
    final DocumentResolver documentResolver;
    final String baseUri;
    final String entryPoint;
    final int maxDepth;
    /** The limit on the time of one match of a pattern, in nanoseconds, or 0 for none. */
    final long patternMatchLimitNanos;

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
        patternMatchLimitNanos = b.patternMatchLimitNanos;
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
        private long patternMatchLimitNanos = Duration.ofSeconds(1).toNanos();

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
         * The limit on the time of one match of a {@code pattern} or {@code patternProperties} regular expression
         * against one string. Defaults to {@link CompileOptions#DEFAULT_PATTERN_MATCH_LIMIT}, 1 second. A match
         * that goes over it is abandoned, and the method that was validating throws
         * {@link PatternMatchLimitException}. A schema and an instance that come from outside can otherwise make a
         * match run for as long as they like: {@code ^(a+)+$} takes time that doubles with each "a" of a string
         * that ends in something else.
         *
         * <p>The clock is read once every few thousand characters the match reads, so a match is abandoned a little
         * after the limit and not at it. The matchers the library has for the common shapes of pattern, which use no
         * regular expression engine, read a string a bounded number of times and have no limit.
         *
         * @param limit the limit, which is more than zero
         * @return this builder
         * @see #noPatternMatchLimit()
         */
        public Builder patternMatchLimit(Duration limit) {
            Objects.requireNonNull(limit);
            if (limit.isZero() || limit.isNegative()) {
                throw new IllegalArgumentException(
                        "patternMatchLimit must be more than zero. Use noPatternMatchLimit() for no limit.");
            }
            long nanos;
            try {
                nanos = limit.toNanos();
            } catch (ArithmeticException e) {
                nanos = Long.MAX_VALUE;
            }
            // Beyond 100 years the deadline would not fit the clock, and is no limit.
            patternMatchLimitNanos = Math.min(nanos, Long.MAX_VALUE / 4);
            return this;
        }

        /**
         * Turns off the limit on the time of a match (see {@link #patternMatchLimit}), for a caller that trusts
         * both the schema and the instances. A match that needs more stack than the thread has still throws
         * {@link PatternMatchLimitException}.
         *
         * @return this builder
         */
        public Builder noPatternMatchLimit() {
            patternMatchLimitNanos = 0;
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
