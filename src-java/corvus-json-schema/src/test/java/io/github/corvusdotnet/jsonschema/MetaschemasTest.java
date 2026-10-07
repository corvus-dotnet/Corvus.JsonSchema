package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Map;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/** The embedded metaschemas match the ones Corvus.Text.Json embeds (src/Corvus.Text.Json/metaschema). */
class MetaschemasTest {
    @Test
    void embeddedMetaschemasAreCurrent() throws IOException {
        String source = System.getProperty("metaschemaSource", "../../src/Corvus.Text.Json/metaschema");
        Path root = Paths.get(source);
        Assumptions.assumeTrue(Files.isDirectory(root), "metaschema source not found at " + root);
        for (Map.Entry<String, String> e : Metaschemas.RESOURCES.entrySet()) {
            byte[] expected = Files.readAllBytes(root.resolve(e.getValue()));
            try (InputStream in = Metaschemas.class.getResourceAsStream("metaschemas/" + e.getValue())) {
                assertNotNull(in, e.getValue());
                assertArrayEquals(expected, in.readAllBytes(), e.getValue() + " is stale: copy it from " + root);
            }
            assertNotNull(Metaschemas.get(e.getKey()));
        }
    }
}
