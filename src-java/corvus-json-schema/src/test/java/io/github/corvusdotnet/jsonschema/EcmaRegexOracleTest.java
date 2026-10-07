package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.regex.Pattern;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.Test;

/**
 * Checks the translator of ECMA-262 patterns against what V8 answers.
 *
 * <p>{@code v8_oracle.json} holds patterns with V8's verdict on each (valid with the u flag, valid with no flag) and
 * its answer on a set of texts. It is a copy of the file the Go port's pattern package is tested with, written by
 * that package's {@code testdata/gen_oracle.js} under Node 24. For every pattern the validity must agree, and for a
 * valid pattern the answer on every text must agree. The exceptions are counted by reason and the counts are
 * asserted, so a pattern cannot move into an exception unnoticed:
 *
 * <ul>
 *   <li>a pattern that is valid ECMA-262 and that {@code java.util.regex} cannot run with the same meaning. The
 *       translator refuses these and says why ({@link EcmaRegex#unsupported});
 *   <li>six patterns on which V8 13.6 itself answers a case-insensitive modifier group differently from the same
 *       pattern with the i flag, which ECMA-262 does not allow. Only their validity is taken from the file. The
 *       same behaviour is checked through the patterns that V8 matched with the flag set.
 * </ul>
 *
 * <p>V8 matches a pattern that is valid only with no flag by UTF-16 code unit, and this library matches every pattern
 * by code point, so such a pattern is not asked about a text with a character beyond the Basic Multilingual Plane.
 */
class EcmaRegexOracleTest {
    /** The patterns of the file that V8 answers inconsistently (see the class comment). */
    private static final Set<String> V8_MODIFIER_BUGS = Set.of(
            "^(?i:\\u212a)$", "^(?i:\\u017f)$", "^(?i:\\u1e9e)$", "^(?i:\\u03bc)$", "^(?i:\\u03bc)\\&?$", "(?i:\\Bs)");

    private static JsonDocument oracle;
    private static String[] inputs;
    private static String[] shortInputs;

    /** What one run over a part of the file found. */
    private static final class Tally {
        int patterns;
        int answers;
        int matched;
        int v8Inconsistent;
        final Map<String, Integer> unsupported = new TreeMap<>();
        final List<String> failures = new ArrayList<>();

        void fail(String message) {
            if (failures.size() < 60) {
                failures.add(message);
            }
        }

        void report(String what) {
            System.out.println("[oracle] " + what + ": " + patterns + " patterns, " + answers + " answers ("
                    + matched + " of them a match), " + v8Inconsistent + " answered inconsistently by V8, unsupported "
                    + unsupported);
            assertTrue(failures.isEmpty(), () -> what + ":\n" + String.join("\n", failures));
        }
    }

    @BeforeAll
    static void load() throws IOException {
        try (InputStream in = EcmaRegexOracleTest.class.getResourceAsStream("v8_oracle.json")) {
            assertNotNull(in, "v8_oracle.json");
            oracle = JsonDocument.parse(in.readAllBytes());
        }
        inputs = strings(oracle.property(oracle.root(), "inputs"));
        shortInputs = strings(oracle.property(oracle.root(), "shortInputs"));
    }

    private static String[] strings(int array) {
        String[] out = new String[oracle.count(array)];
        for (int i = 0; i < out.length; i++) {
            out[i] = oracle.string(oracle.first(array) + i);
        }
        return out;
    }

    /** Bit k of a string of hexadecimal digits, four bits to a digit, first bit highest. */
    private static boolean bit(String hex, int k) {
        return (Character.digit(hex.charAt(k / 4), 16) >> (3 - k % 4) & 1) == 1;
    }

    private static boolean beyondBmp(String s) {
        return s.codePointCount(0, s.length()) != s.length();
    }

    private static String show(String s) {
        StringBuilder sb = new StringBuilder();
        JsonDocument.appendQuoted(sb, s);
        for (int i = sb.length() - 1; i >= 0; i--) {
            char c = sb.charAt(i);
            if (c > 126) {
                sb.replace(i, i + 1, String.format("\\u%04x", (int) c));
            }
        }
        return sb.toString();
    }

