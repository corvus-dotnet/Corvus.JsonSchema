// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// Provides extension methods for applying RFC 6902 JSON Patch operations.
/// </summary>
public static class JsonPatchExtensions
{
    private const int StackallocByteThreshold = 256;

    /// <summary>
    /// Tries to apply an RFC 6902 JSON Patch document to a mutable JSON element.
    /// </summary>
    /// <param name="target">The mutable root element to patch.</param>
    /// <param name="patch">The patch document to apply.</param>
    /// <returns><see langword="true"/> if all operations were applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryApplyPatch(this ref JsonElement.Mutable target, in JsonPatchDocument patch)
    {
        foreach (JsonPatchDocument.PatchOperation operation in patch.EnumerateArray())
        {
            if (operation.TryGetAsAddOperation(out JsonPatchDocument.AddOperation addOp))
            {
                if (!TryApplyAdd(ref target, in addOp))
                {
                    return false;
                }
            }
            else if (operation.TryGetAsRemoveOperation(out JsonPatchDocument.RemoveOperation removeOp))
            {
                if (!TryApplyRemove(ref target, in removeOp))
                {
                    return false;
                }
            }
            else if (operation.TryGetAsReplaceOperation(out JsonPatchDocument.ReplaceOperation replaceOp))
            {
                if (!TryApplyReplace(ref target, in replaceOp))
                {
                    return false;
                }
            }
            else if (operation.TryGetAsMoveOperation(out JsonPatchDocument.MoveOperation moveOp))
            {
                if (!TryApplyMove(ref target, in moveOp))
                {
                    return false;
                }
            }
            else if (operation.TryGetAsCopyOperation(out JsonPatchDocument.CopyOperation copyOp))
            {
                if (!TryApplyCopy(ref target, in copyOp))
                {
                    return false;
                }
            }
            else if (operation.TryGetAsTestOperation(out JsonPatchDocument.TestOperation testOp))
            {
                if (!TryApplyTest(ref target, in testOp))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Begins building a JSON Patch document that can be applied to the target.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <returns>A new <see cref="PatchBuilder"/> for fluent patch construction.</returns>
    public static PatchBuilder BeginPatch(this in JsonElement.Mutable target)
    {
        return new PatchBuilder(true);
    }

    /// <summary>
    /// Tries to add a value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="value">The value to add.</param>
    /// <returns><see langword="true"/> if the add operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryAdd(this ref JsonElement.Mutable target, ReadOnlySpan<byte> path, in JsonElement.Source value)
    {
        if (path.Length == 0)
        {
            ReplaceRoot(ref target, in value);
            return true;
        }

        return TryAddValueFromSpan(ref target, path, in value);
    }

    /// <summary>
    /// Tries to remove the value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <returns><see langword="true"/> if the remove operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryRemove(this ref JsonElement.Mutable target, ReadOnlySpan<byte> path)
    {
        if (path.Length == 0)
        {
            return false;
        }

        if (!TryResolveParent(ref target, path, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index >= parent.GetArrayLength())
            {
                return false;
            }

            parent.RemoveAt(index);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            return parent.RemoveProperty((ReadOnlySpan<byte>)lastSegment);
        }

        return false;
    }

    /// <summary>
    /// Tries to replace the value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="value">The replacement value.</param>
    /// <returns><see langword="true"/> if the replace operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryReplace(this ref JsonElement.Mutable target, ReadOnlySpan<byte> path, in JsonElement.Source value)
    {
        if (path.Length == 0)
        {
            ReplaceRoot(ref target, in value);
            return true;
        }

        if (!TryResolveParent(ref target, path, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index >= parent.GetArrayLength())
            {
                return false;
            }

            parent.SetItem(index, in value);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            if (!parent.TryGetProperty((ReadOnlySpan<byte>)lastSegment, out _))
            {
                return false;
            }

            parent.SetProperty((ReadOnlySpan<byte>)lastSegment, in value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to move a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the move operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryMove(this ref JsonElement.Mutable target, ReadOnlySpan<byte> from, ReadOnlySpan<byte> path)
    {
        if (from.Length == 0)
        {
            return false;
        }

        if (!TryResolvePointer(ref target, from, out JsonElement.Mutable sourceValue))
        {
            return false;
        }

        JsonElement cloned = sourceValue.Clone();

        if (!TryResolveParent(ref target, from, out JsonElement.Mutable fromParent, out byte[] fromSegment))
        {
            return false;
        }

        if (fromParent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(fromSegment, out int fromIndex))
            {
                return false;
            }

            if (fromIndex < 0 || fromIndex >= fromParent.GetArrayLength())
            {
                return false;
            }

            fromParent.RemoveAt(fromIndex);
        }
        else if (fromParent.ValueKind == JsonValueKind.Object)
        {
            if (!fromParent.RemoveProperty((ReadOnlySpan<byte>)fromSegment))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        return TryAddValue(ref target, path, cloned);
    }

    /// <summary>
    /// Tries to copy a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the copy operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCopy(this ref JsonElement.Mutable target, ReadOnlySpan<byte> from, ReadOnlySpan<byte> path)
    {
        if (!TryResolvePointer(ref target, from, out JsonElement.Mutable sourceValue))
        {
            return false;
        }

        JsonElement cloned = sourceValue.Clone();
        return TryAddValue(ref target, path, cloned);
    }

    /// <summary>
    /// Tests whether the value at the specified JSON Pointer path equals the expected value.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><see langword="true"/> if the value at the path equals the expected value; otherwise, <see langword="false"/>.</returns>
    public static bool TryTest(this ref JsonElement.Mutable target, ReadOnlySpan<byte> path, in JsonElement expected)
    {
        if (!TryResolvePointer(ref target, path, out JsonElement.Mutable resolved))
        {
            return false;
        }

        return resolved.Equals(expected);
    }

    private static bool TryApplyAdd(ref JsonElement.Mutable target, in JsonPatchDocument.AddOperation op)
    {
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);
        JsonElement.Source value = op.Value;

        if (pathUtf8.Length == 0)
        {
            ReplaceRoot(ref target, in value);
            return true;
        }

        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (IsAppendToken(lastSegment))
            {
                parent.AddItem(in value);
                return true;
            }

            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index > parent.GetArrayLength())
            {
                return false;
            }

            parent.InsertItem(index, in value);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            parent.SetProperty((ReadOnlySpan<byte>)lastSegment, in value);
            return true;
        }

        return false;
    }

    private static bool TryApplyRemove(ref JsonElement.Mutable target, in JsonPatchDocument.RemoveOperation op)
    {
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);

        if (pathUtf8.Length == 0)
        {
            // Cannot remove the root.
            return false;
        }

        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index >= parent.GetArrayLength())
            {
                return false;
            }

            parent.RemoveAt(index);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            return parent.RemoveProperty((ReadOnlySpan<byte>)lastSegment);
        }

        return false;
    }

