// <copyright file="LedgerService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Ledger.Models;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo.Ledger;

/// <summary>
/// A demo implementation of the generated ledger API: it returns canned, schema-valid sample responses,
/// including a discrepancy report with an array of typed discrepancies and a <c>[from, to]</c> tuple range, so
/// the nightly-reconcile workflow can run against it.
/// </summary>
public sealed class LedgerService : IApiDefaultHandler
{
    private static readonly byte[] Ledger = """{ "entries": 12453 }"""u8.ToArray();
    private static readonly byte[] Transactions = """{ "count": 12480 }"""u8.ToArray();
    private static readonly byte[] Match = """{ "matched": 12300, "unmatched": 180 }"""u8.ToArray();
    private static readonly byte[] Corrections = """{ "posted": 180 }"""u8.ToArray();
    private static readonly byte[] Report = """{ "reportUrl": "https://reports.example.com/2026-06-10" }"""u8.ToArray();

    private static readonly byte[] DiscrepancyReport = """
        {
          "discrepancies": [
            {
              "account": "00012345",
              "delta": -42.5,
              "currency": "GBP",
              "severity": "warning",
              "firstSeen": "2026-06-09",
              "contact": { "email": "ops@example.com", "runbook": "https://runbooks.example.com/reconcile" }
            }
          ],
          "range": [ 0, 12480 ],
          "totalDelta": -42.5,
          "reportUrl": "https://reports.example.com/2026-06-10"
        }
        """u8.ToArray();

    /// <inheritdoc/>
    public ValueTask<LoadLedgerResult> HandleLoadLedgerAsync(LoadLedgerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.GetLedgerOk>.Parse(Ledger);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(LoadLedgerResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<FetchTransactionsResult> HandleFetchTransactionsAsync(FetchTransactionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.GetTransactionsOk>.Parse(Transactions);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(FetchTransactionsResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<MatchEntriesResult> HandleMatchEntriesAsync(MatchEntriesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.PostMatchOk>.Parse(Match);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(MatchEntriesResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<FlagDiscrepanciesResult> HandleFlagDiscrepanciesAsync(FlagDiscrepanciesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.DiscrepancyReport>.Parse(DiscrepancyReport);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(FlagDiscrepanciesResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<PostCorrectionsResult> HandlePostCorrectionsAsync(PostCorrectionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.PostCorrectionsOk>.Parse(Corrections);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(PostCorrectionsResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<PublishReportResult> HandlePublishReportAsync(PublishReportParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.PostReportOk>.Parse(Report);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(PublishReportResult.Ok(doc.RootElement, workspace));
    }
}
