// <copyright file="DeclaredRunnerEnvironmentBindings.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// An <see cref="IRunnerEnvironmentBindings"/> whose bindings are declared by the host: a fixed map from machine
/// principal to the environments that principal serves. The deployment states who may execute what, and no store is
/// consulted.
/// </summary>
/// <remarks>
/// This is the whole binding source for a deployment that runs its own runners and knows them by name. A deployment
/// that lets runners register and has environment administrators authorize them resolves against those records instead;
/// the two answer the same question from different sources of truth, which is why the surface is an interface.
/// </remarks>
public sealed class DeclaredRunnerEnvironmentBindings : IRunnerEnvironmentBindings
{
    private readonly IReadOnlyDictionary<string, RunnerBindings> bindings;

    /// <summary>Initializes a new instance of the <see cref="DeclaredRunnerEnvironmentBindings"/> class.</summary>
    /// <param name="bindings">The environments each machine principal serves. A principal absent from the map is bound
    /// to nothing, which is the fail-closed reading of a principal the deployment has not declared.</param>
    /// <param name="tenants">The tenant each machine principal's usage is counted against, or <see langword="null"/>
    /// when the deployment names none. A deployment that declares its runners by name typically has one tenant, so the
    /// common case is to omit this and have every principal count against the deployment.</param>
    public DeclaredRunnerEnvironmentBindings(
        IReadOnlyDictionary<string, IReadOnlyList<string>> bindings,
        IReadOnlyDictionary<string, string>? tenants = null)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var map = new Dictionary<string, RunnerBindings>(bindings.Count, StringComparer.Ordinal);
        foreach ((string principal, IReadOnlyList<string> environments) in bindings)
        {
            string? tenant = tenants is not null && tenants.TryGetValue(principal, out string? declared) && !string.IsNullOrEmpty(declared)
                ? declared
                : null;

            map[principal] = new RunnerBindings([.. environments], tenant);
        }

        this.bindings = map;
    }

    /// <inheritdoc/>
    public ValueTask<RunnerBindings> ResolveAsync(string principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(
            this.bindings.TryGetValue(principal, out RunnerBindings resolved) ? resolved : RunnerBindings.None);
    }
}