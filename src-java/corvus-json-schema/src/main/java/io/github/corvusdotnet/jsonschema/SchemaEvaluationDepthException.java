package io.github.corvusdotnet.jsonschema;

/** Evaluation recursed in place beyond the maximum depth (a schema that loops without consuming the instance). */
public final class SchemaEvaluationDepthException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    SchemaEvaluationDepthException() {
        super("The schema recursed in place beyond the maximum depth.");
    }
}
