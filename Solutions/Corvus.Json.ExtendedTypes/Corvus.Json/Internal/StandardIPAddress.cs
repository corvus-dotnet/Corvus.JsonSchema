// <copyright file="StandardIPAddress.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;

namespace Corvus.Json.Internal;

/// <summary>
/// Standard IP Address formatting.
/// </summary>
public static class StandardIPAddress
{
    /// <summary>
    /// Parse an IP address.
    /// </summary>
    /// <param name="span">The span to parse.</param>
    /// <param name="state">The state object (expects null).</param>
    /// <param name="value">The parsed IP address.</param>
    /// <returns><see langword="true"/> if the address was parsed successfully.</returns>
#if NET8_0_OR_GREATER
    public static bool IPAddressParser(ReadOnlySpan<char> span, in object? state, out IPAddress value)
    {
        if (IPAddress.TryParse(span, out IPAddress? address))
#else
    public static bool IPAddressParser(string span, in object? state, out IPAddress value)
    {
        if (IPAddress.TryParse(span, out IPAddress? address))
#endif
        {
            value = address;
            return true;
        }

        value = IPAddress.None;
        return false;
    }
}