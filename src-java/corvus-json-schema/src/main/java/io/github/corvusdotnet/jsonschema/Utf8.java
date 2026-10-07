package io.github.corvusdotnet.jsonschema;

/** UTF-8 validation and code point counting over byte ranges. */
final class Utf8 {
    private Utf8() {
    }

    /** The offset of the first byte of an invalid sequence in [start, end), or -1 if the range is valid UTF-8. */
    static int invalidAt(byte[] b, int start, int end) {
        int j = start;
        while (j < end) {
            int c = b[j] & 0xff;
            if (c < 0x80) {
                j++;
                continue;
            }
            int need;
            int min;
            if (c >= 0xc2 && c <= 0xdf) {
                need = 1;
                min = 0x80;
            } else if (c >= 0xe0 && c <= 0xef) {
                need = 2;
                min = 0x800;
            } else if (c >= 0xf0 && c <= 0xf4) {
                need = 3;
                min = 0x10000;
            } else {
                return j;
            }
            if (j + need >= end) {
                // Fewer continuation bytes remain than the lead byte needs.
                return j;
            }
            int cp = c & (0x3f >> need);
            for (int k = 1; k <= need; k++) {
                int d = b[j + k] & 0xff;
                if ((d & 0xc0) != 0x80) {
                    return j;
                }
                cp = (cp << 6) | (d & 0x3f);
            }
            if (cp < min || cp > 0x10ffff || (cp >= 0xd800 && cp <= 0xdfff)) {
                return j;
            }
            j += need + 1;
        }
        return -1;
    }

    /** The number of code points in valid UTF-8 [start, end). */
    static int codePoints(byte[] b, int start, int end) {
        int n = 0;
        for (int j = start; j < end; j++) {
            if ((b[j] & 0xc0) != 0x80) {
                n++;
            }
        }
        return n;
    }
}
