// <copyright file="ParsedValue{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json;

/// <summary>
/// Represents a parsed instance of a type.
/// </summary>
/// <typeparam name="T">The type of the <see cref="IJsonValue"/> to parse.</typeparam>
/// <remarks>
/// This provides a disposable wrapper around an underlying <see cref="JsonDocument"/> and the parsed value.
/// It saves you writing the boilerplate code to create and dispose the <see cref="JsonDocument"/> when you're done with it.
/// </remarks>
public readonly struct ParsedValue<T> : IDisposable
    where T : struct, IJsonValue<T>
{
    private readonly JsonDocument jsonDocument;

    private ParsedValue(JsonDocument jsonDocument, T value)
    {
        this.jsonDocument = jsonDocument;
        this.Instance = value;
    }

    /// <summary>
    /// Gets the instance of the parsed value.
    /// </summary>
    public T Instance { get; }

    /// <summary>
    /// Parse a JSON document into a value.
    /// </summary>
    /// <param name="utf8Json">The UTF8 JSON stream to parse.</param>
    /// <returns>The parsed value.</returns>
    public static ParsedValue<T> Parse(Stream utf8Json)
    {
        var document = JsonDocument.Parse(utf8Json);
        return new(document, T.FromJson(document.RootElement));
    }

    /// <summary>
    /// Parse a JSON document into a value.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>The parsed value.</returns>
    public static ParsedValue<T> Parse(string json)
    {
        var document = JsonDocument.Parse(json);
        return new(document, T.FromJson(document.RootElement));
    }

    /// <summary>
    /// Parse a JSON document into a value.
    /// </summary>
    /// <param name="utf8Json">The JSON string to parse.</param>
    /// <returns>The parsed value.</returns>
    public static ParsedValue<T> Parse(ReadOnlyMemory<byte> utf8Json)
    {
        var document = JsonDocument.Parse(utf8Json);
        return new(document, T.FromJson(document.RootElement));
    }

    /// <summary>
    /// Parse a JSON document into a value.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <returns>The parsed value.</returns>
    public static ParsedValue<T> Parse(ReadOnlyMemory<char> json)
    {
        var document = JsonDocument.Parse(json);
        return new(document, T.FromJson(document.RootElement));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (this.jsonDocument is JsonDocument d)
        {
            d.Dispose();
        }
    }
}