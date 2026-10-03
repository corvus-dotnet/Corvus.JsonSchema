package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.NUMBER;

import java.util.Arrays;
import java.util.function.Predicate;

/**
 * The runtime of compiled schemas: small static helpers that the generated code calls with the schema's constants.
 * They are small enough for the JIT to inline at each call site, where the constant arguments fold away the branches
 * that do not apply.
 */
final class Rt {
    private Rt() {
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Values

    static int kind(JsonDocument d, int x) {
        return (int) d.tape[x << 1] & 0xff;
    }

    static int count(JsonDocument d, int x) {
        return (int) (d.tape[x << 1] >>> 32);
    }

    static int first(JsonDocument d, int x) {
        return (int) d.tape[(x << 1) + 1];
    }

    /** Whether a value is of one of the types in a mask (the type bits and integer). */
    static boolean type(JsonDocument d, int x, int mask) {
        int kind = (int) d.tape[x << 1] & 0xff;
        if ((kind & mask) != 0) {
            return true;
        }
        return kind == NUMBER && (mask & SchemaNode.T_INTEGER) != 0 && integer(d, x);
    }

    static boolean integer(JsonDocument d, int x) {
        return Numbers.isInteger(d.flags(x), d.data(x));
    }

    static boolean equal(JsonDocument d, int x, SchemaNode.Value v) {
        return Values.equal(d, x, v.doc, v.node);
    }

    static boolean anyEqual(JsonDocument d, int x, SchemaNode.Value[] values) {
        for (SchemaNode.Value v : values) {
            if (Values.equal(d, x, v.doc, v.node)) {
                return true;
            }
        }
        return false;
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Numbers

    /** Compares a number value with a constant: negative, zero or positive. */
    static int compare(JsonDocument d, int x, int flag, long bits) {
        return Numbers.compare(d.flags(x), d.data(x), flag, bits);
    }

    static boolean multipleOf(JsonDocument d, int x, Numbers.Divisor divisor) {
        return divisor.divides(d, x);
    }

    static boolean numericFormat(JsonDocument d, int x, Formats.Kind kind) {
        return kind.checkNumber(d.flags(x), d.data(x));
    }

    static boolean customNumberFormat(JsonDocument d, int x, Predicate<String> format) {
        return format.test(d.numberText(x));
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Strings

    /** The length of a string value in code points. */
    static long length(JsonDocument d, int x) {
        long h = d.tape[x << 1];
        int len = (int) (h >>> 32);
        if ((h & (JsonDocument.STR_WIDE << 8)) == 0) {
            return len;
        }
        int off = (int) d.tape[(x << 1) + 1];
        return Utf8.codePoints((h & (JsonDocument.STR_TEXT << 8)) != 0 ? d.text : d.source, off, off + len);
    }

    /**
     * Whether the length of the string value x in code points is within [min, max] (max negative for none). A string
     * of n UTF-8 bytes has between ceil(n / 4) and n code points, which decides most bounds without counting.
     */
    static boolean lengthWithin(JsonDocument d, int x, long min, long max) {
        long h = d.tape[x << 1];
        long bytes = (int) (h >>> 32);
        if (bytes < min || (max >= 0 && (bytes + 3) >>> 2 > max)) {
            return false;
        }
        if ((bytes + 3) >>> 2 >= min && (max < 0 || bytes <= max)) {
            return true;
        }
        long n = length(d, x);
        return n >= min && (max < 0 || n <= max);
    }

    static boolean pattern(Evaluator e, int x, SchemaPattern p) {
        return e.matches(p, x);
    }

    static boolean format(Evaluator e, int x, Formats.Kind kind, boolean legacyHostname) {
        return e.format(x, kind, legacyHostname);
    }

    static boolean customFormat(JsonDocument d, int x, Predicate<String> format) {
        return format.test(d.string(x));
    }

    static boolean content(Evaluator e, int x, int kind) {
        return e.contentOk(x, kind);
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Objects and arrays

    /** The value of an object's property, or -1. */
    static int property(JsonDocument d, int x, byte[] name) {
        long[] t = d.tape;
        int k = (int) t[(x << 1) + 1];
        int len = name.length;
        for (int i = (int) (t[x << 1] >>> 32); i > 0; i--, k += 2) {
            long h = t[k << 1];
            if ((int) (h >>> 32) == len) {
                int off = (int) t[(k << 1) + 1];
                byte[] b = (h & (JsonDocument.STR_TEXT << 8)) != 0 ? d.text : d.source;
                if (Arrays.equals(b, off, off + len, name, 0, len)) {
                    return k + 1;
                }
            }
        }
        return -1;
    }

    private static final java.lang.invoke.VarHandle LONGS =
            java.lang.invoke.MethodHandles.byteArrayViewVarHandle(long[].class, java.nio.ByteOrder.LITTLE_ENDIAN);

    /**
     * The {@code width} (1 to 8) bytes at {@code pos} of the string value k as a little-endian word: one load when
     * the array has eight bytes there, masked to the width.
     */
    static long word(JsonDocument d, int k, int pos, int width) {
        long h = d.tape[k << 1];
        byte[] b = (h & (JsonDocument.STR_TEXT << 8)) != 0 ? d.text : d.source;
        int off = (int) d.tape[(k << 1) + 1] + pos;
        if (off + 8 <= b.length) {
            long w = (long) LONGS.get(b, off);
            return width == 8 ? w : w & ((1L << (width << 3)) - 1);
        }
        return word(b, off, width);
    }

    /** The same over an array (the compiler's constants). */
    static long word(byte[] b, int off, int width) {
        long w = 0;
        for (int i = width - 1; i >= 0; i--) {
            w = (w << 8) | (b[off + i] & 0xff);
        }
        return w;
    }

    static boolean unique(Evaluator e, int x) {
        return e.unique(x);
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Fallbacks

    static boolean enter(Evaluator e) {
        return e.enterInPlace();
    }

    static void leave(Evaluator e) {
        e.leaveInPlace();
    }

    static void push(Evaluator e, int resource) {
        e.pushScope(resource);
    }

    static void pop(Evaluator e) {
        e.popScope();
    }

    static int dynamicTarget(Evaluator e, SchemaNode.DynamicRef d) {
        return e.resolveDynamic(d);
    }

    /** Evaluates a node with the interpreter, fail-fast (for what the compiled code does not specialise). */
    static boolean interpret(Evaluator e, int node, int x) {
        return e.evalNode(node, x, -1);
    }

    /** The branches a discriminator leaves as candidates for an object, or null for all of them. */
    static int[] select(JsonDocument d, int x, SchemaNode.Discriminator disc) {
        int value = property(d, x, disc.utf8);
        if (value < 0) {
            return disc.allRequire ? NONE : null;
        }
        return disc.select(d, value);
    }

    static final int[] NONE = new int[0];
}