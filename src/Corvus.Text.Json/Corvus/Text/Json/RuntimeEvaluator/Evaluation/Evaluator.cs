// <copyright file="Evaluator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

    /// <summary>
    /// <see cref="InlineBitWords"/> words of bitset storage as a local without a stack allocation. A method with a
    /// loop and a stackalloc cannot be entered mid-way by on-stack replacement, so the JIT compiles it fully
    /// optimised at once and never instruments it: it misses the dynamic profile that inlines the other loops'
    /// callees. The general object loop and the in-place anyOf take their bits from here instead; the fused
    /// object's several buffers are allocated by a wrapper without a loop.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = sizeof(ulong) * InlineBitWords)]
    private struct InlineBits
    {
        public ulong First;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Span<ulong> Words(ref InlineBits bits)
    {
#if NET
        return MemoryMarshal.CreateSpan(ref bits.First, InlineBitWords);
#else
        // The struct is a local of the caller (never movable), so a span over its address is sound for that frame.
        return new Span<ulong>(Unsafe.AsPointer(ref bits.First), InlineBitWords);
#endif
    }

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
        // Flag mode over a parsed document without a dynamic scope needs no scope buffer and nothing to release:
        // the common call, kept free of the stack allocation and the try/finally the general entry carries.
        // (JsonSchemaEvaluator reaches EvaluateFlagRaw with the entry data it caches; this is the uncached form.)
        if (collector is null && !program.UsesDynamicScope && document is JsonDocument parsed)
        {
            SchemaNode[] nodes = program.Nodes;
            SchemaNode root = nodes[rootNode];
            return EvaluateFlagRaw(program, nodes, nodes[root.FlagEntry], root.ResourceId, program.Options.MaxDepth, parsed, document, rootNode, index);
        }

        return EvaluateGeneral(program, rootNode, document, index, collector);
    }

    /// <summary>
    /// The document's rows and text. Nearly every document evaluated is a <see cref="ParsedJsonDocument{T}"/> of
    /// <see cref="JsonElement"/>: testing for that exact (sealed) type is one comparison, and its accessor is then
    /// called directly and inlined, where the general case is a virtual call on every document.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool RawSpans(JsonDocument parsed, out ReadOnlySpan<byte> rows, out ReadOnlySpan<byte> utf8)
    {
        return parsed is ParsedJsonDocument<JsonElement> exact
            ? exact.TryGetRawSpans(out rows, out utf8)
            : parsed.TryGetRawSpans(out rows, out utf8);
    }

    /// <summary>
    /// Flag mode over a parsed document without a dynamic scope, from the entry data the caller holds: the node the
    /// evaluation enters (the root's flag entry), its resource and the depth limit. The document writes its spans
    /// straight into the state; a document without local rows takes the general entry.
    /// </summary>
    internal static bool EvaluateFlagRaw(CompiledSchema program, SchemaNode[] nodes, SchemaNode entry, int entryResource, int maxDepth, JsonDocument parsed, IJsonDocument document, int rootNode, int index)
    {
        // Every field written once, in place: an object initializer builds a zeroed temporary and copies it over.
        EvaluationState state;
        if (!RawSpans(parsed, out state.RawRows, out state.RawUtf8))
        {
            return EvaluateGeneral(program, rootNode, document, index, null);
        }

        state.Program = program;
        state.Nodes = nodes;
        state.Collector = null;
        state.Scope = default;
        state.ScopeDepth = 0;
        state.RentedScope = null;
        state.Depth = 0;
        state.MaxDepth = maxDepth;
        state.UsesDynamicScope = false;
        state.EntryResource = entryResource;
        return EvalChildFast<RawAccess>(entry, document, index, ref state);
    }

#if NET
    /// <summary>Flag-mode evaluation through a schema's generated code, with the same state as <see cref="EvaluateFlagRaw"/>.</summary>
    internal static bool EvaluateFlagCompiled(CodeGeneration.NodeValidator compiled, CompiledSchema program, SchemaNode[] nodes, int entryResource, int maxDepth, JsonDocument parsed, IJsonDocument document, int rootNode, int index)
    {
        unsafe
        {
            return EvaluateFlagCompiled((delegate*<ref EvaluationState, IJsonDocument, int, bool>)compiled.Method.MethodHandle.GetFunctionPointer(), program, nodes, entryResource, maxDepth, parsed, document, rootNode, index);
        }
    }

    /// <summary>
    /// Flag-mode evaluation through a schema's generated code, called by its address: a delegate to a static method
    /// goes through a thunk that shifts the arguments, on every document.
    /// </summary>
    internal static unsafe bool EvaluateFlagCompiled(delegate*<ref EvaluationState, IJsonDocument, int, bool> compiled, CompiledSchema program, SchemaNode[] nodes, int entryResource, int maxDepth, JsonDocument parsed, IJsonDocument document, int rootNode, int index)
    {
        EvaluationState state;
        if (!RawSpans(parsed, out state.RawRows, out state.RawUtf8))
        {
            return EvaluateGeneral(program, rootNode, document, index, null);
        }

        state.Program = program;
        state.Nodes = nodes;
        state.Collector = null;
        state.Scope = default;
        state.ScopeDepth = 0;
        state.RentedScope = null;
        state.Depth = 0;
        state.MaxDepth = maxDepth;
        state.UsesDynamicScope = false;
        state.EntryResource = entryResource;
        return compiled(ref state, document, index);
    }

    /// <summary>The interpreter's flag-mode evaluation of one node, for generated code that does not specialise it.</summary>
    internal static bool EvalNodeFast(int nodeId, IJsonDocument doc, int index, ref EvaluationState state)
    {
        return EvalChildFast<RawAccess>(state.Nodes[nodeId], doc, index, ref state);
    }
#endif

    // Not inlined: its scope buffer would otherwise sit in the flag-mode entry's frame and be zeroed on every call.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvaluateGeneral(CompiledSchema program, int rootNode, IJsonDocument document, int index, IJsonSchemaResultsCollector? collector)
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
        bool raw = document is JsonDocument jsonDocument && jsonDocument.TryGetRawSpans(out state.RawRows, out state.RawUtf8);

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
#if NET
        if (node.Plan == NodePlan.Generated && !default(TMode).Collecting && evaluated.IsEmpty && typeof(TAccess) == typeof(RawAccess))
        {
            // A node runtime codegen has compiled (the child dispatch's default case arrives here).
            result = node.Generated!(ref state, doc, index);
            if (pushedScope)
            {
                state.ScopeDepth--;
            }

            return result;
        }

