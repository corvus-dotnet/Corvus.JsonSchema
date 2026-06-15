// <copyright file="InMemoryWorkflowAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="WorkflowAdministratorStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryWorkflowAdministratorStoreConformanceTests : WorkflowAdministratorStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IWorkflowAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryWorkflowAdministratorStore(timeProvider));
}
