// <copyright file="CheckpointIntegrity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The unified MAC of ADR 0065 decision 4: one HMAC-SHA256 under the environment's <c>envelope-mac</c> subkey
/// (<see cref="CheckpointDerivation"/>) that makes the runner region and the payload cryptographically inseparable.
/// The message is the row's header, the key id and the runner region, each length-framed, followed by the SHA-256 of
/// the payload region; the header is inside the coverage so the algorithm selector it carries is authenticated
/// (decision 6), and the key id is inside it so a row cannot be re-pointed at another generation. The MAC is written
/// into the row's MAC region and verified on every open by the party that holds the key: the runner.
/// </summary>
public static class CheckpointIntegrity
{
    /// <summary>The MAC's length: HMAC-SHA256.</summary>
    public const int MacLength = 32;

    private const int DigestLength = 32;
    private const int HeaderLength = 2;
    private const int LengthPrefix = sizeof(uint);

    /// <summary>Seals a row: writes <paramref name="keyId"/> and the MAC computed under <paramref name="envelopeMacKey"/> into it.</summary>
    /// <param name="row">The row or submission to seal; a clear row with empty key id and MAC regions.</param>
    /// <param name="keyId">The key generation the MAC is under.</param>
    /// <param name="envelopeMacKey">The <c>envelope-mac</c> subkey for that generation.</param>
    /// <returns>The sealed row, its submitted bytes changed only in the key id and MAC regions.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row or submission.</exception>
    public static byte[] Seal(ReadOnlySpan<byte> row, string keyId, ReadOnlySpan<byte> envelopeMacKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        byte[] keyIdUtf8 = System.Text.Encoding.UTF8.GetBytes(keyId);
        CheckpointRowLayout layout = CheckpointRow.ParseAny(row);
        Span<byte> mac = stackalloc byte[MacLength];
        Compute(row, layout, keyIdUtf8, envelopeMacKey, mac);
        return CheckpointRow.WithIntegrity(row, keyIdUtf8, mac);
    }

    /// <summary>Verifies a sealed row's MAC under the key its key id names.</summary>
    /// <param name="row">The row or submission.</param>
    /// <param name="envelopeMacKey">The <c>envelope-mac</c> subkey for the generation the row names.</param>
    /// <returns><see langword="true"/> when the row carries a MAC and it verifies.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row or submission.</exception>
    public static bool Verify(ReadOnlySpan<byte> row, ReadOnlySpan<byte> envelopeMacKey)
    {
        CheckpointRowLayout layout = CheckpointRow.ParseAny(row);
        ReadOnlySpan<byte> presented = row[layout.Mac];
        if (presented.Length != MacLength)
        {
            return false;
        }

        Span<byte> expected = stackalloc byte[MacLength];
        Compute(row, layout, row[layout.KeyId], envelopeMacKey, expected);
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }

    /// <summary>The key id a row names, or <see langword="null"/> for a clear row.</summary>
    /// <param name="row">The row or submission.</param>
    /// <returns>The key id.</returns>
    public static string? KeyIdOf(ReadOnlySpan<byte> row)
    {
        CheckpointRowLayout layout = CheckpointRow.ParseAny(row);
        ReadOnlySpan<byte> keyId = row[layout.KeyId];
        return keyId.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(keyId);
    }

    // message = len‖header ‖ len‖keyId ‖ len‖runnerRegion ‖ len‖SHA256(payload)
    private static void Compute(ReadOnlySpan<byte> row, in CheckpointRowLayout layout, ReadOnlySpan<byte> keyId, ReadOnlySpan<byte> envelopeMacKey, Span<byte> destination)
    {
        ReadOnlySpan<byte> runner = row[layout.RunnerRegion];
        Span<byte> digest = stackalloc byte[DigestLength];
        SHA256.HashData(row[layout.Payload], digest);

        int length = (4 * LengthPrefix) + HeaderLength + keyId.Length + runner.Length + DigestLength;
        byte[]? rented = length > 512 ? System.Buffers.ArrayPool<byte>.Shared.Rent(length) : null;
        Span<byte> message = rented ?? stackalloc byte[512];
        message = message[..length];
        try
        {
            int written = 0;
            written += Frame(message[written..], row[..HeaderLength]);
            written += Frame(message[written..], keyId);
            written += Frame(message[written..], runner);
            written += Frame(message[written..], digest);
            HMACSHA256.HashData(envelopeMacKey, message[..written], destination);
        }
        finally
        {
            if (rented is not null)
            {
                message.Clear();
                System.Buffers.ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    private static int Frame(Span<byte> destination, ReadOnlySpan<byte> value)
    {
        BinaryPrimitives.WriteUInt32BigEndian(destination, (uint)value.Length);
        value.CopyTo(destination[LengthPrefix..]);
        return LengthPrefix + value.Length;
    }
}