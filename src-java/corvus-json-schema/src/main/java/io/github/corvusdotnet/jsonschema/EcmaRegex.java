package io.github.corvusdotnet.jsonschema;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.regex.PatternSyntaxException;

/**
 * Translates ECMA-262 regular expressions (with the {@code u} flag, as JSON Schema's {@code pattern} specifies) to
 * {@code java.util.regex} patterns with the same meaning, validating them as ECMA-262 does.
 *
 * <p>Both engines match code points, so the translation is mostly about the constructs whose meaning differs:
 * {@code .}, {@code $}, {@code \d}, {@code \w}, {@code \s}, {@code \b} (ASCII and ECMA's line terminators and white
 * space), backreferences to groups that did not participate (which match the empty string in ECMA-262), named groups
 * (whose names Java restricts), Unicode property names, and class syntax that Java reads differently ({@code [},
 * {@code &&} and empty classes). Every literal is written as a code point escape, so no character is read as syntax.
 *
 * <p>A pattern that is not valid with the {@code u} flag is read without it, as many validators accept such patterns
 * (identity escapes like {@code \&}, lone braces). A lookbehind of unbounded length, which Java cannot run, is a
 * compilation error.
 */
final class EcmaRegex {
    private EcmaRegex() {
    }

    /** The translation of an ECMA-262 pattern, or null when it is not a valid one. */
    static String translate(String pattern) {
        try {
            return new Translator(pattern, true).translate();
        } catch (IllegalArgumentException unicodeError) {
            try {
                return new Translator(pattern, false).translate();
            } catch (IllegalArgumentException e) {
                return null;
            }
        }
    }

    /**
     * The translator's verdict on a pattern read with the u flag: 1 valid, 0 not ECMA-262, 2 valid ECMA-262 that
     * {@code java.util.regex} cannot run (for tests).
     */
    static int translatorVerdict(String pattern) {
        try {
            new Translator(pattern, true).translate();
            return 1;
        } catch (IllegalArgumentException e) {
            return e.getCause() instanceof java.util.regex.PatternSyntaxException ? 2 : 0;
        }
    }

    /** Whether a string is a valid ECMA-262 regular expression with the u flag (the {@code regex} format). */
    static boolean isValid(CharSequence pattern) {
        return new Validator().isValid(pattern);
    }

    // ECMA-262 classes.
    static final String DIGIT = "0-9";
    static final String WORD = "a-zA-Z0-9_";
    /** WhiteSpace and LineTerminator. */
    static final String SPACE = "\\x{9}-\\x{D}\\x{20}\\x{A0}\\x{1680}\\x{2000}-\\x{200A}\\x{2028}\\x{2029}\\x{202F}"
            + "\\x{205F}\\x{3000}\\x{FEFF}";
    static final String NOT_LINE_TERMINATOR = "[^\\x{A}\\x{D}\\x{2028}\\x{2029}]";
    static final String ANY = "[\\x{0}-\\x{10FFFF}]";

    private static final Map<String, String> CATEGORIES = new HashMap<>();
    private static final Map<String, String> BINARY = new HashMap<>();

