package io.github.corvusdotnet.jsonschema;

import java.util.Objects;

/** One result row of an evaluation with a {@link JsonSchemaResultsCollector}. */
public final class SchemaResult {
    private final boolean isMatch;
    private final String message;
    private final String evaluationLocation;
    private final String schemaEvaluationLocation;
    private final String documentEvaluationLocation;

    SchemaResult(
            boolean isMatch,
            String message,
            String evaluationLocation,
            String schemaEvaluationLocation,
            String documentEvaluationLocation) {
        this.isMatch = isMatch;
        this.message = message;
        this.evaluationLocation = evaluationLocation;
        this.schemaEvaluationLocation = schemaEvaluationLocation;
        this.documentEvaluationLocation = documentEvaluationLocation;
    }

    /**
     * Whether the evaluation matched.
     *
     * @return whether it matched
     */
    public boolean isMatch() {
        return isMatch;
    }

    /**
     * The message, or {@code ""} when the level records none or the keyword has none. Annotation rows carry the
     * annotation's value as JSON text.
     *
     * @return the message
     */
    public String message() {
        return message;
    }

    /**
     * The path of keywords from the root schema (for example {@code /properties/name/type}).
     *
     * @return the evaluation location
     */
    public String evaluationLocation() {
        return evaluationLocation;
    }

    /**
     * The JSON pointer of the evaluated schema (or keyword) within its document.
     *
     * @return the schema location
     */
    public String schemaEvaluationLocation() {
        return schemaEvaluationLocation;
    }

    /**
     * The JSON pointer of the instance location (for example {@code /name}).
     *
     * @return the instance location
     */
    public String documentEvaluationLocation() {
        return documentEvaluationLocation;
    }

    @Override
    public boolean equals(Object o) {
        if (!(o instanceof SchemaResult)) {
            return false;
        }
        SchemaResult r = (SchemaResult) o;
        return isMatch == r.isMatch
                && message.equals(r.message)
                && evaluationLocation.equals(r.evaluationLocation)
                && schemaEvaluationLocation.equals(r.schemaEvaluationLocation)
                && documentEvaluationLocation.equals(r.documentEvaluationLocation);
    }

    @Override
    public int hashCode() {
        return Objects.hash(
                isMatch, message, evaluationLocation, schemaEvaluationLocation, documentEvaluationLocation);
    }

    @Override
    public String toString() {
        return (isMatch ? "match " : "fail ") + evaluationLocation + " " + schemaEvaluationLocation + " "
                + documentEvaluationLocation + " " + message;
    }
}
