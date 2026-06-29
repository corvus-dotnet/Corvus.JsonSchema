// <copyright file="InMemoryAvailabilityRequestStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared <see cref="AvailabilityRequestStoreConformance"/> suite against the reference
/// <see cref="InMemoryAvailabilityRequestStore"/>.
/// </summary>
[TestClass]
public sealed class InMemoryAvailabilityRequestStoreConformanceTests : AvailabilityRequestStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IAvailabilityRequestStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryAvailabilityRequestStore(timeProvider));
}