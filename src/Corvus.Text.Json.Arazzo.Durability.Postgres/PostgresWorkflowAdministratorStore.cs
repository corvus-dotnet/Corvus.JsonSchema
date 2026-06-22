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
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
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
        if (existing is not null)
        {
            // Parse the existing record ONCE, NON-COPYING over the driver's array (the read leaf) — used for both the etag
            // check and the carried-forward merge. Serialize the result into the pooled buffer the returned document owns and
            // bind its exact bytes via ReadOnlyMemory (no GC document array, no second copy); the column etag is the one we
            // just generated.
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());

            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            ParsedJsonDocument<WorkflowAdministrators> updated = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            try
            {
                ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
                await using NpgsqlCommand update = connection.CreateCommand();
                update.CommandText = "UPDATE WorkflowAdministrators SET Etag = @etag, Document = @doc WHERE BaseWorkflowId = @id;";
                update.Parameters.AddWithValue("etag", etag.Value!);
                update.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
                update.Parameters.AddWithValue("id", baseWorkflowId);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return updated;
            }
            catch
            {
                updated.Dispose();
                throw;
            }
        }

        // No record yet: materialization is only valid against the None etag (the v1-derived default).
        if (!expectedEtag.IsNone)
        {
            throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
        }

        ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
            insert.Parameters.AddWithValue("id", baseWorkflowId);
            insert.Parameters.AddWithValue("etag", etag.Value!);
            insert.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
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
        """;
}