// <copyright file="Reduction.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for type reduction.
/// </summary>
public static class Reduction
{
    /// <summary>
    /// Determine if a non-reducing keyword applies to a particular schema value.
    /// </summary>
    /// <param name="schemaValue">The schema value to test.</param>
    /// <param name="keyword">The name of the non-reducing keyword.</param>
    /// <returns><see langword="false"/> if the non-reducing keyword is present on the value
    /// (and thus the schema cannot be reduced), otherwise <see langword="true"/> and the
    /// schema can be reduced.</returns>
    public static bool CanReduceNonReducingKeyword(in JsonElement schemaValue, ReadOnlySpan<byte> keyword) =>
        schemaValue.ValueKind != JsonValueKind.Object || !schemaValue.TryGetProperty(keyword, out _);
}