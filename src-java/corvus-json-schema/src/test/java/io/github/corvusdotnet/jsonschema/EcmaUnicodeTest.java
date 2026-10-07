package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.BitSet;
import java.util.IdentityHashMap;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import org.junit.jupiter.api.Test;

/**
 * The Unicode data of ECMA-262 patterns, and the way a set of it is written for {@code java.util.regex}. The data
 * itself was compared, when it was generated, with the Unicode Character Database and with V8 (see
 * {@code gen-ecma-unicode-data.ps1}). These tests check that it is read whole and that a class written from a set
 * matches exactly the set on the JDK the tests run on.
 */
class EcmaUnicodeTest {
    private static int[] property(String expression) {
        return EcmaUnicode.property(expression, 0, expression.length());
    }

    private static BitSet bits(int[] set) {
        BitSet bits = new BitSet();
        for (int i = 0; i < set.length; i += 2) {
            bits.set(set[i], set[i + 1] + 1);
        }
        return bits;
    }

    private static List<String> expressions() {
        List<String> all = new ArrayList<>();
        for (String name : EcmaUnicodeData.CATEGORY_NAMES) {
            all.add(name);
            all.add("gc=" + name);
            all.add("General_Category=" + name);
        }
        for (String name : EcmaUnicodeData.BINARY_NAMES) {
            all.add(name);
        }
        for (String name : EcmaUnicodeData.SCRIPT_NAMES) {
            all.add("sc=" + name);
            all.add("Script=" + name);
            all.add("scx=" + name);
            all.add("Script_Extensions=" + name);
        }
        return all;
    }

    /** Every table is a sorted list of ranges that neither overlap nor touch. */
    @Test
    void everySetIsWellFormed() {
        for (String expression : expressions()) {
            int[] set = property(expression);
            assertNotNull(set, expression);
            assertEquals(0, set.length % 2, expression);
            for (int i = 0; i < set.length; i += 2) {
                assertTrue(set[i] <= set[i + 1], expression);
                assertTrue(i == 0 ? set[i] >= 0 : set[i] > set[i - 1] + 1, expression);
            }
            assertTrue(set.length == 0 || set[set.length - 1] <= EcmaUnicode.MAX, expression);
        }
    }

    /** The General_Category values that are not unions hold every code point once, and the unions are their parts. */
    @Test
    void generalCategoriesPartitionTheCodePoints() {
        String[] leaves = {
            "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
            "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Cn",
        };
        BitSet all = new BitSet();
        int total = 0;
        for (String leaf : leaves) {
            BitSet b = bits(property(leaf));
            assertFalse(b.intersects(all), leaf);
            all.or(b);
            total += b.cardinality();
        }
        assertEquals(0x110000, total);
        String[][] unions = {
            {"LC", "Lu", "Ll", "Lt"}, {"L", "Lu", "Ll", "Lt", "Lm", "Lo"}, {"M", "Mn", "Mc", "Me"},
            {"N", "Nd", "Nl", "No"}, {"P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po"}, {"S", "Sm", "Sc", "Sk", "So"},
            {"Z", "Zs", "Zl", "Zp"}, {"C", "Cc", "Cf", "Cs", "Co", "Cn"},
        };
        for (String[] union : unions) {
            BitSet b = new BitSet();
            for (int k = 1; k < union.length; k++) {
                b.or(bits(property(union[k])));
            }
            assertEquals(b, bits(property(union[0])), union[0]);
        }
        assertArrayEquals(EcmaUnicode.complement(property("Cn")), property("Assigned"));
        assertArrayEquals(new int[] {0, 0x10FFFF}, property("Any"));
        assertArrayEquals(new int[] {0, 0x7F}, property("ASCII"));
        assertArrayEquals(new int[] {0xD800, 0xDFFF}, property("Cs"));
    }

