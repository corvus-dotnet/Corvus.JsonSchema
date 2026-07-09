// <copyright file="VerificationRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Samples.Kyc;

/// <summary>
/// A single KYC verification record as read from the store: the outcome of an identity verification for an account,
/// with the resolved applicant and identity-result documents held as the exact JSON bytes the service emitted.
/// </summary>
/// <param name="AccountId">The account the verification is for (the correlation key; the account itself is owned by the onboarding service).</param>
/// <param name="Status">The outcome (<c>verified</c>, <c>blocked</c>, or <c>pending</c>).</param>
/// <param name="FullName">The resolved applicant name.</param>
/// <param name="Applicant">The resolved applicant document (JSON), or <see langword="null"/>.</param>
/// <param name="Identity">The identity-result document (JSON), or <see langword="null"/>.</param>
/// <param name="SubmittedAt">When the verification was recorded.</param>
/// <param name="VerifiedAt">When the verdict completed, or <see langword="null"/> while pending.</param>
/// <param name="Channel">How the verdict was produced (<c>synchronous</c> or <c>manual</c>).</param>
public sealed record VerificationRecord(
    string AccountId,
    string Status,
    string? FullName,
    byte[]? Applicant,
    byte[]? Identity,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? VerifiedAt,
    string Channel);
