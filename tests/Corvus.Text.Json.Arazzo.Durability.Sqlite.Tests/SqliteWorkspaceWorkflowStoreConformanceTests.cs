// <copyright file="SqliteWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against <see cref="SqliteWorkspaceWorkflowStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteWorkspaceWorkflowStore.ConnectAsync("Data Source=:memory:", timeProvider);
}