#endif
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
                return EvalObjectPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.StrictObject:
                return EvalStrictObjectPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.ArrayItems:
                return EvalArrayItemsPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.DynamicRef:
                return EvalDynamicRefPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.Forward:
                return EvalChildFast<TAccess>(state.Nodes[target.ForwardNode], doc, index, ref state);
            case NodePlan.TypeUnion:
                return EvalTypeUnionPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.Conditional:
                return EvalConditionalPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.TypeDispatch:
                return EvalTypeDispatchPlan<TAccess>(target, doc, index, ref state);
            case NodePlan.Composite:
                return EvalCompositePlan<TAccess>(target, doc, index, ref state);

            case NodePlan.FusedObject:
                return default(TAccess).TokenType(ref state, doc, index) == JsonTokenType.StartObject
                    ? EvalFusedObject<TAccess>(target, doc, index, ref state)
                    : Eval<FastMode, TAccess>(target, doc, index, ref state, default, 0);
            default:
                return Eval<FastMode, TAccess>(target, doc, index, ref state, default, 0);
        }
    }

    /// <summary>
    /// <see cref="NodePlan.TypeUnion"/>: one mask test. Kept out of line so the child dispatch, which is inlined into
    /// every loop, stays small.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalTypeUnionPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        return MatchesType<TAccess>(node.InPlaceUnionMask, default(TAccess).TokenType(ref state, doc, index), ref state, doc, index, node.Dialect == JsonSchemaDialect.Draft4);
    }

    /// <summary>
    /// <see cref="NodePlan.TypeDispatch"/>: the token type selects the one branch that can match; a branch on an
    /// in-place cycle is entered through the guarded general edge.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalTypeDispatchPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        int branch = node.InPlaceDispatch![(int)default(TAccess).TokenType(ref state, doc, index)];
        if (branch < 0)
        {
            return false;
        }

        SchemaNode selected = state.Nodes[node.InPlaceBranches![branch].FastNode];
        return (selected.Flags & NodeFlags.InPlaceCycle) != 0
            ? Eval<FastMode, TAccess>(node, doc, index, ref state, default, 0)
            : EvalChildFast<TAccess>(selected, doc, index, ref state);
    }

    /// <summary>
    /// <see cref="NodePlan.Conditional"/>: the node's own type and object keywords through their plan, then the
    /// <c>if</c> and the branch it selects as children, with none of the general path's in-place bookkeeping.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalConditionalPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        bool pushed = EnterScope(node, ref state);
        bool ok = node.ConditionalOwnPlan switch
        {
            NodePlan.StrictObject => EvalStrictObjectPlan<TAccess>(node, doc, index, ref state),
            NodePlan.Object => EvalObjectPlan<TAccess>(node, doc, index, ref state),
            NodePlan.Leaf => MatchesType<TAccess>(node.Type, default(TAccess).TokenType(ref state, doc, index), ref state, doc, index, (node.Flags & NodeFlags.Draft4) != 0),
            _ => true,
        };

        if (ok)
        {
            SchemaNode[] nodes = state.Nodes;
            ChildRef branch = EvalChildFast<TAccess>(nodes[node.If.FastNode], doc, index, ref state) ? node.Then : node.Else;
            ok = !branch.IsPresent || EvalChildFast<TAccess>(nodes[branch.FastNode], doc, index, ref state);
        }

        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    /// <summary>
    /// <see cref="NodePlan.Composite"/>: the node's own keywords through their plan, then its in-place applicators as
    /// fast-mode children (no evaluated bits: the plan is only chosen without unevaluated keywords).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalCompositePlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        bool pushed = EnterScope(node, ref state);
        bool ok = node.ConditionalOwnPlan switch
        {
            NodePlan.StrictObject => EvalStrictObjectPlan<TAccess>(node, doc, index, ref state),
            NodePlan.Object => EvalObjectPlan<TAccess>(node, doc, index, ref state),
            NodePlan.ArrayItems => EvalArrayItemsPlan<TAccess>(node, doc, index, ref state),
            NodePlan.Leaf => EvalLeafFast<TAccess>(node, doc, index, ref state),
            _ => true,
        };

        if (ok)
        {
            ok = EvalInPlace<FastMode, TAccess>(node, doc, index, ref state, default, 0);
        }

        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    /// <summary>
    /// <see cref="NodePlan.FusedObject"/>: one pass over the properties applying every branch's resolution for each
    /// name, a second step for the branches whose <c>if</c> is decided by the properties seen, then the required
    /// names and the unevaluated check. See <see cref="FusedObject"/>. Out of line: the child dispatch that calls it
    /// is inlined into every object loop.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalFusedObject<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        // Below a live dynamic reference the node itself is entered (its contributors are in its resource).
        bool pushed = EnterScope(node, ref state);
        bool ok = node.Fused!.FlatEntries is StrictEntry[] flat
            ? EvalFlatFusedLoop<TAccess>(node.Fused, flat, doc, index, ref state)
            : EvalFusedObjectBuffers<TAccess>(node, doc, index, ref state);
        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    private static bool EvalFusedObjectBuffers<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        // The buffers are allocated here, in a method without a loop, so that the loops below are compiled through
        // the tiers with a profile (see InlineBits).
        Span<ulong> seenBuffer = stackalloc ulong[InlineBitWords];
        Span<ulong> coverInline = stackalloc ulong[InlineBitWords];
        Span<bool> failed = stackalloc bool[64];
        Span<ulong> altFailed = stackalloc ulong[8];
        Span<int> deferredInline = stackalloc int[3 * 16];
        Span<bool> holds = stackalloc bool[64];
        Span<bool> gateOk = stackalloc bool[64];
        return EvalFusedObjectCore<TAccess>(node, doc, index, ref state, seenBuffer, coverInline, failed, altFailed, deferredInline, holds, gateOk);
    }

    /// <summary>
    /// A flat fused plan (see <see cref="FusedObject.FlatNodes"/>): the strict object loop over the merged names, with
    /// the entry index as the seen bit; names no entry knows are ignored.
    /// </summary>
    private static bool EvalFlatFusedLoop<TAccess>(FusedObject f, StrictEntry[] entries, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (f.FlatMinProperties >= 0 || f.FlatMaxProperties >= 0)
        {
            int count = default(TAccess).Count(ref state, doc, index, JsonTokenType.StartObject);
            if ((f.FlatMinProperties >= 0 && count < f.FlatMinProperties) || (f.FlatMaxProperties >= 0 && count > f.FlatMaxProperties))
            {
                return false;
            }
        }

        Utf8NameMap<FusedEntry> names = f.Entries;
        ulong seen = 0;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        if (!default(TAccess).RowsAvailable(ref state, doc, end))
        {
            ThrowMalformedRows();
        }

        int valueIndex = index + (2 * RowSize);
        while (valueIndex - RowSize < end)
        {
            JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
            int entryIndex;
            int location = default(TAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
            if (location >= 0 && length >= 0)
            {
                entryIndex = names.GetIndex(state.RawUtf8, location, length);
            }
            else
            {
                ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
                entryIndex = !escaped
                    ? (names.TryGetIndex(raw, out int found) ? found : -1)
                    : LookupEscapedFusedName<TAccess>(names, ref state, doc, valueIndex);
            }

            if (entryIndex >= 0)
            {
                seen |= 1UL << entryIndex;
                if (!ApplyEntry<TAccess>(in ArrayRef.At(entries, entryIndex), valueType, doc, valueIndex, ref state))
                {
                    return false;
                }
            }

            valueIndex = next + RowSize;
        }

        return (seen & f.FlatRequiredMask) == f.FlatRequiredMask;
    }

    /// <summary>The fused lookup for an escaped property name, out of line.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LookupEscapedFusedName<TAccess>(Utf8NameMap<FusedEntry> names, ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
        return names.TryGetIndex(name.Span, out int entryIndex) ? entryIndex : -1;
    }

    private static bool EvalFusedObjectCore<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state, scoped Span<ulong> seenBuffer, scoped Span<ulong> coverInline, scoped Span<bool> failed, scoped Span<ulong> altFailed, scoped Span<int> deferredInline, scoped Span<bool> holds, scoped Span<bool> gateOk)
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
                if (contributor.Condition < 0 && contributor.AltGroup < 0 && ((contributor.MinProperties >= 0 && count < contributor.MinProperties) || (contributor.MaxProperties >= 0 && count > contributor.MaxProperties)))
                {
                    return false;
                }
            }
        }

        int seenWords = (f.EntryList.Length + 63) >> 6;
        Span<ulong> seen = seenBuffer[..seenWords];
        seen.Clear();

        bool trackCoverage = f.Unevaluated.IsPresent;
        int coverWords = trackCoverage ? (count + 63) >> 6 : 0;
        ulong[]? rentedCover = coverWords > InlineBitWords ? ArrayPool<ulong>.Shared.Rent(coverWords) : null;
        Span<ulong> covered = rentedCover is null ? coverInline[..coverWords] : rentedCover.AsSpan(0, coverWords);
        covered.Clear();

        failed.Clear();
        altFailed.Clear();
        Span<int> deferred = deferredInline;
        int[]? rentedDeferred = null;
        int deferredCount = 0;
        try
        {
            int ordinal = 0;
            int end = default(TAccess).EndIndex(ref state, doc, index);
            if (!default(TAccess).RowsAvailable(ref state, doc, end))
            {
                ThrowMalformedRows();
            }

            for (int valueIndex = index + (2 * RowSize); valueIndex - RowSize < end; ordinal++)
            {
                JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
                bool cover;
                bool defer;
                int entryIndex;

                // Local rows: the name's location and length from its row, the lookup by word for short names.
                int location = default(TAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
                if (location >= 0 && length >= 0)
                {
                    entryIndex = f.Entries.GetIndex(state.RawUtf8, location, length);
                    bool ok = entryIndex >= 0
                        ? ApplyFusedEntry<TAccess>(f, f.EntryList[entryIndex], valueType, doc, valueIndex, ref state, seen, failed, altFailed, out cover, out defer)
                        : ApplyFusedUnknown<TAccess>(f, state.RawUtf8.Slice(location, length), doc, valueIndex, ref state, failed, altFailed, out cover, out defer);
                    if (!ok)
                    {
                        return false;
                    }
                }
                else
                {
                    ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
                    if (!escaped)
                    {
                        if (!ApplyFusedName<TAccess>(f, raw, valueType, doc, valueIndex, ref state, seen, failed, altFailed, out cover, out defer, out entryIndex))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                        if (!ApplyFusedName<TAccess>(f, name.Span, valueType, doc, valueIndex, ref state, seen, failed, altFailed, out cover, out defer, out entryIndex))
                        {
                            return false;
                        }
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

                valueIndex = next + RowSize;
            }

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
                        if (contributor.Condition < 0)
                        {
                            // Applied in the row loop (the primary of a coalesced application is unconditional when any is).
                            continue;
                        }

                        if (FusedActive(contributor, gateOk, holds) || (app.OtherContributors is int[] more && AnyFusedActive(more, contributors, gateOk, holds)))
                        {
                            if (!ApplyApplication<TAccess>(app, nodes, default(TAccess).TokenType(ref state, doc, valueIndex), doc, valueIndex, ref state))
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

                bool satisfied = true;
                if (contributor.AltGroup >= 0 && ((contributor.MinProperties >= 0 && count < contributor.MinProperties) || (contributor.MaxProperties >= 0 && count > contributor.MaxProperties)))
                {
                    satisfied = false;
                }

                int[] required = contributor.RequiredBits;
                for (int b = 0; b < required.Length && satisfied; b++)
                {
                    satisfied = (seen[required[b] >> 6] & (1UL << (required[b] & 63))) != 0;
                }

                if (!satisfied)
                {
                    if (contributor.AltGroup < 0)
                    {
                        return false;
                    }

                    altFailed[contributor.AltGroup] |= 1UL << contributor.AltBranch;
                }
            }

            FusedAltGroup[] altGroups = f.AltGroups;
            for (int g = 0; g < altGroups.Length; g++)
            {
                FusedAltGroup group = altGroups[g];
                ulong survivors = ~altFailed[g] & (group.BranchCount == 64 ? ulong.MaxValue : (1UL << group.BranchCount) - 1);
                if (survivors == 0 || (group.ExactlyOne && (survivors & (survivors - 1)) != 0))
                {
                    return false;
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

            // not: {required: [...]}: the names must not all be present.
            FusedAlternative[] forbidden = f.Forbidden;
            for (int a = 0; a < forbidden.Length; a++)
            {
                FusedAlternative entry = forbidden[a];
                if (entry.Condition >= 0 && (!gateOk[entry.Condition] || holds[entry.Condition] != entry.Polarity))
                {
                    continue;
                }

                bool all = true;
                int[] bits = entry.Branches[0];
                for (int i = 0; i < bits.Length && all; i++)
                {
                    all = (seen[bits[i] >> 6] & (1UL << (bits[i] & 63))) != 0;
                }

                if (all)
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
    private static bool ApplyApplication<TAccess>(in FusedApplication app, SchemaNode[] nodes, JsonTokenType valueType, IJsonDocument doc, int valueIndex, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        if (app.Node < 0)
        {
            return true;
        }

        int tokenBits = app.TokenBits;
        if (tokenBits != 0)
        {
            return (tokenBits & (1 << (int)valueType)) != 0 && (!app.IntegerOnly || valueType != JsonTokenType.Number || IsInteger<TAccess>(ref state, doc, valueIndex, app.InlineLexical));
        }

        if (app.InlineConst is byte[] constBytes)
        {
            return MatchesStringBytes<TAccess>(constBytes, valueType, ref state, doc, valueIndex);
        }

        return app.InlineEnum is Utf8NameMap<object> allowed
            ? MatchesStringSet<TAccess>(allowed, valueType, ref state, doc, valueIndex)
            : EvalChildFast<TAccess>(nodes[app.Node], doc, valueIndex, ref state);
    }

    /// <summary>
    /// An object with only <c>additionalProperties</c> (a map): no names are read at all. Every value takes the
    /// additional resolution: rejected, a token-bit test, a child dispatch, or nothing.
    /// </summary>
    private static bool EvalMapLoop<TAccess>(SchemaNode node, IJsonDocument doc, int index, int end, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        int valueIndex = index + (2 * RowSize);
        if (node.AdditionalRejects)
        {
            return valueIndex - RowSize >= end;
        }

        ref readonly StrictEntry extra = ref node.AdditionalEntry;
        int tokenBits = extra.TokenBits;
        if (tokenBits != 0)
        {
            bool integerOnly = extra.IntegerOnly;
            bool lexical = extra.Lexical;
            while (valueIndex - RowSize < end)
            {
                JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
                if ((tokenBits & (1 << (int)valueType)) == 0 || (integerOnly && valueType == JsonTokenType.Number && !IsInteger<TAccess>(ref state, doc, valueIndex, lexical)))
                {
                    return false;
                }

                valueIndex = next + RowSize;
            }

            return true;
        }

        if (extra.Child >= 0)
        {
            SchemaNode child = state.Nodes[extra.Child];
            bool nested = extra.NestedObject;
            while (valueIndex - RowSize < end)
            {
                JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
                if (!(nested && valueType == JsonTokenType.StartObject
                    ? EvalStrictObjectNested<TAccess>(child, doc, valueIndex, ref state)
                    : EvalChildFast<TAccess>(child, doc, valueIndex, ref state)))
                {
                    return false;
                }

                valueIndex = next + RowSize;
            }
        }

        return true;
    }

    /// <summary>
    /// An object whose only name test is one pattern property: each name is matched (not at all for a pattern that
    /// matches everything), then the value takes the pattern's resolution or the additional-properties one.
    /// </summary>
    private static bool EvalPatternMapLoop<TAccess>(SchemaNode node, in StrictEntry pattern, IJsonDocument doc, int index, ref EvaluationState state)
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

        int end = default(TAccess).EndIndex(ref state, doc, index);
        if (!default(TAccess).RowsAvailable(ref state, doc, end))
        {
            ThrowMalformedRows();
        }

        PatternMatcher matcher = node.PatternProperties![0].Matcher;
        bool matchAll = matcher.MatchesEverything;
        bool hasAdditional = node.AdditionalProperties.IsPresent;
        int valueIndex = index + (2 * RowSize);
        while (valueIndex - RowSize < end)
        {
            JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
            bool isMatch = matchAll || MatchesName<TAccess>(matcher, ref state, doc, valueIndex);
            if (isMatch || hasAdditional)
            {
                if (!isMatch && node.AdditionalRejects)
                {
                    return false;
                }

                if (!ApplyEntry<TAccess>(in isMatch ? ref pattern : ref node.AdditionalEntry, valueType, doc, valueIndex, ref state))
                {
                    return false;
                }
            }

            valueIndex = next + RowSize;
        }

        return true;
    }

    /// <summary>Prefix items: each position takes its entry's resolution, and the rest the last entry's (the items one).</summary>
    private static bool EvalPrefixLoop<TAccess>(SchemaNode node, StrictEntry[] entries, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        bool pushed = EnterScope(node, ref state);
        bool ok = true;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        int last = entries.Length - 1;
        int position = 0;
        for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(TAccess).NextIndex(ref state, doc, valueIndex))
        {
            JsonTokenType valueType = default(TAccess).TokenType(ref state, doc, valueIndex);
            if (!ApplyEntry<TAccess>(in entries[position < last ? position : last], valueType, doc, valueIndex, ref state))
            {
                ok = false;
                break;
            }

            position++;
        }

        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesName<TAccess>(PatternMatcher matcher, ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
        if (!escaped)
        {
            return matcher.IsMatch(raw);
        }

        using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
        return matcher.IsMatch(name.Span);
    }

    /// <summary>A value against an in-place resolution: a token-type test, a string set or const, or the child.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyEntry<TAccess>(in StrictEntry entry, JsonTokenType valueType, IJsonDocument doc, int valueIndex, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        int tokenBits = entry.TokenBits;
        if (tokenBits != 0)
        {
            return (tokenBits & (1 << (int)valueType)) != 0 && (!entry.IntegerOnly || valueType != JsonTokenType.Number || IsInteger<TAccess>(ref state, doc, valueIndex, entry.Lexical));
        }

        if (entry.Set is Utf8NameMap<object> set)
        {
            return MatchesStringSet<TAccess>(set, valueType, ref state, doc, valueIndex);
        }

        if (entry.ConstBytes is byte[] constBytes)
        {
            return MatchesStringBytes<TAccess>(constBytes, valueType, ref state, doc, valueIndex);
        }

        if (entry.Child < 0)
        {
            return !entry.LengthBounded || LengthLeafMatches<TAccess>(in entry, valueType, ref state, doc, valueIndex);
        }

        int bit = 1 << (int)valueType;
        if ((entry.ChildDecided & bit) != 0)
        {
            return (entry.ChildAccepts & bit) != 0;
        }

        SchemaNode child = state.Nodes[entry.Child];
        return entry.NestedObject && valueType == JsonTokenType.StartObject
            ? EvalStrictObjectNested<TAccess>(child, doc, valueIndex, ref state)
            : EvalChildFast<TAccess>(child, doc, valueIndex, ref state);
    }

    /// <summary>
    /// A value against a string-length leaf entry: its type, then a string's length. Out of line: inlined, it grows the
    /// object loops past the size at which native AOT inlines the strict object plan into the evaluator's entry, which
    /// costs every document more than the call costs the length leaves.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool LengthLeafMatches<TAccess>(in StrictEntry entry, JsonTokenType valueType, ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        int bits = entry.LengthTokenBits;
        if (bits != 0 && ((bits & (1 << (int)valueType)) == 0 || (entry.LengthIntegerOnly && valueType == JsonTokenType.Number && !IsInteger<TAccess>(ref state, doc, valueIndex, entry.Lexical))))
        {
            return false;
        }

        return valueType != JsonTokenType.String || StringLengthWithin<TAccess>(in entry, ref state, doc, valueIndex);
    }

    /// <summary>
    /// Whether a string value is within a length-leaf entry's bounds. A rune is one to four bytes, so an unescaped
    /// value's byte length decides most values without counting; escaped values are unescaped first, out of line.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool StringLengthWithin<TAccess>(in StrictEntry entry, ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index, out bool escaped);
        if (escaped)
        {
            return EscapedStringLengthWithin<TAccess>(entry.MinLength, entry.MaxLength, ref state, doc, index);
        }

        int bytes = raw.Length;
        if ((entry.MaxLength < 0 || bytes <= entry.MaxLength) && (entry.MinLength < 0 || ((bytes + 3) >> 2) >= entry.MinLength))
        {
            return true;
        }

        return LengthWithin(raw, entry.MinLength, entry.MaxLength);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EscapedStringLengthWithin<TAccess>(int minLength, int maxLength, ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
        return LengthWithin(s.Span, minLength, maxLength);
    }

    /// <summary>Whether a string's length in runes is within the bounds (-1 for an absent bound).</summary>
    private static bool LengthWithin(ReadOnlySpan<byte> value, int minLength, int maxLength)
    {
        int bytes = value.Length;
        if ((minLength >= 0 && bytes < minLength) || (maxLength >= 0 && ((bytes + 3) >> 2) > maxLength))
        {
            return false;
        }

        int runes = JsonElementHelpers.CountRunes(value);
        return (minLength < 0 || runes >= minLength) && (maxLength < 0 || runes <= maxLength);
    }

    /// <summary>The name lookup for an escaped property name, out of line: escapes are rare and the loop is register-bound.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int LookupEscapedName<TAccess>(Utf8NameMap<PropertyEntry> properties, ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
        return properties.TryGetIndex(name.Span, out int entryIndex) ? entryIndex : -1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowMalformedRows()
    {
        throw new InvalidOperationException("The document's metadata rows end before the container's end row.");
    }

    /// <summary>Whether the value is the string <paramref name="expected"/>: raw bytes when unescaped, else unescaped.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchesStringBytes<TAccess>(byte[] expected, JsonTokenType tokenType, ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        if (tokenType != JsonTokenType.String)
        {
            return false;
        }

        int location = default(TAccess).RawValueLocation(ref state, doc, index, out int length);
        if (location >= 0 && length >= 0)
        {
            return state.RawUtf8.Slice(location, length).SequenceEqual(expected);
        }

        ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index, out bool escaped);
        if (!escaped)
        {
            return raw.SequenceEqual(expected);
        }

        using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
        return s.Span.SequenceEqual(expected);
    }

    /// <summary>A string value's membership of a set (an <c>enum</c> of strings): the raw text when unescaped, else the unescaped text. Out of line: the loops that call it are inlined widely.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool MatchesStringSet<TAccess>(Utf8NameMap<object> allowed, JsonTokenType tokenType, ref EvaluationState state, IJsonDocument doc, int index)
        where TAccess : struct, IDocumentAccess
    {
        if (tokenType != JsonTokenType.String)
        {
            return false;
        }

        // Local rows: the value's location and length from its row, the lookup by word for short values.
        int location = default(TAccess).RawValueLocation(ref state, doc, index, out int length);
        if (location >= 0 && length >= 0)
        {
            return allowed.GetIndex(state.RawUtf8, location, length) >= 0;
        }

        ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, index, out bool escaped);
        if (!escaped)
        {
            return allowed.TryGetIndex(raw, out _);
        }

        using UnescapedUtf8JsonString s = StringValue<TAccess>(ref state, doc, index);
        return allowed.TryGetIndex(s.Span, out _);
    }

    /// <summary>
    /// The per-property step of <see cref="EvalFusedObject{TAccess}"/>: applies every unconditional resolution of the
    /// name now and notes whether a conditional one is pending. Returns <see langword="false"/> when a child failed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ApplyFusedName<TAccess>(FusedObject f, scoped ReadOnlySpan<byte> nameSpan, JsonTokenType valueType, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<ulong> seen, scoped Span<bool> failed, scoped Span<ulong> altFailed, out bool cover, out bool defer, out int entryIndex)
        where TAccess : struct, IDocumentAccess
    {
        if (f.Entries.TryGetValue(nameSpan, out FusedEntry? entry))
        {
            entryIndex = entry.Index;
            return ApplyFusedEntry<TAccess>(f, entry, valueType, doc, valueIndex, ref state, seen, failed, altFailed, out cover, out defer);
        }

        entryIndex = -1;
        return ApplyFusedUnknown<TAccess>(f, nameSpan, doc, valueIndex, ref state, failed, altFailed, out cover, out defer);
    }

    /// <summary>A known name: its value tests for the conditions, then every unconditional application; conditional ones are deferred to the second pass.</summary>
    private static bool ApplyFusedEntry<TAccess>(FusedObject f, FusedEntry entry, JsonTokenType valueType, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<ulong> seen, scoped Span<bool> failed, scoped Span<ulong> altFailed, out bool cover, out bool defer)
        where TAccess : struct, IDocumentAccess
    {
        SchemaNode[] nodes = state.Nodes;
        FusedContributor[] contributors = f.Contributors;
        cover = false;
        defer = false;
        seen[entry.Index >> 6] |= 1UL << (entry.Index & 63);
        if (entry.HasValueTests)
        {
            ApplyValueTests<TAccess>(entry, valueType, ref state, doc, valueIndex, failed);
        }

        FusedApplication[] applications = entry.Applications;
        for (int a = 0; a < applications.Length; a++)
        {
            FusedApplication app = applications[a];
            FusedContributor applied = contributors[app.Contributor];
            if (applied.Condition < 0)
            {
                if (!ApplyApplication<TAccess>(app, nodes, valueType, doc, valueIndex, ref state))
                {
                    // A branch of an alternative group fails on its own; anything else fails the object.
                    if (applied.AltGroup < 0)
                    {
                        return false;
                    }

                    altFailed[applied.AltGroup] |= 1UL << applied.AltBranch;
                    continue;
                }

                cover = true;
            }
            else
            {
                defer = true;
            }
        }

        return true;
    }

    /// <summary>A name no entry knows: every unconditional branch resolves it (patterns, additionalProperties); conditional ones defer.</summary>
    private static bool ApplyFusedUnknown<TAccess>(FusedObject f, scoped ReadOnlySpan<byte> nameSpan, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<bool> failed, scoped Span<ulong> altFailed, out bool cover, out bool defer)
        where TAccess : struct, IDocumentAccess
    {
        cover = false;
        defer = false;
        if (!f.ResolvesUnknownNames)
        {
            return true;
        }

        FusedAbsentPattern[] absent = f.AbsentPatterns;
        for (int p = 0; p < absent.Length; p++)
        {
            if (!failed[absent[p].Condition] && absent[p].Matcher.IsMatch(nameSpan))
            {
                failed[absent[p].Condition] = true;
            }
        }

        SchemaNode[] nodes = state.Nodes;
        FusedContributor[] contributors = f.Contributors;
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
                if (contributor.AltGroup < 0)
                {
                    return false;
                }

                altFailed[contributor.AltGroup] |= 1UL << contributor.AltBranch;
                continue;
            }

            cover |= matched;
        }

        return true;
    }

    /// <summary>Whether a conditional branch applies: its condition is reachable and holds with its polarity.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FusedActive(FusedContributor contributor, ReadOnlySpan<bool> gateOk, ReadOnlySpan<bool> holds)
    {
        return contributor.Condition >= 0 && gateOk[contributor.Condition] && holds[contributor.Condition] == contributor.Polarity;
    }

    private static bool AnyFusedActive(int[] more, FusedContributor[] contributors, ReadOnlySpan<bool> gateOk, ReadOnlySpan<bool> holds)
    {
        for (int i = 0; i < more.Length; i++)
        {
            if (FusedActive(contributors[more[i]], gateOk, holds))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks a property's value against the conditions that test it: the value is keyed like a discriminator value
    /// (tagged string, canonical integer or boolean) and each condition whose allowed set lacks it is marked failed.
    /// A value of any other kind fails every test, since only keyable constants are accepted at compile time.
    /// </summary>
    private static void ApplyValueTests<TAccess>(FusedEntry entry, JsonTokenType valueType, ref EvaluationState state, IJsonDocument doc, int valueIndex, scoped Span<bool> failed)
        where TAccess : struct, IDocumentAccess
    {
        switch (valueType)
        {
            case JsonTokenType.String:
            {
                ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, valueIndex, out bool escaped);
                if (!escaped)
                {
                    TestValueKey(entry, Discriminator.StringTag, raw, failed);
                    return;
                }

                using UnescapedUtf8JsonString text = StringValue<TAccess>(ref state, doc, valueIndex);
                TestValueKey(entry, Discriminator.StringTag, text.Span, failed);
                return;
            }

            case JsonTokenType.True:
                TestValueKey(entry, Discriminator.BooleanTag, "true"u8, failed);
                return;
            case JsonTokenType.False:
                TestValueKey(entry, Discriminator.BooleanTag, "false"u8, failed);
                return;
            case JsonTokenType.Null:
                TestValueKey(entry, Discriminator.NullTag, "null"u8, failed);
                return;
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> rawNumber = default(TAccess).RawValue(ref state, doc, valueIndex);
                if (IsCanonicalInteger(rawNumber))
                {
                    TestValueKey(entry, Discriminator.NumberTag, rawNumber, failed);
                    return;
                }

                // "2.0" may equal a keyed integer: test it the slow way, by value.
                foreach (FusedValueTest test in entry.KeyedTests)
                {
                    if (!NumberInKeys(rawNumber, test.Allowed!))
                    {
                        failed[test.Condition] = true;
                    }
                }

                foreach (FusedValueTest test in entry.PatternTests)
                {
                    if (test.RequiresString)
                    {
                        failed[test.Condition] = true;
                    }
                }

                return;
            }

            default:
                // A pattern does not apply to a non-string; a key set has nothing that matches a structured value or null.
                foreach (FusedValueTest test in entry.KeyedTests)
                {
                    failed[test.Condition] = true;
                }

                foreach (FusedValueTest test in entry.PatternTests)
                {
                    if (test.RequiresString)
                    {
                        failed[test.Condition] = true;
                    }
                }

                return;
        }
    }

    /// <summary>One lookup of the tagged value decides every keyed test; the pattern tests run on the string.</summary>
    private static void TestValueKey(FusedEntry entry, byte tag, ReadOnlySpan<byte> value, Span<bool> failed)
    {
        FusedValueTest[] keyed = entry.KeyedTests;
        if (keyed.Length > 0)
        {
            if (entry.MergedAllowed is Utf8NameMap<FusedTestMask> merged)
            {
                ulong mask = merged.TryGetValue(tag, value, out FusedTestMask? box) ? box.Mask : 0;
                for (int t = 0; t < keyed.Length; t++)
                {
                    if ((mask & (1UL << t)) == 0)
                    {
                        failed[keyed[t].Condition] = true;
                    }
                }
            }
            else
            {
                for (int t = 0; t < keyed.Length; t++)
                {
                    if (!keyed[t].Allowed!.TryGetValue(tag, value, out _))
                    {
                        failed[keyed[t].Condition] = true;
                    }
                }
            }
        }

        FusedValueTest[] patterns = entry.PatternTests;
        for (int t = 0; t < patterns.Length; t++)
        {
            FusedValueTest test = patterns[t];
            bool passes = tag == Discriminator.StringTag ? test.Pattern!.IsMatch(value) : !test.RequiresString;
            if (!passes)
            {
                failed[test.Condition] = true;
            }
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
        bool ok = EvalObjectPlanCore<TAccess>(node, doc, index, ref state);
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
    private static bool EvalStrictObjectPlan<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        // The object plan's prologue, in the same method as the loop so that entering a strict object is one call.
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
        bool ok = EvalStrictObjectLoop<TAccess>(node, doc, index, ref state);
        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    /// <summary>
    /// A strict object entered from a loop that already holds the value's token type (an object) and whose entry
    /// says the child's type admits objects: the loop without the plan's prologue.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool EvalStrictObjectNested<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
        where TAccess : struct, IDocumentAccess
    {
        bool pushed = EnterScope(node, ref state);
        bool ok = EvalStrictObjectLoop<TAccess>(node, doc, index, ref state);
        if (pushed)
        {
            state.ScopeDepth--;
        }

        return ok;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EvalStrictObjectLoop<TAccess>(SchemaNode node, IJsonDocument doc, int index, ref EvaluationState state)
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

        Utf8NameMap<PropertyEntry>? properties = node.Properties;
        StrictEntry[] entries = node.StrictEntries ?? [];
        ulong seen = 0;
        int end = default(TAccess).EndIndex(ref state, doc, index);
        if (!default(TAccess).RowsAvailable(ref state, doc, end))
        {
            ThrowMalformedRows();
        }

        if (properties is null)
        {
            return EvalMapLoop<TAccess>(node, doc, index, end, ref state);
        }

        // Every row from here to the end row exists (checked once above), so the header and name reads are unchecked.
        int valueIndex = index + (2 * RowSize);
        while (valueIndex - RowSize < end)
        {
            JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
            int entryIndex = -1;
            if (properties is not null)
            {
                // Local rows: the name's location and length from its row, the lookup by word for short names.
                int location = default(TAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
                if (location >= 0)
                {
                    entryIndex = length >= 0
                        ? properties.GetIndex(state.RawUtf8, location, length)
                        : LookupEscapedName<TAccess>(properties, ref state, doc, valueIndex);
                }
                else
                {
                    ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
                    if (!escaped)
                    {
                        properties.TryGetIndex(raw, out entryIndex);
                    }
                    else
                    {
                        entryIndex = LookupEscapedName<TAccess>(properties, ref state, doc, valueIndex);
                    }
                }
            }

            // The resolution for the value: the entry's (its index came from the map) or the additional-properties one.
            if (entryIndex < 0 && node.AdditionalRejects)
            {
                return false;
            }

            ref readonly StrictEntry entry = ref entryIndex < 0 ? ref node.AdditionalEntry : ref ArrayRef.At(entries, entryIndex);
            if (entry.SeenBit >= 0)
            {
                seen |= 1UL << entry.SeenBit;
            }

            int tokenBits = entry.TokenBits;
            if (tokenBits != 0)
            {
                if ((tokenBits & (1 << (int)valueType)) == 0 || (entry.IntegerOnly && valueType == JsonTokenType.Number && !IsInteger<TAccess>(ref state, doc, valueIndex, entry.Lexical)))
                {
                    return false;
                }
            }
            else if (entry.Set is Utf8NameMap<object> set)
            {
                if (!MatchesStringSet<TAccess>(set, valueType, ref state, doc, valueIndex))
                {
                    return false;
                }
            }
            else if (entry.ConstBytes is byte[] constBytes)
            {
                if (!MatchesStringBytes<TAccess>(constBytes, valueType, ref state, doc, valueIndex))
                {
                    return false;
                }
            }
            else if (entry.Child >= 0)
            {
                int bit = 1 << (int)valueType;
                if ((entry.ChildDecided & bit) != 0)
                {
                    // The child's type alone decides this kind of value.
                    if ((entry.ChildAccepts & bit) == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    SchemaNode child = state.Nodes[entry.Child];
                    if (!(entry.NestedObject && valueType == JsonTokenType.StartObject
                        ? EvalStrictObjectNested<TAccess>(child, doc, valueIndex, ref state)
                        : EvalChildFast<TAccess>(child, doc, valueIndex, ref state)))
                    {
                        return false;
                    }
                }
            }
            else if (entry.LengthBounded && !LengthLeafMatches<TAccess>(in entry, valueType, ref state, doc, valueIndex))
            {
                return false;
            }

            valueIndex = next + RowSize;
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

        if (node.PatternMap is StrictEntry[] patternMap)
        {
            return EvalPatternMapLoop<TAccess>(node, in patternMap[0], doc, index, ref state);
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
        InlineBits seenBits = default;
        Span<ulong> seen = Words(ref seenBits)[..words];

        if (properties is not null || patternProperties is not null || additional is not null)
        {
            StrictEntry[] entries = node.StrictEntries ?? [];
            int end = default(TAccess).EndIndex(ref state, doc, index);
            if (!default(TAccess).RowsAvailable(ref state, doc, end))
            {
                ThrowMalformedRows();
            }

            int valueIndex = index + (2 * RowSize);
            while (valueIndex - RowSize < end)
            {
                JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);

                // Local rows: the name's location and length from its row, the lookup by word for short names.
                int location = default(TAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
                if (location >= 0 && length >= 0)
                {
                    int entryIndex = properties is null ? -1 : properties.GetIndex(state.RawUtf8, location, length);
                    if (!EvalObjectPlanProperty<TAccess>(node, entryIndex, entries, patternProperties, additional, state.RawUtf8.Slice(location, length), valueType, doc, valueIndex, ref state, seen))
                    {
                        return false;
                    }
                }
                else
                {
                    ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
                    if (!escaped)
                    {
                        int entryIndex = properties is not null && properties.TryGetIndex(raw, out int found) ? found : -1;
                        if (!EvalObjectPlanProperty<TAccess>(node, entryIndex, entries, patternProperties, additional, raw, valueType, doc, valueIndex, ref state, seen))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        using UnescapedUtf8JsonString name = PropertyName<TAccess>(ref state, doc, valueIndex);
                        int entryIndex = properties is not null && properties.TryGetIndex(name.Span, out int found) ? found : -1;
                        if (!EvalObjectPlanProperty<TAccess>(node, entryIndex, entries, patternProperties, additional, name.Span, valueType, doc, valueIndex, ref state, seen))
                        {
                            return false;
                        }
                    }
                }

                valueIndex = next + RowSize;
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
    private static bool EvalObjectPlanProperty<TAccess>(SchemaNode node, int entryIndex, StrictEntry[] entries, PatternPropertyEntry[]? patternProperties, SchemaNode? additional, scoped ReadOnlySpan<byte> name, JsonTokenType valueType, IJsonDocument doc, int valueIndex, ref EvaluationState state, scoped Span<ulong> seen)
        where TAccess : struct, IDocumentAccess
    {
        bool matched = false;
        if (entryIndex >= 0)
        {
            ref readonly StrictEntry entry = ref ArrayRef.At(entries, entryIndex);
            if (entry.SeenBit >= 0)
            {
                seen[entry.SeenBit >> 6] |= 1UL << (entry.SeenBit & 63);
            }

            int tokenBits = entry.TokenBits;
            if (tokenBits != 0)
            {
                if ((tokenBits & (1 << (int)valueType)) == 0 || (entry.IntegerOnly && valueType == JsonTokenType.Number && !IsInteger<TAccess>(ref state, doc, valueIndex, entry.Lexical)))
                {
                    return false;
                }
            }
            else if (entry.Set is Utf8NameMap<object> allowed)
            {
                if (!MatchesStringSet<TAccess>(allowed, valueType, ref state, doc, valueIndex))
                {
                    return false;
                }
            }
            else if (entry.ConstBytes is byte[] constBytes)
            {
                if (!MatchesStringBytes<TAccess>(constBytes, valueType, ref state, doc, valueIndex))
                {
                    return false;
                }
            }
            else if (entry.Child >= 0)
            {
                int bit = 1 << (int)valueType;
                if ((entry.ChildDecided & bit) != 0)
                {
                    // The child's type alone decides this kind of value.
                    if ((entry.ChildAccepts & bit) == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    SchemaNode child = state.Nodes[entry.Child];
                    if (!(entry.NestedObject && valueType == JsonTokenType.StartObject
                        ? EvalStrictObjectNested<TAccess>(child, doc, valueIndex, ref state)
                        : EvalChildFast<TAccess>(child, doc, valueIndex, ref state)))
                    {
                        return false;
                    }
                }
            }
            else if (entry.LengthBounded && !LengthLeafMatches<TAccess>(in entry, valueType, ref state, doc, valueIndex))
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

        if (matched || additional is null)
        {
            return true;
        }

        ref readonly StrictEntry extra = ref node.AdditionalEntry;
        int additionalBits = extra.TokenBits;
        if (additionalBits != 0)
        {
            return (additionalBits & (1 << (int)valueType)) != 0 && (!extra.IntegerOnly || valueType != JsonTokenType.Number || IsInteger<TAccess>(ref state, doc, valueIndex, extra.Lexical));
        }

        return EvalChildFast<TAccess>(additional, doc, valueIndex, ref state);
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

        if (node.PrefixEntries is StrictEntry[] prefixEntries)
        {
            return EvalPrefixLoop<TAccess>(node, prefixEntries, doc, index, ref state);
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
        if (!default(TAccess).RowsAvailable(ref state, doc, end))
        {
            ThrowMalformedRows();
        }

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
            bool nested = node.ItemsNestedObject;
            int decided = node.ItemsDecided;
            int accepts = node.ItemsAccepts;
            for (int valueIndex = index + RowSize; valueIndex < end;)
            {
                JsonTokenType valueType = default(TAccess).TokenTypeAndNextUnchecked(ref state, doc, valueIndex, out int next);
                int bit = 1 << (int)valueType;
                if ((decided & bit) != 0)
                {
                    // The items schema's type alone decides this kind of item.
                    if ((accepts & bit) == 0)
                    {
                        ok = false;
                        break;
                    }

                    valueIndex = next;
                    continue;
                }

                if (!(nested && valueType == JsonTokenType.StartObject
                    ? EvalStrictObjectNested<TAccess>(items!, doc, valueIndex, ref state)
                    : EvalChildFast<TAccess>(items!, doc, valueIndex, ref state)))
                {
                    ok = false;
                    break;
                }

                valueIndex = next;
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
            // A table smaller than the resource count (a statically resolved reference, or entry points added
            // after it was built) resolves to the fallback.
            int resolved = (uint)state.EntryResource < (uint)byEntry.Length ? byEntry[state.EntryResource] : -1;
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

            return MatchesStringSet<TAccess>(node.EnumStrings!, tokenType, ref state, doc, index);
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
        if (!default(TMode).Collecting && !DisableIntegerFastPath && raw.Length <= 18 && raw.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0
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

            if (node.MultipleOf is DivisorValue lmul)
            {
                if (lmul.AsLong is long d)
                {
                    return value % d == 0;
                }

                goto Slow;
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
                        state.Collector!.EvaluatedKeywordForProperty(present, dep.RequiredNames[r], present ? Providers.RequiredPresent : Providers.RequiredNotPresent, dep.RequiredNames[r], dep.KeywordName);
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
                        state.Collector!.EvaluatedKeywordForProperty(m, dep.NameText, JsonSchemaEvaluation.ExpectedMatchesDependentSchemaValue, dep.Name, dep.KeywordName);
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

    /// <summary>
    /// The node collecting mode evaluates for a child: the end of its pure-<c>$ref</c> chain, as in flag mode, but
    /// not the representative that <c>SchemaCompiler.CanonicalizeEquivalentNodes</c> chose for identical subschemas, so
    /// that results and annotations report the child's own schema location.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CollectingNode(in ChildRef child, SchemaNode[] nodes)
    {
        int elided = nodes[child.Node].ElidedTarget;
        return elided >= 0 ? elided : child.Node;
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
        target = state.Nodes[CollectingNode(child, state.Nodes)];
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

        if (!default(TMode).Collecting && TryNameAgainstLeaf<TAccess>(target, ref state, doc, valueIndex, out bool decided))
        {
            return decided;
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

    /// <summary>
    /// Flag mode's test of a property's name against a <c>propertyNames</c> schema that only a type and string
    /// keywords decide (a pattern, a length, a format): the name's text is tested where it lies, with no document made
    /// of it. False when the schema has other keywords or the name is escaped; the caller then evaluates it as a document.
    /// </summary>
    private static bool TryNameAgainstLeaf<TAccess>(SchemaNode target, ref EvaluationState state, IJsonDocument doc, int valueIndex, out bool result)
        where TAccess : struct, IDocumentAccess
    {
        result = false;
        if (!target.IsLeaf || target.HasConst || target.Enum is not null)
        {
            return false;
        }

        if (target.HasType && (target.Type & TypeMask.String) == 0)
        {
            return true;
        }

        if (!target.HasStringKeywords)
        {
            result = true;
            return true;
        }

        ReadOnlySpan<byte> raw = default(TAccess).PropertyNameRawUnchecked(ref state, doc, valueIndex, out bool escaped);
        if (escaped)
        {
            return false;
        }

        result = EvalStringCore<FastMode, TAccess>(target, raw, ref state);
        return true;
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

        // In flag mode, contains stops being evaluated once minContains is met, unless maxContains bounds the count or
        // its matches are marked evaluated for a live evaluated set.
        bool stopAtMinContains = !default(TMode).Collecting && node.MaxContains < 0 && (!node.ContainsMarksEvaluated || evaluated.IsEmpty);
        bool containsSettled = stopAtMinContains && node.MinContains <= 0;

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

                if (hasContains && !containsSettled)
                {
                    if (EvalContainsItem<TMode, TAccess>(node.Contains, doc, valueIndex, itemIndex, ref state, seq))
                    {
                        containsCount++;
                        if (node.ContainsMarksEvaluated)
                        {
                            MarkEvaluated(evaluated, itemIndex);
                        }

                        if (!default(TMode).Collecting)
                        {
                            if (node.MaxContains >= 0 && containsCount > node.MaxContains)
                            {
                                // More matches than maxContains allows: no later item can undo that.
                                return false;
                            }

                            if (stopAtMinContains && containsCount >= node.MinContains)
                            {
                                // minContains is met and nothing bounds the count above or needs the other matches.
                                containsSettled = true;
                                if (prefixItems is null && !hasItems && !unique)
                                {
                                    break;
                                }
                            }
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
        target = state.Nodes[CollectingNode(child, state.Nodes)];
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
        target = state.Nodes[CollectingNode(child, state.Nodes)];
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
        target = state.Nodes[CollectingNode(child, state.Nodes)];
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
            else if (!default(TMode).Collecting && node.AnyOfByKind is int[]?[] anyByKind && anyByKind[(int)default(TAccess).TokenType(ref state, doc, index)] is int[] anyCandidates)
            {
                // Only the branches that can accept this kind of value can match.
                any = EvalAnyOfSelected<TAccess>(anyOf, anyCandidates, doc, index, ref state, evaluated);
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
            else if (!default(TMode).Collecting && node.OneOfByKind is int[]?[] oneByKind && oneByKind[(int)default(TAccess).TokenType(ref state, doc, index)] is int[] oneCandidates)
            {
                // Only the branches that can accept this kind of value can match.
                matched = EvalOneOfSelected<TAccess>(oneOf, oneCandidates, doc, index, ref state, evaluated);
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

        selected = SelectBranchesByValue<TAccess>(discriminator, ref state, doc, valueIndex);
        return true;
    }

    /// <summary>The branches a discriminator selects for the value of its property.</summary>
    private static int[] SelectBranchesByValue<TAccess>(Discriminator discriminator, ref EvaluationState state, IJsonDocument doc, int valueIndex)
        where TAccess : struct, IDocumentAccess
    {
        JsonTokenType valueType = default(TAccess).TokenType(ref state, doc, valueIndex);
        switch (valueType)
        {
            case JsonTokenType.String:
            {
                if (!default(TAccess).IsEscaped(ref state, doc, valueIndex))
                {
                    return LookupDiscriminator(discriminator, Discriminator.StringTag, default(TAccess).RawValue(ref state, doc, valueIndex));
                }

                using UnescapedUtf8JsonString value = StringValue<TAccess>(ref state, doc, valueIndex);
                return LookupDiscriminator(discriminator, Discriminator.StringTag, value.Span);
            }

            case JsonTokenType.True:
                return LookupDiscriminator(discriminator, Discriminator.BooleanTag, "true"u8);
            case JsonTokenType.False:
                return LookupDiscriminator(discriminator, Discriminator.BooleanTag, "false"u8);
            case JsonTokenType.Null:
                return LookupDiscriminator(discriminator, Discriminator.NullTag, "null"u8);
            case JsonTokenType.Number:
            {
                ReadOnlySpan<byte> raw = default(TAccess).RawValue(ref state, doc, valueIndex);

                // "3.0" may equal a keyed integer: no branch can be excluded.
                return IsCanonicalInteger(raw)
                    ? LookupDiscriminator(discriminator, Discriminator.NumberTag, raw)
                    : discriminator.AllBranches;
            }

            default:
                return discriminator.NonString;
        }
    }

    /// <summary>Looks a tagged value up in the discriminator's known values, falling back to the unknown-value list.</summary>
    private static int[] LookupDiscriminator(Discriminator discriminator, byte tag, ReadOnlySpan<byte> value)
    {
        return discriminator.KnownValues.TryGetValue(tag, value, out int[]? branches) ? branches : discriminator.UnknownString;
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
        InlineBits inline = default;
        Span<ulong> scratch = parentBits.Length <= InlineBitWords ? Words(ref inline) : (rented = ArrayPool<ulong>.Shared.Rent(parentBits.Length));
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