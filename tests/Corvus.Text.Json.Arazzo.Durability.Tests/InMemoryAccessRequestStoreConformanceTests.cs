// <copyright file="InMemoryAccessRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="AccessRequestStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryAccessRequestStoreConformanceTests : AccessRequestStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IAccessRequestStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryAccessRequestStore(timeProvider));
}