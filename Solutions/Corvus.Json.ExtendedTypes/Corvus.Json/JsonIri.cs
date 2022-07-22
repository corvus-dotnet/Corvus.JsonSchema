// <copyright file="JsonIri.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON iri.
/// </summary>
public readonly partial struct JsonIri
{
    private static readonly Uri EmptyUri = new(string.Empty, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonIri"/> struct.
    /// </summary>
    /// <param name="value">The Uri value.</param>
    public JsonIri(Uri value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatUri(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to Uri.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not an Iri.</exception>
    public static implicit operator Uri(JsonIri value)
    {
        return value.GetUri();
    }

    /// <summary>
    /// Implicit conversion from Uri.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonIri(Uri value)
    {
        return new JsonIri(value);
    }

    /// <summary>
    /// Gets the value as a Uri.
    /// </summary>
    /// <returns>The value as a Uri.</returns>
    /// <exception cref="InvalidOperationException">The value was not an Iri.</exception>
    public Uri GetUri()
    {
        if (this.TryGetUri(out Uri? result))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get the Uri value.
    /// </summary>
    /// <param name="result">The Uri value.</param>
    /// <returns><c>True</c> if it was possible to get a Uri value from the instance.</returns>
    public bool TryGetUri([NotNullWhen(true)] out Uri? result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return TryParseUri(this.stringBacking, out result);
        }

        if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            string? str = this.jsonElementBacking.GetString();
            if (str is not null)
            {
                return TryParseUri(str, out result);
            }
        }

        result = EmptyUri;
        return false;
    }

    private static string FormatUri(Uri value)
    {
        return value.OriginalString;
    }

    private static bool TryParseUri(string text, [NotNullWhen(true)] out Uri? value)
    {
        return Uri.TryCreate(text, UriKind.Absolute, out value);
    }
}