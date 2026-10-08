package io.github.corvusdotnet.jsonschema;

import java.util.concurrent.ConcurrentHashMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * A compiled {@code pattern} with ECMA-262 semantics: the cheapest matcher that decides it exactly, falling back to
 * the pattern translated for {@code java.util.regex}. Patterns compile once per process (a pattern is immutable, so
 * identical patterns share one matcher).
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
    /** A process-wide index, for evaluators that keep a matcher per pattern. */
    final int id;

    private static int nextId;

    private SchemaPattern(String source, int kind, Pattern regex, PatternShapes.Matcher shape) {
        this.source = source;
        this.kind = kind;
        this.regex = regex;
        this.shape = shape;
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

    /** Whether the pattern matches somewhere in the text, using (and resetting) a matcher of this pattern. */
    boolean find(Matcher m, CharSequence text) {
        if (kind == EVERYTHING) {
            return true;
        }
        return m.reset(text).find();
    }

    /** Whether the pattern matches somewhere in a string (allocates a matcher). */
    boolean find(CharSequence text) {
        return kind == EVERYTHING || regex.matcher(text).find();
    }
}