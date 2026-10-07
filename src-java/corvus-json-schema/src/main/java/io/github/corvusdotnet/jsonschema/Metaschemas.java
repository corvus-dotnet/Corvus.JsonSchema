package io.github.corvusdotnet.jsonschema;

import java.io.IOException;
import java.io.InputStream;
import java.io.UncheckedIOException;
import java.util.Map;

/** The standard metaschemas, embedded as resources and keyed by their canonical URI (no trailing empty fragment). */
final class Metaschemas {
    private Metaschemas() {
    }

    static final Map<String, String> RESOURCES = Map.ofEntries(
            Map.entry("http://json-schema.org/draft-04/schema", "draft4/schema.json"),
            Map.entry("http://json-schema.org/draft-06/schema", "draft6/schema.json"),
            Map.entry("http://json-schema.org/draft-07/schema", "draft7/schema.json"),
            Map.entry("https://json-schema.org/draft/2019-09/schema", "draft2019-09/schema.json"),
            Map.entry("https://json-schema.org/draft/2020-12/schema", "draft2020-12/schema.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/applicator", "draft2019-09/meta/applicator.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/content", "draft2019-09/meta/content.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/core", "draft2019-09/meta/core.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/format", "draft2019-09/meta/format.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/hyper-schema", "draft2019-09/meta/hyper-schema.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/meta-data", "draft2019-09/meta/meta-data.json"),
            Map.entry("https://json-schema.org/draft/2019-09/meta/validation", "draft2019-09/meta/validation.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/applicator", "draft2020-12/meta/applicator.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/content", "draft2020-12/meta/content.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/core", "draft2020-12/meta/core.json"),
            Map.entry(
                    "https://json-schema.org/draft/2020-12/meta/format-annotation",
                    "draft2020-12/meta/format-annotation.json"),
            Map.entry(
                    "https://json-schema.org/draft/2020-12/meta/format-assertion",
                    "draft2020-12/meta/format-assertion.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/hyper-schema", "draft2020-12/meta/hyper-schema.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/meta-data", "draft2020-12/meta/meta-data.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/unevaluated", "draft2020-12/meta/unevaluated.json"),
            Map.entry("https://json-schema.org/draft/2020-12/meta/validation", "draft2020-12/meta/validation.json"));

    /** The metaschema at a normalised URI, or null. */
    static byte[] get(String uri) {
        String resource = RESOURCES.get(uri);
        if (resource == null) {
            return null;
        }
        try (InputStream in = Metaschemas.class.getResourceAsStream("metaschemas/" + resource)) {
            return in.readAllBytes();
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }
}
