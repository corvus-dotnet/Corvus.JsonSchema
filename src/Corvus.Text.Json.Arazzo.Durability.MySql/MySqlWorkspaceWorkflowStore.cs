// <copyright file="MySqlWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted relationally so a working copy survives a restart. Each working copy is stored as its
/// <see cref="WorkspaceWorkflow"/> document in a <c>LONGBLOB</c> column, keyed by its server-minted <c>id</c> (globally
/// unique, so the id alone is the key — no Name/Tags composite); its etag is held in a column for the
/// optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory over the
/// single row for an id (a working copy outside reach is reported as absent, non-disclosing), and per row in keyset
/// order for the list. Each operation opens a pooled connection, so the store is naturally concurrent. The document is
/// carried bytes-to-bytes (#803): rows read/write via <see cref="ParsedJsonDocument{T}"/> and the shared pooled
/// serialization, never a per-op detached clone.
/// </remarks>
public sealed class MySqlWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlWorkspaceWorkflowStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlWorkspaceWorkflowStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlWorkspaceWorkflowStore>(
            new MySqlWorkspaceWorkflowStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlWorkspaceWorkflowStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlWorkspaceWorkflowStore>(
            new MySqlWorkspaceWorkflowStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential; the
        // id is opaque to clients either way). A freshly minted GUID id is globally unique, so the insert never collides.
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO WorkspaceWorkflows (Id, Etag, Document) VALUES (@id, @etag, @doc);";
        insert.Parameters.AddWithValue("@id", id);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        insert.Parameters.AddWithValue("@doc", json);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await FindForManagementAsync(connection, id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor id in id order — an indexed range scan over the primary key, not a table load
            // (the id is globally unique, so it is the whole total order and the tie-breaker is empty). Reach is a per-row
            // predicate applied in memory as we stream; the reader is consumed only until the page fills.
            await using MySqlCommand select = connection.CreateCommand();
            if (hasCursor)
            {
                select.CommandText = "SELECT Id, Document FROM WorkspaceWorkflows WHERE Id > @id ORDER BY Id;";
                select.Parameters.AddWithValue("@id", cursor.Id);
            }
            else
            {
                select.CommandText = "SELECT Id, Document FROM WorkspaceWorkflows ORDER BY Id;";
            }

            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastId = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string id = reader.GetString(0);
                byte[] json = reader.GetFieldValue<byte[]>(1);
                ParsedJsonDocument<WorkspaceWorkflow> cand = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
                bool kept = false;
                try
                {
                    SecurityTagSet tagSet = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tagSet))
                    {
                        continue;
                    }

                    if (docs.Count == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    docs.Add(cand);
                    kept = true;
                    lastId = id;
                }
                finally
                {
                    if (!kept)
                    {
                        cand.Dispose();
                    }
                }
            }

            return hasMore
                ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                : WorkspaceWorkflowPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await FindForManagementAsync(connection, id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using MySqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE WorkspaceWorkflows SET Etag = @etag, Document = @doc WHERE Id = @id;"; // the id, provenance, and tags are immutable → key unchanged
        update.Parameters.AddWithValue("@etag", WorkspaceWorkflowSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@id", id);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await FindForManagementAsync(connection, id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM WorkspaceWorkflows WHERE Id = @id;";
        delete.Parameters.AddWithValue("@id", id);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
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

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key, so at most one row matches). A working copy outside reach is invisible (non-disclosing).
    private static async ValueTask<byte[]?> FindForManagementAsync(MySqlConnection connection, string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkspaceWorkflows WHERE Id = @id;";
        select.Parameters.AddWithValue("@id", id);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            byte[] json = reader.GetFieldValue<byte[]>(0);
            using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
            return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
        }

        return null;
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkspaceWorkflows (
            Id VARCHAR(255) NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            PRIMARY KEY (Id)
        );
        """;
}