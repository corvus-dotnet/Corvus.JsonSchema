package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

/**
 * Runs the JSON-Schema-Test-Suite annotation tests ({@code JSON-Schema-Test-Suite/annotations}) through a verbose
 * results collector, as the C#, TypeScript and Rust runners do: every draft, cases filtered by "compatibility", and
 * each assertion compared with the annotations grouped by instance location, keyword and schema location.
 */
class AnnotationSuiteTest {
    private static final List<String> ORDER = List.of("3", "4", "6", "7", "2019", "2020");

    static boolean compatible(String level, String compat) {
        int l = ORDER.indexOf(level);
        if (compat.startsWith("<=")) {
            int max = ORDER.indexOf(compat.substring(2));
            return max >= 0 && l <= max;
        }
        int min = ORDER.indexOf(compat);
        return min >= 0 && l >= min;
    }

    @Test
    void annotationSuite() {
        Path root = SuiteTest.suiteRoot();
        Path dir = root.resolve("annotations").resolve("tests");
        Assumptions.assumeTrue(Files.isDirectory(dir), "annotation tests not found at " + dir);
        DocumentResolver resolver = SuiteTest.remotes(root);
        String[][] drafts = {
            {"draft4", "DRAFT4", "4"}, {"draft6", "DRAFT6", "6"}, {"draft7", "DRAFT7", "7"},
            {"draft2019-09", "DRAFT201909", "2019"}, {"draft2020-12", "DRAFT202012", "2020"},
        };
        int total = 0;
        List<String> failures = new ArrayList<>();
        for (String[] draft : drafts) {
            for (Path file : SuiteTest.files(dir)) {
                JsonDocument d = SuiteTest.read(file);
                int suite = d.property(d.root(), "suite");
                for (int gi = 0; gi < d.count(suite); gi++) {
                    int group = d.first(suite) + gi;
                    int compat = d.property(group, "compatibility");
                    if (compat >= 0 && !compatible(draft[2], d.string(compat))) {
                        continue;
                    }
                    int descriptionNode = d.property(group, "description");
                    String description = descriptionNode >= 0 ? d.string(descriptionNode) : "";
                    CompileOptions options = CompileOptions.builder()
                            .defaultDialect(Dialect.valueOf(draft[1]))
                            .documentResolver(resolver)
                            .build();
                    Validator validator;
                    try {
                        validator = Validator.compile(SuiteTest.sub(d, d.property(group, "schema")), options);
                    } catch (RuntimeException e) {
                        failures.add(draft[0] + "/" + file.getFileName() + " [" + description + "]: " + e);
                        continue;
                    }
                    int tests = d.property(group, "tests");
                    for (int ti = 0; ti < d.count(tests); ti++) {
                        int test = d.first(tests) + ti;
                        int instance = d.property(test, "instance");
                        JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
                        validator.evaluate(SuiteTest.sub(d, instance), collector);
                        Map<String, Map<String, Map<String, String>>> produced = collector.collectAnnotations();
                        int assertions = d.property(test, "assertions");
                        for (int ai = 0; ai < d.count(assertions); ai++) {
                            int assertion = d.first(assertions) + ai;
                            total++;
                            String location = d.string(d.property(assertion, "location"));
                            String keyword = d.string(d.property(assertion, "keyword"));
                            int expected = d.property(assertion, "expected");
                            Map<String, String> actual = produced.getOrDefault(location, Map.of()).get(keyword);
                            boolean expectedEmpty = d.count(expected) == 0;
                            boolean ok = actual == null ? expectedEmpty : !expectedEmpty && same(actual, d, expected);
                            if (!ok) {
                                failures.add(draft[0] + "/" + file.getFileName() + " [" + description + "] instance "
                                        + d.toJson(instance) + " '" + location + "' " + keyword + ": expected "
                                        + d.toJson(expected) + ", actual " + actual);
                            }
                        }
                    }
                }
            }
        }
        failures.forEach(System.out::println);
        System.out.println((total - failures.size()) + "/" + total + " annotation assertions passed");
        assertTrue(total > 0, "no annotation assertions ran");
        assertTrue(failures.isEmpty(), failures.size() + " annotation assertions failed");
    }

    /** Whether the produced annotations (JSON text by schema location) equal the expected object. */
    private static boolean same(Map<String, String> actual, JsonDocument d, int expected) {
        if (actual.size() != d.count(expected)) {
            return false;
        }
        for (int i = 0; i < d.count(expected); i++) {
            int k = d.first(expected) + 2 * i;
            String value = actual.get(d.string(k));
            if (value == null) {
                return false;
            }
            JsonDocument a = JsonDocument.parse(value);
            if (!Values.equal(a, a.root(), d, k + 1)) {
                return false;
            }
        }
        return true;
    }
}