// <copyright file="JsonIpV6.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON ipv6.
/// </summary>
public readonly partial struct JsonIpV6
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonIpV6"/> struct.
    /// </summary>
    /// <param name="value">The IP address.</param>
    public JsonIpV6(IPAddress value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = value.ToString();
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to IPAddress.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not an IpV6.</exception>
    public static implicit operator IPAddress(JsonIpV6 value)
    {
        return value.GetIPAddress();
    }

    /// <summary>
    /// Implicit conversion from IPaddress.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonIpV6(IPAddress value)
    {
        return new JsonIpV6(value);
    }

    /// <summary>
    /// Get the value as <see cref="IPAddress"/>.
    /// </summary>
    /// <returns>The IPAddress.</returns>
    /// <exception cref="InvalidOperationException">The value was not an IpV6.</exception>
    public IPAddress GetIPAddress()
    {
        if (this.TryGetIPAddress(out IPAddress? result))
        {
            return result;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Gets the string as <see cref="IPAddress"/>.
    /// </summary>
    /// <param name="result">The value as IPAddress.</param>
    /// <returns><c>True</c> if the value could be retrieved.</returns>
    public bool TryGetIPAddress([NotNullWhen(true)] out IPAddress? result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return IPAddressParser(this.stringBacking.AsSpan(), null, out result);
        }
        else if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return this.jsonElementBacking.TryGetValue(IPAddressParser, (object?)null, out result);
        }

        result = IPAddress.None;
        return false;
    }

    /// <summary>
    /// Parse an IP address.
    /// </summary>
    /// <param name="span">The span to parse.</param>
    /// <param name="state">The state object (expects null).</param>
    /// <param name="value">The parsed IP address.</param>
    /// <returns><see langword="true"/> if the address was parsed successfully.</returns>
    internal static bool IPAddressParser(ReadOnlySpan<char> span, in object? state, out IPAddress value)
    {
        if (IPAddress.TryParse(span, out IPAddress? address))
        {
            value = address;
            return true;
        }

        value = IPAddress.None;
        return false;
    }
}