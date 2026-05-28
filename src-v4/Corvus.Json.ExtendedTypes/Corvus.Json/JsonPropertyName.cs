// <copyright file="JsonPropertyName.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Corvus.Json;

/// <summary>
/// A JSON property name.
/// </summary>
public readonly struct JsonPropertyName
{
    private readonly Backing backing;
    private readonly JsonElement jsonElementBacking;
    private readonly JsonProperty jsonPropertyBacking;
    private readonly string stringBacking;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPropertyName"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the property name.</param>
    public JsonPropertyName(in JsonElement value)
    {
        this.backing = Backing.JsonElement;
        this.jsonElementBacking = value;
        this.jsonPropertyBacking = default;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPropertyName"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the property name.</param>
    public JsonPropertyName(in JsonProperty value)
    {
        this.backing = Backing.JsonProperty;
        this.jsonElementBacking = default;
        this.jsonPropertyBacking = value;
        this.stringBacking = string.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPropertyName"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the property name.</param>
    public JsonPropertyName(string value)
    {
        this.backing = Backing.String;
        this.jsonElementBacking = default;
        this.jsonPropertyBacking = default;
        this.stringBacking = value;
    }

    [Flags]
    private enum Backing
    {
        Unknown = 0,
        String = 0b0001,
        JsonProperty = 0b0010,
        JsonElement = 0b1000,
    }

    /// <summary>
    /// Gets a value indicating whether this has a string backing.
    /// </summary>
    public bool HasStringBacking => (this.backing & Backing.String) != 0;

    /// <summary>
    /// Gets a value indicating whether this has a <see cref="JsonProperty"/> backing.
    /// </summary>
    public bool HasJsonPropertyBacking => (this.backing & Backing.JsonProperty) != 0;

    /// <summary>
    /// Gets a value indicating whether this has a <see cref="JsonElement"/> backing.
    /// </summary>
    public bool HasJsonElementBacking => (this.backing & Backing.JsonElement) != 0;

    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The string value from which to convert.</param>
    public static implicit operator JsonPropertyName(string value) => new(value);

    /// <summary>
    /// Conversion to string.
    /// </summary>
    /// <param name="value">The string value from which to convert.</param>
    public static explicit operator string(JsonPropertyName value) => value.GetString();

    /// <summary>
    /// Equals operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the values are equal.</returns>
    public static bool operator ==(in JsonPropertyName left, in JsonPropertyName right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Not equals operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if the values are not equal.</returns>
    public static bool operator !=(in JsonPropertyName left, in JsonPropertyName right)
    {
        return !(left == right);
    }

    /// <summary>
    /// Less than operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if lhs is less than rhs.</returns>
    public static bool operator <(in JsonPropertyName left, in JsonPropertyName right)
    {
        return left.CompareTo(right) < 0;
    }

    /// <summary>
    /// Less than or equals operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if lhs is less than or equal to rhs.</returns>
    public static bool operator <=(in JsonPropertyName left, in JsonPropertyName right)
    {
        return left.CompareTo(right) <= 0;
    }

    /// <summary>
    /// Greater than operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if lhs is greater than rhs.</returns>
    public static bool operator >(in JsonPropertyName left, in JsonPropertyName right)
    {
        return left.CompareTo(right) > 0;
    }

    /// <summary>
    /// Greater than or equal to operator.
    /// </summary>
    /// <param name="left">The lhs.</param>
    /// <param name="right">The rhs.</param>
    /// <returns><see langword="true"/> if lhs is greater than or equal to rhs.</returns>
    public static bool operator >=(in JsonPropertyName left, in JsonPropertyName right)
    {
        return left.CompareTo(right) >= 0;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonPropertyName"/> struct.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonString{T}"/>.</typeparam>
    /// <param name="value">The value from which to construct the property name.</param>
    /// <returns>An instance of the <see cref="JsonPropertyName"/> initialized from the JsonString.</returns>
    public static JsonPropertyName FromJsonString<T>(in T value)
        where T : struct, IJsonString<T>
    {
        if (value.HasJsonElementBacking)
        {
            return new(value.AsJsonElement);
        }

        return new(value.GetString()!);
    }

    /// <summary>
    /// Parses a JSON value from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonPropertyName ParseValue(ReadOnlySpan<char> buffer)
    {
        int maxByteCount = Encoding.UTF8.GetMaxByteCount(buffer.Length);

#if NET8_0_OR_GREATER
        byte[]? pooledBytes = null;

        Span<byte> utf8Buffer = maxByteCount <= JsonConstants.StackallocThreshold ?
            stackalloc byte[maxByteCount] :
            (pooledBytes = ArrayPool<byte>.Shared.Rent(maxByteCount));

        try
        {
            int written = Encoding.UTF8.GetBytes(buffer, utf8Buffer);
            Utf8JsonReader reader = new(utf8Buffer[..written]);
            return ParseValue(ref reader);
        }
        finally
        {
            if (pooledBytes is not null)
            {
                ArrayPool<byte>.Shared.Return(pooledBytes, true);
            }
        }
#else
        byte[] utf8Buffer = ArrayPool<byte>.Shared.Rent(maxByteCount);
        char[] sourceBuffer = ArrayPool<char>.Shared.Rent(buffer.Length);

        buffer.CopyTo(sourceBuffer);
        try
        {
            int written = Encoding.UTF8.GetBytes(sourceBuffer, 0, buffer.Length, utf8Buffer, 0);
            Utf8JsonReader reader = new(utf8Buffer.AsSpan(0, written));
            return ParseValue(ref reader);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(utf8Buffer, true);
            ArrayPool<char>.Shared.Return(sourceBuffer, true);
        }
#endif
    }

    /// <summary>
    /// Parses a JSON property name from a buffer.
    /// </summary>
    /// <param name="buffer">The buffer from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonPropertyName ParseValue(ReadOnlySpan<byte> buffer)
    {
        Utf8JsonReader reader = new(buffer);
        return ParseValue(ref reader);
    }

    /// <summary>
    /// Parses a JSON property name from a buffer.
    /// </summary>
    /// <param name="reader">The reader from which to parse the value.</param>
    /// <returns>The parsed value.</returns>
    public static JsonPropertyName ParseValue(ref Utf8JsonReader reader)
    {
        return new(JsonElement.ParseValue(ref reader));
    }

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is JsonProperty jp)
        {
            return this.EqualsPropertyNameOf(jp);
        }

        if (obj is JsonPropertyName jpn)
        {
            return this.Equals(jpn);
        }

        if (obj is string s)
        {
            return this.EqualsString(s.AsSpan());
        }

        if (obj is char[] ca)
        {
            return this.EqualsString(ca.AsSpan());
        }

        if (obj is byte[] ba)
        {
            return this.EqualsUtf8String(ba.AsSpan());
        }

        if (obj is JsonString js)
        {
            return this.EqualsJsonString(js);
        }

        return false;
    }

    /// <summary>
    /// Compares with another property name.
    /// </summary>
    /// <param name="other">The name with which to compare.</param>
    /// <returns>0 if they are equal, -1 if this is less than the other, 1 if this is greater than the other.</returns>
    /// <exception cref="InvalidOperationException">The values could not be compared.</exception>
    public int CompareTo(in JsonPropertyName other)
    {
        if (this.HasStringBacking)
        {
            ProcessComparison(this.stringBacking.AsMemory(), other, out int result);
            return result;
        }
        else if (this.HasJsonElementBacking)
        {
            const int state = default;
            if (this.jsonElementBacking.TryGetValue(GetPooledChars, state, out (char[] Chars, int Length) pooledChars))
            {
                try
                {
                    ReadOnlyMemory<char> memory = pooledChars.Chars.AsMemory(0, pooledChars.Length);
                    ProcessComparison(memory, other, out int result);
                    return result;
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(pooledChars.Chars);
                }
            }
        }

        if (this.HasJsonPropertyBacking)
        {
            if (TryGetJsonPropertyNameValue(this.jsonPropertyBacking, ProcessComparisonFromSpan, other, out int result))
            {
                return result;
            }
        }

        throw new InvalidOperationException();

        static bool GetPooledChars(ReadOnlySpan<char> span, in int state, [NotNullWhen(true)] out (char[] Chars, int Length) value)
        {
            char[] chars = ArrayPool<char>.Shared.Rent(span.Length);
            span.CopyTo(chars);
            value = (chars, span.Length);
            return true;
        }

        static bool ProcessComparison(ReadOnlyMemory<char> lhs, in JsonPropertyName rhs, out int result)
        {
            if (rhs.HasStringBacking)
            {
                return CompareTo(lhs, rhs.stringBacking.AsSpan(), out result);
            }

            if (rhs.HasJsonElementBacking)
            {
                return rhs.jsonElementBacking.TryGetValue(ReverseCompareTo, lhs, out result);
            }

            if (rhs.HasJsonPropertyBacking)
            {
                return TryGetJsonPropertyNameValue(rhs.jsonPropertyBacking, ReverseCompareTo, lhs, out result);
            }

            result = default;
            return false;
        }

        static bool CompareTo(ReadOnlyMemory<char> lhs, ReadOnlySpan<char> rhs, out int result)
        {
            result = lhs.Span.CompareTo(rhs, StringComparison.Ordinal);
            return true;
        }

        static bool ProcessComparisonFromSpan(ReadOnlySpan<char> lhs, in JsonPropertyName rhs, out int result)
        {
            if (rhs.HasStringBacking)
            {
                result = lhs.CompareTo(rhs.stringBacking.AsSpan(), StringComparison.Ordinal);
                return true;
            }

            char[]? pooledChars = null;
            ReadOnlyMemory<char> memory;

            if (lhs.Length == 0)
            {
                memory = ReadOnlyMemory<char>.Empty;
            }
            else
            {
                pooledChars = ArrayPool<char>.Shared.Rent(lhs.Length);
                lhs.CopyTo(pooledChars);
                memory = pooledChars.AsMemory(0, lhs.Length);
            }

            try
            {
                return ProcessComparison(memory, rhs, out result);
            }
            finally
            {
                if (pooledChars != null)
                {
                    ArrayPool<char>.Shared.Return(pooledChars);
                }
            }
        }

        static bool ReverseCompareTo(ReadOnlySpan<char> rhs, in ReadOnlyMemory<char> lhs, out int result)
        {
            result = lhs.Span.CompareTo(rhs, StringComparison.Ordinal);
            return true;
        }
    }

    /// <summary>
    /// Equality comparison.
    /// </summary>
    /// <param name="other">The other item with which to compare.</param>
    /// <returns><see langword="true"/> if the values were equal.</returns>
    /// <exception cref="InvalidOperationException">The comparison was not possible.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(in JsonPropertyName other)
    {
        if (other.HasStringBacking)
        {
#if NET8_0_OR_GREATER
            return this.EqualsString(other.stringBacking);
#else
            return this.EqualsString(other.stringBacking.AsSpan());
#endif
        }

        if (other.HasJsonElementBacking)
        {
            return this.EqualsJsonElement(other.jsonElementBacking);
        }

        if (other.HasJsonPropertyBacking)
        {
            return this.EqualsPropertyNameOf(other.jsonPropertyBacking);
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Compares with a JsonElement.
    /// </summary>
    /// <param name="jsonElement">The json element to compare.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EqualsJsonElement(JsonElement jsonElement)
    {
        if (jsonElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        if (this.HasStringBacking)
        {
            return jsonElement.ValueEquals(this.stringBacking);
        }

        if (this.HasJsonElementBacking)
        {
            if (jsonElement.TryGetValue(CompareElement, this.jsonElementBacking, out bool result))
            {
                return result;
            }
        }

        if (this.HasJsonPropertyBacking)
        {
            if (TryGetJsonPropertyNameUtf8Value(this.jsonPropertyBacking, CompareElement, jsonElement, true, out bool result))
            {
                return result;
            }

            return false;
        }

        throw new InvalidOperationException();

        static bool CompareElement(ReadOnlySpan<byte> span, in JsonElement state, out bool result)
        {
            result = state.ValueEquals(span);
            return true;
        }
    }

    /// <summary>
    /// Compare with the name of a <see cref="JsonProperty"/>.
    /// </summary>
    /// <param name="jp">The JSON property whose name is to be compared.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EqualsPropertyNameOf(JsonProperty jp)
    {
        if (this.HasJsonElementBacking)
        {
            if (this.jsonElementBacking.TryGetValue(EqualsFor, jp, out bool result))
            {
                return result;
            }

            return false;
        }

        if (this.HasStringBacking)
        {
            return jp.NameEquals(this.stringBacking);
        }

        if (this.HasJsonPropertyBacking)
        {
            if (TryGetJsonPropertyNameUtf8Value(this.jsonPropertyBacking, NameEquals, jp, true, out bool result))
            {
                return result;
            }

            return false;
        }

        throw new InvalidOperationException("Unsupported JSON property name");

        static bool EqualsFor(ReadOnlySpan<byte> name, in JsonProperty jp, out bool result)
        {
            result = jp.NameEquals(name);
            return true;
        }

        static bool NameEquals(ReadOnlySpan<byte> span, in JsonProperty state, out bool result)
        {
            result = state.NameEquals(span);
            return true;
        }
    }

    /// <summary>
    /// Compare with the name of a <see cref="JsonProperty"/>.
    /// </summary>
    /// <param name="name">The name with which to compare.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EqualsString(ReadOnlySpan<char> name)
    {
        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.ValueEquals(name);
        }

        if (this.HasStringBacking)
        {
#if NET8_0_OR_GREATER
            return name.Equals(this.stringBacking, StringComparison.Ordinal);
#else
            return name.Equals(this.stringBacking.AsSpan(), StringComparison.Ordinal);
#endif
        }

        if (this.HasJsonPropertyBacking)
        {
            return this.jsonPropertyBacking.NameEquals(name);
        }

        throw new InvalidOperationException("Unsupported JSON property name");
    }

    /// <summary>
    /// Compare with a <see cref="JsonString"/>.
    /// </summary>
    /// <param name="name">The name with which to compare.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool EqualsJsonString(in JsonString name)
    {
        if (name.HasJsonElementBacking)
        {
            return this.EqualsJsonElement(name.AsJsonElement);
        }

#if NET8_0_OR_GREATER
        return this.EqualsString((string)name);
#else
        return this.EqualsString(((string)name).AsSpan());
#endif
    }

    /// <summary>
    /// Compare with the name of a <see cref="JsonProperty"/>.
    /// </summary>
    /// <param name="utf8Name">The name with which to compare as a UTF8 string.</param>
    /// <param name="name">The name with which to compare as a string.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(ReadOnlySpan<byte> utf8Name, string name)
    {
        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.ValueEquals(name);
        }

        if (this.HasStringBacking)
        {
            return this.stringBacking.Equals(name);
        }

        if (this.HasJsonPropertyBacking)
        {
            return this.jsonPropertyBacking.NameEquals(utf8Name);
        }

        throw new InvalidOperationException("Unsupported JSON property name");
    }

    /// <summary>
    /// Compare with the name of a <see cref="JsonProperty"/>.
    /// </summary>
    /// <param name="name">The name with which to compare.</param>
    /// <returns><see langword="true"/> if the property name was equal to this name.</returns>
    /// <exception cref="InvalidOperationException">The property name did not have a valid backing.</exception>
    public bool EqualsUtf8String(ReadOnlySpan<byte> name)
    {
        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.ValueEquals(name);
        }

        if (this.HasStringBacking)
        {
#if NET8_0_OR_GREATER
            ReadOnlySpan<char> value = this.stringBacking.AsSpan();
            byte[]? bytes = null;
            try
            {
                int length = Encoding.UTF8.GetMaxByteCount(value.Length);
                Span<byte> utf8Bytes = length < JsonConstants.StackallocThreshold ?
                    stackalloc byte[length] :
                    (bytes = ArrayPool<byte>.Shared.Rent(length));
                int written = Encoding.UTF8.GetBytes(value, utf8Bytes);
                return name.SequenceEqual(utf8Bytes[..written]);
            }
            finally
            {
                if (bytes is byte[] b)
                {
                    ArrayPool<byte>.Shared.Return(b);
                }
            }
#else
            int length = Encoding.UTF8.GetMaxByteCount(this.stringBacking.Length);
            byte[] bytes = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                int written = Encoding.UTF8.GetBytes(this.stringBacking, 0, this.stringBacking.Length, bytes, 0);
                return name.SequenceEqual(bytes.AsSpan(0, written));
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(bytes);
            }
#endif
        }

        if (this.HasJsonPropertyBacking)
        {
            return this.jsonPropertyBacking.NameEquals(name);
        }

        throw new InvalidOperationException("Unsupported JSON property name");
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (this.HasJsonElementBacking)
        {
#if NET8_0_OR_GREATER
            const int state = default;
            if (this.jsonElementBacking.TryGetValue(GetHashCodeParser, state, out int result))
            {
                return result;
            }

            throw new InvalidOperationException();
#else
            return this.jsonElementBacking.GetString()?.GetHashCode() ?? 0;
#endif
        }

        if (this.HasStringBacking)
        {
            return this.stringBacking.GetHashCode();
        }

        if (this.HasJsonPropertyBacking)
        {
#if NET8_0_OR_GREATER
            const int state = default;
            if (TryGetJsonPropertyNameValue(this.jsonPropertyBacking, GetHashCodeParser, state, out int result))
            {
                return result;
            }

            throw new InvalidOperationException();
#else
            return this.jsonPropertyBacking.Name.GetHashCode();
#endif
        }

        throw new InvalidOperationException("Unsupported JSON property name");

#if NET8_0_OR_GREATER
        static bool GetHashCodeParser(ReadOnlySpan<char> name, in int state, out int result)
        {
            result = string.GetHashCode(name);
            return true;
        }
#endif
    }

    /// <inheritdoc/>
    public override string? ToString()
    {
        if (this.TryGetString(out string? value))
        {
            return value;
        }

        return base.ToString();
    }

    /// <summary>
    /// Gets the value as a string.
    /// </summary>
    /// <returns>The value as a string.</returns>
    /// <exception cref="InvalidOperationException">The value could not be converted to a string.</exception>
    public string GetString()
    {
        if (this.TryGetString(out string? value))
        {
            return value;
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Try to get a property from a JSON element.
    /// </summary>
    /// <param name="jsonElement">The json element from which to retrieve the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><see langword="true"/> if the property could be retrieved.</returns>
    public bool TryGetProperty(in JsonElement jsonElement, out JsonElement value)
    {
        if (this.HasStringBacking)
        {
            return jsonElement.TryGetProperty(this.stringBacking, out value);
        }

        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.TryGetValue(TryGetProperty, jsonElement, out value);
        }

        if (this.HasJsonPropertyBacking)
        {
            return TryGetJsonPropertyNameUtf8Value(this.jsonPropertyBacking, TryGetProperty, jsonElement, true, out value);
        }

        throw new InvalidOperationException();

        static bool TryGetProperty(ReadOnlySpan<byte> propertyName, in JsonElement jsonElement, out JsonElement jsonValue)
        {
            return jsonElement.TryGetProperty(propertyName, out jsonValue);
        }
    }

    /// <summary>
    /// Try to get the value as a string.
    /// </summary>
    /// <param name="value">The value as a string.</param>
    /// <returns><see langword="true"/> if the value could be returned as a string.</returns>
    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        if (this.HasStringBacking)
        {
            value = this.stringBacking;
            return true;
        }

        if (this.HasJsonElementBacking)
        {
            value = this.jsonElementBacking.GetString();
            return value is not null;
        }

        if (this.HasJsonPropertyBacking)
        {
            value = this.jsonPropertyBacking.Name;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public bool TryGetValue<TState, TResult>(Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? result)
    {
        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.TryGetValue(parser, state, out result);
        }

        if (this.HasStringBacking)
        {
            return parser(this.stringBacking.AsSpan(), state, out result);
        }

        if (this.HasJsonPropertyBacking)
        {
            return TryGetJsonPropertyNameValue(this.jsonPropertyBacking, parser, state, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public bool TryGetValue<TState, TResult>(Utf8Parser<TState, TResult> parser, in TState state, [NotNullWhen(true)] out TResult? result)
    {
        return this.TryGetValue(parser, state, true, out result);
    }

    /// <summary>
    /// Parses a value from a JsonString type.
    /// </summary>
    /// <typeparam name="TState">The state passed in to the parser.</typeparam>
    /// <typeparam name="TResult">The result of parsing the string.</typeparam>
    /// <param name="parser">The parser to perform the conversion.</param>
    /// <param name="state">The state to be passed to the parser.</param>
    /// <param name="decode">Determines whether to decode the UTF8 bytes.</param>
    /// <param name="result">The result of the parsing.</param>
    /// <returns><see langword="true"/> if the result was parsed successfully, otherwise <see langword="false"/>.</returns>
    public bool TryGetValue<TState, TResult>(Utf8Parser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? result)
    {
        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.TryGetValue(parser, state, decode, out result);
        }

        if (this.HasJsonPropertyBacking)
        {
            return TryGetJsonPropertyNameUtf8Value(this.jsonPropertyBacking, parser, state, decode, out result);
        }

        result = default;
        return false;
    }

    internal static bool TryGetJsonPropertyNameValue<TState, TResult>(
        in JsonProperty jsonProperty,
        Parser<TState, TResult> parser,
        in TState state,
        [NotNullWhen(true)] out TResult? result)
    {
#if BUILDING_SOURCE_GENERATOR
        return parser(jsonProperty.Name.AsSpan(), state, out result);
#else
        ReadOnlySpan<byte> rawName = System.Runtime.InteropServices.JsonMarshal.GetRawUtf8PropertyName(jsonProperty);
        int idx = rawName.IndexOf(JsonConstants.BackSlash);

        if (idx < 0)
        {
            return TryGetJsonPropertyNameValueFromDecodedUtf8(rawName, parser, state, out result);
        }

        byte[]? rentedArray = null;
        int length = rawName.Length;
        Span<byte> unescaped = length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (rentedArray = ArrayPool<byte>.Shared.Rent(length));

        try
        {
            JsonReaderHelper.Unescape(rawName, unescaped, idx, out int written);
            return TryGetJsonPropertyNameValueFromDecodedUtf8(unescaped[..written], parser, state, out result);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray, true);
            }
        }
#endif
    }

    internal static bool TryGetJsonPropertyNameUtf8Value<TState, TResult>(in JsonProperty jsonProperty, Utf8Parser<TState, TResult> parser, in TState state, bool decode, [NotNullWhen(true)] out TResult? result)
    {
#if BUILDING_SOURCE_GENERATOR
        ReadOnlySpan<char> name = jsonProperty.Name.AsSpan();
        byte[]? rentedArray = null;
        int length = Encoding.UTF8.GetMaxByteCount(name.Length);
        Span<byte> utf8Name = length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (rentedArray = ArrayPool<byte>.Shared.Rent(length));

        try
        {
            int written = JsonReaderHelper.TranscodeHelper(name, utf8Name);
            return parser(utf8Name[..written], state, out result);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray, true);
            }
        }
#else
        ReadOnlySpan<byte> rawName = System.Runtime.InteropServices.JsonMarshal.GetRawUtf8PropertyName(jsonProperty);

        if (!decode)
        {
            return parser(rawName, state, out result);
        }

        int idx = rawName.IndexOf(JsonConstants.BackSlash);

        if (idx < 0)
        {
            return parser(rawName, state, out result);
        }

        byte[]? rentedArray = null;
        int length = rawName.Length;
        Span<byte> unescaped = length <= JsonConstants.StackallocThreshold ?
            stackalloc byte[JsonConstants.StackallocThreshold] :
            (rentedArray = ArrayPool<byte>.Shared.Rent(length));

        try
        {
            JsonReaderHelper.Unescape(rawName, unescaped, idx, out int written);
            return parser(unescaped[..written], state, out result);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray, true);
            }
        }
