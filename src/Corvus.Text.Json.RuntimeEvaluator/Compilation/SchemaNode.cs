// <copyright file="SchemaNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Corvus.Text.Json.Internal;

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
        JsonElementHelpers.ParseNumber(raw, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        this.IsNegative = isNegative;
        this.Integral = integral.ToArray();
        this.Fractional = fractional.ToArray();
        this.Exponent = exponent;
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        // Returns the sign of (instance - this).
        return JsonElementHelpers.CompareNormalizedJsonNumbers(isNegative, integral, fractional, exponent, this.IsNegative, this.Integral, this.Fractional, this.Exponent);
    }
}

/// <summary>
/// A normalized <c>multipleOf</c> divisor.
/// </summary>
internal sealed class DivisorValue
{
    public DivisorValue(ReadOnlySpan<byte> raw)
    {
        JsonElementHelpers.ParseNumber(raw, out _, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        this.Exponent = exponent;
        this.Text = System.Text.Encoding.UTF8.GetString(raw);
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
    }

    public bool IsBig { get; }

    public ulong Small { get; }

    public BigInteger Big { get; }

    public int Exponent { get; }

    public string Text { get; }

    public bool IsMultiple(ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        return this.IsBig
            ? JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, this.Big, this.Exponent)
            : JsonElementHelpers.IsMultipleOf(integral, fractional, exponent, this.Small, this.Exponent);
    }
}

/// <summary>
/// A compiled regular expression with fast paths for common shapes.
/// </summary>
internal sealed class PatternMatcher
{
    private static readonly bool ForceInterpretedRegex = Environment.GetEnvironmentVariable("CORVUS_RT_REGEX_INTERPRETED") == "1";
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(string Pattern, RegexOptions Options, TimeSpan Timeout), Regex> RegexCache = new();

    private readonly Regex? regex;
    private readonly byte[]? prefix;
    private readonly int min;
    private readonly int max;
    private readonly Kind kind;

    private PatternMatcher(Kind kind, string source, Regex? regex, byte[]? prefix, int min, int max)
    {
        this.kind = kind;
        this.Source = source;
        this.regex = regex;
        this.prefix = prefix;
        this.min = min;
        this.max = max;
    }

    private enum Kind : byte
    {
        Noop,
        NonEmpty,
        Prefix,
        Range,
        Regex,
    }

    public string Source { get; }

    public static PatternMatcher Create(string ecmaPattern, JsonSchemaEvaluatorOptions options)
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
            Kind.Range => JsonSchemaEvaluation.MatchRangeRegularExpression(utf8Value, this.min, this.max),
            _ => JsonSchemaEvaluation.MatchRegularExpression(utf8Value, this.regex!),
        };
    }

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

    /// <summary>Branch indices to evaluate when the property has a non-string value.</summary>
    public int[] NonString = [];

    /// <summary>Whether every branch requires the property, so its absence fails all branches.</summary>
    public bool AllRequire;
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
internal sealed class SchemaNode
{
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
    public bool HasSeenBits;

    // type
    public bool HasType;
    public TypeMask Type;

    // const / enum
    public bool HasConst;
    public ConstantValue Const;
    public byte[]? ConstString;
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
    public ContentKind Content;
    public bool AssertContent;

    // object
    public Utf8NameMap<PropertyEntry>? Properties;

    /// <summary>When set, flag-mode evaluation looks each entry up by name instead of enumerating the instance.</summary>
    public PropertyEntry[]? UnrolledProperties;
    public int SeenBitCount;
    public int[]? RequiredSeenBits;
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
