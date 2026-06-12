// <copyright file="InMemoryRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared registry-conformance suite against <see cref="InMemoryRunnerRegistry"/> — the reference
/// implementation of <see cref="IRunnerRegistry"/>.
/// </summary>
[TestClass]
public sealed class InMemoryRunnerRegistryConformanceTests : RunnerRegistryConformance
{
    protected override ValueTask<IRunnerRegistry> CreateRegistryAsync()
        => ValueTask.FromResult<IRunnerRegistry>(new InMemoryRunnerRegistry());
}