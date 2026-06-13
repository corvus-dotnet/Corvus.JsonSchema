// <copyright file="SqliteSecurityPolicyStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite.Tests;

/// <summary>
/// Runs the shared <see cref="SecurityPolicyStoreConformance"/> suite against <see cref="SqliteSecurityPolicyStore"/>,
/// each test over its own isolated in-memory SQLite database (held open for the store's lifetime).
/// </summary>
[TestClass]
public sealed class SqliteSecurityPolicyStoreConformanceTests : SecurityPolicyStoreConformance
{
    protected override async ValueTask<ISecurityPolicyStore> CreateStoreAsync(TimeProvider timeProvider)
        => await SqliteSecurityPolicyStore.ConnectAsync("Data Source=:memory:", timeProvider);
}
