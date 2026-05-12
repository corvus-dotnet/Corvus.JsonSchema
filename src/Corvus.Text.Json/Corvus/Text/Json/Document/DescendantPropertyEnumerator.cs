// <copyright file="DescendantPropertyEnumerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

/// <summary>
/// A zero-allocation enumerator that yields every value in the subtree whose
/// containing property has the specified name.
/// </summary>
/// <remarks>
/// <para>
/// The scan is flat: it walks the metadata DB row by row without recursion or
/// enumerator construction, yielding every <c>PropertyName</c> token whose
/// unescaped name equals the target. Values are returned as <see cref="JsonElement"/>
/// instances pointing at the row immediately after the matching property name.
/// </para>
/// <para>
/// Create via <see cref="JsonElement.EnumerateDescendantProperties(ReadOnlySpan{byte})"/>.
/// </para>
/// </remarks>
[DebuggerDisplay("{Current,nq}")]
public ref struct DescendantPropertyEnumerator
{
    private readonly IJsonDocument _document;
    private readonly int _elementIndex;
    private readonly ReadOnlySpan<byte> _utf8PropertyName;
    private int _scanIndex;
    private int _currentValueIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="DescendantPropertyEnumerator"/> struct.
    /// </summary>
    /// <param name="document">The backing document.</param>
    /// <param name="elementIndex">The metadata-DB index of the root element to scan.</param>
    /// <param name="utf8PropertyName">The unescaped UTF-8 property name to match.</param>
    internal DescendantPropertyEnumerator(
        IJsonDocument document,
        int elementIndex,
        ReadOnlySpan<byte> utf8PropertyName)
    {
        _document = document;
        _elementIndex = elementIndex;
        _utf8PropertyName = utf8PropertyName;
        _scanIndex = elementIndex + DbRow.Size;
        _currentValueIndex = -1;
    }

    /// <summary>
    /// Gets the current matched value element.
    /// </summary>
    public JsonElement Current
    {
        get
        {
            Debug.Assert(_currentValueIndex >= 0);
            return new JsonElement(_document, _currentValueIndex);
        }
    }

    /// <summary>
    /// Advances to the next matching descendant property value.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if a match was found; <see langword="false"/>
    /// if the subtree is exhausted.
    /// </returns>
    public bool MoveNext()
    {
        return _document.TryFindNextDescendantPropertyValue(
            _elementIndex,
            ref _scanIndex,
            _utf8PropertyName,
            out _currentValueIndex);
    }

    /// <summary>
    /// Returns this instance as its own enumerator (supports <c>foreach</c>).
    /// </summary>
    /// <returns>This enumerator.</returns>
    public DescendantPropertyEnumerator GetEnumerator() => this;
}