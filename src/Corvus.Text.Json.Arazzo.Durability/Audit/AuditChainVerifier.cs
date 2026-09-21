// <copyright file="AuditChainVerifier.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// Verifies an audit chain from its stored bytes (ADR 0069): every line is an audit record by its schema, the first opens
/// the chain and no other does, the records
/// name one chain, their sequence runs from zero with no gap, and each carries the hash of the line before it. Given a
/// trust store it checks every head's signature, and given an anchor it checks the chain holds it. It reads the sink's
/// bytes directly, so the control plane that wrote them is not in the path that checks them.
/// </summary>
public static class AuditChainVerifier
{
    /// <summary>Verifies one chain, stopping at the first break.</summary>
    /// <param name="chain">The chain's JSON Lines bytes.</param>
    /// <param name="options">What the chain is verified against beyond its own links: a trust store, a hash, an anchor.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>What was found.</returns>
    public static async ValueTask<AuditChainVerification> VerifyAsync(Stream chain, AuditChainVerificationOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chain);

        using var state = new State(options ?? new AuditChainVerificationOptions());
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

    private sealed class State(AuditChainVerificationOptions options) : IDisposable
    {
        private readonly ReadOnlyMemory<byte> expectedHash = options.ExpectedHash;
        private readonly byte[] chainId = new byte[AuditRecord.ChainIdLength];
        private readonly byte[] previousHash = InitialHash();
        private readonly IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long line;
        private long records;
        private string? writer;
        private string? continuesChain;
        private string? continuesHash;
        private bool containsExpectedHash;
        private long heads;
        private long lastHeadRecord;
        private bool anchorFound;

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
            if (!this.expectedHash.IsEmpty && this.expectedHash.Span.SequenceEqual(this.previousHash))
            {
                this.containsExpectedHash = true;
            }
        }

        public AuditChainVerification ToResult()
        {
            // An anchor is a property of the whole chain, so it is judged once every line has been read, and only of a
            // chain that is otherwise intact: a break elsewhere is the more specific finding.
            if (this.Break == AuditChainBreak.None && options.Anchor is not null && !this.anchorFound)
            {
                this.Break = AuditChainBreak.AnchorNotFound;
                this.line = 0;
            }

            return new(
                this.Break,
                this.Break == AuditChainBreak.None ? 0 : this.line,
                this.records == 0 ? null : Encoding.UTF8.GetString(this.chainId),
                this.records,
                this.records == 0 ? null : Encoding.UTF8.GetString(this.previousHash),
                this.continuesChain,
                this.continuesHash,
                this.containsExpectedHash,
                this.heads,
                options.TrustStore is not null,
                this.anchorFound,
                this.records - this.lastHeadRecord,
                this.records == 0 ? null : this.writer);
        }

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

        // A head's links are already checked, so its previous-hash is the chain's tail. What remains is that the audit's
        // key signed exactly that: this chain, this position, this tail.
        private AuditChainBreak CheckHead(in AuditRecord.HeadRecord head)
        {
            if (options.TrustStore is { } trustStore)
            {
                if (!((JsonElement)head.Signature.Value).TryGetBytesFromBase64(out byte[]? value))
                {
                    return AuditChainBreak.HeadSignatureInvalid;
                }

                var signature = new ExecutorPackageSignature((string)head.Signature.Algorithm, (string)head.Signature.KeyId, value);
                byte[] statement = ArrayPool<byte>.Shared.Rent(AuditRecord.MaxHeadStatementLength);
                try
                {
                    int length = AuditRecord.WriteHeadStatement(statement, this.chainId, this.records, this.previousHash);
                    if (!trustStore.Verify(statement.AsMemory(0, length), in signature))
                    {
                        return AuditChainBreak.HeadSignatureInvalid;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(statement);
                }
            }

            if (options.Anchor is { } anchor
                && anchor.Sequence == this.records
                && ((JsonElement)head.Prev).ValueEquals(anchor.PreviousHash)
                && ((JsonElement)head.Chain).ValueEquals(anchor.ChainId))
            {
                this.anchorFound = true;
            }

            this.heads++;
            this.lastHeadRecord = this.records + 1;
            return AuditChainBreak.None;
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

                // The fields that place a record in its chain are the same in both kinds, so they are read from the
                // element; what differs by kind is read through the kind.
                JsonElement element = record;
                JsonElement chainIdElement = element.GetProperty("chain"u8);
                if (this.records == 0)
                {
                    using UnescapedUtf8JsonString id = chainIdElement.GetUtf8String();
                    id.Span.CopyTo(this.chainId);

                    // A chain's first record opens it, and nothing else does: it is where the chain names its writer and
                    // the chain it continues, so a chain that starts with anything else has lost its beginning.
                    if (!record.TryGetAsOpenRecord(out AuditRecord.OpenRecord open))
                    {
                        return AuditChainBreak.MalformedRecord;
                    }

                    this.writer = (string)open.Writer;
                    if (open.Continues.IsNotUndefined())
                    {
                        this.continuesChain = (string)open.Continues.Chain;
                        this.continuesHash = (string)open.Continues.Hash;
                    }
                }
                else if (record.TryGetAsOpenRecord(out _))
                {
                    return AuditChainBreak.MalformedRecord;
                }

                if (this.records > 0 && !chainIdElement.ValueEquals(this.chainId))
                {
                    return AuditChainBreak.ForeignRecord;
                }

                if (element.GetProperty("seq"u8).GetInt64() != this.records)
                {
                    return AuditChainBreak.SequenceGap;
                }

                if (!element.GetProperty("prev"u8).ValueEquals(this.previousHash))
                {
                    return AuditChainBreak.HashMismatch;
                }

                return record.TryGetAsHeadRecord(out AuditRecord.HeadRecord head) ? this.CheckHead(in head) : AuditChainBreak.None;
            }
        }
    }
}