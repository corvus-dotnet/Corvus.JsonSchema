// <copyright file="JsonSchemaResultsCollector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Collects and manages results from JSON schema validation operations with high-performance memory management,
/// stack-based evaluation tracking, and configurable verbosity levels.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Core Architecture:</strong>
/// </para>
/// <para>
/// The <see cref="JsonSchemaResultsCollector"/> provides a sophisticated result collection system optimized
/// for JSON schema validation workflows. It manages multiple evaluation paths simultaneously and supports
/// hierarchical validation contexts through a stack-based approach.
/// </para>
/// <list type="bullet">
/// <item><description><strong>Multi-Path Tracking:</strong> Maintains evaluation, schema, and document paths separately for precise error reporting</description></item>
/// <item><description><strong>Stack-Based Contexts:</strong> Hierarchical validation contexts with proper nesting and cleanup</description></item>
/// <item><description><strong>Pooled Memory Management:</strong> Thread-local pooling for high-throughput validation scenarios</description></item>
/// <item><description><strong>Configurable Verbosity:</strong> Three levels from basic failures to comprehensive validation logs</description></item>
/// <item><description><strong>UTF-8 Optimized:</strong> Direct UTF-8 processing for reduced encoding overhead</description></item>
/// </list>
///
/// <para>
/// <strong>Lifecycle Management:</strong>
/// </para>
/// <para>
/// The collector follows a strict lifecycle pattern designed for efficient resource management:
/// </para>
/// <list type="number">
/// <item><description><strong>Creation:</strong> Use <see cref="Create"/> for pooled instances or <see cref="CreateUnrented"/> for standalone use</description></item>
/// <item><description><strong>Configuration:</strong> Set verbosity level and capacity estimates during creation</description></item>
/// <item><description><strong>Collection:</strong> Use context methods to track validation hierarchy and results</description></item>
/// <item><description><strong>Enumeration:</strong> Access results through <see cref="EnumerateResults"/> after validation completion</description></item>
/// <item><description><strong>Disposal:</strong> Always dispose to return pooled resources and clear sensitive data</description></item>
/// </list>
///
/// <para>
/// <strong>Memory Management:</strong>
/// </para>
/// <para>
/// Designed for high-performance scenarios with minimal garbage collection pressure:
/// </para>
/// <list type="bullet">
/// <item><description><strong>Pooled Instances:</strong> Thread-local caching reduces allocation overhead in high-throughput scenarios</description></item>
/// <item><description><strong>ArrayPool Integration:</strong> Uses shared array pools for internal buffers with proper cleanup</description></item>
/// <item><description><strong>Stack Allocation:</strong> ValueStack structures minimize heap allocations for context management</description></item>
/// <item><description><strong>Capacity Estimation:</strong> Pre-sizing based on expected result count prevents reallocations</description></item>
/// <item><description><strong>Security Clearing:</strong> Sensitive data is explicitly cleared on disposal</description></item>
/// </list>
///
/// <para>
/// <strong>Performance Characteristics:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>Time Complexity:</strong> O(1) result insertion, O(n) enumeration where n is result count</description></item>
/// <item><description><strong>Space Complexity:</strong> O(d + r) where d is max validation depth, r is result count</description></item>
/// <item><description><strong>Memory Overhead:</strong> ~32 bytes per path segment, ~128 bytes per message (configurable)</description></item>
/// <item><description><strong>Thread Safety:</strong> Not thread-safe; use separate instances per thread</description></item>
/// </list>
///
/// <para>
/// <strong>Verbosity Level Guidance:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>Basic:</strong> Failure messages only - minimal overhead, use for production validation</description></item>
/// <item><description><strong>Detailed:</strong> Failure messages with detailed context - moderate overhead, use for debugging</description></item>
/// <item><description><strong>Verbose:</strong> All validation events including successes - high overhead, use for comprehensive analysis</description></item>
/// </list>
///
/// <para>
/// <strong>Usage Patterns:</strong>
/// </para>
/// <list type="bullet">
/// <item><description><strong>High-Throughput:</strong> Use pooled instances with accurate capacity estimation</description></item>
/// <item><description><strong>One-Off Validation:</strong> Use unpooled instances for occasional validation</description></item>
/// <item><description><strong>Debugging:</strong> Use Detailed or Verbose levels with comprehensive result analysis</description></item>
/// <item><description><strong>Production:</strong> Use Basic level with pooled instances for optimal performance</description></item>
/// </list>
///
/// <para>
/// <strong>Context Hierarchy:</strong>
/// </para>
/// <para>
/// The collector maintains a stack-based context system where each validation step creates a child context.
/// Contexts must be completed in reverse order (stack discipline) using either commit or pop operations.
/// This enables precise error location tracking and efficient resource cleanup.
/// </para>
///
/// <para>
/// <strong>Thread Safety:</strong>
/// </para>
/// <para>
/// This class is <strong>not thread-safe</strong>. Each thread must use its own collector instance.
/// The internal thread-local pooling system handles concurrent access to the cache safely.
/// </para>
/// </remarks>
/// <example>
/// <para>Basic validation with pooled collector:</para>
/// <code>
/// using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic, estimatedCapacity: 50);

///
/// // Perform validation operations...
/// int context = collector.BeginChildContext(0, evaluationPath, schemaPath, documentPath);

/// collector.CommitChildContext(context, parentMatch: true, childMatch: false, messageProvider);

///
/// // Enumerate results
/// foreach (var result in collector.EnumerateResults())
/// {
/// if (!result.IsMatch)
/// {
/// Console.WriteLine($"Validation failed: {result.GetMessageText()}");

/// Console.WriteLine($"  Location: {result.GetDocumentEvaluationLocationText()}");

/// }

/// }

/// </code>
/// </example>
/// <example>
/// <para>High-performance validation pattern:</para>
/// <code>
/// // Pre-size for expected validation complexity
/// using var collector = JsonSchemaResultsCollector.Create(
/// JsonSchemaResultsLevel.Basic,
/// estimatedCapacity: documentSize / 10);

///
/// // Use UTF-8 paths for optimal performance
/// ReadOnlySpan&lt;byte&gt; propertyName = "username"u8;

/// int context = collector.BeginChildContext(0, propertyName, evaluationPath, schemaPath);

///
/// // Minimal overhead result reporting
/// collector.CommitChildContext(context, true, validationResult, messageProvider);

///
/// // Fast result access
/// int resultCount = collector.GetResultCount();

/// if (resultCount > 0)
/// {
/// var enumerator = collector.EnumerateResults();

/// while (enumerator.MoveNext())
/// {
/// ProcessResult(enumerator.Current);

/// }

/// }

/// </code>
/// </example>
/// <example>
/// <para>Debugging with verbose output:</para>
/// <code>
/// using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose, 100);

///
/// // Verbose mode captures all validation steps
/// int rootContext = collector.BeginChildContext(0, rootEvalPath, rootSchemaPath, rootDocPath);

///
/// // All keyword evaluations are recorded
/// collector.EvaluatedKeyword(true, successMessageProvider, "type"u8);

/// collector.EvaluatedKeyword(false, failureMessageProvider, "pattern"u8);

///
/// collector.CommitChildContext(rootContext, true, false, summaryMessageProvider);

///
/// // Comprehensive result analysis
/// foreach (var result in collector.EnumerateResults())
/// {
/// LogValidationResult(
/// result.IsMatch,
/// result.GetEvaluationLocationText(),
/// result.GetSchemaEvaluationLocationText(),
/// result.GetDocumentEvaluationLocationText(),
/// result.GetMessageText());

/// }

/// </code>
/// </example>
public sealed class JsonSchemaResultsCollector : IJsonSchemaResultsCollector
{
    // Maximum message length is 1024 bytes
    private const int MaxMessageLength = 1024;

    private const int ResultHeaderSize = 4;

    private const int MaxPathSegmentLength = 1024;

    private const int MaxUriBaseLength = 4096;

    // We assume an initial estimate of 32 bytes per path segment, and 128 bytes per message
    private const int BytesPerPathSegment = 32;

    private const int BytesPerMessage = 128;

