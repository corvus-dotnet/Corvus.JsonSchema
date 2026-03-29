// <copyright file="HashCode.NetStandard.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Provides polyfill methods for string hash code operations and hash code utilities.
/// </summary>
internal static class StringHashCodePolyfills
{
    extension(string)
    {
        /// <summary>
        /// Computes the hash code for the specified character span.
        /// </summary>
        /// <param name="value">The character span to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(ReadOnlySpan<char> value)
        {
            ulong seed = Marvin.DefaultSeed;

            // Multiplication below will not overflow since going from positive Int32 to UInt32.
            return Marvin.ComputeHash32(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(value)), (uint)value.Length * 2 /* in bytes, not chars */, (uint)seed, (uint)(seed >> 32));
        }
    }

    /// <summary>
    /// Adds bytes to the hash code calculation from the specified byte span.
    /// </summary>
    /// <param name="hashCode">The hash code instance to add bytes to.</param>
    /// <param name="value">The byte span to add to the hash code.</param>
    public static void AddBytes(this HashCode hashCode, ReadOnlySpan<byte> value)
    {
        // Add them in blocks of ulongs
        int longs = value.Length / sizeof(ulong);
        int initialLength = longs * sizeof(ulong);
        ReadOnlySpan<ulong> uLongBuffer = MemoryMarshal.Cast<byte, ulong>(value.Slice(0, initialLength));
        for (int i = 0; i < longs; i++)
        {
            hashCode.Add(uLongBuffer[i]);
        }

        // Then add the left-over bytes as bytes
        _ = value.Length % sizeof(ulong);
        for (int i = initialLength; i < value.Length; i++)
        {
            hashCode.Add(value[i]);
        }
    }
}