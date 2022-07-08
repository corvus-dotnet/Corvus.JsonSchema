// <copyright file="PatchBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using Corvus.Json.Patch.Model;

/// <summary>
/// Collates a patch operation on a <see cref="IJsonValue"/>.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "record syntax not supported by current version of StyleCop")]
public readonly record struct PatchBuilder(JsonAny Value, PatchOperationArray PatchOperations)
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
        var operation = Model.Add.Create(value, path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'add' operation value: {value}, path: {path.GetString()}");
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
        var operation = Model.Copy.Create(from, path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'copy' operation from: {from.GetString()}, path: {path.GetString()}");
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
        var operation = Model.Move.Create(from, path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'move' operation from: {from.GetString()}, path: {path.GetString()}");
    }

    /// <summary>
    /// Removes the entity at the given <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The path at which to remove the value.</param>
    /// <returns>An instance of a <see cref="PatchBuilder"/> with the updated value, and the operation added to the operation array.</returns>
    /// <exception cref="JsonPatchException">Thrown if the value cannot be removed at the given path.</exception>
    public PatchBuilder Remove(JsonPointer path)
    {
        var operation = Model.Remove.Create(path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'remove' operation path: {path.GetString()}");
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
        var operation = Model.Replace.Create(value, path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"Unable to apply 'replace' operation value: {value}, path: {path.GetString()}");
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
        var operation = Model.Test.Create(value, path);
        if (this.Value.TryApplyPatch(PatchOperationArray.From(operation), out JsonAny result))
        {
            return new(result, this.PatchOperations.Add(operation));
        }

        throw new JsonPatchException($"The 'test' operation failed. value: {value}, path: {path.GetString()}");
    }
}
