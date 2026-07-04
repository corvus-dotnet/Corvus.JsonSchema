// <copyright file="InMemoryWorkspaceWorkflowStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="WorkspaceWorkflowStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryWorkspaceWorkflowStoreConformanceTests : WorkspaceWorkflowStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IWorkspaceWorkflowStore> CreateStoreAsync(TimeProvider timeProvider)
        => ValueTask.FromResult<IWorkspaceWorkflowStore>(new InMemoryWorkspaceWorkflowStore(timeProvider));
}