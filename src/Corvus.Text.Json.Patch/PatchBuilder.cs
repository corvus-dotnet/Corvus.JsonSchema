// <copyright file="PatchBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Patch;

/// <summary>
/// A fluent builder for constructing RFC 6902 JSON Patch documents.
/// </summary>
/// <remarks>
/// <para>
/// Create a <see cref="PatchBuilder"/> by calling <see cref="JsonPatchExtensions.BeginPatch"/>,
/// chain operations, then call <see cref="GetPatchAndDispose"/> to
/// finalize the builder.
/// </para>
/// <para>
/// The returned <see cref="JsonPatchDocument"/> is backed by the
/// <see cref="JsonWorkspace"/> passed to the constructor. The caller
/// must keep the workspace alive for the lifetime of the patch.
/// </para>
/// <para>
/// <code>
/// JsonPatchDocument patch = target.BeginPatch(workspace)
///     .Add("/foo/bar"u8, JsonElement.ParseValue("42"))
///     .Remove("/baz"u8)
///     .GetPatchAndDispose();
///
/// bool success = target.TryApplyPatch(in patch);
/// </code>
/// </para>
/// </remarks>
public ref struct PatchBuilder
#if NET
    : IDisposable
#endif
{
    private JsonDocumentBuilder<JsonPatchDocument.Mutable> _builder;
    private ComplexValueBuilder _cvb;
    private bool _disposed;

    internal PatchBuilder(JsonWorkspace workspace)
    {
        _builder = workspace.CreateBuilder<JsonPatchDocument.Mutable>(-1, 8192);
        _cvb = ComplexValueBuilder.Create(_builder, 64);
        _cvb.StartArray();
    }

    /// <summary>
    /// Finalizes the patch document and disposes the builder's resources.
    /// </summary>
    /// <returns>The built <see cref="JsonPatchDocument"/>, backed by the workspace.</returns>
    /// <remarks>
    /// After calling this method, no further operations can be added and the builder is disposed.
    /// </remarks>
    public JsonPatchDocument GetPatchAndDispose()
    {
        _cvb.EndArray();
        ((IMutableJsonDocument)_builder).SetAndDispose(ref _cvb);
        _disposed = true;
        return _builder.RootElement;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(ReadOnlySpan<byte> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "add"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(ReadOnlySpan<char> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "add"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds an "add" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The value to add.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Add(string path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "add"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(ReadOnlySpan<byte> path)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "remove"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(ReadOnlySpan<char> path)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "remove"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "remove" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Remove(string path)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "remove"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(ReadOnlySpan<byte> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "replace"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(ReadOnlySpan<char> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "replace"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The replacement value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Replace(string path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "replace"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "move"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "move"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from, escapeName: false, nameRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "move"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "copy"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "copy"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from, escapeName: false, nameRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
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
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "copy"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("from"u8, from.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path as UTF-8 bytes.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(ReadOnlySpan<byte> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "test"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(ReadOnlySpan<char> path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "test"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "test" operation to the patch.
    /// </summary>
    /// <param name="path">The target JSON Pointer path.</param>
    /// <param name="value">The expected value.</param>
    /// <returns>This <see cref="PatchBuilder"/> for fluent chaining.</returns>
    public PatchBuilder Test(string path, scoped in JsonElement.Source value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "test"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path.AsSpan(), escapeName: false, nameRequiresUnescaping: false);
        value.AddAsProperty("value"u8, ref _cvb, escapeName: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Disposes the builder, transferring any outstanding metadata to
    /// the backing document so pooled memory is returned.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed && _builder is not null)
        {
            _cvb.EndArray();
            ((IMutableJsonDocument)_builder).SetAndDispose(ref _cvb);
            _disposed = true;
        }
    }

    /// <summary>
    /// Adds an "add" operation to the patch using a <see cref="JsonElement"/> value directly.
    /// </summary>
    internal PatchBuilder AddElement(ReadOnlySpan<byte> path, in JsonElement value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "add"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty<JsonElement>("value"u8, value, escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }

    /// <summary>
    /// Adds a "replace" operation to the patch using a <see cref="JsonElement"/> value directly.
    /// </summary>
    internal PatchBuilder ReplaceElement(ReadOnlySpan<byte> path, in JsonElement value)
    {
        ComplexValueBuilder.ComplexValueHandle item = _cvb.StartItem();
        _cvb.StartObject();
        _cvb.AddProperty("op"u8, "replace"u8, escapeName: false, escapeValue: false, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty("path"u8, path, escapeName: false, escapeValue: true, nameRequiresUnescaping: false, valueRequiresUnescaping: false);
        _cvb.AddProperty<JsonElement>("value"u8, value, escapeName: false, nameRequiresUnescaping: false);
        _cvb.EndObject();
        _cvb.EndItem(item);
        return this;
    }
}