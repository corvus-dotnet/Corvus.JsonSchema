// <copyright file="MultipartBinaryParts.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Helpers for materializing binary part slices recorded during multipart
/// deserialization over an owned body.
/// </summary>
public static class MultipartBinaryParts
{
    /// <summary>
    /// Creates the list of binary part slices for the recorded (offset, length)
    /// pairs over the owned body bytes, in recording order.
    /// </summary>
    /// <param name="bodyBytes">The owned whole-body bytes.</param>
    /// <param name="offsets">The recorded (offset, length) pairs.</param>
    /// <returns>One slice per recorded pair. The slices are views over
    /// <paramref name="bodyBytes"/> and share its lifetime.</returns>
    public static IReadOnlyList<ReadOnlyMemory<byte>> Slice(
        ReadOnlyMemory<byte> bodyBytes,
        List<(int Offset, int Length)> offsets)
    {
        if (offsets.Count == 0)
        {
            return [];
        }

        ReadOnlyMemory<byte>[] slices = new ReadOnlyMemory<byte>[offsets.Count];
        for (int i = 0; i < offsets.Count; i++)
        {
            (int offset, int length) = offsets[i];
            slices[i] = bodyBytes.Slice(offset, length);
        }

        return slices;
    }
}