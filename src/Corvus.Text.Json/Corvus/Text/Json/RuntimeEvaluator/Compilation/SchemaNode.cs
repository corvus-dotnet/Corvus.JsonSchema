// <copyright file="SchemaNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// Bit mask of JSON types accepted by <c>type</c>.
/// </summary>
[Flags]
internal enum TypeMask : byte
{
    None = 0,
    Null = 1,
    Boolean = 2,
    Object = 4,
    Array = 8,
    Number = 16,
    String = 32,
    Integer = 64,
}

/// <summary>
/// Well-known string formats.
/// </summary>
internal enum FormatKind : byte
{
    None,
    Unknown,
    Date,
    DateTime,
    Time,
    Duration,
    Email,
    IdnEmail,
    Hostname,
    IdnHostname,
    Ipv4,
    Ipv6,
    Uri,
    UriReference,
    Iri,
    IriReference,
    Uuid,
    UriTemplate,
    JsonPointer,
    RelativeJsonPointer,
    Regex,

    // Numeric formats (Corvus extensions) follow; see IsNumeric. Keep Byte first.
    Byte,
    UInt16,
    UInt32,
    UInt64,
    UInt128,
    SByte,
    Int16,
    Int32,
    Int64,
    Int128,
    Half,
    Single,
    Double,
    Decimal,
}

/// <summary>Helpers over <see cref="FormatKind"/>.</summary>
internal static class FormatKinds
{
    /// <summary>Gets a value indicating whether the format applies to numbers rather than strings.</summary>
    public static bool IsNumeric(FormatKind kind) => kind >= FormatKind.Byte;
}

/// <summary>
/// Content keyword handling.
/// </summary>
internal enum ContentKind : byte
{
    None,
    Base64,
    Json,
    Base64Json,
}

/// <summary>
/// A normalized decimal number constant.
/// </summary>
internal sealed class NumberValue
{
    public NumberValue(ReadOnlySpan<byte> raw)
    {
#if STJ
        // The generator build only carries the text into the image; the normalized form is rebuilt on load.
        this.Integral = [];
        this.Fractional = [];
#else
        JsonElementHelpers.ParseNumber(raw, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        this.IsNegative = isNegative;
        this.Integral = integral.ToArray();
        this.Fractional = fractional.ToArray();
        this.Exponent = exponent;
#endif
        this.Text = System.Text.Encoding.UTF8.GetString(raw);
        if (raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0 && raw.Length <= 18 && long.TryParse(this.Text, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out long l))
        {
            this.AsLong = l;
        }
    }

    public bool IsNegative { get; }

    /// <summary>When the constant is a plain integer that fits in a long, its value; otherwise null.</summary>
    public long? AsLong { get; private set; }

    public byte[] Integral { get; }

    public byte[] Fractional { get; }

    public int Exponent { get; }

    public string Text { get; }

#if !STJ
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        // Returns the sign of (instance - this).
        return JsonElementHelpers.CompareNormalizedJsonNumbers(isNegative, integral, fractional, exponent, this.IsNegative, this.Integral, this.Fractional, this.Exponent);
    }
#endif
}

/// <summary>
/// A normalized <c>multipleOf</c> divisor.
/// </summary>
internal sealed class DivisorValue
{
    public DivisorValue(ReadOnlySpan<byte> raw)
    {
        this.Text = System.Text.Encoding.UTF8.GetString(raw);
#if STJ
        // The generator build only carries the text into the image; the normalized form is rebuilt on load.
        return;
#else
        JsonElementHelpers.ParseNumber(raw, out _, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        this.Exponent = exponent;
        Span<byte> digits = stackalloc byte[integral.Length + fractional.Length];
        integral.CopyTo(digits);
        fractional.CopyTo(digits[integral.Length..]);
        if (digits.Length == 0)
        {
            this.Small = 0;
            return;
        }

        if (digits.Length <= 19 && ulong.TryParse(System.Text.Encoding.ASCII.GetString(digits), NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong small))
        {
            this.Small = small;
        }
        else
        {
            this.IsBig = true;
            this.Big = BigInteger.Parse(System.Text.Encoding.ASCII.GetString(digits));
        }
#endif
    }

    public bool IsBig { get; }

    public ulong Small { get; }

    public BigInteger Big { get; }

    public int Exponent { get; }

    public string Text { get; }

#if !STJ
    public bool IsMultiple(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        return this.IsBig
            ? JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, this.Big, this.Exponent)
            : JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, this.Small, this.Exponent);
    }
#endif
}

