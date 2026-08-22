// <copyright file="SqliteScheduleRegistryConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared schedule-registry conformance suite against <see cref="SqliteScheduleRegistry"/> over an
/// in-memory SQLite database (each test opens a fresh one, so each gets an empty registry).
/// </summary>
[TestClass]
public sealed class SqliteScheduleRegistryConformanceTests : ScheduleRegistryConformance
{
    protected override async ValueTask<IScheduleRegistry> CreateRegistryAsync()
        => await SqliteScheduleRegistry.ConnectAsync("Data Source=:memory:");
}