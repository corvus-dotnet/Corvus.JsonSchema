package io.github.corvusdotnet.jsonschema;

/**
 * A {@code pattern} or {@code patternProperties} regular expression is valid ECMA-262 and too large to run: it, or
 * what the library writes for {@code java.util.regex} to run it, is over one of the limits of size. It is thrown when
 * the schema is compiled. The message names the limit and its value. A pattern that is not valid is a
 * {@link SchemaCompilationException} that is not of this class.
 */
public final class PatternTooLargeException extends SchemaCompilationException {
    private static final long serialVersionUID = 1L;

    private final String pattern;

    PatternTooLargeException(String message, String pattern) {
        super(message);
        this.pattern = pattern;
    }

    /**
     * The pattern, as the schema has it.
     *
     * @return the pattern
     */
    public String pattern() {
        return pattern;
    }
}
