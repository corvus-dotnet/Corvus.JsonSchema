// <copyright file="EnvironmentRunnerAuthorizationContinuationToken.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;

namespace Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;

/// <summary>
/// Encodes and decodes the opaque continuation token used to keyset-page
/// <see cref="IEnvironmentRunnerAuthorizationStore.ListAsync(RunnerAuthorizationQuery, int, JsonString, System.Threading.CancellationToken)"/>
/// results (design §5.5). The inbox pages by <c>(environment, runnerId)</c> — both unique together — so the token carries the
/// last row's <c>(environment, runnerId)</c> for the next request to seek strictly past. It is opaque and
/// <b>backend-scoped</b> (only ever presented back to the store that issued it). Mirrors
/// <see cref="Availability.AvailabilityRequestContinuationToken"/>.
/// </summary>
/// <remarks>
/// Fully bytes-native: the token is <c>base64url(environment \0 runnerId)</c> with both parts carried as their persisted
/// UTF-8. <see cref="EncodeToUtf8"/> assembles the key into a pooled scratch then Base64URL-encodes into the caller's buffer;
/// <see cref="TryDecode"/> Base64URL-decodes into the caller's buffer and returns each part as a span into it — no managed
/// string, no intermediate <c>byte[]</c>.
/// </remarks>
public static class EnvironmentRunnerAuthorizationContinuationToken
{
    // The null control byte cannot appear in an environment or a runner id, so it separates the two key parts.
    private const byte Separator = 0;

    // Key parts are short, so the assemble scratch fits the stack in the common case, with the ArrayPool fallback for the
    // rare long key.
    private const int StackThreshold = 256;

    /// <summary>Gets an upper bound, in bytes, on the Base64URL token <see cref="EncodeToUtf8"/> writes for an environment of
    /// <paramref name="environmentUtf8Length"/> bytes and a runner id of <paramref name="runnerIdUtf8Length"/> bytes — safe to
    /// size a buffer with.</summary>
    /// <param name="environmentUtf8Length">The environment's UTF-8 byte length.</param>
    /// <param name="runnerIdUtf8Length">The runner id's UTF-8 byte length.</param>
    /// <returns>An upper bound on the encoded token length in bytes.</returns>
    public static int GetMaxEncodedLength(int environmentUtf8Length, int runnerIdUtf8Length)
        => Base64Url.GetEncodedLength(environmentUtf8Length + 1 + runnerIdUtf8Length);

    /// <summary>Writes the continuation token (<c>base64url</c> of <c>environment \0 runnerId</c>) for the last row's
    /// <c>(environment, runnerId)</c> into <paramref name="destination"/> (size it with <see cref="GetMaxEncodedLength"/>).</summary>
    /// <param name="environmentUtf8">The last row's environment as UTF-8.</param>
    /// <param name="runnerIdUtf8">The last row's runner id as UTF-8.</param>
    /// <param name="destination">The buffer to write the Base64URL token into.</param>
    /// <returns>The number of bytes written.</returns>
    public static int EncodeToUtf8(ReadOnlySpan<byte> environmentUtf8, ReadOnlySpan<byte> runnerIdUtf8, Span<byte> destination)
    {
        int rawMax = environmentUtf8.Length + 1 + runnerIdUtf8.Length;
        byte[]? rented = rawMax > StackThreshold ? ArrayPool<byte>.Shared.Rent(rawMax) : null;
        Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
        try
        {
            environmentUtf8.CopyTo(raw);
            int pos = environmentUtf8.Length;
            raw[pos++] = Separator;
            runnerIdUtf8.CopyTo(raw[pos..]);
            pos += runnerIdUtf8.Length;
            Base64Url.EncodeToUtf8(raw[..pos], destination, out _, out int written);
            return written;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Gets an upper bound, in bytes, on the decoded key <see cref="TryDecode"/> writes for a token of
    /// <paramref name="tokenUtf8Length"/> bytes — safe to size the decode buffer with.</summary>
    /// <param name="tokenUtf8Length">The token's UTF-8 byte length.</param>
    /// <returns>An upper bound on the decoded key length in bytes.</returns>
    public static int GetMaxDecodedLength(int tokenUtf8Length) => Base64Url.GetMaxDecodedLength(tokenUtf8Length);

    /// <summary>Decodes a continuation token's UTF-8 into <paramref name="destination"/> (size it with
    /// <see cref="GetMaxDecodedLength"/>), yielding the <c>(environment, runnerId)</c> to page strictly after — each part
    /// stays a span into the caller's buffer, never a managed string.</summary>
    /// <param name="tokenUtf8">The token's UTF-8 from a previous page's <c>nextPageToken</c>, or empty for the first page.</param>
    /// <param name="destination">The buffer the decoded key is written into.</param>
    /// <param name="environmentUtf8">The decoded environment (a slice of <paramref name="destination"/>) to page strictly after, or empty.</param>
    /// <param name="runnerIdUtf8">The decoded runner id (a slice of <paramref name="destination"/>) to page strictly after, or empty.</param>
    /// <returns><see langword="true"/> if a cursor was decoded; <see langword="false"/> for the first page (empty token).</returns>
    /// <exception cref="FormatException">The token is not a valid continuation token.</exception>
    public static bool TryDecode(ReadOnlySpan<byte> tokenUtf8, Span<byte> destination, out ReadOnlySpan<byte> environmentUtf8, out ReadOnlySpan<byte> runnerIdUtf8)
    {
        environmentUtf8 = default;
        runnerIdUtf8 = default;
        if (tokenUtf8.IsEmpty)
        {
            return false;
        }

        if (Base64Url.DecodeFromUtf8(tokenUtf8, destination, out _, out int decoded) != OperationStatus.Done)
        {
            throw new FormatException("The runner-authorization page token is not valid base64url.");
        }

        ReadOnlySpan<byte> key = destination[..decoded];
        int sep = key.IndexOf(Separator);
        if (sep < 0)
        {
            throw new FormatException("The runner-authorization page token is malformed.");
        }

        environmentUtf8 = key[..sep];
        runnerIdUtf8 = key[(sep + 1)..];
        return true;
    }
}