// <copyright file="ReconciliationEngine.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// The ledger service's reconciliation logic: it computes the entry/transaction counts and the discrepancy report
/// from the real account book (accounts whose ledger and bank balances disagree) rather than returning canned
/// numbers, and it seeds a deterministic account book so the mismatches are real and reproducible.
/// </summary>
/// <remarks>
/// A production reconciliation engine would stream real ledger entries and bank statements; the shape of the result
/// (counts + a typed discrepancy report) is identical, so the nightly-reconcile workflow is unchanged.
/// </remarks>
public static class ReconciliationEngine
{
    private const string ContactEmail = "reconciliation-ops@example.com";
    private const string ContactRunbook = "https://runbooks.ledger.example/reconcile";

    /// <summary>The seed account book: a fixed set of accounts, a handful carrying a bank/ledger discrepancy.</summary>
    /// <returns>The seed accounts.</returns>
    public static IReadOnlyList<LedgerAccountRecord> SeedAccounts()
    {
        string[] currencies = ["GBP", "USD", "EUR"];

        // Fixed discrepancies (account index -> bank-minus-ledger delta), chosen for a spread of severities.
        var discrepancies = new Dictionary<int, decimal>
        {
            [7] = -420.50m,      // warning
            [18] = 1875.00m,     // critical
            [23] = -6.20m,       // info
            [31] = -12480.75m,   // critical
            [44] = 58.90m,       // warning
        };

        const int count = 60;
        var accounts = new List<LedgerAccountRecord>(count);
        for (int i = 0; i < count; i++)
        {
            string account = (10_000_000 + i).ToString(CultureInfo.InvariantCulture);
            string currency = currencies[i % currencies.Length];
            decimal ledgerBalance = Math.Round(1000m + ((LedgerJson.StableHash(account) % 900000) / 100m), 2);
            decimal bankBalance = ledgerBalance + (discrepancies.TryGetValue(i, out decimal delta) ? delta : 0m);
            accounts.Add(new LedgerAccountRecord(account, currency, ledgerBalance, bankBalance));
        }

        return accounts;
    }

    /// <summary>Computes the entry/transaction counts a reconciliation over the given book would report.</summary>
    /// <param name="accounts">The account book.</param>
    /// <returns>The ledger-entry, bank-transaction, matched, and unmatched counts.</returns>
    public static ReconciliationCounts ComputeCounts(IReadOnlyList<LedgerAccountRecord> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        int ledgerEntries = 0;
        int unmatched = 0;
        foreach (LedgerAccountRecord account in accounts)
        {
            ledgerEntries += EntryCount(account.Account);
            if (!account.InSync)
            {
                unmatched += UnmatchedEntries(account.Account);
            }
        }

        return new ReconciliationCounts(ledgerEntries, ledgerEntries + unmatched, ledgerEntries - unmatched, unmatched);
    }

    /// <summary>Reconciles the account book into a complete run outcome (counts + composed discrepancy report).</summary>
    /// <param name="accounts">The account book.</param>
    /// <param name="startedAt">The moment the reconciliation runs.</param>
    /// <param name="runId">The run id to assign.</param>
    /// <returns>The reconciliation outcome, ready to persist and return.</returns>
    public static ReconciliationOutcome Reconcile(IReadOnlyList<LedgerAccountRecord> accounts, DateTimeOffset startedAt, string runId)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(runId);
        ReconciliationCounts counts = ComputeCounts(accounts);

        var discrepant = accounts.Where(a => !a.InSync).OrderBy(a => a.Account, StringComparer.Ordinal).ToList();
        decimal totalDelta = discrepant.Sum(a => a.Delta);
        string reportUrl = string.Concat("https://reports.ledger.example/reconciliations/", runId);
        var asOf = DateOnly.FromDateTime(startedAt.UtcDateTime);

        byte[] discrepancies = LedgerJson.Serialize(writer =>
        {
            writer.WriteStartArray();
            foreach (LedgerAccountRecord account in discrepant)
            {
                writer.WriteStartObject();
                writer.WriteString("account", account.Account);
                writer.WriteNumber("delta", account.Delta);
                writer.WriteString("currency", account.Currency);
                writer.WriteString("severity", Severity(account.Delta));
                writer.WriteString("firstSeen", FirstSeen(account.Account, asOf));
                writer.WritePropertyName("contact");
                writer.WriteStartObject();
                writer.WriteString("email", ContactEmail);
                writer.WriteString("runbook", ContactRunbook);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        });

        byte[] report = LedgerJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            LedgerJson.WriteDocumentProperty(writer, "discrepancies", discrepancies);
            writer.WriteStartArray("range");
            writer.WriteNumberValue(0);
            writer.WriteNumberValue(counts.BankTransactions);
            writer.WriteEndArray();
            writer.WriteNumber("totalDelta", totalDelta);
            writer.WriteString("reportUrl", reportUrl);
            writer.WriteEndObject();
        });

        return new ReconciliationOutcome(
            runId,
            startedAt,
            counts.LedgerEntries,
            counts.BankTransactions,
            counts.Matched,
            counts.Unmatched,
            totalDelta,
            0,
            counts.BankTransactions,
            discrepancies,
            report,
            reportUrl);
    }

    // Each account contributes a deterministic number of ledger entries, so a small book yields a realistic entry count.
    private static int EntryCount(string account) => 150 + (int)(LedgerJson.StableHash(account) % 120);

    // A discrepant account contributes a deterministic number of unmatched entries.
    private static int UnmatchedEntries(string account) => 8 + (int)(LedgerJson.StableHash(account) % 30);

    private static string Severity(decimal delta)
    {
        decimal magnitude = Math.Abs(delta);
        return magnitude < 50m ? "info" : magnitude < 1000m ? "warning" : "critical";
    }

    private static string FirstSeen(string account, DateOnly asOf)
        => asOf.AddDays(-1 - (int)(LedgerJson.StableHash(account) % 14)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>The entry/transaction counts of a reconciliation.</summary>
/// <param name="LedgerEntries">The number of ledger entries scanned.</param>
/// <param name="BankTransactions">The number of bank transactions scanned.</param>
/// <param name="Matched">The number of matched entries.</param>
/// <param name="Unmatched">The number of unmatched entries.</param>
public readonly record struct ReconciliationCounts(int LedgerEntries, int BankTransactions, int Matched, int Unmatched);

/// <summary>A complete reconciliation outcome, ready to persist and return.</summary>
/// <param name="RunId">The run id.</param>
/// <param name="StartedAt">When the reconciliation ran.</param>
/// <param name="LedgerEntries">The number of ledger entries scanned.</param>
/// <param name="BankTransactions">The number of bank transactions scanned.</param>
/// <param name="Matched">The number of matched entries.</param>
/// <param name="Unmatched">The number of unmatched entries.</param>
/// <param name="TotalDelta">The signed sum of all discrepancy deltas.</param>
/// <param name="RangeFrom">The first transaction sequence number scanned.</param>
/// <param name="RangeTo">The last transaction sequence number scanned.</param>
/// <param name="DiscrepanciesBytes">The discrepancies as a JSON array (as emitted).</param>
/// <param name="ReportBytes">The full DiscrepancyReport document (as emitted).</param>
/// <param name="ReportUrl">The report URL.</param>
public readonly record struct ReconciliationOutcome(
    string RunId,
    DateTimeOffset StartedAt,
    int LedgerEntries,
    int BankTransactions,
    int Matched,
    int Unmatched,
    decimal TotalDelta,
    int RangeFrom,
    int RangeTo,
    byte[] DiscrepanciesBytes,
    byte[] ReportBytes,
    string ReportUrl);
