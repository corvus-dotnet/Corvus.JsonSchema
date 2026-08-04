// <copyright file="CheckpointToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Runtime.InteropServices;
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
/// <remarks>
/// <para>
/// The signed message is unframed, and that is sound rather than merely untroublesome so far: the expiry is a
/// digits-only suffix after the final colon, so no other pair of run id and expiry can produce an identical message
/// however the run id is spelled. A run id containing the separator therefore borrows nothing, which is asserted rather
/// than assumed.
/// </para>
/// <para>
/// Validation allocates nothing. It runs on every checkpoint callback, before the caller has proved anything, so its
/// cost is what someone presenting a wrong token can spend on the runner's behalf.
/// </para>
/// </remarks>
public static class CheckpointToken
{
    /// <summary>The minimum checkpoint-secret length: 256 bits, matching the HMAC-SHA256 output, so a full-strength key is required.</summary>
    public const int MinimumSecretBytes = 32;

    // Base64url of a 32-byte HMAC is 43 characters; the buffer is rounded up rather than tightened, since it is stack space.
    private const int SignatureChars = 64;

    // long.MinValue is 20 characters, which bounds every expiry this formats or re-formats.
    private const int CanonicalExpiryChars = 20;

    // Run ids are short, so the signed message is normally stack-sized; the pool is there so an unusually long one
    // degrades rather than overflowing the stack.
    private const int MessageStackThreshold = 256;

    /// <summary>
    /// Mints a checkpoint token for a run, valid until <paramref name="expiry"/>.
    /// </summary>
    /// <param name="secret">The shared checkpoint secret the runner mints with and the checkpoint surface validates with. It must be at least <see cref="MinimumSecretBytes"/> bytes of high-entropy key material.</param>
    /// <param name="runId">The run the token authorises checkpoints for.</param>
    /// <param name="expiry">When the token stops being valid.</param>
    /// <returns>The bearer token string.</returns>
    /// <exception cref="ArgumentException">The secret is shorter than <see cref="MinimumSecretBytes"/>, or the run id is empty.</exception>
    public static string Issue(ReadOnlySpan<byte> secret, string runId, DateTimeOffset expiry)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        if (secret.Length < MinimumSecretBytes)
        {
            throw new ArgumentException($"The checkpoint secret must be at least {MinimumSecretBytes} bytes of high-entropy key material.", nameof(secret));
        }

        long expiryUnixSeconds = expiry.ToUnixTimeSeconds();
        Span<char> signature = stackalloc char[SignatureChars];
        if (!TrySign(secret, runId, expiryUnixSeconds, signature, out int written))
        {
            throw new ArgumentException("The checkpoint token's signature could not be computed.", nameof(runId));
        }

        return string.Concat(expiryUnixSeconds.ToString(CultureInfo.InvariantCulture), ".", signature[..written]);
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
        if (secret.Length < MinimumSecretBytes || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(runId))
        {
            return false;
        }

        int separator = token.IndexOf('.');
        if (separator <= 0 || separator == token.Length - 1)
        {
            return false;
        }

        // Parse the expiry as a bare non-negative decimal, and reject an expired one before doing any work on it.
        ReadOnlySpan<char> expirySegment = token.AsSpan(0, separator);
        if (!long.TryParse(expirySegment, NumberStyles.None, CultureInfo.InvariantCulture, out long expiryUnixSeconds)
            || expiryUnixSeconds <= now.ToUnixTimeSeconds())
        {
            return false;
        }

        // Require the expiry to be canonical (no sign, whitespace, or leading zeros) by re-formatting it and comparing,
        // so exactly one token string authenticates a run and a padded variant is not an equivalent token.
        Span<char> canonical = stackalloc char[CanonicalExpiryChars];
        if (!expiryUnixSeconds.TryFormat(canonical, out int canonicalLength, provider: CultureInfo.InvariantCulture)
            || !expirySegment.SequenceEqual(canonical[..canonicalLength]))
        {
            return false;
        }

        // Recompute the signature over the run in the URL and the token's expiry, and compare in constant time. A token
        // minted for another run yields a different signature, so it does not validate here.
        Span<char> expected = stackalloc char[SignatureChars];
        if (!TrySign(secret, runId, expiryUnixSeconds, expected, out int written))
        {
            return false;
        }

        // The platform's constant-time comparison over the raw UTF-16 code units: nothing is hand-rolled, and neither
        // side is transcoded to reach it. Equal char counts imply equal byte counts, which is what it compares on.
        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(token.AsSpan(separator + 1)),
            MemoryMarshal.AsBytes((ReadOnlySpan<char>)expected[..written]));
    }

    // Signs into the caller's buffer, allocating nothing. Validation runs on every checkpoint callback, before the
    // caller has proved anything, so its cost is what someone presenting a wrong token can spend on the runner's behalf.
    private static bool TrySign(ReadOnlySpan<byte> secret, string runId, long expiryUnixSeconds, Span<char> destination, out int written)
    {
        int maximumMessage = Encoding.UTF8.GetMaxByteCount(runId.Length) + 1 + CanonicalExpiryChars;
        byte[]? rented = maximumMessage > MessageStackThreshold ? ArrayPool<byte>.Shared.Rent(maximumMessage) : null;
        try
        {
            Span<byte> message = rented ?? stackalloc byte[MessageStackThreshold];
            int position = Encoding.UTF8.GetBytes(runId, message);
            message[position++] = (byte)':';
            expiryUnixSeconds.TryFormat(message[position..], out int digits, provider: CultureInfo.InvariantCulture);
            position += digits;

            Span<byte> signature = stackalloc byte[HMACSHA256.HashSizeInBytes];
            HMACSHA256.HashData(secret, message[..position], signature);
            return Base64Url.TryEncodeToChars(signature, destination, out written);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}