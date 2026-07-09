// <copyright file="LedgerStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;
using NpgsqlTypes;

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// The ledger service's own durable store: the account book (each account's ledger and bank balances) and the
/// nightly-reconcile runs it produces, persisted to the service's own PostgreSQL database. The account book is
/// seeded deterministically on <see cref="PrepareAsync(NpgsqlDataSource, CancellationToken)"/> — a fixed set of
/// accounts, a handful of which carry a bank/ledger discrepancy — so reconciliation finds real, reproducible
/// mismatches rather than a canned answer.
/// </summary>
/// <remarks>
/// This is a real backend, not a mock: it owns its schema and data independently of the Arazzo control plane's store
/// (the microservice-owns-its-data pattern). Each operation opens a pooled connection, so it is naturally concurrent.
/// </remarks>
public sealed class LedgerStore : IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private LedgerStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the schema and seeds the account book (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists and the account book is seeded (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the schema and seeds the account book over a caller-supplied data source.</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists and the account book is seeded (idempotent).</returns>
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
    public static ValueTask<LedgerStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<LedgerStore>(new LedgerStore(dataSource, ownsDataSource: false));
    }

    /// <summary>Opens the store for operation against a connection string (the store owns the data source it creates).</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<LedgerStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<LedgerStore>(new LedgerStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Reads the whole account book (small; loaded in full for reconciliation computation).</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>Every account, ordered by account number.</returns>
    public async ValueTask<IReadOnlyList<LedgerAccountRecord>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Account, Currency, LedgerBalance, BankBalance FROM LedgerAccounts ORDER BY Account;";
        var accounts = new List<LedgerAccountRecord>();
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    /// <summary>Lists the account book with keyset pagination, ordered by account number.</summary>
    /// <param name="limit">The maximum number of accounts to return.</param>
    /// <param name="pageToken">An opaque continuation token, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of accounts and the token for the next page.</returns>
    public async ValueTask<(IReadOnlyList<LedgerAccountRecord> Accounts, string? NextPageToken)> ListAccountsAsync(int limit, string? pageToken, CancellationToken cancellationToken = default)
    {
        int pageSize = limit > 0 ? limit : 100;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        if (LedgerPageToken.TryDecode(pageToken, out string cursor))
        {
            select.CommandText = "SELECT Account, Currency, LedgerBalance, BankBalance FROM LedgerAccounts WHERE Account > @a ORDER BY Account LIMIT @take;";
            select.Parameters.AddWithValue("a", cursor);
        }
        else
        {
            select.CommandText = "SELECT Account, Currency, LedgerBalance, BankBalance FROM LedgerAccounts ORDER BY Account LIMIT @take;";
        }

        select.Parameters.AddWithValue("take", pageSize + 1);
        var accounts = new List<LedgerAccountRecord>(pageSize);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accounts.Add(ReadAccount(reader));
        }

        if (accounts.Count > pageSize)
        {
            LedgerAccountRecord last = accounts[pageSize - 1];
            accounts.RemoveAt(pageSize);
            return (accounts, LedgerPageToken.Encode(last.Account));
        }

        return (accounts, null);
    }

    /// <summary>Records a completed reconciliation run.</summary>
    /// <param name="run">The run to persist.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the run is inserted.</returns>
    public async ValueTask InsertReconciliationAsync(ReconciliationRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO Reconciliations
                (RunId, StartedAt, LedgerEntries, BankTransactions, Matched, Unmatched, TotalDelta, RangeFrom, RangeTo, Discrepancies, ReportUrl, CorrectionsPosted, PublishedAt)
            VALUES (@id, @started, @le, @bt, @m, @u, @td, @rf, @rt, @disc, @url, @cp, @pub);
            """;
        insert.Parameters.AddWithValue("id", run.RunId);
        insert.Parameters.AddWithValue("started", NpgsqlDbType.TimestampTz, run.StartedAt);
        insert.Parameters.AddWithValue("le", run.LedgerEntries);
        insert.Parameters.AddWithValue("bt", run.BankTransactions);
        insert.Parameters.AddWithValue("m", run.Matched);
        insert.Parameters.AddWithValue("u", run.Unmatched);
        insert.Parameters.AddWithValue("td", run.TotalDelta);
        insert.Parameters.AddWithValue("rf", run.RangeFrom);
        insert.Parameters.AddWithValue("rt", run.RangeTo);
        insert.Parameters.AddWithValue("disc", NpgsqlDbType.Bytea, run.Discrepancies);
        insert.Parameters.AddWithValue("url", run.ReportUrl);
        insert.Parameters.AddWithValue("cp", run.CorrectionsPosted);
        insert.Parameters.AddWithValue("pub", NpgsqlDbType.TimestampTz, (object?)run.PublishedAt ?? DBNull.Value);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Posts corrections for the most recent reconciliation whose corrections have not yet been posted.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The number of corrections posted (the run's unmatched count), or 0 if none was pending.</returns>
    public async ValueTask<int> PostCorrectionsToLatestAsync(CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE Reconciliations SET CorrectionsPosted = Unmatched
            WHERE RunId = (SELECT RunId FROM Reconciliations WHERE CorrectionsPosted = 0 ORDER BY StartedAt DESC, RunId DESC LIMIT 1)
            RETURNING Unmatched;
            """;
        object? posted = await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return posted is int value ? value : 0;
    }

    /// <summary>Publishes the report for the most recent unpublished reconciliation.</summary>
    /// <param name="publishedAt">The publication time.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The published report URL, or <see langword="null"/> when there are no runs.</returns>
    public async ValueTask<string?> PublishLatestAsync(DateTimeOffset publishedAt, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE Reconciliations SET PublishedAt = @pub
            WHERE RunId = (SELECT RunId FROM Reconciliations WHERE PublishedAt IS NULL ORDER BY StartedAt DESC, RunId DESC LIMIT 1)
            RETURNING ReportUrl;
            """;
        update.Parameters.AddWithValue("pub", NpgsqlDbType.TimestampTz, publishedAt);
        object? url = await update.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (url is string published)
        {
            return published;
        }

        // All runs already published (or a re-publish): return the latest run's URL.
        await using NpgsqlCommand latest = connection.CreateCommand();
        latest.CommandText = "SELECT ReportUrl FROM Reconciliations ORDER BY StartedAt DESC, RunId DESC LIMIT 1;";
        return await latest.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    /// <summary>Reads a single reconciliation run, or <see langword="null"/> if there is no such run.</summary>
    /// <param name="runId">The run id.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The run record, or <see langword="null"/>.</returns>
    public async ValueTask<ReconciliationRecord?> GetReconciliationAsync(string runId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = ReconciliationColumns + " WHERE RunId = @id;";
        select.Parameters.AddWithValue("id", runId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadReconciliation(reader) : null;
    }

    /// <summary>Lists reconciliation runs newest-first with keyset pagination.</summary>
    /// <param name="limit">The maximum number of runs to return.</param>
    /// <param name="pageToken">An opaque continuation token, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The page of runs and the token for the next page.</returns>
    public async ValueTask<(IReadOnlyList<ReconciliationRecord> Reconciliations, string? NextPageToken)> ListReconciliationsAsync(int limit, string? pageToken, CancellationToken cancellationToken = default)
    {
        int pageSize = limit > 0 ? limit : 50;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        if (TryDecodeReconciliationCursor(pageToken, out DateTimeOffset cursorStarted, out string cursorId))
        {
            select.CommandText = ReconciliationColumns + " WHERE (StartedAt, RunId) < (@s, @r) ORDER BY StartedAt DESC, RunId DESC LIMIT @take;";
            select.Parameters.AddWithValue("s", NpgsqlDbType.TimestampTz, cursorStarted);
            select.Parameters.AddWithValue("r", cursorId);
        }
        else
        {
            select.CommandText = ReconciliationColumns + " ORDER BY StartedAt DESC, RunId DESC LIMIT @take;";
        }

        select.Parameters.AddWithValue("take", pageSize + 1);
        var runs = new List<ReconciliationRecord>(pageSize);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            runs.Add(ReadReconciliation(reader));
        }

        if (runs.Count > pageSize)
        {
            ReconciliationRecord last = runs[pageSize - 1];
            runs.RemoveAt(pageSize);
            return (runs, LedgerPageToken.Encode($"{last.StartedAt.UtcTicks}|{last.RunId}"));
        }

        return (runs, null);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static bool TryDecodeReconciliationCursor(string? token, out DateTimeOffset startedAt, out string runId)
    {
        startedAt = default;
        runId = string.Empty;
        if (!LedgerPageToken.TryDecode(token, out string payload))
        {
            return false;
        }

        int sep = payload.IndexOf('|', StringComparison.Ordinal);
        if (sep <= 0 || !long.TryParse(payload.AsSpan(0, sep), out long ticks))
        {
            return false;
        }

        startedAt = new DateTimeOffset(ticks, TimeSpan.Zero);
        runId = payload[(sep + 1)..];
        return true;
    }

    private static LedgerAccountRecord ReadAccount(NpgsqlDataReader reader) => new(
        Account: reader.GetString(0),
        Currency: reader.GetString(1),
        LedgerBalance: reader.GetDecimal(2),
        BankBalance: reader.GetDecimal(3));

    private static ReconciliationRecord ReadReconciliation(NpgsqlDataReader reader) => new(
        RunId: reader.GetString(0),
        StartedAt: reader.GetFieldValue<DateTimeOffset>(1),
        LedgerEntries: reader.GetInt32(2),
        BankTransactions: reader.GetInt32(3),
        Matched: reader.GetInt32(4),
        Unmatched: reader.GetInt32(5),
        TotalDelta: reader.GetDecimal(6),
        RangeFrom: reader.GetInt32(7),
        RangeTo: reader.GetInt32(8),
        Discrepancies: reader.GetFieldValue<byte[]>(9),
        ReportUrl: reader.GetString(10),
        CorrectionsPosted: reader.GetInt32(11),
        PublishedAt: reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12));

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using (NpgsqlCommand schema = connection.CreateCommand())
        {
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Seed the account book (idempotent): a fixed set of accounts, a handful with a bank/ledger discrepancy.
        IReadOnlyList<LedgerAccountRecord> seed = ReconciliationEngine.SeedAccounts();
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            """
            INSERT INTO LedgerAccounts (Account, Currency, LedgerBalance, BankBalance)
            SELECT * FROM unnest(@accounts, @currencies, @ledger, @bank)
            ON CONFLICT (Account) DO NOTHING;
            """;
        insert.Parameters.AddWithValue("accounts", seed.Select(a => a.Account).ToArray());
        insert.Parameters.AddWithValue("currencies", seed.Select(a => a.Currency).ToArray());
        insert.Parameters.AddWithValue("ledger", seed.Select(a => a.LedgerBalance).ToArray());
        insert.Parameters.AddWithValue("bank", seed.Select(a => a.BankBalance).ToArray());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string ReconciliationColumns =
        "SELECT RunId, StartedAt, LedgerEntries, BankTransactions, Matched, Unmatched, TotalDelta, RangeFrom, RangeTo, Discrepancies, ReportUrl, CorrectionsPosted, PublishedAt FROM Reconciliations";

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS LedgerAccounts (
            Account TEXT NOT NULL PRIMARY KEY,
            Currency TEXT NOT NULL,
            LedgerBalance NUMERIC NOT NULL,
            BankBalance NUMERIC NOT NULL
        );
        CREATE TABLE IF NOT EXISTS Reconciliations (
            RunId TEXT NOT NULL PRIMARY KEY,
            StartedAt TIMESTAMPTZ NOT NULL,
            LedgerEntries INT NOT NULL,
            BankTransactions INT NOT NULL,
            Matched INT NOT NULL,
            Unmatched INT NOT NULL,
            TotalDelta NUMERIC NOT NULL,
            RangeFrom INT NOT NULL,
            RangeTo INT NOT NULL,
            Discrepancies BYTEA NOT NULL,
            ReportUrl TEXT NOT NULL,
            CorrectionsPosted INT NOT NULL DEFAULT 0,
            PublishedAt TIMESTAMPTZ
        );
        CREATE INDEX IF NOT EXISTS ix_recon_started ON Reconciliations (StartedAt DESC, RunId DESC);
        """;
}
