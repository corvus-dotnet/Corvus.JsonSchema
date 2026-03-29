// <copyright file="JsonSchemaResultsCollectorCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Defines a thread-local cache for us to store reusable JsonSchemaResultsCollector instances.
/// </summary>
internal static class JsonSchemaResultsCollectorCache
{
    [ThreadStatic]
    private static ThreadLocalState? s_threadLocalState;

    /// <summary>
    /// Rents a JsonSchemaResultsCollector from the thread-local cache or creates a new one.
    /// </summary>
    /// <param name="level">The verbosity level for results collection.</param>
    /// <param name="initialCapacity">The initial capacity estimate for the collector.</param>
    /// <returns>A JsonSchemaResultsCollector instance ready for use.</returns>
    public static JsonSchemaResultsCollector RentResultsCollector(JsonSchemaResultsLevel level = JsonSchemaResultsLevel.Basic, int initialCapacity = 30)
    {
        ThreadLocalState state = s_threadLocalState ??= new();
        JsonSchemaResultsCollector collector;

        if (state.RentedCollectors++ == 0)
        {
            // First call in the stack -- initialize & return the cached instance.
            collector = state.Collector;
            collector.Reset(level, initialCapacity);
        }
        else
        {
            // We've created a second collector, so we're going to create another instance.
            collector = new JsonSchemaResultsCollector(true, level, initialCapacity);
        }

        return collector;
    }

    /// <summary>
    /// Returns a JsonSchemaResultsCollector to the thread-local cache for reuse.
    /// </summary>
    /// <param name="collector">The collector to return to the cache.</param>
    public static void ReturnResultsCollector(JsonSchemaResultsCollector collector)
    {
        Debug.Assert(s_threadLocalState != null);
        ThreadLocalState state = s_threadLocalState;

        collector.ResetAllStateForCacheReuse();

        int rentedWorkspaces = --state.RentedCollectors;
        Debug.Assert((rentedWorkspaces == 0) == ReferenceEquals(state.Collector, collector));
    }

    private sealed class ThreadLocalState
    {
        public readonly JsonSchemaResultsCollector Collector;

        public int RentedCollectors;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadLocalState"/> class.
        /// </summary>
        public ThreadLocalState()
        {
            Collector = JsonSchemaResultsCollector.CreateEmptyInstanceForCaching();
        }
    }
}