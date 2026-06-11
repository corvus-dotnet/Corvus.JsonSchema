// <copyright file="SqlServerCatalogStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared catalog-conformance suite against <see cref="SqlServerWorkflowCatalogStore"/> over a real
/// SQL Server in a container. Each test gets an empty store (the table is dropped and recreated).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerCatalogStoreConformanceTests : WorkflowCatalogStoreConformance
{
    private static MsSqlContainer container = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MsSqlBuilder().Build();
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

    protected override async ValueTask<IWorkflowCatalogStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS CatalogVersions;";
            await reset.ExecuteNonQueryAsync();
        }

        // Provision (DDL) with the test's admin credential, then open for operation with no DDL.
        await SqlServerWorkflowCatalogStore.PrepareAsync(connectionString);
        return await SqlServerWorkflowCatalogStore.ConnectAsync(connectionString, timeProvider);
    }
}