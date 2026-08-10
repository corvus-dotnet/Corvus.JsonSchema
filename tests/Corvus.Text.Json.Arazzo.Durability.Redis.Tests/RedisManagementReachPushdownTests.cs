// <copyright file="RedisManagementReachPushdownTests.cs" company="Endjin Limited">
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
using Shouldly;
using StackExchange.Redis;
using StackExchange.Redis.Profiling;
using Testcontainers.Redis;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Redis.Tests;

/// <summary>
/// Proves the four Redis management stores (environment, source, source-credential, workspace-workflow) narrow a
/// reach-filtered list/count through their §14.4 security-label sets — resolved server-side in one Lua evaluation —
/// instead of sweeping the all index and discarding rows in process, and that a re-tagging update re-points the
/// label entries in the same write. The management sibling of <see cref="RedisCatalogReachPushdownTests"/>.
/// </summary>
/// <remarks>
/// The command mix discriminates: GET counts how many documents were read, SMEMBERS marks a sweep of the all index,
/// and EVAL/EVALSHA marks the label resolution.
/// </remarks>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class RedisManagementReachPushdownTests
{
    private static readonly List<ConnectionMultiplexer> Connections = [];
    private static RedisContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new RedisBuilder().WithImage("redis:7-alpine").Build();
        await container.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        foreach (ConnectionMultiplexer connection in Connections)
        {
            await connection.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_list_resolves_the_label_sets_and_reads_only_those_rows()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisEnvironmentStore store = RedisEnvironmentStore.Connect(connection);
        await SeedEnvironmentsAsync(store);

        log.Start();
        using (EnvironmentPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Environments.Select(e => e.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> commands = log.Finish();

        // The label sets were consulted server-side: the reach became one script evaluation...
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");

        // ...the all index was never swept...
        commands.ShouldNotContain("SMEMBERS");

        // ...and only candidate docs were read. A sweep would have read the acme docs on the way, since acme-*
        // leads globex-* in the keyset order.
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task An_unreachable_environment_list_reads_no_rows_at_all()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisEnvironmentStore store = RedisEnvironmentStore.Connect(connection);
        await SeedEnvironmentsAsync(store);

        log.Start();
        using (EnvironmentPage page = await store.ListAsync(Scope("nobody"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        List<string> commands = log.Finish();

        // An empty candidate set is not "no narrowing": the store must answer without reading a single doc.
        commands.ShouldNotContain("GET");
        commands.ShouldNotContain("SMEMBERS");
    }

    [TestMethod]
    public async Task An_unrestricted_environment_list_still_sweeps_rather_than_enumerating_label_sets()
    {
        // The negative control for the first test: narrowing must be driven by the reach, not applied always.
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisEnvironmentStore store = RedisEnvironmentStore.Connect(connection);
        await SeedEnvironmentsAsync(store);

        log.Start();
        using (EnvironmentPage page = await store.ListAsync(AccessContext.System, 10, default, default))
        {
            page.Environments.Count.ShouldBe(4);
        }

        List<string> commands = log.Finish();
        commands.ShouldContain("SMEMBERS");
        commands.ShouldNotContain(c => c == "EVAL" || c == "EVALSHA");
    }

    [TestMethod]
    public async Task A_re_tagging_environment_update_re_points_the_label_entries()
    {
        // A §14.2 re-tag replaces the row's management tags in place, so the label diff must be maintained — the new
        // tenant's entry added (or the row is hidden from its rightful reach, an availability failure) and the old
        // tenant's removed. The stale old entry would be discarded by the exact evaluation, so the wire is what the
        // test observes: the old scope's narrowed list must answer empty without reading a doc.
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisEnvironmentStore store = RedisEnvironmentStore.Connect(connection);

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

        log.Start();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        log.Finish().ShouldNotContain("GET");
    }

    [TestMethod]
    public async Task Deleting_an_environment_removes_its_label_entries()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisEnvironmentStore store = RedisEnvironmentStore.Connect(connection);

        using (ParsedJsonDocument<Environment> draft = Environment.Draft("ephemeral", null, null, Tenant("acme")))
        {
            (await store.AddAsync(draft.RootElement, "system", default)).Dispose();
        }

        (await store.DeleteAsync("ephemeral", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();

        // A stale entry would produce the same empty page while still costing the read, so the wire is what the
        // test observes.
        log.Start();
        using (EnvironmentPage page = await store.ListAsync(Scope("acme"), 5, default, default))
        {
            page.Environments.ShouldBeEmpty();
        }

        log.Finish().ShouldNotContain("GET");
    }

    [TestMethod]
    public async Task A_reach_filtered_environment_count_narrows_before_reading()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        IEnvironmentStore store = RedisEnvironmentStore.Connect(connection);
        await SeedEnvironmentsAsync(store);

        log.Start();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_list_resolves_the_label_sets_and_reads_only_those_rows()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisSourceStore store = RedisSourceStore.Connect(connection);
        await SeedSourcesAsync(store);

        log.Start();
        using (SourcePage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Sources.Select(s => s.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_source_count_narrows_before_reading()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        ISourceStore store = RedisSourceStore.Connect(connection);
        await SeedSourcesAsync(store);

        log.Start();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_list_resolves_the_label_sets_and_reads_only_those_rows()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisSourceCredentialStore store = RedisSourceCredentialStore.Connect(connection);
        await SeedCredentialsAsync(store);

        log.Start();
        using (SourceCredentialPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_credential_count_narrows_before_reading()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        ISourceCredentialStore store = RedisSourceCredentialStore.Connect(connection);
        await SeedCredentialsAsync(store);

        log.Start();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_list_resolves_the_label_sets_and_reads_only_those_rows()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        RedisWorkspaceWorkflowStore store = RedisWorkspaceWorkflowStore.Connect(connection);
        await SeedWorkingCopiesAsync(store);

        log.Start();
        using (WorkspaceWorkflowPage page = await store.ListAsync(Scope("globex"), 5, default, default))
        {
            page.WorkingCopies.Select(w => w.ManagementTagsValue.ToList().Single().Value).ShouldBe(["globex", "globex"]);
        }

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    [TestMethod]
    public async Task A_reach_filtered_working_copy_count_narrows_before_reading()
    {
        (IConnectionMultiplexer connection, CommandLog log) = await NewConnectionAsync();
        IWorkspaceWorkflowStore store = RedisWorkspaceWorkflowStore.Connect(connection);
        await SeedWorkingCopiesAsync(store);

        log.Start();
        (await store.CountAsync(Scope("globex"), 100, default)).ShouldBe((2, false));

        List<string> commands = log.Finish();
        commands.ShouldContain(c => c == "EVAL" || c == "EVALSHA");
        commands.ShouldNotContain("SMEMBERS");
        commands.Count(c => c == "GET").ShouldBeLessThanOrEqualTo(2);
    }

    // Two rows per tenant, with the acme names leading the keyset order so a globex-scoped page cannot satisfy its
    // GET bound by accident of ordering.
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

    private static async ValueTask<(IConnectionMultiplexer Connection, CommandLog Log)> NewConnectionAsync()
    {
        string configuration = container.GetConnectionString();

        await using (var admin = await ConnectionMultiplexer.ConnectAsync($"{configuration},allowAdmin=true"))
        {
            await admin.GetServer(admin.GetEndPoints()[0]).FlushDatabaseAsync();
        }

        // The store is opened over a caller-owned connection so the test can register the profiler on it; the
        // connections are disposed with the class.
        var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        Connections.Add(connection);
        var log = new CommandLog();
        connection.RegisterProfiler(log.CurrentSession);
        return (connection, log);
    }

    // Records the command name of every call issued while a session is active, which is where a query's shape
    // travels on this backend.
    private sealed class CommandLog
    {
        private ProfilingSession? session;

        public ProfilingSession? CurrentSession() => this.session;

        public void Start() => this.session = new ProfilingSession();

        public List<string> Finish()
        {
            List<string> commands = [.. this.session!.FinishProfiling().Select(c => c.Command)];
            this.session = null;
            return commands;
        }
    }
}
