// <copyright file="SqliteRunnerRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared runner-registry conformance suite against <see cref="SqliteRunnerRegistry"/>, each test over
/// its own isolated in-memory SQLite database (the registry holds a single connection, so an unnamed in-memory
/// database lives for the registry's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteRunnerRegistryConformanceTests : RunnerRegistryConformance
{
    protected override async ValueTask<IRunnerRegistry> CreateRegistryAsync()
        => await SqliteRunnerRegistry.ConnectAsync("Data Source=:memory:");
}