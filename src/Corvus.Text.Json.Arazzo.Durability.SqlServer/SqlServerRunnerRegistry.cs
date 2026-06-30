// <copyright file="SqlServerRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;

using Corvus.Text.Json;

using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a UTF-8 <c>VARCHAR(MAX)</c> column (a <c>_UTF8</c> collation) keyed by runner id, alongside a
/// queryable <c>last_seen_at</c> column used for pruning. The UTF-8 collation lets the heartbeat patch the
/// document's mirrored <c>lastSeenAt</c> field server-side with <c>JSON_MODIFY</c> (a single statement, no read),
/// while reads stay bytes-native via <c>CAST(doc AS VARBINARY(MAX))</c>. It uses Microsoft.Data.SqlClient directly
/// (no ORM, no migrations runtime), so the
/// same code covers SQL Server, Azure SQL Database and Azure SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the registry is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        IF OBJECT_ID(N'runner_registrations', N'U') IS NULL
        BEGIN
            CREATE TABLE runner_registrations (
                runner_id NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                last_seen_at BIGINT NOT NULL,
                doc VARCHAR(MAX) COLLATE Latin1_General_100_CI_AS_SC_UTF8 NOT NULL,
                CONSTRAINT PK_runner_registrations PRIMARY KEY (runner_id)
            );
            CREATE INDEX IX_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        END;
        IF OBJECT_ID(N'runner_hosted_versions', N'U') IS NULL
        BEGIN
            CREATE TABLE runner_hosted_versions (
                runner_id NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                base_workflow_id NVARCHAR(450) NOT NULL,
                version_number INT NOT NULL,
                CONSTRAINT PK_runner_hosted PRIMARY KEY (runner_id, base_workflow_id, version_number),
                CONSTRAINT FK_runner_hosted FOREIGN KEY (runner_id) REFERENCES runner_registrations (runner_id) ON DELETE CASCADE
            );
            CREATE INDEX IX_runner_hosted_versions_version ON runner_hosted_versions (base_workflow_id, version_number);
        END;
        """;

    private readonly string connectionString;

    private SqlServerRunnerRegistry(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Provisions the registry schema (table and index). This performs DDL, so it requires a login permitted
    /// to create tables; run it once at deploy/migration time, separately from the least-privileged login used
    /// to <see cref="ConnectAsync"/> the registry for operation.
    /// </summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string for a login permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data
    /// access on the table. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to
    /// create the schema. The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<SqlServerRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerRunnerRegistry>(new SqlServerRunnerRegistry(connectionString));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;

        // The doc column is a UTF-8 VARCHAR; SqlClient transmits .NET strings as NVARCHAR, which the server converts
        // losslessly into the UTF-8 column. (Sending the UTF-8 bytes as a VARBINARY/VARCHAR param instead would be
        // mis-decoded through the connection code page, corrupting non-ASCII.) Serialize once, then decode to a
        // string for the parameter — a register-time (cold-path) cost; reads stay bytes-native.
        byte[] docUtf8 = PersistedJson.ToArray(registration, static (Utf8JsonWriter writer, in RunnerRegistration r) => r.WriteTo(writer));
        string doc = Encoding.UTF8.GetString(docUtf8);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (SqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                MERGE runner_registrations AS target
                USING (SELECT @runnerId AS runner_id) AS source
                ON target.runner_id = source.runner_id
                WHEN MATCHED THEN
                    UPDATE SET last_seen_at = @lastSeenAt, doc = @doc
                WHEN NOT MATCHED THEN
                    INSERT (runner_id, last_seen_at, doc) VALUES (@runnerId, @lastSeenAt, @doc);
                """;
            upsert.Parameters.AddWithValue("@runnerId", runnerId);
            upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            upsert.Parameters.Add(new SqlParameter("@doc", System.Data.SqlDbType.NVarChar, -1) { Value = doc });
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
        await using (SqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
            clear.Parameters.AddWithValue("@runnerId", runnerId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await using SqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO runner_hosted_versions (runner_id, base_workflow_id, version_number) VALUES (@runnerId, @baseWorkflowId, @versionNumber);";
            insert.Parameters.AddWithValue("@runnerId", runnerId);
            insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            insert.Parameters.AddWithValue("@versionNumber", versionNumber);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber) THEN 1 ELSE 0 END;";
        command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        command.Parameters.AddWithValue("@versionNumber", versionNumber);
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        // A heartbeat advances only last_seen_at and the document's mirrored lastSeenAt. Rather than read the whole
        // registration back and rewrite it client-side (two round-trips plus a full-payload rewrite), patch both in
        // one statement: JSON_MODIFY surgically replaces the one field server-side on the UTF-8 doc column (the rest
        // of the document, including key order, is preserved). The ISO-8601 string is supplied by the caller (the
        // round-trip "O" form the generated model emits and parses). The update is unconditional (last-writer-wins,
        // the intended heartbeat semantics); a zero row count means the runner is unknown.
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE runner_registrations
            SET last_seen_at = @lastSeenAt, doc = JSON_MODIFY(doc, '$.lastSeenAt', @lastSeenIso)
            WHERE runner_id = @runnerId;
            """;
        update.Parameters.AddWithValue("@runnerId", runnerId);
        update.Parameters.AddWithValue("@lastSeenAt", at.ToUnixTimeMilliseconds());
        update.Parameters.AddWithValue("@lastSeenIso", at.ToString("O", CultureInfo.InvariantCulture));
        int affected = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();

        // Read the UTF-8 doc column back as raw bytes (CAST to VARBINARY) so the deserialize stays bytes-native —
        // no nvarchar string materialization on the read path.
        command.CommandText = "SELECT CAST(doc AS VARBINARY(MAX)) FROM runner_registrations;";
        var result = new List<RunnerRegistration>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;

        // Decode the keyset cursor from the request's page token; the runner id reifies to a string only at the SqlClient
        // parameter boundary (a genuine DB-param leaf) — undefined token = first page.
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();

        // Native keyset page: runner_id is the PRIMARY KEY declared COLLATE Latin1_General_BIN2, so the index gives a
        // byte-ordinal order (the same order the in-memory pager sorts by) and drives both the range seek and the ordering;
        // TOP bounds the read to one page — never a full SELECT + parse of every registration. TOP (@limit) is pageSize + 1
        // to look one row ahead. The UTF-8 doc is read back as VARBINARY so the deserialize stays bytes-native.
        select.CommandText =
            """
            SELECT TOP (@limit) CAST(doc AS VARBINARY(MAX)) FROM runner_registrations
            WHERE (@after IS NULL OR runner_id > @after)
            ORDER BY runner_id;
            """;
        select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
        select.Parameters.AddWithValue("@limit", pageSize + 1);

        var page = new List<RunnerRegistration>(pageSize + 1);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            page.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        if (page.Count <= pageSize)
        {
            return RunnerRegistryPage.Create(page);
        }

        // A (pageSize+1)th row exists → there is a next page; drop it and emit the token from the last kept row's id
        // (bytes-native: base64url straight over the runner id's persisted UTF-8, no managed id string).
        page.RemoveAt(page.Count - 1);
        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ReadReach is null)
        {
            // Unrestricted read reach (e.g. the trusted system path): no row is filtered, so the bounded TOP keyset query
            // (one page, no per-row tag work) is exactly right — no full read, no in-memory filter.
            return await this.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);
        }

        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();

        // Reach (§14.2) is a per-row ABAC predicate, not expressible as SQL, so runner_id COLLATE Latin1_General_BIN2 drives
        // an ordered range SEEK past the cursor and rows are streamed + reach-filtered in flight — the reader is consumed only
        // until the page fills, never the full SELECT + parse of every registration the in-memory fallback does. (No TOP:
        // filtered-out rows don't count toward the page, so the page-fill + one-row look-ahead governs how far the scan reads.)
        select.CommandText =
            """
            SELECT CAST(doc AS VARBINARY(MAX)) FROM runner_registrations
            WHERE (@after IS NULL OR runner_id > @after)
            ORDER BY runner_id;
            """;
        select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);

        var page = new List<RunnerRegistration>(pageSize);
        bool hasMore = false;
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            RunnerRegistration runner = RunnerRegistration.FromJson((byte[])reader[0]);

            // reachTags is absent on a runner serving an unscoped environment; an empty tag set fails a scoped reach
            // (fail-closed), so such a runner is invisible to a tenant-scoped caller — matching the in-memory pager.
            SecurityTagSet tags = runner.ReachTags.IsNotUndefined()
                ? SecurityTagSet.CopyFrom(runner.ReachTags)
                : SecurityTagSet.Empty;
            if (!context.Admits(AccessVerb.Read, tags))
            {
                continue;
            }

            if (page.Count == pageSize)
            {
                hasMore = true; // a further reach-visible row exists → there is a next page after the last included row
                break;
            }

            page.Add(runner);
        }

        if (!hasMore)
        {
            return RunnerRegistryPage.Create(page);
        }

        using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
        return RunnerRegistryPage.Create(page, lastId.Span);
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
        command.Parameters.AddWithValue("@cutoff", deadBefore.ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the registry. This implementation holds no per-instance resources, so it is a no-op.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
}