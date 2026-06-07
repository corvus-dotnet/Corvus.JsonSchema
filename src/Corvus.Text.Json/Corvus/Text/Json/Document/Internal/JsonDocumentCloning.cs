// <copyright file="JsonDocumentCloning.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Shared implementation of <see cref="IJsonDocument.CloneElementAsBuilder(int, JsonWorkspace)"/> for
/// document types that have no cheaper specialisation: it serialises the element and re-parses it into
/// a workspace-owned document, producing a standalone copy with no dependency on the source document.
/// </summary>
internal static class JsonDocumentCloning
{
    /// <summary>
    /// Clones the element at <paramref name="index"/> of <paramref name="document"/> into a new
    /// workspace-owned document by serialising and re-parsing it.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="index">The index of the element to clone.</param>
    /// <param name="workspace">The workspace that will own the clone.</param>
    /// <returns>A workspace-owned builder containing a standalone copy of the element.</returns>
    public static JsonDocumentBuilder<JsonElement.Mutable> CloneElementAsBuilderBySerialization(
        IJsonDocument document,
        int index,
        JsonWorkspace workspace)
    {
        if (workspace is null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
        {
            document.WriteElementTo(index, writer);
            writer.Flush();
        }

        return JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, bufferWriter.WrittenSpan);
    }
}