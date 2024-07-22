// <copyright file="FormatProviderRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A registry for type format providers.
/// </summary>
public sealed class FormatProviderRegistry
{
    private readonly HashSet<IFormatProvider> providers = [];

    private FormatProviderRegistry()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the <see cref="FormatProviderRegistry"/>.
    /// </summary>
    public static FormatProviderRegistry Instance { get; } = CreateDefaultInstance();

    /// <summary>
    /// Gets all type format providers.
    /// </summary>
    public IReadOnlyCollection<IFormatProvider> FormatProviders => this.providers;

    /// <summary>
    /// Gets all numeric type format providers.
    /// </summary>
    public IEnumerable<INumberFormatProvider> NumberTypeFormatProviders => this.providers.OfType<INumberFormatProvider>();

    /// <summary>
    /// Gets all string type format providers.
    /// </summary>
    public IEnumerable<IStringFormatProvider> StringTypeFormatProviders => this.providers.OfType<IStringFormatProvider>();

    /// <summary>
    /// Register a type format provider.
    /// </summary>
    /// <param name="providers">The providers to register.</param>
    /// <returns>A reference to the registry having completed the operation.</returns>
    public FormatProviderRegistry RegisterTypeFormatProviders(params IFormatProvider[] providers)
    {
        foreach (IFormatProvider provider in providers)
        {
            this.providers.Add(provider);
        }

        return this;
    }

    private static FormatProviderRegistry CreateDefaultInstance()
    {
        return new FormatProviderRegistry()
            .RegisterTypeFormatProviders(
                WellKnownNumericFormatProvider.Instance,
                WellKnownStringFormatProvider.Instance);
    }
}