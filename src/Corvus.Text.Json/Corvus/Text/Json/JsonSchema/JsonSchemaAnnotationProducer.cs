// <copyright file="JsonSchemaAnnotationProducer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;
using System.Buffers;
using System.Collections.Generic;

namespace Corvus.Text.Json;

/// <summary>
/// Extracts JSON Schema annotations from a <see cref="JsonSchemaResultsCollector"/> that
/// has been used in <see cref="JsonSchemaResultsLevel.Verbose"/> mode.
/// </summary>
/// <remarks>
/// <para>
/// Annotations in JSON Schema are produced by keywords that do not perform validation but
/// instead attach metadata to the instance. When a <see cref="JsonSchemaResultsCollector"/> is
/// used in verbose mode, annotation-producing keywords emit results via
/// <c>IgnoredKeyword</c>. This class filters those results and extracts them as
/// structured annotation values.
/// </para>
/// <para>
/// Each annotation is identified by:
/// <list type="bullet">
/// <item><description><strong>Instance location:</strong> The JSON Pointer into the instance being validated (e.g., "", "/foo", "/items/0").</description></item>
/// <item><description><strong>Keyword:</strong> The annotation keyword name (e.g., "title", "description", "default").</description></item>
/// <item><description><strong>Schema location:</strong> The absolute JSON Pointer into the schema where the keyword appears (e.g., "#", "#/$defs/foo").</description></item>
/// <item><description><strong>Value:</strong> The raw JSON value of the annotation.</description></item>
/// </list>
/// </para>
/// </remarks>
public static class JsonSchemaAnnotationProducer
{
    private static ReadOnlySpan<byte> HashPrefix => "#"u8;

    /// <summary>
    /// Determines whether a byte is the start of a valid JSON value
    /// (string, number, true, false, null, array, or object).
    /// </summary>
    private static bool IsJsonValueStart(byte b)
    {
        return b is (byte)'"' or (byte)'{' or (byte)'[' or
            (byte)'t' or (byte)'f' or (byte)'n' or
            (byte)'-' or (byte)'0' or (byte)'1' or (byte)'2' or
            (byte)'3' or (byte)'4' or (byte)'5' or (byte)'6' or
            (byte)'7' or (byte)'8' or (byte)'9';
    }

    /// <summary>
    /// A callback invoked for each annotation found in the results.
    /// </summary>
    /// <param name="instanceLocation">The instance location as a JSON Pointer string.</param>
    /// <param name="keyword">The annotation keyword name.</param>
    /// <param name="schemaLocation">The schema location as a JSON Pointer string.</param>
    /// <param name="annotationValue">The raw JSON value of the annotation.</param>
    /// <returns><see langword="true"/> to continue enumeration; <see langword="false"/> to stop.</returns>
    public delegate bool AnnotationCallback(
        string instanceLocation,
        string keyword,
        string schemaLocation,
        string annotationValue);

    /// <summary>
    /// Enumerates annotations from the collector as an <see cref="AnnotationEnumerator"/>.
    /// </summary>
    /// <param name="collector">The results collector (must have been used in <see cref="JsonSchemaResultsLevel.Verbose"/> mode).</param>
    /// <returns>An enumerator over annotations.</returns>
    public static AnnotationEnumerator EnumerateAnnotations(
        JsonSchemaResultsCollector collector)
    {
        return new AnnotationEnumerator(collector);
    }

