package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;

import org.junit.jupiter.api.Test;

/** The cases of the unified JSON-Schema-Test-Suite that the format checks got wrong (the descriptions are the suite's). */
class FormatUnifiedSuiteTest {
    private static final CompileOptions FORMATS = CompileOptions.builder().assertFormat(true).build();

    private static boolean valid(String format, String json) {
        return Validator.compile("{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"format\": \""
                + format + "\"}", FORMATS).isValid(JsonDocument.parse(json));
    }

    @Test
    void formatsMatchTheSuite() {
        String fullwidth63 = "\"" + "\\uff41".repeat(63) + "\"";
        Object[][] cases = {
            {"email", "\"\\\"test\\\\\\u00a9\\\"@iana.org\"", false, "a non-ASCII character in a quoted pair is not valid"},
            {"email", "\"\\\"\\\\a\\\"@iana.org\"", true, "a quoted pair in the local part is valid"},
            {"idn-email", "\"\\u0085@example.com\"", true, "a C1 control in the local part is valid"},
            {"idn-email", "\"\\uffff@example.com\"", true, "a noncharacter in the local part is valid"},
            {"idn-hostname", "\"a\\u200bb\"", true, "a zero width space is ignored by the mapping"},
            {"idn-hostname", fullwidth63, true, "a label of 63 fullwidth characters is valid after mapping"},
            {"idn-hostname", "\"\\uff58\\uff4e--nxasmq6b\"", true, "an ACE prefix produced by the mapping step is decoded as an A-label"},
            {"uri-template", "\"a'b\"", true, "an apostrophe in a literal is valid"},
            {"uri-template", "\"a%\"", false, "a literal with a lone percent sign is invalid"},
            {"uri-template", "\"a%4\"", false, "a literal with an incomplete percent-encoded triplet is invalid"},
            {"uri-template", "\"a%GG\"", false, "a literal with non-hex percent encoding is invalid"},
            {"uri-template", "\"a\\u007fb\"", false, "a delete character in a literal is invalid"},
        };
        for (Object[] c : cases) {
            assertEquals(c[2], valid((String) c[0], (String) c[1]), c[0] + ": " + c[3]);
        }
    }

    @Test
    void theMappingIsUnicode17() {
        assertEquals("17.0.0", Uts46Data.UNICODE_VERSION);
        assertEquals("ab", Uts46.map("a\u200bb", 0, 3));
        assertEquals(null, Uts46.map("a\u200db", 0, 3));
    }
}
