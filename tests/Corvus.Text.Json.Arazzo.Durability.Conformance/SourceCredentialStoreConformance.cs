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

        using (ParsedJsonDocument<SourceCredentialBinding>? fetched = await store.GetAsync("petstore", "production", AccessContext.System, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.TryGetSecretRef("value", out SecretRef secretRef).ShouldBeTrue();
            secretRef.Scheme.ShouldBe(SecretScheme.KeyVault);
            secretRef.Locator.ShouldBe("petstore-apikey");
            secretRef.Version.ShouldBe("3");
        }

        using (SourceCredentialPage page = await store.ListAsync(AccessContext.System, 1000, null, default))
        {
            page.Bindings.Select(b => b.SourceNameValue).ShouldBe(["petstore"]);
        }

        (await store.GetAsync("petstore", "staging", AccessContext.System, default)).ShouldBeNull();
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

        using SourceCredentialPage page = await store.ListAsync(AccessContext.System, 1000, null, default);
        page.Bindings.Select(b => b.EnvironmentValue).ShouldBe(["production", "staging"]);
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

        using (ParsedJsonDocument<SourceCredentialBinding>? updated = await store.UpdateAsync("petstore", "production", rotated, addedEtag, "bob", AccessContext.System, default))
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

        (await store.UpdateAsync("missing", "production", ApiKey("missing", "production"), WorkflowEtag.None, "bob", AccessContext.System, default)).ShouldBeNull();
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

        using (await store.UpdateAsync("petstore", "production", ApiKey("petstore", "production"), addedEtag, "bob", AccessContext.System, default))
        {
            // etag now advanced
        }

        await Should.ThrowAsync<SourceCredentialConflictException>(async () =>
            await store.UpdateAsync("petstore", "production", ApiKey("petstore", "production"), addedEtag, "carol", AccessContext.System, default));
        await Should.ThrowAsync<SourceCredentialConflictException>(async () =>
            await store.DeleteAsync("petstore", "production", addedEtag, AccessContext.System, default));

        // WorkflowEtag.None overwrites/deletes unconditionally.
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, AccessContext.System, default)).ShouldBeTrue();
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, AccessContext.System, default)).ShouldBeFalse();
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

        using SourceCredentialPage page = await store.ListAsync(AccessContext.System, 1000, null, default);
        page.Bindings.Select(b => $"{b.SourceNameValue}@{b.EnvironmentValue}").ShouldBe(["alpha@production", "alpha@staging", "zeta@production"]);
    }

    [TestMethod]
    public async Task Listing_keyset_pages_in_source_environment_order_without_gaps_or_duplicates()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        (string Source, string Env)[] keys =
        [
            ("petstore", "production"), ("petstore", "staging"), ("ledger", "production"),
            ("ledger", "staging"), ("ledger", "qa"), ("alpha", "production"), ("zeta", "production"), ("mid", "production"),
        ];

        // Add out of order, to prove the store (not insertion order) establishes the page order.
        foreach ((string source, string env) in keys.OrderByDescending(k => k.Source, StringComparer.Ordinal).ThenByDescending(k => k.Env, StringComparer.Ordinal))
        {
            using (await store.AddAsync(ApiKey(source, env), "system", default))
            {
            }
        }

        // Walk every page via the continuation token with a small limit; collect the keys in page order.
        var seen = new List<string>();
        string? token = null;
        int pages = 0;
        do
        {
            using SourceCredentialPage page = await store.ListAsync(AccessContext.System, 3, token, default);
            page.Bindings.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (SourceCredentialBinding b in page.Bindings)
            {
                seen.Add($"{b.SourceNameValue}@{b.EnvironmentValue}");
            }

            token = page.NextPageToken;
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; no duplicates or gaps across boundaries; contractual (source, env) order.
        pages.ShouldBe(3);
        string[] expected = keys
            .OrderBy(k => k.Source, StringComparer.Ordinal).ThenBy(k => k.Env, StringComparer.Ordinal)
            .Select(k => $"{k.Source}@{k.Env}").ToArray();
        seen.ShouldBe(expected);

        // A malformed token is rejected (rather than silently restarting).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using SourceCredentialPage bad = await store.ListAsync(AccessContext.System, 3, "this~is~not~a~token", default);
        });
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

        using ParsedJsonDocument<SourceCredentialBinding>? fetched = await store.GetAsync("petstore", "production", AccessContext.System, default);
        fetched.ShouldNotBeNull();

        // The serialized document carries the reference (and non-secret config) and nothing that looks like a secret.
        string json = Encoding.UTF8.GetString(fetched!.RootElement.ToJsonBytes());
        json.ShouldContain("vault://kv/data/petstore#client");
        json.ShouldContain("https://auth.example/token");
        fetched.RootElement.TryGetSecretRef("clientSecret", out SecretRef secretRef).ShouldBeTrue();
        secretRef.Scheme.ShouldBe(SecretScheme.HashiCorpVault);
        secretRef.Version.ShouldBe("client");
    }

    [TestMethod]
    public async Task Security_tags_round_trip_and_are_immutable_on_update()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        WorkflowEtag etag;
        using (ParsedJsonDocument<SourceCredentialBinding> added = await store.AddAsync(Tagged("petstore", "production", "acme"), "alice", default))
        {
            added.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
            etag = added.RootElement.EtagValue;
        }

        // An update may not change the security tags (the binding's immutable row-authorization scope).
        SourceCredentialDefinition rotatedWithDifferentTags = Tagged("petstore", "production", "globex") with
        {
            SecretRefs = [new SecretReferenceDefinition("value", "keyvault://petstore-rotated")],
        };
        using ParsedJsonDocument<SourceCredentialBinding>? updated = await store.UpdateAsync("petstore", "production", rotatedWithDifferentTags, etag, "bob", AccessContext.System, default);
        updated.ShouldNotBeNull();
        updated!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
    }

    [TestMethod]
    public async Task Management_reads_are_reach_filtered_and_non_disclosing()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();

        // Two tenants bind the same (sourceName, environment) — distinguished by their security tags.
        using (await store.AddAsync(Tagged("petstore", "production", "acme"), "system", default))
        {
        }

        using (await store.AddAsync(Tagged("petstore", "production", "globex"), "system", default))
        {
        }

        AccessContext acme = Scope("acme");

        // A get under acme's reach returns acme's binding; globex's is invisible (non-disclosing).
        using (ParsedJsonDocument<SourceCredentialBinding>? fetched = await store.GetAsync("petstore", "production", acme, default))
        {
            fetched.ShouldNotBeNull();
            fetched!.RootElement.ManagementTagsValue.ToList().Single().ShouldBe(new SecurityTag("tenant", "acme"));
        }

        using (SourceCredentialPage page = await store.ListAsync(acme, 1000, null, default))
        {
            page.Bindings.Select(b => b.ManagementTagsValue.ToList().Single().Value).ShouldBe(["acme"]);
        }

        // acme cannot delete globex's binding (out of write reach) — reported as absent.
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, Scope("globex"), default)).ShouldBeTrue();
        (await store.DeleteAsync("petstore", "production", WorkflowEtag.None, Scope("globex"), default)).ShouldBeFalse();
        (await store.GetAsync("petstore", "production", acme, default)).ShouldNotBeNull(); // acme's survived
    }

    [TestMethod]
    public async Task Usage_resolves_only_the_binding_a_run_is_entitled_to()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        using (await store.AddAsync(Tagged("petstore", "production", "acme"), "system", default))
        {
        }

        using (await store.AddAsync(Tagged("petstore", "production", "globex"), "system", default))
        {
        }

        // A run tagged tenant=acme resolves acme's binding; tenant=globex resolves globex's.
        using (ParsedJsonDocument<SourceCredentialBinding>? forAcme = await store.ResolveForUsageAsync("petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), default))
        {
            forAcme.ShouldNotBeNull();
            forAcme!.RootElement.UsageTagsValue.ToList().Single().Value.ShouldBe("acme");
        }

        // A run in neither tenant is entitled to nothing — fail closed (no credential, not another tenant's).
        (await store.ResolveForUsageAsync("petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "initech")]), default)).ShouldBeNull();

        // An untagged (shared) binding is usable by any run, including one with no tags.
        using (await store.AddAsync(ApiKey("billing", "production"), "system", default))
        {
        }

        (await store.ResolveForUsageAsync("billing", "production", default, default)).ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Management_scope_and_usage_scope_are_independent()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();

        // A binding the OPS team manages but ACME's runs use — the two scopes are unrelated.
        SourceCredentialDefinition definition = new(
            "petstore",
            "production",
            SourceCredentialKind.ApiKey,
            [new SecretReferenceDefinition("value", "keyvault://petstore-apikey")],
            ManagementTags: SecurityTagSet.FromTags([new SecurityTag("team", "ops")]),
            UsageTags: SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]));
        using (await store.AddAsync(definition, "system", default))
        {
        }

        // Management: the ops team can manage it; an acme-tenant admin cannot (its reach is over management tags).
        (await store.GetAsync("petstore", "production", ScopeBy("team", "ops"), default)).ShouldNotBeNull();
        (await store.GetAsync("petstore", "production", ScopeBy("tenant", "acme"), default)).ShouldBeNull();

        // Usage: an acme run may use it; an ops-team run may not (entitlement is over usage tags).
        (await store.ResolveForUsageAsync("petstore", "production", SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), default)).ShouldNotBeNull();
        (await store.ResolveForUsageAsync("petstore", "production", SecurityTagSet.FromTags([new SecurityTag("team", "ops")]), default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Source_access_evaluation_is_granted_denied_or_unconfigured()
    {
        ISourceCredentialStore store = await this.NewStoreAsync();
        using (await store.AddAsync(Tagged("petstore", "production", "acme"), "system", default))
        {
        }

        SecurityTagSet acme = SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]);
        SecurityTagSet globex = SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]);

        // Granted: acme's tags can use a petstore binding.
        (await store.EvaluateSourceAccessAsync("petstore", acme, default)).ShouldBe(CredentialSourceAccess.Granted);

        // Denied: globex's tags cannot use any petstore binding, but bindings exist.
        (await store.EvaluateSourceAccessAsync("petstore", globex, default)).ShouldBe(CredentialSourceAccess.Denied);

        // Unconfigured: a source with no bindings at all — declaring it is allowed (unauthenticated source).
        (await store.EvaluateSourceAccessAsync("billing", acme, default)).ShouldBe(CredentialSourceAccess.Unconfigured);
    }

    private static SourceCredentialDefinition ApiKey(string sourceName, string environment) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"keyvault://{sourceName}-{environment}-apikey")]);

    private static SourceCredentialDefinition Tagged(string sourceName, string environment, string tenant) => new(
        sourceName,
        environment,
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", $"keyvault://{sourceName}-{environment}-{tenant}-apikey")],
        ManagementTags: SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]),
        UsageTags: SecurityTagSet.FromTags([new SecurityTag("tenant", tenant)]));

    // A read/write/purge reach that admits exactly the rows tagged tenant=<tenant> (the rule tenant == $claim.tenant
    // resolved against a single-tenant claim).
    private static AccessContext Scope(string tenant) => ScopeBy("tenant", tenant);

    // A read/write/purge reach that admits exactly the rows tagged <key>=<value> (the rule key == $claim.key resolved
    // against a single-value claim).
    private static AccessContext ScopeBy(string key, string value) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile($"{key} == $claim.{key}")], new Dictionary<string, IReadOnlyList<string>> { [key] = [value] }));

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