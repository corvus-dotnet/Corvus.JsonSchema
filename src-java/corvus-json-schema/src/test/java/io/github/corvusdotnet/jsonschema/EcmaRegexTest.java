package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.Random;
import java.util.regex.Pattern;
import org.junit.jupiter.api.Test;

/**
 * The translator of ECMA-262 patterns on the points where it once differed from ECMA-262. The expected answers are
 * those of the Unicode Character Database and of V8. {@link EcmaRegexOracleTest} checks the translator against V8 in
 * bulk.
 */
class EcmaRegexTest {
    private static final String GRINNING_FACE = new String(Character.toChars(0x1F600));

    private static boolean matches(String pattern, String text) {
        assertNull(EcmaRegex.unsupported(pattern), pattern);
        String translated = EcmaRegex.translate(pattern);
        assertNotNull(translated, pattern);
        return Pattern.compile(translated).matcher(text).find();
    }

    private static void assertMatches(String pattern, String text) {
        assertTrue(matches(pattern, text), () -> pattern + " should match " + text);
    }

    private static void assertNoMatch(String pattern, String text) {
        assertFalse(matches(pattern, text), () -> pattern + " should not match " + text);
    }

    private static void assertValid(String pattern) {
        assertTrue(EcmaRegex.isValid(pattern), pattern);
        assertNotNull(EcmaRegex.translate(pattern), pattern);
    }

    private static void assertNotValid(String pattern) {
        assertFalse(EcmaRegex.isValid(pattern), pattern);
    }

    /** Every binary property of ECMA-262 (table 69) is the property, under its name and under its alias. */
    @Test
    void binaryPropertiesAreProperties() {
        String[] names = {
            "ASCII", "ASCII_Hex_Digit", "AHex", "Alphabetic", "Alpha", "Any", "Assigned", "Bidi_Control", "Bidi_C",
            "Bidi_Mirrored", "Bidi_M", "Case_Ignorable", "CI", "Cased", "Changes_When_Casefolded", "CWCF",
            "Changes_When_Casemapped", "CWCM", "Changes_When_Lowercased", "CWL", "Changes_When_NFKC_Casefolded",
            "CWKCF", "Changes_When_Titlecased", "CWT", "Changes_When_Uppercased", "CWU", "Dash",
            "Default_Ignorable_Code_Point", "DI", "Deprecated", "Dep", "Diacritic", "Dia", "Emoji", "Emoji_Component",
            "EComp", "Emoji_Modifier", "EMod", "Emoji_Modifier_Base", "EBase", "Emoji_Presentation", "EPres",
            "Extended_Pictographic", "ExtPict", "Extender", "Ext", "Grapheme_Base", "Gr_Base", "Grapheme_Extend",
            "Gr_Ext", "Hex_Digit", "Hex", "IDS_Binary_Operator", "IDSB", "IDS_Trinary_Operator", "IDST", "ID_Continue",
            "IDC", "ID_Start", "IDS", "Ideographic", "Ideo", "Join_Control", "Join_C", "Logical_Order_Exception", "LOE",
            "Lowercase", "Lower", "Math", "Noncharacter_Code_Point", "NChar", "Pattern_Syntax", "Pat_Syn",
            "Pattern_White_Space", "Pat_WS", "Quotation_Mark", "QMark", "Radical", "Regional_Indicator", "RI",
            "Sentence_Terminal", "STerm", "Soft_Dotted", "SD", "Terminal_Punctuation", "Term", "Unified_Ideograph",
            "UIdeo", "Uppercase", "Upper", "Variation_Selector", "VS", "White_Space", "space", "XID_Continue", "XIDC",
            "XID_Start", "XIDS",
        };
        for (String name : names) {
            String pattern = "^\\p{" + name + "}$";
            assertValid(pattern);
            // A name the u flag grammar does not know is read as the text "p{name}" by the grammar with no flag.
            assertNoMatch(pattern, "p{" + name + "}");
            assertNoMatch("^(?:\\p{" + name + "}|\\P{" + name + "})$", "p{" + name + "}");
            assertMatches("^(?:\\p{" + name + "}|\\P{" + name + "})$", "a");
        }
        assertMatches("^\\p{Dash}$", "-");
        assertMatches("^\\p{Dash}$", "\u2014");
        assertNoMatch("^\\p{Dash}$", "a");
        assertMatches("^\\p{Diacritic}$", "\u0301");
        assertMatches("^\\p{Quotation_Mark}$", "\u00ab");
        assertMatches("^\\p{RI}$", new String(Character.toChars(0x1F1E6)));
        assertMatches("^\\p{Math}$", "+");
        assertMatches("^\\p{AHex}+$", "09afAF");
        assertNoMatch("^\\p{AHex}+$", "\uff10");
        assertMatches("^\\p{Hex}+$", "\uff10");
        // The names ECMA-262 does not list are not properties, whatever java.util.regex makes of them.
        for (String name : new String[] {"IsAlphabetic", "javaLowerCase", "Latin", "InGreek", "L&", "Letter&", "blank",
            "Titlecase", "Other_Alphabetic", "Hyphen", "Composition_Exclusion", "alpha", "ascii", "Space"}) {
            assertNotValid("\\p{" + name + "}");
        }
    }

