package io.github.corvusdotnet.jsonschema;

/** An annotation extracted from verbose results ({@code JsonSchemaAnnotationProducer}). */
public final class Annotation {
    private final String instanceLocation;
    private final String keyword;
    private final String schemaLocation;
    private final String value;

    Annotation(String instanceLocation, String keyword, String schemaLocation, String value) {
        this.instanceLocation = instanceLocation;
        this.keyword = keyword;
        this.schemaLocation = schemaLocation;
        this.value = value;
    }

    /**
     * The instance location (a JSON pointer).
     *
     * @return the instance location
     */
    public String instanceLocation() {
        return instanceLocation;
    }

    /**
     * The annotation keyword.
     *
     * @return the keyword
     */
    public String keyword() {
        return keyword;
    }

    /**
     * The JSON pointer of the schema object that holds the keyword.
     *
     * @return the schema location
     */
    public String schemaLocation() {
        return schemaLocation;
    }

    /**
     * The annotation value as JSON text.
     *
     * @return the value
     */
    public String value() {
        return value;
    }
}
