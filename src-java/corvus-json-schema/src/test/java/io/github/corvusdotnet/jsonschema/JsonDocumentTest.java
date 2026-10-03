package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.nio.charset.StandardCharsets;
import org.junit.jupiter.api.Test;

class JsonDocumentTest {
    private static JsonDocument parse(String json) {
        return JsonDocument.parse(json.getBytes(StandardCharsets.UTF_8));
    }

    @Test
    void documentsFromTheReusedParserAreIndependent() {
        JsonDocument a = parse("{\"a\": [1, \"x\\ny\", {\"b\": null}]}");
        JsonDocument b = parse("[true, \"\\u00e9t\\u00e9\", 2.5, {\"c\": \"d\"}]");
        assertEquals("{\"a\":[1,\"x\\ny\",{\"b\":null}]}", a.toJson(a.root()));
        assertEquals("[true,\"été\",2.5,{\"c\":\"d\"}]", b.toJson(b.root()));
    }

    @Test
    void aFailedParseDoesNotAffectTheNext() {
        assertThrows(JsonParseException.class, () -> parse("{\"a\": [1, 2"));
        assertThrows(JsonParseException.class, () -> parse("[\"unterminated"));
        JsonDocument d = parse("{\"k\": [\"v\"]}");
        assertEquals("{\"k\":[\"v\"]}", d.toJson(d.root()));
    }

    @Test
    void aLargeDocumentReleasesTheCachedBuffers() {
        parse("[1]");
        assertTrue(JsonDocument.hasCachedParser());
        StringBuilder sb = new StringBuilder("[");
        for (int i = 0; i < 100_000; i++) {
            sb.append(i == 0 ? "" : ",").append("\"s").append(i).append('"');
        }
        JsonDocument big = parse(sb.append(']').toString());
        assertEquals(100_000, big.count(big.root()));
        assertFalse(JsonDocument.hasCachedParser());
        JsonDocument small = parse("[2]");
        assertEquals("[2]", small.toJson(small.root()));
        assertTrue(JsonDocument.hasCachedParser());
    }

    @Test
    void stringsAreScannedByByteClass() {
        String longAscii = "abcdefghijklmnopqrstuvwxyz0123456789";
        JsonDocument d = parse("[\"" + longAscii + "\", \"café 中文 😀\", \"q\\\"b\\\\\", \"\"]");
        int first = d.first(d.root());
        assertEquals(longAscii, d.string(first));
        assertTrue(d.strAscii(first));
        assertEquals("café 中文 😀", d.string(first + 1));
        assertFalse(d.strAscii(first + 1));
        assertEquals("q\"b\\", d.string(first + 2));
        assertEquals("", d.string(first + 3));
        // Control characters must be escaped; invalid UTF-8 is rejected.
        assertThrows(JsonParseException.class, () -> parse("[\"a\tb\"]"));
        assertThrows(JsonParseException.class,
                () -> JsonDocument.parse(new byte[] {'[', '"', (byte) 0xc3, '"', ']'}));
    }
}
