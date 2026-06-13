// <copyright file="SqliteWorkflowStateStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// SQLite-specific coverage beyond the shared conformance suite: durability across an on-disk file
/// (re-opening a database preserves checkpoints and the wait index), proving the schema is created
/// idempotently and the projection columns persist.
/// </summary>
[TestClass]
public sealed class SqliteWorkflowStateStoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private string dbPath = null!;
    private string connectionString = null!;

    [TestInitialize]
    public void Init()
    {
        this.dbPath = Path.Combine(Path.GetTempPath(), $"arazzo-sqlite-{Guid.NewGuid():N}.db");
        this.connectionString = $"Data Source={this.dbPath}";
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        foreach (string path in new[] { this.dbPath, this.dbPath + "-wal", this.dbPath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [TestMethod]
    public async Task Reopening_a_database_file_preserves_a_checkpoint()
    {
        WorkflowEtag etag;
        await using (SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            etag = await store.SaveAsync("p1", Encoding.UTF8.GetBytes("""{"v":42}"""), Index(), WorkflowEtag.None, default);
        }

        // Re-open: ConnectAsync re-runs the idempotent schema; the checkpoint and its etag survive.
        await using (SqliteWorkflowStateStore reopened = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            WorkflowCheckpoint? loaded = await reopened.LoadAsync("p1", default);
            loaded.ShouldNotBeNull();
            Encoding.UTF8.GetString(loaded.Value.Utf8.Span).ShouldBe("""{"v":42}""");
            loaded.Value.Etag.ShouldBe(etag);
        }
    }

    [TestMethod]
    public async Task Reopening_a_database_file_preserves_the_wait_index()
    {
        await using (SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            await store.SaveAsync(
                "timer",
                Encoding.UTF8.GetBytes("{}"),
                new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Suspended, T0, T0, DueAt: T0 + TimeSpan.FromMinutes(1)),
                WorkflowEtag.None,
                default);
        }

        await using (SqliteWorkflowStateStore reopened = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            var due = new List<WorkflowRunId>();
            await foreach (WorkflowRunId id in reopened.QueryDueAsync(T0 + TimeSpan.FromMinutes(5), default))
            {
                due.Add(id);
            }

            due.ShouldHaveSingleItem().ShouldBe(new WorkflowRunId("timer"));
        }
    }

    private static WorkflowRunIndexEntry Index() => new("wf", WorkflowRunStatus.Running, T0, T0);

    [TestMethod]
    public async Task The_in_query_reach_filter_matches_the_evaluator_across_rule_shapes()
    {
        await using SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString);

        // Seed runs spanning the interesting tag shapes: single-value, multi-value, and no tags.
        (string Id, SecurityTag[] Tags)[] rows =
        [
            ("run-a", [new("tenant", "acme"), new("team", "payments")]),
            ("run-b", [new("tenant", "acme"), new("team", "hr")]),
            ("run-c", [new("tenant", "globex"), new("team", "payments")]),
            ("run-d", []),
            ("run-e", [new("tenant", "acme"), new("tenant", "beta")]),
        ];

        byte[] checkpoint = Encoding.UTF8.GetBytes("{}");
        foreach ((string id, SecurityTag[] tags) in rows)
        {
            await store.SaveAsync(id, checkpoint, new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Running, T0, T0, SecurityTags: tags), WorkflowEtag.None, default);
        }

        var claims = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["tenant"] = ["acme"],
            ["both"] = ["acme", "globex"],
        };

        // Every operator/operand shape the translator must handle; each is cross-checked against the evaluator.
        string[] ruleShapes =
        [
            "tenant == $claim.tenant",            // tag-key == claim
            "tenant == 'acme'",                   // tag-key == literal
            "tenant != $claim.tenant",            // negated comparison
            "tenant",                             // truthy tag-key
            "tenant && team == 'payments'",       // conjunction
            "tenant == 'acme' || team == 'hr'",   // disjunction
            "!(tenant == 'globex')",              // grouped negation
            "tenant == $claim.missing",           // claim resolves to empty -> never matches
            "'a' == 'a'",                         // both constants -> always matches
            "team == team",                       // both tag-keys -> shared value
            "tenant == $claim.both",              // claim with multiple values
        ];

        foreach (string ruleText in ruleShapes)
        {
            var filter = new SecurityFilter([SecurityRule.Compile(ruleText)], claims);

            WorkflowRunPage page = await store.QueryAsync(new WorkflowQuery(Status: null, Limit: 1000, Security: filter), default);
            List<string> actual = [.. page.Runs.Select(r => r.Id.Value).OrderBy(x => x, StringComparer.Ordinal)];
            List<string> expected = [.. rows.Where(r => filter.IsSatisfiedBy(r.Tags)).Select(r => r.Id).OrderBy(x => x, StringComparer.Ordinal)];

            actual.ShouldBe(expected, $"rule: {ruleText}");
        }
    }

    [TestMethod]
    public async Task Deleting_a_run_removes_its_security_tags()
    {
        await using SqliteWorkflowStateStore store = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString);
        await store.SaveAsync("run-x", Encoding.UTF8.GetBytes("{}"), new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Completed, T0, T0, SecurityTags: [new("tenant", "acme")]), WorkflowEtag.None, default);

        var filter = new SecurityFilter([SecurityRule.Compile("tenant == 'acme'")], new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        (await store.QueryAsync(new WorkflowQuery(Status: null, Limit: 10, Security: filter), default)).Runs.Count.ShouldBe(1);

        await store.DeleteAsync("run-x", default);

        // The run is gone and so are its security tags (a re-created run with the same id must not inherit them).
        (await store.QueryAsync(new WorkflowQuery(Status: null, Limit: 10, Security: filter), default)).Runs.Count.ShouldBe(0);
        await store.SaveAsync("run-x", Encoding.UTF8.GetBytes("{}"), new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Completed, T0, T0), WorkflowEtag.None, default);
        (await store.QueryAsync(new WorkflowQuery(Status: null, Limit: 10, Security: filter), default)).Runs.Count.ShouldBe(0);
    }
}