// <copyright file="MySqlResumeClaimTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MySqlConnector;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.MySql.Tests;

/// <summary>
/// Runs the §18 resume-claimable dispatch contract (<see cref="ResumeClaimConformance"/>) against
/// <see cref="MySqlWorkflowStateStore"/> over a real MySQL server in a container. Each test gets an empty store
/// (the tables are dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MySqlResumeClaimTests
{
    private static MySqlContainer container = null!;
    private static MySqlDataSource dataSource = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MySqlBuilder().WithImage("mysql:8.4").Build();
        await container.StartAsync();

        // The store requires "changed rows" semantics for its rows-affected CAS/lease detection.
        string connectionString = new MySqlConnectionStringBuilder(container.GetConnectionString()) { UseAffectedRows = true }.ConnectionString;
        dataSource = new MySqlDataSource(connectionString);
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

    [TestMethod]
    public async Task Surfaces_only_the_resume_requested_runs()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Surfaces_only_the_resume_requested_runs(store);
    }

    [TestMethod]
    public async Task Respects_hosted_and_environment_filters()
    {
        IWorkflowStateStore store = await CreateStoreAsync();
        await ResumeClaimConformance.Respects_hosted_and_environment_filters(store);
    }

    private static async ValueTask<IWorkflowStateStore> CreateStoreAsync()
    {
        await using (MySqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            foreach (string table in new[] { "workflow_runs", "workflow_leases" })
            {
                await using MySqlCommand reset = connection.CreateCommand();
                reset.CommandText = $"DROP TABLE IF EXISTS {table};";
                await reset.ExecuteNonQueryAsync();
            }
        }

        // Provision (DDL) then open for operation (no DDL) over the caller-owned data source.
        await MySqlWorkflowStateStore.PrepareAsync(dataSource);
        return await MySqlWorkflowStateStore.ConnectAsync(dataSource);
    }
}