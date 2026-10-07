package io.github.corvusdotnet.jsonschema;

import java.util.Arrays;
import java.util.HashMap;
import java.util.IdentityHashMap;
import java.util.Map;
import java.util.function.IntPredicate;

/**
 * Unicode for ECMA-262 patterns: the sets of the {@code \p{...}} escapes, the characters of a group name, and the
 * case folding of a case-insensitive group. The data is {@link EcmaUnicodeData}, of one Unicode version, so a pattern
 * means the same whichever JDK runs it ({@code java.util.regex} has the tables of its JDK, which differ from one JDK
 * to the next, and has no table at all for most of the binary properties and for Script_Extensions).
 *
 * <p>A set of code points is an {@code int[]} of inclusive ranges, sorted, that neither overlap nor touch. A set is
 * read from its table the first time it is asked for and then kept, so the set of a property is always the same
 * array. Nothing here runs while a string is matched.
 */
final class EcmaUnicode {
    private EcmaUnicode() {
    }

    /** The last code point. */
    static final int MAX = 0x10FFFF;

    /** Every code point. */
    static final int[] ANY = {0, MAX};

    private static final int[][] TABLES = new int[EcmaUnicodeData.TABLE_COUNT][];
    private static final Map<Integer, int[]> CATEGORIES = new HashMap<>();
    private static final int[] DIGIT_VALUES = new int[128];

    static {
        String alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/";
        for (int i = 0; i < alphabet.length(); i++) {
            DIGIT_VALUES[alphabet.charAt(i)] = i;
        }
    }

    // Sets.

    /** The set of the first {@code count} numbers of {@code pairs}, read as inclusive ranges in any order. */
    static int[] setOf(int[] pairs, int count) {
        int n = count / 2;
        long[] keys = new long[n];
        for (int i = 0; i < n; i++) {
            keys[i] = ((long) pairs[2 * i] << 32) | pairs[2 * i + 1];
        }
        Arrays.sort(keys);
        int[] merged = new int[count];
        int m = 0;
        for (long key : keys) {
            int lo = (int) (key >>> 32);
            int hi = (int) key;
            if (m > 0 && lo <= merged[m - 1] + 1) {
                if (hi > merged[m - 1]) {
                    merged[m - 1] = hi;
                }
                continue;
            }
            merged[m++] = lo;
            merged[m++] = hi;
        }
        return m == count ? merged : Arrays.copyOf(merged, m);
    }

    /** Every code point that is not in the set. */
    static int[] complement(int[] set) {
        int[] out = new int[set.length + 2];
        int m = 0;
        int next = 0;
        for (int i = 0; i < set.length; i += 2) {
            if (set[i] > next) {
                out[m++] = next;
                out[m++] = set[i] - 1;
            }
            next = set[i + 1] + 1;
        }
        if (next <= MAX) {
            out[m++] = next;
            out[m++] = MAX;
        }
        return Arrays.copyOf(out, m);
    }

    /** Every code point that is in either set. */
    static int[] union(int[] a, int[] b) {
        int[] pairs = Arrays.copyOf(a, a.length + b.length);
        System.arraycopy(b, 0, pairs, a.length, b.length);
        return setOf(pairs, pairs.length);
    }

    /** Whether the set holds the code point. */
    static boolean contains(int[] set, int c) {
        int lo = 0;
        int hi = set.length / 2;
        while (lo < hi) {
            int mid = (lo + hi) >>> 1;
            if (c > set[2 * mid + 1]) {
                lo = mid + 1;
            } else if (c < set[2 * mid]) {
                hi = mid;
            } else {
                return true;
            }
        }
        return false;
    }

    /** Whether the two sets share a code point. */
    static boolean intersects(int[] a, int[] b) {
        int i = 0;
        int j = 0;
        while (i < a.length && j < b.length) {
            if (a[i + 1] < b[j]) {
                i += 2;
            } else if (b[j + 1] < a[i]) {
                j += 2;
            } else {
                return true;
            }
        }
        return false;
    }

