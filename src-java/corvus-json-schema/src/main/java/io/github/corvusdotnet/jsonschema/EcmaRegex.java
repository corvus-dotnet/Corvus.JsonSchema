package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.BitSet;
import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.regex.PatternSyntaxException;

/**
 * Translates ECMA-262 regular expressions to {@code java.util.regex} patterns with the same meaning, validating them
 * as ECMA-262 does. The grammar is that of ECMAScript 2025.
 *
 * <p><b>Grammars.</b> A pattern is read first with the grammar of the {@code u} flag, as JSON Schema's {@code pattern}
 * specifies. A pattern that is not valid with it is read with the Annex B grammar of a pattern with no flag, as many
 * schemas hold such patterns (identity escapes like {@code \&}, lone braces). A pattern that is valid with the
 * {@code u} flag matches code points. A pattern that is valid only without it matches UTF-16 code units, as ECMA-262
 * says: its dot takes one half of a character beyond the Basic Multilingual Plane, and such a character written in
 * it is two atoms (see {@link #matchesCodeUnits} and {@link #codeUnits}). {@link #isValid}, which backs the
 * {@code regex} format, accepts only the {@code u} flag grammar.
 *
 * <p><b>What the translation writes.</b> The pattern is read into a tree, and the tree is written for
 * {@code java.util.regex} so that nothing depends on how that engine reads a construct ECMA-262 reads differently:
 *
 * <ul>
 *   <li>every literal is a code point escape, and every class is its set of code points, written as ranges. So
 *       {@code .}, {@code \d}, {@code \w}, {@code \s}, a negated class and a class that holds a class escape all
 *       mean what ECMA-262 says;
 *   <li>a {@code \p{...}} escape is the set of its property in the Unicode version of {@link EcmaUnicodeData}, for
 *       every property, value and alias ECMA-262 lists. It does not depend on the JDK's own Unicode tables, which
 *       differ between Java 17, 21 and 25 and lack most binary properties and Script_Extensions;
 *   <li>the modifiers of a group such as {@code (?i:...)}, {@code (?m:...)} or {@code (?s:...)} are applied while
 *       reading: a case-insensitive literal or class becomes the set of the characters equivalent to it under the
 *       Canonicalize of ECMA-262, {@code ^} and {@code $} become assertions about ECMA-262's four line terminators,
 *       and the dot becomes every character. No flag of {@code java.util.regex} stands for them, as its
 *       case-insensitive and multiline matching follow other rules;
 *   <li>a group is written without capture unless a backreference needs it, and never by name, so a name may be
 *       any ECMA-262 identifier and may be shared by groups in separate alternatives;
 *   <li>a lookbehind is written as it is when its length is bounded, and otherwise in a form that finds where it
 *       starts by search (see {@link Emitter#lookbehind}). A pattern with a lookbehind also ends with a branch that
 *       holds a supplementary character and is never taken, which makes {@code java.util.regex} step over the text
 *       of a lookbehind by code point. Without it a lookbehind counts UTF-16 code units;
 *   <li>a repetition that must run twice or more, and whose body matches the empty string in some places only, is
 *       written with one copy of the body for each iteration it must run (see {@link Emitter#repeat}).
 *       {@code java.util.regex} leaves a repetition after an iteration that matched nothing, where ECMA-262 goes on
 *       to the iterations that are still due.
 * </ul>
 *
 * <p><b>Backreferences.</b> A backreference means "the text the group captured when it last took part on the way the
 * match took, or nothing". {@code java.util.regex} differs from ECMA-262 on what a group holds:
 *
 * <ul>
 *   <li>a backreference to a group that did not take part fails there, and matches the empty string in ECMA-262;
 *   <li>it keeps what a group captured in an earlier iteration of a repetition, where ECMA-262 unsets the groups
 *       of a body when an iteration starts;
 *   <li>it takes an iteration that matched nothing as the last of a repetition, with what the iteration captured,
 *       where ECMA-262 does not take one once the minimum is met;
 *   <li>it does not restore the groups inside a lookaround when the match fails after the lookaround and goes back,
 *       nor the groups inside a repeated group whose body has one way to match when the repetition gives back an
 *       iteration;
 *   <li>it runs a lookbehind forwards, where ECMA-262 runs it backwards, so that a backreference in it reads a
 *       group that stands after it and a group of a repetition in it holds the leftmost iteration;
 *   <li>its case-insensitive backreference compares by the case mappings of the JDK, and ECMA-262 by its
 *       Canonicalize.
 * </ul>
 *
 * <p>Where none of these can be seen, a backreference is written as one, behind a test of an empty group that marks
 * that its group took part when that is not known (see {@link Emitter#backreference}). Otherwise the pattern is
 * written by paths (see {@link Emitter#translateByPaths}): once for each way through the choices that decide what the
 * groups it reads hold, so that every backreference stands where its group is known to be set, by a group of the
 * translation on that way, or known not to be. The ways of one part of the pattern are written as a tree, with what
 * ways next to each other start with written once (see {@link Emitter#joined}). A lookahead whose groups are read
 * after it takes the first way its body matches, each way written after tests that the ways before it fail (see
 * {@link Emitter#firstWay}). A way on which an iteration past the minimum of its repetition matches nothing is not
 * one ECMA-262 takes and is not written, and an iteration that another follows is written apart from the ways (see
 * {@link Emitter#part}), so the text of a counted repetition grows with the count and not twice for each iteration.
 * A group of a lookaround that is read in the same iteration of a loop is set by another run of the lookaround where
 * the match can have come back past a later run of it (see {@link Emitter#fresh}). A lookbehind is written with the
 * captures its backward run gives (see {@link Emitter#edge}). A case-insensitive backreference is compared by
 * {@code java.util.regex} for the characters it compares as ECMA-262 does on the JVM the library runs on, and by a
 * comparison written out for the others (see {@link Emitter#folded}).
 *
 * <p><b>What is refused.</b> A pattern that is valid with the u flag is translated, with one exception and within
 * limits of size. A pattern under either is refused, and is not given an answer that may be wrong.
 * {@link #unsupported} says why, with one of the {@code REFUSED_} constants.
 *
 * <p>The exception is a lookbehind with a group that is read, in it or after it, where what the group holds can be
 * told only by matching the lookbehind backwards, which {@code java.util.regex} does not do. The translator writes
 * the lookbehinds for which it can be told while matching forwards (see {@link Emitter#edge}): groups at a fixed
 * distance from the end of the lookbehind, alternatives, and a repetition of a body of fixed length that stands
 * first. The others are refused:
 *
 * <ul>
 *   <li>{@link #REFUSED_GROUP_IN_LOOKBEHIND}, for a group of the lookbehind that is read after it, as in
 *       {@code (?<=(x*?)a*)\1}, {@code (?<=(a)+b*)x\1} and {@code (?<=(a|ab)c*)\1};
 *   <li>{@link #REFUSED_BACKREFERENCE_IN_LOOKBEHIND}, for a backreference in the lookbehind to a group of it, as in
 *       {@code (?<=\1a*(b))x} and {@code (?<=\1(a)*)x}.
 * </ul>
 *
 * <p>The limits of size are {@link #REFUSED_TOO_MANY_PATHS} (more plans of one scope than {@link Emitter#MAX_PLANS},
 * more text than {@link Emitter#MAX_PATHS_LENGTH}, more text written on the way to it than
 * {@link Emitter#MAX_PATHS_WRITTEN}, or more iterations of a lookahead written one by one than
 * {@link Emitter#MAX_UNROLLED_COPIES}), {@link #REFUSED_EMPTY_ITERATIONS} (more copies of a body than
 * {@link Emitter#MAX_UNROLLED_COPIES}, or more text of them than {@link Emitter#MAX_UNROLLED_LENGTH}) and
 * {@link #REFUSED_TOO_LARGE} (more than {@code java.util.regex} compiles on the stack of the thread). A pattern with
 * no backreference is refused only for the last two. The limits are well beyond what a pattern of ordinary size
 * comes to: none of the 98,839 patterns of the ECMA-262 regular expression test data reaches one.
 *
 * <p>{@link #REFUSED_LOOKAROUND_IN_REPETITION} is kept for a reference to a group of a lookaround that cannot be
 * given a run of the lookaround of its own where the match can come back to it (see {@link Emitter#bodyText}). No
 * pattern is known that it refuses.
 *
 * <p>A translation that {@code java.util.regex} rejects is a fault of this class, and is thrown as an
 * {@link IllegalStateException} that says so.
 */
final class EcmaRegex {
    private EcmaRegex() {
    }

    /**
     * The backreference is in a lookbehind, which ECMA-262 matches backwards, and reads a group of that lookbehind
     * whose place is not at a fixed distance from it.
     */
    static final String REFUSED_BACKREFERENCE_IN_LOOKBEHIND =
            "a backreference inside a lookbehind to a group of that lookbehind that is not at a fixed distance from it";
    /**
     * The group is in a lookbehind, which ECMA-262 matches backwards, and what it captures that way cannot be told
     * by matching forwards.
     */
    static final String REFUSED_GROUP_IN_LOOKBEHIND =
            "a backreference to a group inside a lookbehind whose text depends on the lookbehind being matched backwards";
    /** A backreference that the form with markers cannot serve. The pattern is then written by paths. */
    private static final String NOT_BY_MARKERS = "markers";
    /**
     * The repetition must run so many times, over a body that matches the empty string in some places only, that
     * writing each of those iterations apart is too much.
     */
    static final String REFUSED_EMPTY_ITERATIONS =
            "a repetition this long of a body that can match the empty string";
    /** The group is set in a lookaround that can run again before the match goes back to the backreference. */
    static final String REFUSED_LOOKAROUND_IN_REPETITION =
            "a backreference to a group of a lookaround, in a repetition that can go back to before the backreference";
    /** The pattern would be written with more alternatives, one for each way through its groups, than is kept. */
    static final String REFUSED_TOO_MANY_PATHS = "a pattern with this many ways through the groups it reads";
    /** The translation is more than {@code java.util.regex} compiles on the stack of the thread it is called on. */
    static final String REFUSED_TOO_LARGE = "a pattern too large for java.util.regex to compile";

    /** Groups may nest this deep. It bounds the recursion of the readers. */
    static final int MAX_NESTING = 200;
    /** The most characters of a pattern, and of what is written for {@code java.util.regex} to run it. */
    static final int MAX_PATTERN_LENGTH = 1 << 20;
    static final int MAX_TRANSLATION_LENGTH = 1 << 20;

    // What the message of a pattern that is over a limit of size says of the limit (see TooLarge).
    static final String LIMIT_PATTERN = "it has more than 1,048,576 characters";
    static final String LIMIT_NESTING = "its groups are nested more than 200 deep";
    static final String LIMIT_TRANSLATION =
            "what is written for java.util.regex to run it is more than 1,048,576 characters";
    static final String LIMIT_PLANS = "it is written once for each way through the groups its backreferences read, and "
            + "one part of it has more than 8,192 ways";
    static final String LIMIT_WRITTEN = "writing it for java.util.regex takes more than 16,777,216 characters of text";
    static final String LIMIT_COPIES = "it needs more than 200 copies of the body of a repetition";
    static final String LIMIT_STACK = "java.util.regex needs more of the thread's stack to compile it than there is";
    static final String LIMIT_GROUPS = "it has more than 32,768 capturing groups";
    static final String LIMIT_SETS = "its classes hold more than 524,288 ranges of characters";
    static final String LIMIT_GROUP_PARTS =
            "the number of its groups times the number of its parts is more than 268,435,456";
    /** The most capturing groups of a pattern, and the most numbers in the sets of its classes (two to a range). */
    static final int MAX_GROUPS = 1 << 15;
    static final int MAX_SET_NUMBERS = 1 << 20;

