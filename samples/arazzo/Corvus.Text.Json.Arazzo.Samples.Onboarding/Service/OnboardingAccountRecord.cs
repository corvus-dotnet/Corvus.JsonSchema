// <copyright file="OnboardingAccountRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// A single account row as read from the store: the lifecycle columns plus the accumulated wire documents
/// (each held as the exact JSON bytes the service emitted, or <see langword="null"/> before that stage of the
/// journey). The <c>AccountView</c> read model is composed by splicing these documents into an envelope.
/// </summary>
/// <param name="AccountId">The account id.</param>
/// <param name="Status">The lifecycle status (<c>created</c>, <c>verified</c>, <c>blocked</c>, <c>provisioned</c>, <c>welcomed</c>).</param>
/// <param name="FullName">The resolved applicant name, or <see langword="null"/> before identity verification.</param>
/// <param name="Applicant">The resolved applicant document (JSON), or <see langword="null"/> before identity verification.</param>
/// <param name="Identity">The identity-result document (JSON), or <see langword="null"/> before identity verification.</param>
/// <param name="Provisioning">The provisioning document (JSON), or <see langword="null"/> before provisioning.</param>
/// <param name="SubmittedAt">When the account was created.</param>
/// <param name="VerifiedAt">When identity verification completed, or <see langword="null"/>.</param>
/// <param name="ProvisionedAt">When provisioning completed, or <see langword="null"/>.</param>
/// <param name="WelcomedAt">When the welcome was sent, or <see langword="null"/>.</param>
public sealed record OnboardingAccountRecord(
    string AccountId,
    string Status,
    string? FullName,
    byte[]? Applicant,
    byte[]? Identity,
    byte[]? Provisioning,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? VerifiedAt,
    DateTimeOffset? ProvisionedAt,
    DateTimeOffset? WelcomedAt);
