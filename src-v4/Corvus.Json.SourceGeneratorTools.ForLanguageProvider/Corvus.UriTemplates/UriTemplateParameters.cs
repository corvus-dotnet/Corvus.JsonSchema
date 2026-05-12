// <copyright file="UriTemplateParameters.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.UriTemplates;

/// <summary>
/// A collection of parameters extracted from a URI template.
/// </summary>
/// <remarks>
/// This avoids allocating individual strings for the parameter names and values.
/// </remarks>
public class UriTemplateParameters : IDisposable
{
    private ParameterValue[]? items;

    /// <summary>
    /// Creates a <see cref="UriTemplateParameters"/>.
    /// </summary>
    /// <param name="items">
    /// The parameter value array, which has been rented. Ownership passes to this instance,
    /// which is why <see cref="Dispose"/> has to return it.
    /// </param>
    internal UriTemplateParameters(ParameterValue[] items)
    {
        this.items = items;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.items is not null)
        {
            ParameterByNameAndRangeCache.Return(this.items);
            this.items = null;
        }
    }

    /// <summary>
    /// Tests whether a parameter with the specified name is present.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <returns>True if the named parameter was found.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if <see cref="Dispose"/> has already been called.
    /// </exception>
    public bool Has(ReadOnlySpan<char> name) => this.TryGet(name, out _);

    /// <summary>
    /// Retrieves the value of a parameter.
    /// </summary>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>True if the named parameter was found.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if <see cref="Dispose"/> has already been called.
    /// </exception>
    public bool TryGet(ReadOnlySpan<char> name, out ParameterValue value)
    {
        if (this.items is null)
        {
            throw new ObjectDisposedException(nameof(UriTemplateParameters));
        }

        for (int i = 0; i < this.items.Length; i++)
        {
            ref ParameterValue v = ref this.items[i];
            if (v.Name.Span.IsEmpty)
            {
                // The array is rented, so it will typically be larger than it
                // needs to be. If we hit an empty entry there's no point looking
                // any further.
                break;
            }

            if (v.Name.Span.Equals(name, StringComparison.Ordinal))
            {
                value = v;
                return true;
            }
        }

        value = default;
        return false;
    }
}