    /**
     * The translation of an ECMA-262 pattern. It is null when the pattern is not a valid one, and when it is valid
     * and cannot be run with the same meaning (then {@link #unsupported} says why).
     */
    static String translate(String pattern) {
        try {
            return translation(pattern);
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    /**
     * What is over a limit of size in a pattern that is refused for that, as the end of a sentence that names the
     * limit and its value, or null when the pattern is not refused for its size. {@link #wasRead} tells whether the
     * pattern is known to be valid.
     */
    static String tooLarge(String pattern) {
        try {
            translation(pattern);
            return null;
        } catch (TooLarge e) {
            return e.limit;
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    /**
     * Whether a pattern that {@link #tooLarge} refuses was read to its end before the limit was met, and so is a
     * valid ECMA-262 pattern. One that is too long, or nested too deep, to be read is not known to be.
     */
    static boolean wasRead(String pattern) {
        try {
            translation(pattern);
            return true;
        } catch (TooLarge e) {
            return e.read;
        } catch (IllegalArgumentException e) {
            return true;
        }
    }

    /**
     * The length the limit on the size of a translation counts for a pattern: that of its translation of reference
     * (see {@link #translation}), which is the same on every JDK. For tests.
     */
    static int measuredLength(String pattern) {
        Parser parser = new Parser(pattern, true);
        Emitter emitter = new Emitter(parser, parser.parse());
        emitter.reference = true;
        return emitter.translate().length();
    }

    /** Why a valid ECMA-262 pattern cannot be run by this library, or null when it can be or is not valid. */
    static String unsupported(String pattern) {
        try {
            translation(pattern);
            return null;
        } catch (Unsupported e) {
            return e.getMessage();
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    /**
     * The translator's verdict on a pattern read with the u flag: 1 valid, 0 not ECMA-262, 2 valid ECMA-262 that
     * cannot be run with the same meaning (for tests).
     */
    static int translatorVerdict(String pattern) {
        Parser parser = new Parser(pattern, true);
        Node root;
        try {
            root = parser.parse();
        } catch (IllegalArgumentException e) {
            return 0;
        }
        try {
            new Emitter(parser, root).translate();
            return 1;
        } catch (Unsupported e) {
            return 2;
        }
    }

    /**
     * Where the translation of a pattern without the u flag keeps the surrogates: the code unit U+D800 + n is the
     * character U+F0000 + n, in the pattern and in the text (see {@link #codeUnits}).
     */
    static final int UNIT_BASE = 0xF0000;
    private static final int[] SURROGATES = {Character.MIN_SURROGATE, Character.MAX_SURROGATE};
    private static final int[] UNITS = {UNIT_BASE, UNIT_BASE + 0x7FF};

    /**
     * Whether a pattern that {@link #translate} translates is valid only without the u flag. ECMA-262 matches such
     * a pattern against the UTF-16 code units of the text, and its translation has to be given the text that
     * {@link #codeUnits} makes of one with a character beyond the Basic Multilingual Plane.
     */
    static boolean matchesCodeUnits(String pattern) {
        try {
            new Parser(pattern, true).parse();
            return false;
        } catch (IllegalArgumentException e) {
            return true;
        }
    }

    /** Whether a text has a surrogate, and so is to be written by {@link #codeUnits} for a pattern of code units. */
    static boolean hasSurrogate(CharSequence text) {
        for (int k = 0, n = text.length(); k < n; k++) {
            if (Character.isSurrogate(text.charAt(k))) {
                return true;
            }
        }
        return false;
    }

    /**
     * A text as the translation of a pattern without the u flag reads it: each UTF-16 code unit is one character.
     * A surrogate U+D800 + n is written as the character U+F0000 + n, so that {@code java.util.regex}, which
     * matches by character, takes the two halves of a pair one at a time. No other character of the result is beyond
     * the Basic Multilingual Plane.
     */
    static String codeUnits(CharSequence text) {
        StringBuilder sb = new StringBuilder(text.length() + 16);
        for (int k = 0, n = text.length(); k < n; k++) {
            char c = text.charAt(k);
            if (Character.isSurrogate(c)) {
                sb.appendCodePoint(c - Character.MIN_SURROGATE + UNIT_BASE);
            } else {
                sb.append(c);
            }
        }
        return sb.toString();
    }

    /** Whether a string is a valid ECMA-262 regular expression with the u flag (the {@code regex} format). */
    static boolean isValid(CharSequence pattern) {
        return new Validator().isValid(pattern);
    }

    private static String translation(String pattern) {
        if (pattern.length() > MAX_PATTERN_LENGTH) {
            throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_PATTERN, false);
        }
        Parser parser = new Parser(pattern, true);
        Node root;
        try {
            root = parser.parse();
        } catch (TooLarge e) {
            throw e;
        } catch (IllegalArgumentException unicodeError) {
            parser = new Parser(pattern, false);
            root = parser.parse();
        }
        // Two things are written with what the engine of this JVM knows, so their length depends on the JDK: the
        // comparison of a case-insensitive backreference, which uses the engine's for the characters it compares
        // rightly (see Emitter#folded), and the set of a property the engine has a class for, which is that class
        // and what corrects it (see Emitter#engineSet). A limit of size must not depend on the JDK: a pattern is
        // refused on every JDK or on none. So the translation is made first for the engine of reference, that of
        // JDK 17, which gets the most wrong, and that one is held to the limits. When it wrote either, the pattern
        // is translated again for this engine, which is the shorter text.
        Emitter emitter = new Emitter(parser, root);
        emitter.reference = true;
        String translated = emitter.translate();
        if (!emitter.paddedSets && (!emitter.comparedByEngine || Emitter.isReference(parser.unicode))) {
            return translated;
        }
        try {
            Parser again = new Parser(pattern, parser.unicode);
            return new Emitter(again, again.parse()).translate();
        } catch (TooLarge e) {
            return translated;
        }
    }

    /** Thrown for a valid pattern that cannot be run with the same meaning. The message is the reason. */
    private static class Unsupported extends IllegalArgumentException {
        private static final long serialVersionUID = 1L;

        Unsupported(String reason) {
            super(reason);
        }
    }

    /**
     * Thrown for a pattern that is over a limit of size. The limits keep a pattern that comes from outside from
     * taking the translator, or {@code java.util.regex} when it compiles what is written, more than a fraction of a
     * second or more than a few megabytes: the pattern and the translation are at most 1,048,576 characters, and
     * what multiplies the text on the way to it is bounded too (see {@link Emitter#MAX_PLANS},
     * {@link Emitter#MAX_PATHS_WRITTEN} and {@link Emitter#MAX_UNROLLED_COPIES}). {@link #limit} ends the sentence
     * "the pattern is too large:" with the limit and its value. A pattern over a limit is never said to be invalid.
     */
    private static final class TooLarge extends Unsupported {
        private static final long serialVersionUID = 1L;

        final String limit;
        /** Whether the whole pattern was read, and found valid, before the limit was met. */
        final boolean read;

        TooLarge(String reason, String limit) {
            this(reason, limit, true);
        }

        TooLarge(String reason, String limit, boolean read) {
            super(reason);
            this.limit = limit;
            this.read = read;
        }
    }

    // The classes ECMA-262 defines, as sets. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and the
    // dot is everything but the four line terminators.
    private static final int[] DIGIT = {'0', '9'};
    private static final int[] WORD = {'0', '9', 'A', 'Z', '_', '_', 'a', 'z'};
    private static final int[] SPACE = {
        0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F, 0x205F,
        0x205F, 0x3000, 0x3000, 0xFEFF, 0xFEFF,
    };
    private static final int[] LINE_TERMINATORS = {'\n', '\n', '\r', '\r', 0x2028, 0x2029};
    private static final int[] DOT = EcmaUnicode.complement(LINE_TERMINATORS);
    /**
     * With the u flag grammar and case-insensitive matching, {@code \w} and {@code \b} also count the two characters
     * that fold to an ASCII letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
     */
    private static final int[] FOLD_WORD = {'0', '9', 'A', 'Z', '_', '_', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A};
    private static final int[] NOT_ASCII_LETTER = EcmaUnicode.complement(new int[] {'A', 'Z', 'a', 'z'});
    private static final int[] SUPPLEMENTARY = {0x10000, EcmaUnicode.MAX};
    private static final int[] NO_CHARACTERS = new int[0];

    // The kinds of node.
    private static final int EMPTY = 0;
    private static final int CHAR = 1;
    private static final int SET = 2;
    private static final int CAT = 3;
    private static final int ALT = 4;
    private static final int GROUP = 5;
    private static final int REPEAT = 6;
    private static final int BOL = 7;
    private static final int EOL = 8;
    /** {@code ^} and {@code $} inside a group with the m modifier. */
    private static final int LINE_START = 9;
    private static final int LINE_END = 10;
    private static final int WORD_BOUNDARY = 11;
    private static final int NOT_WORD_BOUNDARY = 12;
    private static final int LOOK = 13;
    private static final int BACKREF = 14;
    // Nodes that are in no pattern. They are written in the test of what remains of a lookahead (see Emitter#guard).
    /** Text of the translation, written as it is. */
    private static final int RAW = 15;
    /** The end of a group that is open where the test stands. */
    private static final int CLOSE = 16;
    /** One more iteration of a repetition whose minimum is met: its groups unset, and not the empty string. */
    private static final int ITERATION = 17;
    /** The end of an iteration that is open where the test stands, and that must not have matched nothing. */
    private static final int MOVED = 18;

    /** The max of a quantifier with no upper bound. */
    private static final int UNBOUNDED = -1;
    /** The largest bound of a counted quantifier. No string reaches a larger count. */
    private static final int MAX_COUNT = Integer.MAX_VALUE - 1;

    // What is known, where a backreference stands, of whether a group it names has taken part.
    private static final int NEVER = 0;
    private static final int MAYBE = 1;
    private static final int DEFINITE = 2;

    private static final Node[] NO_NODES = new Node[0];

    /** One element of the syntax tree of a pattern. */
    private static final class Node {
        final int kind;
        /** The code point of a CHAR. */
        int codePoint;
        /** The set of a SET. */
        int[] set;
        /**
         * When not null, another way to make up the set of a SET: the sets of properties that java.util.regex has a
         * class for, and {@link #rest}, all together, or every character outside them when {@link #partsNegated}.
         */
        int[][] parts;
        int[] rest;
        boolean partsNegated;
        /** The children of CAT and ALT, and the one child of GROUP, REPEAT and LOOK. */
        Node[] subs = NO_NODES;
        /** The bounds of a REPEAT, and whether it is lazy. */
        int min;
        int max;
        boolean lazy;
        /** Marks a REPEAT whose last iteration is written apart from the ones before it. */
        boolean lastApart;
        /** Marks a REPEAT that is written possessive (see {@link Emitter#markPossessive}). */
        boolean possessive;
        /** The number of the capturing group of a GROUP, counted from 1 as ECMA-262 does, or 0. */
        int group;
        /**
         * The groups a BACKREF names. A name can belong to several groups in separate alternatives, of which at most
         * one has taken part at any time.
         */
        int[] groups;
        /** For each of {@link #groups}, what is known of whether it has taken part (NEVER, MAYBE or DEFINITE). */
        int[] statuses;
        /** For each of {@link #groups}, whether java.util.regex is to compare its text ignoring ASCII case. */
        boolean[] ignoreAsciiCase;
        /** The group name of a named BACKREF until it is resolved. */
        String name;
        /** Marks a BACKREF, WORD_BOUNDARY or NOT_WORD_BOUNDARY inside a case-insensitive group. */
        boolean fold;
        /** The direction and the sense of a LOOK. */
        boolean behind;
        boolean negative;
        /** The capturing groups inside the node are those numbered above firstGroup, up to lastGroup. */
        int firstGroup;
        int lastGroup;
        /** The least and the greatest number of characters the node matches, once measured. */
        long minLength = -1;
        long maxLength;
        /** Marks a BACKREF while the groups it names are measured for it. */
        boolean measuring;
        /** Marks a BACKREF that reads nothing on any path to it. */
        boolean dropped;
        /** Marks a lookahead that sets its groups on some of its ways only (see {@link Emitter#firstWay}). */
        boolean byWays;
        /**
         * The text of a RAW. For a CLOSE, the group of the translation that holds the rest of the text from where
         * the group started, or with {@link #pieces} a pattern that matches from that place only.
         */
        String text;
        boolean pieces;
        /** For a CLOSE: where the text of the group starts in the translation, and the scope it was written in. */
        int[] from;
        int scope;
        /**
         * For a CLOSE of a group that started in an iteration of a loop: the group that holds the rest of the text
         * from where the iteration started, and backreferences to the text from there to the group. Without them
         * ({@link #looped}), what the group holds cannot be told in a test.
         */
        String anchor;
        String prefix;
        boolean looped;
        /** The groups the node can set for what follows it, and the groups the backreferences in it read. */
        BitSet sets;
        BitSet reads;

        Node(int kind) {
            this.kind = kind;
        }
    }

    /**
     * Reads a pattern by recursive descent into a tree. With {@code unicode} it applies the grammar of the u flag,
     * and without it the Annex B grammar of a pattern with no flag.
     */
    private static final class Parser {
        private final String pattern;
        private final int[] p;
        final boolean unicode;
        private int i;
        private int depth;
        /**
         * The number of groups and whether any is named, from a scan of the whole pattern before it is read, as the
         * meaning of {@code \1} and {@code \k} depends on groups that may follow them.
         */
        int groupCount;
        private boolean hasNames;
        private int groups;
        /** For each group name, its groups: the number of each, then the alternatives that enclose it. */
        private final Map<String, List<int[]>> names = new HashMap<>();
        private final List<Node> namedRefs = new ArrayList<>();
        /** The alternatives that enclose the current position, innermost last, as disjunction and alternative. */
        private int[] path = new int[16];
        private int pathLength;
        private int disjunctions;
        /** The modifiers in force, which only a group such as {@code (?i:...)} changes. */
        private boolean ignoreCase;
        private boolean multiline;
        private boolean dotAll;
        /** The ranges a class has gathered so far. */
        private int[] pairs = new int[16];
        private int pairCount;
        /** The set of the class atom {@link #classAtom} last read, when it was a class escape, and null otherwise. */
        private int[] atomSet;
        /**
         * The set of the property of the {@code \p} or {@code \P} escape {@link #classEscape} last read, when
         * java.util.regex has a class for it, and whether the escape negated it.
         */
        private int[] engineProperty;
        private boolean enginePropertyNegated;
        /** The numbers in the sets made so far, and the complement of each property that was negated. */
        private long setNumbers;
        private final Map<int[], int[]> complements = new java.util.IdentityHashMap<>();
        private final Map<int[], int[]> closures = new java.util.IdentityHashMap<>();

        Parser(String pattern, boolean unicode) {
            this.pattern = pattern;
            // Without the u flag a pattern is a sequence of UTF-16 code units, each an atom of its own.
            this.p = unicode ? pattern.codePoints().toArray() : pattern.chars().toArray();
            this.unicode = unicode;
        }

        private IllegalArgumentException error(String message) {
            return new IllegalArgumentException(message + " at " + i + " in " + pattern);
        }

        Node parse() {
            scanGroups();
            if (groupCount > MAX_GROUPS) {
                // What is worked out for a pattern holds a bit for each group, in many places.
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_GROUPS, false);
            }
            Node root = disjunction();
            if (i < p.length) {
                throw error(p[i] == ')' ? "unmatched ')'" : "unexpected character");
            }
            for (Node ref : namedRefs) {
                List<int[]> named = names.get(ref.name);
                if (named == null) {
                    throw error("reference to an unknown group name");
                }
                ref.groups = new int[named.size()];
                for (int k = 0; k < ref.groups.length; k++) {
                    ref.groups[k] = named.get(k)[0];
                }
            }
            return root;
        }

        /** Counts the capturing groups of the pattern and notes whether any is named. */
        private void scanGroups() {
            boolean inClass = false;
            for (int k = 0; k < p.length; k++) {
                int c = p[k];
                if (c == '\\') {
                    k++;
                } else if (c == '[') {
                    inClass = true;
                } else if (c == ']') {
                    inClass = false;
                } else if (c == '(' && !inClass) {
                    if (k + 1 >= p.length || p[k + 1] != '?') {
                        groupCount++;
                    } else if (k + 3 < p.length && p[k + 2] == '<' && p[k + 3] != '=' && p[k + 3] != '!') {
                        groupCount++;
                        hasNames = true;
                    }
                }
            }
        }

        private boolean more() {
            return i < p.length;
        }

        private int peek() {
            return i < p.length ? p[i] : -1;
        }

        private boolean eat(int c) {
            if (i < p.length && p[i] == c) {
                i++;
                return true;
            }
            return false;
        }

        private Node disjunction() {
            if (++depth > MAX_NESTING) {
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_NESTING, false);
            }
            if (2 * pathLength + 2 > path.length) {
                path = Arrays.copyOf(path, path.length * 2);
            }
            int level = pathLength++;
            path[2 * level] = ++disjunctions;
            path[2 * level + 1] = 0;
            Node first = alternative();
            Node result = first;
            if (peek() == '|') {
                List<Node> alternatives = new ArrayList<>();
                alternatives.add(first);
                while (eat('|')) {
                    path[2 * level + 1]++;
                    alternatives.add(alternative());
                }
                result = oneCharacter(alternatives);
                if (result == null) {
                    result = new Node(ALT);
                    result.subs = alternatives.toArray(NO_NODES);
                }
            }
            pathLength--;
            depth--;
            return result;
        }

        /**
         * Alternatives that are each one character, as the one set of them, or null when some alternative is
         * anything else. {@code (?:a|b)} takes what {@code [ab]} takes, whatever the order, and holds no group. It
         * matters under a repetition: {@code java.util.regex} runs a repeated class in a loop, and a repeated
         * choice by recursion, a level or more for each iteration, which a long string runs out of stack on.
         */
        private static Node oneCharacter(List<Node> alternatives) {
            int[] set = NO_CHARACTERS;
            for (Node a : alternatives) {
                if (a.kind == CHAR) {
                    set = EcmaUnicode.union(set, new int[] {a.codePoint, a.codePoint});
                } else if (a.kind == SET && a.parts == null) {
                    set = EcmaUnicode.union(set, a.set);
                } else {
                    return null;
                }
            }
            Node node = new Node(SET);
            node.set = set;
            return node;
        }

        private Node alternative() {
            List<Node> terms = new ArrayList<>();
            while (more() && peek() != '|' && peek() != ')') {
                terms.add(term());
            }
            if (terms.isEmpty()) {
                return new Node(EMPTY);
            }
            if (terms.size() == 1) {
                return terms.get(0);
            }
            Node cat = new Node(CAT);
            cat.subs = terms.toArray(NO_NODES);
            return cat;
        }

        /**
         * The node for one literal character. Inside a case-insensitive group that is the set of the characters
         * equivalent to it.
         */
        private Node charNode(int c) {
            if (ignoreCase) {
                int[] set = EcmaUnicode.foldClosure(new int[] {c, c}, unicode);
                if (set.length != 2 || set[0] != set[1]) {
                    return setNode(set);
                }
            }
            Node node = new Node(CHAR);
            node.codePoint = !unicode && c >= Character.MIN_SURROGATE && c <= Character.MAX_SURROGATE
                    ? c - Character.MIN_SURROGATE + UNIT_BASE
                    : c;
            return node;
        }

        private Node setNode(int[] set) {
            if ((setNumbers += set.length) > MAX_SET_NUMBERS) {
                // A class of a large property is some thousands of numbers, and a pattern can name one in every few
                // characters. Written out, this many ranges are far over the limit on the translation.
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_SETS, false);
            }
            Node node = new Node(SET);
            node.set = unicode ? set : unitSet(set);
            return node;
        }

        /**
         * The set of a pattern without the u flag as the translation holds it. Such a pattern matches UTF-16 code
         * units, and the text it is matched against has each half of a surrogate pair written as one character of
         * {@link #UNIT_BASE} (see {@link #codeUnits}). So the surrogates of the set go there. What the set holds of
         * the other characters beyond the Basic Multilingual Plane is left as it is: the text has none of them. A
         * set that holds every surrogate, such as the dot or a negated class, is written as it was.
         */
        private static int[] unitSet(int[] set) {
            int[] surrogates = intersection(set, SURROGATES);
            int[] units = intersection(set, UNITS);
            boolean every = surrogates.length == 2 && surrogates[0] == Character.MIN_SURROGATE
                    && surrogates[1] == Character.MAX_SURROGATE;
            boolean everyUnit = units.length == 2 && units[0] == UNIT_BASE && units[1] == UNIT_BASE + 0x7FF;
            if (surrogates.length == 0 ? units.length == 0 : every && everyUnit) {
                return set;
            }
            int[] moved = new int[surrogates.length];
            for (int k = 0; k < moved.length; k++) {
                moved[k] = surrogates[k] - Character.MIN_SURROGATE + UNIT_BASE;
            }
            int[] others = EcmaUnicode.difference(EcmaUnicode.difference(set, UNITS), SURROGATES);
            return EcmaUnicode.union(others, moved);
        }

        private static int[] intersection(int[] a, int[] b) {
            return EcmaUnicode.difference(a, EcmaUnicode.complement(b));
        }

        /** The set that also holds, inside a case-insensitive group, every character equivalent to a member. */
        private int[] folded(int[] set) {
            if (!ignoreCase) {
                return set;
            }
            // The set of a property is one array wherever the property is named, and its closure is worked out once.
            return closures.computeIfAbsent(set, s -> EcmaUnicode.foldClosure(s, unicode));
        }

        private Node term() {
            int c = peek();
            int groupsBefore = groups;
            int at = i;
            boolean quantifiable = true;
            Node atom;
            switch (c) {
                case '^':
                    i++;
                    atom = new Node(multiline ? LINE_START : BOL);
                    quantifiable = false;
                    break;
                case '$':
                    i++;
                    atom = new Node(multiline ? LINE_END : EOL);
                    quantifiable = false;
                    break;
                case '(':
                    atom = group();
                    // Annex B lets a quantifier follow a lookahead. The u flag does not, and neither lets one follow
                    // a lookbehind.
                    quantifiable = atom.kind != LOOK || (!unicode && !atom.behind);
                    break;
                case '.':
                    i++;
                    atom = setNode(dotAll ? EcmaUnicode.ANY : DOT);
                    break;
                case '[':
                    atom = characterClass();
                    break;
                case '\\':
                    atom = atomEscape();
                    quantifiable = atom.kind != WORD_BOUNDARY && atom.kind != NOT_WORD_BOUNDARY;
                    break;
                case '*':
                case '+':
                case '?':
                    throw error("nothing to repeat");
                case '{':
                    if (unicode || looksLikeQuantifier()) {
                        throw error("nothing to repeat");
                    }
                    i++;
                    atom = charNode('{');
                    break;
                case ']':
                case '}':
                    if (unicode) {
                        throw error("lone bracket");
                    }
                    i++;
                    atom = charNode(c);
                    break;
                default:
                    i++;
                    atom = charNode(c);
                    break;
            }
            atom.firstGroup = groupsBefore;
            atom.lastGroup = groups;
            Node repeat = quantifier();
            if (repeat == null) {
                return atom;
            }
            if (!quantifiable) {
                i = at;
                throw error("nothing to repeat");
            }
            repeat.subs = new Node[] {atom};
            if (zeroWidth(atom)) {
                // A body that takes no character matches the same, with the same groups, however often it is run in
                // one place, and ECMA-262 takes an iteration that matched nothing only while iterations are due. So
                // one iteration stands for any number of them. java.util.regex would run the whole count, without
                // reading the text, which is where a match is counted (see Ticks).
                repeat.min = Math.min(repeat.min, 1);
                repeat.max = repeat.max == 0 ? 0 : 1;
            }
            repeat.firstGroup = groupsBefore;
            repeat.lastGroup = groups;
            return repeat;
        }

        private static boolean isDigit(int c) {
            return c >= '0' && c <= '9';
        }

        /** Whether the text at the current '{' reads as {@code {n}}, {@code {n,}} or {@code {n,m}}. */
        private boolean looksLikeQuantifier() {
            int k = i + 1;
            int start = k;
            while (k < p.length && isDigit(p[k])) {
                k++;
            }
            if (k == start || k >= p.length) {
                return false;
            }
            if (p[k] == '}') {
                return true;
            }
            if (p[k] != ',') {
                return false;
            }
            k++;
            while (k < p.length && isDigit(p[k])) {
                k++;
            }
            return k < p.length && p[k] == '}';
        }

        /** Reads a run of decimal digits, which saturates at {@link #MAX_COUNT}. */
        private int number() {
            int start = i;
            long v = 0;
            while (i < p.length && isDigit(p[i])) {
                v = Math.min(v * 10 + (p[i] - '0'), MAX_COUNT);
                i++;
            }
            if (start == i) {
                throw error("expected a number");
            }
            while (start < i - 1 && p[start] == '0') {
                start++;
            }
            numberStart = start;
            numberEnd = i;
            return (int) v;
        }

        /** Where the digits of the number read last stand in the pattern, without the zeros it starts with. */
        private int numberStart;
        private int numberEnd;

        /**
         * Whether the number read last is less than the one whose digits stand at {@code start} to {@code end}. The
         * numbers are compared as they are written: a count is kept only up to {@link #MAX_COUNT}, and ECMA-262
         * compares the two counts of a quantifier whatever their size.
         */
        private boolean lessThan(int start, int end) {
            if (numberEnd - numberStart != end - start) {
                return numberEnd - numberStart < end - start;
            }
            for (int k = 0; k < end - start; k++) {
                if (p[numberStart + k] != p[start + k]) {
                    return p[numberStart + k] < p[start + k];
                }
            }
            return false;
        }

        /** Whether a node takes no character wherever it matches, by its form: a backreference is not counted so. */
        private static boolean zeroWidth(Node n) {
            switch (n.kind) {
                case CHAR:
                case SET:
                case BACKREF:
                    return false;
                case REPEAT:
                    return n.max == 0 || zeroWidth(n.subs[0]);
                case LOOK:
                    return true;
                default:
                    for (Node sub : n.subs) {
                        if (!zeroWidth(sub)) {
                            return false;
                        }
                    }
                    return true;
            }
        }

        /** Reads a quantifier as a REPEAT with no child yet, or returns null when there is none. */
        private Node quantifier() {
            int c = peek();
            Node repeat = new Node(REPEAT);
            if (c == '*') {
                i++;
                repeat.max = UNBOUNDED;
            } else if (c == '+') {
                i++;
                repeat.min = 1;
                repeat.max = UNBOUNDED;
            } else if (c == '?') {
                i++;
                repeat.max = 1;
            } else if (c == '{' && looksLikeQuantifier()) {
                i++;
                repeat.min = number();
                repeat.max = repeat.min;
                int minStart = numberStart;
                int minEnd = numberEnd;
                boolean ordered = true;
                if (eat(',')) {
                    if (peek() == '}') {
                        repeat.max = UNBOUNDED;
                    } else {
                        repeat.max = number();
                        ordered = !lessThan(minStart, minEnd);
                    }
                }
                if (!eat('}')) {
                    throw error("malformed quantifier");
                }
                if (!ordered) {
                    throw error("numbers out of order in quantifier");
                }
            } else {
                return null;
            }
            repeat.lazy = eat('?');
            return repeat;
        }

        /** Reads a group or a lookaround. */
        private Node group() {
            i++;
            if (!eat('?')) {
                return capture(null);
            }
            if (eat(':')) {
                Node group = new Node(GROUP);
                group.subs = new Node[] {disjunction()};
                close();
                return group;
            }
            int c = peek();
            if (c == 'i' || c == 'm' || c == 's' || c == '-') {
                return modifierGroup();
            }
            if (eat('=') || eat('!')) {
                Node look = new Node(LOOK);
                look.negative = p[i - 1] == '!';
                look.subs = new Node[] {disjunction()};
                close();
                return look;
            }
            if (eat('<')) {
                if (eat('=') || eat('!')) {
                    Node look = new Node(LOOK);
                    look.behind = true;
                    look.negative = p[i - 1] == '!';
                    look.subs = new Node[] {disjunction()};
                    close();
                    return look;
                }
                String name = groupName();
                // Two groups may share a name only when they are in separate alternatives of one disjunction, so
                // that no match can go through both.
                List<int[]> others = names.get(name);
                if (others != null) {
                    for (int[] other : others) {
                        if (!separateAlternatives(other)) {
                            throw error("duplicate group name");
                        }
                    }
                }
                return capture(name);
            }
            throw error("invalid group");
        }

        /**
         * Whether a named group (its number, then the alternatives that enclose it) and the current position lie in
         * different alternatives of some disjunction.
         */
        private boolean separateAlternatives(int[] other) {
            for (int k = 0; 2 * k + 2 < other.length && k < pathLength && other[2 * k + 1] == path[2 * k]; k++) {
                if (other[2 * k + 2] != path[2 * k + 1]) {
                    return true;
                }
            }
            return false;
        }

        /**
         * Reads a group that turns modifiers on or off for its body, such as {@code (?i:...)} or {@code (?s-i:...)}.
         * The position is after the "(?".
         */
        private Node modifierGroup() {
            boolean savedIgnoreCase = ignoreCase;
            boolean savedMultiline = multiline;
            boolean savedDotAll = dotAll;
            int seen = 0;
            boolean removing = false;
            while (!eat(':')) {
                int c = peek();
                i++;
                if (c == '-') {
                    if (removing) {
                        throw error("invalid group modifier");
                    }
                    removing = true;
                    continue;
                }
                int flag = c == 'i' ? 1 : c == 'm' ? 2 : c == 's' ? 4 : 0;
                if (flag == 0) {
                    throw error("invalid group modifier");
                }
                if ((seen & flag) != 0) {
                    throw error("repeated group modifier");
                }
                seen |= flag;
                if (flag == 1) {
                    ignoreCase = !removing;
                } else if (flag == 2) {
                    multiline = !removing;
                } else {
                    dotAll = !removing;
                }
            }
            if (seen == 0) {
                throw error("invalid group modifier");
            }
            Node group = new Node(GROUP);
            group.subs = new Node[] {disjunction()};
            close();
            ignoreCase = savedIgnoreCase;
            multiline = savedMultiline;
            dotAll = savedDotAll;
            return group;
        }

        private Node capture(String name) {
            Node group = new Node(GROUP);
            group.group = ++groups;
            if (name != null) {
                int[] named = new int[1 + 2 * pathLength];
                named[0] = group.group;
                System.arraycopy(path, 0, named, 1, 2 * pathLength);
                names.computeIfAbsent(name, k -> new ArrayList<>()).add(named);
            }
            group.subs = new Node[] {disjunction()};
            close();
            return group;
        }

        private void close() {
            if (!eat(')')) {
                throw error("unterminated group");
            }
        }

        /**
         * Reads a group name up to and including its '>'. A character of the name may be written as a Unicode
         * escape.
         */
        private String groupName() {
            StringBuilder name = new StringBuilder();
            while (!eat('>')) {
                if (!more()) {
                    throw error("invalid group name");
                }
                int c = p[i++];
                if (c == '\\') {
                    if (!eat('u')) {
                        throw error("invalid group name");
                    }
                    c = unicodeEscape(true, true);
                    if (c < 0) {
                        throw error("invalid group name");
                    }
                } else if (!unicode && c >= 0xD800 && c <= 0xDBFF && more() && p[i] >= 0xDC00 && p[i] <= 0xDFFF) {
                    // A name is read by character in both grammars.
                    c = Character.toCodePoint((char) c, (char) p[i++]);
                }
                if (name.length() == 0 ? !EcmaUnicode.isNameStart(c) : !EcmaUnicode.isNamePart(c)) {
                    throw error("invalid group name");
                }
                name.appendCodePoint(c);
            }
            if (name.length() == 0) {
                throw error("invalid group name");
            }
            return name.toString();
        }

        /** Reads an escape outside a class. */
        private Node atomEscape() {
            i++;
            if (!more()) {
                throw error("\\ at end of pattern");
            }
            int c = peek();
            if (c == 'b' || c == 'B') {
                i++;
                Node boundary = new Node(c == 'b' ? WORD_BOUNDARY : NOT_WORD_BOUNDARY);
                // With the u flag grammar a case-insensitive word boundary counts two more characters as word
                // characters.
                boundary.fold = unicode && ignoreCase;
                return boundary;
            }
            if (c == 'k') {
                i++;
                // Annex B reads \k as the letter k in a pattern with no named group.
                if (!unicode && !hasNames) {
                    return charNode('k');
                }
                if (!eat('<')) {
                    throw error("invalid named reference");
                }
                Node ref = new Node(BACKREF);
                ref.name = groupName();
                ref.fold = ignoreCase;
                namedRefs.add(ref);
                return ref;
            }
            if (c >= '1' && c <= '9') {
                int start = i;
                int n = number();
                if (n <= groupCount) {
                    Node ref = new Node(BACKREF);
                    ref.groups = new int[] {n};
                    ref.fold = ignoreCase;
                    return ref;
                }
                if (unicode) {
                    throw error("reference to a group that does not exist");
                }
                // Annex B reads it as an octal escape, or as the digit itself.
                i = start;
                return charNode(legacyOctal());
            }
            int[] set = classEscape(c);
            if (set != null) {
                Node node = setNode(folded(set));
                if (engineProperty != null && !ignoreCase) {
                    node.parts = new int[][] {engineProperty};
                    node.rest = NO_CHARACTERS;
                    node.partsNegated = enginePropertyNegated;
                }
                return node;
            }
            return charNode(characterEscape(false));
        }

        /**
         * Reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one
         * (8 or 9) stands for itself.
         */
        private int legacyOctal() {
            int v = 0;
            int k = 0;
            while (k < 3 && more() && peek() >= '0' && peek() <= '7' && v * 8 + (peek() - '0') <= 0377) {
                v = v * 8 + (peek() - '0');
                i++;
                k++;
            }
            if (k == 0) {
                return p[i++];
            }
            return v;
        }

        /**
         * Reads a class escape such as {@code \d} or {@code \p{...}} whose letter is {@code c}, as a set. Returns
         * null, reading nothing, when {@code c} does not start one.
         */
        private int[] classEscape(int c) {
            engineProperty = null;
            switch (c) {
                case 'd':
                    i++;
                    return DIGIT;
                case 'D':
                    i++;
                    return EcmaUnicode.complement(DIGIT);
                case 'w':
                    i++;
                    return unicode && ignoreCase ? FOLD_WORD : WORD;
                case 'W':
                    i++;
                    return EcmaUnicode.complement(unicode && ignoreCase ? FOLD_WORD : WORD);
                case 's':
                    i++;
                    return SPACE;
                case 'S':
                    i++;
                    return EcmaUnicode.complement(SPACE);
                case 'p':
                case 'P': {
                    // Without the u flag \p is the letter p.
                    if (!unicode) {
                        return null;
                    }
                    i++;
                    if (!eat('{')) {
                        throw error("invalid property name");
                    }
                    int start = i;
                    while (more() && peek() != '}') {
                        i++;
                    }
                    String expression = new String(p, start, i - start);
                    if (!eat('}')) {
                        throw error("invalid property name");
                    }
                    int[] set = EcmaUnicode.property(expression, 0, expression.length());
                    if (set == null) {
                        throw error("invalid property name");
                    }
                    if (EcmaUnicode.engineClass(set) != null) {
                        engineProperty = set;
                        enginePropertyNegated = c == 'P';
                    }
                    return c == 'P' ? complements.computeIfAbsent(set, EcmaUnicode::complement) : set;
                }
                default:
                    return null;
            }
        }

        private static int hexValue(int c) {
            if (c >= '0' && c <= '9') {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f') {
                return c - 'a' + 10;
            }
            if (c >= 'A' && c <= 'F') {
                return c - 'A' + 10;
            }
            return -1;
        }

        /** Reads exactly {@code digits} hexadecimal digits. Returns -1, reading nothing, when they are not there. */
        private int hex(int digits) {
            if (i + digits > p.length) {
                return -1;
            }
            int v = 0;
            for (int k = 0; k < digits; k++) {
                int d = hexValue(p[i + k]);
                if (d < 0) {
                    return -1;
                }
                v = v * 16 + d;
            }
            i += digits;
            return v;
        }

        /** Reads the escape after a backslash, in or out of a class, and returns the code point it names. */
        private int characterEscape(boolean inClass) {
            int c = p[i++];
            switch (c) {
                case 'f':
                    return '\f';
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'v':
                    return 0x0b;
                case 'c': {
                    int l = peek();
                    if ((l >= 'a' && l <= 'z') || (l >= 'A' && l <= 'Z')) {
                        i++;
                        return l % 32;
                    }
                    if (!unicode && inClass && (isDigit(l) || l == '_')) {
                        i++;
                        return l % 32;
                    }
                    if (unicode) {
                        throw error("invalid control escape");
                    }
                    // Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
                    i--;
                    return '\\';
                }
                case '0':
                    if (more() && isDigit(peek())) {
                        if (unicode) {
                            throw error("invalid decimal escape");
                        }
                        i--;
                        return legacyOctal();
                    }
                    return 0;
                case 'x': {
                    int h = hex(2);
                    if (h < 0) {
                        if (unicode) {
                            throw error("invalid \\x escape");
                        }
                        return 'x';
                    }
                    return h;
                }
                case 'u': {
                    int u = unicodeEscape(unicode, unicode);
                    if (u < 0) {
                        if (unicode) {
                            throw error("invalid \\u escape");
                        }
                        return 'u';
                    }
                    return u;
                }
                default:
                    break;
            }
            if (c >= '1' && c <= '9' && !unicode && inClass) {
                i--;
                return legacyOctal();
            }
            // Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B
            // allows any character, but for k in a pattern with a named group, where \k is only ever a reference
            // to one (so not in a class either).
            if (unicode && "^$\\.*+?()[]{}|/".indexOf(c) < 0 && !(inClass && c == '-')) {
                throw error("invalid escape");
            }
            if (c == 'k' && hasNames) {
                throw error("invalid escape");
            }
            return c;
        }

        /**
         * Reads what follows the u of a Unicode escape and returns the code point, or -1, reading nothing, when it
         * is not a well-formed escape. With {@code braces} it accepts the form with braces. With {@code pairs} a
         * surrogate pair written as two escapes is one code point. Without the u flag it is that in a group name
         * only, and two code units anywhere else.
         */
        private int unicodeEscape(boolean braces, boolean pairs) {
            int start = i;
            if (braces && eat('{')) {
                int digits = i;
                int v = 0;
                while (more() && hexValue(peek()) >= 0) {
                    if (v <= EcmaUnicode.MAX) {
                        v = v * 16 + hexValue(peek());
                    }
                    i++;
                }
                if (digits == i || !eat('}') || v > EcmaUnicode.MAX) {
                    i = start;
                    return -1;
                }
                return v;
            }
            int u = hex(4);
            if (u < 0) {
                return -1;
            }
            if (pairs && u >= 0xD800 && u <= 0xDBFF && i + 6 <= p.length && p[i] == '\\' && p[i + 1] == 'u') {
                int save = i;
                i += 2;
                int low = hex(4);
                if (low >= 0xDC00 && low <= 0xDFFF) {
                    return Character.toCodePoint((char) u, (char) low);
                }
                i = save;
            }
            return u;
        }

        private void addPair(int lo, int hi) {
            if (pairCount + 2 > pairs.length) {
                pairs = Arrays.copyOf(pairs, pairs.length * 2);
            }
            pairs[pairCount++] = lo;
            pairs[pairCount++] = hi;
        }

        private void addClassAtom(int c, int[] set) {
            if (set == null) {
                addPair(c, c);
                return;
            }
            for (int k = 0; k < set.length; k += 2) {
                addPair(set[k], set[k + 1]);
            }
        }

        /**
         * Reads a character class from its '[' to its ']'. The properties it names that java.util.regex has a class
         * for are kept apart from its other members, so that the class can be written with them.
         */
        private Node characterClass() {
            i++;
            boolean negated = eat('^');
            pairCount = 0;
            List<int[]> parts = null;
            while (true) {
                if (!more()) {
                    throw error("unterminated character class");
                }
                if (eat(']')) {
                    break;
                }
                int first = classAtom();
                int[] firstSet = atomSet;
                boolean firstIsPart = firstSet != null && engineProperty != null && !enginePropertyNegated;
                if (peek() == '-' && i + 1 < p.length && p[i + 1] != ']') {
                    i++;
                    int second = classAtom();
                    int[] secondSet = atomSet;
                    if (firstSet != null || secondSet != null) {
                        if (unicode) {
                            throw error("invalid character class range");
                        }
                        // Annex B reads the '-' as itself when a class escape is at either end.
                        addClassAtom(first, firstSet);
                        addPair('-', '-');
                        addClassAtom(second, secondSet);
                        continue;
                    }
                    if (second < first) {
                        throw error("range out of order in character class");
                    }
                    addPair(first, second);
                    continue;
                }
                if (firstIsPart && !ignoreCase) {
                    if (parts == null) {
                        parts = new ArrayList<>();
                    }
                    parts.add(firstSet);
                    continue;
                }
                addClassAtom(first, firstSet);
            }
            int[] rest = EcmaUnicode.setOf(pairs, pairCount);
            int[] all = rest;
            if (parts != null) {
                for (int[] part : parts) {
                    all = EcmaUnicode.union(all, part);
                }
            }
            // Inside a case-insensitive group a character matches the class when it is equivalent to a member, and
            // a negated class excludes exactly those characters.
            int[] set = folded(all);
            Node node = setNode(negated ? EcmaUnicode.complement(set) : set);
            if (parts != null) {
                node.parts = parts.toArray(new int[0][]);
                node.rest = rest;
                node.partsNegated = negated;
            }
            return node;
        }

        /** Reads one member of a class. Returns its code point, or sets {@link #atomSet} for a class escape. */
        private int classAtom() {
            atomSet = null;
            int c = p[i++];
            if (c != '\\') {
                return c;
            }
            if (!more()) {
                throw error("\\ at end of pattern");
            }
            int e = peek();
            if (e == 'b') {
                i++;
                return '\b';
            }
            atomSet = classEscape(e);
            if (atomSet != null) {
                return 0;
            }
            return characterEscape(true);
        }
    }

    /** Writes the tree of a pattern as a {@code java.util.regex} pattern with the same meaning, or refuses it. */
    private static final class Emitter {
        private static final String ANY = "[\\x{0}-\\x{10FFFF}]";
        private static final String NOT_LINE_TERMINATOR = "[^\\x{A}\\x{D}\\x{2028}\\x{2029}]";
        private static final String LINE_TERMINATOR = "[\\x{A}\\x{D}\\x{2028}\\x{2029}]";
        private static final String WORD_CLASS = "[a-zA-Z0-9_]";
        private static final String FOLD_WORD_CLASS = "[a-zA-Z0-9_\\x{17F}\\x{212A}]";
        /** A lookbehind that can be longer than this is written in the form that searches for its start. */
        private static final long MAX_DIRECT_LOOKBEHIND = 1 << 16;
        /** A length with no bound. */
        private static final long INFINITE = Long.MAX_VALUE;
        /**
         * The most copies of bodies one translation holds for iterations written apart, and the most text they add.
         * {@code java.util.regex} matches by recursion, some levels for each copy, and a thousand copies of a small
         * body are more than a stack of 512 KB holds.
         */
        static final int MAX_UNROLLED_COPIES = 200;
        static final int MAX_UNROLLED_LENGTH = MAX_TRANSLATION_LENGTH;

        private final boolean unicode;
        private Node root;
        private final Node[] groupNodes;
        /** For each group, whether some backreference reads it, and whether one reads it unsure that it is set. */
        private final boolean[] referenced;
        private final boolean[] needsMarker;
        /** For each group: whether a lookbehind or any lookaround encloses it, and the repetitions that enclose it. */
        private final boolean[] inLookbehind;
        private final boolean[] inLookaround;
        /** For each group, whether a lookahead around it holds a repetition that takes empty iterations. */
        private final boolean[] firstMatchDiffers;
        private final Node[][] repeatsAround;
        /** For each group, the number of its group in the translation, and of the group that marks it took part. */
        private final int[] javaGroup;
        private final int[] javaMarker;
        private final StringBuilder out = new StringBuilder();
        private int javaGroups;
        private int unrolledCopies;
        private int unrolledLength;
        private boolean hasLookbehind;
        private boolean searchingLookbehind;

        Emitter(Parser parser, Node root) {
            this.unicode = parser.unicode;
            this.root = root;
            int n = parser.groupCount + 1;
            groupNodes = new Node[n];
            referenced = new boolean[n];
            needsMarker = new boolean[n];
            inLookbehind = new boolean[n];
            inLookaround = new boolean[n];
            firstMatchDiffers = new boolean[n];
            repeatsAround = new Node[n][];
            javaGroup = new int[n];
            javaMarker = new int[n];
        }

        /** The translation, compiled once to be sure {@code java.util.regex} takes it. */
        String translate() {
            flow(root, new BitSet(), new BitSet(), false);
            try {
                survey(root, false, false, false, new ArrayList<>());
            } catch (Unsupported e) {
                // A backreference the markers cannot serve. The pattern is written by paths.
                return translateByPaths();
            }
            markPossessive();
            String translated = Ticks.insert(write());
            String error = compileError(translated);
            if (error != null && hasLookbehind) {
                // java.util.regex cannot bound some lookbehinds whose length is bounded (a repeated group with
                // alternatives, for one). The form that searches for the start needs no bound.
                searchingLookbehind = true;
                translated = Ticks.insert(write());
                error = compileError(translated);
            }
            return compiled(translated, error);
        }

        // Repetitions the match never needs to go back into.

        /** One character of a set, a number of times: a term of a body that {@link #markPossessive} can read. */
        private static final class Element {
            final int[] set;
            final int min;
            final int max;

            Element(int[] set, int min, int max) {
                this.set = set;
                this.min = min;
                this.max = max;
            }
        }

        /** A node without the groups around it that nothing reads. */
        private Node bare(Node n) {
            while (n.kind == GROUP && (n.group == 0 || !referenced[n.group])) {
                n = n.subs[0];
            }
            return n;
        }

        /** The element a node is, or null when it is anything else. */
        private Element element(Node n) {
            n = bare(n);
            int min = 1;
            int max = 1;
            if (n.kind == REPEAT) {
                if (n.lazy || n.lastApart || n.max == 0) {
                    return null;
                }
                min = n.min;
                max = n.max;
                n = bare(n.subs[0]);
            }
            if (n.kind == CHAR) {
                return new Element(new int[] {n.codePoint, n.codePoint}, min, max);
            }
            return n.kind == SET && n.set.length != 0 ? new Element(n.set, min, max) : null;
        }

        /** The elements a node is a sequence of, or null when it is anything else or can match the empty string. */
        private Element[] elements(Node n) {
            n = bare(n);
            Node[] terms = n.kind == CAT ? n.subs : new Node[] {n};
            Element[] all = new Element[terms.length];
            boolean takes = false;
            for (int k = 0; k < terms.length; k++) {
                all[k] = element(terms[k]);
                if (all[k] == null) {
                    return null;
                }
                takes |= all[k].min > 0;
            }
            return takes ? all : null;
        }

        /** The characters a sequence of elements can start with, from {@code from} on. */
        private static int[] firstOf(Element[] sequence, int from) {
            int[] first = NO_CHARACTERS;
            for (int k = from; k < sequence.length; k++) {
                first = EcmaUnicode.union(first, sequence[k].set);
                if (sequence[k].min > 0) {
                    break;
                }
            }
            return first;
        }

        /**
         * Marks the repetitions of the pattern that are written possessive, as {@code (?:X)*+}: ones the match
         * never needs to go back into.
         *
         * <p>{@code java.util.regex} runs a repetition of a group by recursion, a level or more for each iteration,
         * unless the iterations are all of one length or the repetition is possessive. So {@code ^(?:[^"\\]|\\.)*$}
         * runs out of stack on a string of a few thousand characters, where the possessive form runs in a loop.
         * The two forms give the same answer when the match has nothing to gain by giving iterations back, which
         * is so for a repetition that is a term of the whole pattern when
         *
         * <ul>
         *   <li>its body is a sequence of characters and classes, each repeated or not, or alternatives that are
         *       such sequences, and matches at least one character;
         *   <li>there is one way to match it: the alternatives start with different characters, and a character or
         *       class that is repeated a number of times or another shares no character with what can follow it,
         *       which at the end of an alternative is the start of another iteration or of what follows the
         *       repetition;
         *   <li>what follows the repetition is nothing, or characters and classes that share no character with the
         *       start of the body, up to one that must be there or to the end of the text.
         * </ul>
         *
         * <p>Then the iterations the repetition takes from a place are the only ones it can, and each place it
         * could stop at earlier is followed by a character an iteration starts with, where what follows the
         * repetition cannot match.
         */
        private void markPossessive() {
            Node[] terms = root.kind == CAT ? root.subs : new Node[] {root};
            for (int t = 0; t < terms.length; t++) {
                Node n = bare(terms[t]);
                if (n.kind != REPEAT || n.lazy || n.lastApart || (n.max != UNBOUNDED && n.max < 2)) {
                    continue;
                }
                Node body = bare(n.subs[0]);
                if (body.kind != ALT && body.kind != CAT) {
                    // One character or class: java.util.regex repeats it in a loop as it is.
                    continue;
                }
                Node[] alternatives = body.kind == ALT ? body.subs : new Node[] {body};
                Element[][] sequences = new Element[alternatives.length][];
                int[] start = NO_CHARACTERS;
                boolean one = true;
                for (int k = 0; k < alternatives.length && one; k++) {
                    sequences[k] = elements(alternatives[k]);
                    one = sequences[k] != null;
                    if (one) {
                        int[] first = firstOf(sequences[k], 0);
                        one = !EcmaUnicode.intersects(start, first);
                        start = EcmaUnicode.union(start, first);
                    }
                }
                // What follows: characters that an iteration does not start with, to one that must be there.
                int[] after = NO_CHARACTERS;
                for (int k = t + 1; k < terms.length && one; k++) {
                    if (bare(terms[k]).kind == EOL) {
                        break;
                    }
                    Element e = element(terms[k]);
                    one = e != null && !EcmaUnicode.intersects(start, e.set);
                    if (one) {
                        after = EcmaUnicode.union(after, e.set);
                        if (e.min > 0) {
                            break;
                        }
                    }
                }
                int[] next = EcmaUnicode.union(start, after);
                for (int k = 0; k < sequences.length && one; k++) {
                    Element[] sequence = sequences[k];
                    for (int j = 0; j < sequence.length && one; j++) {
                        if (sequence[j].min != sequence[j].max) {
                            int[] follows = firstOf(sequence, j + 1);
                            boolean toEnd = true;
                            for (int m = j + 1; m < sequence.length; m++) {
                                toEnd &= sequence[m].min == 0;
                            }
                            one = !EcmaUnicode.intersects(sequence[j].set, toEnd ? EcmaUnicode.union(follows, next) : follows);
                        }
                    }
                }
                n.possessive = one;
            }
        }

        /**
         * What {@code java.util.regex} says against a translation, or null when it compiles it. A translation whose
         * compilation overflows the stack is refused as too large: the compiler of {@code java.util.regex} recurses
         * along the pattern, some thousands of groups deep on a stack of 1 MB.
         */
        private static String compileError(String translated) {
            try {
                Pattern.compile(translated);
                return null;
            } catch (PatternSyntaxException e) {
                // The compiler reports the overflow of its own recursion this way.
                if (e.getDescription().startsWith("Stack overflow")) {
                    throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_STACK);
                }
                return e.getDescription() + " near index " + e.getIndex();
            } catch (StackOverflowError e) {
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_STACK);
            }
        }

        /**
         * The translation, when {@code java.util.regex} compiled it. One it rejects is a fault of this translator
         * and not a reason to refuse the pattern, so it is not reported as one.
         */
        private static String compiled(String translated, String error) {
            if (translated.length() > MAX_TRANSLATION_LENGTH) {
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_TRANSLATION);
            }
            if (error != null) {
                throw new IllegalStateException("java.util.regex does not compile the translation of a valid ECMA-262 "
                        + "pattern (" + error + "). This is a fault of the library. The translation is " + translated);
            }
            return translated;
        }

