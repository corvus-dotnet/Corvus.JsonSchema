package io.github.corvusdotnet.jsonschema;

/** Resolves a schema document by absolute URI, for remote references. */
@FunctionalInterface
public interface DocumentResolver {
    /**
     * Resolves a schema document.
     *
     * @param uri the absolute URI of the document (normalised, without a fragment)
     * @return the document, or null if it is unknown
     */
    JsonDocument resolve(String uri);
}
