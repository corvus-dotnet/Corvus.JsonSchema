// <copyright file="MySqlAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow versions are
/// available in which environments) persisted relationally. Each entry is stored as its <see cref="AvailabilityEntry"/>
/// document in a <c>LONGBLOB</c> column, keyed by (BaseWorkflowId, VersionNumber, Environment). Availability has no
/// mutable state and carries no security tags — an entry is created (idempotently) to make a version available and deleted
/// to withdraw it; authorization and readiness are the control-plane surface's concern.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent (no held-connection gate). The two
/// list axes are indexed keyset range scans: by-version orders by Environment (the primary key prefix already covers it);
/// by-environment orders by (BaseWorkflowId, VersionNumber) over a secondary index.
/// </remarks>
public sealed class MySqlAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlAvailabilityStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlAvailabilityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlAvailabilityStore>(
            new MySqlAvailabilityStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlAvailabilityStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlAvailabilityStore>(
            new MySqlAvailabilityStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
        byte[]? existing = await ReadDocumentAsync(connection, baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(existing), false);
        }

        using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
        byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using MySqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Availability (BaseWorkflowId, VersionNumber, Environment, Document) VALUES (@b, @v, @e, @doc);";
        insert.Parameters.AddWithValue("@b", baseWorkflowId);
        insert.Parameters.AddWithValue("@v", versionNumber);
        insert.Parameters.AddWithValue("@e", environment);
        insert.Parameters.AddWithValue("@doc", json);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await ReadDocumentAsync(connection, baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<AvailabilityEntry>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment = @e;";
        delete.Parameters.AddWithValue("@b", baseWorkflowId);
        delete.Parameters.AddWithValue("@v", versionNumber);
        delete.Parameters.AddWithValue("@e", environment);
        return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = hasCursor
            ? "SELECT Environment, Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment > @ce ORDER BY Environment;"
            : "SELECT Environment, Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v ORDER BY Environment;";
        select.Parameters.AddWithValue("@b", baseWorkflowId);
        select.Parameters.AddWithValue("@v", versionNumber);
        if (hasCursor)
        {
            select.Parameters.AddWithValue("@ce", cursor.Environment);
        }

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            bool hasMore = false;
            string lastEnvironment = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                lastEnvironment = reader.GetString(0);
                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(reader.GetFieldValue<byte[]>(1)));
            }

            return hasMore
                ? AvailabilityPage.Create(docs, baseWorkflowId, versionNumber, lastEnvironment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = hasCursor
            ? "SELECT BaseWorkflowId, VersionNumber, Document FROM Availability WHERE Environment = @e AND (BaseWorkflowId > @cb OR (BaseWorkflowId = @cb AND VersionNumber > @cv)) ORDER BY BaseWorkflowId, VersionNumber;"
            : "SELECT BaseWorkflowId, VersionNumber, Document FROM Availability WHERE Environment = @e ORDER BY BaseWorkflowId, VersionNumber;";
        select.Parameters.AddWithValue("@e", environment);
        if (hasCursor)
        {
            select.Parameters.AddWithValue("@cb", cursor.BaseWorkflowId);
            select.Parameters.AddWithValue("@cv", cursor.VersionNumber);
        }

        var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
        try
        {
            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            bool hasMore = false;
            string lastBaseWorkflowId = string.Empty;
            int lastVersionNumber = 0;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                lastBaseWorkflowId = reader.GetString(0);
                lastVersionNumber = reader.GetInt32(1);
                docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(reader.GetFieldValue<byte[]>(2)));
            }

            return hasMore
                ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, environment)
                : AvailabilityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
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

    private static async ValueTask ProvisionAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    private static async ValueTask<byte[]?> ReadDocumentAsync(MySqlConnection connection, string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment = @e;";
        select.Parameters.AddWithValue("@b", baseWorkflowId);
        select.Parameters.AddWithValue("@v", versionNumber);
        select.Parameters.AddWithValue("@e", environment);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Availability (
            BaseWorkflowId VARCHAR(255) NOT NULL,
            VersionNumber INT NOT NULL,
            Environment VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            PRIMARY KEY (BaseWorkflowId, VersionNumber, Environment),
            INDEX IX_Availability_Environment (Environment, BaseWorkflowId, VersionNumber)
        );
        """;
}