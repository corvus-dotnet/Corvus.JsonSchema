// <copyright file="UriValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Json.Internal;

/// <summary>
/// RFC 3986-compliant URI/URI-reference and RFC 3987-compliant IRI/IRI-reference validation.
/// Validates that the raw string does not contain characters disallowed by the respective RFCs.
/// </summary>
internal static class UriValidator
{
    /// <summary>
    /// Validates a URI per RFC 3986 (must be absolute).
    /// </summary>
    internal static bool IsValidUri(ReadOnlySpan<byte> utf8Value)
    {
        return ValidateUri(utf8Value, requireAbsolute: true, allowIri: false);
    }

    /// <summary>
    /// Validates a URI-reference per RFC 3986 (may be relative).
    /// </summary>
    internal static bool IsValidUriReference(ReadOnlySpan<byte> utf8Value)
    {
        return ValidateUri(utf8Value, requireAbsolute: false, allowIri: false);
    }

    /// <summary>
    /// Validates an IRI per RFC 3987 (must be absolute).
    /// </summary>
    internal static bool IsValidIri(ReadOnlySpan<byte> utf8Value)
    {
        return ValidateUri(utf8Value, requireAbsolute: true, allowIri: true);
    }

    /// <summary>
    /// Validates an IRI-reference per RFC 3987 (may be relative).
    /// </summary>
    internal static bool IsValidIriReference(ReadOnlySpan<byte> utf8Value)
    {
        return ValidateUri(utf8Value, requireAbsolute: false, allowIri: true);
    }

    private static bool ValidateUri(ReadOnlySpan<byte> utf8Value, bool requireAbsolute, bool allowIri)
    {
        if (utf8Value.IsEmpty)
        {
            // An empty string is a valid relative reference per RFC 3986 §4.2 (path-empty)
            // but is not a valid absolute URI/IRI.
            return !requireAbsolute;
        }

        // First check for characters disallowed by RFC 3986/3987 in the raw value.
        // System.Uri is too permissive and accepts many of these.
        for (int i = 0; i < utf8Value.Length; i++)
        {
            byte b = utf8Value[i];

            if (b <= 0x20)
            {
                // Control chars and space are never allowed unescaped in URIs
                return false;
            }

            if (b < 0x7F)
            {
                // ASCII printable — check for disallowed characters per RFC 3986 §2.2
                // Disallowed unescaped: \ < > { } | ^ ` "
                switch (b)
                {
                    case (byte)'\\':
                    case (byte)'<':
                    case (byte)'>':
                    case (byte)'{':
                    case (byte)'}':
                    case (byte)'|':
                    case (byte)'^':
                    case (byte)'`':
                    case (byte)'"':
                        return false;
                }
            }
            else if (b == 0x7F)
            {
                // DEL
                return false;
            }
            else if (!allowIri)
            {
                // Non-ASCII bytes in a non-IRI URI must be percent-encoded
                return false;
            }
        }

        // Now use System.Uri for structural validation
        // We need to convert UTF-8 to string for System.Uri
        string uriString;

#if NET8_0_OR_GREATER
        uriString = System.Text.Encoding.UTF8.GetString(utf8Value);
#else
        unsafe
        {
            fixed (byte* ptr = utf8Value)
            {
                uriString = System.Text.Encoding.UTF8.GetString(ptr, utf8Value.Length);
            }
        }
#endif

        if (requireAbsolute)
        {
            // RFC 3986: absolute URI must have a scheme.
            // System.Uri treats "//host/path" as file://host/path (UNC), which is wrong.
            // Check for scheme: must start with ALPHA and contain ':' before '/' or '?'
            if (!HasScheme(utf8Value))
            {
                return false;
            }

            // Check for '[' in userinfo (between "://" and "@").
            // RFC 3986 §3.2.1: userinfo = *( unreserved / pct-encoded / sub-delims / ":" )
            // '[' is not in any of those sets.
            if (HasBracketInUserinfo(uriString))
            {
                return false;
            }

            return Uri.TryCreate(uriString, UriKind.Absolute, out Uri? result) && result.IsAbsoluteUri;
        }
        else
        {
            if (HasBracketInUserinfo(uriString))
            {
                return false;
            }

            return Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out _);
        }
    }

    private static bool HasScheme(ReadOnlySpan<byte> utf8Value)
    {
        // scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        if (utf8Value.IsEmpty || !IsAlpha(utf8Value[0]))
        {
            return false;
        }

        for (int i = 1; i < utf8Value.Length; i++)
        {
            byte b = utf8Value[i];
            if (b == (byte)':')
            {
                return true;
            }

            if (!IsAlpha(b) && !IsDigit(b) && b != (byte)'+' && b != (byte)'-' && b != (byte)'.')
            {
                return false;
            }
        }

        return false;
    }

    private static bool HasBracketInUserinfo(string uriString)
    {
        // Find "://" then check for '[' before '@'
        int authorityStart = uriString.IndexOf("://", StringComparison.Ordinal);
        if (authorityStart < 0)
        {
            return false;
        }

        int afterScheme = authorityStart + 3;
        int atIndex = uriString.IndexOf('@', afterScheme);
        if (atIndex < 0)
        {
            return false;
        }

        // Check for '[' between authority start and '@'
        for (int i = afterScheme; i < atIndex; i++)
        {
            if (uriString[i] == '[')
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAlpha(byte b) => (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'a' && b <= (byte)'z');

    private static bool IsDigit(byte b) => b >= (byte)'0' && b <= (byte)'9';
}