    /** Script_Extensions is its own property. U+0640 ARABIC TATWEEL has the script Common and nine extensions. */
    @Test
    void scriptExtensionsIsNotScript() {
        String tatweel = "\u0640";
        assertMatches("^\\p{sc=Zyyy}$", tatweel);
        assertMatches("^\\p{Script=Common}$", tatweel);
        assertNoMatch("^\\p{sc=Arab}$", tatweel);
        assertNoMatch("^\\p{Script=Syriac}$", tatweel);
        assertNoMatch("^\\p{scx=Zyyy}$", tatweel);
        assertNoMatch("^\\p{Script_Extensions=Common}$", tatweel);
        for (String script : new String[] {"Adlm", "Arab", "Mand", "Mani", "Ougr", "Phlp", "Rohg", "Sogd", "Syrc",
            "Arabic", "Syriac", "Adlam", "Old_Uyghur"}) {
            assertMatches("^\\p{scx=" + script + "}$", tatweel);
            assertMatches("^\\p{Script_Extensions=" + script + "}$", tatweel);
        }
        assertNoMatch("^\\p{scx=Latn}$", tatweel);
        // U+3001 IDEOGRAPHIC COMMA is Common, and is used with Han, Hiragana and others.
        assertMatches("^\\p{sc=Common}$", "\u3001");
        assertMatches("^\\p{scx=Hani}$", "\u3001");
        assertMatches("^\\p{scx=Hira}$", "\u3001");
        assertNoMatch("^\\p{sc=Hani}$", "\u3001");
        // A script with no extension of its own still has its members.
        assertMatches("^\\p{scx=Grek}+$", "\u03b1\u03b2");
        assertMatches("^\\P{scx=Arab}$", "a");
        assertNoMatch("^\\P{scx=Arab}$", tatweel);
        // Scripts by every name ECMA-262 lists, whatever the JDK's own Unicode version.
        assertValid("\\p{Script=Garay}");
        assertValid("\\p{sc=Gara}");
        assertValid("\\p{scx=Sidetic}");
        assertValid("\\p{sc=Unknown}");
        assertValid("\\p{sc=Zzzz}");
        assertValid("\\p{sc=Qaai}");
        assertValid("\\p{sc=Qaac}");
        assertNotValid("\\p{sc=Nope}");
        assertNotValid("\\p{sc=GREEK}");
        assertNotValid("\\p{sc=greek}");
        assertNotValid("\\p{scx=}");
        assertNotValid("\\p{Script}");
        assertNotValid("\\p{Greek}");
        assertNotValid("\\p{Block=Basic_Latin}");
    }

