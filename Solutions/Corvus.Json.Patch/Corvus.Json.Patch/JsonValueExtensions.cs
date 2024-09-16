// <copyright file="JsonValueExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.Patch;

/// <summary>
/// Patch-related extensions to <see cref="IJsonValue"/>.
/// </summary>
public static class JsonValueExtensions
{
    private static readonly JsonObject EmptyObject = JsonObject.FromProperties(ImmutableList<JsonObjectProperty>.Empty);

    /// <summary>
    /// Sets a property on a JSON value, building any missing object property structure on the way.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue{T}"/> on which to set the deep property.</typeparam>
    /// <typeparam name="TValue">The type of the <see cref="IJsonValue{T}"/> to set.</typeparam>
    /// <param name="jsonValue">The <see cref="IJsonValue{T}"/> on which to set the deep property.</param>
    /// <param name="path">The path at which to set the property.</param>
    /// <param name="value">The value to set at that path.</param>
    /// <param name="result">An instance of the value, with any additional object property structure built, and the appropriate value added or replaced.</param>
    /// <returns><see langword="true"/> if the property could be set, otherwise <see langword="false"/>.</returns>
    public static bool TrySetDeepProperty<T, TValue>(this T jsonValue, ReadOnlySpan<char> path, in TValue value, out T result)
        where T : struct, IJsonValue<T>
        where TValue : struct, IJsonValue<TValue>
    {
        if (path.Length == 0)
        {
            if (jsonValue.TryReplace(path.ToString(), value.AsAny, out T patched))
            {
                result = patched;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        bool goingDeep = false;
        int nextSlash;
        int currentIndex = 0;

        // To avoid constantly re-walking the tree, we stash the "last found" node,
        // and trim the path as we go.
        JsonAny currentNode = jsonValue.AsAny;
        T currentResult = jsonValue;

        // Ignore a trailing slash
        ReadOnlySpan<char> currentPath = (path[^1] == '/') ? path[..^1] : path;

#if NET8_0_OR_GREATER
        while ((nextSlash = currentPath.IndexOf("/", StringComparison.Ordinal)) >= 0)
#else
        while ((nextSlash = currentPath.IndexOf('/')) >= 0)
#endif
        {
            currentIndex += nextSlash;

            if (!goingDeep && currentNode.TryResolvePointer(currentPath[..nextSlash], out currentNode))
            {
                currentPath = currentPath[(nextSlash + 1)..];
                currentIndex++;
            }
            else
            {
                goingDeep = true;
                if (!currentResult.TryAdd(
                    path[..currentIndex].ToString(),
                    EmptyObject,
                    out currentResult))
                {
                    result = default;
                    return false;
                }

                currentPath = currentPath[(nextSlash + 1)..];
                currentIndex++;
            }
        }

        // We do not have a trailing slash (we dealt with that above) so there will always be
        // something to do at the end to add or replace the final value.

        // If we are not going deep, we may be replacing the element at the path
        if (!goingDeep && jsonValue.TryResolvePointer(path, out _))
        {
            if (jsonValue.TryReplace(path.ToString(), value.AsAny, out T patched))
            {
                result = patched;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
        else
        {
            if (currentResult.TryAdd(path.ToString(), value.AsAny, out T patched))
            {
                result = patched;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }
    }
}