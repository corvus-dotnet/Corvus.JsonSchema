// <copyright file="OnboardingService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.ControlPlane.Demo.Onboarding.Models;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo.Onboarding;

/// <summary>
/// A demo implementation of the generated onboarding API: it returns canned, schema-valid sample responses
/// (an account, an identity result with a discriminated <c>evidence</c> union, and a provisioning result with a
/// resources array + a tags map) so the onboard-customer workflow can actually run against it.
/// </summary>
public sealed class OnboardingService : IApiDefaultHandler
{
    private static readonly byte[] Account = """
        { "accountId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301" }
        """u8.ToArray();

    private static readonly byte[] IdentityResult = """
        {
          "verified": true,
          "score": 0.92,
          "method": "document",
          "reviewedAt": "2026-06-10T09:30:00Z",
          "applicant": { "fullName": "Ada Lovelace", "dateOfBirth": "1990-12-10", "email": "ada@example.com", "country": "GB" },
          "flags": [],
          "evidence": { "kind": "document", "documentType": "passport", "documentNumber": "X1234567", "expiry": "2031-01-01" }
        }
        """u8.ToArray();

    private static readonly byte[] Provisioning = """
        {
          "accountUrl": "https://app.example.com/accounts/3f2504e0",
          "quotaGb": 50,
          "resources": [
            { "kind": "database", "name": "primary", "region": "eu-west-1", "endpoint": "https://db.example.com/primary" },
            { "kind": "bucket", "name": "assets", "region": "eu-west-1", "endpoint": "https://s3.example.com/assets" }
          ],
          "tags": { "env": "prod", "team": "identity" }
        }
        """u8.ToArray();

    /// <inheritdoc/>
    public ValueTask<CreateAccountResult> HandleCreateAccountAsync(CreateAccountParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.Account>.Parse(Account);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(CreateAccountResult.Created(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<VerifyIdentityResult> HandleVerifyIdentityAsync(VerifyIdentityParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.IdentityResult>.Parse(IdentityResult);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(VerifyIdentityResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<ProvisionResourcesResult> HandleProvisionResourcesAsync(ProvisionResourcesParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        var doc = ParsedJsonDocument<Models.Provisioning>.Parse(Provisioning);
        workspace.TakeOwnership(doc);
        return ValueTask.FromResult(ProvisionResourcesResult.Ok(doc.RootElement, workspace));
    }

    /// <inheritdoc/>
    public ValueTask<SendWelcomeResult> HandleSendWelcomeAsync(SendWelcomeParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(SendWelcomeResult.Accepted());
}
