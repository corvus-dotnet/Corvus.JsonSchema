package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.OutputStreamWriter;
import java.io.Writer;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/**
 * Drives the harness over IHOP as Bowtie does (start, dialect, run with the suite's remotes as the registry, stop), in a
 * separate JVM: every required case of the JSON-Schema-Test-Suite, and the annotation suite's assertions, compared
 * with the expected results. No containers.
 */
class IhopTest {
    static final String[][] DRAFTS = {
        {"draft4", "http://json-schema.org/draft-04/schema#", "4"},
        {"draft6", "http://json-schema.org/draft-06/schema#", "6"},
        {"draft7", "http://json-schema.org/draft-07/schema#", "7"},
        {"draft2019-09", "https://json-schema.org/draft/2019-09/schema", "2019"},
        {"draft2020-12", "https://json-schema.org/draft/2020-12/schema", "2020"},
    };

    /** The harness in its own process. */
    static final class Harness implements AutoCloseable {
        final Process process;
        final Writer input;
        final BufferedReader output;

        Harness() throws IOException {
            String java = Paths.get(System.getProperty("java.home"), "bin", "java").toString();
            process = new ProcessBuilder(java, "-cp", System.getProperty("java.class.path"),
                    BowtieHarness.class.getName()).redirectError(ProcessBuilder.Redirect.INHERIT).start();
            input = new OutputStreamWriter(process.getOutputStream(), StandardCharsets.UTF_8);
            output = new BufferedReader(new InputStreamReader(process.getInputStream(), StandardCharsets.UTF_8));
            JsonDocument started = send("{\"cmd\":\"start\",\"version\":1}");
            int implementation = started.property(started.root(), "implementation");
            assertEquals("java", started.string(started.property(implementation, "language")));
        }

        JsonDocument send(String request) throws IOException {
            input.write(request);
            input.write('\n');
            input.flush();
            return JsonDocument.parse(output.readLine());
        }

        @Override
        public void close() throws IOException {
            input.write("{\"cmd\":\"stop\"}\n");
            input.flush();
            try {
                assertEquals(0, process.waitFor());
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                throw new IOException(e);
            }
        }
    }

    static Path suiteRoot() {
        String p = System.getProperty("jsonSchemaTestSuite");
        return Paths.get(p != null ? p : "../../JSON-Schema-Test-Suite");
    }

    static JsonDocument read(Path file) throws IOException {
        return JsonDocument.parse(Files.readAllBytes(file));
    }

    static List<Path> jsonFiles(Path dir) throws IOException {
        try (Stream<Path> s = Files.list(dir)) {
            return s.filter(p -> p.toString().endsWith(".json")).sorted().collect(Collectors.toList());
        }
    }

    /** Bowtie's registry: every remote, keyed by its http://localhost:1234/ URI. */
    static String registry(Path root) throws IOException {
        Path remotes = root.resolve("remotes");
        StringBuilder sb = new StringBuilder("{");
        try (Stream<Path> s = Files.walk(remotes)) {
            for (Path p : (Iterable<Path>) s.filter(f -> f.toString().endsWith(".json")).sorted()::iterator) {
                String rel = remotes.relativize(p).toString().replace('\\', '/');
                sb.append(sb.length() > 1 ? "," : "");
                JsonDocument.appendQuoted(sb, "http://localhost:1234/" + rel);
                sb.append(':').append(read(p).toString());
            }
        }
        return sb.append('}').toString();
    }

    static boolean compatible(String level, String compat) {
        List<String> order = List.of("3", "4", "6", "7", "2019", "2020");
        int l = order.indexOf(level);
        if (compat.startsWith("<=")) {
            int max = order.indexOf(compat.substring(2));
            return max >= 0 && l <= max;
        }
        int min = order.indexOf(compat);
        return min >= 0 && l >= min;
    }

    private static String quoted(String s) {
        StringBuilder sb = new StringBuilder();
        JsonDocument.appendQuoted(sb, s);
        return sb.toString();
    }

