// <copyright file="NativeEnums.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Controls emission of native C# enums on generated types (V5 engine only).
/// </summary>
public enum NativeEnums
{
    /// <summary>
    /// No native C# enums are emitted.
    /// </summary>
    None,

    /// <summary>
    /// A pure string-enum schema additionally generates a nested native C# enum with conversions.
    /// </summary>
    StringEnums,

    /// <summary>
    /// An object schema whose declared properties are all boolean additionally generates a nested
    /// native C# <c>[Flags]</c> enum with conversions.
    /// </summary>
    FlagsObjects,

    /// <summary>
    /// Both <see cref="StringEnums"/> and <see cref="FlagsObjects"/> are emitted.
    /// </summary>
    All,
}