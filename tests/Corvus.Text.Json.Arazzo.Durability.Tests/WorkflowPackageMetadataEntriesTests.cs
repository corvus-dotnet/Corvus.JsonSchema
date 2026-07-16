// <copyright file="WorkflowPackageMetadataEntriesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The §4.6 reserved metadata entries: <c>metadata/scenarios.json</c> and
/// <c>metadata/evidence.json</c> pack deterministically, read back through
/// <see cref="WorkflowPackage.TryReadEntry"/>, survive the catalog's canonical repack
/// (<see cref="CatalogPackage.Project"/>), and do not perturb the content hash — evidence is
/// metadata, not content.
/// </summary>
[TestClass]
public sealed class WorkflowPackageMetadataEntriesTests
{
    private static readonly byte[] Workflow = Encoding.UTF8.GetBytes(
        """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"wf","steps":[]}]}""");

    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("pets", Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"p","version":"1"},"paths":{}}"""))];

    private static readonly byte[] Scenarios = Encoding.UTF8.GetBytes("""[{"name":"happy"}]""");
    private static readonly byte[] Evidence = Encoding.UTF8.GetBytes("""{"at":"2026-01-01T00:00:00Z","suite":{"total":1,"passed":1,"failed":0},"scenarios":[]}""");

    [TestMethod]
    public void Metadata_entries_round_trip_and_leave_plain_reads_untouched()
    {
        byte[] package = CatalogPackage.Build(Workflow, Sources, Scenarios, Evidence);

        WorkflowPackage.TryReadEntry(package, "metadata/scenarios.json"u8, out ReadOnlyMemory<byte> scenarios).ShouldBeTrue();
        scenarios.ToArray().ShouldBe(Scenarios);
        WorkflowPackage.TryReadEntry(package, "metadata/evidence.json"u8, out ReadOnlyMemory<byte> evidence).ShouldBeTrue();
        evidence.ToArray().ShouldBe(Evidence);
        WorkflowPackage.TryReadEntry(package, "metadata/nope.json"u8, out _).ShouldBeFalse();

        // The classic reads see exactly the workflow + sources they always did.
        (byte[] workflow, IReadOnlyList<KeyValuePair<string, byte[]>> sources) = CatalogPackage.Unpack(package);
        workflow.ShouldBe(Workflow);
        sources.Count.ShouldBe(1);

        // Determinism: same inputs, identical bytes.
        CatalogPackage.Build(Workflow, Sources, Scenarios, Evidence).ShouldBe(package);
    }

    [TestMethod]
    public void Metadata_entries_survive_the_canonical_repack_without_perturbing_the_content_hash()
    {
        byte[] bare = CatalogPackage.Build(Workflow, Sources);
        byte[] carrying = CatalogPackage.Build(Workflow, Sources, Scenarios, Evidence);

        CatalogPackageProjection bareProjection = CatalogPackage.Project(bare, "wf", 1);
        CatalogPackageProjection carryingProjection = CatalogPackage.Project(carrying, "wf", 1);

        // Evidence is metadata, not content: the hash canonicalises only {workflow, sources}.
        carryingProjection.Hash.ShouldBe(bareProjection.Hash);

        // And the canonical package still carries both entries after the repack.
        WorkflowPackage.TryReadEntry(carryingProjection.CanonicalPackage, "metadata/scenarios.json"u8, out ReadOnlyMemory<byte> scenarios).ShouldBeTrue();
        scenarios.ToArray().ShouldBe(Scenarios);
        WorkflowPackage.TryReadEntry(carryingProjection.CanonicalPackage, "metadata/evidence.json"u8, out _).ShouldBeTrue();
    }
}