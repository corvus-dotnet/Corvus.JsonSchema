// <copyright file="JsonBase64String.Core.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON base64string.
/// </summary>
public readonly partial struct JsonBase64String
#if NET8_0_OR_GREATER
    : IJsonString<JsonBase64String>, ISpanFormattable
#else
    : IJsonString<JsonBase64String>
#endif
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly string stringBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
    /// </summary>
    public JsonBase64String()
    {
        this.jsonElementBacking = default;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonBase64String"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonBase64String(in JsonElement value)
    {
        this.jsonElementBacking = value;
        this.backing = Backing.JsonElement;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Gets a Null instance.
    /// </summary>
    public static JsonBase64String Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonBase64String Undefined { get; }

    /// <summary>
    /// Gets a default instance.
    /// </summary>
    public static JsonBase64String DefaultInstance { get; }

    /// <inheritdoc/>
    public JsonAny AsAny
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

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonAny.Null;
            }

            return JsonAny.Undefined;
        }
    }

    /// <inheritdoc/>
    public JsonElement AsJsonElement
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return this.jsonElementBacking;
            }

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueHelpers.NullElement;
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
    JsonBoolean IJsonValue.AsBoolean
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonNumber IJsonValue.AsNumber
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonObject IJsonValue.AsObject
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
            }

            throw new InvalidOperationException();
        }
    }

    /// <inheritdoc/>
    JsonArray IJsonValue.AsArray
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                return new(this.jsonElementBacking);
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

            if ((this.backing & Backing.Null) != 0)
            {
                return JsonValueKind.Null;
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
    public static bool operator ==(in JsonBase64String left, in JsonBase64String right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator !=(in JsonBase64String left, in JsonBase64String right)
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
    public static JsonBase64String FromAny(in JsonAny value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        JsonValueKind valueKind = value.ValueKind;
        return valueKind switch
        {
            JsonValueKind.String => new((string)value.AsString),
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
    public static JsonBase64String FromJson(in JsonElement value)
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
    public static JsonBase64String FromString<TValue>(in TValue value)
        where TValue : struct, IJsonString<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return new(value.GetString()!);
        }

        return Undefined;
    }
#if NET8_0_OR_GREATER

    /// <summary>
    /// Gets an instance of the JSON value from a boolean value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonBase64String IJsonValue<JsonBase64String>.FromBoolean<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }
#endif
#if NET8_0_OR_GREATER

    /// <summary>
    /// Gets an instance of the JSON value from a double value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonBase64String IJsonValue<JsonBase64String>.FromNumber<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }
#endif
#if NET8_0_OR_GREATER

    /// <summary>
    /// Gets an instance of the JSON value from an array value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonBase64String IJsonValue<JsonBase64String>.FromArray<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }
#endif
#if NET8_0_OR_GREATER

    /// <summary>
    /// Gets an instance of the JSON value from an object value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="value">The value from which to instantiate the instance.</param>
    /// <returns>An instance of this type, initialized from the value.</returns>
    /// <remarks>The value will be undefined if it cannot be initialized with the specified instance.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static JsonBase64String IJsonValue<JsonBase64String>.FromObject<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }
#endif

    /// <summary>
    /// Parses a JSON string into a JsonBase64String.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonBase64String"/> instance built from the JSON string.</returns>
    public static JsonBase64String Parse(string json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonBase64String(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonBase64String.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonBase64String"/> instance built from the JSON string.</returns>
    public static JsonBase64String Parse(Stream utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonBase64String(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonBase64String.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonBase64String"/> instance built from the JSON string.</returns>
    public static JsonBase64String Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonBase64String(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonBase64String.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonBase64String"/> instance built from the JSON string.</returns>
    public static JsonBase64String Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonBase64String(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonBase64String.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonBase64String"/> instance built from the JSON string.</returns>
    public static JsonBase64String Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonBase64String(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonBase64String ParseValue(string buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonBase64String>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonBase64String>(buffer.AsSpan());
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonBase64String ParseValue(ReadOnlySpan<char> buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonBase64String>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonBase64String>(buffer);
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonBase64String ParseValue(ReadOnlySpan<byte> buffer)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonBase64String>.ParseValue(buffer);
#else
        return JsonValueHelpers.ParseValue<JsonBase64String>(buffer);
#endif
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonBase64String ParseValue(ref Utf8JsonReader reader)
    {
#if NET8_0_OR_GREATER
        return IJsonValue<JsonBase64String>.ParseValue(ref reader);
#else
        return JsonValueHelpers.ParseValue<JsonBase64String>(ref reader);
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
            return TTarget.FromString(this);
        }

        if ((this.backing & Backing.Null) != 0)
        {
            return TTarget.Null;
        }

        return TTarget.Undefined;
#else
        return this.As<JsonBase64String, TTarget>();
#endif
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return
            (obj is IJsonValue jv && this.Equals(jv.AsAny)) ||
            (obj is null && this.IsNull());
    }

    /// <inheritdoc/>
    public bool Equals<T>(in T other)
        where T : struct, IJsonValue<T>
    {
        return JsonValueHelpers.CompareValues(this, other);
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other item with which to compare.</param>
    /// <returns><see langword="true"/> if the values were equal.</returns>
    public bool Equals(in JsonBase64String other)
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

        if ((this.backing & Backing.Null) != 0)
        {
            writer.WriteNullValue();
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

    /// <inheritdoc/>
    public ValidationContext Validate(in ValidationContext validationContext, ValidationLevel level = ValidationLevel.Flag)
    {
        return Json.Validate.TypeBase64String(this, validationContext, level);
    }

#if NET8_0_OR_GREATER
    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if ((this.backing & Backing.String) != 0)
        {
            int length = Math.Min(destination.Length, this.stringBacking.Length);
            this.stringBacking.AsSpan(0, length).CopyTo(destination);
            charsWritten = length;
            return true;
        }

        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind == JsonValueKind.String)
            {
                char[] buffer = ArrayPool<char>.Shared.Rent(destination.Length);
                try
                {
                    bool result = this.jsonElementBacking.TryGetValue(FormatSpan, new Output(buffer, destination.Length), out charsWritten);
                    if (result)
                    {
                        buffer.AsSpan(0, charsWritten).CopyTo(destination);
                    }

                    return result;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }
            }
            else
            {
                string value = this.jsonElementBacking.GetRawText();
                int length = Math.Min(destination.Length, this.stringBacking.Length);
                this.stringBacking.AsSpan(0, length).CopyTo(destination);
                charsWritten = length;
                return true;
            }
        }

        charsWritten = 0;
        return true;

        static bool FormatSpan(ReadOnlySpan<char> source, in Output output, out int charsWritten)
        {
            int length = Math.Min(output.Length, source.Length);
            source[..length].CopyTo(output.Destination);
            charsWritten = length;
            return true;
        }
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        // There is no formatting for the string
        return this.ToString();
    }

    private readonly record struct Output(char[] Destination, int Length);
#endif
}