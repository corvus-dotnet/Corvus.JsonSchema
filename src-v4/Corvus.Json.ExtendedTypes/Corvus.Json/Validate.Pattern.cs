// <copyright file="Validate.Pattern.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !BUILDING_SOURCE_GENERATOR
using System.Runtime.CompilerServices;

namespace Corvus.Json;

/// <summary>
/// Matches the <c>pattern</c> and <c>patternProperties</c> expressions of generated code.
/// </summary>
public static partial class Validate
{
    /// <summary>
    /// Whether a string matches an ECMA-262 regular expression, the Corvus.Regex.Ecma262 matcher generated code holds
    /// for a <c>pattern</c> or <c>patternProperties</c> pattern.
    /// </summary>
    /// <param name="pattern">The regular expression.</param>
    /// <param name="input">The string.</param>
    /// <returns><see langword="true"/> if the string matches.</returns>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The match ran for longer than the
    /// expression's match timeout, or needed more backtracking memory than a match may hold. It is the exception of
    /// .NET's engine, as a match by a .NET <see cref="System.Text.RegularExpressions.Regex"/> would throw, so a caller
    /// that catches it sees the same.</exception>
    public static bool MatchesPattern(Corvus.Ecma262.RegularExpressions.Regex pattern, ReadOnlySpan<char> input)
    {
        try
        {
            return pattern.IsMatch(input);
        }
        catch (Corvus.Ecma262.RegularExpressions.RegexMatchTimeoutException)
        {
            ThrowRegexMatchTimeout(pattern, input);
            return false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowRegexMatchTimeout(Corvus.Ecma262.RegularExpressions.Regex pattern, ReadOnlySpan<char> input)
    {
        throw new System.Text.RegularExpressions.RegexMatchTimeoutException(input.ToString(), pattern.Pattern, pattern.MatchTimeout);
    }
}
#endif