// <copyright file="InMemoryStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared store-conformance suite against <see cref="InMemoryWorkflowStateStore"/> — the reference
/// implementation of <see cref="IWorkflowStateStore"/> / <see cref="IWorkflowWaitIndex"/>.
/// </summary>
[TestClass]
public sealed class InMemoryStoreConformanceTests : WorkflowStateStoreConformance
{
    protected override ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
        => ValueTask.FromResult<IWorkflowStateStore>(new InMemoryWorkflowStateStore(timeProvider));
}