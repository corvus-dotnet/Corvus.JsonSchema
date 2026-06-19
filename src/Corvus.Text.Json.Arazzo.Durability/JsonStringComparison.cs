// <copyright file="JsonStringComparison.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Ordinal equality between a JSON string element and a managed value <strong>without realizing the JSON side as a heap
/// string</strong> — for the realize-to-compare hot paths (a config-key lookup, a base-workflow-id filter) that would
/// otherwise pay a <c>(string)</c> cast purely to compare and discard.
/// </summary>
public static class JsonStringComparison
{
    /// <summary>
    /// Whether <paramref name="element"/> is a JSON string equal to <paramref name="value"/>, comparing on the element's
    /// unescaped, transcoded UTF-16 (a pooled buffer the dispose returns) rather than a materialized string.
    /// </summary>
    /// <param name="element">The JSON element (a string value).</param>
    /// <param name="value">The managed string to compare against.</param>
    /// <returns><see langword="true"/> if the element is a string equal to <paramref name="value"/>.</returns>
    /// <remarks>For a constant target prefer the UTF-8 form — <c>element.GetUtf8String().Span.SequenceEqual("…"u8)</c> —
    /// which compares the element's native UTF-8 against a literal with no transcode at all.</remarks>
    public static bool EqualsString(this JsonElement element, string value)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        using UnescapedUtf16JsonString utf16 = element.GetUtf16String();
        return utf16.Span.SequenceEqual(value.AsSpan());
    }
}