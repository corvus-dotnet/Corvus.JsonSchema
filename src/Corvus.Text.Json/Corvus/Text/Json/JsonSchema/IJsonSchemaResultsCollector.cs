// <copyright file="IJsonSchemaResultsCollector.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json;

/// <summary>
/// Provides a message for a JSON Schema validation result.
/// </summary>
/// <param name="buffer">
/// The buffer to which the message should be written as UTF-8 bytes.
/// </param>
/// <param name="written">
/// When this method returns, contains the number of bytes written to <paramref name="buffer"/>.
/// </param>
/// <returns>
/// <see langword="true"/> if the message was successfully written to the buffer; otherwise, <see langword="false"/>.
/// </returns>
public delegate bool JsonSchemaMessageProvider(Span<byte> buffer, out int written);

/// <summary>
/// Provides a message for a JSON Schema validation result, using a context value.
/// </summary>
/// <typeparam name="TContext">The type of the context value.</typeparam>
/// <param name="context">The context value used to generate the message.</param>
/// <param name="buffer">
/// The buffer to which the message should be written as UTF-8 bytes.
/// </param>
/// <param name="written">
/// When this method returns, contains the number of bytes written to <paramref name="buffer"/>.
/// </param>
/// <returns>
/// <see langword="true"/> if the message was successfully written to the buffer; otherwise, <see langword="false"/>.
/// </returns>
public delegate bool JsonSchemaMessageProvider<TContext>(TContext context, Span<byte> buffer, out int written);

/// <summary>
/// Provides a path segment for a JSON Schema location or instance path.
/// </summary>
/// <param name="buffer">
/// The buffer to which the path segment should be written as UTF-8 bytes.
/// </param>
/// <param name="written">
/// When this method returns, contains the number of bytes written to <paramref name="buffer"/>.
/// </param>
/// <returns>
/// <see langword="true"/> if the path segment was successfully written to the buffer; otherwise, <see langword="false"/>.
/// </returns>
public delegate bool JsonSchemaPathProvider(Span<byte> buffer, out int written);

/// <summary>
/// Provides a path segment for a JSON Schema location or instance path, using a context value.
/// </summary>
/// <typeparam name="TContext">The type of the context value.</typeparam>
/// <param name="context">The context value used to generate the path segment.</param>
/// <param name="buffer">
/// The buffer to which the path segment should be written as UTF-8 bytes.
/// </param>
/// <param name="written">
/// When this method returns, contains the number of bytes written to <paramref name="buffer"/>.
/// </param>
/// <returns>
/// <see langword="true"/> if the path segment was successfully written to the buffer; otherwise, <see langword="false"/>.
/// </returns>
public delegate bool JsonSchemaPathProvider<TContext>(TContext context, Span<byte> buffer, out int written);

/// <summary>
/// Implemented by types that accumulate the results of a JSON Schema evaluation.
/// </summary>
public interface IJsonSchemaResultsCollector : IDisposable
{
    /// <summary>
    /// Begin a child context.
    /// </summary>
    /// <param name="parentSequenceNumber">The sequence number of the parent context.</param>
    /// <param name="reducedEvaluationPath">The path taken through the schema(s).</param>
    /// <param name="schemaEvaluationPath">The schema evaluation path.</param>
    /// <param name="documentEvaluationPath">The path in the JSON document instance.</param>
    /// <returns>The sequence number of the child context.</returns>
    /// <remarks>
    /// <para>
    /// Begins evaluation of a schema in a child context. The context may later be committed with <see cref="CommitChildContext"/>
    /// or abandoned with <see cref="PopChildContext"/>.
    /// </para>
    /// <para>
    /// In DEBUG builds, the sequence number returned by the call to <see cref="BeginChildContext"/> is passed to the commit or pop methods and validated
    /// to ensure that completion operations are carried out in the expected order.
    /// </para>
    /// </remarks>
    int BeginChildContext(
        int parentSequenceNumber,
        JsonSchemaPathProvider? reducedEvaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null,
        JsonSchemaPathProvider? documentEvaluationPath = null);

    /// <summary>
    /// Begin a child context for a property evaluation.
    /// </summary>
    /// <param name="parentSequenceNumber">The sequence number of the parent context.</param>
    /// <param name="escapedPropertyName">The escaped name of the property for which to begin a child context.</param>
    /// <param name="reducedEvaluationPath">The fully reduced evaluation path for the keyword.</param>
    /// <param name="schemaEvaluationPath">The schema evaluation path of the target schema.</param>
    /// <returns>The sequence number of the child context.</returns>
    /// <remarks>
    /// <para>
    /// Begins evaluation of a schema in a child context. The context may later be committed with <see cref="CommitChildContext"/>
    /// or abandoned with <see cref="PopChildContext"/>.
    /// </para>
    /// </remarks>
    int BeginChildContext(
        int parentSequenceNumber,
        ReadOnlySpan<byte> escapedPropertyName,
        JsonSchemaPathProvider? reducedEvaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null);

