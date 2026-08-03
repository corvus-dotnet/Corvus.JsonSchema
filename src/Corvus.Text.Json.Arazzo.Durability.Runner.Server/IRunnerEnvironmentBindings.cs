// <copyright file="IRunnerEnvironmentBindings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// Resolves the deployment environments an authenticated machine principal may execute runs for (ADR 0027, ADR 0065
/// decision 2). Every operation on the runner API is scoped by the result: a claim offers only runs pinned to one of
/// these environments, and an operation naming a run outside them is refused as though the run did not exist.
/// </summary>
/// <remarks>
/// <para>
/// The environment is never a request parameter, which is why this exists. A client-supplied environment would be a
/// request to claim another tenant's work, so the candidate set is intersected server-side with what the principal is
/// bound to.
/// </para>
/// <para>
/// Resolution happens per request rather than per connection or per process, because that is what gives runner
/// revocation its effect: an implementation that caches bounds the staleness (ADR 0065 decision 2 caps it at thirty
/// seconds), so a revoked runner stops being offered work within that window rather than at the end of its process's
/// life.
/// </para>
/// </remarks>
public interface IRunnerEnvironmentBindings
{
    /// <summary>Resolves the environments <paramref name="principal"/> is currently bound to.</summary>
    /// <param name="principal">The authenticated machine principal.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The bound environments, in the order the runner should be offered work from them. An empty result is
    /// normal and not an error: a principal that is registered but not yet authorized, or whose authorization was
    /// revoked, is bound to nothing and is offered nothing.</returns>
    ValueTask<IReadOnlyList<string>> ResolveAsync(string principal, CancellationToken cancellationToken);
}