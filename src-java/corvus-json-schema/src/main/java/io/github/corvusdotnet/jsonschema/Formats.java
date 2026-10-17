package io.github.corvusdotnet.jsonschema;

/**
 * Format assertions, applied only when {@code format} is asserted (the format-assertion vocabulary or
 * {@link CompileOptions.Builder#assertFormat(Boolean)}). A port of the Rust and TypeScript ports' format checks, which
 * match the C# {@code JsonSchemaEvaluation} ones.
 */
final class Formats {
    private Formats() {
    }

    /** The format a dialect recognises for a {@code format} value ({@code SchemaCompiler.GetFormatKind}). */
    enum Kind {
        UNKNOWN("unknown", null),
        DATE("date", "Expected an ISO8601 Date string."),
        TIME("time", "Expected an ISO8601 Offset Time string."),
        DATE_TIME("date-time", "Expected an ISO8601 Offset DateTime string."),
        DURATION("duration", "Expected an ISO8601 Duration string."),
        UUID("uuid", "Expected an RFC4122 UUID."),
        IPV4("ipv4", "Expected an RFC2673 IP V4 address."),
        IPV6("ipv6", "Expected an RFC2373 IP V6 address."),
        HOSTNAME("hostname", "Expected an RFC1035 hostname."),
        IDN_HOSTNAME("idn-hostname", "Expected an RFC5890 Section-2.3.2.3 IDN hostname."),
        EMAIL("email", "Expected an RFC5321 Section-4.1.2 Email string."),
        IDN_EMAIL("idn-email", "Expected an RFC6531 IDN Email string."),
        URI("uri", "Expected an absolute URI."),
        URI_REFERENCE("uri-reference", "Expected a URI reference."),
        IRI("iri", "Expected an absolute IRI."),
        IRI_REFERENCE("iri-reference", "Expected an IRI reference."),
        URI_TEMPLATE("uri-template", "Expected an RFC6570 URI Template."),
        JSON_POINTER("json-pointer", "Expected an RFC6901 JSON Pointer."),
        RELATIVE_JSON_POINTER(
                "relative-json-pointer",
                "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer)."),
        REGEX("regex", "Expected a regular expression specification."),
        // Numeric formats (a Corvus extension).
        BYTE("byte", null),
        UINT16("uint16", null),
        UINT32("uint32", null),
        UINT64("uint64", null),
        UINT128("uint128", null),
        SBYTE("sbyte", null),
        INT16("int16", null),
        INT32("int32", null),
        INT64("int64", null),
        INT128("int128", null),
        HALF("half", null),
        SINGLE("single", null),
        DOUBLE("double", null),
        DECIMAL("decimal", null);

        /** The canonical name, for messages. */
        final String formatName;
        /** The message for a string format failure (Corvus.Text.Json Strings.resx), or null. */
        final String message;

        Kind(String formatName, String message) {
            this.formatName = formatName;
            this.message = message;
        }

        boolean isNumeric() {
            return compareTo(BYTE) >= 0;
        }

        static Kind of(String format, Dialect dialect) {
            switch (format) {
                case "float":
                case "single":
                    return SINGLE;
                case "byte":
                    return BYTE;
                case "uint16":
                    return UINT16;
                case "uint32":
                    return UINT32;
                case "uint64":
                    return UINT64;
                case "uint128":
                    return UINT128;
                case "sbyte":
                    return SBYTE;
                case "int16":
                    return INT16;
                case "int32":
                    return INT32;
                case "int64":
                    return INT64;
                case "int128":
                    return INT128;
                case "half":
                    return HALF;
                case "double":
                    return DOUBLE;
                case "decimal":
                    return DECIMAL;
                case "date-time":
                    return DATE_TIME;
                case "email":
                    return EMAIL;
                case "hostname":
                    return HOSTNAME;
                case "ipv4":
                    return IPV4;
                case "ipv6":
                    return IPV6;
                case "uri":
                    return URI;
                case "uri-reference":
                    return atLeast(dialect, Dialect.DRAFT6, URI_REFERENCE);
                case "uri-template":
                    return atLeast(dialect, Dialect.DRAFT6, URI_TEMPLATE);
                case "json-pointer":
                    return atLeast(dialect, Dialect.DRAFT6, JSON_POINTER);
                case "date":
                    return atLeast(dialect, Dialect.DRAFT7, DATE);
                case "time":
                    return atLeast(dialect, Dialect.DRAFT7, TIME);
                case "regex":
                    return atLeast(dialect, Dialect.DRAFT7, REGEX);
                case "relative-json-pointer":
                    return atLeast(dialect, Dialect.DRAFT7, RELATIVE_JSON_POINTER);
                case "idn-email":
                    return atLeast(dialect, Dialect.DRAFT7, IDN_EMAIL);
                case "idn-hostname":
                    return atLeast(dialect, Dialect.DRAFT7, IDN_HOSTNAME);
                case "iri":
                    return atLeast(dialect, Dialect.DRAFT7, IRI);
                case "iri-reference":
                    return atLeast(dialect, Dialect.DRAFT7, IRI_REFERENCE);
                case "duration":
                    return atLeast(dialect, Dialect.DRAFT201909, DURATION);
                case "uuid":
                    return atLeast(dialect, Dialect.DRAFT201909, UUID);
                default:
                    return UNKNOWN;
            }
        }

