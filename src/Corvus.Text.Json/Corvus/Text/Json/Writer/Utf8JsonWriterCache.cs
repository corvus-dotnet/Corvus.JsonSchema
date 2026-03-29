// <copyright file="Utf8JsonWriterCache.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;

namespace Corvus.Text.Json;

/// <summary>
/// Provides a high-performance thread-local caching system for <see cref="Utf8JsonWriter"/> and buffer instances
/// to minimize allocation overhead during JSON writing operations.
/// </summary>
/// <remarks>
/// <para>
/// This cache implements a sophisticated strategy to optimize JSON writing performance by reusing
/// expensive-to-create instances across multiple JSON writing calls within the same thread.
/// The caching mechanism is designed to handle both simple and recursive JSON writing scenarios.
/// </para>
/// <para>
/// <strong>Caching Strategy:</strong>
/// <list type="bullet">
/// <item><description><strong>Thread-Local Storage:</strong> Each thread maintains its own cache instance via <see cref="ThreadStaticAttribute"/></description></item>
/// <item><description><strong>Recursive Call Handling:</strong> Tracks rental depth to provide fresh instances for nested JSON writing</description></item>
/// <item><description><strong>Zero-Allocation Reuse:</strong> Cached instances are reset rather than reallocated for subsequent uses</description></item>
/// <item><description><strong>Automatic Cleanup:</strong> Writer and buffer state is automatically cleared when returned to the cache</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Benefits:</strong>
/// <list type="bullet">
/// <item><description>Eliminates repeated allocation of <see cref="Utf8JsonWriter"/> instances</description></item>
/// <item><description>Reuses pooled buffer writers to reduce GC pressure</description></item>
/// <item><description>Provides O(1) rent/return operations with minimal overhead</description></item>
/// <item><description>Scales efficiently with thread-local isolation (no contention)</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// <code>
/// // Rent writer and buffer for JSON writing
/// Utf8JsonWriter writer = Utf8JsonWriterCache.RentWriterAndBuffer(options, bufferSize, out var buffer);
/// try
/// {
/// // Perform JSON writing operations
/// writer.WriteStartObject();
/// writer.WriteString("property", "value");
/// writer.WriteEndObject();
/// }
/// finally
/// {
/// // Always return to cache when done
/// Utf8JsonWriterCache.ReturnWriterAndBuffer(writer, buffer);
/// }
/// </code>
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This cache is thread-safe through thread-local storage.
/// Each thread operates on its own isolated cache instance with no shared state.
/// </para>
/// </remarks>
internal static class Utf8JsonWriterCache
{
    [ThreadStatic]
    private static ThreadLocalState? t_threadLocalState;

    /// <summary>
    /// Rents a <see cref="Utf8JsonWriter"/> instance from the thread-local cache, optimized for the provided buffer writer.
    /// </summary>
    /// <param name="options">The writer options to configure the returned writer instance.</param>
    /// <param name="bufferWriter">The buffer writer that the returned writer will write to.</param>
    /// <returns>
    /// A <see cref="Utf8JsonWriter"/> instance ready for use. This may be a cached instance (for the first
    /// call in the current thread's call stack) or a new instance (for recursive calls).
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method implements intelligent instance management based on call depth:
    /// </para>
    /// <para>
    /// <strong>First Call (Depth 0):</strong>
    /// <list type="bullet">
    /// <item><description>Returns the cached writer instance from thread-local storage</description></item>
    /// <item><description>Resets the writer with the provided buffer and options</description></item>
    /// <item><description>Provides optimal performance through instance reuse</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recursive Calls (Depth > 0):</strong>
    /// <list type="bullet">
    /// <item><description>Creates and returns a fresh writer instance</description></item>
    /// <item><description>Prevents conflicts with the cached instance in use</description></item>
    /// <item><description>Ensures safe nested JSON writing scenarios</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Characteristics:</strong>
    /// <list type="bullet">
    /// <item><description>O(1) operation with minimal allocation overhead</description></item>
    /// <item><description>Zero allocations for non-recursive scenarios (common case)</description></item>
    /// <item><description>Thread-local access eliminates synchronization costs</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Must be paired with <see cref="ReturnWriter"/>:</strong> Always call <see cref="ReturnWriter"/>
    /// when finished to properly return the writer to the cache and maintain correct rental tracking.
    /// </para>
    /// </remarks>
    public static Utf8JsonWriter RentWriter(JsonWriterOptions options, IBufferWriter<byte> bufferWriter)
    {
        ThreadLocalState state = t_threadLocalState ??= new();
        Utf8JsonWriter writer;

        if (state.RentedWriters++ == 0)
        {
            // First call in the stack -- initialize & return the cached instance.
            writer = state.Writer;
            writer.Reset(bufferWriter, options);
        }
        else
        {
            // We're in a recursive call -- return a fresh instance.
            writer = new Utf8JsonWriter(bufferWriter, options);
        }

        return writer;
    }

