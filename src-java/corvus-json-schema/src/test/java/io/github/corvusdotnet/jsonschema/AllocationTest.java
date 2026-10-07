package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.lang.management.ManagementFactory;
import java.util.ArrayList;
import java.util.List;
import org.junit.jupiter.api.Test;

/**
 * Validation allocates nothing in the steady state: every keyword path, valid and invalid instances, compiled and
 * interpreted. Measured with the thread's allocated-bytes counter over many validations after warm-up.
 */
class AllocationTest {
    /** A schema, the options it compiles with, and instances (valid and invalid) to validate against it. */
    static final class Case {
        final String name;
        final String schema;
        final CompileOptions options;
        final String[] instances;

        Case(String name, String schema, CompileOptions options, String... instances) {
            this.name = name;
            this.schema = schema;
            this.options = options;
            this.instances = instances;
        }
    }

    static final CompileOptions FORMATS = CompileOptions.builder().assertFormat(true).build();
    static final CompileOptions DEFAULT = CompileOptions.DEFAULT;

    static List<Case> cases() {
        List<Case> c = new ArrayList<>();
        c.add(new Case("object", """
                {"type": "object", "properties": {"name": {"type": "string", "minLength": 1, "maxLength": 20},
                 "age": {"type": "integer", "minimum": 0}, "tags": {"type": "array", "items": {"type": "string"}}},
                 "required": ["name"], "additionalProperties": false}""", DEFAULT,
                "{\"name\": \"a\", \"age\": 3, \"tags\": [\"x\", \"y\"]}", "{\"name\": \"caf\\u00e9 中\"}",
                "{\"name\": \"\"}", "{\"age\": 1}", "{\"name\": \"a\", \"other\": 1}", "[]"));
        c.add(new Case("many properties", """
                {"properties": {"a": {"type": "integer"}, "b": {"type": "string"}, "c": true, "d": true, "e": true,
                 "f": true, "g": true, "h": true, "i": true, "j": {"type": "boolean"}}, "required": ["a", "j"],
                 "minProperties": 2, "maxProperties": 20}""", DEFAULT,
                "{\"a\": 1, \"j\": true, \"b\": \"x\", \"zz\": null}", "{\"a\": \"x\", \"j\": true}", "{\"j\": true}"));
        c.add(new Case("patterns", """
                {"properties": {"id": {"type": "string", "pattern": "^[a-z][a-z0-9_]*$"},
                 "code": {"type": "string", "pattern": "^(ab|cd)+e$"}, "line": {"pattern": "^.{1,10}$"}},
                 "patternProperties": {"^x-": {"type": "string"}, "[0-9]{3}": {"type": "number"}},
                 "additionalProperties": {"type": "boolean"}, "propertyNames": {"maxLength": 12}}""", DEFAULT,
                "{\"id\": \"abc_1\", \"code\": \"abcde\", \"line\": \"short\", \"x-a\": \"s\", \"a123\": 1, \"q\": true}",
                "{\"code\": \"éabcde\", \"line\": \"café\"}", "{\"id\": \"1abc\"}", "{\"code\": \"abx\"}",
                "{\"q\": 1}", "{\"a-very-long-property-name\": true}"));
        c.add(new Case("enum and const", """
                {"properties": {"few": {"enum": ["a", "b", "c"]}, "many": {"enum": ["a", "b", "c", "d", "e", "f",
                 "g", "h", "i", "j", "k"]}, "mixed": {"enum": [1, "x", null, true, {"a": [1]}]},
                 "c": {"const": "fixed"}, "n": {"const": 2.0}}}""", DEFAULT,
                "{\"few\": \"b\", \"many\": \"k\", \"mixed\": {\"a\": [1.0]}, \"c\": \"fixed\", \"n\": 2}",
                "{\"few\": \"z\"}", "{\"many\": \"z\"}", "{\"mixed\": 2}", "{\"c\": \"other\"}", "{\"n\": 3}"));
        c.add(new Case("numbers", """
                {"properties": {"i": {"type": "integer", "minimum": 1, "maximum": 100, "multipleOf": 3},
                 "d": {"type": "number", "exclusiveMinimum": 0, "exclusiveMaximum": 1000.5, "multipleOf": 0.01},
                 "big": {"minimum": 1e300}, "f": {"format": "int32"}}}""", FORMATS,
                "{\"i\": 9, \"d\": 12.34, \"big\": 1e301, \"f\": 7}", "{\"i\": 10}", "{\"d\": 12.345}",
                "{\"d\": 1e-7}", "{\"i\": 1.5}", "{\"big\": 18446744073709551615}", "{\"f\": 1e20}"));
        c.add(new Case("arrays", """
                {"type": "array", "prefixItems": [{"type": "string"}, {"type": "integer"}],
                 "items": {"type": ["integer", "string"]}, "contains": {"const": 5}, "minContains": 1,
                 "maxContains": 2, "minItems": 2, "maxItems": 40, "uniqueItems": true}""", DEFAULT,
                "[\"a\", 1, 5, 6, 7]", "[\"a\", 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]",
                "[\"a\", 1, 5, 5]", "[\"a\", \"b\"]", "[1]", "[\"a\", 1, 5, 5, 5]"));
        c.add(new Case("unique objects", """
                {"uniqueItems": true}""", DEFAULT,
                "[{\"a\": 1}, {\"a\": 2}, [1, 2], [2, 1], 1, 1.5, \"x\", null, true, false, {\"b\": {\"c\": 1}}, 2, 3,"
                        + " 4, 5, 6, 7, 8, 9]",
                "[{\"a\": 1, \"b\": 2}, {\"b\": 2, \"a\": 1}, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]"));
        c.add(new Case("composition", """
                {"oneOf": [{"properties": {"kind": {"const": "a"}, "x": {"type": "integer"}}, "required": ["kind"]},
                  {"properties": {"kind": {"const": "b"}}, "required": ["kind"]}],
                 "anyOf": [{"type": "object"}, {"type": "array"}],
                 "allOf": [{"$ref": "#/$defs/base"}, {"not": {"required": ["forbidden"]}}],
                 "if": {"properties": {"kind": {"const": "a"}}}, "then": {"required": ["x"]}, "else": {"required": ["y"]},
                 "dependentRequired": {"p": ["q"]}, "dependentSchemas": {"r": {"required": ["s"]}},
                 "$defs": {"base": {"type": "object", "properties": {"kind": {"type": "string"}}}}}""", DEFAULT,
                "{\"kind\": \"a\", \"x\": 1}", "{\"kind\": \"b\", \"y\": 1}", "{\"kind\": \"c\", \"y\": 1}",
                "{\"kind\": \"a\"}", "{\"kind\": \"a\", \"x\": 1, \"forbidden\": 1}", "{\"kind\": \"b\", \"y\": 1, \"p\": 1}",
                "{\"kind\": \"b\", \"y\": 1, \"r\": 1}"));
        c.add(new Case("formats", """
                {"properties": {"date": {"format": "date"}, "dt": {"format": "date-time"}, "time": {"format": "time"},
                 "email": {"format": "email"}, "ipv4": {"format": "ipv4"}, "ipv6": {"format": "ipv6"},
                 "uuid": {"format": "uuid"}, "uri": {"format": "uri"}, "uriRef": {"format": "uri-reference"},
                 "host": {"format": "hostname"}, "dur": {"format": "duration"}, "ptr": {"format": "json-pointer"},
                 "re": {"format": "regex"}, "tpl": {"format": "uri-template"}, "iri": {"format": "iri"},
                 "rel": {"format": "relative-json-pointer"}, "idnHost": {"format": "idn-hostname"},
                 "idnEmail": {"format": "idn-email"}, "unknown": {"format": "made-up"}}}""", FORMATS,
                "{\"date\": \"2020-02-29\", \"dt\": \"1963-06-19T08:30:06.283185Z\", \"time\": \"08:30:06Z\","
                        + " \"email\": \"joe@example.com\", \"ipv4\": \"192.168.0.1\", \"ipv6\": \"::ffff:192.168.0.1\","
                        + " \"uuid\": \"2eb8aa08-aa98-11ea-b4aa-73b441d16380\", \"uri\": \"http://example.com/a?b#c\","
                        + " \"uriRef\": \"../a\", \"host\": \"example.com\", \"dur\": \"P4DT12H30M5S\","
                        + " \"ptr\": \"/a/b\", \"re\": \"^a+$\", \"tpl\": \"http://example.com/{id}\","
                        + " \"iri\": \"http://éxample.com\", \"rel\": \"0/a\", \"idnHost\": \"실례.테스트\","
                        + " \"idnEmail\": \"실례@실례.테스트\", \"unknown\": \"x\"}",
                "{\"date\": \"2021-02-29\"}", "{\"dt\": \"1963-06-19 08:30:06Z\"}", "{\"email\": \"joe\"}",
                "{\"ipv4\": \"256.1.1.1\"}", "{\"uri\": \"//example.com\"}", "{\"host\": \"-a.com\"}",
                "{\"re\": \"(\"}", "{\"idnHost\": \"〮실례.테스트\"}"));
        c.add(new Case("content", """
                {"$schema": "http://json-schema.org/draft-07/schema#", "properties": {
                 "b64": {"contentEncoding": "base64"}, "json": {"contentMediaType": "application/json"},
                 "both": {"contentEncoding": "base64", "contentMediaType": "application/json"}}}""", DEFAULT,
                "{\"b64\": \"aGVsbG8=\", \"json\": \"{\\\"a\\\": [1, 2]}\", \"both\": \"eyJhIjogMX0=\"}",
                "{\"b64\": \"*\"}", "{\"json\": \"{\"}", "{\"both\": \"aGVsbG8=\"}"));
        c.add(new Case("unevaluated", """
                {"properties": {"a": true}, "anyOf": [{"properties": {"b": true}}, {"properties": {"c": true}}],
                 "unevaluatedProperties": false, "items": true, "unevaluatedItems": false}""", DEFAULT,
                "{\"a\": 1, \"b\": 2}", "{\"a\": 1, \"d\": 2}", "[1, 2]"));
        c.add(new Case("dynamic scope", """
                {"$id": "https://example.com/tree", "$dynamicAnchor": "node", "type": "object",
                 "properties": {"data": true, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}},
                 "$defs": {"strict": {"$id": "strict", "$dynamicAnchor": "node", "$ref": "tree",
                   "unevaluatedProperties": false}}}""", DEFAULT,
                "{\"data\": 1, \"children\": [{\"data\": 2, \"children\": []}]}", "{\"children\": [1]}"));
        c.add(new Case("recursion", """
                {"$schema": "https://json-schema.org/draft/2020-12/schema", "$defs": {"a": {"anyOf": [{"$ref": "#/$defs/a"},
                 {"type": "integer"}]}}, "$ref": "#/$defs/a"}""",
                CompileOptions.builder().maxDepth(8).build(), "1", "\"x\""));
        return c;
    }

