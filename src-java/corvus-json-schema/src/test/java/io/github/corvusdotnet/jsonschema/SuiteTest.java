package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/**
 * Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C#, TypeScript and
 * Rust runners. Every dialect's cases are in one tree ({@code validation/tests}, required and optional): each case's
 * {@code compatibility} selects the drafts it applies to, its {@code tags} whether format is asserted, and its
 * {@code externalSchemas} are served to that case alone. Every case runs fail-fast and through a results collector at
 * each level.
 *
 * <p>Set the {@code jsonSchemaTestSuite} system property (or {@code JSON_SCHEMA_TEST_SUITE}) to use another checkout,
 * and {@code SUITE_DRAFT} / {@code SUITE_FILTER} to narrow the run.
 */
class SuiteTest {
    static final String[][] DRAFTS = {
        {"draft4", "DRAFT4", "4"}, {"draft6", "DRAFT6", "6"}, {"draft7", "DRAFT7", "7"},
        {"draft2019-09", "DRAFT201909", "2019"}, {"draft2020-12", "DRAFT202012", "2020"},
    };

    /** Exclusions, matching the C# runner. */
    static final List<String> EXCLUDED_FILES = List.of("draft4/optional/zero-terminated-floats.json");

    /** The number of test cases the suite has, less the exclusions: a smaller count means a stale or partial run. */
    static final int EXPECTED_TOTAL = 8686;

    static Path suiteRoot() {
        String p = System.getProperty("jsonSchemaTestSuite");
        if (p == null) {
            p = System.getenv("JSON_SCHEMA_TEST_SUITE");
        }
        return Paths.get(p != null ? p : "../../JSON-Schema-Test-Suite");
    }

