// <copyright file="Utf8ValueCursor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace NodaTime.Text;

/// <summary>
/// Provides a cursor over text being parsed. None of the methods in this class throw exceptions (unless
/// there is a bug in Noda Time, in which case an exception is appropriate) and none of the methods
/// have ref parameters indicating failures, unlike subclasses. This class is used as the basis for both
/// value and pattern parsing, so can make no judgement about what's wrong (i.e. it wouldn't know what
/// type of failure to indicate). Instead, methods return Boolean values to indicate success or failure.
/// </summary>
[DebuggerStepThrough]
internal ref struct Utf8ValueCursor
{
    /// <summary>
    /// A nul character. This character is not allowed in any string that can be parsed, and is used to
    /// indicate that the current character is not set.
    /// </summary>
    internal const byte Nul = (byte)'\0';

    /// <summary>
    /// Initializes a new instance to parse the given value.
    /// </summary>
    /// <param name="value">The value on which to base the cursor.</param>
    public Utf8ValueCursor(ReadOnlySpan<byte> value)
    {
        // Validated by caller.
        Value = value;
        Length = value.Length;
        Current = Nul;
        Index = -1;
    }

    /// <summary>
    /// Gets the current character.
    /// </summary>
    internal byte Current { get; private set; }

    /// <summary>
    /// Gets the current index into the string being parsed.
    /// </summary>
    internal int Index { get; private set; }

    /// <summary>
    /// Gets the length of the string being parsed.
    /// </summary>
    internal int Length { get; }

    /// <summary>
    /// Gets the string being parsed.
    /// </summary>
    internal ReadOnlySpan<byte> Value { get; }

    /// <summary>
    /// Moves to the next character.
    /// </summary>
    /// <returns><c>true</c> if the requested index is in range.</returns>
    internal bool MoveNext()
    {
        unchecked
        {
            // Logically this is Move(Index + 1), but it's micro-optimized as we
            // know we'll never hit the lower limit this way.
            int targetIndex = Index + 1;
            if (targetIndex < Length)
            {
                Index = targetIndex;
                Current = Value[Index];
                return true;
            }

            Current = Nul;
            Index = Length;
            return false;
        }
    }

    /// <summary>
    /// Parses digits at the current point in the string as a non-negative 64-bit integer value.
    /// </summary>
    /// <param name="result">The result integer value. The value of this is not guaranteed
    /// to be anything specific if the return value is non-null.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    /// <remarks>
    /// The caller (Period parser) guarantees: (1) the cursor is at a digit, (2) no leading '-'.
    /// </remarks>
    internal bool ParseInt64(out long result)
    {
        unchecked
        {
            result = 0L;

            int count = 0;
            int digit;
            while (result < 922337203685477580 && (digit = GetDigit()) != -1)
            {
                result = (result * 10) + digit;
                count++;
                if (!MoveNext())
                {
                    break;
                }
            }

            if (count == 0)
            {
                return false;
            }

            if (result >= 922337203685477580 && (digit = GetDigit()) != -1)
            {
                if (result > 922337203685477580)
                {
                    return false;
                }

                if (digit > 7)
                {
                    return false;
                }

                // We know we can cope with this digit...
                result = (result * 10) + digit;
                MoveNext();
                if (GetDigit() != -1)
                {
                    // Too many digits. Die.
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the integer value of the current digit character, or -1 for "not a digit".
    /// </summary>
    /// <remarks>
    /// This currently only handles ASCII digits, which is all we have to parse to stay in line with the BCL.
    /// </remarks>
    private readonly int GetDigit()
    {
        unchecked
        {
            int c = Current;
            return c < '0' || c > '9' ? -1 : c - '0';
        }
    }
}