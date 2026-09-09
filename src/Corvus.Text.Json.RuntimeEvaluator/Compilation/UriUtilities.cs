// <copyright file="UriUtilities.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// URI helpers for schema identification and reference resolution.
/// </summary>
internal static class UriUtilities
{
    /// <summary>
    /// Splits a URI reference into its non-fragment part and its (decoded) fragment.
    /// </summary>
    public static void Split(string reference, out string uriPart, out string fragment)
    {
        int hash = reference.IndexOf('#');
        if (hash < 0)
        {
            uriPart = reference;
            fragment = string.Empty;
        }
        else
        {
            uriPart = reference[..hash];
            fragment = reference[(hash + 1)..];
        }
    }

    /// <summary>
    /// Resolves a URI reference (without fragment) against an absolute base URI.
    /// </summary>
    public static string Resolve(string baseUri, string reference)
    {
        if (reference.Length == 0)
        {
            return baseUri;
        }

        if (HasScheme(reference) && Uri.TryCreate(reference, UriKind.Absolute, out Uri? absolute))
        {
            return Normalize(absolute);
        }

        if (baseUri.Length == 0)
        {
            return reference;
        }

        if (Uri.TryCreate(baseUri, UriKind.Absolute, out Uri? baseAbsolute))
        {
            if (Uri.TryCreate(baseAbsolute, reference, out Uri? combined))
            {
                return Normalize(combined);
            }
        }

        // Fallback: naive relative resolution for opaque bases such as urn:.
        int lastSlash = baseUri.LastIndexOf('/');
        return lastSlash >= 0 ? baseUri[..(lastSlash + 1)] + reference : reference;
    }

    /// <summary>
    /// Normalizes an absolute URI to its canonical string form (without fragment).
    /// </summary>
    public static string Normalize(string uri)
    {
        Split(uri, out string uriPart, out _);
        if (HasScheme(uriPart) && Uri.TryCreate(uriPart, UriKind.Absolute, out Uri? absolute))
        {
            return Normalize(absolute);
        }

        return uriPart;
    }

    /// <summary>
    /// Determines whether a reference starts with a URI scheme (RFC 3986 section 3.1).
    /// </summary>
    public static bool HasScheme(string reference)
    {
        if (reference.Length == 0 || !char.IsAsciiLetter(reference[0]))
        {
            return false;
        }

        for (int i = 1; i < reference.Length; i++)
        {
            char c = reference[i];
            if (c == ':')
            {
                return true;
            }

            if (!(char.IsAsciiLetterOrDigit(c) || c == '+' || c == '-' || c == '.'))
            {
                return false;
            }
        }

        return false;
    }

    private static string Normalize(Uri absolute)
    {
        string s = absolute.OriginalString;
        int hash = s.IndexOf('#');
        if (hash >= 0)
        {
            s = s[..hash];
        }

        // Use the framework's canonical form for hierarchical URIs so that equivalent
        // spellings (dot segments, default ports, case of scheme/host) compare equal.
        if (!absolute.IsAbsoluteUri)
        {
            return s;
        }

        string canonical = absolute.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
        return canonical;
    }

    /// <summary>
    /// Decodes a URI fragment into a JSON pointer or plain-name anchor.
    /// </summary>
    public static string DecodeFragment(string fragment)
    {
        if (fragment.IndexOf('%') < 0)
        {
            return fragment;
        }

        return Uri.UnescapeDataString(fragment);
    }
}
