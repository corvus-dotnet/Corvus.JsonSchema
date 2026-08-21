// <copyright file="SqliteWorkflowStateStoreTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
            etag = await store.SaveAsync(A("p1"), Encoding.UTF8.GetBytes("""{"v":42}"""), Index(), WorkflowEtag.None, default);
        }

        // Re-open: ConnectAsync re-runs the idempotent schema; the checkpoint and its etag survive.
        await using (SqliteWorkflowStateStore reopened = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            WorkflowCheckpoint? loaded = await reopened.LoadAsync(A("p1"), default);
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
                A("timer"),
                Encoding.UTF8.GetBytes("{}"),
                new WorkflowRunIndexEntry("wf", WorkflowRunStatus.Suspended, T0, T0, DueAt: T0 + TimeSpan.FromMinutes(1)),
                WorkflowEtag.None,
                default);
        }

        await using (SqliteWorkflowStateStore reopened = await SqliteWorkflowStateStore.ConnectAsync(this.connectionString))
        {
            var due = new List<WorkflowRunAddress>();
            await foreach (WorkflowRunAddress address in reopened.QueryDueAsync(T0 + TimeSpan.FromMinutes(5), default))
            {
                due.Add(address);
            }

            due.ShouldHaveSingleItem().ShouldBe(A("timer"));
        }
    }

    private static WorkflowRunIndexEntry Index() => new("wf", WorkflowRunStatus.Running, T0, T0);

    // The run's full (environment, runId) address (ADR 0065 decision 9).
    private static WorkflowRunAddress A(string runId) => new("development", new WorkflowRunId(runId));
}