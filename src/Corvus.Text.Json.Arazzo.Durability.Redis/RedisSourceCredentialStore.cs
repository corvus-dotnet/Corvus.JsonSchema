// <copyright file="RedisSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using StackExchange.Redis;

namespace Corvus.Text.Json.Arazzo.Durability.Redis;

/// <summary>
/// A Redis-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted as their Corvus.Text.Json
/// <see cref="SourceCredentialBinding"/> documents. Each binding is stored verbatim under a per-binding key composed
/// from (SourceName, Environment, and a discriminator over its immutable management/usage tags) so tenant-/workflow-scoped
/// bindings for the same source/environment coexist; its etag travels inside the document, so optimistic concurrency is
/// a read-compare-write.
/// </summary>
/// <remarks>
/// <para>Point management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the
/// usage path by label-superset — applied in memory over the small candidate set for a (sourceName, environment),
/// enumerated through index sets the way <see cref="RedisSecurityPolicyStore"/> and
/// <see cref="RedisWorkflowCatalogStore"/> maintain theirs. List/count narrow through the store's §14.4 security-label
/// sets first — resolved server-side in one read-only Lua evaluation — so only candidate rows are ever read, and the
/// exact reach then decides each one; a re-tagging update re-points the label entries in the same atomic script as the
/// document write.</para>
/// <para>Targets a single Redis instance (or a primary): an add touches the per-binding key, three index sets and the
/// label sets, which is not Redis-Cluster slot-safe.</para>
/// </remarks>
public sealed class RedisSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private const string Prefix = "arazzo:scred:";

    // Per-binding value key: holds the SourceCredentialBinding document bytes verbatim.
    private const string BindPrefix = Prefix + "bind:";

    // Index set per (sourceName, environment): members are the binding discriminators, so the management/usage/resolve
    // paths can enumerate the small candidate set for a key.
    private const string EnvIndexPrefix = Prefix + "env:";

    // Index set per sourceName: members are "{environment}{discriminator}", so EvaluateSourceAccessAsync can
    // enumerate every binding for the source across all environments.
    private const string SourceIndexPrefix = Prefix + "src:";

    // Global index set: members are the full identity tuples "{src}{env}{discriminator}", so ListAsync can
    // enumerate every binding.
    private const string AllIndexKey = Prefix + "all";

    // The control character SourceCredentialKey uses to separate canonical tag sets; reused here to join identity parts
    // into keys/members because it cannot appear in a source name, environment or tag.
    private const char Separator = '\u0001';

    // Write the binding only if its per-binding key does not already exist, and maintain the three index sets and the
    // §14.4 label-set entries, all atomically. KEYS: bind key, env index, source index, all index.
    // ARGV: document bytes, discriminator, source-index member, all-index member, then the label-set keys (the member
    // of each is the all-index member, the same id the reach resolution yields).
    // Returns 1 on success, 0 if a binding with that identity already exists.
    private const string AddScript =
        """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('SADD', KEYS[2], ARGV[2])
        redis.call('SADD', KEYS[3], ARGV[3])
        redis.call('SADD', KEYS[4], ARGV[4])
        for i = 5, #ARGV do redis.call('SADD', ARGV[i], ARGV[4]) end
        return 1
        """;

    // Overwrite the document bytes and re-point the §14.4 label-set entries by the given diff, atomically — so a
    // re-tagging update can never leave the label index deciding by the old tags while the document carries the new.
    // KEYS: bind key. ARGV: document bytes, all-index member, added count, the added label-set keys, then the removed.
    private const string UpdateScript =
        """
        redis.call('SET', KEYS[1], ARGV[1])
        local added = tonumber(ARGV[3])
        for i = 4, 3 + added do redis.call('SADD', ARGV[i], ARGV[2]) end
        for i = 4 + added, #ARGV do redis.call('SREM', ARGV[i], ARGV[2]) end
        return 1
        """;

    // Remove the binding and prune it from the three index sets and its §14.4 label-set entries, atomically.
    // KEYS: bind key, env index, source index, all index.
    // ARGV: discriminator, source-index member, all-index member, then the label-set keys.
    private const string DeleteScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        redis.call('SREM', KEYS[3], ARGV[2])
        redis.call('SREM', KEYS[4], ARGV[3])
        for i = 4, #ARGV do redis.call('SREM', ARGV[i], ARGV[3]) end
        return 1
        """;

    private readonly IConnectionMultiplexer connection;
    private readonly IDatabase database;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsConnection;

    private RedisSourceCredentialStore(IConnectionMultiplexer connection, TimeProvider timeProvider, bool ownsConnection)
    {
        this.connection = connection;
        this.database = connection.GetDatabase();
        this.timeProvider = timeProvider;
        this.ownsConnection = ownsConnection;
    }

    /// <summary>Verifies the store can be reached; Redis needs no schema provisioning.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once connectivity is confirmed.</returns>
    public static async ValueTask PrepareAsync(string configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        await using IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
    }

    /// <summary>Opens a source credential store over the given Redis configuration.</summary>
    /// <param name="configuration">A StackExchange.Redis configuration string (e.g. <c>localhost:6379</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the connection).</returns>
    public static async ValueTask<RedisSourceCredentialStore> ConnectAsync(string configuration, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        cancellationToken.ThrowIfCancellationRequested();
        IConnectionMultiplexer connection = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new RedisSourceCredentialStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: true);
    }

    /// <summary>Creates a source credential store over an existing connection (the caller keeps ownership).</summary>
    /// <param name="connection">The Redis connection.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <returns>The store.</returns>
    public static RedisSourceCredentialStore Connect(IConnectionMultiplexer connection, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new RedisSourceCredentialStore(connection, timeProvider ?? TimeProvider.System, ownsConnection: false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);

        RedisKey[] keys =
        [
            BindKey(draft.SourceNameValue, draft.EnvironmentValue, discriminator),
            EnvIndexKey(draft.SourceNameValue, draft.EnvironmentValue),
            SourceIndexKey(draft.SourceNameValue),
            AllIndexKey,
        ];
        HashSet<string> labelSetKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.SourceCredentialLabelPrefix, draft.ManagementTagsValue);
        RedisValue[] argv =
        [
            json,
            discriminator,
            SourceIndexMember(draft.EnvironmentValue, discriminator),
            AllIndexMember(draft.SourceNameValue, draft.EnvironmentValue, discriminator),
            .. labelSetKeys.Select(static k => (RedisValue)k),
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(AddScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            ThrowHelper.ThrowSourceCredentialAlreadyExists(draft.SourceNameValue, draft.EnvironmentValue);
        }

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        (string SourceName, string Environment, string TieBreaker) cursor = (string.Empty, string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceCredentialContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        // §14.4: narrow to the candidate identities the label sets admit before reading anything. A null answer means
        // the index could not narrow and the all-index sweep runs as it always did; an empty one means no binding
        // qualifies, so the page is empty without a single doc read. With candidates in hand the all index is not even
        // consulted — a candidate id IS the identity tuple — and a stale entry resolving to a deleted row falls into
        // the existing null-value skip. The exact reach below still decides every candidate (the plan is a sound
        // over-approximation), so an imprecise plan costs throughput and can never widen reach.
        IReadOnlySet<string>? candidates = await RedisSecurityLabels.ResolveReachCandidatesAsync(this.database, RedisSecurityLabels.SourceCredentialLabelPrefix, context.Reach(AccessVerb.Read)).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return SourceCredentialPage.Create(PooledDocumentList<SourceCredentialBinding>.Empty);
        }

        // Redis has no server-side ordering for a SET, so the keyset scan runs over the candidate identity tuples in
        // memory (no documents): sort them into the stable total order (sourceName, environment, discriminator), seek
        // past the cursor, then fetch each candidate's document lazily — only up to what the page needs, never the
        // whole table.
        RedisValue[] rawMembers = candidates is null
            ? await this.database.SetMembersAsync(AllIndexKey).ConfigureAwait(false)
            : [.. candidates.Select(static s => (RedisValue)s)];
        var members = new (string Source, string Environment, string Discriminator)[rawMembers.Length];
        for (int i = 0; i < rawMembers.Length; i++)
        {
            members[i] = SplitAllIndexMember((string)rawMembers[i]!);
        }

        Array.Sort(members, ByIdentity);

        // Index of the first member strictly past the cursor in (source, environment, discriminator) order.
        int start = 0;
        if (hasCursor)
        {
            while (start < members.Length && ByIdentity.Compare(members[start], (cursor.SourceName, cursor.Environment, cursor.TieBreaker)) <= 0)
            {
                start++;
            }
        }

        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            string lastSource = string.Empty, lastEnv = string.Empty, lastDisc = string.Empty;
            for (int i = start; i < members.Length; i++)
            {
                (string src, string env, string discriminator) = members[i];
                RedisValue value = await this.database.StringGetAsync(BindKey(src, env, discriminator)).ConfigureAwait(false);
                if (value.IsNullOrEmpty)
                {
                    continue;
                }

                byte[] json = (byte[])value!;
                ParsedJsonDocument<SourceCredentialBinding> cand = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                bool kept = false;
                try
                {
                    SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tags))
                    {
                        continue;
                    }

                    if (docs.Count == pageSize)
                    {
                        // A further visible row beyond the page exists: stop and hand back a cursor at the last included row.
                        hasMore = true;
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastSource = src;
                    lastEnv = env;
                    lastDisc = discriminator;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore ? SourceCredentialPage.Create(docs, lastSource, lastEnv, lastDisc) : SourceCredentialPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The row is addressed by its frozen create-time discriminator (ADR 0067), so the per-binding key and the
        // env/source/all index sets are unchanged (the usage tags stay immutable too). A draft that supplies management
        // tags re-tags the row's reach scope (a store-level replace; an omitted set is carried forward), so the §14.4
        // label-set entries are re-pointed by the diff in the same atomic script as the document write — left stale, a
        // narrowed listing keeps deciding by the old tags and drifts from this store's own get.
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            SecurityTagSet previousTags;
            using (ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(existing))
            {
                previousTags = SecurityTagSet.CopyFrom(current.RootElement.ManagementTags);
            }

            HashSet<string> previousKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.SourceCredentialLabelPrefix, previousTags);
            HashSet<string> desiredKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.SourceCredentialLabelPrefix, draft.ManagementTagsValue);
            List<string> added = [.. desiredKeys.Where(k => !previousKeys.Contains(k))];
            List<string> removed = [.. previousKeys.Where(k => !desiredKeys.Contains(k))];
            RedisValue[] argv =
            [
                json,
                AllIndexMember(sourceName, environment, discriminator!),
                added.Count,
                .. added.Select(static k => (RedisValue)k),
                .. removed.Select(static k => (RedisValue)k),
            ];
            await this.database.ScriptEvaluateAsync(UpdateScript, [BindKey(sourceName, environment, discriminator!)], argv).ConfigureAwait(false);
        }
        else
        {
            await this.database.StringSetAsync(BindKey(sourceName, environment, discriminator!), json).ConfigureAwait(false);
        }

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        RedisKey[] keys =
        [
            BindKey(sourceName, environment, discriminator!),
            EnvIndexKey(sourceName, environment),
            SourceIndexKey(sourceName),
            AllIndexKey,
        ];

        // The label entries derive from the CURRENT doc tags (a re-tag re-pointed them in step), so the teardown
        // removes exactly the entries the row occupies.
        HashSet<string> labelSetKeys;
        using (ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(existing))
        {
            labelSetKeys = RedisSecurityLabels.SetKeysFor(RedisSecurityLabels.SourceCredentialLabelPrefix, SecurityTagSet.CopyFrom(current.RootElement.ManagementTags));
        }

        RedisValue[] argv =
        [
            discriminator!,
            SourceIndexMember(environment, discriminator!),
            AllIndexMember(sourceName, environment, discriminator!),
            .. labelSetKeys.Select(static k => (RedisValue)k),
        ];

        await this.database.ScriptEvaluateAsync(DeleteScript, keys, argv).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        cancellationToken.ThrowIfCancellationRequested();
        foreach (RedisValue member in await this.database.SetMembersAsync(EnvIndexKey(sourceName, environment)).ConfigureAwait(false))
        {
            RedisValue value = await this.database.StringGetAsync(BindKey(sourceName, environment, (string)member!)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>((byte[])value!);
            if (candidate.RootElement.IsUsableBy(runTags))
            {
                return candidate;
            }

            candidate.Dispose();
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        cancellationToken.ThrowIfCancellationRequested();
        bool any = false;
        foreach (RedisValue member in await this.database.SetMembersAsync(SourceIndexKey(sourceName)).ConfigureAwait(false))
        {
            (string env, string discriminator) = SplitSourceIndexMember((string)member!);
            RedisValue value = await this.database.StringGetAsync(BindKey(sourceName, env, discriminator)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            any = true;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>((byte[])value!);
            if (candidate.RootElement.IsUsableBy(tags))
            {
                return CredentialSourceAccess.Granted;
            }
        }

        return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsConnection)
        {
            await this.connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Orders the (unordered) all-index members into the stable total keyset order for ListAsync: the contractual
    // (sourceName, environment) primary order, made total by the discriminator tie-breaker — all compared ordinally to
    // match the token's byte order.
    private static readonly IComparer<(string Source, string Environment, string Discriminator)> ByIdentity =
        Comparer<(string Source, string Environment, string Discriminator)>.Create(static (a, b) =>
        {
            int bySource = string.CompareOrdinal(a.Source, b.Source);
            if (bySource != 0)
            {
                return bySource;
            }

            int byEnv = string.CompareOrdinal(a.Environment, b.Environment);
            return byEnv != 0 ? byEnv : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

    private static RedisKey BindKey(string sourceName, string environment, string discriminator)
        => BindPrefix + sourceName + Separator + environment + Separator + discriminator;

    private static RedisKey EnvIndexKey(string sourceName, string environment)
        => EnvIndexPrefix + sourceName + Separator + environment;

    private static RedisKey SourceIndexKey(string sourceName)
        => SourceIndexPrefix + sourceName;

    private static RedisValue SourceIndexMember(string environment, string discriminator)
        => environment + Separator + discriminator;

    private static (string Environment, string Discriminator) SplitSourceIndexMember(string member)
    {
        int sep = member.IndexOf(Separator, StringComparison.Ordinal);
        return (member[..sep], member[(sep + 1)..]);
    }

    private static RedisValue AllIndexMember(string sourceName, string environment, string discriminator)
        => sourceName + Separator + environment + Separator + discriminator;

    private static (string Source, string Environment, string Discriminator) SplitAllIndexMember(string member)
    {
        int first = member.IndexOf(Separator, StringComparison.Ordinal);
        int second = member.IndexOf(Separator, first + 1);
        return (member[..first], member[(first + 1)..second], member[(second + 1)..]);
    }

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its bytes
    // and its tag discriminator (the index member). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Discriminator)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context)
    {
        foreach (RedisValue member in await this.database.SetMembersAsync(EnvIndexKey(sourceName, environment)).ConfigureAwait(false))
        {
            string discriminator = (string)member!;
            RedisValue value = await this.database.StringGetAsync(BindKey(sourceName, environment, discriminator)).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                continue;
            }

            byte[] json = (byte[])value!;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, discriminator);
            }
        }

        return (null, null);
    }
}