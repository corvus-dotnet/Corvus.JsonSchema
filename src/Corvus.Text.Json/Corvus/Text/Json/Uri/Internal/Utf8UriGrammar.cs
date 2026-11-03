// <copyright file="Utf8UriGrammar.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Matches the RFC 3986 <c>URI</c> and <c>URI-reference</c> grammars and their RFC 3987 IRI counterparts, for the
/// <c>uri</c>, <c>uri-reference</c>, <c>iri</c> and <c>iri-reference</c> formats, and for the <see cref="Utf8UriTools"/> parsing behind the
/// <c>Utf8Uri</c>, <c>Utf8UriReference</c>, <c>Utf8Iri</c> and <c>Utf8IriReference</c> types.
/// </summary>
/// <remarks>
/// <para>
/// These are checks of the grammar alone, read once from start to end without allocating. They do not resolve,
/// normalize or interpret the reference.
/// </para>
/// <code>
/// URI           = scheme ":" hier-part [ "?" query ] [ "#" fragment ]
/// hier-part     = "//" authority path-abempty / path-absolute / path-rootless / path-empty
/// URI-reference = URI / relative-ref
/// relative-ref  = relative-part [ "?" query ] [ "#" fragment ]
/// relative-part = "//" authority path-abempty / path-absolute / path-noscheme / path-empty
/// scheme        = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
/// authority     = [ userinfo "@" ] host [ ":" port ]
/// userinfo      = *( unreserved / pct-encoded / sub-delims / ":" )
/// host          = IP-literal / IPv4address / reg-name
/// IP-literal    = "[" ( IPv6address / IPvFuture ) "]"
/// IPvFuture     = "v" 1*HEXDIG "." 1*( unreserved / sub-delims / ":" )
/// reg-name      = *( unreserved / pct-encoded / sub-delims )
/// port          = *DIGIT
/// path-abempty  = *( "/" segment )
/// path-absolute = "/" [ segment-nz *( "/" segment ) ]
/// path-noscheme = segment-nz-nc *( "/" segment )
/// path-rootless = segment-nz *( "/" segment )
/// segment       = *pchar
/// segment-nz-nc = 1*( unreserved / pct-encoded / sub-delims / "@" )
/// pchar         = unreserved / pct-encoded / sub-delims / ":" / "@"
/// query         = *( pchar / "/" / "?" )
/// fragment      = *( pchar / "/" / "?" )
/// pct-encoded   = "%" 2HEXDIG
/// unreserved    = ALPHA / DIGIT / "-" / "." / "_" / "~"
/// sub-delims    = "!" / "$" / "&amp;" / "'" / "(" / ")" / "*" / "+" / "," / ";" / "="
/// </code>
/// <para>
/// RFC 3987 adds <c>ucschar</c> to <c>unreserved</c> (as <c>iunreserved</c>) everywhere, and <c>iprivate</c> to the
/// query. An IPv4address is also a reg-name, so a host that is not an IP-literal is read as a reg-name.
/// </para>
/// </remarks>
internal static class Utf8UriGrammar
{
    /// <summary>
    /// Matches an RFC 3986 <c>URI</c>, or an RFC 3987 <c>IRI</c> when <paramref name="iri"/> is set.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <param name="iri">Whether to allow the characters RFC 3987 adds.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    public static bool MatchAbsolute(ReadOnlySpan<byte> value, bool iri)
    {
        int schemeEnd = SchemeLength(value);
        if (schemeEnd <= 0)
        {
            return false;
        }

        return MatchHierPartAndRest(value.Slice(schemeEnd + 1), iri, relative: false);
    }

    /// <summary>
    /// Matches an RFC 3986 <c>URI-reference</c>, or an RFC 3987 <c>IRI-reference</c> when <paramref name="iri"/> is set.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value.</param>
    /// <param name="iri">Whether to allow the characters RFC 3987 adds.</param>
    /// <returns><see langword="true"/> if the value matches.</returns>
    public static bool MatchReference(ReadOnlySpan<byte> value, bool iri)
    {
        int schemeEnd = SchemeLength(value);
        if (schemeEnd > 0)
        {
            return MatchHierPartAndRest(value.Slice(schemeEnd + 1), iri, relative: false);
        }

        return MatchHierPartAndRest(value, iri, relative: true);
    }

    /// <summary>
    /// Gets the length of the scheme, if the value starts with <c>scheme ":"</c>.
    /// </summary>
    /// <returns>The index of the colon after the scheme, or -1 if the value does not start with a scheme.</returns>
    private static int SchemeLength(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0 || !IsAlpha(value[0]))
        {
            return -1;
        }

