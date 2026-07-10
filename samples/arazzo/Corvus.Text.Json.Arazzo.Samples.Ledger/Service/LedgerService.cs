// <copyright file="LedgerService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Samples.Ledger.Models;

namespace Corvus.Text.Json.Arazzo.Samples.Ledger;

/// <summary>
/// The real ledger/reconciliation service: a stateful implementation of the generated ledger API. It reconciles a
/// real account book (accounts whose ledger and bank balances disagree) and persists each nightly-reconcile run — so
/// the nightly-reconcile workflow orchestrates a genuine backend, and a reconciliation console can read real runs,
/// rather than either hitting canned numbers.
/// </summary>
/// <remarks>
/// The six workflow operations (load -> fetch -> match -> flag -> correct -> publish) compute their results from the
/// current account book; flagDiscrepancies persists a run, and correct/publish advance the most recent run. The three
/// read operations (list/get reconciliations, list accounts) expose that state. Every response is a generated,
/// schema-validated model (the generated endpoint middleware re-validates each body).
/// </remarks>
public sealed class LedgerService : IApiDefaultHandler
{
    private readonly LedgerStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="LedgerService"/> class.</summary>
    /// <param name="store">The ledger store (the service's own database).</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public LedgerService(LedgerStore store, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<LoadLedgerResult> HandleLoadLedgerAsync(LoadLedgerParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ReconciliationCounts counts = ReconciliationEngine.ComputeCounts(await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false));
        int ledgerEntries = counts.LedgerEntries;
        ParsedJsonDocument<Models.GetLedgerOk> doc = LedgerJson.ToPooledDocument<Models.GetLedgerOk, int>(in ledgerEntries, static (writer, in entries) =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("entries", entries);
            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return LoadLedgerResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<FetchTransactionsResult> HandleFetchTransactionsAsync(FetchTransactionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ReconciliationCounts counts = ReconciliationEngine.ComputeCounts(await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false));
        int bankTransactions = counts.BankTransactions;
        ParsedJsonDocument<Models.GetTransactionsOk> doc = LedgerJson.ToPooledDocument<Models.GetTransactionsOk, int>(in bankTransactions, static (writer, in count) =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("count", count);
            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return FetchTransactionsResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<MatchEntriesResult> HandleMatchEntriesAsync(MatchEntriesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ReconciliationCounts counts = ReconciliationEngine.ComputeCounts(await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false));
        var match = new MatchContext(counts.Matched, counts.Unmatched);
        ParsedJsonDocument<Models.PostMatchOk> doc = LedgerJson.ToPooledDocument<Models.PostMatchOk, MatchContext>(in match, static (writer, in c) =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("matched", c.Matched);
            writer.WriteNumber("unmatched", c.Unmatched);
            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return MatchEntriesResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<FlagDiscrepanciesResult> HandleFlagDiscrepanciesAsync(FlagDiscrepanciesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LedgerAccountRecord> accounts = await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false);
        ReconciliationOutcome outcome = ReconciliationEngine.Reconcile(accounts, this.timeProvider.GetUtcNow(), Guid.NewGuid().ToString());
        await this.store.InsertReconciliationAsync(
            new ReconciliationRecord(
                outcome.RunId,
                outcome.StartedAt,
                outcome.LedgerEntries,
                outcome.BankTransactions,
                outcome.Matched,
                outcome.Unmatched,
                outcome.TotalDelta,
                outcome.RangeFrom,
                outcome.RangeTo,
                outcome.DiscrepanciesBytes,
                outcome.ReportUrl,
                CorrectionsPosted: 0,
                PublishedAt: null),
            cancellationToken).ConfigureAwait(false);

        var doc = ParsedJsonDocument<Models.DiscrepancyReport>.Parse(outcome.ReportBytes);
        workspace.TakeOwnership(doc);
        return FlagDiscrepanciesResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PostCorrectionsResult> HandlePostCorrectionsAsync(PostCorrectionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int posted = await this.store.PostCorrectionsToLatestAsync(cancellationToken).ConfigureAwait(false);
        ParsedJsonDocument<Models.PostCorrectionsOk> doc = LedgerJson.ToPooledDocument<Models.PostCorrectionsOk, int>(in posted, static (writer, in p) =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("posted", p);
            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return PostCorrectionsResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PublishReportResult> HandlePublishReportAsync(PublishReportParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string reportUrl = await this.store.PublishLatestAsync(this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            ?? "https://reports.ledger.example/reconciliations/none";
        ParsedJsonDocument<Models.PostReportOk> doc = LedgerJson.ToPooledDocument<Models.PostReportOk, string>(in reportUrl, static (writer, in url) =>
        {
            writer.WriteStartObject();
            writer.WriteString("reportUrl", url);
            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return PublishReportResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListReconciliationsResult> HandleListReconciliationsAsync(ListReconciliationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit((JsonElement)parameters.Limit);
        string? pageToken = ReadOptionalString((JsonElement)parameters.PageToken);
        (IReadOnlyList<ReconciliationRecord> reconciliations, string? nextPageToken) = await this.store.ListReconciliationsAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        var page = new ReconciliationPageContext(reconciliations, nextPageToken);
        ParsedJsonDocument<Models.ReconciliationPage> doc = LedgerJson.ToPooledDocument<Models.ReconciliationPage, ReconciliationPageContext>(in page, static (writer, in ctx) =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("reconciliations");
            foreach (ReconciliationRecord run in ctx.Reconciliations)
            {
                WriteReconciliationView(writer, run);
            }

            writer.WriteEndArray();
            if (ctx.NextPageToken is not null)
            {
                writer.WriteString("nextPageToken", ctx.NextPageToken);
            }

            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return ListReconciliationsResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetReconciliationResult> HandleGetReconciliationAsync(GetReconciliationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string runId = ((JsonElement)parameters.RunId).GetString() ?? throw new InvalidOperationException("The runId path parameter is required.");
        if (await this.store.GetReconciliationAsync(runId, cancellationToken).ConfigureAwait(false) is not { } run)
        {
            return GetReconciliationResult.NotFound();
        }

        ParsedJsonDocument<Models.ReconciliationView> doc = LedgerJson.ToPooledDocument<Models.ReconciliationView, ReconciliationRecord>(in run, static (writer, in r) => WriteReconciliationView(writer, r));
        workspace.TakeOwnership(doc);
        return GetReconciliationResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListLedgerAccountsResult> HandleListLedgerAccountsAsync(ListLedgerAccountsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit((JsonElement)parameters.Limit);
        string? pageToken = ReadOptionalString((JsonElement)parameters.PageToken);
        (IReadOnlyList<LedgerAccountRecord> accounts, string? nextPageToken) = await this.store.ListAccountsAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        var page = new LedgerAccountPageContext(accounts, nextPageToken);
        ParsedJsonDocument<Models.LedgerAccountPage> doc = LedgerJson.ToPooledDocument<Models.LedgerAccountPage, LedgerAccountPageContext>(in page, static (writer, in ctx) =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("accounts");
            foreach (LedgerAccountRecord account in ctx.Accounts)
            {
                writer.WriteStartObject();
                writer.WriteString("account", account.Account);
                writer.WriteString("currency", account.Currency);
                writer.WriteNumber("ledgerBalance", account.LedgerBalance);
                writer.WriteNumber("bankBalance", account.BankBalance);
                writer.WriteNumber("delta", account.Delta);
                writer.WriteBoolean("inSync", account.InSync);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (ctx.NextPageToken is not null)
            {
                writer.WriteString("nextPageToken", ctx.NextPageToken);
            }

            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return ListLedgerAccountsResult.Ok(doc.RootElement, workspace);
    }

    // Composes a ReconciliationView by writing the run's scalar fields and splicing its stored discrepancies array.
    private static void WriteReconciliationView(Utf8JsonWriter writer, ReconciliationRecord run)
    {
        writer.WriteStartObject();
        writer.WriteString("runId", run.RunId);
        writer.WriteString("startedAt", run.StartedAt);
        writer.WriteNumber("ledgerEntries", run.LedgerEntries);
        writer.WriteNumber("bankTransactions", run.BankTransactions);
        writer.WriteNumber("matched", run.Matched);
        writer.WriteNumber("unmatched", run.Unmatched);
        writer.WriteNumber("totalDelta", run.TotalDelta);
        LedgerJson.WriteDocumentProperty(writer, "discrepancies", run.Discrepancies);
        writer.WriteString("reportUrl", run.ReportUrl);
        writer.WriteNumber("correctionsPosted", run.CorrectionsPosted);
        writer.WriteBoolean("published", run.PublishedAt is not null);
        if (run.PublishedAt is { } publishedAt)
        {
            writer.WriteString("publishedAt", publishedAt);
        }

        writer.WriteEndObject();
    }

    private static int ReadLimit(JsonElement limit)
        => limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out int value) ? value : 50;

    private static string? ReadOptionalString(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Carry the compose state to the pooled writer so the write callbacks stay static (no closure allocation).
    private readonly record struct MatchContext(int Matched, int Unmatched);

    private readonly record struct ReconciliationPageContext(IReadOnlyList<ReconciliationRecord> Reconciliations, string? NextPageToken);

    private readonly record struct LedgerAccountPageContext(IReadOnlyList<LedgerAccountRecord> Accounts, string? NextPageToken);
}
