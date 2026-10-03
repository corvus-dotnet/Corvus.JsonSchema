package io.github.corvusdotnet.jsonschema;

/** A schema could not be compiled (an unresolvable reference, an invalid pattern). */
public final class SchemaCompilationException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    SchemaCompilationException(String message) {
        super(message);
    }
}
