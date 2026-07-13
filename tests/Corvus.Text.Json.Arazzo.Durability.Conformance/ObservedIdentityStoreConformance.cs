// <copyright file="ObservedIdentityStoreConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using Corvus.Text.Json;
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

        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, Person, Str("ali"), 10, default, default);
        ObservedIdentity o = page.Identities.Single();
        o.SubjectKindValue.ShouldBe("person");
        o.SubjectValueValue.ShouldBe("alice");
        o.LabelOrNull.ShouldBe("Alice Smith");
        o.IdentityTagsValue.ToList().ShouldBe(identity.ToList(), ignoreOrder: true);
        o.CompleteValue.ShouldBeTrue();
        Provenance(o).ShouldBe(["accessRequest"]);
        page.NextPageToken.IsEmpty.ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_re_sighting_upserts_one_record_and_unions_provenance()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);
        await store.SeenAsync(Team, Str("acme"), Str("Acme"), identity, true, "catalogVersion", default);
        await store.SeenAsync(Team, Str("acme"), Str("Acme Corp"), identity, true, "administrator", default);

        using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, Team, Str("acme"), 10, default, default);
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

        using (ObservedIdentityPage all = await store.SearchAsync(AccessContext.System, AllKinds, Str("al"), 10, default, default))
        {
            all.Identities.Count.ShouldBe(3); // alice, albert, alpha
        }

        using (ObservedIdentityPage people = await store.SearchAsync(AccessContext.System, Person, Str("al"), 10, default, default))
        {
            people.Identities.Count.ShouldBe(2); // alice, albert
        }

        using (ObservedIdentityPage teams = await store.SearchAsync(AccessContext.System, Team, Str(string.Empty), 10, default, default))
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

        // Walk every page via the continuation token; round-trip it through the JsonString seam exactly as the HTTP layer
        // does (the store emits it as UTF-8, the next request carries it as a JSON string). The page owns the token buffer
        // (freed on dispose), so copy it out per page.
        var seen = new List<string>();
        byte[]? token = null;
        int pages = 0;
        do
        {
            using ParsedJsonDocument<JsonString>? tokenDoc = token is null ? null : AsPageToken(token);
            using ObservedIdentityPage page = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 3, tokenDoc?.RootElement ?? default, default);
            page.Identities.Count.ShouldBeLessThanOrEqualTo(3);
            foreach (ObservedIdentity o in page.Identities)
            {
                seen.Add($"{o.SubjectValueValue}/{o.SubjectKindValue}");
            }

            token = page.NextPageToken.IsEmpty ? null : page.NextPageToken.ToArray();
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
            using ParsedJsonDocument<JsonString> badToken = AsPageToken("this~is~not~a~token"u8);
            using ObservedIdentityPage bad = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 3, badToken.RootElement, default);
        });
    }

    // Wraps an opaque page token's UTF-8 as the JSON string value a request carries it as — the conformance feeds a
    // previous page's NextPageToken (the store's emitted bytes) back through the JsonString seam, mirroring HTTP.
    private static ParsedJsonDocument<JsonString> AsPageToken(ReadOnlySpan<byte> tokenUtf8)
    {
        byte[] quoted = new byte[tokenUtf8.Length + 2];
        quoted[0] = (byte)'"';
        tokenUtf8.CopyTo(quoted.AsSpan(1));
        quoted[^1] = (byte)'"';
        return ParsedJsonDocument<JsonString>.Parse(quoted);
    }

    [TestMethod]
    public async Task Search_is_reach_filtered_to_the_callers_read_reach()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();
        await store.SeenAsync(Team, Str("acme"), Str("Acme"), SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]), true, "test", default);
        await store.SeenAsync(Team, Str("globex"), Str("Globex"), SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]), true, "test", default);

        // A caller reach-scoped to tenant=acme discovers acme's identity only; globex's is invisible (non-disclosing, §17.1).
        using (ObservedIdentityPage acme = await store.SearchAsync(ScopeBy("tenant", "acme"), AllKinds, Str(string.Empty), 10, default, default))
        {
            acme.Identities.Single().SubjectValueValue.ShouldBe("acme");
        }

        // System reach sees both.
        using (ObservedIdentityPage all = await store.SearchAsync(AccessContext.System, AllKinds, Str(string.Empty), 10, default, default))
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

    [TestMethod]
    public async Task FindBroadeningOverlaps_surfaces_grantees_whose_identity_strictly_contains_the_grant()
    {
        IObservedIdentityStore store = await this.NewStoreAsync();

        // An existing PERSON grantee with a rich identity {group=admins, sub=alice}, and a TEAM grantee whose identity is
        // exactly {group=admins}.
        SecurityTagSet alice = SecurityTagSet.FromTags([new SecurityTag("sys:group", "admins"), new SecurityTag("sys:sub", "alice")]);
        SecurityTagSet adminsGroup = SecurityTagSet.FromTags([new SecurityTag("sys:group", "admins")]);
        await store.SeenAsync(Person, Str("alice"), Str("Alice"), alice, true, "administrator", default);
        await store.SeenAsync(Team, Str("admins"), Str("Admins"), adminsGroup, true, "administrator", default);

        // Granting the narrower identity {group=admins} broadens administration to also admit alice (whose identity
        // strictly contains it) and everyone else in group=admins — a cross-kind subset overlap the probe surfaces. The
        // team "admins" holds exactly {group=admins} (set-equal, not a strict superset), so it is the FindIdentityConflict
        // case and is excluded from the broadening overlap.
        using (ObservedIdentityPage page = await store.FindBroadeningOverlapsAsync(Team, Str("new-admins"), adminsGroup, 10, default))
        {
            page.Identities.Count.ShouldBe(1);
            page.Identities.Single().SubjectKindValue.ShouldBe("person");
            page.Identities.Single().SubjectValueValue.ShouldBe("alice");
        }

        // The REDUNDANT direction is not a broadening overlap: granting a broader (more specific) identity that an existing
        // grantee is a subset of does not broaden anything, so nothing is surfaced.
        SecurityTagSet alicePlus = SecurityTagSet.FromTags([new SecurityTag("sys:group", "admins"), new SecurityTag("sys:sub", "alice"), new SecurityTag("sys:iss", "ldap")]);
        using (ObservedIdentityPage page = await store.FindBroadeningOverlapsAsync(Person, Str("alice-2"), alicePlus, 10, default))
        {
            page.Identities.Count.ShouldBe(0);
        }

        // A grant that has no strict superset among the grantees (a distinct, unrelated identity) surfaces nothing.
        SecurityTagSet other = SecurityTagSet.FromTags([new SecurityTag("sys:group", "billing")]);
        using (ObservedIdentityPage page = await store.FindBroadeningOverlapsAsync(Team, Str("billing"), other, 10, default))
        {
            page.Identities.Count.ShouldBe(0);
        }

        // The empty (unscoped) identity is a subset of everything but grants no administration scope, so it broadens
        // nothing and never warns.
        using (ObservedIdentityPage page = await store.FindBroadeningOverlapsAsync(Team, Str("any"), SecurityTagSet.Empty, 10, default))
        {
            page.Identities.Count.ShouldBe(0);
        }
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