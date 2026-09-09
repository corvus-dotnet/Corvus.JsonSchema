// <copyright file="Evaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// The evaluation engine.
/// </summary>
[SkipLocalsInit]
internal static class Evaluator
{
    private const int InlineBitWords = 4; // 256 properties/items before renting

    /// <summary>The size of a metadata row; the layout shared by every Corvus document (see ObjectEnumerator/ArrayEnumerator).</summary>
    internal const int RowSize = 12;

    // Keyword names handed to the shared Corvus helpers on flag-mode paths. Static arrays rather than u8 literals so
    // that tier-0 JIT code (whose static-span accesses go through an allocating helper) stays allocation-free too.
    private static readonly byte[] FormatKeyword = "format"u8.ToArray();
    private static readonly byte[] ContentEncodingKeyword = "contentEncoding"u8.ToArray();
    private static readonly byte[] ContentMediaTypeKeyword = "contentMediaType"u8.ToArray();
    private static readonly bool DisableIntegerFastPath = Environment.GetEnvironmentVariable("CORVUS_RT_NO_INTFAST") == "1";

    public static bool Evaluate(CompiledSchema program, int rootNode, IJsonDocument document, int index, IJsonSchemaResultsCollector? collector)
    {
        Span<int> scope = stackalloc int[32];
        SchemaNode[] nodes = program.Nodes;
        var state = new EvaluationState
        {
            Program = program,
            Nodes = nodes,
            Collector = collector,
            Scope = scope,
            ScopeDepth = 0,
            MaxDepth = program.Options.MaxDepth,
            UsesDynamicScope = program.UsesDynamicScope,
        };

        try
        {
            SchemaNode root = nodes[rootNode];
            bool raw = document is JsonDocument jsonDocument && jsonDocument.TryGetRawAccess(out state.Raw);
            if (collector is null)
            {
                return raw
                    ? Eval<FastMode, RawAccess>(root, document, index, ref state, default, 0)
                    : Eval<FastMode, InterfaceAccess>(root, document, index, ref state, default, 0);
            }

            int seq = collector.BeginChildContext(0);
            bool ok = raw
                ? Eval<CollectingMode, RawAccess>(root, document, index, ref state, default, seq)
                : Eval<CollectingMode, InterfaceAccess>(root, document, index, ref state, default, seq);
            collector.CommitChildContext(seq, parentIsMatch: false, childIsMatch: ok, JsonSchemaEvaluation.EvaluatedSubschema);
            return ok;
        }
        finally
        {
            state.Dispose();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Document access helpers
    // ---------------------------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UnescapedUtf8JsonString StringValue<TAccess>(ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        return TAccess.GetString(ref state, doc, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UnescapedUtf8JsonString PropertyName<TAccess>(ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        return TAccess.GetPropertyName(ref state, doc, valueIndex);
    }

    // ---------------------------------------------------------------------------------------------
    // Node evaluation
    // ---------------------------------------------------------------------------------------------

    private static bool Eval<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (node.AlwaysTrue)
        {
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedBooleanSchema(true, null);
            }

            return true;
        }

        if (node.AlwaysFalse)
        {
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedBooleanSchema(false, null);
            }