        private String write() {
            out.setLength(0);
            javaGroups = 0;
            unrolledCopies = 0;
            unrolledLength = 0;
            hasLookbehind = false;
            Arrays.fill(javaGroup, 0);
            Arrays.fill(javaMarker, 0);
            node(root);
            if (hasLookbehind) {
                // java.util.regex steps back over the text of a lookbehind by UTF-16 code unit unless the pattern
                // text after the lookbehind holds a supplementary character. Then it steps by code point, which is
                // what the lengths it worked out count. The branch that holds the character here is never taken: the
                // empty branch before it always matches, and nothing follows that could fail.
                out.insert(0, "(?:").append(")(?:|\uD800\uDC00)");
            }
            return out.toString();
        }

        // What a backreference can rely on.

        /**
         * Works out, for every backreference, whether each group it names has taken part where the backreference
         * stands: on every path (DEFINITE), on none (NEVER) or on some (MAYBE). The walk follows the order in which
         * ECMA-262 evaluates the pattern, which is backwards inside a lookbehind. {@code definite} and
         * {@code possible} hold the groups set on every path and on some path to the node, and are updated to hold
         * the same for the paths that leave it.
         *
         * <p>ECMA-262 unsets the groups of a quantified body at the start of each of its iterations, and unsets the
         * groups of a negative lookaround when the lookaround is done.
         */
        private void flow(Node n, BitSet definite, BitSet possible, boolean backward) {
            switch (n.kind) {
                case CAT:
                    for (int k = 0; k < n.subs.length; k++) {
                        flow(n.subs[backward ? n.subs.length - 1 - k : k], definite, possible, backward);
                    }
                    break;
                case ALT: {
                    BitSet allDefinite = null;
                    BitSet anyPossible = new BitSet();
                    for (Node sub : n.subs) {
                        BitSet d = (BitSet) definite.clone();
                        BitSet p = (BitSet) possible.clone();
                        flow(sub, d, p, backward);
                        if (allDefinite == null) {
                            allDefinite = d;
                        } else {
                            allDefinite.and(d);
                        }
                        anyPossible.or(p);
                    }
                    definite.clear();
                    definite.or(allDefinite);
                    possible.clear();
                    possible.or(anyPossible);
                    break;
                }
                case GROUP:
                    flow(n.subs[0], definite, possible, backward);
                    if (n.group != 0) {
                        groupNodes[n.group] = n;
                        definite.set(n.group);
                        possible.set(n.group);
                    }
                    break;
                case REPEAT:
                    definite.clear(n.firstGroup + 1, n.lastGroup + 1);
                    possible.clear(n.firstGroup + 1, n.lastGroup + 1);
                    if (n.max == 0) {
                        // The body never runs, so nothing it sets is set after it.
                        flow(n.subs[0], (BitSet) definite.clone(), (BitSet) possible.clone(), backward);
                    } else if (n.min == 0) {
                        flow(n.subs[0], (BitSet) definite.clone(), possible, backward);
                    } else {
                        flow(n.subs[0], definite, possible, backward);
                    }
                    break;
                case LOOK:
                    if (n.negative) {
                        flow(n.subs[0], (BitSet) definite.clone(), (BitSet) possible.clone(), n.behind);
                        definite.clear(n.firstGroup + 1, n.lastGroup + 1);
                        possible.clear(n.firstGroup + 1, n.lastGroup + 1);
                    } else {
                        flow(n.subs[0], definite, possible, n.behind);
                    }
                    break;
                case BACKREF:
                    n.statuses = new int[n.groups.length];
                    n.ignoreAsciiCase = new boolean[n.groups.length];
                    for (int k = 0; k < n.groups.length; k++) {
                        int g = n.groups[k];
                        n.statuses[k] = definite.get(g) ? DEFINITE : possible.get(g) ? MAYBE : NEVER;
                    }
                    break;
                default:
                    break;
            }
        }

        /**
         * Notes what encloses each group, then decides for each backreference whether {@code java.util.regex} gives
         * it the meaning ECMA-262 does in the form with markers, and throws when that cannot be shown.
         */
        private void survey(Node n, boolean lookbehind, boolean lookaround, boolean differs, List<Node> repeats) {
            switch (n.kind) {
                case GROUP:
                    if (n.group != 0) {
                        inLookbehind[n.group] = lookbehind;
                        inLookaround[n.group] = lookaround;
                        firstMatchDiffers[n.group] = differs;
                        repeatsAround[n.group] = repeats.toArray(NO_NODES);
                    }
                    survey(n.subs[0], lookbehind, lookaround, differs, repeats);
                    break;
                case REPEAT:
                    // A repetition of at most once has no earlier iteration. An optional part is written with "?",
                    // which java.util.regex goes back into as it does into an alternative. Any other count makes
                    // it a repetition there, with what follows from that (see decide).
                    if (n.max == UNBOUNDED || n.max > 1 || n.min == 1) {
                        repeats.add(n);
                        survey(n.subs[0], lookbehind, lookaround, differs, repeats);
                        repeats.remove(repeats.size() - 1);
                    } else {
                        survey(n.subs[0], lookbehind, lookaround, differs, repeats);
                    }
                    break;
                case LOOK:
                    survey(n.subs[0], lookbehind || n.behind, true, differs || (!n.behind && holdsEmptyIterations(n)),
                            repeats);
                    break;
                case BACKREF:
                    decide(n, lookbehind, repeats);
                    break;
                default:
                    for (Node sub : n.subs) {
                        survey(sub, lookbehind, lookaround, differs, repeats);
                    }
                    break;
            }
        }

        /**
         * Decides whether one backreference can be written in the form with markers, and throws when it cannot (the
         * pattern is then written by paths). Outside a lookbehind ECMA-262 evaluates a pattern from left to right, so
         * a group that can have taken part comes before the backreference, and what encloses it is known.
         *
         * <p>A group that has taken part on every path to the backreference was set on the path
         * {@code java.util.regex} is on, so its text is the one ECMA-262 means, unless a repetition holds the group
         * and not the backreference. Then the two engines can differ on which iteration the group is of: ECMA-262
         * refuses an empty iteration and {@code java.util.regex} takes one, and {@code java.util.regex} keeps the
         * groups of an iteration it has given back when the body has one way to match.
         *
         * <p>A group that may have taken part needs a test, and the test reads what {@code java.util.regex} kept. It
         * is sound only where that engine forgets a group when ECMA-262 does, which is on backtracking out of the
         * group. It is not sound for a group in a lookaround, which {@code java.util.regex} does not undo when the
         * match fails later. Nor is it sound for a group in a repetition, as ECMA-262 unsets the group when an
         * iteration starts and {@code java.util.regex} keeps it. A repetition that ends before the backreference is
         * therefore written with its last iteration apart (see {@link #repeat}), so that the group is one only the
         * last iteration sets. A repetition that holds the backreference too cannot be.
         */
        private void decide(Node n, boolean lookbehind, List<Node> repeats) {
            for (int k = 0; k < n.groups.length; k++) {
                if (n.statuses[k] == DEFINITE) {
                    // At most one of the groups of a name has taken part, so the others have not.
                    Arrays.fill(n.statuses, NEVER);
                    n.statuses[k] = DEFINITE;
                    break;
                }
            }
            for (int k = 0; k < n.groups.length; k++) {
                int g = n.groups[k];
                if (n.statuses[k] == NEVER) {
                    continue;
                }
                if (lookbehind) {
                    throw new Unsupported(NOT_BY_MARKERS);
                }
                if (inLookbehind[g]) {
                    throw new Unsupported(NOT_BY_MARKERS);
                }
                if (firstMatchDiffers[g]) {
                    // The lookahead takes its first match, and java.util.regex tries the ways of a repetition in
                    // it that can take an empty iteration in another order (see takesEmptyIterations).
                    throw new Unsupported(NOT_BY_MARKERS);
                }
                if (n.statuses[k] == DEFINITE) {
                    for (Node repeat : repeatsAround[g]) {
                        // java.util.regex runs a repeated group whose body has one way to match (no alternative,
                        // no count that varies) as a unit, and when it gives back an iteration it does not restore
                        // the groups inside the body. So a group of a repetition is not read from outside it.
                        if (!repeats.contains(repeat)) {
                            throw new Unsupported(NOT_BY_MARKERS);
                        }
                        if (inLookaround[g] && repeats.contains(repeat)) {
                            throw new Unsupported(NOT_BY_MARKERS);
                        }
                    }
                } else {
                    if (inLookaround[g]) {
                        throw new Unsupported(NOT_BY_MARKERS);
                    }
                    for (Node repeat : repeatsAround[g]) {
                        if (repeats.contains(repeat)) {
                            throw new Unsupported(NOT_BY_MARKERS);
                        }
                        if (minLength(repeat.subs[0]) == 0) {
                            throw new Unsupported(NOT_BY_MARKERS);
                        }
                        repeat.lastApart = true;
                    }
                    needsMarker[g] = true;
                }
                referenced[g] = true;
                n.ignoreAsciiCase[k] = n.fold && comparesIgnoringAsciiCase(g);
            }
        }

        /**
         * Decides how a case-insensitive backreference to a group is written, from the characters the group can
         * capture. Returns false when none of them has a case variant, so that the backreference compares exactly.
         * Returns true when the ASCII case-insensitive comparison of {@code java.util.regex} is the comparison
         * ECMA-262 makes for every one of them: the only characters with a variant are ASCII letters whose only
         * variant is the other ASCII case. With the u flag grammar that leaves out k and s, which U+212A and U+017F
         * fold to. For any other group it throws, and the comparison is written out (see {@link #folded}), as
         * {@code java.util.regex} has no comparison that is the Canonicalize of ECMA-262. So too for a group that
         * can hold a character beyond the Basic Multilingual Plane, as the comparison of Java 17 miscounts them.
         */
        private boolean comparesIgnoringAsciiCase(int g) {
            int[] captured = capturedSet(groupNodes[g]);
            int[] cased = EcmaUnicode.cased(unicode);
            if (!EcmaUnicode.intersects(captured, cased)) {
                return false;
            }
            int[] variants = EcmaUnicode.foldClosure(intersection(captured, cased), unicode);
            if (EcmaUnicode.intersects(variants, NOT_ASCII_LETTER) || EcmaUnicode.intersects(captured, SUPPLEMENTARY)) {
                throw new Unsupported(NOT_BY_MARKERS);
            }
            return true;
        }

        private static int[] intersection(int[] a, int[] b) {
            return EcmaUnicode.complement(EcmaUnicode.union(EcmaUnicode.complement(a), EcmaUnicode.complement(b)));
        }

        /** Every character the text a node matches can hold. */
        private int[] capturedSet(Node n) {
            switch (n.kind) {
                case CHAR:
                    return new int[] {n.codePoint, n.codePoint};
                case SET:
                    return n.set;
                case LOOK:
                    return NO_CHARACTERS;
                case BACKREF: {
                    int[] set = NO_CHARACTERS;
                    for (int k = 0; k < n.groups.length; k++) {
                        if (n.statuses[k] != NEVER) {
                            int[] target = capturedSet(groupNodes[n.groups[k]]);
                            set = EcmaUnicode.union(set, n.fold ? EcmaUnicode.foldClosure(target, unicode) : target);
                        }
                    }
                    return set;
                }
                default: {
                    int[] set = NO_CHARACTERS;
                    for (Node sub : n.subs) {
                        set = EcmaUnicode.union(set, capturedSet(sub));
                    }
                    return set;
                }
            }
        }

        // Lengths, in characters.

        private long minLength(Node n) {
            if (n.minLength < 0) {
                measure(n);
            }
            return n.minLength;
        }

        private long maxLength(Node n) {
            if (n.minLength < 0) {
                measure(n);
            }
            return n.maxLength;
        }

        private static long plus(long a, long b) {
            return a == INFINITE || b == INFINITE ? INFINITE : a + b;
        }

        /** A length some number of times over, where the lengths that are not INFINITE are below 2^47. */
        private static long times(long length, int count) {
            if (length == 0 || count == 0) {
                return 0;
            }
            if (length == INFINITE || count == UNBOUNDED || length > (1L << 47) / count) {
                return INFINITE;
            }
            return length * count;
        }

        private void measure(Node n) {
            long min = 0;
            long max = 0;
            switch (n.kind) {
                case CHAR:
                case SET:
                    min = 1;
                    max = 1;
                    break;
                case CAT:
                    for (Node sub : n.subs) {
                        min = plus(min, minLength(sub));
                        max = plus(max, maxLength(sub));
                    }
                    break;
                case ALT:
                    min = INFINITE;
                    for (Node sub : n.subs) {
                        min = Math.min(min, minLength(sub));
                        max = Math.max(max, maxLength(sub));
                    }
                    break;
                case GROUP:
                    min = minLength(n.subs[0]);
                    max = maxLength(n.subs[0]);
                    break;
                case REPEAT:
                    min = times(minLength(n.subs[0]), n.min);
                    max = times(maxLength(n.subs[0]), n.max);
                    break;
                case ITERATION:
                    min = Math.max(minLength(n.subs[0]), 1);
                    max = Math.max(maxLength(n.subs[0]), 1);
                    break;
                case BACKREF:
                    if (!precise || n.measuring) {
                        max = INFINITE;
                        break;
                    }
                    // The text of a backreference is the text of one of its groups, or nothing.
                    n.measuring = true;
                    for (int k = 0; k < n.groups.length; k++) {
                        if (n.statuses[k] != NEVER) {
                            max = Math.max(max, maxLength(groupNodes[n.groups[k]].subs[0]));
                        }
                    }
                    n.measuring = false;
                    break;
                default:
                    break;
            }
            // Past 2^47 a length is as good as unbounded, and keeping below it keeps the sums exact.
            n.minLength = min > (1L << 47) ? INFINITE : min;
            n.maxLength = max > (1L << 47) ? INFINITE : max;
        }

        // Writing.

