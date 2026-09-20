// <copyright file="AuditChainVerifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Verifies an audit chain from its stored bytes (ADR 0069): every line is an audit record by its schema, the records
/// name one chain, their sequence runs from zero with no gap, and each carries the hash of the line before it. It reads
/// the sink's bytes directly, so the control plane that wrote them is not in the path that checks them.
/// </summary>
public static class AuditChainVerifier
{
    /// <summary>Verifies one chain, stopping at the first break.</summary>
    /// <param name="chain">The chain's JSON Lines bytes.</param>
    /// <param name="expectedHash">A record hash to look for among the verified records (64 lowercase hex digits), or empty to look for none. It is how a caller checks the hash a later chain says it continues from.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>What was found.</returns>
    public static async ValueTask<AuditChainVerification> VerifyAsync(Stream chain, ReadOnlyMemory<byte> expectedHash = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);

        using var state = new State(expectedHash);
        PipeReader reader = PipeReader.Create(chain, new StreamPipeReaderOptions(leaveOpen: true));
        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
                ReadOnlySequence<byte> buffer = result.Buffer;
                while (state.Break == AuditChainBreak.None && TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                {
                    state.Verify(in line);
                }

                if (state.Break != AuditChainBreak.None)
                {
                    break;
                }

                if (result.IsCompleted)
                {
                    if (!buffer.IsEmpty)
                    {
                        state.Fail(AuditChainBreak.TornTail);
                    }

                    break;
                }

                reader.AdvanceTo(buffer.Start, buffer.End);
            }
        }
        finally
        {
            await reader.CompleteAsync().ConfigureAwait(false);
        }

        return state.ToResult();
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        SequencePosition? lineFeed = buffer.PositionOf((byte)'\n');
        if (lineFeed is not { } position)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, position);
        buffer = buffer.Slice(buffer.GetPosition(1, position));
        return true;
    }

    private sealed class State(ReadOnlyMemory<byte> expectedHash) : IDisposable
    {
        private readonly byte[] chainId = new byte[AuditRecord.ChainIdLength];
        private readonly byte[] previousHash = InitialHash();
        private readonly IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long line;
        private long records;
        private string? continuesChain;
        private string? continuesHash;
        private bool containsExpectedHash;

        public AuditChainBreak Break { get; private set; }

        public void Dispose() => this.hasher.Dispose();

        public void Fail(AuditChainBreak found)
        {
            this.Break = found;
            this.line++;
        }

        public void Verify(in ReadOnlySequence<byte> recordLine)
        {
            this.line++;
            AuditChainBreak found = this.Check(in recordLine);
            if (found != AuditChainBreak.None)
            {
                this.Break = found;
                return;
            }

            foreach (ReadOnlyMemory<byte> segment in recordLine)
            {
                this.hasher.AppendData(segment.Span);
            }

            Span<byte> hash = stackalloc byte[SHA256.HashSizeInBytes];
            this.hasher.GetHashAndReset(hash);
            WriteHex(hash, this.previousHash);
            this.records++;
            if (!expectedHash.IsEmpty && expectedHash.Span.SequenceEqual(this.previousHash))
            {
                this.containsExpectedHash = true;
            }
        }

        public AuditChainVerification ToResult()
            => new(
                this.Break,
                this.Break == AuditChainBreak.None ? 0 : this.line,
                this.records == 0 ? null : Encoding.UTF8.GetString(this.chainId),
                this.records,
                this.records == 0 ? null : Encoding.UTF8.GetString(this.previousHash),
                this.continuesChain,
                this.continuesHash,
                this.containsExpectedHash);

        private static byte[] InitialHash()
        {
            byte[] zeros = new byte[AuditRecord.HashLength];
            zeros.AsSpan().Fill((byte)'0');
            return zeros;
        }

        private static void WriteHex(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            ReadOnlySpan<byte> digits = "0123456789abcdef"u8;
            for (int i = 0; i < source.Length; i++)
            {
                destination[i * 2] = digits[source[i] >> 4];
                destination[(i * 2) + 1] = digits[source[i] & 0xF];
            }
        }

        private AuditChainBreak Check(in ReadOnlySequence<byte> recordLine)
        {
            ParsedJsonDocument<AuditRecord> document;
            try
            {
                document = ParsedJsonDocument<AuditRecord>.Parse(recordLine);
            }
            catch (JsonException)
            {
                return AuditChainBreak.MalformedRecord;
            }

            using (document)
            {
                AuditRecord record = document.RootElement;
                if (!record.EvaluateSchema())
                {
                    return AuditChainBreak.MalformedRecord;
                }

                if (this.records == 0)
                {
                    using UnescapedUtf8JsonString id = ((JsonElement)record.Chain).GetUtf8String();
                    id.Span.CopyTo(this.chainId);
                    if (record.Continues.IsNotUndefined())
                    {
                        this.continuesChain = (string)record.Continues.Chain;
                        this.continuesHash = (string)record.Continues.Hash;
                    }
                }
                else if (!((JsonElement)record.Chain).ValueEquals(this.chainId))
                {
                    return AuditChainBreak.ForeignRecord;
                }

                if ((long)record.Seq != this.records)
                {
                    return AuditChainBreak.SequenceGap;
                }

                return ((JsonElement)record.Prev).ValueEquals(this.previousHash) ? AuditChainBreak.None : AuditChainBreak.HashMismatch;
            }
        }
    }
}