    /// <summary>
    /// Begin a child context for an item evaluation.
    /// </summary>
    /// <param name="parentSequenceNumber">The sequence number of the parent context.</param>
    /// <param name="itemIndex">The index of the item for which to begin a child context.</param>
    /// <param name="reducedEvaluationPath">The fully reduced evaluation path for the keyword.</param>
    /// <param name="schemaEvaluationPath">The schema evaluation path of the target schema.</param>
    /// <returns>The sequence number of the child context.</returns>
    /// <remarks>
    /// <para>
    /// Begins evaluation of a schema in a child context. The context may later be committed with <see cref="CommitChildContext"/>
    /// or abandoned with <see cref="PopChildContext"/>.
    /// </para>
    /// </remarks>
    int BeginChildContext(
        int parentSequenceNumber,
        int itemIndex,
        JsonSchemaPathProvider? reducedEvaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null);

    /// <summary>
    /// Begin a child context.
    /// </summary>
    /// <param name="parentSequenceNumber">The sequence number of the parent context.</param>
    /// <param name="providerContext">The context to be passed to the path provider.</param>
    /// <param name="reducedEvaluationPath">The path taken through the schema(s) at which the child context is being evaluated.</param>
    /// <param name="schemaEvaluationPath">The schema evaluation path.</param>
    /// <param name="documentEvaluationPath">The path in the JSON document instance at which the child context is being evaluated.</param>
    /// <returns>The sequence number of the child context.</returns>
    /// <remarks>
    /// <para>
    /// Begins evaluation of a schema in a child context. The context may later be committed with <see cref="CommitChildContext"/>
    /// or abandoned with <see cref="PopChildContext"/>.
    /// </para>
    /// <para>
    /// A child context operates like a stack. You *must* pop/commit child contexts in *reverse order* of that in which you Begin()
    /// a child context. The sequence number returned by <see cref="BeginChildContext{TProviderContext}"/> and passed in to
    /// <see cref="CommitChildContext"/> or <see cref="PopChildContext(int)"/> is used to enforce this
    /// </para>
    /// </remarks>
    int BeginChildContext<TProviderContext>(
        int parentSequenceNumber,
        TProviderContext providerContext,
        JsonSchemaPathProvider<TProviderContext>? reducedEvaluationPath,
        JsonSchemaPathProvider<TProviderContext>? schemaEvaluationPath,
        JsonSchemaPathProvider<TProviderContext>? documentEvaluationPath);

    /// <summary>
    /// Begin a child context for a property evaluation.
    /// </summary>
    /// <param name="parentSequenceNumber">The sequence number of the parent context.</param>
    /// <param name="unescapedPropertyName">The name of the property for which to begin a child context.</param>
    /// <param name="reducedEvaluationPath">The fully reduced evaluation path for the keyword.</param>
    /// <param name="schemaEvaluationPath">The schema evaluation path of the target schema.</param>
    /// <returns>The sequence number of the child context.</returns>
    /// <remarks>
    /// <para>
    /// Begins evaluation of a schema in a child context. The context may later be committed with <see cref="CommitChildContext"/>
    /// or abandoned with <see cref="PopChildContext"/>.
    /// </para>
    /// </remarks>
    int BeginChildContextUnescaped(
        int parentSequenceNumber,
        ReadOnlySpan<byte> unescapedPropertyName,
        JsonSchemaPathProvider? reducedEvaluationPath = null,
        JsonSchemaPathProvider? schemaEvaluationPath = null);

    /// <summary>
    /// Commits the last child context.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number of the child context to commit.</param>
    /// <param name="parentIsMatch">If <see langword="true"/> then the parent commit indicates a successful match.</param>
    /// <param name="childIsMatch">If <see langword="true"/> then the commit indicates that the child produced a successful match.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON validation message.</param>
    /// <remarks>
    /// This allows the collector to update the match state, and commit any resources associated with the child context.
    /// </remarks>
    void CommitChildContext(
        int sequenceNumber,
        bool parentIsMatch,
        bool childIsMatch,
        JsonSchemaMessageProvider? messageProvider);

