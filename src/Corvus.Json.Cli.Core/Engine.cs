// <copyright file="Engine.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Represents the available code generation engines.
/// </summary>
public enum Engine
{
    /// <summary>
    /// The V4 engine using Corvus.Json.ExtendedTypes.
    /// </summary>
    V4,

    /// <summary>
    /// The V5 engine using Corvus.Text.Json.
    /// </summary>
    V5,
}