// <copyright file="JsonSchemaResultsLevel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// The level of result to collect for an <see cref="IJsonSchemaResultsCollector"/>.
/// </summary>
public enum JsonSchemaResultsLevel
{
    /// <summary>
    /// Includes basic location and message information about schema matching failures.
    /// </summary>
    Basic,

    /// <summary>
    /// Includes detailed location and message information about schema matching failures.
    /// </summary>
    Detailed,

    /// <summary>
    /// Includes full location and message information for schema matching.
    /// </summary>
    Verbose,
}