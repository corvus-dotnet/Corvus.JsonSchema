// <copyright file="KycStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;
using NpgsqlTypes;

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// The KYC service's own durable store: the identity verifications it has performed, keyed by the account they are for
/// (the account itself is owned by the onboarding service; the KYC service records only the verdict). Persisted to the
/// service's own PostgreSQL database (the microservice-owns-its-data pattern). The accumulated wire documents (the
/// resolved applicant and the identity result) are held as <c>bytea</c> columns exactly as the service emitted them.
/// </summary>
public sealed class KycStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private KycStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
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

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<KycStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<KycStore>(new KycStore(dataSource, ownsDataSource: false));
    }

    /// <summary>Opens the store for operation against a connection string (the store owns the data source it creates).</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<KycStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<KycStore>(new KycStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Records (or updates, on re-verification) the verification for an account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="status">The outcome (<c>verified</c>/<c>blocked</c>/<c>pending</c>).</param>
    /// <param name="fullName">The resolved applicant name.</param>
    /// <param name="applicant">The resolved applicant document (JSON).</param>
    /// <param name="identity">The identity-result document (JSON).</param>
    /// <param name="submittedAt">The moment the verification was recorded (kept from the first submission on update).</param>
    /// <param name="verifiedAt">The moment the verdict completed.</param>
    /// <param name="channel">How the verdict was produced (<c>synchronous</c>/<c>manual</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the record is written.</returns>
    public async ValueTask RecordVerificationAsync(string accountId, string status, string fullName, byte[] applicant, byte[] identity, DateTimeOffset submittedAt, DateTimeOffset verifiedAt, string channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO Verifications (AccountId, Status, FullName, Applicant, Identity, SubmittedAt, VerifiedAt, Channel)
            VALUES (@id, @status, @name, @applicant, @identity, @submitted, @verified, @channel)
            ON CONFLICT (AccountId) DO UPDATE SET
                Status = EXCLUDED.Status, FullName = EXCLUDED.FullName, Applicant = EXCLUDED.Applicant,
                Identity = EXCLUDED.Identity, VerifiedAt = EXCLUDED.VerifiedAt, Channel = EXCLUDED.Channel;
            """;
        upsert.Parameters.AddWithValue("id", accountId);
        upsert.Parameters.AddWithValue("status", status);
        upsert.Parameters.AddWithValue("name", fullName);
        upsert.Parameters.AddWithValue("applicant", NpgsqlDbType.Bytea, applicant);
        upsert.Parameters.AddWithValue("identity", NpgsqlDbType.Bytea, identity);
        upsert.Parameters.AddWithValue("submitted", NpgsqlDbType.TimestampTz, submittedAt);
        upsert.Parameters.AddWithValue("verified", NpgsqlDbType.TimestampTz, verifiedAt);
        upsert.Parameters.AddWithValue("channel", channel);
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a single account's verification, or <see langword="null"/> if there is none.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The verification record, or <see langword="null"/>.</returns>
    public async ValueTask<VerificationRecord?> GetAsync(string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = SelectColumns + " WHERE AccountId = @id;";
        select.Parameters.AddWithValue("id", accountId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRecord(reader) : null;
    }

    /// <summary>Lists verifications newest-first with keyset pagination.</summary>
    /// <param name="limit">The maximum number to return.</param>
    /// <param name="pageToken">An opaque continuation token, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of verifications and the token for the next page.</returns>
    public async ValueTask<(IReadOnlyList<VerificationRecord> Verifications, string? NextPageToken)> ListVerificationsAsync(int limit, string? pageToken, CancellationToken cancellationToken = default)
    {
        int pageSize = limit > 0 ? limit : 50;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        if (KycPageToken.TryDecode(pageToken, out DateTimeOffset cursorSubmitted, out string cursorId))
        {
            select.CommandText = SelectColumns + " WHERE (SubmittedAt, AccountId) < (@s, @a) ORDER BY SubmittedAt DESC, AccountId DESC LIMIT @take;";
            select.Parameters.AddWithValue("s", NpgsqlDbType.TimestampTz, cursorSubmitted);
            select.Parameters.AddWithValue("a", cursorId);
        }
        else
        {
            select.CommandText = SelectColumns + " ORDER BY SubmittedAt DESC, AccountId DESC LIMIT @take;";
        }

        select.Parameters.AddWithValue("take", pageSize + 1);
        var verifications = new List<VerificationRecord>(pageSize);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            verifications.Add(ReadRecord(reader));
        }

        if (verifications.Count > pageSize)
        {
            VerificationRecord last = verifications[pageSize - 1];
            verifications.RemoveAt(pageSize);
            return (verifications, KycPageToken.Encode(last.SubmittedAt, last.AccountId));
        }

        return (verifications, null);
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

    private static VerificationRecord ReadRecord(NpgsqlDataReader reader) => new(
        AccountId: reader.GetString(0),
        Status: reader.GetString(1),
        FullName: reader.IsDBNull(2) ? null : reader.GetString(2),
        Applicant: reader.IsDBNull(3) ? null : reader.GetFieldValue<byte[]>(3),
        Identity: reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4),
        SubmittedAt: reader.GetFieldValue<DateTimeOffset>(5),
        VerifiedAt: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        Channel: reader.GetString(7));

    private const string SelectColumns =
        "SELECT AccountId, Status, FullName, Applicant, Identity, SubmittedAt, VerifiedAt, Channel FROM Verifications";

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Verifications (
            AccountId TEXT NOT NULL PRIMARY KEY,
            Status TEXT NOT NULL,
            FullName TEXT,
            Applicant BYTEA,
            Identity BYTEA,
            SubmittedAt TIMESTAMPTZ NOT NULL,
            VerifiedAt TIMESTAMPTZ,
            Channel TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_verifications_submitted ON Verifications (SubmittedAt DESC, AccountId DESC);
        """;
}
