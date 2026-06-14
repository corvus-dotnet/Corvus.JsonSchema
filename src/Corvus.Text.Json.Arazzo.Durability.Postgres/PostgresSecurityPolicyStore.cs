// <copyright file="PostgresSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2) persisted relationally. Each record is stored as its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) in a <c>bytea</c> column, with its
/// etag in a column for the optimistic-concurrency check; a single-row meta table holds the monotonic generation a
/// resolver caches against.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class PostgresSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresSecurityPolicyStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresSecurityPolicyStore>(
            new PostgresSecurityPolicyStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresSecurityPolicyStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresSecurityPolicyStore>(
            new PostgresSecurityPolicyStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        var record = SecurityRuleDocument.CreateRule(name, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO SecurityRules (Name, Etag, Document) VALUES (@name, @etag, @doc);";
        insert.Parameters.AddWithValue("name", name);
        insert.Parameters.AddWithValue("etag", record.EtagValue.Value!);
        insert.Parameters.AddWithValue("doc", record.ToJsonBytes());
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityRuleDocument.FromJson(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleDocument?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityRuleDocument current = SecurityRuleDocument.FromJson(doc);
        EnsureEtag("rule", name, expectedEtag, current.EtagValue);
        SecurityRuleDocument updated = current.WithUpdate(definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SecurityRules SET Etag = @etag, Document = @doc WHERE Name = @k;";
        update.Parameters.AddWithValue("etag", updated.EtagValue.Value!);
        update.Parameters.AddWithValue("doc", updated.ToJsonBytes());
        update.Parameters.AddWithValue("k", name);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityRules", "Name", "rule", name, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        var record = SecurityBindingDocument.CreateBinding(id, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO SecurityBindings (Id, SortOrder, Etag, Document) VALUES (@id, @order, @etag, @doc);";
        insert.Parameters.AddWithValue("id", id);
        insert.Parameters.AddWithValue("order", definition.Order);
        insert.Parameters.AddWithValue("etag", record.EtagValue.Value!);
        insert.Parameters.AddWithValue("doc", record.ToJsonBytes());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityBindingDocument.FromJson(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingDocument?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityBindingDocument current = SecurityBindingDocument.FromJson(doc);
        EnsureEtag("binding", id, expectedEtag, current.EtagValue);
        SecurityBindingDocument updated = current.WithUpdate(definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SecurityBindings SET SortOrder = @order, Etag = @etag, Document = @doc WHERE Id = @k;";
        update.Parameters.AddWithValue("order", definition.Order);
        update.Parameters.AddWithValue("etag", updated.EtagValue.Value!);
        update.Parameters.AddWithValue("doc", updated.ToJsonBytes());
        update.Parameters.AddWithValue("k", id);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityBindings", "Id", "binding", id, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityRuleDocument> rules = await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityBindingDocument> bindings = await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Generation FROM SecurityPolicyMeta WHERE Id = 0;";
        object? gen = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new SecurityPolicySnapshot(rules, bindings, gen is long g ? g : 0);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private static async ValueTask<byte[]?> DocumentAsync(NpgsqlConnection connection, string table, string column, string key, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Document FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("k", key);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask<IReadOnlyList<SecurityRuleDocument>> ReadRulesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<SecurityRuleDocument>();
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityRules ORDER BY Name;";
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(SecurityRuleDocument.FromJson(reader.GetFieldValue<byte[]>(0)));
        }

        return list;
    }

    private static async ValueTask<IReadOnlyList<SecurityBindingDocument>> ReadBindingsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<SecurityBindingDocument>();
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityBindings ORDER BY SortOrder, Id;";
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(SecurityBindingDocument.FromJson(reader.GetFieldValue<byte[]>(0)));
        }

        return list;
    }

    private static async ValueTask BumpGenerationAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand bump = connection.CreateCommand();
        bump.CommandText = "UPDATE SecurityPolicyMeta SET Generation = Generation + 1 WHERE Id = 0;";
        await bump.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private async ValueTask<bool> DeleteAsync(string table, string column, string kind, string key, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Etag FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("k", key);
        object? etag = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (etag is not string current)
        {
            return false;
        }

        EnsureEtag(kind, key, expectedEtag, new WorkflowEtag(current));
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM {table} WHERE {column} = @k;";
        delete.Parameters.AddWithValue("k", key);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SecurityRules (
            Name TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        CREATE TABLE IF NOT EXISTS SecurityBindings (
            Id TEXT NOT NULL PRIMARY KEY,
            SortOrder INTEGER NOT NULL,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_securitybindings_order ON SecurityBindings (SortOrder, Id);
        CREATE TABLE IF NOT EXISTS SecurityPolicyMeta (
            Id INTEGER NOT NULL PRIMARY KEY,
            Generation BIGINT NOT NULL
        );
        INSERT INTO SecurityPolicyMeta (Id, Generation) VALUES (0, 0) ON CONFLICT (Id) DO NOTHING;
        """;
}