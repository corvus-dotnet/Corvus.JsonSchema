// <copyright file="JsonNotAny.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json.Internal;

namespace Corvus.Json;

/// <summary>
/// Represents any JSON value, validating false.
/// </summary>
public readonly partial struct JsonNotAny : IJsonObject<JsonNotAny>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNotAny"/> struct.
    /// </summary>
    /// <param name="value">The value from which to construct the instance.</param>
    public JsonNotAny(ImmutableDictionary<JsonPropertyName, JsonAny> value)
    {
        this.jsonElementBacking = default;
        this.backing = Backing.Object;
        this.stringBacking = string.Empty;
        this.boolBacking = default;
        this.numberBacking = default;
        this.arrayBacking = ImmutableList<JsonAny>.Empty;
        this.objectBacking = value;
    }

    /// <inheritdoc/>
    public JsonAny this[in JsonPropertyName name]
    {
        get
        {
            if (this.TryGetProperty(name, out JsonAny result))
            {
                return result;
            }

            throw new IndexOutOfRangeException();
        }
    }

    /// <summary>
    /// Conversion from immutable dictionary.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator JsonNotAny(ImmutableDictionary<JsonPropertyName, JsonAny> value)
    {
        return new(value);
    }

    /// <summary>
    /// Conversion to immutable dictionary.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    public static implicit operator ImmutableDictionary<JsonPropertyName, JsonAny>(JsonNotAny value)
    {
        return value.GetImmutableDictionary();
    }

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    public static JsonNotAny FromProperties(IDictionary<JsonPropertyName, JsonAny> source)
    {
        return new(source.ToImmutableDictionary());
    }

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    public static JsonNotAny FromProperties(ImmutableDictionary<JsonPropertyName, JsonAny> source)
    {
        return new(source);
    }

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    public static JsonNotAny FromProperties(params (JsonPropertyName Name, JsonAny Value)[] source)
    {
        return new(source.ToImmutableDictionary(k => k.Name, v => v.Value));
    }

    /// <inheritdoc/>
    public ImmutableDictionary<JsonPropertyName, JsonAny> AsImmutableDictionary()
    {
        return this.GetImmutableDictionary();
    }

    /// <inheritdoc/>
    public ImmutableDictionary<JsonPropertyName, JsonAny>.Builder AsImmutableDictionaryBuilder()
    {
        return this.GetImmutableDictionaryBuilder();
    }

    /// <inheritdoc/>
    public JsonObjectEnumerator EnumerateObject()
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

    /// <inheritdoc/>
    public bool HasProperties()
    {
        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.Count > 0;
        }

        if ((this.backing & Backing.JsonElement) != 0)
        {
            using JsonElement.ObjectEnumerator enumerator = this.jsonElementBacking.EnumerateObject();
            return enumerator.MoveNext();
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool HasProperty(in JsonPropertyName name)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            // String is the fastest approach right now. If JsonPropertyName changes
            // its internal implementation, we should switch this out.
            return this.jsonElementBacking.TryGetProperty((string)name, out _);
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ContainsKey(name);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool HasProperty(string name)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return this.jsonElementBacking.TryGetProperty(name, out _);
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ContainsKey(name);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool HasProperty(ReadOnlySpan<char> name)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return this.jsonElementBacking.TryGetProperty(name, out _);
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ContainsKey(name);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool HasProperty(ReadOnlySpan<byte> utf8Name)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            return this.jsonElementBacking.TryGetProperty(utf8Name, out _);
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ContainsKey(utf8Name);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty(in JsonPropertyName name, out JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            // String is the fastest approach right now. If JsonPropertyName changes
            // its internal implementation, we should switch this out.
            if (this.jsonElementBacking.TryGetProperty((string)name, out JsonElement result))
            {
                value = new(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.TryGetValue(name, out value);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty(string name, out JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (this.jsonElementBacking.TryGetProperty(name, out JsonElement result))
            {
                value = new(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.TryGetValue(name, out value);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty(ReadOnlySpan<char> name, out JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (this.jsonElementBacking.TryGetProperty(name, out JsonElement result))
            {
                value = new(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.TryGetValue(name, out value);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty(ReadOnlySpan<byte> utf8Name, out JsonAny value)
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (this.jsonElementBacking.TryGetProperty(utf8Name, out JsonElement result))
            {
                value = new(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.TryGetValue(utf8Name, out value);
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty<TValue>(in JsonPropertyName name, out TValue value)
        where TValue : struct, IJsonValue<TValue>
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            // String is the fastest approach right now. If JsonPropertyName changes
            // its internal implementation, we should switch this out.
            if (this.jsonElementBacking.TryGetProperty((string)name, out JsonElement result))
            {
                value = TValue.FromJson(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            if (this.objectBacking.TryGetValue(name, out JsonAny result))
            {
                value = TValue.FromAny(result);
                return true;
            }

            value = default;
            return false;
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty<TValue>(string name, out TValue value)
        where TValue : struct, IJsonValue<TValue>
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            // String is the fastest approach right now. If JsonPropertyName changes
            // its internal implementation, we should switch this out.
            if (this.jsonElementBacking.TryGetProperty(name, out JsonElement result))
            {
                value = TValue.FromJson(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            if (this.objectBacking.TryGetValue(name, out JsonAny result))
            {
                value = TValue.FromAny(result);
                return true;
            }

            value = default;
            return false;
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty<TValue>(ReadOnlySpan<char> name, out TValue value)
        where TValue : struct, IJsonValue<TValue>
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (this.jsonElementBacking.TryGetProperty(name, out JsonElement result))
            {
                value = TValue.FromJson(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            if (this.objectBacking.TryGetValue(name, out JsonAny result))
            {
                value = TValue.FromAny(result);
                return true;
            }

            value = default;
            return false;
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public bool TryGetProperty<TValue>(ReadOnlySpan<byte> utf8Name, out TValue value)
        where TValue : struct, IJsonValue<TValue>
    {
        if ((this.backing & Backing.JsonElement) != 0)
        {
            if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (this.jsonElementBacking.TryGetProperty(utf8Name, out JsonElement result))
            {
                value = TValue.FromJson(result);
                return true;
            }

            value = default;
            return false;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            if (this.objectBacking.TryGetValue(utf8Name, out JsonAny result))
            {
                value = TValue.FromAny(result);
                return true;
            }

            value = default;
            return false;
        }

        throw new InvalidOperationException();
    }

    /// <inheritdoc/>
    public JsonNotAny SetProperty<TValue>(in JsonPropertyName name, TValue value)
        where TValue : struct, IJsonValue
    {
        return new(this.GetImmutableDictionaryWith(name, value.AsAny));
    }

    /// <inheritdoc/>
    public JsonNotAny SetProperty<TValue>(string name, TValue value)
        where TValue : struct, IJsonValue
    {
        return new(this.GetImmutableDictionaryWith(name, value.AsAny));
    }

    /// <inheritdoc/>
    public JsonNotAny SetProperty<TValue>(ReadOnlySpan<char> name, TValue value)
        where TValue : struct, IJsonValue
    {
        return new(this.GetImmutableDictionaryWith(name, value.AsAny));
    }

    /// <inheritdoc/>
    public JsonNotAny SetProperty<TValue>(ReadOnlySpan<byte> utf8Name, TValue value)
        where TValue : struct, IJsonValue
    {
        return new(this.GetImmutableDictionaryWith(utf8Name, value.AsAny));
    }

    /// <inheritdoc/>
    public JsonNotAny RemoveProperty(in JsonPropertyName name)
    {
        return new(this.GetImmutableDictionaryWithout(name));
    }

    /// <inheritdoc/>
    public JsonNotAny RemoveProperty(string name)
    {
        return new(this.GetImmutableDictionaryWithout(name));
    }

    /// <inheritdoc/>
    public JsonNotAny RemoveProperty(ReadOnlySpan<char> name)
    {
        return new(this.GetImmutableDictionaryWithout(name));
    }

    /// <inheritdoc/>
    public JsonNotAny RemoveProperty(ReadOnlySpan<byte> utf8Name)
    {
        return new(this.GetImmutableDictionaryWithout(utf8Name));
    }

    /// <summary>
    /// Builds an <see cref="ImmutableDictionary{JsonPropertyName, JsonAny}"/> from the object.
    /// </summary>
    /// <returns>An immutable list of <see cref="JsonAny"/> built from the array.</returns>
    /// <exception cref="InvalidOperationException">The value is not an array.</exception>
    private ImmutableDictionary<JsonPropertyName, JsonAny> GetImmutableDictionary()
    {
        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking;
        }

        return this.GetImmutableDictionaryBuilder().ToImmutable();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableDictionary{JsonPropertyName, JsonAny}"/> from the object, without a specific property.
    /// </summary>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object, without the given property.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    private ImmutableDictionary<JsonPropertyName, JsonAny> GetImmutableDictionaryWithout(in JsonPropertyName name)
    {
        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.Remove(name);
        }

        return this.GetImmutableDictionaryBuilderWithout(name).ToImmutable();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableDictionary{JsonPropertyName, JsonAny}"/> from the object, without a specific property.
    /// </summary>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object, without the given property.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    private ImmutableDictionary<JsonPropertyName, JsonAny> GetImmutableDictionaryWith(in JsonPropertyName name, in JsonAny value)
    {
        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.SetItem(name, value);
        }

        ImmutableDictionary<JsonPropertyName, JsonAny>.Builder result = this.GetImmutableDictionaryBuilder();
        if (result.ContainsKey(name))
        {
            result.Remove(name);
        }

        result.Add(name, value);
        return result.ToImmutable();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableDictionary{JsonPropertyName, JsonAny}.Builder"/> from the object.
    /// </summary>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    private ImmutableDictionary<JsonPropertyName, JsonAny>.Builder GetImmutableDictionaryBuilder()
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableDictionary<JsonPropertyName, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<JsonPropertyName, JsonAny>();
            foreach (JsonProperty property in this.jsonElementBacking.EnumerateObject())
            {
                builder.Add(property.Name, new(property.Value));
            }

            return builder;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ToBuilder();
        }

        throw new InvalidOperationException();
    }

    /// <summary>
    /// Builds an <see cref="ImmutableDictionary{JsonPropertyName, JsonAny}.Builder"/> from the object, without a specific property.
    /// </summary>
    /// <returns>An immutable dictionary builder of <see cref="JsonPropertyName"/> to <see cref="JsonAny"/>, built from the existing object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    private ImmutableDictionary<JsonPropertyName, JsonAny>.Builder GetImmutableDictionaryBuilderWithout(in JsonPropertyName name)
    {
        if ((this.backing & Backing.JsonElement) != 0 && this.jsonElementBacking.ValueKind == JsonValueKind.Object)
        {
            ImmutableDictionary<JsonPropertyName, JsonAny>.Builder builder = ImmutableDictionary.CreateBuilder<JsonPropertyName, JsonAny>();

            JsonElement.ObjectEnumerator enumerator = this.jsonElementBacking.EnumerateObject();

            while (enumerator.MoveNext())
            {
                builder.Add(enumerator.Current.Name, new(enumerator.Current.Value));
            }

            // It is (currently) benchmarked to be faster to add them all, then remove the
            // one from the hashtable, than it is to check them all on the way through.
            builder.Remove(name);
            return builder;
        }

        if ((this.backing & Backing.Object) != 0)
        {
            return this.objectBacking.ToBuilder();
        }

        throw new InvalidOperationException();
    }
}