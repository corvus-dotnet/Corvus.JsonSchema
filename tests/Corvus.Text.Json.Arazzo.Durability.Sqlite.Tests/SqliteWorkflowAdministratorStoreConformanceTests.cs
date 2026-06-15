// <copyright file="SqliteWorkflowAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowAdministratorStoreConformance"/> suite against
/// <see cref="SqliteWorkflowAdministratorStore"/>, each test over its own isolated in-memory SQLite database (held open
/// for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteWorkflowAdministratorStoreConformanceTests : WorkflowAdministratorStoreConformance
{
    protected override async ValueTask<IWorkflowAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteWorkflowAdministratorStore.ConnectAsync("Data Source=:memory:", timeProvider);
}