    /**
     * Checks one pattern: its validity under each grammar, and, when it can be run and {@code bits} is not null, its
     * answer on every text. The answers of a pattern in {@code inconsistent} are not compared.
     */
    private static void check(Tally tally, String p, boolean validUnicode, boolean validLegacy, String[] texts,
            String bits, Set<String> inconsistent) {
        tally.patterns++;
        if (new EcmaRegex.Validator().isValid(p) != validUnicode) {
            tally.fail("isValid(" + show(p) + ") is " + !validUnicode + ", V8 says " + validUnicode);
        }
        String translated = EcmaRegex.translate(p);
        String reason = EcmaRegex.unsupported(p);
        if (!validUnicode && !validLegacy) {
            if (translated != null || reason != null) {
                tally.fail(show(p) + " is not valid in V8. Got " + (translated != null ? translated : reason));
            }
            return;
        }
        if (reason != null) {
            if (translated != null) {
                tally.fail(show(p) + " is both unsupported (" + reason + ") and translated");
            }
            tally.unsupported.merge(reason, 1, Integer::sum);
            return;
        }
        if (translated == null) {
            tally.fail(show(p) + " is valid in V8 (u " + validUnicode + ", no flag " + validLegacy + ") and was refused");
            return;
        }
        if (bits == null) {
            return;
        }
        if (inconsistent.contains(p)) {
            tally.v8Inconsistent++;
            return;
        }
        Pattern regex = Pattern.compile(translated);
        // The evaluator decides some patterns without the engine: those every string matches, and the common shapes.
        SchemaPattern compiled = SchemaPattern.compile(p);
        PatternShapes.Matcher shape = compiled.shape;
        for (int i = 0; i < texts.length; i++) {
            if (!validUnicode && beyondBmp(texts[i])) {
                continue;
            }
            boolean want = bit(bits, i);
            tally.answers++;
            if (want) {
                tally.matched++;
            }
            if (regex.matcher(texts[i]).find() != want) {
                tally.fail(show(p) + " on " + show(texts[i]) + ": got " + !want + ", V8 says " + want + "  [" + translated
                        + "]");
            }
            if (compiled.find(texts[i]) != want) {
                tally.fail(show(p) + " on " + show(texts[i]) + ": the compiled pattern says " + !want);
            }
            if (shape != null) {
                byte[] utf8 = texts[i].getBytes(StandardCharsets.UTF_8);
                if (shape.matches(utf8, 0, utf8.length, utf8.length == texts[i].length()) != want) {
                    tally.fail(show(p) + " on " + show(texts[i]) + ": its shape says " + !want);
                }
            }
        }
    }

    private String text(int object, String name) {
        int v = oracle.property(object, name);
        return v < 0 ? null : oracle.string(v);
    }

    /** The hand-written patterns: validity, and the answer on every text. */
    @Test
    void handWrittenPatterns() {
        Tally tally = new Tally();
        int hand = oracle.property(oracle.root(), "hand");
        for (int i = 0; i < oracle.count(hand); i++) {
            int h = oracle.first(hand) + i;
            check(tally, text(h, "p"), oracle.bool(oracle.property(h, "u")), oracle.bool(oracle.property(h, "l")),
                    inputs, text(h, "m"), V8_MODIFIER_BUGS);
        }
        tally.report("hand-written");
        assertEquals(EXPECTED_UNSUPPORTED_HAND, tally.unsupported);
        assertEquals(V8_MODIFIER_BUGS.size(), tally.v8Inconsistent);
    }