        for (int i = 1; i < value.Length; i++)
        {
            byte c = value[i];
            if (c == (byte)':')
            {
                return i;
            }

            if (!IsAlpha(c) && !IsDigit(c) && c != (byte)'+' && c != (byte)'-' && c != (byte)'.')
            {
                return -1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Matches <c>hier-part</c> (or <c>relative-part</c>) followed by the optional query and fragment.
    /// </summary>
    private static bool MatchHierPartAndRest(ReadOnlySpan<byte> value, bool iri, bool relative)
    {
        int end = IndexOfQueryOrFragment(value);
        ReadOnlySpan<byte> part = value.Slice(0, end);

        if (part.Length >= 2 && part[0] == (byte)'/' && part[1] == (byte)'/')
        {
            // "//" authority path-abempty
            ReadOnlySpan<byte> rest = part.Slice(2);
            int pathStart = rest.IndexOf((byte)'/');
            ReadOnlySpan<byte> authority = pathStart < 0 ? rest : rest.Slice(0, pathStart);
            if (!MatchAuthority(authority, iri))
            {
                return false;
            }

            if (pathStart >= 0 && !MatchSegments(rest.Slice(pathStart + 1), iri))
            {
                return false;
            }
        }
        else if (part.Length > 0 && part[0] == (byte)'/')
        {
            // path-absolute: "/" [ segment-nz *( "/" segment ) ]; "//" was handled above, so the first segment
            // is either empty at the end of the path or not empty.
            if (!MatchSegments(part.Slice(1), iri))
            {
                return false;
            }
        }
        else if (part.Length > 0)
        {
            // path-rootless, or in a relative reference path-noscheme, whose first segment has no colon.
            int firstSlash = part.IndexOf((byte)'/');
            ReadOnlySpan<byte> first = firstSlash < 0 ? part : part.Slice(0, firstSlash);
            if (relative && first.IndexOf((byte)':') >= 0)
            {
                return false;
            }

            if (!MatchSegments(part, iri))
            {
                return false;
            }
        }

        // path-empty otherwise.
        ReadOnlySpan<byte> tail = value.Slice(end);
        if (tail.Length == 0)
        {
            return true;
        }

        int fragment = tail.IndexOf((byte)'#');
        if (tail[0] == (byte)'?')
        {
            ReadOnlySpan<byte> query = fragment < 0 ? tail.Slice(1) : tail.Slice(1, fragment - 1);
            if (!MatchQueryOrFragment(query, iri, isQuery: true))
            {
                return false;
            }
        }

        return fragment < 0 || MatchQueryOrFragment(tail.Slice(fragment + 1), iri, isQuery: false);
    }

    private static int IndexOfQueryOrFragment(ReadOnlySpan<byte> value)
    {
        int index = value.IndexOfAny((byte)'?', (byte)'#');
        return index < 0 ? value.Length : index;
    }

    /// <summary>
    /// Matches <c>authority = [ userinfo "@" ] host [ ":" port ]</c>.
    /// </summary>
    private static bool MatchAuthority(ReadOnlySpan<byte> authority, bool iri)
    {
        // userinfo cannot contain "@", so the first "@" ends it, and any other "@" is an error in the host.
        int at = authority.IndexOf((byte)'@');
        if (at >= 0)
        {
            if (!MatchRun(authority.Slice(0, at), iri, Allowed.Userinfo))
            {
                return false;
            }

            authority = authority.Slice(at + 1);
        }

        ReadOnlySpan<byte> host;
        ReadOnlySpan<byte> port;
        if (authority.Length > 0 && authority[0] == (byte)'[')
        {
            int close = authority.IndexOf((byte)']');
            if (close < 0)
            {
                return false;
            }

            if (!MatchIPLiteral(authority.Slice(1, close - 1)))
            {
                return false;
            }

            ReadOnlySpan<byte> afterHost = authority.Slice(close + 1);
            if (afterHost.Length == 0)
            {
                return true;
            }

            if (afterHost[0] != (byte)':')
            {
                return false;
            }

            port = afterHost.Slice(1);
            return MatchPort(port);
        }

        int colon = authority.LastIndexOf((byte)':');
        if (colon >= 0)
        {
            host = authority.Slice(0, colon);
            port = authority.Slice(colon + 1);
            if (!MatchPort(port))
            {
                return false;
            }
        }
        else
        {
            host = authority;
        }

        return MatchRun(host, iri, Allowed.RegName);
    }

    private static bool MatchPort(ReadOnlySpan<byte> port)
    {
        foreach (byte c in port)
        {
            if (!IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches the content of an IP-literal (without its brackets): an IPv6address or an IPvFuture.
    /// </summary>
    private static bool MatchIPLiteral(ReadOnlySpan<byte> literal)
    {
        if (literal.Length > 0 && (literal[0] | 0x20) == (byte)'v')
        {
            // IPvFuture = "v" 1*HEXDIG "." 1*( unreserved / sub-delims / ":" )
            int i = 1;
            while (i < literal.Length && IsHexDigit(literal[i]))
            {
                i++;
            }

            if (i == 1 || i >= literal.Length - 1 || literal[i] != (byte)'.')
            {
                return false;
            }

            for (i++; i < literal.Length; i++)
            {
                byte c = literal[i];
                if (!IsUnreserved(c) && !IsSubDelim(c) && c != (byte)':')
                {
                    return false;
                }
            }

            return true;
        }

        return literal.Length > 0 &&
            literal.Length <= IPAddressParser.MaxIPv6StringLength &&
            IPAddressParser.IsValidIPV6(literal);
    }

    /// <summary>
    /// Matches <c>segment *( "/" segment )</c>: the path after its leading slash, or a rootless path.
    /// </summary>
    private static bool MatchSegments(ReadOnlySpan<byte> path, bool iri)
    {
        return MatchRun(path, iri, Allowed.PathWithSlash);
    }

    private static bool MatchQueryOrFragment(ReadOnlySpan<byte> value, bool iri, bool isQuery)
    {
        return MatchRun(value, iri, isQuery ? Allowed.Query : Allowed.Fragment);
    }

    [Flags]
    private enum Allowed
    {
        None = 0,
        Colon = 1,
        At = 2,
        Slash = 4,
        Question = 8,
        Private = 16,

        Userinfo = Colon,
        RegName = None,
        PathWithSlash = Colon | At | Slash,
        Fragment = Colon | At | Slash | Question,
        Query = Fragment | Private,
    }

    /// <summary>
    /// Matches a run of <c>unreserved / pct-encoded / sub-delims</c> characters, with the extra characters the
    /// production allows.
    /// </summary>
    private static bool MatchRun(ReadOnlySpan<byte> value, bool iri, Allowed allowed)
    {
        int i = 0;
        while (i < value.Length)
        {
            byte c = value[i];
            if (c == (byte)'%')
            {
                if (i + 2 >= value.Length || !IsHexDigit(value[i + 1]) || !IsHexDigit(value[i + 2]))
                {
                    return false;
                }

                i += 3;
                continue;
            }

            if (c > 0x7F)
            {
                if (!iri)
                {
                    return false;
                }

                if (Rune.DecodeFromUtf8(value.Slice(i), out Rune rune, out int consumed) != System.Buffers.OperationStatus.Done ||
                    !(IsUcsChar(rune.Value) || ((allowed & Allowed.Private) != 0 && IsIPrivate(rune.Value))))
                {
                    return false;
                }

                i += consumed;
                continue;
            }

            bool ok = IsUnreserved(c) || IsSubDelim(c) ||
                (c == (byte)':' && (allowed & Allowed.Colon) != 0) ||
                (c == (byte)'@' && (allowed & Allowed.At) != 0) ||
                (c == (byte)'/' && (allowed & Allowed.Slash) != 0) ||
                (c == (byte)'?' && (allowed & Allowed.Question) != 0);
            if (!ok)
            {
                return false;
            }

            i++;
        }

        return true;
    }

    private static bool IsAlpha(byte c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a';

    private static bool IsDigit(byte c) => (uint)(c - '0') <= 9;

    private static bool IsHexDigit(byte c) => IsDigit(c) || (uint)((c | 0x20) - 'a') <= 'f' - 'a';

    private static bool IsUnreserved(byte c) => IsAlpha(c) || IsDigit(c) || c == (byte)'-' || c == (byte)'.' || c == (byte)'_' || c == (byte)'~';

    private static bool IsSubDelim(byte c) => c switch
    {
        (byte)'!' or (byte)'$' or (byte)'&' or (byte)'\'' or (byte)'(' or (byte)')' or (byte)'*' or (byte)'+' or (byte)',' or (byte)';' or (byte)'=' => true,
        _ => false,
    };

    /// <summary>
    /// RFC 3987 <c>ucschar</c>.
    /// </summary>
    private static bool IsUcsChar(int c) =>
        (c >= 0xA0 && c <= 0xD7FF) ||
        (c >= 0xF900 && c <= 0xFDCF) ||
        (c >= 0xFDF0 && c <= 0xFFEF) ||
        (c >= 0x10000 && c <= 0xEFFFD && (c & 0xFFFF) <= 0xFFFD && c < 0xE0000) ||
        (c >= 0xE1000 && c <= 0xEFFFD);

    /// <summary>
    /// RFC 3987 <c>iprivate</c>, allowed only in a query.
    /// </summary>
    private static bool IsIPrivate(int c) =>
        (c >= 0xE000 && c <= 0xF8FF) ||
        (c >= 0xF0000 && c <= 0xFFFFD) ||
        (c >= 0x100000 && c <= 0x10FFFD);
}