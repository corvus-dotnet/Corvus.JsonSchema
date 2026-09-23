// <copyright file="CheckpointRow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The pinned stored layout of a checkpoint row (ADR 0065 decisions 4 and 6): deterministic length-framed regions in
/// a fixed order, stored as opaque octets that no backend parses, patches or re-emits.
/// </summary>
/// <remarks>
/// <code>
/// [framing version][algorithm][len‖key id][len‖runner region][len‖salt][len‖nonce][len‖tag][len‖payload][len‖MAC][len‖control-plane region]
/// </code>
/// <para>
/// Every length is a 4-byte big-endian count, the framing the derivation and digest functions already use. The
/// <em>submitted bytes</em>, everything the runner writes, are the prefix that ends where the control-plane region's
/// length begins; the control-plane region is last and is joined by the server, so neither party can rewrite the
/// other's bytes and the digest of decision 6 is a hash over a prefix of the row. The header (the framing version and
/// the algorithm id) is inside that prefix, so an algorithm selector cannot be swapped outside what is authenticated.
/// </para>
/// <para>
/// A <see cref="CheckpointAlgorithm.Clear"/> row, the only kind an unsealed environment writes, carries an empty key
/// id, salt, nonce, tag and MAC, and its payload region is the payload's plaintext JSON. A clear row carrying any of
/// them is malformed: the regions are reserved for a sealed environment and mean nothing without a key.
/// </para>
/// </remarks>
public static class CheckpointRow
{
    /// <summary>The framing version this code writes and reads.</summary>
    public const byte FramingVersion = 1;

    private const int LengthPrefix = sizeof(uint);
    private const int HeaderLength = 2;
    private const int RegionCount = 8;

    /// <summary>Parses a row's layout, throwing when it is not a well-formed row.</summary>
    /// <param name="row">The stored row.</param>
    /// <returns>The layout: where each region sits in <paramref name="row"/>.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row of this framing version.</exception>
    public static CheckpointRowLayout Parse(ReadOnlySpan<byte> row)
    {
        if (!TryParse(row, out CheckpointRowLayout layout))
        {
            ThrowHelper.ThrowCheckpointRowMalformed();
        }

        return layout;
    }

    /// <summary>Parses a row's layout, reporting rather than throwing when the bytes are not a well-formed row.</summary>
    /// <param name="row">The stored row.</param>
    /// <param name="layout">The layout when the row is well formed.</param>
    /// <returns><see langword="true"/> when <paramref name="row"/> is a checkpoint row of this framing version.</returns>
    public static bool TryParse(ReadOnlySpan<byte> row, out CheckpointRowLayout layout)
    {
        layout = default;
        if (row.Length < HeaderLength || row[0] != FramingVersion || row[1] > (byte)CheckpointAlgorithm.Aes256Gcm)
        {
            return false;
        }

        var algorithm = (CheckpointAlgorithm)row[1];
        Span<Range> regions = stackalloc Range[RegionCount];
        int offset = HeaderLength;
        int submittedLength = 0;
        for (int i = 0; i < RegionCount; i++)
        {
            if (i == RegionCount - 1)
            {
                submittedLength = offset;
            }

            if (row.Length - offset < LengthPrefix)
            {
                return false;
            }

            uint length = BinaryPrimitives.ReadUInt32BigEndian(row[offset..]);
            offset += LengthPrefix;
            if (length > (uint)(row.Length - offset))
            {
                return false;
            }

            regions[i] = offset..(offset + (int)length);
            offset += (int)length;
        }

        if (offset != row.Length)
        {
            return false;
        }

        layout = new CheckpointRowLayout(algorithm, regions[0], regions[1], regions[2], regions[3], regions[4], regions[5], regions[6], regions[7], submittedLength);

        // A clear row has nothing to put in the crypto regions; one that carries them is not a row this code wrote.
        return algorithm != CheckpointAlgorithm.Clear
            || (layout.KeyId.IsEmpty() && layout.Salt.IsEmpty() && layout.Nonce.IsEmpty() && layout.Tag.IsEmpty() && layout.Mac.IsEmpty());
    }

