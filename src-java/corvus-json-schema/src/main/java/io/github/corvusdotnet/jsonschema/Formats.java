package io.github.corvusdotnet.jsonschema;

import java.util.regex.Pattern;

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
         * Asserts a string format over a reusable view of the string, with the evaluator's matchers and scratch:
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
                    return c.matcher(DURATION_RE, 0).reset(s).matches();
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
                    return c.matcher(URI_RE, 1).reset(s).matches();
                case URI_REFERENCE:
                    return c.matcher(URI_REF_RE, 2).reset(s).matches();
                case IRI:
                    return c.matcher(IRI_RE, 3).reset(s).matches();
                case IRI_REFERENCE:
                    return c.matcher(IRI_REF_RE, 4).reset(s).matches();
                case URI_TEMPLATE:
                    return c.matcher(URI_TEMPLATE_RE, 5).reset(s).matches();
                case JSON_POINTER:
                    return c.matcher(JSON_POINTER_RE, 6).reset(s).matches();
                case RELATIVE_JSON_POINTER:
                    return c.matcher(RELATIVE_JSON_POINTER_RE, 7).reset(s).matches();
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

    /** An evaluator's reusable state for format checks: regular expression matchers and scratch. */
    static final class Context {
        private final java.util.regex.Matcher[] matchers = new java.util.regex.Matcher[10];
        final char[] scratch = new char[72];
        final Idna idna = new Idna();
        final EcmaRegex.Validator regex = new EcmaRegex.Validator();

        java.util.regex.Matcher matcher(Pattern p, int slot) {
            java.util.regex.Matcher m = matchers[slot];
            if (m == null) {
                m = p.matcher("");
                matchers[slot] = m;
            }
            return m;
        }
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
        java.util.regex.Matcher m = (idn ? c.matcher(IDN_EMAIL_LOCAL_RE, 9) : c.matcher(EMAIL_LOCAL_RE, 8)).reset(s);
        m.region(0, at);
        if (!m.matches()) {
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

    private static final String DURATION_TIME = "(?:[0-9]+H(?:[0-9]+M(?:[0-9]+S)?)?|[0-9]+M(?:[0-9]+S)?|[0-9]+S)";
    static final Pattern DURATION_RE = Pattern.compile("P(?:[0-9]+W|(?:[0-9]+Y(?:[0-9]+M(?:[0-9]+D)?)?|[0-9]+M"
            + "(?:[0-9]+D)?|[0-9]+D)(?:T" + DURATION_TIME + ")?|T" + DURATION_TIME + ")");

    private static boolean isHex(char c) {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    private static boolean isAsciiAlnum(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Email

    private static final Pattern EMAIL_LOCAL_RE = Pattern.compile(
            "(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[^\"\\\\\\r\\n]|\\\\.)*\")");
    private static final Pattern IDN_EMAIL_LOCAL_RE = Pattern.compile("(?:[\\p{L}\\p{M}\\p{N}!#$%&'*+/=?^_`{|}~-]+"
            + "(?:\\.[\\p{L}\\p{M}\\p{N}!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[^\"\\\\\\r\\n]|\\\\.)*\")");

    // ----------------------------------------------------------------------------------------------------------------
    // URIs, IRIs, templates and pointers (RFC 3986 and RFC 3987 grammars)

    private static Pattern uriRegex(boolean iri, boolean reference) {
        String hex = "[0-9A-Fa-f]";
        String pct = "%" + hex + "{2}";
        String sub = "[!$&'()*+,;=]";
        String unreservedAscii = "[A-Za-z0-9\\-._~]";
        String ucschar = "[\\x{A0}-\\x{D7FF}\\x{F900}-\\x{FDCF}\\x{FDF0}-\\x{FFEF}\\x{10000}-\\x{EFFFD}]";
        String unreserved = iri ? "(?:" + unreservedAscii + "|" + ucschar + ")" : unreservedAscii;
        String pchar = "(?:" + unreserved + "|" + pct + "|" + sub + "|[:@])";
        String query = iri
                ? "(?:" + pchar + "|[/?]|[\\x{E000}-\\x{F8FF}\\x{F0000}-\\x{FFFFD}\\x{100000}-\\x{10FFFD}])*"
                : "(?:" + pchar + "|[/?])*";
        String fragment = "(?:" + pchar + "|[/?])*";
        String decOctet = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])";
        String ipv4 = decOctet + "(?:\\." + decOctet + "){3}";
        String h16 = hex + "{1,4}";
        String ls32 = "(?:" + h16 + ":" + h16 + "|" + ipv4 + ")";
        String ipv6 = "(?:(?:" + h16 + ":){6}" + ls32
                + "|::(?:" + h16 + ":){5}" + ls32
                + "|(?:" + h16 + ")?::(?:" + h16 + ":){4}" + ls32
                + "|(?:(?:" + h16 + ":){0,1}" + h16 + ")?::(?:" + h16 + ":){3}" + ls32
                + "|(?:(?:" + h16 + ":){0,2}" + h16 + ")?::(?:" + h16 + ":){2}" + ls32
                + "|(?:(?:" + h16 + ":){0,3}" + h16 + ")?::" + h16 + ":" + ls32
                + "|(?:(?:" + h16 + ":){0,4}" + h16 + ")?::" + ls32
                + "|(?:(?:" + h16 + ":){0,5}" + h16 + ")?::" + h16
                + "|(?:(?:" + h16 + ":){0,6}" + h16 + ")?::)";
        String ipLiteral = "\\[(?:" + ipv6 + "|v" + hex + "+\\.(?:" + unreservedAscii + "|" + sub + "|:)+)\\]";
        String regName = "(?:" + unreserved + "|" + pct + "|" + sub + ")*";
        String authority = "(?:(?:" + unreserved + "|" + pct + "|" + sub + "|:)*@)?(?:" + ipLiteral + "|" + ipv4 + "|"
                + regName + ")(?::[0-9]*)?";
        String segment = pchar + "*";
        String segmentNz = pchar + "+";
        String segmentNzNc = "(?:" + unreserved + "|" + pct + "|" + sub + "|@)+";
        String hierPart = "(?://" + authority + "(?:/" + segment + ")*|/(?:" + segmentNz + "(?:/" + segment + ")*)?|"
                + segmentNz + "(?:/" + segment + ")*|)";
        String relativePart = "(?://" + authority + "(?:/" + segment + ")*|/(?:" + segmentNz + "(?:/" + segment
                + ")*)?|" + segmentNzNc + "(?:/" + segment + ")*|)";
        String scheme = "[A-Za-z][A-Za-z0-9+\\-.]*";
        String absolute = scheme + ":" + hierPart + "(?:\\?" + query + ")?(?:#" + fragment + ")?";
        String relative = relativePart + "(?:\\?" + query + ")?(?:#" + fragment + ")?";
        return Pattern.compile(reference ? "(?:" + absolute + "|" + relative + ")" : absolute);
    }

    static final Pattern URI_RE = uriRegex(false, false);
    static final Pattern URI_REF_RE = uriRegex(false, true);
    static final Pattern IRI_RE = uriRegex(true, false);
    static final Pattern IRI_REF_RE = uriRegex(true, true);

    private static final String TEMPLATE_VAR =
            "(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*(?::[1-9][0-9]{0,3}|\\*)?";
    static final Pattern URI_TEMPLATE_RE = Pattern.compile("(?:[^\\x00-\\x20\"'<>\\\\^`{|}]|\\{[+#./;?&=,!@|]?"
            + TEMPLATE_VAR + "(?:," + TEMPLATE_VAR + ")*\\})*");
    static final Pattern JSON_POINTER_RE = Pattern.compile("(?:/(?:[^~/]|~[01])*)*");
    static final Pattern RELATIVE_JSON_POINTER_RE = Pattern.compile("(?:0|[1-9][0-9]*)(?:#|(?:/(?:[^~/]|~[01])*)*)");
}