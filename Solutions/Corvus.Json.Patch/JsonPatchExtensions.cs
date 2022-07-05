// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Json.Patch.Model;
using Corvus.Json.Visitor;

/// <summary>
/// Extension methods to support JSON Patch [https://jsonpatch.com/].
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1000:Keywords should be spaced correctly", Justification = "new() syntax not supported by current version of StyleCop")]
public static partial class JsonPatchExtensions
{
    private static readonly ReadOnlyMemory<byte> AddAsUtf8 = new byte[] { 0x61, 0x64, 0x64 };
    private static readonly ReadOnlyMemory<byte> CopyAsUtf8 = new byte[] { 0x63, 0x6f, 0x70, 0x79 };
    private static readonly ReadOnlyMemory<byte> MoveAsUtf8 = new byte[] { 0x6d, 0x6f, 0x76, 0x65 };
    private static readonly ReadOnlyMemory<byte> RemoveAsUtf8 = new byte[] { 0x72, 0x65, 0x6d, 0x6f, 0x76, 0x65 };
    private static readonly ReadOnlyMemory<byte> ReplaceAsUtf8 = new byte[] { 0x72, 0x65, 0x70, 0x6c, 0x61, 0x63, 0x65 };
    private static readonly ReadOnlyMemory<byte> TestAsUtf8 = new byte[] { 0x74, 0x65, 0x73, 0x74 };

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
    public static bool TryApplyPatch<T>(this T value, in PatchOperationArray patchOperations, out JsonAny result)
        where T : struct, IJsonValue
    {
        JsonAny current = value.AsAny;

        if (patchOperations.Length == 0)
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

    private static bool TryApplyPatchOperation(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        JsonString op = patchOperation.Op;

        if (patchOperation.HasJsonElement)
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

    private static bool TryGetArrayIndex(in ReadOnlySpan<char> pathSegment, [NotNullWhen(true)] out int index)
    {
        if (pathSegment.Length > 1 && pathSegment[0] == '0')
        {
            index = 0;
            return false;
        }

        return int.TryParse(pathSegment, out index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static JsonAny? FindSourceElement(in JsonAny root, in ReadOnlySpan<char> from)
    {
        // Try to find the node to copy
        if (root.TryResolvePointer(from, out JsonAny sourceElement))
        {
            return sourceElement;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetTerminatingPathElement(in ReadOnlySpan<char> opPathTail, out ReadOnlySpan<char> propertyName)
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyAdd(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        AddVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, (in ReadOnlySpan<char> path, in JsonAny nodeToVisit) => visitor.Visit(path, nodeToVisit), out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyCopy(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(Move.FromUtf8JsonPropertyName.Span, out JsonAny fromAny);
        ReadOnlySpan<char> from = fromAny.AsString;

        // If the source and the destination match, then we are already done!
        if (patchOperation.Path.Equals(from))
        {
            result = node;
            return true;
        }

        JsonAny? source = FindSourceElement(node, from);
        if (!source.HasValue)
        {
            result = node;
            return false;
        }

        CopyVisitor visitor = new(patchOperation, source.Value);

        bool transformed = JsonTransformingVisitor.Visit(node, (in ReadOnlySpan<char> p, in JsonAny n) => visitor.Visit(p, n), out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyMove(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        patchOperation.TryGetProperty(Move.FromUtf8JsonPropertyName.Span, out JsonAny fromAny);
        ReadOnlySpan<char> from = fromAny.AsString;

        // If the source and the destination match, then we are already done!
        if (patchOperation.Path.Equals(from))
        {
            result = node;
            return true;
        }

        JsonAny? source = FindSourceElement(node, from);
        if (!source.HasValue)
        {
            result = node;
            return false;
        }

        MoveVisitor visitor = new(patchOperation, source.Value);
        bool transformed = JsonTransformingVisitor.Visit(node, (in ReadOnlySpan<char> p, in JsonAny n) => visitor.Visit(p, n), out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyRemove(JsonAny node, PatchOperation patchOperation, out JsonAny result)
    {
        RemoveVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, (in ReadOnlySpan<char> p, in JsonAny n) => visitor.Visit(p, n), out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyReplace(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        ReplaceVisitor visitor = new(patchOperation);
        bool transformed = JsonTransformingVisitor.Visit(node, (in ReadOnlySpan<char> p, in JsonAny n) => visitor.Visit(p, n), out JsonAny transformedResult);
        result = transformedResult;
        return transformed;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryApplyTest(in JsonAny node, in PatchOperation patchOperation, out JsonAny result)
    {
        result = node;

        // Find the node to test.
        if (node.TryResolvePointer(patchOperation.Path, out JsonAny itemToTest))
        {
            if (patchOperation.TryGetProperty(Test.ValueUtf8JsonPropertyName.Span, out JsonAny value))
            {
                // Verify that the value of the node is the one supplied in the test operation.
                return itemToTest == value;
            }
        }

        return false;
    }
}
