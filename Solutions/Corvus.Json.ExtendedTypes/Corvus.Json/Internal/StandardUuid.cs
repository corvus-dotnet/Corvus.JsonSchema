// <copyright file="StandardUuid.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Internal;

/// <summary>
/// Parsing for format uuid.
/// </summary>
public static  class StandardUuid
{
    /// <summary>
    /// Parse a uuid string.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="state">The state object (expects null).</param>
    /// <param name="value">The parsed IP address.</param>
    /// <returns><see langword="true"/> if the address was parsed successfully.</returns>
#if NET8_0_OR_GREATER
    public static bool GuidParser(ReadOnlySpan<char> text, in object? state, out Guid value)
    {
        if (Guid.TryParse(text, out Guid guid))
#else
    public static bool GuidParser(string? text, in object? state, out Guid value)
    {
        if (Guid.TryParse(text, out Guid guid))
#endif
        {
            value = guid;
            return true;
        }

        value = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Gormat a uuid.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The standard string format for the uuid.</returns>
    public static string FormatGuid(Guid value)
    {
        return value.ToString("D");
    }
}