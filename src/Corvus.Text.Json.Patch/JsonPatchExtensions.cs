// <copyright file="JsonPatchExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Text;
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
    /// Tries to add a value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="value">The value to add.</param>
    /// <returns><see langword="true"/> if the add operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryAdd(this ref JsonElement.Mutable target, ReadOnlySpan<char> path, in JsonElement.Source value)
    {
        byte[]? rented = null;
        int byteCount = GetUtf8ByteCount(path);
        Span<byte> utf8Path = byteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = GetUtf8Bytes(path, utf8Path);
            return TryAdd(ref target, utf8Path.Slice(0, written), in value);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Tries to add a value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="value">The value to add.</param>
    /// <returns><see langword="true"/> if the add operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryAdd(this ref JsonElement.Mutable target, string path, in JsonElement.Source value)
    {
        return TryAdd(ref target, path.AsSpan(), in value);
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

        Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
        if (!TryResolveParent(ref target, path, out JsonElement.Mutable parent, segBuf, out int segLen))
        {
            return false;
        }

        ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

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
            return parent.RemoveProperty(lastSegment);
        }

        return false;
    }

    /// <summary>
    /// Tries to remove the value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <returns><see langword="true"/> if the remove operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryRemove(this ref JsonElement.Mutable target, ReadOnlySpan<char> path)
    {
        byte[]? rented = null;
        int byteCount = GetUtf8ByteCount(path);
        Span<byte> utf8Path = byteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = GetUtf8Bytes(path, utf8Path);
            return TryRemove(ref target, utf8Path.Slice(0, written));
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Tries to remove the value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <returns><see langword="true"/> if the remove operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryRemove(this ref JsonElement.Mutable target, string path)
    {
        return TryRemove(ref target, path.AsSpan());
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

        Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
        if (!TryResolveParent(ref target, path, out JsonElement.Mutable parent, segBuf, out int segLen))
        {
            return false;
        }

        ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

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
            if (!parent.TryGetProperty(lastSegment, out _))
            {
                return false;
            }

            parent.SetProperty(lastSegment, in value);
            return true;
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
    public static bool TryReplace(this ref JsonElement.Mutable target, ReadOnlySpan<char> path, in JsonElement.Source value)
    {
        byte[]? rented = null;
        int byteCount = GetUtf8ByteCount(path);
        Span<byte> utf8Path = byteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = GetUtf8Bytes(path, utf8Path);
            return TryReplace(ref target, utf8Path.Slice(0, written), in value);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Tries to replace the value at the specified JSON Pointer path.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="value">The replacement value.</param>
    /// <returns><see langword="true"/> if the replace operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryReplace(this ref JsonElement.Mutable target, string path, in JsonElement.Source value)
    {
        return TryReplace(ref target, path.AsSpan(), in value);
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

        // Cache the source value into a rented buffer before removal invalidates it.
        IMutableJsonDocument mutableDoc = (IMutableJsonDocument)GetParentDocument(in target);
        Utf8JsonWriter writer = mutableDoc.Workspace.RentWriterAndBuffer(256, out IByteBufferWriter bufferWriter);
        try
        {
            sourceValue.WriteTo(writer);
            writer.Flush();
            ReadOnlySpan<byte> cachedValue = bufferWriter.WrittenSpan;

            // Remove from source location.
            Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
            if (!TryResolveParent(ref target, from, out JsonElement.Mutable fromParent, segBuf, out int segLen))
            {
                return false;
            }

            ReadOnlySpan<byte> fromSegment = segBuf.Slice(0, segLen);

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
                if (!fromParent.RemoveProperty(fromSegment))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            // Add to destination using the cached value.
            JsonElement.Source source = JsonElement.ParseValue(cachedValue);
            return TryAddValueFromSpan(ref target, path, in source);
        }
        finally
        {
            mutableDoc.Workspace.ReturnWriterAndBuffer(writer, bufferWriter);
        }
    }

    /// <summary>
    /// Tries to move a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the move operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryMove(this ref JsonElement.Mutable target, ReadOnlySpan<char> from, ReadOnlySpan<char> path)
    {
        byte[]? rentedFrom = null;
        int fromByteCount = GetUtf8ByteCount(from);
        Span<byte> utf8From = fromByteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rentedFrom = ArrayPool<byte>.Shared.Rent(fromByteCount));
        try
        {
            int fromWritten = GetUtf8Bytes(from, utf8From);

            byte[]? rentedPath = null;
            int pathByteCount = GetUtf8ByteCount(path);
            Span<byte> utf8Path = pathByteCount <= StackallocByteThreshold
                ? stackalloc byte[StackallocByteThreshold]
                : (rentedPath = ArrayPool<byte>.Shared.Rent(pathByteCount));
            try
            {
                int pathWritten = GetUtf8Bytes(path, utf8Path);
                return TryMove(ref target, utf8From.Slice(0, fromWritten), utf8Path.Slice(0, pathWritten));
            }
            finally
            {
                if (rentedPath != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedPath);
                }
            }
        }
        finally
        {
            if (rentedFrom != null)
            {
                ArrayPool<byte>.Shared.Return(rentedFrom);
            }
        }
    }

    /// <summary>
    /// Tries to move a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the move operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryMove(this ref JsonElement.Mutable target, string from, string path)
    {
        return TryMove(ref target, from.AsSpan(), path.AsSpan());
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

        JsonElement.Source source = sourceValue;
        return TryAddValueFromSpan(ref target, path, in source);
    }

    /// <summary>
    /// Tries to copy a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the copy operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCopy(this ref JsonElement.Mutable target, ReadOnlySpan<char> from, ReadOnlySpan<char> path)
    {
        byte[]? rentedFrom = null;
        int fromByteCount = GetUtf8ByteCount(from);
        Span<byte> utf8From = fromByteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rentedFrom = ArrayPool<byte>.Shared.Rent(fromByteCount));
        try
        {
            int fromWritten = GetUtf8Bytes(from, utf8From);

            byte[]? rentedPath = null;
            int pathByteCount = GetUtf8ByteCount(path);
            Span<byte> utf8Path = pathByteCount <= StackallocByteThreshold
                ? stackalloc byte[StackallocByteThreshold]
                : (rentedPath = ArrayPool<byte>.Shared.Rent(pathByteCount));
            try
            {
                int pathWritten = GetUtf8Bytes(path, utf8Path);
                return TryCopy(ref target, utf8From.Slice(0, fromWritten), utf8Path.Slice(0, pathWritten));
            }
            finally
            {
                if (rentedPath != null)
                {
                    ArrayPool<byte>.Shared.Return(rentedPath);
                }
            }
        }
        finally
        {
            if (rentedFrom != null)
            {
                ArrayPool<byte>.Shared.Return(rentedFrom);
            }
        }
    }

    /// <summary>
    /// Tries to copy a value from one location to another.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns><see langword="true"/> if the copy operation was applied successfully; otherwise, <see langword="false"/>.</returns>
    public static bool TryCopy(this ref JsonElement.Mutable target, string from, string path)
    {
        return TryCopy(ref target, from.AsSpan(), path.AsSpan());
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

    /// <summary>
    /// Tests whether the value at the specified JSON Pointer path equals the expected value.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><see langword="true"/> if the value at the path equals the expected value; otherwise, <see langword="false"/>.</returns>
    public static bool TryTest(this ref JsonElement.Mutable target, ReadOnlySpan<char> path, in JsonElement expected)
    {
        byte[]? rented = null;
        int byteCount = GetUtf8ByteCount(path);
        Span<byte> utf8Path = byteCount <= StackallocByteThreshold
            ? stackalloc byte[StackallocByteThreshold]
            : (rented = ArrayPool<byte>.Shared.Rent(byteCount));
        try
        {
            int written = GetUtf8Bytes(path, utf8Path);
            return TryTest(ref target, utf8Path.Slice(0, written), in expected);
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>
    /// Tests whether the value at the specified JSON Pointer path equals the expected value.
    /// </summary>
    /// <param name="target">The mutable root element.</param>
    /// <param name="path">The JSON Pointer path (e.g. <c>/foo/bar</c>).</param>
    /// <param name="expected">The expected value.</param>
    /// <returns><see langword="true"/> if the value at the path equals the expected value; otherwise, <see langword="false"/>.</returns>
    public static bool TryTest(this ref JsonElement.Mutable target, string path, in JsonElement expected)
    {
        return TryTest(ref target, path.AsSpan(), in expected);
    }

    private static bool TryApplyAdd(ref JsonElement.Mutable target, in JsonPatchDocument.AddOperation op)
    {
        UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
        try
        {
            ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;
            JsonElement.Source value = op.Value;

            if (pathUtf8.Length == 0)
            {
                ReplaceRoot(ref target, in value);
                return true;
            }

            return TryAddValueFromSpan(ref target, pathUtf8, in value);
        }
        finally
        {
            utf8Path.Dispose();
        }
    }

    private static bool TryApplyRemove(ref JsonElement.Mutable target, in JsonPatchDocument.RemoveOperation op)
    {
        UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
        try
        {
            ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;

            if (pathUtf8.Length == 0)
            {
                return false;
            }

            Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
            if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, segBuf, out int segLen))
            {
                return false;
            }

            ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

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
                return parent.RemoveProperty(lastSegment);
            }

            return false;
        }
        finally
        {
            utf8Path.Dispose();
        }
    }

    private static bool TryApplyReplace(ref JsonElement.Mutable target, in JsonPatchDocument.ReplaceOperation op)
    {
        UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
        try
        {
            ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;
            JsonElement.Source value = op.Value;

            if (pathUtf8.Length == 0)
            {
                ReplaceRoot(ref target, in value);
                return true;
            }

            Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
            if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, segBuf, out int segLen))
            {
                return false;
            }

            ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

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
                return parent.TryReplaceProperty(lastSegment, in value);
            }

            return false;
        }
        finally
        {
            utf8Path.Dispose();
        }
    }

    private static bool TryApplyMove(ref JsonElement.Mutable target, in JsonPatchDocument.MoveOperation op)
    {
        UnescapedUtf8JsonString utf8From = GetPathUtf8String(op.FromValue);
        try
        {
            UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
            try
            {
                ReadOnlySpan<byte> fromUtf8 = utf8From.Span;
                ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;

                // Cannot move root.
                if (fromUtf8.Length == 0)
                {
                    return false;
                }

                // Cannot move to root (would need full doc replacement).
                if (pathUtf8.Length == 0)
                {
                    return false;
                }

                // Resolve source parent and last segment.
                Span<byte> fromSegBuf = stackalloc byte[StackallocByteThreshold];
                if (!TryResolveParent(ref target, fromUtf8, out JsonElement.Mutable fromParent, fromSegBuf, out int fromSegLen))
                {
                    return false;
                }

                ReadOnlySpan<byte> fromSegment = fromSegBuf.Slice(0, fromSegLen);

                // Resolve dest parent and last segment.
                Span<byte> destSegBuf = stackalloc byte[StackallocByteThreshold];
                if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable destParent, destSegBuf, out int destSegLen))
                {
                    return false;
                }

                ReadOnlySpan<byte> destSegment = destSegBuf.Slice(0, destSegLen);

                // Dispatch based on source and destination container types.
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

                    if (destParent.ValueKind == JsonValueKind.Array)
                    {
                        if (IsAppendToken(destSegment))
                        {
                            GetMutableDoc(in fromParent).MoveItemToArrayEnd(GetDocIndex(in fromParent), fromIndex, GetDocIndex(in destParent));
                            return true;
                        }

                        if (!TryParseArrayIndex(destSegment, out int destIndex))
                        {
                            return false;
                        }

                        if (destIndex < 0 || destIndex > destParent.GetArrayLength())
                        {
                            return false;
                        }

                        GetMutableDoc(in fromParent).MoveItemToArray(GetDocIndex(in fromParent), fromIndex, GetDocIndex(in destParent), destIndex);
                        return true;
                    }

                    if (destParent.ValueKind == JsonValueKind.Object)
                    {
                        GetMutableDoc(in fromParent).MoveItemToProperty(GetDocIndex(in fromParent), fromIndex, GetDocIndex(in destParent), destSegment);
                        return true;
                    }

                    return false;
                }

                if (fromParent.ValueKind == JsonValueKind.Object)
                {
                    if (destParent.ValueKind == JsonValueKind.Array)
                    {
                        if (IsAppendToken(destSegment))
                        {
                            return GetMutableDoc(in fromParent).MovePropertyToArrayEnd(GetDocIndex(in fromParent), fromSegment, GetDocIndex(in destParent));
                        }

                        if (!TryParseArrayIndex(destSegment, out int destIndex))
                        {
                            return false;
                        }

                        if (destIndex < 0 || destIndex > destParent.GetArrayLength())
                        {
                            return false;
                        }

                        return GetMutableDoc(in fromParent).MovePropertyToArray(GetDocIndex(in fromParent), fromSegment, GetDocIndex(in destParent), destIndex);
                    }

                    if (destParent.ValueKind == JsonValueKind.Object)
                    {
                        return GetMutableDoc(in fromParent).MovePropertyToProperty(GetDocIndex(in fromParent), fromSegment, GetDocIndex(in destParent), destSegment);
                    }

                    return false;
                }

                return false;
            }
            finally
            {
                utf8Path.Dispose();
            }
        }
        finally
        {
            utf8From.Dispose();
        }
    }

    private static bool TryApplyCopy(ref JsonElement.Mutable target, in JsonPatchDocument.CopyOperation op)
    {
        UnescapedUtf8JsonString utf8From = GetPathUtf8String(op.FromValue);
        try
        {
            UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
            try
            {
                ReadOnlySpan<byte> fromUtf8 = utf8From.Span;
                ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;

                if (!TryResolvePointer(ref target, fromUtf8, out JsonElement.Mutable sourceValue))
                {
                    return false;
                }

                return TryCopyValueFromSpan(ref target, pathUtf8, in sourceValue);
            }
            finally
            {
                utf8Path.Dispose();
            }
        }
        finally
        {
            utf8From.Dispose();
        }
    }

    private static bool TryApplyTest(ref JsonElement.Mutable target, in JsonPatchDocument.TestOperation op)
    {
        UnescapedUtf8JsonString utf8Path = GetPathUtf8String(op.Path);
        try
        {
            ReadOnlySpan<byte> pathUtf8 = utf8Path.Span;

            if (!TryResolvePointer(ref target, pathUtf8, out JsonElement.Mutable resolved))
            {
                return false;
            }

            return resolved.Equals(op.Value);
        }
        finally
        {
            utf8Path.Dispose();
        }
    }

    private static bool TryAddValueFromSpan(ref JsonElement.Mutable target, ReadOnlySpan<byte> pathUtf8, in JsonElement.Source source)
    {
        if (pathUtf8.Length == 0)
        {
            ReplaceRoot(ref target, in source);
            return true;
        }

        Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, segBuf, out int segLen))
        {
            return false;
        }

        ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

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
            parent.SetProperty(lastSegment, in source);
            return true;
        }

        return false;
    }

    private static bool TryCopyValueFromSpan(ref JsonElement.Mutable target, ReadOnlySpan<byte> pathUtf8, in JsonElement.Mutable source)
    {
        if (pathUtf8.Length == 0)
        {
            // Replace root — fall back to Source path since we're replacing the entire document.
            JsonElement.Source sourceValue = source;
            ReplaceRoot(ref target, in sourceValue);
            return true;
        }

        Span<byte> segBuf = stackalloc byte[StackallocByteThreshold];
        if (!TryResolveParent(ref target, pathUtf8, out JsonElement.Mutable parent, segBuf, out int segLen))
        {
            return false;
        }

        ReadOnlySpan<byte> lastSegment = segBuf.Slice(0, segLen);

        if (parent.ValueKind == JsonValueKind.Array)
        {
            if (IsAppendToken(lastSegment))
            {
                GetMutableDoc(in parent).CopyValueToArrayEnd(GetDocIndex(in source), GetDocIndex(in parent));
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

            GetMutableDoc(in parent).CopyValueToArrayIndex(GetDocIndex(in source), GetDocIndex(in parent), index);
            return true;
        }

        if (parent.ValueKind == JsonValueKind.Object)
        {
            GetMutableDoc(in parent).CopyValueToProperty(GetDocIndex(in source), GetDocIndex(in parent), lastSegment);
            return true;
        }

        return false;
    }

    private static UnescapedUtf8JsonString GetPathUtf8String<TPath>(in TPath path)
        where TPath : struct, IJsonElement<TPath>
    {
        return path.ParentDocument.GetUtf8JsonString(path.ParentDocumentIndex, JsonTokenType.String);
    }

    private static IJsonDocument GetParentDocument<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return element.ParentDocument;
    }

    private static IMutableJsonDocument GetMutableDoc<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return (IMutableJsonDocument)element.ParentDocument;
    }

    private static int GetDocIndex<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return element.ParentDocumentIndex;
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
        Span<byte> lastSegmentBuffer,
        out int lastSegmentLength)
    {
        parent = default;
        lastSegmentLength = 0;

        int lastSlash = pointerUtf8.LastIndexOf((byte)'/');
        if (lastSlash < 0)
        {
            return false;
        }

        ReadOnlySpan<byte> parentPath = pointerUtf8.Slice(0, lastSlash);
        ReadOnlySpan<byte> encodedLastSegment = pointerUtf8.Slice(lastSlash + 1);

        lastSegmentLength = Utf8JsonPointer.DecodeSegment(encodedLastSegment, lastSegmentBuffer);

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

    private static int GetUtf8ByteCount(ReadOnlySpan<char> chars)
    {
#if NET
        return Encoding.UTF8.GetByteCount(chars);
#else
        if (chars.IsEmpty)
        {
            return 0;
        }

        unsafe
        {
            fixed (char* ptr = chars)
            {
                return Encoding.UTF8.GetByteCount(ptr, chars.Length);
            }
        }
#endif
    }

    private static int GetUtf8Bytes(ReadOnlySpan<char> chars, Span<byte> destination)
    {
#if NET
        return Encoding.UTF8.GetBytes(chars, destination);
#else
        if (chars.IsEmpty)
        {
            return 0;
        }

        unsafe
        {
            fixed (char* srcPtr = chars)
            fixed (byte* destPtr = destination)
            {
                return Encoding.UTF8.GetBytes(srcPtr, chars.Length, destPtr, destination.Length);
            }
        }
#endif
    }
}