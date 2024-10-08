// <copyright file="JsonElementExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Keywords;

/// <summary>
/// Extension methods for <see cref="JsonElement"/>.
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Gets a value indicating whether the <see cref="JsonElement"/> has a keyword
    /// present.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IKeyword"/>.</typeparam>
    /// <param name="element">The schema element to test.</param>
    /// <param name="keyword">The keyword to test.</param>
    /// <returns><see langword="true"/> if the schema contains the keyword.</returns>
    public static bool HasKeyword<T>(this JsonElement element, T keyword)
       where T : notnull, IKeyword
    {
        return
            element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(keyword.KeywordUtf8, out _);
    }

    /// <summary>
    /// Tries to get the value of the keyword on the <see cref="JsonElement"/>.
    /// </summary>
    /// <typeparam name="T">The type of the keyword.</typeparam>
    /// <param name="element">The schema element to test.</param>
    /// <param name="keyword">The keyword to get.</param>
    /// <param name="value">The value of the keyword.</param>
    /// <returns><see langword="true"/> if the keyword is present on the type declaration.</returns>
    public static bool TryGetKeyword<T>(this JsonElement element, T keyword, out JsonElement value)
        where T : notnull, IKeyword
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        return element.TryGetProperty(keyword.KeywordUtf8, out value);
    }
}