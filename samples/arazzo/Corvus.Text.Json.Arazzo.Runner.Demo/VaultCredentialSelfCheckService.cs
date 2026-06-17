// <copyright file="VaultCredentialSelfCheckService.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Vault;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.Core;

namespace Corvus.Text.Json.Arazzo.Runner.Demo;

/// <summary>
/// A one-off startup self-check (design §13.5) that proves the runner's §13 secret-consumer wiring end to end
/// against the real Vault: for every seeded credential binding it resolves the secret reference the control plane
/// registered, using ONLY the runner's read-only token, then asserts that the same token is <em>refused</em> a
/// write (HTTP 403). Resolution succeeding + write being denied together demonstrate the separation-of-duties
/// boundary is real — the runner reads its scoped secrets and can do nothing else.
/// </summary>
/// <remarks>
/// This is not production behaviour; it is the Vault equivalent of the dispatch smoke test. In production the
/// runner resolves credentials at transport-bind time during live execution (the paused phase), never on a
/// timer, and its identity comes from platform attestation/AppRole rather than an injected token.
/// </remarks>
public sealed class VaultCredentialSelfCheckService(
    ISourceCredentialStore credentials,
    IConfiguration configuration,
    ILogger<VaultCredentialSelfCheckService> logger) : BackgroundService
{
    // The demo sources whose bindings the control plane seeds; kept in sync with the AppHost's provisioned paths.
    private static readonly string[] DemoSources = ["onboarding", "ledger"];
    private const string DemoEnvironment = "production";

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? address = configuration["VAULT_ADDR"];
        string? token = configuration["VAULT_TOKEN"];
        if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(token))
        {
            logger.LogInformation("No Vault configured (VAULT_ADDR/VAULT_TOKEN unset); skipping the credential self-check.");
            return;
        }

        try
        {
            IAuthMethodInfo auth = new TokenAuthMethodInfo(token);
            IVaultClient client = new VaultClient(new VaultClientSettings(address, auth));
            ISecretResolver resolver = new SecretResolverBuilder().AddHashiCorpVault(client).Build();

            await this.ResolveSeededReferencesAsync(resolver, stoppingToken).ConfigureAwait(false);
            await this.AssertWriteRefusedAsync(client).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Vault credential self-check failed.");
        }
    }

    // Resolve every reference on every seeded binding — the faithful runner-side path: store → reference → Vault.
    private async Task ResolveSeededReferencesAsync(ISecretResolver resolver, CancellationToken cancellationToken)
    {
        int resolved = 0;
        foreach (string source in DemoSources)
        {
            using ParsedJsonDocument<SourceCredentialBinding>? binding =
                await credentials.GetAsync(source, DemoEnvironment, AccessContext.System, cancellationToken).ConfigureAwait(false);
            if (binding is null)
            {
                continue;
            }

            foreach (var reference in binding.RootElement.SecretRefs.EnumerateArray())
            {
                SecretRef secretRef = SecretRef.Parse((string)reference.Ref);

                // The material is owned + scrubbed here; the value is never logged — only that resolution succeeded.
                using SecretMaterial material = await resolver.ResolveAsync(secretRef, cancellationToken).ConfigureAwait(false);
                resolved++;
                logger.LogInformation(
                    "Resolved '{Role}' for source '{Source}' from {Reference} ({Bytes} bytes) using the read-only token.",
                    (string)reference.Name,
                    source,
                    secretRef.Raw,
                    material.Utf8.Length);
            }
        }

        logger.LogInformation("Credential self-check: resolved {Count} secret reference(s) against Vault read-only.", resolved);
    }

    // Prove least privilege: the read-only token MUST be refused a write. A successful write is a security defect.
    private async Task AssertWriteRefusedAsync(IVaultClient client)
    {
        try
        {
            await client.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                "arazzo/onboarding",
                new Dictionary<string, object> { ["api-key"] = "tampered-by-runner" },
                mountPoint: "secret").ConfigureAwait(false);

            // Reached only if the write was NOT refused — the runner's token is over-privileged.
            logger.LogError(
                "SECURITY: the runner's token was able to WRITE to Vault — it must be read-only (separation of duties, §13.5).");
        }
        catch (VaultApiException ex)
        {
            // A read-only token is denied write (403) — the expected, healthy outcome.
            if ((int)ex.HttpStatusCode == 403)
            {
                logger.LogInformation("Least privilege confirmed: the read-only token was refused write access (HTTP 403).");
            }
            else
            {
                logger.LogWarning("Write-denied self-check got an unexpected Vault status {Status}.", ex.HttpStatusCode);
            }
        }
    }
}
