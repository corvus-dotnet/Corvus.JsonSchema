package io.github.corvusdotnet.jsonschema;

import java.util.Arrays;

/** A map from UTF-8 names to ints, looked up by a byte range without creating a key. */
final class NameMap {
    private final byte[][] keys;
    private final int[] values;
    private final int mask;

    NameMap(int expected) {
        int capacity = Integer.highestOneBit(Math.max(4, expected * 2) - 1) << 1;
        keys = new byte[capacity][];
        values = new int[capacity];
        mask = capacity - 1;
    }

    private static int hash(byte[] b, int off, int len) {
        int h = len;
        for (int i = 0; i < len; i++) {
            h = h * 31 + b[off + i];
        }
        return h ^ (h >>> 16);
    }

    void putIfAbsent(byte[] key, int value) {
        int i = hash(key, 0, key.length) & mask;
        while (keys[i] != null) {
            if (Arrays.equals(keys[i], key)) {
                return;
            }
            i = (i + 1) & mask;
        }
        keys[i] = key;
        values[i] = value;
    }

    /** The value for the name in b[off, off + len), or -1. */
    int get(byte[] b, int off, int len) {
        int i = hash(b, off, len) & mask;
        while (true) {
            byte[] k = keys[i];
            if (k == null) {
                return -1;
            }
            if (k.length == len && Arrays.equals(k, 0, len, b, off, off + len)) {
                return values[i];
            }
            i = (i + 1) & mask;
        }
    }
}
