// <copyright file="JsonElementTensorHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Helper methods for JSON element for conversion to tensors.
/// </summary>
public static partial class JsonElementTensorHelpers
{
    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<long> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<long> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<ulong> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<ulong> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<int> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<int> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<uint> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<uint> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<short> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<short> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<ushort> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<ushort> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<sbyte> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<sbyte> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<byte> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<byte> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<double> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<double> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<float> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<float> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<decimal> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<decimal> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

#if NET

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<Int128> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<Int128> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<UInt128> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<UInt128> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

    /// <summary>
    /// Tries to copy the array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool TryCopyTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<Half> array, out int written)
    {
        return TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array, out written);
    }

    /// <summary>
    /// Tries to copy the higher-rank array data from the instance to the given array.
    /// </summary>
    /// <param name="parentDocument">The parent document of the JSON array instance.</param>
    /// <param name="parentDocumentIndex">The parent document index of the JSON array instance.</param>
    /// <param name="array">The target array.</param>
    /// <param name="rank">The rank of the array (e.g. [,] == rank 2, [,,,] == rank 3 etc.)</param>
    /// <param name="written">The number of values written.</param>
    /// <returns><see langword="true"/> if it was able to copy the values to the target array, otherwise false.</returns>
    /// <exception cref="InvalidOperationException">The element was not a JSON array of the target type and rank.</exception>
    /// <exception cref="FormatException">An item in the array was not of the target numeric format.</exception>
    [CLSCompliant(false)]
    public static bool TryCopyArrayOfRankTo(IJsonDocument parentDocument, int parentDocumentIndex, Span<Half> array, int rank, out int written)
    {
        Debug.Assert(rank > 0);

        int localWritten = 0;
        if (!TryCopyArbitraryRankUnsafe(parentDocument, parentDocumentIndex, array, rank, ref localWritten))
        {
            written = 0;
            return false;
        }

        written = localWritten;
        return true;
    }

#endif

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<long> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<long> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out long value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Int64);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<ulong> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    written = 0;
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<ulong> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out ulong value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.UInt64);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<int> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<int> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out int value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Int32);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<uint> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<uint> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out uint value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.UInt32);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<short> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<short> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out short value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Int16);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<ushort> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<ushort> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out ushort value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.UInt16);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<sbyte> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<sbyte> array, out int written)
    {
        try
        {
            if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
            {
                written = 0;
                return false;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
            written = 0;
            while (enumerator.MoveNext())
            {
                if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out sbyte value))
                {
                    written = 0;

                    // Clear the array as it may contain sensitive data
                    array.Slice(0, written).Clear();
                    ThrowHelper.ThrowFormatException(NumericType.SByte);
                }

                array[written++] = value;
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            written = 0;

            // Clear the array as it may contain sensitive data
            array.Slice(0, written).Clear();
            throw;
        }
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<byte> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    // We have only written something if we got into the 1D case, so we can
                    // just return from here.
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            // Clear the portion of the array that we wrote it may contain sensitive data
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<byte> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out byte value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Byte);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<double> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<double> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out double value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Double);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<float> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<float> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out float value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Single);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<decimal> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<decimal> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out decimal value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Decimal);
            }

            array[written++] = value;
        }

        return true;
    }

#if NET

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<Int128> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<Int128> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out Int128 value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Int128);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<UInt128> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<UInt128> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out UInt128 value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.UInt128);
            }

            array[written++] = value;
        }

        return true;
    }

    private static bool TryCopyArbitraryRankUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<Half> array, int rank, ref int written)
    {
        int localWritten = written;
        try
        {
            if (rank == 1)
            {
                if (!TryCopy1DUnsafe(parentDocument, parentDocumentIndex, array.Slice(written), out int writtenLocal))
                {
                    array.Slice(0, written).Clear();
                    return false;
                }

                written += writtenLocal;
                return true;
            }

            ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);

            while (enumerator.MoveNext())
            {
                if (!TryCopyArbitraryRankUnsafe(parentDocument, enumerator.CurrentIndex, array, rank - 1, ref written))
                {
                    return false;
                }
            }

            return true;
        }
        catch (InvalidOperationException)
        {
            array.Slice(localWritten, written).Clear();
            throw;
        }
    }

    private static bool TryCopy1DUnsafe(IJsonDocument parentDocument, int parentDocumentIndex, Span<Half> array, out int written)
    {
        if (array.Length < parentDocument.GetArrayLength(parentDocumentIndex))
        {
            written = 0;
            return false;
        }

        ArrayEnumerator enumerator = new(parentDocument, parentDocumentIndex);
        written = 0;
        while (enumerator.MoveNext())
        {
            if (!parentDocument.TryGetValue(enumerator.CurrentIndex, out Half value))
            {
                written = 0;

                // Clear the array as it may contain sensitive data
                array.Slice(0, written).Clear();
                ThrowHelper.ThrowFormatException(NumericType.Int128);
            }

            array[written++] = value;
        }

        return true;
    }

#endif
}