// <copyright file="MySqlScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MySqlConnector;
using Testcontainers.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.MySql.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="MySqlScheduleRegistry"/> over a
/// real MySQL server in a container. Each test gets an empty registry (the table is dropped and
/// re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MySqlScheduleRegistryConformanceTests : ScheduleRegistryConformance
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

    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
    {
        await using (MySqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using MySqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS schedule_registrations;";
            await reset.ExecuteNonQueryAsync();
        }

        await MySqlScheduleRegistry.PrepareAsync(dataSource);
        return await MySqlScheduleRegistry.ConnectAsync(dataSource);
    }
}