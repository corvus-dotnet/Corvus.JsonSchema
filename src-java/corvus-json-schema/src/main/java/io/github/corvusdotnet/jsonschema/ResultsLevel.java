package io.github.corvusdotnet.jsonschema;

/** How much a {@link JsonSchemaResultsCollector} records. */
public enum ResultsLevel {
    /** Failures only, without message text (the lowest overhead). */
    BASIC,
    /** Failures only, with message text. */
    DETAILED,
    /** Every evaluation, passing and failing, with message text, including annotations. */
    VERBOSE
}