    @Test
    void requiredSuiteOverIhop() throws Exception {
        Path root = suiteRoot();
        Assumptions.assumeTrue(Files.isDirectory(root.resolve("tests")), "JSON-Schema-Test-Suite not found");
        String registry = registry(root);
        int seq = 0;
        int total = 0;
        List<String> failures = new ArrayList<>();
        try (Harness h = new Harness()) {
            for (String[] draft : DRAFTS) {
                JsonDocument ok = h.send("{\"cmd\":\"dialect\",\"dialect\":" + quoted(draft[1]) + "}");
                assertTrue(ok.bool(ok.property(ok.root(), "ok")));
                for (Path file : jsonFiles(root.resolve("tests").resolve(draft[0]))) {
                    JsonDocument d = read(file);
                    for (int g = 0; g < d.count(d.root()); g++) {
                        int group = d.first(d.root()) + g;
                        int tests = d.property(group, "tests");
                        StringBuilder t = new StringBuilder("[");
                        for (int i = 0; i < d.count(tests); i++) {
                            int test = d.first(tests) + i;
                            t.append(i == 0 ? "" : ",").append("{\"description\":")
                                    .append(d.toJson(d.property(test, "description"))).append(",\"instance\":")
                                    .append(d.toJson(d.property(test, "data"))).append('}');
                        }
                        t.append(']');
                        seq++;
                        JsonDocument response = h.send("{\"cmd\":\"run\",\"seq\":" + seq + ",\"case\":{\"description\":"
                                + d.toJson(d.property(group, "description")) + ",\"schema\":"
                                + d.toJson(d.property(group, "schema")) + ",\"registry\":" + registry + ",\"tests\":"
                                + t + "}}");
                        int r = response.root();
                        assertEquals(seq, (int) response.doubleValue(response.property(r, "seq")));
                        int results = response.property(r, "results");
                        for (int i = 0; i < d.count(tests); i++) {
                            total++;
                            boolean expected = d.bool(d.property(d.first(tests) + i, "valid"));
                            int result = results >= 0 ? response.first(results) + i : -1;
                            int valid = result >= 0 ? response.property(result, "valid") : -1;
                            if (valid < 0 || response.bool(valid) != expected) {
                                failures.add(draft[0] + "/" + file.getFileName() + ": "
                                        + d.toJson(d.property(group, "description")) + ": " + response);
                            }
                        }
                    }
                }
            }
        }
        assertTrue(failures.isEmpty(), failures.size() + " of " + total + " failed: " + failures);
        assertTrue(total > 4000, "only " + total + " tests ran");
    }

    @Test
    void annotationSuiteOverIhop() throws Exception {
        Path root = suiteRoot();
        Path dir = root.resolve("annotations").resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(dir), "annotation tests not found");
        String registry = registry(root);
        int seq = 0;
        int total = 0;
        List<String> failures = new ArrayList<>();
        try (Harness h = new Harness()) {
            for (String[] draft : DRAFTS) {
                h.send("{\"cmd\":\"dialect\",\"dialect\":" + quoted(draft[1]) + "}");
                for (Path file : jsonFiles(dir)) {
                    JsonDocument d = read(file);
                    int suite = d.property(d.root(), "suite");
                    for (int g = 0; g < d.count(suite); g++) {
                        int group = d.first(suite) + g;
                        int compat = d.property(group, "compatibility");
                        if (compat >= 0 && !compatible(draft[2], d.string(compat))) {
                            continue;
                        }
                        int tests = d.property(group, "tests");
                        StringBuilder t = new StringBuilder("[");
                        for (int i = 0; i < d.count(tests); i++) {
                            t.append(i == 0 ? "" : ",").append("{\"description\":\"\",\"instance\":")
                                    .append(d.toJson(d.property(d.first(tests) + i, "instance"))).append('}');
                        }
                        t.append(']');
                        seq++;
                        JsonDocument response = h.send("{\"cmd\":\"run\",\"seq\":" + seq
                                + ",\"output\":\"annotations\",\"case\":{\"description\":\"\",\"schema\":"
                                + d.toJson(d.property(group, "schema")) + ",\"registry\":" + registry + ",\"tests\":"
                                + t + "}}");
                        int results = response.property(response.root(), "results");
                        for (int i = 0; i < d.count(tests); i++) {
                            int test = d.first(tests) + i;
                            int result = response.first(results) + i;
                            int found = response.property(result, "annotations");
                            int assertions = d.property(test, "assertions");
                            for (int a = 0; a < d.count(assertions); a++) {
                                int assertion = d.first(assertions) + a;
                                total++;
                                String location = d.string(d.property(assertion, "location"));
                                String keyword = d.string(d.property(assertion, "keyword"));
                                int expected = d.property(assertion, "expected");
                                // The annotations for this location and keyword, by schema location.
                                StringBuilder actual = new StringBuilder("{");
                                for (int f = 0; f < response.count(found); f++) {
                                    int an = response.first(found) + f;
                                    if (!location.equals(response.string(response.property(an, "instanceLocation")))
                                            || !keyword.equals(response.string(response.property(an, "keyword")))) {
                                        continue;
                                    }
                                    String at = response.string(response.property(an, "keywordLocation"));
                                    String suffix = "/" + keyword;
                                    String schema = at.endsWith(suffix) ? at.substring(0, at.length() - suffix.length()) : at;
                                    actual.append(actual.length() > 1 ? "," : "").append(quoted(schema)).append(':')
                                            .append(response.toJson(response.property(an, "annotation")));
                                }
                                JsonDocument a2 = JsonDocument.parse(actual.append('}').toString());
                                if (!Values.equal(a2, a2.root(), d, expected)) {
                                    failures.add(file.getFileName() + " (" + draft[1] + "): " + location + " " + keyword
                                            + ": expected " + d.toJson(expected) + " got " + a2);
                                }
                            }
                        }
                    }
                }
            }
        }
        assertTrue(failures.isEmpty(), failures.size() + " of " + total + " failed: " + failures);
        assertTrue(total > 0);
    }
}
