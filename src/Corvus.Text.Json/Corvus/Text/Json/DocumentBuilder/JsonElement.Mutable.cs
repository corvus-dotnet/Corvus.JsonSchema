// <copyright file="JsonElement.Mutable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using static Corvus.Text.Json.JsonElement;

namespace Corvus.Text.Json;

public readonly partial struct JsonElement
{
    private static readonly StandardFormat s_dateTimeStandardFormat = new('O');

    /// <summary>
    /// Represents a source for creating mutable JSON elements from various value types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Source"/> ref struct provides a unified abstraction for converting .NET values
    /// into JSON element sources, enabling type-safe and efficient construction of mutable JSON documents
    /// through the <see cref="JsonDocumentBuilder{T}"/> infrastructure.
    /// </para>
    /// <para>
    /// <strong>Core Architecture:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Type Abstraction:</strong> Unified interface for 40+ .NET types including primitives, strings, dates, and custom types</description></item>
    /// <item><description><strong>Value Storage:</strong> Optimized backing stores for different value categories (simple types, UTF-8/UTF-16 strings, complex builders)</description></item>
    /// <item><description><strong>Kind Classification:</strong> Internal discrimination system for efficient dispatch during JSON construction</description></item>
    /// <item><description><strong>Zero-Copy Design:</strong> Direct span-based operations for string and numeric data where possible</description></item>
    /// </list>
    /// <para>
    /// <strong>Integration with JsonDocumentBuilder:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="CreateBuilder"/> creates document builders from Source instances</description></item>
    /// <item><description><see cref="AddAsProperty"/> and <see cref="AddAsItem"/> integrate with <see cref="ComplexValueBuilder"/> for nested structures</description></item>
    /// <item><description>Automatic format selection and optimization based on value type and characteristics</description></item>
    /// </list>
    /// <para>
    /// <strong>Performance Characteristics:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Stack-allocated ref struct with minimal heap allocations</description></item>
    /// <item><description>Direct UTF-8 formatting for numeric types using <see cref="SimpleTypesBacking"/></description></item>
    /// <item><description>Efficient span-based string handling with escape analysis</description></item>
    /// <item><description>Lazy evaluation and format-on-demand for complex values</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <para>Basic value construction:</para>
    /// <code>
    /// using JsonWorkspace workspace = JsonWorkspace.Create();

    ///
    /// // Numeric values
    /// using var intDoc = JsonElement.CreateBuilder(workspace, 42);

    /// using var doubleDoc = JsonElement.CreateBuilder(workspace, 3.14159);

    ///
    /// // String values
    /// using var stringDoc = JsonElement.CreateBuilder(workspace, "Hello, World!");

    /// using var utf8Doc = JsonElement.CreateBuilder(workspace, "Hello"u8);

    ///
    /// // Boolean and null
    /// using var boolDoc = JsonElement.CreateBuilder(workspace, true);

    /// using var nullDoc = JsonElement.CreateBuilder(workspace, JsonElement.Source.Null());

    /// </code>
    /// </example>
    /// <example>
    /// <para>Complex object construction:</para>
    /// <code>
    /// using var objectDoc = JsonElement.CreateBuilder(workspace,
    /// new((ref JsonObjectBuilder objBuilder) =>
    /// {
    /// objBuilder.Add("name", "John Doe");

    /// objBuilder.Add("age", 30);

    /// objBuilder.Add("active", true);

    /// objBuilder.Add("metadata", new((ref JsonObjectBuilder metaBuilder) =>
    /// {
    /// metaBuilder.Add("created", DateTime.UtcNow);

    /// metaBuilder.Add("version", 1);

    /// }));

    /// }));

    /// </code>
    /// </example>
    /// <example>
    /// <para>Array construction:</para>
    /// <code>
    /// using var arrayDoc = JsonElement.CreateBuilder(workspace,
    /// new((ref JsonArrayBuilder arrayBuilder) =>
    /// {
    /// arrayBuilder.AddItem(1);

    /// arrayBuilder.AddItem("two");

    /// arrayBuilder.AddItem(3.0);

    /// arrayBuilder.AddItem(new((ref JsonObjectBuilder objBuilder) =>
    /// {
    /// objBuilder.AddProperty("nested", true);

    /// }));

    /// }));

    /// </code>
    /// </example>
    public readonly ref struct Source
    {
        /// <summary>
        /// Discriminates the type and storage mechanism for the source value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The Kind enum enables efficient dispatch during JSON construction by categorizing
        /// values based on their storage requirements and formatting needs:
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Simple Values:</strong> Null, True, False - stored as kind only</description></item>
        /// <item><description><strong>Numeric Types:</strong> NumericSimpleType - formatted via SimpleTypesBacking</description></item>
        /// <item><description><strong>String Types:</strong> Multiple variants for UTF-8/UTF-16, escaped/unescaped</description></item>
        /// <item><description><strong>Complex Types:</strong> Array and object builders for nested structures</description></item>
        /// </list>
        /// </remarks>
        private enum Kind
        {
            // We only need to include the kinds we
            // actually need!
            Unknown,

            JsonElement,
            Null,
            True,
            False,
            NumericSimpleType,
            FormattedNumber,
            StringSimpleType,
            RawUtf8StringRequiresUnescaping,
            RawUtf8StringNotRequiresUnescaping,
            Utf8String,
            Utf16String,

            // Add additional kinds for composed schema builders
            JsonArrayBuilderInstance,

            JsonObjectBuilderInstance,
        }

        private readonly Kind _kind;

        private readonly JsonElement _jsonElement;

        private readonly SimpleTypesBacking _simpleTypeBacking;

        private readonly ReadOnlySpan<byte> _utf8Backing;

        private readonly ReadOnlySpan<char> _utf16Backing;

        private readonly ArrayBuilder.Build? _arrayBuilder;

        private readonly ObjectBuilder.Build? _objectBuilder;

        /// <summary>
        /// Gets a value indicating whether the source value is undefined (not set).
        /// </summary>
        public bool IsUndefined => _kind == Kind.Unknown;

        private Source(JsonElement jsonElement)
        {
            _jsonElement = jsonElement;
            _kind = jsonElement.TokenType == JsonTokenType.None ? Kind.Unknown : Kind.JsonElement;
        }

        private Source(Kind kind)
        {
            Debug.Assert(kind is Kind.Null or Kind.True or Kind.False);
            _kind = kind;
        }

        private Source(ReadOnlySpan<byte> value)
        {
            _utf8Backing = value;
            _kind = Kind.Utf8String;
        }

        private Source(ReadOnlySpan<char> value)
        {
            _utf16Backing = value;
            _kind = Kind.Utf16String;
        }

        private Source(ReadOnlySpan<byte> value, Kind kind)
        {
            Debug.Assert(kind is Kind.FormattedNumber);

            _utf8Backing = value;
            _kind = kind;
        }

        private Source(ReadOnlySpan<byte> value, bool requiresUnescaping)
        {
            _utf8Backing = value;
            _kind = requiresUnescaping ? Kind.RawUtf8StringRequiresUnescaping : Kind.RawUtf8StringNotRequiresUnescaping;
        }

        private Source(long value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(int value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(short value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(sbyte value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(ulong value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(uint value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(ushort value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a byte value.
        /// </summary>
        /// <param name="value">The byte value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// This constructor uses <see cref="SimpleTypesBacking"/> to efficiently format the numeric value
        /// into UTF-8 bytes using <see cref="Utf8Formatter.TryFormat(byte, Span{byte}, out int)"/>.
        /// The formatted bytes are stored in a stack-allocated buffer for zero-allocation JSON generation.
        /// </para>
        /// <para>
        /// <strong>Performance Notes:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>No heap allocations during construction or formatting</description></item>
        /// <item><description>Direct UTF-8 output eliminates string conversion overhead</description></item>
        /// <item><description>Uses optimized formatting paths from System.Buffers.Text</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a JSON number without quotes.
        /// </para>
        /// </remarks>
        private Source(byte value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a decimal value.
        /// </summary>
        /// <param name="value">The decimal value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// This constructor uses <see cref="SimpleTypesBacking"/> to efficiently format the numeric value
        /// into UTF-8 bytes using <see cref="Utf8Formatter.TryFormat(decimal, Span{byte}, out int)"/>.
        /// The formatted bytes are stored in a stack-allocated buffer for zero-allocation JSON generation.
        /// </para>
        /// <para>
        /// <strong>Performance Notes:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>No heap allocations during construction or formatting</description></item>
        /// <item><description>Direct UTF-8 output eliminates string conversion overhead</description></item>
        /// <item><description>Uses optimized formatting paths from System.Buffers.Text</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a JSON number without quotes.
        /// </para>
        /// </remarks>
        private Source(decimal value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a double value.
        /// </summary>
        /// <param name="value">The double value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// This constructor uses <see cref="SimpleTypesBacking"/> to efficiently format the numeric value
        /// into UTF-8 bytes using <see cref="Utf8Formatter.TryFormat(double, Span{byte}, out int)"/>.
        /// The formatted bytes are stored in a stack-allocated buffer for zero-allocation JSON generation.
        /// </para>
        /// <para>
        /// <strong>Performance Notes:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>No heap allocations during construction or formatting</description></item>
        /// <item><description>Direct UTF-8 output eliminates string conversion overhead</description></item>
        /// <item><description>Uses optimized formatting paths from System.Buffers.Text</description></item>
        /// </list>
        /// <para>
        /// <strong>Special Values:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>NaN values are formatted as "NaN" (not valid JSON, use with caution)</description></item>
        /// <item><description>Infinity values are formatted as "Infinity" or "-Infinity"</description></item>
        /// <item><description>Consider validation for JSON compliance in strict scenarios</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a JSON number without quotes.
        /// </para>
        /// </remarks>
        private Source(double value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a float value.
        /// </summary>
        /// <param name="value">The float value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// This constructor uses <see cref="SimpleTypesBacking"/> to efficiently format the numeric value
        /// into UTF-8 bytes using <see cref="Utf8Formatter.TryFormat(float, Span{byte}, out int)"/>.
        /// The formatted bytes are stored in a stack-allocated buffer for zero-allocation JSON generation.
        /// </para>
        /// <para>
        /// <strong>Performance Notes:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>No heap allocations during construction or formatting</description></item>
        /// <item><description>Direct UTF-8 output eliminates string conversion overhead</description></item>
        /// <item><description>Uses optimized formatting paths from System.Buffers.Text</description></item>
        /// </list>
        /// <para>
        /// <strong>Special Values:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>NaN values are formatted as "NaN" (not valid JSON, use with caution)</description></item>
        /// <item><description>Infinity values are formatted as "Infinity" or "-Infinity"</description></item>
        /// <item><description>Consider validation for JSON compliance in strict scenarios</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a JSON number without quotes.
        /// </para>
        /// </remarks>
        private Source(float value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a DateTimeOffset value.
        /// </summary>
        /// <param name="value">The DateTimeOffset value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// Date and time values are formatted as JSON strings using specialized formatting logic.
        /// The value is pre-formatted into UTF-8 bytes and stored with <see cref="Kind.StringSimpleType"/>
        /// to indicate string representation in the final JSON.
        /// </para>
        /// <para>
        /// <strong>Formatting Behavior:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Uses <see cref="Utf8Formatter.TryFormat(DateTimeOffset, Span{byte}, out int)"/> with ISO 8601 format</description></item>
        /// <item><description>Output includes surrounding quotes as per JSON string specification</description></item>
        /// <item><description>Preserves timezone offset information in the formatted output</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a quoted JSON string with ISO 8601 format
        /// including timezone offset (e.g., "2023-07-20T10:30:00+00:00").
        /// </para>
        /// </remarks>
        private Source(DateTimeOffset value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written, s_dateTimeStandardFormat));
            _kind = Kind.StringSimpleType;
        }

        private Source(DateTime value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written, s_dateTimeStandardFormat));
            _kind = Kind.StringSimpleType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from an OffsetDateTime value.
        /// </summary>
        /// <param name="value">The OffsetDateTime value to convert to a JSON source.</param>
        /// <remarks>
        /// <para>
        /// NodaTime date and time values are formatted as JSON strings using specialized formatting logic
        /// from <see cref="JsonElementHelpers"/>. The value is pre-formatted into UTF-8 bytes and stored
        /// with <see cref="Kind.StringSimpleType"/> to indicate string representation in the final JSON.
        /// </para>
        /// <para>
        /// <strong>Formatting Behavior:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Uses <see cref="JsonElementHelpers.TryFormatOffsetDateTime"/> for specialized NodaTime formatting</description></item>
        /// <item><description>Output includes surrounding quotes as per JSON string specification</description></item>
        /// <item><description>Preserves precise NodaTime semantics and timezone offset information</description></item>
        /// </list>
        /// <para>
        /// The resulting JSON will represent this value as a quoted JSON string with NodaTime's
        /// standard OffsetDateTime format including timezone offset.
        /// </para>
        /// </remarks>
        private Source(OffsetDateTime value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatOffsetDateTime(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(OffsetDate value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatOffsetDate(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(OffsetTime value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatOffsetTime(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(LocalDate value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatLocalDate(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(Period value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => JsonElementHelpers.TryFormatPeriod(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(Guid value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written));
            _kind = Kind.StringSimpleType;
        }

        private Source(Uri value)
        {
            _utf16Backing = value.OriginalString.AsSpan();
            _kind = Kind.Utf16String;
        }

#if NET

        private Source(Int128 value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(UInt128 value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

        private Source(Half value)
        {
            SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written));
            _kind = Kind.NumericSimpleType;
        }

#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a JSON array builder.
        /// </summary>
        /// <param name="value">The array builder delegate to use as the source.</param>
        public Source(ArrayBuilder.Build value)
        {
            _arrayBuilder = value;
            _kind = Kind.JsonArrayBuilderInstance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a JSON object builder.
        /// </summary>
        /// <param name="value">The object builder delegate to use as the source.</param>
        public Source(ObjectBuilder.Build value)
        {
            _objectBuilder = value;
            _kind = Kind.JsonObjectBuilderInstance;
        }

        /// <summary>
        /// Implicitly converts a <see cref="JsonElement"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The JSON element to convert.</param>
        /// <returns>A source representing the JSON element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(in JsonElement value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="JsonElement"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The JSON element to convert.</param>
        /// <returns>A source representing the JSON element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(in Mutable value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a UTF-8 byte span to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The UTF-8 byte span to convert.</param>
        /// <returns>A source representing the UTF-8 string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(ReadOnlySpan<byte> value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a UTF-16 character span to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The UTF-16 character span to convert.</param>
        /// <returns>A source representing the UTF-16 string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(ReadOnlySpan<char> value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a string to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The string to convert.</param>
        /// <returns>A source representing the string.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(string value)
        {
            return new(value.AsSpan());
        }

        /// <summary>
        /// Implicitly converts a <see cref="DateTimeOffset"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The DateTimeOffset value to convert.</param>
        /// <returns>A source representing the DateTimeOffset value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(DateTimeOffset value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="DateTime"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The DateTime value to convert.</param>
        /// <returns>A source representing the DateTime value.</returns>
        public static implicit operator Source(DateTime value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an <see cref="OffsetDateTime"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The OffsetDateTime value to convert.</param>
        /// <returns>A source representing the OffsetDateTime value.</returns>
        public static implicit operator Source(OffsetDateTime value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an <see cref="OffsetDate"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The OffsetDate value to convert.</param>
        /// <returns>A source representing the OffsetDate value.</returns>
        public static implicit operator Source(OffsetDate value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an <see cref="OffsetTime"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The OffsetTime value to convert.</param>
        /// <returns>A source representing the OffsetTime value.</returns>
        public static implicit operator Source(OffsetTime value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="LocalDate"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The LocalDate value to convert.</param>
        /// <returns>A source representing the LocalDate value.</returns>
        public static implicit operator Source(LocalDate value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="Period"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The Period value to convert.</param>
        /// <returns>A source representing the Period value.</returns>
        public static implicit operator Source(Period value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="Guid"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The Guid value to convert.</param>
        /// <returns>A source representing the Guid value.</returns>
        public static implicit operator Source(Guid value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a <see cref="Uri"/> to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The Uri value to convert.</param>
        /// <returns>A source representing the Uri value.</returns>
        public static implicit operator Source(Uri value)
        {
            return new(value);
        }

        /// <summary>
        /// Creates a <see cref="Source"/> representing a null value.
        /// </summary>
        /// <returns>A source representing a null JSON value.</returns>
        /// <remarks>
        /// <para>
        /// This method creates a lightweight source that represents a JSON null value.
        /// The source stores only the <see cref="Kind.Null"/> discriminator without any
        /// additional data, making it extremely efficient for null value construction.
        /// </para>
        /// <para>
        /// <strong>Usage Examples:</strong>
        /// </para>
        /// <code>
        /// // Create a document with null value
        /// using var doc = JsonElement.CreateBuilder(workspace, JsonElement.Source.Null());

        ///
        /// // Add null property to object
        /// objBuilder.Add("nullProp", JsonElement.Source.Null());

        /// </code>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Source Null()
        {
            return new(Kind.Null);
        }

        /// <summary>
        /// Creates a <see cref="Source"/> from a raw UTF-8 string.
        /// </summary>
        /// <param name="value">The raw UTF-8 string bytes.</param>
        /// <param name="requiresUnescaping">Whether the string requires unescaping.</param>
        /// <returns>A source representing the raw UTF-8 string.</returns>
        /// <remarks>
        /// <para>
        /// This method creates a source from pre-existing UTF-8 string data, avoiding
        /// encoding conversion overhead. The <paramref name="requiresUnescaping"/> parameter
        /// indicates whether the string contains JSON escape sequences that need processing.
        /// </para>
        /// <para>
        /// <strong>Escape Handling:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>requiresUnescaping = true</c>: String contains escape sequences like \n, \", \\</description></item>
        /// <item><description><c>requiresUnescaping = false</c>: String is literal and needs no unescaping</description></item>
        /// <item><description>The choice affects performance during JSON construction</description></item>
        /// </list>
        /// <para>
        /// <strong>Usage Examples:</strong>
        /// </para>
        /// <code>
        /// // Raw string without escapes
        /// var source1 = JsonElement.Source.RawString("hello"u8, requiresUnescaping: false);

        ///
        /// // String with escape sequences
        /// var source2 = JsonElement.Source.RawString("hello\\nworld"u8, requiresUnescaping: true);

        /// </code>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Source RawString(ReadOnlySpan<byte> value, bool requiresUnescaping)
        {
            return new(value, requiresUnescaping);
        }

        /// <summary>
        /// Creates a <see cref="Source"/> from a formatted number value.
        /// </summary>
        /// <param name="value">The raw UTF-8 bytes representing the formatted number.</param>
        /// <returns>A source representing the formatted number.</returns>
        /// <remarks>
        /// <para>
        /// This method creates a source from pre-formatted numeric data in UTF-8 encoding,
        /// avoiding the overhead of numeric formatting during JSON construction. The provided
        /// bytes should represent a valid JSON number without quotes.
        /// </para>
        /// <para>
        /// <strong>Format Requirements:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Must be valid JSON number format (e.g., "123", "-45.67", "1.23e-4")</description></item>
        /// <item><description>Must not include surrounding quotes</description></item>
        /// <item><description>Must be UTF-8 encoded</description></item>
        /// <item><description>Should follow JSON numeric format specification</description></item>
        /// </list>
        /// <para>
        /// <strong>Usage Examples:</strong>
        /// </para>
        /// <code>
        /// // Pre-formatted integer
        /// var source1 = JsonElement.Source.FormattedNumber("12345"u8);

        ///
        /// // Pre-formatted decimal
        /// var source2 = JsonElement.Source.FormattedNumber("123.456"u8);

        ///
        /// // Scientific notation
        /// var source3 = JsonElement.Source.FormattedNumber("1.23e+10"u8);

        /// </code>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Source FormattedNumber(ReadOnlySpan<byte> value)
        {
            return new(value, Kind.FormattedNumber);
        }

        /// <summary>
        /// Implicitly converts a boolean value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <returns>A source representing the boolean value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(bool value)
        {
            return new(value ? Kind.True : Kind.False);
        }

        /// <summary>
        /// Implicitly converts a long value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The long value to convert.</param>
        /// <returns>A source representing the long value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(long value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an integer value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The integer value to convert.</param>
        /// <returns>A source representing the integer value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(int value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a short value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The short value to convert.</param>
        /// <returns>A source representing the short value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(short value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a signed byte value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The signed byte value to convert.</param>
        /// <returns>A source representing the signed byte value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(sbyte value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an unsigned long value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The unsigned long value to convert.</param>
        /// <returns>A source representing the unsigned long value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(ulong value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an unsigned integer value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The unsigned integer value to convert.</param>
        /// <returns>A source representing the unsigned integer value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(uint value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts an unsigned short value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The unsigned short value to convert.</param>
        /// <returns>A source representing the unsigned short value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(ushort value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a byte value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The byte value to convert.</param>
        /// <returns>A source representing the byte value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(byte value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a decimal value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The decimal value to convert.</param>
        /// <returns>A source representing the decimal value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(decimal value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a double value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The double value to convert.</param>
        /// <returns>A source representing the double value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(double value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a float value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The float value to convert.</param>
        /// <returns>A source representing the float value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Source(float value)
        {
            return new(value);
        }

#if NET

        /// <summary>
        /// Implicitly converts an Int128 value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The Int128 value to convert.</param>
        /// <returns>A source representing the Int128 value.</returns>
        public static implicit operator Source(Int128 value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a UInt128 value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The UInt128 value to convert.</param>
        /// <returns>A source representing the UInt128 value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        public static implicit operator Source(UInt128 value)
        {
            return new(value);
        }

        /// <summary>
        /// Implicitly converts a Half value to a <see cref="Source"/>.
        /// </summary>
        /// <param name="value">The Half value to convert.</param>
        /// <returns>A source representing the Half value.</returns>
        public static implicit operator Source(Half value)
        {
            return new(value);
        }

#endif

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="utf8Name">The UTF-8 encoded property name.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        /// <remarks>
        /// <para>
        /// This method implements type-specific dispatch based on the internal <see cref="Kind"/> value,
        /// routing each source type to the appropriate <see cref="ComplexValueBuilder"/> method:
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Primitives:</strong> Direct calls to type-specific AddProperty overloads</description></item>
        /// <item><description><strong>Numeric Types:</strong> Formatted as JSON numbers using pre-computed UTF-8 bytes</description></item>
        /// <item><description><strong>String Types:</strong> Handled with appropriate escaping and encoding parameters</description></item>
        /// <item><description><strong>Complex Types:</strong> Delegates to builder instances for recursive construction</description></item>
        /// </list>
        /// <para>
        /// <strong>Escape Handling:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>escapeName</c>: Controls whether property names are JSON-escaped</description></item>
        /// <item><description><c>nameRequiresUnescaping</c>: Indicates pre-escaped property names</description></item>
        /// <item><description>String values have separate escape handling based on their source type</description></item>
        /// </list>
        /// <para>
        /// This method is typically called automatically during JSON object construction and
        /// forms part of the internal document building pipeline.
        /// </para>
        /// </remarks>
        public void AddAsProperty(ReadOnlySpan<byte> utf8Name, ref ComplexValueBuilder valueBuilder, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            switch (_kind)
            {
                case Kind.JsonElement:
                    valueBuilder.AddProperty(utf8Name, _jsonElement, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.Null:
                    valueBuilder.AddPropertyNull(utf8Name, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.True:
                    valueBuilder.AddProperty(utf8Name, true, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.False:
                    valueBuilder.AddProperty(utf8Name, false, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.NumericSimpleType:
                    valueBuilder.AddPropertyFormattedNumber(utf8Name, _simpleTypeBacking.Span(), escapeName, nameRequiresUnescaping);
                    break;

                case Kind.FormattedNumber:
                    valueBuilder.AddPropertyFormattedNumber(utf8Name, _utf8Backing, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.StringSimpleType:
                    valueBuilder.AddProperty(utf8Name, _simpleTypeBacking.Span(), escapeName, escapeValue: false, nameRequiresUnescaping, valueRequiresUnescaping: false);
                    break;

                case Kind.RawUtf8StringRequiresUnescaping:
                    valueBuilder.AddProperty(utf8Name, _utf8Backing, escapeName, escapeValue: false, nameRequiresUnescaping, valueRequiresUnescaping: true);
                    break;

                case Kind.RawUtf8StringNotRequiresUnescaping:
                    valueBuilder.AddProperty(utf8Name, _utf8Backing, escapeName, escapeValue: false, nameRequiresUnescaping, valueRequiresUnescaping: false);
                    break;

                case Kind.Utf8String:
                    valueBuilder.AddProperty(utf8Name, _utf8Backing, escapeName, escapeValue: true, nameRequiresUnescaping, valueRequiresUnescaping: false);
                    break;

                case Kind.Utf16String:
                    valueBuilder.AddProperty(utf8Name, _utf16Backing, escapeName, nameRequiresUnescaping);
                    break;

                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddProperty(utf8Name, _arrayBuilder!, static (in b, ref o) => ArrayBuilder.BuildValue(b, ref o), escapeName, nameRequiresUnescaping);
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddProperty(utf8Name, _objectBuilder!, static (in b, ref o) => ObjectBuilder.BuildValue(b, ref o), escapeName, nameRequiresUnescaping);
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        public void AddAsProperty(string name, ref ComplexValueBuilder valueBuilder)
        {
            AddAsProperty(name.AsSpan(), ref valueBuilder);
        }

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="name">The property name as a character span.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        public void AddAsProperty(ReadOnlySpan<char> name, ref ComplexValueBuilder valueBuilder)
        {
            switch (_kind)
            {
                case Kind.JsonElement:
                    valueBuilder.AddProperty(name, _jsonElement);
                    break;

                case Kind.Null:
                    valueBuilder.AddPropertyNull(name);
                    break;

                case Kind.True:
                    valueBuilder.AddProperty(name, true);
                    break;

                case Kind.False:
                    valueBuilder.AddProperty(name, false);
                    break;

                case Kind.NumericSimpleType:
                    valueBuilder.AddPropertyFormattedNumber(name, _simpleTypeBacking.Span());
                    break;

                case Kind.FormattedNumber:
                    valueBuilder.AddPropertyFormattedNumber(name, _utf8Backing);
                    break;

                case Kind.StringSimpleType:
                    valueBuilder.AddProperty(name, _simpleTypeBacking.Span(), escapeValue: false, valueRequiresUnescaping: false);
                    break;

                case Kind.RawUtf8StringRequiresUnescaping:
                    valueBuilder.AddProperty(name, _utf8Backing, escapeValue: false, valueRequiresUnescaping: true);
                    break;

                case Kind.RawUtf8StringNotRequiresUnescaping:
                    valueBuilder.AddProperty(name, _utf8Backing, escapeValue: false, valueRequiresUnescaping: false);
                    break;

                case Kind.Utf8String:
                    valueBuilder.AddProperty(name, _utf8Backing, escapeValue: true, valueRequiresUnescaping: false);
                    break;

                case Kind.Utf16String:
                    valueBuilder.AddProperty(name, _utf16Backing);
                    break;

                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddProperty(name, _arrayBuilder!, static (in b, ref o) => ArrayBuilder.BuildValue(b, ref o));
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddProperty(name, _objectBuilder!, static (in b, ref o) => ObjectBuilder.BuildValue(b, ref o));
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }

        /// <summary>
        /// Adds this source as an item to a complex value builder.
        /// </summary>
        /// <param name="valueBuilder">The complex value builder to add the item to.</param>
        /// <remarks>
        /// <para>
        /// This method implements type-specific dispatch based on the internal <see cref="Kind"/> value,
        /// routing each source type to the appropriate <see cref="ComplexValueBuilder"/> method for array items:
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Primitives:</strong> Direct calls to type-specific AddItem overloads</description></item>
        /// <item><description><strong>Numeric Types:</strong> Formatted as JSON numbers using pre-computed UTF-8 bytes</description></item>
        /// <item><description><strong>String Types:</strong> Handled with appropriate escaping parameters</description></item>
        /// <item><description><strong>Complex Types:</strong> Delegates to builder instances for recursive construction</description></item>
        /// </list>
        /// <para>
        /// Unlike <see cref="AddAsProperty"/>, this method does not handle property name escaping
        /// since array items do not have names. String values are processed with their appropriate
        /// escape handling based on the source type.
        /// </para>
        /// <para>
        /// This method is typically called automatically during JSON array construction and
        /// forms part of the internal document building pipeline.
        /// </para>
        /// </remarks>
        public void AddAsItem(ref ComplexValueBuilder valueBuilder)
        {
            switch (_kind)
            {
                case Kind.JsonElement:
                    valueBuilder.AddItem(_jsonElement);
                    break;

                case Kind.Null:
                    valueBuilder.AddItemNull();
                    break;

                case Kind.True:
                    valueBuilder.AddItem(true);
                    break;

                case Kind.False:
                    valueBuilder.AddItem(false);
                    break;

                case Kind.NumericSimpleType:
                    valueBuilder.AddItemFormattedNumber(_simpleTypeBacking.Span());
                    break;

                case Kind.FormattedNumber:
                    valueBuilder.AddItemFormattedNumber(_utf8Backing);
                    break;

                case Kind.StringSimpleType:
                    valueBuilder.AddItem(_simpleTypeBacking.Span(), escapeValue: false, requiresUnescaping: false);
                    break;

                case Kind.RawUtf8StringRequiresUnescaping:
                    valueBuilder.AddItem(_utf8Backing, escapeValue: false, requiresUnescaping: true);
                    break;

                case Kind.RawUtf8StringNotRequiresUnescaping:
                    valueBuilder.AddItem(_utf8Backing, escapeValue: false, requiresUnescaping: false);
                    break;

                case Kind.Utf8String:
                    valueBuilder.AddItem(_utf8Backing, escapeValue: true, requiresUnescaping: false);
                    break;

                case Kind.Utf16String:
                    valueBuilder.AddItem(_utf16Backing);
                    break;

                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddItem(_arrayBuilder!, static (in b, ref o) => ArrayBuilder.BuildValue(b, ref o));
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddItem(_objectBuilder!, static (in b, ref o) => ObjectBuilder.BuildValue(b, ref o));
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }
    }

    public readonly ref struct Source<TContext>
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        /// <summary>
        /// Discriminates the type and storage mechanism for the source value.
        /// </summary>
        /// <remarks>
        private enum Kind
        {
            // We only need to include the kinds we
            // actually need!
            Unknown,
            Source,
            JsonArrayBuilderInstance,
            JsonObjectBuilderInstance,
        }

        private readonly Kind _kind;

        private readonly TContext _context;

        private readonly ArrayBuilder.Build<TContext>? _arrayBuilder;

        private readonly ObjectBuilder.Build<TContext>? _objectBuilder;

        private readonly Source _source;

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a JSON array builder.
        /// </summary>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="value">The array builder delegate to use as the source.</param>
        public Source(in TContext context, ArrayBuilder.Build<TContext> value)
        {
            _context = context;
            _arrayBuilder = value;
            _kind = Kind.JsonArrayBuilderInstance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a JSON object builder.
        /// </summary>
        /// <param name="value">The object builder delegate to use as the source.</param>
        public Source(in TContext context, ObjectBuilder.Build<TContext> value)
        {
            _context = context;
            _objectBuilder = value;
            _kind = Kind.JsonObjectBuilderInstance;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Source"/> struct from a JSON object builder.
        /// </summary>
        /// <param name="source">The context-free source to use as the source.</param>
        public Source(Source source)
        {
            _source = source;
            _context = default!;
            _kind = Kind.Source;
        }

        public static implicit operator Source<TContext>(Source source)
        {
            return new(source);
        }

        /// <summary>
        /// Gets a value indicating whether this Source is undefined (uninitialized).
        /// </summary>
        public bool IsUndefined => _kind == Kind.Unknown;

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="utf8Name">The UTF-8 encoded property name.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        /// <remarks>
        /// <para>
        /// This method implements type-specific dispatch based on the internal <see cref="Kind"/> value,
        /// routing each source type to the appropriate <see cref="ComplexValueBuilder"/> method:
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Primitives:</strong> Direct calls to type-specific AddProperty overloads</description></item>
        /// <item><description><strong>Numeric Types:</strong> Formatted as JSON numbers using pre-computed UTF-8 bytes</description></item>
        /// <item><description><strong>String Types:</strong> Handled with appropriate escaping and encoding parameters</description></item>
        /// <item><description><strong>Complex Types:</strong> Delegates to builder instances for recursive construction</description></item>
        /// </list>
        /// <para>
        /// <strong>Escape Handling:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>escapeName</c>: Controls whether property names are JSON-escaped</description></item>
        /// <item><description><c>nameRequiresUnescaping</c>: Indicates pre-escaped property names</description></item>
        /// <item><description>String values have separate escape handling based on their source type</description></item>
        /// </list>
        /// <para>
        /// This method is typically called automatically during JSON object construction and
        /// forms part of the internal document building pipeline.
        /// </para>
        /// </remarks>
        public void AddAsProperty(ReadOnlySpan<byte> utf8Name, ref ComplexValueBuilder valueBuilder, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            switch (_kind)
            {
                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddProperty(utf8Name, BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ArrayBuilder.BuildValue(b.Context, b.Build, ref o), escapeName, nameRequiresUnescaping);
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddProperty(utf8Name, BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ObjectBuilder.BuildValue(b.Context, b.Build, ref o), escapeName, nameRequiresUnescaping);
                    break;

                case Kind.Source:
                    _source.AddAsProperty(utf8Name, ref valueBuilder, escapeName, nameRequiresUnescaping);
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="name">The property name.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        public void AddAsProperty(string name, ref ComplexValueBuilder valueBuilder)
        {
            AddAsProperty(name.AsSpan(), ref valueBuilder);
        }

        /// <summary>
        /// Adds this source as a property to a complex value builder.
        /// </summary>
        /// <param name="name">The property name as a character span.</param>
        /// <param name="valueBuilder">The complex value builder to add the property to.</param>
        public void AddAsProperty(ReadOnlySpan<char> name, ref ComplexValueBuilder valueBuilder)
        {
            switch (_kind)
            {
                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddProperty(name, BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ArrayBuilder.BuildValue(b.Context, b.Build, ref o));
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddProperty(name, BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ObjectBuilder.BuildValue(b.Context, b.Build, ref o));
                    break;

                case Kind.Source:
                    _source.AddAsProperty(name, ref valueBuilder);
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }

        /// <summary>
        /// Adds this source as an item to a complex value builder.
        /// </summary>
        /// <param name="valueBuilder">The complex value builder to add the item to.</param>
        /// <remarks>
        /// <para>
        /// This method implements type-specific dispatch based on the internal <see cref="Kind"/> value,
        /// routing each source type to the appropriate <see cref="ComplexValueBuilder"/> method for array items:
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Primitives:</strong> Direct calls to type-specific AddItem overloads</description></item>
        /// <item><description><strong>Numeric Types:</strong> Formatted as JSON numbers using pre-computed UTF-8 bytes</description></item>
        /// <item><description><strong>String Types:</strong> Handled with appropriate escaping parameters</description></item>
        /// <item><description><strong>Complex Types:</strong> Delegates to builder instances for recursive construction</description></item>
        /// </list>
        /// <para>
        /// Unlike <see cref="AddAsProperty"/>, this method does not handle property name escaping
        /// since array items do not have names. String values are processed with their appropriate
        /// escape handling based on the source type.
        /// </para>
        /// <para>
        /// This method is typically called automatically during JSON array construction and
        /// forms part of the internal document building pipeline.
        /// </para>
        /// </remarks>
        public void AddAsItem(ref ComplexValueBuilder valueBuilder)
        {
            switch (_kind)
            {
                case Kind.JsonArrayBuilderInstance:
                    valueBuilder.AddItem(BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ArrayBuilder.BuildValue(b.Context, b.Build, ref o));
                    break;

                case Kind.JsonObjectBuilderInstance:
                    valueBuilder.AddItem(BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ObjectBuilder.BuildValue(b.Context, b.Build, ref o));
                    break;

                case Kind.Source:
                    _source.AddAsItem(ref valueBuilder);
                    break;

                default:
                    Debug.Fail("Unrecognized kind.");
                    break;
            }
        }
    }

    /// <summary>
    /// Creates a JSON document builder from a source value.
    /// </summary>
    /// <param name="workspace">The JSON workspace to use for the document builder.</param>
    /// <param name="source">The source value to build the document from.</param>
    /// <param name="estimatedMemberCount">The estimated number of members in the document.</param>
    /// <returns>A JSON document builder containing the source value.</returns>
    /// <remarks>
    /// <para>
    /// This method represents the primary integration point between <see cref="Source"/> values
    /// and the <see cref="JsonDocumentBuilder{T}"/> infrastructure. It creates a complete
    /// mutable JSON document from any compatible .NET value.
    /// </para>
    /// <para>
    /// <strong>Construction Process:</strong>
    /// </para>
    /// <list type="number">
    /// <item><description>Creates a new <see cref="JsonDocumentBuilder{Mutable}"/> from the workspace</description></item>
    /// <item><description>Initializes a <see cref="ComplexValueBuilder"/> with the estimated capacity</description></item>
    /// <item><description>Calls <see cref="Source.AddAsItem"/> to convert the source into the document structure</description></item>
    /// <item><description>Finalizes the document and transfers ownership to the returned builder</description></item>
    /// </list>
    /// <para>
    /// <strong>Memory Management:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Uses workspace-managed memory pools for efficient allocation</description></item>
    /// <item><description>EstimatedMemberCount pre-sizes internal data structures to minimize reallocations</description></item>
    /// <item><description>Returns a disposable document builder that manages resource cleanup</description></item>
    /// </list>
    /// <para>
    /// <strong>Performance Best Practices:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Capacity Estimation:</strong> Provide accurate <c>estimatedMemberCount</c> to minimize internal reallocations</description></item>
    /// <item><description><strong>String Handling:</strong> Use UTF-8 spans when possible to avoid encoding overhead</description></item>
    /// <item><description><strong>Nested Structures:</strong> Consider flattening deeply nested objects for better performance</description></item>
    /// <item><description><strong>Workspace Reuse:</strong> Reuse <see cref="JsonWorkspace"/> instances across multiple document creations</description></item>
    /// </list>
    /// <para>
    /// <strong>Memory Allocation Patterns:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Stack Allocation:</strong> Source and ComplexValueBuilder are ref structs with stack-only allocation</description></item>
    /// <item><description><strong>Pooled Memory:</strong> Document storage uses workspace-managed memory pools</description></item>
    /// <item><description><strong>Zero-Copy Scenarios:</strong> String and span data often referenced without copying</description></item>
    /// </list>
    /// <para>
    /// <strong>Usage Examples:</strong>
    /// </para>
    /// <code>
    /// // Simple value
    /// using var doc = JsonElement.CreateBuilder(workspace, 42);

    ///
    /// // Complex object
    /// using var doc = JsonElement.CreateBuilder(workspace,
    /// new(objectBuilder => { /* build object */ }));

    ///
    /// // From existing JsonElement
    /// using var doc = JsonElement.CreateBuilder(workspace, existingElement);

    /// </code>
    /// </remarks>
    /// <remarks>This method is not CLS compliant.</remarks>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateBuilder(JsonWorkspace workspace, in Source source, int estimatedMemberCount = 30)
    {
        // Create the document builder without a MetadataDb
        if (source.IsUndefined)
        {
            ThrowHelper.ThrowArgumentException(SR.EmptyJsonIsInvalid);
        }

        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, estimatedMemberCount);
        source.AddAsItem(ref cvb);
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Creates a JSON document builder from an array builder.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
    /// <param name="workspace">The JSON workspace to use for the document builder.</param>
    /// <param name="context">The builder context.</param>
    /// <param name="builder">The array value builder.</param>
    /// <param name="estimatedMemberCount">The estimated number of members in the document.</param>
    /// <returns>A JSON document builder containing the source value.</returns>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateBuilder<TContext>(JsonWorkspace workspace, in TContext context, ArrayBuilder.Build<TContext> builder, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        // Create the document builder without a MetadataDb
        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, estimatedMemberCount);
        cvb.AddItem(BuildWithContext.Create(context, builder), static (in b, ref o) => ArrayBuilder.BuildValue(b.Context, b.Build, ref o));
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Creates a JSON document builder from an object builder.
    /// </summary>
    /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
    /// <param name="workspace">The JSON workspace to use for the document builder.</param>
    /// <param name="context">The builder context.</param>
    /// <param name="builder">The array value builder.</param>
    /// <param name="estimatedMemberCount">The estimated number of members in the document.</param>
    /// <returns>A JSON document builder containing the array value.</returns>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateBuilder<TContext>(JsonWorkspace workspace, in TContext context, ObjectBuilder.Build<TContext> builder, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
        where TContext : allows ref struct
#endif
    {
        // Create the document builder without a MetadataDb
        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, estimatedMemberCount);
        cvb.AddItem(BuildWithContext.Create(context, builder), static (in b, ref o) => ObjectBuilder.BuildValue(b.Context, b.Build, ref o));
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Creates an empty mutable array document builder.
    /// </summary>
    /// <param name="workspace">The JSON workspace to use for the document builder.</param>
    /// <param name="estimatedMemberCount">The estimated number of members in the document.</param>
    /// <returns>A JSON document builder containing an empty array.</returns>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateArrayBuilder(JsonWorkspace workspace, int estimatedMemberCount = 30)
    {
        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, estimatedMemberCount);
        cvb.StartArray();
        cvb.EndArray();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Creates an empty mutable object document builder.
    /// </summary>
    /// <param name="workspace">The JSON workspace to use for the document builder.</param>
    /// <param name="estimatedMemberCount">The estimated number of members in the document.</param>
    /// <returns>A JSON document builder containing an empty object.</returns>
    [CLSCompliant(false)]
    public static JsonDocumentBuilder<Mutable> CreateObjectBuilder(JsonWorkspace workspace, int estimatedMemberCount = 30)
    {
        JsonDocumentBuilder<Mutable> documentBuilder = workspace.CreateBuilder<Mutable>(-1);
        var cvb = ComplexValueBuilder.Create(documentBuilder, estimatedMemberCount);
        cvb.StartObject();
        cvb.EndObject();
        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
        return documentBuilder;
    }

    /// <summary>
    /// Represents a specific JSON value within a <see cref="IMutableJsonDocument"/>.
    /// </summary>

    /// <summary>
    /// Represents a mutable JSON value within a <see cref="IMutableJsonDocument"/> that supports both read and write operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Mutable"/> struct is the primary interface for interacting with JSON data in a mutable document.
    /// It provides comprehensive read access to JSON values, type-safe mutation operations, and efficient in-place
    /// modification capabilities while maintaining document version consistency.
    /// </para>
    ///
    /// <para>
    /// <strong>Core Features:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Type Safety:</strong> Runtime validation ensures operations match actual JSON value types</description></item>
    /// <item><description><strong>Version Tracking:</strong> Automatic staleness detection prevents use of outdated references</description></item>
    /// <item><description><strong>In-Place Mutation:</strong> Direct document modification without full reconstruction</description></item>
    /// <item><description><strong>Comprehensive Type Support:</strong> All .NET primitive types, dates, strings, and complex structures</description></item>
    /// <item><description><strong>Memory Efficiency:</strong> Minimal per-instance overhead with workspace-managed memory pools</description></item>
    /// </list>
    ///
    /// <para>
    /// <strong>Usage Patterns:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item><description><strong>Property Access:</strong> <c>GetProperty(name)</c>, <c>TryGetProperty(name, out value)</c></description></item>
    /// <item><description><strong>Array Access:</strong> <c>element[index]</c>, <c>EnumerateArray()</c></description></item>
    /// <item><description><strong>Value Extraction:</strong> <c>GetString()</c>, <c>GetInt32()</c>, <c>GetBoolean()</c></description></item>
    /// <item><description><strong>Mutation:</strong> <c>SetProperty(name, value)</c>, <c>SetItem(index, value)</c></description></item>
    /// </list>
    ///
    /// <para>
    /// <strong>Thread Safety:</strong> This type is not thread-safe. Concurrent access requires external synchronization.
    /// </para>
    ///
    /// <para>
    /// <strong>Version Management:</strong> Each mutation updates the document version. Stale references throw
    /// <see cref="InvalidOperationException"/> when accessed.
    /// </para>
    /// </remarks>
    /// <example>
    /// <para>Basic property manipulation:</para>
    /// <code>
    /// using var workspace = JsonWorkspace.Create();

    /// using var doc = JsonElement.CreateBuilder(workspace, new Source(
    /// new JsonObjectBuilder.Build((ref JsonObjectBuilder builder) =>
    /// {
    /// builder.Add("name", "John Doe");

    /// builder.Add("age", 30);

    /// builder.Add("active", true);

    /// })));

    ///
    /// var root = doc.RootElement;

    ///
    /// // Read properties
    /// string name = root.GetProperty("name").GetString();

    /// int age = root.GetProperty("age").GetInt32();

    ///
    /// // Modify properties
    /// root.SetProperty("age", age + 1);

    /// root.SetProperty("lastModified", DateTime.UtcNow);

    ///
    /// // Safe property access
    /// if (root.TryGetProperty("email", out var emailElement))
    /// {
    /// Console.WriteLine($"Email: {emailElement.GetString()}");

    /// }

    /// </code>
    /// </example>
    /// <example>
    /// <para>Array manipulation:</para>
    /// <code>
    /// // Create array document
    /// using var arrayDoc = JsonElement.CreateBuilder(workspace, new Source(
    /// new JsonArrayBuilder.Build((ref JsonArrayBuilder builder) =>
    /// {
    /// builder.Add(1);

    /// builder.Add(2);

    /// builder.Add(3);

    /// })));

    ///
    /// var array = arrayDoc.RootElement;

    ///
    /// // Read array elements
    /// for (int i = 0; i &lt; array.GetArrayLength(); i++)
    /// {
    /// Console.WriteLine(array[i].GetInt32());

    /// }

    ///
    /// // Modify array elements
    /// array.SetItem(0, 10);

    /// array.SetItem(1, "modified");

    ///
    /// // Enumerate array
    /// foreach (var item in array.EnumerateArray())
    /// {
    /// Console.WriteLine(item.ToString());

    /// }

    /// </code>
    /// </example>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public partial struct Mutable : IMutableJsonElement<Mutable>,
#if NET
    IFormattable,
    ISpanFormattable,
    IUtf8SpanFormattable
#else
    IFormattable
#endif
    {
        private readonly IMutableJsonDocument _parent;

        private readonly int _idx;

        private ulong _documentVersion;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        /// <summary>
        /// Initializes a new instance of the <see cref="Mutable"/> struct.
        /// </summary>
        /// <param name="parent">The parent document containing this JSON element. Must implement <see cref="IMutableJsonDocument"/>.</param>
        /// <param name="idx">The zero-based index of this element within the document's internal structure.</param>
        /// <remarks>
        /// <para>
        /// This constructor is internal and should not be called directly. Instances are typically created through:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Document builders (<see cref="JsonDocumentBuilder{T}"/>)</description></item>
        /// <item><description>Property access methods (<see cref="GetProperty(string)"/>)</description></item>
        /// <item><description>Array indexing (<see cref="this[int]"/>)</description></item>
        /// <item><description>Enumeration operations</description></item>
        /// </list>
        /// <para>
        /// The constructor captures the current document version for staleness detection and performs
        /// runtime validation to ensure the parent implements the required mutable interface.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="parent"/> does not implement <see cref="IMutableJsonDocument"/>.</exception>
        internal Mutable(IJsonDocument parent, int idx)
        {
            // parent is usually not null, but the Current property
            // on the enumerators (when initialized as `default`) can
            // get here with a null.
            Debug.Assert(idx >= 0);

            _parent = (IMutableJsonDocument)parent;
            _idx = idx;
            _documentVersion = _parent?.Version ?? 0;
        }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        /// <summary>
        /// Gets the JSON token type for this element from the parent document.
        /// </summary>
        /// <value>
        /// The <see cref="JsonTokenType"/> representing the low-level token type of this JSON element.
        /// Returns <see cref="JsonTokenType.None"/> if the parent document is null or disposed.
        /// </value>
        /// <remarks>
        /// This property provides access to the underlying JSON token representation used internally
        /// by the document. It is primarily used for type validation and conversion to <see cref="JsonValueKind"/>.
        /// Most consumers should use <see cref="ValueKind"/> instead for standard JSON value type checking.
        /// </remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly JsonTokenType TokenType
        {
            get
            {
                return _parent?.GetJsonTokenType(_idx) ?? JsonTokenType.None;
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonValueKind"/> that represents the type of this JSON value.
        /// </summary>
        /// <value>
        /// A <see cref="JsonValueKind"/> enumeration value indicating whether this element represents
        /// a JSON null, boolean, number, string, array, or object.
        /// </value>
        /// <remarks>
        /// <para>
        /// This property is the primary way to determine the type of a JSON value before performing
        /// type-specific operations. It provides a high-level classification that corresponds to
        /// the JSON specification's value types.
        /// </para>
        /// <para>
        /// <strong>Possible Values:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><see cref="JsonValueKind.Null"/> - JSON null value</description></item>
        /// <item><description><see cref="JsonValueKind.True"/> - JSON boolean true</description></item>
        /// <item><description><see cref="JsonValueKind.False"/> - JSON boolean false</description></item>
        /// <item><description><see cref="JsonValueKind.Number"/> - JSON numeric value</description></item>
        /// <item><description><see cref="JsonValueKind.String"/> - JSON string value</description></item>
        /// <item><description><see cref="JsonValueKind.Array"/> - JSON array</description></item>
        /// <item><description><see cref="JsonValueKind.Object"/> - JSON object</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// switch (element.ValueKind)
        /// {
        /// case JsonValueKind.String:
        /// Console.WriteLine($"String: {element.GetString()}");

        /// break;

        /// case JsonValueKind.Number:
        /// Console.WriteLine($"Number: {element.GetDouble()}");

        /// break;

        /// case JsonValueKind.Array:
        /// Console.WriteLine($"Array length: {element.GetArrayLength()}");

        /// break;

        /// case JsonValueKind.Object:
        /// Console.WriteLine($"Object properties: {element.GetPropertyCount()}");

        /// break;

        /// }

        /// </code>
        /// </example>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly JsonValueKind ValueKind => TokenType.ToValueKind();

        /// <summary>
        /// Gets the JSON array element at the specified zero-based index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to retrieve.</param>
        /// <value>
        /// A <see cref="Mutable"/> representing the JSON value at the specified array index.
        /// </value>
        /// <remarks>
        /// <para>
        /// This indexer provides convenient access to array elements using familiar array syntax.
        /// The returned element is a new <see cref="Mutable"/> instance that can be used for both
        /// reading and writing operations on the array element.
        /// </para>
        /// <para>
        /// <strong>Performance:</strong> Array element access is O(1) as it uses internal document indexing.
        /// </para>
        /// <para>
        /// <strong>Type Safety:</strong> This operation validates that the current element is a JSON array
        /// and that the index is within valid bounds before returning the element.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Access array elements
        /// var firstElement = arrayElement[0];

        /// var secondElement = arrayElement[1];

        ///
        /// // Chain operations
        /// string value = arrayElement[2].GetString();

        /// int count = arrayElement[3].GetArrayLength(); // if nested array
        ///
        /// // Modify through indexer
        /// arrayElement[0].SetProperty("name", "updated"); // if element is object
        /// </code>
        /// </example>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// <paramref name="index"/> is not in the range [0, <see cref="GetArrayLength"/>()).
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly Mutable this[int index]
        {
            get
            {
                CheckValidInstance();

                return _parent.GetArrayIndexElement(_idx, index);
            }
        }

        /// <summary>
        /// Gets the value of the property with the given UTF-8 encoded name when the current value is an
        /// <see cref="JsonValueKind.Object"/>.
        /// </summary>
        /// <param name="propertyName">
        /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property.
        /// </param>
        /// <returns>
        /// The value of the property with the given name, or a default <see cref="Mutable"/>
        /// if no such property exists.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent document has been disposed.
        /// </exception>
        public readonly Mutable this[ReadOnlySpan<byte> propertyName]
        {
            get
            {
                CheckValidInstance();

                if (!_parent.TryGetNamedPropertyValue(_idx, propertyName, out Mutable value))
                {
                    return default;
                }

                return value;
            }
        }

        /// <summary>
        /// Gets the value of the property with the given name when the current value is an
        /// <see cref="JsonValueKind.Object"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>
        /// The value of the property with the given name, or a default <see cref="Mutable"/>
        /// if no such property exists.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent document has been disposed.
        /// </exception>
        public readonly Mutable this[ReadOnlySpan<char> propertyName]
        {
            get
            {
                CheckValidInstance();

                if (!_parent.TryGetNamedPropertyValue(_idx, propertyName, out Mutable value))
                {
                    return default;
                }

                return value;
            }
        }

        /// <summary>
        /// Gets the value of the property with the given name when the current value is an
        /// <see cref="JsonValueKind.Object"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>
        /// The value of the property with the given name, or a default <see cref="Mutable"/>
        /// if no such property exists.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent document has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        public readonly Mutable this[string propertyName]
        {
            get
            {
                ArgumentNullException.ThrowIfNull(propertyName);

                return this[propertyName.AsSpan()];
            }
        }

        /// <summary>
        /// Implicitly converts a <see cref="Mutable"/> to a read-only <see cref="JsonElement"/>.
        /// </summary>
        /// <param name="value">The mutable JSON element to convert.</param>
        /// <returns>A read-only <see cref="JsonElement"/> representing the same JSON value.</returns>
        /// <remarks>
        /// <para>
        /// This implicit conversion allows mutable elements to be used anywhere a read-only
        /// <see cref="JsonElement"/> is expected, providing seamless interoperability between
        /// mutable and immutable JSON APIs.
        /// </para>
        /// <para>
        /// The conversion creates a new <see cref="JsonElement"/> that references the same
        /// underlying document and element index, but restricts access to read-only operations.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Passing mutable elements to methods expecting read-only elements</description></item>
        /// <item><description>Interfacing with System.Text.Json APIs</description></item>
        /// <item><description>Creating immutable snapshots for comparison or serialization</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// Mutable mutableElement = GetMutableElement();

        ///
        /// // Implicit conversion to JsonElement
        /// JsonElement readOnlyElement = mutableElement;

        ///
        /// // Use with read-only APIs
        /// ProcessReadOnlyElement(mutableElement); // implicit conversion
        ///
        /// void ProcessReadOnlyElement(JsonElement element)
        /// {
        /// // Can only read, not modify
        /// Console.WriteLine(element.GetString());

        /// }

        /// </code>
        /// </example>
        public static implicit operator JsonElement(Mutable value)
        {
            return new(value._parent, value._idx);
        }

        /// <summary>
        /// Explicitly converts a read-only <see cref="JsonElement"/> to a <see cref="Mutable"/>.
        /// </summary>
        /// <param name="value">The read-only JSON element to convert.</param>
        /// <returns>A <see cref="Mutable"/> representing the same JSON value with write capabilities.</returns>
        /// <remarks>
        /// <para>
        /// This explicit conversion enables upgrading read-only elements back to mutable form,
        /// but only when the underlying document supports mutation. The conversion validates
        /// that the source element originates from a mutable document.
        /// </para>
        /// <para>
        /// <strong>Requirements:</strong> The source <see cref="JsonElement"/> must have been created
        /// from a document that implements <see cref="IMutableJsonDocument"/>. Elements from
        /// read-only documents (such as those created by <see cref="JsonDocument.Parse(string)"/>)
        /// cannot be converted to mutable form.
        /// </para>
        /// <para>
        /// <strong>Use Cases:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description>Converting results from read-only API calls back to mutable form</description></item>
        /// <item><description>Restoring mutability after passing through read-only interfaces</description></item>
        /// <item><description>Type casting when element mutability is known at runtime</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Start with mutable element
        /// Mutable mutableElement = GetMutableElement();

        ///
        /// // Convert to read-only for some operation
        /// JsonElement readOnlyElement = mutableElement;

        ///
        /// // Convert back to mutable (explicit cast required)
        /// Mutable backToMutable = (Mutable)readOnlyElement;

        ///
        /// // Now can modify again
        /// backToMutable.SetProperty("modified", true);

        ///
        /// // This would fail - element from read-only document
        /// JsonElement fromParse = JsonDocument.Parse("{}").RootElement;

        /// Mutable invalid = (Mutable)fromParse; // throws FormatException
        /// </code>
        /// </example>
        /// <exception cref="FormatException">
        /// The JSON element is not from a mutable JSON document (i.e., its parent document
        /// does not implement <see cref="IMutableJsonDocument"/>).
        /// </exception>
        public static explicit operator Mutable(JsonElement value)
        {
            if (value._parent is not IMutableJsonDocument)
            {
                ThrowHelper.ThrowFormatException();

                // We will never get here
                return default;
            }

            return new(value._parent, value._idx);
        }

        /// <summary>
        /// Determines whether two <see cref="Mutable"/> instances represent equal JSON values.
        /// </summary>
        /// <param name="left">The first mutable JSON element to compare.</param>
        /// <param name="right">The second mutable JSON element to compare.</param>
        /// <returns><c>true</c> if the JSON values are structurally equal; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// This operator performs deep structural comparison of the JSON values, not reference equality.
        /// Two elements are considered equal if they represent the same JSON value, regardless of
        /// their source document or internal representation.
        /// </para>
        /// <para>
        /// <strong>Comparison Rules:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>Primitives:</strong> Values are compared directly (numbers, booleans, strings, null)</description></item>
        /// <item><description><strong>Arrays:</strong> Elements are compared in order; lengths must match</description></item>
        /// <item><description><strong>Objects:</strong> All properties are compared; order is irrelevant</description></item>
        /// <item><description><strong>Cross-document:</strong> Elements from different documents can be equal if values match</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// var element1 = CreateElementWithValue(42);

        /// var element2 = CreateElementWithValue(42);

        /// var element3 = CreateElementWithValue(43);

        ///
        /// bool same = element1 == element2;      // true - same value
        /// bool different = element1 == element3; // false - different value
        ///
        /// // Works across different documents
        /// var doc1Element = doc1.RootElement.GetProperty("value");

        /// var doc2Element = doc2.RootElement.GetProperty("value");

        /// bool crossDoc = doc1Element == doc2Element; // true if values match
        /// </code>
        /// </example>
        public static bool operator ==(Mutable left, Mutable right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="Mutable"/> instances represent different JSON values.
        /// </summary>
        /// <param name="left">The first mutable JSON element to compare.</param>
        /// <param name="right">The second mutable JSON element to compare.</param>
        /// <returns><c>true</c> if the JSON values are structurally different; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This operator is the logical inverse of the equality operator (<see cref="op_Equality"/>).
        /// It returns <c>true</c> when the elements represent different JSON values using the same
        /// deep structural comparison rules.
        /// </remarks>
        public static bool operator !=(Mutable left, Mutable right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether a <see cref="Mutable"/> and a read-only <see cref="JsonElement"/> represent equal JSON values.
        /// </summary>
        /// <param name="left">The mutable JSON element to compare.</param>
        /// <param name="right">The read-only JSON element to compare.</param>
        /// <returns><c>true</c> if the JSON values are structurally equal; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This operator enables seamless comparison between mutable and read-only JSON elements,
        /// using the same deep structural comparison as other equality operators. The mutability
        /// difference does not affect equality - only the actual JSON values are compared.
        /// </remarks>
        /// <example>
        /// <code>
        /// Mutable mutableElement = GetMutableElement();

        /// JsonElement readOnlyElement = JsonDocument.Parse("42").RootElement;

        ///
        /// bool equal = mutableElement == readOnlyElement; // true if both represent 42
        /// </code>
        /// </example>
        public static bool operator ==(Mutable left, JsonElement right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether a <see cref="Mutable"/> and a read-only <see cref="JsonElement"/> represent different JSON values.
        /// </summary>
        /// <param name="left">The mutable JSON element to compare.</param>
        /// <param name="right">The read-only JSON element to compare.</param>
        /// <returns><c>true</c> if the JSON values are structurally different; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This operator is the logical inverse of the mixed equality operator. It returns <c>true</c>
        /// when the mutable and read-only elements represent different JSON values.
        /// </remarks>
        public static bool operator !=(Mutable left, JsonElement right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether this JSON element is equal to the specified object.
        /// </summary>
        /// <param name="obj">The object to compare with this JSON element.</param>
        /// <returns><c>true</c> if the object represents an equal JSON value; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// This method provides standard .NET equality semantics for JSON elements. It supports
        /// comparison with other JSON element types implementing <see cref="IJsonElement"/>,
        /// as well as null comparison for JSON null values.
        /// </para>
        /// <para>
        /// <strong>Supported Comparisons:</strong>
        /// </para>
        /// <list type="bullet">
        /// <item><description><strong>IJsonElement implementations:</strong> Compares JSON values structurally</description></item>
        /// <item><description><strong>null objects:</strong> Returns true only if this element represents JSON null</description></item>
        /// <item><description><strong>Other types:</strong> Always returns false</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// Mutable element = GetElement();

        ///
        /// // Compare with other JSON elements
        /// bool equal1 = element.Equals(otherMutableElement);

        /// bool equal2 = element.Equals(readOnlyElement);

        ///
        /// // Null comparison for JSON null values
        /// bool isNull = nullElement.Equals(null); // true for JSON null
        ///
        /// // Non-JSON objects always return false
        /// bool notEqual = element.Equals("not a JSON element"); // false
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object? obj)
        {
            return (obj is IJsonElement other && Equals(new JsonElement(other.ParentDocument, other.ParentDocumentIndex)))
                || (obj is null && this.IsNull());
        }

        /// <summary>
        /// Determines whether this JSON element is equal to another JSON element of any compatible type.
        /// </summary>
        /// <typeparam name="T">The type of JSON element to compare with. Must implement <see cref="IJsonElement"/>.</typeparam>
        /// <param name="other">The JSON element to compare with this instance.</param>
        /// <returns><c>true</c> if the JSON elements represent equal values; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// <para>
        /// This generic method enables type-safe comparison with any JSON element type that implements
        /// <see cref="IJsonElement"/>, providing compile-time type checking while maintaining the
        /// same deep structural comparison semantics.
        /// </para>
        /// <para>
        /// This method is not CLS compliant due to its generic constraint.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// Mutable mutableElement = GetMutableElement();

        /// JsonElement readOnlyElement = GetReadOnlyElement();

        ///
        /// // Type-safe comparison
        /// bool equal = mutableElement.Equals(readOnlyElement);

        ///
        /// // Works with any IJsonElement implementation
        /// bool customEqual = mutableElement.Equals(customJsonElement);

        /// </code>
        /// </example>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals<T>(T other)
            where T : struct, IJsonElement
        {
            return JsonElementHelpers.DeepEquals(this, other);
        }

        /// <summary>
        /// Creates a JSON document builder from this mutable JSON element.
        /// </summary>
        /// <param name="workspace">The JSON workspace to use for the document builder.</param>
        /// <returns>A JSON document builder containing this mutable JSON element.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        public readonly JsonDocumentBuilder<Mutable> CreateBuilder(JsonWorkspace workspace)
        {
            return workspace.CreateBuilder<Mutable, Mutable>(this);
        }

        /// <summary>
        /// Creates a mutable JSON element from another mutable JSON element instance.
        /// </summary>
        /// <typeparam name="T">The type of the source mutable JSON element.</typeparam>
        /// <param name="instance">The source mutable JSON element instance.</param>
        /// <returns>A new mutable JSON element representing the same JSON value.</returns>
        /// <remarks>This method is not CLS compliant.</remarks>
        [CLSCompliant(false)]
        public static Mutable From<T>(in T instance)
            where T : struct, IMutableJsonElement<T>
        {
            return new(instance.ParentDocument, instance.ParentDocumentIndex);
        }

        /// <summary>
        /// Get the number of values contained within the current array value.
        /// </summary>
        /// <returns>The number of values contained within the current array value.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly int GetArrayLength()
        {
            CheckValidInstance();

            return _parent.GetArrayLength(_idx);
        }

        /// <summary>
        /// Get the number of properties contained within the current object value.
        /// </summary>
        /// <returns>The number of properties contained within the current object value.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly int GetPropertyCount()
        {
            CheckValidInstance();

            return _parent.GetPropertyCount(_idx);
        }

        /// <summary>
        /// Gets a <see cref="Mutable"/> representing the value of a required property identified
        /// by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        ///
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        /// A <see cref="Mutable"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// No property was found with the requested name.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly Mutable GetProperty(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            if (TryGetProperty(propertyName, out Mutable property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Gets a <see cref="Mutable"/> representing the value of a required property identified
        /// by <paramref name="propertyName"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        /// </para>
        ///
        /// <para>
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property whose value to return.</param>
        /// <returns>
        /// A <see cref="Mutable"/> representing the value of the requested property.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly Mutable GetProperty(ReadOnlySpan<char> propertyName)
        {
            if (TryGetProperty(propertyName, out Mutable property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Gets a <see cref="Mutable"/> representing the value of a required property identified
        /// by <paramref name="utf8PropertyName"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        /// </para>
        ///
        /// <para>
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <returns>
        /// A <see cref="Mutable"/> representing the value of the requested property.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// No property was found with the requested name.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public readonly Mutable GetProperty(ReadOnlySpan<byte> utf8PropertyName)
        {
            if (TryGetProperty(utf8PropertyName, out Mutable property))
            {
                return property;
            }

            throw new KeyNotFoundException();
        }

        /// <summary>
        /// Looks for a property named <paramref name="propertyName"/> in the current object, returning
        /// whether or not such a property existed. When the property exists <paramref name="value"/>
        /// is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        /// </para>
        ///
        /// <para>
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="propertyName"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="EnumerateObject"/>
        public readonly bool TryGetProperty(string propertyName, out Mutable value)
        {
            ArgumentNullException.ThrowIfNull(propertyName);

            return TryGetProperty(propertyName.AsSpan(), out value);
        }

        /// <summary>
        /// Looks for a property named <paramref name="propertyName"/> in the current object, returning
        /// whether or not such a property existed. When the property exists <paramref name="value"/>
        /// is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        /// </para>
        ///
        /// <para>
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </para>
        /// </remarks>
        /// <param name="propertyName">Name of the property to find.</param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetProperty(ReadOnlySpan<char> propertyName, out Mutable value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(_idx, propertyName, out value);
        }

        /// <summary>
        /// Looks for a property named <paramref name="utf8PropertyName"/> in the current object, returning
        /// whether or not such a property existed. When the property exists <paramref name="value"/>
        /// is assigned to the value of that property.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Property name matching is performed as an ordinal, case-sensitive, comparison.
        /// </para>
        ///
        /// <para>
        /// If a property is defined multiple times for the same object, the last such definition is
        /// what is matched.
        /// </para>
        /// </remarks>
        /// <param name="utf8PropertyName">
        /// The UTF-8 (with no Byte-Order-Mark (BOM)) representation of the name of the property to return.
        /// </param>
        /// <param name="value">Receives the value of the located property.</param>
        /// <returns>
        /// <see langword="true"/> if the property was found, <see langword="false"/> otherwise.
        /// </returns>
        /// <seealso cref="EnumerateObject"/>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetProperty(ReadOnlySpan<byte> utf8PropertyName, out Mutable value)
        {
            CheckValidInstance();

            return _parent.TryGetNamedPropertyValue(_idx, utf8PropertyName, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="bool"/>.
        /// </summary>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="bool"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.True"/> or
        /// <see cref="JsonValueKind.False"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool GetBoolean()
        {
            // CheckValidInstance is redundant.  Asking for the type will
            // return None, which then throws the same exception in the return statement.
            JsonTokenType type = TokenType;

#pragma warning disable IDE0075, RCS1104 // Simplify conditional expression
            return
                type == JsonTokenType.True ||
                (type == JsonTokenType.False ? false :
                ThrowJsonElementWrongTypeException(type));
#pragma warning restore IDE0075, RCS1104 // Simplify conditional expression

            static bool ThrowJsonElementWrongTypeException(JsonTokenType actualType)
            {
                throw ThrowHelper.GetJsonElementWrongTypeException(nameof(Boolean), actualType.ToValueKind());
            }
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="string"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a string representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="string"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is neither <see cref="JsonValueKind.String"/> nor <see cref="JsonValueKind.Null"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly string? GetString()
        {
            CheckValidInstance();

            return _parent.GetString(_idx, JsonTokenType.String);
        }

        /// <summary>
        /// Gets the value of the element as an unescaped UTF-8 JSON string.
        /// </summary>
        /// <returns>The value of the element as an unescaped UTF-8 JSON string.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly UnescapedUtf8JsonString GetUtf8String()
        {
            CheckValidInstance();

            return _parent.GetUtf8JsonString(_idx, JsonTokenType.String);
        }

        /// <summary>
        /// Gets the value of the element as an unescaped UTF-16 JSON string.
        /// </summary>
        /// <returns>The value of the element as an unescaped UTF-16 JSON string.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly UnescapedUtf16JsonString GetUtf16String()
        {
            CheckValidInstance();

            return _parent.GetUtf16JsonString(_idx, JsonTokenType.String);
        }

        /// <summary>
        /// Attempts to represent the current JSON string as bytes assuming it is Base64 encoded.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a byte[] representation of values other than base 64 encoded JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the entire token value is encoded as valid Base64 text and can be successfully decoded to bytes.
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetBytesFromBase64([NotNullWhen(true)] out byte[]? value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as bytes.
        /// </summary>
        /// <remarks>
        /// This method does not create a byte[] representation of values other than Base64 encoded JSON strings.
        /// </remarks>
        /// <returns>The value decode to bytes.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value is not encoded as Base64 text and hence cannot be decoded to bytes.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly byte[] GetBytesFromBase64()
        {
            if (!TryGetBytesFromBase64(out byte[]? value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as an <see cref="sbyte"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as an <see cref="sbyte"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly bool TryGetSByte(out sbyte value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as an <see cref="sbyte"/>.
        /// </summary>
        /// <returns>The current JSON number as an <see cref="sbyte"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as an <see cref="sbyte"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly sbyte GetSByte()
        {
            if (TryGetSByte(out sbyte value))
            {
                return value;
            }

            throw new FormatException();
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="byte"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="byte"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetByte(out byte value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="byte"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="byte"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="byte"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly byte GetByte()
        {
            if (TryGetByte(out byte value))
            {
                return value;
            }

            throw new FormatException();
        }

        /// <summary>
        /// Attempts to represent the current JSON number as an <see cref="short"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as an <see cref="short"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetInt16(out short value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as an <see cref="short"/>.
        /// </summary>
        /// <returns>The current JSON number as an <see cref="short"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as an <see cref="short"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly short GetInt16()
        {
            if (TryGetInt16(out short value))
            {
                return value;
            }

            throw new FormatException();
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="ushort"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="ushort"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly bool TryGetUInt16(out ushort value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="ushort"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="ushort"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="ushort"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly ushort GetUInt16()
        {
            if (TryGetUInt16(out ushort value))
            {
                return value;
            }

            throw new FormatException();
        }

        /// <summary>
        /// Attempts to represent the current JSON number as an <see cref="int"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as an <see cref="int"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetInt32(out int value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as an <see cref="int"/>.
        /// </summary>
        /// <returns>The current JSON number as an <see cref="int"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as an <see cref="int"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly int GetInt32()
        {
            if (!TryGetInt32(out int value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="uint"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="uint"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly bool TryGetUInt32(out uint value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="uint"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="uint"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="uint"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly uint GetUInt32()
        {
            if (!TryGetUInt32(out uint value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="long"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="long"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetInt64(out long value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="long"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="long"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="long"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly long GetInt64()
        {
            if (!TryGetInt64(out long value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="ulong"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="ulong"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly bool TryGetUInt64(out ulong value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="ulong"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="ulong"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="ulong"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly ulong GetUInt64()
        {
            if (!TryGetUInt64(out ulong value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="double"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// <para>
        /// This method does not parse the contents of a JSON string value.
        /// </para>
        ///
        /// <para>
        /// On .NET Core this method does not return <see langword="false"/> for values larger than
        /// <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>),
        /// instead <see langword="true"/> is returned and <see cref="double.PositiveInfinity"/> (or
        /// <see cref="double.NegativeInfinity"/>) is emitted.
        /// </para>
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="double"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetDouble(out double value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="double"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="double"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method does not parse the contents of a JSON string value.
        /// </para>
        ///
        /// <para>
        /// On .NET Core this method returns <see cref="double.PositiveInfinity"/> (or
        /// <see cref="double.NegativeInfinity"/>) for values larger than
        /// <see cref="double.MaxValue"/> (or smaller than <see cref="double.MinValue"/>).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="double"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly double GetDouble()
        {
            if (!TryGetDouble(out double value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="float"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// <para>
        /// This method does not parse the contents of a JSON string value.
        /// </para>
        ///
        /// <para>
        /// On .NET Core this method does not return <see langword="false"/> for values larger than
        /// <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>),
        /// instead <see langword="true"/> is returned and <see cref="float.PositiveInfinity"/> (or
        /// <see cref="float.NegativeInfinity"/>) is emitted.
        /// </para>
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="float"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetSingle(out float value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="float"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="float"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method does not parse the contents of a JSON string value.
        /// </para>
        ///
        /// <para>
        /// On .NET Core this method returns <see cref="float.PositiveInfinity"/> (or
        /// <see cref="float.NegativeInfinity"/>) for values larger than
        /// <see cref="float.MaxValue"/> (or smaller than <see cref="float.MinValue"/>).
        /// </para>
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="float"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly float GetSingle()
        {
            if (!TryGetSingle(out float value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="decimal"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="decimal"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly bool TryGetDecimal(out decimal value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="decimal"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="decimal"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="decimal"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly decimal GetDecimal()
        {
            if (!TryGetDecimal(out decimal value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

#if NET

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="Int128"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="Int128"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly bool TryGetInt128(out Int128 value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="Int128"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="Int128"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="Int128"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly Int128 GetInt128()
        {
            if (!TryGetInt128(out Int128 value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="UInt128"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="UInt128"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        [CLSCompliant(false)]
        public readonly bool TryGetUInt128(out UInt128 value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="UInt128"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="UInt128"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="UInt128"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        [CLSCompliant(false)]
        public readonly UInt128 GetUInt128()
        {
            if (!TryGetUInt128(out UInt128 value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="Half"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="Half"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly bool TryGetHalf(out Half value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="Half"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="Half"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="Half"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly Half GetHalf()
        {
            if (!TryGetHalf(out Half value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

#endif

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="BigNumber"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="BigNumber"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        [CLSCompliant(false)]
        public readonly bool TryGetBigNumber(out BigNumber value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="BigNumber"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="BigNumber"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="BigNumber"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        [CLSCompliant(false)]
        public readonly BigNumber GetBigNumber()
        {
            if (!TryGetBigNumber(out BigNumber value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON number as a <see cref="BigInteger"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the number can be represented as a <see cref="BigInteger"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly bool TryGetBigInteger(out BigInteger value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the current JSON number as a <see cref="BigInteger"/>.
        /// </summary>
        /// <returns>The current JSON number as a <see cref="BigInteger"/>.</returns>
        /// <remarks>
        /// This method does not parse the contents of a JSON string value.
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Number"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="BigInteger"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="GetRawText"/>
        public readonly BigInteger GetBigInteger()
        {
            if (!TryGetBigInteger(out BigInteger value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="LocalDate"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a LocalDate representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="LocalDate"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetLocalDate(out LocalDate value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="LocalDate"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a LocalDate representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="LocalDate"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly LocalDate GetLocalDate()
        {
            if (!TryGetLocalDate(out LocalDate value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="OffsetTime"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a OffsetTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="OffsetTime"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetOffsetTime(out OffsetTime value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="OffsetTime"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a OffsetTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="OffsetTime"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly OffsetTime GetOffsetTime()
        {
            if (!TryGetOffsetTime(out OffsetTime value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="OffsetDateTime"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a OffsetDateTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="OffsetDateTime"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetOffsetDateTime(out OffsetDateTime value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="OffsetDateTime"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a OffsetDateTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="OffsetDateTime"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly OffsetDateTime GetOffsetDateTime()
        {
            if (!TryGetOffsetDateTime(out OffsetDateTime value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="OffsetDate"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a OffsetDate representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="OffsetDate"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetOffsetDate(out OffsetDate value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="OffsetDate"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a OffsetDate representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="OffsetDate"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly OffsetDate GetOffsetDate()
        {
            if (!TryGetOffsetDate(out OffsetDate value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="Period"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a Period representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="Period"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetPeriod(out Period value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="Period"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a Period representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="Period"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly Period GetPeriod()
        {
            if (!TryGetPeriod(out Period value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="DateTime"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a DateTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="DateTime"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetDateTime(out DateTime value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="DateTime"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a DateTime representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="DateTime"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTime"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly DateTime GetDateTime()
        {
            if (!TryGetDateTime(out DateTime value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a DateTimeOffset representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="DateTimeOffset"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetDateTimeOffset(out DateTimeOffset value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a DateTimeOffset representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="DateTimeOffset"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="DateTimeOffset"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly DateTimeOffset GetDateTimeOffset()
        {
            if (!TryGetDateTimeOffset(out DateTimeOffset value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Attempts to represent the current JSON string as a <see cref="Guid"/>.
        /// </summary>
        /// <param name="value">Receives the value.</param>
        /// <remarks>
        /// This method does not create a Guid representation of values other than JSON strings.
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the string can be represented as a <see cref="Guid"/>,
        /// <see langword="false"/> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly bool TryGetGuid(out Guid value)
        {
            CheckValidInstance();

            return _parent.TryGetValue(_idx, out value);
        }

        /// <summary>
        /// Gets the value of the element as a <see cref="Guid"/>.
        /// </summary>
        /// <remarks>
        /// This method does not create a Guid representation of values other than JSON strings.
        /// </remarks>
        /// <returns>The value of the element as a <see cref="Guid"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <exception cref="FormatException">
        /// The value cannot be represented as a <see cref="Guid"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <seealso cref="ToString"/>
        public readonly Guid GetGuid()
        {
            if (!TryGetGuid(out Guid value))
            {
                ThrowHelper.ThrowFormatException();
            }

            return value;
        }

        /// <summary>
        /// Gets the original input data backing this value, returning it as a <see cref="string"/>.
        /// </summary>
        /// <returns>
        /// The original input data backing this value, returning it as a <see cref="string"/>.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly string GetRawText()
        {
            CheckValidInstance();

            return _parent.GetRawValueAsString(_idx);
        }

        internal readonly bool ValueIsEscaped
        {
            get
            {
                CheckValidInstance();

                return _parent.ValueIsEscaped(_idx, isPropertyName: false);
            }
        }

        internal readonly ReadOnlySpan<byte> ValueSpan
        {
            get
            {
                CheckValidInstance();

                return _parent.GetRawValue(_idx, includeQuotes: false).Span;
            }
        }

        public static void EnsurePropertyMap(in Mutable element)
        {
            element._parent.EnsurePropertyMap(element._idx);
        }

        /// <summary>
        /// Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        /// <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        /// <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        /// This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        /// the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public readonly bool ValueEquals(string? text)
        {
            // CheckValidInstance is done in the helper
            if (TokenType == JsonTokenType.Null)
            {
                return text == null;
            }

            return TextEqualsHelper(text.AsSpan(), isPropertyName: false);
        }

        /// <summary>
        /// Compares the text represented by <paramref name="utf8Text" /> to the string value of this element.
        /// </summary>
        /// <param name="utf8Text">The UTF-8 encoded text to compare against.</param>
        /// <returns>
        /// <see langword="true" /> if the string value of this element has the same UTF-8 encoding as
        /// <paramref name="utf8Text" />, <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        /// This method is functionally equal to doing an ordinal comparison of the string produced by UTF-8 decoding
        /// <paramref name="utf8Text" /> with the result of calling <see cref="GetString" />, but avoids creating the
        /// string instances.
        /// </remarks>
        public readonly bool ValueEquals(ReadOnlySpan<byte> utf8Text)
        {
            // CheckValidInstance is done in the helper
            if (TokenType == JsonTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
                return utf8Text.Slice(0, 0) == default;
#pragma warning restore CA2265
            }

            return TextEqualsHelper(utf8Text, isPropertyName: false, shouldUnescape: true);
        }

        /// <summary>
        /// Compares <paramref name="text" /> to the string value of this element.
        /// </summary>
        /// <param name="text">The text to compare against.</param>
        /// <returns>
        /// <see langword="true" /> if the string value of this element matches <paramref name="text"/>,
        /// <see langword="false" /> otherwise.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.String"/>.
        /// </exception>
        /// <remarks>
        /// This method is functionally equal to doing an ordinal comparison of <paramref name="text" /> and
        /// the result of calling <see cref="GetString" />, but avoids creating the string instance.
        /// </remarks>
        public readonly bool ValueEquals(ReadOnlySpan<char> text)
        {
            // CheckValidInstance is done in the helper
            if (TokenType == JsonTokenType.Null)
            {
                // This is different than Length == 0, in that it tests true for null, but false for ""
#pragma warning disable CA2265
                return text.Slice(0, 0) == default;
#pragma warning restore CA2265
            }

            return TextEqualsHelper(text, isPropertyName: false);
        }

        internal readonly bool TextEqualsHelper(ReadOnlySpan<byte> utf8Text, bool isPropertyName, bool shouldUnescape)
        {
            CheckValidInstance();

            return _parent.TextEquals(_idx, utf8Text, isPropertyName, shouldUnescape);
        }

        internal readonly bool TextEqualsHelper(ReadOnlySpan<char> text, bool isPropertyName)
        {
            CheckValidInstance();

            return _parent.TextEquals(_idx, text, isPropertyName);
        }

        /// <summary>
        /// Write the element into the provided writer as a JSON value.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is <see cref="JsonValueKind.Undefined"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public readonly void WriteTo(Utf8JsonWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            CheckValidInstance();

            _parent.WriteElementTo(_idx, writer);
        }

        /// <summary>
        /// Get an enumerator to enumerate the values in the JSON array represented by this Mutable.
        /// </summary>
        /// <returns>
        /// An enumerator to enumerate the values in the JSON array represented by this Mutable.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly ArrayEnumerator<Mutable> EnumerateArray()
        {
            CheckValidInstance();

            JsonTokenType tokenType = TokenType;

            if (tokenType != JsonTokenType.StartArray)
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartArray, tokenType);
            }

            return new ArrayEnumerator<Mutable>(_parent, _idx);
        }

        /// <summary>
        /// Get an enumerator to enumerate the properties in the JSON object represented by this Mutable.
        /// </summary>
        /// <returns>
        /// An enumerator to enumerate the properties in the JSON object represented by this Mutable.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [CLSCompliant(false)]
        public readonly ObjectEnumerator<Mutable> EnumerateObject()
        {
            CheckValidInstance();

            JsonTokenType tokenType = TokenType;

            if (tokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonElementWrongTypeException(JsonTokenType.StartObject, tokenType);
            }

            return new ObjectEnumerator<Mutable>(_parent, _idx);
        }

        /// <summary>
        /// Gets a string representation for the current value appropriate to the value type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For Mutable built from <see cref="IMutableJsonDocument"/>:
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.Null"/>, <see cref="string.Empty"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.True"/>, <see cref="bool.TrueString"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.False"/>, <see cref="bool.FalseString"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.String"/>, the value of <see cref="GetString"/>() is returned.
        /// </para>
        ///
        /// <para>
        /// For other types, the value of <see cref="GetRawText"/>() is returned.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A string representation for the current value appropriate to the value type.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public override readonly string ToString()
        {
            if (_parent == null || _documentVersion != _parent.Version)
            {
                return string.Empty;
            }

            return _parent.ToString(_idx);
        }

        /// <inheritdoc />
        public override readonly int GetHashCode()
        {
            if (_parent is null)
            {
                return 0;
            }

            return _parent.GetHashCode(_idx);
        }

        /// <inheritdoc/>
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            CheckValidInstance();

            return _parent.ToString(_idx, format, formatProvider);
        }

        /// <inheritdoc/>
        public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            CheckValidInstance();

            return _parent.TryFormat(_idx, destination, out charsWritten, format, provider);
        }

        /// <inheritdoc/>
        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        {
            CheckValidInstance();

            return _parent.TryFormat(_idx, utf8Destination, out bytesWritten, format, provider);
        }

        /// <summary>
        /// Get a JsonElement which can be safely stored beyond the lifetime of the
        /// original <see cref="IMutableJsonDocument"/>.
        /// </summary>
        /// <returns>
        /// A JsonElement which can be safely stored beyond the lifetime of the
        /// original <see cref="IMutableJsonDocument"/>.
        /// </returns>
        public readonly JsonElement Clone()
        {
            CheckValidInstance();

            return _parent.CloneElement(_idx);
        }

        /// <summary>
        /// Sets a JSON object property on this element using a value Source.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="source">The source of the property.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetProperty(string propertyName, in Source source)
        {
            SetProperty(propertyName.AsSpan(), source);
        }

        /// <summary>
        /// Sets a JSON object property on this element using a value Source.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="source">The source of the property.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<char> propertyName, in Source source, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            if (source.IsUndefined)
            {
                JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);
                _documentVersion = _parent.Version;
                return;
            }

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                source.AddAsItem(ref cvb);
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                source.AddAsProperty(propertyName, ref cvb);
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON object property on this element using a value Source.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="source">The source of the property.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<byte> propertyName, in Source source, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            if (source.IsUndefined)
            {
                JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);
                _documentVersion = _parent.Version;
                return;
            }

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                source.AddAsItem(ref cvb);
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                source.AddAsProperty(propertyName, ref cvb);
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetProperty(string propertyName, ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            SetProperty(propertyName.AsSpan(), objectValue, estimatedMemberCount);
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetProperty<TContext>(string propertyName, in TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            SetProperty(propertyName.AsSpan(), context, objectValue, estimatedMemberCount);
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set as a character span.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a character span to avoid string allocation when the property name
        /// is already available as a span.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<char> propertyName, ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem((ref o) => ObjectBuilder.BuildValue(objectValue, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, objectValue, static (in build, ref o) => ObjectBuilder.BuildValue(build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a character span to avoid string allocation when the property name
        /// is already available as a span.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty<TContext>(ReadOnlySpan<char> propertyName, TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set as a UTF-8 encoded byte span.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a UTF-8 encoded byte span for optimal performance when working
        /// with UTF-8 property names, avoiding encoding conversions.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<byte> propertyName, ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(objectValue, static (in build, ref o) => ObjectBuilder.BuildValue(build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, objectValue, static (in build, ref o) => ObjectBuilder.BuildValue(build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON object property on this element using a JsonObjectBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="objectValue">A delegate that builds the JSON object value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a UTF-8 encoded byte span for optimal performance when working
        /// with UTF-8 property names, avoiding encoding conversions.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty<TContext>(ReadOnlySpan<byte> propertyName, TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of elements in the array for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetProperty(string propertyName, ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            SetProperty(propertyName.AsSpan(), arrayValue, estimatedMemberCount);
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetProperty<TContext>(string propertyName, in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            SetProperty(propertyName.AsSpan(), context, arrayValue, estimatedMemberCount);
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set as a character span.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of elements in the array for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a character span to avoid string allocation when the property name
        /// is already available as a span.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<char> propertyName, ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(arrayValue, static (in build, ref o) => ArrayBuilder.BuildValue(build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, arrayValue, static (in arrayValue, ref o) => ArrayBuilder.BuildValue(arrayValue, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a character span to avoid string allocation when the property name
        /// is already available as a span.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty<TContext>(ReadOnlySpan<char> propertyName, in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(BuildWithContext.Create(context, arrayValue), static (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, BuildWithContext.Create(context, arrayValue), (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <param name="propertyName">The name of the property to set as a UTF-8 encoded byte span.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of elements in the array for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a UTF-8 encoded byte span for optimal performance when working
        /// with UTF-8 property names, avoiding encoding conversions.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty(ReadOnlySpan<byte> propertyName, ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(arrayValue, static (in arrayValue, ref o) => ArrayBuilder.BuildValue(arrayValue, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, arrayValue, static (in arrayValue, ref o) => ArrayBuilder.BuildValue(arrayValue, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a JSON array property on this element using a JsonArrayBuilder.Build delegate.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="context">The builder context.</param>
        /// <param name="arrayValue">A delegate that builds the JSON array value.</param>
        /// <param name="estimatedMemberCount">An estimate of the number of members in the object for performance optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a UTF-8 encoded byte span for optimal performance when working
        /// with UTF-8 property names, avoiding encoding conversions.
        /// </para>
        /// <para>
        /// The <paramref name="estimatedMemberCount"/> parameter helps optimize memory allocation.
        /// Providing an accurate estimate can improve performance by reducing reallocations.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced.
        /// If the property doesn't exist, it will be added to the object.
        /// </para>
        /// </remarks>
        public void SetProperty<TContext>(ReadOnlySpan<byte> propertyName, in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItem(BuildWithContext.Create(context, arrayValue), (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddProperty(propertyName, BuildWithContext.Create(context, arrayValue), static (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a property to JSON null on this JSON object element.
        /// </summary>
        /// <param name="propertyName">The name of the property to set to null.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This sets the property to the JSON null value, not the .NET null reference.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced with null.
        /// If the property doesn't exist, it will be added to the object with a null value.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPropertyNull(string propertyName)
        {
            SetPropertyNull(propertyName.AsSpan());
        }

        /// <summary>
        /// Sets a property to JSON null on this JSON object element.
        /// </summary>
        /// <param name="propertyName">The name of the property to set to null as a character span.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a character span to avoid string allocation when the property name
        /// is already available as a span.
        /// </para>
        /// <para>
        /// This sets the property to the JSON null value, not the .NET null reference.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced with null.
        /// If the property doesn't exist, it will be added to the object with a null value.
        /// </para>
        /// </remarks>
        public void SetPropertyNull(ReadOnlySpan<char> propertyName)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, 1);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItemNull();
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddPropertyNull(propertyName);
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets a property to JSON null on this JSON object element.
        /// </summary>
        /// <param name="propertyName">The name of the property to set to null as a UTF-8 encoded byte span.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This overload accepts a UTF-8 encoded byte span for optimal performance when working
        /// with UTF-8 property names, avoiding encoding conversions.
        /// </para>
        /// <para>
        /// This sets the property to the JSON null value, not the .NET null reference.
        /// </para>
        /// <para>
        /// If the property already exists, its value will be replaced with null.
        /// If the property doesn't exist, it will be added to the object with a null value.
        /// </para>
        /// </remarks>
        public void SetPropertyNull(ReadOnlySpan<byte> propertyName)
        {
            CheckValidInstance();

            var cvb = ComplexValueBuilder.Create(_parent, 1);
            if (_parent.TryGetNamedPropertyValue(_idx, propertyName, out JsonElement value))
            {
                // We are going to replace just the value
                cvb.AddItemNull();
                _parent.OverwriteAndDispose(_idx, value._idx, value._idx + value._parent.GetDbSize(value._idx, true), 1, ref cvb);
            }
            else
            {
                // We are going to insert the new value
                cvb.AddPropertyNull(propertyName);
                int endIndex = _idx + _parent.GetDbSize(_idx, false);
                _parent.InsertAndDispose(_idx, endIndex, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Removes the property with the given name, if present.
        /// </summary>
        /// <param name="propertyName">The property name to remove.</param>
        /// <returns><see langword="true"/> if the property was found and removed; otherwise, <see langword="false"/>.</returns>
        public bool RemoveProperty(string propertyName)
        {
            return RemoveProperty(propertyName.AsSpan());
        }

        /// <summary>
        /// Removes the property with the given name, if present.
        /// </summary>
        /// <param name="propertyName">The property name to remove.</param>
        /// <returns><see langword="true"/> if the property was found and removed; otherwise, <see langword="false"/>.</returns>
        public bool RemoveProperty(ReadOnlySpan<char> propertyName)
        {
            CheckValidInstance();
            bool result = JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);
            _documentVersion = _parent.Version;
            return result;
        }

        /// <summary>
        /// Removes the property with the given name, if present.
        /// </summary>
        /// <param name="propertyName">The property name to remove.</param>
        /// <returns><see langword="true"/> if the property was found and removed; otherwise, <see langword="false"/>.</returns>
        public bool RemoveProperty(ReadOnlySpan<byte> propertyName)
        {
            CheckValidInstance();
            bool result = JsonElementHelpers.RemovePropertyUnsafe(_parent, _idx, propertyName);
            _documentVersion = _parent.Version;
            return result;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index using a value Source.
        /// </summary>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <param name="source">The source of the item.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The object is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON objects.
        /// </para>
        /// </remarks>
        public void SetItem(int itemIndex, in Source source, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            if (source.IsUndefined)
            {
                RemoveAt(itemIndex);
                return;
            }

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            source.AddAsItem(ref cvb);
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON object.
        /// </summary>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The object is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON objects.
        /// </para>
        /// </remarks>
        public void SetItem(int itemIndex, ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(objectValue, static (in objectValue, ref o) => ObjectBuilder.BuildValue(objectValue, ref o));
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON object.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The object is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON objects.
        /// </para>
        /// </remarks>
        public void SetItem<TContext>(int itemIndex, in TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON array.
        /// </summary>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The array is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON arrays.
        /// </para>
        /// </remarks>
        public void SetItem(int itemIndex, ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(arrayValue, static (in arrayValue, ref o) => ArrayBuilder.BuildValue(arrayValue, ref o));
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON array.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The array is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON arrays.
        /// </para>
        /// </remarks>
        public void SetItem<TContext>(int itemIndex, in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(BuildWithContext.Create(context, arrayValue), static (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to null.
        /// </summary>
        /// <param name="itemIndex">The zero-based index of the array element to set.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </remarks>
        public void SetItemNull(int itemIndex)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, 1);
            cvb.AddItemNull();
            int arrayLength = GetArrayLength();
            if (itemIndex == arrayLength)
            {
                _parent.InsertAndDispose(_idx, _idx + _parent.GetDbSize(_idx, false), ref cvb);
            }
            else
            {
                Mutable element = _parent.GetArrayIndexElement(_idx, itemIndex);
                _parent.OverwriteAndDispose(_idx, element._idx, element._idx + element._parent.GetDbSize(element._idx, true), 1, ref cvb);
            }

            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Inserts an array element at the specified index from a <see cref="Source"/> value.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <param name="source">The source value to insert.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the value for capacity optimization.</param>
        public void InsertItem(int itemIndex, in Source source, int estimatedMemberCount = 30)
        {
            CheckValidInstance();

            if (source.IsUndefined)
            {
                return;
            }

            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            source.AddAsItem(ref cvb);
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON object.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The object is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON objects.
        /// </para>
        /// </remarks>
        public void InsertItem(int itemIndex, ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(objectValue, static (in objectValue, ref o) => ObjectBuilder.BuildValue(objectValue, ref o));
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON object.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The object is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON objects.
        /// </para>
        /// </remarks>
        public void InsertItem<TContext>(int itemIndex, in TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(BuildWithContext.Create(context, objectValue), static (in context, ref o) => ObjectBuilder.BuildValue(context.Context, context.Build, ref o));
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON array.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The array is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON arrays.
        /// </para>
        /// </remarks>
        public void InsertItem(int itemIndex, ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(arrayValue, static (in arrayValue, ref o) => ArrayBuilder.BuildValue(arrayValue, ref o));
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to a JSON array.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </para>
        /// <para>
        /// The array is constructed using the provided builder delegate, which provides a fluent API
        /// for efficiently building nested JSON arrays.
        /// </para>
        /// </remarks>
        public void InsertItem<TContext>(int itemIndex, in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            cvb.AddItem(BuildWithContext.Create(context, arrayValue), static (in context, ref o) => ArrayBuilder.BuildValue(context.Context, context.Build, ref o));
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Sets the value of an array element at the specified index to null.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the item.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// A new element will be inserted when <paramref name="itemIndex"/> equals the current array length.
        /// </remarks>
        public void InsertItemNull(int itemIndex)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, 1);
            cvb.AddItemNull();
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Adds an item to the end of the array using a value <see cref="Source"/>.
        /// </summary>
        /// <param name="source">The source of the item to add.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem(in Source source, int estimatedMemberCount = 30)
        {
            InsertItem(GetArrayLength(), in source, estimatedMemberCount);
        }

        /// <summary>
        /// Adds a JSON object item to the end of the array.
        /// </summary>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem(ObjectBuilder.Build objectValue, int estimatedMemberCount = 30)
        {
            InsertItem(GetArrayLength(), objectValue, estimatedMemberCount);
        }

        /// <summary>
        /// Adds a JSON object item to the end of the array.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="objectValue">The object builder delegate that constructs the JSON object.</param>
        /// <param name="estimatedMemberCount">The estimated number of members in the object for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem<TContext>(in TContext context, ObjectBuilder.Build<TContext> objectValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            InsertItem(GetArrayLength(), in context, objectValue, estimatedMemberCount);
        }

        /// <summary>
        /// Adds a JSON array item to the end of the array.
        /// </summary>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem(ArrayBuilder.Build arrayValue, int estimatedMemberCount = 30)
        {
            InsertItem(GetArrayLength(), arrayValue, estimatedMemberCount);
        }

        /// <summary>
        /// Adds a JSON array item to the end of the array.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="context">The context to pass to the builder.</param>
        /// <param name="arrayValue">The array builder delegate that constructs the JSON array.</param>
        /// <param name="estimatedMemberCount">The estimated number of elements in the array for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItem<TContext>(in TContext context, ArrayBuilder.Build<TContext> arrayValue, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            InsertItem(GetArrayLength(), in context, arrayValue, estimatedMemberCount);
        }

        /// <summary>
        /// Adds a null item to the end of the array.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddItemNull()
        {
            InsertItemNull(GetArrayLength());
        }

        /// <summary>
        /// Inserts multiple items into the array at the specified index, using
        /// an <see cref="ArrayBuilder"/> delegate to build the items.
        /// </summary>
        /// <param name="itemIndex">The zero-based index at which to insert the items.</param>
        /// <param name="rangeBuilder">
        /// A delegate that adds items to an <see cref="ArrayBuilder"/>. Each item
        /// added inside the delegate is inserted into this array at <paramref name="itemIndex"/>.
        /// </param>
        /// <param name="estimatedMemberCount">The estimated total number of elements for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Only the items built by the delegate are inserted — no array wrapper is added.
        /// If the delegate adds no items, this is a no-op.
        /// </para>
        /// </remarks>
        public void InsertRange(int itemIndex, ArrayBuilder.Build rangeBuilder, int estimatedMemberCount = 30)
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            ArrayBuilder.BuildItems(rangeBuilder, ref cvb);
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Inserts multiple items into the array at the specified index, using
        /// an <see cref="ArrayBuilder"/> delegate with a context parameter.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="itemIndex">The zero-based index at which to insert the items.</param>
        /// <param name="context">The context to pass to the builder delegate.</param>
        /// <param name="rangeBuilder">
        /// A delegate that adds items to an <see cref="ArrayBuilder"/>. Each item
        /// added inside the delegate is inserted into this array at <paramref name="itemIndex"/>.
        /// </param>
        /// <param name="estimatedMemberCount">The estimated total number of elements for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="itemIndex"/> is negative or greater than the current array length.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Only the items built by the delegate are inserted — no array wrapper is added.
        /// If the delegate adds no items, this is a no-op.
        /// </para>
        /// </remarks>
        public void InsertRange<TContext>(int itemIndex, in TContext context, ArrayBuilder.Build<TContext> rangeBuilder, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            CheckValidInstance();
            var cvb = ComplexValueBuilder.Create(_parent, estimatedMemberCount);
            ArrayBuilder.BuildItems(context, rangeBuilder, ref cvb);
            _parent.InsertAndDispose(_idx, _parent.GetArrayInsertionIndex(_idx, itemIndex), ref cvb);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Appends multiple items to the end of the array, using an <see cref="ArrayBuilder"/>
        /// delegate to build the items.
        /// </summary>
        /// <param name="rangeBuilder">
        /// A delegate that adds items to an <see cref="ArrayBuilder"/>. Each item
        /// added inside the delegate is appended to this array.
        /// </param>
        /// <param name="estimatedMemberCount">The estimated total number of elements for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Only the items built by the delegate are added — no array wrapper is added.
        /// If the delegate adds no items, this is a no-op.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange(ArrayBuilder.Build rangeBuilder, int estimatedMemberCount = 30)
        {
            InsertRange(GetArrayLength(), rangeBuilder, estimatedMemberCount);
        }

        /// <summary>
        /// Appends multiple items to the end of the array, using an <see cref="ArrayBuilder"/>
        /// delegate with a context parameter.
        /// </summary>
        /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
        /// <param name="context">The context to pass to the builder delegate.</param>
        /// <param name="rangeBuilder">
        /// A delegate that adds items to an <see cref="ArrayBuilder"/>. Each item
        /// added inside the delegate is appended to this array.
        /// </param>
        /// <param name="estimatedMemberCount">The estimated total number of elements for capacity optimization.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Only the items built by the delegate are added — no array wrapper is added.
        /// If the delegate adds no items, this is a no-op.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddRange<TContext>(in TContext context, ArrayBuilder.Build<TContext> rangeBuilder, int estimatedMemberCount = 30)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            InsertRange(GetArrayLength(), context, rangeBuilder, estimatedMemberCount);
        }

        /// <summary>
        /// Removes a range of items from the array starting at the specified index.
        /// </summary>
        /// <param name="startIndex">The zero-based index at which to begin removing items.</param>
        /// <param name="count">The number of items to remove.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="startIndex"/> is negative or greater than the current array length.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveRange(int startIndex, int count)
        {
            CheckValidInstance();
            JsonElementHelpers.RemoveRangeUnsafe(this, startIndex, count);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Removes a single item from the array at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the item to remove.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="index"/> is negative or greater than or equal to the current array length.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void RemoveAt(int index)
        {
            CheckValidInstance();
            JsonElementHelpers.RemoveRangeUnsafe(this, index, 1);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Removes the first array element that equals the specified item.
        /// </summary>
        /// <param name="item">The item to find and remove.</param>
        /// <returns><see langword="true"/> if an element was found and removed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method iterates forward through the array and removes the first element
        /// whose value deeply equals the specified <paramref name="item"/>.
        /// </para>
        /// </remarks>
        [CLSCompliant(false)]
        public bool Remove(in JsonElement item)
        {
            CheckValidInstance();
            if (!JsonElementHelpers.RemoveFirstUnsafe<Mutable, JsonElement>(this, in item))
            {
                return false;
            }

            _documentVersion = _parent.Version;
            return true;
        }

        /// <summary>
        /// Replaces the first array element that deeply equals the specified item
        /// with a new value.
        /// </summary>
        /// <param name="oldItem">The item to find.</param>
        /// <param name="newItem">The value to replace it with.</param>
        /// <returns><see langword="true"/> if an element was found and replaced; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method iterates forward through the array and replaces the first element
        /// whose value deeply equals the specified <paramref name="oldItem"/>.
        /// </para>
        /// </remarks>
        [CLSCompliant(false)]
        public bool Replace(in JsonElement oldItem, in Source newItem)
        {
            CheckValidInstance();

            if (newItem.IsUndefined)
            {
                return Remove(in oldItem);
            }

            ArrayEnumerator<JsonElement> enumerator = new(_parent, _idx);

            while (enumerator.MoveNext())
            {
                JsonElement current = enumerator.Current;
                if (JsonElementHelpers.DeepEquals(in current, in oldItem))
                {
                    var cvb = ComplexValueBuilder.Create(_parent, 30);
                    newItem.AddAsItem(ref cvb);

                    int elementStart = ((IJsonElement)current).ParentDocumentIndex;
                    int elementEnd = elementStart + ((IJsonElement)current).ParentDocument.GetDbSize(elementStart, true);
                    _parent.OverwriteAndDispose(_idx, elementStart, elementEnd, 1, ref cvb);

                    _documentVersion = _parent.Version;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all array elements that match the specified predicate.
        /// </summary>
        /// <typeparam name="T">The type of JSON element implementing <see cref="IJsonElement{T}"/>.</typeparam>
        /// <param name="predicate">The predicate function that determines which elements to remove.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method efficiently removes elements in a single pass by iterating backwards
        /// through the array and removing consecutive blocks of matching elements.
        /// </para>
        /// <para>
        /// The predicate function is called for each element in the array. If the predicate
        /// returns <see langword="true"/>, the element will be removed from the array.
        /// </para>
        /// <para>
        /// This generic overload accepts any type implementing <see cref="IJsonElement{T}"/>,
        /// enabling type-safe element processing with compile-time type checking.
        /// </para>
        /// <para>
        /// This method is not CLS-compliant due to its generic constraint requirements.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public void RemoveWhere<T>(JsonPredicate<T> predicate)
            where T : struct, IJsonElement<T>
        {
            CheckValidInstance();
            JsonElementHelpers.RemoveWhereUnsafe<Mutable, T>(this, predicate);
            _documentVersion = _parent.Version;
        }

        /// <summary>
        /// Removes all array elements that match the specified predicate.
        /// </summary>
        /// <param name="predicate">The predicate function that determines which elements to remove.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="predicate"/> is <see langword="null"/>.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method efficiently removes elements in a single pass by iterating backwards
        /// through the array and removing consecutive blocks of matching elements.
        /// </para>
        /// <para>
        /// The predicate function is called for each element in the array. If the predicate
        /// returns <see langword="true"/>, the element will be removed from the array.
        /// </para>
        /// <para>
        /// This overload is a convenience method that calls the generic version with
        /// <see cref="JsonElement"/> as the type parameter.
        /// </para>
        /// <para>
        /// This method is not CLS-compliant due to its generic constraint requirements.
        /// </para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public void RemoveWhere(JsonPredicate<JsonElement> predicate)
        {
            RemoveWhere<JsonElement>(predicate);
        }

        /// <summary>
        /// Applies all properties from another JSON object element to this JSON object element.
        /// </summary>
        /// <typeparam name="T">The type of JSON element implementing <see cref="IJsonElement{T}"/>.</typeparam>
        /// <param name="value">The JSON object element whose properties will be copied to this element.</param>
        /// <exception cref="InvalidOperationException">
        /// This element's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// the source <paramref name="value"/>'s <see cref="ValueKind"/> is not <see cref="JsonValueKind.Object"/>,
        /// or the element reference is stale due to document mutations.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method performs a merge of properties from the source JSON object
        /// to this JSON object. Each property from the source object is copied to this object,
        /// replacing any existing properties with the same name.
        /// </para>
        /// <para>
        /// Both this element and the source value must be JSON object elements.
        /// </para>
        /// <para>
        /// This method is not CLS-compliant due to its generic constraint requirements.
        /// </para>
        /// </remarks>
        [CLSCompliant(false)]
        public void Apply<T>(in T value)
            where T : struct, IJsonElement<T>
        {
            CheckValidInstance();
            JsonElementHelpers.ApplyUnsafe(this, value);
            _documentVersion = _parent.Version;
        }

        private readonly void CheckValidInstance()
        {
            if (_parent == null)
            {
                throw new InvalidOperationException();
            }

            if (_idx != 0 && _documentVersion != _parent.Version)
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc />
        readonly void IJsonElement.CheckValidInstance() => CheckValidInstance();

#if NET

        /// <inheritdoc />
        static Mutable IJsonElement<Mutable>.CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => new(parentDocument, parentDocumentIndex);

#endif

        /// <summary>
        /// Evaluates the schema against this JSON element.
        /// </summary>
        /// <param name="resultsCollector">The optional results collector for schema validation.</param>
        /// <returns>True if the schema evaluation passes; otherwise, false.</returns>
        public readonly bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null) => JsonSchema.Evaluate(_parent, _idx, resultsCollector);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly string DebuggerDisplay => $"JsonElement.Mutable: ValueKind = {ValueKind} : \"{ToString()}\"";

        /// <inheritdoc />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly IJsonDocument IJsonElement.ParentDocument => _parent;

        /// <inheritdoc />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly int IJsonElement.ParentDocumentIndex => _idx;

        /// <inheritdoc />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly JsonTokenType IJsonElement.TokenType => TokenType;

        /// <inheritdoc />
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly JsonValueKind IJsonElement.ValueKind => ValueKind;
    }
}