    private static long allocated() {
        return ((com.sun.management.ThreadMXBean) ManagementFactory.getThreadMXBean()).getCurrentThreadAllocatedBytes();
    }

    /**
     * Says where a failing case allocates: the same passes again in blocks of 100 (an allocation that is not there on
     * the second measurement, or is in one block only, happened once), then each instance by each entry.
     */
    private static String breakdown(Validator v, Case c, JsonDocument[] docs, byte[][] utf8, long overhead) {
        StringBuilder text = new StringBuilder();
        int sink = 0;
        long[] blocks = new long[20];
        for (int b = 0; b < blocks.length; b++) {
            long before = allocated();
            for (int r = 0; r < 100; r++) {
                for (int i = 0; i < docs.length; i++) {
                    sink += v.isValid(docs[i]) ? 1 : 0;
                    sink += v.isValid(c.instances[i]) ? 1 : 0;
                    sink += v.isValid(utf8[i]) ? 1 : 0;
                }
            }
            blocks[b] = allocated() - before - overhead;
        }
        text.append(" [again, by 100 passes: ").append(java.util.Arrays.toString(blocks)).append(']');
        for (int i = 0; i < docs.length; i++) {
            long[] by = new long[3];
            for (int entry = 0; entry < 3; entry++) {
                long before = allocated();
                for (int r = 0; r < 2_000; r++) {
                    sink += (entry == 0 ? v.isValid(docs[i]) : entry == 1 ? v.isValid(c.instances[i]) : v.isValid(utf8[i]))
                            ? 1 : 0;
                }
                by[entry] = allocated() - before - overhead;
            }
            if (by[0] != 0 || by[1] != 0 || by[2] != 0) {
                text.append(" [instance ").append(i).append(", 2000 calls, document/text/bytes: ")
                        .append(java.util.Arrays.toString(by)).append(']');
            }
        }
        return sink >= 0 ? text.toString() : "";
    }