    // Tables.

    /** The set of a table of {@link EcmaUnicodeData}. */
    private static int[] table(int number) {
        synchronized (TABLES) {
            int[] set = TABLES[number];
            if (set == null) {
                String text = EcmaUnicodeData.table(number);
                int[] out = new int[text.length()];
                int m = 0;
                int[] at = {0};
                int next = 0;
                while (at[0] < text.length()) {
                    int lo = next + number(text, at);
                    int hi = lo + number(text, at);
                    out[m++] = lo;
                    out[m++] = hi;
                    next = hi + 1;
                }
                set = Arrays.copyOf(out, m);
                TABLES[number] = set;
            }
            return set;
        }
    }

    /** Reads one number of a table's text at {@code at[0]}, which it moves past the number. */
    private static int number(String text, int[] at) {
        int value = 0;
        int shift = 0;
        while (true) {
            int digit = DIGIT_VALUES[text.charAt(at[0]++)];
            value |= (digit & 31) << shift;
            if (digit < 32) {
                return value;
            }
            shift += 5;
        }
    }

    /** The index in {@code names} of the name p[start, end), or -1. Allocates nothing. */
    private static int find(String[] names, CharSequence p, int start, int end) {
        int length = end - start;
        next:
        for (int i = 0; i < names.length; i++) {
            String name = names[i];
            if (name.length() != length) {
                continue;
            }
            for (int k = 0; k < length; k++) {
                if (name.charAt(k) != p.charAt(start + k)) {
                    continue next;
                }
            }
            return i;
        }
        return -1;
    }

    private static boolean is(String name, CharSequence p, int start, int end) {
        if (end - start != name.length()) {
            return false;
        }
        for (int k = 0; k < name.length(); k++) {
            if (p.charAt(start + k) != name.charAt(k)) {
                return false;
            }
        }
        return true;
    }

    // What a property expression names: nothing, a General_Category value, a binary property, a script by its Script
    // property, or a script by its Script_Extensions property. The index of the name is in the low bits.
    private static final int NONE = -1;
    private static final int CATEGORY = 1 << 16;
    private static final int BINARY = 2 << 16;
    private static final int SCRIPT = 3 << 16;
    private static final int SCRIPT_EXTENSIONS = 4 << 16;

    /**
     * Resolves the expression p[start, end) between the braces of {@code \p{...}}, as ECMA-262 does: a name and a
     * value of General_Category, Script or Script_Extensions joined by {@code =}, or the lone name of a
     * General_Category value or of a binary property. Names are matched exactly. Allocates nothing.
     */
    private static int resolve(CharSequence p, int start, int end) {
        int eq = -1;
        for (int i = start; i < end; i++) {
            if (p.charAt(i) == '=') {
                eq = i;
                break;
            }
        }
        if (eq < 0) {
            int category = find(EcmaUnicodeData.CATEGORY_NAMES, p, start, end);
            if (category >= 0) {
                return CATEGORY | category;
            }
            int binary = find(EcmaUnicodeData.BINARY_NAMES, p, start, end);
            return binary >= 0 ? BINARY | binary : NONE;
        }
        if (is("General_Category", p, start, eq) || is("gc", p, start, eq)) {
            int category = find(EcmaUnicodeData.CATEGORY_NAMES, p, eq + 1, end);
            return category >= 0 ? CATEGORY | category : NONE;
        }
        if (is("Script", p, start, eq) || is("sc", p, start, eq)) {
            int script = find(EcmaUnicodeData.SCRIPT_NAMES, p, eq + 1, end);
            return script >= 0 ? SCRIPT | script : NONE;
        }
        if (is("Script_Extensions", p, start, eq) || is("scx", p, start, eq)) {
            int script = find(EcmaUnicodeData.SCRIPT_NAMES, p, eq + 1, end);
            return script >= 0 ? SCRIPT_EXTENSIONS | script : NONE;
        }
        return NONE;
    }

