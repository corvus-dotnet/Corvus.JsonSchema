// <copyright file="JsonNotAny.Core.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value, validating false.
/// </summary>
public readonly partial struct JsonNotAny
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly string stringBacking;
    private readonly bool boolBacking;
    private readonly double numberBacking;
    private readonly ImmutableList<JsonAny> arrayBacking;
    private readonly ImmutableDictionary<JsonPropertyName, JsonAny> objectBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
    /// </summary>
    public JsonNotAny()
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
    /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNotAny(in JsonElement value)
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
    public static JsonNotAny Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonNotAny Undefined { get; } = default;

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
    /// Conversion to JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(JsonNotAny value)
    {
        if ((value.backing & Backing.JsonElement) != 0)
        {
            return new(value.jsonElementBacking);
        }

        if ((value.backing & Backing.Object) != 0)
        {
            return new(value.objectBacking);
        }

        if ((value.backing & Backing.Array) != 0)
        {
            return new(value.arrayBacking);
        }

        if ((value.backing & Backing.String) != 0)
        {
            return new(value.stringBacking);
        }

        if ((value.backing & Backing.Number) != 0)
        {
            return new(value.numberBacking);
        }

        if ((value.backing & Backing.Bool) != 0)
        {
            return new(value.boolBacking);
        }

        if ((value.backing & Backing.Null) != 0)
        {
            return JsonAny.Null;
        }

        return JsonAny.Undefined;
    }

    /// <summary>s
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonAny value)
    {
        return FromAny(value);
    }

    /// <summary>
    /// Conversion to JsonArray.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonArray(JsonNotAny value)
    {
        return value.AsArray;
    }

    /// <summary>
    /// Conversion from JsonAny.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonArray value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion to JsonBoolean.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonBoolean(JsonNotAny value)
    {
        return value.AsBoolean;
    }

    /// <summary>
    /// Conversion from JsonBoolean.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonBoolean value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion to JsonObject.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonObject(JsonNotAny value)
    {
        return value.AsObject;
    }

    /// <summary>
    /// Conversion from JsonObject.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonObject value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion to JsonString.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonString(JsonNotAny value)
    {
        return value.AsString;
    }

    /// <summary>
    /// Conversion from JsonString.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonString value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Conversion to JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNumber(JsonNotAny value)
    {
        return value.AsNumber;
    }

    /// <summary>
    /// Conversion from JsonNumber.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(JsonNumber value)
    {
        return value.AsAny;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator ==(in JsonNotAny left, in JsonNotAny right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator !=(in JsonNotAny left, in JsonNotAny right)
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
    public static JsonNotAny FromAny(in JsonAny value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => new(value.AsImmutableDictionary()),
            JsonValueKind.Array => new(value.AsImmutableList()),
            JsonValueKind.String => new((string)value),
            JsonValueKind.Number => new((double)value),
            JsonValueKind.True => new(true),
            JsonValueKind.False => new(false),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
    /// </summary>
    /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonNotAny FromJson(in JsonElement value)
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
    public static JsonNotAny FromString<TValue>(in TValue value)
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
    public static JsonNotAny FromBoolean<TValue>(in TValue value)
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
    public static JsonNotAny FromNumber<TValue>(in TValue value)
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
    public static JsonNotAny FromArray<TValue>(in TValue value)
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
    public static JsonNotAny FromObject<TValue>(in TValue value)
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
    /// Create a <see cref="JsonNotAny"/> instance from an arbitrary object.
    /// </summary>
    /// <typeparam name="T">The type of the object from which to create the instance.</typeparam>
    /// <param name="instance">The object from which to create the instance.</param>
    /// <param name="options">The (optional) <see cref="JsonWriterOptions"/>.</param>
    /// <returns>A <see cref="JsonNotAny"/> derived from serializing the object.</returns>
    public static JsonNotAny From<T>(T instance, JsonWriterOptions options = default)
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
    public static JsonNotAny ParseUriValue(string value)
    {
        if (value == "null")
        {
            return JsonNotAny.Null;
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
    public static JsonNotAny ParseUriValue(ReadOnlyMemory<char> value, JsonDocumentOptions options = default)
    {
        ReadOnlySpan<char> valueSpan = value.Span;
        if (valueSpan.SequenceEqual("null"))
        {
            return JsonNotAny.Null;
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
    /// Parses a JSON string into a JsonNotAny.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNotAny"/> instance built from the JSON string.</returns>
    public static JsonNotAny Parse(string json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNotAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNotAny"/> instance built from the JSON string.</returns>
    public static JsonNotAny Parse(Stream utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNotAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNotAny"/> instance built from the JSON string.</returns>
    public static JsonNotAny Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNotAny.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNotAny"/> instance built from the JSON string.</returns>
    public static JsonNotAny Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNotAny.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNotAny"/> instance built from the JSON string.</returns>
    public static JsonNotAny Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonNotAny(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNotAny ParseValue(ReadOnlySpan<char> buffer)
    {
        return IJsonValue<JsonNotAny>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNotAny ParseValue(ReadOnlySpan<byte> buffer)
    {
        return IJsonValue<JsonNotAny>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNotAny ParseValue(ref Utf8JsonReader reader)
    {
        return IJsonValue<JsonNotAny>.ParseValue(ref reader);
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
            (obj is IJsonValue jv && this.Equals(jv.As<JsonNotAny>())) ||
            (obj is null && this.IsNull());
    }

    /// <inheritdoc/>
    public bool Equals<T>(T other)
        where T : struct, IJsonValue<T>
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <inheritdoc/>
    public bool Equals(JsonNotAny other)
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