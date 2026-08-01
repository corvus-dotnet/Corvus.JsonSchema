// <copyright file="EnvironmentStoreSealKeyResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Proves the store-backed seal-key resolver (ADR 0065): a sealed environment's registration resolves to the key
/// id and the decoded seal key, unsealed and unknown environments resolve to the baseline posture, and the cache
/// serves within its TTL (a rotation becomes visible once it lapses).
/// </summary>
[TestClass]
public sealed class EnvironmentStoreSealKeyResolverTests
{
    private static readonly byte[] SealKeyBytes = SealedCheckpointProtector.GenerateKeyPair().SealKey;

    [TestMethod]
    public async Task A_sealed_environments_registration_resolves_with_the_key_decoded()
    {
        var store = new InMemoryEnvironmentStore();
        await AddEnvironmentAsync(store, "acme-prod", "acme-1", SealKeyBytes);
        var resolver = new EnvironmentStoreSealKeyResolver(store);

        EnvironmentSealKey? resolved = await resolver.ResolveAsync("acme-prod", default);

        resolved.ShouldNotBeNull();
        resolved.Value.KeyId.ShouldBe("acme-1");
        resolved.Value.SealKey.ToArray().ShouldBe(SealKeyBytes);
    }

    [TestMethod]
    public async Task Unsealed_and_unknown_environments_resolve_to_null()
    {
        var store = new InMemoryEnvironmentStore();
        await AddEnvironmentAsync(store, "open-dev", keyId: null, sealKey: null);
        var resolver = new EnvironmentStoreSealKeyResolver(store);

        (await resolver.ResolveAsync("open-dev", default)).ShouldBeNull();
        (await resolver.ResolveAsync("never-registered", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task The_cache_serves_within_its_ttl_and_a_rotation_is_visible_once_it_lapses()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero));
        var store = new InMemoryEnvironmentStore();
        WorkflowEtag etag = await AddEnvironmentAsync(store, "acme-prod", "acme-2026-08", SealKeyBytes);
        var resolver = new EnvironmentStoreSealKeyResolver(store, TimeSpan.FromSeconds(30), clock);

        (await resolver.ResolveAsync("acme-prod", default))!.Value.KeyId.ShouldBe("acme-2026-08");

        // Rotate the registration in the store; within the TTL the cached generation still serves.
        await RotateAsync(store, "acme-prod", "acme-2026-09", SealKeyBytes, etag);
        clock.Advance(TimeSpan.FromSeconds(10));
        (await resolver.ResolveAsync("acme-prod", default))!.Value.KeyId.ShouldBe("acme-2026-08");

        // Once the TTL lapses the rotation is visible.
        clock.Advance(TimeSpan.FromSeconds(30));
        (await resolver.ResolveAsync("acme-prod", default))!.Value.KeyId.ShouldBe("acme-2026-09");
    }

    private static async Task<WorkflowEtag> AddEnvironmentAsync(InMemoryEnvironmentStore store, string name, string? keyId, byte[]? sealKey)
    {
        using ParsedJsonDocument<Environment> draft = Draft(name, keyId, sealKey);
        using ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, "test", default);
        return added.RootElement.EtagValue;
    }

    private static async Task RotateAsync(InMemoryEnvironmentStore store, string name, string keyId, byte[] sealKey, WorkflowEtag etag)
    {
        using ParsedJsonDocument<Environment> draft = Draft(name: null, keyId, sealKey);
        (await store.UpdateAsync(name, draft.RootElement, etag, "test", AccessContext.System, default))?.Dispose();
    }

    // The pooled source documents stay alive across the Draft call (it copies their values synchronously into the
    // pooled draft), so each branch parses, drafts, and only then lets `using` return the buffers.
    private static ParsedJsonDocument<Environment> Draft(string? name, string? keyId, byte[]? sealKey)
    {
        if (keyId is null)
        {
            using ParsedJsonDocument<JsonElement> plainName = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes($"\"{name}\""));
            return Environment.Draft(plainName.RootElement, default, default, default);
        }

        string registration = $$"""{"keyId":"{{keyId}}","sealKey":"{{Convert.ToBase64String(sealKey!)}}"}""";
        using ParsedJsonDocument<JsonElement> registrationDocument = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(registration));
        if (name is null)
        {
            return Environment.Draft(default, default, default, default, checkpointKey: registrationDocument.RootElement);
        }

        using ParsedJsonDocument<JsonElement> nameDocument = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes($"\"{name}\""));
        return Environment.Draft(nameDocument.RootElement, default, default, default, checkpointKey: registrationDocument.RootElement);
    }
}