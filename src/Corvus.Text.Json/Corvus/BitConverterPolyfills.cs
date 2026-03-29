// <copyright file="BitConverterPolyfills.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus;

/// <summary>
/// Provides polyfills for <see cref="System.BitConverter"/> methods for .NET Standard 2.0, enabling conversion between primitive types and byte arrays or spans.
/// </summary>
[CLSCompliant(false)]
public static class BitConverterPolyfills
{
    extension(BitConverter)
    {
        /// <summary>
        /// Converts a 32-bit signed integer (<see cref="int"/>) into a span of bytes.
        /// </summary>
        /// <param name="destination">The span to receive the bytes representing the converted value.</param>
        /// <param name="value">The 32-bit signed integer to convert.</param>
        /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryWriteBytes(Span<byte> destination, int value)
        {
            if (destination.Length < sizeof(int))
            {
                return false;
            }

            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
            return true;
        }

        /// <summary>
        /// Converts a 32-bit unsigned integer (<see cref="uint"/>) into a span of bytes.
        /// </summary>
        /// <param name="destination">The span to receive the bytes representing the converted value.</param>
        /// <param name="value">The 32-bit unsigned integer to convert.</param>
        /// <returns><see langword="true"/> if the conversion was successful; otherwise, <see langword="false"/>.</returns>
        public static bool TryWriteBytes(Span<byte> destination, uint value)
        {
            if (destination.Length < sizeof(uint))
            {
                return false;
            }

            Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
            return true;
        }
    }
}