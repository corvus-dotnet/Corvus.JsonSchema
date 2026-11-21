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

    /** A buffer of a framing prefix, the text, and the bytes of what follows it. */
    private static byte[] framed(String prefix, String text, String suffix) {
        return (prefix + text + suffix).getBytes(StandardCharsets.UTF_8);
    }

    private static int length(String text) {
        return text.getBytes(StandardCharsets.UTF_8).length;
    }

    @Test
    void aRangeOfAnArrayValidatesAsTheSameBytesAlone() {
        Validator v = Validator.compile(SCHEMA);
        String[] texts = {
            "{\"id\": 3}", "{\"id\": 3, \"name\": \"caf\u00e9 \ud83d\ude00\"}", " {\"id\": 3,\n\"name\": \"a\\nb\"} ",
            "{\"id\": 0}", "{}", "[]", "{\"id\": 1, \"name\": \"\"}", "{\"id\": 1, \"id\": 0}",
        };
        // What surrounds the range would change the answer, or fail to parse, if any of it were read.
        String[][] frames = {{"", ""}, {"\u0001\u0002len=", "{\"id\": 0}"}, {"{\"id\": 0}", "}]\"x"}, {"[\"", "\", 12"}};
        for (String t : texts) {
            boolean expected = v.isValid(t.getBytes(StandardCharsets.UTF_8));
            for (String[] f : frames) {
                byte[] buffer = framed(f[0], t, f[1]);
                assertEquals(expected, v.isValid(buffer, length(f[0]), length(t)), f[0] + "|" + t + "|" + f[1]);
            }
        }
        // Several records in one buffer, each validated where it is.
        String[] records = {"{\"id\": 1}", "{\"id\": 0}", "{\"id\": 22, \"name\": \"x\"}", "7"};
        boolean[] answers = {true, false, true, false};
        byte[] buffer = String.join("", records).getBytes(StandardCharsets.UTF_8);
        for (int pass = 0; pass < 2; pass++) {
            int at = 0;
            for (int r = 0; r < records.length; r++) {
                assertEquals(answers[r], v.isValid(buffer, at, records[r].length()), records[r]);
                at += records[r].length();
            }
        }
    }

    @Test
    void aRangeEndsWhereItSaysItDoes() {
        // A number, a literal and a string that the bytes after the range would continue.
        Validator max = Validator.compile("{\"maximum\": 12}");
        assertTrue(max.isValid(framed("9", "12", "345"), 1, 2));
        assertFalse(max.isValid(framed("9", "123", "45"), 1, 3));
        assertTrue(max.isValid(framed("", "1.5", "e9"), 0, 3));
        assertTrue(max.isValid(framed("-", "1", ".5"), 1, 1));
        // The number's text decides (more digits than a double holds): it must stop at the end of the range.
        Validator exact = Validator.compile("{\"const\": 0.123456789123456789}");
        assertTrue(exact.isValid(framed("1", "0.123456789123456789", "9"), 1, 20));
        assertFalse(exact.isValid(framed("1", "0.1234567891234567899", ""), 1, 21));
        Validator multiple = Validator.compile("{\"multipleOf\": 0.01}");
        assertTrue(multiple.isValid(framed("", "12.34", "5"), 0, 5));
        assertTrue(Validator.compile("{\"const\": \"ab\"}").isValid(framed("\"", "\"ab\"", "c\""), 1, 4));
        Validator any = Validator.compile("true");
        assertTrue(any.isValid(framed("", "true", "x"), 0, 4));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "tru", "e"), 0, 3));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "\"ab", "\""), 0, 3));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "[1", "]"), 0, 2));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "1.", "5"), 0, 2));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "1e", "5"), 0, 2));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "-", "5"), 0, 1));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "\"\\u00", "41\""), 0, 5));
        assertThrows(JsonParseException.class, () -> any.isValid(framed("", "\"\\", "n\""), 0, 2));
        // An empty range, and one of white space, hold no value.
        assertEquals(0, assertThrows(JsonParseException.class, () -> any.isValid(framed("1", "", "2"), 1, 0)).offset());
        assertThrows(JsonParseException.class, () -> any.isValid(framed("1", "  ", "2"), 1, 2));
    }

    @Test
    void aRangeIsAsStrictAsAnArray() {
        Validator any = Validator.compile("true");
        // Content after the value, inside the range, is an error at its offset from the start of the range.
        JsonParseException trailing = assertThrows(
                JsonParseException.class, () -> any.isValid(framed("####", "{\"id\": 1} {\"id\": 2}", ""), 4, 19));
        assertEquals(10, trailing.offset());
        assertEquals(7, assertThrows(JsonParseException.class, () -> any.isValid(framed("ab", "{\"id\": }", "1}"), 2, 8))
                .offset());
        assertEquals(7, assertThrows(JsonParseException.class, () -> any.isValid("{\"id\": }".getBytes(StandardCharsets.UTF_8)))
                .offset());
        // Bytes that are not UTF-8 are refused, in a string that has an escape and in one that has none.
        byte[][] illFormed = {
            {'"', (byte) 0xff, '"'}, {'"', (byte) 0xc3, '"'}, {'"', (byte) 0xed, (byte) 0xa0, (byte) 0x80, '"'},
            {'"', (byte) 0xc0, (byte) 0xaf, '"'}, {'"', '\\', 'n', (byte) 0xe2, (byte) 0x82, '"'},
        };
        for (byte[] text : illFormed) {
            byte[] buffer = new byte[text.length + 5];
            System.arraycopy(text, 0, buffer, 2, text.length);
            assertThrows(JsonParseException.class, () -> any.isValid(text));
            assertThrows(JsonParseException.class, () -> any.isValid(buffer, 2, text.length));
        }
        // A character cut by the end of the range: its last byte is outside.
        byte[] cut = framed("", "\"\u00e9\"", "");
        assertThrows(JsonParseException.class, () -> any.isValid(new byte[] {cut[0], cut[1], '"', cut[2]}, 0, 3));
        // The validator is usable after each error.
        assertTrue(any.isValid(framed("x", "1", "x"), 1, 1));
    }

    @Test
    void aRangeOutsideTheArrayThrows() {
        Validator v = Validator.compile("true");
        byte[] buffer = framed("", "[1]", "");
        assertTrue(v.isValid(buffer, 0, 3));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, -1, 2));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, 0, 4));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, 2, 2));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, 4, 0));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, 1, -1));
        assertThrows(IndexOutOfBoundsException.class, () -> v.isValid(buffer, Integer.MAX_VALUE, 2));
        assertThrows(NullPointerException.class, () -> v.isValid(null, 0, 0));
        // A range that ends at the end of the array, and an empty one there.
        assertTrue(v.isValid(buffer, 1, 1));
        assertThrows(JsonParseException.class, () -> v.isValid(buffer, 3, 0));
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

    /**
     * A not whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops
     * at the maximum depth: validate() and evaluate() throw, and isValid() reports the instance as invalid.
     * (Evaluating not went around the depth guard, so the first of these schemas overflowed the stack, and for the
     * others the not turned the abandoned evaluation's false into true.)
     */
    @Test
    void notOnAnInPlaceCycleStopsAtTheMaximumDepth() {
        String looping = "{\"allOf\": [{\"$ref\": \"#/$defs/loop\"}]}";
        String[] schemas = {
            "{\"not\": {\"$ref\": \"#\"}}",
            "{\"not\": {\"not\": {\"$ref\": \"#\"}}}",
            "{\"type\": \"integer\", \"not\": {\"$ref\": \"#\"}}",
            "{\"allOf\": [{\"not\": {\"$ref\": \"#\"}}]}",
            "{\"$defs\": {\"a\": {\"not\": {\"$ref\": \"#/$defs/b\"}}, \"b\": {\"not\": {\"$ref\": \"#/$defs/a\"}}}, "
                    + "\"$ref\": \"#/$defs/a\"}",
            "{\"$defs\": {\"loop\": " + looping + "}, \"not\": {\"$ref\": \"#/$defs/loop\"}}",
            "{\"$defs\": {\"loop\": " + looping + "}, \"not\": {\"not\": {\"$ref\": \"#/$defs/loop\"}}}",
            "{\"$defs\": {\"loop\": " + looping + "}, \"properties\": {\"a\": {\"not\": {\"$ref\": \"#/$defs/loop\"}}}}",
            "{\"unevaluatedProperties\": false, \"not\": {\"$ref\": \"#\"}}",
        };
        String[] instances = {"1", "\"a\"", "{\"a\": 1}", "[1]"};
        for (int s = 0; s < schemas.length; s++) {
            Validator v = Validator.compile(schemas[s], CompileOptions.builder().maxDepth(16).build());
            for (String instance : instances) {
                // Only an object with the property reaches the loop of the eighth schema, and anything but an
                // integer fails the type of the third before its not is reached, when failing fast.
                if (s == 7 && !instance.startsWith("{") || s == 2 && !instance.equals("1")) {
                    continue;
                }
                String what = schemas[s] + " with " + instance;
                JsonDocument document = JsonDocument.parse(instance);
                assertFalse(v.isValid(instance), what);
                assertFalse(v.isValid(document), what);
                assertThrows(SchemaEvaluationDepthException.class, () -> v.validate(document), what);
                for (ResultsLevel level : ResultsLevel.values()) {
                    assertThrows(SchemaEvaluationDepthException.class,
                            () -> v.evaluate(document, JsonSchemaResultsCollector.create(level)), what);
                }
            }
        }
    }

    @Test
    void recursionBeyondTheMaximumDepth() {
        // anyOf would recover through its true branch, but an evaluation that went beyond the maximum depth is not
        // valid, whatever it came to: isValid() says so, and validate() reports the depth.
        Validator v = Validator.compile("{\"$defs\": {\"a\": {\"anyOf\": [{\"$ref\": \"#/$defs/a\"}, true]}}, "
                + "\"$ref\": \"#/$defs/a\"}", CompileOptions.builder().maxDepth(4).build());
        assertFalse(v.isValid("1"));
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
        // A backreference means what ECMA-262 says where java.util.regex differs: in a repetition whose iterations
        // unset its groups, in a lookbehind, which runs backwards, and in a case-insensitive group.
        Validator repeated = Validator.compile("{\"pattern\": \"^(?:(a)|x)*\\\\1$\"}");
        assertTrue(repeated.isValid("\"ax\""));
        assertTrue(repeated.isValid("\"xaa\""));
        assertFalse(repeated.isValid("\"axa\""));
        Validator behind = Validator.compile("{\"pattern\": \"(?<=\\\\1(a))b\"}");
        assertTrue(behind.isValid("\"aab\""));
        assertFalse(behind.isValid("\"ab\""));
        Validator folded = Validator.compile("{\"pattern\": \"^(?i:(.+)-\\\\1)$\"}");
        assertTrue(folded.isValid("\"Stra\u00dfe-STRA\u1e9eE\""));
        assertFalse(folded.isValid("\"i-\u0130\""));
        Validator foldedBehind = Validator.compile("{\"pattern\": \"(?i:^(b*)(?<=\\\\1)c$)\"}");
        assertTrue(foldedBehind.isValid("\"bBc\""));
        assertFalse(foldedBehind.isValid("\"abc\""));
        // A lookahead takes the first way its body matches, and its groups hold what that way set.
        Validator ahead = Validator.compile("{\"pattern\": \"^(?=(?:b|bc)*(c)?)b\\\\1$\"}");
        assertTrue(ahead.isValid("\"bc\""));
        assertFalse(ahead.isValid("\"bb\""));
        Validator emptyFirst = Validator.compile("{\"pattern\": \"^(?=((?:c??)?))\\\\1$\"}");
        assertTrue(emptyFirst.isValid("\"c\""));
        // A group of a lookaround is read as the lookaround of the same iteration set it.
        Validator looped = Validator.compile("{\"pattern\": \"^(?:(?=([ab]))[ab](?:c|ca)\\\\1)*$\"}");
        assertTrue(looped.isValid("\"acaa\""));
        assertFalse(looped.isValid("\"acab\""));
        Validator counted = Validator.compile("{\"pattern\": \"(?<=(?:(a|b)(?:c|b){2})+)\\\\1\"}");
        assertTrue(counted.isValid("\"abca\""));
        assertFalse(counted.isValid("\"abcb\""));
        // A valid pattern whose meaning java.util.regex cannot give is an error that says so. It is never run with
        // another meaning.
        SchemaCompilationException refused = assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"pattern\": \"(?<=(x*?)a*)\\\\1\"}"));
        assertEquals("Unsupported regular expression '(?<=(x*?)a*)\\1' in pattern. It is valid ECMA-262, but this "
                + "library cannot run a backreference to a group inside a lookbehind whose text depends on the "
                + "lookbehind being matched backwards with the meaning ECMA-262 gives it.", refused.getMessage());
        SchemaCompilationException invalid = assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"patternProperties\": {\"(?<a>x)(?<a>y)\": {}}}"));
        assertEquals("Invalid regular expression '(?<a>x)(?<a>y)' in patternProperties.", invalid.getMessage());
        // The regex format is about validity alone.
        Validator format = Validator.compile("{\"format\": \"regex\"}",
                CompileOptions.builder().assertFormat(true).build());
        assertTrue(format.isValid("\"(?<=(x*?)a*)\\\\1\""));
        assertTrue(format.isValid("\"(?i:a)\\\\p{scx=Arab}\""));
        assertFalse(format.isValid("\"(?i)a\""));
        assertFalse(format.isValid("\"\\\\p{Nope}\""));
    }

    /**
     * A pattern and a string whose match takes time that doubles with each "a": far beyond any limit. (Without the
     * backreference, java.util.regex remembers where the repetition failed and answers at once.)
     */
    static final String RUNAWAY = "^(?:(a+)\\1*)+$";
    static final String RUNAWAY_JSON = "\"^(?:(a+)\\\\1*)+$\"";
    static final String RUNAWAY_TEXT = "\"" + "a".repeat(60) + "b\"";

    /** Runs a validation that must go over the limit on a match, and returns the error and the seconds it took. */
    private static PatternMatchLimitException overTheLimit(Runnable validation, double[] seconds) {
        long start = System.nanoTime();
        PatternMatchLimitException e = assertThrows(PatternMatchLimitException.class, validation::run);
        seconds[0] = (System.nanoTime() - start) / 1e9;
        return e;
    }

    @Test
    void aMatchIsLimitedToOneSecondByDefault() {
        assertEquals(java.time.Duration.ofSeconds(1), CompileOptions.DEFAULT_PATTERN_MATCH_LIMIT);
        Validator pattern = Validator.compile("{\"pattern\": " + RUNAWAY_JSON + "}");
        Validator properties = Validator.compile(
                "{\"patternProperties\": {" + RUNAWAY_JSON + ": {\"type\": \"integer\"}}, \"additionalProperties\": false}");
        String object = "{" + RUNAWAY_TEXT + ": 1}";
        double[] seconds = new double[1];
        // Every way to validate reports the limit as an error. None of them answers. The bound on the time is
        // generous, as the machine may be busy: the match itself would take centuries.
        Runnable[] ways = {
            () -> pattern.isValid(RUNAWAY_TEXT),
            () -> pattern.isValid(RUNAWAY_TEXT.getBytes(StandardCharsets.UTF_8)),
            () -> pattern.isValid(JsonDocument.parse(RUNAWAY_TEXT)),
            () -> pattern.validate(JsonDocument.parse(RUNAWAY_TEXT)),
            () -> pattern.evaluate(JsonDocument.parse(RUNAWAY_TEXT), JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE)),
            () -> properties.isValid(object),
            () -> properties.validate(JsonDocument.parse(object)),
            () -> properties.evaluate(JsonDocument.parse(object), JsonSchemaResultsCollector.create(ResultsLevel.BASIC)),
        };
        for (Runnable way : ways) {
            PatternMatchLimitException e = overTheLimit(way, seconds);
            assertEquals(RUNAWAY, e.pattern());
            assertEquals(java.time.Duration.ofSeconds(1), e.limit());
            assertFalse(e.isStackLimit());
            assertTrue(e.getMessage().contains("ran for longer than the limit of PT1S"), e.getMessage());
            assertTrue(seconds[0] >= 0.9 && seconds[0] < 20, "took " + seconds[0] + " s");
        }
        // The validator is as it was: the next instance is answered.
        assertTrue(pattern.isValid("\"aaa\""));
        assertFalse(pattern.isValid("\"aab\""));
        assertTrue(properties.isValid("{\"aaa\": 1}"));
        assertFalse(properties.isValid("{\"aab\": 1}"));
    }

    @Test
    void theLimitOnAMatchCanBeSetAndTurnedOff() {
        String schema = "{\"pattern\": " + RUNAWAY_JSON + "}";
        double[] seconds = new double[1];
        Validator quick = Validator.compile(schema,
                CompileOptions.builder().patternMatchLimit(java.time.Duration.ofMillis(50)).build());
        PatternMatchLimitException e = overTheLimit(() -> quick.isValid(RUNAWAY_TEXT), seconds);
        assertEquals(java.time.Duration.ofMillis(50), e.limit());
        assertTrue(seconds[0] >= 0.04 && seconds[0] < 10, "took " + seconds[0] + " s");
        // A match of some hundreds of thousands of steps is over a limit of a millisecond, and is answered with no
        // limit.
        String long22 = "\"" + "a".repeat(18) + "b\"";
        Validator tiny = Validator.compile(schema,
                CompileOptions.builder().patternMatchLimit(java.time.Duration.ofMillis(1)).build());
        assertThrows(PatternMatchLimitException.class, () -> tiny.isValid(long22));
        Validator unlimited = Validator.compile(schema, CompileOptions.builder().noPatternMatchLimit().build());
        assertFalse(unlimited.isValid(long22));
        assertTrue(unlimited.isValid("\"" + "a".repeat(18) + "\""));
        // The limit is more than zero.
        assertThrows(IllegalArgumentException.class,
                () -> CompileOptions.builder().patternMatchLimit(java.time.Duration.ZERO));
        assertThrows(IllegalArgumentException.class,
                () -> CompileOptions.builder().patternMatchLimit(java.time.Duration.ofMillis(-1)));
        assertThrows(NullPointerException.class, () -> CompileOptions.builder().patternMatchLimit(null));
    }

    @Test
    void anOrdinaryMatchIsNotLimited() {
        // The clock is not read for a match that reads the string a few thousand times, whatever the limit is.
        CompileOptions shortest = CompileOptions.builder().patternMatchLimit(java.time.Duration.ofNanos(1)).build();
        Validator v = Validator.compile(
                "{\"pattern\": \"^[a-z]+(?:-[a-z0-9]+)*@(?:x|y)+$\", \"patternProperties\": {\"^(?:ab|cd)+e$\": true},"
                        + " \"additionalProperties\": false}", shortest);
        assertTrue(v.isValid("\"abc-d1-e2@xyx\""));
        assertFalse(v.isValid("\"abc-d1-e2@xyz\""));
        assertTrue(v.isValid("{\"abcde\": 1}"));
        assertFalse(v.isValid("{\"abcdf\": 1}"));
        // A long string against a pattern that reads it once is answered within the default limit.
        Validator words = Validator.compile("{\"pattern\": \"^(?:[a-z]+ ?\\\\d*,)*$\"}");
        assertTrue(words.isValid("\"" + "abc 12,".repeat(100_000) + "\""));
        assertFalse(words.isValid("\"" + "abc 12,".repeat(100_000) + "!\""));
    }

    @Test
    void aMatchThatReadsNoCharacterIsLimitedToo() {
        // java.util.regex can try a choice between two ways to match nothing without reading the string, which is
        // what the limit counts. Sixty such choices are 2^60 ways to fail at the end of "y".
        String schema = "{\"pattern\": \"" + "(?:|)".repeat(60) + "^(?<=y)\"}";
        Validator v = Validator.compile(schema,
                CompileOptions.builder().patternMatchLimit(java.time.Duration.ofMillis(100)).build());
        double[] seconds = new double[1];
        overTheLimit(() -> v.isValid("\"y\""), seconds);
        assertTrue(seconds[0] < 10, "took " + seconds[0] + " s");
        // So is a counted repetition of a backreference to a group that holds nothing.
        Validator counted = Validator.compile("{\"pattern\": \"()(?:(?:(?:\\\\1){1000}){1000}){1000}^(?<=y)\"}",
                CompileOptions.builder().patternMatchLimit(java.time.Duration.ofMillis(100)).build());
        overTheLimit(() -> counted.isValid("\"y\""), seconds);
        assertTrue(seconds[0] < 10, "took " + seconds[0] + " s");
        // A repetition of what takes no character is run once, whatever its count.
        Validator anchors = Validator.compile("{\"pattern\": \"(?:(?:(?:^|$){2147483646}){2147483646})*a\"}");
        assertTrue(anchors.isValid("\"a\""));
        assertFalse(anchors.isValid("\"b\""));
    }

    /** Runs a task on a thread with a small stack, and returns what it threw. */
    private static Throwable onASmallStack(Runnable task) throws InterruptedException {
        AtomicReference<Throwable> thrown = new AtomicReference<>();
        Thread thread = new Thread(null, () -> {
            try {
                task.run();
            } catch (Throwable t) {
                thrown.set(t);
            }
        }, "small stack", 256 * 1024);
        thread.start();
        thread.join();
        return thrown.get();
    }

    @Test
    void aMatchThatRunsOutOfStackIsTheLimitError() throws InterruptedException {
        // java.util.regex matches a repetition of a group with alternatives of different lengths by recursion. A
        // string of 200,000 characters is more than a stack of 256 KB holds. The outcome is the error of the limit
        // and not a StackOverflowError, and the validator answers the next instance.
        Validator v = Validator.compile("{\"pattern\": \"^(?:ab|ba|a|b)*$\"}");
        String text = "\"" + "ab".repeat(100_000) + "\"";
        Throwable thrown = onASmallStack(() -> v.isValid(text));
        assertTrue(thrown instanceof PatternMatchLimitException, String.valueOf(thrown));
        PatternMatchLimitException e = (PatternMatchLimitException) thrown;
        assertTrue(e.isStackLimit());
        assertEquals("^(?:ab|ba|a|b)*$", e.pattern());
        assertTrue(e.getMessage().contains("needed more of the thread's stack"), e.getMessage());
        Throwable collecting = onASmallStack(() -> v.evaluate(JsonDocument.parse(text),
                JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE)));
        assertTrue(collecting instanceof PatternMatchLimitException, String.valueOf(collecting));
        assertTrue(v.isValid("\"abba\""));
        assertFalse(v.isValid("\"abca\""));
        // The common forms are written so that they run in a loop, and are answered on the same stack.
        String[][] looped = {
            {"^(a|b)*$", "ab".repeat(100_000), "ab".repeat(100_000) + "c"},
            {"^(?:.|\\\\n)*x$", "ab\\n".repeat(50_000) + "x", "ab\\n".repeat(50_000)},
            {"^'(?:[^'\\\\\\\\]|\\\\\\\\.)*'$", "'" + "ab\\\\n".repeat(50_000) + "'", "'" + "ab\\\\n".repeat(50_000) + "'x"},
            {"^[a-z]+(?:\\\\.[a-z]+)*@[a-z]+$", "a.".repeat(100_000) + "a@b", "a.".repeat(100_000) + "@b"},
            {"^(?:[a-z]+,)*$", "ab,".repeat(70_000), "ab,".repeat(70_000) + "a"},
        };
        for (String[] c : looped) {
            Validator w = Validator.compile("{\"pattern\": \"" + c[0] + "\"}");
            Object[] answers = new Object[2];
            Throwable t = onASmallStack(() -> {
                answers[0] = w.isValid("\"" + c[1] + "\"");
                answers[1] = w.isValid("\"" + c[2] + "\"");
            });
            assertEquals(null, t, c[0]);
            assertEquals(true, answers[0], c[0]);
            assertEquals(false, answers[1], c[0]);
        }
    }

    @Test
    void aPatternOverALimitOfSizeIsRefusedAsTooLarge() {
        // Four hundred classes of every letter are valid, and are over a million characters as java.util.regex is
        // given them.
        String classes = "\\\\p{L}".repeat(400);
        long start = System.nanoTime();
        PatternTooLargeException large = assertThrows(PatternTooLargeException.class,
                () -> Validator.compile("{\"pattern\": \"" + classes + "\"}"));
        assertTrue((System.nanoTime() - start) / 1e9 < 20);
        assertEquals("\\p{L}".repeat(400), large.pattern());
        assertEquals("The regular expression '" + "\\p{L}".repeat(24) + "...' (of 2000 characters) in pattern is over a "
                + "limit of size. It is a valid ECMA-262 regular expression, and is too large: what is written for "
                + "java.util.regex to run it is more than 1,048,576 characters.", large.getMessage());
        // It is an error of compilation, in patternProperties too, and not the error of a pattern that is not valid.
        assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"patternProperties\": {\"" + classes + "\": {}}}"));
        SchemaCompilationException invalid = assertThrows(SchemaCompilationException.class,
                () -> Validator.compile("{\"pattern\": \"" + classes + "(\"}"));
        assertFalse(invalid instanceof PatternTooLargeException);
        // A pattern of more than 1,048,576 characters, or with groups more than 200 deep, is not read.
        PatternTooLargeException longer = assertThrows(PatternTooLargeException.class,
                () -> Validator.compile("{\"pattern\": \"" + "a".repeat((1 << 20) + 1) + "\"}"));
        assertTrue(longer.getMessage().endsWith("is over a limit of size. It is too large to be read: it has more "
                + "than 1,048,576 characters. Whether it is valid is not known."), longer.getMessage());
        PatternTooLargeException deep = assertThrows(PatternTooLargeException.class,
                () -> Validator.compile("{\"pattern\": \"" + "(?:".repeat(201) + "a" + ")".repeat(201) + "\"}"));
        assertTrue(deep.getMessage().endsWith("It is too large to be read: its groups are nested more than 200 deep. "
                + "Whether it is valid is not known."), deep.getMessage());
        // What is under the limits compiles, and matches.
        Validator most = Validator.compile("{\"pattern\": \"^" + "\\\\p{L}".repeat(300) + "$\"}");
        assertTrue(most.isValid("\"" + "a".repeat(300) + "\""));
        assertFalse(most.isValid("\"" + "a".repeat(299) + "1\""));
        Validator nested = Validator.compile("{\"pattern\": \"^" + "(?:".repeat(199) + "a" + ")".repeat(199) + "$\"}");
        assertTrue(nested.isValid("\"a\""));
    }

    @Test
    void documentsPrintAsJson() {
        String text = "{\"a\":[1,2.50,true,null,\"x\\n\\\"\\u0001\"],\"b\":{}}";
        assertEquals(text, JsonDocument.parse(text).toString());
    }
}
