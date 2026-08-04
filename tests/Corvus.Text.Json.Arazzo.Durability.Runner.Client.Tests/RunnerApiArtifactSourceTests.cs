// <copyright file="RunnerApiArtifactSourceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Fixture = Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests.RunnerApiFixture;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client.Tests;

/// <summary>
/// Pulling executor artifacts over the runner API (ADR 0065), which is what lets a runner load a version holding no
/// catalog credential. The catalog and availability stores are the real ones; the source never sees either.
/// </summary>
[TestClass]
public sealed class RunnerApiArtifactSourceTests
{
    private const string Production = Fixture.Production;

    [TestMethod]
    public async Task A_version_available_to_this_runner_serves_its_hash_and_documents()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", Production);
        var artifacts = new RunnerApiArtifactSource(fixture.Transport);

        string? hash = await artifacts.GetContentHashAsync("adopt", 1, default);

        hash.ShouldNotBeNullOrEmpty();

        // Compared against the store directly, so the assertion is that the API serves the same bytes rather than
        // merely serves some. The workflow document stands in for the executor, which this package does not carry.
        ReadOnlyMemory<byte>? fromStore = await fixture.Catalog.GetDocumentAsync("adopt", 1, WorkflowPackage.WorkflowDocumentName, default);
        fromStore.ShouldNotBeNull();

        ReadOnlyMemory<byte>? overApi = await artifacts.GetDocumentAsync("adopt", 1, WorkflowPackage.WorkflowDocumentName, default);
        overApi.ShouldNotBeNull();
        overApi!.Value.Span.SequenceEqual(fromStore!.Value.Span).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_version_in_an_environment_this_runner_does_not_serve_is_answered_as_absent()
    {
        // Not "forbidden": that would confirm the version exists. A runner cannot tell this from a version that was
        // never catalogued, which is the point.
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedCatalogAsync("secret", "staging");
        var artifacts = new RunnerApiArtifactSource(fixture.Transport);

        (await artifacts.GetContentHashAsync("secret", 1, default)).ShouldBeNull();
        (await artifacts.GetDocumentAsync("secret", 1, WorkflowPackage.WorkflowDocumentName, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_version_that_does_not_exist_is_answered_the_same_way()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        var artifacts = new RunnerApiArtifactSource(fixture.Transport);

        (await artifacts.GetContentHashAsync("never", 1, default)).ShouldBeNull();
        (await artifacts.GetDocumentAsync("never", 1, WorkflowPackage.WorkflowDocumentName, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_document_that_is_not_in_the_package_is_absent_rather_than_an_error()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", Production);
        var artifacts = new RunnerApiArtifactSource(fixture.Transport);

        (await artifacts.GetDocumentAsync("adopt", 1, "no-such-document.bin", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_runner_is_told_which_versions_it_may_execute()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", Production);
        await fixture.SeedCatalogAsync("renew", Production);
        await fixture.SeedCatalogAsync("secret", "staging");

        IReadOnlyList<RunnerHostedVersion> hosted = await fixture.Client.ListHostedVersionsAsync();

        // The staging version is not offered: the listing is what this runner may execute, not what exists.
        hosted.Select(static v => v.BaseWorkflowId).ShouldBe(["adopt", "renew"], ignoreOrder: true);
        hosted.ShouldAllBe(static v => v.VersionNumber == 1);
        hosted.ShouldAllBe(static v => v.Hash.Length > 0);
    }

    [TestMethod]
    public async Task A_runner_bound_to_nothing_is_told_it_hosts_nothing()
    {
        await using Fixture fixture = await Fixture.StartAsync();
        await fixture.SeedCatalogAsync("adopt", Production);

        (await fixture.StrangerClient.ListHostedVersionsAsync()).ShouldBeEmpty();
    }
}