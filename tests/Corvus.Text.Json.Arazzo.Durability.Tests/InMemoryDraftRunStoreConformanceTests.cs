// <copyright file="InMemoryDraftRunStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared draft-run-store conformance suite against the in-memory reference implementation.</summary>
[TestClass]
public sealed class InMemoryDraftRunStoreConformanceTests : DraftRunStoreConformance
{
    protected override ValueTask<IDraftRunStore> CreateStoreAsync()
        => ValueTask.FromResult<IDraftRunStore>(new InMemoryDraftRunStore());
}