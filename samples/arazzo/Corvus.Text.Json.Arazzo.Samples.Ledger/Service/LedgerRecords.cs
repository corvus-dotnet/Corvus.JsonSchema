// <copyright file="LedgerRecords.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// One account in the ledger book as read from the store: its ledger balance and the bank's balance for the same
/// account. When they disagree the account is a reconciliation discrepancy.
/// </summary>
/// <param name="Account">The eight-digit account number.</param>
/// <param name="Currency">The account currency (GBP/USD/EUR).</param>
/// <param name="LedgerBalance">The balance recorded in the ledger.</param>
/// <param name="BankBalance">The balance reported by the bank.</param>
public sealed record LedgerAccountRecord(string Account, string Currency, decimal LedgerBalance, decimal BankBalance)
{
    /// <summary>Gets the signed difference (bank - ledger); zero when the account reconciles.</summary>
    public decimal Delta => this.BankBalance - this.LedgerBalance;

    /// <summary>Gets a value indicating whether the ledger and bank balances agree.</summary>
    public bool InSync => this.BankBalance == this.LedgerBalance;
}

/// <summary>
/// A persisted nightly-reconcile run: the counts it computed, the discrepancies it found (held as the exact JSON
/// array the service emitted), and its correction/publication state.
/// </summary>
/// <param name="RunId">The run id.</param>
/// <param name="StartedAt">When the reconciliation ran.</param>
/// <param name="LedgerEntries">The number of ledger entries scanned.</param>
/// <param name="BankTransactions">The number of bank transactions scanned.</param>
/// <param name="Matched">The number of matched entries.</param>
/// <param name="Unmatched">The number of unmatched entries.</param>
/// <param name="TotalDelta">The signed sum of all discrepancy deltas.</param>
/// <param name="RangeFrom">The first transaction sequence number scanned.</param>
/// <param name="RangeTo">The last transaction sequence number scanned.</param>
/// <param name="Discrepancies">The discrepancies as a JSON array (as emitted).</param>
/// <param name="ReportUrl">The published report URL.</param>
/// <param name="CorrectionsPosted">The number of corrections posted (0 until postCorrections runs).</param>
/// <param name="PublishedAt">When the report was published, or <see langword="null"/>.</param>
public sealed record ReconciliationRecord(
    string RunId,
    DateTimeOffset StartedAt,
    int LedgerEntries,
    int BankTransactions,
    int Matched,
    int Unmatched,
    decimal TotalDelta,
    int RangeFrom,
    int RangeTo,
    byte[] Discrepancies,
    string ReportUrl,
    int CorrectionsPosted,
    DateTimeOffset? PublishedAt);
