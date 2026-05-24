// <copyright file="CompositeAuthenticationProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Authentication provider that delegates to multiple providers based on the
/// security scheme type.
/// </summary>
/// <remarks>
/// <para>
/// AsyncAPI servers may require multiple security schemes. This composite provider
/// selects the appropriate delegate based on the <see cref="MessageAuthenticationContext.SchemeType"/>
/// of the incoming context.
/// </para>
/// </remarks>
public sealed class CompositeAuthenticationProvider : IMessageAuthenticationProvider
{
    private readonly Dictionary<SecuritySchemeType, IMessageAuthenticationProvider> providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeAuthenticationProvider"/> class.
    /// </summary>
    /// <param name="providers">A mapping of scheme types to their providers.</param>
    public CompositeAuthenticationProvider(
        IEnumerable<KeyValuePair<SecuritySchemeType, IMessageAuthenticationProvider>> providers)
    {
        this.providers = new Dictionary<SecuritySchemeType, IMessageAuthenticationProvider>(providers);
    }

    /// <inheritdoc/>
    public ValueTask AuthenticateAsync(
        MessageAuthenticationContext context,
        CancellationToken cancellationToken = default)
    {
        if (this.providers.TryGetValue(context.SchemeType, out IMessageAuthenticationProvider? provider))
        {
            return provider.AuthenticateAsync(context, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }
}