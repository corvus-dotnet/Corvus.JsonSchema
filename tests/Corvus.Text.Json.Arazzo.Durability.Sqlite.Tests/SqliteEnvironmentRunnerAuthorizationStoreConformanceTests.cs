// <copyright file="SqliteEnvironmentRunnerAuthorizationStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="EnvironmentRunnerAuthorizationStoreConformance"/> suite against
/// <see cref="SqliteEnvironmentRunnerAuthorizationStore"/>, each test over its own isolated in-memory SQLite database
/// (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteEnvironmentRunnerAuthorizationStoreConformanceTests : EnvironmentRunnerAuthorizationStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<IEnvironmentRunnerAuthorizationStore> CreateStoreAsync()
        => await SqliteEnvironmentRunnerAuthorizationStore.ConnectAsync("Data Source=:memory:");
}