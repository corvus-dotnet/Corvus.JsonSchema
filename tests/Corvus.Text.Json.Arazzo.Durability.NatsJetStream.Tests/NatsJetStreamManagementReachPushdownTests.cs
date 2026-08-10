// <copyright file="NatsJetStreamManagementReachPushdownTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;
using Shouldly;
using Testcontainers.Nats;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.NatsJetStream.Tests;

/// <summary>
/// Proves the four NATS management stores (environment, source, source-credential, workspace-workflow) narrow a
/// reach-filtered list/count through their §14.4 label buckets — the reach resolving to candidate keys by
/// subject-filtered key listings — instead of scanning every key and discarding documents in process, and that a
/// re-tagging update re-points the label entries around the write. The management sibling of
/// <see cref="NatsJetStreamCatalogReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The subject mix discriminates, observed from a second connection on <c>$JS.API.&gt;</c>: a per-row document
/// read addresses the store bucket's stream, a key scan creates a consumer on it, and the label resolution
/// creates consumers on the labels bucket's stream instead.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class NatsJetStreamManagementReachPushdownTests
{
    private static NatsContainer container = null!;
    private static NatsConnection connection = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new NatsBuilder().WithImage("nats:2.11").WithCommand("-js").Build();
        await container.StartAsync();
        connection = new NatsConnection(NatsOpts.Default with { Url = container.GetConnectionString() });
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_list_resolves_the_label_bucket_and_reads_only_those_rows()
    {
        IEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());

        // The reachable rows (globex-*) do NOT lead in keyset order — acme-* sorts first — so a store that merely
        // swept and took the first matching page would read the acme docs on the way and the read bound below
        // could not hold by accident.
        using (EnvironmentPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> subjects = await log.FinishAsync();

        // The label bucket was consulted: the reach became subject-filtered key listings there...
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_environment_labels"));

        // ...the store's keys were never scanned...
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_environments"));

        // ...and only candidate docs were read.
        subjects.Count(s => IsRead(s, "arazzo_environments")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task An_unreachable_environment_list_reads_no_rows_at_all()
    {
        IEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (EnvironmentPage page = await store.ListAsync(Scope("nobody"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        List<string> subjects = await log.FinishAsync();

        // An empty candidate set is not "no narrowing": the store must answer without touching its bucket.
        subjects.ShouldNotContain(s => IsRead(s, "arazzo_environments"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_environments"));
    }

    [TestMethod]
    public async Task An_unrestricted_environment_list_still_scans_rather_than_enumerating_labels()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        IEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Environments.Count.ShouldBe(4);
        }

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsScan(s, "arazzo_environments"));
        subjects.ShouldNotContain(s => IsLabelTraffic(s, "arazzo_environment_labels"));
    }

    [TestMethod]
    public async Task A_re_tagging_environment_update_re_points_the_label_entries()
    {
        // A §14.2 re-tag replaces the row's management tags in place, so the label diff must be maintained — the
        // new tenant's entry added (or the row is hidden from its rightful reach, an availability failure) and the
        // old tenant's removed. The stale old entry would be discarded by the exact evaluation, so the wire is
        // what the test observes: the old scope's narrowed list must answer empty without reading a doc.
        IEnvironmentStore store = await NewEnvironmentStoreAsync();

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("production", null, null, Tenant("acme")))
        {
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }

        using (ParsedJsonDocument<Environment> reTag = Environment.Draft("production", null, null, Tenant("globex")))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", reTag.RootElement, WorkflowEtag.None, "carol", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
        }

        using (EnvironmentPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Environments.Count.ShouldBe(1);
        }

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        (await log.FinishAsync()).ShouldNotContain(s => IsRead(s, "arazzo_environments"));
    }

    [TestMethod]
    public async Task Deleting_an_environment_removes_its_label_entries()
    {
        IEnvironmentStore store = await NewEnvironmentStoreAsync();

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("ephemeral", null, null, Tenant("acme")))
        {
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }

        (await store.DeleteAsync("ephemeral", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();

        // A stale entry would produce the same empty page while still costing the read, so the wire is what the
        // test observes.
        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        (await log.FinishAsync()).ShouldNotContain(s => IsRead(s, "arazzo_environments"));
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_count_narrows_before_reading()
    {
        IEnvironmentStore store = await NewEnvironmentStoreAsync();
        await SeedEnvironmentsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_environment_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_environments"));
        subjects.Count(s => IsRead(s, "arazzo_environments")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_list_resolves_the_label_bucket_and_reads_only_those_rows()
    {
        ISourceStore store = await NewSourceStoreAsync();
        await SeedSourcesAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (SourcePage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_source_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_sources"));
        subjects.Count(s => IsRead(s, "arazzo_sources")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_count_narrows_before_reading()
    {
        ISourceStore store = await NewSourceStoreAsync();
        await SeedSourcesAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_source_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_sources"));
        subjects.Count(s => IsRead(s, "arazzo_sources")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_list_resolves_the_label_bucket_and_reads_only_those_rows()
    {
        ISourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (SourceCredentialPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_source_credential_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_source_credentials"));
        subjects.Count(s => IsRead(s, "arazzo_source_credentials")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_count_narrows_before_reading()
    {
        ISourceCredentialStore store = await NewCredentialStoreAsync();
        await SeedCredentialsAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_source_credential_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_source_credentials"));
        subjects.Count(s => IsRead(s, "arazzo_source_credentials")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_list_resolves_the_label_bucket_and_reads_only_those_rows()
    {
        IWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopiesAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        using (WorkspaceWorkflowPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.WorkingCopies.Select(w => w.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_workspace_workflow_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_workspace_workflows"));
        subjects.Count(s => IsRead(s, "arazzo_workspace_workflows")).ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_count_narrows_before_reading()
    {
        IWorkspaceWorkflowStore store = await NewWorkspaceStoreAsync();
        await SeedWorkingCopiesAsync(store);

        await using ApiLog log = await ApiLog.StartAsync(container.GetConnectionString());
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> subjects = await log.FinishAsync();
        subjects.ShouldContain(s => IsLabelTraffic(s, "arazzo_workspace_workflow_labels"));
        subjects.ShouldNotContain(s => IsScan(s, "arazzo_workspace_workflows"));
        subjects.Count(s => IsRead(s, "arazzo_workspace_workflows")).ShouldBeLessThanOrEqualTo(2);
    }

    // A store bucket's stream name can be a prefix of its labels stream's shape family, so the read/scan matchers
    // require the token boundary after the full bucket name.
    private static bool IsRead(string subject, string bucket)
        => subject == $"$JS.API.DIRECT.GET.KV_{bucket}" || subject == $"$JS.API.STREAM.MSG.GET.KV_{bucket}"
            || subject.StartsWith($"$JS.API.DIRECT.GET.KV_{bucket}.", StringComparison.Ordinal)
            || subject.StartsWith($"$JS.API.STREAM.MSG.GET.KV_{bucket}.", StringComparison.Ordinal);

    private static bool IsScan(string subject, string bucket)
        => subject.StartsWith($"$JS.API.CONSUMER.CREATE.KV_{bucket}.", StringComparison.Ordinal);

    private static bool IsLabelTraffic(string subject, string labelBucket)
        => subject.Contains($"KV_{labelBucket}", StringComparison.Ordinal);

    // Two rows per tenant, with the acme names leading the keyset order so a globex-scoped page cannot satisfy its
    // read bound by accident of ordering.
    private static async ValueTask SeedEnvironmentsAsync(IEnvironmentStore store)
    {
        foreach ((string name, string tenant) in ((string, string)[])[("acme-0", "acme"), ("acme-1", "acme"), ("globex-0", "globex"), ("globex-1", "globex")])
        {
            using ParsedJsonDocument<Environment> draft = Environment.Draft(name, null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static async ValueTask SeedSourcesAsync(ISourceStore store)
    {
        foreach ((string name, string tenant) in ((string, string)[])[("acme-0", "acme"), ("acme-1", "acme"), ("globex-0", "globex"), ("globex-1", "globex")])
        {
            using ParsedJsonDocument<RegisteredSource> draft = RegisteredSource.Draft(name, "openapi", DocUtf8(name), null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static async ValueTask SeedCredentialsAsync(ISourceCredentialStore store)
    {
        foreach ((string source, string tenant) in ((string, string)[])[("acme-api-0", "acme"), ("acme-api-1", "acme"), ("globex-api-0", "globex"), ("globex-api-1", "globex")])
        {
            var definition = new SourceCredentialDefinition(
                source,
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", $"keyvault://{source}-apikey")],
                ManagementTags: Tenant(tenant),
                UsageTags: Tenant(tenant));
            using ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(definition, "system", default);
        }
    }

    private static async ValueTask SeedWorkingCopiesAsync(IWorkspaceWorkflowStore store)
    {
        foreach (string tenant in (string[])["acme", "acme", "globex", "globex"])
        {
            using ParsedJsonDocument<WorkspaceWorkflow> draft = WorkspaceWorkflow.Draft("wc", DocUtf8(tenant), default, null, null, Tenant(tenant));
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }
    }

    private static ReadOnlyMemory<byte> DocUtf8(string marker)
        => Encoding.UTF8.GetBytes($$"""{"arazzo":"1.1.0","x-marker":"{{marker}}"}""");

    private static SecurityTagSet Tenant(string tenant) => SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]);

    // A read/write/purge reach that admits exactly the rows tagged tenant=<tenant> (tenant == $claim.tenant resolved
    // against a single-tenant claim).
    private static AccessContext Scope(string tenant) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile("tenant == $claim.tenant")], new Dictionary<string, IReadOnlyList<string>> { ["tenant"] = [tenant] }));

    private static async ValueTask<IEnvironmentStore> NewEnvironmentStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_environments", "arazzo_environment_labels"],
            () => NatsJetStreamEnvironmentStore.PrepareAsync(connection));
        return await NatsJetStreamEnvironmentStore.ConnectAsync(connection);
    }

    private static async ValueTask<ISourceStore> NewSourceStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_sources", "arazzo_source_labels"],
            () => NatsJetStreamSourceStore.PrepareAsync(connection));
        return await NatsJetStreamSourceStore.ConnectAsync(connection);
    }

    private static async ValueTask<ISourceCredentialStore> NewCredentialStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_source_credentials", "arazzo_source_credential_labels"],
            () => NatsJetStreamSourceCredentialStore.PrepareAsync(connection));
        return await NatsJetStreamSourceCredentialStore.ConnectAsync(connection);
    }

    private static async ValueTask<IWorkspaceWorkflowStore> NewWorkspaceStoreAsync()
    {
        var kv = new NatsKVContext(new NatsJSContext(connection));
        await NatsKvTestReset.ResetAndProvisionAsync(
            kv,
            ["arazzo_workspace_workflows", "arazzo_workspace_workflow_labels"],
            () => NatsJetStreamWorkspaceWorkflowStore.PrepareAsync(connection));
        return await NatsJetStreamWorkspaceWorkflowStore.ConnectAsync(connection);
    }

    // Observes the store's JetStream API traffic from a second connection: every JetStream operation is a NATS
    // request on a $JS.API.> subject, so the subjects seen here are the store's actual wire behaviour. Starting
    // the log after seeding scopes it to the query under test; FinishAsync drains briefly so absence assertions
    // are not satisfied by racing the subscription.
    private sealed class ApiLog : IAsyncDisposable
    {
        private readonly NatsConnection monitor;
        private readonly CancellationTokenSource cts = new();
        private readonly List<string> subjects = [];
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Task? pump;

        private ApiLog(NatsConnection monitor) => this.monitor = monitor;

        public static async ValueTask<ApiLog> StartAsync(string url)
        {
            var log = new ApiLog(new NatsConnection(NatsOpts.Default with { Url = url }));
            log.pump = Task.Run(log.PumpAsync);
            await log.ready.Task;
            return log;
        }

        public async ValueTask<List<string>> FinishAsync()
        {
            // Cross-connection observation is asynchronous; a short drain keeps "nothing was read" assertions
            // from passing because the messages had not arrived yet.
            await Task.Delay(TimeSpan.FromMilliseconds(300));
            lock (this.subjects)
            {
                return [.. this.subjects];
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.cts.CancelAsync();
            if (this.pump is { } task)
            {
                try
                {
                    await task;
                }
                catch (OperationCanceledException)
                {
                }
            }

            await this.monitor.DisposeAsync();
            this.cts.Dispose();
        }

        private async Task PumpAsync()
        {
            await using INatsSub<byte[]> sub = await this.monitor.SubscribeCoreAsync<byte[]>("$JS.API.>", cancellationToken: this.cts.Token);
            this.ready.SetResult();
            await foreach (NatsMsg<byte[]> msg in sub.Msgs.ReadAllAsync(this.cts.Token))
            {
                lock (this.subjects)
                {
                    this.subjects.Add(msg.Subject);
                }
            }
        }
    }
}
