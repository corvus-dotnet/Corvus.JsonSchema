package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.BitSet;
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
 * schemas hold such patterns (identity escapes like {@code \&}, lone braces). Whichever grammar accepts the pattern,
 * it matches code points. {@link #isValid}, which backs the {@code regex} format, accepts only the {@code u} flag
 * grammar.
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
 *       of a lookbehind by code point. Without it a lookbehind counts UTF-16 code units.
 * </ul>
 *
 * <p><b>What is refused.</b> A backreference means "the text the group matched when it last took part, or nothing".
 * {@code java.util.regex} differs from ECMA-262 on when a group stops having taken part: it keeps what a group
 * captured in an earlier iteration of a repetition or in a lookaround after which the match failed, and it runs a
 * lookbehind forwards where ECMA-262 runs it backwards. The translator shows, for each backreference, that these
 * differences cannot be seen, and writes the backreference in a form that is exact. Where it cannot show it, the
 * pattern is refused rather than given an answer that may be wrong, and {@link #unsupported} says why. The reasons
 * are the {@code REFUSED_} constants. A pattern with no backreference is never refused for them.
 */
final class EcmaRegex {
    private EcmaRegex() {
    }

    /** The backreference is in a lookbehind, which ECMA-262 matches backwards. */
    static final String REFUSED_BACKREFERENCE_IN_LOOKBEHIND = "a backreference inside a lookbehind";
    /** The group is in a lookbehind, which ECMA-262 matches backwards, so it can capture other text. */
    static final String REFUSED_GROUP_IN_LOOKBEHIND = "a backreference to a group inside a lookbehind";
    /** The repetition that holds the backreference can skip the group, which unsets it only in ECMA-262. */
    static final String REFUSED_GROUP_IN_REPETITION =
            "a backreference to a group that an iteration of a repetition can skip";
    /** The group is in a lookaround that may not have matched, after which java.util.regex can keep its text. */
    static final String REFUSED_GROUP_IN_LOOKAROUND =
            "a backreference to a group of a lookaround that may not have matched";
    /** The group is in a repetition whose body can match nothing, which the two engines end differently. */
    static final String REFUSED_GROUP_IN_EMPTY_REPETITION =
            "a backreference to a group of a repetition that can match the empty string";
    /** The backreference ignores case, and the group can hold a character java.util.regex compares differently. */
    static final String REFUSED_CASE_INSENSITIVE_BACKREFERENCE =
            "a case-insensitive backreference to text that java.util.regex compares differently";
    /** {@code java.util.regex} did not compile the translation. */
    static final String REFUSED_BY_ENGINE = "a pattern java.util.regex does not compile";

    /** Groups may nest this deep. It bounds the recursion of the readers. */
    static final int MAX_NESTING = 200;

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

    /** Whether a string is a valid ECMA-262 regular expression with the u flag (the {@code regex} format). */
    static boolean isValid(CharSequence pattern) {
        return new Validator().isValid(pattern);
    }

    private static String translation(String pattern) {
        Parser parser = new Parser(pattern, true);
        Node root;
        try {
            root = parser.parse();
        } catch (IllegalArgumentException unicodeError) {
            parser = new Parser(pattern, false);
            root = parser.parse();
        }
        return new Emitter(parser, root).translate();
    }

    /** Thrown for a valid pattern that cannot be run with the same meaning. The message is the reason. */
    private static final class Unsupported extends IllegalArgumentException {
        private static final long serialVersionUID = 1L;

        Unsupported(String reason) {
            super(reason);
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

        Parser(String pattern, boolean unicode) {
            this.pattern = pattern;
            this.p = pattern.codePoints().toArray();
            this.unicode = unicode;
        }

        private IllegalArgumentException error(String message) {
            return new IllegalArgumentException(message + " at " + i + " in " + pattern);
        }

        Node parse() {
            scanGroups();
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
                throw error("groups nested too deeply");
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
                result = new Node(ALT);
                result.subs = alternatives.toArray(NO_NODES);
            }
            pathLength--;
            depth--;
            return result;
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
            node.codePoint = c;
            return node;
        }

        private static Node setNode(int[] set) {
            Node node = new Node(SET);
            node.set = set;
            return node;
        }

        /** The set that also holds, inside a case-insensitive group, every character equivalent to a member. */
        private int[] folded(int[] set) {
            return ignoreCase ? EcmaUnicode.foldClosure(set, unicode) : set;
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
            return (int) v;
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
                if (eat(',')) {
                    repeat.max = peek() == '}' ? UNBOUNDED : number();
                }
                if (!eat('}')) {
                    throw error("malformed quantifier");
                }
                if (repeat.max != UNBOUNDED && repeat.max < repeat.min) {
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
                    c = unicodeEscape(true);
                    if (c < 0) {
                        throw error("invalid group name");
                    }
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
                    return c == 'P' ? EcmaUnicode.complement(set) : set;
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
                    int u = unicodeEscape(unicode);
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
            // allows any character.
            if (unicode && "^$\\.*+?()[]{}|/".indexOf(c) < 0 && !(inClass && c == '-')) {
                throw error("invalid escape");
            }
            return c;
        }

        /**
         * Reads what follows the u of a Unicode escape and returns the code point, or -1, reading nothing, when it
         * is not a well-formed escape. With {@code braces} it accepts the form with braces. A surrogate pair written
         * as two escapes is one code point.
         */
        private int unicodeEscape(boolean braces) {
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
            if (u >= 0xD800 && u <= 0xDBFF && i + 6 <= p.length && p[i] == '\\' && p[i + 1] == 'u') {
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

        private final boolean unicode;
        private final Node root;
        private final Node[] groupNodes;
        /** For each group, whether some backreference reads it, and whether one reads it unsure that it is set. */
        private final boolean[] referenced;
        private final boolean[] needsMarker;
        /** For each group: whether a lookbehind or any lookaround encloses it, and the repetitions that enclose it. */
        private final boolean[] inLookbehind;
        private final boolean[] inLookaround;
        private final Node[][] repeatsAround;
        /** For each group, the number of its group in the translation, and of the group that marks it took part. */
        private final int[] javaGroup;
        private final int[] javaMarker;
        private final StringBuilder out = new StringBuilder();
        private int javaGroups;
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
            repeatsAround = new Node[n][];
            javaGroup = new int[n];
            javaMarker = new int[n];
        }

        /** The translation, compiled once to be sure {@code java.util.regex} takes it. */
        String translate() {
            flow(root, new BitSet(), new BitSet(), false);
            survey(root, false, false, new ArrayList<>());
            String translated = write();
            if (compiles(translated)) {
                return translated;
            }
            if (hasLookbehind) {
                // java.util.regex cannot bound some lookbehinds whose length is bounded (a repeated group with
                // alternatives, for one). The form that searches for the start needs no bound.
                searchingLookbehind = true;
                translated = write();
                if (compiles(translated)) {
                    return translated;
                }
            }
            throw new Unsupported(REFUSED_BY_ENGINE);
        }

        private static boolean compiles(String translated) {
            try {
                Pattern.compile(translated);
                return true;
            } catch (PatternSyntaxException | StackOverflowError e) {
                return false;
            }
        }

        private String write() {
            out.setLength(0);
            javaGroups = 0;
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
         * it the meaning ECMA-262 does, and refuses the pattern when that cannot be shown.
         */
        private void survey(Node n, boolean lookbehind, boolean lookaround, List<Node> repeats) {
            switch (n.kind) {
                case GROUP:
                    if (n.group != 0) {
                        inLookbehind[n.group] = lookbehind;
                        inLookaround[n.group] = lookaround;
                        repeatsAround[n.group] = repeats.toArray(NO_NODES);
                    }
                    survey(n.subs[0], lookbehind, lookaround, repeats);
                    break;
                case REPEAT:
                    // A repetition of at most once has no earlier iteration.
                    if (n.max == UNBOUNDED || n.max > 1) {
                        repeats.add(n);
                        survey(n.subs[0], lookbehind, lookaround, repeats);
                        repeats.remove(repeats.size() - 1);
                    } else {
                        survey(n.subs[0], lookbehind, lookaround, repeats);
                    }
                    break;
                case LOOK:
                    survey(n.subs[0], lookbehind || n.behind, true, repeats);
                    break;
                case BACKREF:
                    decide(n, lookbehind, repeats);
                    break;
                default:
                    for (Node sub : n.subs) {
                        survey(sub, lookbehind, lookaround, repeats);
                    }
                    break;
            }
        }

        /**
         * Decides one backreference. Outside a lookbehind ECMA-262 evaluates a pattern from left to right, so a group
         * that can have taken part comes before the backreference, and what encloses it is known.
         *
         * <p>A group that has taken part on every path to the backreference was set on the path
         * {@code java.util.regex} is on, so its text is the one ECMA-262 means. The two engines differ there in one
         * case, a repetition whose body can match the empty string: ECMA-262 refuses an empty iteration and
         * {@code java.util.regex} takes one, which can leave a group of the body with other text.
         *
         * <p>A group that may have taken part needs a test, and the test reads what {@code java.util.regex} kept. It
         * is sound only where that engine forgets a group when ECMA-262 does, which is on backtracking out of the
         * group. It is not sound for a group in a lookaround, which {@code java.util.regex} does not undo when the
         * match fails later. Nor is it sound for a group in a repetition, as ECMA-262 unsets the group when an
         * iteration starts and {@code java.util.regex} keeps it. A repetition that ends before the backreference is
         * therefore written with its last iteration apart (see {@link #repeat}), so that the group is one only the
         * last iteration sets. A repetition that holds the backreference too cannot be, and is refused.
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
                    throw new Unsupported(REFUSED_BACKREFERENCE_IN_LOOKBEHIND);
                }
                if (inLookbehind[g]) {
                    throw new Unsupported(REFUSED_GROUP_IN_LOOKBEHIND);
                }
                if (n.statuses[k] == DEFINITE) {
                    for (Node repeat : repeatsAround[g]) {
                        if (!repeats.contains(repeat) && minLength(repeat.subs[0]) == 0) {
                            throw new Unsupported(REFUSED_GROUP_IN_EMPTY_REPETITION);
                        }
                    }
                } else {
                    if (inLookaround[g]) {
                        throw new Unsupported(REFUSED_GROUP_IN_LOOKAROUND);
                    }
                    for (Node repeat : repeatsAround[g]) {
                        if (repeats.contains(repeat)) {
                            throw new Unsupported(REFUSED_GROUP_IN_REPETITION);
                        }
                        if (minLength(repeat.subs[0]) == 0) {
                            throw new Unsupported(REFUSED_GROUP_IN_EMPTY_REPETITION);
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
         * fold to. Any other group is refused, as {@code java.util.regex} has no comparison that is the Canonicalize
         * of ECMA-262. A group that can hold a character beyond the Basic Multilingual Plane is refused too, as the
         * comparison of Java 17 miscounts them.
         */
        private boolean comparesIgnoringAsciiCase(int g) {
            int[] captured = capturedSet(groupNodes[g]);
            int[] cased = EcmaUnicode.cased(unicode);
            if (!EcmaUnicode.intersects(captured, cased)) {
                return false;
            }
            int[] variants = EcmaUnicode.foldClosure(intersection(captured, cased), unicode);
            if (EcmaUnicode.intersects(variants, NOT_ASCII_LETTER) || EcmaUnicode.intersects(captured, SUPPLEMENTARY)) {
                throw new Unsupported(REFUSED_CASE_INSENSITIVE_BACKREFERENCE);
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
                case BACKREF:
                    max = INFINITE;
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
            switch (n.kind) {
                case EMPTY:
                    break;
                case CHAR:
                    literal(n.codePoint);
                    break;
                case SET:
                    if (n.parts != null) {
                        engineSet(n);
                    } else {
                        set(n.set);
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
                case LOOK:
                    if (n.behind) {
                        lookbehind(n);
                    } else {
                        out.append(n.negative ? "(?!" : "(?=");
                        node(n.subs[0]);
                        out.append(')');
                    }
                    break;
                case BACKREF:
                    backreference(n);
                    break;
                default:
                    throw new IllegalStateException();
            }
        }

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
            out.append(n.partsNegated ? "[^" : "[");
            for (int[] part : n.parts) {
                EcmaUnicode.EngineClass engine = EcmaUnicode.engineClass(part);
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
            out.append(']');
        }

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
         * (such a repetition is refused), so every iteration is one ECMA-262 takes.
         */
        private void repeat(Node n) {
            Node atom = n.subs[0];
            if (!n.lastApart) {
                atom(atom);
                quantifier(n.min, n.max, n.lazy);
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
         * <p>Running the body forwards where ECMA-262 runs it backwards finds a match exactly when ECMA-262 does. The
         * two directions can differ only in what the groups of the body capture, and a backreference to a group of a
         * lookbehind, or inside a lookbehind, is refused.
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
            out.append("(?=(").append(ANY).append("*))").append(n.negative ? "(?<!" : "(?<=").append("(?=(?:");
            node(body);
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
            return v;
        }

        private boolean quantifier() {
            int c = peek();
            if (c == '*' || c == '+' || c == '?') {
                i++;
            } else if (c == '{' && looksLikeQuantifier()) {
                i++;
                long min = number();
                long max = min;
                boolean open = false;
                if (eat(',')) {
                    if (peek() == '}') {
                        open = true;
                    } else {
                        max = number();
                    }
                }
                if (!eat('}') || (!open && max < min)) {
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
