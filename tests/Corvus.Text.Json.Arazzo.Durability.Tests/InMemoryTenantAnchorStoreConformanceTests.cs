// <copyright file="InMemoryTenantAnchorStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Anchoring;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared tenant-anchor-store conformance suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemoryTenantAnchorStoreConformanceTests : TenantAnchorStoreConformance
{
    protected override ValueTask<ITenantAnchorStore> CreateStoreAsync() => new(new InMemoryTenantAnchorStore());
}