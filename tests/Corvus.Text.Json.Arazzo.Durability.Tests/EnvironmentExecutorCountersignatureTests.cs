// <copyright file="EnvironmentExecutorCountersignatureTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

[TestClass]
public sealed class EnvironmentExecutorCountersignatureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void A_countersignature_is_recorded_in_order_replaced_for_the_same_version_and_withdrawn()
    {
        using ParsedJsonDocument<Environment> stored = Parse("""{"name":"production","createdBy":"ops","createdAt":"2026-01-01T00:00:00Z","etag":"e1","keyGenerations":[{"keyId":"k1","sealPublicKey":"AAEC","algorithm":"ES256","state":"Active","registeredBy":"ops","registeredAt":"2026-01-01T00:00:00Z"}]}""");

        using ParsedJsonDocument<Environment> one = Countersign(stored.RootElement, "onboard", 2, "h2", "d2");
        Ids(one.RootElement).ShouldBe(["onboard/2"]);
        Environment.EnvironmentExecutorCountersignature recorded = Environment.FindExecutorCountersignature(one.RootElement, "onboard", 2)!.Value;
        ((string)recorded.PackageHash).ShouldBe("h2");
        ((string)recorded.AssemblyDigest).ShouldBe("d2");
        ((string)recorded.Signature).ShouldBe("c2ln");
        ((string)recorded.SignedBy).ShouldBe("alice");
        Environment.FindExecutorCountersignature(one.RootElement, "onboard", 3).ShouldBeNull();

        // Sorted by base workflow id then version, wherever they are recorded; the key generations ride along.
        using ParsedJsonDocument<Environment> two = Countersign(one.RootElement, "onboard", 1, "h1", "d1");
        using ParsedJsonDocument<Environment> three = Countersign(two.RootElement, "nightly", 7, "h7", "d7");
        using ParsedJsonDocument<Environment> four = Countersign(three.RootElement, "onboard", 3, "h3", "d3");
        Ids(four.RootElement).ShouldBe(["nightly/7", "onboard/1", "onboard/2", "onboard/3"]);
        Environment.Enumerate(four.RootElement.KeyGenerations).GetEnumerator().MoveNext().ShouldBeTrue("the generations are echoed through the draft");

        // Re-signing a version replaces its record in place rather than adding a second.
        using ParsedJsonDocument<Environment> replaced = Countersign(four.RootElement, "onboard", 2, "h2b", "d2b");
        Ids(replaced.RootElement).ShouldBe(["nightly/7", "onboard/1", "onboard/2", "onboard/3"]);
        ((string)Environment.FindExecutorCountersignature(replaced.RootElement, "onboard", 2)!.Value.AssemblyDigest).ShouldBe("d2b");

        // Withdrawing removes exactly that one; withdrawing what is not there changes nothing.
        using ParsedJsonDocument<Environment> withdrawn = Environment.DraftWithExecutorCountersignatureWithdrawn(replaced.RootElement, "onboard", 2);
        Ids(withdrawn.RootElement).ShouldBe(["nightly/7", "onboard/1", "onboard/3"]);
        using ParsedJsonDocument<Environment> unchanged = Environment.DraftWithExecutorCountersignatureWithdrawn(withdrawn.RootElement, "onboard", 2);
        Ids(unchanged.RootElement).ShouldBe(["nightly/7", "onboard/1", "onboard/3"]);
    }

    [TestMethod]
    public void The_countersignatures_survive_a_key_registration_and_an_ordinary_update()
    {
        using ParsedJsonDocument<Environment> stored = Parse("""{"name":"production","createdBy":"ops","createdAt":"2026-01-01T00:00:00Z","etag":"e1"}""");
        using ParsedJsonDocument<Environment> signed = Countersign(stored.RootElement, "onboard", 1, "h1", "d1");

        // A key registration through the key draft carries the countersignatures forward.
        using ParsedJsonDocument<JsonElement> spki = ParsedJsonDocument<JsonElement>.Parse("\"AAEC\""u8.ToArray());
        using ParsedJsonDocument<JsonElement> algorithm = ParsedJsonDocument<JsonElement>.Parse("\"ES256\""u8.ToArray());
        using ParsedJsonDocument<Environment> registered = Environment.DraftWithKeyRegistered(signed.RootElement, "k1", spki.RootElement, algorithm.RootElement, "ops", T0);
        Ids(registered.RootElement).ShouldBe(["onboard/1"]);
        Environment.Enumerate(registered.RootElement.KeyGenerations).GetEnumerator().MoveNext().ShouldBeTrue();

        // An ordinary update whose draft omits them carries the stored set forward, as it does the generations.
        using ParsedJsonDocument<Environment> rename = Environment.Draft("production", "Production", null, SecurityTagSet.Empty);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            registered.RootElement.WriteUpdated(writer, rename.RootElement, "ops", T0, new WorkflowEtag("e2"));
        }

        using ParsedJsonDocument<Environment> updated = ParsedJsonDocument<Environment>.Parse(buffer.WrittenMemory);
        Ids(updated.RootElement).ShouldBe(["onboard/1"]);
        ((string)updated.RootElement.DisplayName).ShouldBe("Production");
    }

    private static ParsedJsonDocument<Environment> Countersign(in Environment stored, string baseWorkflowId, int versionNumber, string hash, string digest)
    {
        using ParsedJsonDocument<JsonElement> packageHash = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes($"\"{hash}\""));
        using ParsedJsonDocument<JsonElement> assemblyDigest = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes($"\"{digest}\""));
        using ParsedJsonDocument<JsonElement> signature = ParsedJsonDocument<JsonElement>.Parse("\"c2ln\""u8.ToArray());
        return Environment.DraftWithExecutorCountersigned(stored, baseWorkflowId, versionNumber, packageHash.RootElement, assemblyDigest.RootElement, signature.RootElement, "alice", T0);
    }

    private static List<string> Ids(in Environment environment)
    {
        var ids = new List<string>();
        foreach (Environment.EnvironmentExecutorCountersignature countersignature in Environment.Enumerate(environment.ExecutorCountersignatures))
        {
            ids.Add($"{(string)countersignature.BaseWorkflowId}/{(int)countersignature.VersionNumber}");
        }

        return ids;
    }

    private static ParsedJsonDocument<Environment> Parse(string json) => ParsedJsonDocument<Environment>.Parse(Encoding.UTF8.GetBytes(json));
}