// <copyright file="AuditChainResumeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// A chain does not outlive its process (ADR 0069), so a writer that starts reads its own last chain back and continues
/// it under a head signed at once. That links the chains and freezes the tail the last process left unsigned. It does
/// not authenticate that tail, and these tests pin both halves.
/// </summary>
[TestClass]
public sealed class AuditChainResumeTests
{
    private static readonly AuditEntry Approve = new("access-request.approve", "alice", "acme", "access-request", "req-1", "granted", "production");

    [TestMethod]
    public async Task A_writer_that_starts_again_continues_its_last_chain_under_a_head_signed_at_once()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // The first process records two actions and is gone without closing its chain: a crash. Its tail is unsigned.
        AuditChainWriter crashed = Writer(sink, key, "cp-0");
        await crashed.AppendAsync(Approve, default);
        await crashed.AppendAsync(Approve, default);

        AuditChainVerification? seen = null;
        await using (var restarted = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)), writerId: "cp-0", onResumed: v => seen = v))
        {
            await restarted.ResumeAsync(default);

            // Before any action is recorded the new chain is open, names the old one, and is signed.
            sink.ChainIds.Count.ShouldBe(2);
            string[] opening = Lines(sink.Snapshot(sink.ChainIds[1]));
            opening.Length.ShouldBe(2);
            opening[0].ShouldContain("\"kind\":\"open\"");
            opening[0].ShouldContain(sink.ChainIds[0]);
            opening[1].ShouldContain("\"kind\":\"head\"");
        }

        seen.ShouldNotBeNull();
        seen.Value.ChainId.ShouldBe(sink.ChainIds[0]);
        seen.Value.RecordCount.ShouldBe(3);

        AuditChainSetVerification set = await AuditChainSetVerifier.VerifyAsync(Sources(sink), Trust(key));
        set.IsIntact.ShouldBeTrue();

        // The old chain's tail is frozen, and still reported as unsigned: nothing authenticated it.
        set.Chains[0].Verification.UnsignedTailCount.ShouldBe(3);
        set.Chains[0].FrozenBy.ShouldBe(sink.ChainIds[1]);
        set.Chains[1].Verification.ContinuesHash.ShouldBe(set.Chains[0].Verification.LastHash);
        set.Chains[1].Verification.UnsignedTailCount.ShouldBe(0);
    }

    [TestMethod]
    public async Task Once_continued_the_old_tail_cannot_be_altered_or_cut_or_the_chain_removed_without_it_showing()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuditChainWriter crashed = Writer(sink, key, "cp-0");
        await crashed.AppendAsync(Approve, default);
        await crashed.AppendAsync(Approve, default);
        await using (AuditChainWriter restarted = Writer(sink, key, "cp-0"))
        {
            await restarted.ResumeAsync(default);
        }

        byte[] old = sink.Snapshot(sink.ChainIds[0]);
        AuditChainSource successor = Sources(sink)[1];

        // The last record of the unsigned tail altered: no link breaks inside the old chain, since nothing follows it, and
        // no signature covers it. The successor's continuation is what shows it.
        string[] lines = Lines(old);
        lines[^1] = lines[^1].Replace("\"granted\"", "\"denied\"");
        AuditChainSource altered = new("old", () => new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n")));
        (await AuditChainSetVerifier.VerifyAsync([altered, successor], Trust(key))).Chains[1].Standing.ShouldBe(AuditChainStanding.ContinuationNotFound);

        // The tail cut off at a record boundary, which leaves a well-formed chain.
        AuditChainSource cut = new("old", () => new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', Lines(old)[..2]) + "\n")));
        (await AuditChainSetVerifier.VerifyAsync([cut, successor], Trust(key))).Chains[1].Standing.ShouldBe(AuditChainStanding.ContinuationNotFound);

        // The old chain removed whole.
        (await AuditChainSetVerifier.VerifyAsync([successor], Trust(key))).Chains[0].Standing.ShouldBe(AuditChainStanding.PredecessorMissing);
    }

    [TestMethod]
    public async Task A_tail_altered_before_the_restart_is_continued_as_it_stands_which_is_the_limit()
    {
        // Between a crash and the restart the tail is whatever the sink holds. A record altered then is read back, its
        // hash named by the new chain, and the set verifies. Continuation freezes; it does not authenticate.
        var inner = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuditChainWriter crashed = Writer(inner, key, "cp-0");
        await crashed.AppendAsync(Approve, default);

        string[] lines = Lines(inner.Snapshot(inner.ChainIds[0]));
        lines[^1] = lines[^1].Replace("\"granted\"", "\"denied\"");
        byte[] tampered = Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n");
        var sink = new RewrittenLastChainSink(inner, tampered);

        await using (AuditChainWriter restarted = Writer(sink, key, "cp-0"))
        {
            await restarted.ResumeAsync(default);
        }

        AuditChainSource old = new("old", () => new MemoryStream(tampered));
        AuditChainSource successor = new("new", () => new MemoryStream(inner.Snapshot(inner.ChainIds[1])));
        AuditChainSetVerification set = await AuditChainSetVerifier.VerifyAsync([old, successor], Trust(key));
        set.IsIntact.ShouldBeTrue();
        set.Chains[0].Verification.UnsignedTailCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_last_chain_that_fails_verification_is_reported_and_continued_from_its_last_good_record()
    {
        var inner = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuditChainWriter crashed = Writer(inner, key, "cp-0");
        await crashed.AppendAsync(Approve, default);
        await crashed.AppendAsync(Approve, default);
        await crashed.AppendAsync(Approve, default);

        // The middle record altered: the record after it no longer links.
        string[] lines = Lines(inner.Snapshot(inner.ChainIds[0]));
        lines[2] = lines[2].Replace("\"granted\"", "\"denied\"");
        var sink = new RewrittenLastChainSink(inner, Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n"));

        AuditChainVerification? seen = null;
        await using (var restarted = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), writerId: "cp-0", onResumed: v => seen = v))
        {
            await restarted.AppendAsync(Approve, default);
        }

        seen.ShouldNotBeNull();
        seen.Value.Break.ShouldBe(AuditChainBreak.HashMismatch);
        seen.Value.BreakLine.ShouldBe(4);

        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(inner.ChainIds[1])));
        next.IsIntact.ShouldBeTrue();
        next.ContinuesChain.ShouldBe(inner.ChainIds[0]);
        next.ContinuesHash.ShouldBe(seen.Value.LastHash);
    }

    [TestMethod]
    public async Task A_writer_continues_only_its_own_chains_and_its_first_append_resumes_it_if_nothing_else_has()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        AuditChainWriter other = Writer(sink, key, "cp-0");
        await other.AppendAsync(Approve, default);

        await using (AuditChainWriter stranger = Writer(sink, key, "cp-1"))
        {
            await stranger.AppendAsync(Approve, default);
        }

        AuditChainVerification strangers = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[1])));
        strangers.Writer.ShouldBe("cp-1");
        strangers.ContinuesChain.ShouldBeNull();

        // cp-0 again, never told to resume: its first append does it.
        await using (AuditChainWriter again = Writer(sink, key, "cp-0"))
        {
            await again.AppendAsync(Approve, default);
        }

        AuditChainVerification continued = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[2])));
        continued.Writer.ShouldBe("cp-0");
        continued.ContinuesChain.ShouldBe(sink.ChainIds[0]);
    }

    [TestMethod]
    public async Task The_file_sink_finds_a_writers_last_chain_among_several()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arazzo-audit-resume-" + Guid.NewGuid().ToString("N"));
        try
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var chains = new List<string>();
            for (int process = 0; process < 3; process++)
            {
                await using AuditChainWriter writer = Writer(new FileAuditSink(directory), key, "cp-0");
                await writer.ResumeAsync(default);
                await writer.AppendAsync(Approve, default);
                chains.Add(Directory.GetFiles(Path.Combine(directory, "cp-0")).Order(StringComparer.Ordinal).Last());
            }

            // Each process continued the one before it, and chain ids sort by when they were opened.
            chains.ShouldBe(chains.Order(StringComparer.Ordinal).ToList());
            AuditChainSource[] sources = [.. chains.Select(f => new AuditChainSource(Path.GetFileName(f), () => File.OpenRead(f)))];
            AuditChainSetVerification set = await AuditChainSetVerifier.VerifyAsync(sources, Trust(key));
            set.IsIntact.ShouldBeTrue();
            set.Chains[1].Verification.ContinuesChain.ShouldBe(set.Chains[0].Verification.ChainId);
            set.Chains[2].Verification.ContinuesChain.ShouldBe(set.Chains[1].Verification.ChainId);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task A_chain_that_has_lost_its_open_record_or_gained_a_second_is_malformed()
    {
        var sink = new InMemoryAuditSink();
        await using (var writer = new AuditChainWriter(sink, writerId: "cp-0"))
        {
            await writer.AppendAsync(Approve, default);
            await writer.AppendAsync(Approve, default);
        }

        string[] lines = Lines(sink.Snapshot(sink.ChainIds[0]));

        // Without its first record a chain no longer says whose it is or what it continues.
        AuditChainVerification beheaded = await AuditChainVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines[1..]) + "\n")));
        beheaded.Break.ShouldBe(AuditChainBreak.MalformedRecord);
        beheaded.BreakLine.ShouldBe(1);

        AuditChainVerification reopened = await AuditChainVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', [lines[0], lines[0]]) + "\n")));
        reopened.Break.ShouldBe(AuditChainBreak.MalformedRecord);
        reopened.BreakLine.ShouldBe(2);
    }

    [TestMethod]
    public void A_writer_id_outside_the_grammar_is_refused_and_the_default_is_within_it()
    {
        var sink = new InMemoryAuditSink();
        Should.Throw<ArgumentException>(() => new AuditChainWriter(sink, writerId: "Control Plane"));
        Should.Throw<ArgumentException>(() => new AuditChainWriter(sink, writerId: string.Empty));
        Should.Throw<ArgumentException>(() => new AuditChainWriter(sink, writerId: new string('a', 64)));
        Should.NotThrow(() => new AuditChainWriter(sink));
    }

    private static AuditChainWriter Writer(IAuditSink sink, ECDsa key, string writerId)
        => new(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)), writerId: writerId);

    private static TrustStoreExecutorPackageVerifier Trust(ECDsa key)
        => new(new Dictionary<string, AsymmetricAlgorithm> { ["audit-1"] = key });

    private static AuditChainSource[] Sources(InMemoryAuditSink sink)
        => [.. sink.ChainIds.Select(id => new AuditChainSource(id, () => new MemoryStream(sink.Snapshot(id))))];

    private static string[] Lines(byte[] stored)
        => Encoding.UTF8.GetString(stored).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private sealed class RewrittenLastChainSink(IAuditSink inner, byte[] lastChain) : IAuditSink
    {
        public ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
            => inner.CreateChainAsync(writerId, chainId, cancellationToken);

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken)
            => new(new MemoryStream(lastChain));
    }
}