// <copyright file="EcmaRegexTranslator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Translates ECMA-262 regular expression patterns to .NET regular expression patterns with the same meaning,
/// validating them as ECMA-262 does. The grammar is that of ECMAScript 2025.
/// </summary>
/// <remarks>
/// <para>
/// <b>Grammars.</b> A pattern is read first with the grammar of the <c>u</c> flag, as JSON Schema's <c>pattern</c>
/// specifies, and is then matched by code point. A pattern that is not valid with it is read with the Annex B grammar
/// of a pattern with no flag, as many schemas hold such patterns (identity escapes like <c>\-</c>, lone braces), and
/// is then matched by UTF-16 code unit, as ECMA-262 matches a pattern with no flag. A pattern neither grammar accepts
/// is not valid. <see cref="Translate"/> and <see cref="TryTranslate(ReadOnlySpan{char}, Span{char}, out int)"/> accept
/// only the <c>u</c> flag grammar. <see cref="TryTranslate(string, out string, out string)"/> and
/// <see cref="TranslateOrFallback"/> try both.
/// </para>
/// <para>
/// <b>What the translation writes.</b> The pattern is read into a tree (<see cref="EcmaRegexParser"/>), and the tree
/// is written for .NET so that nothing depends on how .NET reads a construct ECMA-262 reads differently.
/// <list type="bullet">
/// <item><c>\d</c>, <c>\w</c>, <c>\s</c>, <c>\b</c> and the dot are the ASCII and line terminator sets of ECMA-262.</item>
/// <item><c>$</c> is <c>\z</c>: without the <c>m</c> modifier it holds only at the end of the input.</item>
/// <item>With the <c>u</c> flag grammar a character beyond the Basic Multilingual Plane is one character to a set,
/// to the dot and to a quantifier, though .NET matches UTF-16 code units. A set that holds such characters is written
/// as an alternation of the code units and the surrogate pairs it matches.</item>
/// <item>A <c>\p{...}</c> escape is the set of its property in the Unicode version of
/// <see cref="EcmaRegexUnicodeData"/>, for every property, value and alias ECMA-262 lists. Only a General_Category
/// value is written with .NET's own class, for the Basic Multilingual Plane.</item>
/// <item>The modifiers of a group such as <c>(?i:...)</c>, <c>(?m:...)</c> or <c>(?s:...)</c> are applied while
/// reading: a case-insensitive literal or class becomes the set of the characters equivalent to it under the
/// Canonicalize of ECMA-262, <c>^</c> and <c>$</c> become assertions about ECMA-262's four line terminators, and the
/// dot becomes every character. No option of .NET stands for them.</item>
/// <item>A group is written without capture unless a backreference reads it, and then by its number.</item>
/// <item>A backreference matches the empty string when its group has not taken part, and a quantified group forgets
/// what its groups captured when an iteration starts, as ECMA-262 specifies (see <see cref="Emitter"/>).</item>
/// </list>
/// </para>
/// </remarks>
internal static class EcmaRegexTranslator
{
    /// <summary>
    /// The longest translation that is given to .NET's compiled engine. A longer one is interpreted. The only
    /// translations this long are those of a case-insensitive backreference to a group that can capture any letter,
    /// which hold an alternative for every class of case variants (see <see cref="Emitter"/>). .NET compiles such
    /// an expression in seconds, at the first match, and constructs it for the interpreter in milliseconds.
    /// </summary>
    internal const int LongestCompiledTranslation = 32_768;

    /// <summary>
    /// What a translation starts with when it has to be run by .NET's interpreted engine: a comment, which .NET
    /// skips. Such a translation keeps a lazy repetition of a group, inside a lookaround whose captures a
    /// backreference reads, where the order in which paths are tried decides the answer. .NET's compiled engine
    /// does not always return from a match of one (after a repetition whose iterations hold a lookbehind and can
    /// match nothing, it loops on a lazy repetition of a group in a lookbehind), and the code the regular expression
    /// generator writes is the same algorithm. The interpreted engine answers.
    /// </summary>
    internal const string InterpretedMarker = "(?#interpreted)";

    /// <summary>
    /// Whether a translation may be given to .NET's compiled engine: it is not marked for the interpreted one
    /// (<see cref="InterpretedMarker"/>) and is no longer than <see cref="LongestCompiledTranslation"/>.
    /// </summary>
    /// <param name="translated">The translation.</param>
    /// <returns><see langword="true"/> when the translation may be compiled.</returns>
    internal static bool IsForCompiledEngine(string translated)
    {
        return translated.Length <= LongestCompiledTranslation && !translated.StartsWith(InterpretedMarker, StringComparison.Ordinal);
    }

    /// <summary>
    /// Attempts to translate an ECMAScript 262 regex pattern (with /u Unicode mode)
    /// to an equivalent .NET regex pattern.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern to translate.</param>
    /// <param name="destination">The buffer to write the translated .NET pattern to.</param>
    /// <param name="charsWritten">The number of characters written to <paramref name="destination"/>.</param>
    /// <returns>
    /// <see cref="OperationStatus.Done"/> if the translation succeeded;
    /// <see cref="OperationStatus.DestinationTooSmall"/> if the destination buffer is too small;
    /// <see cref="OperationStatus.InvalidData"/> if the ECMAScript pattern is invalid.
    /// </returns>
    public static OperationStatus TryTranslate(
        ReadOnlySpan<char> ecmaPattern,
        Span<char> destination,
        out int charsWritten)
    {
        charsWritten = 0;
        if (!TryTranslateCore(ecmaPattern, unicodeOnly: true, out string? translated, out _, out _))
        {
            return OperationStatus.InvalidData;
        }

        if (translated!.Length > destination.Length)
        {
            return OperationStatus.DestinationTooSmall;
        }

        translated.AsSpan().CopyTo(destination);
        charsWritten = translated.Length;
        return OperationStatus.Done;
    }

    /// <summary>
    /// Translates an ECMAScript 262 regex pattern (with /u Unicode mode)
    /// to an equivalent .NET regex pattern string.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern to translate.</param>
    /// <returns>The translated .NET regex pattern string.</returns>
    /// <exception cref="ArgumentException">The ECMAScript pattern is invalid.</exception>
    public static string Translate(ReadOnlySpan<char> ecmaPattern)
    {
        if (TryTranslateCore(ecmaPattern, unicodeOnly: true, out string? translated, out _, out string? error))
        {
            return translated!;
        }

        throw new ArgumentException("The ECMAScript pattern is invalid. " + error, nameof(ecmaPattern));
    }

    /// <summary>
    /// Translates an ECMA-262 pattern to a .NET pattern, reading it with the grammar of the <c>u</c> flag and, when
    /// that grammar does not accept it, with the grammar of a pattern with no flag.
    /// </summary>
    /// <param name="ecmaPattern">The ECMA-262 pattern.</param>
    /// <param name="translated">The .NET pattern, when the pattern is valid.</param>
    /// <param name="error">What is wrong with the pattern, when it is not valid.</param>
    /// <returns><see langword="true"/> when the pattern is a valid ECMA-262 pattern.</returns>
    public static bool TryTranslate(string ecmaPattern, out string? translated, out string? error)
    {
        return TryTranslateCore(ecmaPattern.AsSpan(), unicodeOnly: false, out translated, out _, out error);
    }

    /// <summary>
    /// Translates an ECMA-262 pattern to a .NET pattern, reading it with the grammar of the <c>u</c> flag and, when
    /// that grammar does not accept it, with the grammar of a pattern with no flag.
    /// </summary>
    /// <param name="ecmaPattern">The ECMA-262 pattern.</param>
    /// <param name="translated">The .NET pattern, when the pattern is valid.</param>
    /// <param name="unicode">
    /// Whether the grammar of the <c>u</c> flag accepted the pattern, which is then matched by code point. A pattern
    /// only the other grammar accepts is matched by UTF-16 code unit.
    /// </param>
    /// <param name="error">What is wrong with the pattern, when it is not valid.</param>
    /// <returns><see langword="true"/> when the pattern is a valid ECMA-262 pattern.</returns>
    public static bool TryTranslate(string ecmaPattern, out string? translated, out bool unicode, out string? error)
    {
        return TryTranslateCore(ecmaPattern.AsSpan(), unicodeOnly: false, out translated, out unicode, out error);
    }

    /// <summary>
    /// Translates an ECMAScript regex pattern to a .NET pattern, falling back
    /// to the original pattern if it is not a valid ECMA-262 pattern.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern.</param>
    /// <returns>The translated .NET regex pattern, or the original if the pattern is not valid.</returns>
    internal static string TranslateOrFallback(string ecmaPattern)
    {
        return TryTranslateCore(ecmaPattern.AsSpan(), unicodeOnly: false, out string? translated, out _, out _) ? translated! : ecmaPattern;
    }

    /// <summary>
    /// Gets the maximum possible length of the translated pattern, suitable for buffer allocation.
    /// </summary>
    /// <param name="ecmaPattern">The ECMAScript regex pattern.</param>
    /// <returns>The maximum number of characters the translated pattern could require.</returns>
    public static int GetMaxTranslatedLength(ReadOnlySpan<char> ecmaPattern)
    {
        // The length of a translation is not a simple function of the length of the pattern (a set that holds
        // characters beyond the Basic Multilingual Plane is written out), so the pattern is translated to find it.
        return TryTranslateCore(ecmaPattern, unicodeOnly: true, out string? translated, out _, out _) ? translated!.Length : ecmaPattern.Length;
    }

    private static bool TryTranslateCore(ReadOnlySpan<char> ecmaPattern, bool unicodeOnly, out string? translated, out bool unicode, out string? error)
    {
        unicode = true;
        EcmaRegexParser parser = new(ecmaPattern, unicode: true);
        EcmaRegexNode root;
        try
        {
            root = parser.Parse();
        }
        catch (EcmaRegexSyntaxException unicodeError)
        {
            if (unicodeOnly)
            {
                translated = null;
                error = unicodeError.Message + ".";
                return false;
            }

            unicode = false;
            parser = new EcmaRegexParser(ecmaPattern, unicode: false);
            try
            {
                root = parser.Parse();
            }
            catch (EcmaRegexSyntaxException legacyError)
            {
                translated = null;
                error = unicodeError.Message == legacyError.Message
                    ? unicodeError.Message + "."
                    : unicodeError.Message + " (read without the u flag: " + legacyError.Message + ").";
                return false;
            }
        }

        translated = new Emitter(parser, root).Translate();
        error = null;
        return true;
    }

