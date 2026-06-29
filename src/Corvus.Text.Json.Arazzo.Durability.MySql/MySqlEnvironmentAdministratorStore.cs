// <copyright file="MySqlEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities entitled to manage administration. Each record is
/// stored as its <see cref="EnvironmentAdministrators"/> document in a <c>LONGBLOB</c> column, keyed by EnvironmentName;
/// its etag is held in a column for the optimistic-concurrency check. A companion reverse administration index (design §7.8)
/// maps each administrator digest to the environments it administers, powering the promotion approver inbox. Mirrors
/// <see cref="MySqlWorkflowAdministratorStore"/>. The record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent; the <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its etag before writing, mirroring the other backends.
/// </remarks>
public sealed class MySqlEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlEnvironmentAdministratorStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A MySqlConnector connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the schema over a caller-supplied data source.</summary>
    /// <param name="dataSource">A MySqlConnector data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using MySqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the data source it creates).</returns>
    public static ValueTask<MySqlEnvironmentAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlEnvironmentAdministratorStore>(
            new MySqlEnvironmentAdministratorStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlEnvironmentAdministratorStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlEnvironmentAdministratorStore>(
            new MySqlEnvironmentAdministratorStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
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

        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
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
            // No record yet: materialization is only valid against the None etag.
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = false;
        }

        // The document write and the reverse-index rewrite are atomic (design §7.8): the inbox must never observe an
        // environment indexed under a digest its current administrator set no longer holds, or vice versa.
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (MySqlCommand write = connection.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = isUpdate
                ? "UPDATE EnvironmentAdministrators SET Etag = @etag, Document = @doc WHERE EnvironmentName = @id;"
                : "INSERT INTO EnvironmentAdministrators (EnvironmentName, Etag, Document) VALUES (@id, @etag, @doc);";
            write.Parameters.AddWithValue("@id", environmentName);
            write.Parameters.AddWithValue("@etag", etag.Value!);
            write.Parameters.AddWithValue("@doc", json);
            await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await RewriteIndexAsync(connection, transaction, environmentName, administrators, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // The document delete and the reverse-index retraction are atomic (design §7.8): the inbox must never observe an
        // environment still indexed under a digest after its record is gone. Deleting a missing record is a harmless no-op
        // (both deletes affect zero rows), preserving the contract's "delete of an absent environment is a no-op" semantics.
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (MySqlCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
            delete.Parameters.AddWithValue("@id", environmentName);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (MySqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM EnvironmentAdministratorIndex WHERE EnvironmentName = @id;";
            clear.Parameters.AddWithValue("@id", environmentName);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;

        // The keyset cursor (the environment name to page strictly after) reifies once here for the @after parameter — the
        // SQL leaf — never per row. The index columns are utf8mb4_bin so the keyset compare is binary (ordinal; the contract's order).
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = after is null
            ? "SELECT EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest ORDER BY EnvironmentName LIMIT @n;"
            : "SELECT EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest AND EnvironmentName > @after ORDER BY EnvironmentName LIMIT @n;";
        select.Parameters.AddWithValue("@digest", adminDigest);
        select.Parameters.AddWithValue("@n", pageSize + 1);
        if (after is not null)
        {
            select.Parameters.AddWithValue("@after", after);
        }

        var rows = new List<string>(pageSize + 1);
        await using (MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(reader.GetString(0));
            }
        }

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Rewrites this environment's reverse-index rows within the write transaction (§7.8): retract the stale digests, then
    // index the current ones. The administrator set is small, so a delete-all-then-insert is simplest and correct.
    private static async ValueTask RewriteIndexAsync(MySqlConnection connection, MySqlTransaction transaction, string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, CancellationToken cancellationToken)
    {
        await using (MySqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM EnvironmentAdministratorIndex WHERE EnvironmentName = @id;";
            clear.Parameters.AddWithValue("@id", environmentName);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (string digest in EnvironmentAdministeredPaging.DistinctDigests(administrators))
        {
            await using MySqlCommand index = connection.CreateCommand();
            index.Transaction = transaction;
            index.CommandText = "INSERT INTO EnvironmentAdministratorIndex (AdminDigest, EnvironmentName) VALUES (@digest, @id);";
            index.Parameters.AddWithValue("@digest", digest);
            index.Parameters.AddWithValue("@id", environmentName);
            await index.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> ReadDocumentAsync(MySqlConnection connection, string environmentName, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
        select.Parameters.AddWithValue("@id", environmentName);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private static async ValueTask ProvisionAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS EnvironmentAdministrators (
            EnvironmentName VARCHAR(255) NOT NULL PRIMARY KEY,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS EnvironmentAdministratorIndex (
            AdminDigest VARCHAR(64) COLLATE utf8mb4_bin NOT NULL,
            EnvironmentName VARCHAR(255) COLLATE utf8mb4_bin NOT NULL,
            PRIMARY KEY (AdminDigest, EnvironmentName)
        );
        """;
}