    /// <summary>
    /// Represents a range of values in the results buffer with efficient bounds tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This structure is used internally to track segments within the UTF-8 result buffer,
    /// enabling O(1) access to result components without string allocation overhead.
    /// The sequential layout optimizes for cache performance during result enumeration.
    /// </para>
    /// <para>
    /// <strong>Memory Layout:</strong>
    /// </para>
    /// <para>
    /// Designed as a sequential structure to minimize memory fragmentation and optimize
    /// cache line utilization during result processing operations.
    /// </para>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct ValueRange
    {
        public readonly int Start;

        public readonly int End;

        public ValueRange(int start, int end)
        {
            Debug.Assert(start <= end);
            Start = start;
            End = end;
        }

        public int Length => End - Start;
    }

    /// <summary>
    /// Represents a range of values in the results buffer, including commit index and sequence number for hierarchical context tracking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This structure extends <see cref="ValueRange"/> with additional metadata required for managing
    /// hierarchical validation contexts. The commit index and sequence number enable proper stack
    /// discipline and result rollback during validation tree traversal.
    /// </para>
    /// <para>
    /// <strong>Context Management:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>CommitIndex:</strong> Tracks the committed result stack state at context creation</description></item>
    /// <item><description><strong>SequenceNumber:</strong> Enforces proper nesting order in debug builds</description></item>
    /// <item><description><strong>Range:</strong> Tracks buffer positions for efficient result storage</description></item>
    /// </list>
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    private readonly struct ValueRangeWithCommitIndexAndSequenceNumber
    {
        public readonly int Start;

        public readonly int End;

        public readonly int CommitIndex;

        public readonly int SequenceNumber;

        public ValueRangeWithCommitIndexAndSequenceNumber(int start, int end, int commitIndex, int sequenceNumber)
        {
            Debug.Assert(start <= end);
            Start = start;
            End = end;
            CommitIndex = commitIndex;
            SequenceNumber = sequenceNumber;
        }

        public int Length => End - Start;
    }

    private readonly bool _rented;

    private bool _isDisposed;

    private byte[] _utf8StringBacking;

    private int _utf8StringBackingLength;

    private ValueRange _currentEvaluationPathRange;

    private ValueRange _currentDocumentEvaluationPathRange;

    private ValueRange _currentSchemaEvaluationPathRange;

    private byte[] _evaluationPath;

    private byte[] _schemaEvaluationPath;

    private byte[] _documentEvaluationPath;

    private int _sequenceNumber;

    private JsonSchemaResultsLevel _level;

    // indices for the end of the path stack at each level
    // When pushing items onto a path stack, we just append it if it is an append
    // or push the whole path if it is change of base. We push the previous start/end
    // range onto the corresponding stack, and then update the _currentXYZPathRange.
    private ValueStack<ValueRange> _evaluationPathStack;

    private ValueStack<ValueRange> _documentEvaluationPathStack;

    private ValueStack<ValueRange> _schemaEvaluationPathStack;

    private ValueStack<ValueRangeWithCommitIndexAndSequenceNumber> _resultStack;

    private ValueStack<ValueRange> _committedResultStack;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaResultsCollector"/> class.
    /// </summary>
    /// <param name="rented">A value indicating whether this instance is rented from a pool.</param>
    /// <param name="level">The level of detail to collect in the results.</param>
    /// <param name="estimatedCapacity">An estimate of the number of results that will be collected.</param>
    internal JsonSchemaResultsCollector(bool rented, JsonSchemaResultsLevel level, int estimatedCapacity = 30)
    {
        if (estimatedCapacity <= 0)
        {
            // Force to a reasonable basic estimated capacity
            estimatedCapacity = 30;
        }

        _rented = rented;
        _level = level;

        // We will allow an additional "MaxPathSegmentLength" of capacity to avoid
        // enlarging with our max test each time.
        int pathCapacity = (estimatedCapacity * BytesPerPathSegment) + MaxPathSegmentLength;

        _schemaEvaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity + MaxUriBaseLength);

