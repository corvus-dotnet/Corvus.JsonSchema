// <copyright file="KycReviewHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
using NModels = Corvus.Text.Json.Arazzo.Samples.Notifications.Models;

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// The KYC service's review-request inbox. It is the consumer side of the AsyncAPI exchange: the onboard-customer
/// workflow PUBLISHES a review request to the <c>kyc.requests</c> channel (carrying the account id) and suspends; this
/// handler records each incoming request as a <c>pending</c> verification. That pending set is the manual-recovery
/// queue an operator works through — resolving one calls <c>submitVerdict</c>, which publishes the verdict onto the
/// bus and resumes the suspended run.
/// </summary>
public sealed class KycReviewHandler : IReceiveKycReviewHandler
{
    // A pending review has no verdict yet, so its identity result is an empty (all-optional) document.
    private static readonly byte[] EmptyIdentity = "{}"u8.ToArray();

    private readonly KycStore store;
    private readonly TimeProvider timeProvider;

    /// <summary>Initializes a new instance of the <see cref="KycReviewHandler"/> class.</summary>
    /// <param name="store">The verification store.</param>
    /// <param name="timeProvider">The time source; defaults to <see cref="TimeProvider.System"/>.</param>
    public KycReviewHandler(KycStore store, TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask HandleKycReviewRequestAsync(NModels.KycReviewRequestPayload payload, CancellationToken cancellationToken = default)
    {
        var element = (JsonElement)payload;
        string accountId = element.TryGetProperty("accountId"u8, out JsonElement a) && a.ValueKind == JsonValueKind.String
            ? a.GetString()!
            : throw new InvalidOperationException("A kycReviewRequest message is missing its accountId.");
        string fullName = element.TryGetProperty("fullName"u8, out JsonElement n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()!
            : "Applicant";

        byte[] applicant = KycJson.ToArray<string>(in fullName, static (writer, in name) =>
        {
            writer.WriteStartObject();
            writer.WriteString("fullName", name);
            writer.WriteEndObject();
        });

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await this.store.RecordVerificationAsync(accountId, "pending", fullName, applicant, EmptyIdentity, now, verifiedAt: null, "manual", cancellationToken).ConfigureAwait(false);
    }
}
