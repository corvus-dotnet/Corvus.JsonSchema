// <copyright file="Evaluator.Generated.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET && !STJ
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator.Compilation;

namespace Corvus.Text.Json.RuntimeEvaluator.Evaluation;

/// <summary>
/// What generated code calls: the interpreter's reads of the raw rows and its out-of-line value tests, closed over
/// <see cref="RawAccess"/> so that a backend names a plain static method. The row reads are inlined into generated
/// methods; the slow paths are not.
/// </summary>
internal static partial class Evaluator
{
    /// <summary>The token type of the value at a row index.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsonTokenType GenToken(ref EvaluationState state, int index) => default(RawAccess).TokenType(ref state, null!, index);

    /// <summary>An object's property count, or an array's length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GenCount(ref EvaluationState state, int index) => default(RawAccess).Count(ref state, null!, index, JsonTokenType.StartObject);

    /// <summary>An object's or array's end row, its rows checked once: the reads inside the container are unchecked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GenEnd(ref EvaluationState state, int index)
    {
        int end = default(RawAccess).EndIndex(ref state, null!, index);
        if (!default(RawAccess).RowsAvailable(ref state, null!, end))
        {
            ThrowMalformedRows();
        }

        return end;
    }

    /// <summary>A property value's or an item's token type, and the row after it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static JsonTokenType GenTokenAndNext(ref EvaluationState state, int valueIndex, out int next) => default(RawAccess).TokenTypeAndNextUnchecked(ref state, null!, valueIndex, out next);

    /// <summary>
    /// The location of a property's name for comparison by words, with its length: -1 for a name that is escaped or
    /// that starts within eight bytes of the end of the text (which <see cref="GenSlowName"/> looks up instead).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GenName(ref EvaluationState state, int valueIndex, out int length)
    {
        int location = default(RawAccess).PropertyNameLocationUnchecked(ref state, null!, valueIndex, out length);
        return length >= 0 && (ulong)(uint)location + sizeof(ulong) <= (ulong)(uint)state.RawUtf8.Length ? location : -1;
    }

    /// <summary>Eight bytes of the text, which <see cref="GenName"/> said are there.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GenWord(ref EvaluationState state, int location) => Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(state.RawUtf8), (nint)(uint)location));

    /// <summary>The index of a property's name in a name map, or -1: the interpreter's lookup, for the names generated code does not compare by words.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int GenSlowName<T>(ref EvaluationState state, IJsonDocument doc, int valueIndex, Utf8NameMap<T> names)
        where T : class
    {
        int location = default(RawAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
        if (length >= 0)
        {
            return names.GetIndex(state.RawUtf8, location, length);
        }

        using UnescapedUtf8JsonString name = PropertyName<RawAccess>(ref state, doc, valueIndex);
        return names.TryGetIndex(name.Span, out int index) ? index : -1;
    }

    /// <summary>
    /// The interpreter's general evaluation of a node the program compiled, for the values a generated method does
    /// not handle itself (a fused object's method, given a value that is not an object). The node is the program's
    /// own, not the generated copy, whose plan would call the method again.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenEvalGeneral(int nodeId, IJsonDocument doc, int index, ref EvaluationState state)
    {
        return Eval<FastMode, RawAccess>(state.Program.Nodes[nodeId], doc, index, ref state, default, 0);
    }

    /// <summary>Whether a property's name matches a pattern (the name unescaped first when it is escaped).</summary>
    internal static bool GenNameMatches(ref EvaluationState state, IJsonDocument doc, int valueIndex, PatternMatcher matcher) => MatchesName<RawAccess>(matcher, ref state, doc, valueIndex);

    /// <summary>A node's own local keywords (type, const, enum, number and string constraints) at a value: the interpreter's leaf evaluation.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenOwnLeaf(int nodeId, IJsonDocument doc, int index, ref EvaluationState state) => EvalLeafFast<RawAccess>(state.Nodes[nodeId], doc, index, ref state);

    /// <summary>
    /// A fused entry's value tests at a property's value (the interpreter's): each condition whose test the value
    /// fails gets its bit set in <paramref name="failed"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void GenFusedValueTests(ref EvaluationState state, IJsonDocument doc, int valueIndex, JsonTokenType valueType, FusedEntry entry, int conditions, ref ulong failed)
    {
        Span<bool> flags = stackalloc bool[64];
        flags = flags[..conditions];
        flags.Clear();
        ApplyValueTests<RawAccess>(entry, valueType, ref state, doc, valueIndex, flags);
        for (int i = 0; i < flags.Length; i++)
        {
            if (flags[i])
            {
                failed |= 1UL << i;
            }
        }
    }

    /// <summary>Whether a number value is an integer.</summary>
    internal static bool GenIsInteger(ref EvaluationState state, IJsonDocument doc, int index, bool lexical) => IsInteger<RawAccess>(ref state, doc, index, lexical);

    /// <summary>Whether a value is a string in a set.</summary>
    internal static bool GenStringSet(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, Utf8NameMap<object> allowed) => MatchesStringSet<RawAccess>(allowed, tokenType, ref state, doc, index);

    /// <summary>Whether a value is one string.</summary>
    internal static bool GenStringConst(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, byte[] expected) => MatchesStringBytes<RawAccess>(expected, tokenType, ref state, doc, index);

    /// <summary>Whether a value satisfies a string-length leaf.</summary>
    internal static bool GenLengthLeaf(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, in StrictEntry entry) => LengthLeafMatches<RawAccess>(in entry, tokenType, ref state, doc, index);
}
#endif