    /// <summary>
    /// Rents both a <see cref="Utf8JsonWriter"/> and a <see cref="PooledByteBufferWriter"/> from the thread-local cache
    /// for complete JSON serialization with optimized buffer management.
    /// </summary>
    /// <param name="options">The writer options to configure the returned writer instance.</param>
    /// <param name="defaultBufferSize">The initial buffer size for the pooled buffer writer.</param>
    /// <param name="bufferWriter">
    /// When this method returns, contains the rented buffer writer instance that the returned writer will use.
    /// </param>
    /// <returns>
    /// A <see cref="Utf8JsonWriter"/> instance configured to write to the provided <paramref name="bufferWriter"/>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides the most comprehensive caching strategy by managing both the writer and its
    /// underlying buffer. It's optimized for scenarios where you need complete control over the entire
    /// serialization pipeline.
    /// </para>
    /// <para>
    /// <strong>Caching Behavior by Call Depth:</strong>
    /// </para>
    /// <para>
    /// <strong>First Call (Depth 0):</strong>
    /// <list type="bullet">
    /// <item><description>Returns cached writer and buffer instances from thread-local storage</description></item>
    /// <item><description>Initializes the buffer with the specified default size</description></item>
    /// <item><description>Resets the writer to use the initialized buffer</description></item>
    /// <item><description>Achieves maximum performance through dual instance reuse</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Recursive Calls (Depth > 0):</strong>
    /// <list type="bullet">
    /// <item><description>Creates fresh writer and buffer instances</description></item>
    /// <item><description>Prevents interference with cached instances in use</description></item>
    /// <item><description>Maintains safety for nested serialization operations</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Buffer Management Benefits:</strong>
    /// <list type="bullet">
    /// <item><description>Pooled buffer reduces allocation pressure from array pools</description></item>
    /// <item><description>Automatic buffer expansion as needed during serialization</description></item>
    /// <item><description>Efficient buffer clearing and pool return on cache return</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Must be paired with <see cref="ReturnWriterAndBuffer"/>:</strong> Always call
    /// <see cref="ReturnWriterAndBuffer"/> when finished to properly return both instances to the cache.
    /// </para>
    /// </remarks>
    public static Utf8JsonWriter RentWriterAndBuffer(JsonWriterOptions options, int defaultBufferSize, out PooledByteBufferWriter bufferWriter)
    {
        ThreadLocalState state = t_threadLocalState ??= new();
        Utf8JsonWriter writer;

        if (state.RentedWriters++ == 0)
        {
            // First JsonSerializer call in the stack -- initialize & return the cached instances.
            bufferWriter = state.BufferWriter;
            writer = state.Writer;

            bufferWriter.InitializeEmptyInstance(defaultBufferSize);
            writer.Reset(bufferWriter, options);
        }
        else
        {
            // We're in a recursive JsonSerializer call -- return fresh instances.
            bufferWriter = new PooledByteBufferWriter(defaultBufferSize);
            writer = new Utf8JsonWriter(bufferWriter, options);
        }

        return writer;
    }

