// <copyright file="RegexExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Text.RegularExpressions;

namespace Corvus.Json;

/// <summary>
/// Regex extensions for Regex for netstandard2.0 compatibility.
/// </summary>
public static class RegexExtensions
{
    /// <summary>
    /// Gets a value indicating whether the regex matches the given value.
    /// </summary>
    /// <param name="regex">The regular expression.</param>
    /// <param name="input">The input value.</param>
    /// <returns><see langword="true"/> if the value is a match for the regular expression.</returns>
    public static bool IsMatch(this Regex regex, ReadOnlySpan<char> input)
    {
        return regex.IsMatch(input.ToString());
    }
}
#endif