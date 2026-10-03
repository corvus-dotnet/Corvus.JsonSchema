package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Random;
import java.util.Set;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;

/** The allocation-free validator of the regex format agrees with the translator's reading of ECMA-262 (u flag). */
class EcmaRegexValidatorTest {
    static final String[] HAND = {
        "", "a", "^a$", "a|b", "(a)", "(?:a)", "(?=a)", "(?!a)", "(?<=a)", "(?<!a)", "(?<n>a)\\k<n>", "\\k<n>",
        "(?<n>a)(?<n>b)", "(?<$x_1>a)", "(?<1a>a)", "a{2}", "a{2,}", "a{2,3}", "a{3,2}", "a{", "a{,2}", "{", "}", "]",
        "[", "[]", "[^]", "[a-z]", "[z-a]", "[\\d-z]", "[a-\\d]", "[\\b]", "[\\-]", "\\-", "\\a", "\\c", "\\cA", "\\c1",
        "\\0", "\\01", "\\1", "(a)\\1", "(a)\\2", "\\x4", "\\x41", "\\u004", "\\u0041", "\\u{1F600}", "\\u{110000}",
        "\\ud83d\\ude00", "[\\ud83d\\ude00-\\ud83d\\ude4f]", "\\p{L}", "\\p{Letter}", "\\p{digit}", "\\p{Nope}",
        "\\p{gc=Lu}", "\\p{Script=Greek}", "\\p{sc=Grek}", "\\p{Script=Nope}", "\\P{ASCII}", "\\p", "\\p{",
        "a**", "a+?", "*a", "(?=a)*", "^*", "$+", "\\b+", "a)", "(a", "(?a)", "\\/", "\\.", "a\\", "[a", "x{1}{2}",
        "\\w+@\\w+\\.\\w+", "^[a-z][a-z0-9_]*$", "(?<a>.)\\k<a>", "[\\p{L}\\d]", "\\s\\S\\w\\W\\d\\D",
    };

    static List<String> corpusPatterns() throws IOException {
        Set<String> out = new LinkedHashSet<>();
        Path suite = SuiteTest.suiteRoot().resolve("tests");
        if (Files.isDirectory(suite)) {
            try (Stream<Path> files = Files.walk(suite)) {
                for (Path f : (Iterable<Path>) files.filter(p -> p.toString().endsWith(".json"))::iterator) {
                    JsonDocument d = SuiteTest.read(f);
                    collect(d, d.root(), out);
                }
            }
        }
        return new ArrayList<>(out);
    }

    private static void collect(JsonDocument d, int v, Set<String> out) {
        int kind = d.kind(v);
        if (kind == JsonDocument.ARRAY) {
            for (int i = 0; i < d.count(v); i++) {
                collect(d, d.first(v) + i, out);
            }
        } else if (kind == JsonDocument.OBJECT) {
            for (int i = 0; i < d.count(v); i++) {
                int k = d.first(v) + 2 * i;
                String name = d.string(k);
                if (name.equals("pattern") && d.kind(k + 1) == JsonDocument.STRING) {
                    out.add(d.string(k + 1));
                }
                if (name.equals("patternProperties") && d.kind(k + 1) == JsonDocument.OBJECT) {
                    for (int j = 0; j < d.count(k + 1); j++) {
                        out.add(d.string(d.first(k + 1) + 2 * j));
                    }
                }
                if (name.equals("data") && d.kind(k + 1) == JsonDocument.STRING) {
                    // The regex format tests' instances are patterns too.
                    out.add(d.string(k + 1));
                }
                collect(d, k + 1, out);
            }
        }
    }

    private static void check(String p) {
        int verdict = EcmaRegex.translatorVerdict(p);
        if (verdict == 2) {
            return;
        }
        assertEquals(verdict == 1, new EcmaRegex.Validator().isValid(p), p);
    }

    @Test
    void agreesOnHandWrittenPatterns() {
        for (String p : HAND) {
            check(p);
        }
    }

    @Test
    void agreesOnTheSuitesPatterns() throws IOException {
        for (String p : corpusPatterns()) {
            check(p);
        }
    }

    @Test
    void agreesOnGeneratedPatterns() {
        String alphabet = "ab0\\\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L";
        Random random = new Random(7);
        EcmaRegex.Validator v = new EcmaRegex.Validator();
        for (int i = 0; i < 50_000; i++) {
            StringBuilder sb = new StringBuilder();
            int len = 1 + random.nextInt(8);
            for (int k = 0; k < len; k++) {
                sb.append(alphabet.charAt(random.nextInt(alphabet.length())));
            }
            String p = sb.toString();
            int verdict = EcmaRegex.translatorVerdict(p);
            if (verdict != 2) {
                assertEquals(verdict == 1, v.isValid(p), p);
            }
        }
    }
}
