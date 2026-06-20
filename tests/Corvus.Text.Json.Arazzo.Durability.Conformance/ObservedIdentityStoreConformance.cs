// <copyright file="ObservedIdentityStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IObservedIdentityStore"/> must satisfy (design §16.5.4): a sighting upserts by
/// (subjectKind, subjectValue) — preserving first-seen, bumping last-seen, unioning provenance — and the prefix search
/// is an indexed keyset page in <c>(subjectValue, subjectKind)</c> order with no gaps or duplicates across boundaries. A
/// backend's test project derives a concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateStoreAsync"/>;
/// the in-memory store is the reference implementation and runs the same suite.
/// </summary>
public abstract class ObservedIdentityStoreConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty store backed by the implementation under test.</summary>
    /// <param name="timeProvider">The time source the store must use for sighting timestamps.</param>
    /// <returns>The store.</returns>
    protected abstract ValueTask<IObservedIdentityStore> CreateStoreAsync(TimeProvider timeProvider);

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
    public async Task A_sighting_round_trips_through_seen_and_search()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme"), new SecurityTag("sys:sub", "alice")]);
        await store.SeenAsync(Person, Str("alice"), Str("Alice Smith"), identity, true, "accessRequest", default);

        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, Person, Str("ali"), 10, null, default);
        ObservedIdentity o = page.Identities.Single();
        o.SubjectKindValue.ShouldBe("person");
        o.SubjectValueValue.ShouldBe("alice");
        o.LabelOrNull.ShouldBe("Alice Smith");
        o.IdentityTagsValue.ToList().ShouldBe(identity.ToList(), ignoreOrder: true);
        o.CompleteValue.ShouldBeTrue();
        Provenance(o).ShouldBe(["accessRequest"]);
        page.NextPageToken.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_re_sighting_upserts_one_record_and_unions_provenance()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);
        await store.SeenAsync(Team, Str("acme"), Str("Acme"), identity, true, "catalogVersion", default);
        await store.SeenAsync(Team, Str("acme"), Str("Acme Corp"), identity, true, "administrator", default);

        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, Team, Str("acme"), 10, null, default);
        ObservedIdentity o = page.Identities.Single(); // one record, not two
        o.LabelOrNull.ShouldBe("Acme Corp"); // refreshed by the later sighting
        Provenance(o).ShouldBe(["catalogVersion", "administrator"], ignoreOrder: true);
        o.FirstSeenAtValue.ShouldBeLessThanOrEqualTo(o.LastSeenAtValue);
    }

    [TestMethod]
    public async Task Search_filters_by_prefix_and_kind()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet id = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);
        await store.SeenAsync(Person, Str("alice"), default, id, true, "x", default);
        await store.SeenAsync(Person, Str("albert"), default, id, true, "x", default);
        await store.SeenAsync(Team, Str("alpha"), default, id, true, "x", default);
        await store.SeenAsync(Person, Str("bob"), default, id, true, "x", default);

        using (ObservedIdentityPage all = await store.SearchAsync(AccessContext.System, AllKinds, Str("al"), 10, null, default))
        {
            all.Identities.Count.ShouldBe(3); // alice, albert, alpha
        }

        using (ObservedIdentityPage people = await store.SearchAsync(AccessContext.System, Person, Str("al"), 10, null, default))
        {
            people.Identities.Count.ShouldBe(2); // alice, albert
        }

        using (ObservedIdentityPage teams = await store.SearchAsync(AccessContext.System, Team, Str(string.Empty), 10, null, default))
        {
            teams.Identities.Single().SubjectValueValue.ShouldBe("alpha"); // empty prefix matches all of the kind
        }
    }

    [TestMethod]
    public async Task Search_keyset_pages_in_value_kind_order_without_gaps_or_duplicates()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet id = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);
        (ObservedIdentity.GranteeKind Kind, string Value)[] keys =
        [
            (Person, "delta"), (Person, "alpha"), (Team, "alpha"), (Person, "echo"),
            (Person, "bravo"), (Role, "charlie"), (Person, "foxtrot"), (Team, "golf"),
        ];

        // Add out of order, to prove the store (not insertion order) establishes the page order, and that the
        // (subjectValue, subjectKind) tie-breaker separates the two "alpha" rows deterministically.
        foreach ((ObservedIdentity.GranteeKind kind, string value) in keys.OrderByDescending(k => k.Value, StringComparer.Ordinal).ThenByDescending(k => k.Kind.ToToken(), StringComparer.Ordinal))
        {
            await store.SeenAsync(kind, Str(value), default, id, true, "x", default);
        }

        var seen = new List<string>();
        string? token = null;
        int pages = 0;
        do
        {
            using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 3, token, default);
            page.Identities.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (ObservedIdentity o in page.Identities)
            {
                seen.Add($"{o.SubjectValueValue}/{o.SubjectKindValue}");
            }

            token = page.NextPageToken;
            pages++;
        }
        while (token is not null);

        // 8 items, 3 per page → 3 pages; no duplicates or gaps; contractual (subjectValue, subjectKind) order.
        pages.ShouldBe(3);
        string[] expected = keys
            .OrderBy(k => k.Value, StringComparer.Ordinal).ThenBy(k => k.Kind.ToToken(), StringComparer.Ordinal)
            .Select(k => $"{k.Value}/{k.Kind.ToToken()}").ToArray();
        seen.ShouldBe(expected);

        // A malformed token is rejected (rather than silently restarting).
        await Should.ThrowAsync<FormatException>(async () =>
        {
            using ObservedIdentityPage bad = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 3, "this~is~not~a~token", default);
        });
    }

    [TestMethod]
    public async Task Search_is_reach_filtered_to_the_callers_read_reach()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        await store.SeenAsync(Team, Str("acme"), Str("Acme"), SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), true, "test", default);
        await store.SeenAsync(Team, Str("globex"), Str("Globex"), SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]), true, "test", default);

        // A caller reach-scoped to tenant=acme discovers acme's identity only; globex's is invisible (non-disclosing, §17.1).
        using (ObservedIdentityPage acme = await store.SearchAsync(ScopeBy("tenant", "acme"), AllKinds, Str(string.Empty), 10, null, default))
        {
            acme.Identities.Single().SubjectValueValue.ShouldBe("acme");
        }

        // System reach sees both.
        using (ObservedIdentityPage all = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 10, null, default))
        {
            all.Identities.Count.ShouldBe(2);
        }
    }

    [TestMethod]
    public async Task FindIdentityConflict_detects_a_different_grantee_with_a_set_equal_identity()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet alice = SecurityTagSet.FromTags([new SecurityTag("sys:iss", "ldap"), new SecurityTag("sys:sub", "alice")]);
        await store.SeenAsync(Person, Str("alice"), Str("Alice"), alice, true, "administrator", default);

        // A DIFFERENT grantee value resolving to the same identity (tags supplied in a different order, to prove the match
        // is by set-equality, not byte-equality) is a collision the authoring path must refuse.
        SecurityTagSet sameIdentityReordered = SecurityTagSet.FromTags([new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:iss", "ldap")]);
        using ParsedJsonDocument<ObservedIdentity>? conflict = await store.FindIdentityConflictAsync(Person, Str("alice-2"), sameIdentityReordered, default);
        conflict.ShouldNotBeNull();
        conflict!.RootElement.SubjectKindValue.ShouldBe("person");
        conflict.RootElement.SubjectValueValue.ShouldBe("alice");

        // The SAME grantee is not in conflict with itself; a genuinely distinct identity has no conflict.
        (await store.FindIdentityConflictAsync(Person, Str("alice"), alice, default)).ShouldBeNull();
        (await store.FindIdentityConflictAsync(Person, Str("bob"), SecurityTagSet.FromTags([new SecurityTag("sys:iss", "ldap"), new SecurityTag("sys:sub", "bob")]), default)).ShouldBeNull();

        // The empty (unscoped) identity never collides, even though many grantees may map to it.
        await store.SeenAsync(Role, Str("system"), default, SecurityTagSet.Empty, true, "x", default);
        (await store.FindIdentityConflictAsync(Role, Str("other"), SecurityTagSet.Empty, default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task FindIdentityConflict_follows_a_re_sighting_that_changes_the_identity()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet original = SecurityTagSet.FromTags([new SecurityTag("sys:iss", "ldap"), new SecurityTag("sys:sub", "old")]);
        SecurityTagSet updated = SecurityTagSet.FromTags([new SecurityTag("sys:iss", "ldap"), new SecurityTag("sys:sub", "new")]);
        await store.SeenAsync(Person, Str("carol"), default, original, true, "x", default);
        await store.SeenAsync(Person, Str("carol"), default, updated, true, "x", default); // the identity changed on re-sighting

        // The retired identity no longer conflicts (the digest index followed the re-sighting); the current one does.
        (await store.FindIdentityConflictAsync(Person, Str("carol-2"), original, default)).ShouldBeNull();
        using ParsedJsonDocument<ObservedIdentity>? current = await store.FindIdentityConflictAsync(Person, Str("carol-2"), updated, default);
        current!.RootElement.SubjectValueValue.ShouldBe("carol");
    }

    // The seam carries the grantee kind as its JSON value (ObservedIdentity.GranteeKind); these are the enum constants the
    // tests pass, with AllKinds (undefined) meaning "all kinds" to a search.
    private static readonly ObservedIdentity.GranteeKind Person = ObservedIdentity.GranteeKind.EnumValues.Person;
    private static readonly ObservedIdentity.GranteeKind Team = ObservedIdentity.GranteeKind.EnumValues.Team;
    private static readonly ObservedIdentity.GranteeKind Role = ObservedIdentity.GranteeKind.EnumValues.Role;
    private static readonly ObservedIdentity.GranteeKind AllKinds = default;

    // The seam carries the JSON value (the store reifies only at its own key leaf); a test builds one from a managed
    // string by parsing the JSON string literal (the test values contain no characters needing escaping).
    private static JsonString Str(string value) => JsonString.ParseValue($"\"{value}\"");

    private static AccessContext ScopeBy(string key, string value) => AccessContext.Uniform(
        new SecurityFilter([SecurityRule.Compile($"{key} == $claim.{key}")], new Dictionary<string, IReadOnlyList<string>> { [key] = [value] }));

    private static List<string> Provenance(ObservedIdentity o)
    {
        var list = new List<string>();
        if (o.Provenance.IsNotUndefined())
        {
            foreach (JsonString source in o.Provenance.EnumerateArray())
            {
                list.Add((string)source);
            }
        }

        return list;
    }

    private async ValueTask<IObservedIdentityStore> NewStoreAsync(TimeProvider? timeProvider = null)
    {
        IObservedIdentityStore store = await this.CreateStoreAsync(timeProvider ?? TimeProvider.System);
        if (store is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return store;
    }
}