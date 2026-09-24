// <copyright file="RunnerBindings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// What an authenticated machine principal resolves to: the environments it may execute runs for, and the tenant its
/// usage is counted against (ADR 0065 decision 3).
/// </summary>
/// <param name="Environments">The bound environments, in the order the runner should be offered work from them.</param>
/// <param name="Tenant">The owner group the bound environments belong to, or <see langword="null"/> when they name
/// none.</param>
/// <remarks>
/// <para>
/// The two travel together because they come from one read and share one cache window. Resolving them separately would
/// let a deployment be wired with reach it enforces and a quota counter it does not, and the quota would report success
/// while counting every tenant onto one bucket.
/// </para>
/// <para>
/// A <see langword="null"/> tenant is not an error. A deployment that publishes nothing to tell owner groups apart
/// stamps none, and has exactly one tenant by construction; so does the platform's own environment, which belongs to
/// the deployment rather than to a tenant. Both count against the deployment rather than against a named group.
/// </para>
/// </remarks>
public readonly record struct RunnerBindings(IReadOnlyList<string> Environments, string? Tenant, IReadOnlyDictionary<string, IReadOnlySet<string>>? SealedGenerations = null)
{
    private static readonly string[] NoEnvironments = [];

    /// <summary>
    /// The active key generations of a bound environment whose record is sealed (a tenant environment holding at
    /// least one active generation, ADR 0065 decision 10), or <see langword="null"/> for an environment that is not.
    /// The runner API cannot verify a MAC, since it holds no key, but it refuses a clear submission for a sealed
    /// environment, and one under a generation the record does not hold.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <returns>The active generation ids, or <see langword="null"/> when the environment is not sealed.</returns>
    public IReadOnlySet<string>? SealedGenerationsOf(string environment)
        => this.SealedGenerations is { } generationsByEnvironment && generationsByEnvironment.TryGetValue(environment, out IReadOnlySet<string>? generations) ? generations : null;

    /// <summary>Gets the resolution of a principal that is bound to nothing.</summary>
    /// <remarks>The correct answer for a principal whose authorization is pending or revoked, and for one whose
    /// bindings are refused outright. It is offered nothing and charged to nobody.</remarks>
    public static RunnerBindings None => new(NoEnvironments, null);

    /// <summary>Gets the number of bound environments.</summary>
    public int Count => this.Environments.Count;
}