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
    private static readonly string[] None = [];

    private readonly IReadOnlyDictionary<string, string[]> bindings;

    /// <summary>Initializes a new instance of the <see cref="DeclaredRunnerEnvironmentBindings"/> class.</summary>
    /// <param name="bindings">The environments each machine principal serves. A principal absent from the map is bound
    /// to nothing, which is the fail-closed reading of a principal the deployment has not declared.</param>
    public DeclaredRunnerEnvironmentBindings(IReadOnlyDictionary<string, IReadOnlyList<string>> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        var map = new Dictionary<string, string[]>(bindings.Count, StringComparer.Ordinal);
        foreach ((string principal, IReadOnlyList<string> environments) in bindings)
        {
            map[principal] = [.. environments];
        }

        this.bindings = map;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<string>> ResolveAsync(string principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult<IReadOnlyList<string>>(
            this.bindings.TryGetValue(principal, out string[]? environments) ? environments : None);
    }
}