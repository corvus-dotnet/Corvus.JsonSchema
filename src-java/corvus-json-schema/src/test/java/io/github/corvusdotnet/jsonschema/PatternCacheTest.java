package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.lang.ref.WeakReference;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ConcurrentLinkedQueue;
import java.util.function.IntFunction;
import org.junit.jupiter.api.Test;

/**
 * The cache of compiled patterns of the process: bounded in the number of patterns and in the bytes counted for
 * them, oldest out first, with the bounds of the Go module and the Rust crate. {@link PatternRetentionTest} has what
 * the bounds are for.
 */
class PatternCacheTest {
    /** Checks what the cache says of itself against its bounds and against what it holds. */
    private static long[] checkCache() {
        long[] f = SchemaPattern.cacheFigures();
        assertTrue(f[0] <= SchemaPattern.MAX_CACHED_PATTERNS && f[1] <= SchemaPattern.MAX_CACHED_BYTES,
                "the cache holds " + f[0] + " patterns counted as " + f[1] + " bytes, over its bounds");
        assertEquals(f[2], f[1], "the cache counts other than what its patterns come to");
        return f;
    }

    private static String shape(int n) {
        switch (n % 4) {
            case 0:
                return "^cache-bound-" + n + "(?=x)x$";
            case 1:
                return "^cache-bound-" + n + "[a-z]{1,4}$";
            case 2:
                return "^cache-bound-" + n + "(?:a|bc)*c$";
            default:
                return "^(cache-bound-" + n + "|none|auto)$";
        }
    }

    private static String text(int n, int shape) {
        return "cache-bound-" + n + new String[] {"x", "abc", "abcac", ""}[shape];
    }

    @Test
    void theBoundsAreThoseOfTheOtherPorts() {
        assertEquals(1024, SchemaPattern.MAX_CACHED_PATTERNS);
        assertEquals(64L << 20, SchemaPattern.MAX_CACHED_BYTES);
        assertEquals(2L << 20, SchemaPattern.MAX_CACHED_PATTERN_BYTES);
    }

    /**
     * Three times as many patterns as the cache holds, of each kind of matcher. The cache holds no more than its
     * bound, the patterns compiled first have left it, and a pattern that has left it still answers for whoever
     * holds it, and compiles again to a pattern of its own.
     */
    @Test
    void theCacheIsBoundedInNumber() {
        SchemaPattern first = SchemaPattern.compile("^cache-bound-0+$");
        List<SchemaPattern> held = new ArrayList<>();
        for (int n = 0; n < 3 * SchemaPattern.MAX_CACHED_PATTERNS; n++) {
            held.add(SchemaPattern.compile(shape(n)));
        }
        assertEquals(SchemaPattern.MAX_CACHED_PATTERNS, checkCache()[0]);
        assertNull(SchemaPattern.cached("^cache-bound-0+$"), "the pattern compiled first is still in the cache");
        assertNull(SchemaPattern.cached(held.get(0).source), "a pattern compiled early is still in the cache");
        // Oldest out first: exactly the patterns compiled last are in it.
        for (int n = 0; n < held.size(); n++) {
            boolean in = n >= held.size() - SchemaPattern.MAX_CACHED_PATTERNS;
            assertSame(in ? held.get(n) : null, SchemaPattern.cached(held.get(n).source), "pattern " + n);
        }
        assertTrue(first.find("cache-bound-000") && !first.find("cache-bound-1"));
        for (int n = 0; n < held.size(); n++) {
            SchemaPattern p = held.get(n);
            assertTrue(p.find(text(n, n % 4)) && !p.find(text(n + 1, n % 4)), p.source);
        }
        SchemaPattern again = SchemaPattern.compile("^cache-bound-0+$");
        assertNotSame(first, again);
        assertTrue(again.find("cache-bound-0") && !again.find("cache-bound-"));
        assertSame(again, SchemaPattern.compile("^cache-bound-0+$"), "a pattern in the cache was compiled again");
        checkCache();
    }

    /**
     * Large patterns leave the cache by what they are counted as, well before their number is the bound, and a
     * pattern counted as more than 2 MiB is not kept at all. Each of the 300 patterns is counted as 1 MiB or so, so
     * the cache holds some sixty of them, and the process keeps no more of them than that.
     */
    @Test
    void theCacheIsBoundedInSize() throws Exception {
        List<WeakReference<java.util.regex.Pattern>> compiled = new ArrayList<>();
        for (int n = 0; n < 300; n++) {
            SchemaPattern p = SchemaPattern.compile("^(?=large-" + n + ")" + "[a]".repeat(1000) + "$");
            assertTrue(p.counted > 512 << 10 && p.counted <= SchemaPattern.MAX_CACHED_PATTERN_BYTES,
                    "a large pattern is counted as " + p.counted + " bytes");
            assertFalse(p.find("large"));
            assertSame(p, SchemaPattern.cached(p.source));
            compiled.add(new WeakReference<>(p.regex));
        }
        long[] f = checkCache();
        assertTrue(f[0] < 100, "the cache holds " + f[0] + " patterns counted as 1 MiB each");
        assertTrue(f[1] > SchemaPattern.MAX_CACHED_BYTES - SchemaPattern.MAX_CACHED_PATTERN_BYTES,
                "the cache counts " + f[1] + " bytes, well under its bound, after patterns left it");
        int kept = PatternRetentionTest.live(compiled);
        assertTrue(kept <= f[0], kept + " of " + compiled.size() + " large patterns are still on the heap, and the cache holds " + f[0]);

        String source = "^too-large(?=a)" + "[a]".repeat(2500) + "$";
        SchemaPattern large = SchemaPattern.compile(source);
        assertTrue(large.counted > SchemaPattern.MAX_CACHED_PATTERN_BYTES, "counted as " + large.counted);
        assertNull(SchemaPattern.cached(source), "a pattern counted as more than 2 MiB is in the cache");
        assertNotSame(large, SchemaPattern.compile(source));
        assertTrue(large.find("too-large" + "a".repeat(2500)) && !large.find("too-large" + "a".repeat(2499)));
        // A validator has it all the same.
        Validator v = Validator.compile("{\"pattern\": \"" + source + "\"}");
        assertTrue(v.isValid("\"too-large" + "a".repeat(2500) + "\""));
        assertFalse(v.isValid("\"too-large" + "a".repeat(2499) + "\""));
        assertEquals(f[1], checkCache()[1]);
    }

