package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.List;

/**
 * Matchers for the common shapes of {@code pattern} that decide them exactly without a regular expression engine,
 * reading the string's UTF-8 bytes (a port of the Rust port's class sequences, after the C# evaluator's pattern
 * matchers):
 *
 * <ul>
 *   <li>{@code .}, {@code .+}: the string has a character that is not a line terminator ({@code ^.}, {@code ^.+}:
 *       its first character is not one);
 *   <li>{@code ^.{m,n}$} and its relatives: between m and n characters, none a line terminator;
 *   <li>anchored sequences of quantified ASCII classes and literals ({@code ^[a-z][a-z0-9_]{0,29}$}, {@code ^x-},
 *       {@code ^[@$_#]}), matched greedily in one pass, which is exact because every variable item's set is
 *       disjoint from what can follow it.
 * </ul>
 */
final class PatternShapes {
    private PatternShapes() {
    }

    /** A set of characters: ASCII by bitmask, then all or none of U+2028/U+2029, and all or none of the rest. */
    static final class CharSet {
        final long lo;
        final long hi;
        final boolean nonAscii;
        final boolean separators;

        CharSet(long lo, long hi, boolean nonAscii, boolean separators) {
            this.lo = lo;
            this.hi = hi;
            this.nonAscii = nonAscii;
            this.separators = separators;
        }

        static final CharSet EMPTY = new CharSet(0, 0, false, false);

        static CharSet range(int from, int to) {
            long lo = 0;
            long hi = 0;
            for (int c = from; c <= to; c++) {
                if (c < 64) {
                    lo |= 1L << c;
                } else {
                    hi |= 1L << (c - 64);
                }
            }
            return new CharSet(lo, hi, false, false);
        }

        /** ECMA-262's {@code .}: everything but the line terminators. */
        static final CharSet DOT = new CharSet(~((1L << '\n') | (1L << '\r')), -1L, true, false);
        static final CharSet DIGIT = range('0', '9');
        static final CharSet WORD = range('0', '9').union(range('A', 'Z')).union(range('a', 'z')).union(range('_', '_'));

        CharSet union(CharSet o) {
            return new CharSet(lo | o.lo, hi | o.hi, nonAscii || o.nonAscii, separators || o.separators);
        }

        CharSet negate() {
            return new CharSet(~lo, ~hi, !nonAscii, !separators);
        }

        boolean disjoint(CharSet o) {
            return (lo & o.lo) == 0 && (hi & o.hi) == 0 && !(nonAscii && o.nonAscii) && !(separators && o.separators);
        }

        boolean hasAscii(int c) {
            return c < 64 ? (lo >>> c & 1) != 0 : (hi >>> (c - 64) & 1) != 0;
        }

        boolean contains(int c) {
            if (c < 128) {
                return hasAscii(c);
            }
            if (c == 0x2028 || c == 0x2029) {
                return separators;
            }
            return nonAscii;
        }

        int asciiCount() {
            return Long.bitCount(lo) + Long.bitCount(hi);
        }
    }

    /** One quantified set of a sequence; max is Integer.MAX_VALUE when unbounded. */
    static final class Item {
        final CharSet set;
        final int min;
        final int max;

        Item(CharSet set, int min, int max) {
            this.set = set;
            this.min = min;
            this.max = max;
        }
    }

    /** A matcher over a string's UTF-8 bytes. */
    interface Matcher {
        boolean matches(byte[] b, int off, int len, boolean ascii);
    }

    /** The code point at {@code i} of valid UTF-8 in the low half, its byte length in the high half. */
    static long decode(byte[] b, int i) {
        int c = b[i] & 0xff;
        if (c < 0x80) {
            return c | (1L << 32);
        }
        if (c < 0xe0) {
            return (((c & 0x1f) << 6) | (b[i + 1] & 0x3f)) | (2L << 32);
        }
        if (c < 0xf0) {
            return (((c & 0x0f) << 12) | ((b[i + 1] & 0x3f) << 6) | (b[i + 2] & 0x3f)) | (3L << 32);
        }
        return (((c & 0x07) << 18) | ((b[i + 1] & 0x3f) << 12) | ((b[i + 2] & 0x3f) << 6) | (b[i + 3] & 0x3f))
                | (4L << 32);
    }

    private static boolean isLineTerminator(int c) {
        return c == '\n' || c == '\r' || c == 0x2028 || c == 0x2029;
    }

