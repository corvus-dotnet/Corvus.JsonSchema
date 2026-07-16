// <copyright file="ParsedJsonDocumentBuilderCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Defines a thread-local cache for us to store reusable <see cref="ParsedJsonDocumentBuilder"/> instances.
/// </summary>
internal static class ParsedJsonDocumentBuilderCache
{
    [ThreadStatic]
    private static ThreadLocalState? t_threadLocalState;

    /// <summary>
    /// Rents a builder from the thread-local cache or creates a new one.
    /// </summary>
    /// <returns>A builder instance from the cache or a new instance.</returns>
    /// <remarks>
    /// The depth counter handles re-entrant rentals (for example a nested <c>Create()</c> call made
    /// from inside a build delegate): the first rental on the stack gets the cached instance, and
    /// nested rentals get fresh instances that are simply dropped on return.
    /// </remarks>
    public static ParsedJsonDocumentBuilder RentBuilder()
    {
        ThreadLocalState state = t_threadLocalState ??= new();

        if (state.RentedBuilders++ == 0)
        {
            // First call in the stack -- return the cached instance.
            return state.Builder;
        }

        // We've rented a second builder, so we're going to create another instance.
        return ParsedJsonDocumentBuilder.CreateEmptyInstanceForCaching();
    }

    /// <summary>
    /// Returns a builder to the thread-local cache for reuse. The builder must already have released
    /// its pooled resources.
    /// </summary>
    /// <param name="builder">The builder to return to the cache.</param>
    public static void ReturnBuilder(ParsedJsonDocumentBuilder builder)
    {
        Debug.Assert(t_threadLocalState != null);
        ThreadLocalState state = t_threadLocalState;

        int rentedBuilders = --state.RentedBuilders;
        Debug.Assert((rentedBuilders == 0) == ReferenceEquals(state.Builder, builder));
    }

    private sealed class ThreadLocalState
    {
        public readonly ParsedJsonDocumentBuilder Builder;

        public int RentedBuilders;

        public ThreadLocalState()
        {
            Builder = ParsedJsonDocumentBuilder.CreateEmptyInstanceForCaching();
        }
    }
}