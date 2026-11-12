package io.github.corvusdotnet.jsonschema;

import static org.junit.jupiter.api.Assertions.assertEquals;

import org.junit.jupiter.api.Test;

/**
 * A leap second is a valid {@code date-time} only at 23:59:60 UTC on a day that ended with one, in whatever offset it
 * is written, and a valid {@code time} whenever it is 23:59 in UTC.
 */
class LeapSecondTest {
    private static final CompileOptions FORMATS = CompileOptions.builder().assertFormat(true).build();
    private static final Validator DATE_TIME = Validator.compile(
            "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"format\": \"date-time\"}", FORMATS);
    private static final Validator TIME = Validator.compile(
            "{\"$schema\": \"https://json-schema.org/draft/2020-12/schema\", \"format\": \"time\"}", FORMATS);

    private static void check(Validator v, String text, boolean valid) {
        assertEquals(valid, v.isValid("\"" + text + "\""), text);
    }

    @Test
    void dateTimes() {
        check(DATE_TIME, "1998-12-31T23:59:60Z", true);
        check(DATE_TIME, "1998-12-31T15:59:60.123-08:00", true);
        check(DATE_TIME, "1972-06-30T23:59:60Z", true);
        check(DATE_TIME, "2016-12-31T23:59:60z", true);
        check(DATE_TIME, "2017-01-01T00:59:60+01:00", true);
        check(DATE_TIME, "1998-12-31T00:29:60-23:30", true);
        check(DATE_TIME, "1999-01-01T00:29:60+00:30", true);
        check(DATE_TIME, "1998-12-30T23:59:60-24:00", false);
        check(DATE_TIME, "1998-12-31T23:59:60+00:00", true);
        check(DATE_TIME, "1999-12-31T23:59:60Z", false);
        check(DATE_TIME, "2017-12-31T23:59:60Z", false);
        check(DATE_TIME, "1998-12-31T23:58:60Z", false);
        check(DATE_TIME, "1998-12-31T22:59:60Z", false);
        check(DATE_TIME, "1998-12-31T23:59:61Z", false);
        check(DATE_TIME, "2016-12-31T22:59:60-01:00", true);
        check(DATE_TIME, "2016-12-31T00:00:60Z", false);
        check(DATE_TIME, "1998-12-31T23:59:60+23:59", false);
        check(DATE_TIME, "1999-01-01T23:58:60+23:59", true);
        check(DATE_TIME, "1998-12-31T00:00:60-23:59", true);
    }

    @Test
    void times() {
        check(TIME, "23:59:60Z", true);
        check(TIME, "15:59:60-08:00", true);
        check(TIME, "00:29:60-23:30", true);
        check(TIME, "23:58:60Z", false);
    }
}
