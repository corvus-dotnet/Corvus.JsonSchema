// <copyright file="OnboardingAccountRecord.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Samples.Onboarding;

/// <summary>
/// A single account row as read from the store: the signup facts the onboarding service owns (the customer's email
/// and chosen plan), the lifecycle columns, and the provisioning wire document (held as the exact JSON bytes the
/// service emitted, or <see langword="null"/> before provisioning). Identity verification is a separate concern
/// owned by the KYC service, so no identity is held here. The <c>AccountView</c> read model is composed by writing
/// the scalar fields and splicing the provisioning document into an envelope.
/// </summary>
/// <param name="AccountId">The account id.</param>
/// <param name="Status">The lifecycle status (<c>created</c>, <c>provisioned</c>, <c>welcomed</c>).</param>
/// <param name="Email">The customer's email, or <see langword="null"/> if none was supplied at signup.</param>
/// <param name="Plan">The chosen plan (<c>free</c>, <c>pro</c>, <c>enterprise</c>), or <see langword="null"/> if none was supplied.</param>
/// <param name="Provisioning">The provisioning document (JSON), or <see langword="null"/> before provisioning.</param>
/// <param name="SubmittedAt">When the account was created.</param>
/// <param name="ProvisionedAt">When provisioning completed, or <see langword="null"/>.</param>
/// <param name="WelcomedAt">When the welcome was sent, or <see langword="null"/>.</param>
public sealed record OnboardingAccountRecord(
    string AccountId,
    string Status,
    string? Email,
    string? Plan,
    byte[]? Provisioning,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ProvisionedAt,
    DateTimeOffset? WelcomedAt);
