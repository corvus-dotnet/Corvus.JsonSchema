package io.github.corvusdotnet.jsonschema;

import java.lang.invoke.MethodHandles;
import java.lang.invoke.VarHandle;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.HashSet;

/**
 * JSON text parsed for evaluation: the UTF-8 text and one flat array of values (a tape) that the evaluator reads in
 * place, with no object per value. Strings stay in the text where they have no escapes, so reading a document creates
 * no {@code String}.
 *
 * <p>A document is immutable and safe to share between threads. Parse instances with {@link #parse(String)} or
 * {@link #parse(byte[])} and pass them to {@link Validator#validate(JsonDocument)}; a document can be validated any
 * number of times.
 *
 * <p>Parsing is strict RFC 8259: anything but whitespace after the value, invalid UTF-8, lone surrogates in
 * {@code \\u} escapes, numbers out of the range of a double and nesting deeper than {@value #MAX_DEPTH} levels are
 * errors. Of duplicate property names, the last value is kept, at the position of the first.
 */
public final class JsonDocument {
    /** The deepest nesting of arrays and objects accepted. */
    public static final int MAX_DEPTH = 1000;

    // The kinds are the evaluator's type bits, so a type test is one mask operation.
    static final int NULL = 1;
    static final int BOOLEAN = 2;
    static final int OBJECT = 4;
    static final int ARRAY = 8;
    static final int NUMBER = 16;
    static final int STRING = 32;

    // Number representations (the flags of a number).
    static final int NUM_LONG = 0;
    /** An integer in [2^63, 2^64), held as the bits of an unsigned long. */
    static final int NUM_U64 = 1;
    static final int NUM_DOUBLE = 2;

    // String flags.
    /** The string's bytes are in {@link #text} (it had escapes), not in {@link #source}. */
    static final int STR_TEXT = 1;
    /** The string has bytes outside ASCII. */
    static final int STR_WIDE = 2;

    /**
     * Two longs per value. The first is the header: the kind in bits 0 to 7, flags in bits 8 to 15 and a 32-bit field
     * in the high half (a string's byte length, a number's offset in the source, a container's count). The second is
     * the data: a string's offset, a number's bits, a container's first child, a boolean's 0 or 1. The children of a
     * container are consecutive; an object's are key and value pairs.
     */
    final long[] tape;

    final byte[] source;
    /** The unescaped strings. */
    final byte[] text;
    final int root;

    private JsonDocument(byte[] source, long[] tape, byte[] text, int root) {
        this.source = source;
        this.tape = tape;
        this.text = text;
        this.root = root;
    }

    /**
     * Parses JSON text.
     *
     * @param json the text
     * @return the document
     * @throws JsonParseException if the text is not valid JSON
     */
    public static JsonDocument parse(String json) {
        return parse(json.getBytes(StandardCharsets.UTF_8));
    }