    /** Whether p[start, end) is a property expression ECMA-262 defines. Allocates nothing. */
    static boolean isProperty(CharSequence p, int start, int end) {
        return resolve(p, start, end) != NONE;
    }

    /** The set of the property expression p[start, end), or null when ECMA-262 does not define it. */
    static int[] property(CharSequence p, int start, int end) {
        int resolved = resolve(p, start, end);
        if (resolved == NONE) {
            return null;
        }
        int index = resolved & 0xFFFF;
        switch (resolved & ~0xFFFF) {
            case CATEGORY: {
                int tables = EcmaUnicodeData.CATEGORY_TABLES[index];
                int[] set = category(tables);
                engineClass(set, tables, null);
                return set;
            }
            case BINARY: {
                int[] set = table(EcmaUnicodeData.BINARY_TABLES[index]);
                String name = EcmaUnicodeData.BINARY_NAMES[index];
                if (name.equals("Alphabetic") || name.equals("Lowercase") || name.equals("Uppercase")) {
                    engineClass(set, 0, name);
                }
                return set;
            }
            case SCRIPT:
                return table(EcmaUnicodeData.SCRIPT_TABLES[index]);
            default:
                return table(EcmaUnicodeData.SCRIPT_EXTENSION_TABLES[index]);
        }
    }

