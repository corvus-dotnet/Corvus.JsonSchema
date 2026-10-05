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

    /// <summary>
    /// The location of a string value's text for comparison by words, with its length: -1 for a value that is
    /// escaped or that starts within eight bytes of the end of the text (which the set's lookup decides instead).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GenStringLocation(ref EvaluationState state, int valueIndex, out int length)
    {
        int location = default(RawAccess).RawValueLocation(ref state, null!, valueIndex, out length);
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

    /// <summary>Whether a property's name is valid against a <c>propertyNames</c> schema (the interpreter's test).</summary>
    internal static bool GenPropertyName(ref EvaluationState state, IJsonDocument doc, int valueIndex, ChildRef names) => EvalPropertyName<FastMode, RawAccess>(in names, doc, valueIndex, ref state, 0);

    /// <summary>Whether a property's name matches a pattern (the name unescaped first when it is escaped).</summary>
    internal static bool GenNameMatches(ref EvaluationState state, IJsonDocument doc, int valueIndex, PatternMatcher matcher) => MatchesName<RawAccess>(matcher, ref state, doc, valueIndex);

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

    /// <summary>
    /// The branches a discriminator selects for a value (the interpreter's selection), as a bit per branch; false
    /// when it selects nothing in particular and the keyword's other narrowing applies.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenSelectBranches(ref EvaluationState state, IJsonDocument doc, int index, Discriminator discriminator, out ulong selected)
    {
        selected = 0;
        if (!TrySelectBranches<RawAccess>(discriminator, ref state, doc, index, out int[] branches))
        {
            return false;
        }

        foreach (int branch in branches)
        {
            selected |= 1UL << branch;
        }

        return true;
    }

    /// <summary>A node's <c>const</c> at a value (the interpreter's comparison), for constants generated code does not compare in place.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenOwnConst(int nodeId, IJsonDocument doc, int index, JsonTokenType tokenType, ref EvaluationState state) => MatchesConst<RawAccess>(state.Nodes[nodeId], ref state, doc, index, tokenType);

    /// <summary>A node's <c>enum</c> at a value (the interpreter's comparison), for enums that are not all strings.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenOwnEnum(int nodeId, IJsonDocument doc, int index, JsonTokenType tokenType, ref EvaluationState state) => MatchesEnum<RawAccess>(state.Nodes[nodeId], ref state, doc, index, tokenType);

    /// <summary>A node's number keywords at a number value (the interpreter's evaluation).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenOwnNumber(int nodeId, IJsonDocument doc, int index, ref EvaluationState state) => EvalNumber<FastMode, RawAccess>(state.Nodes[nodeId], doc, index, ref state);

    /// <summary>A node's string keywords at a string value (the interpreter's evaluation).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenOwnString(int nodeId, IJsonDocument doc, int index, ref EvaluationState state) => EvalString<FastMode, RawAccess>(state.Nodes[nodeId], doc, index, ref state);

    /// <summary>
    /// Whether generated code may compare a plain integer literal against integer bounds as longs: the interpreter's
    /// fast path, unless it is switched off.
    /// </summary>
    internal static bool GenIntegerFastPath => !DisableIntegerFastPath;

    /// <summary>
    /// A number value as a long when its text is a plain integer literal of at most 18 characters (the condition of
    /// the interpreter's fast path); false for any other number, which takes the general comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GenTryLong(ref EvaluationState state, int index, out long value)
    {
        // The value's row has been read (checked) for its token type, so its first two words are within the rows:
        // they are read unchecked, and the text is checked once.
        ulong pair = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref MemoryMarshal.GetReference(state.RawRows), index));
        int location = (int)pair & RawAccess.LocationMask;
        int length = (int)(pair >> 32) & int.MaxValue;
        ReadOnlySpan<byte> text = state.RawUtf8;
        if (!BitConverter.IsLittleEndian || (ulong)(uint)location + (uint)length > (ulong)(uint)text.Length)
        {
            return TryPlainLong(default(RawAccess).RawValue(ref state, null!, index), out value);
        }

        // Up to eight digits with eight bytes of text to read (all but a number at the very end of the text): the
        // digits are tested and converted together, with no loop whose length the processor has to predict.
        ref byte start = ref Unsafe.Add(ref MemoryMarshal.GetReference(text), location);
        bool negative = length > 1 && start == (byte)'-';
        int digits = negative ? length - 1 : length;
        if ((uint)(digits - 1) < 8 && (ulong)(uint)location + (negative ? 9u : 8u) <= (ulong)(uint)text.Length)
        {
            ulong word = Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref start, negative ? 1 : 0));

            // The digits moved to the top bytes (the first digit lowest of them), with '0' in the bytes beneath.
            int spare = (8 - digits) * 8;
            word = (word << spare) | (spare == 0 ? 0UL : 0x3030303030303030UL >> (64 - spare));
            word -= 0x3030303030303030UL;
            if (((word | (word + 0x7676767676767676UL)) & 0x8080808080808080UL) != 0)
            {
                value = 0;
                return false;
            }

            word = (word * 10) + (word >> 8);
            long magnitude = (long)((((word & 0x000000FF000000FFUL) * 0x000F424000000064UL) + (((word >> 16) & 0x000000FF000000FFUL) * 0x0000271000000001UL)) >> 32);
            value = negative ? -magnitude : magnitude;
            return true;
        }

        return TryPlainLong(MemoryMarshal.CreateReadOnlySpan(ref start, length), out value);
    }

    /// <summary>
    /// Whether an array's items are all different (the interpreter's test: pairwise for a short array, otherwise a
    /// set of hashes).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenUniqueItems(ref EvaluationState state, IJsonDocument doc, int index)
    {
        int end = default(RawAccess).EndIndex(ref state, doc, index);
        int count = default(RawAccess).Count(ref state, doc, index, JsonTokenType.StartArray);
        if (count <= PairwiseLimit)
        {
            return AllUniquePairwise<RawAccess>(ref state, doc, index, end);
        }

        var set = new UniqueItemSet(count);
        try
        {
            for (int valueIndex = index + RowSize; valueIndex < end; valueIndex = default(RawAccess).NextIndex(ref state, doc, valueIndex))
            {
                if (!set.TryAdd<RawAccess>(ref state, doc, valueIndex))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            set.Dispose();
        }
    }

    /// <summary>Whether the parser found a string value's text to be unescaped and all ASCII (one character to a byte).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool GenIsAscii(ref EvaluationState state, int valueIndex) => default(RawAccess).IsAsciiText(ref state, null!, valueIndex);

    /// <summary>The byte length of a string value's text, negative when it has escapes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GenStringBytes(ref EvaluationState state, int valueIndex)
    {
        default(RawAccess).RawValueLocation(ref state, null!, valueIndex, out int length);
        return length;
    }

    /// <summary>
    /// Whether a string value's length in runes is within bounds, counted: for the values whose byte length does not
    /// decide it (a rune is one to four bytes), and for escaped values.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenStringLengthCounted(ref EvaluationState state, IJsonDocument doc, int index, int minLength, int maxLength)
    {
        ReadOnlySpan<byte> raw = default(RawAccess).RawValue(ref state, doc, index, out bool escaped);
        if (escaped)
        {
            return EscapedStringLengthWithin<RawAccess>(minLength, maxLength, ref state, doc, index);
        }

        // An unescaped string the parser found all ASCII has one character to a byte: its length is exact.
        return default(RawAccess).IsAsciiText(ref state, doc, index)
            ? (maxLength < 0 || raw.Length <= maxLength) && (minLength < 0 || raw.Length >= minLength)
            : LengthWithin(raw, minLength, maxLength);
    }

    /// <summary>Whether a property's name is a given name, for the names generated code does not compare by words (escaped, or at the very end of the text).</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool GenNameEquals(ref EvaluationState state, IJsonDocument doc, int valueIndex, byte[] name)
    {
        ReadOnlySpan<byte> raw = default(RawAccess).PropertyNameRaw(ref state, doc, valueIndex, out bool escaped);
        if (!escaped)
        {
            return raw.SequenceEqual(name);
        }

        using UnescapedUtf8JsonString unescaped = PropertyName<RawAccess>(ref state, doc, valueIndex);
        return unescaped.Span.SequenceEqual(name);
    }

    /// <summary>
    /// The branches a discriminator selects for the value of its property (the interpreter's selection), as a bit per
    /// branch: for the values generated code does not decide by words.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ulong GenSelectByValue(ref EvaluationState state, IJsonDocument doc, int valueIndex, Discriminator discriminator)
    {
        ulong selected = 0;
        foreach (int branch in SelectBranchesByValue<RawAccess>(discriminator, ref state, doc, valueIndex))
        {
            selected |= 1UL << branch;
        }

        return selected;
    }

    /// <summary>Whether a number value is an integer.</summary>
    internal static bool GenIsInteger(ref EvaluationState state, IJsonDocument doc, int index, bool lexical) => IsInteger<RawAccess>(ref state, doc, index, lexical);

    /// <summary>
    /// Whether a string value matches a pattern: the value's text where it lies when it has no escapes, and otherwise
    /// its unescaped text.
    /// </summary>
    internal static bool GenPattern(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, PatternMatcher matcher)
    {
        ReadOnlySpan<byte> raw = default(RawAccess).RawValue(ref state, doc, index, out bool escaped);
        if (!escaped)
        {
            return matcher.IsMatch(raw);
        }

        using UnescapedUtf8JsonString text = StringValue<RawAccess>(ref state, doc, index);
        return matcher.IsMatch(text.Span);
    }

    /// <summary>Whether a value is a string in a set.</summary>
    internal static bool GenStringSet(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, Utf8NameMap<object> allowed) => MatchesStringSet<RawAccess>(allowed, tokenType, ref state, doc, index);

    /// <summary>Whether a value is one string.</summary>
    internal static bool GenStringConst(ref EvaluationState state, IJsonDocument doc, int index, JsonTokenType tokenType, byte[] expected) => MatchesStringBytes<RawAccess>(expected, tokenType, ref state, doc, index);
}
#endif