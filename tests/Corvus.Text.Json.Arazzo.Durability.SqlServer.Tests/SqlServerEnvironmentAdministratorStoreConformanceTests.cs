// <copyright file="SqlServerEnvironmentAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentAdministratorStoreConformance"/> suite against
/// <see cref="SqlServerEnvironmentAdministratorStore"/> over a real SQL Server in a container. Each test gets an empty
/// store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerEnvironmentAdministratorStoreConformanceTests : EnvironmentAdministratorStoreConformance
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

    protected override async ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS EnvironmentAdministrators; DROP TABLE IF EXISTS EnvironmentAdministratorIndex;";
            await reset.ExecuteNonQueryAsync();
        }

        await SqlServerEnvironmentAdministratorStore.PrepareAsync(connectionString);
        return await SqlServerEnvironmentAdministratorStore.ConnectAsync(connectionString, timeProvider);
    }
}