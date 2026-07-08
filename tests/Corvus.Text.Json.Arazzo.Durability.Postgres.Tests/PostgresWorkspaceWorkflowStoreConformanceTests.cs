// <copyright file="PostgresWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres.Tests;

/// <summary>
/// Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against
/// <see cref="PostgresWorkspaceWorkflowStore"/> over a real PostgreSQL server in a container. Each test gets an empty
/// store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class PostgresWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await using (NpgsqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using NpgsqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS WorkspaceWorkflows;";
            await reset.ExecuteNonQueryAsync();
        }

        await PostgresWorkspaceWorkflowStore.PrepareAsync(dataSource);
        return await PostgresWorkspaceWorkflowStore.ConnectAsync(dataSource, timeProvider);
    }
}