    static {
        String[] categories = {
            "Letter", "L", "Lowercase_Letter", "Ll", "Uppercase_Letter", "Lu", "Titlecase_Letter", "Lt",
            "Cased_Letter", "LC", "Modifier_Letter", "Lm", "Other_Letter", "Lo", "Mark", "M", "Combining_Mark", "M",
            "Nonspacing_Mark", "Mn", "Spacing_Mark", "Mc", "Enclosing_Mark", "Me", "Number", "N", "Decimal_Number",
            "Nd", "digit", "Nd", "Letter_Number", "Nl", "Other_Number", "No", "Punctuation", "P", "punct", "P",
            "Connector_Punctuation", "Pc", "Dash_Punctuation", "Pd", "Open_Punctuation", "Ps", "Close_Punctuation",
            "Pe", "Initial_Punctuation", "Pi", "Final_Punctuation", "Pf", "Other_Punctuation", "Po", "Symbol", "S",
            "Math_Symbol", "Sm", "Currency_Symbol", "Sc", "Modifier_Symbol", "Sk", "Other_Symbol", "So", "Separator",
            "Z", "Space_Separator", "Zs", "Line_Separator", "Zl", "Paragraph_Separator", "Zp", "Other", "C",
            "Control", "Cc", "cntrl", "Cc", "Format", "Cf", "Surrogate", "Cs", "Private_Use", "Co", "Unassigned",
            "Cn",
        };
        for (int i = 0; i < categories.length; i += 2) {
            CATEGORIES.put(categories[i], categories[i + 1]);
        }
        for (String s : new String[] {
            "L", "Ll", "Lu", "Lt", "LC", "Lm", "Lo", "M", "Mn", "Mc", "Me", "N", "Nd", "Nl", "No", "P", "Pc", "Pd", "Ps",
            "Pe", "Pi", "Pf", "Po", "S", "Sm", "Sc", "Sk", "So", "Z", "Zs", "Zl", "Zp", "C", "Cc", "Cf", "Cs", "Co",
            "Cn",
        }) {
            CATEGORIES.put(s, s);
        }
        // Binary properties, as class content.
        BINARY.put("ASCII", "\\x{0}-\\x{7F}");
        BINARY.put("Any", "\\x{0}-\\x{10FFFF}");
        BINARY.put("Assigned", "\\P{Cn}");
        BINARY.put("Alphabetic", "\\p{IsAlphabetic}");
        BINARY.put("Alpha", "\\p{IsAlphabetic}");
        BINARY.put("White_Space", SPACE);
        BINARY.put("space", SPACE);
        BINARY.put("Lowercase", "\\p{IsLowercase}");
        BINARY.put("Lower", "\\p{IsLowercase}");
        BINARY.put("Uppercase", "\\p{IsUppercase}");
        BINARY.put("Upper", "\\p{IsUppercase}");
        BINARY.put("Ideographic", "\\p{IsIdeographic}");
        BINARY.put("Ideo", "\\p{IsIdeographic}");
        BINARY.put("Hex_Digit", "0-9A-Fa-f\\x{FF10}-\\x{FF19}\\x{FF21}-\\x{FF26}\\x{FF41}-\\x{FF46}");
        BINARY.put("ASCII_Hex_Digit", "0-9A-Fa-f");
        BINARY.put("Join_Control", "\\x{200C}\\x{200D}");
        BINARY.put("Noncharacter_Code_Point", "\\p{IsNoncharacter_Code_Point}");
        BINARY.put("ID_Start", "\\p{javaUnicodeIdentifierStart}");
        BINARY.put("ID_Continue", "[\\p{javaUnicodeIdentifierPart}&&[^\\x{0}-\\x{8}\\x{E}-\\x{1B}\\x{7F}-\\x{9F}]]");
        BINARY.put("Emoji", "\\p{IsEmoji}");
        BINARY.put("Emoji_Presentation", "\\p{IsEmoji_Presentation}");
        BINARY.put("Emoji_Modifier", "\\p{IsEmoji_Modifier}");
        BINARY.put("Emoji_Modifier_Base", "\\p{IsEmoji_Modifier_Base}");
        BINARY.put("Emoji_Component", "\\p{IsEmoji_Component}");
        BINARY.put("Extended_Pictographic", "\\p{IsExtended_Pictographic}");
    }

    /** A recursive-descent reader of an ECMA-262 pattern that writes the Java pattern as it goes. */
    private static final class Translator {
        private final String p;
        private final boolean unicode;
        private int i;
        private final StringBuilder out = new StringBuilder();
        /** Capturing groups in ECMA order: each one's Java group number, and the number of its participation marker. */
        private final List<int[]> groups = new ArrayList<>();
        private final Map<String, Integer> names = new HashMap<>();
        private int javaGroups;
        private int ecmaGroupCount;
        /** Backreferences, patched once the group numbers are known. */
        private final List<Backref> backrefs = new ArrayList<>();

        /** A backreference: its offset in the output, and its ECMA group number or name. */
        private static final class Backref {
            final int offset;
            final int group;
            final String name;

            Backref(int offset, int group, String name) {
                this.offset = offset;
                this.group = group;
                this.name = name;
            }
        }

        Translator(String p, boolean unicode) {
            this.p = p;
            this.unicode = unicode;
        }

        private IllegalArgumentException error(String message) {
            return new IllegalArgumentException(message + " at " + i + " in " + p);
        }

