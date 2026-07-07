// <copyright file="DraftRunStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IDraftRunStore"/> must satisfy, regardless of backend: the §18 draft-run
/// capture (the audited <see cref="DraftRun"/> record + the packed document/sources) round-trips keyed by run
/// id, a put replaces, a delete removes, and a missing capture reads as absent. A backend's test project
/// derives a concrete <see cref="TestClassAttribute"/> from this and implements <see cref="CreateStoreAsync"/>;
/// the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class DraftRunStoreConformance
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IDraftRunStore> CreateStoreAsync();

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
    public async Task Put_then_Get_round_trips_the_capture_record()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Record("run-1", workingCopyId: "wc-1", contentHash: "hash-1"), Package("""{"arazzo":"1.0.1"}"""), default);

        using ParsedJsonDocument<DraftRun>? record = await store.GetAsync("run-1", default);
        record.ShouldNotBeNull();
        DraftRun draft = record.RootElement;
        draft.RunIdValue.ShouldBe("run-1");
        draft.WorkingCopyIdValue.ShouldBe("wc-1");
        ((string)draft.WorkflowId).ShouldBe("checkout");
        ((string)draft.DocumentEtag).ShouldBe("etag-7");
        ((string)draft.Environment).ShouldBe("development");
        ((string)draft.StartedBy).ShouldBe("alice");
        draft.ContentHashValue.ShouldBe("hash-1");
    }

    [TestMethod]
    public async Task Put_then_GetPackage_round_trips_the_package_bytes()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        byte[] package = Package("""{"arazzo":"1.0.1","info":{}}""");
        await store.PutAsync("run-1", Record("run-1"), package, default);

        ReadOnlyMemory<byte>? loaded = await store.GetPackageAsync("run-1", default);
        loaded.ShouldNotBeNull();
        loaded.Value.ToArray().ShouldBe(package);
    }

    [TestMethod]
    public async Task Get_of_unknown_run_returns_null()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        (await store.GetAsync("missing", default)).ShouldBeNull();
        (await store.GetPackageAsync("missing", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Put_with_same_id_replaces_the_capture()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Record("run-1", contentHash: "hash-1"), Package("""{"v":1}"""), default);
        await store.PutAsync("run-1", Record("run-1", contentHash: "hash-2"), Package("""{"v":2}"""), default);

        using ParsedJsonDocument<DraftRun>? record = await store.GetAsync("run-1", default);
        record!.RootElement.ContentHashValue.ShouldBe("hash-2");
        (await store.GetPackageAsync("run-1", default))!.Value.ToArray().ShouldBe(Package("""{"v":2}"""));
    }

    [TestMethod]
    public async Task Captures_are_kept_per_run()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Record("run-1", workingCopyId: "wc-1"), Package("""{"v":1}"""), default);
        await store.PutAsync("run-2", Record("run-2", workingCopyId: "wc-2"), Package("""{"v":2}"""), default);

        using ParsedJsonDocument<DraftRun>? first = await store.GetAsync("run-1", default);
        first!.RootElement.WorkingCopyIdValue.ShouldBe("wc-1");
        using ParsedJsonDocument<DraftRun>? second = await store.GetAsync("run-2", default);
        second!.RootElement.WorkingCopyIdValue.ShouldBe("wc-2");
    }

    [TestMethod]
    public async Task Delete_removes_the_capture_and_reports_whether_it_existed()
    {
        IDraftRunStore store = await this.NewStoreAsync();
        await store.PutAsync("run-1", Record("run-1"), Package("""{"v":1}"""), default);

        (await store.DeleteAsync("run-1", default)).ShouldBeTrue();
        (await store.GetAsync("run-1", default)).ShouldBeNull();
        (await store.GetPackageAsync("run-1", default)).ShouldBeNull();
        (await store.DeleteAsync("run-1", default)).ShouldBeFalse();
        (await store.DeleteAsync("never-existed", default)).ShouldBeFalse();
    }

    // A real WorkflowPackage container (the store treats it as an opaque blob, but conformance uses the honest shape).
    private static byte[] Package(string workflowJson)
        => WorkflowPackage.Pack(Encoding.UTF8.GetBytes(workflowJson), [new("petstore", Encoding.UTF8.GetBytes("""{"openapi":"3.1.0"}"""))]);

    private static DraftRun Record(string runId, string workingCopyId = "wc-1", string contentHash = "hash-1")
    {
        // A detached value: parse the composed document rather than holding a builder workspace open across the test.
        byte[] utf8 = PersistedJson.ToArray(
            (RunId: runId, WorkingCopyId: workingCopyId, ContentHash: contentHash),
            static (Utf8JsonWriter writer, in (string RunId, string WorkingCopyId, string ContentHash) r) =>
            {
                writer.WriteStartObject();
                writer.WriteString("runId"u8, r.RunId);
                writer.WriteString("workingCopyId"u8, r.WorkingCopyId);
                writer.WriteString("workflowId"u8, "checkout");
                writer.WriteString("documentEtag"u8, "etag-7");
                writer.WriteString("environment"u8, "development");
                writer.WriteString("startedBy"u8, "alice");
                writer.WriteString("startedAt"u8, T0);
                writer.WriteString("contentHash"u8, r.ContentHash);
                writer.WriteEndObject();
            });
        return DraftRun.FromJson(utf8);
    }

    private async ValueTask<IDraftRunStore> NewStoreAsync()
    {
        IDraftRunStore store = await this.CreateStoreAsync();
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}