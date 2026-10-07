package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;
import org.junit.jupiter.api.Test;

/**
 * Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
 * traced through the C# collecting path (row order, locations, messages, levels). A port of the Rust
 * {@code tests/results.rs} and the TypeScript {@code test/results.test.mjs}.
 */
class ResultsTest {
    static JsonDocument json(String text) {
        return JsonDocument.parse(text);
    }

    static String dump(Validator v, String instance, ResultsLevel level) {
        JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(level);
        v.evaluate(json(instance), c);
        return c.results().stream()
                .map(r -> (r.isMatch() ? "match" : "fail") + "|" + r.schemaEvaluationLocation() + "|"
                        + r.evaluationLocation() + "|" + r.documentEvaluationLocation() + "|" + r.message())
                .collect(Collectors.joining("\n"));
    }

    static String lines(String... lines) {
        return String.join("\n", lines);
    }

    static final String PERSON = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "type": "object",
              "title": "Person",
              "properties": {
                "name": { "type": "string", "minLength": 1, "description": "The name" },
                "age": { "type": "integer", "minimum": 0 }
              },
              "required": ["name"],
              "additionalProperties": false
            }""";

    static final String REFS = """
            {
              "$schema": "https://json-schema.org/draft/2020-12/schema",
              "$defs": {
                "fooId": { "type": "integer", "minimum": 0 },
                "holder": { "type": "object", "properties": { "fooId": { "$ref": "#/$defs/fooId" } } },
                "viaRef": { "$ref": "#/$defs/fooId" }
              }
            }""";

    static final String EXAMPLE = """
            {
              "type": "object",
              "properties": { "a": { "type": "string" } },
              "required": ["b"],
              "anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]
            }""";

    static Validator entry(String schema, String entryPoint) {
        return Validator.compile(schema, CompileOptions.builder().entryPoint(entryPoint).build());
    }

    @Test
    void flagAndCollectingEvaluationAgreeAtEveryLevel() {
        Validator v = Validator.compile(PERSON);
        Object[][] cases = {
            {"{\"name\": \"a\", \"age\": 3}", true},
            {"{\"name\": \"\", \"age\": 3}", false},
            {"{\"age\": 3}", false},
            {"{\"name\": \"a\", \"extra\": 1}", false},
            {"{\"name\": \"a\", \"age\": -1}", false},
            {"[]", false},
        };
        for (Object[] c : cases) {
            boolean expected = (Boolean) c[1];
            assertEquals(expected, v.isValid((String) c[0]));
            for (ResultsLevel level : ResultsLevel.values()) {
                assertEquals(expected, v.evaluate(json((String) c[0]), JsonSchemaResultsCollector.create(level)));
            }
        }
    }

    @Test
    void basicResultsReportFailingKeywordsWithLocations() {
        JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(ResultsLevel.BASIC);
        assertFalse(Validator.compile(PERSON).evaluate(json("{\"name\": \"\", \"age\": -1}"), c));
        List<SchemaResult> failures = c.results().stream().filter(r -> !r.isMatch()).collect(Collectors.toList());
        assertTrue(failures.stream().anyMatch(f -> f.evaluationLocation().endsWith("/minLength")
                && f.documentEvaluationLocation().equals("/name")));
        assertTrue(failures.stream().anyMatch(f -> f.evaluationLocation().endsWith("/minimum")
                && f.documentEvaluationLocation().equals("/age")));
        assertTrue(failures.stream().anyMatch(f -> f.schemaEvaluationLocation().equals("/properties/name")));
        assertTrue(c.results().stream().allMatch(r -> r.message().isEmpty()));
    }

    @Test
    void verboseAnnotationsAreProduced() {
        JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
        assertTrue(Validator.compile(PERSON).evaluate(json("{\"name\": \"a\"}"), c));
        Map<String, Map<String, Map<String, String>>> annotations = c.collectAnnotations();
        assertEquals("\"Person\"", annotations.get("").get("title").get("#"));
        assertEquals(1, annotations.get("").get("title").size());
        assertEquals("\"The name\"", annotations.get("/name").get("description").get("#/properties/name"));
    }

    @Test
    void verboseOutputFollowsTheCsharpRowOrder() {
        assertEquals(
                lines(
                        "match|/properties/name|/properties/name|/name|The value was expected to match the subschema.",
                        "match|/properties/name|/properties/name/description|/name|\"The name\"",
                        "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the "
                                + "value to be greater than or equal to '1'",
                        "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type "
                                + "'string'",
                        "match||||The value was expected to match the subschema.",
                        "match||/title||\"Person\"",
                        "match|/required|/required|/name|Required property present 'name'",
                        "match|/type|/type||The value was expected to be of type 'object'"),
                dump(Validator.compile(PERSON), "{\"name\": \"a\"}", ResultsLevel.VERBOSE));
    }

    @Test
    void matchingKeywordsCarryTheirMessageInVerboseOutput() {
        Validator v = Validator.compile("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "type": ["integer", "array"],
                  "uniqueItems": true,
                  "properties": { "n": { "type": "integer" } }
                }""");
        JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
        assertTrue(v.evaluate(json("[1, 2]"), c));
        assertTrue(c.results().stream().anyMatch(x -> x.isMatch() && x.evaluationLocation().equals("/type")
                && x.message().equals("The value was expected to be of type '[\"array\", \"integer\"]'")));
        assertTrue(c.results().stream().anyMatch(x -> x.isMatch() && x.evaluationLocation().equals("/uniqueItems")
                && !x.message().isEmpty()));
        c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
        assertFalse(v.evaluate(json("[1, 1]"), c));
        assertTrue(c.results().stream().anyMatch(x -> !x.isMatch() && x.evaluationLocation().equals("/uniqueItems")
                && !x.message().isEmpty()));
        c = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
        assertTrue(Validator.compile("{\"type\": \"integer\"}").evaluate(json("3"), c));
        assertTrue(c.results().stream().anyMatch(x -> x.isMatch() && x.evaluationLocation().equals("/type")
                && x.message().equals("The value was expected to be of type 'integer'")));
    }

    @Test
    void anEntryPointReportsItsOwnSchemaLocation() {
        assertEquals(
                lines(
                        "fail|/$defs/fooId|||The value was expected to match the subschema.",
                        "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'"),
                dump(entry(REFS, "#/$defs/fooId"), "\"notAnInteger\"", ResultsLevel.DETAILED));
    }

    @Test
    void aPureRefPropertyIsElidedWithRefInTheEvaluationPath() {
        assertEquals(
                lines(
                        "fail|/$defs/fooId|/properties/fooId/$ref|/fooId|The value was expected to match the "
                                + "subschema.",
                        "fail|/$defs/fooId/type|/properties/fooId/$ref/type|/fooId|The value was expected to be of "
                                + "type 'integer'",
                        "fail|/$defs/holder|||The value was expected to match the subschema."),
                dump(entry(REFS, "#/$defs/holder"), "{\"fooId\": \"notAnInteger\"}", ResultsLevel.DETAILED));
    }

    @Test
    void aPureRefRootReportsAgainstItsTarget() {
        assertEquals(
                lines(
                        "fail|/$defs/fooId|||The value was expected to match the subschema.",
                        "fail|/$defs/fooId/type|/type||The value was expected to be of type 'integer'"),
                dump(entry(REFS, "#/$defs/viaRef"), "\"notAnInteger\"", ResultsLevel.DETAILED));
    }

    @Test
    void aRequiredFailureCarriesThePropertyName() {
        assertEquals(
                lines(
                        "fail||||The value was expected to match the subschema.",
                        "fail|/required|/required|/name|Required property not present 'name'"),
                dump(Validator.compile("{\"type\": \"object\", \"required\": [\"name\"]}"), "{}",
                        ResultsLevel.DETAILED));
    }

    @Test
    void detailedOutputKeepsFailuresOnlyWithMessages() {
        String[] expected = {
            "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
            "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
            "fail||||The value was expected to match the subschema.",
            "fail|/required|/required|/b|Required property not present 'b'",
        };
        Validator v = Validator.compile(EXAMPLE);
        assertEquals(lines(expected), dump(v, "{\"a\": 1}", ResultsLevel.DETAILED));
        String[] basic = new String[expected.length];
        for (int i = 0; i < expected.length; i++) {
            basic[i] = expected[i].substring(0, expected[i].lastIndexOf('|') + 1);
        }
        assertEquals(lines(basic), dump(v, "{\"a\": 1}", ResultsLevel.BASIC));
    }

    @Test
    void verboseOutputReversesAContextsOwnRowsAfterItsSummary() {
        assertEquals(
                lines(
                        "fail|/properties/a|/properties/a|/a|The value was expected to match the subschema.",
                        "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
                        "match|/anyOf/0|/anyOf/0||The value was expected to match the subschema.",
                        "match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'",
                        "fail||||The value was expected to match the subschema.",
                        "match|/anyOf|/anyOf||The value matched at least one subschema.",
                        "fail|/required|/required|/b|Required property not present 'b'",
                        "match|/type|/type||The value was expected to be of type 'object'"),
                dump(Validator.compile(EXAMPLE), "{\"a\": 1}", ResultsLevel.VERBOSE));
    }

    @Test
    void notSubtreesAndBooleanSchemas() {
        assertEquals(
                lines(
                        "fail||||The value was expected to match the subschema.",
                        "fail|/not|/not||The value matched the subschema in a not composition, which means the "
                                + "evaluation was not a match."),
                dump(Validator.compile("{\"not\": {\"type\": \"string\"}}"), "\"x\"", ResultsLevel.DETAILED));
        assertEquals(
                lines("fail||||The value was expected to match the subschema.", "fail||||"),
                dump(Validator.compile("false"), "1", ResultsLevel.DETAILED));
    }

    @Test
    void aValidInstanceAtDetailedLevelYieldsOnlyThePassingRootRow() {
        assertEquals("match||||", dump(Validator.compile(PERSON), "{\"name\": \"a\"}", ResultsLevel.DETAILED));
    }

    @Test
    void propertyNamesKeepsTheObjectLocationAndAddsAFailureRowPerName() {
        assertEquals(
                lines(
                        "fail|/propertyNames|/propertyNames||The value was expected to match the subschema.",
                        "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to "
                                + "be less than or equal to '2'",
                        "fail||||The value was expected to match the subschema.",
                        "fail|/propertyNames|/propertyNames||The property name did not match the schema."),
                dump(Validator.compile("{\"propertyNames\": {\"maxLength\": 2}}"), "{\"abc\": 1}",
                        ResultsLevel.DETAILED));
    }

    @Test
    void draft4ExclusiveBoundsReportUnderExclusiveMaximumWithTheMaximum() {
        Validator v = Validator.compile("{\"$schema\": \"http://json-schema.org/draft-04/schema#\", \"maximum\": 3, "
                + "\"exclusiveMaximum\": true}");
        assertEquals(
                lines(
                        "fail||||The value was expected to match the subschema.",
                        "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'"),
                dump(v, "3", ResultsLevel.DETAILED));
    }

    @Test
    void failingAnyOfBranchesAreDiscardedAndUnevaluatedPropertiesHasNoMessage() {
        Validator v = Validator.compile("{\"anyOf\": [{\"properties\": {\"a\": true}}, {\"required\": [\"zz\"]}], "
                + "\"unevaluatedProperties\": false}");
        assertEquals(
                lines(
                        "fail|/unevaluatedProperties|/unevaluatedProperties|/b|The value was expected to match the "
                                + "subschema.",
                        "fail|/unevaluatedProperties|/unevaluatedProperties|/b|",
                        "fail||||The value was expected to match the subschema.",
                        "fail|/unevaluatedProperties|/unevaluatedProperties||"),
                dump(v, "{\"a\": 1, \"b\": 2}", ResultsLevel.DETAILED));
    }

    @Test
    void aCollectorAccumulatesAcrossEvaluations() {
        Validator v = Validator.compile(PERSON);
        JsonSchemaResultsCollector c = JsonSchemaResultsCollector.create(ResultsLevel.DETAILED);
        v.evaluate(json("{\"age\": \"x\"}"), c);
        int first = c.results().size();
        assertTrue(first > 0);
        v.evaluate(json("{\"age\": \"x\"}"), c);
        assertEquals(2 * first, c.results().size());
        c.reset();
        assertEquals(0, c.results().size());
    }

    @Test
    void dependenciesReportsUnderItsOwnNameInEveryDialect() {
        Validator v = Validator.compile("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "dependencies": { "a": ["b"], "c": { "required": ["d"] } }
                }""");
        assertEquals(
                lines(
                        "fail|/dependencies/c|/dependencies/c||The value was expected to match the subschema.",
                        "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
                        "fail||||The value was expected to match the subschema.",
                        "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it "
                                + "contained the property 'c'",
                        "fail|/dependencies|/dependencies|/b|Required property not present 'b'"),
                dump(v, "{\"a\": 1, \"c\": 1}", ResultsLevel.DETAILED));
        Validator modern = Validator.compile("{\"dependentRequired\": {\"a\": [\"b\"]}, \"dependentSchemas\": "
                + "{\"c\": {\"required\": [\"d\"]}}}");
        String rows = dump(modern, "{\"a\": 1, \"c\": 1}", ResultsLevel.DETAILED);
        assertTrue(rows.contains("fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'"));
        assertTrue(rows.contains("fail|/dependentSchemas/c|/dependentSchemas/c||"));
    }

    @Test
    void aStaticallyResolvedDynamicRefHopIsNamedDynamicRefInTheEvaluationPath() {
        Validator v = Validator.compile("""
                {
                  "$schema": "https://json-schema.org/draft/2020-12/schema",
                  "properties": { "p": { "$dynamicRef": "#/$defs/n" } },
                  "$defs": { "n": { "type": "integer" } }
                }""");
        assertEquals(
                lines(
                        "fail|/$defs/n|/properties/p/$dynamicRef|/p|The value was expected to match the subschema.",
                        "fail|/$defs/n/type|/properties/p/$dynamicRef/type|/p|The value was expected to be of type "
                                + "'integer'",
                        "fail||||The value was expected to match the subschema."),
                dump(v, "{\"p\": \"x\"}", ResultsLevel.DETAILED));
    }
}