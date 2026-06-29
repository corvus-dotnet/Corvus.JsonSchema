// <copyright file="PostgresEnvironmentAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentAdministratorStoreConformance"/> suite against
/// <see cref="PostgresEnvironmentAdministratorStore"/> over a real PostgreSQL server in a container. Each test gets an empty
/// store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class PostgresEnvironmentAdministratorStoreConformanceTests : EnvironmentAdministratorStoreConformance
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

    protected override async ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await using (NpgsqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using NpgsqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS EnvironmentAdministrators; DROP TABLE IF EXISTS EnvironmentAdministratorIndex;";
            await reset.ExecuteNonQueryAsync();
        }

        await PostgresEnvironmentAdministratorStore.PrepareAsync(dataSource);
        return await PostgresEnvironmentAdministratorStore.ConnectAsync(dataSource, timeProvider);
    }
}