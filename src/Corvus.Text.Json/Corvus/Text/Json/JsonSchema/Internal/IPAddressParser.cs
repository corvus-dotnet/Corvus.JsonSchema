// <copyright file="IPAddressParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides utilities for parsing and validating IP addresses.
/// </summary>
internal static class IPAddressParser
{
    /// <summary>
    /// The maximum length of an IPv4 address string representation.
    /// </summary>
    internal const int MaxIPv4StringLength = 15; // 4 numbers separated by 3 periods, with up to 3 digits per number

    /// <summary>
    /// The maximum length of an IPv6 address string representation.
    /// </summary>
    internal const int MaxIPv6StringLength = 65;

    /// <summary>
    /// Determines whether the specified span represents a valid IPv4 address.
    /// </summary>
    /// <param name="ipSpan">The span containing the IPv4 address bytes to validate.</param>
    /// <param name="requireCanonical">If true, requires the IPv4 address to be in canonical form.</param>
    /// <returns>true if the span represents a valid IPv4 address; otherwise, false.</returns>
    public static unsafe bool IsValidIPV4(ReadOnlySpan<byte> ipSpan, bool requireCanonical = true)
    {
        fixed (byte* ipStringPtr = &MemoryMarshal.GetReference(ipSpan))
        {
            if (ipSpan.IndexOf((byte)':') >= 0)
            {
                return false;
            }
            else
            {
                int end = ipSpan.Length;
                long address = IPv4AddressHelper.ParseNonCanonical(ipStringPtr, 0, ref end, notImplicitFile: true, requireCanonical);
                return address != IPv4AddressHelper.Invalid && end == ipSpan.Length;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified span represents a valid IPv6 address.
    /// </summary>
    /// <param name="ipSpan">The span containing the IPv6 address bytes to validate.</param>
    /// <param name="disallowScope">If true, scope identifiers are not allowed in the IPv6 address.</param>
    /// <returns>true if the span represents a valid IPv6 address; otherwise, false.</returns>
    public static unsafe bool IsValidIPV6(ReadOnlySpan<byte> ipSpan, bool disallowScope = true)
    {
        fixed (byte* ipStringPtr = &MemoryMarshal.GetReference(ipSpan))
        {
            if (ipSpan.IndexOf((byte)':') >= 0)
            {
                return IPv6AddressHelper.IsValidStrict(ipStringPtr, 0, ipSpan.Length, disallowScope);
            }

            return false;
        }
    }
}