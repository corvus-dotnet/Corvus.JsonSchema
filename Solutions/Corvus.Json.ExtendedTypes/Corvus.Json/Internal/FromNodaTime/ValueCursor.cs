// <copyright file="ValueCursor.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Copyright 2011 The Noda Time Authors. All rights reserved.
// Use of this source code is governed by the Apache License 2.0,
// as found in the LICENSE.txt file.
// </licensing>

#if NET8_0_OR_GREATER

using System.Diagnostics;
using System.Globalization;

namespace NodaTime.Text;

/// <summary>
/// Provides a cursor over text being parsed. None of the methods in this class throw exceptions (unless
/// there is a bug in Noda Time, in which case an exception is appropriate) and none of the methods
/// have ref parameters indicating failures, unlike subclasses. This class is used as the basis for both
/// value and pattern parsing, so can make no judgement about what's wrong (i.e. it wouldn't know what
/// type of failure to indicate). Instead, methods return Boolean values to indicate success or failure.
/// </summary>
[DebuggerStepThrough]
internal ref struct ValueCursor
{
    /// <summary>
    /// A nul character. This character is not allowed in any parsable string and is used to
    /// indicate that the current character is not set.
    /// </summary>
    internal const char Nul = '\0';

    /// <summary>
    /// Initializes a new instance to parse the given value.
    /// </summary>
    /// <param name="value">The value on which to base the cursor.</param>
    public ValueCursor(ReadOnlySpan<char> value)
    {
        // Validated by caller.
        this.Value = value;
        this.Length = value.Length;
        this.Move(-1);
    }

    /// <summary>
    /// Gets the length of the string being parsed.
    /// </summary>
    internal int Length { get; }

    /// <summary>
    /// Gets the string being parsed.
    /// </summary>
    internal ReadOnlySpan<char> Value { get; }

    /// <summary>
    /// Gets the current character.
    /// </summary>
    internal char Current { get; private set; }

    /////// <summary>
    /////// Gets a value indicating whether this instance has more characters.
    /////// </summary>
    /////// <value>
    /////// <c>true</c> if this instance has more characters; otherwise, <c>false</c>.
    /////// </value>
    ////internal readonly bool HasMoreCharacters => unchecked(this.Index + 1) < this.Length;

    /// <summary>
    /// Gets the current index into the string being parsed.
    /// </summary>
    internal int Index { get; private set; }

    /////// <summary>
    /////// Gets the remainder the string that has not been parsed yet.
    /////// </summary>
    ////internal readonly ReadOnlySpan<char> Remainder => this.Value[this.Index..];

    /// <summary>
    ///   Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    ///   A <see cref="string" /> that represents this instance.
    /// </returns>
    public override readonly string ToString() =>
#if NET8_0_OR_GREATER
        this.Index <= 0 ? $"^{this.Value}"
            : this.Index >= this.Length ? $"{this.Value}^"
            : this.Value.ToString().Insert(this.Index, "^");
#else
        this.Index <= 0 ? $"^{this.Value.ToString()}"
            : this.Index >= this.Length ? $"{this.Value.ToString()}^"
            : this.Value.ToString().Insert(this.Index, "^");
#endif

    /////// <summary>
    /////// Peek the next character.
    /////// </summary>
    /////// <returns>Returns the next character if there is one or <see cref="Nul" /> if there isn't.</returns>
    ////internal char PeekNext() => unchecked(this.HasMoreCharacters ? this.Value[this.Index + 1] : Nul);

    /// <summary>
    /// Moves the specified target index. If the new index is out of range of the valid indices
    /// for this string then the index is set to the beginning or the end of the string whichever
    /// is nearest the requested index.
    /// </summary>
    /// <param name="targetIndex">Index of the target.</param>
    /// <returns><c>true</c> if the requested index is in range.</returns>
    internal bool Move(int targetIndex)
    {
        unchecked
        {
            if (targetIndex >= 0)
            {
                if (targetIndex < this.Length)
                {
                    this.Index = targetIndex;
                    this.Current = this.Value[this.Index];
                    return true;
                }
                else
                {
                    this.Current = Nul;
                    this.Index = this.Length;
                    return false;
                }
            }

            this.Current = Nul;
            this.Index = -1;
            return false;
        }
    }

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
            int targetIndex = this.Index + 1;
            if (targetIndex < this.Length)
            {
                this.Index = targetIndex;
                this.Current = this.Value[this.Index];
                return true;
            }

            this.Current = Nul;
            this.Index = this.Length;
            return false;
        }
    }

    /////// <summary>
    /////// Moves to the previous character.
    /////// </summary>
    /////// <returns><c>true</c> if the requested index is in range.</returns>
    ////internal bool MovePrevious()
    ////{
    ////    unchecked
    ////    {
    ////        // Logically this is Move(Index - 1), but it's micro-optimized as we
    ////        // know we'll never hit the upper limit this way.
    ////        if (this.Index > 0)
    ////        {
    ////            this.Index--;
    ////            this.Current = this.Value[this.Index];
    ////            return true;
    ////        }

    ////        this.Current = Nul;
    ////        this.Index = -1;
    ////        return false;
    ////    }
    ////}

    /////// <summary>
    ///////   Attempts to match the specified character with the current character of the string. If the
    ///////   character matches then the index is moved passed the character.
    /////// </summary>
    /////// <param name="character">The character to match.</param>
    /////// <returns><c>true</c> if the character matches.</returns>
    ////internal bool Match(char character)
    ////{
    ////    if (this.Current == character)
    ////    {
    ////        this.MoveNext();
    ////        return true;
    ////    }

    ////    return false;
    ////}

    /////// <summary>
    /////// Attempts to match the specified string with the current point in the string. If the
    /////// character matches then the index is moved past the string.
    /////// </summary>
    /////// <param name="match">The string to match.</param>
    /////// <returns><c>true</c> if the string matches.</returns>
    ////internal bool Match(ReadOnlySpan<char> match)
    ////{
    ////    unchecked
    ////    {
    ////        if (match.Length > this.Value.Length - this.Index)
    ////        {
    ////            return false;
    ////        }

    ////        if (this.Value.Slice(this.Index, match.Length).SequenceEqual(match))
    ////        {
    ////            this.Move(this.Index + match.Length);
    ////            return true;
    ////        }

    ////        return false;
    ////    }
    ////}

    /////// <summary>
    /////// Attempts to match the specified string with the current point in the string in a case-insensitive
    /////// manner, according to the given comparison info. The cursor is optionally updated to the end of the match.
    /////// </summary>
    /////// <param name="match">The value to match.</param>
    /////// <param name="compareInfo">The comparison type to use.</param>
    /////// <param name="moveOnSuccess">If <see langword="true"/>, advance <see cref="Index"/> on success.</param>
    /////// <returns><c>True</c> if there is a case-insensitive match.</returns>
    ////internal bool MatchCaseInsensitive(ReadOnlySpan<char> match, CompareInfo compareInfo, bool moveOnSuccess)
    ////{
    ////    unchecked
    ////    {
    ////        if (match.Length > this.Value.Length - this.Index)
    ////        {
    ////            return false;
    ////        }

    ////        // Note: This will fail if the length in the input string is different to the length in the
    ////        // match string for culture-specific reasons. It's not clear how to handle that...
    ////        // See issue 210 for details - we're not intending to fix this, but it's annoying.
    ////        if (compareInfo.Compare(this.Value.Slice(this.Index, match.Length), match, CompareOptions.IgnoreCase) == 0)
    ////        {
    ////            if (moveOnSuccess)
    ////            {
    ////                this.Move(this.Index + match.Length);
    ////            }

    ////            return true;
    ////        }

    ////        return false;
    ////    }
    ////}

    /////// <summary>
    /////// Compares the value from the current cursor position with the given match. If the
    /////// given match string is longer than the remaining length, the comparison still goes
    /////// ahead but the result is never 0: if the result of comparing to the end of the
    /////// value returns 0, the result is -1 to indicate that the value is earlier than the given match.
    /////// Conversely, if the remaining value is longer than the match string, the comparison only
    /////// goes as far as the end of the match. So "xabcd" with the cursor at "a" will return 0 when
    /////// matched with "abc".
    /////// </summary>
    /////// <param name="match">The value with which to match.</param>
    /////// <returns>A negative number if the value (from the current cursor position) is lexicographically
    /////// earlier than the given match string; 0 if they are equal (as far as the end of the match) and
    /////// a positive number if the value is lexicographically later than the given match string.</returns>
    ////internal readonly int CompareOrdinal(ReadOnlySpan<char> match)
    ////{
    ////    int remaining = this.Value.Length - this.Index;
    ////    if (match.Length > remaining)
    ////    {
    ////        int ret = this.Value[this.Index..].CompareTo(match[..remaining], StringComparison.Ordinal);
    ////        return ret == 0 ? -1 : ret;
    ////    }

    ////    return this.Value.Slice(this.Index, match.Length).CompareTo(match, StringComparison.Ordinal);
    ////}

    /// <summary>
    /// Parses digits at the current point in the string as a signed 64-bit integer value.
    /// Currently this method only supports cultures whose negative sign is "-" (and
    /// using ASCII digits).
    /// </summary>
    /// <param name="result">The result integer value. The value of this is not guaranteed
    /// to be anything specific if the return value is non-null.</param>
    /// <returns><see langword="true"/> if the value was parsed successfully.</returns>
    internal bool ParseInt64(out long result)
    {
        unchecked
        {
            result = 0L;
            int startIndex = this.Index;
            bool negative = this.Current == '-';
            if (negative)
            {
                if (!this.MoveNext())
                {
                    this.Move(startIndex);
                    return false;
                }
            }

            int count = 0;
            int digit;
            while (result < 922337203685477580 && (digit = this.GetDigit()) != -1)
            {
                result = (result * 10) + digit;
                count++;
                if (!this.MoveNext())
                {
                    break;
                }
            }

            if (count == 0)
            {
                this.Move(startIndex);
                return false;
            }

            if (result >= 922337203685477580 && (digit = this.GetDigit()) != -1)
            {
                if (result > 922337203685477580)
                {
                    return false;
                }

                if (negative && digit == 8)
                {
                    this.MoveNext();
                    result = long.MinValue;
                    return true;
                }

                if (digit > 7)
                {
                    return false;
                }

                // We know we can cope with this digit...
                result = (result * 10) + digit;
                this.MoveNext();
                if (this.GetDigit() != -1)
                {
                    // Too many digits. Die.
                    return false;
                }
            }

            if (negative)
            {
                result = -result;
            }

            return true;
        }
    }

    /////// <summary>
    /////// Parses digits at the current point in the string, as an <see cref="long"/> value.
    /////// If the minimum required
    /////// digits are not present then the index is unchanged. If there are more digits than
    /////// the maximum allowed they are ignored.
    /////// </summary>
    /////// <param name="minimumDigits">The minimum allowed digits.</param>
    /////// <param name="maximumDigits">The maximum allowed digits.</param>
    /////// <param name="result">The result integer value. The value of this is not guaranteed
    /////// to be anything specific if the return value is false.</param>
    /////// <returns><c>true</c> if the digits were parsed.</returns>
    ////internal bool ParseInt64Digits(int minimumDigits, int maximumDigits, out long result)
    ////{
    ////    unchecked
    ////    {
    ////        result = 0;
    ////        int localIndex = this.Index;
    ////        int maxIndex = localIndex + maximumDigits;
    ////        if (maxIndex >= this.Length)
    ////        {
    ////            maxIndex = this.Length;
    ////        }

    ////        for (; localIndex < maxIndex; localIndex++)
    ////        {
    ////            // Optimized digit handling: rather than checking for the range, returning -1
    ////            // and then checking whether the result is -1, we can do both checks at once.
    ////            int digit = this.Value[localIndex] - '0';
    ////            if (digit < 0 || digit > 9)
    ////            {
    ////                break;
    ////            }

    ////            result = (result * 10) + digit;
    ////        }

    ////        int count = localIndex - this.Index;
    ////        if (count < minimumDigits)
    ////        {
    ////            return false;
    ////        }

    ////        this.Move(localIndex);
    ////        return true;
    ////    }
    ////}

    /////// <summary>
    /////// Parses digits at the current point in the string. If the minimum required
    /////// digits are not present then the index is unchanged. If there are more digits than
    /////// the maximum allowed they are ignored.
    /////// </summary>
    /////// <param name="minimumDigits">The minimum allowed digits.</param>
    /////// <param name="maximumDigits">The maximum allowed digits.</param>
    /////// <param name="result">The result integer value. The value of this is not guaranteed
    /////// to be anything specific if the return value is false.</param>
    /////// <returns><c>true</c> if the digits were parsed.</returns>
    ////internal bool ParseDigits(int minimumDigits, int maximumDigits, out int result)
    ////{
    ////    unchecked
    ////    {
    ////        result = 0;
    ////        int localIndex = this.Index;
    ////        int maxIndex = localIndex + maximumDigits;
    ////        if (maxIndex >= this.Length)
    ////        {
    ////            maxIndex = this.Length;
    ////        }

    ////        for (; localIndex < maxIndex; localIndex++)
    ////        {
    ////            // Optimized digit handling: rather than checking for the range, returning -1
    ////            // and then checking whether the result is -1, we can do both checks at once.
    ////            int digit = this.Value[localIndex] - '0';
    ////            if (digit < 0 || digit > 9)
    ////            {
    ////                break;
    ////            }

    ////            result = (result * 10) + digit;
    ////        }

    ////        int count = localIndex - this.Index;
    ////        if (count < minimumDigits)
    ////        {
    ////            return false;
    ////        }

    ////        this.Move(localIndex);
    ////        return true;
    ////    }
    ////}

    /// <summary>
    /// Parses digits at the current point in the string as a fractional value.
    /// </summary>
    /// <param name="maximumDigits">The maximum allowed digits. Trusted to be less than or equal to scale.</param>
    /// <param name="scale">The scale of the fractional value.</param>
    /// <param name="result">The result value scaled by scale. The value of this is not guaranteed
    /// to be anything specific if the return value is false.</param>
    /// <param name="minimumDigits">The minimum number of digits that must be specified in the value.</param>
    /// <returns><c>true</c> if the digits were parsed.</returns>
    internal bool ParseFraction(int maximumDigits, int scale, out int result, int minimumDigits)
    {
        unchecked
        {
            result = 0;
            int localIndex = this.Index;
            int minIndex = localIndex + minimumDigits;
            if (minIndex > this.Length)
            {
                // If we don't have all the digits we're meant to have, we can't possibly succeed.
                return false;
            }

            int maxIndex = Math.Min(localIndex + maximumDigits, this.Length);
            for (; localIndex < maxIndex; localIndex++)
            {
                // Optimized digit handling: rather than checking for the range, returning -1
                // and then checking whether the result is -1, we can do both checks at once.
                int digit = this.Value[localIndex] - '0';
                if (digit < 0 || digit > 9)
                {
                    break;
                }

                result = (result * 10) + digit;
            }

            int count = localIndex - this.Index;

            // Couldn't parse the minimum number of digits required?
            if (count < minimumDigits)
            {
                return false;
            }

            result = (int)(result * Math.Pow(10.0, scale - count));
            this.Move(localIndex);
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
            int c = this.Current;
            return c < '0' || c > '9' ? -1 : c - '0';
        }
    }
}

#endif