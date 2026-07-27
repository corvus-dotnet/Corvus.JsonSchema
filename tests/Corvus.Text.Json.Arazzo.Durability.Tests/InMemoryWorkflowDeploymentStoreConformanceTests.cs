// <copyright file="InMemoryWorkflowDeploymentStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared <see cref="WorkflowDeploymentStoreConformance"/> suite against the reference
/// <see cref="InMemoryWorkflowDeploymentStore"/>.
/// </summary>
[TestClass]
public sealed class InMemoryWorkflowDeploymentStoreConformanceTests : WorkflowDeploymentStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IWorkflowDeploymentStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryWorkflowDeploymentStore(timeProvider));
}