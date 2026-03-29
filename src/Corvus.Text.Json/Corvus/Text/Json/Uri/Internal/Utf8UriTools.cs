// <copyright file="Utf8UriTools.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;

#if NET

using System.Buffers.Text;

#endif

using System.Diagnostics;
using System.Runtime.CompilerServices;
using NodaTime;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides utilities for parsing and validating UTF-8 URI strings.
/// </summary>
internal static class Utf8UriTools
{
    /// <summary>
    /// The maximum size for a URI buffer.
    /// </summary>
    internal const int c_MaxUriBufferSize = 0xFFF0;

    /// <summary>
    /// The maximum length for a URI scheme name.
    /// </summary>
    internal const int c_MaxUriSchemeName = 1024;

    /// <summary>
    /// An invalid Unicode character used as a dummy character parameter.
    /// </summary>
    internal const char c_DummyChar = (char)0xFFFF;     // An Invalid Unicode character used as a dummy char passed into the parameter

    /// <summary>
    /// End-of-line character.
    /// </summary>
    internal const byte c_EOL = 0x00;

    /// <summary>
    /// Flags used during URI parsing and validation.
    /// </summary>
    [Flags]
    internal enum Flags : ulong
    {
        Zero = 0x00000000,

        SchemeNotCanonical = 0x1,
        UserNotCanonical = 0x2,
        HostNotCanonical = 0x4,
        PortNotCanonical = 0x8,
        PathNotCanonical = 0x10,
        QueryNotCanonical = 0x20,
        FragmentNotCanonical = 0x40,
        CannotDisplayCanonical = 0x7F,

        E_UserNotCanonical = 0x80,
        E_HostNotCanonical = 0x100,
        E_PortNotCanonical = 0x200,
        E_PathNotCanonical = 0x400,
        E_QueryNotCanonical = 0x800,
        E_FragmentNotCanonical = 0x1000,
        E_CannotDisplayCanonical = 0x1F80,

        E_NonCanonical = E_UserNotCanonical | E_HostNotCanonical | E_PortNotCanonical | E_PathNotCanonical | E_QueryNotCanonical | E_FragmentNotCanonical | E_CannotDisplayCanonical,

        ShouldBeCompressed = 0x2000,
        FirstSlashAbsent = 0x4000,
        BackslashInPath = 0x8000,

        IndexMask = 0x0000FFFF,
        HostTypeMask = 0x00070000,
        HostNotParsed = 0x00000000,
        IPv6HostType = 0x00010000,
        IPv4HostType = 0x00020000,
        DnsHostType = 0x00030000,
        UncHostType = 0x00040000,
        BasicHostType = 0x00050000,
        UnusedHostType = 0x00060000,
        UnknownHostType = 0x00070000,

        UserEscaped = 0x00080000,
        AuthorityFound = 0x00100000,
        HasUserInfo = 0x00200000,
        LoopbackHost = 0x00400000,
        NotDefaultPort = 0x00800000,

        UserDrivenParsing = 0x01000000,
        CanonicalDnsHost = 0x02000000,
        ErrorOrParsingRecursion = 0x04000000,   // Used to signal a default parser error and also to confirm Port

        // and Host values in case of a custom user Parser
        DosPath = 0x08000000,

        UncPath = 0x10000000,
        ImplicitFile = 0x20000000,
        MinimalUriInfoSet = 0x40000000,
        AllUriInfoSet = unchecked(0x80000000),
        IdnHost = 0x100000000,
        HasUnicode = 0x200000000,

        // Is this component Iri canonical
        UserIriCanonical = 0x8000000000,

        PathIriCanonical = 0x10000000000,
        QueryIriCanonical = 0x20000000000,
        FragmentIriCanonical = 0x40000000000,
        IriCanonical = 0x78000000000,
        UnixPath = 0x100000000000,

        /// <summary>
        /// Disables any validation/normalization past the authority. Fragments will always be empty. GetComponents will throw for Path/Query.
        /// </summary>
        DisablePathAndQueryCanonicalization = 0x200000000000,

        /// <summary>
        /// Used to ensure that InitializeAndValidate is only called once per Uri instance and only from an override of InitializeAndValidate
        /// </summary>
        CustomParser_ParseMinimalAlreadyCalled = 0x4000000000000000,

        HasUnescapedUnicode = 0x8000000000000000,
    }

    [Flags]
    private enum Check
    {
        None = 0x0,
        EscapedCanonical = 0x1,
        DisplayCanonical = 0x2,
        DotSlashAttn = 0x4,
        DotSlashEscaped = 0x80,
        BackslashInPath = 0x10,
        ReservedFound = 0x20,
        NotIriCanonical = 0x40,
        FoundNonAscii = 0x8
    }

    internal static bool MakeRelative(ReadOnlySpan<byte> baseUri, in Utf8UriOffset baseUriOffsets, Flags baseUriFlags, ReadOnlySpan<byte> uriToMakeRelative, in Utf8UriOffset uriToMakeRelativeOffsets, Flags uriToMakeRelativeFlags, Span<byte> destination, out int written, bool allowIri = false)
    {
        written = 0;

        // Extract base URI components
        int baseSchemeLen = baseUriOffsets.User - baseUriOffsets.Scheme;
        ReadOnlySpan<byte> baseScheme = baseSchemeLen > 0 ? baseUri.Slice(baseUriOffsets.Scheme, baseSchemeLen) : ReadOnlySpan<byte>.Empty;

        int baseHostLen = baseUriOffsets.Port > 0 ? baseUriOffsets.Port - baseUriOffsets.Host : baseUriOffsets.Path - baseUriOffsets.Host;
        ReadOnlySpan<byte> baseHost = baseHostLen > 0 ? baseUri.Slice(baseUriOffsets.Host, baseHostLen) : ReadOnlySpan<byte>.Empty;

        ReadOnlySpan<byte> basePort = ReadOnlySpan<byte>.Empty;
        if (baseUriOffsets.Port > 0 && baseUriOffsets.Path > baseUriOffsets.Port)
        {
            basePort = baseUri.Slice(baseUriOffsets.Port, baseUriOffsets.Path - baseUriOffsets.Port);
        }

        int basePathLen = baseUriOffsets.Query - baseUriOffsets.Path;
        ReadOnlySpan<byte> basePath = basePathLen > 0 ? baseUri.Slice(baseUriOffsets.Path, basePathLen) : ReadOnlySpan<byte>.Empty;

        // Extract target URI components
        int targetSchemeLen = uriToMakeRelativeOffsets.User - uriToMakeRelativeOffsets.Scheme;
        ReadOnlySpan<byte> targetScheme = targetSchemeLen > 0 ? uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Scheme, targetSchemeLen) : ReadOnlySpan<byte>.Empty;

