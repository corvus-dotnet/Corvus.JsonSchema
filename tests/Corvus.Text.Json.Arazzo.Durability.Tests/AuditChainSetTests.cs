// <copyright file="AuditChainSetTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The chains of a sink verified together (ADR 0069): what one chain cannot show about itself, its neighbours and the
/// anchors held outside the sink do.
/// </summary>
[TestClass]
public sealed class AuditChainSetTests
{
    private static readonly AuditEntry Approve = new("access-request.approve", "alice", "acme", "access-request", "req-1", "granted", "production");

    [TestMethod]
    public async Task A_chain_abandoned_after_a_failed_append_stands_because_its_successor_continues_it()
    {
        (InMemoryAuditSink sink, _) = await AbandonedThenContinuedAsync();

        AuditChainSetVerification result = await AuditChainSetVerifier.VerifyAsync(Sources(sink));

        result.IsIntact.ShouldBeTrue();
        result.Chains[0].Standing.ShouldBe(AuditChainStanding.AbandonedAndContinued);
        result.Chains[0].Verification.Break.ShouldBe(AuditChainBreak.TornTail);
        result.Chains[1].Standing.ShouldBe(AuditChainStanding.Verified);
    }

    [TestMethod]
    public async Task A_torn_tail_that_no_successor_continues_is_a_chain_cut_short()
    {
        (InMemoryAuditSink sink, _) = await AbandonedThenContinuedAsync();

        // The abandoned chain alone: its torn line is now indistinguishable from a truncation, and is treated as one.
        AuditChainSetVerification alone = await AuditChainSetVerifier.VerifyAsync([Sources(sink)[0]]);
        alone.IsIntact.ShouldBeFalse();
        alone.Chains[0].Standing.ShouldBe(AuditChainStanding.Broken);

        // And a chain cut back past the record its successor names: the successor no longer finds its continuation.
        byte[] abandoned = sink.Snapshot(sink.ChainIds[0]);
        int firstLine = Array.IndexOf(abandoned, (byte)'\n') + 1;
        AuditChainSource cut = new("cut", () => new MemoryStream(abandoned, 0, firstLine + 10));
        AuditChainSetVerification cutBack = await AuditChainSetVerifier.VerifyAsync([cut, Sources(sink)[1]]);
        cutBack.IsIntact.ShouldBeFalse();
        cutBack.Chains[0].Standing.ShouldBe(AuditChainStanding.Broken);
        cutBack.Chains[1].Standing.ShouldBe(AuditChainStanding.ContinuationNotFound);
    }

    [TestMethod]
    public async Task A_chain_removed_whole_is_named_by_the_chain_that_continues_it()
    {
        (InMemoryAuditSink sink, _) = await AbandonedThenContinuedAsync();

        AuditChainSetVerification result = await AuditChainSetVerifier.VerifyAsync([Sources(sink)[1]]);

        result.IsIntact.ShouldBeFalse();
        result.Chains[0].Standing.ShouldBe(AuditChainStanding.PredecessorMissing);
        result.Chains[0].Verification.ContinuesChain.ShouldBe(sink.ChainIds[0]);
    }

