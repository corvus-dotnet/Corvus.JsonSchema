// <copyright file="JsonNumber.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents a JSON number.
/// </summary>
public readonly partial struct JsonNumber :
    IJsonNumber<JsonNumber>,
    ISpanFormattable,
    IUtf8SpanFormattable
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly BinaryJsonNumber numberBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="numberBacking">The binary number backing the number.</param>
    public JsonNumber(in BinaryJsonNumber numberBacking)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Number;
        this.numberBacking = numberBacking;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNumber"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNumber(in JsonElement value)
    {
        this.jsonElementBacking = value;
        this.backing = Backing.JsonElement;
    }

    /// <summary>
    /// Gets a Null instance.
    /// </summary>
    public static JsonNumber Null { get; } = new(JsonValueHelpers.NullElement);

    /// <summary>
    /// Gets an Undefined instance.
    /// </summary>
    public static JsonNumber Undefined { get; }

    /// <summary>
    /// Gets a default instance.
    /// </summary>
    public static JsonNumber DefaultInstance { get; }

    /// <inheritdoc/>
    public JsonAny AsAny
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

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueHelpers.NumberToJsonElement(this.numberBacking);
            }

            return default;
        }
    }

    /// <inheritdoc/>
    JsonString IJsonValue.AsString
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
    public JsonNumber AsNumber
    {
        get
        {
            return this;
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

            if ((this.backing & Backing.Number) != 0)
            {
                return JsonValueKind.Number;
            }

            return JsonValueKind.Undefined;
        }
    }

    /// <inheritdoc/>
    public BinaryJsonNumber AsBinaryJsonNumber => this.HasDotnetBacking ? this.numberBacking : BinaryJsonNumber.FromJson(this.jsonElementBacking);

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator ==(in JsonNumber left, in JsonNumber right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><c>True</c> if the values are equal.</returns>
    public static bool operator !=(in JsonNumber left, in JsonNumber right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator <(in JsonNumber left, in JsonNumber right)
    {
        return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) < 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator >(in JsonNumber left, in JsonNumber right)
    {
        return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) > 0;
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is less than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator <=(in JsonNumber left, in JsonNumber right)
    {
        return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The LHS of the comparison.</param>
    /// <param name="right">The RHS of the comparison.</param>
    /// <returns><see langword="true"/> if the left is greater than the right, otherwise <see langword="false"/>.</returns>
    public static bool operator >=(in JsonNumber left, in JsonNumber right)
    {
        return left.IsNotNullOrUndefined() && right.IsNotNullOrUndefined() && Compare(left, right) >= 0;
    }

    /// <summary>
    /// Compare with another number.
    /// </summary>
    /// <param name="lhs">The lhs of the comparison.</param>
    /// <param name="rhs">The rhs of the comparison.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    public static int Compare(in JsonNumber lhs, in JsonNumber rhs)
    {
        if (lhs.ValueKind != rhs.ValueKind)
        {
            // We can't be equal if we are not the same underlying type
            return lhs.IsNullOrUndefined() ? 1 : -1;
        }

        if (lhs.IsNull())
        {
            // Nulls are always equal
            return 0;
        }

        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
        }

        // After this point there is no need to check both value kinds because our first quick test verified that they were the same.
        // If either one is a Backing.Number or a JsonValueKind.Number then we know the rhs is conmpatible.
        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.numberBacking);
        }

        if (lhs.backing == Backing.Number &&
            rhs.backing == Backing.JsonElement)
        {
            return BinaryJsonNumber.Compare(lhs.numberBacking, rhs.jsonElementBacking);
        }

        if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.Number)
        {
            return BinaryJsonNumber.Compare(lhs.jsonElementBacking, rhs.numberBacking);
        }

        if (lhs.backing == Backing.JsonElement && rhs.backing == Backing.JsonElement && rhs.jsonElementBacking.ValueKind == JsonValueKind.Number)
        {
            return JsonValueHelpers.NumericCompare(lhs.jsonElementBacking, rhs.jsonElementBacking);
        }

        throw new InvalidOperationException();
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
    public static JsonNumber FromAny(in JsonAny value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        JsonValueKind valueKind = value.ValueKind;
        return valueKind switch
        {
            JsonValueKind.Number => value.AsNumber,
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
    public static JsonNumber FromJson(in JsonElement value)
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
    static JsonNumber IJsonValue<JsonNumber>.FromString<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
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
    static JsonNumber IJsonValue<JsonNumber>.FromBoolean<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
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
    public static JsonNumber FromNumber<TValue>(in TValue value)
        where TValue : struct, IJsonNumber<TValue>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.AsNumber;
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
    static JsonNumber IJsonValue<JsonNumber>.FromArray<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
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
    static JsonNumber IJsonValue<JsonNumber>.FromObject<TValue>(in TValue value)
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return Undefined;
    }

    /// <summary>
    /// Parses a JSON string into a JsonNumber.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNumber"/> instance built from the JSON string.</returns>
    public static JsonNumber Parse(string json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonNumber(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNumber.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNumber"/> instance built from the JSON string.</returns>
    public static JsonNumber Parse(Stream utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonNumber(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNumber.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNumber"/> instance built from the JSON string.</returns>
    public static JsonNumber Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonNumber(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNumber.
    /// </summary>
    /// <param name="json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNumber"/> instance built from the JSON string.</returns>
    public static JsonNumber Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(json, options);
        return new JsonNumber(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON string into a JsonNumber.
    /// </summary>
    /// <param name="utf8Json">The json string to parse.</param>
    /// <param name="options">The (optional) JsonDocumentOptions.</param>
    /// <returns>A <see cref="JsonNumber"/> instance built from the JSON string.</returns>
    public static JsonNumber Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default)
    {
        using var jsonDocument = JsonDocument.Parse(utf8Json, options);
        return new JsonNumber(jsonDocument.RootElement.Clone());
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNumber ParseValue(ReadOnlySpan<char> buffer)
    {
        return IJsonValue<JsonNumber>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNumber ParseValue(ReadOnlySpan<byte> buffer)
    {
        return IJsonValue<JsonNumber>.ParseValue(buffer);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonNumber ParseValue(ref Utf8JsonReader reader)
    {
        return IJsonValue<JsonNumber>.ParseValue(ref reader);
    }

    /// <summary>
    /// Compare this number with another.
    /// </summary>
    /// <typeparam name="TOther">The type of the other Json Number.</typeparam>
    /// <param name="rhs">The json number with which to compare.</param>
    /// <returns>0 if the numbers are equal, -1 if the lhs is less than the rhs, and 1 if the lhs is greater than the rhs.</returns>
    public int CompareTo<TOther>(in TOther rhs)
        where TOther : struct, IJsonNumber<TOther>
    {
        return Compare(this, rhs.AsNumber);
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
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return TTarget.FromJson(this.jsonElementBacking);
        }

        if ((this.backing & Backing.Number) != 0)
        {
            return TTarget.FromNumber(this);
        }

        if ((this.backing & Backing.Null) != 0)
        {
            return TTarget.Null;
        }

        return TTarget.Undefined;
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
    public bool Equals(in JsonNumber other)
    {
        if (this.ValueKind != other.ValueKind)
        {
            // We can't be equal if we are not the same underlying type
            return false;
        }

        if (this.IsNull())
        {
            // Nulls are always equal
            return true;
        }

        if (this.backing == Backing.Number &&
            other.backing == Backing.Number)
        {
            return BinaryJsonNumber.Equals(this.numberBacking, other.numberBacking);
        }

        // After this point there is no need to check both value kinds because our first quick test verified that they were the same.
        // If either one is a Backing.Number or a JsonValueKind.Number then we know the other is conmpatible.
        if (this.backing == Backing.Number &&
            other.backing == Backing.JsonElement)
        {
            return BinaryJsonNumber.Equals(this.numberBacking, other.jsonElementBacking);
        }

        if (this.backing == Backing.JsonElement &&
            other.backing == Backing.Number)
        {
            return BinaryJsonNumber.Equals(this.jsonElementBacking, other.numberBacking);
        }

        if (this.backing == Backing.JsonElement && other.backing == Backing.JsonElement && this.jsonElementBacking.ValueKind == JsonValueKind.Number)
        {
            return JsonValueHelpers.NumericEquals(this.jsonElementBacking, other.jsonElementBacking);
        }

        throw new InvalidOperationException();
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

        if ((this.backing & Backing.Number) != 0)
        {
            this.numberBacking.WriteTo(writer);
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
        return Json.Validate.TypeNumber(this.ValueKind, validationContext, level);
    }

    /// <summary>
    /// Gets the maximum char length for a number of this size.
    /// </summary>
    /// <returns>The maximum possible length of the buffer required if the number is written to a <see cref="Span{T}"/> - either bytes or chars.</returns>
    public int GetMaxCharLength()
    {
        if (this.HasJsonElementBacking)
        {
            // This is the largest possible output size.
            return BinaryJsonNumber.GetMaxCharLength(BinaryJsonNumber.Kind.Double);
        }

        if (this.HasDotnetBacking)
        {
            return this.numberBacking.GetMaxCharLength();
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (this.HasJsonElementBacking)
        {
            // This is the largest possible output size.
            if (this.jsonElementBacking.TryGetDouble(out double v1))
            {
                return v1.TryFormat(destination, out charsWritten, format, provider);
            }

            if (this.jsonElementBacking.TryGetDecimal(out decimal v2))
            {
                return v2.TryFormat(destination, out charsWritten, format, provider);
            }
        }

        if (this.HasDotnetBacking)
        {
            return this.numberBacking.TryFormat(destination, out charsWritten, format, provider);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        if (this.HasJsonElementBacking)
        {
            // This is the largest possible output size.
            if (this.jsonElementBacking.TryGetDouble(out double v1))
            {
                return v1.TryFormat(utf8Destination, out bytesWritten, format, provider);
            }

            if (this.jsonElementBacking.TryGetDecimal(out decimal v2))
            {
                return v2.TryFormat(utf8Destination, out bytesWritten, format, provider);
            }
        }

        if (this.HasDotnetBacking)
        {
            return this.numberBacking.TryFormat(utf8Destination, out bytesWritten, format, provider);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (this.HasJsonElementBacking)
        {
            // This is the largest possible output size.
            if (this.jsonElementBacking.TryGetDouble(out double v1))
            {
                return v1.ToString(format, formatProvider);
            }

            if (this.jsonElementBacking.TryGetDecimal(out decimal v2))
            {
                return v2.ToString(format, formatProvider);
            }
        }

        if (this.HasDotnetBacking)
        {
            return this.numberBacking.ToString(format, formatProvider);
        }

        throw new InvalidOperationException();
    }
}