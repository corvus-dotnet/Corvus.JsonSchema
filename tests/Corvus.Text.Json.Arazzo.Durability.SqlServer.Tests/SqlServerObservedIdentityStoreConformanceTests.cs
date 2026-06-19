// <copyright file="SqlServerObservedIdentityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared <see cref="ObservedIdentityStoreConformance"/> suite against <see cref="SqlServerObservedIdentityStore"/>
/// over a real SQL Server in a container. Each test gets an empty store (the tables are dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerObservedIdentityStoreConformanceTests : ObservedIdentityStoreConformance
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

    protected override async ValueTask<IObservedIdentityStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS ObservedIdentitySecurityTags; DROP TABLE IF EXISTS ObservedIdentities;";
            await reset.ExecuteNonQueryAsync();
        }

        await SqlServerObservedIdentityStore.PrepareAsync(connectionString);
        return await SqlServerObservedIdentityStore.ConnectAsync(connectionString, timeProvider);
    }
}