// <copyright file="InMemoryObservedIdentityStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="ObservedIdentityStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryObservedIdentityStoreConformanceTests : ObservedIdentityStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IObservedIdentityStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryObservedIdentityStore(timeProvider));
}