// <copyright file="IdentityBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>Adds an identity's tags to <paramref name="builder"/> from <paramref name="state"/> — the callback of <see cref="SecurityTagSet.Build{TState}"/>.</summary>
/// <typeparam name="TState">The state type (carried by reference so the callback can be a <see langword="static"/> lambda).</typeparam>
/// <param name="builder">The identity builder writing into a pooled buffer.</param>
/// <param name="state">The build state.</param>
public delegate void SecurityTagBuildAction<TState>(ref IdentityBuilder builder, in TState state);

/// <summary>
/// Builds a <see cref="SecurityTagSet"/> tag by tag straight into a pooled JSON buffer (design §14.2 / §16.5.4), so an
/// identity assembled from another UTF-8 source (a directory response, a delimited column) flows <strong>bytes to
/// bytes</strong> — never materializing a managed <see cref="string"/> per key or value, and never an intermediate
/// <see cref="SecurityTag"/> list. Obtained only from <see cref="SecurityTagSet.Build{TState}"/>, which owns the pooled
/// writer's lifetime; the builder itself is a <see langword="ref"/> struct so it cannot escape that scope.
/// </summary>
/// <remarks>
/// The fast path is <see cref="Add(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>: a key as a UTF-8 literal
/// (<c>"sys:tenant"u8</c>) and a value as the unescaped UTF-8 span the source already holds
/// (<c>reader.GetUtf8String().Span</c>) — the value is JSON-escaped straight into the buffer. The
/// <see cref="Add(ReadOnlySpan{byte}, string)"/> overload exists only for the rare case where a deployment genuinely has a
/// <see cref="string"/> in hand (a computed value from a string-typed API); the span overload is the default.
/// </remarks>
public ref struct IdentityBuilder
{
    private readonly Utf8JsonWriter writer;
    private int count;

    internal IdentityBuilder(Utf8JsonWriter writer)
    {
        this.writer = writer;
        this.count = 0;
    }

    internal readonly int Count => this.count;

    /// <summary>Adds one tag from UTF-8 spans — the zero-allocation path (the value is JSON-escaped straight into the pooled buffer; no managed string is created).</summary>
    /// <param name="key">The tag key as UTF-8 (typically a <c>"sys:…"u8</c> literal).</param>
    /// <param name="value">The tag value as unescaped UTF-8 (e.g. the source reader's <c>GetUtf8String().Span</c>).</param>
    public void Add(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        this.writer.WriteStartObject();
        this.writer.WriteString(KeyUtf8, key);
        this.writer.WriteString(ValueUtf8, value);
        this.writer.WriteEndObject();
        this.count++;
    }

    /// <summary>Adds one tag with a UTF-8 key and a <see cref="string"/> value — the opt-in path for a value a deployment computed through a string-typed API.</summary>
    /// <param name="key">The tag key as UTF-8 (typically a <c>"sys:…"u8</c> literal).</param>
    /// <param name="value">The tag value.</param>
    public void Add(ReadOnlySpan<byte> key, string value)
    {
        this.writer.WriteStartObject();
        this.writer.WriteString(KeyUtf8, key);
        this.writer.WriteString(ValueUtf8, value);
        this.writer.WriteEndObject();
        this.count++;
    }

    // The bare-array property names, matching SecurityTagSet's canonical { "key", "value" } form.
    private static ReadOnlySpan<byte> KeyUtf8 => "key"u8;

    private static ReadOnlySpan<byte> ValueUtf8 => "value"u8;
}