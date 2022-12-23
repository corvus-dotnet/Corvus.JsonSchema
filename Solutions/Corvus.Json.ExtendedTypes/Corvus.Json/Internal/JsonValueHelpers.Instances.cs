// <copyright file="JsonValueHelpers.Instances.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text.Json;

namespace Corvus.Json.Internal;

/// <summary>
/// Methods that help you to implement <see cref="IJsonValue{T}"/>.
/// </summary>
public static partial class JsonValueHelpers
{
    /// <summary>
    /// Gets a null JsonElement.
    /// </summary>
    public static readonly JsonElement NullElement = CreateNullInstance();

    /// <summary>
    /// Gets a JsonElement representing True.
    /// </summary>
    public static readonly JsonElement TrueElement = CreateBoolInstance(true);

    /// <summary>
    /// Gets a JsonElement representing False.
    /// </summary>
    public static readonly JsonElement FalseElement = CreateBoolInstance(false);

    private static JsonElement CreateNullInstance()
    {
        using var doc = JsonDocument.Parse("null");
        return doc.RootElement.Clone();
    }

    private static JsonElement CreateBoolInstance(bool value)
    {
        var abw = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(abw);
        writer.WriteBooleanValue(value);
        writer.Flush();
        var reader = new Utf8JsonReader(abw.WrittenSpan);
        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.Clone();
    }
}