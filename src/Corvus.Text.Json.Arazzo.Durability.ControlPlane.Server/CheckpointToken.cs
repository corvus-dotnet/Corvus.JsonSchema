// <copyright file="CheckpointToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// A run-scoped, short-lived bearer token that authenticates a serverless function's checkpoint callback to the runner's
/// checkpoint surface (ADR 0062). The runner mints one per dispatch, carries it in the invocation, and the function
/// presents it as <c>Authorization: Bearer</c> on every checkpoint request; the checkpoint endpoint validates it against
/// the run in the request URL. It is a symmetric HMAC over the run id and an expiry, so it needs no cloud identity
/// provider, it is opaque to the function (which never interprets it), and a leaked token authenticates only its own run
/// and only until it expires. The token is <c>{expiryUnixSeconds}.{base64url(HMAC-SHA256(secret, "runId:expiry"))}</c> —
/// the run id is bound by the signature but never transmitted, since the checkpoint endpoint already knows it from the URL.
/// </summary>
public static class CheckpointToken
{
    /// <summary>
    /// Mints a checkpoint token for a run, valid until <paramref name="expiry"/>.
    /// </summary>
    /// <param name="secret">The shared checkpoint secret the runner mints with and the checkpoint surface validates with.</param>
    /// <param name="runId">The run the token authorises checkpoints for.</param>
    /// <param name="expiry">When the token stops being valid.</param>
    /// <returns>The bearer token string.</returns>
    public static string Issue(ReadOnlySpan<byte> secret, string runId, DateTimeOffset expiry)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        long expiryUnixSeconds = expiry.ToUnixTimeSeconds();
        return $"{expiryUnixSeconds.ToString(CultureInfo.InvariantCulture)}.{Sign(secret, runId, expiryUnixSeconds)}";
    }

    /// <summary>
    /// Validates a checkpoint token against a run: it must be well-formed, signed with <paramref name="secret"/> for
    /// <paramref name="runId"/>, and not yet expired at <paramref name="now"/>.
    /// </summary>
    /// <param name="secret">The shared checkpoint secret.</param>
    /// <param name="token">The presented bearer token (may be <see langword="null"/> or empty).</param>
    /// <param name="runId">The run from the request URL the token must authorise.</param>
    /// <param name="now">The current time, for the expiry check.</param>
    /// <returns><see langword="true"/> if the token authorises checkpoints for the run.</returns>
    public static bool TryValidate(ReadOnlySpan<byte> secret, string? token, string runId, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(runId))
        {
            return false;
        }

        int separator = token.IndexOf('.');
        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        if (!long.TryParse(token.AsSpan(0, separator), NumberStyles.Integer, CultureInfo.InvariantCulture, out long expiryUnixSeconds)
            || expiryUnixSeconds <= now.ToUnixTimeSeconds())
        {
            return false;
        }

        // Recompute the signature over the run in the URL and the token's expiry, and compare in constant time. A token
        // minted for another run yields a different signature, so it does not validate here.
        string expected = Sign(secret, runId, expiryUnixSeconds);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(token[(separator + 1)..]),
            Encoding.ASCII.GetBytes(expected));
    }

    private static string Sign(ReadOnlySpan<byte> secret, string runId, long expiryUnixSeconds)
    {
        byte[] message = Encoding.UTF8.GetBytes($"{runId}:{expiryUnixSeconds.ToString(CultureInfo.InvariantCulture)}");
        Span<byte> signature = stackalloc byte[HMACSHA256.HashSizeInBytes];
        HMACSHA256.HashData(secret, message, signature);
        return Base64Url.EncodeToString(signature);
    }
}