    /// <summary>
    /// Returns a rented <see cref="Utf8JsonWriter"/> instance to the thread-local cache for future reuse.
    /// </summary>
    /// <param name="writer">The writer instance to return, previously obtained from <see cref="RentWriter"/>.</param>
    /// <remarks>
    /// <para>
    /// This method completes the writer rental lifecycle by:
    /// <list type="bullet">
    /// <item><description>Resetting all writer state to prepare for cache reuse</description></item>
    /// <item><description>Decrementing the rental depth counter for proper recursive call tracking</description></item>
    /// <item><description>Validating that cache state remains consistent (in debug builds)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>State Cleanup:</strong>
    /// The writer's <c>ResetAllStateForCacheReuse()</c> method is called to ensure:
    /// <list type="bullet">
    /// <item><description>All internal buffers and state are cleared</description></item>
    /// <item><description>The writer is ready for a completely fresh serialization operation</description></item>
    /// <item><description>No data leakage occurs between different serialization sessions</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Rental Depth Management:</strong>
    /// The method decrements the rental counter, which:
    /// <list type="bullet">
    /// <item><description>Tracks the depth of nested serialization calls</description></item>
    /// <item><description>Ensures proper pairing of rent/return operations</description></item>
    /// <item><description>Enables debug assertions to verify cache consistency</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Critical:</strong> This method must be called for every writer obtained from <see cref="RentWriter"/>
    /// to maintain proper cache state and prevent resource leaks.
    /// </para>
    /// </remarks>
    public static void ReturnWriter(Utf8JsonWriter writer)
    {
        Debug.Assert(t_threadLocalState != null);
        ThreadLocalState state = t_threadLocalState;

        writer.ResetAllStateForCacheReuse();

        int rentedWriters = --state.RentedWriters;
        Debug.Assert((rentedWriters == 0) == ReferenceEquals(state.Writer, writer));
    }

    /// <summary>
    /// Returns both a rented <see cref="Utf8JsonWriter"/> and its associated buffer writer to the thread-local cache.
    /// </summary>
    /// <param name="writer">The writer instance to return, previously obtained from <see cref="RentWriterAndBuffer"/>.</param>
    /// <param name="bufferWriter">The buffer writer instance to return, previously obtained from <see cref="RentWriterAndBuffer"/>.</param>
    /// <remarks>
    /// <para>
    /// This method provides comprehensive cleanup for both the writer and buffer components:
    /// </para>
    /// <para>
    /// <strong>Writer State Reset:</strong>
    /// <list type="bullet">
    /// <item><description>Calls <c>ResetAllStateForCacheReuse()</c> to clear all writer state</description></item>
    /// <item><description>Prepares the writer for future reuse without state contamination</description></item>
    /// <item><description>Ensures JSON writing state is completely reset</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Buffer Management:</strong>
    /// <list type="bullet">
    /// <item><description>Calls <c>ClearAndReturnBuffers()</c> to return pooled memory to the array pool</description></item>
    /// <item><description>Clears all buffered content to prevent data leakage</description></item>
    /// <item><description>Resets buffer state for optimal reuse performance</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Cache Consistency:</strong>
    /// <list type="bullet">
    /// <item><description>Decrements rental depth counter for proper recursive call tracking</description></item>
    /// <item><description>Validates that cached instances match returned instances (debug builds)</description></item>
    /// <item><description>Maintains thread-local cache state integrity</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Performance Benefits:</strong>
    /// This dual cleanup approach ensures that both writer and buffer are optimally prepared for
    /// subsequent cache reuse, maximizing the performance benefits of the caching system.
    /// </para>
    /// <para>
    /// <strong>Critical:</strong> This method must be called for every writer and buffer pair obtained from
    /// <see cref="RentWriterAndBuffer"/> to maintain proper cache state and prevent resource leaks.
    /// </para>
    /// </remarks>
    public static void ReturnWriterAndBuffer(Utf8JsonWriter writer, IByteBufferWriter bufferWriter)
    {
        Debug.Assert(t_threadLocalState != null);
        ThreadLocalState state = t_threadLocalState;

        writer.ResetAllStateForCacheReuse();
        bufferWriter.ClearAndReturnBuffers();

        int rentedWriters = --state.RentedWriters;
        Debug.Assert((rentedWriters == 0) == (ReferenceEquals(state.BufferWriter, bufferWriter) && ReferenceEquals(state.Writer, writer)));
    }

