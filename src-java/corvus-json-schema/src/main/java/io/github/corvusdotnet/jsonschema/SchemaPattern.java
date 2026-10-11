package io.github.corvusdotnet.jsonschema;

import java.util.concurrent.ConcurrentHashMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * A compiled {@code pattern} with ECMA-262 semantics: the cheapest matcher that decides it exactly, falling back to
 * the pattern translated for {@code java.util.regex}. Patterns compile once per process (a pattern is immutable, so
 * identical patterns share one matcher).
 *
 * <p>A pattern that is valid with the u flag matches characters. One that is valid only without it matches UTF-16
 * code units, as ECMA-262 says: {@code ^\-?.$} does not match one character beyond the Basic Multilingual Plane, which
 * is two code units.
 */
final class SchemaPattern {
    private static final int EVERYTHING = 0;
    private static final int REGEX = 1;
    private static final int SHAPE = 2;

    private static final ConcurrentHashMap<String, SchemaPattern> CACHE = new ConcurrentHashMap<>();

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
    /** A process-wide index, for evaluators that keep a matcher per pattern. */
    final int id;
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

    private static int nextId;

    private SchemaPattern(String source, int kind, Pattern regex, PatternShapes.Matcher shape) {
        this.source = source;
        this.kind = kind;
        this.regex = regex;
        this.shape = shape;
        this.units = kind == REGEX && EcmaRegex.matchesCodeUnits(source);
        this.interval = regex == null
                ? MAX_INTERVAL
                : Math.max(MIN_INTERVAL, Math.min(MAX_INTERVAL, INTERVAL_SCALE / Math.max(1, regex.pattern().length())));
        synchronized (SchemaPattern.class) {
            this.id = nextId++;
        }
    }

    /**
     * The message for a pattern {@link #compile} returned null for, where {@code keyword} is the keyword that holds
     * it. A pattern that is valid ECMA-262 and that cannot be run with the same meaning says why (see
     * {@link EcmaRegex}).
     */
    static String failure(String pattern, String keyword) {
        String reason = EcmaRegex.unsupported(pattern);
        if (reason == null && EcmaRegex.translate(pattern) != null) {
            // It is translated now: it was too large for the stack as it stood when it was compiled.
            reason = EcmaRegex.REFUSED_TOO_LARGE;
        }
        if (reason == null) {
            return "Invalid regular expression '" + pattern + "' in " + keyword + ".";
        }
        return "Unsupported regular expression '" + pattern + "' in " + keyword + ". It is valid ECMA-262, but this "
                + "library cannot run " + reason + " with the meaning ECMA-262 gives it.";
    }

    /**
     * Compiles (or fetches from the cache) a pattern; null when it is not a valid ECMA-262 regular expression, or is
     * one that cannot be run with the same meaning ({@link #failure} tells the two apart).
     */
    static SchemaPattern compile(String pattern) {
        SchemaPattern cached = CACHE.get(pattern);
        if (cached != null) {
            return cached;
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
        SchemaPattern previous = CACHE.putIfAbsent(pattern, p);
        return previous != null ? previous : p;
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