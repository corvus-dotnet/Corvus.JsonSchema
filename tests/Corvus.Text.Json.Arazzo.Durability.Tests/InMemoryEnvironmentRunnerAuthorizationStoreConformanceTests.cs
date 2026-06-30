// <copyright file="InMemoryEnvironmentRunnerAuthorizationStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentRunnerAuthorizationStoreConformance"/> suite against the reference
/// <see cref="InMemoryEnvironmentRunnerAuthorizationStore"/>.
/// </summary>
[TestClass]
public sealed class InMemoryEnvironmentRunnerAuthorizationStoreConformanceTests : EnvironmentRunnerAuthorizationStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IEnvironmentRunnerAuthorizationStore> CreateStoreAsync()
        => new(new InMemoryEnvironmentRunnerAuthorizationStore(new TestTimeProvider(new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero))));
}