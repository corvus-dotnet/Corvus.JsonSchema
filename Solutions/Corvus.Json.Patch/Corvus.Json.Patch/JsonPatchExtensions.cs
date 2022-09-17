// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

namespace Corvus.Json.Patch;

/// <summary>
/// Extension methods to support JSON Patch [https://jsonpatch.com/].
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
public static partial class JsonPatchExtensions
{
    private static readonly ReadOnlyMemory<byte> AddAsUtf8 = "add"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> CopyAsUtf8 = "copy"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> MoveAsUtf8 = "move"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> RemoveAsUtf8 = "remove"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> ReplaceAsUtf8 = "replace"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> TestAsUtf8 = "test"u8.ToArray();

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

    private static bool TryApplyPatchOperation(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        JsonString op = patchOperation.Op;

        if (patchOperation.HasJsonElementBacking)
        {
            if (op.EqualsUtf8Bytes(AddAsUtf8.Span))
            {
                return TryApplyAdd(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(CopyAsUtf8.Span))
            {
                return TryApplyCopy(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(MoveAsUtf8.Span))
            {
                return TryApplyMove(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(RemoveAsUtf8.Span))
            {
                return TryApplyRemove(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(ReplaceAsUtf8.Span))
            {
                return TryApplyReplace(node, patchOperation, out result);
            }

            if (op.EqualsUtf8Bytes(TestAsUtf8.Span))
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
        patchOperation.TryGetProperty(JsonPatchDocument.AddEntity.PathUtf8JsonPropertyName.Span, out JsonAny pathAny);
        patchOperation.TryGetProperty(JsonPatchDocument.AddEntity.ValueUtf8JsonPropertyName.Span, out JsonAny value);
        string path = pathAny;
        if (path.Length == 0)
        {
            result = value;
            return true;
        }

        AddVisitor visitor = new(path, value);

        bool transformed = node.Visit(visitor.Visit, out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    private static bool TryApplyCopy(in JsonAny node, in JsonPatchDocument.PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(JsonPatchDocument.Copy.FromValueUtf8JsonPropertyName.Span, out JsonAny fromAny);
        patchOperation.TryGetProperty(JsonPatchDocument.Copy.PathUtf8JsonPropertyName.Span, out JsonAny pathAny);
        string from = fromAny;
        string path = pathAny;

        if (from.Equals(path))
        {
            result = node;
            return true;
        }

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
        patchOperation.TryGetProperty(JsonPatchDocument.Move.FromValueUtf8JsonPropertyName.Span, out JsonAny fromAny);
        patchOperation.TryGetProperty(JsonPatchDocument.Move.PathUtf8JsonPropertyName.Span, out JsonAny pathAny);
        string from = fromAny;
        string path = pathAny;

        if (from.Equals(path))
        {
            result = node;
            return true;
        }

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
            if (patchOperation.TryGetProperty(JsonPatchDocument.Test.ValueUtf8JsonPropertyName.Span, out JsonAny value))
            {
                // Verify that the value of the node is the one supplied in the test operation.
                return itemToTest.Equals(value);
            }
        }

        return false;
    }
}