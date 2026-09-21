// <copyright file="AuditChainTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The audit chain (ADR 0069): what the writer stores, what the verifier accepts, and every way a stored chain can be
/// altered that the verifier must name.
/// </summary>
[TestClass]
public sealed class AuditChainTests
{
    private static readonly AuditEntry Approve = new("access-request.approve", "alice", "acme", "access-request", "req-1", "granted", "production");
    private static readonly AuditEntry Refuse = new("access-request.approve", "oscar", null, "access-request", "req-2", "refused-own-request", null);

    [TestMethod]
    public async Task Records_are_numbered_stamped_and_linked_and_the_chain_verifies()
    {
        var sink = new InMemoryAuditSink();
        var clock = new FixedClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        await using var writer = new AuditChainWriter(sink, clock, writerId: "cp-test");

        await writer.AppendAsync(Approve, default);
        await writer.AppendAsync(Refuse, default);
        await writer.AppendAsync(Approve, default);

        string chainId = sink.ChainIds.ShouldHaveSingleItem();
        byte[] stored = sink.Snapshot(chainId);
        string[] lines = Lines(stored);
        lines.Length.ShouldBe(4);

        // The chain's first record opens it: it names the writer, and carries the 64 zeros a first record links to.
        using ParsedJsonDocument<AuditRecord> opening = ParsedJsonDocument<AuditRecord>.Parse(Encoding.UTF8.GetBytes(lines[0]));
        opening.RootElement.EvaluateSchema().ShouldBeTrue();
        opening.RootElement.TryGetAsOpenRecord(out AuditRecord.OpenRecord open).ShouldBeTrue();
        ((string)open.Chain).ShouldBe(chainId);
        ((long)open.Seq).ShouldBe(0);
        ((string)open.Prev).ShouldBe(new string('0', 64));
        ((string)open.Writer).ShouldBe("cp-test");
        open.Continues.IsUndefined().ShouldBeTrue();

        using ParsedJsonDocument<AuditRecord> first = ParsedJsonDocument<AuditRecord>.Parse(Encoding.UTF8.GetBytes(lines[1]));
        first.RootElement.EvaluateSchema().ShouldBeTrue();
        first.RootElement.TryGetAsMutationRecord(out AuditRecord.MutationRecord record).ShouldBeTrue();
        ((string)record.Chain).ShouldBe(chainId);
        ((long)record.Seq).ShouldBe(1);
        ((string)record.Prev).ShouldBe(HashOf(lines[0]));
        lines[1].ShouldContain("\"kind\":\"mutation\"");
        ((string)record.Action).ShouldBe("access-request.approve");
        ((string)record.Actor).ShouldBe("alice");
        ((string)record.Tenant).ShouldBe("acme");
        ((string)record.TargetKind).ShouldBe("access-request");
        ((string)record.TargetId).ShouldBe("req-1");
        ((string)record.Outcome).ShouldBe("granted");
        ((string)record.Environment).ShouldBe("production");
        lines[1].ShouldContain("2026-09-20T12:00:00");

        // A record with no tenant and no environment says nothing about them, rather than recording a placeholder.
        lines[2].ShouldNotContain("\"tenant\"");
        lines[2].ShouldNotContain("\"environment\"");

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored));
        verification.IsIntact.ShouldBeTrue();
        verification.BreakLine.ShouldBe(0);
        verification.ChainId.ShouldBe(chainId);
        verification.Writer.ShouldBe("cp-test");
        verification.RecordCount.ShouldBe(4);
        verification.LastHash.ShouldBe(HashOf(lines[3]));
        verification.ContinuesChain.ShouldBeNull();
    }

    [TestMethod]
    public async Task An_altered_record_breaks_the_link_from_the_record_after_it()
    {
        string[] lines = Lines(await ChainOfAsync(4));
        lines[1] = lines[1].Replace("\"granted\"", "\"denied\"");

        AuditChainVerification verification = await VerifyAsync(lines);

        verification.Break.ShouldBe(AuditChainBreak.HashMismatch);
        verification.BreakLine.ShouldBe(3);
        verification.RecordCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_removed_record_is_a_sequence_gap()
    {
        string[] lines = Lines(await ChainOfAsync(4));

        AuditChainVerification verification = await VerifyAsync([lines[0], lines[2], lines[3]]);

        verification.Break.ShouldBe(AuditChainBreak.SequenceGap);
        verification.BreakLine.ShouldBe(2);
        verification.RecordCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task Reordered_records_are_a_sequence_gap()
    {
        string[] lines = Lines(await ChainOfAsync(3));

        AuditChainVerification verification = await VerifyAsync([lines[0], lines[2], lines[1]]);

        verification.Break.ShouldBe(AuditChainBreak.SequenceGap);
        verification.BreakLine.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_record_from_another_chain_is_foreign_even_at_the_right_sequence()
    {
        string[] lines = Lines(await ChainOfAsync(3));
        string[] other = Lines(await ChainOfAsync(3));

        AuditChainVerification verification = await VerifyAsync([lines[0], other[1], lines[2]]);

        verification.Break.ShouldBe(AuditChainBreak.ForeignRecord);
        verification.BreakLine.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_line_that_is_not_json_or_not_an_audit_record_is_malformed()
    {
        string[] lines = Lines(await ChainOfAsync(2));

        (await VerifyAsync([lines[0], "not json"])).Break.ShouldBe(AuditChainBreak.MalformedRecord);
        (await VerifyAsync([lines[0], lines[1].Replace("\"actor\"", "\"author\"")])).Break.ShouldBe(AuditChainBreak.MalformedRecord);
        (await VerifyAsync([lines[0], lines[1].Replace("\"mutation\"", "\"rumour\"")])).Break.ShouldBe(AuditChainBreak.MalformedRecord);
    }

    [TestMethod]
    public async Task A_last_line_with_no_line_feed_is_a_torn_tail_and_the_records_before_it_stand()
    {
        byte[] stored = await ChainOfAsync(3);

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored, 0, stored.Length - 20));

        verification.Break.ShouldBe(AuditChainBreak.TornTail);
        verification.BreakLine.ShouldBe(4);
        verification.RecordCount.ShouldBe(3);
        verification.LastHash.ShouldBe(HashOf(Lines(stored)[2]));
    }

    [TestMethod]
    public async Task An_empty_chain_is_intact_and_names_no_chain()
    {
        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream());

        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(0);
        verification.ChainId.ShouldBeNull();
        verification.LastHash.ShouldBeNull();
    }

    [TestMethod]
    public async Task The_verifier_reads_a_chain_that_arrives_a_few_bytes_at_a_time()
    {
        byte[] stored = await ChainOfAsync(5);

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new TrickleStream(stored, 7));

        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(6);
    }

    [TestMethod]
    public async Task A_failed_append_abandons_the_chain_and_the_next_chain_says_where_it_continues_from()
    {
        var inner = new InMemoryAuditSink();
        var sink = new FailingSink(inner);
        await using var writer = new AuditChainWriter(sink);

        await writer.AppendAsync(Approve, default);
        await writer.AppendAsync(Approve, default);

        sink.FailNext = new IOException("disk full");
        AuditAppendException failure = await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Refuse, default));
        failure.InnerException.ShouldBeOfType<IOException>();

        await writer.AppendAsync(Refuse, default);

        inner.ChainIds.Count.ShouldBe(2);
        string abandoned = inner.ChainIds[0];
        string successor = inner.ChainIds[1];

        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(successor)));
        next.IsIntact.ShouldBeTrue();
        next.RecordCount.ShouldBe(2);
        next.ContinuesChain.ShouldBe(abandoned);

        // The abandoned chain ends in the half-stored line, and the hash the successor names is a record it really
        // holds: its last whole one.
        AuditChainVerification previous = await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(abandoned)), new AuditChainVerificationOptions { ExpectedHash = Encoding.UTF8.GetBytes(next.ContinuesHash!) });
        previous.Break.ShouldBe(AuditChainBreak.TornTail);
        previous.BreakLine.ShouldBe(4);
        previous.RecordCount.ShouldBe(3);
        previous.LastHash.ShouldBe(next.ContinuesHash);
        previous.ContainsExpectedHash.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_cancelled_append_abandons_the_chain_too()
    {
        var inner = new InMemoryAuditSink();
        var sink = new FailingSink(inner);
        await using var writer = new AuditChainWriter(sink);
        await writer.AppendAsync(Approve, default);

        sink.FailNext = new OperationCanceledException();
        await Should.ThrowAsync<OperationCanceledException>(async () => await writer.AppendAsync(Approve, default));
        await writer.AppendAsync(Approve, default);

        inner.ChainIds.Count.ShouldBe(2);
        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(inner.ChainIds[1])));
        next.ContinuesChain.ShouldBe(inner.ChainIds[0]);
    }

    [TestMethod]
    public async Task A_chain_that_never_took_a_record_is_not_what_the_next_chain_continues()
    {
        var inner = new InMemoryAuditSink();
        var sink = new FailingSink(inner);
        await using var writer = new AuditChainWriter(sink);

        sink.FailNext = new IOException("disk full");
        await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Approve, default));
        await writer.AppendAsync(Approve, default);

        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(inner.ChainIds[1])));
        next.IsIntact.ShouldBeTrue();
        next.ContinuesChain.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_sink_that_cannot_create_a_chain_refuses_the_append_and_the_next_append_tries_again()
    {
        var inner = new InMemoryAuditSink();
        var sink = new FailingSink(inner) { FailNextCreate = new IOException("container missing") };
        await using var writer = new AuditChainWriter(sink);

        await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Approve, default));
        await writer.AppendAsync(Approve, default);

        (await AuditChainVerifier.VerifyAsync(new MemoryStream(inner.Snapshot(inner.ChainIds.ShouldHaveSingleItem())))).RecordCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_full_chain_rolls_over_into_one_that_continues_it()
    {
        var sink = new InMemoryAuditSink();
        // A chain's open record counts towards its limit, so three to a chain is an open record and two mutations.
        await using var writer = new AuditChainWriter(sink, maxRecordsPerChain: 3);

        for (int i = 0; i < 5; i++)
        {
            await writer.AppendAsync(Approve, default);
        }

        sink.ChainIds.Count.ShouldBe(3);
        AuditChainVerification first = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[0])));
        AuditChainVerification second = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[1])));
        AuditChainVerification third = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[2])));
        first.RecordCount.ShouldBe(3);
        second.RecordCount.ShouldBe(3);
        third.RecordCount.ShouldBe(2);
        second.ContinuesChain.ShouldBe(first.ChainId);
        second.ContinuesHash.ShouldBe(first.LastHash);
        third.ContinuesChain.ShouldBe(second.ChainId);
        third.ContinuesHash.ShouldBe(second.LastHash);
    }

    [TestMethod]
    public async Task Concurrent_appends_make_one_intact_chain()
    {
        var sink = new InMemoryAuditSink();
        await using var writer = new AuditChainWriter(sink);

        await Task.WhenAll(Enumerable.Range(0, 64).Select(_ => Task.Run(async () => await writer.AppendAsync(Approve, default))));

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem())));
        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(65);
    }

    [TestMethod]
    public async Task A_head_is_signed_at_the_record_cadence_and_vouches_for_the_records_before_it()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new List<AuditHead>();
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(3, TimeSpan.FromHours(1)), onHeadSigned: anchors.Add))
        {
            for (int i = 0; i < 7; i++)
            {
                await writer.AppendAsync(Approve, default);
            }
        }

        // The open record and seven mutations at three to a head: a head after the third and the sixth record, and the
        // close signs the last two.
        byte[] stored = sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem());
        string[] lines = Lines(stored);
        lines.Length.ShouldBe(11);
        lines[3].ShouldContain("\"kind\":\"head\"");
        lines[7].ShouldContain("\"kind\":\"head\"");
        lines[10].ShouldContain("\"kind\":\"head\"");

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored), new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1") });
        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(11);
        verification.HeadCount.ShouldBe(3);
        verification.HeadSignaturesChecked.ShouldBeTrue();
        verification.UnsignedTailCount.ShouldBe(0);

        // Each head was published as an anchor naming the tail it vouches for.
        anchors.Count.ShouldBe(3);
        anchors[0].Sequence.ShouldBe(3);
        anchors[0].PreviousHash.ShouldBe(HashOf(lines[2]));
        anchors[0].KeyId.ShouldBe("audit-1");
    }

    [TestMethod]
    public async Task A_quiet_tail_is_signed_once_its_oldest_record_reaches_the_interval()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var clock = new ManualClock(new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero));
        var signed = new SemaphoreSlim(0);
        await using var writer = new AuditChainWriter(sink, clock, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(1000, TimeSpan.FromSeconds(60)), onHeadSigned: _ => signed.Release());

        await writer.AppendAsync(Approve, default);

        // A tick before the record is a minute old signs nothing.
        clock.Advance(TimeSpan.FromSeconds(30));
        clock.Tick();
        (await signed.WaitAsync(TimeSpan.FromMilliseconds(200))).ShouldBeFalse();

        clock.Advance(TimeSpan.FromSeconds(30));
        clock.Tick();
        (await signed.WaitAsync(TimeSpan.FromSeconds(30))).ShouldBeTrue();

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds[0])), new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1") });
        verification.IsIntact.ShouldBeTrue();
        verification.HeadCount.ShouldBe(1);
        verification.UnsignedTailCount.ShouldBe(0);

        // With nothing unsigned, a later tick signs nothing: a head is never a head over a head.
        clock.Advance(TimeSpan.FromMinutes(5));
        clock.Tick();
        (await signed.WaitAsync(TimeSpan.FromMilliseconds(200))).ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_tail_rewritten_with_its_hashes_recomputed_is_shown_by_the_head_it_cannot_sign()
    {
        // The attack the hash links cannot show: whoever holds the sink rewrites the last records and re-links everything
        // after them, the head included. What they cannot do is sign the head with the audit's key.
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa attacker = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] forged = await SignedChainAsync(attacker, records: 3);

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(forged), new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1") });

        verification.Break.ShouldBe(AuditChainBreak.HeadSignatureInvalid);
        verification.BreakLine.ShouldBe(4);
        verification.RecordCount.ShouldBe(3);

        // The same chain with no trust store reads as intact, and says its heads were not checked.
        AuditChainVerification unchecked_ = await AuditChainVerifier.VerifyAsync(new MemoryStream(forged));
        unchecked_.IsIntact.ShouldBeTrue();
        unchecked_.HeadSignaturesChecked.ShouldBeFalse();
    }

    [TestMethod]
    public async Task A_record_altered_under_a_head_breaks_the_link_into_the_head()
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string[] lines = Lines(await SignedChainAsync(key, records: 3));
        lines[2] = lines[2].Replace("\"granted\"", "\"denied\"");

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n")),
            new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1") });

        verification.Break.ShouldBe(AuditChainBreak.HashMismatch);
        verification.BreakLine.ShouldBe(4);
    }

    [TestMethod]
    public async Task A_chain_cut_short_of_a_published_anchor_does_not_hold_it()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var anchors = new List<AuditHead>();
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(2, TimeSpan.FromHours(1)), onHeadSigned: anchors.Add))
        {
            for (int i = 0; i < 4; i++)
            {
                await writer.AppendAsync(Approve, default);
            }
        }

        byte[] stored = sink.Snapshot(sink.ChainIds[0]);
        string[] lines = Lines(stored);
        lines.Length.ShouldBe(8);
        AuditHead last = anchors[^1];

        AuditChainVerification whole = await AuditChainVerifier.VerifyAsync(new MemoryStream(stored), new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1"), Anchor = last });
        whole.IsIntact.ShouldBeTrue();

        // Keep only the open record, the first mutation and their head: what is left is a well-formed, fully signed chain. Only the anchor,
        // held outside the sink, shows it is not the chain that was signed.
        AuditChainVerification cut = await AuditChainVerifier.VerifyAsync(
            new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines[..3]) + "\n")),
            new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1"), Anchor = last });
        cut.Break.ShouldBe(AuditChainBreak.AnchorNotFound);
        cut.RecordCount.ShouldBe(3);
    }

    [TestMethod]
    public async Task A_head_that_cannot_be_signed_never_fails_the_append_and_is_tried_again()
    {
        var sink = new InMemoryAuditSink();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signer = new FlakySigner(new EcdsaExecutorPackageSigner(key, "audit-1")) { Failing = true };
        var failures = new List<Exception>();
        int signedHeads = 0;
        await using var writer = new AuditChainWriter(sink, headSigner: signer, headOptions: new AuditHeadOptions(3, TimeSpan.FromHours(1)), onHeadSigned: _ => signedHeads++, onHeadFailed: failures.Add);

        await writer.AppendAsync(Approve, default);
        await writer.AppendAsync(Approve, default);

        failures.ShouldHaveSingleItem().ShouldBeOfType<CryptographicException>();
        signedHeads.ShouldBe(0);

        // The key service recovers: the next append is over the cadence, so it signs a head over all four records.
        signer.Failing = false;
        await writer.AppendAsync(Approve, default);
        signedHeads.ShouldBe(1);

        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem())), new AuditChainVerificationOptions { TrustStore = TrustStore(key, "audit-1") });
        verification.IsIntact.ShouldBeTrue();
        verification.RecordCount.ShouldBe(5);
        verification.UnsignedTailCount.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_chain_with_no_signer_is_wholly_unsigned_and_says_so()
    {
        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(new MemoryStream(await ChainOfAsync(5)));

        verification.IsIntact.ShouldBeTrue();
        verification.HeadCount.ShouldBe(0);
        verification.UnsignedTailCount.ShouldBe(6);
    }

    [TestMethod]
    public async Task A_steady_state_append_allocates_nothing_on_the_heap()
    {
        // The record is rendered into a pooled buffer, hashed on the stack and handed to the sink as pooled memory, so
        // what a governance request pays for its audit record is the sink's own I/O and no garbage.
        await using var writer = new AuditChainWriter(new DiscardingSink(), maxRecordsPerChain: long.MaxValue);
        for (int i = 0; i < 200; i++)
        {
            await writer.AppendAsync(Approve, default);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            await writer.AppendAsync(Approve, default);
        }

        long perAppend = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;

        // A Debug build compiles an async method's state machine as a class, so each call allocates it (168 B here). A
        // Release build makes it a struct that never leaves the stack while the append completes synchronously.
#if DEBUG
        perAppend.ShouldBeLessThanOrEqualTo(256);
#else
        perAppend.ShouldBe(0);
#endif
    }

    [TestMethod]
    public async Task A_disposed_writer_refuses_an_append()
    {
        var writer = new AuditChainWriter(new InMemoryAuditSink());
        await writer.DisposeAsync();
        await writer.DisposeAsync();

        await Should.ThrowAsync<ObjectDisposedException>(async () => await writer.AppendAsync(Approve, default));
    }

    [TestMethod]
    public async Task The_file_sink_stores_each_chain_as_its_own_file_and_never_reopens_one()
    {
        string directory = Path.Combine(Path.GetTempPath(), "arazzo-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sink = new FileAuditSink(directory);
            await using (var writer = new AuditChainWriter(sink, writerId: "cp-0"))
            {
                await writer.AppendAsync(Approve, default);
                await writer.AppendAsync(Refuse, default);

                // The records are on disk before the append returns, while the writer still holds the file.
                string open = Directory.GetFiles(directory, "*" + FileAuditSink.ChainFileExtension, SearchOption.AllDirectories).ShouldHaveSingleItem();
                await using var reading = new FileStream(open, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                (await AuditChainVerifier.VerifyAsync(reading)).RecordCount.ShouldBe(3);
            }

            // A writer's chains are kept under its id.
            string file = Directory.GetFiles(Path.Combine(directory, "cp-0")).ShouldHaveSingleItem();
            string chainId = Path.GetFileNameWithoutExtension(file);
            await using (FileStream stored = File.OpenRead(file))
            {
                AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(stored);
                verification.IsIntact.ShouldBeTrue();
                verification.ChainId.ShouldBe(chainId);
            }

            await Should.ThrowAsync<IOException>(async () => await sink.CreateChainAsync("cp-0"u8.ToArray(), Encoding.UTF8.GetBytes(chainId), default));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<byte[]> ChainOfAsync(int records)
    {
        var sink = new InMemoryAuditSink();
        await using var writer = new AuditChainWriter(sink);
        for (int i = 0; i < records; i++)
        {
            await writer.AppendAsync(Approve, default);
        }

        return sink.Snapshot(sink.ChainIds[0]);
    }

    private static async Task<byte[]> SignedChainAsync(ECDsa key, int records)
    {
        var sink = new InMemoryAuditSink();
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(records, TimeSpan.FromHours(1))))
        {
            for (int i = 0; i < records; i++)
            {
                await writer.AppendAsync(Approve, default);
            }
        }

        return sink.Snapshot(sink.ChainIds[0]);
    }

    private static TrustStoreExecutorPackageVerifier TrustStore(ECDsa key, string keyId)
        => new(new Dictionary<string, AsymmetricAlgorithm> { [keyId] = key });

    private static string[] Lines(byte[] stored)
        => Encoding.UTF8.GetString(stored).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static ValueTask<AuditChainVerification> VerifyAsync(string[] lines)
        => AuditChainVerifier.VerifyAsync(new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n")));

    private static string HashOf(string line)
        => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(line)));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset now = now;

        public override DateTimeOffset GetUtcNow() => this.now;

        public void Advance(TimeSpan by) => this.now += by;

        public void Tick()
        {
            foreach (ManualTimer timer in this.timers.ToArray())
            {
                timer.Fire();
            }
        }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state);
            this.timers.Add(timer);
            return timer;
        }

        private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            private bool disposed;

            public void Fire()
            {
                if (!this.disposed)
                {
                    callback(state);
                }
            }

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() => this.disposed = true;

            public ValueTask DisposeAsync()
            {
                this.disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }

    private sealed class FlakySigner(IExecutorPackageSigner inner) : IExecutorPackageSigner
    {
        public bool Failing { get; set; }

        public ValueTask<ExecutorPackageSignature> SignAsync(ReadOnlyMemory<byte> manifestUtf8, CancellationToken cancellationToken)
            => this.Failing ? throw new CryptographicException("the key service is unreachable") : inner.SignAsync(manifestUtf8, cancellationToken);
    }

    private sealed class TrickleStream(byte[] content, int chunk) : MemoryStream(content)
    {
        public override int Read(Span<byte> buffer) => base.Read(buffer[..Math.Min(buffer.Length, chunk)]);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, chunk)], cancellationToken);
    }

    private sealed class DiscardingSink : IAuditSink, IAuditChainStream
    {
        public ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken) => new(this);

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken) => new((Stream?)null);

        public ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailingSink(IAuditSink inner) : IAuditSink
    {
        public Exception? FailNext { get; set; }

        public Exception? FailNextCreate { get; set; }

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken) => inner.OpenLastChainAsync(writerId, cancellationToken);

        public async ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
        {
            if (this.FailNextCreate is { } failure)
            {
                this.FailNextCreate = null;
                throw failure;
            }

            return new Chain(this, await inner.CreateChainAsync(writerId, chainId, cancellationToken));
        }

        private sealed class Chain(FailingSink owner, IAuditChainStream chain) : IAuditChainStream
        {
            public ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
            {
                if (owner.FailNext is { } failure)
                {
                    owner.FailNext = null;

                    // Half the line reaches the sink before the failure, which is what makes the tail unknowable.
                    chain.AppendAsync(line[..(line.Length / 2)], cancellationToken).AsTask().GetAwaiter().GetResult();
                    throw failure;
                }

                return chain.AppendAsync(line, cancellationToken);
            }

            public ValueTask DisposeAsync() => chain.DisposeAsync();
        }
    }
}