// <copyright file="StandardUri.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.Internal;

/// <summary>
/// Parsing for format uri/uri-reference/iri/iri-reference.
/// </summary>
public static class StandardUri
{
    /// <summary>
    /// Gets an instance of an empty URI, that is configured to be <see cref="UriKind.RelativeOrAbsolute"/>.
    /// </summary>
    public static readonly Uri EmptyUri = new(string.Empty, UriKind.RelativeOrAbsolute);

    /// <summary>
    /// Format a URI to a string.
    /// </summary>
    /// <param name="value">The <see cref="Uri"/> to format.</param>
    /// <returns>The uri format string.</returns>
    public static string FormatUri(Uri value)
    {
        return value.OriginalString;
    }

    /// <summary>
    /// Try to parse a <see cref="Uri"/>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The resulting URI, or <see langword="null"/> if the uri format could not be parsed.</param>
    /// <returns><see langword="true"/> if the URI could be parsed.</returns>
    /// <remarks>
    /// This will parse any uri format including <c>uri</c>, <c>uri-reference</c>, <c>iri</c>, and <c>iri-reference</c>.
    /// </remarks>
    public static bool TryParseUri(string text, [NotNullWhen(true)] out Uri? value)
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

    /// <summary>
    /// Try to parse a <see cref="Uri"/>.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="value">The resulting URI, or <see langword="null"/> if the uri format could not be parsed.</param>
    /// <returns><see langword="true"/> if the URI could be parsed.</returns>
    /// <remarks>
    /// This will parse any uri format including <c>uri</c>, <c>uri-reference</c>, <c>iri</c>, and <c>iri-reference</c>.
    /// </remarks>
    public static bool TryParseUriReference(string text, [NotNullWhen(true)] out Uri? value)
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
        return Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out value);
    }
}