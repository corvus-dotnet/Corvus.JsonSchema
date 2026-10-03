package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.Random;
import org.junit.jupiter.api.Test;

/** The runtime helpers of compiled schemas agree with their straightforward readings. */
class RuntimeTest {
    @Test
    void lengthBoundsAgreeWithCountingCodePoints() {
        String[] alphabet = {"a", "é", "中", "😀"};
        Random random = new Random(3);
        for (int i = 0; i < 20_000; i++) {
            StringBuilder sb = new StringBuilder();
            int len = random.nextInt(12);
            for (int k = 0; k < len; k++) {
                sb.append(alphabet[random.nextInt(alphabet.length)]);
            }
            String s = sb.toString();
            JsonDocument d = JsonDocument.parse("\"" + s + "\"");
            long n = s.codePointCount(0, s.length());
            long min = random.nextInt(14);
            long max = random.nextInt(3) == 0 ? -1 : random.nextInt(14);
            boolean expected = n >= min && (max < 0 || n <= max);
            assertEquals(expected, Rt.lengthWithin(d, d.root(), min, max), s + " " + min + " " + max);
        }
    }

    @Test
    void uniqueStringsPairwise() {
        StringBuilder items = new StringBuilder("[");
        for (int i = 0; i < 30; i++) {
            items.append(i == 0 ? "" : ",").append("\"s").append(i).append('"');
        }
        JsonDocument unique = JsonDocument.parse(items + "]");
        JsonDocument repeated = JsonDocument.parse(items + ",\"s7\"]");
        long[] scratch = new long[64];
        assertEquals(true, Values.allUnique(unique, unique.root(), scratch));
        assertEquals(false, Values.allUnique(repeated, repeated.root(), scratch));
    }
}