        private static Kind atLeast(Dialect dialect, Dialect min, Kind kind) {
            return dialect.atLeast(min) ? kind : UNKNOWN;
        }

        /**
         * Asserts a string format over a reusable view of the string, with the evaluator's readers and scratch:
         * allocates nothing.
         */
        boolean check(Context c, CharSequence s, boolean legacyHostname) {
            int n = s.length();
            switch (this) {
                case DATE:
                    return date(s, 0, n);
                case TIME:
                    return time(s, 0, n);
                case DATE_TIME:
                    return n > 11 && (s.charAt(10) == 'T' || s.charAt(10) == 't') && date(s, 0, 10) && time(s, 11, n);
                case DURATION:
                    return duration(s);
                case UUID:
                    return uuid(s);
                case IPV4:
                    return ipv4(s, 0, n);
                case IPV6:
                    return ipv6(c, s, 0, n);
                case HOSTNAME:
                    return legacyHostname ? legacyHostName(s, 0, n) : hostname(c, s, 0, n);
                case EMAIL:
                    return email(c, s, false);
                case IDN_EMAIL:
                    return email(c, s, true);
                case IDN_HOSTNAME:
                    return c.idna.idnHostname(s, 0, n);
                case REGEX:
                    return c.regex.isValid(s);
                case URI:
                    return uri(s, false, false);
                case URI_REFERENCE:
                    return uri(s, false, true);
                case IRI:
                    return uri(s, true, false);
                case IRI_REFERENCE:
                    return uri(s, true, true);
                case URI_TEMPLATE:
                    return uriTemplate(s);
                case JSON_POINTER:
                    return jsonPointer(s, 0, n);
                case RELATIVE_JSON_POINTER:
                    return relativeJsonPointer(s);
                default:
                    return true;
            }
        }

        /** Asserts a numeric format. */
        boolean checkNumber(int flag, long bits) {
            switch (this) {
                case BYTE:
                    return intRange(flag, bits, 0, 255);
                case UINT16:
                    return intRange(flag, bits, 0, 65535);
                case UINT32:
                    return intRange(flag, bits, 0, 4294967295L);
                case UINT64:
                    if (flag == JsonDocument.NUM_U64) {
                        return true;
                    }
                    if (flag == JsonDocument.NUM_LONG) {
                        return bits >= 0;
                    }
                    return integral(bits, 0, 0x1p64 - 1);
                case UINT128:
                    if (flag == JsonDocument.NUM_U64) {
                        return true;
                    }
                    if (flag == JsonDocument.NUM_LONG) {
                        return bits >= 0;
                    }
                    return integral(bits, 0, 3.402823669209385e38);
                case SBYTE:
                    return intRange(flag, bits, -128, 127);
                case INT16:
                    return intRange(flag, bits, -32768, 32767);
                case INT32:
                    return intRange(flag, bits, Integer.MIN_VALUE, Integer.MAX_VALUE);
                case INT64:
                    if (flag == JsonDocument.NUM_LONG) {
                        return true;
                    }
                    if (flag == JsonDocument.NUM_U64) {
                        return false;
                    }
                    return integral(bits, -0x1p63, 0x1p63 - 1);
                case INT128:
                    if (flag != JsonDocument.NUM_DOUBLE) {
                        return true;
                    }
                    return integral(bits, -1.7014118346046923e38, 1.7014118346046923e38);
                case HALF:
                    return magnitude(flag, bits, 65504.0);
                case SINGLE:
                    return magnitude(flag, bits, 3.4028234663852886e38);
                case DOUBLE:
                    return magnitude(flag, bits, Double.MAX_VALUE);
                case DECIMAL:
                    return magnitude(flag, bits, 7.922816251426434e28);
                default:
                    return true;
            }
        }

