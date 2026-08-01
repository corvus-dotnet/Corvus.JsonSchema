// <copyright file="AnchorRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The tenant anchor's ordering key (ADR 0065): a <c>(incarnation, epoch)</c> pair compared lexicographically.
/// The incarnation is never a bare epoch's companion by accident — an incarnation change resets the control
/// plane's epoch counter, so epochs are not comparable across one.
/// </summary>
/// <param name="Incarnation">The store incarnation, advanced out of band on every restore or migration.</param>
/// <param name="Epoch">The monotonic lease epoch the control plane mints per grant.</param>
public readonly record struct AnchorOrderingKey(ulong Incarnation, ulong Epoch) : IComparable<AnchorOrderingKey>
{
    /// <inheritdoc/>
    public int CompareTo(AnchorOrderingKey other)
    {
        int byIncarnation = this.Incarnation.CompareTo(other.Incarnation);
        return byIncarnation != 0 ? byIncarnation : this.Epoch.CompareTo(other.Epoch);
    }

    /// <summary>Compares two keys.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>Whether <paramref name="left"/> orders before <paramref name="right"/>.</returns>
    public static bool operator <(AnchorOrderingKey left, AnchorOrderingKey right) => left.CompareTo(right) < 0;

    /// <summary>Compares two keys.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>Whether <paramref name="left"/> orders after <paramref name="right"/>.</returns>
    public static bool operator >(AnchorOrderingKey left, AnchorOrderingKey right) => left.CompareTo(right) > 0;

    /// <summary>Compares two keys.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>Whether <paramref name="left"/> orders at or before <paramref name="right"/>.</returns>
    public static bool operator <=(AnchorOrderingKey left, AnchorOrderingKey right) => left.CompareTo(right) <= 0;

    /// <summary>Compares two keys.</summary>
    /// <param name="left">The left key.</param>
    /// <param name="right">The right key.</param>
    /// <returns>Whether <paramref name="left"/> orders at or after <paramref name="right"/>.</returns>
    public static bool operator >=(AnchorOrderingKey left, AnchorOrderingKey right) => left.CompareTo(right) >= 0;
}

/// <summary>
/// A checkpoint digest (ADR 0065): 32 bytes held inline as four 64-bit words, so the value compares and copies
/// without allocating and without the reference equality an array field would bring to a record.
/// </summary>
public readonly struct AnchorDigest : IEquatable<AnchorDigest>
{
    private readonly ulong w0;
    private readonly ulong w1;
    private readonly ulong w2;
    private readonly ulong w3;

    /// <summary>Initializes a new instance of the <see cref="AnchorDigest"/> struct.</summary>
    /// <param name="digest">Exactly 32 bytes.</param>
    public AnchorDigest(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != SHA256.HashSizeInBytes)
        {
            throw new ArgumentException($"A checkpoint digest is exactly {SHA256.HashSizeInBytes} bytes.", nameof(digest));
        }

        this.w0 = BitConverter.ToUInt64(digest[..8]);
        this.w1 = BitConverter.ToUInt64(digest.Slice(8, 8));
        this.w2 = BitConverter.ToUInt64(digest.Slice(16, 8));
        this.w3 = BitConverter.ToUInt64(digest.Slice(24, 8));
    }

    /// <inheritdoc/>
    public bool Equals(AnchorDigest other)
        => this.w0 == other.w0 && this.w1 == other.w1 && this.w2 == other.w2 && this.w3 == other.w3;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is AnchorDigest other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.w0, this.w1, this.w2, this.w3);

    /// <summary>Compares two digests.</summary>
    /// <param name="left">The left digest.</param>
    /// <param name="right">The right digest.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool operator ==(AnchorDigest left, AnchorDigest right) => left.Equals(right);

    /// <summary>Compares two digests.</summary>
    /// <param name="left">The left digest.</param>
    /// <param name="right">The right digest.</param>
    /// <returns>Whether they differ.</returns>
    public static bool operator !=(AnchorDigest left, AnchorDigest right) => !left.Equals(right);
}

/// <summary>A committed or pending mark in the anchor record (ADR 0065).</summary>
/// <param name="Key">The ordering key the mark was written under.</param>
/// <param name="Sequence">The checkpoint sequence.</param>
/// <param name="Digest">The checkpoint digest, over the pinned submitted-bytes layout.</param>
public readonly record struct AnchorMark(AnchorOrderingKey Key, ulong Sequence, AnchorDigest Digest);

/// <summary>
/// How a closed anchor record came to be closed (ADR 0065). A terminal record's disposition is what
/// distinguishes an ordinary completion from an operator-signed abandonment: the two are otherwise the same
/// record shape, and a store that could not tell them apart would let a writer silently drop an in-flight save
/// by calling it a completion.
/// </summary>
public enum AnchorDisposition
{
    /// <summary>The record is live; no disposition applies.</summary>
    None,

    /// <summary>The run finished normally, its terminal checkpoint committed.</summary>
    Completed,

    /// <summary>An operator disposed of a run whose open hard-faults, discarding an outstanding staged save.</summary>
    Abandoned,
}

/// <summary>Whether a run's anchor is still advancing, or closed (ADR 0065).</summary>
public enum AnchorState
{
    /// <summary>The run is advancing; checkpoints may still be written.</summary>
    Live,

    /// <summary>The run finished, or was disposed of by an operator-signed abandon. Claims are refused.</summary>
    Terminal,
}

/// <summary>
/// The tenant anchor record for one run (ADR 0065): the tenant-held high-water mark the control plane cannot
/// rewrite, and the write-ahead log that makes an honest crash recoverable rather than fatal.
/// </summary>
/// <param name="RunId">The run this record anchors (immutable across every write).</param>
/// <param name="EnvironmentId">The environment the run is pinned to (immutable across every write).</param>
/// <param name="State">Whether the run is live or closed.</param>
/// <param name="EpochHighWater">The highest ordering key ever accepted for this run.</param>
/// <param name="Committed">The last checkpoint the tenant committed to.</param>
/// <param name="Pending">The staged next checkpoint, if a save is in flight.</param>
/// <param name="ReanchorCounter">Strictly monotonic; a re-anchor increments it, which is what stops a signed re-anchor being replayed. A record recovered from a lost anchor starts at 1, which is also what distinguishes that recovery from a fresh create.</param>
/// <param name="Disposition">How a terminal record was closed; <see cref="AnchorDisposition.None"/> while live.</param>
public readonly record struct AnchorRecord(
    string RunId,
    string EnvironmentId,
    AnchorState State,
    AnchorOrderingKey EpochHighWater,
    AnchorMark Committed,
    AnchorMark? Pending,
    ulong ReanchorCounter,
    AnchorDisposition Disposition = AnchorDisposition.None);