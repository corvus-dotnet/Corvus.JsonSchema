// <copyright file="PatchBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Patch;

/// <summary>
/// A fluent builder for constructing RFC 6902 JSON Patch documents.
/// </summary>
/// <remarks>
/// <para>
/// Create a <see cref="PatchBuilder"/> by calling <see cref="JsonPatchExtensions.BeginPatch"/>,
/// chain operations, then call <see cref="GetPatchAndDispose"/> to
/// finalize and dispose the builder.
/// </para>
/// <para>
/// <code>
/// JsonPatchDocument patch = target.BeginPatch()
///     .Add("/foo/bar"u8, JsonElement.ParseValue("42"))
///     .Remove("/baz"u8)
///     .GetPatchAndDispose();
///
/// bool success = target.TryApplyPatch(in patch);
/// </code>
/// </para>
/// </remarks>
public struct PatchBuilder : IDisposable
{
    private JsonWorkspace _workspace;
    private Utf8JsonWriter _writer;
    private IByteBufferWriter _bufferWriter;
    private bool _disposed;

    internal PatchBuilder(bool initialize)
    {
        _workspace = JsonWorkspace.CreateUnrented();
        _writer = _workspace.RentWriterAndBuffer(256, out _bufferWriter);
        _writer.WriteStartArray();
    }

    /// <summary>
    /// Finalizes the patch document and disposes the builder's resources.
    /// </summary>
    /// <returns>The built <see cref="JsonPatchDocument"/>.</returns>
    /// <remarks>
    /// After calling this method, no further operations can be added and the builder is disposed.
    /// </remarks>
    public JsonPatchDocument GetPatchAndDispose()
    {
        _writer.WriteEndArray();
        _writer.Flush();
        JsonPatchDocument patch = JsonPatchDocument.ParseValue(_bufferWriter.WrittenSpan);
        Dispose();
        return patch;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(ReadOnlySpan<byte> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "add"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(ReadOnlySpan<char> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "add"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(string path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "add"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(ReadOnlySpan<byte> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "remove"u8);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(ReadOnlySpan<char> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "remove"u8);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(string path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "remove"u8);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(ReadOnlySpan<byte> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "replace"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(ReadOnlySpan<char> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "replace"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(string path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "replace"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "move" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="path">The destination JSON Pointer path as UTF-8 bytes.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Move(ReadOnlySpan<byte> from, ReadOnlySpan<byte> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "move"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "move" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Move(ReadOnlySpan<char> from, ReadOnlySpan<char> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "move"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "move" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Move(string from, string path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "move"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "copy" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="path">The destination JSON Pointer path as UTF-8 bytes.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Copy(ReadOnlySpan<byte> from, ReadOnlySpan<byte> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "copy"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "copy" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Copy(ReadOnlySpan<char> from, ReadOnlySpan<char> path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "copy"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "copy" operation to the patch.
    /// </summary>
    /// <param name="from">The source JSON Pointer path.</param>
    /// <param name="path">The destination JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Copy(string from, string path)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "copy"u8);
        _writer.WriteString("from"u8, from);
        _writer.WriteString("path"u8, path);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(ReadOnlySpan<byte> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "test"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(ReadOnlySpan<char> path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "test"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(string path, in JsonElement.Source value)
    {
        _writer.WriteStartObject();
        _writer.WriteString("op"u8, "test"u8);
        _writer.WriteString("path"u8, path);
        _writer.WritePropertyName("value"u8);
        value.WriteTo(_writer);
        _writer.WriteEndObject();
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _workspace.ReturnWriterAndBuffer(_writer, _bufferWriter);
            _workspace.Dispose();
            _disposed = true;
        }
    }
}