    static List<Path> files(Path dir) {
        if (!Files.isDirectory(dir)) {
            return List.of();
        }
        try (Stream<Path> s = Files.list(dir)) {
            return s.filter(f -> f.toString().endsWith(".json")).sorted().collect(Collectors.toList());
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    static JsonDocument read(Path file) {
        try {
            return JsonDocument.parse(Files.readAllBytes(file));
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    /** A value of a document as a document of its own. */
    static JsonDocument sub(JsonDocument d, int node) {
        return JsonDocument.parse(d.toJson(node));
    }

    /**
     * Whether a test case's {@code compatibility} (a comma-separated list of minimums, {@code <=} maximums and
     * {@code =} releases, from the smallest release to the largest) admits the release, by the algorithm of the
     * suite's validation/README.md.
     */
    static boolean compatible(String compatibility, int release) {
        if (compatibility == null) {
            return true;
        }
        boolean valid = true;
        for (String constraint : compatibility.split(",")) {
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
        if (tags < 0) {
            return false;
        }
        int t = d.first(tags);
        for (int i = 0; i < d.count(tags); i++) {
            if (tag.equals(d.string(t + i))) {
                return true;
            }
        }
        return false;
    }

    /** A test case's {@code compatibility}, or null if it has none. */
    static String compatibility(JsonDocument d, int testCase) {
        int c = d.property(testCase, "compatibility");
        return c < 0 ? null : d.string(c);
    }

    /** A resolver serving a test case's {@code externalSchemas} at their retrieval URIs, or null if it has none. */
    static DocumentResolver externalSchemas(JsonDocument d, int testCase) {
        int schemas = d.property(testCase, "externalSchemas");
        if (schemas < 0) {
            return null;
        }
        Map<String, JsonDocument> documents = new HashMap<>();
        int k = d.first(schemas);
        for (int i = 0; i < d.count(schemas); i++, k += 2) {
            documents.put(d.string(k), sub(d, k + 1));
        }
        return documents::get;
    }

    private int total;
    private int groups;
    private int compiled;
    private final List<String> failures = new ArrayList<>();

    private static final ResultsLevel[] LEVELS = ResultsLevel.values();

    private static String runCase(Validator v, JsonDocument data) {
        boolean fast;
        try {
            fast = v.validate(data);
        } catch (RuntimeException e) {
            return "fast: " + e;
        }
        for (ResultsLevel level : LEVELS) {
            JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(level);
            boolean ok;
            try {
                ok = v.evaluate(data, c);
            } catch (RuntimeException e) {
                return level + ": " + e;
            }
            if (ok != fast) {
                return level + " returned " + ok + ", fast returned " + fast;
            }
            boolean summary = false;
            for (SchemaResult r : c.results()) {
                summary |= r.evaluationLocation().isEmpty() && r.documentEvaluationLocation().isEmpty()
                        && r.isMatch() == ok;
            }
            if (!summary) {
                return level + ": no root summary row matching the result";
            }
        }
        return Boolean.toString(fast);
    }

    private void runFile(Dialect dialect, int release, Path file, String label, String filter) {
        JsonDocument groups = read(file);
        int cases = groups.property(groups.root(), "tests");
        int g = groups.first(cases);
        for (int gi = 0; gi < groups.count(cases); gi++) {
            int group = g + gi;
            if (!compatible(compatibility(groups, group), release)) {
                continue;
            }
            // The engine asserts format by default up to draft7, so a format-annotation case does not apply there.
            if (hasTag(groups, group, "format-annotation") && release <= 7) {
                continue;
            }
            boolean assertFormat = hasTag(groups, group, "format-assertion");
            String description = groups.string(groups.property(group, "description"));
            if (filter != null && !description.contains(filter) && !label.contains(filter)) {
                continue;
            }
            CompileOptions options = CompileOptions.builder()
                    .defaultDialect(dialect)
                    .assertFormat(assertFormat ? Boolean.TRUE : null)
                    .documentResolver(externalSchemas(groups, group))
                    .build();
            Validator v = null;
            String compileError = null;
            try {
                v = Validator.compile(sub(groups, groups.property(group, "schema")), options);
                this.groups++;
                if (v.isCompiled()) {
                    compiled++;
                }
            } catch (RuntimeException e) {
                compileError = "compile error: " + e;
            }
            int tests = groups.property(group, "tests");
            int t = groups.first(tests);
            for (int ti = 0; ti < groups.count(tests); ti++) {
                int test = t + ti;
                total++;
                boolean expected = groups.bool(groups.property(test, "valid"));
                String testDescription = groups.string(groups.property(test, "description"));
                String actual = compileError != null
                        ? compileError
                        : runCase(v, sub(groups, groups.property(test, "data")));
                if (!actual.equals(Boolean.toString(expected))) {
                    // Leap seconds are skipped where format is asserted, as in the C# runner.
                    if (assertFormat && testDescription.toLowerCase(Locale.ROOT).contains("leap second")) {
                        continue;
                    }
                    failures.add(label + " [" + description + "] " + testDescription + ": expected " + expected
                            + ", got " + actual);
                }
            }
        }
    }

    @Test
    void jsonSchemaTestSuite() {
        Path root = suiteRoot();
        Path tests = root.resolve("validation").resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(tests), "JSON-Schema-Test-Suite not found at " + root);
        String draftFilter = System.getenv("SUITE_DRAFT");
        String filter = System.getenv("SUITE_FILTER");
        List<Path> suiteFiles = new ArrayList<>(files(tests));
        suiteFiles.addAll(files(tests.resolve("optional")));
        for (String[] draft : DRAFTS) {
            if (draftFilter != null && !draftFilter.equals(draft[0])) {
                continue;
            }
            Dialect dialect = Dialect.valueOf(draft[1]);
            int release = Integer.parseInt(draft[2]);
            for (Path f : suiteFiles) {
                String label = draft[0] + "/" + tests.relativize(f).toString().replace('\\', '/');
                if (!EXCLUDED_FILES.contains(label)) {
                    runFile(dialect, release, f, label, filter);
                }
            }
        }
        failures.forEach(System.out::println);
        System.out.println("JSON-Schema-Test-Suite: " + (total - failures.size()) + "/" + total + " passed ("
                + compiled + " of " + groups + " schemas compiled)");
        assertTrue(failures.isEmpty(), failures.size() + " failures; first: "
                + (failures.isEmpty() ? "" : failures.get(0)));
        if (draftFilter == null && filter == null) {
            assertEquals(EXPECTED_TOTAL, total, "test case count");
        }
        if (!Validator.INTERPRET) {
            // Only schemas with a live dynamic scope run on the interpreter.
            assertTrue(compiled > groups * 9 / 10, compiled + " of " + groups + " schemas compiled");
        }
    }
}