    /** The union of the General_Category tables whose bits are set. */
    private static int[] category(int tables) {
        if (Integer.bitCount(tables) == 1) {
            return table(Integer.numberOfTrailingZeros(tables));
        }
        synchronized (CATEGORIES) {
            int[] set = CATEGORIES.get(tables);
            if (set == null) {
                int length = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1) {
                    length += table(Integer.numberOfTrailingZeros(bits)).length;
                }
                int[] pairs = new int[length];
                int m = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1) {
                    int[] part = table(Integer.numberOfTrailingZeros(bits));
                    System.arraycopy(part, 0, pairs, m, part.length);
                    m += part.length;
                }
                set = setOf(pairs, m);
                CATEGORIES.put(tables, set);
            }
            return set;
        }
    }

    // The classes java.util.regex has of its own.

    /**
     * A property that {@code java.util.regex} has a class for, with what must be taken from that class and added to
     * it, on the JDK that is running, to make it the set of the property in the Unicode version of the data.
     */
    static final class EngineClass {
        /** The class, as {@code java.util.regex} writes it. */
        final String name;
        /** The code points of the class that are not in the set. */
        final int[] removed;
        /** The code points of the set that are not in the class. */
        final int[] added;

        EngineClass(String name, int[] removed, int[] added) {
            this.name = name;
            this.removed = removed;
            this.added = added;
        }
    }

    // The General_Category values that are not unions, in the order of their tables, as java.util.regex names them
    // and as java.lang.Character numbers them.
    private static final String[] LEAF_NAMES = {
        "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po",
        "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Cn",
    };
    private static final byte[] LEAF_TYPES = {
        Character.UPPERCASE_LETTER, Character.LOWERCASE_LETTER, Character.TITLECASE_LETTER, Character.MODIFIER_LETTER,
        Character.OTHER_LETTER, Character.NON_SPACING_MARK, Character.COMBINING_SPACING_MARK, Character.ENCLOSING_MARK,
        Character.DECIMAL_DIGIT_NUMBER, Character.LETTER_NUMBER, Character.OTHER_NUMBER,
        Character.CONNECTOR_PUNCTUATION, Character.DASH_PUNCTUATION, Character.START_PUNCTUATION,
        Character.END_PUNCTUATION, Character.INITIAL_QUOTE_PUNCTUATION, Character.FINAL_QUOTE_PUNCTUATION,
        Character.OTHER_PUNCTUATION, Character.MATH_SYMBOL, Character.CURRENCY_SYMBOL, Character.MODIFIER_SYMBOL,
        Character.OTHER_SYMBOL, Character.SPACE_SEPARATOR, Character.LINE_SEPARATOR, Character.PARAGRAPH_SEPARATOR,
        Character.CONTROL, Character.FORMAT, Character.SURROGATE, Character.PRIVATE_USE, Character.UNASSIGNED,
    };
    /** The unions of General_Category values, as the bits of their tables, with the names of java.util.regex. */
    private static final String[] UNION_NAMES = {"LC", "L", "M", "N", "P", "S", "Z", "C"};
    private static final int[] UNION_TABLES = {
        0x7, 0x1F, 0x7 << 5, 0x7 << 8, 0x7F << 11, 0xF << 18, 0x7 << 22, 0x1F << 25,
    };

    /** For the set of a property with an engine class: the class once worked out, or what it is to be made from. */
    private static final Map<int[], Object> ENGINE_CLASSES = new IdentityHashMap<>();
    /** What java.lang.Character says the General_Category of each code point is, as one set to a value. */
    private static int[][] characterCategories;

    /** Notes that the set of a property (a General_Category value by its tables, or a binary property) has a class. */
    private static void engineClass(int[] set, int tables, String binary) {
        synchronized (ENGINE_CLASSES) {
            if (!ENGINE_CLASSES.containsKey(set)) {
                ENGINE_CLASSES.put(set, binary != null ? (Object) binary : (Object) Integer.valueOf(tables));
            }
        }
    }

    /**
     * The class {@code java.util.regex} has for the property whose set this is (the very array {@link #property}
     * returned), or null when it has none that is used. It has one for each General_Category value and for
     * Alphabetic, Lowercase and Uppercase, and matches a character against one of those in a step or two where a
     * class written out from the set takes many.
     *
     * <p>The class of the engine holds what the JDK's Unicode version says, so it is corrected: the first use of a
     * property on a JDK asks {@code java.lang.Character}, which is where the engine's class gets its answer, about
     * every code point, and keeps the differences from the set.
     */
    static EngineClass engineClass(int[] set) {
        synchronized (ENGINE_CLASSES) {
            Object known = ENGINE_CLASSES.get(set);
            if (known == null || known instanceof EngineClass) {
                return (EngineClass) known;
            }
            String name;
            int[] engine;
            if (known instanceof String) {
                String binary = (String) known;
                name = "\\p{Is" + binary + "}";
                engine = scan(binary.equals("Alphabetic") ? Character::isAlphabetic
                        : binary.equals("Lowercase") ? Character::isLowerCase : Character::isUpperCase);
            } else {
                int tables = (Integer) known;
                if (characterCategories == null) {
                    characterCategories = scanCategories();
                }
                name = null;
                int length = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1) {
                    length += characterCategories[Integer.numberOfTrailingZeros(bits)].length;
                }
                int[] pairs = new int[length];
                int m = 0;
                for (int bits = tables; bits != 0; bits &= bits - 1) {
                    int leaf = Integer.numberOfTrailingZeros(bits);
                    System.arraycopy(characterCategories[leaf], 0, pairs, m, characterCategories[leaf].length);
                    m += characterCategories[leaf].length;
                    if (Integer.bitCount(tables) == 1) {
                        name = "\\p{" + LEAF_NAMES[leaf] + "}";
                    }
                }
                for (int k = 0; k < UNION_TABLES.length; k++) {
                    if (UNION_TABLES[k] == tables) {
                        name = "\\p{" + UNION_NAMES[k] + "}";
                    }
                }
                if (name == null) {
                    ENGINE_CLASSES.remove(set);
                    return null;
                }
                engine = setOf(pairs, m);
            }
            EngineClass result = new EngineClass(name, difference(engine, set), difference(set, engine));
            ENGINE_CLASSES.put(set, result);
            return result;
        }
    }

    /** The code points of {@code a} that are not in {@code b}. */
    static int[] difference(int[] a, int[] b) {
        return complement(union(complement(a), b));
    }

    /** The set of the code points a predicate of java.lang.Character holds for. */
    private static int[] scan(IntPredicate predicate) {
        int[] out = new int[64];
        int m = 0;
        int start = -1;
        for (int c = 0; c <= MAX + 1; c++) {
            boolean in = c <= MAX && predicate.test(c);
            if (in && start < 0) {
                start = c;
            } else if (!in && start >= 0) {
                if (m + 2 > out.length) {
                    out = Arrays.copyOf(out, out.length * 2);
                }
                out[m++] = start;
                out[m++] = c - 1;
                start = -1;
            }
        }
        return Arrays.copyOf(out, m);
    }

    /** For each General_Category value that is not a union, the code points java.lang.Character gives it. */
    private static int[][] scanCategories() {
        int[] leafOfType = new int[32];
        Arrays.fill(leafOfType, -1);
        for (int leaf = 0; leaf < LEAF_TYPES.length; leaf++) {
            leafOfType[LEAF_TYPES[leaf]] = leaf;
        }
        int[][] out = new int[LEAF_TYPES.length][16];
        int[] lengths = new int[LEAF_TYPES.length];
        int start = 0;
        int current = leafOfType[Character.getType(0)];
        for (int c = 1; c <= MAX + 1; c++) {
            int leaf = c <= MAX ? leafOfType[Character.getType(c)] : -2;
            if (leaf != current) {
                if (current >= 0) {
                    if (lengths[current] + 2 > out[current].length) {
                        out[current] = Arrays.copyOf(out[current], out[current].length * 2);
                    }
                    out[current][lengths[current]++] = start;
                    out[current][lengths[current]++] = c - 1;
                }
                start = c;
                current = leaf;
            }
        }
        for (int leaf = 0; leaf < out.length; leaf++) {
            out[leaf] = Arrays.copyOf(out[leaf], lengths[leaf]);
        }
        return out;
    }

    // Group names.

    private static volatile int[] idStart;
    private static volatile int[] idContinue;

    /** Whether a code point may start a group name: ID_Start, {@code $} or {@code _}. */
    static boolean isNameStart(int c) {
        if (c < 128) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '$' || c == '_';
        }
        int[] set = idStart;
        if (set == null) {
            set = property("ID_Start", 0, 8);
            idStart = set;
        }
        return contains(set, c);
    }

    /** Whether a code point may continue a group name: ID_Continue, {@code $}, U+200C or U+200D. */
    static boolean isNamePart(int c) {
        if (c < 128) {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '$' || c == '_';
        }
        if (c == 0x200C || c == 0x200D) {
            return true;
        }
        int[] set = idContinue;
        if (set == null) {
            set = property("ID_Continue", 0, 11);
            idContinue = set;
        }
        return contains(set, c);
    }

    // Case folding. ECMA-262 defines case-insensitive matching through a Canonicalize function: two characters match
    // when they canonicalize to the same character. With the u flag grammar it is Unicode simple case folding. With
    // no flag it is the uppercase mapping of one UTF-16 code unit, when that is again one code unit, and it never
    // maps a character outside ASCII into ASCII.

    /**
     * The characters of the Basic Multilingual Plane whose full uppercase form is more than one character, which
     * the Canonicalize of a pattern with no flag leaves alone (they are the single characters of SpecialCasing.txt).
     */
    private static final int[] MULTIPLE_UPPERCASE = {
        0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50,
        0x1F50, 0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7, 0x1FBC,
        0x1FBC, 0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2, 0x1FE4, 0x1FE6,
        0x1FE7, 0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
    };

    /** The characters that are equivalent to some other character, each with every member of its class. */
    private static final class Folding {
        /** The characters, sorted. */
        int[] points;
        /** For each of {@link #points}, the members of its class, itself among them. */
        int[][] classes;
        /** {@link #points} as a set. */
        int[] set;
    }

    private static Folding unicodeFolding;
    private static Folding legacyFolding;

    /** A case mapping of {@link EcmaUnicodeData} as runs of start, length, delta and stride. */
    private static int[] mapping(String text) {
        int[] out = new int[text.length()];
        int m = 0;
        int[] at = {0};
        int next = 0;
        while (at[0] < text.length()) {
            int start = next + number(text, at);
            int length = number(text, at) + 1;
            int delta = number(text, at);
            out[m++] = start;
            out[m++] = length;
            out[m++] = (delta & 1) != 0 ? -(delta >> 1) : delta >> 1;
            out[m++] = number(text, at) + 1;
            next = start + length;
        }
        return Arrays.copyOf(out, m);
    }

    /** What a mapping makes of a code point. */
    private static int map(int[] runs, int c) {
        int lo = 0;
        int hi = runs.length / 4;
        while (lo < hi) {
            int mid = (lo + hi) >>> 1;
            int start = runs[4 * mid];
            if (c < start) {
                hi = mid;
            } else if (c >= start + runs[4 * mid + 1]) {
                lo = mid + 1;
            } else {
                return (c - start) % runs[4 * mid + 3] == 0 ? c + runs[4 * mid + 2] : c;
            }
        }
        return c;
    }

    private static synchronized Folding folding(boolean unicode) {
        Folding f = unicode ? unicodeFolding : legacyFolding;
        if (f != null) {
            return f;
        }
        Map<Integer, int[]> byKey = new HashMap<>();
        if (unicode) {
            int[] runs = mapping(EcmaUnicodeData.SIMPLE_FOLDING);
            for (int r = 0; r < runs.length; r += 4) {
                for (int c = runs[r]; c < runs[r] + runs[r + 1]; c += runs[r + 3]) {
                    int key = c + runs[r + 2];
                    add(byKey, key, key);
                    add(byKey, key, c);
                }
            }
        } else {
            int[] runs = mapping(EcmaUnicodeData.UPPERCASE);
            for (int c = 0; c <= 0xFFFF; c++) {
                int upper = contains(MULTIPLE_UPPERCASE, c) ? c : map(runs, c);
                if ((c >= 128 && upper < 128) || upper > 0xFFFF) {
                    upper = c;
                }
                add(byKey, upper, c);
            }
        }
        int count = 0;
        for (int[] members : byKey.values()) {
            if (members.length > 1) {
                count += members.length;
            }
        }
        f = new Folding();
        f.points = new int[count];
        int m = 0;
        for (int[] members : byKey.values()) {
            if (members.length > 1) {
                System.arraycopy(members, 0, f.points, m, members.length);
                m += members.length;
            }
        }
        Arrays.sort(f.points);
        f.classes = new int[count][];
        int[] pairs = new int[2 * count];
        for (int[] members : byKey.values()) {
            if (members.length > 1) {
                for (int member : members) {
                    f.classes[Arrays.binarySearch(f.points, member)] = members;
                }
            }
        }
        for (int i = 0; i < count; i++) {
            pairs[2 * i] = f.points[i];
            pairs[2 * i + 1] = f.points[i];
        }
        f.set = setOf(pairs, pairs.length);
        if (unicode) {
            unicodeFolding = f;
        } else {
            legacyFolding = f;
        }
        return f;
    }

    private static void add(Map<Integer, int[]> byKey, int key, int member) {
        int[] members = byKey.get(key);
        if (members == null) {
            byKey.put(key, new int[] {member});
            return;
        }
        for (int existing : members) {
            if (existing == member) {
                return;
            }
        }
        members = Arrays.copyOf(members, members.length + 1);
        members[members.length - 1] = member;
        byKey.put(key, members);
    }

    /**
     * The set of every character equivalent to some member of the set, under the Canonicalize of the u flag grammar
     * ({@code unicode}) or of a pattern with no flag.
     */
    static int[] foldClosure(int[] set, boolean unicode) {
        Folding f = folding(unicode);
        int[] extra = new int[16];
        int m = 0;
        for (int i = 0; i < set.length; i += 2) {
            int k = Arrays.binarySearch(f.points, set[i]);
            if (k < 0) {
                k = -k - 1;
            }
            for (; k < f.points.length && f.points[k] <= set[i + 1]; k++) {
                for (int member : f.classes[k]) {
                    if (!contains(set, member)) {
                        if (m + 2 > extra.length) {
                            extra = Arrays.copyOf(extra, extra.length * 2);
                        }
                        extra[m++] = member;
                        extra[m++] = member;
                    }
                }
            }
        }
        if (m == 0) {
            return set;
        }
        return union(set, Arrays.copyOf(extra, m));
    }

    /** The characters that are equivalent to some other character under the Canonicalize of a grammar. */
    static int[] cased(boolean unicode) {
        return folding(unicode).set;
    }

    // Writing a set for java.util.regex.

    /** The sets up to this many ranges are written as one flat class. */
    private static final int FLAT_RANGES = 12;
    /** The ranges at a leaf of a larger set. */
    private static final int LEAF_RANGES = 4;

    /**
     * Appends a {@code java.util.regex} character class that matches exactly the set, which must not be empty.
     *
     * <p>A class of many ranges is slow in {@code java.util.regex}, which tries the ranges one after another for
     * every character. So a large set is written as a search tree. Its members below U+0100 are written one by one,
     * which {@code java.util.regex} keeps as a bit map, and the rest are split in halves, each half guarded by the
     * range that spans it ({@code [\x{100}-\x{3ff}&&[...]]}), so that a character is tried against a number of
     * ranges that grows with the logarithm of the size of the set.
     */
    static void appendClass(StringBuilder sb, int[] set) {
        sb.append('[');
        if (set.length <= 2 * FLAT_RANGES) {
            appendRanges(sb, set, 0, set.length);
        } else {
            appendSearchTree(sb, set);
        }
        sb.append(']');
    }

    /**
     * Appends the set as a search tree (see {@link #appendClass}), without brackets around it, to stand inside a
     * class. A character outside the span of the members above U+00FF is turned away by one test.
     */
    static void appendSearchTree(StringBuilder sb, int[] set) {
        int from = 0;
        while (from < set.length && set[from] < 0x100) {
            for (int c = set[from]; c <= set[from + 1] && c < 0x100; c++) {
                appendCodePoint(sb, c);
            }
            if (set[from + 1] >= 0x100) {
                break;
            }
            from += 2;
        }
        if (from < set.length) {
            // The first range of the rest may have begun below U+0100.
            int[] rest = Arrays.copyOfRange(set, from, set.length);
            rest[0] = Math.max(rest[0], 0x100);
            appendHalf(sb, rest, 0, rest.length);
        }
    }

    private static void appendTree(StringBuilder sb, int[] set, int from, int to) {
        int ranges = (to - from) / 2;
        if (ranges <= LEAF_RANGES) {
            appendRanges(sb, set, from, to);
            return;
        }
        int mid = from + (ranges / 2) * 2;
        appendHalf(sb, set, from, mid);
        appendHalf(sb, set, mid, to);
    }

    private static void appendHalf(StringBuilder sb, int[] set, int from, int to) {
        sb.append('[');
        appendCodePoint(sb, set[from]);
        sb.append('-');
        appendCodePoint(sb, set[to - 1]);
        sb.append("&&[");
        appendTree(sb, set, from, to);
        sb.append("]]");
    }

    private static void appendRanges(StringBuilder sb, int[] set, int from, int to) {
        for (int i = from; i < to; i += 2) {
            appendCodePoint(sb, set[i]);
            if (set[i + 1] != set[i]) {
                sb.append('-');
                appendCodePoint(sb, set[i + 1]);
            }
        }
    }

    /** Appends a code point as an escape, so that {@code java.util.regex} reads none as syntax. */
    static void appendCodePoint(StringBuilder sb, int c) {
        sb.append("\\x{").append(Integer.toHexString(c)).append('}');
    }
}
