// <copyright file="PostgresWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for a
/// base workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is stored as its <see cref="WorkflowAdministrators"/> document in a <c>bytea</c> column,
/// keyed by BaseWorkflowId; its etag is held in a column for the optimistic-concurrency check. The record holds
/// deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent; the <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its etag before writing, mirroring the other backends.
/// </remarks>
public sealed class PostgresWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresWorkflowAdministratorStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the schema over a caller-supplied data source.</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the data source it creates).</returns>
    public static ValueTask<PostgresWorkflowAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowAdministratorStore>(
            new PostgresWorkflowAdministratorStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresWorkflowAdministratorStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowAdministratorStore>(
            new PostgresWorkflowAdministratorStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await ReadDocumentAsync(connection, baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<WorkflowAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await ReadDocumentAsync(connection, baseWorkflowId, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();

        // Build the document to persist (and decide insert vs update) BEFORE the transaction: the etag conflict is the
        // caller's error and must throw before any write. The returned document owns its pooled buffer; current (parsed
        // non-copying over the driver array) need only outlive the synchronous serialize.
        ParsedJsonDocument<WorkflowAdministrators> result;
        bool isUpdate;
        if (existing is not null)
        {
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            result = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = true;
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            result = WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = false;
        }

        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(result.RootElement).Memory;

            // The document write and the reverse-index rewrite are atomic (design §15.4): the inbox must never observe a
            // base id indexed under a digest its current administrator set no longer holds, or vice versa.
            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await using (NpgsqlCommand write = connection.CreateCommand())
            {
                write.Transaction = transaction;
                write.CommandText = isUpdate
                    ? "UPDATE WorkflowAdministrators SET Etag = @etag, Document = @doc WHERE BaseWorkflowId = @id;"
                    : "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
                write.Parameters.AddWithValue("etag", etag.Value!);
                write.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
                write.Parameters.AddWithValue("id", baseWorkflowId);
                await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await RewriteIndexAsync(connection, transaction, baseWorkflowId, administrators, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;

        // The keyset cursor (the base id to page strictly after) reifies once here for the @after parameter — the SQL
        // leaf — never per row. The index columns are COLLATE "C" so the keyset compare is ordinal (the contract's order).
        string? after = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = after is null
            ? "SELECT BaseWorkflowId FROM WorkflowAdministratorIndex WHERE AdminDigest = @digest ORDER BY BaseWorkflowId LIMIT @n;"
            : "SELECT BaseWorkflowId FROM WorkflowAdministratorIndex WHERE AdminDigest = @digest AND BaseWorkflowId > @after ORDER BY BaseWorkflowId LIMIT @n;";
        select.Parameters.AddWithValue("digest", adminDigest);
        select.Parameters.AddWithValue("n", pageSize + 1);
        if (after is not null)
        {
            select.Parameters.AddWithValue("after", after);
        }

        var rows = new List<string>(pageSize + 1);
        await using (NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(reader.GetString(0));
            }
        }

        return WorkflowAdministeredPaging.ToPage(rows, pageSize);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Rewrites this base id's reverse-index rows within the write transaction (§15.4): retract the stale digests, then
    // index the current ones. The administrator set is small, so a delete-all-then-insert is simplest and correct.
    private static async ValueTask RewriteIndexAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, CancellationToken cancellationToken)
    {
        await using (NpgsqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM WorkflowAdministratorIndex WHERE BaseWorkflowId = @id;";
            clear.Parameters.AddWithValue("id", baseWorkflowId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (string digest in WorkflowAdministeredPaging.DistinctDigests(administrators))
        {
            await using NpgsqlCommand index = connection.CreateCommand();
            index.Transaction = transaction;
            index.CommandText = "INSERT INTO WorkflowAdministratorIndex (AdminDigest, BaseWorkflowId) VALUES (@digest, @id);";
            index.Parameters.AddWithValue("digest", digest);
            index.Parameters.AddWithValue("id", baseWorkflowId);
            await index.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> ReadDocumentAsync(NpgsqlConnection connection, string baseWorkflowId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkflowAdministrators WHERE BaseWorkflowId = @id;";
        select.Parameters.AddWithValue("id", baseWorkflowId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkflowAdministrators (
            BaseWorkflowId TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        CREATE TABLE IF NOT EXISTS WorkflowAdministratorIndex (
            AdminDigest TEXT COLLATE "C" NOT NULL,
            BaseWorkflowId TEXT COLLATE "C" NOT NULL,
            PRIMARY KEY (AdminDigest, BaseWorkflowId)
        );
        """;
}