    [TestMethod]
    public async Task An_anchor_for_a_chain_the_sink_no_longer_holds_is_unmatched()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new List<AuditHead>();
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(1, TimeSpan.FromHours(1)), onHeadSigned: anchors.Add))
        {
            await writer.AppendAsync(Approve, default);
        }

        var trust = new TrustStoreExecutorPackageVerifier(new Dictionary<string, AsymmetricAlgorithm> { ["audit-1"] = key });
        (await AuditChainSetVerifier.VerifyAsync(Sources(sink), trust, anchors)).IsIntact.ShouldBeTrue();

        // The only record of the chain's existence outside the sink is the anchor: with the chain gone, it says so.
        AuditChainSetVerification gone = await AuditChainSetVerifier.VerifyAsync([], trust, anchors);
        gone.IsIntact.ShouldBeFalse();
        gone.UnmatchedAnchors.ShouldHaveSingleItem().ChainId.ShouldBe(sink.ChainIds[0]);
    }

    [TestMethod]
    public async Task An_anchor_is_held_by_a_chain_that_tears_after_it_and_not_by_one_rewritten_under_it()
    {
        // A signed chain, abandoned after its head, then continued: the torn chain still holds the anchor.
        var inner = new InMemoryAuditSink();
        var sink = new FailOnceSink(inner);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new List<AuditHead>();
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(1, TimeSpan.FromHours(1)), onHeadSigned: anchors.Add))
        {
            await writer.AppendAsync(Approve, default);
            sink.FailNext = true;
            await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Approve, default));
            await writer.AppendAsync(Approve, default);
        }

        var trust = new TrustStoreExecutorPackageVerifier(new Dictionary<string, AsymmetricAlgorithm> { ["audit-1"] = key });
        AuditHead first = anchors[0];
        AuditChainSetVerification held = await AuditChainSetVerifier.VerifyAsync(Sources(inner), trust, [first]);
        held.IsIntact.ShouldBeTrue();
        held.Chains[0].Standing.ShouldBe(AuditChainStanding.AbandonedAndContinued);

        // The same position with another hash is an anchor the chain does not hold, torn tail or not.
        AuditHead other = first with { PreviousHash = new string('a', 64) };
        AuditChainSetVerification notHeld = await AuditChainSetVerifier.VerifyAsync(Sources(inner), trust, [other]);
        notHeld.IsIntact.ShouldBeFalse();
        notHeld.Chains[0].Verification.Break.ShouldBe(AuditChainBreak.AnchorNotFound);
    }

    [TestMethod]
    public async Task A_chain_with_no_whole_record_is_reported_and_does_not_fail_the_set()
    {
        AuditChainSetVerification result = await AuditChainSetVerifier.VerifyAsync([new AuditChainSource("empty", () => new MemoryStream()), new AuditChainSource("torn-first-line", () => new MemoryStream(Encoding.UTF8.GetBytes("{\"chain\":")))]);

        result.IsIntact.ShouldBeTrue();
        result.Chains[0].Standing.ShouldBe(AuditChainStanding.Empty);
        result.Chains[1].Standing.ShouldBe(AuditChainStanding.Empty);
    }

    private static async Task<(InMemoryAuditSink Sink, FailOnceSink Failing)> AbandonedThenContinuedAsync()
    {
        var inner = new InMemoryAuditSink();
        var sink = new FailOnceSink(inner);
        await using var writer = new AuditChainWriter(sink);
        await writer.AppendAsync(Approve, default);
        await writer.AppendAsync(Approve, default);
        sink.FailNext = true;
        await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Approve, default));
        await writer.AppendAsync(Approve, default);
        return (inner, sink);
    }

    private static AuditChainSource[] Sources(InMemoryAuditSink sink)
        => [.. sink.ChainIds.Select(id => new AuditChainSource(id, () => new MemoryStream(sink.Snapshot(id))))];

    private sealed class FailOnceSink(IAuditSink inner) : IAuditSink
    {
        public bool FailNext { get; set; }

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken) => inner.OpenLastChainAsync(writerId, cancellationToken);

        public async ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
            => new Chain(this, await inner.CreateChainAsync(writerId, chainId, cancellationToken));

        private sealed class Chain(FailOnceSink owner, IAuditChainStream chain) : IAuditChainStream
        {
            public ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
            {
                if (owner.FailNext)
                {
                    owner.FailNext = false;
                    chain.AppendAsync(line[..(line.Length / 2)], cancellationToken).AsTask().GetAwaiter().GetResult();
                    throw new IOException("disk full");
                }

                return chain.AppendAsync(line, cancellationToken);
            }

            public ValueTask DisposeAsync() => chain.DisposeAsync();
        }
    }
}