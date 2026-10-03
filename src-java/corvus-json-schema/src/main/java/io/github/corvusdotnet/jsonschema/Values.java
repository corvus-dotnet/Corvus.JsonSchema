package io.github.corvusdotnet.jsonschema;

import static io.github.corvusdotnet.jsonschema.JsonDocument.ARRAY;
import static io.github.corvusdotnet.jsonschema.JsonDocument.BOOLEAN;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NULL;
import static io.github.corvusdotnet.jsonschema.JsonDocument.NUMBER;
import static io.github.corvusdotnet.jsonschema.JsonDocument.OBJECT;
import static io.github.corvusdotnet.jsonschema.JsonDocument.STRING;

import java.util.Arrays;

/** JSON equality, hashing and uniqueness over values in documents (an instance against a schema's constant). */
final class Values {
    private Values() {
    }

    /** JSON equality: numbers by value, objects by their property sets, arrays element-wise. */
    static boolean equal(JsonDocument a, int x, JsonDocument b, int y) {
        int kind = a.kind(x);
        if (kind != b.kind(y)) {
            return false;
        }
        switch (kind) {
            case NULL:
                return true;
            case BOOLEAN:
                return a.bool(x) == b.bool(y);
            case NUMBER:
                return Numbers.compare(a.flags(x), a.data(x), b.flags(y), b.data(y)) == 0;
            case STRING:
                return a.stringEquals(x, b, y);
            case ARRAY: {
                int n = a.count(x);
                if (n != b.count(y)) {
                    return false;
                }
                int p = a.first(x);
                int q = b.first(y);
                for (int i = 0; i < n; i++) {
                    if (!equal(a, p + i, b, q + i)) {
                        return false;
                    }
                }
                return true;
            }
            default: {
                int n = a.count(x);
                if (n != b.count(y)) {
                    return false;
                }
                // Objects usually list their members in the same order: compare position by position, and look names
                // up only from the first position where the names differ.
                int p = a.first(x);
                int q = b.first(y);
                for (int i = 0; i < n; i++) {
                    int k = p + 2 * i;
                    int l = q + 2 * i;
                    if (!a.stringEquals(k, b, l)) {
                        for (int j = i; j < n; j++) {
                            int key = p + 2 * j;
                            int w = findProperty(b, y, a, key);
                            if (w < 0 || !equal(a, key + 1, b, w)) {
                                return false;
                            }
                        }
                        return true;
                    }
                    if (!equal(a, k + 1, b, l + 1)) {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /** The value of the property of {@code object} (in b) named by the string {@code key} (in a), or -1. */
    private static int findProperty(JsonDocument b, int object, JsonDocument a, int key) {
        int k = b.first(object);
        for (int i = b.count(object); i > 0; i--, k += 2) {
            if (b.stringEquals(k, a, key)) {
                return k + 1;
            }
        }
        return -1;
    }

    private static final long K = 0x9e3779b97f4a7c15L;

    /** A hash that agrees with JSON equality (object hashing is order-independent). */
    static long hash(JsonDocument d, int v) {
        switch (d.kind(v)) {
            case NULL:
                return 0x53;
            case BOOLEAN:
                return 0x51 + (d.bool(v) ? 1 : 0);
            case NUMBER: {
                int flag = d.flags(v);
                long bits = d.data(v);
                if (flag == JsonDocument.NUM_DOUBLE) {
                    double f = Double.longBitsToDouble(bits);
                    // Integral doubles hash as the integer they equal.
                    if (f == Math.floor(f) && Math.abs(f) < 0x1p63) {
                        return ((long) f) * K ^ 0x1234;
                    }
                    if (f == 0) {
                        return 0x1234;
                    }
                    return Double.doubleToLongBits(f) * K ^ 0x4321;
                }
                if (flag == JsonDocument.NUM_U64) {
                    // Doubles at or above 2^63 are integers, and never equal an unsigned long exactly unless the
                    // unsigned long is a double: hash both by the double.
                    return Double.doubleToLongBits(Numbers.u64ToDouble(bits)) * K ^ 0x4321;
                }
                return bits * K ^ 0x1234;
            }
            case STRING:
                return strHash(d.strBytes(v), d.strOffset(v), d.count(v));
            case ARRAY: {
                long h = 0x54 + d.count(v);
                int c = d.first(v);
                for (int i = 0; i < d.count(v); i++) {
                    h = h * 31 + hash(d, c + i);
                }
                return h;
            }
            default: {
                // Equal objects have the same member values, so a sum of the values' hashes (whatever the order)
                // agrees with equality.
                long h = 0x55 + d.count(v);
                int c = d.first(v);
                for (int i = 0; i < d.count(v); i++) {
                    h += hash(d, c + 2 * i + 1) * 0x2c1b3c6dL;
                }
                return h;
            }
        }
    }

    static long strHash(byte[] b, int off, int len) {
        long h = len * K;
        for (int i = 0; i < len; i++) {
            h = (Long.rotateLeft(h, 5) ^ b[off + i]) * K;
        }
        return h;
    }

    /**
     * {@code uniqueItems}: pairwise for short arrays; otherwise sorted by hash in {@code scratch} (at least as long as
     * the array), each entry the hash's high half and the item's index, so that only items with equal hashes are
     * compared. Allocates nothing.
     */
    static boolean allUnique(JsonDocument d, int array, long[] scratch) {
        int n = d.count(array);
        if (n < 2) {
            return true;
        }
        int c = d.first(array);
        if (n <= 16) {
            for (int i = 1; i < n; i++) {
                for (int j = 0; j < i; j++) {
                    if (equal(d, c + i, d, c + j)) {
                        return false;
                    }
                }
            }
            return true;
        }
        for (int i = 0; i < n; i++) {
            scratch[i] = (hash(d, c + i) & 0xffffffff00000000L) | i;
        }
        Arrays.sort(scratch, 0, n);
        int start = 0;
        for (int end = 1; end <= n; end++) {
            if (end == n || (scratch[end] >>> 32) != (scratch[start] >>> 32)) {
                for (int i = start + 1; i < end; i++) {
                    for (int j = start; j < i; j++) {
                        if (equal(d, c + (int) scratch[i], d, c + (int) scratch[j])) {
                            return false;
                        }
                    }
                }
                start = end;
            }
        }
        return true;
    }

    static boolean isObject(JsonDocument d, int v) {
        return d.kind(v) == OBJECT;
    }
}