    /**
     * Parses UTF-8 JSON text. The document keeps the array: do not modify it afterwards.
     *
     * @param utf8 the text
     * @return the document
     * @throws JsonParseException if the text is not valid JSON
     */
    public static JsonDocument parse(byte[] utf8) {
        return new Parser(utf8).parse();
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Reading values (the evaluator's side). A value is the index of its node.

    final int root() {
        return root;
    }

    final int kind(int n) {
        return (int) tape[n << 1] & 0xff;
    }

    final int flags(int n) {
        return ((int) tape[n << 1] >>> 8) & 0xff;
    }

    /** A container's item or property count, or a string's byte length. */
    final int count(int n) {
        return (int) (tape[n << 1] >>> 32);
    }

    final long data(int n) {
        return tape[(n << 1) + 1];
    }

    /** A container's first child. */
    final int first(int n) {
        return (int) tape[(n << 1) + 1];
    }

    final boolean bool(int n) {
        return tape[(n << 1) + 1] != 0;
    }

    /** The array holding a string's bytes. */
    final byte[] strBytes(int n) {
        return (tape[n << 1] & (STR_TEXT << 8)) != 0 ? text : source;
    }

    final int strOffset(int n) {
        return (int) tape[(n << 1) + 1];
    }

    final boolean strAscii(int n) {
        return (tape[n << 1] & (STR_WIDE << 8)) == 0;
    }

    /** A string value as a Java string. */
    final String string(int n) {
        return new String(strBytes(n), strOffset(n), count(n), StandardCharsets.UTF_8);
    }

    /** Whether a string value equals UTF-8 bytes. */
    final boolean stringEquals(int n, byte[] utf8) {
        int len = count(n);
        if (len != utf8.length) {
            return false;
        }
        int off = strOffset(n);
        return Arrays.equals(strBytes(n), off, off + len, utf8, 0, len);
    }

    /** Whether two string values (of this document and another) are equal. */
    final boolean stringEquals(int n, JsonDocument other, int m) {
        int len = count(n);
        if (len != other.count(m)) {
            return false;
        }
        int a = strOffset(n);
        int b = other.strOffset(m);
        return Arrays.equals(strBytes(n), a, a + len, other.strBytes(m), b, b + len);
    }

    /** The value of an object's property, or -1. */
    final int property(int object, byte[] name) {
        int k = first(object);
        for (int i = count(object); i > 0; i--, k += 2) {
            if (stringEquals(k, name)) {
                return k + 1;
            }
        }
        return -1;
    }

    /** The value of an object's property, or -1. */
    final int property(int object, String name) {
        return property(object, name.getBytes(StandardCharsets.UTF_8));
    }

    /** A number's value as a double. */
    final double doubleValue(int n) {
        long bits = data(n);
        switch (flags(n)) {
            case NUM_LONG:
                return bits;
            case NUM_U64:
                return Numbers.u64ToDouble(bits);
            default:
                return Double.longBitsToDouble(bits);
        }
    }

    /** A number's text as written. */
    final String numberText(int n) {
        int start = count(n);
        return new String(source, start, Parser.numberEnd(source, start) - start, StandardCharsets.ISO_8859_1);
    }

    /** The value as JSON text (numbers as written, strings re-escaped). */
    final String toJson(int n) {
        StringBuilder sb = new StringBuilder();
        appendJson(sb, n);
        return sb.toString();
    }

    final void appendJson(StringBuilder sb, int n) {
        switch (kind(n)) {
            case NULL:
                sb.append("null");
                break;
            case BOOLEAN:
                sb.append(bool(n) ? "true" : "false");
                break;
            case NUMBER:
                sb.append(numberText(n));
                break;
            case STRING:
                appendQuoted(sb, string(n));
                break;
            case ARRAY: {
                sb.append('[');
                int c = first(n);
                for (int i = 0; i < count(n); i++) {
                    if (i > 0) {
                        sb.append(',');
                    }
                    appendJson(sb, c + i);
                }
                sb.append(']');
                break;
            }
            default: {
                sb.append('{');
                int c = first(n);
                for (int i = 0; i < count(n); i++) {
                    if (i > 0) {
                        sb.append(',');
                    }
                    appendQuoted(sb, string(c + 2 * i));
                    sb.append(':');
                    appendJson(sb, c + 2 * i + 1);
                }
                sb.append('}');
                break;
            }
        }
    }

    static void appendQuoted(StringBuilder sb, String s) {
        sb.append('"');
        for (int i = 0; i < s.length(); i++) {
            char c = s.charAt(i);
            switch (c) {
                case '"':
                    sb.append("\\\"");
                    break;
                case '\\':
                    sb.append("\\\\");
                    break;
                case '\n':
                    sb.append("\\n");
                    break;
                case '\r':
                    sb.append("\\r");
                    break;
                case '\t':
                    sb.append("\\t");
                    break;
                case '\b':
                    sb.append("\\b");
                    break;
                case '\f':
                    sb.append("\\f");
                    break;
                default:
                    if (c < 0x20) {
                        sb.append(String.format("\\u%04x", (int) c));
                    } else {
                        sb.append(c);
                    }
            }
        }
        sb.append('"');
    }

    @Override
    public String toString() {
        return toJson(root);
    }

    // ----------------------------------------------------------------------------------------------------------------
    // The parser

    private static final VarHandle LONGS = MethodHandles.byteArrayViewVarHandle(long[].class, ByteOrder.LITTLE_ENDIAN);

    private static final class Parser {
        private final byte[] b;
        private int i;
        /** Finished children of closed containers, each container's consecutive (two longs per node). */
        private long[] nodes = new long[64];
        private int nodeCount;
        /** The values of the open containers, innermost last; a container's run moves to nodes when it closes. */
        private long[] scratch = new long[64];
        private int scratchCount;
        /** The scratch index at which each open container's children start, with the object flag in bit 31. */
        private int[] frames = new int[16];
        private int depth;
        private byte[] text = EMPTY;
        private int textLength;

        private static final byte[] EMPTY = new byte[0];

        Parser(byte[] b) {
            this.b = b;
        }

        private JsonParseException error(String message) {
            return new JsonParseException(message, i);
        }

        private void skipWs() {
            byte[] b = this.b;
            int i = this.i;
            while (i < b.length) {
                byte c = b[i];
                if (c != ' ' && c != '\n' && c != '\r' && c != '\t') {
                    break;
                }
                i++;
            }
            this.i = i;
        }

        private int peek() {
            return i < b.length ? b[i] & 0xff : -1;
        }

        private void push(long header, long data) {
            if (scratchCount * 2 + 2 > scratch.length) {
                scratch = Arrays.copyOf(scratch, scratch.length * 2);
            }
            scratch[scratchCount * 2] = header;
            scratch[scratchCount * 2 + 1] = data;
            scratchCount++;
        }

        JsonDocument parse() {
            skipWs();
            while (true) {
                // A value.
                switch (peek()) {
                    case '{':
                        i++;
                        skipWs();
                        if (peek() == '}') {
                            i++;
                            checkDepth();
                            push(OBJECT, 0);
                        } else {
                            open(true);
                            key();
                            continue;
                        }
                        break;
                    case '[':
                        i++;
                        skipWs();
                        if (peek() == ']') {
                            i++;
                            checkDepth();
                            push(ARRAY, 0);
                        } else {
                            open(false);
                            continue;
                        }
                        break;
                    case '"':
                        string();
                        break;
                    case 't':
                        literal(TRUE, BOOLEAN, 1);
                        break;
                    case 'f':
                        literal(FALSE, BOOLEAN, 0);
                        break;
                    case 'n':
                        literal(NULL_WORD, NULL, 0);
                        break;
                    case '-':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        number();
                        break;
                    case -1:
                        throw error("unexpected end of input");
                    default:
                        throw error("expected a value");
                }
                // After a value: separators and closing brackets, until the next value or the end.
                boolean next = false;
                while (!next) {
                    if (depth == 0) {
                        skipWs();
                        if (i != b.length) {
                            throw error("trailing characters");
                        }
                        return finish();
                    }
                    int frame = frames[depth - 1];
                    boolean object = frame < 0;
                    skipWs();
                    int c = peek();
                    if (c == ',') {
                        i++;
                        skipWs();
                        if (object) {
                            key();
                        }
                        next = true;
                    } else if (c == '}' && object) {
                        i++;
                        close(frame & 0x7fffffff, true);
                    } else if (c == ']' && !object) {
                        i++;
                        close(frame, false);
                    } else if (c == -1) {
                        throw error("unexpected end of input");
                    } else {
                        throw error(object ? "expected ',' or '}'" : "expected ',' or ']'");
                    }
                }
            }
        }

        /** Appends the root after the other nodes and builds the document, exactly sized. */
        private JsonDocument finish() {
            int root = nodeCount;
            long[] tape = Arrays.copyOf(nodes, nodeCount * 2 + 2);
            tape[root * 2] = scratch[0];
            tape[root * 2 + 1] = scratch[1];
            byte[] t = textLength == 0 ? EMPTY : Arrays.copyOf(text, textLength);
            return new JsonDocument(b, tape, t, root);
        }

        private void open(boolean object) {
            checkDepth();
            if (depth == frames.length) {
                frames = Arrays.copyOf(frames, depth * 2);
            }
            frames[depth++] = scratchCount | (object ? 0x80000000 : 0);
        }

        /** An array or object may open at the current depth (an empty one counts). */
        private void checkDepth() {
            if (depth >= MAX_DEPTH) {
                throw error("nesting too deep");
            }
        }

        /** A property name and its colon, leaving the parser at the value. */
        private void key() {
            if (peek() != '"') {
                throw error("expected a property name");
            }
            string();
            skipWs();
            if (peek() != ':') {
                throw error("expected ':'");
            }
            i++;
            skipWs();
        }

        /** Moves the closed container's children to nodes and pushes the container in their place. */
        private void close(int start, boolean object) {
            depth--;
            if (object && scratchCount - start > 2) {
                dedupe(start);
            }
            int children = scratchCount - start;
            int first = nodeCount;
            if ((nodeCount + children) * 2 > nodes.length) {
                nodes = Arrays.copyOf(nodes, Math.max(nodes.length * 2, (nodeCount + children) * 2));
            }
            System.arraycopy(scratch, start * 2, nodes, nodeCount * 2, children * 2);
            nodeCount += children;
            scratchCount = start;
            long count = object ? children / 2 : children;
            push((object ? OBJECT : ARRAY) | (count << 32), first);
        }

        private boolean keyEquals(int a, int b) {
            long ha = scratch[a * 2];
            long hb = scratch[b * 2];
            int len = (int) (ha >>> 32);
            if (len != (int) (hb >>> 32)) {
                return false;
            }
            byte[] ba = (ha & (STR_TEXT << 8)) != 0 ? text : this.b;
            byte[] bb = (hb & (STR_TEXT << 8)) != 0 ? text : this.b;
            int oa = (int) scratch[a * 2 + 1];
            int ob = (int) scratch[b * 2 + 1];
            return Arrays.equals(ba, oa, oa + len, bb, ob, ob + len);
        }

        private String keyString(int k) {
            long h = scratch[k * 2];
            byte[] buf = (h & (STR_TEXT << 8)) != 0 ? text : this.b;
            return new String(buf, (int) scratch[k * 2 + 1], (int) (h >>> 32), StandardCharsets.UTF_8);
        }

        /**
         * Of duplicate property names in the object whose pairs start at {@code start}, keeps the last value at the
         * first position.
         */
        private void dedupe(int start) {
            int count = (scratchCount - start) / 2;
            boolean duplicate = false;
            if (count <= 16) {
                outer:
                for (int j = 1; j < count; j++) {
                    for (int k = 0; k < j; k++) {
                        if (keyEquals(start + 2 * k, start + 2 * j)) {
                            duplicate = true;
                            break outer;
                        }
                    }
                }
            } else {
                HashSet<String> seen = new HashSet<>();
                for (int j = 0; j < count; j++) {
                    if (!seen.add(keyString(start + 2 * j))) {
                        duplicate = true;
                        break;
                    }
                }
            }
            if (!duplicate) {
                return;
            }
            long[] pairs = Arrays.copyOfRange(scratch, start * 2, scratchCount * 2);
            scratchCount = start;
            for (int p = 0; p < count; p++) {
                int keyIndex = -1;
                long[] copy = scratch;
                for (int q = start; q < scratchCount; q += 2) {
                    // Compare the candidate (held in pairs) with a kept key (in scratch).
                    long hp = pairs[p * 4];
                    long hq = copy[q * 2];
                    int len = (int) (hp >>> 32);
                    if (len != (int) (hq >>> 32)) {
                        continue;
                    }
                    byte[] bp = (hp & (STR_TEXT << 8)) != 0 ? text : this.b;
                    byte[] bq = (hq & (STR_TEXT << 8)) != 0 ? text : this.b;
                    int op = (int) pairs[p * 4 + 1];
                    int oq = (int) copy[q * 2 + 1];
                    if (Arrays.equals(bp, op, op + len, bq, oq, oq + len)) {
                        keyIndex = q;
                        break;
                    }
                }
                if (keyIndex >= 0) {
                    scratch[(keyIndex + 1) * 2] = pairs[p * 4 + 2];
                    scratch[(keyIndex + 1) * 2 + 1] = pairs[p * 4 + 3];
                } else {
                    push(pairs[p * 4], pairs[p * 4 + 1]);
                    push(pairs[p * 4 + 2], pairs[p * 4 + 3]);
                }
            }
        }

        private static final byte[] TRUE = {'t', 'r', 'u', 'e'};
        private static final byte[] FALSE = {'f', 'a', 'l', 's', 'e'};
        private static final byte[] NULL_WORD = {'n', 'u', 'l', 'l'};

        private void literal(byte[] word, int kind, long data) {
            if (i + word.length > b.length || !Arrays.equals(b, i, i + word.length, word, 0, word.length)) {
                throw error("expected a value");
            }
            i += word.length;
            push(kind, data);
        }

        /** Bytes equal to n in a word (exact at the lowest set bit, which is all the scanner uses). */
        private static long eqBytes(long w, long n) {
            long x = w ^ (0x0101010101010101L * n);
            return (x - 0x0101010101010101L) & ~x & 0x8080808080808080L;
        }

        /** Bytes below 0x20 in a word (exact at the lowest set bit). */
        private static long controlBytes(long w) {
            return (w - 0x2020202020202020L) & ~w & 0x8080808080808080L;
        }

        /** A string, from its opening quote. */
        private void string() {
            byte[] b = this.b;
            int start = i + 1;
            int j = start;
            long wide = 0;
            // Eight bytes at a time to the first quote, backslash or control character.
            while (j + 8 <= b.length) {
                long w = (long) LONGS.get(b, j);
                long special = eqBytes(w, '"') | eqBytes(w, '\\') | controlBytes(w);
                if (special != 0) {
                    int at = Long.numberOfTrailingZeros(special) >>> 3;
                    // Only the bytes before the special one count towards the string.
                    wide |= w & (at == 0 ? 0 : (-1L >>> (64 - 8 * at))) & 0x8080808080808080L;
                    j += at;
                    stringAt(start, j, wide != 0);
                    return;
                }
                wide |= w & 0x8080808080808080L;
                j += 8;
            }
            while (j < b.length) {
                int c = b[j] & 0xff;
                if (c == '"' || c == '\\' || c < 0x20) {
                    break;
                }
                wide |= c & 0x80;
                j++;
            }
            stringAt(start, j, wide != 0);
        }

        private void stringAt(int start, int j, boolean wide) {
            if (j >= b.length) {
                i = j;
                throw error("unterminated string");
            }
            byte c = b[j];
            if (c == '"') {
                if (wide) {
                    validateUtf8(start, j);
                }
                i = j + 1;
                push(STRING | (wide ? STR_WIDE << 8 : 0) | ((long) (j - start) << 32), start);
            } else if (c == '\\') {
                escaped(start, j, wide);
            } else {
                i = j;
                throw error("control character in a string");
            }
        }

        private void validateUtf8(int start, int end) {
            int at = Utf8.invalidAt(b, start, end);
            if (at >= 0) {
                i = at;
                throw error("invalid UTF-8");
            }
        }

        private void appendText(byte[] from, int start, int end) {
            int len = end - start;
            ensureText(len);
            System.arraycopy(from, start, text, textLength, len);
            textLength += len;
        }

        private void ensureText(int more) {
            if (textLength + more > text.length) {
                text = Arrays.copyOf(text, Math.max(64, Math.max(text.length * 2, textLength + more)));
            }
        }

        private void appendByte(int c) {
            ensureText(1);
            text[textLength++] = (byte) c;
        }

        /** The rest of a string with escapes, unescaped into the text buffer: j is at the first backslash. */
        private void escaped(int start, int j, boolean wide) {
            int offset = textLength;
            byte[] b = this.b;
            int run = start;
            while (true) {
                if (j >= b.length) {
                    i = j;
                    throw error("unterminated string");
                }
                int c = b[j] & 0xff;
                if (c == '"') {
                    if (wide) {
                        validateUtf8(run, j);
                    }
                    appendText(b, run, j);
                    i = j + 1;
                    long len = textLength - offset;
                    push(STRING | (STR_TEXT << 8) | (wide ? STR_WIDE << 8 : 0) | (len << 32), offset);
                    return;
                } else if (c == '\\') {
                    if (wide) {
                        validateUtf8(run, j);
                    }
                    appendText(b, run, j);
                    int e = j + 1 < b.length ? b[j + 1] & 0xff : -1;
                    int out;
                    switch (e) {
                        case '"':
                            out = '"';
                            break;
                        case '\\':
                            out = '\\';
                            break;
                        case '/':
                            out = '/';
                            break;
                        case 'b':
                            out = '\b';
                            break;
                        case 'f':
                            out = '\f';
                            break;
                        case 'n':
                            out = '\n';
                            break;
                        case 'r':
                            out = '\r';
                            break;
                        case 't':
                            out = '\t';
                            break;
                        case 'u': {
                            int cp = unicodeEscape(j);
                            j += cp >= 0x10000 ? 12 : 6;
                            if (cp >= 0x80) {
                                wide = true;
                            }
                            appendCodePoint(cp);
                            run = j;
                            continue;
                        }
                        default:
                            i = j;
                            throw error("invalid escape");
                    }
                    appendByte(out);
                    j += 2;
                    run = j;
                } else if (c < 0x20) {
                    i = j;
                    throw error("control character in a string");
                } else {
                    if (c >= 0x80) {
                        wide = true;
                    }
                    j++;
                }
            }
        }

        private void appendCodePoint(int cp) {
            ensureText(4);
            if (cp < 0x80) {
                text[textLength++] = (byte) cp;
            } else if (cp < 0x800) {
                text[textLength++] = (byte) (0xc0 | (cp >> 6));
                text[textLength++] = (byte) (0x80 | (cp & 0x3f));
            } else if (cp < 0x10000) {
                text[textLength++] = (byte) (0xe0 | (cp >> 12));
                text[textLength++] = (byte) (0x80 | ((cp >> 6) & 0x3f));
                text[textLength++] = (byte) (0x80 | (cp & 0x3f));
            } else {
                text[textLength++] = (byte) (0xf0 | (cp >> 18));
                text[textLength++] = (byte) (0x80 | ((cp >> 12) & 0x3f));
                text[textLength++] = (byte) (0x80 | ((cp >> 6) & 0x3f));
                text[textLength++] = (byte) (0x80 | (cp & 0x3f));
            }
        }

        private int hex4(int at) {
            if (at + 4 > b.length) {
                return -1;
            }
            int v = 0;
            for (int k = at; k < at + 4; k++) {
                int d = Character.digit(b[k], 16);
                if (d < 0) {
                    return -1;
                }
                v = v * 16 + d;
            }
            return v;
        }

        /** A \\u escape at j (and its low surrogate, for a high one): the code point. */
        private int unicodeEscape(int j) {
            int u = hex4(j + 2);
            if (u < 0) {
                i = j;
                throw error("invalid \\u escape");
            }
            if (u >= 0xd800 && u <= 0xdbff) {
                int low = j + 7 < b.length && b[j + 6] == '\\' && b[j + 7] == 'u' ? hex4(j + 8) : -1;
                if (low >= 0xdc00 && low <= 0xdfff) {
                    return 0x10000 + ((u - 0xd800) << 10) + (low - 0xdc00);
                }
                i = j;
                throw error("lone leading surrogate in hex escape");
            }
            if (u >= 0xdc00 && u <= 0xdfff) {
                i = j;
                throw error("lone trailing surrogate in hex escape");
            }
            return u;
        }

        /** The end of the number whose text starts at {@code start} (already validated). */
        static int numberEnd(byte[] b, int j) {
            while (j < b.length) {
                int c = b[j];
                if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E') {
                    j++;
                } else {
                    break;
                }
            }
            return j;
        }

        private static boolean digit(byte[] b, int j) {
            return j < b.length && b[j] >= '0' && b[j] <= '9';
        }

        /**
         * A number: integers that fit 64 bits as integers (unsigned beyond a long), anything else as a double; the
         * header keeps the offset of the text.
         */
        private void number() {
            byte[] b = this.b;
            int start = i;
            int j = start;
            boolean negative = b[j] == '-';
            if (negative) {
                j++;
            }
            long value = 0;
            boolean overflow = false;
            int intDigits = 0;
            if (j < b.length && b[j] == '0') {
                j++;
                intDigits = 1;
            } else if (digit(b, j)) {
                while (digit(b, j)) {
                    int d = b[j] - '0';
                    // Unsigned 64-bit accumulation.
                    if (!overflow) {
                        long hi = Math.multiplyHigh(value, 10) + ((value >> 63) & 10);
                        long lo = value * 10;
                        long sum = lo + d;
                        if (hi != 0 || Long.compareUnsigned(sum, lo) < 0) {
                            overflow = true;
                        } else {
                            value = sum;
                        }
                    }
                    intDigits++;
                    j++;
                }
            } else {
                i = j;
                throw error("invalid number");
            }
            boolean floating = false;
            int fracStart = -1;
            int fracEnd = -1;
            if (j < b.length && b[j] == '.') {
                j++;
                if (!digit(b, j)) {
                    i = j;
                    throw error("invalid number");
                }
                fracStart = j;
                while (digit(b, j)) {
                    j++;
                }
                fracEnd = j;
                floating = true;
            }
            int exponent = 0;
            boolean expOverflow = false;
            if (j < b.length && (b[j] == 'e' || b[j] == 'E')) {
                j++;
                boolean expNegative = false;
                if (j < b.length && (b[j] == '+' || b[j] == '-')) {
                    expNegative = b[j] == '-';
                    j++;
                }
                if (!digit(b, j)) {
                    i = j;
                    throw error("invalid number");
                }
                while (digit(b, j)) {
                    if (exponent < 100000) {
                        exponent = exponent * 10 + (b[j] - '0');
                    } else {
                        expOverflow = true;
                    }
                    j++;
                }
                if (expNegative) {
                    exponent = -exponent;
                }
                floating = true;
            }
            i = j;
            long header = NUMBER | ((long) start << 32);
            if (!floating && !overflow) {
                if (!negative) {
                    push(header | ((value < 0 ? NUM_U64 : NUM_LONG) << 8), value);
                    return;
                }
                // -0 is the double.
                if (value != 0 && Long.compareUnsigned(value, Long.MIN_VALUE) <= 0) {
                    push(header | (NUM_LONG << 8), -value);
                    return;
                }
            }
            double d = expOverflow ? Double.NaN : fastDouble(b, negative, start, intDigits, fracStart, fracEnd, exponent);
            if (Double.isNaN(d)) {
                d = Double.parseDouble(new String(b, start, j - start, StandardCharsets.ISO_8859_1));
            }
            if (Double.isInfinite(d)) {
                i = start;
                throw error("number out of range");
            }
            push(header | (NUM_DOUBLE << 8), Double.doubleToRawLongBits(d));
        }

        private static final double[] POWERS = {
            1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18,
            1e19, 1e20, 1e21, 1e22,
        };

        /**
         * The correctly rounded double for a number whose significant digits fit 2^53 and whose decimal exponent is
         * within 10^22 (Clinger's fast path: one exact conversion and one correctly rounded operation), or NaN.
         */
        private static double fastDouble(
                byte[] b, boolean negative, int start, int intDigits, int fracStart, int fracEnd, int exponent) {
            int intStart = negative ? start + 1 : start;
            long mantissa = 0;
            int digits = 0;
            for (int k = intStart; k < intStart + intDigits; k++) {
                if (digits > 0 || b[k] != '0') {
                    digits++;
                }
                mantissa = mantissa * 10 + (b[k] - '0');
                if (digits > 15) {
                    return Double.NaN;
                }
            }
            int scale = exponent;
            if (fracStart >= 0) {
                for (int k = fracStart; k < fracEnd; k++) {
                    if (digits > 0 || b[k] != '0') {
                        digits++;
                    }
                    mantissa = mantissa * 10 + (b[k] - '0');
                    if (digits > 15) {
                        return Double.NaN;
                    }
                }
                scale -= fracEnd - fracStart;
            }
            double m = mantissa;
            double d;
            if (scale == 0) {
                d = m;
            } else if (scale > 0 && scale <= 22) {
                d = m * POWERS[scale];
            } else if (scale < 0 && scale >= -22) {
                d = m / POWERS[-scale];
            } else {
                return Double.NaN;
            }
            return negative ? -d : d;
        }
    }
}