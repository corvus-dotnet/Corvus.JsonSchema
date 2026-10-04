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

    /// <summary>The index of a property's name in the node's properties, or -1: the interpreter's lookup, for the names generated code does not compare by words.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static int GenSlowName(ref EvaluationState state, IJsonDocument doc, int valueIndex, Utf8NameMap<PropertyEntry> properties)
    {
        int location = default(RawAccess).PropertyNameLocationUnchecked(ref state, doc, valueIndex, out int length);
        return length >= 0
            ? properties.GetIndex(state.RawUtf8, location, length)
            : LookupEscapedName<RawAccess>(properties, ref state, doc, valueIndex);
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