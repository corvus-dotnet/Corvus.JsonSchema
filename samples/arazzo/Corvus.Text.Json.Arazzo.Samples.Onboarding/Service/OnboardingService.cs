// <copyright file="OnboardingService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Samples.Onboarding.Models;

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// The real onboarding service: a stateful implementation of the generated onboarding API. It persists customer
/// accounts (with the signup facts it owns — email and chosen plan) to its own store and provisions per-account
/// resources — so the onboard-customer workflow orchestrates a genuine backend, and the onboarding console reads
/// real state, rather than either hitting canned responses. Identity verification is a separate concern owned by
/// the KYC service (the workflow's verifyIdentity step resolves there), not conflated into onboarding.
/// </summary>
/// <remarks>
/// The three write operations (create -> provision -> welcome) advance an account through its lifecycle; provision
/// persists the wire document it emits. The two read operations (list, get) compose the <c>AccountView</c> aggregate
/// from the stored row + document. Every response is a generated, schema-validated model (the generated endpoint
/// middleware re-validates each body), so the contract stays type-checked end to end.
/// </remarks>
public sealed class OnboardingService : IApiDefaultHandler
{
    private readonly OnboardingAccountStore store;
    private readonly ResourceAllocator allocator;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OnboardingService"/> class.</summary>
    /// <param name="store">The account store (the service's own database).</param>
    /// <param name="allocator">The resource allocator.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public OnboardingService(OnboardingAccountStore store, ResourceAllocator allocator, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<CreateAccountResult> HandleCreateAccountAsync(CreateAccountParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = Guid.NewGuid().ToString();
        (string? email, string? plan) = ReadSignup(parameters.Body);
        await this.store.CreateAsync(accountId, email, plan, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        // The generated Create() realises the body (text + parse metadata) in one pooled pass. The response Body
        // references this document, so the workspace owns it — disposed only after the endpoint middleware has
        // validated and written the response.
        ParsedJsonDocument<Models.Account> doc = Models.Account.Create(accountId: accountId);
        workspace.TakeOwnership(doc);
        return CreateAccountResult.Created(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ProvisionResourcesResult> HandleProvisionResourcesAsync(ProvisionResourcesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        byte[] provisioning = this.allocator.Allocate(accountId);
        await this.store.RecordProvisioningAsync(accountId, provisioning, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        var doc = ParsedJsonDocument<Models.Provisioning>.Parse(provisioning);
        workspace.TakeOwnership(doc);
        return ProvisionResourcesResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<SendWelcomeResult> HandleSendWelcomeAsync(SendWelcomeParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);

        // Phase A records the welcome as durable state (the account moves to 'welcomed'). Delivering the welcome
        // through a real message broker is the messaging gap (design §8); the durable state change stands in until then.
        await this.store.RecordWelcomeAsync(accountId, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return SendWelcomeResult.Accepted();
    }

    /// <inheritdoc/>
    public async ValueTask<GetAccountResult> HandleGetAccountAsync(GetAccountParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        if (await this.store.GetAsync(accountId, cancellationToken).ConfigureAwait(false) is not { } record)
        {
            return GetAccountResult.NotFound();
        }

        ParsedJsonDocument<Models.AccountView> doc = OnboardingJson.ToPooledDocument<Models.AccountView, OnboardingAccountRecord>(in record, static (writer, in r) => WriteAccountView(writer, r));
        workspace.TakeOwnership(doc);
        return GetAccountResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListAccountsResult> HandleListAccountsAsync(ListAccountsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit(parameters.Limit);
        string? pageToken = ReadOptionalString(parameters.PageToken);

        (IReadOnlyList<OnboardingAccountRecord> accounts, string? nextPageToken) = await this.store.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        var page = new AccountPageContext(accounts, nextPageToken);
        ParsedJsonDocument<Models.AccountPage> doc = OnboardingJson.ToPooledDocument<Models.AccountPage, AccountPageContext>(in page, static (writer, in ctx) =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("accounts");
            foreach (OnboardingAccountRecord account in ctx.Accounts)
            {
                WriteAccountView(writer, account);
            }

            writer.WriteEndArray();
            if (ctx.NextPageToken is not null)
            {
                writer.WriteString("nextPageToken", ctx.NextPageToken);
            }

            writer.WriteEndObject();
        });
        workspace.TakeOwnership(doc);
        return ListAccountsResult.Ok(doc.RootElement, workspace);
    }

    // Composes the AccountView aggregate by writing the envelope fields and splicing the stored provisioning document.
    private static void WriteAccountView(Utf8JsonWriter writer, OnboardingAccountRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("accountId", record.AccountId);
        writer.WriteString("status", record.Status);
        if (record.Email is not null)
        {
            writer.WriteString("email", record.Email);
        }

        if (record.Plan is not null)
        {
            writer.WriteString("plan", record.Plan);
        }

        writer.WriteString("submittedAt", record.SubmittedAt);
        OnboardingJson.WriteDocumentProperty(writer, "provisioning", record.Provisioning);
        if (record.ProvisionedAt is { } provisionedAt)
        {
            writer.WriteString("provisionedAt", provisionedAt);
        }

        if (record.WelcomedAt is { } welcomedAt)
        {
            writer.WriteString("welcomedAt", welcomedAt);
        }

        writer.WriteEndObject();
    }

    // Reads the signup facts the onboarding service owns (email + chosen plan) from the createAccount request body.
    private static (string? Email, string? Plan) ReadSignup(Models.CreateAccountRequest body)
    {
        string? email = null;
        string? plan = null;
        var element = (JsonElement)body;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("email"u8, out JsonElement e) && e.ValueKind == JsonValueKind.String)
            {
                email = e.GetString();
            }

            if (element.TryGetProperty("plan"u8, out JsonElement p) && p.ValueKind == JsonValueKind.String)
            {
                plan = p.GetString();
            }
        }

        return (email, plan);
    }

    private static string AccountId(Models.JsonString accountId)
        => ((JsonElement)accountId).GetString() ?? throw new InvalidOperationException("The accountId path parameter is required.");

    private static int ReadLimit(Models.GetAccountsLimit limit)
    {
        var element = (JsonElement)limit;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int value) ? value : 50;
    }

    private static string? ReadOptionalString(Models.JsonString value)
    {
        var element = (JsonElement)value;
        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    // Carries the list-page state to the pooled compose so the write callback stays static (no closure allocation).
    private readonly record struct AccountPageContext(IReadOnlyList<OnboardingAccountRecord> Accounts, string? NextPageToken);
}
