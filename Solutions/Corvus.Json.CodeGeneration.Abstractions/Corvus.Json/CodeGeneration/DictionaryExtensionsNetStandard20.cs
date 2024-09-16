// <copyright file="DictionaryExtensionsNetStandard20.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Extensions to patch missing APIs in netstandard2.0.
/// </summary>
internal static class DictionaryExtensionsNetStandard20
{
    /// <summary>
    /// Adds a key/value pair to the <see cref="IDictionary{TKey, TValue}"/> if the key does not already exist.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="dictionary">The dictionary to which to add the entry.</param>
    /// <param name="key">The key for the entry.</param>
    /// <param name="value">The value for the entry.</param>
    /// <returns><see langword="true"/> if the entry was added.</returns>
    public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue value)
    {
        if (dictionary.ContainsKey(key))
        {
            return false;
        }

        dictionary.Add(key, value);
        return true;
    }
}