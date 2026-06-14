// <copyright file="PooledDocumentPatternTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the candidate "store returns a pooled, caller-disposed document" pattern before it is rolled through the
/// stores: a store hands back a <see cref="ParsedJsonDocument{T}"/> whose buffer is rented from the pool; a transient
/// consumer reads and disposes it; a long-lived consumer clones the value out and disposes the instance it was given.
/// </summary>
[TestClass]
public class PooledDocumentPatternTests
{
    private static byte[] SampleRuleJson()
        => PersistedJson.ToArray(
            ("tenant-scoped", new SecurityRuleDefinition("sys:tenant == $claim.tenant", "Tenant isolation."), "alice", DateTimeOffset.UnixEpoch, new WorkflowEtag("etag-1")),
            static (Utf8JsonWriter writer, in (string Name, SecurityRuleDefinition Def, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => SecurityRuleDocument.WriteNew(writer, c.Name, c.Def, c.Actor, c.At, c.Tag));

    [TestMethod]
    public void A_pooled_document_round_trips_and_a_clone_outlives_its_dispose()
    {
        byte[] json = SampleRuleJson();

        SecurityRuleDocument retained;
        using (ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(json))
        {
            // Transient consumer: read fields directly off the pooled document.
            document.RootElement.NameValue.ShouldBe("tenant-scoped");
            document.RootElement.ExpressionValue.ShouldBe("sys:tenant == $claim.tenant");
            document.RootElement.EtagValue.ShouldBe(new WorkflowEtag("etag-1"));

            // Long-lived consumer: clone the value out, then let the pooled instance dispose.
            retained = document.RootElement.Clone();
        }

        // The clone is independent of the (now-disposed, buffer-returned) pooled document.
        retained.NameValue.ShouldBe("tenant-scoped");
        retained.ExpressionValue.ShouldBe("sys:tenant == $claim.tenant");
    }

    [TestMethod]
    public void A_pooled_document_allocates_less_than_a_detached_one_at_steady_state()
    {
        byte[] json = SampleRuleJson();

        // Warm both the ArrayPool and the writer/metadata caches so we measure steady state, not first-use growth.
        for (int i = 0; i < 64; i++)
        {
            using ParsedJsonDocument<SecurityRuleDocument> warm = PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
            _ = SecurityRuleDocument.FromJson(json);
        }

        long pooled = Measure(json, static j =>
        {
            using ParsedJsonDocument<SecurityRuleDocument> document = PersistedJson.ToPooledDocument<SecurityRuleDocument>(j);
            _ = document.RootElement.ValueKind;
        });

        long detached = Measure(json, static j =>
        {
            SecurityRuleDocument document = SecurityRuleDocument.FromJson(j);
            _ = document.ValueKind;
        });

        // The pooled document recycles its backing buffer + metadata; only the small wrapper is on the GC heap, so it
        // allocates strictly less than the detached document, which owns a fresh byte[] + metadata every time.
        pooled.ShouldBeLessThan(detached);
    }

    private static long Measure(byte[] json, Action<byte[]> action)
    {
        const int iterations = 256;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < iterations; i++)
        {
            action(json);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}