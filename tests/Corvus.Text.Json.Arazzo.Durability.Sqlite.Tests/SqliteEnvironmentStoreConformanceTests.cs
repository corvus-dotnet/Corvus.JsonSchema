// <copyright file="SqliteEnvironmentStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentStoreConformance"/> suite against <see cref="SqliteEnvironmentStore"/>, each
/// test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteEnvironmentStoreConformanceTests : EnvironmentStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IEnvironmentStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteEnvironmentStore.ConnectAsync("Data Source=:memory:", timeProvider);
}