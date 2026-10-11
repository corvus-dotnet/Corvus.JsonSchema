package io.github.corvusdotnet.jsonschema;

import java.time.Duration;

/**
 * The match of a {@code pattern} or {@code patternProperties} regular expression against one string went over a
 * limit, and was abandoned. The instance is neither valid nor invalid: every method that validates throws this
 * instead of answering.
 *
 * <p>There are two limits. The time of one match is limited to {@link CompileOptions.Builder#patternMatchLimit}, 1
 * second unless it is set ({@link #isStackLimit} is false, and {@link #limit} is the limit that was in force). And
 * {@code java.util.regex}, which runs the pattern, matches by recursion, so a match can need more of the thread's
 * stack than there is ({@link #isStackLimit} is true). That limit is the stack size of the thread that validates.
 */
public final class PatternMatchLimitException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    private final String pattern;
    private final transient Duration limit;
    private final boolean stackLimit;

    private PatternMatchLimitException(String message, String pattern, Duration limit, boolean stackLimit) {
        super(message);
        this.pattern = pattern;
        this.limit = limit;
        this.stackLimit = stackLimit;
    }

    /** A match that ran for longer than the limit on its time. */
    static PatternMatchLimitException time(String pattern, long limitNanos, int textLength) {
        Duration limit = Duration.ofNanos(limitNanos);
        return new PatternMatchLimitException(
                "The match of the regular expression " + shown(pattern) + " against a string of " + textLength
                        + " characters ran for longer than the limit of " + limit
                        + " (CompileOptions.Builder.patternMatchLimit) and was abandoned.",
                pattern, limit, false);
    }

    /** A match that needed more stack than the thread has. */
    static PatternMatchLimitException stack(String pattern, long limitNanos, int textLength) {
        return new PatternMatchLimitException(
                "The match of the regular expression " + shown(pattern) + " against a string of " + textLength
                        + " characters needed more of the thread's stack than there is (java.util.regex matches by "
                        + "recursion) and was abandoned.",
                pattern, limitNanos == 0 ? null : Duration.ofNanos(limitNanos), true);
    }

    private static String shown(String pattern) {
        return pattern.length() <= 120
                ? "'" + pattern + "'"
                : "'" + pattern.substring(0, 120) + "...' (of " + pattern.length() + " characters)";
    }

    /**
     * The pattern, as the schema has it.
     *
     * @return the pattern
     */
    public String pattern() {
        return pattern;
    }

    /**
     * The limit on the time of a match that was in force, or null when it was turned off.
     *
     * @return the limit, or null
     */
    public Duration limit() {
        return limit;
    }

    /**
     * Whether the match ran out of stack. Otherwise it ran out of time.
     *
     * @return whether the limit met was the stack of the thread
     */
    public boolean isStackLimit() {
        return stackLimit;
    }
}
