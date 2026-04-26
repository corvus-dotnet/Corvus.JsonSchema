// <copyright file="JsonDiffExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// Extension methods for computing a JSON Patch (RFC 6902) that transforms
/// one <see cref="JsonElement"/> into another.
/// </summary>
/// <remarks>
/// <para>
/// The diff operates on semantic JSON equality: object property order is
/// not significant, and all property names within an object are assumed
/// to be unique. If duplicate property names are present, only the last
/// value for each name is considered (matching <see cref="JsonElement"/>
/// comparison semantics).
/// </para>
/// <para>
/// For arrays, the diff compares element-by-element when lengths match.
/// When array lengths differ, the entire array is replaced. This produces
/// correct but not necessarily minimal patches for arrays with insertions
/// or deletions. A future version may implement LCS-based array diffing
/// for more compact patches.
/// </para>
/// </remarks>
public static class JsonDiffExtensions
{
    /// <summary>
    /// Creates a <see cref="JsonPatchDocument"/> that transforms
    /// <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    /// <param name="source">The original JSON element.</param>
    /// <param name="target">The desired JSON element.</param>
    /// <returns>A <see cref="JsonPatchDocument"/> containing the operations
    /// needed to transform <paramref name="source"/> into <paramref name="target"/>.</returns>
    public static JsonPatchDocument CreatePatch(in JsonElement source, in JsonElement target)
    {
        PatchBuilder patchBuilder = new(true);

        try
        {
            DiffRecursive(source, target, string.Empty, ref patchBuilder);
            return patchBuilder.GetPatchAndDispose();
        }
        catch
        {
            patchBuilder.Dispose();
            throw;
        }
    }

    private static void DiffRecursive(
        in JsonElement source,
        in JsonElement target,
        string path,
        ref PatchBuilder patchBuilder)
    {
        if (source == target)
        {
            return;
        }

        if (source.ValueKind != target.ValueKind)
        {
            patchBuilder.Replace(path, target);
            return;
        }

        switch (source.ValueKind)
        {
            case JsonValueKind.Object:
                DiffObject(source, target, path, ref patchBuilder);
                break;

            case JsonValueKind.Array:
                DiffArray(source, target, path, ref patchBuilder);
                break;

            default:
                // Same kind but different value (number, string, etc.)
                patchBuilder.Replace(path, target);
                break;
        }
    }

    private static void DiffObject(
        in JsonElement source,
        in JsonElement target,
        string path,
        ref PatchBuilder patchBuilder)
    {
        // Process properties present in source
        foreach (JsonProperty<JsonElement> sourceProp in source.EnumerateObject())
        {
            string name = sourceProp.Name;
            string childPath = AppendToPointer(path, name);

            if (target.TryGetProperty(name, out JsonElement targetValue))
            {
                // Property exists in both — recurse
                DiffRecursive(sourceProp.Value, targetValue, childPath, ref patchBuilder);
            }
            else
            {
                // Property removed
                patchBuilder.Remove(childPath);
            }
        }

        // Process properties present only in target
        foreach (JsonProperty<JsonElement> targetProp in target.EnumerateObject())
        {
            string name = targetProp.Name;

            if (!source.TryGetProperty(name, out _))
            {
                string childPath = AppendToPointer(path, name);
                patchBuilder.Add(childPath, targetProp.Value);
            }
        }
    }

    private static void DiffArray(
        in JsonElement source,
        in JsonElement target,
        string path,
        ref PatchBuilder patchBuilder)
    {
        int sourceLength = source.GetArrayLength();
        int targetLength = target.GetArrayLength();

        if (sourceLength != targetLength)
        {
            // Different lengths — replace whole array
            patchBuilder.Replace(path, target);
            return;
        }

        // Same length — diff element by element
        for (int i = 0; i < sourceLength; i++)
        {
            string childPath = string.Concat(path, "/", i.ToString());
            DiffRecursive(source[i], target[i], childPath, ref patchBuilder);
        }
    }

    /// <summary>
    /// Appends a property name to a JSON Pointer path, escaping per RFC 6901.
    /// </summary>
    private static string AppendToPointer(string basePath, string propertyName)
    {
        // RFC 6901: ~ → ~0, / → ~1
        if (propertyName.IndexOfAny(['~', '/']) < 0)
        {
            return string.Concat(basePath, "/", propertyName);
        }

        return string.Concat(
            basePath,
            "/",
            propertyName.Replace("~", "~0").Replace("/", "~1"));
    }
}