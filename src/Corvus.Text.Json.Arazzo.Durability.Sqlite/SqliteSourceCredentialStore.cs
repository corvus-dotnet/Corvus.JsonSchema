// <copyright file="SqliteSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted for a single-file / embedded host. Each binding is
/// stored as its <see cref="SourceCredentialBinding"/> document in a BLOB column, keyed by (SourceName, Environment, and
/// a discriminator over its immutable management/usage tags) so tenant-/workflow-scoped bindings for the same
/// source/environment coexist; its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage path by
/// label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a deployment
/// keeps those reach-disjoint. One connection is held open and all operations are serialised through it, as the other
/// Sqlite stores do.
/// </remarks>
public sealed class SqliteSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private long idSequence;

    private SqliteSourceCredentialStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema against a database.</summary>
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

    /// <summary>Opens a source credential store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=credentials.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteSourceCredentialStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteSourceCredentialStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialDefinition definition, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = $"scred-{++this.idSequence}";
            WorkflowEtag etag = NewEtag();
            byte[] json = SourceCredentialSerialization.SerializeNew(id, definition, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO SourceCredentials (SourceName, Environment, Tags, Etag, Document) VALUES (@s, @e, @t, @etag, @doc);";
            insert.Parameters.AddWithValue("@s", definition.SourceName);
            insert.Parameters.AddWithValue("@e", definition.Environment);
            insert.Parameters.AddWithValue("@t", SourceCredentialKey.Discriminator(definition.ManagementTags, definition.UsageTags));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"A source credential binding for '{definition.SourceName}@{definition.Environment}' with those security tags already exists.");
            }

            return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SourceCredentialBinding>> ListAsync(AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<SourceCredentialBinding>();
            try
            {
                using SqliteCommand select = this.connection.CreateCommand();
                select.CommandText = "SELECT Document FROM SourceCredentials ORDER BY SourceName, Environment;";
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    byte[] json = reader.GetFieldValue<byte[]>(0);
                    using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                    if (context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                    {
                        docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
                    }
                }

                return docs;
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialDefinition definition, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDefinition(definition);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return null;
            }

            byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, definition, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SourceCredentials SET Etag = @etag, Document = @doc WHERE SourceName = @s AND Environment = @e AND Tags = @t;";
            update.Parameters.AddWithValue("@etag", SourceCredentialSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@s", sourceName);
            update.Parameters.AddWithValue("@e", environment);
            update.Parameters.AddWithValue("@t", tags!);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return false;
            }

            if (!expectedEtag.IsNone)
            {
                SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
            }

            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM SourceCredentials WHERE SourceName = @s AND Environment = @e AND Tags = @t;";
            delete.Parameters.AddWithValue("@s", sourceName);
            delete.Parameters.AddWithValue("@e", environment);
            delete.Parameters.AddWithValue("@t", tags!);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
            select.Parameters.AddWithValue("@s", sourceName);
            select.Parameters.AddWithValue("@e", environment);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(0));
                if (candidate.RootElement.IsUsableBy(runTags))
                {
                    return candidate;
                }

                candidate.Dispose();
            }

            return null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s;";
            select.Parameters.AddWithValue("@s", sourceName);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            bool any = false;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                any = true;
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(0));
                if (candidate.RootElement.IsUsableBy(tags))
                {
                    return CredentialSourceAccess.Granted;
                }
            }

            return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its bytes
    // and its tag discriminator (the row key). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string tags = reader.GetString(0);
            byte[] json = reader.GetFieldValue<byte[]>(1);
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, tags);
            }
        }

        return (null, null);
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SourceCredentials (
            SourceName TEXT NOT NULL,
            Environment TEXT NOT NULL,
            Tags TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (SourceName, Environment, Tags)
        );
        CREATE INDEX IF NOT EXISTS IX_SourceCredentials_Source ON SourceCredentials (SourceName);
        """;
}