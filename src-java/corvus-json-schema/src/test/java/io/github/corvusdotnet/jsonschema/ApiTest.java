package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;

/** The public API: compiling, the ways to validate, options, and errors. */
class ApiTest {
    static final String SCHEMA = """
            {"type": "object", "properties": {"id": {"type": "integer", "minimum": 1},
             "name": {"type": "string", "minLength": 1}}, "required": ["id"]}""";

    @Test
    void everyWayToValidateAgrees() {
        Validator v = Validator.compile(SCHEMA);
        String[] valid = {"{\"id\": 3}", "{\"id\": 3, \"name\": \"caf\u00e9 \ud83d\ude00\"}"};
        String[] invalid = {"{\"id\": 0}", "{}", "[]", "{\"id\": 1, \"name\": \"\"}"};
        for (String t : valid) {
            assertTrue(v.isValid(t), t);
            assertTrue(v.isValid(t.getBytes(StandardCharsets.UTF_8)), t);
            assertTrue(v.isValid(JsonDocument.parse(t)), t);
            assertTrue(v.validate(JsonDocument.parse(t)), t);
        }
        for (String t : invalid) {
            assertFalse(v.isValid(t), t);
            assertFalse(v.isValid(t.getBytes(StandardCharsets.UTF_8)), t);
            assertFalse(v.isValid(JsonDocument.parse(t)), t);
            assertFalse(v.validate(JsonDocument.parse(t)), t);
        }
    }

    @Test
    void textWithEscapesNumbersAndLoneSurrogates() {
        Validator v = Validator.compile("{\"properties\": {\"a\": {\"const\": \"x\\ny\"}, \"n\": {\"multipleOf\": 0.01}}}");
        assertTrue(v.isValid("{\"a\": \"x\\ny\", \"n\": 12.34}"));
        assertFalse(v.isValid("{\"a\": \"x\\ny\", \"n\": 12.345}"));
        // A reused buffer holds the longer text first: the shorter one must not read its leftovers.
        assertFalse(v.isValid("{\"n\": 1.2345678}"));
        assertTrue(v.isValid("{\"n\": 1.2}"));
        // A lone surrogate is written as '?', as String.getBytes does.
        assertTrue(Validator.compile("{\"const\": \"a?\"}").isValid("\"a\ud800\""));
    }

    @Test
    void invalidJsonThrows() {
        Validator v = Validator.compile(SCHEMA);
        JsonParseException e = assertThrows(JsonParseException.class, () -> v.isValid("{\"id\": }"));
        assertEquals(7, e.offset());
        assertThrows(JsonParseException.class, () -> v.isValid(new byte[] {'[', '1'}));
        assertThrows(JsonParseException.class, () -> JsonDocument.parse("tru"));
        // The validator is usable after a parse error.
        assertTrue(v.isValid("{\"id\": 5}"));
    }

    @Test
    void aFormatCallbackMayValidateWithTheSameValidator() {
        AtomicReference<Validator> self = new AtomicReference<>();
        CompileOptions options = CompileOptions.builder()
                .format("nested", s -> self.get().isValid(s))
                .assertFormat(true)
                .build();
        Validator v = Validator.compile("{\"anyOf\": [{\"type\": \"integer\"}, {\"type\": \"string\", \"format\": "
                + "\"nested\"}]}", options);
        self.set(v);
        assertTrue(v.isValid("\"42\""));
        assertFalse(v.isValid("\"true\""));
        assertTrue(v.isValid("7"));
    }

    @Test
    void recursionBeyondTheMaximumDepth() {
        // anyOf recovers through its true branch, so the instance is valid, but validate() reports the depth.
        Validator v = Validator.compile("{\"$defs\": {\"a\": {\"anyOf\": [{\"$ref\": \"#/$defs/a\"}, true]}}, "
                + "\"$ref\": \"#/$defs/a\"}", CompileOptions.builder().maxDepth(4).build());
        assertTrue(v.isValid("1"));
        Validator loop = Validator.compile("{\"$defs\": {\"a\": {\"allOf\": [{\"$ref\": \"#/$defs/a\"}]}}, "
                + "\"$ref\": \"#/$defs/a\"}", CompileOptions.builder().maxDepth(4).build());
        assertFalse(loop.isValid("1"));
        assertThrows(SchemaEvaluationDepthException.class, () -> v.validate(JsonDocument.parse("1")));
        assertThrows(SchemaEvaluationDepthException.class,
                () -> v.evaluate(JsonDocument.parse("1"), JsonSchemaResultsCollector.create(ResultsLevel.BASIC)));
        assertThrows(IllegalArgumentException.class, () -> CompileOptions.builder().maxDepth(0));
    }

