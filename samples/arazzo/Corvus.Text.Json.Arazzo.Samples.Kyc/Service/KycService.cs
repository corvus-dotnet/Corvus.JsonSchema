// <copyright file="KycService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Samples.Kyc.Models;

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// The real KYC service: a stateful implementation of the generated KYC API. It owns all identity verification — the
/// synchronous <c>verifyIdentity</c> the onboard-customer workflow calls (run through a KYC policy and persisted as a
/// verification record) — so the workflow orchestrates a genuine KYC backend, and the KYC console reads real
/// verifications, rather than the onboarding service conflating accounts with identity.
/// </summary>
/// <remarks>
/// Every response is a generated, schema-validated model (the generated endpoint middleware re-validates each body).
/// </remarks>
public sealed class KycService : IApiDefaultHandler
{
    private readonly KycStore store;
    private readonly IdentityVerificationPolicy policy;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="KycService"/> class.</summary>
    /// <param name="store">The verification store (the service's own database).</param>
    /// <param name="policy">The identity-verification (KYC) policy.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public KycService(KycStore store, IdentityVerificationPolicy policy, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<VerifyIdentityResult> HandleVerifyIdentityAsync(VerifyIdentityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        (string fullName, string? documentNumber) = ReadApplicant(parameters.Body);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        IdentityOutcome outcome = this.policy.Evaluate(accountId, fullName, documentNumber, now);
        await this.store.RecordVerificationAsync(accountId, outcome.Status, outcome.FullName, outcome.ApplicantBytes, outcome.IdentityBytes, now, now, "synchronous", cancellationToken).ConfigureAwait(false);

        var doc = ParsedJsonDocument<Models.IdentityResult>.Parse(outcome.IdentityBytes);
        workspace.TakeOwnership(doc);
        return VerifyIdentityResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<GetVerificationResult> HandleGetVerificationAsync(GetVerificationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        VerificationRecord? record = await this.store.GetAsync(accountId, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return GetVerificationResult.NotFound();
        }

        byte[] view = KycJson.Serialize(writer => WriteVerificationView(writer, record));
        var doc = ParsedJsonDocument<Models.VerificationView>.Parse(view);
        workspace.TakeOwnership(doc);
        return GetVerificationResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListVerificationsResult> HandleListVerificationsAsync(ListVerificationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit((JsonElement)parameters.Limit);
        string? pageToken = ReadOptionalString((JsonElement)parameters.PageToken);
        (IReadOnlyList<VerificationRecord> verifications, string? nextPageToken) = await this.store.ListVerificationsAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        byte[] page = KycJson.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("verifications");
            foreach (VerificationRecord record in verifications)
            {
                WriteVerificationView(writer, record);
            }

            writer.WriteEndArray();
            if (nextPageToken is not null)
            {
                writer.WriteString("nextPageToken", nextPageToken);
            }

            writer.WriteEndObject();
        });

        var doc = ParsedJsonDocument<Models.VerificationPage>.Parse(page);
        workspace.TakeOwnership(doc);
        return ListVerificationsResult.Ok(doc.RootElement, workspace);
    }

    // Composes a VerificationView by writing the scalar fields and splicing the stored identity document.
    private static void WriteVerificationView(Utf8JsonWriter writer, VerificationRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("accountId", record.AccountId);
        writer.WriteString("status", record.Status);
        if (record.FullName is not null)
        {
            writer.WriteString("fullName", record.FullName);
        }

        writer.WriteString("submittedAt", record.SubmittedAt);
        if (record.VerifiedAt is { } verifiedAt)
        {
            writer.WriteString("verifiedAt", verifiedAt);
        }

        writer.WriteString("channel", record.Channel);
        KycJson.WriteDocumentProperty(writer, "identity", record.Identity);
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

    private static int ReadLimit(JsonElement limit)
        => limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out int value) ? value : 50;

    private static string? ReadOptionalString(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
