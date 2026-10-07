package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

import java.nio.charset.StandardCharsets;
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
}