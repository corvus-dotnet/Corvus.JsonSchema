// <copyright file="InMemoryNativeBuildJobStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared <see cref="NativeBuildJobStoreConformance"/> suite against the reference
/// <see cref="InMemoryNativeBuildJobStore"/>.
/// </summary>
[TestClass]
public sealed class InMemoryNativeBuildJobStoreConformanceTests : NativeBuildJobStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<INativeBuildJobStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemoryNativeBuildJobStore(timeProvider));
}