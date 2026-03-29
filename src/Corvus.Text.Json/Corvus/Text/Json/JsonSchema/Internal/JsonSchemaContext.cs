// <copyright file="JsonSchemaContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Diagnostics;

#if NET

using System.Numerics;

#endif

using System.Runtime.CompilerServices;

#if NET

using System.Runtime.InteropServices;

#endif

using System.Threading;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// The context for a JSON schema evaluation.
/// </summary>
[CLSCompliant(false)]
public struct JsonSchemaContext
    : IDisposable
{
    private const int InitialRentedBufferSize = 8192; // This allows for 65,536 property/item bits without reallocation

#if NET

    // This allows for 255 property/item bits without allocation, and is exactly one 256Bit Vector in size so merging values will be as simple a SIMD instruction as possible
    // on the most common processors at the time of writing.
    private const int BufferSize = 8;

    private const int BitsInAnInt = sizeof(int) * 8;

    // This is the maximum number of properties/items for which we can store bits without allocation.
    private const int MaxComplexValueCount = (BufferSize * BitsInAnInt) - 1;

#endif

    private readonly IJsonSchemaResultsCollector? _resultsCollector;

    private readonly int _offset;

    private readonly int _sequenceNumber;

    private int[]? _rentedBuffer;

    private uint _lengthAndUsingFeatures;

#if NET

    // If the top bit of the last byte of _localEvaluated is set, it indicates that we are using the rented buffer
    // for local evaluated bits and the first int is interpreted as the offset into the _rentedBuffer where the
    // local evaluated bits start with the second int interpreted as the bit buffer length.
    // If clear, then the remaining bits represent local evaluated indices.
    private EvaluatedIndexBuffer _localEvaluated;

    // If the top bit of the last byte of _appliedEvaluated is set, it indicates that we are using the rented buffer
    // for applied evaluated bits, and the first int is interpreted as the offset into the _rentedBuffer where the
    // applied evaluated bits start with the second int interpreted as the bit buffer length.
    // If clear, then the remaining bits represent applied evaluated indices.
    private EvaluatedIndexBuffer _appliedEvaluated;

#else
    private readonly int _localEvaluatedOffset;

    private readonly int _localEvaluatedLength;

    private readonly int _appliedEvaluatedOffset;

    private readonly int _appliedEvaluatedLength;
#endif

    [Flags]
    private enum UsingFeatures : uint
    {
        EvaluatedProperties = 0b0001_0000_0000_0000_0000_0000_0000_0000,
        EvaluatedItems = 0b0010_0000_0000_0000_0000_0000_0000_0000,
        IsMatch = 0b0100_0000_0000_0000_0000_0000_0000_0000,
        IsDisposable = 0b1000_0000_0000_0000_0000_0000_0000_0000,

        EvaluatedPropertiesOrItems = EvaluatedProperties | EvaluatedItems
    }

    private JsonSchemaContext(int sequenceNumber, int[]? rentedBuffer, uint lengthAndUsingFeatures, int offset, int evaluatedCount, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        _sequenceNumber = sequenceNumber;
        _rentedBuffer = rentedBuffer;
        _offset = offset;
        _resultsCollector = resultsCollector;
        _lengthAndUsingFeatures =
            lengthAndUsingFeatures
            | (uint)UsingFeatures.IsMatch; // But always  valid

#if NET
        if (evaluatedCount > MaxComplexValueCount)
        {
            int bitBufferLength = EnsureBitBufferLengths(evaluatedCount);
            _localEvaluated[^1] = 0b1000_0000; // Set the top bit to indicate that we are using the buffer for evaluated items
            _appliedEvaluated[^1] = 0b1000_0000; // Set the top bit to indicate that we are using the buffer for evaluated items
            _localEvaluated[0] = _offset;
            _localEvaluated[1] = bitBufferLength;
            _appliedEvaluated[0] = _offset + bitBufferLength;
            _appliedEvaluated[1] = bitBufferLength;
            _lengthAndUsingFeatures = (_lengthAndUsingFeatures & 0xF000_0000U) | unchecked((uint)(bitBufferLength * 2));
        }
#else
        if (evaluatedCount > 0)
        {
            int bitBufferLength = EnsureBitBufferLengths(evaluatedCount);
            _localEvaluatedOffset = offset;
            _localEvaluatedLength = bitBufferLength;
            _appliedEvaluatedOffset = offset + bitBufferLength;
            _appliedEvaluatedLength = bitBufferLength;
            _lengthAndUsingFeatures = (_lengthAndUsingFeatures & 0xF000_0000U) | unchecked((uint)(bitBufferLength * 2));
        }
#endif
    }

    /// <summary>
    /// Gets a value indicating whether the context represents a match.
    /// </summary>
    public readonly bool IsMatch => ((_lengthAndUsingFeatures & (uint)UsingFeatures.IsMatch) != 0);

    /// <summary>
    /// Gets a value indicating whether this context has a <see cref="IJsonSchemaResultsCollector"/>.
    /// </summary>
    public readonly bool HasCollector => _resultsCollector is not null;

    /// <summary>
    /// Gets a value indicating whether this context requires evaluation tracking.
    /// </summary>
    public readonly bool RequiresEvaluationTracking => (_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedPropertiesOrItems) != 0;

    // The length is the _lengthAndUsingFeatures union with the top nybble masked
    private readonly int Length => unchecked((int)(_lengthAndUsingFeatures & 0x0FFF_FFFFU));

    private readonly bool IsDisposable => ((_lengthAndUsingFeatures & (uint)UsingFeatures.IsDisposable) != 0);

    private readonly bool UseEvaluatedProperties => ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedProperties) != 0);

    private readonly bool UseEvaluatedItems => ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedItems) != 0);

    private Span<int> LocalEvaluated
    {
#pragma warning disable IDE0251 // Member can be made 'readonly'
        get
        {
#if NET
            if ((_localEvaluated[^1] & 0b1000_0000) == 0)
            {
                return MemoryMarshal.CreateSpan(ref _localEvaluated[0], BufferSize);
            }
            else
            {
                return _rentedBuffer.AsSpan(_localEvaluated[0], _localEvaluated[1]);
            }
#else
            return _rentedBuffer.AsSpan(_localEvaluatedOffset, _localEvaluatedLength);
#endif
        }
#pragma warning  restore IDE0251
    }

    private Span<int> AppliedEvaluated
    {
#pragma warning disable IDE0251 // Member can be made 'readonly'
        get
        {
#if NET
            if ((_appliedEvaluated[^1] & 0b1000_0000) == 0)
            {
                return MemoryMarshal.CreateSpan(ref _appliedEvaluated[0], BufferSize);
            }
            else
            {
                return _rentedBuffer.AsSpan(_appliedEvaluated[0], _appliedEvaluated[1]);
            }
#else
            return _rentedBuffer.AsSpan(_appliedEvaluatedOffset, _appliedEvaluatedLength);
#endif
        }
#pragma warning  restore IDE0251
    }

    /// <summary>
    /// Begins a new JSON schema evaluation context for the specified document.
    /// </summary>
    /// <typeparam name="T">The type of the JSON document.</typeparam>
    /// <param name="parentDocument">The parent JSON document to evaluate.</param>
    /// <param name="parentDocumentIndex">The index within the parent document.</param>
    /// <param name="usingEvaluatedItems">A value indicating whether to track evaluated items.</param>
    /// <param name="usingEvaluatedProperties">A value indicating whether to track evaluated properties.</param>
    /// <param name="resultsCollector">An optional results collector for gathering evaluation results.</param>
    /// <returns>A new <see cref="JsonSchemaContext"/> for the evaluation.</returns>
    public static JsonSchemaContext BeginContext<T>(
        T parentDocument,
        int parentDocumentIndex,
        bool usingEvaluatedItems,
        bool usingEvaluatedProperties,
        IJsonSchemaResultsCollector? resultsCollector = null)
        where T : IJsonDocument
    {
        int sequenceNumber = resultsCollector?.BeginChildContext(0) ?? 0;

        uint usingFeatures = usingEvaluatedProperties ? (uint)UsingFeatures.EvaluatedProperties : 0;
        usingFeatures |= usingEvaluatedItems ? (uint)UsingFeatures.EvaluatedItems : 0;
        usingFeatures |= (uint)UsingFeatures.IsMatch | (uint)UsingFeatures.IsDisposable;

        JsonTokenType valueKind = parentDocument.GetJsonTokenType(parentDocumentIndex);
        if (usingEvaluatedProperties && valueKind == JsonTokenType.StartObject)
        {
            return new JsonSchemaContext(
                sequenceNumber,
                null,
                usingFeatures,
                offset: 0,
                evaluatedCount: parentDocument.GetPropertyCount(parentDocumentIndex),
                resultsCollector);
        }

        if (usingEvaluatedItems && valueKind == JsonTokenType.StartArray)
        {
            return new JsonSchemaContext(
                sequenceNumber,
                null,
                usingFeatures,
                offset: 0,
                evaluatedCount: parentDocument.GetArrayLength(parentDocumentIndex),
                resultsCollector);
        }

        return new JsonSchemaContext(
            sequenceNumber,
            null,
            usingFeatures,
            offset: 0,
            evaluatedCount: -1,
            resultsCollector);
    }

    /// <summary>
    /// Creates a new child context for schema evaluation with escaped property name tracking.
    /// </summary>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether this child context should track evaluated array items.</param>
    /// <param name="useEvaluatedProperties">Whether this child context should track evaluated object properties.</param>
    /// <param name="escapedPropertyName">The escaped property name for path tracking in validation results.</param>
    /// <param name="evaluationPath">Optional provider for the reduced evaluation path in the schema.</param>
    /// <param name="schemaEvaluationPath">Optional provider for the full schema evaluation path.</param>
    /// <returns>A new child context initialized for the specified element.</returns>
    /// <remarks>
    /// <para>
    /// This method is part of the context lifecycle management system for JSON Schema validation.
    /// Child contexts inherit buffer space and configuration from their parent but maintain
    /// separate tracking for evaluated properties and items.
    /// </para>
    /// <para>
    /// The child context shares the same underlying buffer as the parent but uses a different
    /// offset to avoid conflicts. When the child context is committed via <see cref="CommitChildContext"/>,
    /// its results are merged back into the parent's validation results.
    /// </para>
    /// <para>
    /// <strong>Usage Pattern:</strong>
    /// <code>
    /// // Push child context for validating a property
    /// JsonSchemaContext childContext = parentContext.PushChildContext(
    /// document, propertyIndex, useItems: false, useProperties: true, propertyName);

    ///
    /// // Perform validation using child context
    /// bool isValid = ValidateProperty(ref childContext);

    ///
    /// // Commit results back to parent
    /// parentContext.CommitChildContext(isValid, ref childContext);

    /// </code>
    /// </para>
    /// </remarks>
    public readonly JsonSchemaContext PushChildContext(
        IJsonDocument parentDocument,
        int parentDocumentIndex,
        bool useEvaluatedItems,
        bool useEvaluatedProperties,
        ReadOnlySpan<byte> escapedPropertyName,
        JsonSchemaPathProvider? evaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null)
    {
        int sequenceNumber = _resultsCollector?.BeginChildContext(_sequenceNumber, escapedPropertyName, evaluationPath, schemaEvaluationPath) ?? 0;

        return PushChildContextCore(sequenceNumber, parentDocument, parentDocumentIndex, useEvaluatedItems, useEvaluatedProperties);
    }

    /// <summary>
    /// Creates a new child context for schema evaluation with item index tracking.
    /// </summary>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether this child context should track evaluated array items.</param>
    /// <param name="useEvaluatedProperties">Whether this child context should track evaluated object properties.</param>
    /// <param name="itemIndex">The item index for path tracking in validation results.</param>
    /// <param name="evaluationPath">Optional provider for the reduced evaluation path in the schema.</param>
    /// <param name="schemaEvaluationPath">Optional provider for the full schema evaluation path.</param>
    /// <returns>A new child context initialized for the specified element.</returns>
    /// <remarks>
    /// <para>
    /// This method is part of the context lifecycle management system for JSON Schema validation.
    /// Child contexts inherit buffer space and configuration from their parent but maintain
    /// separate tracking for evaluated properties and items.
    /// </para>
    /// <para>
    /// The child context shares the same underlying buffer as the parent but uses a different
    /// offset to avoid conflicts. When the child context is committed via <see cref="CommitChildContext"/>,
    /// its results are merged back into the parent's validation results.
    /// </para>
    /// <para>
    /// <strong>Usage Pattern:</strong>
    /// <code>
    /// // Push child context for validating a property
    /// JsonSchemaContext childContext = parentContext.PushChildContext(
    /// document, propertyIndex, useItems: false, useProperties: true, propertyName);

    ///
    /// // Perform validation using child context
    /// bool isValid = ValidateProperty(ref childContext);

    ///
    /// // Commit results back to parent
    /// parentContext.CommitChildContext(isValid, ref childContext);

    /// </code>
    /// </para>
    /// </remarks>
    public readonly JsonSchemaContext PushChildContext(
        IJsonDocument parentDocument,
        int parentDocumentIndex,
        bool useEvaluatedItems,
        bool useEvaluatedProperties,
        int itemIndex,
        JsonSchemaPathProvider? evaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null)
    {
        int sequenceNumber = _resultsCollector?.BeginChildContext(_sequenceNumber, itemIndex, evaluationPath, schemaEvaluationPath) ?? 0;

        return PushChildContextCore(sequenceNumber, parentDocument, parentDocumentIndex, useEvaluatedItems, useEvaluatedProperties);
    }

    /// <summary>
    /// Creates a new child context for schema evaluation with unescaped property name tracking.
    /// </summary>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether this child context should track evaluated array items.</param>
    /// <param name="useEvaluatedProperties">Whether this child context should track evaluated object properties.</param>
    /// <param name="unescapedPropertyName">The unescaped property name for path tracking in validation results.</param>
    /// <param name="evaluationPath">Optional provider for the reduced evaluation path in the schema.</param>
    /// <param name="schemaEvaluationPath">Optional provider for the full schema evaluation path.</param>
    /// <returns>A new child context initialized for the specified element.</returns>
    /// <remarks>
    /// <para>
    /// This is the unescaped variant of <see cref="PushChildContext"/>. Use this method when
    /// the property name is already in unescaped form to avoid unnecessary processing overhead.
    /// </para>
    /// <para>
    /// The context lifecycle and buffer management behavior is identical to the escaped variant.
    /// The only difference is that the property name is passed directly to the results collector
    /// without additional escaping processing.
    /// </para>
    /// </remarks>
    public readonly JsonSchemaContext PushChildContextUnescaped(
        IJsonDocument parentDocument,
        int parentDocumentIndex,
        bool useEvaluatedItems,
        bool useEvaluatedProperties,
        ReadOnlySpan<byte> unescapedPropertyName,
        JsonSchemaPathProvider? evaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null)
    {
        int sequenceNumber = _resultsCollector?.BeginChildContextUnescaped(_sequenceNumber, unescapedPropertyName, evaluationPath, schemaEvaluationPath) ?? 0;

        return PushChildContextCore(sequenceNumber, parentDocument, parentDocumentIndex, useEvaluatedItems, useEvaluatedProperties);
    }

    /// <summary>
    /// Creates a new child context for schema evaluation with typed provider context for path generation.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context used for path generation.</typeparam>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether this child context should track evaluated array items.</param>
    /// <param name="useEvaluatedProperties">Whether this child context should track evaluated object properties.</param>
    /// <param name="providerContext">The typed context object passed to path provider delegates.</param>
    /// <param name="evaluationPath">Optional provider for the reduced evaluation path in the schema.</param>
    /// <param name="schemaEvaluationPath">Optional provider for the full schema evaluation path.</param>
    /// <param name="documentEvaluationPath">Optional provider for the document instance path.</param>
    /// <returns>A new child context initialized for the specified element.</returns>
    /// <remarks>
    /// <para>
    /// This overload provides strongly-typed context support for custom path providers.
    /// The <paramref name="providerContext"/> is passed to each of the path provider delegates,
    /// allowing for stateful or computed path generation based on validation context.
    /// </para>
    /// <para>
    /// This is particularly useful for complex validation scenarios where path generation
    /// depends on runtime state, computed values, or external configuration.
    /// </para>
    /// </remarks>
    public readonly JsonSchemaContext PushChildContext<TProviderContext>(
        IJsonDocument parentDocument,
        int parentDocumentIndex,
        bool useEvaluatedItems,
        bool useEvaluatedProperties,
        TProviderContext providerContext,
        JsonSchemaPathProvider<TProviderContext>? evaluationPath = null,
        JsonSchemaPathProvider<TProviderContext>? schemaEvaluationPath = null,
        JsonSchemaPathProvider<TProviderContext>? documentEvaluationPath = null)
    {
        int sequenceNumber = _resultsCollector?.BeginChildContext(_sequenceNumber, providerContext, evaluationPath, schemaEvaluationPath, documentEvaluationPath) ?? 0;

        return PushChildContextCore(sequenceNumber, parentDocument, parentDocumentIndex, useEvaluatedItems, useEvaluatedProperties);
    }

    /// <summary>
    /// Creates a new child context for schema evaluation with optional path providers.
    /// </summary>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether this child context should track evaluated array items.</param>
    /// <param name="useEvaluatedProperties">Whether this child context should track evaluated object properties.</param>
    /// <param name="evaluationPath">Optional provider for the reduced evaluation path in the schema.</param>
    /// <param name="schemaEvaluationPath">Optional provider for the full schema evaluation path.</param>
    /// <param name="documentEvaluationPath">Optional provider for the document instance path.</param>
    /// <returns>A new child context initialized for the specified element.</returns>
    /// <remarks>
    /// <para>
    /// This is the most flexible overload for child context creation, allowing custom path
    /// providers for all three path types: evaluation path, schema evaluation path, and
    /// document evaluation path. These paths are used for generating detailed validation
    /// error messages and tracing schema evaluation flow.
    /// </para>
    /// <para>
    /// Use this overload when you need full control over path generation without requiring
    /// a typed provider context object.
    /// </para>
    /// </remarks>
    public readonly JsonSchemaContext PushChildContext(
        IJsonDocument parentDocument,
        int parentDocumentIndex,
        bool useEvaluatedItems,
        bool useEvaluatedProperties,
        JsonSchemaPathProvider? evaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null,
        JsonSchemaPathProvider? documentEvaluationPath = null)
    {
        int sequenceNumber = _resultsCollector?.BeginChildContext(_sequenceNumber, reducedEvaluationPath: evaluationPath, schemaEvaluationPath: schemaEvaluationPath, documentEvaluationPath) ?? 0;

        return PushChildContextCore(sequenceNumber, parentDocument, parentDocumentIndex, useEvaluatedItems, useEvaluatedProperties);
    }

    /// <summary>
    /// Commits a child context back to its parent, merging validation results and cleaning up resources.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context used for message generation.</typeparam>
    /// <param name="isMatch">Whether the parent validation succeeded.</param>
    /// <param name="childContext">The child context to commit (passed by readonly reference for performance).</param>
    /// <param name="providerContext">The typed context object passed to the message provider.</param>
    /// <param name="messageProvider">Optional provider for generating validation messages.</param>
    /// <remarks>
    /// <para>
    /// This method completes the child context lifecycle by:
    /// <list type="bullet">
    /// <item><description>Merging validation results from the child into the results collector</description></item>
    /// <item><description>Transferring ownership of shared buffer resources from child to parent</description></item>
    /// <item><description>Updating the parent's match status based on both parent and child results</description></item>
    /// <item><description>Generating validation messages using the provided message provider</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Important:</strong> This method does NOT automatically apply evaluated properties/items
    /// from the child context to the parent. Use <see cref="ApplyEvaluated"/> separately if you need
    /// to merge evaluated tracking information.
    /// </para>
    /// <para>
    /// <strong>Performance Note:</strong> The child context is passed by readonly reference to avoid
    /// copying the entire struct. The buffer ownership transfer ensures proper resource management
    /// without requiring explicit disposal of the child context.
    /// </para>
    /// <para>
    /// <strong>Usage Pattern:</strong>
    /// <code>
    /// // After validation with child context
    /// bool childIsValid = ValidateWithChild(ref childContext);
    /// bool parentIsValid = parentContext.IsMatch &amp;&amp; childIsValid;
    ///
    /// // Commit the child results
    /// parentContext.CommitChildContext(parentIsValid, ref childContext, contextData, messageProvider);
    ///
    /// // Optionally merge evaluated tracking
    /// if (needsEvaluatedTracking)
    /// {
    ///     parentContext.ApplyEvaluated(ref childContext);
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitChildContext<TProviderContext>(bool isMatch, ref readonly JsonSchemaContext childContext, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider = null)
    {
        _resultsCollector?.CommitChildContext(childContext._sequenceNumber, parentIsMatch: isMatch, childIsMatch: childContext.IsMatch, providerContext, messageProvider ?? (static (_, buffer, out written) => JsonSchemaEvaluation.EvaluatedSubschema(buffer, out written)));
        _rentedBuffer = childContext._rentedBuffer;
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Commits a child context back to its parent, merging validation results and cleaning up resources.
    /// </summary>
    /// <param name="isMatch">Whether the parent validation succeeded.</param>
    /// <param name="childContext">The child context to commit (passed by readonly reference for performance).</param>
    /// <param name="messageProvider">Optional provider for generating validation messages.</param>
    /// <remarks>
    /// <para>
    /// This is the non-generic overload of <see cref="CommitChildContext{TProviderContext}"/>.
    /// Use this method when you don't need typed provider context for message generation.
    /// </para>
    /// <para>
    /// The lifecycle management behavior is identical to the generic overload:
    /// <list type="bullet">
    /// <item><description>Validation results are merged into the results collector</description></item>
    /// <item><description>Buffer ownership is transferred from child to parent</description></item>
    /// <item><description>Parent match status is updated based on the provided <paramref name="isMatch"/> value</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Typical Usage:</strong> Use this overload for simple validation scenarios where
    /// error messages don't require additional context beyond the standard validation paths.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CommitChildContext(bool isMatch, ref readonly JsonSchemaContext childContext, JsonSchemaMessageProvider? messageProvider = null)
    {
        _resultsCollector?.CommitChildContext(childContext._sequenceNumber, parentIsMatch: isMatch, childIsMatch: childContext.IsMatch, messageProvider ?? JsonSchemaEvaluation.EvaluatedSubschema);
        _rentedBuffer = childContext._rentedBuffer;
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Ends the root evaluation context, committing any pending results to the results collector.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method must be called after the root <c>Evaluate</c> completes to ensure that
    /// results written directly at the root level (e.g., <c>required</c> keyword failures)
    /// are committed to the results collector. Without this call, such results are orphaned
    /// because <see cref="BeginContext{T}"/> opens a child context on the collector that
    /// is never otherwise committed.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndContext()
    {
        _resultsCollector?.CommitChildContext(_sequenceNumber, parentIsMatch: false, childIsMatch: IsMatch, JsonSchemaEvaluation.EvaluatedSubschema);
    }

    /// <summary>
    /// Records the evaluation of a boolean schema.
    /// </summary>
    /// <param name="isMatch">A value indicating whether the boolean schema matched.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedBooleanSchema(bool isMatch)
    {
        _resultsCollector?.EvaluatedBooleanSchema(isMatch, null);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword.
    /// </summary>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="unescapedKeyword">The unescaped keyword that was evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeyword(
        bool isMatch,
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> unescapedKeyword)
    {
        _resultsCollector?.EvaluatedKeyword(isMatch, messageProvider, unescapedKeyword);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword with a provider context.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context.</typeparam>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="providerContext">The provider context for the evaluation.</param>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="unescapedKeyword">The unescaped keyword that was evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeyword<TProviderContext>(
        bool isMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
         ReadOnlySpan<byte> unescapedKeyword)
    {
        _resultsCollector?.EvaluatedKeyword(isMatch, providerContext, messageProvider, unescapedKeyword);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword for a specific property.
    /// </summary>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="propertyName">The name of the property being evaluated.</param>
    /// <param name="unescapedKeyword">The unescaped keyword that was evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeywordForProperty(
        bool isMatch,
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> propertyName,
        ReadOnlySpan<byte> unescapedKeyword)
    {
        _resultsCollector?.EvaluatedKeywordForProperty(isMatch, messageProvider, propertyName, unescapedKeyword);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword for a specific property with a provider context.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context.</typeparam>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="providerContext">The provider context for the evaluation.</param>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="propertyName">The name of the property being evaluated.</param>
    /// <param name="unescapedKeyword">The unescaped keyword that was evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeywordForProperty<TProviderContext>(
        bool isMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
        ReadOnlySpan<byte> propertyName,
        ReadOnlySpan<byte> unescapedKeyword)
    {
        _resultsCollector?.EvaluatedKeywordForProperty(isMatch, providerContext, messageProvider, propertyName, unescapedKeyword);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword using a path-based approach.
    /// </summary>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="messageProvider">The message provider for generating evaluation messages.</param>
    /// <param name="keywordPath">The path provider for the keyword being evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeywordPath(
        bool isMatch,
        JsonSchemaMessageProvider messageProvider,
        JsonSchemaPathProvider keywordPath)
    {
        _resultsCollector?.EvaluatedKeywordPath(isMatch, messageProvider, keywordPath);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records the evaluation of a schema keyword using a path-based approach with a provider context.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context.</typeparam>
    /// <param name="isMatch">A value indicating whether the keyword evaluation matched.</param>
    /// <param name="providerContext">The provider context for the evaluation.</param>
    /// <param name="messageProvider">The message provider for generating evaluation messages.</param>
    /// <param name="keywordPath">The path provider for the keyword being evaluated.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EvaluatedKeywordPath<TProviderContext>(
        bool isMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext> messageProvider,
        JsonSchemaPathProvider<TProviderContext> keywordPath)
    {
        _resultsCollector?.EvaluatedKeywordPath(isMatch, providerContext, messageProvider, keywordPath);
        if (!isMatch)
        {
            _lengthAndUsingFeatures &= ~(uint)UsingFeatures.IsMatch;
        }
    }

    /// <summary>
    /// Records that a keyword was ignored during schema evaluation.
    /// </summary>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="encodedKeyword">The encoded keyword that was ignored.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void IgnoredKeyword(
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> encodedKeyword)
    {
        _resultsCollector?.IgnoredKeyword(messageProvider, encodedKeyword);
    }

    /// <summary>
    /// Records that a keyword was ignored during schema evaluation with a provider context.
    /// </summary>
    /// <typeparam name="TProviderContext">The type of the provider context.</typeparam>
    /// <param name="providerContext">The provider context for the evaluation.</param>
    /// <param name="messageProvider">An optional message provider for generating evaluation messages.</param>
    /// <param name="unescapedKeyword">The unescaped keyword that was ignored.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void IgnoredKeyword<TProviderContext>(
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
        ReadOnlySpan<byte> unescapedKeyword)
    {
        _resultsCollector?.IgnoredKeyword(providerContext, messageProvider, unescapedKeyword);
    }

    /// <summary>
    /// Pops the most recently pushed child context without committing changes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopChildContext(ref readonly JsonSchemaContext childContext)
    {
        _resultsCollector?.PopChildContext(childContext._sequenceNumber);
        _rentedBuffer = childContext._rentedBuffer;
    }

    /// <summary>
    /// Determines whether a specific item at the given index has been locally evaluated.
    /// </summary>
    /// <param name="index">The index of the item to check.</param>
    /// <returns><see langword="true"/> if the item at the specified index has been locally evaluated; otherwise, <see langword="false"/>.</returns>
    public bool HasLocalEvaluatedItem(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedItems) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            return (LocalEvaluated[intOffset] & bit) != 0;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a specific property at the given index has been locally evaluated.
    /// </summary>
    /// <param name="index">The index of the property to check.</param>
    /// <returns><see langword="true"/> if the property at the specified index has been locally evaluated; otherwise, <see langword="false"/>.</returns>
    public bool HasLocalEvaluatedProperty(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedProperties) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            return (LocalEvaluated[intOffset] & bit) != 0;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a specific item at the given index has been either locally or applied evaluated.
    /// </summary>
    /// <param name="index">The index of the item to check.</param>
    /// <returns><see langword="true"/> if the item at the specified index has been locally or applied evaluated; otherwise, <see langword="false"/>.</returns>
    public bool HasLocalOrAppliedEvaluatedItem(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedItems) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            return (LocalEvaluated[intOffset] & bit) != 0 || (AppliedEvaluated[intOffset] & bit) != 0;
        }

        return false;
    }

    /// <summary>
    /// Determines whether a specific property at the given index has been either locally or applied evaluated.
    /// </summary>
    /// <param name="index">The index of the property to check.</param>
    /// <returns><see langword="true"/> if the property at the specified index has been locally or applied evaluated; otherwise, <see langword="false"/>.</returns>
    public bool HasLocalOrAppliedEvaluatedProperty(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedProperties) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            return (LocalEvaluated[intOffset] & bit) != 0 || (AppliedEvaluated[intOffset] & bit) != 0;
        }

        return false;
    }

    /// <summary>
    /// Applies the evaluated properties/items from the child context
    /// to this (parent) context, if appropriate.
    /// </summary>
    /// <param name="childContext">The child context from which to apply evaluated properties/items</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ApplyEvaluated(ref readonly JsonSchemaContext childContext)
    {
        if ((childContext.UseEvaluatedItems && UseEvaluatedItems) || (childContext.UseEvaluatedProperties && UseEvaluatedProperties))
        {
            Span<int> childLocalEvaluated = childContext.LocalEvaluated;
            Span<int> childAppliedEvaluated = childContext.AppliedEvaluated;
            Span<int> evaluatedItems = AppliedEvaluated;

            // Ensure that we are all the same length - which we should be because we
            // must be talking about the same object!
            Debug.Assert(childLocalEvaluated.Length == childAppliedEvaluated.Length);
            Debug.Assert(evaluatedItems.Length == childAppliedEvaluated.Length);

#if NET
            int vectorSize = Vector<int>.Count;
            int length = evaluatedItems.Length;
            int vectorCount = length / vectorSize;
            int vectorizedLength = vectorCount * vectorSize;

            Span<Vector<int>> vEvaluatedItems = MemoryMarshal.Cast<int, Vector<int>>(evaluatedItems.Slice(0, vectorizedLength));
            Span<Vector<int>> vChildLocal = MemoryMarshal.Cast<int, Vector<int>>(childLocalEvaluated.Slice(0, vectorizedLength));
            Span<Vector<int>> vChildApplied = MemoryMarshal.Cast<int, Vector<int>>(childAppliedEvaluated.Slice(0, vectorizedLength));

            for (int i = 0; i < vEvaluatedItems.Length; i++)
            {
                vEvaluatedItems[i] = vEvaluatedItems[i] | vChildLocal[i] | vChildApplied[i];
            }

            // Scalar loop for remaining elements
            for (int i = vectorizedLength; i < length; i++)
            {
                evaluatedItems[i] |= childLocalEvaluated[i] | childAppliedEvaluated[i];
            }
#else
            for (int i = 0; i < childLocalEvaluated.Length; i++)
            {
                evaluatedItems[i] |= childLocalEvaluated[i] | childAppliedEvaluated[i];
            }
#endif
        }
    }

    public void Dispose()
    {
        if (_rentedBuffer != null && IsDisposable)
        {
            int[]? bufferToReturn = Interlocked.Exchange(ref _rentedBuffer, null);
            if (bufferToReturn != null)
            {
                // Clear the entire buffer as they may contain actual data
                bufferToReturn.AsSpan().Clear();
                ArrayPool<int>.Shared.Return(bufferToReturn);
            }
        }
    }

    /// <summary>
    /// Adds an item at the specified index to the local evaluated items collection.
    /// </summary>
    /// <param name="index">The index of the item to mark as locally evaluated.</param>
    public void AddLocalEvaluatedItem(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedItems) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            LocalEvaluated[intOffset] |= bit;
        }
    }

    /// <summary>
    /// Adds a property at the specified index to the local evaluated properties collection.
    /// </summary>
    /// <param name="index">The index of the property to mark as locally evaluated.</param>
    public void AddLocalEvaluatedProperty(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedProperties) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            LocalEvaluated[intOffset] |= bit;
        }
    }

    /// <summary>
    /// Adds a property at the specified index to the applied evaluated properties collection.
    /// </summary>
    /// <param name="index">The index of the property to mark as applied evaluated.</param>
    /// <remarks>
    /// Use this instead of <see cref="AddLocalEvaluatedProperty(int)"/> when the property
    /// was evaluated by an applicator (e.g. a hoisted allOf branch) rather than by a keyword
    /// defined directly in this schema. This ensures that <c>additionalProperties</c> still
    /// sees the property as additional, while <c>unevaluatedProperties</c> recognises it as
    /// evaluated.
    /// </remarks>
    public void AddAppliedEvaluatedProperty(int index)
    {
        if ((_lengthAndUsingFeatures & (uint)UsingFeatures.EvaluatedProperties) != 0)
        {
            // Calculate the offset into the array
            int intOffset = index >> 5; // divide by 32 ==> shift right 5
            int bitOffset = index & 0b1_1111; // remainder of dividing by 32
            int bit = 1 << bitOffset;
            AppliedEvaluated[intOffset] |= bit;
        }
    }

    /// <summary>
    /// Core implementation for creating child contexts with optimized buffer allocation strategies.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number assigned by the results collector for tracking.</param>
    /// <param name="parentDocument">The JSON document containing the element being evaluated.</param>
    /// <param name="parentDocumentIndex">The index of the parent element in the document.</param>
    /// <param name="useEvaluatedItems">Whether to track evaluated array items for this context.</param>
    /// <param name="useEvaluatedProperties">Whether to track evaluated object properties for this context.</param>
    /// <returns>A new child context with appropriate buffer allocation and tracking configuration.</returns>
    /// <remarks>
    /// <para>
    /// This method implements the core child context creation logic with several important optimizations:
    /// </para>
    /// <para>
    /// <strong>Buffer Sharing Strategy:</strong>
    /// <list type="bullet">
    /// <item><description>Child contexts share the same underlying buffer as their parent</description></item>
    /// <item><description>Each child gets a unique offset within the shared buffer</description></item>
    /// <item><description>This avoids allocation overhead for short-lived child contexts</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Evaluated Tracking Optimization:</strong>
    /// <list type="bullet">
    /// <item><description>For JSON objects: allocates bit buffers sized to the object's property count</description></item>
    /// <item><description>For JSON arrays: allocates bit buffers sized to the array's length</description></item>
    /// <item><description>For other types: creates minimal context without evaluated tracking</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Feature Flag Management:</strong>
    /// Child contexts inherit feature flags from their parent but are marked as non-disposable
    /// since they share buffer ownership with the parent. The parent retains disposal responsibility.
    /// </para>
    /// </remarks>
    private readonly JsonSchemaContext PushChildContextCore(int sequenceNumber, IJsonDocument parentDocument, int parentDocumentIndex, bool useEvaluatedItems, bool useEvaluatedProperties)
    {
        bool usesEvaluatedProperties = UseEvaluatedProperties || useEvaluatedProperties;
        bool usesEvaluatedItems = UseEvaluatedItems || useEvaluatedItems;

        uint usingFeatures = usesEvaluatedProperties ? (uint)UsingFeatures.EvaluatedProperties : 0;
        usingFeatures |= usesEvaluatedItems ? (uint)UsingFeatures.EvaluatedItems : 0;

        // If we are creating a child context, we have to ensure we are using new buffers
        if (usesEvaluatedItems || usesEvaluatedProperties)
        {
            JsonTokenType tokenType = parentDocument.GetJsonTokenType(parentDocumentIndex);
            if (usesEvaluatedProperties && tokenType == JsonTokenType.StartObject)
            {
                return new JsonSchemaContext(
                    sequenceNumber,
                    _rentedBuffer,
                    _lengthAndUsingFeatures | usingFeatures & ~(uint)UsingFeatures.IsDisposable,
                    offset: _offset + Length,
                    evaluatedCount: parentDocument.GetPropertyCount(parentDocumentIndex),
                    resultsCollector: _resultsCollector);
            }

            if (usesEvaluatedItems && tokenType == JsonTokenType.StartArray)
            {
                return new JsonSchemaContext(
                    sequenceNumber,
                    _rentedBuffer,
                    _lengthAndUsingFeatures | usingFeatures & ~(uint)UsingFeatures.IsDisposable,
                    offset: _offset + Length,
                    evaluatedCount: parentDocument.GetArrayLength(parentDocumentIndex),
                    resultsCollector: _resultsCollector);
            }
        }

        return new JsonSchemaContext(
            sequenceNumber,
            _rentedBuffer,
            _lengthAndUsingFeatures & ~(uint)UsingFeatures.IsDisposable,
            offset: _offset + Length,
            evaluatedCount: -1,
            resultsCollector: _resultsCollector);
    }

    private int EnsureBitBufferLengths(int count)
    {
        Debug.Assert(count != 0);

        // Required property buffer length
        int bitBufferLength = (count >> 5) + 1; // Divide by 32 (>> 5) gives offset, add 1 to give length
        int propertyRemainder = count & 0b1_1111; // Remainder is the bottom 5 bits (0 > 31)
        bitBufferLength += (propertyRemainder == 0 ? 0 : 1);

        if (bitBufferLength > 0)
        {
            if ((_rentedBuffer is null || bitBufferLength > _rentedBuffer.Length - _offset - Length))
            {
                Enlarge(bitBufferLength * 2); // We double the required length in order to support local and applied bitBuffers
            }
            else
            {
                // Clear our bit of the buffer
                _rentedBuffer.AsSpan(_offset, bitBufferLength * 2).Clear();
            }
        }

        return bitBufferLength;
    }

    private void Enlarge(int required)
    {
        if (_rentedBuffer == null)
        {
            _rentedBuffer = ArrayPool<int>.Shared.Rent(InitialRentedBufferSize);
            _rentedBuffer.AsSpan().Clear();
            return;
        }

        int[] toReturn = _rentedBuffer;

        // Allow the data to grow up to maximum possible capacity (~2G bytes) before encountering overflow.
        // Note: Array.MaxLength exists only on .NET 6 or greater,
        // so for the other versions value is hardcoded
        const int MaxArrayLength = 0x7FFFFFC7;

#if NET
        Debug.Assert(MaxArrayLength == Array.MaxLength);
#endif

        // We will double the length, or use required
        int newCapacity = Math.Max(toReturn.Length * 2, toReturn.Length + required);

        // Note that this check works even when newCapacity overflowed thanks to the (uint) cast
        if ((uint)newCapacity > MaxArrayLength) newCapacity = MaxArrayLength;

        // If the maximum capacity has already been reached,
        // then set the new capacity to be larger than what is possible
        // so that ArrayPool.Rent throws an OutOfMemoryException for us.
        if (newCapacity == toReturn.Length) newCapacity = int.MaxValue;

        _rentedBuffer = ArrayPool<int>.Shared.Rent(newCapacity);
        Buffer.BlockCopy(toReturn, 0, _rentedBuffer, 0, toReturn.Length * sizeof(int));

        // Clear the new buffer bits
        _rentedBuffer.AsSpan(toReturn.Length).Clear();

        // The data in this rented buffer only conveys the
        // index of items or properties in a complex value, but no content;
        // so it does not need to be cleared.
        ArrayPool<int>.Shared.Return(toReturn);
    }

#if NET

    /// <summary>
    /// Represents an inline array buffer for storing evaluated index information.
    /// </summary>
    [InlineArray(BufferSize)]
    public struct EvaluatedIndexBuffer
    {
        private int _element0;
    }

#endif
}