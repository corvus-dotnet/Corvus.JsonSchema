// <copyright file="EnvironmentName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The environment-name grammar: 1 to 63 characters of lowercase ASCII letters, digits and hyphen, not
/// beginning or ending with a hyphen (a DNS-label shape).
/// </summary>
/// <remarks>
/// The name is half of the composite <c>(environment, runId)</c> primary key under which every backend
/// stores a run (ADR 0065 §9), including the backends that encode that key as a delimited string (Redis key
/// segments, NATS KV keys, Azure Table partition keys). The grammar excludes every delimiter and forbidden
/// character those encodings involve — <c>:</c>, <c>.</c>, <c>/</c>, <c>\</c>, <c>#</c>, <c>?</c>,
/// whitespace, control characters — so a composite key can never be aliased by a crafted name. The
/// contract's <c>EnvironmentName</c> schema enforces the same pattern at the HTTP ingress; these predicates
/// guard the in-process construction leaves (the <see cref="Environment"/> draft factories).
/// </remarks>
public static class EnvironmentName
{
    /// <summary>The maximum length of an environment name.</summary>
    public const int MaxLength = 63;

    /// <summary>Tests a candidate against the environment-name grammar.</summary>
    /// <param name="value">The candidate environment name.</param>
    /// <returns><see langword="true"/> when the candidate is inside the grammar.</returns>
    public static bool IsWellFormed(ReadOnlySpan<char> value)
    {
        if (value.Length is 0 or > MaxLength || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Tests a UTF-8 candidate against the environment-name grammar.</summary>
    /// <remarks>
    /// The grammar's characters are ASCII, so byte-wise inspection is exact and an ingress that holds the
    /// candidate as bytes refuses it without materializing a string.
    /// </remarks>
    /// <param name="utf8Value">The candidate environment name as UTF-8 bytes.</param>
    /// <returns><see langword="true"/> when the candidate is inside the grammar.</returns>
    public static bool IsWellFormedUtf8(ReadOnlySpan<byte> utf8Value)
    {
        if (utf8Value.Length is 0 or > MaxLength || utf8Value[0] == (byte)'-' || utf8Value[^1] == (byte)'-')
        {
            return false;
        }

        foreach (byte b in utf8Value)
        {
            char c = (char)b;
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c) && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}