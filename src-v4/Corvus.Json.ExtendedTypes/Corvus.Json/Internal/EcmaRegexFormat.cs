// <copyright file="EcmaRegexFormat.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if BUILDING_SOURCE_GENERATOR
using EcmaRegexEdition = Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexEdition;
using EcmaRegexUnicodeVersion = Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexUnicodeVersion;
#else
using EcmaRegexEdition = Corvus.Ecma262.RegularExpressions.RegexEdition;
using EcmaRegexUnicodeVersion = Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion;
#endif

namespace Corvus.Json.Internal;

/// <summary>
/// The <c>regex</c> format: whether a string is a valid ECMA-262 regular expression in the grammar of the <c>u</c>
/// flag, decided by Corvus.Regex.Ecma262, the engine generated code matches patterns with.
/// </summary>
internal static class EcmaRegexFormat
{
    /// <summary>
    /// Whether a string is a valid ECMA-262 regular expression in the grammar of the <c>u</c> flag of today's ECMAScript,
    /// as the JSON Schema Test Suite reads <c>format: regex</c> (<c>\a</c>, which only the grammar of no flag accepts, is
    /// not one).
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsValid(ReadOnlySpan<char> value) => IsValid(value, EcmaRegexEdition.Current, EcmaRegexUnicodeVersion.Default);

    /// <summary>
    /// Whether a string is a valid ECMA-262 regular expression in the grammar of the <c>u</c> flag, of the edition and
    /// version of Unicode given: the ones the dialect reads its patterns with. The format is the grammar alone: a
    /// pattern over one of the engine's limits of size is valid, though a schema that held it as a <c>pattern</c> would
    /// be refused.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="edition">The edition of the grammar.</param>
    /// <param name="unicodeVersion">The version of Unicode.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsValid(ReadOnlySpan<char> value, EcmaRegexEdition edition, EcmaRegexUnicodeVersion unicodeVersion)
    {
#if BUILDING_SOURCE_GENERATOR
        // The source generator carries the engine as Corvus.Regex.Ecma262.CodeGeneration.Sources, which compiles a
        // pattern to say whether it is valid.
        return Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexEmitter.TryValidate(
            value.ToString(),
            Corvus.Ecma262.RegularExpressions.CodeGeneration.RegexFlags.Unicode,
            edition,
            unicodeVersion,
            out _,
            out bool tooLarge) || tooLarge;
#else
        // Nothing is allocated.
        return Corvus.Ecma262.RegularExpressions.Regex.IsValidPattern(value, Corvus.Ecma262.RegularExpressions.RegexFlags.Unicode, edition, unicodeVersion);
#endif
    }
}