// <copyright file="JsonElementForBooleanFalseSchema.JsonSchema.cs" company="Endjin Limited">
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
/// Represents a placeholder for the <c>false</c> boolean schema which disallows any value.
/// </summary>
public readonly partial struct JsonElementForBooleanFalseSchema
{
    /// <summary>
    /// Evaluates this element against the boolean false schema.
    /// </summary>
    /// <param name="resultsCollector">The optional results collector for schema evaluation.</param>
    /// <returns><see langword="false"/> because this represents a boolean false schema.</returns>
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
        public static readonly JsonSchemaPathProvider SchemaLocationProvider = static (buffer, out written) => { written = 0; return true; };

        public const string SchemaLocation = "";

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

        [CLSCompliant(false)]
        public static void Evaluate(IJsonDocument parentDocument, int parentIndex, ref JsonSchemaContext context)
        {
            // You're not allowed to ask about non-value-like entities
            Debug.Assert(parentDocument.GetJsonTokenType(parentIndex) is not
                (JsonTokenType.None or
                JsonTokenType.EndObject or
                JsonTokenType.EndArray or
                JsonTokenType.PropertyName));

            context.EvaluatedBooleanSchema(false);
        }

        [CLSCompliant(false)]
        public static bool Evaluate(IJsonDocument parentDocument, int parentIndex, IJsonSchemaResultsCollector? resultsCollector)
        {
            return false;
        }
    }
}