    /** The six patterns V8 answers inconsistently are answered here as V8 answers the same pattern with the i flag. */
    @Test
    void thePatternsV8AnswersInconsistently() {
        String[][] expected = {
            {"^(?i:\\u212a)$", "k", "K", "\u212a"},
            {"^(?i:\\u017f)$", "s", "S", "\u017f"},
            {"^(?i:\\u1e9e)$", "\u00df", "\u1e9e"},
            {"^(?i:\\u03bc)$", "\u00b5", "\u03bc", "\u039c"},
            {"^(?i:\\u03bc)\\&?$", "\u00b5", "\u03bc", "\u039c"},
        };
        for (String[] e : expected) {
            assertTrue(V8_MODIFIER_BUGS.contains(e[0]), e[0]);
            Pattern regex = Pattern.compile(EcmaRegex.translate(e[0]));
            for (String text : inputs) {
                boolean want = false;
                for (int k = 1; k < e.length; k++) {
                    want |= e[k].equals(text);
                }
                assertEquals(want, regex.matcher(text).find(), show(e[0]) + " on " + show(text));
            }
        }
    }

    /**
     * Case-insensitive, multiline and dot-all matching. V8 matched each pattern with the flags set for the whole
     * pattern, and here the pattern is wrapped in the modifier group that means the same.
     */
    @Test
    void patternsMatchedWithFlags() {
        Tally tally = new Tally();
        int flagged = oracle.property(oracle.root(), "flagged");
        for (int i = 0; i < oracle.count(flagged); i++) {
            int f = oracle.first(flagged) + i;
            boolean u = oracle.bool(oracle.property(f, "u"));
            check(tally, "(?" + text(f, "f") + ":" + text(f, "p") + ")", u, !u, inputs, text(f, "m"), Set.of());
        }
        tally.report("matched with flags");
        assertEquals(EXPECTED_UNSUPPORTED_FLAGGED, tally.unsupported);
        assertEquals(0, tally.v8Inconsistent);
    }

    /**
     * Random patterns over an alphabet of syntax characters, built with the generator the file was written with.
     * Each one's validity is checked, and the first few thousand are also matched against short texts.
     */
    @Test
    void generatedPatterns() {
        Tally tally = new Tally();
        String verdicts = text(oracle.root(), "verdicts");
        String[] matches = strings(oracle.property(oracle.root(), "randomMatches"));
        String alphabet = "ab0\\\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L";
        int[] seed = {0x9e3779b9};
        int[] counts = new int[3];
        for (int i = 0; i < verdicts.length(); i++) {
            int n = 1 + (int) (next(seed) % 8);
            StringBuilder sb = new StringBuilder();
            for (int k = 0; k < n; k++) {
                sb.append(alphabet.charAt((int) (next(seed) % alphabet.length())));
            }
            char verdict = verdicts.charAt(i);
            counts[verdict - '0']++;
            check(tally, sb.toString(), verdict == '1', verdict == '2', shortInputs,
                    i < matches.length && verdict != '0' ? matches[i] : null, Set.of());
        }
        System.out.println("[oracle] generated: " + counts[0] + " not valid, " + counts[1] + " valid with u, "
                + counts[2] + " valid only with no flag");
        tally.report("generated");
        assertEquals(EXPECTED_UNSUPPORTED_GENERATED, tally.unsupported);
    }

    /** One step of xorshift32, as an unsigned value. */
    private static long next(int[] seed) {
        int s = seed[0];
        s ^= s << 13;
        s ^= s >>> 17;
        s ^= s << 5;
        seed[0] = s;
        return s & 0xffffffffL;
    }

    // The patterns the translator refuses, by reason. EcmaRegex says what each reason means.
    private static final Map<String, Integer> EXPECTED_UNSUPPORTED_HAND = Map.of(
            EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND, 6,
            EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND, 3,
            EcmaRegex.REFUSED_GROUP_IN_LOOKAROUND, 2,
            EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE, 9);
    private static final Map<String, Integer> EXPECTED_UNSUPPORTED_FLAGGED = Map.of(
            EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND, 8,
            EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE, 16);
    private static final Map<String, Integer> EXPECTED_UNSUPPORTED_GENERATED = Map.of();
}
