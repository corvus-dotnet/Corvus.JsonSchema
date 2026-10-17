package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.IntFunction;
import java.util.regex.Pattern;
import org.junit.jupiter.api.Test;

/**
 * The limit on what {@code java.util.regex} keeps for a pattern. The limit on the length of a translation, 1,048,576
 * characters, bounds what the engine keeps for literal text and not for the rest: it keeps 320 bytes for a class of
 * one character, which the translation writes in eight, and 66 for a repetition of one character, which is two. So
 * a pattern under every limit could hold 42 MB once compiled. The library now counts what the engine keeps for a
 * translation ({@link EcmaRegex#held}) and refuses a pattern counted over 8 MiB as too large.
 */
class PatternHeldLimitTest {
    private static final String ENDS = "It is a valid ECMA-262 regular expression, and is too large: java.util.regex "
            + "would keep more than 8,388,608 bytes for it, as the library counts them.";

    /** Runs on a thread with a stack of 512 MB, so that the stack java.util.regex compiles on is no limit here. */
    private static void withStack(Runnable work) throws InterruptedException {
        AtomicReference<Throwable> failure = new AtomicReference<>();
        Thread thread = new Thread(null, () -> {
            try {
                work.run();
            } catch (Throwable e) {
                failure.set(e);
            }
        }, "deep", 512L << 20);
        thread.start();
        thread.join();
        if (failure.get() != null) {
            throw new AssertionError(failure.get());
        }
    }

    private static long heap() {
        long least = Long.MAX_VALUE;
        for (int pass = 0; pass < 4; pass++) {
            System.gc();
            Runtime r = Runtime.getRuntime();
            least = Math.min(least, r.totalMemory() - r.freeMemory());
        }
        return least;
    }

    /**
     * Patterns of a million characters or fewer that are under every other limit: classes of one character,
     * optional characters, lookbehinds, small groups that repeat. Each is refused as too large, with a message that
     * names the limit, and never as invalid. With a third of the copies each compiles and matches.
     */
    @Test
    void aPatternThatWouldHoldMoreThanTheLimitIsRefusedAsTooLarge() throws InterruptedException {
        // A shape, the copies of it that are refused, the copies that are a pattern as before, and a text with
        // whether that pattern takes it.
        String[][] cases = {
            {"[a]", "100000", "1000", "a".repeat(1000), "true"}, {"a?", "400000", "3000", "", "true"},
            {"(?<=a)", "100000", "1000", "a", "false"}, {"(?:a|bc)*", "100000", "1000", "abca", "true"},
            {"(?=a)", "150000", "1000", "a", "true"}, {"\\\\b", "10000", "100", "a", "true"},
            {"(?:)", "200000", "2000", "", "true"}, {"a|", "500000", "3000", "", "true"},
        };
        withStack(() -> {
            for (String[] c : cases) {
                String body = c[0].repeat(Integer.parseInt(c[1]));
                PatternTooLargeException large = assertThrows(PatternTooLargeException.class,
                        () -> Validator.compile("{\"pattern\": \"^(?:" + body + ")\"}"), c[0]);
                assertTrue(large.getMessage().endsWith(ENDS), large.getMessage());
                assertFalse(large.getMessage().contains("nvalid"), large.getMessage());
                // Not valid is still not valid, and is said to be.
                SchemaCompilationException invalid = assertThrows(SchemaCompilationException.class,
                        () -> Validator.compile("{\"pattern\": \"^(?:" + body + "\"}"), c[0]);
                assertFalse(invalid instanceof PatternTooLargeException, c[0]);
            }
        });
        // With fewer copies each is a pattern as it was, on a stack of any size.
        for (String[] c : cases) {
            Validator v = Validator.compile("{\"pattern\": \"^(?:" + c[0].repeat(Integer.parseInt(c[2])) + ")\"}");
            assertEquals(Boolean.parseBoolean(c[4]), v.isValid("\"" + c[3] + "\""), c[0]);
        }
    }

