// <copyright file="JsonElement.JsonSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// Represents a specific JSON value within a <see cref="JsonDocument"/>.
/// </summary>
public readonly partial struct JsonElement
{
    /// <summary>
    /// Evaluates the JSON Schema for this element.
    /// </summary>
    /// <param name="resultsCollector">The (optional) results collector for schema validation.</param>
    /// <returns><see langword="true"/> if the element is valid according to its schema; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null)
    {
        return JsonSchema.Evaluate(_parent, _idx, resultsCollector);
    }

    /// <summary>
    /// JSON Schema support for the <see cref="JsonElement"/>
    /// </summary>
    public static class JsonSchema
    {
        /// <summary>
        /// Gets the schema location provider for this element type.
        /// </summary>
        public static readonly JsonSchemaPathProvider SchemaLocationProvider = static (buffer, out written) => { written = 0; return true; };

        /// <summary>
        /// Gets the schema location for this element type.
        /// </summary>
        public const string SchemaLocation = "";

        /// <summary>
        /// Gets the schema location for this element type as a UTF-8 byte span.
        /// </summary>
        public static ReadOnlySpan<byte> SchemaLocationUtf8 => ""u8;

        /// <summary>
        /// Push the current context as a child context for the <see cref="JsonElement"/> schema evaluation.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentDocumentIndex">The index in the parent document for the instance.</param>
        /// <param name="context">The current evaluation context.</param>
        /// <param name="schemaEvaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
        /// <param name="documentEvaluationPath">The (optional) path in the document being evaluated in the child context.</param>
        /// <returns>The child context.</returns>
        [CLSCompliant(false)]
        public static JsonSchemaContext PushChildContext(
            IJsonDocument parentDocument,
            int parentDocumentIndex,
            ref JsonSchemaContext context,
            JsonSchemaPathProvider? schemaEvaluationPath = null,
            JsonSchemaPathProvider? documentEvaluationPath = null)
        {
            return
                context.PushChildContext(
                    parentDocument,
                    parentDocumentIndex,
                    useEvaluatedItems: false, // We don't use evaluated items
                    useEvaluatedProperties: false,
                    schemaEvaluationPath: schemaEvaluationPath,
                    documentEvaluationPath: documentEvaluationPath);
        }

        /// <summary>
        /// Push the current context as a child context for the <see cref="JsonElement"/> schema evaluation.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to be passed to the path providers.</typeparam>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentDocumentIndex">The index in the parent document for the instance.</param>
        /// <param name="context">The current evaluation context.</param>
        /// <param name="providerContext">The context to be passed to the path providers.</param>
        /// <param name="schemaEvaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
        /// <param name="documentEvaluationPath">The (optional) path in the document being evaluated in the child context.</param>
        /// <returns>The child context.</returns>
        [CLSCompliant(false)]
        public static JsonSchemaContext PushChildContext<TContext>(
            IJsonDocument parentDocument,
            int parentDocumentIndex,
            ref JsonSchemaContext context,
            TContext providerContext,
            JsonSchemaPathProvider<TContext>? schemaEvaluationPath = null,
            JsonSchemaPathProvider<TContext>? documentEvaluationPath = null)
        {
            return
                context.PushChildContext(
                    parentDocument,
                    parentDocumentIndex,
                    useEvaluatedItems: false, // We don't use evaluated items
                    useEvaluatedProperties: false,
                    evaluationPath: schemaEvaluationPath,
                    documentEvaluationPath: documentEvaluationPath,
                    providerContext: providerContext);
        }

        /// <summary>
        /// Push the current context as a child context for the <see cref="JsonElement"/> schema evaluation, navigating to a property.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentDocumentIndex">The index in the parent document for the instance.</param>
        /// <param name="context">The current evaluation context.</param>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="evaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
        /// <returns>The child context.</returns>
        [CLSCompliant(false)]
        public static JsonSchemaContext PushChildContext(
            IJsonDocument parentDocument,
            int parentDocumentIndex,
            ref JsonSchemaContext context,
            ReadOnlySpan<byte> propertyName,
            JsonSchemaPathProvider? evaluationPath = null)
        {
            return
                context.PushChildContext(
                    parentDocument,
                    parentDocumentIndex,
                    useEvaluatedItems: false, // We don't use evaluated items
                    useEvaluatedProperties: false,
                    propertyName,
                    evaluationPath: evaluationPath,
                    schemaEvaluationPath: SchemaLocationProvider);
        }

        /// <summary>
        /// Push the current context as a child context for the <see cref="JsonElement"/> schema evaluation, navigating to an array item.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentDocumentIndex">The index in the parent document for the instance.</param>
        /// <param name="context">The current evaluation context.</param>
        /// <param name="itemIndex">The array item index.</param>
        /// <param name="evaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
        /// <returns>The child context.</returns>
        [CLSCompliant(false)]
        public static JsonSchemaContext PushChildContext(
            IJsonDocument parentDocument,
            int parentDocumentIndex,
            ref JsonSchemaContext context,
            int itemIndex,
            JsonSchemaPathProvider? evaluationPath = null)
        {
            return
                context.PushChildContext(
                    parentDocument,
                    parentDocumentIndex,
                    useEvaluatedItems: false, // We don't use evaluated items
                    useEvaluatedProperties: false,
                    itemIndex,
                    evaluationPath: evaluationPath,
                    schemaEvaluationPath: SchemaLocationProvider);
        }

        /// <summary>
        /// Push the current context as a child context for the <see cref="JsonElement"/> schema evaluation, navigating to a property using an unescaped property name.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentDocumentIndex">The index in the parent document for the instance.</param>
        /// <param name="context">The current evaluation context.</param>
        /// <param name="propertyName">The unescaped property name as a UTF-8 byte span.</param>
        /// <param name="evaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
        /// <returns>The child context.</returns>
        [CLSCompliant(false)]
        public static JsonSchemaContext PushChildContextUnescaped(
            IJsonDocument parentDocument,
            int parentDocumentIndex,
            ref JsonSchemaContext context,
            ReadOnlySpan<byte> propertyName,
            JsonSchemaPathProvider? evaluationPath = null)
        {
            return
                context.PushChildContextUnescaped(
                    parentDocument,
                    parentDocumentIndex,
                    useEvaluatedItems: false, // We don't use evaluated items
                    useEvaluatedProperties: false,
                    propertyName,
                    evaluationPath: evaluationPath,
                    schemaEvaluationPath: SchemaLocationProvider);
        }

        /// <summary>
        /// Evaluates the element against this schema type.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentIndex">The index of the element in the parent document.</param>
        /// <param name="context">The schema evaluation context.</param>
        [CLSCompliant(false)]
        public static void Evaluate(IJsonDocument parentDocument, int parentIndex, ref JsonSchemaContext context)
        {
            // You're not allowed to ask about non-value-like entities
            Debug.Assert(parentDocument.GetJsonTokenType(parentIndex) is not
                (JsonTokenType.None or
                JsonTokenType.EndObject or
                JsonTokenType.EndArray or
                JsonTokenType.PropertyName));

            context.EvaluatedBooleanSchema(true);
        }

        /// <summary>
        /// Evaluates the element against this schema type.
        /// </summary>
        /// <param name="parentDocument">The parent document for the instance.</param>
        /// <param name="parentIndex">The index of the element in the parent document.</param>
        /// <param name="resultsCollector">The (optional) results collector for schema validation.</param>
        /// <returns><see langword="true"/> if the value is valid according to this schema; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        public static bool Evaluate(IJsonDocument parentDocument, int parentIndex, IJsonSchemaResultsCollector? resultsCollector)
        {
            return true;
        }
    }
}