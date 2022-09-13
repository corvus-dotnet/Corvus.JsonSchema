// <copyright file="JsonUuid.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON uuid.
/// </summary>
public readonly partial struct JsonUuid
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonUuid"/> struct.
    /// </summary>
    /// <param name="value">The Guid value.</param>
    public JsonUuid(Guid value)
    {
        this.jsonElementBacking = default;
        this.stringBacking = FormatGuid(value);
        this.backing = Backing.String;
    }

    /// <summary>
    /// Implicit conversion to Guid.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <exception cref="InvalidOperationException">The value was not a uuid.</exception>
    public static implicit operator Guid(JsonUuid value)
    {
        return value.GetGuid();
    }

    /// <summary>
    /// Implicit conversion from Guid.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonUuid(Guid value)
    {
        return new JsonUuid(value);
    }

    /// <summary>
    /// Gets the value as a Guid.
    /// </summary>
    /// <returns>The value as a Guid.</returns>
    /// <exception cref="InvalidOperationException">The value was not a uuid.</exception>
    public Guid GetGuid()
    {
        if (this.TryGetGuid(out Guid result))
        {
            return result;
        }

        return Guid.Empty;
    }

    /// <summary>
    /// Try to get the Guid value.
    /// </summary>
    /// <param name="result">The guid value.</param>
    /// <returns><c>True</c> if it was possible to get a guid value from the instance.</returns>
    public bool TryGetGuid(out Guid result)
    {
        if ((this.backing & Backing.String) != 0)
        {
            return GuidParser(this.stringBacking.AsSpan(), null, out result);
        }
        else if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
        {
            return this.jsonElementBacking.TryGetValue(GuidParser, (object?)null, out result);
        }

        result = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Parse an IP address.
    /// </summary>
    /// <param name="span">The span to parse.</param>
    /// <param name="state">The state object (expects null).</param>
    /// <param name="value">The parsed IP address.</param>
    /// <returns><see langword="true"/> if the address was parsed successfully.</returns>
    internal static bool GuidParser(ReadOnlySpan<char> span, in object? state, out Guid value)
    {
        if (Guid.TryParse(span, out Guid guid))
        {
            value = guid;
            return true;
        }

        value = Guid.Empty;
        return false;
    }

    private static string FormatGuid(Guid value)
    {
        return value.ToString("D");
    }
}