// <copyright file="WaitIndexBlinder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// Blinds a message wait's match key for one environment and key generation (ADR 0065 decision 4): an HMAC-SHA256
/// under the <c>wait-index</c> subkey of the environment's payload key over the length-framed channel and correlation
/// id, rendered as <c>{keyId}.{base64url}</c>, which is the <c>(keyId, index)</c> pair the wait index is keyed by. The
/// environment is inside the subkey, so one tenant's <c>staging</c> message cannot wake its <c>production</c> run. The
/// control plane matches a delivered message to a parked run by equality on the index and learns neither the channel
/// nor the business key.
/// </summary>
/// <remarks>
/// A channel-only wait, one with no correlation id, is blinded with an explicit sentinel: a kind byte after the channel
/// says whether a correlation id follows at all, so absence is its own value under the framing and no correlation id,
/// whatever its text, can equal it. Every channel-only wait on one channel therefore shares one index, a per-channel
/// constant the control plane can group by, which is the accepted residue AR-8. A delivery
/// that carries a correlation id queries its own index and the channel-only index, so a run awaiting any message on the
/// channel still wakes; a delivery with none matches the channel-only index alone, never every correlated waiter.
/// </remarks>
public sealed class WaitIndexBlinder
{
    private const int MaxIdentifierLength = 256;
    private const int IndexLength = 32;
    private const byte NoCorrelation = 0;
    private const byte WithCorrelation = 1;

    private readonly string keyId;
    private readonly byte[] subkey;

    /// <summary>Initializes a new instance of the <see cref="WaitIndexBlinder"/> class.</summary>
    /// <param name="environment">The environment the key belongs to.</param>
    /// <param name="keyId">The key generation.</param>
    /// <param name="payloadKey">The environment's payload key; the <c>wait-index</c> subkey is derived from it here.</param>
    public WaitIndexBlinder(string environment, string keyId, ReadOnlySpan<byte> payloadKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(keyId);
        this.keyId = keyId;
        this.subkey = new byte[IndexLength];
        CheckpointDerivation.DeriveSubkey(payloadKey, CheckpointSubkey.WaitIndex, environment, keyId, this.subkey);
    }

    /// <summary>Gets the key generation the indexes carry.</summary>
    public string KeyId => this.keyId;

    /// <summary>Blinds a wait's match key.</summary>
    /// <param name="channel">The channel.</param>
    /// <param name="correlationId">The correlation id, or <see langword="null"/> for a channel-only wait.</param>
    /// <returns>The blind wait index, <c>{keyId}.{base64url}</c>.</returns>
    public string Blind(string channel, string? correlationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);
        if (channel.Length > MaxIdentifierLength || correlationId?.Length > MaxIdentifierLength)
        {
            throw new ArgumentException($"A channel and a correlation id are limited to {MaxIdentifierLength} characters.");
        }

        // Every field is length-framed, so ("orders", "123") and ("order", "s123") cannot collide, and the kind byte
        // is the sentinel: a channel-only wait has no correlation field at all, which no correlation id can spell.
        int channelBytes = Encoding.UTF8.GetByteCount(channel);
        int correlationBytes = correlationId is null ? 0 : sizeof(int) + Encoding.UTF8.GetByteCount(correlationId);
        Span<byte> message = stackalloc byte[sizeof(int) + channelBytes + 1 + correlationBytes];
        BinaryPrimitives.WriteInt32BigEndian(message, channelBytes);
        Encoding.UTF8.GetBytes(channel, message[sizeof(int)..]);
        int at = sizeof(int) + channelBytes;
        message[at++] = correlationId is null ? NoCorrelation : WithCorrelation;
        if (correlationId is not null)
        {
            BinaryPrimitives.WriteInt32BigEndian(message[at..], correlationBytes - sizeof(int));
            Encoding.UTF8.GetBytes(correlationId, message[(at + sizeof(int))..]);
        }

        Span<byte> mac = stackalloc byte[IndexLength];
        HMACSHA256.HashData(this.subkey, message, mac);
        return string.Concat(this.keyId, ".", Base64Url.EncodeToString(mac));
    }

    /// <summary>Blinds the channel-only wait's match key for <paramref name="channel"/>: what a run awaiting any message on the channel is parked under.</summary>
    /// <param name="channel">The channel.</param>
    /// <returns>The blind wait index.</returns>
    public string BlindChannelOnly(string channel) => this.Blind(channel, null);
}