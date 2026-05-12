// <copyright file="MatchIndex.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// A match index result from a <see cref="PropertySchemaMatchers{T}"/> lookup
/// used for unified property matching across local and hoisted allOf properties.
/// </summary>
public sealed class MatchIndex
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MatchIndex"/> class.
    /// </summary>
    /// <param name="value">The index value identifying the matched property.</param>
    public MatchIndex(int value)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the index value identifying the matched property.
    /// </summary>
    public int Value { get; }
}