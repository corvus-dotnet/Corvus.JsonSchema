// <copyright file="MySqlAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using global::MySqlConnector;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MySql;

namespace Corvus.Text.Json.Arazzo.Durability.MySql.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against <see cref="MySqlAvailabilityRequestStore"/>
/// over a real MySQL server in a container. Each test gets an empty store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class MySqlAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
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

    /// <inheritdoc/>
    protected override async ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        await using (MySqlConnection connection = await dataSource.OpenConnectionAsync())
        {
            await using MySqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS AvailabilityRequests;";
            await reset.ExecuteNonQueryAsync();
        }

        await MySqlAvailabilityRequestStore.PrepareAsync(dataSource);
        return await MySqlAvailabilityRequestStore.ConnectAsync(dataSource, timeProvider);
    }
}