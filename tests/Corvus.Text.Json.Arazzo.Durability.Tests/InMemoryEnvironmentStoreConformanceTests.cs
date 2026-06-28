// <copyright file="InMemoryEnvironmentStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="EnvironmentStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryEnvironmentStoreConformanceTests : EnvironmentStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IEnvironmentStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryEnvironmentStore(timeProvider));
}