package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/**
 * Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C#, TypeScript and
 * Rust runners: required and optional tests with format as an annotation, optional/format with format asserted. Every
 * case runs fail-fast and through a results collector at each level.
 *
 * <p>Set the {@code jsonSchemaTestSuite} system property (or {@code JSON_SCHEMA_TEST_SUITE}) to use another checkout,
 * and {@code SUITE_DRAFT} / {@code SUITE_FILTER} to narrow the run.
 */
class SuiteTest {
    static final String[][] DRAFTS = {
        {"draft4", "DRAFT4"}, {"draft6", "DRAFT6"}, {"draft7", "DRAFT7"}, {"draft2019-09", "DRAFT201909"},
        {"draft2020-12", "DRAFT202012"},
    };

    /** Exclusions, matching the C# runner. */
    static final List<String> EXCLUDED_FILES = List.of("draft4/optional/zeroTerminatedFloats.json");

    /** The number of test cases the suite has, less the exclusions: a smaller count means a stale or partial run. */
    static final int EXPECTED_TOTAL = 7966;

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

    static DocumentResolver remotes(Path root) {
        Path remotes = root.resolve("remotes");
        Map<String, JsonDocument> cache = new ConcurrentHashMap<>();
        return uri -> {
            String prefix = "http://localhost:1234/";
            if (!uri.startsWith(prefix)) {
                return null;
            }
            Path file = remotes.resolve(uri.substring(prefix.length()));
            if (!Files.isRegularFile(file)) {
                return null;
            }
            return cache.computeIfAbsent(uri, k -> read(file));
        };
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

    private void runFile(Dialect dialect, Path file, String label, boolean assertFormat, DocumentResolver resolver,
            String filter) {
        JsonDocument groups = read(file);
        int g = groups.first(groups.root());
        for (int gi = 0; gi < groups.count(groups.root()); gi++) {
            int group = g + gi;
            String description = groups.string(groups.property(group, "description"));
            if (filter != null && !description.contains(filter) && !label.contains(filter)) {
                continue;
            }
            CompileOptions options = CompileOptions.builder()
                    .defaultDialect(dialect)
                    .assertFormat(assertFormat ? Boolean.TRUE : null)
                    .documentResolver(resolver)
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
                    // Leap seconds are skipped in the format run, as in the C# runner.
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
        Path tests = root.resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(tests), "JSON-Schema-Test-Suite not found at " + root);
        DocumentResolver resolver = remotes(root);
        String draftFilter = System.getenv("SUITE_DRAFT");
        String filter = System.getenv("SUITE_FILTER");
        for (String[] draft : DRAFTS) {
            if (draftFilter != null && !draftFilter.equals(draft[0])) {
                continue;
            }
            Dialect dialect = Dialect.valueOf(draft[1]);
            Path dir = tests.resolve(draft[0]);
            for (Path f : files(dir)) {
                runFile(dialect, f, draft[0] + "/" + f.getFileName(), false, resolver, filter);
            }
            for (Path f : files(dir.resolve("optional"))) {
                String label = draft[0] + "/optional/" + f.getFileName();
                if (!EXCLUDED_FILES.contains(label)) {
                    runFile(dialect, f, label, false, resolver, filter);
                }
            }
            for (Path f : files(dir.resolve("optional").resolve("format"))) {
                runFile(dialect, f, draft[0] + "/optional/format/" + f.getFileName(), true, resolver, filter);
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