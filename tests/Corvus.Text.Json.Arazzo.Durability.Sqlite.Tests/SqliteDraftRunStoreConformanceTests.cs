// <copyright file="SqliteDraftRunStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared draft-run-store conformance suite against <see cref="SqliteDraftRunStore"/>, each test over
/// its own isolated in-memory SQLite database (the store holds a single connection, so an unnamed in-memory
/// database lives for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteDraftRunStoreConformanceTests : DraftRunStoreConformance
{
    protected override async ValueTask<IDraftRunStore> CreateStoreAsync()
        => await SqliteDraftRunStore.ConnectAsync("Data Source=:memory:");
}