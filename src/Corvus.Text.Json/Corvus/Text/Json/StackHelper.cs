// <copyright file="StackHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json;

/// <summary>Provides tools for avoiding stack overflows.</summary>
internal static class StackHelper
{
    /// <summary>Tries to ensure there is sufficient stack to execute the average .NET function.</summary>
    public static bool TryEnsureSufficientExecutionStack()
    {
#if NET
        return RuntimeHelpers.TryEnsureSufficientExecutionStack();
#else
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return true;
        }
        catch
        {
            return false;
        }
#endif
    }
}