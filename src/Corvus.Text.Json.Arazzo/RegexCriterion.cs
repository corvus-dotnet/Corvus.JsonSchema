// <copyright file="RegexCriterion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The runtime surface a generated executor calls when inlining a <c>regex</c> success criterion. The
/// pattern itself is compiled ahead-of-time by the regular-expression source generator (via
/// <c>[GeneratedRegex]</c> on the executor); these helpers resolve the criterion's context value and
/// run the match with the spec's behaviour — a non-string JSON value never matches, and a match that
/// times out (catastrophic backtracking) returns <see langword="false"/> rather than throwing.
/// </summary>
public static class RegexCriterion
{
    /// <summary>
    /// Matches a JSON-valued context against the regex. Only string values are matched (the unescaped
    /// UTF-8 content is transcoded to UTF-16 without allocating a managed string); any other value kind
    /// — including an absent/undefined value — does not match.
    /// </summary>
    /// <param name="regex">The source-generated regular expression.</param>
    /// <param name="value">The resolved context value.</param>
    /// <returns><see langword="true"/> if the value is a string that matches.</returns>
    public static bool IsMatch(Regex regex, in JsonElement value)
    {
        ArgumentNullException.ThrowIfNull(regex);

        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        using UnescapedUtf8JsonString unescaped = value.GetUtf8String();
        return IsMatchUtf8(regex, unescaped.Span);
    }

    /// <summary>
    /// Matches a <c>$statusCode</c> context against the regex, formatting the integer straight into a
    /// stack buffer (no managed string).
    /// </summary>
    /// <param name="regex">The source-generated regular expression.</param>
    /// <param name="statusCode">The response status code.</param>
    /// <returns><see langword="true"/> if the formatted status code matches.</returns>
    public static bool IsMatch(Regex regex, int statusCode)
    {
        ArgumentNullException.ThrowIfNull(regex);

        Span<char> buffer = stackalloc char[16];
        statusCode.TryFormat(buffer, out int written, default, CultureInfo.InvariantCulture);
        return IsMatch(regex, buffer[..written]);
    }

    /// <summary>
    /// Matches a UTF-16 context (a literal or already-resolved string) against the regex, returning
    /// <see langword="false"/> on a match timeout.
    /// </summary>
    /// <param name="regex">The source-generated regular expression.</param>
    /// <param name="input">The input to match.</param>
    /// <returns><see langword="true"/> if the input matches.</returns>
    public static bool IsMatch(Regex regex, ReadOnlySpan<char> input)
    {
        ArgumentNullException.ThrowIfNull(regex);

        try
        {
            return regex.IsMatch(input);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool IsMatchUtf8(Regex regex, ReadOnlySpan<byte> utf8)
    {
        const int StackThreshold = 256;

        int charCount = Encoding.UTF8.GetCharCount(utf8);
        char[]? rented = null;
        Span<char> chars = charCount <= StackThreshold ? stackalloc char[StackThreshold] : (rented = ArrayPool<char>.Shared.Rent(charCount));

        try
        {
            int written = Encoding.UTF8.GetChars(utf8, chars);
            return IsMatch(regex, chars[..written]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}