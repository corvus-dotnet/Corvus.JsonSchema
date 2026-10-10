package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.ArrayList;
import java.util.List;
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

    /**
     * The texts of up to {@code max} characters over an alphabet, the shortest first, and then some more. These are
     * the texts the tables of answers below were made with.
     */
    private static List<String> textsOver(int max, String[] alphabet, String... more) {
        List<String> texts = new ArrayList<>(List.of(""));
        for (int length = 0, from = 0; length < max; length++) {
            int end = texts.size();
            for (int k = from; k < end; k++) {
                for (String c : alphabet) {
                    texts.add(texts.get(k) + c);
                }
            }
            from = end;
        }
        texts.addAll(List.of(more));
        return texts;
    }

    private static String json(String text) {
        StringBuilder sb = new StringBuilder("\"");
        for (int k = 0; k < text.length(); k++) {
            sb.append(String.format("\\u%04x", (int) text.charAt(k)));
        }
        return sb.append('"').toString();
    }

    /**
     * Checks a table of patterns, each with the texts it matches, every one preceded by "|", against the
     * translation and against a validator of {@code {"pattern": ...}}, on every one of the texts. The answers of the
     * tables are V8's: {@code new RegExp(pattern, "u").test(text)}, or without the flag for a pattern that is not
     * valid with it.
     */
    private static void assertAnswers(String[][] cases, List<String> texts) {
        List<String> wrong = new ArrayList<>();
        for (String[] c : cases) {
            List<String> expected = c[1].isEmpty() ? List.of() : List.of(c[1].substring(1).split("\\|", -1));
            String translation = EcmaRegex.translate(c[0]);
            if (translation == null) {
                wrong.add(c[0] + " is refused: " + EcmaRegex.unsupported(c[0]));
                continue;
            }
            Pattern translated = Pattern.compile(translation);
            Validator validator = Validator.compile("{\"pattern\": " + json(c[0]) + "}");
            for (String text : texts) {
                boolean matches = expected.contains(text);
                if (translated.matcher(text).find() != matches) {
                    wrong.add(c[0] + (matches ? " does not match " : " matches ") + text);
                }
                if (validator.isValid(json(text)) != matches) {
                    wrong.add("{\"pattern\": \"" + c[0] + "\"} is " + (matches ? "not " : "") + "valid for " + text);
                }
            }
        }
        assertEquals(List.of(), wrong);
    }

    /** Backreferences to groups of repetitions, of alternatives and of lookaheads, with the texts each matches. */
    private static final String[][] BACKREFERENCES_ON_PATHS = {
            {"^(?:(a)?\\1)*x$", "|x|aax"},
            {"^(?:(a)?\\1){2}x$", "|x|aax"},
            {"^(?:(a)?\\1){2,3}?x$", "|x|aax"},
            {"^(?:(a)|x)*\\1$", "||x|aa|ax|xx|aaa|aax|axx|xaa|xax|xxx|aaaa|xxxx"},
            {"^(?:(a)|())*\\1\\2$", "||aa|aaa|aaaa"},
            {"^(?:(a)|()){2,3}\\1\\2-$", "|-|a-|aa-"},
            {
                "^(?:a|()|x)*\\1-",
                "|-|a-|x-|-a|-x|--|aa-|ax-|a-a|a-x|a--|xa-|xx-|x-a|x-x|x--|-aa|-ax|-a-|-xa|-xx"
                        + "|-x-|--a|--x|---",
            },
            {"(\\b){0,}(a\\1){3}$", "|aaa|aaaa"},
            {
                "(x*){0,}(?:a|\\1){2,3}$",
                "||a|x|-|aa|ax|a-|xa|xx|x-|-a|-x|--|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax"
                        + "|xa-|xxa|xxx|xx-|x-a|x-x|x--|-aa|-ax|-a-|-xa|-xx|-x-|--a|--x|---|aaaa|xxxb"
                        + "|xaxbb|aaxa|axxa|xxxx",
            },
            {
                "(?:^|(|[ax]){2}\\1){0,}a",
                "|a|aa|ax|a-|xa|-a|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax|xa-|xxa|x-a|-aa"
                        + "|-ax|-a-|-xa|--a|aaaa|xaxbb|aaxa|axxa",
            },
            {"^(a*)+\\1x", "|x|xa|xx|x-|aax|xaa|xax|xa-|xxa|xxx|xx-|x-a|x-x|x--|xxxb|xaxbb|aaxa|xxxx"},
            {"^(a*)*\\1x$", "|x|aax"},
            {"^(?:(a*)x)+\\1$", "|x|xx|axa|axx|xxx|xxxx"},
            {"^(?:(a)|(x))+\\1\\2$", "|aa|xx|aaa|axx|xaa|xxx|aaaa|xxxx"},
            {"^(?:x(.)){1,2}\\1", "|xaa|xxx|x--|xxxb|xaxbb|xxxx"},
            {"^(?:(?:x(.)){2})?\\1$", "||xaxbb"},
            {
                "(a(){1})?\\2a",
                "|a|aa|ax|a-|xa|-a|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax|xa-|xxa|x-a|-aa"
                        + "|-ax|-a-|-xa|--a|aaaa|xaxbb|aaxa|axxa",
            },
            {
                "(?:([^a]x\\1){1,1}|(?:\\1){2,3})?\\1$",
                "||a|x|-|aa|ax|a-|xa|xx|x-|-a|-x|--|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax"
                        + "|xa-|xxa|xxx|xx-|x-a|x-x|x--|-aa|-ax|-a-|-xa|-xx|-x-|--a|--x|---|aaaa|xxxb"
                        + "|xaxbb|aaxa|axxa|xxxx",
            },
            {"^(?:(?=(a)))?\\1x", "|x|xa|xx|x-|xaa|xax|xa-|xxa|xxx|xx-|x-a|x-x|x--|xxxb|xaxbb|xxxx"},
            {"^(?:(?=(a)))?\\1a", "|a|aa|ax|a-|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|aaaa|aaxa|axxa"},
            {"(?:(?=(?<!a)|(x){2})\\1|(?<!^)){0}?((?<=a)-){2,2}x", ""},
            {"^(?:|($|)?\\1){2,3}?a", "|a|aa|ax|a-|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|aaaa|aaxa|axxa"},
            {
                "(?:\\Bx|((?<!)|)??\\1){4,}x",
                "|x|ax|xa|xx|x-|-x|aax|axa|axx|ax-|a-x|xaa|xax|xa-|xxa|xxx|xx-|x-a|x-x|x--|-ax"
                        + "|-xa|-xx|-x-|--x|xxxb|xaxbb|aaxa|axxa|xxxx",
            },
            {"^(?:(a|x)\\1?)+-$", "|a-|x-|aa-|ax-|xa-|xx-"},
            {"^(?:(?:(a)|x)\\1-?){2,}$", "|xx|aax|xaa|xxx|xx-|x-x|aaaa|xxxx"},
            {
                "^(?:(a)|(x)|-)*(?:\\1x|\\2a)$",
                "|a|x|aa|xx|-a|-x|aaa|aax|axx|a-a|a-x|xaa|xxa|xxx|x-a|x-x|-aa|-xx|--a|--x|aaaa"
                        + "|axxa|xxxx",
            },
            {"^(?:(a+)|x){2}\\1$", "|ax|xx|aaa|aax|xaa|aaaa"},
            {"^(a|x(a)?){2}\\2-$", "|aa-|ax-|xa-|xx-"},
            {"^(?:(?=(a)|x).)\\1$", "|x|aa"},
            {
                "^(?:(?=|(a)).)+\\1$",
                "|a|x|-|aa|ax|a-|xa|xx|x-|-a|-x|--|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax"
                        + "|xa-|xxa|xxx|xx-|x-a|x-x|x--|-aa|-ax|-a-|-xa|-xx|-x-|--a|--x|---|aaaa|xxxb"
                        + "|xaxbb|aaxa|axxa|xxxx",
            },
            {
                "(?:(?=|(a|).|)){3,6}\\1a",
                "|a|aa|ax|a-|xa|-a|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax|xa-|xxa|x-a|-aa"
                        + "|-ax|-a-|-xa|--a|aaaa|xaxbb|aaxa|axxa",
            },
            {"^(x|\\1(?=(|a)|)){1,3}\\1\\2$", "||xx|xxx|xxxx"},
    };

    /**
     * A backreference reads what its group captured on the way the match took, or nothing. ECMA-262 unsets the
     * groups of a repeated body when an iteration starts, and does not take an iteration past the minimum that
     * matched nothing. {@code java.util.regex} keeps the groups of an earlier iteration, takes the empty iteration,
     * and keeps the groups of an iteration it has given back when the body has one way to match.
     */
    @Test
    void aBackreferenceReadsWhatItsGroupHoldsOnThePathOfTheMatch() {
        assertAnswers(BACKREFERENCES_ON_PATHS,
                textsOver(3, new String[] {"a", "x", "-"}, "aaaa", "xxxb", "xaxbb", "aaxa", "axxa", "xxxx"));
    }

    /** Lookbehinds with groups that are read after them, or with backreferences to their own groups. */
    private static final String[][] BACKREFERENCES_AND_LOOKBEHINDS = {
            {"(?<=\\1(a))b", "|aab|aaab|aaba|aabb|aabx|baab|xaab|aabx|aaxaab|baabx|aabbx"},
            {
                "(?<=\\1(.))a",
                "|aaa|bba|xxa|aaaa|aaab|aaax|abba|axxa|baaa|bbaa|bbab|bbax|bbba|bxxa|xaaa|xbba"
                        + "|xxaa|xxab|xxax|xxxa",
            },
            {
                "(?<!\\1(.))a$",
                "|a|aa|ba|xa|aba|axa|baa|bxa|xaa|xba|aaba|aaxa|abaa|abxa|axaa|axba|baba|baxa"
                        + "|bbaa|bbxa|bxaa|bxba|xaba|xaxa|xbaa|xbxa|xxaa|xxba|xaaxa|abxba|xabxba",
            },
            {
                "(\\w)(?<=\\1\\1)x",
                "|aax|bbx|xxx|aaax|aaxa|aaxb|aaxx|abbx|axxx|baax|bbbx|bbxa|bbxb|bbxx|bxxx|xaax"
                        + "|xbbx|xxxa|xxxb|xxxx|bbbx|xaaxa|aaxaab|aabbx",
            },
            {"(?<=\\1\\1(.))x", "|aaax|bbbx|xxxx|bbbx"},
            {"(?<=(a)(b))x\\2\\1", "|abxba|xabxba"},
            {"(?<=\\2\\1(a)(b))x", "|baabx"},
            {"(?<=(?:\\1(.)){2})x$", "|aabbx"},
            {
                "(?<=([ab])+)x\\1",
                "|axa|bxb|aaxa|abxa|axaa|axab|axax|baxb|bbxb|bxba|bxbb|bxbx|xaxa|xbxb|xaaxa"
                        + "|aaxaab|axab|abxab",
            },
            {
                "(?<=([ab])*)x\\1",
                "|x|xa|xb|xx|axa|axx|bxb|bxx|xaa|xab|xax|xba|xbb|xbx|xxa|xxb|xxx|aaxa|aaxx|abxa"
                        + "|abxx|axaa|axab|axax|axxa|axxb|axxx|baxb|baxx|bbxb|bbxx|bxba|bxbb|bxbx|bxxa"
                        + "|bxxb|bxxx|xaaa|xaab|xaax|xaba|xabb|xabx|xaxa|xaxb|xaxx|xbaa|xbab|xbax|xbba"
                        + "|xbbb|xbbx|xbxa|xbxb|xbxx|xxaa|xxab|xxax|xxba|xxbb|xxbx|xxxa|xxxb|xxxx|xaaxa"
                        + "|aaxaab|axab|abxab|xabxba",
            },
            {
                "(?<=([ab]){1,2})x\\1",
                "|axa|bxb|aaxa|abxa|axaa|axab|axax|baxb|bbxb|bxba|bxbb|bxbx|xaxa|xbxb|xaaxa"
                        + "|aaxaab|axab|abxab",
            },
            {
                "(?<=([ab])+?)x\\1",
                "|axa|bxb|aaxa|abxb|axaa|axab|axax|baxa|bbxb|bxba|bxbb|bxbx|xaxa|xbxb|xaaxa"
                        + "|aaxaab|axab|abxba|xabxba",
            },
            {"(?<=([ab]){2})x\\1", "|aaxa|abxa|baxb|bbxb|xaaxa|aaxaab|abxab"},
            {
                "(?<=(a|b)+?)x\\1",
                "|axa|bxb|aaxa|abxb|axaa|axab|axax|baxa|bbxb|bxba|bxbb|bxbx|xaxa|xbxb|xaaxa"
                        + "|aaxaab|axab|abxba|xabxba",
            },
            {
                "(?<=(?<n>a)|(?<n>b))\\k<n>x",
                "|aax|bbx|aaax|aaxa|aaxb|aaxx|abbx|baax|bbbx|bbxa|bbxb|bbxx|xaax|xbbx|bbbx"
                        + "|xaaxa|aaxaab|aabbx",
            },
            {"(?<=$|(|a)\\b)x\\1a", "|xa|xaa|xab|xax|xaaa|xaab|xaax|xaba|xabb|xabx|xaxa|xaxb|xaxx|xaaxa|xabxba"},
            {
                "(?<=(a*))x\\1b",
                "|xb|bxb|xba|xbb|xbx|xxb|abxb|axab|axxb|bbxb|bxba|bxbb|bxbx|bxxb|xbaa|xbab|xbax"
                        + "|xbba|xbbb|xbbx|xbxa|xbxb|xbxx|xxba|xxbb|xxbx|xxxb|aaxaab|axab|abxba|xabxba",
            },
            {"(?<=(a|ab))x\\1$", "|axa|aaxa|baxa|xaxa|xaaxa|abxab"},
            {"(?<=^a*(b))x\\1$", "|bxb|abxb"},
            {"(?<=(a)x|(b)x)\\1\\2$", "|axa|bxb|aaxa|abxb|baxa|bbxb|xaxa|xbxb|xaaxa"},
            {"(?<=(?:(a)|(b))x)\\1\\2$", "|axa|bxb|aaxa|abxb|baxa|bbxb|xaxa|xbxb|xaaxa"},
            // A repetition first in the lookbehind, of a body that holds a counted group with alternatives.
            {"(?<=(?:(a|b)(?:x|b){2})+)\\1", "|abba|abxa|axba|axxa|bbbb|bbxb|bxbb|bxxb|abxab"},
            {"(?<=(?:(?:x|b){2}(a))+)\\1", "|bbaa|bxaa|xbaa|xxaa"},
            {
                "(?<=(?:(a)(?:x|b){2})*)\\1$",
                "||a|b|x|aa|ab|ax|ba|bb|bx|xa|xb|xx|aaa|aab|aax|aba|axa|baa|bab|bax|bba|bbb|bbx"
                        + "|bxa|bxb|bxx|xaa|xab|xax|xba|xbb|xbx|xxa|xxb|xxx|aaaa|aaab|aaax|aaba|aaxa|abaa"
                        + "|abab|abax|abba|abbb|abbx|abxa|abxb|abxx|axaa|axab|axax|axba|axbb|axbx|axxa"
                        + "|axxb|axxx|baaa|baab|baax|baba|baxa|bbaa|bbab|bbax|bbba|bbbb|bbbx|bbxa|bbxb"
                        + "|bbxx|bxaa|bxab|bxax|bxba|bxbb|bxbx|bxxa|bxxb|bxxx|xaaa|xaab|xaax|xaba|xaxa"
                        + "|xbaa|xbab|xbax|xbba|xbbb|xbbx|xbxa|xbxb|xbxx|xxaa|xxab|xxax|xxba|xxbb|xxbx"
                        + "|xxxa|xxxb|xxxx|abax|bbbx|xaaxa|aaxaab|axab|abxba|aabbx|abxab|xabxba",
            },
            {"(?<=(?:(a)(?:x|b){2})+b)\\1", "|abxba|xabxba"},
    };

    private static final String[] MORE_FOR_LOOKBEHINDS = {
        "aabx", "abax", "ababx", "bbbx", "xaaxa", "aaxaab", "axab", "abxba", "baabx", "aabbx", "abxab", "xabxba"
    };

    /**
     * ECMA-262 runs a lookbehind backwards: a backreference in it reads a group that stands after it, and a group
     * of a repetition in it holds the leftmost iteration.
     */
    @Test
    void aLookbehindCapturesAsIfRunBackwards() {
        assertAnswers(BACKREFERENCES_AND_LOOKBEHINDS, textsOver(4, new String[] {"a", "b", "x"}, MORE_FOR_LOOKBEHINDS));
    }

    /**
     * Lookaheads whose groups are read after them, and that set them on some of the ways they can match only. A
     * lookahead takes the first way its body matches, in the order ECMA-262 tries them.
     */
    private static final String[][] LOOKAHEADS_BY_WAYS = {
            {
                "(?=(a)?b)\\1",
                "|b|ab|ba|bb|bc|cb|aab|aba|abb|abc|acb|baa|bab|bac|bba|bbb|bbc|bca|bcb|bcc|cab"
                        + "|cba|cbb|cbc|ccb|aaab|aaba|aabb|aabc|aacb|abaa|abab|abac|abba|abbb|abbc|abca"
                        + "|abcb|abcc|acab|acba|acbb|acbc|accb|baaa|baab|baac|baba|babb|babc|baca|bacb"
                        + "|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb"
                        + "|bcbc|bcca|bccb|bccc|caab|caba|cabb|cabc|cacb|cbaa|cbab|cbac|cbba|cbbb|cbbc"
                        + "|cbca|cbcb|cbcc|ccab|ccba|ccbb|ccbc|cccb|acbcb|acbccb|abcabc|aabbaa|acacab"
                        + "|bcbcab|abababa|ababcab|axbba|abxba",
            },
            {
                "(?=(?:(a)|b)c)\\1",
                "|ac|bc|aac|abc|aca|acb|acc|bac|bbc|bca|bcb|bcc|cac|cbc|aaac|aabc|aaca|aacb"
                        + "|aacc|abac|abbc|abca|abcb|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc"
                        + "|baac|babc|baca|bacb|bacc|bbac|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb"
                        + "|bcbc|bcca|bccb|bccc|caac|cabc|caca|cacb|cacc|cbac|cbbc|cbca|cbcb|cbcc|ccac"
                        + "|ccbc|acaca|acbcb|acbccb|abcabc|acacab|bcbcab|ababcab",
            },
            {"^(?=[bc]*(c)?)b\\1$", "|b"},
            {
                "^(?=(?:b|bc)*(c)?)\\1",
                "||a|b|c|aa|ab|ac|ba|bb|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab|aaac|aaba|aabb|aabc"
                        + "|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb|abcc|acaa|acab|acac"
                        + "|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc"
                        + "|bbaa|bbab|bbac|bbba|bbbb|caaa|caab|caac|caba|cabb|cabc|caca|cacb|cacc|cbaa"
                        + "|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb|ccbc|ccca"
                        + "|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(a)*?b?)\\1",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa"
                        + "|ababcab|axbba|abxba",
            },
            {
                "^(?=(a*)*?(b)?)\\2\\1",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa"
                        + "|ababcab|axbba|abxba",
            },
            {"^(?=((?:c??)?))\\1$", "||c"},
            {"^(?=((?:|c)*))\\1$", "||c|cc|ccc|cccc"},
            {
                "^(?=(a)?(?:(b)|c)*?\\1)\\2",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|abb|abc|aca|acb|acc|baa|bab|bac"
                        + "|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab|aaac"
                        + "|aaba|aabb|aabc|aaca|aacb|aacc|abbb|abbc|abca|abcb|abcc|acaa|acab|acac|acbb"
                        + "|acbc|acca|accb|accc|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab"
                        + "|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb"
                        + "|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb|cacc|cbaa|cbab|cbac|cbba|cbbb"
                        + "|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb|ccbc|ccca|cccb|cccc|acaca|acbcb"
                        + "|acbccb|abcabc|aabbaa|acacab|bcbcab|axbba|abxba",
            },
            {"^(?=(?:(a)|(b))+?c?)\\1\\2$", "|a|b"},
            {
                "^(?=((?:a|ab)*)c?\\1(b)?)\\2",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cc|aaa|aac|aba|abb|abc|aca|acb|acc|baa|bab|bac"
                        + "|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cca|ccb|ccc|aaaa|aaab|aaac|aaca|aacb|aacc"
                        + "|abaa|abab|abac|abba|abbb|abbc|abca|abcb|abcc|acaa|acac|acba|acbb|acbc|acca"
                        + "|accb|accc|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba"
                        + "|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa"
                        + "|caab|caac|caba|cabb|cabc|caca|cacb|cacc|ccaa|ccab|ccac|ccba|ccbb|ccbc|ccca"
                        + "|cccb|cccc|acaca|acbcb|acbccb|abcabc|acacab|bcbcab|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:((?:a|ab)*)c)*\\1(b)?)\\2.",
                "|a|c|aa|ab|ac|ba|bb|bc|ca|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab|bac"
                        + "|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cca|ccc|aaaa|aaab|aaac|aaba|aabb|aabc"
                        + "|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb|abcc|acaa|acac|acba"
                        + "|acbb|acbc|acca|accc|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab"
                        + "|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb"
                        + "|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb|cacc|ccaa|ccab|ccac|ccca|cccc"
                        + "|acaca|acbcb|acbccb|abcabc|aabbaa|bcbcab|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:a(b)?)*c)a\\1",
                "|ac|aac|abc|aca|acb|acc|aaac|aaca|aacb|aacc|abac|abca|abcb|abcc|acaa|acab|acac"
                        + "|acba|acbb|acbc|acca|accb|accc|acaca|acbcb|acbccb|abcabc|acacab|ababcab",
            },
            {
                "^(?=(?:(a)|b){1,2}c?)\\1",
                "|a|b|aa|ab|ac|bb|bc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|bba|bbb|bbc|bca|bcb"
                        + "|bcc|aaaa|aaab|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb"
                        + "|abbc|abca|abcb|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|bbaa|bbab"
                        + "|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb"
                        + "|bccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa|ababcab|axbba"
                        + "|abxba",
            },
            {
                "^(?=(?=(a)?)(b)?a)\\1\\2",
                "|a|aa|ab|ac|ba|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab|bac|aaaa|aaab|aaac"
                        + "|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb|abcc"
                        + "|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb|babc"
                        + "|baca|bacb|bacc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|abababa|ababcab|axbba"
                        + "|abxba",
            },
            {
                "(?=(a|ab)(?:c|(b)c))\\1\\2",
                "|ac|aac|abc|aca|acb|acc|bac|cac|aaac|aabc|aaca|aacb|aacc|abac|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baac|babc|baca|bacb|bacc"
                        + "|bbac|bcac|caac|cabc|caca|cacb|cacc|cbac|ccac|acaca|acbcb|acbccb|abcabc|acacab"
                        + "|ababcab",
            },
            {
                "^(?=(a)??(?:b|a))\\1",
                "|a|b|aa|ab|ac|ba|bb|bc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab|bac|bba|bbb"
                        + "|bbc|bca|bcb|bcc|aaaa|aaab|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac"
                        + "|abba|abbb|abbc|abca|abcb|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc"
                        + "|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc"
                        + "|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|acaca|acbcb"
                        + "|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:(a)|(ab))(?:c|bc))\\1\\2",
                "|ac|abc|aca|acb|acc|abbc|abca|abcb|abcc|acaa|acab|acac|acba|acbb|acbc|acca"
                        + "|accb|accc|acaca|acbcb|acbccb|abcabc|acacab",
            },
            {
                "^(?=(?:((?:a|ab)*)\\1c)*)\\1",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caba|cabb|cabc|caca|cacb|cacc"
                        + "|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb|ccbc"
                        + "|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa|ababcab"
                        + "|axbba|abxba",
            },
            {
                "^(?=(?:(a)|b)*?(?:c|(b))\\1)\\2a",
                "|ba|abc|aca|baa|bab|bac|aabc|aaca|abca|abcb|abcc|acaa|acab|acac|baaa|baab|baac"
                        + "|baba|babb|babc|baca|bacb|bacc|acaca|abcabc|acacab",
            },
            {
                "^(?=(a)?(?:\\1b)*(c)?)\\2",
                "||a|b|c|aa|ab|ba|bb|ca|cb|cc|aaa|aab|aac|aba|abb|abc|baa|bab|bac|bba|bbb|caa"
                        + "|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab|aaac|aaba|aabb|aaca|aacb|aacc|abaa"
                        + "|abab|abac|abba|abbb|abbc|abca|abcb|abcc|baaa|baab|baac|baba|babb|babc|baca"
                        + "|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|abcabc|aabbaa|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:(a+)|b)(?:\\1|c)*?(b)?c)\\2",
                "|ac|bc|aac|aca|acb|acc|bbc|bca|bcb|bcc|aaac|aaca|aacb|aacc|acaa|acab|acac|acba"
                        + "|acbb|acbc|acca|accb|accc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca"
                        + "|bccb|bccc|acaca|acbcb|acbccb|acacab|bcbcab",
            },
            {
                "(?=((?:.|.b))??(?:(a)|a)(?:\\1)*)\\1.",
                "|a|aa|ab|ac|ba|ca|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab|bac|bba|bca|caa"
                        + "|cab|cac|cba|cca|aaaa|aaab|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac"
                        + "|abba|abbb|abbc|abca|abcb|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc"
                        + "|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbca|bcaa"
                        + "|bcab|bcac|bcba|bcca|caaa|caab|caac|caba|cabb|cabc|caca|cacb|cacc|cbaa|cbab"
                        + "|cbac|cbba|cbca|ccaa|ccab|ccac|ccba|ccca|acaca|acbcb|acbccb|abcabc|aabbaa"
                        + "|acacab|bcbcab|abababa|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:((?:bb|a))|[ab])\\1[bc])\\1\\1.",
                "|ab|ac|bb|bc|aab|aac|aba|abb|abc|aca|acb|acc|bba|bbb|bbc|bca|bcb|bcc|aaba|aabb"
                        + "|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb|abcc|acaa|acab"
                        + "|acac|acba|acbb|acbc|acca|accb|accc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb"
                        + "|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|acaca|acbcb|acbccb|abcabc"
                        + "|aabbaa|acacab|bcbcab|abababa|ababcab|abxba",
            },
            {
                "a(?=((?:a|cc))*?\\1(ab|a))\\1\\2\\1",
                "|aa|aaa|aab|aac|baa|caa|aaaa|aaab|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|acaa"
                        + "|baaa|baab|baac|bbaa|bcaa|caaa|caab|caac|cbaa|ccaa|aabbaa",
            },
            {"^(?=(a*?)\\1(?:(a?)|c?))\\1\\1$\\2$", "|"},
            {
                "^(?=(?:(?:(c)|(b[bc]))|(?:([bc])|(?:[^a]|ba)))(a)?)\\1\\2\\3\\4",
                "|b|c|ba|bb|bc|ca|cb|cc|baa|bab|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb"
                        + "|cbc|cca|ccb|ccc|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab|bbac"
                        + "|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc"
                        + "|caaa|caab|caac|caba|cabb|cabc|caca|cacb|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc"
                        + "|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb|ccbc|ccca|cccb|cccc|bcbcab",
            },
            {
                "^(?=(?:(a)*(?:c|cb))*\\1b)\\1",
                "|b|ba|bb|bc|cb|baa|bab|bac|bba|bbb|bbc|bca|bcb|bcc|cba|cbb|cbc|ccb|acab|accb"
                        + "|baaa|baab|baac|baba|babb|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc"
                        + "|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|cbaa|cbab|cbac"
                        + "|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccba|ccbb|ccbc|cccb|acbcb|acbccb|acacab|bcbcab",
            },
            {
                "^(?=(?:(?:(a)|b)*?(?:c|(b))\\1)*)\\2a",
                "|a|aa|ab|ac|ba|aaa|aab|aac|abc|aca|acb|acc|baa|bab|bac|aaaa|aaab|aaac|aabc"
                        + "|aaca|aacb|aacc|abac|abbc|abca|abcc|acaa|acac|acba|acbb|acbc|acca|accb|accc"
                        + "|baaa|baab|baac|baba|babb|bacb|bacc|acaca|acbcb|acbccb|abcabc|acacab|ababcab"
                        + "|axbba|abxba",
            },
    };

    private static final String[] MORE_FOR_LOOKAHEADS = {
        "acaca", "acbcb", "acbccb", "abcabc", "aabbaa", "acacab", "bcbcab", "abababa", "ababcab", "axbba", "abxba"
    };

    /** A group that a lookahead sets on some of its ways only holds what the first way that matches gave it. */
    @Test
    void aLookaheadTakesTheFirstWayItsBodyMatches() {
        assertAnswers(LOOKAHEADS_BY_WAYS, textsOver(4, new String[] {"a", "b", "c"}, MORE_FOR_LOOKAHEADS));
    }

    /**
     * Groups of a lookaround that are read in the same iteration of a loop, past a place the match can come back to
     * after the lookaround has run again in a later iteration. java.util.regex does not restore the groups of a
     * lookaround, so the lookaround is run again before the reference.
     */
    private static final String[][] LOOKAROUND_GROUPS_IN_LOOPS = {
            {"^(?:(?=([ab]))[ab](?:c|ca)\\1)*$", "||aca|bcb|acaa|bcab"},
            {"^(?:(?=([ab]))[ab](?:c\\1|ca\\1))*$", "||aca|bcb|acaa|bcab"},
            {
                "^(?:(?=([ab]+?))(?:\\1.)*?b)+$",
                "|b|bb|aab|abb|acb|bab|bbb|bcb|aabb|abbb|acbb|baab|babb|bacb|bbab|bbbb|bbcb"
                        + "|bcbb",
            },
            {"^(?:(?=(.))(?:[bc]\\1[^a]?)+)*$", "||bb|cc|bbb|bbc|ccb|ccc|bbbb|bbcb|bbcc|ccbb|ccbc|cccc"},
            {
                "^(?:(?=([ab]c?))(?:\\1([bc]+?))*)*$",
                "||ab|bb|abb|abc|acb|acc|bbb|bbc|bcb|bcc|abab|abac|abbb|abbc|abcb|abcc|acbb"
                        + "|acbc|accb|accc|bbab|bbbb|bbbc|bbcb|bbcc|bcbb|bcbc|bccb|bccc|acbcb|acbccb"
                        + "|abcabc|bcbcab|ababcab",
            },
            {"^(?:(?=(a*))(?:b\\1(?<=\\1b?))*?)*$", "||b|bb|bbb|bbbb"},
            {"^(?:(?<=(.))(?:a\\1c?)?\\1|b)*$", "||b|bb|bbb|babb|bbbb"},
            {
                "^(?:a(?:(?=(b))b(?:c|\\1)*)*)*$",
                "||a|aa|ab|aaa|aab|aba|abb|abc|aaaa|aaab|aaba|aabb|aabc|abaa|abab|abba|abbb"
                        + "|abbc|abca|abcb|abcc|abcabc|aabbaa|abababa|ababcab",
            },
            {
                "^(?:[bc]?(?:.(?=([ab]c?))(?:c\\1)*\\1)*?b?)*$",
                "||b|c|aa|ab|ba|bb|bc|ca|cb|cc|aab|aac|abb|abc|baa|bab|bac|bba|bbb|bbc|bca|bcb"
                        + "|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab|aaba|aabb|aabc|aacb|aacc"
                        + "|abaa|abab|abba|abbb|abbc|abcb|abcc|baaa|baab|baac|baba|babb|babc|bacb|bacc"
                        + "|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab|bcac|bcba|bcbb|bcbc"
                        + "|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|cacb|cacc|cbaa|cbab|cbac|cbba"
                        + "|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb|ccbc|ccca|cccb|cccc|abcabc"
                        + "|aabbaa|bcbcab|ababcab",
            },
            {
                "^(?:b?(?:(?<=(a)[^a])a\\1\\2(?=(?:(a*?)(a)?\\1)?)(?:c*\\2c?)?\\3)?.?){2}",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa"
                        + "|ababcab|axbba|abxba",
            },
            {"(?i:^(?:(?=(a|))c\\1)*$)", "||c|cc|ccc|cccc"},
            {"^(?:(?=(a|b))(?:c*\\1)+?c)+$", "|ac|bc|aac|bbc|aaac|acac|acbc|bbbc|bcac|bcbc"},
            {"^(?:(?=(a)|b)[ab](?:c|cb)\\1)*$", "||bc|aca|bcb|acba|bcbc"},
            {"^(?:(?=(a)?)(?:b|ba)\\1c)+$", "|bc|bac|bcbc"},
            {"^(?:(?<=(a|b))c(?:a|ab)\\1)+$", ""},
            {
                "^(?=(?:(?=(a|b))[ab](?:c|ca)\\1)*)\\1",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa"
                        + "|ababcab|axbba|abxba",
            },
            {
                "^(?=(?:(?:((?:a|ab)*)\\1c)*b)*)\\1",
                "||a|b|c|aa|ab|ac|ba|bb|bc|ca|cb|cc|aaa|aab|aac|aba|abb|abc|aca|acb|acc|baa|bab"
                        + "|bac|bba|bbb|bbc|bca|bcb|bcc|caa|cab|cac|cba|cbb|cbc|cca|ccb|ccc|aaaa|aaab"
                        + "|aaac|aaba|aabb|aabc|aaca|aacb|aacc|abaa|abab|abac|abba|abbb|abbc|abca|abcb"
                        + "|abcc|acaa|acab|acac|acba|acbb|acbc|acca|accb|accc|baaa|baab|baac|baba|babb"
                        + "|babc|baca|bacb|bacc|bbaa|bbab|bbac|bbba|bbbb|bbbc|bbca|bbcb|bbcc|bcaa|bcab"
                        + "|bcac|bcba|bcbb|bcbc|bcca|bccb|bccc|caaa|caab|caac|caba|cabb|cabc|caca|cacb"
                        + "|cacc|cbaa|cbab|cbac|cbba|cbbb|cbbc|cbca|cbcb|cbcc|ccaa|ccab|ccac|ccba|ccbb"
                        + "|ccbc|ccca|cccb|cccc|acaca|acbcb|acbccb|abcabc|aabbaa|acacab|bcbcab|abababa"
                        + "|ababcab|axbba|abxba",
            },
    };

    /** A reference reads what the lookaround of its own iteration captured. */
    @Test
    void aGroupOfALookaroundIsReadAsItsIterationSetIt() {
        assertAnswers(LOOKAROUND_GROUPS_IN_LOOPS, textsOver(4, new String[] {"a", "b", "c"}, MORE_FOR_LOOKAHEADS));
    }

    /** Backreferences inside a case-insensitive group, in patterns that are valid with the u flag. */
    private static final String[][] CASE_INSENSITIVE_BACKREFERENCES = {
            {
                "^(.)(?i:\\1)$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
            {
                "^(?i:(.)\\1)$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
            {
                "^(.+)(?i:\\1)$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|isIS|ks\u212a\u017f|\u00e9s\u00c9S"
                        + "|\u00e9\u00e9|\u00e9\u00c9|\u212aSks",
            },
            {
                "^(?i:(.+)\\1)$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|isIS|ks\u212a\u017f|\u00e9s\u00c9S"
                        + "|\u00e9\u00e9|\u00e9\u00c9|\u212aSks",
            },
            {
                "^(?<n>.)(?i:\\k<n>)+$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|iii|iiI|iIi|iII|Iii|IiI|IIi|III"
                        + "|\u0130\u0130\u0130|\u0131\u0131\u0131|sss|ss\u017f|s\u017fs|s\u017f\u017f"
                        + "|\u017fss|\u017fs\u017f|\u017f\u017fs|\u017f\u017f\u017f|kkk|kk\u212a|k\u212ak"
                        + "|k\u212a\u212a|\u212akk|\u212ak\u212a|\u212a\u212ak|\u212a\u212a\u212a|---"
                        + "|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df|\ud801\udc00\ud801\udc28"
                        + "|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00|\u03c2\u03c3|\u03c3\u03a3"
                        + "|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
            {
                "^(?i:([a-z]+)-\\1)$",
                "|i-i|i-I|I-i|I-I|s-s|s-\u017f|\u017f-s|\u017f-\u017f|k-k|k-\u212a|\u212a-k"
                        + "|\u212a-\u212a",
            },
            {"^([a-z]+)-(?i:\\1)$", "|i-i|i-I|s-s|s-\u017f|k-k|k-\u212a"},
            {
                "^(?i:(?:(.)\\1)+)$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
            {"^(\\u00df)(?i:\\1)$", "|\u00df\u1e9e|\u00df\u00df"},
            {"^(\\u03c2)(?i:\\1)$", "|\u03c2\u03c3"},
            {"^(\\ud801\\udc00)(?i:\\1)$", "|\ud801\udc00\ud801\udc28|\ud801\udc00\ud801\udc00"},
            {"(?<=(?i:\\1)(.))x", "|kKx|\u212akx|k\u212ax|iIx|\u017fsx"},
            {"(?i:(?<=\\1(k)))x", "|kKx|\u212akx|k\u212ax"},
            {"(?i:(?<=\\1(..)))x$", "|ks\u212aSx|kskSx"},
            {
                "^(?=.(.))(?i:\\1)",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|s\u017f|\u017fs|\u017f\u017f|kk"
                        + "|k\u212a|\u212ak|\u212a\u212a|--|iii|iiI|ii\u0130|ii\u0131|iis|ii\u017f|iik"
                        + "|ii\u212a|ii-|iIi|iII|iI\u0130|iI\u0131|iIs|iI\u017f|iIk|iI\u212a|iI-|Iii|IiI"
                        + "|Ii\u0130|Ii\u0131|Iis|Ii\u017f|Iik|Ii\u212a|Ii-|IIi|III|II\u0130|II\u0131|IIs"
                        + "|II\u017f|IIk|II\u212a|II-|\u0130\u0130i|\u0130\u0130I|\u0130\u0130\u0130"
                        + "|\u0130\u0130\u0131|\u0130\u0130s|\u0130\u0130\u017f|\u0130\u0130k"
                        + "|\u0130\u0130\u212a|\u0130\u0130-|\u0131\u0131i|\u0131\u0131I"
                        + "|\u0131\u0131\u0130|\u0131\u0131\u0131|\u0131\u0131s|\u0131\u0131\u017f"
                        + "|\u0131\u0131k|\u0131\u0131\u212a|\u0131\u0131-|ssi|ssI|ss\u0130|ss\u0131|sss"
                        + "|ss\u017f|ssk|ss\u212a|ss-|s\u017fi|s\u017fI|s\u017f\u0130|s\u017f\u0131"
                        + "|s\u017fs|s\u017f\u017f|s\u017fk|s\u017f\u212a|s\u017f-|\u017fsi|\u017fsI"
                        + "|\u017fs\u0130|\u017fs\u0131|\u017fss|\u017fs\u017f|\u017fsk|\u017fs\u212a"
                        + "|\u017fs-|\u017f\u017fi|\u017f\u017fI|\u017f\u017f\u0130|\u017f\u017f\u0131"
                        + "|\u017f\u017fs|\u017f\u017f\u017f|\u017f\u017fk|\u017f\u017f\u212a"
                        + "|\u017f\u017f-|kki|kkI|kk\u0130|kk\u0131|kks|kk\u017f|kkk|kk\u212a|kk-"
                        + "|k\u212ai|k\u212aI|k\u212a\u0130|k\u212a\u0131|k\u212as|k\u212a\u017f|k\u212ak"
                        + "|k\u212a\u212a|k\u212a-|\u212aki|\u212akI|\u212ak\u0130|\u212ak\u0131|\u212aks"
                        + "|\u212ak\u017f|\u212akk|\u212ak\u212a|\u212ak-|\u212a\u212ai|\u212a\u212aI"
                        + "|\u212a\u212a\u0130|\u212a\u212a\u0131|\u212a\u212as|\u212a\u212a\u017f"
                        + "|\u212a\u212ak|\u212a\u212a\u212a|\u212a\u212a-|--i|--I|--\u0130|--\u0131|--s"
                        + "|--\u017f|--k|--\u212a|---|\u00df\u1e9e|\u1e9e\u00df|\u00df\u00df"
                        + "|\ud801\udc00\ud801\udc28|\ud801\udc28\ud801\udc00|\ud801\udc00\ud801\udc00"
                        + "|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9|kKx|\u212akx"
                        + "|k\u212ax|iIx|\u017fsx",
            },
            {
                "^(?=.(..))(?i:\\1)",
                "|iii|iiI|iIi|iII|Iii|IiI|IIi|III|\u0130\u0130\u0130|\u0131\u0131\u0131|sss"
                        + "|ss\u017f|s\u017fs|s\u017f\u017f|\u017fss|\u017fs\u017f|\u017f\u017fs"
                        + "|\u017f\u017f\u017f|kkk|kk\u212a|k\u212ak|k\u212a\u212a|\u212akk|\u212ak\u212a"
                        + "|\u212a\u212ak|\u212a\u212a\u212a|---",
            },
            {
                "^(?i:(.*)-\\1)$",
                "|-|i-i|i-I|I-i|I-I|\u0130-\u0130|\u0131-\u0131|s-s|s-\u017f|\u017f-s"
                        + "|\u017f-\u017f|k-k|k-\u212a|\u212a-k|\u212a-\u212a|---",
            },
            // In a lookbehind, where the backreference is tried at places before the end of its group.
            {
                "^(?i:(s*)(?<=\\1)k)$",
                "|k|\u212a|sk|s\u212a|\u017fk|\u017f\u212a|ssk|ss\u212a|s\u017fk|s\u017f\u212a"
                        + "|\u017fsk|\u017fs\u212a|\u017f\u017fk|\u017f\u017f\u212a",
            },
            {
                "(?i:(.+)(?<=\\1\\1)-)",
                "|ii-|iI-|Ii-|II-|\u0130\u0130-|\u0131\u0131-|ss-|s\u017f-|\u017fs-"
                        + "|\u017f\u017f-|kk-|k\u212a-|\u212ak-|\u212a\u212a-|---",
            },
    };

    /** The same in patterns valid only without the u flag, where a character is compared by its uppercase. */
    private static final String[][] CASE_INSENSITIVE_BACKREFERENCES_WITHOUT_THE_U_FLAG = {
            {
                "^(.)(?i:\\1)\\&?$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|\u017f\u017f|kk|\u212a\u212a|--"
                        + "|\u00df\u00df|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
            {
                "^(?i:(.+)\\1)\\&?$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|\u017f\u017f|kk|\u212a\u212a|--"
                        + "|\u00df\u00df|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|isIS|\u00e9s\u00c9S"
                        + "|\u00e9\u00e9|\u00e9\u00c9",
            },
            {"^([a-z]+)-(?i:\\1)\\&?$", "|i-i|i-I|s-s|k-k"},
            {
                "^(?i:(?:(.)\\1)+)\\&?$",
                "|ii|iI|Ii|II|\u0130\u0130|\u0131\u0131|ss|\u017f\u017f|kk|\u212a\u212a|--"
                        + "|\u00df\u00df|\u03c2\u03c3|\u03c3\u03a3|\u03a3\u03c2|\u00e9\u00e9|\u00e9\u00c9",
            },
    };

    private static final String[] CASED = {"i", "I", "\u0130", "\u0131", "s", "\u017f", "k", "\u212a", "-"};
    private static final String[] MORE_CASED = {
        "\u00df\u1e9e", "\u1e9e\u00df", "\u00df\u00df", "\u00dfs", "\ud801\udc00\ud801\udc28",
        "\ud801\udc28\ud801\udc00", "\ud801\udc00\ud801\udc00", "\ud801\udc00\ud801\udc01", "\u03c2\u03c3",
        "\u03c3\u03a3", "\u03a3\u03c2", "\u03c3\u03c4", "isIS", "is\u0130S", "ks\u212a\u017f", "\u00e9s\u00c9S",
        "\u00e9\u00e9", "\u00e9\u00c9", "x\u212a", "xk", "xK", "\u017fix", "six", "Kix", "\u0131ix", "sik\u212a",
        "\u212aSks", "kKx", "\u212akx", "k\u212ax", "iIx", "\u0130ix", "\u017fsx", "ks\u212aSx", "kskSx"
    };
    private static final String[] MORE_CASED_WITHOUT_U = {
        "\u00df\u1e9e", "\u1e9e\u00df", "\u00df\u00df", "\u00dfs", "\u03c2\u03c3", "\u03c3\u03a3", "\u03a3\u03c2",
        "\u03c3\u03c4", "isIS", "is\u0130S", "ks\u212a\u017f", "\u00e9s\u00c9S", "\u00e9\u00e9", "\u00e9\u00c9",
        "x\u212a", "xk", "xK", "\u017fix", "six", "Kix", "\u0131ix", "sik\u212a", "\u212aSks"
    };

    /**
     * A backreference inside a case-insensitive group compares by the Canonicalize of ECMA-262: simple case folding
     * with the u flag, so that "s", "S" and U+017F are one, as are "k", "K" and U+212A, and U+00DF and U+1E9E, and
     * "i" is neither U+0130 nor U+0131. Without the flag it is the uppercase of one code unit, which keeps U+017F
     * and U+212A apart from the letters. The case-insensitive backreference of {@code java.util.regex} does neither
     * (it equates "i", U+0130 and U+0131 on every JDK, and other pairs differ with the JDK), so the answers must be
     * the same on every JDK.
     */
    @Test
    void aCaseInsensitiveBackreferenceComparesAsEcma262Does() {
        assertAnswers(CASE_INSENSITIVE_BACKREFERENCES, textsOver(3, CASED, MORE_CASED));
        assertAnswers(CASE_INSENSITIVE_BACKREFERENCES_WITHOUT_THE_U_FLAG, textsOver(3, CASED, MORE_CASED_WITHOUT_U));
        // Where the group can hold no character with another case, the backreference is written as it is.
        assertEquals("^(?:([\\x{30}-\\x{39}]+)\\x{2d}(?:(?:\\1)))\\z", EcmaRegex.translate("^(?i:([0-9]+)-\\1)$"));
        assertMatches("^(?i:([a-j]+)-\\1)$", "abc-ABC");
        assertNoMatch("^(?i:([a-j]+)-\\1)$", "abc-ABD");
    }

    /**
     * What is still refused: a valid pattern whose backreference cannot be given its meaning by a pattern for
     * {@code java.util.regex}, and one that would be written too long. None is run with another meaning.
     */
    @Test
    void refusedPatterns() {
        String[][] refused = {
            // What the group holds depends on the lookbehind being run backwards.
            {"(?<=(x*?)a*)\\1", EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND},
            {"(?<=(a)+b*)x\\1", EcmaRegex.REFUSED_GROUP_IN_LOOKBEHIND},
            {"(?<=\\1a*(b))x", EcmaRegex.REFUSED_BACKREFERENCE_IN_LOOKBEHIND},
            // A lookahead in a loop of a lookahead, with a lazy loop between it and a reference to its group, where
            // what follows the reference sets a group that is read after the outer lookahead.
            {"^(?=(?:(?=(a|b))[ab](?:c|d)*?\\1(d)?)*)\\1\\2", EcmaRegex.REFUSED_LOOKAROUND_IN_REPETITION},
            {
                "(?:(a)?(b)?(c)?(d)?(e)?(f)?(g)?(h)?(i)?\\1\\2\\3\\4\\5\\6\\7\\8\\9)*",
                EcmaRegex.REFUSED_TOO_MANY_PATHS,
            },
        };
        for (String[] r : refused) {
            assertEquals(r[1], EcmaRegex.unsupported(r[0]), r[0]);
            assertNull(EcmaRegex.translate(r[0]), r[0]);
            assertTrue(EcmaRegex.isValid(r[0]), r[0]);
        }
        // An invalid pattern is not a refused one.
        assertNull(EcmaRegex.unsupported("(a"));
    }

    /**
     * A translation too large for {@code java.util.regex} to compile is refused, and the error says that. Its
     * compiler recurses along the pattern and reports the overflow of the stack as a syntax error.
     */
    @Test
    void aTranslationTooLargeForTheEngineIsRefusedAsSuch() throws InterruptedException {
        String pattern = "(?:a|b)".repeat(20000);
        String[] found = new String[3];
        Thread thread = new Thread(null, () -> {
            found[0] = EcmaRegex.unsupported(pattern);
            found[1] = EcmaRegex.translate(pattern);
            found[2] = SchemaPattern.failure(pattern, "pattern");
        }, "small stack", 256 * 1024);
        thread.start();
        thread.join();
        assertEquals(EcmaRegex.REFUSED_TOO_LARGE, found[0]);
        assertNull(found[1]);
        assertTrue(found[2].endsWith("' in pattern. It is valid ECMA-262, but this library cannot run a pattern too "
                + "large for java.util.regex to compile with the meaning ECMA-262 gives it."), found[2]);
        assertTrue(EcmaRegex.isValid(pattern));
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

    /**
     * Counted repetitions of a body that can match the empty string, each with the texts it matches, every one
     * preceded by "|". The texts asked are all those of up to three characters over "a", "x" and "-". The answers
     * are V8's: {@code new RegExp(pattern, "u").test(text)}.
     */
    private static final String[][] EMPTY_ITERATIONS = {
            {"(^|x){2}a", "|a|aa|ax|a-|xa|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--|xaa|xax|xa-|xxa"},
            {"^(?:-|(?!$)){2}$", "|-|--"},
            {"(\\b|a){2}a$", "|a|aa|-a|aaa|a-a|x-a|-aa|--a"},
            {"^(?:(?<!a)|[ax]{1,2}){2}a$", "|a|aa|xa|aaa|axa|xaa|xxa"},
            {"^([^-]+[^-]+|(?!a)){2}a", "|xaa|xxa"},
            {"(\\B(?<!a)|a){2}a$", "|xa|aaa|axa|xaa|xxa|-xa"},
            {"^(?:^|x){3}a$", "|a|xa|xxa"},
            {"^(?:(?=a)|x){2,}a$", "|a|xa|xxa"},
            {"^(?:(?=a)|x){2,3}a$", "|a|xa|xxa"},
            {"^(?:(?=a)|x){2,3}?a$", "|a|xa|xxa"},
            {"^(?:(?=a)|x){3,}?a$", "|a|xa|xxa"},
            {"^(?:$|a){4}$", "||a|aa|aaa"},
            {"^(?:(?:^|x){2}a|-){2}$", "|a-|--|xa-"},
            {"^(?:(?:\\b|a){2}|x(?=-)){2,}-$", "|a-|x-|aa-|ax-"},
            {"(?<=(?:^|x){2}a)-", "|a-|a-a|a-x|a--|xa-"},
            {"(?<=(?:.[^a]|(?=x)){3,})a", "|xxa|x-a"},
            {"^(?:(a|x|)\\1|^-){2}$", "||-|aa|xx|-aa|-xx"},
            {"^(?:(?=(a))\\1|x){2}$", "|aa|ax|xa|xx"},
            {"^(?:(?=a)a?){2,4}(?!a)", "|a|aa|ax|a-|aaa|aax|aa-|axa|axx|ax-|a-a|a-x|a--"},
            {"^(?:^|x)+a$", "|a|xa|xxa"},
            {"^(?:^|x)*a$", "|a|xa|xxa"},
            {"^(?:^|x){1,2}a$", "|a|xa|xxa"},
            {"^(?:a|){3}x$", "|x|ax|aax"},
            {"^(?:a*){2,}x$", "|x|ax|aax"},
            {"^(?:\\b){2}a$", "|a"},
            {"^(?:(?=a)|(?=x)){3}.$", "|a|x"},
            {"^(?:ax){2}$", ""},
    };

    /**
     * An iteration that matches nothing does not end a repetition whose iterations are still due. ECMA-262 goes on
     * to the next, where {@code java.util.regex} goes on to what follows the repetition.
     */
    @Test
    void anEmptyIterationDoesNotEndARepetitionThatIsStillDue() {
        assertAnswers(EMPTY_ITERATIONS, textsOver(3, new String[] {"a", "x", "-"}));
    }

    /** Only a repetition on which the two engines differ is written with its iterations apart. */
    @Test
    void iterationsAreWrittenApartOnlyWhereTheEnginesDiffer() {
        // A minimum of two, and a body that is empty in some places only.
        assertEquals("(?:^|x)(?:^|x)a", EcmaRegex.translate("(?:^|x){2}a"));
        assertEquals("(?:^|x)(?:^|x)(?:^|x)*a", EcmaRegex.translate("(?:^|x){2,}a"));
        assertEquals("(?:^|x)(?:^|x)(?:^|x){0,3}?a", EcmaRegex.translate("(?:^|x){2,5}?a"));
        // A minimum below two.
        assertEquals("(?:^|x)+a", EcmaRegex.translate("(?:^|x)+a"));
        assertEquals("(?:^|x){1,4}a", EcmaRegex.translate("(?:^|x){1,4}a"));
        assertEquals("(?:^|x){0,4}a", EcmaRegex.translate("(?:^|x){0,4}a"));
        // A body that is empty anywhere, one that is never empty and one that is only ever empty.
        assertEquals("(?:x|){3}a", EcmaRegex.translate("(?:x|){3}a"));
        assertEquals("(?:x*){3,}a", EcmaRegex.translate("(?:x*){3,}a"));
        assertEquals("(?:^x){3}a", EcmaRegex.translate("(?:^x){3}a"));
        assertEquals("(?:^|(?=x)){3}a", EcmaRegex.translate("(?:^|(?=x)){3}a"));
        // Each copy of a body has its own group for the backreference inside it.
        assertEquals("(?:(a|)(?:(?:\\1))|^x)(?:(a|)(?:(?:\\2))|^x)", EcmaRegex.translate("(?:(a|)\\1|^x){2}"));
    }

    /** A repetition too long to write with its iterations apart is refused, and is not given another meaning. */
    @Test
    void aRepetitionTooLongToWriteApartIsRefused() {
        assertMatches("^(?:^|x){200}a$", "xxa");
        assertNoMatch("^(?:^|x){200}a$", "-a");
        assertMatches("^(?:^|x){2,}a$", "x".repeat(300) + "a");
        String longBody = "(?:^|" + "abcdefghij".repeat(40) + ")";
        assertMatches("^" + longBody + "{100}$", "abcdefghij".repeat(80));
        for (String p : new String[] {
            "(?:^|x){201}a", "(?:^|x){200,}a", "(?:^|x){2147483647}a", "(?:(?:^|x){15}-|$){15}", longBody + "{180}",
        }) {
            assertEquals(EcmaRegex.REFUSED_EMPTY_ITERATIONS, EcmaRegex.unsupported(p), p);
            assertNull(EcmaRegex.translate(p), p);
            assertTrue(EcmaRegex.isValid(p), p);
        }
        // The same counts over a body the engines agree on are written as they are.
        assertMatches("^(?:x|){2147483647}a$", "xxa");
        assertMatches("^(?:^x|b){201,}a$", "b".repeat(300) + "a");
        assertEquals("(?:^x|b){201,}a", EcmaRegex.translate("(?:^x|b){201,}a"));
    }
}
