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
/// <item>A backreference matches the empty string when its group has not taken part (see <see cref="Emitter"/>).</item>
/// </list>
/// </para>
/// </remarks>
internal static class EcmaRegexTranslator
{
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
    /// In ECMA-262 a backreference matches the text its group captured when the group last took part, and the empty
    /// string when it has not taken part. .NET fails a backreference to a group that has not taken part. So a
    /// backreference to group 1 is written <c>(?(1)\1)</c>, which matches nothing when the group is unset.
    /// </remarks>
    private sealed class Emitter
    {
        private const string WordClass = "[a-zA-Z0-9_]";
        private const string FoldWordClass = @"[a-zA-Z0-9_\u017F\u212A]";
        private const string LineTerminatorClass = @"[\n\r\u2028\u2029]";
        private const string HighSurrogates = @"[\uD800-\uDBFF]";
        private const string LowSurrogates = @"[\uDC00-\uDFFF]";

        /// <summary>Holds between the two halves of a character beyond the Basic Multilingual Plane.</summary>
        private const string InsideSurrogatePair = @"(?<=[\uD800-\uDBFF])[\uDC00-\uDFFF]";

        /// <summary>More alternatives of surrogate pairs than this stand behind one test for a surrogate.</summary>
        private const int ManyAlternatives = 2;

        private static readonly int[] SurrogateRange = [0xD800, 0xDFFF];
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
        private readonly EcmaRegexNode root;
        private readonly EcmaRegexNode?[] groupNodes;

        /// <summary>For each group, whether some backreference reads it.</summary>
        private readonly bool[] referenced;

        private readonly StringBuilder sb = new();

        public Emitter(EcmaRegexParser parser, EcmaRegexNode root)
        {
            this.unicode = parser.Unicode;
            this.hasBackreferences = parser.HasBackreferences;
            int n = parser.GroupCount + 1;
            this.groupNodes = new EcmaRegexNode?[n];
            this.referenced = new bool[n];
            this.root = root;
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
                    return this.sb.ToString();
                }
            }

            this.Node(this.root, Context.Last, null);
            return this.sb.ToString();
        }

        private static bool IsSurrogate(int c) => c >= 0xD800 && c <= 0xDFFF;

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
                    this.referenced[g] = true;
                }
            }

            foreach (EcmaRegexNode sub in n.Subs)
            {
                this.Survey(sub);
            }
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
                    this.Node(n.Subs[0], n.Behind ? Context.Backward : Context.None, null);
                    this.sb.Append(')');
                    break;
                case EcmaRegexNodeKind.Backref:
                    this.Backreference(n);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>Writes alternatives.</summary>
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

        /// <summary>Writes a repetition.</summary>
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

            this.Atom(body, inner, null);
            this.Quantifier(min, max, lazy);
        }

        /// <summary>
        /// Writes a backreference, which in ECMA-262 matches the text the group captured when the group has taken
        /// part and the empty string when it has not. A name can belong to several groups in separate alternatives,
        /// of which at most one has taken part, so the tests of each follow one another.
        /// </summary>
        private void Backreference(EcmaRegexNode n)
        {
            foreach (int g in n.Groups)
            {
                string number = g.ToString(CultureInfo.InvariantCulture);
                this.sb.Append("(?(").Append(number).Append(')');
                if (n.Fold && this.CapturesCased(g))
                {
                    this.sb.Append(@"(?i:\").Append(number).Append(')');
                }
                else
                {
                    this.sb.Append('\\').Append(number);
                }

                this.sb.Append(')');
            }
        }

        /// <summary>Whether the text a group captures can hold a character that has a case variant.</summary>
        private bool CapturesCased(int g)
        {
            EcmaRegexNode? group = this.groupNodes[g];
            return group is not null && EcmaRegexUnicode.Intersects(this.CapturedSet(group, 0), EcmaRegexUnicode.Cased(this.unicode));
        }

        /// <summary>Every character the text a node matches can hold.</summary>
        private int[] CapturedSet(EcmaRegexNode n, int depth)
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
                    if (depth > EcmaRegexParser.MaxNesting)
                    {
                        return [0, EcmaRegexUnicode.Max];
                    }

                    int[] set = [];
                    foreach (int g in n.Groups)
                    {
                        if (this.groupNodes[g] is EcmaRegexNode target)
                        {
                            int[] captured = this.CapturedSet(target, depth + 1);
                            set = EcmaRegexUnicode.Union(set, n.Fold ? EcmaRegexUnicode.FoldClosure(captured, this.unicode) : captured);
                        }
                    }

                    return set;
                }

                default:
                {
                    int[] set = [];
                    foreach (EcmaRegexNode sub in n.Subs)
                    {
                        set = EcmaRegexUnicode.Union(set, this.CapturedSet(sub, depth));
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
                // No character: an assertion that always fails.
                this.sb.Append("(?!)");
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