    /**
     * The limit is of the translation of reference, so a pattern is refused on every JDK or on none: the same
     * copies are accepted and refused here on Java 17, 21 and 25, for patterns whose translation differs between
     * them (a property, and a backreference that ignores case).
     */
    @Test
    void theLimitIsTheSameOnEveryJdk() throws InterruptedException {
        withStack(() -> {
            assertEquals(null, EcmaRegex.tooLarge("^" + "[a]".repeat(25000) + "$"));
            assertEquals(EcmaRegex.LIMIT_HELD, EcmaRegex.tooLarge("^" + "[a]".repeat(26000) + "$"));
            assertEquals(null, EcmaRegex.tooLarge("^" + "a?".repeat(110000) + "$"));
            assertEquals(EcmaRegex.LIMIT_HELD, EcmaRegex.tooLarge("^" + "a?".repeat(120000) + "$"));
            assertEquals(null, EcmaRegex.tooLarge("^" + "\\p{L}".repeat(300) + "$"));
            assertEquals(EcmaRegex.LIMIT_TRANSLATION, EcmaRegex.tooLarge("^" + "\\p{L}".repeat(400) + "$"));
            assertEquals(null, EcmaRegex.tooLarge("^(?i:" + "(.)\\1".repeat(50) + ")$"));
            assertEquals(EcmaRegex.LIMIT_HELD, EcmaRegex.tooLarge("^(?i:" + "(.)\\1".repeat(60) + ")$"));
        });
    }

    /** What the count is of, part by part. */
    @Test
    void theCountOfATranslation() {
        long base = EcmaRegex.held("");
        assertEquals(512, base);
        assertEquals(1 + 36 + 4, EcmaRegex.held("a") - base);
        assertEquals(3 + 36 + 12, EcmaRegex.held("abc") - base);
        assertEquals(6 + 36 + 4, EcmaRegex.held("\\x{61}") - base);
        assertEquals(2 + 36 + 4, EcmaRegex.held("\\(") - base);
        assertEquals(2 + 36 + 4 + 32, EcmaRegex.held("a?") - base);
        assertEquals(3 + 36 + 4 + 32, EcmaRegex.held("a+?") - base);
        assertEquals(3 + 36 + 8 + 32 + 36, EcmaRegex.held("ab?") - base);
        assertEquals(6 + 36 + 4 + 32, EcmaRegex.held("a{2,3}") - base);
        assertEquals(5 + 48 + 36 + 4, EcmaRegex.held("(?:a)") - base);
        assertEquals(6 + 48 + 36 + 4 + 72, EcmaRegex.held("(?:a)*") - base);
        assertEquals(5 + 72 + 36 + 4, EcmaRegex.held("(?=a)") - base);
        assertEquals(6 + 104 + 36 + 4, EcmaRegex.held("(?<!a)") - base);
        assertEquals(9 + 48 + 2 * (36 + 4) + 36 + 4 + 84 + 12, EcmaRegex.held("(?:a|b|c)") - base);
        assertEquals(3 + 16 + 36 + 4 + 16, EcmaRegex.held("^a$") - base);
        assertEquals(5 + 48 + 36 + 4 + 24, EcmaRegex.held("(a)\\1") - base);
        // Classes: a Latin-1 character by itself makes the table, once; a range and a character beyond do not.
        assertEquals(8 + 8 + 312, EcmaRegex.held("[\\x{61}]") - base);
        assertEquals(5 + 8 + 312, EcmaRegex.held("[abc]") - base);
        assertEquals(15 + 8 + 48, EcmaRegex.held("[\\x{61}-\\x{62}]") - base);
        assertEquals(5 + 8 + 48, EcmaRegex.held("[a-c]") - base);
        assertEquals(6 + 8 + 48 + 312, EcmaRegex.held("[a-c_]") - base);
        assertEquals(9 + 8 + 40, EcmaRegex.held("[\\x{100}]") - base);
        assertEquals(16 + 8 + 80, EcmaRegex.held("[\\x{100}\\x{102}]") - base);
        assertEquals(4 + 8 + 16 + 312, EcmaRegex.held("[^a]") - base);
        assertEquals(4 + 8 + 312, EcmaRegex.held("[\\]]") - base);
        assertEquals(15 + 8 + 48 + 8 + 16 + 312, EcmaRegex.held("[a-z&&[^aeiou]]") - base);
    }

