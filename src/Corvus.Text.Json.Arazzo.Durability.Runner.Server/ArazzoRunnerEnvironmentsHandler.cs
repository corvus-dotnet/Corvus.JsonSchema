// <copyright file="ArazzoRunnerEnvironmentsHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;
using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The runner API's environments surface (ADR 0065 decision 10): what the control plane advertises about an
/// environment a runner serves, for the runner to check against the allowlist it holds. Today that is the seal key
/// generations, which a runner compares to the fingerprint the tenant pinned; the answer is a claim the pin checks,
/// never an authority.
/// </summary>
public sealed class ArazzoRunnerEnvironmentsHandler : IApiEnvironmentsHandler
{
    private readonly IRunnerEnvironmentBindings bindings;
    private readonly RunnerPrincipalAccessor principals;
    private readonly RunnerQuotaGate quotas;

    /// <summary>Initializes a new instance of the <see cref="ArazzoRunnerEnvironmentsHandler"/> class.</summary>
    /// <param name="bindings">The environments each principal is bound to, with what the deployment advertises for each.</param>
    /// <param name="principals">Resolves the caller's machine principal.</param>
    /// <param name="quotas">The per-tenant and per-runner quotas.</param>
    public ArazzoRunnerEnvironmentsHandler(IRunnerEnvironmentBindings bindings, RunnerPrincipalAccessor principals, RunnerQuotaGate quotas)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(principals);
        ArgumentNullException.ThrowIfNull(quotas);
        this.bindings = bindings;
        this.principals = principals;
        this.quotas = quotas;
    }

    /// <inheritdoc/>
    public async ValueTask<GetEnvironmentSealKeyResult> HandleGetEnvironmentSealKeyAsync(GetEnvironmentSealKeyParams parameters, JsonWorkspace workspace, CancellationToken cancellationToken = default)
    {
        if (this.principals.Resolve() is not { } principal)
        {
            return GetEnvironmentSealKeyResult.Forbidden(RunnerProblems.NoPrincipal(), workspace);
        }

        if (await this.quotas.TryAcquireAsync(RunnerQuotaKind.Catalog, principal, 1, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return GetEnvironmentSealKeyResult.TooManyRequests(RunnerProblems.QuotaExceeded(refused), workspace, RunnerQuotaGate.RetryAfterSeconds(refused));
        }

        string environment = (string)parameters.Environment;
        RunnerBindings bound = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        if (!bound.Environments.Contains(environment, StringComparer.Ordinal))
        {
            // Not bound and not there are indistinguishable on purpose: the answer says nothing about environments
            // the principal does not serve.
            return GetEnvironmentSealKeyResult.NotFound(RunnerProblems.NoSuchDocument(), workspace);
        }

        IReadOnlyList<RunnerSealKeyGeneration> generations = bound.SealKeysOf(environment) ?? [];
        return GetEnvironmentSealKeyResult.Ok(
            EnvironmentSealKeys.Build(
                in generations,
                environment,
                EnvironmentSealKeys.EnvironmentSealKeyGenerationArray.Build(
                    in generations,
                    static (in IReadOnlyList<RunnerSealKeyGeneration> source, ref EnvironmentSealKeys.EnvironmentSealKeyGenerationArray.Builder builder) =>
                    {
                        foreach (RunnerSealKeyGeneration generation in source)
                        {
                            // The rotation link travels with the generation (decision 12), so the runner can verify
                            // the chain from a successor back to the fingerprint it pinned.
                            builder.AddItem(EnvironmentSealKeyGeneration.Build(
                                keyId: generation.KeyId,
                                sealPublicKey: generation.SealPublicKey,
                                state: generation.Active ? "Active" : "Retired",
                                predecessorKeyId: generation.PredecessorKeyId is { } predecessor ? (Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models.JsonString.Source)predecessor : default,
                                rotationSignature: generation.RotationSignature is { } signature ? (Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models.JsonCorvusBase64String.Source)signature : default));
                        }
                    })),
            workspace);
    }
}