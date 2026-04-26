// <copyright file="JsonDiffExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// Extension methods for computing a JSON Patch (RFC 6902) that transforms
/// one <see cref="JsonElement"/> into another.
/// </summary>
/// <remarks>
/// <para>
/// The diff operates on semantic JSON equality: object property order is
/// not significant, and all property names within an object are assumed
/// to be unique. If duplicate property names are present, only the last
/// value for each name is considered (matching <see cref="JsonElement"/>
/// comparison semantics).
/// </para>
/// <para>
/// For arrays, the diff compares element-by-element when lengths match.
/// When array lengths differ, the entire array is replaced. This produces
/// correct but not necessarily minimal patches for arrays with insertions
/// or deletions. A future version may implement LCS-based array diffing
/// for more compact patches.
/// </para>
/// </remarks>
public static class JsonDiffExtensions
{
    private const int InitialPathBufferSize = 1024;

    /// <summary>
    /// Creates a <see cref="JsonPatchDocument"/> that transforms
    /// <paramref name="source"/> into <paramref name="target"/>.
    /// </summary>
    /// <param name="source">The original JSON element.</param>
    /// <param name="target">The desired JSON element.</param>
    /// <returns>A <see cref="JsonPatchDocument"/> containing the operations
    /// needed to transform <paramref name="source"/> into <paramref name="target"/>.</returns>
    public static JsonPatchDocument CreatePatch(in JsonElement source, in JsonElement target)
    {
        PatchBuilder patchBuilder = new(true);
        byte[] pathBuffer = ArrayPool<byte>.Shared.Rent(InitialPathBufferSize);

        try
        {
            DiffRecursive(source, target, ref pathBuffer, 0, ref patchBuilder);
            return patchBuilder.GetPatchAndDispose();
        }
        catch
        {
            patchBuilder.Dispose();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(pathBuffer);
        }
    }

    private static void DiffRecursive(
        in JsonElement source,
        in JsonElement target,
        ref byte[] pathBuffer,
        int pathLength,
        ref PatchBuilder patchBuilder)
    {
        if (source == target)
        {
            return;
        }

        if (source.ValueKind != target.ValueKind)
        {
            patchBuilder.Replace(pathBuffer.AsSpan(0, pathLength), target);
            return;
        }

        switch (source.ValueKind)
        {
            case JsonValueKind.Object:
                DiffObject(source, target, ref pathBuffer, pathLength, ref patchBuilder);
                break;

            case JsonValueKind.Array:
                DiffArray(source, target, ref pathBuffer, pathLength, ref patchBuilder);
                break;

            default:
                // Same kind but different value (number, string, etc.)
                patchBuilder.Replace(pathBuffer.AsSpan(0, pathLength), target);
                break;
        }
    }

    private static void DiffObject(
        in JsonElement source,
        in JsonElement target,
        ref byte[] pathBuffer,
        int pathLength,
        ref PatchBuilder patchBuilder)
    {
        // Process properties present in source
        foreach (JsonProperty<JsonElement> sourceProp in source.EnumerateObject())
        {
            using UnescapedUtf8JsonString nameUtf8 = sourceProp.Utf8NameSpan;
            ReadOnlySpan<byte> nameSpan = nameUtf8.Span;
            int childPathLength = AppendSegment(ref pathBuffer, pathLength, nameSpan);

            if (target.TryGetProperty(nameSpan, out JsonElement targetValue))
            {
                // Property exists in both — recurse
                DiffRecursive(sourceProp.Value, targetValue, ref pathBuffer, childPathLength, ref patchBuilder);
            }
            else
            {
                // Property removed
                patchBuilder.Remove(pathBuffer.AsSpan(0, childPathLength));
            }
        }

        // Process properties present only in target
        foreach (JsonProperty<JsonElement> targetProp in target.EnumerateObject())
        {
            using UnescapedUtf8JsonString nameUtf8 = targetProp.Utf8NameSpan;
            ReadOnlySpan<byte> nameSpan = nameUtf8.Span;

            if (!source.TryGetProperty(nameSpan, out _))
            {
                int childPathLength = AppendSegment(ref pathBuffer, pathLength, nameSpan);
                patchBuilder.Add(pathBuffer.AsSpan(0, childPathLength), targetProp.Value);
            }
        }
    }

    private static void DiffArray(
        in JsonElement source,
        in JsonElement target,
        ref byte[] pathBuffer,
        int pathLength,
        ref PatchBuilder patchBuilder)
    {
        int sourceLength = source.GetArrayLength();
        int targetLength = target.GetArrayLength();

        if (sourceLength != targetLength)
        {
            // Different lengths — replace whole array
            patchBuilder.Replace(pathBuffer.AsSpan(0, pathLength), target);
            return;
        }

        // Same length — diff element by element
        for (int i = 0; i < sourceLength; i++)
        {
            int childPathLength = AppendArrayIndex(ref pathBuffer, pathLength, i);
            DiffRecursive(source[i], target[i], ref pathBuffer, childPathLength, ref patchBuilder);
        }
    }

    /// <summary>
    /// Appends a JSON Pointer segment to the path buffer, escaping per RFC 6901.
    /// </summary>
    private static int AppendSegment(ref byte[] buffer, int position, ReadOnlySpan<byte> segment)
    {
        // Calculate escaped length: '/' + each byte (~ and / expand to 2 bytes)
        int escapedLength = 1;
        for (int i = 0; i < segment.Length; i++)
        {
            byte b = segment[i];
            escapedLength += (b == (byte)'~' || b == (byte)'/') ? 2 : 1;
        }

        int needed = position + escapedLength;
        if (needed > buffer.Length)
        {
            GrowBuffer(ref buffer, position, needed);
        }

        buffer[position++] = (byte)'/';
        for (int i = 0; i < segment.Length; i++)
        {
            byte b = segment[i];
            if (b == (byte)'~')
            {
                buffer[position++] = (byte)'~';
                buffer[position++] = (byte)'0';
            }
            else if (b == (byte)'/')
            {
                buffer[position++] = (byte)'~';
                buffer[position++] = (byte)'1';
            }
            else
            {
                buffer[position++] = b;
            }
        }

        return position;
    }

    /// <summary>
    /// Appends an array index as a JSON Pointer segment to the path buffer.
    /// </summary>
    private static int AppendArrayIndex(ref byte[] buffer, int position, int index)
    {
        // '/' + up to 10 digits for a 32-bit int
        int needed = position + 11;
        if (needed > buffer.Length)
        {
            GrowBuffer(ref buffer, position, needed);
        }

        buffer[position++] = (byte)'/';

        if (!Utf8Formatter.TryFormat(index, buffer.AsSpan(position), out int written))
        {
            throw new InvalidOperationException("Failed to format array index.");
        }

        return position + written;
    }

    private static void GrowBuffer(ref byte[] buffer, int contentLength, int minSize)
    {
        int newSize = Math.Max(buffer.Length * 2, minSize);
        byte[] newBuffer = ArrayPool<byte>.Shared.Rent(newSize);
        buffer.AsSpan(0, contentLength).CopyTo(newBuffer);
        ArrayPool<byte>.Shared.Return(buffer);
        buffer = newBuffer;
    }
}