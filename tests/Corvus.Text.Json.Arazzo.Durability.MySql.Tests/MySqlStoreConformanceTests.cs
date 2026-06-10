// <copyright file="MySqlStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MySqlConnector;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.MySql.Tests;

/// <summary>
/// Runs the shared store-conformance suite against <see cref="MySqlWorkflowStateStore"/> over a real MySQL
/// server in a container. Each test gets an empty store (the tables are dropped and recreated).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MySqlStoreConformanceTests : WorkflowStateStoreConformance
{
    private static MySqlContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MySqlBuilder().WithImage("mysql:8.4").Build();
        await container.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override async ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new MySqlConnection(connectionString))
        {
            await connection.OpenAsync();
            foreach (string table in new[] { "workflow_runs", "workflow_leases" })
            {
                await using MySqlCommand reset = connection.CreateCommand();
                reset.CommandText = $"DROP TABLE IF EXISTS {table};";
                await reset.ExecuteNonQueryAsync();
            }
        }

        return await MySqlWorkflowStateStore.CreateAsync(connectionString, timeProvider);
    }
}