// <copyright file="SqlServerAvailabilityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityStoreConformance"/> suite against <see cref="SqlServerAvailabilityStore"/> over a
/// real SQL Server in a container. Each test gets an empty store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerAvailabilityStoreConformanceTests : AvailabilityStoreConformance
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
    protected override async ValueTask<IAvailabilityStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS Availability;";
            await reset.ExecuteNonQueryAsync();
        }

        await SqlServerAvailabilityStore.PrepareAsync(connectionString);
        return await SqlServerAvailabilityStore.ConnectAsync(connectionString, timeProvider);
    }
}