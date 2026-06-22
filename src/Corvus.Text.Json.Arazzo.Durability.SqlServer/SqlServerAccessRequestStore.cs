// <copyright file="SqlServerAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted relationally. Each
/// request is stored as its <see cref="AccessRequest"/> schema document in a <c>varbinary(max)</c> column, with the
/// filterable fields (status, target workflow, subject) and the etag mirrored into columns for querying and the
/// optimistic-concurrency check. The creation instant is held sortably (ISO-8601 round-trip text) so the contract's
/// oldest-first ordering is a plain <c>ORDER BY</c>.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class SqlServerAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerAccessRequestStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerAccessRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerAccessRequestStore>(new SqlServerAccessRequestStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; stream its exact bytes as the VARBINARY(MAX)
        // parameter (no GC document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<AccessRequest> doc = AccessRequestSerialization.SerializeNewDoc(id, draft, actor, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand insert = connection.CreateCommand();
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
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
            insert.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<AccessRequest>();
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM AccessRequests");
            var conditions = new List<string>(4);
            if (query.Status is { } status)
            {
                conditions.Add("Status = @status");
                select.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(status));
            }

            if (query.BaseWorkflowId is { } baseWorkflowId)
            {
                conditions.Add("BaseWorkflowId = @bw");
                select.Parameters.AddWithValue("@bw", baseWorkflowId);
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
            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<AccessRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // decided record into the pooled buffer the returned document owns — streamed as the parameter (no GC array, no copy).
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(existing.AsMemory());
        ParsedJsonDocument<AccessRequest> updated = AccessRequestSerialization.SerializeDecisionDoc(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using SqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE AccessRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
            update.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> DocumentAsync(SqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AccessRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
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

    private const string SchemaSql =
        """
        IF OBJECT_ID(N'AccessRequests', N'U') IS NULL
        BEGIN
            CREATE TABLE AccessRequests (
                Id NVARCHAR(450) NOT NULL PRIMARY KEY,
                BaseWorkflowId NVARCHAR(450) NOT NULL,
                SubjectClaimType NVARCHAR(450) NOT NULL,
                SubjectClaimValue NVARCHAR(450) NOT NULL,
                Status NVARCHAR(64) NOT NULL,
                CreatedAt NVARCHAR(33) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
            CREATE INDEX IX_AccessRequests_Status ON AccessRequests (Status);
            CREATE INDEX IX_AccessRequests_Workflow ON AccessRequests (BaseWorkflowId);
            CREATE INDEX IX_AccessRequests_Subject ON AccessRequests (SubjectClaimType, SubjectClaimValue);
        END;
        """;
}