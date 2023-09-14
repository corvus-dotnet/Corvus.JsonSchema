// <copyright file="JsonAny.Core.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonAny>))]
public readonly partial struct JsonAny
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly string stringBacking;
    private readonly bool boolBacking;
    private readonly double numberBacking;
    private readonly ImmutableList<JsonAny> arrayBacking;
    private readonly ImmutableDictionary<JsonPropertyName, JsonAny> objectBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    public JsonAny()
    {
        this.jsonElementBacking = default;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(in JsonElement value)
    {
        this.jsonElementBacking = value;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableDictionary<JsonPropertyName, JsonAny>.Empty;
    }

    /// <summary>
    /// Gets a Null instance.
    /// </summary>
    public static JsonAny Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonAny Undefined { get; }

    /// <summary>
    /// Gets a default instance.
    /// </summary>
    public static JsonAny DefaultInstance { get; }

    /// <inheritdoc/>
    public JsonAny AsAny => this;

    /// <inheritdoc/>
    public JsonElement AsJsonElement
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking;
            }

            if ((this.backing & Backing.Array) != 0)
            {
                return JsonValueHelpers.ArrayToJsonElement(this.arrayBacking);
            }

            if ((this.backing & Backing.Bool) != 0)
            {
                return JsonValueHelpers.BoolToJsonElement(this.boolBacking);
            }

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueHelpers.NumberToJsonElement(this.numberBacking);
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueHelpers.NullElement;
            }

            if ((this.backing & Backing.Object) != 0)
            {
                return JsonValueHelpers.ObjectToJsonElement(this.objectBacking);
            }

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueHelpers.StringToJsonElement(this.stringBacking);
            }

            return default;
        }
    }

    /// <inheritdoc/>
    public JsonString AsString
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.String) != 0)
            {
                return new(this.stringBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public JsonBoolean AsBoolean
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.Bool) != 0)
            {
                return new(this.boolBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public JsonNumber AsNumber
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.Number) != 0)
            {
                return new(this.numberBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public JsonObject AsObject
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.Object) != 0)
            {
                return new(this.objectBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public JsonArray AsArray
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                return new(this.arrayBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    public bool HasJsonElementBacking => (this.backing & Backing.JsonElement) != 0;

    /// <inheritdoc/>
    public bool HasDotnetBacking => (this.backing & Backing.Dotnet) != 0;

    /// <inheritdoc/>
    public JsonValueKind ValueKind
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking.ValueKind;
            }

            if ((this.backing & Backing.Array) != 0)
            {
                return JsonValueKind.Array;
            }

            if ((this.backing & Backing.Bool) != 0)
            {
                return this.boolBacking ? JsonValueKind.True : JsonValueKind.False;
            }

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueKind.Number;
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueKind.Null;
            }

            if ((this.backing & Backing.Object) != 0)
            {
                return JsonValueKind.Object;
            }

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueKind.String;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator ==(in JsonAny left, in JsonAny right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator !=(in JsonAny left, in JsonAny right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a JsonAny value.
    /// </summary>
    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/>.</returns>
    /// <remarks>The returned value will have a <see cref="IJsonValue.ValueKind"/> of <see cref="JsonValueKind.Undefined"/> if the
    /// value cannot be constructed from the given instance (e.g. because they have an incompatible dotnet backing type.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromAny(in JsonAny value)
    {
        return value;
    }

    /// <summary>
    /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
    /// </summary>
    /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromJson(in JsonElement value)
    {
        return new(value);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a string value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromString<TValue>(in TValue value)
        where TValue : struct, IJsonString<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return new((string)value);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from a boolean value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromBoolean<TValue>(in TValue value)
        where TValue : struct, IJsonBoolean<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return new(true);
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return new(false);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from a double value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromNumber<TValue>(in TValue value)
        where TValue : struct, IJsonNumber<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return new((double)value);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from an array value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromArray<TValue>(in TValue value)
        where TValue : struct, IJsonArray<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return new((ImmutableList<JsonAny>)value);
        }

        return Undefined;
    }

    /// <summary>
    /// Gets an instance of the JSON value from an object value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromObject<TValue>(in TValue value)
        where TValue : struct, IJsonObject<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            return new((ImmutableDictionary<JsonPropertyName, JsonAny>)value);
        }

        return Undefined;
    }

    /// <summary>
    /// Create a <see cref="JsonAny"/> instance from an arbitrary object.
    /// </summary>
    /// <typeparam name="T">The type of the object from which to create the instance.</typeparam>
    /// <param name="instance">The object from which to create the instance.</param>
    /// <param name="options">The (optional) <see cref="JsonWriterOptions"/>.</param>
    /// <returns>A <see cref="JsonAny"/> derived from serializing the object.</returns>
    public static JsonAny From<T>(T instance, JsonWriterOptions options = default)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw, options);
        JsonSerializer.Serialize(writer, instance, typeof(T));
        writer.Flush();
        return Parse(abw.WrittenMemory);
    }

    /// <summary>
    /// Parses a naked value from a URI string.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
    /// <remarks>Note that this only applies to <c>null</c>, <c>bool</c>, <c>number</c> and <c>string</c> types.</remarks>
    public static JsonAny ParseUriValue(string value)
    {
        if (value == "null")
        {
            return JsonAny.Null;
        }

        if (bool.TryParse(value, out bool boolResult))
        {
            return new(boolResult);
        }

        if (double.TryParse(value, out double numberResult))
        {
            return new(numberResult);
        }

        return new(value);
    }

    /// <summary>
    /// Parses a naked value from a URI string.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance representing the value.</returns>
    /// <remarks>Note that this only applies to <c>null</c>, <c>bool</c>, <c>number</c> and <c>string</c> types.</remarks>
    public static JsonAny ParseUriValue(ReadOnlyMemory<char> value, JsonDocumentOptions options = default)
    {
        ReadOnlySpan<char> valueSpan = value.Span;
        if (valueSpan.SequenceEqual("null"))
        {
            return JsonAny.Null;
        }

        if (bool.TryParse(valueSpan, out bool boolResult))
        {
            return new(boolResult);
        }

        if (double.TryParse(valueSpan, out double numberResult))
        {
            return new(numberResult);
        }

        return new(valueSpan);
    }

    /// <summary>
    /// Parses a JSON string into a JsonAny.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
    public static JsonAny Parse(string json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
    public static JsonAny Parse(Stream utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
    public static JsonAny Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonAny.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
    public static JsonAny Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonAny"/> instance built from the JSON string.</returns>
    public static JsonAny Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonAny(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonAny ParseValue(ReadOnlySpan<char> buffer)
    {
        return IJsonValue<JsonAny>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonAny ParseValue(ReadOnlySpan<byte> buffer)
    {
        return IJsonValue<JsonAny>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonAny ParseValue(ref Utf8JsonReader reader)
    {
        return IJsonValue<JsonAny>.ParseValue(ref reader);
    }

    /// <summary>
    /// Gets the value as the target value.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target.</typeparam>
    /// <returns>An instance of the target type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TTarget As<TTarget>()
        where TTarget : struct, IJsonValue<TTarget>
    {
        if (this.HasJsonElementBacking)
        {
            return TTarget.FromJson(this.AsJsonElement);
        }

        if (this.HasDotnetBacking)
        {
            return TTarget.FromAny(this.AsAny);
        }

        return TTarget.Undefined;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return
            (obj is IJsonValue jv && this.Equals(jv.As<JsonAny>())) ||
            (obj is null && this.IsNull());
    }

    /// <inheritdoc/>
    public bool Equals<T>(T other)
        where T : struct, IJsonValue<T>
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <inheritdoc/>
    public bool Equals(JsonAny other)
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <inheritdoc/>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Undefined)
            {
                this.jsonElementBacking.WriteTo(writer);
            }

            return;
        }

        if ((this.backing & Backing.Array) != 0)
        {
            JsonValueHelpers.WriteItems(this.arrayBacking, writer);
            return;
        }

        if ((this.backing & Backing.Bool) != 0)
        {
            writer.WriteBooleanValue(this.boolBacking);
            return;
        }

        if ((this.backing & Backing.Number) != 0)
        {
            writer.WriteNumberValue(this.numberBacking);
            return;
        }

        if ((this.backing & Backing.Null) != 0)
        {
            writer.WriteNullValue();
            return;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            JsonValueHelpers.WriteProperties(this.objectBacking, writer);
            return;
        }

        if ((this.backing & Backing.String) != 0)
        {
            writer.WriteStringValue(this.stringBacking);
            return;
        }
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return JsonValueHelpers.GetHashCode(this);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Serialize();
    }
}