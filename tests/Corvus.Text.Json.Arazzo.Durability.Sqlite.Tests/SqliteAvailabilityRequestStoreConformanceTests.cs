// <copyright file="SqliteAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against <see cref="SqliteAvailabilityRequestStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteAvailabilityRequestStore.ConnectAsync("Data Source=:memory:", timeProvider);
}