    /** White_Space is the Unicode property, not the set of the \s escape. */
    @Test
    void whiteSpaceIsTheUnicodeProperty() {
        assertNoMatch("\\p{White_Space}", "\ufeff");
        assertMatches("\\p{White_Space}", "\u0085");
        assertNoMatch("\\p{space}", "\ufeff");
        assertMatches("\\p{space}", "\u0085");
        assertMatches("^\\p{White_Space}+$", "\t\n\u000b\f\r \u0085\u00a0\u1680\u2000\u200a\u2028\u2029\u202f\u205f\u3000");
        assertNoMatch("\\p{White_Space}", "\u200b");
        assertMatches("\\s", "\ufeff");
        assertNoMatch("\\s", "\u0085");
        assertMatches("\\P{White_Space}", "\ufeff");
        assertNoMatch("\\P{White_Space}", "\u0085");
    }

    /** The emoji properties, which java.util.regex has only from Java 21. */
    @Test
    void emojiPropertiesOnEveryJdk() {
        assertMatches("^\\p{Emoji}$", GRINNING_FACE);
        assertNoMatch("^\\p{Emoji}$", "a");
        assertMatches("^\\p{Emoji}$", "#");
        assertNoMatch("^\\p{Emoji}$", "p{Emoji}");
        assertMatches("^\\p{Emoji_Presentation}$", GRINNING_FACE);
        assertNoMatch("^\\p{Emoji_Presentation}$", "\u00a9");
        assertMatches("^\\p{Emoji}$", "\u00a9");
        assertMatches("^\\p{Extended_Pictographic}$", "\u00a9");
        assertMatches("^\\p{Emoji_Modifier}$", new String(Character.toChars(0x1F3FB)));
        assertMatches("^\\p{Emoji_Modifier_Base}$", new String(Character.toChars(0x1F44D)));
        assertMatches("^\\p{Emoji_Component}$", "\u200d");
        assertNoMatch("^\\P{Emoji}$", GRINNING_FACE);
    }

    /** The data is that of one Unicode version on every JDK. These code points were assigned after Unicode 13. */
    @Test
    void propertiesDoNotDependOnTheJdk() {
        // U+1F6DC WIRELESS (Unicode 15), U+1FAE9 FACE WITH BAGS UNDER EYES (Unicode 16).
        assertMatches("^\\p{Emoji}$", new String(Character.toChars(0x1F6DC)));
        assertMatches("^\\p{So}$", new String(Character.toChars(0x1FAE9)));
        // U+A7CB LATIN CAPITAL LETTER RAMS HORN (Unicode 16), U+10D50 GARAY CAPITAL LETTER A (Unicode 16).
        assertMatches("^\\p{Lu}$", "\ua7cb");
        assertMatches("^\\p{Uppercase}$", "\ua7cb");
        assertMatches("^\\p{sc=Latin}$", "\ua7cb");
        assertMatches("^\\p{sc=Garay}$", new String(Character.toChars(0x10D50)));
        assertMatches("^\\p{L}$", new String(Character.toChars(0x10D50)));
        // U+16D40 KIRAT RAI SIGN ANUSVARA and U+1CCF0 OUTLINED DIGIT ZERO (Unicode 16).
        assertMatches("^\\p{Lm}$", new String(Character.toChars(0x16D40)));
        assertMatches("^\\p{Nd}$", new String(Character.toChars(0x1CCF0)));
        // U+088F ARABIC LETTER NOON WITH RING ABOVE and U+A7CE LATIN CAPITAL LETTER PHARYNGEAL VOICED FRICATIVE
        // (Unicode 17, which also made U+0295 an Lo where it was an Ll).
        assertMatches("^\\p{Lu}$", "\ua7ce");
        assertMatches("^\\p{Lo}$", "\u0295");
        assertNoMatch("^\\p{Ll}$", "\u0295");
        assertMatches("^\\p{L}$", "\u088f");
        assertMatches("^\\p{Alphabetic}$", "\u088f");
        assertMatches("^\\p{Assigned}$", "\u088f");
        assertNoMatch("^\\p{Cn}$", "\u088f");
        assertMatches("^\\p{Cn}$", "\u0378");
        assertMatches("^\\p{sc=Unknown}$", "\u0378");
    }

