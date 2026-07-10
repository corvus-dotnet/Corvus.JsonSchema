// <copyright file="KycService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
using Models = Corvus.Text.Json.Arazzo.Samples.Kyc.Models;
using NModels = Corvus.Text.Json.Arazzo.Samples.Notifications.Models;

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
    private readonly PublishKycVerdictProducer verdictProducer;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="KycService"/> class.</summary>
    /// <param name="store">The verification store (the service's own database).</param>
    /// <param name="policy">The identity-verification (KYC) policy.</param>
    /// <param name="verdictProducer">The AsyncAPI producer that publishes a manual-recovery verdict onto the bus.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public KycService(KycStore store, IdentityVerificationPolicy policy, PublishKycVerdictProducer verdictProducer, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.verdictProducer = verdictProducer ?? throw new ArgumentNullException(nameof(verdictProducer));
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
    public async ValueTask<SubmitVerdictResult> HandleSubmitVerdictAsync(SubmitVerdictParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        (bool verified, double score, string? fullName) = ReadVerdict(parameters.Body);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string status = verified ? "verified" : "blocked";
        string resolvedName = fullName ?? "Applicant";

        // A manual verdict is an operator decision, so the identity result is minimal (no synthetic evidence). Composed
        // through the pooled writer into owned arrays (the store columns need byte[]); state by `in` context, static lambdas.
        var identityContext = new VerdictIdentityContext(verified, score, now);
        byte[] identity = KycJson.ToArray<VerdictIdentityContext>(in identityContext, static (writer, in c) =>
        {
            writer.WriteStartObject();
            writer.WriteBoolean("verified", c.Verified);
            writer.WriteNumber("score", c.Score);
            writer.WriteString("reviewedAt", c.ReviewedAt);
            writer.WriteEndObject();
        });
        byte[] applicant = KycJson.ToArray<string>(in resolvedName, static (writer, in name) =>
        {
            writer.WriteStartObject();
            writer.WriteString("fullName", name);
            writer.WriteEndObject();
        });

        // Persist the verdict (channel 'manual'), then PUBLISH it onto the bus. The runner consumes the message and
        // resumes the workflow run that suspended awaiting the KYC verdict — the application owns this exchange.
        await this.store.RecordVerificationAsync(accountId, status, resolvedName, applicant, identity, now, now, "manual", cancellationToken).ConfigureAwait(false);

        // CreateUnrented: the workspace is disposed after the publish await, so it must not be a thread-affine rented one.
        using JsonWorkspace payloadWorkspace = JsonWorkspace.CreateUnrented();
        NModels.KycVerdictPayload verdict = NModels.KycVerdictPayload.CreateBuilder(payloadWorkspace, accountId, score, verified).RootElement;
        await this.verdictProducer.PublishKycVerdictAsync(verdict, cancellationToken).ConfigureAwait(false);

        return SubmitVerdictResult.Accepted();
    }

    /// <inheritdoc/>
    public async ValueTask<GetVerificationResult> HandleGetVerificationAsync(GetVerificationParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        string accountId = AccountId(parameters.AccountId);
        if (await this.store.GetAsync(accountId, cancellationToken).ConfigureAwait(false) is not { } record)
        {
            return GetVerificationResult.NotFound();
        }

        ParsedJsonDocument<Models.VerificationView> doc = KycJson.ToPooledDocument<Models.VerificationView, VerificationRecord>(in record, static (writer, in r) => WriteVerificationView(writer, r));
        workspace.TakeOwnership(doc);
        return GetVerificationResult.Ok(doc.RootElement, workspace);
    }

    /// <inheritdoc/>
    public async ValueTask<ListVerificationsResult> HandleListVerificationsAsync(ListVerificationsParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        int limit = ReadLimit((JsonElement)parameters.Limit);
        string? pageToken = ReadOptionalString((JsonElement)parameters.PageToken);
        (IReadOnlyList<VerificationRecord> verifications, string? nextPageToken) = await this.store.ListVerificationsAsync(limit, pageToken, cancellationToken).ConfigureAwait(false);

        var page = new VerificationPageContext(verifications, nextPageToken);
        ParsedJsonDocument<Models.VerificationPage> doc = KycJson.ToPooledDocument<Models.VerificationPage, VerificationPageContext>(in page, static (writer, in ctx) =>
        {
            writer.WriteStartObject();
            writer.WriteStartArray("verifications");
            foreach (VerificationRecord record in ctx.Verifications)
            {
                WriteVerificationView(writer, record);
            }

            writer.WriteEndArray();
            if (ctx.NextPageToken is not null)
            {
                writer.WriteString("nextPageToken", ctx.NextPageToken);
            }

            writer.WriteEndObject();
        });
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

    private static (bool Verified, double Score, string? FullName) ReadVerdict(Models.VerdictRequest body)
    {
        bool verified = false;
        double score = 0;
        string? fullName = null;
        var element = (JsonElement)body;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("verified"u8, out JsonElement v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
            {
                verified = v.GetBoolean();
            }

            if (element.TryGetProperty("score"u8, out JsonElement s) && s.ValueKind == JsonValueKind.Number)
            {
                score = s.GetDouble();
            }

            if (element.TryGetProperty("fullName"u8, out JsonElement n) && n.ValueKind == JsonValueKind.String)
            {
                fullName = n.GetString();
            }
        }

        return (verified, score, fullName);
    }

    private static int ReadLimit(JsonElement limit)
        => limit.ValueKind == JsonValueKind.Number && limit.TryGetInt32(out int value) ? value : 50;

    private static string? ReadOptionalString(JsonElement value)
        => value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    // Carries state to the pooled compose so the write callbacks stay static (no closure allocation).
    private readonly record struct VerdictIdentityContext(bool Verified, double Score, DateTimeOffset ReviewedAt);

    private readonly record struct VerificationPageContext(IReadOnlyList<VerificationRecord> Verifications, string? NextPageToken);
}