        private void node(Node n) {
            if (out.length() > MAX_TRANSLATION_LENGTH) {
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_TRANSLATION);
            }
            switch (n.kind) {
                case EMPTY:
                    break;
                case CHAR:
                case SET:
                    if (!barrier.isEmpty()) {
                        // In a group, so that a quantifier after the character repeats the test with it.
                        out.append("(?:").append(barrier);
                    }
                    if (n.kind == CHAR) {
                        literal(n.codePoint);
                    } else if (n.parts != null) {
                        engineSet(n);
                    } else {
                        set(n.set);
                    }
                    if (!barrier.isEmpty()) {
                        out.append(')');
                    }
                    break;
                case CAT:
                    for (Node sub : n.subs) {
                        node(sub);
                    }
                    break;
                case ALT:
                    for (int k = 0; k < n.subs.length; k++) {
                        if (k > 0) {
                            out.append('|');
                        }
                        node(n.subs[k]);
                    }
                    break;
                case GROUP:
                    group(n);
                    break;
                case REPEAT:
                    repeat(n);
                    break;
                case BOL:
                    out.append('^');
                    break;
                case EOL:
                    out.append("\\z");
                    break;
                case LINE_START:
                    out.append("(?:^|(?<=").append(LINE_TERMINATOR).append("))");
                    break;
                case LINE_END:
                    out.append("(?:\\z|(?=").append(LINE_TERMINATOR).append("))");
                    break;
                case WORD_BOUNDARY: {
                    String w = n.fold ? FOLD_WORD_CLASS : WORD_CLASS;
                    out.append("(?:(?<=").append(w).append(")(?!").append(w).append(")|(?<!").append(w).append(")(?=")
                            .append(w).append("))");
                    break;
                }
                case NOT_WORD_BOUNDARY: {
                    String w = n.fold ? FOLD_WORD_CLASS : WORD_CLASS;
                    out.append("(?:(?<=").append(w).append(")(?=").append(w).append(")|(?<!").append(w).append(")(?!")
                            .append(w).append("))");
                    break;
                }
                case LOOK: {
                    // A lookaround inside a lookbehind is not held to the place the lookbehind stands at.
                    String wasBarrier = barrier;
                    barrier = "";
                    if (n.behind) {
                        lookbehind(n);
                    } else {
                        out.append(n.negative ? "(?!" : "(?=");
                        node(n.subs[0]);
                        out.append(')');
                    }
                    barrier = wasBarrier;
                    break;
                }
                case BACKREF:
                    backreference(n);
                    break;
                default:
                    throw new IllegalStateException();
            }
        }

        /** A lookbehind written as a search reaches back over the characters of a set of up to this many ranges. */
        private static final int MAX_REACH_RANGES = 8;

        /**
         * The characters a node can take, outside the lookarounds in it, or null when that is any character (a
         * backreference takes what its group holds).
         */
        private static int[] taken(Node n) {
            switch (n.kind) {
                case CHAR:
                    return new int[] {n.codePoint, n.codePoint};
                case SET:
                    return n.set;
                case LOOK:
                    return NO_CHARACTERS;
                case BACKREF:
                    return null;
                default: {
                    int[] all = NO_CHARACTERS;
                    for (Node sub : n.subs) {
                        int[] one = taken(sub);
                        if (one == null) {
                            return null;
                        }
                        all = EcmaUnicode.union(all, one);
                    }
                    return all;
                }
            }
        }

        /**
         * Inside a lookbehind that is written as a search (see {@link #lookbehind}): the test, written before each
         * character the body takes, that the place is not yet the one the lookbehind stands at. Empty elsewhere.
         */
        private String barrier = "";

        private void literal(int c) {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) {
                out.append((char) c);
            } else {
                EcmaUnicode.appendCodePoint(out, c);
            }
        }

        private void set(int[] set) {
            if (set.length == 0) {
                // No character: an assertion that always fails.
                out.append("(?!)");
            } else if (set.length == 2 && set[0] == 0 && set[1] == EcmaUnicode.MAX) {
                out.append(ANY);
            } else if (Arrays.equals(set, DOT)) {
                out.append(NOT_LINE_TERMINATOR);
            } else {
                EcmaUnicode.appendClass(out, set);
            }
        }

        /**
         * Writes a set that names properties java.util.regex has classes for, with those classes. Each class comes
         * with the code points to take from it, and the code points to add to it join the other members of the set
         * (see {@link EcmaUnicode#engineClass}), so what is written matches exactly the set whatever the JDK. The
         * classes come first, as they are the cheap test.
         */
        private void engineSet(Node n) {
            int[] others = n.rest;
            int start = out.length();
            int nominal = REFERENCE_PADDING;
            out.append(n.partsNegated ? "[^" : "[");
            for (int[] part : n.parts) {
                EcmaUnicode.EngineClass engine = EcmaUnicode.engineClass(part);
                nominal += EcmaUnicode.referenceLength(engine.name);
                if (engine.removed.length == 0) {
                    out.append(engine.name);
                } else {
                    out.append('[').append(engine.name).append("&&[^");
                    EcmaUnicode.appendSearchTree(out, engine.removed);
                    out.append("]]");
                }
                others = EcmaUnicode.union(others, engine.added);
            }
            EcmaUnicode.appendSearchTree(out, others);
            if (reference) {
                // What corrects the class of the engine depends on the JDK, and so does its length. The translation
                // of reference has every such set at one length, whatever the JDK: the length it has on JDK 17,
                // which needs the most corrections, made up with members that add nothing to the set.
                paddedSets = true;
                StringBuilder rest = new StringBuilder();
                EcmaUnicode.appendSearchTree(rest, n.rest);
                int pad = nominal + rest.length() - (out.length() + 1 - start);
                if (pad >= REFERENCE_PADDING_LEAST) {
                    out.append("&&[\\x{0}-\\x{10ffff}");
                    for (int k = REFERENCE_PADDING_LEAST; k < pad; k++) {
                        out.append('a');
                    }
                    out.append(']');
                }
            }
            out.append(']');
        }

        /** The least a set can be padded by, which is the text that holds the padding, and what every set is given. */
        private static final int REFERENCE_PADDING_LEAST = 20;
        private static final int REFERENCE_PADDING = 24;

        private void group(Node n) {
            if (n.group == 0 || !referenced[n.group]) {
                out.append("(?:");
                node(n.subs[0]);
                out.append(')');
                return;
            }
            javaGroup[n.group] = ++javaGroups;
            if (!needsMarker[n.group]) {
                out.append('(');
                node(n.subs[0]);
                out.append(')');
                return;
            }
            // An empty group at the end marks that the group took part.
            out.append("((?:");
            node(n.subs[0]);
            javaMarker[n.group] = ++javaGroups;
            out.append(")())");
        }

        /**
         * Writes a repetition.
         *
         * <p>When a backreference after the repetition reads a group of its body that an iteration can skip, the body
         * X is written twice, as {@code (?:(?:X){m-1,n-1}X)}, and made optional when the repetition can run no times.
         * The backreference reads the groups of the second X, which only the last iteration sets, as in ECMA-262,
         * where each iteration starts by unsetting the groups of the body. The first X has groups of its own for the
         * backreferences inside it. The two forms match the same texts, as the body cannot match the empty string
         * (such a repetition is written by paths), so every iteration is one ECMA-262 takes.
         *
         * <p>A repetition X{m,n} that must run twice or more over a body that can match the empty string is written
         * as m copies of X and then {@code (?:X){0,n-m}}, when {@link #leavesEarly} says the two engines differ on
         * it. Each copy has groups of its own, and a backreference in a copy reads the groups of that copy, as
         * ECMA-262 unsets the groups of the body when an iteration starts. A pattern with a backreference after the
         * repetition to a group of such a body is written by paths, so none reads the wrong copy.
         */
        private void repeat(Node n) {
            Node atom = n.subs[0];
            if (!n.lastApart) {
                if (leavesEarly(n)) {
                    unrolled(n);
                    return;
                }
                atom(atom);
                quantifier(n.min, n.max, n.lazy);
                if (n.possessive) {
                    out.append('+');
                }
                return;
            }
            out.append("(?:");
            atom(atom);
            quantifier(Math.max(n.min - 1, 0), n.max == UNBOUNDED ? UNBOUNDED : n.max - 1, n.lazy);
            node(atom);
            out.append(')');
            if (n.min == 0) {
                out.append(n.lazy ? "??" : "?");
            }
        }

        /**
         * Whether {@code java.util.regex} can leave a repetition before the iterations it must run are done, where
         * ECMA-262 runs them all.
         *
         * <p>ECMA-262 takes an iteration that matched nothing while iterations are still due, and goes on to the
         * next (RepeatMatcher refuses an empty iteration only once the minimum is zero). {@code java.util.regex}
         * goes on to what follows the repetition after any iteration that matched nothing, as if the rest were
         * done. So in {@code (^|x){2}a} on "xa", ECMA-262 takes "^" and then "x", and {@code java.util.regex} takes
         * "^" and looks for "a" at once.
         *
         * <p>The answers differ only when a path of ECMA-262 has an empty iteration followed by one that is not
         * empty, and {@code java.util.regex} has no empty iteration to end on where that path leaves the
         * repetition. That needs two iterations that are due, so a minimum of two. It needs a body that matches
         * both the empty string and a longer one. And it needs a body that does not match the empty string
         * everywhere, which is one whose every empty match passes an assertion or a backreference. With a minimum
         * of one or none the paths of the two engines reach the same places: an empty iteration does not move, and
         * the one the paths can differ by is at the end.
         *
         * <p>And it needs a body with more than one way to match where it stands. A body with one way (see
         * {@link #oneWayToMatch}) that matched nothing in one iteration matches nothing in the next, which starts at
         * the same place with the same groups, and so in all that are due: leaving the repetition there is what
         * ECMA-262 comes to. A backreference under a count is such a body.
         */
        private boolean leavesEarly(Node n) {
            Node body = n.subs[0];
            return n.min >= 2 && minLength(body) == 0 && maxLength(body) != 0 && !emptyAnywhere(body)
                    && !oneWayToMatch(body);
        }

        /**
         * Whether a node has at most one way to match where it stands, given what the groups outside it hold: it has
         * no alternatives and no repetition that may run a number of times or another. A lookaround counts as one
         * way, as it takes the first way its body matches and the match does not go back into it.
         */
        private static boolean oneWayToMatch(Node n) {
            switch (n.kind) {
                case ALT:
                    return false;
                case REPEAT:
                    return n.min == n.max && oneWayToMatch(n.subs[0]);
                case LOOK:
                    return true;
                default:
                    for (Node sub : n.subs) {
                        if (!oneWayToMatch(sub)) {
                            return false;
                        }
                    }
                    return true;
            }
        }

        /** Whether a node matches the empty string wherever it stands: through no assertion and no backreference. */
        private static boolean emptyAnywhere(Node n) {
            switch (n.kind) {
                case EMPTY:
                    return true;
                case CAT:
                    for (Node sub : n.subs) {
                        if (!emptyAnywhere(sub)) {
                            return false;
                        }
                    }
                    return true;
                case ALT:
                    for (Node sub : n.subs) {
                        if (emptyAnywhere(sub)) {
                            return true;
                        }
                    }
                    return false;
                case GROUP:
                    return emptyAnywhere(n.subs[0]);
                case REPEAT:
                    return n.min == 0 || emptyAnywhere(n.subs[0]);
                case BACKREF:
                    // A backreference to groups that are never set where it stands matches the empty string.
                    if (n.statuses == null) {
                        return false;
                    }
                    for (int status : n.statuses) {
                        if (status != NEVER) {
                            return false;
                        }
                    }
                    return true;
                default:
                    return false;
            }
        }

        /**
         * Writes a repetition with the iterations it must run apart, a copy of the body for each, and the ones it
         * may run after them. The copies are counted over the whole translation, as a repetition inside a body is
         * written again with each copy of it.
         */
        private void unrolled(Node n) {
            Node atom = n.subs[0];
            for (int k = 0; k < n.min; k++) {
                copy(atom);
            }
            if (n.max != n.min) {
                copy(atom);
                quantifier(0, n.max == UNBOUNDED ? UNBOUNDED : n.max - n.min, n.lazy);
            }
        }

        private void copy(Node atom) {
            int start = out.length();
            atom(atom);
            unrolledLength += out.length() - start;
            if (++unrolledCopies > MAX_UNROLLED_COPIES) {
                throw new TooLarge(REFUSED_EMPTY_ITERATIONS, LIMIT_COPIES);
            }
            if (unrolledLength > MAX_UNROLLED_LENGTH) {
                throw new TooLarge(REFUSED_EMPTY_ITERATIONS, LIMIT_TRANSLATION);
            }
        }

        /** Writes a node as one unit that a quantifier can follow. */
        private void atom(Node atom) {
            boolean unit = atom.kind == CHAR || atom.kind == GROUP || (atom.kind == SET && atom.set.length != 0);
            if (!unit) {
                out.append("(?:");
            }
            node(atom);
            if (!unit) {
                out.append(')');
            }
        }

        private void quantifier(int min, int max, boolean lazy) {
            if (min == 0 && max == UNBOUNDED) {
                out.append('*');
            } else if (min == 1 && max == UNBOUNDED) {
                out.append('+');
            } else if (min == 0 && max == 1) {
                out.append('?');
            } else {
                out.append('{').append(min);
                if (max != min) {
                    out.append(',');
                    if (max != UNBOUNDED) {
                        out.append(max);
                    }
                }
                out.append('}');
            }
            if (lazy) {
                out.append('?');
            }
        }

        /**
         * Writes a lookbehind. {@code java.util.regex} runs one by trying its body from each start within the bounds
         * it worked out for the length of the body, so it needs bounds, and for some bodies it has none or has them
         * wrong: it rejects a repeated group with alternatives, and for a body with two unbounded repetitions its
         * sum overflows and the lookbehind never matches.
         *
         * <p>So a body is written as it is only when this class can bound its length, and bound it low. Any other
         * body X is written in a form that needs no bound, where A is any character:
         *
         * <pre>(?=(A*))(?&lt;=(?=X\1\z)A{0,2147483647})</pre>
         *
         * <p>The first lookahead captures the rest of the text. The lookbehind then reaches back over any number of
         * characters to a start, and from that start a lookahead runs X forwards and requires that what follows X
         * be the captured rest up to the end of the text, which is so only where the lookbehind stands. The search
         * takes time that grows with the square of the length of the text.
         *
         * <p>Each character X takes is written after the test {@code (?!\1\z)}, that the place is not yet the one
         * the lookbehind stands at. X can match only up to that place, and without the test it runs on past it and
         * comes back, which for a repetition inside a repetition, as in {@code (?<=(?:[ax]+){2,})$}, is every way
         * to split the text after that place before the first way that ends at it. ECMA-262 runs the body backwards
         * from that place and never sees the text after it. For the same reason, when the characters X can take
         * are a set T of few ranges, X is tried only at a start from which every character up to that place is in
         * T, by the test {@code (?=(?:(?!\1\z)T)*\1\z)} before it. X cannot match from a start before a character
         * it cannot take, and it would try every way to split the text up to that character.
         *
         * <p>Running the body forwards where ECMA-262 runs it backwards finds a match exactly when ECMA-262 does. The
         * two directions can differ only in what the groups of the body capture, and a pattern with a backreference
         * to a group of a lookbehind, or inside a lookbehind, is written by paths.
         */
        private void lookbehind(Node n) {
            hasLookbehind = true;
            Node body = n.subs[0];
            if (!searchingLookbehind && maxLength(body) <= MAX_DIRECT_LOOKBEHIND) {
                out.append(n.negative ? "(?<!" : "(?<=");
                node(body);
                out.append(')');
                return;
            }
            int rest = ++javaGroups;
            out.append("(?=(").append(ANY).append("*))").append(n.negative ? "(?<!" : "(?<=");
            int[] taken = taken(body);
            boolean reach = taken != null && taken.length > 0 && taken.length <= 2 * MAX_REACH_RANGES;
            barrier = "(?!\\" + rest + "\\z)";
            if (reach) {
                // java.util.regex tries X at a start before it tries to reach the start, so the test comes first.
                out.append("(?=(?:").append(barrier);
                set(taken);
                out.append(")*\\").append(rest).append("\\z)");
            }
            out.append("(?=(?:");
            node(body);
            barrier = "";
            out.append(")\\").append(rest).append("\\z)").append(ANY).append("{0,2147483647})");
        }

        /**
         * Writes a backreference, which in ECMA-262 matches the text the group captured when the group has taken
         * part and the empty string when it has not. A group known to have taken part is read as it is. One that
         * may have is read behind a test of the empty group that marks it, and the last branch is the case where
         * none of the groups of the name took part.
         */
        private void backreference(Node n) {
            boolean any = false;
            boolean anyMaybe = false;
            for (int k = 0; k < n.groups.length; k++) {
                int g = n.groups[k];
                if (n.statuses[k] == NEVER) {
                    continue;
                }
                if (javaGroup[g] == 0) {
                    throw new IllegalStateException("backreference before its group");
                }
                out.append(any ? "|" : "(?:");
                any = true;
                if (n.statuses[k] == MAYBE) {
                    out.append("(?=\\").append(javaMarker[g]).append(')');
                    anyMaybe = true;
                }
                // The parentheses keep a digit that follows from being read as part of the group number.
                out.append(n.ignoreAsciiCase[k] ? "(?i:\\" : "(?:\\").append(javaGroup[g]).append(')');
            }
            if (!any) {
                return;
            }
            if (anyMaybe) {
                out.append('|');
                for (int k = 0; k < n.groups.length; k++) {
                    if (n.statuses[k] == MAYBE) {
                        out.append("(?!\\").append(javaMarker[n.groups[k]]).append(')');
                    }
                }
            }
            out.append(')');
        }

        // The translation by paths.
        //
        // A backreference reads what its group captured on the path the match is on. java.util.regex keeps other
        // text in a group in three cases: a group of an earlier iteration of a repetition (ECMA-262 unsets the
        // groups of a body when an iteration starts), a group of an iteration that matched nothing (which ECMA-262
        // does not take once the minimum is met), and a group of a lookaround, which it does not restore when the
        // match fails after the lookaround and goes back. So where a pattern has a backreference that the marker
        // form above cannot serve, the translation does not ask java.util.regex whether a group took part. It is
        // written so that each backreference stands on a path of the pattern text on which its group is known to
        // have been set, by a group of the translation that the path went through, or known not to have been.
        //
        // A choice that decides whether a group read later is set (an alternative, an optional part, the number of
        // iterations of a repetition) is a decision. Every way of taking the decisions of a scope is a plan, and the
        // scope is written once for each plan, as an alternative, with groups of its own. A scope is the pattern, the
        // body of a repetition, an alternative that sets nothing read after it, or a lookaround none of whose groups
        // is read after it. A match of the pattern is a match of one of the plans, so the order of the plans does not
        // matter, except in a lookaround whose groups are read after it: that one takes the first match of its body,
        // and is written only when it has no decision.

        /** The most plans of one scope. */
        static final int MAX_PLANS = 1 << 13;
        /** The most text of a translation by paths. */
        static final int MAX_PATHS_LENGTH = MAX_TRANSLATION_LENGTH;
        /**
         * The most text written while a translation by paths is made. A scope inside a scope is written again for
         * each plan of the outer one, so this is what bounds the time a translation takes.
         */
        static final long MAX_PATHS_WRITTEN = 16L * MAX_PATHS_LENGTH;

        /** What a path has set: for each group of the pattern, the group of the translation, or null. */
        private static final class Env {
            String[] name;
            /** Whether the group was set in a lookaround, which java.util.regex does not undo. */
            boolean[] look;
            /** The count of choices written when that lookaround ended. */
            int[] mark;
            /** The loop, by its serial number, that was open when the group was set, or 0. */
            int[] body;
            /** For a group set in a lookaround: the lookaround as it was written, and the number of what held it. */
            Look[] rec;
            int[] ctx;
            /** Whether the path wrote nothing for the group to match, so that it holds the empty string. */
            boolean[] empty;
            /**
             * Inside a lookahead written by ways: how many of the iterations that are open, counted from the
             * innermost, have matched nothing so far on the path.
             */
            int still;
            /**
             * Whether, since the innermost of those iterations started, the path wrote a part that may match
             * something and that is not a decision of the plan. Without one the iteration is known to match nothing.
             */
            boolean unsure;

            Env(int n) {
                name = new String[n];
                look = new boolean[n];
                mark = new int[n];
                body = new int[n];
                rec = new Look[n];
                ctx = new int[n];
                empty = new boolean[n];
            }

            Env copy() {
                Env e = new Env(0);
                e.name = name.clone();
                e.look = look.clone();
                e.mark = mark.clone();
                e.body = body.clone();
                e.rec = rec.clone();
                e.ctx = ctx.clone();
                e.empty = empty.clone();
                e.still = still;
                e.unsure = unsure;
                return e;
            }
        }

        private static final BitSet NOTHING = new BitSet();
        /** The text of a scope none of whose plans can match. */
        private static final String NEVER_MATCHES = "(?!)";

        private boolean precise;
        private Env env;
        /** The groups some backreference can read, and those of them that a plan wrote a backreference to. */
        private BitSet read;
        private final BitSet used = new BitSet();
        private int names;
        /** The places written so far at which java.util.regex can take another way when the match fails later. */
        private int choices;
        private int bodySerial;
        /** The repetitions being written as loops of java.util.regex, outermost first. */
        private final List<Loop> openLoops = new ArrayList<>();
        /** How many of them were open where the lookaround being written starts. */
        private int loopBase;
        /** A number for what a lookaround holds (and for the pattern): the one being written, and how many so far. */
        private int atomic;
        private int atomics;
        /** What {@link #ahead} or {@link #behind} has to note of its groups, for {@link #look} to note. */
        private Env noted;
        /** Inside a lookaround that is being written again (see {@link #rerun}). */
        private int replaying;
        /** How many references were written to groups of a lookaround that cannot be run again where they stand. */
        private int unanchored;
        private List<int[]> decisions;
        private int decisionAt;
        /**
         * Inside a lookahead whose groups are read after it, while it is written as it stands: the first match of
         * its body is the one taken, so it can be written that way only when it has no decision.
         */
        private boolean strict;
        /** The plans being written: the number of the innermost {@link #plans}, and how many there have been. */
        private int scopeNow;
        private int scopes;
        private boolean changed;
        private long written;

        private String translateByPaths() {
            precise = true;
            do {
                changed = false;
                clearLengths(root);
                flow(root, new BitSet(), new BitSet(), false);
                root = simplify(root);
            } while (changed);
            if ((long) groupNodes.length * countNodes(root) > MAX_GROUP_PARTS) {
                // Each part of the pattern is given the set of the groups it sets and of those it reads.
                throw new TooLarge(REFUSED_TOO_LARGE, LIMIT_GROUP_PARTS);
            }
            index(root);
            read = root.reads;
            String translated = writePaths();
            while (dropUnused(root)) {
                // A backreference that no plan wrote reads groups that are unset or empty on every path to it. The
                // pattern is the same without it, and has fewer decisions.
                do {
                    changed = false;
                    clearLengths(root);
                    flow(root, new BitSet(), new BitSet(), false);
                    root = simplify(root);
                } while (changed);
                index(root);
                read = root.reads;
                translated = writePaths();
            }
            translated = Ticks.insert(translated);
            String error = compileError(translated);
            if (error != null && hasLookbehind) {
                searchingLookbehind = true;
                translated = Ticks.insert(writePaths());
                error = compileError(translated);
            }
            return compiled(translated, error);
        }

        private static final long MAX_GROUP_PARTS = 1L << 28;

        private static long countNodes(Node n) {
            long count = 1;
            for (Node sub : n.subs) {
                count += countNodes(sub);
            }
            return count;
        }

        /** Makes of every backreference to groups no plan read a backreference to nothing. */
        private boolean dropUnused(Node n) {
            boolean any = false;
            if (n.kind == BACKREF && !n.dropped) {
                n.dropped = true;
                for (int g : n.groups) {
                    n.dropped &= !used.get(g);
                }
                any = n.dropped;
            }
            for (Node sub : n.subs) {
                any |= dropUnused(sub);
            }
            return any;
        }

        private static void clearLengths(Node n) {
            n.minLength = -1;
            for (Node sub : n.subs) {
                clearLengths(sub);
            }
        }

        /**
         * Removes what ECMA-262 never runs or runs to no effect, which leaves the pattern with the same meaning: a
         * repetition of no times, a repetition of a body that can only match the empty string (ECMA-262 does not
         * take an iteration that matched nothing once the minimum is met, so such a body runs once when the minimum
         * is one or more, and not at all otherwise), and a backreference to groups that are never set where it
         * stands or that can only capture the empty string.
         */
        private Node simplify(Node n) {
            for (int k = 0; k < n.subs.length; k++) {
                n.subs[k] = simplify(n.subs[k]);
            }
            Node never = fails(n);
            if (never != n) {
                changed = true;
                return never;
            }
            if (n.kind == REPEAT) {
                if (n.max == 0) {
                    changed = true;
                    return new Node(EMPTY);
                }
                if (maxLength(n.subs[0]) == 0) {
                    changed = true;
                    return n.min == 0 ? new Node(EMPTY) : n.subs[0];
                }
            } else if (n.kind == BACKREF) {
                for (int k = 0; k < n.groups.length && !n.dropped; k++) {
                    if (n.statuses[k] != NEVER && maxLength(groupNodes[n.groups[k]].subs[0]) != 0) {
                        return n;
                    }
                }
                changed = true;
                return new Node(EMPTY);
            }
            return n;
        }

        private static boolean isFailure(Node n) {
            return n.kind == SET && n.set.length == 0 && n.parts == null;
        }

        /**
         * What a node whose parts have been simplified comes to when some part of it can never match: a failure
         * (the empty class) for a sequence with a failing term, a group or a lookahead of a failure, and a negative
         * lookaround of a body that matches the empty string anywhere. Alternatives that fail are left out. A
         * negative lookaround of a failure always holds, and a repetition of one runs no times.
         */
        private static Node fails(Node n) {
            switch (n.kind) {
                case CAT:
                    for (Node sub : n.subs) {
                        if (isFailure(sub)) {
                            return sub;
                        }
                    }
                    return n;
                case ALT: {
                    int kept = 0;
                    for (Node sub : n.subs) {
                        kept += isFailure(sub) ? 0 : 1;
                    }
                    if (kept == n.subs.length) {
                        return n;
                    }
                    if (kept == 0) {
                        return n.subs[0];
                    }
                    Node[] subs = new Node[kept];
                    int m = 0;
                    for (Node sub : n.subs) {
                        if (!isFailure(sub)) {
                            subs[m++] = sub;
                        }
                    }
                    if (kept == 1) {
                        return subs[0];
                    }
                    Node alt = new Node(ALT);
                    alt.subs = subs;
                    return alt;
                }
                case GROUP:
                    return isFailure(n.subs[0]) ? n.subs[0] : n;
                case REPEAT:
                    if (isFailure(n.subs[0])) {
                        return n.min == 0 ? new Node(EMPTY) : n.subs[0];
                    }
                    return n;
                case LOOK:
                    if (isFailure(n.subs[0])) {
                        return n.negative ? new Node(EMPTY) : n.subs[0];
                    }
                    if (n.negative && emptyAnywhere(n.subs[0])) {
                        Node failure = new Node(SET);
                        failure.set = NO_CHARACTERS;
                        return failure;
                    }
                    return n;
                default:
                    return n;
            }
        }

        /** Notes for every node the groups it can set for what follows it, and the groups its backreferences read. */
        private void index(Node n) {
            n.sets = new BitSet();
            n.reads = new BitSet();
            for (Node sub : n.subs) {
                index(sub);
                n.reads.or(sub.reads);
                if (!(n.kind == LOOK && n.negative)) {
                    n.sets.or(sub.sets);
                }
            }
            if (n.kind == BACKREF) {
                for (int k = 0; k < n.groups.length; k++) {
                    if (n.statuses[k] != NEVER) {
                        n.reads.set(n.groups[k]);
                    }
                }
            } else if (n.kind == GROUP && n.group != 0) {
                n.sets.set(n.group);
            }
        }

        private String writePaths() {
            out.setLength(0);
            used.clear();
            written = 0;
            names = 0;
            choices = 0;
            bodySerial = 0;
            unrolledCopies = 0;
            unrolledLength = 0;
            hasLookbehind = false;
            strict = false;
            scopeNow = 0;
            scopes = 0;
            lookLive = NOTHING;
            iterations = 0;
            offsets.clear();
            openLoops.clear();
            loopBase = 0;
            atomic = 0;
            atomics = 0;
            noted = null;
            replaying = 0;
            unanchored = 0;
            chain = null;
            lastLoop = null;
            env = new Env(groupNodes.length);
            String text = scope(root, NOTHING, null);
            if (hasLookbehind) {
                text = "(?:" + text + ")(?:|𐀀)";
            }
            return numbered(text);
        }

        /**
         * Gives the groups of a translation their numbers. While it is written its groups have names, as a plan that
         * is written again takes new groups.
         */
        private static String numbered(String text) {
            Map<String, Integer> numbers = new HashMap<>();
            int at = 0;
            while ((at = text.indexOf("(?<g", at)) >= 0) {
                int end = text.indexOf('>', at);
                numbers.put(text.substring(at + 3, end), numbers.size() + 1);
                at = end;
            }
            if (numbers.isEmpty()) {
                return text;
            }
            StringBuilder sb = new StringBuilder(text.length());
            int from = 0;
            while (from < text.length()) {
                int open = text.indexOf("(?<g", from);
                int ref = text.indexOf("\\k<g", from);
                if (open < 0 && ref < 0) {
                    break;
                }
                if (ref < 0 || (open >= 0 && open < ref)) {
                    sb.append(text, from, open).append('(');
                    from = text.indexOf('>', open) + 1;
                } else {
                    int end = text.indexOf('>', ref);
                    // The parentheses keep a digit that follows from being read as part of the number.
                    sb.append(text, from, ref).append("(?:\\").append(numbers.get(text.substring(ref + 3, end)))
                            .append(')');
                    from = end + 1;
                }
            }
            return sb.append(text, from, text.length()).toString();
        }

        private String newName() {
            return "g" + ++names;
        }

        private void refer(String name) {
            out.append("\\k<").append(name).append('>');
        }

        private static BitSet without(BitSet a, BitSet b) {
            BitSet c = (BitSet) a.clone();
            c.andNot(b);
            return c;
        }

        private static BitSet both(BitSet a, BitSet b) {
            BitSet c = (BitSet) a.clone();
            c.and(b);
            return c;
        }

        /** Notes that the path has set a group, as the group of the translation whose body starts at {@code from}. */
        private void set(int group, String name, int from) {
            env.name[group] = name;
            env.look[group] = false;
            env.body[group] = loopNow() == null ? 0 : loopNow().serial;
            boolean empty = true;
            for (int k = from; k < out.length() && empty; k++) {
                // Only groups that hold nothing.
                empty = out.charAt(k) == ')' || out.indexOf("(?:", k) == k;
                k += out.charAt(k) == ')' ? 0 : 2;
            }
            env.empty[group] = empty;
        }

        private void unset(BitSet groups) {
            for (int g = groups.nextSetBit(0); g >= 0; g = groups.nextSetBit(g + 1)) {
                env.name[g] = null;
            }
        }

        /** Takes a decision of the plan being written: the one the plan holds, or the first way when it is new. */
        private int choose(int ways) {
            if (strict) {
                throw new NotOneWay();
            }
            if (decisionAt == decisions.size()) {
                decisions.add(new int[] {0, ways});
            }
            return decisions.get(decisionAt++)[0];
        }

        // The plans of a scope, joined.
        //
        // The plans of a scope are alternatives, and plans next to each other often start with the same text: what
        // the scope writes before the first decision, and after it whatever the plans that share a decision write
        // before the next. A repetition whose last iteration is written apart starts every plan with the iterations
        // before it, which can be most of the text. So the plans are joined as a tree: what plans next to each other
        // start with is written once, and the rest of each follows as alternatives, "A(?:B|C)" for "AB|AC". The two
        // match the same texts from the same place.
        //
        // The text of a plan is cut into its terms for this (an atom with its quantifier), with the groups that only
        // hold a sequence taken away, and terms are compared with the names of the groups they define left out: each
        // plan has names of its own for the same groups. The rest of a plan whose start is left out is written with
        // the names of the start that is kept.
        //
        // One thing is not shared: a term that sets a group inside a lookaround, when the rest of a plan reads the
        // group. java.util.regex does not undo such a group, so where the plans are apart a plan that is tried after
        // another sets the group again, and after a shared start it would read what a later run of the lookaround
        // left in it (see the notes above Look).

        /** The plans of a scope as one group of alternatives, with what plans next to each other start with once. */
        private String joined(List<String> plans) {
            List<List<String>> all = new ArrayList<>(plans.size());
            for (String plan : plans) {
                all.add(flat(plan));
            }
            StringBuilder sb = new StringBuilder("(?:");
            alternatives(sb, all, 0, all.size(), 0);
            sb.append(')');
            if (out.length() + sb.length() > MAX_PATHS_LENGTH) {
                throw new TooLarge(REFUSED_TOO_MANY_PATHS, LIMIT_TRANSLATION);
            }
            return sb.toString();
        }

        /**
         * Writes the plans from {@code from} to {@code to}, which share their terms before {@code at}, from that term
         * on.
         */
        private static void alternatives(StringBuilder sb, List<List<String>> all, int from, int to, int at) {
            int k = from;
            while (k < to) {
                if (k > from) {
                    sb.append('|');
                }
                List<String> lead = all.get(k);
                // The plans after this one that start with the same term, and how many terms all of them share.
                int end = k + 1;
                int shared = lead.size();
                List<List<Map<String, String>>> renames = new ArrayList<>();
                while (end < to && shared > at) {
                    List<Map<String, String>> same = sameTerms(lead, all.get(end), at, shared);
                    if (same.isEmpty()) {
                        break;
                    }
                    shared = at + same.size();
                    renames.add(same);
                    end++;
                }
                int cut = end - k == 1 ? at : cut(all, k, end, at, shared);
                if (cut == at) {
                    for (int p = k; p < end; p++) {
                        sb.append(p > k ? "|" : "");
                        append(sb, all.get(p), at);
                    }
                    k = end;
                    continue;
                }
                for (int t = at; t < cut; t++) {
                    sb.append(lead.get(t));
                }
                for (int p = k + 1; p < end; p++) {
                    Map<String, String> names = new HashMap<>();
                    List<Map<String, String>> same = renames.get(p - k - 1);
                    for (int t = at; t < cut; t++) {
                        names.putAll(same.get(t - at));
                    }
                    List<String> plan = all.get(p);
                    for (int t = cut; t < plan.size() && !names.isEmpty(); t++) {
                        plan.set(t, renamed(plan.get(t), names));
                    }
                }
                sb.append("(?:");
                alternatives(sb, all, k, end, cut);
                sb.append(')');
                k = end;
            }
        }

        private static void append(StringBuilder sb, List<String> plan, int at) {
            for (int t = at; t < plan.size(); t++) {
                sb.append(plan.get(t));
            }
        }

        /**
         * For each term from {@code at} on, up to {@code limit}, that two plans have in common: the names of the
         * second plan for the groups the term defines, with the names of the first. The list ends at the first term
         * that differs.
         */
        private static List<Map<String, String>> sameTerms(List<String> a, List<String> b, int at, int limit) {
            List<Map<String, String>> same = new ArrayList<>();
            Map<String, String> names = new HashMap<>();
            for (int t = at; t < limit && t < b.size(); t++) {
                Map<String, String> defined = sameTerm(a.get(t), b.get(t), names);
                if (defined == null) {
                    break;
                }
                names.putAll(defined);
                same.add(defined);
            }
            return same;
        }

        /**
         * Whether two terms are the same but for the names of the groups they define. Returns the names of the second
         * for those groups with the names of the first, or null when the terms differ. {@code names} holds the same
         * for the terms before.
         */
        private static Map<String, String> sameTerm(String a, String b, Map<String, String> names) {
            if (a.length() != b.length() && a.indexOf("<g") < 0) {
                return null;
            }
            Map<String, String> defined = new HashMap<>();
            // The text between the names, and the groups defined.
            int ia = 0;
            int ib = 0;
            while (true) {
                int na = nextName(a, ia);
                int nb = nextName(b, ib);
                int sa = na < 0 ? a.length() : na;
                int sb = nb < 0 ? b.length() : nb;
                if ((na < 0) != (nb < 0) || sa - ia != sb - ib || !a.regionMatches(ia, b, ib, sa - ia)) {
                    return null;
                }
                if (na < 0) {
                    break;
                }
                int da = digitsEnd(a, na);
                int db = digitsEnd(b, nb);
                ia = a.indexOf('>', da);
                ib = b.indexOf('>', db);
                if (ia - da != ib - db || !a.regionMatches(da, b, db, ia - da)) {
                    return null;
                }
                if (a.charAt(na - 2) == '?' && ia == da) {
                    String name = a.substring(na, da);
                    String was = defined.put(b.substring(nb, db), name);
                    if (was != null && !was.equals(name)) {
                        return null;
                    }
                }
            }
            // Every name of the second is the name of the first at the same place.
            ia = 0;
            ib = 0;
            while ((ia = nextName(a, ia)) >= 0) {
                ib = nextName(b, ib);
                int da = digitsEnd(a, ia);
                int db = digitsEnd(b, ib);
                String name = b.substring(ib, db);
                String mapped = defined.get(name);
                if (mapped == null) {
                    mapped = names.getOrDefault(name, name);
                }
                if (mapped.length() != da - ia || !a.regionMatches(ia, mapped, 0, da - ia)) {
                    return null;
                }
                ia = da;
                ib = db;
            }
            return defined;
        }

        /**
         * Where the next name of a group of the translation stands in a text, from a place on, or -1: the "g" of
         * "(?<g12>" or "\\k<g12>". The translation has "<g" nowhere else, as it writes such characters by number.
         */
        private static int nextName(String text, int from) {
            for (int at = text.indexOf("<g", from); at >= 0; at = text.indexOf("<g", at + 1)) {
                if (at >= 2 && at + 2 < text.length() && isDigit(text.charAt(at + 2))
                        && ((text.charAt(at - 1) == '?' && text.charAt(at - 2) == '(')
                                || (text.charAt(at - 1) == 'k' && text.charAt(at - 2) == '\\'))) {
                    return at + 1;
                }
            }
            return -1;
        }

        /** The place after the number of the name that starts at {@code name}. */
        private static int digitsEnd(String text, int name) {
            int end = name + 1;
            while (isDigit(text.charAt(end))) {
                end++;
            }
            return end;
        }

        private static boolean isDigit(char c) {
            return c >= '0' && c <= '9';
        }

        /** A term with other names for some groups. */
        private static String renamed(String term, Map<String, String> names) {
            StringBuilder sb = null;
            int from = 0;
            for (int at = nextName(term, 0); at >= 0; at = nextName(term, at)) {
                int end = digitsEnd(term, at);
                String to = names.get(term.substring(at, end));
                if (to != null) {
                    sb = sb != null ? sb : new StringBuilder(term.length() + 8);
                    sb.append(term, from, at).append(to);
                    from = end;
                }
                at = end;
            }
            return sb == null ? term : sb.append(term, from, term.length()).toString();
        }

        /**
         * How many of the terms that the plans from {@code from} to {@code to} share, from {@code at} up to
         * {@code shared}, can be written once: the most for which none of them sets a group in a lookaround that a
         * term after them reads, in any of the plans.
         */
        private static int cut(List<List<String>> all, int from, int to, int at, int shared) {
            int cut = shared;
            for (int p = from; p < to && cut > at; p++) {
                List<String> plan = all.get(p);
                // For each name set in a lookaround in the shared terms: the term that sets it.
                Map<String, Integer> sticky = new HashMap<>();
                for (int t = at; t < cut; t++) {
                    setInLookarounds(plan.get(t), t, sticky);
                }
                if (sticky.isEmpty()) {
                    continue;
                }
                // The last term that reads a group each term sets.
                int[] last = new int[cut - at];
                for (int t = at + 1; t < plan.size(); t++) {
                    String term = plan.get(t);
                    for (int name = nextName(term, 0); name >= 0; name = nextName(term, name)) {
                        int end = digitsEnd(term, name);
                        Integer set = term.charAt(name - 2) == '?' ? null : sticky.get(term.substring(name, end));
                        if (set != null && set < t) {
                            last[set - at] = t;
                        }
                        name = end;
                    }
                }
                // The most terms such that what reads a group they set is among them.
                int best = at;
                int need = at;
                for (int t = at; t < cut; t++) {
                    need = Math.max(need, last[t - at]);
                    if (need <= t) {
                        best = t + 1;
                    }
                }
                cut = best;
            }
            return cut;
        }

        /** Notes, for each group a term defines inside a lookaround, the number of the term. */
        private static void setInLookarounds(String term, int number, Map<String, Integer> sticky) {
            if (term.indexOf("(?<g") < 0) {
                return;
            }
            // For each group that is open, whether it is in a lookaround. An atomic group counts as one, and so
            // does a term with a possessive quantifier: the engine does not undo what was set in those either.
            BitSet inLook = new BitSet();
            inLook.set(0, term.indexOf("*+") >= 0 || term.indexOf("++") >= 0 || term.indexOf("?+") >= 0
                    || term.indexOf("}+") >= 0);
            int depth = 0;
            for (int i = 0, n = term.length(); i < n;) {
                char c = term.charAt(i);
                if (c == '\\') {
                    i += 2;
                } else if (c == '[') {
                    i = classEnd(term, i);
                } else {
                    if (c == '(') {
                        boolean look = term.startsWith("(?=", i) || term.startsWith("(?!", i)
                                || term.startsWith("(?<=", i) || term.startsWith("(?<!", i)
                                || term.startsWith("(?>", i);
                        inLook.set(depth + 1, look || inLook.get(depth));
                        depth++;
                        if (inLook.get(depth) && term.startsWith("(?<g", i)) {
                            int end = term.indexOf('>', i);
                            int digits = i + 4;
                            while (digits < end && Character.isDigit(term.charAt(digits))) {
                                digits++;
                            }
                            sticky.put(term.substring(i + 3, digits), number);
                        }
                    } else if (c == ')') {
                        depth--;
                    }
                    i++;
                }
            }
        }

        /**
         * The terms of a text of the translation: each an atom with its quantifier. A group that only holds a
         * sequence, with no quantifier, gives its terms in its place. A text with alternatives is one term.
         */
        private static List<String> flat(String text) {
            List<String> terms = new ArrayList<>();
            List<String> todo = terms(text);
            if (todo == null) {
                terms.add(text);
                return terms;
            }
            Collections.reverse(todo);
            while (!todo.isEmpty()) {
                String next = todo.remove(todo.size() - 1);
                List<String> parts = next.startsWith("(?:") && atomEnd(next, 0) == next.length()
                        ? terms(next.substring(3, next.length() - 1)) : null;
                if (parts == null) {
                    terms.add(next);
                    continue;
                }
                for (int k = parts.size() - 1; k >= 0; k--) {
                    todo.add(parts.get(k));
                }
            }
            return terms;
        }

        /** The terms of a text, or null when it is alternatives. */
        private static List<String> terms(String text) {
            List<String> terms = new ArrayList<>();
            int n = text.length();
            for (int i = 0; i < n;) {
                char c = text.charAt(i);
                if (c == '|') {
                    return null;
                }
                if (c == ')' || c == '*' || c == '+' || c == '?' || c == '{') {
                    throw new IllegalStateException("a stretch of a translation that is not a term");
                }
                int end = atomEnd(text, i);
                if (end < n) {
                    char q = text.charAt(end);
                    int after = q == '{' ? text.indexOf('}', end) + 1 : end;
                    if (q == '*' || q == '+' || q == '?') {
                        after = end + 1;
                    }
                    if (after > end && after < n && (text.charAt(after) == '?' || text.charAt(after) == '+')) {
                        after++;
                    }
                    end = after;
                }
                terms.add(text.substring(i, end));
                i = end;
            }
            return terms;
        }

        /** The place after the atom that starts at {@code i} in a text of the translation. */
        private static int atomEnd(String text, int i) {
            char c = text.charAt(i);
            if (c == '\\') {
                char e = text.charAt(i + 1);
                if (e == 'x' && text.charAt(i + 2) == '{') {
                    return text.indexOf('}', i) + 1;
                }
                return e == 'k' ? text.indexOf('>', i) + 1 : i + 2;
            }
            if (c == '[') {
                return classEnd(text, i);
            }
            if (c != '(') {
                return Character.isHighSurrogate(c) ? i + 2 : i + 1;
            }
            int depth = 0;
            do {
                c = text.charAt(i);
                if (c == '\\') {
                    i += 2;
                } else if (c == '[') {
                    i = classEnd(text, i);
                } else {
                    depth += c == '(' ? 1 : c == ')' ? -1 : 0;
                    i++;
                }
            } while (depth > 0);
            return i;
        }

        /** The place after the class that starts at {@code i} in a text of the translation. */
        private static int classEnd(CharSequence text, int i) {
            int depth = 0;
            do {
                char c = text.charAt(i);
                if (c == '\\') {
                    i++;
                } else if (c == '[') {
                    depth++;
                } else if (c == ']') {
                    depth--;
                }
                i++;
            } while (depth > 0);
            return i;
        }

        /**
         * Thrown where the plan being written turns out to match nothing: an iteration past the minimum of its
         * repetition in which the plan wrote only parts that match the empty string. ECMA-262 does not take such an
         * iteration, and the test after it would fail every time. The plan is left out (see {@link #plans}), which
         * is what keeps a counted repetition of a body that can match the empty string from being written twice
         * for each iteration.
         */
        private static final class DeadPlan extends RuntimeException {
            private static final long serialVersionUID = 1L;
            static final DeadPlan INSTANCE = new DeadPlan();

            private DeadPlan() {
                super(null, null, false, false);
            }
        }

        /** Something that writes text on the plan being written. */
        private interface Writer {
            void write();
        }

        /**
         * Writes a scope: the node once for each plan, and returns the text, which is one alternative for each plan
         * inside a group when there are several. The groups in {@code reset}, if any, are unset where each plan
         * starts. What the plans set is forgotten when the scope ends.
         */
        private String scope(Node n, BitSet live, BitSet reset) {
            return scope(n, live, reset, null);
        }

        /** As {@link #scope(Node, BitSet, BitSet)}, where {@code since} is the {@link #chain} of each plan. */
        private String scope(Node n, BitSet live, BitSet reset, String since) {
            String wasChain = chain;
            int wasStart = chainStart;
            int wasScope = chainScope;
            List<String> plans = plans(() -> {
                chain = since;
                chainStart = out.length();
                chainScope = scopeNow;
                p(n, live);
            }, reset);
            chain = wasChain;
            chainStart = wasStart;
            chainScope = wasScope;
            if (plans.size() == 1) {
                return plans.get(0);
            }
            if (strict) {
                throw new NotOneWay();
            }
            choices++;
            return joined(plans);
        }

        /** The text a writer writes, once for each way of taking the decisions it meets. */
        private List<String> plans(Writer writer, BitSet reset) {
            Env entry = env;
            List<int[]> outerDecisions = decisions;
            int outerAt = decisionAt;
            int outerScope = scopeNow;
            int outerOffsets = offsets.size();
            scopeNow = ++scopes;
            List<int[]> mine = new ArrayList<>();
            List<String> plans = new ArrayList<>();
            int start = out.length();
            // The copies of bodies are counted along one way through the pattern, which is what the depth of the
            // recursion of java.util.regex follows.
            int copies = unrolledCopies;
            int copied = unrolledLength;
            int most = copies;
            int longest = copied;
            boolean more;
            do {
                unrolledCopies = copies;
                unrolledLength = copied;
                env = entry.copy();
                if (reset != null) {
                    unset(reset);
                }
                decisions = mine;
                decisionAt = 0;
                while (offsets.size() > outerOffsets) {
                    offsets.remove(offsets.size() - 1);
                }
                Mark mark = new Mark();
                int wasUnanchored = unanchored;
                boolean dead = false;
                try {
                    writer.write();
                } catch (DeadPlan e) {
                    dead = true;
                }
                String plan = out.substring(start);
                out.setLength(start);
                written += plan.length();
                if (dead) {
                    // The decisions the plan took stay, so that the next plan differs in the last of them.
                    List<int[]> taken = new ArrayList<>(mine);
                    mark.restore();
                    unanchored = wasUnanchored;
                    mine.clear();
                    mine.addAll(taken);
                    while (offsets.size() > outerOffsets) {
                        offsets.remove(offsets.size() - 1);
                    }
                } else {
                    plans.add(plan);
                    most = Math.max(most, unrolledCopies);
                    longest = Math.max(longest, unrolledLength);
                }
                if (plans.size() > MAX_PLANS) {
                    throw new TooLarge(REFUSED_TOO_MANY_PATHS, LIMIT_PLANS);
                }
                if (start + plan.length() > MAX_PATHS_LENGTH) {
                    throw new TooLarge(REFUSED_TOO_MANY_PATHS, LIMIT_TRANSLATION);
                }
                if (written > MAX_PATHS_WRITTEN) {
                    throw new TooLarge(REFUSED_TOO_MANY_PATHS, LIMIT_WRITTEN);
                }
                more = false;
                while (!mine.isEmpty()) {
                    int[] last = mine.get(mine.size() - 1);
                    if (last[0] + 1 < last[1]) {
                        last[0]++;
                        more = true;
                        break;
                    }
                    mine.remove(mine.size() - 1);
                }
            } while (more);
            decisions = outerDecisions;
            decisionAt = outerAt;
            scopeNow = outerScope;
            env = entry;
            unrolledCopies = most;
            unrolledLength = longest;
            if (plans.isEmpty()) {
                // No plan can match, so the scope is a failure.
                plans.add(NEVER_MATCHES);
            }
            return plans;
        }

        /** Writes a node on the plan being written. {@code live} holds the groups that are read after the node. */
        private void p(Node n, BitSet live) {
            switch (n.kind) {
                case CAT: {
                    BitSet[] after = new BitSet[n.subs.length];
                    BitSet later = live;
                    for (int k = n.subs.length - 1; k >= 0; k--) {
                        after[k] = later;
                        if (!n.subs[k].reads.isEmpty()) {
                            later = (BitSet) later.clone();
                            later.or(n.subs[k].reads);
                        }
                    }
                    for (int k = 0; k < n.subs.length; k++) {
                        p(n.subs[k], after[k]);
                    }
                    break;
                }
                case ALT:
                    if (n.sets.intersects(live) || readsTracked(n)) {
                        // Which alternative is taken decides what the groups read later hold. And an alternative
                        // that reads a group of a lookaround of this iteration is taken before the lookaround is
                        // written, so that the match cannot come back to it past a later run of the lookaround.
                        out.append("(?:");
                        p(n.subs[choose(n.subs.length)], live);
                        out.append(')');
                    } else {
                        String since = extend(n);
                        out.append("(?:");
                        for (int k = 0; k < n.subs.length; k++) {
                            if (k > 0) {
                                out.append('|');
                            }
                            out.append(scope(n.subs[k], live, null, since));
                        }
                        out.append(')');
                        if (n.subs.length > 1) {
                            choices++;
                        }
                    }
                    break;
                case GROUP:
                    if (n.group != 0 && read.get(n.group)) {
                        String name = newName();
                        out.append("(?<").append(name).append('>');
                        int from = out.length();
                        p(n.subs[0], live);
                        set(n.group, name, from);
                        out.append(')');
                    } else {
                        out.append("(?:");
                        p(n.subs[0], live);
                        out.append(')');
                    }
                    break;
                case REPEAT:
                    repetition(n, live);
                    break;
                case LOOK:
                    look(n, live);
                    break;
                case BACKREF:
                    reference(n);
                    break;
                case RAW:
                    out.append(n.text);
                    break;
                case CLOSE:
                    close(n);
                    break;
                case ITERATION:
                    oneMore(n, live);
                    break;
                default:
                    node(n);
                    break;
            }
        }

        /**
         * Writes a repetition. When none of the groups of its body is read after it, it is written as it is, with
         * the body as a scope of its own (the groups of a body are unset when an iteration starts, so nothing an
         * iteration sets is read in another).
         *
         * <p>Otherwise the iteration that runs last, whose groups are the ones read, is written apart, after the
         * others, and how many iterations run is a decision of the plan:
         *
         * <ul>
         *   <li>none, when the minimum is 0: nothing is written, and the groups stay as they were;
         *   <li>exactly the minimum m, when it is 1 or more: {@code (?:X){m-1}} and then the body;
         *   <li>more than the minimum: {@code (?:X){m,n-1}} and then the body, which must not match the empty
         *       string here, as ECMA-262 does not take an iteration past the minimum that matched nothing.
         * </ul>
         *
         * <p>java.util.regex takes an iteration that matched nothing as the last one of a repetition. In the
         * repetitions written here that changes no answer: none of their groups is read after them, and without the
         * empty iteration the match is at the same place.
         */
        private void repetition(Node n, BitSet live) {
            Node body = n.subs[0];
            if (strict && takesEmptyIterations(n)) {
                throw new NotOneWay();
            }
            prepare(body);
            if (!n.sets.intersects(live)) {
                repeated(body, n.min, n.max, n.lazy);
                return;
            }
            boolean canRunMore = n.max == UNBOUNDED || n.max > Math.max(n.min, 1) || n.min == 0;
            int way;
            if (n.min == 0) {
                way = choose(2) == 0 ? 2 : 0;
            } else {
                way = canRunMore ? 1 + choose(2) : 1;
            }
            if (way == 0) {
                return;
            }
            if (way == 1) {
                repeated(body, n.min - 1, n.min - 1, false);
                unset(body.sets);
                out.append("(?:");
                p(body, live);
                out.append(')');
                return;
            }
            repeated(body, n.min, n.max == UNBOUNDED ? UNBOUNDED : n.max - 1, n.lazy);
            unset(body.sets);
            if (minLength(body) > 0) {
                out.append("(?:");
                p(body, live);
                out.append(')');
                return;
            }
            String taken = newName();
            String rest = newName();
            out.append("(?<").append(taken).append('>');
            p(body, live);
            out.append(")(?=(?<").append(rest).append('>').append(ANY).append("*))(?!");
            refer(taken);
            refer(rest);
            out.append("\\z)");
        }

        /**
         * Whether a repetition can take, past its minimum, an iteration that matches the empty string of a body
         * that can also match more. ECMA-262 does not take such an iteration and goes on to the other ways of the
         * body, and java.util.regex takes it and leaves the repetition. Both reach the same places, in another
         * order, which shows only where the first match is the one that counts: in a lookahead whose groups are
         * read after it.
         */
        private boolean takesEmptyIterations(Node n) {
            return n.max != n.min && minLength(n.subs[0]) == 0 && maxLength(n.subs[0]) != 0;
        }

        /** Whether some repetition in a node {@link #takesEmptyIterations}. */
        private boolean holdsEmptyIterations(Node n) {
            if (n.kind == REPEAT && takesEmptyIterations(n)) {
                return true;
            }
            for (Node sub : n.subs) {
                if (holdsEmptyIterations(sub)) {
                    return true;
                }
            }
            return false;
        }

        /** Writes a repetition none of whose groups is read after it. */
        private void repeated(Node body, int min, int max, boolean lazy) {
            if (max == 0) {
                return;
            }
            // The iterations that are due are written apart where java.util.regex would leave the repetition early
            // (see leavesEarly). And in a loop inside another, the place an iteration started at can be told only
            // when the loop has no minimum (see stamped), so where that may be asked for they are written apart too.
            boolean early = min >= 2 && minLength(body) == 0 && maxLength(body) != 0 && !emptyAnywhere(body)
                    && !oneWayToMatch(body);
            boolean inner = min > 0 && (max == UNBOUNDED || max > 1) && loopNow() != null && holdsReadLook(body);
            if (early || inner) {
                for (int k = 0; k < min; k++) {
                    copyOfBody(body, early);
                }
                if (max != min) {
                    int start = out.length();
                    loop(body, 0, max == UNBOUNDED ? UNBOUNDED : max - min, lazy);
                    counted(out.length() - start, early);
                }
                return;
            }
            loop(body, min, max, lazy);
        }

        private void copyOfBody(Node body, boolean early) {
            String text = bodyText(body, false, false, false);
            out.append("(?:").append(text).append(')');
            counted(text.length(), early);
        }

        /**
         * Counts a copy of a body. The copies of a repetition that java.util.regex would leave early have limits of
         * their own. The others are bounded by the number of copies and by the limits of a translation by paths.
         */
        private void counted(int length, boolean early) {
            if (early) {
                unrolledLength += length;
            }
            if (++unrolledCopies > MAX_UNROLLED_COPIES) {
                throw new TooLarge(early ? REFUSED_EMPTY_ITERATIONS : REFUSED_TOO_MANY_PATHS, LIMIT_COPIES);
            }
            if (unrolledLength > MAX_UNROLLED_LENGTH) {
                throw new TooLarge(REFUSED_EMPTY_ITERATIONS, LIMIT_TRANSLATION);
            }
        }

        /** Writes the body of a repetition with its quantifier. */
        private void loop(Node body, int min, int max, boolean lazy) {
            boolean varies = min != max;
            String text = bodyText(body, max == UNBOUNDED || max > 1, lazy && varies, min == 0);
            Loop mine = lastLoop;
            out.append("(?:").append(text).append(')');
            if (mine != null && mine.stamp != null && !mine.single) {
                // One more iteration, which is the empty one that ends the loop (see stamped).
                quantifier(0, max == UNBOUNDED ? UNBOUNDED : max + 1, lazy);
                out.append("(?=\\k<").append(mine.stamp).append(">\\z)");
            } else if (min != 1 || max != 1) {
                quantifier(min, max, lazy);
            }
            if (varies) {
                choices++;
            }
        }

        /**
         * The body of a repetition as a scope, with its groups unset where it starts. {@code loop} says that
         * java.util.regex can run the text more than once, {@code lazy} that it tries the body after what follows,
         * and {@code optional} that the repetition has no minimum.
         *
         * <p>A reference in the body to a group of a lookaround that stands before the repetition, in an iteration
         * of a loop around both, reads what java.util.regex left in the group. That is the text ECMA-262 means
         * unless the lookaround has run again since, in a later iteration, and the match has come back into the
         * body. Where the reference could not be given a run of the lookaround of its own (see {@link #fresh}), the
         * body is written only when the match cannot come back into it before the reference: it has one plan, no
         * place to go back to, and is not tried again after what follows.
         */
        private String bodyText(Node body, boolean loop, boolean lazy, boolean optional) {
            String since = loop ? "" : extend(body);
            Loop mine = loop ? open(optional) : null;
            int references = unanchored;
            int wasChoices = choices;
            String wasChain = chain;
            int wasStart = chainStart;
            int wasScope = chainScope;
            List<String> plans = plans(() -> {
                if (mine != null) {
                    mine.scope = scopeNow;
                }
                chain = since;
                chainStart = out.length();
                chainScope = scopeNow;
                p(body, NOTHING);
            }, body.sets);
            chain = wasChain;
            chainStart = wasStart;
            chainScope = wasScope;
            if (mine != null) {
                openLoops.remove(openLoops.size() - 1);
            }
            lastLoop = mine;
            if (unanchored != references && (plans.size() != 1 || choices != wasChoices || lazy)) {
                throw new Unsupported(REFUSED_LOOKAROUND_IN_REPETITION);
            }
            String text = plans.get(0);
            if (plans.size() > 1) {
                if (strict) {
                    throw new NotOneWay();
                }
                choices++;
                text = joined(plans);
            }
            return mine == null || mine.stamp == null ? text : stamped(mine, text);
        }

        /** The loop {@link #bodyText} wrote the body of last, or null when it was not a loop. */
        private Loop lastLoop;

        /**
         * In an iteration of a loop: backreferences to the text matched from where the iteration started to where
         * the plan being written starts, or null when that is not known. The text from there on is at
         * {@link #chainStart} in the translation, and {@link #chainScope} is the scope of the plan.
         */
        private String chain;
        private int chainStart;
        private int chainScope;

        /**
         * The {@link #chain} of a scope that starts here and holds the node, or null when no chain is kept for it. One
         * is kept where the node holds a lookaround whose groups are read, which a reference may have to run again
         * (see {@link #anchor}). It puts the text of the plan so far in groups.
         */
        private String extend(Node n) {
            if (chain == null || chainScope != scopeNow || loopNow() == null || !holdsReadLook(n)) {
                return null;
            }
            return chain + pieces(chainStart, out.length());
        }

        /** Whether a node holds a lookaround that sets a group some backreference reads. */
        private boolean holdsReadLook(Node n) {
            if (n.kind == LOOK && !n.negative && n.sets.intersects(read)) {
                return true;
            }
            for (Node sub : n.subs) {
                if (holdsReadLook(sub)) {
                    return true;
                }
            }
            return false;
        }

        /** A repetition written as a loop of java.util.regex, while its body is being written. */
        private static final class Loop {
            int serial;
            /** Whether no other loop was open where it started, in the lookaround that holds it or in the pattern. */
            boolean single;
            /** Whether it has no minimum. */
            boolean optional;
            /** The scope of the plan of the iteration being written. */
            int scope;
            /** The group that holds the rest of the text from where the iteration started, once one is asked for. */
            String stamp;
        }

        private Loop open(boolean optional) {
            Loop mine = new Loop();
            mine.serial = ++bodySerial;
            mine.optional = optional;
            mine.single = openLoops.size() == loopBase;
            openLoops.add(mine);
            return mine;
        }

        /** The innermost loop open in the lookaround being written (or in the pattern), or null. */
        private Loop loopNow() {
            return openLoops.size() > loopBase ? openLoops.get(openLoops.size() - 1) : null;
        }

        /**
         * The body of a loop with the group S that holds the rest of the text from where the iteration started.
         *
         * <p>A group set in a lookahead at that place would not do: java.util.regex does not restore it when the
         * match goes back from the next iteration into this one. So S is set again on the way back. The body stands
         * in a group I, which holds the text of the iteration before while an iteration starts, and starts with
         *
         * <pre>(?:|(F)) (?=(R A*)) (?&lt;=(?=(?=(?:(?!F)|(?=F)I)R\z)(S A*))A{0,2147483647}) (?!F)</pre>
         *
         * <p>Going forward, F is not set and S is set where the iteration starts. When the match comes back to the
         * start of the iteration, the second branch sets F, S is set one iteration back (the place from which I and
         * then the rest of the text from here follow), and the last test fails, so the match goes on back with S as
         * it was in the iteration before. F is only ever set on the way to that failing test.
         *
         * <p>That keeps S right while the match stays in the loop, which is enough for a loop that is entered once
         * on a path. A loop inside another is entered again, and the match can come back from there into its last
         * iteration, past no start of an iteration. So such a loop ends with one more iteration, which matches
         * nothing: the body is {@code (?:X|)}, and the loop is followed by {@code (?=S\z)}, which holds only after
         * that iteration (it set S where the loop ends, and an iteration of X starts before that). When the match
         * comes back into the loop it passes the start of that iteration, and S is set back as above. For this the
         * loop must have no minimum, as java.util.regex leaves a loop after an iteration that matched nothing. X
         * stands first also in a lazy loop, so that the empty iteration is taken only once X has failed there, and
         * the match that comes back into it has nowhere to go but past its start.
         */
        private String stamped(Loop mine, String text) {
            String flag = "\\k<" + mine.stamp + "f>";
            String here = newName();
            hasLookbehind = true;
            String body = mine.single ? text : "(?:" + text + "|)";
            return "(?<" + mine.stamp + "i>(?:|(?<" + mine.stamp + "f>))(?=(?<" + here + ">" + ANY + "*))(?<=(?=(?=(?:(?!" + flag
                    + ")|(?=" + flag + ")\\k<" + mine.stamp + "i>)\\k<" + here + ">\\z)(?<" + mine.stamp + ">" + ANY + "*))"
                    + ANY + "{0,2147483647})(?!" + flag + ")" + body + ")";
        }

        // A group of a lookaround, read in the same iteration of a loop.
        //
        // java.util.regex does not restore the groups a lookaround set when the match fails after it and goes back.
        // Outside a loop that cannot be seen: the group is set again before it is read again. In a loop it can: the
        // lookaround of a later iteration sets the group, the match goes back into the earlier iteration, to a place
        // between its lookaround and a reference, and the reference reads the text of the later iteration, where
        // ECMA-262 reads that of its own. So a reference that the match can reach that way is written after another
        // run of the lookaround, which sets groups of its own:
        //
        // - a choice between the lookaround and the reference that holds the reference (alternatives) is a decision
        //   of the plan, so it is taken before the lookaround;
        // - past a place the match can come back to, in the same plan, the lookaround is run again at the place it
        //   stood at, which is found by the text matched since: that text is put in groups (see #pieces), and a
        //   lookbehind reaches back to the place from which those groups and then the rest of the text follow;
        // - in the body of a repetition that started after the lookaround, the text matched since cannot be put in
        //   groups, as iterations are in it. There the place is found from the start of the iteration of the loop
        //   around both (see #stamped) and the text from there to the lookaround.
        //
        // A lookaround is one step for java.util.regex: the match does not come back into it. So what stands in a
        // lookaround cannot be reached that way from outside it, and the groups it reads are made right where it
        // starts.

        /** A lookaround as it was written on a plan, whose groups are read after it: what is needed to write it again. */
        private static final class Look {
            Node node;
            BitSet live;
            Env before;
            List<int[]> decisions;
            int from;
            /** Where its text starts and ends in the translation, and the scope it was written in. */
            int[] start;
            int[] end;
            int scope;
            /** Backreferences to the text matched from the place it stood at to where its text ends. */
            String back = "";
            /** The loop it stands in, and once asked for: the group of the start of the iteration, and the text since. */
            Loop loop;
            String anchor;
            String prefix;
        }

        /** Whether a group was set by a lookaround in an iteration, still open, of a loop of what is being written. */
        private boolean tracked(int g) {
            if (!env.look[g] || env.name[g] == null || env.rec[g] == null || env.ctx[g] != atomic || env.body[g] == 0) {
                return false;
            }
            for (int k = loopBase; k < openLoops.size(); k++) {
                if (openLoops.get(k).serial == env.body[g]) {
                    return true;
                }
            }
            return false;
        }

        private boolean readsTracked(Node n) {
            for (int g = n.reads.nextSetBit(0); g >= 0; g = n.reads.nextSetBit(g + 1)) {
                if (tracked(g)) {
                    return true;
                }
            }
            return false;
        }

        /**
         * Writes a lookaround: first what makes the groups it reads right, then the lookaround, which is one step
         * for java.util.regex, so the places in it that the match can go back to are none for what follows.
         */
        private void look(Node n, BitSet live) {
            // Not the groups it sets itself: a reference in it to one of those reads what it has set by then.
            BitSet reads = (BitSet) n.reads.clone();
            reads.andNot(n.sets);
            freshen(reads, false);
            int wasAtomic = atomic;
            int wasBase = loopBase;
            int wasChoices = choices;
            int wasCopies = unrolledCopies;
            List<int[]> list = decisions;
            int at = decisionAt;
            int start = out.length();
            String wasChain = chain;
            atomic = ++atomics;
            loopBase = openLoops.size();
            chain = null;
            if (n.behind) {
                behind(n, live);
            } else {
                ahead(n, live);
            }
            // The engine is done with a lookaround before it goes on, so the copies in it are not on the way of
            // what follows.
            unrolledCopies = wasCopies;
            atomic = wasAtomic;
            loopBase = wasBase;
            chain = wasChain;
            choices = wasChoices;
            if (noted == null) {
                return;
            }
            Env before = noted;
            noted = null;
            Look look = new Look();
            look.node = n;
            look.live = live;
            look.before = before;
            look.decisions = list;
            look.from = at;
            look.start = new int[] {start};
            look.end = new int[] {out.length()};
            look.scope = scopeNow;
            look.loop = loopNow();
            offsets.add(look.start);
            offsets.add(look.end);
            for (int g = n.sets.nextSetBit(0); g >= 0; g = n.sets.nextSetBit(g + 1)) {
                if (env.name[g] != null && !env.name[g].equals(before.name[g])) {
                    env.look[g] = true;
                    env.mark[g] = choices;
                    env.body[g] = look.loop == null ? 0 : look.loop.serial;
                    env.ctx[g] = atomic;
                    env.rec[g] = look;
                }
            }
        }

        /** Makes right, where they are about to be read, the groups among {@code reads} that a lookaround set. */
        private void freshen(BitSet reads, boolean inLoop) {
            for (int g = reads.nextSetBit(0); g >= 0; g = reads.nextSetBit(g + 1)) {
                if (inLoop && replaying == 0 && tracked(g) && env.rec[g].anchor == null) {
                    // A test that stands in a loop that started after the lookaround.
                    throw new Unsupported(REFUSED_LOOKAROUND_IN_REPETITION);
                }
                fresh(g, inLoop);
            }
        }

        /**
         * Makes a group right where it is about to be read, when a lookaround set it in this iteration of a loop
         * (see the notes above {@link Look}): by another run of the lookaround when the match can have come back to
         * here past a later run of it. Where that cannot be written, the reference is counted, and the body it
         * stands in decides (see {@link #bodyText}).
         */
        private void fresh(int g, boolean inner) {
            if (replaying > 0 || !tracked(g)) {
                return;
            }
            Look look = env.rec[g];
            inner |= look.scope != scopeNow;
            if (look.anchor != null) {
                if (inner || choices > env.mark[g]) {
                    rerun(look);
                }
            } else if (!inner) {
                if (choices > env.mark[g]) {
                    rerun(look);
                }
            } else {
                unanchored++;
            }
        }

        /**
         * Before a repetition whose body reads groups of a lookaround of this iteration: gives the lookaround what
         * a reference in the body needs to run it again, and makes the groups right where the repetition starts.
         */
        private void prepare(Node body) {
            if (replaying > 0 || !readsTracked(body)) {
                return;
            }
            prepare(body.reads);
        }

        private void prepare(BitSet reads) {
            for (int g = reads.nextSetBit(0); g >= 0; g = reads.nextSetBit(g + 1)) {
                if (tracked(g)) {
                    anchor(env.rec[g]);
                    fresh(g, false);
                }
            }
        }

        /**
         * Notes how to find the place a lookaround stood at from inside a repetition that follows it: the start of
         * the iteration of its loop, and the text from there to the lookaround (see {@link #chain}), while the plan
         * the lookaround stands on is written.
         */
        private void anchor(Look look) {
            Loop mine = look.loop;
            if (look.anchor != null || mine == null || !(mine.single || mine.optional) || look.scope != scopeNow
                    || chain == null || chainScope != scopeNow) {
                return;
            }
            if (mine.stamp == null) {
                mine.stamp = newName();
            }
            look.prefix = chain + pieces(chainStart, look.start[0]);
            look.anchor = mine.stamp;
            // Running it again makes the groups it reads right first, so the lookarounds that set those too.
            for (int g = look.node.reads.nextSetBit(0); g >= 0; g = look.node.reads.nextSetBit(g + 1)) {
                if (!look.node.sets.get(g) && tracked(g)) {
                    anchor(env.rec[g]);
                }
            }
        }

        /**
         * Runs a lookaround again, at the place it stood at, and makes the groups it sets the ones the path holds.
         * The lookaround is written as it was, with the decisions of the plan it took, inside a lookbehind that
         * reaches back to that place.
         */
        private void rerun(Look look) {
            BitSet reads = (BitSet) look.node.reads.clone();
            reads.andNot(look.node.sets);
            freshen(reads, false);
            String back = null;
            if (look.anchor == null) {
                back = look.back + pieces(look.end[0], out.length());
            }
            hasLookbehind = true;
            if (back == null) {
                out.append("(?<=(?=(?=");
                refer(look.anchor);
                out.append("\\z)").append(look.prefix);
            } else {
                String rest = restOfText();
                out.append("(?<=(?=(?=").append(back);
                refer(rest);
                out.append("\\z)");
            }
            Env now = env;
            List<int[]> list = decisions;
            int at = decisionAt;
            env = look.before.copy();
            for (int g = 0; g < env.name.length; g++) {
                if (env.name[g] != null && env.look[g] && now.name[g] != null) {
                    // A group of a lookaround before this one: the group the path holds for it now.
                    env.name[g] = now.name[g];
                    env.empty[g] = now.empty[g];
                }
            }
            env.still = 0;
            decisions = look.decisions;
            decisionAt = look.from;
            replaying++;
            look(look.node, look.live);
            replaying--;
            noted = null;
            Env after = env;
            env = now;
            decisions = list;
            decisionAt = at;
            out.append(')').append(ANY).append("{0,2147483647})");
            Look again = new Look();
            again.node = look.node;
            again.live = look.live;
            again.before = look.before;
            again.decisions = look.decisions;
            again.from = look.from;
            again.start = look.start;
            again.end = new int[] {out.length()};
            again.scope = scopeNow;
            again.back = back == null ? "" : back;
            again.loop = look.loop;
            again.anchor = look.anchor;
            again.prefix = look.prefix;
            offsets.add(again.end);
            for (int g = look.node.sets.nextSetBit(0); g >= 0; g = look.node.sets.nextSetBit(g + 1)) {
                if (after.name[g] != null && !after.name[g].equals(look.before.name[g])) {
                    env.name[g] = after.name[g];
                    env.empty[g] = after.empty[g];
                    env.look[g] = true;
                    env.mark[g] = choices;
                    env.ctx[g] = atomic;
                    env.body[g] = look.loop == null ? 0 : look.loop.serial;
                    env.rec[g] = again;
                }
            }
        }

        /**
         * Writes a lookahead. One whose groups are not read after it is a scope. One whose groups are read after it
         * is written on the plan, and its groups are noted as set in a lookaround.
         *
         * <p>A lookahead takes the first way its body matches. With no decision in the body that is what
         * java.util.regex does with the body as it stands (see {@link #oneWay}). With one, the body is written by
         * ways (see {@link #firstWay}).
         */
        private void ahead(Node n, BitSet live) {
            boolean wasStrict = strict;
            if (n.negative || !n.sets.intersects(live)) {
                strict = false;
                String text = scope(n.subs[0], NOTHING, null);
                strict = wasStrict;
                out.append(n.negative ? "(?!" : "(?=").append(text).append(')');
                return;
            }
            Env before = env.copy();
            if (!n.byWays) {
                Mark mark = new Mark();
                try {
                    oneWay(n, live);
                } catch (NotOneWay e) {
                    mark.restore();
                    n.byWays = true;
                }
            }
            if (n.byWays) {
                if (wasStrict) {
                    // A lookahead around this one is being written as it stands, and this one has decisions.
                    throw new NotOneWay();
                }
                firstWay(n, live);
            }
            strict = wasStrict;
            noted = before;
        }

        /** Thrown where a lookahead that is being written as it stands turns out to have a decision. */
        private static final class NotOneWay extends Unsupported {
            private static final long serialVersionUID = 1L;

            NotOneWay() {
                super(NOT_BY_MARKERS);
            }
        }

        /** What the writer holds where a part starts that may have to be written again another way. */
        private final class Mark {
            private final int length = out.length();
            private final Env wasEnv = env.copy();
            private final List<int[]> wasDecisions = decisions;
            private final int wasDecisionAt = decisionAt;
            private final int wasDecided = decisions.size();
            private final int wasChoices = choices;
            private final int wasCopies = unrolledCopies;
            private final int wasCopied = unrolledLength;
            private final int wasLoopsOpen = openLoops.size();
            private final int wasLoopBase = loopBase;
            private final String wasChain = chain;
            private final int wasChainStart = chainStart;
            private final int wasChainScope = chainScope;
            private final int wasAtomic = atomic;
            private final int wasReplaying = replaying;
            private final int wasScope = scopeNow;
            private final boolean wasStrict = strict;
            private final BitSet wasLookLive = lookLive;
            private final int wasIterations = iterations;
            private final int wasOffsets = offsets.size();
            private final BitSet wasInside = inside;
            private final BitSet wasInvolved = involvedInside;
            private final Map<Integer, long[]> wasPlaces = places;
            private final long wasAt = at;
            private final boolean wasCaptured = captured;
            private final boolean wasReadAfter = readAfter;

            void restore() {
                out.setLength(length);
                env = wasEnv;
                decisions = wasDecisions;
                decisionAt = wasDecisionAt;
                while (decisions.size() > wasDecided) {
                    decisions.remove(decisions.size() - 1);
                }
                choices = wasChoices;
                unrolledCopies = wasCopies;
                unrolledLength = wasCopied;
                while (openLoops.size() > wasLoopsOpen) {
                    openLoops.remove(openLoops.size() - 1);
                }
                loopBase = wasLoopBase;
                chain = wasChain;
                chainStart = wasChainStart;
                chainScope = wasChainScope;
                atomic = wasAtomic;
                replaying = wasReplaying;
                noted = null;
                while (offsets.size() > wasOffsets) {
                    offsets.remove(offsets.size() - 1);
                }
                scopeNow = wasScope;
                strict = wasStrict;
                lookLive = wasLookLive;
                iterations = wasIterations;
                inside = wasInside;
                involvedInside = wasInvolved;
                places = wasPlaces;
                at = wasAt;
                captured = wasCaptured;
                readAfter = wasReadAfter;
            }
        }

        /**
         * Writes a lookahead whose groups are read after it with its body as it stands, which is right when the
         * body has no decision: its first match in java.util.regex is then its first match in ECMA-262. Alternatives
         * of the whole body are decisions of the plan, each written after a negative lookahead for each alternative
         * before it. Throws {@link NotOneWay} at any other decision.
         */
        private void oneWay(Node n, BitSet live) {
            Node body = n.subs[0];
            if (body.kind == ALT) {
                // The first alternative that matches is the one taken. One that matches the empty string anywhere
                // is taken before any after it.
                int ways = 1;
                while (ways < body.subs.length && !emptyAnywhere(body.subs[ways - 1])) {
                    ways++;
                }
                int way = ways > 1 ? choose(ways) : 0;
                strict = false;
                for (int k = 0; k < way; k++) {
                    out.append("(?!").append(scope(body.subs[k], NOTHING, null)).append(')');
                }
                body = body.subs[way];
            }
            strict = true;
            out.append("(?=");
            p(body, live);
            out.append(')');
        }

        // A lookahead by ways.
        //
        // A lookahead takes the first way its body matches, in the order ECMA-262 tries them, and the match never
        // goes back into it. When the ways differ in what they set of the groups read after the lookahead, the plans
        // of the body cannot simply be alternatives: each would match where its own way matches, which need not be
        // the first way. So in such a body every choice is a decision of the plan, and where a plan does not take
        // the way ECMA-262 tries first, it is written after a test that the ways tried before it fail: a negative
        // lookahead that holds each such way followed by all that remains of the lookahead (see #guard). A path that
        // passes every test of its plan is the first match of the body, or differs from it only after the last place
        // that sets a group read after the lookahead. So whichever plans match, they hold the same groups.
        //
        // The choices are these. Of alternatives, one. Of a repetition, each iteration it must run is written apart
        // (it may match nothing). Past the minimum, an iteration or none: one more is a copy of the body, and the
        // rest follows the same way. A repetition with no upper bound becomes a loop once every iteration that is
        // open has moved, with a test after it that one more iteration fails (and a lazy one with a test before each
        // iteration that stopping fails). Its body is the plans of one iteration as alternatives, which the tests in
        // them keep apart. When its groups are read after it, the iteration that runs last is written apart.
        //
        // An iteration past the minimum must not match the empty string. Whether one has moved is known along the
        // plan: a character moves, a part that matches nothing does not, and what may do either (a backreference, a
        // repetition) is a decision. So where a test is written it is known, of each iteration that is open, whether
        // it has moved, and the test asks of the others that the place they end at is not the place it stands at.
        //
        // A plan on which an iteration past the minimum wrote only parts that match nothing is not a way ECMA-262
        // takes, and is left out (see DeadPlan). Without that a body that can match nothing gives two plans for
        // each iteration, one of them dead, each with the iterations after it, and the text doubles with the count.
        //
        // An iteration that another iteration follows is written apart from the plan, as a scope of its own (see
        // #part): the next iteration unsets what it sets, so nothing after it reads that, and its plans need not be
        // plans of the lookahead. Whether another follows is a decision taken where the iteration is written. So a
        // repetition of n iterations whose groups are read after it gives n plans for how many run, each with the
        // plans of its last iteration, and not a plan for each way through all of them.
        //
        // Where nothing from a place to the end of the lookahead sets a group read after it, the rest is written as
        // any other part of a plan. No test is needed there: the ways differ in nothing that is read.

        /** The groups of the lookahead being written by ways that are read after it. */
        private BitSet lookLive = NOTHING;
        /** The iterations open in it that may match nothing. */
        private int iterations;
        /** Where the text of each group that is open in it starts in the translation. */
        private final List<int[]> offsets = new ArrayList<>();

        /** What follows a node in a lookahead written by ways, up to the end of the lookahead. */
        private static final class Rest {
            final Node node;
            final Rest next;
            /** The groups set, and the groups read, from here to the end of the lookahead. */
            final BitSet sets;
            final BitSet reads;

            Rest(Node node, Rest next) {
                this.node = node;
                this.next = next;
                this.sets = (BitSet) node.sets.clone();
                this.reads = (BitSet) node.reads.clone();
                if (next != null) {
                    sets.or(next.sets);
                    reads.or(next.reads);
                }
            }
        }

        private static Node made(int kind, Node... subs) {
            Node n = new Node(kind);
            n.subs = subs;
            n.sets = new BitSet();
            n.reads = new BitSet();
            for (Node sub : subs) {
                n.sets.or(sub.sets);
                n.reads.or(sub.reads);
            }
            return n;
        }

        private static Node raw(String text) {
            Node n = made(RAW);
            n.text = text;
            return n;
        }

        /** Writes a lookahead that captures the rest of the text in a new group, and returns the group. */
        private String restOfText() {
            String name = newName();
            out.append("(?=(?<").append(name).append('>').append(ANY).append("*))");
            return name;
        }

        /** Writes a lookahead whose groups are read after it, by ways. */
        private void firstWay(Node n, BitSet live) {
            BitSet wasLive = lookLive;
            int wasIterations = iterations;
            int wasStill = env.still;
            boolean wasUnsure = env.unsure;
            lookLive = both(n.sets, live);
            iterations = 0;
            env.still = 0;
            strict = false;
            out.append("(?=");
            way(n.subs[0], null, live);
            out.append(')');
            lookLive = wasLive;
            iterations = wasIterations;
            env.still = wasStill;
            env.unsure = wasUnsure;
        }

        /**
         * Writes a node of a lookahead written by ways, on the plan being written. {@code rest} is what follows the
         * node in the lookahead, and {@code live} holds the groups read after the node.
         */
        private void way(Node n, Rest rest, BitSet live) {
            if (!n.sets.intersects(lookLive) && (rest == null || !rest.sets.intersects(lookLive))) {
                if (env.still > 0 && maxLength(n) != 0) {
                    env.unsure = true;
                }
                p(n, live);
                return;
            }
            switch (n.kind) {
                case CAT: {
                    int count = n.subs.length;
                    BitSet[] after = new BitSet[count];
                    Rest[] following = new Rest[count];
                    BitSet later = live;
                    Rest next = rest;
                    for (int k = count - 1; k >= 0; k--) {
                        after[k] = later;
                        following[k] = next;
                        if (!n.subs[k].reads.isEmpty()) {
                            later = (BitSet) later.clone();
                            later.or(n.subs[k].reads);
                        }
                        next = new Rest(n.subs[k], next);
                    }
                    for (int k = 0; k < count; k++) {
                        way(n.subs[k], following[k], after[k]);
                    }
                    break;
                }
                case ALT: {
                    if (sameAfter(n, rest, live)) {
                        // Each alternative after the tests that the ones before it fail.
                        String since = chainHere();
                        out.append("(?:");
                        for (int k = 0; k < n.subs.length; k++) {
                            int taken = k;
                            out.append(k > 0 ? "|" : "").append(alternative(() -> {
                                for (int j = 0; j < taken; j++) {
                                    guard(n.subs[j], rest);
                                }
                                way(n.subs[taken], rest, live);
                            }, since));
                        }
                        out.append(')');
                        choices++;
                        break;
                    }
                    int taken = choose(n.subs.length);
                    for (int k = 0; k < taken; k++) {
                        guard(n.subs[k], rest);
                    }
                    out.append("(?:");
                    way(n.subs[taken], rest, live);
                    out.append(')');
                    break;
                }
                case GROUP:
                    if (n.group != 0 && read.get(n.group)) {
                        Node close = made(CLOSE);
                        close.group = n.group;
                        close.sets.set(n.group);
                        boolean readLater = false;
                        for (Rest r = rest; r != null && !readLater; r = r.next) {
                            readLater = r.node.reads.get(n.group);
                        }
                        Loop loop = loopNow();
                        if (readLater && loop == null) {
                            // A test in the group may have to say what the group holds.
                            close.text = restOfText();
                        } else if (readLater && chain != null && chainScope == scopeNow) {
                            // In an iteration of a loop, a group set here in a lookahead is not restored when the
                            // match goes back into the iteration. The place is told from the start of the iteration.
                            if (loop.stamp == null) {
                                loop.stamp = newName();
                            }
                            close.anchor = loop.stamp;
                            close.prefix = chain + pieces(chainStart, out.length());
                        } else {
                            close.looped = readLater;
                        }
                        String name = newName();
                        out.append("(?<").append(name).append('>');
                        close.from = new int[] {out.length()};
                        close.scope = scopeNow;
                        offsets.add(close.from);
                        way(n.subs[0], new Rest(close, rest), live);
                        offsets.remove(close.from);
                        set(n.group, name, close.from[0]);
                        out.append(')');
                    } else {
                        out.append("(?:");
                        way(n.subs[0], rest, live);
                        out.append(')');
                    }
                    break;
                case REPEAT:
                    repeatedWay(n, rest, live);
                    break;
                case LOOK:
                    look(n, live);
                    break;
                case BACKREF:
                    referenceWay(n);
                    break;
                case CHAR:
                case SET:
                    node(n);
                    env.still = 0;
                    break;
                default:
                    node(n);
                    break;
            }
        }

        /**
         * Whether the plan is the same after a choice whichever way is taken: nothing the node sets is read after
         * it, and every iteration that is open has moved. The ways of such a choice are written as alternatives in
         * the plan, each with its tests, and are not decisions of the plan, which keeps the plans few. Not where the
         * node reads a group of a lookaround of this iteration of a loop: the alternatives of the translation would
         * be places the match can come back to (see {@link #fresh}), and nor where what follows reads one, as the
         * tests of the alternatives hold what follows.
         */
        private boolean sameAfter(Node n, Rest rest, BitSet live) {
            if (env.still != 0 || n.sets.intersects(live) || readsTracked(n)) {
                return false;
            }
            for (Rest r = rest; r != null; r = r.next) {
                if (readsTracked(r.node)) {
                    return false;
                }
            }
            return true;
        }

        /** The {@link #chain} of a scope that starts here, or null when none is known. */
        private String chainHere() {
            return chain == null || chainScope != scopeNow ? null : chain + pieces(chainStart, out.length());
        }

        /** How many plans the scope that {@link #alternative} wrote last has. */
        private int alternatives;

        /** The text a writer writes, as a scope of its own that starts here: its plans as alternatives. */
        private String alternative(Writer writer, String since) {
            String wasChain = chain;
            int wasStart = chainStart;
            int wasScope = chainScope;
            int wasStill = env.still;
            List<String> plans = plans(() -> {
                chain = since;
                chainStart = out.length();
                chainScope = scopeNow;
                writer.write();
            }, null);
            chain = wasChain;
            chainStart = wasStart;
            chainScope = wasScope;
            env.still = wasStill;
            alternatives = plans.size();
            return plans.size() == 1 ? plans.get(0) : joined(plans);
        }

        /**
         * Writes a backreference in a lookahead written by ways. While an iteration is open that has matched
         * nothing so far, whether the backreference matches something is a decision of the plan, written with a
         * test of the group: it holds the empty string when it is all of the text up to where the rest starts.
         */
        private void referenceWay(Node n) {
            int group = groupRead(n);
            if (env.still == 0 || group == 0 || env.empty[group] || maxLength(groupNodes[group].subs[0]) == 0) {
                reference(n);
                return;
            }
            boolean moves = choose(2) == 0;
            if (moves) {
                reference(n);
            }
            used.set(group);
            String rest = restOfText();
            out.append(moves ? "(?!" : "(?=");
            refer(env.name[group]);
            refer(rest);
            out.append("\\z)");
            if (moves) {
                env.still = 0;
            }
        }

        private void countCopy() {
            if (++unrolledCopies > MAX_UNROLLED_COPIES) {
                throw new TooLarge(REFUSED_TOO_MANY_PATHS, LIMIT_COPIES);
            }
        }

        /** The repetition of the body of {@code n} with other bounds, or an empty node when it runs no times. */
        private static Node again(Node n, int min, int max) {
            if (max == 0) {
                return made(EMPTY);
            }
            if (min == n.min && max == n.max) {
                return n;
            }
            Node r = made(REPEAT, n.subs[0]);
            r.min = min;
            r.max = max;
            r.lazy = n.lazy;
            r.firstGroup = n.firstGroup;
            r.lastGroup = n.lastGroup;
            return r;
        }

        /** Writes a repetition in a lookahead written by ways (see the notes above {@link #lookLive}). */
        private void repeatedWay(Node n, Rest rest, BitSet live) {
            Node body = n.subs[0];
            if (n.max == 0) {
                return;
            }
            prepare(body);
            if (rest != null && replaying == 0) {
                // The tests in a loop hold what follows it.
                prepare(rest.reads);
            }
            boolean decided = n == decidedFor;
            if (decided) {
                decidedFor = null;
            }
            boolean apart = apart(n, rest);
            if (n.min > 0) {
                // An iteration that is due. It may match nothing.
                Node again = again(n, n.min - 1, n.max == UNBOUNDED ? UNBOUNDED : n.max - 1);
                countCopy();
                Rest after = again.kind == EMPTY ? rest : new Rest(again, rest);
                Writer due = () -> {
                    unset(body.sets);
                    out.append("(?:");
                    way(body, after, live);
                    out.append(')');
                };
                if (apart && (!n.sets.intersects(live) || (again.kind == REPEAT && again.min > 0))) {
                    // Nothing reads what the iteration sets: nothing after the repetition does, or another
                    // iteration follows, which unsets it.
                    BitSet unread = without(live, body.sets);
                    part(() -> {
                        unset(body.sets);
                        out.append("(?:");
                        way(body, after, unread);
                        out.append(')');
                    });
                } else if (apart && again.kind == REPEAT && again.max != UNBOUNDED) {
                    // The last iteration that is due. Whether another follows is decided here, so that it is
                    // written apart when one does.
                    boolean more = (choose(2) == 0) != n.lazy;
                    if (more && n.lazy) {
                        part(() -> {
                            due.write();
                            guard(null, rest);
                        });
                    } else if (more) {
                        BitSet unread = without(live, body.sets);
                        part(() -> {
                            unset(body.sets);
                            out.append("(?:");
                            way(body, after, unread);
                            out.append(')');
                        });
                    } else {
                        due.write();
                    }
                    decidedFor = again;
                    decidedRuns = more;
                } else {
                    due.write();
                }
                way(again, rest, live);
                return;
            }
            boolean unbounded = n.max == UNBOUNDED;
            if (!unbounded || env.still > 0) {
                // One more iteration and then the rest of them, or none. A greedy repetition tries the iteration
                // first, and a lazy one tries to stop first.
                Node again = unbounded ? n : again(n, 0, n.max - 1);
                if (!decided && sameAfter(n, rest, live)) {
                    String since = chainHere();
                    String one = alternative(() -> {
                        if (n.lazy) {
                            guard(null, rest);
                        }
                        // The iteration is a scope of its own, so that its plans do not each hold the iterations
                        // after it.
                        part(() -> iteration(body, again, rest, live));
                        way(again, rest, live);
                    }, since);
                    String none = n.lazy ? "" : alternative(() -> guard(made(CAT, made(ITERATION, body), again), rest), since);
                    out.append("(?:").append(n.lazy ? none : one).append('|').append(n.lazy ? one : none).append(')');
                    choices++;
                    return;
                }
                boolean runs = decided ? decidedRuns : (choose(2) == 0) != n.lazy;
                if (runs) {
                    if (n.lazy && !decided) {
                        guard(null, rest);
                    }
                    if (apart && !unbounded && again.kind == REPEAT) {
                        // Whether another iteration follows this one is decided here. When one does, this one is
                        // written apart: the next unsets what it sets, so only the tests in it read that.
                        boolean more = (choose(2) == 0) != n.lazy;
                        if (more) {
                            // In a lazy repetition the test that stopping after it fails reads what it sets.
                            BitSet readAfter = n.lazy ? live : without(live, body.sets);
                            part(() -> {
                                iteration(body, again, rest, readAfter);
                                if (n.lazy) {
                                    guard(null, rest);
                                }
                            });
                        } else {
                            iteration(body, again, rest, live);
                        }
                        decidedFor = again;
                        decidedRuns = more;
                    } else {
                        iteration(body, again, rest, live);
                    }
                    way(again, rest, live);
                } else if (!n.lazy) {
                    guard(made(CAT, made(ITERATION, body), again), rest);
                }
                return;
            }
            // The groups of its body are read after it: the iteration that runs last is written apart.
            boolean last = n.sets.intersects(live);
            if (!n.lazy) {
                if (!last || choose(2) == 0) {
                    out.append(loopOfWays(body, n, rest, false, false, false));
                    choices++;
                    if (last) {
                        iteration(body, n, rest, live);
                    }
                }
                guard(made(CAT, made(ITERATION, body), n), rest);
            } else if (!last) {
                out.append(loopOfWays(body, n, rest, true, false, true));
                choices++;
            } else if (choose(2) == 1) {
                // What follows the repetition reads the groups of the iteration before it. So the test that
                // stopping fails stands where the repetition starts and at the end of each iteration but the last.
                guard(null, rest);
                out.append(loopOfWays(body, n, rest, false, true, true));
                choices++;
                iteration(body, n, rest, live);
            }
        }

        /**
         * The rest of a repetition for which {@link #repeatedWay} has decided, where it wrote the iteration before,
         * whether it runs another iteration ({@link #decidedRuns}), or null.
         */
        private Node decidedFor;
        private boolean decidedRuns;

        /**
         * Whether an iteration of a repetition can be written apart from the plan, as a scope of its own (see
         * {@link #part}): every iteration that is open has moved, and neither the repetition nor what follows reads
         * a group of a lookaround of this iteration of a loop (see {@link #sameAfter}).
         */
        private boolean apart(Node n, Rest rest) {
            if (env.still != 0 || readsTracked(n)) {
                return false;
            }
            for (Rest r = rest; r != null; r = r.next) {
                if (readsTracked(r.node)) {
                    return false;
                }
            }
            return true;
        }

        /**
         * Writes a part of a plan as a scope of its own that starts here: its plans as alternatives, each with its
         * tests. What the part sets is forgotten after it, so it is for a part whose groups nothing after it reads.
         * The plans of the part then add to the text of the plan and do not multiply the plans of the scope around
         * it. A part that no plan of it can match makes the plan one that cannot match.
         */
        private void part(Writer writer) {
            String text = alternative(writer, chainHere());
            if (text.equals(NEVER_MATCHES)) {
                throw DeadPlan.INSTANCE;
            }
            out.append(text);
            if (alternatives > 1) {
                choices++;
            }
            env.still = 0;
        }

        /**
         * Writes, on the plan, one iteration that is past the minimum of its repetition, which {@code again} is the
         * rest of. It must not match the empty string.
         */
        private void iteration(Node body, Node again, Rest rest, BitSet live) {
            countCopy();
            unset(body.sets);
            Rest after = again.kind == EMPTY ? rest : new Rest(again, rest);
            if (minLength(body) > 0) {
                out.append("(?:");
                way(body, after, live);
                out.append(')');
            } else {
                movingIteration(body, after, live);
            }
            env.still = 0;
        }

        /**
         * Writes an iteration of a body that can match the empty string, inside a group that holds what it matched.
         * When the plan does not show that it moved, a test follows that the group does not hold the empty string.
         */
        private void movingIteration(Node body, Rest after, BitSet live) {
            Node moved = made(MOVED);
            moved.min = ++iterations;
            env.still++;
            env.unsure = false;
            String taken = newName();
            out.append("(?<").append(taken).append('>');
            way(body, new Rest(moved, after), live);
            out.append(')');
            if (env.still > 0) {
                if (!env.unsure) {
                    throw DeadPlan.INSTANCE;
                }
                String rest = restOfText();
                out.append("(?!");
                refer(taken);
                refer(rest);
                out.append("\\z)");
            }
            iterations--;
        }

        /**
         * A loop in a lookahead written by ways. Its body is the plans of one iteration, as alternatives. Every
         * iteration that was open where the loop starts has moved, so the tests in the body are the same in each
         * iteration. With {@code guarded}, each iteration starts with the test that stopping before it fails, and
         * with {@code stops}, each ends with the test that stopping after it fails. A test in the body may need the
         * place the iteration started at (see {@link #guard} and {@link #stamped}).
         */
        private String loopOfWays(Node body, Node again, Rest rest, boolean guarded, boolean stops, boolean lazy) {
            Loop mine = open(true);
            int references = unanchored;
            int wasChoices = choices;
            String wasChain = chain;
            int wasStart = chainStart;
            int wasScope = chainScope;
            Rest after = new Rest(again, rest);
            // What an iteration sets is read by what follows the loop when the loop stops after it, which the tests
            // in the body ask about.
            BitSet live = after.reads;
            List<String> plans = plans(() -> {
                mine.scope = scopeNow;
                chain = "";
                chainStart = out.length();
                chainScope = scopeNow;
                if (guarded) {
                    guard(null, rest, true);
                }
                if (minLength(body) > 0) {
                    way(body, after, live);
                } else {
                    movingIteration(body, after, live);
                }
                if (stops) {
                    guard(null, rest, true);
                }
            }, body.sets);
            chain = wasChain;
            chainStart = wasStart;
            chainScope = wasScope;
            openLoops.remove(openLoops.size() - 1);
            if (unanchored != references && (plans.size() != 1 || choices != wasChoices || lazy)) {
                throw new Unsupported(REFUSED_LOOKAROUND_IN_REPETITION);
            }
            String text = plans.size() == 1 ? "(?:" + plans.get(0) + ")" : joined(plans);
            String quantifier = lazy ? "*?" : "*";
            if (mine.stamp == null) {
                return text + quantifier;
            }
            text = "(?:" + stamped(mine, text) + ")" + quantifier;
            return mine.single ? text : text + "(?=\\k<" + mine.stamp + ">\\z)";
        }

        /**
         * Writes the test that a way the plan does not take fails: a negative lookahead that holds {@code first}
         * (when not null) and then all that remains of the lookahead. It is a scope: only whether it matches counts.
         *
         * <p>Of what remains, the end of an iteration that has not moved becomes a test that the place is not the
         * one the lookahead of the test stands at. The end of a group that is open, when something after it reads
         * the group, sets a group of the translation to the text from where the group started (see {@link #close}).
         */
        private void guard(Node first, Rest rest) {
            guard(first, rest, false);
        }

        /** As {@link #guard(Node, Rest)}, for a test that stands in a loop when {@code inLoop}. */
        private void guard(Node first, Rest rest, boolean inLoop) {
            List<Node> items = new ArrayList<>();
            String here = null;
            if (first != null) {
                items.add(first);
            }
            for (Rest r = rest; r != null; r = r.next) {
                Node item = r.node;
                if (item.kind == MOVED) {
                    if (item.min > iterations - env.still) {
                        here = here != null ? here : newName();
                        items.add(raw("(?!\\k<" + here + ">\\z)"));
                    }
                } else if (item.kind != CLOSE) {
                    items.add(item);
                } else if (item.text != null || item.anchor != null || item.looped) {
                    if (item.scope == scopeNow && !inLoop) {
                        // The text the group has matched so far, in groups of the plan, and then the rest of the text
                        // from here: together they match only from where the group started.
                        here = here != null ? here : newName();
                        Node close = made(CLOSE);
                        close.group = item.group;
                        close.sets.set(item.group);
                        close.pieces = true;
                        close.text = pieces(item.from[0], out.length()) + "\\k<" + here + ">\\z";
                        items.add(close);
                    } else if (!item.looped) {
                        items.add(item);
                    } else {
                        // The group started in an iteration of a loop that is inside another loop of the lookahead,
                        // and the test is in a loop inside the group. The place the group started at is not known
                        // here: java.util.regex does not restore a group set in a lookahead when the match goes
                        // back into the iteration.
                        throw new Unsupported(REFUSED_LOOKAROUND_IN_REPETITION);
                    }
                }
            }
            Node all = made(CAT, items.toArray(NO_NODES));
            freshen(all.reads, inLoop);
            boolean wasStrict = strict;
            int wasAtomic = atomic;
            int wasBase = loopBase;
            int wasChoices = choices;
            String wasChain = chain;
            strict = false;
            atomic = ++atomics;
            loopBase = openLoops.size();
            chain = null;
            out.append("(?!");
            if (here != null) {
                out.append("(?=(?<").append(here).append('>').append(ANY).append("*))");
            }
            int wasCopies = unrolledCopies;
            out.append(scope(all, NOTHING, null)).append(')');
            unrolledCopies = wasCopies;
            strict = wasStrict;
            atomic = wasAtomic;
            loopBase = wasBase;
            chain = wasChain;
            choices = wasChoices;
        }

        /**
         * Writes the end of a group that was open where a test of what remains was written: a group of the
         * translation is set to the text from where the group started to here, by a lookbehind that reaches back to
         * that place, which is known by the rest of the text from it.
         */
        private void close(Node n) {
            String name = newName();
            hasLookbehind = true;
            String here = restOfText();
            out.append("(?<=(?=(?=");
            if (n.pieces) {
                out.append(n.text).append(")(?<").append(name).append('>').append(ANY).append("*?)");
            } else if (n.anchor != null) {
                refer(n.anchor);
                out.append("\\z)").append(n.prefix).append("(?<").append(name).append('>').append(ANY).append("*?)");
            } else {
                refer(n.text);
                out.append("\\z)(?<").append(name).append('>').append(ANY).append("*?)");
            }
            refer(here);
            out.append("\\z)").append(ANY).append("{0,2147483647})");
            env.name[n.group] = name;
            env.look[n.group] = false;
            env.empty[n.group] = false;
            env.body[n.group] = loopNow() == null ? 0 : loopNow().serial;
        }

        /** Writes one more iteration of a repetition whose minimum is met, in a test of what remains. */
        private void oneMore(Node n, BitSet live) {
            Node body = n.subs[0];
            unset(body.sets);
            if (minLength(body) > 0) {
                out.append("(?:");
                p(body, live);
                out.append(')');
                return;
            }
            String taken = newName();
            out.append("(?<").append(taken).append('>');
            p(body, live);
            out.append(')');
            String rest = restOfText();
            out.append("(?!");
            refer(taken);
            refer(rest);
            out.append("\\z)");
        }

        /**
         * Puts the text written from {@code from} on into groups, and returns backreferences to them in order, which
         * match the text it has matched. The text is cut where a parenthesis stands that is not closed, or was not
         * opened, in it, and each stretch between is one group.
         */
        private String pieces(int from, int to) {
            List<Integer> open = new ArrayList<>();
            List<int[]> cuts = new ArrayList<>();
            for (int i = from; i < to;) {
                char c = out.charAt(i);
                if (c == '\\') {
                    i += 2;
                } else if (c == '[') {
                    i = classEnd(i);
                } else {
                    if (c == '(') {
                        open.add(i);
                    } else if (c == ')') {
                        if (open.isEmpty()) {
                            cuts.add(new int[] {i, i + 1});
                        } else {
                            open.remove(open.size() - 1);
                        }
                    }
                    i++;
                }
            }
            for (int at : open) {
                cuts.add(new int[] {at, at + header(at)});
            }
            cuts.add(new int[] {to, to});
            List<int[]> runs = new ArrayList<>();
            int start = from;
            for (int[] cut : cuts) {
                if (cut[0] > start) {
                    runs.add(new int[] {start, cut[0]});
                } else if (cut[0] < start) {
                    throw new IllegalStateException("a group of a translation that is not one to cut at");
                }
                start = cut[1];
            }
            String[] names = new String[runs.size()];
            StringBuilder text = new StringBuilder();
            for (int k = 0; k < names.length; k++) {
                names[k] = newName();
                text.append("\\k<").append(names[k]).append('>');
            }
            for (int k = names.length - 1; k >= 0; k--) {
                int[] run = runs.get(k);
                if ("*+?{|".indexOf(out.charAt(run[0])) >= 0) {
                    throw new IllegalStateException("a stretch of a translation that is not a term");
                }
                insert(run[1], ")");
                insert(run[0], "(?<" + names[k] + ">");
            }
            return text.toString();
        }

        /** The place after the class that starts at {@code i} in the translation. */
        private int classEnd(int i) {
            return classEnd(out, i);
        }

        /** The length of what opens the group at {@code at} in the translation. */
        private int header(int at) {
            if (out.charAt(at + 1) != '?') {
                return 1;
            }
            if (out.charAt(at + 2) == ':') {
                return 3;
            }
            if (out.charAt(at + 2) != '<' || out.charAt(at + 3) == '=' || out.charAt(at + 3) == '!') {
                throw new IllegalStateException("a group of a translation that is not one to cut at");
            }
            return out.indexOf(">", at) + 1 - at;
        }

        /** Inserts text in the translation, and moves the places noted after it. */
        private void insert(int at, String text) {
            out.insert(at, text);
            for (int[] offset : offsets) {
                if (offset[0] > at) {
                    offset[0] += text.length();
                }
            }
        }

        /**
         * Writes a lookbehind.
         *
         * <p>A lookbehind none of whose groups is read, in it or after it, holds where its body matches some stretch
         * of the text that ends there, whichever way the body is run. It is written forwards (see
         * {@link #lookbehind}), in the form that searches for its start when it has a backreference, whose length
         * java.util.regex cannot bound.
         *
         * <p>ECMA-262 runs a lookbehind backwards, from its last term to its first. That shows in what its groups
         * capture, and a backreference in it reads a group that stands after it. Such a lookbehind is written when
         * what it captures can be told without running it backwards (see {@link #edge}), and is refused otherwise.
         * Its alternatives are tried in order whichever way it runs, so each is written as a lookbehind of its own,
         * after negative lookbehinds for the ones before it.
         */
        private void behind(Node n, BitSet live) {
            lookbehinds++;
            try {
                writeBehind(n, live);
            } finally {
                lookbehinds--;
            }
        }

        /** The lookbehinds being written, one inside another. */
        private int lookbehinds;

        private void writeBehind(Node n, BitSet live) {
            Node body = n.subs[0];
            BitSet outside = n.negative ? NOTHING : both(body.sets, live);
            BitSet involved = both(body.sets, body.reads);
            involved.or(outside);
            hasLookbehind = true;
            if (involved.isEmpty()) {
                boolean wasStrict = strict;
                strict = false;
                String text = scope(body, NOTHING, null);
                strict = wasStrict;
                wrapBehind(n.negative, text, body.reads.isEmpty() && maxLength(body) <= MAX_DIRECT_LOOKBEHIND);
                return;
            }
            BitSet wasInside = inside;
            BitSet wasInvolved = involvedInside;
            Map<Integer, long[]> wasPlaces = places;
            long wasAt = at;
            boolean wasCaptured = captured;
            boolean wasReadAfter = readAfter;
            captured = false;
            readAfter = !outside.isEmpty();
            inside = body.sets;
            involvedInside = involved;
            Node[] alternatives = body.kind == ALT ? body.subs : new Node[] {body};
            if (n.negative) {
                for (Node alternative : alternatives) {
                    notBehind(alternative);
                }
            } else if (outside.isEmpty()) {
                // No group is read after it, so which alternative matched does not matter.
                out.append("(?:");
                for (int k = 0; k < alternatives.length; k++) {
                    Node alternative = alternatives[k];
                    List<String> plans = plans(() -> edge(alternative, NOTHING), null);
                    for (int w = 0; w < plans.size(); w++) {
                        out.append(k + w > 0 ? "|" : "");
                        wrapBehind(false, plans.get(w), bounded(alternative));
                    }
                }
                out.append(')');
            } else {
                Env before = env.copy();
                int way = alternatives.length > 1 ? choose(alternatives.length) : 0;
                for (int k = 0; k < way; k++) {
                    notBehind(alternatives[k]);
                }
                int start = out.length();
                edge(alternatives[way], live);
                String text = out.substring(start);
                out.setLength(start);
                wrapBehind(false, text, bounded(alternatives[way]));
                noted = before;
            }
            captured = wasCaptured;
            readAfter = wasReadAfter;
            inside = wasInside;
            involvedInside = wasInvolved;
            places = wasPlaces;
            at = wasAt;
        }

        private boolean bounded(Node body) {
            BitSet others = (BitSet) body.reads.clone();
            others.andNot(inside);
            return others.isEmpty() && maxLength(body) <= MAX_DIRECT_LOOKBEHIND;
        }

        /** Writes negative lookbehinds that hold where the node, run as ECMA-262 runs a lookbehind, does not match. */
        private void notBehind(Node body) {
            for (String plan : plans(() -> edge(body, NOTHING), null)) {
                wrapBehind(true, plan, bounded(body));
            }
        }

        private void wrapBehind(boolean negative, String text, boolean bounded) {
            if (!searchingLookbehind && bounded) {
                out.append(negative ? "(?<!" : "(?<=").append(text).append(')');
                return;
            }
            String rest = newName();
            out.append("(?=(?<").append(rest).append('>').append(ANY).append("*))").append(negative ? "(?<!" : "(?<=")
                    .append("(?=(?:").append(text).append(')');
            refer(rest);
            out.append("\\z)").append(ANY).append("{0,2147483647})");
        }

        /** The groups of the lookbehind being written, and those of them that are read, in it or after it. */
        private BitSet inside;
        private BitSet involvedInside;
        /** Whether a group of that lookbehind is read after it. */
        private boolean readAfter;
        /** Where each group of a stretch of fixed length starts, and its length, counted from a common origin. */
        private Map<Integer, long[]> places;
        /** The place the text being written has reached, from the same origin. */
        private long at;

        /** Inside a group of the lookbehind that is read after it, where what a node matches is what is captured. */
        private boolean captured;

        private boolean uninvolved(Node n) {
            return !n.sets.intersects(involvedInside) && !n.reads.intersects(inside);
        }

        /** The one group of the lookbehind a backreference in it reads, or 0 when that is not one group for sure. */
        private int target(Node ref) {
            int target = 0;
            for (int k = 0; k < ref.groups.length; k++) {
                if (ref.statuses[k] != NEVER) {
                    if (target != 0 || ref.statuses[k] != DEFINITE || !inside.get(ref.groups[k])) {
                        return 0;
                    }
                    target = ref.groups[k];
                }
            }
            return target;
        }

        /**
         * The length of a node of a lookbehind when the node is rigid, and -1 when it is not. A rigid node matches
         * texts of one length only, and what its groups capture does not depend on the direction it is run in: it
         * is made of characters, classes, assertions, groups, repetitions of a fixed count, backreferences to rigid
         * groups of the lookbehind, and parts of fixed length that hold no group that is read and no backreference
         * to a group of the lookbehind.
         */
        private long rigid(Node n) {
            if (uninvolved(n)) {
                return minLength(n) == maxLength(n) ? minLength(n) : -1;
            }
            switch (n.kind) {
                case GROUP:
                    return rigid(n.subs[0]);
                case CAT: {
                    long sum = 0;
                    for (Node sub : n.subs) {
                        long length = rigid(sub);
                        if (length < 0) {
                            return -1;
                        }
                        sum += length;
                    }
                    return sum;
                }
                case REPEAT: {
                    long length = rigid(n.subs[0]);
                    return n.min == n.max && length >= 0 && n.min <= MAX_UNROLLED_COPIES ? length * n.min : -1;
                }
                case BACKREF: {
                    int target = target(n);
                    return target == 0 ? -1 : rigid(groupNodes[target].subs[0]);
                }
                default:
                    return -1;
            }
        }

        /** Notes where the groups of a rigid node start when the node starts at {@code from}. */
        private void place(Node n, long from) {
            if (n.kind == GROUP && n.group != 0) {
                places.put(n.group, new long[] {from, rigid(n.subs[0])});
            }
            if (n.kind == CAT) {
                for (Node sub : n.subs) {
                    place(sub, from);
                    from += Math.max(rigid(sub), 0);
                }
            } else if (n.kind == GROUP || (n.kind == REPEAT && !uninvolved(n))) {
                // Of a repetition, the first copy: the last iteration of a lookbehind is the leftmost.
                place(n.subs[0], from);
            }
        }

        /**
         * Writes a rigid node of a lookbehind, forwards. A group read after the lookbehind is a group of the
         * translation. A backreference to a group of the lookbehind, which stands further on at a known distance d,
         * is written as a test that the text here is the text d characters on, {@code (?=(A{n})A{d-n}\1)A{n}} for a
         * group of n characters. A repetition of a fixed count is written as its copies, of which the first holds the
         * groups that are read after it, as it is the iteration ECMA-262 runs last.
         */
        private void writeRigid(Node n, BitSet live) {
            if (uninvolved(n)) {
                boolean wasStrict = strict;
                strict = false;
                out.append("(?:").append(scope(n, NOTHING, null)).append(')');
                strict = wasStrict;
                at += minLength(n);
                return;
            }
            switch (n.kind) {
                case GROUP:
                    if (n.group != 0 && live.get(n.group)) {
                        String name = newName();
                        out.append("(?<").append(name).append('>');
                        int from = out.length();
                        writeRigid(n.subs[0], live);
                        set(n.group, name, from);
                        out.append(')');
                    } else {
                        out.append("(?:");
                        writeRigid(n.subs[0], live);
                        out.append(')');
                    }
                    break;
                case CAT:
                    for (Node sub : n.subs) {
                        writeRigid(sub, live);
                    }
                    break;
                case REPEAT: {
                    long start = at;
                    long length = rigid(n.subs[0]);
                    for (int k = 0; k < n.min; k++) {
                        place(n.subs[0], start + k * length);
                        if (k == 0) {
                            writeRigid(n.subs[0], live);
                        } else {
                            Env saved = env.copy();
                            writeRigid(n.subs[0], NOTHING);
                            env = saved;
                        }
                        if (++unrolledCopies > MAX_UNROLLED_COPIES) {
                            throw new TooLarge(REFUSED_EMPTY_ITERATIONS, LIMIT_COPIES);
                        }
                    }
                    place(n.subs[0], start);
                    break;
                }
                case BACKREF: {
                    int target = target(n);
                    long[] place = places.get(target);
                    if (place == null || place[0] - at < place[1]) {
                        throw new Unsupported(REFUSED_BACKREFERENCE_IN_LOOKBEHIND);
                    }
                    used.set(target);
                    long distance = place[0] - at;
                    long length = place[1];
                    if (length == 0) {
                        break;
                    }
                    int[][] classes = n.fold
                            ? EcmaUnicode.foldClasses(capturedSet(groupNodes[target].subs[0]), unicode)
                            : new int[0][];
                    if (classes.length == 0) {
                        String copy = newName();
                        out.append("(?=(?<").append(copy).append('>').append(ANY).append('{').append(length)
                                .append("})").append(ANY).append('{').append(distance - length).append('}');
                        refer(copy);
                        out.append(')').append(ANY).append('{').append(length).append('}');
                    } else {
                        // Character by character: the character d on is this one, or both are in one class (by the
                        // tests of foldTests).
                        // Written once for each character when they are few, as java.util.regex does not bound a
                        // lookbehind that holds a repeated group with alternatives.
                        String skip = ANY + "{" + (distance - 1) + "}";
                        long copies = length <= 16 ? length : 1;
                        String[][] tests = foldTests(classes);
                        for (long k = 0; k < copies; k++) {
                            String one = newName();
                            out.append("(?:(?:(?=(?<").append(one).append('>').append(ANY).append(')').append(skip);
                            refer(one);
                            out.append(")|");
                            for (String[] test : tests) {
                                out.append("(?:");
                                for (int t = 0; t < test.length; t++) {
                                    out.append(t > 0 ? "|" : "").append("(?=").append(test[t]).append(skip)
                                            .append(test[t]).append(')');
                                }
                                out.append(')');
                            }
                            out.append(')').append(ANY).append(')');
                        }
                        if (copies != length) {
                            out.append('{').append(length).append('}');
                        }
                    }
                    at += length;
                    break;
                }
                default:
                    throw new Unsupported(REFUSED_BACKREFERENCE_IN_LOOKBEHIND);
            }
        }

        /**
         * Writes, forwards, a node of a lookbehind that holds a group that is read or a backreference to a group of
         * the lookbehind. The node is the one ECMA-262 runs last (the lookbehind itself, or what stands first in
         * it), so nothing can fail after it and send the match back into it, and it takes the first of its ways that
         * matches:
         *
         * <ul>
         *   <li>a rigid node has one way (see {@link #writeRigid});
         *   <li>a sequence is its first term, then rigid terms;
         *   <li>of alternatives, the first that matches is taken: each is a decision of the plan, written with a
         *       negative lookbehind after it for each alternative before it;
         *   <li>a greedy repetition of a rigid body runs as many times as it can. It is written with the copy that
         *       holds its groups first, and is either not preceded by one more match of the body or at its
         *       maximum. A lazy one runs its minimum.
         * </ul>
         *
         * <p>Anything else is refused.
         */
        private void edge(Node n, BitSet live) {
            if (rigid(n) >= 0) {
                places = new HashMap<>();
                at = 0;
                place(n, 0);
                writeRigid(n, live);
                return;
            }
            if (uninvolved(n) && !captured) {
                boolean wasStrict = strict;
                strict = false;
                out.append("(?:").append(scope(n, NOTHING, null)).append(')');
                strict = wasStrict;
                return;
            }
            String refused = readAfter ? REFUSED_GROUP_IN_LOOKBEHIND : REFUSED_BACKREFERENCE_IN_LOOKBEHIND;
            switch (n.kind) {
                case GROUP:
                    if (n.group != 0 && live.get(n.group)) {
                        String name = newName();
                        out.append("(?<").append(name).append('>');
                        int from = out.length();
                        boolean wasCaptured = captured;
                        captured = true;
                        edge(n.subs[0], live);
                        captured = wasCaptured;
                        set(n.group, name, from);
                        out.append(')');
                    } else {
                        out.append("(?:");
                        edge(n.subs[0], live);
                        out.append(')');
                    }
                    break;
                case ALT: {
                    // An alternative that matches the empty string anywhere is taken before any after it.
                    int ways = 1;
                    while (ways < n.subs.length && !emptyAnywhere(n.subs[ways - 1])) {
                        ways++;
                    }
                    int way = ways > 1 ? choose(ways) : 0;
                    out.append("(?:");
                    edge(n.subs[way], live);
                    out.append(')');
                    for (int k = 0; k < way; k++) {
                        notBehind(n.subs[k]);
                    }
                    break;
                }
                case CAT: {
                    // The rigid terms at the end, and before them either one term, or terms that hold no group
                    // that is read and no backreference to a group of the lookbehind.
                    int tail = n.subs.length;
                    while (tail > 0 && rigid(n.subs[tail - 1]) >= 0) {
                        tail--;
                    }
                    if (tail == 1) {
                        edge(n.subs[0], live);
                    } else {
                        for (int k = 0; k < tail; k++) {
                            // Inside a group that is read, what these terms match is part of what is captured.
                            if (captured || !uninvolved(n.subs[k])) {
                                throw new Unsupported(refused);
                            }
                            boolean wasStrict = strict;
                            strict = false;
                            out.append("(?:").append(scope(n.subs[k], NOTHING, null)).append(')');
                            strict = wasStrict;
                        }
                    }
                    places = new HashMap<>();
                    at = 0;
                    long from = 0;
                    for (int k = tail; k < n.subs.length; k++) {
                        place(n.subs[k], from);
                        from += rigid(n.subs[k]);
                    }
                    for (int k = tail; k < n.subs.length; k++) {
                        writeRigid(n.subs[k], live);
                    }
                    break;
                }
                case REPEAT: {
                    Node body = n.subs[0];
                    if (rigid(body) <= 0) {
                        throw new Unsupported(refused);
                    }
                    if (n.lazy) {
                        if (n.min > 0) {
                            edgeCopies(body, live, n.min - 1, n.min - 1);
                        }
                        break;
                    }
                    // 0: no iteration. 1: as many as match. 2: the maximum.
                    int way = (n.min == 0 ? 0 : 1) + choose((n.min == 0 ? 1 : 0) + (n.max == UNBOUNDED ? 1 : 2));
                    if (way < 2) {
                        Env saved = env.copy();
                        int start = out.length();
                        places = new HashMap<>();
                        at = 0;
                        place(body, 0);
                        writeRigid(body, NOTHING);
                        String once = out.substring(start);
                        out.setLength(start);
                        env = saved;
                        // Not preceded by one more match of the body, which has one length. The body stands in a
                        // lookahead, at that distance back: java.util.regex does not work out the length of a
                        // lookbehind that holds a counted group with alternatives.
                        out.append("(?<!(?=").append(once).append(')').append(ANY).append('{').append(rigid(body))
                                .append("})");
                    }
                    if (way == 1) {
                        edgeCopies(body, live, Math.max(n.min, 1) - 1, n.max == UNBOUNDED ? UNBOUNDED : n.max - 1);
                    } else if (way == 2) {
                        edgeCopies(body, live, n.max - 1, n.max - 1);
                    }
                    break;
                }
                default:
                    throw new Unsupported(refused);
            }
        }

        /** Writes the copy of a rigid body that holds the groups read after it, then the others as a repetition. */
        private void edgeCopies(Node body, BitSet live, int min, int max) {
            places = new HashMap<>();
            at = 0;
            place(body, 0);
            out.append("(?:");
            writeRigid(body, live);
            out.append(')');
            if (max == 0) {
                return;
            }
            Env saved = env.copy();
            places = new HashMap<>();
            at = 0;
            place(body, 0);
            out.append("(?:");
            writeRigid(body, NOTHING);
            out.append(')');
            quantifier(min, max, false);
            env = saved;
        }

        /**
         * Writes a backreference: the group of the translation that the path set, or nothing when the path set none.
         *
         * <p>A group set in a lookaround holds what the lookaround captured when it last matched, also after the
         * match went back to before that. That can only be seen when the lookaround runs again and the match then
         * goes back to a place between it and the backreference, so in a loop that holds both, with a place
         * between them where the match can take another way. There the lookaround is run again before the
         * backreference (see {@link #fresh}).
         */
        private void reference(Node n) {
            int group = groupRead(n);
            if (group == 0 || env.empty[group]) {
                return;
            }
            fresh(group, false);
            used.set(group);
            if (n.fold) {
                folded(group);
            } else {
                refer(env.name[group]);
            }
        }

        /** The group, of those a backreference names, that the path has set, or 0. */
        private int groupRead(Node n) {
            int group = 0;
            for (int g : n.groups) {
                if (env.name[g] != null) {
                    if (group != 0) {
                        throw new IllegalStateException("two groups of one name on a path");
                    }
                    group = g;
                }
            }
            return group;
        }

        /** Up to this many classes of equivalent characters are tested one by one. */
        private static final int FOLD_CLASSES_ONE_BY_ONE = 8;

        /**
         * The tests that say together that two characters are in one of the classes: each test is a list of sets
         * that share no character, and two characters are in one class when, for every test, one set of it holds
         * both.
         *
         * <p>A few classes are one test, with a set for each class. A group that can hold any character has over
         * 1,400 classes, and a test of that many sets is long: each set is written with what finds the other
         * character. So many classes are numbered in their order, and two tests are written. In the first a set
         * holds the classes of one quotient of the number by the square root of the count, and in the second the
         * classes of one remainder. Two classes with the same quotient and the same remainder are the same class,
         * and the two tests have twice the square root of the count of sets between them.
         */
        private static String[][] foldTests(int[][] classes) {
            int n = classes.length;
            if (n <= FOLD_CLASSES_ONE_BY_ONE) {
                String[] sets = new String[n];
                for (int k = 0; k < n; k++) {
                    sets[k] = members(classes, k, 1, k + 1);
                }
                return new String[][] {sets};
            }
            int side = (int) Math.ceil(Math.sqrt(n));
            String[] quotients = new String[(n + side - 1) / side];
            for (int k = 0; k < quotients.length; k++) {
                quotients[k] = members(classes, k * side, 1, Math.min(n, (k + 1) * side));
            }
            String[] remainders = new String[side];
            for (int k = 0; k < side; k++) {
                remainders[k] = members(classes, k, side, n);
            }
            return new String[][] {quotients, remainders};
        }

        /**
         * A class of java.util.regex with the members of the classes from {@code from}, every {@code step}th, up to
         * {@code to}. A letter is written as itself, which keeps a class of many members short, and runs of members
         * as ranges.
         */
        private static String members(int[][] classes, int from, int step, int to) {
            int count = 0;
            for (int k = from; k < to; k += step) {
                count += classes[k].length;
            }
            int[] sorted = new int[count];
            count = 0;
            for (int k = from; k < to; k += step) {
                System.arraycopy(classes[k], 0, sorted, count, classes[k].length);
                count += classes[k].length;
            }
            Arrays.sort(sorted);
            StringBuilder sb = new StringBuilder(count + 2).append('[');
            for (int k = 0; k < count; k++) {
                int last = k;
                while (last + 1 < count && sorted[last + 1] == sorted[last] + 1) {
                    last++;
                }
                appendMember(sb, sorted[k]);
                if (last > k + 1) {
                    sb.append('-');
                }
                if (last > k) {
                    appendMember(sb, sorted[last]);
                }
                k = last;
            }
            return sb.append(']').toString();
        }

        private static void appendMember(StringBuilder sb, int c) {
            if (c >= 0x80 ? !(c >= Character.MIN_SURROGATE && c <= Character.MAX_SURROGATE)
                    : (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) {
                sb.appendCodePoint(c);
            } else {
                EcmaUnicode.appendCodePoint(sb, c);
            }
        }

        /**
         * Writes a case-insensitive backreference. ECMA-262 compares the text of the group and the text at the
         * backreference character by character, by its Canonicalize. The case-insensitive backreference of
         * java.util.regex compares by the case mappings of the JDK it runs on, which equate other pairs (among them
         * "i" and U+0130 or U+0131), and on Java 17 it miscounts a character beyond the Basic Multilingual Plane. So
         * it is used only for the characters it compares as ECMA-262 does (see {@link #engineDiffers}), and for the
         * others the comparison is written out with the classes of equivalent characters of {@link EcmaUnicode}, the
         * ones a character of the group can belong to.
         *
         * <p>A group of at most one character G is compared in one step: the text is G, or its next character is
         * one the engine compares rightly and the engine takes it for G, or it is in some class K that is written
         * out and G is in K too. That G is in K is shown by a search for a place in the text where G stands (the
         * place it was captured at, if no other) at a character of K. The classes are written as the tests of
         * {@link #foldTests}.
         */
        private void folded(int group) {
            String name = env.name[group];
            Node body = groupNodes[group].subs[0];
            int[][] classes = EcmaUnicode.foldClasses(capturedSet(body), unicode);
            if (classes.length == 0) {
                refer(name);
                return;
            }
            hasLookbehind = true;
            // A group set in a lookaround can stand after the backreference, and so can a group that a backreference
            // in a lookbehind reads, which is tried at places before the lookbehind. Such a group is looked for in
            // the whole text.
            boolean anywhere = env.look[group] || lookbehinds > 0;
            if (maxLength(body) > 1) {
                foldedLoop(name, classes, anywhere);
                return;
            }
            String rest = newName();
            // For a group set in a lookaround the two ways are one step: the match must not come back to the second
            // after a later run of the lookaround has set the group again. Both take the same text where both match.
            out.append(env.look[group] ? "(?>" : "(?:");
            refer(name);
            out.append("|(?=(?<").append(rest).append('>').append(ANY).append("*))(?!");
            refer(name);
            refer(rest);
            out.append("\\z)");
            int[][] written = writtenOut(classes);
            if (written.length < classes.length) {
                // The character is one the engine compares as ECMA-262 does, and the engine says it is the same.
                out.append("(?:").append(engineCompares()).append("(?=(?iu:");
                refer(name);
                out.append("))");
                if (written.length == 0) {
                    out.append(')').append(ANY).append(')');
                    return;
                }
                out.append('|');
            }
            for (String[] test : foldTests(written)) {
                out.append("(?:");
                for (int k = 0; k < test.length; k++) {
                    out.append(k > 0 ? "|" : "").append("(?=").append(test[k]).append(')')
                            .append(found("(?=\\k<" + name + ">)(?=" + test[k] + ")", anywhere));
                }
                out.append(')');
            }
            out.append(written.length < classes.length ? ")" : "").append(ANY).append(')');
        }

        // The comparison of the engine.
        //
        // The case-insensitive backreference of java.util.regex, with its flag for Unicode case, compares two
        // characters by the case mappings of the JDK it runs on. For most characters that is the comparison of
        // ECMA-262, and it is one step where the comparison written out is a search. So the engine compares the
        // characters for which it is known to agree, and the others are compared by the classes written out.
        //
        // Which characters those are is not taken from a table: the JDKs differ (Java 21 does not equate the
        // pairs that Unicode 15.1 and 16 added, every JDK equates "i" with U+0130 and U+0131, and Java 17 fails on
        // a character beyond the Basic Multilingual Plane). It is found by asking the engine of this JVM, once, about
        // every pair of characters that a case mapping of the JDK or a class of ECMA-262 relates. A character is
        // left to the engine only when the engine agreed with ECMA-262 on every pair it is in.

        /** For each grammar (with the u flag, without it): the characters not left to the engine, once worked out. */
        private static final int[][] ENGINE_DIFFERS = new int[4][];
        /** The same, as a test that the next character is not one of them. */
        private static final String[] ENGINE_COMPARES = new String[4];
        /**
         * The characters the engine of JDK 17 does not compare as ECMA-262 does, with the u flag and without it.
         * Those of JDK 21 and of JDK 25 are among them (JDK 17 compares no character beyond the Basic Multilingual
         * Plane). A translation made for these (see {@link #reference}) is the same text on every one of the three.
         */
        private static final int[][] REFERENCE_DIFFERS = {
            {
                0x49, 0x49, 0x69, 0x69, 0x130, 0x131, 0x19B, 0x19B, 0x264, 0x264, 0x390, 0x390, 0x3B0, 0x3B0, 0x1C89,
                0x1C8A, 0x1FD3, 0x1FD3, 0x1FE3, 0x1FE3, 0x2C2F, 0x2C2F, 0x2C5F, 0x2C5F, 0xA7C0, 0xA7C1, 0xA7CB, 0xA7DC,
                0xFB05, 0xFB06, 0x10000, 0x10FFFF,
            },
            {
                0x49, 0x49, 0x4B, 0x4B, 0x53, 0x53, 0x69, 0x69, 0x6B, 0x6B, 0x73, 0x73, 0xC5, 0xC5, 0xDF, 0xDF, 0xE5,
                0xE5, 0x130, 0x131, 0x17F, 0x17F, 0x19B, 0x19B, 0x264, 0x264, 0x398, 0x398, 0x3A9, 0x3A9, 0x3B8, 0x3B8,
                0x3C9, 0x3C9, 0x3D1, 0x3D1, 0x3F4, 0x3F4, 0x1C89, 0x1C8A, 0x1E9E, 0x1E9E, 0x1F80, 0x1FAF, 0x1FB3, 0x1FB3,
                0x1FBC, 0x1FBC, 0x1FC3, 0x1FC3, 0x1FCC, 0x1FCC, 0x1FF3, 0x1FF3, 0x1FFC, 0x1FFC, 0x2126, 0x2126, 0x212A,
                0x212B, 0x2C2F, 0x2C2F, 0x2C5F, 0x2C5F, 0xA7C0, 0xA7C1, 0xA7CB, 0xA7DC, 0x10000, 0x10FFFF,
            },
        };
        /**
         * Whether the translation is made for the engine of reference, and whether it wrote a comparison that
         * depends on the engine. See {@link EcmaRegex#translation}.
         */
        boolean reference;
        boolean comparedByEngine;
        boolean paddedSets;

        /** A test that the next character is one the engine compares as ECMA-262 does with every character. */
        private String engineCompares() {
            comparedByEngine = true;
            engineDiffers(unicode, reference);
            return ENGINE_COMPARES[(unicode ? 0 : 1) + (reference ? 2 : 0)];
        }

        /** The classes among these that have a character the engine does not compare as ECMA-262 does. */
        private int[][] writtenOut(int[][] classes) {
            comparedByEngine = true;
            int[] differs = engineDiffers(unicode, reference);
            List<int[]> kept = new ArrayList<>();
            for (int[] members : classes) {
                for (int c : members) {
                    if (EcmaUnicode.contains(differs, c)) {
                        kept.add(members);
                        break;
                    }
                }
            }
            return kept.size() == classes.length ? classes : kept.toArray(new int[0][]);
        }

        /**
         * The characters that the case-insensitive backreference of the java.util.regex of this JVM does not compare,
         * with some character, as the Canonicalize of a grammar does. Both characters of a pair it gets wrong are in
         * the set, whichever of them stands in the group.
         */
        private static int[] engineDiffers(boolean unicode, boolean reference) {
            if (reference) {
                // What this engine gets wrong is part of it too, whatever the JDK, so that the answers are right.
                engineDiffers(unicode, false);
            }
            int at = (unicode ? 0 : 1) + (reference ? 2 : 0);
            synchronized (ENGINE_DIFFERS) {
                if (ENGINE_DIFFERS[at] == null) {
                    int[] differs = reference
                            ? EcmaUnicode.union(REFERENCE_DIFFERS[unicode ? 0 : 1], ENGINE_DIFFERS[unicode ? 0 : 1])
                            : askEngine(unicode);
                    StringBuilder sb = new StringBuilder("(?!");
                    if (differs.length == 0) {
                        sb.setLength(0);
                    } else {
                        EcmaUnicode.appendClass(sb, differs);
                        sb.append(')');
                    }
                    ENGINE_COMPARES[at] = sb.toString();
                    ENGINE_DIFFERS[at] = differs;
                }
                return ENGINE_DIFFERS[at];
            }
        }

        /** Whether the engine of this JVM gets wrong every character the engine of reference does. */
        static boolean isReference(boolean unicode) {
            return Arrays.equals(engineDiffers(unicode, false), engineDiffers(unicode, true));
        }

        private static final int CODE_POINTS = EcmaUnicode.MAX + 1;

        private static int root(int[] parent, int c) {
            while (parent[c] != c) {
                parent[c] = parent[parent[c]];
                c = parent[c];
            }
            return c;
        }

        private static void relate(int[] parent, int a, int b) {
            int ra = root(parent, a);
            int rb = root(parent, b);
            if (ra != rb) {
                parent[Math.max(ra, rb)] = Math.min(ra, rb);
            }
        }

        private static int[] askEngine(boolean unicode) {
            // The characters that may be compared as the same: those a case mapping of the JDK relates, which is
            // what the engine goes by, and those of one class of ECMA-262.
            int[] parent = new int[CODE_POINTS];
            for (int c = 0; c < CODE_POINTS; c++) {
                parent[c] = c;
            }
            for (int c = 0; c < CODE_POINTS; c++) {
                if (c < Character.MIN_SURROGATE || c > Character.MAX_SURROGATE) {
                    relate(parent, c, Character.toUpperCase(c));
                    relate(parent, c, Character.toLowerCase(c));
                    relate(parent, c, Character.toTitleCase(c));
                }
            }
            int[][] classes = EcmaUnicode.foldClasses(new int[] {0, EcmaUnicode.MAX}, unicode);
            int[] classOf = new int[CODE_POINTS];
            for (int k = 0; k < classes.length; k++) {
                for (int c : classes[k]) {
                    classOf[c] = k + 1;
                    relate(parent, classes[k][0], c);
                }
            }
            Map<Integer, List<Integer>> related = new HashMap<>();
            for (int c = 0; c < CODE_POINTS; c++) {
                int r = root(parent, c);
                if (r != c) {
                    related.computeIfAbsent(r, k -> new ArrayList<>(List.of(k))).add(c);
                }
            }
            Pattern probe = Pattern.compile("(" + ANY + ")(?iu:\\1)");
            BitSet differs = new BitSet();
            StringBuilder two = new StringBuilder(4);
            for (List<Integer> members : related.values()) {
                for (int inGroup : members) {
                    for (int inText : members) {
                        boolean ecma = inGroup == inText
                                || (classOf[inGroup] != 0 && classOf[inGroup] == classOf[inText]);
                        if (same(probe, two, inGroup, inText) != (ecma ? 1 : 0)) {
                            differs.set(inGroup);
                            differs.set(inText);
                        }
                    }
                }
            }
            // A character that no mapping relates to another is compared with itself only. The engine of Java 17
            // fails on that for a character beyond the Basic Multilingual Plane.
            for (int c : new int[] {'-', 0x3042, 0x10000, 0x1F600, UNIT_BASE, EcmaUnicode.MAX}) {
                if (same(probe, two, c, c) != 1 || same(probe, two, c, c - 1) != 0 || same(probe, two, c - 1, c) != 0) {
                    differs.set(c < 0x10000 ? 0 : 0x10000, c < 0x10000 ? 0x10000 : CODE_POINTS);
                }
            }
            int[] pairs = new int[2 * differs.cardinality()];
            int count = 0;
            for (int c = differs.nextSetBit(0); c >= 0; c = differs.nextSetBit(c + 1)) {
                int last = differs.nextClearBit(c) - 1;
                pairs[2 * count] = c;
                pairs[2 * count + 1] = last;
                count++;
                c = last;
            }
            return Arrays.copyOf(pairs, 2 * count);
        }

        /**
         * Whether the engine takes the second character for a case-insensitive backreference to the first: 1 when it
         * does, 0 when it does not, and -1 when it fails on the pair, which is then not left to it.
         */
        private static int same(Pattern probe, StringBuilder two, int inGroup, int inText) {
            two.setLength(0);
            two.appendCodePoint(inGroup).appendCodePoint(inText);
            try {
                return probe.matcher(two).matches() ? 1 : 0;
            } catch (RuntimeException e) {
                return -1;
            }
        }

        /**
         * A lookbehind that holds when the text has a place where {@code test} holds: one at or before the place
         * the lookbehind stands at, or anywhere in the text.
         */
        private static String found(String test, boolean anywhere) {
            return anywhere
                    ? "(?<=^(?=" + ANY + "*?" + test + ")" + ANY + "{0,2147483647})"
                    : "(?<=" + test + ANY + "{0,2147483647})";
        }

        /**
         * Writes a case-insensitive backreference to a group G of any length, as a loop that takes one character of
         * the text for each character of G. A group P of the translation holds the rest of the text from the place
         * in G the loop has reached (a pointer into a place in the text where G stands), and each iteration sets it
         * again, one character on. java.util.regex keeps what a group captured in the iteration before, and a
         * backreference to P inside P itself reads that, as a group is set when it closes. That is what carries
         * the pointer. (A backreference written before its group would do too, but java.util.regex reads the number
         * of one only as far as the groups it has seen, so not past 9.)
         *
         * <pre>
         * (?=(Q0 A*))                          the rest of the text where the backreference starts
         * (?&gt;(?:
         *     (?=(Q (X A) A*))                  the next character X, and the rest of the text from it
         *     (?&lt;=(?=(P                         look for the place one after the pointer, and make it the pointer:
         *        (?: first (?&lt;=(?=G (E A*)) (?!E\z) same A)    in the first iteration it is one after a place
         *                                              where G stands: E is the rest of the text after G, G is
         *                                              not empty, and the character before is the same as X
         *          | (?&lt;=(?=P\z) (?!E\z) same A) not first )    later the rest of the text one before is P,
         *                                                         and G has not ended
         *        A*))
         *        A{0,2147483647})
         *     A                                 the character is taken
         *   )*+
         *   (?: (?=Q0\z) (?=G Q0\z)             no character was taken, and G is empty
         *     | (?!Q0\z) (?&lt;=(?=P\z)(?=E\z) A{0,2147483647}) ) )    or the pointer is at the end of G
         * </pre>
         *
         * <p>"Same" is {@code (?: (?=X) | (?![D]) (?=(?iu:X)) | (?=[K]) here [K] | ... )}: the character is X, or it
         * is not one of the characters D the engine compares wrongly and the engine takes it for X, or it and X are
         * in one class K that is written out, or with many such classes in one set of each test of
         * {@link #foldTests}. "Here [K]" tests the character X from the place looked at,
         * {@code (?=A*?(?=Q\z)[K])}, and "first" is the test that the iteration stands where the backreference
         * starts, {@code (?=A*?(?=Q\z)(?=Q0\z))}. The loop is possessive and the whole is atomic. There is one way to
         * compare two texts, and going back into it would read a P that a later iteration has set.
         *
         * <p>With {@code anywhere}, for a group set in a lookaround, which can stand after the backreference, each
         * search goes through the whole text and not only back.
         */
        private void foldedLoop(String name, int[][] classes, boolean anywhere) {
            String start = newName();
            String here = newName();
            String next = newName();
            String pointer = newName();
            String end = newName();
            String toHere = ANY + "*?(?=\\k<" + here + ">\\z)";
            // From the place looked at, to the place the iteration stands at.
            String hereStart = anywhere ? "(?<=^(?=" + toHere : "(?=" + toHere;
            String hereEnd = anywhere ? ")" + ANY + "{0,2147483647})" : ")";
            String first = hereStart + "(?=\\k<" + start + ">\\z)" + hereEnd;
            StringBuilder same = new StringBuilder("(?:(?=\\k<" + next + ">)|");
            int[][] written = writtenOut(classes);
            if (written.length < classes.length) {
                // The character of the group is one the engine compares as ECMA-262 does, and it says so.
                same.append(engineCompares()).append("(?=(?iu:\\k<").append(next).append(">))")
                        .append(written.length == 0 ? "" : "|");
            }
            for (String[] test : foldTests(written)) {
                same.append("(?:");
                for (int k = 0; k < test.length; k++) {
                    same.append(k > 0 ? "|" : "").append("(?=").append(test[k]).append(')').append(hereStart)
                            .append(test[k]).append(hereEnd);
                }
                same.append(')');
            }
            same.append(')');
            String notEnded = "(?!\\k<" + end + ">\\z)";
            // E is set only once the iteration is known to be the first, as a later one reads it.
            // The tests of the place one before stand in a lookahead, inside a lookbehind of one character:
            // java.util.regex does not bound a lookbehind that holds another one outside a lookahead.
            String step = "(?=(?<" + pointer + ">(?:(?<=(?=\\k<" + name + ">)" + ANY + ")" + first + "(?<=(?=(?=\\k<"
                    + name + ">(?<" + end + ">" + ANY + "*))" + notEnded + same + ")" + ANY + ")|(?<=(?=(?=\\k<"
                    + pointer + ">\\z)" + notEnded + same + ")" + ANY + ")(?!" + first + "))" + ANY + "*))";
            out.append("(?=(?<").append(start).append('>').append(ANY).append("*))(?>(?:(?=(?<").append(here)
                    .append(">(?<").append(next).append('>').append(ANY).append(')').append(ANY).append("*))")
                    .append(found(step, anywhere)).append(ANY).append(")*+(?:(?=");
            refer(start);
            out.append("\\z)(?=");
            refer(name);
            refer(start);
            out.append("\\z)|(?!");
            refer(start);
            out.append("\\z)").append(found("(?=\\k<" + pointer + ">\\z)(?=\\k<" + end + ">\\z)", anywhere))
                    .append("))");
        }
    }

    /**
     * Writes into a translation the tests that let a match be held to its limit of time.
     *
     * <p>{@code java.util.regex} has no limit on a match. What the library can see of one is what it does to the
     * text: {@link Utf8Chars} counts every character the engine reads and every time it asks for the length of the
     * text, and reads the clock every so many. With the bounds the library gives a matcher (see
     * {@link SchemaPattern#matcher}) the engine asks for the length at every lookahead, word boundary and end anchor.
     * So a match is counted wherever it reads a character or meets one of those, which is nearly everywhere: a
     * backtracking search goes wrong by trying to read the text in many ways.
     *
     * <p>It is not everywhere. The engine reads no character at the end of the text (a test of a character fails
     * there unread), nor to take an empty alternative, enter or leave a group, pass {@code ^}, or compare a group
     * that holds nothing. A pattern made of such steps can try them in a number of ways that doubles with each
     * choice, {@code (?:|)(?:|)(?:|)...} for one, and never be counted. So where the engine could take a choice, or
     * come round a loop, without reading and without asking, a test that asks is written: the empty lookahead
     * {@code (?=)}, which holds everywhere, takes no character and sets no group, and so changes no answer. It is
     * written
     *
     * <ul>
     *   <li>before a group with two or more alternatives that can match nothing uncounted (other than a lookahead,
     *       which asks as it is entered);
     *   <li>before a repetition that can run no times over a body that can match nothing uncounted, which is a
     *       choice between two ways to do so;
     *   <li>at the start of the body of a repetition that must run twice or more over a body that can match nothing
     *       uncounted, which the engine can run the whole count of;
     *   <li>at the start of the pattern when it does not start with a step that is counted or that fails at once,
     *       as the engine runs the pattern from every place in the text.
     * </ul>
     *
     * <p>A part matches nothing uncounted when it can match the empty string by steps that are all uncounted: it
     * is made of empty alternatives, parts that can run no times, {@code ^}, lookbehinds and backreferences. One
     * that can match the empty string only past a lookahead, a word boundary or an end anchor is counted there.
     *
     * <p>With them, the steps the engine takes between two that are counted are at most the nodes of the
     * translation, and the interval at which the clock is read is shorter for a longer translation (see
     * {@link SchemaPattern#interval}).
     *
     * <p>The text is read as the translator writes it: a literal that is not a letter or a digit is an escape, a
     * class is in brackets, and every quantified thing that is not one character is a group.
     */
    static final class Ticks {
        private Ticks() {
        }

        /** The test that asks for the length of the text and always holds. */
        static final String TICK = "(?=)";

        private static final int GROUP = 0;
        private static final int LOOKAHEAD = 1;
        private static final int LOOKBEHIND = 2;
        private static final int NEGATIVE_LOOKBEHIND = 3;

        // What is known of the first term of an alternative.
        private static final int NO_TERM = 0;
        private static final int COUNTED = 1;
        private static final int SILENT = 2;

        /** The groups that are open, innermost last. Frame 0 is the pattern. */
        private static final class Frames {
            int depth;
            int[] open = new int[16];
            int[] body = new int[16];
            int[] kind = new int[16];
            /** The alternatives closed so far, and how many of them can match nothing uncounted. */
            int[] alternatives = new int[16];
            int[] nullableAlternatives = new int[16];
            /** What the first term of the alternative being read is, and what that of the first alternative was. */
            int[] first = new int[16];
            int[] firstOfAll = new int[16];
            boolean[] alternativeNullable = new boolean[16];
            /** The terms read in the group, and whether each was one character. */
            int[] terms = new int[16];
            boolean[] onlyCharacters = new boolean[16];
            boolean[] plain = new boolean[16];

            void push(int openAt, int bodyAt, int kindOf) {
                if (++depth == open.length) {
                    int n = 2 * depth;
                    open = Arrays.copyOf(open, n);
                    body = Arrays.copyOf(body, n);
                    kind = Arrays.copyOf(kind, n);
                    alternatives = Arrays.copyOf(alternatives, n);
                    nullableAlternatives = Arrays.copyOf(nullableAlternatives, n);
                    first = Arrays.copyOf(first, n);
                    firstOfAll = Arrays.copyOf(firstOfAll, n);
                    alternativeNullable = Arrays.copyOf(alternativeNullable, n);
                    terms = Arrays.copyOf(terms, n);
                    onlyCharacters = Arrays.copyOf(onlyCharacters, n);
                    plain = Arrays.copyOf(plain, n);
                }
                open[depth] = openAt;
                body[depth] = bodyAt;
                kind[depth] = kindOf;
                alternatives[depth] = 0;
                nullableAlternatives[depth] = 0;
                first[depth] = NO_TERM;
                firstOfAll[depth] = NO_TERM;
                alternativeNullable[depth] = true;
                terms[depth] = 0;
                onlyCharacters[depth] = true;
                plain[depth] = true;
            }
        }

        /** The places to write the test at. */
        private static final class Places {
            int[] at = new int[8];
            int count;

            void add(int place) {
                if (count == at.length) {
                    at = Arrays.copyOf(at, 2 * count);
                }
                at[count++] = place;
            }
        }

        /** The translation with the tests written into it. It is the same string when it needs none. */
        static String insert(String t) {
            Frames f = new Frames();
            f.depth = -1;
            f.push(-1, 0, GROUP);
            Places places = new Places();
            int n = t.length();
            if (n == 0) {
                // The pattern that matches everywhere. Nothing of it is run.
                return t;
            }
            int i = 0;
            while (i < n) {
                char c = t.charAt(i);
                if (c == '|') {
                    f.plain[f.depth] = false;
                    endAlternative(f);
                    i++;
                    continue;
                }
                int start = i;
                // Whether the atom can match nothing uncounted, and whether its first step is counted or fails at
                // once. A lookbehind of one character reads it, or fails at the start of the text, and then the
                // term after it is the first.
                boolean nullable = false;
                boolean counted = true;
                boolean passed = false;
                // Whether a group has no alternatives and no repetition but one of a fixed count, in it or in a
                // group of it. java.util.regex runs the whole count of a repetition of such a group.
                boolean plain = true;
                boolean character = false;
                boolean group = false;
                int groupKind = GROUP;
                int groupBody = 0;
                int groupNullable = 0;
                if (c == '(') {
                    // A group opens: its terms are read into a frame of their own.
                    int kind = GROUP;
                    int body = i + 1;
                    if (body < n && t.charAt(body) == '?') {
                        char k = t.charAt(body + 1);
                        if (k == '=' || k == '!') {
                            kind = LOOKAHEAD;
                            body += 2;
                        } else if (k == '<' && (t.charAt(body + 2) == '=' || t.charAt(body + 2) == '!')) {
                            kind = t.charAt(body + 2) == '=' ? LOOKBEHIND : NEGATIVE_LOOKBEHIND;
                            body += 3;
                        } else if (k == '<') {
                            body = t.indexOf('>', body) + 1;
                        } else if (k == ':' || k == '>') {
                            body += 2;
                        } else {
                            // Modifiers, as in (?i:...).
                            body = t.indexOf(':', body) + 1;
                        }
                    }
                    f.push(i, body, kind);
                    i = body;
                    continue;
                }
                if (c == ')') {
                    endAlternative(f);
                    int d = f.depth--;
                    if (d == 0) {
                        throw new IllegalStateException("a translation with a group that is not open: " + t);
                    }
                    start = f.open[d];
                    group = true;
                    groupKind = f.kind[d];
                    groupBody = f.body[d];
                    groupNullable = f.nullableAlternatives[d];
                    boolean behind = groupKind == LOOKBEHIND || groupKind == NEGATIVE_LOOKBEHIND;
                    // A lookbehind of one character reads it, except at the start of the text, where it fails
                    // unread. The negative one holds there, uncounted.
                    passed = behind && f.terms[d] == 1 && f.onlyCharacters[d];
                    nullable = groupKind == GROUP ? groupNullable > 0 : behind && !(passed && groupKind == LOOKBEHIND);
                    counted = groupKind == LOOKAHEAD || (groupKind == GROUP && f.firstOfAll[d] == COUNTED);
                    plain = f.plain[d];
                    i++;
                } else if (c == '[') {
                    i = Emitter.classEnd(t, i);
                    character = true;
                } else if (c == '\\') {
                    char e = t.charAt(i + 1);
                    if ((e == 'x' || e == 'p' || e == 'P') && i + 2 < n && t.charAt(i + 2) == '{') {
                        i = t.indexOf('}', i) + 1;
                        character = true;
                    } else if (e == 'k') {
                        i = t.indexOf('>', i) + 1;
                        nullable = true;
                        counted = false;
                    } else if (e >= '1' && e <= '9') {
                        i += 2;
                        while (i < n && t.charAt(i) >= '0' && t.charAt(i) <= '9') {
                            i++;
                        }
                        nullable = true;
                        counted = false;
                    } else {
                        i += 2;
                        // The end of the text and a word boundary ask for the length. \A fails at once anywhere but
                        // the start, where it is not counted.
                        nullable = e == 'A' || e == 'G';
                        counted = e != 'G';
                        character = !nullable && e != 'z' && e != 'Z' && e != 'b' && e != 'B';
                    }
                } else if (c == '^') {
                    i++;
                    nullable = true;
                } else if (c == '$') {
                    i++;
                } else if (c == '*' || c == '+' || c == '?' || c == '{' || c == '}' || c == ']') {
                    throw new IllegalStateException("a translation that is not read as it was written, at " + i + ": " + t);
                } else {
                    i += Character.isHighSurrogate(c) ? 2 : 1;
                    character = true;
                }
                // The quantifier of the term.
                long min = 1;
                boolean quantified = false;
                boolean exact = false;
                boolean lazy = false;
                if (i < n) {
                    char q = t.charAt(i);
                    if (q == '*' || q == '?') {
                        min = 0;
                        quantified = true;
                        i++;
                    } else if (q == '+') {
                        quantified = true;
                        i++;
                    } else if (q == '{') {
                        quantified = true;
                        min = 0;
                        for (i++; t.charAt(i) >= '0' && t.charAt(i) <= '9'; i++) {
                            min = Math.min(min * 10 + (t.charAt(i) - '0'), Integer.MAX_VALUE);
                        }
                        exact = t.charAt(i) == '}';
                        i = t.indexOf('}', i) + 1;
                    }
                    if (quantified && i < n && (t.charAt(i) == '?' || t.charAt(i) == '+')) {
                        lazy = t.charAt(i) == '?';
                        i++;
                    }
                }
                if (group && groupKind != LOOKAHEAD && groupNullable >= 2) {
                    places.add(start);
                    counted = true;
                }
                if (quantified && nullable && min == 0) {
                    places.add(start);
                    counted = true;
                }
                if (quantified && nullable && min >= 2 && group && groupKind == GROUP && plain) {
                    places.add(groupBody);
                }
                int d = f.depth;
                f.plain[d] &= plain && !(quantified && !exact);
                f.terms[d]++;
                f.onlyCharacters[d] &= character && !quantified;
                if (f.first[d] == NO_TERM && !(passed && !quantified) && !(counted && quantified && min == 0 && lazy)) {
                    // A lazy repetition that can run no times goes to the term after it first, as a lookbehind of
                    // one character that held does.
                    f.first[d] = counted ? COUNTED : SILENT;
                }
                f.alternativeNullable[d] &= nullable || (quantified && min == 0);
            }
            if (f.depth != 0) {
                throw new IllegalStateException("a translation with a group that is not closed: " + t);
            }
            endAlternative(f);
            if (f.firstOfAll[0] != COUNTED) {
                places.add(0);
            }
            if (places.count == 0) {
                return t;
            }
            int[] at = Arrays.copyOf(places.at, places.count);
            Arrays.sort(at);
            StringBuilder sb = new StringBuilder(n + TICK.length() * at.length);
            int from = 0;
            for (int k = 0; k < at.length; k++) {
                if (k > 0 && at[k] == at[k - 1]) {
                    continue;
                }
                sb.append(t, from, at[k]).append(TICK);
                from = at[k];
            }
            return sb.append(t, from, n).toString();
        }

        /** Closes the alternative being read in the innermost frame. */
        private static void endAlternative(Frames f) {
            int d = f.depth;
            if (f.alternativeNullable[d]) {
                f.nullableAlternatives[d]++;
            }
            if (f.alternatives[d]++ == 0) {
                // The engine tries the alternatives in order, so the first step of a group is that of the first.
                f.firstOfAll[d] = f.first[d] == COUNTED ? COUNTED : SILENT;
            }
            f.alternativeNullable[d] = true;
            f.first[d] = NO_TERM;
        }
    }

    /**
     * Checks that a string is a valid ECMA-262 regular expression with the u flag (the {@code regex} format), reading
     * it as the translator does but building nothing: allocates nothing once its buffers have grown. Not thread-safe;
     * one per evaluator.
     */
    static final class Validator {
        /** Thrown (shared) on the first error. */
        private static final IllegalArgumentException INVALID = new IllegalArgumentException("invalid");

        private CharSequence p;
        private int i;
        private int n;
        private int groupCount;
        private int depth;
        /** Group names: for each, its range of the pattern, then the range of {@link #paths} that holds its path. */
        private int[] names = new int[16];
        private int nameCount;
        /** The paths of the group names: the alternatives that enclose each, as disjunction and alternative. */
        private int[] paths = new int[16];
        private int pathsLength;
        /** The alternatives that enclose the current position, innermost last. */
        private int[] path = new int[16];
        private int pathLength;
        private int disjunctions;
        /** Named references, as ranges of the pattern. */
        private int[] refs = new int[8];
        private int refCount;
        /** Where the character of a group name that {@link #nameChar} read ends. */
        private int nameNext;

        boolean isValid(CharSequence pattern) {
            p = pattern;
            i = 0;
            n = pattern.length();
            depth = 0;
            nameCount = 0;
            pathsLength = 0;
            pathLength = 0;
            disjunctions = 0;
            refCount = 0;
            try {
                groupCount = countGroups();
                disjunction();
                if (i < n) {
                    return false;
                }
                for (int r = 0; r < refCount; r++) {
                    if (findName(refs[2 * r], refs[2 * r + 1], 0) < 0) {
                        return false;
                    }
                }
                return true;
            } catch (IllegalArgumentException e) {
                return false;
            } finally {
                p = null;
            }
        }

        private int countGroups() {
            int count = 0;
            boolean inClass = false;
            for (int k = 0; k < n; k++) {
                char c = p.charAt(k);
                if (c == '\\') {
                    k++;
                } else if (c == '[') {
                    inClass = true;
                } else if (c == ']') {
                    inClass = false;
                } else if (c == '(' && !inClass) {
                    if (k + 1 >= n || p.charAt(k + 1) != '?') {
                        count++;
                    } else if (k + 3 < n && p.charAt(k + 2) == '<' && p.charAt(k + 3) != '='
                            && p.charAt(k + 3) != '!') {
                        count++;
                    }
                }
            }
            return count;
        }

        private int peek() {
            return i < n ? Character.codePointAt(p, i) : -1;
        }

        private boolean eat(char c) {
            if (i < n && p.charAt(i) == c) {
                i++;
                return true;
            }
            return false;
        }

        private void disjunction() {
            if (++depth > MAX_NESTING) {
                throw INVALID;
            }
            if (2 * pathLength + 2 > path.length) {
                path = Arrays.copyOf(path, path.length * 2);
            }
            int level = pathLength++;
            path[2 * level] = ++disjunctions;
            path[2 * level + 1] = 0;
            alternative();
            while (eat('|')) {
                path[2 * level + 1]++;
                alternative();
            }
            pathLength--;
            depth--;
        }

        private void alternative() {
            while (i < n && peek() != '|' && peek() != ')') {
                term();
            }
        }

        private void term() {
            int c = peek();
            boolean quantifiable = true;
            switch (c) {
                case '^':
                case '$':
                    i++;
                    quantifiable = false;
                    break;
                case '(':
                    quantifiable = group();
                    break;
                case '.':
                    i++;
                    break;
                case '[':
                    characterClass();
                    break;
                case '\\':
                    quantifiable = atomEscape();
                    break;
                case '*':
                case '+':
                case '?':
                case '{':
                case ']':
                case '}':
                    throw INVALID;
                default:
                    i += Character.charCount(c);
                    break;
            }
            if (quantifier() && !quantifiable) {
                throw INVALID;
            }
        }

        private boolean looksLikeQuantifier() {
            int k = i + 1;
            int digits = 0;
            while (k < n && p.charAt(k) >= '0' && p.charAt(k) <= '9') {
                k++;
                digits++;
            }
            if (digits == 0) {
                return false;
            }
            if (k < n && p.charAt(k) == '}') {
                return true;
            }
            if (k < n && p.charAt(k) == ',') {
                k++;
                while (k < n && p.charAt(k) >= '0' && p.charAt(k) <= '9') {
                    k++;
                }
                return k < n && p.charAt(k) == '}';
            }
            return false;
        }

        /** Reads a run of decimal digits, which saturates as the translator's does. */
        private long number() {
            int start = i;
            long v = 0;
            while (i < n && p.charAt(i) >= '0' && p.charAt(i) <= '9') {
                v = Math.min(v * 10 + (p.charAt(i) - '0'), MAX_COUNT);
                i++;
            }
            if (start == i) {
                throw INVALID;
            }
            while (start < i - 1 && p.charAt(start) == '0') {
                start++;
            }
            numberStart = start;
            numberEnd = i;
            return v;
        }

        /** Where the digits of the number read last stand in the pattern, without the zeros it starts with. */
        private int numberStart;
        private int numberEnd;

        /** Whether the number read last is less than the one at {@code start} to {@code end}, as they are written. */
        private boolean lessThan(int start, int end) {
            if (numberEnd - numberStart != end - start) {
                return numberEnd - numberStart < end - start;
            }
            for (int k = 0; k < end - start; k++) {
                if (p.charAt(numberStart + k) != p.charAt(start + k)) {
                    return p.charAt(numberStart + k) < p.charAt(start + k);
                }
            }
            return false;
        }

        private boolean quantifier() {
            int c = peek();
            if (c == '*' || c == '+' || c == '?') {
                i++;
            } else if (c == '{' && looksLikeQuantifier()) {
                i++;
                number();
                int minStart = numberStart;
                int minEnd = numberEnd;
                boolean ordered = true;
                if (eat(',') && peek() != '}') {
                    number();
                    ordered = !lessThan(minStart, minEnd);
                }
                if (!eat('}') || !ordered) {
                    throw INVALID;
                }
            } else {
                return false;
            }
            eat('?');
            return true;
        }

        /** A group or a lookaround; returns whether it may be quantified. */
        private boolean group() {
            i++;
            if (!eat('?') || eat(':')) {
                body();
                return true;
            }
            int c = peek();
            if (c == 'i' || c == 'm' || c == 's' || c == '-') {
                modifiers();
                body();
                return true;
            }
            if (eat('=') || eat('!')) {
                body();
                // Lookaheads are not quantifiable with the u flag.
                return false;
            }
            if (eat('<')) {
                if (eat('=') || eat('!')) {
                    body();
                    return false;
                }
                int start = i;
                groupName();
                int end = i - 1;
                // Two groups may share a name only in separate alternatives of one disjunction.
                for (int k = findName(start, end, 0); k >= 0; k = findName(start, end, k + 1)) {
                    if (!separateAlternatives(names[4 * k + 2], names[4 * k + 3])) {
                        throw INVALID;
                    }
                }
                if (4 * nameCount + 4 > names.length) {
                    names = Arrays.copyOf(names, names.length * 2);
                }
                if (pathsLength + 2 * pathLength > paths.length) {
                    paths = Arrays.copyOf(paths, Math.max(paths.length * 2, pathsLength + 2 * pathLength));
                }
                System.arraycopy(path, 0, paths, pathsLength, 2 * pathLength);
                names[4 * nameCount] = start;
                names[4 * nameCount + 1] = end;
                names[4 * nameCount + 2] = pathsLength;
                names[4 * nameCount + 3] = pathLength;
                nameCount++;
                pathsLength += 2 * pathLength;
                body();
                return true;
            }
            throw INVALID;
        }

        private void body() {
            disjunction();
            if (!eat(')')) {
                throw INVALID;
            }
        }

        /** The modifiers of a group such as {@code (?i:} or {@code (?s-i:}, up to and including the colon. */
        private void modifiers() {
            int seen = 0;
            boolean removing = false;
            while (!eat(':')) {
                int c = peek();
                i++;
                if (c == '-') {
                    if (removing) {
                        throw INVALID;
                    }
                    removing = true;
                    continue;
                }
                int flag = c == 'i' ? 1 : c == 'm' ? 2 : c == 's' ? 4 : 0;
                if (flag == 0 || (seen & flag) != 0) {
                    throw INVALID;
                }
                seen |= flag;
            }
            if (seen == 0) {
                throw INVALID;
            }
        }

        /**
         * Whether a stored path (of {@code length} levels at {@code offset} of {@link #paths}) and the current
         * position lie in different alternatives of some disjunction.
         */
        private boolean separateAlternatives(int offset, int length) {
            for (int k = 0; k < length && k < pathLength && paths[offset + 2 * k] == path[2 * k]; k++) {
                if (paths[offset + 2 * k + 1] != path[2 * k + 1]) {
                    return true;
                }
            }
            return false;
        }

        /** A group name up to and including its '>'. */
        private void groupName() {
            boolean first = true;
            while (true) {
                if (i >= n) {
                    throw INVALID;
                }
                if (p.charAt(i) == '>') {
                    if (first) {
                        throw INVALID;
                    }
                    i++;
                    return;
                }
                int c = nameChar(i);
                i = nameNext;
                if (first ? !EcmaUnicode.isNameStart(c) : !EcmaUnicode.isNamePart(c)) {
                    throw INVALID;
                }
                first = false;
            }
        }

        /**
         * The character of a group name at {@code at}, which may be written as a {@code \}{@code u} escape. Sets
         * {@link #nameNext} to where it ends.
         */
        private int nameChar(int at) {
            int c = Character.codePointAt(p, at);
            at += Character.charCount(c);
            if (c != '\\') {
                nameNext = at;
                return c;
            }
            if (at >= n || p.charAt(at) != 'u') {
                throw INVALID;
            }
            at++;
            if (at < n && p.charAt(at) == '{') {
                int v = 0;
                int start = ++at;
                while (at < n && hexDigit(p.charAt(at)) >= 0) {
                    if (v <= EcmaUnicode.MAX) {
                        v = v * 16 + hexDigit(p.charAt(at));
                    }
                    at++;
                }
                if (at == start || at >= n || p.charAt(at) != '}' || v > EcmaUnicode.MAX) {
                    throw INVALID;
                }
                nameNext = at + 1;
                return v;
            }
            int u = hex4(at);
            if (u < 0) {
                throw INVALID;
            }
            at += 4;
            // A surrogate pair written as two escapes is one code point.
            if (u >= 0xd800 && u <= 0xdbff && at + 6 <= n && p.charAt(at) == '\\' && p.charAt(at + 1) == 'u') {
                int low = hex4(at + 2);
                if (low >= 0xdc00 && low <= 0xdfff) {
                    nameNext = at + 6;
                    return Character.toCodePoint((char) u, (char) low);
                }
            }
            nameNext = at;
            return u;
        }

        private static int hexDigit(char c) {
            if (c >= '0' && c <= '9') {
                return c - '0';
            }
            if (c >= 'a' && c <= 'f') {
                return c - 'a' + 10;
            }
            if (c >= 'A' && c <= 'F') {
                return c - 'A' + 10;
            }
            return -1;
        }

        /** The value of the four hexadecimal digits at {@code at}, or -1. */
        private int hex4(int at) {
            if (at + 4 > n) {
                return -1;
            }
            int v = 0;
            for (int k = 0; k < 4; k++) {
                int d = hexDigit(p.charAt(at + k));
                if (d < 0) {
                    return -1;
                }
                v = v * 16 + d;
            }
            return v;
        }

        /** The index, from {@code from} on, of a group whose name is the one written at p[start, end), or -1. */
        private int findName(int start, int end, int from) {
            for (int k = from; k < nameCount; k++) {
                if (sameName(names[4 * k], names[4 * k + 1], start, end)) {
                    return k;
                }
            }
            return -1;
        }

        /** Whether two valid group names of the pattern are the same name, however their characters are written. */
        private boolean sameName(int a, int aEnd, int b, int bEnd) {
            while (a < aEnd && b < bEnd) {
                int ca = nameChar(a);
                a = nameNext;
                int cb = nameChar(b);
                b = nameNext;
                if (ca != cb) {
                    return false;
                }
            }
            return a == aEnd && b == bEnd;
        }

        private boolean atomEscape() {
            i++;
            if (i >= n) {
                throw INVALID;
            }
            int c = peek();
            if (c == 'b' || c == 'B') {
                i++;
                return false;
            }
            if (c == 'k') {
                i++;
                if (!eat('<')) {
                    throw INVALID;
                }
                int start = i;
                groupName();
                if (2 * refCount + 2 > refs.length) {
                    refs = Arrays.copyOf(refs, refs.length * 2);
                }
                refs[2 * refCount] = start;
                refs[2 * refCount + 1] = i - 1;
                refCount++;
                return true;
            }
            if (c >= '1' && c <= '9') {
                if (number() > groupCount) {
                    throw INVALID;
                }
                return true;
            }
            if (classEscape(c)) {
                return true;
            }
            characterEscape(false);
            return true;
        }

        /** A class escape at i (consumed); false when there is none. */
        private boolean classEscape(int c) {
            switch (c) {
                case 'd':
                case 'D':
                case 'w':
                case 'W':
                case 's':
                case 'S':
                    i++;
                    return true;
                case 'p':
                case 'P':
                    i++;
                    property();
                    return true;
                default:
                    return false;
            }
        }

        private void property() {
            if (!eat('{')) {
                throw INVALID;
            }
            int start = i;
            while (i < n && p.charAt(i) != '}') {
                i++;
            }
            int end = i;
            if (!eat('}') || !EcmaUnicode.isProperty(p, start, end)) {
                throw INVALID;
            }
        }

        private boolean hex(int digits) {
            if (i + digits > n) {
                return false;
            }
            for (int k = 0; k < digits; k++) {
                if (hexDigit(p.charAt(i + k)) < 0) {
                    return false;
                }
            }
            i += digits;
            return true;
        }

        /** A character escape after the backslash at i (consumed). */
        private void characterEscape(boolean inClass) {
            int c = peek();
            i += Character.charCount(c);
            switch (c) {
                case 'f':
                case 'n':
                case 'r':
                case 't':
                case 'v':
                    return;
                case 'c': {
                    int l = peek();
                    if ((l >= 'a' && l <= 'z') || (l >= 'A' && l <= 'Z')) {
                        i++;
                        return;
                    }
                    throw INVALID;
                }
                case '0':
                    if (i < n && p.charAt(i) >= '0' && p.charAt(i) <= '9') {
                        throw INVALID;
                    }
                    return;
                case 'x':
                    if (!hex(2)) {
                        throw INVALID;
                    }
                    return;
                case 'u':
                    if (eat('{')) {
                        long v = 0;
                        int start = i;
                        while (i < n && hexDigit(p.charAt(i)) >= 0) {
                            if (v <= EcmaUnicode.MAX) {
                                v = v * 16 + hexDigit(p.charAt(i));
                            }
                            i++;
                        }
                        if (start == i || !eat('}') || v > EcmaUnicode.MAX) {
                            throw INVALID;
                        }
                        return;
                    }
                    if (!hex(4)) {
                        throw INVALID;
                    }
                    return;
                default:
                    break;
            }
            // Identity escapes: with the u flag, only syntax characters and '/' (and '-' in a class).
            if ("^$\\.*+?()[]{}|/".indexOf(c) >= 0 || (inClass && c == '-')) {
                return;
            }
            throw INVALID;
        }

        private void characterClass() {
            i++;
            eat('^');
            while (true) {
                if (i >= n) {
                    throw INVALID;
                }
                if (peek() == ']') {
                    i++;
                    return;
                }
                int first = classAtom();
                if (peek() == '-' && i + 1 < n && p.charAt(i + 1) != ']') {
                    i++;
                    int second = classAtom();
                    if (first < 0 || second < 0 || second < first) {
                        throw INVALID;
                    }
                }
            }
        }

        /** A class atom: its code point, or -1 for a class escape. */
        private int classAtom() {
            int c = peek();
            if (c == '\\') {
                i++;
                if (i >= n) {
                    throw INVALID;
                }
                int e = peek();
                if (e == 'b') {
                    i++;
                    return '\b';
                }
                if (e == '-') {
                    i++;
                    return '-';
                }
                if (classEscape(e)) {
                    return -1;
                }
                int at = i;
                characterEscape(true);
                return escapedValue(at);
            }
            i += Character.charCount(c);
            return c;
        }

        /** The code point of the character escape that starts at {@code at} (already validated). */
        private int escapedValue(int at) {
            int c = Character.codePointAt(p, at);
            switch (c) {
                case 'f':
                    return '\f';
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'v':
                    return 0x0b;
                case 'c':
                    return p.charAt(at + 1) % 32;
                case '0':
                    return 0;
                case 'x':
                    return hexValue(at + 1, at + 3);
                case 'u':
                    if (p.charAt(at + 1) == '{') {
                        return hexValue(at + 2, i - 1);
                    }
                    int u = hexValue(at + 1, at + 5);
                    // A surrogate pair written as two escapes is one code point.
                    if (u >= 0xd800 && u <= 0xdbff && i + 6 <= n && p.charAt(i) == '\\' && p.charAt(i + 1) == 'u') {
                        int low = hex4(i + 2);
                        if (low >= 0xdc00 && low <= 0xdfff) {
                            i += 6;
                            return Character.toCodePoint((char) u, (char) low);
                        }
                    }
                    return u;
                default:
                    return c;
            }
        }

        /** The value of the hexadecimal digits of p[from, to), which saturates above the last code point. */
        private int hexValue(int from, int to) {
            int v = 0;
            for (int k = from; k < to; k++) {
                if (v <= EcmaUnicode.MAX) {
                    v = v * 16 + hexDigit(p.charAt(k));
                }
            }
            return v;
        }
    }
}
