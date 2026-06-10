// <copyright file="SqliteStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared store-conformance suite against <see cref="SqliteWorkflowStateStore"/>, each test over its
/// own isolated in-memory SQLite database (the store holds a single connection, so an unnamed in-memory
/// database lives for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteStoreConformanceTests : WorkflowStateStoreConformance
{
    protected override async ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteWorkflowStateStore.ConnectAsync("Data Source=:memory:", timeProvider);
}