        private static boolean intRange(int flag, long bits, long min, long max) {
            if (flag == JsonDocument.NUM_LONG) {
                return bits >= min && bits <= max;
            }
            if (flag == JsonDocument.NUM_U64) {
                return false;
            }
            return integral(bits, min, max);
        }

        private static boolean integral(long bits, double min, double max) {
            double f = Double.longBitsToDouble(bits);
            return !Double.isInfinite(f) && f == Math.floor(f) && f >= min && f <= max;
        }

        private static boolean magnitude(int flag, long bits, double max) {
            double f = flag == JsonDocument.NUM_DOUBLE
                    ? Double.longBitsToDouble(bits)
                    : flag == JsonDocument.NUM_LONG ? (double) bits : Numbers.u64ToDouble(bits);
            return !Double.isInfinite(f) && Math.abs(f) <= max;
        }
    }

    /** An evaluator's reusable state for format checks: scratch, and the readers that keep buffers. */
    static final class Context {
        final char[] scratch = new char[72];
        final Idna idna = new Idna();
        final EcmaRegex.Validator regex = new EcmaRegex.Validator();
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Over character sequences, by index (allocation-free)

    private static int digits(CharSequence s, int from, int to) {
        if (from >= to) {
            return -1;
        }
        int v = 0;
        for (int i = from; i < to; i++) {
            char c = s.charAt(i);
            if (c < '0' || c > '9') {
                return -1;
            }
            v = v * 10 + (c - '0');
        }
        return v;
    }

    static boolean date(CharSequence s, int from, int to) {
        if (to - from != 10 || s.charAt(from + 4) != '-' || s.charAt(from + 7) != '-') {
            return false;
        }
        int y = digits(s, from, from + 4);
        int m = digits(s, from + 5, from + 7);
        int d = digits(s, from + 8, from + 10);
        if (y < 0 || m < 0 || d < 0) {
            return false;
        }
        return m >= 1 && m <= 12 && d >= 1 && d <= (m == 2 && isLeapYear(y) ? 29 : DAYS[m]);
    }

    static boolean time(CharSequence s, int from, int to) {
        if (to - from < 9 || s.charAt(from + 2) != ':' || s.charAt(from + 5) != ':') {
            return false;
        }
        int h = digits(s, from, from + 2);
        int mi = digits(s, from + 3, from + 5);
        int sec = digits(s, from + 6, from + 8);
        if (h < 0 || mi < 0 || sec < 0) {
            return false;
        }
        int i = from + 8;
        if (s.charAt(i) == '.') {
            i++;
            int start = i;
            while (i < to && s.charAt(i) >= '0' && s.charAt(i) <= '9') {
                i++;
            }
            if (i == start) {
                return false;
            }
        }
        if (i >= to) {
            return false;
        }
        int oh = 0;
        int om = 0;
        int sign = 0;
        char c = s.charAt(i);
        if (c == 'z' || c == 'Z') {
            if (i + 1 != to) {
                return false;
            }
        } else if (c == '+' || c == '-') {
            if (to - i != 6 || s.charAt(i + 3) != ':') {
                return false;
            }
            sign = c == '-' ? 1 : -1;
            int x = digits(s, i + 1, i + 3);
            int y = digits(s, i + 4, i + 6);
            if (x < 0 || y < 0 || x > 23 || y > 59) {
                return false;
            }
            oh = x;
            om = y;
        } else {
            return false;
        }
        if (h > 23 || mi > 59 || sec > 60) {
            return false;
        }
        if (sec == 60) {
            int utc = (h * 60 + mi) + sign * (oh * 60 + om);
            return Math.floorMod(utc, 1440) == 23 * 60 + 59;
        }
        return true;
    }

    static boolean uuid(CharSequence s) {
        if (s.length() != 36) {
            return false;
        }
        for (int i = 0; i < 36; i++) {
            char c = s.charAt(i);
            if (i == 8 || i == 13 || i == 18 || i == 23) {
                if (c != '-') {
                    return false;
                }
            } else if (!isHex(c)) {
                return false;
            }
        }
        return true;
    }

    static boolean ipv4(CharSequence s, int from, int to) {
        int parts = 0;
        int start = from;
        while (true) {
            int end = start;
            while (end < to && s.charAt(end) != '.') {
                end++;
            }
            parts++;
            int len = end - start;
            if (parts > 4 || len == 0 || len > 3 || (len > 1 && s.charAt(start) == '0')) {
                return false;
            }
            int v = digits(s, start, end);
            if (v < 0 || v > 255) {
                return false;
            }
            if (end == to) {
                break;
            }
            start = end + 1;
        }
        return parts == 4;
    }