    /// <summary>Writes the tree of a pattern as a .NET pattern with the same meaning.</summary>
    /// <remarks>
    /// <para>
    /// <b>Backreferences.</b> In ECMA-262 a backreference matches the text its group captured when the group last
    /// took part, and the empty string when it has not taken part. A quantified term unsets the groups inside it
    /// when each iteration starts. .NET fails a backreference to a group that has not taken part and keeps what a
    /// group captured in an earlier iteration. So a backreference to group 1 is written <c>(?(1)\1)</c>, which
    /// matches nothing when the group is unset, and a repetition whose body holds a group that a backreference reads
    /// starts each iteration with <c>(?(1)(?&lt;-1&gt;))</c>, a balancing group that takes the capture away, and
    /// gives it back when the match backtracks out of the iteration. A lookbehind is matched backwards by both
    /// engines, and both undo the captures of a negative lookaround.
    /// </para>
    /// </remarks>
    private sealed class Emitter
    {
        private const string WordClass = "[a-zA-Z0-9_]";
        private const string FoldWordClass = @"[a-zA-Z0-9_\u017F\u212A]";
        private const string LineTerminatorClass = @"[\n\r\u2028\u2029]";
        private const string HighSurrogates = @"[\uD800-\uDBFF]";
        private const string LowSurrogates = @"[\uDC00-\uDFFF]";

        /// <summary>
        /// An assertion that never holds: what follows is neither the end of the input nor a character. It is not
        /// written <c>(?!)</c>, which .NET takes out of the pattern before it runs it, with what depends on it, and
        /// the compiled engine can then throw (<c>(?:(?:(?!)a)??){0,2}</c> before a lookbehind is one such pattern).
        /// </summary>
        /// <summary>How many classes of Canonicalize a written-out comparison tries behind one look at the next character.</summary>
        private const int FoldClassesTogether = 32;

        /// <summary>The letters of ASCII that no character outside ASCII is a case variant of, in any casing.</summary>
        private const string PlainAsciiLetters = "[A-HJL-RT-Za-hjl-rt-z]";

        private const string Never = @"(?!\z|[\s\S])";

        /// <summary>Holds between the two halves of a character beyond the Basic Multilingual Plane.</summary>
        private const string InsideSurrogatePair = @"(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF]";

        /// <summary>The most nodes read to write a body that cannot match the empty string (<see cref="NonEmpty"/>).</summary>
        private const int NonEmptyBudget = 256;

        /// <summary>More alternatives of surrogate pairs than this stand behind one test for a surrogate.</summary>
        private const int ManyAlternatives = 2;

        /// <summary>A length with no bound.</summary>
        private const long Infinite = long.MaxValue;

        /// <summary>
        /// The most iterations a lazy repetition may leave to choice and still be written as optional groups one in
        /// another, each a copy of its body.
        /// </summary>
        private const int MaxNestedLazy = 8;

        private static readonly int[] SurrogateRange = [0xD800, 0xDFFF];

        /// <summary>The letters of ASCII that a character outside ASCII is a case variant of, in some casing: I, K and S.</summary>
        private static readonly int[] NotPlainAsciiLetters = ['I', 'I', 'K', 'K', 'S', 'S', 'i', 'i', 'k', 'k', 's', 's'];

        private static readonly int[] HighSurrogateRange = [0xD800, 0xDBFF];
        private static readonly int[] LowSurrogateRange = [0xDC00, 0xDFFF];
        private static readonly int[] SupplementaryRange = [0x10000, EcmaRegexUnicode.Max];

        // The General_Category values .NET names, by the number of their table, and the unions of them it names.
        private static readonly string[] CategoryNames =
        [
            "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Sm",
            "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Cn",
        ];

        private static readonly (int Tables, string Name)[] CategoryUnions =
        [
            (0x1F, "L"), (0xE0, "M"), (0x700, "N"), (0x3F800, "P"), (0x3C0000, "S"), (0x1C00000, "Z"),
        ];

        private readonly bool unicode;
        private readonly bool hasBackreferences;
        private EcmaRegexNode root;
        private readonly EcmaRegexNode?[] groupNodes;

        /// <summary>For each group, whether some backreference reads it.</summary>
        private readonly bool[] referenced;

        /// <summary>For each group, whether it is in a part of the pattern that never runs, so that it never takes part.</summary>
        private readonly bool[] dead;
        private readonly StringBuilder sb = new();
        private int markers;

        /// <summary>For each group, every character its text can hold (see <see cref="CapturedSets"/>).</summary>
        private int[][]? capturedSets;

        /// <summary>
        /// How many positive lookarounds that hold a group a backreference reads enclose what is being written. The
        /// first path through such a lookaround is the one whose captures are kept.
        /// </summary>
        private int inCapturingLookaround;

        /// <summary>
        /// Whether a lazy repetition of anything but one code unit has been written as one, which happens only inside
        /// a lookaround whose captures a backreference reads. .NET's compiled engine does not always return from
        /// such an expression, so the translation is marked for the interpreted one (<see cref="InterpretedMarker"/>).
        /// </summary>
        private bool keepsLazyGroupRepetition;

        public Emitter(EcmaRegexParser parser, EcmaRegexNode root)
        {
            this.unicode = parser.Unicode;
            this.hasBackreferences = parser.HasBackreferences;
            int n = parser.GroupCount + 1;
            this.groupNodes = new EcmaRegexNode?[n];
            this.referenced = new bool[n];
            this.dead = new bool[n];
            this.root = this.Simplify(root, new bool[n], new int[n]);
        }

        /// <summary>Where a node stands, for what is written around it.</summary>
        [Flags]
        private enum Context
        {
            None = 0,

            /// <summary>Inside a lookbehind, which is matched from right to left.</summary>
            Backward = 1,

            /// <summary>Nothing follows the node in the pattern: when it has matched, the pattern has.</summary>
            Last = 2,
        }

        private enum Start
        {
            /// <summary>The node cannot match between the two halves of a surrogate pair.</summary>
            Blocks,

            /// <summary>The node matches there only as the empty string, with no assertion passed.</summary>
            Passes,

            /// <summary>The node may match there.</summary>
            Unsafe,
        }

        public string Translate()
        {
            this.Survey(this.root);
            this.root = this.Prune(this.root);
            if (this.unicode && this.StartOf(this.root, top: true, backward: false) == Start.Unsafe)
            {
                // .NET tries a match at every code unit, and so between the two halves of a character beyond the
                // Basic Multilingual Plane, where ECMA-262 has no position.
                this.sb.Append("(?!").Append(InsideSurrogatePair).Append(')');
                if (this.root.Kind == EcmaRegexNodeKind.Alt)
                {
                    this.sb.Append("(?:");
                    this.Node(this.root, Context.Last, null);
                    this.sb.Append(')');
                    return this.Finish();
                }
            }

            this.Node(this.root, Context.Last, null);
            return this.Finish();
        }

        private static long Plus(long a, long b) => a == Infinite || b == Infinite ? Infinite : a + b;

        /// <summary>A length some number of times over, where the lengths that are not infinite are below 2^47.</summary>
        private static long Times(long length, int count)
        {
            if (length == 0 || count == 0)
            {
                return 0;
            }

            if (length == Infinite || count == EcmaRegexNode.Unbounded || length > (1L << 47) / count)
            {
                return Infinite;
            }

            return length * count;
        }

        /// <summary>Whether a node matches the empty string wherever it stands: through no assertion and no backreference.</summary>
        private static bool EmptyAnywhere(EcmaRegexNode n)
        {
            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Empty:
                    return true;
                case EcmaRegexNodeKind.Cat:
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        if (!EmptyAnywhere(sub))
                        {
                            return false;
                        }
                    }

                    return true;
                case EcmaRegexNodeKind.Alt:
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        if (EmptyAnywhere(sub))
                        {
                            return true;
                        }
                    }