    /** The counts ECMA-262 gives: 53 binary properties, 38 General_Category values. The scripts hold every code point. */
    @Test
    void theNamesAreThoseOfEcma262() {
        Map<int[], Boolean> binary = new IdentityHashMap<>();
        for (String name : EcmaUnicodeData.BINARY_NAMES) {
            binary.put(property(name), true);
        }
        assertEquals(53, binary.size());
        Map<int[], Boolean> categories = new IdentityHashMap<>();
        for (String name : EcmaUnicodeData.CATEGORY_NAMES) {
            categories.put(property(name), true);
        }
        assertEquals(38, categories.size());
        Map<int[], Boolean> scripts = new IdentityHashMap<>();
        int total = 0;
        for (String name : EcmaUnicodeData.SCRIPT_NAMES) {
            int[] set = property("sc=" + name);
            if (scripts.put(set, true) == null) {
                total += bits(set).cardinality();
            }
        }
        assertEquals(0x110000, total);
        assertEquals("17.0.0", EcmaUnicodeData.UNICODE_VERSION);
        // Names are matched exactly, and a name of one kind is not a name of another.
        assertNull(property("lu"));
        assertNull(property("Greek"));
        assertNull(property("sc=Lu"));
        assertNull(property("gc=Greek"));
        assertNull(property("gc=Alphabetic"));
        assertNull(property("Script"));
        assertNull(property(""));
        assertNull(property("="));
        assertNull(property("sc="));
        assertNull(property("Lu="));
        assertNull(property("scx=Hrkt"));
        assertTrue(EcmaUnicode.isProperty("x\\p{scx=Grek}", 4, 12));
        assertFalse(EcmaUnicode.isProperty("x\\p{scx=Grek}", 4, 11));
    }

    /**
     * A class written from a set matches exactly the set. Every distinct set of the data and its complement are
     * written, and each is asked about the code points on either side of each of its boundaries and about a sample of
     * the others.
     */
    @Test
    void aWrittenClassMatchesItsSet() {
        Map<int[], String> sets = new IdentityHashMap<>();
        for (String expression : expressions()) {
            sets.putIfAbsent(property(expression), expression);
        }
        int checked = 0;
        for (Map.Entry<int[], String> e : sets.entrySet()) {
            for (int[] set : new int[][] {e.getKey(), EcmaUnicode.complement(e.getKey())}) {
                if (set.length == 0) {
                    continue;
                }
                StringBuilder sb = new StringBuilder();
                EcmaUnicode.appendClass(sb, set);
                Matcher m = Pattern.compile(sb.toString()).matcher("");
                for (int i = 0; i < set.length; i++) {
                    for (int c = set[i] - 1; c <= set[i] + 1; c++) {
                        checked += check(m, set, c, e.getValue());
                    }
                }
                for (int c = 0; c <= EcmaUnicode.MAX; c += 211) {
                    checked += check(m, set, c, e.getValue());
                }
            }
        }
        assertTrue(checked > 1_000_000, "checked " + checked);
    }

    private static int check(Matcher m, int[] set, int c, String what) {
        if (c < 0 || c > EcmaUnicode.MAX) {
            return 0;
        }
        boolean expected = EcmaUnicode.contains(set, c);
        if (m.reset(new String(Character.toChars(c))).matches() != expected) {
            assertEquals(expected, !expected, what + " at U+" + Integer.toHexString(c));
        }
        return 1;
    }

    /** One large set is checked on every code point. */
    @Test
    void aWrittenClassMatchesItsSetOnEveryCodePoint() {
        for (String expression : new String[] {"L", "Grapheme_Base", "scx=Common", "Emoji"}) {
            int[] set = property(expression);
            StringBuilder sb = new StringBuilder();
            EcmaUnicode.appendClass(sb, set);
            Matcher m = Pattern.compile(sb.toString()).matcher("");
            for (int c = 0; c <= EcmaUnicode.MAX; c++) {
                check(m, set, c, expression);
            }
        }
    }

