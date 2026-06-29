// <copyright file="SqlServerEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for
/// a deployment environment — the mutable set of administrator identities. Each record is stored as its
/// <see cref="EnvironmentAdministrators"/> document in a <c>varbinary(max)</c> column, keyed by EnvironmentName; its etag
/// is held in a column for the optimistic-concurrency check. Mirrors <see cref="SqlServerWorkflowAdministratorStore"/>,
/// including the reverse administration index that answers "which environments does this administrator administer?". The
/// record holds deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent; the <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its etag before writing, mirroring the other backends.
/// </remarks>
public sealed class SqlServerEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerEnvironmentAdministratorStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerEnvironmentAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerEnvironmentAdministratorStore>(new SqlServerEnvironmentAdministratorStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            isUpdate = false;
        }

        // The document write and the reverse-index rewrite are atomic (design §7.8): the inbox must never observe an
        // environment indexed under a digest its current administrator set no longer holds, or vice versa.
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (SqlCommand write = connection.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = isUpdate
                ? "UPDATE EnvironmentAdministrators SET Etag = @etag, Document = @doc WHERE EnvironmentName = @id;"
                : "INSERT INTO EnvironmentAdministrators (EnvironmentName, Etag, Document) VALUES (@id, @etag, @doc);";
            write.Parameters.AddWithValue("@etag", etag.Value!);
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(json.AsMemory());
            write.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
            write.Parameters.AddWithValue("@id", environmentName);
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Deleting the record must also retract this environment from every administrator-digest bucket it sat in, so the
        // reverse index never strands an environment that no longer exists. The document delete and the index retraction
        // are atomic for the same reason PutAsync's rewrite is (design §7.8). A missing record remains a no-op: both
        // statements simply affect zero rows.
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (SqlCommand delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
            delete.Parameters.AddWithValue("@id", environmentName);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (SqlCommand clear = connection.CreateCommand())
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
        // SQL leaf — never per row. The index columns are Latin1_General_BIN2 so the keyset compare is ordinal (the
        // contract's order).
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = after is null
            ? "SELECT TOP (@n) EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest ORDER BY EnvironmentName;"
            : "SELECT TOP (@n) EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest AND EnvironmentName > @after ORDER BY EnvironmentName;";
        select.Parameters.AddWithValue("@digest", adminDigest);
        select.Parameters.AddWithValue("@n", pageSize + 1);
        if (after is not null)
        {
            select.Parameters.AddWithValue("@after", after);
        }

        var rows = new List<string>(pageSize + 1);
        await using (SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(reader.GetString(0));
            }
        }

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Rewrites this environment's reverse-index rows within the write transaction (§7.8): retract the stale digests, then
    // index the current ones. The administrator set is small, so a delete-all-then-insert is simplest and correct.
    private static async ValueTask RewriteIndexAsync(SqlConnection connection, SqlTransaction transaction, string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, CancellationToken cancellationToken)
    {
        await using (SqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM EnvironmentAdministratorIndex WHERE EnvironmentName = @id;";
            clear.Parameters.AddWithValue("@id", environmentName);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (string digest in EnvironmentAdministeredPaging.DistinctDigests(administrators))
        {
            await using SqlCommand index = connection.CreateCommand();
            index.Transaction = transaction;
            index.CommandText = "INSERT INTO EnvironmentAdministratorIndex (AdminDigest, EnvironmentName) VALUES (@digest, @id);";
            index.Parameters.AddWithValue("@digest", digest);
            index.Parameters.AddWithValue("@id", environmentName);
            await index.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<byte[]?> ReadDocumentAsync(SqlConnection connection, string environmentName, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
        select.Parameters.AddWithValue("@id", environmentName);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
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
        IF OBJECT_ID(N'EnvironmentAdministrators', N'U') IS NULL
        BEGIN
            CREATE TABLE EnvironmentAdministrators (
                EnvironmentName NVARCHAR(450) NOT NULL PRIMARY KEY,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
        END;
        IF OBJECT_ID(N'EnvironmentAdministratorIndex', N'U') IS NULL
        BEGIN
            CREATE TABLE EnvironmentAdministratorIndex (
                AdminDigest NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
                EnvironmentName NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                PRIMARY KEY (AdminDigest, EnvironmentName)
            );
        END;
        """;
}