// <copyright file="ValueStringBuilder.Replace.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <license>
// Derived from code Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See https:// github.com/dotnet/runtime/blob/c1049390d5b33483203f058b0e1457d2a1f62bf4/src/libraries/Common/src/System/Text/ValueStringBuilder.cs
// </license>
namespace Corvus.Text;

internal ref partial struct Utf8ValueStringBuilder
{
    public void Replace(
        scoped ReadOnlySpan<byte> oldValue,
        scoped ReadOnlySpan<byte> newValue,
        int startIndex,
        int count)
    {
        if (startIndex < 0 || (startIndex + count) > _pos)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (count == 0)
        {
            return;
        }

        Span<byte> rangeBuffer = _bytes.Slice(startIndex, count);

        int diff = newValue.Length - oldValue.Length;
        if (diff == 0)
        {
            int matchIndex = rangeBuffer.IndexOf(oldValue);
            if (matchIndex == -1)
            {
                return;
            }

            Span<byte> remainingBuffer = rangeBuffer;
            do
            {
                remainingBuffer = remainingBuffer[matchIndex..];
                newValue.CopyTo(remainingBuffer);
                remainingBuffer = remainingBuffer[oldValue.Length..];

                matchIndex = remainingBuffer.IndexOf(oldValue);
            } while (matchIndex != -1);

            return;
        }

        if (diff < 0)
        {
            int matchIndex = rangeBuffer.IndexOf(oldValue);
            if (matchIndex == -1)
            {
                return;
            }

            // We will never need to grow the buffer, but we might need to shift byteacters
            // down.
            Span<byte> remainingTargetBuffer = _bytes[(startIndex + matchIndex)..this._pos];
            Span<byte> remainingSourceBuffer = remainingTargetBuffer;
            int endOfSearchRangeRelativeToRemainingSourceBuffer = count - matchIndex;
            do
            {
                this._pos += diff;

                newValue.CopyTo(remainingTargetBuffer);

                remainingSourceBuffer = remainingSourceBuffer[oldValue.Length..];
                endOfSearchRangeRelativeToRemainingSourceBuffer -= oldValue.Length;
                remainingTargetBuffer = remainingTargetBuffer[newValue.Length..];

                matchIndex = remainingSourceBuffer[..endOfSearchRangeRelativeToRemainingSourceBuffer]
                    .IndexOf(oldValue);

                int lengthOfChunkToRelocate = matchIndex == -1
                    ? remainingSourceBuffer.Length
                    : matchIndex;
                remainingSourceBuffer[..lengthOfChunkToRelocate].CopyTo(remainingTargetBuffer);

                remainingSourceBuffer = remainingSourceBuffer[lengthOfChunkToRelocate..];
                endOfSearchRangeRelativeToRemainingSourceBuffer -= lengthOfChunkToRelocate;
                remainingTargetBuffer = remainingTargetBuffer[lengthOfChunkToRelocate..];
            } while (matchIndex != -1);

            return;
        }
        else
        {
            int matchIndex = rangeBuffer.IndexOf(oldValue);
            if (matchIndex == -1)
            {
                return;
            }

            Span<int> matchIndexes = stackalloc int[(rangeBuffer.Length + oldValue.Length - 1) / oldValue.Length];

            int matchCount = 0;
            int currentRelocationDistance = 0;
            while (matchIndex != -1)
            {
                matchIndexes[matchCount++] = matchIndex;
                currentRelocationDistance += diff;

                int nextIndex = rangeBuffer[(matchIndex + oldValue.Length)..].IndexOf(oldValue);
                matchIndex = nextIndex == -1 ? -1 : matchIndex + nextIndex + oldValue.Length;
            }

            int relocationRangeEndIndex = this._pos;

            int growBy = (this._pos + currentRelocationDistance) - _bytes.Length;
            if (growBy > 0)
            {
                Grow(growBy);
            }

            this._pos += currentRelocationDistance;

            // We work from the back of the string when growing to avoid having to
            // shift anything more than once.
            do
            {
                matchIndex = matchIndexes[matchCount - 1];

                int relocationTargetStart = startIndex + matchIndex + oldValue.Length + currentRelocationDistance;
                int relocationSourceStart = startIndex + matchIndex + oldValue.Length;
                int endOfSearchRangeRelativeToRemainingSourceBuffer = count - matchIndex;

                Span<byte> relocationTargetBuffer = _bytes[relocationTargetStart..];
                Span<byte> sourceBuffer = _bytes[relocationSourceStart..relocationRangeEndIndex];

                sourceBuffer.CopyTo(relocationTargetBuffer);

                currentRelocationDistance -= diff;
                Span<byte> replaceTargetBuffer = this._bytes.Slice(startIndex + matchIndex + currentRelocationDistance);
                newValue.CopyTo(replaceTargetBuffer);

                relocationRangeEndIndex = matchIndex + startIndex;
                matchIndex = rangeBuffer[..matchIndex].LastIndexOf(oldValue);

                matchCount--;
            } while (matchCount > 0);
        }
    }
}