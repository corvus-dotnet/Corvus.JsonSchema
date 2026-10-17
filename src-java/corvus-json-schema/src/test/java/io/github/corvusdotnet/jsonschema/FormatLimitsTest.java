package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTimeoutPreemptively;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.lang.management.ManagementFactory;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;

/**
 * What a text from outside can cost a {@code format} check. The checks of duration, the URI and IRI formats, URI
 * templates, the JSON Pointer formats and the local part of an email address were regular expressions run by
 * {@code java.util.regex}, which matches a repetition of alternatives by recursion: a text of some thousands of
 * characters threw {@code StackOverflowError} out of the validator. The check of an internationalized host name, and
 * the reading of the group names of a regular expression, took time that grew with the square of the text. Each is
 * now read once from left to right.
 */
class FormatLimitsTest {
    private static final CompileOptions FORMATS = CompileOptions.builder().assertFormat(true).build();
    private static final Map<String, Validator> VALIDATORS = new HashMap<>();
    private static final int LONG = 1_000_000;

    private static Validator format(String name) {
        return VALIDATORS.computeIfAbsent(name, f -> {
            String draft = f.endsWith("@6") ? "http://json-schema.org/draft-06/schema#"
                    : "https://json-schema.org/draft/2020-12/schema";
            return Validator.compile(
                    "{\"$schema\": \"" + draft + "\", \"format\": \"" + f.replace("@6", "") + "\"}", FORMATS);
        });
    }