    /** A matcher for the pattern, or null when its shape is not one of these. */
    static Matcher of(String pattern) {
        switch (pattern) {
            case ".":
            case ".+":
            case "(.+)":
                return (b, off, len, ascii) -> hasContent(b, off, len, false);
            case "^.":
            case "^.+":
                return (b, off, len, ascii) -> hasContent(b, off, len, true);
            default:
                break;
        }
        // ^X.* (no $) matches exactly where ^X does: .* can match nothing.
        if (pattern.endsWith(".*") && pattern.startsWith("^") && pattern.length() > 3) {
            String rest = pattern.substring(0, pattern.length() - 2);
            char last = rest.charAt(rest.length() - 1);
            if ("*+?}|(^".indexOf(last) < 0 && !endsWithEscape(rest)) {
                Matcher m = of(rest);
                if (m != null) {
                    return m;
                }
            }
        }
        int[] line = lineRange(pattern);
        if (line != null) {
            int min = line[0];
            int max = line[1];
            return (b, off, len, ascii) -> {
                int n = lineLength(b, off, len, ascii);
                return n >= min && n <= max;
            };
        }
        List<String> whole = wholeAlternatives(pattern);
        if (whole != null) {
            return literals(whole);
        }
        Matcher alternatives = alternatives(pattern);
        if (alternatives != null) {
            return alternatives;
        }
        Matcher list = SeparatedList.parse(pattern);
        if (list != null) {
            return list;
        }
        Matcher excluded = excludedClassWithWord(pattern);
        if (excluded != null) {
            return excluded;
        }
        Sequence seq = Sequence.parse(pattern);
        if (seq != null) {
            byte[] literal = seq.literal();
            if (literal != null) {
                int n = literal.length;
                if (seq.toEnd) {
                    return (b, off, len, ascii) -> len == n && java.util.Arrays.equals(b, off, off + n, literal, 0, n);
                }
                return (b, off, len, ascii) -> len >= n && java.util.Arrays.equals(b, off, off + n, literal, 0, n);
            }
            return seq;
        }
        return null;
    }

    private static boolean endsWithEscape(String t) {
        int n = 0;
        for (int i = t.length() - 1; i >= 0 && t.charAt(i) == '\\'; i--) {
            n++;
        }
        return n % 2 == 1;
    }

    private static boolean hasContent(byte[] b, int off, int len, boolean start) {
        int end = off + len;
        for (int i = off; i < end; ) {
            long cw = decode(b, i);
            i += (int) (cw >>> 32);
            if (!isLineTerminator((int) cw)) {
                return true;
            }
            if (start) {
                return false;
            }
        }
        return false;
    }

    /** The number of characters, or -1 when one is a line terminator. */
    private static int lineLength(byte[] b, int off, int len, boolean ascii) {
        int end = off + len;
        if (ascii) {
            for (int i = off; i < end; i++) {
                if (b[i] == '\n' || b[i] == '\r') {
                    return -1;
                }
            }
            return len;
        }
        int n = 0;
        for (int i = off; i < end; ) {
            long cw = decode(b, i);
            i += (int) (cw >>> 32);
            if (isLineTerminator((int) cw)) {
                return -1;
            }
            n++;
        }
        return n;
    }

    /** {@code ^.{m,n}$} (and {@code ^.*$}, {@code ^.+$}, each also as {@code ^(.…)$}): the bounds, or null. */
    private static int[] lineRange(String p) {
        String q;
        if (p.startsWith("^.") && p.endsWith("$")) {
            q = p.substring(2, p.length() - 1);
        } else if (p.startsWith("^(.") && p.endsWith(")$")) {
            q = p.substring(3, p.length() - 2);
        } else {
            return null;
        }
        if (q.isEmpty() || q.endsWith("?")) {
            return null;
        }
        int[] quantifier = parseQuantifier(q, 0);
        return quantifier != null && quantifier[2] == q.length() ? new int[] {quantifier[0], quantifier[1]} : null;
    }

    /** An optional quantifier at i: {min, max, next}; {1, 1, i} for none; null when malformed. */
    static int[] parseQuantifier(String p, int i) {
        int min;
        int max;
        char c = i < p.length() ? p.charAt(i) : 0;
        if (c == '*') {
            min = 0;
            max = Integer.MAX_VALUE;
            i++;
        } else if (c == '+') {
            min = 1;
            max = Integer.MAX_VALUE;
            i++;
        } else if (c == '?') {
            min = 0;
            max = 1;
            i++;
        } else if (c == '{') {
            int end = p.indexOf('}', i);
            if (end < 0) {
                return null;
            }
            String body = p.substring(i + 1, end);
            i = end + 1;
            try {
                int comma = body.indexOf(',');
                if (comma < 0) {
                    min = Integer.parseInt(body);
                    max = min;
                } else {
                    min = Integer.parseInt(body.substring(0, comma));
                    String hi = body.substring(comma + 1);
                    max = hi.isEmpty() ? Integer.MAX_VALUE : Integer.parseInt(hi);
                }
            } catch (NumberFormatException e) {
                return null;
            }
        } else {
            return new int[] {1, 1, i};
        }
        if (min < 0 || min > max) {
            return null;
        }
        if (i < p.length() && p.charAt(i) == '?') {
            // Laziness does not change whether the whole pattern matches.
            i++;
        }
        return new int[] {min, max, i};
    }