    /** General_Category values under each of their names. */
    @Test
    void generalCategories() {
        assertMatches("^\\p{L}\\p{Letter}\\p{gc=L}\\p{General_Category=Letter}$", "aaaa");
        assertMatches("^\\p{LC}\\p{Cased_Letter}$", "aA");
        assertNoMatch("^\\p{LC}$", "\u00aa");
        assertMatches("^\\p{Lo}$", "\u00aa");
        assertMatches("^\\p{M}\\p{Mark}\\p{Combining_Mark}$", "\u0301\u0301\u0301");
        assertMatches("^\\p{digit}\\p{Nd}\\p{Decimal_Number}$", "123");
        assertMatches("^\\p{punct}\\p{P}\\p{Punctuation}$", "!!!");
        assertMatches("^\\p{cntrl}\\p{Cc}\\p{Control}$", "\u0001\u0001\u0001");
        assertMatches("^\\p{Cs}\\p{Surrogate}$", "\ud800\ud800");
        assertMatches("^\\p{Co}\\p{Private_Use}$", "\ue000\ue000");
        assertMatches("^\\p{C}\\p{Other}$", "\u0378\u0001");
        assertMatches("^\\p{Zs}\\p{Space_Separator}\\p{Z}\\p{Separator}$", "  \u2028\u2029");
        assertNotValid("\\p{gc=Dash}");
        assertNotValid("\\p{gc=ASCII}");
        assertNotValid("\\p{General_Category}");
        assertNotValid("\\p{L=}");
        assertNotValid("\\p{ L}");
        assertNotValid("\\p{l}");
        assertNotValid("\\p{Lu=Lu}");
        assertNotValid("\\p{sc=Greek=x}");
    }

    /** The modifier groups of ECMAScript 2025. */
    @Test
    void modifierGroups() {
        assertMatches("^(?i:abc)$", "AbC");
        assertNoMatch("^a(?i:b)c$", "AbC");
        assertMatches("^a(?i:b)c$", "aBc");
        assertMatches("^(?i:a(?-i:b)c)$", "AbC");
        assertNoMatch("^(?i:a(?-i:b)c)$", "ABC");
        assertMatches("^(?i:[a-c]+)$", "AbC");
        assertNoMatch("^(?i:[^a-c]+)$", "A");
        // With the u flag grammar, case-insensitive matching is Unicode simple case folding.
        assertMatches("^(?i:k)$", "\u212a");
        assertMatches("^(?i:\u00df)$", "\u1e9e");
        assertNoMatch("^(?i:i)$", "\u0131");
        assertNoMatch("^(?i:i)$", "\u0130");
        assertMatches("^(?i:\u03c3)$", "\u03c2");
        assertMatches("^(?i:\\w)$", "\u017f");
        assertNoMatch("^\\w$", "\u017f");
        assertMatches("(?i:\\b)\u212a", "\u212a");
        // Dot-all and multiline.
        assertNoMatch("^.$", "\n");
        assertMatches("^(?s:.)$", "\n");
        assertMatches("^(?s:.)$", "\u2028");
        assertNoMatch("^(?s:(?-s:.))$", "\n");
        assertNoMatch("a$", "a\nb");
        assertMatches("(?m:a$)", "a\nb");
        assertMatches("(?m:^b)", "a\nb");
        assertMatches("(?m:^b)", "a\rb");
        assertMatches("(?m:^b)", "a\u2028b");
        assertNoMatch("(?m:^b)", "a\u0085b");
        assertMatches("\\r(?m:^)\\n", "\r\n");
        assertMatches("\\n(?m:^$)", "a\n");
        assertMatches("^(?ims:A.b$)", "a\nB\n");
        for (String bad : new String[] {"(?i)a", "(?-:a)", "(?ii:a)", "(?i-i:a)", "(?x:a)", "(?-i-s:a)", "(?i", "(?i:a",
            "(?u:a)", "(?g:a)"}) {
            assertNotValid(bad);
            assertNull(EcmaRegex.translate(bad), bad);
        }
        for (String good : new String[] {"(?i:a)", "(?-i:a)", "(?i-:a)", "(?ims:a)", "(?i-ms:a)", "(?s-i:a)", "(?m-is:a)"}) {
            assertValid(good);
        }
    }

