// <copyright file="JsonElementHelpers.BigNumberFormatting.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides the <see cref="TryFormatNumber(ReadOnlySpan{byte}, Span{char}, out int, ReadOnlySpan{char}, IFormatProvider?, bool, ReadOnlySpan{byte}, ReadOnlySpan{byte}, int)"/>
/// overloads required by <c>BigNumber.OptimizedFormatting</c> when building with a custom format specifier.
/// </summary>
/// <remarks>
/// The code generator only exercises the default (empty) format path in <c>BigNumber.TryFormat</c>,
/// which goes through <c>TryFormatRawUtf8</c> and never reaches these methods. They exist solely
/// to satisfy the compiler when linking <c>BigNumber.OptimizedFormatting.cs</c>.
/// </remarks>
internal static partial class JsonElementHelpers
{
    /// <summary>
    /// Formats a pre-parsed number into a UTF-16 destination span.
    /// </summary>
    internal static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider, bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        throw new NotSupportedException("Custom format specifiers are not supported in the source generator context.");
    }

    /// <summary>
    /// Formats a pre-parsed number into a UTF-8 destination span.
    /// </summary>
    internal static bool TryFormatNumber(ReadOnlySpan<byte> span, Span<byte> destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider, bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        throw new NotSupportedException("Custom format specifiers are not supported in the source generator context.");
    }
}