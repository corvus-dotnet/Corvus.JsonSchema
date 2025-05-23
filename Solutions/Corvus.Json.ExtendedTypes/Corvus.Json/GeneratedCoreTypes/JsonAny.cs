//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

#nullable enable

using System.Buffers;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<JsonAny>))]
public readonly partial struct JsonAny
    : IJsonValue<Corvus.Json.JsonAny>
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly string stringBacking;
    private readonly bool boolBacking;
    private readonly BinaryJsonNumber numberBacking;
    private readonly ImmutableList<JsonAny> arrayBacking;
    private readonly ImmutableList<JsonObjectProperty> objectBacking;

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
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
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
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(ImmutableList<JsonAny> value)
    {
        this.backing = Backing.Array;
        this.jsonElementBacking = default;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = value;
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(bool value)
    {
        this.backing = Backing.Bool;
        this.jsonElementBacking = default;
        this.stringBacking = string.Empty;
        this.boolBacking = value;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(BinaryJsonNumber value)
    {
        this.backing = Backing.Number;
        this.jsonElementBacking = default;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = value;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(ImmutableList<JsonObjectProperty> value)
    {
        this.backing = Backing.Object;
        this.jsonElementBacking = default;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonAny(string value)
    {
        this.backing = Backing.String;
        this.jsonElementBacking = default;
        this.stringBacking = value;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = ImmutableList<JsonObjectProperty>.Empty;
    }

    /// <summary>
    /// Gets the schema location from which this type was generated.
    /// </summary>
    public static string SchemaLocation { get; } = "#/$defs/JsonAny";

    /// <summary>
    /// Gets a Null instance.
    /// </summary>
    public static JsonAny Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonAny Undefined { get; }

    /// <summary>
    /// Gets the default instance.
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

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueHelpers.StringToJsonElement(this.stringBacking);
            }

            if ((this.backing & Backing.Bool) != 0)
            {
                return JsonValueHelpers.BoolToJsonElement(this.boolBacking);
            }

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueHelpers.NumberToJsonElement(this.numberBacking);
            }

            if ((this.backing & Backing.Array) != 0)
            {
                return JsonValueHelpers.ArrayToJsonElement(this.arrayBacking);
            }

            if ((this.backing & Backing.Object) != 0)
            {
                return JsonValueHelpers.ObjectToJsonElement(this.objectBacking);
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueHelpers.NullElement;
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
    public bool HasJsonElementBacking
    {
        get
        {
            return (this.backing & Backing.JsonElement) != 0;
        }
    }

    /// <inheritdoc/>
    public bool HasDotnetBacking
    {
        get
        {
            return (this.backing & Backing.Dotnet) != 0;
        }
    }

    /// <inheritdoc/>
    public JsonValueKind ValueKind
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking.ValueKind;
            }

            if ((this.backing & Backing.String) != 0)
            {
                return JsonValueKind.String;
            }

            if ((this.backing & Backing.Bool) != 0)
            {
                return this.boolBacking ? JsonValueKind.True : JsonValueKind.False;
            }

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueKind.Number;
            }

            if ((this.backing & Backing.Array) != 0)
            {
                return JsonValueKind.Array;
            }

            if ((this.backing & Backing.Object) != 0)
            {
                return JsonValueKind.Object;
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueKind.Null;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <summary>
    /// Conversion from byte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(byte value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from decimal.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(decimal value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from double.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(double value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from short.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(short value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from int.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(int value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from long.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(long value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Int128.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(Int128 value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <summary>
    /// Conversion from sbyte.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(sbyte value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from Half.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(Half value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <summary>
    /// Conversion from float.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(float value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from ushort.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(ushort value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from uint.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(uint value)
    {
        return new(new BinaryJsonNumber(value));
    }

    /// <summary>
    /// Conversion from ulong.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(ulong value)
    {
        return new(new BinaryJsonNumber(value));
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from UInt128.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    /// <returns>An instance of the <see cref="JsonAny"/>.</returns>
    public static implicit operator JsonAny(UInt128 value)
    {
        return new(new BinaryJsonNumber(value));
    }
#endif

    /// <summary>
    /// Conversion from <see cref="bool"/>.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(bool value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion from <see cref="string"/>.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonAny(string value)
    {
        return new(value);
    }

    /// <summary>
    /// Operator ==.
    /// </summary>
    /// <param name="left">The lhs of the operator.</param>
    /// <param name="right">The rhs of the operator.</param>
    /// <returns>
    /// <c>True</c> if the values are equal.
    /// </returns>
    public static bool operator ==(in JsonAny left, in JsonAny right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Operator !=.
    /// </summary>
    /// <param name="left">The lhs of the operator.</param>
    /// <param name="right">The rhs of the operator.</param>
    /// <returns>
    /// <c>True</c> if the values are not equal.
    /// </returns>
    public static bool operator !=(in JsonAny left, in JsonAny right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
    /// </summary>
    /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
    /// <remarks>The returned value will have a <see cref = "IJsonValue.ValueKind"/> of <see cref = "JsonValueKind.Undefined"/> if the
    /// value cannot be constructed from the given instance (e.g. because they have an incompatible .NET backing type).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromJson(in JsonElement value)
    {
        return new(value);
    }

    /// <summary>
    /// Create a <see cref="JsonAny"/> instance from an arbitrary object.
    /// </summary>
    /// <typeparam name="T">The type of the object from which to create the instance.</typeparam>
    /// <param name="instance">The object from which to create the instance.</param>
    /// <param name="options">The (optional) <see cref="JsonWriterOptions"/>.</param>
    /// <returns>A <see cref="JsonAny"/> derived from serializing the object.</returns>
    public static JsonAny CreateFromSerializedInstance<T>(T instance, JsonWriterOptions options = default)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw, options);
        JsonSerializer.Serialize(writer, instance, typeof(T));
        writer.Flush();
        return Parse(abw.WrittenMemory);
    }

    /// <summary>
    /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
    /// </summary>
    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromAny(in JsonAny value) => value;

    /// <summary>
    /// Gets an instance of the JSON value from the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromBoolean<TValue>(in TValue value)
        where TValue : struct, IJsonBoolean<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => new(true),
            JsonValueKind.False => new(false),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromString<TValue>(in TValue value)
        where TValue : struct, IJsonString<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => new(value.GetString()!),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromNumber<TValue>(in TValue value)
        where TValue : struct, IJsonNumber<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => new(value.AsBinaryJsonNumber),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromObject<TValue>(in TValue value)
        where TValue : struct, IJsonObject<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Object => new(value.AsPropertyBacking()),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Gets an instance of the JSON value from the provided value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the provided value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JsonAny FromArray<TValue>(in TValue value)
        where TValue : struct, IJsonArray<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return value.ValueKind switch
        {
            JsonValueKind.Array => new(value.AsImmutableList()),
            JsonValueKind.Null => Null,
            _ => Undefined,
        };
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    public static JsonAny Parse(string source, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(source, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    public static JsonAny Parse(Stream source, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(source, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    public static JsonAny Parse(ReadOnlyMemory<byte> source, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(source, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    public static JsonAny Parse(ReadOnlyMemory<char> source, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(source, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    public static JsonAny Parse(ReadOnlySequence<byte> source, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(source, options);
        return new(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    public static JsonAny ParseValue(string source)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonAny>.ParseValue(source);
#else
        return JsonValueHelpers.ParseValue<JsonAny>(source.AsSpan());
#endif
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    public static JsonAny ParseValue(ReadOnlySpan<char> source)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonAny>.ParseValue(source);
#else
        return JsonValueHelpers.ParseValue<JsonAny>(source);
#endif
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    public static JsonAny ParseValue(ReadOnlySpan<byte> source)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonAny>.ParseValue(source);
#else
        return JsonValueHelpers.ParseValue<JsonAny>(source);
#endif
    }

    /// <summary>
    /// Parses the JsonAny.
    /// </summary>
    /// <param name="source">The source of the JSON string to parse.</param>
    public static JsonAny ParseValue(ref Utf8JsonReader source)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonAny>.ParseValue(ref source);
#else
        return JsonValueHelpers.ParseValue<JsonAny>(ref source);
#endif
    }

    /// <summary>
    /// Gets the value as an instance of the target value.
    /// </summary>
    /// <typeparam name="TTarget">The type of the target.</typeparam>
    /// <returns>An instance of the target type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TTarget As<TTarget>()
        where TTarget : struct, IJsonValue<TTarget>
    {
#if NET8_0_OR_GREATER
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return TTarget.FromJson(this.jsonElementBacking);
        }

        if ((this.backing & Backing.String) != 0)
        {
            return TTarget.FromString(this.AsString);
        }

        if ((this.backing & Backing.Bool) != 0)
        {
            return TTarget.FromBoolean(this.AsBoolean);
        }

        if ((this.backing & Backing.Number) != 0)
        {
            return TTarget.FromNumber(this.AsNumber);
        }

        if ((this.backing & Backing.Array) != 0)
        {
            return TTarget.FromArray(this.AsArray);
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return TTarget.FromObject(this.AsObject);
        }

        if ((this.backing & Backing.Null) != 0)
        {
            return TTarget.Null;
        }

        return TTarget.Undefined;
#else
        return this.As<JsonAny, TTarget>();
#endif
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return
            (obj is IJsonValue jv && this.Equals(jv.As<JsonAny>())) ||
            (obj is null && this.IsNull());
    }

    /// <inheritdoc/>
    public bool Equals<T>(in T other)
        where T : struct, IJsonValue<T>
    {
        return this.Equals(other.As<JsonAny>());
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other item with which to compare.</param>
    /// <returns><see langword="true"/> if the values were equal.</returns>
    public bool Equals(in JsonAny other)
    {
        JsonValueKind thisKind = this.ValueKind;
        JsonValueKind otherKind = other.ValueKind;
        if (thisKind != otherKind)
        {
            return false;
        }

        if (thisKind == JsonValueKind.Null || thisKind == JsonValueKind.Undefined)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Array)
        {
            JsonArray thisArray = this.AsArray;
            JsonArray otherArray = other.AsArray;
            JsonArrayEnumerator lhs = thisArray.EnumerateArray();
            JsonArrayEnumerator rhs = otherArray.EnumerateArray();
            while (lhs.MoveNext())
            {
                if (!rhs.MoveNext())
                {
                    return false;
                }

                if (!lhs.Current.Equals(rhs.Current))
                {
                    return false;
                }
            }

            return !rhs.MoveNext();
        }

        if (thisKind == JsonValueKind.True || thisKind == JsonValueKind.False)
        {
            return true;
        }

        if (thisKind == JsonValueKind.Number)
        {
            if (this.backing == Backing.Number && other.backing == Backing.Number)
            {
                return BinaryJsonNumber.Equals(this.numberBacking, other.numberBacking);
            }

            if (this.backing == Backing.Number && other.backing == Backing.JsonElement)
            {
                return BinaryJsonNumber.Equals(this.numberBacking, other.jsonElementBacking);
            }

            if (this.backing == Backing.JsonElement && other.backing == Backing.Number)
            {
                return BinaryJsonNumber.Equals(this.jsonElementBacking, other.numberBacking);
            }

            if (this.jsonElementBacking.TryGetDouble(out double lDouble))
            {
                if (other.jsonElementBacking.TryGetDouble(out double rDouble))
                {
                    return lDouble.Equals(rDouble);
                }
            }

            if (this.jsonElementBacking.TryGetDecimal(out decimal lDecimal))
            {
                if (other.jsonElementBacking.TryGetDecimal(out decimal rDecimal))
                {
                    return lDecimal.Equals(rDecimal);
                }
            }
        }

        if (thisKind == JsonValueKind.Object)
        {
            JsonObject thisObject = this.AsObject;
            JsonObject otherObject = other.AsObject;
            int count = 0;
            foreach (JsonObjectProperty property in thisObject.EnumerateObject())
            {
                if (!otherObject.TryGetProperty(property.Name, out JsonAny value) || !property.Value.Equals(value))
                {
                    return false;
                }

                count++;
            }

            int otherCount = 0;
            foreach (JsonObjectProperty otherProperty in otherObject.EnumerateObject())
            {
                otherCount++;
                if (otherCount > count)
                {
                    return false;
                }
            }

            return count == otherCount;
        }

        if (thisKind == JsonValueKind.String)
        {
            if (this.backing == Backing.JsonElement)
            {
                if (other.backing == Backing.String)
                {
                    return this.jsonElementBacking.ValueEquals(other.stringBacking);
                }
                else
                {
                    other.jsonElementBacking.TryGetValue(CompareValues, this.jsonElementBacking, out bool areEqual);
                    return areEqual;
                }

            }

            if (other.backing == Backing.JsonElement)
            {
                return other.jsonElementBacking.ValueEquals(this.stringBacking);
            }

            return this.stringBacking.Equals(other.stringBacking);

            static bool CompareValues(ReadOnlySpan<byte> span, in JsonElement firstItem, out bool value)
            {
                value = firstItem.ValueEquals(span);
                return true;
            }
        }

        return false;
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
            this.numberBacking.WriteTo(writer);

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

        if ((this.backing & Backing.Null) != 0)
        {
            writer.WriteNullValue();

            return;
        }
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return this.ValueKind switch
        {
            JsonValueKind.Array => JsonValueHelpers.GetArrayHashCode(((IJsonValue)this).AsArray),
            JsonValueKind.Object => JsonValueHelpers.GetObjectHashCode(((IJsonValue)this).AsObject),
            JsonValueKind.Number => JsonValueHelpers.GetHashCodeForNumber(((IJsonValue)this).AsNumber),
            JsonValueKind.String => JsonValueHelpers.GetHashCodeForString(((IJsonValue)this).AsString),
            JsonValueKind.True => true.GetHashCode(),
            JsonValueKind.False => false.GetHashCode(),
            JsonValueKind.Null => JsonValueHelpers.NullHashCode,
            _ => JsonValueHelpers.UndefinedHashCode,
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return this.Serialize();
    }

    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext context, ValidationLevel validationLevel = ValidationLevel.Flag) => context;
}