    /// <summary>Writes a clear row: the runner region and the payload plaintext, with the control-plane region joined.</summary>
    /// <param name="runnerRegion">The runner-authored region (the envelope JSON).</param>
    /// <param name="payload">The payload plaintext JSON.</param>
    /// <param name="controlPlaneRegion">The control-plane region, carried verbatim.</param>
    /// <returns>The row.</returns>
    public static byte[] WriteClear(ReadOnlySpan<byte> runnerRegion, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> controlPlaneRegion)
    {
        byte[] row = new byte[HeaderLength + (RegionCount * LengthPrefix) + runnerRegion.Length + payload.Length + controlPlaneRegion.Length];
        row[0] = FramingVersion;
        row[1] = (byte)CheckpointAlgorithm.Clear;
        int offset = HeaderLength;
        offset = WriteRegion(row, offset, ReadOnlySpan<byte>.Empty); // key id
        offset = WriteRegion(row, offset, runnerRegion);
        offset = WriteRegion(row, offset, ReadOnlySpan<byte>.Empty); // salt
        offset = WriteRegion(row, offset, ReadOnlySpan<byte>.Empty); // nonce
        offset = WriteRegion(row, offset, ReadOnlySpan<byte>.Empty); // tag
        offset = WriteRegion(row, offset, payload);
        offset = WriteRegion(row, offset, ReadOnlySpan<byte>.Empty); // MAC
        WriteRegion(row, offset, controlPlaneRegion);
        return row;
    }

    /// <summary>
    /// Replaces a row's control-plane region, leaving every submitted byte exactly as it was. This is the one write
    /// the control plane makes to a row (ADR 0065 decision 7): it never rewrites the runner's octets.
    /// </summary>
    /// <param name="row">The stored row.</param>
    /// <param name="controlPlaneRegion">The new control-plane region.</param>
    /// <returns>The row with its control-plane region replaced.</returns>
    /// <exception cref="FormatException">The bytes are not a checkpoint row.</exception>
    public static byte[] WithControlPlaneRegion(ReadOnlySpan<byte> row, ReadOnlySpan<byte> controlPlaneRegion)
    {
        CheckpointRowLayout layout = Parse(row);
        byte[] rewritten = new byte[layout.SubmittedLength + LengthPrefix + controlPlaneRegion.Length];
        row[..layout.SubmittedLength].CopyTo(rewritten);
        WriteRegion(rewritten, layout.SubmittedLength, controlPlaneRegion);
        return rewritten;
    }

    private static int WriteRegion(Span<byte> row, int offset, ReadOnlySpan<byte> region)
    {
        BinaryPrimitives.WriteUInt32BigEndian(row[offset..], (uint)region.Length);
        offset += LengthPrefix;
        region.CopyTo(row[offset..]);
        return offset + region.Length;
    }

    private static bool IsEmpty(this Range range) => range.Start.Value == range.End.Value;
}

/// <summary>The payload algorithm a row's header names; the selector is inside the submitted bytes and therefore authenticated once a MAC exists.</summary>
public enum CheckpointAlgorithm : byte
{
    /// <summary>No payload encryption: the payload region is plaintext JSON. The only algorithm of an unsealed environment.</summary>
    Clear = 0,

    /// <summary>AES-256-GCM under a per-encryption derived data key (ADR 0065 decision 5). Reserved until SEQ-3 builds it.</summary>
    Aes256Gcm = 1,
}

/// <summary>Where each region of a parsed checkpoint row sits, as ranges into the row's bytes.</summary>
/// <param name="Algorithm">The payload algorithm the header names.</param>
/// <param name="KeyId">The key id region (empty on a clear row).</param>
/// <param name="RunnerRegion">The runner-authored region: the envelope JSON.</param>
/// <param name="Salt">The data-key salt (empty on a clear row).</param>
/// <param name="Nonce">The AEAD nonce (empty on a clear row).</param>
/// <param name="Tag">The AEAD tag (empty on a clear row).</param>
/// <param name="Payload">The payload region: plaintext JSON on a clear row, ciphertext otherwise.</param>
/// <param name="Mac">The unified MAC (empty on a clear row).</param>
/// <param name="ControlPlaneRegion">The server-owned control-plane region, joined at read time.</param>
/// <param name="SubmittedLength">The length of the submitted bytes: the prefix of the row every runner-written region lies in, which the checkpoint digest is taken over.</param>
public readonly record struct CheckpointRowLayout(
    CheckpointAlgorithm Algorithm,
    Range KeyId,
    Range RunnerRegion,
    Range Salt,
    Range Nonce,
    Range Tag,
    Range Payload,
    Range Mac,
    Range ControlPlaneRegion,
    int SubmittedLength);