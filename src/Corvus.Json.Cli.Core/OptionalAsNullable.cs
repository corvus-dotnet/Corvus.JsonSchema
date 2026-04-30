// <copyright file="OptionalAsNullable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Determines whether optional properties are emitted as .NET nullable values.
/// </summary>
public enum OptionalAsNullable
{
    /// <summary>
    /// Optional properties are not emitted as nullable
    /// </summary>
    None,

    /// <summary>
    /// Properties that are optional are emitted as nullable, and <c>JsonValueKind.Null</c> or 
    /// <c>JsonValueKind.Undefined</c> values produce <see langword="null"/>.
    /// </summary>
    NullOrUndefined,
}