    /**
     * The properties java.util.regex has classes for are written with those classes, corrected for the JDK the tests
     * run on. Each is asked about every code point, alone, negated and in a class with other members.
     */
    @Test
    void anEngineClassIsCorrectedToItsSet() {
        List<String> names = new ArrayList<>();
        for (String name : EcmaUnicodeData.CATEGORY_NAMES) {
            names.add(name);
        }
        names.add("Alphabetic");
        names.add("Lowercase");
        names.add("Uppercase");
        Map<int[], Boolean> seen = new IdentityHashMap<>();
        int[] extra = {'_', '_', 0x378, 0x379, 0x1F600, 0x1F600};
        for (String name : names) {
            int[] set = property(name);
            EcmaUnicode.EngineClass engine = EcmaUnicode.engineClass(set);
            assertNotNull(engine, name);
            assertFalse(EcmaUnicode.intersects(engine.removed, set), name);
            assertArrayEquals(EcmaUnicode.difference(engine.added, set), new int[0], name);
            if (seen.put(set, true) != null) {
                continue;
            }
            assertWritten("\\p{" + name + "}", set);
            assertWritten("\\P{" + name + "}", EcmaUnicode.complement(set));
            assertWritten("[\\p{" + name + "}_\\u0378\\u0379\\u{1F600}]", EcmaUnicode.union(set, extra));
            assertWritten("[^\\p{" + name + "}_\\u0378\\u0379\\u{1F600}]",
                    EcmaUnicode.complement(EcmaUnicode.union(set, extra)));
        }
        assertEquals(41, seen.size());
        assertWritten("[\\p{Lu}\\p{Nd}\\p{Alphabetic}\\P{L}]",
                EcmaUnicode.union(EcmaUnicode.union(property("Lu"), property("Nd")),
                        EcmaUnicode.union(property("Alphabetic"), EcmaUnicode.complement(property("L")))));
        assertNull(EcmaUnicode.engineClass(property("Dash")));
        assertNull(EcmaUnicode.engineClass(property("sc=Latin")));
    }

    /** Every code point in order, but for the surrogates (a high one before a low one would pair with it). */
    private static String everyCodePoint;

    /** Checks that a one-character pattern, as translated, matches exactly the set, on every code point. */
    private static void assertWritten(String pattern, int[] set) {
        if (everyCodePoint == null) {
            StringBuilder all = new StringBuilder(0x220000);
            for (int c = 0; c <= EcmaUnicode.MAX; c++) {
                if (c < 0xD800 || c > 0xDFFF) {
                    all.appendCodePoint(c);
                }
            }
            everyCodePoint = all.toString();
        }
        Matcher m = Pattern.compile(EcmaRegex.translate(pattern)).matcher("");
        for (int c = 0xD800; c <= 0xDFFF; c += 0x3FF) {
            check(m, set, c, pattern);
        }
        String text = everyCodePoint;
        m.reset(text);
        BitSet matched = new BitSet();
        int at = 0;
        while (at < text.length() && m.find(at)) {
            matched.set(text.codePointAt(m.start()));
            at = m.end();
        }
        BitSet expected = bits(set);
        expected.clear(0xD800, 0xE000);
        assertEquals(expected, matched, pattern);
    }

    /** The set operations agree with a bit set. */
    @Test
    void setOperations() {
        Random random = new Random(11);
        for (int round = 0; round < 500; round++) {
            int n = random.nextInt(12);
            int[] pairs = new int[2 * n + 2];
            BitSet expected = new BitSet();
            for (int k = 0; k < n; k++) {
                int lo = random.nextInt(60);
                int hi = lo + random.nextInt(8);
                pairs[2 * k] = lo;
                pairs[2 * k + 1] = hi;
                expected.set(lo, hi + 1);
            }
            int[] set = EcmaUnicode.setOf(pairs, 2 * n);
            assertEquals(expected, bits(set));
            for (int i = 2; i < set.length; i += 2) {
                assertTrue(set[i] > set[i - 1] + 1);
            }
            BitSet inverse = new BitSet();
            inverse.set(0, 0x110000);
            inverse.andNot(expected);
            assertEquals(inverse, bits(EcmaUnicode.complement(set)));
            assertArrayEquals(set, EcmaUnicode.complement(EcmaUnicode.complement(set)));
            int[] other = EcmaUnicode.setOf(new int[] {random.nextInt(70), 70 + random.nextInt(5)}, 2);
            BitSet union = (BitSet) expected.clone();
            union.or(bits(other));
            assertEquals(union, bits(EcmaUnicode.union(set, other)));
            assertEquals(expected.intersects(bits(other)), EcmaUnicode.intersects(set, other));
            for (int c = 0; c < 80; c++) {
                assertEquals(expected.get(c), EcmaUnicode.contains(set, c));
            }
        }
    }