        String translate() {
            ecmaGroupCount = countGroups();
            disjunction();
            if (i < p.length()) {
                throw error(p.charAt(i) == ')' ? "unmatched ')'" : "unexpected character");
            }
            for (Backref b : backrefs) {
                if (b.name != null && !names.containsKey(b.name)) {
                    throw error("unknown group name");
                }
            }
            // Backreferences become "the group, if it took part, else nothing" (ECMA-262's meaning), written once
            // the Java numbers of the groups and their participation markers are known.
            StringBuilder result = new StringBuilder(out.length() + backrefs.size() * 24);
            int last = 0;
            for (Backref b : backrefs) {
                result.append(out, last, b.offset);
                int[] g = groups.get((b.name != null ? names.get(b.name) : b.group) - 1);
                result.append("(?:(?=\\").append(g[1]).append(")\\").append(g[0])
                        .append("|(?!\\").append(g[1]).append("))");
                last = b.offset;
            }
            result.append(out, last, out.length());
            String translated = result.toString();
            try {
                Pattern.compile(translated);
            } catch (PatternSyntaxException e) {
                throw new IllegalArgumentException(e.getMessage(), e);
            }
            return translated;
        }

        /** The number of capturing groups in the pattern (for deciding what a decimal escape is). */
        private int countGroups() {
            int count = 0;
            boolean inClass = false;
            for (int k = 0; k < p.length(); k++) {
                char c = p.charAt(k);
                if (c == '\\') {
                    k++;
                } else if (c == '[') {
                    inClass = true;
                } else if (c == ']') {
                    inClass = false;
                } else if (c == '(' && !inClass) {
                    if (k + 1 >= p.length() || p.charAt(k + 1) != '?') {
                        count++;
                    } else if (k + 2 < p.length() && p.charAt(k + 2) == '<'
                            && k + 3 < p.length() && p.charAt(k + 3) != '=' && p.charAt(k + 3) != '!') {
                        count++;
                    }
                }
            }
            return count;
        }

        private boolean more() {
            return i < p.length();
        }

        private int peek() {
            return i < p.length() ? p.codePointAt(i) : -1;
        }

        private boolean eat(char c) {
            if (i < p.length() && p.charAt(i) == c) {
                i++;
                return true;
            }
            return false;
        }

        private void disjunction() {
            alternative();
            while (eat('|')) {
                out.append('|');
                alternative();
            }
        }

        private void alternative() {
            while (more() && peek() != '|' && peek() != ')') {
                term();
            }
        }

        private void term() {
            int c = peek();
            int start = out.length();
            boolean quantifiable = true;
            switch (c) {
                case '^':
                    i++;
                    out.append('^');
                    quantifiable = false;
                    break;
                case '$':
                    i++;
                    out.append("\\z");
                    quantifiable = false;
                    break;
                case '(':
                    quantifiable = group();
                    break;
                case '.':
                    i++;
                    out.append(NOT_LINE_TERMINATOR);
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
                    throw error("nothing to repeat");
                case '{':
                    if (unicode || looksLikeQuantifier()) {
                        throw error("nothing to repeat");
                    }
                    i++;
                    literal('{');
                    break;
                case ']':
                case '}':
                    if (unicode) {
                        throw error("lone " + (char) c);
                    }
                    i++;
                    literal(c);
                    break;
                default:
                    i += Character.charCount(c);
                    literal(c);
                    break;
            }
            if (quantifier()) {
                if (!quantifiable) {
                    throw error("nothing to repeat");
                }
                // Java quantifiers apply to the last atom; the atom is already one unit (a group, class or escape).
                if (out.length() - start > 0) {
                    String atom = out.substring(start);
                    if (!isSingleUnit(atom)) {
                        out.setLength(start);
                        out.append("(?:").append(atom).append(')');
                    }
                }
                out.append(pendingQuantifier);
            }
        }

        private String pendingQuantifier;

        /** Whether the text at {@code i} reads as a {@code {n}}, {@code {n,}} or {@code {n,m}} quantifier. */
        private boolean looksLikeQuantifier() {
            int k = i + 1;
            int digits = 0;
            while (k < p.length() && Character.isDigit(p.charAt(k)) && p.charAt(k) < 128) {
                k++;
                digits++;
            }
            if (digits == 0) {
                return false;
            }
            if (k < p.length() && p.charAt(k) == '}') {
                return true;
            }
            if (k < p.length() && p.charAt(k) == ',') {
                k++;
                while (k < p.length() && p.charAt(k) >= '0' && p.charAt(k) <= '9') {
                    k++;
                }
                return k < p.length() && p.charAt(k) == '}';
            }
            return false;
        }

