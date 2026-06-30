// <copyright file="PostgresRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

using Corvus.Text.Json;

using Npgsql;
using NpgsqlTypes;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>bytea</c> column keyed by runner id, alongside a queryable <c>last_seen_at</c> column
/// used for pruning. It opens a pooled connection per operation, so it is naturally concurrent.
/// </summary>
public sealed class PostgresRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS runner_registrations (
            runner_id TEXT COLLATE "C" PRIMARY KEY NOT NULL,
            last_seen_at BIGINT NOT NULL,
            doc BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        CREATE TABLE IF NOT EXISTS runner_hosted_versions (
            runner_id TEXT COLLATE "C" NOT NULL REFERENCES runner_registrations (runner_id) ON DELETE CASCADE,
            base_workflow_id TEXT NOT NULL,
            version_number INTEGER NOT NULL,
            PRIMARY KEY (runner_id, base_workflow_id, version_number)
        );
        CREATE INDEX IF NOT EXISTS ix_runner_hosted_versions_version ON runner_hosted_versions (base_workflow_id, version_number);
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresRunnerRegistry(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the registry schema (table and index) from a connection string.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the registry schema over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<PostgresRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresRunnerRegistry>(new PostgresRunnerRegistry(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<PostgresRunnerRegistry> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresRunnerRegistry>(new PostgresRunnerRegistry(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO runner_registrations (runner_id, last_seen_at, doc)
                VALUES (@runnerId, @lastSeenAt, @doc)
                ON CONFLICT (runner_id) DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at, doc = EXCLUDED.doc;
                """;
            upsert.Parameters.AddWithValue("@runnerId", runnerId);
            upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            upsert.Parameters.AddWithValue("@doc", PersistedJson.ToArray(registration, static (Utf8JsonWriter writer, in RunnerRegistration r) => r.WriteTo(writer)));
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
        await using (NpgsqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
            clear.Parameters.AddWithValue("@runnerId", runnerId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO runner_hosted_versions (runner_id, base_workflow_id, version_number) VALUES (@runnerId, @baseWorkflowId, @versionNumber);";
            insert.Parameters.AddWithValue("@runnerId", runnerId);
            insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            insert.Parameters.AddWithValue("@versionNumber", versionNumber);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber);";
        command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        command.Parameters.AddWithValue("@versionNumber", versionNumber);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // A heartbeat advances only `last_seen_at` and the document's mirrored `lastSeenAt` field. Rather than read
        // the whole registration back and rewrite it client-side (two round-trips plus a full-payload rewrite), patch
        // both in a single statement so the indexed column and the JSON document's mirror move together and a
        // subsequent read reconstructs consistently. The document is stored as UTF-8 bytes, so it is decoded to
        // jsonb, the one field is replaced, and it is re-encoded — entirely server-side, with no document leaving the
        // database. The ISO-8601 string is supplied by the caller (round-trip "O" form, the same representation the
        // generated model emits and parses) and jsonb stores string values verbatim. The update is unconditional
        // (last-writer-wins, the intended heartbeat semantics); a zero row count means the runner is unknown.
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE runner_registrations
            SET last_seen_at = @lastSeenAt,
                doc = convert_to(jsonb_set(convert_from(doc, 'UTF8')::jsonb, '{lastSeenAt}', to_jsonb(@lastSeenIso))::text, 'UTF8')
            WHERE runner_id = @runnerId;
            """;
        update.Parameters.AddWithValue("@runnerId", runnerId);
        update.Parameters.AddWithValue("@lastSeenAt", at.ToUnixTimeMilliseconds());
        update.Parameters.AddWithValue("@lastSeenIso", at.ToString("O", CultureInfo.InvariantCulture));
        int affected = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT doc FROM runner_registrations;";
        var result = new List<RunnerRegistration>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;

        // Decode the keyset cursor from the request's page token; the runner id reifies to a string only at the Npgsql
        // TEXT-parameter boundary (a genuine DB-param leaf) — undefined token = first page.
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();

        // Native keyset page: runner_id is the PRIMARY KEY declared COLLATE "C", so the index gives a byte-ordinal order
        // (the same order the in-memory pager sorts by) and drives both the range seek and the ordering; LIMIT bounds the
        // read to one page — never a full SELECT + parse of every registration. @limit is pageSize + 1 to look one ahead.
        select.CommandText =
            """
            SELECT doc FROM runner_registrations
            WHERE (@after IS NULL OR runner_id > @after)
            ORDER BY runner_id
            LIMIT @limit;
            """;

        // Explicitly typed Text: an untyped DBNull @after (first page) leaves Npgsql unable to infer the parameter type
        // for `@after IS NULL` (error 42P08) — the same reason the runs store binds its keyset cursor as typed Text.
        select.Parameters.Add(new NpgsqlParameter("@after", NpgsqlDbType.Text) { Value = (object?)after ?? DBNull.Value });
        select.Parameters.AddWithValue("@limit", pageSize + 1);

        var page = new List<RunnerRegistration>(pageSize + 1);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            page.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        if (page.Count <= pageSize)
        {
            return RunnerRegistryPage.Create(page);
        }

        // A (pageSize+1)th row exists → there is a next page; drop it and emit the token from the last kept row's id
        // (bytes-native: base64url straight over the runner id's persisted UTF-8, no managed id string).
        page.RemoveAt(page.Count - 1);
        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ReadReach is null)
        {
            // Unrestricted read reach (e.g. the trusted system path): no row is filtered, so the bounded LIMIT keyset query
            // (one page, no per-row tag work) is exactly right — no full read, no in-memory filter.
            return await this.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);
        }

        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();

        // Reach (§14.2) is a per-row ABAC predicate, not expressible as SQL, so runner_id COLLATE "C" drives an ordered
        // range SEEK past the cursor and rows are streamed + reach-filtered in flight — the reader is consumed only until the
        // page fills, never the full SELECT + parse of every registration the in-memory fallback does. (No LIMIT: filtered-out
        // rows don't count toward the page, so the page-fill + one-row look-ahead governs how far the scan reads.)
        select.CommandText =
            """
            SELECT doc FROM runner_registrations
            WHERE (@after IS NULL OR runner_id > @after)
            ORDER BY runner_id;
            """;
        select.Parameters.Add(new NpgsqlParameter("@after", NpgsqlDbType.Text) { Value = (object?)after ?? DBNull.Value });

        var page = new List<RunnerRegistration>(pageSize);
        bool hasMore = false;
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            RunnerRegistration runner = RunnerRegistration.FromJson((byte[])reader[0]);

            // reachTags is absent on a runner serving an unscoped environment; an empty tag set fails a scoped reach
            // (fail-closed), so such a runner is invisible to a tenant-scoped caller — matching the in-memory pager.
            SecurityTagSet tags = runner.ReachTags.IsNotUndefined()
                ? SecurityTagSet.CopyFrom(runner.ReachTags)
                : SecurityTagSet.Empty;
            if (!context.Admits(AccessVerb.Read, tags))
            {
                continue;
            }

            if (page.Count == pageSize)
            {
                hasMore = true; // a further reach-visible row exists → there is a next page after the last included row
                break;
            }

            page.Add(runner);
        }

        if (!hasMore)
        {
            return RunnerRegistryPage.Create(page);
        }

        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
        command.Parameters.AddWithValue("@cutoff", deadBefore.ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the data source if this registry created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }
}