// <copyright file="HeaderValueParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.OpenApi;

/// <summary>
/// Zero-allocation helpers for parsing HTTP response header values
/// into strongly-typed JSON schema values.
/// </summary>
/// <remarks>
/// <para>
/// These methods are called by generated response structs to
/// lazily deserialize header values on first access.
/// </para>
/// <para>
/// Scalar values use <see cref="FixedJsonValueDocument{T}"/>
/// backed by a thread-local pool — zero heap allocation.
/// The document is registered with the workspace for lifetime management.
/// </para>
/// </remarks>
public static class HeaderValueParser
{
    /// <summary>
    /// Parses a raw header string as a JSON number value.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (e.g. "42", "3.14").</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseNumber<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForNumberFromSpan(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Parses a raw header string as a JSON string value.
    /// JSON escaping and quoting are applied automatically.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (unquoted, unescaped).</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseString<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForUnescapedString(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }

    /// <summary>
    /// Parses a raw header string as a JSON boolean value.
    /// </summary>
    /// <typeparam name="T">The generated JSON element type.</typeparam>
    /// <param name="rawValue">The raw header value (<c>true</c> or <c>false</c>).</param>
    /// <param name="workspace">The workspace that will own the document's lifetime.</param>
    /// <returns>The typed element backed by a pooled document.</returns>
    public static T ParseBoolean<T>(string rawValue, JsonWorkspace workspace)
        where T : struct, IJsonElement<T>
    {
        FixedJsonValueDocument<T> doc =
            FixedJsonValueDocument<T>.ForBooleanFromSpan(rawValue.AsSpan());
        workspace.RegisterDocument(doc);
        return doc.RootElement;
    }
}