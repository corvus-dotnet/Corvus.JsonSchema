// <copyright file="TenantAnchorStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="ITenantAnchorStore"/> must satisfy, regardless of backend (ADR 0065, the
/// normative tenant-anchor specification): a whole-record compare-and-swap that admits exactly what
/// <see cref="AnchorAcceptance.Classify"/> admits and nothing else, and a strictly monotonic incarnation attestation.
/// A backend's test project derives a concrete test class from this and implements <see cref="CreateStoreAsync"/>;
/// the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class TenantAnchorStoreConformance
{
    private const string Production = "production";
    private const string Run = "00000000000000000000000000000001";

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ITenantAnchorStore> CreateStoreAsync();

    /// <summary>Disposes any stores created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task An_unattested_environment_reads_null_and_admits_no_write()
    {
        ITenantAnchorStore store = await this.NewStoreAsync();

        (await store.ReadAttestedIncarnationAsync(Production, default)).ShouldBeNull();
        (await store.ReadAsync(Production, Run, default)).ShouldBeNull();
        (await store.WriteAsync(Production, null, Created(1, 7), default)).ShouldBe(AnchorWriteKind.Rejected, "every clause is stated against the attested incarnation, and there is none");
        (await store.ReadAsync(Production, Run, default)).ShouldBeNull("a rejected write writes nothing");
    }

    [TestMethod]
    public async Task Attestation_is_strictly_monotonic_and_never_zero()
    {
        ITenantAnchorStore store = await this.NewStoreAsync();

        (await store.AttestIncarnationAsync(Production, 0, default)).ShouldBeFalse("zero is what an unattested region reads as");
        (await store.AttestIncarnationAsync(Production, 1, default)).ShouldBeTrue();
        (await store.ReadAttestedIncarnationAsync(Production, default)).ShouldBe(1UL);
        (await store.AttestIncarnationAsync(Production, 1, default)).ShouldBeFalse("equal is not above");
        (await store.AttestIncarnationAsync(Production, 3, default)).ShouldBeTrue("a restore may skip values; only the direction is fixed");
        (await store.AttestIncarnationAsync(Production, 2, default)).ShouldBeFalse("and it never goes back");
        (await store.ReadAttestedIncarnationAsync(Production, default)).ShouldBe(3UL);
        (await store.ReadAttestedIncarnationAsync("staging", default)).ShouldBeNull("attestation is per environment");
    }

    [TestMethod]
    public async Task A_write_is_admitted_by_the_clause_the_predicate_names_and_stored_whole()
    {
        ITenantAnchorStore store = await this.NewStoreAsync();
        await store.AttestIncarnationAsync(Production, 1, default);

        AnchorRecord created = Created(1, 7);
        (await store.WriteAsync(Production, null, created, default)).ShouldBe(AnchorWriteKind.Create);
        (await store.ReadAsync(Production, Run, default)).ShouldBe(created, "the record round-trips whole");

        AnchorRecord prepared = created with { Pending = Mark(1, 7, 1, Digest(1)), EpochHighWater = Key(1, 7) };
        (await store.WriteAsync(Production, created, prepared, default)).ShouldBe(AnchorWriteKind.Prepare);
        (await store.ReadAsync(Production, Run, default)).ShouldBe(prepared);

        AnchorRecord fused = prepared with { Committed = prepared.Pending!.Value, Pending = Mark(1, 7, 2, Digest(2)) };
        (await store.WriteAsync(Production, prepared, fused, default)).ShouldBe(AnchorWriteKind.PromoteAndPrepare);

        AnchorRecord promoted = fused with { Committed = fused.Pending!.Value, Pending = null };
        (await store.WriteAsync(Production, fused, promoted, default)).ShouldBe(AnchorWriteKind.Promote);

        AnchorRecord finalized = promoted with { State = AnchorState.Terminal, Disposition = AnchorDisposition.Completed };
        (await store.WriteAsync(Production, promoted, finalized, default)).ShouldBe(AnchorWriteKind.Finalize);
        (await store.ReadAsync(Production, Run, default)).ShouldBe(finalized);
    }

    [TestMethod]
    public async Task A_write_no_clause_admits_is_rejected_and_writes_nothing()
    {
        // The primitive the predicate exists to remove: a bare committed advance with no pending to promote.
        ITenantAnchorStore store = await this.NewStoreAsync();
        await store.AttestIncarnationAsync(Production, 1, default);
        AnchorRecord created = Created(1, 7);
        await store.WriteAsync(Production, null, created, default);

        AnchorRecord bare = created with { Committed = Mark(1, 7, 5, Digest(5)) };
        (await store.WriteAsync(Production, created, bare, default)).ShouldBe(AnchorWriteKind.Rejected);
        (await store.ReadAsync(Production, Run, default)).ShouldBe(created, "the stored record is untouched");

        // And a create that is not the fully constrained genesis shape.
        (await store.WriteAsync(Production, null, Created(1, 7) with { RunId = "00000000000000000000000000000002", ReanchorCounter = 5 }, default)).ShouldBe(AnchorWriteKind.Rejected);
        (await store.ReadAsync(Production, "00000000000000000000000000000002", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_write_against_a_record_that_moved_is_rejected()
    {
        // The compare half: the writer decided against a record a second writer has since replaced. The sole-writer
        // rule forbids a second writer, so this is a refusal and never a retry.
        ITenantAnchorStore store = await this.NewStoreAsync();
        await store.AttestIncarnationAsync(Production, 1, default);
        AnchorRecord created = Created(1, 7);
        await store.WriteAsync(Production, null, created, default);
        AnchorRecord prepared = created with { Pending = Mark(1, 7, 1, Digest(1)), EpochHighWater = Key(1, 7) };
        (await store.WriteAsync(Production, created, prepared, default)).ShouldBe(AnchorWriteKind.Prepare);

        // A stale writer still holding `created` stages a different sequence-1 mark: a clause admits its write over
        // `created`, but `created` is no longer what is stored.
        AnchorRecord stale = created with { Pending = Mark(1, 7, 1, Digest(9)), EpochHighWater = Key(1, 7) };
        (await store.WriteAsync(Production, created, stale, default)).ShouldBe(AnchorWriteKind.Rejected);
        (await store.ReadAsync(Production, Run, default)).ShouldBe(prepared);

        // Expecting a record where there is none, and expecting none where there is one, are both misses.
        (await store.WriteAsync(Production, null, Created(1, 7), default)).ShouldBe(AnchorWriteKind.Rejected);
        (await store.WriteAsync("staging", created, prepared, default)).ShouldBe(AnchorWriteKind.Rejected, "the record's environment is part of its identity");
    }

    [TestMethod]
    public async Task Of_two_first_claims_racing_to_create_exactly_one_lands()
    {
        ITenantAnchorStore store = await this.NewStoreAsync();
        await store.AttestIncarnationAsync(Production, 1, default);

        AnchorRecord[] proposals = Enumerable.Range(0, 8).Select(i => Created(1, (ulong)(7 + i))).ToArray();
        AnchorWriteKind[] outcomes = await Task.WhenAll(proposals.Select(p => Task.Run(async () => await store.WriteAsync(Production, null, p, default))));

        outcomes.Count(k => k == AnchorWriteKind.Create).ShouldBe(1);
        outcomes.Count(k => k == AnchorWriteKind.Rejected).ShouldBe(7);
        AnchorRecord? stored = await store.ReadAsync(Production, Run, default);
        stored.ShouldNotBeNull();
        proposals.ShouldContain(stored!.Value);
    }

    [TestMethod]
    public async Task The_predicate_is_evaluated_against_the_attested_incarnation_the_store_holds()
    {
        // A5 as the store can enforce it: a create under an incarnation other than the attested one is refused, and
        // a prepare whose key is below the high-water mark is refused, both by the predicate the store delegates to.
        ITenantAnchorStore store = await this.NewStoreAsync();
        await store.AttestIncarnationAsync(Production, 2, default);

        (await store.WriteAsync(Production, null, Created(1, 7), default)).ShouldBe(AnchorWriteKind.Rejected, "incarnation 1 is not the attested 2");
        AnchorRecord created = Created(2, 7);
        (await store.WriteAsync(Production, null, created, default)).ShouldBe(AnchorWriteKind.Create);
        (await store.WriteAsync(Production, created, created with { Pending = Mark(2, 6, 1, Digest(1)), EpochHighWater = Key(2, 6) }, default)).ShouldBe(AnchorWriteKind.Rejected, "epoch 6 is below the high-water mark");
        (await store.WriteAsync(Production, created, created with { Pending = Mark(2, 8, 1, Digest(1)), EpochHighWater = Key(2, 8) }, default)).ShouldBe(AnchorWriteKind.Prepare);
    }

    private static AnchorOrderingKey Key(ulong incarnation, ulong epoch) => new(incarnation, epoch);

    private static AnchorDigest Digest(byte seed)
    {
        Span<byte> bytes = stackalloc byte[32];
        bytes.Fill(seed);
        return new AnchorDigest(bytes);
    }

    private static AnchorMark Mark(ulong incarnation, ulong epoch, ulong sequence, AnchorDigest digest) => new(Key(incarnation, epoch), sequence, digest);

    private static AnchorRecord Created(ulong incarnation, ulong epoch)
        => new(Run, Production, AnchorState.Live, Key(incarnation, epoch), Mark(incarnation, epoch, 0, Digest(0)), null, 0);

    private async ValueTask<ITenantAnchorStore> NewStoreAsync()
    {
        ITenantAnchorStore store = await this.CreateStoreAsync();
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}