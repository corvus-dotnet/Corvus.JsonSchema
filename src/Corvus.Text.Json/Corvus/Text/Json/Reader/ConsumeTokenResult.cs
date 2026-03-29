// <copyright file="ConsumeTokenResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// This enum captures the tri-state return value when trying to read the
/// next JSON token.
/// </summary>
internal enum ConsumeTokenResult : byte
{
    /// <summary>
    /// Reached a valid end of token and hence no action is required.
    /// </summary>
    Success,

    /// <summary>
    /// Observed incomplete data but progressed state partially in looking ahead.
    /// Return false and roll-back to a previously saved state.
    /// </summary>
    NotEnoughDataRollBackState,

    /// <summary>
    /// Observed incomplete data but no change was made to the state.
    /// Return false, but do not roll-back anything since nothing changed.
    /// </summary>
    IncompleteNoRollBackNecessary,
}