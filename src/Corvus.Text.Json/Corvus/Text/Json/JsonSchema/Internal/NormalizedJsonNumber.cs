// <copyright file="NormalizedJsonNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Represents a normalized JSON number.
/// </summary>
public readonly struct NormalizedJsonNumber
{
    private readonly byte[] _integral;

    private readonly byte[] _fractional;

    public NormalizedJsonNumber(bool isNegative, byte[] integral, byte[] fractional, int exponent)
    {
        IsNegative = isNegative;
        _integral = integral;
        _fractional = fractional;
        Exponent = exponent;
    }

    /// <summary>
    /// Indicates whether the number is negative.
    /// </summary>
    public bool IsNegative { get; }

    /// <summary>
    /// The normalized integral part of the original JSON representation of the number.
    /// </summary>
    public ReadOnlySpan<byte> Integral => _integral;

    /// <summary>
    /// The normalized fractional part of the original JSON representation of the number.
    /// </summary>
    public ReadOnlySpan<byte> Fractional => _fractional;

    /// <summary>
    /// The exponent to apply after concatenating the integral and fractional parts.
    /// </summary>
    public int Exponent { get; }
}