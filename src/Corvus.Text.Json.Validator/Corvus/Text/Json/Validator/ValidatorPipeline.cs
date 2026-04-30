// <copyright file="ValidatorPipeline.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Buffers;
using System.Text.Json;

namespace Corvus.Text.Json.Validator;

/// <summary>
/// Internal pipeline that validates JSON documents against a compiled schema type.
/// </summary>
internal abstract class ValidatorPipeline
{
    /// <summary>
    /// Creates a <see cref="ValidatorPipeline"/> for a boolean true schema.
    /// </summary>
    /// <param name="isAlwaysTrue">If <see langword="true"/>, the pipeline always returns <see langword="true"/>.</param>
    /// <param name="isAlwaysFalse">If <see langword="true"/>, the pipeline always returns <see langword="false"/>.</param>
    /// <returns>The <see cref="ValidatorPipeline"/>.</returns>
    public static ValidatorPipeline Create(bool isAlwaysTrue, bool isAlwaysFalse)
    {
        if (isAlwaysTrue)
        {
            return AlwaysTruePipeline.Instance;
        }

        return AlwaysFalsePipeline.Instance;
    }

    /// <summary>
    /// Creates a <see cref="ValidatorPipeline"/> backed by a <see cref="DynamicJsonType"/>.
    /// </summary>
    /// <param name="dynamicType">The compiled dynamic type.</param>
    /// <returns>The <see cref="ValidatorPipeline"/>.</returns>
    public static ValidatorPipeline Create(DynamicJsonType dynamicType)
    {
        return new DynamicTypePipeline(dynamicType);
    }

    /// <summary>
    /// Validate a JSON string.
    /// </summary>
    public abstract bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector);

    /// <summary>
    /// Validate UTF-8 JSON bytes.
    /// </summary>
    public abstract bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector);

    /// <summary>
    /// Validate a JSON char memory.
    /// </summary>
    public abstract bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector);

    /// <summary>
    /// Validate a UTF-8 JSON stream.
    /// </summary>
    public abstract bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector);

    /// <summary>
    /// Validate a UTF-8 JSON byte sequence.
    /// </summary>
    public abstract bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector);

    /// <summary>
    /// Validate a pre-parsed <see cref="JsonElement"/>.
    /// </summary>
    public abstract bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector);

    private sealed class AlwaysTruePipeline : ValidatorPipeline
    {
        public static readonly AlwaysTruePipeline Instance = new();

        public override bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector) => true;

        public override bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector) => true;

        public override bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector) => true;

        public override bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector) => true;

        public override bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector) => true;

        public override bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector) => true;
    }

    private sealed class AlwaysFalsePipeline : ValidatorPipeline
    {
        public static readonly AlwaysFalsePipeline Instance = new();

        public override bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector) => false;

        public override bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector) => false;

        public override bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector) => false;

        public override bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector) => false;

        public override bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector) => false;

        public override bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector) => false;
    }

    private sealed class DynamicTypePipeline(DynamicJsonType dynamicType) : ValidatorPipeline
    {
        public override bool Validate(string json, IJsonSchemaResultsCollector? resultsCollector)
        {
            using DynamicJsonElement element = dynamicType.ParseInstance(json);
            return element.EvaluateSchema(resultsCollector);
        }

        public override bool Validate(ReadOnlyMemory<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector)
        {
            using DynamicJsonElement element = dynamicType.ParseInstance(utf8Json);
            return element.EvaluateSchema(resultsCollector);
        }

        public override bool Validate(ReadOnlyMemory<char> json, IJsonSchemaResultsCollector? resultsCollector)
        {
            using DynamicJsonElement element = dynamicType.ParseInstance(json);
            return element.EvaluateSchema(resultsCollector);
        }

        public override bool Validate(Stream utf8Json, IJsonSchemaResultsCollector? resultsCollector)
        {
            using DynamicJsonElement element = dynamicType.ParseInstance(utf8Json);
            return element.EvaluateSchema(resultsCollector);
        }

        public override bool Validate(ReadOnlySequence<byte> utf8Json, IJsonSchemaResultsCollector? resultsCollector)
        {
            using DynamicJsonElement element = dynamicType.ParseInstance(utf8Json);
            return element.EvaluateSchema(resultsCollector);
        }

        public override bool Validate(in JsonElement element, IJsonSchemaResultsCollector? resultsCollector)
        {
            DynamicJsonElement dynamicElement = dynamicType.FromElement(element);
            return dynamicElement.EvaluateSchema(resultsCollector);
        }
    }
}