    private static bool TryApplyReplace(ref JsonElement.Mutable target, in JsonPatchDocument.ReplaceOperation op)
    {
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);
        JsonElement.Source value = op.Value;

        if (pathUtf8.Length == 0)
        {
            ReplaceRoot(ref target, in value);
            return true;
        }

        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index >= parent.GetArrayLength())
            {
                return false;
            }

            parent.SetItem(index, in value);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            // Replace requires the property to already exist.
            if (!parent.TryGetProperty((ReadOnlySpan<byte>)lastSegment, out _))
            {
                return false;
            }

            parent.SetProperty((ReadOnlySpan<byte>)lastSegment, in value);
            return true;
        }

        return false;
    }

    private static bool TryApplyMove(ref JsonElement.Mutable target, in JsonPatchDocument.MoveOperation op)
    {
        byte[] fromUtf8 = GetPathUtf8Bytes(op.FromValue);
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);

        // Resolve the source value.
        if (!TryResolvePointer(ref target, fromUtf8, out JsonElement.Mutable sourceValue))
        {
            return false;
        }

        // Clone the value before removing the source (removal invalidates the element).
        JsonElement cloned = sourceValue.Clone();

        // Remove from source location.
        if (fromUtf8.Length == 0)
        {
            return false;
        }

        if (!TryResolveParent(ref target, fromUtf8, out JsonElement.Mutable fromParent, out byte[] fromSegment))
        {
            return false;
        }

        if (fromParent.ValueKind == JsonValueKind.Array)
        {
            if (!TryParseArrayIndex(fromSegment, out int fromIndex))
            {
                return false;
            }

            if (fromIndex < 0 || fromIndex >= fromParent.GetArrayLength())
            {
                return false;
            }

            fromParent.RemoveAt(fromIndex);
        }
        else if (fromParent.ValueKind == JsonValueKind.Object)
        {
            if (!fromParent.RemoveProperty((ReadOnlySpan<byte>)fromSegment))
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        // Add to destination (same logic as add).
        return TryAddValue(ref target, pathUtf8, cloned);
    }

    private static bool TryApplyCopy(ref JsonElement.Mutable target, in JsonPatchDocument.CopyOperation op)
    {
        byte[] fromUtf8 = GetPathUtf8Bytes(op.FromValue);
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);

        // Resolve the source value and clone it.
        if (!TryResolvePointer(ref target, fromUtf8, out JsonElement.Mutable sourceValue))
        {
            return false;
        }

        JsonElement cloned = sourceValue.Clone();

        // Add the cloned value to the destination (same logic as add).
        return TryAddValue(ref target, pathUtf8, cloned);
    }

    private static bool TryApplyTest(ref JsonElement.Mutable target, in JsonPatchDocument.TestOperation op)
    {
        byte[] pathUtf8 = GetPathUtf8Bytes(op.Path);

        if (!TryResolvePointer(ref target, pathUtf8, out JsonElement.Mutable resolved))
        {
            return false;
        }

        return resolved.Equals(op.Value);
    }

    private static bool TryAddValue(ref JsonElement.Mutable target, byte[] pathUtf8, in JsonElement value)
    {
        JsonElement.Source source = value;
        return TryAddValueFromSpan(ref target, (ReadOnlySpan<byte>)pathUtf8, in source);
    }

    private static bool TryAddValue(ref JsonElement.Mutable target, ReadOnlySpan<byte> pathUtf8, in JsonElement value)
    {
        JsonElement.Source source = value;
        return TryAddValueFromSpan(ref target, pathUtf8, in source);
    }

    private static bool TryAddValueFromSpan(ref JsonElement.Mutable target, ReadOnlySpan<byte> pathUtf8, in JsonElement.Source source)
    {
        if (pathUtf8.Length == 0)
        {
            ReplaceRoot(ref target, in source);
            return true;
        }

        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, out byte[] lastSegment))
        {
            return false;
        }

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (IsAppendToken(lastSegment))
            {
                parent.AddItem(in source);
                return true;
            }

            if (!TryParseArrayIndex(lastSegment, out int index))
            {
                return false;
            }

            if (index < 0 || index > parent.GetArrayLength())
            {
                return false;
            }

            parent.InsertItem(index, in source);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            parent.SetProperty((ReadOnlySpan<byte>)lastSegment, in source);
            return true;
        }

        return false;
    }

    private static byte[] GetPathUtf8Bytes<TPath>(in TPath path)
        where TPath : struct, IJsonElement<TPath>
    {
        UnescapedUtf8JsonString utf8 = path.ParentDocument.GetUtf8JsonString(path.ParentDocumentIndex, JsonTokenType.String);
        try
        {
            return utf8.Span.ToArray();
        }
        finally
        {
            utf8.Dispose();
        }
    }

    private static IJsonDocument GetParentDocument<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return element.ParentDocument;
    }

    private static bool TryResolvePointer(ref JsonElement.Mutable root, ReadOnlySpan<byte> pointerUtf8, out JsonElement.Mutable result)
    {
        if (pointerUtf8.Length == 0)
        {
            result = root;
            return true;
        }

        if (!Utf8JsonPointer.TryCreateJsonPointer(pointerUtf8, out Utf8JsonPointer pointer))
        {
            result = default;
            return false;
        }

        return pointer.TryResolve(in root, out result);
    }

    private static bool TryResolveParent(
        ref JsonElement.Mutable root,
        ReadOnlySpan<byte> pointerUtf8,
        out JsonElement.Mutable parent,
        out byte[] lastSegment)
    {
        parent = default;
        lastSegment = Array.Empty<byte>();

        int lastSlash = pointerUtf8.LastIndexOf((byte)'/');
        if (lastSlash < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> parentPath = pointerUtf8.Slice(0, lastSlash);
        ReadOnlySpan<byte> encodedLastSegment = pointerUtf8.Slice(lastSlash + 1);

        // Decode the last segment.
        byte[]? rentedBuffer = null;
        int segmentLength = encodedLastSegment.Length;

        Span<byte> decodedBuffer = segmentLength <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rentedBuffer = ArrayPool<byte>.Shared.Rent(segmentLength));

        try
        {
            int decodedLength = Utf8JsonPointer.DecodeSegment(encodedLastSegment, decodedBuffer);
            lastSegment = decodedBuffer.Slice(0, decodedLength).ToArray();
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }

        // Resolve the parent.
        if (parentPath.Length == 0)
        {
            parent = root;
            return true;
        }

        return TryResolvePointer(ref root, parentPath, out parent);
    }

    private static void ReplaceRoot(ref JsonElement.Mutable target, in JsonElement.Source value)
    {
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)GetParentDocument(in target);
        ComplexValueBuilder cvb = ComplexValueBuilder.Create(mutableDoc, 30);
        value.AddAsItem(ref cvb);
        mutableDoc.ReplaceRootAndDispose(ref cvb);

        // Re-acquire the root element after replacement.
        target = ((JsonDocumentBuilder<JsonElement.Mutable>)mutableDoc).RootElement;
    }

    private static bool IsAppendToken(ReadOnlySpan<byte> segment)
    {
        return segment.Length == 1 && segment[0] == (byte)'-';
    }

    private static bool TryParseArrayIndex(ReadOnlySpan<byte> segment, out int index)
    {
        index = -1;

        if (segment.Length == 0)
        {
            return false;
        }

        // Leading zeros are invalid per RFC 6901 (except "0" itself).
        if (segment.Length > 1 && segment[0] == (byte)'0')
        {
            return false;
        }

        // All bytes must be consumed — reject inputs like "1e0".
        return Utf8Parser.TryParse(segment, out index, out int bytesConsumed)
            && bytesConsumed == segment.Length
            && index >= 0;
    }
}