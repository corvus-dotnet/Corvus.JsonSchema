// <copyright file="GeneratedTypeAccessibility.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Defines the accessibility of the generated types.
/// </summary>
public enum GeneratedTypeAccessibility
{
    /// <summary>
    /// The generated types should be <see langword="public"/>.
    /// </summary>
    Public,

    /// <summary>
    /// The generated types should be <see langword="internal"/>.
    /// </summary>
    Internal,

    /// <summary>
    /// The generated types should be <see langword="private"/>.
    /// </summary>
    Private
}