    /** A group name may be shared by groups in separate alternatives, and a backreference finds the one that matched. */
    @Test
    void duplicateGroupNames() {
        assertMatches("^(?:(?<a>x)|(?<a>y))\\k<a>$", "xx");
        assertMatches("^(?:(?<a>x)|(?<a>y))\\k<a>$", "yy");
        assertNoMatch("^(?:(?<a>x)|(?<a>y))\\k<a>$", "xy");
        assertNoMatch("^(?:(?<a>x)|(?<a>y))\\k<a>$", "x");
        assertMatches("^(?:(?<a>a)|(?<a>b))(?:(?<b>a)|(?<b>b))\\k<a>\\k<b>$", "abab");
        assertNoMatch("^(?:(?<a>a)|(?<a>b))(?:(?<b>a)|(?<b>b))\\k<a>\\k<b>$", "abba");
        assertMatches("^(?<y>\\d{4})-\\d\\d|\\d\\d-(?<y>\\d{4})$", "12-2026");
        assertValid("(?<a>x)|(?<a>y)|(?<a>z)");
        assertValid("(?<a>x)|(?:(?<a>y))");
        assertNotValid("(?<a>x)(?<a>y)");
        assertNotValid("(?<a>(?<a>x))");
        assertNotValid("(?<a>x)|((?<a>y)(?<a>z))");
        assertNotValid("(?:(?<a>x)|(?<b>y))(?<a>z)");
        assertNotValid("(?:(?<a>x)|b)(?:(?<a>y)|c)");
        assertNull(EcmaRegex.translate("(?<a>x)(?<a>y)"));
    }

    /** A character of a group name may be written as a \\u escape. */
    @Test
    void unicodeEscapesInGroupNames() {
        assertMatches("^(?<\\u0061>a)\\k<a>$", "aa");
        assertMatches("^(?<a>a)\\k<\\u0061>$", "aa");
        assertMatches("^(?<\\u{61}b>a)\\k<ab>$", "aa");
        assertMatches("^(?<\u03c0>a)\\k<\\u03c0>$", "aa");
        assertValid("(?<\\u{1d4d1}>a)");
        assertValid("(?<\\ud835\\udcd1>a)");
        assertValid("(?<a\\u0030>a)\\k<a0>");
        assertNotValid("(?<\\u0030>a)");
        assertNotValid("(?<a\\u0020>a)");
        assertNotValid("(?<\\x61>a)");
        assertNotValid("(?<\\u>a)");
        assertNotValid("(?<\\u{}>a)");
        assertNotValid("(?<a>a)\\k<b>");
    }

    /** A lookbehind of any length, which java.util.regex runs only when it can bound the length. */
    @Test
    void lookbehindOfAnyLength() {
        assertMatches("(?<=a*)b", "aab");
        assertMatches("(?<=^a*)b", "aab");
        assertNoMatch("(?<=^a*)b", "cab");
        assertMatches("(?<!^a*)b", "cab");
        assertNoMatch("(?<!^a*)b", "aab");
        // java.util.regex compiles this one and answers false.
        assertMatches("(?<=(\\d+)(\\d+))$", "2015");
        assertNoMatch("(?<=(\\d+)(\\d+))$", "x5");
        assertMatches("(?<=(?:ab)+)x", "ababx");
        assertNoMatch("(?<=^(?:ab)+)x", "abax");
        assertMatches("(?<=(?:a|ab){2})c", "aabc");
        assertMatches("(?<=\\b\\w+)\\W", "ab!");
        assertMatches("(?<=\u00e9.*)a", "\u00e9xxa");
        assertNoMatch("(?<=\u00e9.*)a", "\u00e9x\na");
        // A lookbehind counts characters, not UTF-16 code units.
        assertMatches("(?<=^.{2})a", GRINNING_FACE + "xa");
        assertNoMatch("(?<=^.{2})a", GRINNING_FACE + "a");
        assertMatches("(?<=^.)a", GRINNING_FACE + "a");
        assertNoMatch("(?<=[^" + GRINNING_FACE + "]b)", GRINNING_FACE + "b");
        assertMatches("(?<=" + GRINNING_FACE + ".?)a", GRINNING_FACE + "a");
        assertMatches("(?<!" + GRINNING_FACE + ".*)a", "xa");
        assertNoMatch("(?<!" + GRINNING_FACE + ".*)a", GRINNING_FACE + "xa");
        assertMatches("(?<=(?<!b)a)a", "aa");
        assertMatches("(?<=^(?:a|bc)*)d", "abcad");
    }

