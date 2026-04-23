// <copyright file="YamlEvent.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System;

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Represents a single YAML parse event with zero-copy access to the underlying UTF-8 data.
/// </summary>
/// <remarks>
/// <para>
/// This is a <see langword="ref struct"/> because it contains <see cref="ReadOnlySpan{T}"/>
/// fields that point directly into the source YAML buffer. The event is only valid for the
/// duration of the <see cref="YamlEventCallback"/> invocation.
/// </para>
/// </remarks>
public readonly ref struct YamlEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    internal YamlEvent(YamlEventType type, int line, int column)
    {
        this.Type = type;
        this.Line = line;
        this.Column = column;
        this.Value = default;
        this.ScalarStyle = default;
        this.Anchor = default;
        this.Tag = default;
        this.IsImplicit = false;
        this.IsFlowStyle = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct for document
    /// start or end events.
    /// </summary>
    /// <param name="type">The event type (<see cref="YamlEventType.DocumentStart"/>
    /// or <see cref="YamlEventType.DocumentEnd"/>).</param>
    /// <param name="isImplicit">Whether the document boundary is implicit.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    internal YamlEvent(YamlEventType type, bool isImplicit, int line, int column)
    {
        this.Type = type;
        this.Line = line;
        this.Column = column;
        this.Value = default;
        this.ScalarStyle = default;
        this.Anchor = default;
        this.Tag = default;
        this.IsImplicit = isImplicit;
        this.IsFlowStyle = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct for collection
    /// start events (mapping or sequence) with optional anchor and tag.
    /// </summary>
    /// <param name="type">The event type (<see cref="YamlEventType.MappingStart"/>
    /// or <see cref="YamlEventType.SequenceStart"/>).</param>
    /// <param name="anchor">The anchor name, if any.</param>
    /// <param name="tag">The tag text, if any.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    internal YamlEvent(
        YamlEventType type,
        ReadOnlySpan<byte> anchor,
        ReadOnlySpan<byte> tag,
        bool isFlowStyle,
        int line,
        int column)
    {
        this.Type = type;
        this.Line = line;
        this.Column = column;
        this.Value = default;
        this.ScalarStyle = default;
        this.Anchor = anchor;
        this.Tag = tag;
        this.IsImplicit = false;
        this.IsFlowStyle = isFlowStyle;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct for alias events.
    /// </summary>
    /// <param name="aliasName">The anchor name referenced by the alias (without leading <c>*</c>).</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    internal static YamlEvent Alias(ReadOnlySpan<byte> aliasName, int line, int column)
    {
        return new YamlEvent(aliasName, line, column);
    }

    /// <summary>
    /// Initializes a new instance for alias events (private, used by <see cref="Alias"/>).
    /// </summary>
    private YamlEvent(ReadOnlySpan<byte> aliasName, int line, int column)
    {
        this.Type = YamlEventType.Alias;
        this.Line = line;
        this.Column = column;
        this.Value = aliasName;
        this.ScalarStyle = default;
        this.Anchor = default;
        this.Tag = default;
        this.IsImplicit = false;
        this.IsFlowStyle = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct with all fields.
    /// </summary>
    /// <param name="type">The event type.</param>
    /// <param name="value">The scalar value or alias name bytes.</param>
    /// <param name="scalarStyle">The scalar style.</param>
    /// <param name="anchor">The anchor name, if any.</param>
    /// <param name="tag">The tag text, if any.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <param name="isImplicit">Whether the document start/end is implicit.</param>
    internal YamlEvent(
        YamlEventType type,
        ReadOnlySpan<byte> value,
        YamlScalarStyle scalarStyle,
        ReadOnlySpan<byte> anchor,
        ReadOnlySpan<byte> tag,
        int line,
        int column,
        bool isImplicit)
    {
        this.Type = type;
        this.Value = value;
        this.ScalarStyle = scalarStyle;
        this.Anchor = anchor;
        this.Tag = tag;
        this.Line = line;
        this.Column = column;
        this.IsImplicit = isImplicit;
        this.IsFlowStyle = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlEvent"/> struct for scalar events.
    /// </summary>
    /// <param name="value">The scalar value bytes.</param>
    /// <param name="scalarStyle">The scalar style.</param>
    /// <param name="anchor">The anchor name, if any.</param>
    /// <param name="tag">The tag text, if any.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    internal YamlEvent(
        ReadOnlySpan<byte> value,
        YamlScalarStyle scalarStyle,
        ReadOnlySpan<byte> anchor,
        ReadOnlySpan<byte> tag,
        int line,
        int column)
    {
        this.Type = YamlEventType.Scalar;
        this.Value = value;
        this.ScalarStyle = scalarStyle;
        this.Anchor = anchor;
        this.Tag = tag;
        this.Line = line;
        this.Column = column;
        this.IsImplicit = false;
        this.IsFlowStyle = false;
    }

    /// <summary>
    /// Gets the event type.
    /// </summary>
    public YamlEventType Type { get; }

    /// <summary>
    /// Gets the scalar value or alias anchor name as raw UTF-8 bytes.
    /// </summary>
    /// <remarks>
    /// <para>For <see cref="YamlEventType.Scalar"/> events, this contains the processed scalar
    /// value (escape sequences resolved, folding applied).</para>
    /// <para>For <see cref="YamlEventType.Alias"/> events, this contains the anchor name
    /// (without the leading <c>*</c>).</para>
    /// <para>For all other event types, this is empty.</para>
    /// </remarks>
    public ReadOnlySpan<byte> Value { get; }

    /// <summary>
    /// Gets the scalar style. Only meaningful for <see cref="YamlEventType.Scalar"/> events.
    /// </summary>
    public YamlScalarStyle ScalarStyle { get; }

    /// <summary>
    /// Gets the anchor name (without the leading <c>&amp;</c>) if the node has an anchor,
    /// or an empty span otherwise.
    /// </summary>
    public ReadOnlySpan<byte> Anchor { get; }

    /// <summary>
    /// Gets the tag text as it appears in the source (e.g. <c>!!str</c>, <c>!local</c>,
    /// <c>!&lt;tag:yaml.org,2002:str&gt;</c>), or an empty span if no tag is present.
    /// </summary>
    public ReadOnlySpan<byte> Tag { get; }

    /// <summary>
    /// Gets the 1-based line number where this event starts.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the 1-based column number where this event starts.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets a value indicating whether the document start or end is implicit (no explicit
    /// <c>---</c> or <c>...</c> marker). Only meaningful for <see cref="YamlEventType.DocumentStart"/>
    /// and <see cref="YamlEventType.DocumentEnd"/> events.
    /// </summary>
    public bool IsImplicit { get; }

    /// <summary>
    /// Gets a value indicating whether a collection (mapping or sequence) uses flow style
    /// (<c>{}</c> or <c>[]</c>). Only meaningful for <see cref="YamlEventType.MappingStart"/>
    /// and <see cref="YamlEventType.SequenceStart"/> events.
    /// </summary>
    public bool IsFlowStyle { get; }
}