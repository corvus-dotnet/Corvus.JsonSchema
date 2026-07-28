// <copyright file="SqliteWorkflowDeploymentStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowDeploymentStoreConformance"/> suite against <see cref="SqliteWorkflowDeploymentStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteWorkflowDeploymentStoreConformanceTests : WorkflowDeploymentStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IWorkflowDeploymentStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteWorkflowDeploymentStore.ConnectAsync("Data Source=:memory:", timeProvider);
}