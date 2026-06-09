// <copyright file="CorrelationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// The runtime surface a generated executor calls to honour an AsyncAPI <c>correlationId</c> on a receive
/// step: it reads the correlation token from a message at the correlation id's <c>location</c> (a JSON
/// Pointer into the message payload) and compares it to the token a prior send registered. This links a
/// request a workflow published with the response it then receives.
/// </summary>
/// <remarks>
/// Correlation tokens are matched as JSON strings. A value that is absent, not a string, or does not match
/// the expected token does not correlate, so the receive keeps waiting for the message that does.
/// </remarks>
public static class CorrelationToken
{
    /// <summary>
    /// Reads the correlation token from a message at the given JSON Pointer as UTF-8 bytes. Used once per
    /// send to register the token a workflow published; the token is kept as UTF-8 so the receive side can
    /// compare it without transcoding. The pointer is a UTF-8 literal, so no transcoding occurs here either.
    /// </summary>
    /// <param name="message">The message payload to read from.</param>
    /// <param name="utf8Pointer">The JSON Pointer to the token, as UTF-8 (the correlation id's <c>location</c> fragment, e.g. <c>/correlationId</c>).</param>
    /// <param name="token">The token's unescaped UTF-8 bytes, when the pointer resolves to a JSON string.</param>
    /// <returns><see langword="true"/> if a string token was read.</returns>
    public static bool TryRead(in JsonElement message, ReadOnlySpan<byte> utf8Pointer, out byte[] token)
    {
        if (message.TryResolvePointer(utf8Pointer, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
        {
            using UnescapedUtf8JsonString utf8 = value.GetUtf8String();
            token = utf8.Span.ToArray();
            return true;
        }

        token = [];
        return false;
    }

    /// <summary>
    /// Determines whether a message's correlation token at <paramref name="utf8Pointer"/> equals
    /// <paramref name="expectedUtf8"/>. This runs on the receive hot path (once per delivered message until
    /// one correlates), so it resolves the token against a UTF-8 pointer literal and compares with
    /// <see cref="JsonElement.ValueEquals(ReadOnlySpan{byte})"/> — a UTF-8 ↔ UTF-8 comparison that allocates
    /// no managed string and performs no transcoding.
    /// </summary>
    /// <param name="message">The message payload to read from.</param>
    /// <param name="utf8Pointer">The JSON Pointer to the token, as UTF-8.</param>
    /// <param name="expectedUtf8">The expected token as UTF-8 (the value a prior send registered).</param>
    /// <returns><see langword="true"/> if the message carries the expected token.</returns>
    public static bool Matches(in JsonElement message, ReadOnlySpan<byte> utf8Pointer, ReadOnlySpan<byte> expectedUtf8)
        => message.TryResolvePointer(utf8Pointer, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            && value.ValueEquals(expectedUtf8);
}