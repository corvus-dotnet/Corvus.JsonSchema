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