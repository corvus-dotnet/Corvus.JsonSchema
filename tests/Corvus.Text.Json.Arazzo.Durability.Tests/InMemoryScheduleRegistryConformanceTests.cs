// <copyright file="InMemoryScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="ScheduleRegistryConformance"/> contract against the in-memory reference implementation.</summary>
[TestClass]
public sealed class InMemoryScheduleRegistryConformanceTests : ScheduleRegistryConformance
{
    /// <inheritdoc/>
    protected override ValueTask<IScheduleRegistry> CreateRegistryAsync()
        => new(new InMemoryScheduleRegistry());
}