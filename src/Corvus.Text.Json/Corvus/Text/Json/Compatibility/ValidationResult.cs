// <copyright file="ValidationResult.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;

namespace Corvus.Text.Json.Compatibility;

/// <summary>
/// Represents the result of a single JSON schema validation, including validity, message, and locations.
/// </summary>
public readonly struct ValidationResult
{
    private readonly JsonSchemaResultsCollector _collector;

    private readonly int _resultIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> struct.
    /// </summary>
    /// <param name="collector">The results collector containing the validation data.</param>
    /// <param name="resultIndex">The index of the result within the collector.</param>
    internal ValidationResult(JsonSchemaResultsCollector collector, int resultIndex)
    {
        Debug.Assert(resultIndex >= 0);

        _collector = collector;
        _resultIndex = resultIndex;
    }

    /// <summary>
    /// Gets the location information for this validation result.
    /// </summary>
    public LocationTuple Location
    {
        get
        {
            JsonSchemaResultsCollector.Result result = _collector.ReadResult(_resultIndex);
            return new LocationTuple(result.EvaluationLocation, result.SchemaEvaluationLocation, result.DocumentEvaluationLocation);
        }
    }

    /// <summary>
    /// Gets the validation message for this result, if any.
    /// </summary>
    public string? Message
    {
        get
        {
            JsonSchemaResultsCollector.Result result = _collector.ReadResult(_resultIndex);
            return result.GetMessageText();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the validation result is valid.
    /// </summary>
    public bool Valid
    {
        get
        {
            JsonSchemaResultsCollector.Result result = _collector.ReadResult(_resultIndex);
            return result.IsMatch;
        }
    }

    /// <summary>
    /// Represents the locations associated with a validation result, including validation, schema, and document locations.
    /// </summary>
    public readonly ref struct LocationTuple
    {
        private readonly ReadOnlySpan<byte> _documentLocation;

        private readonly ReadOnlySpan<byte> _schemaLocation;

        private readonly ReadOnlySpan<byte> _validationLocation;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocationTuple"/> struct.
        /// </summary>
        /// <param name="validationLocation">The validation location.</param>
        /// <param name="schemaLocation">The schema location.</param>
        /// <param name="documentLocation">The document location.</param>
        internal LocationTuple(ReadOnlySpan<byte> validationLocation, ReadOnlySpan<byte> schemaLocation, ReadOnlySpan<byte> documentLocation)
        {
            _validationLocation = validationLocation;
            _schemaLocation = schemaLocation;
            _documentLocation = documentLocation;
        }

        /// <summary>
        /// Gets the document location as a JSON reference.
        /// </summary>
        public Utf8IriReference DocumentLocation => Utf8IriReference.CreateIriReference(_documentLocation);

        /// <summary>
        /// Gets the schema location as a JSON reference.
        /// </summary>
        public Utf8IriReference SchemaLocation => Utf8IriReference.CreateIriReference(_schemaLocation);

        /// <summary>
        /// Gets the validation location as a JSON reference.
        /// </summary>
        public Utf8IriReference ValidationLocation => Utf8IriReference.CreateIriReference(_validationLocation);
    }
}