/// <summary>
/// A compiled regular expression with fast paths for common shapes.
/// </summary>
internal sealed class PatternMatcher
{
#if STJ
    private static readonly bool ForceInterpretedRegex = false;
#else
    private static readonly bool ForceInterpretedRegex = Environment.GetEnvironmentVariable("CORVUS_RT_REGEX_INTERPRETED") == "1";
#endif
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Pattern, RegexOptions Options, TimeSpan Timeout), Regex> RegexCache = new();

    private readonly Regex? regex;
    private readonly byte[]? prefix;
    private readonly int min;
    private readonly int max;
    private readonly ClassAtom[]? atoms;
    private readonly PatternSequence[]? sequences;
    private readonly SeparatedList? separated;
    private readonly byte[][]? literals;
    private readonly LiteralAnchor[]? anchors;
    private readonly Kind kind;

    private PatternMatcher(Kind kind, string source, Regex? regex, byte[]? prefix, int min, int max, ClassAtom[]? atoms = null, byte[][]? literals = null, LiteralAnchor[]? anchors = null, PatternSequence[]? sequences = null, SeparatedList? separated = null)
    {
        this.anchors = anchors;
        this.sequences = sequences;
        this.separated = separated;
        this.kind = kind;
        this.Source = source;
        this.regex = regex;
        this.prefix = prefix;
        this.min = min;
        this.max = max;
        this.atoms = atoms;
        this.literals = literals;
    }

    private enum LiteralAnchor : byte
    {
        None,
        Start,
        End,
        Both,
    }

    private enum Kind : byte
    {
        Noop,
        NonEmpty,
        Prefix,
        Range,
        Sequences,
        SeparatedList,
        ExcludedClassWithWord,
        Literals,
        AnchoredLiterals,
        Regex,
    }

    public string Source { get; }

    /// <summary>Gets a value indicating whether matching uses a <see cref="Regex"/> (as opposed to a prefix, range or trivial test).</summary>
    public bool UsesRegex => this.kind == Kind.Regex;

    /// <summary>
    /// Creates a matcher for a pattern, consulting <see cref="JsonSchemaEvaluatorOptions.RegexProvider"/> (with the
    /// given pattern-table index) before constructing a regular expression for patterns that need one.
    /// </summary>
    public static PatternMatcher Create(string ecmaPattern, JsonSchemaEvaluatorOptions options, int patternIndex = -1)
    {
        switch (ecmaPattern)
        {
            case ".*":
            case "^.*$":
            case "^(.*)$":
            case "(.*)":
            case "[\\s\\S]*":
            case "^[\\s\\S]*$":
                return new PatternMatcher(Kind.Noop, ecmaPattern, null, null, 0, 0);
            case ".+":
            case "^.+$":
            case "^(.+)$":
            case "(.+)":
            case ".":
                return new PatternMatcher(Kind.NonEmpty, ecmaPattern, null, null, 0, 0);
        }

        if (TryParsePrefix(ecmaPattern, out string? prefix))
        {
            return new PatternMatcher(Kind.Prefix, ecmaPattern, null, System.Text.Encoding.UTF8.GetBytes(prefix!), 0, 0);
        }

        if (TryParseRange(ecmaPattern, out int min, out int max))
        {
            return new PatternMatcher(Kind.Range, ecmaPattern, null, null, min, max);
        }

        if (TryParseLiterals(ecmaPattern, out byte[][]? literals))
        {
            return new PatternMatcher(Kind.Literals, ecmaPattern, null, null, 0, 0, literals: literals);
        }

        if (TryParseAnchoredLiterals(ecmaPattern, out literals, out LiteralAnchor[]? anchors))
        {
            return new PatternMatcher(Kind.AnchoredLiterals, ecmaPattern, null, null, 0, 0, literals: literals, anchors: anchors);
        }

        if (TryParseExcludedClassWithWord(ecmaPattern, out ClassAtom[]? excluded))
        {
            return new PatternMatcher(Kind.ExcludedClassWithWord, ecmaPattern, null, null, 0, 0, atoms: excluded);
        }

        if (TryParseSeparatedList(ecmaPattern, out SeparatedList? separated))
        {
            return new PatternMatcher(Kind.SeparatedList, ecmaPattern, null, null, 0, 0, separated: separated);
        }

        if (TryParseSequences(ecmaPattern, out PatternSequence[]? sequences))
        {
            return new PatternMatcher(Kind.Sequences, ecmaPattern, null, null, 0, 0, sequences: sequences);
        }

        if (options.RegexProvider is JsonSchemaRegexProvider provider && provider(patternIndex, ecmaPattern) is Regex provided)
        {
            return new PatternMatcher(Kind.Regex, ecmaPattern, provided, null, 0, 0);
        }

        RegexOptions regexOptions = RegexOptions.CultureInvariant;
        if (options.CompileRegularExpressions && !ForceInterpretedRegex)
        {
            regexOptions |= RegexOptions.Compiled;
        }

        // Regex instances are immutable and thread-safe, so identical patterns are shared process-wide:
        // repeated patterns (metaschemas, many evaluators of similar schemas) compile once.
        var key = (ecmaPattern, regexOptions, options.RegexMatchTimeout);
        Regex regex = RegexCache.GetOrAdd(key, static k =>
        {
            string translated = Corvus.Text.Json.CodeGeneration.EcmaRegexTranslator.TranslateOrFallback(k.Pattern);
            return new Regex(translated, k.Options, k.Timeout);
        });

        return new PatternMatcher(Kind.Regex, ecmaPattern, regex, null, 0, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsMatch(ReadOnlySpan<byte> utf8Value)
    {
        return this.kind switch
        {
            Kind.Noop => true,
            Kind.NonEmpty => utf8Value.Length > 0,
            Kind.Prefix => utf8Value.StartsWith(this.prefix),
            Kind.Literals => MatchesLiterals(this.literals!, utf8Value),
            Kind.AnchoredLiterals => MatchesAnchoredLiterals(this.literals!, this.anchors!, utf8Value),
            Kind.Sequences => MatchesSequences(this.sequences!, utf8Value),
            Kind.SeparatedList => MatchesSeparatedList(this.separated!, utf8Value),
            Kind.ExcludedClassWithWord => MatchesExcludedClassWithWord(this.atoms!, utf8Value),
#if STJ
            Kind.Range => !ContainsLineTerminator(utf8Value) && RuneCount(utf8Value) >= this.min && RuneCount(utf8Value) <= this.max,
            _ => this.regex!.IsMatch(System.Text.Encoding.UTF8.GetString(utf8Value)),
#else
            Kind.Range => !ContainsLineTerminator(utf8Value) && JsonSchemaEvaluation.MatchRangeRegularExpression(utf8Value, this.min, this.max),
            _ => JsonSchemaEvaluation.MatchRegularExpression(utf8Value, this.regex!),
#endif
        };
    }

#if STJ
    private static int RuneCount(ReadOnlySpan<byte> utf8)
    {
        int count = 0;
        foreach (byte b in utf8)
        {
            if ((b & 0xC0) != 0x80)
            {
                count++;
            }
        }

        return count;
    }
#endif

    private static bool TryParsePrefix(string pattern, out string? prefix)
    {
        // ^literal or ^literal.*  where literal is [A-Za-z0-9_/@-]+
        prefix = null;
        if (pattern.Length < 2 || pattern[0] != '^')
        {
            return false;
        }

        int end = pattern.Length;
        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            end -= 2;
        }

        if (end <= 1)
        {
            return false;
        }

        for (int i = 1; i < end; i++)
        {
            char c = pattern[i];
            if (!(AsciiChar.IsLetterOrDigit(c) || c == '_' || c == '/' || c == '@' || c == '-'))
            {
                return false;
            }
        }

        prefix = pattern[1..end];
        return true;
    }

    /// <summary>The non-ASCII characters a class admits, in the three parts ECMA-262's classes distinguish: the line separators U+2028 and U+2029, the other non-ASCII members of <c>\s</c>, and everything else.</summary>
    [Flags]
    internal enum NonAscii : byte
    {
        None = 0,
        LineSeparators = 1,
        Spaces = 2,
        Others = 4,
        All = 7,
    }

    /// <summary>
    /// One class (or literal, or <c>.</c>) with its quantifier: an ASCII bitset plus which non-ASCII characters it
    /// admits. ECMA-262 classes are ASCII (<c>\w</c> is <c>[a-zA-Z0-9_]</c>), so a non-ASCII character can only be
    /// admitted wholesale by a negated class, by <c>\W</c>, <c>\D</c> or <c>\S</c>, as one of the <c>\s</c> spaces,
    /// or by <c>.</c>.
    /// </summary>
    internal readonly struct ClassAtom : IEquatable<ClassAtom>
    {
        public ClassAtom(ulong bits0, ulong bits1, int min, int max, NonAscii extra = NonAscii.None, byte[]? rune = null)
        {
            this.Bits0 = bits0;
            this.Bits1 = bits1;
            this.Min = min;
            this.Max = max;
            this.Extra = extra;
            this.Rune = rune;
        }

        /// <summary>A single non-ASCII character the atom admits (its UTF-8 bytes), for a literal outside ASCII.</summary>
        public byte[]? Rune { get; }

        public ulong Bits0 { get; }

        public ulong Bits1 { get; }

        public int Min { get; }

        /// <summary>The maximum count, or -1 for unbounded.</summary>
        public int Max { get; }

        public NonAscii Extra { get; }

        /// <summary>Whether the atom consumes a fixed number of characters.</summary>
        public bool Fixed => this.Min == this.Max;

        /// <summary>The atom for <c>.</c>: everything but the line terminators.</summary>
        public static ClassAtom Dot(int min, int max) => new(~((1UL << '\n') | (1UL << '\r')), ulong.MaxValue, min, max, NonAscii.Spaces | NonAscii.Others);

        /// <summary>Classifies a non-ASCII character (its UTF-8 bytes) into the parts of <see cref="NonAscii"/>.</summary>
        public static NonAscii Classify(ReadOnlySpan<byte> rune)
        {
            // U+2028/U+2029 are E2 80 A8/A9. The other ECMA-262 \s members: U+00A0 (C2 A0), U+1680 (E1 9A 80),
            // U+2000-U+200A (E2 80 80-8A), U+202F (E2 80 AF), U+205F (E2 81 9F), U+3000 (E3 80 80), U+FEFF (EF BB BF).
            if (rune.Length == 2)
            {
                return rune[0] == 0xC2 && rune[1] == 0xA0 ? NonAscii.Spaces : NonAscii.Others;
            }

            if (rune.Length == 3)
            {
                byte b0 = rune[0];
                byte b1 = rune[1];
                byte b2 = rune[2];
                if (b0 == 0xE2 && b1 == 0x80)
                {
                    if (b2 == 0xA8 || b2 == 0xA9)
                    {
                        return NonAscii.LineSeparators;
                    }

                    return (b2 >= 0x80 && b2 <= 0x8A) || b2 == 0xAF ? NonAscii.Spaces : NonAscii.Others;
                }

                if ((b0 == 0xE1 && b1 == 0x9A && b2 == 0x80) || (b0 == 0xE2 && b1 == 0x81 && b2 == 0x9F) || (b0 == 0xE3 && b1 == 0x80 && b2 == 0x80) || (b0 == 0xEF && b1 == 0xBB && b2 == 0xBF))
                {
                    return NonAscii.Spaces;
                }
            }

            return NonAscii.Others;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(byte b)
        {
            return b < 64 ? (this.Bits0 & (1UL << b)) != 0 : b < 128 && (this.Bits1 & (1UL << (b - 64))) != 0;
        }

        /// <summary>Whether no character is in both atoms.</summary>
        public bool Disjoint(in ClassAtom other)
        {
            if ((this.Bits0 & other.Bits0) != 0 || (this.Bits1 & other.Bits1) != 0 || (this.Extra & other.Extra) != 0)
            {
                return false;
            }

            return (this.Rune is null || !other.Admits(this.Rune)) && (other.Rune is null || !this.Admits(other.Rune));
        }

        /// <summary>Whether two atoms admit the same characters, whatever their counts.</summary>
        public bool SameClass(in ClassAtom other)
        {
            return this.Bits0 == other.Bits0 && this.Bits1 == other.Bits1 && this.Extra == other.Extra
                && (this.Rune is null ? other.Rune is null : other.Rune is not null && this.Rune.AsSpan().SequenceEqual(other.Rune));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryConsume(ReadOnlySpan<byte> value, ref int pos)
        {
            if (pos >= value.Length)
            {
                return false;
            }

            byte b = value[pos];
            if (b < 0x80)
            {
                if (!this.Contains(b))
                {
                    return false;
                }

                pos++;
                return true;
            }

            return this.TryConsumeNonAscii(value, ref pos, b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool TryConsumeNonAscii(ReadOnlySpan<byte> value, ref int pos, byte lead)
        {
            // The text is valid UTF-8, so the lead byte gives the length.
            int length = lead >= 0xF0 ? 4 : lead >= 0xE0 ? 3 : 2;
            if (pos + length > value.Length || !this.Admits(value.Slice(pos, length)))
            {
                return false;
            }

            pos += length;
            return true;
        }

        /// <summary>Consumes one character ending at <paramref name="end"/> (exclusive), moving it back.</summary>
        public bool TryConsumeBackward(ReadOnlySpan<byte> value, ref int end)
        {
            if (end <= 0)
            {
                return false;
            }

            int start = end - 1;
            byte b = value[start];
            if (b < 0x80)
            {
                if (!this.Contains(b))
                {
                    return false;
                }

                end = start;
                return true;
            }

            while (start > 0 && (value[start] & 0xC0) == 0x80)
            {
                start--;
            }

            if (!this.Admits(value.Slice(start, end - start)))
            {
                return false;
            }

            end = start;
            return true;
        }

        public bool Equals(ClassAtom other)
        {
            return this.SameClass(other) && this.Min == other.Min && this.Max == other.Max;
        }

        public override bool Equals(object? obj) => obj is ClassAtom other && this.Equals(other);

        public override int GetHashCode() => HashCode.Combine(this.Bits0, this.Bits1, this.Min, this.Max, this.Extra);

        private bool Admits(ReadOnlySpan<byte> rune)
        {
            if (this.Rune is byte[] exact && rune.SequenceEqual(exact))
            {
                return true;
            }

            NonAscii extra = this.Extra;
            if (extra == NonAscii.All)
            {
                return true;
            }

            return extra != NonAscii.None && (extra & Classify(rune)) != 0;
        }
    }

    /// <summary>Where a sequence is anchored: the alternatives of <c>^a|b|c$</c> anchor only the first at the start and the last at the end.</summary>
    internal enum SequenceAnchor : byte
    {
        None,
        Start,
        End,
        Both,
    }

    /// <summary>
    /// A sequence of atoms with an anchoring. Every variable atom but the last is followed by a fixed atom disjoint
    /// from it, so consuming it greedily is exact; the atoms after the last variable one are fixed and are matched
    /// from the end. No backtracking is ever needed.
    /// </summary>
    internal sealed class PatternSequence
    {
        public PatternSequence(ClassAtom[] atoms, SequenceAnchor anchor)
        {
            this.Atoms = atoms;
            this.Anchor = anchor;
            this.LastVariable = -1;
            int minimum = 0;
            for (int i = 0; i < atoms.Length; i++)
            {
                if (!atoms[i].Fixed)
                {
                    this.LastVariable = i;
                }

                minimum += atoms[i].Min;
            }

            this.MinimumLength = minimum;
            if (atoms.Length > 0 && atoms[0].Min >= 1 && atoms[0].Extra == NonAscii.None && atoms[0].Rune is null)
            {
                var bytes = new List<byte>(4);
                for (int b = 0; b < 128 && bytes.Count <= 3; b++)
                {
                    if (atoms[0].Contains((byte)b))
                    {
                        bytes.Add((byte)b);
                    }
                }

                if (bytes.Count <= 3)
                {
                    this.StartBytes = [.. bytes];
                }
            }
        }

        /// <summary>The first atom's characters when they are at most three ASCII bytes, for a vectorised search.</summary>
        public byte[]? StartBytes { get; }

        public ClassAtom[] Atoms { get; }

        public SequenceAnchor Anchor { get; }

        /// <summary>The index of the last variable atom, or -1.</summary>
        public int LastVariable { get; }

        /// <summary>The fewest characters (so at least as many bytes) a match takes.</summary>
        public int MinimumLength { get; }

        /// <summary>Whether a match could start at <paramref name="start"/>: the first atom admits the character there (or needs none).</summary>
        public bool CanStartAt(ReadOnlySpan<byte> value, int start)
        {
            if (this.Atoms.Length == 0 || this.Atoms[0].Min == 0)
            {
                return true;
            }

            if (start >= value.Length)
            {
                return false;
            }

            ref readonly ClassAtom first = ref this.Atoms[0];
            byte b = value[start];
            return b < 0x80 ? first.Contains(b) : first.Extra != NonAscii.None || first.Rune is not null;
        }
    }

    /// <summary>
    /// <c>^item(separator item)*$</c> (or <c>+</c>, or <c>^(item separator)*item$</c>): the item's last atom may be
    /// variable, the separator is fixed and starts with a character the item's variable atom does not admit, so the
    /// list splits exactly where the greedy item stops.
    /// </summary>
    internal sealed class SeparatedList
    {
        public SeparatedList(ClassAtom[]? first, ClassAtom[] repeated, ClassAtom[]? final, ClassAtom[] separator, int minRepeats)
        {
            this.First = first;
            this.Repeated = repeated;
            this.Final = final is null ? null : new PatternSequence(final, SequenceAnchor.Both);
            this.Separator = separator;
            this.MinRepeats = minRepeats;
        }

        /// <summary>The first item when it differs from the repeated one (<c>^first(separator item)*$</c>).</summary>
        public ClassAtom[]? First { get; }

        public ClassAtom[] Repeated { get; }

        /// <summary>The last item when it differs from the repeated one (<c>^(item separator)*final$</c>), matched over the remainder.</summary>
        public PatternSequence? Final { get; }

        public ClassAtom[] Separator { get; }

        public int MinRepeats { get; }
    }

    /// <summary>ECMA-262 <c>.</c> excludes the line terminators U+000A, U+000D, U+2028 and U+2029.</summary>
    private static bool MatchesSequences(PatternSequence[] sequences, ReadOnlySpan<byte> value)
    {
        if (sequences.Length == 1 && sequences[0].Anchor == SequenceAnchor.Both)
        {
            return MatchWhole(sequences[0], value);
        }

        for (int i = 0; i < sequences.Length; i++)
        {
            if (MatchesSequence(sequences[i], value))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesSequence(PatternSequence sequence, ReadOnlySpan<byte> value)
    {
        switch (sequence.Anchor)
        {
            case SequenceAnchor.Both:
                return MatchWhole(sequence, value);
            case SequenceAnchor.Start:
                return MatchPrefix(sequence, value);
            case SequenceAnchor.End:
                return Search(sequence, value, whole: true);
            default:
                return Search(sequence, value, whole: false);
        }
    }

    /// <summary>A match starting at any character boundary: positions the first atom rejects, or too near the end, are skipped without an attempt.</summary>
    private static bool Search(PatternSequence sequence, ReadOnlySpan<byte> value, bool whole)
    {
        int minimum = sequence.MinimumLength;
        int start = 0;
        if (sequence.StartBytes is byte[] sb)
        {
            while (value.Length - start >= minimum)
            {
                ReadOnlySpan<byte> rest = value[start..];
                int at = sb.Length == 1 ? rest.IndexOf(sb[0]) : sb.Length == 2 ? rest.IndexOfAny(sb[0], sb[1]) : sb.Length == 3 ? rest.IndexOfAny(sb[0], sb[1], sb[2]) : -1;
                if (at < 0)
                {
                    return false;
                }

                start += at;
                if (value.Length - start < minimum)
                {
                    return false;
                }

                if (whole ? MatchWhole(sequence, value[start..]) : MatchPrefix(sequence, value[start..]))
                {
                    return true;
                }

                start++;
            }

            return false;
        }

        while (value.Length - start >= minimum)
        {
            if (sequence.CanStartAt(value, start) && (whole ? MatchWhole(sequence, value[start..]) : MatchPrefix(sequence, value[start..])))
            {
                return true;
            }

            if (start >= value.Length)
            {
                return false;
            }

            start++;
            while (start < value.Length && (value[start] & 0xC0) == 0x80)
            {
                start++;
            }
        }

        return false;
    }

    /// <summary>Consumes the atoms before the last variable one: fixed atoms their count, variable ones greedily.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ConsumeHead(ClassAtom[] atoms, int upTo, ReadOnlySpan<byte> value, ref int pos)
    {
        for (int a = 0; a < upTo; a++)
        {
            ref readonly ClassAtom atom = ref atoms[a];
            if (atom.Fixed)
            {
                for (int n = 0; n < atom.Min; n++)
                {
                    if (!atom.TryConsume(value, ref pos))
                    {
                        return false;
                    }
                }
            }
            else
            {
                int count = 0;
                while ((atom.Max < 0 || count < atom.Max) && atom.TryConsume(value, ref pos))
                {
                    count++;
                }

                if (count < atom.Min)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The whole value, anchored at both ends.</summary>
    private static bool MatchWhole(PatternSequence sequence, ReadOnlySpan<byte> value)
    {
        ClassAtom[] atoms = sequence.Atoms;
        int last = sequence.LastVariable;
        int pos = 0;
        if (!ConsumeHead(atoms, last < 0 ? atoms.Length : last, value, ref pos))
        {
            return false;
        }

        if (last < 0)
        {
            return pos == value.Length;
        }

        int end = value.Length;
        for (int a = atoms.Length - 1; a > last; a--)
        {
            ref readonly ClassAtom atom = ref atoms[a];
            for (int n = 0; n < atom.Min; n++)
            {
                if (!atom.TryConsumeBackward(value, ref end))
                {
                    return false;
                }
            }
        }

        if (end < pos)
        {
            return false;
        }

        ref readonly ClassAtom variable = ref atoms[last];
        int count = 0;
        while (pos < end)
        {
            if (!variable.TryConsume(value, ref pos))
            {
                return false;
            }

            count++;
        }

        return count >= variable.Min && (variable.Max < 0 || count <= variable.Max);
    }

    /// <summary>Anchored at the start only: the rest of the value is free after the last atom.</summary>
    private static bool MatchPrefix(PatternSequence sequence, ReadOnlySpan<byte> value)
    {
        ClassAtom[] atoms = sequence.Atoms;
        int last = sequence.LastVariable;
        int pos = 0;
        if (!ConsumeHead(atoms, last < 0 ? atoms.Length : last, value, ref pos))
        {
            return false;
        }

        if (last < 0)
        {
            return true;
        }

        ref readonly ClassAtom variable = ref atoms[last];
        int count = 0;
        int p = pos;
        while ((variable.Max < 0 || count < variable.Max) && variable.TryConsume(value, ref p))
        {
            count++;
        }

        if (count < variable.Min)
        {
            return false;
        }

        if (last == atoms.Length - 1)
        {
            return true;
        }

        // The fixed tail may need the variable atom to give characters back; try each extent from the greedy one down.
        while (true)
        {
            if (TailMatchesAt(atoms, last + 1, value, p))
            {
                return true;
            }

            if (count == variable.Min)
            {
                return false;
            }

            p--;
            while (p > pos && (value[p] & 0xC0) == 0x80)
            {
                p--;
            }

            count--;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TailMatchesAt(ClassAtom[] atoms, int from, ReadOnlySpan<byte> value, int pos)
    {
        for (int a = from; a < atoms.Length; a++)
        {
            ref readonly ClassAtom atom = ref atoms[a];
            for (int n = 0; n < atom.Min; n++)
            {
                if (!atom.TryConsume(value, ref pos))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool MatchesSeparatedList(SeparatedList list, ReadOnlySpan<byte> value)
    {
        ClassAtom[] separator = list.Separator;
        int pos = 0;
        int repeats = 0;
        while (true)
        {
            int segment = pos;
            ClassAtom[] item = repeats == 0 && list.First is ClassAtom[] first ? first : list.Repeated;
            bool consumed = ConsumeHead(item, item.Length, value, ref pos);
            if (!consumed || pos == value.Length || !TailMatchesAt(separator, 0, value, pos))
            {
                // The remainder is the final item, or the list ends here.
                if (list.Final is PatternSequence final)
                {
                    return repeats >= list.MinRepeats && MatchWhole(final, value[segment..]);
                }

                return consumed && pos == value.Length && repeats >= list.MinRepeats;
            }

            for (int a = 0; a < separator.Length; a++)
            {
                for (int n = 0; n < separator[a].Min; n++)
                {
                    separator[a].TryConsume(value, ref pos);
                }
            }

            repeats++;
        }
    }

    /// <summary>
    /// Splits a pattern on its top-level <c>|</c> (outside classes, groups and escapes).
    /// </summary>
    private static List<string>? SplitTopLevel(string pattern)
    {
        var parts = new List<string>();
        int depth = 0;
        bool inClass = false;
        int start = 0;
        for (int i = 0; i < pattern.Length; i++)
        {
            char c = pattern[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (inClass)
            {
                if (c == ']')
                {
                    inClass = false;
                }

                continue;
            }

            if (c == '[')
            {
                inClass = true;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth < 0)
                {
                    return null;
                }
            }
            else if (c == '|' && depth == 0)
            {
                parts.Add(pattern.Substring(start, i - start));
                start = i + 1;
            }
        }

        if (depth != 0 || inClass)
        {
            return null;
        }

        parts.Add(pattern.Substring(start));
        return parts;
    }

    /// <summary>Removes a plain or non-capturing group that encloses the whole text, repeatedly.</summary>
    private static string StripEnclosingGroup(string text)
    {
        while (text.Length >= 2 && text[0] == '(' && text[text.Length - 1] != '\\' && MatchingParen(text, 0) == text.Length - 1)
        {
            if (text.Length >= 3 && text[1] == '?')
            {
                if (text.Length >= 4 && text[2] == ':')
                {
                    text = text.Substring(3, text.Length - 4);
                    continue;
                }

                return text;
            }

            text = text.Substring(1, text.Length - 2);
        }

        return text;
    }

    /// <summary>The index of the parenthesis closing the one at <paramref name="open"/>, or -1.</summary>
    private static int MatchingParen(string text, int open)
    {
        int depth = 0;
        bool inClass = false;
        for (int i = open; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\')
            {
                i++;
            }
            else if (inClass)
            {
                inClass = c != ']';
            }
            else if (c == '[')
            {
                inClass = true;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static bool EndsWithUnescapedDollar(string text)
    {
        if (text.Length == 0 || text[text.Length - 1] != '$')
        {
            return false;
        }

        int backslashes = 0;
        for (int i = text.Length - 2; i >= 0 && text[i] == '\\'; i--)
        {
            backslashes++;
        }

        return backslashes % 2 == 0;
    }

    /// <summary>Whether the text has an unescaped <c>^</c> or <c>$</c> outside a class (which the sequence form cannot express).</summary>
    private static bool HasInnerAnchor(string text)
    {
        bool inClass = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\')
            {
                i++;
            }
            else if (inClass)
            {
                inClass = c != ']';
            }
            else if (c == '[')
            {
                inClass = true;
            }
            else if (c == '^' || c == '$')
            {
                return true;
            }
        }

        return false;
    }

    private static string StripFreeDotStar(string inner, bool start, bool end)
    {
        // A leading .* without a start anchor, or a trailing one without an end anchor, changes nothing.
        if (!end)
        {
            foreach (string tail in new[] { "(?:.*)", "(.*)", ".*" })
            {
                if (inner.EndsWith(tail, StringComparison.Ordinal))
                {
                    inner = inner.Substring(0, inner.Length - tail.Length);
                    break;
                }
            }
        }

        if (!start)
        {
            foreach (string head in new[] { "(?:.*)", "(.*)", ".*" })
            {
                if (inner.StartsWith(head, StringComparison.Ordinal))
                {
                    inner = inner.Substring(head.Length);
                    break;
                }
            }
        }

        return inner;
    }

    /// <summary>
    /// Parses a pattern as alternatives of sequences, each anchored where its own <c>^</c> and <c>$</c> say (so in
    /// <c>^a|b|c$</c> only the first is anchored at the start and the last at the end), with groups of alternatives
    /// and optional groups flattened into whole alternatives. A sequence qualifies when every variable atom but the
    /// last is followed by a fixed atom disjoint from it.
    /// </summary>
    private static bool TryParseSequences(string pattern, [NotNullWhen(true)] out PatternSequence[]? sequences)
    {
        sequences = null;
        List<string>? parts = SplitTopLevel(pattern);
        if (parts is null)
        {
            return false;
        }

        var result = new List<PatternSequence>();
        foreach (string rawPart in parts)
        {
            string part = StripEnclosingGroup(rawPart);
            bool start = part.Length > 0 && part[0] == '^';
            bool end = EndsWithUnescapedDollar(part) && !(start && part.Length == 1);
            string inner = part.Substring(start ? 1 : 0, part.Length - (start ? 1 : 0) - (end ? 1 : 0));
            inner = StripFreeDotStar(inner, start, end);
            if (HasInnerAnchor(inner))
            {
                return false;
            }

            SequenceAnchor anchor = start && end ? SequenceAnchor.Both : start ? SequenceAnchor.Start : end ? SequenceAnchor.End : SequenceAnchor.None;
            if (inner.Length == 0)
            {
                result.Add(new PatternSequence([], anchor));
                continue;
            }

            int i = 0;
            if (!TryParseAlternation(inner, ref i, inner.Length, depth: 1, out List<List<ClassAtom>>? alternatives) || i != inner.Length)
            {
                return false;
            }

            foreach (List<ClassAtom> atoms in alternatives)
            {
                var sequence = new PatternSequence([.. atoms], anchor);
                if (!IsDeterministic(sequence.Atoms, sequence.LastVariable))
                {
                    return false;
                }

                result.Add(sequence);
                if (result.Count > MaxAlternatives)
                {
                    return false;
                }
            }
        }

        sequences = [.. result];
        return true;
    }

    /// <summary>
    /// Every variable atom before the last one is followed by atoms that admit none of its characters, up to and
    /// including the first that must consume something, so its greedy run ends exactly where the regex's would.
    /// </summary>
    private static bool IsDeterministic(ClassAtom[] atoms, int lastVariable)
    {
        for (int a = 0; a < lastVariable; a++)
        {
            if (atoms[a].Fixed)
            {
                continue;
            }

            for (int j = a + 1; j < atoms.Length; j++)
            {
                if (!atoms[j].Disjoint(atoms[a]))
                {
                    return false;
                }

                if (atoms[j].Min >= 1)
                {
                    break;
                }
            }
        }

        return true;
    }

    /// <summary>Parses <c>^item(separator item)*$</c>, <c>^item(separator item)+$</c> or <c>^(item separator)*item$</c>; see <see cref="SeparatedList"/>.</summary>
    private static bool TryParseSeparatedList(string pattern, [NotNullWhen(true)] out SeparatedList? list)
    {
        list = null;
        if (pattern.Length < 4 || pattern[0] != '^' || !EndsWithUnescapedDollar(pattern))
        {
            return false;
        }

        string body = pattern.Substring(1, pattern.Length - 2);
        int groupStart = -1;
        int groupEnd = -1;
        int depth = 0;
        bool inClass = false;
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\\')
            {
                i++;
            }
            else if (inClass)
            {
                inClass = c != ']';
            }
            else if (c == '[')
            {
                inClass = true;
            }
            else if (c == '(')
            {
                if (depth == 0)
                {
                    groupStart = i;
                }

                depth++;
            }
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && i + 1 < body.Length && (body[i + 1] == '*' || body[i + 1] == '+'))
                {
                    if (groupEnd >= 0)
                    {
                        return false;
                    }

                    groupEnd = i;
                }
            }
        }

        if (groupEnd < 0)
        {
            return false;
        }

        // The quantified group is the one that ends at groupEnd; find its opening parenthesis.
        depth = 0;
        inClass = false;
        for (int i = groupEnd; i >= 0; i--)
        {
            char c = body[i];
            if (i > 0 && body[i - 1] == '\\')
            {
                continue;
            }

            if (c == ')')
            {
                depth++;
            }
            else if (c == '(')
            {
                depth--;
                if (depth == 0)
                {
                    groupStart = i;
                    break;
                }
            }
        }

        int minRepeats = body[groupEnd + 1] == '+' ? 1 : 0;
        string before = body.Substring(0, groupStart);
        string after = body.Substring(groupEnd + 2);
        string group = body.Substring(groupStart + 1, groupEnd - groupStart - 1);
        if (group.StartsWith("?:", StringComparison.Ordinal))
        {
            group = group.Substring(2);
        }
        else if (group.StartsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        if ((before.Length > 0) == (after.Length > 0))
        {
            return false;
        }

        bool itemFirst = before.Length > 0;
        string itemText = itemFirst ? before : after;
        if (itemText.Length >= 2 && itemText[0] == '(' && itemText[itemText.Length - 1] == ')' && SplitTopLevel(itemText.Substring(1, itemText.Length - 2)) is { Count: 1 })
        {
            itemText = itemText.Substring(1, itemText.Length - 2);
            if (itemText.StartsWith("?:", StringComparison.Ordinal))
            {
                itemText = itemText.Substring(2);
            }
            else if (itemText.StartsWith("?", StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (!TryParseSingleSequence(itemText, out ClassAtom[]? item) || !TryParseSingleSequence(group, out ClassAtom[]? groupAtoms) || groupAtoms.Length <= item.Length)
        {
            return false;
        }

        // The group is separator + repeated item (item first) or repeated item + separator (final item last). The
        // repeated item is the part of the group with the same classes as the outer item, else the part from its
        // first (or up to its last) variable atom.
        ClassAtom[] separator;
        ClassAtom[] repeated;
        if (itemFirst)
        {
            int split = SameClasses(groupAtoms, groupAtoms.Length - item.Length, item) ? groupAtoms.Length - item.Length : Array.FindIndex(groupAtoms, a => !a.Fixed);
            if (split <= 0)
            {
                return false;
            }

            separator = groupAtoms.AsSpan(0, split).ToArray();
            repeated = groupAtoms.AsSpan(split).ToArray();
        }
        else
        {
            int split = SameClasses(groupAtoms, 0, item) ? item.Length : Array.FindLastIndex(groupAtoms, a => !a.Fixed) + 1;
            if (split <= 0 || split >= groupAtoms.Length)
            {
                return false;
            }

            repeated = groupAtoms.AsSpan(0, split).ToArray();
            separator = groupAtoms.AsSpan(split).ToArray();
        }

        foreach (ClassAtom atom in separator)
        {
            if (!atom.Fixed || atom.Min < 1)
            {
                return false;
            }
        }

        foreach (ClassAtom[] segment in new[] { item, repeated })
        {
            for (int k = 0; k < segment.Length; k++)
            {
                if (!segment[k].Fixed && (k != segment.Length - 1 || !separator[0].Disjoint(segment[k])))
                {
                    return false;
                }
            }
        }

        bool same = item.Length == repeated.Length && SameClasses(repeated, 0, item);
        for (int k = 0; same && k < item.Length; k++)
        {
            same = item[k].Equals(repeated[k]);
        }

        list = itemFirst
            ? new SeparatedList(same ? null : item, repeated, null, separator, minRepeats)
            : new SeparatedList(null, repeated, same ? null : item, separator, minRepeats);
        return true;
    }

    private static bool SameClasses(ClassAtom[] atoms, int from, ClassAtom[] reference)
    {
        if (from < 0 || from + reference.Length > atoms.Length)
        {
            return false;
        }

        for (int k = 0; k < reference.Length; k++)
        {
            if (!atoms[from + k].SameClass(reference[k]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseSingleSequence(string text, [NotNullWhen(true)] out ClassAtom[]? atoms)
    {
        atoms = null;
        if (text.Length == 0 || HasInnerAnchor(text))
        {
            return false;
        }

        int i = 0;
        if (!TryParseAlternation(text, ref i, text.Length, depth: 1, out List<List<ClassAtom>>? alternatives) || i != text.Length || alternatives.Count != 1)
        {
            return false;
        }

        atoms = [.. alternatives[0]];
        return true;
    }

    private static bool ContainsLineTerminator(ReadOnlySpan<byte> value)
    {
        if (value.IndexOfAny((byte)'\n', (byte)'\r') >= 0)
        {
            return true;
        }

        int i = value.IndexOf((byte)0xE2);
        while (i >= 0 && i + 2 < value.Length)
        {
            if (value[i + 1] == 0x80 && (value[i + 2] == 0xA8 || value[i + 2] == 0xA9))
            {
                return true;
            }

            int next = value[(i + 1)..].IndexOf((byte)0xE2);
            i = next < 0 ? -1 : i + 1 + next;
        }

        return false;
    }

    private static bool MatchesLiterals(byte[][] literals, ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < literals.Length; i++)
        {
            if (value.SequenceEqual(literals[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesAnchoredLiterals(byte[][] literals, LiteralAnchor[] anchors, ReadOnlySpan<byte> value)
    {
        for (int i = 0; i < literals.Length; i++)
        {
            byte[] literal = literals[i];
            bool matched = anchors[i] switch
            {
                LiteralAnchor.Both => value.SequenceEqual(literal),
                LiteralAnchor.Start => value.StartsWith(literal),
                LiteralAnchor.End => value.EndsWith(literal),
                _ => value.IndexOf(literal) >= 0,
            };

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>^(?=[^SET]+$)(?=(.*\w)).+$</c>, optionally <c>^(?=!+[^SET]+$)...</c>: after any leading <c>!</c>s (when the
    /// form allows them) no byte may be in SET, the value must be non-empty, and some ASCII word character must
    /// occur. Non-ASCII characters are outside any ASCII SET and are never word characters.
    /// </summary>
    private static bool MatchesExcludedClassWithWord(ClassAtom[] atoms, ReadOnlySpan<byte> value)
    {
        // atoms[0]: the excluded set; atoms[0].Min == 1 when leading '!'s are required, 0 otherwise.
        ClassAtom excluded = atoms[0];
        int pos = 0;
        if (excluded.Min == 1)
        {
            while (pos < value.Length && value[pos] == (byte)'!')
            {
                pos++;
            }

            if (pos == 0)
            {
                return false;
            }
        }

        if (pos >= value.Length)
        {
            return false;
        }

        // The trailing .+ excludes line terminators whatever the set says.
        bool word = false;
        for (int i = pos; i < value.Length; i++)
        {
            byte b = value[i];
            if (b < 128)
            {
                if (excluded.Contains(b) || b == (byte)'\n' || b == (byte)'\r')
                {
                    return false;
                }

                word |= (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'z') || (b >= (byte)'A' && b <= (byte)'Z') || b == (byte)'_';
            }
            else if (b == 0xE2 && i + 2 < value.Length && value[i + 1] == 0x80 && (value[i + 2] == 0xA8 || value[i + 2] == 0xA9))
            {
                return false;
            }
        }

        return word;
    }

    /// <summary>Parses <c>^(a|b|c)$</c>-style anchored alternations of plain literals.</summary>
    private static bool TryParseLiterals(string pattern, out byte[][]? literals)
    {
        literals = null;
        if (pattern.Length < 3 || pattern[0] != '^' || pattern[pattern.Length - 1] != '$')
        {
            return false;
        }

        string body = pattern.Substring(1, pattern.Length - 2);
        if (body.StartsWith("(?:", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
        {
            body = body.Substring(3, body.Length - 4);
        }
        else if (body.StartsWith("(", StringComparison.Ordinal) && body.EndsWith(")", StringComparison.Ordinal))
        {
            body = body.Substring(1, body.Length - 2);
        }
        else if (body.IndexOf('|') >= 0)
        {
            // ^a|b$ anchors only its first and last alternatives.
            return false;
        }

        if (body.Length == 0 || body.IndexOf('(') >= 0)
        {
            return false;
        }

        string[] parts = body.Split('|');
        var result = new byte[parts.Length][];
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                return false;
            }

            foreach (char c in part)
            {
                if (c >= 128 || !(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '/' || c == '@' || c == '#' || c == ',' || c == '=' || c == '%' || c == '!' || c == '~' || c == ' '))
                {
                    return false;
                }
            }

            result[i] = System.Text.Encoding.UTF8.GetBytes(part);
        }

        literals = result;
        return true;
    }

    /// <summary>
    /// Parses <c>^a|b|c$</c>: an ungrouped alternation of plain literals where the anchors bind only to the first and
    /// last alternatives, so <c>^ES5|ES6|ES7$</c> is "starts with ES5, or contains ES6, or ends with ES7".
    /// </summary>
    private static bool TryParseAnchoredLiterals(string pattern, out byte[][]? literals, out LiteralAnchor[]? anchors)
    {
        literals = null;
        anchors = null;
        if (pattern.Length < 2 || pattern.IndexOf('|') < 0 || pattern.IndexOf('(') >= 0 || pattern.IndexOf(')') >= 0)
        {
            return false;
        }

        bool startAnchored = pattern[0] == '^';
        bool endAnchored = pattern[pattern.Length - 1] == '$';
        string body = pattern.Substring(startAnchored ? 1 : 0, pattern.Length - (startAnchored ? 1 : 0) - (endAnchored ? 1 : 0));
        if (body.Length == 0 || body.IndexOf('^') >= 0 || body.IndexOf('$') >= 0)
        {
            return false;
        }

        string[] parts = body.Split('|');
        var result = new byte[parts.Length][];
        var resultAnchors = new LiteralAnchor[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (part.Length == 0)
            {
                return false;
            }

            foreach (char c in part)
            {
                if (c >= 128 || !(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ':' || c == '/' || c == '@' || c == '#' || c == ',' || c == '=' || c == '%' || c == '!' || c == '~' || c == ' '))
                {
                    return false;
                }
            }

            result[i] = System.Text.Encoding.UTF8.GetBytes(part);
            bool start = startAnchored && i == 0;
            bool end = endAnchored && i == parts.Length - 1;
            resultAnchors[i] = start && end ? LiteralAnchor.Both : start ? LiteralAnchor.Start : end ? LiteralAnchor.End : LiteralAnchor.None;
        }

        literals = result;
        anchors = resultAnchors;
        return true;
    }

    /// <summary>Recognises exactly <c>^(?=[^SET]+$)(?=(.*\w)).+$</c> and <c>^(?=!+[^SET]+$)(?=(.*\w)).+$</c> with an ASCII SET.</summary>
    private static bool TryParseExcludedClassWithWord(string pattern, [NotNullWhen(true)] out ClassAtom[]? atoms)
    {
        atoms = null;
        const string Tail = "]+$)(?=(.*\\w)).+$";
        if (!pattern.EndsWith(Tail, StringComparison.Ordinal))
        {
            return false;
        }

        int bangs;
        if (pattern.StartsWith("^(?=[^", StringComparison.Ordinal))
        {
            bangs = 0;
        }
        else if (pattern.StartsWith("^(?=!+[^", StringComparison.Ordinal))
        {
            bangs = 1;
        }
        else
        {
            return false;
        }

        int start = bangs == 0 ? 6 : 8;
        int end = pattern.Length - Tail.Length;
        if (end <= start)
        {
            return false;
        }

        ulong bits0 = 0;
        ulong bits1 = 0;
        if (!TryParseClass(pattern.AsSpan(start, end - start), ref bits0, ref bits1))
        {
            return false;
        }

        atoms = [new ClassAtom(bits0, bits1, bangs, bangs)];
        return true;
    }

    private const int MaxAlternatives = 64;

    /// <summary>
    /// Parses <c>sequence ('|' sequence)*</c> up to <paramref name="end"/> or an unmatched <c>)</c>. At the top level
    /// an alternation is refused: in <c>^a|b$</c> only the first alternative is anchored at the start.
    /// </summary>
    private static bool TryParseAlternation(string pattern, ref int i, int end, int depth, [NotNullWhen(true)] out List<List<ClassAtom>>? alternatives)
    {
        alternatives = null;
        var all = new List<List<ClassAtom>>();
        while (true)
        {
            if (!TryParseSequence(pattern, ref i, end, depth, out List<List<ClassAtom>>? sequence))
            {
                return false;
            }

            all.AddRange(sequence);
            if (all.Count > MaxAlternatives)
            {
                return false;
            }

            if (i < end && pattern[i] == '|')
            {
                if (depth == 0)
                {
                    return false;
                }

                i++;
                continue;
            }

            break;
        }

        alternatives = all;
        return true;
    }

    /// <summary>Parses atoms and groups up to <paramref name="end"/>, <c>|</c> or <c>)</c>; a group multiplies the alternatives so far.</summary>
    private static bool TryParseSequence(string pattern, ref int i, int end, int depth, [NotNullWhen(true)] out List<List<ClassAtom>>? alternatives)
    {
        alternatives = null;
        var current = new List<List<ClassAtom>> { new() };
        while (i < end && pattern[i] != '|' && pattern[i] != ')')
        {
            if (pattern[i] == '(')
            {
                i++;
                if (i + 1 < end && pattern[i] == '?' && pattern[i + 1] == ':')
                {
                    i += 2;
                }
                else if (i < end && pattern[i] == '?')
                {
                    // Lookarounds and named groups stay with the regex.
                    return false;
                }

                if (!TryParseAlternation(pattern, ref i, end, depth + 1, out List<List<ClassAtom>>? group) || i >= end || pattern[i] != ')')
                {
                    return false;
                }

                i++;
                if (i < end && pattern[i] == '?')
                {
                    group.Add([]);
                    i++;
                }
                else if (i < end && pattern[i] == '{')
                {
                    // (…){n} for a small fixed n repeats the group's alternatives n times.
                    int close = pattern.IndexOf('}', i);
                    if (close < 0 || close >= end || !int.TryParse(pattern.Substring(i + 1, close - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int times) || times > 8)
                    {
                        return false;
                    }

                    List<List<ClassAtom>> once = group;
                    group = [[]];
                    for (int t = 0; t < times; t++)
                    {
                        if (group.Count * once.Count > MaxAlternatives)
                        {
                            return false;
                        }

                        var repeated = new List<List<ClassAtom>>(group.Count * once.Count);
                        foreach (List<ClassAtom> head in group)
                        {
                            foreach (List<ClassAtom> tail in once)
                            {
                                var combined = new List<ClassAtom>(head.Count + tail.Count);
                                combined.AddRange(head);
                                combined.AddRange(tail);
                                repeated.Add(combined);
                            }
                        }

                        group = repeated;
                    }

                    i = close + 1;
                }

                if (i < end && (pattern[i] == '*' || pattern[i] == '+' || pattern[i] == '{' || pattern[i] == '?'))
                {
                    return false;
                }

                if (current.Count * group.Count > MaxAlternatives)
                {
                    return false;
                }

                var product = new List<List<ClassAtom>>(current.Count * group.Count);
                foreach (List<ClassAtom> head in current)
                {
                    foreach (List<ClassAtom> tail in group)
                    {
                        var combined = new List<ClassAtom>(head.Count + tail.Count);
                        combined.AddRange(head);
                        combined.AddRange(tail);
                        product.Add(combined);
                    }
                }

                current = product;
                continue;
            }

            if (!TryParseAtom(pattern, ref i, end, out ClassAtom atom))
            {
                return false;
            }

            foreach (List<ClassAtom> alternative in current)
            {
                alternative.Add(atom);
            }
        }

        alternatives = current;
        return true;
    }

    /// <summary>Parses one class, escape, literal or <c>.</c> with its quantifier.</summary>
    private static bool TryParseAtom(string pattern, ref int i, int end, out ClassAtom atom)
    {
        atom = default;
        ulong bits0 = 0;
        ulong bits1 = 0;
        NonAscii extra = NonAscii.None;
        byte[]? rune = null;
        char c = pattern[i];
        if (c == '[')
        {
            int close = FindClassEnd(pattern, i + 1);
            if (close < 0 || close > end - 1)
            {
                return false;
            }

            bool negated = i + 1 < close && pattern[i + 1] == '^';
            if (!TryParseClass(pattern.AsSpan(i + (negated ? 2 : 1), close - i - (negated ? 2 : 1)), ref bits0, ref bits1, ref extra))
            {
                return false;
            }

            if (negated)
            {
                bits0 = ~bits0;
                bits1 = ~bits1;
                extra = NonAscii.All & ~extra;
            }

            i = close + 1;
        }
        else if (c == '\\')
        {
            if (i + 1 >= end)
            {
                return false;
            }

            char e = pattern[i + 1];
            if (!TryAddEscape(e, ref bits0, ref bits1, ref extra))
            {
                return false;
            }

            i += 2;
        }
        else if (c == '.')
        {
            ClassAtom dot = ClassAtom.Dot(1, 1);
            bits0 = dot.Bits0;
            bits1 = dot.Bits1;
            extra = dot.Extra;
            i++;
        }
        else if (c >= 32 && c < 127 && c != '^' && c != '$' && c != '|' && c != '?' && c != '*' && c != '+' && c != '(' && c != ')' && c != '[' && c != ']' && c != '{' && c != '}')
        {
            Set(c, ref bits0, ref bits1);
            i++;
        }
        else if (c >= 128)
        {
            // A literal outside ASCII: exactly that character.
            int codePoint;
            if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(pattern[i + 1]))
            {
                codePoint = char.ConvertToUtf32(c, pattern[i + 1]);
                i += 2;
            }
            else if (char.IsSurrogate(c))
            {
                return false;
            }
            else
            {
                codePoint = c;
                i++;
            }

            rune = System.Text.Encoding.UTF8.GetBytes(char.ConvertFromUtf32(codePoint));
        }
        else
        {
            return false;
        }

        int min = 1;
        int max = 1;
        if (i < end)
        {
            char q = pattern[i];
            if (q == '*')
            {
                min = 0;
                max = -1;
                i++;
            }
            else if (q == '+')
            {
                min = 1;
                max = -1;
                i++;
            }
            else if (q == '?')
            {
                min = 0;
                max = 1;
                i++;
            }
            else if (q == '{')
            {
                int close = pattern.IndexOf('}', i);
                if (close < 0 || close >= end)
                {
                    return false;
                }

                string inner = pattern.Substring(i + 1, close - i - 1);
                int comma = inner.IndexOf(',');
                if (comma < 0)
                {
                    if (!int.TryParse(inner, NumberStyles.None, CultureInfo.InvariantCulture, out min))
                    {
                        return false;
                    }

                    max = min;
                }
                else
                {
                    if (!int.TryParse(inner.Substring(0, comma), NumberStyles.None, CultureInfo.InvariantCulture, out min))
                    {
                        return false;
                    }

                    string upper = inner.Substring(comma + 1);
                    if (upper.Length == 0)
                    {
                        max = -1;
                    }
                    else if (!int.TryParse(upper, NumberStyles.None, CultureInfo.InvariantCulture, out max) || max < min)
                    {
                        return false;
                    }
                }

                i = close + 1;
            }

            // A lazy or possessive suffix changes nothing for an anchored match but is left to the regex.
            if (i < end && (pattern[i] == '?' || pattern[i] == '+'))
            {
                return false;
            }
        }

        atom = new ClassAtom(bits0, bits1, min, max, extra, rune);
        return true;
    }

    /// <summary>The index of the <c>]</c> closing a class whose body starts at <paramref name="from"/>, skipping escapes; a leading <c>]</c> or <c>^]</c> is literal.</summary>
    private static int FindClassEnd(string pattern, int from)
    {
        int i = from;
        if (i < pattern.Length && pattern[i] == '^')
        {
            i++;
        }

        if (i < pattern.Length && pattern[i] == ']')
        {
            i++;
        }

        for (; i < pattern.Length; i++)
        {
            if (pattern[i] == '\\')
            {
                i++;
            }
            else if (pattern[i] == ']')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool TryParseClass(ReadOnlySpan<char> body, ref ulong bits0, ref ulong bits1)
    {
        NonAscii extra = NonAscii.None;
        return TryParseClass(body, ref bits0, ref bits1, ref extra) && extra == NonAscii.None;
    }

    private static bool TryParseClass(ReadOnlySpan<char> body, ref ulong bits0, ref ulong bits1, ref NonAscii extra)
    {
        if (body.Length == 0)
        {
            return false;
        }

        int i = 0;
        while (i < body.Length)
        {
            char c = body[i];
            char low;
            if (c == '\\')
            {
                if (i + 1 >= body.Length)
                {
                    return false;
                }

                char e = body[i + 1];
                if (char.IsLetter(e))
                {
                    if (!TryAddEscape(e, ref bits0, ref bits1, ref extra))
                    {
                        return false;
                    }

                    i += 2;
                    continue;
                }

                if (e >= 128)
                {
                    return false;
                }

                low = e;
                i += 2;
            }
            else if (c >= 128)
            {
                return false;
            }
            else
            {
                // A bare '[' inside a class is a literal in ECMA-262.
                low = c;
                i++;
            }

            if (i + 1 < body.Length && body[i] == '-')
            {
                char high = body[i + 1];
                if (high == '\\' || high >= 128 || high < low)
                {
                    return false;
                }

                for (char r = low; r <= high; r++)
                {
                    Set(r, ref bits0, ref bits1);
                }

                i += 2;
            }
            else
            {
                Set(low, ref bits0, ref bits1);
            }
        }

        return true;
    }

    /// <summary>Adds an escape's characters: the ECMA-262 classes (<c>\d \w \s</c> and their negations, ASCII except for the <c>\s</c> spaces), the control escapes, or a literal.</summary>
    private static bool TryAddEscape(char e, ref ulong bits0, ref ulong bits1, ref NonAscii extra)
    {
        ulong d0 = 0;
        ulong d1 = 0;
        switch (e)
        {
            case 'd':
            case 'D':
                for (char r = '0'; r <= '9'; r++)
                {
                    Set(r, ref d0, ref d1);
                }

                break;
            case 'w':
            case 'W':
                for (char r = '0'; r <= '9'; r++)
                {
                    Set(r, ref d0, ref d1);
                }

                for (char r = 'a'; r <= 'z'; r++)
                {
                    Set(r, ref d0, ref d1);
                }

                for (char r = 'A'; r <= 'Z'; r++)
                {
                    Set(r, ref d0, ref d1);
                }

                Set('_', ref d0, ref d1);
                break;
            case 's':
            case 'S':
                Set('\t', ref d0, ref d1);
                Set('\n', ref d0, ref d1);
                Set('\v', ref d0, ref d1);
                Set('\f', ref d0, ref d1);
                Set('\r', ref d0, ref d1);
                Set(' ', ref d0, ref d1);
                break;
            case 'n':
                Set('\n', ref bits0, ref bits1);
                return true;
            case 'r':
                Set('\r', ref bits0, ref bits1);
                return true;
            case 't':
                Set('\t', ref bits0, ref bits1);
                return true;
            case 'v':
                Set('\v', ref bits0, ref bits1);
                return true;
            case 'f':
                Set('\f', ref bits0, ref bits1);
                return true;
            default:
                if (e < 128 && !char.IsLetterOrDigit(e))
                {
                    Set(e, ref bits0, ref bits1);
                    return true;
                }

                return false;
        }

        if (char.IsLower(e))
        {
            bits0 |= d0;
            bits1 |= d1;
            if (e == 's')
            {
                extra |= NonAscii.Spaces | NonAscii.LineSeparators;
            }
        }
        else
        {
            bits0 |= ~d0;
            bits1 |= ~d1;
            extra |= e == 'S' ? NonAscii.Others : NonAscii.All;
        }

        return true;
    }

    private static void Set(char c, ref ulong bits0, ref ulong bits1)
    {
        if (c < 64)
        {
            bits0 |= 1UL << c;
        }
        else
        {
            bits1 |= 1UL << (c - 64);
        }
    }

    private static bool TryParseRange(string pattern, out int min, out int max)
    {
        // ^.{m,n}$
        min = 0;
        max = 0;
        if (!pattern.StartsWith("^.{", StringComparison.Ordinal) || !pattern.EndsWith("}$", StringComparison.Ordinal))
        {
            return false;
        }

        string inner = pattern[3..^2];
        int comma = inner.IndexOf(',');
        if (comma < 0)
        {
            return false;
        }

        return int.TryParse(inner.Substring(0, comma), NumberStyles.Integer, CultureInfo.InvariantCulture, out min) && int.TryParse(inner.Substring(comma + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out max);
    }
}

/// <summary>
/// A reference to a child schema node together with its evaluation-path segment.
/// </summary>
internal struct ChildRef
{
    public static readonly ChildRef None = new(-1, null);

    public ChildRef(int node, byte[]? path)
    {
        this.Node = node;
        this.Path = path;
        this.FastNode = node;
    }

    public int Node;

    /// <summary>
    /// The evaluation path segment to report in collecting mode when the child is reached through elided pure
    /// <c>$ref</c> hops (<see cref="Path"/> followed by one <c>$ref</c> per hop), or <see langword="null"/> when
    /// <see cref="Path"/> applies.
    /// </summary>
    public byte[]? CollectingPath;

    /// <summary>The evaluation path segment (without leading slash), e.g. "properties/foo".</summary>
    public byte[]? Path;

    /// <summary>
    /// The node to evaluate in flag mode: pure <c>$ref</c> nodes are elided so evaluation jumps straight to the target.
    /// </summary>
    public int FastNode;

    public readonly bool IsPresent => this.Node >= 0;
}

/// <summary>
/// An entry in the compiled properties map.
/// </summary>
internal sealed class PropertyEntry
{
    public byte[] Name = [];

    public ChildRef Schema = ChildRef.None;

    /// <summary>The index of the bit set when the property is seen, or -1.</summary>
    public int SeenBit = -1;

    /// <summary>Whether the property is listed in <c>required</c>.</summary>
    public bool IsRequired;

    /// <summary>
    /// When the child is a type-only leaf, its type mask, so the object loops test the token type in place of a call;
    /// <see cref="TypeMask.None"/> otherwise. Derived from the graph (see <c>SchemaCompiler.ComputeObjectDetails</c>).
    /// </summary>
    public TypeMask InlineType;

    /// <summary>Whether <see cref="InlineType"/> uses draft 4's lexical integer test.</summary>
    public bool InlineLexical;

    /// <summary>Whether the child is the schema <c>true</c> (nothing to evaluate). Derived from the graph.</summary>
    public bool InlineTrue;

    /// <summary>When the child is a leaf whose only assertion is an <c>enum</c> of strings (with at most <c>type: string</c>), that set. Derived from the graph.</summary>
    public Utf8NameMap<object>? InlineEnum;
}

/// <summary>
/// What the strict object loop does with one known property, as a value: the seen bit, a type mask to test in place,
/// a string set to test in place, or a child node to dispatch on (-1 for none).
/// </summary>
internal readonly struct StrictEntry(int seenBit, TypeMask mask, bool lexical, Utf8NameMap<object>? set, int child, bool nestedObject = false)
{
    /// <summary>Whether <see cref="Child"/> is a strict object the loop enters without its prologue when the value is an object.</summary>
    public readonly bool NestedObject = nestedObject;

    public readonly int SeenBit = seenBit;
    public readonly TypeMask Mask = mask;
    public readonly bool Lexical = lexical;
    public readonly Utf8NameMap<object>? Set = set;
    public readonly int Child = child;

    /// <summary>The token types <see cref="Mask"/> accepts, one bit per <see cref="JsonTokenType"/> value; 0 when there is no type to test.</summary>
    public readonly ushort TokenBits = TokenBitsOf(mask);

    /// <summary>Whether a number token must also be an integer (the mask has integer but not number).</summary>
    public readonly bool IntegerOnly = (mask & TypeMask.Integer) != 0 && (mask & TypeMask.Number) == 0;

    /// <summary>The token bits a type mask accepts: a number token for number or integer, both booleans for boolean.</summary>
    public static ushort TokenBitsOf(TypeMask mask)
    {
        int bits = 0;
        if ((mask & TypeMask.String) != 0)
        {
            bits |= 1 << (int)JsonTokenType.String;
        }

        if ((mask & TypeMask.Object) != 0)
        {
            bits |= 1 << (int)JsonTokenType.StartObject;
        }

        if ((mask & TypeMask.Array) != 0)
        {
            bits |= 1 << (int)JsonTokenType.StartArray;
        }

        if ((mask & (TypeMask.Number | TypeMask.Integer)) != 0)
        {
            bits |= 1 << (int)JsonTokenType.Number;
        }

        if ((mask & TypeMask.Boolean) != 0)
        {
            bits |= (1 << (int)JsonTokenType.True) | (1 << (int)JsonTokenType.False);
        }

        if ((mask & TypeMask.Null) != 0)
        {
            bits |= 1 << (int)JsonTokenType.Null;
        }

        return (ushort)bits;
    }
}

/// <summary>
/// A <c>patternProperties</c> entry.
/// </summary>
internal sealed class PatternPropertyEntry
{
    public PatternMatcher Matcher = null!;

    public ChildRef Schema;

    public byte[] Name = [];
}

/// <summary>
/// A <c>dependencies</c>/<c>dependentRequired</c>/<c>dependentSchemas</c> entry.
/// </summary>
internal sealed class DependencyEntry
{
    public byte[] Name = [];

    /// <summary>The dependency property name as text, for messages.</summary>
    public string NameText = string.Empty;

    public int SeenBit;

    public int[] RequiredSeenBits = [];

    public byte[][] RequiredNames = [];

    public ChildRef Schema = ChildRef.None;
}

/// <summary>
/// A dynamic reference resolved against the dynamic scope at evaluation time.
/// </summary>
internal sealed class DynamicRefTarget
{
    public string Anchor = string.Empty;

    /// <summary>The node used when no resource in the dynamic scope has the anchor.</summary>
    public int FallbackNode = -1;

    /// <summary>Resource id to node id. Indexed by resource id; -1 where the resource has no such anchor.</summary>
    public int[] NodeByResource = [];

    /// <summary>
    /// When every entry point that can reach the reference starts in a resource that defines the anchor, the
    /// outermost scope decides on every path and the target is a function of the entry resource alone: this table,
    /// indexed by entry resource id, gives it, and the evaluator keeps no dynamic scope for the reference.
    /// </summary>
    public int[]? NodeByEntryResource;

    /// <summary>Gets a value indicating whether the reference needs the dynamic scope at evaluation time.</summary>
    public bool NeedsScope => this.NodeByEntryResource is null;

    public byte[] PathSegment = [];

    public bool IsRecursive;
}

/// <summary>
/// Pre-computed branch selection for oneOf/anyOf keyed on a string-valued property of the instance.
/// </summary>
internal sealed class Discriminator
{
    /// <summary>The property name (unescaped UTF-8).</summary>
    public byte[] PropertyName = [];

    /// <summary>Branch indices to evaluate for each known discriminator value.</summary>
    public Utf8NameMap<int[]> KnownValues = null!;

    /// <summary>Branch indices to evaluate for a string value not in any set.</summary>
    public int[] UnknownString = [];

    /// <summary>Branch indices to evaluate when the property has a value of a kind no branch keys on (object, array, null).</summary>
    public int[] NonString = [];

    /// <summary>Every branch: the choice when the value is a number whose text is not canonical, since it may equal any keyed number.</summary>
    public int[] AllBranches = [];

    /// <summary>Whether every branch requires the property, so its absence fails all branches.</summary>
    public bool AllRequire;

    /// <summary>Key tag for a string value; keys are the tag byte followed by the value's UTF-8.</summary>
    public const byte StringTag = (byte)'s';

    /// <summary>Key tag for a canonical integer value (the digits with an optional sign).</summary>
    public const byte NumberTag = (byte)'n';

    /// <summary>Key tag for a boolean value (<c>true</c> or <c>false</c>).</summary>
    public const byte BooleanTag = (byte)'b';

    /// <summary>Key tag for <c>null</c>; the value bytes are <c>null</c>.</summary>
    public const byte NullTag = (byte)'z';
}

/// <summary>
/// An annotation-only keyword whose raw JSON value is reported when collecting results.
/// </summary>
internal sealed class AnnotationEntry
{
    public byte[] Keyword = [];

    public byte[] RawJson = [];

    /// <summary>When set, the annotation is only produced for string instances.</summary>
    public bool StringsOnly;
}

/// <summary>
/// A constant or enum value element.
/// </summary>
internal readonly struct ConstantValue
{
    public ConstantValue(IJsonDocument document, int index, JsonTokenType tokenType)
    {
        this.Document = document;
        this.Index = index;
        this.TokenType = tokenType;
    }

    public IJsonDocument Document { get; }

    public int Index { get; }

    public JsonTokenType TokenType { get; }
}

/// <summary>
/// A compiled subschema.
/// </summary>
/// <summary>
/// Keyword-presence and structural flags of a node packed into one word so that node entry tests a single field.
/// Computed by the compiler's final pass from the individual fields, which remain the source of truth.
/// </summary>
[Flags]
internal enum NodeFlags : uint
{
    None = 0,
    AlwaysTrue = 1u << 0,
    AlwaysFalse = 1u << 1,
    HasType = 1u << 2,
    HasConst = 1u << 3,
    HasEnum = 1u << 4,
    HasNumberKeywords = 1u << 5,
    HasStringKeywords = 1u << 6,
    HasObjectKeywords = 1u << 7,
    HasArrayKeywords = 1u << 8,
    HasInPlaceApplicators = 1u << 9,
    HasUnevaluatedProperties = 1u << 10,
    HasUnevaluatedItems = 1u << 11,
    HasAnnotations = 1u << 12,
    TracksProperties = 1u << 13,
    TracksItems = 1u << 14,

    /// <summary>The node lies on a cycle of in-place applicators, so entering it needs the runaway guard.</summary>
    InPlaceCycle = 1u << 15,
    Draft4 = 1u << 16,

    AlwaysBoolean = AlwaysTrue | AlwaysFalse,
    ValueKeywords = HasType | HasConst | HasEnum,
}

/// <summary>
/// The fused flag-mode evaluation routine selected for a node at compile time. Child entry sites dispatch on this
/// once instead of testing keyword flags on every entry (the runtime analogue of Blaze's instruction selection).
/// Collecting mode and in-place entry with a live evaluated bitset always use the general path.
/// </summary>
internal enum NodePlan : byte
{
    /// <summary>The general keyword-by-keyword path (<c>Evaluator.Eval</c>).</summary>
    General = 0,

    /// <summary>The boolean schema <c>true</c>.</summary>
    AlwaysTrue,

    /// <summary>The boolean schema <c>false</c>.</summary>
    AlwaysFalse,

    /// <summary>Only local keywords (type/const/enum/number/string).</summary>
    Leaf,

    /// <summary><c>type: array</c> with leaf items and length bounds only.</summary>
    SimpleArray,

    /// <summary>Optional <c>type</c> plus <c>items</c> and length bounds only; items may be any plan.</summary>
    ArrayItems,

    /// <summary>Optional <c>type</c> plus properties/required/additionalProperties/property-count bounds only.</summary>
    Object,

    /// <summary>
    /// An <see cref="Object"/> without pattern properties or dependencies and with at most 64 seen bits: one loop that
    /// looks each name up (or, for an object with only <c>additionalProperties</c>, skips the lookup) and tests the
    /// value's token type where the child is a type-only leaf, dispatching on the child's plan otherwise, then one
    /// mask test for <c>required</c>.
    /// </summary>
    StrictObject,

    /// <summary>A bare <c>$dynamicRef</c>: resolved against the dynamic scope at the entry site and dispatched directly.</summary>
    DynamicRef,

    /// <summary>A node whose only assertion is one in-place child (a lone <c>allOf</c> branch or <c>$ref</c>): flag mode goes straight to <see cref="SchemaNode.ForwardNode"/>.</summary>
    Forward,

    /// <summary>A node whose only keyword is an <c>anyOf</c>/<c>oneOf</c> of type-only branches: one mask test (<see cref="SchemaNode.InPlaceUnionMask"/>).</summary>
    TypeUnion,

    /// <summary>A node whose only keyword is an <c>anyOf</c>/<c>oneOf</c> with disjoint branch types: the token type selects the branch (<see cref="SchemaNode.InPlaceDispatch"/>).</summary>
    TypeDispatch,

    /// <summary>One pass over an object for a schema whose object semantics are spread over in-place applicators; see <see cref="FusedObject"/>.</summary>
    FusedObject,

    /// <summary>
    /// Type and object keywords through their plan (<see cref="SchemaNode.ConditionalOwnPlan"/>), then <c>if</c> and the
    /// branch it selects as children: the shape of a decision tree too large to fuse, without the general path's bookkeeping.
    /// </summary>
    Conditional,
}

internal sealed class SchemaNode
{
    /// <summary>The number of 64-bit words of seen/evaluated bits the evaluator keeps on the stack before renting (256 properties or items).</summary>
    public const int InlineBitWords = 4;

    public int Id;
    public int ResourceId;
    public JsonSchemaDialect Dialect;

    public bool AlwaysTrue;
    public bool AlwaysFalse;

    /// <summary>The node consists solely of a <c>type</c> keyword (plus annotations), so flag-mode evaluation is a token-type test.</summary>
    public bool IsTypeOnly;

    /// <summary>
    /// The node has only local keywords (type, const, enum, number and string constraints): no applicators, no
    /// object/array keywords, no references. Flag mode evaluates it without the node-entry bookkeeping.
    /// </summary>
    public bool IsLeaf;

    /// <summary>
    /// The node is an array of leaf items with optional size bounds (and nothing else), so flag mode can run
    /// a fused loop without per-item node entry (Blaze's LoopItemsTypeStrict family).
    /// </summary>
    public bool IsSimpleArray;

    public bool TracksProperties;
    public bool TracksItems;

    /// <summary>The node (or an in-place descendant) can mark object properties as evaluated.</summary>
    public bool MarksProperties;

    /// <summary>The node (or an in-place descendant) can mark array items as evaluated.</summary>
    public bool MarksItems;

    public byte[] SchemaLocation = [];

    // Summary flags
    public bool HasNumberKeywords;
    public bool HasStringKeywords;
    public bool HasObjectKeywords;
    public bool HasArrayKeywords;
    public bool HasInPlaceApplicators;

    /// <summary>Set when the node is part of a cycle of in-place applicators (see <see cref="NodeFlags.InPlaceCycle"/>).</summary>
    public bool InPlaceCycle;

    /// <summary>The packed flags; see <see cref="NodeFlags"/>.</summary>
    public NodeFlags Flags;

    /// <summary>The fused flag-mode routine for this node; see <see cref="NodePlan"/>.</summary>
    public NodePlan Plan;

    /// <summary>The fused object plan (flag mode), when the node's object semantics fuse into one pass.</summary>
    public FusedObject? Fused;
    public bool HasSeenBits;

    // type
    public bool HasType;
    public TypeMask Type;

#if !STJ
    /// <summary>The message reported for the <c>type</c> keyword (match or mismatch), mirroring generated models.</summary>
    public JsonSchemaMessageProvider? TypeMessage;
#endif

    // const / enum
    public bool HasConst;
    public ConstantValue Const;
    public byte[]? ConstString;

    /// <summary>The <c>const</c> value as message text: the JSON literal for numbers, the string for strings.</summary>
    public string? ConstText;
    public NumberValue? ConstNumber;
    public ConstantValue[]? Enum;
    public Utf8NameMap<object>? EnumStrings;
    public bool EnumAllStrings;

    // number
    public NumberValue? Minimum;
    public NumberValue? Maximum;
    public NumberValue? ExclusiveMinimum;
    public NumberValue? ExclusiveMaximum;
    public DivisorValue? MultipleOf;

    // string
    public int MinLength = -1;
    public int MaxLength = -1;
    public PatternMatcher? Pattern;
    public FormatKind Format;
    public bool AssertFormat;

    /// <summary>When set with <see cref="AssertFormat"/>, a non-conforming value is reported as a warning rather than a failure.</summary>
    public bool WarnFormat;

    /// <summary>
    /// For a node that is nothing but a <c>$ref</c>, the node its chain of pure references ends at (or -1): results
    /// are reported against that node, as generated models do for reduced types.
    /// </summary>
    public int ElidedTarget = -1;
    public ContentKind Content;
    public bool AssertContent;

    // object
    public Utf8NameMap<PropertyEntry>? Properties;

    /// <summary>When set, flag-mode evaluation looks each entry up by name instead of enumerating the instance.</summary>
    public PropertyEntry[]? UnrolledProperties;
    public int SeenBitCount;
    public int[]? RequiredSeenBits;

    /// <summary>The required seen bits as one word when there are at most 64 of them. Derived from the graph.</summary>
    public ulong RequiredMask;

    /// <summary>Whether <c>additionalProperties</c> is <c>false</c>: an unknown name fails the object. Derived from the graph.</summary>
    public bool AdditionalRejects;

    /// <summary>The <c>additionalProperties</c> schema's type mask when it is a type-only leaf, else <see cref="TypeMask.None"/>. Derived from the graph.</summary>
    public TypeMask AdditionalInlineType;

    public bool AdditionalInlineLexical;

    /// <summary>The node to evaluate unknown names' values against when <c>additionalProperties</c> is a schema that is neither <c>true</c>, <c>false</c> nor a type-only leaf; -1 otherwise. Derived from the graph.</summary>
    public int AdditionalFastNode = -1;

    /// <summary>The strict loop's per-property resolutions, parallel to <see cref="Properties"/>' values. Derived from the graph.</summary>
    public StrictEntry[]? StrictEntries;

    /// <summary>The additional-properties resolution in the same form, for unknown names. Derived from the graph.</summary>
    public StrictEntry AdditionalEntry;
    public byte[][]? RequiredNames;
    public PatternPropertyEntry[]? PatternProperties;
    public ChildRef AdditionalProperties = ChildRef.None;
    public ChildRef PropertyNames = ChildRef.None;
    public ChildRef UnevaluatedProperties = ChildRef.None;
    public int MinProperties = -1;
    public int MaxProperties = -1;
    public DependencyEntry[]? Dependencies;

    // array
    public ChildRef[]? PrefixItems;
    public ChildRef Items = ChildRef.None;
    public ChildRef Contains = ChildRef.None;
    public int MinContains = 1;
    public int MaxContains = -1;
    public bool UniqueItems;
    public int MinItems = -1;
    public int MaxItems = -1;
    public ChildRef UnevaluatedItems = ChildRef.None;
    public bool ContainsMarksEvaluated;

    // in-place applicators
    public ChildRef Ref = ChildRef.None;
    public DynamicRefTarget? DynamicRef;
    public ChildRef[]? AllOf;
    public ChildRef[]? AnyOf;
    public ChildRef[]? OneOf;
    public Discriminator? AnyOfDiscriminator;
    public Discriminator? OneOfDiscriminator;

    /// <summary>
    /// For an <c>anyOf</c> whose branches assert disjoint types: the branch index for each <see cref="JsonTokenType"/>
    /// value, or -1 where no branch accepts the type. Derived from the graph (not stored in images).
    /// </summary>
    public int[]? AnyOfTypeDispatch;

    /// <summary>The <c>oneOf</c> counterpart of <see cref="AnyOfTypeDispatch"/>.</summary>
    public int[]? OneOfTypeDispatch;

    /// <summary>The node flag mode forwards to under <see cref="NodePlan.Forward"/>, or -1. Derived from the graph.</summary>
    public int ForwardNode = -1;

    /// <summary>For a node whose only keyword is one <c>anyOf</c>/<c>oneOf</c>: that keyword's type union, dispatch table and branches. Derived from the graph.</summary>
    public TypeMask InPlaceUnionMask;

    /// <summary>The node flag-mode evaluation enters for this root: the elided target with its forward hop taken. Derived with the forwards.</summary>
    public int FlagEntry;

    /// <summary>Whether <c>items</c> is a strict object the array loop enters without its prologue when the element is an object.</summary>
    public bool ItemsNestedObject;

    /// <summary>Under <see cref="NodePlan.Conditional"/>, the plan for the node's own keywords: strict object, object, leaf (type only) or always-true (none).</summary>
    public NodePlan ConditionalOwnPlan;

    public int[]? InPlaceDispatch;

    public ChildRef[]? InPlaceBranches;

    /// <summary>When every anyOf branch is a type-only schema, the union of their types; otherwise None.</summary>
    public TypeMask AnyOfTypeUnion;

    /// <summary>When every oneOf branch is a type-only schema with disjoint types, the union of their types; otherwise None.</summary>
    public TypeMask OneOfTypeUnion;
    public ChildRef Not = ChildRef.None;
    public ChildRef If = ChildRef.None;
    public ChildRef Then = ChildRef.None;
    public ChildRef Else = ChildRef.None;

    // annotations
    public AnnotationEntry[]? Annotations;

    /// <summary>
    /// Adds every child node to a list: the in-place applicators (with every candidate of a dynamic reference),
    /// the property, item and dependency schemas, and the remaining single-schema keywords.
    /// </summary>
    public void CollectChildren(List<int> into)
    {
        into.AddRange(this.InPlaceChildren(includeNot: true));

        if (this.Properties is not null)
        {
            foreach (PropertyEntry e in this.Properties.Values)
            {
                Add(into, e.Schema);
            }
        }

        if (this.PatternProperties is not null)
        {
            foreach (PatternPropertyEntry e in this.PatternProperties)
            {
                Add(into, e.Schema);
            }
        }

        if (this.Dependencies is not null)
        {
            foreach (DependencyEntry e in this.Dependencies)
            {
                Add(into, e.Schema);
            }
        }

        if (this.PrefixItems is not null)
        {
            foreach (ChildRef c in this.PrefixItems)
            {
                Add(into, c);
            }
        }

        Add(into, this.AdditionalProperties);
        Add(into, this.PropertyNames);
        Add(into, this.UnevaluatedProperties);
        Add(into, this.Items);
        Add(into, this.Contains);
        Add(into, this.UnevaluatedItems);

        static void Add(List<int> into, in ChildRef c)
        {
            if (c.IsPresent)
            {
                into.Add(c.Node);
            }
        }
    }

    /// <summary>
    /// Enumerates the in-place applicator children (those applied to the same instance).
    /// </summary>
    public IEnumerable<int> InPlaceChildren(bool includeNot)
    {
        if (this.Ref.IsPresent)
        {
            yield return this.Ref.Node;
        }

        if (this.DynamicRef is not null)
        {
            if (this.DynamicRef.FallbackNode >= 0)
            {
                yield return this.DynamicRef.FallbackNode;
            }

            foreach (int n in this.DynamicRef.NodeByResource)
            {
                if (n >= 0)
                {
                    yield return n;
                }
            }
        }

        if (this.AllOf is not null)
        {
            foreach (ChildRef c in this.AllOf)
            {
                yield return c.Node;
            }
        }

        if (this.AnyOf is not null)
        {
            foreach (ChildRef c in this.AnyOf)
            {
                yield return c.Node;
            }
        }

        if (this.OneOf is not null)
        {
            foreach (ChildRef c in this.OneOf)
            {
                yield return c.Node;
            }
        }

        if (includeNot && this.Not.IsPresent)
        {
            yield return this.Not.Node;
        }

        if (this.If.IsPresent)
        {
            yield return this.If.Node;
        }

        if (this.Then.IsPresent)
        {
            yield return this.Then.Node;
        }

        if (this.Else.IsPresent)
        {
            yield return this.Else.Node;
        }

        if (this.Dependencies is not null)
        {
            foreach (DependencyEntry d in this.Dependencies)
            {
                if (d.Schema.IsPresent)
                {
                    yield return d.Schema.Node;
                }
            }
        }
    }
}