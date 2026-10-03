package io.github.corvusdotnet.jsonschema;

/**
 * The IDNA checks of the {@code idn-hostname} and {@code idn-email} formats and of A-labels in {@code hostname}:
 * punycode (RFC 3492), the contextual and disallowed code points of RFC 5892 that the test suite exercises, and the
 * Bidi rule of RFC 5893 with Bidi classes approximated by script and general category. A port of the Rust and
 * TypeScript checks, over code point buffers the instance reuses, so a check allocates nothing once they have grown.
 * Not thread-safe; one per evaluator.
 */
final class Idna {
    /** The code points of the text being checked. */
    private int[] cps = new int[64];
    /** Label boundaries in {@link #cps}: start and end of each. */
    private int[] labels = new int[16];
    /** Decoded labels (the U-labels of A-labels), one after another, with their bounds in {@link #decodedAt}. */
    private int[] decoded = new int[64];
    private int[] decodedAt = new int[16];
    /** A re-encoded label (punycode output). */
    private char[] encoded = new char[64];
    private int encodedLength;

    private static int[] grow(int[] a, int need) {
        return need <= a.length ? a : java.util.Arrays.copyOf(a, Math.max(need, a.length * 2));
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Code point classes

    private static boolean isAsciiAlnum(int c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
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

    private static boolean script(int c, Character.UnicodeScript s) {
        return Character.UnicodeScript.of(c) == s;
    }

    private static boolean isArabicLike(int c) {
        Character.UnicodeScript s = Character.UnicodeScript.of(c);
        return s == Character.UnicodeScript.ARABIC
                || s == Character.UnicodeScript.SYRIAC
                || s == Character.UnicodeScript.THAANA
                || s == Character.UnicodeScript.NKO;
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

    private static final String DISALLOWED_EXCEPTIONS = "۽۾་·׳״・-";

    /**
     * Code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned, separators, uppercase
     * and titlecase letters (mapped away, never PVALID), symbols and punctuation; less a few contextual exceptions.
     */
    private static boolean disallowed(int c) {
        if (DISALLOWED_EXCEPTIONS.indexOf(c) >= 0) {
            return false;
        }
        switch (Character.getType(c)) {
            case Character.CONTROL:
            case Character.PRIVATE_USE:
            case Character.UNASSIGNED:
            case Character.SPACE_SEPARATOR:
            case Character.LINE_SEPARATOR:
            case Character.PARAGRAPH_SEPARATOR:
            case Character.UPPERCASE_LETTER:
            case Character.TITLECASE_LETTER:
            case Character.MATH_SYMBOL:
            case Character.OTHER_SYMBOL:
            case Character.CONNECTOR_PUNCTUATION:
            case Character.DASH_PUNCTUATION:
            case Character.START_PUNCTUATION:
            case Character.END_PUNCTUATION:
            case Character.INITIAL_QUOTE_PUNCTUATION:
            case Character.FINAL_QUOTE_PUNCTUATION:
            case Character.OTHER_PUNCTUATION:
                return true;
            default:
                return false;
        }
    }

    private static boolean anyDisallowed(int[] a, int from, int to) {
        for (int i = from; i < to; i++) {
            if (disallowed(a[i])) {
                return true;
            }
        }
        return false;
    }

    // RFC 5893 Bidi classes.
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
        if (Character.isLetter(c) || Character.getType(c) == Character.COMBINING_SPACING_MARK) {
            return L;
        }
        return ON;
    }

    /** A label that makes the domain a Bidi domain: an RTL character and an R, AL or AN class. */
    private static boolean bidiDomain(int[] a, int from, int to) {
        boolean rtl = false;
        boolean rightClass = false;
        for (int i = from; i < to; i++) {
            rtl |= isRtl(a[i]);
            int b = bidiClass(a[i]);
            rightClass |= b == R || b == AL || b == AN;
        }
        return rtl && rightClass;
    }

    private static boolean bidiLabelOk(int[] a, int from, int to, boolean isBidiDomain) {
        if (!isBidiDomain) {
            return true;
        }
        if (from == to) {
            return false;
        }
        int last = to - 1;
        while (last > from && bidiClass(a[last]) == NSM) {
            last--;
        }
        boolean hasL = false;
        boolean hasEn = false;
        boolean hasAn = false;
        boolean hasRAlAn = false;
        for (int i = from; i < to; i++) {
            int c = bidiClass(a[i]);
            hasL |= c == L;
            hasEn |= c == EN;
            hasAn |= c == AN;
            hasRAlAn |= c == R || c == AL || c == AN;
        }
        int first = bidiClass(a[from]);
        int end = bidiClass(a[last]);
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

    /** (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D}), approximated with Arabic. */
    private static boolean zwnjJoiningContext(int[] a, int from, int to, int i) {
        int l = i - 1;
        while (l >= from && isMn(a[l])) {
            l--;
        }
        int r = i + 1;
        while (r < to && isMn(a[r])) {
            r++;
        }
        return l >= from && r < to && isJoiner(a[l]) && isJoiner(a[r]);
    }

    private static boolean isKanaHan(int c) {
        Character.UnicodeScript s = Character.UnicodeScript.of(c);
        return s == Character.UnicodeScript.HIRAGANA
                || s == Character.UnicodeScript.KATAKANA
                || s == Character.UnicodeScript.HAN;
    }

    /** Contextual and disallowed code points from RFC 5892 that the test suite exercises. */
    private static boolean labelOk(int[] a, int from, int to) {
        int n = to - from;
        if (n == 0 || a[from] == '-' || a[to - 1] == '-') {
            return false;
        }
        if (n >= 4 && a[from + 2] == '-' && a[from + 3] == '-') {
            return false;
        }
        if (isMark(a[from])) {
            return false;
        }
        boolean arabicIndic = false;
        boolean extended = false;
        for (int i = from; i < to; i++) {
            arabicIndic |= a[i] >= 0x660 && a[i] <= 0x669;
            extended |= a[i] >= 0x6f0 && a[i] <= 0x6f9;
        }
        if (arabicIndic && extended) {
            return false;
        }
        for (int i = from; i < to; i++) {
            int c = a[i];
            if (c == 0x302e || c == 0x302f || c == 0x0640 || c == 0x07fa || (c >= 0x3031 && c <= 0x3035)
                    || c == 0x303b) {
                return false;
            }
            switch (c) {
                case 0x00b7:
                    if (!(i > from && i < to - 1 && a[i - 1] == 'l' && a[i + 1] == 'l')) {
                        return false;
                    }
                    break;
                case 0x0375:
                    if (!(i < to - 1 && script(a[i + 1], Character.UnicodeScript.GREEK))) {
                        return false;
                    }
                    break;
                case 0x05f3:
                case 0x05f4:
                    if (!(i > from && script(a[i - 1], Character.UnicodeScript.HEBREW))) {
                        return false;
                    }
                    break;
                case 0x30fb: {
                    boolean found = false;
                    for (int k = from; k < to; k++) {
                        found |= a[k] != 0x30fb && isKanaHan(a[k]);
                    }
                    if (!found) {
                        return false;
                    }
                    break;
                }
                case 0x200d:
                    if (i == from || !isVirama(a[i - 1])) {
                        return false;
                    }
                    break;
                case 0x200c:
                    if ((i == from || !isVirama(a[i - 1])) && !zwnjJoiningContext(a, from, to, i)) {
                        return false;
                    }
                    break;
                default:
                    break;
            }
        }
        return true;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Punycode (RFC 3492)

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

    private static char digit(int d) {
        return (char) (d < 26 ? d + 97 : d + 22);
    }

    private void putEncoded(char c) {
        if (encodedLength == encoded.length) {
            encoded = java.util.Arrays.copyOf(encoded, encoded.length * 2);
        }
        encoded[encodedLength++] = c;
    }

    /** Encodes a[from, to) into {@link #encoded}; returns the length. */
    private int encode(int[] a, int from, int to) {
        encodedLength = 0;
        for (int i = from; i < to; i++) {
            if (a[i] < 0x80) {
                putEncoded((char) a[i]);
            }
        }
        int basicLength = encodedLength;
        int h = basicLength;
        if (basicLength > 0) {
            putEncoded('-');
        }
        int n = 128;
        long delta = 0;
        int bias = 72;
        int count = to - from;
        while (h < count) {
            int m = Integer.MAX_VALUE;
            for (int i = from; i < to; i++) {
                if (a[i] >= n && a[i] < m) {
                    m = a[i];
                }
            }
            delta = Math.min(Integer.MAX_VALUE, delta + (long) (m - n) * (h + 1));
            n = m;
            for (int i = from; i < to; i++) {
                int c = a[i];
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
                        putEncoded(digit((int) (t + (q - t) % (BASE - t))));
                        q = (q - t) / (BASE - t);
                    }
                    putEncoded(digit((int) q));
                    bias = adapt((int) delta, h + 1, h == basicLength);
                    delta = 0;
                    h++;
                }
            }
            delta++;
            n++;
        }
        return encodedLength;
    }

    /**
     * Decodes the ASCII punycode s[from, to) (an A-label without its "xn--") onto the end of {@link #decoded} from
     * {@code at}; returns the number of code points, or -1 when invalid.
     */
    private int decode(CharSequence s, int from, int to, int at) {
        int n = 128;
        long i = 0;
        int bias = 72;
        int basic = from;
        for (int k = to - 1; k >= from; k--) {
            if (s.charAt(k) == '-') {
                basic = k;
                break;
            }
        }
        int length = 0;
        for (int k = from; k < basic; k++) {
            char c = s.charAt(k);
            if (c >= 0x80) {
                return -1;
            }
            decoded = grow(decoded, at + length + 1);
            decoded[at + length++] = c;
        }
        int index = basic > from ? basic + 1 : from;
        while (index < to) {
            long oldi = i;
            long w = 1;
            for (int k = BASE; ; k += BASE) {
                if (index >= to) {
                    return -1;
                }
                int c = s.charAt(index++);
                int d = c - 48 >= 0 && c - 48 < 10 ? c - 22
                        : c - 65 >= 0 && c - 65 < 26 ? c - 65
                        : c - 97 >= 0 && c - 97 < 26 ? c - 97 : BASE;
                if (d >= BASE) {
                    return -1;
                }
                i += d * w;
                if (i > 0xffffffffL) {
                    return -1;
                }
                int t = k <= bias ? T_MIN : k >= bias + T_MAX ? T_MAX : k - bias;
                if (d < t) {
                    break;
                }
                w *= BASE - t;
                if (w > 0xffffffffL) {
                    return -1;
                }
            }
            int len = length + 1;
            bias = adapt((int) (i - oldi), len, oldi == 0);
            n += (int) (i / len);
            i %= len;
            if (n > 0x10ffff || n < 0 || (n >= 0xd800 && n <= 0xdfff)) {
                return -1;
            }
            decoded = grow(decoded, at + length + 1);
            System.arraycopy(decoded, at + (int) i, decoded, at + (int) i + 1, length - (int) i);
            decoded[at + (int) i] = n;
            length++;
            i++;
        }
        return length;
    }

    /** Whether the encoding of decoded[from, to) is s[sFrom, sTo) in lower case (punycode is canonical). */
    private boolean encodesTo(int[] a, int from, int to, CharSequence s, int sFrom, int sTo) {
        int n = encode(a, from, to);
        if (n != sTo - sFrom) {
            return false;
        }
        for (int k = 0; k < n; k++) {
            char c = s.charAt(sFrom + k);
            if (c >= 'A' && c <= 'Z') {
                c += 32;
            }
            if (encoded[k] != c) {
                return false;
            }
        }
        return true;
    }

    /** An A-label's punycode s[from, to) (without "xn--"): it decodes to a valid, non-ASCII U-label. */
    boolean punycodeLabelOk(CharSequence s, int from, int to) {
        int n = decode(s, from, to, 0);
        if (n <= 0) {
            return false;
        }
        boolean ascii = true;
        for (int k = 0; k < n; k++) {
            ascii &= decoded[k] < 0x80;
        }
        if (ascii || !encodesTo(decoded, 0, n, s, from, to)) {
            return false;
        }
        return labelOk(decoded, 0, n) && !anyDisallowed(decoded, 0, n)
                && bidiLabelOk(decoded, 0, n, bidiDomain(decoded, 0, n));
    }

    private static boolean startsWithXn(int[] a, int from, int to) {
        return to - from >= 4 && (a[from] | 0x20) == 'x' && (a[from + 1] | 0x20) == 'n' && a[from + 2] == '-'
                && a[from + 3] == '-';
    }

    private static boolean hostnameLabelOk(int[] a, int from, int to) {
        int n = to - from;
        if (n == 0 || n > 63 || !isAsciiAlnum(a[from]) || !isAsciiAlnum(a[to - 1])) {
            return false;
        }
        for (int i = from; i < to; i++) {
            if (!isAsciiAlnum(a[i]) && a[i] != '-') {
                return false;
            }
        }
        // "--" in the third and fourth positions is reserved for A-labels (xn--).
        return !(n >= 4 && a[from + 2] == '-' && a[from + 3] == '-' && !startsWithXn(a, from, to));
    }

    /** The code points of an ASCII range of a[from, to) as a CharSequence, for {@link #decode}. */
    private final class Ascii implements CharSequence {
        int from;
        int to;

        @Override
        public int length() {
            return to - from;
        }

        @Override
        public char charAt(int index) {
            return (char) cps[from + index];
        }

        @Override
        public CharSequence subSequence(int start, int end) {
            throw new UnsupportedOperationException();
        }
    }

    private final Ascii ascii = new Ascii();

    /** The {@code idn-hostname} format over s[from, to). */
    boolean idnHostname(CharSequence s, int from, int to) {
        if (from == to) {
            return false;
        }
        // The code points, and the labels between separators: full stop, ideographic full stop, fullwidth full stop,
        // halfwidth ideographic full stop.
        int count = 0;
        int labelCount = 0;
        cps = grow(cps, to - from);
        labels = grow(labels, 2);
        labels[0] = 0;
        for (int i = from; i < to; ) {
            int c = Character.codePointAt(s, i);
            i += Character.charCount(c);
            if (c == '.' || c == 0x3002 || c == 0xff0e || c == 0xff61) {
                labels = grow(labels, 2 * labelCount + 4);
                labels[2 * labelCount + 1] = count;
                labelCount++;
                labels[2 * labelCount] = count;
                continue;
            }
            cps[count++] = c;
        }
        labels[2 * labelCount + 1] = count;
        labelCount++;

        // The U-label of each label (A-labels decoded), for the Bidi domain test.
        decodedAt = grow(decodedAt, 2 * labelCount);
        int used = 0;
        boolean isBidi = false;
        for (int l = 0; l < labelCount; l++) {
            int a = labels[2 * l];
            int b = labels[2 * l + 1];
            int n = -1;
            if (startsWithXn(cps, a, b)) {
                boolean allAscii = true;
                for (int k = a; k < b; k++) {
                    allAscii &= cps[k] < 0x80;
                }
                if (allAscii) {
                    ascii.from = a + 4;
                    ascii.to = b;
                    n = decode(ascii, 0, b - a - 4, used);
                }
            }
            if (n < 0) {
                decoded = grow(decoded, used + b - a);
                System.arraycopy(cps, a, decoded, used, b - a);
                n = b - a;
            }
            decodedAt[2 * l] = used;
            decodedAt[2 * l + 1] = used + n;
            used += n;
            isBidi |= bidiDomain(decoded, decodedAt[2 * l], decodedAt[2 * l + 1]);
        }

        int asciiLength = 0;
        for (int l = 0; l < labelCount; l++) {
            int a = labels[2 * l];
            int b = labels[2 * l + 1];
            if (a == b) {
                return false;
            }
            boolean allAscii = true;
            for (int k = a; k < b; k++) {
                allAscii &= cps[k] < 0x80;
            }
            if (allAscii) {
                if (!hostnameLabelOk(cps, a, b)) {
                    return false;
                }
                if (startsWithXn(cps, a, b)) {
                    ascii.from = a + 4;
                    ascii.to = b;
                    // decode() writes at the end of the decoded labels, which stay where they are.
                    int at = decodedAt[2 * labelCount - 1];
                    int n = decode(ascii, 0, b - a - 4, at);
                    boolean nonAscii = false;
                    for (int k = 0; k < n; k++) {
                        nonAscii |= decoded[at + k] >= 0x80;
                    }
                    if (n <= 0 || !nonAscii || !encodesTo(decoded, at, at + n, ascii, 0, b - a - 4)
                            || !labelOk(decoded, at, at + n) || anyDisallowed(decoded, at, at + n)
                            || !bidiLabelOk(decoded, at, at + n, bidiDomain(decoded, at, at + n))) {
                        return false;
                    }
                }
                if (!bidiLabelOk(decoded, decodedAt[2 * l], decodedAt[2 * l + 1], isBidi)) {
                    return false;
                }
                asciiLength += b - a + 1;
            } else {
                if (!labelOk(cps, a, b)) {
                    return false;
                }
                for (int k = a; k < b; k++) {
                    int c = cps[k];
                    if (c != 0x200c && c != 0x200d && (isControlFormatSpace(c) || disallowed(c))) {
                        return false;
                    }
                }
                if (!bidiLabelOk(cps, a, b, isBidi)) {
                    return false;
                }
                int aLabelLength = 4 + encode(cps, a, b);
                if (aLabelLength > 63) {
                    return false;
                }
                asciiLength += aLabelLength + 1;
            }
        }
        return asciiLength - 1 <= 253;
    }
}