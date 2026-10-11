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
    void documentsPrintAsJson() {
        String text = "{\"a\":[1,2.50,true,null,\"x\\n\\\"\\u0001\"],\"b\":{}}";
        assertEquals(text, JsonDocument.parse(text).toString());
    }
}
