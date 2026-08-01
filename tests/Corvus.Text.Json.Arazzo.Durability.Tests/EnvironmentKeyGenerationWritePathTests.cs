// <copyright file="EnvironmentKeyGenerationWritePathTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Environments;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The write-path plumbing for an environment's key generations (ADR 0065), proved through a real store rather than
/// against the writer alone. Adding a field to the schema makes it readable and never persisted, and
/// <see cref="Environment.WriteUpdated"/> enumerates each property by hand, so what matters is what an update does to
/// a field it says nothing about.
/// </summary>
[TestClass]
public sealed class EnvironmentKeyGenerationWritePathTests
{
    private const string OneActiveGeneration = """
        [{"keyId":"k1","sealPublicKey":"AAAA","algorithm":"ES256","state":"Active","registeredBy":"alice","registeredAt":"2026-08-01T09:00:00+00:00"}]
        """;

    private const string RotatedGenerations = """
        [{"keyId":"k1","sealPublicKey":"AAAA","algorithm":"ES256","state":"Retired","registeredBy":"alice","registeredAt":"2026-08-01T09:00:00+00:00"},{"keyId":"k2","sealPublicKey":"BBBB","algorithm":"ES256","state":"Active","registeredBy":"alice","registeredAt":"2026-08-01T10:00:00+00:00"}]
        """;

    [TestMethod]
    public async Task A_registered_generation_persists_through_the_store()
    {
        // Schema plus regeneration makes a field readable, not persisted. This is the end-to-end proof that a
        // generation survives a real write and read rather than only the writer's own output.
        IEnvironmentStore store = new InMemoryEnvironmentStore();
        WorkflowEtag etag = await AddAsync(store, "production", "Production", "The live environment.");

        await RegisterAsync(store, "production", etag, OneActiveGeneration);

        using ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default);
        fetched.ShouldNotBeNull();
        Generations(fetched!.RootElement).Select(g => (string)g.KeyId).ShouldBe(["k1"]);
    }

    [TestMethod]
    public async Task An_unrelated_environment_update_preserves_the_generations()
    {
        // The failure this pins: WriteUpdated lists every property, so a generation set it omitted would be dropped
        // by an unrelated rename. That silently removes the last active generation the tenancy invariant reads,
        // turning a gated deployment into an ungated one with nothing reported anywhere.
        IEnvironmentStore store = new InMemoryEnvironmentStore();
        WorkflowEtag etag = await AddAsync(store, "production", "Production", "The live environment.");
        etag = await RegisterAsync(store, "production", etag, OneActiveGeneration);

        using (ParsedJsonDocument<Environment> rename = Environment.Draft("production", "Renamed", "A new description.", default))
        using (ParsedJsonDocument<Environment>? updated = await store.UpdateAsync("production", rename.RootElement, etag, "bob", AccessContext.System, default))
        {
            updated.ShouldNotBeNull();
        }

        using ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default);
        Generations(fetched!.RootElement).Select(g => (string)g.KeyId).ShouldBe(["k1"], "a rename must not drop the generations");
        ((string)fetched.RootElement.DisplayName).ShouldBe("Renamed");
    }

    [TestMethod]
    public async Task Registering_a_generation_preserves_every_other_mutable_value()
    {
        // The mirror failure: a partial update expressed through a full-replace write path. A draft carrying only the
        // generations would blank displayName and description, because WriteUpdated takes those from the draft alone.
        IEnvironmentStore store = new InMemoryEnvironmentStore();
        WorkflowEtag etag = await AddAsync(store, "production", "Production", "The live environment.");

        await RegisterAsync(store, "production", etag, OneActiveGeneration);

        using ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default);
        ((string)fetched!.RootElement.DisplayName).ShouldBe("Production");
        ((string)fetched.RootElement.Description).ShouldBe("The live environment.");
    }

    [TestMethod]
    public async Task A_rotation_replaces_the_whole_set_so_a_retired_generation_stays_recorded()
    {
        IEnvironmentStore store = new InMemoryEnvironmentStore();
        WorkflowEtag etag = await AddAsync(store, "production", "Production", null);
        etag = await RegisterAsync(store, "production", etag, OneActiveGeneration);

        await RegisterAsync(store, "production", etag, RotatedGenerations);

        using ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default);
        List<Environment.EnvironmentKeyGeneration> generations = Generations(fetched!.RootElement);
        generations.Select(g => (string)g.KeyId).ShouldBe(["k1", "k2"]);
        ((string)generations[0].State).ShouldBe("Retired", "retirement is recorded, never a delete");
        ((string)generations[1].State).ShouldBe("Active");
    }

    [TestMethod]
    public async Task A_new_environment_has_no_generations()
    {
        IEnvironmentStore store = new InMemoryEnvironmentStore();
        await AddAsync(store, "production", "Production", null);

        using ParsedJsonDocument<Environment>? fetched = await store.GetAsync("production", AccessContext.System, default);
        fetched!.RootElement.KeyGenerations.IsUndefined().ShouldBeTrue("creation never registers a key");
    }

    private static async Task<WorkflowEtag> AddAsync(IEnvironmentStore store, string name, string? displayName, string? description)
    {
        using ParsedJsonDocument<Environment> draft = Environment.Draft(name, displayName, description, default);
        using ParsedJsonDocument<Environment> added = await store.AddAsync(draft.RootElement, "alice", default);
        return added.RootElement.EtagValue;
    }

    private static async Task<WorkflowEtag> RegisterAsync(IEnvironmentStore store, string name, WorkflowEtag etag, string generationsJson)
    {
        using ParsedJsonDocument<Environment>? stored = await store.GetAsync(name, AccessContext.System, default);
        Environment.EnvironmentKeyGenerationArray generations = Environment.EnvironmentKeyGenerationArray.ParseValue(generationsJson);
        using ParsedJsonDocument<Environment> draft = Environment.DraftWithKeyGenerations(stored!.RootElement, generations);
        using ParsedJsonDocument<Environment>? updated = await store.UpdateAsync(name, draft.RootElement, etag, "alice", AccessContext.System, default);
        updated.ShouldNotBeNull();
        return updated!.RootElement.EtagValue;
    }

    private static List<Environment.EnvironmentKeyGeneration> Generations(in Environment environment)
    {
        var generations = new List<Environment.EnvironmentKeyGeneration>();
        foreach (Environment.EnvironmentKeyGeneration generation in environment.KeyGenerations.EnumerateArray())
        {
            generations.Add(generation);
        }

        return generations;
    }
}