    private static JsonDocument text(String s) {
        StringBuilder json = new StringBuilder(s.length() + 2).append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (c == '"' || c == '\\') {
                json.append('\\').append(c);
            } else if (c < 0x20) {
                json.append(String.format("\\u%04x", (int) c));
            } else {
                json.append(c);
            }
        }
        return JsonDocument.parse(json.append('"').toString());
    }

    /**
     * The answers the formats gave before their regular expressions were replaced, for the format instances of the
     * JSON-Schema-Test-Suite, texts written for each rule of each grammar, and generated texts: 12,927 answers.
     * The same build answered a further 1.4 million generated texts the same way as the build that replaced it.
     */
    @Test
    void theFormatsAnswerAsTheyDid() throws IOException {
        List<String> wrong = new ArrayList<>();
        int count = 0;
        try (InputStream in = FormatLimitsTest.class.getResourceAsStream("format_answers.tsv")) {
            assertNotNull(in, "format_answers.tsv");
            BufferedReader lines = new BufferedReader(new InputStreamReader(in, StandardCharsets.UTF_8));
            for (String line = lines.readLine(); line != null; line = lines.readLine()) {
                String[] f = line.split("\t", 3);
                boolean want = f[1].equals("1");
                if (format(f[0]).isValid(JsonDocument.parse(f[2])) != want) {
                    wrong.add(f[0] + " on " + f[2] + " is not " + want);
                }
                count++;
            }
        }
        assertEquals(12927, count);
        assertTrue(wrong.isEmpty(), wrong.size() + " answers differ: " + wrong.subList(0, Math.min(10, wrong.size())));
    }

    /** A text of a million characters for each format that a regular expression checked: one valid, one not. */
    private static String[][] longTexts() {
        String a = "a".repeat(LONG);
        String seg = "/ab~0c".repeat(LONG / 6);
        return new String[][] {
            {"uri", "http://example.com/" + a, "http://example.com/" + a + " "},
            {"uri", "x:" + "a/".repeat(LONG / 2) + "?" + a + "#" + a, "x:" + "%41".repeat(LONG / 3) + "%4"},
            {"uri", "http://" + "u".repeat(LONG) + "@h:80/", "http://" + "u@".repeat(LONG / 2) + "h"},
            {"uri-reference", "a/".repeat(LONG / 2), "a/".repeat(LONG / 2) + "#" + "#"},
            {"uri-reference", "//" + a + "/" + a + "?" + a, a + ":"+ "\\"},
            {"iri", "http://éxample.com/" + "é".repeat(LONG) + "?" + "".repeat(1000), "http://éxample.com/" + "é".repeat(LONG) + ""},
            {"iri-reference", "é/".repeat(LONG / 2) + "#é", "é/".repeat(LONG / 2) + "#é#"},
            {"uri-template", "/a{b,c:3,d*}".repeat(LONG / 12), "/a{b,c:3,d*}".repeat(LONG / 12) + "{"},
            {"uri-template", "{" + "a,".repeat(LONG / 2) + "a}", "{" + "a,".repeat(LONG / 2) + "}"},
            {"json-pointer", seg, seg + "~"},
            {"relative-json-pointer", "1" + seg, "0" + "1".repeat(LONG)},
            {"relative-json-pointer", "1" + "0".repeat(LONG) + "#", "1" + "0".repeat(LONG) + "#/"},
            {"email", "a." .repeat(LONG / 2) + "a@example.com", "a.".repeat(LONG / 2) + "@example.com"},
            {"email", "\"" + "a\\\"".repeat(LONG / 3) + "\"@example.com", "\"" + "a\\\"".repeat(LONG / 3) + "@example.com"},
            {"idn-email", "é.".repeat(LONG / 2) + "é@example.com", "é.".repeat(LONG / 2) + "é @example.com"},
            {"duration", "P" + "1".repeat(LONG) + "Y" + "2".repeat(LONG) + "M", "P" + "1".repeat(LONG) + "Y" + "2".repeat(LONG) + "D"},
            {"duration", "PT" + "1".repeat(LONG) + "S", "PT" + "1".repeat(LONG)},
        };
    }

    /**
     * Each check answers a text of a million characters, on a thread with a stack of 256 KB. With the regular
     * expressions every one of these but the durations threw {@code StackOverflowError}, on a stack of any size a
     * thread is given by default.
     */
    @Test
    void aLongTextIsAnsweredAndDoesNotOverflowTheStack() throws InterruptedException {
        List<String> failures = new ArrayList<>();
        AtomicReference<Throwable> thrown = new AtomicReference<>();
        Thread thread = new Thread(null, () -> {
            for (String[] c : longTexts()) {
                for (int k = 1; k <= 2; k++) {
                    try {
                        if (format(c[0]).isValid(text(c[k])) != (k == 1)) {
                            failures.add(c[0] + " answers " + (k != 1) + " for " + c[k].substring(0, 30) + "...");
                        }
                    } catch (StackOverflowError e) {
                        failures.add(c[0] + " overflows the stack on " + c[k].substring(0, 30) + "...");
                    } catch (Throwable e) {
                        thrown.set(e);
                    }
                }
            }
        }, "small-stack", 256 * 1024);
        thread.start();
        thread.join();
        assertTrue(thrown.get() == null, String.valueOf(thrown.get()));
        assertTrue(failures.isEmpty(), String.join("; ", failures));
    }

    private static String repeat(int count, java.util.function.IntFunction<String> part) {
        StringBuilder text = new StringBuilder();
        for (int n = 0; n < count; n++) {
            text.append(part.apply(n));
        }
        return text.toString();
    }

    /**
     * Texts of a million characters or so that took time growing with the square of their length: a host name
     * of one long label (letters, an A-label, Katakana middle dots), the same as the domain of an address, and a
     * regular expression with many group names, many groups of one name, names written to be alike, and many
     * references. Each took minutes or more, and all of them together now have 20 seconds, which is hundreds of
     * times what they take.
     */
    @Test
    void theTimeOfACheckDoesNotGrowWithTheSquareOfTheText() {
        String letters = "é".repeat(LONG);
        String dots = "・".repeat(LONG);
        String punycode = "xn--" + "a".repeat(LONG);
        String labels = "é.".repeat(LONG / 2) + "é";
        String names = repeat(LONG / 14, n -> "(?<n" + n + ">a)");
        String sameName = "(?<n>a)" + repeat(LONG / 8, n -> "|(?<n>a)");
        String alike = repeat(LONG / 40, n -> "(?<" + alike(n) + ">a)");
        String references = "(?<a>x)(?<b>y)" + repeat(LONG / 12, n -> "\\k<a>\\k<b>");
        String namesAndReferences = repeat(LONG / 28, n -> "(?<n" + n + ">a)") + repeat(LONG / 28, n -> "\\k<n" + n + ">");
        JsonDocument[] invalid = {
            text(letters), text(dots), text(punycode), text(labels), text("a@" + letters), text("a@" + dots),
            text("a@" + punycode), text("a@" + labels),
        };
        String[] formats = {"idn-hostname", "idn-hostname", "idn-hostname", "idn-hostname", "idn-email", "idn-email",
            "idn-email", "idn-email"};
        JsonDocument[] regexes = {text(names), text(sameName), text(alike), text(references), text(namesAndReferences)};
        JsonDocument notRegex = text(names + "(?<n7>a)");
        JsonDocument noSuchName = text(names + "\\k<m7>");
        assertTimeoutPreemptively(Duration.ofSeconds(20), () -> {
            for (int k = 0; k < invalid.length; k++) {
                assertTrue(!format(formats[k]).isValid(invalid[k]), formats[k] + " case " + k);
            }
            for (int k = 0; k < regexes.length; k++) {
                assertTrue(format("regex").isValid(regexes[k]), "regex case " + k);
            }
            assertTrue(!format("regex").isValid(notRegex), "a name used twice");
            assertTrue(!format("regex").isValid(noSuchName), "a reference to no group");
        });
    }

    /** A name of 17 pairs of letters, each "Aa" or "BB": java.lang.String has one hash for all such names. */
    private static String alike(int n) {
        StringBuilder name = new StringBuilder();
        for (int bit = 0; bit < 17; bit++) {
            name.append((n >> bit & 1) == 0 ? "Aa" : "BB");
        }
        return name.toString();
    }

    private static final com.sun.management.ThreadMXBean THREADS =
            (com.sun.management.ThreadMXBean) ManagementFactory.getThreadMXBean();

    /** What one validation of a document allocates on this thread, after one that grew the buffers it reuses. */
    private static long allocatedBy(Validator v, JsonDocument small, JsonDocument d, boolean want) {
        v.isValid(small);
        long before = THREADS.getCurrentThreadAllocatedBytes();
        boolean answer = v.isValid(d);
        long bytes = THREADS.getCurrentThreadAllocatedBytes() - before;
        assertEquals(want, answer);
        return bytes;
    }

    /**
     * What a check keeps is in proportion to the text and not more. A host name of a million letters is refused
     * at its 64th, and no buffer grows for it. The reader of regular expressions kept the whole path of
     * alternatives for each named group, 380 numbers for a group nested 190 deep, so 20,000 such groups in a text
     * of 240 KB took 101 MB. It keeps a node for each alternative that holds a named group.
     */
    @Test
    void whatACheckKeepsIsInProportionToTheText() {
        Validator host = Validator.compile("{\"format\": \"idn-hostname\"}", FORMATS);
        long hostBytes = allocatedBy(host, text("a.b"), text("a".repeat(LONG)), false);
        assertTrue(hostBytes < 64 << 10, "idn-hostname allocated " + hostBytes + " bytes for a label of a million letters");

        Validator regex = Validator.compile("{\"format\": \"regex\"}", FORMATS);
        String deep = "(?:".repeat(190) + repeat(20000, n -> "(?<n" + n + ">a)") + ")".repeat(190);
        long regexBytes = allocatedBy(regex, text("(?<a>b)"), text(deep), true);
        assertTrue(regexBytes < 4 << 20, "regex allocated " + regexBytes + " bytes for 20,000 named groups nested 190 deep");
    }
}
