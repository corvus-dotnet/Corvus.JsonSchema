// <copyright file="SourceCredentialStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="ISourceCredentialStore"/> must satisfy: binding CRUD keyed by
/// (sourceName, environment), optimistic concurrency via etag, deterministic list ordering, and — the §13 trust
/// boundary — that what is persisted is a <em>reference</em> plus non-sensitive metadata, never secret material. A
/// backend's test project derives a concrete <see cref="TestClassAttribute"/> and implements
/// <see cref="CreateStoreAsync"/>; the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class SourceCredentialStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for audit timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<ISourceCredentialStore> CreateStoreAsync(TimeProvider timeProvider);

    /// <summary>Disposes any stores created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task A_binding_round_trips_through_add_get_and_list()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        SourceCredentialDefinition definition = new(
            "petstore",
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", "keyvault://petstore-apikey#3")],
            [new CredentialConfigDefinition("headerName", "X-Api-Key")],
            "Petstore API key.");

        using (ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(definition, "alice", default))
        {
            added.RootElement.IdValue.ShouldNotBeNullOrEmpty();
            added.RootElement.SourceNameValue.ShouldBe("petstore");
            added.RootElement.EnvironmentValue.ShouldBe("production");
            added.RootElement.AuthKindValue.ShouldBe(SourceCredentialKind.ApiKey);
            added.RootElement.DescriptionOrNull.ShouldBe("Petstore API key.");
            added.RootElement.CreatedByValue.ShouldBe("alice");
            added.RootElement.EtagValue.IsNone.ShouldBeFalse();
            added.RootElement.TryGetConfigValue("headerName", out string? header).ShouldBeTrue();
            header.ShouldBe("X-Api-Key");
        }

        using (ParsedJsonDocument<SourceCredentialBinding>? fetched = await store.GetAsync("petstore", "production", default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.TryGetSecretRef("value", out SecretRef secretRef).ShouldBeTrue();
            secretRef.Scheme.ShouldBe(SecretScheme.KeyVault);
            secretRef.Locator.ShouldBe("petstore-apikey");
            secretRef.Version.ShouldBe("3");
        }

        using (PooledDocumentList<SourceCredentialBinding> list = await store.ListAsync(default))
        {
            list.Select(b => b.SourceNameValue).ShouldBe(["petstore"]);
        }

        (await store.GetAsync("petstore", "staging", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Adding_a_duplicate_source_environment_fails()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        SourceCredentialDefinition definition = ApiKey("petstore", "production");
        using (await store.AddAsync(definition, "alice", default))
        {
        }

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await store.AddAsync(definition, "bob", default));

        // The same source in a different environment is a distinct binding.
        using (await store.AddAsync(ApiKey("petstore", "staging"), "alice", default))
        {
        }

        using PooledDocumentList<SourceCredentialBinding> list = await store.ListAsync(default);
        list.Select(b => b.EnvironmentValue).ShouldBe(["production", "staging"]);
    }

    [TestMethod]
    public async Task Updating_a_binding_bumps_the_etag_carries_identity_and_records_the_actor()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        string addedId;
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(ApiKey("petstore", "production"), "alice", default))
        {
            addedId = added.RootElement.IdValue;
            addedEtag = added.RootElement.EtagValue;
        }

        SourceCredentialDefinition rotated = new(
            "petstore",
            "production",
            SourceCredentialKind.Bearer,
            [new SecretReferenceDefinition("value", "keyvault://petstore-token#9")]);

        using (ParsedJsonDocument<SourceCredentialBinding>? updated = await store.UpdateAsync("petstore", "production", rotated, addedEtag, "bob", default))
        {
            updated.ShouldNotBeNull();
            updated!.RootElement.IdValue.ShouldBe(addedId); // immutable identity carried forward
            updated.RootElement.CreatedByValue.ShouldBe("alice"); // created-* audit carried forward
            updated.RootElement.AuthKindValue.ShouldBe(SourceCredentialKind.Bearer);
            updated.RootElement.LastUpdatedByOrNull.ShouldBe("bob");
            updated.RootElement.LastUpdatedAtValue.ShouldNotBeNull();
            (updated.RootElement.EtagValue == addedEtag).ShouldBeFalse();
            updated.RootElement.TryGetSecretRef("value", out SecretRef secretRef).ShouldBeTrue();
            secretRef.Version.ShouldBe("9");
        }

        (await store.UpdateAsync("missing", "production", ApiKey("missing", "production"), WorkflowEtag.None, "bob", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task A_stale_etag_on_update_or_delete_conflicts()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        WorkflowEtag addedEtag;
        using (ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(ApiKey("petstore", "production"), "alice", default))
        {
            addedEtag = added.RootElement.EtagValue;
        }

        using (await store.UpdateAsync("petstore", "production", ApiKey("petstore", "production"), addedEtag, "bob", default))
        {
            // etag now advanced
        }

        await Should.ThrowAsync<SourceCredentialConflictException>(async () =>
            await store.UpdateAsync("petstore", "production", ApiKey("petstore", "production"), addedEtag, "carol", default));
        await Should.ThrowAsync<SourceCredentialConflictException>(async () =>
            await store.DeleteAsync("petstore", "production", addedEtag, default));

        // WorkflowEtag.None overwrites/deletes unconditionally.
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, default)).ShouldBeTrue();
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, default)).ShouldBeFalse();
    }

    [TestMethod]
    public async Task The_listing_is_ordered_by_source_then_environment()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        using (await store.AddAsync(ApiKey("zeta", "production"), "alice", default))
        {
        }

        using (await store.AddAsync(ApiKey("alpha", "staging"), "alice", default))
        {
        }

        using (await store.AddAsync(ApiKey("alpha", "production"), "alice", default))
        {
        }

        using PooledDocumentList<SourceCredentialBinding> list = await store.ListAsync(default);
        list.Select(b => $"{b.SourceNameValue}@{b.EnvironmentValue}").ShouldBe(["alpha@production", "alpha@staging", "zeta@production"]);
    }

    [TestMethod]
    public async Task An_inline_secret_like_reference_is_rejected_at_the_boundary()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();

        // A bare value with no scheme is not a SecretRef — the store must refuse to persist it, so secret material can
        // never be smuggled into the binding document inline.
        SourceCredentialDefinition inlineSecret = new(
            "petstore",
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", "hunter2-the-actual-secret")]);

        await Should.ThrowAsync<ArgumentException>(async () => await store.AddAsync(inlineSecret, "alice", default));
    }

    [TestMethod]
    public async Task The_persisted_document_holds_references_not_secret_material()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        SourceCredentialDefinition definition = new(
            "petstore",
            "production",
            SourceCredentialKind.OAuth2ClientCredentials,
            [new SecretReferenceDefinition("clientSecret", "vault://kv/data/petstore#client")],
            [new CredentialConfigDefinition("tokenUrl", "https://auth.example/token"), new CredentialConfigDefinition("scopes", "read write")]);

        using (await store.AddAsync(definition, "alice", default))
        {
        }

        using ParsedJsonDocument<SourceCredentialBinding>? fetched = await store.GetAsync("petstore", "production", default);
        fetched.ShouldNotBeNull();

        // The serialized document carries the reference (and non-secret config) and nothing that looks like a secret.
        string json = Encoding.UTF8.GetString(fetched!.RootElement.ToJsonBytes());
        json.ShouldContain("vault://kv/data/petstore#client");
        json.ShouldContain("https://auth.example/token");
        fetched.RootElement.TryGetSecretRef("clientSecret", out SecretRef secretRef).ShouldBeTrue();
        secretRef.Scheme.ShouldBe(SecretScheme.HashiCorpVault);
        secretRef.Version.ShouldBe("client");
    }

    private static SourceCredentialDefinition ApiKey(string sourceName, string environment) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"keyvault://{sourceName}-{environment}-apikey")]);

    private async ValueTask<ISourceCredentialStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        ISourceCredentialStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}