    static boolean ipv6(Context c, CharSequence s, int from, int to) {
        int n = to - from;
        if (n < 2 || n > 64) {
            return false;
        }
        boolean dotted = false;
        for (int i = from; i < to; i++) {
            char ch = s.charAt(i);
            if (!(isHex(ch) || ch == ':' || ch == '.')) {
                return false;
            }
            dotted |= ch == '.';
        }
        // An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place.
        char[] buf = c.scratch;
        int len;
        if (dotted) {
            int lastColon = -1;
            for (int i = to - 1; i >= from; i--) {
                if (s.charAt(i) == ':') {
                    lastColon = i;
                    break;
                }
            }
            if (lastColon < 0 || !ipv4(s, lastColon + 1, to)) {
                return false;
            }
            len = 0;
            for (int i = from; i <= lastColon; i++) {
                buf[len++] = s.charAt(i);
            }
            buf[len++] = '0';
            buf[len++] = ':';
            buf[len++] = '0';
        } else {
            len = 0;
            for (int i = from; i < to; i++) {
                buf[len++] = s.charAt(i);
            }
        }
        int dbl = -1;
        for (int i = 0; i + 1 < len; i++) {
            if (buf[i] == ':' && buf[i + 1] == ':') {
                if (dbl >= 0) {
                    return false;
                }
                dbl = i;
                i++;
            }
        }
        if (dbl >= 0) {
            // A second "::" overlapping the first (":::") also fails.
            if (dbl + 2 < len && buf[dbl + 2] == ':') {
                return false;
            }
            int l = groups(buf, 0, dbl);
            int r = groups(buf, dbl + 2, len);
            return l >= 0 && r >= 0 && l + r < 8;
        }
        return groups(buf, 0, len) == 8;
    }

    /** The number of valid hex groups in a colon-separated range, or -1. */
    private static int groups(char[] b, int from, int to) {
        if (from >= to) {
            return 0;
        }
        int n = 0;
        int start = from;
        for (int i = from; i <= to; i++) {
            if (i == to || b[i] == ':') {
                int len = i - start;
                if (len == 0 || len > 4) {
                    return -1;
                }
                for (int k = start; k < i; k++) {
                    if (!isHex(b[k])) {
                        return -1;
                    }
                }
                n++;
                start = i + 1;
            }
        }
        return n;
    }

    private static boolean ldhLabel(CharSequence s, int from, int to) {
        int n = to - from;
        if (n == 0 || n > 63 || !isAsciiAlnum(s.charAt(from)) || !isAsciiAlnum(s.charAt(to - 1))) {
            return false;
        }
        for (int i = from; i < to; i++) {
            char c = s.charAt(i);
            if (!isAsciiAlnum(c) && c != '-') {
                return false;
            }
        }
        return true;
    }

    private static boolean startsWithXn(CharSequence s, int from, int to) {
        return to - from >= 4
                && (s.charAt(from) | 0x20) == 'x'
                && (s.charAt(from + 1) | 0x20) == 'n'
                && s.charAt(from + 2) == '-'
                && s.charAt(from + 3) == '-';
    }

    static boolean legacyHostName(CharSequence s, int from, int to) {
        if (to == from || to - from > 253) {
            return false;
        }
        int start = from;
        for (int i = from; i <= to; i++) {
            if (i == to || s.charAt(i) == '.') {
                if (!ldhLabel(s, start, i)) {
                    return false;
                }
                start = i + 1;
            }
        }
        return true;
    }

    static boolean hostname(Context c, CharSequence s, int from, int to) {
        if (to == from || to - from > 253) {
            return false;
        }
        int start = from;
        for (int i = from; i <= to; i++) {
            if (i == to || s.charAt(i) == '.') {
                if (!ldhLabel(s, start, i)) {
                    return false;
                }
                // "--" in the third and fourth positions is reserved for A-labels (xn--).
                boolean xn = startsWithXn(s, start, i);
                if (i - start >= 4 && s.charAt(start + 2) == '-' && s.charAt(start + 3) == '-' && !xn) {
                    return false;
                }
                if (xn && !c.idna.punycodeLabelOk(s, start + 4, i)) {
                    return false;
                }
                start = i + 1;
            }
        }
        return true;
    }