    /** The set of a class escape, or null for {@code \s} (not ASCII-only) and anything else. */
    static CharSet classEscape(char c) {
        switch (c) {
            case 'd':
                return CharSet.DIGIT;
            case 'D':
                return CharSet.DIGIT.negate();
            case 'w':
                return CharSet.WORD;
            case 'W':
                return CharSet.WORD.negate();
            case 'n':
                return CharSet.range('\n', '\n');
            case 'r':
                return CharSet.range('\r', '\r');
            case 't':
                return CharSet.range('\t', '\t');
            default:
                return c < 128 && isAsciiPunctuation(c) ? CharSet.range(c, c) : null;
        }
    }

    private static boolean isAsciiPunctuation(char c) {
        return (c >= 0x21 && c <= 0x2f) || (c >= 0x3a && c <= 0x40) || (c >= 0x5b && c <= 0x60)
                || (c >= 0x7b && c <= 0x7e);
    }

    /** A class body from after [ to after ]: {set, next} as an Object pair, or null. */
    static Object[] parseClass(String p, int i) {
        boolean negated = i < p.length() && p.charAt(i) == '^';
        if (negated) {
            i++;
        }
        CharSet set = CharSet.EMPTY;
        boolean first = true;
        while (true) {
            if (i >= p.length()) {
                return null;
            }
            char c = p.charAt(i);
            if (c == ']') {
                if (first) {
                    // [] and [^]
                    return null;
                }
                i++;
                break;
            }
            first = false;
            CharSet atom;
            int single;
            if (c == '\\') {
                if (i + 1 >= p.length()) {
                    return null;
                }
                char e = p.charAt(i + 1);
                atom = classEscape(e);
                if (atom == null) {
                    return null;
                }
                boolean isSingle = e == 'n' || e == 'r' || e == 't' || isAsciiPunctuation(e);
                single = isSingle ? (e == 'n' ? '\n' : e == 'r' ? '\r' : e == 't' ? '\t' : e) : -1;
                i += 2;
            } else {
                if (c >= 128) {
                    return null;
                }
                atom = CharSet.range(c, c);
                single = c;
                i++;
            }
            if (i + 1 < p.length() && p.charAt(i) == '-' && p.charAt(i + 1) != ']') {
                if (single < 0) {
                    return null;
                }
                char h = p.charAt(i + 1);
                int high;
                if (h == '\\') {
                    if (i + 2 >= p.length() || !isAsciiPunctuation(p.charAt(i + 2))) {
                        return null;
                    }
                    high = p.charAt(i + 2);
                    i += 3;
                } else {
                    if (h >= 128) {
                        return null;
                    }
                    high = h;
                    i += 2;
                }
                if (single > high) {
                    return null;
                }
                set = set.union(CharSet.range(single, high));
            } else {
                set = set.union(atom);
            }
        }
        return new Object[] {negated ? set.negate() : set, i};
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Literals and alternatives

    /** Literal text: characters other than syntax characters, and identity or control escapes; null otherwise. */
    static String literalText(String p) {
        StringBuilder out = new StringBuilder(p.length());
        for (int i = 0; i < p.length(); i++) {
            char c = p.charAt(i);
            if (c == '\\') {
                if (++i >= p.length()) {
                    return null;
                }
                char e = p.charAt(i);
                switch (e) {
                    case 'n':
                        out.append('\n');
                        break;
                    case 'r':
                        out.append('\r');
                        break;
                    case 't':
                        out.append('\t');
                        break;
                    case 'f':
                        out.append('\f');
                        break;
                    case 'v':
                        out.append('\u000b');
                        break;
                    default:
                        if ("^$\\.*+?()[]{}|/-".indexOf(e) < 0) {
                            return null;
                        }
                        out.append(e);
                        break;
                }
            } else if ("^$.*+?()[]{}|".indexOf(c) >= 0) {
                return null;
            } else {
                out.append(c);
            }
        }
        return out.toString();
    }

    /** Splits at top-level '|'s (none inside a group, a class or after a backslash); null when unbalanced. */
    static List<String> splitAlternatives(String p) {
        List<String> parts = new ArrayList<>();
        int depth = 0;
        boolean inClass = false;
        int start = 0;
        for (int i = 0; i < p.length(); i++) {
            char c = p.charAt(i);
            if (c == '\\') {
                i++;
            } else if (c == '[') {
                inClass = true;
            } else if (c == ']') {
                inClass = false;
            } else if (c == '(' && !inClass) {
                depth++;
            } else if (c == ')' && !inClass) {
                if (--depth < 0) {
                    return null;
                }
            } else if (c == '|' && !inClass && depth == 0) {
                parts.add(p.substring(start, i));
                start = i + 1;
            }
        }
        parts.add(p.substring(start));
        return parts;
    }

    /** {@code ^(a|b|...)$} or {@code ^(?:a|b|...)$} over literal alternatives: the literals, or null. */
    static List<String> wholeAlternatives(String p) {
        if (!p.startsWith("^(") || !p.endsWith(")$")) {
            return null;
        }
        String inner = p.substring(2, p.length() - 2);
        if (inner.startsWith("?:")) {
            inner = inner.substring(2);
        } else if (inner.startsWith("?")) {
            return null;
        }
        List<String> parts = splitAlternatives(inner);
        if (parts == null || parts.size() < 2) {
            return null;
        }
        List<String> out = new ArrayList<>();
        for (String part : parts) {
            String text = literalText(part);
            if (text == null) {
                return null;
            }
            out.add(text);
        }
        return out;
    }

    private static Matcher literals(List<String> texts) {
        byte[][] utf8 = texts.stream().map(t -> t.getBytes(java.nio.charset.StandardCharsets.UTF_8))
                .toArray(byte[][]::new);
        if (utf8.length <= 8) {
            return (b, off, len, ascii) -> {
                for (byte[] t : utf8) {
                    if (t.length == len && java.util.Arrays.equals(b, off, off + len, t, 0, len)) {
                        return true;
                    }
                }
                return false;
            };
        }
        NameMap map = new NameMap(utf8.length);
        for (byte[] t : utf8) {
            map.putIfAbsent(t, 0);
        }
        return (b, off, len, ascii) -> map.get(b, off, len) >= 0;
    }

    /** At most this many alternatives after expanding groups. */
    private static final int MAX_ALTERNATIVES = 64;
    /** At most this many alternatives when any is a class sequence. */
    private static final int MAX_SEQUENCE_ALTERNATIVES = 4;

    /**
     * Expands the alternatives from {@code start} up to an unmatched ')' or the end: groups of alternatives multiply
     * out, a group quantified by '?' also contributes the empty alternative, and an exact count repeats the group.
     * Returns the alternatives (with the end index in {@code end[0]}), or null for lookarounds, other quantified
     * groups, or too many alternatives.
     */
    static List<String> expand(String c, int start, int[] end) {
        List<String> all = new ArrayList<>();
        List<StringBuilder> branch = new ArrayList<>();
        branch.add(new StringBuilder());
        int i = start;
        while (i < c.length()) {
            char ch = c.charAt(i);
            if (ch == '|') {
                for (StringBuilder b : branch) {
                    all.add(b.toString());
                }
                branch.clear();
                branch.add(new StringBuilder());
                i++;
            } else if (ch == ')') {
                break;
            } else if (ch == '(') {
                i++;
                if (i < c.length() && c.charAt(i) == '?') {
                    if (i + 1 >= c.length() || c.charAt(i + 1) != ':') {
                        return null;
                    }
                    i += 2;
                }
                int[] next = new int[1];
                List<String> inner = expand(c, i, next);
                if (inner == null || next[0] >= c.length() || c.charAt(next[0]) != ')') {
                    return null;
                }
                i = next[0] + 1;
                char q = i < c.length() ? c.charAt(i) : 0;
                if (q == '?') {
                    inner.add("");
                    i++;
                    if (i < c.length() && c.charAt(i) == '?') {
                        i++;
                    }
                } else if (q == '{') {
                    // An exact count repeats the group; any other bound needs a real regex.
                    int close = c.indexOf('}', i);
                    if (close < 0) {
                        return null;
                    }
                    int n;
                    try {
                        n = Integer.parseInt(c.substring(i + 1, close));
                    } catch (NumberFormatException e) {
                        return null;
                    }
                    if (n > 16) {
                        return null;
                    }
                    List<String> repeated = new ArrayList<>();
                    repeated.add("");
                    for (int k = 0; k < n; k++) {
                        if ((long) repeated.size() * inner.size() > MAX_ALTERNATIVES) {
                            return null;
                        }
                        List<String> r = new ArrayList<>();
                        for (String a : repeated) {
                            for (String x : inner) {
                                r.add(a + x);
                            }
                        }
                        repeated = r;
                    }
                    inner = repeated;
                    i = close + 1;
                    if (i < c.length() && c.charAt(i) == '?') {
                        i++;
                    }
                } else if (q == '*' || q == '+') {
                    return null;
                }
                if ((long) branch.size() * inner.size() > MAX_ALTERNATIVES) {
                    return null;
                }
                List<StringBuilder> b2 = new ArrayList<>();
                for (StringBuilder b : branch) {
                    for (String x : inner) {
                        b2.add(new StringBuilder(b).append(x));
                    }
                }
                branch = b2;
            } else if (ch == '[') {
                int s0 = i;
                i++;
                if (i < c.length() && c.charAt(i) == '^') {
                    i++;
                }
                if (i < c.length() && c.charAt(i) == ']') {
                    i++;
                }
                while (true) {
                    if (i >= c.length()) {
                        return null;
                    }
                    if (c.charAt(i) == ']') {
                        break;
                    }
                    i += c.charAt(i) == '\\' ? 2 : 1;
                }
                i++;
                String text = c.substring(s0, Math.min(i, c.length()));
                for (StringBuilder b : branch) {
                    b.append(text);
                }
            } else if (ch == '\\') {
                if (i + 2 > c.length()) {
                    return null;
                }
                String text = c.substring(i, i + 2);
                for (StringBuilder b : branch) {
                    b.append(text);
                }
                i += 2;
            } else {
                for (StringBuilder b : branch) {
                    b.append(ch);
                }
                i++;
            }
            if (all.size() + branch.size() > MAX_ALTERNATIVES) {
                return null;
            }
        }
        for (StringBuilder b : branch) {
            all.add(b.toString());
        }
        end[0] = i;
        return all;
    }

    /**
     * Two or more alternatives once groups are expanded ({@code ^a|b|c$}, {@code ^([a|A]uto)|([n|N]one)$},
     * {@code ^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$}), each a literal or a class sequence anchored where its own
     * {@code ^} and {@code $} say; null otherwise.
     */
    static Matcher alternatives(String p) {
        int[] end = new int[1];
        List<String> parts = expand(p, 0, end);
        if (parts == null || end[0] != p.length() || parts.isEmpty() || (parts.size() == 1 && parts.get(0).equals(p))) {
            return null;
        }
        List<Matcher> alternatives = new ArrayList<>();
        boolean sequences = false;
        for (String part : parts) {
            boolean start = part.startsWith("^");
            String rest = start ? part.substring(1) : part;
            boolean atEnd = rest.endsWith("$") && !endsWithEscape(rest.substring(0, rest.length() - 1));
            String body = atEnd ? rest.substring(0, rest.length() - 1) : rest;
            String text = literalText(body);
            if (text != null) {
                byte[] t = text.getBytes(java.nio.charset.StandardCharsets.UTF_8);
                alternatives.add(literal(t, start, atEnd));
                continue;
            }
            sequences = true;
            Sequence seq = Sequence.parse("^" + body + (atEnd ? "$" : ""));
            if (seq == null) {
                return null;
            }
            if (start) {
                alternatives.add(seq);
                continue;
            }
            int width = 0;
            for (Item item : seq.items) {
                if (item.min != item.max) {
                    return null;
                }
                width += item.min;
            }
            int w = width;
            if (atEnd) {
                // Over the string's last w characters.
                alternatives.add((b, off, len, ascii) -> {
                    int at = ascii ? len - w : lastChars(b, off, len, w);
                    return at >= 0 && seq.matches(b, off + at, len - at, ascii);
                });
            } else {
                // At each position.
                alternatives.add((b, off, len, ascii) -> {
                    for (int at = 0; at <= len; ) {
                        if (seq.matches(b, off + at, len - at, ascii)) {
                            return true;
                        }
                        if (at == len) {
                            break;
                        }
                        at += ascii ? 1 : (int) (decode(b, off + at) >>> 32);
                    }
                    return false;
                });
            }
        }
        if (sequences && alternatives.size() > MAX_SEQUENCE_ALTERNATIVES) {
            return null;
        }
        Matcher[] all = alternatives.toArray(new Matcher[0]);
        return (b, off, len, ascii) -> {
            for (Matcher m : all) {
                if (m.matches(b, off, len, ascii)) {
                    return true;
                }
            }
            return false;
        };
    }

    /** The byte offset (from off) of the start of the last w characters, or -1 when there are fewer. */
    private static int lastChars(byte[] b, int off, int len, int w) {
        int at = len;
        for (int k = 0; k < w; k++) {
            if (at == 0) {
                return -1;
            }
            do {
                at--;
            } while (at > 0 && (b[off + at] & 0xc0) == 0x80);
        }
        return at;
    }

    private static Matcher literal(byte[] t, boolean start, boolean end) {
        int n = t.length;
        if (start && end) {
            return (b, off, len, ascii) -> len == n && java.util.Arrays.equals(b, off, off + n, t, 0, n);
        }
        if (start) {
            return (b, off, len, ascii) -> len >= n && java.util.Arrays.equals(b, off, off + n, t, 0, n);
        }
        if (end) {
            return (b, off, len, ascii) -> len >= n && java.util.Arrays.equals(b, off + len - n, off + len, t, 0, n);
        }
        return (b, off, len, ascii) -> {
            for (int at = 0; at + n <= len; at++) {
                if (java.util.Arrays.equals(b, off + at, off + at + n, t, 0, n)) {
                    return true;
                }
            }
            return false;
        };
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Excluded class with a word character

    /**
     * {@code ^(?=[^SET]+$)(?=(.*\w)).+$} (also with {@code (?:.*\w)} or {@code .*\w}): a non-empty line without a
     * character of SET, containing a word character. With {@code ^(?=!+[^SET]+$)…} (SET holding '!'), that line
     * follows one or more '!'.
     */
    static Matcher excludedClassWithWord(String p) {
        boolean bangs;
        String body;
        if (p.startsWith("^(?=!+[")) {
            bangs = true;
            body = p.substring(7);
        } else if (p.startsWith("^(?=[")) {
            bangs = false;
            body = p.substring(5);
        } else {
            return null;
        }
        if (!body.startsWith("^")) {
            return null;
        }
        Object[] parsed = parseClass(body, 0);
        if (parsed == null) {
            return null;
        }
        CharSet negated = (CharSet) parsed[0];
        CharSet excluded = new CharSet(~negated.lo, ~negated.hi, false, false);
        if (bangs && !excluded.hasAscii('!')) {
            return null;
        }
        String rest = body.substring((Integer) parsed[1]);
        if (!rest.equals("+$)(?=(.*\\w)).+$") && !rest.equals("+$)(?=(?:.*\\w)).+$") && !rest.equals("+$)(?=.*\\w).+$")) {
            return null;
        }
        return (b, off, len, ascii) -> {
            int at = 0;
            if (bangs) {
                while (at < len && b[off + at] == '!') {
                    at++;
                }
                if (at == 0 || at == len) {
                    return false;
                }
            }
            boolean word = false;
            for (int i = at; i < len; ) {
                long cw = decode(b, off + i);
                int c = (int) cw;
                i += (int) (cw >>> 32);
                if ((c < 128 && excluded.hasAscii(c)) || isLineTerminator(c)) {
                    return false;
                }
                word |= c < 128 && CharSet.WORD.hasAscii(c);
            }
            return word;
        };
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Separated lists

    /**
     * A list of items between separators: {@code ^I(SR)*$} or {@code ^I(SR)+$}, or {@code ^(RS)*F$} and
     * {@code ^(RS)+F$}. The separator is a fixed sequence and no variable class can run into what follows it, so
     * greedy matching splits the string exactly where the pattern does.
     */
    static final class SeparatedList implements Matcher {
        private final Sequence first;
        private final Sequence repeated;
        private final Sequence separator;
        private final Sequence last;
        private final int minRepeats;

        private SeparatedList(Sequence first, Sequence repeated, Sequence separator, Sequence last, int minRepeats) {
            this.first = first;
            this.repeated = repeated;
            this.separator = separator;
            this.last = last;
            this.minRepeats = minRepeats;
        }

        @Override
        public boolean matches(byte[] b, int off, int len, boolean ascii) {
            int at = off;
            int end = off + len;
            int repeats = 0;
            if (first != null) {
                int n = first.consume(b, at, end);
                if (n < 0) {
                    return false;
                }
                at += n;
                while (true) {
                    if (at == end) {
                        return repeats >= minRepeats;
                    }
                    n = separator.consume(b, at, end);
                    if (n < 0) {
                        return false;
                    }
                    at += n;
                    n = repeated.consume(b, at, end);
                    if (n < 0) {
                        return false;
                    }
                    at += n;
                    repeats++;
                }
            }
            while (true) {
                if (repeats >= minRepeats && last.matches(b, at, end - at, ascii)) {
                    return true;
                }
                int n = repeated.consume(b, at, end);
                if (n < 0) {
                    return false;
                }
                int m = separator.consume(b, at + n, end);
                if (m < 0) {
                    return false;
                }
                at += n + m;
                repeats++;
            }
        }

        private static String stripGroup(String g) {
            if (!g.startsWith("(") || !g.endsWith(")")) {
                return null;
            }
            String inner = g.substring(1, g.length() - 1);
            if (inner.startsWith("?:")) {
                return inner.substring(2);
            }
            return inner.startsWith("?") ? null : inner;
        }

        /** A text that is one group wrapping everything, unwrapped; otherwise the text itself; null when invalid. */
        private static String unwrapGroup(String t) {
            if (t.startsWith("(") && t.endsWith(")")) {
                String inner = stripGroup(t);
                if (inner == null) {
                    return null;
                }
                int depth = 0;
                boolean escaped = false;
                for (int i = 0; i < inner.length(); i++) {
                    char c = inner.charAt(i);
                    if (escaped) {
                        escaped = false;
                    } else if (c == '\\') {
                        escaped = true;
                    } else if (c == '(') {
                        depth++;
                    } else if (c == ')' && --depth < 0) {
                        return null;
                    }
                }
                return inner;
            }
            return t;
        }

        private static Item[] items(String t) {
            Sequence s = Sequence.parse("^" + t);
            return s == null ? null : s.items;
        }

        private static boolean fixed(Item[] items) {
            if (items.length == 0) {
                return false;
            }
            for (Item i : items) {
                if (i.min != i.max || i.min == 0) {
                    return false;
                }
            }
            return true;
        }

        static SeparatedList parse(String p) {
            if (!p.startsWith("^") || !p.endsWith("$")) {
                return null;
            }
            String body = p.substring(1, p.length() - 1);
            if (endsWithEscape(body)) {
                return null;
            }
            // Top-level groups: (start, end) of each, where end is the index of its ')'.
            List<int[]> groups = new ArrayList<>();
            int depth = 0;
            boolean inClass = false;
            int open = 0;
            for (int i = 0; i < body.length(); i++) {
                char c = body.charAt(i);
                if (c == '\\') {
                    i++;
                } else if (c == '[' && !inClass) {
                    inClass = true;
                } else if (c == ']' && inClass) {
                    inClass = false;
                } else if (c == '(' && !inClass) {
                    if (depth == 0) {
                        open = i;
                    }
                    depth++;
                } else if (c == ')' && !inClass) {
                    if (--depth < 0) {
                        return null;
                    }
                    if (depth == 0) {
                        groups.add(new int[] {open, i});
                    }
                }
            }
            int[] quantified = null;
            int count = 0;
            for (int[] g : groups) {
                char q = g[1] + 1 < body.length() ? body.charAt(g[1] + 1) : 0;
                if (q == '*' || q == '+') {
                    quantified = g;
                    count++;
                }
            }
            if (count != 1) {
                return null;
            }
            int close = quantified[1];
            int minRepeats = body.charAt(close + 1) == '+' ? 1 : 0;
            String group = stripGroup(body.substring(quantified[0], close + 1));
            if (group == null) {
                return null;
            }
            String before = body.substring(0, quantified[0]);
            String after = body.substring(close + 2);
            Item[] g = items(group);
            if (g == null) {
                return null;
            }
            if (!before.isEmpty() && after.isEmpty()) {
                // ^I(SR)*$
                String unwrapped = unwrapGroup(before);
                Item[] first = unwrapped == null ? null : items(unwrapped);
                if (first == null) {
                    return null;
                }
                for (int k = 1; k < g.length; k++) {
                    Item[] separator = java.util.Arrays.copyOfRange(g, 0, k);
                    Item[] repeated = java.util.Arrays.copyOfRange(g, k, g.length);
                    CharSet next = separator[0].set;
                    if (fixed(separator) && greedyBefore(first, next) && greedyBefore(repeated, next)) {
                        return new SeparatedList(new Sequence(first, false), new Sequence(repeated, false),
                                new Sequence(separator, false), null, minRepeats);
                    }
                }
                return null;
            }
            if (before.isEmpty() && !after.isEmpty()) {
                // ^(RS)*F$
                String unwrapped = unwrapGroup(after);
                Sequence last = unwrapped == null ? null : Sequence.parse("^" + unwrapped + "$");
                if (last == null) {
                    return null;
                }
                for (int k = 1; k < g.length; k++) {
                    Item[] repeated = java.util.Arrays.copyOfRange(g, 0, k);
                    Item[] separator = java.util.Arrays.copyOfRange(g, k, g.length);
                    if (fixed(separator) && greedyBefore(repeated, separator[0].set)) {
                        return new SeparatedList(null, new Sequence(repeated, false), new Sequence(separator, false),
                                new Sequence(last.items, true), minRepeats);
                    }
                }
            }
            return null;
        }
    }

    /**
     * Whether greedy matching is exact when {@code next} can follow the items: every variable item is disjoint from
     * the items that can directly follow it (up to the first that cannot match nothing), next included.
     */
    static boolean greedyBefore(Item[] items, CharSet next) {
        for (int i = 0; i < items.length; i++) {
            Item item = items[i];
            if (item.min == item.max) {
                continue;
            }
            boolean decided = false;
            for (int j = i + 1; j < items.length; j++) {
                if (!item.set.disjoint(items[j].set)) {
                    return false;
                }
                if (items[j].min > 0) {
                    decided = true;
                    break;
                }
            }
            if (!decided && !item.set.disjoint(next)) {
                return false;
            }
        }
        return true;
    }

    /**
     * {@code ^} then quantified character sets, optionally {@code $}; matched greedily, which is exact because every
     * variable item's set is disjoint from the next items' (a character the item leaves cannot be taken by them).
     */
    static final class Sequence implements Matcher {
        final Item[] items;
        final boolean toEnd;

        Sequence(Item[] items, boolean toEnd) {
            this.items = items;
            this.toEnd = toEnd;
        }

        static Sequence parse(String p) {
            if (!p.startsWith("^")) {
                return null;
            }
            for (int k = 0; k < p.length(); k++) {
                if (p.charAt(k) >= 128) {
                    return null;
                }
            }
            int i = 1;
            List<Item> items = new ArrayList<>();
            boolean toEnd = false;
            while (i < p.length()) {
                char c = p.charAt(i);
                CharSet set;
                if (c == '$' && i == p.length() - 1) {
                    toEnd = true;
                    i++;
                    break;
                } else if (c == '[') {
                    Object[] r = parseClass(p, i + 1);
                    if (r == null) {
                        return null;
                    }
                    set = (CharSet) r[0];
                    i = (Integer) r[1];
                } else if (c == '\\') {
                    if (i + 1 >= p.length()) {
                        return null;
                    }
                    set = classEscape(p.charAt(i + 1));
                    if (set == null) {
                        return null;
                    }
                    i += 2;
                } else if (c == '.') {
                    set = CharSet.DOT;
                    i++;
                } else if ("()|^$*+?{}]".indexOf(c) >= 0) {
                    return null;
                } else {
                    set = CharSet.range(c, c);
                    i++;
                }
                int[] q = parseQuantifier(p, i);
                if (q == null) {
                    return null;
                }
                i = q[2];
                items.add(new Item(set, q[0], q[1]));
            }
            if (i != p.length()) {
                return null;
            }
            for (int k = 0; k < items.size(); k++) {
                Item item = items.get(k);
                if (item.min == item.max) {
                    continue;
                }
                for (int j = k + 1; j < items.size(); j++) {
                    Item next = items.get(j);
                    if (!item.set.disjoint(next.set)) {
                        return null;
                    }
                    if (next.min > 0) {
                        break;
                    }
                }
            }
            return new Sequence(items.toArray(new Item[0]), toEnd);
        }

        /** The text, when every item is one fixed ASCII character; otherwise null. */
        byte[] literal() {
            byte[] out = new byte[items.length];
            for (int k = 0; k < items.length; k++) {
                Item it = items[k];
                if (it.min != 1 || it.max != 1 || it.set.nonAscii || it.set.separators || it.set.asciiCount() != 1) {
                    return null;
                }
                out[k] = (byte) (it.set.lo != 0
                        ? Long.numberOfTrailingZeros(it.set.lo)
                        : 64 + Long.numberOfTrailingZeros(it.set.hi));
            }
            return out;
        }

        /** Greedily matches the items at b[at, end) (ignoring toEnd): the bytes taken, or -1. */
        int consume(byte[] b, int at, int end) {
            int start = at;
            for (Item item : items) {
                int n = 0;
                while (n < item.max && at < end) {
                    long cw = decode(b, at);
                    if (!item.set.contains((int) cw)) {
                        break;
                    }
                    at += (int) (cw >>> 32);
                    n++;
                }
                if (n < item.min) {
                    return -1;
                }
            }
            return at - start;
        }

        @Override
        public boolean matches(byte[] b, int off, int len, boolean ascii) {
            int end = off + len;
            int at = off;
            if (ascii) {
                for (Item item : items) {
                    int start = at;
                    int limit = item.max == Integer.MAX_VALUE ? end : (int) Math.min(end, (long) start + item.max);
                    while (at < limit && item.set.hasAscii(b[at])) {
                        at++;
                    }
                    if (at - start < item.min) {
                        return false;
                    }
                }
                return !toEnd || at == end;
            }
            for (Item item : items) {
                int n = 0;
                while (n < item.max && at < end) {
                    long cw = decode(b, at);
                    if (!item.set.contains((int) cw)) {
                        break;
                    }
                    at += (int) (cw >>> 32);
                    n++;
                }
                if (n < item.min) {
                    return false;
                }
            }
            return !toEnd || at == end;
        }
    }
}