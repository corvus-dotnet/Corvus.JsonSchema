// <copyright file="SqliteEnvironmentAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentAdministratorStoreConformance"/> suite against
/// <see cref="SqliteEnvironmentAdministratorStore"/>, each test over its own isolated in-memory SQLite database (held
/// open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteEnvironmentAdministratorStoreConformanceTests : EnvironmentAdministratorStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteEnvironmentAdministratorStore.ConnectAsync("Data Source=:memory:", timeProvider);
}