                    return false;
                case EcmaRegexNodeKind.Group:
                    return EmptyAnywhere(n.Subs[0]);
                case EcmaRegexNodeKind.Repeat:
                    return n.Min == 0 || EmptyAnywhere(n.Subs[0]);
                default:
                    return false;
            }
        }

        private static bool IsSurrogate(int c) => c >= 0xD800 && c <= 0xDFFF;

        /// <summary>A node that never matches.</summary>
        private static EcmaRegexNode NeverNode() => new(EcmaRegexNodeKind.Set);

        private static bool IsNever(EcmaRegexNode n) => n.Kind == EcmaRegexNodeKind.Set && n.Set.Length == 0;

        /// <summary>Adds the numbers of the capturing groups of a node.</summary>
        private static void CollectGroups(EcmaRegexNode n, List<int> groups)
        {
            if (n.Kind == EcmaRegexNodeKind.Group && n.Group != 0)
            {
                groups.Add(n.Group);
            }

            foreach (EcmaRegexNode sub in n.Subs)
            {
                CollectGroups(sub, groups);
            }
        }

        /// <summary>Whether a node is one backreference, in non-capturing groups or in none.</summary>
        private static bool IsOneReference(EcmaRegexNode n)
        {
            while (n.Kind == EcmaRegexNodeKind.Group && n.Group == 0)
            {
                n = n.Subs[0];
            }

            return n.Kind == EcmaRegexNodeKind.Backref;
        }

        /// <summary>Whether a node holds a backreference.</summary>
        private static bool HoldsBackreference(EcmaRegexNode n)
        {
            if (n.Kind == EcmaRegexNodeKind.Backref)
            {
                return true;
            }

            foreach (EcmaRegexNode sub in n.Subs)
            {
                if (HoldsBackreference(sub))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether a set holds every surrogate and every character beyond the Basic Multilingual Plane.</summary>
        private static bool HoldsEveryPair(int[] set) => HoldsRange(set, 0xD800, 0xDFFF) && HoldsRange(set, 0x10000, EcmaRegexUnicode.Max);

        /// <summary>Whether a set holds every code point of a range.</summary>
        private static bool HoldsRange(int[] set, int lo, int hi)
        {
            for (int k = 0; k < set.Length; k += 2)
            {
                if (set[k] <= lo && set[k + 1] >= hi)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Whether the tables of a General_Category value are written as one class of .NET.</summary>
        private static bool IsOneCategory(int tables)
        {
            if ((tables & (tables - 1)) == 0)
            {
                return true;
            }

            foreach ((int unionTables, _) in CategoryUnions)
            {
                if (tables == unionTables)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Takes out of the tree what never runs or runs to no effect, so that what is written holds no repetition
        /// .NET would have to run over a body that cannot move.
        /// <list type="bullet">
        /// <item>A backreference inside the group it reads is nothing. The group is not set until it closes, and a
        /// repetition around it unsets it when each iteration starts, so inside the group it is never set.</item>
        /// <item>So is a backreference to a group in another alternative of the alternation it stands in. One
        /// alternative runs at a time, and a repetition around the alternation unsets the group when each iteration
        /// starts.</item>
        /// <item>A repetition of at most zero times is nothing, and its groups never take part.</item>
        /// <item>A negative lookaround of a body that matches the empty string wherever it stands never holds.</item>
        /// <item>A repetition of a body that can only match the empty string is that body once when it must run, and
        /// nothing when it need not: ECMA-262 refuses an iteration that matched nothing once the iterations that
        /// must run are done, and the ones that must run all stand at the same place.</item>
        /// </list>
        /// V8 makes the first rewrite and the last. Without them a pattern such as <c>((|\2|\1\2){3,}|a){4}</c>, whose
        /// inner body is then seen to match only the empty string, is run through every combination of the ways
        /// through every iteration.
        /// </summary>
        /// <param name="n">The node.</param>
        /// <param name="open">For each group, whether the node is inside it.</param>
        /// <param name="apart">For each group, in how many alternations the node and the group are in different alternatives.</param>
        private EcmaRegexNode Simplify(EcmaRegexNode n, bool[] open, int[] apart)
        {
            if (n.Kind == EcmaRegexNodeKind.Backref && n.Groups.Length > 0)
            {
                // A name can belong to several groups. The reference reads those of them that can be set here.
                int[] set = Array.FindAll(n.Groups, g => !open[g] && apart[g] == 0);
                if (set.Length == 0)
                {
                    return new EcmaRegexNode(EcmaRegexNodeKind.Empty);
                }

                n.Groups = set;
                return n;
            }

            if (n.Kind == EcmaRegexNodeKind.Alt && this.hasBackreferences)
            {
                List<int>[] groups = new List<int>[n.Subs.Length];
                for (int k = 0; k < n.Subs.Length; k++)
                {
                    groups[k] = [];
                    CollectGroups(n.Subs[k], groups[k]);
                }

                for (int k = 0; k < n.Subs.Length; k++)
                {
                    for (int other = 0; other < n.Subs.Length; other++)
                    {
                        if (other != k)
                        {
                            foreach (int g in groups[other])
                            {
                                apart[g]++;
                            }
                        }
                    }

                    n.Subs[k] = this.Simplify(n.Subs[k], open, apart);
                    for (int other = 0; other < n.Subs.Length; other++)
                    {
                        if (other != k)
                        {
                            foreach (int g in groups[other])
                            {
                                apart[g]--;
                            }
                        }
                    }
                }

                return n;
            }

            bool opens = n.Kind == EcmaRegexNodeKind.Group && n.Group != 0;
            if (opens)
            {
                open[n.Group] = true;
            }

            for (int k = 0; k < n.Subs.Length; k++)
            {
                n.Subs[k] = this.Simplify(n.Subs[k], open, apart);
            }

            if (opens)
            {
                open[n.Group] = false;
            }

            if (n.Kind == EcmaRegexNodeKind.Look && n.Negative && EmptyAnywhere(n.Subs[0]))
            {
                // The body matches the empty string wherever it stands, so the lookaround never holds.
                this.Kill(n);
                return new EcmaRegexNode(EcmaRegexNodeKind.Set);
            }

            if (n.Kind == EcmaRegexNodeKind.Repeat)
            {
                EcmaRegexNode body = n.Subs[0];
                if (n.Max == 0 || (n.Min == 0 && this.MaxLength(body) == 0))
                {
                    this.Kill(n);
                    return new EcmaRegexNode(EcmaRegexNodeKind.Empty);
                }

                if (this.MaxLength(body) == 0)
                {
                    return body;
                }
            }

            return n;
        }

        /// <summary>
        /// Makes an empty node of what would be written as nothing, or as what .NET takes for nothing: a group no
        /// backreference reads around nothing, a positive lookaround of nothing, a repetition of nothing, a
        /// backreference to groups that never take part. An alternative that is nothing is then seen for one (see
        /// <see cref="Alternation"/>).
        /// </summary>
        private EcmaRegexNode Prune(EcmaRegexNode n)
        {
            int kept = 0;
            for (int k = 0; k < n.Subs.Length; k++)
            {
                n.Subs[k] = this.Prune(n.Subs[k]);
                kept += n.Subs[k].Kind == EcmaRegexNodeKind.Empty ? 0 : 1;
            }

            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Backref when Array.TrueForAll(n.Groups, g => this.dead[g]):
                case EcmaRegexNodeKind.Group when kept == 0 && (n.Group == 0 || !this.referenced[n.Group]):
                case EcmaRegexNodeKind.Look when kept == 0 && !n.Negative:
                case EcmaRegexNodeKind.Repeat when kept == 0:
                case EcmaRegexNodeKind.Alt when kept == 0:
                case EcmaRegexNodeKind.Cat when kept == 0:
                    return new EcmaRegexNode(EcmaRegexNodeKind.Empty);
                case EcmaRegexNodeKind.Cat when kept < n.Subs.Length:
                {
                    EcmaRegexNode[] subs = Array.FindAll(n.Subs, sub => sub.Kind != EcmaRegexNodeKind.Empty);
                    if (subs.Length == 1)
                    {
                        return subs[0];
                    }

                    n.Subs = subs;
                    return n;
                }

                default:
                    return n;
            }
        }

        /// <summary>The translation, marked for the interpreted engine when it keeps a lazy repetition of a group.</summary>
        private string Finish()
        {
            if (this.keepsLazyGroupRepetition)
            {
                this.sb.Insert(0, InterpretedMarker);
            }

            return this.sb.ToString();
        }

        /// <summary>Notes that the groups of a node never take part, as the node is not written.</summary>
        private void Kill(EcmaRegexNode n)
        {
            for (int g = n.FirstGroup + 1; g <= n.LastGroup; g++)
            {
                this.dead[g] = true;
            }
        }

        /// <summary>Notes the group nodes and which of them a backreference reads.</summary>
        private void Survey(EcmaRegexNode n)
        {
            if (n.Kind == EcmaRegexNodeKind.Group && n.Group != 0)
            {
                this.groupNodes[n.Group] = n;
            }
            else if (n.Kind == EcmaRegexNodeKind.Backref)
            {
                foreach (int g in n.Groups)
                {
                    if (!this.dead[g])
                    {
                        this.referenced[g] = true;
                    }
                }
            }

            foreach (EcmaRegexNode sub in n.Subs)
            {
                this.Survey(sub);
            }
        }

        private bool AnyReferenced(EcmaRegexNode n)
        {
            for (int g = n.FirstGroup + 1; g <= n.LastGroup; g++)
            {
                if (this.referenced[g])
                {
                    return true;
                }
            }

            return false;
        }

        // Lengths, in characters.
        private long MinLength(EcmaRegexNode n)
        {
            if (n.MinLength < 0)
            {
                this.Measure(n);
            }

            return n.MinLength;
        }

        private long MaxLength(EcmaRegexNode n)
        {
            if (n.MinLength < 0)
            {
                this.Measure(n);
            }

            return n.MaxLength;
        }

        private void Measure(EcmaRegexNode n)
        {
            long min = 0;
            long max = 0;
            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Char:
                case EcmaRegexNodeKind.Set:
                    min = 1;
                    max = 1;
                    break;
                case EcmaRegexNodeKind.Cat:
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        min = Plus(min, this.MinLength(sub));
                        max = Plus(max, this.MaxLength(sub));
                    }

                    break;
                case EcmaRegexNodeKind.Alt:
                    min = Infinite;
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        min = Math.Min(min, this.MinLength(sub));
                        max = Math.Max(max, this.MaxLength(sub));
                    }

                    break;
                case EcmaRegexNodeKind.Group:
                    min = this.MinLength(n.Subs[0]);
                    max = this.MaxLength(n.Subs[0]);
                    break;
                case EcmaRegexNodeKind.Repeat:
                    min = Times(this.MinLength(n.Subs[0]), n.Min);
                    max = Times(this.MaxLength(n.Subs[0]), n.Max);
                    break;
                case EcmaRegexNodeKind.Backref:
                    max = Infinite;
                    break;
                default:
                    break;
            }

            // Past 2^47 a length is as good as unbounded, and keeping below it keeps the sums exact.
            n.MinLength = min > (1L << 47) ? Infinite : min;
            n.MaxLength = max > (1L << 47) ? Infinite : max;
        }

        // Where a match may start.

        /// <summary>
        /// What a node does when a match is tried between the two halves of a surrogate pair. Every set and every
        /// character is written so that it does not match there, so only an assertion that holds there, a
        /// backreference or a repetition written by code unit can let a match through.
        /// </summary>
        private Start StartOf(EcmaRegexNode n, bool top, bool backward)
        {
            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Empty:
                    return Start.Passes;
                case EcmaRegexNodeKind.Char:
                case EcmaRegexNodeKind.Set:
                case EcmaRegexNodeKind.Bol:
                case EcmaRegexNodeKind.Eol:
                case EcmaRegexNodeKind.LineStart:
                case EcmaRegexNodeKind.LineEnd:
                case EcmaRegexNodeKind.WordBoundary:
                    return Start.Blocks;
                case EcmaRegexNodeKind.Cat:
                    for (int k = 0; k < n.Subs.Length; k++)
                    {
                        // A lookbehind is matched from its last term to its first.
                        Start s = this.StartOf(n.Subs[backward ? n.Subs.Length - 1 - k : k], top, backward);
                        if (s != Start.Passes)
                        {
                            return s;
                        }
                    }

                    return Start.Passes;
                case EcmaRegexNodeKind.Alt:
                {
                    Start result = Start.Blocks;
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        Start s = this.StartOf(sub, top, backward);
                        if (s == Start.Unsafe)
                        {
                            return s;
                        }

                        if (s == Start.Passes)
                        {
                            result = s;
                        }
                    }

                    return result;
                }

                case EcmaRegexNodeKind.Group:
                    return this.StartOf(n.Subs[0], top, backward);
                case EcmaRegexNodeKind.Backref when Array.TrueForAll(n.Groups, g => this.dead[g]):
                    // A reference to groups that never take part is nothing.
                    return Start.Passes;
                case EcmaRegexNodeKind.Look:
                    // A lookaround that has to match holds there only when its body matches from there.
                    return n.Negative ? Start.Unsafe : this.StartOf(n.Subs[0], top: false, backward: n.Behind);
                case EcmaRegexNodeKind.Repeat:
                {
                    if (this.IsCodeUnitLoop(n))
                    {
                        // A match that starts inside a pair with this repetition also starts one code unit earlier,
                        // where the repetition takes the first half too, unless something could tell the two apart.
                        return top && !backward && !this.hasBackreferences && n.Min <= 1 ? Start.Blocks : Start.Unsafe;
                    }

                    Start s = this.StartOf(n.Subs[0], top: false, backward);
                    return s == Start.Unsafe || n.Min > 0 ? s : Start.Passes;
                }

                default:
                    return Start.Unsafe;
            }
        }

        // Writing.
        private void Node(EcmaRegexNode n, Context context, EcmaRegexNode? next)
        {
            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Empty:
                    break;
                case EcmaRegexNodeKind.Char:
                    this.Character(n.CodePoint);
                    break;
                case EcmaRegexNodeKind.Set:
                    this.SetAtom(n, (context & Context.Backward) != 0);
                    break;
                case EcmaRegexNodeKind.Cat:
                {
                    bool backward = (context & Context.Backward) != 0;
                    for (int k = 0; k < n.Subs.Length; k++)
                    {
                        // What is matched after a term is the term to its right, or to its left in a lookbehind.
                        int after = backward ? k - 1 : k + 1;
                        bool inside = after >= 0 && after < n.Subs.Length;
                        this.Node(n.Subs[k], inside ? context & ~Context.Last : context, inside ? n.Subs[after] : next);
                    }

                    break;
                }

                case EcmaRegexNodeKind.Alt:
                    this.Alternation(n, context, next);
                    break;
                case EcmaRegexNodeKind.Group:
                    this.Group(n, context, next);
                    break;
                case EcmaRegexNodeKind.Repeat:
                    this.Repeat(n, context, next);
                    break;
                case EcmaRegexNodeKind.Bol:
                    this.sb.Append('^');
                    break;
                case EcmaRegexNodeKind.Eol:
                    this.sb.Append(@"\z");
                    break;
                case EcmaRegexNodeKind.LineStart:
                    this.sb.Append("(?:^|(?<=").Append(LineTerminatorClass).Append("))");
                    break;
                case EcmaRegexNodeKind.LineEnd:
                    this.sb.Append(@"(?:\z|(?=").Append(LineTerminatorClass).Append("))");
                    break;
                case EcmaRegexNodeKind.WordBoundary:
                {
                    string w = n.Fold ? FoldWordClass : WordClass;
                    this.sb.Append("(?:(?<=").Append(w).Append(")(?!").Append(w).Append(")|(?<!").Append(w).Append(")(?=").Append(w).Append("))");
                    break;
                }

                case EcmaRegexNodeKind.NotWordBoundary:
                {
                    string w = n.Fold ? FoldWordClass : WordClass;
                    this.sb.Append("(?:(?<=").Append(w).Append(")(?=").Append(w).Append(")|(?<!").Append(w).Append(")(?!").Append(w).Append("))");
                    break;
                }

                case EcmaRegexNodeKind.Look:
                    this.sb.Append(n.Behind ? (n.Negative ? "(?<!" : "(?<=") : (n.Negative ? "(?!" : "(?="));
                    int capturing = !n.Negative && this.AnyReferenced(n) ? 1 : 0;
                    this.inCapturingLookaround += capturing;
                    this.Node(n.Subs[0], n.Behind ? Context.Backward : Context.None, null);
                    this.inCapturingLookaround -= capturing;
                    this.sb.Append(')');
                    break;
                case EcmaRegexNodeKind.Backref:
                    this.Backreference(n, (context & Context.Backward) != 0);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Writes alternatives. An empty alternative beside one other is written as that other made optional, which
        /// is what .NET makes of it, and written so because .NET then goes wrong when the other is a repetition of
        /// one character and a repetition encloses both: it takes <c>(?:a+|)+</c> for <c>a+</c>.
        /// </summary>
        private void Alternation(EcmaRegexNode n, Context context, EcmaRegexNode? next)
        {
            // An empty alternative after another empty one is never the one that matches.
            List<EcmaRegexNode> subs = new(n.Subs.Length);
            bool seenEmpty = false;
            foreach (EcmaRegexNode sub in n.Subs)
            {
                if (sub.Kind == EcmaRegexNodeKind.Empty)
                {
                    if (seenEmpty)
                    {
                        continue;
                    }

                    seenEmpty = true;
                }

                subs.Add(sub);
            }

            if (subs.Count == 1)
            {
                this.Node(subs[0], context, next);
                return;
            }

            if (subs.Count == 2 && seenEmpty)
            {
                // The empty alternative first is the lazy form, which is kept only where the order of the paths counts.
                bool emptyFirst = subs[0].Kind == EcmaRegexNodeKind.Empty && this.inCapturingLookaround > 0;
                this.sb.Append("(?:");
                this.Node(subs[subs[0].Kind == EcmaRegexNodeKind.Empty ? 1 : 0], context, next);
                this.sb.Append(emptyFirst ? ")??" : ")?");
                this.keepsLazyGroupRepetition |= emptyFirst;
                return;
            }

            for (int k = 0; k < subs.Count; k++)
            {
                if (k > 0)
                {
                    this.sb.Append('|');
                }

                this.Node(subs[k], context, next);
            }
        }

        private void Group(EcmaRegexNode n, Context context, EcmaRegexNode? next)
        {
            if (n.Group == 0 || !this.referenced[n.Group])
            {
                this.sb.Append("(?:");
            }
            else
            {
                this.sb.Append("(?<").Append(n.Group.ToString(CultureInfo.InvariantCulture)).Append('>');
            }

            this.Node(n.Subs[0], context, next);
            this.sb.Append(')');
        }

        /// <summary>Writes a node as one unit that a quantifier can follow.</summary>
        private void Atom(EcmaRegexNode atom, Context context, EcmaRegexNode? next)
        {
            if (atom.Kind == EcmaRegexNodeKind.Set && atom.Set.Length != 0)
            {
                this.SetAtom(atom, (context & Context.Backward) != 0, wrap: true);
                return;
            }

            // A character beyond the Basic Multilingual Plane is written in a group of its own.
            bool unit = atom.Kind == EcmaRegexNodeKind.Group
                || (atom.Kind == EcmaRegexNodeKind.Backref && atom.ReferenceForm == 1)
                || (atom.Kind == EcmaRegexNodeKind.Char && (!this.unicode || !IsSurrogate(atom.CodePoint)));
            if (!unit)
            {
                this.sb.Append("(?:");
            }

            this.Node(atom, context, next);
            if (!unit)
            {
                this.sb.Append(')');
            }
        }

        private void Quantifier(int min, int max, bool lazy)
        {
            if (min == 1 && max == 1)
            {
                return;
            }

            if (min == 0 && max == EcmaRegexNode.Unbounded)
            {
                this.sb.Append('*');
            }
            else if (min == 1 && max == EcmaRegexNode.Unbounded)
            {
                this.sb.Append('+');
            }
            else if (min == 0 && max == 1)
            {
                this.sb.Append('?');
            }
            else
            {
                this.sb.Append('{').Append(min.ToString(CultureInfo.InvariantCulture));
                if (max != min)
                {
                    this.sb.Append(',');
                    if (max != EcmaRegexNode.Unbounded)
                    {
                        this.sb.Append(max.ToString(CultureInfo.InvariantCulture));
                    }
                }

                this.sb.Append('}');
            }

            if (lazy)
            {
                this.sb.Append('?');
            }
        }

        /// <summary>
        /// Whether a repetition is written as a repetition of code units: one of a set that holds every surrogate
        /// and every character beyond the Basic Multilingual Plane (the dot, a negated class), with no upper bound.
        /// Any run of the code units of such a set that does not start or end inside a surrogate pair is a run of
        /// its characters, so .NET can run it as it runs any class.
        /// </summary>
        private bool IsCodeUnitLoop(EcmaRegexNode n)
        {
            if (!this.unicode || n.Kind != EcmaRegexNodeKind.Repeat || n.Max != EcmaRegexNode.Unbounded || n.Subs[0].Kind != EcmaRegexNodeKind.Set)
            {
                return false;
            }

            return HoldsEveryPair(n.Subs[0].Set);
        }

        /// <summary>
        /// Whether what is matched after a repetition of code units could match when the repetition stops between the
        /// two halves of a surrogate pair. A character, a set and the end of the input cannot.
        /// </summary>
        private bool MayFollowInsidePair(EcmaRegexNode? next, Context context)
        {
            if (next is null)
            {
                // The end of the pattern is the end of the match, and a match that ends inside a pair is one that
                // ends after it too. The end of a group or of a lookaround is followed by something not known here.
                return (context & Context.Last) == 0;
            }

            switch (next.Kind)
            {
                case EcmaRegexNodeKind.Char:
                case EcmaRegexNodeKind.Set:
                case EcmaRegexNodeKind.Eol:
                case EcmaRegexNodeKind.Bol:
                case EcmaRegexNodeKind.LineStart:
                case EcmaRegexNodeKind.LineEnd:
                case EcmaRegexNodeKind.WordBoundary:
                    return false;
                case EcmaRegexNodeKind.Repeat:
                    return next.Min == 0 || this.IsCodeUnitLoop(next) || this.MayFollowInsidePair(next.Subs[0], Context.None);
                case EcmaRegexNodeKind.Group:
                    return next.Subs[0].Kind is not (EcmaRegexNodeKind.Char or EcmaRegexNodeKind.Set);
                default:
                    return true;
            }
        }

        /// <summary>
        /// Writes a repetition.
        /// <para>
        /// When a backreference reads a group of the body, each iteration starts by unsetting the groups of the body,
        /// as ECMA-262 does. .NET takes an iteration that matched nothing and then leaves the repetition, where
        /// ECMA-262 refuses such an iteration once the iterations that must run are done. The two reach the same
        /// places in the text, but .NET reaches them with the groups as the empty iteration left them. So the
        /// iterations that need not run are then written apart, each made to fail when it has matched nothing.
        /// </para>
        /// <para>
        /// A lazy repetition of anything but one code unit is written greedy. Whether a pattern matches does not
        /// depend on which of two paths is tried first, unless a lookaround ends the search at the first and a
        /// backreference reads what it captured. .NET's lazy repetition does not always end, and can throw: in both
        /// engines when an iteration matches nothing, and in the compiled one when it has a highest count and stands
        /// in another repetition. Inside such a lookaround the repetition stays lazy and every repetition keeps the
        /// order of its paths: the iterations that need not run are made to fail when they have matched nothing, and
        /// a lazy repetition with a highest count is written as optional groups one in another, or, past
        /// <see cref="MaxNestedLazy"/> of them, as a lazy repetition with no highest count whose iterations a group
        /// counts.
        /// </para>
        /// </summary>
        private void Repeat(EcmaRegexNode n, Context context, EcmaRegexNode? next)
        {
            EcmaRegexNode body = n.Subs[0];
            int min = n.Min;
            int max = n.Max;
            bool lazy = n.Lazy;
            bool backward = (context & Context.Backward) != 0;
            Context inner = context & Context.Backward;
            if (this.IsCodeUnitLoop(n))
            {
                // The iterations that must run are counted by character, and the rest are any code units.
                if (min > 1)
                {
                    if (backward)
                    {
                        this.CodeUnitLoop(body, 0, lazy, this.MayFollowInsidePair(next, context), backward);
                        this.SetAtom(body, backward, wrap: true);
                        this.Quantifier(min, min, lazy: false);
                    }
                    else
                    {
                        this.SetAtom(body, backward, wrap: true);
                        this.Quantifier(min, min, lazy: false);
                        this.CodeUnitLoop(body, 0, lazy, this.MayFollowInsidePair(next, context), backward);
                    }
                }
                else
                {
                    this.CodeUnitLoop(body, min, lazy, this.MayFollowInsidePair(next, context), backward);
                }

                return;
            }

            if (this.unicode && max == EcmaRegexNode.Unbounded && body.Kind == EcmaRegexNodeKind.Set && body.Set.Length != 0 && !this.IsOneClass(body))
            {
                this.SetRuns(body, min, lazy, backward);
                return;
            }

            if (this.unicode && max > 1 && body.Kind == EcmaRegexNodeKind.Set && HoldsEveryPair(body.Set) && this.CountedCodeUnits(body, min, max, lazy, backward))
            {
                return;
            }

            bool nullable = this.MinLength(body) == 0;
            bool resets = (max == EcmaRegexNode.Unbounded || max > 1) && this.AnyReferenced(n);

            // One code unit. A lazy repetition of it is one .NET runs without the faults of its other lazy repetitions.
            bool single = (body.Kind == EcmaRegexNodeKind.Char && (!this.unicode || !IsSurrogate(body.CodePoint)) && body.CodePoint <= 0xFFFF)
                || (body.Kind == EcmaRegexNodeKind.Set && this.IsOneClass(body));
            if (lazy && this.inCapturingLookaround == 0 && (nullable || !single))
            {
                lazy = false;
            }

            if (nullable && min > 0 && this.inCapturingLookaround == 0 && !this.AnyReferenced(n) && EmptyAnywhere(body))
            {
                // Every iteration that must run can match nothing, whatever the text, and leaves nothing behind.
                min = 0;
            }

            // An iteration that need not run is made to fail when it has matched nothing, where .NET would go on with
            // the groups as that iteration left them, and where the order of the paths counts.
            bool guarded = nullable && max != min && (this.inCapturingLookaround > 0 || this.AnyReferenced(n));
            if (!guarded && nullable && max != min && HoldsBackreference(body) && !(min > 0 && IsOneReference(body)))
            {
                // A backreference matches nothing whenever its group is unset or empty. .NET takes an iteration that
                // matched nothing and then tries again without it, from the same place and with the same groups, so a
                // pattern with several such repetitions is searched many times over, where ECMA-262 refuses the
                // iteration. The iterations that need not run are written as a body that cannot match nothing, or
                // made to fail when they have, where there is no such body. A body that is one backreference, with
                // an iteration that must run, needs neither: when that iteration matches nothing .NET runs no other,
                // and when it matches something so does every other.
                int budget = NonEmptyBudget;
                if (this.NonEmpty(body, ref budget) is EcmaRegexNode nonEmpty)
                {
                    if (!backward && min > 0)
                    {
                        this.Atom(body, inner, null);
                        this.Quantifier(min, min, lazy: false);
                    }

                    if (!IsNever(nonEmpty))
                    {
                        this.Atom(nonEmpty, inner, null);
                        this.Quantifier(0, max == EcmaRegexNode.Unbounded ? max : max - min, lazy: false);
                    }

                    if (backward && min > 0)
                    {
                        this.Atom(body, inner, null);
                        this.Quantifier(min, min, lazy: false);
                    }

                    return;
                }

                guarded = true;
            }

            // A lazy repetition that is still lazy here, with a highest count, is written as optional groups one in
            // another.
            bool nested = lazy && !single && max > 1 && max != EcmaRegexNode.Unbounded && max - min >= 1 && max - min <= MaxNestedLazy;

            // With more iterations left to choice than that, it is a lazy repetition with no highest count, and a
            // group counts the iterations.
            bool counted = lazy && !single && max != EcmaRegexNode.Unbounded && max - min > MaxNestedLazy;
            this.keepsLazyGroupRepetition |= lazy && !single;
            if (!resets && !guarded && !nested && !counted)
            {
                this.Atom(body, inner, null);
                this.Quantifier(min, max, lazy);
                return;
            }

            string reset = resets ? this.Resets(n) : string.Empty;
            if (!guarded && !nested && !counted)
            {
                this.Iteration(body, inner, reset, null);
                this.Quantifier(min, max, lazy);
                return;
            }

            // The iterations that must run, which may match nothing, and then the ones that need not, which may not.
            // In a lookbehind the ones that must run are matched first, and they stand on the right.
            string? marker = guarded ? "e" + (++this.markers).ToString(CultureInfo.InvariantCulture) : null;
            int rest = max == EcmaRegexNode.Unbounded ? EcmaRegexNode.Unbounded : max - min;
            if (!backward && min > 0)
            {
                this.Iteration(body, inner, reset, null);
                this.Quantifier(min, min, lazy: false);
            }

            if (nested)
            {
                // X{0,3}? is (?:X(?:X(?:X)??)??)??, and in a lookbehind (?:(?:(?:X)??X)??X)??.
                for (int k = 0; k < rest; k++)
                {
                    this.sb.Append("(?:");
                    if (!backward)
                    {
                        this.Iteration(body, inner, reset, marker);
                    }
                }

                for (int k = 0; k < rest; k++)
                {
                    if (backward)
                    {
                        this.Iteration(body, inner, reset, marker);
                    }

                    this.sb.Append(")??");
                }
            }
            else if (counted)
            {
                // A group is given one capture for each iteration that may run, an iteration takes one away and
                // cannot start when none is left, and what is left is taken away after the last. A repetition of
                // an exact count runs every iteration, though it matches nothing.
                string counter = "c" + (++this.markers).ToString(CultureInfo.InvariantCulture);
                string count = rest.ToString(CultureInfo.InvariantCulture);
                string give = "(?:(?<" + counter + ">)){" + count + "}";
                string take = "(?<-" + counter + ">)";
                string clear = "(?:(?(" + counter + ")" + take + ")){" + count + "}";
                this.sb.Append(backward ? clear : give).Append("(?:");
                if (!backward)
                {
                    this.sb.Append(take);
                }

                this.Iteration(body, inner, reset, marker);
                if (backward)
                {
                    this.sb.Append(take);
                }

                this.sb.Append(")*?").Append(backward ? give : clear);
            }
            else
            {
                this.Iteration(body, inner, reset, marker);
                this.Quantifier(0, rest, lazy);
            }

            if (backward && min > 0)
            {
                this.Iteration(body, inner, reset, null);
                this.Quantifier(min, min, lazy: false);
            }
        }

        /// <summary>
        /// A tree that matches what a node matches, the empty string left out, or null when there is none to be had
        /// within <see cref="NonEmptyBudget"/>. A node that never matches stands for a node that can only match the
        /// empty string. The node holds no group a backreference reads, and is not in a lookaround whose captures one
        /// reads, so neither the groups nor the order of the paths count.
        /// <para>
        /// Terms one after another match something when one of them is the first to: the terms before it match
        /// nothing (<see cref="OnlyEmpty"/>), it matches something, and the rest match as they do. A repetition
        /// matches something when its iterations do, since the ones that need not run cannot match nothing. A
        /// backreference to a group that cannot capture the empty string matches something exactly when the group is
        /// set, which is how .NET reads a backreference with no test of the group around it.
        /// </para>
        /// </summary>
        private EcmaRegexNode? NonEmpty(EcmaRegexNode n, ref int budget)
        {
            if (--budget < 0)
            {
                return null;
            }

            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Char:
                case EcmaRegexNodeKind.Set:
                    return n;
                case EcmaRegexNodeKind.Backref:
                    return this.ReadsTextOnly(n)
                        ? new EcmaRegexNode(EcmaRegexNodeKind.Backref) { Groups = n.Groups, Fold = n.Fold, ReferenceForm = 1 }
                        : null;
                case EcmaRegexNodeKind.Group:
                    return this.NonEmpty(n.Subs[0], ref budget) is EcmaRegexNode grouped
                        ? (IsNever(grouped) || grouped.Kind is EcmaRegexNodeKind.Backref or EcmaRegexNodeKind.Char or EcmaRegexNodeKind.Set or EcmaRegexNodeKind.Group
                            ? grouped
                            : new EcmaRegexNode(EcmaRegexNodeKind.Group) { Subs = [grouped] })
                        : null;
                case EcmaRegexNodeKind.Alt:
                {
                    List<EcmaRegexNode> subs = [];
                    foreach (EcmaRegexNode alternative in n.Subs)
                    {
                        if (this.NonEmpty(alternative, ref budget) is not EcmaRegexNode sub)
                        {
                            return null;
                        }

                        if (!IsNever(sub))
                        {
                            subs.Add(sub);
                        }
                    }

                    return subs.Count == 0 ? NeverNode() : subs.Count == 1 ? subs[0] : new EcmaRegexNode(EcmaRegexNodeKind.Alt) { Subs = [.. subs] };
                }

                case EcmaRegexNodeKind.Cat:
                {
                    // One alternative for each term that can be the first to match something.
                    List<EcmaRegexNode> alternatives = [];
                    List<EcmaRegexNode> before = [];
                    for (int k = 0; k < n.Subs.Length; k++)
                    {
                        if (this.NonEmpty(n.Subs[k], ref budget) is not EcmaRegexNode first)
                        {
                            return null;
                        }

                        if (!IsNever(first))
                        {
                            List<EcmaRegexNode> terms = [];
                            if (before.Count > 0)
                            {
                                // What holds where the terms before match nothing, once however many ways it holds.
                                terms.Add(new EcmaRegexNode(EcmaRegexNodeKind.Look) { Subs = [before.Count == 1 ? before[0] : new EcmaRegexNode(EcmaRegexNodeKind.Cat) { Subs = [.. before] }] });
                            }

                            terms.Add(first);
                            for (int rest = k + 1; rest < n.Subs.Length; rest++)
                            {
                                terms.Add(n.Subs[rest]);
                            }

                            budget -= terms.Count;
                            alternatives.Add(terms.Count == 1 ? terms[0] : new EcmaRegexNode(EcmaRegexNodeKind.Cat) { Subs = [.. terms] });
                        }

                        if (this.MinLength(n.Subs[k]) > 0)
                        {
                            break;
                        }

                        if (this.OnlyEmpty(n.Subs[k], ref budget) is not EcmaRegexNode nothing)
                        {
                            return null;
                        }

                        if (IsNever(nothing))
                        {
                            break;
                        }

                        if (nothing.Kind != EcmaRegexNodeKind.Empty)
                        {
                            before.Add(nothing);
                        }
                    }

                    return budget < 0
                        ? null
                        : alternatives.Count == 0 ? NeverNode() : alternatives.Count == 1 ? alternatives[0] : new EcmaRegexNode(EcmaRegexNodeKind.Alt) { Subs = [.. alternatives] };
                }

                case EcmaRegexNodeKind.Repeat:
                {
                    EcmaRegexNode body = n.Subs[0];
                    if (n.Max == 0)
                    {
                        return NeverNode();
                    }

                    if (this.MinLength(body) > 0)
                    {
                        // Every iteration matches something, so the repetition does when one runs.
                        return n.Min > 0 ? n : new EcmaRegexNode(EcmaRegexNodeKind.Repeat) { Subs = [body], Min = 1, Max = n.Max, Lazy = n.Lazy };
                    }

                    // With one iteration that must run, or none, the repetition matches something exactly when some
                    // number of iterations, up to its highest count, each match something: an iteration that must run
                    // and matches nothing adds no text. With two or more that must run it is so when an iteration
                    // can match nothing wherever it stands.
                    if (n.Min > 1 && !EmptyAnywhere(body))
                    {
                        return null;
                    }

                    return this.NonEmpty(body, ref budget) is EcmaRegexNode repeated
                        ? (IsNever(repeated) ? repeated : new EcmaRegexNode(EcmaRegexNodeKind.Repeat) { Subs = [repeated], Min = 1, Max = n.Max, Lazy = n.Lazy })
                        : null;
                }

                default:
                    // The empty node and every assertion.
                    return NeverNode();
            }
        }

        /// <summary>
        /// A tree that matches nothing, and holds exactly where a node can match the empty string, or null when
        /// there is none to be had. A node that never matches stands for a node that cannot match the empty string,
        /// and the empty node for one that can wherever it stands.
        /// </summary>
        private EcmaRegexNode? OnlyEmpty(EcmaRegexNode n, ref int budget)
        {
            if (--budget < 0)
            {
                return null;
            }

            if (this.MinLength(n) > 0)
            {
                return NeverNode();
            }

            if (EmptyAnywhere(n))
            {
                return new EcmaRegexNode(EcmaRegexNodeKind.Empty);
            }

            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Backref:
                    return this.ReadsTextOnly(n)
                        ? new EcmaRegexNode(EcmaRegexNodeKind.Backref) { Groups = n.Groups, Fold = n.Fold, ReferenceForm = 2 }
                        : null;
                case EcmaRegexNodeKind.Group:
                    return this.OnlyEmpty(n.Subs[0], ref budget);
                case EcmaRegexNodeKind.Repeat:
                    // No iteration must run here only when the node matches the empty string wherever it stands, so
                    // one must, and the ones that must all stand at the same place.
                    return this.OnlyEmpty(n.Subs[0], ref budget);
                case EcmaRegexNodeKind.Alt:
                {
                    List<EcmaRegexNode> subs = [];
                    foreach (EcmaRegexNode alternative in n.Subs)
                    {
                        if (this.OnlyEmpty(alternative, ref budget) is not EcmaRegexNode sub)
                        {
                            return null;
                        }

                        if (!IsNever(sub))
                        {
                            subs.Add(sub);
                        }
                    }

                    return subs.Count == 0 ? NeverNode() : subs.Count == 1 ? subs[0] : new EcmaRegexNode(EcmaRegexNodeKind.Alt) { Subs = [.. subs] };
                }

                case EcmaRegexNodeKind.Cat:
                {
                    List<EcmaRegexNode> subs = [];
                    foreach (EcmaRegexNode term in n.Subs)
                    {
                        if (this.OnlyEmpty(term, ref budget) is not EcmaRegexNode sub)
                        {
                            return null;
                        }

                        if (IsNever(sub))
                        {
                            return sub;
                        }

                        if (sub.Kind != EcmaRegexNodeKind.Empty)
                        {
                            subs.Add(sub);
                        }
                    }

                    return subs.Count == 0 ? new EcmaRegexNode(EcmaRegexNodeKind.Empty) : subs.Count == 1 ? subs[0] : new EcmaRegexNode(EcmaRegexNodeKind.Cat) { Subs = [.. subs] };
                }

                default:
                    // An assertion holds where it holds.
                    return n;
            }
        }

        /// <summary>
        /// Whether a backreference reads only groups that cannot capture the empty string, so that it matches the
        /// empty string exactly when no group it reads is set.
        /// </summary>
        private bool ReadsTextOnly(EcmaRegexNode n)
        {
            bool any = false;
            foreach (int g in n.Groups)
            {
                if (!this.dead[g])
                {
                    if (this.groupNodes[g] is not EcmaRegexNode group || this.MinLength(group) == 0)
                    {
                        return false;
                    }

                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Writes one iteration of a repetition as a group: what unsets the groups of the body, then the body. With
        /// <paramref name="marker"/> the iteration fails when it has matched nothing: a group of that name captures
        /// the rest of the input when the iteration starts, and the iteration ends only where the rest of the input
        /// is no longer that text. In a lookbehind everything is matched from right to left, so what comes first
        /// stands last, and the group captures the input before the iteration.
        /// </summary>
        private void Iteration(EcmaRegexNode body, Context inner, string reset, string? marker)
        {
            if (reset.Length == 0 && marker is null)
            {
                this.Atom(body, inner, null);
                return;
            }

            bool backward = (inner & Context.Backward) != 0;
            this.sb.Append("(?:");
            if (!backward)
            {
                this.sb.Append(reset);
                if (marker is not null)
                {
                    this.sb.Append("(?=(?<").Append(marker).Append(@">[\s\S]*))");
                }

                this.Node(body, inner, null);
                if (marker is not null)
                {
                    this.sb.Append(@"(?!\k<").Append(marker).Append(@">\z)");
                }
            }
            else
            {
                if (marker is not null)
                {
                    this.sb.Append(@"(?<!^\k<").Append(marker).Append(">)");
                }

                this.Node(body, inner, null);
                if (marker is not null)
                {
                    this.sb.Append("(?<=(?<").Append(marker).Append(@">^[\s\S]*))");
                }

                this.sb.Append(reset);
            }

            this.sb.Append(')');
        }

        /// <summary>What unsets every group of a node that a backreference reads.</summary>
        private string Resets(EcmaRegexNode n)
        {
            StringBuilder reset = new();
            for (int g = n.FirstGroup + 1; g <= n.LastGroup; g++)
            {
                if (this.referenced[g])
                {
                    string number = g.ToString(CultureInfo.InvariantCulture);
                    reset.Append("(?(").Append(number).Append(")(?<-").Append(number).Append(">))");
                }
            }

            return reset.ToString();
        }

        /// <summary>
        /// Writes a backreference, which in ECMA-262 matches the text the group captured when the group has taken
        /// part and the empty string when it has not. A name can belong to several groups in separate alternatives,
        /// of which at most one has taken part, so the tests of each follow one another.
        /// </summary>
        private void Backreference(EcmaRegexNode n, bool backward)
        {
            if (n.ReferenceForm == 1)
            {
                // The text of the group that is set, and no match when none is. .NET fails a backreference to a group
                // that is unset. The group around it keeps a digit that follows out of the number.
                this.sb.Append("(?:");
                bool first = true;
                foreach (int g in n.Groups)
                {
                    if (this.dead[g])
                    {
                        continue;
                    }

                    string number = g.ToString(CultureInfo.InvariantCulture);
                    this.sb.Append(first ? string.Empty : "|");
                    first = false;
                    if (n.Fold && this.CapturesCased(g))
                    {
                        this.sb.Append("(?(").Append(number).Append(')');
                        this.FoldedBackreference(g, number, backward);
                        this.sb.Append('|').Append(Never).Append(')');
                    }
                    else
                    {
                        this.sb.Append('\\').Append(number);
                    }
                }

                this.sb.Append(')');
                return;
            }

            foreach (int g in n.Groups)
            {
                if (this.dead[g])
                {
                    continue;
                }

                string number = g.ToString(CultureInfo.InvariantCulture);
                this.sb.Append("(?(").Append(number).Append(')');
                if (n.ReferenceForm == 2)
                {
                    // Holds where the group is unset.
                    this.sb.Append(Never);
                }
                else if (n.Fold && this.CapturesCased(g))
                {
                    this.FoldedBackreference(g, number, backward);
                }
                else
                {
                    this.sb.Append('\\').Append(number);
                }

                this.sb.Append(')');
            }
        }

        /// <summary>
        /// Writes a backreference that ignores case, to a group whose text can hold a character with a case variant.
        /// <para>
        /// ECMA-262 compares the text of the group with the input character by character, and two characters are the
        /// same when Canonicalize gives the same character for both. .NET's own case-insensitive backreference
        /// compares by its own table, which is not that relation (with the u flag grammar it does not know that the
        /// two small sigmas are one letter, nor any pair beyond the Basic Multilingual Plane) and is not the same
        /// table in every runtime. So the comparison is written out, and .NET's is used only for the letters of ASCII
        /// that no character outside ASCII is a case variant of.
        /// </para>
        /// <para>
        /// The text of the group cannot be read where it stands in the pattern, but it stands somewhere in the input.
        /// A group <c>q</c> holds the input from a place in a copy of that text to the end, which names the place,
        /// and <c>z</c> the input from the end of the copy. Each step looks at the next character of the input, goes
        /// to the place <c>q</c> names (from the end of the input, back over the text of <c>q</c>), tests the
        /// character there, moves <c>q</c> past it and takes the character of the input. A step is one of: the same
        /// character, an ASCII letter and its other case, or two characters of one class of Canonicalize, for each
        /// class that has a member the group can capture. The steps end when <c>q</c> is <c>z</c>. In a lookbehind
        /// the same comparison runs forward from a place before the current one and has to end at the current one.
        /// </para>
        /// </summary>
        private void FoldedBackreference(int g, string number, bool backward)
        {
            int[] captured = this.CapturedSets()[g];
            if (captured[captured.Length - 1] < 0x80 && !EcmaRegexUnicode.Intersects(captured, NotPlainAsciiLetters))
            {
                // The text of the group is ASCII with none of the letters that have a case variant outside ASCII, and
                // for such a text .NET's comparison is ECMA-262's, in every runtime.
                this.sb.Append(@"(?i:\").Append(number).Append(')');
                return;
            }

            string id = (++this.markers).ToString(CultureInfo.InvariantCulture);
            string q = "q" + id;
            string z = "z" + id;
            string u = "u" + id;
            string boundary = this.unicode ? "(?!" + InsideSurrogatePair + ")" : string.Empty;
            string character = this.unicode ? HighSurrogates + LowSurrogates + @"|[\s\S]" : @"[\s\S]";

            // What goes to the place q names, tests there, and moves q on.
            string home = @"(?=[\s\S]*\z(?<=(?=(?!\k<" + z + @">\z)";
            string moved = "(?<" + q + @">[\s\S]*))\k<" + q + ">))";

            if (backward)
            {
                this.sb.Append("(?=").Append(boundary);
            }

            // A copy of the text of the group, which starts and ends between characters.
            this.sb.Append(@"(?<=\A(?=[\s\S]*?(?=").Append(boundary).Append('\\').Append(number).Append(boundary)
                .Append("(?<").Append(z).Append(@">[\s\S]*))(?<").Append(q).Append(@">[\s\S]*))[\s\S]*)");

            // The same character.
            this.sb.Append("(?>(?:(?=(?<").Append(u).Append('>').Append(character).Append("))").Append(home)
                .Append(@"\k<").Append(u).Append('>').Append(boundary).Append(moved).Append(@"\k<").Append(u).Append('>');

            bool ascii = false;
            List<EcmaRegexNode> classes = [];
            foreach (int[] members in EcmaRegexUnicode.FoldClasses(captured, this.unicode))
            {
                if (members.Length == 2 && members[0] + 0x20 == members[1] && members[0] >= 'A' && members[0] <= 'Z'
                    && members[0] != 'I' && members[0] != 'K' && members[0] != 'S')
                {
                    ascii = true;
                    continue;
                }

                int[] set = new int[2 * members.Length];
                for (int k = 0; k < members.Length; k++)
                {
                    set[2 * k] = members[k];
                    set[(2 * k) + 1] = members[k];
                }

                classes.Add(new EcmaRegexNode(EcmaRegexNodeKind.Set) { Set = EcmaRegexUnicode.SetOf(set, set.Length) });
            }

            // Two characters of one class. Many classes are tried so many at a time, behind a look at the next
            // character for a member of any of them.
            for (int first = 0; first < classes.Count; first += FoldClassesTogether)
            {
                int end = Math.Min(classes.Count, first + FoldClassesTogether);
                bool together = classes.Count > FoldClassesTogether;
                if (together)
                {
                    int[] any = [];
                    for (int k = first; k < end; k++)
                    {
                        any = EcmaRegexUnicode.Union(any, classes[k].Set);
                    }

                    this.sb.Append("|(?=");
                    this.SetAtom(new EcmaRegexNode(EcmaRegexNodeKind.Set) { Set = any }, backward: false);
                    this.sb.Append(")(?:");
                }

                for (int k = first; k < end; k++)
                {
                    this.sb.Append(together && k == first ? "(?=" : "|(?=");
                    this.SetAtom(classes[k], backward: false);
                    this.sb.Append(')').Append(home);
                    this.SetAtom(classes[k], backward: false);
                    this.sb.Append(moved);
                    this.SetAtom(classes[k], backward: false);
                }

                if (together)
                {
                    this.sb.Append(')');
                }
            }

            if (ascii)
            {
                // No character outside ASCII is a case variant of one of these letters, in ECMA-262 or in .NET.
                this.sb.Append("|(?=(?<").Append(u).Append(">").Append(PlainAsciiLetters).Append("))").Append(home)
                    .Append(@"(?i:\k<").Append(u).Append(">)").Append(moved).Append(PlainAsciiLetters);
            }

            // Every character of the copy has been compared.
            this.sb.Append(@")*)(?=[\s\S]*\z(?<=(?=\k<").Append(z).Append(@">\z)\k<").Append(q).Append(">))");

            if (backward)
            {
                // Matched from right to left: the current place is named, a place before it is tried, and the
                // comparison from there has to end at the current place.
                string p = "p" + id;
                this.sb.Append(@"(?=\k<").Append(p).Append(@">\z))[\s\S]*?(?=(?<").Append(p).Append(@">[\s\S]*))");
            }
        }

        /// <summary>Whether the text a group captures can hold a character that has a case variant.</summary>
        private bool CapturesCased(int g)
        {
            return this.groupNodes[g] is not null && EcmaRegexUnicode.Intersects(this.CapturedSets()[g], EcmaRegexUnicode.Cased(this.unicode));
        }

        /// <summary>
        /// For each group, every character the text it captures can hold. A group can hold the text of another
        /// through a backreference, and of itself, in any order, so the sets are the least ones that satisfy every
        /// group: they start empty and are worked out again until none of them grows. Following each backreference
        /// to its group instead does not end for a group that refers to itself twice.
        /// </summary>
        private int[][] CapturedSets()
        {
            if (this.capturedSets is int[][] known)
            {
                return known;
            }

            int[][] sets = new int[this.groupNodes.Length][];
            for (int g = 0; g < sets.Length; g++)
            {
                sets[g] = [];
            }

            bool grew;
            do
            {
                grew = false;
                for (int g = 1; g < sets.Length; g++)
                {
                    if (this.groupNodes[g] is EcmaRegexNode group)
                    {
                        int[] set = this.CapturedSet(group, sets);
                        if (!EcmaRegexUnicode.SetEquals(set, sets[g]))
                        {
                            sets[g] = set;
                            grew = true;
                        }
                    }
                }
            }
            while (grew);

            this.capturedSets = sets;
            return sets;
        }

        /// <summary>Every character the text a node matches can hold, given what each group can.</summary>
        private int[] CapturedSet(EcmaRegexNode n, int[][] groups)
        {
            switch (n.Kind)
            {
                case EcmaRegexNodeKind.Char:
                    return [n.CodePoint, n.CodePoint];
                case EcmaRegexNodeKind.Set:
                    return n.Set;
                case EcmaRegexNodeKind.Look:
                    return [];
                case EcmaRegexNodeKind.Backref:
                {
                    int[] set = [];
                    foreach (int g in n.Groups)
                    {
                        set = EcmaRegexUnicode.Union(set, n.Fold ? EcmaRegexUnicode.FoldClosure(groups[g], this.unicode) : groups[g]);
                    }

                    return set;
                }

                default:
                {
                    int[] set = [];
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        set = EcmaRegexUnicode.Union(set, this.CapturedSet(sub, groups));
                    }

                    return set;
                }
            }
        }

        // Characters and sets.

        /// <summary>Writes one literal character.</summary>
        private void Character(int c)
        {
            if (c > 0xFFFF)
            {
                // The group makes a quantifier that follows apply to both halves.
                this.sb.Append("(?:");
                this.Escape(char.ConvertFromUtf32(c)[0]);
                this.Escape(char.ConvertFromUtf32(c)[1]);
                this.sb.Append(')');
            }
            else if (this.unicode && c >= 0xD800 && c <= 0xDBFF)
            {
                // A lone surrogate in a pattern read by code point does not match half of a pair.
                this.Escape((char)c);
                this.sb.Append("(?!").Append(LowSurrogates).Append(')');
            }
            else if (this.unicode && c >= 0xDC00 && c <= 0xDFFF)
            {
                this.sb.Append("(?<!").Append(HighSurrogates).Append(')');
                this.Escape((char)c);
            }
            else
            {
                this.Literal((char)c, inClass: false);
            }
        }

        private void Escape(char c)
        {
            this.sb.Append(@"\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }

        /// <summary>Writes a code unit as itself where .NET reads it as itself, and as an escape elsewhere.</summary>
        private void Literal(char c, bool inClass)
        {
            switch (c)
            {
                case '\t':
                    this.sb.Append(@"\t");
                    return;
                case '\n':
                    this.sb.Append(@"\n");
                    return;
                case '\v':
                    this.sb.Append(@"\v");
                    return;
                case '\f':
                    this.sb.Append(@"\f");
                    return;
                case '\r':
                    this.sb.Append(@"\r");
                    return;
            }

            if (c < 0x20 || c > 0x7E)
            {
                this.Escape(c);
                return;
            }

            bool special = inClass
                ? c is '\\' or ']' or '[' or '^' or '-'
                : c is '\\' or '*' or '+' or '?' or '|' or '{' or '}' or '[' or ']' or '(' or ')' or '^' or '$' or '.';
            if (special)
            {
                this.sb.Append('\\');
            }

            this.sb.Append(c);
        }

        private void Ranges(int[] set)
        {
            for (int k = 0; k < set.Length; k += 2)
            {
                if (set[k + 1] == set[k])
                {
                    this.Literal((char)set[k], inClass: true);
                    continue;
                }

                // .NET does not take an escaped hyphen for the end of a range, so one is written by its number.
                this.RangeEnd((char)set[k]);
                if (set[k + 1] != set[k] + 1)
                {
                    this.sb.Append('-');
                }

                this.RangeEnd((char)set[k + 1]);
            }
        }

        private void RangeEnd(char c)
        {
            if (c == '-')
            {
                this.Escape(c);
            }
            else
            {
                this.Literal(c, inClass: true);
            }
        }

        private void CategoryClasses(int[] categories)
        {
            foreach (int tables in categories)
            {
                int left = tables;
                foreach ((int unionTables, string name) in CategoryUnions)
                {
                    if ((left & unionTables) == unionTables)
                    {
                        this.sb.Append(@"\p{").Append(name).Append('}');
                        left &= ~unionTables;
                    }
                }

                for (int bit = 0; bit < CategoryNames.Length; bit++)
                {
                    if ((left & (1 << bit)) != 0)
                    {
                        this.sb.Append(@"\p{").Append(CategoryNames[bit]).Append('}');
                    }
                }
            }
        }

        /// <summary>
        /// Whether a set is written as one class of .NET: every set of a pattern matched by code unit, and with the
        /// u flag grammar a set with no surrogate and no character beyond the Basic Multilingual Plane.
        /// </summary>
        private bool IsOneClass(EcmaRegexNode n)
        {
            int[] set = n.Set;
            if (set.Length == 0)
            {
                return false;
            }

            return !this.unicode || (set[set.Length - 1] <= 0xFFFF && !EcmaRegexUnicode.Intersects(set, SurrogateRange));
        }

        /// <summary>
        /// Writes the class of the code units of a set that are characters by themselves: all of them for a pattern
        /// matched by code unit, and otherwise those that are not surrogates. With <paramref name="surrogates"/> the
        /// surrogates are among them, which is right only for a set that holds them all. Returns false, writing
        /// nothing, when there is no such code unit.
        /// </summary>
        private bool UnitClass(EcmaRegexNode n, bool surrogates)
        {
            if (n.Items is not null && (n.Negated || n.Categories is not null || n.Items.Length > 0))
            {
                if (!n.Negated)
                {
                    // One General_Category value needs no brackets.
                    bool one = n.Items.Length == 0 && n.Categories!.Length == 1 && IsOneCategory(n.Categories[0]);
                    if (!one)
                    {
                        this.sb.Append('[');
                    }

                    if (n.Categories is not null)
                    {
                        this.CategoryClasses(n.Categories);
                    }

                    this.Ranges(n.Items);
                    if (!one)
                    {
                        this.sb.Append(']');
                    }

                    return true;
                }

                if (n.Items.Length == 0 && n.Categories is null && (surrogates || !this.unicode))
                {
                    this.sb.Append(@"[\s\S]");
                    return true;
                }

                this.sb.Append("[^");
                if (n.Categories is not null)
                {
                    this.CategoryClasses(n.Categories);
                }

                this.Ranges(n.Items);
                if (this.unicode && !surrogates)
                {
                    this.sb.Append(@"\uD800-\uDFFF");
                }

                this.sb.Append(']');
                return true;
            }

            int[] units = this.unicode
                ? EcmaRegexUnicode.Union(EcmaRegexUnicode.Slice(n.Set, 0, 0xD7FF), EcmaRegexUnicode.Slice(n.Set, surrogates ? 0xD800 : 0xE000, 0xFFFF))
                : n.Set;
            if (units.Length == 0)
            {
                return false;
            }

            if (units.Length == 2 && units[0] == 0 && units[1] == 0xFFFF)
            {
                this.sb.Append(@"[\s\S]");
                return true;
            }

            if (units.Length == 2 && units[0] == units[1])
            {
                this.Literal((char)units[0], inClass: false);
                return true;
            }

            // A set that leaves out a few characters is shorter written by what it leaves out.
            int[] others = EcmaRegexUnicode.Complement(units, 0xFFFF);
            if (others.Length < units.Length)
            {
                this.sb.Append("[^");
                this.Ranges(others);
                this.sb.Append(']');
                return true;
            }

            this.sb.Append('[');
            this.Ranges(units);
            this.sb.Append(']');
            return true;
        }

        /// <summary>
        /// Writes a repetition of a set by code unit (see <see cref="IsCodeUnitLoop"/>). With <paramref name="guard"/>
        /// it may not stop between the two halves of a surrogate pair.
        /// </summary>
        private void CodeUnitLoop(EcmaRegexNode set, int min, bool lazy, bool guard, bool backward)
        {
            // In a lookbehind the repetition runs to the left, so it stops at its left.
            if (guard && backward)
            {
                this.sb.Append("(?!").Append(InsideSurrogatePair).Append(')');
            }

            this.UnitClass(set, surrogates: true);
            this.Quantifier(min, EcmaRegexNode.Unbounded, lazy);
            if (guard && !backward)
            {
                this.sb.Append("(?!").Append(InsideSurrogatePair).Append(')');
            }
        }

        /// <summary>
        /// Writes what matches one character of a set. With the u flag grammar a character is a code point, and what
        /// is written matches by code unit: a code unit that is not a surrogate, or a surrogate pair, or a surrogate
        /// that is not half of a pair. So the dot, which is every character but four, is written
        /// <c>(?&gt;[^\n\r\u2028\u2029\uD800-\uDFFF]|[\uD800-\uDBFF](?:[\uDC00-\uDFFF]|(?![\uDC00-\uDFFF]))|(?&lt;![\uD800-\uDBFF])[\uDC00-\uDFFF])</c>.
        /// </summary>
        private void SetAtom(EcmaRegexNode n, bool backward, bool wrap = false)
        {
            int[] set = n.Set;
            if (set.Length == 0)
            {
                // No character.
                this.sb.Append(Never);
                return;
            }

            if (this.IsOneClass(n))
            {
                this.UnitClass(n, surrogates: false);
                return;
            }

            int start = this.sb.Length;
            int alternatives = this.UnitClass(n, surrogates: false) ? 1 : 0;
            if (alternatives > 0)
            {
                this.sb.Append('|');
            }

            alternatives += this.BeyondUnits(n, backward);
            if (alternatives > 1)
            {
                // No two of the alternatives start with the same code unit, so the first that matches is the only
                // one that can, and .NET need not keep the others to come back to.
                this.sb.Insert(start, "(?>").Append(')');
            }
            else if (wrap)
            {
                this.sb.Insert(start, "(?:").Append(')');
            }
        }

        /// <summary>
        /// Writes, as alternatives, the characters of a set that are not one code unit that stands for itself: its
        /// characters beyond the Basic Multilingual Plane as surrogate pairs, and its surrogates, each where it is
        /// not half of a pair. Every one of them starts with a surrogate (and ends with one, which is where a
        /// lookbehind starts), so when there are many they are written as one alternative, behind a test for a
        /// surrogate that turns away at once a character that is none. Returns the number of alternatives written.
        /// </summary>
        private int BeyondUnits(EcmaRegexNode n, bool backward)
        {
            int start = this.sb.Length;
            int[] set = n.Set;
            int[] high = EcmaRegexUnicode.Slice(set, 0xD800, 0xDBFF);
            int[] low = EcmaRegexUnicode.Slice(set, 0xDC00, 0xDFFF);
            int[] supplementary = EcmaRegexUnicode.Slice(set, 0x10000, EcmaRegexUnicode.Max);
            int alternatives = 0;
            if (EcmaRegexUnicode.SetEquals(high, HighSurrogateRange) && EcmaRegexUnicode.SetEquals(supplementary, SupplementaryRange))
            {
                // A first half, with the second half when it is there.
                this.Or(ref alternatives);
                this.sb.Append(HighSurrogates).Append("(?:").Append(LowSurrogates).Append("|(?!").Append(LowSurrogates).Append("))");
            }
            else
            {
                if (supplementary.Length > 0)
                {
                    this.Or(ref alternatives);
                    alternatives += this.SurrogatePairs(supplementary) - 1;
                }

                if (high.Length > 0)
                {
                    this.Or(ref alternatives);
                    this.sb.Append('[');
                    this.Ranges(high);
                    this.sb.Append("](?!").Append(LowSurrogates).Append(')');
                }
            }

            if (low.Length > 0)
            {
                this.Or(ref alternatives);
                this.sb.Append("(?<!").Append(HighSurrogates).Append(")[");
                this.Ranges(low);
                this.sb.Append(']');
            }

            if (alternatives > ManyAlternatives)
            {
                // What is matched first stands first, and in a lookbehind last.
                if (backward)
                {
                    this.sb.Insert(start, "(?:").Append(@")(?<=[\uD800-\uDFFF])");
                }
                else
                {
                    this.sb.Insert(start, @"(?=[\uD800-\uDFFF])(?:").Append(')');
                }

                return 1;
            }

            return alternatives;
        }

        /// <summary>
        /// Writes a counted repetition, of at most <paramref name="max"/> times, of a set that holds every surrogate
        /// and every character beyond the Basic Multilingual Plane (the dot, a negated class). When none of the next
        /// <paramref name="max"/> code units is a surrogate, each of them is a character, and the repetition is one of
        /// the class of the set's code units, which .NET runs as it runs any class. A conditional tests for that, and
        /// its other branch counts characters, each as the set once. In a lookbehind the test is of the code units
        /// before. Returns false, writing nothing, when the set has no code unit that is a character by itself.
        /// </summary>
        private bool CountedCodeUnits(EcmaRegexNode set, int min, int max, bool lazy, bool backward)
        {
            int start = this.sb.Length;
            string count = max.ToString(CultureInfo.InvariantCulture);
            this.sb.Append(backward ? @"(?(?<=[^\uD800-\uDFFF]{" : @"(?(?=[^\uD800-\uDFFF]{").Append(count);
            this.sb.Append(backward ? @"}|^[^\uD800-\uDFFF]*)" : @"}|[^\uD800-\uDFFF]*\z)");
            if (!this.UnitClass(set, surrogates: false))
            {
                this.sb.Length = start;
                return false;
            }

            this.Quantifier(min, max, lazy);
            this.sb.Append('|');
            this.SetAtom(set, backward, wrap: true);
            this.Quantifier(min, max, lazy);
            this.sb.Append(')');
            return true;
        }

        /// <summary>
        /// Writes a repetition with no upper bound of a set that holds some characters that are not one code unit
        /// (a class with a character beyond the Basic Multilingual Plane, most <c>\p{...}</c> properties). Where C is
        /// the class of its single code units and P its surrogate pairs, the set repeated is
        /// <c>C*(?:PC*)*</c>: runs of code units with a pair between one run and the next. That is one class
        /// repetition for a text with no such character, where an alternation of C and P would be tried again for
        /// every character. The iterations that must run come first, each as the set once. In a lookbehind
        /// everything stands the other way round.
        /// </summary>
        private void SetRuns(EcmaRegexNode set, int min, bool lazy, bool backward)
        {
            int mark = this.sb.Length;
            bool hasUnits = this.UnitClass(set, surrogates: false);
            string units = this.sb.ToString(mark, this.sb.Length - mark);
            this.sb.Length = mark;
            if (!hasUnits)
            {
                // Nothing but pairs, so there are no runs between them.
                this.SetAtom(set, backward, wrap: true);
                this.Quantifier(min, EcmaRegexNode.Unbounded, lazy);
                return;
            }

            int count = this.BeyondUnits(set, backward);
            string beyond = this.sb.ToString(mark, this.sb.Length - mark);
            this.sb.Length = mark;
            if (count > 1)
            {
                beyond = "(?>" + beyond + ")";
            }

            string star = lazy ? "*?" : "*";
            if (backward)
            {
                this.sb.Append("(?:").Append(units).Append(star).Append(beyond).Append(')').Append(star).Append(units).Append(star);
            }

            if (min > 0)
            {
                this.SetAtom(set, backward, wrap: true);
                this.Quantifier(min, min, lazy: false);
            }

            if (!backward)
            {
                this.sb.Append(units).Append(star).Append("(?:").Append(beyond).Append(units).Append(star).Append(')').Append(star);
            }
        }

        private void Or(ref int alternatives)
        {
            if (alternatives++ > 0)
            {
                this.sb.Append('|');
            }
        }

        /// <summary>
        /// Writes the characters beyond the Basic Multilingual Plane of a set as alternatives, each a first half or
        /// a class of first halves and then a class of second halves. Returns the number of alternatives.
        /// </summary>
        private int SurrogatePairs(int[] supplementary)
        {
            // Each piece is a range of first halves that share a range of second halves.
            List<(int HighFirst, int HighLast, int LowFirst, int LowLast)> pieces = [];
            for (int k = 0; k < supplementary.Length; k += 2)
            {
                int a = supplementary[k] - 0x10000;
                int b = supplementary[k + 1] - 0x10000;
                int ha = 0xD800 + (a >> 10);
                int la = 0xDC00 + (a & 0x3FF);
                int hb = 0xD800 + (b >> 10);
                int lb = 0xDC00 + (b & 0x3FF);
                if (ha == hb)
                {
                    pieces.Add((ha, ha, la, lb));
                    continue;
                }

                if (la != 0xDC00)
                {
                    pieces.Add((ha, ha, la, 0xDFFF));
                    ha++;
                }

                if (lb != 0xDFFF)
                {
                    if (ha <= hb - 1)
                    {
                        pieces.Add((ha, hb - 1, 0xDC00, 0xDFFF));
                    }

                    pieces.Add((hb, hb, 0xDC00, lb));
                }
                else if (ha <= hb)
                {
                    pieces.Add((ha, hb, 0xDC00, 0xDFFF));
                }
            }

            int count = 0;
            for (int k = 0; k < pieces.Count;)
            {
                if (count++ > 0)
                {
                    this.sb.Append('|');
                }

                (int highFirst, int highLast, int lowFirst, int lowLast) = pieces[k];
                if (highFirst == highLast)
                {
                    // The pieces of one first half share it.
                    this.Escape((char)highFirst);
                    this.sb.Append('[');
                    for (; k < pieces.Count && pieces[k].HighFirst == highFirst && pieces[k].HighLast == highFirst; k++)
                    {
                        this.Ranges([pieces[k].LowFirst, pieces[k].LowLast]);
                    }

                    this.sb.Append(']');
                    continue;
                }

                this.sb.Append('[');
                this.Ranges([highFirst, highLast]);
                this.sb.Append("][");
                this.Ranges([lowFirst, lowLast]);
                this.sb.Append(']');
                k++;
            }

            return count;
        }
    }
}