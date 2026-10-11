package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.lang.ref.WeakReference;
import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;

/**
 * What a process keeps of the patterns it has compiled. A pattern comes from a schema, which may come from outside,
 * and the library kept every pattern it was ever given, and gave each a number of the process that an evaluator's
 * array of matchers was indexed by. So a process that compiles schemas for its callers grew for as long as it ran,
 * and the evaluator of a schema with one pattern held an array as long as the count of patterns of the process.
 */
class PatternRetentionTest {
    /** How many patterns the cache of the process may hold (the bound of the Go module and the Rust crate). */
    private static final int BOUND = 1024;

    /** Runs on a thread of its own, which ends: what the thread kept for the validators it used goes with it. */
    private static void onAThread(Runnable work) throws InterruptedException {
        AtomicReference<Throwable> failure = new AtomicReference<>();
        Thread thread = new Thread(() -> {
            try {
                work.run();
            } catch (Throwable e) {
                failure.set(e);
            }
        });
        thread.start();
        thread.join();
        if (failure.get() != null) {
            throw new AssertionError(failure.get());
        }
    }

    /** How many of the references still hold their object, after collections. */
    static <T> int live(List<WeakReference<T>> references) throws InterruptedException {
        int live = Integer.MAX_VALUE;
        for (int pass = 0; pass < 20; pass++) {
            System.gc();
            Thread.sleep(20);
            int now = 0;
            for (WeakReference<T> r : references) {
                now += r.get() != null ? 1 : 0;
            }
            if (now == live && pass >= 2) {
                break;
            }
            live = now;
        }
        return live;
    }

    /** The array of matchers of the evaluator a validator uses on this thread. */
    private static Object[] matchers(Validator v) throws ReflectiveOperationException {
        Field evaluators = Validator.class.getDeclaredField("evaluators");
        evaluators.setAccessible(true);
        Object evaluator = ((ThreadLocal<?>) evaluators.get(v)).get();
        Field matchers = Evaluator.class.getDeclaredField("matchers");
        matchers.setAccessible(true);
        return (Object[]) matchers.get(evaluator);
    }

    /**
     * Three times as many validators as the cache holds patterns, each with a pattern of its own that the engine
     * runs, each used once and dropped. The validators are collected, with their compiled code, and the process
     * keeps no more compiled patterns than the cache may hold.
     */
    @Test
    void theProcessKeepsNoMorePatternsOfDroppedValidatorsThanTheCacheHolds() throws Exception {
        int count = 3 * BOUND;
        List<WeakReference<java.util.regex.Pattern>> patterns = new ArrayList<>();
        List<WeakReference<Validator>> validators = new ArrayList<>();
        List<WeakReference<Object>> code = new ArrayList<>();
        onAThread(() -> {
            for (int n = 0; n < count; n++) {
                String source = "^(?=dropped-" + n + "$)[a-z0-9-]+$";
                // The validator compiled next has this very pattern: it is the one compiled last.
                patterns.add(new WeakReference<>(SchemaPattern.compile(source).regex));
                Validator v = Validator.compile("{\"pattern\": \"" + source + "\"}");
                assertTrue(v.isValid("\"dropped-" + n + "\""), source);
                assertFalse(v.isValid("\"dropped-" + (n + 1) + "\""), source);
                validators.add(new WeakReference<>(v));
                if (v.code() != null) {
                    code.add(new WeakReference<>(v.code()));
                }
            }
        });
        assertEquals(0, live(validators), "validators that nothing holds are still on the heap");
        assertEquals(0, live(code), "the compiled code of validators that nothing holds is still on the heap");
        int kept = live(patterns);
        assertTrue(kept <= BOUND, kept + " of " + count + " compiled patterns are still on the heap, with a bound of "
                + BOUND + " on the cache");
    }

