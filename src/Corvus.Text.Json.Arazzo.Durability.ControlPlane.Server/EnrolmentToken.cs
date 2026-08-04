// <copyright file="EnrolmentToken.cs" company="Endjin Limited">
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
/// An environment-scoped, short-lived bearer token that lets a runner register itself into an environment nobody has
/// named it in yet (ADR 0065 decision 2). An administrator mints one for an environment and the deployment delivers it
/// to the runners it starts there; a runner presents it on registration, and the control plane binds the authorization
/// to the principal that presented it.
/// </summary>
/// <remarks>
/// <para>
/// It exists because the alternative does not scale. Registration must be scoped per environment rather than left as a
/// system-context operation, and the other way to scope it is for an administrator to pre-authorize every runner id in
/// advance. That is workable for a fixed fleet and not for one that scales itself: something has to pre-authorize each
/// new instance, and that something needs the power to pre-authorize any id in the environment, which is the blanket
/// capability the scoping was meant to remove, moved rather than removed. A token bounds the same capability in time
/// instead of dissolving it.
/// </para>
/// <para>
/// What it grants is deliberately small. Presenting a valid token creates a <c>Pending</c> authorization bound to the
/// presenting principal, and <c>Pending</c> dispatches nothing: an administrator of the environment still decides
/// whether the runner may execute. So a leaked token buys an entry in an approval queue, not work, and only until it
/// expires. It is scoped to one environment, so it cannot be replayed into another.
/// </para>
/// <para>
/// The construction mirrors <see cref="CheckpointToken"/>, which solves the same problem for a different callback, so
/// the two read alike and neither invents its own scheme. As there, the bound value is not transmitted: registration
/// already carries the environment in its URL. The message is unframed like its counterpart's, and that is sound for
/// the same reason it is there rather than by assumption: the expiry is a digits-only suffix after the final colon, so
/// no other (environment, expiry) pair can produce an identical message however the environment is spelled.
/// </para>
/// </remarks>
public static class EnrolmentToken
{
    /// <summary>The minimum enrolment-secret length: 256 bits, matching the HMAC-SHA256 output, so a full-strength key is required.</summary>
    public const int MinimumSecretBytes = 32;

    // Base64url of a 32-byte HMAC is 43 characters; the buffer is rounded up rather than tightened, since it is stack space.
    private const int SignatureChars = 64;

    // long.MinValue is 20 characters, which bounds every expiry this formats or re-formats.
    private const int CanonicalExpiryChars = 20;

    // Environment names are short in every deployment, so the signed message is normally stack-sized; the pool is there
    // so an unusually long one degrades rather than overflowing the stack.
    private const int MessageStackThreshold = 256;

    /// <summary>
    /// Mints an enrolment token for an environment, valid until <paramref name="expiry"/>.
    /// </summary>
    /// <param name="secret">The enrolment secret the control plane mints with and validates with. It must be at least <see cref="MinimumSecretBytes"/> bytes of high-entropy key material.</param>
    /// <param name="environment">The environment the token admits a runner to.</param>
    /// <param name="expiry">When the token stops being valid. Keep it short; it is the whole of the bound on a leak.</param>
    /// <returns>The bearer token string.</returns>
    /// <exception cref="ArgumentException">The secret is shorter than <see cref="MinimumSecretBytes"/>, or the environment is empty.</exception>
    public static string Issue(ReadOnlySpan<byte> secret, string environment, DateTimeOffset expiry)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (secret.Length < MinimumSecretBytes)
        {
            throw new ArgumentException($"The enrolment secret must be at least {MinimumSecretBytes} bytes of high-entropy key material.", nameof(secret));
        }

        long expiryUnixSeconds = expiry.ToUnixTimeSeconds();
        Span<char> signature = stackalloc char[SignatureChars];
        if (!TrySign(secret, environment, expiryUnixSeconds, signature, out int written))
        {
            throw new ArgumentException("The enrolment token's signature could not be computed.", nameof(environment));
        }

        return string.Concat(expiryUnixSeconds.ToString(CultureInfo.InvariantCulture), ".", signature[..written]);
    }

    /// <summary>
    /// Validates an enrolment token against an environment: it must be well-formed, signed with
    /// <paramref name="secret"/> for <paramref name="environment"/>, and not yet expired at <paramref name="now"/>.
    /// </summary>
    /// <param name="secret">The enrolment secret.</param>
    /// <param name="token">The presented token (may be <see langword="null"/> or empty).</param>
    /// <param name="environment">The environment from the request URL the token must admit a runner to.</param>
    /// <param name="now">The current time, for the expiry check.</param>
    /// <returns><see langword="true"/> if the token admits a runner to the environment.</returns>
    public static bool TryValidate(ReadOnlySpan<byte> secret, string? token, string environment, DateTimeOffset now)
    {
        if (secret.Length < MinimumSecretBytes || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(environment))
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
        // so exactly one token string admits a runner and a padded variant is not an equivalent token.
        Span<char> canonical = stackalloc char[CanonicalExpiryChars];
        if (!expiryUnixSeconds.TryFormat(canonical, out int canonicalLength, provider: CultureInfo.InvariantCulture)
            || !expirySegment.SequenceEqual(canonical[..canonicalLength]))
        {
            return false;
        }

        // Recompute the signature over the environment in the URL and the token's expiry, and compare in constant time.
        // A token minted for another environment yields a different signature, so it does not validate here.
        Span<char> expected = stackalloc char[SignatureChars];
        if (!TrySign(secret, environment, expiryUnixSeconds, expected, out int written))
        {
            return false;
        }

        // The platform's constant-time comparison over the raw UTF-16 code units: nothing is hand-rolled, and neither
        // side is transcoded to reach it. Equal char counts imply equal byte counts, which is what it compares on.
        return CryptographicOperations.FixedTimeEquals(
            MemoryMarshal.AsBytes(token.AsSpan(separator + 1)),
            MemoryMarshal.AsBytes((ReadOnlySpan<char>)expected[..written]));
    }

    // Signs into the caller's buffer, allocating nothing. Validation runs before the caller has proved anything, so its
    // cost is what someone presenting a wrong token can spend on the deployment's behalf.
    private static bool TrySign(ReadOnlySpan<byte> secret, string environment, long expiryUnixSeconds, Span<char> destination, out int written)
    {
        int maximumMessage = Encoding.UTF8.GetMaxByteCount(environment.Length) + 1 + CanonicalExpiryChars;
        byte[]? rented = maximumMessage > MessageStackThreshold ? ArrayPool<byte>.Shared.Rent(maximumMessage) : null;
        try
        {
            Span<byte> message = rented ?? stackalloc byte[MessageStackThreshold];
            int position = Encoding.UTF8.GetBytes(environment, message);
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