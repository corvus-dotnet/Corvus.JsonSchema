// <copyright file="InMemoryCatalogStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared catalog-conformance suite against <see cref="InMemoryWorkflowCatalogStore"/> — the
/// reference implementation of <see cref="IWorkflowCatalogStore"/>.
/// </summary>
[TestClass]
public sealed class InMemoryCatalogStoreConformanceTests : WorkflowCatalogStoreConformance
{
    protected override ValueTask<IWorkflowCatalogStore> CreateStoreAsync(TimeProvider timeProvider)
        => ValueTask.FromResult<IWorkflowCatalogStore>(new InMemoryWorkflowCatalogStore(timeProvider));
}