    static boolean email(Context c, CharSequence s, boolean idn) {
        int n = s.length();
        int at = -1;
        for (int i = n - 1; i >= 0; i--) {
            if (s.charAt(i) == '@') {
                at = i;
                break;
            }
        }
        if (at <= 0) {
            return false;
        }
        if (!emailLocalPart(s, at, idn)) {
            return false;
        }
        int d = at + 1;
        if (n - d >= 2 && s.charAt(d) == '[' && s.charAt(n - 1) == ']') {
            int from = d + 1;
            int to = n - 1;
            if (to - from >= 5 && (s.charAt(from) | 0x20) == 'i' && (s.charAt(from + 1) | 0x20) == 'p'
                    && (s.charAt(from + 2) | 0x20) == 'v' && s.charAt(from + 3) == '6' && s.charAt(from + 4) == ':') {
                return ipv6(c, s, from + 5, to);
            }
            return ipv4(s, from, to);
        }
        return idn ? c.idna.idnHostname(s, d, n) : hostname(c, s, d, n);
    }

    private static boolean isLeapYear(int y) {
        return y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);
    }

    private static final int[] DAYS = {0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};

    private static boolean isHex(char c) {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    private static boolean isAsciiAlnum(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Durations, email local parts, URIs, IRIs, templates and pointers
    //
    // Each is read once from left to right, with no regular expression engine: the time is linear in the text and
    // nothing is kept on the stack for it. java.util.regex matched these by recursion, a level for each character of
    // a repetition of alternatives, and a string of some thousands of characters overflowed the stack of the thread.

    /**
     * Reads the components of the date or of the time of a duration from {@code i}: one or more of a run of digits
     * and a designator, the designators being consecutive ones of {@code a}, {@code b}, {@code c} in that order.
     * Returns where they end, or -1 when there is none or one is not well formed.
     */
    private static int durationComponents(CharSequence s, int i, int n, char a, char b, char c) {
        int last = -1;
        while (true) {
            int d = i;
            while (d < n && s.charAt(d) >= '0' && s.charAt(d) <= '9') {
                d++;
            }
            if (d == i) {
                return last < 0 ? -1 : i;
            }
            if (d == n) {
                return -1;
            }
            char u = s.charAt(d);
            int unit = u == a ? 0 : u == b ? 1 : u == c ? 2 : -1;
            if (unit < 0 || (last >= 0 && unit != last + 1)) {
                return -1;
            }
            last = unit;
            i = d + 1;
        }
    }

    /**
     * An ISO 8601 duration as RFC 3339 Appendix A writes it: {@code P} and a number of weeks, or the date
     * components, the time components after {@code T}, or both. The components present are consecutive: years,
     * months and days, and hours, minutes and seconds.
     */
    static boolean duration(CharSequence s) {
        int n = s.length();
        if (n < 3 || s.charAt(0) != 'P') {
            return false;
        }
        int i = 1;
        if (s.charAt(1) != 'T') {
            int d = 1;
            while (d < n && s.charAt(d) >= '0' && s.charAt(d) <= '9') {
                d++;
            }
            if (d > 1 && d == n - 1 && s.charAt(d) == 'W') {
                return true;
            }
            i = durationComponents(s, 1, n, 'Y', 'M', 'D');
            if (i < 0) {
                return false;
            }
            if (i == n) {
                return true;
            }
            if (s.charAt(i) != 'T') {
                return false;
            }
        }
        return durationComponents(s, i + 1, n, 'H', 'M', 'S') == n;
    }

    /** The characters RFC 5322 allows in an atom, other than letters and digits. */
    private static final String ATEXT_SPECIALS = "!#$%&'*+/=?^_`{|}~-";

    private static boolean isAtext(int c, boolean idn) {
        if (c < 0x80) {
            return isAsciiAlnum((char) c) || ATEXT_SPECIALS.indexOf(c) >= 0;
        }
        if (!idn) {
            return false;
        }
        // Letters, marks and numbers (the general categories L, M and N).
        switch (Character.getType(c)) {
            case Character.UPPERCASE_LETTER:
            case Character.LOWERCASE_LETTER:
            case Character.TITLECASE_LETTER:
            case Character.MODIFIER_LETTER:
            case Character.OTHER_LETTER:
            case Character.NON_SPACING_MARK:
            case Character.ENCLOSING_MARK:
            case Character.COMBINING_SPACING_MARK:
            case Character.DECIMAL_DIGIT_NUMBER:
            case Character.LETTER_NUMBER:
            case Character.OTHER_NUMBER:
                return true;
            default:
                return false;
        }
    }

    /**
     * The local part of an email address, s[0, to): atoms separated by single dots, or a quoted string, in which a
     * backslash takes the character after it (which is not a line terminator) and no quote, backslash, carriage
     * return or line feed stands by itself.
     */
    static boolean emailLocalPart(CharSequence s, int to, boolean idn) {
        if (to == 0) {
            return false;
        }
        if (s.charAt(0) == '"') {
            int i = 1;
            while (i < to) {
                char c = s.charAt(i);
                if (c == '"') {
                    return i == to - 1;
                }
                if (c == '\\') {
                    if (i + 1 >= to) {
                        return false;
                    }
                    char e = s.charAt(i + 1);
                    if (e == '\n' || e == '\r' || e == 0x85 || e == 0x2028 || e == 0x2029) {
                        return false;
                    }
                    i += 2;
                } else if (c == '\r' || c == '\n') {
                    return false;
                } else {
                    i++;
                }
            }
            return false;
        }
        boolean atom = false;
        for (int i = 0; i < to; ) {
            int c = Character.codePointAt(s, i);
            if (c == '.') {
                if (!atom) {
                    return false;
                }
                atom = false;
                i++;
            } else if (isAtext(c, idn)) {
                atom = true;
                i += Character.charCount(c);
            } else {
                return false;
            }
        }
        return atom;
    }

    // What an ASCII character may be in a URI (RFC 3986), as bits of URI_CLASS.
    private static final int UNRESERVED = 1;
    private static final int SUB_DELIM = 2;
    private static final int HEX = 4;
    private static final int SCHEME = 8;
    private static final int COLON = 16;
    private static final int AT = 32;
    private static final int SLASH = 64;
    private static final int QUESTION = 128;
    /** A character of a segment of a path, less the percent-encoded ones. */
    private static final int PCHAR = UNRESERVED | SUB_DELIM | COLON | AT;
    private static final byte[] URI_CLASS = new byte[128];

    static {
        for (int c = 0; c < 128; c++) {
            int bits = 0;
            boolean alpha = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
            boolean digit = c >= '0' && c <= '9';
            if (alpha || digit || c == '-' || c == '.' || c == '_' || c == '~') {
                bits |= UNRESERVED;
            }
            if ("!$&'()*+,;=".indexOf(c) >= 0) {
                bits |= SUB_DELIM;
            }
            if (digit || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')) {
                bits |= HEX;
            }
            if (alpha || digit || c == '+' || c == '-' || c == '.') {
                bits |= SCHEME;
            }
            bits |= c == ':' ? COLON : c == '@' ? AT : c == '/' ? SLASH : c == '?' ? QUESTION : 0;
            URI_CLASS[c] = (byte) bits;
        }
    }

    private static boolean is(char c, int bits) {
        return c < 128 && (URI_CLASS[c] & bits) != 0;
    }

    /** The characters beyond ASCII that RFC 3987 allows where a URI has an unreserved character (ucschar). */
    private static boolean isUcsChar(int c) {
        return (c >= 0xa0 && c <= 0xd7ff) || (c >= 0xf900 && c <= 0xfdcf) || (c >= 0xfdf0 && c <= 0xffef)
                || (c >= 0x10000 && c <= 0xefffd);
    }

    /** The private-use characters RFC 3987 allows in a query (iprivate). */
    private static boolean isPrivate(int c) {
        return (c >= 0xe000 && c <= 0xf8ff) || (c >= 0xf0000 && c <= 0xffffd) || (c >= 0x100000 && c <= 0x10fffd);
    }

    /**
     * Reads characters of s[i, to) that are of the ASCII classes {@code bits}, percent-encoded, or for an IRI
     * beyond ASCII and allowed (in a query, the private-use ones too). Returns where the first other character
     * stands, or {@code to}; -1 for a percent sign that two hexadecimal digits do not follow.
     */
    private static int uriRun(CharSequence s, int i, int to, int bits, boolean iri, boolean query) {
        while (i < to) {
            char c = s.charAt(i);
            if (c < 128) {
                if ((URI_CLASS[c] & bits) != 0) {
                    i++;
                } else if (c == '%') {
                    if (i + 2 >= to || !is(s.charAt(i + 1), HEX) || !is(s.charAt(i + 2), HEX)) {
                        return -1;
                    }
                    i += 3;
                } else {
                    return i;
                }
            } else {
                if (!iri) {
                    return i;
                }
                int cp = Character.codePointAt(s, i);
                if (!isUcsChar(cp) && !(query && isPrivate(cp))) {
                    return i;
                }
                i += Character.charCount(cp);
            }
        }
        return to;
    }

    /**
     * The IPv6address of RFC 3986 over s[from, to): groups of one to four hexadecimal digits, with the last two
     * written as an IPv4 address or not; eight groups in all, or fewer around one "::".
     */
    private static boolean uriIpv6(CharSequence s, int from, int to) {
        int i = from;
        int groups = 0;
        boolean compressed = false;
        if (i + 1 < to && s.charAt(i) == ':' && s.charAt(i + 1) == ':') {
            compressed = true;
            i += 2;
        }
        while (i < to) {
            int j = i;
            while (j < to && j - i < 4 && is(s.charAt(j), HEX)) {
                j++;
            }
            if (j < to && s.charAt(j) == '.') {
                if (!ipv4(s, i, to)) {
                    return false;
                }
                groups += 2;
                i = to;
                break;
            }
            if (j == i) {
                return false;
            }
            groups++;
            if (j == to) {
                i = j;
                break;
            }
            if (s.charAt(j) != ':') {
                return false;
            }
            if (j + 1 < to && s.charAt(j + 1) == ':') {
                if (compressed) {
                    return false;
                }
                compressed = true;
                i = j + 2;
            } else {
                i = j + 1;
                if (i == to) {
                    return false;
                }
            }
        }
        return compressed ? groups <= 7 : groups == 8;
    }

    /** The IPvFuture of RFC 3986 over s[from, to): v, hexadecimal digits, a dot, and one or more characters. */
    private static boolean uriIpvFuture(CharSequence s, int from, int to) {
        if (from >= to || s.charAt(from) != 'v') {
            return false;
        }
        int i = from + 1;
        while (i < to && is(s.charAt(i), HEX)) {
            i++;
        }
        if (i == from + 1 || i >= to || s.charAt(i) != '.' || i + 1 == to) {
            return false;
        }
        for (i++; i < to; i++) {
            if (!is(s.charAt(i), UNRESERVED | SUB_DELIM | COLON)) {
                return false;
            }
        }
        return true;
    }

    /**
     * The authority of a URI over s[from, to), which holds no slash, question mark or number sign: user
     * information and an at sign or neither, a host (an IP literal in brackets, or a registered name, which an IPv4
     * address is one of), and a colon and a port or neither.
     */
    private static boolean uriAuthority(CharSequence s, int from, int to, boolean iri) {
        int host = from;
        int i = uriRun(s, from, to, UNRESERVED | SUB_DELIM | COLON, iri, false);
        if (i < 0) {
            return false;
        }
        if (i < to && s.charAt(i) == '@') {
            host = i + 1;
        }
        if (host < to && s.charAt(host) == '[') {
            int close = host + 1;
            while (close < to && s.charAt(close) != ']') {
                close++;
            }
            if (close == to || !(uriIpv6(s, host + 1, close) || uriIpvFuture(s, host + 1, close))) {
                return false;
            }
            i = close + 1;
        } else {
            i = uriRun(s, host, to, UNRESERVED | SUB_DELIM, iri, false);
            if (i < 0) {
                return false;
            }
        }
        if (i < to && s.charAt(i) == ':') {
            for (i++; i < to && s.charAt(i) >= '0' && s.charAt(i) <= '9'; i++) {
            }
        }
        return i == to;
    }

    /**
     * What follows the scheme of a URI, or a relative reference, over s[from, n): an authority after two slashes
     * or none, a path, a query after a question mark or none, and a fragment after a number sign or none. The
     * first segment of the path of a relative reference with no authority holds no colon.
     */
    private static boolean uriAfterScheme(CharSequence s, int from, int n, boolean iri, boolean relative) {
        int i = from;
        if (i + 1 < n && s.charAt(i) == '/' && s.charAt(i + 1) == '/') {
            int end = i + 2;
            while (end < n && s.charAt(end) != '/' && s.charAt(end) != '?' && s.charAt(end) != '#') {
                end++;
            }
            if (!uriAuthority(s, i + 2, end, iri)) {
                return false;
            }
            i = end;
        } else if (relative) {
            for (int k = i; k < n; k++) {
                char c = s.charAt(k);
                if (c == '/' || c == '?' || c == '#') {
                    break;
                }
                if (c == ':') {
                    return false;
                }
            }
        }
        i = uriRun(s, i, n, PCHAR | SLASH, iri, false);
        if (i >= 0 && i < n && s.charAt(i) == '?') {
            i = uriRun(s, i + 1, n, PCHAR | SLASH | QUESTION, iri, true);
        }
        if (i >= 0 && i < n && s.charAt(i) == '#') {
            i = uriRun(s, i + 1, n, PCHAR | SLASH | QUESTION, iri, false);
        }
        return i == n;
    }

    /**
     * A URI (RFC 3986) or an IRI (RFC 3987): a scheme, a colon and what follows; for a reference, that or a
     * relative reference.
     */
    static boolean uri(CharSequence s, boolean iri, boolean reference) {
        int n = s.length();
        int colon = 0;
        while (colon < n && is(s.charAt(colon), SCHEME)) {
            colon++;
        }
        if (colon > 0 && colon < n && s.charAt(colon) == ':' && isAsciiAlpha(s.charAt(0))
                && uriAfterScheme(s, colon + 1, n, iri, false)) {
            return true;
        }
        return reference && uriAfterScheme(s, 0, n, iri, true);
    }

    private static boolean isAsciiAlpha(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
    }

    /**
     * Reads a variable of a URI template expression (RFC 6570) from {@code i}: letters, digits, underscores and
     * percent-encoded characters with single dots between them, then a colon and a length of one to four digits
     * that does not start with zero, an asterisk, or neither. Returns where it ends, or -1.
     */
    private static int templateVariable(CharSequence s, int i, int n) {
        boolean character = false;
        while (i < n) {
            char c = s.charAt(i);
            if (isAsciiAlnum(c) || c == '_') {
                i++;
            } else if (c == '%') {
                if (i + 2 >= n || !is(s.charAt(i + 1), HEX) || !is(s.charAt(i + 2), HEX)) {
                    return -1;
                }
                i += 3;
            } else if (c == '.' && character) {
                character = false;
                i++;
                continue;
            } else {
                break;
            }
            character = true;
        }
        if (!character) {
            return -1;
        }
        if (i < n && s.charAt(i) == '*') {
            return i + 1;
        }
        if (i < n && s.charAt(i) == ':') {
            int d = i + 1;
            if (d >= n || s.charAt(d) < '1' || s.charAt(d) > '9') {
                return -1;
            }
            for (d++; d < n && d < i + 5 && s.charAt(d) >= '0' && s.charAt(d) <= '9'; d++) {
            }
            return d;
        }
        return i;
    }

    /** A URI template (RFC 6570): literal characters, and expressions in braces. */
    static boolean uriTemplate(CharSequence s) {
        int n = s.length();
        int i = 0;
        while (i < n) {
            char c = s.charAt(i++);
            if (c == '{') {
                if (i < n && "+#./;?&=,!@|".indexOf(s.charAt(i)) >= 0) {
                    i++;
                }
                while (true) {
                    i = templateVariable(s, i, n);
                    if (i < 0 || i >= n) {
                        return false;
                    }
                    char e = s.charAt(i++);
                    if (e == '}') {
                        break;
                    }
                    if (e != ',') {
                        return false;
                    }
                }
            } else if (c <= 0x20 || "\"'<>\\^`|}".indexOf(c) >= 0) {
                return false;
            }
        }
        return true;
    }

    /** A JSON Pointer (RFC 6901) over s[from, to): tokens that each follow a slash, with ~ only as ~0 or ~1. */
    static boolean jsonPointer(CharSequence s, int from, int to) {
        if (from == to) {
            return true;
        }
        if (s.charAt(from) != '/') {
            return false;
        }
        for (int i = from + 1; i < to; i++) {
            if (s.charAt(i) == '~') {
                if (++i == to || (s.charAt(i) != '0' && s.charAt(i) != '1')) {
                    return false;
                }
            }
        }
        return true;
    }

    /** A relative JSON Pointer: a number with no zero before it, then a number sign or a JSON Pointer. */
    static boolean relativeJsonPointer(CharSequence s) {
        int n = s.length();
        if (n == 0) {
            return false;
        }
        int i = 0;
        char c = s.charAt(0);
        if (c == '0') {
            i = 1;
        } else if (c >= '1' && c <= '9') {
            for (i = 1; i < n && s.charAt(i) >= '0' && s.charAt(i) <= '9'; i++) {
            }
        } else {
            return false;
        }
        if (i < n && s.charAt(i) == '#') {
            return i + 1 == n;
        }
        return jsonPointer(s, i, n);
    }
}