    /** The two Canonicalize functions of ECMA-262. */
    @Test
    void caseFolding() {
        // With the u flag grammar: simple case folding.
        assertArrayEquals(new int[] {'K', 'K', 'k', 'k', 0x212A, 0x212A}, EcmaUnicode.foldClosure(new int[] {'k', 'k'}, true));
        assertArrayEquals(new int[] {'S', 'S', 's', 's', 0x17F, 0x17F}, EcmaUnicode.foldClosure(new int[] {'S', 'S'}, true));
        assertArrayEquals(new int[] {0xDF, 0xDF, 0x1E9E, 0x1E9E}, EcmaUnicode.foldClosure(new int[] {0xDF, 0xDF}, true));
        assertArrayEquals(new int[] {0x3A3, 0x3A3, 0x3C2, 0x3C3}, EcmaUnicode.foldClosure(new int[] {0x3C2, 0x3C2}, true));
        assertArrayEquals(new int[] {0x130, 0x131}, EcmaUnicode.foldClosure(new int[] {0x130, 0x131}, true));
        assertArrayEquals(new int[] {0x10400, 0x10400, 0x10428, 0x10428},
                EcmaUnicode.foldClosure(new int[] {0x10400, 0x10400}, true));
        assertArrayEquals(new int[] {'A', 'Z', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A},
                EcmaUnicode.foldClosure(new int[] {'a', 'z'}, true));
        // With no flag: the uppercase of one UTF-16 code unit, never from outside ASCII into ASCII.
        assertArrayEquals(new int[] {'K', 'K', 'k', 'k'}, EcmaUnicode.foldClosure(new int[] {'k', 'k'}, false));
        assertArrayEquals(new int[] {0x17F, 0x17F}, EcmaUnicode.foldClosure(new int[] {0x17F, 0x17F}, false));
        assertArrayEquals(new int[] {0xDF, 0xDF}, EcmaUnicode.foldClosure(new int[] {0xDF, 0xDF}, false));
        assertArrayEquals(new int[] {0x1F80, 0x1F80}, EcmaUnicode.foldClosure(new int[] {0x1F80, 0x1F80}, false));
        assertArrayEquals(new int[] {0x10400, 0x10400}, EcmaUnicode.foldClosure(new int[] {0x10400, 0x10400}, false));
        assertArrayEquals(new int[] {0xB5, 0xB5, 0x39C, 0x39C, 0x3BC, 0x3BC},
                EcmaUnicode.foldClosure(new int[] {0xB5, 0xB5}, false));
        assertArrayEquals(new int[] {0x1C4, 0x1C6}, EcmaUnicode.foldClosure(new int[] {0x1C5, 0x1C5}, false));
        assertTrue(EcmaUnicode.contains(EcmaUnicode.cased(true), 0x212A));
        assertFalse(EcmaUnicode.contains(EcmaUnicode.cased(false), 0x212A));
        assertFalse(EcmaUnicode.contains(EcmaUnicode.cased(true), '1'));
    }

    /** The characters of a group name. */
    @Test
    void groupNameCharacters() {
        assertTrue(EcmaUnicode.isNameStart('a'));
        assertTrue(EcmaUnicode.isNameStart('$'));
        assertTrue(EcmaUnicode.isNameStart('_'));
        assertFalse(EcmaUnicode.isNameStart('1'));
        assertFalse(EcmaUnicode.isNameStart('-'));
        assertTrue(EcmaUnicode.isNameStart(0x3C0));
        assertTrue(EcmaUnicode.isNameStart(0x1D4D1));
        assertFalse(EcmaUnicode.isNameStart(0x1F600));
        assertFalse(EcmaUnicode.isNameStart(0x200D));
        assertTrue(EcmaUnicode.isNamePart('1'));
        assertTrue(EcmaUnicode.isNamePart(0x200C));
        assertTrue(EcmaUnicode.isNamePart(0x200D));
        assertTrue(EcmaUnicode.isNamePart(0x301));
        assertFalse(EcmaUnicode.isNamePart(' '));
        assertFalse(EcmaUnicode.isNamePart(0x1F600));
    }
}
