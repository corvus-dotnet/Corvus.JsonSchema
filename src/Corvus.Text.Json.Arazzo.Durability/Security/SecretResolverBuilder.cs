// <copyright file="SecretResolverBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// Composes the runner's secret-resolver set (design §13). A deployment registers exactly the secret stores it uses —
/// the built-in <c>env://</c>/<c>file://</c> schemes via <see cref="AddEnvironment"/>/<see cref="AddFile"/>, and the
/// external stores (Key Vault / AWS Secrets Manager / HashiCorp Vault) via the <c>Add…</c> extension methods their
/// resolver assemblies contribute — then calls <see cref="Build"/> to get a single dispatching
/// <see cref="ISecretResolver"/> to hand to the runner's <c>SourceCredentialProviderFactory</c>.
/// </summary>
/// <remarks>
/// <para>This is pure composition ergonomics over the existing bring-your-own-resolver model: the builder only
/// assembles resolvers the host explicitly registers (and the external ones still take a host-supplied SDK client), so
/// it widens nothing — the control plane and the durability stores still never hold a resolver. Secret schemes are
/// disjoint, so registration order does not affect dispatch; <see cref="Build"/> rejects registering more than one
/// resolver for the same scheme so a shadowed, dead-config resolver is a loud error rather than a silent surprise.</para>
/// </remarks>
public sealed class SecretResolverBuilder
{
    private readonly List<ISecretResolver> resolvers = [];

    /// <summary>Registers an arbitrary resolver — the seam for a custom <see cref="ISecretResolver"/>.</summary>
    /// <param name="resolver">The resolver to register.</param>
    /// <returns>This builder, for chaining.</returns>
    public SecretResolverBuilder Add(ISecretResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        this.resolvers.Add(resolver);
        return this;
    }

    /// <summary>Registers the built-in <c>env://</c> resolver (<see cref="EnvSecretResolver"/>).</summary>
    /// <returns>This builder, for chaining.</returns>
    public SecretResolverBuilder AddEnvironment() => this.Add(new EnvSecretResolver());

    /// <summary>Registers the built-in <c>file://</c> resolver (<see cref="FileSecretResolver"/>).</summary>
    /// <param name="secretRoot">
    /// An optional confinement root (§17.5/F6): when supplied, locators are resolved relative to it and may not escape
    /// it (absolute paths and <c>..</c> traversal are rejected). When <see langword="null"/> the locator is the exact
    /// file path (trusted-operator behaviour).
    /// </param>
    /// <returns>This builder, for chaining.</returns>
    public SecretResolverBuilder AddFile(string? secretRoot = null) => this.Add(new FileSecretResolver(secretRoot));

    /// <summary>Registers both built-in resolvers (<c>env://</c> and <c>file://</c>).</summary>
    /// <param name="secretRoot">An optional confinement root for the <c>file://</c> resolver (§17.5/F6).</param>
    /// <returns>This builder, for chaining.</returns>
    public SecretResolverBuilder AddEnvironmentAndFile(string? secretRoot = null) => this.AddEnvironment().AddFile(secretRoot);

    /// <summary>Builds a single <see cref="ISecretResolver"/> that dispatches a reference to the registered resolver
    /// for its scheme (fail-closed for an unregistered scheme).</summary>
    /// <returns>The composed resolver.</returns>
    /// <exception cref="InvalidOperationException">No resolver was registered, or more than one resolver was registered
    /// for the same scheme.</exception>
    public ISecretResolver Build()
    {
        if (this.resolvers.Count == 0)
        {
            throw new InvalidOperationException("no secret resolvers have been registered; register at least one before calling Build().");
        }

        // Each scheme must be handled by at most one registered resolver: CompositeSecretResolver dispatches to the
        // first that claims a scheme, so a second resolver for the same scheme would be unreachable dead config.
        foreach (SecretScheme scheme in Enum.GetValues<SecretScheme>())
        {
            int count = 0;
            foreach (ISecretResolver resolver in this.resolvers)
            {
                if (resolver.CanResolve(scheme))
                {
                    count++;
                }
            }

            if (count > 1)
            {
                throw new InvalidOperationException($"more than one resolver was registered for the '{scheme}' scheme; register exactly one resolver per scheme.");
            }
        }

        return new CompositeSecretResolver([.. this.resolvers]);
    }
}