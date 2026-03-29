// <copyright file="JsonElement.ObjectBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Numerics;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using NodaTime;
using static Corvus.Text.Json.JsonElement.ArrayBuilder;

namespace Corvus.Text.Json;

public readonly partial struct JsonElement
{
    /// <summary>
    /// Provides a high-performance, low-allocation builder for constructing JSON objects
    /// within an <see cref="IMutableJsonDocument"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ObjectBuilder"/> is a ref struct designed for use in stack-based scenarios,
    /// enabling efficient construction of JSON objects by directly manipulating the underlying metadata database.
    /// </para>
    /// <para>
    /// This builder supports adding properties of various types, including primitives, strings, numbers, booleans, nulls,
    /// arrays, and complex/nested values. It also provides methods for starting and ending JSON objects, as well as
    /// for integrating with <see cref="IMutableJsonDocument"/> for document mutation.
    /// </para>
    /// <para>
    /// Typical usage involves creating a builder via <see cref="ComplexValueBuilder.Create(IMutableJsonDocument, int)"/>,
    /// using <see cref="Add"/> and related methods to populate the structure,
    /// and then finalizing with <see cref="BuildValue"/>.
    /// </para>
    /// <para>
    /// This type is not thread-safe and must not be stored on the heap.
    /// </para>
    /// </remarks>
    public ref struct ObjectBuilder()
    {
        /// <summary>
        /// Delegate for building a value using a <see cref="ObjectBuilder"/>.
        /// </summary>
        /// <param name="builder">The builder to use for value construction.</param>
        public delegate void Build(ref ObjectBuilder builder);

#if NET9_0_OR_GREATER
        public delegate void Build<T>(in T context, ref ObjectBuilder builder)
            where T : allows ref struct;
#else
        public delegate void Build<T>(in T context, ref ObjectBuilder builder);
#endif

        private ComplexValueBuilder _builder;

        /// <summary>
        /// Initializes a new instance of the <see cref="ObjectBuilder"/> struct.
        /// </summary>
        /// <param name="builder">The underlying <see cref="ComplexValueBuilder"/> to use.</param>
        internal ObjectBuilder(ComplexValueBuilder builder)
            : this() => _builder = builder;

        /// <summary>
        /// Builds a JSON object value using the provided delegate and value builder.
        /// </summary>
        /// <param name="value">The delegate to build the object.</param>
        /// <param name="valueBuilder">The <see cref="ComplexValueBuilder"/> to use.</param>
        public static void BuildValue(Build value, ref ComplexValueBuilder valueBuilder)
        {
            valueBuilder.StartObject();
            ObjectBuilder objectBuilder = new(valueBuilder);
            value(ref objectBuilder);
            valueBuilder = objectBuilder._builder;
            valueBuilder.EndObject();
        }

        /// <summary>
        /// Builds a JSON array value using the provided delegate and value builder.
        /// </summary>
        /// <param name="value">The delegate to build the array.</param>
        /// <param name="valueBuilder">The <see cref="ComplexValueBuilder"/> to use.</param>
        public static void BuildValue<TContext>(in TContext context, Build<TContext> value, ref ComplexValueBuilder valueBuilder)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            valueBuilder.StartObject();
            ObjectBuilder ovb = new(valueBuilder);
            value(context, ref ovb);
            valueBuilder = ovb._builder;
            valueBuilder.EndObject();
        }

        /// <summary>
        /// Adds a property with a formatted number value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The number value as a UTF-8 byte span.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddFormattedNumber(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyFormattedNumber(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a raw string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The value as a UTF-8 byte span.</param>
        /// <param name="valueRequiresUnescaping">Whether the value requires unescaping.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddRawString(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value, bool valueRequiresUnescaping, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyRawString(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping,
                valueRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a complex value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">A delegate that builds the property value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, Build value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                static (in contextValue, ref valueBuilder) => BuildValue(contextValue, ref valueBuilder),
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a complex value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">A delegate that builds the property value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty<TContext>(ReadOnlySpan<byte> propertyName, in TContext context, Build<TContext> value, bool escapeName = true, bool nameRequiresUnescaping = false)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                BuildWithContext.Create(context, value),
                static (in context, ref valueBuilder) => BuildValue(context.Context, context.Build, ref valueBuilder),
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a JSON array value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">A delegate that builds the array value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, ArrayBuilder.Build value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                static (in contextValue, ref valueBuilder) => ArrayBuilder.BuildValue(contextValue, ref valueBuilder),
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a JSON array value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">A delegate that builds the array value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty<TContext>(ReadOnlySpan<byte> propertyName, in TContext context, ArrayBuilder.Build<TContext> value, bool escapeName = true, bool nameRequiresUnescaping = false)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                BuildWithContext.Create(context, value),
                static (in context, ref valueBuilder) => ArrayBuilder.BuildValue(context.Context, context.Build, ref valueBuilder),
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="utf8String">The property value as a UTF-8 byte span.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> utf8String, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                utf8String,
                escapeName,
                true,
                nameRequiresUnescaping,
                false);
        }

        /// <summary>
        /// Adds a property with a string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="value">The property value as a string.</param>
        public void AddProperty(string propertyName, string value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The property value as a character span.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a null value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddPropertyNull(ReadOnlySpan<byte> propertyName, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyNull(propertyName, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a boolean value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The boolean value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, bool value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a JSON element value to the current object.
        /// </summary>
        /// <typeparam name="T">The type of the JSON element value.</typeparam>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The JSON element value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty<T>(ReadOnlySpan<byte> propertyName, T value, bool escapeName = true, bool nameRequiresUnescaping = false)
            where T : struct, IJsonElement<T>
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a boolean value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The string value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, string value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a boolean value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The string value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a GUID value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The GUID value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, Guid value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="DateTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="DateTime"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTime value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="DateTimeOffset"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in DateTimeOffset value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetDateTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="OffsetDateTime"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDateTime value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetDate"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="OffsetDate"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetDate value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="OffsetTime"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in OffsetTime value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="LocalDate"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="LocalDate"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in LocalDate value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="Period"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="Period"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in Period value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="sbyte"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="sbyte"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, sbyte value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="byte"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="byte"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, byte value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="int"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="int"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, int value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="uint"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="uint"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, uint value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="long"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="long"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, long value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="ulong"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="ulong"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, ulong value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="short"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="short"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, short value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="ushort"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="ushort"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, ushort value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="float"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="float"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, float value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="double"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="double"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, double value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="decimal"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="decimal"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, decimal value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="BigInteger"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="BigInteger"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, in BigInteger value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="BigNumber"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="BigNumber"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, in BigNumber value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a formatted number value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The number value as a UTF-8 byte span.</param>
        public void AddFormattedNumber(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
        {
            _builder.AddPropertyFormattedNumber(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a raw string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The value as a UTF-8 byte span.</param>
        /// <param name="valueRequiresUnescaping">Whether the value requires unescaping.</param>
        public void AddRawString(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, bool valueRequiresUnescaping)
        {
            _builder.AddPropertyRawString(
                propertyName,
                value,
                valueRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a complex value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">A delegate that builds the property value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, Build value)
        {
            _builder.AddProperty(
                propertyName,
                (ref valueBuilder) => BuildValue(value, ref valueBuilder));
        }

        /// <summary>
        /// Adds a property with a JSON array value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">A delegate that builds the array value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, ArrayBuilder.Build value)
        {
            _builder.AddProperty(
                propertyName,
                (ref valueBuilder) => ArrayBuilder.BuildValue(value, ref valueBuilder));
        }

        /// <summary>
        /// Adds a property with a JSON array value to the current object using a builder delegate.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">A delegate that builds the array value.</param>
        public void AddProperty<TContext>(ReadOnlySpan<char> propertyName, in TContext context, ArrayBuilder.Build<TContext> value)
#if NET9_0_OR_GREATER
            where TContext : allows ref struct
#endif
        {
            _builder.AddProperty(
                propertyName,
                BuildWithContext.Create(context, value),
                static (in context, ref valueBuilder) => ArrayBuilder.BuildValue(context.Context, context.Build, ref valueBuilder));
        }

        /// <summary>
        /// Adds a property with a string value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="utf8String">The property value as a UTF-8 byte span.</param>
        /// <param name="escapeValue">Whether to escape the property value.</param>
        /// <param name="valueRequiresUnescaping">Whether the property value requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8String, bool escapeValue, bool valueRequiresUnescaping)
        {
            _builder.AddProperty(
                propertyName,
                utf8String,
                escapeValue,
                valueRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a null value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        public void AddPropertyNull(ReadOnlySpan<char> propertyName)
        {
            _builder.AddPropertyNull(propertyName);
        }

        /// <summary>
        /// Adds a property with a boolean value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The boolean value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, bool value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a JSON element value to the current object.
        /// </summary>
        /// <typeparam name="T">The type of the JSON element value.</typeparam>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The JSON element value.</param>
        [CLSCompliant(false)]
        public void AddProperty<T>(ReadOnlySpan<char> propertyName, T value)
            where T : struct, IJsonElement<T>
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a GUID value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The GUID value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, Guid value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="DateTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="DateTime"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in DateTime value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="DateTimeOffset"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="DateTimeOffset"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in DateTimeOffset value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetDateTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="OffsetDateTime"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetDateTime value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetDate"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="OffsetDate"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetDate value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with an <see cref="OffsetTime"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="OffsetTime"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in OffsetTime value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="LocalDate"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="LocalDate"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in LocalDate value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="Period"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="Period"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in Period value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with an <see cref="sbyte"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="sbyte"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, sbyte value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="byte"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="byte"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, byte value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with an <see cref="int"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="int"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, int value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="uint"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="uint"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, uint value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="long"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="long"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, long value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="ulong"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="ulong"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, ulong value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="short"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="short"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, short value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="ushort"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="ushort"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, ushort value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="float"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="float"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, float value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="double"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="double"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, double value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="decimal"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="decimal"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, decimal value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="BigInteger"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="BigInteger"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, in BigInteger value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="BigNumber"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="BigNumber"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, in BigNumber value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

#if NET

        /// <summary>
        /// Adds a property with an <see cref="Int128"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="Int128"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, Int128 value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="UInt128"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="UInt128"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<byte> propertyName, UInt128 value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with a <see cref="Half"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="value">The <see cref="Half"/> value.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddProperty(ReadOnlySpan<byte> propertyName, Half value, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddProperty(
                propertyName,
                value,
                escapeName,
                nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds a property with an <see cref="Int128"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="Int128"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, Int128 value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="UInt128"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="UInt128"/> value.</param>
        [CLSCompliant(false)]
        public void AddProperty(ReadOnlySpan<char> propertyName, UInt128 value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

        /// <summary>
        /// Adds a property with a <see cref="Half"/> value to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="value">The <see cref="Half"/> value.</param>
        public void AddProperty(ReadOnlySpan<char> propertyName, Half value)
        {
            _builder.AddProperty(
                propertyName,
                value);
        }

#endif

        /// <summary>
        /// Adds an array property with <see cref="long"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="long"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<long> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="int"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="int"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<int> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="short"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="short"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<short> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="sbyte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="sbyte"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<sbyte> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="ulong"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="ulong"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<ulong> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="uint"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="uint"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<uint> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="ushort"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="ushort"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<ushort> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="byte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="byte"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<byte> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="decimal"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="decimal"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<decimal> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="double"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="double"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<double> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="float"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="float"/> values.</param>
        public void AddArrayValue(string propertyName, ReadOnlySpan<float> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

#if NET

        /// <summary>
        /// Adds an array property with <see cref="Int128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="Int128"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<Int128> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="UInt128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="UInt128"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<UInt128> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="Half"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        /// <param name="array">The array of <see cref="Half"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(string propertyName, ReadOnlySpan<Half> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

#endif

        /// <summary>
        /// Adds an array property with <see cref="long"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="long"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<long> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="int"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="int"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<int> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="short"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="short"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<short> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="sbyte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="sbyte"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<sbyte> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="ulong"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="ulong"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<ulong> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="uint"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="uint"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<uint> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="ushort"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="ushort"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<ushort> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="byte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="byte"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="decimal"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="decimal"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<decimal> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="double"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="double"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<double> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="float"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="float"/> values.</param>
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<float> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

#if NET

        /// <summary>
        /// Adds an array property with <see cref="Int128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="Int128"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<Int128> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="UInt128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="UInt128"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<UInt128> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

        /// <summary>
        /// Adds an array property with <see cref="Half"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a character span.</param>
        /// <param name="array">The array of <see cref="Half"/> values.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<Half> array)
        {
            _builder.AddPropertyArrayValue(propertyName, array);
        }

#endif

        /// <summary>
        /// Adds an array property with <see cref="long"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="long"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<long> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="int"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="int"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<int> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="short"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="short"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<short> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="sbyte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="sbyte"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<sbyte> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="ulong"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="ulong"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<ulong> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="uint"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="uint"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<uint> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="ushort"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="ushort"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<ushort> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="byte"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="byte"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="decimal"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="decimal"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<decimal> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="double"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="double"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<double> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="float"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="float"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<float> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Remove the given property from the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        public void RemoveProperty(ReadOnlySpan<byte> propertyName, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.RemoveProperty(propertyName, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Remove the given property from the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-16 char span.</param>
        public void RemoveProperty(ReadOnlySpan<char> propertyName)
        {
            _builder.RemoveProperty(propertyName);
        }

        /// <summary>
        /// Remove the given property from the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a string.</param>
        public void RemoveProperty(string propertyName)
        {
            _builder.RemoveProperty(propertyName);
        }

        /// <summary>
        /// Tries to apply an object instance value to the document.
        /// </summary>
        /// <typeparam name="TApplicator">The type of the <paramref name="value"/>.</typeparam>
        /// <param name="value">The value to apply.</param>
        /// <returns><see langword="true"/> if the value was applied.</returns>
        /// <remarks>
        /// <para>
        /// If the value is a JSON object, its properties (if any) will be set on the current document,
        /// replacing any existing values if present, and the method returns <see langword="true"/>.
        /// </para>
        /// <para>
        /// Otherwise, no changes are made, and the method returns <see langword="false"/>.
        /// </para>
        /// </remarks>
        [CLSCompliant(false)]
        public bool TryApply<TApplicator>(in TApplicator value)
            where TApplicator : struct, IJsonElement<TApplicator>
        {
            return _builder.TryApply(value);
        }

#if NET

        /// <summary>
        /// Adds an array property with <see cref="Int128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="Int128"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<Int128> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="UInt128"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="UInt128"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<UInt128> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

        /// <summary>
        /// Adds an array property with <see cref="Half"/> values to the current object.
        /// </summary>
        /// <param name="propertyName">The property name as a UTF-8 byte span.</param>
        /// <param name="array">The array of <see cref="Half"/> values.</param>
        /// <param name="escapeName">Whether to escape the property name.</param>
        /// <param name="nameRequiresUnescaping">Whether the property name requires unescaping.</param>
        [CLSCompliant(false)]
        public void AddArrayValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<Half> array, bool escapeName = true, bool nameRequiresUnescaping = false)
        {
            Debug.Assert((escapeName && !nameRequiresUnescaping) || (!escapeName));
            _builder.AddPropertyArrayValue(propertyName, array, escapeName, nameRequiresUnescaping);
        }

#endif
    }
}