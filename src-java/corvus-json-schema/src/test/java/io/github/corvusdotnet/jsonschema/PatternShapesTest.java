package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.regex.Pattern;
import org.junit.jupiter.api.Test;

/** The pattern shapes decide exactly what the translated regular expression decides. */
class PatternShapesTest {
    static final String[] SHAPED = {
        ".", ".+", "(.+)", "^.", "^.+", "^.{1,5}$", "^.*$", "^.+$", "^(.{2,})$", "^.{3}$", "^[@$_#]", "^x-",
        "^#[0-9a-fA-F]{6}$", "^[a-z][a-z0-9_]{0,5}$", "^[A-Z]+$", "^\\d+$", "^\\w*$", "^[^:]+:", "^a.*",
        "^[a-z]+\\.json$", "^[^\\n]*$", "^\\D\\W", "^ab?c*d+$", "^[\\-.]{2}", "^[a\\]]+$", "^x{2,3}y$",
        "^(abc|de)$", "^(?:a|bc|d)$", "^(a|b|c|d|e|f|g|h|i|j)$", "^[Ee][Ss]2015(\\.([Cc]ore|[Pp]roxy))?$",
        "^([a|A]uto)|([n|N]one)$", "default|^[0-9]+$", "^([a-z]+)(\\.[a-z]+)*$", "^(?:[a-z]+:)+[0-9]$",
        "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$", "^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$", "ab|c$|^d",
        "ab|x[0-9]{2}$", "a.c|^q", "(ab){2}$|z",
    };

    static final String[] NOT_SHAPED = {
        "^[a-z]+[a-z0-9]*$", "a+", "^(a|b)+$", "^\\s+$", "^[é]$", "^a{2,1}$", "^[]$", "^.*?$x",
    };

    private static final String ALPHABET = "abcdexyzAZ09!,_-.:#@$\\]\n\ré 中😀 ";

    @Test
    void shapesAgreeWithTheRegularExpression() {
        Random random = new Random(42);
        for (String p : SHAPED) {
            PatternShapes.Matcher shape = PatternShapes.of(p);
            assertNotNull(shape, p);
            Pattern regex = Pattern.compile(EcmaRegex.translate(p));
            for (int i = 0; i < 4000; i++) {
                StringBuilder sb = new StringBuilder();
                int len = random.nextInt(9);
                for (int k = 0; k < len; k++) {
                    int at = random.nextInt(ALPHABET.length());
                    char c = ALPHABET.charAt(at);
                    if (Character.isLowSurrogate(c)) {
                        continue;
                    }
                    sb.append(c);
                    if (Character.isHighSurrogate(c)) {
                        sb.append(ALPHABET.charAt(at + 1));
                    }
                }
                String s = sb.toString();
                byte[] b = s.getBytes(StandardCharsets.UTF_8);
                boolean ascii = s.chars().allMatch(c -> c < 128);
                assertEquals(regex.matcher(s).find(), shape.matches(b, 0, b.length, ascii), p + " on " + s);
            }
        }
    }

    @Test
    void otherPatternsHaveNoShape() {
        for (String p : NOT_SHAPED) {
            assertNull(PatternShapes.of(p), p);
        }
    }

    /** A pattern with the texts it matches and the texts it does not. */
    record AnchorCase(String pattern, List<String> matches, List<String> fails) {}