    /** Backreferences mean what ECMA-262 says, or the pattern is refused. */
    @Test
    void backreferences() {
        assertMatches("^(a)\\1$", "aa");
        assertMatches("^(a|b|c)+?\\1$", "abb");
        assertNoMatch("^(a|b|c)+?\\1$", "ab");
        assertMatches("^(\u00e9|" + GRINNING_FACE + ")+\\1$", "\u00e9" + GRINNING_FACE + GRINNING_FACE);
        // A group that did not take part matches the empty string.
        assertMatches("^(a)?\\1b$", "b");
        assertMatches("^(?:(a)|b)\\1$", "b");
        assertMatches("^\\1(a)$", "a");
        assertMatches("^(a\\1)$", "a");
        // The groups of a negative lookahead are never set outside it.
        assertMatches("(?!(a))\\1b", "ab");
        // ECMA-262 unsets the groups of a repeated body when an iteration starts.
        assertMatches("^(?:(a)|b)*\\1$", "ab");
        assertNoMatch("^(?:(a)|b)*\\1$", "ba");
        assertMatches("^(?:(a)|b)*\\1$", "baa");
        assertMatches("^(?:(a)|(b))+\\1\\2$", "abb");
        assertNoMatch("^(?:(a)|(b))+\\1\\2$", "abab");
        assertMatches("^(?:(?<a>x)|(?<a>y))+\\k<a>$", "xyy");
        assertNoMatch("^(?:(?<a>x)|(?<a>y))+\\k<a>$", "xyx");
        // A digit after a backreference is not part of its number.
        assertMatches("^(a)\\1(?:0)$", "aa0");
        assertMatches("^(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\\10$", "abcdefghijj");
        // With one group, \10 is not a reference. The grammar with no flag reads it as the octal escape of U+0008.
        assertMatches("^(a)\\10$", "a\b");
    }

    /** A pattern that is valid and whose backreference java.util.regex cannot give the meaning of is refused. */
    @Test
    void refusedPatterns() {
        String[][] refused = {
            {"(?<=\\1(a))b", EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND},
            {"(?<=(a|b)+?)c\\1", EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND},
            {"^(?:(?:(a)|b)\\1)+$", EcmaRegex.REFUSED_GROUP_IN_REPETITION},
            {"^(?:(?=(a)))?\\1b", EcmaRegex.REFUSED_GROUP_IN_LOOKAROUND},
            {"^(a*)+\\1b", EcmaRegex.REFUSED_GROUP_IN_EMPTY_REPETITION},
            {"^(.)(?i:\\1)$", EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE},
            {"^(?i:([a-z]+)-\\1)$", EcmaRegex.REFUSED_CASE_INSENSITIVE_BACKREFERENCE},
        };
        for (String[] r : refused) {
            assertEquals(r[1], EcmaRegex.unsupported(r[0]), r[0]);
            assertNull(EcmaRegex.translate(r[0]), r[0]);
            assertTrue(EcmaRegex.isValid(r[0]), r[0]);
        }
        // A case-insensitive backreference is exact where the group can hold no character that folds differently.
        assertMatches("^(?i:([0-9]+)-\\1)$", "12-12");
        assertMatches("^(?i:([a-j]+)-\\1)$", "abc-ABC");
        assertNoMatch("^(?i:([a-j]+)-\\1)$", "abc-ABD");
        assertMatches("^([a-z]+)-(?i:\\1)\\&?$", "sky-SKY");
        assertNoMatch("^([a-z]+)-(?i:\\1)\\&?$", "sky-S\u212aY");
        // An invalid pattern is not a refused one.
        assertNull(EcmaRegex.unsupported("(a"));
    }

