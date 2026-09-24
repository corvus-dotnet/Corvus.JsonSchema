// <copyright file="PostgresTenantAnchorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Anchoring;

using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="ITenantAnchorStore"/> (ADR 0065 decision 6): the tenant's own database, never the
/// control plane's. Each run's <see cref="AnchorRecord"/> is stored as its persisted JSON (<see cref="TenantAnchorRecord"/>)
/// in a <c>bytea</c> column keyed by environment and run, and each environment's attested incarnation in a row of its
/// own. A record write is one transaction: the stored row is read under a row lock, compared with what the writer
/// expected, classified by <see cref="AnchorAcceptance.Classify"/> against the environment's attested incarnation, and
/// replaced only if a clause admits it. That is the whole of what the store enforces.
/// </summary>
public sealed class PostgresTenantAnchorStore : ITenantAnchorStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS tenant_anchor_incarnations (
            environment TEXT COLLATE "C" PRIMARY KEY NOT NULL,
            incarnation BIGINT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS tenant_anchors (
            environment TEXT COLLATE "C" NOT NULL,
            run_id TEXT COLLATE "C" NOT NULL,
            doc BYTEA NOT NULL,
            PRIMARY KEY (environment, run_id)
        );
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresTenantAnchorStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the anchor schema from a connection string.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the anchor schema over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresTenantAnchorStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresTenantAnchorStore>(new PostgresTenantAnchorStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the store over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresTenantAnchorStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresTenantAnchorStore>(new PostgresTenantAnchorStore(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask<AnchorRecord?> ReadAsync(string environment, string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT doc FROM tenant_anchors WHERE environment = @environment AND run_id = @runId;";
        select.Parameters.AddWithValue("@environment", environment);
        select.Parameters.AddWithValue("@runId", runId);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] doc ? TenantAnchorRecord.Deserialize(doc) : null;
    }

    /// <inheritdoc/>
    public async ValueTask<AnchorWriteKind> WriteAsync(string environment, AnchorRecord? expected, AnchorRecord proposed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (!string.Equals(proposed.EnvironmentId, environment, StringComparison.Ordinal))
        {
            return AnchorWriteKind.Rejected;
        }

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // The attestation, read inside the transaction so the classification and the record it admits agree.
        ulong? attested;
        await using (NpgsqlCommand incarnation = connection.CreateCommand())
        {
            incarnation.Transaction = transaction;
            incarnation.CommandText = "SELECT incarnation FROM tenant_anchor_incarnations WHERE environment = @environment;";
            incarnation.Parameters.AddWithValue("@environment", environment);
            attested = await incarnation.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long value ? (ulong)value : null;
        }

        if (attested is not { } attestedIncarnation)
        {
            return AnchorWriteKind.Rejected;
        }

        // The stored record under a row lock: the compare half of the compare-and-swap, held until the replace.
        AnchorRecord? stored = null;
        await using (NpgsqlCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT doc FROM tenant_anchors WHERE environment = @environment AND run_id = @runId FOR UPDATE;";
            select.Parameters.AddWithValue("@environment", environment);
            select.Parameters.AddWithValue("@runId", proposed.RunId);
            if (await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] doc)
            {
                stored = TenantAnchorRecord.Deserialize(doc);
            }
        }

        if (stored != expected)
        {
            return AnchorWriteKind.Rejected;
        }

        AnchorWriteKind kind = AnchorAcceptance.Classify(stored, proposed, attestedIncarnation);
        if (kind == AnchorWriteKind.Rejected)
        {
            return AnchorWriteKind.Rejected;
        }

        await using (NpgsqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;

            // Create-if-absent when nothing was expected, so two first claims cannot both insert; a plain replace
            // otherwise, over the row the lock holds.
            upsert.CommandText = stored is null
                ? "INSERT INTO tenant_anchors (environment, run_id, doc) VALUES (@environment, @runId, @doc) ON CONFLICT DO NOTHING;"
                : "UPDATE tenant_anchors SET doc = @doc WHERE environment = @environment AND run_id = @runId;";
            upsert.Parameters.AddWithValue("@environment", environment);
            upsert.Parameters.AddWithValue("@runId", proposed.RunId);
            upsert.Parameters.AddWithValue("@doc", TenantAnchorRecord.Serialize(proposed));
            if (await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
            {
                return AnchorWriteKind.Rejected;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return kind;
    }

    /// <inheritdoc/>
    public async ValueTask<ulong?> ReadAttestedIncarnationAsync(string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT incarnation FROM tenant_anchor_incarnations WHERE environment = @environment;";
        select.Parameters.AddWithValue("@environment", environment);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is long value ? (ulong)value : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> AttestIncarnationAsync(string environment, ulong incarnation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        if (incarnation == 0 || incarnation > long.MaxValue)
        {
            return false;
        }

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand upsert = connection.CreateCommand();

        // Strictly monotonic in one statement: the insert lands only when no row exists, and the update only when
        // the recorded value is below the attested one. Anything else affects no row.
        upsert.CommandText =
            """
            INSERT INTO tenant_anchor_incarnations (environment, incarnation) VALUES (@environment, @incarnation)
            ON CONFLICT (environment) DO UPDATE SET incarnation = EXCLUDED.incarnation
            WHERE tenant_anchor_incarnations.incarnation < EXCLUDED.incarnation;
            """;
        upsert.Parameters.AddWithValue("@environment", environment);
        upsert.Parameters.AddWithValue("@incarnation", (long)incarnation);
        return await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }
}