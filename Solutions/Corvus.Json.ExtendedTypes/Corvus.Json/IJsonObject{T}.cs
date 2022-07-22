// <copyright file="IJsonObject{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json;

/// <summary>
/// A JSON object.
/// </summary>
/// <typeparam name="T">The type implementing the interface.</typeparam>
public interface IJsonObject<T> : IJsonValue<T>
    where T : struct, IJsonObject<T>
{
    /// <summary>
    /// Gets the value of the property with the given name.
    /// </summary>
    /// <param name="name">The name of the property to get.</param>
    /// <returns>The item at the given index.</returns>
    /// <exception cref="IndexOutOfRangeException">The property was not present in the object.</exception>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    JsonAny this[in JsonPropertyName name] { get; }

    /// <summary>
    /// Conversion from immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(ImmutableDictionary<JsonPropertyName, JsonAny> value);

    /// <summary>s
    /// Conversion to immutable list.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator ImmutableDictionary<JsonPropertyName, JsonAny>(T value);

    /// <summary>
    /// Enumerate the array.
    /// </summary>
    /// <returns>An enumerator for the object.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    JsonObjectEnumerator EnumerateObject();

    /// <summary>
    /// Gets a value indicating whether the object has any properties.
    /// </summary>
    /// <returns><c>True</c> if the object has properties.</returns>
    /// <exception cref="InvalidOperationException">The value was not an object.</exception>
    bool HasProperties();

    /// <summary>
    /// Determine if a property exists on the object.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool HasProperty(in JsonPropertyName name);

    /// <summary>
    /// Determine if a property exists on the object.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool HasProperty(string name);

    /// <summary>
    /// Determine if a property exists on the object.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool HasProperty(ReadOnlySpan<char> name);

    /// <summary>
    /// Determine if a property exists on the object.
    /// </summary>
    /// <param name="utf8Name">The name of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool HasProperty(ReadOnlySpan<byte> utf8Name);

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty(in JsonPropertyName name, out JsonAny value);

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty(string name, out JsonAny value);

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty(ReadOnlySpan<char> name, out JsonAny value);

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <param name="utf8Name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty(ReadOnlySpan<byte> utf8Name, out JsonAny value);

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty<TValue>(in JsonPropertyName name, out TValue value)
        where TValue : struct, IJsonValue<TValue>;

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty<TValue>(string name, out TValue value)
        where TValue : struct, IJsonValue<TValue>;

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty<TValue>(ReadOnlySpan<char> name, out TValue value)
        where TValue : struct, IJsonValue<TValue>;

    /// <summary>
    /// Get a property.
    /// </summary>
    /// <typeparam name="TValue">The type of the value to get.</typeparam>
    /// <param name="utf8Name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns><c>True</c> if the property was present.</returns>
    /// <exception cref="InvalidOperationException">The value is not an object.</exception>
    bool TryGetProperty<TValue>(ReadOnlySpan<byte> utf8Name, out TValue value)
        where TValue : struct, IJsonValue<TValue>;

    /// <summary>
    /// Sets the given property value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>The instance with the property set.</returns>
    T SetProperty<TValue>(in JsonPropertyName name, TValue value)
        where TValue : struct, IJsonValue;

    /// <summary>
    /// Sets the given property value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>The instance with the property set.</returns>
    T SetProperty<TValue>(string name, TValue value)
        where TValue : struct, IJsonValue;

    /// <summary>
    /// Sets the given property value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>The instance with the property set.</returns>
    T SetProperty<TValue>(ReadOnlySpan<char> name, TValue value)
        where TValue : struct, IJsonValue;

    /// <summary>
    /// Sets the given property value.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="utf8Name">The utf8-encoded name of the property.</param>
    /// <param name="value">The value of the property.</param>
    /// <returns>The instance with the property set.</returns>
    T SetProperty<TValue>(ReadOnlySpan<byte> utf8Name, TValue value)
        where TValue : struct, IJsonValue;

    /// <summary>
    /// Removes the given property value.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns>The isntance with the property removed.</returns>
    T RemoveProperty(in JsonPropertyName name);

    /// <summary>
    /// Removes the given property value.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns>The isntance with the property removed.</returns>
    T RemoveProperty(string name);

    /// <summary>
    /// Removes the given property value.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <returns>The isntance with the property removed.</returns>
    T RemoveProperty(ReadOnlySpan<char> name);

    /// <summary>
    /// Removes the given property value.
    /// </summary>
    /// <param name="utf8Name">The utf8-encoded name of the property.</param>
    /// <returns>The isntance with the property removed.</returns>
    T RemoveProperty(ReadOnlySpan<byte> utf8Name);

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    static abstract T FromProperties(IDictionary<JsonPropertyName, JsonAny> source);

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    static abstract T FromProperties(ImmutableDictionary<JsonPropertyName, JsonAny> source);

    /// <summary>
    /// Creates an instance of the type from the given dictionary of properties.
    /// </summary>
    /// <param name="source">The dictionary of properties.</param>
    /// <returns>An instance of the type initialized from the dictionary of properties.</returns>
    static abstract T FromProperties(params (JsonPropertyName Name, JsonAny Value)[] source);

    /// <summary>
    /// Gets the object as an immutable dictionary of properties.
    /// </summary>
    /// <returns>An immutable dictionary of the properties of the object.</returns>
    ImmutableDictionary<JsonPropertyName, JsonAny> AsImmutableDictionary();

    /// <summary>
    /// Gets the object as an immutable dictionary builder of properties.
    /// </summary>
    /// <returns>An immutable dictionary builder, populated with the properties of the object.</returns>
    public ImmutableDictionary<JsonPropertyName, JsonAny>.Builder AsImmutableDictionaryBuilder();
}