    /** Smaller points on which the translator differed from V8. */
    @Test
    void otherDivergences() {
        // Leading zeros in a \\u{...} escape.
        assertMatches("^\\u{0000000061}$", "a");
        assertNotValid("\\u{110000}");
        assertNotValid("\\u{00000000110000}");
        // Without the u flag grammar \\p is the letter p.
        assertMatches("^\\p\\&$", "p&");
    }

    /**
     * A lookbehind against its definition: it holds at a position when its body matches some stretch of the text
     * that ends there. The bodies are bounded and unbounded, with and without alternatives, and the texts hold a
     * character beyond the Basic Multilingual Plane.
     */
    @Test
    void lookbehindAgreesWithItsDefinition() {
        String[] pieces = {
            "a", "b", ".", "[^a]", "a*", "b+", ".?", "(?:ab|a)", "(?:a|bc){2}", "(?:ab|b)+", "\\b", "^", "(?:^|b)",
            GRINNING_FACE, ".{1,2}", "[^" + GRINNING_FACE + "]", "(?=b)", "(?<!a)", "\\n", "(?:a|)",
        };
        String alphabet = "ab\n" + GRINNING_FACE + " ";
        Random random = new Random(5);
        for (int round = 0; round < 1500; round++) {
            StringBuilder body = new StringBuilder();
            for (int k = 1 + random.nextInt(3); k > 0; k--) {
                body.append(pieces[random.nextInt(pieces.length)]);
            }
            boolean negative = random.nextBoolean();
            String pattern = (negative ? "(?<!" : "(?<=") + body + ")c";
            Pattern translated = Pattern.compile(EcmaRegex.translate(pattern));
            Pattern whole = Pattern.compile(EcmaRegex.translate(body.toString()));
            for (int t = 0; t < 12; t++) {
                StringBuilder text = new StringBuilder();
                for (int k = random.nextInt(6); k > 0; k--) {
                    text.appendCodePoint(alphabet.codePointAt(alphabet.offsetByCodePoints(0, random.nextInt(5))));
                }
                text.append('c');
                if (random.nextBoolean()) {
                    text.append("ac");
                }
                String s = text.toString();
                boolean expected = false;
                for (int i = s.indexOf('c'); i >= 0 && !expected; i = s.indexOf('c', i + 1)) {
                    boolean behind = false;
                    for (int j = i; j >= 0 && !behind; j--) {
                        if (j > 0 && j < s.length() && Character.isLowSurrogate(s.charAt(j))) {
                            continue;
                        }
                        behind = whole.matcher(s).region(j, i).useTransparentBounds(true).useAnchoringBounds(false)
                                .matches();
                    }
                    expected = behind != negative;
                }
                String shown = s.replace("\n", "\\n");
                assertEquals(expected, translated.matcher(s).find(), () -> pattern + " on " + shown);
            }
        }
    }

    /** A pattern nested too deeply to read is refused and is not an error of the Java stack. */
    @Test
    void deepNesting() {
        String deep = "(".repeat(100_000) + ")".repeat(100_000);
        assertFalse(EcmaRegex.isValid(deep));
        assertNull(EcmaRegex.translate(deep));
        String shallow = "(?:".repeat(150) + "a" + ")".repeat(150);
        assertValid(shallow);
        assertEquals(true, matches(shallow, "a"));
    }
}