    @Test
    void optionsAndEntryPoints() {
        String schema = "{\"$defs\": {\"item\": {\"type\": \"string\", \"format\": \"date\"}}}";
        Validator item = Validator.compile(schema, CompileOptions.builder().entryPoint("#/$defs/item").build());
        assertTrue(item.isValid("\"not a date\""));
        Validator asserting = Validator.compile(schema,
                CompileOptions.builder().entryPoint("#/$defs/item").assertFormat(true).build());
        assertFalse(asserting.isValid("\"not a date\""));
        assertTrue(asserting.isValid("\"2020-02-29\""));
        assertThrows(SchemaCompilationException.class,
                () -> Validator.compile(schema, CompileOptions.builder().entryPoint("#/$defs/nope").build()));
        Validator draft7 = Validator.compile("{\"type\": \"object\", \"dependentRequired\": {\"a\": [\"b\"]}}",
                CompileOptions.builder().defaultDialect(Dialect.DRAFT7).build());
        assertTrue(draft7.isValid("{\"a\": 1}"));
        Validator legacy = Validator.compile("{\"format\": \"date\"}",
                CompileOptions.builder().defaultDialect(Dialect.DRAFT7).assertFormatInLegacyDrafts(true).build());
        assertFalse(legacy.isValid("\"x\""));
        Validator content = Validator.compile("{\"contentEncoding\": \"base64\"}",
                CompileOptions.builder().defaultDialect(Dialect.DRAFT7).assertContent(false).build());
        assertTrue(content.isValid("\"*\""));
    }

    @Test
    void remoteDocumentsAndCompileFromUri() {
        DocumentResolver resolver = uri -> uri.equals("https://example.com/item.json")
                ? JsonDocument.parse("{\"type\": \"integer\"}") : null;
        CompileOptions options = CompileOptions.builder().documentResolver(resolver)
                .baseUri("https://example.com/root.json").build();
        Validator v = Validator.compile("{\"items\": {\"$ref\": \"item.json\"}}", options);
        assertTrue(v.isValid("[1, 2]"));
        assertFalse(v.isValid("[1, \"x\"]"));
        Validator fromUri = Validator.compileFromUri("https://example.com/item.json", options);
        assertTrue(fromUri.isValid("3"));
        Validator meta = Validator.compileFromUri("https://json-schema.org/draft/2020-12/schema", CompileOptions.DEFAULT);
        assertTrue(meta.isValid(SCHEMA));
        assertFalse(meta.isValid("{\"type\": 3}"));
        assertThrows(SchemaCompilationException.class,
                () -> Validator.compileFromUri("https://example.com/missing.json", options));
        assertThrows(SchemaCompilationException.class, () -> Validator.compile("{\"$ref\": \"missing.json\"}", options));
        assertThrows(SchemaCompilationException.class, () -> Validator.compile("{\"pattern\": \"(\"}"));
        assertThrows(SchemaCompilationException.class, () -> Validator.compile("{\"patternProperties\": {\"[\": {}}}"));
    }

    @Test
    void patternsOfEcmaScript2025AndRefusedPatterns() {
        Validator v = Validator.compile("{\"pattern\": \"^(?i:[a-f]+)\\\\p{Dash}(?<n>x)\\\\k<\\\\u006e>(?<=^.*)$\"}");
        assertTrue(v.isValid("\"aBc-xx\""));
        assertTrue(v.isValid("\"F\u2014xx\""));
        assertFalse(v.isValid("\"g-xx\""));
        assertFalse(v.isValid("\"a-x\""));
        Validator names = Validator.compile(
                "{\"patternProperties\": {\"^(?:(?<k>a)|(?<k>b))\\\\k<k>$\": {\"type\": \"number\"}}}");
        assertTrue(names.isValid("{\"aa\": 1, \"bb\": 2, \"ab\": \"x\"}"));
        assertFalse(names.isValid("{\"bb\": \"x\"}"));
        // A valid pattern whose meaning java.util.regex cannot give is an error that says so. It is never run with
        // another meaning.
        SchemaCompilationException refused = assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"pattern\": \"(?<=\\\\1(a))b\"}"));
        assertEquals("Unsupported regular expression '(?<=\\1(a))b' in pattern. It is valid ECMA-262, but this library "
                + "cannot run a backreference inside a lookbehind with the meaning ECMA-262 gives it.", refused.getMessage());
        SchemaCompilationException invalid = assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"patternProperties\": {\"(?<a>x)(?<a>y)\": {}}}"));
        assertEquals("Invalid regular expression '(?<a>x)(?<a>y)' in patternProperties.", invalid.getMessage());
        // The regex format is about validity alone.
        Validator format = Validator.compile("{\"format\": \"regex\"}",
                CompileOptions.builder().assertFormat(true).build());
        assertTrue(format.isValid("\"(?<=\\\\1(a))b\""));
        assertTrue(format.isValid("\"(?i:a)\\\\p{scx=Arab}\""));
        assertFalse(format.isValid("\"(?i)a\""));
        assertFalse(format.isValid("\"\\\\p{Nope}\""));
    }

    @Test
    void documentsPrintAsJson() {
        String text = "{\"a\":[1,2.50,true,null,\"x\\n\\\"\\u0001\"],\"b\":{}}";
        assertEquals(text, JsonDocument.parse(text).toString());
    }
}
