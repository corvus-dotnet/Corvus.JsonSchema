package io.github.corvusdotnet.jsonschema;

/** The text is not valid JSON, or nests arrays and objects too deeply. */
public final class JsonParseException extends IllegalArgumentException {
    private static final long serialVersionUID = 1L;

    private final int offset;

    JsonParseException(String message, int offset) {
        super(message + " at byte " + offset);
        this.offset = offset;
    }

    /** The shared exception of checks that only need to know that parsing failed (thrown many times, built once). */
    JsonParseException(String message) {
        super(message);
        this.offset = -1;
    }

    /**
     * The byte offset in the UTF-8 text at which the error was found.
     *
     * @return the offset
     */
    public int offset() {
        return offset;
    }
}
