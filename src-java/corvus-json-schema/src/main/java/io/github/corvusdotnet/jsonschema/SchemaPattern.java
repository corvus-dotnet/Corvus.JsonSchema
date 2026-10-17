package io.github.corvusdotnet.jsonschema;

import java.util.Iterator;
import java.util.LinkedHashMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * A compiled {@code pattern} with ECMA-262 semantics: the cheapest matcher that decides it exactly, falling back to
 * the pattern translated for {@code java.util.regex}. A pattern is immutable, so identical patterns share what was
 * compiled for them, through a cache of the process that is bounded in number and in size (see {@link #compile}).
 *
 * <p>An evaluator keeps a {@link Matcher} for each pattern of its schema that the engine runs. Its place in the
 * evaluator's array is the pattern's {@link #slot}, which the compiler of the schema gives it ({@link #inSlot}), so
 * the array is as long as the schema has such patterns, however many patterns the process has compiled.
 *
 * <p>A pattern that is valid with the u flag matches characters. One that is valid only without it matches UTF-16
 * code units, as ECMA-262 says: {@code ^\-?.$} does not match one character beyond the Basic Multilingual Plane, which
 * is two code units.
 */
final class SchemaPattern {
    private static final int EVERYTHING = 0;
    private static final int REGEX = 1;
    private static final int SHAPE = 2;

    /** The most patterns the cache of the process holds. */
    static final int MAX_CACHED_PATTERNS = 1024;
    /** The most the patterns of the cache come to, in the bytes they are counted as ({@link #counted}). */
    static final long MAX_CACHED_BYTES = 64L << 20;
    /** A pattern counted as more than this is not kept in the cache. */
    static final long MAX_CACHED_PATTERN_BYTES = 2L << 20;

    /**
     * The compiled patterns of the process by source, in the order they were compiled. It is read and written only
     * when a schema is compiled, under its own lock. Nothing reads it when an instance is evaluated.
     */
    private static final LinkedHashMap<String, SchemaPattern> CACHE = new LinkedHashMap<>();
    private static long cachedBytes;

    final String source;
    private final int kind;
    final Pattern regex;
    /** A matcher that decides the pattern without the regular expression engine, or null. */
    final PatternShapes.Matcher shape;
    /**
     * Whether the pattern is valid only without the u flag. ECMA-262 then matches it against the UTF-16 code units
     * of the text, and the translation is given the text as {@link EcmaRegex#codeUnits} writes it.
     */
    final boolean units;
    /**
     * The place of the pattern's matcher in an evaluator of the schema the pattern was compiled for, or -1: for the
     * pattern as the cache holds it, and for one whose matches use no matcher of the engine.
     */
    final int slot;
    /**
     * What the pattern is counted as in the cache, in bytes: an estimate of what it holds that is not below it. It
     * is the source, the translation as {@code java.util.regex} keeps it, and {@link #COUNT_PER_CHARACTER} bytes for
     * each character of the translation for what the engine compiles it to.
     */
    final long counted;
    /**
     * How many times a match of the pattern reads the text, or asks for its length, between two readings of the
     * clock (see {@link Utf8Chars#limit}). Between two such calls the engine takes at most about as many steps as
     * the translation has nodes (see {@link EcmaRegex.Ticks}), so a longer translation has the clock read more often.
     */
    final int interval;

    /** The interval for a translation of up to {@link #INTERVAL_SCALE} / MAX_INTERVAL characters. */
    static final int MAX_INTERVAL = 4096;
    static final int MIN_INTERVAL = 16;
    static final int INTERVAL_SCALE = 1 << 22;

    /**
     * The bytes counted for each character of a translation. {@code java.util.regex} compiles a class of Latin-1
     * characters to a table of 256 bytes whatever its length, so {@code [ab]}, four characters, holds 330 bytes or
     * so. The tests compare the count with the heap a pattern holds.
     */
    static final int COUNT_PER_CHARACTER = 128;
    private static final int COUNT_FIXED = 512;

    private SchemaPattern(String source, int kind, Pattern regex, PatternShapes.Matcher shape) {
        this.source = source;
        this.kind = kind;
        this.regex = regex;
        this.shape = shape;
        this.units = kind == REGEX && EcmaRegex.matchesCodeUnits(source);
        this.interval = regex == null
                ? MAX_INTERVAL
                : Math.max(MIN_INTERVAL, Math.min(MAX_INTERVAL, INTERVAL_SCALE / Math.max(1, regex.pattern().length())));
        this.slot = -1;
        this.counted = COUNT_FIXED + 2L * source.length()
                + (regex == null ? 0 : (2L + COUNT_PER_CHARACTER) * regex.pattern().length());
    }

    private SchemaPattern(SchemaPattern shared, int slot) {
        this.source = shared.source;
        this.kind = shared.kind;
        this.regex = shared.regex;
        this.shape = shared.shape;
        this.units = shared.units;
        this.interval = shared.interval;
        this.counted = shared.counted;
        this.slot = slot;
    }

    /** Whether a match of the pattern runs the engine, and so needs a matcher of the evaluator. */
    boolean needsMatcher() {
        return kind == REGEX;
    }

    /**
     * The pattern for one schema, with a place for its matcher in the evaluators of that schema. It shares all that
     * was compiled with this one.
     */
    SchemaPattern inSlot(int slot) {
        return new SchemaPattern(this, slot);
    }

    /**
     * The error for a pattern {@link #compile} returned null for, where {@code keyword} is the keyword that holds
     * it. A pattern that is valid ECMA-262 and that cannot be run with the same meaning says why (see
     * {@link EcmaRegex}). One that is over a limit of size is a {@link PatternTooLargeException}, which names the
     * limit, and is not said to be invalid.
     */
    static SchemaCompilationException failure(String pattern, String keyword) {
        String limit = EcmaRegex.tooLarge(pattern);
        if (limit != null) {
            String valid = EcmaRegex.wasRead(pattern)
                    ? "It is a valid ECMA-262 regular expression, and is too large: " + limit + "."
                    : "It is too large to be read: " + limit + ". Whether it is valid is not known.";
            return new PatternTooLargeException(
                    "The regular expression " + shown(pattern) + " in " + keyword + " is over a limit of size. " + valid,
                    pattern);
        }
        String reason = EcmaRegex.unsupported(pattern);
        if (reason == null && EcmaRegex.translate(pattern) != null) {
            // It is translated now: it was too large for the stack as it stood when it was compiled.
            return new PatternTooLargeException(
                    "The regular expression " + shown(pattern) + " in " + keyword + " is over a limit of size. It is a "
                            + "valid ECMA-262 regular expression, and is too large: " + EcmaRegex.LIMIT_STACK + ".",
                    pattern);
        }
        if (reason == null) {
            return new SchemaCompilationException("Invalid regular expression '" + pattern + "' in " + keyword + ".");
        }
        return new SchemaCompilationException(
                "Unsupported regular expression '" + pattern + "' in " + keyword + ". It is valid ECMA-262, but this "
                        + "library cannot run " + reason + " with the meaning ECMA-262 gives it.");
    }

    /** A pattern as a message shows it: the first 120 characters of a long one, and its length. */
    private static String shown(String pattern) {
        return pattern.length() <= 120
                ? "'" + pattern + "'"
                : "'" + pattern.substring(0, 120) + "...' (of " + pattern.length() + " characters)";
    }

    /**
     * Compiles (or fetches from the cache) a pattern; null when it is not a valid ECMA-262 regular expression, or is
     * one that cannot be run with the same meaning ({@link #failure} tells the two apart).
     *
     * <p>The cache is bounded, since the patterns of a schema may come from outside. It holds
     * {@link #MAX_CACHED_PATTERNS} patterns at most, counted as {@link #MAX_CACHED_BYTES} at most. Over either bound
     * the patterns compiled first go, oldest first. A pattern counted as more than
     * {@link #MAX_CACHED_PATTERN_BYTES} is not kept at all. A schema holds the patterns it was compiled with, so a
     * pattern that has left the cache is still matched by the validators that have it, and is compiled again only
     * for a schema compiled later.
     */
    static SchemaPattern compile(String pattern) {
        synchronized (CACHE) {
            SchemaPattern cached = CACHE.get(pattern);
            if (cached != null) {
                return cached;
            }
        }
        String translated = EcmaRegex.translate(pattern);
        if (translated == null) {
            return null;
        }
        SchemaPattern p;
        PatternShapes.Matcher shape;
        if (matchesEverything(pattern)) {
            p = new SchemaPattern(pattern, EVERYTHING, null, null);
        } else if (new EcmaRegex.Validator().isValid(pattern) && (shape = PatternShapes.of(pattern)) != null) {
            // Without the u flag a pattern matches UTF-16 code units, which only the regular expression engine does.
            p = new SchemaPattern(pattern, SHAPE, Pattern.compile(translated), shape);
        } else {
            p = new SchemaPattern(pattern, REGEX, Pattern.compile(translated), null);
        }
        return keep(p);
    }

    /** Keeps a pattern in the cache, and returns the one to use: it, or the one another thread kept first. */
    private static SchemaPattern keep(SchemaPattern p) {
        if (p.counted > MAX_CACHED_PATTERN_BYTES) {
            return p;
        }
        synchronized (CACHE) {
            SchemaPattern previous = CACHE.putIfAbsent(p.source, p);
            if (previous != null) {
                return previous;
            }
            cachedBytes += p.counted;
            Iterator<SchemaPattern> oldest = CACHE.values().iterator();
            while (CACHE.size() > MAX_CACHED_PATTERNS || cachedBytes > MAX_CACHED_BYTES) {
                cachedBytes -= oldest.next().counted;
                oldest.remove();
            }
            return p;
        }
    }

    /** The pattern the cache holds for a source, or null. */
    static SchemaPattern cached(String pattern) {
        synchronized (CACHE) {
            return CACHE.get(pattern);
        }
    }

    /**
     * What the cache holds: the number of its patterns, the bytes it counts for them, and the sum of what its
     * patterns are counted as, which is the same number.
     */
    static long[] cacheFigures() {
        synchronized (CACHE) {
            long sum = 0;
            for (SchemaPattern p : CACHE.values()) {
                sum += p.counted;
            }
            return new long[] {CACHE.size(), cachedBytes, sum};
        }
    }

    /**
     * Patterns every string matches: an unanchored (or start-anchored) {@code .*} finds an empty match anywhere;
     * {@code ^.*$} does not ({@code .} stops at a line terminator).
     */
    private static boolean matchesEverything(String p) {
        switch (p) {
            case "":
            case ".*":
            case "^.*":
            case ".*$":
            case "(.*)":
            case "^(.*)":
            case "[\\s\\S]*":
            case "^[\\s\\S]*":
            case "^[\\s\\S]*$":
                return true;
            default:
                return false;
        }
    }

    boolean matchesAll() {
        return kind == EVERYTHING;
    }

    /**
     * A matcher of the pattern for {@link #find(Matcher, Utf8Chars, long)}. Its bounds are transparent and not
     * anchoring. Over a whole text, which is all the library matches, that changes no answer. It makes the engine
     * ask the text for its length at every lookahead, word boundary and end anchor, where with the default bounds it
     * reads a field of its own, and the asking is counted towards the limit of a match (see {@link Utf8Chars}).
     */
    Matcher matcher() {
        return regex.matcher("").useTransparentBounds(true).useAnchoringBounds(false);
    }

    /**
     * Whether the pattern matches somewhere in the text, using (and resetting) a matcher of this pattern that
     * {@link #matcher} made. The text has been told the limit of time ({@link Utf8Chars#limit}).
     *
     * @throws PatternMatchLimitException when the match goes over the limit
     */
    boolean find(Matcher m, Utf8Chars text, long limitNanos) {
        if (kind == EVERYTHING) {
            return true;
        }
        try {
            return m.reset(text).find();
        } catch (StackOverflowError e) {
            // java.util.regex matches by recursion, and has no limit of depth but the stack. The error is caught
            // here, where nothing but the engine's own frames is unwound: they hold no lock and no state but the
            // matcher's, which the next match resets.
            throw PatternMatchLimitException.stack(source, limitNanos, text.length());
        }
    }

    static {
        // What a match can load for the first time is loaded now, and not deep in a recursion where the stack
        // could run out as a class is initialized, which would leave the class unusable: the engine's set of the
        // places a loop failed at, the tables of the character properties of each plane, and the exception.
        Pattern.compile("(?:(?:a*)*b|\\p{L}\\P{L})").matcher("aa").find();
        for (int c : new int[] {0x100, 0x10000, 0x20000, 0x30000, 0xE0000, 0xF0000, 0x100000, 0x40000}) {
            Character.getType(c);
            Character.toLowerCase(c);
        }
        PatternMatchLimitException.stack("", 0, 0);
        PatternMatchLimitException.time("", 1, 0);
    }

    /** Whether the pattern matches somewhere in a string (allocates a matcher, and has no limit of time). */
    boolean find(CharSequence text) {
        if (kind == EVERYTHING) {
            return true;
        }
        return regex.matcher(units && EcmaRegex.hasSurrogate(text) ? EcmaRegex.codeUnits(text) : text).find();
    }
}