        int targetHostLen = uriToMakeRelativeOffsets.Port > 0 ? uriToMakeRelativeOffsets.Port - uriToMakeRelativeOffsets.Host : uriToMakeRelativeOffsets.Path - uriToMakeRelativeOffsets.Host;
        ReadOnlySpan<byte> targetHost = targetHostLen > 0 ? uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Host, targetHostLen) : ReadOnlySpan<byte>.Empty;

        ReadOnlySpan<byte> targetPort = ReadOnlySpan<byte>.Empty;
        if (uriToMakeRelativeOffsets.Port > 0 && uriToMakeRelativeOffsets.Path > uriToMakeRelativeOffsets.Port)
        {
            targetPort = uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Port, uriToMakeRelativeOffsets.Path - uriToMakeRelativeOffsets.Port);
        }

        int targetPathLen = uriToMakeRelativeOffsets.Query - uriToMakeRelativeOffsets.Path;
        ReadOnlySpan<byte> targetPath = targetPathLen > 0 ? uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Path, targetPathLen) : ReadOnlySpan<byte>.Empty;

        int targetQueryLen = uriToMakeRelativeOffsets.Fragment - uriToMakeRelativeOffsets.Query;
        ReadOnlySpan<byte> targetQuery = targetQueryLen > 0 ? uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Query, targetQueryLen) : ReadOnlySpan<byte>.Empty;

        int targetFragmentLen = uriToMakeRelativeOffsets.End - uriToMakeRelativeOffsets.Fragment;
        ReadOnlySpan<byte> targetFragment = targetFragmentLen > 0 ? uriToMakeRelative.Slice(uriToMakeRelativeOffsets.Fragment, targetFragmentLen) : ReadOnlySpan<byte>.Empty;

        // Check if scheme, host, and port match
        if (SchemeEquals(baseScheme, targetScheme) &&
            HostEquals(baseHost, targetHost) &&
            PortEquals(basePort, targetPort))
        {
            // Normalize paths before comparison using the canonical path writer
            // We must use ArrayPool for these because we need to reference them later
            byte[]? normalizedBaseBuffer = null;
            byte[]? normalizedTargetBuffer = null;

            try
            {
                // Normalize base path
                if (basePath.Length > 0)
                {
                    int baseBufferSize = basePath.Length * 3; // 3x for worst-case percent-encoding
                    normalizedBaseBuffer = ArrayPool<byte>.Shared.Rent(baseBufferSize);
                    int basePos = 0;

                    // Always compress to remove dot segments
                    if (!TryWriteCanonicalPath(basePath, compress: true, allowIri, normalizedBaseBuffer, ref basePos))
                    {
                        return false;
                    }

                    basePath = normalizedBaseBuffer.AsSpan(0, basePos);
                }

                // Normalize target path
                if (targetPath.Length > 0)
                {
                    int targetBufferSize = targetPath.Length * 3; // 3x for worst-case percent-encoding
                    normalizedTargetBuffer = ArrayPool<byte>.Shared.Rent(targetBufferSize);
                    int targetPos = 0;

                    // Always compress to remove dot segments
                    if (!TryWriteCanonicalPath(targetPath, compress: true, allowIri, normalizedTargetBuffer, ref targetPos))
                    {
                        return false;
                    }

                    targetPath = normalizedTargetBuffer.AsSpan(0, targetPos);
                }

                // Calculate the path difference using normalized paths
                if (!TryCalculatePathDifference(basePath, targetPath, destination, out int pathWritten))
                {
                    return false;
                }

                // Check for colon in first path segment (RFC 3986, Section 4.2)
                if (pathWritten > 0 && CheckForColonInFirstPathSegment(destination.Slice(0, pathWritten)) &&
                    !targetPath.SequenceEqual(destination.Slice(0, pathWritten)))
                {
                    // Prepend "./" to relative path
                    if (destination.Length < 2 + pathWritten)
                    {
                        return false;
                    }

                    // Shift existing content
                    destination.Slice(0, pathWritten).CopyTo(destination.Slice(2));
                    destination[0] = (byte)'.';
                    destination[1] = (byte)'/';
                    pathWritten += 2;
                }

                // Write query if present
                if (targetQuery.Length > 0)
                {
                    if (pathWritten + targetQuery.Length > destination.Length)
                    {
                        return false;
                    }

                    targetQuery.CopyTo(destination.Slice(pathWritten));
                    pathWritten += targetQuery.Length;
                }

                // Write fragment if present
                if (targetFragment.Length > 0)
                {
                    if (pathWritten + targetFragment.Length > destination.Length)
                    {
                        return false;
                    }

                    targetFragment.CopyTo(destination.Slice(pathWritten));
                    pathWritten += targetFragment.Length;
                }

                written = pathWritten;
                return true;
            }
            finally
            {
                if (normalizedBaseBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(normalizedBaseBuffer);
                }

                if (normalizedTargetBuffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(normalizedTargetBuffer);
                }
            }
        }

        // Scheme/host/port don't match, return the full target URI
        if (uriToMakeRelative.Length > destination.Length)
        {
            return false;
        }

        uriToMakeRelative.CopyTo(destination);
        written = uriToMakeRelative.Length;
        return true;
    }

    private static bool SchemeEquals(ReadOnlySpan<byte> scheme1, ReadOnlySpan<byte> scheme2)
    {
        if (scheme1.Length != scheme2.Length)
        {
            return false;
        }

        // Schemes are case-insensitive, but we need to compare up to the colon (excluding it)
        int len = scheme1.Length;
        if (len > 0 && scheme1[len - 1] == (byte)':')
        {
            len--;
        }

        int len2 = scheme2.Length;
        if (len2 > 0 && scheme2[len2 - 1] == (byte)':')
        {
            len2--;
        }

        if (len != len2)
        {
            return false;
        }

        for (int i = 0; i < len; i++)
        {
            byte b1 = scheme1[i];
            byte b2 = scheme2[i];

            // Convert to lowercase for comparison
            if (b1 >= (byte)'A' && b1 <= (byte)'Z')
            {
                b1 = (byte)(b1 | 0x20);
            }

            if (b2 >= (byte)'A' && b2 <= (byte)'Z')
            {
                b2 = (byte)(b2 | 0x20);
            }

            if (b1 != b2)
            {
                return false;
            }
        }

        return true;
    }

    private static bool HostEquals(ReadOnlySpan<byte> host1, ReadOnlySpan<byte> host2)
    {
        if (host1.Length != host2.Length)
        {
            return false;
        }

        // Hosts are case-insensitive
        for (int i = 0; i < host1.Length; i++)
        {
            byte b1 = host1[i];
            byte b2 = host2[i];

            // Convert to lowercase for comparison
            if (b1 >= (byte)'A' && b1 <= (byte)'Z')
            {
                b1 = (byte)(b1 | 0x20);
            }

            if (b2 >= (byte)'A' && b2 <= (byte)'Z')
            {
                b2 = (byte)(b2 | 0x20);
            }

            if (b1 != b2)
            {
                return false;
            }
        }

        return true;
    }

    private static bool PortEquals(ReadOnlySpan<byte> port1, ReadOnlySpan<byte> port2)
    {
        return port1.SequenceEqual(port2);
    }

    private static bool TryCalculatePathDifference(ReadOnlySpan<byte> basePath, ReadOnlySpan<byte> targetPath, Span<byte> destination, out int written)
    {
        written = 0;

        int i;
        int lastSlashIndex = -1;

        // Find the common prefix up to the last common directory separator
        for (i = 0; i < basePath.Length && i < targetPath.Length; i++)
        {
            if (basePath[i] != targetPath[i])
            {
                break;
            }

            if (basePath[i] == (byte)'/')
            {
                lastSlashIndex = i;
            }
        }

        // If no common prefix at all
        if (i == 0)
        {
            if (targetPath.Length > destination.Length)
            {
                return false;
            }

            targetPath.CopyTo(destination);
            written = targetPath.Length;
            return true;
        }

        // If paths are identical
        if (i == basePath.Length && i == targetPath.Length)
        {
            written = 0;
            return true;
        }

        // Build the relative path using a stack-allocated buffer
        Span<byte> relPathBuffer = stackalloc byte[4096];
        if (relPathBuffer.Length < destination.Length)
        {
            relPathBuffer = destination;
        }

        int relPathPos = 0;

        // Count remaining slashes in base path and add "../" for each
        for (int j = i; j < basePath.Length; j++)
        {
            if (basePath[j] == (byte)'/')
            {
                if (relPathPos + 3 > relPathBuffer.Length)
                {
                    return false;
                }

                relPathBuffer[relPathPos++] = (byte)'.';
                relPathBuffer[relPathPos++] = (byte)'.';
                relPathBuffer[relPathPos++] = (byte)'/';
            }
        }

        // If no "../" segments and target ended with file after common directory
        if (relPathPos == 0 && targetPath.Length - 1 == lastSlashIndex)
        {
            if (destination.Length < 2)
            {
                return false;
            }

            destination[0] = (byte)'.';
            destination[1] = (byte)'/';
            written = 2;
            return true;
        }

        // Append the remaining part of target path after the last common slash
        ReadOnlySpan<byte> remainingTarget = targetPath.Slice(lastSlashIndex + 1);
        if (relPathPos + remainingTarget.Length > relPathBuffer.Length)
        {
            return false;
        }

        remainingTarget.CopyTo(relPathBuffer.Slice(relPathPos));
        relPathPos += remainingTarget.Length;

        // Copy to destination if we used a different buffer
        if (!relPathBuffer.Overlaps(destination))
        {
            if (relPathPos > destination.Length)
            {
                return false;
            }

            relPathBuffer.Slice(0, relPathPos).CopyTo(destination);
        }

        written = relPathPos;
        return true;
    }

    private static bool CheckForColonInFirstPathSegment(ReadOnlySpan<byte> pathString)
    {
        // Check for anything that may terminate the first regular path segment or an illegal colon
        for (int i = 0; i < pathString.Length; i++)
        {
            byte b = pathString[i];
            if (b == (byte)'/' || b == (byte)'?' || b == (byte)'#')
            {
                // Reached end of first segment without finding colon
                return false;
            }

            if (b == (byte)':')
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryApply(ReadOnlySpan<byte> baseUri, in Utf8UriOffset baseUriOffsets, Flags baseUriFlags, ReadOnlySpan<byte> targetUri, in Utf8UriOffset targetUriOffsets, Flags targetUriFlags, Span<byte> destination, out int written, bool strict = true)
    {
        written = 0;

        // Check for authority-only reference (starts with "// ")
        bool targetIsAuthorityOnly = targetUri.Length >= 2 &&
                                      targetUri[0] == (byte)'/' &&
                                      targetUri[1] == (byte)'/';

        // Extract target (reference) URI components
        int targetSchemeLen = targetUriOffsets.User - targetUriOffsets.Scheme;
        ReadOnlySpan<byte> targetScheme = targetSchemeLen > 0 ? targetUri.Slice(targetUriOffsets.Scheme, targetSchemeLen) : ReadOnlySpan<byte>.Empty;

        int targetAuthorityLen = targetUriOffsets.Path - targetUriOffsets.User;
        ReadOnlySpan<byte> targetAuthority;
        ReadOnlySpan<byte> targetPath;

        if (targetIsAuthorityOnly && targetAuthorityLen == 0)
        {
            // Authority-only reference but parser didn't recognize it
            // Extract authority manually (skip "// " and take until path delimiter)
            int authEnd = 2;
            while (authEnd < targetUri.Length &&
                   targetUri[authEnd] != (byte)'/' &&
                   targetUri[authEnd] != (byte)'?' &&
                   targetUri[authEnd] != (byte)'#')
            {
                authEnd++;
            }

            targetAuthority = targetUri.Slice(2, authEnd - 2);
            _ = targetAuthority.Length;

            // Path starts after authority (if any)
            int targetPathLen = targetUriOffsets.Query - authEnd;
            targetPath = targetPathLen > 0 ? targetUri.Slice(authEnd, targetPathLen) : ReadOnlySpan<byte>.Empty;
        }
        else
        {
            targetAuthority = targetAuthorityLen > 0 ? targetUri.Slice(targetUriOffsets.User, targetAuthorityLen) : ReadOnlySpan<byte>.Empty;

            int targetPathLen = targetUriOffsets.Query - targetUriOffsets.Path;
            targetPath = targetPathLen > 0 ? targetUri.Slice(targetUriOffsets.Path, targetPathLen) : ReadOnlySpan<byte>.Empty;
        }

        int targetQueryLen = targetUriOffsets.Fragment - targetUriOffsets.Query;
        ReadOnlySpan<byte> targetQuery = targetQueryLen > 0 ? targetUri.Slice(targetUriOffsets.Query, targetQueryLen) : ReadOnlySpan<byte>.Empty;

        int targetFragmentLen = targetUriOffsets.End - targetUriOffsets.Fragment;
        ReadOnlySpan<byte> targetFragment = targetFragmentLen > 0 ? targetUri.Slice(targetUriOffsets.Fragment, targetFragmentLen) : ReadOnlySpan<byte>.Empty;

        // Extract base URI components
        int baseSchemeLen = baseUriOffsets.User - baseUriOffsets.Scheme;
        ReadOnlySpan<byte> baseScheme = baseSchemeLen > 0 ? baseUri.Slice(baseUriOffsets.Scheme, baseSchemeLen) : ReadOnlySpan<byte>.Empty;

        int baseAuthorityLen = baseUriOffsets.Path - baseUriOffsets.User;
        ReadOnlySpan<byte> baseAuthority = baseAuthorityLen > 0 ? baseUri.Slice(baseUriOffsets.User, baseAuthorityLen) : ReadOnlySpan<byte>.Empty;

        int basePathLen = baseUriOffsets.Query - baseUriOffsets.Path;
        ReadOnlySpan<byte> basePath = basePathLen > 0 ? baseUri.Slice(baseUriOffsets.Path, basePathLen) : ReadOnlySpan<byte>.Empty;

        int baseQueryLen = baseUriOffsets.Fragment - baseUriOffsets.Query;
        ReadOnlySpan<byte> baseQuery = baseQueryLen > 0 ? baseUri.Slice(baseUriOffsets.Query, baseQueryLen) : ReadOnlySpan<byte>.Empty;

        // Result components (will reference either base or target components)
        ReadOnlySpan<byte> resultScheme;
        ReadOnlySpan<byte> resultAuthority;
        ReadOnlySpan<byte> resultPath;
        ReadOnlySpan<byte> resultQuery;
        ReadOnlySpan<byte> resultFragment = targetFragment;

        // Rent buffer for path merge/normalization operations
        int bufferSize = basePath.Length + targetPath.Length + 1;
        byte[] pathBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(bufferSize, 256));

        try
        {
            // Non-strict mode: if schemes match, treat target as if it has no scheme
            if (!strict && targetScheme.Length > 0 && targetScheme.SequenceEqual(baseScheme))
            {
                targetScheme = ReadOnlySpan<byte>.Empty;
            }

            // RFC 3986 Section 5.2.2 - Transform References
            if (targetScheme.Length > 0)
            {
                // Target has scheme - use target scheme/authority/query and normalize path
                resultScheme = targetScheme;
                resultAuthority = targetAuthority;

                // Copy target path to buffer and normalize
                targetPath.CopyTo(pathBuffer);
                int normalizedLen = RemoveDotSegments(pathBuffer, targetPath.Length);
                resultPath = pathBuffer.AsSpan(0, normalizedLen);
                resultQuery = targetQuery;
            }
            else
            {
                // No scheme - use base scheme
                if (targetAuthority.Length > 0)
                {
                    // Target has authority - use target authority/query and normalize path
                    resultAuthority = targetAuthority;

                    // Copy target path to buffer and normalize
                    targetPath.CopyTo(pathBuffer);
                    int normalizedLen = RemoveDotSegments(pathBuffer, targetPath.Length);
                    resultPath = pathBuffer.AsSpan(0, normalizedLen);
                    resultQuery = targetQuery;
                }
                else
                {
                    // No authority
                    if (targetPath.Length == 0)
                    {
                        // Empty path - use base path
                        resultPath = basePath;
                        resultQuery = targetQuery.Length > 0 ? targetQuery : baseQuery;
                    }
                    else
                    {
                        // Has path
                        if (targetPath.Length > 0 && targetPath[0] == (byte)'/')
                        {
                            // Absolute path - normalize it
                            targetPath.CopyTo(pathBuffer);
                            int normalizedLen = RemoveDotSegments(pathBuffer, targetPath.Length);
                            resultPath = pathBuffer.AsSpan(0, normalizedLen);
                        }
                        else
                        {
                            // Relative path - merge with base path then normalize
                            int mergedLen = MergePaths(basePath, targetPath, baseAuthority.Length > 0, pathBuffer);
                            int normalizedLen = RemoveDotSegments(pathBuffer, mergedLen);
                            resultPath = pathBuffer.AsSpan(0, normalizedLen);
                        }

                        resultQuery = targetQuery;
                    }

                    resultAuthority = baseAuthority;
                }

                resultScheme = baseScheme;
            }

            // Assemble the result URI into destination
            int pos = 0;

            // Write scheme (includes ":")
            if (resultScheme.Length > 0)
            {
                if (pos + resultScheme.Length > destination.Length)
                {
                    return false;
                }

                resultScheme.CopyTo(destination[pos..]);
                pos += resultScheme.Length;
            }

            // Write authority
            if (resultAuthority.Length > 0)
            {
                // Check if authority already includes "// " prefix and scheme ends with ":// "
                int authorityStart = 0;
                if (resultScheme.Length >= 3 &&
                    resultScheme[resultScheme.Length - 3] == (byte)':' &&
                    resultScheme[resultScheme.Length - 2] == (byte)'/' &&
                    resultScheme[resultScheme.Length - 1] == (byte)'/' &&
                    resultAuthority.Length >= 2 &&
                    resultAuthority[0] == (byte)'/' &&
                    resultAuthority[1] == (byte)'/')
                {
                    // Skip the "// " prefix from authority since scheme already has it
                    authorityStart = 2;
                }

                int authorityLen = resultAuthority.Length - authorityStart;
                if (pos + authorityLen > destination.Length)
                {
                    return false;
                }

                resultAuthority.Slice(authorityStart).CopyTo(destination[pos..]);
                pos += authorityLen;
            }

            // Write path
            if (resultPath.Length > 0)
            {
                if (pos + resultPath.Length > destination.Length)
                {
                    return false;
                }

                resultPath.CopyTo(destination[pos..]);
                pos += resultPath.Length;
            }

            // Write query (includes "?")
            if (resultQuery.Length > 0)
            {
                if (pos + resultQuery.Length > destination.Length)
                {
                    return false;
                }

                resultQuery.CopyTo(destination[pos..]);
                pos += resultQuery.Length;
            }

            // Write fragment (includes "#")
            if (resultFragment.Length > 0)
            {
                if (pos + resultFragment.Length > destination.Length)
                {
                    return false;
                }

                resultFragment.CopyTo(destination[pos..]);
                pos += resultFragment.Length;
            }

            written = pos;
            return true;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pathBuffer);
        }
    }

    /// <summary>
    /// Merges a base path with a reference path according to RFC 3986 Section 5.2.3.
    /// </summary>
    private static int MergePaths(ReadOnlySpan<byte> basePath, ReadOnlySpan<byte> referencePath, bool baseHasAuthority, Span<byte> destination)
    {
        // RFC 3986: if base has authority and empty path, prepend "/" to reference
        if (baseHasAuthority && basePath.Length == 0)
        {
            destination[0] = (byte)'/';
            referencePath.CopyTo(destination[1..]);
            return 1 + referencePath.Length;
        }

        // Find last "/" in base path
        int lastSlash = basePath.LastIndexOf((byte)'/');
        if (lastSlash >= 0)
        {
            // Copy base up to and including the last "/"
            basePath[..(lastSlash + 1)].CopyTo(destination);
            referencePath.CopyTo(destination[(lastSlash + 1)..]);
            return lastSlash + 1 + referencePath.Length;
        }

        // No "/" found - just return reference path
        referencePath.CopyTo(destination);
        return referencePath.Length;
    }

    internal static bool TryFormatDisplay(ReadOnlySpan<byte> originalUri, in Utf8UriOffset offsets, Flags resultFlags, Span<byte> destination, out int written)
    {
        written = 0;

        // Always perform normalization - no fast path
        // (We normalize scheme, decode unreserved chars regardless of flags)
        bool hostNeedsLowercase = HostType(resultFlags) == Flags.DnsHostType && NotAny(resultFlags, Flags.CanonicalDnsHost);

        int pos = 0;

        // Scheme + separator: originalUri[Scheme..User] - always normalize to lowercase for display
        {
            int schemeLen = offsets.User - offsets.Scheme;
            if (schemeLen > 0)
            {
                ReadOnlySpan<byte> schemeSep = originalUri.Slice(offsets.Scheme, schemeLen);
                if (!TryWriteLowercasedAndNormalized(schemeSep, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // UserInfo: originalUri[User..Host]
        {
            int userLen = offsets.Host - offsets.User;
            if (userLen > 0)
            {
                ReadOnlySpan<byte> userInfo = originalUri.Slice(offsets.User, userLen);

                // Always decode for display
                if (!TryWriteUnescapedDisplayComponent(userInfo, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Host: up to Port (if explicit) or Path
        {
            bool hasExplicitPort = offsets.Port > 0 && offsets.Path > offsets.Port;
            int hostEnd = hasExplicitPort ? offsets.Port : offsets.Path;
            int hostLen = hostEnd - offsets.Host;
            if (hostLen > 0)
            {
                ReadOnlySpan<byte> host = originalUri.Slice(offsets.Host, hostLen);
                if (InFact(resultFlags, Flags.HostNotCanonical) || hostNeedsLowercase)
                {
                    if (!TryWriteLowercasedAndNormalized(host, destination, ref pos))
                    {
                        return false;
                    }
                }
                else
                {
                    if (pos + hostLen > destination.Length)
                    {
                        return false;
                    }

                    host.CopyTo(destination.Slice(pos));
                    pos += hostLen;
                }
            }
        }

        // Port: write only if non-default
        if (InFact(resultFlags, Flags.NotDefaultPort))
        {
            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = (byte)':';

            if (!TryWriteDecimalPort(offsets.PortValue, destination, ref pos))
            {
                return false;
            }
        }

        // Leading slash if absent
        if (InFact(resultFlags, Flags.FirstSlashAbsent))
        {
            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = (byte)'/';
        }

        // Path: originalUri[Path..Query]
        {
            int pathLen = offsets.Query - offsets.Path;
            if (pathLen > 0)
            {
                ReadOnlySpan<byte> path = originalUri.Slice(offsets.Path, pathLen);
                bool compress = InFact(resultFlags, Flags.ShouldBeCompressed);

                // Always decode for display
                if (!TryWriteDisplayPath(path, compress, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Query: originalUri[Query..Fragment]
        {
            int queryLen = offsets.Fragment - offsets.Query;
            if (queryLen > 0)
            {
                ReadOnlySpan<byte> query = originalUri.Slice(offsets.Query, queryLen);

                // Always decode for display
                if (!TryWriteUnescapedDisplayComponent(query, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Fragment: originalUri[Fragment..End]
        {
            int fragLen = offsets.End - offsets.Fragment;
            if (fragLen > 0)
            {
                ReadOnlySpan<byte> fragment = originalUri.Slice(offsets.Fragment, fragLen);

                // Always decode for display
                if (!TryWriteUnescapedDisplayComponent(fragment, destination, ref pos))
                {
                    return false;
                }
            }
        }

        written = pos;
        return true;
    }

    private static bool TryWriteLowercasedAndNormalized(ReadOnlySpan<byte> source, Span<byte> destination, ref int pos)
    {
        for (int i = 0; i < source.Length; i++)
        {
            if (pos >= destination.Length)
            {
                return false;
            }

            byte b = source[i];
            if (b >= (byte)'A' && b <= (byte)'Z')
            {
                destination[pos++] = (byte)(b | 0x20);
            }
            else if (b == (byte)'\\')
            {
                destination[pos++] = (byte)'/';
            }
            else
            {
                destination[pos++] = b;
            }
        }

        return true;
    }

    private static bool TryWriteUnescapedDisplayComponent(ReadOnlySpan<byte> source, Span<byte> destination, ref int pos)
    {
        for (int i = 0; i < source.Length; i++)
        {
            byte b = source[i];
            if (b == (byte)'%' && i + 2 < source.Length)
            {
                // Try to decode UTF-8 sequences for display
                if (TryDecodePercentEncodedUtf8(source, i, destination, ref pos, out int bytesConsumed))
                {
                    i += bytesConsumed - 1; // -1 because loop will increment
                    continue;
                }

                // Try decoding as single ASCII byte (unreserved only for display)
                char decoded = Utf8UriHelper.DecodeHexChars(source[i + 1], source[i + 2]);
                if (decoded != c_DummyChar && IsAscii(decoded) && Contains(decoded, Utf8UriHelper.Unreserved))
                {
                    if (pos >= destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = (byte)decoded;
                    i += 2;
                    continue;
                }
            }

            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = b;
        }

        return true;
    }

    private static bool TryWriteDisplayPath(ReadOnlySpan<byte> path, bool compress, Span<byte> destination, ref int pos)
    {
        if (!compress)
        {
            for (int i = 0; i < path.Length; i++)
            {
                byte b = path[i];

                // Convert backslashes
                if (b == (byte)'\\')
                {
                    if (pos >= destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = (byte)'/';
                    continue;
                }

                // Always decode unreserved characters for display
                if (b == (byte)'%' && i + 2 < path.Length)
                {
                    // Try to decode UTF-8 sequences for display
                    if (TryDecodePercentEncodedUtf8(path, i, destination, ref pos, out int bytesConsumed))
                    {
                        i += bytesConsumed - 1; // -1 because loop will increment
                        continue;
                    }

                    // Try decoding as single ASCII byte (unreserved only for display)
                    char decoded = Utf8UriHelper.DecodeHexChars(path[i + 1], path[i + 2]);
                    if (decoded != c_DummyChar && IsAscii(decoded) && Contains(decoded, Utf8UriHelper.Unreserved))
                    {
                        if (pos >= destination.Length)
                        {
                            return false;
                        }

                        destination[pos++] = (byte)decoded;
                        i += 2;
                        continue;
                    }
                }

                if (pos >= destination.Length)
                {
                    return false;
                }

                destination[pos++] = b;
            }

            return true;
        }

        // Path compression required: process into temp buffer, then apply dot-segment removal
        Span<byte> tempBuffer = path.Length <= 256
            ? stackalloc byte[256]
            : new byte[path.Length];
        int tempPos = 0;

        for (int i = 0; i < path.Length; i++)
        {
            byte b = path[i];

            // Convert backslashes
            if (b == (byte)'\\')
            {
                tempBuffer[tempPos++] = (byte)'/';
                continue;
            }

            // Always decode unreserved characters for display
            if (b == (byte)'%' && i + 2 < path.Length)
            {
                // Try to decode UTF-8 sequences for display
                if (TryDecodePercentEncodedUtf8(path, i, tempBuffer, ref tempPos, out int bytesConsumed))
                {
                    i += bytesConsumed - 1; // -1 because loop will increment
                    continue;
                }

                // Try decoding as single ASCII byte (unreserved only for display)
                char decoded = Utf8UriHelper.DecodeHexChars(path[i + 1], path[i + 2]);
                if (decoded != c_DummyChar && IsAscii(decoded) && Contains(decoded, Utf8UriHelper.Unreserved))
                {
                    tempBuffer[tempPos++] = (byte)decoded;
                    i += 2;
                    continue;
                }
            }

            tempBuffer[tempPos++] = b;
        }

        int compressedLen = RemoveDotSegments(tempBuffer, tempPos);

        if (pos + compressedLen > destination.Length)
        {
            return false;
        }

        tempBuffer.Slice(0, compressedLen).CopyTo(destination.Slice(pos));
        pos += compressedLen;
        return true;
    }

    private static bool TryWriteDecimalPort(int portValue, Span<byte> destination, ref int pos)
    {
        Span<byte> buffer = stackalloc byte[5]; // Max port is 65535
        int digits = 0;

        do
        {
            buffer[digits++] = (byte)('0' + (portValue % 10));
            portValue /= 10;
        }
        while (portValue > 0);

        if (pos + digits > destination.Length)
        {
            return false;
        }

        for (int i = digits - 1; i >= 0; i--)
        {
            destination[pos++] = buffer[i];
        }

        return true;
    }

    internal static bool TryFormatCanonical(ReadOnlySpan<byte> originalUri, in Utf8UriOffset offsets, Flags resultFlags, bool allowIri, Span<byte> destination, out int written)
    {
        written = 0;

        // Always perform normalization - no fast path
        // (We normalize scheme, decode unreserved chars, uppercase hex regardless of flags)
        bool hostNeedsLowercase = HostType(resultFlags) == Flags.DnsHostType && NotAny(resultFlags, Flags.CanonicalDnsHost);

        int pos = 0;

        // Scheme + separator: originalUri[Scheme..User] - always normalize to lowercase
        {
            int schemeLen = offsets.User - offsets.Scheme;
            if (schemeLen > 0)
            {
                ReadOnlySpan<byte> schemeSep = originalUri.Slice(offsets.Scheme, schemeLen);
                if (!TryWriteLowercasedAndNormalized(schemeSep, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // UserInfo: originalUri[User..Host]
        {
            int userLen = offsets.Host - offsets.User;
            if (userLen > 0)
            {
                ReadOnlySpan<byte> userInfo = originalUri.Slice(offsets.User, userLen);

                // Always normalize for canonical form
                if (!TryWriteCanonicalComponent(userInfo, allowIri, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Host: up to Port (if explicit) or Path
        {
            bool hasExplicitPort = offsets.Port > 0 && offsets.Path > offsets.Port;
            int hostEnd = hasExplicitPort ? offsets.Port : offsets.Path;
            int hostLen = hostEnd - offsets.Host;
            if (hostLen > 0)
            {
                ReadOnlySpan<byte> host = originalUri.Slice(offsets.Host, hostLen);
                if (InFact(resultFlags, Flags.E_HostNotCanonical) || hostNeedsLowercase)
                {
                    if (!TryWriteLowercasedAndNormalized(host, destination, ref pos))
                    {
                        return false;
                    }
                }
                else
                {
                    if (pos + hostLen > destination.Length)
                    {
                        return false;
                    }

                    host.CopyTo(destination.Slice(pos));
                    pos += hostLen;
                }
            }
        }

        // Port: write only if non-default
        if (InFact(resultFlags, Flags.NotDefaultPort))
        {
            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = (byte)':';

            if (!TryWriteDecimalPort(offsets.PortValue, destination, ref pos))
            {
                return false;
            }
        }

        // Leading slash if absent
        if (InFact(resultFlags, Flags.FirstSlashAbsent))
        {
            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = (byte)'/';
        }

        // Path: originalUri[Path..Query]
        {
            int pathLen = offsets.Query - offsets.Path;
            if (pathLen > 0)
            {
                ReadOnlySpan<byte> path = originalUri.Slice(offsets.Path, pathLen);

                // Always normalize path for canonical form
                bool compress = InFact(resultFlags, Flags.ShouldBeCompressed);
                if (!TryWriteCanonicalPath(path, compress, allowIri, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Query: originalUri[Query..Fragment]
        {
            int queryLen = offsets.Fragment - offsets.Query;
            if (queryLen > 0)
            {
                ReadOnlySpan<byte> query = originalUri.Slice(offsets.Query, queryLen);

                // Always normalize for canonical form
                if (!TryWriteCanonicalComponent(query, allowIri, destination, ref pos))
                {
                    return false;
                }
            }
        }

        // Fragment: originalUri[Fragment..End]
        {
            int fragLen = offsets.End - offsets.Fragment;
            if (fragLen > 0)
            {
                ReadOnlySpan<byte> fragment = originalUri.Slice(offsets.Fragment, fragLen);

                // Always normalize for canonical form
                if (!TryWriteCanonicalComponent(fragment, allowIri, destination, ref pos))
                {
                    return false;
                }
            }
        }

        written = pos;
        return true;
    }

    private static bool TryWriteCanonicalComponent(ReadOnlySpan<byte> source, bool allowIri, Span<byte> destination, ref int pos)
    {
        for (int i = 0; i < source.Length; i++)
        {
            byte b = source[i];

            // Handle percent-encoded sequences
            if (b == (byte)'%' && i + 2 < source.Length)
            {
                // If IRI is allowed, try to decode UTF-8 sequences
                if (allowIri && TryDecodePercentEncodedUtf8(source, i, destination, ref pos, out int bytesConsumed))
                {
                    i += bytesConsumed - 1; // -1 because loop will increment
                    continue;
                }

                // Try decoding as single ASCII byte
                char decoded = Utf8UriHelper.DecodeHexChars(source[i + 1], source[i + 2]);
                if (decoded != c_DummyChar && IsAscii(decoded))
                {
                    // If it's an unreserved character, decode it
                    if (Contains(decoded, Utf8UriHelper.Unreserved))
                    {
                        if (pos >= destination.Length)
                        {
                            return false;
                        }

                        destination[pos++] = (byte)decoded;
                        i += 2;
                        continue;
                    }
                }

                // Normalize hex digits to uppercase (whether decoded or not)
                if (pos + 3 > destination.Length)
                {
                    return false;
                }

                destination[pos++] = (byte)'%';
                destination[pos++] = ToUpperHex(source[i + 1]);
                destination[pos++] = ToUpperHex(source[i + 2]);
                i += 2;
                continue;
            }

            // Handle non-ASCII characters
            if (!IsAscii((char)b))
            {
                if (allowIri)
                {
                    // Allow IRI characters through
                    if (pos >= destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = b;
                }
                else
                {
                    // Percent-encode non-ASCII
                    if (pos + 3 > destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = (byte)'%';
                    destination[pos++] = ToHexChar((byte)(b >> 4));
                    destination[pos++] = ToHexChar((byte)(b & 0x0F));
                }

                continue;
            }

            // Handle unreserved ASCII characters
            if (Contains((char)b, Utf8UriHelper.Unreserved))
            {
                if (pos >= destination.Length)
                {
                    return false;
                }

                destination[pos++] = b;
                continue;
            }

            // Handle reserved and other characters - keep as-is
            if (pos >= destination.Length)
            {
                return false;
            }

            destination[pos++] = b;
        }

        return true;
    }

    private static bool TryWriteCanonicalPath(ReadOnlySpan<byte> path, bool compress, bool allowIri, Span<byte> destination, ref int pos)
    {
        if (!compress)
        {
            for (int i = 0; i < path.Length; i++)
            {
                byte b = path[i];

                // Handle backslashes
                if (b == (byte)'\\')
                {
                    if (pos >= destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = (byte)'/';
                    continue;
                }

                // Handle percent-encoded sequences - always normalize
                if (b == (byte)'%' && i + 2 < path.Length)
                {
                    // If IRI is allowed, try to decode UTF-8 sequences
                    if (allowIri && TryDecodePercentEncodedUtf8(path, i, destination, ref pos, out int bytesConsumed))
                    {
                        i += bytesConsumed - 1; // -1 because loop will increment
                        continue;
                    }

                    // Try decoding as single ASCII byte
                    char decoded = Utf8UriHelper.DecodeHexChars(path[i + 1], path[i + 2]);
                    if (decoded != c_DummyChar && IsAscii(decoded))
                    {
                        // If it's an unreserved character, decode it
                        if (Contains(decoded, Utf8UriHelper.Unreserved))
                        {
                            if (pos >= destination.Length)
                            {
                                return false;
                            }

                            destination[pos++] = (byte)decoded;
                            i += 2;
                            continue;
                        }
                    }

                    // Normalize hex digits to uppercase (whether decoded or not)
                    if (pos + 3 > destination.Length)
                    {
                        return false;
                    }

                    destination[pos++] = (byte)'%';
                    destination[pos++] = ToUpperHex(path[i + 1]);
                    destination[pos++] = ToUpperHex(path[i + 2]);
                    i += 2;
                    continue;
                }

                // Handle non-ASCII characters
                if (!IsAscii((char)b))
                {
                    if (allowIri)
                    {
                        if (pos >= destination.Length)
                        {
                            return false;
                        }

                        destination[pos++] = b;
                    }
                    else
                    {
                        if (pos + 3 > destination.Length)
                        {
                            return false;
                        }

                        destination[pos++] = (byte)'%';
                        destination[pos++] = ToHexChar((byte)(b >> 4));
                        destination[pos++] = ToHexChar((byte)(b & 0x0F));
                    }

                    continue;
                }

                if (pos >= destination.Length)
                {
                    return false;
                }

                destination[pos++] = b;
            }

            return true;
        }

        // Path compression required: process into temp buffer, then apply dot-segment removal
        int bufferSize = path.Length * 3;
        byte[]? rentedBuffer = null;
        Span<byte> tempBuffer = bufferSize <= 768
            ? stackalloc byte[768]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(bufferSize));

        try
        {
            int tempPos = 0;

            for (int i = 0; i < path.Length; i++)
            {
                byte b = path[i];

                // Handle backslashes
                if (b == (byte)'\\')
                {
                    tempBuffer[tempPos++] = (byte)'/';
                    continue;
                }

                // Handle percent-encoded sequences - always normalize
                if (b == (byte)'%' && i + 2 < path.Length)
                {
                    // If IRI is allowed, try to decode UTF-8 sequences
                    if (allowIri && TryDecodePercentEncodedUtf8(path, i, tempBuffer, ref tempPos, out int bytesConsumed))
                    {
                        i += bytesConsumed - 1; // -1 because loop will increment
                        continue;
                    }

                    // Try decoding as single ASCII byte
                    char decoded = Utf8UriHelper.DecodeHexChars(path[i + 1], path[i + 2]);
                    if (decoded != c_DummyChar && IsAscii(decoded))
                    {
                        if (Contains(decoded, Utf8UriHelper.Unreserved))
                        {
                            tempBuffer[tempPos++] = (byte)decoded;
                            i += 2;
                            continue;
                        }
                    }

                    // Normalize hex digits to uppercase (whether decoded or not)
                    tempBuffer[tempPos++] = (byte)'%';
                    tempBuffer[tempPos++] = ToUpperHex(path[i + 1]);
                    tempBuffer[tempPos++] = ToUpperHex(path[i + 2]);
                    i += 2;
                    continue;
                }

                // Handle non-ASCII characters
                if (!IsAscii((char)b))
                {
                    if (allowIri)
                    {
                        tempBuffer[tempPos++] = b;
                    }
                    else
                    {
                        tempBuffer[tempPos++] = (byte)'%';
                        tempBuffer[tempPos++] = ToHexChar((byte)(b >> 4));
                        tempBuffer[tempPos++] = ToHexChar((byte)(b & 0x0F));
                    }

                    continue;
                }

                tempBuffer[tempPos++] = b;
            }

            int compressedLen = RemoveDotSegments(tempBuffer, tempPos);

            if (pos + compressedLen > destination.Length)
            {
                return false;
            }

            tempBuffer.Slice(0, compressedLen).CopyTo(destination.Slice(pos));
            pos += compressedLen;
            return true;
        }
        finally
        {
            if (rentedBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToHexChar(byte value)
    {
        return (byte)(value < 10 ? '0' + value : 'A' + (value - 10));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ToUpperHex(byte hexChar)
    {
        if (hexChar >= (byte)'a' && hexChar <= (byte)'f')
        {
            return (byte)(hexChar - 32);
        }

        return hexChar;
    }

    /// <summary>
    /// Checks if a Rune should be kept percent-encoded even in IRI/display formats.
    /// Returns true if the character should stay encoded (is problematic for display/security).
    /// </summary>
    private static bool ShouldStayEncoded(Rune rune)
    {
        int value = rune.Value;

        // C0 and C1 control characters (U+0000-U+001F, U+007F-U+009F)
        if (value <= 0x1F || (value >= 0x7F && value <= 0x9F))
        {
            return true;
        }

        // Zero-width and invisible formatting characters
        if (value >= 0x200B && value <= 0x200F) // ZWSP, ZWNJ, ZWJ, LRM, RLM
        {
            return true;
        }

        if (value >= 0x202A && value <= 0x202E) // Bidi embedding/override controls
        {
            return true;
        }

        if (value == 0xFEFF) // ZERO WIDTH NO-BREAK SPACE (BOM)
        {
            return true;
        }

        // Non-characters (U+FFFE, U+FFFF, and others ending in FFFE/FFFF)
        if ((value & 0xFFFE) == 0xFFFE) // Matches FFFE and FFFF in any plane
        {
            return true;
        }

        // FDD0..FDEF are also non-characters
        if (value >= 0xFDD0 && value <= 0xFDEF)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to decode a percent-encoded UTF-8 sequence and write it to the destination.
    /// Only decodes multi-byte UTF-8 sequences (non-ASCII characters).
    /// </summary>
    /// <param name="source">The source span containing percent-encoded bytes.</param>
    /// <param name="sourceIndex">The index in source where the sequence starts (at the '%').</param>
    /// <param name="destination">The destination span to write decoded UTF-8 bytes to.</param>
    /// <param name="destPos">The current position in the destination span (will be updated).</param>
    /// <param name="bytesConsumed">The number of bytes consumed from source (including % signs).</param>
    /// <returns>True if a valid multi-byte UTF-8 sequence was decoded and written; otherwise false.</returns>
    private static bool TryDecodePercentEncodedUtf8(ReadOnlySpan<byte> source, int sourceIndex, Span<byte> destination, ref int destPos, out int bytesConsumed)
    {
        bytesConsumed = 0;

        // First check if this looks like a multi-byte UTF-8 sequence
        // Multi-byte UTF-8 starts with a byte >= 0x80
        if (sourceIndex + 2 < source.Length && source[sourceIndex] == (byte)'%')
        {
            char firstDecoded = Utf8UriHelper.DecodeHexChars(source[sourceIndex + 1], source[sourceIndex + 2]);
            if (firstDecoded == c_DummyChar || firstDecoded < 0x80)
            {
                // Not a multi-byte UTF-8 sequence
                return false;
            }
        }
        else
        {
            return false;
        }

        // Try to decode up to 4 bytes (max UTF-8 sequence length)
        Span<byte> utf8Bytes = stackalloc byte[4];
        int utf8ByteCount = 0;
        int currentIndex = sourceIndex;

        // Collect consecutive percent-encoded bytes
        while (utf8ByteCount < 4 &&
               currentIndex + 2 < source.Length &&
               source[currentIndex] == (byte)'%')
        {
            char decoded = Utf8UriHelper.DecodeHexChars(source[currentIndex + 1], source[currentIndex + 2]);
            if (decoded == c_DummyChar)
            {
                break;
            }

            utf8Bytes[utf8ByteCount++] = (byte)decoded;
            currentIndex += 3;
        }

        if (utf8ByteCount == 0)
        {
            return false;
        }

        // Validate as UTF-8 using Rune (polyfilled for all versions)
        ReadOnlySpan<byte> utf8Sequence = utf8Bytes.Slice(0, utf8ByteCount);

        if (Rune.DecodeFromUtf8(utf8Sequence, out Rune rune, out int bytesRead) == System.Buffers.OperationStatus.Done)
        {
            // Check if this character should stay encoded (control chars, zero-width, etc.)
            if (ShouldStayEncoded(rune))
            {
                return false;
            }

            // Valid UTF-8 sequence that's safe to decode - copy the UTF-8 bytes directly
            if (destPos + bytesRead > destination.Length)
            {
                return false;
            }

            utf8Sequence.Slice(0, bytesRead).CopyTo(destination.Slice(destPos));
            destPos += bytesRead;
            bytesConsumed = bytesRead * 3; // Each byte was %XX (3 chars)
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes dot segments from a UTF-8 path buffer in-place per RFC 3986 Section 5.2.4.
    /// </summary>
    /// <param name="buffer">The path buffer to process in-place.</param>
    /// <param name="length">The number of valid bytes in the buffer.</param>
    /// <returns>The length of the resulting path after dot-segment removal.</returns>
    private static int RemoveDotSegments(Span<byte> buffer, int length)
    {
        int readPos = 0;
        int writePos = 0;

        while (readPos < length)
        {
            // A: If the input begins with "../" or "./"
            if (readPos + 3 <= length && buffer[readPos] == (byte)'.' && buffer[readPos + 1] == (byte)'.' && buffer[readPos + 2] == (byte)'/')
            {
                readPos += 3;
                continue;
            }

            if (readPos + 2 <= length && buffer[readPos] == (byte)'.' && buffer[readPos + 1] == (byte)'/')
            {
                readPos += 2;
                continue;
            }

            // B: If the input begins with "/./" or "/." where "." is a complete segment
            if (readPos + 3 <= length && buffer[readPos] == (byte)'/' && buffer[readPos + 1] == (byte)'.' && buffer[readPos + 2] == (byte)'/')
            {
                readPos += 2;
                continue;
            }

            if (readPos + 2 == length && buffer[readPos] == (byte)'/' && buffer[readPos + 1] == (byte)'.')
            {
                readPos += 2;
                buffer[writePos++] = (byte)'/';
                continue;
            }

            // C: If the input begins with "/../" or "/.." where ".." is a complete segment
            if (readPos + 4 <= length && buffer[readPos] == (byte)'/' && buffer[readPos + 1] == (byte)'.' && buffer[readPos + 2] == (byte)'.' && buffer[readPos + 3] == (byte)'/')
            {
                readPos += 3;
                while (writePos > 0 && buffer[writePos - 1] != (byte)'/')
                {
                    writePos--;
                }

                if (writePos > 0)
                {
                    writePos--;
                }

                continue;
            }

            if (readPos + 3 == length && buffer[readPos] == (byte)'/' && buffer[readPos + 1] == (byte)'.' && buffer[readPos + 2] == (byte)'.')
            {
                readPos += 3;
                while (writePos > 0 && buffer[writePos - 1] != (byte)'/')
                {
                    writePos--;
                }

                if (writePos > 0)
                {
                    writePos--;
                }

                buffer[writePos++] = (byte)'/';
                continue;
            }

            // D: If the input is only "." or ".."
            if (readPos + 1 == length && buffer[readPos] == (byte)'.')
            {
                readPos++;
                continue;
            }

            if (readPos + 2 == length && buffer[readPos] == (byte)'.' && buffer[readPos + 1] == (byte)'.')
            {
                readPos += 2;
                continue;
            }

            // E: Move the first path segment to output
            if (buffer[readPos] == (byte)'/')
            {
                buffer[writePos++] = buffer[readPos++];
            }

            while (readPos < length && buffer[readPos] != (byte)'/')
            {
                buffer[writePos++] = buffer[readPos++];
            }
        }

        return writePos;
    }

    /// <summary>
    /// Parses URI information from the specified UTF-8 URI string.
    /// </summary>
    /// <param name="uriString">The UTF-8 URI string to parse.</param>
    /// <param name="uriKind">The kind of URI to parse.</param>
    /// <param name="requireAbsolute">A value indicating whether to require an absolute URI.</param>
    /// <param name="allowIri">A value indicating whether to allow IRI parsing.</param>
    /// <param name="allowUNCPath">A value indicating whether to allow UNC path parsing.</param>
    /// <param name="uriInfo">When this method returns, contains the parsed URI offset information.</param>
    /// <param name="resultFlags">When this method returns, contains the parsing result flags.</param>
    /// <returns><see langword="true"/> if the URI was parsed successfully; otherwise, <see langword="false"/>.</returns>
    internal static bool ParseUriInfo(ReadOnlySpan<byte> uriString, Utf8UriKind uriKind, bool requireAbsolute, bool allowIri, out Utf8UriOffset uriInfo, out Flags resultFlags)
    {
        Utf8UriParser? syntax = null;
        Flags flags = Flags.Zero;
        Utf8UriParsingError err = ParseScheme(uriString, ref flags, ref syntax);

        if ((flags & Flags.HasUnicode) != 0 && !allowIri)
        {
            uriInfo = default;
            resultFlags = flags;
            return false;
        }

        // We won't use User factory for these errors
        if (err != Utf8UriParsingError.None)
        {
            if (uriKind != Utf8UriKind.Absolute && !requireAbsolute && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
            {
                // If it looks as a relative Uri, custom factory is ignored
                resultFlags = flags | Flags.UserEscaped;
                return GetUriInfoForRelativeReference(uriString, allowIri, out uriInfo);
            }

            uriInfo = default;
            resultFlags = flags;
            return false;
        }

        // Cannot be relative Uri if came here
        Debug.Assert(syntax != null);
        bool result = ParseCore(err, ref flags, ref syntax, uriKind, uriString, requireAbsolute, out uriInfo);
        if (!result)
        {
            uriInfo = default;
            resultFlags = flags;
            return false;
        }

        if ((flags & Flags.HasUnescapedUnicode) != 0 && !allowIri)
        {
            uriInfo = default;
            resultFlags = flags;
            return false;
        }

        resultFlags = flags;
        return true;
    }

    /// <summary>
    /// Validates the specified UTF-8 URI string.
    /// </summary>
    /// <param name="uriString">The UTF-8 URI string to validate.</param>
    /// <param name="uriKind">The kind of URI to validate.</param>
    /// <param name="requireAbsolute">A value indicating whether to require an absolute URI.</param>
    /// <param name="allowIri">A value indicating whether to allow IRI validation.</param>
    /// <param name="allowUNCPath">A value indicating whether to allow UNC path validation.</param>
    /// <returns><see langword="true"/> if the URI is valid; otherwise, <see langword="false"/>.</returns>
    internal static bool Validate(ReadOnlySpan<byte> uriString, Utf8UriKind uriKind, bool requireAbsolute, bool allowIri, bool allowUNCPath)
    {
        Utf8UriParser? syntax = null;
        Flags flags = Flags.Zero;
        Utf8UriParsingError err = ParseScheme(uriString, ref flags, ref syntax);

        if ((flags & Flags.HasUnicode) != 0 && !allowIri)
        {
            return false;
        }

        // We won't use User factory for these errors
        if (uriKind != Utf8UriKind.Absolute && err != Utf8UriParsingError.None)
        {
            // If it looks as a relative Uri, custom factory is ignored
            if (!requireAbsolute && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
                return ValidateRelativeReference(uriString, allowIri);

            return false;
        }

        // Cannot be relative Uri if came here
        Debug.Assert(syntax != null);
        bool result = ValidateCore(err, ref flags, ref syntax, uriKind, uriString, requireAbsolute, allowUNCPath);
        if (!result)
        {
            return false;
        }

        if ((flags & Flags.HasUnicode) != 0 && !allowIri)
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAbsoluteUri(Utf8UriParser? syntax) => syntax is not null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsImplicitFile(Flags flags)
    {
        return (flags & Flags.ImplicitFile) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDosPath(Flags flags)
    {
        return (flags & Flags.DosPath) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool UserDrivenParsing(Flags flags)
    {
        return (flags & Flags.UserDrivenParsing) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Flags HostType(Flags flags)
    {
        return flags & Flags.HostTypeMask;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsError(Flags flags)
    {
        return (flags & Flags.ErrorOrParsingRecursion) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NotAny(Flags allFlags, Flags checkFlags)
    {
        return (allFlags & checkFlags) == 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InFact(Flags allFlags, Flags checkFlags)
    {
        return (allFlags & checkFlags) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IriParsing(Utf8UriParser? syntax)
    {
        return syntax?.InFact(Utf8UriSyntaxFlags.AllowIriParsing) != false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFile(Utf8UriParser syntax)
    {
        return syntax.InFact(Utf8UriSyntaxFlags.FileLikeUri);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ValidatePathQueryAndFragmentSegment(ReadOnlySpan<byte> uriString, bool iriParsing)
    {
        return ValidateRelativeReference(uriString, iriParsing);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool GetUriInfoForRelativeReference(ReadOnlySpan<byte> uriString, bool iriParsing, out Utf8UriOffset uriInfo)
    {
        Utf8UriOffset info = default;
        int length = uriString.Length;

        GetUriInfoForQueryAndFragment(uriString, ref info, out int queryIdx, out int hashIdx);

        int idx = 0;
        if (ValidateRelativeReferenceCore(uriString, iriParsing, length, ref idx, queryIdx, hashIdx))
        {
            uriInfo = info;
            return true;
        }

        uriInfo = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void GetUriInfoForQueryAndFragment(ReadOnlySpan<byte> uriString, ref Utf8UriOffset info, out int queryIdx, out int hashIdx)
    {
        info.End = (ushort)uriString.Length;

        queryIdx = uriString.IndexOf((byte)'?');
        int hashStart = 0;

        if (queryIdx >= 0)
        {
            info.Query = (ushort)queryIdx;
            hashStart = queryIdx + 1;
        }
        else
        {
            info.Query = info.End;
        }

        hashIdx = uriString.Slice(hashStart).IndexOf((byte)'#');
        if (hashIdx >= 0)
        {
            info.Fragment = (ushort)(hashIdx + hashStart);
            if (info.Query == info.End)
            {
                info.Query = info.Fragment;
            }
        }
        else
        {
            info.Fragment = info.End;
        }
    }

    private static unsafe bool ValidateRelativeReference(ReadOnlySpan<byte> uriString, bool iriParsing)
    {
        int length = uriString.Length;

        int idx = 0;

        int queryIdx = uriString.IndexOf((byte)'?');

        int hashStart = queryIdx > 0 ? queryIdx + 1 : 0;

        int hashIdx = uriString.Slice(hashStart).IndexOf((byte)'#') + hashStart;

        return ValidateRelativeReferenceCore(uriString, iriParsing, length, ref idx, queryIdx, hashIdx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool ValidateRelativeReferenceCore(ReadOnlySpan<byte> uriString, bool iriParsing, int length, ref int idx, int queryIdx, int hashIdx)
    {
        if (idx < length && uriString[idx] is not ((byte)'?' or (byte)'#'))
        {
            // Check the path
            fixed (byte* str = uriString)
            {
                while (str[idx] == (byte)' ')
                {
                    idx++;
                }

                Check result = CheckCanonical(str, ref idx, length, (queryIdx >= 0) ? (byte)'?' : (hashIdx >= 0) ? (byte)'#' : c_EOL, iriParsing, queryIdx >= 0, hashIdx >= 0);
                if ((result & (Check.BackslashInPath | Check.ReservedFound | Check.NotIriCanonical)) != 0)
                {
                    return false;
                }

                if ((result & (Check.DisplayCanonical | Check.EscapedCanonical)) == 0)
                {
                    return false;
                }

                if (!iriParsing && (result & Check.FoundNonAscii) != 0)
                {
                    // If we are not Iri parsing, we cannot have non-ascii characters in the path
                    return false;
                }
            }
        }

        if (idx < length && uriString[idx] == '?')
        {
            // Move past the delimiter
            idx++;
            if (idx < length)
            {
                // Check the query
                fixed (byte* str = uriString)
                {
                    Check result = CheckCanonical(str, ref idx, length, (hashIdx >= 0) ? (byte)'#' : c_EOL, iriParsing, queryIdx >= 0, hashIdx >= 0);
                    if ((result & (Check.BackslashInPath | Check.ReservedFound | Check.NotIriCanonical)) != 0)
                    {
                        return false;
                    }

                    if (!iriParsing && (result & Check.FoundNonAscii) != 0)
                    {
                        // If we are not Iri parsing, we cannot have non-ascii characters in the query
                        return false;
                    }
                }
            }
        }

        if (idx < length && uriString[idx] == '#')
        {
            // Move past the deliimiter
            idx++;
            if (idx < length)
            {
                // Check the fragment
                fixed (byte* str = uriString)
                {
                    Check result = CheckCanonical(str, ref idx, length, c_EOL, iriParsing, queryIdx >= 0, hashIdx >= 0);
                    if ((result & (Check.BackslashInPath | Check.ReservedFound | Check.NotIriCanonical)) != 0)
                    {
                        return false;
                    }

                    if (!iriParsing && (result & Check.FoundNonAscii) != 0)
                    {
                        // If we are not Iri parsing, we cannot have non-ascii characters in the query
                        return false;
                    }
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ParseCore(Utf8UriParsingError err, ref Flags flags, ref Utf8UriParser syntax, Utf8UriKind uriKind, ReadOnlySpan<byte> uriString, bool requireAbsolute, out Utf8UriOffset uriInfo)
    {
        Debug.Assert(err == Utf8UriParsingError.None);

        Utf8UriOffset info = default;
        info.End = (ushort)uriString.Length;

        if (IsImplicitFile(flags))
        {
            // V1 compat
            // A relative Uri wins over implicit UNC path unless the UNC path is of the form "\\something" and
            // uriKind != Absolute
            // A relative Uri wins over implicit Unix path unless uriKind == Absolute
            if (NotAny(flags, Flags.DosPath) &&
                uriKind != Utf8UriKind.Absolute &&
#if NET
                ((uriKind == Utf8UriKind.Relative || (uriString.Length >= 2 && (uriString[0] != (byte)'\\' || uriString[1] != (byte)'\\')))
            || (!OperatingSystem.IsWindows() && InFact(flags, Flags.UnixPath))))
#else
                ((uriKind == Utf8UriKind.Relative || (uriString.Length >= 2 && (uriString[0] != (byte)'\\' || uriString[1] != (byte)'\\')))))
#endif
            {
                flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri
                GetUriInfoForQueryAndFragment(uriString, ref info, out _, out _);
                syntax = null!; // make it be relative Uri
                uriInfo =
                    new()
                    {
                        Query = info.Query,
                        Fragment = info.Fragment,
                        End = info.End
                    };

                return !requireAbsolute;

                // Otherwise an absolute file Uri wins when it's of the form "\\something"
            }

            // V1 compat issue
            // We should support relative Uris of the form c:\bla or c:/bla
            else if (uriKind != Utf8UriKind.Absolute && InFact(flags, Flags.DosPath))
            {
                flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri
                GetUriInfoForQueryAndFragment(uriString, ref info, out _, out _);
                syntax = null!; // make it be relative Uri
                uriInfo =
                    new()
                    {
                        Query = info.Query,
                        Fragment = info.Fragment,
                        End = info.End
                    };

                return !requireAbsolute;

                // Otherwise an absolute file Uri wins when it's of the form "c:\something"
            }
        }

        if (IriParsing(syntax) && CheckForUnicodeOrEscapedUnreserved(uriString, out Flags localFlags))
        {
            flags |= localFlags;
        }

        bool success = true;

        if (syntax != null)
        {
            if ((err = PrivateParseMinimal(uriString, ref flags, ref syntax)) != Utf8UriParsingError.None)
            {
                if (uriKind != Utf8UriKind.Absolute && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
                {
                    // RFC 3986 Section 5.4.2 - http:(relativeUri) may be considered a valid relative Uri.
                    int offset = syntax.SchemeName.Length + 1;
                    bool result = GetUriInfoForRelativeReference(uriString.Slice(offset), IriParsing(syntax), out info);
                    uriInfo =
                        new()
                        {
                            Scheme = (ushort)offset,
                            End = (ushort)(info.End + offset),
                            User = (ushort)offset,
                            Host = (ushort)offset,
                            Port = (ushort)offset,
                            PortValue = 0,
                            Path = (ushort)(info.Path + offset),
                            Query = (ushort)(info.Query + offset),
                            Fragment = (ushort)(info.Fragment + offset)
                        };

                    syntax = null!; // convert to relative uri
                    flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri
                    return result;
                }
                else
                    success = false;
            }
            else if (uriKind == Utf8UriKind.Relative)
            {
                // Here we know that we can create an absolute Uri, but the user has requested only a relative one
                success = false;
            }
            else
            {
                success = true;
            }

            // will return from here
            // In this scenario we need to parse the whole string
            if (!success || !ValidateRemaining(err, ref flags, syntax, uriKind, uriString, out info))
            {
                uriInfo = default;
                return false;
            }
        }

        // If we encountered any parsing errors that indicate this may be a relative Uri,
        // and we'll allow relative Uri's, then create one.
        else if (err != Utf8UriParsingError.None && uriKind != Utf8UriKind.Absolute
            && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
        {
            success = true;
            flags &= (Flags.UserEscaped | Flags.HasUnicode); // the only flags that makes sense for a relative uri
        }
        else
        {
            success = false;
        }

        if ((flags & Flags.UncPath) != 0)
        {
            uriInfo = default;
            return false;
        }

        uriInfo = success ? info : default;
        return success;
    }

    private static bool ValidateCore(Utf8UriParsingError err, ref Flags flags, ref Utf8UriParser syntax, Utf8UriKind uriKind, ReadOnlySpan<byte> uriString, bool requireAbsolute, bool allowUNCPath)
    {
        if (err == Utf8UriParsingError.None)
        {
            if (IsImplicitFile(flags))
            {
                // V1 compat
                // A relative Uri wins over implicit UNC path unless the UNC path is of the form "\\something" and
                // uriKind != Absolute
                // A relative Uri wins over implicit Unix path unless uriKind == Absolute
                if (NotAny(flags, Flags.DosPath) &&
                    uriKind != Utf8UriKind.Absolute &&
#if NET
                   ((uriKind == Utf8UriKind.Relative || (uriString.Length >= 2 && (uriString[0] != (byte)'\\' || uriString[1] != (byte)'\\')))
                || (!OperatingSystem.IsWindows() && InFact(flags, Flags.UnixPath))))
#else
                   ((uriKind == Utf8UriKind.Relative || (uriString.Length >= 2 && (uriString[0] != (byte)'\\' || uriString[1] != (byte)'\\')))))
#endif
                {
                    syntax = null!; // make it be relative Uri
                    flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri

                    if (CheckForUnicodeOrEscapedUnreserved(uriString, out Flags localFlags))
                    {
                        flags |= localFlags;
                    }

                    return !requireAbsolute;

                    // Otherwise an absolute file Uri wins when it's of the form "\\something"
                }

                // V1 compat issue
                // We should support relative Uris of the form c:\bla or c:/bla
                else if (uriKind != Utf8UriKind.Absolute && InFact(flags, Flags.DosPath))
                {
                    syntax = null!; // make it be relative Uri
                    flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri
                    return !requireAbsolute;

                    // Otherwise an absolute file Uri wins when it's of the form "c:\something"
                }
            }
        }
        else if (err > Utf8UriParsingError.LastRelativeUriOkErrIndex)
        {
            // This is a fatal error based solely on scheme name parsing
            return false;
        }

        if (IriParsing(syntax) && CheckForUnicodeOrEscapedUnreserved(uriString, out Flags localFlags2))
        {
            flags |= localFlags2;
        }

        bool success = true;

        if (syntax != null)
        {
            if ((err = PrivateParseMinimal(uriString, ref flags, ref syntax)) != Utf8UriParsingError.None)
            {
                if (uriKind != Utf8UriKind.Absolute && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
                {
                    // RFC 3986 Section 5.4.2 - http:(relativeUri) may be considered a valid relative Uri.
                    syntax = null!; // convert to relative uri
                    flags &= Flags.UserEscaped; // the only flag that makes sense for a relative uri
                    return true;
                }
                else
                    success = false;
            }
            else if (uriKind == Utf8UriKind.Relative)
            {
                // Here we know that we can create an absolute Uri, but the user has requested only a relative one
                success = false;
            }
            else
            {
                success = true;
            }

            // will return from here
            // In this scenario we need to parse the whole string
            if (!success || !ValidateRemaining(err, ref flags, syntax, uriKind, uriString, out _))
            {
                return false;
            }
        }

        // If we encountered any parsing errors that indicate this may be a relative Uri,
        // and we'll allow relative Uri's, then create one.
        else if (err != Utf8UriParsingError.None && uriKind != Utf8UriKind.Absolute
            && err <= Utf8UriParsingError.LastRelativeUriOkErrIndex)
        {
            success = true;
            flags &= (Flags.UserEscaped | Flags.HasUnicode); // the only flags that makes sense for a relative uri
        }
        else
        {
            success = false;
        }

        if (!allowUNCPath && (flags & Flags.UncPath) != 0)
        {
            return false;
        }

        return success;
    }

    private static Utf8UriOffset EnsureUriInfo(Utf8UriParser syntax, ref Flags flags, ReadOnlySpan<byte> uriString)
    {
        Debug.Assert((flags & Flags.MinimalUriInfoSet) == 0);
        return CreateUriInfo(syntax, ref flags, uriString);
    }

    // The method is called when we have to access info members.
    // This will create the info based on the copied parser context.
    // If multi-threading, this method may do duplicated yet harmless work.
    private static unsafe Utf8UriOffset CreateUriInfo(Utf8UriParser syntax, ref Flags flags, ReadOnlySpan<byte> uriString)
    {
        Utf8UriOffset info = default;

        // This will be revisited in ParseRemaining but for now just have it at least uriString.Length
        info.End = (ushort)uriString.Length;

        if (UserDrivenParsing(flags))
            goto Done;

        int idx;
        bool notCanonicalScheme = false;

        // The uriString may have leading spaces, figure that out
        // plus it will set idx value for next steps
        if ((flags & Flags.ImplicitFile) != 0)
        {
            idx = 0;
            while (Utf8UriHelper.IsLWS(uriString[idx]))
            {
                ++idx;
                ++info.Scheme;
            }

            if (InFact(flags, Flags.UncPath))
            {
                // For implicit file AND Unc only
                idx += 2;

                // skip any other slashes (compatibility with V1.0 parser)
                int end = (int)(flags & Flags.IndexMask);
                while (idx < end && (uriString[idx] == (byte)'/' || uriString[idx] == (byte)'\\'))
                {
                    ++idx;
                }
            }
        }
        else
        {
            // This is NOT an ImplicitFile uri
            idx = syntax.SchemeName.Length;

            while (uriString[idx++] != (byte)':')
            {
                ++info.Scheme;
            }

            if ((flags & Flags.AuthorityFound) != 0)
            {
                if (uriString[idx] == (byte)'\\' || uriString[idx + 1] == (byte)'\\')
                    notCanonicalScheme = true;

                idx += 2;
                if ((flags & (Flags.UncPath | Flags.DosPath)) != 0)
                {
                    // Skip slashes if it was allowed during ctor time
                    // NB: Today this is only allowed if a Unc or DosPath was found after the scheme
                    int end = (int)(flags & Flags.IndexMask);
                    while (idx < end && (uriString[idx] == (byte)'/' || uriString[idx] == (byte)'\\'))
                    {
                        notCanonicalScheme = true;
                        ++idx;
                    }
                }
            }
        }

        // Some schemes (mailto) do not have Authority-based syntax, still they do have a port
        if (syntax.DefaultPort != Utf8UriParser.NoDefaultPort)
            info.PortValue = (ushort)syntax.DefaultPort;

        // Here we set the indexes for already parsed components
        if ((flags & Flags.HostTypeMask) == Flags.UnknownHostType
            || InFact(flags, Flags.DosPath))
        {
            // there is no Authority component defined
            info.User = (ushort)(flags & Flags.IndexMask);
            info.Host = info.User;
            info.Path = info.User;
            info.Port = info.User;
            flags &= ~Flags.IndexMask;
            if (notCanonicalScheme)
            {
                flags |= Flags.SchemeNotCanonical;
            }

            goto Done;
        }

        info.User = (ushort)idx;

        // Basic Host Type does not have userinfo and port
        if (HostType(flags) == Flags.BasicHostType)
        {
            info.Host = (ushort)idx;
            info.Path = (ushort)(flags & Flags.IndexMask);
            flags &= ~Flags.IndexMask;
            goto Done;
        }

        if ((flags & Flags.HasUserInfo) != 0)
        {
            // we previously found a userinfo, get it again
            while (uriString[idx] != '@')
            {
                ++idx;
            }

            ++idx;
            info.Host = (ushort)idx;
        }
        else
        {
            info.Host = (ushort)idx;
        }

        // Now reload the end of the parsed host
        idx = (int)(flags & Flags.IndexMask);

        // From now on we do not need IndexMask bits, and reuse the space for X_NotCanonical flags
        // clear them now
        flags &= ~Flags.IndexMask;

        // If this is not canonical, don't count on user input to be good
        if (notCanonicalScheme)
        {
            flags |= Flags.SchemeNotCanonical;
        }

        // Guessing this is a path start
        info.Path = (ushort)idx;

        // parse Port if any. The new spec allows a port after ':' to be empty (assuming default?)
        bool notEmpty = false;

        // Note we already checked on general port syntax in ParseMinimal()
        if (idx < info.End)
        {
            fixed (byte* userString = uriString)
            {
                if (userString[idx] == (byte)':')
                {
                    int port = 0;
                    info.Port = (ushort)idx;

                    // Check on some non-canonical cases http:// host:0324/, http:// host:03, http:// host:0, etc
                    if (++idx < info.End)
                    {
                        port = userString[idx] - (byte)'0';
                        if ((uint)port <= ((byte)'9' - (byte)'0'))
                        {
                            notEmpty = true;
                            if (port == 0)
                            {
                                flags |= (Flags.PortNotCanonical | Flags.E_PortNotCanonical);
                            }

                            for (++idx; idx < info.End; ++idx)
                            {
                                int val = userString[idx] - (byte)'0';
                                if ((uint)val > ((byte)'9' - (byte)'0'))
                                {
                                    break;
                                }

                                port = (port * 10 + val);
                            }
                        }
                    }

                    if (notEmpty && syntax.DefaultPort != port)
                    {
                        info.PortValue = (ushort)port;
                        flags |= Flags.NotDefaultPort;
                    }
                    else
                    {
                        // This will tell that we do have a ':' but the port value does
                        // not follow to canonical rules
                        flags |= (Flags.PortNotCanonical | Flags.E_PortNotCanonical);
                    }

                    info.Path = (ushort)idx;
                }
            }
        }

    Done:
        flags |= Flags.MinimalUriInfoSet;
        return info;
    }

    // This method does:
    // - Creates info member
    // - checks all components up to path on their canonical representation
    // - continues parsing starting the path position
    // - Sets the offsets of remaining components
    // - Sets the Canonicalization flags if applied
    private static unsafe bool ValidateRemaining(Utf8UriParsingError err, ref Flags flags, Utf8UriParser syntax, Utf8UriKind uriKind, ReadOnlySpan<byte> uriString, out Utf8UriOffset uriInfo)
    {
        // ensure we parsed up to the path
        Utf8UriOffset info = EnsureUriInfo(syntax, ref flags, uriString);

        Flags cF = Flags.Zero;

        if (UserDrivenParsing(flags))
            goto Done;

        // Do we have to continue building Iri'zed string from original string
        int idx = info.Scheme;
        int length = uriString.Length;
        Check result = Check.None;
        Utf8UriSyntaxFlags syntaxFlags = syntax.Flags;

        fixed (byte* str = uriString)
        {
            GetLengthWithoutTrailingSpaces(uriString, ref length, idx);

            if (IsImplicitFile(flags))
            {
                cF |= Flags.SchemeNotCanonical;
            }
            else
            {
                int i;
                string schemeName = syntax.SchemeName;
                for (i = 0; i < schemeName.Length; ++i)
                {
                    if (schemeName[i] != str[idx + i])
                        cF |= Flags.SchemeNotCanonical;
                }

                // For an authority Uri only // after the scheme would be canonical
                // (for compatibility with: http:\\host)
                if (((flags & Flags.AuthorityFound) != 0) && (idx + i + 3 >= length || str[idx + i + 1] != (byte)'/' ||
                    str[idx + i + 2] != (byte)'/'))
                {
                    cF |= Flags.SchemeNotCanonical;
                }
            }

            // Check the form of the user info
            if ((flags & Flags.HasUserInfo) != 0)
            {
                idx = info.User;
                result = CheckCanonical(str, ref idx, info.Host, (byte)'@', syntax, ref flags);
                if ((result & Check.DisplayCanonical) == 0)
                {
                    cF |= Flags.UserNotCanonical;
                }

                if ((result & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                {
                    cF |= Flags.E_UserNotCanonical;
                }

                if (IriParsing(syntax) && ((result & (Check.DisplayCanonical | Check.EscapedCanonical | Check.BackslashInPath
                                                | Check.FoundNonAscii | Check.NotIriCanonical))
                                                == (Check.DisplayCanonical | Check.FoundNonAscii)))
                {
                    cF |= Flags.UserIriCanonical;
                }
            }
        }

        // We have already checked on the port in EnsureUriInfo() that calls CreateUriInfo
        // Parsing the Path if any
        // For iri parsing if we found unicode the idx has offset into _originalUnicodeString..
        // so restart parsing from there and make info.Path as uriString.Length
        idx = info.Path;

        fixed (byte* str = uriString)
        {
            if (IsImplicitFile(flags) || ((syntaxFlags & (Utf8UriSyntaxFlags.MayHaveQuery | Utf8UriSyntaxFlags.MayHaveFragment)) == 0))
            {
                result = CheckCanonical(str, ref idx, length, c_EOL, syntax, ref flags);
            }
            else
            {
                result = CheckCanonical(str, ref idx, length, (((syntaxFlags & Utf8UriSyntaxFlags.MayHaveQuery) != 0)
                    ? (byte)'?' : syntax.InFact(Utf8UriSyntaxFlags.MayHaveFragment) ? (byte)'#' : c_EOL), syntax, ref flags);
            }

            // ATTN:
            // This may render problems for unknown schemes, but in general for an authority based Uri
            // (that has slashes) a path should start with "/"
            // This becomes more interesting knowing how a file uri is used in "file:// c:/path"
            // It will be converted to file:/// c:/path
            // However, even more interesting is that vsmacros:// c:\path will not add the third slash in the _canonical_ case
            // We use special syntax flag to check if the path is rooted, i.e. has a first slash
            if (((flags & Flags.AuthorityFound) != 0) && ((syntaxFlags & Utf8UriSyntaxFlags.PathIsRooted) != 0)
                && (info.Path == length || (str[info.Path] != '/' && str[info.Path] != (byte)'\\')))
            {
                cF |= Flags.FirstSlashAbsent;
            }
        }

        // Check the need for compression or backslashes conversion
        // we included IsDosPath since it may come with other than FILE uri, for ex. scheme:// C:\path
        // (This is very unfortunate that the original design has included that feature)
        bool nonCanonical = false;
        if (IsDosPath(flags) || (((flags & Flags.AuthorityFound) != 0) &&
            (((syntaxFlags & (Utf8UriSyntaxFlags.CompressPath | Utf8UriSyntaxFlags.ConvertPathSlashes)) != 0) ||
            syntax.InFact(Utf8UriSyntaxFlags.UnEscapeDotsAndSlashes))))
        {
            if (((result & Check.DotSlashEscaped) != 0) && syntax.InFact(Utf8UriSyntaxFlags.UnEscapeDotsAndSlashes))
            {
                cF |= (Flags.E_PathNotCanonical | Flags.PathNotCanonical);
                nonCanonical = true;
            }

            if (((syntaxFlags & (Utf8UriSyntaxFlags.ConvertPathSlashes)) != 0) && (result & Check.BackslashInPath) != 0)
            {
                cF |= Flags.PathNotCanonical;

                if (!IsDosPath(flags))
                {
                    cF |= Flags.E_PathNotCanonical;
                }

                nonCanonical = true;
            }

            if (((syntaxFlags & (Utf8UriSyntaxFlags.CompressPath)) != 0) && ((cF & Flags.E_PathNotCanonical) != 0 ||
                (result & Check.DotSlashAttn) != 0))
            {
                cF |= Flags.ShouldBeCompressed;
            }

            if ((result & Check.BackslashInPath) != 0)
                cF |= Flags.BackslashInPath;
        }
        else if ((result & Check.BackslashInPath) != 0)
        {
            // for a "generic" path '\' should be escaped
            cF |= Flags.E_PathNotCanonical;
            nonCanonical = true;
        }

        if ((result & Check.DisplayCanonical) == 0)
        {
            // For implicit file the user string is usually in perfect display format,
            // Hence, ignoring complains from CheckCanonical()
            // V1 compat. In fact we should simply ignore dontEscape parameter for Implicit file.
            // Currently we don't.
            if (((flags & Flags.ImplicitFile) == 0) || ((flags & Flags.UserEscaped) != 0) ||
                (result & Check.ReservedFound) != 0)
            {
                // means it's found as escaped or has unescaped Reserved Characters
                cF |= Flags.PathNotCanonical;
                nonCanonical = true;
            }
        }

        if (((flags & Flags.ImplicitFile) != 0) && (result & (Check.ReservedFound | Check.EscapedCanonical)) != 0)
        {
            // need to escape reserved chars or re-escape '%' if an "escaped sequence" was found
            result &= ~Check.EscapedCanonical;
        }

        if ((result & Check.EscapedCanonical) == 0)
        {
            // means it's found as not completely escaped
            cF |= Flags.E_PathNotCanonical;
        }

        if (IriParsing(syntax) && !nonCanonical && ((result & (Check.DisplayCanonical | Check.EscapedCanonical
                        | Check.FoundNonAscii | Check.NotIriCanonical))
                        == (Check.DisplayCanonical | Check.FoundNonAscii)))
        {
            cF |= Flags.PathIriCanonical;
        }

        // Now we've got to parse the Query if any. Note that Query requires the presence of '?'
        info.Query = (ushort)idx;

        fixed (byte* str = uriString)
        {
            if (idx < length && str[idx] == '?')
            {
                ++idx; // This is to exclude first '?' character from checking
                result = CheckCanonical(str, ref idx, length, ((syntaxFlags & (Utf8UriSyntaxFlags.MayHaveFragment)) != 0)
                    ? (byte)'#' : c_EOL, syntax, ref flags);

                if ((result & Check.DisplayCanonical) == 0)
                {
                    cF |= Flags.QueryNotCanonical;
                }

                if (IriParsing(syntax) && ((result & (Check.DisplayCanonical | Check.EscapedCanonical | Check.BackslashInPath
                            | Check.FoundNonAscii | Check.NotIriCanonical))
                            == (Check.DisplayCanonical | Check.FoundNonAscii)))
                {
                    cF |= Flags.QueryIriCanonical;
                }

                if ((result & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                {
                    if ((cF & Flags.QueryIriCanonical) == 0)
                    {
                        cF |= Flags.E_QueryNotCanonical;
                    }
                }
            }
        }

        info.Fragment = (ushort)idx;

        fixed (byte* str = uriString)
        {
            if (idx < length && str[idx] == (byte)'#')
            {
                ++idx; // This is to exclude first '#' character from checking

                // We don't using c_DummyChar since want to allow '?' and '#' as unescaped
                result = CheckCanonical(str, ref idx, length, c_EOL, syntax, ref flags);
                if ((result & Check.DisplayCanonical) == 0)
                {
                    cF |= Flags.FragmentNotCanonical;
                }

                if (IriParsing(syntax) && ((result & (Check.DisplayCanonical | Check.EscapedCanonical | Check.BackslashInPath
                            | Check.FoundNonAscii | Check.NotIriCanonical))
                            == (Check.DisplayCanonical | Check.FoundNonAscii)))
                {
                    cF |= Flags.FragmentIriCanonical;
                }

                if ((result & (Check.EscapedCanonical | Check.BackslashInPath)) != Check.EscapedCanonical)
                {
                    if ((cF & Flags.FragmentIriCanonical) == 0)
                    {
                        cF |= Flags.E_FragmentNotCanonical;
                    }
                }
            }
        }

        info.End = (ushort)idx;

    Done:
        cF |= Flags.AllUriInfoSet;

        uriInfo = info;
        return (IriParsing(syntax) && ((cF & Flags.IriCanonical) != 0)) || (cF & Flags.E_NonCanonical) == 0;
    }

    // Used by ParseRemaining
    private static unsafe Check CheckCanonical(byte* str, ref int idx, int end, byte delim, Utf8UriParser syntax, ref Flags flags)
    {
        Check res = Check.None;
        bool needsEscaping = false;
        bool foundEscaping = false;
        bool iriParsing = IriParsing(syntax);

        byte c;
        int i = idx;
        for (; i < end;)
        {
            int bytesConsumed = 1;
            c = str[i];

            // Fast path for common ASCII characters that don't need special handling
            if ((c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z') || (c >= (byte)'0' && c <= (byte)'9'))
            {
                // alphanumeric characters are always safe
                i++;
                continue;
            }

            // Control chars usually should be escaped in any case
            if (c <= (byte)'\x1F' || (c >= (byte)'\x7F' && c <= (byte)'\x9F'))
            {
                needsEscaping = true;
                foundEscaping = true;
                res |= Check.ReservedFound;
            }
            else if (c > '~')
            {
                if (iriParsing)
                {
                    res |= Check.FoundNonAscii;
                    Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(str + i, end - i), out Rune rune, out bytesConsumed);

                    if (!Utf8IriHelper.CheckIriUnicodeRange((uint)rune.Value, true))
                    {
                        res |= Check.NotIriCanonical;
                    }
                }

                needsEscaping = true;
            }
            else if (c == delim)
            {
                break;
            }
            else if (delim == (byte)'?' && c == (byte)'#' && (syntax?.InFact(Utf8UriSyntaxFlags.MayHaveFragment) == true))
            {
                // this is a special case when deciding on Query/Fragment
                break;
            }
            else if (c == (byte)'?')
            {
                if (IsImplicitFile(flags) || (syntax?.InFact(Utf8UriSyntaxFlags.MayHaveQuery) == false
                    && delim != c_EOL))
                {
                    // If found as reserved this char is not suitable for safe unescaped display
                    // Will need to escape it when both escaping and unescaping the string
                    res |= Check.ReservedFound;
                    foundEscaping = true;
                    needsEscaping = true;
                }
            }
            else if (c == (byte)'#')
            {
                needsEscaping = true;
                if (IsImplicitFile(flags) || (syntax?.InFact(Utf8UriSyntaxFlags.MayHaveFragment) == false))
                {
                    // If found as reserved this char is not suitable for safe unescaped display
                    // Will need to escape it when both escaping and unescaping the string
                    res |= Check.ReservedFound;
                    foundEscaping = true;
                }
            }
            else if (c == (byte)'/' || c == (byte)'\\')
            {
                if ((res & Check.BackslashInPath) == 0 && c == (byte)'\\')
                {
                    res |= Check.BackslashInPath;
                }

                if ((res & Check.DotSlashAttn) == 0 && i + 1 != end && (str[i + 1] == (byte)'/' || str[i + 1] == (byte)'\\'))
                {
                    res |= Check.DotSlashAttn;
                }
            }
            else if (c == (byte)'.')
            {
                if ((res & Check.DotSlashAttn) == 0 && i + 1 == end || str[i + 1] == (byte)'.' || str[i + 1] == (byte)'/'
                    || str[i + 1] == (byte)'\\' || str[i + 1] == (byte)'?' || str[i + 1] == (byte)'#')
                {
                    res |= Check.DotSlashAttn;
                }
            }
            else if (((c <= (byte)'"' && c != (byte)'!') || (c >= (byte)'[' && c <= (byte)'^') || c == (byte)'>'
                    || c == (byte)'<' || c == (byte)'`'))
            {
                needsEscaping = true;

                // The check above validates only that we have valid IRI characters, which is not enough to
                // conclude that we have a valid canonical IRI.
                // If we have an IRI with Flags.HasUnicode, we need to set Check.NotIriCanonical so that the
                // path, query, and fragment will be validated.
                if ((flags & Flags.HasUnicode) != 0)
                {
                    res |= Check.NotIriCanonical;
                }
            }
            else if (c >= (byte)'{' && c <= (byte)'}') // includes '{', '|', '}'
            {
                needsEscaping = true;
            }
            else if (c == (byte)'%')
            {
                foundEscaping = true;

                // try unescape a byte hex escaping
                if (i + 2 < end)
                {
                    int unescaped = Utf8UriHelper.DecodeHexChars(str[i + 1], str[i + 2]);
                    if (unescaped != c_DummyChar)
                    {
                        byte unescapedByte = (byte)unescaped;
                        if (unescapedByte == (byte)'.' || unescapedByte == (byte)'/' || unescapedByte == (byte)'\\')
                        {
                            res |= Check.DotSlashEscaped;
                        }

                        i += 3; // Skip the '%' and two hex digits
                        continue;
                    }
                }

                // otherwise we follow to non escaped case
                needsEscaping = true;
            }

            i += bytesConsumed;
        }

        if (foundEscaping)
        {
            if (!needsEscaping)
            {
                res |= Check.EscapedCanonical;
            }
        }
        else
        {
            if (!needsEscaping)
            {
                res |= Check.EscapedCanonical;
            }
            else
            {
                res |= Check.DisplayCanonical;
            }
        }

        idx = i;
        return res;
    }

    // Used by relative reference validation
    private static unsafe Check CheckCanonical(byte* str, ref int idx, int end, byte delim, bool iriParsing, bool mayHaveQuery, bool mayHaveFragment)
    {
        Check res = Check.None;
        bool needsEscaping = false;
        bool foundEscaping = false;

        byte c;
        int i = idx;
        for (; i < end;)
        {
            int bytesConsumed = 1;
            c = str[i];

            // Fast path for common ASCII characters that don't need special handling
            if ((c >= (byte)'A' && c <= (byte)'Z') || (c >= (byte)'a' && c <= (byte)'z') || (c >= (byte)'0' && c <= (byte)'9'))
            {
                // alphanumeric characters are always safe
                i++;
                continue;
            }

            // Control chars usually should be escaped in any case
            if (c <= (byte)'\x1F' || (c >= (byte)'\x7F' && c <= (byte)'\x9F'))
            {
                needsEscaping = true;
                foundEscaping = true;
                res |= Check.ReservedFound;
            }
            else if (c > '~')
            {
                if (iriParsing)
                {
                    res |= Check.FoundNonAscii;
                    Rune.DecodeFromUtf8(new ReadOnlySpan<byte>(str + i, end - i), out Rune rune, out bytesConsumed);

                    if (!Utf8IriHelper.CheckIriUnicodeRange((uint)rune.Value, true))
                    {
                        res |= Check.NotIriCanonical;
                    }
                }

                needsEscaping = true;
            }
            else if (c == delim)
            {
                break;
            }
            else if (delim == (byte)'?' && c == (byte)'#' && mayHaveFragment)
            {
                // this is a special case when deciding on Query/Fragment
                break;
            }
            else if (c == (byte)'?')
            {
                if (!mayHaveQuery && delim != c_EOL)
                {
                    // If found as reserved this char is not suitable for safe unescaped display
                    // Will need to escape it when both escaping and unescaping the string
                    res |= Check.ReservedFound;
                    foundEscaping = true;
                    needsEscaping = true;
                }
            }
            else if (c == (byte)'#')
            {
                needsEscaping = true;
                if (!mayHaveFragment)
                {
                    // If found as reserved this char is not suitable for safe unescaped display
                    // Will need to escape it when both escaping and unescaping the string
                    res |= Check.ReservedFound;
                    foundEscaping = true;
                }
            }
            else if (c == (byte)'/' || c == (byte)'\\')
            {
                if ((res & Check.BackslashInPath) == 0 && c == (byte)'\\')
                {
                    res |= Check.BackslashInPath;
                }

                if ((res & Check.DotSlashAttn) == 0 && i + 1 != end && (str[i + 1] == (byte)'/' || str[i + 1] == (byte)'\\'))
                {
                    res |= Check.DotSlashAttn;
                }
            }
            else if (c == (byte)'.')
            {
                if ((res & Check.DotSlashAttn) == 0 && i + 1 == end || str[i + 1] == (byte)'.' || str[i + 1] == (byte)'/'
                    || str[i + 1] == (byte)'\\' || str[i + 1] == (byte)'?' || str[i + 1] == (byte)'#')
                {
                    res |= Check.DotSlashAttn;
                }
            }
            else if (((c <= (byte)'"' && c != (byte)'!') || (c >= (byte)'[' && c <= (byte)'^') || c == (byte)'>'
                    || c == (byte)'<' || c == (byte)'`'))
            {
                needsEscaping = true;

                // The check above validates only that we have valid IRI characters, which is not enough to
                // conclude that we have a valid canonical IRI.
                // If we have an IRI with Flags.HasUnicode, we need to set Check.NotIriCanonical so that the
                // path, query, and fragment will be validated.
                if (iriParsing)
                {
                    res |= Check.NotIriCanonical;
                }
            }
            else if (c >= (byte)'{' && c <= (byte)'}') // includes '{', '|', '}'
            {
                needsEscaping = true;
            }
            else if (c == (byte)'%')
            {
                if (!foundEscaping) foundEscaping = true;

                // try unescape a byte hex escaping
                if (i + 2 < end)
                {
                    int unescaped = Utf8UriHelper.DecodeHexChars(str[i + 1], str[i + 2]);
                    if (unescaped != c_DummyChar)
                    {
                        byte unescapedByte = (byte)unescaped;
                        if (unescapedByte == (byte)'.' || unescapedByte == (byte)'/' || unescapedByte == (byte)'\\')
                        {
                            res |= Check.DotSlashEscaped;
                        }

                        i += 3; // Skip the '%' and two hex digits
                        continue;
                    }
                }

                // otherwise we follow to non escaped case
                needsEscaping = true;
            }

            i += bytesConsumed;
        }

        if (foundEscaping)
        {
            if (!needsEscaping)
            {
                res |= Check.EscapedCanonical;
            }
        }
        else
        {
            if (!needsEscaping)
            {
                res |= Check.EscapedCanonical;
            }
            else
            {
                res |= Check.DisplayCanonical | Check.FoundNonAscii;
            }
        }

        idx = i;
        return res;
    }

    private static void GetLengthWithoutTrailingSpaces(ReadOnlySpan<byte> uriString, ref int length, int idx)
    {
        // Early exit if no trimming needed
        if (length <= idx)
        {
            return;
        }

        // Check if last character is whitespace before entering loop
        if (!Utf8UriHelper.IsLWS(uriString[length - 1]))
        {
            return;
        }

        // to avoid dereferencing ref length parameter for every update
        int local = length - 1;
        while (local > idx && Utf8UriHelper.IsLWS(uriString[local]))
        {
            --local;
        }

        length = local + 1; // Adjust for the fact we decremented past the last non-whitespace
    }

#if NET

    private static readonly SearchValues<byte> s_asciiOtherThanPercent = SearchValues.Create([
        0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x000F,
        0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017, 0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, /* % */ 0x0026, 0x0027, 0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047, 0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057, 0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067, 0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077, 0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x007F,
        ]);

#else
    private static ReadOnlySpan<byte> s_asciiOtherThanPercent => [
        0x0000, 0x0001, 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0007, 0x0008, 0x0009, 0x000A, 0x000B, 0x000C, 0x000D, 0x000E, 0x000F,
        0x0010, 0x0011, 0x0012, 0x0013, 0x0014, 0x0015, 0x0016, 0x0017, 0x0018, 0x0019, 0x001A, 0x001B, 0x001C, 0x001D, 0x001E, 0x001F,
        0x0020, 0x0021, 0x0022, 0x0023, 0x0024, /* % */ 0x0026, 0x0027, 0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
        0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
        0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047, 0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
        0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057, 0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
        0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067, 0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
        0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077, 0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x007F,
        ];
#endif

    /// <summary>
    /// Unescapes entire string and checks if it has unicode chars.Also checks for sequences that are 3986 Unreserved characters as these should be un-escaped
    /// </summary>
    private static bool CheckForUnicodeOrEscapedUnreserved(ReadOnlySpan<byte> data, out Flags flags)
    {
        flags = default;
        int i = IndexOfAnyExcept(data, s_asciiOtherThanPercent);
        if (i >= 0)
        {
            int length = data.Length;
            for (; i < length; i++)
            {
                byte b = data[i];
                if (b == (byte)'%')
                {
                    // Optimized bounds check: ensure we have at least 2 more bytes
                    if (i + 2 < length)
                    {
                        char value = Utf8UriHelper.DecodeHexChars(data[i + 1], data[i + 2]);

                        bool isAscii = IsAscii(value);
                        if (!isAscii || Contains(value, Utf8UriHelper.Unreserved))
                        {
                            flags |= Flags.HasUnicode;
                            return true;
                        }

                        i += 2; // Skip the two hex chars we just processed
                    }

                    // If we don't have enough bytes, continue to next iteration
                }
                else if (b > 0x7F)
                {
                    flags |= Flags.HasUnicode | Flags.HasUnescapedUnicode;
                    return true;
                }
            }
        }

        return false;
    }

    // This method is called first to figure out the scheme or a simple file path
    // Is called only at the .ctor time
    private static Utf8UriParsingError ParseScheme(ReadOnlySpan<byte> uriString, ref Flags flags, ref Utf8UriParser? syntax)
    {
        int length = uriString.Length;
        if (length == 0)
            return Utf8UriParsingError.EmptyUriString;

        if (length >= c_MaxUriBufferSize)
            return Utf8UriParsingError.SizeLimit;

        // Fast path for valid http(s) schemes with no leading whitespace that are expected to be very common.
        if (StartsWithAsciiOrdinalIgnoreCase(uriString, "https:"u8))
        {
            syntax = Utf8UriParser.HttpsUri;
            flags |= Flags.UserNotCanonical | Flags.HostNotCanonical;
        }
        else if (StartsWithAsciiOrdinalIgnoreCase(uriString, "http:"u8))
        {
            syntax = Utf8UriParser.HttpUri;
            flags |= Flags.SchemeNotCanonical | Flags.HostNotCanonical;
        }
        else
        {
            // STEP1: parse scheme, lookup this Uri Syntax or create one using UnknownV1SyntaxFlags uri syntax template
            Utf8UriParsingError err = Utf8UriParsingError.None;
            int idx = ParseSchemeCheckImplicitFile(uriString, ref err, ref flags, ref syntax);
            Debug.Assert((err is Utf8UriParsingError.None) == (syntax is not null));

            if (err != Utf8UriParsingError.None)
                return err;

            flags |= (Flags)idx;
        }

        return Utf8UriParsingError.None;
    }

    // verifies the syntax of the scheme part
    // Checks on implicit File: scheme due to simple Dos/Unc path passed
    // returns the start of the next component  position
    private static int ParseSchemeCheckImplicitFile(ReadOnlySpan<byte> uriString, ref Utf8UriParsingError err, ref Flags flags, ref Utf8UriParser? syntax)
    {
        Debug.Assert(err == Utf8UriParsingError.None);

        int i = 0;
        int length = uriString.Length;

        // Optimized whitespace skipping - first check if we even have whitespace
        while ((uint)i < (uint)length && Utf8UriHelper.IsLWS(uriString[i]))
        {
            i++;
        }

#if NET
        // Sadly, we don't support UNIX on .NET Framework (sorry Mono)
        // Unix: Unix path?
        // A path starting with 2 / or \ (including mixed) is treated as UNC and will be matched below
        if (!OperatingSystem.IsWindows() &&
            (uint)i < (uint)uriString.Length && uriString[i] == '/' &&
            ((uint)(i + 1) >= (uint)uriString.Length || uriString[i + 1] is not ((byte)'/' or (byte)'\\')))
        {
            flags |= (Flags.UnixPath | Flags.ImplicitFile | Flags.AuthorityFound);
            syntax = Utf8UriParser.UnixFileUri;
            return i;
        }
#endif

        // Find the colon.
        // Note that we don't support one-letter schemes that will be put into a DOS path bucket
        int colonOffset = uriString.Slice(i).IndexOf((byte)':');

        // Early bounds check: A string must have at least 3 characters and at least 1 before ':'
        // Combine checks to reduce branching
        if (colonOffset == 0 || (uint)(i + 2) >= (uint)length)
        {
            err = Utf8UriParsingError.BadFormat;
            return 0;
        }

        ////// Check for supported special cases like a DOS file path OR a UNC share path
        ////// NB: A string may not have ':' if this is a UNC path
        //// if (uriString[i + 1] is (byte)':' or (byte)'|')
        //// {
        //// // DOS-like path?
        //// if (IsAsciiLetter(uriString[i]))
        //// {
        //// if (uriString[i + 2] is (byte)'\\' or (byte)'/')
        //// {
        //// flags |= (Flags.DosPath | Flags.ImplicitFile | Flags.AuthorityFound);

        //// syntax = Utf8UriParser.FileUri;

        //// return i;

        //// }

        //// err = Utf8UriParsingError.MustRootedPath;

        //// return 0;

        //// }

        //// err = uriString[i + 1] == (byte)':' ? Utf8UriParsingError.BadScheme : Utf8UriParsingError.BadFormat;

        //// return 0;

        //// }

        //// else if (uriString[i] is (byte)'/' or (byte)'\\')
        //// {
        //// // UNC share?
        //// if (uriString[i + 1] is (byte)'\\' or (byte)'/')
        //// {
        //// flags |= (Flags.UncPath | Flags.ImplicitFile | Flags.AuthorityFound);

        //// syntax = Utf8UriParser.FileUri;

        //// i += 2;

        //// // V1.1 compat this will simply eat any slashes prepended to a UNC path
        //// while ((uint)i < (uint)uriString.Length && uriString[i] is (byte)'/' or (byte)'\\')
        //// {
        //// i++;

        //// }

        //// return i;

        //// }

        //// err = Utf8UriParsingError.BadFormat;

        //// return 0;

        //// }

        if (colonOffset < 0)
        {
            err = Utf8UriParsingError.BadFormat;
            return 0;
        }

        // This is a potentially valid scheme, but we have not identified it yet.
        // Check for illegal characters, canonicalize, and check the length.
        syntax = CheckSchemeSyntax(uriString.Slice(i, colonOffset), ref err);

        if (syntax is null)
        {
            return 0;
        }

        return i + colonOffset + 1;
    }

#if NET

    private static readonly SearchValues<byte> s_schemeChars =
        SearchValues.Create("+-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

#else
    private static ReadOnlySpan<byte> s_schemeChars => "+-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8;
#endif

    private static Utf8UriParser? CheckSchemeSyntax(ReadOnlySpan<byte> scheme, ref Utf8UriParsingError error)
    {
        Debug.Assert(error == Utf8UriParsingError.None);

        // Early validation for common error cases
        if (scheme.Length == 0)
        {
            error = Utf8UriParsingError.BadScheme;
            return null;
        }

        if (scheme.Length > c_MaxUriSchemeName)
        {
            error = Utf8UriParsingError.SchemeLimit;
            return null;
        }

        // First character must be ASCII letter - check early to avoid further processing
        byte firstChar = scheme[0];
        if (!IsAsciiLetter(firstChar))
        {
            error = Utf8UriParsingError.BadScheme;
            return null;
        }

        switch (scheme.Length)
        {
            case 2:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "ws"u8)) return Utf8UriParser.WsUri;
                break;

            case 3:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "wss"u8)) return Utf8UriParser.WssUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "ftp"u8)) return Utf8UriParser.FtpUri;
                break;

            case 4:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "http"u8)) return Utf8UriParser.HttpUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "file"u8)) return Utf8UriParser.FileUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "uuid"u8)) return Utf8UriParser.UuidUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "nntp"u8)) return Utf8UriParser.NntpUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "ldap"u8)) return Utf8UriParser.LdapUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "news"u8)) return Utf8UriParser.NewsUri;
                break;

            case 5:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "https"u8)) return Utf8UriParser.HttpsUri;
                break;

            case 6:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "mailto"u8)) return Utf8UriParser.MailToUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "gopher"u8)) return Utf8UriParser.GopherUri;
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "telnet"u8)) return Utf8UriParser.TelnetUri;
                break;

            case 7:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "net.tcp"u8)) return Utf8UriParser.NetTcpUri;
                break;

            case 8:
                if (EqualsAsciiOrdinalIgnoreCase(scheme, "net.pipe"u8)) return Utf8UriParser.NetPipeUri;
                break;
        }

        // scheme = alpha *(alpha | digit | '+' | '-' | '.')
        // We already checked length and first character, now check the rest
        if (ContainsAnyExcept(scheme, s_schemeChars))
        {
            error = Utf8UriParsingError.BadScheme;
            return null;
        }

        // Then look up the syntax in a string-based table. Note that this will allocate a string if we've had to fall
        // through to here
        return Utf8UriParser.FindOrFetchAsUnknownV1Syntax(Utf8UriHelper.AsciiSchemeToLowerInvariantString(scheme));
    }

    // This method tries to parse the minimal information needed to certify the validity
    // of a uri string
    // scheme:// userinfo@host:Port/Path?Query#Fragment
    // The method must be called only at the .ctor time
    // Returns Utf8UriParsingError.None if the Uri syntax is valid, an error otherwise
    private static unsafe Utf8UriParsingError PrivateParseMinimal(ReadOnlySpan<byte> uriString, ref Flags flags, ref Utf8UriParser syntax)
    {
        Debug.Assert(syntax != null);

        int idx = (int)(flags & Flags.IndexMask);
        int length = uriString.Length;

        // Means a custom Utf8UriParser did call "base" InitializeAndValidate()
        flags &= ~(Flags.IndexMask | Flags.UserDrivenParsing);

        // STEP2: Parse up to the port
        fixed (byte* pUriString = uriString)
        {
            // Cut trailing spaces in uriString
            if (length > idx && Utf8UriHelper.IsLWS(pUriString[length - 1]))
            {
                --length;
                while (length != idx && Utf8UriHelper.IsLWS(pUriString[--length]))
                    ;
                ++length;
            }

#if NET
            // Once again, we don't support netstandard on Linux. Sorry Mono.
            // Unix Path
            if (!OperatingSystem.IsWindows() && InFact(flags, Flags.UnixPath))
            {
                flags |= Flags.BasicHostType;
                flags |= (Flags)idx;
                return Utf8UriParsingError.None;
            }
#endif

            // Old Uri parser tries to figure out on a DosPath in all cases.
            // Hence http:// c:/ is treated as DosPath without the host while it should be a host "c", port 80
            // This block is compatible with Old Uri parser in terms it will look for the DosPath if the scheme
            // syntax allows both empty hostnames and DosPath
            if (syntax.IsAllSet(Utf8UriSyntaxFlags.AllowEmptyHost | Utf8UriSyntaxFlags.AllowDOSPath)
                && NotAny(flags, Flags.ImplicitFile) && (idx + 1 < length))
            {
                byte c;
                int i = idx;

                // V1 Compat: Allow _compression_ of > 3 slashes only for File scheme.
                // This will skip all slashes and if their number is 2+ it sets the AuthorityFound flag
                for (; i < length; ++i)
                {
                    if (!((c = pUriString[i]) == '\\' || c == '/'))
                        break;
                }

                if (syntax.InFact(Utf8UriSyntaxFlags.FileLikeUri) || i - idx <= 3)
                {
                    // if more than one slash after the scheme, the authority is present
                    if (i - idx >= 2)
                    {
                        flags |= Flags.AuthorityFound;
                    }

                    // DOS-like path?
                    if (i + 1 < length && ((c = pUriString[i + 1]) == ':' || c == '|') &&
                        IsAsciiLetter(pUriString[i]))
                    {
                        if (i + 2 >= length || ((c = pUriString[i + 2]) != '\\' && c != '/'))
                        {
                            // report an error but only for a file: scheme
                            if (syntax.InFact(Utf8UriSyntaxFlags.FileLikeUri))
                                return Utf8UriParsingError.MustRootedPath;
                        }
                        else
                        {
                            // This will set IsDosPath
                            flags |= Flags.DosPath;

                            if (syntax.InFact(Utf8UriSyntaxFlags.MustHaveAuthority))
                            {
                                // when DosPath found and Authority is required, set this flag even if Authority is empty
                                flags |= Flags.AuthorityFound;
                            }

                            if (i != idx && i - idx != 2)
                            {
                                // This will remember that DosPath is rooted
                                idx = i - 1;
                            }
                            else
                            {
                                idx = i;
                            }
                        }
                    }

                    // UNC share?
                    else if (syntax.InFact(Utf8UriSyntaxFlags.FileLikeUri) && (i - idx >= 2 && i - idx != 3 &&
                        i < length && pUriString[i] != '?' && pUriString[i] != '#'))
                    {
                        // V1.0 did not support file:/// , fixing it with minimal behavior change impact
                        // Only FILE scheme may have UNC Path flag set
                        flags |= Flags.UncPath;
                        idx = i;
                    }

#if NET
                    // More "sorry Mono" - we don't support running the netstandard build on Linux; it's .NET only.
                    else if (!OperatingSystem.IsWindows() && syntax.InFact(Utf8UriSyntaxFlags.FileLikeUri) && pUriString[i - 1] == '/' && i - idx == 3)
                    {
                        syntax = Utf8UriParser.UnixFileUri;
                        flags |= Flags.UnixPath | Flags.AuthorityFound;
                        idx += 2;
                    }
#endif
                }
            }

            // STEP 1.5 decide on the Authority component
            if ((flags & (Flags.UncPath | Flags.DosPath | Flags.UnixPath)) != 0)
            {
            }
            else if ((idx + 2) <= length)
            {
                byte first = pUriString[idx];
                byte second = pUriString[idx + 1];

                if (syntax.InFact(Utf8UriSyntaxFlags.MustHaveAuthority))
                {
                    // (V1.0 compatibility) This will allow http:\\ http:\/ http:/\
                    if ((first == '/' || first == '\\') && (second == '/' || second == '\\'))
                    {
                        flags |= Flags.AuthorityFound;
                        idx += 2;
                    }
                    else
                    {
                        return Utf8UriParsingError.BadAuthority;
                    }
                }
                else if (syntax.InFact(Utf8UriSyntaxFlags.OptionalAuthority) && (InFact(flags, Flags.AuthorityFound) ||
                    (first == '/' && second == '/')))
                {
                    flags |= Flags.AuthorityFound;
                    idx += 2;
                }

                // There is no Authority component, save the Path index
                // Ideally we would treat mailto like any other URI, but for historical reasons we have to separate out its host parsing.
                else if (syntax.NotAny(Utf8UriSyntaxFlags.MailToLikeUri))
                {
                    // By now we know the URI has no Authority, so if the URI must be normalized, initialize it without one.
                    if (InFact(flags, Flags.HasUnicode))
                    {
                        uriString = uriString.Slice(0, idx);
                    }

                    // Since there is no Authority, the path index is just the end of the scheme.
                    flags |= ((Flags)idx | Flags.UnknownHostType);
                    return Utf8UriParsingError.None;
                }
            }
            else if (syntax.InFact(Utf8UriSyntaxFlags.MustHaveAuthority))
            {
                return Utf8UriParsingError.BadAuthority;
            }

            // There is no Authority component, save the Path index
            // Ideally we would treat mailto like any other URI, but for historical reasons we have to separate out its host parsing.
            else if (syntax.NotAny(Utf8UriSyntaxFlags.MailToLikeUri))
            {
                // By now we know the URI has no Authority, so if the URI must be normalized, initialize it without one.
                if (InFact(flags, Flags.HasUnicode))
                {
                    uriString = uriString.Slice(0, idx);
                }

                // Since there is no Authority, the path index is just the end of the scheme.
                flags |= ((Flags)idx | Flags.UnknownHostType);
                return Utf8UriParsingError.None;
            }

            // vsmacros:// c:\path\file
            // Note that two slashes say there must be an Authority but instead the path goes
            // Fro V1 compat the next block allow this case but not for schemes like http
            if (InFact(flags, Flags.DosPath))
            {
                flags |= (((flags & Flags.AuthorityFound) != 0) ? Flags.BasicHostType : Flags.UnknownHostType);
                flags |= (Flags)idx;
                return Utf8UriParsingError.None;
            }

            // STEP 2: Check the syntax of authority expecting at least one character in it
            // Note here we do know that there is an authority in the string OR it's a DOS path
            // We may find a userInfo and the port when parsing an authority
            // Also we may find a registry based authority.
            // We must ensure that known schemes do use a server-based authority
            {
                Utf8UriParsingError err = Utf8UriParsingError.None;

                idx = CheckAuthorityHelper(pUriString, idx, length, ref err, ref flags, syntax); // REMOVED THE NEWHOST STRING MODIFICATION
                if (err != Utf8UriParsingError.None)
                    return err;

                if (idx < length)
                {
                    byte hostTerminator = pUriString[idx];

                    // This will disallow '\' as the host terminator for any scheme that is not implicitFile or cannot have a Dos Path
                    if (hostTerminator == '\\' && NotAny(flags, Flags.ImplicitFile) && syntax.NotAny(Utf8UriSyntaxFlags.AllowDOSPath))
                    {
                        return Utf8UriParsingError.BadAuthorityTerminator;
                    }

#if NET
                    // (Sorry Mono again, we still don't support you.)
                    // When the hostTerminator is '/' on Unix, use the UnixFile syntax (preserve backslashes)
                    else if (!OperatingSystem.IsWindows() && hostTerminator == '/' && NotAny(flags, Flags.ImplicitFile) && InFact(flags, Flags.UncPath) && syntax == Utf8UriParser.FileUri)
                    {
                        syntax = Utf8UriParser.UnixFileUri;
                    }
#endif
                }
            }

            // The Path (or Port) parsing index is reloaded on demand in CreateUriInfo when accessing a Uri property
            flags |= (Flags)idx;

            // The rest of the string will be parsed on demand
            // The Host/Authority is all checked, the type is known but the host value string
            // is not created/canonicalized at this point.
        }

        return Utf8UriParsingError.None;
    }

    // Checks the syntax of an authority component. It may also get a userInfo if present
    // Returns an error if no/malformed authority found
    // Does not NOT touch info
    // Returns position of the Path component
    // Must be called in the ctor only
    private static unsafe int CheckAuthorityHelper(byte* pString, int idx, int length,
        ref Utf8UriParsingError err, ref Flags flags, Utf8UriParser syntax)
    {
        int end = length;
        byte ch;
        int startInput = idx;
        int start = idx;
        bool hasUnicode = ((flags & Flags.HasUnicode) != 0);
        Utf8UriSyntaxFlags syntaxFlags = syntax.Flags;

        Debug.Assert((flags & Flags.HasUserInfo) == 0 && (flags & Flags.HostTypeMask) == 0);

        // Special case is an empty authority
        if (idx == length || ((ch = pString[idx]) == (byte)'/' || (ch == (byte)'\\' && IsFile(syntax)) || ch == (byte)'#' || ch == (byte)'?'))
        {
            if (syntax.InFact(Utf8UriSyntaxFlags.AllowEmptyHost))
            {
                flags &= ~Flags.UncPath;    // UNC cannot have an empty hostname
                if (InFact(flags, Flags.ImplicitFile))
                    err = Utf8UriParsingError.BadHostName;
                else
                    flags |= Flags.BasicHostType;
            }
            else
                err = Utf8UriParsingError.BadHostName;

            return idx;
        }

        // Attempt to parse user info first
        if ((syntaxFlags & Utf8UriSyntaxFlags.MayHaveUserInfo) != 0)
        {
            for (; start < end; ++start)
            {
                if (start == end - 1 || pString[start] == (byte)'?' || pString[start] == (byte)'#' || pString[start] == (byte)'\\' ||
                    pString[start] == (byte)'/')
                {
                    start = idx;
                    break;
                }
                else if (pString[start] == (byte)'@')
                {
                    flags |= Flags.HasUserInfo;
                    ++start;
                    ch = pString[start];
                    break;
                }
            }
        }

        if (ch == (byte)'[' && syntax.InFact(Utf8UriSyntaxFlags.AllowIPv6Host) &&
            IPv6AddressHelper.IsValid(pString, start, end, true))
        {
            flags |= Flags.IPv6HostType;
        }
        else if (IsAsciiDigit(ch) && syntax.InFact(Utf8UriSyntaxFlags.AllowIPv4Host) &&
            IPv4AddressHelper.IsValid(pString, start, ref end, false, NotAny(flags, Flags.ImplicitFile), false))
        {
            flags |= Flags.IPv4HostType;
        }
        else if (((syntaxFlags & Utf8UriSyntaxFlags.AllowDnsHost) != 0) && !IriParsing(syntax) &&
            Utf8UriDomainNameHelper.IsValid(new ReadOnlySpan<byte>(pString + start, end - start), iri: false, NotAny(flags, Flags.ImplicitFile), out int domainNameLength))
        {
            end = start + domainNameLength;

            // comes here if there are only ascii chars in host with original parsing and no Iri
            flags |= Flags.DnsHostType;

            // Canonical DNS hostnames don't contain uppercase letters
            if (!ContainsAnyInRange(new ReadOnlySpan<byte>(pString + start, domainNameLength), (byte)'A', (byte)'Z'))
            {
                flags |= Flags.CanonicalDnsHost;
            }
        }
        else if (((syntaxFlags & Utf8UriSyntaxFlags.AllowDnsHost) != 0) &&
            (hasUnicode || syntax.InFact(Utf8UriSyntaxFlags.AllowIdn)) &&
            Utf8UriDomainNameHelper.IsValid(new ReadOnlySpan<byte>(pString + start, end - start), iri: true, NotAny(flags, Flags.ImplicitFile), out domainNameLength))
        {
            end = start + domainNameLength;

            CheckAuthorityHelperHandleDnsIri(pString, start, end, hasUnicode,
                ref flags, ref err);
        }
        else if ((syntaxFlags & Utf8UriSyntaxFlags.AllowUncHost) != 0)
        {
            // This must remain as the last check before BasicHost type
            if (Utf8UriUncNameHelper.IsValid(pString, start, ref end, NotAny(flags, Flags.ImplicitFile)))
            {
                if (end - start <= Utf8UriUncNameHelper.MaximumInternetNameLength)
                {
                    flags |= Flags.UncHostType;
                }
            }
        }

        // The deal here is that we won't allow '\' host terminator except for the File scheme
        // If we see '\' we try to make it a part of a Basic host
        if (end < length && pString[end] == (byte)'\\' && (flags & Flags.HostTypeMask) != Flags.HostNotParsed
            && !IsFile(syntax))
        {
            flags &= ~Flags.HostTypeMask;
        }

        // Here we have checked the syntax up to the end of host
        // The only thing that can cause an exception is the port value
        // Spend some (duplicated) cycles on that.
        else if (end < length && pString[end] == (byte)':')
        {
            if (syntax.InFact(Utf8UriSyntaxFlags.MayHavePort))
            {
                int port = 0;
                int startPort = end;
                for (idx = end + 1; idx < length; ++idx)
                {
                    int val = pString[idx] - (byte)'0';
                    if ((uint)val <= ((byte)'9' - (byte)'0'))
                    {
                        if ((port = (port * 10 + val)) > 0xFFFF)
                            break;
                    }
                    else if (val == ((byte)'/' - (byte)'0') || val == ((byte)'?' - (byte)'0') || val == ((byte)'#' - (byte)'0'))
                    {
                        break;
                    }
                    else
                    {
                        // The second check is to keep compatibility with V1 until the Utf8UriParser is registered
                        err = Utf8UriParsingError.BadPort;
                        return idx;
                    }
                }

                // check on 0-ffff range
                if (port > 0xFFFF)
                {
                    if (syntax.InFact(Utf8UriSyntaxFlags.AllowAnyOtherHost))
                    {
                        flags &= ~Flags.HostTypeMask;
                    }
                    else
                    {
                        err = Utf8UriParsingError.BadPort;
                        return idx;
                    }
                }
            }
            else
            {
                flags &= ~Flags.HostTypeMask;
            }
        }

        // check on whether nothing has worked out
        if ((flags & Flags.HostTypeMask) == Flags.HostNotParsed)
        {
            // No user info for a Basic hostname
            flags &= ~Flags.HasUserInfo;

            // Some schemes do not allow HostType = Basic (plus V1 almost never understands this issue)
            if (syntax.InFact(Utf8UriSyntaxFlags.AllowAnyOtherHost))
            {
                flags |= Flags.BasicHostType;
                for (end = idx; end < length; ++end)
                {
                    if (pString[end] == (byte)'/' || (pString[end] == (byte)'?' || pString[end] == (byte)'#'))
                    {
                        break;
                    }
                }
            }
            else
            {
                // ATTN V1 compat: V1 supports hostnames like ".." and ".", and so we do but only for unknown schemes.
                if (syntax.InFact(Utf8UriSyntaxFlags.MustHaveAuthority) ||
                         (syntax.InFact(Utf8UriSyntaxFlags.MailToLikeUri)))
                {
                    err = Utf8UriParsingError.BadHostName;
                    flags |= Flags.UnknownHostType;
                    return idx;
                }
            }
        }

        return end;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void CheckAuthorityHelperHandleDnsIri(byte* pString, int start, int end,
       bool hasUnicode, ref Flags flags,
       ref Utf8UriParsingError err)
    {
        // comes here only if host has unicode chars and iri is on or idn is allowed
        flags |= Flags.DnsHostType;

        // Normalization not required
    }

#if NET

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsAnyInRange(ReadOnlySpan<byte> source, byte start, byte end) => source.ContainsAnyInRange(start, end);

#else
    internal static bool ContainsAnyInRange(ReadOnlySpan<byte> source, byte start, byte end)
    {
        for (int i = 0; i < source.Length; ++i)
        {
            if (source[i] >= start && source[i] <= end)
            {
                return true;
            }
        }

        return false;
    }
#endif

#if NET

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool ContainsAnyExcept(ReadOnlySpan<byte> container, SearchValues<byte> exceptions) => container.ContainsAnyExcept(exceptions);

#else
    private static bool ContainsAnyExcept(ReadOnlySpan<byte> scheme, ReadOnlySpan<byte> schemaChars)
    {
        for (int i = 0; i < scheme.Length; i++)
        {
            if (schemaChars.IndexOf(scheme[i]) < 0)
            {
                return true;
            }
        }

        return false;
    }
#endif

#if NET

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int IndexOfAnyExcept(ReadOnlySpan<byte> value, SearchValues<byte> exceptions) => value.IndexOfAnyExcept(exceptions);

#else
    internal static int IndexOfAnyExcept(ReadOnlySpan<byte> value, ReadOnlySpan<byte> exceptions)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (exceptions.IndexOf(value[i]) < 0)
            {
                return i;
            }
        }

        return -1;
    }
#endif

#if NET

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Contains(char value, SearchValues<char> search) => search.Contains(value);

#else
    private static bool Contains(char value, ReadOnlySpan<char> search)
    {
        return search.IndexOf(value) >= 0;
    }
#endif

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiLetter(byte v)
    {
#if NET
        return char.IsAsciiLetter((char)v);
#else
        return v >= (byte)'a' && v <= (byte)'z' || v >= (byte)'A' && v <= (byte)'Z';
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiLetterOrDigit(byte v)
    {
#if NET
        return char.IsAsciiLetterOrDigit((char)v);
#else
        return v >= (byte)'a' && v <= (byte)'z' || v >= (byte)'A' && v <= (byte)'Z' || v >= (byte)'0' && v <= (byte)'9';
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAsciiDigit(byte v)
    {
#if NET
        return char.IsAsciiDigit((char)v);
#else
        return v >= (byte)'0' && v <= (byte)'9';
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAscii(char v)
    {
#if NET
        return char.IsAscii(v);
#else
        return (uint)v <= 0x007f;
#endif
    }

    private static bool StartsWithAsciiOrdinalIgnoreCase(ReadOnlySpan<byte> uriString, ReadOnlySpan<byte> asciiSpan)
    {
        if (uriString.Length < asciiSpan.Length)
        {
            return false;
        }

#if NET
        return Ascii.EqualsIgnoreCase(uriString.Slice(0, asciiSpan.Length), asciiSpan);
#else
        for (int i = 0; i < asciiSpan.Length; i++)
        {
            if (uriString[i] != asciiSpan[i] && char.ToLowerInvariant((char)uriString[i]) != char.ToLowerInvariant((char)asciiSpan[i]))
            {
                return false;
            }
        }

        return true;
#endif
    }

    private static bool EqualsAsciiOrdinalIgnoreCase(ReadOnlySpan<byte> uriString, ReadOnlySpan<byte> asciiSpan)
    {
        if (uriString.Length != asciiSpan.Length)
        {
            return false;
        }

#if NET
        return Ascii.EqualsIgnoreCase(uriString, asciiSpan);
#else
        for (int i = 0; i < asciiSpan.Length; i++)
        {
            if (uriString[i] != asciiSpan[i] && char.ToLowerInvariant((char)uriString[i]) != char.ToLowerInvariant((char)asciiSpan[i]))
            {
                return false;
            }
        }

        return true;
#endif
    }
}