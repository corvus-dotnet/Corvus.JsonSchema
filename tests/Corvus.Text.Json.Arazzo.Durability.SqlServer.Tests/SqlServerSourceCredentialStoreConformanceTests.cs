// <copyright file="SqlServerSourceCredentialStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.MsSql;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer.Tests;

/// <summary>
/// Runs the shared <see cref="SourceCredentialStoreConformance"/> suite against <see cref="SqlServerSourceCredentialStore"/>
/// over a real SQL Server in a container. Each test gets an empty store (the table is dropped and re-provisioned).
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class SqlServerSourceCredentialStoreConformanceTests : SourceCredentialStoreConformance
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

    protected override async ValueTask<ISourceCredentialStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        string connectionString = container.GetConnectionString();

        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using SqlCommand reset = connection.CreateCommand();
            reset.CommandText = "DROP TABLE IF EXISTS SourceCredentials;";
            await reset.ExecuteNonQueryAsync();
        }

        await SqlServerSourceCredentialStore.PrepareAsync(connectionString);
        return await SqlServerSourceCredentialStore.ConnectAsync(connectionString, timeProvider);
    }
}