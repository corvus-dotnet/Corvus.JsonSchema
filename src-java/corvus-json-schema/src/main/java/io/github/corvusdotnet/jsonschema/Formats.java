package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.List;
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

        /** Asserts a string format. {@code legacyHostname} selects the RFC 1123 host names of draft 4 and 6. */
        boolean checkString(String s, boolean legacyHostname) {
            switch (this) {
                case DATE:
                    return date(s);
                case TIME:
                    return time(s);
                case DATE_TIME:
                    return dateTime(s);
                case DURATION:
                    return DURATION_RE.matcher(s).matches();
                case UUID:
                    return uuid(s);
                case IPV4:
                    return ipv4(s);
                case IPV6:
                    return ipv6(s);
                case HOSTNAME:
                    return legacyHostname ? legacyHostName(s) : hostname(s);
                case IDN_HOSTNAME:
                    return idnHostname(s);
                case EMAIL:
                    return email(s, false);
                case IDN_EMAIL:
                    return email(s, true);
                case URI:
                    return URI_RE.matcher(s).matches();
                case URI_REFERENCE:
                    return URI_REF_RE.matcher(s).matches();
                case IRI:
                    return IRI_RE.matcher(s).matches();
                case IRI_REFERENCE:
                    return IRI_REF_RE.matcher(s).matches();
                case URI_TEMPLATE:
                    return URI_TEMPLATE_RE.matcher(s).matches();
                case JSON_POINTER:
                    return JSON_POINTER_RE.matcher(s).matches();
                case RELATIVE_JSON_POINTER:
                    return RELATIVE_JSON_POINTER_RE.matcher(s).matches();
                case REGEX:
                    return EcmaRegex.isValid(s);
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

    // ----------------------------------------------------------------------------------------------------------------
    // Dates and times

    private static boolean isLeapYear(int y) {
        return y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);
    }

    /** The value of the ASCII digits [from, to), or -1. */
    private static int digits(String s, int from, int to) {
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

    private static final int[] DAYS = {0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31};

    static boolean date(String s) {
        if (s.length() != 10 || s.charAt(4) != '-' || s.charAt(7) != '-') {
            return false;
        }
        int y = digits(s, 0, 4);
        int m = digits(s, 5, 7);
        int d = digits(s, 8, 10);
        if (y < 0 || m < 0 || d < 0) {
            return false;
        }
        return m >= 1 && m <= 12 && d >= 1 && d <= (m == 2 && isLeapYear(y) ? 29 : DAYS[m]);
    }

    static boolean time(String s) {
        int n = s.length();
        if (n < 9 || s.charAt(2) != ':' || s.charAt(5) != ':') {
            return false;
        }
        int h = digits(s, 0, 2);
        int mi = digits(s, 3, 5);
        int sec = digits(s, 6, 8);
        if (h < 0 || mi < 0 || sec < 0) {
            return false;
        }
        int i = 8;
        if (s.charAt(i) == '.') {
            i++;
            int start = i;
            while (i < n && s.charAt(i) >= '0' && s.charAt(i) <= '9') {
                i++;
            }
            if (i == start) {
                return false;
            }
        }
        if (i >= n) {
            return false;
        }
        int oh = 0;
        int om = 0;
        int sign = 0;
        char c = s.charAt(i);
        if (c == 'z' || c == 'Z') {
            if (i + 1 != n) {
                return false;
            }
        } else if (c == '+' || c == '-') {
            if (n - i != 6 || s.charAt(i + 3) != ':') {
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
            // A leap second is only valid at 23:59:60 UTC.
            int utc = (h * 60 + mi) + sign * (oh * 60 + om);
            return Math.floorMod(utc, 1440) == 23 * 60 + 59;
        }
        return true;
    }

    static boolean dateTime(String s) {
        return s.length() > 11
                && (s.charAt(10) == 'T' || s.charAt(10) == 't')
                && date(s.substring(0, 10))
                && time(s.substring(11));
    }

    private static final String DURATION_TIME = "(?:[0-9]+H(?:[0-9]+M(?:[0-9]+S)?)?|[0-9]+M(?:[0-9]+S)?|[0-9]+S)";
    static final Pattern DURATION_RE = Pattern.compile("P(?:[0-9]+W|(?:[0-9]+Y(?:[0-9]+M(?:[0-9]+D)?)?|[0-9]+M"
            + "(?:[0-9]+D)?|[0-9]+D)(?:T" + DURATION_TIME + ")?|T" + DURATION_TIME + ")");

    static boolean uuid(String s) {
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

    private static boolean isHex(char c) {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }

    // ----------------------------------------------------------------------------------------------------------------
    // IP addresses

    static boolean ipv4(String s) {
        int parts = 0;
        int start = 0;
        int n = s.length();
        while (true) {
            int end = s.indexOf('.', start);
            if (end < 0) {
                end = n;
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
            if (end == n) {
                break;
            }
            start = end + 1;
        }
        return parts == 4;
    }

    static boolean ipv6(String s) {
        // A valid address is at most 51 characters (an IPv4 tail after six groups).
        if (s.length() < 2 || s.length() > 64) {
            return false;
        }
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            if (!(isHex(c) || c == ':' || c == '.')) {
                return false;
            }
        }
        // An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place.
        String tail = s;
        if (s.indexOf('.') >= 0) {
            int lastColon = s.lastIndexOf(':');
            if (lastColon < 0 || !ipv4(s.substring(lastColon + 1))) {
                return false;
            }
            tail = s.substring(0, lastColon + 1) + "0:0";
        }
        int dbl = tail.indexOf("::");
        if (dbl >= 0) {
            if (tail.indexOf("::", dbl + 1) >= 0) {
                return false;
            }
            int l = groups(tail.substring(0, dbl));
            int r = groups(tail.substring(dbl + 2));
            return l >= 0 && r >= 0 && l + r < 8;
        }
        return groups(tail) == 8;
    }

    /** The number of valid hex groups in a colon-separated text, or -1. */
    private static int groups(String x) {
        if (x.isEmpty()) {
            return 0;
        }
        int n = 0;
        for (String p : x.split(":", -1)) {
            if (p.isEmpty() || p.length() > 4) {
                return -1;
            }
            for (int i = 0; i < p.length(); i++) {
                if (!isHex(p.charAt(i))) {
                    return -1;
                }
            }
            n++;
        }
        return n;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Host names

    private static boolean isAsciiAlnum(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    private static boolean ldhLabel(String l) {
        int n = l.length();
        if (n == 0 || n > 63 || !isAsciiAlnum(l.charAt(0)) || !isAsciiAlnum(l.charAt(n - 1))) {
            return false;
        }
        for (int i = 0; i < n; i++) {
            char c = l.charAt(i);
            if (!isAsciiAlnum(c) && c != '-') {
                return false;
            }
        }
        return true;
    }

    private static boolean startsWithXn(String l) {
        return l.length() >= 4 && l.regionMatches(true, 0, "xn--", 0, 4);
    }

    private static boolean hostnameLabelOk(String label) {
        if (!ldhLabel(label)) {
            return false;
        }
        // "--" in the third and fourth positions is reserved for A-labels (xn--).
        return !(label.length() >= 4 && label.charAt(2) == '-' && label.charAt(3) == '-' && !startsWithXn(label));
    }

    /** RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels. */
    static boolean legacyHostName(String s) {
        if (s.isEmpty() || s.length() > 253) {
            return false;
        }
        for (String l : s.split("\\.", -1)) {
            if (!ldhLabel(l)) {
                return false;
            }
        }
        return true;
    }

    static boolean hostname(String s) {
        if (s.isEmpty() || s.length() > 253) {
            return false;
        }
        for (String l : s.split("\\.", -1)) {
            if (!hostnameLabelOk(l) || (startsWithXn(l) && !punycodeLabelOk(l.substring(4)))) {
                return false;
            }
        }
        return true;
    }

    // Code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned, separators,
    // uppercase and titlecase letters (mapped away, never PVALID), symbols and punctuation.
    private static final Pattern DISALLOWED_RE =
            Pattern.compile("[\\p{Cc}\\p{Co}\\p{Cn}\\p{Zs}\\p{Zl}\\p{Zp}\\p{Lu}\\p{Lt}\\p{Sm}\\p{So}\\p{P}]");
    private static final String DISALLOWED_EXCEPTIONS = "۽۾་·׳״・-";

    private static boolean disallowed(String label) {
        StringBuilder stripped = new StringBuilder(label.length());
        label.codePoints().filter(c -> DISALLOWED_EXCEPTIONS.indexOf(c) < 0).forEach(stripped::appendCodePoint);
        return DISALLOWED_RE.matcher(stripped).find();
    }

    private static boolean isAscii(String s) {
        for (int i = 0; i < s.length(); i++) {
            if (s.charAt(i) >= 0x80) {
                return false;
            }
        }
        return true;
    }

    private static boolean punycodeLabelOk(String encoded) {
        String decoded = punycodeDecode(encoded);
        if (decoded == null || decoded.isEmpty() || isAscii(decoded)) {
            return false;
        }
        // The encoding must be canonical: re-encoding the U-label gives the same A-label.
        if (!punycodeEncode(decoded).equals(encoded.toLowerCase(java.util.Locale.ROOT))) {
            return false;
        }
        return idnLabelOk(decoded) && !disallowed(decoded) && bidiLabelOk(decoded, bidiDomain(decoded));
    }

    private static final int BASE = 36;
    private static final int T_MIN = 1;
    private static final int T_MAX = 26;
    private static final int SKEW = 38;
    private static final int DAMP = 700;

    private static int adapt(int delta, int numPoints, boolean firstTime) {
        delta = firstTime ? delta / DAMP : delta >>> 1;
        delta += delta / numPoints;
        int k = 0;
        while (delta > ((BASE - T_MIN) * T_MAX) >> 1) {
            delta /= BASE - T_MIN;
            k += BASE;
        }
        return k + ((BASE - T_MIN + 1) * delta) / (delta + SKEW);
    }

    private static char punyDigit(int d) {
        return (char) (d < 26 ? d + 97 : d + 22);
    }

    /** RFC 3492 encoding, for the canonical round-trip and A-label length checks. */
    static String punycodeEncode(String input) {
        int[] cps = input.codePoints().toArray();
        StringBuilder out = new StringBuilder();
        for (int c : cps) {
            if (c < 0x80) {
                out.append((char) c);
            }
        }
        int basicLength = out.length();
        int h = basicLength;
        if (basicLength > 0) {
            out.append('-');
        }
        int n = 128;
        long delta = 0;
        int bias = 72;
        while (h < cps.length) {
            int m = Integer.MAX_VALUE;
            for (int c : cps) {
                if (c >= n && c < m) {
                    m = c;
                }
            }
            delta = Math.min(Integer.MAX_VALUE, delta + (long) (m - n) * (h + 1));
            n = m;
            for (int c : cps) {
                if (c < n) {
                    delta = Math.min(Integer.MAX_VALUE, delta + 1);
                }
                if (c == n) {
                    long q = delta;
                    for (int k = BASE; ; k += BASE) {
                        int t = k <= bias ? T_MIN : k >= bias + T_MAX ? T_MAX : k - bias;
                        if (q < t) {
                            break;
                        }
                        out.append(punyDigit((int) (t + (q - t) % (BASE - t))));
                        q = (q - t) / (BASE - t);
                    }
                    out.append(punyDigit((int) q));
                    bias = adapt((int) delta, h + 1, h == basicLength);
                    delta = 0;
                    h++;
                }
            }
            delta++;
            n++;
        }
        return out.toString();
    }

    /** RFC 3492 decoding, enough to validate A-labels; null when invalid. */
    static String punycodeDecode(String input) {
        int n = 128;
        long i = 0;
        int bias = 72;
        List<Integer> output = new ArrayList<>();
        int basic = Math.max(input.lastIndexOf('-'), 0);
        for (int k = 0; k < basic; k++) {
            char c = input.charAt(k);
            if (c >= 0x80) {
                return null;
            }
            output.add((int) c);
        }
        int index = basic > 0 ? basic + 1 : 0;
        while (index < input.length()) {
            long oldi = i;
            long w = 1;
            for (int k = BASE; ; k += BASE) {
                if (index >= input.length()) {
                    return null;
                }
                int c = input.charAt(index++);
                int digit = c - 48 >= 0 && c - 48 < 10 ? c - 22
                        : c - 65 >= 0 && c - 65 < 26 ? c - 65
                        : c - 97 >= 0 && c - 97 < 26 ? c - 97 : BASE;
                if (digit >= BASE) {
                    return null;
                }
                i += digit * w;
                if (i > 0xffffffffL) {
                    return null;
                }
                int t = k <= bias ? T_MIN : k >= bias + T_MAX ? T_MAX : k - bias;
                if (digit < t) {
                    break;
                }
                w *= BASE - t;
                if (w > 0xffffffffL) {
                    return null;
                }
            }
            int len = output.size() + 1;
            bias = adapt((int) (i - oldi), len, oldi == 0);
            n += (int) (i / len);
            i %= len;
            if (n > 0x10ffff || n < 0) {
                return null;
            }
            output.add((int) i, n);
            i++;
        }
        StringBuilder sb = new StringBuilder(output.size());
        for (int cp : output) {
            if (cp >= 0xd800 && cp <= 0xdfff) {
                return null;
            }
            sb.appendCodePoint(cp);
        }
        return sb.toString();
    }

    private static boolean script(int c, Character.UnicodeScript s) {
        return Character.UnicodeScript.of(c) == s;
    }

    private static boolean isMn(int c) {
        return Character.getType(c) == Character.NON_SPACING_MARK;
    }

    private static boolean isMnMe(int c) {
        int t = Character.getType(c);
        return t == Character.NON_SPACING_MARK || t == Character.ENCLOSING_MARK;
    }

    private static boolean isMark(int c) {
        int t = Character.getType(c);
        return t == Character.NON_SPACING_MARK || t == Character.ENCLOSING_MARK || t == Character.COMBINING_SPACING_MARK;
    }

    private static boolean isArabicLike(int c) {
        Character.UnicodeScript s = Character.UnicodeScript.of(c);
        return s == Character.UnicodeScript.ARABIC
                || s == Character.UnicodeScript.SYRIAC
                || s == Character.UnicodeScript.THAANA
                || s == Character.UnicodeScript.NKO;
    }

    private static boolean isLetterOrMc(int c) {
        return Character.isLetter(c) || Character.getType(c) == Character.COMBINING_SPACING_MARK;
    }

    private static boolean isRtl(int c) {
        return script(c, Character.UnicodeScript.HEBREW)
                || isArabicLike(c)
                || (c >= 0x660 && c <= 0x669)
                || c == 0x66b
                || c == 0x66c;
    }

    private static boolean isControlFormatSpace(int c) {
        int t = Character.getType(c);
        return t == Character.CONTROL || t == Character.FORMAT || t == Character.SPACE_SEPARATOR
                || t == Character.UNASSIGNED;
    }

    // RFC 5893 Bidi classes, approximated by script and general category.
    private static final int L = 0;
    private static final int R = 1;
    private static final int AL = 2;
    private static final int AN = 3;
    private static final int EN = 4;
    private static final int NSM = 5;
    private static final int ON = 6;

    private static int bidiClass(int c) {
        if (isMnMe(c)) {
            return NSM;
        }
        if ((c >= 0x660 && c <= 0x669) || c == 0x66b || c == 0x66c) {
            return AN;
        }
        if ((c >= 0x30 && c <= 0x39) || (c >= 0x6f0 && c <= 0x6f9)) {
            return EN;
        }
        if (script(c, Character.UnicodeScript.HEBREW)) {
            return R;
        }
        if (isArabicLike(c)) {
            return AL;
        }
        if (isLetterOrMc(c)) {
            return L;
        }
        return ON;
    }

    private static boolean bidiDomain(String label) {
        return label.codePoints().anyMatch(Formats::isRtl)
                && label.codePoints().anyMatch(c -> {
                    int b = bidiClass(c);
                    return b == R || b == AL || b == AN;
                });
    }

    private static boolean bidiLabelOk(String label, boolean isBidiDomain) {
        if (!isBidiDomain) {
            return true;
        }
        int[] classes = label.codePoints().map(Formats::bidiClass).toArray();
        if (classes.length == 0) {
            return false;
        }
        int last = classes.length - 1;
        while (last > 0 && classes[last] == NSM) {
            last--;
        }
        boolean hasL = false;
        boolean hasEn = false;
        boolean hasAn = false;
        boolean hasRAlAn = false;
        for (int c : classes) {
            hasL |= c == L;
            hasEn |= c == EN;
            hasAn |= c == AN;
            hasRAlAn |= c == R || c == AL || c == AN;
        }
        int first = classes[0];
        int end = classes[last];
        if (first == R || first == AL) {
            return !hasL && (end == R || end == AL || end == EN || end == AN) && !(hasEn && hasAn);
        }
        if (first == L) {
            return !hasRAlAn && (end == L || end == EN);
        }
        return false;
    }

    private static final int[] VIRAMAS = {
        0x094d, 0x09cd, 0x0a4d, 0x0acd, 0x0b4d, 0x0bcd, 0x0c4d, 0x0ccd, 0x0d3b, 0x0d3c, 0x0d4d, 0x0dca, 0x0e3a, 0x0eba,
        0x0f84, 0x1039, 0x103a, 0x1714, 0x1734, 0x17d2, 0x1a60, 0x1b44, 0x1baa, 0x1bab, 0x1bf2, 0x1bf3, 0x2d7f, 0xa806,
        0xa8c4, 0xa953, 0xa9c0, 0xaaf6, 0xabed,
    };

    private static boolean isVirama(int c) {
        for (int v : VIRAMAS) {
            if (v == c) {
                return true;
            }
        }
        return false;
    }

    private static boolean isJoiner(int c) {
        return (c >= 0x0620 && c <= 0x064a) || (c >= 0x066e && c <= 0x06d3);
    }

    private static boolean zwnjJoiningContext(int[] cps, int i) {
        // (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D}), approximated with Arabic.
        int l = i - 1;
        while (l >= 0 && isMn(cps[l])) {
            l--;
        }
        int r = i + 1;
        while (r < cps.length && isMn(cps[r])) {
            r++;
        }
        return l >= 0 && r < cps.length && isJoiner(cps[l]) && isJoiner(cps[r]);
    }

    private static boolean isKanaHan(int c) {
        Character.UnicodeScript s = Character.UnicodeScript.of(c);
        return s == Character.UnicodeScript.HIRAGANA
                || s == Character.UnicodeScript.KATAKANA
                || s == Character.UnicodeScript.HAN;
    }

    /** Contextual and disallowed code points from RFC 5892 that the test suite exercises. */
    private static boolean idnLabelOk(String label) {
        if (label.isEmpty() || label.startsWith("-") || label.endsWith("-")) {
            return false;
        }
        int[] cps = label.codePoints().toArray();
        if (cps.length >= 4 && cps[2] == '-' && cps[3] == '-') {
            return false;
        }
        if (isMark(cps[0])) {
            return false;
        }
        boolean arabicIndic = false;
        boolean extended = false;
        for (int c : cps) {
            arabicIndic |= c >= 0x660 && c <= 0x669;
            extended |= c >= 0x6f0 && c <= 0x6f9;
        }
        if (arabicIndic && extended) {
            return false;
        }
        for (int i = 0; i < cps.length; i++) {
            int c = cps[i];
            if (c == 0x302e || c == 0x302f || c == 0x0640 || c == 0x07fa || (c >= 0x3031 && c <= 0x3035)
                    || c == 0x303b) {
                return false;
            }
            switch (c) {
                case 0x00b7:
                    if (!(i > 0 && i < cps.length - 1 && cps[i - 1] == 'l' && cps[i + 1] == 'l')) {
                        return false;
                    }
                    break;
                case 0x0375:
                    if (!(i < cps.length - 1 && script(cps[i + 1], Character.UnicodeScript.GREEK))) {
                        return false;
                    }
                    break;
                case 0x05f3:
                case 0x05f4:
                    if (!(i > 0 && script(cps[i - 1], Character.UnicodeScript.HEBREW))) {
                        return false;
                    }
                    break;
                case 0x30fb: {
                    boolean found = false;
                    for (int d : cps) {
                        if (d != 0x30fb && isKanaHan(d)) {
                            found = true;
                        }
                    }
                    if (!found) {
                        return false;
                    }
                    break;
                }
                case 0x200d:
                    if (i == 0 || !isVirama(cps[i - 1])) {
                        return false;
                    }
                    break;
                case 0x200c:
                    if ((i == 0 || !isVirama(cps[i - 1])) && !zwnjJoiningContext(cps, i)) {
                        return false;
                    }
                    break;
                default:
                    break;
            }
        }
        return true;
    }

    static boolean idnHostname(String s) {
        if (s.isEmpty()) {
            return false;
        }
        // Label separators: full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
        String[] labels = s.split("[.。．｡]", -1);
        String[] unicodeLabels = new String[labels.length];
        boolean isBidi = false;
        for (int i = 0; i < labels.length; i++) {
            String l = labels[i];
            String decoded = startsWithXn(l) ? punycodeDecode(l.substring(4)) : null;
            unicodeLabels[i] = decoded != null ? decoded : l;
            isBidi |= bidiDomain(unicodeLabels[i]);
        }
        int asciiLength = 0;
        for (int i = 0; i < labels.length; i++) {
            String label = labels[i];
            if (label.isEmpty()) {
                return false;
            }
            if (isAscii(label)) {
                if (!hostnameLabelOk(label)) {
                    return false;
                }
                if (startsWithXn(label) && !punycodeLabelOk(label.substring(4))) {
                    return false;
                }
                if (!bidiLabelOk(unicodeLabels[i], isBidi)) {
                    return false;
                }
                asciiLength += label.length() + 1;
            } else {
                if (!idnLabelOk(label)) {
                    return false;
                }
                StringBuilder withoutJoiners = new StringBuilder(label.length());
                label.codePoints().filter(c -> c != 0x200c && c != 0x200d).forEach(withoutJoiners::appendCodePoint);
                String w = withoutJoiners.toString();
                if (w.codePoints().anyMatch(Formats::isControlFormatSpace) || disallowed(w)) {
                    return false;
                }
                if (!bidiLabelOk(label, isBidi)) {
                    return false;
                }
                int aLabelLength = 4 + punycodeEncode(label).length();
                if (aLabelLength > 63) {
                    return false;
                }
                asciiLength += aLabelLength + 1;
            }
        }
        return asciiLength - 1 <= 253;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Email

    private static final Pattern EMAIL_LOCAL_RE = Pattern.compile(
            "(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[^\"\\\\\\r\\n]|\\\\.)*\")");
    private static final Pattern IDN_EMAIL_LOCAL_RE = Pattern.compile("(?:[\\p{L}\\p{M}\\p{N}!#$%&'*+/=?^_`{|}~-]+"
            + "(?:\\.[\\p{L}\\p{M}\\p{N}!#$%&'*+/=?^_`{|}~-]+)*|\"(?:[^\"\\\\\\r\\n]|\\\\.)*\")");

    static boolean email(String s, boolean idn) {
        int at = s.lastIndexOf('@');
        if (at <= 0) {
            return false;
        }
        String local = s.substring(0, at);
        String domain = s.substring(at + 1);
        if (!(idn ? IDN_EMAIL_LOCAL_RE : EMAIL_LOCAL_RE).matcher(local).matches()) {
            return false;
        }
        if (domain.startsWith("[") && domain.endsWith("]") && domain.length() >= 2) {
            String inner = domain.substring(1, domain.length() - 1);
            if (inner.length() >= 5 && inner.regionMatches(true, 0, "IPv6:", 0, 5)) {
                return ipv6(inner.substring(5));
            }
            return ipv4(inner);
        }
        return idn ? idnHostname(domain) : hostname(domain);
    }

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