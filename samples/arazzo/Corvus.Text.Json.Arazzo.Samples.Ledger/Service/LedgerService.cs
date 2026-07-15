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
        ParsedJsonDocument<Models.GetLedgerOk> doc = Models.GetLedgerOk.Create(entries: ledgerEntries);
        workspace.TakeOwnership(doc);
        return LoadLedgerResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<FetchTransactionsResult> HandleFetchTransactionsAsync(FetchTransactionsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ReconciliationCounts counts = ReconciliationEngine.ComputeCounts(await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false));
        int bankTransactions = counts.BankTransactions;
        ParsedJsonDocument<Models.GetTransactionsOk> doc = Models.GetTransactionsOk.Create(count: bankTransactions);
        workspace.TakeOwnership(doc);
        return FetchTransactionsResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<MatchEntriesResult> HandleMatchEntriesAsync(MatchEntriesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        ReconciliationCounts counts = ReconciliationEngine.ComputeCounts(await this.store.GetAccountsAsync(cancellationToken).ConfigureAwait(false));
        ParsedJsonDocument<Models.PostMatchOk> doc = Models.PostMatchOk.Create(matched: counts.Matched, unmatched: counts.Unmatched);
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
        ParsedJsonDocument<Models.PostCorrectionsOk> doc = Models.PostCorrectionsOk.Create(posted: posted);
        workspace.TakeOwnership(doc);
        return PostCorrectionsResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<PublishReportResult> HandlePublishReportAsync(PublishReportParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string reportUrl = await this.store.PublishLatestAsync(this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            ?? "https://reports.ledger.example/reconciliations/none";
        ParsedJsonDocument<Models.PostReportOk> doc = Models.PostReportOk.Create(reportUrl: reportUrl);
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
        ParsedJsonDocument<Models.LedgerAccountPage> doc = Models.LedgerAccountPage.Create(
            context: page,
            accounts: Models.LedgerAccountPage.LedgerAccountArray.Build(
                page,
                static (in LedgerAccountPageContext ctx, ref Models.LedgerAccountPage.LedgerAccountArray.Builder b) =>
                {
                    // The balances are decimals; format them to UTF-8 and pass them as formatted numbers so the
                    // persisted precision carries through (a double hop would round). The scratch is a small heap
                    // array because the Sources capture the spans (stackalloc would not satisfy ref-safety).
                    byte[] numberScratch = new byte[120];
                    Span<byte> ledgerBuffer = numberScratch.AsSpan(0, 40);
                    Span<byte> bankBuffer = numberScratch.AsSpan(40, 40);
                    Span<byte> deltaBuffer = numberScratch.AsSpan(80, 40);
                    foreach (LedgerAccountRecord account in ctx.Accounts)
                    {
                        System.Buffers.Text.Utf8Formatter.TryFormat(account.LedgerBalance, ledgerBuffer, out int ledgerWritten);
                        System.Buffers.Text.Utf8Formatter.TryFormat(account.BankBalance, bankBuffer, out int bankWritten);
                        System.Buffers.Text.Utf8Formatter.TryFormat(account.Delta, deltaBuffer, out int deltaWritten);
                        b.AddItem(Models.LedgerAccount.Build(
                            account: account.Account,
                            bankBalance: Models.JsonNumber.Source.FormattedNumber(bankBuffer[..bankWritten]),
                            currency: account.Currency,
                            inSync: account.InSync,
                            ledgerBalance: Models.JsonNumber.Source.FormattedNumber(ledgerBuffer[..ledgerWritten]),
                            delta: Models.JsonNumber.Source.FormattedNumber(deltaBuffer[..deltaWritten])));
                    }
                }),
            nextPageToken: page.NextPageToken is { } token ? (Models.JsonString.Source)token : default);
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
