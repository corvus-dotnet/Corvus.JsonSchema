// <copyright file="JsonReferenceContentComparer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Compares <see cref="JsonReference"/> values by their text, ordinally.
/// </summary>
/// <remarks>
/// <see cref="JsonReference.GetHashCode"/> hashes the identity of the memory that holds the reference,
/// so a <see cref="JsonReference"/> cannot key a hash table with the default comparer. This comparer
/// hashes the characters instead, so a table keyed by <see cref="JsonReference"/> behaves like the
/// ordinal string-keyed table it replaces, without converting every key to a string for a lookup.
/// </remarks>
internal sealed class JsonReferenceContentComparer : IEqualityComparer<JsonReference>
{
    private JsonReferenceContentComparer()
    {
    }

    /// <summary>
    /// Gets the instance of the comparer.
    /// </summary>
    public static JsonReferenceContentComparer Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(JsonReference x, JsonReference y) => x.Equals(y);

    /// <inheritdoc/>
    public int GetHashCode(JsonReference obj)
    {
        // FNV-1a over the characters. The URI and fragment together are the whole reference, and
        // equal references split identically, so hashing the two parts is consistent with Equals.
        uint hash = 2166136261;
        foreach (char c in obj.Uri)
        {
            hash = (hash ^ c) * 16777619;
        }

        foreach (char c in obj.Fragment)
        {
            hash = (hash ^ c) * 16777619;
        }

        return unchecked((int)hash);
    }
}