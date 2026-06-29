// <copyright file="InMemoryAvailabilityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="AvailabilityStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryAvailabilityStoreConformanceTests : AvailabilityStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IAvailabilityStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryAvailabilityStore(timeProvider));
}