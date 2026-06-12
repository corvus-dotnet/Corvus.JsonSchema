// <copyright file="MySqlRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MySqlConnector;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.MySql.Tests;

/// <summary>
/// Runs the shared registry-conformance suite against <see cref="MySqlRunnerRegistry"/> over a real MySQL
/// server in a container, exercising the data-source (caller-owned client) overloads. Each test gets an empty
/// registry (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MySqlRunnerRegistryConformanceTests : RunnerRegistryConformance
{
    private static MySqlContainer container = null!;
    private static MySqlDataSource dataSource = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new MySqlBuilder().WithImage("mysql:8.4").Build();
        await container.StartAsync();
        dataSource = new MySqlDataSource(container.GetConnectionString());
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

    protected override async ValueTask<IRunnerRegistry> CreateRegistryAsync()
    {
        await using (MySqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using MySqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS runner_registrations;";
            await reset.ExecuteNonQueryAsync();
        }

        // Provision (DDL) then open for operation (no DDL) over the caller-owned data source.
        await MySqlRunnerRegistry.PrepareAsync(dataSource);
        return await MySqlRunnerRegistry.ConnectAsync(dataSource);
    }
}