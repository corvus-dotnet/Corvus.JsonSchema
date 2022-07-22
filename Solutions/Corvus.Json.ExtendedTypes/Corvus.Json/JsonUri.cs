// <copyright file="JsonUri.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uri.
/// </summary>
public readonly partial struct JsonUri
{
    private static readonly Uri EmptyUri = new(string.Empty, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUri"/> struct.
    /// </summary>
    /// <param name="value">The Uri value.</param>
    public JsonUri(Uri value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatUri(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to Uri.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a uri.</exception>
    public static implicit operator Uri(JsonUri value)
    {
        return value.GetUri();
    }

    /// <summary>
    /// Implicit conversion from Uri.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUri(Uri value)
    {
        return new JsonUri(value);
    }

    /// <summary>
    /// Gets the value as a Uri.
    /// </summary>
    /// <returns>The value as a Uri.</returns>
    /// <exception cref="InvalidOperationException">The value was not a uri.</exception>
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
        // Uri.TryCreate considers full-qualified file paths to be acceptable as absolute Uris.
        // This means that on Linux "/abc" is considered an acceptable absolute Uri! (This is
        // conceptually equivalent to "C:\abc" being an absolute Uri on Windows, but it's more
        // of a problem because a lot of relative Uris of the kind you come across on the web
        // look exactly like Unix file paths.)
        // https://github.com/dotnet/runtime/issues/22718
        // However, this only needs to be a problem if you insist that the Uri is absolute.
        // If you accept either absolute or relative Uris, it will intepret "/abc" as a
        // relative Uri on either Windows or Linux. It only interprets it as an absolute Uri
        // if you pass UriKind.Absolute when parsing.
        // This is why we take the peculiar-looking step of passing UriKind.RelativeOrAbsolute
        // and then rejecting relative Uris. This causes this method to reject "/abc" on all
        // platforms. Back when we passed UriKind.Absolute, this code incorrectly accepted
        // "abc".
        return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value) &&
            value.IsAbsoluteUri;
    }
}