    /**
     * An evaluator's array of matchers is as long as its own schema has patterns that the engine runs, however many
     * patterns the process compiled before the schema.
     */
    @Test
    void anEvaluatorHoldsMatchersForThePatternsOfItsOwnSchemaOnly() throws Exception {
        for (int n = 0; n < 2000; n++) {
            assertTrue(Validator.compile("{\"pattern\": \"^(?=others-" + n + ")[a-z0-9-]+$\"}").isValid("\"others-" + n + "\""));
        }
        // Three patterns that the engine runs (one of them twice, one for a property name), and three that it
        // does not: a list, a prefix and one that every string matches.
        Validator v = Validator.compile("""
                {"properties": {"a": {"pattern": "^(?=own)[a-z]+$"}, "b": {"pattern": "^(a)\\\\1$"},
                 "c": {"pattern": "^(?=own)[a-z]+$"}, "d": {"pattern": "^[a-z]+(,[a-z]+)*$"}, "e": {"pattern": ".*"}},
                 "patternProperties": {"^x-": {"type": "string"}, "^(?!y)[0-9]{3}$": {"type": "number"}}}""");
        assertTrue(v.isValid("{\"a\": \"own\", \"b\": \"aa\", \"c\": \"owner\", \"d\": \"a,b\", \"e\": 1, \"x-a\": \"s\", \"123\": 1}"));
        assertFalse(v.isValid("{\"a\": \"mine\"}"));
        assertFalse(v.isValid("{\"b\": \"ab\"}"));
        assertFalse(v.isValid("{\"c\": \"mine\"}"));
        assertFalse(v.isValid("{\"123\": \"s\"}"));
        Object[] matchers = matchers(v);
        assertEquals(3, matchers.length, "the evaluator of a schema with three patterns for the engine holds "
                + matchers.length + " places for matchers");
        for (Object m : matchers) {
            assertTrue(m != null, "a place for a matcher was not used");
        }
        // A schema with no pattern for the engine has no place for one.
        Validator none = Validator.compile("{\"pattern\": \"^[a-z]+$\", \"type\": \"string\"}");
        assertTrue(none.isValid("\"abc\""));
        assertEquals(0, matchers(none).length);
    }

    /**
     * Two validators with the same pattern each keep it when it has left the cache, and each has its matchers at the
     * places of its own schema.
     */
    @Test
    void aPatternThatLeftTheCacheIsStillMatchedByTheValidatorsThatHaveIt() throws Exception {
        String shared = "^(?=kept-)[a-z0-9-]+(?<!-)$";
        Validator one = Validator.compile("{\"pattern\": \"" + shared + "\"}");
        Validator two = Validator.compile(
                "{\"properties\": {\"p\": {\"pattern\": \"^(b)\\\\1$\"}, \"q\": {\"pattern\": \"" + shared + "\"}}}");
        assertTrue(one.isValid("\"kept-1\""));
        assertTrue(two.isValid("{\"p\": \"bb\", \"q\": \"kept-2\"}"));
        WeakReference<java.util.regex.Pattern> first = new WeakReference<>(SchemaPattern.compile(shared).regex);
        List<WeakReference<java.util.regex.Pattern>> others = new ArrayList<>();
        onAThread(() -> {
            for (int n = 0; n < 2 * BOUND; n++) {
                String source = "^(?=flood-" + n + ")[a-z0-9-]+$";
                others.add(new WeakReference<>(SchemaPattern.compile(source).regex));
                assertTrue(Validator.compile("{\"pattern\": \"" + source + "\"}").isValid("\"flood-" + n + "\""));
            }
        });
        // The flood pushed every pattern that came before it out of a cache that is bounded.
        assertTrue(live(others) <= BOUND, "the cache kept more than its bound");
        assertTrue(first.get() != null, "a pattern two validators hold was collected");
        assertTrue(one.isValid("\"kept-3\""));
        assertFalse(one.isValid("\"kept-\""));
        assertFalse(one.isValid("\"lost-3\""));
        assertTrue(two.isValid("{\"p\": \"bb\", \"q\": \"kept-4\"}"));
        assertFalse(two.isValid("{\"p\": \"bb\", \"q\": \"kept-4-\"}"));
        assertFalse(two.isValid("{\"p\": \"ba\", \"q\": \"kept-4\"}"));
        assertEquals(1, matchers(one).length);
        assertEquals(2, matchers(two).length);
        // A schema compiled now compiles the pattern again, and answers the same.
        Validator three = Validator.compile("{\"pattern\": \"" + shared + "\"}");
        assertTrue(three.isValid("\"kept-5\""));
        assertFalse(three.isValid("\"kept-\""));
    }
}
