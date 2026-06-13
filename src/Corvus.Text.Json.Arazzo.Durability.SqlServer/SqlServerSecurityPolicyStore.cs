// <copyright file="SqlServerSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2) persisted relationally. Each record is stored as its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) in a <c>varbinary(max)</c> column,
/// with its etag in a column for the optimistic-concurrency check; a single-row meta table holds the monotonic
/// generation a resolver caches against.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class SqlServerSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerSecurityPolicyStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A SqlClient connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">A SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerSecurityPolicyStore>(new SqlServerSecurityPolicyStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord> AddRuleAsync(string name, SecurityRuleDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        var record = new SecurityRuleRecord(name, definition.Expression, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO SecurityRules (Name, Etag, Document) VALUES (@name, @etag, @doc);";
        insert.Parameters.AddWithValue("@name", name);
        insert.Parameters.AddWithValue("@etag", record.Etag.Value!);
        insert.Parameters.AddWithValue("@doc", SecurityRuleDocument.From(record).ToJsonBytes());
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException($"A security rule named '{name}' already exists.");
        }

        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityRuleDocument.FromJson(doc).ToRecord();
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityRuleRecord>> ListRulesAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRuleRecord?> UpdateRuleAsync(string name, SecurityRuleDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentException.ThrowIfNullOrEmpty(definition.Expression);
        ArgumentNullException.ThrowIfNull(actor);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityRuleRecord current = SecurityRuleDocument.FromJson(doc).ToRecord();
        EnsureEtag("rule", name, expectedEtag, current.Etag);
        var updated = current with
        {
            Expression = definition.Expression,
            Description = definition.Description,
            UpdatedBy = actor,
            UpdatedAt = this.timeProvider.GetUtcNow(),
            Etag = NewEtag(),
        };
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SecurityRules SET Etag = @etag, Document = @doc WHERE Name = @k;";
        update.Parameters.AddWithValue("@etag", updated.Etag.Value!);
        update.Parameters.AddWithValue("@doc", SecurityRuleDocument.From(updated).ToJsonBytes());
        update.Parameters.AddWithValue("@k", name);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityRules", "Name", "rule", name, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        var record = new SecurityBinding(id, definition.ClaimType, definition.ClaimValue, definition.Read, definition.Write, definition.Purge, definition.Order, definition.Description, actor, this.timeProvider.GetUtcNow(), null, null, NewEtag());
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO SecurityBindings (Id, SortOrder, Etag, Document) VALUES (@id, @order, @etag, @doc);";
        insert.Parameters.AddWithValue("@id", id);
        insert.Parameters.AddWithValue("@order", definition.Order);
        insert.Parameters.AddWithValue("@etag", record.Etag.Value!);
        insert.Parameters.AddWithValue("@doc", SecurityBindingDocument.From(record).ToJsonBytes());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return record;
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : SecurityBindingDocument.FromJson(doc).ToRecord();
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<SecurityBinding>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBinding?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        SecurityBinding current = SecurityBindingDocument.FromJson(doc).ToRecord();
        EnsureEtag("binding", id, expectedEtag, current.Etag);
        var updated = current with
        {
            ClaimType = definition.ClaimType,
            ClaimValue = definition.ClaimValue,
            Read = definition.Read,
            Write = definition.Write,
            Purge = definition.Purge,
            Order = definition.Order,
            Description = definition.Description,
            UpdatedBy = actor,
            UpdatedAt = this.timeProvider.GetUtcNow(),
            Etag = NewEtag(),
        };
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SecurityBindings SET SortOrder = @order, Etag = @etag, Document = @doc WHERE Id = @k;";
        update.Parameters.AddWithValue("@order", definition.Order);
        update.Parameters.AddWithValue("@etag", updated.Etag.Value!);
        update.Parameters.AddWithValue("@doc", SecurityBindingDocument.From(updated).ToJsonBytes());
        update.Parameters.AddWithValue("@k", id);
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityRuleRecord> rules = await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<SecurityBinding> bindings = await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Generation FROM SecurityPolicyMeta WHERE Id = 0;";
        object? gen = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new SecurityPolicySnapshot(rules, bindings, gen is long g ? g : 0);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static void EnsureEtag(string kind, string id, WorkflowEtag expected, WorkflowEtag actual)
    {
        if (!expected.IsNone && expected != actual)
        {
            throw new SecurityPolicyConflictException(kind, id, expected);
        }
    }

    private static async ValueTask<byte[]?> DocumentAsync(SqlConnection connection, string table, string column, string key, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Document FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask<IReadOnlyList<SecurityRuleRecord>> ReadRulesAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<SecurityRuleRecord>();
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityRules ORDER BY Name;";
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(SecurityRuleDocument.FromJson(reader.GetFieldValue<byte[]>(0)).ToRecord());
        }

        return list;
    }

    private static async ValueTask<IReadOnlyList<SecurityBinding>> ReadBindingsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new List<SecurityBinding>();
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityBindings ORDER BY SortOrder, Id;";
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(SecurityBindingDocument.FromJson(reader.GetFieldValue<byte[]>(0)).ToRecord());
        }

        return list;
    }

    private static async ValueTask BumpGenerationAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using SqlCommand bump = connection.CreateCommand();
        bump.CommandText = "UPDATE SecurityPolicyMeta SET Generation = Generation + 1 WHERE Id = 0;";
        await bump.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(this.connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask<bool> DeleteAsync(string table, string column, string kind, string key, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Etag FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? etag = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (etag is not string current)
        {
            return false;
        }

        EnsureEtag(kind, key, expectedEtag, new WorkflowEtag(current));
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM {table} WHERE {column} = @k;";
        delete.Parameters.AddWithValue("@k", key);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private const string SchemaSql =
        """
        IF OBJECT_ID(N'SecurityRules', N'U') IS NULL
        BEGIN
            CREATE TABLE SecurityRules (
                Name NVARCHAR(450) NOT NULL PRIMARY KEY,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
        END;
        IF OBJECT_ID(N'SecurityBindings', N'U') IS NULL
        BEGIN
            CREATE TABLE SecurityBindings (
                Id NVARCHAR(450) NOT NULL PRIMARY KEY,
                SortOrder INT NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
            CREATE INDEX IX_SecurityBindings_Order ON SecurityBindings (SortOrder, Id);
        END;
        IF OBJECT_ID(N'SecurityPolicyMeta', N'U') IS NULL
        BEGIN
            CREATE TABLE SecurityPolicyMeta (
                Id INT NOT NULL PRIMARY KEY,
                Generation BIGINT NOT NULL
            );
        END;
        IF NOT EXISTS (SELECT 1 FROM SecurityPolicyMeta WHERE Id = 0)
            INSERT INTO SecurityPolicyMeta (Id, Generation) VALUES (0, 0);
        """;
}