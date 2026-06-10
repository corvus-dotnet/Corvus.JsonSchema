// <copyright file="KeyVaultProtectedStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.KeyVault.Tests;

/// <summary>
/// Runs the shared store-conformance suite against the in-memory store wrapped in a
/// <see cref="ProtectedWorkflowStateStore"/> using a <see cref="KeyVaultCheckpointProtector"/> over a fake
/// vault. This exercises the protector's wrap/unwrap path and the envelope through the full store contract
/// without a real Key Vault (the AES-GCM crypto is real; only the vault round-trip is faked).
/// </summary>
[TestClass]
public sealed class KeyVaultProtectedStoreConformanceTests : WorkflowStateStoreConformance
{
    protected override ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        var protector = new KeyVaultCheckpointProtector(new FakeCryptographyClient());
        var inner = new InMemoryWorkflowStateStore(timeProvider);
        return ValueTask.FromResult<IWorkflowStateStore>(new ProtectedWorkflowStateStore(inner, protector));
    }
}