    @Test
    void validationAllocatesNothingInTheSteadyState() {
        List<String> failures = new ArrayList<>();
        for (Case c : cases()) {
            Validator v = Validator.compile(c.schema, c.options);
            JsonDocument[] docs = new JsonDocument[c.instances.length];
            for (int i = 0; i < docs.length; i++) {
                docs[i] = JsonDocument.parse(c.instances[i]);
            }
            byte[][] utf8 = new byte[c.instances.length][];
            for (int i = 0; i < utf8.length; i++) {
                utf8[i] = c.instances[i].getBytes(java.nio.charset.StandardCharsets.UTF_8);
            }
            int sink = 0;
            for (int r = 0; r < 20_000; r++) {
                for (int i = 0; i < docs.length; i++) {
                    sink += v.isValid(docs[i]) ? 1 : 0;
                    sink += v.isValid(c.instances[i]) ? 1 : 0;
                    sink += v.isValid(utf8[i]) ? 1 : 0;
                }
            }
            // The counter itself allocates a little: measure it alone and subtract.
            long overhead = -allocated() + allocated();
            int rounds = 2_000;
            long before = allocated();
            for (int r = 0; r < rounds; r++) {
                for (int i = 0; i < docs.length; i++) {
                    sink += v.isValid(docs[i]) ? 1 : 0;
                    sink += v.isValid(c.instances[i]) ? 1 : 0;
                    sink += v.isValid(utf8[i]) ? 1 : 0;
                }
            }
            long bytes = allocated() - before - overhead;
            if (bytes > 256) {
                failures.add(c.name + (v.isCompiled() ? " (compiled)" : " (interpreted)") + ": " + bytes + " bytes in "
                        + rounds + " passes of " + docs.length + " instances" + breakdown(v, c, docs, utf8, overhead));
            }
            assertTrue(sink >= 0);
        }
        failures.forEach(System.out::println);
        assertTrue(failures.isEmpty(), String.join("; ", failures));
    }
}