#endif
    }

    private static bool TryGetJsonPropertyNameValueFromDecodedUtf8<TState, TResult>(
        ReadOnlySpan<byte> decodedUtf8Name,
        Parser<TState, TResult> parser,
        in TState state,
        [NotNullWhen(true)] out TResult? result)
    {
        char[]? rentedArray = null;
        int length = checked(decodedUtf8Name.Length * JsonConstants.MaxExpansionFactorWhileTranscoding);
        Span<char> transcoded = length <= JsonConstants.StackallocThreshold ?
            stackalloc char[JsonConstants.StackallocThreshold] :
            (rentedArray = ArrayPool<char>.Shared.Rent(length));

        try
        {
            int written = JsonReaderHelper.TranscodeHelper(decodedUtf8Name, transcoded);
            return parser(transcoded[..written], state, out result);
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Write the name to a <see cref="Utf8JsonReader"/>.
    /// </summary>
    /// <param name="writer">The writer to which to write the name.</param>
    public void WriteTo(Utf8JsonWriter writer)
    {
        if (this.HasJsonElementBacking)
        {
            this.jsonElementBacking.TryGetValue(WritePropertyName, writer, out int _);
            return;
        }

        if (this.HasStringBacking)
        {
            writer.WritePropertyName(this.stringBacking);
            return;
        }

        if (this.HasJsonPropertyBacking)
        {
            TryGetJsonPropertyNameUtf8Value(this.jsonPropertyBacking, WritePropertyName, writer, true, out int _);
            return;
        }

        throw new InvalidOperationException("Unsupported JSON property name");

        static bool WritePropertyName(ReadOnlySpan<byte> name, in Utf8JsonWriter writer, out int value)
        {
            writer.WritePropertyName(name);
            value = default;
            return true;
        }
    }

    /// <summary>
    /// Gets the property name as a JSON string of the given type.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonString{T}"/>.</typeparam>
    /// <returns>An instance of the property name converted to the given type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T As<T>()
        where T : struct, IJsonString<T>
    {
#if NET8_0_OR_GREATER
        if (this.HasJsonElementBacking)
        {
            return T.FromJson(this.jsonElementBacking);
        }

        if (this.HasJsonPropertyBacking)
        {
            return T.FromString(new JsonString(this.jsonPropertyBacking.Name));
        }

        return T.FromString(new JsonString(this.stringBacking));
#else
        if (this.HasJsonElementBacking)
        {
            return JsonValueNetStandard20Extensions.FromJsonElement<T>(this.jsonElementBacking);
        }

        if (this.HasJsonPropertyBacking)
        {
            return new JsonString(this.jsonPropertyBacking.Name).As<T>();
        }

        return new JsonString(this.stringBacking).As<T>();

#endif
    }

    /// <summary>
    /// Gets a value indicating whether this property name matches
    /// the given <see cref="Regex"/>.
    /// </summary>
    /// <param name="regex">The regular expression to match.</param>
    /// <returns><see langword="true"/> if the expression is a match.</returns>
    /// <exception cref="InvalidOperationException">The name was not in a valid state.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsMatch(Regex regex)
    {
        if (this.HasJsonElementBacking)
        {
            if (this.jsonElementBacking.TryGetValue(MatchRegex, regex, out bool isMatch))
            {
                return isMatch;
            }

            return false;
        }

        if (this.HasStringBacking)
        {
            return regex.IsMatch(this.stringBacking);
        }

        if (this.HasJsonPropertyBacking)
        {
            if (TryGetJsonPropertyNameValue(this.jsonPropertyBacking, MatchRegex, regex, out bool isMatch))
            {
                return isMatch;
            }

            return false;
        }

        throw new InvalidOperationException();

        static bool MatchRegex(ReadOnlySpan<char> span, in Regex regex, out bool value)
        {
            value = regex.IsMatch(span);
            return true;
        }
    }

    /// <summary>
    /// Gets an estimate of the length of the name.
    /// </summary>
    /// <returns>An estimate of the length of the name.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int EstimateCharLength()
    {
        if (this.HasJsonElementBacking)
        {
            // Default to 64 characters as a reasonable guess for how much space we will need.
            // Tragically, we can't find out from JsonElement.
            return 64;
        }

        if (this.HasStringBacking)
        {
            return this.stringBacking.Length;
        }

        throw new InvalidOperationException("Unsupported JSON property name");
    }

    /// <summary>
    /// Copies the value to a buffer, returning the required length.
    /// </summary>
    /// <param name="memory">The memory to which to write the value.</param>
    /// <param name="length">The length that is needed.</param>
    /// <returns><see langword="true"/> if the value was copied successfully, otherwise false. The required length will be set in either case.</returns>
    internal bool TryCopyTo(Memory<char> memory, out int length)
    {
        if (this.HasStringBacking)
        {
            length = this.stringBacking.Length;
            if (memory.Length < length)
            {
                return false;
            }

#if NET8_0_OR_GREATER
            this.stringBacking.CopyTo(memory.Span);
#else
            this.stringBacking.AsSpan().CopyTo(memory.Span);
#endif
            return true;
        }

        if (this.HasJsonElementBacking)
        {
            return this.jsonElementBacking.TryGetValue(CopyTo, memory, out length);
        }

        throw new InvalidOperationException();

        static bool CopyTo(ReadOnlySpan<char> span, in Memory<char> memory, out int length)
        {
            length = span.Length;

            if (memory.Length < length)
            {
                return false;
            }

            span.CopyTo(memory.Span);
            return true;
        }
    }
}