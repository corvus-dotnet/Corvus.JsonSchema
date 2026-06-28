// <copyright file="PostgresEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities entitled to manage the environment and its
/// administration. Each record is stored as its <see cref="EnvironmentAdministrators"/> document in a <c>bytea</c> column,
/// keyed by EnvironmentName; its etag is held in a column for the optimistic-concurrency check. Mirrors
/// <see cref="PostgresWorkflowAdministratorStore"/> without the reverse administration index.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent; the <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its etag before writing, mirroring the other backends.
/// </remarks>
public sealed class PostgresEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresEnvironmentAdministratorStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresEnvironmentAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentAdministratorStore>(
            new PostgresEnvironmentAdministratorStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresEnvironmentAdministratorStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentAdministratorStore>(
            new PostgresEnvironmentAdministratorStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await ReadDocumentAsync(connection, environmentName, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<EnvironmentAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await ReadDocumentAsync(connection, environmentName, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();
        byte[] json;
        bool isUpdate;
        if (existing is not null)
        {
            // A record exists: parse it ONCE, NON-COPYING over the driver's array, for both the etag check and the
            // carried-forward merge. The caller must hold its current etag (None means "I expected no record").
            using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = true;
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = false;
        }

        using NpgsqlCommand write = connection.CreateCommand();
        write.CommandText = isUpdate
            ? "UPDATE EnvironmentAdministrators SET Etag = @etag, Document = @doc WHERE EnvironmentName = @id;"
            : "INSERT INTO EnvironmentAdministrators (EnvironmentName, Etag, Document) VALUES (@id, @etag, @doc);";
        write.Parameters.AddWithValue("etag", etag.Value!);
        write.Parameters.AddWithValue("doc", json);
        write.Parameters.AddWithValue("id", environmentName);
        await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
        delete.Parameters.AddWithValue("id", environmentName);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    private static async ValueTask<byte[]?> ReadDocumentAsync(NpgsqlConnection connection, string environmentName, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
        select.Parameters.AddWithValue("id", environmentName);
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
        CREATE TABLE IF NOT EXISTS EnvironmentAdministrators (
            EnvironmentName TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        """;
}