// <copyright file="ConsumeNumberResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// This enum captures the tri-state return value when trying to read a
/// JSON number.
/// </summary>
internal enum ConsumeNumberResult : byte
{
    /// <summary>
    /// Reached a valid end of number and hence no action is required.
    /// </summary>
    Success,

    /// <summary>
    /// Successfully processed a portion of the number and need to
    /// read to the next region of the number.
    /// </summary>
    OperationIncomplete,

    /// <summary>
    /// Observed incomplete data.
    /// Return false if we have more data to follow. Otherwise throw.
    /// </summary>
    NeedMoreData,
}