        _evaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity);
        _documentEvaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity);

        int messageCapacity = estimatedCapacity * BytesPerMessage;
        _utf8StringBacking = ArrayPool<byte>.Shared.Rent(messageCapacity);

        // We will just use the default max depth for a JSON document for the evaluation path depth
        _evaluationPathStack = new ValueStack<ValueRange>(JsonDocumentOptions.DefaultMaxDepth);
        _documentEvaluationPathStack = new ValueStack<ValueRange>(JsonDocumentOptions.DefaultMaxDepth);
        _schemaEvaluationPathStack = new ValueStack<ValueRange>(JsonDocumentOptions.DefaultMaxDepth);

        _resultStack = new ValueStack<ValueRangeWithCommitIndexAndSequenceNumber>(JsonDocumentOptions.DefaultMaxDepth);
        _committedResultStack = new ValueStack<ValueRange>(JsonDocumentOptions.DefaultMaxDepth);
    }

    /// <summary>
    /// Creates a JSON schema results collector from the thread-local pool with optimal memory management.
    /// </summary>
    /// <param name="level">
    /// Controls result verbosity and collection overhead:
    /// <list type="bullet">
    /// <item><description><see cref="JsonSchemaResultsLevel.Basic"/>: Failure messages only (lowest overhead)</description></item>
    /// <item><description><see cref="JsonSchemaResultsLevel.Detailed"/>: Detailed failure context (moderate overhead)</description></item>
    /// <item><description><see cref="JsonSchemaResultsLevel.Verbose"/>: All validation events (highest overhead)</description></item>
    /// </list>
    /// </param>
    /// <param name="estimatedCapacity">
    /// Expected number of validation results to optimize internal buffer sizing.
    /// Accurate estimation prevents reallocations and improves performance.
    /// Use 0 for unknown capacity (defaults to 30).
    /// </param>
    /// <returns>
    /// A pooled <see cref="JsonSchemaResultsCollector"/> instance ready for validation result collection.
    /// Must be disposed to return resources to the pool.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Pooling Behavior:</strong>
    /// </para>
    /// <para>
    /// This method leverages thread-local pooling for optimal performance in high-throughput scenarios.
    /// The first call per thread returns a cached instance; subsequent concurrent calls create new instances.
    /// Always dispose the returned collector to ensure proper pool management.
    /// </para>
    /// <para>
    /// <strong>Memory Pre-allocation:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Path Buffers:</strong> (estimatedCapacity × 32 bytes) + 1024 bytes for path segments</description></item>
    /// <item><description><strong>Message Buffer:</strong> Varies by level - Basic: minimal, Verbose: (estimatedCapacity × 128 bytes)</description></item>
    /// <item><description><strong>Schema Path Buffer:</strong> Additional 4096 bytes for URI base length</description></item>
    /// </list>
    /// <para>
    /// <strong>Performance Guidelines:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>High Throughput:</strong> Use accurate capacity estimation to minimize allocations</description></item>
    /// <item><description><strong>Memory Constrained:</strong> Use lower verbosity levels and conservative capacity estimates</description></item>
    /// <item><description><strong>Debugging:</strong> Use Verbose level with generous capacity estimates for complete information</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para>Production validation with optimized settings:</para>
    /// <code>
    /// // Estimate capacity based on schema complexity
    /// int expectedResults = schemaKeywordCount * documentDepth / 4;

    /// using var collector = JsonSchemaResultsCollector.Create(
    /// JsonSchemaResultsLevel.Basic,
    /// expectedResults);

    ///
    /// // Validation operations...
    /// </code>
    /// </example>
    /// <example>
    /// <para>Development debugging with comprehensive logging:</para>
    /// <code>
    /// // Liberal capacity for complete validation trace
    /// using var collector = JsonSchemaResultsCollector.Create(
    /// JsonSchemaResultsLevel.Verbose,
    /// estimatedCapacity: 200);

    ///
    /// // All validation steps will be captured
    /// </code>
    /// </example>
    public static JsonSchemaResultsCollector Create(JsonSchemaResultsLevel level, int estimatedCapacity = 30)
    {
        return JsonSchemaResultsCollectorCache.RentResultsCollector(level, estimatedCapacity);
    }

    /// <summary>
    /// Creates a non-pooled JSON schema results collector for standalone or specialized usage scenarios.
    /// </summary>
    /// <param name="level">Controls result verbosity and collection overhead.</param>
    /// <param name="estimatedCapacity">Expected number of validation results for buffer pre-sizing.</param>
    /// <returns>
    /// A standalone <see cref="JsonSchemaResultsCollector"/> instance that is not managed by the thread-local pool.
    /// Resources are released directly on disposal without pool interaction.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Use Cases:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Long-lived Instances:</strong> When the collector lifetime exceeds typical validation scope</description></item>
    /// <item><description><strong>Memory Isolation:</strong> When pool interaction is undesirable for security or debugging</description></item>
    /// <item><description><strong>Testing:</strong> When predictable memory behavior is required for unit tests</description></item>
    /// <item><description><strong>Single-use:</strong> When validation frequency doesn't justify pooling overhead</description></item>
    /// </list>
    /// <para>
    /// <strong>Performance Considerations:</strong>
    /// </para>
    /// <para>
    /// Non-pooled instances have higher allocation overhead but provide complete memory isolation.
    /// Each instance allocates fresh buffers from ArrayPool and returns them directly on disposal.
    /// Use when pool interaction is problematic or when instance lifetime is unpredictable.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Standalone collector for isolated validation
    /// using var collector = JsonSchemaResultsCollector.CreateUnrented(
    /// JsonSchemaResultsLevel.Detailed,
    /// estimatedCapacity: 75);

    ///
    /// // Validation operations with complete memory isolation...
    /// </code>
    /// </example>
    public static JsonSchemaResultsCollector CreateUnrented(JsonSchemaResultsLevel level, int estimatedCapacity = 30)
    {
        return new(false, level, estimatedCapacity);
    }

    /// <summary>
    /// Represents a single validation result with efficient UTF-8 data access and optional string conversion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Data Access Performance:</strong>
    /// </para>
    /// <para>
    /// The Result structure provides direct access to UTF-8 validation data stored in the collector's
    /// internal buffers. This design minimizes string allocation overhead while providing convenient
    /// conversion methods for display and logging scenarios.
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>UTF-8 Properties:</strong> Direct span access with zero allocation overhead</description></item>
    /// <item><description><strong>String Properties:</strong> On-demand conversion using helper methods</description></item>
    /// <item><description><strong>Lifetime:</strong> Valid until the parent collector is disposed</description></item>
    /// <item><description><strong>Thread Safety:</strong> Read-only access is safe; parent collector is not thread-safe</description></item>
    /// </list>
    /// <para>
    /// <strong>Memory Efficiency:</strong>
    /// </para>
    /// <para>
    /// Result instances are lightweight value types that reference data in the collector's internal
    /// buffers. No additional allocations occur during result creation or enumeration, making this
    /// structure suitable for high-performance validation scenarios.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly struct Result
    {
        private readonly JsonSchemaResultsCollector _collector;

        private readonly ValueRange _evaluationLocation;

        private readonly ValueRange _schemaEvaluationLocation;

        private readonly ValueRange _documentEvaluationLocation;

        private readonly ValueRange _message;

        internal Result(
            JsonSchemaResultsCollector collector,
            bool isMatch,
            ValueRange evaluationLocation,
            ValueRange schemaEvaluationLocation,
            ValueRange documentEvaluationLocation,
            ValueRange message)
        {
            _collector = collector;
            IsMatch = isMatch;
            _evaluationLocation = evaluationLocation;
            _schemaEvaluationLocation = schemaEvaluationLocation;
            _documentEvaluationLocation = documentEvaluationLocation;
            _message = message;
        }

        /// <summary>
        /// Gets a value indicating whether the schema evaluation was a match.
        /// </summary>
        public bool IsMatch { get; }

        /// <summary>
        /// Gets the message for this result as a UTF-8 byte span.
        /// </summary>
        public ReadOnlySpan<byte> Message => _collector.GetResultString(_message);

        /// <summary>
        /// Gets the evaluation location for this result as a UTF-8 byte span.
        /// </summary>
        public ReadOnlySpan<byte> EvaluationLocation => _collector.GetResultString(_evaluationLocation);

        /// <summary>
        /// Gets the schema evaluation location for this result as a UTF-8 byte span.
        /// </summary>
        public ReadOnlySpan<byte> SchemaEvaluationLocation => _collector.GetResultString(_schemaEvaluationLocation);

        /// <summary>
        /// Gets the document evaluation location for this result as a UTF-8 byte span.
        /// </summary>
        public ReadOnlySpan<byte> DocumentEvaluationLocation => _collector.GetResultString(_documentEvaluationLocation);

        /// <summary>
        /// Gets the message for this result as a string.
        /// </summary>
        public string GetMessageText() => JsonReaderHelper.GetTextFromUtf8(Message);

        /// <summary>
        /// Gets the evaluation location for this result as a string.
        /// </summary>
        public string GetEvaluationLocationText() => JsonReaderHelper.GetTextFromUtf8(EvaluationLocation);

        /// <summary>
        /// Gets the schema evaluation location for this result as a string.
        /// </summary>
        public string GetSchemaEvaluationLocationText() => JsonReaderHelper.GetTextFromUtf8(SchemaEvaluationLocation);

        /// <summary>
        /// Gets the document evaluation location for this result as a string.
        /// </summary>
        public string GetDocumentEvaluationLocationText() => JsonReaderHelper.GetTextFromUtf8(DocumentEvaluationLocation);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => _collector is null ? "" : $"Match: {IsMatch} {JsonReaderHelper.GetTextFromUtf8(Message)}{(Message.Length > 0 ? " " : "")}({JsonReaderHelper.GetTextFromUtf8(EvaluationLocation)}, {JsonReaderHelper.GetTextFromUtf8(DocumentEvaluationLocation)}, {JsonReaderHelper.GetTextFromUtf8(SchemaEvaluationLocation)})";
    }

    /// <summary>
    /// Enumerates validation results with efficient memory access patterns and minimal allocation overhead.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Performance Characteristics:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Value Type:</strong> Lightweight enumerator with minimal allocation overhead</description></item>
    /// <item><description><strong>Forward-Only:</strong> Sequential access optimized for cache-friendly iteration</description></item>
    /// <item><description><strong>Direct Access:</strong> Results reference internal buffers without additional copies</description></item>
    /// <item><description><strong>Thread Safety:</strong> Not thread-safe; do not use concurrently with collection operations</description></item>
    /// </list>
    /// <para>
    /// <strong>Usage Patterns:</strong>
    /// </para>
    /// <para>
    /// The enumerator provides both <c>foreach</c> support and manual iteration capabilities.
    /// Results are stable during enumeration but become invalid when the parent collector is disposed.
    /// </para>
    /// </remarks>
    [DebuggerDisplay("{Current,nq}")]
    [CLSCompliant(false)]
    public struct ResultsEnumerator : IEnumerable<Result>, IEnumerator<Result>
    {
        private readonly JsonSchemaResultsCollector _collector;

        private int _endResultIdx; // end of the committed result stack range
        private int _curResultIdx; // the current index in the committed result stack range

        /// <summary>
        /// Creates an instance of a <see cref="ResultsEnumerator"/>.
        /// </summary>
        /// <param name="collector">The parent collector.</param>
        internal ResultsEnumerator(JsonSchemaResultsCollector collector)
        {
            _collector = collector;
            _curResultIdx = -1;
            _endResultIdx = collector._committedResultStack.Length;
        }

        /// <summary>
        /// Gets the current <see cref="Result"/> in the enumeration.
        /// </summary>
        public readonly Result Current
        {
            get
            {
                return _curResultIdx >= 0 && _curResultIdx < _endResultIdx ? _collector.ReadResult(_curResultIdx) : default;
            }
        }

        /// <inheritdoc />
        object IEnumerator.Current => Current;

        /// <inheritdoc />
        public void Dispose()
        {
            _endResultIdx = -1;
        }

        /// <inheritdoc />
        public void Reset()
        {
            _curResultIdx = -1;
        }

        /// <inheritdoc />
        public bool MoveNext()
        {
            _curResultIdx++;

            if (_curResultIdx >= _endResultIdx)
            {
                // We have reached the end of the results
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public IEnumerator<Result> GetEnumerator() => this;

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Enumerates validation results in collection order with efficient memory access patterns.
    /// </summary>
    /// <returns>
    /// A <see cref="ResultsEnumerator"/> that provides forward-only iteration over committed validation results.
    /// The enumerator is a value type optimized for minimal allocation overhead.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Enumeration Characteristics:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Order:</strong> Results are returned in the order they were committed to the collector</description></item>
    /// <item><description><strong>Stability:</strong> Result order and content remain stable until collector disposal</description></item>
    /// <item><description><strong>Performance:</strong> O(1) per result access with minimal memory allocation</description></item>
    /// <item><description><strong>Thread Safety:</strong> Not thread-safe; do not enumerate concurrently with result collection</description></item>
    /// </list>
    /// <para>
    /// <strong>Memory Access Pattern:</strong>
    /// </para>
    /// <para>
    /// The enumerator provides direct access to UTF-8 result data stored in internal buffers.
    /// Result strings are accessed as ReadOnlySpan&lt;byte&gt; for optimal performance, with helper
    /// methods available for string conversion when needed.
    /// </para>
    /// <para>
    /// <strong>Usage Guidelines:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Performance Critical:</strong> Use UTF-8 span accessors (e.g., <see cref="Result.Message"/>) when possible</description></item>
    /// <item><description><strong>Convenience:</strong> Use string accessors (e.g., <see cref="Result.GetMessageText"/>) for display purposes</description></item>
    /// <item><description><strong>Filtering:</strong> Check <see cref="Result.IsMatch"/> early to optimize result processing</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para>High-performance result processing:</para>
    /// <code>
    /// var enumerator = collector.EnumerateResults();

    /// while (enumerator.MoveNext())
    /// {
    /// var result = enumerator.Current;

    /// if (!result.IsMatch)
    /// {
    /// // Direct UTF-8 processing for optimal performance
    /// ProcessValidationFailure(
    /// result.Message,
    /// result.DocumentEvaluationLocation,
    /// result.SchemaEvaluationLocation);

    /// }

    /// }

    /// </code>
    /// </example>
    /// <example>
    /// <para>User-friendly result display:</para>
    /// <code>
    /// foreach (var result in collector.EnumerateResults())
    /// {
    /// Console.WriteLine($"Match: {result.IsMatch}");

    /// Console.WriteLine($"Message: {result.GetMessageText()}");

    /// Console.WriteLine($"Document Path: {result.GetDocumentEvaluationLocationText()}");

    /// Console.WriteLine($"Schema Path: {result.GetSchemaEvaluationLocationText()}");

    /// Console.WriteLine();

    /// }

    /// </code>
    /// </example>
    [CLSCompliant(false)]
    public ResultsEnumerator EnumerateResults()
    {
        return new(this);
    }

    /// <summary>
    /// Gets the total count of committed validation results with O(1) performance.
    /// </summary>
    /// <returns>
    /// The number of validation results that have been committed and are available for enumeration.
    /// This count includes both successful and failed validation results based on the configured verbosity level.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <strong>Result Counting by Verbosity Level:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Basic:</strong> Counts failure results only</description></item>
    /// <item><description><strong>Detailed:</strong> Counts failure results with enhanced context</description></item>
    /// <item><description><strong>Verbose:</strong> Counts all validation events (successes and failures)</description></item>
    /// </list>
    /// <para>
    /// <strong>Performance:</strong>
    /// </para>
    /// <para>
    /// This operation is O(1) as it returns the length of the committed results stack without enumeration.
    /// Use this method to determine if enumeration is necessary or to pre-size result processing structures.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// int resultCount = collector.GetResultCount();

    /// if (resultCount > 0)
    /// {
    /// Console.WriteLine($"Validation produced {resultCount} results");

    ///
    /// // Pre-size collections if needed
    /// var issues = new List&lt;ValidationIssue&gt;(resultCount);

    ///
    /// foreach (var result in collector.EnumerateResults())
    /// {
    /// issues.Add(ConvertToIssue(result));

    /// }

    /// }

    /// else
    /// {
    /// Console.WriteLine("Validation completed successfully with no issues");

    /// }

    /// </code>
    /// </example>
    public int GetResultCount()
    {
        return _committedResultStack.Length;
    }

    /// <summary>
    /// Releases all resources and returns the collector to the pool if rented, ensuring proper cleanup of sensitive data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Resource Cleanup Process:</strong>
    /// </para>
    /// <list type="number">
    /// <item><description><strong>Pooled Instances:</strong> Resets state and returns to thread-local cache for reuse</description></item>
    /// <item><description><strong>Non-pooled Instances:</strong> Clears sensitive data and returns buffers to ArrayPool</description></item>
    /// <item><description><strong>Security:</strong> All internal buffers containing validation data are explicitly cleared</description></item>
    /// <item><description><strong>Stack Cleanup:</strong> Internal stacks are disposed and their resources released</description></item>
    /// </list>
    /// <para>
    /// <strong>Security Considerations:</strong>
    /// </para>
    /// <para>
    /// The disposal process explicitly clears all internal buffers that may contain sensitive validation
    /// data, including validation messages, document paths, and schema paths. This ensures that sensitive
    /// information does not persist in memory after validation completion.
    /// </para>
    /// <para>
    /// <strong>Performance Impact:</strong>
    /// </para>
    /// <para>
    /// Disposal is designed to be efficient, with O(1) pool return for rented instances and O(n) buffer
    /// clearing for non-pooled instances where n is the total buffer usage. The explicit clearing overhead
    /// is necessary for security but minimal in typical usage scenarios.
    /// </para>
    /// <para>
    /// <strong>Important:</strong>
    /// </para>
    /// <para>
    /// Always dispose collector instances to ensure proper resource management. Failure to dispose
    /// rented instances can lead to pool exhaustion, while failure to dispose non-pooled instances
    /// can lead to memory leaks and security concerns.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Recommended using pattern
    /// using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic)
    ///
    /// // Validation operations...
    ///
    /// // Automatic disposal ensures proper cleanup
    /// </code>
    /// </example>
    /// <example>
    /// <code>
    /// // Manual disposal when using pattern is not available
    /// var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
    /// try
    /// {
    /// // Validation operations...
    /// }
    /// finally
    /// {
    /// collector.Dispose(); // Essential for proper resource management
    /// }
    /// </code>
    /// </example>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_rented)
        {
            JsonSchemaResultsCollectorCache.ReturnResultsCollector(this);
        }
        else
        {
            // We need to clear all of these buffers as they contain sensitive data
            // The entire buffer needs to be cleared, because we do not track the
            // maximum length we have used as we push and pop path elements
            ArrayPool<byte>.Shared.Return(_documentEvaluationPath, true);
            ArrayPool<byte>.Shared.Return(_schemaEvaluationPath, true);
            ArrayPool<byte>.Shared.Return(_evaluationPath, true);
            if (_utf8StringBacking.Length > 0)
            {
                // We only need to clear the length we have used because this
                // is a grow-only buffer
                _utf8StringBacking.AsSpan(0, _utf8StringBacking.Length).Clear();
                ArrayPool<byte>.Shared.Return(_utf8StringBacking);
            }

            _evaluationPathStack.Dispose();
            _documentEvaluationPathStack.Dispose();
            _schemaEvaluationPathStack.Dispose();

            _resultStack.Dispose();
            _committedResultStack.Dispose();
        }
    }

    internal void Reset(JsonSchemaResultsLevel level, int estimatedCapacity)
    {
        _isDisposed = false;

        int pathCapacity = estimatedCapacity * BytesPerPathSegment; // we will assume 30 characters per path segment

        if (_documentEvaluationPath.Length < pathCapacity)
        {
            // We need to clear the buffer as it contain sensitive data
            // The entire buffer needs to be cleared, because we do not track the
            // maximum length we have used as we push and pop path elements
            ArrayPool<byte>.Shared.Return(_documentEvaluationPath, true);
            _documentEvaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity);
        }

        if (_schemaEvaluationPath.Length < pathCapacity)
        {
            // We need to clear the buffer as it contain sensitive data
            // The entire buffer needs to be cleared, because we do not track the
            // maximum length we have used as we push and pop path elements
            ArrayPool<byte>.Shared.Return(_schemaEvaluationPath, true);
            _schemaEvaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity);
        }

        if (_evaluationPath.Length < pathCapacity)
        {
            // We need to clear the buffer as it contain sensitive data
            // The entire buffer needs to be cleared, because we do not track the
            // maximum length we have used as we push and pop path elements
            ArrayPool<byte>.Shared.Return(_evaluationPath, true);
            _evaluationPath = ArrayPool<byte>.Shared.Rent(pathCapacity);
        }

        _currentEvaluationPathRange = default;
        _currentSchemaEvaluationPathRange = default;
        _currentDocumentEvaluationPathRange = default;

        _level = level;
        int messageCapacity;
        if (level == JsonSchemaResultsLevel.Basic)
        {
            messageCapacity = pathCapacity * 3;
        }
        else
        {
            messageCapacity = estimatedCapacity * BytesPerMessage + (pathCapacity * 3);
        }

        if (_utf8StringBacking.Length < messageCapacity)
        {
            // We only need to clear the length we have used because this
            // is a grow-only buffer
            _utf8StringBacking.AsSpan(0, _utf8StringBacking.Length).Clear();
            ArrayPool<byte>.Shared.Return(_utf8StringBacking);
            _utf8StringBacking = ArrayPool<byte>.Shared.Rent(messageCapacity);
        }

        _utf8StringBackingLength = 0;

        // And reset the stacks
        _evaluationPathStack.Length = 0;
        _documentEvaluationPathStack.Length = 0;
        _schemaEvaluationPathStack.Length = 0;
        _resultStack.Length = 0;
        _committedResultStack.Length = 0;
        _sequenceNumber = 0;
    }

    internal void ResetAllStateForCacheReuse()
    {
        _currentDocumentEvaluationPathRange = default;
        _currentSchemaEvaluationPathRange = default;
        _currentDocumentEvaluationPathRange = default;

        _utf8StringBackingLength = 0;
        _evaluationPathStack.Length = 0;
        _documentEvaluationPathStack.Length = 0;
        _schemaEvaluationPathStack.Length = 0;
        _resultStack.Length = 0;
        _committedResultStack.Length = 0;
        _sequenceNumber = 0;
    }

    internal static JsonSchemaResultsCollector CreateEmptyInstanceForCaching() => new(true, JsonSchemaResultsLevel.Basic, 30);

    /*
     * Results come as a block of 4 consecutive strings in the _utf8StringBacking array
     * The first string is the result message, the second is the evaluation path,
     * the third is the document evaluation path, and the fourth is the schema evaluation path.
     */

    private void WriteResult(bool match, JsonSchemaMessageProvider? messageProvider)
    {
        Debug.Assert(_resultStack.Length != 0, "No parent context.");

        bool writeMessage = EnsureCapacityForResult(match);
        int written = 0;

        // We only write out if we both have a message provider *and* we are
        // expecting to write a message given the level settings
        if (messageProvider is not null && writeMessage)
        {
            messageProvider(_utf8StringBacking.AsSpan(_utf8StringBackingLength + ResultHeaderSize), out written);
        }

        WriteHeaderAndPathsAndUpdateResultStack(match, written);
    }

    private void WriteResult<TProviderContext>(bool match, TProviderContext context, JsonSchemaMessageProvider<TProviderContext>? messageProvider)
    {
        Debug.Assert(_resultStack.Length != 0, "No parent context.");

        bool writeMessage = EnsureCapacityForResult(match);
        int written = 0;

        // We only write out if we both have a message provider *and* we are
        // expecting to write a message given the level settings
        if (messageProvider is not null && writeMessage)
        {
            if (!messageProvider(context, _utf8StringBacking.AsSpan(_utf8StringBackingLength + ResultHeaderSize), out written))
            {
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            }
        }

        WriteHeaderAndPathsAndUpdateResultStack(match, written);
    }

    private void WriteHeaderAndPathsAndUpdateResultStack(bool match, int written)
    {
        // We save the top nybble for the match 0b0010 is match true, 0b0001 is match false
        // 0b0000 is reserved for the result path strings
        // This means we have this many bytes free for a message.
        if (written > 0x7000_0000)
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        int writtenAndMatch = written | (match ? 0x2000_0000 : 0x1000_0000);

        int start = _utf8StringBackingLength;

        BitConverter.TryWriteBytes(_utf8StringBacking.AsSpan(_utf8StringBackingLength, ResultHeaderSize), writtenAndMatch);
        _utf8StringBackingLength += written + ResultHeaderSize;

        // Finally, write the paths to the results - first the header containing the length,
        // then the rest of the string
        BitConverter.TryWriteBytes(_utf8StringBacking.AsSpan(_utf8StringBackingLength, ResultHeaderSize), _currentEvaluationPathRange.Length);
        _evaluationPath.AsSpan(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.Length)
            .CopyTo(_utf8StringBacking.AsSpan(_utf8StringBackingLength + ResultHeaderSize));
        _utf8StringBackingLength += _currentEvaluationPathRange.Length + ResultHeaderSize;

        BitConverter.TryWriteBytes(_utf8StringBacking.AsSpan(_utf8StringBackingLength, ResultHeaderSize), _currentDocumentEvaluationPathRange.Length);
        _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.Length)
            .CopyTo(_utf8StringBacking.AsSpan(_utf8StringBackingLength + ResultHeaderSize));
        _utf8StringBackingLength += _currentDocumentEvaluationPathRange.Length + ResultHeaderSize;

        BitConverter.TryWriteBytes(_utf8StringBacking.AsSpan(_utf8StringBackingLength, ResultHeaderSize), _currentSchemaEvaluationPathRange.Length);
        _schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.Start, _currentSchemaEvaluationPathRange.Length)
            .CopyTo(_utf8StringBacking.AsSpan(_utf8StringBackingLength + ResultHeaderSize));
        _utf8StringBackingLength += _currentSchemaEvaluationPathRange.Length + ResultHeaderSize;

        ValueRangeWithCommitIndexAndSequenceNumber range = _resultStack.Peek();
        _resultStack.Append(new ValueRangeWithCommitIndexAndSequenceNumber(start, _utf8StringBackingLength, range.CommitIndex, _sequenceNumber));
    }

    internal Result ReadResult(int resultIndex)
    {
        Debug.Assert(resultIndex >= 0 && resultIndex < _committedResultStack.Length, "Invalid result index.");
        ValueRange range = _committedResultStack[resultIndex];
        Debug.Assert(range.Length > 0, "Result range must have a positive length.");

        int curIndex = range.Start;

        // First, read the initial header
        int header = BitConverter.ToInt32(_utf8StringBacking, curIndex);
        bool isMatch = (header & 0x3000_0000) == 0x2000_0000; // 0b0010 is match true, 0b0001 is match false
        int length = header & 0x0FFF_FFFF;

        ValueRange messageRange = new(curIndex + 4, curIndex + 4 + length);
        curIndex += 4 + length;

        header = BitConverter.ToInt32(_utf8StringBacking, curIndex);
        length = header & 0x0FFF_FFFF;
        ValueRange evaluationLocationRange = new(curIndex + 4, curIndex + 4 + length);
        curIndex += 4 + length;

        header = BitConverter.ToInt32(_utf8StringBacking, curIndex);
        length = header & 0x0FFF_FFFF;
        ValueRange documentEvaluationLocationRange = new(curIndex + 4, curIndex + 4 + length);
        curIndex += 4 + length;

        header = BitConverter.ToInt32(_utf8StringBacking, curIndex);
        length = header & 0x0FFF_FFFF;
        ValueRange schemaEvaluationLocationRange = new(curIndex + 4, curIndex + 4 + length);

        return new(this, isMatch, evaluationLocationRange, schemaEvaluationLocationRange, documentEvaluationLocationRange, messageRange);
    }

    private ReadOnlySpan<byte> GetResultString(ValueRange message) => message.Start >= 0 && message.Length < _utf8StringBacking.Length ? _utf8StringBacking.AsSpan(message.Start, message.Length) : default;

    private bool EnsureCapacityForResult(bool match)
    {
        int messageLength = 0;
        if (_level == JsonSchemaResultsLevel.Verbose || (!match && _level >= JsonSchemaResultsLevel.Detailed))
        {
            // we are only writing messages if we are either verbose, or detailed and we have a match.
            messageLength = MaxMessageLength;
        }

        // 32 = metadata for 4 strings [the result with message + the 3 path strings]
        // Then we need a max message length plus the actual path lengths
        int totalLength = _utf8StringBackingLength + messageLength + 32 + _currentEvaluationPathRange.Length + _currentDocumentEvaluationPathRange.Length + _currentSchemaEvaluationPathRange.Length;

        if (_utf8StringBacking.Length < totalLength)
        {
            Enlarge(totalLength, ref _utf8StringBacking, _utf8StringBackingLength);
        }

        return messageLength > 0;
    }

    private static void Enlarge(int additionalLength, ref byte[] backing, int usedLength)
    {
        byte[] toReturn = backing;

        // Allow the data to grow up to maximum possible capacity (~2G bytes) before encountering overflow.
        // Note: Array.MaxLength exists only on .NET 6 or greater,
        // so for the other versions value is hardcoded
        const int MaxArrayLength = 0x7FFFFFC7;
#if NET
        Debug.Assert(MaxArrayLength == Array.MaxLength);
#endif

        // Double the base length and add the additional capacity
        int newCapacity = (toReturn.Length * 2) + additionalLength;

        // Note that this check works even when newCapacity overflowed thanks to the (uint) cast
        if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;

        // If the maximum capacity has already been reached,
        // then set the new capacity to be larger than what is possible
        // so that ArrayPool.Rent throws an OutOfMemoryException for us.
        if (newCapacity == toReturn.Length) newCapacity = int.MaxValue;

        backing = ArrayPool<byte>.Shared.Rent(newCapacity);

        if (toReturn.Length > 0)
        {
            Buffer.BlockCopy(toReturn, 0, backing, 0, toReturn.Length);

            // This could be security sensitive, so we clear the array
            toReturn.AsSpan(0, usedLength).Clear();
            ArrayPool<byte>.Shared.Return(toReturn);
        }
    }

    private void AppendToEvaluationPath(JsonSchemaPathProvider path)
    {
        if (_currentEvaluationPathRange.End + MaxPathSegmentLength > _evaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _evaluationPath, _currentEvaluationPathRange.End);
        }

        _evaluationPath[_currentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(_evaluationPath.AsSpan(_currentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.End + written + 1);
    }

    private void AppendParallelEvaluationPath(int sequenceOffset, JsonSchemaPathProvider path)
    {
        ValueRange parentRange = _evaluationPathStack[^(sequenceOffset + 1)];
        if (_currentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _evaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _evaluationPath, _currentEvaluationPathRange.End);
        }

        _evaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_evaluationPath.AsSpan(_currentEvaluationPathRange.End));

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.End, _currentEvaluationPathRange.End + parentRange.Length);

        _evaluationPath[_currentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(_evaluationPath.AsSpan(_currentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.End + written + 1);
    }

    private void AppendToEvaluationPath<T>(T context, JsonSchemaPathProvider<T> path)
    {
        if (_currentEvaluationPathRange.End + MaxPathSegmentLength > _evaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _evaluationPath, _currentEvaluationPathRange.End);
        }

        _evaluationPath[_currentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(context, _evaluationPath.AsSpan(_currentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.End + written + 1);
    }

    private void AppendParallelEvaluationPath<T>(int sequenceOffset, T context, JsonSchemaPathProvider<T> path)
    {
        ValueRange parentRange = _evaluationPathStack[^(sequenceOffset + 1)];
        if (_currentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _evaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _evaluationPath, _currentEvaluationPathRange.End);
        }

        _evaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_evaluationPath.AsSpan(_currentEvaluationPathRange.End));

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.End, _currentEvaluationPathRange.End + parentRange.Length);

        _evaluationPath[_currentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(context, _evaluationPath.AsSpan(_currentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.End + written + 1);
    }

    private void AppendToSchemaEvaluationPath(JsonSchemaPathProvider path)
    {
        if (_currentSchemaEvaluationPathRange.End + MaxPathSegmentLength > _schemaEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _schemaEvaluationPath, _currentSchemaEvaluationPathRange.End);
        }

        _schemaEvaluationPath[_currentSchemaEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(_schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentSchemaEvaluationPathRange = new ValueRange(_currentSchemaEvaluationPathRange.Start, _currentSchemaEvaluationPathRange.End + written + 1);
    }

    private void AppendToSchemaEvaluationPath<T>(T context, JsonSchemaPathProvider<T> path)
    {
        if (_currentSchemaEvaluationPathRange.End + MaxPathSegmentLength > _schemaEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _schemaEvaluationPath, _currentSchemaEvaluationPathRange.End);
        }

        _schemaEvaluationPath[_currentSchemaEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(context, _schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentSchemaEvaluationPathRange = new ValueRange(_currentSchemaEvaluationPathRange.Start, _currentSchemaEvaluationPathRange.End + written + 1);
    }

    private void AppendToDocumentEvaluationPath(JsonSchemaPathProvider path)
    {
        if (_currentDocumentEvaluationPathRange.End + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void AppendParallelDocumentEvaluationPath(int sequenceOffset, JsonSchemaPathProvider path)
    {
        ValueRange parentRange = _documentEvaluationPathStack[^(sequenceOffset + 1)];
        if (_currentDocumentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End));

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.End, _currentDocumentEvaluationPathRange.End + parentRange.Length);

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void AppendToDocumentEvaluationPath<T>(T context, JsonSchemaPathProvider<T> path)
    {
        if (_currentDocumentEvaluationPathRange.End + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(context, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void AppendParallelDocumentEvaluationPath<T>(int sequenceOffset, T context, JsonSchemaPathProvider<T> path)
    {
        ValueRange parentRange = _documentEvaluationPathStack[^(sequenceOffset + 1)];
        if (_currentDocumentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End));

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.End, _currentDocumentEvaluationPathRange.End + parentRange.Length);

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!path(context, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void AppendToDocumentEvaluationPath(int index)
    {
        if (_currentDocumentEvaluationPathRange.End + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!Utf8Formatter.TryFormat(index, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void AppendParallelDocumentEvaluationPath(int sequenceOffset, int index)
    {
        ValueRange parentRange = _documentEvaluationPathStack[^(sequenceOffset + 1)];
        if (_currentDocumentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End));

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.End, _currentDocumentEvaluationPathRange.End + parentRange.Length);

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!Utf8Formatter.TryFormat(index, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void UnescapeEncodeAndAppendToDocumentEvaluationPath(ReadOnlySpan<byte> escapedAndUnencodedPropertyName)
    {
        int length = (escapedAndUnencodedPropertyName.Length * JsonConstants.MaxExpansionFactorWhileEncodingPointer);

        if (_currentDocumentEvaluationPathRange.End + length > _documentEvaluationPath.Length)
        {
            Enlarge(length, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryUnescapeAndEncodePointer(escapedAndUnencodedPropertyName, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void UnescapeEncodeAndAppendParallelDocumentEvaluationPath(int sequenceOffset, ReadOnlySpan<byte> escapedAndUnencodedPropertyName)
    {
        ValueRange parentRange = _documentEvaluationPathStack[^(sequenceOffset + 1)];
        if (_currentDocumentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End));

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.End, _currentDocumentEvaluationPathRange.End + parentRange.Length);

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryUnescapeAndEncodePointer(escapedAndUnencodedPropertyName, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void EncodeAndAppendToDocumentEvaluationPath(ReadOnlySpan<byte> unencodedPropertyName)
    {
        int length = (unencodedPropertyName.Length * JsonConstants.MaxExpansionFactorWhileEncodingPointer);

        if (_currentDocumentEvaluationPathRange.End + length > _documentEvaluationPath.Length)
        {
            Enlarge(length, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryEncodePointer(unencodedPropertyName, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void EncodeAndAppendParallelDocumentEvaluationPath(int sequenceOffset, ReadOnlySpan<byte> unencodedPropertyName)
    {
        ValueRange parentRange = _documentEvaluationPathStack[^(sequenceOffset + 1)];
        if (_currentDocumentEvaluationPathRange.End + parentRange.Length + MaxPathSegmentLength > _documentEvaluationPath.Length)
        {
            Enlarge(parentRange.Length + MaxPathSegmentLength, ref _documentEvaluationPath, _currentDocumentEvaluationPathRange.End);
        }

        _documentEvaluationPath.AsSpan(parentRange.Start, parentRange.Length).CopyTo(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End));

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.End, _currentDocumentEvaluationPathRange.End + parentRange.Length);

        _documentEvaluationPath[_currentDocumentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryEncodePointer(unencodedPropertyName, _documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentDocumentEvaluationPathRange = new ValueRange(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.End + written + 1);
    }

    private void EncodeAndAppendToSchemaEvaluationPath(ReadOnlySpan<byte> unencodedPropertyName)
    {
        int length = (unencodedPropertyName.Length * JsonConstants.MaxExpansionFactorWhileEncodingPointer);

        if (_currentSchemaEvaluationPathRange.End + length > _schemaEvaluationPath.Length)
        {
            Enlarge(length, ref _schemaEvaluationPath, _currentSchemaEvaluationPathRange.End);
        }

        _schemaEvaluationPath[_currentSchemaEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryEncodePointer(unencodedPropertyName, _schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentSchemaEvaluationPathRange = new ValueRange(_currentSchemaEvaluationPathRange.Start, _currentSchemaEvaluationPathRange.End + written + 1);
    }

    private void EncodeAndAppendToEvaluationPath(ReadOnlySpan<byte> unencodedPropertyName)
    {
        int length = (unencodedPropertyName.Length * JsonConstants.MaxExpansionFactorWhileEncodingPointer);

        if (_currentEvaluationPathRange.End + length > _evaluationPath.Length)
        {
            Enlarge(length, ref _evaluationPath, _currentEvaluationPathRange.End);
        }

        _evaluationPath[_currentEvaluationPathRange.End] = JsonConstants.Slash; // Ensure we start with a slash

        if (!JsonReaderHelper.TryEncodePointer(unencodedPropertyName, _evaluationPath.AsSpan(_currentEvaluationPathRange.End + 1), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentEvaluationPathRange = new ValueRange(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.End + written + 1);
    }

    int IJsonSchemaResultsCollector.BeginChildContext(int parentSequenceNumber, JsonSchemaPathProvider? evaluationPath, JsonSchemaPathProvider? schemaEvaluationPath, JsonSchemaPathProvider? documentEvaluationPath)
    {
        IsConsistent(_sequenceNumber);

        // Push the paths onto the stack
        _evaluationPathStack.Append(_currentEvaluationPathRange);
        _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);
        _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

        int sequenceOffset = _sequenceNumber - parentSequenceNumber;

        if (evaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelEvaluationPath(sequenceOffset, evaluationPath);
            }
            else
            {
                AppendToEvaluationPath(evaluationPath);
            }
        }

        if (schemaEvaluationPath is not null)
        {
            SetSchemaEvaluationPath(schemaEvaluationPath);
        }

        if (documentEvaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelDocumentEvaluationPath(sequenceOffset, documentEvaluationPath);
            }
            else
            {
                AppendToDocumentEvaluationPath(documentEvaluationPath);
            }
        }

        // There are no current results for this context (hence our result stack has 0 length)
        // But we also record the committed result stack at this point, in case we wish to pop and unwind it later.
        _resultStack.Append(
            new ValueRangeWithCommitIndexAndSequenceNumber(_utf8StringBackingLength, _utf8StringBackingLength, _committedResultStack.Length, _sequenceNumber));

        _sequenceNumber++;
        return _sequenceNumber;
    }

    int IJsonSchemaResultsCollector.BeginChildContext<TProviderContext>(int parentSequenceNumber, TProviderContext providerContext, JsonSchemaPathProvider<TProviderContext>? evaluationPath, JsonSchemaPathProvider<TProviderContext>? schemaEvaluationPath, JsonSchemaPathProvider<TProviderContext>? documentEvaluationPath)
    {
        IsConsistent(_sequenceNumber);

        // Push the paths onto the stack
        _evaluationPathStack.Append(_currentEvaluationPathRange);
        _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);
        _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

        int sequenceOffset = _sequenceNumber - parentSequenceNumber;

        if (evaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelEvaluationPath(sequenceOffset, providerContext, evaluationPath);
            }
            else
            {
                AppendToEvaluationPath(providerContext, evaluationPath);
            }
        }

        if (schemaEvaluationPath is not null)
        {
            SetSchemaEvaluationPath(providerContext, schemaEvaluationPath);
        }

        if (documentEvaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelDocumentEvaluationPath(sequenceOffset, providerContext, documentEvaluationPath);
            }
            else
            {
                AppendToDocumentEvaluationPath(providerContext, documentEvaluationPath);
            }
        }

        // There are no current results for this context (hence our result stack has 0 length)
        // But we also record the committed result stack at this point, in case we wish to pop and unwind it later.
        _resultStack.Append(
            new ValueRangeWithCommitIndexAndSequenceNumber(_utf8StringBackingLength, _utf8StringBackingLength, _committedResultStack.Length, _sequenceNumber));

        _sequenceNumber++;
        return _sequenceNumber;
    }

    int IJsonSchemaResultsCollector.BeginChildContext(int parentSequenceNumber, int itemIndex, JsonSchemaPathProvider? evaluationPath, JsonSchemaPathProvider? schemaEvaluationPath)
    {
        IsConsistent(_sequenceNumber);

        // Push the paths onto the stack
        _evaluationPathStack.Append(_currentEvaluationPathRange);
        _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);
        _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

        int sequenceOffset = _sequenceNumber - parentSequenceNumber;

        if (evaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelEvaluationPath(sequenceOffset, evaluationPath);
            }
            else
            {
                AppendToEvaluationPath(evaluationPath);
            }
        }

        if (schemaEvaluationPath is not null)
        {
            SetSchemaEvaluationPath(schemaEvaluationPath);
        }

        if (sequenceOffset > 0)
        {
            AppendParallelDocumentEvaluationPath(sequenceOffset, itemIndex);
        }
        else
        {
            AppendToDocumentEvaluationPath(itemIndex);
        }

        // There are no current results for this context (hence our result stack has 0 length)
        // But we also record the committed result stack at this point, in case we wish to pop and unwind it later.
        _resultStack.Append(
            new ValueRangeWithCommitIndexAndSequenceNumber(_utf8StringBackingLength, _utf8StringBackingLength, _committedResultStack.Length, _sequenceNumber));

        _sequenceNumber++;
        return _sequenceNumber;
    }

    int IJsonSchemaResultsCollector.BeginChildContext(int parentSequenceNumber, ReadOnlySpan<byte> escapedPropertyName, JsonSchemaPathProvider? evaluationPath, JsonSchemaPathProvider? schemaEvaluationPath)
    {
        IsConsistent(_sequenceNumber);

        // Push the paths onto the stack
        _evaluationPathStack.Append(_currentEvaluationPathRange);
        _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);
        _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

        int sequenceOffset = _sequenceNumber - parentSequenceNumber;

        if (evaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelEvaluationPath(sequenceOffset, evaluationPath);
            }
            else
            {
                AppendToEvaluationPath(evaluationPath);
            }
        }

        if (schemaEvaluationPath is not null)
        {
            SetSchemaEvaluationPath(schemaEvaluationPath);
        }

        if (sequenceOffset > 0)
        {
            UnescapeEncodeAndAppendParallelDocumentEvaluationPath(sequenceOffset, escapedPropertyName);
        }
        else
        {
            UnescapeEncodeAndAppendToDocumentEvaluationPath(escapedPropertyName);
        }

        // There are no current results for this context (hence our result stack has 0 length)
        // But we also record the committed result stack at this point, in case we wish to pop and unwind it later.
        _resultStack.Append(
            new ValueRangeWithCommitIndexAndSequenceNumber(_utf8StringBackingLength, _utf8StringBackingLength, _committedResultStack.Length, _sequenceNumber));

        _sequenceNumber++;
        return _sequenceNumber;
    }

    int IJsonSchemaResultsCollector.BeginChildContextUnescaped(int parentSequenceNumber, ReadOnlySpan<byte> unescapedPropertyName, JsonSchemaPathProvider? evaluationPath, JsonSchemaPathProvider? schemaEvaluationPath)
    {
        IsConsistent(_sequenceNumber);

        // Push the paths onto the stack
        _evaluationPathStack.Append(_currentEvaluationPathRange);
        _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);
        _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

        int sequenceOffset = _sequenceNumber - parentSequenceNumber;

        if (evaluationPath is not null)
        {
            if (sequenceOffset > 0)
            {
                AppendParallelEvaluationPath(sequenceOffset, evaluationPath);
            }
            else
            {
                AppendToEvaluationPath(evaluationPath);
            }
        }

        if (schemaEvaluationPath is not null)
        {
            SetSchemaEvaluationPath(schemaEvaluationPath);
        }

        if (sequenceOffset > 0)
        {
            EncodeAndAppendParallelDocumentEvaluationPath(sequenceOffset, unescapedPropertyName);
        }
        else
        {
            EncodeAndAppendToDocumentEvaluationPath(unescapedPropertyName);
        }

        // There are no current results for this context (hence our result stack has 0 length)
        // But we also record the committed result stack at this point, in case we wish to pop and unwind it later.
        _resultStack.Append(
            new ValueRangeWithCommitIndexAndSequenceNumber(_utf8StringBackingLength, _utf8StringBackingLength, _committedResultStack.Length, _sequenceNumber));

        _sequenceNumber++;
        return _sequenceNumber;
    }

    void IJsonSchemaResultsCollector.CommitChildContext(int sequenceNumber, bool parentIsMatch, bool childIsMatch, JsonSchemaMessageProvider? messageProvider)
    {
        IsConsistent(sequenceNumber);

        if (parentIsMatch && _level != JsonSchemaResultsLevel.Verbose)
        {
            PopChildContextUnsafe();
            return;
        }

        if (!parentIsMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            WriteResult(childIsMatch, messageProvider);
        }

        CommitCurrentResults();

        // Pop the paths off the stack
        _currentEvaluationPathRange = _evaluationPathStack.Pop();
        _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        _currentDocumentEvaluationPathRange = _documentEvaluationPathStack.Pop();
        _sequenceNumber--;
    }

    void IJsonSchemaResultsCollector.CommitChildContext<TProviderContext>(int sequenceNumber, bool parentIsMatch, bool childIsMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider)
    {
        IsConsistent(sequenceNumber);

        if (parentIsMatch && _level != JsonSchemaResultsLevel.Verbose)
        {
            PopChildContextUnsafe();
            return;
        }

        if (!parentIsMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            WriteResult(childIsMatch, providerContext, messageProvider);
        }

        CommitCurrentResults();

        // Pop the paths off the stack
        _currentEvaluationPathRange = _evaluationPathStack.Pop();
        _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        _currentDocumentEvaluationPathRange = _documentEvaluationPathStack.Pop();
        _sequenceNumber--;
    }

    private void CommitCurrentResults()
    {
        while (_resultStack.Peek().SequenceNumber == _sequenceNumber)
        {
            // Also, pop the results off the stack
            ValueRangeWithCommitIndexAndSequenceNumber range = _resultStack.Pop();
            if (range.Length > 0)
            {
                _committedResultStack.Append(new ValueRange(range.Start, range.End));
            }
        }
    }

    void IJsonSchemaResultsCollector.PopChildContext(int sequenceNumber)
    {
        IsConsistent(sequenceNumber);

        PopChildContextUnsafe();
    }

    private void PopChildContextUnsafe()
    {
        // Pop the paths off the stack
        _currentEvaluationPathRange = _evaluationPathStack.Pop();
        _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        _currentDocumentEvaluationPathRange = _documentEvaluationPathStack.Pop();

        // Also, pop the results off the stack
        // There must be at least one.
        ValueRangeWithCommitIndexAndSequenceNumber range = default;
        while (_resultStack.Peek().SequenceNumber == _sequenceNumber)
        {
            range = _resultStack.Pop();
        }

        // And ensure we roll back any commits
        _committedResultStack.Length = range.CommitIndex;
        _utf8StringBackingLength = range.Start;
        _sequenceNumber--;
    }

    void IJsonSchemaResultsCollector.IgnoredKeyword(JsonSchemaMessageProvider? messageProvider, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (_level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);

            WriteResult(match: true, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.IgnoredKeyword<TProviderContext>(TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (_level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);

            WriteResult(match: true, context: providerContext, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeyword(bool isMatch, JsonSchemaMessageProvider? messageProvider, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);
            EncodeAndAppendToSchemaEvaluationPath(unescapedKeyword);

            WriteResult(match: isMatch, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeyword<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);
            EncodeAndAppendToSchemaEvaluationPath(unescapedKeyword);

            WriteResult(match: isMatch, context: providerContext, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeywordPath(bool isMatch, JsonSchemaMessageProvider messageProvider, JsonSchemaPathProvider keywordPath)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            AppendToEvaluationPath(keywordPath);
            AppendToSchemaEvaluationPath(keywordPath);

            WriteResult(match: isMatch, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeywordPath<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, JsonSchemaPathProvider<TProviderContext> keywordPath)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);

            AppendToEvaluationPath(providerContext, keywordPath);
            AppendToSchemaEvaluationPath(providerContext, keywordPath);

            WriteResult(match: isMatch, context: providerContext, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeywordForProperty(bool isMatch, JsonSchemaMessageProvider? messageProvider, ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);
            _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);
            EncodeAndAppendToSchemaEvaluationPath(unescapedKeyword);
            EncodeAndAppendToDocumentEvaluationPath(propertyName);

            WriteResult(match: isMatch, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
            _currentDocumentEvaluationPathRange = _documentEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedKeywordForProperty<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider, ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> unescapedKeyword)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            _evaluationPathStack.Append(_currentEvaluationPathRange);
            _schemaEvaluationPathStack.Append(_currentSchemaEvaluationPathRange);
            _documentEvaluationPathStack.Append(_currentDocumentEvaluationPathRange);

            EncodeAndAppendToEvaluationPath(unescapedKeyword);
            EncodeAndAppendToSchemaEvaluationPath(unescapedKeyword);
            EncodeAndAppendToDocumentEvaluationPath(propertyName);

            WriteResult(match: isMatch, context: providerContext, messageProvider: messageProvider);

            _currentEvaluationPathRange = _evaluationPathStack.Pop();
            _currentSchemaEvaluationPathRange = _schemaEvaluationPathStack.Pop();
            _currentDocumentEvaluationPathRange = _documentEvaluationPathStack.Pop();
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedBooleanSchema(bool isMatch, JsonSchemaMessageProvider? messageProvider)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            WriteResult(match: isMatch, messageProvider: messageProvider);
        }
    }

    void IJsonSchemaResultsCollector.EvaluatedBooleanSchema<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider)
    {
        if (!isMatch || _level == JsonSchemaResultsLevel.Verbose)
        {
            WriteResult(match: isMatch, context: providerContext, messageProvider: messageProvider);
        }
    }

    private void SetSchemaEvaluationPath(JsonSchemaPathProvider path)
    {
        if (_currentSchemaEvaluationPathRange.End + MaxPathSegmentLength > _schemaEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _schemaEvaluationPath, _currentSchemaEvaluationPathRange.End);
        }

        if (!path(_schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.End), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentSchemaEvaluationPathRange = new ValueRange(_currentSchemaEvaluationPathRange.End, _currentSchemaEvaluationPathRange.End + written);
    }

    private void SetSchemaEvaluationPath<TProviderContext>(TProviderContext providerContext, JsonSchemaPathProvider<TProviderContext> path)
    {
        if (_currentSchemaEvaluationPathRange.End + MaxPathSegmentLength > _schemaEvaluationPath.Length)
        {
            Enlarge(MaxPathSegmentLength, ref _schemaEvaluationPath, _currentSchemaEvaluationPathRange.End);
        }

        if (!path(providerContext, _schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.End), out int written))
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        _currentSchemaEvaluationPathRange = new ValueRange(_currentSchemaEvaluationPathRange.End, _currentSchemaEvaluationPathRange.End + written);
    }

    [Conditional("DEBUG")]
    private void IsConsistent(int sequenceNumber)
    {
        // Ensure the path stacks are consistent
        Debug.Assert(
            (_evaluationPathStack.Length == 0 && _documentEvaluationPathStack.Length == 0 && _schemaEvaluationPathStack.Length == 0)
            || (_evaluationPathStack.Length == _documentEvaluationPathStack.Length && _evaluationPathStack.Length == _schemaEvaluationPathStack.Length));
        Debug.Assert(sequenceNumber == _sequenceNumber, "A context has been completed out-of-order");
    }

    internal string SchemaLocation => JsonReaderHelper.GetTextFromUtf8(_schemaEvaluationPath.AsSpan(_currentSchemaEvaluationPathRange.Start, _currentSchemaEvaluationPathRange.Length));

    internal string DocumentLocation => JsonReaderHelper.GetTextFromUtf8(_documentEvaluationPath.AsSpan(_currentDocumentEvaluationPathRange.Start, _currentDocumentEvaluationPathRange.Length));

    internal string EvaluationLocation => JsonReaderHelper.GetTextFromUtf8(_evaluationPath.AsSpan(_currentEvaluationPathRange.Start, _currentEvaluationPathRange.Length));
}