    /// <summary>
    /// Enumerates annotations from the collector, invoking the callback for each one.
    /// </summary>
    /// <param name="collector">The results collector (must have been used in <see cref="JsonSchemaResultsLevel.Verbose"/> mode).</param>
    /// <param name="callback">The callback to invoke for each annotation.</param>
    public static void EnumerateAnnotations(
        JsonSchemaResultsCollector collector,
        AnnotationCallback callback)
    {
        foreach (Annotation annotation in EnumerateAnnotations(collector))
        {
            string keyword = annotation.GetKeywordText();
            string instanceLocation = annotation.GetInstanceLocationText();
            string schemaLocation = annotation.GetSchemaLocationText();
            string annotationValue = annotation.GetValueText();

            if (!callback(instanceLocation, keyword, schemaLocation, annotationValue))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Writes all annotations from the collector as a JSON object to the given writer.
    /// </summary>
    /// <param name="collector">The results collector (must have been used in <see cref="JsonSchemaResultsLevel.Verbose"/> mode).</param>
    /// <param name="writer">The writer to which the annotations JSON will be written.</param>
    /// <remarks>
    /// <para>
    /// The output structure is:
    /// <code>
    /// {
    ///   "instanceLocation": {
    ///     "keyword": {
    ///       "#schemaLocation": annotationValue,
    ///       ...
    ///     },
    ///     ...
    ///   },
    ///   ...
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    public static void WriteAnnotationsTo(
        JsonSchemaResultsCollector collector,
        Utf8JsonWriter writer)
    {
        // Annotations from the same (instanceLocation, keyword) pair may not be contiguous
        // in collector order (they are interleaved with sub-schema evaluations). We collect
        // them first so the JSON output is correctly grouped.
        Dictionary<(string, string), Dictionary<string, string>> annotations = CollectAnnotations(collector);

        writer.WriteStartObject();

        // Group by instance location, then by keyword.
        string? currentInstanceLocation = null;

        foreach (KeyValuePair<(string InstanceLocation, string Keyword), Dictionary<string, string>> entry in annotations)
        {
            if (entry.Key.InstanceLocation != currentInstanceLocation)
            {
                if (currentInstanceLocation is not null)
                {
                    writer.WriteEndObject();
                }

                writer.WritePropertyName(entry.Key.InstanceLocation);
                writer.WriteStartObject();
                currentInstanceLocation = entry.Key.InstanceLocation;
            }

            writer.WritePropertyName(entry.Key.Keyword);
            writer.WriteStartObject();

            foreach (KeyValuePair<string, string> schemaEntry in entry.Value)
            {
                writer.WritePropertyName(schemaEntry.Key);
                writer.WriteRawValue(schemaEntry.Value, skipInputValidation: true);
            }

            writer.WriteEndObject();
        }

        if (currentInstanceLocation is not null)
        {
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Collects all annotations from the collector into a dictionary keyed by
    /// (instanceLocation, keyword), with values being a dictionary of
    /// schemaLocation → annotationValue.
    /// </summary>
    /// <param name="collector">The results collector (must have been used in <see cref="JsonSchemaResultsLevel.Verbose"/> mode).</param>
    /// <returns>A dictionary of annotations keyed by (instanceLocation, keyword).</returns>
    /// <remarks>
    /// This is a convenience method primarily intended for testing. For production use,
    /// prefer <see cref="EnumerateAnnotations(JsonSchemaResultsCollector)"/> or
    /// <see cref="WriteAnnotationsTo"/> to avoid unnecessary allocations.
    /// </remarks>
    public static Dictionary<(string InstanceLocation, string Keyword), Dictionary<string, string>> CollectAnnotations(
        JsonSchemaResultsCollector collector)
    {
        var result = new Dictionary<(string, string), Dictionary<string, string>>();

        foreach (Annotation annotation in EnumerateAnnotations(collector))
        {
            string instanceLocation = annotation.GetInstanceLocationText();
            string keyword = annotation.GetKeywordText();
            string schemaLocation = annotation.GetSchemaLocationText();
            string annotationValue = annotation.GetValueText();

            var key = (instanceLocation, keyword);
            if (!result.TryGetValue(key, out Dictionary<string, string>? schemaMap))
            {
                schemaMap = new Dictionary<string, string>();
                result[key] = schemaMap;
            }

            schemaMap[schemaLocation] = annotationValue;
        }

        return result;
    }

    /// <summary>
    /// Determines whether a <see cref="JsonSchemaResultsCollector.Result"/> represents an annotation.
    /// </summary>
    internal static bool IsAnnotation(in JsonSchemaResultsCollector.Result result, out ReadOnlySpan<byte> keyword)
    {
        keyword = default;

        if (!result.IsMatch)
        {
            return false;
        }

        ReadOnlySpan<byte> message = result.Message;
        if (message.Length == 0)
        {
            return false;
        }

        ReadOnlySpan<byte> evaluationLocation = result.EvaluationLocation;
        int lastSlash = evaluationLocation.LastIndexOf((byte)'/');
        if (lastSlash < 0)
        {
            return false;
        }

        // IgnoredKeyword pushes the keyword to EvaluationLocation but NOT to
        // SchemaEvaluationLocation. EvaluatedKeyword pushes to both. We only
        // want annotations (IgnoredKeyword), so skip results where the two
        // locations are equal — those are validation results.
        ReadOnlySpan<byte> schemaEvaluationLocation = result.SchemaEvaluationLocation;
        if (evaluationLocation.SequenceEqual(schemaEvaluationLocation))
        {
            return false;
        }

        keyword = evaluationLocation[(lastSlash + 1)..];
        if (keyword.Length == 0)
        {
            return false;
        }

        // Annotation values are raw JSON. Diagnostic messages from IgnoredKeyword
        // (e.g., "The type was not 'object'") are NOT valid JSON.
        return IsJsonValueStart(message[0]);
    }

    /// <summary>
    /// Represents a single annotation extracted from evaluation results.
    /// </summary>
    /// <remarks>
    /// This is a <see langword="ref"/> <see langword="struct"/> whose spans reference
    /// the internal buffers of the <see cref="JsonSchemaResultsCollector"/>. It is only
    /// valid during enumeration and must not be stored beyond the current iteration.
    /// </remarks>
    public readonly ref struct Annotation
    {
        private readonly ReadOnlySpan<byte> instanceLocation;
        private readonly ReadOnlySpan<byte> keyword;
        private readonly ReadOnlySpan<byte> schemaLocation;
        private readonly ReadOnlySpan<byte> value;

        /// <summary>
        /// Initializes a new instance of the <see cref="Annotation"/> struct.
        /// </summary>
        /// <param name="instanceLocation">The instance location as a UTF-8 JSON Pointer.</param>
        /// <param name="keyword">The annotation keyword name as UTF-8.</param>
        /// <param name="schemaLocation">The schema location as a UTF-8 JSON Pointer (without '#' prefix).</param>
        /// <param name="value">The raw JSON annotation value as UTF-8.</param>
        internal Annotation(
            ReadOnlySpan<byte> instanceLocation,
            ReadOnlySpan<byte> keyword,
            ReadOnlySpan<byte> schemaLocation,
            ReadOnlySpan<byte> value)
        {
            this.instanceLocation = instanceLocation;
            this.keyword = keyword;
            this.schemaLocation = schemaLocation;
            this.value = value;
        }

        /// <summary>
        /// Gets the instance location as a UTF-8 JSON Pointer (e.g., "", "/foo", "/items/0").
        /// </summary>
        public ReadOnlySpan<byte> InstanceLocation => this.instanceLocation;

        /// <summary>
        /// Gets the annotation keyword name as UTF-8 (e.g., "title", "description").
        /// </summary>
        public ReadOnlySpan<byte> Keyword => this.keyword;

        /// <summary>
        /// Gets the schema location as a UTF-8 JSON Pointer (without the '#' prefix).
        /// </summary>
        public ReadOnlySpan<byte> SchemaLocation => this.schemaLocation;

        /// <summary>
        /// Gets the raw JSON annotation value as UTF-8.
        /// </summary>
        public ReadOnlySpan<byte> Value => this.value;

        /// <summary>
        /// Writes the raw JSON annotation value to the given writer.
        /// </summary>
        /// <param name="writer">The writer to which the value will be written.</param>
        public void WriteValueTo(Utf8JsonWriter writer)
        {
            writer.WriteRawValue(this.value, skipInputValidation: true);
        }

        /// <summary>
        /// Writes a JSON property with the schema location (prefixed with '#') as the
        /// property name and the annotation value as the raw JSON value.
        /// </summary>
        /// <param name="writer">The writer to which the property will be written.</param>
        public void WriteSchemaLocationPropertyTo(Utf8JsonWriter writer)
        {
            // Build "#" + schemaLocation as the property name.
            int totalLength = 1 + this.schemaLocation.Length;

            byte[]? rented = null;
            Span<byte> propertyName = totalLength <= JsonConstants.StackallocByteThreshold
                ? stackalloc byte[JsonConstants.StackallocByteThreshold]
                : (rented = ArrayPool<byte>.Shared.Rent(totalLength));

            try
            {
                HashPrefix.CopyTo(propertyName);
                this.schemaLocation.CopyTo(propertyName[1..]);

                writer.WritePropertyName(propertyName[..totalLength]);
                writer.WriteRawValue(this.value, skipInputValidation: true);
            }
            finally
            {
                if (rented is not null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// Gets the instance location as a string.
        /// </summary>
        /// <returns>The instance location text.</returns>
        public string GetInstanceLocationText()
        {
            return JsonReaderHelper.GetTextFromUtf8(this.instanceLocation);
        }

        /// <summary>
        /// Gets the keyword name as a string.
        /// </summary>
        /// <returns>The keyword text.</returns>
        public string GetKeywordText()
        {
            return JsonReaderHelper.GetTextFromUtf8(this.keyword);
        }

        /// <summary>
        /// Gets the schema location as a string, with the '#' prefix.
        /// </summary>
        /// <returns>The schema location text.</returns>
        public string GetSchemaLocationText()
        {
            return "#" + JsonReaderHelper.GetTextFromUtf8(this.schemaLocation);
        }

        /// <summary>
        /// Gets the annotation value as a string.
        /// </summary>
        /// <returns>The annotation value text.</returns>
        public string GetValueText()
        {
            return JsonReaderHelper.GetTextFromUtf8(this.value);
        }
    }

    /// <summary>
    /// A forward-only enumerator over annotations in a <see cref="JsonSchemaResultsCollector"/>.
    /// </summary>
    /// <remarks>
    /// This is a <see langword="ref"/> <see langword="struct"/> that filters the collector's
    /// results to yield only annotation entries.
    /// </remarks>
    public ref struct AnnotationEnumerator
    {
        private JsonSchemaResultsCollector.ResultsEnumerator inner;
        private Annotation current;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnnotationEnumerator"/> struct.
        /// </summary>
        /// <param name="collector">The collector to enumerate.</param>
        internal AnnotationEnumerator(JsonSchemaResultsCollector collector)
        {
            this.inner = collector.EnumerateResults();
            this.current = default;
        }

        /// <summary>
        /// Gets the current annotation.
        /// </summary>
        public Annotation Current => this.current;

        /// <summary>
        /// Advances to the next annotation.
        /// </summary>
        /// <returns><see langword="true"/> if an annotation was found; otherwise <see langword="false"/>.</returns>
        public bool MoveNext()
        {
            while (this.inner.MoveNext())
            {
                JsonSchemaResultsCollector.Result result = this.inner.Current;

                if (!result.IsMatch)
                {
                    continue;
                }

                ReadOnlySpan<byte> message = result.Message;
                if (message.Length == 0)
                {
                    continue;
                }

                ReadOnlySpan<byte> evaluationLocation = result.EvaluationLocation;
                int lastSlash = evaluationLocation.LastIndexOf((byte)'/');
                if (lastSlash < 0)
                {
                    continue;
                }

                ReadOnlySpan<byte> schemaEvaluationLocation = result.SchemaEvaluationLocation;
                if (evaluationLocation.SequenceEqual(schemaEvaluationLocation))
                {
                    continue;
                }

                ReadOnlySpan<byte> keyword = evaluationLocation[(lastSlash + 1)..];
                if (keyword.Length == 0)
                {
                    continue;
                }

                // Annotation values are raw JSON (strings, numbers, booleans, arrays, objects).
                // Diagnostic messages from IgnoredKeyword (e.g., "The type was not 'object'")
                // are NOT valid JSON. Filter them by checking the first byte is a valid JSON
                // value start character.
                if (!IsJsonValueStart(message[0]))
                {
                    continue;
                }

                this.current = new Annotation(
                    result.DocumentEvaluationLocation,
                    keyword,
                    schemaEvaluationLocation,
                    message);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns this enumerator (enables <see langword="foreach"/>).
        /// </summary>
        /// <returns>This enumerator.</returns>
        public AnnotationEnumerator GetEnumerator() => this;
    }
}