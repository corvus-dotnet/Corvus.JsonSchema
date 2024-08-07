// <copyright file="StandardRegex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Corvus.Json.Internal;

/// <summary>
/// Standard regex format support.
/// </summary>
public static partial class StandardRegex
{
    /// <summary>
    /// The empty regular expression.
    /// </summary>
    public static readonly Regex Empty = EmptyRegex();

    /// <summary>
    /// Format a regular expression to a string.
    /// </summary>
    /// <param name="value">The <see cref="Regex"/> to format.</param>
    /// <returns>The string representation of the regular expression.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string FormatRegex(Regex value)
    {
        return value.ToString();
    }

    /// <summary>
    /// Try to parse a regular expression to a <see cref="Regex"/>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="options">The <see cref="RegexOptions"/> to use.</param>
    /// <param name="value">The resulting <see cref="Regex"/>, or <see cref="Empty"/> if it was not possible to parse the regular expression.</param>
    /// <returns><see langword="true"/> if it was possible to parse the regular expression.</returns>
    public static bool TryParseRegex(string? text, RegexOptions options, out Regex value)
    {
        if (text is string s)
        {
            try
            {
                value = new Regex(s, options);
                return true;
            }
            catch
            {
                // Fall through.
            }
        }

        value = Empty;
        return false;
    }

#if NET8_0_OR_GREATER
    [GeneratedRegex(".*", RegexOptions.None)]
    private static partial Regex EmptyRegex();
#else
    private static Regex EmptyRegex() => new(".*", RegexOptions.Compiled);
#endif
}