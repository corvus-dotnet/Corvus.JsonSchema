// <copyright file="InMemorySourceStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="SourceStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemorySourceStoreConformanceTests : SourceStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<ISourceStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemorySourceStore(timeProvider));
}