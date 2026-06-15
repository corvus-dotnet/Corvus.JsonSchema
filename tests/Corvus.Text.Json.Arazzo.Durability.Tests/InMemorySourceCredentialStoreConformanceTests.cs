// <copyright file="InMemorySourceCredentialStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>Runs the shared <see cref="SourceCredentialStoreConformance"/> suite against the in-memory reference store.</summary>
[TestClass]
public sealed class InMemorySourceCredentialStoreConformanceTests : SourceCredentialStoreConformance
{
    /// <inheritdoc/>
    protected override ValueTask<ISourceCredentialStore> CreateStoreAsync(TimeProvider timeProvider)
        => new(new InMemorySourceCredentialStore(timeProvider));
}