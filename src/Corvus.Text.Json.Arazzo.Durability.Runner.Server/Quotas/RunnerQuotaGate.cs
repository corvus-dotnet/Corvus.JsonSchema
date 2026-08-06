// <copyright file="RunnerQuotaGate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server.Quotas;

/// <summary>
/// What the runner API's handlers charge through: it resolves the caller's tenant and meters the request, so no handler
/// has to know that a quota is per tenant or where a tenant comes from.
/// </summary>
/// <remarks>
/// <para>
/// The resolution is the same one the claim and catalog paths already make, and it is cached for the same bounded
/// window, so on the paths that resolve bindings anyway this costs a dictionary lookup. The checkpoint path does not
/// resolve them today — it goes straight to the lease check — so this is the one place metering adds work to the
/// hottest surface, and it is a cache hit rather than a store read.
/// </para>
/// <para>
/// A <see langword="null"/> guard means the deployment enforces no quotas, and every charge is admitted.
/// </para>
/// </remarks>
public sealed class RunnerQuotaGate
{
    private readonly IRunnerEnvironmentBindings bindings;
    private readonly IRunnerQuotaGuard? guard;

    /// <summary>Initializes a new instance of the <see cref="RunnerQuotaGate"/> class.</summary>
    /// <param name="bindings">Resolves the caller's tenant, from the same read and the same staleness bound as its reach.</param>
    /// <param name="guard">The meter, or <see langword="null"/> to enforce no quotas.</param>
    public RunnerQuotaGate(IRunnerEnvironmentBindings bindings, IRunnerQuotaGuard? guard)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        this.bindings = bindings;
        this.guard = guard;
    }

    /// <summary>Gets a value indicating whether this gate meters anything.</summary>
    public bool IsEnabled => this.guard is not null;

    /// <summary>Charges one request.</summary>
    /// <param name="kind">The dimension being charged.</param>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="cost">What to charge; one for a request-counting dimension.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The refusal, or <see langword="null"/> when the request is admitted.</returns>
    public async ValueTask<RunnerQuotaRejection?> TryAcquireAsync(RunnerQuotaKind kind, string principal, long cost, CancellationToken cancellationToken)
    {
        if (this.guard is not { } meter)
        {
            return null;
        }

        RunnerBindings resolved = await this.bindings.ResolveAsync(principal, cancellationToken).ConfigureAwait(false);
        return await meter.TryAcquireAsync(kind, resolved.Tenant, principal, cost, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The <c>Retry-After</c> value for a refusal, in whole seconds.</summary>
    /// <param name="rejection">The refusal.</param>
    /// <returns>The seconds to wait, never negative.</returns>
    /// <remarks>Rounded up. Reporting a shorter wait than the deficit needs would have the caller return to a refusal it
    /// was told it would not meet, which is a spin rather than a hold.</remarks>
    public static long RetryAfterSeconds(in RunnerQuotaRejection rejection)
    {
        double seconds = Math.Ceiling(rejection.RetryAfter.TotalSeconds);
        return seconds <= 0 ? 0 : (long)seconds;
    }
}