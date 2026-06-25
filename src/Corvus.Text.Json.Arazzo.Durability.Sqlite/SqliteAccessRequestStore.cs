// <copyright file="SqliteAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted for a single-file /
/// embedded host. Each request is stored as its <see cref="AccessRequest"/> schema document in a BLOB column, with the
/// filterable fields (status, target workflow, subject) and the etag mirrored into columns for querying and the
/// optimistic-concurrency check.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through it, as the other SQLite stores do.</remarks>
public sealed class SqliteAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteAccessRequestStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an access-request store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=requests.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteAccessRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteAccessRequestStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            WorkflowEtag etag = NewEtag();
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            byte[] json = AccessRequestSerialization.SerializeNew(id, draft, actor, now, etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO AccessRequests (Id, BaseWorkflowId, SubjectClaimType, SubjectClaimValue, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @bw, @st, @sv, @status, @createdAt, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@bw", draft.BaseWorkflowIdValue);
            insert.Parameters.AddWithValue("@st", draft.SubjectClaimTypeValue);
            insert.Parameters.AddWithValue("@sv", draft.SubjectClaimValueValue);
            insert.Parameters.AddWithValue("@status", AccessRequestStatusNames.Pending);
            insert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<AccessRequest>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new PooledDocumentList<AccessRequest>();
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM AccessRequests");
            var conditions = new List<string>(4);
            if (query.Status is { } status)
            {
                conditions.Add("Status = @status");
                select.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(status));
            }

            if (query.BaseWorkflowId.IsNotUndefined())
            {
                conditions.Add("BaseWorkflowId = @bw");
                select.Parameters.AddWithValue("@bw", (string)query.BaseWorkflowId);
            }

            if (query.SubjectClaimType is { } subjectType)
            {
                conditions.Add("SubjectClaimType = @st");
                select.Parameters.AddWithValue("@st", subjectType);
            }

            if (query.SubjectClaimValue is { } subjectValue)
            {
                conditions.Add("SubjectClaimValue = @sv");
                select.Parameters.AddWithValue("@sv", subjectValue);
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY CreatedAt, Id;");
            select.CommandText = sql.ToString();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<AccessRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
            byte[] json = AccessRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE AccessRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<AccessRequest>(json);
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

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AccessRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

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
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Status ON AccessRequests (Status);
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Workflow ON AccessRequests (BaseWorkflowId);
        CREATE INDEX IF NOT EXISTS IX_AccessRequests_Subject ON AccessRequests (SubjectClaimType, SubjectClaimValue);
        """;
}