// <copyright file="PostgresAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted relationally. Each
/// request is stored as its <see cref="AccessRequest"/> schema document in a <c>bytea</c> column, with the filterable
/// fields (status, target workflow, subject) and the etag mirrored into columns for querying and the
/// optimistic-concurrency check.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class PostgresAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresAccessRequestStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresAccessRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresAccessRequestStore>(
            new PostgresAccessRequestStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresAccessRequestStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresAccessRequestStore>(
            new PostgresAccessRequestStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequestDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.BaseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(definition.SubjectClaimType);
        ArgumentException.ThrowIfNullOrEmpty(definition.SubjectClaimValue);
        ArgumentNullException.ThrowIfNull(definition.RequestedScopes);
        ArgumentOutOfRangeException.ThrowIfZero(definition.RequestedScopes.Count);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        byte[] json = AccessRequestSerialization.SerializeNew(id, definition, actor, now, etag);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO AccessRequests (Id, BaseWorkflowId, SubjectClaimType, SubjectClaimValue, Status, CreatedAt, Etag, Document) " +
            "VALUES (@id, @bw, @st, @sv, @status, @createdAt, @etag, @doc);";
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("bw", definition.BaseWorkflowId);
        insert.Parameters.AddWithValue("st", definition.SubjectClaimType);
        insert.Parameters.AddWithValue("sv", definition.SubjectClaimValue);
        insert.Parameters.AddWithValue("status", AccessRequestStatusNames.Pending);
        insert.Parameters.AddWithValue("createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("etag", etag.Value!);
        insert.Parameters.AddWithValue("doc", json);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<AccessRequest>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<AccessRequest>();
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM AccessRequests");
        var conditions = new List<string>(4);
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            select.Parameters.AddWithValue("status", AccessRequestStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            conditions.Add("BaseWorkflowId = @bw");
            select.Parameters.AddWithValue("bw", baseWorkflowId);
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            conditions.Add("SubjectClaimType = @st");
            select.Parameters.AddWithValue("st", subjectType);
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            conditions.Add("SubjectClaimValue = @sv");
            select.Parameters.AddWithValue("sv", subjectValue);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY CreatedAt, Id;");
        select.CommandText = sql.ToString();
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(PersistedJson.ToPooledDocument<AccessRequest>(reader.GetFieldValue<byte[]>(0)));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = AccessRequestSerialization.SerializeDecision(doc, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE AccessRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
        update.Parameters.AddWithValue("status", AccessRequestStatusNames.ToWire(decision.Status));
        update.Parameters.AddWithValue("etag", etag.Value!);
        update.Parameters.AddWithValue("doc", json);
        update.Parameters.AddWithValue("k", id);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<AccessRequest>(json);
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

    private static async ValueTask<byte[]?> DocumentAsync(NpgsqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AccessRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS AccessRequests (
            Id TEXT NOT NULL PRIMARY KEY,
            BaseWorkflowId TEXT NOT NULL,
            SubjectClaimType TEXT NOT NULL,
            SubjectClaimValue TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Status ON AccessRequests (Status);
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Workflow ON AccessRequests (BaseWorkflowId);
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Subject ON AccessRequests (SubjectClaimType, SubjectClaimValue);
        """;
}