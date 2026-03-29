// <copyright file="ValidationLevel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Compatibility;

/// <summary>
/// The validation level.
/// </summary>
public enum ValidationLevel
{
    /// <summary>
    /// 10.4.1. Flag.
    /// </summary>
    Flag,

    /// <summary>
    /// 10.4.2. Basic.
    /// </summary>
    Basic,

    /// <summary>
    /// 10.4.3. Detailed.
    /// </summary>
    Detailed,

    /// <summary>
    /// 10.4.4. Verbose.
    /// </summary>
    Verbose,
}