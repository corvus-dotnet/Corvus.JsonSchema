// <copyright file="HeaderValueParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Zero-allocation helpers for parsing HTTP response header values
/// into strongly-typed JSON schema values.
/// </summary>
/// <remarks>
/// <para>
/// These methods are called by generated response structs to
/// lazily deserialize header values on first access.
/// </para>
/// <para>
/// Scalar values use <see cref="FixedJsonValueDocument{T}"/>
/// backed by a thread-local pool — zero heap allocation.
/// The document is registered with the workspace for lifetime management.
/// </para>
/// </remarks>
public static class HeaderValueParser
{
    /// <summary>
    /// Parses a raw header string as a JSON number value.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (e.g. "42", "3.14").</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseNumber<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        if (!IsJsonNumber(rawValue.AsSpan()))
        {
            // Text that is not a JSON number is bound as a string so that schema validation rejects it; stamping
            // arbitrary text as a number token would put a malformed number in front of the evaluator.
            return ParseString<T>(rawValue, workspace);
        }

        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForNumberFromSpan(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Parses a raw header string as a JSON string value.
    /// JSON escaping and quoting are applied automatically.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (unquoted, unescaped).</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseString<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForUnescapedString(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Parses a raw header string as a JSON boolean value.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (<c>true</c> or <c>false</c>).</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseBoolean<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        if (rawValue is not ("true" or "false"))
        {
            // Only the JSON literals are booleans; anything else is bound as a string so that schema validation
            // rejects it rather than becoming a malformed boolean token.
            return ParseString<T>(rawValue, workspace);
        }

        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForBooleanFromSpan(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Determines whether the text is a JSON number (RFC 8259 section 6): an optional minus, an integer part with no
    /// leading zero, an optional fraction and an optional exponent, and nothing else.
    /// </summary>
    private static bool IsJsonNumber(ReadOnlySpan<char> chars)
    {
        int i = 0;
        int length = chars.Length;

        if (i < length && chars[i] == '-')
        {
            i++;
        }

        if (i >= length)
        {
            return false;
        }

        if (chars[i] == '0')
        {
            i++;
        }
        else if (chars[i] is >= '1' and <= '9')
        {
            i = SkipDigits(chars, i);
        }
        else
        {
            return false;
        }

        if (i < length && chars[i] == '.')
        {
            i++;
            int start = i;
            i = SkipDigits(chars, i);
            if (i == start)
            {
                return false;
            }
        }

        if (i < length && chars[i] is 'e' or 'E')
        {
            i++;
            if (i < length && chars[i] is '+' or '-')
            {
                i++;
            }

            int start = i;
            i = SkipDigits(chars, i);
            if (i == start)
            {
                return false;
            }
        }

        return i == length;

        static int SkipDigits(ReadOnlySpan<char> chars, int i)
        {
            while (i < chars.Length && chars[i] is >= '0' and <= '9')
            {
                i++;
            }

            return i;
        }
    }
}