// <copyright file="InMemoryEnvironmentAdministratorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="EnvironmentAdministratorStoreConformance"/> suite against the reference
/// <see cref="InMemoryEnvironmentAdministratorStore"/>.</summary>
[TestClass]
public sealed class InMemoryEnvironmentAdministratorStoreConformanceTests : EnvironmentAdministratorStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IEnvironmentAdministratorStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryEnvironmentAdministratorStore(timeProvider));
}