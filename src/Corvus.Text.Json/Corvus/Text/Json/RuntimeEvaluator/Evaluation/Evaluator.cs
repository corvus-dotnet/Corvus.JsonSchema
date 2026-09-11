// <copyright file="Evaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// The evaluation engine.
/// </summary>
[SkipLocalsInit]
internal static partial class Evaluator
{
    internal const int InlineBitWords = SchemaNode.InlineBitWords;

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
            EntryResource = nodes[rootNode].ResourceId,
        };

        SchemaNode root = nodes[rootNode];
        bool raw = document is JsonDocument jsonDocument && jsonDocument.TryGetRawAccess(out state.Raw);
        if (raw)
        {
            state.RawRows = state.Raw.Rows;
            state.RawUtf8 = state.Raw.Utf8.Span;
        }

        // A root that is nothing but a $ref reports against its target, as a generated model rooted at a reduced
        // type does; the root context carries the target's schema location. Flag mode starts there too.
        if (root.ElidedTarget >= 0)
        {
            root = nodes[root.ElidedTarget];
        }

        if (collector is null && !state.UsesDynamicScope)
        {
            // The scope never grows without a dynamic scope, so there is nothing to return: dispatch straight to
            // the root's plan without the try/finally.
            return raw
                ? EvalChildFast<RawAccess>(root, document, index, ref state)
                : EvalChildFast<InterfaceAccess>(root, document, index, ref state);
        }

        try
        {
            if (collector is null)
            {
                return raw
                    ? Eval<FastMode, RawAccess>(root, document, index, ref state, default, 0)
                    : Eval<FastMode, InterfaceAccess>(root, document, index, ref state, default, 0);
            }

            int seq = collector.BeginChildContext(0, new EdgeContext(null, root.SchemaLocation, null, -1, -1), null, Providers.SchemaPath, null);
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
        return default(TAccess).GetString(ref state, doc, index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static UnescapedUtf8JsonString PropertyName<TAccess>(ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        return default(TAccess).GetPropertyName(ref state, doc, valueIndex);
    }

    // ---------------------------------------------------------------------------------------------
    // Node evaluation
    // ---------------------------------------------------------------------------------------------
    private static bool Eval<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        NodeFlags flags = node.Flags;
        if ((flags & NodeFlags.AlwaysBoolean) != 0)
        {
            bool value = (flags & NodeFlags.AlwaysTrue) != 0;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedBooleanSchema(value, null);
            }

            return value;
        }

        bool pushedScope = EnterScope(node, ref state);

        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, index);

        bool result;
        if (!default(TMode).Collecting && evaluated.IsEmpty && node.Plan == NodePlan.FusedObject && tokenType == JsonTokenType.StartObject)
        {
            result = EvalFusedObject<TAccess>(node, doc, index, ref state);
            if (pushedScope)
            {
                state.ScopeDepth--;
            }

            return result;
        }

        if (evaluated.IsEmpty && (tokenType == JsonTokenType.StartObject ? (flags & NodeFlags.TracksProperties) != 0 : (tokenType == JsonTokenType.StartArray && (flags & NodeFlags.TracksItems) != 0)))
        {
            int count = default(TAccess).Count(ref state, doc, index, tokenType);
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
        NodeFlags flags = node.Flags;

        if ((flags & NodeFlags.ValueKeywords) != 0 && !EvalValueKeywords<TMode, TAccess>(node, flags, doc, index, tokenType, ref state, ref ok))
        {
            return false;
        }

        switch (tokenType)
        {
            case JsonTokenType.Number:
                if ((flags & NodeFlags.HasNumberKeywords) != 0)
                {
                    ok &= EvalNumber<TMode, TAccess>(node, doc, index, ref state);
                }

                break;
            case JsonTokenType.String:
                if ((flags & NodeFlags.HasStringKeywords) != 0)
                {
                    ok &= EvalString<TMode, TAccess>(node, doc, index, ref state);
                }

                break;
            case JsonTokenType.StartObject:
                if ((flags & NodeFlags.HasObjectKeywords) != 0)
                {
                    ok &= EvalObject<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
                }

                break;
            case JsonTokenType.StartArray:
                if ((flags & NodeFlags.HasArrayKeywords) != 0)
                {
                    ok &= EvalArray<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
                }

                break;
        }

        if (!ok && !default(TMode).Collecting)
        {
            return false;
        }

        if ((flags & NodeFlags.HasInPlaceApplicators) != 0)
        {
            ok &= EvalInPlace<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !default(TMode).Collecting)
            {
                return false;
            }
        }

        if (tokenType == JsonTokenType.StartObject && (flags & NodeFlags.HasUnevaluatedProperties) != 0)
        {
            ok &= EvalUnevaluatedProperties<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !default(TMode).Collecting)
            {
                return false;
            }
        }
        else if (tokenType == JsonTokenType.StartArray && (flags & NodeFlags.HasUnevaluatedItems) != 0)
        {
            ok &= EvalUnevaluatedItems<TMode, TAccess>(node, doc, index, ref state, evaluated, seq);
            if (!ok && !default(TMode).Collecting)
            {
                return false;
            }
        }

        if (default(TMode).Collecting && (flags & NodeFlags.HasAnnotations) != 0)
        {
            foreach (AnnotationEntry a in node.Annotations!)
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

    /// <summary>
    /// The type/const/enum assertions, split out so that nodes without them (the common case) do not pay for the
    /// three tests and the collector calls at all. Returns <see langword="false"/> to fail fast in flag mode.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalValueKeywords<TMode, TAccess>(SchemaNode node, NodeFlags flags, IJsonDocument doc, int index, JsonTokenType tokenType, ref EvaluationState state, ref bool ok)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        if ((flags & NodeFlags.HasType) != 0)
        {
            bool match = MatchesType<TAccess>(node.Type, tokenType, ref state, doc, index, (flags & NodeFlags.Draft4) != 0);
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(match, node.TypeMessage, "type"u8);
            }

            if (!match)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if ((flags & NodeFlags.HasConst) != 0)
        {
            bool match = MatchesConst<TAccess>(node, ref state, doc, index, tokenType);
            if (default(TMode).Collecting)
            {
                ReportConst(node, match, ref state);
            }

            if (!match)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if ((flags & NodeFlags.HasEnum) != 0)
        {
            bool match = MatchesEnum<TAccess>(node, ref state, doc, index, tokenType);
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(match, match ? JsonSchemaEvaluation.MatchedAtLeastOneConstantValue : JsonSchemaEvaluation.DidNotMatchAtLeastOneConstantValue, "enum"u8);
            }

            if (!match)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        return true;
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
        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, index);
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
    /// <summary>
    /// Pushes the node's resource onto the dynamic scope when it differs from the innermost one; the caller pops
    /// it again when this returns <see langword="true"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EnterScope(SchemaNode node, ref EvaluationState state)
    {
        if (state.UsesDynamicScope && (state.ScopeDepth == 0 || state.Scope[state.ScopeDepth - 1] != node.ResourceId))
        {
            state.PushScope(node.ResourceId);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Flag-mode entry for a child schema of a consuming keyword (or an in-place child with no live bitset):
    /// dispatches once on the node's compile-time plan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalChildFast<TAccess>(SchemaNode target, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        switch (target.Plan)
        {
            case NodePlan.AlwaysTrue:
                return true;
            case NodePlan.AlwaysFalse:
                return false;
            case NodePlan.Leaf:
                return EvalLeafFast<TAccess>(target, doc, index, ref state);
            case NodePlan.SimpleArray:
                return EvalSimpleArrayFast<TAccess>(target, doc, index, ref state);
            case NodePlan.Object:
            case NodePlan.StrictObject:
                return EvalObjectPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.ArrayItems:
                return EvalArrayItemsPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.DynamicRef:
                return EvalDynamicRefPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.Forward:
                return EvalChildFast<TAccess>(state.Nodes[target.ForwardNode], doc, index, ref state);
            case NodePlan.FusedObject:
                return default(TAccess).TokenType(ref state, doc, index) == JsonTokenType.StartObject
                    ? EvalFusedObject<TAccess>(target, doc, index, ref state)
                    : Eval<FastMode, TAccess>(target, doc, index, ref state, default, 0);
            default:
                return Eval<FastMode, TAccess>(target, doc, index, ref state, default, 0);
        }
    }

    /// <summary>
    /// <see cref="NodePlan.FusedObject"/>: one pass over the properties applying every branch's resolution for each
    /// name, a second step for the branches whose <c>if</c> is decided by the properties seen, then the required
    /// names and the unevaluated check. See <see cref="FusedObject"/>.
    /// </summary>
    private static bool EvalFusedObject<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        FusedObject f = node.Fused!;
        SchemaNode[] nodes = state.Nodes;
        FusedContributor[] contributors = f.Contributors;
        int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);

        if (f.HasCountBounds)
        {
            for (int c = 0; c < contributors.Length; c++)
            {
                FusedContributor contributor = contributors[c];
                if (contributor.Condition < 0 && ((contributor.MinProperties >= 0 && count < contributor.MinProperties) || (contributor.MaxProperties >= 0 && count > contributor.MaxProperties)))
                {
                    return false;
                }
            }
        }

        int seenWords = (f.EntryList.Length + 63) >> 6;
        Span<ulong> seen = stackalloc ulong[InlineBitWords];
        seen = seen[..seenWords];
        seen.Clear();

        bool trackCoverage = f.Unevaluated.IsPresent;
        int coverWords = trackCoverage ? (count + 63) >> 6 : 0;
        ulong[]? rentedCover = coverWords > InlineBitWords ? ArrayPool<ulong>.Shared.Rent(coverWords) : null;
        Span<ulong> coverInline = stackalloc ulong[InlineBitWords];
        Span<ulong> covered = rentedCover is null ? coverInline[..coverWords] : rentedCover.AsSpan(0, coverWords);
        covered.Clear();

        Span<bool> failed = stackalloc bool[64];
        failed.Clear();
        Span<int> deferredInline = stackalloc int[3 * 16];
        Span<int> deferred = deferredInline;
        int[]? rentedDeferred = null;
        int deferredCount = 0;
        try
        {
            int ordinal = 0;
            int end = default(TAccess).EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize, ordinal++)
            {
                ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRaw(ref state, doc, valueIndex, out bool escaped);
                bool cover;
                bool defer;
                int entryIndex;
                if (!escaped)
                {
                    if (!ApplyFusedName<TAccess>(f, raw, doc, valueIndex, ref state, seen, failed, out cover, out defer, out entryIndex))
                    {
                        return false;
                    }
                }
                else
                {
                    using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                    if (!ApplyFusedName<TAccess>(f, name.Span, doc, valueIndex, ref state, seen, failed, out cover, out defer, out entryIndex))
                    {
                        return false;
                    }
                }

                if (cover && trackCoverage)
                {
                    MarkEvaluated(covered, ordinal);
                }

                if (defer)
                {
                    if ((deferredCount * 3) + 3 > deferred.Length)
                    {
                        int[] grown = ArrayPool<int>.Shared.Rent(deferred.Length * 2);
                        deferred.CopyTo(grown);
                        if (rentedDeferred is not null)
                        {
                            ArrayPool<int>.Shared.Return(rentedDeferred);
                        }

                        rentedDeferred = grown;
                        deferred = grown;
                    }

                    deferred[deferredCount * 3] = ordinal;
                    deferred[(deferredCount * 3) + 1] = valueIndex;
                    deferred[(deferredCount * 3) + 2] = entryIndex;
                    deferredCount++;
                }
            }

            Span<bool> holds = stackalloc bool[64];
            FusedCondition[] conditions = f.Conditions;
            for (int i = 0; i < conditions.Length; i++)
            {
                bool all = !failed[i];
                int[] bits = conditions[i].RequiredBits;
                for (int b = 0; b < bits.Length && all; b++)
                {
                    all = (seen[bits[b] >> 6] & (1UL << (bits[b] & 63))) != 0;
                }

                holds[i] = all;
            }

            // A condition under another applies only along the chain that reaches it.
            Span<bool> gateOk = stackalloc bool[64];
            for (int i = 0; i < conditions.Length; i++)
            {
                int gate = conditions[i].Gate;
                gateOk[i] = gate < 0 || (gateOk[gate] && holds[gate] == conditions[i].GatePolarity);
            }

            for (int d = 0; d < deferredCount; d++)
            {
                int ordinal2 = deferred[d * 3];
                int valueIndex = deferred[(d * 3) + 1];
                int entryIndex = deferred[(d * 3) + 2];
                bool cover = false;
                if (entryIndex >= 0)
                {
                    FusedApplication[] applications = f.EntryList[entryIndex].Applications;
                    for (int a = 0; a < applications.Length; a++)
                    {
                        FusedApplication app = applications[a];
                        FusedContributor contributor = contributors[app.Contributor];
                        if (contributor.Condition >= 0 && gateOk[contributor.Condition] && holds[contributor.Condition] == contributor.Polarity)
                        {
                            if (!ApplyApplication<TAccess>(app, nodes, doc, valueIndex, ref state))
                            {
                                return false;
                            }

                            cover = true;
                        }
                    }
                }
                else
                {
                    using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                    for (int c = 0; c < contributors.Length; c++)
                    {
                        FusedContributor contributor = contributors[c];
                        if (contributor.Condition >= 0 && gateOk[contributor.Condition] && holds[contributor.Condition] == contributor.Polarity)
                        {
                            if (!ResolveUnknownName<TAccess>(contributor, name.Span, nodes, doc, valueIndex, ref state, out bool matched))
                            {
                                return false;
                            }

                            cover |= matched;
                        }
                    }
                }

                if (cover && trackCoverage)
                {
                    MarkEvaluated(covered, ordinal2);
                }
            }

            for (int c = 0; c < contributors.Length; c++)
            {
                FusedContributor contributor = contributors[c];
                if (contributor.Condition >= 0)
                {
                    if (!gateOk[contributor.Condition] || holds[contributor.Condition] != contributor.Polarity)
                    {
                        continue;
                    }

                    if ((contributor.MinProperties >= 0 && count < contributor.MinProperties) || (contributor.MaxProperties >= 0 && count > contributor.MaxProperties))
                    {
                        return false;
                    }
                }

                int[] required = contributor.RequiredBits;
                for (int b = 0; b < required.Length; b++)
                {
                    if ((seen[required[b] >> 6] & (1UL << (required[b] & 63))) == 0)
                    {
                        return false;
                    }
                }
            }

            FusedAlternative[] alternatives = f.Alternatives;
            for (int a = 0; a < alternatives.Length; a++)
            {
                FusedAlternative alternative = alternatives[a];
                if (alternative.Condition >= 0 && (!gateOk[alternative.Condition] || holds[alternative.Condition] != alternative.Polarity))
                {
                    continue;
                }

                int matches = 0;
                int[][] branches = alternative.Branches;
                for (int b = 0; b < branches.Length; b++)
                {
                    bool all = true;
                    int[] bits = branches[b];
                    for (int i = 0; i < bits.Length && all; i++)
                    {
                        all = (seen[bits[i] >> 6] & (1UL << (bits[i] & 63))) != 0;
                    }

                    if (all)
                    {
                        matches++;
                    }
                }

                if (matches == 0 || (alternative.ExactlyOne && matches != 1))
                {
                    return false;
                }
            }

            if (trackCoverage)
            {
                SchemaNode target = nodes[f.Unevaluated.FastNode];
                int ordinal2 = 0;
                for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize, ordinal2++)
                {
                    if (!IsEvaluated(covered, ordinal2))
                    {
                        if (target.AlwaysFalse || (!target.AlwaysTrue && !EvalChildFast<TAccess>(target, doc, valueIndex, ref state)))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }
        finally
        {
            if (rentedCover is not null)
            {
                ArrayPool<ulong>.Shared.Return(rentedCover);
            }

            if (rentedDeferred is not null)
            {
                ArrayPool<int>.Shared.Return(rentedDeferred);
            }
        }
    }

    /// <summary>Applies one fused resolution to a property value: nothing for <c>true</c>, a token-type test for a type-only leaf, else the child's plan.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyApplication<TAccess>(in FusedApplication app, SchemaNode[] nodes, IJsonDocument doc, int valueIndex, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (app.Node < 0)
        {
            return true;
        }

        TypeMask inline = app.InlineType;
        return inline != TypeMask.None
            ? MatchesType<TAccess>(inline, default(TAccess).TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, app.InlineLexical)
            : EvalChildFast<TAccess>(nodes[app.Node], doc, valueIndex, ref state);
    }

    /// <summary>
    /// The per-property step of <see cref="EvalFusedObject{TAccess}"/>: applies every unconditional resolution of the
    /// name now and notes whether a conditional one is pending. Returns <see langword="false"/> when a child failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyFusedName<TAccess>(FusedObject f, scoped ReadOnlySpan<byte> nameSpan, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<ulong> seen, scoped Span<bool> failed, out bool cover, out bool defer, out int entryIndex)
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode[] nodes = state.Nodes;
        FusedContributor[] contributors = f.Contributors;
        cover = false;
        defer = false;
        entryIndex = -1;
        if (f.Entries.TryGetValue(nameSpan, out FusedEntry? entry))
        {
            seen[entry.Index >> 6] |= 1UL << (entry.Index & 63);
            entryIndex = entry.Index;
            if (entry.ValueTests.Length > 0)
            {
                ApplyValueTests<TAccess>(entry.ValueTests, ref state, doc, valueIndex, failed);
            }

            FusedApplication[] applications = entry.Applications;
            for (int a = 0; a < applications.Length; a++)
            {
                FusedApplication app = applications[a];
                if (contributors[app.Contributor].Condition < 0)
                {
                    if (!ApplyApplication<TAccess>(app, nodes, doc, valueIndex, ref state))
                    {
                        return false;
                    }

                    cover = true;
                }
                else
                {
                    defer = true;
                }
            }
        }
        else if (f.ResolvesUnknownNames)
        {
            for (int c = 0; c < contributors.Length; c++)
            {
                FusedContributor contributor = contributors[c];
                if (contributor.Condition >= 0)
                {
                    defer |= contributor.Patterns is not null || contributor.AdditionalNode >= 0 || contributor.AdditionalCoversOnly;
                    continue;
                }

                if (!ResolveUnknownName<TAccess>(contributor, nameSpan, nodes, doc, valueIndex, ref state, out bool matched))
                {
                    return false;
                }

                cover |= matched;
            }
        }

        return true;
    }

    /// <summary>
    /// Checks a property's value against the conditions that test it: the value is keyed like a discriminator value
    /// (tagged string, canonical integer or boolean) and each condition whose allowed set lacks it is marked failed.
    /// A value of any other kind fails every test, since only keyable constants are accepted at compile time.
    /// </summary>
    private static void ApplyValueTests<TAccess>(FusedValueTest[] tests, ref EvaluationState state, IJsonDocument doc, int valueIndex, scoped Span<bool> failed)
        where TAccess : struct, IDocumentAccess
    {
        Span<byte> buffer = stackalloc byte[128];
        switch (default(TAccess).TokenType(ref state, doc, valueIndex))
        {
            case JsonTokenType.String:
            {
                using UnescapedUtf8JsonString text = StringValue<TAccess>(ref state, doc, valueIndex);
                TestValueKey(tests, Discriminator.StringTag, text.Span, buffer, failed);
                return;
            }

            case JsonTokenType.True:
                TestValueKey(tests, Discriminator.BooleanTag, "true"u8, buffer, failed);
                return;
            case JsonTokenType.False:
                TestValueKey(tests, Discriminator.BooleanTag, "false"u8, buffer, failed);
                return;
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, valueIndex);
                if (IsCanonicalInteger(raw))
                {
                    TestValueKey(tests, Discriminator.NumberTag, raw, buffer, failed);
                    return;
                }

                // "2.0" may equal a keyed integer: test it the slow way, by value.
                for (int t = 0; t < tests.Length; t++)
                {
                    bool passes = tests[t].Pattern is not null ? !tests[t].RequiresString : NumberInKeys(raw, tests[t].Allowed!);
                    if (!passes)
                    {
                        failed[tests[t].Condition] = true;
                    }
                }

                return;
            }

            default:
                // A pattern does not apply to a non-string; a key set has nothing that matches a structured value or null.
                for (int t = 0; t < tests.Length; t++)
                {
                    if (tests[t].Pattern is null || tests[t].RequiresString)
                    {
                        failed[tests[t].Condition] = true;
                    }
                }

                return;
        }
    }

    private static void TestValueKey(FusedValueTest[] tests, byte tag, ReadOnlySpan<byte> value, Span<byte> buffer, Span<bool> failed)
    {
        byte[]? rented = value.Length + 1 > buffer.Length ? ArrayPool<byte>.Shared.Rent(value.Length + 1) : null;
        Span<byte> key = rented is null ? buffer[..(value.Length + 1)] : rented.AsSpan(0, value.Length + 1);
        key[0] = tag;
        value.CopyTo(key[1..]);
        for (int t = 0; t < tests.Length; t++)
        {
            FusedValueTest test = tests[t];
            bool passes = test.Pattern is PatternMatcher pattern
                ? (tag == Discriminator.StringTag ? pattern.IsMatch(value) : !test.RequiresString)
                : test.Allowed!.TryGetValue(key, out _);
            if (!passes)
            {
                failed[test.Condition] = true;
            }
        }

        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Whether a non-canonical number equals any integer key of the set.</summary>
    private static bool NumberInKeys(ReadOnlySpan<byte> raw, Utf8NameMap<object> allowed)
    {
        foreach (byte[] key in allowed.Keys)
        {
            if (key.Length > 1 && key[0] == Discriminator.NumberTag && JsonElementHelpers.AreEqualJsonNumbers(raw, key.AsSpan(1)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Applies a branch's pattern properties, or its additional properties when none matched, to a name no entry knows.</summary>
    private static bool ResolveUnknownName<TAccess>(FusedContributor contributor, scoped ReadOnlySpan<byte> name, SchemaNode[] nodes, IJsonDocument doc, int valueIndex, ref EvaluationState state, out bool covered)
        where TAccess : struct, IDocumentAccess
    {
        covered = false;
        if (contributor.Patterns is PatternPropertyEntry[] patterns)
        {
            for (int p = 0; p < patterns.Length; p++)
            {
                PatternPropertyEntry pattern = patterns[p];
                if (pattern.Matcher.IsMatch(name))
                {
                    covered = true;
                    SchemaNode target = nodes[pattern.Schema.FastNode];
                    if (!target.AlwaysTrue && !EvalChildFast<TAccess>(target, doc, valueIndex, ref state))
                    {
                        return false;
                    }
                }
            }
        }

        if (!covered)
        {
            if (contributor.AdditionalNode >= 0)
            {
                covered = true;
                return EvalChildFast<TAccess>(nodes[contributor.AdditionalNode], doc, valueIndex, ref state);
            }

            covered = contributor.AdditionalCoversOnly;
        }

        return true;
    }

    /// <summary>
    /// <see cref="NodePlan.Object"/>: optional type test, then properties/additionalProperties/required/count bounds
    /// in one loop, with children dispatched through <see cref="EvalChildFast{TAccess}"/>.
    /// </summary>
    private static bool EvalObjectPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, index);
        if (tokenType != JsonTokenType.StartObject)
        {
            return !node.HasType || MatchesType<TAccess>(node.Type, tokenType, ref state, doc, index, (node.Flags & NodeFlags.Draft4) != 0);
        }

        if (node.HasType && (node.Type & TypeMask.Object) == 0)
        {
            return false;
        }

        bool pushed = EnterScope(node, ref state);
        bool ok = node.Plan == NodePlan.StrictObject
            ? EvalStrictObjectCore<TAccess>(node, doc, index, ref state)
            : EvalObjectPlanCore<TAccess>(node, doc, index, ref state);
        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    /// <summary>
    /// <see cref="NodePlan.StrictObject"/>: a name lookup and a token-type test per property (a call only for children
    /// that are not type-only leaves), unknown names rejected when additionalProperties is false, and
    /// <c>required</c> one mask test at the end.
    /// </summary>
    private static bool EvalStrictObjectCore<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);
            if ((node.MinProperties >= 0 && count < node.MinProperties) || (node.MaxProperties >= 0 && count > node.MaxProperties))
            {
                return false;
            }
        }

        Utf8NameMap<PropertyEntry> properties = node.Properties!;
        bool rejectUnknown = node.AdditionalProperties.IsPresent && state.Nodes[node.AdditionalProperties.FastNode].AlwaysFalse;
        ulong seen = 0;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
        {
            ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRaw(ref state, doc, valueIndex, out bool escaped);
            PropertyEntry? entry;
            if (!escaped)
            {
                properties.TryGetValue(raw, out entry);
            }
            else
            {
                using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                properties.TryGetValue(name.Span, out entry);
            }

            if (entry is null)
            {
                if (rejectUnknown)
                {
                    return false;
                }

                continue;
            }

            if (entry.SeenBit >= 0)
            {
                seen |= 1UL << entry.SeenBit;
            }

            TypeMask mask = entry.InlineType;
            if (mask != TypeMask.None)
            {
                if (!MatchesType<TAccess>(mask, default(TAccess).TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, entry.InlineLexical))
                {
                    return false;
                }
            }
            else if (entry.Schema.IsPresent && !entry.InlineTrue && !EvalChildFast<TAccess>(state.Nodes[entry.Schema.FastNode], doc, valueIndex, ref state))
            {
                return false;
            }
        }

        return (seen & node.RequiredMask) == node.RequiredMask;
    }

    private static bool EvalObjectPlanCore<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (node.UnrolledProperties is PropertyEntry[] unrolled)
        {
            return EvalObjectUnrolled<TAccess>(node, unrolled, doc, index, ref state);
        }

        if (node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);
            if ((node.MinProperties >= 0 && count < node.MinProperties) || (node.MaxProperties >= 0 && count > node.MaxProperties))
            {
                return false;
            }
        }

        Utf8NameMap<PropertyEntry>? properties = node.Properties;
        PatternPropertyEntry[]? patternProperties = node.PatternProperties;
        SchemaNode? additional = node.AdditionalProperties.IsPresent ? state.Nodes[node.AdditionalProperties.FastNode] : null;
        int words = (node.SeenBitCount + 63) >> 6;
        Span<ulong> seen = stackalloc ulong[InlineBitWords];
        seen = seen[..words];
        seen.Clear();

        if (properties is not null || patternProperties is not null || additional is not null)
        {
            int end = default(TAccess).EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
            {
                ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRaw(ref state, doc, valueIndex, out bool escaped);
                if (!escaped)
                {
                    if (!EvalObjectPlanProperty<TAccess>(properties, patternProperties, additional, raw, doc, valueIndex, ref state, seen))
                    {
                        return false;
                    }
                }
                else
                {
                    using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                    if (!EvalObjectPlanProperty<TAccess>(properties, patternProperties, additional, name.Span, doc, valueIndex, ref state, seen))
                    {
                        return false;
                    }
                }
            }
        }

        if (node.RequiredSeenBits is int[] required)
        {
            if (words == 1)
            {
                if ((seen[0] & node.RequiredMask) != node.RequiredMask)
                {
                    return false;
                }
            }
            else
            {
                for (int i = 0; i < required.Length; i++)
                {
                    int bit = required[i];
                    if ((seen[bit >> 6] & (1UL << (bit & 63))) == 0)
                    {
                        return false;
                    }
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
                    if ((seen[rb >> 6] & (1UL << (rb & 63))) == 0)
                    {
                        return false;
                    }
                }

                if (dep.Schema.IsPresent && !EvalChildFast<TAccess>(state.Nodes[dep.Schema.FastNode], doc, index, ref state))
                {
                    return false;
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalObjectPlanProperty<TAccess>(Utf8NameMap<PropertyEntry>? properties, PatternPropertyEntry[]? patternProperties, SchemaNode? additional, scoped ReadOnlySpan<byte> name, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<ulong> seen)
        where TAccess : struct, IDocumentAccess
    {
        bool matched = false;
        if (properties is not null && properties.TryGetValue(name, out PropertyEntry? entry))
        {
            if (entry.SeenBit >= 0)
            {
                seen[entry.SeenBit >> 6] |= 1UL << (entry.SeenBit & 63);
            }

            TypeMask inline = entry.InlineType;
            if (inline != TypeMask.None)
            {
                if (!MatchesType<TAccess>(inline, default(TAccess).TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, entry.InlineLexical))
                {
                    return false;
                }
            }
            else if (entry.Schema.IsPresent && !entry.InlineTrue && !EvalChildFast<TAccess>(state.Nodes[entry.Schema.FastNode], doc, valueIndex, ref state))
            {
                return false;
            }

            matched = true;
        }

        if (patternProperties is not null)
        {
            for (int p = 0; p < patternProperties.Length; p++)
            {
                PatternPropertyEntry pp = patternProperties[p];
                if (pp.Matcher.IsMatch(name))
                {
                    matched = true;
                    if (!EvalChildFast<TAccess>(state.Nodes[pp.Schema.FastNode], doc, valueIndex, ref state))
                    {
                        return false;
                    }
                }
            }
        }

        return matched || additional is null || EvalChildFast<TAccess>(additional, doc, valueIndex, ref state);
    }

    /// <summary>
    /// <see cref="NodePlan.ArrayItems"/>: optional type test, length bounds, then every item through
    /// <see cref="EvalChildFast{TAccess}"/>.
    /// </summary>
    private static bool EvalArrayItemsPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, index);
        if (tokenType != JsonTokenType.StartArray)
        {
            return !node.HasType || MatchesType<TAccess>(node.Type, tokenType, ref state, doc, index, (node.Flags & NodeFlags.Draft4) != 0);
        }

        if (node.HasType && (node.Type & TypeMask.Array) == 0)
        {
            return false;
        }

        if (node.MinItems >= 0 || node.MaxItems >= 0)
        {
            int length = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartArray);
            if ((node.MinItems >= 0 && length < node.MinItems) || (node.MaxItems >= 0 && length > node.MaxItems))
            {
                return false;
            }
        }

        if (!node.Items.IsPresent && !node.UniqueItems)
        {
            return true;
        }

        // Without items (or with items: true), uniqueness is the only per-item work.
        SchemaNode? items = node.Items.IsPresent ? state.Nodes[node.Items.FastNode] : null;
        bool checkItems = items is not null && items.Plan != NodePlan.AlwaysTrue;
        if (!checkItems && !node.UniqueItems)
        {
            return true;
        }

        bool pushed = EnterScope(node, ref state);
        bool ok = true;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        int count = node.UniqueItems ? default(TAccess).Count(ref state, doc, index, JsonTokenType.StartArray) : 0;
        if (node.UniqueItems && count <= PairwiseLimit)
        {
            if (!AllUniquePairwise<TAccess>(ref state, doc, index, end))
            {
                ok = false;
            }
            else if (checkItems)
            {
                for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
                {
                    if (!EvalChildFast<TAccess>(items!, doc, valueIndex, ref state))
                    {
                        ok = false;
                        break;
                    }
                }
            }
        }
        else if (node.UniqueItems)
        {
            var set = new UniqueItemSet(count);
            try
            {
                for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
                {
                    if (!set.TryAdd<TAccess>(ref state, doc, valueIndex) || (checkItems && !EvalChildFast<TAccess>(items!, doc, valueIndex, ref state)))
                    {
                        ok = false;
                        break;
                    }
                }
            }
            finally
            {
                set.Dispose();
            }
        }
        else
        {
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
            {
                if (!EvalChildFast<TAccess>(items!, doc, valueIndex, ref state))
                {
                    ok = false;
                    break;
                }
            }
        }

        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    /// <summary>
    /// Resolves a dynamic reference: by the entry resource alone when the compiler proved that decides it, otherwise
    /// by the dynamic scope, outermost resource first, falling back to the initial target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ResolveDynamicRef(DynamicRefTarget dynamicRef, ref EvaluationState state)
    {
        if (dynamicRef.NodeByEntryResource is int[] byEntry)
        {
            int resolved = byEntry[state.EntryResource];
            return resolved >= 0 ? resolved : dynamicRef.FallbackNode;
        }

        int[] table = dynamicRef.NodeByResource;
        Span<int> scope = state.Scope[..state.ScopeDepth];
        for (int i = 0; i < scope.Length; i++)
        {
            int candidate = table[scope[i]];
            if (candidate >= 0)
            {
                return candidate;
            }
        }

        return dynamicRef.FallbackNode;
    }

    /// <summary>
    /// <see cref="NodePlan.DynamicRef"/>: resolves the anchor against the dynamic scope (outermost resource first)
    /// and dispatches straight to the target's plan. Targets on an in-place cycle keep the general path so that the
    /// runaway guard still applies.
    /// </summary>
    private static bool EvalDynamicRefPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        DynamicRefTarget dynamicRef = node.DynamicRef!;
        int targetNode = ResolveDynamicRef(dynamicRef, ref state);
        SchemaNode target = state.Nodes[targetNode];
        if ((target.Flags & NodeFlags.InPlaceCycle) != 0)
        {
            return Eval<FastMode, TAccess>(node, doc, index, ref state, default, 0);
        }

        return EvalChildFast<TAccess>(target, doc, index, ref state);
    }

    private static bool EvalSimpleArrayFast<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType tokenType = default(TAccess).TokenType(ref state, doc, index);
        if (tokenType != JsonTokenType.StartArray)
        {
            // Not an array: `type` fails, otherwise the array keywords do not apply.
            return !node.HasType;
        }

        int length = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartArray);
        if ((node.MinItems >= 0 && length < node.MinItems) || (node.MaxItems >= 0 && length > node.MaxItems))
        {
            return false;
        }

        SchemaNode items = state.Nodes[node.Items.FastNode];
        int end = default(TAccess).EndIndex(ref state, doc, index);
        if (node.UniqueItems && length <= PairwiseLimit)
        {
            if (!AllUniquePairwise<TAccess>(ref state, doc, index, end))
            {
                return false;
            }
        }
        else if (node.UniqueItems)
        {
            var set = new UniqueItemSet(length);
            try
            {
                for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
                {
                    if (!set.TryAdd<TAccess>(ref state, doc, valueIndex) || (!items.AlwaysTrue && !EvalLeafFast<TAccess>(items, doc, valueIndex, ref state)))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                set.Dispose();
            }

            return true;
        }

        if (items.AlwaysTrue)
        {
            return true;
        }

        if (items.IsTypeOnly)
        {
            TypeMask mask = items.Type;
            bool lexical = items.Dialect == JsonSchemaDialect.Draft4;
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
            {
                if (!MatchesType<TAccess>(mask, default(TAccess).TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, lexical))
                {
                    return false;
                }
            }

            return true;
        }

        for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
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
        ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index);
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

    private static void ReportConst(SchemaNode node, bool match, ref EvaluationState state)
    {
        IJsonSchemaResultsCollector collector = state.Collector!;
        switch (node.Const.TokenType)
        {
            case JsonTokenType.String:
                collector.EvaluatedKeyword(match, node.ConstText!, JsonSchemaEvaluation.ExpectedStringEquals, "const"u8);
                break;
            case JsonTokenType.Number:
                collector.EvaluatedKeyword(match, node.ConstText!, JsonSchemaEvaluation.ExpectedEquals, "const"u8);
                break;
            case JsonTokenType.True:
                collector.EvaluatedKeyword(match, JsonSchemaEvaluation.ExpectedBooleanTrue, "const"u8);
                break;
            case JsonTokenType.False:
                collector.EvaluatedKeyword(match, JsonSchemaEvaluation.ExpectedBooleanFalse, "const"u8);
                break;
            case JsonTokenType.Null:
                collector.EvaluatedKeyword(match, JsonSchemaEvaluation.ExpectedNull, "const"u8);
                break;
            default:
                collector.EvaluatedKeyword(match, null, "const"u8);
                break;
        }
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

            JsonElementHelpers.ParseNumber(default(TAccess).RawValue(ref state, doc, index), out bool neg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
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
        bool ok = true;
        if (node.AssertFormat && FormatKinds.IsNumeric(node.Format))
        {
            JsonElementHelpers.ParseNumber(default(TAccess).RawValue(ref state, doc, index), out bool neg, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
            bool m = MatchesNumericFormat(node.Format, neg, integral, fractional, exponent, ref state);
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, Providers.ExpectedFor(node.Format), "format"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        // Both sides run: in collecting mode every keyword reports, so this must not short-circuit.
#pragma warning disable RCS1233 // Use short-circuiting operator
        return EvalNumberBounds<TMode, TAccess>(node, doc, index, ref state) & ok;
#pragma warning restore RCS1233
    }

    private static bool MatchesNumericFormat(FormatKind format, bool isNegative, scoped ReadOnlySpan<byte> integral, scoped ReadOnlySpan<byte> fractional, int exponent, ref EvaluationState state)
    {
        // A throwaway context so that the shared helpers can be reused; formats are rare enough to build it per call.
        JsonSchemaContext scratch = default;
        ReadOnlySpan<byte> keyword = FormatKeyword;
        return format switch
        {
            FormatKind.Byte => JsonSchemaEvaluation.MatchByte(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.UInt16 => JsonSchemaEvaluation.MatchUInt16(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.UInt32 => JsonSchemaEvaluation.MatchUInt32(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.UInt64 => JsonSchemaEvaluation.MatchUInt64(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.UInt128 => JsonSchemaEvaluation.MatchUInt128(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.SByte => JsonSchemaEvaluation.MatchSByte(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Int16 => JsonSchemaEvaluation.MatchInt16(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Int32 => JsonSchemaEvaluation.MatchInt32(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Int64 => JsonSchemaEvaluation.MatchInt64(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Int128 => JsonSchemaEvaluation.MatchInt128(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Half => JsonSchemaEvaluation.MatchHalf(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Single => JsonSchemaEvaluation.MatchSingle(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Double => JsonSchemaEvaluation.MatchDouble(isNegative, integral, fractional, exponent, keyword, ref scratch),
            FormatKind.Decimal => JsonSchemaEvaluation.MatchDecimal(isNegative, integral, fractional, exponent, keyword, ref scratch),
            _ => true,
        };
    }

    private static bool EvalNumberBounds<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index);

        // Plain integer literal against plain integer bounds: compare as longs (exact) without normalising.
        if (!default(TMode).Collecting && !DisableIntegerFastPath && node.MultipleOf is null && raw.Length <= 18 && raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0
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
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, min.Text, JsonSchemaEvaluation.ExpectedGreaterThanOrEquals, "minimum"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.Maximum is NumberValue max)
        {
            bool m = max.CompareTo(neg, integral, fractional, exponent) <= 0;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, max.Text, JsonSchemaEvaluation.ExpectedLessThanOrEquals, "maximum"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.ExclusiveMinimum is NumberValue emin)
        {
            bool m = emin.CompareTo(neg, integral, fractional, exponent) > 0;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, emin.Text, JsonSchemaEvaluation.ExpectedGreaterThan, "exclusiveMinimum"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.ExclusiveMaximum is NumberValue emax)
        {
            bool m = emax.CompareTo(neg, integral, fractional, exponent) < 0;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, emax.Text, JsonSchemaEvaluation.ExpectedLessThan, "exclusiveMaximum"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.MultipleOf is DivisorValue divisor)
        {
            bool m = divisor.IsMultiple(integral, fractional, exponent);
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, divisor.Text, JsonSchemaEvaluation.ExpectedMultipleOf, "multipleOf"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
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
        // Escapes are rare: the raw text is the value, with no wrapper to dispose.
        if (!default(TAccess).IsEscaped(ref state, doc, index))
        {
            return EvalStringCore<TMode, TAccess>(node, default(TAccess).RawValue(ref state, doc, index), ref state);
        }

        using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
        return EvalStringCore<TMode, TAccess>(node, s.Span, ref state);
    }

    private static bool EvalStringCore<TMode, TAccess>(SchemaNode node, scoped ReadOnlySpan<byte> value, ref EvaluationState state)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;

        if (node.MinLength >= 0 || node.MaxLength >= 0)
        {
            // A rune is one to four bytes, so the byte length bounds the rune count both ways; only values inside
            // the band are counted.
            int byteLength = value.Length;
            int runeCount = -1;
            if (node.MinLength >= 0)
            {
                bool m;
                if (byteLength < node.MinLength)
                {
                    m = false;
                }
                else if (((byteLength + 3) >> 2) >= node.MinLength)
                {
                    m = true;
                }
                else
                {
                    runeCount = JsonElementHelpers.CountRunes(value);
                    m = runeCount >= node.MinLength;
                }

                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, node.MinLength, JsonSchemaEvaluation.ExpectedStringLengthGreaterThanOrEquals, "minLength"u8);
                }

                if (!m)
                {
                    if (!default(TMode).Collecting)
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
                else if (((byteLength + 3) >> 2) > node.MaxLength)
                {
                    m = false;
                }
                else
                {
                    if (runeCount < 0)
                    {
                        runeCount = JsonElementHelpers.CountRunes(value);
                    }

                    m = runeCount <= node.MaxLength;
                }

                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, node.MaxLength, JsonSchemaEvaluation.ExpectedStringLengthLessThanOrEquals, "maxLength"u8);
                }

                if (!m)
                {
                    if (!default(TMode).Collecting)
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
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, pattern.Source, JsonSchemaEvaluation.ExpectedStringMatchesRegularExpression, "pattern"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.AssertFormat && node.Format != FormatKind.None && !FormatKinds.IsNumeric(node.Format))
        {
            bool m = MatchesFormat(node.Format, value, ref state);
            if (node.WarnFormat)
            {
                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(true, m ? Providers.ExpectedFor(node.Format) : Providers.WarningFor(node.Format), "format"u8);
                }

                m = true;
            }
            else if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, Providers.ExpectedFor(node.Format), "format"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.AssertContent)
        {
            JsonSchemaContext scratch = default;
            bool m = node.Content switch
            {
                ContentKind.Base64 => JsonSchemaEvaluation.MatchBase64String(value, ContentEncodingKeyword, ref scratch),
                ContentKind.Json => JsonSchemaEvaluation.MatchJsonContent(value, ContentMediaTypeKeyword, ref scratch),
                ContentKind.Base64Json => JsonSchemaEvaluation.MatchBase64Content(value, ContentMediaTypeKeyword, ref scratch),
                _ => true,
            };

            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(
                    m,
                    node.Content switch
                    {
                        ContentKind.Base64 => JsonSchemaEvaluation.ExpectedBase64String,
                        ContentKind.Json => JsonSchemaEvaluation.ExpectedJsonContent,
                        _ => JsonSchemaEvaluation.ExpectedBase64Content,
                    },
                    node.Content == ContentKind.Base64 ? "contentEncoding"u8 : "contentMediaType"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
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
        if (FormatKinds.IsNumeric(format))
        {
            return true;
        }

        ReadOnlySpan<byte> keyword = FormatKeyword;
        JsonSchemaContext scratch = default;
        return format switch
        {
            FormatKind.Date => JsonSchemaEvaluation.MatchDate(value, keyword, ref scratch),
            FormatKind.DateTime => JsonSchemaEvaluation.MatchDateTime(value, keyword, ref scratch),
            FormatKind.Time => JsonSchemaEvaluation.MatchTime(value, keyword, ref scratch),
            FormatKind.Duration => JsonSchemaEvaluation.MatchDuration(value, keyword, ref scratch),
            FormatKind.Email => JsonSchemaEvaluation.MatchEmail(value, keyword, ref scratch),
            FormatKind.IdnEmail => JsonSchemaEvaluation.MatchIdnEmail(value, keyword, ref scratch),
            FormatKind.Hostname => JsonSchemaEvaluation.MatchHostname(value, keyword, ref scratch),
            FormatKind.IdnHostname => JsonSchemaEvaluation.MatchIdnHostname(value, keyword, ref scratch),
            FormatKind.Ipv4 => JsonSchemaEvaluation.MatchIPV4(value, keyword, ref scratch),
            FormatKind.Ipv6 => JsonSchemaEvaluation.MatchIPV6(value, keyword, ref scratch),
            FormatKind.Uri => JsonSchemaEvaluation.MatchUri(value, keyword, ref scratch),
            FormatKind.UriReference => JsonSchemaEvaluation.MatchUriReference(value, keyword, ref scratch),
            FormatKind.Iri => JsonSchemaEvaluation.MatchIri(value, keyword, ref scratch),
            FormatKind.IriReference => JsonSchemaEvaluation.MatchIriReference(value, keyword, ref scratch),
            FormatKind.Uuid => JsonSchemaEvaluation.MatchUuid(value, keyword, ref scratch),
            FormatKind.UriTemplate => JsonSchemaEvaluation.MatchUriTemplate(value, keyword, ref scratch),
            FormatKind.JsonPointer => JsonSchemaEvaluation.MatchJsonPointer(value, keyword, ref scratch),
            FormatKind.RelativeJsonPointer => JsonSchemaEvaluation.MatchRelativeJsonPointer(value, keyword, ref scratch),
            FormatKind.Regex => JsonSchemaEvaluation.MatchRegex(value, keyword, ref scratch),
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
        if (!default(TMode).Collecting && evaluated.IsEmpty && node.UnrolledProperties is PropertyEntry[] unrolled)
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
            int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);
            if ((node.MinProperties >= 0 && count < node.MinProperties) || (node.MaxProperties >= 0 && count > node.MaxProperties))
            {
                return false;
            }
        }

        // One pass over the instance, matching each name against the few entries by length then bytes: no hash, no
        // by-name lookup through the document (which scans or builds a map per property).
        uint seen = 0;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
        {
            int match;
            ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRaw(ref state, doc, valueIndex, out bool escaped);
            if (!escaped)
            {
                match = FindEntry(entries, raw);
            }
            else
            {
                using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                match = FindEntry(entries, name.Span);
            }

            if (match >= 0)
            {
                seen |= 1u << match;
                PropertyEntry entry = entries[match];
                if (entry.Schema.IsPresent && !EvalProperty<FastMode, TAccess>(entry.Schema, doc, valueIndex, ref state, 0))
                {
                    return false;
                }
            }
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i].IsRequired && (seen & (1u << i)) == 0)
            {
                return false;
            }
        }

        return true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int FindEntry(PropertyEntry[] entries, ReadOnlySpan<byte> name)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                byte[] candidate = entries[i].Name;
                if (candidate.Length == name.Length && name.SequenceEqual(candidate))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Finds a property of an object by name with one pass over its properties through the document access in use.
    /// </summary>
    private static bool TryFindProperty<TAccess>(ref EvaluationState state, IJsonDocument doc, int index, byte[] name, out int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        int end = default(TAccess).EndIndex(ref state, doc, index);
        for (int candidate = index + (2 * RowSize); candidate - RowSize < end; candidate = default(TAccess).NextIndex(ref state, doc, candidate) + RowSize)
        {
            ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRaw(ref state, doc, candidate, out bool escaped);
            if (!escaped)
            {
                if (raw.Length == name.Length && raw.SequenceEqual(name))
                {
                    valueIndex = candidate;
                    return true;
                }
            }
            else
            {
                using UnescapedUtf8JsonString unescaped = PropertyName<TAccess>(ref state, doc, candidate);
                if (unescaped.Span.SequenceEqual(name))
                {
                    valueIndex = candidate;
                    return true;
                }
            }
        }

        valueIndex = -1;
        return false;
    }

    private static bool EvalObjectCore<TMode, TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> evaluated, scoped Span<ulong> seen, int seq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        bool ok = true;

        if (node.MinProperties >= 0 || node.MaxProperties >= 0)
        {
            int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);
            if (node.MinProperties >= 0)
            {
                bool m = count >= node.MinProperties;
                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, node.MinProperties, JsonSchemaEvaluation.ExpectedPropertyCountGreaterThanOrEqualsValue, "minProperties"u8);
                }

                if (!m)
                {
                    if (!default(TMode).Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            if (node.MaxProperties >= 0)
            {
                bool m = count <= node.MaxProperties;
                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, node.MaxProperties, JsonSchemaEvaluation.ExpectedPropertyCountLessThanOrEqualsValue, "maxProperties"u8);
                }

                if (!m)
                {
                    if (!default(TMode).Collecting)
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
            int end = default(TAccess).EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
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
                            if (!default(TMode).Collecting)
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
                                if (!default(TMode).Collecting)
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
                        if (!default(TMode).Collecting)
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
                        if (!default(TMode).Collecting)
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
            int end = default(TAccess).EndIndex(ref state, doc, index);
            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
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
                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeywordForProperty(present, node.RequiredNames![i], present ? Providers.RequiredPresent : Providers.RequiredNotPresent, node.RequiredNames![i], "required"u8);
                }

                if (!present)
                {
                    if (!default(TMode).Collecting)
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
                    if (default(TMode).Collecting)
                    {
                        state.Collector!.EvaluatedKeywordForProperty(present, dep.RequiredNames[r], present ? Providers.RequiredPresent : Providers.RequiredNotPresent, dep.RequiredNames[r], node.Dialect >= JsonSchemaDialect.Draft201909 ? "dependentRequired"u8 : "dependencies"u8);
                    }

                    if (!present)
                    {
                        if (!default(TMode).Collecting)
                        {
                            return false;
                        }

                        ok = false;
                    }
                }

                if (dep.Schema.IsPresent)
                {
                    bool m = EvalInPlaceChild<TMode, TAccess>(dep.Schema, doc, index, ref state, evaluated, seq);
                    if (default(TMode).Collecting)
                    {
                        state.Collector!.EvaluatedKeywordForProperty(m, dep.NameText, JsonSchemaEvaluation.ExpectedMatchesDependentSchemaValue, dep.Name, node.Dialect >= JsonSchemaDialect.Draft201909 ? "dependentSchemas"u8 : "dependencies"u8);
                    }

                    if (!m)
                    {
                        if (!default(TMode).Collecting)
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
        SchemaNode target = state.Nodes[child.FastNode];
        if (!default(TMode).Collecting)
        {
            return EvalChildFast<TAccess>(target, doc, valueIndex, ref state);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.CollectingPath ?? child.Path, target.SchemaLocation, doc, valueIndex, -1), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
        bool ok = Eval<TMode, TAccess>(target, doc, valueIndex, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        return ok;
    }

    private static bool EvalPropertyName<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[default(TMode).Collecting ? child.Node : child.FastNode];
        if (!default(TMode).Collecting)
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
        if (!default(TMode).Collecting)
        {
            return Eval<FastMode, InterfaceAccess>(target, nameDoc, 0, ref state, default, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.Path, target.SchemaLocation, null, -1, -1), Providers.EvalPath, Providers.SchemaPath, null);
        bool ok = Eval<TMode, InterfaceAccess>(target, nameDoc, 0, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        if (!ok)
        {
            collector.EvaluatedKeyword(false, JsonSchemaEvaluation.ExpectedPropertyNameMatchesSchema, "propertyNames"u8);
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
        int end = default(TAccess).EndIndex(ref state, doc, index);
        for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex) + RowSize)
        {
            if (evaluated.IsEmpty || !IsEvaluated(evaluated, propertyIndex))
            {
                MarkEvaluated(evaluated, propertyIndex);
                if (!default(TMode).Collecting && target.AlwaysFalse)
                {
                    return false;
                }

                if (!EvalProperty<TMode, TAccess>(node.UnevaluatedProperties, doc, valueIndex, ref state, seq))
                {
                    if (!default(TMode).Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            propertyIndex++;
        }

        if (default(TMode).Collecting)
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
        int length = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartArray);

        if (node.MinItems >= 0)
        {
            bool m = length >= node.MinItems;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, node.MinItems, JsonSchemaEvaluation.ExpectedItemCountGreaterThanOrEqualsValue, "minItems"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.MaxItems >= 0)
        {
            bool m = length <= node.MaxItems;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, node.MaxItems, JsonSchemaEvaluation.ExpectedItemCountLessThanOrEqualsValue, "maxItems"u8);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
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
        SchemaNode? itemsNode = hasItems ? state.Nodes[default(TMode).Collecting ? node.Items.Node : node.Items.FastNode] : null;
        if (!default(TMode).Collecting && itemsNode is not null)
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
        if (!default(TMode).Collecting && prefixItems is null && !hasContains && !unique && evaluated.IsEmpty && itemsNode is not null && (itemsNode.IsTypeOnly || itemsNode.IsLeaf))
        {
            int simpleEnd = default(TAccess).EndIndex(ref state, doc, index);
            if (itemsNode.IsTypeOnly)
            {
                TypeMask mask = itemsNode.Type;
                bool lexical = itemsNode.Dialect == JsonSchemaDialect.Draft4;
                for (int valueIndex = index + RowSize; valueIndex < simpleEnd; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
                {
                    if (!MatchesType<TAccess>(mask, default(TAccess).TokenType(ref state, doc, valueIndex), ref state, doc, valueIndex, lexical))
                    {
                        return false;
                    }
                }

                return ok;
            }

            for (int valueIndex = index + RowSize; valueIndex < simpleEnd; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
            {
                if (!EvalLeafFast<TAccess>(itemsNode, doc, valueIndex, ref state))
                {
                    return false;
                }
            }

            return ok;
        }

        UniqueItemSet set = unique ? new UniqueItemSet(length) : default;
        bool isUnique = true;
        int containsCount = 0;
        int itemIndex = 0;

        try
        {
            int end = default(TAccess).EndIndex(ref state, doc, index);
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
            {
                if (prefixItems is not null && itemIndex < prefixItems.Length)
                {
                    MarkEvaluated(evaluated, itemIndex);
                    if (!EvalItem<TMode, TAccess>(prefixItems[itemIndex], doc, valueIndex, itemIndex, ref state, seq))
                    {
                        if (!default(TMode).Collecting)
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
                        if (!default(TMode).Collecting)
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
                    if (!set.TryAdd<TAccess>(ref state, doc, valueIndex))
                    {
                        isUnique = false;
                        if (!default(TMode).Collecting)
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
            if (unique)
            {
                set.Dispose();
            }
        }

        if (unique)
        {
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(isUnique, JsonSchemaEvaluation.ExpectedUniqueItems, "uniqueItems"u8);
            }

            ok &= isUnique;
        }

        if (hasContains)
        {
            bool m = containsCount >= node.MinContains && (node.MaxContains < 0 || containsCount <= node.MaxContains);
            if (default(TMode).Collecting)
            {
                if (node.MaxContains >= 0 && containsCount > node.MaxContains)
                {
                    state.Collector!.EvaluatedKeyword(m, node.MaxContains, JsonSchemaEvaluation.ExpectedContainsCountLessThanOrEqualsValue, "contains"u8);
                }
                else
                {
                    state.Collector!.EvaluatedKeyword(m, node.MinContains, JsonSchemaEvaluation.ExpectedContainsCountGreaterThanOrEqualsValue, "contains"u8);
                }
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        return ok;
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
        SchemaNode target = state.Nodes[child.FastNode];
        if (!default(TMode).Collecting)
        {
            return EvalChildFast<TAccess>(target, doc, valueIndex, ref state);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.CollectingPath ?? child.Path, target.SchemaLocation, doc, -1, itemIndex), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
        bool ok = Eval<TMode, TAccess>(target, doc, valueIndex, ref state, default, seq);
        collector.CommitChildContext(seq, ok, ok, JsonSchemaEvaluation.EvaluatedSubschema);
        return ok;
    }

    private static bool EvalContainsItem<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int valueIndex, int itemIndex, ref EvaluationState state, int parentSeq)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[child.FastNode];
        if (!default(TMode).Collecting)
        {
            return EvalChildFast<TAccess>(target, doc, valueIndex, ref state);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.CollectingPath ?? child.Path, target.SchemaLocation, doc, -1, itemIndex), Providers.EvalPath, Providers.SchemaPath, Providers.DocumentPath);
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
        int end = default(TAccess).EndIndex(ref state, doc, index);
        for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
        {
            if (evaluated.IsEmpty || !IsEvaluated(evaluated, itemIndex))
            {
                MarkEvaluated(evaluated, itemIndex);
                if (!default(TMode).Collecting && target.AlwaysFalse)
                {
                    return false;
                }

                if (!EvalItem<TMode, TAccess>(node.UnevaluatedItems, doc, valueIndex, itemIndex, ref state, seq))
                {
                    if (!default(TMode).Collecting)
                    {
                        return false;
                    }

                    ok = false;
                }
            }

            itemIndex++;
        }

        if (default(TMode).Collecting)
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
        if (parentBits.IsEmpty || !CanMark<TAccess>(state.Nodes[default(TMode).Collecting ? child.Node : child.FastNode], ref state, doc, index))
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
        return default(TAccess).TokenType(ref state, doc, index) == JsonTokenType.StartObject ? target.MarksProperties : target.MarksItems;
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
        // Only nodes on a cycle of in-place applicators can recurse without consuming the instance (whose depth the
        // parser bounds); the compiler marks them, so every other edge skips the guard. The state is per call, so
        // an exception needs no unwinding of the counter.
        if ((state.Nodes[default(TMode).Collecting ? child.Node : child.FastNode].Flags & NodeFlags.InPlaceCycle) == 0)
        {
            return EvalInPlaceCoreImpl<TMode, TAccess>(child, doc, index, ref state, bits, parentSeq, commitOnFailure);
        }

        if (++state.Depth > state.MaxDepth)
        {
            throw new JsonSchemaEvaluationException("Maximum in-place applicator depth exceeded; the schema recurses through $ref/allOf/anyOf/oneOf/not/if without consuming the instance.");
        }

        bool result = EvalInPlaceCoreImpl<TMode, TAccess>(child, doc, index, ref state, bits, parentSeq, commitOnFailure);
        state.Depth--;
        return result;
    }

    private static bool EvalInPlaceCoreImpl<TMode, TAccess>(in ChildRef child, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> bits, int parentSeq, bool commitOnFailure)
        where TMode : struct, IEvaluationMode
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode target = state.Nodes[child.FastNode];
        if (!default(TMode).Collecting)
        {
            return bits.IsEmpty
                ? EvalChildFast<TAccess>(target, doc, index, ref state)
                : Eval<FastMode, TAccess>(target, doc, index, ref state, bits, 0);
        }

        IJsonSchemaResultsCollector collector = state.Collector!;
        int seq = collector.BeginChildContext(parentSeq, new EdgeContext(child.CollectingPath ?? child.Path, target.SchemaLocation, null, -1, -1), Providers.EvalPath, Providers.SchemaPath, null);
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
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, node.Ref.Path);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.DynamicRef is DynamicRefTarget dynamicRef)
        {
            int targetNode = ResolveDynamicRef(dynamicRef, ref state);
            var child = new ChildRef(targetNode, dynamicRef.PathSegment);
            bool m = EvalInPlaceChild<TMode, TAccess>(child, doc, index, ref state, evaluated, seq);
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, dynamicRef.PathSegment);
            }

            if (!m)
            {
                if (!default(TMode).Collecting)
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
                    if (!default(TMode).Collecting)
                    {
                        return false;
                    }

                    all = false;
                }
            }

            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(all, all ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, "allOf"u8);
            }

            ok &= all;
        }

        if (node.AnyOf is ChildRef[] anyOf)
        {
            bool any;
            if (!default(TMode).Collecting && node.AnyOfTypeUnion != TypeMask.None)
            {
                any = MatchesType<TAccess>(node.AnyOfTypeUnion, default(TAccess).TokenType(ref state, doc, index), ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4);
            }
            else if (!default(TMode).Collecting && node.AnyOfDiscriminator is Discriminator anyDiscriminator && TrySelectBranches<TAccess>(anyDiscriminator, ref state, doc, index, out int[] selected))
            {
                any = EvalAnyOfSelected<TAccess>(anyOf, selected, doc, index, ref state, evaluated);
            }
            else if (!default(TMode).Collecting && node.AnyOfTypeDispatch is int[] anyDispatch)
            {
                // Only one branch accepts the instance's type; it marks straight into the parent's bits since in
                // flag mode its failure fails the keyword.
                int branch = anyDispatch[(int)default(TAccess).TokenType(ref state, doc, index)];
                any = branch >= 0 && EvalInPlaceCore<FastMode, TAccess>(anyOf[branch], doc, index, ref state, evaluated, 0, commitOnFailure: false);
            }
            else
            {
                any = EvalAnyOf<TMode, TAccess>(anyOf, doc, index, ref state, evaluated, seq);
            }

            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(any, any ? JsonSchemaEvaluation.MatchedAtLeastOneSchema : JsonSchemaEvaluation.DidNotMatchAtLeastOneSchema, "anyOf"u8);
            }

            if (!any)
            {
                if (!default(TMode).Collecting)
                {
                    return false;
                }

                ok = false;
            }
        }

        if (node.OneOf is ChildRef[] oneOf)
        {
            int matched;
            if (!default(TMode).Collecting && node.OneOfTypeUnion != TypeMask.None)
            {
                matched = MatchesType<TAccess>(node.OneOfTypeUnion, default(TAccess).TokenType(ref state, doc, index), ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4) ? 1 : 0;
            }
            else if (!default(TMode).Collecting && node.OneOfDiscriminator is Discriminator oneDiscriminator && TrySelectBranches<TAccess>(oneDiscriminator, ref state, doc, index, out int[] selected))
            {
                matched = EvalOneOfSelected<TAccess>(oneOf, selected, doc, index, ref state, evaluated);
            }
            else if (!default(TMode).Collecting && node.OneOfTypeDispatch is int[] oneDispatch)
            {
                int branch = oneDispatch[(int)default(TAccess).TokenType(ref state, doc, index)];
                matched = branch >= 0 && EvalInPlaceCore<FastMode, TAccess>(oneOf[branch], doc, index, ref state, evaluated, 0, commitOnFailure: false) ? 1 : 0;
            }
            else
            {
                matched = EvalOneOf<TMode, TAccess>(oneOf, doc, index, ref state, evaluated, seq);
            }

            bool one = matched == 1;
            if (default(TMode).Collecting)
            {
                state.Collector!.EvaluatedKeyword(one, matched == 0 ? JsonSchemaEvaluation.MatchedNoSchema : one ? JsonSchemaEvaluation.MatchedExactlyOneSchema : JsonSchemaEvaluation.MatchedMoreThanOneSchema, "oneOf"u8);
            }

            if (!one)
            {
                if (!default(TMode).Collecting)
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
            if (!default(TMode).Collecting)
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
                if (!default(TMode).Collecting)
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
                    if (default(TMode).Collecting)
                    {
                        state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedThen : JsonSchemaEvaluation.DidNotMatchThen, "then"u8);
                    }

                    if (!m)
                    {
                        if (!default(TMode).Collecting)
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
                if (default(TMode).Collecting)
                {
                    state.Collector!.EvaluatedKeyword(m, m ? JsonSchemaEvaluation.MatchedElse : JsonSchemaEvaluation.DidNotMatchElse, "else"u8);
                }

                if (!m)
                {
                    if (!default(TMode).Collecting)
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
        if (parentBits.IsEmpty || !CanMark<TAccess>(state.Nodes[default(TMode).Collecting ? child.Node : child.FastNode], ref state, doc, index))
        {
            bool r = EvalInPlaceCore<TMode, TAccess>(child, doc, index, ref state, default, parentSeq, commitOnFailure: false);
            if (default(TMode).Collecting)
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

            if (default(TMode).Collecting)
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
        if (default(TAccess).TokenType(ref state, doc, index) != JsonTokenType.StartObject)
        {
            selected = [];
            return false;
        }

        if (!TryFindProperty<TAccess>(ref state, doc, index, discriminator.PropertyName, out int valueIndex))
        {
            // Every branch requires the property: none can match.
            selected = [];
            return discriminator.AllRequire;
        }

        JsonTokenType valueType = default(TAccess).TokenType(ref state, doc, valueIndex);
        switch (valueType)
        {
            case JsonTokenType.String:
            {
                using UnescapedUtf8JsonString value = StringValue<TAccess>(ref state, doc, valueIndex);
                selected = LookupDiscriminator(discriminator, Discriminator.StringTag, value.Span);
                return true;
            }

            case JsonTokenType.True:
                selected = LookupDiscriminator(discriminator, Discriminator.BooleanTag, "true"u8);
                return true;
            case JsonTokenType.False:
                selected = LookupDiscriminator(discriminator, Discriminator.BooleanTag, "false"u8);
                return true;
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, valueIndex);
                if (IsCanonicalInteger(raw))
                {
                    selected = LookupDiscriminator(discriminator, Discriminator.NumberTag, raw);
                }
                else
                {
                    // "3.0" may equal a keyed integer: no branch can be excluded.
                    selected = discriminator.AllBranches;
                }

                return true;
            }

            default:
                selected = discriminator.NonString;
                return true;
        }
    }

    /// <summary>Looks a tagged value up in the discriminator's known values, falling back to the unknown-value list.</summary>
    private static int[] LookupDiscriminator(Discriminator discriminator, byte tag, ReadOnlySpan<byte> value)
    {
        Span<byte> inline = stackalloc byte[128];
        byte[]? rented = null;
        Span<byte> key = value.Length < inline.Length ? inline[..(value.Length + 1)] : (rented = ArrayPool<byte>.Shared.Rent(value.Length + 1)).AsSpan(0, value.Length + 1);
        key[0] = tag;
        value.CopyTo(key[1..]);
        int[] selected = discriminator.KnownValues.TryGetValue(key, out int[]? branches) ? branches : discriminator.UnknownString;
        if (rented is not null)
        {
            ArrayPool<byte>.Shared.Return(rented);
        }

        return selected;
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
                    if (!default(TMode).Collecting)
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
                    if (!default(TMode).Collecting && matched > 1)
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
                    else if (!default(TMode).Collecting)
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
}