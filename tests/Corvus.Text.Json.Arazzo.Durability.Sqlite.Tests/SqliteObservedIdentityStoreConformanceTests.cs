// <copyright file="SqliteObservedIdentityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="ObservedIdentityStoreConformance"/> suite against <see cref="SqliteObservedIdentityStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteObservedIdentityStoreConformanceTests : ObservedIdentityStoreConformance
{
    protected override async ValueTask<IObservedIdentityStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteObservedIdentityStore.ConnectAsync("Data Source=:memory:", timeProvider);
}