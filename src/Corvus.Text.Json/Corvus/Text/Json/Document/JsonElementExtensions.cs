// <copyright file="JsonElementExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Extension methods for <see cref="IJsonElement"/>.
/// </summary>
[CLSCompliant(false)]
public static class JsonElementExtensions
{
    /// <summary>
    /// Gets a value indicating whether this value is not null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is not null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotNull<T>(this T value)
        where T : struct, IJsonElement
    {
        return value.ParentDocument is not null && value.TokenType is not JsonTokenType.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is neither null nor undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is neither null nor undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotNullOrUndefined<T>(this T value)
        where T : struct, IJsonElement
    {
        return value.ParentDocument is not null && value.TokenType is not JsonTokenType.None and not JsonTokenType.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is not undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is not undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNotUndefined<T>(this T value)
        where T : struct, IJsonElement
    {
        return value.ParentDocument is not null && value.TokenType is not JsonTokenType.None;
    }

    /// <summary>
    /// Gets a value indicating whether this value is null.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNull<T>(this T value)
    where T : struct, IJsonElement
    {
        return value.ParentDocument is not null && value.TokenType is JsonTokenType.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is null or undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrUndefined<T>(this T value)
        where T : struct, IJsonElement
    {
        return value.ParentDocument is null || value.TokenType is JsonTokenType.None or JsonTokenType.Null;
    }

    /// <summary>
    /// Gets a value indicating whether this value is undefined.
    /// </summary>
    /// <typeparam name="T">The type of the value to check.</typeparam>
    /// <param name="value">The value to check.</param>
    /// <returns><c>True</c> if the value is undefined.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsUndefined<T>(this T value)
        where T : struct, IJsonElement
    {
        return value.ParentDocument is null || value.TokenType is JsonTokenType.None;
    }
}