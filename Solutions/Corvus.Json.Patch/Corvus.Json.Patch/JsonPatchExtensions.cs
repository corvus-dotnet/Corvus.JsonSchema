// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.IO;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Extension methods to support JSON Patch [https://jsonpatch.com/].
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
public static partial class JsonPatchExtensions
{
    private static ReadOnlySpan<byte> AddAsUtf8 => "add"u8;

    private static ReadOnlySpan<byte> CopyAsUtf8 => "copy"u8;

    private static ReadOnlySpan<byte> MoveAsUtf8 => "move"u8;

    private static ReadOnlySpan<byte> RemoveAsUtf8 => "remove"u8;

    private static ReadOnlySpan<byte> ReplaceAsUtf8 => "replace"u8;

    private static ReadOnlySpan<byte> TestAsUtf8 => "test"u8;

    /// <summary>
    /// Begin gathering a <see cref="JsonPatchDocument"/> by applying successive patch operations to an initial <see cref="IJsonValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to patch.</typeparam>
    /// <param name="value">The value to patch.</param>
    /// <returns>A <see cref="PatchBuilder"/> initialized for patching the value.</returns>
    public static PatchBuilder BeginPatch<T>(this T value)
        where T : struct, IJsonValue
    {
        return new(value.AsAny, JsonPatchDocument.EmptyArray);
    }

    /// <summary>
    /// Apply a patch to a <see cref="IJsonValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/>.</typeparam>
    /// <param name="value">The value to which to apply the patch.</param>
    /// <param name="patchOperations">The patch operations to apply.</param>
    /// <param name="result">The result of applying the patch.</param>
    /// <returns><c>True</c> is the patch was applied.</returns>
    public static bool TryApplyPatch<T>(this T value, in JsonPatchDocument patchOperations, out JsonAny result)
        where T : struct, IJsonValue
    {
        JsonAny current = value.AsAny;

        if (patchOperations.GetArrayLength() == 0)
        {
            result = current;
            return false;
        }

        foreach (JsonPatchDocument.PatchOperation patchOperation in patchOperations.EnumerateArray())
        {
            if (!TryApplyPatchOperation(current, patchOperation, out current))
            {
                // The patch did not succeed
                result = value.AsAny;
                return false;
            }
        }

        result = current;
        return true;
    }

