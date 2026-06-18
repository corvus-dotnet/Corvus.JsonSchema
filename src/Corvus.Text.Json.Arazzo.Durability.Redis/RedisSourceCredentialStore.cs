// <copyright file="RedisSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
/// <para>Redis has no server-side filtering, so management reads/writes are reach-filtered by the caller's
/// <see cref="AccessContext"/> (§14.2) and the usage path by label-superset — applied in memory over the small candidate
/// set for a (sourceName, environment), enumerated through index sets the way
/// <see cref="RedisSecurityPolicyStore"/> and <see cref="RedisWorkflowCatalogStore"/> maintain theirs.</para>
/// <para>Targets a single Redis instance (or a primary): an add touches the per-binding key and three index sets, which
/// is not Redis-Cluster slot-safe.</para>
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

    // Write the binding only if its per-binding key does not already exist, and maintain the three index sets, all
    // atomically. KEYS: bind key, env index, source index, all index.
    // ARGV: document bytes, discriminator, source-index member, all-index member.
    // Returns 1 on success, 0 if a binding with that identity already exists.
    private const string AddScript =
        """
        if redis.call('EXISTS', KEYS[1]) == 1 then return 0 end
        redis.call('SET', KEYS[1], ARGV[1])
        redis.call('SADD', KEYS[2], ARGV[2])
        redis.call('SADD', KEYS[3], ARGV[3])
        redis.call('SADD', KEYS[4], ARGV[4])
        return 1
        """;

    // Remove the binding and prune it from the three index sets, atomically. KEYS: bind key, env index, source index,
    // all index. ARGV: discriminator, source-index member, all-index member.
    private const string DeleteScript =
        """
        redis.call('DEL', KEYS[1])
        redis.call('SREM', KEYS[2], ARGV[1])
        redis.call('SREM', KEYS[3], ARGV[2])
        redis.call('SREM', KEYS[4], ARGV[3])
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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        cancellationToken.ThrowIfCancellationRequested();
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.Discriminator(definition.ManagementTags, definition.UsageTags);

        RedisKey[] keys =
        [
            BindKey(definition.SourceName, definition.Environment, discriminator),
            EnvIndexKey(definition.SourceName, definition.Environment),
            SourceIndexKey(definition.SourceName),
            AllIndexKey,
        ];
        RedisValue[] argv =
        [
            json,
            discriminator,
            SourceIndexMember(definition.Environment, discriminator),
            AllIndexMember(definition.SourceName, definition.Environment, discriminator),
        ];

        RedisResult result = await this.database.ScriptEvaluateAsync(AddScript, keys, argv).ConfigureAwait(false);
        if ((long)result == 0)
        {
            throw new InvalidOperationException($"A source credential binding for '{definition.SourceName}@{definition.Environment}' with those security tags already exists.");
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
    public async ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        int pageSize = limit > 0 ? limit : 1;
        bool hasCursor = SourceCredentialContinuationToken.TryDecode(pageToken, out (string SourceName, string Environment, string TieBreaker) cursor);

        // Redis has no server-side ordering for a SET, so the keyset scan runs over the all-index members in memory: the
        // members are the small identity tuples (no documents), so sort them into the stable total order
        // (sourceName, environment, discriminator), seek past the cursor, then fetch each candidate's document lazily —
        // only up to what the page needs, never the whole table.
        RedisValue[] rawMembers = await this.database.SetMembersAsync(AllIndexKey).ConfigureAwait(false);
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
        string? nextToken = null;
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
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                if (!context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                {
                    continue;
                }

                if (docs.Count == pageSize)
                {
                    // A further visible row beyond the page exists: stop and hand back a cursor at the last included row.
                    nextToken = SourceCredentialContinuationToken.Encode(lastSource, lastEnv, lastDisc);
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
                lastSource = src;
                lastEnv = env;
                lastDisc = discriminator;
            }

            return new SourceCredentialPage(docs, nextToken);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());

        // The identity (sourceName, environment, security tags) is immutable, so the per-binding key is unchanged and
        // the index sets need no maintenance; just overwrite the document bytes verbatim.
        await this.database.StringSetAsync(BindKey(sourceName, environment, discriminator!), json).ConfigureAwait(false);
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
        RedisValue[] argv =
        [
            discriminator!,
            SourceIndexMember(environment, discriminator!),
            AllIndexMember(sourceName, environment, discriminator!),
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