// <copyright file="PatchBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.Patch.Model;

namespace Corvus.Json.Patch;
/// <summary>
/// Collates a patch operation on a <see cref="IJsonValue"/>.
/// </summary>
public readonly record struct PatchBuilder(JsonAny Value, JsonPatchDocument PatchOperations)
{
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
        var operation = JsonPatchDocument.CopyEntity.Create(from, path);
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
        var operation = JsonPatchDocument.MoveEntity.Create(from, path);
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
        var operation = JsonPatchDocument.TestEntity.Create(value, path);
        if (this.Value.TryApplyPatch(JsonPatchDocument.FromItems(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"The 'test' operation failed. value: {value}, path: {path}");
    }
}