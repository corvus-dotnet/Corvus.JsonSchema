package io.github.corvusdotnet.jsonschema;

import java.nio.charset.StandardCharsets;

/**
 * The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the
 * {@code idn-hostname} and {@code idn-email} formats apply to a name before they check it.
 *
 * <p>An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is
 * kept: "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay
 * and meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse.
 * ASCII is kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid.
 * The normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name,
 * as the JSON-Schema-Test-Suite expects. The table is the one the .NET engine and the Rust crate use.
 */
final class Uts46 {
    private static final int ENTRY_SIZE = 8;

    private Uts46() {
    }

    /**
     * The mapped form of s[from, to), or {@code null} when nothing in it is mapped or ignored (so the common case
     * allocates nothing).
     */
    static String map(CharSequence s, int from, int to) {
        int first = -1;
        for (int i = from; i < to; ) {
            int c = Character.codePointAt(s, i);
            if (c >= 0x80 && find(c) >= 0) {
                first = i;
                break;
            }
            i += Character.charCount(c);
        }
        if (first < 0) {
            return null;
        }
        StringBuilder mapped = new StringBuilder(to - from + 16);
        mapped.append(s, from, first);
        for (int i = first; i < to; ) {
            int c = Character.codePointAt(s, i);
            i += Character.charCount(c);
            int entry = c >= 0x80 ? find(c) : -1;
            if (entry < 0) {
                mapped.appendCodePoint(c);
                continue;
            }
            byte[] e = Uts46Data.ENTRIES;
            int offset = ((e[entry + 5] & 0xff) << 8) | (e[entry + 6] & 0xff);
            int length = e[entry + 7] & 0xff;
            if (length > 0) {
                mapped.append(new String(Uts46Data.POOL, offset, length, StandardCharsets.UTF_8));
            }
        }
        return mapped.toString();
    }

    /** The index in the table of the entry for a code point outside ASCII, or -1 if it is neither ignored nor mapped. */
    private static int find(int codePoint) {
        byte[] e = Uts46Data.ENTRIES;
        int low = 0;
        int high = e.length / ENTRY_SIZE;
        while (low < high) {
            int middle = (low + high) >>> 1;
            int at = middle * ENTRY_SIZE;
            int start = ((e[at] & 0xff) << 16) | ((e[at + 1] & 0xff) << 8) | (e[at + 2] & 0xff);
            int end = start + (((e[at + 3] & 0xff) << 8) | (e[at + 4] & 0xff));
            if (codePoint < start) {
                high = middle;
            } else if (codePoint > end) {
                low = middle + 1;
            } else {
                return at;
            }
        }
        return -1;
    }
}
