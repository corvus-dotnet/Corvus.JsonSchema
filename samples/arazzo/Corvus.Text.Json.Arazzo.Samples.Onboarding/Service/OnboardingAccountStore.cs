// <copyright file="OnboardingAccountStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;
using NpgsqlTypes;

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// The onboarding service's own durable store: customer accounts and their progress through the onboarding
/// journey (created -> provisioned -> welcomed), persisted to the service's own PostgreSQL database. Each account
/// is a row keyed by its id, holding the signup facts the service owns (the customer's email and chosen plan) and
/// the provisioning wire document as a <c>bytea</c> column exactly as the service emitted it, so the read model
/// (<c>AccountView</c>) is composed by writing the scalars and splicing the document at the leaf. Identity
/// verification is owned by the KYC service, so no identity is held here.
/// </summary>
/// <remarks>
/// This is a real backend, not a mock: it owns its schema and data independently of the Arazzo control plane's
/// store (the microservice-owns-its-data pattern). Each operation opens a pooled connection, so it is naturally
/// concurrent. It follows the same lifecycle as the control-plane stores — <see cref="PrepareAsync(NpgsqlDataSource, CancellationToken)"/>
/// provisions the schema (a DDL-capable phase, run once at deploy time) and <see cref="ConnectAsync(NpgsqlDataSource, CancellationToken)"/>
/// opens it for operation.
/// </remarks>
public sealed class OnboardingAccountStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private OnboardingAccountStore(NpgsqlDataSource dataSource, bool ownsDataSource)
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
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<OnboardingAccountStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<OnboardingAccountStore>(new OnboardingAccountStore(dataSource, ownsDataSource: false));
    }

    /// <summary>Opens the store for operation against a connection string (the store owns the data source it creates).</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<OnboardingAccountStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<OnboardingAccountStore>(new OnboardingAccountStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Creates a new account in the <c>created</c> state with the signup facts the service owns.</summary>
    /// <param name="accountId">The new account's id.</param>
    /// <param name="email">The customer's email, or <see langword="null"/> if none was supplied.</param>
    /// <param name="plan">The chosen plan, or <see langword="null"/> if none was supplied.</param>
    /// <param name="submittedAt">The moment the account was created.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the row is inserted.</returns>
    public async ValueTask CreateAsync(string accountId, string? email, string? plan, DateTimeOffset submittedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Accounts (AccountId, Status, Email, Plan, SubmittedAt) VALUES (@id, 'created', @email, @plan, @submitted);";
        insert.Parameters.AddWithValue("id", accountId);
        insert.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        insert.Parameters.AddWithValue("plan", (object?)plan ?? DBNull.Value);
        insert.Parameters.AddWithValue("submitted", NpgsqlDbType.TimestampTz, submittedAt);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Records a provisioning outcome against an account and moves it to <c>provisioned</c>.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="provisioning">The provisioning document (JSON), as emitted.</param>
    /// <param name="provisionedAt">The moment provisioning completed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the account existed and was updated; otherwise <see langword="false"/>.</returns>
    public async ValueTask<bool> RecordProvisioningAsync(string accountId, byte[] provisioning, DateTimeOffset provisionedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Accounts SET Status = 'provisioned', Provisioning = @provisioning, ProvisionedAt = @at WHERE AccountId = @id;";
        update.Parameters.AddWithValue("provisioning", NpgsqlDbType.Bytea, provisioning);
        update.Parameters.AddWithValue("at", NpgsqlDbType.TimestampTz, provisionedAt);
        update.Parameters.AddWithValue("id", accountId);
        return await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Marks an account <c>welcomed</c>.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="welcomedAt">The moment the welcome was sent.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> if the account existed and was updated; otherwise <see langword="false"/>.</returns>
    public async ValueTask<bool> RecordWelcomeAsync(string accountId, DateTimeOffset welcomedAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Accounts SET Status = 'welcomed', WelcomedAt = @at WHERE AccountId = @id;";
        update.Parameters.AddWithValue("at", NpgsqlDbType.TimestampTz, welcomedAt);
        update.Parameters.AddWithValue("id", accountId);
        return await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Reads a single account, or <see langword="null"/> if there is no such account.</summary>
    /// <param name="accountId">The account id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The account record, or <see langword="null"/>.</returns>
    public async ValueTask<OnboardingAccountRecord?> GetAsync(string accountId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = SelectColumns + " WHERE AccountId = @id;";
        select.Parameters.AddWithValue("id", accountId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadRecord(reader) : null;
    }

    /// <summary>Lists accounts newest-first with keyset pagination.</summary>
    /// <param name="limit">The maximum number of accounts to return.</param>
    /// <param name="pageToken">An opaque continuation token from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of accounts and the token for the next page (<see langword="null"/> on the last page).</returns>
    public async ValueTask<(IReadOnlyList<OnboardingAccountRecord> Accounts, string? NextPageToken)> ListAsync(int limit, string? pageToken, CancellationToken cancellationToken = default)
    {
        int pageSize = limit > 0 ? limit : 50;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();

        // Keyset seek strictly past the cursor in (SubmittedAt DESC, AccountId DESC) order — an indexed range scan.
        // Fetch one extra row to learn whether a further page exists without a second query.
        if (OnboardingPageToken.TryDecode(pageToken, out DateTimeOffset cursorSubmitted, out string cursorId))
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

        var accounts = new List<OnboardingAccountRecord>(pageSize);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(ReadRecord(reader));
        }

        if (accounts.Count > pageSize)
        {
            OnboardingAccountRecord last = accounts[pageSize - 1];
            accounts.RemoveAt(pageSize);
            return (accounts, OnboardingPageToken.Encode(last.SubmittedAt, last.AccountId));
        }

        return (accounts, null);
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

    private static OnboardingAccountRecord ReadRecord(NpgsqlDataReader reader) => new(
        AccountId: reader.GetString(0),
        Status: reader.GetString(1),
        Email: reader.IsDBNull(2) ? null : reader.GetString(2),
        Plan: reader.IsDBNull(3) ? null : reader.GetString(3),
        Provisioning: reader.IsDBNull(4) ? null : reader.GetFieldValue<byte[]>(4),
        SubmittedAt: reader.GetFieldValue<DateTimeOffset>(5),
        ProvisionedAt: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
        WelcomedAt: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));

    private const string SelectColumns =
        "SELECT AccountId, Status, Email, Plan, Provisioning, SubmittedAt, ProvisionedAt, WelcomedAt FROM Accounts";

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Accounts (
            AccountId TEXT NOT NULL PRIMARY KEY,
            Status TEXT NOT NULL,
            Email TEXT,
            Plan TEXT,
            Provisioning BYTEA,
            SubmittedAt TIMESTAMPTZ NOT NULL,
            ProvisionedAt TIMESTAMPTZ,
            WelcomedAt TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS ix_accounts_submitted ON Accounts (SubmittedAt DESC, AccountId DESC);
        """;
}