    private static long heap() throws InterruptedException {
        long least = Long.MAX_VALUE;
        for (int pass = 0; pass < 4; pass++) {
            System.gc();
            Thread.sleep(10);
            Runtime r = Runtime.getRuntime();
            least = Math.min(least, r.totalMemory() - r.freeMemory());
        }
        return least;
    }

    /**
     * The count of a pattern is not below what it holds: the heap that 200 patterns of a shape hold, each with a
     * number of its own in it, against what they are counted as. The shapes are those that held most for a
     * character of translation among the 59 shapes of the probe (followup-probe/java/tools/Held.java), where none
     * held more than a third of its count.
     */
    @Test
    void aPatternIsCountedAsNoLessThanItHolds() throws Exception {
        List<IntFunction<String>> shapes = List.of(
                n -> "^(?=" + n + ")" + "[a]".repeat(1000),
                n -> "^(?=" + n + ")" + "\\b".repeat(1000),
                n -> "^(?=" + n + ")" + "a?".repeat(2000),
                n -> "(?i:^(?=" + n + ")" + "a".repeat(2000) + "(?=b))",
                n -> "^(?=" + n + ")" + "(?:a)?".repeat(1000),
                n -> "^(?=" + n + ")" + ".".repeat(2000),
                n -> "^(k" + n + "|" + "v".repeat(300) + "|w|x|y|z)$",
                n -> "^k" + n + "[ab]".repeat(1000) + "$");
        for (IntFunction<String> shape : shapes) {
            int copies = 200;
            SchemaPattern[] held = new SchemaPattern[copies];
            shape.apply(0);
            long before = heap();
            long counted = 0;
            for (int n = 0; n < copies; n++) {
                held[n] = SchemaPattern.compile(shape.apply(100000 + n));
                counted += held[n].counted;
            }
            long holds = heap() - before;
            assertTrue(holds <= counted, copies + " patterns like " + held[0].source.substring(0, 30) + " hold " + holds
                    + " bytes and are counted as " + counted);
            assertTrue(held[copies - 1].counted > 0);
        }
    }

    /**
     * Threads that compile schemas whose patterns overlap, and validate with them and with one validator they all
     * share, while the cache turns over: every answer is right and the cache is as it says.
     */
    @Test
    void theCacheAndTheSlotsAreSafeAcrossThreads() throws Exception {
        Validator shared = Validator.compile(
                "{\"properties\": {\"a\": {\"pattern\": \"^(?=shared-)[a-z0-9-]+$\"}, \"b\": {\"pattern\": \"^(c)\\\\1$\"}}}");
        ConcurrentLinkedQueue<String> failures = new ConcurrentLinkedQueue<>();
        Thread[] threads = new Thread[8];
        for (int t = 0; t < threads.length; t++) {
            int thread = t;
            threads[t] = new Thread(() -> {
                try {
                    for (int round = 0; round < 700; round++) {
                        // 1,500 distinct patterns in all, more than the cache holds, each met by several threads.
                        int n = (round * 7 + thread * 131) % 1500;
                        int m = (n + 1) % 1500;
                        Validator v = Validator.compile("{\"properties\": {\"p\": {\"pattern\": \"^(?=threads-" + n
                                + "$)[a-z0-9-]+$\"}, \"q\": {\"pattern\": \"^(?=threads-" + m + "$)[a-z0-9-]+$\"}}}");
                        if (!v.isValid("{\"p\": \"threads-" + n + "\", \"q\": \"threads-" + m + "\"}")
                                || v.isValid("{\"p\": \"threads-" + m + "\"}") || v.isValid("{\"q\": \"threads-" + n + "\"}")) {
                            failures.add("patterns " + n + " and " + m + " on thread " + thread);
                        }
                        if (!shared.isValid("{\"a\": \"shared-" + round + "\", \"b\": \"cc\"}")
                                || shared.isValid("{\"a\": \"other\"}") || shared.isValid("{\"b\": \"cd\"}")) {
                            failures.add("the shared validator on thread " + thread);
                        }
                    }
                } catch (Throwable e) {
                    failures.add(e.toString());
                }
            });
            threads[t].start();
        }
        for (Thread t : threads) {
            t.join();
        }
        assertTrue(failures.isEmpty(), String.join("; ", failures));
        checkCache();
    }
}