        /** Reads a quantifier into {@link #pendingQuantifier}; false when there is none. */
        private boolean quantifier() {
            int c = peek();
            String q;
            if (c == '*' || c == '+' || c == '?') {
                i++;
                q = String.valueOf((char) c);
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
                if (!eat('}')) {
                    throw error("bad quantifier");
                }
                if (!open && max < min) {
                    throw error("numbers out of order in quantifier");
                }
                // Java's bounds are ints; a larger bound cannot be reached by any string Java can hold.
                long cap = Integer.MAX_VALUE - 1;
                q = "{" + Math.min(min, cap) + (open ? "," : max != min ? "," + Math.min(max, cap) : "") + "}";
            } else {
                return false;
            }
            if (eat('?')) {
                q += "?";
            }
            pendingQuantifier = q;
            return true;
        }

        private long number() {
            int start = i;
            while (i < p.length() && p.charAt(i) >= '0' && p.charAt(i) <= '9') {
                i++;
            }
            if (start == i) {
                throw error("expected a number");
            }
            String digits = p.substring(start, i);
            return digits.length() > 18 ? Long.MAX_VALUE : Long.parseLong(digits);
        }

        /** Whether a translated atom is one unit that a quantifier can follow directly. */
        private static boolean isSingleUnit(String atom) {
            if (atom.startsWith("\\x{") && atom.indexOf('}') == atom.length() - 1) {
                return true;
            }
            if (atom.length() == 1) {
                return true;
            }
            char first = atom.charAt(0);
            if (first == '(' || first == '[') {
                // One group or class spanning the whole atom.
                return closes(atom);
            }
            return false;
        }

        /** Whether the bracket or parenthesis at the start of a translated atom closes at its end. */
        private static boolean closes(String atom) {
            int depth = 0;
            for (int k = 0; k < atom.length(); k++) {
                char c = atom.charAt(k);
                if (c == '\\') {
                    k++;
                    continue;
                }
                if (c == '(' || c == '[') {
                    depth++;
                } else if (c == ')' || c == ']') {
                    depth--;
                    if (depth == 0 && k != atom.length() - 1) {
                        return false;
                    }
                }
            }
            return true;
        }

        /** A group or lookaround; returns whether it may be quantified. */
        private boolean group() {
            i++;
            if (eat('?')) {
                if (eat(':')) {
                    out.append("(?:");
                    disjunction();
                    close();
                    return true;
                }
                if (eat('=') || eat('!')) {
                    out.append("(?").append(p.charAt(i - 1));
                    disjunction();
                    close();
                    // Lookaheads are quantifiable only without the u flag.
                    return !unicode;
                }
                if (eat('<')) {
                    if (eat('=') || eat('!')) {
                        out.append("(?<").append(p.charAt(i - 1));
                        disjunction();
                        close();
                        return false;
                    }
                    String name = groupName();
                    if (names.containsKey(name)) {
                        throw error("duplicate group name");
                    }
                    names.put(name, groups.size() + 1);
                    capture();
                    return true;
                }
                throw error("invalid group");
            }
            capture();
            return true;
        }

        private void capture() {
            int[] g = new int[2];
            groups.add(g);
            g[0] = ++javaGroups;
            out.append('(');
            disjunction();
            // An empty group at the end marks that the group took part (for backreferences).
            g[1] = ++javaGroups;
            out.append("())");
            if (!eat(')')) {
                throw error("unterminated group");
            }
        }

        private void close() {
            if (!eat(')')) {
                throw error("unterminated group");
            }
            out.append(')');
        }

        private String groupName() {
            int start = i;
            int first = peek();
            if (first < 0 || !(Character.isUnicodeIdentifierStart(first) || first == '$' || first == '_')) {
                throw error("invalid group name");
            }
            i += Character.charCount(first);
            while (more() && peek() != '>') {
                int c = peek();
                if (!(Character.isUnicodeIdentifierPart(c) || c == '$' || c == '‌' || c == '‍')
                        || Character.isIdentifierIgnorable(c) && c != '‌' && c != '‍') {
                    throw error("invalid group name");
                }
                i += Character.charCount(c);
            }
            String name = p.substring(start, i);
            if (!eat('>')) {
                throw error("invalid group name");
            }
            return name;
        }

        private void literal(int c) {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) {
                out.append((char) c);
            } else {
                out.append("\\x{").append(Integer.toHexString(c)).append('}');
            }
        }