    /// <summary>
    /// Represents the thread-local state for caching writer and buffer instances along with rental tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class encapsulates all per-thread cache state, providing isolation between threads
    /// and enabling lock-free cache operations. Each thread maintains its own instance via
    /// <see cref="ThreadStaticAttribute"/>.
    /// </para>
    /// <para>
    /// <strong>State Components:</strong>
    /// <list type="bullet">
    /// <item><description><see cref="BufferWriter"/>: Reusable pooled buffer writer instance</description></item>
    /// <item><description><see cref="Writer"/>: Reusable JSON writer instance</description></item>
    /// <item><description><see cref="RentedWriters"/>: Depth counter for recursive call tracking</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Initialization Strategy:</strong>
    /// Both cached instances are created in a special "empty" state optimized for caching.
    /// This allows them to be efficiently reset and reused without recreating the underlying objects.
    /// </para>
    /// <para>
    /// <strong>Rental Depth Tracking:</strong>
    /// The <see cref="RentedWriters"/> counter enables the cache to distinguish between:
    /// <list type="bullet">
    /// <item><description>Initial calls (depth 0): Use cached instances</description></item>
    /// <item><description>Recursive calls (depth > 0): Create fresh instances</description></item>
    /// </list>
    /// This prevents conflicts when serialization operations are nested.
    /// </para>
    /// </remarks>
    private sealed class ThreadLocalState
    {
        /// <summary>
        /// Gets the cached <see cref="PooledByteBufferWriter"/> instance for this thread.
        /// </summary>
        /// <remarks>
        /// This buffer writer is initialized in an empty state and reused across multiple
        /// serialization operations within the same thread. It's automatically cleared
        /// and reset when returned to the cache.
        /// </remarks>
        public readonly PooledByteBufferWriter BufferWriter;

        /// <summary>
        /// Gets the cached <see cref="Utf8JsonWriter"/> instance for this thread.
        /// </summary>
        /// <remarks>
        /// This writer is initialized in an empty state and reused across multiple
        /// serialization operations within the same thread. It's automatically reset
        /// when returned to the cache.
        /// </remarks>
        public readonly Utf8JsonWriter Writer;

        /// <summary>
        /// Gets or sets the current rental depth for tracking recursive serialization calls.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This counter tracks how many writers are currently rented from this thread's cache:
        /// <list type="bullet">
        /// <item><description>0: No active rentals, cache instances available</description></item>
        /// <item><description>1: First rental active, using cached instances</description></item>
        /// <item><description>>1: Recursive rentals active, using fresh instances</description></item>
        /// </list>
        /// </para>
        /// <para>
        /// This enables the cache to provide cached instances for the common case (single serialization)
        /// while ensuring thread safety for nested serialization scenarios.
        /// </para>
        /// </remarks>
        public int RentedWriters;

        /// <summary>
        /// Initializes a new thread-local cache state with empty writer and buffer instances.
        /// </summary>
        /// <remarks>
        /// Both the buffer writer and JSON writer are created in special "empty" states that
        /// are optimized for caching and reuse. These instances avoid the overhead of repeated
        /// object allocation while maintaining full functionality when reset for actual use.
        /// </remarks>
        public ThreadLocalState()
        {
            BufferWriter = PooledByteBufferWriter.CreateEmptyInstanceForCaching();
            Writer = Utf8JsonWriter.CreateEmptyInstanceForCaching();
        }
    }
}