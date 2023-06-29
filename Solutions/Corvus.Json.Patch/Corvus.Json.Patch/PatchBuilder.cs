// <copyright file="PatchBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.Patch.Model;

namespace Corvus.Json.Patch;
/// <summary>
/// Collates a patch operation on a <see cref="IJsonValue"/>.
/// </summary>
public readonly record struct PatchBuilder(JsonAny Value, JsonPatchDocument PatchOperations)
{
    private static readonly JsonObject EmptyObject = JsonObject.FromProperties(ImmutableDictionary<JsonPropertyName, JsonAny>.Empty);

    /// <summary>
    /// Adds or replaces the value found at the given location, building any missing intermediate structure as object properties.
    /// </summary>
    /// <param name="value">The value to add or replace at the <paramref name="path"/>.</param>
    /// <param name="path">The location at which to add or replace the <paramref name="value"/>.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    public PatchBuilder DeepAddOrReplaceObjectProperties(JsonAny value, ReadOnlySpan<char> path)
    {
        if (path.Length == 0)
        {
            return this.Replace(value, path);
        }

        bool goingDeep = false;
        int nextSlash;
        int currentIndex = 0;

        // To avoid constantly re-walking the tree, we stash the "last found" node,
        // and trim the path as we go.
        JsonAny currentNode = this.Value;

        // Ignore a trailing slash
        ReadOnlySpan<char> currentPath = (path[^1] == '/') ? path[..^1] : path;
        PatchBuilder currentBuilder = this;

        while ((nextSlash = currentPath.IndexOf("/", StringComparison.Ordinal)) >= 0)
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
                currentBuilder =
                    currentBuilder.Add(
                        EmptyObject,
                        path[..currentIndex]);
                currentPath = currentPath[(nextSlash + 1)..];
                currentIndex++;
            }
        }

        // We do not have a trailing slash (we dealt with that above) so there will always be
        // something to do at the end to add or replace the final value.

        // If we are not going deep, we may be replacing the element at the path
        if (!goingDeep && this.Value.TryResolvePointer(path, out _))
        {
            return currentBuilder.Replace(value, path);
        }
        else
        {
            return currentBuilder.Add(value, path);
        }
    }

    /// <summary>
    /// Add the given <paramref name="value"/> to the entity at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="value">The <see cref="IJsonValue"/> to add.</param>
    /// <param name="path">The path at which to add the value.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be added at the given path.</exception>
    public PatchBuilder Add(JsonAny value, JsonPointer path)
    {
        var operation = JsonPatchDocument.AddEntity.Create(value, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'add' operation value: {value}, path: {path}");
    }

    /// <summary>
    /// Copy the entity at <paramref name="from"/> to the given <paramref name="path"/>.
    /// </summary>
    /// <param name="from">The path from which to copy the entity.</param>
    /// <param name="path">The path at which to add the copied entity.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be copied from the source to the path.</exception>
    public PatchBuilder Copy(JsonPointer from, JsonPointer path)
    {
        var operation = JsonPatchDocument.Copy.Create(from, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'copy' operation from: {from}, path: {path}");
    }

    /// <summary>
    /// Move the entity at <paramref name="from"/> to the given <paramref name="path"/>.
    /// </summary>
    /// <param name="from">The path from which to move the entity.</param>
    /// <param name="path">The path at which to add the copied entity.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be moved from the source location to the path.</exception>
    public PatchBuilder Move(JsonPointer from, JsonPointer path)
    {
        var operation = JsonPatchDocument.Move.Create(from, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'move' operation from: {from}, path: {path}");
    }

    /// <summary>
    /// Removes the entity at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path at which to remove the value.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be removed at the given path.</exception>
    public PatchBuilder Remove(JsonPointer path)
    {
        var operation = JsonPatchDocument.RemoveEntity.Create(path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'remove' operation path: {path}");
    }

    /// <summary>
    /// Replace the given <paramref name="value"/> to the entity at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="value">The <see cref="IJsonValue"/> with which to replace the existing value.</param>
    /// <param name="path">The path at which to replace the existing value.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be replaced at the given path.</exception>
    public PatchBuilder Replace(JsonAny value, JsonPointer path)
    {
        var operation = JsonPatchDocument.ReplaceEntity.Create(value, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'replace' operation value: {value}, path: {path}");
    }

    /// <summary>
    /// Tests that the <paramref name="value"/> is found at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="value">The <see cref="IJsonValue"/> expected at the given path.</param>
    /// <param name="path">The path at which to replace the existing value.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be replaced at the given path.</exception>
    public PatchBuilder Test(JsonAny value, JsonPointer path)
    {
        var operation = JsonPatchDocument.Test.Create(value, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"The 'test' operation failed. value: {value}, path: {path}");
    }
}