    /// <summary>
    /// Apply a single patch operation to a <see cref="IJsonValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/>.</typeparam>
    /// <param name="value">The value to which to apply the patch.</param>
    /// <param name="patchOperation">The patch operation to apply.</param>
    /// <param name="result">The result of applying the patch.</param>
    /// <returns><see langword="true"/> if the patch was applied, otherwise <see langword="false"/>.</returns>
    public static bool TryApplyPatchOperation<T>(this T value, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
        where T : struct, IJsonValue
    {
        return TryApplyPatchOperation(value.AsAny, patchOperation, out result);
    }

    /// <summary>
    /// Applies an Add operation to the node.
    /// </summary>
    /// <typeparam name="T">The type of the node to modify.</typeparam>
    /// <param name="node">The node to which to add the value.</param>
    /// <param name="path">The path at which to add the value.</param>
    /// <param name="value">The value to add.</param>
    /// <param name="result">The resulting entity.</param>
    /// <returns><see langword="true"/> if the value was added, otherwise <see langword="false"/>.</returns>
    public static bool TryAdd<T>(this T node, string path, JsonAny value, out T result)
        where T : struct, IJsonValue<T>
    {
        if (path.Length == 0)
        {
            result = value.As<T>();
            return true;
        }

        AddVisitor visitor = new(path, value);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult.As<T>();
        return transformed;
    }

    /// <summary>
    /// Applies a Replace operation to the node.
    /// </summary>
    /// <typeparam name="T">The type of the node to modify.</typeparam>
    /// <param name="node">The node in which to replace the value.</param>
    /// <param name="path">The path at which to replace the value.</param>
    /// <param name="value">The value to replace.</param>
    /// <param name="result">The resulting entity.</param>
    /// <returns><see langword="true"/> if the value was replaced, otherwise <see langword="false"/>.</returns>
    public static bool TryReplace<T>(this T node, string path, JsonAny value, out T result)
        where T : struct, IJsonValue<T>
    {
        if (path.Length == 0)
        {
            result = value.As<T>();
            return true;
        }

        ReplaceVisitor visitor = new(value, path);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult.As<T>();
        return transformed;
    }

    /// <summary>
    /// Applies a Copy operation to the node.
    /// </summary>
    /// <typeparam name="T">The type of the node to modify.</typeparam>
    /// <param name="node">The node in which to copy the value.</param>
    /// <param name="destinationPath">The path to which to copy the value.</param>
    /// <param name="sourcePath">The path from which to copy the value.</param>
    /// <param name="result">The resulting entity.</param>
    /// <returns><see langword="true"/> if the value was copied, otherwise <see langword="false"/>.</returns>
    public static bool TryCopy<T>(this T node, string destinationPath, string sourcePath, out T result)
        where T : struct, IJsonValue<T>
    {
        if (sourcePath.Equals(destinationPath))
        {
            result = node;
            return true;
        }

        if (!node.TryResolvePointer(sourcePath, out JsonAny source))
        {
            result = node;
            return false;
        }

        if (destinationPath.Length == 0)
        {
            result = source.As<T>();
            return true;
        }

        CopyVisitor visitor = new(destinationPath, source);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult.As<T>();
        return transformed;
    }

    /// <summary>
    /// Applies a Move operation to the node.
    /// </summary>
    /// <typeparam name="T">The type of the node to modify.</typeparam>
    /// <param name="node">The node in which to move the value.</param>
    /// <param name="destinationPath">The path to which to move the value.</param>
    /// <param name="sourcePath">The path from which to move the value.</param>
    /// <param name="result">The resulting entity.</param>
    /// <returns><see langword="true"/> if the value was moved, otherwise <see langword="false"/>.</returns>
    public static bool TryMove<T>(this T node, string destinationPath, string sourcePath, out T result)
        where T : struct, IJsonValue<T>
    {
        if (sourcePath.Equals(destinationPath))
        {
            result = node;
            return true;
        }

        if (!node.TryResolvePointer(sourcePath, out JsonAny source))
        {
            result = node;
            return false;
        }

        if (destinationPath.Length == 0)
        {
            result = source.As<T>();
            return true;
        }

        MoveVisitor visitor = new(destinationPath, sourcePath, source);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult.As<T>();
        return transformed;
    }

    /// <summary>
    /// Applies a Remove operation to the node.
    /// </summary>
    /// <typeparam name="T">The type of the node to modify.</typeparam>
    /// <param name="node">The node from which to remove the value.</param>
    /// <param name="path">The path at which to remove the value.</param>
    /// <param name="result">The resulting entity.</param>
    /// <returns><see langword="true"/> if the value was removed, otherwise <see langword="false"/>.</returns>
    public static bool TryRemove<T>(this T node, string path, out T result)
        where T : struct, IJsonValue<T>
    {
        RemoveVisitor visitor = new(path);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult.As<T>();
        return transformed;
    }

    private static bool TryApplyPatchOperation(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        JsonString op = patchOperation.Op;

        if (patchOperation.HasJsonElementBacking)
        {
            if (op.EqualsUtf8Bytes(AddAsUtf8))
            {
                return TryApplyAdd(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(CopyAsUtf8))
            {
                return TryApplyCopy(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(MoveAsUtf8))
            {
                return TryApplyMove(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(RemoveAsUtf8))
            {
                return TryApplyRemove(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(ReplaceAsUtf8))
            {
                return TryApplyReplace(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(TestAsUtf8))
            {
                return TryApplyTest(node, patchOperation, out result);
            }

            result = node;
            return false;
        }

        if (op.EqualsString("add"))
        {
            return TryApplyAdd(node, patchOperation, out result);
        }

        if (op.EqualsString("copy"))
        {
            return TryApplyCopy(node, patchOperation, out result);
        }

        if (op.EqualsString("move"))
        {
            return TryApplyMove(node, patchOperation, out result);
        }

        if (op.EqualsString("remove"))
        {
            return TryApplyRemove(node, patchOperation, out result);
        }

        if (op.EqualsString("replace"))
        {
            return TryApplyReplace(node, patchOperation, out result);
        }

        if (op.EqualsString("test"))
        {
            return TryApplyTest(node, patchOperation, out result);
        }

        result = node;
        return false;
    }

    private static bool TryGetArrayIndex(ReadOnlySpan<char> pathSegment, [NotNullWhen(true)] out int index)
    {
        if (pathSegment.Length > 1 && pathSegment[0] == '0')
        {
            index = 0;
            return false;
        }

        return int.TryParse(pathSegment, out index);
    }

    private static bool TryApplyAdd(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(JsonPatchDocument.AddEntity.JsonPropertyNames.PathUtf8, out JsonString pathAny);
        patchOperation.TryGetProperty(JsonPatchDocument.AddEntity.JsonPropertyNames.ValueUtf8, out JsonAny value);
        string path = (string)pathAny;
        return TryAdd(node, path, value, out result);
    }

    private static bool TryApplyCopy(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(JsonPatchDocument.Copy.JsonPropertyNames.FromValueUtf8, out JsonString fromAny);
        patchOperation.TryGetProperty(JsonPatchDocument.Copy.JsonPropertyNames.PathUtf8, out JsonString pathAny);

        if (fromAny.Equals(pathAny))
        {
            result = node;
            return true;
        }

        string from = (string)fromAny;
        string path = (string)pathAny;

        if (!node.TryResolvePointer(from, out JsonAny source))
        {
            result = node;
            return false;
        }

        if (path.Length == 0)
        {
            result = source;
            return true;
        }

        CopyVisitor visitor = new(path, source);
        return node.Visit(visitor.Visit, out result);
    }

    private static bool TryApplyMove(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(JsonPatchDocument.Move.JsonPropertyNames.FromValueUtf8, out JsonString fromAny);
        patchOperation.TryGetProperty(JsonPatchDocument.Move.JsonPropertyNames.PathUtf8, out JsonString pathAny);

        if (fromAny.Equals(pathAny))
        {
            result = node;
            return true;
        }

        string from = (string)fromAny;
        string path = (string)pathAny;

        if (!node.TryResolvePointer(from, out JsonAny source))
        {
            result = node;
            return false;
        }

        if (path.Length == 0)
        {
            result = source;
            return true;
        }

        MoveVisitor visitor = new(path, from, source);
        return node.Visit(visitor.Visit, out result);
    }

    private static bool TryApplyRemove(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        RemoveVisitor visitor = new(patchOperation);
        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyReplace(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        ReplaceVisitor visitor = new(patchOperation);

        if (visitor.Path.Length == 0)
        {
            result = visitor.Value;
            return true;
        }

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyTest(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        result = node;

        // Find the node to test.
        if (node.TryResolvePointer(patchOperation.Path, out JsonAny itemToTest))
        {
            if (patchOperation.TryGetProperty(JsonPatchDocument.Test.JsonPropertyNames.ValueUtf8, out JsonAny value))
            {
                // Verify that the value of the node is the one supplied in the test operation.
                return itemToTest.Equals(value);
            }
        }

        return false;
    }
}