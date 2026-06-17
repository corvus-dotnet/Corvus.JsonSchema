// <copyright file="SqlServerAccessRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared <see cref="AccessRequestStoreConformance"/> suite against <see cref="SqlServerAccessRequestStore"/>
/// over a real SQL Server in a container. Each test gets an empty store (the table is dropped and recreated).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerAccessRequestStoreConformanceTests : AccessRequestStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IAccessRequestStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS AccessRequests;";
            await reset.ExecuteNonQueryAsync();
        }

        await SqlServerAccessRequestStore.PrepareAsync(connectionString);
        return await SqlServerAccessRequestStore.ConnectAsync(connectionString, timeProvider);
    }
}