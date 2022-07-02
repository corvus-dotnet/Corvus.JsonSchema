// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using System.Diagnostics.CodeAnalysis;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// Extension methods to support JSON Patch [https://jsonpatch.com/].
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
public static partial class JsonPatchExtensions
{
    /// <summary>
    /// Begin gathering a <see cref="PatchOperationArray"/> by applying successive patch operations to an initial <see cref="IJsonValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of the <see cref="IJsonValue"/> to patch.</typeparam>
    /// <param name="value">The value to patch.</param>
    /// <returns>A <see cref="PatchBuilder"/> initialized for patching the value.</returns>
    public static PatchBuilder BeginPatch<T>(this T value)
        where T : struct, IJsonValue
    {
        return new(value.AsAny, JsonArray.Empty);
    }

    /// <summary>
    /// Apply a patch to a <see cref="IJsonValue"/>.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="IJsonValue"/>.</typeparam>
    /// <param name="value">The value to which to apply the patch.</param>
    /// <param name="patchOperations">The patch operations to apply.</param>
    /// <param name="result">The result of applying the patch.</param>
    /// <returns><c>True</c> is the patch was applied.</returns>
    public static bool TryApplyPatch<T>(this T value, PatchOperationArray patchOperations, out JsonAny result)
        where T : struct, IJsonValue
    {
        JsonAny current = value.AsAny;

        if (!patchOperations.IsValid())
        {
            result = current;
            return false;
        }

        foreach (PatchOperation patchOperation in patchOperations.EnumerateItems())
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

    private static bool TryApplyPatchOperation(JsonAny node, PatchOperation patchOperation, out JsonAny result)
    {
        switch (patchOperation.Op.GetString())
        {
            case "add":
                return TryApplyAdd(node, patchOperation, out result);
            case "copy":
                return TryApplyCopy(node, patchOperation, out result);
            case "move":
                return TryApplyMove(node, patchOperation, out result);
            case "remove":
                return TryApplyRemove(node, patchOperation, out result);
            case "replace":
                return TryApplyReplace(node, patchOperation, out result);
            case "test":
                return TryApplyTest(node, patchOperation, out result);
            default:
                result = node;
                return false;
        }
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

    private static JsonAny? FindSourceElement(JsonAny root, ReadOnlySpan<char> from)
    {
        // Try to find the node to copy
        if (root.TryResolvePointer(from, out JsonAny sourceElement))
        {
            return sourceElement;
        }

        return null;
    }

    private static bool TryGetTerminatingPathElement(ReadOnlySpan<char> opPathTail, out ReadOnlySpan<char> propertyName)
    {
        int index = 0;
        int start = 0;
        int length = 0;
        while (index < opPathTail.Length)
        {
            // If we hit a separator, we have a potential problem.
            if (opPathTail[index] == '/')
            {
                if (index == 0)
                {
                    // Phew! we were at the start, we can just skip it
                    start++;
                    index++;
                    continue;
                }
                else
                {
                    // Uh-oh! We found another separator - this wasn't for us after all.
                    propertyName = default;
                    return false;
                }
            }

            length++;
            index++;
        }

        propertyName = opPathTail.Slice(start, length);
        return true;
    }

    private static bool TryApplyAdd(JsonAny node, Add patchOperation, out JsonAny result)
    {
        AddVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyCopy(JsonAny node, Copy patchOperation, out JsonAny result)
    {
        // If the source and the destination match, then we are already done!
        if (patchOperation.Path.Equals(patchOperation.From))
        {
            result = node;
            return true;
        }

        CopyVisitor visitor = new(node, patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyMove(JsonAny node, Move patchOperation, out JsonAny result)
    {
        // If the source and the destination match, then we are already done!
        if (patchOperation.Path.Equals(patchOperation.From))
        {
            result = node;
            return true;
        }

        MoveVisitor visitor = new(node, patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyRemove(JsonAny node, Remove patchOperation, out JsonAny result)
    {
        RemoveVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyReplace(JsonAny node, Replace patchOperation, out JsonAny result)
    {
        ReplaceVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyTest(JsonAny node, Test patchOperation, out JsonAny result)
    {
        result = node;

        // Find the node to test.
        if (node.TryResolvePointer(patchOperation.Path, out JsonAny itemToTest))
        {
            // Verify that the value of the node is the one supplied in the test operation.
            return itemToTest == patchOperation.Value;
        }

        return false;
    }
}