        /** An escape outside a class; returns whether it may be quantified. */
        private boolean atomEscape() {
            i++;
            if (!more()) {
                throw error("\\ at end of pattern");
            }
            int c = peek();
            switch (c) {
                case 'b':
                    i++;
                    out.append("(?:(?<=[" + WORD + "])(?![" + WORD + "])|(?<![" + WORD + "])(?=[" + WORD + "]))");
                    return false;
                case 'B':
                    i++;
                    out.append("(?:(?<=[" + WORD + "])(?=[" + WORD + "])|(?<![" + WORD + "])(?![" + WORD + "]))");
                    return false;
                case 'k':
                    if (unicode || !names.isEmpty() || p.contains("(?<")) {
                        i++;
                        if (!eat('<')) {
                            throw error("invalid named reference");
                        }
                        int start = i;
                        while (more() && peek() != '>') {
                            i++;
                        }
                        String name = p.substring(start, i);
                        if (!eat('>') || name.isEmpty()) {
                            throw error("invalid named reference");
                        }
                        backrefs.add(new Backref(out.length(), 0, name));
                        return true;
                    }
                    i++;
                    literal('k');
                    return true;
                default:
                    break;
            }
            if (c >= '1' && c <= '9') {
                int start = i;
                long n = number();
                if (n <= ecmaGroupCount) {
                    backrefs.add(new Backref(out.length(), (int) n, null));
                    return true;
                }
                if (unicode) {
                    throw error("invalid backreference");
                }
                // Without the u flag, an octal escape or the digits themselves.
                i = start;
                legacyOctal();
                return true;
            }
            String set = classEscape(c);
            if (set != null) {
                out.append(set);
                return true;
            }
            literal(characterEscape(false));
            return true;
        }

        private void legacyOctal() {
            int v = 0;
            int k = 0;
            while (k < 3 && more() && peek() >= '0' && peek() <= '7' && v * 8 + (peek() - '0') <= 0377) {
                v = v * 8 + (peek() - '0');
                i++;
                k++;
            }
            if (k == 0) {
                // \8 or \9: the digit itself.
                literal(peek());
                i++;
                return;
            }
            literal(v);
        }

        /** A class escape ({@code \d}, {@code \p{...}} and so on) at {@code i}, as a Java class, or null. */
        private String classEscape(int c) {
            switch (c) {
                case 'd':
                    i++;
                    return "[" + DIGIT + "]";
                case 'D':
                    i++;
                    return "[^" + DIGIT + "]";
                case 'w':
                    i++;
                    return "[" + WORD + "]";
                case 'W':
                    i++;
                    return "[^" + WORD + "]";
                case 's':
                    i++;
                    return "[" + SPACE + "]";
                case 'S':
                    i++;
                    return "[^" + SPACE + "]";
                case 'p':
                case 'P':
                    if (!unicode) {
                        return null;
                    }
                    i++;
                    String content = property();
                    return c == 'p' ? "[" + content + "]" : "[^" + content + "]";
                default:
                    return null;
            }
        }

        /** A Unicode property expression after {@code \p} or {@code \P}, as class content. */
        private String property() {
            if (!eat('{')) {
                throw error("invalid property name");
            }
            int start = i;
            while (more() && peek() != '}') {
                i++;
            }
            String expr = p.substring(start, i);
            if (!eat('}')) {
                throw error("invalid property name");
            }
            int eq = expr.indexOf('=');
            if (eq >= 0) {
                String name = expr.substring(0, eq);
                String value = expr.substring(eq + 1);
                switch (name) {
                    case "General_Category":
                    case "gc": {
                        String gc = CATEGORIES.get(value);
                        if (gc == null) {
                            throw error("invalid property name");
                        }
                        return "\\p{" + gc + "}";
                    }
                    case "Script":
                    case "sc":
                    case "Script_Extensions":
                    case "scx":
                        try {
                            Character.UnicodeScript.forName(value);
                        } catch (IllegalArgumentException e) {
                            throw error("invalid property name");
                        }
                        return "\\p{sc=" + value + "}";
                    default:
                        throw error("invalid property name");
                }
            }
            String gc = CATEGORIES.get(expr);
            if (gc != null) {
                return "\\p{" + gc + "}";
            }
            String binary = BINARY.get(expr);
            if (binary != null) {
                return binary;
            }
            throw error("invalid property name");
        }