            return false;
        }

        bool pushedScope = false;
        if (state.UsesDynamicScope && (state.ScopeDepth == 0 || state.Scope[state.ScopeDepth - 1] != node.ResourceId))
        {
            state.PushScope(node.ResourceId);
            pushedScope = true;
        }

        JsonTokenType tokenType = TAccess.TokenType(ref state, doc, index);

        bool result;
        if (evaluated.IsEmpty && (tokenType == JsonTokenType.StartObject ? node.TracksProperties : (tokenType == JsonTokenType.StartArray && node.TracksItems)))
        {
            int count = TAccess.Count(ref state, doc, index, tokenType);
            int words = (count + 63) >> 6;
            if (words <= InlineBitWords)
            {
                Span<ulong> local = stackalloc ulong[InlineBitWords];
                local = local[..words];
                local.Clear();
                result = EvalCore<TMode, TAccess>(node, doc, index, tokenType, ref state, local, seq);
            }
            else
            {
                ulong[] rented = ArrayPool<ulong>.Shared.Rent(words);
                Span<ulong> bits = rented.AsSpan(0, words);
                bits.Clear();
                try
                {
                    result = EvalCore<TMode, TAccess>(node, doc, index, tokenType, ref state, bits, seq);
                }
                finally
                {
                    ArrayPool<ulong>.Shared.Return(rented);
                }
            }
        }
        else
        {
            result = EvalCore<TMode, TAccess>(node, doc, index, tokenType, ref state, evaluated, seq);
        }

        if (pushedScope)
        {
            state.ScopeDepth--;
        }

        return result;
    }

    private static bool EvalCore<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, JsonTokenType tokenType, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;

        if (node.HasType)
        {
            bool match = MatchesType<TAccess>(node.Type, tokenType, ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(match, match ? null : ExpectedTypeProvider(node.Type), "type"u8);
            }

            if (!match)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.HasConst)
        {
            bool match = MatchesConst<TAccess>(node, ref state, doc, index, tokenType);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(match, null, "const"u8);
            }

            if (!match)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.Enum is not null)
        {
            bool match = MatchesEnum<TAccess>(node, ref state, doc, index, tokenType);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(match, match ? JsonSchemaEvaluation.MatchedAtLeastOneConstantValue : JsonSchemaEvaluation.DidNotMatchAtLeastOneConstantValue, "enum"u8);
            }

            if (!match)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        switch (tokenType)
        {
            case JsonTokenType.Number:
                if (node.HasNumberKeywords)
                {
                    ok &= EvalNumber<TMode, TAccess>(node, doc, index, ref state);
                }

                break;
            case JsonTokenType.String:
                if (node.HasStringKeywords)
                {
                    ok &= EvalString<TMode, TAccess>(node, doc, index, ref state);
                }

                break;
            case JsonTokenType.StartObject:
                if (node.HasObjectKeywords)
                {
                    ok &= EvalObject<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
                }

                break;
            case JsonTokenType.StartArray:
                if (node.HasArrayKeywords)
                {
                    ok &= EvalArray<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
                }

                break;
        }

        if (!ok && !TMode.Collecting)
        {
            return false;
        }

        if (node.HasInPlaceApplicators)
        {
            ok &= EvalInPlace<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !TMode.Collecting)
            {
                return false;
            }
        }

        if (tokenType == JsonTokenType.StartObject && node.UnevaluatedProperties.IsPresent)
        {
            ok &= EvalUnevaluatedProperties<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !TMode.Collecting)
            {
                return false;
            }
        }
        else if (tokenType == JsonTokenType.StartArray && node.UnevaluatedItems.IsPresent)
        {
            ok &= EvalUnevaluatedItems<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !TMode.Collecting)
            {
                return false;
            }
        }

        if (TMode.Collecting && node.Annotations is not null)
        {
            foreach (AnnotationEntry a in node.Annotations)
            {
                if (a.StringsOnly && tokenType != JsonTokenType.String)
                {
                    continue;
                }

                state.Collector!.IgnoredKeyword(a.RawJson, Providers.RawJson, a.Keyword);
            }
        }

        return ok;
    }

    // ---------------------------------------------------------------------------------------------
    // type / const / enum
    // ---------------------------------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesType<TAccess>(TypeMask mask, JsonTokenType tokenType, ref EvaluationState state, IJsonDocument doc, int index, bool lexicalInteger)
        where TAccess : struct, IDocumentAccess
    {
        switch (tokenType)
        {
            case JsonTokenType.String:
                return (mask & TypeMask.String) != 0;
            case JsonTokenType.StartObject:
                return (mask & TypeMask.Object) != 0;
            case JsonTokenType.StartArray:
                return (mask & TypeMask.Array) != 0;
            case JsonTokenType.Number:
                if ((mask & TypeMask.Number) != 0)
                {
                    return true;
                }

                return (mask & TypeMask.Integer) != 0 && IsInteger<TAccess>(ref state, doc, index, lexicalInteger);
            case JsonTokenType.True:
            case JsonTokenType.False:
                return (mask & TypeMask.Boolean) != 0;
            case JsonTokenType.Null:
                return (mask & TypeMask.Null) != 0;
            default:
                return false;
        }
    }

    /// <summary>
    /// Flag-mode evaluation of a leaf node (type/const/enum/number/string keywords only) without the
    /// depth, dynamic-scope and evaluated-bit bookkeeping of <see cref="Eval{TMode}"/>.
    /// </summary>
    private static bool EvalLeafFast<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = TAccess.TokenType(ref state, doc, index);
        if (node.HasType && !MatchesType<TAccess>(node.Type, tokenType, ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4))
        {
            return false;
        }

        if (node.HasConst && !MatchesConst<TAccess>(node, ref state, doc, index, tokenType))
        {
            return false;
        }

        if (node.Enum is not null && !MatchesEnum<TAccess>(node, ref state, doc, index, tokenType))
        {
            return false;
        }

        if (tokenType == JsonTokenType.Number)
        {
            return !node.HasNumberKeywords || EvalNumber<FastMode, TAccess>(node, doc, index, ref state);
        }

        if (tokenType == JsonTokenType.String)
        {
            return !node.HasStringKeywords || EvalString<FastMode, TAccess>(node, doc, index, ref state);
        }

        return true;
    }

    /// <summary>
    /// Fused flag-mode evaluation of an array whose items are leaves: size bounds plus one tight loop.
    /// </summary>
    private static bool EvalSimpleArrayFast<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = TAccess.TokenType(ref state, doc, index);
        if (tokenType != JsonTokenType.StartArray)
        {
            // Not an array: `type` fails, otherwise the array keywords do not apply.
            return !node.HasType;
        }

        int length = TAccess.Count(ref state, doc, index, JsonTokenType.StartArray);
        if ((node.MinItems >= 0 && length < node.MinItems) || (node.MaxItems >= 0 && length > node.MaxItems))
        {
            return false;
        }

        SchemaNode items = state.Nodes[node.Items.FastNode];
        if (items.AlwaysTrue)
        {
            return true;
        }

        int end = TAccess.EndIndex(ref state, doc, index);
        if (items.IsTypeOnly)
        {
            TypeMask mask = items.Type;
            bool lexical = items.Dialect == JsonSchemaDialect.Draft4;
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
            {
                if (!MatchesType<TAccess>(mask, TAccess.TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, lexical))
                {
                    return false;
                }
            }

            return true;
        }

        for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
        {
            if (!EvalLeafFast<TAccess>(items, doc, valueIndex, ref state))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsInteger<TAccess>(ref EvaluationState state, IJsonDocument doc, int index, bool lexicalInteger)
        where TAccess : struct, IDocumentAccess
    {
        ReadOnlySpan<byte> raw = TAccess.RawValue(ref state, doc, index);
        if (raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0)
        {
            return true;
        }

        // Draft 4 defines an integer lexically: no fraction or exponent part.
        if (lexicalInteger)
        {
            return false;
        }

        JsonElementHelpers.ParseNumber(raw, out _, out _, out _, out int exponent);
        return exponent >= 0;
    }

    private static bool MatchesConst<TAccess>(SchemaNode node, ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType)
        where TAccess : struct, IDocumentAccess
    {
        if (node.ConstString is byte[] constString)
        {
            if (tokenType != JsonTokenType.String)
            {
                return false;
            }

            using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
            return s.Span.SequenceEqual(constString);
        }

        if (node.ConstNumber is NumberValue constNumber)
        {
            if (tokenType != JsonTokenType.Number)
            {
                return false;
            }

            JsonElementHelpers.ParseNumber(TAccess.RawValue(ref state, doc, index), out bool neg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
            return constNumber.CompareTo(neg, integral, fractional, exponent) == 0;
        }

        ConstantValue c = node.Const;
        if (c.TokenType is JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null)
        {
            return tokenType == c.TokenType;
        }

        if (tokenType != c.TokenType)
        {
            return false;
        }

        return JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, index, c.Document, c.Index);
    }

    private static bool MatchesEnum<TAccess>(SchemaNode node, ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType)
        where TAccess : struct, IDocumentAccess
    {
        if (node.EnumAllStrings)
        {
            if (tokenType != JsonTokenType.String)
            {
                return false;
            }

            using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
            return node.EnumStrings!.TryGetValue(s.Span, out _);
        }

        ConstantValue[] values = node.Enum!;
        for (int i = 0; i < values.Length; i++)
        {
            ConstantValue c = values[i];
            JsonTokenType ct = c.TokenType;
            if (ct is JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null)
            {
                if (ct == tokenType)
                {
                    return true;
                }

                continue;
            }

            if (ct != tokenType)
            {
                continue;
            }

            if (JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, index, c.Document, c.Index))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------------------------------------
    // number
    // ---------------------------------------------------------------------------------------------

    private static bool EvalNumber<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        ReadOnlySpan<byte> raw = TAccess.RawValue(ref state, doc, index);

        // Plain integer literal against plain integer bounds: compare as longs (exact) without normalising.
        if (!TMode.Collecting && !DisableIntegerFastPath && node.MultipleOf is null && raw.Length <= 18 && raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0
            && System.Buffers.Text.Utf8Parser.TryParse(raw, out long value, out int consumed) && consumed == raw.Length)
        {
            if (node.Minimum is NumberValue lmin)
            {
                if (lmin.AsLong is long b)
                {
                    if (value < b)
                    {
                        return false;
                    }
                }
                else
                {
                    goto Slow;
                }
            }

            if (node.Maximum is NumberValue lmax)
            {
                if (lmax.AsLong is long b)
                {
                    if (value > b)
                    {
                        return false;
                    }
                }
                else
                {
                    goto Slow;
                }
            }

            if (node.ExclusiveMinimum is NumberValue lemin)
            {
                if (lemin.AsLong is long b)
                {
                    if (value <= b)
                    {
                        return false;
                    }
                }
                else
                {
                    goto Slow;
                }
            }

            if (node.ExclusiveMaximum is NumberValue lemax)
            {
                if (lemax.AsLong is long b)
                {
                    if (value >= b)
                    {
                        return false;
                    }
                }
                else
                {
                    goto Slow;
                }
            }

            return true;
        }

    Slow:
        JsonElementHelpers.ParseNumber(raw, out bool neg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
        bool ok = true;

        if (node.Minimum is NumberValue min)
        {
            bool m = min.CompareTo(neg, integral, fractional, exponent) >= 0;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "minimum"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.Maximum is NumberValue max)
        {
            bool m = max.CompareTo(neg, integral, fractional, exponent) <= 0;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "maximum"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.ExclusiveMinimum is NumberValue emin)
        {
            bool m = emin.CompareTo(neg, integral, fractional, exponent) > 0;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "exclusiveMinimum"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.ExclusiveMaximum is NumberValue emax)
        {
            bool m = emax.CompareTo(neg, integral, fractional, exponent) < 0;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "exclusiveMaximum"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.MultipleOf is DivisorValue divisor)
        {
            bool m = divisor.IsMultiple(integral, fractional, exponent);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "multipleOf"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        return ok;
    }

    // ---------------------------------------------------------------------------------------------
    // string
    // ---------------------------------------------------------------------------------------------

    private static bool EvalString<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
        ReadOnlySpan<byte> value = s.Span;
        bool ok = true;

        if (node.MinLength >= 0 || node.MaxLength >= 0)
        {
            int byteLength = value.Length;
            int runeCount = -1;
            if (node.MinLength >= 0)
            {
                bool m;
                if (byteLength < node.MinLength)
                {
                    m = false;
                }
                else
                {
                    runeCount = JsonElementHelpers.CountRunes(value);
                    m = runeCount >= node.MinLength;
                }

                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, null, "minLength"u8);
                }

                if (!m)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            if (node.MaxLength >= 0)
            {
                bool m;
                if (byteLength <= node.MaxLength)
                {
                    m = true;
                }
                else
                {
                    if (runeCount < 0)
                    {
                        runeCount = JsonElementHelpers.CountRunes(value);
                    }

                    m = runeCount <= node.MaxLength;
                }

                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, null, "maxLength"u8);
                }

                if (!m)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }
        }

        if (node.Pattern is PatternMatcher pattern)
        {
            bool m = pattern.IsMatch(value);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "pattern"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.AssertFormat && node.Format != FormatKind.None)
        {
            bool m = MatchesFormat(node.Format, value, ref state);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "format"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.AssertContent)
        {
            bool m = node.Content switch
            {
                ContentKind.Base64 => JsonSchemaEvaluation.MatchBase64String(value, ContentEncodingKeyword, ref state.Scratch),
                ContentKind.Json => JsonSchemaEvaluation.MatchJsonContent(value, ContentMediaTypeKeyword, ref state.Scratch),
                ContentKind.Base64Json => JsonSchemaEvaluation.MatchBase64Content(value, ContentMediaTypeKeyword, ref state.Scratch),
                _ => true,
            };

            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, node.Content == ContentKind.Base64 ? "contentEncoding"u8 : "contentMediaType"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        return ok;
    }

    private static bool MatchesFormat(FormatKind format, scoped ReadOnlySpan<byte> value, ref EvaluationState state)
    {
        ReadOnlySpan<byte> keyword = FormatKeyword;
        return format switch
        {
            FormatKind.Date => JsonSchemaEvaluation.MatchDate(value, keyword, ref state.Scratch),
            FormatKind.DateTime => JsonSchemaEvaluation.MatchDateTime(value, keyword, ref state.Scratch),
            FormatKind.Time => JsonSchemaEvaluation.MatchTime(value, keyword, ref state.Scratch),
            FormatKind.Duration => JsonSchemaEvaluation.MatchDuration(value, keyword, ref state.Scratch),
            FormatKind.Email => JsonSchemaEvaluation.MatchEmail(value, keyword, ref state.Scratch),
            FormatKind.IdnEmail => JsonSchemaEvaluation.MatchIdnEmail(value, keyword, ref state.Scratch),
            FormatKind.Hostname => JsonSchemaEvaluation.MatchHostname(value, keyword, ref state.Scratch),
            FormatKind.IdnHostname => JsonSchemaEvaluation.MatchIdnHostname(value, keyword, ref state.Scratch),
            FormatKind.Ipv4 => JsonSchemaEvaluation.MatchIPV4(value, keyword, ref state.Scratch),
            FormatKind.Ipv6 => JsonSchemaEvaluation.MatchIPV6(value, keyword, ref state.Scratch),
            FormatKind.Uri => JsonSchemaEvaluation.MatchUri(value, keyword, ref state.Scratch),
            FormatKind.UriReference => JsonSchemaEvaluation.MatchUriReference(value, keyword, ref state.Scratch),
            FormatKind.Iri => JsonSchemaEvaluation.MatchIri(value, keyword, ref state.Scratch),
            FormatKind.IriReference => JsonSchemaEvaluation.MatchIriReference(value, keyword, ref state.Scratch),
            FormatKind.Uuid => JsonSchemaEvaluation.MatchUuid(value, keyword, ref state.Scratch),
            FormatKind.UriTemplate => JsonSchemaEvaluation.MatchUriTemplate(value, keyword, ref state.Scratch),
            FormatKind.JsonPointer => JsonSchemaEvaluation.MatchJsonPointer(value, keyword, ref state.Scratch),
            FormatKind.RelativeJsonPointer => JsonSchemaEvaluation.MatchRelativeJsonPointer(value, keyword, ref state.Scratch),
            FormatKind.Regex => JsonSchemaEvaluation.MatchRegex(value, keyword, ref state.Scratch),
            _ => true,
        };
    }

    // ---------------------------------------------------------------------------------------------
    // object
    // ---------------------------------------------------------------------------------------------

    private static bool EvalObject<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (!TMode.Collecting && evaluated.IsEmpty && node.UnrolledProperties is PropertyEntry[] unrolled)
        {
            return EvalObjectUnrolled<TAccess>(node, unrolled, doc, index, ref state);
        }

        int words = (node.SeenBitCount + 63) >> 6;
        if (words <= InlineBitWords)
        {
            Span<ulong> seen = stackalloc ulong[InlineBitWords];
            seen = seen[..words];
            seen.Clear();
            return EvalObjectCore<TMode, TAccess>(node, doc, index, ref state, evaluated, seen, seq);
        }

        ulong[] rented = ArrayPool<ulong>.Shared.Rent(words);
        Span<ulong> bits = rented.AsSpan(0, words);
        bits.Clear();
        try
        {
            return EvalObjectCore<TMode, TAccess>(node, doc, index, ref state, evaluated, bits, seq);
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    /// <summary>
    /// Flag-mode object evaluation by direct property lookup (required entries first).
    /// </summary>
    private static bool EvalObjectUnrolled<TAccess>(SchemaNode node, PropertyEntry[] entries, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            int count = TAccess.Count(ref state, doc, index, JsonTokenType.StartObject);
            if ((node.MinProperties >= 0 && count < node.MinProperties) || (node.MaxProperties >= 0 && count > node.MaxProperties))
            {
                return false;
            }
        }

        for (int i = 0; i < entries.Length; i++)
        {
            PropertyEntry entry = entries[i];
            if (doc.TryGetNamedPropertyValue(index, entry.Name, out IJsonDocument? valueDoc, out int valueIndex))
            {
                if (entry.Schema.IsPresent)
                {
                    bool ok = ReferenceEquals(valueDoc, doc)
                        ? EvalProperty<FastMode, TAccess>(entry.Schema, doc, valueIndex, ref state, 0)
                        : EvalProperty<FastMode, InterfaceAccess>(entry.Schema, valueDoc, valueIndex, ref state, 0);
                    if (!ok)
                    {
                        return false;
                    }
                }
            }
            else if (entry.IsRequired)
            {
                return false;
            }
        }

        return true;
    }

    private static bool EvalObjectCore<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, scoped Span<ulong> seen, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;

        if (node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            int count = TAccess.Count(ref state, doc, index, JsonTokenType.StartObject);
            if (node.MinProperties >= 0)
            {
                bool m = count >= node.MinProperties;
                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, null, "minProperties"u8);
                }

                if (!m)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            if (node.MaxProperties >= 0)
            {
                bool m = count <= node.MaxProperties;
                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, null, "maxProperties"u8);
                }

                if (!m)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }
        }

        Utf8NameMap<PropertyEntry>? properties = node.Properties;
        PatternPropertyEntry[]? patternProperties = node.PatternProperties;
        bool hasAdditional = node.AdditionalProperties.IsPresent;
        bool hasPropertyNames = node.PropertyNames.IsPresent;
        SchemaNode[] nodes = state.Nodes;

        if (properties is not null || patternProperties is not null || hasAdditional || hasPropertyNames)
        {
            // Fast path: additionalProperties: false with no property schemas can short-circuit.
            int propertyIndex = 0;
            int end = TAccess.EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex) + RowSize)
            {
                bool matched = false;
                using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                ReadOnlySpan<byte> nameSpan = name.Span;

                if (properties is not null && properties.TryGetValue(nameSpan, out PropertyEntry? entry))
                {
                    if (entry.SeenBit >= 0)
                    {
                        seen[entry.SeenBit >> 6] |= 1UL << (entry.SeenBit & 63);
                    }

                    if (entry.Schema.IsPresent)
                    {
                        matched = true;
                        MarkEvaluated(evaluated, propertyIndex);
                        if (!EvalProperty<TMode, TAccess>(entry.Schema, doc, valueIndex, ref state, seq))
                        {
                            if (!TMode.Collecting)
                            {
                                return false;
                            }

                            ok = false;
                        }
                    }
                }

                if (patternProperties is not null)
                {
                    for (int p = 0; p < patternProperties.Length; p++)
                    {
                        PatternPropertyEntry pp = patternProperties[p];
                        if (pp.Matcher.IsMatch(nameSpan))
                        {
                            matched = true;
                            MarkEvaluated(evaluated, propertyIndex);
                            if (!EvalProperty<TMode, TAccess>(pp.Schema, doc, valueIndex, ref state, seq))
                            {
                                if (!TMode.Collecting)
                                {
                                    return false;
                                }

                                ok = false;
                            }
                        }
                    }
                }

                if (!matched && hasAdditional)
                {
                    MarkEvaluated(evaluated, propertyIndex);
                    if (!EvalProperty<TMode, TAccess>(node.AdditionalProperties, doc, valueIndex, ref state, seq))
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }

                if (hasPropertyNames)
                {
                    if (!EvalPropertyName<TMode, TAccess>(node.PropertyNames, doc, valueIndex, ref state, seq))
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }

                propertyIndex++;
            }
        }
        else if (node.HasSeenBits)
        {
            // Only presence checks (required / dependencies): enumerate names without descending.
            int end = TAccess.EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex) + RowSize)
            {
                using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                if (node.Properties is not null && node.Properties.TryGetValue(name.Span, out PropertyEntry? entry) && entry.SeenBit >= 0)
                {
                    seen[entry.SeenBit >> 6] |= 1UL << (entry.SeenBit & 63);
                }
            }
        }

        if (node.RequiredSeenBits is int[] required)
        {
            for (int i = 0; i < required.Length; i++)
            {
                int bit = required[i];
                bool present = (seen[bit >> 6] & (1UL << (bit & 63))) != 0;
                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeywordForProperty(present, null, node.RequiredNames![i], "required"u8);
                }

                if (!present)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }
        }

        if (node.Dependencies is DependencyEntry[] dependencies)
        {
            for (int i = 0; i < dependencies.Length; i++)
            {
                DependencyEntry dep = dependencies[i];
                int bit = dep.SeenBit;
                if ((seen[bit >> 6] & (1UL << (bit & 63))) == 0)
                {
                    continue;
                }

                int[] requiredBits = dep.RequiredSeenBits;
                for (int r = 0; r < requiredBits.Length; r++)
                {
                    int rb = requiredBits[r];
                    bool present = (seen[rb >> 6] & (1UL << (rb & 63))) != 0;
                    if (TMode.Collecting)
                    {
                        state.Collector!.EvaluatedKeywordForProperty(present, null, dep.RequiredNames[r], node.Dialect >= JsonSchemaDialect.Draft201909 ? "dependentRequired"u8 : "dependencies"u8);
                    }

                    if (!present)
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }

                if (dep.Schema.IsPresent)
                {
                    bool m = EvalInPlaceChild<TMode, TAccess>(dep.Schema, doc, index, ref state, evaluated, seq);
                    if (TMode.Collecting)
                    {
                        state.Collector!.EvaluatedKeywordForProperty(m, null, dep.Name, node.Dialect >= JsonSchemaDialect.Draft201909 ? "dependentSchemas"u8 : "dependencies"u8);
                    }

                    if (!m)
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }
            }
        }

        return ok;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MarkEvaluated(Span<ulong> bits, int i)
    {
        if (!bits.IsEmpty)
        {
            bits[i >> 6] |= 1UL << (i & 63);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsEvaluated(Span<ulong> bits, int i)
    {
        return (bits[i >> 6] & (1UL << (i & 63))) != 0;
    }

    private static bool EvalProperty<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[TMode.Collecting ? child.Node : child.FastNode];
        if (!TMode.Collecting)
        {
            if (target.AlwaysTrue)
            {
                return true;
            }

            if (target.AlwaysFalse)
            {
                return false;
            }

            if (target.IsLeaf)
            {
                return EvalLeafFast<TAccess>(target, doc, valueIndex, ref state);
            }

            if (target.IsSimpleArray)
            {
                return EvalSimpleArrayFast<TAccess>(target, doc, valueIndex, ref state);
            }

            return Eval<FastMode, TAccess>(target, doc, valueIndex, ref state, default, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, doc, valueIndex, -1), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
        bool ok = Eval<TMode, TAccess>(target, doc, valueIndex, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        return ok;
    }

    private static bool EvalPropertyName<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[TMode.Collecting ? child.Node : child.FastNode];
        if (!TMode.Collecting)
        {
            if (target.AlwaysTrue)
            {
                return true;
            }

            if (target.AlwaysFalse)
            {
                return false;
            }
        }

        using FixedStringJsonDocument<JsonElement> nameDoc = FixedStringJsonDocument<JsonElement>.Parse(doc.GetPropertyNameRaw(valueIndex, true), doc.ValueIsEscaped(valueIndex, true));
        if (!TMode.Collecting)
        {
            return Eval<FastMode, InterfaceAccess>(target, nameDoc, 0, ref state, default, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, null, -1, -1), Providers.EvalPath, Providers.SchemaPath, null);
        bool ok = Eval<TMode, InterfaceAccess>(target, nameDoc, 0, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        if (!ok)
        {
            collector.EvaluatedKeyword(false, null, "propertyNames"u8);
        }

        return ok;
    }

    private static bool EvalUnevaluatedProperties<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[node.UnevaluatedProperties.Node];
        bool ok = true;
        int propertyIndex = 0;
        int end = TAccess.EndIndex(ref state, doc, index);
        for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex) + RowSize)
        {
            if (evaluated.IsEmpty || !IsEvaluated(evaluated, propertyIndex))
            {
                MarkEvaluated(evaluated, propertyIndex);
                if (!TMode.Collecting && target.AlwaysFalse)
                {
                    return false;
                }

                if (!EvalProperty<TMode, TAccess>(node.UnevaluatedProperties, doc, valueIndex, ref state, seq))
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            propertyIndex++;
        }

        if (TMode.Collecting)
        {
            state.Collector!.EvaluatedKeyword(ok, null, "unevaluatedProperties"u8);
        }

        return ok;
    }

    // ---------------------------------------------------------------------------------------------
    // array
    // ---------------------------------------------------------------------------------------------

    private static bool EvalArray<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;
        int length = TAccess.Count(ref state, doc, index, JsonTokenType.StartArray);

        if (node.MinItems >= 0)
        {
            bool m = length >= node.MinItems;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "minItems"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.MaxItems >= 0)
        {
            bool m = length <= node.MaxItems;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "maxItems"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        ChildRef[]? prefixItems = node.PrefixItems;
        bool hasItems = node.Items.IsPresent;
        bool hasContains = node.Contains.IsPresent;
        bool unique = node.UniqueItems;

        if (prefixItems is null && !hasItems && !hasContains && !unique)
        {
            return ok;
        }

        // In flag mode, `items: false` (or true) needs no descent.
        SchemaNode? itemsNode = hasItems ? state.Nodes[TMode.Collecting ? node.Items.Node : node.Items.FastNode] : null;
        if (!TMode.Collecting && itemsNode is not null)
        {
            if (itemsNode.AlwaysFalse)
            {
                int prefixCount = prefixItems?.Length ?? 0;
                if (length > prefixCount)
                {
                    return false;
                }

                hasItems = false;
            }
            else if (itemsNode.AlwaysTrue)
            {
                hasItems = false;
                MarkAllEvaluated(evaluated, length);
            }
        }

        if (!hasItems && prefixItems is null && !hasContains && !unique)
        {
            return ok;
        }

        // Flag mode, items only, no tracking: tight loops for type-only / leaf item schemas.
        if (!TMode.Collecting && prefixItems is null && !hasContains && !unique && evaluated.IsEmpty && itemsNode is not null && (itemsNode.IsTypeOnly || itemsNode.IsLeaf))
        {
            int simpleEnd = TAccess.EndIndex(ref state, doc, index);
            if (itemsNode.IsTypeOnly)
            {
                TypeMask mask = itemsNode.Type;
                bool lexical = itemsNode.Dialect == JsonSchemaDialect.Draft4;
                for (int valueIndex = index + RowSize; valueIndex < simpleEnd; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
                {
                    if (!MatchesType<TAccess>(mask, TAccess.TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, lexical))
                    {
                        return false;
                    }
                }

                return ok;
            }

            for (int valueIndex = index + RowSize; valueIndex < simpleEnd; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
            {
                if (!EvalLeafFast<TAccess>(itemsNode, doc, valueIndex, ref state))
                {
                    return false;
                }
            }

            return ok;
        }

        // Small arrays: pairwise comparison beats hashing every element (object hashing transcodes every string).
        bool pairwise = unique && length <= SmallUniqueThreshold;
        bool useSet = unique && !pairwise;
        Span<int> buckets = useSet ? stackalloc int[UniqueItemsHashSet.StackAllocBucketSize] : default;
        Span<byte> entries = useSet ? stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize] : default;
        UniqueItemsHashSet set = useSet ? new UniqueItemsHashSet(doc, length, buckets, entries) : default;
        Span<int> seenIndices = pairwise ? stackalloc int[SmallUniqueThreshold] : default;
        bool isUnique = true;
        int containsCount = 0;
        int itemIndex = 0;

        try
        {
            int end = TAccess.EndIndex(ref state, doc, index);
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
            {

                if (prefixItems is not null && itemIndex < prefixItems.Length)
                {
                    MarkEvaluated(evaluated, itemIndex);
                    if (!EvalItem<TMode, TAccess>(prefixItems[itemIndex], doc, valueIndex, itemIndex, ref state, seq))
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }
                else if (hasItems)
                {
                    MarkEvaluated(evaluated, itemIndex);
                    if (!EvalItem<TMode, TAccess>(node.Items, doc, valueIndex, itemIndex, ref state, seq))
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }

                if (hasContains)
                {
                    if (EvalContainsItem<TMode, TAccess>(node.Contains, doc, valueIndex, itemIndex, ref state, seq))
                    {
                        containsCount++;
                        if (node.ContainsMarksEvaluated)
                        {
                            MarkEvaluated(evaluated, itemIndex);
                        }
                    }
                }

                if (unique && isUnique)
                {
                    bool duplicate = pairwise ? IsDuplicate<TAccess>(ref state, doc, valueIndex, seenIndices[..itemIndex]) : !set.AddItemIfNotExists(valueIndex);
                    if (pairwise)
                    {
                        seenIndices[itemIndex] = valueIndex;
                    }

                    if (duplicate)
                    {
                        isUnique = false;
                        if (!TMode.Collecting)
                        {
                            return false;
                        }
                    }
                }

                itemIndex++;
            }
        }
        finally
        {
            if (useSet)
            {
                set.Dispose();
            }
        }

        if (unique)
        {
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(isUnique, isUnique ? null : JsonSchemaEvaluation.ExpectedUniqueItems, "uniqueItems"u8);
            }

            ok &= isUnique;
        }

        if (hasContains)
        {
            bool m = containsCount >= node.MinContains && (node.MaxContains < 0 || containsCount <= node.MaxContains);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, null, "contains"u8);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        return ok;
    }

    private const int SmallUniqueThreshold = 8;

    /// <summary>
    /// Pairwise duplicate test for small arrays with cheap prefilters (token type, raw bytes for scalars).
    /// </summary>
    private static bool IsDuplicate<TAccess>(ref EvaluationState state, IJsonDocument doc, int valueIndex, scoped ReadOnlySpan<int> previous)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = TAccess.TokenType(ref state, doc, valueIndex);
        for (int i = 0; i < previous.Length; i++)
        {
            int other = previous[i];
            JsonTokenType otherType = TAccess.TokenType(ref state, doc, other);
            if (otherType != tokenType)
            {
                continue;
            }

            switch (tokenType)
            {
                case JsonTokenType.True:
                case JsonTokenType.False:
                case JsonTokenType.Null:
                    return true;
                case JsonTokenType.String:
                    if (TAccess.RawValue(ref state, doc, valueIndex).SequenceEqual(TAccess.RawValue(ref state, doc, other))
                        || (TAccess.IsEscaped(ref state, doc, valueIndex) | TAccess.IsEscaped(ref state, doc, other)) && JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, valueIndex, doc, other))
                    {
                        return true;
                    }

                    break;
                case JsonTokenType.Number:
                {
                    ReadOnlySpan<byte> a = TAccess.RawValue(ref state, doc, valueIndex);
                    ReadOnlySpan<byte> b = TAccess.RawValue(ref state, doc, other);
                    if (a.SequenceEqual(b))
                    {
                        return true;
                    }

                    // Two canonical integer literals with different text are different numbers.
                    if (IsCanonicalInteger(a) && IsCanonicalInteger(b))
                    {
                        break;
                    }

                    if (JsonElementHelpers.AreEqualJsonNumbers(a, b))
                    {
                        return true;
                    }

                    break;
                }
                default:
                    if (JsonElementHelpers.DeepEqualsNoParentDocumentCheck(doc, valueIndex, doc, other))
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsCanonicalInteger(ReadOnlySpan<byte> raw)
    {
        // Optional '-', no leading zero (except "0" itself), digits only.
        int start = raw.Length > 0 && raw[0] == (byte)'-' ? 1 : 0;
        if (raw.Length == start || raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') >= 0)
        {
            return false;
        }

        return !(raw[start] == (byte)'0' && raw.Length > start + 1) && raw != "-0"u8;
    }

    private static void MarkAllEvaluated(Span<ulong> bits, int count)
    {
        if (bits.IsEmpty)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            bits[i >> 6] |= 1UL << (i & 63);
        }
    }

    private static bool EvalItem<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, int itemIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[TMode.Collecting ? child.Node : child.FastNode];
        if (!TMode.Collecting)
        {
            if (target.AlwaysTrue)
            {
                return true;
            }

            if (target.AlwaysFalse)
            {
                return false;
            }

            if (target.IsLeaf)
            {
                return EvalLeafFast<TAccess>(target, doc, valueIndex, ref state);
            }

            if (target.IsSimpleArray)
            {
                return EvalSimpleArrayFast<TAccess>(target, doc, valueIndex, ref state);
            }

            return Eval<FastMode, TAccess>(target, doc, valueIndex, ref state, default, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, doc, -1, itemIndex), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
        bool ok = Eval<TMode, TAccess>(target, doc, valueIndex, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        return ok;
    }

    private static bool EvalContainsItem<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, int itemIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[TMode.Collecting ? child.Node : child.FastNode];
        if (!TMode.Collecting)
        {
            if (target.AlwaysTrue)
            {
                return true;
            }

            if (target.AlwaysFalse)
            {
                return false;
            }

            if (target.IsLeaf)
            {
                return EvalLeafFast<TAccess>(target, doc, valueIndex, ref state);
            }

            if (target.IsSimpleArray)
            {
                return EvalSimpleArrayFast<TAccess>(target, doc, valueIndex, ref state);
            }

            return Eval<FastMode, TAccess>(target, doc, valueIndex, ref state, default, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, doc, -1, itemIndex), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
        bool ok = Eval<TMode, TAccess>(target, doc, valueIndex, ref state, default, seq);
        if (ok)
        {
            collector.CommitChildContext(seq, true, true, JsonSchemaEvaluation.EvaluatedSubschema);
        }
        else
        {
            collector.PopChildContext(seq);
        }

        return ok;
    }

    private static bool EvalUnevaluatedItems<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[node.UnevaluatedItems.Node];
        bool ok = true;
        int itemIndex = 0;
        int end = TAccess.EndIndex(ref state, doc, index);
        for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = TAccess.NextIndex(ref state, doc, valueIndex))
        {
            if (evaluated.IsEmpty || !IsEvaluated(evaluated, itemIndex))
            {
                MarkEvaluated(evaluated, itemIndex);
                if (!TMode.Collecting && target.AlwaysFalse)
                {
                    return false;
                }

                if (!EvalItem<TMode, TAccess>(node.UnevaluatedItems, doc, valueIndex, itemIndex, ref state, seq))
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            itemIndex++;
        }

        if (TMode.Collecting)
        {
            state.Collector!.EvaluatedKeyword(ok, null, "unevaluatedItems"u8);
        }

        return ok;
    }

    // ---------------------------------------------------------------------------------------------
    // in-place applicators
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Evaluates a child schema against the same instance, merging its evaluated annotations into the
    /// parent's only when the child succeeds. Annotations never flow downwards.
    /// </summary>
    private static bool EvalInPlaceChild<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty || !CanMark<TAccess>(state.Nodes[TMode.Collecting ? child.Node : child.FastNode], ref state, doc, index))
        {
            // Nothing to merge: the child cannot mark properties/items of this instance.
            return EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, default, parentSeq, commitOnFailure: true);
        }

        if (parentBits.Length <= InlineBitWords)
        {
            Span<ulong> scratch = stackalloc ulong[InlineBitWords];
            scratch = scratch[..parentBits.Length];
            scratch.Clear();
            bool ok = EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, scratch, parentSeq, commitOnFailure: true);
            if (ok)
            {
                Merge(parentBits, scratch);
            }

            return ok;
        }

        ulong[] rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length);
        try
        {
            Span<ulong> scratch = rented.AsSpan(0, parentBits.Length);
            scratch.Clear();
            bool ok = EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, scratch, parentSeq, commitOnFailure: true);
            if (ok)
            {
                Merge(parentBits, scratch);
            }

            return ok;
        }
        finally
        {
            ArrayPool<ulong>.Shared.Return(rented);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanMark<TAccess>(SchemaNode target, ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        return TAccess.TokenType(ref state, doc, index) == JsonTokenType.StartObject ? target.MarksProperties : target.MarksItems;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Merge(Span<ulong> target, Span<ulong> source)
    {
        for (int i = 0; i < target.Length; i++)
        {
            target[i] |= source[i];
        }
    }

    private static bool EvalInPlaceCore<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> bits, int parentSeq, bool commitOnFailure)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        // Only in-place applicators can recurse without consuming the instance (whose depth the parser bounds),
        // so the runaway guard counts in-place nesting alone.
        if (++state.Depth > state.MaxDepth)
        {
            throw new JsonSchemaEvaluationException("Maximum in-place applicator depth exceeded; the schema recurses through $ref/allOf/anyOf/oneOf/not/if without consuming the instance.");
        }

        try
        {
            return EvalInPlaceCoreImpl<TMode, TAccess>(child, doc, index, ref state, bits, parentSeq, commitOnFailure);
        }
        finally
        {
            state.Depth--;
        }
    }

    private static bool EvalInPlaceCoreImpl<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> bits, int parentSeq, bool commitOnFailure)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[TMode.Collecting ? child.Node : child.FastNode];
        if (!TMode.Collecting)
        {
            return Eval<FastMode, TAccess>(target, doc, index, ref state, bits, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, null, -1, -1), Providers.EvalPath, Providers.SchemaPath, null);
        bool ok = Eval<TMode, TAccess>(target, doc, index, ref state, bits, seq);
        if (ok || commitOnFailure)
        {
            collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        }
        else
        {
            collector.PopChildContext(seq);
        }

        return ok;
    }

    private static bool EvalInPlace<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;
        SchemaNode[] nodes = state.Nodes;

        if (node.Ref.IsPresent)
        {
            bool m = EvalInPlaceChild<TMode, TAccess>(node.Ref, doc, index, ref state, evaluated, seq);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, node.Ref.Path);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.DynamicRef is DynamicRefTarget dynamicRef)
        {
            int targetNode = -1;
            int[] table = dynamicRef.NodeByResource;
            Span<int> scope = state.Scope[..state.ScopeDepth];
            for (int i = 0; i < scope.Length; i++)
            {
                int candidate = table[scope[i]];
                if (candidate >= 0)
                {
                    targetNode = candidate;
                    break;
                }
            }

            if (targetNode < 0)
            {
                targetNode = dynamicRef.FallbackNode;
            }

            var child = new ChildRef(targetNode, dynamicRef.PathSegment);
            bool m = EvalInPlaceChild<TMode, TAccess>(child, doc, index, ref state, evaluated, seq);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, dynamicRef.PathSegment);
            }

            if (!m)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.AllOf is ChildRef[] allOf)
        {
            bool all = true;
            for (int i = 0; i < allOf.Length; i++)
            {
                if (!EvalInPlaceChild<TMode, TAccess>(allOf[i], doc, index, ref state, evaluated, seq))
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    all = false;
                }
            }

            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(all, all ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, "allOf"u8);
            }

            ok &= all;
        }

        if (node.AnyOf is ChildRef[] anyOf)
        {
            bool any;
            if (!TMode.Collecting && node.AnyOfTypeUnion != TypeMask.None)
            {
                any = MatchesType<TAccess>(node.AnyOfTypeUnion, TAccess.TokenType(ref state, doc, index), ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4);
            }
            else if (!TMode.Collecting && node.AnyOfDiscriminator is Discriminator anyDiscriminator && TrySelectBranches<TAccess>(anyDiscriminator, ref state, doc, index, out int[] selected))
            {
                any = EvalAnyOfSelected<TAccess>(anyOf, selected, doc, index, ref state, evaluated);
            }
            else
            {
                any = EvalAnyOf<TMode, TAccess>(anyOf, doc, index, ref state, evaluated, seq);
            }

            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(any, any ? JsonSchemaEvaluation.MatchedAtLeastOneSchema : JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema, "anyOf"u8);
            }

            if (!any)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.OneOf is ChildRef[] oneOf)
        {
            int matched;
            if (!TMode.Collecting && node.OneOfTypeUnion != TypeMask.None)
            {
                matched = MatchesType<TAccess>(node.OneOfTypeUnion, TAccess.TokenType(ref state, doc, index), ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4) ? 1 : 0;
            }
            else if (!TMode.Collecting && node.OneOfDiscriminator is Discriminator oneDiscriminator && TrySelectBranches<TAccess>(oneDiscriminator, ref state, doc, index, out int[] selected))
            {
                matched = EvalOneOfSelected<TAccess>(oneOf, selected, doc, index, ref state, evaluated);
            }
            else
            {
                matched = EvalOneOf<TMode, TAccess>(oneOf, doc, index, ref state, evaluated, seq);
            }

            bool one = matched == 1;
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(one, matched == 0 ? JsonSchemaEvaluation.MatchedNoSchema : one ? JsonSchemaEvaluation.MatchedExactlyOneSchema : JsonSchemaEvaluation.MatchedMoreThanOneSchema, "oneOf"u8);
            }

            if (!one)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.Not.IsPresent)
        {
            SchemaNode target = nodes[node.Not.Node];
            bool inner;
            if (!TMode.Collecting)
            {
                inner = Eval<FastMode, TAccess>(target, doc, index, ref state, default, 0);
            }
            else
            {
                IJsonSchemaResultsCollector collector = state.Collector!;
                int childSeq = collector.BeginChildContext(seq, new EdgeContext(node.Not.Path, target.SchemaLocation, null, -1, -1), Providers.EvalPath, Providers.SchemaPath, null);
                inner = Eval<TMode, TAccess>(target, doc, index, ref state, default, childSeq);

                // Results (and therefore annotations) produced beneath `not` are always discarded.
                collector.PopChildContext(childSeq);
                collector.EvaluatedKeyword(!inner, inner ? JsonSchemaEvaluation.MatchedNotSchema : JsonSchemaEvaluation.DidNotMatchNotSchema, "not"u8);
            }

            if (inner)
            {
                if (!TMode.Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.If.IsPresent)
        {
            bool condition = EvalIf<TMode, TAccess>(node.If, doc, index, ref state, evaluated, seq);
            if (condition)
            {
                if (node.Then.IsPresent)
                {
                    bool m = EvalInPlaceChild<TMode, TAccess>(node.Then, doc, index, ref state, evaluated, seq);
                    if (TMode.Collecting)
                    {
                        state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedThen : JsonSchemaEvaluation.DidNotMatchThen, "then"u8);
                    }

                    if (!m)
                    {
                        if (!TMode.Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }
            }
            else if (node.Else.IsPresent)
            {
                bool m = EvalInPlaceChild<TMode, TAccess>(node.Else, doc, index, ref state, evaluated, seq);
                if (TMode.Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedElse : JsonSchemaEvaluation.DidNotMatchElse, "else"u8);
                }

                if (!m)
                {
                    if (!TMode.Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }
        }

        return ok;
    }

    private static bool EvalIf<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty || !CanMark<TAccess>(state.Nodes[TMode.Collecting ? child.Node : child.FastNode], ref state, doc, index))
        {
            bool r = EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, default, parentSeq, commitOnFailure: false);
            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(true, r ? JsonSchemaEvaluation.MatchedIfForThen : JsonSchemaEvaluation.MatchedIfForElse, "if"u8);
            }

            return r;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        scratch = scratch[..parentBits.Length];
        scratch.Clear();
        try
        {
            bool r = EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, scratch, parentSeq, commitOnFailure: false);
            if (r)
            {
                Merge(parentBits, scratch);
            }

            if (TMode.Collecting)
            {
                state.Collector!.EvaluatedKeyword(true, r ? JsonSchemaEvaluation.MatchedIfForThen : JsonSchemaEvaluation.MatchedIfForElse, "if"u8);
            }

            return r;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Selects the candidate branches for an object instance from its discriminator property.
    /// </summary>
    private static bool TrySelectBranches<TAccess>(Discriminator discriminator, ref EvaluationState state, IJsonDocument doc, int index, out int[] selected)
        where TAccess : struct, IDocumentAccess
    {
        if (TAccess.TokenType(ref state, doc, index) != JsonTokenType.StartObject)
        {
            selected = [];
            return false;
        }

        if (!doc.TryGetNamedPropertyValue(index, discriminator.PropertyName, out IJsonDocument? valueDoc, out int valueIndex))
        {
            // Every branch requires the property: none can match.
            selected = [];
            return discriminator.AllRequire;
        }

        bool sameDoc = ReferenceEquals(valueDoc, doc);
        JsonTokenType valueType = sameDoc ? TAccess.TokenType(ref state, doc, valueIndex) : valueDoc.GetJsonTokenType(valueIndex);
        if (valueType != JsonTokenType.String)
        {
            selected = discriminator.NonString;
            return true;
        }

        using UnescapedUtf8JsonString value = sameDoc ? StringValue<TAccess>(ref state, doc, valueIndex) : valueDoc.GetUtf8JsonString(valueIndex, JsonTokenType.String);
        if (discriminator.KnownValues.TryGetValue(value.Span, out int[]? branches))
        {
            selected = branches;
        }
        else
        {
            selected = discriminator.UnknownString;
        }

        return true;
    }

    private static bool EvalAnyOfSelected<TAccess>(ChildRef[] branches, int[] selected, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits)
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty)
        {
            for (int i = 0; i < selected.Length; i++)
            {
                if (EvalInPlaceCore<FastMode, TAccess>(branches[selected[i]], doc, index, ref state, default, 0, commitOnFailure: false))
                {
                    return true;
                }
            }

            return false;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        scratch = scratch[..parentBits.Length];
        try
        {
            bool any = false;
            for (int i = 0; i < selected.Length; i++)
            {
                scratch.Clear();
                if (EvalInPlaceCore<FastMode, TAccess>(branches[selected[i]], doc, index, ref state, scratch, 0, commitOnFailure: false))
                {
                    any = true;
                    Merge(parentBits, scratch);
                }
            }

            return any;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }
        }
    }

    private static int EvalOneOfSelected<TAccess>(ChildRef[] branches, int[] selected, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits)
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty)
        {
            int matched = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                if (EvalInPlaceCore<FastMode, TAccess>(branches[selected[i]], doc, index, ref state, default, 0, commitOnFailure: false))
                {
                    matched++;
                    if (matched > 1)
                    {
                        return matched;
                    }
                }
            }

            return matched;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        scratch = scratch[..parentBits.Length];
        ulong[]? rentedWinner = null;
        Span<ulong> winner = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rentedWinner = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        winner = winner[..parentBits.Length];
        try
        {
            int matched = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                scratch.Clear();
                if (EvalInPlaceCore<FastMode, TAccess>(branches[selected[i]], doc, index, ref state, scratch, 0, commitOnFailure: false))
                {
                    matched++;
                    if (matched == 1)
                    {
                        scratch.CopyTo(winner);
                    }
                    else
                    {
                        return matched;
                    }
                }
            }

            if (matched == 1)
            {
                Merge(parentBits, winner);
            }

            return matched;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }

            if (rentedWinner is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedWinner);
            }
        }
    }

    private static bool EvalAnyOf<TMode, TAccess>(ChildRef[] branches, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty)
        {
            bool any = false;
            for (int i = 0; i < branches.Length; i++)
            {
                if (EvalInPlaceCore<TMode, TAccess>(branches[i], doc, index, ref state, default, parentSeq, commitOnFailure: false))
                {
                    any = true;
                    if (!TMode.Collecting)
                    {
                        return true;
                    }
                }
            }

            return any;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        scratch = scratch[..parentBits.Length];
        try
        {
            bool any = false;
            for (int i = 0; i < branches.Length; i++)
            {
                scratch.Clear();
                if (EvalInPlaceCore<TMode, TAccess>(branches[i], doc, index, ref state, scratch, parentSeq, commitOnFailure: false))
                {
                    any = true;
                    Merge(parentBits, scratch);
                }
            }

            return any;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }
        }
    }

    private static int EvalOneOf<TMode, TAccess>(ChildRef[] branches, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> parentBits, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if (parentBits.IsEmpty)
        {
            int matched = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                if (EvalInPlaceCore<TMode, TAccess>(branches[i], doc, index, ref state, default, parentSeq, commitOnFailure: false))
                {
                    matched++;
                    if (!TMode.Collecting && matched > 1)
                    {
                        return matched;
                    }
                }
            }

            return matched;
        }

        ulong[]? rented = null;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        scratch = scratch[..parentBits.Length];
        ulong[]? rentedWinner = null;
        Span<ulong> winner = parentBits.Length <= InlineBitWords ? stackalloc ulong[InlineBitWords] : (rentedWinner = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
        winner = winner[..parentBits.Length];
        try
        {
            int matched = 0;
            for (int i = 0; i < branches.Length; i++)
            {
                scratch.Clear();
                if (EvalInPlaceCore<TMode, TAccess>(branches[i], doc, index, ref state, scratch, parentSeq, commitOnFailure: false))
                {
                    matched++;
                    if (matched == 1)
                    {
                        scratch.CopyTo(winner);
                    }
                    else if (!TMode.Collecting)
                    {
                        return matched;
                    }
                }
            }

            if (matched == 1)
            {
                Merge(parentBits, winner);
            }

            return matched;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<ulong>.Shared.Return(rented);
            }

            if (rentedWinner is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedWinner);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // messages
    // ---------------------------------------------------------------------------------------------

    private static JsonSchemaMessageProvider? ExpectedTypeProvider(TypeMask mask)
    {
        return mask switch
        {
            TypeMask.String => JsonSchemaEvaluation.ExpectedTypeString,
            TypeMask.Object => JsonSchemaEvaluation.ExpectedTypeObject,
            TypeMask.Array => JsonSchemaEvaluation.ExpectedTypeArray,
            TypeMask.Number => JsonSchemaEvaluation.ExpectedTypeNumber,
            TypeMask.Integer => JsonSchemaEvaluation.ExpectedTypeInteger,
            TypeMask.Boolean => JsonSchemaEvaluation.ExpectedTypeBoolean,
            TypeMask.Null => JsonSchemaEvaluation.ExpectedTypeNull,
            _ => null,
        };
    }
}
