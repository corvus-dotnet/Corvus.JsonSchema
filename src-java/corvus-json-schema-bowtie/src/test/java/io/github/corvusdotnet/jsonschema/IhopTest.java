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
 * Drives the harness over IHOP as Bowtie does (start, dialect, run with each case's externalSchemas as the registry,
 * stop), in a separate JVM: every required case of the JSON-Schema-Test-Suite, and the annotation suite's assertions,
 * compared with the expected results. No containers.
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

    /** Bowtie's registry for a test case: its externalSchemas, keyed by their retrieval URIs. */
    static String registry(JsonDocument d, int testCase) {
        int schemas = d.property(testCase, "externalSchemas");
        return schemas < 0 ? "{}" : d.toJson(schemas);
    }

    /**
     * Whether a test case's {@code compatibility} (a comma-separated list of minimums, {@code <=} maximums and
     * {@code =} releases, from the smallest release to the largest) admits the release, by the algorithm of the
     * suite's validation/README.md.
     */
    static boolean compatible(JsonDocument d, int testCase, int release) {
        int c = d.property(testCase, "compatibility");
        if (c < 0) {
            return true;
        }
        boolean valid = true;
        for (String constraint : d.string(c).split(",")) {
            String op = "";
            String version = constraint;
            if (constraint.startsWith("<=")) {
                op = "<=";
                version = constraint.substring(2);
            } else if (constraint.startsWith("=")) {
                op = "=";
                version = constraint.substring(1);
            }
            int number = Integer.parseInt(version.trim());
            valid = op.equals("<=") ? release <= number : op.equals("=") ? release == number : release >= number;
            if (release <= number) {
                break;
            }
        }
        return valid;
    }

    /** Whether a test case has a tag. */
    static boolean hasTag(JsonDocument d, int testCase, String tag) {
        int tags = d.property(testCase, "tags");
        for (int i = 0; tags >= 0 && i < d.count(tags); i++) {
            if (tag.equals(d.string(d.first(tags) + i))) {
                return true;
            }
        }
        return false;
    }

    private static String quoted(String s) {
        StringBuilder sb = new StringBuilder();
        JsonDocument.appendQuoted(sb, s);
        return sb.toString();
    }

    @Test
    void requiredSuiteOverIhop() throws Exception {
        Path root = suiteRoot();
        Path tests = root.resolve("validation").resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(tests), "JSON-Schema-Test-Suite not found");
        int seq = 0;
        int total = 0;
        List<String> failures = new ArrayList<>();
        try (Harness h = new Harness()) {
            for (String[] draft : DRAFTS) {
                JsonDocument ok = h.send("{\"cmd\":\"dialect\",\"dialect\":" + quoted(draft[1]) + "}");
                assertTrue(ok.bool(ok.property(ok.root(), "ok")));
                int release = Integer.parseInt(draft[2]);
                for (Path file : jsonFiles(tests)) {
                    JsonDocument d = read(file);
                    int cases = d.property(d.root(), "tests");
                    for (int g = 0; g < d.count(cases); g++) {
                        int group = d.first(cases) + g;
                        // The harness treats format as an annotation in every dialect, as Bowtie expects.
                        if (!compatible(d, group, release) || hasTag(d, group, "format-assertion")) {
                            continue;
                        }
                        int caseTests = d.property(group, "tests");
                        StringBuilder t = new StringBuilder("[");
                        for (int i = 0; i < d.count(caseTests); i++) {
                            int test = d.first(caseTests) + i;
                            t.append(i == 0 ? "" : ",").append("{\"description\":")
                                    .append(d.toJson(d.property(test, "description"))).append(",\"instance\":")
                                    .append(d.toJson(d.property(test, "data"))).append('}');
                        }
                        t.append(']');
                        seq++;
                        JsonDocument response = h.send("{\"cmd\":\"run\",\"seq\":" + seq + ",\"case\":{\"description\":"
                                + d.toJson(d.property(group, "description")) + ",\"schema\":"
                                + d.toJson(d.property(group, "schema")) + ",\"registry\":" + registry(d, group)
                                + ",\"tests\":" + t + "}}");
                        int r = response.root();
                        assertEquals(seq, (int) response.doubleValue(response.property(r, "seq")));
                        int results = response.property(r, "results");
                        for (int i = 0; i < d.count(caseTests); i++) {
                            total++;
                            boolean expected = d.bool(d.property(d.first(caseTests) + i, "valid"));
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
        assertTrue(total > 5000, "only " + total + " tests ran");
    }

    /**
     * A match of a pattern that goes over its limit, and a pattern over a limit of size, are reported as errors:
     * the one for its test alone, the other for the whole case. Neither is a verdict.
     */
    @Test
    void aLimitOnAPatternIsAnErrorAndNoVerdict() throws Exception {
        try (Harness h = new Harness()) {
            h.send("{\"cmd\":\"dialect\",\"dialect\":\"https://json-schema.org/draft/2020-12/schema\"}");
            JsonDocument r = h.send("{\"cmd\":\"run\",\"seq\":1,\"case\":{\"description\":\"limit\",\"schema\":"
                    + "{\"pattern\":\"^(?:(a+)\\\\1*)+$\"},\"tests\":[{\"description\":\"runaway\",\"instance\":\""
                    + "a".repeat(60) + "b\"},{\"description\":\"answered\",\"instance\":\"aaa\"}]}}");
            int results = r.property(r.root(), "results");
            assertEquals(2, r.count(results));
            int first = r.first(results);
            assertTrue(r.bool(r.property(first, "errored")), r.toString());
            assertTrue(r.property(first, "valid") < 0, r.toString());
            assertTrue(r.toString().contains("PatternMatchLimitException"), r.toString());
            assertTrue(r.bool(r.property(first + 1, "valid")), r.toString());
            JsonDocument large = h.send("{\"cmd\":\"run\",\"seq\":2,\"case\":{\"description\":\"size\",\"schema\":"
                    + "{\"pattern\":\"" + "\\\\p{L}".repeat(400) + "\"},\"tests\":[{\"description\":\"x\",\"instance\":\"a\"}]}}");
            assertTrue(large.bool(large.property(large.root(), "errored")), large.toString());
            assertTrue(large.property(large.root(), "results") < 0, large.toString());
            assertTrue(large.toString().contains("PatternTooLargeException"), large.toString());
        }
    }

    @Test
    void annotationSuiteOverIhop() throws Exception {
        Path root = suiteRoot();
        Path dir = root.resolve("annotations").resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(dir), "annotation tests not found");
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
                        if (!compatible(d, group, Integer.parseInt(draft[2]))) {
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
                                + d.toJson(d.property(group, "schema")) + ",\"registry\":" + registry(d, group)
                                + ",\"tests\":" + t + "}}");
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