        /** A character escape after {@code \} at {@code i} (in or out of a class): the code point. */
        private int characterEscape(boolean inClass) {
            int c = peek();
            i += Character.charCount(c);
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
                    if (!unicode && inClass && ((l >= '0' && l <= '9') || l == '_')) {
                        i++;
                        return l % 32;
                    }
                    if (unicode) {
                        throw error("invalid control escape");
                    }
                    // Without the u flag, \c that is not a control escape is a backslash and a c.
                    i--;
                    return '\\';
                }
                case '0':
                    if (more() && peek() >= '0' && peek() <= '9') {
                        if (unicode) {
                            throw error("invalid decimal escape");
                        }
                        i--;
                        return legacyOctalValue();
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
                    if (unicode && eat('{')) {
                        int start = i;
                        while (more() && isHex(peek())) {
                            i++;
                        }
                        if (start == i || !eat('}')) {
                            throw error("invalid \\u{} escape");
                        }
                        String digits = p.substring(start, i - 1);
                        long v = digits.length() > 8 ? Long.MAX_VALUE : Long.parseLong(digits, 16);
                        if (v > 0x10ffff) {
                            throw error("invalid \\u{} escape");
                        }
                        return (int) v;
                    }
                    int u = hex(4);
                    if (u < 0) {
                        if (unicode) {
                            throw error("invalid \\u escape");
                        }
                        return 'u';
                    }
                    // A surrogate pair written as two escapes is one code point.
                    if (unicode && u >= 0xd800 && u <= 0xdbff && i + 6 <= p.length() && p.charAt(i) == '\\'
                            && p.charAt(i + 1) == 'u') {
                        int save = i;
                        i += 2;
                        int low = hex(4);
                        if (low >= 0xdc00 && low <= 0xdfff) {
                            return Character.toCodePoint((char) u, (char) low);
                        }
                        i = save;
                    }
                    return u;
                }
                default:
                    break;
            }
            if (c >= '1' && c <= '9' && !unicode && inClass) {
                i--;
                return legacyOctalValue();
            }
            // Identity escapes: with the u flag, only syntax characters and '/' (and '-' in a class).
            boolean syntax = "^$\\.*+?()[]{}|/".indexOf(c) >= 0;
            if (unicode) {
                if (syntax || (inClass && c == '-')) {
                    return c;
                }
                throw error("invalid escape");
            }
            if (Character.isLetterOrDigit(c) && c < 128 && c != 'k') {
                // Without the u flag, letters other than the ones above escape themselves (\a is 'a').
                return c;
            }
            return c;
        }

        private int legacyOctalValue() {
            int v = 0;
            int k = 0;
            while (k < 3 && more() && peek() >= '0' && peek() <= '7' && v * 8 + (peek() - '0') <= 0377) {
                v = v * 8 + (peek() - '0');
                i++;
                k++;
            }
            if (k == 0) {
                int d = peek();
                i++;
                return d;
            }
            return v;
        }

        private static boolean isHex(int c) {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        private int hex(int digits) {
            if (i + digits > p.length()) {
                return -1;
            }
            int v = 0;
            for (int k = 0; k < digits; k++) {
                int d = Character.digit(p.charAt(i + k), 16);
                if (d < 0 || p.charAt(i + k) > 127) {
                    return -1;
                }
                v = v * 16 + d;
            }
            i += digits;
            return v;
        }

        /** A character class: written as a Java class, or a lookahead and any character when it nests negations. */
        private void characterClass() {
            i++;
            boolean negated = eat('^');
            StringBuilder items = new StringBuilder();
            boolean nested = false;
            boolean empty = true;
            while (true) {
                if (!more()) {
                    throw error("unterminated character class");
                }
                if (peek() == ']') {
                    i++;
                    break;
                }
                empty = false;
                ClassAtom first = classAtom();
                if (peek() == '-' && i + 1 < p.length() && p.charAt(i + 1) != ']') {
                    i++;
                    ClassAtom second = classAtom();
                    if (first.set != null || second.set != null) {
                        if (unicode) {
                            throw error("invalid character class range");
                        }
                        // Without the u flag, a class escape at either end makes the '-' literal.
                        appendClassAtom(items, first);
                        items.append("\\x{2d}");
                        appendClassAtom(items, second);
                        nested = true;
                        continue;
                    }
                    if (second.codePoint < first.codePoint) {
                        throw error("range out of order in character class");
                    }
                    items.append("\\x{").append(Integer.toHexString(first.codePoint)).append("}-\\x{")
                            .append(Integer.toHexString(second.codePoint)).append('}');
                    continue;
                }
                nested |= first.set != null;
                appendClassAtom(items, first);
            }
            if (empty) {
                out.append(negated ? ANY : "(?!)");
                return;
            }
            if (!negated) {
                out.append('[').append(items).append(']');
            } else if (!nested) {
                out.append("[^").append(items).append(']');
            } else {
                // A negated class holding a class escape: any character the positive class does not match.
                out.append("(?:(?![").append(items).append("])").append(ANY).append(')');
            }
        }

        /** A class atom: a character, or a class escape as a Java class. */
        private static final class ClassAtom {
            final int codePoint;
            final String set;

            ClassAtom(int codePoint, String set) {
                this.codePoint = codePoint;
                this.set = set;
            }
        }

        private ClassAtom classAtom() {
            int c = peek();
            if (c == '\\') {
                i++;
                if (!more()) {
                    throw error("\\ at end of pattern");
                }
                int e = peek();
                if (e == 'b') {
                    i++;
                    return new ClassAtom('\b', null);
                }
                if (e == '-' && unicode) {
                    i++;
                    return new ClassAtom('-', null);
                }
                String set = classEscape(e);
                if (set != null) {
                    return new ClassAtom(-1, set);
                }
                return new ClassAtom(characterEscape(true), null);
            }
            i += Character.charCount(c);
            return new ClassAtom(c, null);
        }

        private static void appendClassAtom(StringBuilder items, ClassAtom atom) {
            if (atom.set != null) {
                items.append(atom.set);
            } else {
                items.append("\\x{").append(Integer.toHexString(atom.codePoint)).append('}');
            }
        }
    }

    /**
     * Checks that a string is a valid ECMA-262 regular expression with the u flag (the {@code regex} format), reading
     * it as the translator does but writing nothing: allocates nothing once its buffers have grown, except to look up
     * a {@code \p{Script=...}} name. Not thread-safe; one per evaluator.
     */
    static final class Validator {
        /** Thrown (shared) on the first error. */
        private static final IllegalArgumentException INVALID = new IllegalArgumentException("invalid");
        private static final String[] PROPERTY_NAMES;
        private static final String[] CATEGORY_NAMES;

        static {
            CATEGORY_NAMES = CATEGORIES.keySet().toArray(new String[0]);
            java.util.List<String> all = new java.util.ArrayList<>(CATEGORIES.keySet());
            all.addAll(BINARY.keySet());
            PROPERTY_NAMES = all.toArray(new String[0]);
        }

        private CharSequence p;
        private int i;
        private int n;
        private int groupCount;
        /** Group names and named references, as ranges of the pattern. */
        private int[] names = new int[8];
        private int nameCount;
        private int[] refs = new int[8];
        private int refCount;

        boolean isValid(CharSequence pattern) {
            p = pattern;
            i = 0;
            n = pattern.length();
            nameCount = 0;
            refCount = 0;
            try {
                groupCount = countGroups();
                disjunction();
                if (i < n) {
                    return false;
                }
                for (int r = 0; r < refCount; r++) {
                    if (findName(refs[2 * r], refs[2 * r + 1]) < 0) {
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
                    } else if (k + 3 < n && p.charAt(k + 2) == '<' && p.charAt(k + 3) != '=' && p.charAt(k + 3) != '!') {
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
            alternative();
            while (eat('|')) {
                alternative();
            }
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

        private long number() {
            int start = i;
            long v = 0;
            while (i < n && p.charAt(i) >= '0' && p.charAt(i) <= '9') {
                if (v < Long.MAX_VALUE / 10) {
                    v = v * 10 + (p.charAt(i) - '0');
                }
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

        private boolean group() {
            i++;
            if (eat('?')) {
                if (eat(':')) {
                    disjunction();
                    close();
                    return true;
                }
                if (eat('=') || eat('!')) {
                    disjunction();
                    close();
                    // Lookaheads are not quantifiable with the u flag.
                    return false;
                }
                if (eat('<')) {
                    if (eat('=') || eat('!')) {
                        disjunction();
                        close();
                        return false;
                    }
                    int start = i;
                    groupName();
                    if (findName(start, i - 1) >= 0) {
                        throw INVALID;
                    }
                    if (2 * nameCount + 2 > names.length) {
                        names = java.util.Arrays.copyOf(names, names.length * 2);
                    }
                    names[2 * nameCount] = start;
                    names[2 * nameCount + 1] = i - 1;
                    nameCount++;
                    disjunction();
                    close();
                    return true;
                }
                throw INVALID;
            }
            disjunction();
            close();
            return true;
        }

        private void close() {
            if (!eat(')')) {
                throw INVALID;
            }
        }

        /** A group name up to and including its '>'. */
        private void groupName() {
            int first = peek();
            if (first < 0 || !(Character.isUnicodeIdentifierStart(first) || first == '$' || first == '_')) {
                throw INVALID;
            }
            i += Character.charCount(first);
            while (i < n && peek() != '>') {
                int c = peek();
                if (!(Character.isUnicodeIdentifierPart(c) || c == '$' || c == '\u200c' || c == '\u200d')
                        || Character.isIdentifierIgnorable(c) && c != '\u200c' && c != '\u200d') {
                    throw INVALID;
                }
                i += Character.charCount(c);
            }
            if (!eat('>')) {
                throw INVALID;
            }
        }

        /** The index of the group named by p[start, end), or -1. */
        private int findName(int start, int end) {
            for (int k = 0; k < nameCount; k++) {
                int a = names[2 * k];
                int b = names[2 * k + 1];
                if (b - a == end - start && regionEquals(a, start, end - start)) {
                    return k;
                }
            }
            return -1;
        }

        private boolean regionEquals(int a, int b, int len) {
            for (int k = 0; k < len; k++) {
                if (p.charAt(a + k) != p.charAt(b + k)) {
                    return false;
                }
            }
            return true;
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
                while (i < n && peek() != '>') {
                    i++;
                }
                int end = i;
                if (!eat('>') || end == start) {
                    throw INVALID;
                }
                if (2 * refCount + 2 > refs.length) {
                    refs = java.util.Arrays.copyOf(refs, refs.length * 2);
                }
                refs[2 * refCount] = start;
                refs[2 * refCount + 1] = end;
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
            int eq = -1;
            while (i < n && p.charAt(i) != '}') {
                if (p.charAt(i) == '=' && eq < 0) {
                    eq = i;
                }
                i++;
            }
            int end = i;
            if (!eat('}')) {
                throw INVALID;
            }
            if (eq < 0) {
                if (!oneOf(PROPERTY_NAMES, start, end)) {
                    throw INVALID;
                }
                return;
            }
            if (is("General_Category", start, eq) || is("gc", start, eq)) {
                if (!oneOf(CATEGORY_NAMES, eq + 1, end)) {
                    throw INVALID;
                }
                return;
            }
            if (is("Script", start, eq) || is("sc", start, eq) || is("Script_Extensions", start, eq)
                    || is("scx", start, eq)) {
                try {
                    Character.UnicodeScript.forName(p.subSequence(eq + 1, end).toString());
                } catch (IllegalArgumentException e) {
                    throw INVALID;
                }
                return;
            }
            throw INVALID;
        }

        private boolean is(String name, int start, int end) {
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

        private boolean oneOf(String[] names, int start, int end) {
            for (String name : names) {
                if (is(name, start, end)) {
                    return true;
                }
            }
            return false;
        }

        private boolean hex(int digits) {
            if (i + digits > n) {
                return false;
            }
            for (int k = 0; k < digits; k++) {
                char c = p.charAt(i + k);
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) {
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
                        while (i < n && Character.digit(p.charAt(i), 16) >= 0 && p.charAt(i) < 128) {
                            if (v <= 0x10ffff) {
                                v = v * 16 + Character.digit(p.charAt(i), 16);
                            }
                            i++;
                        }
                        if (start == i || !eat('}') || v > 0x10ffff) {
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
                        int save = i;
                        i += 2;
                        if (hex(4)) {
                            int low = hexValue(save + 2, save + 6);
                            if (low >= 0xdc00 && low <= 0xdfff) {
                                return Character.toCodePoint((char) u, (char) low);
                            }
                        }
                        i = save;
                    }
                    return u;
                default:
                    return c;
            }
        }

        private int hexValue(int from, int to) {
            int v = 0;
            for (int k = from; k < to; k++) {
                v = v * 16 + Character.digit(p.charAt(k), 16);
            }
            return v;
        }
    }
}
