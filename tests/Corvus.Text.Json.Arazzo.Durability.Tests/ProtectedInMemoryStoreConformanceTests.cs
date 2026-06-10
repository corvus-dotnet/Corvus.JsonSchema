// <copyright file="ProtectedInMemoryStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Runs the shared store-conformance suite against the in-memory reference store wrapped in a
/// <see cref="ProtectedWorkflowStateStore"/> (AES-GCM). Passing the full suite proves the encryption decorator
/// is transparent: checkpoints encrypt on save and decrypt on load with an exact byte round-trip, and the
/// projected index still drives the wait/visibility queries.
/// </summary>
[TestClass]
public sealed class ProtectedInMemoryStoreConformanceTests : WorkflowStateStoreConformance
{
    protected override ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        byte[] key = new byte[32];
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (byte)(i + 1);
        }

        var protector = new AesGcmCheckpointProtector(key);
        var inner = new InMemoryWorkflowStateStore(timeProvider);
        return ValueTask.FromResult<IWorkflowStateStore>(new ProtectedWorkflowStateStore(inner, protector));
    }
}