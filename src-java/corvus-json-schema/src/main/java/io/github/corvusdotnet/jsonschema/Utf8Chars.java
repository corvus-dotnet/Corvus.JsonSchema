package io.github.corvusdotnet.jsonschema;

/**
 * A reusable {@link CharSequence} over a string value of a document, for the regular expression engine: ASCII strings
 * are read in place, others are decoded into a buffer the view keeps. Not thread-safe; one per evaluation.
 */
final class Utf8Chars implements CharSequence {
    private byte[] bytes;
    private int offset;
    private int length;
    private char[] chars = new char[64];
    private boolean ascii;

    /** Views the string value {@code n} of {@code doc}. */
    Utf8Chars of(JsonDocument doc, int n) {
        byte[] b = doc.strBytes(n);
        int off = doc.strOffset(n);
        int len = doc.count(n);
        if (doc.strAscii(n)) {
            bytes = b;
            offset = off;
            length = len;
            ascii = true;
            return this;
        }
        if (chars.length < len) {
            chars = new char[Math.max(len, chars.length * 2)];
        }
        length = decode(b, off, off + len, chars);
        ascii = false;
        return this;
    }

    /** Decodes valid UTF-8 into UTF-16, returning the number of chars. */
    static int decode(byte[] b, int from, int to, char[] out) {
        int n = 0;
        int i = from;
        while (i < to) {
            int c = b[i] & 0xff;
            if (c < 0x80) {
                out[n++] = (char) c;
                i++;
            } else if (c < 0xe0) {
                out[n++] = (char) (((c & 0x1f) << 6) | (b[i + 1] & 0x3f));
                i += 2;
            } else if (c < 0xf0) {
                out[n++] = (char) (((c & 0x0f) << 12) | ((b[i + 1] & 0x3f) << 6) | (b[i + 2] & 0x3f));
                i += 3;
            } else {
                int cp = ((c & 0x07) << 18) | ((b[i + 1] & 0x3f) << 12) | ((b[i + 2] & 0x3f) << 6) | (b[i + 3] & 0x3f);
                out[n++] = Character.highSurrogate(cp);
                out[n++] = Character.lowSurrogate(cp);
                i += 4;
            }
        }
        return n;
    }

    @Override
    public int length() {
        return length;
    }

    @Override
    public char charAt(int index) {
        return ascii ? (char) bytes[offset + index] : chars[index];
    }

    @Override
    public CharSequence subSequence(int start, int end) {
        return toString().substring(start, end);
    }

    @Override
    public String toString() {
        return ascii
                ? new String(bytes, offset, length, java.nio.charset.StandardCharsets.ISO_8859_1)
                : new String(chars, 0, length);
    }
}
