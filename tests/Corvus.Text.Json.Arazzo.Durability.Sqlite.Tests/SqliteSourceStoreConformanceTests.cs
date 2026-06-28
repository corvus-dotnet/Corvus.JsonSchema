// <copyright file="SqliteSourceStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="SourceStoreConformance"/> suite against <see cref="SqliteSourceStore"/>, each
/// test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteSourceStoreConformanceTests : SourceStoreConformance
{
    /// <inheritdoc/>
    protected override async ValueTask<ISourceStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteSourceStore.ConnectAsync("Data Source=:memory:", timeProvider);
}