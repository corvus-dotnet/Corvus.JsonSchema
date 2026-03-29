// <copyright file="Polyfills.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.IO;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Compatibility;

/// <summary>
/// Provides polyfills for Corvus.JsonSchema API compatibility.
/// </summary>
[CLSCompliant(false)]
public static class Polyfills
{
    private static class Instances<T>
        where T : struct, IJsonElement<T>
    {
        public static readonly T NullInstance = ParsedJsonDocument<T>.ParseValue("null", default).RootElement;
    }

    extension<T>(T element)
        where T : struct, IJsonElement<T>
    {
        /// <summary>
        /// Gets a value indicating whether this instance is valid according to its schema.
        /// </summary>
        /// <returns><see langword="true"/> if the instance is valid.</returns>
        public bool IsValid()
        {
            return element.EvaluateSchema();
        }

        /// <summary>
        /// Gets a value indicating whether this instance has .NET backing.
        /// </summary>
        public bool HasDotnetBacking => false;

        /// <summary>
        /// Gets a value indicating whether this instance has JsonElement backing.
        /// </summary>
        public bool HasJsonElementBacking => true;

        /// <summary>
        /// Gets a null instance of this type.
        /// </summary>
        public static T Null => Instances<T>.NullInstance;

        /// <summary>
        /// Gets an undefined instance of this type.
        /// </summary>
        public static T Undefined => default;

        /// <summary>
        /// Gets this instance as a <see cref="JsonElement"/>.
        /// </summary>
        public JsonElement AsJsonElement => new(element.ParentDocument, element.ParentDocumentIndex);

        /// <summary>
        /// Gets this instance as a <see cref="JsonElement"/> (equivalent to JsonAny in Corvus.JsonSchema).
        /// </summary>
        // JsonElement is the equivalent of JsonAny in Corvus.JsonSchema
        public JsonElement AsAny => new(element.ParentDocument, element.ParentDocumentIndex);

        /// <summary>
        /// Parses a JSON string and returns an instance of the type.
        /// </summary>
        /// <param name="value">The JSON string to parse.</param>
        /// <param name="options">The JSON document options.</param>
        /// <returns>An instance of the type.</returns>
        public static T Parse(string value, JsonDocumentOptions options = default)
        {
            // This is the unrented path/
            var document = ParsedJsonDocument<T>.ParseValue(value, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses a JSON stream and returns an instance of the type.
        /// </summary>
        /// <param name="value">The JSON stream to parse.</param>
        /// <param name="options">The JSON document options.</param>
        /// <returns>An instance of the type.</returns>
        public static T Parse(Stream value, JsonDocumentOptions options = default)
        {
            // This is the unrented path/
            var document = ParsedJsonDocument<T>.ParseValue(value, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses JSON from a memory buffer and returns an instance of the type.
        /// </summary>
        /// <param name="value">The JSON memory buffer to parse.</param>
        /// <param name="options">The JSON document options.</param>
        /// <returns>An instance of the type.</returns>
        public static T Parse(ReadOnlyMemory<byte> value, JsonDocumentOptions options = default)
        {
            // This is the unrented path/
            var document = ParsedJsonDocument<T>.ParseValue(value.Span, options);
            return document.RootElement;
        }

        /// <summary>
        /// Parses JSON from a character memory buffer and returns an instance of the type.
        /// </summary>
        /// <param name="value">The JSON character memory buffer to parse.</param>
        /// <param name="options">The JSON document options.</param>
        /// <returns>An instance of the type.</returns>
        public static T Parse(ReadOnlyMemory<char> value, JsonDocumentOptions options = default)
        {
            // This is the unrented path
            var document = ParsedJsonDocument<T>.ParseValue(value.Span, options);
            return document.RootElement;
        }

        /// <summary>
        /// Creates an instance of this type from another JSON element type.
        /// </summary>
        /// <typeparam name="TTarget">The type of the source JSON element.</typeparam>
        /// <param name="value">The source JSON element.</param>
        /// <returns>An instance of this type.</returns>
        public static T FromJson<TTarget>(in TTarget value)
            where TTarget : struct, IJsonElement<TTarget>
        {
#if NET
            return T.CreateInstance(value.ParentDocument, value.ParentDocumentIndex);
#else
           return JsonElementHelpers.CreateInstance<T>(value.ParentDocument, value.ParentDocumentIndex);
#endif
        }

        /// <summary>
        /// Converts this instance to the specified target type.
        /// </summary>
        /// <typeparam name="TTarget">The target JSON element type.</typeparam>
        /// <returns>An instance of the target type.</returns>
        public TTarget As<TTarget>()
            where TTarget : struct, IJsonElement<TTarget>
        {
#if NET
            return TTarget.CreateInstance(element.ParentDocument, element.ParentDocumentIndex);
#else
           return JsonElementHelpers.CreateInstance<TTarget>(element.ParentDocument, element.ParentDocumentIndex);
#endif
        }

        /// <summary>
        /// Validates the instance against its own schema.
        /// </summary>
        /// <param name="context">The current validation context.</param>
        /// <param name="validationLevel">The validation level. (Defaults to <see cref="ValidationLevel.Flag"/>).</param>
        /// <returns>The <see cref="ValidationContext"/> updated with the results from this validation operation.</returns>
        public ValidationContext Validate(in ValidationContext context, ValidationLevel validationLevel = ValidationLevel.Flag)
        {
            if (validationLevel == ValidationLevel.Flag)
            {
                return new(element.EvaluateSchema());
            }

            JsonSchemaResultsCollector collector = context.Collector ?? JsonSchemaResultsCollector.Create(ValidationContext.MapLevel(validationLevel));
            bool isMatch = element.EvaluateSchema(collector);
            return new(isMatch && context.IsValid, collector);
        }
    }
}