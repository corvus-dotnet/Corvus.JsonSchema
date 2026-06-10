// <copyright file="PostgresStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres.Tests;

/// <summary>
/// Runs the shared store-conformance suite against <see cref="PostgresWorkflowStateStore"/> over a real
/// PostgreSQL server in a container, exercising the data-source (caller-owned client) overloads. Each test
/// gets an empty store (the tables are dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class PostgresStoreConformanceTests : WorkflowStateStoreConformance
{
    private static PostgreSqlContainer container = null!;
    private static NpgsqlDataSource dataSource = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (dataSource is not null)
        {
            await dataSource.DisposeAsync();
        }

        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override async ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        // Per-test isolation: drop the tables so each test starts from an empty store.
        await using (NpgsqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using NpgsqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS workflow_runs; DROP TABLE IF EXISTS workflow_leases;";
            await reset.ExecuteNonQueryAsync();
        }

        // Provision (DDL) then open for operation (no DDL) over the caller-owned data source.
        await PostgresWorkflowStateStore.PrepareAsync(dataSource);
        return await PostgresWorkflowStateStore.ConnectAsync(dataSource, timeProvider);
    }
}
