// <copyright file="SqlServerRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared registry-conformance suite against <see cref="SqlServerRunnerRegistry"/> over a real
/// SQL Server in a container. Each test gets an empty registry (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerRunnerRegistryConformanceTests : RunnerRegistryConformance
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

    protected override async ValueTask<IRunnerRegistry> CreateRegistryAsync()
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS runner_hosted_versions; DROP TABLE IF EXISTS runner_registrations;";
            await reset.ExecuteNonQueryAsync();
        }

        // Provision (DDL) with the test's admin credential, then open for operation with no DDL.
        await SqlServerRunnerRegistry.PrepareAsync(connectionString);
        return await SqlServerRunnerRegistry.ConnectAsync(connectionString);
    }
}