// <copyright file="SqliteSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2) persisted for a single-file / embedded host. Each record is stored as its
/// Corvus.Text.Json schema document (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>)
/// in a BLOB column, with its etag in a column for the optimistic-concurrency check; a single-row meta table holds
/// the monotonic generation a resolver caches against.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through it, as the catalog/state stores do.</remarks>
public sealed class SqliteSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteSecurityPolicyStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema against a file database.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a security-policy store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=policy.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteSecurityPolicyStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO SecurityRules (Name, Etag, Document) VALUES (@name, @etag, @doc);";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"A security rule named '{name}' already exists.");
            }

            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : PersistedJson.ToPooledDocument<SecurityRuleDocument>(doc);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(doc, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SecurityRules SET Etag = @etag, Document = @doc WHERE Name = @k;";
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", name);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityRules", "Name", "rule", name, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDefinition definition, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO SecurityBindings (Id, SortOrder, Etag, Document) VALUES (@id, @order, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@order", definition.Order);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : PersistedJson.ToPooledDocument<SecurityBindingDocument>(doc);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDefinition definition, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrEmpty(definition.ClaimType);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(doc, "binding", id, expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SecurityBindings SET SortOrder = @order, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@order", definition.Order);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityBindings", "Id", "binding", id, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PooledDocumentList<SecurityRuleDocument> rules = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
            PooledDocumentList<SecurityBindingDocument> bindings = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Generation FROM SecurityPolicyMeta WHERE Id = 0;";
            object? gen = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return new SecurityPolicySnapshot(rules, bindings, gen is long g ? g : 0);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private async ValueTask<byte[]?> DocumentAsync(string table, string column, string key, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = $"SELECT Document FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityRules ORDER BY Name;";
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(PersistedJson.ToPooledDocument<SecurityRuleDocument>(reader.GetFieldValue<byte[]>(0)));
        }

        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityBindings ORDER BY SortOrder, Id;";
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(PersistedJson.ToPooledDocument<SecurityBindingDocument>(reader.GetFieldValue<byte[]>(0)));
        }

        return list;
    }

    private async ValueTask<bool> DeleteAsync(string table, string column, string kind, string key, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = $"SELECT Etag FROM {table} WHERE {column} = @k;";
            select.Parameters.AddWithValue("@k", key);
            object? etag = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (etag is not string current)
            {
                return false;
            }

            SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, new WorkflowEtag(current));
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = $"DELETE FROM {table} WHERE {column} = @k;";
            delete.Parameters.AddWithValue("@k", key);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken)
    {
        using SqliteCommand bump = this.connection.CreateCommand();
        bump.CommandText = "UPDATE SecurityPolicyMeta SET Generation = Generation + 1 WHERE Id = 0;";
        await bump.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SecurityRules (
            Name TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS SecurityBindings (
            Id TEXT NOT NULL PRIMARY KEY,
            SortOrder INTEGER NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SecurityBindings_Order ON SecurityBindings (SortOrder, Id);
        CREATE TABLE IF NOT EXISTS SecurityPolicyMeta (
            Id INTEGER NOT NULL PRIMARY KEY,
            Generation INTEGER NOT NULL
        );
        INSERT OR IGNORE INTO SecurityPolicyMeta (Id, Generation) VALUES (0, 0);
        """;
}