// <copyright file="OnboardingService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Samples.Onboarding.Models;

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// The real onboarding service: a stateful implementation of the generated onboarding API. It persists customer
/// accounts to its own store, verifies identity through a KYC policy, and provisions per-account resources — so the
/// onboard-customer workflow orchestrates a genuine backend, and the onboarding console reads real state, rather
/// than either hitting canned responses.
/// </summary>
/// <remarks>
/// The four write operations (create -> verify -> provision -> welcome) advance an account through its lifecycle and
/// each persist the wire document they emit; the two read operations (list, get) compose the <c>AccountView</c>
/// aggregate from the stored documents. Every response is a generated, schema-validated model (the generated
/// endpoint middleware re-validates each body), so the contract stays type-checked end to end.
/// </remarks>
public sealed class OnboardingService : IApiDefaultHandler
{
    private readonly OnboardingAccountStore store;
    private readonly IdentityVerificationPolicy policy;
    private readonly ResourceAllocator allocator;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="OnboardingService"/> class.</summary>
    /// <param name="store">The account store (the service's own database).</param>
    /// <param name="policy">The identity-verification (KYC) policy.</param>
    /// <param name="allocator">The resource allocator.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public OnboardingService(OnboardingAccountStore store, IdentityVerificationPolicy policy, ResourceAllocator allocator, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.allocator = allocator ?? throw new ArgumentNullException(nameof(allocator));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<CreateAccountResult> HandleCreateAccountAsync(CreateAccountParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = Guid.NewGuid().ToString();
        await this.store.CreateAsync(accountId, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        byte[] account = OnboardingJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("accountId", accountId);
            writer.WriteEndObject();
        });

        // The response Body is materialised into the workspace but references this parsed document, so the workspace
        // must own it — it is disposed only after the endpoint middleware has validated and written the response.
        var doc = ParsedJsonDocument<Models.Account>.Parse(account);
        workspace.TakeOwnership(doc);
        return CreateAccountResult.Created(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<VerifyIdentityResult> HandleVerifyIdentityAsync(VerifyIdentityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        (string fullName, string? documentNumber) = ReadApplicant(parameters.Body);

        IdentityOutcome outcome = this.policy.Evaluate(accountId, fullName, documentNumber, this.timeProvider.GetUtcNow());
        await this.store.RecordIdentityAsync(accountId, outcome.Status, outcome.FullName, outcome.ApplicantBytes, outcome.IdentityBytes, this.timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);

        var doc = ParsedJsonDocument<Models.IdentityResult>.Parse(outcome.IdentityBytes);
        workspace.TakeOwnership(doc);
        return VerifyIdentityResult.Ok(doc.RootElement, workspace);
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
        OnboardingAccountRecord? record = await this.store.GetAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return GetAccountResult.NotFound();
        }

        byte[] view = OnboardingJson.Serialize(writer => WriteAccountView(writer, record));
        var doc = ParsedJsonDocument<Models.AccountView>.Parse(view);
        workspace.TakeOwnership(doc);
        return GetAccountResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListAccountsResult> HandleListAccountsAsync(ListAccountsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit(parameters.Limit);
        string? pageToken = ReadOptionalString(parameters.PageToken);

        (IReadOnlyList<OnboardingAccountRecord> accounts, string? nextPageToken) = await this.store.ListAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        byte[] page = OnboardingJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("accounts");
            foreach (OnboardingAccountRecord account in accounts)
            {
                WriteAccountView(writer, account);
            }

            writer.WriteEndArray();
            if (nextPageToken is not null)
            {
                writer.WriteString("nextPageToken", nextPageToken);
            }

            writer.WriteEndObject();
        });

        var doc = ParsedJsonDocument<Models.AccountPage>.Parse(page);
        workspace.TakeOwnership(doc);
        return ListAccountsResult.Ok(doc.RootElement, workspace);
    }

    // Composes the AccountView aggregate by writing the envelope fields and splicing the stored wire documents.
    private static void WriteAccountView(Utf8JsonWriter writer, OnboardingAccountRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("accountId", record.AccountId);
        writer.WriteString("status", record.Status);
        writer.WriteString("submittedAt", record.SubmittedAt);
        OnboardingJson.WriteDocumentProperty(writer, "applicant", record.Applicant);
        OnboardingJson.WriteDocumentProperty(writer, "identity", record.Identity);
        OnboardingJson.WriteDocumentProperty(writer, "provisioning", record.Provisioning);
        if (record.VerifiedAt is { } verifiedAt)
        {
            writer.WriteString("verifiedAt", verifiedAt);
        }

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

    private static string AccountId(Models.JsonString accountId)
        => ((JsonElement)accountId).GetString() ?? throw new InvalidOperationException("The accountId path parameter is required.");

    private static (string FullName, string? DocumentNumber) ReadApplicant(Models.IdentityRequest body)
    {
        string fullName = "Applicant";
        string? documentNumber = null;
        var element = (JsonElement)body;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("fullName"u8, out JsonElement name) && name.ValueKind == JsonValueKind.String)
            {
                fullName = name.GetString()!;
            }

            if (element.TryGetProperty("documentNumber"u8, out JsonElement document) && document.ValueKind == JsonValueKind.String)
            {
                documentNumber = document.GetString();
            }
        }

        return (fullName, documentNumber);
    }

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
}
