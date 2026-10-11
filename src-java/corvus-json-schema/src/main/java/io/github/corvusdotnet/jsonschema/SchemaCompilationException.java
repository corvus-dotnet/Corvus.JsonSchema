package io.github.corvusdotnet.jsonschema;

/**
 * A schema could not be compiled (an unresolvable reference, an invalid pattern). A pattern that is valid and over a
 * limit of size is the subclass {@link PatternTooLargeException}.
 */
public class SchemaCompilationException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    SchemaCompilationException(String message) {
        super(message);
    }
}