    /// <summary>
    /// Commits the last child context.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number of the child context to commit.</param>
    /// <param name="parentIsMatch">If <see langword="true"/> then the parent commit indicates a successful match.</param>
    /// <param name="childIsMatch">If <see langword="true"/> then the commit indicates that the child produced a successful match.</param>
    /// <param name="providerContext">The context to provide to the message provider.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <remarks>
    /// This allows the collector to update the match state, and commit any resources associated with the child context.
    /// </remarks>
    void CommitChildContext<TProviderContext>(
        int sequenceNumber,
        bool parentIsMatch,
        bool childIsMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider);

    /// <summary>
    /// Indicates that a boolean schema was evaluated.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <remarks>
    /// This is used when evaluating a schema of the form <c>true</c> or <c>false</c>.
    /// </remarks>
    void EvaluatedBooleanSchema(bool isMatch, JsonSchemaMessageProvider? messageProvider);

    /// <summary>
    /// Indicates that a boolean schema was evaluated.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="providerContext">The context to provide to the message provider.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <remarks>
    /// This is used when evaluating a schema of the form <c>true</c> or <c>false</c>.
    /// </remarks>
    void EvaluatedBooleanSchema<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext>? messageProvider);

    /// <summary>
    /// Updates the match state for the given evaluated keyword.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeyword">The keyword that was evaluated.</param>
    void EvaluatedKeyword(
        bool isMatch,
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Updates the match state for the given evaluated keyword.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="providerContext">The context to provider to the providers.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeyword">The keyword that was evaluated.</param>
    void EvaluatedKeyword<TProviderContext>(
        bool isMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Updates the match state for the given keyword evaluated against the given property.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="propertyName">The name of the property for which to begin a child context.</param>
    /// <param name="encodedKeyword">The keyword that was evaluated.</param>
    void EvaluatedKeywordForProperty(
        bool isMatch,
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> propertyName,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Updates the match state for the given keyword evaluated against the given property.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="providerContext">The context to provider to the providers.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="propertyName">The name of the property for which to begin a child context.</param>
    /// <param name="encodedKeyword">The keyword that was evaluated.</param>
    void EvaluatedKeywordForProperty<TProviderContext>(
        bool isMatch,
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
        ReadOnlySpan<byte> propertyName,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Updates the match state for the given evaluated keyword.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeywordPath">The keyword and its sub-path that was evaluated.</param>
    /// <remarks>
    /// This is used when the entity evaluated was a sub-element of the keyword (e.g. the index of the first name in the array
    /// for the <c>required</c> keyword, would produce <c>required/0</c> as the <paramref name="encodedKeywordPath"/>).
    /// </remarks>
    void EvaluatedKeywordPath(bool isMatch, JsonSchemaMessageProvider messageProvider, JsonSchemaPathProvider encodedKeywordPath);

    /// <summary>
    /// Updates the match state for the given evaluated keyword.
    /// </summary>
    /// <param name="isMatch">If <see langword="true"/> then this indicates that the current context produced a successful match.</param>
    /// <param name="providerContext">The context to provider to the providers.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeywordPath">The keyword and its sub-path that was evaluated.</param>
    /// <remarks>
    /// This is used when the entity evaluated was a sub-element of the keyword (e.g. the index of the first name in the array
    /// for the <c>required</c> keyword, would produce <c>required/0</c> as the <paramref name="encodedKeywordPath"/>).
    /// </remarks>
    void EvaluatedKeywordPath<TProviderContext>(bool isMatch, TProviderContext providerContext, JsonSchemaMessageProvider<TProviderContext> messageProvider, JsonSchemaPathProvider<TProviderContext> encodedKeywordPath);

    /// <summary>
    /// Indicates that a schema keyword was ignored.
    /// </summary>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeyword">The keyword that is ignored.</param>
    void IgnoredKeyword(
        JsonSchemaMessageProvider? messageProvider,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Indicates that a schema keyword was ignored.
    /// </summary>
    /// <param name="providerContext">The context to provide to the message provider.</param>
    /// <param name="messageProvider">The (optional) provider for a JSON schema evaluation message.</param>
    /// <param name="encodedKeyword">The keyword that is ignored.</param>
    void IgnoredKeyword<TProviderContext>(
        TProviderContext providerContext,
        JsonSchemaMessageProvider<TProviderContext>? messageProvider,
        ReadOnlySpan<byte> encodedKeyword);

    /// <summary>
    /// Abandons the last child context.
    /// </summary>
    /// <param name="sequenceNumber">The sequence number of the child context to commit.</param>
    /// <remarks>
    /// This will not update the match state, and allows the collector to release any resources associated with the child context.
    /// </remarks>
    void PopChildContext(int sequenceNumber);
}