    /**
     * Patterns with an anchor, a word boundary or a lookaround in a place where a shape could lose it, and patterns
     * with an escaped "$" or "^", which is a literal character. The answers are V8's:
     * {@code new RegExp(pattern, "u").test(text)}.
     */
    static final List<AnchorCase> ANCHOR_CASES = List.of(
        new AnchorCase("^x(,a$)*$", List.of("x", "x,a"), List.of("x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(,a$)+$", List.of("x,a"), List.of("x", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(?:,a$)*$", List.of("x", "x,a"), List.of("x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x$(,a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^(x$)(,a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^(?:x$)(,a)+$", List.of(), List.of("x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^$(,a)*$", List.of(""), List.of(",a", ",a,a", "a")),
        new AnchorCase("^(a,$)*b$", List.of("b"), List.of("a,b", "a,a,b", "a,", "", "a,a")),
        new AnchorCase("^(a,$)+b$", List.of(), List.of("b", "a,b", "a,a,b", "a,", "", "a,a")),
        new AnchorCase("^(?:a,$)*b$", List.of("b"), List.of("a,b", "a,a,b", "a,", "", "a,a")),
        new AnchorCase("^([a-z]+)(\\.[a-z]+$)*$", List.of("a", "a.b"), List.of("a.b.c", "a.", "")),
        new AnchorCase("^([a-z]+\\.$)*[a-z]+$", List.of("a"), List.of("a.b", "a.b.c", ".a", "")),
        new AnchorCase("^x(,a$$)*$", List.of("x", "x,a"), List.of("x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(^,a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(,^a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^^x(,a)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^(^x)(,a)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^(^a,)*b$", List.of("b", "a,b"), List.of("a,a,b", "a,", "", "a,a")),
        new AnchorCase("^(a,)*^b$", List.of("b"), List.of("a,b", "a,a,b", "a,", "", "a,a")),
        new AnchorCase("^x(,(a$))*$", List.of("x", "x,a"), List.of("x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(,(?:a$))*$", List.of("x", "x,a"), List.of("x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(,a|$)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^x(,(a|$))*$", List.of("x", "x,a", "x,a,a", "x,a,a,a", "x,"), List.of("", "x,b")),
        new AnchorCase("^x((^|,)a)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^(a,)*(b$)$", List.of("b", "a,b", "a,a,b"), List.of("a,", "", "a,a")),
        new AnchorCase("^(a,)*b$$", List.of("b", "a,b", "a,a,b"), List.of("a,", "", "a,a")),
        new AnchorCase("^x(,a\\b)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b", "x,ab")),
        new AnchorCase("^x(\\b,a)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^x(,\\Ba)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(\\B,a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x\\b(,a)*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^x\\B(,a)*$", List.of(), List.of("x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^(a\\b,)*b$", List.of("b", "a,b", "a,a,b"), List.of("a,", "", "a,a")),
        new AnchorCase("^(a,\\B)*b$", List.of("b"), List.of("a,b", "a,a,b", "a,", "", "a,a")),
        new AnchorCase("^x(,a(?=,|$))*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^x(,(?!a)a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(?=,)(,a)*$", List.of("x,a", "x,a,a", "x,a,a,a"), List.of("x", "", "x,", "x,b")),
        new AnchorCase("^x(,a(?<=a))*$", List.of("x", "x,a", "x,a,a", "x,a,a,a"), List.of("", "x,", "x,b")),
        new AnchorCase("^x(,(?<!,)a)*$", List.of("x"), List.of("x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^x(?=,a$)(,a)*$", List.of("x,a"), List.of("x", "x,a,a", "x,a,a,a", "", "x,", "x,b")),
        new AnchorCase("^(a,(?=b))*b$", List.of("b", "a,b"), List.of("a,a,b", "a,", "", "a,a")),
        new AnchorCase("^(?!a,a)(a,)*b$", List.of("b", "a,b"), List.of("a,a,b", "a,", "", "a,a")),
        new AnchorCase("^x(,a\\$)*$", List.of("x", "x,a$", "x,a$,a$"), List.of("x,a", "x,a,a", "x,a$,a")),
        new AnchorCase("^x\\$(,a)*$", List.of("x$", "x$,a", "x$,a,a"), List.of("x", "x,a")),
        new AnchorCase("^(a\\$,)*b$", List.of("b", "a$,b", "a$,a$,b"), List.of("a,b", "a$,a,b")),
        new AnchorCase("^x(,\\^a)*$", List.of("x", "x,^a", "x,^a,^a"), List.of("x,a", "x,a,a")),
        new AnchorCase("^(a,)*b\\$$", List.of("b$", "a,b$", "a,a,b$"), List.of("b", "a,b")),
        new AnchorCase("^x(,[$^]a)*$", List.of("x", "x,$a", "x,^a,$a"), List.of("x,a", "x,a,a")),
        new AnchorCase("^a$b", List.of(), List.of("a", "ab", "a$b", "")),
        new AnchorCase("^a^b", List.of(), List.of("a", "ab", "b")),
        new AnchorCase("^a$$", List.of("a"), List.of("ab", "")),
        new AnchorCase("^^a", List.of("a", "ab"), List.of("ba")),
        new AnchorCase("^a\\$b$", List.of("a$b"), List.of("ab", "a")),
        new AnchorCase("^a\\b", List.of("a", "a b", "a,"), List.of("ab")),
        new AnchorCase("^a\\Bb", List.of("ab"), List.of("a b", "a")),
        new AnchorCase("^\\ba", List.of("a"), List.of("ba", "")),
        new AnchorCase("^a(?=b)", List.of("ab"), List.of("a", "ac")),
        new AnchorCase("^a(?!b)", List.of("a", "ac"), List.of("ab")),
        new AnchorCase("^[a-c]+\\b$", List.of("a", "abc"), List.of("")),
        new AnchorCase("^[a-c]+$\\b", List.of("a", "abc"), List.of("")),
        new AnchorCase("^$", List.of(""), List.of("a")),
        new AnchorCase("^a$.*", List.of("a"), List.of("ab", "")),
        new AnchorCase("^a\\b.*", List.of("a", "a b"), List.of("ab")),
        new AnchorCase("^a^.*", List.of(), List.of("a", "ab")),
        new AnchorCase("^\\$.*", List.of("$", "$a"), List.of("a")),
        new AnchorCase("^a$|b", List.of("a", "ab", "b", "cb"), List.of("c")),
        new AnchorCase("a$|^b", List.of("a", "ba", "bc"), List.of("ab", "cb")),
        new AnchorCase("^(a$|b)$", List.of("a", "b"), List.of("ab", "")),
        new AnchorCase("^(a|^b)$", List.of("a", "b"), List.of("ab")),
        new AnchorCase("^(a|b\\b)$", List.of("a", "b"), List.of("ab")),
        new AnchorCase("^(?:a\\$|b)$", List.of("a$", "b"), List.of("a")),
        new AnchorCase("^(a|$)$", List.of("a", ""), List.of("b")),
        new AnchorCase("^(^|b)$", List.of("", "b"), List.of("a")),
        new AnchorCase("^(a$|b)c", List.of("bc", "bcd"), List.of("a", "ac")),
        new AnchorCase("(^a|b)c$", List.of("ac", "bc", "xbc"), List.of("xac", "acx")),
        new AnchorCase("a(b|^c)", List.of("ab", "xab"), List.of("ac", "c")),
        new AnchorCase("(a$){2}", List.of(), List.of("a", "aa", "")),
        new AnchorCase("(^a){2}", List.of(), List.of("a", "aa")),
        new AnchorCase("x(a$)?b", List.of("xb"), List.of("xab", "xa")),
        new AnchorCase("^x(a$)?", List.of("x", "xa", "xab", "xb"), List.of()),
        new AnchorCase("(a|b$)c", List.of("ac"), List.of("bc", "b")),
        new AnchorCase("a|\\bb", List.of("a", "b", "c b"), List.of("cb")),
        new AnchorCase("a\\b|c", List.of("a", "c", "b a"), List.of("ab")),
        new AnchorCase("(?=a)a|b", List.of("a", "b"), List.of("c")),
        new AnchorCase("(?<=a)b|c", List.of("ab", "c"), List.of("b")),
        new AnchorCase("a^|b", List.of("b"), List.of("a", "")),
        new AnchorCase("$a|b", List.of("b"), List.of("a", "")),
        new AnchorCase("^|a", List.of("", "b"), List.of()),
        new AnchorCase("a|$", List.of("", "b"), List.of()),
        new AnchorCase("(a|b)$c", List.of(), List.of("ac", "a", "bc")),
        new AnchorCase("^(a|b)^c", List.of(), List.of("ac", "c")),
        new AnchorCase("[a-c]{2}$|^x", List.of("ab", "x", "yab"), List.of("abx", "yx")),
        new AnchorCase("[a-c]\\b|x", List.of("a", "x", "d a"), List.of("ad")),
        new AnchorCase("^[a-c]+$|^\\d$", List.of("abc", "1"), List.of("a1", "")),
        new AnchorCase("^(a|b){2}$", List.of("ab"), List.of("a", "abc")),
        new AnchorCase("^.{1,3}$$", List.of("a"), List.of("abcd", "")),
        new AnchorCase("^.$^", List.of(), List.of("a", "")),
        new AnchorCase("^(.*)$", List.of("", "a"), List.of()),
        new AnchorCase("^es$|ms", List.of("es", "xms"), List.of("esx", "x")),
        new AnchorCase("^x-$", List.of("x-"), List.of("x-a")),
        new AnchorCase("^x-\\b", List.of("x-a"), List.of("x-", "x--")));

    /**
     * A shape that cannot express an anchor or an assertion leaves the pattern to the regular expression. The
     * separated list dropped a "$" at the end of its repeated group and after its first item (0.1.0 to 0.1.2), so
     * {"pattern": "^x(,a$)*$"} took "x,a,a", which has a "$" before its second ",a".
     */
    @Test
    void anAnchorOrAnAssertionInsideAShapeIsNotDropped() {
        List<String> wrong = new ArrayList<>();
        for (AnchorCase c : ANCHOR_CASES) {
            Validator v = Validator.compile("{\"pattern\": " + quote(c.pattern()) + "}");
            for (String text : c.matches()) {
                if (!v.isValid(quote(text))) {
                    wrong.add(c.pattern() + " does not match " + text);
                }
            }
            for (String text : c.fails()) {
                if (v.isValid(quote(text))) {
                    wrong.add(c.pattern() + " matches " + text);
                }
            }
        }
        assertEquals(List.of(), wrong);
    }

    /** A list with an anchor or an assertion inside it has no shape. One with an escaped anchor keeps its shape. */
    @Test
    void aSeparatedListWithAnAnchorInsideItHasNoShape() {
        String[] anchored = {
            "^x(,a$)*$", "^x(?:,a$)+$", "^x$(,a)*$", "^(x$)(,a)*$", "^$(,a)*$", "^(a,$)*b$", "^x(^,a)*$", "^(^a,)*b$",
            "^x(,(a$))*$", "^x(,a\\b)*$", "^x\\B(,a)*$", "^x(,a(?=,|$))*$", "^x(?=,)(,a)*$",
        };
        for (String p : anchored) {
            assertNull(PatternShapes.of(p), p);
        }
        String[] escaped = {
            "^x(,a\\$)*$", "^x\\$(,a)*$", "^(a\\$,)*b$", "^x(,\\^a)*$", "^(a,)*b\\$$", "^x(,[$^]a)*$",
        };
        for (String p : escaped) {
            assertNotNull(PatternShapes.of(p), p);
        }
    }

    /** The text as a JSON string. */
    private static String quote(String text) {
        return "\"" + text.replace("\\", "\\\\").replace("\"", "\\\"") + "\"";
    }
}