    /**
     * The count is not below what a compiled translation holds: the heap that copies of a translation hold against
     * the count, for the shapes that hold most for their length and for patterns at the limit. No pattern the
     * library accepts holds more than the 8 MiB of the limit and what the measurement is out by.
     */
    @Test
    void aTranslationIsCountedAsNoLessThanItHolds() throws InterruptedException {
        List<IntFunction<String>> shapes = List.of(
                n -> "^(?=" + n + ")" + "[a]".repeat(2000), n -> "^(?=" + n + ")" + "ab?".repeat(4000),
                n -> "^(?=" + n + ")" + "\\b".repeat(1000), n -> "^(?=" + n + ")" + "(?:a|bc)*".repeat(2000),
                n -> "^(?=" + n + ")" + "(?<=a)".repeat(2000), n -> "^(?=" + n + ")" + "\\s.\\w".repeat(1000),
                n -> "(?i:^(?=" + n + ")" + "k".repeat(2000) + ")", n -> "(?m:^(?=" + n + ")" + "$^".repeat(1000) + ")",
                n -> "^(?=" + n + ")" + "a".repeat(40000), n -> "^(?=" + n + ")" + "(?:ab|cd)".repeat(3000),
                n -> "^(?=" + n + ")" + "(a)?\\1".repeat(1000));
        List<String> under = new ArrayList<>();
        for (IntFunction<String> shape : shapes) {
            int copies = 100;
            Pattern[] held = new Pattern[copies];
            long counted = 0;
            long before = heap();
            for (int n = 0; n < copies; n++) {
                String translated = EcmaRegex.translate(shape.apply(1000 + n));
                counted += EcmaRegex.held(translated);
                held[n] = Pattern.compile(translated);
            }
            long holds = heap() - before;
            // Within 5%: a long run of literal text is an int[] large enough for G1 to give it regions of its own,
            // and what it rounds up to depends on the size of the regions, which depends on the heap the machine
            // gives the JVM. On a macOS runner with Java 25, 40,000 characters held 2.6% more than counted.
            if (holds > counted + counted / 20) {
                under.add(held[0].pattern().substring(0, 40) + " holds " + holds + " and is counted " + counted);
            }
        }
        assertTrue(under.isEmpty(), String.join("; ", under));
        // At the limit: the most copies of a shape that are accepted, and literal text to the limit of length.
        withStack(() -> {
            for (String body : new String[] {"[a]", "(?<=a)", "a?", "(?:a|bc)*"}) {
                int low = 0;
                int high = 1 << 17;
                while (low < high) {
                    int mid = (low + high + 1) >>> 1;
                    String p = "^" + body.repeat(mid);
                    if (p.length() <= 1 << 20 && EcmaRegex.translate(p) != null) {
                        low = mid;
                    } else {
                        high = mid - 1;
                    }
                }
                String translated = EcmaRegex.translate("^" + body.repeat(low));
                assertTrue(EcmaRegex.held(translated) > 8_300_000, body + " is counted " + EcmaRegex.held(translated));
                long before = heap();
                Pattern one = Pattern.compile(translated);
                Pattern two = Pattern.compile(new String(translated.toCharArray()));
                long holds = (heap() - before) / 2;
                assertTrue(holds < 9L << 20, low + " copies of " + body + " hold " + holds + " bytes");
                assertTrue(one != two);
            }
        });
    }
}
