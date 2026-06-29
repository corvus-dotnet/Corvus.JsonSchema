// <copyright file="SqliteAvailabilityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityStoreConformance"/> suite against <see cref="SqliteAvailabilityStore"/>, each
/// test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteAvailabilityStoreConformanceTests : AvailabilityStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IAvailabilityStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteAvailabilityStore.ConnectAsync("Data Source=:memory:", timeProvider);
}