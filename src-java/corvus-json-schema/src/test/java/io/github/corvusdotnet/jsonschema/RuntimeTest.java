package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Random;
import java.util.stream.Stream;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.io.TempDir;

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
    void sortAgreesWithArraysSort() {
        Random random = new Random(11);
        for (int i = 0; i < 4_000; i++) {
            int n = random.nextInt(i % 10 == 0 ? 3_000 : 120);
            long[] values = new long[n + 2];
            for (int k = 0; k < values.length; k++) {
                // Few distinct values in some arrays, sorted and reversed runs in others.
                values[k] = switch (i % 4) {
                    case 0 -> random.nextLong();
                    case 1 -> random.nextInt(4);
                    case 2 -> k;
                    default -> -k;
                };
            }
            long[] expected = values.clone();
            java.util.Arrays.sort(expected, 1, n + 1);
            Values.sort(values, 1, n);
            org.junit.jupiter.api.Assertions.assertArrayEquals(expected, values, "case " + i);
        }
    }

    @Test
    void uniqueItemsInLongArraysAndDuplicateKeysInWideObjects() {
        Validator unique = Validator.compile("{\"uniqueItems\": true}");
        Random random = new Random(5);
        for (int i = 0; i < 300; i++) {
            int n = 17 + random.nextInt(400);
            java.util.Set<Integer> seen = new java.util.HashSet<>();
            StringBuilder array = new StringBuilder("[");
            StringBuilder object = new StringBuilder("{");
            boolean distinct = true;
            for (int k = 0; k < n; k++) {
                int value = random.nextInt(i % 2 == 0 ? 1_000_000 : n * 20);
                distinct &= seen.add(value);
                array.append(k == 0 ? "" : ",").append(value);
                object.append(k == 0 ? "" : ",").append("\"k").append(value).append("\":0");
            }
            assertEquals(distinct, unique.isValid(array.append(']').toString()), "array " + i);
            // Of duplicate names the parser keeps one property.
            JsonDocument parsed = JsonDocument.parse(object.append('}').toString());
            assertEquals(seen.size(), parsed.count(parsed.root()), "object " + i);
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

    @Test
    void dumpsGeneratedClassFiles(@TempDir Path dir) throws IOException {
        System.setProperty(CodeGen.DUMP_PROPERTY, dir.resolve("classes").toString());
        try {
            Validator.compile("{\"type\": \"string\"}");
        } finally {
            System.clearProperty(CodeGen.DUMP_PROPERTY);
        }
        try (Stream<Path> files = Files.list(dir.resolve("classes"))) {
            Path file = files.findFirst().orElseThrow();
            byte[] bytes = Files.readAllBytes(file);
            assertEquals(0xCAFEBABE, ((bytes[0] & 0xff) << 24) | ((bytes[1] & 0xff) << 16)
                    | ((bytes[2] & 0xff) << 8) | (bytes[3] & 0xff));
        }
    }

    @Test
    void interpretedNodesHandValuesBackToCompiledCode() {
        // anyOf branches that add evaluated names leave the root to the interpreter; the values below it, and the
        // in-place children that cannot mark, still run compiled.
        Validator v = Validator.compile("""
            {
              "type": "object",
              "properties": {
                "id": {"type": "integer", "minimum": 1},
                "tags": {"type": "array", "items": {"type": "string", "maxLength": 3}}
              },
              "anyOf": [
                {"properties": {"a": {"type": "string"}}, "required": ["a"]},
                {"properties": {"b": {"type": "number"}}, "required": ["b"]}
              ],
              "not": {"required": ["forbidden"]},
              "unevaluatedProperties": false
            }""");
        boolean[] methods = v.code().methods();
        int compiled = 0;
        for (boolean m : methods) {
            compiled += m ? 1 : 0;
        }
        assertTrue(compiled > 0);
        assertTrue(v.isValid("{\"id\": 1, \"tags\": [\"x\", \"yz\"], \"a\": \"s\"}"));
        assertTrue(v.isValid("{\"b\": 2}"));
        assertFalse(v.isValid("{\"id\": 0, \"a\": \"s\"}"));
        assertFalse(v.isValid("{\"tags\": [\"long\"], \"a\": \"s\"}"));
        assertFalse(v.isValid("{\"a\": \"s\", \"b\": 2, \"c\": 3}"));
        assertFalse(v.isValid("{\"a\": 1}"));
        assertFalse(v.isValid("{\"a\": \"s\", \"forbidden\": 1}"));
    }
}
