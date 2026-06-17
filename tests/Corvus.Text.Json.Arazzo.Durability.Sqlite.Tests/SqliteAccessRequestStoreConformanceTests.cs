// <copyright file="SqliteAccessRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="AccessRequestStoreConformance"/> suite against <see cref="SqliteAccessRequestStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteAccessRequestStoreConformanceTests : AccessRequestStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IAccessRequestStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteAccessRequestStore.ConnectAsync("Data Source=:memory:", timeProvider);
}